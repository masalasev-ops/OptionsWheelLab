using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// A store in its own temporary directory, disposed with the test.
/// </summary>
/// <remarks>
/// <b>Every store test gets a fresh database.</b> That is not a preference: the
/// append-only triggers on <c>config_rows</c> raise on UPDATE and DELETE, so the
/// table cannot be cleaned between cases and a shared database would carry rows
/// from one test into the next. Creating a new one per test is the only way to
/// isolate them.
/// <para>
/// Tests never touch the configured store directory, so the suite is
/// independent of the machine and runs on CI where the real path does not
/// exist.
/// </para>
/// </remarks>
internal sealed class TempStore : IDisposable
{
    private TempStore(string directory)
    {
        Directory = directory;
        Location = StoreLocation.From(new StorageOptionsView(directory));
        Connections = new StoreConnectionFactory(Location);
    }

    internal string Directory { get; }

    internal StoreLocation Location { get; }

    internal StoreConnectionFactory Connections { get; }

    internal string DatabasePath => Location.DatabasePath;

    /// <summary>A directory with no database file in it yet.</summary>
    internal static TempStore Empty()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "owl-tests",
            Guid.NewGuid().ToString("N"));

        System.IO.Directory.CreateDirectory(directory);
        return new TempStore(directory);
    }

    /// <summary>A directory whose database file exists and is in WAL mode.</summary>
    internal static TempStore Created()
    {
        var store = Empty();
        using var connection = store.Connections.Open(StoreAccess.Write);
        return store;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leaked handle should not fail an otherwise passing test. The
            // directory is under the OS temp path and will be reclaimed.
        }
    }
}
