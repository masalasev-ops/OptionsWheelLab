using System.Globalization;

namespace OptionsWheelLab.Core.Storage;

/// <summary>
/// The canonical stored form of a decimal [D-W29].
/// </summary>
/// <remarks>
/// One number has exactly one stored representation. <c>50</c>, <c>50.0</c> and
/// <c>50.00</c> are the same value and would be three different strings, and a
/// strike participates in contract identity, so a non-canonical form would give
/// one contract two identities and split its history without ever failing.
/// <para>
/// <b>Two entry points, and the difference is the point.</b>
/// <see cref="ToStored"/> refuses a value it cannot represent exactly, for
/// vendor quotes, strikes and ledger amounts, where the scale is a fidelity
/// requirement and losing a digit quietly is the failure.
/// <see cref="ToStoredRounded"/> rounds, and is the only path that does, for
/// computed values, where the scale is a rounding policy. Decimal division is
/// non-terminating in general, so a single refusing entry point could not store
/// a return on committed capital at all. Rounding is therefore a visible choice
/// at the call site rather than a silent property of the storage layer.
/// </para>
/// <para>
/// <b>Not order-preserving, deliberately.</b> The integer part is variable
/// width, so <c>"9.00000000"</c> sorts above <c>"10.00000000"</c>, and negatives
/// invert again. No SQL orders, ranges over, or aggregates a column holding this
/// form; comparison and arithmetic happen here, after parsing [D-W29].
/// </para>
/// </remarks>
public static class StoreDecimal
{
    /// <summary>
    /// Decimal places every stored decimal carries.
    /// </summary>
    /// <remarks>
    /// <b>This is not a tunable.</b> Changing it reinterprets every stored row,
    /// so it is a migration rather than an edit, in the same class as
    /// <see cref="StoreTimestamp.StoredFormat"/>. It is not a config key for
    /// that reason.
    /// <para>
    /// Driven by <c>contract_quotes.gamma</c>, with <c>iv</c> alongside. Those
    /// are vendor values, so they are the ones that can FAIL rather than merely
    /// round: a scale below the vendor's precision refuses the quote and stops
    /// ingestion. Gamma is the smallest greek and needs the most places to stay
    /// significant. The precision figure is assumed rather than measured, since
    /// the options add-on is not purchased until Phase 8 and nothing in the
    /// corpus states it; six decimals is the assumption and this carries two
    /// digits of headroom. Being generous costs two characters per stored
    /// number; being short costs a Phase 8 failure.
    /// </para>
    /// <para>
    /// The measured figures bound it from below and sit well inside: the worked
    /// example's net basis 49.0565 and premium per share 0.9435 need four
    /// places, and regret quoted to 9.309 pp needs five as a fraction.
    /// </para>
    /// </remarks>
    public const int Scale = 8;

    /// <summary>
    /// The format string that produces the canonical form.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="Scale"/> rather than written as a literal, so the
    /// two cannot disagree.
    /// </remarks>
    public static readonly string StoredFormat =
        "F" + Scale.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The largest magnitude representable at <see cref="Scale"/>.
    /// </summary>
    /// <remarks>
    /// Computed from <see cref="Scale"/>, never typed. A decimal is a 96-bit
    /// mantissa with a scale, so setting every mantissa bit and applying the
    /// scale gives the bound by construction: it cannot disagree with
    /// <see cref="Scale"/> and it cannot be transcribed wrongly. At scale 8 it
    /// is 792281625142643375935.43950335, which is easy to write down with the
    /// point in the wrong place.
    /// </remarks>
    public static readonly decimal MaxRepresentable =
        new(lo: -1, mid: -1, hi: -1, isNegative: false, scale: Scale);

    /// <summary>The negation of <see cref="MaxRepresentable"/>.</summary>
    public static readonly decimal MinRepresentable = -MaxRepresentable;

    /// <summary>
    /// The canonical form of a value that must be exact.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value needs more than <see cref="Scale"/> decimal places, or its
    /// magnitude exceeds <see cref="MaxRepresentable"/>.
    /// </exception>
    public static string ToStored(decimal value)
    {
        RefuseUnrepresentableMagnitude(value);

        if (decimal.Round(value, Scale, MidpointRounding.AwayFromZero) != value)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"{value} is not exactly representable at scale {Scale}, and this path refuses "
                + "rather than rounds because the value must be exact. Use "
                + $"{nameof(ToStoredRounded)} for a computed value.");
        }

        return Render(value);
    }

    /// <summary>
    /// The canonical form of a computed value, rounded to <see cref="Scale"/>.
    /// </summary>
    /// <remarks>
    /// <b>Away from zero at the midpoint, named rather than defaulted.</b>
    /// <see cref="MidpointRounding.ToEven"/> is <c>decimal.Round</c>'s default
    /// and disagrees with this on exactly the values a ranking will sit on.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The magnitude exceeds <see cref="MaxRepresentable"/>. Rounding bounds
    /// precision and not magnitude, so this path refuses on magnitude too:
    /// without it a large computed value would round cleanly and then render a
    /// string <see cref="ParseStored"/> could not read back.
    /// </exception>
    public static string ToStoredRounded(decimal value)
    {
        RefuseUnrepresentableMagnitude(value);

        return Render(decimal.Round(value, Scale, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// The value a stored string carries.
    /// </summary>
    /// <remarks>
    /// <b>Lenient on padding, strict on precision.</b> A hand-written config row
    /// carries <c>0.35</c> rather than the padded form and must still read, so
    /// this accepts any exact invariant decimal literal. It refuses more than
    /// <see cref="Scale"/> decimal places, counted on the STRING rather than on
    /// the parsed value, because <c>decimal.Parse</c> silently rounds an input
    /// carrying more than 29 significant digits rather than throwing. Without
    /// that check a row could read back as a different number than the row says,
    /// with no error, in a store whose purpose is that a later behaviour change
    /// can be explained after the fact.
    /// <para>
    /// <b>"Lenient on padding" means shorter than the stored form, not longer.</b>
    /// <c>0.35</c> is accepted and <c>0.350000000</c> is refused, though the nine
    /// zeros carry no information and the value is exactly representable. So the
    /// same logical value is admitted from a <c>decimal</c>, whose own scale may
    /// be 28 when the extra digits are zero, and refused from a string. The
    /// asymmetry is deliberate: at most <see cref="Scale"/> PLACES is a simpler
    /// contract to state and to check than at most <see cref="Scale"/>
    /// SIGNIFICANT places, and nothing writes the padded form except this type.
    /// </para>
    /// <para>
    /// So <c>Parse(Render(x)) == x</c> holds for every x in the domain, while
    /// <c>Render(Parse(s)) == s</c> deliberately does not.
    /// </para>
    /// </remarks>
    /// <exception cref="FormatException">
    /// The string is not an invariant decimal literal, or carries more than
    /// <see cref="Scale"/> decimal places.
    /// </exception>
    public static decimal ParseStored(string stored)
    {
        ArgumentNullException.ThrowIfNull(stored);

        var places = DecimalPlacesIn(stored);

        if (places > Scale)
        {
            throw new FormatException(
                $"'{stored}' carries {places} decimal places and the stored form carries at most "
                + $"{Scale}. Parsing it would round it silently, so the value a row reads back as "
                + "would differ from the value the row states.");
        }

        return decimal.Parse(
            stored,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The value as it will be stored, for a value that must be exact.
    /// </summary>
    /// <remarks>
    /// Used where a decimal is held in memory and compared, so that the
    /// in-memory value and the stored string agree. It goes through the refusing
    /// path: a strike is exact, never rounded.
    /// </remarks>
    public static decimal Canonicalise(decimal value) => ParseStored(ToStored(value));

    /// <summary>
    /// Renders a value already known to be representable.
    /// </summary>
    /// <remarks>
    /// <b>Negative zero collapses to zero here, and the guard is defensive
    /// rather than load-bearing.</b> <c>-0.0m</c> equals <c>0m</c>, so a
    /// surviving sign would give one value two stored strings, which is the
    /// single property this type exists to prevent. Measured: the runtime
    /// already renders <c>-0.0m</c> as <c>0.00000000</c>, because the .NET Core
    /// 3.0 change that made negative zero print its sign is scoped to IEEE
    /// floating point and does not reach <c>decimal</c>. The guard stays so the
    /// property is a property of this code rather than of that detail, and
    /// FX-MoneyRoundTrip asserts it either way.
    /// </remarks>
    private static string Render(decimal value)
    {
        var rendered = (value == 0m ? 0m : value)
            .ToString(StoredFormat, CultureInfo.InvariantCulture);

        return rendered;
    }

    private static void RefuseUnrepresentableMagnitude(decimal value)
    {
        if (value >= MinRepresentable && value <= MaxRepresentable)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            $"{value} has a magnitude beyond {MaxRepresentable}, which is the largest value "
            + $"representable at scale {Scale}. Rendering it would produce a string that cannot "
            + "be read back.");
    }

    /// <summary>
    /// Decimal places the string carries, counted before parsing.
    /// </summary>
    private static int DecimalPlacesIn(string stored)
    {
        var point = stored.IndexOf('.', StringComparison.Ordinal);

        return point < 0 ? 0 : stored.Length - point - 1;
    }
}
