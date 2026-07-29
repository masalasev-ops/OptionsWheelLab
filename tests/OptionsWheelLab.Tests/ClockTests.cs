using OptionsWheelLab.Core.Time;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Not a registered fixture, so not named <c>FX-*</c>. The registered checks at
/// 0.5 are FX-NoAmbientClock, which is a source guard, and
/// FX-ClockIsNotADateSource, which is a static check over shape. This is the
/// behaviour of the implementation itself.
/// </summary>
public sealed class ClockTests
{
    /// <summary>
    /// UTC by construction rather than by convention.
    /// </summary>
    /// <remarks>
    /// The trap this pins is a <c>DateTime</c> whose <c>Kind</c> nothing checks.
    /// <see cref="DateTimeOffset"/> closes it by naming an absolute instant, and
    /// the system clock's offset is asserted zero so a later change to
    /// <c>DateTimeOffset.Now</c> fails here as well as in the guard.
    /// </remarks>
    [Fact]
    public void The_system_clock_reads_utc()
    {
        Assert.Equal(TimeSpan.Zero, SystemClock.Instance.UtcNow.Offset);
    }

    [Fact]
    public void The_system_clock_moves_forward()
    {
        var first = SystemClock.Instance.UtcNow;
        var second = SystemClock.Instance.UtcNow;

        Assert.True(second >= first);
    }

    /// <summary>
    /// A fixed clock is a five-line record rather than a package. The alternative
    /// was <c>FakeTimeProvider</c>, which arrives with
    /// <c>Microsoft.Extensions.TimeProvider.Testing</c>, and a dependency added
    /// for this would be a dependency to audit forever [CLAUDE.md 4a].
    /// </summary>
    [Fact]
    public void A_fixed_clock_returns_the_instant_it_was_given()
    {
        var instant = new DateTimeOffset(2026, 7, 28, 9, 15, 30, 250, TimeSpan.Zero);
        IClock clock = new FixedClock(instant);

        Assert.Equal(instant, clock.UtcNow);
        Assert.Equal(instant, clock.UtcNow);
    }
}
