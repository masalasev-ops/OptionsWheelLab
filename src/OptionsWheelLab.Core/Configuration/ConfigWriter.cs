using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Configuration;

/// <summary>One key and the value to write for it, with the note explaining the choice.</summary>
public sealed record ConfigEntry(string Key, string Value, string? Note = null);

/// <summary>
/// Appends a new version of a configuration key.
/// </summary>
/// <remarks>
/// Worker-side: the Worker is the sole writer [D-W1]. A change never updates a
/// row, it inserts version + 1, which is what lets a later behaviour change be
/// explained after the fact.
/// <para>
/// <b>The cross-key invariants are enforced here, on every write.</b> They were
/// pure predicates with no caller until 0.8. Putting the check in the seeder
/// instead would leave <see cref="Append"/> an unguarded path, and D-W23, D-W24
/// and D-W27 all say enforcement is at the moment a version is written rather
/// than at startup, precisely because versions are insertable while the process
/// runs.
/// </para>
/// <para>
/// <b>A write that leaves an invariant unevaluable is refused too</b> [D-W34],
/// which is what stops the check passing vacuously on the way to a complete set.
/// Its consequence is the mechanism: <c>Gate:MaxDte</c> and
/// <c>Trial:MaxTrialDays</c> cannot be written apart.
/// </para>
/// </remarks>
public sealed class ConfigWriter
{
    private readonly SqliteConnection _connection;

    public ConfigWriter(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// Inserts the next version of <paramref name="key"/> and returns the
    /// version number written.
    /// </summary>
    /// <remarks>
    /// The version is computed inside the same statement and transaction as the
    /// insert, so two writers cannot both read the same maximum and produce one
    /// version. The primary key on (key, version) makes a collision fail rather
    /// than pass silently.
    /// <para>
    /// <paramref name="setAt"/> is a parameter, never a clock read. A clock
    /// exists from 0.5 and this still does not take one: it is read at
    /// composition and entry points only, and nothing below them holds one
    /// [D-W30]. Injecting it here would also replace a fixed value in tests with
    /// a fake and buy nothing.
    /// </para>
    /// </remarks>
    public int Append(string key, string value, DateTimeOffset setAt, string? note = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        AppendAll([new ConfigEntry(key, value, note)], setAt);

        using var read = _connection.CreateCommand();
        read.CommandText = "SELECT MAX(version) FROM config_rows WHERE key = $key;";
        read.Parameters.AddWithValue("$key", key);
        return Convert.ToInt32(read.ExecuteScalar());
    }

    /// <summary>
    /// Writes every entry in one transaction, or writes none of them.
    /// </summary>
    /// <remarks>
    /// <b>One transaction is not a convenience.</b> A cross-key invariant cannot
    /// be evaluated while only one of its keys exists, so writing key by key
    /// either fails on the first or passes vacuously until the last. The
    /// invariants are checked over the complete set, with what is already stored,
    /// before the transaction commits.
    /// </remarks>
    public void AppendAll(IReadOnlyList<ConfigEntry> entries, DateTimeOffset setAt)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return;
        }

        using var transaction = _connection.BeginTransaction(deferred: false);

        foreach (var entry in entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Key);
            ArgumentNullException.ThrowIfNull(entry.Value);

            RefuseIfEarlierThanNewest(transaction, entry.Key, setAt);
            Insert(transaction, entry, setAt);
        }

        RefuseIfInvariantsDoNotHold(transaction, entries);

        transaction.Commit();
    }

    private void Insert(SqliteTransaction transaction, ConfigEntry entry, DateTimeOffset setAt)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO config_rows (key, version, value, set_at, note)
            SELECT $key,
                   COALESCE(MAX(version), 0) + 1,
                   $value,
                   $setAt,
                   $note
            FROM config_rows
            WHERE key = $key;
            """;
        insert.Parameters.AddWithValue("$key", entry.Key);
        insert.Parameters.AddWithValue("$value", entry.Value);
        insert.Parameters.AddWithValue("$setAt", StoreTimestamp.ToStored(setAt));
        insert.Parameters.AddWithValue("$note", entry.Note ?? (object)DBNull.Value);
        insert.ExecuteNonQuery();
    }

    /// <summary>
    /// Checks both cross-key invariants over the store as this write leaves it.
    /// </summary>
    /// <remarks>
    /// Read through the current-value surface's query rather than an as-of one:
    /// a write is an operational path, not a simulated-date one, so reading the
    /// newest version is the right question [D-W26].
    /// <para>
    /// Only an invariant this write touches is checked. A write of an unrelated
    /// key is not the moment to re-litigate a pair it has nothing to do with,
    /// and D-W34 scopes the refusal the same way.
    /// </para>
    /// </remarks>
    private void RefuseIfInvariantsDoNotHold(
        SqliteTransaction transaction,
        IReadOnlyList<ConfigEntry> entries)
    {
        var touched = entries.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);

        if (touched.Overlaps(ConfigKeys.DeltaCeilingKeys))
        {
            CheckDeltaCeiling(transaction);
        }

        if (touched.Overlaps(ConfigKeys.TrialBoundKeys))
        {
            CheckTrialBound(transaction);
        }
    }

    private void CheckDeltaCeiling(SqliteTransaction transaction)
    {
        var ceiling = RequiredDecimal(transaction, ConfigKeys.GateMaxDelta, ConfigKeys.DeltaCeilingKeys);

        var bands = ConfigKeys.PolicyBandCeilings
            .Select(band => new PolicyBand(
                band.Name,
                decimal.Zero,
                RequiredDecimal(transaction, band.Key, ConfigKeys.DeltaCeilingKeys)))
            .ToList();

        if (ConfigurationInvariants.CeilingNotInsidePolicyBand(ceiling, bands))
        {
            return;
        }

        var offending = ConfigurationInvariants.BandsTighterThanCeiling(ceiling, bands);

        throw new InvalidOperationException(
            $"'{ConfigKeys.GateMaxDelta}' of {ceiling} is tighter than "
            + $"{string.Join(" and ", offending.Select(band => $"the {band.Name} band at {band.DeltaMax}"))}. "
            + "The ceiling is an outer bound on catastrophe, not a strategy parameter, so a "
            + "ceiling inside a policy band would silently override that policy rather than "
            + "bound it [D-W23]. No row was written.");
    }

    private void CheckTrialBound(SqliteTransaction transaction)
    {
        var maxDte = RequiredInt(transaction, ConfigKeys.GateMaxDte, ConfigKeys.TrialBoundKeys);
        var maxTrialDays = RequiredInt(transaction, ConfigKeys.TrialMaxTrialDays, ConfigKeys.TrialBoundKeys);

        if (ConfigurationInvariants.MaxDteBelowTrialBound(maxDte, maxTrialDays))
        {
            return;
        }

        throw new InvalidOperationException(
            $"'{ConfigKeys.GateMaxDte}' of {maxDte} is not below "
            + $"'{ConfigKeys.TrialMaxTrialDays}' of {maxTrialDays}. An opening contract "
            + "longer-dated than the trial bound would guarantee a forced close at market "
            + "before its own expiry, making the trial's outcome an artefact of the bound "
            + "rather than of the decision [D-W24]. No row was written.");
    }

    private decimal RequiredDecimal(SqliteTransaction transaction, string key, IReadOnlySet<string> needed) =>
        ConfigValue.AsDecimal(Required(transaction, key, needed), key)!.Value;

    private int RequiredInt(SqliteTransaction transaction, string key, IReadOnlySet<string> needed) =>
        ConfigValue.AsInt(Required(transaction, key, needed), key)!.Value;

    /// <summary>
    /// The value a touched invariant needs, or a refusal naming what is missing.
    /// </summary>
    /// <remarks>
    /// This is D-W34. Skipping the check when a key is absent would let it pass
    /// vacuously until the last key landed, which is the state the enforcement
    /// exists to prevent, so the write is refused instead of the check being
    /// waived.
    /// </remarks>
    private string Required(SqliteTransaction transaction, string key, IReadOnlySet<string> needed)
    {
        var stored = ConfigRowQuery.ResolveCurrent(_connection, key, transaction);

        if (stored is not null)
        {
            return stored;
        }

        throw new InvalidOperationException(
            $"This write touches a cross-key invariant and '{key}' has no value, so the "
            + "invariant cannot be evaluated. Every one of "
            + $"{string.Join(", ", needed.Order(StringComparer.Ordinal))} must be present. "
            + "Write them together: a check skipped for want of an operand passes vacuously "
            + "until the last one lands, which is the state it exists to prevent [D-W34]. No "
            + "row was written.");
    }

    /// <summary>
    /// Refuses a version that predates the newest already stored for the key.
    /// </summary>
    /// <remarks>
    /// The store enforces this with a trigger, which holds against any writer.
    /// This check exists only so the refusal can name both instants: SQLite's
    /// <c>RAISE</c> takes a string literal and cannot interpolate the values
    /// that caused it. Inside the same transaction, so it cannot race the
    /// insert it guards.
    /// </remarks>
    private void RefuseIfEarlierThanNewest(
        SqliteTransaction transaction,
        string key,
        DateTimeOffset setAt)
    {
        using var newest = _connection.CreateCommand();
        newest.Transaction = transaction;
        newest.CommandText = "SELECT MAX(set_at) FROM config_rows WHERE key = $key;";
        newest.Parameters.AddWithValue("$key", key);

        if (newest.ExecuteScalar() is not string newestSetAt)
        {
            return;
        }

        var candidate = StoreTimestamp.ToStored(setAt);

        if (string.CompareOrdinal(candidate, newestSetAt) < 0)
        {
            throw new InvalidOperationException(
                $"Cannot append '{key}' at {candidate} because its newest version is already at "
                + $"{newestSetAt}. set_at moves forward for a key: resolution filters on set_at "
                + "and then orders by version, so an earlier timestamp would make the value in "
                + "force on a date depend on insertion order rather than on time, and the "
                + "append-only guards would make that permanent.");
        }
    }
}
