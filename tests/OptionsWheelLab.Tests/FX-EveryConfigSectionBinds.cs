using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-EveryConfigSectionBinds: no configuration section binds to nothing.
/// </summary>
/// <remarks>
/// A sibling project shipped two configuration blocks that were never bound, so
/// editing them silently did nothing. A test asserting that known sections
/// populate would not have caught it, because the unbound blocks were not among
/// the known ones. So this enumerates what composition registered and what the
/// file contains, and compares the two sets in both directions.
/// </remarks>
public sealed class FX_EveryConfigSectionBinds
{
    /// <summary>
    /// The defect this checkpoint exists to prevent: a section sitting in
    /// appsettings.json that nothing reads.
    /// </summary>
    [Fact]
    public void Every_section_in_appsettings_binds_to_a_registered_options_type()
    {
        var configuration = Composition.Configuration();
        var services = Composition.Services(configuration);

        var sectionsInFile = Composition.SectionsInFile(configuration);
        var bound = Composition.BoundSections(services);

        // A parse that silently matched nothing would pass every assertion
        // below without testing anything.
        Assert.NotEmpty(sectionsInFile);

        var unbound = SectionBinding.Unbound(sectionsInFile, bound);

        Assert.True(
            unbound.Count == 0,
            $"These sections are present in {RepoRoot.AppSettingsPath} but bind to nothing: "
            + $"{string.Join(", ", unbound)}. Either bind the section to an options type at "
            + "composition, or remove it from the file.");
    }

    /// <summary>
    /// The same defect from the other side: a registration pointing at a
    /// section that is not there.
    /// </summary>
    [Fact]
    public void Every_bound_section_is_present_in_appsettings()
    {
        var configuration = Composition.Configuration();
        var services = Composition.Services(configuration);

        var sectionsInFile = Composition.SectionsInFile(configuration);
        var bound = Composition.BoundSections(services);

        Assert.NotEmpty(bound);

        var missing = bound
            .Where(section => !sectionsInFile.Contains(section.Path, StringComparer.OrdinalIgnoreCase))
            .Select(section => $"{section.Path} -> {section.OptionsType.Name}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"These sections are bound at composition but absent from {RepoRoot.AppSettingsPath}: "
            + string.Join(", ", missing));
    }

    /// <summary>
    /// Closes the bypass: someone calls services.Configure&lt;T&gt;() directly
    /// and the BoundSection registry never learns about it, so the two tests
    /// above would compare an incomplete set and pass.
    /// </summary>
    [Fact]
    public void Every_options_type_configured_in_composition_is_recorded_as_a_bound_section()
    {
        var configuration = Composition.Configuration();
        var services = Composition.Services(configuration);

        var recorded = Composition.BoundSections(services)
            .Select(section => section.OptionsType)
            .ToHashSet();

        var configuredHere = services
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(IConfigureOptions<>))
            .Select(type => type.GetGenericArguments()[0])
            .Where(IsOurs)
            .ToHashSet();

        // Without this the test would pass vacuously if Bind() ever stopped
        // registering IConfigureOptions<T>, which is the whole mechanism it
        // relies on.
        Assert.NotEmpty(configuredHere);

        var unrecorded = configuredHere.Except(recorded).Select(type => type.FullName).ToList();

        Assert.True(
            unrecorded.Count == 0,
            "These options types are configured at composition but were not registered through "
            + $"BindSection, so no BoundSection records them: {string.Join(", ", unrecorded)}.");
    }

    /// <summary>
    /// Permanent cover for the mechanism itself, so the definition of done is
    /// not resting only on a one-off manual demonstration.
    /// </summary>
    [Fact]
    public void A_stray_section_is_reported()
    {
        var bound = new[] { new BoundSection("Eodhd", typeof(EodhdOptions)) };
        var sectionsInFile = new[] { "Eodhd", "StraySection" };

        var unbound = SectionBinding.Unbound(sectionsInFile, bound);

        Assert.Equal(["StraySection"], unbound);
    }

    [Fact]
    public void A_file_whose_sections_all_bind_reports_nothing()
    {
        var bound = new[] { new BoundSection("Eodhd", typeof(EodhdOptions)) };
        var sectionsInFile = new[] { "Eodhd" };

        Assert.Empty(SectionBinding.Unbound(sectionsInFile, bound));
    }

    private static bool IsOurs(Type type) =>
        type.Assembly.GetName().Name?.StartsWith("OptionsWheelLab", StringComparison.Ordinal) == true;
}

internal static class SectionBinding
{
    /// <summary>
    /// Sections present in the file that no registration binds.
    /// </summary>
    internal static IReadOnlyList<string> Unbound(
        IEnumerable<string> sectionsInFile,
        IEnumerable<BoundSection> bound)
    {
        var boundPaths = bound
            .Select(section => section.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return sectionsInFile
            .Where(section => !boundPaths.Contains(section))
            .OrderBy(section => section, StringComparer.Ordinal)
            .ToList();
    }
}
