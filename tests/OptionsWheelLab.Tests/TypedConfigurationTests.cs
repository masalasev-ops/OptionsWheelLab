using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The typed accessors on both configuration surfaces.
/// </summary>
/// <remarks>
/// Not a registered fixture, so not named <c>FX-*</c>.
/// <para>
/// These exist so the canonical form is validated at the point of reading rather
/// than assumed, and so changing <see cref="StoreDecimal.Scale"/> stays one edit.
/// The ambient-culture argument does not apply: <c>InvariantGlobalization</c> is
/// on repository-wide.
/// </para>
/// </remarks>
public sealed class TypedConfigurationTests
{
    private const string DecimalKey = "Costs:CommissionPerContract";

    private const string IntKey = "Trial:MaxRolls";

    [Fact]
    public void A_stored_decimal_reads_back_as_the_same_decimal_at_a_simulated_date()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(
            DecimalKey,
            StoreDecimal.ToStored(0.65m),
            At(2026, 1, 10));

        var resolved = new AsOfConfiguration(connection)
            .ResolveDecimal(DecimalKey, new DateOnly(2026, 2, 1));

        Assert.Equal(0.65m, resolved);
    }

    /// <summary>
    /// The typed accessor resolves as-of like the untyped one, so the type does
    /// not quietly become a second read path [D-W26].
    /// </summary>
    [Fact]
    public void A_typed_read_resolves_the_version_in_force_not_the_newest()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        writer.Append(DecimalKey, StoreDecimal.ToStored(0.65m), At(2026, 1, 10));
        writer.Append(DecimalKey, StoreDecimal.ToStored(0.50m), At(2026, 6, 5));

        var asOf = new AsOfConfiguration(connection);

        Assert.Equal(0.65m, asOf.ResolveDecimal(DecimalKey, new DateOnly(2026, 2, 1)));
        Assert.Equal(0.50m, asOf.ResolveDecimal(DecimalKey, new DateOnly(2026, 7, 1)));
        Assert.Equal(0.50m, new CurrentConfiguration(connection).ResolveCurrentDecimal(DecimalKey));
    }

    /// <summary>
    /// An integer is not stored in the canonical decimal form, which is the
    /// obvious wrong inference from the two accessors sitting side by side.
    /// </summary>
    [Fact]
    public void An_integer_is_stored_plainly_and_not_in_the_decimal_form()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(IntKey, "7", At(2026, 1, 10));

        var asOf = new AsOfConfiguration(connection);

        Assert.Equal("7", asOf.Resolve(IntKey, new DateOnly(2026, 2, 1)));
        Assert.Equal(7, asOf.ResolveInt(IntKey, new DateOnly(2026, 2, 1)));
        Assert.Equal(7, new CurrentConfiguration(connection).ResolveCurrentInt(IntKey));
    }

    [Fact]
    public void A_key_with_no_version_by_the_as_of_date_reads_as_nothing()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(DecimalKey, StoreDecimal.ToStored(0.65m), At(2026, 6, 1));

        var asOf = new AsOfConfiguration(connection);

        Assert.Null(asOf.ResolveDecimal(DecimalKey, new DateOnly(2026, 1, 1)));
        Assert.Null(asOf.ResolveInt(IntKey, new DateOnly(2026, 1, 1)));
    }

    /// <summary>
    /// A value that is not in the stored form is refused at the point of
    /// reading, and the failure names the key.
    /// </summary>
    /// <remarks>
    /// The parser can say what is wrong with the text and cannot say which key
    /// carried it. A configuration failure that does not name its key is a
    /// search rather than a diagnosis.
    /// </remarks>
    [Fact]
    public void A_value_that_is_not_in_the_stored_form_is_refused_and_names_its_key()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        // Beyond the declared scale, so parsing it would round it silently.
        new ConfigWriter(connection).Append(DecimalKey, "0.123456789", At(2026, 1, 10));

        var thrown = Assert.Throws<FormatException>(
            () => new AsOfConfiguration(connection)
                .ResolveDecimal(DecimalKey, new DateOnly(2026, 2, 1)));

        Assert.Contains(DecimalKey, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("0.123456789", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_that_is_not_an_integer_is_refused_and_names_its_key()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(IntKey, "7.5", At(2026, 1, 10));

        var thrown = Assert.Throws<FormatException>(
            () => new AsOfConfiguration(connection).ResolveInt(IntKey, new DateOnly(2026, 2, 1)));

        Assert.Contains(IntKey, thrown.Message, StringComparison.Ordinal);
    }

    private static DateTimeOffset At(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(At(2026, 1, 1));
        return store;
    }
}
