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
/// <para>
/// The typed accessors are public instance methods and must stay that way. An
/// extension method or a static would be a natural way to add one, and
/// FX-NoCurrentConfigReadOnSimulatedPath reflects over this type's declared
/// instance members, so either would be invisible to it: the guard would read
/// green while covering nothing.
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

    /// <summary>
    /// The decimal in force on <paramref name="asOf"/>, or null when the key had
    /// no value by then. See <see cref="ConfigValue"/> for why this exists.
    /// </summary>
    public decimal? ResolveDecimal(string key, DateOnly asOf) =>
        ConfigValue.AsDecimal(Resolve(key, asOf), key);

    /// <summary>
    /// The integer in force on <paramref name="asOf"/>, or null when the key had
    /// no value by then.
    /// </summary>
    public int? ResolveInt(string key, DateOnly asOf) =>
        ConfigValue.AsInt(Resolve(key, asOf), key);

    /// <summary>
    /// The version in force on <paramref name="asOf"/>, or null when the key had
    /// no value by then.
    /// </summary>
    /// <remarks>
    /// <b>Which row supplied a value, rather than the value.</b> Every other
    /// method here answers what a component should use; this one answers which
    /// generation of configuration it used, which only a record has a use for.
    /// <c>decisions.policy_version</c> is that record and is the first consumer
    /// [4.3].
    /// <para>
    /// It resolves on the same boundary and ordering as the others, so a value
    /// and its version cannot be read from different rows.
    /// </para>
    /// </remarks>
    public int? ResolveVersion(string key, DateOnly asOf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return ConfigRowQuery.VersionAtOrBefore(_connection, key, AsOfBoundary.LastInstantOf(asOf));
    }
}
