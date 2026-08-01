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
/// verdicts §3 states, under the four contract constraints.
/// </summary>
/// <remarks>
/// The fourth fixture pinning that document. 0.6's proves the file loads to
/// what §2 states, 1.4's proves the store returns it, 2.2's proves the
/// generator offers all seven, and this proves the gate reaches §3's verdicts.
/// §3 was written at 2.1, before any gate existed, so it is a prediction this
/// checkpoint either meets or contradicts.
/// <para>
/// <b>The per-name cap is stripped from each expected verdict, because it is
/// 2.4's.</b> §3 states the whole gate's verdict and 2.3 builds two thirds of
/// it, so the two strikes failing on both grounds carry the delta reason here
/// and gain the capital reason at 2.4. Stripping is done by name against a
/// declared phrase rather than by dropping unrecognised text, so a reason this
/// checkpoint should have produced cannot vanish into the gap.
/// </para>
/// <para>
/// The mapping from §3's phrases to reasons is asserted total over the
/// vocabulary, so a reason with no phrase fails here rather than passing
/// unnoticed. That is what keeps the document's wording authoritative without
/// making it a third representation of the vocabulary.
/// </para>
/// </remarks>
public sealed class FX_WorkedExampleGateVerdicts
{
    /// <summary>
    /// §3's own words for each reason 2.3 can produce, and the one it cannot.
    /// </summary>
    /// <remarks>
    /// The expiry window and earnings clearance have no phrase because §3 states
    /// that one snapshot cannot demonstrate them, which is why they are absent
    /// from that table rather than missing from it.
    /// </remarks>
    private static readonly (string Phrase, GateReason Reason)[] Phrases =
    [
        ("spread cap", GateReason.SpreadCap),
        ("premium floor", GateReason.PremiumFloor),
        ("delta ceiling", GateReason.DeltaCeiling),
    ];

    /// <summary>The reason 2.4 adds, named so it can be stripped by name.</summary>
    private const string PerNameCap = "per-name cap";

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
    /// is one §3 states it cannot demonstrate.
    /// </summary>
    /// <remarks>
    /// Without this, a reason added to the vocabulary with no phrase would make
    /// every expected verdict quietly narrower, and the comparison above would
    /// still pass.
    /// </remarks>
    [Fact]
    public void Every_declared_reason_is_accounted_for()
    {
        GateReason[] undemonstrable =
        [
            GateReason.CrossedMarket,
            GateReason.ExpiryWindow,
            GateReason.EarningsClearance,
        ];

        var accounted = Phrases
            .Select(entry => entry.Reason)
            .Concat(undemonstrable)
            .ToHashSet();

        Assert.Equal(Enum.GetValues<GateReason>().ToHashSet(), accounted);
    }

    /// <summary>
    /// The reasons §3's Gate cell states, less the one 2.4 owns.
    /// </summary>
    private static IReadOnlyList<GateReason> ExpectedReasons(string cell)
    {
        if (cell.Equals("feasible", StringComparison.Ordinal))
        {
            return [];
        }

        var stated = cell["rejected:".Length..]
            .Split(',')
            .Select(part => part.Trim())
            .ToList();

        // Stripped by name, not by discarding what does not match. A phrase
        // neither this file nor 2.4 knows about is a failure rather than a
        // silent omission.
        var forThisCheckpoint = stated.Where(
            phrase => !phrase.Equals(PerNameCap, StringComparison.Ordinal));

        return
        [
            .. forThisCheckpoint
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
            .GateFor(Ticker.Normalise(Symbol), SnapshotDate, PositionState.Cash)
            .ToDictionary(
                candidate => candidate.Candidate.Quote.Contract.Strike,
                candidate => candidate.Reasons);
    }
}
