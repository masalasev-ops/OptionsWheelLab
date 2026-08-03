using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ProceedsUsableOnSettlement: a trial closed by assignment cannot commit its
/// proceeds on the session of the assignment and can on the following session
/// [D-W40].
/// </summary>
/// <remarks>
/// Cash and shares from an assignment or a call-away settle on the first business
/// day after the session the exercise occurred in, which is also the session the
/// account first learns of the assignment [D-W39]. Rule 15c6-1(a) as amended
/// gives the cycle for a purchase or sale and OCC's own Rule 903 for the exercise
/// leg, which is a clearing event rather than a trade.
/// <para>
/// <b>What no rule reaches is modelled and recorded as a model.</b> When a broker
/// makes settled proceeds available to trade against is house policy rather than
/// a settlement cycle. The lab models them as usable on the settlement session.
/// </para>
/// <para>
/// <b>The trial is closed by a call-away, which is what "closed by assignment"
/// means for a wheel turn.</b> A put assignment does not close a trial: it buys
/// the shares and the turn continues [D-W16]. The proceeds that a maker might
/// want to commit elsewhere are the ones the call-away released.
/// </para>
/// </remarks>
public sealed class FX_ProceedsUsableOnSettlement
{
    [Fact]
    public void The_proceeds_are_not_usable_on_the_session_of_the_call_away()
    {
        var entries = CalledAway();

        Assert.Equal(
            LedgerReading.CashKnownOn(entries, SecondMonday),
            LedgerReading.CashKnownOn(entries, ThirdExpiry));
    }

    [Fact]
    public void The_proceeds_are_usable_on_the_following_session()
    {
        var entries = CalledAway();

        var before = LedgerReading.CashKnownOn(entries, ThirdExpiry);
        var after = LedgerReading.CashKnownOn(entries, ThirdMonday);

        Assert.Equal(5_250.00m, after - before);
    }

    /// <summary>
    /// The entry the read filters on carries the settlement session.
    /// </summary>
    [Fact]
    public void The_call_away_entry_settles_on_the_session_after_it_occurred()
    {
        var callAway = CalledAway().Last();

        Assert.Equal(LedgerEntryKind.CallAway, callAway.Kind);
        Assert.Equal(ThirdExpiry, callAway.EntryDate);
        Assert.Equal(ThirdMonday, callAway.KnownOn);
    }

    /// <summary>
    /// The trial's own close date is the session the call-away happened, not the
    /// session its cash settled.
    /// </summary>
    /// <remarks>
    /// The two questions are separate and answered by separate columns. §6.3
    /// measures 2026-03-02 to 2026-06-19 as 109 days, so a close date moved to
    /// the settlement session would change a figure that document states.
    /// </remarks>
    [Fact]
    public void The_trial_closes_on_the_session_the_call_away_occurred()
    {
        var machine = Machine();
        var written = Written(machine);

        var closed = machine.Advance(written, Session(ThirdExpiry, close: 53.40m));

        Assert.Equal(ThirdExpiry, closed.State.ClosedOn);
        Assert.Equal(109, closed.State.ClosedOn!.Value.DayNumber - Opened.DayNumber);
    }

    /// <summary>§6.3's trial through to the call-away, as ledger entries.</summary>
    private static IReadOnlyList<LedgerEntry> CalledAway()
    {
        var machine = Machine();
        var written = Written(machine);

        return
        [
            new LedgerEntry(
                Opened, Opened, LedgerEntryKind.PremiumReceived, 94.35m,
                Put(50.00m, FirstExpiry)),
            new LedgerEntry(
                FirstExpiry, MondayAfter, LedgerEntryKind.Assignment, -5_000.00m,
                Put(50.00m, FirstExpiry)),
            new LedgerEntry(
                SecondMonday, SecondMonday, LedgerEntryKind.PremiumReceived, 84.35m,
                Call(52.50m, ThirdExpiry)),
            .. machine.Advance(written, Session(ThirdExpiry, close: 53.40m)).Entries,
        ];
    }

    /// <summary>The trial holding shares with the second covered call written.</summary>
    private static TrialState Written(WheelStateMachine machine)
    {
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;

        return machine.WriteCall(
            holding, SecondMonday, Call(52.50m, ThirdExpiry), Sold(0.85m)).State;
    }
}
