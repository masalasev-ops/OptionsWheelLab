using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-CeilingNotInsidePolicyBand: the predicate holds that the delta ceiling is
/// no tighter than any policy band.
/// </summary>
/// <remarks>
/// The ceiling is an outer bound on catastrophe, not a strategy parameter, so a
/// ceiling sitting inside a policy band would silently override that policy
/// rather than bound it [D-W23].
/// <para>
/// Values are supplied by the test, so nothing here reads configuration. Both
/// operands are config rows, seeded at 0.8, and enforcement is at config-write
/// time rather than startup [D-W27]. That the refusal actually happens is
/// FX-ConfigWriteRefusesInvariantBreach's; this covers the predicate alone.
/// </para>
/// </remarks>
public sealed class FX_CeilingNotInsidePolicyBand
{
    private static readonly PolicyBand Baseline = new("Baseline", 0.15m, 0.30m);
    private static readonly PolicyBand Random = new("Random", 0.20m, 0.35m);

    [Fact]
    public void Holds_when_the_ceiling_is_above_every_band()
    {
        Assert.True(ConfigurationInvariants.CeilingNotInsidePolicyBand(0.40m, [Baseline, Random]));
    }

    [Fact]
    public void Holds_when_the_ceiling_equals_the_loosest_band()
    {
        // The boundary case is the one that matters: the proposed ceiling of
        // 0.35 exactly equals the random control's upper bound, and equality is
        // permitted because the ceiling bounds the band rather than cutting
        // into it.
        Assert.True(ConfigurationInvariants.CeilingNotInsidePolicyBand(0.35m, [Baseline, Random]));
    }

    [Fact]
    public void Fails_when_the_ceiling_is_tighter_than_a_band()
    {
        Assert.False(ConfigurationInvariants.CeilingNotInsidePolicyBand(0.34m, [Baseline, Random]));
    }

    [Fact]
    public void Fails_when_the_ceiling_is_tighter_than_every_band()
    {
        Assert.False(ConfigurationInvariants.CeilingNotInsidePolicyBand(0.10m, [Baseline, Random]));
    }

    [Fact]
    public void Names_every_band_the_ceiling_is_tighter_than_not_only_the_first()
    {
        var offending = ConfigurationInvariants.BandsTighterThanCeiling(0.10m, [Baseline, Random]);

        Assert.Equal(["Baseline", "Random"], offending.Select(band => band.Name));
    }

    [Fact]
    public void Names_nothing_when_the_invariant_holds()
    {
        Assert.Empty(ConfigurationInvariants.BandsTighterThanCeiling(0.35m, [Baseline, Random]));
    }

    [Fact]
    public void Holds_vacuously_when_no_policy_band_is_configured()
    {
        Assert.True(ConfigurationInvariants.CeilingNotInsidePolicyBand(0.35m, []));
    }
}
