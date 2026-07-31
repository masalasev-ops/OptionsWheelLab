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
/// <b>Two members since 2.2, which is the checkpoint that decided how the gate
/// asks.</b> The per-symbol read was withheld through Phase 1 on the grounds
/// that a member nothing calls is speculation; the candidate generator is
/// per-symbol, so it now has a caller. Answering it through
/// <c>MembersOn(...).Contains(...)</c> would read the whole watchlist once per
/// name per day, and both members resolve through one ranking, so the second
/// member is a narrower question rather than a second answer.
/// </para>
/// </remarks>
public sealed class AsOfMembership
{
    /// <summary>
    /// The resolution, stated once. Every read on this surface runs this text.
    /// </summary>
    /// <remarks>
    /// <b>One statement rather than one per member.</b> Two copies of this
    /// ranking would drift the way two copies of one fact do, which is why 1.5
    /// removed <c>Contract.DeliverableShares</c> once identity carried it. A
    /// member answering a narrower question narrows the input, never the rule.
    /// <para>
    /// <b>Narrowing to one symbol cannot change that symbol's answer</b>, which
    /// is what makes the shared text sound rather than merely tidy. The window
    /// partitions by symbol, so a row's rank is computed against its own
    /// symbol's transitions and against nothing else; removing other symbols'
    /// rows removes rows from no partition that matters.
    /// </para>
    /// <para>
    /// <b>The symbol predicate is substituted, not bound, and that was
    /// measured.</b> Writing it as <c>($symbol IS NULL OR symbol = $symbol)</c>
    /// would keep the text literally constant, and <c>EXPLAIN QUERY PLAN</c>
    /// reports <c>SCAN watchlist_membership</c> for it where the direct
    /// predicate reports <c>SEARCH ... (symbol=?)</c>: SQLite will not seek an
    /// index through an <c>OR</c> on a parameter's nullness. A scan per call is
    /// most of what a per-symbol read exists to avoid, so the predicate varies
    /// and the ranking does not. The placeholder is a SQL comment, which keeps
    /// the template valid SQL as written.
    /// </para>
    /// <para>
    /// The ranking runs over every visible transition rather than any single
    /// latest row, which is what makes "no query resolves membership from the
    /// latest row alone" structural. The window's <c>ORDER BY</c> names both
    /// axes where the choice is visible; neither is a decimal column, so the
    /// decimal-ordering rule is untouched.
    /// </para>
    /// <para>
    /// The <c>joined</c> filter is a parameter rendered through
    /// <see cref="StoreMembershipKind"/>, never a literal restating the
    /// declared form: a literal would return empty rather than fail if the
    /// declared form ever moved.
    /// </para>
    /// </remarks>
    private const string RankedMembership =
        """
        WITH ranked(symbol, kind, recency) AS (
            SELECT symbol, kind,
                   ROW_NUMBER() OVER (
                       PARTITION BY symbol
                       ORDER BY effective_on DESC, version DESC)
            FROM watchlist_membership
            WHERE effective_on <= $date
              AND observed_at <= $asOf
              /*symbol*/
        )
        SELECT symbol
        FROM ranked
        WHERE recency = 1 AND kind = $joined
        ORDER BY symbol;
        """;

    /// <summary>
    /// The placeholder the symbol predicate replaces, and the two things it
    /// becomes.
    /// </summary>
    private const string SymbolPlaceholder = "/*symbol*/";

    private const string EverySymbol = "";

    private const string OneSymbol = "AND symbol = $symbol";

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
    public IReadOnlyList<Ticker> MembersOn(DateOnly date, DateOnly asOf) =>
        Resolve(date, asOf, symbol: null);

    /// <summary>
    /// Whether <paramref name="symbol"/> was a member on <paramref name="date"/>,
    /// as known at the end of <paramref name="asOf"/>.
    /// </summary>
    /// <remarks>
    /// The same resolution as <see cref="MembersOn"/>, asked of one name. It
    /// cannot answer differently, because there is one ranking and this narrows
    /// its input rather than restating its rule.
    /// <para>
    /// The candidate generator is this member's caller and asks per symbol,
    /// which is why the narrower question exists at all [2.2].
    /// </para>
    /// </remarks>
    public bool WasMemberOn(Ticker symbol, DateOnly date, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        return Resolve(date, asOf, symbol).Count != 0;
    }

    /// <summary>
    /// The members the resolution returns, restricted to
    /// <paramref name="symbol"/> when one is given and unrestricted when it is
    /// not.
    /// </summary>
    private IReadOnlyList<Ticker> Resolve(DateOnly date, DateOnly asOf, Ticker? symbol)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = RankedMembership.Replace(
            SymbolPlaceholder,
            symbol is null ? EverySymbol : OneSymbol,
            StringComparison.Ordinal);
        command.Parameters.AddWithValue("$date", StoreDate.ToStored(date));
        command.Parameters.AddWithValue("$asOf", AsOfBoundary.LastInstantOf(asOf));
        command.Parameters.AddWithValue(
            "$joined", StoreMembershipKind.ToStored(MembershipKind.Joined));

        if (symbol is not null)
        {
            command.Parameters.AddWithValue("$symbol", symbol.Value);
        }

        var members = new List<Ticker>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            members.Add(Ticker.Normalise(reader.GetString(0)));
        }

        return members;
    }
}
