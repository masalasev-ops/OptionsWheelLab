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
/// FX-RollAtTheThreshold: a maker acts at seven days and not at eight, rolls when
/// a bound has not been reached, and closes when one has [D-W54].
/// </summary>
/// <remarks>
/// <b>The chain carries four snapshots, and which session a case runs on is the
/// case.</b> A single-snapshot chain cannot distinguish a maker declining to act
/// from a maker with nothing to act on, because both produce no decision, and
/// D-W54 makes that difference load-bearing: the first is the threshold and the
/// second is the conditionality that lets `WORKED_EXAMPLE` §6.3 reach its
/// assignment. Written inline on 2.2's precedent rather than as a scenario file,
/// because the sessions are the fixture's subject and a reader should not have to
/// open a second file to see them.
/// <para>
/// The short expires 2026-03-09 and is not one of the chain's contracts. A maker
/// is handed what buying it back costs rather than looking it up
/// [<see cref="OpenTrialContext"/>], so the leg being closed and the legs
/// available to roll into need not come from one snapshot.
/// </para>
/// <para>
/// Which session serves which case:
/// </para>
/// <list type="table">
///   <item>
///     <term>2026-03-01</term>
///     <description>
///       Eight days out. Quoted, in band, and in the money, so the only reason
///       nothing happens is the threshold.
///     </description>
///   </item>
///   <item>
///     <term>2026-03-02</term>
///     <description>
///       Seven days out. Rolls; closes at a roll bound, at a day bound, and when
///       the position is out of the money it is left alone.
///     </description>
///   </item>
///   <item>
///     <term>2026-03-03</term>
///     <description>
///       In band, and every bid below what the short costs to buy back. Serves
///       the debit condition against the same session's cheaper ask.
///     </description>
///   </item>
///   <item>
///     <term>2026-03-04</term>
///     <description>
///       Not quoted. No feasible set, so the position is left as it stands.
///     </description>
///   </item>
/// </list>
/// </remarks>
public sealed class FX_RollAtTheThreshold
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");

    /// <summary>The short the trial carries, eight days out on the first session.</summary>
    private static readonly DateOnly ShortExpiry = new(2026, 3, 9);

    /// <summary>What the sessions offer to roll into, in the baseline's band.</summary>
    private static readonly DateOnly RollExpiry = new(2026, 4, 17);

    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    /// <summary>The sessions the chain is quoted on, in order.</summary>
    private static readonly DateOnly[] Snapshots =
    [
        new(2026, 3, 1),
        new(2026, 3, 2),
        new(2026, 3, 3),
    ];

    /// <summary>
    /// A maker acts at seven days to expiry and not at eight.
    /// </summary>
    /// <remarks>
    /// The threshold's own boundary. One day earlier the position is not looked
    /// at whatever it is worth, which is the difference a stated number has to
    /// make to be a rule rather than a description.
    /// <para>
    /// <b>The feasible set is asserted before the decision is read.</b> An
    /// eight-days-out case whose session happened to offer nothing would pass
    /// while testing nothing, and that is not hypothetical: this fixture did
    /// exactly that until its snapshots were observed on their own sessions.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("2026-03-01", DecisionKind.None)]
    [InlineData("2026-03-02", DecisionKind.Roll)]
    public void A_maker_acts_at_seven_days_and_not_at_eight(string date, DecisionKind expected)
    {
        var session = StoreDate.ParseStored(date);

        using var store = Chained();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var offered = Offered(connection, session);

        Assert.Contains(offered, candidate => candidate.IsFeasible);

        var decision = HighestCreditMaker.Baseline(new AsOfConfiguration(connection)).Decide(
            Symbol,
            session,
            PositionState.ShortPut,
            BookState.Empty,
            offered,
            InTheMoney(session));

        Assert.Equal(expected, decision.Kind);
    }

    /// <summary>
    /// A short that would expire worthless is left to expire.
    /// </summary>
    /// <remarks>
    /// <b>The condition without which the rule breaks the wheel.</b> A maker
    /// acting on every position at the threshold would never hold one to expiry,
    /// so it would never be assigned, never write a covered call, and never reach
    /// the states this lab exists to measure [D-W54].
    /// </remarks>
    [Fact]
    public void A_short_out_of_the_money_is_left_to_expire()
    {
        var session = new DateOnly(2026, 3, 2);

        // A 50.00 put with the underlying at 52.40 is out of the money, on the
        // same session the in-the-money case rolls on.
        var decision = Decide(session, Trial(session, underlyingClose: 52.40m));

        Assert.Equal(DecisionKind.None, decision.Kind);
        Assert.Null(decision.Chosen);
    }

    /// <summary>
    /// A maker rolls into the highest credit its band admits.
    /// </summary>
    /// <remarks>
    /// The session offers two contracts in the baseline's band, so a roll that
    /// took either would be a roll; the assertion is that it took the one the
    /// maker's own opening rule would take [D-W54].
    /// </remarks>
    [Fact]
    public void A_roll_takes_the_candidate_the_opening_rule_would_take()
    {
        var session = new DateOnly(2026, 3, 2);

        var decision = Decide(session, InTheMoney(session));

        Assert.Equal(DecisionKind.Roll, decision.Kind);
        Assert.Equal(52.50m, decision.Chosen!.Strike);
        Assert.Equal(RollExpiry, decision.Chosen.Expiry);
    }

    /// <summary>
    /// A maker at a bound closes rather than rolling [D-W14].
    /// </summary>
    /// <remarks>
    /// Both bounds, on one session and against one chain, so the difference
    /// between rolling and closing is the bound and nothing else. The trial has
    /// two rolls and 120 days, which are the seeded values, and each case reaches
    /// one of them while leaving the other slack.
    /// </remarks>
    [Theory]
    [InlineData(2, 30)]
    [InlineData(0, 120)]
    public void A_maker_at_a_bound_closes(int rollsUsed, int daysOpen)
    {
        var session = new DateOnly(2026, 3, 2);

        var trial = InTheMoney(session) with
        {
            RollsUsed = rollsUsed,
            OpenedOn = session.AddDays(-daysOpen),
        };

        var decision = Decide(session, trial);

        Assert.Equal(DecisionKind.Close, decision.Kind);

        // The close names the leg being bought back, not a candidate.
        Assert.Equal(ShortExpiry, decision.Chosen!.Expiry);
    }

    /// <summary>
    /// A roll that would pay a net debit closes instead [D-W54].
    /// </summary>
    /// <remarks>
    /// <b>The pair is what makes this the debit condition rather than an empty
    /// band.</b> Both cases run on 2026-03-03, whose quotes are in band and admit
    /// a candidate; only the cost of buying the short back differs, and it decides
    /// whether the same session rolls or closes.
    /// </remarks>
    [Theory]
    [InlineData("1.01", DecisionKind.Close)]
    [InlineData("0.35", DecisionKind.Roll)]
    public void A_roll_paying_a_debit_closes_instead(string shortAsk, DecisionKind expected)
    {
        var session = new DateOnly(2026, 3, 3);

        var trial = InTheMoney(session) with { ShortAsk = StoreDecimal.ParseStored(shortAsk) };

        Assert.Equal(expected, Decide(session, trial).Kind);
    }

    /// <summary>
    /// A session offering nothing leaves the position rather than closing it.
    /// </summary>
    /// <remarks>
    /// Acting requires a feasible set [D-W52, D-W54], and a session with no chain
    /// has none. This is the path `WORKED_EXAMPLE` §6.3 takes, and it is why that
    /// trial reaches the assignment the document records despite its short being
    /// deep in the money at the threshold.
    /// </remarks>
    [Fact]
    public void A_session_offering_nothing_leaves_the_position()
    {
        var session = new DateOnly(2026, 3, 4);

        var decision = Decide(session, InTheMoney(session));

        Assert.Equal(DecisionKind.None, decision.Kind);
        Assert.Null(decision.Chosen);
    }

    /// <summary>
    /// Every maker acts on the same trigger, whatever its own band [D-W54].
    /// </summary>
    /// <remarks>
    /// The learner's band tops out at 0.20 delta and the baseline's starts at
    /// 0.20, so no contract on this session is in both. Each arm acts at the same
    /// threshold and each rolls into what its own band admits, which is the shape
    /// D-W4 requires: the makers differ in their bands and not in their rules. A
    /// trigger read off the acting maker's own band would let the learner defer
    /// assignment on a schedule the baseline could not.
    /// </remarks>
    [Theory]
    [InlineData("baseline", "52.50")]
    [InlineData("learner", "47.50")]
    public void Every_maker_acts_on_the_same_trigger(string arm, string strike)
    {
        var session = new DateOnly(2026, 3, 2);

        using var store = Chained();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var configuration = new AsOfConfiguration(connection);
        var maker = arm == "baseline"
            ? HighestCreditMaker.Baseline(configuration)
            : HighestCreditMaker.Learner(configuration);

        var decision = maker.Decide(
            Symbol,
            session,
            PositionState.ShortPut,
            BookState.Empty,
            Offered(connection, session),
            InTheMoney(session));

        Assert.Equal(DecisionKind.Roll, decision.Kind);
        Assert.Equal(StoreDecimal.ParseStored(strike), decision.Chosen!.Strike);
    }

    /// <summary>What the baseline maker decides on this session about this trial.</summary>
    private static MakerDecision Decide(DateOnly session, OpenTrialContext trial)
    {
        using var store = Chained();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        return HighestCreditMaker.Baseline(new AsOfConfiguration(connection)).Decide(
            Symbol,
            session,
            PositionState.ShortPut,
            BookState.Empty,
            Offered(connection, session),
            trial);
    }

    private static IReadOnlyList<GatedCandidate> Offered(
        SqliteConnection connection, DateOnly session) =>
        new CandidateGenerator(
            new AsOfMembership(connection),
            new AsOfMarketData(connection),
            new AsOfConfiguration(connection))
            .GateFor(Symbol, session, PositionState.ShortPut, BookState.Empty);

    /// <summary>A trial short the 50.00 put, in the money at 45.80.</summary>
    private static OpenTrialContext InTheMoney(DateOnly session) =>
        Trial(session, underlyingClose: 45.80m);

    private static OpenTrialContext Trial(DateOnly session, decimal underlyingClose) =>
        new(
            ContractIdentity.Of(Symbol, ShortExpiry, OptionRight.Put, 50.00m),
            ShortAsk: 1.01m,
            underlyingClose,
            OpenedOn: session.AddDays(-30),
            RollsUsed: 0,
            new TrialBounds(MaxRolls: 2, MaxTrialDays: 120));

    /// <summary>The store, with the four snapshots written as one chain.</summary>
    private static TempStore Chained()
    {
        var store = TempStore.Empty();

        new MigrationRunner(store.Connections).Run(Seeded);

        using (var write = store.Connections.Open(StoreAccess.Write))
        {
            new ConfigWriter(write).AppendAll(SeedValues.All, Seeded);

            new MembershipWriter(write).Append(
                Symbol, MembershipKind.Joined, new DateOnly(2026, 1, 2), Seeded);

            // One ingest per snapshot, each observed on its own session. A
            // single write stamped at the last of them would put every quote
            // beyond the as-of cutoff of every earlier session, and the
            // threshold case would then pass because its session saw no chain
            // rather than because the maker declined to act [D-W26].
            var chains = new ChainWriter(write);

            foreach (var snapshot in Snapshots)
            {
                chains.Ingest(
                    new SyntheticChain(Symbol, [], Quotes(snapshot), [], []),
                    new DateTimeOffset(snapshot.ToDateTime(new TimeOnly(21, 0)), TimeSpan.Zero));
            }
        }

        return store;
    }

    /// <summary>
    /// What one snapshot carries, and a fourth session deliberately absent.
    /// </summary>
    /// <remarks>
    /// The first two snapshots are identical, so the only thing separating them
    /// is the session a maker is asked about. The third's bids sit above
    /// `Gate:MinPremium` and below what the short costs, so its candidates are
    /// admitted and still cannot pay for the leg being closed.
    /// </remarks>
    private static IReadOnlyList<ContractQuote> Quotes(DateOnly session) =>
        session == new DateOnly(2026, 3, 3)
            ?
            [
                Quote(session, strike: 50.00m, bid: 0.40m, ask: 0.44m, delta: -0.18m),
                Quote(session, strike: 52.50m, bid: 0.60m, ask: 0.66m, delta: -0.28m),
            ]
            :
            [
                // In the learner's band and below the baseline's floor.
                Quote(session, strike: 47.50m, bid: 1.20m, ask: 1.30m, delta: -0.15m),
                Quote(session, strike: 50.00m, bid: 0.95m, ask: 1.01m, delta: -0.24m),
                Quote(session, strike: 52.50m, bid: 2.05m, ask: 2.20m, delta: -0.28m),
            ];

    private static ContractQuote Quote(
        DateOnly session, decimal strike, decimal bid, decimal ask, decimal delta) =>
        new(
            ContractIdentity.Of(Symbol, RollExpiry, OptionRight.Put, strike),
            session,
            bid,
            ask,
            Delta: delta);
}
