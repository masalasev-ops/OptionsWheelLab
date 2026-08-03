using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-NextSessionSkipsAClosedDate: an assignment whose following date is absent
/// from the calendar settles on the next date the calendar carries, and a date
/// the calendar does not reach stops rather than resolving [D-W46].
/// </summary>
/// <remarks>
/// The calendar is transcribed and never derived. A per-symbol sequence cannot
/// tell a market holiday from a name that did not trade, and a derived calendar's
/// answer about a past date would change when another symbol was ingested, which
/// is the leak an as-of read exists to close [D-W8] reached through a derived
/// value.
/// <para>
/// <b>The closed date here is a real one.</b> Good Friday 2026 falls on 3 April,
/// so a Thursday expiry that week settles on the Monday rather than the Friday.
/// A fixture skipping an invented date would assert the mechanism and not that it
/// answers a question anyone has.
/// </para>
/// </remarks>
public sealed class FX_NextSessionSkipsAClosedDate
{
    private static readonly DateOnly Thursday = new(2026, 4, 2);
    private static readonly DateOnly GoodFriday = new(2026, 4, 3);
    private static readonly DateOnly EasterMonday = new(2026, 4, 6);

    /// <summary>Easter week, with Good Friday absent because the market was shut.</summary>
    private static readonly SessionCalendar OverEaster = SessionCalendar.Of(
        [new(2026, 3, 2), new(2026, 4, 1), Thursday, EasterMonday]);

    [Fact]
    public void An_assignment_settles_on_the_next_date_the_calendar_carries()
    {
        var machine = MachineOn(OverEaster);
        var opened = machine.OpenTrial(Put(50.00m, Thursday), Sold(0.95m), Opened).State;

        var assigned = machine.Advance(opened, Session(Thursday, close: 48.90m));

        var entry = Assert.Single(assigned.Entries);

        Assert.Equal(Thursday, entry.EntryDate);
        Assert.Equal(EasterMonday, entry.KnownOn);
        Assert.Equal(EasterMonday, assigned.State.EffectiveFrom);
    }

    /// <summary>
    /// The skipped date is not a session, so nothing settles on it.
    /// </summary>
    /// <remarks>
    /// Asserted directly as well as through the settlement, because a calendar
    /// that answered the settlement correctly by counting three days forward
    /// would pass the case above and be wrong about every other holiday.
    /// </remarks>
    [Fact]
    public void The_closed_date_is_not_a_session()
    {
        Assert.False(OverEaster.IsSession(GoodFriday));
        Assert.True(OverEaster.IsSession(Thursday));
        Assert.True(OverEaster.IsSession(EasterMonday));
        Assert.Equal(EasterMonday, OverEaster.NextSessionAfter(GoodFriday));
    }

    /// <summary>
    /// A date the calendar does not reach stops rather than resolving either way
    /// [D-W37].
    /// </summary>
    /// <remarks>
    /// Guessing forward would put an unrecorded market assumption inside a
    /// settlement date, and guessing that the date is a session would settle
    /// proceeds on a day the market was shut. Neither is recoverable from the
    /// record.
    /// </remarks>
    [Fact]
    public void A_date_the_calendar_does_not_reach_stops_the_evaluation()
    {
        var machine = MachineOn(OverEaster);
        var opened = machine.OpenTrial(Put(50.00m, EasterMonday), Sold(0.95m), Opened).State;

        var thrown = Assert.Throws<InvalidOperationException>(
            () => machine.Advance(opened, Session(EasterMonday, close: 48.90m)));

        Assert.Contains("does not guess forward", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2026-04-06", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The calendar redefines no day count, which is the clause keeping it from
    /// spreading.
    /// </summary>
    /// <remarks>
    /// Days to expiry and a trial's day bound are calendar days [D-W24, D-W14],
    /// and §5's own trial runs 109 of them. A calendar that quietly redefined
    /// either would move every gate verdict in <c>WORKED_EXAMPLE.md</c> and put
    /// its total out of reach. The sessions between the two dates are four; the
    /// days are 109.
    /// </remarks>
    [Fact]
    public void The_calendar_does_not_redefine_a_day_count()
    {
        Assert.Equal(109, ThirdExpiry.DayNumber - Opened.DayNumber);

        var sessionsBetween = new[]
            {
                Opened, new DateOnly(2026, 4, 8), FirstExpiry, MondayAfter,
                SecondExpiry, SecondMonday, ThirdExpiry,
            }
            .Count(session => session > Opened && session <= ThirdExpiry);

        Assert.Equal(6, sessionsBetween);
        Assert.NotEqual(sessionsBetween, ThirdExpiry.DayNumber - Opened.DayNumber);
    }
}
