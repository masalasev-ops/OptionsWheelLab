using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of a position's state, being <c>cash</c>, <c>short_put</c>,
/// <c>holding_shares</c> or <c>short_call</c> [DATA_AND_SCHEMA §4.3].
/// </summary>
/// <remarks>
/// <see cref="StoreOptionRight"/>'s shape, declared now rather than when
/// <c>positions</c> lands. A tag whose stored form arrives with the table is a
/// tag whose stored form was never a decision.
/// <para>
/// <b>The permitted values are declared, not derived from the enum's
/// spelling.</b> The argument is stronger here than for a right or a
/// transition kind, where a casing rule would have worked by accident:
/// <c>ToString</c> yields <c>HoldingShares</c>, and no casing of it produces
/// <c>holding_shares</c>. Two of these four are unreachable by any
/// transformation of the member name, so a derivation would have to be a
/// mapping, which is what this is.
/// </para>
/// </remarks>
public static class StorePositionState
{
    public const string Cash = "cash";

    public const string ShortPut = "short_put";

    public const string HoldingShares = "holding_shares";

    public const string ShortCall = "short_call";

    public static string ToStored(PositionState state) => state switch
    {
        PositionState.Cash => Cash,
        PositionState.ShortPut => ShortPut,
        PositionState.HoldingShares => HoldingShares,
        PositionState.ShortCall => ShortCall,
        _ => throw new ArgumentOutOfRangeException(
            nameof(state),
            state,
            $"'{state}' is not a position state. The stored form is '{Cash}', '{ShortPut}', "
            + $"'{HoldingShares}' or '{ShortCall}'."),
    };

    public static PositionState ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            Cash => PositionState.Cash,
            ShortPut => PositionState.ShortPut,
            HoldingShares => PositionState.HoldingShares,
            ShortCall => PositionState.ShortCall,
            _ => throw new FormatException(
                $"'{stored}' is not a stored position state. The permitted values are '{Cash}', "
                + $"'{ShortPut}', '{HoldingShares}' and '{ShortCall}', lower case."),
        };
    }
}
