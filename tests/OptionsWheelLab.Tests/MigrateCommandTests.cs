using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Time;
using OptionsWheelLab.Worker;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The Worker's <c>migrate</c> verb, which is what <c>migrate.ps1</c> invokes.
/// </summary>
/// <remarks>
/// Not a registered fixture, so not named <c>FX-*</c>. This is the test that
/// makes the Worker reference from the test project real rather than a device
/// for getting the host compiled.
/// <para>
/// The verb is an entry point and so reads the clock [D-W30]. Two tests went
/// with <c>--at</c> at 0.5: an absent instant and an unparseable one were usage
/// errors while the operator named the instant, and neither failure mode exists
/// once nothing outside the process can name it.
/// </para>
/// </remarks>
public sealed class MigrateCommandTests
{
    [Fact]
    public void Migrate_applies_the_migrations_and_reports_what_it_did()
    {
        using var store = TempStore.Empty();
        var output = new StringWriter();

        var exit = MigrateCommand.Run(ServicesFor(store), output);

        var written = output.ToString();

        Assert.Equal(0, exit);
        Assert.Contains(store.DatabasePath, written, StringComparison.Ordinal);
        Assert.Contains("nothing to snapshot", written, StringComparison.Ordinal);
        Assert.Contains("config_rows", written, StringComparison.Ordinal);
    }

    [Fact]
    public void A_second_migrate_reports_that_it_is_up_to_date_and_snapshots()
    {
        using var store = TempStore.Empty();
        var services = ServicesFor(store);

        MigrateCommand.Run(services, new StringWriter());

        var output = new StringWriter();
        var exit = MigrateCommand.Run(services, output);

        var written = output.ToString();

        Assert.Equal(0, exit);
        Assert.Contains("already up to date", written, StringComparison.Ordinal);
        Assert.Contains("snapshot-", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// The verb takes its instant from the injected clock and from nowhere else.
    /// </summary>
    /// <remarks>
    /// Asserted on the stored row rather than on the console line, because the
    /// row is what a later as-of read resolves against. An ambient call anywhere
    /// on this path would put the wall-clock instant in the column instead, and
    /// the assertion would fail by roughly the age of this checkpoint.
    /// </remarks>
    [Fact]
    public void Migrate_stamps_the_row_with_the_instant_the_clock_gave()
    {
        using var store = TempStore.Empty();
        var instant = new DateTimeOffset(2019, 2, 3, 4, 5, 6, 7, TimeSpan.Zero);

        MigrateCommand.Run(ServicesFor(store, new FixedClock(instant)), new StringWriter());

        using var connection = store.Connections.Open(StoreAccess.Write);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT applied_at FROM schema_migrations ORDER BY id;";

        using var reader = command.ExecuteReader();
        var stamps = new List<string>();

        while (reader.Read())
        {
            stamps.Add(reader.GetString(0));
        }

        Assert.NotEmpty(stamps);
        Assert.All(stamps, stamp => Assert.Equal(StoreTimestamp.ToStored(instant), stamp));
    }

    [Fact]
    public void Migrate_reports_the_instant_it_ran_at()
    {
        using var store = TempStore.Empty();
        var output = new StringWriter();

        MigrateCommand.Run(ServicesFor(store), output);

        Assert.Contains(
            StoreTimestamp.ToStored(FixedClock.DefaultInstant),
            output.ToString(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The verb reads the store location from bound options and the instant from
    /// the clock, so the test supplies both directly rather than through the
    /// environment.
    /// </summary>
    private static IServiceProvider ServicesFor(TempStore store, IClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<StorageOptions>>(
            Microsoft.Extensions.Options.Options.Create(
                new StorageOptions { Path = store.Directory }));
        services.AddSingleton<IClock>(clock ?? FixedClock.At());

        return services.BuildServiceProvider();
    }
}
