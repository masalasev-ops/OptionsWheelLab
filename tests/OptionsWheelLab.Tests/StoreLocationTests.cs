using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The store resolves to one location both hosts agree on, and refuses values
/// that would let it resolve to two.
/// </summary>
/// <remarks>
/// Not a registered fixture, so deliberately not named <c>FX-*</c>:
/// FX-RegistryMatchesDisk requires every <c>FX-*.cs</c> to have a row in
/// <c>FIXTURES.md</c>.
/// </remarks>
public sealed class StoreLocationTests
{
    [Fact]
    public void An_empty_path_is_refused_with_a_message_naming_the_environment_variable()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => StoreLocation.From(new StorageOptionsView("")));

        Assert.Contains(StoreLocation.EnvironmentVariable, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_path_is_refused()
    {
        Assert.Throws<InvalidOperationException>(
            () => StoreLocation.From(new StorageOptionsView(null)));
    }

    [Fact]
    public void A_relative_path_is_refused_and_the_message_says_why()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => StoreLocation.From(new StorageOptionsView(Path.Combine("data", "store"))));

        Assert.Contains(StoreLocation.EnvironmentVariable, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("working directories", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_absolute_path_binds()
    {
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "owl-absolute"));

        var location = StoreLocation.From(new StorageOptionsView(directory));

        Assert.Equal(directory, location.Directory);
        Assert.Equal(Path.Combine(directory, StoreLocation.DatabaseFileName), location.DatabasePath);
    }

    /// <summary>
    /// The C7 test. The Worker and the Api have different base directories and
    /// different working directories, and neither may influence where the store
    /// is found.
    /// </summary>
    [Fact]
    public void Both_hosts_resolve_the_same_store_from_different_base_directories()
    {
        var configured = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "owl-shared-store"));

        var workerSide = ResolveFrom(configured, workingDirectory: Path.GetTempPath());
        var apiSide = ResolveFrom(configured, workingDirectory: AppContext.BaseDirectory);

        Assert.Equal(workerSide, apiSide);
    }

    /// <summary>
    /// Resolves the store path with the process sitting in a given working
    /// directory, so a resolution that secretly depended on it would differ.
    /// </summary>
    private static string ResolveFrom(string configured, string workingDirectory)
    {
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(workingDirectory);
            return StoreLocation.From(new StorageOptionsView(configured)).DatabasePath;
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }
}
