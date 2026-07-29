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
/// Nineteen of the twenty-three `rows`-classed keys are here. The three
/// <c>Risk:</c> fractions are the operator's risk appetite [D-W11] and
/// <c>Costs:AssignmentFee</c> has no statement anywhere; both are carried
/// obligations against the phase that first consumes them, because leaving a key
/// unseeded and leaving it unscheduled are different things.
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
        new("Gate:MaxSpreadFractionOfMid", "0.12",
            "Proposed default [D-W22]. A fraction of mid, so 0.12 is twelve percent."),
        new("Gate:MinPremium", "0.30",
            "Proposed default [D-W22]. Below this the per-contract commission eats a large "
            + "fraction of the credit."),
        new(ConfigKeys.GateMaxDelta, "0.35",
            "Proposed default [D-W23], and it settles that decision's open clause. Held equal "
            + "to Policy:Random:DeltaMax rather than above it, so the random control spans the "
            + "delta range the gate admits. A control drawing from a smaller opportunity set "
            + "than the gate allows would make a difference between makers partly permission "
            + "rather than judgement [D-W4, D-W10]."),
        new("Gate:MinDte", "7",
            "Proposed default [D-W24]. The fill and assignment models are least defensible on "
            + "contracts about to expire."),
        new(ConfigKeys.GateMaxDte, "70",
            "Proposed default [D-W24]. Must stay strictly below Trial:MaxTrialDays, which the "
            + "write path now enforces."),
        new("Gate:EarningsClearanceDays", "7",
            "Proposed default [D-W25]. Buffered on both sides because a vendor report date "
            + "moves."),

        // Trial bounds. No value is proposed anywhere, so both are judged.
        new("Trial:MaxRolls", "2",
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

        // Costs. Two of the three, judged per key.
        new("Costs:CommissionPerContract", "0.65",
            "WORKED_EXAMPLE section 1, which section 4's fills, section 6.3's ledger and "
            + "FX-TrialCompleteIncludesAssignment's total all depend on. A real broker's rate "
            + "replaces it by version + 1, which is what the versioned store is for."),
        new("Costs:FillPoint", "bid",
            "Fixed in advance and not a tunable [D-W12]. Seeded anyway because a fixed value "
            + "still has to be readable, and a rows-classed key never written cannot be "
            + "resolved as-of at all."),
    ];
}
