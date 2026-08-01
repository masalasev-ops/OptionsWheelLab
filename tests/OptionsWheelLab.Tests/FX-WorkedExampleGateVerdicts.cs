using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using static OptionsWheelLab.Tests.WorkedExampleOracle;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-WorkedExampleGateVerdicts: the worked example's chain gates to the
/// verdicts §3 states, under both constraint families.
/// </summary>
/// <remarks>
/// The fourth fixture pinning that document. 0.6's proves the file loads to
/// what §2 states, 1.4's proves the store returns it, 2.2's proves the
/// generator offers all seven, and this proves the gate reaches §3's verdicts.
/// §3 was written at 2.1, before any gate existed, so it is a prediction this
/// checkpoint either meets or contradicts.
/// <para>
/// <b>Nothing is stripped as of 2.4.</b> 2.3 built two thirds of the gate, so
/// this file removed the per-name cap from each expected verdict by name and
/// said which checkpoint owned it. The constraint exists now, so the phrase
/// joins the mapping and the two strikes failing on both grounds carry both.
/// </para>
/// <para>
/// <b>All three caps take their opening exposure from §1.</b> That document
/// states 19,900.00 committed in this name and 38,000.00 across all names and
/// derives both headrooms from them, and only the per-name figure reaches §3.
/// Supplying the second is what keeps the total cap from being exercised at
/// zero exposure, which is the vacuity in the opposite direction from an empty
/// book: not a cap never asked, but a cap whose bound is unreachable, and a
/// total cap wired to the wrong figure or to nothing reproduces §3 exactly.
/// </para>
/// <para>
/// The mapping from §3's phrases to reasons is asserted total over the
/// vocabulary, so a reason with no phrase fails here rather than passing
/// unnoticed. That is what keeps the document's wording authoritative without
/// making it a third representation of the vocabulary, and it is what fired the
/// moment 2.4 declared four reasons.
/// </para>
/// </remarks>
public sealed class FX_WorkedExampleGateVerdicts
{
    /// <summary>
    /// §3's own words for each reason its snapshot can produce.
    /// </summary>
    private static readonly (string Phrase, GateReason Reason)[] Phrases =
    [
        ("spread cap", GateReason.SpreadCap),
        ("premium floor", GateReason.PremiumFloor),
        ("delta ceiling", GateReason.DeltaCeiling),
        ("per-name cap", GateReason.PerNameCap),
    ];

    /// <summary>
    /// What §1 says the account already carries on the snapshot date.
    /// </summary>
    /// <remarks>
    /// No shares are held in this name, so there is no gross basis: §1's book is
    /// cash and committed puts, and the assignment that produces a basis is
    /// §6.3, seven weeks later.
    /// </remarks>
    private static readonly BookState Book = new(
        CommittedInName: 19_900.00m,
        CommittedTotal: 38_000.00m);

    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Recorded =
        new(SnapshotDate, new TimeOnly(21, 0), TimeSpan.Zero);

    [Fact]
    public void The_gate_reaches_the_verdicts_section_three_states()
    {
        var expected = GateTable();
        var actual = Gated();

        // A table that matched nothing would let every comparison below pass
        // while comparing nothing.
        Assert.NotEmpty(expected);
        Assert.NotEmpty(actual);
        Assert.Equal(expected.Count, actual.Count);

        foreach (var row in expected)
        {
            var strike = StoreDecimal.ParseStored(row[0]);

            Assert.Equal(ExpectedReasons(row[5]), actual[strike]);
        }
    }

    /// <summary>
    /// The feasible set §3 names, reached rather than restated.
    /// </summary>
    [Fact]
    public void The_feasible_set_is_the_three_strikes_section_three_names()
    {
        var feasible = Gated()
            .Where(entry => entry.Value.Count == 0)
            .Select(entry => entry.Key)
            .Order()
            .ToList();

        Assert.Equal([45.00m, 47.50m, 50.00m], feasible);
    }

    /// <summary>
    /// Every reason the vocabulary declares either has a phrase in this file or
    /// is one this snapshot cannot demonstrate.
    /// </summary>
    /// <remarks>
    /// Without this, a reason added to the vocabulary with no phrase would make
    /// every expected verdict quietly narrower, and the comparison above would
    /// still pass. It did its job at 2.4: the four portfolio reasons landed and
    /// this failed until each was accounted for.
    /// <para>
    /// Each entry below is undemonstrable for its own stated reason rather than
    /// by being left out. The crossed market, the expiry window and earnings
    /// clearance are §3's own three, being a chain with no crossed quote, one
    /// expiry and no report date. The total cap is undemonstrable because §1
    /// says it does not bind, and assignment stress because it is held equal to
    /// the total cap [CONFIG_REFERENCE]; the headroom test below is what
    /// exercises both. Gross basis is a call constraint and §3 enumerates puts.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_declared_reason_is_accounted_for()
    {
        GateReason[] undemonstrable =
        [
            GateReason.CrossedMarket,
            GateReason.ExpiryWindow,
            GateReason.EarningsClearance,
            GateReason.TotalCap,
            GateReason.AssignmentStress,
            GateReason.GrossBasis,
        ];

        var accounted = Phrases
            .Select(entry => entry.Reason)
            .Concat(undemonstrable)
            .ToHashSet();

        Assert.Equal(Enum.GetValues<GateReason>().ToHashSet(), accounted);
    }

    /// <summary>
    /// §1's claim that the per-name cap binds and the total does not, checked
    /// rather than read.
    /// </summary>
    /// <remarks>
    /// The headrooms are the figures §1 derives at its own line 26. Asserting
    /// them through the functions the constraint compares against is what
    /// separates a working total cap from one reading the per-name exposure, or
    /// from one not wired at all: both reproduce §3's verdicts exactly, because
    /// no candidate on this chain comes within 16,500.00 of the total headroom.
    /// </remarks>
    [Fact]
    public void Section_one_derives_both_headrooms_and_only_the_per_name_one_binds()
    {
        var caps = SeededBounds();

        Assert.Equal(5_100.00m, PortfolioConstraints.PerNameHeadroom(caps, Book));
        Assert.Equal(22_000.00m, PortfolioConstraints.TotalHeadroom(caps, Book));
        Assert.Equal(22_000.00m, PortfolioConstraints.AssignmentHeadroom(caps, Book));

        var reasons = Gated().SelectMany(entry => entry.Value).ToList();

        Assert.Contains(GateReason.PerNameCap, reasons);
        Assert.DoesNotContain(GateReason.TotalCap, reasons);
        Assert.DoesNotContain(GateReason.AssignmentStress, reasons);
    }

    /// <summary>
    /// The caps in force on the snapshot date, resolved rather than restated.
    /// </summary>
    private static PortfolioBounds SeededBounds()
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Seeded);

        using (var write = store.Connections.Open(StoreAccess.Write))
        {
            new ConfigWriter(write).AppendAll(SeedValues.All, Seeded);
        }

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        return PortfolioBounds.ResolveFor(new AsOfConfiguration(connection), SnapshotDate);
    }

    /// <summary>
    /// The reasons §3's Gate cell states, all of them.
    /// </summary>
    /// <remarks>
    /// <c>Single</c> rather than a filter, so a phrase this file does not know
    /// is a failure rather than a silent omission. That mattered while 2.3 was
    /// stripping one phrase by name and it still matters: §3 is authored prose
    /// and a revision naming a reason nothing maps should stop here.
    /// </remarks>
    private static IReadOnlyList<GateReason> ExpectedReasons(string cell)
    {
        if (cell.Equals("feasible", StringComparison.Ordinal))
        {
            return [];
        }

        return
        [
            .. cell["rejected:".Length..]
                .Split(',')
                .Select(part => part.Trim())
                .Select(phrase => Phrases.Single(entry =>
                    entry.Phrase.Equals(phrase, StringComparison.Ordinal)).Reason)
                .Order()
        ];
    }

    private static IReadOnlyDictionary<decimal, IReadOnlyList<GateReason>> Gated()
    {
        using var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Seeded);

        using (var write = store.Connections.Open(StoreAccess.Write))
        {
            new ConfigWriter(write).AppendAll(SeedValues.All, Seeded);

            new MembershipWriter(write).Append(
                Ticker.Normalise(Symbol),
                MembershipKind.Joined,
                new DateOnly(2026, 1, 2),
                Seeded);

            // The bars are dropped for the reason 2.2's fixture states: §5's
            // closes run to June and the read is as of the snapshot date, so
            // stamping them as observed in March would be a false observation.
            new ChainWriter(write).Ingest(LoadChain() with { Bars = [] }, Recorded);
        }

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        return new CandidateGenerator(
                new AsOfMembership(connection),
                new AsOfMarketData(connection),
                new AsOfConfiguration(connection))
            .GateFor(Ticker.Normalise(Symbol), SnapshotDate, PositionState.Cash, Book)
            .ToDictionary(
                candidate => candidate.Candidate.Quote.Contract.Strike,
                candidate => candidate.Reasons);
    }
}
