using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-CorporateActionMintsSuccessor: a split mints a stated successor with its
/// predecessor recorded, the original row unchanged, and the lineage walk
/// resolving all generations.
/// </summary>
/// <remarks>
/// The three-generation chain is 1.1's collision demonstration made permanent:
/// 90 strike with 100 shares, then 60 with 150, then 40 with 225. Every term
/// below is a TRANSCRIBED value, stated the way the adjusting authority's memo
/// states it, never computed from the ratio [D-W36]. That each step happens to
/// preserve the 9000 aggregate exercise value is the authority's statement
/// preserving it, not this fixture's arithmetic.
/// </remarks>
public sealed class FX_CorporateActionMintsSuccessor
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly DateOnly Expiry = new(2026, 9, 18);

    private static readonly DateTimeOffset FirstObserved =
        new(2026, 5, 4, 12, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset SecondObserved =
        new(2026, 7, 6, 12, 0, 0, 0, TimeSpan.Zero);

    // Stated, not derived: the memo for each 3-for-2 states the successor's
    // strike and deliverable.
    private static readonly StatedSuccessorTerms FirstStated =
        new(Strike: 60m, DeliverableShares: 150, Multiplier: 100);

    private static readonly StatedSuccessorTerms SecondStated =
        new(Strike: 40m, DeliverableShares: 225, Multiplier: 100);

    [Fact]
    public void A_split_mints_a_new_identity_with_its_predecessor_recorded()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new CorporateActionWriter(connection);

        var origin = InsertOriginal(connection);
        var second = writer.MintSuccessor(
            origin, FirstStated, ThreeForTwo(new DateOnly(2026, 5, 5)), FirstObserved);
        var third = writer.MintSuccessor(
            second, SecondStated, ThreeForTwo(new DateOnly(2026, 7, 7)), SecondObserved);

        var lineage = new ContractLineage(connection).WalkFrom(third);

        Assert.Equal(3, lineage.Count);
        Assert.Equal([third, second, origin], lineage.Select(entry => entry.ContractId));
        Assert.Equal(
            [second, origin, null], lineage.Select(entry => entry.PredecessorContractId));

        // Three distinct identities: a mint never restates one.
        Assert.Equal(3, lineage.Select(entry => entry.Identity).Distinct().Count());
        Assert.Equal([225, 150, 100], lineage.Select(entry => entry.Identity.DeliverableShares));
    }

    /// <summary>
    /// The originals are unchanged, by row comparison across both later mints.
    /// </summary>
    [Fact]
    public void The_original_rows_are_unchanged_by_later_mints()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new CorporateActionWriter(connection);

        var origin = InsertOriginal(connection);
        var originBefore = RowOf(connection, origin);

        var second = writer.MintSuccessor(
            origin, FirstStated, ThreeForTwo(new DateOnly(2026, 5, 5)), FirstObserved);
        var secondBefore = RowOf(connection, second);

        writer.MintSuccessor(
            second, SecondStated, ThreeForTwo(new DateOnly(2026, 7, 7)), SecondObserved);

        Assert.Equal(originBefore, RowOf(connection, origin));
        Assert.Equal(secondBefore, RowOf(connection, second));
    }

    /// <summary>
    /// The 1.1 collision demonstration made permanent: the adjusted series at
    /// 60 with 150 shares lists beside a standard 60 with 100, two rows and
    /// two identities.
    /// </summary>
    [Fact]
    public void The_adjusted_series_lists_beside_the_standard_contract()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new CorporateActionWriter(connection);

        var origin = InsertOriginal(connection);
        writer.MintSuccessor(
            origin, FirstStated, ThreeForTwo(new DateOnly(2026, 5, 5)), FirstObserved);

        // A standard 60-strike contract on the same underlying, expiry and
        // right, admitted by the store beside the adjusted series.
        using var standard = connection.CreateCommand();
        standard.CommandText =
            """
            INSERT INTO contracts (symbol, expiry, right, strike)
            VALUES ($symbol, $expiry, 'put', '60.00000000');
            """;
        standard.Parameters.AddWithValue("$symbol", Symbol.Value);
        standard.Parameters.AddWithValue("$expiry", StoreDate.ToStored(Expiry));
        standard.ExecuteNonQuery();

        using var count = connection.CreateCommand();
        count.CommandText =
            "SELECT COUNT(*) FROM contracts WHERE strike = '60.00000000';";
        Assert.Equal(2L, count.ExecuteScalar());

        var adjusted = ContractIdentity.Of(
            Symbol, Expiry, OptionRight.Put, 60m, deliverableShares: 150);
        var plain = ContractIdentity.Of(Symbol, Expiry, OptionRight.Put, 60m);

        Assert.NotEqual(adjusted, plain);
    }

    private static CorporateAction ThreeForTwo(DateOnly exDate) =>
        new(CorporateActionKind.Split, exDate, Ratio: 1.5m);

    private static long InsertOriginal(SqliteConnection connection)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO contracts (symbol, expiry, right, strike)
            VALUES ($symbol, $expiry, 'put', '90.00000000')
            RETURNING contract_id;
            """;
        insert.Parameters.AddWithValue("$symbol", Symbol.Value);
        insert.Parameters.AddWithValue("$expiry", StoreDate.ToStored(Expiry));
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

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(FirstObserved);
        return store;
    }
}
