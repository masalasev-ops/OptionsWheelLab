using System.Text;
using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;
using OptionsWheelLab.Core.Decisions;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;

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
/// <b>Driven by makers from 4.5, and the change is not mechanical.</b> Until then
/// this walked a supplied sequence naming three contracts and their bids, so what
/// it asserted was that the machine is deterministic given the same choices. A
/// seeded maker is the part that was missing, and the random control is the part
/// that could have made it false. The three inline contracts are gone: a fixture
/// that hands a maker its answer asserts nothing about choosing, which 4.5's own
/// definition of done forbids.
/// </para>
/// <para>
/// <b>The decision record is in the comparison now.</b> It did not exist when this
/// rendering was written, and it is the primary artefact of the system [D-W3], so
/// a determinism check over the ledger alone would have left the thing that
/// matters most unchecked. It is also where a non-deterministic run would show
/// first, since it carries a row for every session every maker was asked.
/// </para>
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

    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");

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

        Assert.Contains("decision:", rendered, StringComparison.Ordinal);

        // Three trials, one per maker, and the twenty-one decisions seven
        // sessions and three makers produce.
        Assert.Equal(3, rendered.Split("trial:").Length - 1);
        Assert.Equal(21, rendered.Split("decision:").Length - 1);

        // Nine: three rows each. The two that expired worthless carry the
        // premium, its commission and the expiry, because an expiry that pays
        // nothing is still an entry [D-W48]; the third carries the premium, its
        // commission and the assignment.
        Assert.Equal(9, rendered.Split("ledger:").Length - 1);
    }

    /// <summary>
    /// The random control is in the run, so a draw is part of what is compared.
    /// </summary>
    /// <remarks>
    /// <b>The vacuity guard this fixture needed once a maker drove it.</b> Two
    /// invocations of a run whose every choice was deterministic by construction
    /// would agree whether or not the seeded generator worked, so the assertion
    /// that matters is that the arm whose choice comes from a draw took a
    /// contract and took the same one twice [D-W51].
    /// </remarks>
    [Fact]
    public void The_drawn_choice_is_part_of_what_is_compared()
    {
        var rendered = Invoke(Seeded);

        Assert.Contains($"decision:{MakerIds.Random},", rendered, StringComparison.Ordinal);

        // 45.00 is the strike §4's draw takes, and it reaches the ledger only
        // because the draw put it there.
        Assert.Contains("WDGT 2026-04-17 put 45.00", rendered, StringComparison.Ordinal);
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

        // The key it names moved from Costs: to Policy: when a maker began
        // driving the run, because the maker resolves its band before anything
        // prices a fill. Which key is first is not the property; that the run
        // stops rather than producing a different artefact is.
        Assert.Contains("Policy:", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2026-03-02", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("D-W37", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>One whole invocation, rendered.</summary>
    /// <remarks>
    /// The chain is the worked example's, so the makers choose out of §2's own
    /// quotes rather than out of anything this fixture invented.
    /// </remarks>
    private static string Invoke(DateTimeOffset instant)
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(instant);

        using var connection = store.Connections.Open(StoreAccess.Write);
        new ConfigWriter(connection).AppendAll(SeedValues.All, instant);

        new MembershipWriter(connection).Append(
            Symbol, MembershipKind.Joined, new DateOnly(2026, 1, 2), instant);

        var chain = WorkedExampleOracle.LoadChain();

        new ChainWriter(connection).Ingest(chain, instant);

        var configuration = new AsOfConfiguration(connection);
        var trials = new TrialStore(connection);

        var driven = new MakerRun(
            new CandidateGenerator(
                new AsOfMembership(connection), new AsOfMarketData(connection), configuration),
            configuration,
            new FillModel(configuration),
            SessionCalendar.Of(chain.Bars.Select(bar => bar.SessionDate)),
            new DecisionStore(connection),
            trials).Walk(
                chain,
                Symbol,
                chain.Bars[0].SessionDate,
                chain.Bars[^1].SessionDate,
                [
                    HighestCreditMaker.Baseline(configuration),
                    new RandomWithinBandMaker(configuration),
                    HighestCreditMaker.Learner(configuration),
                ],
                instant);

        foreach (var trial in driven.SelectMany(result => result.Trials))
        {
            trials.Rebuild(trial.TrialId, configuration);
        }

        return Render(connection, trials, driven);
    }

    /// <summary>
    /// The ledger and both projections as text, read back out of the store.
    /// </summary>
    private static string Render(
        SqliteConnection connection,
        TrialStore trials,
        IReadOnlyList<MakerRunResult> driven)
    {
        var rendered = new StringBuilder();

        foreach (var trial in driven.SelectMany(result => result.Trials).OrderBy(t => t.TrialId))
        {
            foreach (var entry in trials.EntriesFor(trial.TrialId))
            {
                rendered.Append(
                    $"ledger:{trial.TrialId},{entry.EntryDate:yyyy-MM-dd},"
                    + $"{entry.KnownOn:yyyy-MM-dd},"
                    + $"{StoreLedgerEntryKind.ToStored(entry.Kind)},"
                    + $"{StoreDecimal.ToStored(entry.Amount)},{entry.Contract},{entry.Note}\n");
            }
        }

        // The primary artefact [D-W3], ordered by what it is about rather than by
        // the order it happened to be written in.
        Read(connection, rendered, "decision",
            """
            SELECT decisions.maker_id, decisions.decision_date, decisions.kind,
                   decisions.trial_id, decisions.policy_version,
                   contracts.expiry, contracts.right, contracts.strike
            FROM decisions
            LEFT JOIN candidates ON candidates.candidate_id = decisions.chosen_candidate_id
            LEFT JOIN contracts ON contracts.contract_id = candidates.contract_id
            ORDER BY decisions.decision_date, decisions.maker_id;
            """);

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
}
