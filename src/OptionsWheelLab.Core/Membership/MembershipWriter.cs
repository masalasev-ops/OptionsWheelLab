using System.Globalization;
using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Membership;

/// <summary>
/// Appends one watchlist transition and returns the version written.
/// </summary>
/// <remarks>
/// Worker-side: the Worker is the sole writer [D-W1]. This is
/// <see cref="Configuration.ConfigWriter"/>'s shape: the version is
/// <c>MAX(version) + 1</c> for the symbol, computed inside the same statement
/// and transaction as the insert, and reported through <c>RETURNING</c> so the
/// number returned is the one this statement wrote.
/// <para>
/// Both instants are parameters, never clock reads. The clock is read at
/// composition and entry points only [D-W30], and a correction appends with the
/// instant its caller was given, which the store's monotonic trigger keeps at
/// or after the symbol's newest.
/// </para>
/// <para>
/// The trigger holds against any writer; the check here exists only so the
/// refusal can name both instants, which SQLite's <c>RAISE</c> cannot. Inside
/// the same transaction, so it cannot race the insert it guards.
/// </para>
/// </remarks>
public sealed class MembershipWriter
{
    private readonly SqliteConnection _connection;

    public MembershipWriter(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// Inserts the next version of <paramref name="symbol"/>'s transition
    /// history and returns the version number written.
    /// </summary>
    public int Append(
        Ticker symbol,
        MembershipKind kind,
        DateOnly effectiveOn,
        DateTimeOffset observedAt,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        using var transaction = _connection.BeginTransaction(deferred: false);

        RefuseIfEarlierThanNewest(transaction, symbol, observedAt);
        var version = Insert(transaction, symbol, kind, effectiveOn, observedAt, reason);

        transaction.Commit();

        return version;
    }

    private int Insert(
        SqliteTransaction transaction,
        Ticker symbol,
        MembershipKind kind,
        DateOnly effectiveOn,
        DateTimeOffset observedAt,
        string? reason)
    {
        using var insert = _connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT INTO watchlist_membership (symbol, version, effective_on, kind, reason, observed_at)
            SELECT $symbol,
                   COALESCE(MAX(version), 0) + 1,
                   $effectiveOn,
                   $kind,
                   $reason,
                   $observed
            FROM watchlist_membership
            WHERE symbol = $symbol
            RETURNING version;
            """;
        insert.Parameters.AddWithValue("$symbol", symbol.Value);
        insert.Parameters.AddWithValue("$effectiveOn", StoreDate.ToStored(effectiveOn));
        insert.Parameters.AddWithValue("$kind", StoreMembershipKind.ToStored(kind));
        insert.Parameters.AddWithValue("$reason", reason ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$observed", StoreTimestamp.ToStored(observedAt));

        return Convert.ToInt32(insert.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Refuses a stamp that predates the newest already stored for the symbol.
    /// </summary>
    /// <remarks>
    /// The as-of read filters on <c>observed_at</c>, so an earlier stamp would
    /// change what was believed at a past instant after the fact, and the
    /// append-only guards would make that permanent. Equal is allowed: two
    /// transitions can share an instant, and version breaks the tie.
    /// </remarks>
    private void RefuseIfEarlierThanNewest(
        SqliteTransaction transaction,
        Ticker symbol,
        DateTimeOffset observedAt)
    {
        using var newest = _connection.CreateCommand();
        newest.Transaction = transaction;
        newest.CommandText =
            "SELECT MAX(observed_at) FROM watchlist_membership WHERE symbol = $symbol;";
        newest.Parameters.AddWithValue("$symbol", symbol.Value);

        if (newest.ExecuteScalar() is not string newestObserved)
        {
            return;
        }

        var candidate = StoreTimestamp.ToStored(observedAt);

        if (string.CompareOrdinal(candidate, newestObserved) < 0)
        {
            throw new InvalidOperationException(
                $"Cannot append a transition for '{symbol.Value}' at {candidate} because its "
                + $"newest version is already at {newestObserved}. observed_at moves forward "
                + "for a symbol: the as-of read filters on it, so an earlier stamp would "
                + "change what was believed at a past instant after the fact, and the "
                + "append-only guards would make that permanent. No row was written.");
        }
    }
}
