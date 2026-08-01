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
/// <c>earnings_calendar</c> got its read at 2.3, when the clearance constraint
/// became its first consumer [D-W25]. The <c>corporate_actions</c> read is still
/// deliberately absent: 1.5 reaches a predecessor link through
/// <c>ContractLineage</c>, which is timeless, and no consumer wants the actions
/// in force at a date yet.
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
                   contracts.deliverable_shares,
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
            // The deliverable is read, not defaulted: identity carries five
            // components [§2], and a read that omitted the fifth would mint
            // every stored contract as standard, adjusted series included.
            var identity = ContractIdentity.Of(
                symbol,
                StoreDate.ParseStored(reader.GetString(0)),
                StoreOptionRight.ParseStored(reader.GetString(1)),
                StoreDecimal.ParseStored(reader.GetString(2)),
                reader.GetInt32(3));

            quotes.Add(new ContractQuote(
                identity,
                snapshotDate,
                Bid: StoreDecimal.ParseStored(reader.GetString(4)),
                Ask: StoreDecimal.ParseStored(reader.GetString(5)),
                Last: OptionalDecimal(reader, 6),
                Volume: OptionalCount(reader, 7),
                OpenInterest: OptionalCount(reader, 8),
                ImpliedVolatility: OptionalDecimal(reader, 9),
                Delta: OptionalDecimal(reader, 10),
                Gamma: OptionalDecimal(reader, 11),
                Theta: OptionalDecimal(reader, 12),
                Vega: OptionalDecimal(reader, 13)));
        }

        return [.. quotes.OrderBy(quote => quote.Contract)];
    }

    /// <summary>
    /// The scheduled report dates for <paramref name="symbol"/> falling between
    /// <paramref name="from"/> and <paramref name="to"/> inclusive, as known at
    /// the end of <paramref name="asOf"/>, in date order.
    /// </summary>
    /// <remarks>
    /// The caller passes the buffered window rather than the contract's life, so
    /// the buffer's arithmetic lives with the constraint that owns it [D-W25]
    /// and this read stays a plain range query. Both ends are inclusive, which
    /// is what makes the buffer's own edge inclusive without this method knowing
    /// what a buffer is.
    /// <para>
    /// The session is not returned. D-W25 reads the date only, and a value
    /// nothing consumes would be speculation; the column is written so a vendor
    /// that supplies it does not have the fact discarded, not so this read can
    /// hand it out.
    /// </para>
    /// <para>
    /// The latest observation per report date comes from a CTE with declared
    /// column names, the same shape <see cref="QuotesFor"/> uses. A correction
    /// appends [D-W8], so without the grouping a corrected date would return
    /// twice.
    /// </para>
    /// </remarks>
    public IReadOnlyList<DateOnly> ReportDatesFor(
        Ticker symbol,
        DateOnly from,
        DateOnly to,
        DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            WITH latest(report_date, observed_at) AS (
                SELECT report_date, MAX(observed_at)
                FROM earnings_calendar
                WHERE symbol = $symbol
                  AND report_date >= $from
                  AND report_date <= $to
                  AND observed_at <= $asOf
                GROUP BY report_date
            )
            SELECT report_date
            FROM latest
            ORDER BY report_date;
            """;
        command.Parameters.AddWithValue("$symbol", symbol.Value);
        command.Parameters.AddWithValue("$from", StoreDate.ToStored(from));
        command.Parameters.AddWithValue("$to", StoreDate.ToStored(to));
        command.Parameters.AddWithValue("$asOf", AsOfBoundary.LastInstantOf(asOf));

        var dates = new List<DateOnly>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            dates.Add(StoreDate.ParseStored(reader.GetString(0)));
        }

        return dates;
    }

    private static decimal? OptionalDecimal(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : StoreDecimal.ParseStored(reader.GetString(ordinal));

    private static long? OptionalCount(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
}
