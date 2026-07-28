using System.Globalization;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The stored form of a date.
/// </summary>
/// <remarks>
/// Session dates, expiries, ex-dates and report dates. Fixed width and UTC, for
/// the reason the Time section of <c>DATA_AND_SCHEMA.md</c> gives: every as-of
/// read is a string comparison, and a variable-width value misorders silently
/// rather than failing.
/// <para>
/// <b>The obvious alternative is culture-independent and still wrong.</b>
/// <c>InvariantGlobalization</c> is on repository-wide, so a bare
/// <c>ToString()</c> on a date cannot vary by machine, which makes it look
/// correct. It produces <c>MM/dd/yyyy</c>, which is not the stored form and
/// sorts by month. A culture test would never catch it, so the coupling is
/// asserted by a test instead.
/// </para>
/// </remarks>
public static class StoreDate
{
    /// <summary>The form every date column stores.</summary>
    public const string StoredFormat = "yyyy-MM-dd";

    public static string ToStored(DateOnly date) =>
        date.ToString(StoredFormat, CultureInfo.InvariantCulture);

    public static DateOnly ParseStored(string stored) =>
        DateOnly.ParseExact(stored, StoredFormat, CultureInfo.InvariantCulture);
}
