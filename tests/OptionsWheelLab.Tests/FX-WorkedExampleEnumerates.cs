using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using static OptionsWheelLab.Tests.WorkedExampleOracle;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-WorkedExampleEnumerates: the worked example's chain enumerates exactly
/// the strikes §2 states and §3 claims, in identity order.
/// </summary>
/// <remarks>
/// The third fixture pinning that document and the first to read §3 rather
/// than §2. 0.6's proves the file loads to what §2 states and 1.4's proves the
/// store returns it; this one proves the generator offers it. §3 opens "All
/// seven strikes are enumerated" and 2.2 owns the first half of that sentence,
/// the second belonging to the gate that 2.3 to 2.5 build.
/// <para>
/// <b>If §3 and the enumerator ever disagree, one of them is wrong and this
/// says which.</b> The two tables are halves of one document that nothing
/// compared before 2.2: a revision touching §2 and not §3 was invisible.
/// </para>
/// <para>
/// <b>The chain is ingested at the snapshot date's own evening, with the bars
/// dropped.</b> 1.4's fixture ingests whole at 2026-06-19 and reads as of then;
/// here the read is as of 2026-03-02, so anything stamped later is correctly
/// invisible and the chain has to be recorded by then. §5's closes run to June,
/// and a store built for a simulated 2026-03-02 legitimately holds only what
/// was observed by then, so stamping those closes as observed in March would
/// write a false observation to no purpose: 2.2 reads no bars. The quotes are
/// every quote the document states, unmodified.
/// </para>
/// </remarks>
public sealed class FX_WorkedExampleEnumerates
{
    /// <summary>
    /// The evening of the snapshot date, which is the latest instant the
    /// simulated date can see.
    /// </summary>
    private static readonly DateTimeOffset Recorded =
        new(SnapshotDate, new TimeOnly(21, 0), TimeSpan.Zero);

    /// <summary>
    /// Joined well before the example opens. §1's account already holds a
    /// position in this name, so membership is a precondition of the example
    /// rather than a fact it states.
    /// </summary>
    private static readonly DateOnly Joined = new(2026, 1, 2);

    [Fact]
    public void Enumerating_in_cash_yields_the_strikes_the_document_states()
    {
        var expected = StrikeTable();

        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var candidates = Enumerate(connection);

        // A table that matched nothing would let every comparison below pass
        // while comparing nothing.
        Assert.NotEmpty(expected);
        Assert.NotEmpty(candidates);

        Assert.Equal(
            expected.Select(row => StoreDecimal.ParseStored(row[0])),
            candidates.Select(candidate => candidate.Quote.Contract.Strike));
    }

    /// <summary>
    /// §3's claim, read as the rows of its own table: the same seven, and
    /// nothing filtered out before the gate sees them.
    /// </summary>
    /// <remarks>
    /// Enumeration filters on nothing but position state and membership
    /// [D-W5, D-W10], so the four §3 marks rejected are enumerated here
    /// alongside the three it marks feasible. A generator that pre-filtered
    /// would produce a smaller enumerated set and a smaller record of what the
    /// gate did.
    /// </remarks>
    [Fact]
    public void Enumeration_yields_every_strike_the_gate_section_claims()
    {
        var claimed = GateTable();

        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var candidates = Enumerate(connection);

        Assert.NotEmpty(claimed);
        Assert.NotEmpty(candidates);

        Assert.Equal(
            claimed.Select(row => StoreDecimal.ParseStored(row[0])),
            candidates.Select(candidate => candidate.Quote.Contract.Strike));

        // Including the ones §3 rejects, which is the property that makes the
        // gate's effect auditable rather than invisible.
        Assert.Contains(claimed, row => row[5].StartsWith("rejected", StringComparison.Ordinal));
    }

    /// <summary>
    /// The document's two halves agree with each other.
    /// </summary>
    /// <remarks>
    /// Asserted directly rather than inferred from the two comparisons above
    /// passing, so a disagreement names itself instead of arriving as two
    /// failures that have to be read together.
    /// </remarks>
    [Fact]
    public void The_chain_section_and_the_gate_section_state_the_same_strikes()
    {
        var stated = StrikeTable();
        var claimed = GateTable();

        Assert.NotEmpty(stated);
        Assert.NotEmpty(claimed);

        Assert.Equal(
            stated.Select(row => StoreDecimal.ParseStored(row[0])),
            claimed.Select(row => StoreDecimal.ParseStored(row[0])));
    }

    /// <summary>
    /// Every candidate is a put on the stated expiry and snapshot date, in
    /// identity order.
    /// </summary>
    /// <remarks>
    /// The order is the generator's own guarantee asserted at its output.
    /// <see cref="AsOfMarketData.QuotesFor"/> imposes identity order and the
    /// generator inherits it rather than sorting again, so a read that stopped
    /// ordering fails here rather than silently reaching three makers who are
    /// promised byte-identical sets [D-W4].
    /// </remarks>
    [Fact]
    public void Every_candidate_is_a_put_on_the_stated_expiry_in_identity_order()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var candidates = Enumerate(connection);

        Assert.NotEmpty(candidates);

        Assert.All(candidates, candidate =>
        {
            Assert.Equal(Ticker.Normalise(Symbol), candidate.Quote.Contract.Underlying);
            Assert.Equal(Expiry, candidate.Quote.Contract.Expiry);
            Assert.Equal(Right, candidate.Quote.Contract.Right);
            Assert.Equal(SnapshotDate, candidate.Quote.SnapshotDate);
        });

        Assert.Equal(
            candidates.Select(candidate => candidate.Quote.Contract).Order(),
            candidates.Select(candidate => candidate.Quote.Contract));
    }

    private static IReadOnlyList<EnumeratedCandidate> Enumerate(
        Microsoft.Data.Sqlite.SqliteConnection connection) =>
        new CandidateGenerator(
                new AsOfMembership(connection), new AsOfMarketData(connection))
            .EnumerateFor(Ticker.Normalise(Symbol), SnapshotDate, PositionState.Cash);

    private static TempStore IngestedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Recorded);

        using var connection = store.Connections.Open(StoreAccess.Write);

        new MembershipWriter(connection).Append(
            Ticker.Normalise(Symbol), MembershipKind.Joined, Joined, Recorded);

        // The bars are dropped rather than the document's quotes changed: what
        // the example states about the chain is exactly what is ingested.
        new ChainWriter(connection).Ingest(LoadChain() with { Bars = [] }, Recorded);

        return store;
    }
}
