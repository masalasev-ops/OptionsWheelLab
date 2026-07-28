using System.Text.RegularExpressions;

namespace OptionsWheelLab.Tests;

/// <summary>A key's storage class as <c>CONFIG_REFERENCE.md</c> declares it.</summary>
internal sealed record ConfigKeyClass(string Key, string Store)
{
    /// <summary>
    /// The appsettings section a key would live under, being the part before
    /// the first colon.
    /// </summary>
    internal string SectionRoot => Key.Split(':')[0];
}

/// <summary>
/// Reads the Store column out of <c>CONFIG_REFERENCE.md</c>.
/// </summary>
/// <remarks>
/// Parsing the document rather than restating its contents here is what makes
/// the Store column a checked contract instead of prose. A hand-written list
/// would drift from the document and the drift would be invisible.
/// </remarks>
internal static class ConfigReferenceParser
{
    internal const string RowsClass = "rows";
    internal const string AppClass = "app";

    // The first backticked token in the Key cell. Rows carrying two keys, such
    // as `Gate:MinDte` / `Gate:MaxDte`, yield the first, which is a full key
    // and so carries the same section root as its partner.
    private static readonly Regex FirstBacktickedToken = new(@"`([^`]+)`", RegexOptions.Compiled);

    internal static IReadOnlyList<ConfigKeyClass> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var keys = new List<ConfigKeyClass>();

        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.Trim();

            if (!trimmed.StartsWith('|'))
            {
                continue;
            }

            var cells = trimmed.Trim('|').Split('|');

            if (cells.Length < 2)
            {
                continue;
            }

            var store = cells[1].Trim();

            // Skips header and separator rows without needing to recognise them:
            // only rows whose second cell is a storage class are of interest.
            if (store != RowsClass && store != AppClass)
            {
                continue;
            }

            var match = FirstBacktickedToken.Match(cells[0]);

            if (!match.Success)
            {
                continue;
            }

            keys.Add(new ConfigKeyClass(match.Groups[1].Value.Trim(), store));
        }

        return keys;
    }

    internal static IReadOnlySet<string> SectionRoots(
        IEnumerable<ConfigKeyClass> keys,
        string storeClass) =>
        keys.Where(key => key.Store == storeClass)
            .Select(key => key.SectionRoot)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
