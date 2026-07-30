using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The lineage walk resolves every generation, newest first, without a
/// self-join.
/// </summary>
/// <remarks>
/// Not a registered fixture; the registered check at 1.5 exercises the walk
/// over minted successors in its own file. These pin the reader itself over
/// hand-inserted rows.
/// </remarks>
public sealed class ContractLineageTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 5, 4, 12, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_walk_returns_all_generations_in_order()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var origin = Insert(connection, strike: "90.00000000", deliverable: 100, predecessor: null);
        var second = Insert(connection, strike: "60.00000000", deliverable: 150, predecessor: origin);
        var third = Insert(connection, strike: "40.00000000", deliverable: 225, predecessor: second);

        var lineage = new ContractLineage(connection).WalkFrom(third);

        Assert.Equal([third, second, origin], lineage.Select(entry => entry.ContractId));
        Assert.Equal([225, 150, 100], lineage.Select(entry => entry.Identity.DeliverableShares));
        Assert.Equal([second, origin, null], lineage.Select(entry => entry.PredecessorContractId));
    }

    [Fact]
    public void A_contract_with_no_predecessor_returns_itself_alone()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var only = Insert(connection, strike: "90.00000000", deliverable: 100, predecessor: null);

        var lineage = new ContractLineage(connection).WalkFrom(only);

        var entry = Assert.Single(lineage);
        Assert.Equal(only, entry.ContractId);
        Assert.Null(entry.PredecessorContractId);
    }

    [Fact]
    public void An_unknown_contract_returns_an_empty_lineage()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Assert.Empty(new ContractLineage(connection).WalkFrom(41));
    }

    private static long Insert(
        SqliteConnection connection, string strike, int deliverable, long? predecessor)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO contracts
                (symbol, expiry, right, strike, predecessor_contract_id, deliverable_shares)
            VALUES ('WDGT', '2026-09-18', 'put', $strike, $predecessor, $deliverable)
            RETURNING contract_id;
            """;
        insert.Parameters.AddWithValue("$strike", strike);
        insert.Parameters.AddWithValue("$predecessor", predecessor ?? (object)DBNull.Value);
        insert.Parameters.AddWithValue("$deliverable", deliverable);
        return (long)insert.ExecuteScalar()!;
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);
        return store;
    }
}
