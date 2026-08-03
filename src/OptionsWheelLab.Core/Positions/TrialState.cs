using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// What a trial holds, and from which session it holds it.
/// </summary>
/// <remarks>
/// <b><see cref="EffectiveFrom"/> is why this is not just a tag.</b> Assignment
/// is determined after a session's close and is not known to the account until
/// the next morning [D-W39], so the state a decision on session D may read is not
/// the state the market produced on D. Carrying the date the state takes effect
/// is what lets a caller ask what the trial held on a date and get the answer
/// that was available then, rather than the one that was true.
/// <para>
/// <b>Committed capital is fixed at open and does not move</b> [D-W17]. A covered
/// call written against shares the trial already holds commits nothing further
/// [D-W43], so the caps read one figure per trial from open to close, and there
/// is no path here that changes it.
/// </para>
/// <para>
/// <b>Both bases, and both nullable.</b> Cost basis exists after assignment
/// [D-W19], so a trial in cash or short a put has none. Net basis is derived from
/// gross and the premium banked rather than stored twice, which keeps the two
/// conventions from drifting apart when premium arrives.
/// </para>
/// </remarks>
public sealed record TrialState
{
    private TrialState(
        PositionState state,
        DateOnly effectiveFrom,
        decimal committedCapital,
        int shares,
        decimal? grossBasis,
        decimal premiumBanked,
        ContractIdentity? contract,
        int rollsUsed,
        DateOnly openedOn,
        DateOnly? closedOn,
        TrialCloseKind? closeKind)
    {
        State = state;
        EffectiveFrom = effectiveFrom;
        CommittedCapital = committedCapital;
        Shares = shares;
        GrossBasis = grossBasis;
        PremiumBanked = premiumBanked;
        Contract = contract;
        RollsUsed = rollsUsed;
        OpenedOn = openedOn;
        ClosedOn = closedOn;
        CloseKind = closeKind;
    }

    public PositionState State { get; }

    /// <summary>The session from which this state is what the account holds.</summary>
    public DateOnly EffectiveFrom { get; }

    /// <summary>Fixed when the put was sold [D-W17], and never moved after.</summary>
    public decimal CommittedCapital { get; }

    public int Shares { get; }

    /// <summary>Per share, with premium tracked separately [D-W19].</summary>
    public decimal? GrossBasis { get; }

    /// <summary>Premium received less premium paid, over the whole trial.</summary>
    public decimal PremiumBanked { get; }

    /// <summary>The short contract, when there is one.</summary>
    public ContractIdentity? Contract { get; }

    public int RollsUsed { get; }

    public DateOnly OpenedOn { get; }

    public DateOnly? ClosedOn { get; }

    public TrialCloseKind? CloseKind { get; }

    public bool IsClosed => ClosedOn is not null;

    /// <summary>
    /// Per share, with premium reducing basis [D-W19].
    /// </summary>
    /// <remarks>
    /// <b>Derived rather than stored, and it moves as premium arrives.</b>
    /// D-W19 says premium reduces basis without saying which premium, and
    /// <c>WORKED_EXAMPLE.md</c> §6.3 states the figure at the moment of
    /// assignment, when only the put's premium had been banked. Recomputing from
    /// everything the trial has banked is the reading that makes the two
    /// conventions actually diverge over a trial's life, which is what D-W19
    /// exists to exploit: the gross-basis constraint binds a covered call strike
    /// and net basis would loosen it as premium accumulated. That is the drift
    /// the decision prevents, so net basis has to be the number that would have
    /// drifted.
    /// </remarks>
    public decimal? NetBasis =>
        GrossBasis is { } gross && Shares > 0 ? gross - (PremiumBanked / Shares) : null;

    /// <summary>
    /// A trial opening with a short put sold on <paramref name="on"/>.
    /// </summary>
    /// <remarks>
    /// The credit is banked here rather than by an event, because a trial that
    /// has not sold anything is not a trial: [D-W14] runs one from first open
    /// through to return to cash, and the open is the sale.
    /// </remarks>
    public static TrialState OpenShortPut(ContractIdentity put, decimal credit, DateOnly on)
    {
        ArgumentNullException.ThrowIfNull(put);

        if (put.Right is not OptionRight.Put)
        {
            throw new ArgumentOutOfRangeException(
                nameof(put),
                put.Right,
                "A wheel turn opens by selling a cash-secured put [D-W16]. A trial opened "
                + "against a call would be a covered call with no shares behind it.");
        }

        return new TrialState(
            PositionState.ShortPut,
            on,
            Generation.CommittedCapital.For(put),
            shares: 0,
            grossBasis: null,
            premiumBanked: credit,
            contract: put,
            rollsUsed: 0,
            openedOn: on,
            closedOn: null,
            closeKind: null);
    }

    internal TrialState HoldingSharesFrom(
        DateOnly effectiveFrom,
        int shares,
        decimal grossBasis,
        decimal premiumBanked) =>
        new(
            PositionState.HoldingShares,
            effectiveFrom,
            CommittedCapital,
            shares,
            grossBasis,
            premiumBanked,
            contract: null,
            RollsUsed,
            OpenedOn,
            ClosedOn,
            CloseKind);

    internal TrialState ShortCallFrom(
        DateOnly effectiveFrom,
        ContractIdentity call,
        decimal premiumBanked) =>
        new(
            PositionState.ShortCall,
            effectiveFrom,
            CommittedCapital,
            Shares,
            GrossBasis,
            premiumBanked,
            call,
            RollsUsed,
            OpenedOn,
            ClosedOn,
            CloseKind);

    /// <summary>The same position against adjusted terms [D-W36].</summary>
    internal TrialState WithContract(ContractIdentity contract) =>
        new(
            State,
            EffectiveFrom,
            CommittedCapital,
            Shares,
            GrossBasis,
            PremiumBanked,
            contract,
            RollsUsed,
            OpenedOn,
            ClosedOn,
            CloseKind);

    /// <summary>
    /// Premium moved without the position moving, which is the paying leg of a
    /// roll waiting for the credit that follows it.
    /// </summary>
    internal TrialState WithPremiumBanked(decimal premiumBanked) =>
        new(
            State,
            EffectiveFrom,
            CommittedCapital,
            Shares,
            GrossBasis,
            premiumBanked,
            Contract,
            RollsUsed,
            OpenedOn,
            ClosedOn,
            CloseKind);

    /// <summary>One short bought back and another sold, on one session [D-W14].</summary>
    internal TrialState RolledInto(
        DateOnly effectiveFrom,
        ContractIdentity opened,
        decimal premiumBanked) =>
        new(
            State,
            effectiveFrom,
            CommittedCapital,
            Shares,
            GrossBasis,
            premiumBanked,
            opened,
            RollsUsed + 1,
            OpenedOn,
            ClosedOn,
            CloseKind);

    /// <summary>
    /// Back to cash, which is where a trial ends [D-W14].
    /// </summary>
    /// <remarks>
    /// <paramref name="closedOn"/> is the session the closing event occurred in
    /// rather than the session it was known, which is what keeps
    /// <c>WORKED_EXAMPLE.md</c>'s 109 days at 109: §6.3 dates the call-away
    /// 2026-06-19 and counts to there. The cash's availability is the entry's
    /// <c>known_on</c> and a separate question [D-W40].
    /// <para>
    /// <b>Both bases go with the shares.</b> A trial back in cash holds none, and
    /// basis is a per-share figure, so carrying the old gross basis forward would
    /// state a cost for a position that no longer exists. Written the other way
    /// first, and the rebuild is what showed it: the <c>cash</c> row came back
    /// with a gross basis and a null net basis, since net is derived and already
    /// answered null on zero shares. One convention nulling itself while the other
    /// did not is what made the inconsistency visible rather than plausible.
    /// </para>
    /// </remarks>
    internal TrialState ClosedTo(
        DateOnly effectiveFrom,
        DateOnly closedOn,
        TrialCloseKind closeKind,
        decimal premiumBanked) =>
        new(
            PositionState.Cash,
            effectiveFrom,
            CommittedCapital,
            shares: 0,
            grossBasis: null,
            premiumBanked,
            contract: null,
            RollsUsed,
            OpenedOn,
            closedOn,
            closeKind);
}
