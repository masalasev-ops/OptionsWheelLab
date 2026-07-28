using System.Globalization;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The two renderings of one instant.
/// </summary>
/// <remarks>
/// Stored columns take <see cref="ToStored"/>, filenames take
/// <see cref="ToFileName"/>. The forms are never mixed and never converted into
/// one another: the same instant is rendered twice, at the point of use.
/// <para>
/// A filename cannot carry the stored form because <c>:</c> is illegal in a
/// Windows path, which would make the first snapshot fail to create its file.
/// </para>
/// </remarks>
public static class StoreTimestamp
{
    /// <summary>The form every column ending <c>_at</c> stores.</summary>
    public const string StoredFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    /// <summary>The same instant with the separators removed, for filenames.</summary>
    public const string FileNameFormat = "yyyyMMddTHHmmssfffZ";

    public static string ToStored(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString(StoredFormat, CultureInfo.InvariantCulture);

    public static string ToFileName(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString(FileNameFormat, CultureInfo.InvariantCulture);

    public static DateTimeOffset ParseStored(string stored) =>
        DateTimeOffset.ParseExact(
            stored,
            StoredFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    public static DateTimeOffset ParseFileName(string fileNamePart) =>
        DateTimeOffset.ParseExact(
            fileNamePart,
            FileNameFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
