using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-TickerDashForm: dot and dash ticker forms normalise together.
/// </summary>
/// <remarks>
/// The registered assertion is satisfied by a constant function, so the fixture
/// also carries the two sides that make it mean something: distinct symbols stay
/// distinct, and shapes the rule cannot resolve are refused rather than mangled.
/// </remarks>
public sealed class FX_TickerDashForm
{
    /// <summary>The registered assertion.</summary>
    [Fact]
    public void The_dot_form_and_the_dash_form_normalise_to_one_key()
    {
        Assert.Equal(Ticker.Normalise("BRK-B"), Ticker.Normalise("BRK.B"));
        Assert.Equal("BRK-B", Ticker.Normalise("BRK.B").Value);
    }

    /// <summary>
    /// The case the naive rule gets wrong. Turning dots into dashes before
    /// stripping the exchange gives <c>BRK-B-US</c>, which is a different key
    /// from every other spelling of the same company.
    /// </summary>
    [Fact]
    public void The_exchange_suffix_is_stripped_before_dots_become_dashes()
    {
        Assert.Equal("BRK-B", Ticker.Normalise("BRK-B.US").Value);
        Assert.Equal("BRK-B", Ticker.Normalise("BRK.B.US").Value);
        Assert.Equal("AAPL", Ticker.Normalise("AAPL.US").Value);
    }

    [Fact]
    public void Case_and_surrounding_whitespace_do_not_make_a_second_key()
    {
        var expected = Ticker.Normalise("BRK-B");

        Assert.Equal(expected, Ticker.Normalise("brk.b"));
        Assert.Equal(expected, Ticker.Normalise("  BRK-B.us  "));
        Assert.Equal(expected, Ticker.Normalise("Brk.B"));
    }

    /// <summary>
    /// Normalising a normalised ticker changes nothing, so a value that has been
    /// through the boundary twice is the same key as one that has been through
    /// once.
    /// </summary>
    [Fact]
    public void Normalisation_is_idempotent()
    {
        foreach (var raw in new[] { "BRK.B", "BRK-B.US", "aapl", "BF.A" })
        {
            var once = Ticker.Normalise(raw);

            Assert.Equal(once, Ticker.Normalise(once.Value));
        }
    }

    /// <summary>
    /// The injectivity side. Without this the registered assertion holds for a
    /// function that returns the same key for everything.
    /// </summary>
    [Fact]
    public void Distinct_symbols_stay_distinct()
    {
        Assert.NotEqual(Ticker.Normalise("BRK-B"), Ticker.Normalise("BRKB"));
        Assert.NotEqual(Ticker.Normalise("BRK-A"), Ticker.Normalise("BRK-B"));
        Assert.NotEqual(Ticker.Normalise("BF.A"), Ticker.Normalise("BF.B"));
    }

    /// <summary>
    /// A stored ticker never carries an exchange suffix, which holds by
    /// construction because the type cannot be built any other way.
    /// </summary>
    [Fact]
    public void A_stored_form_never_carries_an_exchange_suffix()
    {
        foreach (var raw in new[] { "AAPL.US", "BRK-B.US", "brk.b.us", "AAPL" })
        {
            Assert.DoesNotContain(".", Ticker.Normalise(raw).Value, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The vendor form is the only place a suffix is added, and it is the exact
    /// inverse of stripping one.
    /// </summary>
    [Fact]
    public void The_vendor_form_adds_the_suffix_and_normalises_back()
    {
        var ticker = Ticker.Normalise("BRK.B");

        Assert.Equal("BRK-B.US", ticker.ToVendor());
        Assert.Equal(ticker, Ticker.Normalise(ticker.ToVendor()));
    }

    /// <summary>
    /// An unknown code-shaped suffix is refused rather than dashed.
    /// </summary>
    /// <remarks>
    /// This is the choice the whole rule turns on. <c>GSPC.INDX</c> silently
    /// becoming <c>GSPC-INDX</c> mints a ticker that matches nothing and never
    /// fails, which is strictly worse than refusing an instrument the lab does
    /// not trade.
    /// </remarks>
    [Fact]
    public void A_suffix_that_is_neither_a_share_class_nor_a_known_exchange_is_refused()
    {
        foreach (var raw in new[] { "GSPC.INDX", "EURUSD.FOREX", "AAPL.LSE", "ABC.WS" })
        {
            var thrown = Assert.Throws<FormatException>(() => Ticker.Normalise(raw));

            Assert.Contains("refused rather than converted", thrown.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Other vendors' conventions are refused by the character rule rather than
    /// mapped onto a guess.
    /// </summary>
    [Fact]
    public void A_symbol_carrying_characters_a_ticker_cannot_is_refused()
    {
        foreach (var raw in new[] { "^GSPC", "BRK/B", "BRK B", "BRK_B", "" })
        {
            Assert.Throws<FormatException>(() => Ticker.Normalise(raw));
        }
    }

    /// <summary>
    /// A homoglyph must not survive upper-casing into a valid-looking ticker, so
    /// the character check runs before the case fold.
    /// </summary>
    [Fact]
    public void A_non_ascii_letter_is_refused_rather_than_folded_into_ascii()
    {
        // Turkish dotless i upper-cases to ASCII 'I' under the invariant culture.
        Assert.Throws<FormatException>(() => Ticker.Normalise("BRıK"));

        // Zero-width space is not whitespace to Trim and renders as nothing, so
        // this would be a second key that looks identical to AAPL. Written as an
        // escape because the literal character is invisible in a source file,
        // which is the whole problem.
        Assert.Throws<FormatException>(() => Ticker.Normalise("AAPL\u200B"));
    }

    [Fact]
    public void A_malformed_dash_or_dot_arrangement_is_refused()
    {
        foreach (var raw in new[] { "-AAPL", "AAPL-", "AAPL--B", "AAPL.", ".AAPL" })
        {
            Assert.Throws<FormatException>(() => Ticker.Normalise(raw));
        }
    }

    [Fact]
    public void The_try_form_reports_the_reason_rather_than_throwing()
    {
        Assert.False(Ticker.TryNormalise("GSPC.INDX", out var refused, out var reason));
        Assert.Null(refused);
        Assert.Contains("INDX", reason, StringComparison.Ordinal);

        Assert.True(Ticker.TryNormalise("brk.b", out var accepted, out var noReason));
        Assert.Equal("BRK-B", accepted.Value);
        Assert.Null(noReason);
    }
}
