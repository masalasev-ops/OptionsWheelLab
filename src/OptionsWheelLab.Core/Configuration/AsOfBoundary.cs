using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// Widens a simulated date to the last instant of that date, for comparison
/// against a timestamp column.
/// </summary>
/// <remarks>
/// This is the one place the widening happens. <c>set_at</c> is a timestamp and
/// a simulated date is a date, and a date and a timestamp never appear on
/// opposite sides of a comparison. A timestamp for any instant on a day sorts
/// after that day's bare date, so comparing them directly would make everything
/// written on the as-of date invisible to that date, which is the opposite of
/// what D-W26 requires.
/// </remarks>
public static class AsOfBoundary
{
    /// <summary>
    /// The stored-form timestamp that every instant on <paramref name="date"/>
    /// is at or before.
    /// </summary>
    public static string LastInstantOf(DateOnly date) =>
        StoreTimestamp.ToStored(
            new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, 999, TimeSpan.Zero));
}
