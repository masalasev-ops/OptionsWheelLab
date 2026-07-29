using OptionsWheelLab.Core.Time;

namespace OptionsWheelLab.Tests;

/// <summary>
/// A clock stopped at one instant, for the entry points that read one.
/// </summary>
/// <remarks>
/// Only the composition root and the entry points take an <see cref="IClock"/>
/// [D-W30], so this is the only kind of test double the clock needs. Everything
/// below them takes an instant as a parameter and is given a literal, which is
/// why <c>ConfigWriter</c> and <c>MigrationRunner</c> were deliberately left
/// taking one.
/// </remarks>
internal sealed class FixedClock(DateTimeOffset instant) : IClock
{
    internal static readonly DateTimeOffset DefaultInstant =
        new(2026, 7, 28, 9, 15, 30, 250, TimeSpan.Zero);

    public DateTimeOffset UtcNow { get; } = instant;

    internal static FixedClock At() => new(DefaultInstant);
}
