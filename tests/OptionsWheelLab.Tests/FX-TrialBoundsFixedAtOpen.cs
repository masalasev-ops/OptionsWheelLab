using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-TrialBoundsFixedAtOpen: a trial spanning a configuration change is bound by
/// the values in force when it opened, a trial opened after the change is bound by
/// the new ones, and a rebuild reaches the same verdict as the run [D-W53].
/// </summary>
/// <remarks>
/// <b>Three versions, because two cannot tell the two wrong answers apart.</b>
/// D-W53 excludes reading current configuration and reading what was in force on
/// the session being rebuilt, by name. With one change either wrong answer can
/// coincide with the other, so the store carries a version in force at the open, a
/// second landing between the open and the close, and a third landing after both
/// trials had closed. Only resolving as of the open gives the run's own answer.
/// <para>
/// <b>Nothing here shares a literal with the run.</b> Both machines take bounds
/// resolved from the store as of the session their trial opened, and the rebuild
/// resolves them again from the same store without being told. A fixture handing
/// the rebuild the constant the machine was built from would agree with itself
/// whatever the resolution did, which is how every caller of this path read until
/// this checkpoint.
/// </para>
/// <para>
/// <c>Trial:MaxRolls</c> is what varies, and it is chosen for what it does not
/// touch: <c>Trial:MaxTrialDays</c> sits in an invariant with <c>Gate:MaxDte</c>
/// [<see cref="ConfigurationInvariants"/>], so varying it would drag a second key
/// into every write and change what the gate admits alongside what the bound does.
/// </para>
/// <para>
/// <b>The two trials differ in when they opened and in nothing else.</b> Same
/// legs, same roll, same prices, and a close kind that comes back different
/// because the configuration in force at their opens was different.
/// </para>
/// </remarks>
public sealed class FX_TrialBoundsFixedAtOpen
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");

    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    /// <summary>The session the first trial opens, under one permitted roll.</summary>
    private static readonly DateOnly FirstOpen = new(2026, 3, 2);

    /// <summary>The session the second opens, under three.</summary>
    private static readonly DateOnly SecondOpen = new(2026, 3, 20);

    /// <summary>The session both trials roll on.</summary>
    private static readonly DateOnly Rolled = new(2026, 4, 8);

    /// <summary>The session the second trial is closed by choice on.</summary>
    private static readonly DateOnly Chosen = new(2026, 4, 17);

    private static readonly DateOnly FirstExpiry = new(2026, 5, 15);
    private static readonly DateOnly SecondExpiry = new(2026, 6, 19);

    private static readonly SessionCalendar Calendar = SessionCalendar.Of(
    [
        FirstOpen, SecondOpen, Rolled, Chosen,
        new(2026, 4, 20), FirstExpiry, SecondExpiry,
    ]);

    /// <summary>
    /// The three versions after the seed, and when each takes effect.
    /// </summary>
    /// <remarks>
    /// The seeded value is 2, so a rebuild reading the seed rather than resolving
    /// would also come back wrong for the first trial, which is a fourth wrong
    /// answer this arrangement happens to exclude.
    /// </remarks>
    private static readonly (DateTimeOffset At, string MaxRolls)[] Versions =
    [
        (new DateTimeOffset(2026, 2, 1, 21, 0, 0, TimeSpan.Zero), "1"),
        (new DateTimeOffset(2026, 3, 15, 21, 0, 0, TimeSpan.Zero), "3"),
        (new DateTimeOffset(2026, 5, 1, 21, 0, 0, TimeSpan.Zero), "5"),
    ];

    /// <summary>
    /// Each version is in force over the sessions between it and the next.
    /// </summary>
    /// <remarks>
    /// The arrangement stated as an assertion rather than left in a comment. Every
    /// claim below about which answer is right depends on these three being
    /// distinct on these three dates, and a seed that changed would otherwise make
    /// the later assertions pass for a reason nobody chose.
    /// </remarks>
    [Theory]
    [InlineData("2026-03-02", 1)]
    [InlineData("2026-04-08", 3)]
    [InlineData("2026-06-01", 5)]
    public void Each_version_is_in_force_over_its_own_sessions(string session, int maxRolls)
    {
        using var store = Written();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var bounds = TrialBounds.ResolveFor(
            new AsOfConfiguration(connection), StoreDate.ParseStored(session));

        Assert.Equal(maxRolls, bounds.MaxRolls);
    }

    /// <summary>
    /// A trial spanning a change is bound by the values it opened under, and one
    /// opened after the change by the new ones.
    /// </summary>
    /// <remarks>
    /// Both trials roll once. Under one permitted roll that is the bound, and the
    /// run closes the position at market on the session it rolled; under three it
    /// is not, and the trial is still open for a maker to close by choice. The
    /// close kind is therefore the difference the resolution makes, visible in the
    /// projection rather than argued about.
    /// </remarks>
    [Fact]
    public void A_trial_is_bound_by_the_configuration_it_opened_under()
    {
        using var store = Written();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var (trials, spanning, later) = TwoTrials(connection);

        trials.Rebuild(spanning, new AsOfConfiguration(connection));
        trials.Rebuild(later, new AsOfConfiguration(connection));

        Assert.Equal("closed_at_bound", CloseKindOf(connection, spanning));
        Assert.Equal("closed_by_choice", CloseKindOf(connection, later));
    }

    /// <summary>
    /// The rebuild reaches the verdict the run reached [D-W53].
    /// </summary>
    /// <remarks>
    /// The run's own close kinds are captured as the machine produced them and
    /// compared against what the projection recovered from the ledger alone. The
    /// ledger cannot carry the distinction: both trials end in a
    /// <c>bought_to_close</c> with nothing following [D-W48], so agreement here is
    /// the resolution working rather than the entry being read.
    /// </remarks>
    [Fact]
    public void The_rebuild_reaches_the_verdict_the_run_reached()
    {
        using var store = Written();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var (trials, spanning, later) = TwoTrials(connection);

        Assert.Equal(TrialCloseKind.ClosedAtBound, RunKinds[spanning]);
        Assert.Equal(TrialCloseKind.ClosedByChoice, RunKinds[later]);

        foreach (var (trialId, kind) in RunKinds)
        {
            trials.Rebuild(trialId, new AsOfConfiguration(connection));

            Assert.Equal(StoreTrialCloseKind.ToStored(kind), CloseKindOf(connection, trialId));
        }
    }

    /// <summary>
    /// A rebuild against current configuration disagrees with the run.
    /// </summary>
    /// <remarks>
    /// <b>The disagreement is made to happen rather than assumed impossible.</b>
    /// Without this the fixture above would pass against a rebuild that read
    /// anything at all, since a projection agreeing with the run proves nothing
    /// unless some other resolution would have disagreed. Five permitted rolls is
    /// what current configuration says, and under it the spanning trial's single
    /// roll is not a bound, so its close comes back as a choice the maker never
    /// made.
    /// </remarks>
    [Fact]
    public void A_rebuild_reading_current_configuration_would_disagree()
    {
        using var store = Written();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var (trials, spanning, _) = TwoTrials(connection);

        trials.Rebuild(spanning, new TrialBounds(MaxRolls: 5, MaxTrialDays: 120));

        Assert.Equal("closed_by_choice", CloseKindOf(connection, spanning));
        Assert.NotEqual(
            StoreTrialCloseKind.ToStored(RunKinds[spanning]),
            CloseKindOf(connection, spanning));
    }

    /// <summary>
    /// A run building one machine would bind both trials by one trial's values
    /// [D-W53, as amended].
    /// </summary>
    /// <remarks>
    /// <b>The run-level half of this decision, which the rebuild cases cannot
    /// reach.</b> Those compare a projection against a run; a run holding one
    /// machine across the trials it drives makes both halves wrong together, so
    /// they would agree and the property would be false. What is asserted here is
    /// that two trials opened either side of a configuration change reach
    /// different close kinds, which is only possible if each carries the bounds
    /// its own open resolved.
    /// <para>
    /// This is the same arrangement the cases above use, read from the other side:
    /// there the rebuild is asked which bounds a trial ran under, and here the run
    /// is.
    /// </para>
    /// </remarks>
    [Fact]
    public void Two_trials_opened_either_side_of_a_change_run_under_their_own_bounds()
    {
        using var store = Written();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var (_, spanning, later) = TwoTrials(connection);

        // One roll each and nothing else different. The first opened under one
        // permitted roll and closed at that bound; the second opened under three
        // and was still open for a maker to close.
        Assert.Equal(TrialCloseKind.ClosedAtBound, RunKinds[spanning]);
        Assert.Equal(TrialCloseKind.ClosedByChoice, RunKinds[later]);

        Assert.NotEqual(RunKinds[spanning], RunKinds[later]);
    }

    /// <summary>What the run said, per trial, for the rebuild to be compared with.</summary>
    private Dictionary<long, TrialCloseKind> RunKinds { get; } = [];

    /// <summary>
    /// Both trials, walked by a machine bound as of each one's open.
    /// </summary>
    /// <remarks>
    /// The composition D-W53 requires, done here because no composition root
    /// exists to do it: the bounds are resolved once, as of the session the trial
    /// opened, and the machine carries them for the trial's life.
    /// </remarks>
    private (TrialStore Trials, long Spanning, long Later) TwoTrials(SqliteConnection connection)
    {
        var trials = new TrialStore(connection);

        WriteContract(connection, FirstExpiry);
        WriteContract(connection, SecondExpiry);

        return (trials, Walk(connection, trials, FirstOpen), Walk(connection, trials, SecondOpen));
    }

    /// <summary>
    /// One trial: open, roll once, then close by whichever route its bounds leave
    /// open.
    /// </summary>
    private long Walk(SqliteConnection connection, TrialStore trials, DateOnly opened)
    {
        var configuration = new AsOfConfiguration(connection);
        var fills = new FillModel(configuration);

        var machine = new WheelStateMachine(
            Calendar,
            TrialBounds.ResolveFor(configuration, opened),
            CostBounds.ResolveFor(configuration, opened));

        var trialId = trials.OpenTrial("baseline", Symbol, opened, 50.00m);

        var opening = machine.OpenTrial(
            Put(FirstExpiry), fills.Sell(0.95m, opened), opened);

        var rolling = machine.Roll(
            opening.State,
            Rolled,
            fills.Buy(1.20m, Rolled),
            Put(SecondExpiry),
            fills.Sell(0.85m, Rolled));

        // The bound binds here or it does not, and which it is, is the subject.
        var advanced = machine.Advance(rolling.State, Facts(Rolled));

        var entries = new List<LedgerEntry>(
            [.. opening.Entries, .. rolling.Entries, .. advanced.Entries]);

        var state = advanced.State;

        if (!state.IsClosed)
        {
            var closed = machine.CloseByChoice(state, Facts(Chosen), fills.Buy(0.90m, Chosen));

            entries.AddRange(closed.Entries);
            state = closed.State;
        }

        trials.Append(trialId, entries);
        RunKinds[trialId] = state.CloseKind!.Value;

        return trialId;
    }

    /// <summary>
    /// A session's facts, carrying both sides of the short's quote [D-W49].
    /// </summary>
    /// <remarks>
    /// The ask is stated because a close at market pays it and refuses without it.
    /// The underlying close is above every strike here, so no expiry in this
    /// window resolves to an assignment and the only way either trial ends is the
    /// one under test.
    /// </remarks>
    private static SessionFacts Facts(DateOnly session) =>
        new(session, UnderlyingClose: 60.00m, Actions: [], ShortContractBid: 0.85m,
            ShortContractAsk: 0.90m);

    private static ContractIdentity Put(DateOnly expiry) =>
        ContractIdentity.Of(Symbol, expiry, OptionRight.Put, 50.00m);

    /// <summary>The store, migrated, seeded, and given its three later versions.</summary>
    private static TempStore Written()
    {
        var store = TempStore.Empty();

        new MigrationRunner(store.Connections).Run(Seeded);

        using var write = store.Connections.Open(StoreAccess.Write);

        var configuration = new ConfigWriter(write);
        configuration.AppendAll(SeedValues.All, Seeded);

        foreach (var (at, maxRolls) in Versions)
        {
            configuration.Append(ConfigKeys.TrialMaxRolls, maxRolls, at);
        }

        return store;
    }

    private static void WriteContract(SqliteConnection connection, DateOnly expiry)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO contracts (symbol, expiry, right, strike, multiplier, deliverable_shares)
            VALUES ($symbol, $expiry, $right, $strike, 100, 100);
            """;
        insert.Parameters.AddWithValue("$symbol", Symbol.Value);
        insert.Parameters.AddStored("$expiry", expiry);
        insert.Parameters.AddStored("$right", OptionRight.Put);
        insert.Parameters.AddStored("$strike", 50.00m);
        insert.ExecuteNonQuery();
    }

    private static string CloseKindOf(SqliteConnection connection, long trialId)
    {
        using var read = connection.CreateCommand();
        read.CommandText = "SELECT close_kind FROM trials WHERE trial_id = $trial;";
        read.Parameters.AddWithValue("$trial", trialId);

        return read.ExecuteScalar() as string ?? "null";
    }
}
