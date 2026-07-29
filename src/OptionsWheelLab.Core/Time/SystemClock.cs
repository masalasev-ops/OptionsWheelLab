namespace OptionsWheelLab.Core.Time;

/// <summary>
/// The clock implementation, and the only place in the tree that reads the
/// ambient clock [D-W30].
/// </summary>
/// <remarks>
/// <b>This file is named in <c>guards.ps1</c> as the one permitted site for an
/// ambient clock call.</b> That is not an exemption mechanism: the rule is "no
/// ambient clock call outside the clock implementation", so the carve-out is
/// part of the rule rather than an escape from it, and it is one hardcoded path
/// rather than a list anything can be added to. The guard also refuses to pass
/// if scanning this file finds no ambient call at all, because a carve-out
/// pointing at a file that no longer needs it is a hole nobody would notice.
/// <para>
/// Stateless, so a single instance serves every caller.
/// </para>
/// </remarks>
public sealed class SystemClock : IClock
{
    /// <summary>The shared instance. Reading a clock allocates nothing.</summary>
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
