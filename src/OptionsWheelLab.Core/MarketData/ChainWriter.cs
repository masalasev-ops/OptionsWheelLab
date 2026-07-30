using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Core.MarketData;

/// <summary>
/// Persists one loaded chain, whole, at one observation instant.
/// </summary>
/// <remarks>
/// Worker-side: the Worker is the sole writer [D-W1]. Beside
/// <see cref="AsOfMarketData"/> on the membership precedent: 1.3 put the
/// reader and writer of one subject in one folder, and an Ingest folder would
/// hold market data's only writer away from market data. No operator entry
/// point exists; tests are the only caller until Phase 8's vendor ingest needs
/// one, and a verb nothing calls is speculation.
/// <para>
/// <b>One chain, one transaction, all or nothing</b>, matching the loader's
/// fails-whole rule: a partially persisted chain must not exist for the same
/// reason a partially loaded one must not.
/// </para>
/// <para>
/// <b>The chain carries no instant and this takes one</b> [D-W30]. Every row
/// of the load is stamped with it. Re-recording at the same instant is refused
/// by the primary keys; the check here exists so the refusal can name the
/// correction path, which <c>RAISE</c> cannot: record the chain again with a
/// new instant, and both observations remain readable, each to its own as-of
/// [D-W8].
/// </para>
/// <para>
/// Every rendered value binds through <see cref="StoredParameters"/>, the
/// write-side seam. Counts have no stored form and bind directly.
/// </para>
/// </remarks>
public sealed class ChainWriter
{
    private readonly SqliteConnection _connection;

    public ChainWriter(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    public void Ingest(SyntheticChain chain, DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(chain);

        var snapshotDates = chain.Quotes
            .Select(quote => quote.SnapshotDate)
            .Distinct()
            .ToList();

        using var transaction = _connection.BeginTransaction(deferred: false);

        foreach (var snapshotDate in snapshotDates)
        {
            RefuseIfAlreadyRecorded(transaction, chain.Symbol, snapshotDate, observedAt);
        }

        foreach (var snapshotDate in snapshotDates)
        {
            InsertHeader(transaction, chain.Symbol, snapshotDate, observedAt);
        }

        foreach (var bar in chain.Bars)
        {
            InsertBar(transaction, bar, observedAt);
        }

        // Contracts before quotes: Microsoft.Data.Sqlite enables foreign keys
        // and a bare sqlite3 prompt does not, so this ordering is load-bearing
        // under the application and unchecked outside it [1.1].
        var contractIds = FindOrCreateContracts(transaction, chain);

        foreach (var quote in chain.Quotes)
        {
            InsertQuote(transaction, contractIds[quote.Contract], quote, observedAt);
        }

        transaction.Commit();
    }

    private void RefuseIfAlreadyRecorded(
        SqliteTransaction transaction,
        Ticker symbol,
        DateOnly snapshotDate,
        DateTimeOffset observedAt)
    {
        using var existing = _connection.CreateCommand();
        existing.Transaction = transaction;
        existing.CommandText =
            """
            SELECT COUNT(*)
            FROM chain_snapshots
            WHERE symbol = $symbol
              AND snapshot_date = $snapshotDate
              AND observed_at = $observed;
            """;
        existing.Parameters.AddWithValue("$symbol", symbol.Value);
        existing.Parameters.AddStored("$snapshotDate", snapshotDate);
        existing.Parameters.AddStored("$observed", observedAt);

        if ((long)existing.ExecuteScalar()! == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The chain for '{symbol.Value}' on {StoreDate.ToStored(snapshotDate)} is already "
            + $"recorded at {StoreTimestamp.ToStored(observedAt)}. A snapshot is never "
            + "re-recorded at the same instant: record it again with a new observation "
            + "instant, and both observations remain readable, each to its own as-of. No row "
            + "was written.");
    }

    private void InsertHeader(
        SqliteTransaction transaction,
        Ticker symbol,
        DateOnly snapshotDate,
        DateTimeOffset observedAt)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO chain_snapshots (symbol, snapshot_date, observed_at)
            VALUES ($symbol, $snapshotDate, $observed);
            """;
        insert.Parameters.AddWithValue("$symbol", symbol.Value);
        insert.Parameters.AddStored("$snapshotDate", snapshotDate);
        insert.Parameters.AddStored("$observed", observedAt);
        insert.ExecuteNonQuery();
    }

    private void InsertBar(SqliteTransaction transaction, UnderlyingBar bar, DateTimeOffset observedAt)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO underlying_bars
                (symbol, session_date, open, high, low, close, adj_close, volume, observed_at)
            VALUES ($symbol, $sessionDate, $open, $high, $low, $close, $adjClose, $volume, $observed);
            """;
        insert.Parameters.AddWithValue("$symbol", bar.Symbol.Value);
        insert.Parameters.AddStored("$sessionDate", bar.SessionDate);
        insert.Parameters.AddStored("$open", bar.Open);
        insert.Parameters.AddStored("$high", bar.High);
        insert.Parameters.AddStored("$low", bar.Low);
        insert.Parameters.AddStored("$close", bar.Close);
        insert.Parameters.AddStored("$adjClose", bar.AdjustedClose);
        insert.Parameters.AddWithValue("$volume", bar.Volume ?? (object)DBNull.Value);
        insert.Parameters.AddStored("$observed", observedAt);
        insert.ExecuteNonQuery();
    }

    /// <summary>
    /// One store row per distinct identity the chain quotes, found or created.
    /// </summary>
    /// <remarks>
    /// <c>ON CONFLICT ... DO NOTHING</c> and never <c>DO UPDATE</c>: an upsert
    /// is impossible by construction here, because the append-only trigger
    /// refuses the update half. <c>vendor_symbol</c> and the two quantity
    /// defaults are the DDL's: a synthetic chain has no vendor and standard
    /// terms [§4.1].
    /// </remarks>
    private Dictionary<ContractIdentity, long> FindOrCreateContracts(
        SqliteTransaction transaction,
        SyntheticChain chain)
    {
        var contractIds = new Dictionary<ContractIdentity, long>();

        foreach (var identity in chain.Quotes.Select(quote => quote.Contract).Distinct())
        {
            contractIds[identity] = FindOrCreate(transaction, identity);
        }

        return contractIds;
    }

    private long FindOrCreate(SqliteTransaction transaction, ContractIdentity identity)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO contracts (symbol, expiry, right, strike)
            VALUES ($symbol, $expiry, $right, $strike)
            ON CONFLICT (symbol, expiry, right, strike, deliverable_shares) DO NOTHING
            RETURNING contract_id;
            """;
        insert.Parameters.AddWithValue("$symbol", identity.Underlying.Value);
        insert.Parameters.AddStored("$expiry", identity.Expiry);
        insert.Parameters.AddStored("$right", identity.Right);
        insert.Parameters.AddStored("$strike", identity.Strike);

        if (insert.ExecuteScalar() is long created)
        {
            return created;
        }

        // The conflict case: the contract exists. A synthetic chain cannot
        // state a deliverable, so the lookup is by the four-tuple; two rows
        // can match only once an adjusted series shares the tuple, which 1.5
        // mints and §2's banner records as unsettled, so more than one match
        // refuses rather than guesses.
        using var find = _connection.CreateCommand();
        find.Transaction = transaction;
        find.CommandText =
            """
            SELECT contract_id
            FROM contracts
            WHERE symbol = $symbol
              AND expiry = $expiry
              AND right = $right
              AND strike = $strike;
            """;
        find.Parameters.AddWithValue("$symbol", identity.Underlying.Value);
        find.Parameters.AddStored("$expiry", identity.Expiry);
        find.Parameters.AddStored("$right", identity.Right);
        find.Parameters.AddStored("$strike", identity.Strike);

        var matches = new List<long>();
        using var reader = find.ExecuteReader();

        while (reader.Read())
        {
            matches.Add(reader.GetInt64(0));
        }

        return matches.Count == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"{matches.Count} contracts share the identity tuple of '{identity.Underlying.Value}' "
                + $"{StoreDate.ToStored(identity.Expiry)} {StoreOptionRight.ToStored(identity.Right)} "
                + $"{StoreDecimal.ToStored(identity.Strike)}. A synthetic chain cannot state a "
                + "deliverable to pick one, and §2 records the identity question as unsettled. "
                + "Nothing was ingested.");
    }

    private void InsertQuote(
        SqliteTransaction transaction,
        long contractId,
        ContractQuote quote,
        DateTimeOffset observedAt)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO contract_quotes
                (contract_id, snapshot_date, bid, ask, last, volume, open_interest,
                 iv, delta, gamma, theta, vega, observed_at)
            VALUES ($id, $snapshotDate, $bid, $ask, $last, $volume, $openInterest,
                    $iv, $delta, $gamma, $theta, $vega, $observed);
            """;
        insert.Parameters.AddWithValue("$id", contractId);
        insert.Parameters.AddStored("$snapshotDate", quote.SnapshotDate);
        insert.Parameters.AddStored("$bid", quote.Bid);
        insert.Parameters.AddStored("$ask", quote.Ask);
        insert.Parameters.AddStored("$last", quote.Last);
        insert.Parameters.AddWithValue("$volume", quote.Volume ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$openInterest", quote.OpenInterest ?? (object)DBNull.Value);
        insert.Parameters.AddStored("$iv", quote.ImpliedVolatility);
        insert.Parameters.AddStored("$delta", quote.Delta);
        insert.Parameters.AddStored("$gamma", quote.Gamma);
        insert.Parameters.AddStored("$theta", quote.Theta);
        insert.Parameters.AddStored("$vega", quote.Vega);
        insert.Parameters.AddStored("$observed", observedAt);
        insert.ExecuteNonQuery();
    }
}
