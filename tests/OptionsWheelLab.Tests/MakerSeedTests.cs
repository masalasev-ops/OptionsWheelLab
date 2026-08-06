using OptionsWheelLab.Core.Decisions;
using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The derived seed. Not a registered fixture, so not named <c>FX-*</c>.
/// </summary>
/// <remarks>
/// <b>Every assertion here compares against a literal, and that is the whole
/// method.</b> The failure these exist to catch is a derivation built on
/// <see cref="HashCode"/> or <see cref="string.GetHashCode()"/>, both randomised
/// per process. Such a derivation agrees with itself inside one run, so a test
/// comparing two invocations passes while the property is false, and
/// FX-RunIsByteIdentical runs both of its invocations in one process. Only a
/// constant written into the source can tell the two apart.
/// <para>
/// The literals were measured from a run and written in, never computed by the
/// assertion. A test that derived its own expectation would be the same
/// instrument agreeing with itself one level up.
/// </para>
/// </remarks>
public sealed class MakerSeedTests
{
    /// <summary>The worked example's name and session, and the seeded value.</summary>
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");

    private static readonly DateOnly Session = new(2026, 3, 2);

    private const int Configured = 20260729;

    [Fact]
    public void The_derived_seed_for_a_known_triple_is_this_number()
    {
        Assert.Equal(809011066, MakerSeed.For(Configured, Symbol, Session));
    }

    /// <summary>
    /// The first draw for that seed, pinned across the whole int range.
    /// </summary>
    /// <remarks>
    /// <b>Full range rather than the three-candidate draw the maker makes.</b>
    /// Written first as <c>Next(3)</c> against a placeholder of zero, it passed
    /// on the first run: three outcomes make a coincidence with a placeholder a
    /// one-in-three event, and a pin that agrees with an unmeasured guess is
    /// indistinguishable from one nobody measured. A full-range draw is a
    /// fingerprint of the sequence instead of a sample from it.
    /// <para>
    /// What this pins is <see cref="Random"/> itself. The seeded constructor keeps
    /// the legacy algorithm where the parameterless one did not, so this is the
    /// residual recorded rather than assumed, and a runtime that changed it fails
    /// here rather than silently redrawing every control arm.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_first_draw_for_that_seed_is_this_number()
    {
        var random = new Random(MakerSeed.For(Configured, Symbol, Session));

        Assert.Equal(136963533, random.Next());
    }

    /// <summary>
    /// A different session gives a different seed, so the draw is per session and
    /// not per run.
    /// </summary>
    [Fact]
    public void A_different_session_derives_a_different_seed()
    {
        Assert.NotEqual(
            MakerSeed.For(Configured, Symbol, Session),
            MakerSeed.For(Configured, Symbol, Session.AddDays(1)));
    }

    /// <summary>A different name does too.</summary>
    [Fact]
    public void A_different_name_derives_a_different_seed()
    {
        Assert.NotEqual(
            MakerSeed.For(Configured, Symbol, Session),
            MakerSeed.For(Configured, Ticker.Normalise("ACME"), Session));
    }

    /// <summary>
    /// And so does a different configured seed, which is the row an operator
    /// changes to re-draw a whole experiment.
    /// </summary>
    [Fact]
    public void A_different_configured_seed_derives_a_different_seed()
    {
        Assert.NotEqual(
            MakerSeed.For(Configured, Symbol, Session),
            MakerSeed.For(Configured + 1, Symbol, Session));
    }
}
