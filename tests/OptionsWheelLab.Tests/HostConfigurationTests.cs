using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Configuration as the hosts actually assemble it.
/// </summary>
/// <remarks>
/// Every test here mutates a process-wide environment variable, so they live in
/// one class: xunit runs tests within a class sequentially, and no other class
/// reads the environment.
/// <para>
/// Not a registered fixture, so not named <c>FX-*</c>.
/// </para>
/// </remarks>
public sealed class HostConfigurationTests : IDisposable
{
    private const string Variable = "Storage__Path";

    private readonly string? _original = Environment.GetEnvironmentVariable(Variable);

    public void Dispose() => Environment.SetEnvironmentVariable(Variable, _original);

    /// <summary>
    /// The provider-ordering fix, asserted rather than demonstrated. Both host
    /// builders add environment variables during construction, so the JSON
    /// files added afterwards would win unless the extension re-adds the
    /// environment after them.
    /// </summary>
    [Fact]
    public void The_generic_host_resolves_the_store_path_from_the_environment()
    {
        var supplied = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "owl-generic-host"));
        Environment.SetEnvironmentVariable(Variable, supplied);

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddOptionsWheelLabConfiguration();
        builder.Services.AddOptionsWheelLabOptions(builder.Configuration);

        using var host = builder.Build();

        Assert.Equal(supplied, StorageOptionsOf(host.Services).Path);
    }

    /// <summary>
    /// The web host resolves it identically. The two host kinds default their
    /// content roots differently, which is why the extension loads its files
    /// from <see cref="AppContext.BaseDirectory"/> rather than relying on that.
    /// </summary>
    [Fact]
    public void The_web_host_resolves_the_same_store_path_as_the_generic_host()
    {
        var supplied = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "owl-web-host"));
        Environment.SetEnvironmentVariable(Variable, supplied);

        var web = WebApplication.CreateBuilder();
        web.Configuration.AddOptionsWheelLabConfiguration();
        web.Services.AddOptionsWheelLabOptions(web.Configuration);
        using var api = web.Build();

        var generic = Host.CreateApplicationBuilder();
        generic.Configuration.AddOptionsWheelLabConfiguration();
        generic.Services.AddOptionsWheelLabOptions(generic.Configuration);
        using var worker = generic.Build();

        Assert.Equal(supplied, StorageOptionsOf(api.Services).Path);
        Assert.Equal(
            StorageOptionsOf(worker.Services).Path,
            StorageOptionsOf(api.Services).Path);
    }

    /// <summary>
    /// The 0.2 rule and the validation-at-use resolution, pinned rather than
    /// argued. Binding an unset key must not stop a host starting; only opening
    /// the store fails.
    /// </summary>
    [Fact]
    public void With_the_variable_absent_binding_succeeds_and_only_opening_the_store_fails()
    {
        Environment.SetEnvironmentVariable(Variable, null);

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddOptionsWheelLabConfiguration();
        builder.Services.AddOptionsWheelLabOptions(builder.Configuration);

        using var host = builder.Build();

        // Binding is fine: the committed value is empty by design.
        var options = StorageOptionsOf(host.Services);
        Assert.True(string.IsNullOrEmpty(options.Path));

        // Using it is not.
        var thrown = Assert.Throws<InvalidOperationException>(
            () => StoreLocation.From(new StorageOptionsView(options.Path)));

        Assert.Contains(Variable, thrown.Message, StringComparison.Ordinal);
    }

    private static StorageOptions StorageOptionsOf(IServiceProvider services) =>
        services.GetRequiredService<IOptions<StorageOptions>>().Value;
}
