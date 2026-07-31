using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The worked example as an oracle: its parsed tables, its structural
/// constants, and its chain file, stated once for every fixture that compares
/// against the document.
/// </summary>
/// <remarks>
/// Extracted from FX-WorkedExampleChainLoads when 1.4's persistence fixture
/// became its second consumer. The parser was already shared
/// (<see cref="MarkdownTable"/>); what two fixtures would otherwise restate
/// are the header vocabularies and the structural constants, and two
/// statements of a vocabulary drift the same way two parsers would.
/// <para>
/// <b>Where the line is drawn.</b> Symbol, snapshot date, expiry and right are
/// constants here. They are structural and stated once in prose: if any
/// changed the example would be a different example and every fixture reading
/// it would fail anyway. The per-strike values are what a revision actually
/// moves, and they are the only thing a second copy could silently disagree
/// about. No prose is parsed.
/// </para>
/// </remarks>
internal static class WorkedExampleOracle
{
    public const string Symbol = "WDGT";
    public static readonly DateOnly SnapshotDate = new(2026, 3, 2);
    public static readonly DateOnly Expiry = new(2026, 4, 17);
    public const OptionRight Right = OptionRight.Put;

    public static SyntheticChain LoadChain() =>
        SyntheticChainReader.Read(File.ReadAllText(RepoRoot.WorkedExampleChainPath));

    /// <summary>§2's chain snapshot: strike, delta, bid, ask.</summary>
    /// <remarks>
    /// The fifth column, "Committed if 1 contract", is derived, being strike
    /// times the multiplier, so it is not an observation and is not compared.
    /// </remarks>
    public static IReadOnlyList<IReadOnlyList<string>> StrikeTable() =>
        MarkdownTable.Rows(
            Document(),
            "Strike", "Delta", "Bid", "Ask", "Committed if 1 contract");

    /// <summary>§5's underlying path: date, close.</summary>
    /// <remarks>The third column is commentary rather than an observation.</remarks>
    public static IReadOnlyList<IReadOnlyList<string>> BarTable() =>
        MarkdownTable.Rows(Document(), "Date", "Close", "Note");

    /// <summary>
    /// §3's gate table: strike, spread, bid, delta, committed, verdict.
    /// </summary>
    /// <remarks>
    /// <b>§3's claim rather than §2's data.</b> §2 states what the chain
    /// carried; §3 states what the generator does with it, opening "All seven
    /// strikes are enumerated". That sentence is prose and nothing here parses
    /// prose, so the claim is read as the rows of the table beneath it, which
    /// is the same seven.
    /// <para>
    /// The two tables are independently maintained halves of one document and
    /// nothing before 2.2 compared them. A revision that added a strike to §2
    /// and not to §3, or the reverse, was previously invisible.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<IReadOnlyList<string>> GateTable() =>
        MarkdownTable.Rows(
            Document(), "Strike", "Spread, % of mid", "Bid", "Delta", "Committed", "Gate");

    private static string Document() => File.ReadAllText(RepoRoot.WorkedExamplePath);
}
