using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Membership;

/// <summary>
/// Watchlist membership as it was known at a date. The only read surface over
/// the membership record.
/// </summary>
/// <remarks>
/// <b>Its own type, not a member of <see cref="MarketData.AsOfMarketData"/>,
/// deliberately.</b> That type documents itself as the only read surface over
/// the snapshot tables, and membership is not a snapshot: it is the record of
/// §4.2 [D-W35], correcting by version rather than by re-observation, with a
/// writer beside it. The market-data one-surface guarantee also rests on there
/// being no operational current-read consumer for market data anywhere in the
/// design, which was never argued for membership and is probably false for it:
/// the watchlist is operator-managed state, and Phase 8's ingest plausibly
/// reads current membership to know what to fetch. If a current-membership
/// surface is ever justified, it arrives as a decision, not a cast from this
/// one.
/// <para>
/// <b>The governing axis is <c>effective_on</c>, with <c>version</c> breaking
/// ties</b> [§4.2]. Among rows whose <c>effective_on</c> is at or before the
/// date and whose <c>observed_at</c> is at or before the instant, the row with
/// the greatest (<c>effective_on</c>, <c>version</c>) governs. Latest version
/// alone would be wrong: a correction fixing an old join date would govern
/// dates after a genuine later departure. Under this rule an appended
/// correction supersedes a transition only by tying its date, and correcting a
/// transition's date is a compensating pair.
/// </para>
/// <para>
/// Every read filters on two independent axes: which date is being asked
/// about, and what was known when it is asked [D-W9]. A correction recorded
/// after a simulated instant is invisible to a read as of that instant.
/// </para>
/// <para>
/// One member. A per-symbol read has no consumer until Phase 2 decides how the
/// gate asks, and a member nothing calls is speculation.
/// </para>
/// </remarks>
public sealed class AsOfMembership
{
    private readonly SqliteConnection _connection;

    public AsOfMembership(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// The names that were members on <paramref name="date"/>, as known at the
    /// end of <paramref name="asOf"/>, in symbol order, empty when nothing had
    /// been observed by then.
    /// </summary>
    /// <remarks>
    /// The ranking runs over every visible transition rather than any single
    /// latest row, which is what makes "no query resolves membership from the
    /// latest row alone" structural. The window's <c>ORDER BY</c> names both
    /// axes where the choice is visible; neither is a decimal column, so the
    /// decimal-ordering rule is untouched.
    /// <para>
    /// The <c>joined</c> filter is a parameter rendered through
    /// <see cref="StoreMembershipKind"/>, never a literal restating the
    /// declared form: a literal would return empty rather than fail if the
    /// declared form ever moved.
    /// </para>
    /// </remarks>
    public IReadOnlyList<Ticker> MembersOn(DateOnly date, DateOnly asOf)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            WITH ranked(symbol, kind, recency) AS (
                SELECT symbol, kind,
                       ROW_NUMBER() OVER (
                           PARTITION BY symbol
                           ORDER BY effective_on DESC, version DESC)
                FROM watchlist_membership
                WHERE effective_on <= $date
                  AND observed_at <= $asOf
            )
            SELECT symbol
            FROM ranked
            WHERE recency = 1 AND kind = $joined
            ORDER BY symbol;
            """;
        command.Parameters.AddWithValue("$date", StoreDate.ToStored(date));
        command.Parameters.AddWithValue("$asOf", AsOfBoundary.LastInstantOf(asOf));
        command.Parameters.AddWithValue(
            "$joined", StoreMembershipKind.ToStored(MembershipKind.Joined));

        var members = new List<Ticker>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            members.Add(Ticker.Normalise(reader.GetString(0)));
        }

        return members;
    }
}
