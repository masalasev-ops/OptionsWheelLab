using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Worker;

/// <summary>
/// The <c>migrate</c> verb. Lives on the Worker because the Worker is the sole
/// writer to the store [D-W1].
/// </summary>
internal static class MigrateCommand
{
    internal const string Verb = "migrate";

    private const string InstantOption = "--at";

    internal static int Run(IServiceProvider services, string[] args, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (!TryReadInstant(args, out var instant, out var problem))
        {
            output.WriteLine(problem);
            return 2;
        }

        var options = services.GetRequiredService<IOptions<StorageOptions>>().Value;
        var location = StoreLocation.From(new StorageOptionsView(options.Path));
        var runner = new MigrationRunner(new StoreConnectionFactory(location));

        output.WriteLine($"Store:    {location.DatabasePath}");

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

    /// <summary>
    /// The instant is supplied rather than read from a clock. There is no clock
    /// until 0.5, and a <c>DateTime.UtcNow</c> here would be a call 0.5 has to
    /// remove.
    /// </summary>
    private static bool TryReadInstant(string[] args, out DateTimeOffset instant, out string problem)
    {
        instant = default;
        problem = string.Empty;

        var index = Array.FindIndex(
            args,
            argument => string.Equals(argument, InstantOption, StringComparison.Ordinal));

        if (index < 0 || index + 1 >= args.Length)
        {
            problem =
                $"{InstantOption} is required, as {InstantOption} {StoreTimestamp.StoredFormat}. "
                + "It is supplied rather than read from a clock because the clock abstraction "
                + "lands at 0.5. migrate.ps1 supplies it.";
            return false;
        }

        try
        {
            instant = StoreTimestamp.ParseStored(args[index + 1]);
            return true;
        }
        catch (FormatException)
        {
            problem =
                $"'{args[index + 1]}' is not a timestamp in the form {StoreTimestamp.StoredFormat}.";
            return false;
        }
    }
}
