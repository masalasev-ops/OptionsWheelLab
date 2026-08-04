using OptionsWheelLab.Core.Decisions;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of <see cref="DecisionKind"/>, which migration 9's
/// <c>CHECK</c> carries the same list of.
/// </summary>
/// <remarks>
/// Declared rather than derived from the member's spelling, on the reason every
/// vocabulary here is: a rename would silently change what the store holds, and
/// the stored form is data while the member name is code.
/// <para>
/// FX-StoredVocabulariesMatchTheirChecks holds this against the <c>CHECK</c>, and
/// from 4.2 it also holds that every declared vocabulary is either enforced or
/// named as unenforced. This one is enforced from the migration that introduces
/// it, which is what <see cref="StoreGateReason"/> was not for four checkpoints.
/// </para>
/// </remarks>
public static class StoreDecisionKind
{
    public const string OpenPut = "open_put";

    public const string OpenCall = "open_call";

    public const string Roll = "roll";

    public const string Close = "close";

    public const string None = "none";

    /// <summary>Every permitted value, for the refusal messages below.</summary>
    public static readonly string[] All = [OpenPut, OpenCall, Roll, Close, None];

    public static string ToStored(DecisionKind kind) => kind switch
    {
        DecisionKind.OpenPut => OpenPut,
        DecisionKind.OpenCall => OpenCall,
        DecisionKind.Roll => Roll,
        DecisionKind.Close => Close,
        DecisionKind.None => None,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            $"'{kind}' is not a decision kind. This is most likely an uninitialised value: "
            + "the enumeration deliberately does not start at zero, and a maker that took no "
            + "candidate carries 'none' rather than nothing."),
    };

    public static DecisionKind ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        return stored switch
        {
            OpenPut => DecisionKind.OpenPut,
            OpenCall => DecisionKind.OpenCall,
            Roll => DecisionKind.Roll,
            Close => DecisionKind.Close,
            None => DecisionKind.None,
            _ => throw new FormatException(
                $"'{stored}' is not a stored decision kind. The permitted values are "
                + $"{string.Join(", ", All)}, and migration 9's CHECK carries the same list."),
        };
    }
}
