using OptionsWheelLab.Core.Configuration;

// The Api opens the store read-only. It has no store to open yet; that arrives
// at Phase 0.3, and the read-only connection is proven by FX-ApiCannotWrite.
var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddOptionsWheelLabConfiguration();
builder.Services.AddOptionsWheelLabOptions(builder.Configuration);

var app = builder.Build();

app.Run();
