using Microsoft.Data.Sqlite;

namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// The one SQL definition of resolving a key, shared by the two surfaces.
/// </summary>
/// <remarks>
/// Internal so neither surface can be bypassed by reaching this directly.
/// </remarks>
internal static class ConfigRowQuery
{
    /// <summary>
    /// The value of the highest version at or before <paramref name="upperBound"/>.
    /// </summary>
    /// <remarks>
    /// No transaction parameter, unlike <see cref="ResolveCurrent"/>, and the
    /// asymmetry is deliberate rather than an omission. An as-of read serves a
    /// simulated date and never runs inside a write today, so it never meets the
    /// behaviour that forced the parameter onto the other method:
    /// Microsoft.Data.Sqlite refuses a command with no transaction while one is
    /// pending on the connection.
    /// <para>
    /// What ends it is a CONFIG as-of read inside a write transaction, and 1.4 was
    /// checked rather than predicted: its detail persists what the loader yields
    /// and verifies by reading back after commit, with no as-of read of anything
    /// inside the write. The remark used to name ingest resolving watchlist
    /// membership as the ender, which was doubly wrong: 1.4's detail contains no
    /// membership resolution, and membership is not a config read, so it would
    /// never pass through this method at all.
    /// </para>
    /// </remarks>
    internal static string? ResolveAtOrBefore(
        SqliteConnection connection,
        string key,
        string upperBound)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT value
            FROM config_rows
            WHERE key = $key AND set_at <= $upperBound
            ORDER BY version DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$upperBound", upperBound);

        return command.ExecuteScalar() as string;
    }

    /// <summary>
    /// The version in force on the same boundary, or null when the key had no
    /// value by then.
    /// </summary>
    /// <remarks>
    /// <b>A widening, and stated as one rather than left to read as a
    /// relaxation.</b> This type returns <c>value</c> and never <c>version</c>
    /// because a consumer reading a bound has no use for which version supplied
    /// it: the number it needs is the number, and handing it a version invites
    /// arithmetic on a column that exists to order rows.
    /// <para>
    /// <b>A decision record does have a use for it</b> [4.3].
    /// <c>decisions.policy_version</c> records which generation of a maker's
    /// policy produced a decision, so a re-score can tell a decision made under
    /// one band from one made under the next. That is a fact about the rows
    /// rather than about their values, and it is the first consumer of this
    /// column outside ordering.
    /// </para>
    /// <para>
    /// It resolves on the same boundary as <see cref="ResolveAtOrBefore"/> and
    /// through the same ordering, so a value and its version cannot come from
    /// different rows.
    /// </para>
    /// </remarks>
    internal static int? VersionAtOrBefore(
        SqliteConnection connection,
        string key,
        string upperBound)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT version
            FROM config_rows
            WHERE key = $key AND set_at <= $upperBound
            ORDER BY version DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$upperBound", upperBound);

        return command.ExecuteScalar() is long version ? (int)version : null;
    }

    /// <summary>The value of the highest version, with no bound.</summary>
    /// <remarks>
    /// <paramref name="transaction"/> is supplied when the read happens inside a
    /// write that has not committed, which is how the cross-key invariants see
    /// the rows they are about to guard [D-W23, D-W24]. Microsoft.Data.Sqlite
    /// refuses a command with no transaction while one is pending on the
    /// connection, so it cannot be left off there.
    /// </remarks>
    internal static string? ResolveCurrent(
        SqliteConnection connection,
        string key,
        SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT value
            FROM config_rows
            WHERE key = $key
            ORDER BY version DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$key", key);

        return command.ExecuteScalar() as string;
    }
}
