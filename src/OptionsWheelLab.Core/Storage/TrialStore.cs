using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The trial record and the two projections over it [D-W35].
/// </summary>
/// <remarks>
/// Worker-side: the Worker is the sole writer [D-W1]. Beside
/// <see cref="MarketData.ChainWriter"/> and
/// <see cref="MarketData.CorporateActionWriter"/> on the one-subject precedent,
/// and like them with no operator entry point, since tests are the only caller
/// until a phase needs one.
/// <para>
/// <b>Appending to the ledger and writing the projections are separate acts, and
/// only one of them is append-only.</b> <see cref="Append"/> writes the record
/// and the store's triggers refuse any rewrite of it. <see cref="Rebuild"/>
/// discards the projections and reconstructs them, which is exactly what [D-W35]
/// permits and exactly what makes them projections rather than rewritable tables
/// with a flattering name.
/// </para>
/// <para>
/// <b>The rebuild reads the ledger and <c>contracts</c>, and nothing else.</b>
/// Both are append-only, which is the condition on deriving anything from them:
/// a projection derived from something rewritable would reconstruct a different
/// answer after the source moved.
/// </para>
/// </remarks>
public sealed class TrialStore
{
    private readonly SqliteConnection _connection;

    public TrialStore(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// Opens a trial, returning its id.
    /// </summary>
    /// <remarks>
    /// <paramref name="makerId"/> is supplied rather than rebuilt, because it is
    /// the one column the ledger cannot carry: which maker opened a trial is a
    /// fact about a decision, and <c>decisions</c> lands at Phase 4 [§4.3].
    /// </remarks>
    public long OpenTrial(string makerId, Ticker symbol, DateOnly openedOn, decimal openStrike)
    {
        ArgumentNullException.ThrowIfNull(makerId);
        ArgumentNullException.ThrowIfNull(symbol);

        using var insert = _connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO trials
                (maker_id, symbol, opened_on, open_strike, committed_capital, rolls_used)
            VALUES ($maker, $symbol, $openedOn, $openStrike, $committed, 0)
            RETURNING trial_id;
            """;
        insert.Parameters.AddWithValue("$maker", makerId);
        insert.Parameters.AddWithValue("$symbol", symbol.Value);
        insert.Parameters.AddStored("$openedOn", openedOn);
        insert.Parameters.AddStored("$openStrike", openStrike);
        insert.Parameters.AddStored(
            "$committed", openStrike * Identity.ContractTerms.StandardMultiplier);

        return (long)insert.ExecuteScalar()!;
    }

    /// <summary>
    /// Appends entries to the ledger, which is never rewritten [D-W35].
    /// </summary>
    /// <remarks>
    /// One transaction, on 1.4's shape: a session's entries are one act, and half
    /// a session in the record would rebuild into a state the account was never
    /// in.
    /// </remarks>
    public void Append(long trialId, IReadOnlyList<LedgerEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        using var transaction = _connection.BeginTransaction(deferred: false);

        foreach (var entry in entries)
        {
            using var insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO ledger_entries
                    (trial_id, entry_date, known_on, kind, amount, contract_id, note)
                VALUES ($trial, $entryDate, $knownOn, $kind, $amount, $contract, $note);
                """;
            insert.Parameters.AddWithValue("$trial", trialId);
            insert.Parameters.AddStored("$entryDate", entry.EntryDate);
            insert.Parameters.AddStored("$knownOn", entry.KnownOn);
            insert.Parameters.AddWithValue("$kind", StoreLedgerEntryKind.ToStored(entry.Kind));
            insert.Parameters.AddStored("$amount", entry.Amount);
            insert.Parameters.AddWithValue(
                "$contract", ContractIdOf(transaction, entry.Contract) ?? (object)DBNull.Value);
            insert.Parameters.AddWithValue("$note", entry.Note ?? (object)DBNull.Value);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>
    /// The trial's entries, in the order they were written.
    /// </summary>
    /// <remarks>
    /// Ordered by <c>entry_date</c> then <c>entry_id</c>, which is the index
    /// migration 8 creates for exactly this read. The id breaks a tie within one
    /// session, and a session's entries are written in the order the machine
    /// produced them, so a roll's two legs come back the way they went in.
    /// </remarks>
    public IReadOnlyList<LedgerEntry> EntriesFor(long trialId)
    {
        var entries = new List<LedgerEntry>();

        using var read = _connection.CreateCommand();
        read.CommandText =
            """
            SELECT ledger_entries.entry_date,
                   ledger_entries.known_on,
                   ledger_entries.kind,
                   ledger_entries.amount,
                   ledger_entries.contract_id,
                   ledger_entries.note
            FROM ledger_entries
            WHERE ledger_entries.trial_id = $trial
            ORDER BY ledger_entries.entry_date, ledger_entries.entry_id;
            """;
        read.Parameters.AddWithValue("$trial", trialId);

        using var reader = read.ExecuteReader();

        while (reader.Read())
        {
            entries.Add(new LedgerEntry(
                StoreDate.ParseStored(reader.GetString(0)),
                StoreDate.ParseStored(reader.GetString(1)),
                StoreLedgerEntryKind.ParseStored(reader.GetString(2)),
                StoreDecimal.ParseStored(reader.GetString(3)),
                reader.IsDBNull(4) ? null : ContractOf(reader.GetInt64(4)),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return entries;
    }

    /// <summary>
    /// Discards the projections for a trial and reconstructs them from the
    /// ledger [D-W35].
    /// </summary>
    /// <remarks>
    /// <b><c>maker_id</c> survives the discard because it is read back first.</b>
    /// The ledger cannot supply it and this method will not invent it, so a
    /// rebuild preserves the attribution it found and reconstructs everything
    /// else. From Phase 4 the source is <c>decisions</c>, which is a record like
    /// the ledger, and the preservation becomes a read.
    /// </remarks>
    public void Rebuild(long trialId, AsOfConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var entries = EntriesFor(trialId);

        Rebuild(trialId, entries, TrialBounds.ResolveFor(configuration, OpenedOn(entries)));
    }

    /// <summary>
    /// The session the trial opened, read from the ledger [D-W53].
    /// </summary>
    /// <remarks>
    /// <b>Not <c>trials.opened_on</c>, and the difference is the point.</b> That
    /// column is in a projection this method is about to discard and rewrite
    /// [D-W35], so resolving the bounds from it would make the rebuild depend on
    /// its own previous output. The ledger is append-only, and its first entry is
    /// the sale that opens a trial [<see cref="TrialProjection.Replay"/>], which
    /// is the same date the replay will arrive at independently.
    /// </remarks>
    private static DateOnly OpenedOn(IReadOnlyList<LedgerEntry> entries) =>
        entries.Count > 0
            ? entries[0].EntryDate
            : throw new InvalidOperationException(
                "A trial with no ledger entries has no open to resolve its bounds as of. A "
                + "trial runs from first open through to return to cash [D-W14], and the open "
                + "is a sale that writes an entry.");

    /// <summary>
    /// Reconstructs the projections against bounds the caller has resolved.
    /// </summary>
    /// <remarks>
    /// <b>A caller supplying bounds is asserting which ones the run used</b>, and
    /// a caller that supplies the same literal the run was built from proves
    /// nothing about the resolution [D-W53]. The overload taking configuration
    /// resolves them from the store, and it is the one a rebuild off a real run
    /// takes.
    /// </remarks>
    public void Rebuild(long trialId, TrialBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);

        Rebuild(trialId, EntriesFor(trialId), bounds);
    }

    private void Rebuild(long trialId, IReadOnlyList<LedgerEntry> entries, TrialBounds bounds)
    {
        var projected = TrialProjection.Rebuild(entries, bounds);
        var makerId = MakerOf(trialId);

        using var transaction = _connection.BeginTransaction(deferred: false);

        Execute(transaction, "DELETE FROM positions WHERE trial_id = $trial;", trialId);
        Execute(transaction, "DELETE FROM trials WHERE trial_id = $trial;", trialId);

        using (var trial = _connection.CreateCommand())
        {
            trial.Transaction = transaction;
            trial.CommandText =
                """
                INSERT INTO trials
                    (trial_id, maker_id, symbol, opened_on, closed_on, open_strike,
                     committed_capital, rolls_used, close_kind)
                VALUES ($id, $maker, $symbol, $openedOn, $closedOn, $openStrike,
                        $committed, $rolls, $closeKind);
                """;
            trial.Parameters.AddWithValue("$id", trialId);
            trial.Parameters.AddWithValue("$maker", makerId);
            trial.Parameters.AddWithValue("$symbol", projected.Symbol.Value);
            trial.Parameters.AddStored("$openedOn", projected.OpenedOn);
            trial.Parameters.AddWithValue(
                "$closedOn",
                projected.ClosedOn is { } closed
                    ? StoreDate.ToStored(closed)
                    : (object)DBNull.Value);
            trial.Parameters.AddStored("$openStrike", projected.OpenStrike);
            trial.Parameters.AddStored("$committed", projected.CommittedCapital);
            trial.Parameters.AddWithValue("$rolls", projected.RollsUsed);
            trial.Parameters.AddWithValue(
                "$closeKind",
                projected.CloseKind is { } kind
                    ? StoreTrialCloseKind.ToStored(kind)
                    : (object)DBNull.Value);
            trial.ExecuteNonQuery();
        }

        foreach (var position in projected.Positions)
        {
            using var insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO positions
                    (trial_id, state, effective_from, effective_to, shares,
                     gross_basis, net_basis, contract_id)
                VALUES ($trial, $state, $from, $to, $shares, $gross, $net, $contract);
                """;
            insert.Parameters.AddWithValue("$trial", trialId);
            insert.Parameters.AddWithValue("$state", StorePositionState.ToStored(position.State));
            insert.Parameters.AddStored("$from", position.EffectiveFrom);
            insert.Parameters.AddWithValue(
                "$to",
                position.EffectiveTo is { } to ? StoreDate.ToStored(to) : (object)DBNull.Value);
            insert.Parameters.AddWithValue("$shares", position.Shares);

            // Both bases are divisions and round at the bind, visibly, which is
            // what this seam prescribes for a computed value. Gross is what the
            // assignment paid divided by the shares it delivered, and net
            // subtracts the premium per share; either can need more places than
            // the scale holds. Every other value written here is exact and takes
            // the refusing path.
            insert.Parameters.AddStoredRounded("$gross", position.GrossBasis);
            insert.Parameters.AddStoredRounded("$net", position.NetBasis);
            insert.Parameters.AddWithValue(
                "$contract",
                ContractIdOf(transaction, position.Contract) ?? (object)DBNull.Value);
            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private void Execute(SqliteTransaction transaction, string sql, long trialId)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("$trial", trialId);
        command.ExecuteNonQuery();
    }

    private string MakerOf(long trialId)
    {
        using var read = _connection.CreateCommand();
        read.CommandText = "SELECT maker_id FROM trials WHERE trial_id = $trial;";
        read.Parameters.AddWithValue("$trial", trialId);

        return read.ExecuteScalar() as string
            ?? throw new InvalidOperationException(
                $"No trial with id {trialId} exists to rebuild. A rebuild reconstructs a "
                + "trial's positions from its ledger; it does not create the trial, whose "
                + "maker the ledger cannot supply [§4.3].");
    }

    /// <summary>
    /// The stored id of a contract identity, which the ledger references by
    /// surrogate.
    /// </summary>
    /// <remarks>
    /// Identity is the five-component tuple [1.5], and the deliverable is what
    /// separates an adjusted series from a standard one at the same strike, so
    /// every component is in the lookup. A contract the store does not hold is a
    /// refusal rather than a null: an entry pointing at nothing would rebuild
    /// into a position with no instrument.
    /// </remarks>
    private long? ContractIdOf(SqliteTransaction? transaction, ContractIdentity? contract)
    {
        if (contract is null)
        {
            return null;
        }

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

        return read.ExecuteScalar() as long?
            ?? throw new InvalidOperationException(
                $"'{contract}' is not a contract this store holds, so a ledger entry cannot "
                + "reference it. The chain is written before the trial that trades it.");
    }

    private ContractIdentity ContractOf(long contractId)
    {
        using var read = _connection.CreateCommand();
        read.CommandText =
            """
            SELECT contracts.symbol,
                   contracts.expiry,
                   contracts.right,
                   contracts.strike,
                   contracts.deliverable_shares
            FROM contracts
            WHERE contracts.contract_id = $id;
            """;
        read.Parameters.AddWithValue("$id", contractId);

        using var reader = read.ExecuteReader();

        if (!reader.Read())
        {
            throw new InvalidOperationException(
                $"Ledger entry references contract {contractId}, which this store does not "
                + "hold.");
        }

        return ContractIdentity.Of(
            Ticker.Normalise(reader.GetString(0)),
            StoreDate.ParseStored(reader.GetString(1)),
            StoreOptionRight.ParseStored(reader.GetString(2)),
            StoreDecimal.ParseStored(reader.GetString(3)),
            reader.GetInt32(4));
    }
}
