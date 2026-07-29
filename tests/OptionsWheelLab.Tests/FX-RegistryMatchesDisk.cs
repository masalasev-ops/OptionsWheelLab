using System.Text.RegularExpressions;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-RegistryMatchesDisk: every artefact on disk has an entry in
/// <c>FIXTURES.md</c> and is named for it.
/// </summary>
/// <remarks>
/// Artefact to entry only. The other direction, that every registered entry has
/// its artefact, cannot be a standing assertion while most entries belong to
/// checkpoints not yet built, so it is a definition of done on each checkpoint
/// instead. This direction is safe from the first one onward, which is why the
/// fixture sits at 0.2 rather than 0.6.
/// <para>
/// <b>Two kinds of artefact, because there are two kinds of check.</b> A
/// <c>fixture</c> is an <c>FX-*.cs</c> file in this project; a <c>guard</c> is a
/// named check in <c>guards.ps1</c>, which must fail even when the build does
/// not. Registering only the first was the defect that let 0.4's floating-point
/// guard exist unregistered for a whole checkpoint.
/// </para>
/// </remarks>
public sealed class FX_RegistryMatchesDisk
{
    private const string FixtureFilePattern = "FX-*.cs";

    // A registry row opens with the name in the first cell and its Kind in the
    // second.
    private static readonly Regex RegistryRow = new(
        @"^\|\s*(FX-[A-Za-z0-9]+)\s*\|\s*(fixture|guard)\s*\|",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // A named check in the script. The script declares this exactly once per
    // check and nowhere else, which is what makes the name discoverable from
    // here without parsing PowerShell.
    private static readonly Regex GuardName = new(
        @"Name\s*=\s*'(FX-[A-Za-z0-9]+)'",
        RegexOptions.Compiled);

    [Fact]
    public void Every_fixture_file_on_disk_has_an_entry_in_the_registry()
    {
        var filesOnDisk = FixtureFileNames();
        var registered = RegisteredOfKind("fixture");

        // A scan that found no files would pass without testing anything.
        Assert.NotEmpty(filesOnDisk);
        Assert.NotEmpty(registered);

        var unregistered = Unregistered(filesOnDisk, registered);

        Assert.True(
            unregistered.Count == 0,
            $"These fixture files exist in {RepoRoot.TestProjectPath} but have no row of Kind "
            + $"fixture in {RepoRoot.FixturesRegistryPath}: "
            + string.Join(", ", unregistered.Select(name => $"{name}.cs"))
            + ". Every fixture appears in the registry exactly once, registered against one "
            + "checkpoint.");
    }

    /// <summary>
    /// The same direction for the other artefact. A check can be added to the
    /// script in one line, and the registry is where anything planning a
    /// checkpoint looks for what the build enforces.
    /// </summary>
    [Fact]
    public void Every_named_check_in_the_guard_script_has_an_entry_in_the_registry()
    {
        var inScript = GuardNames();
        var registered = RegisteredOfKind("guard");

        // Both ends asserted non-empty first. A regex that stopped matching the
        // script would otherwise report every guard as registered by finding
        // none to check, which is the shape of failure this repository has
        // already had once.
        Assert.NotEmpty(inScript);
        Assert.NotEmpty(registered);

        var unregistered = Unregistered(inScript, registered);

        Assert.True(
            unregistered.Count == 0,
            $"These checks are named in {RepoRoot.GuardScriptPath} but have no row of Kind guard "
            + $"in {RepoRoot.FixturesRegistryPath}: {string.Join(", ", unregistered)}. A check the "
            + "build enforces and the registry does not list is a check nothing planning a "
            + "checkpoint will find.");
    }

    /// <summary>
    /// The registry distinguishes the two kinds, so a row cannot claim to be
    /// both and cannot be neither.
    /// </summary>
    [Fact]
    public void Every_registry_row_declares_a_kind()
    {
        var registry = File.ReadAllText(RepoRoot.FixturesRegistryPath);

        var rowsOpeningWithAName = Regex.Matches(registry, @"^\|\s*(FX-[A-Za-z0-9]+)\s*\|", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToList();

        var rowsWithAKind = RegistryRow
            .Matches(registry)
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(rowsOpeningWithAName);

        var withoutKind = rowsOpeningWithAName
            .Except(rowsWithAKind, StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            withoutKind.Count == 0,
            $"These rows in {RepoRoot.FixturesRegistryPath} do not declare a Kind of fixture or "
            + $"guard: {string.Join(", ", withoutKind)}. Without one, neither enforcement "
            + "direction knows which artefact the entry points at.");
    }

    /// <summary>
    /// The comparison itself, on synthetic input, so it is known to be capable
    /// of failing rather than merely observed to pass against a tree that
    /// happens to be correct.
    /// </summary>
    [Fact]
    public void An_artefact_with_no_row_of_its_kind_is_reported()
    {
        var registry =
            "| FX-Alpha | fixture | 0.2 | asserts something | authored |\n"
            + "| FX-Beta | guard | 0.4 | asserts something else | authored |\n";

        var fixtures = RegisteredIn(registry, "fixture");
        var guards = RegisteredIn(registry, "guard");

        Assert.Equal(["FX-Alpha"], fixtures.Order(StringComparer.Ordinal));
        Assert.Equal(["FX-Beta"], guards.Order(StringComparer.Ordinal));

        // A guard registered as a fixture does not satisfy the guard direction,
        // and the other way round. Kind is what each direction filters on.
        Assert.Equal(["FX-Beta"], Unregistered(["FX-Alpha", "FX-Beta"], fixtures));
        Assert.Equal(["FX-Alpha"], Unregistered(["FX-Alpha", "FX-Beta"], guards));
        Assert.Empty(Unregistered(["FX-Alpha"], fixtures));
    }

    /// <summary>
    /// The script's names are found by the same regex the real check uses, so a
    /// change to how a check is declared fails here rather than silently
    /// reducing the scan to nothing.
    /// </summary>
    [Fact]
    public void A_named_check_is_found_in_script_text()
    {
        var script = "$checks = @(\n    @{\n        Name = 'FX-Gamma'\n        Subject = 'x'\n";

        Assert.Equal(["FX-Gamma"], NamesIn(script).Order(StringComparer.Ordinal));
    }

    // The entry-to-artefact direction is deliberately absent. It cannot be a
    // standing assertion while most registry entries belong to checkpoints not
    // yet built, so it is a definition of done on each checkpoint instead.

    private static IReadOnlyList<string> Unregistered(
        IEnumerable<string> artefacts,
        IReadOnlySet<string> registered) =>
        [.. artefacts
            .Where(name => !registered.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)];

    private static IReadOnlySet<string> FixtureFileNames() =>
        Directory
            .EnumerateFiles(RepoRoot.TestProjectPath, FixtureFilePattern, SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> GuardNames() =>
        NamesIn(File.ReadAllText(RepoRoot.GuardScriptPath));

    private static IReadOnlySet<string> NamesIn(string script) =>
        GuardName
            .Matches(script)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> RegisteredOfKind(string kind) =>
        RegisteredIn(File.ReadAllText(RepoRoot.FixturesRegistryPath), kind);

    private static IReadOnlySet<string> RegisteredIn(string registry, string kind) =>
        RegistryRow
            .Matches(registry)
            .Where(match => string.Equals(match.Groups[2].Value, kind, StringComparison.Ordinal))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
}
