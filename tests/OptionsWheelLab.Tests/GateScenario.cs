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
        IReadOnlyList<EarningsReport>? earnings = null,
        BookState? book = null,
        PositionState state = PositionState.Cash,
        IReadOnlyList<ConfigEntry>? overrides = null)
    {
        using var store = TempStore.Empty();
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
                new SyntheticChain(Symbol, [], quotes, earnings ?? []), Recorded);
        }

        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var gated = new CandidateGenerator(
                new AsOfMembership(connection),
                new AsOfMarketData(connection),
                new AsOfConfiguration(connection))
            .GateFor(Symbol, Simulated, state, book ?? BookState.Empty);

        return gated.ToDictionary(
            candidate => candidate.Candidate.Quote.Contract.Strike,
            candidate => candidate.Reasons);
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
