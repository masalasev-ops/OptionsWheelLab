using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Decisions;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-MakersDriveTheRun: three makers driving one chain produce three trials,
/// three ledgers and one decision record, with no contract supplied by the test
/// [D-W55, D-W56].
/// </summary>
/// <remarks>
/// <b>The test names no contract, and that is the property.</b> Every earlier
/// fixture that produced a ledger handed the run its choices, so what it asserted
/// was the machine rather than the choosing. Here the run is given a chain, a
/// window and three makers, and every contract in the result was selected by a
/// maker out of a set the gate produced.
/// <para>
/// <b>What the worked example can demonstrate, and where it stops.</b> §6.1 and
/// §6.2 reproduce end to end: the random maker's 45.00 and the learner's 47.50
/// open on 2026-03-02 and expire worthless on 2026-04-17. §6.3 reproduces to its
/// assignment and no further, because §2 quotes no calls on any session and the
/// two 52.50 calls its leg table names were never in the chain. Adding them would
/// put quotes in the oracle that the oracle does not claim
/// [`WORKED_EXAMPLE.md`], so the baseline's trial ends the run holding shares,
/// which is a real state and not a failure.
/// </para>
/// <para>
/// <b>The bars are the run's sessions and the chain is one snapshot.</b> That
/// combination is what makes every session after the first offer nothing, so the
/// makers take nothing and the trials run to expiry, and it is why this fixture
/// cannot exercise a second trial. The multi-trial case has its own chain.
/// </para>
/// </remarks>
public sealed class FX_MakersDriveTheRun
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");

    private static readonly DateOnly Opened = new(2026, 3, 2);
    private static readonly DateOnly Last = new(2026, 6, 19);

    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Recorded =
        new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// §4's three choices, reached by three makers driving the run.
    /// </summary>
    /// <remarks>
    /// The same three strikes FX-WorkedExampleDecisions asserts against one gate
    /// evaluation. What is new is that nothing here calls the gate or the maker:
    /// the run does both, and the strike is read back out of the trial it opened.
    /// </remarks>
    [Theory]
    [InlineData(MakerIds.Baseline, "50.00")]
    [InlineData(MakerIds.Random, "45.00")]
    [InlineData(MakerIds.Learner, "47.50")]
    public void Each_maker_opens_the_trial_section_4_gives_it(string makerId, string strike)
    {
        using var scenario = Driven();

        var trial = Assert.Single(scenario.For(makerId).Trials);

        // Read off the leg that opened it, since a closed trial holds no contract.
        Assert.Equal(StoreDecimal.ParseStored(strike), trial.Entries[0].Contract!.Strike);
        Assert.Equal(Opened, trial.State.OpenedOn);
    }

    /// <summary>
    /// The two trials §6.1 and §6.2 record expire worthless and return to cash.
    /// </summary>
    /// <remarks>
    /// Their totals are the document's: 29.35 and 54.35, being the bid less the
    /// commission [D-W50], with nothing after because the put expired below its
    /// strike. This is the first time either has been produced by anything, since
    /// they are the totals of the two trials no maker had opened.
    /// </remarks>
    [Theory]
    [InlineData(MakerIds.Random, "29.35")]
    [InlineData(MakerIds.Learner, "54.35")]
    public void The_two_trials_that_expire_worthless_total_what_section_6_states(
        string makerId, string total)
    {
        using var scenario = Driven();

        var trial = Assert.Single(scenario.For(makerId).Trials);

        Assert.Equal(TrialCloseKind.ExpiredWorthless, trial.State.CloseKind);
        Assert.Equal(StoreDecimal.ParseStored(total), trial.Entries.Sum(entry => entry.Amount));
    }

    /// <summary>
    /// §6.3's trial reaches its assignment and stops there, holding shares.
    /// </summary>
    /// <remarks>
    /// <b>Where the oracle runs out, stated rather than worked around.</b> The
    /// covered calls §6.3 lists are legs and never quotes, so no maker can choose
    /// them. The trial holds 100 shares from the session after the assignment
    /// [D-W39] to the end of the window, with no close kind, which is what an open
    /// position is.
    /// </remarks>
    [Fact]
    public void The_third_trial_reaches_its_assignment_and_holds_shares()
    {
        using var scenario = Driven();

        var trial = Assert.Single(scenario.For(MakerIds.Baseline).Trials);

        Assert.Equal(PositionState.HoldingShares, trial.State.State);
        Assert.Null(trial.State.CloseKind);
        Assert.Equal(100, trial.State.Shares);

        // The premium, its commission, and the assignment. No assignment fee row,
        // because the seeded fee is 0.00 and a cost of zero is not a cost [D-W50].
        Assert.Equal(
            [
                LedgerEntryKind.PremiumReceived,
                LedgerEntryKind.Commission,
                LedgerEntryKind.Assignment,
            ],
            trial.Entries.Select(entry => entry.Kind));
    }

    /// <summary>
    /// One decision record, and every session a maker was asked is in it [D-W5].
    /// </summary>
    /// <remarks>
    /// Seven sessions and three makers is twenty-one decisions, of which three
    /// take a contract and eighteen take nothing. Taking nothing is a choice and
    /// is scored, so a run that recorded only the sessions something happened on
    /// would leave the scorer unable to tell a maker that declined from a maker
    /// that was never asked.
    /// </remarks>
    [Fact]
    public void Every_session_a_maker_was_asked_is_in_the_record()
    {
        using var scenario = Driven();

        Assert.Equal(21, Count(scenario.Connection, "decisions"));
        Assert.Equal(
            3,
            Count(scenario.Connection, "decisions", "WHERE chosen_candidate_id IS NOT NULL"));
    }

    /// <summary>
    /// One feasible set per symbol, session and right, however many makers act
    /// against it [D-W52].
    /// </summary>
    /// <remarks>
    /// On the opening session all three makers are in cash and present one set.
    /// After the assignment the baseline is offered calls where the other two are
    /// in cash, so that session carries two sets, which is the bound D-W52 states
    /// rather than one set per maker.
    /// </remarks>
    [Fact]
    public void The_makers_share_one_set_per_session_and_right()
    {
        using var scenario = Driven();

        // Eleven, not twenty-one. Three sessions where all three makers are in
        // cash or short a put carry one set each; the four after the assignment
        // carry two, since the baseline is offered calls and the other two puts.
        Assert.Equal(11, Count(scenario.Connection, "feasible_sets"));

        Assert.Equal(
            1, Count(scenario.Connection, "feasible_sets", "WHERE session_date = '2026-03-02'"));

        Assert.Equal(
            2, Count(scenario.Connection, "feasible_sets", "WHERE session_date = '2026-04-20'"));
    }

    /// <summary>
    /// An opening decision names the trial it opened [D-W56].
    /// </summary>
    /// <remarks>
    /// The ordering is the whole of it: the trial is minted and the decision
    /// records its identifier, where a decision written first would carry a null
    /// no later write could fill, `decisions` being append-only [D-W3].
    /// </remarks>
    [Fact]
    public void An_opening_decision_names_the_trial_it_opened()
    {
        using var scenario = Driven();

        Assert.Equal(
            0,
            Count(
                scenario.Connection,
                "decisions",
                "WHERE kind = 'open_put' AND trial_id IS NULL"));

        foreach (var result in scenario.Results)
        {
            var trial = Assert.Single(result.Trials);

            Assert.Equal(
                1,
                Count(
                    scenario.Connection,
                    "decisions",
                    $"WHERE kind = 'open_put' AND trial_id = {trial.TrialId} "
                    + $"AND maker_id = '{result.MakerId}'"));
        }
    }

    /// <summary>The run, walked once, with its store kept open to read back.</summary>
    private static DrivenScenario Driven() => new();

    private static long Count(SqliteConnection connection, string table, string where = "") =>
        Scalar(connection, $"SELECT COUNT(*) FROM {table} {where};");

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var read = connection.CreateCommand();
        read.CommandText = sql;

        return (long)read.ExecuteScalar()!;
    }

    /// <summary>A store the run has walked, held open for reading.</summary>
    private sealed class DrivenScenario : IDisposable
    {
        private readonly TempStore _store;

        internal DrivenScenario()
        {
            _store = TempStore.Empty();
            new MigrationRunner(_store.Connections).Run(Seeded);

            Connection = _store.Connections.Open(StoreAccess.Write);

            new ConfigWriter(Connection).AppendAll(SeedValues.All, Seeded);

            new MembershipWriter(Connection).Append(
                Symbol, MembershipKind.Joined, new DateOnly(2026, 1, 2), Seeded);

            var chain = WorkedExampleOracle.LoadChain();

            new ChainWriter(Connection).Ingest(chain, Recorded);

            var configuration = new AsOfConfiguration(Connection);

            var run = new MakerRun(
                new CandidateGenerator(
                    new AsOfMembership(Connection), new AsOfMarketData(Connection), configuration),
                configuration,
                new FillModel(configuration),
                SessionCalendar.Of(chain.Bars.Select(bar => bar.SessionDate)),
                new DecisionStore(Connection),
                new TrialStore(Connection));

            Results = run.Walk(
                chain,
                Symbol,
                Opened,
                Last,
                [
                    HighestCreditMaker.Baseline(configuration),
                    new RandomWithinBandMaker(configuration),
                    HighestCreditMaker.Learner(configuration),
                ],
                Recorded);
        }

        internal SqliteConnection Connection { get; }

        internal IReadOnlyList<MakerRunResult> Results { get; }

        internal MakerRunResult For(string makerId) =>
            Results.Single(result => result.MakerId == makerId);

        public void Dispose()
        {
            Connection.Dispose();
            _store.Dispose();
        }
    }
}
