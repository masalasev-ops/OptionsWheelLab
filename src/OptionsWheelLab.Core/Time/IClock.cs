namespace OptionsWheelLab.Core.Time;

/// <summary>
/// Wall-clock time, and nothing else [D-W30].
/// </summary>
/// <remarks>
/// The lab has two kinds of time and they are unrelated: when this run is
/// happening, and which day is being simulated. This is the first. A simulated
/// date is never obtained from here; simulated dates arrive as parameters and
/// are threaded through, exactly as configuration is resolved as-of a date
/// rather than as-now [D-W26].
/// <para>
/// <b>Read at composition and entry points only.</b> Nothing below them takes an
/// <see cref="IClock"/>; they take instants as parameters, which is the shape
/// 0.3 deliberately gave <c>set_at</c> and the migration instant. So a test
/// supplies a fixed instant directly rather than through a fake, and the one
/// place that does need a clock injects it at the edge.
/// </para>
/// <para>
/// <b>One member, returning an instant rather than a date.</b> Converting an
/// instant to a trading date needs a market calendar and a session timezone, and
/// that is Phase 1. A clock that could hand out a <c>DateOnly</c> would make the
/// leakage above a one-line mistake instead of a visible conversion.
/// </para>
/// <para>
/// <b>Not <c>TimeProvider</c>.</b> That type's ambient instance and an injected
/// one are the same type, separated only by which member is touched, so the
/// source guard would have to infer types from text, which it states it cannot
/// do. It also carries <c>GetLocalNow</c> and a local timezone, which is the
/// machine-local surface this decision scopes out.
/// </para>
/// </remarks>
public interface IClock
{
    /// <summary>
    /// The instant the process is running at.
    /// </summary>
    /// <remarks>
    /// <see cref="DateTimeOffset"/> rather than <see cref="DateTime"/>, so the
    /// value is an absolute instant by construction rather than by convention. A
    /// <c>DateTime</c> carries a <c>Kind</c> nothing checks, and it is also not
    /// what <c>StoreTimestamp</c> takes.
    /// </remarks>
    DateTimeOffset UtcNow { get; }
}
