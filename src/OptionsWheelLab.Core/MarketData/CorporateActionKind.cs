namespace OptionsWheelLab.Core.MarketData;

/// <summary>The kind of corporate action a `corporate_actions` row records.</summary>
/// <remarks>
/// `Split` only, deliberately. The fuller vocabulary, and whether the table
/// gains a CHECK the way `right` and membership's `kind` have one, is Phase 3's
/// dividend decision to settle: a rebuild migration should not ride 1.5
/// silently, and this checkpoint's writer records splits.
/// </remarks>
public enum CorporateActionKind
{
    Split,
}
