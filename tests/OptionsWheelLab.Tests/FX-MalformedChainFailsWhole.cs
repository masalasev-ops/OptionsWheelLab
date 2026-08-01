using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-MalformedChainFailsWhole: a chain with one malformed contract yields
/// nothing rather than the valid ones before it.
/// </summary>
/// <remarks>
/// Parse then yield, never stream and apply. A partially loaded chain is worse
/// than none: it looks like a chain, and whatever it is missing is missing
/// silently.
/// <para>
/// Every case here is an inline string rather than a file on disk. A malformed
/// file sitting in <c>synthetic/</c> would read as data rather than as a test
/// case, and the loader must be known to be capable of failing rather than
/// observed to pass against a tree that happens to be correct.
/// </para>
/// </remarks>
public sealed class FX_MalformedChainFailsWhole
{
    /// <summary>
    /// The definition of done, stated directly: the valid contracts before the
    /// bad one do not come back.
    /// </summary>
    [Fact]
    public void One_bad_contract_at_the_end_yields_nothing()
    {
        var good = SyntheticChainReader.Read(Chain(
            """{ "strike": "45.00", "bid": "0.30", "ask": "0.36" }""",
            """{ "strike": "47.50", "bid": "0.55", "ask": "0.62" }"""));

        Assert.Equal(2, good.Quotes.Count);

        var problems = Refused(Chain(
            """{ "strike": "45.00", "bid": "0.30", "ask": "0.36" }""",
            """{ "strike": "47.50", "bid": "0.55", "ask": "0.62" }""",
            """{ "strike": "50.00", "bid": "not a number", "ask": "1.05" }"""));

        Assert.NotEmpty(problems);
    }

    /// <summary>
    /// A hand-written file carries several typos as often as one, so every
    /// reason is reported in one pass rather than one run at a time.
    /// </summary>
    [Fact]
    public void Every_reason_is_reported_at_once()
    {
        var problems = Refused(Chain(
            """{ "strike": "45.00", "bid": "nonsense", "ask": "0.36" }""",
            """{ "strike": "47.50", "bid": "0.55", "ask": "also nonsense" }""",
            """{ "strike": "wrong", "bid": "0.95", "ask": "1.05" }"""));

        Assert.Equal(3, problems.Count);
    }

    /// <summary>
    /// A hand-written value is exact, so a value the scale cannot hold is a
    /// malformed chain rather than one to round [D-W29, D-W31].
    /// </summary>
    [Fact]
    public void A_decimal_beyond_the_scale_is_malformed_rather_than_rounded()
    {
        var problems = Refused(Chain(
            """{ "strike": "45.00", "bid": "0.123456789", "ask": "0.36" }"""));

        Assert.Contains(problems, problem => problem.Contains("bid", StringComparison.Ordinal));
    }

    /// <summary>
    /// The worst failure a hand-written file has, if it is tolerated.
    /// </summary>
    /// <remarks>
    /// A misspelling ignored silently leaves the value absent, the chain loading,
    /// and nothing to show for it.
    /// </remarks>
    [Fact]
    public void A_misspelled_property_is_refused_rather_than_ignored()
    {
        var problems = Refused(Chain(
            """{ "strike": "45.00", "bid": "0.30", "ask": "0.36", "dleta": "-0.10" }"""));

        Assert.Contains(problems, problem => problem.Contains("dleta", StringComparison.Ordinal));
    }

    /// <summary>
    /// An unquoted number is refused, which is what keeps a JSON number from ever
    /// becoming a floating-point value.
    /// </summary>
    /// <remarks>
    /// The source guard states that it cannot see this, so the format closes it
    /// by construction instead: every value in a chain is quoted, so an unquoted
    /// one is a malformed chain.
    /// </remarks>
    [Fact]
    public void An_unquoted_number_is_refused()
    {
        var problems = Refused(Chain(
            """{ "strike": "45.00", "bid": 0.30, "ask": "0.36" }"""));

        Assert.Contains(problems, problem => problem.Contains("bid", StringComparison.Ordinal));
    }

    /// <summary>
    /// A crossed quote loads, because the rule moved to the gate at 2.3.
    /// </summary>
    /// <remarks>
    /// <b>This case asserted the opposite until 2.3 and is inverted rather than
    /// deleted</b>, because what the loader enforces is smaller than it was and
    /// a silent removal would leave nothing saying so. The refusal was right
    /// about the risk and wrong about the venue: Phase 8's vendor ingest reaches
    /// the store without passing this reader, so the gate rejects a crossed
    /// quote with its own reason [D-W22, as amended] and the loader carries it
    /// through. That is what lets a fixture express one at all.
    /// <para>
    /// The negative-price refusals below are unchanged, so this suite still
    /// holds that the reader enforces something about what a market can be.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_bid_above_its_ask_now_loads_because_the_gate_owns_that_rule()
    {
        var chain = SyntheticChainReader.Read(Chain(
            """{ "strike": "45.00", "bid": "0.40", "ask": "0.36" }"""));

        var quote = Assert.Single(chain.Quotes);

        Assert.Equal(0.40m, quote.Bid);
        Assert.Equal(0.36m, quote.Ask);
    }

    /// <summary>
    /// A locked market was never refused here and still is not.
    /// </summary>
    [Fact]
    public void A_locked_market_loads()
    {
        var chain = SyntheticChainReader.Read(Chain(
            """{ "strike": "45.00", "bid": "0.36", "ask": "0.36" }"""));

        var quote = Assert.Single(chain.Quotes);

        Assert.Equal(quote.Ask, quote.Bid);
    }

    [Fact]
    public void A_negative_bid_is_refused()
    {
        var problems = Refused(Chain(
            """{ "strike": "45.00", "bid": "-0.30", "ask": "0.36" }"""));

        Assert.Contains(problems, problem => problem.Contains("negative", StringComparison.Ordinal));
    }

    /// <summary>
    /// Equal bid and ask is a locked market, which is thin but real, so it loads.
    /// </summary>
    /// <remarks>
    /// The boundary of the rule above, asserted so the refusal is known not to
    /// have swallowed the case next to it.
    /// </remarks>
    [Fact]
    public void A_locked_market_is_not_refused()
    {
        var chain = SyntheticChainReader.Read(Chain(
            """{ "strike": "45.00", "bid": "0.36", "ask": "0.36" }"""));

        Assert.Single(chain.Quotes);
    }

    /// <summary>
    /// A contract is keyed on identity and a snapshot date, so two quotes for one
    /// leave which one it was undecided.
    /// </summary>
    [Fact]
    public void A_duplicate_contract_on_one_date_is_refused()
    {
        var problems = Refused(Chain(
            """{ "strike": "45.00", "bid": "0.30", "ask": "0.36" }""",
            """{ "strike": "45.00", "bid": "0.31", "ask": "0.37" }"""));

        Assert.Contains(problems, problem => problem.Contains("twice", StringComparison.Ordinal)
            || problem.Contains("2 times", StringComparison.Ordinal));
    }

    [Fact]
    public void A_duplicate_bar_on_one_date_is_refused()
    {
        const string Json = """
            {
              "symbol": "WDGT",
              "bars": [
                { "date": "2026-03-02", "close": "52.40" },
                { "date": "2026-03-02", "close": "52.41" }
              ],
              "chains": []
            }
            """;

        Assert.Contains(Refused(Json), problem => problem.Contains("bars", StringComparison.Ordinal));
    }

    /// <summary>
    /// A contract that had already expired cannot appear in the snapshot.
    /// </summary>
    [Fact]
    public void An_expiry_before_its_snapshot_date_is_refused()
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
                      "expiry": "2026-02-20",
                      "right": "put",
                      "quotes": [ { "strike": "45.00", "bid": "0.30", "ask": "0.36" } ]
                    }
                  ]
                }
              ]
            }
            """;

        Assert.Contains(
            Refused(Json),
            problem => problem.Contains("already expired", StringComparison.Ordinal));
    }

    /// <summary>
    /// The expiry day itself is admitted, since a contract quotes on the morning
    /// it expires.
    /// </summary>
    [Fact]
    public void An_expiry_on_its_snapshot_date_is_not_refused()
    {
        const string Json = """
            {
              "symbol": "WDGT",
              "bars": [ { "date": "2026-04-17", "close": "48.90" } ],
              "chains": [
                {
                  "date": "2026-04-17",
                  "contracts": [
                    {
                      "expiry": "2026-04-17",
                      "right": "put",
                      "quotes": [ { "strike": "45.00", "bid": "0.30", "ask": "0.36" } ]
                    }
                  ]
                }
              ]
            }
            """;

        Assert.Single(SyntheticChainReader.Read(Json).Quotes);
    }

    [Theory]
    [InlineData("\"right\": \"Put\"", "right")]
    [InlineData("\"expiry\": \"17/04/2026\"", "expiry")]
    public void A_value_outside_its_stored_form_is_refused(string replacement, string expected)
    {
        var json = Chain("""{ "strike": "45.00", "bid": "0.30", "ask": "0.36" }""")
            .Replace("\"right\": \"put\"", replacement, StringComparison.Ordinal)
            .Replace("\"expiry\": \"2026-04-17\"", replacement, StringComparison.Ordinal);

        Assert.Contains(
            Refused(json),
            problem => problem.Contains(expected, StringComparison.Ordinal));
    }

    /// <summary>
    /// A ticker carrying an exchange suffix the lab does not read is refused by
    /// the type that owns that rule, rather than by a second copy of it here.
    /// </summary>
    [Fact]
    public void A_symbol_the_ticker_rules_refuse_is_refused()
    {
        var json = Chain("""{ "strike": "45.00", "bid": "0.30", "ask": "0.36" }""")
            .Replace("\"symbol\": \"WDGT\"", "\"symbol\": \"GSPC.INDX\"", StringComparison.Ordinal);

        Assert.Contains(
            Refused(json),
            problem => problem.Contains("symbol", StringComparison.Ordinal));
    }

    [Fact]
    public void Text_that_is_not_json_is_refused_as_one_reason()
    {
        var problem = Assert.Single(Refused("this is not a chain"));

        Assert.Contains("not readable as JSON", problem, StringComparison.Ordinal);
    }

    /// <summary>
    /// Comments and a trailing comma survive, because a hand-written file carries
    /// commentary and gets its lists reordered.
    /// </summary>
    [Fact]
    public void Comments_and_a_trailing_comma_are_admitted()
    {
        const string Json = """
            {
              // The underlying, and a note about why this case exists.
              "symbol": "WDGT",
              "bars": [ { "date": "2026-03-02", "close": "52.40" }, ],
              "chains": [],
            }
            """;

        Assert.Single(SyntheticChainReader.Read(Json).Bars);
    }

    private static IReadOnlyList<string> Refused(string json) =>
        Assert.Throws<MalformedChainException>(() => SyntheticChainReader.Read(json)).Problems;

    private static string Chain(params string[] quotes) =>
        $$"""
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
