using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ChainLoadsInIdentityOrder: quotes are yielded in contract identity order,
/// and loading twice gives one sequence.
/// </summary>
/// <remarks>
/// This is where the total order 0.4 gave <c>ContractIdentity</c> gets its first
/// caller. It exists because three makers receive byte-identical candidate sets
/// [D-W4], which cannot depend on the order candidates arrive in, and because a
/// hand-written file gets reordered by whoever edits it [D-W31]. An order that
/// came from the file would change when someone tidied it.
/// </remarks>
public sealed class FX_ChainLoadsInIdentityOrder
{
    /// <summary>
    /// The same chain written in three orders loads to one sequence.
    /// </summary>
    /// <remarks>
    /// Written out of order deliberately. A file already in identity order would
    /// pass whether or not the loader sorted anything, which is the shape of test
    /// that observes a property rather than asserting it.
    /// </remarks>
    [Fact]
    public void The_file_order_does_not_reach_the_output()
    {
        var ascending = SyntheticChainReader.Read(Chain("45.00", "47.50", "50.00"));
        var descending = SyntheticChainReader.Read(Chain("50.00", "47.50", "45.00"));
        var shuffled = SyntheticChainReader.Read(Chain("47.50", "45.00", "50.00"));

        Assert.NotEmpty(ascending.Quotes);

        Assert.Equal(Strikes(ascending), Strikes(descending));
        Assert.Equal(Strikes(ascending), Strikes(shuffled));
        Assert.Equal([45.00m, 47.50m, 50.00m], Strikes(ascending));
    }

    /// <summary>
    /// Two reads of one text give one sequence.
    /// </summary>
    [Fact]
    public void Loading_twice_gives_the_same_sequence()
    {
        var text = Chain("50.00", "45.00", "47.50");

        var first = SyntheticChainReader.Read(text);
        var second = SyntheticChainReader.Read(text);

        Assert.NotEmpty(first.Quotes);
        Assert.Equal(first.Quotes, second.Quotes);
        Assert.Equal(first.Bars, second.Bars);
    }

    /// <summary>
    /// Identity order is underlying, then expiry, then right, then strike, so a
    /// nearer expiry sorts first and a put sorts before a call at one expiry.
    /// </summary>
    /// <remarks>
    /// Asserted here rather than assumed from the strike case above, which would
    /// pass on a loader that only sorted by strike.
    /// </remarks>
    [Fact]
    public void Expiry_orders_before_right_and_right_before_strike()
    {
        const string Json = """
            {
              "symbol": "WDGT",
              "bars": [ { "date": "2026-03-02", "close": "52.40" } ],
              "chains": [
                {
                  "date": "2026-03-02",
                  "contracts": [
                    {
                      "expiry": "2026-05-15",
                      "right": "put",
                      "quotes": [ { "strike": "10.00", "bid": "1.00", "ask": "1.10" } ]
                    },
                    {
                      "expiry": "2026-04-17",
                      "right": "call",
                      "quotes": [ { "strike": "99.00", "bid": "1.00", "ask": "1.10" } ]
                    },
                    {
                      "expiry": "2026-04-17",
                      "right": "put",
                      "quotes": [ { "strike": "99.00", "bid": "1.00", "ask": "1.10" } ]
                    }
                  ]
                }
              ]
            }
            """;

        var quotes = SyntheticChainReader.Read(Json).Quotes;

        Assert.Equal(3, quotes.Count);
        Assert.Equal(
            ["2026-04-17 put 99.00000000", "2026-04-17 call 99.00000000", "2026-05-15 put 10.00000000"],
            quotes.Select(quote => quote.Contract.ToString()["WDGT ".Length..]));
    }

    /// <summary>
    /// Bars come back in session-date order for the same reason.
    /// </summary>
    [Fact]
    public void Bars_are_in_session_date_order()
    {
        const string Json = """
            {
              "symbol": "WDGT",
              "bars": [
                { "date": "2026-04-17", "close": "48.90" },
                { "date": "2026-03-02", "close": "52.40" },
                { "date": "2026-04-08", "close": "45.80" }
              ],
              "chains": []
            }
            """;

        var bars = SyntheticChainReader.Read(Json).Bars;

        Assert.NotEmpty(bars);
        Assert.Equal(
            [new DateOnly(2026, 3, 2), new DateOnly(2026, 4, 8), new DateOnly(2026, 4, 17)],
            bars.Select(bar => bar.SessionDate));
    }

    private static IReadOnlyList<decimal> Strikes(SyntheticChain chain) =>
        [.. chain.Quotes.Select(quote => quote.Contract.Strike)];

    private static string Chain(params string[] strikes)
    {
        var quotes = strikes.Select(strike =>
            $$"""{ "strike": "{{strike}}", "bid": "1.00", "ask": "1.10" }""");

        return $$"""
            {
              "symbol": "WDGT",
              "bars": [ { "date": "2026-03-02", "close": "52.40" } ],
              "chains": [
                {
                  "date": "2026-03-02",
                  "contracts": [
                    {
                      "expiry": "2026-04-17",
                      "right": "put",
                      "quotes": [ {{string.Join(", ", quotes)}} ]
                    }
                  ]
                }
              ]
            }
            """;
    }
}
