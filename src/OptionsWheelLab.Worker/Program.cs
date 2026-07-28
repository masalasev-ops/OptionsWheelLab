using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Worker;

// The Worker is the sole writer to the store [D-W1], so the migrate verb lives
// here rather than in a separate tool.
var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddOptionsWheelLabConfiguration();
builder.Services.AddOptionsWheelLabOptions(builder.Configuration);

var host = builder.Build();

if (args.Length > 0 && string.Equals(args[0], MigrateCommand.Verb, StringComparison.OrdinalIgnoreCase))
{
    return MigrateCommand.Run(host.Services, args, Console.Out);
}

host.Run();
return 0;
