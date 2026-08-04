using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// Rebuilds a decision from the record alone, with no access to live state
/// [D-W3].
/// </summary>
/// <remarks>
/// <b>Its own type so the claim is checkable.</b> The definition of done is that
/// this path reads no table the record does not name, and a scan over a file
/// holding the writer too would see the inserts and prove nothing. Everything
/// this file issues is a read, so the tables it names are exactly the tables
/// re-scoring needs.
/// <para>
/// <b>Six tables are permitted and the barred set is what gives the rule its
/// meaning.</b> The five record tables, plus <c>contracts</c> because
/// <c>candidates.contract_id</c> names it and it is append-only: a corporate
/// action mints a new identity rather than editing a row [D-W36], so reading it
/// later returns what stood then.
/// </para>
/// <para>
/// Barred: <c>contract_quotes</c>, which is the live market, and
/// <c>config_rows</c>, which a re-score must not resolve as-now [D-W26]. Barred
/// too are <c>trials</c> and <c>positions</c>, and the distinction is
/// rewritability rather than convenience: they are projections [D-W35] and a
/// projection read later returns whatever it was last rebuilt to, where an
/// append-only record returns what it held.
/// </para>
/// <para>
/// Every read orders explicitly. Row order is nondeterminism in SQL that is not a
/// function [D-W51], and a re-scoring that returned a set in a different order on
/// two reads would make the record's own reproducibility depend on a query plan.
/// </para>
/// </remarks>
public sealed class DecisionRecordReader
{
    private readonly SqliteConnection _connection;

    public DecisionRecordReader(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>The decision and the whole set it was made against.</summary>
    public DecisionRecord Read(long decisionId)
    {
        using var read = _connection.CreateCommand();
        read.CommandText =
            """
            SELECT decisions.maker_id,
                   decisions.decision_date,
                   decisions.symbol,
                   decisions.kind,
                   decisions.chosen_candidate_id,
                   decisions.trial_id,
                   decisions.policy_version,
                   decisions.feasible_set_id,
                   feasible_sets.right
            FROM decisions
            JOIN feasible_sets
              ON feasible_sets.feasible_set_id = decisions.feasible_set_id
            WHERE decisions.decision_id = $decision;
            """;
        read.Parameters.AddWithValue("$decision", decisionId);

        using var reader = read.ExecuteReader();

        if (!reader.Read())
        {
            throw new InvalidOperationException(
                $"No decision {decisionId} is recorded. A decision is never rewritten and never "
                + "deleted [D-W3], so an absent one was never written rather than removed.");
        }

        var makerId = reader.GetString(0);
        var decisionDate = StoreDate.ParseStored(reader.GetString(1));
        var symbol = Ticker.Normalise(reader.GetString(2));
        var kind = StoreDecisionKind.ParseStored(reader.GetString(3));
        var chosen = reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4);
        var trialId = reader.IsDBNull(5) ? (long?)null : reader.GetInt64(5);
        var policyVersion = reader.GetInt32(6);
        var setId = reader.GetInt64(7);
        var right = StoreOptionRight.ParseStored(reader.GetString(8));

        reader.Close();

        return new DecisionRecord(
            decisionId,
            makerId,
            decisionDate,
            symbol,
            right,
            kind,
            chosen,
            trialId,
            policyVersion,
            CandidatesFor(setId, decisionId));
    }

    /// <summary>
    /// The set's candidates, each carrying the reasons this decision saw.
    /// </summary>
    /// <remarks>
    /// The contract-level reasons come from the set and are the same for every
    /// maker that shared it; the portfolio-level ones come from this decision and
    /// are not [D-W52]. Merged into the enumeration's declared order, which is the
    /// order the candidate was offered in.
    /// </remarks>
    private IReadOnlyList<RecordedCandidate> CandidatesFor(long setId, long decisionId)
    {
        var reasons = ReasonsFor(setId, decisionId);
        var candidates = new List<RecordedCandidate>();

        using var read = _connection.CreateCommand();
        read.CommandText =
            """
            SELECT candidates.candidate_id,
                   candidates.contracts_qty,
                   candidates.committed_capital,
                   candidates.credit,
                   candidates.bid,
                   candidates.ask,
                   candidates.feature_json,
                   contracts.symbol,
                   contracts.expiry,
                   contracts.right,
                   contracts.strike,
                   contracts.deliverable_shares
            FROM candidates
            JOIN contracts
              ON contracts.contract_id = candidates.contract_id
            WHERE candidates.feasible_set_id = $set
            ORDER BY candidates.candidate_id;
            """;
        read.Parameters.AddWithValue("$set", setId);

        using var reader = read.ExecuteReader();

        while (reader.Read())
        {
            var candidateId = reader.GetInt64(0);

            candidates.Add(new RecordedCandidate(
                candidateId,
                ContractIdentity.Of(
                    Ticker.Normalise(reader.GetString(7)),
                    StoreDate.ParseStored(reader.GetString(8)),
                    StoreOptionRight.ParseStored(reader.GetString(9)),
                    StoreDecimal.ParseStored(reader.GetString(10)),
                    reader.GetInt32(11)),
                reader.GetInt32(1),
                StoreDecimal.ParseStored(reader.GetString(2)),
                StoreDecimal.ParseStored(reader.GetString(3)),
                StoreDecimal.ParseStored(reader.GetString(4)),
                StoreDecimal.ParseStored(reader.GetString(5)),
                reader.GetString(6),
                reasons.TryGetValue(candidateId, out var found) ? found : []));
        }

        return candidates;
    }

    /// <summary>
    /// Both reason families for this decision, by candidate, in declared order.
    /// </summary>
    private Dictionary<long, IReadOnlyList<GateReason>> ReasonsFor(long setId, long decisionId)
    {
        var byCandidate = new Dictionary<long, List<GateReason>>();

        using var shared = _connection.CreateCommand();
        shared.CommandText =
            """
            SELECT candidate_gate_reasons.candidate_id, candidate_gate_reasons.reason
            FROM candidate_gate_reasons
            JOIN candidates
              ON candidates.candidate_id = candidate_gate_reasons.candidate_id
            WHERE candidates.feasible_set_id = $set
            ORDER BY candidate_gate_reasons.candidate_id, candidate_gate_reasons.reason;
            """;
        shared.Parameters.AddWithValue("$set", setId);

        Collect(shared, byCandidate);

        using var mine = _connection.CreateCommand();
        mine.CommandText =
            """
            SELECT decision_gate_reasons.candidate_id, decision_gate_reasons.reason
            FROM decision_gate_reasons
            WHERE decision_gate_reasons.decision_id = $decision
            ORDER BY decision_gate_reasons.candidate_id, decision_gate_reasons.reason;
            """;
        mine.Parameters.AddWithValue("$decision", decisionId);

        Collect(mine, byCandidate);

        return byCandidate.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<GateReason>)[.. entry.Value.OrderBy(reason => reason)]);
    }

    private static void Collect(SqliteCommand command, Dictionary<long, List<GateReason>> into)
    {
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var candidateId = reader.GetInt64(0);

            if (!into.TryGetValue(candidateId, out var reasons))
            {
                into[candidateId] = reasons = [];
            }

            reasons.Add(StoreGateReason.ParseStored(reader.GetString(1)));
        }
    }
}
