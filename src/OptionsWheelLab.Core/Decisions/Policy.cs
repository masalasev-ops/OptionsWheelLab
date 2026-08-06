using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// One maker's delta band and expiry window, resolved as of the simulated date
/// [D-W26].
/// </summary>
/// <remarks>
/// <b>A policy is a configuration row rather than compiled code, so a variant is
/// a new row</b> [SYSTEM_DESIGN §3.5]. That is not a preference about shape: the
/// learning channel writes to the learner and nothing else [D-W6], and what a
/// channel can write is a row, so a policy living in code would be one no channel
/// could change and the learner would be a second frozen arm with a different
/// band. The baseline and the learner therefore share this record and their
/// selection rule, and differ only in the rows they read.
/// <para>
/// <b>The random maker differs in rule rather than in policy.</b> It is a control
/// separating selection skill from the return to being short volatility
/// [SYSTEM_DESIGN §3.5], and it draws uniformly where the other two prefer the
/// highest credit in band [WORKED_EXAMPLE §1]. Its band is still a band and it is
/// still resolved here.
/// </para>
/// <para>
/// <b>Two brackets, because one source states one of those claims.</b> This
/// sentence carried a single §3.5 reference until the two were separated, and
/// that section does not contain the phrase "highest credit" at all: the rule for
/// the baseline and the learner is stated in the worked example's setup and
/// nowhere in the narrative. Seventh instance in this corpus of a citation naming
/// a source for a property the source does not carry.
/// </para>
/// <para>
/// <b>Three named factories rather than one taking a maker's name</b>, because
/// the random maker's policy is not symmetrical with the other two: it reads its
/// own delta band and the <i>baseline's</i> expiry window. That asymmetry is what
/// <c>Policy:Random:</c> carrying no DTE keys expresses, which
/// <c>CONFIG_REFERENCE.md</c> calls the coupling rather than an omission. A prefix
/// parameter would hide it and send a reader looking for keys that do not exist.
/// </para>
/// <para>
/// <b>The first bound record to read two sub-prefixes, and the refusal names the
/// key rather than the maker that asked.</b> Resolving the random maker's policy
/// can stop on <c>Policy:Baseline:DteMin</c>, which is the right key to name:
/// that key failing breaks the baseline and the random maker both, where naming
/// the maker that happened to ask would point at one of the two.
/// </para>
/// </remarks>
public sealed record Policy(decimal DeltaMin, decimal DeltaMax, int DteMin, int DteMax)
{
    private const string Baseline = "Policy:Baseline:";
    private const string RandomBand = "Policy:Random:";
    private const string Learner = "Policy:Learner:";

    /// <summary>The frozen baseline's, which never changes [D-W4].</summary>
    /// <exception cref="InvalidOperationException">
    /// When any of the four has no value in force on that date.
    /// </exception>
    public static Policy ForBaseline(AsOfConfiguration configuration, DateOnly simulatedDate) =>
        Resolve(configuration, band: Baseline, window: Baseline, simulatedDate);

    /// <summary>
    /// The learner's, which the channel rewrites and this reads as of the date.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When any of the four has no value in force on that date.
    /// </exception>
    public static Policy ForLearner(AsOfConfiguration configuration, DateOnly simulatedDate) =>
        Resolve(configuration, band: Learner, window: Learner, simulatedDate);

    /// <summary>
    /// The random control's band inside the baseline's window.
    /// </summary>
    /// <remarks>
    /// The window is the baseline's deliberately. A control drawing from a
    /// different expiry window than the arm it is a control for would make a
    /// difference between the two partly opportunity rather than judgement, which
    /// is the argument <c>Policy:Random:DeltaMax</c> already carries about the
    /// delta ceiling [D-W4].
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// When any of the four has no value in force on that date.
    /// </exception>
    public static Policy ForRandom(AsOfConfiguration configuration, DateOnly simulatedDate) =>
        Resolve(configuration, band: RandomBand, window: Baseline, simulatedDate);

    /// <summary>
    /// Whether this policy will consider a candidate at these terms.
    /// </summary>
    /// <remarks>
    /// <b>Both ends are inclusive, and the band is the fifth constraint in this
    /// lab to state its own boundary.</b> 2.3 settled that each states it rather
    /// than one convention governing, after four gate constraints turned out to
    /// differ: the spread cap and the delta ceiling reject on exceeding, the
    /// premium floor rejects a bid strictly below it, the expiry window admits its
    /// own bounds, and the earnings buffer includes its edge. This matches
    /// <c>Gate:MinDte</c> to <c>Gate:MaxDte</c>, which [D-W24] makes an inclusive
    /// range in as many words.
    /// <para>
    /// <b>It decides what the worked example's random maker draws, and an earlier
    /// version of this remark said it decided nothing.</b> That version reasoned
    /// only about the learner: the 45.00 put carries a delta of exactly 0.10 and
    /// the learner's <c>DeltaMin</c> is exactly 0.10, and the 45.00 loses on
    /// credit either way, so the learner's row reproduces under either reading.
    /// True, and it generalised to the whole document, which is false.
    /// </para>
    /// <para>
    /// <c>Policy:Random:DeltaMin</c> is <i>also</i> exactly 0.10, and there the
    /// convention decides the drawn strike rather than only set membership. Under
    /// an exclusive floor the random maker's set is the 47.50 and the 50.00, its
    /// index-zero draw takes the 47.50, and §4's 45.00 becomes false. Measured
    /// rather than argued: for the worked example's seed the draw is index zero
    /// at every set size from two to fifteen. So the inclusive convention is
    /// load-bearing for the document, through the maker the earlier remark did not
    /// consider.
    /// </para>
    /// <para>
    /// The delta compared is the absolute value, as the gate's ceiling is
    /// [D-W23]: the chain states a put's delta as negative and a band written
    /// 0.20 to 0.30 means magnitudes.
    /// </para>
    /// </remarks>
    public bool Admits(decimal delta, int daysToExpiry)
    {
        var magnitude = Math.Abs(delta);

        return magnitude >= DeltaMin
            && magnitude <= DeltaMax
            && daysToExpiry >= DteMin
            && daysToExpiry <= DteMax;
    }

    private static Policy Resolve(
        AsOfConfiguration configuration,
        string band,
        string window,
        DateOnly simulatedDate)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new Policy(
            ResolvedBound.RequiredDecimal(configuration, band + "DeltaMin", simulatedDate),
            ResolvedBound.RequiredDecimal(configuration, band + "DeltaMax", simulatedDate),
            ResolvedBound.RequiredInt(configuration, window + "DteMin", simulatedDate),
            ResolvedBound.RequiredInt(configuration, window + "DteMax", simulatedDate));
    }
}
