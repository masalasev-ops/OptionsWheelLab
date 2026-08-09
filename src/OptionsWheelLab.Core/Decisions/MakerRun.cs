using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>One trial a maker drove, as it ended and what it wrote.</summary>
public sealed record DrivenTrial(
    long TrialId,
    TrialState State,
    IReadOnlyList<LedgerEntry> Entries);

/// <summary>What one maker did over a run [D-W4, D-W55].</summary>
/// <remarks>
/// A sequence rather than a state beside one list, because a run holds a sequence
/// of trials and the ledger is written per trial [D-W35, D-W55]. A flat list would
/// have to be partitioned by inferring where one trial ended.
/// </remarks>
public sealed record MakerRunResult(string MakerId, IReadOnlyList<DrivenTrial> Trials);

/// <summary>
/// The composition root: three makers driving one chain [D-W4].
/// </summary>
/// <remarks>
/// <b>This is the piece 3.5 turned out not to be.</b> `TrialRun` steps sessions and
/// applies choices somebody supplied; this decides what those choices are, and it
/// is the only thing in the tree that reads a maker's decision and writes anything
/// down.
/// <para>
/// <b>The shared evaluation happens once per session and right, not once per
/// maker</b> [D-W52, as amended]. Makers are grouped on the right their state
/// makes sellable, one <see cref="CandidateGenerator.SharedFor"/> call serves the
/// group, and each maker's caps are applied over it against its own book. Three
/// evaluations agreeing would satisfy the test and fail the property, because the
/// refusal that guards the stored set compares contract identities rather than
/// verdicts and could not see the difference.
/// </para>
/// <para>
/// <b>A machine per trial, built where the trial opens</b> [D-W53, as amended].
/// Bounds are fixed at a trial's open, so a root holding one machine would apply
/// the bounds of whichever trial opened first to every trial after it. The machine
/// is constructed at the open and discarded with the trial, which is why
/// <see cref="TrialRun"/> stopped holding one.
/// </para>
/// <para>
/// <b>Nothing here reads a clock.</b> The instant a decision is recorded under
/// arrives as a parameter, on <see cref="TrialRun"/>'s own finding: every date the
/// loop uses is a session date, and a run that stamped its own record would not be
/// byte-identical across two invocations [D-W28].
/// </para>
/// </remarks>
public sealed class MakerRun
{
    private readonly CandidateGenerator _generator;
    private readonly AsOfConfiguration _configuration;
    private readonly SessionCalendar _calendar;
    private readonly DecisionStore _decisions;
    private readonly TrialStore _trials;
    private readonly TrialRun _run;

    public MakerRun(
        CandidateGenerator generator,
        AsOfConfiguration configuration,
        FillModel fills,
        SessionCalendar calendar,
        DecisionStore decisions,
        TrialStore trials)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(fills);
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(trials);

        _generator = generator;
        _configuration = configuration;
        _calendar = calendar;
        _decisions = decisions;
        _trials = trials;
        _run = new TrialRun(fills, calendar);
    }

    /// <summary>
    /// Every maker over every session in the range, deciding and recording.
    /// </summary>
    /// <remarks>
    /// <b>The order within a session is decide, apply, then advance</b>, which is
    /// <see cref="TrialRun"/>'s order and its argument: a decision is what a maker
    /// would have made that morning against the state it could read, and the
    /// session's events resolve against its close.
    /// <para>
    /// Every session a maker is asked produces a decision row, including the one
    /// where it takes nothing. A decision is recorded with the set exactly as it
    /// stood [D-W3], and a maker that was offered a set and took none of it made a
    /// decision; a run that wrote nothing on those sessions would leave a reader
    /// unable to tell a maker that declined from one never asked.
    /// </para>
    /// </remarks>
    /// <remarks>
    /// The fill model is taken and handed straight to <see cref="TrialRun"/> rather
    /// than kept: this type never prices anything, because the prices a choice
    /// carries are read off the session and the walk applies them. It was held as a
    /// field until a sweep found the field assigned and never read.
    /// </remarks>
    /// <param name="recordedAt">
    /// The observation stamp every decision this run writes carries. Supplied
    /// rather than read, so two invocations produce one record [D-W28].
    /// </param>
    public IReadOnlyList<MakerRunResult> Walk(
        SyntheticChain chain,
        Ticker symbol,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<IDecisionMaker> makers,
        DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(makers);

        var arms = makers.Select(maker => new Arm(maker)).ToList();

        foreach (var session in _run.SessionsIn(chain, from, to))
        {
            var shared = SharedByRight(symbol, session, arms);

            foreach (var arm in arms)
            {
                Decide(chain, symbol, session, arm, shared, recordedAt);
            }

            foreach (var arm in arms)
            {
                Advance(chain, session, arm);
            }
        }

        foreach (var arm in arms)
        {
            arm.Finish(_trials);
        }

        return [.. arms.Select(arm => new MakerRunResult(arm.Maker.MakerId, arm.Completed))];
    }

    /// <summary>
    /// One evaluation per right the arms are in, keyed on the right [D-W52].
    /// </summary>
    /// <remarks>
    /// Ordered by the right rather than by the order the arms happen to be in, so
    /// the evaluations run in one order whatever order the makers were supplied
    /// in. The order is <see cref="OptionRight"/>'s own and not its stored form's,
    /// which is what <c>Order</c> on an enum gives; either would do here, and
    /// naming the wrong one would send a reader looking for a comparer that is not
    /// there.
    /// </remarks>
    private Dictionary<OptionRight, IReadOnlyList<GatedCandidate>> SharedByRight(
        Ticker symbol,
        DateOnly session,
        IReadOnlyList<Arm> arms) =>
        arms
            .Select(arm => CandidateGenerator.SellableRight(arm.PositionState))
            .Distinct()
            .Order()
            .ToDictionary(
                right => right,
                right => _generator.SharedFor(symbol, session, right));

    /// <summary>What one maker decides this session, and what the record keeps.</summary>
    private void Decide(
        SyntheticChain chain,
        Ticker symbol,
        DateOnly session,
        Arm arm,
        IReadOnlyDictionary<OptionRight, IReadOnlyList<GatedCandidate>> shared,
        DateTimeOffset recordedAt)
    {
        var right = CandidateGenerator.SellableRight(arm.PositionState);
        var offered = _generator.Against(shared[right], session, arm.Book);

        var decision = arm.Maker.Decide(
            symbol,
            session,
            arm.PositionState,
            arm.Book,
            offered,
            ShortFor(chain, session, arm));

        // The trial is minted before the decision naming it is recorded [D-W56].
        // A null written here could never be filled, since decisions is
        // append-only and refuses an update [D-W3].
        var trialId = decision.Kind is DecisionKind.OpenPut && decision.Chosen is { } opened
            ? _trials.OpenTrial(arm.Maker.MakerId, symbol, session, opened.Strike)
            : arm.TrialId;

        _decisions.Record(
            arm.Maker.MakerId,
            symbol,
            session,
            right,
            offered,
            decision.Kind,
            decision.Chosen,
            trialId,
            decision.PolicyVersion,
            recordedAt);

        if (ChoiceFor(decision, session, offered, arm) is not { } choice)
        {
            return;
        }

        var machine = arm.Open(trialId, MachineFor(session, arm));

        arm.Applied(_run.Apply(machine, arm.State, choice, chain));
    }

    /// <summary>
    /// A decision as the choice the run applies, or nothing.
    /// </summary>
    /// <remarks>
    /// <b>The adapter [D-W54, <see cref="MakerDecision"/>].</b> A maker returns a
    /// decision rather than a choice so that it can express taking nothing, which
    /// <see cref="TrialChoice"/> has no member for; this is where the two meet.
    /// The prices come from the session rather than from the maker: a sale fills
    /// at the chosen candidate's bid and a purchase pays the short's ask [D-W12,
    /// D-W49], and a maker that carried its own prices could name one the chain
    /// never quoted.
    /// </remarks>
    private static TrialChoice? ChoiceFor(
        MakerDecision decision,
        DateOnly session,
        IReadOnlyList<GatedCandidate> offered,
        Arm arm) =>
        decision.Kind switch
        {
            DecisionKind.OpenPut =>
                new OpenPut(session, decision.Chosen!, BidFor(decision.Chosen!, offered)),

            DecisionKind.OpenCall =>
                new WriteCoveredCall(session, decision.Chosen!, BidFor(decision.Chosen!, offered)),

            DecisionKind.Roll =>
                new RollInto(
                    session,
                    decision.Chosen!,
                    Ask: arm.ShortAsk!.Value,
                    Bid: BidFor(decision.Chosen!, offered)),

            DecisionKind.Close =>
                new CloseTrial(session, decision.Chosen!, Ask: arm.ShortAsk!.Value),

            _ => null,
        };

    /// <summary>
    /// The bid the run fills a sale at, read off the set the maker chose from.
    /// </summary>
    /// <remarks>
    /// Refuses rather than defaults. A chosen contract absent from the set it was
    /// chosen out of is a maker returning something it was not offered, which is a
    /// run described wrongly rather than a price to guess.
    /// </remarks>
    private static decimal BidFor(
        ContractIdentity chosen, IReadOnlyList<GatedCandidate> offered) =>
        offered.FirstOrDefault(candidate => candidate.Candidate.Quote.Contract == chosen)
            ?.Candidate.Quote.Bid
        ?? throw new InvalidOperationException(
            $"A maker chose '{chosen}', which is not in the {offered.Count} candidate(s) it was "
            + "offered. A choice outside the feasible set is a maker deciding against something "
            + "the run did not show it, and its fill would be a price nothing quoted [D-W12].");

    /// <summary>
    /// The short this maker holds, or null when it holds none [<see cref="OpenShort"/>].
    /// </summary>
    private OpenShort? ShortFor(SyntheticChain chain, DateOnly session, Arm arm)
    {
        if (arm.State is not { IsClosed: false, Contract: { } contract } state)
        {
            return null;
        }

        var facts = _run.FactsFor(chain, session, state);
        arm.ShortAsk = facts.ShortContractAsk;

        return new OpenShort(
            contract,
            facts.ShortContractAsk,
            facts.UnderlyingClose,
            state.OpenedOn,
            state.RollsUsed,
            arm.Bounds!,
            arm.TrialId);
    }

    /// <summary>
    /// The machine this trial runs under, built where the trial opens [D-W53].
    /// </summary>
    private (WheelStateMachine Machine, TrialBounds Bounds) MachineFor(DateOnly session, Arm arm)
    {
        if (arm.Machine is { } held)
        {
            return (held, arm.Bounds!);
        }

        var bounds = TrialBounds.ResolveFor(_configuration, session);

        return (
            new WheelStateMachine(_calendar, bounds, CostBounds.ResolveFor(_configuration, session)),
            bounds);
    }

    private void Advance(SyntheticChain chain, DateOnly session, Arm arm)
    {
        if (arm.State is not { IsClosed: false } state)
        {
            return;
        }

        arm.Applied(arm.Machine!.Advance(state, _run.FactsFor(chain, session, state)));

        if (arm.State!.IsClosed)
        {
            arm.Close(_trials);
        }
    }

    /// <summary>
    /// One maker's position in the run: its trial, its machine and its ledger.
    /// </summary>
    /// <remarks>
    /// <b>Mutable and private, because a run is a walk rather than a fold.</b> The
    /// state each arm carries between sessions is exactly what a maker's own
    /// history is, and D-W4's whole point is that the three diverge.
    /// </remarks>
    private sealed class Arm(IDecisionMaker maker)
    {
        private readonly List<LedgerEntry> _entries = [];
        private readonly List<DrivenTrial> _completed = [];

        internal IDecisionMaker Maker { get; } = maker;

        internal TrialState? State { get; private set; }

        internal WheelStateMachine? Machine { get; private set; }

        internal TrialBounds? Bounds { get; private set; }

        internal long? TrialId { get; private set; }

        /// <summary>The ask this session quoted for the short, for the adapter.</summary>
        internal decimal? ShortAsk { get; set; }

        internal IReadOnlyList<DrivenTrial> Completed => _completed;

        /// <summary>
        /// What this maker is in, which is cash between trials.
        /// </summary>
        /// <remarks>
        /// A closed trial reads as cash rather than as its final state, so a maker
        /// whose trial ended is offered puts again on the next session it is asked
        /// [D-W55]. A trial's closed state and an account holding nothing are the
        /// same account.
        /// </remarks>
        internal PositionState PositionState =>
            State is { IsClosed: false } open ? open.State : Positions.PositionState.Cash;

        /// <summary>
        /// What this maker's own book carries, which is its own trial and nothing
        /// else [D-W11].
        /// </summary>
        /// <remarks>
        /// A run over one symbol makes the per-name and total figures the same
        /// number. They are written separately rather than shared, because the two
        /// caps are different claims and a run over two names would make them
        /// differ.
        /// </remarks>
        internal BookState Book =>
            State is { IsClosed: false } open
                ? new BookState(open.CommittedCapital, open.CommittedCapital, open.GrossBasis)
                : BookState.Empty;

        internal WheelStateMachine Open(
            long? trialId, (WheelStateMachine Machine, TrialBounds Bounds) built)
        {
            TrialId = trialId;
            Machine = built.Machine;
            Bounds = built.Bounds;

            return built.Machine;
        }

        internal void Applied(Transition transition)
        {
            State = transition.State;
            _entries.AddRange(transition.Entries);
        }

        /// <summary>Writes the finished trial down and returns this arm to cash.</summary>
        internal void Close(TrialStore trials)
        {
            Write(trials);

            _entries.Clear();
            State = null;
            Machine = null;
            Bounds = null;
            TrialId = null;
        }

        /// <summary>
        /// A trial still open when the run ends is written down as it stands.
        /// </summary>
        /// <remarks>
        /// <b>Not an error and not discarded.</b> A run ends at a date rather than
        /// at a state, so a trial the window cut short is a real position and its
        /// ledger is what the account actually holds. `WORKED_EXAMPLE` §6.3 is the
        /// case: its covered calls are stated as legs and never as quotes, so no
        /// maker can write them and the trial ends the run holding shares.
        /// </remarks>
        internal void Finish(TrialStore trials)
        {
            if (State is null)
            {
                return;
            }

            Write(trials);
        }

        /// <summary>
        /// The trial's ledger, appended once.
        /// </summary>
        /// <remarks>
        /// <b>Shared between closing and finishing, because it was not.</b> A
        /// trial the window cut short was returned to the caller and never
        /// written, so the store held a `trials` row with no entries under it and
        /// a rebuild stopped rather than resolving. Found by the determinism
        /// fixture, which rebuilds every trial the run produced.
        /// </remarks>
        private void Write(TrialStore trials)
        {
            trials.Append(TrialId!.Value, _entries);
            _completed.Add(new DrivenTrial(TrialId.Value, State!, [.. _entries]));
        }
    }
}
