using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of a contract right, being <c>put</c> or <c>call</c>.
/// </summary>
/// <remarks>
/// <b>The third instance of one shape, which is why it gets a helper rather than
/// a call to <c>ToString</c>.</b> A decimal's <c>ToString</c>, a
/// <c>DateOnly</c>'s, and an enum's are each culture-independent, plausible, and
/// wrong: this one yields <c>Put</c> where the schema says <c>put</c>, and
/// <c>Enum.Parse</c> reads the wrong case back unless told to ignore it. The
/// strike and the date each got a helper for exactly this, and <c>right</c>
/// looks too simple to need one, which is the trap.
/// <para>
/// <b>The permitted values are declared, not derived from the enum's
/// spelling.</b> Renaming a member is a source-level change; it must not
/// silently change the stored form of every existing row.
/// </para>
/// </remarks>
public static class StoreOptionRight
{
    public const string Put = "put";

    public const string Call = "call";

    public static string ToStored(OptionRight right) => right switch
    {
        OptionRight.Put => Put,
        OptionRight.Call => Call,
        _ => throw new ArgumentOutOfRangeException(
            nameof(right),
            right,
            $"'{right}' is not a contract right. The stored form is '{Put}' or '{Call}'."),
    };

    public static OptionRight ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            Put => OptionRight.Put,
            Call => OptionRight.Call,
            _ => throw new FormatException(
                $"'{stored}' is not a stored contract right. The permitted values are '{Put}' and "
                + $"'{Call}', lower case."),
        };
    }
}
