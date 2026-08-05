using System.Text.RegularExpressions;
using OptionsWheelLab.Core.Decisions;
using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-RecordCarriesFeasibleSet: a decision is re-scorable from its record alone
/// [D-W3].
/// </summary>
/// <remarks>
/// <b>The one unrecoverable loss in the design.</b> A decision that cannot be
/// re-scored from what stood at the time cannot contribute to the measurement
/// this lab exists to make, and unlike every other kind of missing data it cannot
/// be re-fetched later, because what was true then is gone.
/// <para>
/// <b>The definition of done is about what the read does not touch.</b> Asserting
/// that a record rebuilds is easy and nearly vacuous: a reader free to consult
/// live quotes and current configuration would rebuild something on any schema at
/// all. What makes the claim mean anything is the barred set, so the scan below
/// names it rather than only listing what is permitted.
/// </para>
/// </remarks>
public sealed class FX_RecordCarriesFeasibleSet
{
    /// <summary>
    /// The record's own tables, plus the one append-only table it references.
    /// </summary>
    /// <remarks>
    /// <c>contracts</c> is inside the record because <c>candidates.contract_id</c>
    /// names it and a corporate action mints a new identity rather than editing a
    /// row [D-W36], so reading it later returns what stood then.
    /// </remarks>
    private static readonly string[] Permitted =
    [
        "feasible_sets",
        "candidates",
        "candidate_gate_reasons",
        "decisions",
        "decision_gate_reasons",
        "contracts",
    ];

    /// <summary>
    /// The tables a re-score must not reach, and the reason each is barred.
    /// </summary>
    /// <remarks>
    /// The first two are live state. The second two are projections [D-W35], and
    /// the distinction that puts <c>contracts</c> inside the record and these
    /// outside it is rewritability rather than convenience: a projection read
    /// later returns whatever it was last rebuilt to.
    /// </remarks>
    private static readonly (string Table, string Why)[] Barred =
    [
        ("contract_quotes", "the live market, which is what feature_json exists to avoid re-reading"),
        ("config_rows", "current configuration, which a re-score must not resolve as-now [D-W26]"),
        ("trials", "a projection, rewritable and so not what stood then [D-W35]"),
        ("positions", "a projection, rewritable and so not what stood then [D-W35]"),
    ];

    [Fact]
    public void The_re_scoring_path_reads_no_table_the_record_does_not_name()
    {
        var tables = TablesTheReaderNames();

        // The vacuity guard. A reader whose SQL failed to extract would name no
        // table and satisfy a subset assertion while proving nothing.
        Assert.NotEmpty(tables);
        Assert.Contains("decisions", tables);

        var outside = tables.Where(table => !Permitted.Contains(table)).Order(StringComparer.Ordinal).ToList();

        Assert.True(
            outside.Count == 0,
            $"{nameof(DecisionRecordReader)} reads these tables, which the record does not name: "
            + $"{string.Join(", ", outside)}. A decision is re-scorable from the record alone "
            + "[D-W3], so a read outside it makes the claim false without making any test fail.");
    }

    /// <summary>
    /// Each barred table by name, so the rule states what it excludes.
    /// </summary>
    /// <remarks>
    /// Separate from the subset case above, and not redundant with it. That one
    /// fails if the reader grows a table; this one says which growths were
    /// considered and refused, so a later reader adding
    /// <c>contract_quotes</c> meets the reason rather than a list.
    /// </remarks>
    [Theory]
    [InlineData("contract_quotes")]
    [InlineData("config_rows")]
    [InlineData("trials")]
    [InlineData("positions")]
    public void A_barred_table_is_not_read(string table)
    {
        Assert.DoesNotContain(table, TablesTheReaderNames());

        // The reason travels with the bar rather than living only here.
        Assert.Contains(Barred, entry => entry.Table == table && entry.Why.Length > 0);
    }

    /// <summary>
    /// The detector fires, shown by asking it about the writer, which touches the
    /// same tables and is not the subject.
    /// </summary>
    /// <remarks>
    /// An assertion nobody has seen fail is an assertion nobody has read. The
    /// writer names every record table too, so this proves the extraction reads a
    /// file rather than returning the permitted list back.
    /// </remarks>
    [Fact]
    public void The_scan_reads_the_file_rather_than_the_list()
    {
        var writer = TablesNamedIn("DecisionStore.cs");

        Assert.Contains("feasible_sets", writer);
        Assert.Contains("contracts", writer);
    }

    /// <summary>
    /// The whole set comes back, which is what makes the scan above worth having.
    /// </summary>
    /// <remarks>
    /// Without this a reader that queried nothing would pass every case above.
    /// </remarks>
    [Fact]
    public void A_recorded_decision_rebuilds_to_what_the_maker_was_offered()
    {
        using var scenario = new DecisionScenario(
        [
            GateScenario.Quote(45.00m, bid: 0.20m, ask: 0.30m),
            GateScenario.Quote(47.50m),
            GateScenario.Quote(50.00m),
        ]);

        var offered = scenario.Gated();
        var decisionId = scenario.Record("baseline", offered);

        var record = scenario.Reader.Read(decisionId);

        Assert.Equal("baseline", record.MakerId);
        Assert.Equal(DecisionKind.None, record.Kind);
        Assert.Null(record.Chosen);
        Assert.Equal(offered.Count, record.FeasibleSet.Count);

        // Every candidate the maker was offered, with the same identity and the
        // same verdict, which is the re-scoring input.
        Assert.Equal(
            offered.Select(candidate => candidate.Candidate.Quote.Contract).ToList(),
            record.FeasibleSet.Select(candidate => candidate.Contract).ToList());

        Assert.Equal(
            offered.Select(candidate => candidate.Reasons.Order().ToList()).ToList(),
            record.FeasibleSet.Select(candidate => candidate.Reasons.Order().ToList()).ToList());

        // The rejected are recorded too, so the gate's effect is auditable
        // [D-W10] rather than inferred from what survived.
        Assert.Contains(record.FeasibleSet, candidate => !candidate.IsFeasible);
        Assert.Contains(record.FeasibleSet, candidate => candidate.IsFeasible);
    }

    /// <summary>
    /// The quote the gate read survives in the record, which is why the live
    /// market is barred rather than merely unnecessary.
    /// </summary>
    [Fact]
    public void The_record_carries_the_quote_the_gate_read()
    {
        using var scenario = new DecisionScenario([GateScenario.Quote(50.00m)]);

        var offered = scenario.Gated();
        var record = scenario.Reader.Read(scenario.Record("baseline", offered));

        var candidate = Assert.Single(record.FeasibleSet);

        Assert.Equal(0.95m, candidate.Bid);
        Assert.Equal(1.01m, candidate.Ask);
        Assert.Contains("\"delta\"", candidate.FeatureJson, StringComparison.Ordinal);
        Assert.Contains("\"dte\":46", candidate.FeatureJson, StringComparison.Ordinal);

        // Money is a column and never a field inside the blob [D-W29].
        Assert.DoesNotContain("bid", candidate.FeatureJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ask", candidate.FeatureJson, StringComparison.Ordinal);
    }

    private static IReadOnlySet<string> TablesTheReaderNames() =>
        TablesNamedIn("DecisionRecordReader.cs");

    /// <summary>
    /// Every table the file's SQL names, after <c>FROM</c>, <c>JOIN</c> or
    /// <c>INTO</c>.
    /// </summary>
    private static IReadOnlySet<string> TablesNamedIn(string fileName)
    {
        var path = Directory
            .EnumerateFiles(RepoRoot.SourcePath, fileName, SearchOption.AllDirectories)
            .Single();

        var named = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sql in DecimalOrderingInSql.SqlIn(File.ReadAllText(path)))
        {
            foreach (Match match in Regex.Matches(
                DecimalOrderingInSql.WithoutCommentsOrLiterals(sql),
                @"\b(?:FROM|JOIN|INTO|UPDATE)\s+([a-z_][a-z0-9_]*)",
                RegexOptions.IgnoreCase))
            {
                named.Add(match.Groups[1].Value);
            }
        }

        return named;
    }
}
