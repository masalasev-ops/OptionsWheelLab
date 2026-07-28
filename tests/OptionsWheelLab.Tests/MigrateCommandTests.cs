using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Worker;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The Worker's <c>migrate</c> verb, which is what <c>migrate.ps1</c> invokes.
/// </summary>
/// <remarks>
/// Not a registered fixture, so not named <c>FX-*</c>. This is the test that
/// makes the Worker reference from the test project real rather than a device
/// for getting the host compiled.
/// </remarks>
public sealed class MigrateCommandTests
{
    private const string Instant = "2026-07-28T09:15:30.250Z";

    [Fact]
    public void Migrate_applies_the_migrations_and_reports_what_it_did()
    {
        using var store = TempStore.Empty();
        var output = new StringWriter();

        var exit = MigrateCommand.Run(
            ServicesFor(store),
            [MigrateCommand.Verb, "--at", Instant],
            output);

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

        MigrateCommand.Run(services, [MigrateCommand.Verb, "--at", Instant], new StringWriter());

        var output = new StringWriter();
        var exit = MigrateCommand.Run(
            services,
            [MigrateCommand.Verb, "--at", "2026-07-28T10:15:30.250Z"],
            output);

        var written = output.ToString();

        Assert.Equal(0, exit);
        Assert.Contains("already up to date", written, StringComparison.Ordinal);
        Assert.Contains("snapshot-", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// The instant is supplied rather than read from a clock, so its absence is
    /// a usage error and not a reason to invent one.
    /// </summary>
    [Fact]
    public void Migrate_without_an_instant_refuses_and_says_what_it_wanted()
    {
        using var store = TempStore.Empty();
        var output = new StringWriter();

        var exit = MigrateCommand.Run(ServicesFor(store), [MigrateCommand.Verb], output);

        Assert.NotEqual(0, exit);
        Assert.Contains("--at", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Migrate_with_an_unparseable_instant_refuses()
    {
        using var store = TempStore.Empty();
        var output = new StringWriter();

        var exit = MigrateCommand.Run(
            ServicesFor(store),
            [MigrateCommand.Verb, "--at", "yesterday"],
            output);

        Assert.NotEqual(0, exit);
        Assert.Contains(StoreTimestamp.StoredFormat, output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The verb reads the store location from bound options, so the test
    /// supplies them directly rather than through the environment.
    /// </summary>
    private static IServiceProvider ServicesFor(TempStore store)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<StorageOptions>>(
            Microsoft.Extensions.Options.Options.Create(
                new StorageOptions { Path = store.Directory }));

        return services.BuildServiceProvider();
    }
}
