using OptionsWheelLab.Core.Configuration;

// The Worker is the sole writer to the store. Nothing is written yet; the store
// arrives at Phase 0.3.
var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddOptionsWheelLabConfiguration();
builder.Services.AddOptionsWheelLabOptions(builder.Configuration);

var host = builder.Build();
host.Run();
