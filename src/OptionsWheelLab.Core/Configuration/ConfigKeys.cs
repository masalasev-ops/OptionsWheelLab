namespace OptionsWheelLab.Core.Configuration;

/// <summary>
/// The configuration keys the code names, and the key sets each cross-key
/// invariant needs.
/// </summary>
/// <remarks>
/// A declared vocabulary rather than literals at call sites, the same shape as
/// <see cref="Storage.DecimalColumns"/> and <see cref="Storage.AppendOnlyTables"/>
/// and for the same reason: the check reads its subject from one list that can
/// be checked against <c>CONFIG_REFERENCE.md</c>.
/// <para>
/// Two reasons a key is named here: an invariant operates over it, or code
/// reads it. Until 2.3 only the first applied, so `Gate:MaxDelta` and
/// `Gate:MaxDte` were here and the other four gate bounds were literals in the
/// seeder. The gate reads all six as of the simulated date [D-W26], so all six
/// are named and none is a literal at a call site.
/// </para>
/// <para>
/// The rest of the store's keys are still the seeder's business and are
/// declared with their values, because a key with no code that reads it has
/// nothing to name it for.
/// </para>
/// </remarks>
public static class ConfigKeys
{
    public const string GateMaxSpreadFractionOfMid = "Gate:MaxSpreadFractionOfMid";
    public const string GateMinPremium = "Gate:MinPremium";
    public const string GateMaxDelta = "Gate:MaxDelta";
    public const string GateMinDte = "Gate:MinDte";
    public const string GateMaxDte = "Gate:MaxDte";
    public const string GateEarningsClearanceDays = "Gate:EarningsClearanceDays";
    public const string TrialMaxTrialDays = "Trial:MaxTrialDays";

    public const string BaselineDeltaMax = "Policy:Baseline:DeltaMax";
    public const string RandomDeltaMax = "Policy:Random:DeltaMax";

    /// <summary>
    /// The bands the delta ceiling is checked against [D-W23], as key and name.
    /// </summary>
    /// <remarks>
    /// The name travels with the key so a refusal can say which band it failed
    /// against rather than only that one of them failed, which is why
    /// <see cref="ConfigurationInvariants.BandsTighterThanCeiling"/> exists.
    /// <para>
    /// A maker added later brings its band here. Until it does, the invariant
    /// does not know about it, and that is the direction this list can be wrong
    /// in: a band nothing names is a band the ceiling is not checked against.
    /// FX-EveryPolicyBandIsChecked holds that direction, reading every
    /// <c>Policy:*:DeltaMax</c> row out of <c>CONFIG_REFERENCE.md</c>.
    /// </para>
    /// <para>
    /// <b>That is the opposite direction from the other two vocabularies, and it
    /// is the rule rather than an inconsistency.</b>
    /// <see cref="Storage.DecimalColumns"/> and
    /// <see cref="Storage.AppendOnlyTables"/> are checked list to document,
    /// because there the error is a name with no table behind it. Here it is
    /// document to list, because the error is a band with no entry. Each is
    /// checked standing in the direction in which absence causes the bad outcome.
    /// The consequence differs in kind too: an incomplete catch-list still
    /// catches what is on it, where an incomplete band list makes a violating
    /// configuration pass.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<(string Key, string Name)> PolicyBandCeilings { get; } =
    [
        (BaselineDeltaMax, "Baseline"),
        (RandomDeltaMax, "Random"),
    ];

    /// <summary>
    /// Every key D-W23's invariant needs before it can be evaluated.
    /// </summary>
    public static IReadOnlySet<string> DeltaCeilingKeys { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            GateMaxDelta,
            BaselineDeltaMax,
            RandomDeltaMax,
        };

    /// <summary>
    /// Every key D-W24's invariant needs before it can be evaluated.
    /// </summary>
    public static IReadOnlySet<string> TrialBoundKeys { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            GateMaxDte,
            TrialMaxTrialDays,
        };

    /// <summary>
    /// Every key belonging to any cross-key invariant.
    /// </summary>
    /// <remarks>
    /// A write touching one of these must leave the store holding that
    /// invariant's whole key set [D-W34]. A write touching none of them is
    /// permitted whatever else is absent.
    /// </remarks>
    public static IReadOnlySet<string> InvariantKeys { get; } =
        DeltaCeilingKeys.Concat(TrialBoundKeys).ToHashSet(StringComparer.Ordinal);
}
