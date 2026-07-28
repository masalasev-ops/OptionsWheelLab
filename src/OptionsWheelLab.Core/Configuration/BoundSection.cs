namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// Records that <paramref name="Path"/> was bound to <paramref name="OptionsType"/>.
/// </summary>
/// <remarks>
/// Registered as a side effect of performing the binding rather than written by
/// hand, so the record cannot drift from what composition actually did. A test
/// that read a hand-maintained list would pass while the list was stale, which
/// is the failure this checkpoint exists to prevent.
/// </remarks>
public sealed record BoundSection(string Path, Type OptionsType);
