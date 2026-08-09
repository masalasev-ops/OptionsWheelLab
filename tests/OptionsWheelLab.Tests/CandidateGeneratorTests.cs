using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// What the position state makes sellable, and that enumeration is a pure
/// function of its three arguments.
/// </summary>
/// <remarks>
/// Not a registered fixture: the checks registered against 2.2 are
/// FX-OffWatchlistRejected and FX-WorkedExampleEnumerates, each with its own
/// file. 1.2 set the precedent of a checkpoint's remaining tests landing as an
/// unregistered suite.
/// <para>
/// <b>The chain here carries both rights, which no other chain in the
/// repository does.</b> WORKED_EXAMPLE's is seven puts on one expiry, so every
/// fixture reading it exercises the cash-sells-puts half alone: an enumerator
/// that ignored its third argument, or filtered to puts unconditionally, would
/// pass all of them. It is built inline rather than added to
/// <c>synthetic/</c>, which holds hand-written chains the corpus refers to
/// [D-W31]; this is scaffolding for one suite.
/// </para>
/// </remarks>
public sealed class CandidateGeneratorTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WHEL");
    private static readonly Ticker Second = Ticker.Normalise("ACME");
    private static readonly DateOnly SnapshotDate = new(2026, 3, 2);
    private static readonly DateOnly Expiry = new(2026, 4, 17);
    private static readonly decimal[] Strikes = [45.00m, 50.00m, 55.00m];

    private static readonly DateTimeOffset Recorded =
        new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cash_enumerates_the_puts_and_no_call()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var candidates = Generator(connection)
            .EnumerateFor(Symbol, SnapshotDate, PositionState.Cash);

        Assert.Equal(Strikes, candidates.Select(candidate => candidate.Quote.Contract.Strike));
        Assert.All(candidates, candidate =>
            Assert.Equal(OptionRight.Put, candidate.Quote.Contract.Right));
    }

    /// <summary>
    /// The other direction on the same chain, so neither half passes for want
    /// of data.
    /// </summary>
    [Fact]
    public void Holding_shares_enumerates_the_calls_and_no_put()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var candidates = Generator(connection)
            .EnumerateFor(Symbol, SnapshotDate, PositionState.HoldingShares);

        Assert.Equal(Strikes, candidates.Select(candidate => candidate.Quote.Contract.Strike));
        Assert.All(candidates, candidate =>
            Assert.Equal(OptionRight.Call, candidate.Quote.Contract.Right));
    }

    /// <summary>
    /// The two states have disjoint answers on one chain, which is the claim
    /// that a state-blind enumerator would fail.
    /// </summary>
    [Fact]
    public void The_two_states_enumerate_disjoint_sets_of_the_same_chain()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var generator = Generator(connection);
        var puts = generator.EnumerateFor(Symbol, SnapshotDate, PositionState.Cash);
        var calls = generator.EnumerateFor(Symbol, SnapshotDate, PositionState.HoldingShares);

        Assert.NotEmpty(puts);
        Assert.NotEmpty(calls);
        Assert.Empty(puts.Intersect(calls));

        // Together they are the whole chain: enumeration drops contracts for
        // being the wrong right and for nothing else [D-W5, D-W10].
        var whole = new AsOfMarketData(connection).QuotesFor(Symbol, SnapshotDate, SnapshotDate);

        Assert.Equal(whole.Count, puts.Count + calls.Count);
    }

    /// <summary>
    /// A short leg enumerates the right it is short, so a roll has something to
    /// roll into.
    /// </summary>
    /// <remarks>
    /// <b>This replaces a case written to fail the day rolling got a rule.</b>
    /// That case asserted a short leg enumerates nothing, and its remark said the
    /// failure would be "the right moment to be told that 2.2 made an assumption
    /// Phase 3 has overtaken". Phase 3 did not overtake it; [D-W54] did at 4.4,
    /// two phases later than predicted. It was watched failing and replaced
    /// deliberately rather than discovered red, because a green suite after the
    /// rule landed without this file being touched would mean the rule had not
    /// reached enumeration.
    /// <para>
    /// A short put enumerates puts and a short call enumerates calls, which is the
    /// state's own right rather than the one it would move to. A roll keeps the
    /// right and changes the contract [D-W54].
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(PositionState.ShortPut, OptionRight.Put)]
    [InlineData(PositionState.ShortCall, OptionRight.Call)]
    public void A_short_leg_enumerates_the_right_it_is_short(
        PositionState state,
        OptionRight right)
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var enumerated = Generator(connection).EnumerateFor(Symbol, SnapshotDate, state);

        Assert.NotEmpty(enumerated);
        Assert.All(enumerated, candidate => Assert.Equal(right, candidate.Quote.Contract.Right));
    }

    /// <summary>
    /// A short leg is offered exactly what the matching open state is offered.
    /// </summary>
    /// <remarks>
    /// [D-W52]'s property in the one place it could break. The set is keyed on
    /// symbol, session and right and not on state, so a maker holding a short put
    /// and a maker holding cash must see one set or the key is wrong. Asserted
    /// rather than reasoned, because the whole feasible-set grain rests on it.
    /// </remarks>
    [Fact]
    public void A_short_put_is_offered_what_a_cash_state_is_offered()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var generator = Generator(connection);

        Assert.Equal(
            generator.EnumerateFor(Symbol, SnapshotDate, PositionState.Cash),
            generator.EnumerateFor(Symbol, SnapshotDate, PositionState.ShortPut));
    }

    [Fact]
    public void An_uninitialised_state_is_refused_rather_than_enumerating_puts()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => Generator(connection).EnumerateFor(Symbol, SnapshotDate, default));

        Assert.Contains("not a position state", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same three arguments enumerate the same candidates in the same
    /// order, twice, which is 2.2's definition of done [D-W4].
    /// </summary>
    /// <remarks>
    /// <see cref="EnumeratedCandidate"/>, <see cref="ContractQuote"/> and
    /// <see cref="ContractIdentity"/> are all records, so comparing the lists is
    /// deep value equality including order rather than reference identity.
    /// </remarks>
    [Fact]
    public void The_same_inputs_enumerate_the_same_candidates_in_the_same_order()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var generator = Generator(connection);

        var first = generator.EnumerateFor(Symbol, SnapshotDate, PositionState.Cash);
        var second = generator.EnumerateFor(Symbol, SnapshotDate, PositionState.Cash);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// Interleaving other work between two identical calls does not change the
    /// second, which rules out state accumulated across calls.
    /// </summary>
    [Fact]
    public void An_interleaved_call_does_not_change_the_repeat()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var generator = Generator(connection);

        var first = generator.EnumerateFor(Symbol, SnapshotDate, PositionState.Cash);

        generator.EnumerateFor(Second, SnapshotDate, PositionState.HoldingShares);
        generator.EnumerateFor(Symbol, SnapshotDate.AddDays(1), PositionState.Cash);
        generator.EnumerateFor(Symbol, SnapshotDate, PositionState.HoldingShares);

        Assert.Equal(first, generator.EnumerateFor(Symbol, SnapshotDate, PositionState.Cash));
    }

    /// <summary>
    /// Two generators over two connections to one store agree, which rules out
    /// instance state.
    /// </summary>
    [Fact]
    public void Two_generators_over_one_store_agree()
    {
        using var store = IngestedStore();
        using var first = store.Connections.Open(StoreAccess.ReadOnly);
        using var second = store.Connections.Open(StoreAccess.ReadOnly);

        Assert.Equal(
            Generator(first).EnumerateFor(Symbol, SnapshotDate, PositionState.Cash),
            Generator(second).EnumerateFor(Symbol, SnapshotDate, PositionState.Cash));
    }

    /// <summary>
    /// Each of the three arguments demonstrably changes the answer, so "a
    /// function of its three arguments" is not vacuously true of something that
    /// returns a constant.
    /// </summary>
    [Fact]
    public void Every_argument_varies_the_answer()
    {
        using var store = IngestedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var generator = Generator(connection);
        var baseline = generator.EnumerateFor(Symbol, SnapshotDate, PositionState.Cash);

        Assert.NotEmpty(baseline);
        Assert.NotEqual(baseline, generator.EnumerateFor(Second, SnapshotDate, PositionState.Cash));
        Assert.NotEqual(
            baseline, generator.EnumerateFor(Symbol, SnapshotDate.AddDays(1), PositionState.Cash));
        Assert.NotEqual(
            baseline, generator.EnumerateFor(Symbol, SnapshotDate, PositionState.HoldingShares));
    }

    private static CandidateGenerator Generator(Microsoft.Data.Sqlite.SqliteConnection connection) =>
        new(new AsOfMembership(connection), new AsOfMarketData(connection));

    /// <summary>
    /// A store holding a two-right chain for two member names.
    /// </summary>
    /// <remarks>
    /// Read through a <see cref="StoreAccess.ReadOnly"/> connection in every
    /// case above. That is how purity over the store is asserted rather than
    /// described: a generator that wrote anything at all would raise on the
    /// connection rather than needing to be caught by a comparison.
    /// </remarks>
    private static TempStore IngestedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Recorded);

        using var connection = store.Connections.Open(StoreAccess.Write);
        var membership = new MembershipWriter(connection);
        var chains = new ChainWriter(connection);

        foreach (var symbol in (Ticker[])[Symbol, Second])
        {
            membership.Append(
                symbol, MembershipKind.Joined, new DateOnly(2026, 1, 2), Recorded);

            chains.Ingest(TwoRightChain(symbol), Recorded);
        }

        return store;
    }

    /// <summary>
    /// Both rights at every strike on one expiry, in contract identity order.
    /// </summary>
    private static SyntheticChain TwoRightChain(Ticker symbol)
    {
        var quotes =
            from right in (OptionRight[])[OptionRight.Put, OptionRight.Call]
            from strike in Strikes
            select new ContractQuote(
                ContractIdentity.Of(symbol, Expiry, right, strike),
                SnapshotDate,
                Bid: 1.00m,
                Ask: 1.10m,
                Delta: right == OptionRight.Put ? -0.25m : 0.25m);

        // No bars and no earnings: this suite is about which right a position
        // state makes sellable, so the earnings constraint has nothing to read
        // and admits throughout, which is what isolates the case under test.
        return new SyntheticChain(symbol, [], [.. quotes], [], []);
    }
}
