using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Builds the real composition root over the committed
/// <c>src/appsettings.json</c>.
/// </summary>
/// <remarks>
/// The tests enumerate what this produces rather than what they expect it to
/// produce. Nothing here names a section.
/// </remarks>
internal static class Composition
{
    internal static IConfigurationRoot Configuration() => Read(RepoRoot.AppSettingsPath);

    /// <summary>
    /// The committed secrets example, which is inside the binding check because
    /// a section in it that binds to nothing is the same defect.
    /// </summary>
    /// <remarks>
    /// The real <c>appsettings.Secrets.json</c> stays outside: it is gitignored
    /// and absent on a fresh clone, so a test asserting against it would pass
    /// or fail depending on the developer's machine.
    /// </remarks>
    internal static IConfigurationRoot SecretsExample() => Read(RepoRoot.SecretsExamplePath);

    private static IConfigurationRoot Read(string path) =>
        new ConfigurationBuilder()
            .AddJsonFile(path, optional: false, reloadOnChange: false)
            .Build();

    internal static IServiceCollection Services(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddOptionsWheelLabOptions(configuration);
        return services;
    }

    /// <summary>Top-level section names present in <c>src/appsettings.json</c>.</summary>
    internal static IReadOnlyList<string> SectionsInFile(IConfiguration configuration) =>
        configuration.GetChildren().Select(section => section.Key).ToList();

    /// <summary>Every section composition actually bound.</summary>
    internal static IReadOnlyList<BoundSection> BoundSections(IServiceCollection services) =>
        services
            .Where(descriptor => descriptor.ServiceType == typeof(BoundSection))
            .Select(descriptor => (BoundSection)descriptor.ImplementationInstance!)
            .ToList();
}
