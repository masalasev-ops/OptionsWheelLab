namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// Where in the spread a sale fills [D-W12].
/// </summary>
/// <remarks>
/// <b>One member, and that is the decision rather than an unfinished
/// vocabulary.</b> A sale fills at the bid, fixed in advance and not a tunable:
/// end-of-day granularity means the realised fill is never observed, and filling
/// at the mid manufactures an edge from the accounting alone. Admitting a second
/// value here would be changing that rule, which is a decision and not an edit.
/// <para>
/// <b>It is the opposite case to <see cref="MarketData.CorporateActionKind"/>,
/// which was one member and wrong for it.</b> That vocabulary was incomplete
/// against a world OCC enumerates, so the missing values existed whether the enum
/// named them or not. This one is complete against a rule the lab wrote, so a
/// missing value would have to be authored before it could exist.
/// </para>
/// <para>
/// <b>The type exists so a stored word is checked rather than assumed.</b>
/// <c>Costs:FillPoint</c> is readable configuration, so a store carrying
/// <c>mid</c> is reachable, and a fill model that ignored the key would honour
/// D-W12 by accident while the row said otherwise. Parsing refuses instead.
/// </para>
/// <para>
/// <b>Deliberately not starting at zero</b>, on <see cref="PositionState"/>'s
/// precedent. With one member the hazard is at its sharpest: <c>default</c> would
/// be a valid-looking fill point that no configuration produced.
/// </para>
/// </remarks>
public enum FillPoint
{
    Bid = 1,
}
