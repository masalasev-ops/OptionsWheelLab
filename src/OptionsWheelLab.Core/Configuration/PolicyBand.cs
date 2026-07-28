namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// A named delta band belonging to one decision-maker's policy.
/// </summary>
/// <remarks>
/// The name is carried so a failing cross-key invariant can say which band it
/// failed against rather than only that one of them failed.
/// <para>
/// Delta is <see cref="decimal"/>, never <see cref="double"/>.
/// </para>
/// </remarks>
public sealed record PolicyBand(string Name, decimal DeltaMin, decimal DeltaMax);
