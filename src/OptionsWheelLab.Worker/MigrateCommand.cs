using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Time;

namespace OptionsWheelLab.Worker;

/// <summary>
/// The <c>migrate</c> verb. Lives on the Worker because the Worker is the sole
/// writer to the store [D-W1].
/// </summary>
/// <remarks>
/// An entry point, and so one of the two places sanctioned to read a clock
/// [D-W30]. Everything it calls takes the instant as a parameter.
/// <para>
/// There is no way to supply the instant from outside. It was <c>--at</c> until
/// 0.5, for want of a clock, and an override left in place would be a way to
/// write a <c>set_at</c> that never happened into a store whose rows can never
/// be corrected.
/// </para>
/// </remarks>
internal static class MigrateCommand
{
    internal const string Verb = "migrate";

    internal static int Run(IServiceProvider services, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);

        var options = services.GetRequiredService<IOptions<StorageOptions>>().Value;
        var instant = services.GetRequiredService<IClock>().UtcNow;

        var location = StoreLocation.From(new StorageOptionsView(options.Path));
        var runner = new MigrationRunner(new StoreConnectionFactory(location));

        output.WriteLine($"Store:    {location.DatabasePath}");
        output.WriteLine($"Instant:  {StoreTimestamp.ToStored(instant)}");

        var result = runner.Run(instant);

        output.WriteLine(result.Snapshot.Taken
            ? $"Snapshot: {result.Snapshot.Path}"
            : $"Snapshot: skipped, {result.Snapshot.Reason}");

        if (result.Applied.Count == 0)
        {
            output.WriteLine("Applied:  nothing, already up to date");
        }
        else
        {
            foreach (var migration in result.Applied)
            {
                output.WriteLine($"Applied:  {migration.Id} {migration.Name}");
            }
        }

        output.WriteLine($"Schema:   {result.SchemaVersion}");
        return 0;
    }
}
