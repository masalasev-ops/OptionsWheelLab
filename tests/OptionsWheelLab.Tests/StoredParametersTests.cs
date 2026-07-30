using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Every AddStored overload renders exactly as its Store* form does, asserted
/// where the coupling lives.
/// </summary>
/// <remarks>
/// Not a registered fixture, for the same reason as
/// <see cref="BarsSchemaTests"/>.
/// </remarks>
public sealed class StoredParametersTests
{
    [Fact]
    public void A_decimal_binds_as_its_stored_form()
    {
        using var command = new SqliteCommand();

        command.Parameters.AddStored("$value", 52.4m);

        Assert.Equal(StoreDecimal.ToStored(52.4m), command.Parameters["$value"].Value);
    }

    /// <summary>
    /// The refusing path, not the rounding one: a value beyond the scale is an
    /// error at the bind, never quietly rounded on the way to the store.
    /// </summary>
    [Fact]
    public void A_decimal_needing_rounding_is_refused_at_the_bind()
    {
        using var command = new SqliteCommand();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => command.Parameters.AddStored("$value", 0.123456789m));
    }

    [Fact]
    public void A_null_decimal_binds_as_dbnull_and_a_present_one_as_its_form()
    {
        using var command = new SqliteCommand();

        command.Parameters.AddStored("$absent", (decimal?)null);
        command.Parameters.AddStored("$present", (decimal?)0.35m);

        Assert.Equal(DBNull.Value, command.Parameters["$absent"].Value);
        Assert.Equal(StoreDecimal.ToStored(0.35m), command.Parameters["$present"].Value);
    }

    [Fact]
    public void A_date_binds_as_its_stored_form()
    {
        using var command = new SqliteCommand();

        command.Parameters.AddStored("$date", new DateOnly(2026, 3, 2));

        Assert.Equal(
            StoreDate.ToStored(new DateOnly(2026, 3, 2)), command.Parameters["$date"].Value);
    }

    [Fact]
    public void An_instant_binds_as_its_stored_form()
    {
        var instant = new DateTimeOffset(2026, 3, 2, 21, 0, 0, 0, TimeSpan.Zero);
        using var command = new SqliteCommand();

        command.Parameters.AddStored("$at", instant);

        Assert.Equal(StoreTimestamp.ToStored(instant), command.Parameters["$at"].Value);
    }

    [Fact]
    public void A_right_binds_as_its_stored_form()
    {
        using var command = new SqliteCommand();

        command.Parameters.AddStored("$right", OptionRight.Put);

        Assert.Equal(StoreOptionRight.ToStored(OptionRight.Put), command.Parameters["$right"].Value);
    }
}
