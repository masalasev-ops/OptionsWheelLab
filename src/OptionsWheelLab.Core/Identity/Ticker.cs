using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace OptionsWheelLab.Core.Identity;

/// <summary>
/// An underlying symbol in the bare EODHD dash form, for example
/// <c>BRK-B</c>.
/// </summary>
/// <remarks>
/// A ticker has two forms and they are not interchangeable. The store uses the
/// bare dash form; requests to the vendor take the exchange-suffixed form,
/// <c>BRK-B.US</c>. The suffix is added at the boundary and never stored, so a
/// stored ticker is always comparable to another stored ticker.
/// <para>
/// <b>Constructed only through <see cref="Normalise"/>.</b> A ticker carrying an
/// exchange suffix therefore cannot exist, which makes "a stored form never
/// carries a suffix" a property of the type rather than something a test checks
/// over a sample. A <c>record struct</c> would admit <c>default</c> and reopen
/// that, so this is a reference type.
/// </para>
/// <para>
/// <b>Refusing beats guessing, and that is the load-bearing choice.</b> EODHD
/// suffixes are not only country codes: <c>GSPC.INDX</c>, <c>EURUSD.FOREX</c>
/// and <c>BTC-USD.CC</c> all exist. A rule that turned every dot into a dash
/// would mint <c>GSPC-INDX</c>, a ticker that matches nothing and never fails.
/// A refusal naming the known set is loud, and the lab trades US common stock,
/// so it costs nothing today.
/// </para>
/// </remarks>
public sealed record Ticker
{
    /// <summary>
    /// The exchange suffix the vendor expects, without its dot.
    /// </summary>
    /// <remarks>
    /// A constant rather than a config key. It gains a second value only when
    /// the lab trades a second exchange, which is not before Phase 8, and adding
    /// a key means adding a row to a document authored elsewhere.
    /// </remarks>
    public const string VendorExchange = "US";

    /// <summary>
    /// Exchange suffixes that may be stripped. One entry, deliberately.
    /// </summary>
    /// <remarks>
    /// Widening this is not free. <c>BRK.B</c> normalises correctly only because
    /// <c>B</c> is not treated as an exchange, and EODHD's codes include
    /// single-letter and two-letter codes that collide with share-class letters.
    /// The length rule below, not this set, is what keeps <c>BRK.B</c> safe.
    /// </remarks>
    private static readonly string[] KnownExchanges = [VendorExchange];

    /// <summary>The permitted shape of a stored ticker.</summary>
    private static readonly Regex StoredShape =
        new("^[A-Z0-9]+(-[A-Z0-9]+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// ASCII only, checked BEFORE upper-casing. <c>ToUpperInvariant</c> maps some
    /// non-ASCII letters into ASCII, so filtering afterwards would silently
    /// accept a homoglyph as a valid ticker.
    /// </summary>
    private static readonly Regex AsciiOnly =
        new("^[A-Za-z0-9.-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private Ticker(string value) => Value = value;

    /// <summary>The bare dash form, as stored.</summary>
    public string Value { get; }

    /// <summary>
    /// The bare dash form of a raw symbol.
    /// </summary>
    /// <exception cref="FormatException">
    /// The symbol is empty, carries characters a ticker cannot, or carries a
    /// dot-suffix that is neither a share class nor a known exchange.
    /// </exception>
    public static Ticker Normalise(string raw)
    {
        if (!TryNormalise(raw, out var ticker, out var reason))
        {
            throw new FormatException(reason);
        }

        return ticker;
    }

    /// <summary>
    /// The bare dash form, or false with the reason.
    /// </summary>
    public static bool TryNormalise(
        string? raw,
        [NotNullWhen(true)] out Ticker? ticker,
        [NotNullWhen(false)] out string? reason)
    {
        ticker = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            reason = "A ticker cannot be empty.";
            return false;
        }

        var trimmed = raw.Trim();

        if (!AsciiOnly.IsMatch(trimmed))
        {
            reason =
                $"'{raw}' carries characters a ticker cannot. Only letters, digits, dots and "
                + "dashes are read, and other vendors' conventions such as '^GSPC' or 'BRK/B' are "
                + "refused rather than mapped.";
            return false;
        }

        var upper = trimmed.ToUpperInvariant();

        if (!TryStripExchange(upper, out var bare, out reason))
        {
            return false;
        }

        // Any dot still present is a share-class separator, since the exchange
        // suffix is gone by now.
        var dashed = bare.Replace('.', '-');

        if (!StoredShape.IsMatch(dashed))
        {
            reason =
                $"'{raw}' does not normalise to a ticker. The stored form is letters and digits in "
                + "dash-separated segments, for example 'BRK-B'.";
            return false;
        }

        ticker = new Ticker(dashed);
        reason = null;
        return true;
    }

    /// <summary>
    /// The exchange-suffixed form the vendor expects.
    /// </summary>
    /// <remarks>
    /// The one place a suffix is ever added. It has no caller until the ingest
    /// path exists at Phase 8; it lives here so that when one arrives there is
    /// somewhere for it to go other than a string concatenation at a call site.
    /// </remarks>
    public string ToVendor() => $"{Value}.{VendorExchange}";

    public override string ToString() => Value;

    /// <summary>
    /// Removes a trailing exchange suffix, deciding what a dot-suffix is by its
    /// length.
    /// </summary>
    /// <remarks>
    /// <b>One letter is a share class; two to five is an exchange code.</b>
    /// <c>BRK.B</c> is genuinely ambiguous between "class B" and "ticker BRK on
    /// exchange B", and this rule is the convention that resolves it, not a fact
    /// about the world. It holds for every US name I can name and has not been
    /// checked exhaustively.
    /// <para>
    /// An unknown code-shaped suffix is refused rather than dashed, because
    /// <c>GSPC.INDX</c> becoming <c>GSPC-INDX</c> is a silent corruption and a
    /// refusal is not.
    /// </para>
    /// </remarks>
    private static bool TryStripExchange(
        string upper,
        out string bare,
        [NotNullWhen(false)] out string? reason)
    {
        bare = upper;
        reason = null;

        var lastDot = upper.LastIndexOf('.');

        if (lastDot < 0)
        {
            return true;
        }

        var suffix = upper[(lastDot + 1)..];

        if (suffix.Length <= 1)
        {
            // A share class, or a trailing dot the shape check will refuse.
            return true;
        }

        if (KnownExchanges.Contains(suffix, StringComparer.Ordinal))
        {
            bare = upper[..lastDot];
            return true;
        }

        reason =
            $"'{upper}' carries the suffix '.{suffix}', which is not a share class and is not "
            + $"among the exchanges this lab reads ({string.Join(", ", KnownExchanges)}). It is "
            + "refused rather than converted, because turning '.INDX' into '-INDX' would mint a "
            + "ticker that matches nothing and never fails.";
        return false;
    }
}
