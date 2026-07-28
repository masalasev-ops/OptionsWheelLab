using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-MoneyRoundTrip: adversarial decimals survive storage, and equal values
/// written differently store identically.
/// </summary>
/// <remarks>
/// <b>No floating-point value is constructed anywhere in this file, and that is
/// deliberate.</b> The build plan asks for values that lose precision as
/// doubles, and the definition of done for this same checkpoint bans the type
/// from the tree with no exemption mechanism. Both are satisfiable at once: the
/// values below are exactly the ones binary floating point cannot represent, and
/// the property under test is that they round-trip EXACTLY. Demonstrating that
/// needs the values, not the type.
/// <para>
/// If a case ever genuinely needs one, that is the first recorded decision the
/// no-exemption policy exists to force, and it gets taken deliberately rather
/// than by reaching for an exemption at a failing run.
/// </para>
/// </remarks>
public sealed class FX_MoneyRoundTrip
{
    /// <summary>
    /// The identity property, and the reason D-W29 exists. A strike written
    /// three ways is one contract, not three.
    /// </summary>
    [Fact]
    public void Equal_values_written_differently_produce_one_stored_string()
    {
        Assert.Equal(StoreDecimal.ToStored(50m), StoreDecimal.ToStored(50.0m));
        Assert.Equal(StoreDecimal.ToStored(50m), StoreDecimal.ToStored(50.00m));
        Assert.Equal(StoreDecimal.ToStored(50m), StoreDecimal.ToStored(50.000000000000m));
    }

    /// <summary>
    /// Negative zero, asserted as an equality between two strings rather than
    /// listed as another round-trip case.
    /// </summary>
    /// <remarks>
    /// <c>-0.0m</c> equals <c>0m</c>, so if the sign survived rendering, zero
    /// would have two stored forms and the canonical property would be false for
    /// the one value most likely to appear. A case in a list can be satisfied by
    /// whatever the runtime happens to do; an equality cannot.
    /// </remarks>
    [Fact]
    public void Negative_zero_and_zero_produce_one_stored_string()
    {
        Assert.Equal(StoreDecimal.ToStored(0m), StoreDecimal.ToStored(-0.0m));
        Assert.Equal(StoreDecimal.ToStored(0m), StoreDecimal.ToStored(decimal.Negate(0m)));
        Assert.Equal(StoreDecimal.ToStored(0m), StoreDecimal.ToStored(0m * -1m));
        Assert.Equal(StoreDecimal.ToStored(0m), StoreDecimal.ToStored(StoreDecimal.ParseStored("-0.00")));
    }

    /// <summary>
    /// Values binary floating point cannot represent exactly, which is why money
    /// is decimal. Each round-trips without loss.
    /// </summary>
    [Fact]
    public void Values_that_lose_precision_as_doubles_round_trip_exactly()
    {
        foreach (var value in new[] { 0.1m, 0.2m, 0.3m, 1.005m, 0.65m, 52.40m })
        {
            Assert.Equal(value, StoreDecimal.ParseStored(StoreDecimal.ToStored(value)));
        }

        // 0.1 + 0.2 == 0.3 holds in decimal and does not in binary. Asserting
        // the decimal side is the whole point: the type is chosen so this is
        // true, and no counter-example needs constructing to say so.
        Assert.Equal(StoreDecimal.ToStored(0.3m), StoreDecimal.ToStored(0.1m + 0.2m));
    }

    /// <summary>
    /// The worked example's own figures, so the fixture is anchored to arithmetic
    /// the corpus performs rather than to invented values.
    /// </summary>
    [Fact]
    public void The_worked_examples_figures_round_trip_exactly()
    {
        foreach (var value in new[] { 49.0565m, 0.9435m, 498.05m, 94.35m, 5250.00m, -0.24m })
        {
            Assert.Equal(value, StoreDecimal.ParseStored(StoreDecimal.ToStored(value)));
        }
    }

    [Fact]
    public void A_value_at_the_scale_boundary_is_admitted_and_round_trips()
    {
        const decimal Smallest = 0.00000001m;

        Assert.Equal(Smallest, StoreDecimal.ParseStored(StoreDecimal.ToStored(Smallest)));
        Assert.Equal(-Smallest, StoreDecimal.ParseStored(StoreDecimal.ToStored(-Smallest)));
    }

    /// <summary>
    /// One place beyond the scale is refused rather than rounded, because this
    /// path is for values that must be exact.
    /// </summary>
    [Fact]
    public void A_value_beyond_the_scale_is_refused_rather_than_rounded()
    {
        const decimal TooPrecise = 0.000000001m;

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => StoreDecimal.ToStored(TooPrecise));

        Assert.Contains("scale", thrown.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A non-terminating quotient, which is the shape Phase 5's primary output
    /// takes. The refusing path cannot store it and the rounding path can, which
    /// is why there are two.
    /// </summary>
    [Fact]
    public void A_non_terminating_ratio_refuses_exactly_and_stores_rounded()
    {
        // The worked example's first candidate: 29.35 credit on 4,500.00
        // committed. In decimal this carries 28 fractional digits.
        var returnOnCommitted = 29.35m / 4500.00m;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => StoreDecimal.ToStored(returnOnCommitted));

        var stored = StoreDecimal.ToStoredRounded(returnOnCommitted);

        Assert.Equal("0.00652222", stored);
        Assert.Equal(StoreDecimal.ParseStored(stored), StoreDecimal.ParseStored(stored));
    }

    [Fact]
    public void A_rounded_value_round_trips()
    {
        var rounded = StoreDecimal.ParseStored(StoreDecimal.ToStoredRounded(1m / 3m));

        Assert.Equal(rounded, StoreDecimal.ParseStored(StoreDecimal.ToStored(rounded)));
    }

    /// <summary>
    /// The midpoint rule, pinned against the default it disagrees with.
    /// </summary>
    /// <remarks>
    /// <see cref="MidpointRounding.ToEven"/> is <c>decimal.Round</c>'s default
    /// and would render 0.00000002 here. Away from zero renders 0.00000003. The
    /// two differ on exactly the values a ranking sits on, so the choice is
    /// asserted rather than inherited.
    /// </remarks>
    [Fact]
    public void A_midpoint_rounds_away_from_zero_not_to_even()
    {
        Assert.Equal("0.00000003", StoreDecimal.ToStoredRounded(0.000000025m));
        Assert.Equal("-0.00000003", StoreDecimal.ToStoredRounded(-0.000000025m));
    }

    [Fact]
    public void The_representable_bounds_round_trip()
    {
        Assert.Equal(
            StoreDecimal.MaxRepresentable,
            StoreDecimal.ParseStored(StoreDecimal.ToStored(StoreDecimal.MaxRepresentable)));

        Assert.Equal(
            StoreDecimal.MinRepresentable,
            StoreDecimal.ParseStored(StoreDecimal.ToStored(StoreDecimal.MinRepresentable)));
    }

    /// <summary>
    /// Beyond the bound both entry points refuse, and the message names
    /// magnitude rather than scale.
    /// </summary>
    /// <remarks>
    /// Rounding bounds precision and not magnitude, so without the check on the
    /// rounding path a large computed value would round cleanly and then render
    /// a string the parser could not read back: the storage layer emitting its
    /// own unreadable output. No realistic lab value approaches this, which is
    /// the argument for making the branch explicit rather than for omitting it.
    /// </remarks>
    [Fact]
    public void A_magnitude_beyond_the_bound_is_refused_by_both_entry_points()
    {
        var exact = Assert.Throws<ArgumentOutOfRangeException>(
            () => StoreDecimal.ToStored(decimal.MaxValue));

        var rounded = Assert.Throws<ArgumentOutOfRangeException>(
            () => StoreDecimal.ToStoredRounded(decimal.MaxValue));

        Assert.Contains("magnitude", exact.Message, StringComparison.Ordinal);
        Assert.Contains("magnitude", rounded.Message, StringComparison.Ordinal);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => StoreDecimal.ToStoredRounded(decimal.MinValue));
    }

    /// <summary>
    /// The bound is derived from the scale, not written down.
    /// </summary>
    /// <remarks>
    /// It is 792281625142643375935.43950335 at scale 8, which is easy to
    /// transcribe with the point in the wrong place, and an earlier draft of this
    /// checkpoint's plan did exactly that. Asserting the arithmetic rather than
    /// the digits means raising the scale moves this test with the constant.
    /// </remarks>
    [Fact]
    public void The_bound_is_the_full_mantissa_scaled_and_carries_the_declared_places()
    {
        var rendered = StoreDecimal.ToStored(StoreDecimal.MaxRepresentable);
        var point = rendered.IndexOf('.', StringComparison.Ordinal);

        Assert.Equal(StoreDecimal.Scale, rendered.Length - point - 1);

        // Every significant digit of a decimal, so no larger value exists at
        // this scale.
        Assert.Equal(
            decimal.MaxValue,
            StoreDecimal.MaxRepresentable * decimal.Parse(
                "1" + new string('0', StoreDecimal.Scale),
                System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Parsing is lenient about padding, because a config row written by hand at
    /// 0.8 carries 0.35 rather than the padded form.
    /// </summary>
    [Fact]
    public void An_unpadded_value_parses()
    {
        Assert.Equal(0.35m, StoreDecimal.ParseStored("0.35"));
        Assert.Equal(50m, StoreDecimal.ParseStored("50"));
        Assert.Equal(-0.35m, StoreDecimal.ParseStored("-0.35"));
    }

    /// <summary>
    /// Parsing is strict about precision, and the check runs on the string.
    /// </summary>
    /// <remarks>
    /// <c>decimal.Parse</c> silently rounds an input carrying more than 29
    /// significant digits rather than throwing. Counting places on the parsed
    /// value would therefore be too late: the row would read back as a different
    /// number than the row states, with no error, in a store whose whole purpose
    /// is that a later behaviour change can be explained after the fact.
    /// </remarks>
    [Fact]
    public void A_string_carrying_more_places_than_the_scale_is_refused()
    {
        var thrown = Assert.Throws<FormatException>(
            () => StoreDecimal.ParseStored("0.123456789"));

        Assert.Contains("decimal places", thrown.Message, StringComparison.Ordinal);

        Assert.Throws<FormatException>(
            () => StoreDecimal.ParseStored("0.123456789012345678901234567890123"));
    }

    [Fact]
    public void An_exponent_or_a_group_separator_is_refused()
    {
        Assert.Throws<FormatException>(() => StoreDecimal.ParseStored("1e3"));
        Assert.Throws<FormatException>(() => StoreDecimal.ParseStored("1,000.00"));
        Assert.Throws<FormatException>(() => StoreDecimal.ParseStored(""));
    }

    /// <summary>
    /// Canonicalise takes the refusing path, because a strike is exact.
    /// </summary>
    [Fact]
    public void Canonicalise_agrees_with_the_stored_form_and_refuses_what_it_cannot_hold()
    {
        Assert.Equal(StoreDecimal.Canonicalise(50m), StoreDecimal.Canonicalise(50.00m));
        Assert.Equal(
            StoreDecimal.ToStored(50m),
            StoreDecimal.ToStored(StoreDecimal.Canonicalise(50.00m)));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => StoreDecimal.Canonicalise(0.000000001m));
    }
}
