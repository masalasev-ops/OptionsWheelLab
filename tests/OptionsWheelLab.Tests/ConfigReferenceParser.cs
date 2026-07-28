using System.Text.RegularExpressions;

namespace OptionsWheelLab.Tests;

/// <summary>
/// A row carrying a key whose Store cell is neither <c>rows</c> nor <c>app</c>.
/// </summary>
internal sealed record UnclassifiedRow(string Key, string StoreCell);

/// <summary>Keys read from the document, and rows that could not be classified.</summary>
internal sealed record ParseResult(
    IReadOnlyList<ConfigKeyClass> Keys,
    IReadOnlyList<UnclassifiedRow> Unclassified);

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

    internal static ParseResult Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var keys = new List<ConfigKeyClass>();
        var unclassified = new List<UnclassifiedRow>();

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

            var match = FirstBacktickedToken.Match(cells[0]);

            // A row with no backticked key in its first cell is a header or a
            // separator, not a key row.
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups[1].Value.Trim();
            var store = cells[1].Trim();

            if (store == RowsClass || store == AppClass)
            {
                keys.Add(new ConfigKeyClass(key, store));
                continue;
            }

            // A key row whose Store cell is neither class is recorded rather
            // than skipped. Skipping it would drop the key out of the contract
            // silently, and a malformed cell such as **rows** would then read
            // as no key at all. The non-empty guard catches a total parse
            // failure; it cannot catch a partial one.
            unclassified.Add(new UnclassifiedRow(key, store));
        }

        return new ParseResult(keys, unclassified);
    }

    internal static IReadOnlySet<string> SectionRoots(
        IEnumerable<ConfigKeyClass> keys,
        string storeClass) =>
        keys.Where(key => key.Store == storeClass)
            .Select(key => key.SectionRoot)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
