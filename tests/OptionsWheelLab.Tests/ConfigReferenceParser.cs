using System.Text.RegularExpressions;

namespace OptionsWheelLab.Tests;

/// <summary>
/// A row carrying a key whose Store cell is neither <c>rows</c> nor <c>app</c>.
/// </summary>
internal sealed record UnclassifiedRow(string Key, string StoreCell);

/// <summary>A row whose key cell names more than one key.</summary>
internal sealed record SharedKeyRow(string KeyCell, IReadOnlyList<string> Tokens);

/// <summary>
/// Keys read from the document, rows that could not be classified, and rows
/// naming more than one key.
/// </summary>
internal sealed record ParseResult(
    IReadOnlyList<ConfigKeyClass> Keys,
    IReadOnlyList<UnclassifiedRow> Unclassified,
    IReadOnlyList<SharedKeyRow> SharedRows);

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

    // Backticked tokens in the Key cell. The document carries one key per row,
    // so a cell yielding more than one token is a defect rather than a form to
    // be interpreted: only the first would ever be read, leaving the rest
    // undocumented as far as any check could tell.
    private static readonly Regex BacktickedToken = new(@"`([^`]+)`", RegexOptions.Compiled);

    internal static ParseResult Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var keys = new List<ConfigKeyClass>();
        var unclassified = new List<UnclassifiedRow>();
        var sharedRows = new List<SharedKeyRow>();

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

            var tokens = BacktickedToken.Matches(cells[0])
                .Select(token => token.Groups[1].Value.Trim())
                .ToList();

            // A row with no backticked key in its first cell is a header or a
            // separator, not a key row.
            if (tokens.Count == 0)
            {
                continue;
            }

            if (tokens.Count > 1)
            {
                sharedRows.Add(new SharedKeyRow(cells[0].Trim(), tokens));
            }

            var key = tokens[0];
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

        return new ParseResult(keys, unclassified, sharedRows);
    }

    internal static IReadOnlySet<string> SectionRoots(
        IEnumerable<ConfigKeyClass> keys,
        string storeClass) =>
        keys.Where(key => key.Store == storeClass)
            .Select(key => key.SectionRoot)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
