using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;

namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// One session applied to one trial: the four states, and the events that move
/// between them [SYSTEM_DESIGN §3.8].
/// </summary>
/// <remarks>
/// <b>A function of its arguments, driven by a caller rather than a loop.</b>
/// Nothing here reads configuration, a clock or a table: the bounds arrive
/// resolved [D-W37], the calendar is handed in, and the session's facts are a
/// parameter. No maker exists until Phase 4 and no run loop exists at all, so
/// what steps this today is a test, and that is the shape rather than a stage of
/// it.
/// <para>
/// <b>Every transition cites a decision settled at 3.1</b>, which was that
/// checkpoint's definition of done. Nothing below is encoded from recollection.
/// </para>
/// <para>
/// <b>The order within a session is the sequence below, and it is not
/// arbitrary.</b> An unmodelled action stops the trial before anything else can
/// price it. Early assignment is checked before expiry because it acts on the
/// session before an ex-date and would otherwise be pre-empted by an expiry the
/// same day. Expiry resolves before the bound, because a trial that expired to
/// cash has already ended and closing it again at market would double its exit.
/// The bound is last for that reason.
/// </para>
/// <para>
/// <b>What it does not do.</b> Rolling is not initiated here: choosing to roll is
/// a decision and decisions are Phase 4's [§4.3]. What is here is the bound that
/// terminates a rolled chain [D-W14], which is a consequence of the trial's state
/// rather than a choice, and <see cref="Roll"/>, which applies a choice a caller
/// has already made.
/// </para>
/// </remarks>
public sealed class WheelStateMachine
{
    /// <summary>
    /// The exercise-by-exception threshold: one cent in the money [D-W38].
    /// </summary>
    /// <remarks>
    /// OCC Rule 805's figure, and a stated one rather than a tunable, which is
    /// why it is a constant here and not a configuration key [CLAUDE.md §3]. A
    /// value that moved would be a rule change at the clearing house, recorded by
    /// amending the decision.
    /// </remarks>
    public const decimal ExerciseByExceptionThreshold = 0.01m;

    private readonly SessionCalendar _calendar;
    private readonly TrialBounds _bounds;
    private readonly CostBounds _costs;

    /// <summary>
    /// The machine, handed everything it reads.
    /// </summary>
    /// <remarks>
    /// <paramref name="costs"/> arrives resolved for the same reason the bounds
    /// do [D-W37]: this type reads no configuration. What it needs from them is
    /// the assignment fee, which the machine rather than the fill model writes,
    /// because an assignment is a clearing event and not a trade [D-W40] and the
    /// entry belongs beside the assignment that caused it.
    /// </remarks>
    public WheelStateMachine(SessionCalendar calendar, TrialBounds bounds, CostBounds costs)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(costs);

        _calendar = calendar;
        _bounds = bounds;
        _costs = costs;
    }

    /// <summary>
    /// Opening a trial by selling a cash-secured put [D-W16].
    /// </summary>
    /// <remarks>
    /// <b>The open writes its entries here rather than leaving them to a
    /// caller.</b> Every other leg's entries come from this type, and an open
    /// whose ledger rows were written elsewhere would be the one event with two
    /// producers. It also means a trial's first entry is a
    /// <see cref="LedgerEntryKind.PremiumReceived"/> by construction, which is
    /// what <see cref="TrialProjection.Replay"/> requires of a ledger it can read.
    /// </remarks>
    public Transition OpenTrial(ContractIdentity put, Fill sold, DateOnly session)
    {
        ArgumentNullException.ThrowIfNull(put);
        ArgumentNullException.ThrowIfNull(sold);

        return new Transition(
            TrialState.OpenShortPut(put, sold.Net, session),
            [.. PremiumEntries(session, session, LedgerEntryKind.PremiumReceived, sold, put)]);
    }

    /// <summary>
    /// A leg's two entries: the premium and, when one was charged, the commission
    /// [D-W50].
    /// </summary>
    /// <remarks>
    /// <b>A commission of zero writes no row, and that is not the same rule as an
    /// expiry's.</b> An expiry that pays nothing is still an event and takes a row
    /// with a zero amount [D-W48], because the projection has to know the short
    /// closed. A commission is a cost rather than an event, and a cost that was
    /// not charged is not one.
    /// </remarks>
    private static IReadOnlyList<LedgerEntry> PremiumEntries(
        DateOnly session,
        DateOnly known,
        LedgerEntryKind kind,
        Fill fill,
        ContractIdentity contract)
    {
        var entries = new List<LedgerEntry>
        {
            new(session, known, kind, fill.Premium, contract),
        };

        if (fill.Commission != 0m)
        {
            entries.Add(new LedgerEntry(
                session, known, LedgerEntryKind.Commission, -fill.Commission, contract));
        }

        return entries;
    }

    /// <summary>
    /// Applies one session to <paramref name="state"/>.
    /// </summary>
    public Transition Advance(TrialState state, SessionFacts facts)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(facts);

        if (state.IsClosed)
        {
            return Transition.Unchanged(state);
        }

        if (Stopping(facts) is { } stopping)
        {
            return Stop(state, facts, stopping);
        }

        var dividend = PayDividend(state, facts);
        var afterDividend = dividend?.State ?? state;
        var entries = dividend is null ? new List<LedgerEntry>() : [.. dividend.Entries];

        var adjusted = Adjust(afterDividend, facts);

        if (EarlyAssignment(adjusted, facts) is { } early)
        {
            return new Transition(early.State, [.. entries, .. early.Entries]);
        }

        if (Expiry(adjusted, facts) is { } expiry)
        {
            entries.AddRange(expiry.Entries);
            adjusted = expiry.State;
        }

        if (Bound(adjusted, facts) is { } bound)
        {
            entries.AddRange(bound.Entries);
            adjusted = bound.State;
        }

        return new Transition(adjusted, entries);
    }

    /// <summary>
    /// A roll: the short is bought back and a new one sold on the same session.
    /// </summary>
    /// <remarks>
    /// <b>Both legs reach the ledger</b>, without which the projection cannot
    /// rebuild [D-W35]. The paying leg is <see cref="LedgerEntryKind.PremiumPaid"/>
    /// rather than <see cref="LedgerEntryKind.BoughtToClose"/>, which is the
    /// distinction D-W48 draws: a roll pays a premium and opens a position, a
    /// close pays a premium and ends one, and after the fact the sequence alone
    /// cannot tell a trial closed at its last permitted roll from one closed by
    /// choice.
    /// <para>
    /// The roll's own decision row is Phase 4's, where <c>decisions</c> lands.
    /// This applies a choice; it does not make one.
    /// </para>
    /// </remarks>
    public Transition Roll(
        TrialState state,
        DateOnly session,
        Fill bought,
        ContractIdentity opened,
        Fill sold)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(opened);
        ArgumentNullException.ThrowIfNull(bought);
        ArgumentNullException.ThrowIfNull(sold);

        if (state.IsClosed)
        {
            throw new InvalidOperationException(
                $"The trial closed on {state.ClosedOn:yyyy-MM-dd} and cannot be rolled. A "
                + "closed trial has returned to cash [D-W14], so there is no short to buy "
                + "back.");
        }

        if (state.Contract is null)
        {
            throw new InvalidOperationException(
                "A roll buys back a short and this trial holds none. Only a short put or a "
                + "short call can be rolled, and this state is "
                + $"'{state.State}'.");
        }

        var rolled = state.RolledInto(
            session, opened, state.PremiumBanked + bought.Net + sold.Net);

        return new Transition(
            rolled,
            [
                .. PremiumEntries(
                    session, session, LedgerEntryKind.PremiumPaid, bought, state.Contract),
                .. PremiumEntries(
                    session, session, LedgerEntryKind.PremiumReceived, sold, opened),
            ]);
    }

    /// <summary>
    /// Ending the trial by buying its short back [D-W54].
    /// </summary>
    /// <remarks>
    /// <b>The same close as <see cref="Bound"/>, reached deliberately.</b> A
    /// maker that closes and a bound that binds put the account in the same
    /// place, so the arithmetic is one path: the short is bought back and shares
    /// the trial holds are sold at the close. Only <see cref="TrialCloseKind"/>
    /// differs, and it differs because the trigger did.
    /// <para>
    /// The price arrives as a <see cref="Fill"/> rather than being read from the
    /// session, which is what every choice-driven transition does and what
    /// <see cref="Bound"/> cannot do: a bound fires without anyone choosing, so it
    /// has no choice to carry a price. The purchase pays the ask either way
    /// [D-W12, D-W49].
    /// </para>
    /// </remarks>
    public Transition CloseByChoice(TrialState state, SessionFacts facts, Fill bought)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(bought);

        if (state.IsClosed)
        {
            throw new InvalidOperationException(
                $"The trial closed on {state.ClosedOn:yyyy-MM-dd} and cannot be closed again. "
                + "A closed trial has returned to cash [D-W14], so there is no short to buy "
                + "back.");
        }

        if (state.Contract is not { } contract)
        {
            throw new InvalidOperationException(
                "A close buys back a short and this trial holds none. Only a short put or a "
                + $"short call can be closed by choice, and this state is '{state.State}'.");
        }

        var settles = _calendar.NextSessionAfter(facts.Session);

        var entries = new List<LedgerEntry>(
            PremiumEntries(
                facts.Session, settles, LedgerEntryKind.BoughtToClose, bought, contract));

        if (state.Shares > 0)
        {
            entries.Add(new LedgerEntry(
                facts.Session,
                settles,
                LedgerEntryKind.SharesSold,
                facts.UnderlyingClose * state.Shares));
        }

        return new Transition(
            state.ClosedTo(
                settles,
                facts.Session,
                TrialCloseKind.ClosedByChoice,
                state.PremiumBanked + bought.Net),
            entries);
    }

    /// <summary>
    /// Selling a covered call against shares the trial already holds.
    /// </summary>
    /// <remarks>
    /// It commits no further capital [D-W43]: the figure was fixed when the put
    /// was sold and the shares are what that capital bought, so nothing here
    /// touches <see cref="TrialState.CommittedCapital"/>.
    /// </remarks>
    public Transition WriteCall(
        TrialState state,
        DateOnly session,
        ContractIdentity call,
        Fill sold)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(sold);

        if (state.State is not PositionState.HoldingShares)
        {
            throw new InvalidOperationException(
                $"A covered call is written against held shares and this trial is "
                + $"'{state.State}'. An uncovered call is not a wheel position [D-W16].");
        }

        if (call.Right is not OptionRight.Call)
        {
            throw new ArgumentOutOfRangeException(nameof(call), call.Right, "This is not a call.");
        }

        return new Transition(
            state.ShortCallFrom(session, call, state.PremiumBanked + sold.Net),
            [.. PremiumEntries(
                session, session, LedgerEntryKind.PremiumReceived, sold, call)]);
    }

    /// <summary>
    /// The action ending the trial, when one of this session's actions is a kind
    /// the lab does not model [D-W47].
    /// </summary>
    private static CorporateAction? Stopping(SessionFacts facts) =>
        facts.Actions
            .Where(action => action.Action.ExDate == facts.Session)
            .Select(action => action.Action)
            .FirstOrDefault(action => !Modelled(action.Kind));

    /// <summary>
    /// What the lab has a transition for. Everything else stops the trial rather
    /// than passing through it [D-W47].
    /// </summary>
    private static bool Modelled(CorporateActionKind kind) => kind
        is CorporateActionKind.OrdinaryDividend
        or CorporateActionKind.NonOrdinaryDividend
        or CorporateActionKind.Split;

    /// <summary>
    /// A trial ended by an action the lab does not model, valued at the close
    /// [D-W47, D-W49].
    /// </summary>
    /// <remarks>
    /// <b>Valued rather than zeroed.</b> [D-W47] says the trial stops and carries
    /// the action as its reason; it does not say the position is liquidated at
    /// nothing. Zeroing it made every name with a corporate action a total loss,
    /// which is a bias with a sign in a lab whose criterion is comparing decision
    /// quality across makers: a maker that happened to hold the name with the
    /// merger would be scored worse for an event no maker chose.
    /// <para>
    /// <b>The mark is a model and the entry says so.</b> Shares at the session's
    /// close and the short at the ask, which is what buying it back would cost and
    /// the side of the spread the account does not choose [D-W12]. Nothing is
    /// traded here, so this is one <c>stopped</c> entry carrying the mark rather
    /// than a sale and a buy-back that did not happen.
    /// </para>
    /// </remarks>
    private Transition Stop(TrialState state, SessionFacts facts, CorporateAction action)
    {
        var settles = _calendar.NextSessionAfter(facts.Session);
        var marked = facts.UnderlyingClose * state.Shares;

        if (state.Contract is { } contract)
        {
            if (facts.ShortContractAsk is not { } ask)
            {
                throw new InvalidOperationException(
                    $"The trial is short '{contract}' and this session carries no ask for it, so "
                    + $"the position cannot be marked and the {action.Kind} on "
                    + $"{action.ExDate:yyyy-MM-dd} cannot be valued. A stopped trial is valued "
                    + "at the close [D-W49], and a mark this lab cannot observe is not one it "
                    + "invents.");
            }

            marked -= ContractTerms.CashFor(ask);
        }

        return new Transition(
            state.ClosedTo(settles, facts.Session, TrialCloseKind.Stopped, state.PremiumBanked),
            [
                new LedgerEntry(
                    facts.Session,
                    settles,
                    LedgerEntryKind.Stopped,
                    marked,
                    state.Contract,
                    $"{Storage.StoreCorporateActionKind.ToStored(action.Kind)} on "
                    + $"{action.ExDate:yyyy-MM-dd}, marked at the close"),
            ]);
    }

    /// <summary>
    /// A dividend the trial's shares earned [D-W41].
    /// </summary>
    /// <remarks>
    /// <b>Entitlement is read off the state entering the session</b>, which is
    /// exactly "holding the shares before the ex-dividend date": the state at the
    /// start of the ex-date session is what the prior session's close left.
    /// <para>
    /// <b>Only an ordinary dividend pays cash</b> [D-W44]. A non-ordinary one
    /// adjusts the contract instead, by calling for delivery of the dividend, so
    /// it reaches the trial through the deliverable rather than the ledger.
    /// </para>
    /// </remarks>
    private Transition? PayDividend(TrialState state, SessionFacts facts)
    {
        var dividend = facts.Actions.FirstOrDefault(action =>
            action.Action.ExDate == facts.Session
            && action.Action.Kind is CorporateActionKind.OrdinaryDividend);

        if (dividend is null || state.Shares == 0 || dividend.Action.Amount is not { } perShare)
        {
            return null;
        }

        var paid = _calendar.NextSessionAfter(facts.Session);

        return new Transition(
            state,
            [
                new LedgerEntry(
                    facts.Session, paid, LedgerEntryKind.Dividend, perShare * state.Shares),
            ]);
    }

    /// <summary>
    /// An adjustment moves the deliverable and leaves the strike [D-W17], and its
    /// terms are transcribed rather than computed [D-W36].
    /// </summary>
    private static TrialState Adjust(TrialState state, SessionFacts facts)
    {
        var adjusting = facts.Actions.FirstOrDefault(action =>
            action.Action.ExDate == facts.Session
            && action.Action.Kind
                is CorporateActionKind.Split
                or CorporateActionKind.NonOrdinaryDividend);

        if (adjusting is null || state.Contract is not { } contract)
        {
            return state;
        }

        if (adjusting.StatedSuccessor is not { } stated)
        {
            throw new InvalidOperationException(
                $"A {Storage.StoreCorporateActionKind.ToStored(adjusting.Action.Kind)} on "
                + $"{adjusting.Action.ExDate:yyyy-MM-dd} adjusts '{contract}' and states no "
                + "successor terms. Adjusted terms are transcribed from what the adjusting "
                + "authority states and are never derived from a ratio [D-W36], so this "
                + "stops rather than computing one.");
        }

        return state.WithContract(ContractIdentity.Of(
            contract.Underlying,
            contract.Expiry,
            contract.Right,
            stated.Strike,
            stated.DeliverableShares));
    }

    /// <summary>
    /// A short call assigned the session before an ex-dividend date [D-W42].
    /// </summary>
    /// <remarks>
    /// <b>Unadjusted dividends only</b>, as amended at 3.2. Where the contract is
    /// adjusted the holder receives the dividend through the deliverable and has
    /// no reason to surrender the option's time value, so the adjusted case has no
    /// early assignment at all [D-W44].
    /// <para>
    /// The condition is chosen rather than transcribed, and no rule governs it:
    /// whether the holder of a long call exercises early is that holder's
    /// decision. A holder who exercises captures the dividend and gives up the
    /// remaining time value, so the exchange is worth making when the first
    /// exceeds the second.
    /// </para>
    /// </remarks>
    private Transition? EarlyAssignment(TrialState state, SessionFacts facts)
    {
        if (state.State is not PositionState.ShortCall
            || state.Contract is not { } call
            || facts.ShortContractBid is not { } bid)
        {
            return null;
        }

        var next = _calendar.NextSessionAfter(facts.Session);

        var dividend = facts.Actions.FirstOrDefault(action =>
            action.Action.ExDate == next
            && action.Action.Kind is CorporateActionKind.OrdinaryDividend);

        if (dividend?.Action.Amount is not { } perShare)
        {
            return null;
        }

        var intrinsic = Math.Max(0m, facts.UnderlyingClose - call.Strike);
        var timeValue = bid - intrinsic;

        return perShare > timeValue ? CallAway(state, facts.Session, call) : null;
    }

    /// <summary>
    /// Expiry, resolved by exercise at one cent in the money [D-W38].
    /// </summary>
    private Transition? Expiry(TrialState state, SessionFacts facts)
    {
        if (state.Contract is not { } contract || contract.Expiry != facts.Session)
        {
            return null;
        }

        var moneyness = contract.Right is OptionRight.Put
            ? contract.Strike - facts.UnderlyingClose
            : facts.UnderlyingClose - contract.Strike;

        if (moneyness < ExerciseByExceptionThreshold)
        {
            return ExpireWorthless(state, facts.Session, contract);
        }

        return contract.Right is OptionRight.Put
            ? Assign(state, facts.Session, contract)
            : CallAway(state, facts.Session, contract);
    }

    /// <summary>
    /// A short expiring out of the money, or in by less than a cent [D-W38].
    /// </summary>
    /// <remarks>
    /// An entry with a zero amount rather than no entry, because the projection
    /// rebuilt from the ledger has to know the short closed and no other table
    /// says so [D-W48]. A put expiring worthless ends the trial; a call expiring
    /// worthless leaves the shares, so the trial continues.
    /// </remarks>
    private Transition ExpireWorthless(
        TrialState state,
        DateOnly session,
        ContractIdentity contract)
    {
        var next = _calendar.NextSessionAfter(session);

        var entry = new LedgerEntry(
            session, next, LedgerEntryKind.ExpiredWorthless, 0m, contract);

        if (contract.Right is OptionRight.Put)
        {
            return new Transition(
                state.ClosedTo(
                    next, session, TrialCloseKind.ExpiredWorthless, state.PremiumBanked),
                [entry]);
        }

        // A call expiring leaves the shares behind, so there have to be shares
        // with a basis. Without this the null-forgiving read below threw a
        // NullReferenceException naming nothing, where everything else here
        // refuses and says what was wrong.
        if (state.GrossBasis is not { } basis)
        {
            throw new InvalidOperationException(
                $"'{contract}' is a call expiring against a position that never held shares, so "
                + "there is no basis for it to leave behind. A covered call is written against "
                + "held shares [D-W16, D-W19], and a trial reaching here without them was "
                + "assembled rather than played.");
        }

        return new Transition(
            state.HoldingSharesFrom(next, state.Shares, basis, state.PremiumBanked),
            [entry]);
    }

    /// <summary>
    /// A short put assigned: the shares arrive and the cash leaves [D-W38, D-W39].
    /// </summary>
    /// <remarks>
    /// <b>The state takes effect on the next session, not this one.</b>
    /// Assignment is determined after the close and is not known to the account
    /// until the next morning [D-W39], so no decision made on this session may
    /// depend on it.
    /// <para>
    /// <b>The cash is the aggregate exercise price and the shares are the
    /// deliverable, which are the same number only for a standard contract.</b>
    /// This paid strike times the deliverable until 3.3's review, so an adjusted
    /// put charged 7,500 against a trial that had committed 5,000 [D-W17]. Gross
    /// basis follows from the pair rather than from the strike: it is what was
    /// paid divided by what arrived, and for a 150-share deliverable that is not
    /// the strike. `WORKED_EXAMPLE.md` §6.3's 50.00 is the standard case of the
    /// same arithmetic, which is why reading the strike looked right.
    /// </para>
    /// </remarks>
    private Transition Assign(TrialState state, DateOnly session, ContractIdentity put)
    {
        var known = _calendar.NextSessionAfter(session);
        var shares = put.DeliverableShares;
        var paid = ContractTerms.AggregateExercisePrice(put);

        return new Transition(
            state.HoldingSharesFrom(known, shares, paid / shares, state.PremiumBanked),
            [
                new LedgerEntry(session, known, LedgerEntryKind.Assignment, -paid, put),
                .. AssignmentFee(session, known, put),
            ]);
    }

    /// <summary>
    /// The fee an exercise is charged, when one is [D-W50].
    /// </summary>
    /// <remarks>
    /// Zero for the schedule this lab is configured against, and read rather than
    /// assumed, because the key is what makes a broker that charges a stored value
    /// changing rather than code changing. No row when nothing was charged, on the
    /// argument that a cost of zero is not a cost.
    /// </remarks>
    private IReadOnlyList<LedgerEntry> AssignmentFee(
        DateOnly session,
        DateOnly known,
        ContractIdentity contract) =>
        _costs.AssignmentFee == 0m
            ? []
            : [
                new LedgerEntry(
                    session,
                    known,
                    LedgerEntryKind.AssignmentFee,
                    -_costs.AssignmentFee,
                    contract),
            ];

    /// <summary>
    /// Shares taken at the strike, which ends the trial [D-W19, D-W39].
    /// </summary>
    /// <remarks>
    /// The proceeds are the aggregate exercise price and not the strike times the
    /// shares delivered. An adjustment moves the deliverable and leaves that
    /// figure alone [D-W17], so an adjusted call delivers more shares for the same
    /// cash, which is the whole of what an adjustment does.
    /// </remarks>
    private Transition CallAway(TrialState state, DateOnly session, ContractIdentity call)
    {
        var known = _calendar.NextSessionAfter(session);
        var proceeds = ContractTerms.AggregateExercisePrice(call);

        return new Transition(
            state.ClosedTo(known, session, TrialCloseKind.CalledAway, state.PremiumBanked),
            [
                new LedgerEntry(session, known, LedgerEntryKind.CallAway, proceeds, call),
                .. AssignmentFee(session, known, call),
            ]);
    }

    /// <summary>
    /// The roll bound or the day bound, whichever binds first [D-W14].
    /// </summary>
    /// <remarks>
    /// <b>One close kind for two triggers.</b> Which of them fired is read from
    /// <c>rolls_used</c> beside <c>opened_on</c> and <c>closed_on</c>, so a second
    /// value would state one fact twice.
    /// <para>
    /// Closing at market means closing everything the trial holds: a short is
    /// bought back and shares are sold, and a covered call is both. The prices are
    /// the session's close, which is the only price an end-of-day lab has.
    /// </para>
    /// <para>
    /// <b>It waits for a state the account knows about, and that is not a
    /// refinement.</b> This is the one place where one step of a session reads
    /// another's output: an expiry resolving to assignment leaves a state
    /// effective on the next session [D-W39], and a bound acting on it here would
    /// sell shares on the session the assignment happened, which is a decision
    /// depending on an assignment that occurred the same day. Written without the
    /// guard first, and the test that rolls to the bound and expires in the money
    /// on one session is what showed it: two entries came back where the account
    /// could only have produced one.
    /// </para>
    /// </remarks>
    private Transition? Bound(TrialState state, SessionFacts facts)
    {
        if (state.IsClosed || state.EffectiveFrom > facts.Session)
        {
            return null;
        }

        var days = facts.Session.DayNumber - state.OpenedOn.DayNumber;

        if (state.RollsUsed < _bounds.MaxRolls && days < _bounds.MaxTrialDays)
        {
            return null;
        }

        var settles = _calendar.NextSessionAfter(facts.Session);
        var entries = new List<LedgerEntry>();
        var banked = state.PremiumBanked;

        if (state.Contract is { } contract)
        {
            // The ask, not the intrinsic value [D-W49]. An option costs at least
            // its intrinsic to buy back and normally more, so closing at intrinsic
            // closes below the bid and flatters exactly the trials this bound
            // exists to terminate, which are the losing ones.
            //
            // A price per share times the multiplier, which is what a premium is
            // [D-W17]. Reading the deliverable here would have made an adjusted
            // contract cost half as much again to buy back.
            if (facts.ShortContractAsk is not { } ask)
            {
                throw new InvalidOperationException(
                    $"The bound binds on {facts.Session:yyyy-MM-dd} and this session carries no "
                    + $"ask for '{contract}', so the position cannot be closed at market. A "
                    + "forced close pays the ask [D-W49], and a price this lab cannot observe "
                    + "is not one it invents.");
            }

            // No commission, where CloseByChoice writes one. Two closes of this
            // position differ by Costs:CommissionPerContract and the difference is
            // which trigger fired, which is carried as an obligation at Phase 5
            // [BUILD_PLAN.md] rather than corrected here: the amounts are recorded
            // and changing them is a decision.
            var debit = ContractTerms.CashFor(ask);
            banked -= debit;

            entries.Add(new LedgerEntry(
                facts.Session, settles, LedgerEntryKind.BoughtToClose, -debit, contract));
        }

        if (state.Shares > 0)
        {
            entries.Add(new LedgerEntry(
                facts.Session,
                settles,
                LedgerEntryKind.SharesSold,
                facts.UnderlyingClose * state.Shares));
        }

        return new Transition(
            state.ClosedTo(settles, facts.Session, TrialCloseKind.ClosedAtBound, banked),
            entries);
    }
}
