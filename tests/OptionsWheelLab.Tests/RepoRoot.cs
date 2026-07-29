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

    /// <summary>
    /// The source guards. Registered in `FIXTURES.md` with Kind `guard`, so the
    /// suite reads the script to check the registry against it.
    /// </summary>
    internal static string GuardScriptPath => Path.Combine(Location, "guards.ps1");

    internal static string TestProjectPath =>
        Path.Combine(Location, "tests", "OptionsWheelLab.Tests");

    internal static string SourcePath => Path.Combine(Location, "src");

    internal static string SchemaDocumentPath =>
        Path.Combine(Location, "docs", "DATA_AND_SCHEMA.md");

    /// <summary>
    /// Every committed C# file under a directory, with build output excluded.
    /// </summary>
    /// <remarks>
    /// <c>bin</c> and <c>obj</c> hold generated sources such as
    /// <c>*.AssemblyInfo.cs</c> and <c>*.GlobalUsings.g.cs</c>. Scanning those
    /// would assert over code nobody wrote.
    /// </remarks>
    internal static IReadOnlyList<string> SourceFilesUnder(string directory) =>
        [.. Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .OrderBy(path => path, StringComparer.Ordinal)];

    private static bool IsBuildOutput(string path)
    {
        var relative = Path.GetRelativePath(Location, path);

        return relative
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment =>
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

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
