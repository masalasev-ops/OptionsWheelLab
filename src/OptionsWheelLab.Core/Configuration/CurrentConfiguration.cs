using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// Configuration at its newest version, for operational paths.
/// </summary>
/// <remarks>
/// Deliberately a different type from <see cref="AsOfConfiguration"/> and not
/// reachable from it. Nothing serving a simulated date may depend on this:
/// re-scoring, replaying or auditing an earlier session against current
/// configuration fails its own reproduction check, and the failure presents as
/// impure inputs rather than as a configuration-resolution bug [D-W26].
/// <para>
/// Legitimate callers are operator tooling and anything reporting what the lab
/// is configured to do now.
/// </para>
/// </remarks>
public sealed class CurrentConfiguration
{
    private readonly SqliteConnection _connection;

    public CurrentConfiguration(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>The newest value for the key, or null when it has none.</summary>
    public string? ResolveCurrent(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return ConfigRowQuery.ResolveCurrent(_connection, key);
    }
}
