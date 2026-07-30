using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Core.MarketData;

/// <summary>
/// Market data as it was known at a date. The only read surface over the
/// snapshot tables.
/// </summary>
/// <remarks>
/// <b>One type, and no current-value counterpart exists at all.</b> Configuration
/// got two surfaces because two legitimate consumers exist: operational paths read
/// current [D-W26] and simulated paths read as-of. Market data has no operational
/// current-read consumer anywhere in the design: the gate, the makers and scoring
/// all serve a simulated date, and ingest writes without reading. A
/// <c>CurrentMarketData</c> would be a second path to values with no consumer to
/// justify it, so the strongest form of "cannot read current" is that no
/// current-reading type exists to cast to.
/// <para>
/// <b>Every read filters on two independent axes</b>: which session the row
/// describes, and when it was observed. "The bar for 2 March, as known on
/// 5 March" is neither axis alone. A correction is a second row on the same
/// session with a later stamp [D-W8], so a read as of before the correction still
/// returns what was believed then. No tie is possible on the second axis, because
/// <c>observed_at</c> is in the primary key; <c>config_rows</c> needs
/// <c>version</c> to break that tie and these tables do not.
/// </para>
/// <para>
/// <b>Every value-returning member takes <c>asOf</c>, by that name.</b> The shape
/// check reflects over this type and asserts it, the way
/// FX-NoCurrentConfigReadOnSimulatedPath asserts the configuration surface. The
/// name matters here where it did not there: a two-axis read means "takes a date"
/// is satisfiable by the session axis alone, and a member taking the session date
/// but not <c>asOf</c> would read the latest observation while looking compliant.
/// </para>
/// <para>
/// Returns the <see cref="Synthetic"/> records rather than new ones, so 1.4's
/// round trip compares what the loader produced with what the store returns using
/// one vocabulary and the same oracle 0.6's fixture uses.
/// </para>
/// <para>
/// <c>corporate_actions</c> and <c>earnings_calendar</c> reads are deliberately
/// absent: their first consumers are 1.5 and Phase 2, and a member nothing calls
/// is speculation.
/// </para>
/// </remarks>
public sealed class AsOfMarketData
{
    private readonly SqliteConnection _connection;

    public AsOfMarketData(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _connection = connection;
    }

    /// <summary>
    /// The bar for <paramref name="sessionDate"/> as known at the end of
    /// <paramref name="asOf"/>, or null when nothing had been observed by then.
    /// </summary>
    public UnderlyingBar? BarFor(Ticker symbol, DateOnly sessionDate, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT open, high, low, close, adj_close, volume
            FROM underlying_bars
            WHERE symbol = $symbol
              AND session_date = $sessionDate
              AND observed_at <= $asOf
            ORDER BY observed_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$symbol", symbol.Value);
        command.Parameters.AddWithValue("$sessionDate", StoreDate.ToStored(sessionDate));
        command.Parameters.AddWithValue("$asOf", AsOfBoundary.LastInstantOf(asOf));

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        // The optional fields go through the null-tolerant readers because
        // UnderlyingBar declares them optional: only the close is required, since
        // WORKED_EXAMPLE §5 supplies closes and nothing else. The schema currently
        // makes all five NOT NULL, which contradicts that record and is raised as
        // a 1.4 finding; reading them as optional here means the read does not
        // change when the schema is corrected.
        return new UnderlyingBar(
            symbol,
            sessionDate,
            Close: StoreDecimal.ParseStored(reader.GetString(3)),
            Open: OptionalDecimal(reader, 0),
            High: OptionalDecimal(reader, 1),
            Low: OptionalDecimal(reader, 2),
            AdjustedClose: OptionalDecimal(reader, 4),
            Volume: OptionalCount(reader, 5));
    }

    /// <summary>
    /// The chain for <paramref name="snapshotDate"/> as known at the end of
    /// <paramref name="asOf"/>, in identity order, empty when nothing had been
    /// observed by then.
    /// </summary>
    /// <remarks>
    /// This is the join 1.1 deferred: <c>contract_quotes</c> reaches identity
    /// through <c>contracts</c> on <c>contract_id</c>, filtered by
    /// <c>contracts.symbol</c>, rather than through a denormalised symbol column.
    /// <para>
    /// The latest observation per contract comes from a CTE with declared column
    /// names, which is the alias convention's own shape: the aggregate lands in a
    /// declared column and nothing acquires a second name.
    /// </para>
    /// <para>
    /// <b>Identity order is imposed here, not in SQL.</b> The stored decimal form
    /// is not order-preserving, so <c>ORDER BY strike</c> would order "9" above
    /// "10" and FX-NoDecimalOrderingInSql refuses it. Ordering happens on the
    /// parsed identities through <see cref="ContractIdentity.CompareTo"/>, the
    /// same total order the loader yields in [D-W4].
    /// </para>
    /// </remarks>
    public IReadOnlyList<ContractQuote> QuotesFor(Ticker symbol, DateOnly snapshotDate, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            WITH latest(contract_id, observed_at) AS (
                SELECT contract_id, MAX(observed_at)
                FROM contract_quotes
                WHERE snapshot_date = $snapshotDate
                  AND observed_at <= $asOf
                GROUP BY contract_id
            )
            SELECT contracts.expiry, contracts.right, contracts.strike,
                   contract_quotes.bid, contract_quotes.ask, contract_quotes.last,
                   contract_quotes.volume, contract_quotes.open_interest,
                   contract_quotes.iv, contract_quotes.delta, contract_quotes.gamma,
                   contract_quotes.theta, contract_quotes.vega
            FROM contract_quotes
            JOIN latest
              ON contract_quotes.contract_id = latest.contract_id
             AND contract_quotes.observed_at = latest.observed_at
            JOIN contracts
              ON contracts.contract_id = contract_quotes.contract_id
            WHERE contracts.symbol = $symbol
              AND contract_quotes.snapshot_date = $snapshotDate;
            """;
        command.Parameters.AddWithValue("$symbol", symbol.Value);
        command.Parameters.AddWithValue("$snapshotDate", StoreDate.ToStored(snapshotDate));
        command.Parameters.AddWithValue("$asOf", AsOfBoundary.LastInstantOf(asOf));

        var quotes = new List<ContractQuote>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            var identity = ContractIdentity.Of(
                symbol,
                StoreDate.ParseStored(reader.GetString(0)),
                StoreOptionRight.ParseStored(reader.GetString(1)),
                StoreDecimal.ParseStored(reader.GetString(2)));

            quotes.Add(new ContractQuote(
                identity,
                snapshotDate,
                Bid: StoreDecimal.ParseStored(reader.GetString(3)),
                Ask: StoreDecimal.ParseStored(reader.GetString(4)),
                Last: OptionalDecimal(reader, 5),
                Volume: OptionalCount(reader, 6),
                OpenInterest: OptionalCount(reader, 7),
                ImpliedVolatility: OptionalDecimal(reader, 8),
                Delta: OptionalDecimal(reader, 9),
                Gamma: OptionalDecimal(reader, 10),
                Theta: OptionalDecimal(reader, 11),
                Vega: OptionalDecimal(reader, 12)));
        }

        return [.. quotes.OrderBy(quote => quote.Contract)];
    }

    private static decimal? OptionalDecimal(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : StoreDecimal.ParseStored(reader.GetString(ordinal));

    private static long? OptionalCount(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
}
