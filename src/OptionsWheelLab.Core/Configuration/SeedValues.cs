namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// The initial version of every configuration key Phase 0.8 sets, with the note
/// explaining each choice.
/// </summary>
/// <remarks>
/// <b>Provenance is judged per key, not per section.</b> Three kinds appear
/// here and the notes say which each is: transcribed from a corpus statement,
/// taken from a proposed value in a decision, or judged. The third is the one
/// worth arguing, and there are four of them.
/// <para>
/// All twenty-four `rows`-classed keys are here. Nineteen landed at 0.8; the
/// four <c>Risk:</c> keys landed at 2.4, which is the checkpoint that first
/// reads them; and <c>Costs:AssignmentFee</c> landed at 3.4, which is the
/// checkpoint that first computes with it. It stayed a carried obligation
/// rather than a gap for four phases, because leaving a key unseeded and
/// leaving it unscheduled are different things.
/// </para>
/// <para>
/// <b>That key brought a kind of provenance this file did not have</b>
/// [D-W50]: transcribed from a named external source with a retrieval date,
/// where every other entry is transcribed from this corpus, taken from a
/// decision's proposed value, or judged. It is the shape Phase 3's decisions
/// carry, and it states what the source does not reach, since one broker's
/// schedule establishes a common case and not a market rule.
/// </para>
/// <para>
/// <b>The <c>Risk:</c> block is a fourth kind of provenance and it is stated per
/// key.</b> Three of the four are transcribed from <c>WORKED_EXAMPLE.md</c>
/// section 1, which states the equity and both cap percentages and derives the
/// headrooms section 3 uses. The fourth is chosen, no document stating it. 0.8
/// left all of them on the ground that an equity-relative cap is the operator's
/// risk appetite [D-W11] and that an example illustrating one account is not the
/// operator setting one; that argument is about who decides, and it does not
/// stop the decided values coinciding with the example's.
/// </para>
/// <para>
/// Values are literals here rather than bound from <c>appsettings</c>. A section
/// classed <c>rows</c> is never given an appsettings-bound options type, because
/// that would create the second current-value path as-of resolution exists to
/// prevent [D-W26, D-W27].
/// </para>
/// </remarks>
public static class SeedValues
{
    /// <summary>Every entry, in the order a reader would want them.</summary>
    /// <remarks>
    /// Written together in one transaction, which is not a convenience: an
    /// invariant over two keys cannot be evaluated while only one exists.
    /// </remarks>
    public static IReadOnlyList<ConfigEntry> All { get; } =
    [
        // Gate constraints. Every value is the one proposed in CONFIG_REFERENCE
        // and in the decision that introduced it.
        new(ConfigKeys.GateMaxSpreadFractionOfMid, "0.12",
            "Proposed default [D-W22]. A fraction of mid, so 0.12 is twelve percent."),
        new(ConfigKeys.GateMinPremium, "0.30",
            "Proposed default [D-W22]. Below this the per-contract commission eats a large "
            + "fraction of the credit."),
        new(ConfigKeys.GateMaxDelta, "0.35",
            "Proposed default [D-W23], and it settles that decision's open clause. Held equal "
            + "to Policy:Random:DeltaMax rather than above it, so the random control spans the "
            + "delta range the gate admits. A control drawing from a smaller opportunity set "
            + "than the gate allows would make a difference between makers partly permission "
            + "rather than judgement [D-W4, D-W10]."),
        new(ConfigKeys.GateMinDte, "7",
            "Proposed default [D-W24]. The fill and assignment models are least defensible on "
            + "contracts about to expire."),
        new(ConfigKeys.GateMaxDte, "70",
            "Proposed default [D-W24]. Must stay strictly below Trial:MaxTrialDays, which the "
            + "write path now enforces."),
        new(ConfigKeys.GateEarningsClearanceDays, "7",
            "Proposed default [D-W25]. Buffered on both sides because a vendor report date "
            + "moves."),

        // Risk caps. Structural and outside what the learner may propose
        // [D-W11]. Three are transcribed from WORKED_EXAMPLE section 1 and the
        // fourth is chosen; each note says which.
        new(ConfigKeys.RiskEquity, "100000.00",
            "Transcribed from WORKED_EXAMPLE section 1. A configuration key rather than a "
            + "figure derived from cash and open positions: a derived denominator moves with "
            + "the run and would loosen every cap during a drawdown, which is when they "
            + "matter [D-W11]."),
        new(ConfigKeys.RiskPerNameCapFraction, "0.25",
            "Transcribed from WORKED_EXAMPLE section 1, which states 25 percent and derives "
            + "the 5,100.00 headroom section 3's verdicts rest on. Against equity of 100,000 "
            + "that is 25,000 committed in one name."),
        new(ConfigKeys.RiskTotalCapFraction, "0.60",
            "Transcribed from WORKED_EXAMPLE section 1, which states 60 percent. Against "
            + "equity of 100,000 that is 60,000 committed in total, so two names at the full "
            + "per-name cap commit 50,000 and a third cannot reach its own cap."),
        new(ConfigKeys.RiskSimultaneousAssignmentLimitFraction, "0.60",
            "Chosen, not transcribed: no document states it. Held equal to "
            + "Risk:TotalCapFraction because a cash-secured put's committed capital is its "
            + "assignment exposure, so a lower value makes the total cap unreachable and a "
            + "higher one never binds. The relationship changes at Phase 3, when a covered "
            + "call commits shares rather than cash."),

        // Trial bounds. No value is proposed anywhere, so both are judged.
        new(ConfigKeys.TrialMaxRolls, "2",
            "Judged. No value is proposed anywhere [D-W14]. Low enough to bind sometimes rather "
            + "than be decorative: with MaxDte at 70 and MaxTrialDays at 120, the day bound "
            + "already caps most rolled chains, so a high roll count would never be the "
            + "constraint that acted."),
        new(ConfigKeys.TrialMaxTrialDays, "120",
            "Judged, but doubly constrained. Must exceed Gate:MaxDte of 70 [D-W24]. And "
            + "WORKED_EXAMPLE's own trial runs 2026-03-02 to 2026-06-19, which is 109 days, so "
            + "a bound below that would force-close the example's trial before its third expiry "
            + "and make FX-TrialCompleteIncludesAssignment's total unreachable."),

        // Scoring. Free judgement: nothing in the corpus constrains either.
        new("Scoring:DivergenceThreshold", "0.70",
            "Judged, and free: no corpus statement constrains it [D-W20]. A rank-correlation "
            + "floor between the fast and slow scores, below which the monitor fires."),
        new("Scoring:DivergenceWindowDays", "90",
            "Judged, and free [D-W20]. Long enough for a rank correlation over a meaningful "
            + "number of decisions, short enough to notice a change within a quarter."),

        // Policy bands, transcribed from WORKED_EXAMPLE section 1.
        new("Policy:Baseline:DeltaMin", "0.20",
            "WORKED_EXAMPLE section 1. Never changes for the life of the experiment [D-W4]."),
        new(ConfigKeys.BaselineDeltaMax, "0.30",
            "WORKED_EXAMPLE section 1. Never changes [D-W4]."),
        new("Policy:Baseline:DteMin", "30",
            "WORKED_EXAMPLE section 1. Never changes [D-W4]."),
        new("Policy:Baseline:DteMax", "60",
            "WORKED_EXAMPLE section 1. Never changes [D-W4]. The random maker reads this "
            + "window, which is why Policy:Random carries no DTE keys."),
        new("Policy:Random:DeltaMin", "0.10",
            "WORKED_EXAMPLE section 1, inherited rather than argued. D-W4's argument reaches "
            + "the ceiling and not the floor: there is no Gate:MinDelta, so this sits strictly "
            + "inside what the gate admits. Whether Gate:MinPremium already excludes most of "
            + "what lies below it is a measurement Phase 2 can make and 0.8 cannot."),
        new(ConfigKeys.RandomDeltaMax, "0.35",
            "WORKED_EXAMPLE section 1, and equal to Gate:MaxDelta by choice rather than "
            + "coincidence. See that key's note [D-W23]."),
        new("Policy:Random:Seed", "20260729",
            "Chosen, not stated. The value is arbitrary; that it is fixed is not, since the "
            + "random control has to draw the same way on a re-run."),

        // Costs. All three from 3.4, and the third is the last key this corpus
        // owed a value to.
        new("Costs:CommissionPerContract", "0.65",
            "WORKED_EXAMPLE section 1, which section 4's fills, section 6.3's ledger and "
            + "FX-TrialCompleteIncludesAssignment's total all depend on. Corroborated at 3.4 "
            + "against Schwab's April 2026 pricing guide, which publishes the same figure; the "
            + "value was judged from the document until then. A real broker's rate replaces it "
            + "by version + 1, which is what the versioned store is for."),
        new("Costs:AssignmentFee", "0.00",
            "Transcribed [D-W50]. Schwab's Pricing Guide for Individual Investors, April 2026: "
            + "there are no commissions or per-contract fees assessed on transactions resulting "
            + "from options exercises and assignments. One broker's schedule is not a market "
            + "rule, so this is the common case rather than a universal one, and the key is "
            + "what makes a broker that charges a change to a stored value rather than to code. "
            + "A zero inferred from an absent ledger line would be invisible when wrong; this "
            + "one is stated."),
        new("Costs:FillPoint", "bid",
            "Fixed in advance and not a tunable [D-W12]. Seeded anyway because a fixed value "
            + "still has to be readable, and a rows-classed key never written cannot be "
            + "resolved as-of at all."),
    ];
}
