namespace OptionsWheelLab.Tests;

/// <summary>
/// Reads a pipe-delimited table out of a corpus document.
/// </summary>
/// <remarks>
/// The same shape <c>ConfigReferenceParser</c> reads, and the third document to
/// become machine-checked rather than described, after the Store column and the
/// fixture registry.
/// <para>
/// <b>Tables only, and deliberately no prose.</b> A table is found by its own
/// header row, so renumbering a section or rewording a sentence around it
/// changes nothing here. A regex over "Puts expiring 2026-04-17" would break on a
/// rewording that changed no fact, which trades a real tripwire for a false one.
/// Values stated once in prose are restated as constants by the caller instead:
/// if one of those changed, the example would be a different example and every
/// fixture reading it would fail anyway.
/// </para>
/// </remarks>
internal static class MarkdownTable
{
    /// <summary>
    /// The body rows of the table whose header is exactly
    /// <paramref name="headers"/>, each row as its trimmed cells.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<string>> Rows(
        string markdown,
        params string[] headers)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(headers);

        var lines = markdown.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            if (!Cells(lines[index]).SequenceEqual(headers, StringComparer.Ordinal))
            {
                continue;
            }

            return Body(lines, index + 1);
        }

        return [];
    }

    private static IReadOnlyList<IReadOnlyList<string>> Body(string[] lines, int from)
    {
        var rows = new List<IReadOnlyList<string>>();

        for (var index = from; index < lines.Length; index++)
        {
            var cells = Cells(lines[index]);

            if (cells.Count == 0)
            {
                break;
            }

            // The separator under the header, which is dashes and colons only.
            if (cells.All(cell => cell.Length != 0 && cell.All(c => c is '-' or ':')))
            {
                continue;
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static IReadOnlyList<string> Cells(string line)
    {
        var trimmed = line.Trim();

        return trimmed.StartsWith('|')
            ? [.. trimmed.Trim('|').Split('|').Select(cell => cell.Trim())]
            : [];
    }
}
