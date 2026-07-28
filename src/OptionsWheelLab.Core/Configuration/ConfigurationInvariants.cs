namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// The two cross-key configuration invariants [D-W23, D-W24], as pure
/// predicates over supplied values.
/// </summary>
/// <remarks>
/// Deliberately free of any host, config store, startup wiring or clock. The
/// operands are config rows, which are versioned and insertable while the
/// process runs, so enforcement belongs at config-write time: a version
/// violating an invariant is refused rather than recorded and detected on a
/// later boot. A startup check would leave every later version unguarded and
/// survives only as a backstop [D-W27].
/// <para>
/// The write path lands at Phase 0.8, which is where these are wired and where
/// FX-ConfigWriteRefusesInvariantBreach demonstrates the refusal.
/// </para>
/// </remarks>
public static class ConfigurationInvariants
{
    /// <summary>
    /// Holds when the gate's delta ceiling is no tighter than every policy
    /// band's upper bound.
    /// </summary>
    /// <remarks>
    /// The ceiling is an outer bound on catastrophe, not a strategy parameter.
    /// A ceiling sitting inside a policy band would silently override that
    /// policy rather than bound it [D-W23].
    /// </remarks>
    /// <returns><c>true</c> when the invariant holds.</returns>
    public static bool CeilingNotInsidePolicyBand(
        decimal maxDelta,
        IReadOnlyCollection<PolicyBand> policyBands)
    {
        ArgumentNullException.ThrowIfNull(policyBands);

        return policyBands.All(band => maxDelta >= band.DeltaMax);
    }

    /// <summary>
    /// Names the policy bands a delta ceiling is tighter than, empty when
    /// <see cref="CeilingNotInsidePolicyBand"/> holds.
    /// </summary>
    /// <remarks>
    /// Separate from the predicate so a refusal can state which bands it failed
    /// against, in the spirit of recording every failing reason rather than the
    /// first [D-W22].
    /// </remarks>
    public static IReadOnlyList<PolicyBand> BandsTighterThanCeiling(
        decimal maxDelta,
        IReadOnlyCollection<PolicyBand> policyBands)
    {
        ArgumentNullException.ThrowIfNull(policyBands);

        return policyBands.Where(band => maxDelta < band.DeltaMax).ToList();
    }

    /// <summary>
    /// Holds when the gate's maximum days to expiry is strictly below the trial
    /// day bound.
    /// </summary>
    /// <remarks>
    /// An opening contract longer-dated than the trial bound would guarantee a
    /// forced close at market before its own expiry, making the trial's outcome
    /// an artefact of the bound rather than of the decision [D-W24].
    /// </remarks>
    /// <returns><c>true</c> when the invariant holds.</returns>
    public static bool MaxDteBelowTrialBound(int maxDte, int maxTrialDays) => maxDte < maxTrialDays;
}
