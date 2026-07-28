namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ConfigStoreClassHonoured: parses the Store column from
/// <c>CONFIG_REFERENCE.md</c> and asserts no appsettings section has a root
/// classed <c>rows</c>.
/// </summary>
/// <remarks>
/// A value read while producing or scoring a simulated decision is a config row
/// resolved as-of, and everything else is bound from appsettings [D-W27]. A
/// registered options class is itself a current-value accessor whether or not
/// anything consumes it, so binding a rows-classed section, even as a
/// placeholder, would create the second path to those values that as-of
/// resolution exists to prevent [D-W26].
/// <para>
/// This and FX-EveryConfigSectionBinds close the loop from opposite directions
/// and neither closes it alone. This one catches a rows-classed section that
/// gains a binding; the other catches a section that binds to nothing.
/// </para>
/// </remarks>
public sealed class FX_ConfigStoreClassHonoured
{
    [Fact]
    public void No_appsettings_section_has_a_root_classed_rows()
    {
        var keys = ParsedKeys();
        var rowsRoots = ConfigReferenceParser.SectionRoots(keys, ConfigReferenceParser.RowsClass);

        var configuration = Composition.Configuration();
        var offending = Composition.SectionsInFile(configuration)
            .Where(rowsRoots.Contains)
            .OrderBy(section => section, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offending.Count == 0,
            $"These sections are present in {RepoRoot.AppSettingsPath} but CONFIG_REFERENCE.md "
            + $"classes their keys as rows: {string.Join(", ", offending)}. A rows-classed value "
            + "is resolved as-of from the config store and is never bound from appsettings.");
    }

    [Fact]
    public void No_bound_options_type_targets_a_section_classed_rows()
    {
        var keys = ParsedKeys();
        var rowsRoots = ConfigReferenceParser.SectionRoots(keys, ConfigReferenceParser.RowsClass);

        var configuration = Composition.Configuration();
        var services = Composition.Services(configuration);

        var offending = Composition.BoundSections(services)
            .Where(section => rowsRoots.Contains(section.Path.Split(':')[0]))
            .Select(section => $"{section.Path} -> {section.OptionsType.Name}")
            .ToList();

        Assert.True(
            offending.Count == 0,
            "These options types are bound from appsettings but CONFIG_REFERENCE.md classes "
            + $"their section as rows: {string.Join(", ", offending)}.");
    }

    [Fact]
    public void The_two_storage_classes_do_not_overlap()
    {
        var keys = ParsedKeys();

        var rowsRoots = ConfigReferenceParser.SectionRoots(keys, ConfigReferenceParser.RowsClass);
        var appRoots = ConfigReferenceParser.SectionRoots(keys, ConfigReferenceParser.AppClass);

        var overlap = rowsRoots.Intersect(appRoots, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.True(
            overlap.Count == 0,
            "CONFIG_REFERENCE.md classes these section roots as both rows and app, so the "
            + $"document contradicts itself: {string.Join(", ", overlap)}.");
    }

    /// <summary>
    /// A parser that silently matched nothing would pass every assertion above.
    /// That failure has already happened once in this corpus, where an edit
    /// matched nothing, no-opped, and was recorded as done.
    /// </summary>
    [Fact]
    public void The_store_column_parses_to_keys_in_both_classes()
    {
        var keys = ParsedKeys();

        Assert.NotEmpty(keys);
        Assert.NotEmpty(ConfigReferenceParser.SectionRoots(keys, ConfigReferenceParser.RowsClass));
        Assert.NotEmpty(ConfigReferenceParser.SectionRoots(keys, ConfigReferenceParser.AppClass));
    }

    /// <summary>
    /// Every key row must classify. A Store cell that is neither class, such as
    /// a bolded <c>**rows**</c>, would otherwise drop that key out of the
    /// contract without any test noticing.
    /// </summary>
    [Fact]
    public void Every_key_row_carries_a_recognised_store_class()
    {
        var unclassified = Parsed().Unclassified;

        Assert.True(
            unclassified.Count == 0,
            $"These rows in {RepoRoot.ConfigReferencePath} carry a key but their Store cell is "
            + $"neither '{ConfigReferenceParser.RowsClass}' nor '{ConfigReferenceParser.AppClass}': "
            + string.Join(
                ", ",
                unclassified.Select(row => $"{row.Key} has Store cell '{row.StoreCell}'"))
            + ". An unclassified key is silently outside the storage-class contract.");
    }

    [Fact]
    public void An_unclassified_store_cell_is_reported_with_its_key_and_cell()
    {
        const string Markdown = """
            | `Gate:MaxDelta` | **rows** | reject above this delta | Risk gate | |
            | `Eodhd:BaseUrl` | app | API root | Ingest | |
            """;

        var result = ConfigReferenceParser.Parse(Markdown);

        Assert.Single(result.Keys);
        var offender = Assert.Single(result.Unclassified);
        Assert.Equal("Gate:MaxDelta", offender.Key);
        Assert.Equal("**rows**", offender.StoreCell);
    }

    [Fact]
    public void A_header_row_is_not_reported_as_unclassified()
    {
        const string Markdown = """
            | Key | Store | Meaning | Consumer | Notes |
            |---|---|---|---|---|
            | `Eodhd:BaseUrl` | app | API root | Ingest | |
            """;

        var result = ConfigReferenceParser.Parse(Markdown);

        Assert.Single(result.Keys);
        Assert.Empty(result.Unclassified);
    }

    [Fact]
    public void A_rows_classed_section_appearing_in_appsettings_is_reported()
    {
        const string Markdown = """
            | Key | Store | Meaning | Consumer | Notes |
            |---|---|---|---|---|
            | `Gate:MaxDelta` | rows | reject above this delta | Risk gate | |
            | `Eodhd:BaseUrl` | app | API root | Ingest | |
            """;

        var keys = ConfigReferenceParser.Parse(Markdown).Keys;
        var rowsRoots = ConfigReferenceParser.SectionRoots(keys, ConfigReferenceParser.RowsClass);

        Assert.Equal(2, keys.Count);
        Assert.Contains("Gate", rowsRoots);
        Assert.DoesNotContain("Eodhd", rowsRoots);
    }

    /// <summary>
    /// One key per row. Only the first backticked token in a key cell is ever
    /// read, so a row naming two keys leaves the second undocumented as far as
    /// any check can tell.
    /// </summary>
    /// <remarks>
    /// This replaces a test that asserted a shared row yields its section root.
    /// That test encoded the form the document now forbids, so it would have
    /// kept passing while documenting the wrong contract.
    /// </remarks>
    [Fact]
    public void Every_key_row_names_exactly_one_key()
    {
        var shared = Parsed().SharedRows;

        Assert.True(
            shared.Count == 0,
            $"These rows in {RepoRoot.ConfigReferencePath} name more than one key, and only the "
            + "first is ever read: "
            + string.Join(
                "; ",
                shared.Select(row => $"'{row.KeyCell}' names {string.Join(", ", row.Tokens)}"))
            + ". Split the row so every row names one key, and state any constraint between them "
            + "in their Notes.");
    }

    [Fact]
    public void A_row_naming_two_keys_is_reported_with_every_token_found()
    {
        const string Markdown = """
            | `Gate:MinDte` / `Gate:MaxDte` | rows | admissible expiry window | Risk gate | |
            | `Eodhd:BaseUrl` | app | API root | Ingest | |
            """;

        var result = ConfigReferenceParser.Parse(Markdown);

        var offender = Assert.Single(result.SharedRows);
        Assert.Equal("`Gate:MinDte` / `Gate:MaxDte`", offender.KeyCell);
        Assert.Equal(["Gate:MinDte", "Gate:MaxDte"], offender.Tokens);
    }

    /// <summary>
    /// A suffix-only second token is the worse form: it is not a key path at
    /// all, so nothing could resolve it even if the parser read it.
    /// </summary>
    [Fact]
    public void A_row_naming_a_key_and_a_bare_suffix_is_reported()
    {
        const string Markdown = """
            | `Policy:Baseline:DeltaMin` / `DeltaMax` | rows | delta band | Frozen baseline maker | |
            """;

        var offender = Assert.Single(ConfigReferenceParser.Parse(Markdown).SharedRows);

        Assert.Equal(["Policy:Baseline:DeltaMin", "DeltaMax"], offender.Tokens);
    }

    [Fact]
    public void A_row_naming_one_key_is_not_reported()
    {
        const string Markdown = """
            | `Gate:MaxDte` | rows | latest admissible expiry | Risk gate | Must be less than `Trial:MaxTrialDays` |
            """;

        var result = ConfigReferenceParser.Parse(Markdown);

        // A backticked token in the Notes cell is not a second key.
        Assert.Empty(result.SharedRows);
        Assert.Equal("Gate:MaxDte", Assert.Single(result.Keys).Key);
    }

    private static ParseResult Parsed() =>
        ConfigReferenceParser.Parse(File.ReadAllText(RepoRoot.ConfigReferencePath));

    private static IReadOnlyList<ConfigKeyClass> ParsedKeys() => Parsed().Keys;
}
