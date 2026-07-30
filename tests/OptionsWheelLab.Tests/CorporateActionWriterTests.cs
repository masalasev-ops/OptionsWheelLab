using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// A mint records the event and the stated successor, atomic, and touches
/// nothing else.
/// </summary>
/// <remarks>
/// Not a registered fixture; the registered check at 1.5 is the
/// three-generation fixture, which has its own file.
/// </remarks>
public sealed class CorporateActionWriterTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly DateOnly Expiry = new(2026, 9, 18);
    private static readonly DateOnly ExDate = new(2026, 5, 4);

    private static readonly DateTimeOffset Observed =
        new(2026, 5, 4, 12, 0, 0, 0, TimeSpan.Zero);

    private static readonly CorporateAction ThreeForTwo =
        new(CorporateActionKind.Split, ExDate, Ratio: 1.5m);

    // Stated terms, transcribed: the authority's memo for a 3-for-2 states the
    // 60 strike and the 150-share deliverable. Nothing here computes them.
    private static readonly StatedSuccessorTerms Stated =
        new(Strike: 60m, DeliverableShares: 150, Multiplier: 100);

    [Fact]
    public void The_mint_records_the_event_and_the_stated_successor_with_its_link()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var predecessorId = InsertStandardContract(connection, strike: "90.00000000");

        var successorId = new CorporateActionWriter(connection)
            .MintSuccessor(predecessorId, Stated, ThreeForTwo, Observed);

        using var read = connection.CreateCommand();
        read.CommandText =
            """
            SELECT symbol, strike, deliverable_shares, multiplier, predecessor_contract_id
            FROM contracts
            WHERE contract_id = $id;
            """;
        read.Parameters.AddWithValue("$id", successorId);

        using var reader = read.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(Symbol.Value, reader.GetString(0));
        Assert.Equal("60.00000000", reader.GetString(1));
        Assert.Equal(150, reader.GetInt32(2));
        Assert.Equal(100, reader.GetInt32(3));
        Assert.Equal(predecessorId, reader.GetInt64(4));

        Assert.Equal(1L, Count(connection, "corporate_actions"));
    }

    [Fact]
    public void The_events_kind_and_ratio_are_recorded_facts()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var predecessorId = InsertStandardContract(connection, strike: "90.00000000");

        new CorporateActionWriter(connection)
            .MintSuccessor(predecessorId, Stated, ThreeForTwo, Observed);

        using var read = connection.CreateCommand();
        read.CommandText = "SELECT kind, ratio, amount, ex_date FROM corporate_actions;";

        using var reader = read.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(StoreCorporateActionKind.Split, reader.GetString(0));
        Assert.Equal("1.50000000", reader.GetString(1));
        Assert.True(reader.IsDBNull(2));
        Assert.Equal(StoreDate.ToStored(ExDate), reader.GetString(3));
    }

    /// <summary>
    /// The predecessor is untouched, asserted by row comparison rather than by
    /// the absence of an exception.
    /// </summary>
    [Fact]
    public void The_predecessor_row_reads_back_byte_identical_after_the_mint()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var predecessorId = InsertStandardContract(connection, strike: "90.00000000");

        var before = RowOf(connection, predecessorId);
        new CorporateActionWriter(connection)
            .MintSuccessor(predecessorId, Stated, ThreeForTwo, Observed);
        var after = RowOf(connection, predecessorId);

        Assert.Equal(before, after);
    }

    [Fact]
    public void An_adjustment_that_changes_nothing_is_refused()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var predecessorId = InsertStandardContract(connection, strike: "90.00000000");

        var unchanged = new StatedSuccessorTerms(
            Strike: 90m, DeliverableShares: 100, Multiplier: 100);

        var refusal = Assert.Throws<InvalidOperationException>(
            () => new CorporateActionWriter(connection)
                .MintSuccessor(predecessorId, unchanged, ThreeForTwo, Observed));

        Assert.Contains("not an adjustment", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(0L, Count(connection, "corporate_actions"));
        Assert.Equal(1L, Count(connection, "contracts"));
    }

    /// <summary>
    /// Atomic both ways, in 1.4's observed-rollback shape: the event insert
    /// precedes the successor insert, so a successor collision must take the
    /// already-inserted event row down with it.
    /// </summary>
    [Fact]
    public void A_mid_transaction_failure_leaves_neither_the_event_nor_the_successor()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var predecessorId = InsertStandardContract(connection, strike: "90.00000000");

        // The intended successor's five-tuple already exists, so the successor
        // insert collides with the uniqueness constraint after the event row
        // has been written inside the transaction.
        InsertContract(
            connection, strike: "60.00000000", deliverableShares: 150);

        Assert.Throws<SqliteException>(
            () => new CorporateActionWriter(connection)
                .MintSuccessor(predecessorId, Stated, ThreeForTwo, Observed));

        Assert.Equal(0L, Count(connection, "corporate_actions"));
        Assert.Equal(2L, Count(connection, "contracts"));
    }

    [Fact]
    public void A_missing_predecessor_is_refused_by_name()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var refusal = Assert.Throws<InvalidOperationException>(
            () => new CorporateActionWriter(connection)
                .MintSuccessor(41, Stated, ThreeForTwo, Observed));

        Assert.Contains("No contract with id 41", refusal.Message, StringComparison.Ordinal);
    }

    private static long InsertStandardContract(SqliteConnection connection, string strike) =>
        InsertContract(connection, strike, deliverableShares: 100);

    private static long InsertContract(
        SqliteConnection connection, string strike, int deliverableShares)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO contracts (symbol, expiry, right, strike, deliverable_shares)
            VALUES ($symbol, $expiry, 'put', $strike, $deliverable)
            RETURNING contract_id;
            """;
        insert.Parameters.AddWithValue("$symbol", Symbol.Value);
        insert.Parameters.AddWithValue("$expiry", StoreDate.ToStored(Expiry));
        insert.Parameters.AddWithValue("$strike", strike);
        insert.Parameters.AddWithValue("$deliverable", deliverableShares);
        return (long)insert.ExecuteScalar()!;
    }

    private static IReadOnlyList<object> RowOf(SqliteConnection connection, long contractId)
    {
        using var read = connection.CreateCommand();
        read.CommandText = "SELECT * FROM contracts WHERE contract_id = $id;";
        read.Parameters.AddWithValue("$id", contractId);

        using var reader = read.ExecuteReader();
        Assert.True(reader.Read());

        var values = new object[reader.FieldCount];
        reader.GetValues(values);
        return values;
    }

    private static long Count(SqliteConnection connection, string table)
    {
        using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM {table};";
        return (long)count.ExecuteScalar()!;
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Observed);
        return store;
    }
}
