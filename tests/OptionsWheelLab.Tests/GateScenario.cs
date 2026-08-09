using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// A member name, the seeded bounds in force, and a chain, gated on one
/// simulated date.
/// </summary>
/// <remarks>
/// Stated once for the fixtures that each isolate one constraint family. What
/// they would otherwise restate is the setup that has nothing to do with the
/// constraint under test, and five copies of it drift the way two copies of one
/// query do.
/// <para>
/// <b>Configuration is seeded before the simulated date, deliberately.</b> The
/// seed's own `set_at` comes from the wall clock, so a store seeded "now" and
/// queried at a 2026 simulated date resolves every bound to null and stops the
/// evaluation [D-W37]. Tests choose the instant; a walk-forward cannot, which
/// is the Phase 9 obligation.
/// </para>
/// </remarks>
internal static class GateScenario
{
    internal static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    internal static readonly DateOnly Simulated = new(2026, 3, 2);
    internal static readonly DateOnly Expiry = new(2026, 4, 17);

    /// <summary>Before the simulated date, so every bound resolves.</summary>
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Recorded =
        new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The gate's verdict on every quote, keyed by strike.
    /// </summary>
    /// <param name="book">
    /// What the account already carries, defaulting to nothing.
    /// </param>
    /// <param name="overrides">
    /// Config versions appended after the seed, for a fixture whose subject the
    /// seeded values cannot express.
    /// </param>
    /// <remarks>
    /// <b>The default book is empty and the caps are therefore silent by
    /// default.</b> That is right for the contract-constraint fixtures, which is
    /// what this helper was built for: a book they did not ask about should not
    /// put a reason on their quotes. It is wrong for a cap fixture, and a cap
    /// tested against an empty book passes whether or not it works, so every
    /// registered cap fixture states a book rather than taking this default.
    /// </remarks>
    internal static IReadOnlyDictionary<decimal, IReadOnlyList<GateReason>> Gate(
        IReadOnlyList<ContractQuote> quotes,
        BookState book,
        IReadOnlyList<EarningsReport>? earnings = null,
        PositionState state = PositionState.Cash,
        IReadOnlyList<ConfigEntry>? overrides = null) =>
        ByStrike(EnumeratedAndGated(quotes, earnings, book, state, overrides).Gated);

    /// <summary>
    /// The contract-level verdicts alone, which no book can change [D-W52].
    /// </summary>
    /// <remarks>
    /// <b>There is no book here rather than an empty one.</b> This helper took a
    /// book defaulting to <see cref="BookState.Empty"/> until 4.5, and its own
    /// remark said that was right for a contract fixture and wrong for a cap
    /// fixture. The gate splitting at the same seam makes the distinction a
    /// signature rather than a convention: a fixture whose subject is a contract
    /// constraint calls this and cannot be handed a cap verdict it did not ask
    /// for, and a fixture whose subject is a cap calls <see cref="Gate"/> and
    /// must state the book it is about.
    /// </remarks>
    internal static IReadOnlyDictionary<decimal, IReadOnlyList<GateReason>> Shared(
        IReadOnlyList<ContractQuote> quotes,
        IReadOnlyList<EarningsReport>? earnings = null,
        OptionRight right = OptionRight.Put,
        IReadOnlyList<ConfigEntry>? overrides = null)
    {
        using var scenario = Store(quotes, earnings, overrides);

        return ByStrike(scenario.Generator.SharedFor(Symbol, Simulated, right));
    }

    private static IReadOnlyDictionary<decimal, IReadOnlyList<GateReason>> ByStrike(
        IReadOnlyList<GatedCandidate> gated) =>
        gated.ToDictionary(
            candidate => candidate.Candidate.Quote.Contract.Strike,
            candidate => candidate.Reasons);

    /// <summary>
    /// What the generator enumerated and what the gate made of it, both in the
    /// order they were returned.
    /// </summary>
    /// <remarks>
    /// <see cref="Gate"/> keys by strike, which is what a constraint fixture
    /// wants and which discards the order. 2.5's subject is the order, so it
    /// needs the sequences: the gated one to assert it is the identity total
    /// order, and the enumerated one beside it to assert the gate inherits that
    /// order rather than imposing it a second time.
    /// <para>
    /// Both come from one store and one generator, so a difference between them
    /// is the gate's and not two ingests disagreeing.
    /// </para>
    /// </remarks>
    internal static (
        IReadOnlyList<EnumeratedCandidate> Enumerated,
        IReadOnlyList<GatedCandidate> Gated) EnumeratedAndGated(
        IReadOnlyList<ContractQuote> quotes,
        IReadOnlyList<EarningsReport>? earnings = null,
        BookState? book = null,
        PositionState state = PositionState.Cash,
        IReadOnlyList<ConfigEntry>? overrides = null)
    {
        using var scenario = Store(quotes, earnings, overrides);

        return (
            scenario.Generator.EnumerateFor(Symbol, Simulated, state),
            scenario.Generator.GateFor(Symbol, Simulated, state, book ?? BookState.Empty));
    }

    /// <summary>A store holding this chain, with a generator over it.</summary>
    private static GateStore Store(
        IReadOnlyList<ContractQuote> quotes,
        IReadOnlyList<EarningsReport>? earnings,
        IReadOnlyList<ConfigEntry>? overrides)
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Seeded);

        using (var write = store.Connections.Open(StoreAccess.Write))
        {
            var configuration = new ConfigWriter(write);
            configuration.AppendAll(SeedValues.All, Seeded);

            // Version 2 at the same instant. Equal set_at is permitted and
            // version breaks the tie, which is what as-of resolution already
            // does [D-W26], so an override is in force on the simulated date
            // without backdating anything.
            foreach (var entry in overrides ?? [])
            {
                configuration.Append(entry.Key, entry.Value, Seeded);
            }

            new MembershipWriter(write).Append(
                Symbol, MembershipKind.Joined, new DateOnly(2026, 1, 2), Seeded);

            new ChainWriter(write).Ingest(
                new SyntheticChain(Symbol, [], quotes, earnings ?? [], []), Recorded);
        }

        return new GateStore(store);
    }

    /// <summary>A store and its generator, disposed together.</summary>
    private sealed class GateStore : IDisposable
    {
        private readonly TempStore _store;
        private readonly SqliteConnection _connection;

        internal GateStore(TempStore store)
        {
            _store = store;
            _connection = store.Connections.Open(StoreAccess.ReadOnly);

            Generator = new CandidateGenerator(
                new AsOfMembership(_connection),
                new AsOfMarketData(_connection),
                new AsOfConfiguration(_connection));
        }

        internal CandidateGenerator Generator { get; }

        public void Dispose()
        {
            _connection.Dispose();
            _store.Dispose();
        }
    }

    /// <summary>
    /// One put, with everything not under test set to a value that passes.
    /// </summary>
    /// <remarks>
    /// The defaults are the worked example's 50.00 strike: a 6.12 percent
    /// spread, a bid well above the floor, a delta inside the ceiling and an
    /// expiry 46 days out. A fixture overrides the one quantity it is about, so
    /// a reason it did not ask for means the constraint under test leaked.
    /// </remarks>
    internal static ContractQuote Quote(
        decimal strike,
        decimal bid = 0.95m,
        decimal ask = 1.01m,
        decimal? delta = -0.24m,
        DateOnly? expiry = null,
        OptionRight right = OptionRight.Put) =>
        new(
            ContractIdentity.Of(Symbol, expiry ?? Expiry, right, strike),
            Simulated,
            bid,
            ask,
            Delta: delta);
}
