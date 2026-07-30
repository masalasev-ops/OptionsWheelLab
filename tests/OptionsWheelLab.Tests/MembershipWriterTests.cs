using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The membership writer appends versions the way the config writer does.
/// </summary>
/// <remarks>
/// Not a registered fixture, for the same reason as
/// <see cref="MembershipStoreTests"/>.
/// </remarks>
public sealed class MembershipWriterTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");
    private static readonly Ticker Other = Ticker.Normalise("OTHR");

    private static readonly DateOnly EffectiveOn = new(2026, 3, 1);

    private static readonly DateTimeOffset Observed =
        new(2026, 3, 1, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Two_appends_for_one_symbol_produce_versions_one_and_two()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        var first = writer.Append(Symbol, MembershipKind.Joined, EffectiveOn, Observed);
        var second = writer.Append(
            Symbol, MembershipKind.Left, EffectiveOn.AddMonths(5), Observed.AddDays(1));

        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public void A_different_symbol_is_versioned_independently()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Symbol, MembershipKind.Joined, EffectiveOn, Observed);
        writer.Append(Symbol, MembershipKind.Left, EffectiveOn.AddMonths(5), Observed.AddDays(1));

        Assert.Equal(1, writer.Append(Other, MembershipKind.Joined, EffectiveOn, Observed.AddDays(2)));
    }

    /// <summary>
    /// The kind lands in the declared stored form, not the enum's spelling.
    /// </summary>
    [Fact]
    public void The_kind_is_rendered_through_the_declared_form()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Symbol, MembershipKind.Joined, EffectiveOn, Observed);
        writer.Append(Symbol, MembershipKind.Left, EffectiveOn.AddMonths(5), Observed.AddDays(1));

        using var read = connection.CreateCommand();
        read.CommandText = "SELECT kind FROM watchlist_membership ORDER BY version;";

        var kinds = new List<string>();
        using var reader = read.ExecuteReader();

        while (reader.Read())
        {
            kinds.Add(reader.GetString(0));
        }

        Assert.Equal([StoreMembershipKind.Joined, StoreMembershipKind.Left], kinds);
    }

    [Fact]
    public void The_stored_form_round_trips()
    {
        Assert.Equal(
            MembershipKind.Joined,
            StoreMembershipKind.ParseStored(StoreMembershipKind.ToStored(MembershipKind.Joined)));
        Assert.Equal(
            MembershipKind.Left,
            StoreMembershipKind.ParseStored(StoreMembershipKind.ToStored(MembershipKind.Left)));
    }

    [Fact]
    public void A_reason_is_optional_and_stored_when_given()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Symbol, MembershipKind.Joined, EffectiveOn, Observed);
        writer.Append(
            Symbol,
            MembershipKind.Left,
            EffectiveOn.AddMonths(5),
            Observed.AddDays(1),
            reason: "liquidity fell below the gate floor");

        using var read = connection.CreateCommand();
        read.CommandText = "SELECT reason FROM watchlist_membership ORDER BY version;";

        var reasons = new List<string?>();
        using var reader = read.ExecuteReader();

        while (reader.Read())
        {
            reasons.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
        }

        Assert.Equal([null, "liquidity fell below the gate floor"], reasons);
    }

    /// <summary>
    /// The writer's refusal names both instants, which the store trigger's
    /// fixed message cannot.
    /// </summary>
    [Fact]
    public void A_backdated_stamp_is_refused_and_names_both_instants()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Symbol, MembershipKind.Joined, EffectiveOn, Observed);

        var refusal = Assert.Throws<InvalidOperationException>(
            () => writer.Append(
                Symbol, MembershipKind.Left, EffectiveOn.AddMonths(5), Observed.AddDays(-1)));

        Assert.Contains(StoreTimestamp.ToStored(Observed), refusal.Message, StringComparison.Ordinal);
        Assert.Contains(
            StoreTimestamp.ToStored(Observed.AddDays(-1)), refusal.Message, StringComparison.Ordinal);
        Assert.Equal(1L, CountRows(connection));
    }

    [Fact]
    public void An_equal_stamp_is_allowed()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Symbol, MembershipKind.Joined, EffectiveOn, Observed);
        var second = writer.Append(Symbol, MembershipKind.Left, EffectiveOn.AddMonths(5), Observed);

        Assert.Equal(2, second);
    }

    private static long CountRows(SqliteConnection connection)
    {
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM watchlist_membership;";
        return (long)count.ExecuteScalar()!;
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Observed);
        return store;
    }
}
