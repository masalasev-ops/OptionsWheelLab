using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The ledger writes, reads back, and rebuilds its projections.
/// </summary>
/// <remarks>
/// Not registered fixtures: FX-ProjectionRebuildsFromLedger is the registered
/// check and asserts the property against the worked example's trial. What is
/// here is the plumbing that check cannot isolate, on
/// <see cref="PortfolioConstraintsTests"/>' argument.
/// </remarks>
public sealed class TrialStoreTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 7, 30, 9, 0, 0, 0, TimeSpan.Zero);

    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly TrialBounds Seeded = new(MaxRolls: 2, MaxTrialDays: 120);

    [Fact]
    public void Entries_read_back_in_the_order_they_were_written()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var put = Written(connection, new(2026, 4, 17), OptionRight.Put, 50.00m);
        var trials = new TrialStore(connection);
        var trialId = trials.OpenTrial("baseline", Symbol, new(2026, 3, 2), 50.00m);

        trials.Append(
            trialId,
            [
                new LedgerEntry(
                    new(2026, 3, 2), new(2026, 3, 2),
                    LedgerEntryKind.PremiumReceived, 94.35m, put),
                new LedgerEntry(
                    new(2026, 4, 17), new(2026, 4, 20),
                    LedgerEntryKind.Assignment, -5_000.00m, put),
            ]);

        var read = trials.EntriesFor(trialId);

        Assert.Equal(
            [LedgerEntryKind.PremiumReceived, LedgerEntryKind.Assignment],
            read.Select(entry => entry.Kind));
        Assert.Equal(put, read[1].Contract);
        Assert.Equal(new DateOnly(2026, 4, 20), read[1].KnownOn);
        Assert.Equal(-5_000.00m, read[1].Amount);
    }

    /// <summary>
    /// An entry against a contract the store does not hold is refused.
    /// </summary>
    /// <remarks>
    /// The chain is written before the trial that trades it. A null would leave
    /// an entry pointing at nothing, which rebuilds into a position with no
    /// instrument, and a rebuild that silently loses an instrument is the failure
    /// the rebuild test exists to catch and would not.
    /// </remarks>
    [Fact]
    public void An_entry_against_an_unheld_contract_is_refused()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var trials = new TrialStore(connection);
        var trialId = trials.OpenTrial("baseline", Symbol, new(2026, 3, 2), 50.00m);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => trials.Append(
                trialId,
                [
                    new LedgerEntry(
                        new(2026, 3, 2), new(2026, 3, 2),
                        LedgerEntryKind.PremiumReceived,
                        94.35m,
                        ContractIdentity.Of(Symbol, new(2026, 4, 17), OptionRight.Put, 50.00m)),
                ]));

        Assert.Contains("not a contract this store holds", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The projections are rewritten in place, and the ledger is not.
    /// </summary>
    /// <remarks>
    /// Both halves of [D-W35] against the store in one test: the rebuild deletes
    /// from <c>positions</c> and <c>trials</c> and succeeds, and the entries it
    /// read are still there afterwards because nothing may remove them.
    /// </remarks>
    [Fact]
    public void A_rebuild_rewrites_the_projections_and_leaves_the_ledger()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var (trials, trialId) = WorkedExampleTrial(connection);

        trials.Rebuild(trialId, Seeded);
        trials.Rebuild(trialId, Seeded);

        Assert.Equal(6, trials.EntriesFor(trialId).Count);
        Assert.Equal(1L, CountOf(connection, "trials"));
        Assert.Equal(4L, CountOf(connection, "positions"));
    }

    /// <summary>
    /// The trial row the rebuild reconstructs carries §6.3's figures.
    /// </summary>
    [Fact]
    public void The_rebuilt_trial_carries_the_worked_examples_figures()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var (trials, trialId) = WorkedExampleTrial(connection);

        trials.Rebuild(trialId, Seeded);

        using var read = connection.CreateCommand();
        read.CommandText =
            """
            SELECT maker_id, symbol, opened_on, closed_on, open_strike, committed_capital,
                   rolls_used, close_kind
            FROM trials
            WHERE trial_id = $trial;
            """;
        read.Parameters.AddWithValue("$trial", trialId);

        using var row = read.ExecuteReader();

        Assert.True(row.Read());
        Assert.Equal("baseline", row.GetString(0));
        Assert.Equal("WDGT", row.GetString(1));
        Assert.Equal("2026-03-02", row.GetString(2));
        Assert.Equal("2026-06-19", row.GetString(3));
        Assert.Equal("50.00000000", row.GetString(4));
        Assert.Equal("5000.00000000", row.GetString(5));
        Assert.Equal(0L, row.GetInt64(6));
        Assert.Equal("called_away", row.GetString(7));
    }

    /// <summary>
    /// The positions the rebuild reconstructs, each taking effect when the
    /// account knew of it [D-W39].
    /// </summary>
    /// <remarks>
    /// <b>There is no <c>holding_shares</c> row, and that is the trial rather
    /// than the rebuild.</b> §6.3 writes its covered call on the session the
    /// assignment becomes known, and again on the session the first call expires,
    /// so bare shares are held between two closes and never across one. The
    /// shares are carried on the <c>short_call</c> rows, which is what the
    /// account held at each close. A lab that observes closes [D-W12] cannot
    /// claim to have seen a state that began and ended between two of them.
    /// <para>
    /// The two <c>short_call</c> rows are two different contracts, which is what
    /// makes them two rows rather than one span.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_rebuilt_positions_are_the_states_the_trial_passed_through()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var (trials, trialId) = WorkedExampleTrial(connection);

        trials.Rebuild(trialId, Seeded);

        using var read = connection.CreateCommand();
        read.CommandText =
            """
            SELECT state, effective_from, effective_to, shares, gross_basis
            FROM positions
            WHERE trial_id = $trial
            ORDER BY effective_from;
            """;
        read.Parameters.AddWithValue("$trial", trialId);

        var states = new List<(string State, string From, string? To, long Shares, string? Gross)>();
        using var row = read.ExecuteReader();

        while (row.Read())
        {
            states.Add((
                row.GetString(0),
                row.GetString(1),
                row.IsDBNull(2) ? null : row.GetString(2),
                row.GetInt64(3),
                row.IsDBNull(4) ? null : row.GetString(4)));
        }

        Assert.Equal(
            [
                ("short_put", "2026-03-02", "2026-04-20", 0L, null),
                ("short_call", "2026-04-20", "2026-05-18", 100L, "50.00000000"),
                ("short_call", "2026-05-18", "2026-06-22", 100L, "50.00000000"),
                ("cash", "2026-06-22", null, 0L, null),
            ],
            states);
    }

    /// <summary>
    /// A basis needing more places than the scale holds rounds at the bind rather
    /// than refusing.
    /// </summary>
    /// <remarks>
    /// <b>The defect 3.3's review found, on data the ledger's own scale admits.</b>
    /// Both bases are divisions, and a premium carrying eight places gives a net
    /// basis needing ten. They were bound through the refusing path, so a rebuild
    /// threw <c>ArgumentOutOfRangeException</c> rather than storing a rounded
    /// figure. <c>StoredParameters</c> had said since 0.4 that Phase 3 would need
    /// the rounding path called at the site.
    /// <para>
    /// The trial here is standard in every other way, so nothing but the premium's
    /// precision distinguishes it from the worked example's.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_basis_beyond_the_scale_is_rounded_rather_than_refused()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var put = Written(connection, new(2026, 4, 17), OptionRight.Put, 50.00m);
        var trials = new TrialStore(connection);
        var trialId = trials.OpenTrial("baseline", Symbol, new(2026, 3, 2), 50.00m);

        trials.Append(
            trialId,
            [
                new LedgerEntry(
                    new(2026, 3, 2), new(2026, 3, 2),
                    LedgerEntryKind.PremiumReceived, 0.12345678m, put),
                new LedgerEntry(
                    new(2026, 4, 17), new(2026, 4, 20),
                    LedgerEntryKind.Assignment, -5_000.00m, put),
            ]);

        trials.Rebuild(trialId, Seeded);

        using var read = connection.CreateCommand();
        read.CommandText =
            """
            SELECT gross_basis, net_basis
            FROM positions
            WHERE trial_id = $trial AND state = 'holding_shares';
            """;
        read.Parameters.AddWithValue("$trial", trialId);

        using var row = read.ExecuteReader();

        Assert.True(row.Read());
        Assert.Equal("50.00000000", row.GetString(0));

        // 50 - 0.12345678 / 100 is 49.9987654322, which the scale cannot hold.
        Assert.Equal("49.99876543", row.GetString(1));
    }

    /// <summary>
    /// The rebuild reconstructs an adjusted assignment the way the machine
    /// resolved it [D-W17].
    /// </summary>
    /// <remarks>
    /// The projection replays the same arithmetic and carried the same defect, so
    /// this is the store-side half of the review's first finding: an adjusted put
    /// pays the aggregate exercise price for the deliverable's worth of shares,
    /// and the basis is the quotient rather than the strike.
    /// </remarks>
    [Fact]
    public void An_adjusted_assignment_rebuilds_at_the_aggregate_exercise_price()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var adjusted = WrittenAdjusted(connection, new(2026, 4, 17), 50.00m, deliverable: 150);
        var trials = new TrialStore(connection);
        var trialId = trials.OpenTrial("baseline", Symbol, new(2026, 3, 2), 50.00m);

        trials.Append(
            trialId,
            [
                new LedgerEntry(
                    new(2026, 3, 2), new(2026, 3, 2),
                    LedgerEntryKind.PremiumReceived, 94.35m, adjusted),
                new LedgerEntry(
                    new(2026, 4, 17), new(2026, 4, 20),
                    LedgerEntryKind.Assignment, -5_000.00m, adjusted),
            ]);

        trials.Rebuild(trialId, Seeded);

        using var read = connection.CreateCommand();
        read.CommandText =
            """
            SELECT shares, gross_basis, net_basis
            FROM positions
            WHERE trial_id = $trial AND state = 'holding_shares';
            """;
        read.Parameters.AddWithValue("$trial", trialId);

        using var row = read.ExecuteReader();

        Assert.True(row.Read());
        Assert.Equal(150L, row.GetInt64(0));

        // 5,000 paid for 150 shares, less the 94.35 premium over the same 150.
        Assert.Equal("33.33333333", row.GetString(1));
        Assert.Equal("32.70433333", row.GetString(2));
    }

    /// <summary>
    /// A ledger that does not open with a sale cannot be replayed.
    /// </summary>
    /// <remarks>
    /// The vacuity guard on the rebuild. A replay that quietly started from
    /// whatever it found would reconstruct a trial that never happened, and the
    /// rebuild test compares the projection against itself, so it would agree.
    /// </remarks>
    [Fact]
    public void A_ledger_that_does_not_open_with_a_sale_is_refused()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => TrialProjection.Replay(
                [
                    new LedgerEntry(
                        new(2026, 3, 2), new(2026, 3, 2), LedgerEntryKind.Dividend, 44.00m),
                ],
                Seeded));

        Assert.Contains("cash-secured put", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_ledger_cannot_be_replayed()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => TrialProjection.Replay([], Seeded));

        Assert.Contains("no ledger entries", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// WORKED_EXAMPLE §6.3's trial, written to the ledger leg by leg.
    /// </summary>
    /// <remarks>
    /// The document's six legs, with the two dates each carries under [D-W39] and
    /// [D-W40]: an expiry resolved against a Friday's close is known and settles
    /// on the Monday, which is the shape §5's dates already have.
    /// </remarks>
    private static (TrialStore Trials, long TrialId) WorkedExampleTrial(SqliteConnection connection)
    {
        var put = Written(connection, new(2026, 4, 17), OptionRight.Put, 50.00m);
        var firstCall = Written(connection, new(2026, 5, 15), OptionRight.Call, 52.50m);
        var secondCall = Written(connection, new(2026, 6, 19), OptionRight.Call, 52.50m);

        var trials = new TrialStore(connection);
        var trialId = trials.OpenTrial("baseline", Symbol, new(2026, 3, 2), 50.00m);

        trials.Append(
            trialId,
            [
                new LedgerEntry(
                    new(2026, 3, 2), new(2026, 3, 2),
                    LedgerEntryKind.PremiumReceived, 94.35m, put),
                new LedgerEntry(
                    new(2026, 4, 17), new(2026, 4, 20),
                    LedgerEntryKind.Assignment, -5_000.00m, put),
                new LedgerEntry(
                    new(2026, 4, 20), new(2026, 4, 20),
                    LedgerEntryKind.PremiumReceived, 69.35m, firstCall),
                new LedgerEntry(
                    new(2026, 5, 15), new(2026, 5, 18),
                    LedgerEntryKind.ExpiredWorthless, 0m, firstCall),
                new LedgerEntry(
                    new(2026, 5, 18), new(2026, 5, 18),
                    LedgerEntryKind.PremiumReceived, 84.35m, secondCall),
                new LedgerEntry(
                    new(2026, 6, 19), new(2026, 6, 22),
                    LedgerEntryKind.CallAway, 5_250.00m, secondCall),
            ]);

        return (trials, trialId);
    }

    /// <summary>An adjusted put: the strike stands and the deliverable moves [D-W17].</summary>
    private static ContractIdentity WrittenAdjusted(
        SqliteConnection connection,
        DateOnly expiry,
        decimal strike,
        int deliverable)
    {
        var identity = ContractIdentity.Of(
            Symbol, expiry, OptionRight.Put, strike, deliverable);

        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO contracts (symbol, expiry, right, strike, multiplier, deliverable_shares)
            VALUES ($symbol, $expiry, 'put', $strike, 100, $deliverable);
            """;
        insert.Parameters.AddWithValue("$symbol", Symbol.Value);
        insert.Parameters.AddStored("$expiry", expiry);
        insert.Parameters.AddStored("$strike", strike);
        insert.Parameters.AddWithValue("$deliverable", deliverable);
        insert.ExecuteNonQuery();

        return identity;
    }

    private static ContractIdentity Written(
        SqliteConnection connection,
        DateOnly expiry,
        OptionRight right,
        decimal strike)
    {
        var identity = ContractIdentity.Of(Symbol, expiry, right, strike);

        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO contracts (symbol, expiry, right, strike, multiplier, deliverable_shares)
            VALUES ($symbol, $expiry, $right, $strike, 100, 100);
            """;
        insert.Parameters.AddWithValue("$symbol", Symbol.Value);
        insert.Parameters.AddStored("$expiry", expiry);
        insert.Parameters.AddStored("$right", right);
        insert.Parameters.AddStored("$strike", strike);
        insert.ExecuteNonQuery();

        return identity;
    }

    private static long CountOf(SqliteConnection connection, string table)
    {
        using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)count.ExecuteScalar()!;
    }

    private static SqliteConnection Migrated(TempStore store)
    {
        new MigrationRunner(store.Connections).Run(Instant);
        return store.Connections.Open(StoreAccess.Write);
    }
}
