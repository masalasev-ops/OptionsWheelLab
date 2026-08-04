using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// A decision a session needs, supplied rather than chosen [3.5].
/// </summary>
/// <remarks>
/// <b>A supplied sequence is a maker's output with the choosing already done.</b>
/// A maker produces one decision per session; this is that, written down in
/// advance, which is why Phase 4 replaces the sequence and changes nothing else
/// about the loop. Determinism is a property of the loop rather than of the
/// choice, so a run needs no maker to be worth asserting byte-identical.
/// <para>
/// <b>The contract is named and the price is a quote, not a fill.</b> The run
/// prices it through <see cref="FillModel"/> as of the session, so a choice
/// cannot carry a figure the fill model would not have produced. That keeps the
/// one place a quote becomes cash [D-W50] on the run's path rather than beside
/// it.
/// </para>
/// </remarks>
public abstract record TrialChoice(DateOnly Session)
{
    /// <summary>What the choice is called when a refusal has to name it.</summary>
    public abstract string Describe();
}

/// <summary>Opening the trial by selling a cash-secured put [D-W16].</summary>
public sealed record OpenPut(DateOnly Session, ContractIdentity Put, decimal Bid)
    : TrialChoice(Session)
{
    public override string Describe() => $"open by selling '{Put}'";
}

/// <summary>Writing a covered call against shares the trial holds [D-W43].</summary>
public sealed record WriteCoveredCall(DateOnly Session, ContractIdentity Call, decimal Bid)
    : TrialChoice(Session)
{
    public override string Describe() => $"write the covered call '{Call}'";
}

/// <summary>
/// Rolling: buying the short back and selling another on one session [D-W14].
/// </summary>
/// <remarks>
/// Two prices because it is two legs, and the account is on the side of the
/// spread it did not choose in both: the ask to buy back and the bid to sell
/// [D-W12, D-W49].
/// </remarks>
public sealed record RollInto(
    DateOnly Session,
    ContractIdentity Into,
    decimal Ask,
    decimal Bid) : TrialChoice(Session)
{
    public override string Describe() => $"roll into '{Into}'";
}
