using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The scenario 3.3's fixtures are built on: WORKED_EXAMPLE §5's sessions and
/// §6.3's trial.
/// </summary>
/// <remarks>
/// <b>Shared because the sessions are the point, not the convenience.</b> §5's
/// dates are Fridays and the following Mondays, which is what next-session
/// notification [D-W39] and next-session settlement [D-W40] produce. A fixture
/// inventing consecutive calendar days would exercise neither, and would pass
/// against a calendar that did not exist.
/// <para>
/// <b>Not a registered fixture and not the worked example's expectations.</b> The
/// rows at 3.3 all carry <c>authored</c> in the registry: their expectations come
/// from the decisions rather than from that document, and what is borrowed here
/// is the shape of a realistic session sequence.
/// </para>
/// </remarks>
internal static class TrialScenario
{
    internal static readonly Ticker Symbol = Ticker.Normalise("WDGT");

    internal static readonly DateOnly Opened = new(2026, 3, 2);
    internal static readonly DateOnly FirstExpiry = new(2026, 4, 17);
    internal static readonly DateOnly MondayAfter = new(2026, 4, 20);
    internal static readonly DateOnly SecondExpiry = new(2026, 5, 15);
    internal static readonly DateOnly SecondMonday = new(2026, 5, 18);
    internal static readonly DateOnly ThirdExpiry = new(2026, 6, 19);
    internal static readonly DateOnly ThirdMonday = new(2026, 6, 22);

    /// <summary>WORKED_EXAMPLE §5's sessions, Fridays and the Mondays after.</summary>
    internal static readonly SessionCalendar Calendar = SessionCalendar.Of(
    [
        Opened, new(2026, 4, 8), FirstExpiry, MondayAfter,
        SecondExpiry, SecondMonday, ThirdExpiry, ThirdMonday,
    ]);

    /// <summary>The seeded bounds [CONFIG_REFERENCE].</summary>
    internal static readonly TrialBounds Seeded = new(MaxRolls: 2, MaxTrialDays: 120);

    /// <summary>The seeded costs [CONFIG_REFERENCE, D-W50].</summary>
    internal static readonly CostBounds Costs = new(
        CommissionPerContract: 0.65m, AssignmentFee: 0.00m, FillPoint: FillPoint.Bid);

    internal static WheelStateMachine Machine() => new(Calendar, Seeded, Costs);

    internal static WheelStateMachine MachineOn(SessionCalendar calendar) =>
        new(calendar, Seeded, Costs);

    /// <summary>
    /// A sale at <paramref name="bid"/>, priced the way the fill model prices it.
    /// </summary>
    /// <remarks>
    /// The premium and the commission separately [D-W50], so a fixture states the
    /// quote it means rather than the net it computed. §6.3's three sales are
    /// 0.95, 0.70 and 0.85, whose nets are 94.35, 69.35 and 84.35.
    /// </remarks>
    internal static Fill Sold(decimal bid, int contracts = 1) =>
        new(ContractTerms.CashFor(bid) * contracts, Costs.CommissionPerContract * contracts);

    /// <summary>A purchase at <paramref name="ask"/> [D-W49].</summary>
    internal static Fill Bought(decimal ask, int contracts = 1) =>
        new(-(ContractTerms.CashFor(ask) * contracts), Costs.CommissionPerContract * contracts);

    /// <summary>§6.3's opening leg: the 50.00 put sold at a bid of 0.95.</summary>
    internal static TrialState OpenedTrial() => OpenedTransition().State;

    /// <summary>The same open, when a fixture needs the entries it wrote.</summary>
    internal static Transition OpenedTransition() =>
        Machine().OpenTrial(Put(50.00m, FirstExpiry), Sold(0.95m), Opened);

    internal static ContractIdentity Put(decimal strike, DateOnly expiry) =>
        ContractIdentity.Of(Symbol, expiry, OptionRight.Put, strike);

    internal static ContractIdentity Call(decimal strike, DateOnly expiry) =>
        ContractIdentity.Of(Symbol, expiry, OptionRight.Call, strike);

    internal static SessionFacts Session(
        DateOnly session,
        decimal close,
        IReadOnlyList<ActionOnUnderlying>? actions = null,
        decimal? bid = null,
        decimal? ask = null) =>
        new(session, close, actions ?? [], bid, ask);

    internal static ActionOnUnderlying Ordinary(DateOnly exDate, decimal perShare) =>
        new(new CorporateAction(CorporateActionKind.OrdinaryDividend, exDate, Amount: perShare));

    internal static ActionOnUnderlying NonOrdinary(
        DateOnly exDate,
        decimal perShare,
        StatedSuccessorTerms successor) =>
        new(
            new CorporateAction(
                CorporateActionKind.NonOrdinaryDividend, exDate, Amount: perShare),
            successor);
}
