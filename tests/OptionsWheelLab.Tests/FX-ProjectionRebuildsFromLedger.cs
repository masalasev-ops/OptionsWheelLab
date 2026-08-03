using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ProjectionRebuildsFromLedger: `trials` and `positions` discarded and
/// rebuilt from `ledger_entries` give the same rows, which is the condition on
/// rewriting them at all [D-W35].
/// </summary>
/// <remarks>
/// A projection may be rewritten only where a test discards it, rebuilds it from
/// its source, and gets the same rows. Without that test it is not a projection,
/// it is a rewritable table with a flattering name. This is that test.
/// <para>
/// <b>It also proves the ledger's kind vocabulary carries enough to rebuild
/// from, which nothing else checks.</b> A kind missing from [D-W48] is a fact the
/// rebuild cannot recover, and it would show here as a row that came back
/// different rather than as anything naming the cause.
/// </para>
/// <para>
/// <b>Every column is compared, not the ones a reader would think of.</b> Rows
/// are read as their full stored text, so a rebuild losing a close kind, a
/// nullable span end or a basis fails. Comparing chosen columns would make this
/// assert the reader's model of the projection rather than the projection.
/// </para>
/// <para>
/// <b>The tables are discarded between the two reads, and that is asserted
/// too.</b> A rebuild that skipped the delete and reinserted nothing would return
/// identical rows for the wrong reason, which is the shape of every check that
/// passes on an untouched subject.
/// </para>
/// </remarks>
public sealed class FX_ProjectionRebuildsFromLedger
{
    private static readonly DateTimeOffset Instant =
        new(2026, 7, 30, 9, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Discarded_and_rebuilt_the_projections_give_the_same_rows()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var (trials, trialId) = WorkedExampleTrial(connection);

        trials.Rebuild(trialId, Seeded);

        var before = Projection(connection);

        trials.Rebuild(trialId, Seeded);

        Assert.Equal(before, Projection(connection));
    }

    /// <summary>
    /// The rebuild really discards, so the comparison above is not between a
    /// table and itself.
    /// </summary>
    [Fact]
    public void The_projections_are_discarded_before_they_are_rebuilt()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var (trials, trialId) = WorkedExampleTrial(connection);

        trials.Rebuild(trialId, Seeded);

        // A row the rebuild did not write, which a discard removes and a
        // reinsert-only rebuild would leave behind.
        Execute(
            connection,
            """
            INSERT INTO positions (trial_id, state, effective_from, shares)
            VALUES (1, 'cash', '2020-01-01', 0);
            """);

        var planted = Projection(connection);

        trials.Rebuild(trialId, Seeded);

        Assert.NotEqual(planted, Projection(connection));
        Assert.DoesNotContain("2020-01-01", string.Join("|", Projection(connection)));
    }

    /// <summary>
    /// The rebuild is over a trial with something to recover, not an empty one.
    /// </summary>
    /// <remarks>
    /// The vacuity guard. A trial of one leg would rebuild identically under a
    /// vocabulary missing half its kinds, so the subject is the whole of §6.3:
    /// four states, an assignment known a session later, two covered calls, an
    /// expiry that paid nothing, and a call-away.
    /// </remarks>
    [Fact]
    public void The_rebuilt_trial_has_something_to_recover()
    {
        using var store = TempStore.Empty();
        using var connection = Migrated(store);

        var (trials, trialId) = WorkedExampleTrial(connection);

        trials.Rebuild(trialId, Seeded);

        Assert.Equal(6, trials.EntriesFor(trialId).Count);

        Assert.Equal(
            4,
            Projection(connection)
                .Count(row => row.StartsWith("position", StringComparison.Ordinal)));

        // Four distinct kinds across the six legs, three of them opening or
        // closing a position and one paying nothing at all.
        Assert.Equal(
            [
                LedgerEntryKind.PremiumReceived,
                LedgerEntryKind.Assignment,
                LedgerEntryKind.ExpiredWorthless,
                LedgerEntryKind.CallAway,
            ],
            trials.EntriesFor(trialId).Select(entry => entry.Kind).Distinct());
    }

    /// <summary>
    /// A ledger the rebuild cannot read stops rather than producing a projection.
    /// </summary>
    /// <remarks>
    /// The direction that would fail silently: a replay quietly starting from
    /// whatever it found would reconstruct a trial that never happened, and this
    /// fixture compares the projection against itself, so it would agree twice.
    /// </remarks>
    [Fact]
    public void A_ledger_that_does_not_open_with_a_sale_is_refused()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => TrialProjection.Replay(
                [
                    new LedgerEntry(
                        Opened, Opened, LedgerEntryKind.Dividend, 44.00m),
                ],
                Seeded));

        Assert.Contains("cash-secured put", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both projections as their full stored text, one row per line.
    /// </summary>
    private static IReadOnlyList<string> Projection(SqliteConnection connection)
    {
        var rows = new List<string>();

        rows.AddRange(Read(
            connection,
            "trial",
            """
            SELECT trial_id, maker_id, symbol, opened_on, closed_on, open_strike,
                   committed_capital, rolls_used, close_kind
            FROM trials
            ORDER BY trial_id;
            """));

        rows.AddRange(Read(
            connection,
            "position",
            """
            SELECT trial_id, state, effective_from, effective_to, shares,
                   gross_basis, net_basis, contract_id
            FROM positions
            ORDER BY trial_id, effective_from, state;
            """));

        return rows;
    }

    private static IReadOnlyList<string> Read(
        SqliteConnection connection,
        string label,
        string sql)
    {
        var rows = new List<string>();

        using var read = connection.CreateCommand();
        read.CommandText = sql;

        using var reader = read.ExecuteReader();

        while (reader.Read())
        {
            var values = new string[reader.FieldCount];

            for (var field = 0; field < reader.FieldCount; field++)
            {
                values[field] = reader.IsDBNull(field) ? "null" : reader.GetValue(field).ToString()!;
            }

            rows.Add($"{label}:{string.Join(",", values)}");
        }

        return rows;
    }

    /// <summary>WORKED_EXAMPLE §6.3's trial, written to the ledger leg by leg.</summary>
    private static (TrialStore Trials, long TrialId) WorkedExampleTrial(SqliteConnection connection)
    {
        var put = Written(connection, FirstExpiry, OptionRight.Put, 50.00m);
        var firstCall = Written(connection, SecondExpiry, OptionRight.Call, 52.50m);
        var secondCall = Written(connection, ThirdExpiry, OptionRight.Call, 52.50m);

        var trials = new TrialStore(connection);
        var trialId = trials.OpenTrial("baseline", Symbol, Opened, 50.00m);

        trials.Append(
            trialId,
            [
                new LedgerEntry(
                    Opened, Opened, LedgerEntryKind.PremiumReceived, 94.35m, put),
                new LedgerEntry(
                    FirstExpiry, MondayAfter, LedgerEntryKind.Assignment, -5_000.00m, put),
                new LedgerEntry(
                    MondayAfter, MondayAfter,
                    LedgerEntryKind.PremiumReceived, 69.35m, firstCall),
                new LedgerEntry(
                    SecondExpiry, SecondMonday,
                    LedgerEntryKind.ExpiredWorthless, 0m, firstCall),
                new LedgerEntry(
                    SecondMonday, SecondMonday,
                    LedgerEntryKind.PremiumReceived, 84.35m, secondCall),
                new LedgerEntry(
                    ThirdExpiry, ThirdMonday, LedgerEntryKind.CallAway, 5_250.00m, secondCall),
            ]);

        return (trials, trialId);
    }

    private static ContractIdentity Written(
        SqliteConnection connection,
        DateOnly expiry,
        OptionRight right,
        decimal strike)
    {
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

        return ContractIdentity.Of(Symbol, expiry, right, strike);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static SqliteConnection Migrated(TempStore store)
    {
        new MigrationRunner(store.Connections).Run(Instant);
        return store.Connections.Open(StoreAccess.Write);
    }
}
