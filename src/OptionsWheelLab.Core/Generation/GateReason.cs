namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// Why the gate refused a candidate. One entry per ground a decision states.
/// </summary>
/// <remarks>
/// <b>A candidate carries a set of these rather than one.</b> The gate evaluates
/// every constraint and records every failing reason rather than the first
/// [D-W22], so a candidate failing two constraints shows both, which is what
/// makes the gate's effect auditable rather than merely visible [D-W5, D-W10].
/// <para>
/// <b>Declaration order is the order they are recorded in</b>, and that is
/// load-bearing rather than tidy. Three decision-makers receive byte-identical
/// candidate sets [D-W4], so a reason collection whose order varied between runs
/// would defeat the guarantee the whole feasible set exists to hold. Evaluating
/// in this order and appending also makes a duplicate impossible by
/// construction, which a set would have to enforce.
/// </para>
/// <para>
/// <b>Every entry names a ground its decision states.</b> That is a rule and not
/// an observation: at 2.3 the gate was about to reject a crossed quote on a
/// ground D-W22 did not state, and the decision was amended before the code
/// landed rather than after. A reason with no decision behind it is a rule
/// nobody agreed to.
/// </para>
/// <para>
/// <b>Deliberately not starting at zero</b>, on <see cref="Identity.OptionRight"/>'s
/// precedent: <c>default(GateReason)</c> is not a valid value and can be
/// detected, rather than reading as the first ground and putting a reason on a
/// candidate that never failed it.
/// </para>
/// <para>
/// 2.4's portfolio grounds are not here. They arrive with the constraints that
/// record them, because a member nothing calls is speculation.
/// </para>
/// </remarks>
public enum GateReason
{
    /// <summary>The quoted spread exceeds the cap as a fraction of mid [D-W22].</summary>
    SpreadCap = 1,

    /// <summary>The bid falls below the premium floor [D-W22].</summary>
    PremiumFloor = 2,

    /// <summary>The bid exceeds the ask, which is not a market [D-W22].</summary>
    CrossedMarket = 3,

    /// <summary>Absolute delta exceeds the ceiling [D-W23].</summary>
    DeltaCeiling = 4,

    /// <summary>Days to expiry fall outside the inclusive range [D-W24].</summary>
    ExpiryWindow = 5,

    /// <summary>The contract's buffered life contains a report date [D-W25].</summary>
    EarningsClearance = 6,
}
