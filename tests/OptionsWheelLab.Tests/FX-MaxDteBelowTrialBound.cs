using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-MaxDteBelowTrialBound: the predicate holds that MaxDte is below
/// MaxTrialDays.
/// </summary>
/// <remarks>
/// An opening contract longer-dated than the trial bound would guarantee a
/// forced close at market before its own expiry, which makes the trial's
/// outcome an artefact of the bound rather than of the decision [D-W24].
/// <para>
/// Values are supplied by the test, so nothing here reads configuration. Both
/// operands are config rows, seeded at 0.8, and enforcement is at config-write
/// time rather than startup [D-W27]. That the refusal actually happens is
/// FX-ConfigWriteRefusesInvariantBreach's; this covers the predicate alone.
/// </para>
/// </remarks>
public sealed class FX_MaxDteBelowTrialBound
{
    [Fact]
    public void Holds_when_max_dte_is_below_the_trial_bound()
    {
        Assert.True(ConfigurationInvariants.MaxDteBelowTrialBound(maxDte: 70, maxTrialDays: 120));
    }

    [Fact]
    public void Fails_when_max_dte_equals_the_trial_bound()
    {
        // Equality fails. A contract expiring exactly on the bound would race
        // the forced close, and which one wins is not something the design
        // should leave open.
        Assert.False(ConfigurationInvariants.MaxDteBelowTrialBound(maxDte: 90, maxTrialDays: 90));
    }

    [Fact]
    public void Fails_when_max_dte_exceeds_the_trial_bound()
    {
        Assert.False(ConfigurationInvariants.MaxDteBelowTrialBound(maxDte: 120, maxTrialDays: 70));
    }

    [Fact]
    public void Holds_one_day_below_the_trial_bound()
    {
        Assert.True(ConfigurationInvariants.MaxDteBelowTrialBound(maxDte: 89, maxTrialDays: 90));
    }
}
