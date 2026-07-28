namespace OptionsWheelLab.Tests;

/// <summary>
/// Locates repository files that tests assert against.
/// </summary>
/// <remarks>
/// Tests here read the committed source files rather than the copies in the
/// output directory, so that adding a stray section to <c>src/appsettings.json</c>
/// is observed directly and cannot be masked by a stale copy.
/// </remarks>
internal static class RepoRoot
{
    /// <summary>
    /// Both solution filenames, so converting between the two formats does not
    /// break test discovery at runtime rather than at build.
    /// </summary>
    private static readonly string[] SolutionFileNames =
        ["OptionsWheelLab.sln", "OptionsWheelLab.slnx"];

    internal static string Location { get; } = Find();

    internal static string AppSettingsPath =>
        Path.Combine(Location, "src", "appsettings.json");

    internal static string SecretsExamplePath =>
        Path.Combine(Location, "src", "appsettings.Secrets.example.json");

    internal static string ConfigReferencePath =>
        Path.Combine(Location, "docs", "CONFIG_REFERENCE.md");

    internal static string FixturesRegistryPath =>
        Path.Combine(Location, "docs", "FIXTURES.md");

    internal static string TestProjectPath =>
        Path.Combine(Location, "tests", "OptionsWheelLab.Tests");

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (SolutionFileNames.Any(name => File.Exists(Path.Combine(directory.FullName, name))))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find any of {string.Join(" or ", SolutionFileNames)} in any directory "
            + $"above {AppContext.BaseDirectory}.");
    }
}
