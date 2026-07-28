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
    private const string SolutionFileName = "OptionsWheelLab.slnx";

    internal static string Location { get; } = Find();

    internal static string AppSettingsPath =>
        Path.Combine(Location, "src", "appsettings.json");

    internal static string ConfigReferencePath =>
        Path.Combine(Location, "docs", "CONFIG_REFERENCE.md");

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {SolutionFileName} in any directory above {AppContext.BaseDirectory}.");
    }
}
