using Microsoft.Extensions.DependencyInjection;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Time;
using OptionsWheelLab.Worker;

// The Worker is the sole writer to the store [D-W1], so the migrate verb lives
// here rather than in a separate tool.
var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddOptionsWheelLabConfiguration();
builder.Services.AddOptionsWheelLabOptions(builder.Configuration);

// The clock is registered here rather than in Core's composition site, and only
// in this host. The Worker is the sole writer and so the only host that produces
// an instant; the Api opens the store read-only and has nothing to stamp
// [D-W30].
builder.Services.AddSingleton<IClock>(SystemClock.Instance);

var host = builder.Build();

if (args.Length > 0 && string.Equals(args[0], MigrateCommand.Verb, StringComparison.OrdinalIgnoreCase))
{
    return MigrateCommand.Run(host.Services, Console.Out);
}

host.Run();
return 0;
