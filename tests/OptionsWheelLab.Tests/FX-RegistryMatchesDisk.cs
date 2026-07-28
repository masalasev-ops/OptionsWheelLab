using System.Text.RegularExpressions;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-RegistryMatchesDisk: every fixture file on disk has an entry in
/// <c>FIXTURES.md</c> and is named for it.
/// </summary>
/// <remarks>
/// File to entry only. The other direction, that every registered entry has a
/// file, cannot be a standing assertion while most entries belong to
/// checkpoints not yet built, so it is a definition of done on each checkpoint
/// instead. This direction is safe from the first fixture onward, which is why
/// the fixture sits at 0.2 rather than 0.6.
/// </remarks>
public sealed class FX_RegistryMatchesDisk
{
    private const string FixtureFilePattern = "FX-*.cs";

    // A registry row opens with the fixture name in the first cell.
    private static readonly Regex RegistryRow =
        new(@"^\|\s*(FX-[A-Za-z0-9]+)\s*\|", RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void Every_fixture_file_on_disk_has_an_entry_in_the_registry()
    {
        var filesOnDisk = FixtureFileNames();
        var registered = RegisteredFixtures();

        // A scan that found no files would pass without testing anything.
        Assert.NotEmpty(filesOnDisk);
        Assert.NotEmpty(registered);

        var unregistered = filesOnDisk
            .Where(name => !registered.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unregistered.Count == 0,
            $"These fixture files exist in {RepoRoot.TestProjectPath} but have no row in "
            + $"{RepoRoot.FixturesRegistryPath}: "
            + string.Join(", ", unregistered.Select(name => $"{name}.cs"))
            + ". Every fixture appears in the registry exactly once, registered against one "
            + "checkpoint.");
    }

    // The entry-to-file direction is deliberately absent. It cannot be a
    // standing assertion while most registry entries belong to checkpoints not
    // yet built, so it is a definition of done on each checkpoint instead.

    private static IReadOnlySet<string> FixtureFileNames() =>
        Directory
            .EnumerateFiles(RepoRoot.TestProjectPath, FixtureFilePattern, SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> RegisteredFixtures() =>
        RegistryRow
            .Matches(File.ReadAllText(RepoRoot.FixturesRegistryPath))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
}
