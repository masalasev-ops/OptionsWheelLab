using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.MarketData;

/// <summary>
/// The successor's terms as the adjusting authority states them, transcribed
/// and never derived [D-W36].
/// </summary>
public sealed record StatedSuccessorTerms(
    decimal Strike,
    int DeliverableShares,
    int Multiplier,
    string? VendorSymbol = null);

/// <summary>
/// The event as recorded fact: what happened, when it took effect, and the
/// ratio or amount the authority stated about it. Never an input to
/// arithmetic [D-W36].
/// </summary>
public sealed record CorporateAction(
    CorporateActionKind Kind,
    DateOnly ExDate,
    decimal? Ratio = null,
    decimal? Amount = null);

/// <summary>
/// An event on an underlying, with the successor terms the adjusting authority
/// stated when it adjusts a contract.
/// </summary>
/// <remarks>
/// The terms travel with the event because they are transcribed and never
/// derived [D-W36]. Nothing computes an adjusted strike or deliverable from a
/// ratio, so an action that adjusts a contract and states no terms cannot be
/// applied, which is a refusal rather than a guess.
/// <para>
/// Here rather than beside the state machine that reads it, because an event and
/// its stated terms are market data. A synthetic scenario states them [D-W31] and
/// Phase 8's vendor ingest will too, and neither is a position.
/// </para>
/// </remarks>
public sealed record ActionOnUnderlying(
    CorporateAction Action,
    StatedSuccessorTerms? StatedSuccessor = null);

/// <summary>
/// Mints an adjusted contract: the event row and the stated successor, atomic.
/// </summary>
/// <remarks>
/// Worker-side: the Worker is the sole writer [D-W1]. Beside
/// <see cref="ChainWriter"/> on the one-subject precedent, and like it with no
/// operator entry point: tests are the only caller until a phase needs one.
/// <para>
/// <b>No term is computed from another, ever</b> [D-W36]. The successor's
/// strike, deliverable and multiplier arrive stated; the ratio and amount are
/// recorded facts about the event. The stated strike goes through the refusing
/// decimal path on its way to identity, which is the decision's tripwire: a
/// derivation that produced a non-terminating value could not be stored at
/// all.
/// </para>
/// <para>
/// <b>One transaction, both rows.</b> An event without its consequence cannot
/// exist, and 1.4's shape applies: the rollback is observed by the tests, not
/// assumed.
/// </para>
/// </remarks>
public sealed class CorporateActionWriter
{
    private readonly SqliteConnection _connection;

    public CorporateActionWriter(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// Records the event and inserts the stated successor with its predecessor
    /// link, returning the successor's contract id.
    /// </summary>
    public long MintSuccessor(
        long predecessorContractId,
        StatedSuccessorTerms stated,
        CorporateAction action,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(stated);
        ArgumentNullException.ThrowIfNull(action);

        if (stated.Multiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stated),
                stated.Multiplier,
                "A multiplier is a positive stated term [D-W36].");
        }

        using var transaction = _connection.BeginTransaction(deferred: false);

        var predecessor = ReadPredecessor(transaction, predecessorContractId);

        // Symbol, expiry and right carry over from the predecessor; the
        // adjustment states the strike and the deliverable. Building the
        // successor identity runs the stated strike through the refusing
        // decimal path [D-W36's tripwire].
        var successor = ContractIdentity.Of(
            predecessor.Underlying,
            predecessor.Expiry,
            predecessor.Right,
            stated.Strike,
            stated.DeliverableShares);

        if (successor == predecessor)
        {
            throw new InvalidOperationException(
                $"The stated terms give the successor the predecessor's own identity, "
                + $"{predecessor}. An adjustment that changes nothing is not an adjustment: "
                + "either the strike or the deliverable moves, or there is no event to "
                + "record. No row was written.");
        }

        InsertEvent(transaction, predecessor.Underlying, action, observedAt);
        var successorId = InsertSuccessor(transaction, predecessorContractId, successor, stated);

        transaction.Commit();

        return successorId;
    }

    private ContractIdentity ReadPredecessor(SqliteTransaction transaction, long contractId)
    {
        using var read = _connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText =
            """
            SELECT symbol, expiry, right, strike, deliverable_shares
            FROM contracts
            WHERE contract_id = $id;
            """;
        read.Parameters.AddWithValue("$id", contractId);

        using var reader = read.ExecuteReader();

        if (!reader.Read())
        {
            throw new InvalidOperationException(
                $"No contract with id {contractId} exists to adjust. The predecessor is "
                + "a stored row, not a description. No row was written.");
        }

        return ContractIdentity.Of(
            Ticker.Normalise(reader.GetString(0)),
            StoreDate.ParseStored(reader.GetString(1)),
            StoreOptionRight.ParseStored(reader.GetString(2)),
            StoreDecimal.ParseStored(reader.GetString(3)),
            reader.GetInt32(4));
    }

    private void InsertEvent(
        SqliteTransaction transaction,
        Ticker symbol,
        CorporateAction action,
        DateTimeOffset observedAt)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO corporate_actions (symbol, ex_date, kind, ratio, amount, observed_at)
            VALUES ($symbol, $exDate, $kind, $ratio, $amount, $observed);
            """;
        insert.Parameters.AddWithValue("$symbol", symbol.Value);
        insert.Parameters.AddStored("$exDate", action.ExDate);
        insert.Parameters.AddWithValue("$kind", StoreCorporateActionKind.ToStored(action.Kind));
        insert.Parameters.AddStored("$ratio", action.Ratio);
        insert.Parameters.AddStored("$amount", action.Amount);
        insert.Parameters.AddStored("$observed", observedAt);
        insert.ExecuteNonQuery();
    }

    // The instant stamps the event row only: contracts carry no observation
    // stamp, a corporate action minting a new identity rather than restating a
    // row [§4.1].
    private long InsertSuccessor(
        SqliteTransaction transaction,
        long predecessorContractId,
        ContractIdentity successor,
        StatedSuccessorTerms stated)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO contracts
                (symbol, expiry, right, strike, vendor_symbol,
                 predecessor_contract_id, multiplier, deliverable_shares)
            VALUES ($symbol, $expiry, $right, $strike, $vendorSymbol,
                    $predecessor, $multiplier, $deliverable)
            RETURNING contract_id;
            """;
        insert.Parameters.AddWithValue("$symbol", successor.Underlying.Value);
        insert.Parameters.AddStored("$expiry", successor.Expiry);
        insert.Parameters.AddStored("$right", successor.Right);
        insert.Parameters.AddStored("$strike", successor.Strike);
        insert.Parameters.AddWithValue(
            "$vendorSymbol", stated.VendorSymbol ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$predecessor", predecessorContractId);
        insert.Parameters.AddWithValue("$multiplier", stated.Multiplier);
        insert.Parameters.AddWithValue("$deliverable", successor.DeliverableShares);

        return (long)insert.ExecuteScalar()!;
    }
}
