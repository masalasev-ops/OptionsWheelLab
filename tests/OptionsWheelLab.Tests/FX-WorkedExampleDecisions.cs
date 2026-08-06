using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Decisions;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using static OptionsWheelLab.Tests.WorkedExampleOracle;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-WorkedExampleDecisions: the three makers reproduce §4's choices from §3's
/// feasible set, and the random maker's draw is reproducible from its seed alone.
/// </summary>
/// <remarks>
/// The ninth implemented fixture reading this document and the first to read §4,
/// which the corpus has treated as the oracle for decisions since v1.0.0 and
/// never once checked. Every earlier one reads the chain, the verdicts, the bases
/// or the ledger.
/// <para>
/// <b>It asserts reproduction, and reproduction is not evidence that any maker's
/// rule works.</b> A green run here means the corpus and the code agree about
/// three choices, which is worth having and is much less than it looks. The
/// limits are measured rather than guessed and each is stated below, because a
/// reader inferring from this fixture that the makers are tested would be wrong.
/// </para>
/// <para>
/// <b>The baseline's rule is undiscriminated.</b> §3's feasible set is the 45.00,
/// the 47.50 and the 50.00, and the baseline's band of 0.20 to 0.30 admits only
/// the 50.00 at delta 0.24. So "highest credit in band" and "the only one in
/// band" give the same answer, the maximum never compares two values, and the
/// tie-break is never reached. Any selection rule at all reproduces this row.
/// </para>
/// <para>
/// <b>The random maker is indistinguishable from one that draws nothing.</b> For
/// this session the derived seed's first draw is 136,963,533 of 2,147,483,647,
/// which is 6.4 percent of range, so the index is zero for every set size up to
/// fifteen. This document enumerates seven strikes, so <b>no set it could hold
/// would tell a uniform draw from a maker taking the first candidate.</b> That is
/// a property of the document rather than of its feasible set, and it is why
/// <see cref="MakerTests"/> pins the draw on two further sessions where the index
/// is two and one.
/// </para>
/// <para>
/// <b>Only the learner's row exercises anything.</b> Its band of 0.10 to 0.20
/// admits the 45.00 and the 47.50, whose bids differ, so the credit comparison
/// runs exactly once in this document and no tie is reached there either.
/// </para>
/// <para>
/// <b>The band's inclusive bound is load-bearing here, through the random
/// maker.</b> <c>Policy:Random:DeltaMin</c> is exactly 0.10 and the 45.00's delta
/// is exactly 0.10, so under an exclusive floor the draw set would be the 47.50
/// and the 50.00 and index zero would take the 47.50, contradicting §4. The
/// learner's floor sits on the same value and decides nothing there, which is why
/// an earlier reading of this called the convention unexercised.
/// </para>
/// </remarks>
public sealed class FX_WorkedExampleDecisions
{
    /// <summary>§1's existing state, which makes the per-name cap bind.</summary>
    private static readonly BookState Book = new(
        CommittedInName: 19_900.00m,
        CommittedTotal: 38_000.00m);

    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Recorded =
        new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);

    /// <summary>§4's three decisions, read out of the document.</summary>
    /// <remarks>
    /// Parsed from the table rather than transcribed, so a revision to that
    /// document reaches this fixture rather than leaving it asserting what the
    /// document used to say.
    /// </remarks>
    public static TheoryData<string, string> Decisions()
    {
        var data = new TheoryData<string, string>();

        foreach (var row in MakerTable())
        {
            data.Add(row[0], row[1]);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Decisions))]
    public void Each_maker_takes_the_strike_the_document_states(string maker, string choice)
    {
        using var store = Chain();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var decision = MakerNamed(maker, new AsOfConfiguration(connection)).Decide(
            Ticker.Normalise(Symbol),
            SnapshotDate,
            PositionState.Cash,
            Book,
            Offered(connection));

        Assert.Equal(DecisionKind.OpenPut, decision.Kind);
        Assert.Equal(StrikeIn(choice), decision.Chosen!.Strike);
    }

    /// <summary>
    /// The random maker's draw comes back the same from the seed alone.
    /// </summary>
    /// <remarks>
    /// Two makers built separately, each resolving the seed as of the session and
    /// deriving from it, rather than one asked twice. A maker holding a generator
    /// would pass the second and fail the first.
    /// </remarks>
    [Fact]
    public void The_random_draw_is_reproducible_from_the_seed_alone()
    {
        using var store = Chain();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var configuration = new AsOfConfiguration(connection);
        var offered = Offered(connection);

        var first = new RandomWithinBandMaker(configuration).Decide(
            Ticker.Normalise(Symbol), SnapshotDate, PositionState.Cash, Book, offered);

        var second = new RandomWithinBandMaker(configuration).Decide(
            Ticker.Normalise(Symbol), SnapshotDate, PositionState.Cash, Book, offered);

        Assert.Equal(first.Chosen, second.Chosen);

        // And it is the document's choice rather than merely a stable one.
        Assert.Equal(45.00m, first.Chosen!.Strike);
    }

    /// <summary>
    /// The set the makers were offered is §3's, so the choices are made from the
    /// document's feasible set and not from the whole chain.
    /// </summary>
    /// <remarks>
    /// Without this the three rows above could reproduce while every maker chose
    /// from seven strikes, which the random maker's band would in fact permit:
    /// applied to all seven, 0.10 to 0.35 also yields exactly these three, since
    /// the 40.00 and the 42.50 sit below its floor and the 52.50 and the 55.00
    /// above its ceiling. So §4's random row does not demonstrate that the gate
    /// ran, and this case is what does.
    /// </remarks>
    [Fact]
    public void The_makers_choose_from_the_documents_feasible_set()
    {
        using var store = Chain();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var feasible = Offered(connection)
            .Where(candidate => candidate.IsFeasible)
            .Select(candidate => candidate.Candidate.Quote.Contract.Strike)
            .Order()
            .ToList();

        Assert.Equal([45.00m, 47.50m, 50.00m], feasible);
    }

    private static IDecisionMaker MakerNamed(string maker, AsOfConfiguration configuration) =>
        maker switch
        {
            "Frozen baseline" => HighestCreditMaker.Baseline(configuration),
            "Random within band" => new RandomWithinBandMaker(configuration),
            "Learner" => HighestCreditMaker.Learner(configuration),
            _ => throw new ArgumentOutOfRangeException(
                nameof(maker),
                maker,
                "The document names a maker this lab does not have. [D-W4] fixes the arms at "
                + "three, so a fourth row is a decision rather than a fixture change."),
        };

    /// <summary>"50.00 put" as a strike.</summary>
    private static decimal StrikeIn(string choice) =>
        StoreDecimal.ParseStored(choice.Split(' ')[0]);

    private static IReadOnlyList<GatedCandidate> Offered(Microsoft.Data.Sqlite.SqliteConnection connection) =>
        new CandidateGenerator(
                new AsOfMembership(connection),
                new AsOfMarketData(connection),
                new AsOfConfiguration(connection))
            .GateFor(Ticker.Normalise(Symbol), SnapshotDate, PositionState.Cash, Book);

    private static TempStore Chain()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Seeded);

        using (var write = store.Connections.Open(StoreAccess.Write))
        {
            new ConfigWriter(write).AppendAll(SeedValues.All, Seeded);

            new MembershipWriter(write).Append(
                Ticker.Normalise(Symbol), MembershipKind.Joined, new DateOnly(2026, 1, 2), Seeded);

            // The bars are dropped for the reason 2.2's fixture states: §5's
            // closes run to June and the read is as of the snapshot date.
            new ChainWriter(write).Ingest(LoadChain() with { Bars = [] }, Recorded);
        }

        return store;
    }
}
