using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The widened as-of boundary, and its coupling to the stored timestamp format.
/// Not a registered fixture, so not named <c>FX-*</c>.
/// </summary>
public sealed class AsOfBoundaryTests
{
    /// <summary>
    /// The boundary must be the greatest value that renders under
    /// <see cref="StoreTimestamp.StoredFormat"/> for that date, derived from
    /// the format rather than restated.
    /// </summary>
    /// <remarks>
    /// The widening hardcodes 999 milliseconds, which is only the last instant
    /// of the day while the format carries three fractional digits. Adding
    /// precision to the format without changing the widening would quietly
    /// exclude the end of every day, so the expectation here is computed from
    /// the format and this test fails instead.
    /// </remarks>
    [Fact]
    public void The_boundary_is_the_last_instant_the_stored_format_can_render_for_that_date()
    {
        var date = new DateOnly(2026, 3, 20);

        var midnightNextDay = new DateTimeOffset(
            date.AddDays(1).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        var lastRenderable = midnightNextDay.AddTicks(-SmallestRenderableTick());

        Assert.Equal(StoreTimestamp.ToStored(lastRenderable), AsOfBoundary.LastInstantOf(date));
    }

    [Fact]
    public void One_increment_past_the_boundary_renders_on_the_following_date()
    {
        var date = new DateOnly(2026, 3, 20);

        var boundary = StoreTimestamp.ParseStored(AsOfBoundary.LastInstantOf(date));
        var justAfter = boundary.AddTicks(SmallestRenderableTick());

        Assert.Equal(date.AddDays(1), DateOnly.FromDateTime(justAfter.UtcDateTime));
    }

    [Fact]
    public void The_boundary_falls_on_the_date_it_was_asked_for()
    {
        var date = new DateOnly(2026, 3, 20);

        var boundary = StoreTimestamp.ParseStored(AsOfBoundary.LastInstantOf(date));

        Assert.Equal(date, DateOnly.FromDateTime(boundary.UtcDateTime));
    }

    /// <summary>
    /// The smallest interval the stored format can distinguish, read from the
    /// format's fractional digits rather than assumed.
    /// </summary>
    private static long SmallestRenderableTick()
    {
        var fractionalDigits = StoreTimestamp.StoredFormat.Count(character => character == 'f');

        Assert.True(fractionalDigits > 0, "the stored format should carry fractional seconds");

        // Integer arithmetic throughout. This was Math.Pow, which computes an
        // exact power of ten as a double and casts the result back, and the
        // floating-point guard caught it on the day it landed.
        var ticksPerRenderedUnit = 1L;

        for (var digit = 0; digit < fractionalDigits; digit++)
        {
            ticksPerRenderedUnit *= 10;
        }

        return TimeSpan.TicksPerSecond / ticksPerRenderedUnit;
    }
}
