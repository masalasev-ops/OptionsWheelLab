using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-EveryBoundKeyIsDocumented: every settable key on a bound options type has
/// a row in <c>CONFIG_REFERENCE.md</c>.
/// </summary>
/// <remarks>
/// The original defect inverted. 0.2 was built to catch a configuration section
/// nobody reads; this catches a configuration key nobody wrote down. A key the
/// code can read but the reference does not list is invisible to every audit
/// that reads the reference, which is how two threshold literals stayed hidden
/// in the sibling project.
/// </remarks>
public sealed class FX_EveryBoundKeyIsDocumented
{
    [Fact]
    public void Every_settable_key_on_a_bound_options_type_has_a_row_in_the_reference()
    {
        var bound = Composition.BoundSections(Composition.Services(Composition.Configuration()));
        var keys = OptionsKeyWalker.KeysOf(bound);

        var documented = DocumentedKeys();

        var undocumented = keys
            .Where(key => !documented.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            undocumented.Count == 0,
            $"These keys are readable from a bound options type but have no row in "
            + $"{RepoRoot.ConfigReferencePath}: {string.Join(", ", undocumented)}. A key the code "
            + "can read and the reference does not list is invisible to any audit of the "
            + "reference.");
    }

    /// <summary>
    /// A walk that returned nothing would pass the assertion above without
    /// testing anything.
    /// </summary>
    [Fact]
    public void The_walk_finds_keys_on_every_bound_section()
    {
        var bound = Composition.BoundSections(Composition.Services(Composition.Configuration()));

        Assert.NotEmpty(bound);
        Assert.NotEmpty(OptionsKeyWalker.KeysOf(bound));
        Assert.NotEmpty(DocumentedKeys());

        foreach (var section in bound)
        {
            Assert.True(
                OptionsKeyWalker.KeysOf(section).Count > 0,
                $"{section.OptionsType.Name} is bound to '{section.Path}' but exposes no settable "
                + "property, so it can never carry a value.");
        }
    }

    [Fact]
    public void A_key_path_is_the_section_path_and_the_property_name()
    {
        var keys = OptionsKeyWalker.KeysOf(new BoundSection("Eodhd", typeof(EodhdOptions)));

        Assert.Equal(["Eodhd:ApiKey", "Eodhd:BaseUrl", "Eodhd:OptionsAddOnEnabled"], keys);
    }

    [Fact]
    public void A_nested_options_class_contributes_its_own_segment()
    {
        var keys = OptionsKeyWalker.KeysOf(new BoundSection("Policy", typeof(NestedProbe)));

        Assert.Equal(["Policy:Band:DeltaMax", "Policy:Band:DeltaMin", "Policy:Name"], keys);
    }

    [Fact]
    public void A_read_only_property_contributes_no_key()
    {
        var keys = OptionsKeyWalker.KeysOf(new BoundSection("Probe", typeof(ReadOnlyProbe)));

        Assert.Equal(["Probe:Settable"], keys);
    }

    private static IReadOnlySet<string> DocumentedKeys()
    {
        var markdown = File.ReadAllText(RepoRoot.ConfigReferencePath);

        return ConfigReferenceParser.Parse(markdown).Keys
            .Select(key => key.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class NestedProbe
    {
        public string? Name { get; set; }

        public NestedBand? Band { get; set; }
    }

    private sealed class NestedBand
    {
        public decimal DeltaMin { get; set; }

        public decimal DeltaMax { get; set; }
    }

    private sealed class ReadOnlyProbe
    {
        public string? Settable { get; set; }

        public string Computed => "not a configuration key";
    }
}
