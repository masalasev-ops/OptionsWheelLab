using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-EarningsClearanceRejects: a candidate whose life spans a buffered report
/// date is rejected.
/// </summary>
/// <remarks>
/// An earnings report is a scheduled binary event capable of a single-session
/// move larger than a year of collected premium, and it is exactly the class of
/// tail the available sample cannot price [D-W25, D-W11].
/// <para>
/// <b>Every case ingests a report date, including the admit case.</b> An absent
/// `earnings` array means no reports, so an admit case with an empty store
/// would pass without the constraint running at all and the fixture would say
/// it ran. The rule is 2.2's unchanged: a case needs the data its subject would
/// have used had the subject been wrong.
/// </para>
/// <para>
/// Life is 2026-03-02 to 2026-04-17 and the buffer is seven, so the rejecting
/// window closes on 2026-04-24. The four cases sit 35 days inside that edge, 3
/// days inside it, exactly on it, and 7 days outside it.
/// </para>
/// </remarks>
public sealed class FX_EarningsClearanceRejects
{
    /// <summary>The far end of the buffered window, being expiry plus seven.</summary>
    private static readonly DateOnly Edge = new(2026, 4, 24);

    [Fact]
    public void A_report_inside_the_life_is_rejected()
    {
        Assert.Equal(
            [GateReason.EarningsClearance],
            Verdict(new DateOnly(2026, 3, 20)));
    }

    /// <summary>
    /// The case the buffer exists for: the report falls after expiry, so an
    /// unbuffered filter admits it.
    /// </summary>
    [Fact]
    public void A_report_clearing_the_expiry_but_not_the_buffer_is_rejected()
    {
        var report = new DateOnly(2026, 4, 21);

        Assert.True(report > GateScenario.Expiry);
        Assert.Equal([GateReason.EarningsClearance], Verdict(report));
    }

    /// <summary>
    /// The buffer is inclusive of its edge [D-W25, as amended]. A window
    /// excluding its own edge would admit a report at precisely the distance
    /// the buffer was sized for, which is the case the buffer exists to catch.
    /// </summary>
    [Fact]
    public void A_report_exactly_on_the_buffers_edge_is_rejected()
    {
        Assert.Equal(GateScenario.Expiry.AddDays(7), Edge);
        Assert.Equal([GateReason.EarningsClearance], Verdict(Edge));
    }

    /// <summary>
    /// The admit case, with a report present in the store rather than absent.
    /// </summary>
    [Fact]
    public void A_report_clearing_the_buffer_is_admitted()
    {
        var report = new DateOnly(2026, 5, 1);

        Assert.True(report > Edge);
        Assert.Empty(Verdict(report));
    }

    /// <summary>
    /// The other end of the window, since the buffer runs both ways [D-W25].
    /// </summary>
    [Fact]
    public void The_window_reaches_backwards_from_the_open_as_well()
    {
        // The open is 2026-03-02, so the near edge is 2026-02-23.
        Assert.Equal([GateReason.EarningsClearance], Verdict(new DateOnly(2026, 2, 23)));
        Assert.Empty(Verdict(new DateOnly(2026, 2, 22)));
    }

    /// <summary>
    /// The gate's verdict on one quote whose only variable is where the report
    /// falls. Everything else about the quote passes.
    /// </summary>
    private static IReadOnlyList<GateReason> Verdict(DateOnly report) =>
        GateScenario.Shared(
            [GateScenario.Quote(50.00m)],
            [new EarningsReport(report, EarningsSession.AfterClose)])[50.00m];
}
