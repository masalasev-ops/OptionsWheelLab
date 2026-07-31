using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-OffWatchlistRejected: no candidates for a non-member symbol.
/// </summary>
/// <remarks>
/// The candidate universe is restricted to point-in-time watchlist names the
/// lab would accept holding, because assignment is a designed leg rather than
/// an exit to be avoided [D-W16]. Applying today's watchlist retrospectively
/// would select names that survived, which removes exactly the cases the risk
/// machinery exists to catch [D-W9].
/// <para>
/// <b>Every case ingests a chain for the symbol under test.</b> A chain-less
/// symbol enumerates nothing whatever the membership answer, so it would pass
/// this fixture for the wrong reason and would keep passing if the membership
/// question were deleted from the generator outright. Both halves have to be
/// present for the assertion to be about membership.
/// </para>
/// <para>
/// A name is a non-member in more than one way, and this checks each: never
/// joined, joined later, left earlier, and joined-but-not-yet-known. The last
/// is the knowledge axis, which is the second of the two the generator collapses
/// onto one simulated date.
/// </para>
/// </remarks>
public sealed class FX_OffWatchlistRejected
{
    private static readonly Ticker Member = Ticker.Normalise("MEMB");
    private static readonly Ticker Stranger = Ticker.Normalise("STRG");
    private static readonly DateOnly SnapshotDate = new(2026, 3, 2);
    private static readonly DateOnly LaterDate = new(2026, 6, 1);
    private static readonly DateOnly Expiry = new(2026, 4, 17);

    private static readonly DateTimeOffset Recorded =
        new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A symbol that was never a member enumerates nothing, with its chain
    /// present, while a member with the same chain enumerates.
    /// </summary>
    [Fact]
    public void A_non_member_enumerates_nothing_though_its_chain_is_present()
    {
        using var store = StoreWith(joins:
        [
            (Member, new DateOnly(2026, 1, 2), Recorded),
        ]);

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);
        var generator = Generator(connection);

        // The control first. If this were empty the assertion below would hold
        // for want of a chain rather than for want of membership.
        Assert.NotEmpty(generator.EnumerateFor(Member, SnapshotDate, PositionState.Cash));

        // And the stranger's chain is in the store, read directly, so its
        // absence from the enumeration is the generator's doing.
        Assert.NotEmpty(new AsOfMarketData(connection).QuotesFor(
            Stranger, SnapshotDate, SnapshotDate));

        Assert.Empty(generator.EnumerateFor(Stranger, SnapshotDate, PositionState.Cash));
    }

    /// <summary>
    /// 1.3's FX-PitMembershipExcludesLaterJoiner reaching the generator rather
    /// than being restated: a name that joined after the simulated date is not a
    /// member at it.
    /// </summary>
    [Fact]
    public void A_symbol_that_joined_after_the_simulated_date_enumerates_nothing()
    {
        using var store = StoreWith(joins:
        [
            (Member, new DateOnly(2026, 1, 2), Recorded),
            (Stranger, LaterDate, new DateTimeOffset(LaterDate, new TimeOnly(21, 0), TimeSpan.Zero)),
        ]);

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);
        var generator = Generator(connection);

        Assert.Empty(generator.EnumerateFor(Stranger, SnapshotDate, PositionState.Cash));

        // The same name and the same generator on the date it joined. The chain
        // is quoted on both dates, so the only thing that changed between the
        // two answers is membership.
        Assert.NotEmpty(generator.EnumerateFor(Stranger, LaterDate, PositionState.Cash));
    }

    /// <summary>
    /// The other way to be a non-member: a name that left before the simulated
    /// date. Membership is state rather than a filter, so a departure is
    /// resolved from the transition sequence [D-W9].
    /// </summary>
    [Fact]
    public void A_symbol_that_left_before_the_simulated_date_enumerates_nothing()
    {
        using var store = StoreWith(
            joins:
            [
                (Member, new DateOnly(2026, 1, 2), Recorded),
                (Stranger, new DateOnly(2026, 1, 2), Recorded),
            ],
            departure: (Stranger, new DateOnly(2026, 2, 1), Recorded));

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);
        var generator = Generator(connection);

        Assert.NotEmpty(generator.EnumerateFor(Member, SnapshotDate, PositionState.Cash));
        Assert.Empty(generator.EnumerateFor(Stranger, SnapshotDate, PositionState.Cash));
    }

    /// <summary>
    /// The knowledge axis: a join effective before the simulated date but
    /// recorded after it is invisible to that date.
    /// </summary>
    /// <remarks>
    /// This is the axis a read could silently drop while looking correct, and
    /// the reason the generator passes its one date to both parameters of both
    /// reads rather than only to the session one.
    /// </remarks>
    [Fact]
    public void A_join_recorded_after_the_simulated_date_enumerates_nothing_at_it()
    {
        var backfilled = new DateTimeOffset(LaterDate, new TimeOnly(21, 0), TimeSpan.Zero);

        using var store = StoreWith(joins:
        [
            (Member, new DateOnly(2026, 1, 2), Recorded),
            (Stranger, new DateOnly(2026, 1, 2), backfilled),
        ]);

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);
        var generator = Generator(connection);

        Assert.Empty(generator.EnumerateFor(Stranger, SnapshotDate, PositionState.Cash));

        // Once the backfill is known, the same name enumerates.
        Assert.NotEmpty(generator.EnumerateFor(Stranger, LaterDate, PositionState.Cash));

        // The row was effective before the simulated date all along, so only
        // the knowledge axis excluded it. The generator cannot express this
        // pairing, because it collapses both axes onto one simulated date; the
        // read underneath it can, which is why the two parameters exist.
        Assert.True(new AsOfMembership(connection).WasMemberOn(
            Stranger, SnapshotDate, asOf: LaterDate));
    }

    private static CandidateGenerator Generator(SqliteConnection connection) =>
        new(new AsOfMembership(connection), new AsOfMarketData(connection));

    /// <summary>
    /// A store holding a chain for both names, and the membership transitions
    /// the case asks for.
    /// </summary>
    private static TempStore StoreWith(
        (Ticker Symbol, DateOnly EffectiveOn, DateTimeOffset ObservedAt)[] joins,
        (Ticker Symbol, DateOnly EffectiveOn, DateTimeOffset ObservedAt)? departure = null)
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Recorded);

        using var connection = store.Connections.Open(StoreAccess.Write);
        var membership = new MembershipWriter(connection);
        var chains = new ChainWriter(connection);

        foreach (var (symbol, effectiveOn, observedAt) in joins)
        {
            membership.Append(symbol, MembershipKind.Joined, effectiveOn, observedAt);
        }

        if (departure is var (left, on, at))
        {
            membership.Append(left, MembershipKind.Left, on, at, reason: "left the watchlist");
        }

        // Both names get a chain, always. The stranger's presence is what makes
        // every empty result above a statement about membership.
        foreach (var symbol in (Ticker[])[Member, Stranger])
        {
            chains.Ingest(Chain(symbol), Recorded);
        }

        return store;
    }

    /// <summary>
    /// One put, quoted on every date any case asks about.
    /// </summary>
    /// <remarks>
    /// The later date is there so the later-joiner case can show the same
    /// symbol enumerating once it is a member, rather than only showing that it
    /// does not before. Without it, both dates would be empty and the second
    /// would be empty for want of a chain.
    /// </remarks>
    private static SyntheticChain Chain(Ticker symbol)
    {
        DateOnly[] snapshotDates = [SnapshotDate, LaterDate];

        var quotes = snapshotDates.Select(snapshotDate => new ContractQuote(
            ContractIdentity.Of(symbol, Expiry, OptionRight.Put, 50.00m),
            snapshotDate,
            Bid: 0.95m,
            Ask: 1.01m,
            Delta: -0.24m));

        return new SyntheticChain(symbol, [], [.. quotes]);
    }
}
