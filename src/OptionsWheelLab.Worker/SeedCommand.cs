using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Time;

namespace OptionsWheelLab.Worker;

/// <summary>
/// The <c>seed</c> verb, which writes the initial version of every configuration
/// key Phase 0.8 sets. Lives on the Worker because the Worker is the sole writer
/// to the store [D-W1].
/// </summary>
/// <remarks>
/// <b>A verb rather than a migration, and the reason is not tidiness.</b> A
/// migration is applied once and recorded by id; it can never be re-run or
/// corrected except by another migration. A config value is expected to be
/// revised. Putting version 1 in a migration would make the first version of
/// every key structurally different in origin from every later one, so "how did
/// this value get here" would have two answers, which is what the versioned store
/// exists to prevent.
/// <para>
/// An entry point, and so one of the two places sanctioned to read a clock
/// [D-W30]. The instant is read once and threaded down as a parameter.
/// </para>
/// <para>
/// Idempotent by skipping, not by overwriting: a second run writes nothing. See
/// <see cref="ConfigWriter.AppendMissing"/> for why an identical version + 1
/// would be the wrong kind of no-op.
/// </para>
/// </remarks>
internal static class SeedCommand
{
    internal const string Verb = "seed";

    internal static int Run(IServiceProvider services, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);

        var options = services.GetRequiredService<IOptions<StorageOptions>>().Value;
        var instant = services.GetRequiredService<IClock>().UtcNow;

        var location = StoreLocation.From(new StorageOptionsView(options.Path));
        var factory = new StoreConnectionFactory(location);

        output.WriteLine($"Store:    {location.DatabasePath}");
        output.WriteLine($"Instant:  {StoreTimestamp.ToStored(instant)}");

        using var connection = factory.Open(StoreAccess.Write);
        var outcome = new ConfigWriter(connection).AppendMissing(SeedValues.All, instant);

        foreach (var key in outcome.Written)
        {
            output.WriteLine($"Wrote:    {key}");
        }

        output.WriteLine($"Written:  {outcome.Written.Count}");
        output.WriteLine($"Skipped:  {outcome.Skipped.Count}, already versioned");
        return 0;
    }
}
