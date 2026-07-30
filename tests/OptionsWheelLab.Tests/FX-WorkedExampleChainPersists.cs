using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Storage;
using static OptionsWheelLab.Tests.WorkedExampleOracle;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-WorkedExampleChainPersists: the worked example's chain persists and
/// reads back identical to the document's tables.
/// </summary>
/// <remarks>
/// 0.6's fixture proves the file loads to what the document states; this one
/// proves the store returns it. The document stays the oracle across the whole
/// round trip, so a revision to §2 or §5 fails both fixtures and names the
/// value, and a store that mangled a decimal on the way through would fail
/// here and nowhere else.
/// <para>
/// The scenario is loaded whole at one instant, dated after the last bar the
/// example states, because observed-at records when the corpus recorded it,
/// not when the market printed it. Every read is as of that instant's date.
/// </para>
/// </remarks>
public sealed class FX_WorkedExampleChainPersists
{
    private static readonly DateTimeOffset Recorded =
        new(2026, 6, 19, 21, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly AsOf = new(2026, 6, 19);

    [Fact]
    public void The_chain_persists_and_reads_back_the_documents_strikes()
    {
        var expected = StrikeTable();
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var quotes = new AsOfMarketData(connection).QuotesFor(
            Ticker.Normalise(Symbol), SnapshotDate, AsOf);

        // A table that matched nothing would let every comparison below pass
        // while comparing nothing.
        Assert.NotEmpty(expected);
        Assert.NotEmpty(quotes);

        Assert.Equal(expected.Count, quotes.Count);

        // Pairwise: the read-back is in identity order and the document's rows
        // ascend by strike with one expiry and right, so comparing in order
        // asserts the values and the ordering at once.
        foreach (var (row, quote) in expected.Zip(quotes))
        {
            Assert.Equal(StoreDecimal.ParseStored(row[0]), quote.Contract.Strike);
            Assert.Equal(Expiry, quote.Contract.Expiry);
            Assert.Equal(Right, quote.Contract.Right);
            Assert.Equal(SnapshotDate, quote.SnapshotDate);
            Assert.Equal(StoreDecimal.ParseStored(row[1]), quote.Delta);
            Assert.Equal(StoreDecimal.ParseStored(row[2]), quote.Bid);
            Assert.Equal(StoreDecimal.ParseStored(row[3]), quote.Ask);
        }
    }

    [Fact]
    public void The_bars_persist_and_read_back_the_documents_closes()
    {
        var expected = BarTable();
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var reads = new AsOfMarketData(connection);

        Assert.NotEmpty(expected);

        foreach (var row in expected)
        {
            var bar = reads.BarFor(
                Ticker.Normalise(Symbol), StoreDate.ParseStored(row[0]), AsOf);

            Assert.NotNull(bar);
            Assert.Equal(StoreDecimal.ParseStored(row[1]), bar.Close);
        }
    }

    /// <summary>
    /// Absence survives the store: what the document does not state reads back
    /// null, not zero, which is the round trip exercising migration 5.
    /// </summary>
    [Fact]
    public void What_the_document_does_not_state_reads_back_absent()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var reads = new AsOfMarketData(connection);
        var quotes = reads.QuotesFor(Ticker.Normalise(Symbol), SnapshotDate, AsOf);
        var bar = reads.BarFor(Ticker.Normalise(Symbol), SnapshotDate, AsOf);

        Assert.NotEmpty(quotes);
        Assert.All(quotes, quote =>
        {
            Assert.Null(quote.Last);
            Assert.Null(quote.Volume);
            Assert.Null(quote.OpenInterest);
            Assert.Null(quote.ImpliedVolatility);
            Assert.Null(quote.Gamma);
            Assert.Null(quote.Theta);
            Assert.Null(quote.Vega);
            Assert.NotNull(quote.Delta);
        });

        Assert.NotNull(bar);
        Assert.Null(bar.Open);
        Assert.Null(bar.High);
        Assert.Null(bar.Low);
        Assert.Null(bar.AdjustedClose);
        Assert.Null(bar.Volume);
    }

    private static TempStore IngestedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Recorded);

        using var connection = store.Connections.Open(StoreAccess.Write);
        new ChainWriter(connection).Ingest(LoadChain(), Recorded);

        return store;
    }
}
