using System.Text;
using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-RunIsByteIdentical: a composed run produces byte-identical output across
/// two invocations.
/// </summary>
/// <remarks>
/// 0.5 restated the byte-identical definition of done as identical stored rows
/// because no run existed to make; 3.5 composes one, so the obligation is
/// discharged in its own terms at last.
/// <para>
/// <b>Compared as produced artefacts, never as a database file</b> [D-W28]. Two
/// SQLite files differ in page layout, free-list order and journal state for
/// reasons that have nothing to do with what was recorded, so comparing them
/// would fail on facts about the storage engine. What is compared is the ledger
/// and both projections, read back out of each store and rendered.
/// </para>
/// <para>
/// <b>Two stores, not one store twice.</b> Running twice into the same store
/// would compare a run against a run that started from its own output. Each
/// invocation gets a store migrated and seeded from nothing, which is what makes
/// the comparison about the run rather than about idempotence.
/// </para>
/// <para>
/// <b>Nothing in the run reads a clock, which is the finding rather than the
/// precaution.</b> Every date the loop uses is a session date, and the ledger's
/// two dates are both sessions [D-W39]. The clock reaches the store only through
/// the migration and seed stamps, so those are given a fixed instant here to keep
/// the setup identical, and the run itself would be unaffected by any instant at
/// all. That is asserted below rather than left as a claim.
/// </para>
/// </remarks>
public sealed class FX_RunIsByteIdentical
{
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Two_invocations_produce_the_same_artefact()
    {
        Assert.Equal(Invoke(Seeded), Invoke(Seeded));
    }

    /// <summary>
    /// The artefact is not empty, so the comparison is not between two nothings.
    /// </summary>
    /// <remarks>
    /// The vacuity guard every scanning check here carries. A run that produced
    /// no entries, or a read that returned none, would compare equal and assert
    /// nothing at all.
    /// </remarks>
    [Fact]
    public void The_artefact_carries_the_whole_trial()
    {
        var rendered = Invoke(Seeded);

        Assert.Contains("ledger:", rendered, StringComparison.Ordinal);
        Assert.Contains("trial:", rendered, StringComparison.Ordinal);
        Assert.Contains("position:", rendered, StringComparison.Ordinal);

        // Nine ledger rows, one trial and four positions, which is §6.3's trial.
        Assert.Equal(9, rendered.Split("ledger:").Length - 1);
        Assert.Equal(4, rendered.Split("position:").Length - 1);
    }

    /// <summary>
    /// A store seeded after the run's own dates refuses, rather than producing a
    /// different artefact.
    /// </summary>
    /// <remarks>
    /// <b>Written expecting the two to be equal, and they are not.</b> The run
    /// reads no clock, but the store it reads is not clock-free: `SeedCommand`
    /// stamps `set_at` from the wall clock, so a store seeded in 2027 has no
    /// commission in force on a 2026 session and `CostBounds` cannot resolve.
    /// That is the carried obligation owed at Phase 9, reached here from
    /// determinism rather than from a walk-forward.
    /// <para>
    /// <b>The answer is better than equality would have been.</b> It does not
    /// produce a different artefact quietly; it stops [D-W37]. So a run's output
    /// cannot silently depend on when its store was seeded, and the two
    /// invocations compared above differ in nothing at all rather than in nothing
    /// observed. A resolution rule that defaulted would have made this test pass
    /// and the property false.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_store_seeded_after_the_runs_dates_stops_rather_than_differing()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => Invoke(new DateTimeOffset(2027, 5, 4, 9, 30, 0, TimeSpan.Zero)));

        Assert.Contains("Costs:", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2026-03-02", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("D-W37", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>One whole invocation, rendered.</summary>
    private static string Invoke(DateTimeOffset instant)
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(instant);

        using var connection = store.Connections.Open(StoreAccess.Write);
        new ConfigWriter(connection).AppendAll(SeedValues.All, instant);

        var put = Written(connection, FirstExpiry, OptionRight.Put, 50.00m);
        var firstCall = Written(connection, SecondExpiry, OptionRight.Call, 52.50m);
        var secondCall = Written(connection, ThirdExpiry, OptionRight.Call, 52.50m);

        var run = new TrialRun(Machine(), new FillModel(new AsOfConfiguration(connection)), Calendar);

        var result = run.Walk(
            Chain(),
            Opened,
            ThirdMonday,
            [
                new OpenPut(Opened, put, Bid: 0.95m),
                new WriteCoveredCall(MondayAfter, firstCall, Bid: 0.70m),
                new WriteCoveredCall(SecondMonday, secondCall, Bid: 0.85m),
            ]);

        var trials = new TrialStore(connection);
        var trialId = trials.OpenTrial("baseline", Symbol, result.State.OpenedOn, 50.00m);

        trials.Append(trialId, result.Entries);
        trials.Rebuild(trialId, TrialScenario.Seeded);

        return Render(connection, trials, trialId);
    }

    /// <summary>
    /// The ledger and both projections as text, read back out of the store.
    /// </summary>
    private static string Render(SqliteConnection connection, TrialStore trials, long trialId)
    {
        var rendered = new StringBuilder();

        foreach (var entry in trials.EntriesFor(trialId))
        {
            rendered.Append(
                $"ledger:{entry.EntryDate:yyyy-MM-dd},{entry.KnownOn:yyyy-MM-dd},"
                + $"{StoreLedgerEntryKind.ToStored(entry.Kind)},"
                + $"{StoreDecimal.ToStored(entry.Amount)},{entry.Contract},{entry.Note}\n");
        }

        Read(connection, rendered, "trial",
            """
            SELECT maker_id, symbol, opened_on, closed_on, open_strike, committed_capital,
                   rolls_used, close_kind
            FROM trials ORDER BY trial_id;
            """);

        Read(connection, rendered, "position",
            """
            SELECT state, effective_from, effective_to, shares, gross_basis, net_basis
            FROM positions ORDER BY effective_from, state;
            """);

        return rendered.ToString();
    }

    private static void Read(
        SqliteConnection connection,
        StringBuilder rendered,
        string label,
        string sql)
    {
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

            rendered.Append($"{label}:{string.Join(",", values)}\n");
        }
    }

    /// <summary>§5's closes, over the six sessions the trial touches.</summary>
    private static SyntheticChain Chain() =>
        new(
            Symbol,
            [
                new UnderlyingBar(Symbol, Opened, Close: 52.40m),
                new UnderlyingBar(Symbol, FirstExpiry, Close: 48.90m),
                new UnderlyingBar(Symbol, MondayAfter, Close: 48.95m),
                new UnderlyingBar(Symbol, SecondExpiry, Close: 51.20m),
                new UnderlyingBar(Symbol, SecondMonday, Close: 51.30m),
                new UnderlyingBar(Symbol, ThirdExpiry, Close: 53.40m),
            ],
            [],
            [],
            []);

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
}
