using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// Configuration resolved as of a simulated date.
/// </summary>
/// <remarks>
/// Every member takes the date. There is no member that returns a value without
/// one, and no overload that omits it, so a component depending on this type
/// cannot read current configuration even by mistake [D-W26].
/// <para>
/// This is a separate type from <see cref="CurrentConfiguration"/> rather than
/// a second interface on one class, because a shared implementation could be
/// cast back to the current-value surface and the guarantee would be a
/// convention again.
/// </para>
/// <para>
/// Resolution is inclusive of the as-of date: a row written at any time on that
/// date is in force on it. The date is widened to its last instant in
/// <see cref="AsOfBoundary"/> before it meets the timestamp column.
/// </para>
/// </remarks>
public sealed class AsOfConfiguration
{
    private readonly SqliteConnection _connection;

    public AsOfConfiguration(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// The value in force on <paramref name="asOf"/>, or null when the key had
    /// no value by then.
    /// </summary>
    public string? Resolve(string key, DateOnly asOf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return ConfigRowQuery.ResolveAtOrBefore(_connection, key, AsOfBoundary.LastInstantOf(asOf));
    }
}
