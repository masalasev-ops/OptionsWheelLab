using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Core.Positions;

/// <summary>What a run produced: the trial as it ended, and its ledger.</summary>
public sealed record RunResult(TrialState State, IReadOnlyList<LedgerEntry> Entries);

/// <summary>
/// One trial walked from cash to cash over a chain's sessions [3.5].
/// </summary>
/// <remarks>
/// <b>The loop was extracted rather than written.</b>
/// FX-TrialCompleteIncludesAssignment walked §6.3 session by session with the
/// sequence hand-inlined, so composing a run is lifting that walk into one place.
/// A run written fresh beside a test that walks the same trial would be two
/// producers of one sequence, which is what <see cref="WheelStateMachine.OpenTrial"/>
/// closed at 3.4 for a single leg.
/// <para>
/// <b>Nothing here reads a clock, and that is the checkpoint's own finding rather
/// than a precaution.</b> Every date the loop uses is a session date off the
/// calendar or the chain, and the ledger's two dates are both sessions [D-W39].
/// The only clock reads that reach a store are the migration and seed stamps,
/// which are setup rather than the run, so a fixed clock changes nothing inside
/// this type.
/// </para>
/// <para>
/// <b>A session's facts are derived from the chain, not supplied.</b> The close
/// comes from the bar, the actions from the chain, and the short's bid and ask
/// from that contract's quote on that session. A run handed its facts would be a
/// run asserting that whatever produced them was deterministic.
/// </para>
/// </remarks>
public sealed class TrialRun
{
    private readonly WheelStateMachine _machine;
    private readonly FillModel _fills;
    private readonly SessionCalendar _calendar;

    public TrialRun(WheelStateMachine machine, FillModel fills, SessionCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(fills);
        ArgumentNullException.ThrowIfNull(calendar);

        _machine = machine;
        _fills = fills;
        _calendar = calendar;
    }

    /// <summary>
    /// Steps every session in <paramref name="chain"/> between the two dates,
    /// applying the choices supplied for each.
    /// </summary>
    /// <remarks>
    /// <b>The order within a session is choice then advance.</b> A choice is what
    /// a maker would have decided that morning against the state it could read,
    /// and the session's events resolve against its close, so applying the choice
    /// first is what makes the sequence a decision rather than a reaction.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// When a supplied choice cannot be honoured by the state, or when the chain
    /// states no bar for a session it must step.
    /// </exception>
    public RunResult Walk(
        SyntheticChain chain,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<TrialChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(choices);

        RefuseChoicesOutsideTheRun(choices, from, to);

        var sessions = chain.Bars
            .Select(bar => bar.SessionDate)
            .Where(session => session >= from && session <= to)
            .Order()
            .ToList();

        if (sessions.Count == 0)
        {
            throw new InvalidOperationException(
                $"The chain states no bar between {from:yyyy-MM-dd} and {to:yyyy-MM-dd}, so "
                + "there is no session to step. A run over no sessions produces an empty "
                + "ledger, which is indistinguishable from a trial that did nothing.");
        }

        RefuseBarsTheCalendarDoesNotCarry(sessions);

        var entries = new List<LedgerEntry>();
        TrialState? state = null;

        foreach (var session in sessions)
        {
            foreach (var choice in choices.Where(c => c.Session == session))
            {
                var applied = Apply(state, choice, chain);
                state = applied.State;
                entries.AddRange(applied.Entries);
            }

            if (state is null || state.IsClosed)
            {
                continue;
            }

            var advanced = _machine.Advance(state, FactsFor(chain, session, state));
            state = advanced.State;
            entries.AddRange(advanced.Entries);
        }

        return state is null
            ? throw new InvalidOperationException(
                $"No choice opened a trial between {from:yyyy-MM-dd} and {to:yyyy-MM-dd}. A "
                + "run with nothing to walk is a run described wrongly rather than one with "
                + "no result.")
            : new RunResult(state, entries);
    }

    /// <summary>
    /// A supplied choice the state cannot honour is refused by name.
    /// </summary>
    /// <remarks>
    /// <b>A mis-described run must not produce a plausible ledger.</b> Skipping a
    /// choice the state cannot take would give a run that walked, produced
    /// entries, and described a trial nobody asked for. That is the failure
    /// [D-W48]'s vocabulary prevents one level down, where a ledger that cannot
    /// express an event is better than one that expresses the wrong one, and
    /// <see cref="TrialProjection"/> already refuses a closed trial that receives
    /// premium for the same reason.
    /// </remarks>
    private Transition Apply(TrialState? state, TrialChoice choice, SyntheticChain chain)
    {
        switch (choice)
        {
            case OpenPut open when state is null:
                return _machine.OpenTrial(
                    open.Put, _fills.Sell(open.Bid, open.Session), open.Session);

            case OpenPut open:
                throw Refuse(open, state, "the trial is already open");

            case WriteCoveredCall call when state?.State is PositionState.HoldingShares:
                return _machine.WriteCall(
                    state, call.Session, call.Call, _fills.Sell(call.Bid, call.Session));

            case WriteCoveredCall call:
                throw Refuse(
                    call,
                    state,
                    "a covered call is written against held shares [D-W16, D-W43]");

            case RollInto roll when state is { IsClosed: false, Contract: not null }:
                return _machine.Roll(
                    state,
                    roll.Session,
                    _fills.Buy(roll.Ask, roll.Session),
                    roll.Into,
                    _fills.Sell(roll.Bid, roll.Session));

            case RollInto roll:
                throw Refuse(roll, state, "a roll buys back a short and this trial holds none");

            default:
                throw new InvalidOperationException(
                    $"'{choice.GetType().Name}' is not a choice this run can apply. A choice "
                    + "the loop has no arm for would otherwise be skipped, and a skipped "
                    + "choice is a session that silently did nothing.");
        }
    }

    private static InvalidOperationException Refuse(
        TrialChoice choice,
        TrialState? state,
        string because)
    {
        var holding = state is null
            ? "no trial is open"
            : state.IsClosed
                ? $"the trial closed on {state.ClosedOn:yyyy-MM-dd}"
                : $"the trial is '{state.State}'";

        return new InvalidOperationException(
            $"The run was asked to {choice.Describe()} on {choice.Session:yyyy-MM-dd} and "
            + $"{holding}, so it cannot: {because}. A choice the state cannot honour is a run "
            + "described wrongly rather than a session to skip, and skipping it would produce "
            + "a plausible ledger for a trial nobody asked for.");
    }

    /// <summary>
    /// The chain and the calendar are two statements about which dates are
    /// sessions, and the calendar is the authority [D-W46].
    /// </summary>
    /// <remarks>
    /// <b>Found by the calendar being handed to this type and having nothing to
    /// do.</b> The loop takes its sessions from the chain's bars and the machine
    /// resolves the next session from the calendar, so a bar on a date the
    /// calendar does not carry makes the two disagree: the loop would step a
    /// session and then settle an assignment onto a date the chain has no bar
    /// for. Rather than drop the parameter, it checks the agreement its presence
    /// implied.
    /// <para>
    /// The reverse is not checked and is not an error. A calendar session with no
    /// bar is a name that did not trade, which is exactly the case a per-symbol
    /// sequence cannot express and the calendar exists to distinguish.
    /// </para>
    /// </remarks>
    private void RefuseBarsTheCalendarDoesNotCarry(IReadOnlyList<DateOnly> sessions)
    {
        var unknown = sessions.Where(session => !_calendar.IsSession(session)).ToList();

        if (unknown.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The chain states a bar on {unknown[0]:yyyy-MM-dd} and the calendar does not carry "
            + $"it as a session, with {unknown.Count} such date(s) in the range. The calendar is "
            + "the authority on what the market did [D-W46], so this is a scenario "
            + "contradicting it rather than a session to step.");
    }

    private static void RefuseChoicesOutsideTheRun(
        IReadOnlyList<TrialChoice> choices,
        DateOnly from,
        DateOnly to)
    {
        var outside = choices
            .Where(choice => choice.Session < from || choice.Session > to)
            .ToList();

        if (outside.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{outside.Count} choice(s) fall outside {from:yyyy-MM-dd} to {to:yyyy-MM-dd}, the "
            + $"first being {outside[0].Describe()} on {outside[0].Session:yyyy-MM-dd}. The "
            + "loop steps sessions in the range, so a choice outside it would never be "
            + "applied and the run would differ from the one described.");
    }

    /// <summary>
    /// What a session shows the trial, read off the chain.
    /// </summary>
    /// <remarks>
    /// The short's quote is looked up by the contract the trial holds, so a
    /// session where the chain quotes nothing for it carries no bid and no ask,
    /// and every rule that needs one refuses rather than assuming a price
    /// [D-W42, D-W49].
    /// </remarks>
    private SessionFacts FactsFor(SyntheticChain chain, DateOnly session, TrialState state)
    {
        var bar = chain.Bars.First(candidate => candidate.SessionDate == session);

        var quote = state.Contract is { } contract
            ? chain.Quotes.FirstOrDefault(
                candidate => candidate.SnapshotDate == session && candidate.Contract == contract)
            : null;

        return new SessionFacts(
            session, bar.Close, chain.Actions, quote?.Bid, quote?.Ask);
    }
}
