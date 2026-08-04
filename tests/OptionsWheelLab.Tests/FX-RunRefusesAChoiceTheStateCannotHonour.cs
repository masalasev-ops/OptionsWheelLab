using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-RunRefusesAChoiceTheStateCannotHonour: a supplied choice the trial cannot
/// take refuses by name rather than being skipped.
/// </summary>
/// <remarks>
/// <b>A mis-described run must not produce a plausible ledger.</b> Skipping a
/// choice the state cannot honour would give a run that walked, wrote entries and
/// described a trial nobody asked for, which is worse than one that stopped: the
/// output would be readable, internally consistent and wrong.
/// <para>
/// It is [D-W48]'s argument one level up. A ledger that cannot express an event
/// is better than one that expresses the wrong event, and
/// <see cref="TrialProjection"/> already refuses a closed trial that receives
/// premium for the same reason. The run is where the same mistake can be made
/// before anything is written.
/// </para>
/// <para>
/// <b>Every refusal names the session and the state.</b> A choice sequence is
/// written by hand, so the two facts a reader needs are which line is wrong and
/// what the trial was actually holding when it got there.
/// </para>
/// </remarks>
public sealed class FX_RunRefusesAChoiceTheStateCannotHonour
{
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A call before any assignment, which is the case the clause names.
    /// </summary>
    [Fact]
    public void A_call_supplied_before_any_assignment_refuses()
    {
        var thrown = Walk(
            [
                new OpenPut(Opened, Put(50.00m, ThirdExpiry), Bid: 0.95m),
                new WriteCoveredCall(MondayAfter, Call(52.50m, ThirdExpiry), Bid: 0.70m),
            ]);

        Assert.Contains("2026-04-20", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("the trial is 'ShortPut'", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("held shares", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>A call before anything at all opened.</summary>
    [Fact]
    public void A_call_supplied_with_no_trial_open_refuses()
    {
        var thrown = Walk([new WriteCoveredCall(Opened, Call(52.50m, ThirdExpiry), Bid: 0.70m)]);

        Assert.Contains("no trial is open", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>A second open against a trial already open.</summary>
    [Fact]
    public void A_second_open_refuses()
    {
        var thrown = Walk(
            [
                new OpenPut(Opened, Put(50.00m, ThirdExpiry), Bid: 0.95m),
                new OpenPut(MondayAfter, Put(48.00m, ThirdExpiry), Bid: 0.55m),
            ]);

        Assert.Contains("already open", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2026-04-20", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>A roll against a trial holding no short.</summary>
    [Fact]
    public void A_roll_against_shares_refuses()
    {
        var thrown = Walk(
            [
                new OpenPut(Opened, Put(50.00m, FirstExpiry), Bid: 0.95m),
                new RollInto(MondayAfter, Put(48.00m, ThirdExpiry), Ask: 1.10m, Bid: 0.55m),
            ]);

        Assert.Contains("holds none", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("HoldingShares", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A choice outside the range refuses rather than never being applied.
    /// </summary>
    /// <remarks>
    /// The quietest of the four. A session the loop never steps carries a choice
    /// the loop never sees, so the run would differ from the one described and
    /// nothing would say so.
    /// </remarks>
    [Fact]
    public void A_choice_outside_the_range_refuses()
    {
        var thrown = Walk(
            [
                new OpenPut(Opened, Put(50.00m, ThirdExpiry), Bid: 0.95m),
                new WriteCoveredCall(new(2027, 1, 4), Call(52.50m, ThirdExpiry), Bid: 0.70m),
            ]);

        Assert.Contains("fall outside", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2027-01-04", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A bar on a date the calendar does not carry refuses.
    /// </summary>
    /// <remarks>
    /// The chain and the calendar are two statements about which dates are
    /// sessions and the calendar is the authority [D-W46]. Found by the calendar
    /// being handed to the run and having nothing to do: without this the loop
    /// would step a session and then settle an assignment onto a date the chain
    /// has no bar for.
    /// </remarks>
    [Fact]
    public void A_bar_the_calendar_does_not_carry_refuses()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => Walk(
                [new OpenPut(Opened, Put(50.00m, ThirdExpiry), Bid: 0.95m)],
                chain: new SyntheticChain(
                    Symbol,
                    [
                        new UnderlyingBar(Symbol, Opened, Close: 52.40m),
                        new UnderlyingBar(Symbol, new(2026, 4, 4), Close: 51.00m),
                    ],
                    [],
                    [],
                    []),
                rethrow: false));

        Assert.Contains("2026-04-04", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("the authority", thrown.Message, StringComparison.Ordinal);
    }

    private static InvalidOperationException Walk(
        IReadOnlyList<TrialChoice> choices,
        SyntheticChain? chain = null,
        bool rethrow = true)
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Seeded);

        using var connection = store.Connections.Open(StoreAccess.Write);
        new ConfigWriter(connection).AppendAll(SeedValues.All, Seeded);

        var run = new TrialRun(
            Machine(), new FillModel(new AsOfConfiguration(connection)), Calendar);

        var walk = () => run.Walk(chain ?? Chain(), Opened, ThirdMonday, choices);

        return rethrow
            ? Assert.Throws<InvalidOperationException>(() => walk())
            : throw Record.Exception(() => walk())!;
    }

    /// <summary>§5's closes over the sessions the calendar carries.</summary>
    private static SyntheticChain Chain() =>
        new(
            Symbol,
            [
                new UnderlyingBar(Symbol, Opened, Close: 52.40m),
                new UnderlyingBar(Symbol, FirstExpiry, Close: 48.90m),
                new UnderlyingBar(Symbol, MondayAfter, Close: 48.95m),
                new UnderlyingBar(Symbol, SecondExpiry, Close: 51.20m),
                new UnderlyingBar(Symbol, SecondMonday, Close: 51.30m),
                new UnderlyingBar(Symbol, ThirdExpiry, Close: 53.40m),
            ],
            [],
            [],
            []);
}
