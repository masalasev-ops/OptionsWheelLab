using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The three builds earnings clearance needed before its constraint was
/// testable: a format that can express a report date, a writer, and an as-of
/// read.
/// </summary>
/// <remarks>
/// Not a registered fixture: the check registered against the constraint is
/// FX-EarningsClearanceRejects, and these are the machinery beneath it.
/// <c>earnings_calendar</c> had existed since migration 3 with nothing reading
/// it and nothing writing it.
/// </remarks>
public sealed class EarningsCalendarTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly DateOnly Report = new(2026, 4, 21);

    private static readonly DateTimeOffset Recorded =
        new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A file stating no <c>earnings</c> array reads as no reports, which is
    /// what keeps every scenario written before 2.3 parsing unchanged.
    /// </summary>
    [Fact]
    public void An_absent_earnings_array_reads_as_no_reports()
    {
        var chain = SyntheticChainReader.Read(
            """
            { "symbol": "WDGT", "bars": [], "chains": [] }
            """);

        Assert.Empty(chain.Earnings);
    }

    [Fact]
    public void The_worked_examples_file_still_parses_without_an_earnings_array()
    {
        var chain = WorkedExampleOracle.LoadChain();

        Assert.Empty(chain.Earnings);
        Assert.NotEmpty(chain.Quotes);
    }

    [Fact]
    public void Reports_are_read_in_date_order_whatever_the_file_states()
    {
        var chain = SyntheticChainReader.Read(
            """
            {
              "symbol": "WDGT",
              "bars": [],
              "chains": [],
              "earnings": [
                { "date": "2026-05-01", "session": "after_close" },
                { "date": "2026-01-15", "session": "before_open" },
                { "date": "2026-03-20", "session": "unspecified" }
              ]
            }
            """);

        Assert.Equal(
            [new DateOnly(2026, 1, 15), new DateOnly(2026, 3, 20), new DateOnly(2026, 5, 1)],
            chain.Earnings.Select(report => report.ReportDate));

        Assert.Equal(
            [EarningsSession.BeforeOpen, EarningsSession.Unspecified, EarningsSession.AfterClose],
            chain.Earnings.Select(report => report.Session));
    }

    /// <summary>
    /// The session goes through its declared stored form, so a value outside
    /// the vocabulary is refused rather than carried.
    /// </summary>
    [Fact]
    public void A_session_outside_the_vocabulary_is_refused()
    {
        var thrown = Assert.Throws<MalformedChainException>(() => SyntheticChainReader.Read(
            """
            {
              "symbol": "WDGT", "bars": [], "chains": [],
              "earnings": [ { "date": "2026-04-21", "session": "premarket" } ]
            }
            """));

        Assert.Contains("premarket", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_property_on_a_report_is_refused()
    {
        var thrown = Assert.Throws<MalformedChainException>(() => SyntheticChainReader.Read(
            """
            {
              "symbol": "WDGT", "bars": [], "chains": [],
              "earnings": [ { "date": "2026-04-21", "session": "after_close", "confirmed": true } ]
            }
            """));

        Assert.Contains("confirmed", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Two_reports_on_one_date_are_refused()
    {
        var thrown = Assert.Throws<MalformedChainException>(() => SyntheticChainReader.Read(
            """
            {
              "symbol": "WDGT", "bars": [], "chains": [],
              "earnings": [
                { "date": "2026-04-21", "session": "after_close" },
                { "date": "2026-04-21", "session": "before_open" }
              ]
            }
            """));

        Assert.Contains("2026-04-21", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_report_persists_and_reads_back_inside_its_window()
    {
        using var store = IngestedStore(Report);
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var dates = new AsOfMarketData(connection).ReportDatesFor(
            Symbol, from: new DateOnly(2026, 3, 1), to: new DateOnly(2026, 5, 1), asOf: Report);

        Assert.Equal([Report], dates);
    }

    /// <summary>
    /// Both ends of the range are inclusive, which is what lets the buffer's
    /// own edge be inclusive without this read knowing what a buffer is.
    /// </summary>
    [Fact]
    public void Both_ends_of_the_range_are_inclusive()
    {
        using var store = IngestedStore(Report);
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var reads = new AsOfMarketData(connection);

        Assert.Equal([Report], reads.ReportDatesFor(Symbol, Report, Report, asOf: Report));
        Assert.Empty(reads.ReportDatesFor(
            Symbol, Report.AddDays(1), Report.AddDays(10), asOf: Report));
        Assert.Empty(reads.ReportDatesFor(
            Symbol, Report.AddDays(-10), Report.AddDays(-1), asOf: Report));
    }

    /// <summary>
    /// The knowledge axis, as every read on this surface has: a report recorded
    /// after a simulated date is invisible to it.
    /// </summary>
    [Fact]
    public void A_report_recorded_after_the_as_of_date_is_invisible_to_it()
    {
        var late = new DateTimeOffset(2026, 6, 1, 21, 0, 0, TimeSpan.Zero);

        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Recorded);

        using (var write = store.Connections.Open(StoreAccess.Write))
        {
            new ChainWriter(write).Ingest(ChainWith(Report), late);
        }

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);
        var reads = new AsOfMarketData(connection);

        Assert.Empty(reads.ReportDatesFor(
            Symbol, Report.AddDays(-30), Report.AddDays(30), asOf: new DateOnly(2026, 5, 1)));

        Assert.Equal(
            [Report],
            reads.ReportDatesFor(
                Symbol, Report.AddDays(-30), Report.AddDays(30), asOf: new DateOnly(2026, 6, 1)));
    }

    /// <summary>
    /// A correction appends [D-W8], and the read returns one row rather than
    /// both observations of it.
    /// </summary>
    [Fact]
    public void A_re_observed_report_returns_once()
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Recorded);

        using (var write = store.Connections.Open(StoreAccess.Write))
        {
            var writer = new ChainWriter(write);
            writer.Ingest(ChainWith(Report), Recorded);
            writer.Ingest(ChainWith(Report), Recorded.AddDays(1));
        }

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        Assert.Equal(
            [Report],
            new AsOfMarketData(connection).ReportDatesFor(
                Symbol, Report.AddDays(-30), Report.AddDays(30), asOf: new DateOnly(2026, 5, 1)));
    }

    private static TempStore IngestedStore(DateOnly report)
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Recorded);

        using var connection = store.Connections.Open(StoreAccess.Write);
        new ChainWriter(connection).Ingest(ChainWith(report), Recorded);

        return store;
    }

    /// <summary>
    /// A scenario carrying one report and no quotes, which the writer accepts
    /// because a scenario is not required to state a chain.
    /// </summary>
    private static SyntheticChain ChainWith(DateOnly report) =>
        new(Symbol, [], [], [new EarningsReport(report, EarningsSession.AfterClose)], []);
}
