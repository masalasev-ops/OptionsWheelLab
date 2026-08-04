using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// Writes the decision record: the feasible set once per symbol, session and
/// right, and one decision per maker acting against it [D-W52].
/// </summary>
/// <remarks>
/// <b>Append-only throughout</b> [D-W3]. Nothing here updates a row, and the
/// store refuses one that tries.
/// <para>
/// <b>The set is found or created, never rewritten, and a second maker reuses
/// it.</b> That is what makes [D-W4]'s byte-identical property true by
/// construction rather than by three writes agreeing. A maker arriving with a
/// different set for the same symbol, session and right is refused rather than
/// merged: two makers holding the same position saw the same opportunities, and
/// if they did not, one of the two records is wrong and neither can be told which
/// afterwards.
/// </para>
/// <para>
/// <b>Reasons are split by which evaluator raised them</b>
/// [<see cref="GateReasonFamily"/>]. The contract-level six go beside the shared
/// candidate; the portfolio-level four go against the decision, because they were
/// computed from a book no other maker shares.
/// </para>
/// <para>
/// No clock. <paramref name="recordedAt"/> arrives from a caller that read one at
/// an entry point [D-W30].
/// </para>
/// </remarks>
public sealed class DecisionStore
{
    private readonly SqliteConnection _connection;

    public DecisionStore(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// Records one maker's decision against the set it was offered, returning the
    /// decision id.
    /// </summary>
    public long Record(
        string makerId,
        Ticker symbol,
        DateOnly session,
        OptionRight right,
        IReadOnlyList<GatedCandidate> offered,
        DecisionKind kind,
        ContractIdentity? chosen,
        long? trialId,
        int policyVersion,
        DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(makerId);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(offered);

        using var transaction = _connection.BeginTransaction(deferred: false);

        var contractIds = offered
            .Select(candidate => candidate.Candidate.Quote.Contract)
            .ToDictionary(contract => contract, contract => ContractIdOf(transaction, contract));

        var setId = FindOrCreateSet(transaction, symbol, session, right, recordedAt, offered, contractIds);
        var candidateIds = CandidateIdsFor(transaction, setId);

        var decisionId = InsertDecision(
            transaction, makerId, symbol, session, setId, kind,
            chosen is null ? null : candidateIds[contractIds[chosen]],
            trialId, policyVersion, recordedAt);

        foreach (var candidate in offered)
        {
            var candidateId = candidateIds[contractIds[candidate.Candidate.Quote.Contract]];

            foreach (var reason in candidate.Reasons.Where(r => !GateReasonFamily.IsContractLevel(r)))
            {
                InsertDecisionReason(transaction, decisionId, candidateId, setId, reason);
            }
        }

        transaction.Commit();

        return decisionId;
    }

    /// <summary>
    /// The set for this symbol, session and right, created with its candidates if
    /// it is the first maker to arrive and reused otherwise.
    /// </summary>
    private long FindOrCreateSet(
        SqliteTransaction transaction,
        Ticker symbol,
        DateOnly session,
        OptionRight right,
        DateTimeOffset generatedAt,
        IReadOnlyList<GatedCandidate> offered,
        IReadOnlyDictionary<ContractIdentity, long> contractIds)
    {
        using var read = _connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText =
            """
            SELECT feasible_sets.feasible_set_id
            FROM feasible_sets
            WHERE feasible_sets.symbol = $symbol
              AND feasible_sets.session_date = $session
              AND feasible_sets.right = $right;
            """;
        read.Parameters.AddWithValue("$symbol", symbol.Value);
        read.Parameters.AddStored("$session", session);
        read.Parameters.AddStored("$right", right);

        if (read.ExecuteScalar() is long existing)
        {
            RefuseADifferentSet(transaction, existing, offered, contractIds);
            return existing;
        }

        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO feasible_sets (symbol, session_date, right, generated_at)
            VALUES ($symbol, $session, $right, $generatedAt)
            RETURNING feasible_set_id;
            """;
        insert.Parameters.AddWithValue("$symbol", symbol.Value);
        insert.Parameters.AddStored("$session", session);
        insert.Parameters.AddStored("$right", right);
        insert.Parameters.AddStored("$generatedAt", generatedAt);

        var setId = (long)insert.ExecuteScalar()!;

        foreach (var candidate in offered)
        {
            InsertCandidate(transaction, setId, candidate, contractIds, session);
        }

        return setId;
    }

    /// <summary>
    /// A second maker presenting a set the first did not see is refused.
    /// </summary>
    /// <remarks>
    /// Compared on contract identity in the order the sets carry, which is
    /// identity order from the generator. The message names the count on both
    /// sides rather than only saying they differ, because the two ways this
    /// happens look nothing alike: a different length is a generator reading a
    /// different chain, and an equal length differing in one identity is a
    /// membership or an adjustment moving under the session.
    /// </remarks>
    private void RefuseADifferentSet(
        SqliteTransaction transaction,
        long setId,
        IReadOnlyList<GatedCandidate> offered,
        IReadOnlyDictionary<ContractIdentity, long> contractIds)
    {
        var stored = CandidateIdsFor(transaction, setId);
        var presented = offered
            .Select(candidate => contractIds[candidate.Candidate.Quote.Contract])
            .ToList();

        var same = stored.Count == presented.Count
            && presented.All(stored.ContainsKey);

        if (!same)
        {
            throw new InvalidOperationException(
                $"This maker was offered {presented.Count} candidates for a feasible set already "
                + $"holding {stored.Count}. Makers holding the same position in a name receive "
                + "byte-identical candidate sets [D-W4], and the set is stored once and referenced "
                + "rather than written per maker so that property holds by construction [D-W52]. "
                + "One of the two is wrong and nothing recorded afterwards could say which.");
        }
    }

    private void InsertCandidate(
        SqliteTransaction transaction,
        long setId,
        GatedCandidate candidate,
        IReadOnlyDictionary<ContractIdentity, long> contractIds,
        DateOnly session)
    {
        var quote = candidate.Candidate.Quote;
        var contract = quote.Contract;

        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO candidates
                (feasible_set_id, contract_id, contracts_qty, committed_capital,
                 credit, bid, ask, feature_json)
            VALUES ($set, $contract, $qty, $committed, $credit, $bid, $ask, $features)
            RETURNING candidate_id;
            """;
        insert.Parameters.AddWithValue("$set", setId);
        insert.Parameters.AddWithValue("$contract", contractIds[contract]);
        insert.Parameters.AddWithValue("$qty", 1);
        insert.Parameters.AddStored("$committed", CommittedCapital.For(contract));
        insert.Parameters.AddStored("$credit", ContractTerms.CashFor(quote.Bid));
        insert.Parameters.AddStored("$bid", quote.Bid);
        insert.Parameters.AddStored("$ask", quote.Ask);
        insert.Parameters.AddWithValue("$features", CandidateFeatures.Json(quote, session));

        var candidateId = (long)insert.ExecuteScalar()!;

        foreach (var reason in candidate.Reasons.Where(GateReasonFamily.IsContractLevel))
        {
            using var reasonInsert = _connection.CreateCommand();
            reasonInsert.Transaction = transaction;
            reasonInsert.CommandText =
                """
                INSERT INTO candidate_gate_reasons (candidate_id, reason)
                VALUES ($candidate, $reason);
                """;
            reasonInsert.Parameters.AddWithValue("$candidate", candidateId);
            reasonInsert.Parameters.AddWithValue("$reason", StoreGateReason.ToStored(reason));
            reasonInsert.ExecuteNonQuery();
        }
    }

    private long InsertDecision(
        SqliteTransaction transaction,
        string makerId,
        Ticker symbol,
        DateOnly session,
        long setId,
        DecisionKind kind,
        long? chosenCandidateId,
        long? trialId,
        int policyVersion,
        DateTimeOffset recordedAt)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO decisions
                (maker_id, decision_date, symbol, feasible_set_id, kind,
                 chosen_candidate_id, trial_id, policy_version, recorded_at)
            VALUES ($maker, $session, $symbol, $set, $kind, $chosen, $trial, $policy, $recordedAt)
            RETURNING decision_id;
            """;
        insert.Parameters.AddWithValue("$maker", makerId);
        insert.Parameters.AddStored("$session", session);
        insert.Parameters.AddWithValue("$symbol", symbol.Value);
        insert.Parameters.AddWithValue("$set", setId);
        insert.Parameters.AddWithValue("$kind", StoreDecisionKind.ToStored(kind));
        insert.Parameters.AddWithValue("$chosen", chosenCandidateId ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$trial", trialId ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$policy", policyVersion);
        insert.Parameters.AddStored("$recordedAt", recordedAt);

        return (long)insert.ExecuteScalar()!;
    }

    private void InsertDecisionReason(
        SqliteTransaction transaction,
        long decisionId,
        long candidateId,
        long setId,
        GateReason reason)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO decision_gate_reasons
                (decision_id, candidate_id, feasible_set_id, reason)
            VALUES ($decision, $candidate, $set, $reason);
            """;
        insert.Parameters.AddWithValue("$decision", decisionId);
        insert.Parameters.AddWithValue("$candidate", candidateId);
        insert.Parameters.AddWithValue("$set", setId);
        insert.Parameters.AddWithValue("$reason", StoreGateReason.ToStored(reason));
        insert.ExecuteNonQuery();
    }

    /// <summary>Contract id to candidate id, for the set.</summary>
    private Dictionary<long, long> CandidateIdsFor(SqliteTransaction transaction, long setId)
    {
        using var read = _connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText =
            """
            SELECT candidates.contract_id, candidates.candidate_id
            FROM candidates
            WHERE candidates.feasible_set_id = $set
            ORDER BY candidates.candidate_id;
            """;
        read.Parameters.AddWithValue("$set", setId);

        var ids = new Dictionary<long, long>();

        using var reader = read.ExecuteReader();

        while (reader.Read())
        {
            ids[reader.GetInt64(0)] = reader.GetInt64(1);
        }

        return ids;
    }

    private long ContractIdOf(SqliteTransaction transaction, ContractIdentity contract)
    {
        using var read = _connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText =
            """
            SELECT contracts.contract_id
            FROM contracts
            WHERE contracts.symbol = $symbol
              AND contracts.expiry = $expiry
              AND contracts.right = $right
              AND contracts.strike = $strike
              AND contracts.deliverable_shares = $deliverable;
            """;
        read.Parameters.AddWithValue("$symbol", contract.Underlying.Value);
        read.Parameters.AddStored("$expiry", contract.Expiry);
        read.Parameters.AddStored("$right", contract.Right);
        read.Parameters.AddStored("$strike", contract.Strike);
        read.Parameters.AddWithValue("$deliverable", contract.DeliverableShares);

        return read.ExecuteScalar() is long id
            ? id
            : throw new InvalidOperationException(
                $"{contract} is not in contracts, so a candidate naming it could not be recorded. "
                + "The chain is written before a decision against it [1.1].");
    }
}
