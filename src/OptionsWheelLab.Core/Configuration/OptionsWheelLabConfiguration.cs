using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// The single composition site for configuration. Both hosts call it, so there
/// is one place where a section becomes a typed options class and one thing for
/// the binding test to enumerate.
/// </summary>
public static class OptionsWheelLabConfiguration
{
    public const string MainFileName = "appsettings.json";
    public const string SecretsFileName = "appsettings.Secrets.json";

    /// <summary>
    /// Loads the lab's configuration files from the output directory.
    /// </summary>
    /// <remarks>
    /// Resolved against <see cref="AppContext.BaseDirectory"/> rather than left
    /// to the host's default probe, because the generic host and the web host
    /// default their content roots differently and relying on that difference
    /// would make the two hosts read different files.
    /// </remarks>
    public static IConfigurationBuilder AddOptionsWheelLabConfiguration(this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, MainFileName),
            optional: false,
            reloadOnChange: false);

        // Optional so a fresh clone, which has no secrets file, still runs.
        builder.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, SecretsFileName),
            optional: true,
            reloadOnChange: false);

        return builder;
    }

    /// <summary>
    /// Binds every section <c>CONFIG_REFERENCE.md</c> classes as <c>app</c>.
    /// </summary>
    /// <remarks>
    /// A section classed <c>rows</c> is deliberately absent, not overlooked. A
    /// registered options class is itself a current-value accessor, so binding
    /// one here would create the second path to those values that as-of
    /// resolution exists to prevent [D-W26, D-W27].
    /// </remarks>
    public static IServiceCollection AddOptionsWheelLabOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.BindSection<EodhdOptions>(configuration, EodhdOptions.SectionPath);

        return services;
    }

    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to <paramref name="path"/> and
    /// records the pairing in the same call.
    /// </summary>
    /// <remarks>
    /// Every binding goes through here so that the set of
    /// <see cref="BoundSection"/> registrations is a by-product of binding
    /// rather than a parallel list someone has to remember to update.
    /// </remarks>
    public static IServiceCollection BindSection<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string path)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        services.AddOptions<TOptions>().Bind(configuration.GetSection(path));
        services.AddSingleton(new BoundSection(path, typeof(TOptions)));

        return services;
    }
}
