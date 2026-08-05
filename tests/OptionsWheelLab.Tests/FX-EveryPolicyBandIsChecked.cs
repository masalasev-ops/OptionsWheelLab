using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-EveryPolicyBandIsChecked: every <c>Policy:*:DeltaMax</c> row in
/// <c>CONFIG_REFERENCE.md</c> appears in
/// <see cref="ConfigKeys.PolicyBandCeilings"/>.
/// </summary>
/// <remarks>
/// <b>This enforces the opposite direction from the other two declared
/// vocabularies, and that is the rule rather than a coincidence.</b>
/// <see cref="Storage.DecimalColumns"/> and
/// <see cref="Storage.AppendOnlyTables"/> are checked list to document, because
/// there the error is a name in the list with no table behind it. Here it is
/// document to list, because the error is a band with no entry. Each vocabulary
/// is checked standing in the direction in which absence causes the bad outcome;
/// the other direction is a definition of done on the checkpoint that adds the
/// thing.
/// <para>
/// <b>The consequence differs in kind from an incomplete catch-list.</b> An
/// incomplete catch-list still catches what is on it. An incomplete band list
/// makes a violating configuration pass: D-W23's ceiling is compared against
/// fewer bands than exist, and the write is accepted. <c>ConfigKeys</c>'s own
/// remarks name it, which is what made the missing check visible.
/// </para>
/// <para>
/// <b>It is scheduled to happen.</b> The learner acts from policy rows, so its
/// band arrives at Phase 4, and without this nothing would fail if it were never
/// added to the list.
/// </para>
/// </remarks>
public sealed class FX_EveryPolicyBandIsChecked
{
    private const string BandCeilingSuffix = ":DeltaMax";
    private const string PolicyPrefix = "Policy:";

    [Fact]
    public void Every_policy_band_ceiling_in_the_reference_is_checked_against()
    {
        var documented = DocumentedBandCeilings();
        var checkedAgainst = ConfigKeys.PolicyBandCeilings
            .Select(band => band.Key)
            .ToHashSet(StringComparer.Ordinal);

        // A parse that matched nothing would pass the assertion below without
        // testing anything, and so would an empty vocabulary.
        Assert.NotEmpty(documented);
        Assert.NotEmpty(checkedAgainst);

        var unchecked_ = documented
            .Where(key => !checkedAgainst.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unchecked_.Count == 0,
            $"These policy band ceilings are documented in {RepoRoot.ConfigReferencePath} but are "
            + $"absent from ConfigKeys.PolicyBandCeilings: {string.Join(", ", unchecked_)}. The "
            + "delta ceiling is compared only against the bands that list names, so a band with "
            + "no entry is a band the ceiling is not checked against, and a configuration "
            + "violating D-W23 would be accepted rather than refused.");
    }

    /// <summary>
    /// Every entry in the list names a band the document carries, which is the
    /// other direction and is asserted here because it is free.
    /// </summary>
    /// <remarks>
    /// Not the standing reason this fixture exists. An entry naming a key the
    /// document does not carry fails loudly the moment a write touches the
    /// ceiling, because the invariant requires every listed band to have a value
    /// [D-W34]. It is cheap to check and it names the defect better than that
    /// refusal would.
    /// </remarks>
    [Fact]
    public void Every_band_in_the_vocabulary_is_documented()
    {
        var documented = DocumentedBandCeilings();

        Assert.All(
            ConfigKeys.PolicyBandCeilings,
            band => Assert.Contains(band.Key, documented));
    }

    /// <summary>
    /// Each band carries a name, since a refusal states which band it failed
    /// against rather than only that one of them failed [D-W22].
    /// </summary>
    [Fact]
    public void Every_band_carries_a_name()
    {
        Assert.All(
            ConfigKeys.PolicyBandCeilings,
            band => Assert.False(string.IsNullOrWhiteSpace(band.Name)));
    }

    /// <summary>
    /// Permanent cover for the comparison, so the check does not rest on a live
    /// tree that happens to be complete today.
    /// </summary>
    /// <remarks>
    /// <b>The absent band must name a maker that will never exist.</b> This case
    /// used <c>Policy:Learner:</c> until 4.3, when that maker arrived and joined
    /// the vocabulary, and the synthetic gap closed itself: the case still passed
    /// its own construction and asserted nothing. A negative example naming a
    /// thing the roadmap intends to build is a negative example with an expiry
    /// date on it.
    /// <para>
    /// <c>Policy:Chartist:</c> is not a maker in this design. [D-W4] fixes the
    /// arms at three, a frozen baseline, a random control and the learner, so a
    /// fourth is a decision rather than an addition and would arrive with its own
    /// number.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_documented_band_absent_from_the_vocabulary_is_reported()
    {
        const string Markdown =
            """
            | Key | Store | Meaning | Consumer | Notes |
            |---|---|---|---|---|
            | `Policy:Baseline:DeltaMax` | rows | in the vocabulary | Baseline maker | |
            | `Policy:Chartist:DeltaMax` | rows | in no vocabulary | Chartist | |
            | `Policy:Baseline:DeltaMin` | rows | a floor, not a ceiling | Baseline maker | |
            | `Gate:MaxDelta` | rows | the ceiling itself, not a band | Risk gate | |
            """;

        var documented = BandCeilingsIn(Markdown);
        var checkedAgainst = ConfigKeys.PolicyBandCeilings
            .Select(band => band.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            ["Policy:Chartist:DeltaMax"],
            documented.Where(key => !checkedAgainst.Contains(key)).ToList());
    }

    /// <summary>
    /// A band floor is not a ceiling, and the gate's own ceiling is not a band.
    /// Either one swept into the set would make the check fire on a correct tree,
    /// and the usual answer to a fixture that fires wrongly is to weaken it.
    /// </summary>
    [Fact]
    public void Neither_a_band_floor_nor_the_gate_ceiling_is_taken_for_a_band_ceiling()
    {
        const string Markdown =
            """
            | Key | Store | Meaning | Consumer | Notes |
            |---|---|---|---|---|
            | `Policy:Baseline:DeltaMin` | rows | a floor | Baseline maker | |
            | `Gate:MaxDelta` | rows | the ceiling | Risk gate | |
            | `Trial:MaxTrialDays` | rows | not a delta at all | State machine | |
            """;

        Assert.Empty(BandCeilingsIn(Markdown));
    }

    private static IReadOnlySet<string> DocumentedBandCeilings() =>
        BandCeilingsIn(File.ReadAllText(RepoRoot.ConfigReferencePath));

    private static IReadOnlySet<string> BandCeilingsIn(string markdown) =>
        ConfigReferenceParser.Parse(markdown).Keys
            .Select(key => key.Key)
            .Where(key =>
                key.StartsWith(PolicyPrefix, StringComparison.Ordinal)
                && key.EndsWith(BandCeilingSuffix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
}
