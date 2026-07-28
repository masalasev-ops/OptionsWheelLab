using System.Globalization;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// Turns a stored configuration value into the type it stands for.
/// </summary>
/// <remarks>
/// Internal, and shared by both configuration surfaces, so there is one place a
/// stored value is interpreted rather than one per surface.
/// <para>
/// <b>Not because of ambient culture.</b> <c>InvariantGlobalization</c> is on
/// repository-wide, so <c>CurrentCulture</c> is already invariant and the
/// classic silently-wrong-separator trap is closed without this. The reason the
/// decimal accessor exists is that it is where the canonical form is VALIDATED
/// rather than assumed, and where changing <see cref="StoreDecimal.Scale"/>
/// stays one edit instead of a search.
/// </para>
/// <para>
/// <b>The int case is weaker, and it is here for a different reason.</b>
/// <c>Costs</c>, <c>Risk</c>, <c>Gate</c> and <c>Scoring</c> are decimals;
/// <c>Trial</c> and the expiry bounds are integers. Between them that is
/// essentially all of the config store. A surface that types one and hands back
/// strings for the other has a mixed shape, and a mixed shape teaches callers to
/// parse at the call site, which is the habit a typed surface exists to prevent.
/// </para>
/// <para>
/// <b>An integer is not stored in the canonical decimal form.</b>
/// <c>Trial:MaxRolls</c> is <c>7</c>, not <c>7.00000000</c>. That is the obvious
/// wrong inference from the two accessors sitting side by side, so it is stated
/// rather than left to be discovered.
/// </para>
/// </remarks>
internal static class ConfigValue
{
    /// <summary>
    /// The decimal a stored value carries, or null when the key had no value.
    /// </summary>
    /// <remarks>
    /// Parsing is <see cref="StoreDecimal.ParseStored"/> and is not
    /// reimplemented here, so a value that is not in the stored form is refused
    /// at the point of reading rather than flowing on as a plausible number.
    /// </remarks>
    internal static decimal? AsDecimal(string? stored, string key) =>
        stored is null ? null : Interpreted(() => StoreDecimal.ParseStored(stored), stored, key, "a decimal");

    /// <summary>
    /// The integer a stored value carries, or null when the key had no value.
    /// </summary>
    internal static int? AsInt(string? stored, string key) =>
        stored is null
            ? null
            : Interpreted(
                () => int.Parse(stored, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
                stored,
                key,
                "an integer");

    /// <summary>
    /// Runs a parse and names the key when it fails.
    /// </summary>
    /// <remarks>
    /// The parser's own message says what was wrong with the text and cannot say
    /// which key carried it, and a configuration failure that does not name its
    /// key is a search rather than a diagnosis.
    /// </remarks>
    private static T Interpreted<T>(Func<T> parse, string stored, string key, string expected)
    {
        try
        {
            return parse();
        }
        catch (Exception failure) when (failure is FormatException or OverflowException)
        {
            throw new FormatException(
                $"Configuration key '{key}' holds '{stored}', which is not {expected} in the "
                + "stored form.",
                failure);
        }
    }
}
