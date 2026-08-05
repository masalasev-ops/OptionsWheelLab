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
/// A store holding a chain, kept open so decisions can be written against it.
/// </summary>
/// <remarks>
/// <see cref="GateScenario"/> disposes its store before returning, which is right
/// for a fixture asserting about verdicts and wrong for one asserting about what
/// was recorded. This keeps the connection, and borrows that helper's quotes so
/// the two are describing the same chain.
/// </remarks>
internal sealed class DecisionScenario : IDisposable
{
    internal static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Recorded =
        new(2026, 3, 2, 21, 0, 0, TimeSpan.Zero);

    private readonly TempStore _store;

    internal DecisionScenario(IReadOnlyList<ContractQuote> quotes)
    {
        _store = TempStore.Empty();
        new MigrationRunner(_store.Connections).Run(Seeded);

        Connection = _store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(Connection).AppendAll(SeedValues.All, Seeded);

        new MembershipWriter(Connection).Append(
            GateScenario.Symbol, MembershipKind.Joined, new DateOnly(2026, 1, 2), Seeded);

        new ChainWriter(Connection).Ingest(
            new SyntheticChain(GateScenario.Symbol, [], quotes, [], []), Recorded);

        Generator = new CandidateGenerator(
            new AsOfMembership(Connection),
            new AsOfMarketData(Connection),
            new AsOfConfiguration(Connection));
    }

    internal SqliteConnection Connection { get; }

    internal CandidateGenerator Generator { get; }

    internal DecisionStore Decisions => new(Connection);

    internal DecisionRecordReader Reader => new(Connection);

    /// <summary>What a maker in this state with this book is offered.</summary>
    internal IReadOnlyList<GatedCandidate> Gated(
        BookState? book = null,
        PositionState state = PositionState.Cash) =>
        Generator.GateFor(
            GateScenario.Symbol, GateScenario.Simulated, state, book ?? BookState.Empty);

    /// <summary>Records one maker's decision, taking nothing.</summary>
    internal long Record(
        string makerId,
        IReadOnlyList<GatedCandidate> offered,
        ContractIdentity? chosen = null) =>
        Decisions.Record(
            makerId,
            GateScenario.Symbol,
            GateScenario.Simulated,
            OptionRight.Put,
            offered,
            chosen is null ? DecisionKind.None : DecisionKind.OpenPut,
            chosen,
            trialId: null,
            policyVersion: 1,
            Recorded);

    public void Dispose()
    {
        Connection.Dispose();
        _store.Dispose();
    }
}
