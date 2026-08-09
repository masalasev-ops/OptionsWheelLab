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
/// The composition root on chains the worked example cannot supply.
/// </summary>
/// <remarks>
/// <b>Two properties that need a chain quoting more than one session.</b>
/// `WORKED_EXAMPLE`'s is a single snapshot, so under it every session after the
/// first offers nothing and every maker takes nothing. That makes it the right
/// oracle for what a maker chooses and the wrong one for what a run does over
/// time, and the two cases here are the ones it cannot reach: a maker opening a
/// second trial after its first returns to cash [D-W55], and a maker holding a
/// short the session does not quote [D-W49, `OpenShort`].
/// <para>
/// <b>The baseline's expiry window is widened rather than worked around.</b> Its
/// seeded band admits 30 to 60 days, so a trial that opens and closes inside a
/// short window cannot be opened at all. `Policy:Baseline:DteMin` is appended as a
/// later version, which is what a configuration change is [D-W26], rather than the
/// chain being stretched until the seeded band happens to fit.
/// </para>
/// </remarks>
public sealed class MakerRunTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");

    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A maker whose trial returns to cash opens another [D-W55].
    /// </summary>
    /// <remarks>
    /// Two snapshots, each offering one put fourteen days out, and an underlying
    /// above both strikes at both expiries. The first trial expires worthless and
    /// the maker is in cash on the next session it is asked, where the second
    /// snapshot gives it something to open. A run that held one trial would
    /// produce the first and refuse the second, which is what `TrialRun` does and
    /// what makes it right for a supplied sequence and wrong as a run's shape.
    /// </remarks>
    [Fact]
    public void A_maker_opens_a_second_trial_after_the_first_returns_to_cash()
    {
        using var scenario = new RunScenario(Sequential());

        var trials = Assert.Single(scenario.Results).Trials;

        Assert.Equal(2, trials.Count);
        Assert.All(trials, trial => Assert.Equal(TrialCloseKind.ExpiredWorthless, trial.State.CloseKind));

        // Two trials and not one twice: separate identifiers, separate opens.
        Assert.Equal(2, trials.Select(trial => trial.TrialId).Distinct().Count());
        Assert.Equal([new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 19)],
            trials.Select(trial => trial.State.OpenedOn));

        Assert.Equal(2, scenario.Count("trials"));
    }

    /// <summary>
    /// A maker holding a short the session does not quote takes nothing, and the
    /// run opens nothing in its place.
    /// </summary>
    /// <remarks>
    /// <b>The case the fallback would have turned into a second concurrent
    /// trial.</b> On 2026-03-09 the trial is short the 50.00 with seven days to
    /// run and the underlying at 45.00, so it is at the threshold and in the money
    /// [D-W54]; the session quotes a different put and not that one, so there is
    /// no ask. A run that handed the maker no short at all would send it down the
    /// opening path, where the session's other put is in band and would be sold
    /// against a position already held, which [D-W55] refuses by name.
    /// <para>
    /// The session offers something, which is what separates this from a session
    /// with no chain. Both end in the maker taking nothing and they are the same
    /// answer for different reasons.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_short_the_session_does_not_quote_leaves_the_run_holding_it()
    {
        using var scenario = new RunScenario(Unquoted());

        var trial = Assert.Single(Assert.Single(scenario.Results).Trials);

        Assert.Equal(1, scenario.Count("trials"));

        // Still short the leg it opened on the session it could not price.
        Assert.Equal(
            1,
            scenario.Count("decisions", "WHERE decision_date = '2026-03-09' AND kind = 'none'"));

        Assert.Equal(new DateOnly(2026, 3, 2), trial.State.OpenedOn);
    }

    /// <summary>
    /// The session that could not price the short still offered something.
    /// </summary>
    /// <remarks>
    /// The vacuity guard for the case above. A session offering nothing produces
    /// the same decision for a different reason, so without this the fixture would
    /// pass against a chain that simply ran out.
    /// </remarks>
    [Fact]
    public void The_session_that_could_not_price_the_short_offered_something()
    {
        using var scenario = new RunScenario(Unquoted());

        Assert.True(
            scenario.Count(
                "candidates",
                "JOIN feasible_sets USING (feasible_set_id) "
                + "WHERE feasible_sets.session_date = '2026-03-09'") > 0);
    }

    /// <summary>Two snapshots, each opening a trial that expires worthless.</summary>
    private static SyntheticChain Sequential() =>
        new(
            Symbol,
            [
                Bar(new(2026, 3, 2), 52.40m),
                Bar(new(2026, 3, 16), 52.00m),
                Bar(new(2026, 3, 19), 52.00m),
                Bar(new(2026, 4, 2), 52.00m),
            ],
            [
                Put(new(2026, 3, 2), strike: 45.00m, expiry: new(2026, 3, 16)),
                Put(new(2026, 3, 19), strike: 45.00m, expiry: new(2026, 4, 2)),
            ],
            [],
            []);

    /// <summary>
    /// A chain whose second snapshot quotes a put and not the one held.
    /// </summary>
    private static SyntheticChain Unquoted() =>
        new(
            Symbol,
            [
                Bar(new(2026, 3, 2), 52.40m),
                Bar(new(2026, 3, 9), 45.00m),
                Bar(new(2026, 3, 16), 45.00m),
            ],
            [
                Put(new(2026, 3, 2), strike: 50.00m, expiry: new(2026, 3, 16)),
                Put(new(2026, 3, 9), strike: 47.50m, expiry: new(2026, 3, 23)),
            ],
            [],
            []);

    private static UnderlyingBar Bar(DateOnly session, decimal close) =>
        new(Symbol, session, Close: close);

    private static ContractQuote Put(DateOnly session, decimal strike, DateOnly expiry) =>
        new(
            ContractIdentity.Of(Symbol, expiry, OptionRight.Put, strike),
            session,
            Bid: 0.95m,
            Ask: 1.01m,
            Delta: -0.24m);

    /// <summary>A store one maker has driven, held open for reading.</summary>
    private sealed class RunScenario : IDisposable
    {
        private readonly TempStore _store;
        private readonly SqliteConnection _connection;

        internal RunScenario(SyntheticChain chain)
        {
            _store = TempStore.Empty();
            new MigrationRunner(_store.Connections).Run(Seeded);

            _connection = _store.Connections.Open(StoreAccess.Write);

            var configuration = new ConfigWriter(_connection);
            configuration.AppendAll(SeedValues.All, Seeded);

            // A later version at the same instant, which is what as-of resolution
            // already does with equal timestamps [D-W26]. Fourteen days out is
            // outside the seeded band and inside this one.
            configuration.Append("Policy:Baseline:DteMin", "7", Seeded);

            new MembershipWriter(_connection).Append(
                Symbol, MembershipKind.Joined, new DateOnly(2026, 1, 2), Seeded);

            new ChainWriter(_connection).Ingest(chain, Seeded);

            var asOf = new AsOfConfiguration(_connection);

            Results = new MakerRun(
                new CandidateGenerator(
                    new AsOfMembership(_connection), new AsOfMarketData(_connection), asOf),
                asOf,
                new FillModel(asOf),
                // One session past the last bar. An expiry on the final session
                // settles onto the next one [D-W39, D-W40], and a calendar
                // session with no bar is a name that did not trade, which is
                // the case the calendar exists to distinguish [D-W46].
                SessionCalendar.Of(
                    [
                        .. chain.Bars.Select(bar => bar.SessionDate),
                        chain.Bars[^1].SessionDate.AddDays(3),
                    ]),
                new DecisionStore(_connection),
                new TrialStore(_connection)).Walk(
                    chain,
                    Symbol,
                    chain.Bars[0].SessionDate,
                    chain.Bars[^1].SessionDate,
                    [HighestCreditMaker.Baseline(asOf)],
                    Seeded);
        }

        internal IReadOnlyList<MakerRunResult> Results { get; }

        internal long Count(string table, string where = "")
        {
            using var read = _connection.CreateCommand();
            read.CommandText = $"SELECT COUNT(*) FROM {table} {where};";

            return (long)read.ExecuteScalar()!;
        }

        public void Dispose()
        {
            _connection.Dispose();
            _store.Dispose();
        }
    }
}
