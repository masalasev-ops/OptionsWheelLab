using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The transitions, driven directly because nothing drives them yet.
/// </summary>
/// <remarks>
/// Not registered fixtures. The registry rows at 3.3 assert the decisions
/// themselves against named scenarios; what is here is the behaviour those
/// checks cannot isolate, on <see cref="PortfolioConstraintsTests"/>' argument.
/// <para>
/// <b>The sessions are the worked example's, deliberately.</b> §5's dates are
/// Fridays and the following Mondays, which is what next-session notification
/// [D-W39] and next-session settlement [D-W40] produce, so a scenario built on
/// them exercises the calendar rather than assuming every date is a session.
/// </para>
/// </remarks>
public sealed class WheelStateMachineTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");

    /// <summary>WORKED_EXAMPLE §5's sessions, Fridays and the Mondays after.</summary>
    private static readonly SessionCalendar Calendar = SessionCalendar.Of(
    [
        new(2026, 3, 2), new(2026, 4, 8), new(2026, 4, 17), new(2026, 4, 20),
        new(2026, 5, 15), new(2026, 5, 18), new(2026, 6, 19), new(2026, 6, 22),
    ]);

    private static readonly TrialBounds Seeded = new(MaxRolls: 2, MaxTrialDays: 120);

    private static readonly DateOnly Opened = new(2026, 3, 2);
    private static readonly DateOnly FirstExpiry = new(2026, 4, 17);
    private static readonly DateOnly MondayAfter = new(2026, 4, 20);

    [Fact]
    public void A_put_a_cent_in_the_money_assigns_and_one_at_the_strike_does_not()
    {
        Assert.Equal(
            PositionState.HoldingShares,
            Resolve(close: 49.99m).State.State);

        Assert.Equal(
            PositionState.Cash,
            Resolve(close: 50.00m).State.State);
    }

    /// <summary>
    /// The threshold is the decision's, and it is checked at the boundary rather
    /// than either side of it.
    /// </summary>
    /// <remarks>
    /// A cent in the money assigns and anything less does not [D-W38], so the
    /// pair that distinguishes a correct threshold from an approximately correct
    /// one is 49.99 against 49.995, not 49.99 against 50.00.
    /// </remarks>
    [Fact]
    public void A_put_less_than_a_cent_in_the_money_expires_worthless()
    {
        Assert.Equal(PositionState.Cash, Resolve(close: 49.995m).State.State);
        Assert.Equal(
            TrialCloseKind.ExpiredWorthless, Resolve(close: 49.995m).State.CloseKind);
    }

    /// <summary>
    /// Assignment takes effect the session after it happened [D-W39].
    /// </summary>
    [Fact]
    public void The_shares_arrive_on_the_next_session_not_the_session_of_assignment()
    {
        var assigned = Resolve(close: 48.90m);

        Assert.Equal(MondayAfter, assigned.State.EffectiveFrom);

        var entry = Assert.Single(assigned.Entries);

        Assert.Equal(LedgerEntryKind.Assignment, entry.Kind);
        Assert.Equal(FirstExpiry, entry.EntryDate);
        Assert.Equal(MondayAfter, entry.KnownOn);
        Assert.Equal(-5_000.00m, entry.Amount);
    }

    /// <summary>
    /// Both bases after assignment, on §6.3's figures [D-W19].
    /// </summary>
    [Fact]
    public void Gross_basis_is_the_strike_and_net_basis_is_the_strike_less_premium()
    {
        var assigned = Resolve(close: 48.90m).State;

        Assert.Equal(50.00m, assigned.GrossBasis);
        Assert.Equal(49.0565m, assigned.NetBasis);
    }

    /// <summary>
    /// A covered call commits nothing beyond what the trial already carries
    /// [D-W43].
    /// </summary>
    [Fact]
    public void Writing_a_call_leaves_committed_capital_where_the_put_fixed_it()
    {
        var machine = Machine();
        var holding = Resolve(close: 48.90m).State;

        var written = machine.WriteCall(holding, MondayAfter, Call(52.50m), credit: 69.35m);

        Assert.Equal(5_000.00m, holding.CommittedCapital);
        Assert.Equal(5_000.00m, written.State.CommittedCapital);
        Assert.Equal(PositionState.ShortCall, written.State.State);
    }

    /// <summary>
    /// A roll banks both legs and neither is a close [D-W48].
    /// </summary>
    [Fact]
    public void A_roll_writes_a_premium_paid_and_a_premium_received()
    {
        var machine = Machine();
        var opened = TrialState.OpenShortPut(Put(50.00m), credit: 94.35m, Opened);

        var rolled = machine.Roll(
            opened, FirstExpiry, debit: 120.00m, opened: Put(48.00m), credit: 150.00m);

        Assert.Equal(
            [LedgerEntryKind.PremiumPaid, LedgerEntryKind.PremiumReceived],
            rolled.Entries.Select(entry => entry.Kind));
        Assert.Equal(1, rolled.State.RollsUsed);
        Assert.Equal(94.35m - 120.00m + 150.00m, rolled.State.PremiumBanked);
    }

    /// <summary>
    /// The roll bound closes at market and the trial resolves [D-W14].
    /// </summary>
    [Fact]
    public void The_roll_bound_closes_the_position_at_market()
    {
        var bound = Machine().Advance(
            RolledTwice(), Session(new(2026, 5, 15), close: 45.00m));

        Assert.Equal(PositionState.Cash, bound.State.State);
        Assert.Equal(TrialCloseKind.ClosedAtBound, bound.State.CloseKind);
        Assert.Equal(new DateOnly(2026, 5, 15), bound.State.ClosedOn);

        var entry = Assert.Single(bound.Entries);

        Assert.Equal(LedgerEntryKind.BoughtToClose, entry.Kind);
        Assert.Equal(-500.00m, entry.Amount);
        Assert.Equal(new DateOnly(2026, 5, 18), entry.KnownOn);
    }

    /// <summary>
    /// The bound waits for a state the account knows about [D-W39].
    /// </summary>
    /// <remarks>
    /// This is the one place where one step of a session reads another's output.
    /// A trial at its roll bound whose put expires in the money is assigned on
    /// that session and holds shares from the next, so a bound acting
    /// immediately would sell shares on the day the assignment happened, which is
    /// a decision depending on an assignment that occurred the same day. Written
    /// without the guard first, and this is what found it.
    /// </remarks>
    [Fact]
    public void The_bound_does_not_sell_shares_on_the_session_they_were_assigned()
    {
        var machine = Machine();
        var expiring = machine.Roll(
            RolledTwice(), new DateOnly(2026, 4, 8), 1m, Put(50.00m), 1m).State;

        var assigned = machine.Advance(expiring, Session(FirstExpiry, close: 45.00m));

        var entry = Assert.Single(assigned.Entries);

        Assert.Equal(LedgerEntryKind.Assignment, entry.Kind);
        Assert.False(assigned.State.IsClosed);
        Assert.Equal(MondayAfter, assigned.State.EffectiveFrom);

        // The following session, the account knows it holds shares, and the bound
        // acts.
        var closed = machine.Advance(assigned.State, Session(MondayAfter, close: 45.50m));

        Assert.Equal(TrialCloseKind.ClosedAtBound, closed.State.CloseKind);
        Assert.Equal(LedgerEntryKind.SharesSold, Assert.Single(closed.Entries).Kind);
        Assert.Equal(4_550.00m, Assert.Single(closed.Entries).Amount);
    }

    /// <summary>§6.3's trial rolled to its bound, expiring after it binds.</summary>
    private static TrialState RolledTwice()
    {
        var machine = Machine();
        var opened = TrialState.OpenShortPut(LatePut(50.00m), credit: 94.35m, Opened);

        var once = machine.Roll(
            opened, new DateOnly(2026, 4, 8), 1m, LatePut(50.00m), 1m).State;

        return machine.Roll(once, new DateOnly(2026, 4, 8), 1m, LatePut(50.00m), 1m).State;
    }

    /// <summary>
    /// A dividend the trial's shares earned reaches the ledger [D-W41].
    /// </summary>
    [Fact]
    public void A_dividend_while_holding_shares_is_an_entry()
    {
        var machine = Machine();
        var holding = Resolve(close: 48.90m).State;

        var paid = machine.Advance(
            holding,
            Session(
                new(2026, 5, 15),
                close: 51.20m,
                actions: [Ordinary(new(2026, 5, 15), perShare: 0.44m)]));

        var entry = Assert.Single(paid.Entries);

        Assert.Equal(LedgerEntryKind.Dividend, entry.Kind);
        Assert.Equal(44.00m, entry.Amount);
        Assert.Equal(new DateOnly(2026, 5, 18), entry.KnownOn);
    }

    /// <summary>
    /// A trial holding nothing earns no dividend, which is the half that would
    /// pass on an empty check.
    /// </summary>
    [Fact]
    public void A_dividend_with_no_shares_held_is_not_an_entry()
    {
        var machine = Machine();
        var opened = TrialState.OpenShortPut(Put(50.00m), credit: 94.35m, Opened);

        var paid = machine.Advance(
            opened,
            Session(
                new(2026, 4, 8),
                close: 45.80m,
                actions: [Ordinary(new(2026, 4, 8), perShare: 0.44m)]));

        Assert.Empty(paid.Entries);
    }

    /// <summary>
    /// An action the lab does not model stops the trial rather than passing
    /// through it [D-W47].
    /// </summary>
    [Fact]
    public void A_merger_stops_the_trial_and_carries_its_reason()
    {
        var machine = Machine();
        var opened = TrialState.OpenShortPut(Put(50.00m), credit: 94.35m, Opened);

        var stopped = machine.Advance(
            opened,
            Session(
                new(2026, 4, 8),
                close: 45.80m,
                actions:
                [
                    new ActionOnUnderlying(
                        new CorporateAction(CorporateActionKind.Merger, new(2026, 4, 8))),
                ]));

        Assert.Equal(TrialCloseKind.Stopped, stopped.State.CloseKind);

        var entry = Assert.Single(stopped.Entries);

        Assert.Equal(LedgerEntryKind.Stopped, entry.Kind);
        Assert.Equal(0m, entry.Amount);
        Assert.Contains("merger", entry.Note!, StringComparison.Ordinal);
    }

    /// <summary>
    /// An adjustment with no stated terms stops rather than deriving them
    /// [D-W36].
    /// </summary>
    [Fact]
    public void A_split_stating_no_successor_stops_the_evaluation()
    {
        var machine = Machine();
        var opened = TrialState.OpenShortPut(Put(50.00m), credit: 94.35m, Opened);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => machine.Advance(
                opened,
                Session(
                    new(2026, 4, 8),
                    close: 45.80m,
                    actions:
                    [
                        new ActionOnUnderlying(
                            new CorporateAction(
                                CorporateActionKind.Split, new(2026, 4, 8), Ratio: 1.5m)),
                    ])));

        Assert.Contains("never derived", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The calendar refuses to guess past its last session [D-W46, D-W37].
    /// </summary>
    [Fact]
    public void A_session_the_calendar_cannot_follow_stops_rather_than_resolving()
    {
        var machine = Machine();
        var opened = TrialState.OpenShortPut(
            ContractIdentity.Of(Symbol, new(2026, 6, 22), OptionRight.Put, 50.00m),
            credit: 94.35m,
            Opened);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => machine.Advance(opened, Session(new(2026, 6, 22), close: 45.00m)));

        Assert.Contains("does not guess forward", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_calendar_with_no_sessions_is_refused()
    {
        var thrown = Assert.Throws<ArgumentException>(() => SessionCalendar.Of([]));

        Assert.Contains("never opened", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A closed trial is not advanced again, so a caller stepping past the close
    /// cannot double an exit.
    /// </summary>
    [Fact]
    public void A_closed_trial_is_unchanged_by_a_further_session()
    {
        var machine = Machine();
        var closed = Resolve(close: 50.00m).State;

        var after = machine.Advance(closed, Session(new(2026, 4, 20), close: 51.00m));

        Assert.Same(closed, after.State);
        Assert.Empty(after.Entries);
    }

    private static WheelStateMachine Machine() => new(Calendar, Seeded);

    /// <summary>§6.3's trial at its first expiry, with the close as supplied.</summary>
    private static Transition Resolve(decimal close)
    {
        var opened = TrialState.OpenShortPut(Put(50.00m), credit: 94.35m, Opened);

        return Machine().Advance(opened, Session(FirstExpiry, close));
    }

    private static SessionFacts Session(
        DateOnly session,
        decimal close,
        IReadOnlyList<ActionOnUnderlying>? actions = null,
        decimal? bid = null) =>
        new(session, close, actions ?? [], bid);

    private static ActionOnUnderlying Ordinary(DateOnly exDate, decimal perShare) =>
        new(new CorporateAction(CorporateActionKind.OrdinaryDividend, exDate, Amount: perShare));

    private static ContractIdentity Put(decimal strike) =>
        ContractIdentity.Of(Symbol, FirstExpiry, OptionRight.Put, strike);

    /// <summary>A put expiring after the bound binds, so the bound acts alone.</summary>
    private static ContractIdentity LatePut(decimal strike) =>
        ContractIdentity.Of(Symbol, new(2026, 6, 19), OptionRight.Put, strike);

    private static ContractIdentity Call(decimal strike) =>
        ContractIdentity.Of(Symbol, new(2026, 5, 15), OptionRight.Call, strike);
}
