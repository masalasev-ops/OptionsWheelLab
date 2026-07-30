using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// An as-of read sees what was believed then, not what the vendor said later.
/// </summary>
/// <remarks>
/// Not a registered fixture: no check is registered against 1.2, which its detail
/// states per FIXTURES rule 2, so these land unregistered the way
/// <c>ConfigWriteTests</c> does.
/// <para>
/// This is the first read of what 1.1's key change exists for. The key admits a
/// correction as a second row with its own stamp; these tests are what that was
/// FOR: a read as of before the correction still returns the original belief.
/// </para>
/// </remarks>
public sealed class AsOfMarketDataTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly DateOnly Session = new(2026, 3, 2);

    private static readonly DateTimeOffset Observed = new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstCorrection = new(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SecondCorrection = new(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_correction_recorded_after_a_date_is_invisible_at_that_date_and_visible_after()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertBar(connection, close: "52.40000000", observedAt: Observed);
        InsertBar(connection, close: "52.44000000", observedAt: FirstCorrection);

        var reads = new AsOfMarketData(connection);

        var before = reads.BarFor(Symbol, Session, asOf: new DateOnly(2026, 3, 3));
        var after = reads.BarFor(Symbol, Session, asOf: new DateOnly(2026, 3, 4));

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(52.40m, before.Close);
        Assert.Equal(52.44m, after.Close);
    }

    /// <summary>
    /// One session, three as-of dates spanning two corrections, three answers in
    /// the right order.
    /// </summary>
    [Fact]
    public void Three_as_of_dates_spanning_two_corrections_return_three_answers_in_order()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertBar(connection, close: "52.40000000", observedAt: Observed);
        InsertBar(connection, close: "52.44000000", observedAt: FirstCorrection);
        InsertBar(connection, close: "52.41000000", observedAt: SecondCorrection);

        var reads = new AsOfMarketData(connection);

        var answers = new[]
        {
            reads.BarFor(Symbol, Session, asOf: new DateOnly(2026, 3, 3)),
            reads.BarFor(Symbol, Session, asOf: new DateOnly(2026, 3, 5)),
            reads.BarFor(Symbol, Session, asOf: new DateOnly(2026, 3, 7)),
        };

        Assert.All(answers, answer => Assert.NotNull(answer));
        Assert.Equal([52.40m, 52.44m, 52.41m], answers.Select(answer => answer!.Close));
    }

    /// <summary>
    /// Before anything was observed there is nothing, not the earliest row.
    /// </summary>
    [Fact]
    public void An_as_of_date_before_the_first_observation_sees_nothing()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertBar(connection, close: "52.40000000", observedAt: Observed);

        Assert.Null(new AsOfMarketData(connection).BarFor(Symbol, Session, asOf: new DateOnly(2026, 3, 1)));
    }

    /// <summary>
    /// The observation on the as-of date itself is visible, which is the boundary
    /// <see cref="Core.Configuration.AsOfBoundary"/> exists to keep inclusive.
    /// </summary>
    [Fact]
    public void An_observation_on_the_as_of_date_itself_is_visible()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertBar(connection, close: "52.40000000", observedAt: Observed);

        Assert.NotNull(new AsOfMarketData(connection).BarFor(Symbol, Session, asOf: Session));
    }

    [Fact]
    public void A_quote_correction_is_invisible_before_its_stamp_and_visible_after()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertContract(connection, id: 1, strike: "50.00000000");
        InsertQuote(connection, contractId: 1, bid: "0.95000000", observedAt: Observed);
        InsertQuote(connection, contractId: 1, bid: "0.96000000", observedAt: FirstCorrection);

        var reads = new AsOfMarketData(connection);

        var before = reads.QuotesFor(Symbol, Session, asOf: new DateOnly(2026, 3, 3));
        var after = reads.QuotesFor(Symbol, Session, asOf: new DateOnly(2026, 3, 5));

        Assert.Equal(0.95m, Assert.Single(before).Bid);
        Assert.Equal(0.96m, Assert.Single(after).Bid);
    }

    /// <summary>
    /// The chain comes back in identity order regardless of insertion order,
    /// imposed on the parsed identities rather than in SQL, where ordering a
    /// stored decimal is forbidden because the form does not sort.
    /// </summary>
    [Fact]
    public void The_chain_returns_in_identity_order_not_insertion_order()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        // Inserted high strike first, and with a 9-vs-10 pair whose stored forms
        // sort backwards as text, which is the case SQL ordering would get wrong.
        InsertContract(connection, id: 1, strike: "10.00000000");
        InsertContract(connection, id: 2, strike: "9.00000000");
        InsertQuote(connection, contractId: 1, bid: "0.30000000", observedAt: Observed);
        InsertQuote(connection, contractId: 2, bid: "0.55000000", observedAt: Observed);

        var quotes = new AsOfMarketData(connection).QuotesFor(Symbol, Session, asOf: new DateOnly(2026, 3, 3));

        Assert.Equal(2, quotes.Count);
        Assert.Equal([9.00m, 10.00m], quotes.Select(quote => quote.Contract.Strike));
    }

    [Fact]
    public void A_chain_read_before_anything_was_observed_is_empty()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        InsertContract(connection, id: 1, strike: "50.00000000");
        InsertQuote(connection, contractId: 1, bid: "0.95000000", observedAt: Observed);

        Assert.Empty(new AsOfMarketData(connection).QuotesFor(Symbol, Session, asOf: new DateOnly(2026, 3, 1)));
    }

    private static void InsertBar(SqliteConnection connection, string close, DateTimeOffset observedAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO underlying_bars
                (symbol, session_date, open, high, low, close, adj_close, volume, observed_at)
            VALUES ($symbol, $session, '52.00000000', '52.90000000', '51.80000000', $close, $close, 1200000, $observed);
            """;
        command.Parameters.AddWithValue("$symbol", Symbol.Value);
        command.Parameters.AddWithValue("$session", StoreDate.ToStored(Session));
        command.Parameters.AddWithValue("$close", close);
        command.Parameters.AddWithValue("$observed", StoreTimestamp.ToStored(observedAt));
        command.ExecuteNonQuery();
    }

    private static void InsertContract(SqliteConnection connection, long id, string strike)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO contracts (contract_id, symbol, expiry, right, strike)
            VALUES ($id, $symbol, '2026-04-17', 'put', $strike);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$symbol", Symbol.Value);
        command.Parameters.AddWithValue("$strike", strike);
        command.ExecuteNonQuery();
    }

    private static void InsertQuote(
        SqliteConnection connection,
        long contractId,
        string bid,
        DateTimeOffset observedAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO contract_quotes (contract_id, snapshot_date, bid, ask, observed_at)
            VALUES ($id, $session, $bid, '1.05000000', $observed);
            """;
        command.Parameters.AddWithValue("$id", contractId);
        command.Parameters.AddWithValue("$session", StoreDate.ToStored(Session));
        command.Parameters.AddWithValue("$bid", bid);
        command.Parameters.AddWithValue("$observed", StoreTimestamp.ToStored(observedAt));
        command.ExecuteNonQuery();
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Observed);
        return store;
    }
}
