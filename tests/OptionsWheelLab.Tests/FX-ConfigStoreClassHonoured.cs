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

    [Fact]
    public void A_rows_classed_section_appearing_in_appsettings_is_reported()
    {
        const string Markdown = """
            | Key | Store | Meaning | Consumer | Notes |
            |---|---|---|---|---|
            | `Gate:MaxDelta` | rows | reject above this delta | Risk gate | |
            | `Eodhd:BaseUrl` | app | API root | Ingest | |
            """;

        var keys = ConfigReferenceParser.Parse(Markdown);
        var rowsRoots = ConfigReferenceParser.SectionRoots(keys, ConfigReferenceParser.RowsClass);

        Assert.Equal(2, keys.Count);
        Assert.Contains("Gate", rowsRoots);
        Assert.DoesNotContain("Eodhd", rowsRoots);
    }

    /// <summary>
    /// A row naming two keys must still yield its section root, or a whole
    /// section could slip past unclassified.
    /// </summary>
    [Fact]
    public void A_row_carrying_two_keys_yields_its_section_root()
    {
        const string Markdown = """
            | `Gate:MinDte` / `Gate:MaxDte` | rows | admissible expiry window | Risk gate | |
            """;

        var keys = ConfigReferenceParser.Parse(Markdown);

        Assert.Single(keys);
        Assert.Equal("Gate:MinDte", keys[0].Key);
        Assert.Equal("Gate", keys[0].SectionRoot);
    }

    private static IReadOnlyList<ConfigKeyClass> ParsedKeys() =>
        ConfigReferenceParser.Parse(File.ReadAllText(RepoRoot.ConfigReferencePath));
}
