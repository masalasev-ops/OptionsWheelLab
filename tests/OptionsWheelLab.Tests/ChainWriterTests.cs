using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// One chain persists whole at one instant, and a second run behaves both
/// ways the detail settles: same instant refused, new instant alongside.
/// </summary>
/// <remarks>
/// Not a registered fixture, for the same reason as
/// <see cref="BarsSchemaTests"/>. The registered check at 1.4 is the
/// worked-example round trip, which has its own file.
/// </remarks>
public sealed class ChainWriterTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly DateOnly Session = new(2026, 3, 2);
    private static readonly DateOnly Expiry = new(2026, 4, 17);

    private static readonly DateTimeOffset First =
        new(2026, 3, 2, 21, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Second =
        new(2026, 3, 4, 12, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_chain_persists_and_reads_back_through_the_as_of_surface()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ChainWriter(connection).Ingest(SmallChain(), First);

        var reads = new AsOfMarketData(connection);
        var quotes = reads.QuotesFor(Symbol, Session, asOf: Session);
        var bar = reads.BarFor(Symbol, Session, asOf: Session);

        Assert.Equal(2, quotes.Count);
        Assert.Equal([47.50m, 50.00m], quotes.Select(quote => quote.Contract.Strike));
        Assert.NotNull(bar);
        Assert.Equal(52.40m, bar.Close);
        Assert.Null(bar.Open);
    }

    [Fact]
    public void A_same_instant_re_ingest_is_refused_and_names_the_correction_path()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new ChainWriter(connection);

        writer.Ingest(SmallChain(), First);

        var refusal = Assert.Throws<InvalidOperationException>(
            () => writer.Ingest(SmallChain(), First));

        Assert.Contains("new observation instant", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(1L, Count(connection, "chain_snapshots"));
        Assert.Equal(1L, Count(connection, "underlying_bars"));
        Assert.Equal(2L, Count(connection, "contract_quotes"));
    }

    /// <summary>
    /// The correction model at the ingest level: the same chain at a new
    /// instant appends alongside the old, each observation visible to its own
    /// as-of, and the contracts are found rather than recreated.
    /// </summary>
    [Fact]
    public void A_new_instant_appends_alongside_and_each_as_of_sees_its_own()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new ChainWriter(connection);

        writer.Ingest(SmallChain(bid50: 0.95m), First);
        writer.Ingest(SmallChain(bid50: 0.96m), Second);

        var reads = new AsOfMarketData(connection);

        var before = reads.QuotesFor(Symbol, Session, asOf: new DateOnly(2026, 3, 3));
        var after = reads.QuotesFor(Symbol, Session, asOf: new DateOnly(2026, 3, 5));

        Assert.Equal(0.95m, Assert.Single(before, quote => quote.Contract.Strike == 50.00m).Bid);
        Assert.Equal(0.96m, Assert.Single(after, quote => quote.Contract.Strike == 50.00m).Bid);
        Assert.Equal(2L, Count(connection, "contracts"));
        Assert.Equal(2L, Count(connection, "chain_snapshots"));
    }

    /// <summary>
    /// All or nothing, observed rather than assumed: a collision after the
    /// header insert rolls the header back too.
    /// </summary>
    [Fact]
    public void A_mid_transaction_collision_persists_nothing()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        using (var collide = connection.CreateCommand())
        {
            collide.CommandText =
                """
                INSERT INTO underlying_bars (symbol, session_date, close, observed_at)
                VALUES ($symbol, $session, '99.00000000', $observed);
                """;
            collide.Parameters.AddWithValue("$symbol", Symbol.Value);
            collide.Parameters.AddStored("$session", Session);
            collide.Parameters.AddStored("$observed", First);
            collide.ExecuteNonQuery();
        }

        Assert.Throws<SqliteException>(
            () => new ChainWriter(connection).Ingest(SmallChain(), First));

        Assert.Equal(0L, Count(connection, "chain_snapshots"));
        Assert.Equal(0L, Count(connection, "contracts"));
        Assert.Equal(0L, Count(connection, "contract_quotes"));
        Assert.Equal(1L, Count(connection, "underlying_bars"));
    }

    private static SyntheticChain SmallChain(decimal bid50 = 0.95m) =>
        new(
            Symbol,
            [new UnderlyingBar(Symbol, Session, Close: 52.40m)],
            [
                new ContractQuote(
                    ContractIdentity.Of(Symbol, Expiry, OptionRight.Put, 47.50m),
                    Session,
                    Bid: 0.55m,
                    Ask: 0.65m),
                new ContractQuote(
                    ContractIdentity.Of(Symbol, Expiry, OptionRight.Put, 50.00m),
                    Session,
                    Bid: bid50,
                    Ask: 1.05m),
            ],
            // No scheduled reports: this suite is about how a chain persists,
            // and the earnings rows have their own cases.
            []);

    private static long Count(SqliteConnection connection, string table)
    {
        using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)count.ExecuteScalar()!;
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(First);
        return store;
    }
}
