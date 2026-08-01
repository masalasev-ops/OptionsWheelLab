using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The stored forms of a date, a contract right, a position state and a gate
/// reason.
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

    [Fact]
    public void A_position_state_stores_the_schemas_tag_and_round_trips()
    {
        Assert.Equal("cash", StorePositionState.ToStored(PositionState.Cash));
        Assert.Equal("short_put", StorePositionState.ToStored(PositionState.ShortPut));
        Assert.Equal("holding_shares", StorePositionState.ToStored(PositionState.HoldingShares));
        Assert.Equal("short_call", StorePositionState.ToStored(PositionState.ShortCall));

        Assert.All(
            Enum.GetValues<PositionState>(),
            state => Assert.Equal(
                state, StorePositionState.ParseStored(StorePositionState.ToStored(state))));
    }

    /// <summary>
    /// The enum's own spelling is not the stored form, and two of the four are
    /// unreachable from it by any casing rule.
    /// </summary>
    /// <remarks>
    /// This is the case that makes "declared, not derived" concrete rather than
    /// conventional. A lower-casing derivation would have produced <c>put</c>
    /// and <c>joined</c> correctly and would produce <c>holdingshares</c> here.
    /// </remarks>
    [Fact]
    public void The_stored_state_is_not_the_enum_spelling()
    {
        Assert.NotEqual(
            nameof(PositionState.HoldingShares),
            StorePositionState.ToStored(PositionState.HoldingShares));

        Assert.NotEqual(
            nameof(PositionState.HoldingShares).ToLowerInvariant(),
            StorePositionState.ToStored(PositionState.HoldingShares));

        Assert.Throws<FormatException>(() => StorePositionState.ParseStored("HoldingShares"));
        Assert.Throws<FormatException>(() => StorePositionState.ParseStored("holdingshares"));
    }

    /// <summary>
    /// An unrecognised value is refused rather than defaulting, which is why
    /// <see cref="PositionState"/> starts at one: a default reading as
    /// <see cref="PositionState.Cash"/> would enumerate puts against an account
    /// holding shares.
    /// </summary>
    [Fact]
    public void An_unrecognised_state_is_refused_rather_than_defaulted()
    {
        Assert.Throws<FormatException>(() => StorePositionState.ParseStored("assigned"));
        Assert.Throws<FormatException>(() => StorePositionState.ParseStored(""));

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => StorePositionState.ToStored(default));

        Assert.Contains("not a position state", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The four tags are exactly what §4.3 names, read from the document rather
    /// than restated here.
    /// </summary>
    /// <remarks>
    /// The schema document is prose about <c>state</c> rather than a table, so
    /// this reads the one sentence that lists the tags. It is the narrowest
    /// thing that would notice §4.3 and this type disagreeing, and 1.1's
    /// divergence is why noticing matters: nothing parses §4.1 as a schema, and
    /// the document and the migration drifted in six places.
    /// </remarks>
    [Fact]
    public void The_declared_tags_are_the_ones_the_schema_document_names()
    {
        var schema = File.ReadAllText(RepoRoot.SchemaDocumentPath);

        var declared = Enum
            .GetValues<PositionState>()
            .Select(StorePositionState.ToStored)
            .ToList();

        // The paragraph in §4.3 that names the union's tags, not the line: the
        // sentence wraps, and reading one line would silently drop the tag that
        // fell past the wrap.
        var marker = schema.IndexOf(
            "is the discriminated union tag", StringComparison.Ordinal);

        Assert.NotEqual(-1, marker);

        var paragraph = Paragraph(schema, marker);

        var named = declared.Where(tag => paragraph.Contains($"`{tag}`", StringComparison.Ordinal));

        Assert.Equal(declared, named);
    }

    [Fact]
    public void A_gate_reason_stores_lower_case_and_round_trips()
    {
        Assert.Equal("spread_cap", StoreGateReason.ToStored(GateReason.SpreadCap));
        Assert.Equal("premium_floor", StoreGateReason.ToStored(GateReason.PremiumFloor));
        Assert.Equal("crossed_market", StoreGateReason.ToStored(GateReason.CrossedMarket));
        Assert.Equal("delta_ceiling", StoreGateReason.ToStored(GateReason.DeltaCeiling));
        Assert.Equal("expiry_window", StoreGateReason.ToStored(GateReason.ExpiryWindow));
        Assert.Equal(
            "earnings_clearance", StoreGateReason.ToStored(GateReason.EarningsClearance));

        Assert.All(
            Enum.GetValues<GateReason>(),
            reason => Assert.Equal(
                reason, StoreGateReason.ParseStored(StoreGateReason.ToStored(reason))));
    }

    [Fact]
    public void The_stored_reason_is_not_the_enum_spelling()
    {
        Assert.NotEqual(
            nameof(GateReason.SpreadCap), StoreGateReason.ToStored(GateReason.SpreadCap));

        Assert.NotEqual(
            nameof(GateReason.SpreadCap).ToLowerInvariant(),
            StoreGateReason.ToStored(GateReason.SpreadCap));

        Assert.Throws<FormatException>(() => StoreGateReason.ParseStored("SpreadCap"));
        Assert.Throws<FormatException>(() => StoreGateReason.ParseStored("spreadcap"));
    }

    [Fact]
    public void An_unrecognised_reason_is_refused_rather_than_defaulted()
    {
        Assert.Throws<FormatException>(() => StoreGateReason.ParseStored("per_name_cap"));
        Assert.Throws<FormatException>(() => StoreGateReason.ParseStored(""));

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => StoreGateReason.ToStored(default));

        Assert.Contains("not a gate reason", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every declared reason names a decision that states its ground.
    /// </summary>
    /// <remarks>
    /// The rule 2.3 established when the gate was about to reject a crossed
    /// quote on a ground D-W22 did not state. Read off the enum's own summaries,
    /// so a reason added without a bracketed decision fails here rather than
    /// reaching the audit trail unaccounted for.
    /// </remarks>
    [Fact]
    public void Every_gate_reason_cites_a_decision_in_its_summary()
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot.SourcePath, "OptionsWheelLab.Core", "Generation", "GateReason.cs"));

        var undocumented = Enum
            .GetValues<GateReason>()
            .Where(reason => !CitesADecision(source, reason.ToString()))
            .ToList();

        Assert.NotEmpty(Enum.GetValues<GateReason>());

        Assert.True(
            undocumented.Count == 0,
            "These gate reasons have no summary naming the decision that states their ground: "
            + string.Join(", ", undocumented)
            + ". A reason with no decision behind it is a rule nobody agreed to.");
    }

    /// <summary>
    /// The member's own summary line carries a <c>[D-Wnn]</c> bracket.
    /// </summary>
    private static bool CitesADecision(string source, string member)
    {
        var declaration = source.IndexOf($"{member} =", StringComparison.Ordinal);

        if (declaration == -1)
        {
            return false;
        }

        // The summary sits above the declaration, so look back to the previous
        // blank line rather than forward.
        var preceding = source[..declaration];
        var summaryStart = preceding.LastIndexOf("/// <summary>", StringComparison.Ordinal);

        return summaryStart != -1
            && System.Text.RegularExpressions.Regex.IsMatch(
                preceding[summaryStart..], @"\[D-W\d+\]");
    }

    /// <summary>
    /// From <paramref name="from"/> to the end of the paragraph containing it.
    /// </summary>
    private static string Paragraph(string document, int from)
    {
        var end = document.IndexOf("\n\n", from, StringComparison.Ordinal);

        return end == -1 ? document[from..] : document[from..end];
    }
}
