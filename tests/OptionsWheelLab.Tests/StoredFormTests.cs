using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The stored forms of a date and a contract right.
/// </summary>
/// <remarks>
/// Not registered fixtures, so deliberately not named <c>FX-*</c>:
/// FX-RegistryMatchesDisk requires every <c>FX-*.cs</c> to have a row in
/// <c>FIXTURES.md</c>.
/// <para>
/// Both exist for one reason. The obvious rendering is culture-independent,
/// plausible, and wrong, and <c>InvariantGlobalization</c> means no culture test
/// could ever catch either.
/// </para>
/// </remarks>
public sealed class StoredFormTests
{
    [Fact]
    public void A_date_renders_in_the_declared_form_and_round_trips()
    {
        var date = new DateOnly(2026, 7, 3);

        Assert.Equal("2026-07-03", StoreDate.ToStored(date));
        Assert.Equal(date, StoreDate.ParseStored(StoreDate.ToStored(date)));
    }

    /// <summary>
    /// The coupling asserted rather than assumed.
    /// </summary>
    /// <remarks>
    /// Under <c>InvariantGlobalization</c> a bare <c>ToString()</c> on a date
    /// gives <c>MM/dd/yyyy</c>, which cannot vary by machine and is still the
    /// wrong form: it sorts by month, and every as-of read is a string
    /// comparison. Stating that the two differ is the only way to notice if
    /// someone reaches for the shorter call.
    /// </remarks>
    [Fact]
    public void The_stored_date_form_is_not_the_invariant_short_date()
    {
        var date = new DateOnly(2026, 7, 3);

        // The culture-less call is the point of the assertion, not an oversight.
        var invariantShortDate = date.ToString();

        Assert.NotEqual(invariantShortDate, StoreDate.ToStored(date));
        Assert.Equal("07/03/2026", invariantShortDate);
    }

    [Fact]
    public void A_date_that_is_not_the_stored_form_is_refused()
    {
        Assert.Throws<FormatException>(() => StoreDate.ParseStored("07/03/2026"));
        Assert.Throws<FormatException>(() => StoreDate.ParseStored("2026-7-3"));
    }

    [Fact]
    public void A_contract_right_stores_lower_case_and_round_trips()
    {
        Assert.Equal("put", StoreOptionRight.ToStored(OptionRight.Put));
        Assert.Equal("call", StoreOptionRight.ToStored(OptionRight.Call));

        Assert.Equal(OptionRight.Put, StoreOptionRight.ParseStored("put"));
        Assert.Equal(OptionRight.Call, StoreOptionRight.ParseStored("call"));
    }

    /// <summary>
    /// The enum's own spelling is not the stored form, and nothing derives one
    /// from the other.
    /// </summary>
    [Fact]
    public void The_stored_right_is_not_the_enum_spelling()
    {
        Assert.NotEqual(nameof(OptionRight.Put), StoreOptionRight.ToStored(OptionRight.Put));
        Assert.Throws<FormatException>(() => StoreOptionRight.ParseStored("Put"));
    }

    /// <summary>
    /// An unrecognised value is refused rather than defaulting, which is why
    /// <see cref="OptionRight"/> starts at one.
    /// </summary>
    [Fact]
    public void An_unrecognised_right_is_refused_rather_than_defaulted()
    {
        Assert.Throws<FormatException>(() => StoreOptionRight.ParseStored("straddle"));
        Assert.Throws<FormatException>(() => StoreOptionRight.ParseStored(""));

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => StoreOptionRight.ToStored(default));

        Assert.Contains("not a contract right", thrown.Message, StringComparison.Ordinal);
    }
}
