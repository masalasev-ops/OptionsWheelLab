using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-WorkedExampleChainLoads: the chain and bars in <c>WORKED_EXAMPLE.md</c> §2
/// and §5 round-trip through the loader.
/// </summary>
/// <remarks>
/// <b>The document is the oracle and the file is the input.</b> The two tables
/// are parsed and compared against what loaded, rather than their numbers being
/// restated here. A second copy of a number is a second thing to keep true, and
/// this corpus has corrected that defect five times.
/// <para>
/// It also closes a live coupling. §3 carries an unresolved banner against the
/// contract-level gate constraints, and the recorded fix is "either revised
/// quotes or revised arithmetic". If the quotes are revised, this fails at once
/// and names the value that moved, instead of the transcription diverging
/// quietly until Phase 2 reads it.
/// </para>
/// <para>
/// <b>Where the line is drawn.</b> Symbol, snapshot date, expiry and right are
/// constants below. They are structural and stated once in prose: if any changed
/// the example would be a different example and every fixture reading it would
/// fail anyway. The per-strike values are what a revision actually moves, and
/// they are the only thing a second copy could silently disagree about. No prose
/// is parsed.
/// </para>
/// </remarks>
public sealed class FX_WorkedExampleChainLoads
{
    private const string Symbol = "WDGT";
    private static readonly DateOnly SnapshotDate = new(2026, 3, 2);
    private static readonly DateOnly Expiry = new(2026, 4, 17);
    private const OptionRight Right = OptionRight.Put;

    [Fact]
    public void The_chain_carries_the_strikes_the_document_states()
    {
        var expected = StrikeTable();
        var chain = Load();

        // A table that matched nothing would let every comparison below pass
        // while comparing nothing.
        Assert.NotEmpty(expected);
        Assert.NotEmpty(chain.Quotes);

        Assert.Equal(expected.Count, chain.Quotes.Count);

        foreach (var row in expected)
        {
            var strike = StoreDecimal.ParseStored(row[0]);
            var identity = ContractIdentity.Of(Ticker.Normalise(Symbol), Expiry, Right, strike);

            var quote = Assert.Single(chain.Quotes, q => q.Contract == identity);

            Assert.Equal(SnapshotDate, quote.SnapshotDate);
            Assert.Equal(StoreDecimal.ParseStored(row[1]), quote.Delta);
            Assert.Equal(StoreDecimal.ParseStored(row[2]), quote.Bid);
            Assert.Equal(StoreDecimal.ParseStored(row[3]), quote.Ask);
        }
    }

    [Fact]
    public void The_bars_carry_the_closes_the_document_states()
    {
        var expected = BarTable();
        var chain = Load();

        Assert.NotEmpty(expected);
        Assert.NotEmpty(chain.Bars);

        Assert.Equal(expected.Count, chain.Bars.Count);

        foreach (var row in expected)
        {
            var date = StoreDate.ParseStored(row[0]);
            var bar = Assert.Single(chain.Bars, b => b.SessionDate == date);

            Assert.Equal(StoreDecimal.ParseStored(row[1]), bar.Close);
            Assert.Equal(Symbol, bar.Symbol.Value);
        }
    }

    /// <summary>
    /// What the document does not state, the chain does not invent.
    /// </summary>
    /// <remarks>
    /// §2 gives bid, ask and delta; §5 gives closes. A zero implied volatility or
    /// a zero opening price would be a false observation rather than a missing
    /// one, so absence is carried as absence.
    /// </remarks>
    [Fact]
    public void What_the_document_does_not_state_is_absent_rather_than_zero()
    {
        var chain = Load();

        Assert.All(chain.Quotes, quote =>
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

        Assert.All(chain.Bars, bar =>
        {
            Assert.Null(bar.Open);
            Assert.Null(bar.High);
            Assert.Null(bar.Low);
            Assert.Null(bar.AdjustedClose);
            Assert.Null(bar.Volume);
        });
    }

    /// <summary>
    /// The underlying's close on the snapshot date is stated once, by the bar.
    /// </summary>
    /// <remarks>
    /// §2 opens "WDGT last close 52.40" and §5's bar for the same date says the
    /// same thing. The chain does not carry it a second time, because a fact kept
    /// in two places drifts.
    /// </remarks>
    [Fact]
    public void The_close_on_the_snapshot_date_comes_from_the_bar()
    {
        var chain = Load();
        var bar = Assert.Single(chain.Bars, b => b.SessionDate == SnapshotDate);

        var stated = BarTable().Single(row => StoreDate.ParseStored(row[0]) == SnapshotDate);

        Assert.Equal(StoreDecimal.ParseStored(stated[1]), bar.Close);
    }

    private static SyntheticChain Load() =>
        SyntheticChainReader.Read(File.ReadAllText(RepoRoot.WorkedExampleChainPath));

    /// <summary>§2's chain snapshot: strike, delta, bid, ask.</summary>
    /// <remarks>
    /// The fifth column, "Committed if 1 contract", is derived, being strike
    /// times the multiplier, so it is not an observation and is not compared.
    /// </remarks>
    private static IReadOnlyList<IReadOnlyList<string>> StrikeTable() =>
        MarkdownTable.Rows(
            Document(),
            "Strike", "Delta", "Bid", "Ask", "Committed if 1 contract");

    /// <summary>§5's underlying path: date, close.</summary>
    /// <remarks>The third column is commentary rather than an observation.</remarks>
    private static IReadOnlyList<IReadOnlyList<string>> BarTable() =>
        MarkdownTable.Rows(Document(), "Date", "Close", "Note");

    private static string Document() => File.ReadAllText(RepoRoot.WorkedExamplePath);
}
