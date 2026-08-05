using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ConfigWriteRefusesInvariantBreach: a config version violating a cross-key
/// invariant is refused and no row is written.
/// </summary>
/// <remarks>
/// Both invariants, because both are enforced [D-W23, D-W24], and the
/// unevaluable case, because a write leaving an invariant without an operand is
/// refused too [D-W34].
/// <para>
/// <b>No row is written</b> is asserted by comparing the table before and after
/// rather than by the absence of an exception. The refusal happens inside a
/// transaction and the table is append-only [D-W8], so a partial write would be
/// permanent and would not announce itself.
/// </para>
/// <para>
/// Against a real migrated store rather than a fake, since the transaction and
/// the trigger are the things under test.
/// </para>
/// </remarks>
public sealed class FX_ConfigWriteRefusesInvariantBreach
{
    private static readonly DateTimeOffset SetAt =
        new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The bands as the seed sets them, so the violating cases below differ from
    /// a passing write in one value.
    /// </summary>
    private static readonly ConfigEntry[] Bands =
    [
        new(ConfigKeys.BaselineDeltaMax, "0.30"),
        new(ConfigKeys.RandomDeltaMax, "0.35"),

        // The learner's, from 4.3. It sits below every ceiling these cases use,
        // so it never becomes the violating band and the cases still turn on the
        // one value they change. Adding a band to the invariant is what obliges
        // it here: a set short of one operand refuses for want of the operand
        // rather than for the breach under test [D-W34].
        new(ConfigKeys.LearnerDeltaMax, "0.20"),
    ];

    // D-W23: the ceiling is no tighter than any policy band.

    [Fact]
    public void A_ceiling_tighter_than_a_band_is_refused_naming_that_band()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            new ConfigWriter(connection).AppendAll(
                [new ConfigEntry(ConfigKeys.GateMaxDelta, "0.32"), .. Bands],
                SetAt));

        Assert.Contains("Random", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Baseline", thrown.Message, StringComparison.Ordinal);
        Assert.Empty(RowsIn(connection));
    }

    /// <summary>
    /// Every failing band, not the first, in the spirit of recording every
    /// failing reason [D-W22].
    /// </summary>
    [Fact]
    public void A_ceiling_tighter_than_every_band_names_all_of_them()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            new ConfigWriter(connection).AppendAll(
                [new ConfigEntry(ConfigKeys.GateMaxDelta, "0.10"), .. Bands],
                SetAt));

        Assert.Contains("Baseline", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("Random", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The boundary case, which is the one the seeded values sit on: 0.35
    /// against a widest band of 0.35 is permitted, because the ceiling bounds the
    /// band rather than cutting into it.
    /// </summary>
    [Fact]
    public void A_ceiling_equal_to_the_loosest_band_is_accepted()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).AppendAll(
            [new ConfigEntry(ConfigKeys.GateMaxDelta, "0.35"), .. Bands],
            SetAt);

        // Counted from the set rather than written out, because a band added to
        // the invariant would otherwise make this case fail for arithmetic.
        Assert.Equal(Bands.Length + 1, RowsIn(connection).Count);
    }

    /// <summary>
    /// A later version is guarded exactly as the first is. This is why the check
    /// is in the write path rather than in the seeder: versions are insertable
    /// while the process runs, so a startup check would leave every version after
    /// the first unguarded [D-W27].
    /// </summary>
    [Fact]
    public void A_revision_that_tightens_the_ceiling_below_a_band_is_refused()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        writer.AppendAll([new ConfigEntry(ConfigKeys.GateMaxDelta, "0.35"), .. Bands], SetAt);

        var before = RowsIn(connection);

        Assert.Throws<InvalidOperationException>(
            () => writer.Append(ConfigKeys.GateMaxDelta, "0.20", SetAt.AddDays(1)));

        Assert.Equal(before, RowsIn(connection));
    }

    // D-W24: MaxDte is strictly below the trial bound.

    [Fact]
    public void A_max_dte_above_the_trial_bound_is_refused()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            new ConfigWriter(connection).AppendAll(
                [
                    new ConfigEntry(ConfigKeys.GateMaxDte, "150"),
                    new ConfigEntry(ConfigKeys.TrialMaxTrialDays, "120"),
                ],
                SetAt));

        Assert.Contains("150", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("120", thrown.Message, StringComparison.Ordinal);
        Assert.Empty(RowsIn(connection));
    }

    /// <summary>
    /// Equality fails: a contract expiring exactly on the bound would race the
    /// forced close, and which one wins is not something the design leaves open.
    /// </summary>
    [Fact]
    public void A_max_dte_equal_to_the_trial_bound_is_refused()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Assert.Throws<InvalidOperationException>(() =>
            new ConfigWriter(connection).AppendAll(
                [
                    new ConfigEntry(ConfigKeys.GateMaxDte, "120"),
                    new ConfigEntry(ConfigKeys.TrialMaxTrialDays, "120"),
                ],
                SetAt));

        Assert.Empty(RowsIn(connection));
    }

    /// <summary>The seeded pair, which is the case that has to pass.</summary>
    [Fact]
    public void The_seeded_pair_of_seventy_and_a_hundred_and_twenty_is_accepted()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).AppendAll(
            [
                new ConfigEntry(ConfigKeys.GateMaxDte, "70"),
                new ConfigEntry(ConfigKeys.TrialMaxTrialDays, "120"),
            ],
            SetAt);

        Assert.Equal(2, RowsIn(connection).Count);
    }

    // D-W34: a write leaving an invariant unevaluable is refused.

    /// <summary>
    /// The consequence D-W34 exists for: the pair is atomic by the write path
    /// rather than by the seeder's discipline.
    /// </summary>
    [Fact]
    public void Max_dte_alone_is_refused_because_the_invariant_cannot_be_evaluated()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => new ConfigWriter(connection).Append(ConfigKeys.GateMaxDte, "70", SetAt));

        Assert.Contains(ConfigKeys.TrialMaxTrialDays, thrown.Message, StringComparison.Ordinal);
        Assert.Empty(RowsIn(connection));
    }

    [Fact]
    public void The_trial_bound_alone_is_refused_too()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Assert.Throws<InvalidOperationException>(
            () => new ConfigWriter(connection).Append(ConfigKeys.TrialMaxTrialDays, "120", SetAt));

        Assert.Empty(RowsIn(connection));
    }

    /// <summary>
    /// A ceiling written without the bands is refused, which is what makes
    /// FX-CeilingNotInsidePolicyBand's vacuous pass over an empty band set
    /// unreachable through the write path.
    /// </summary>
    [Fact]
    public void The_ceiling_without_its_bands_is_refused_rather_than_passing_vacuously()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => new ConfigWriter(connection).Append(ConfigKeys.GateMaxDelta, "0.35", SetAt));

        Assert.Contains(ConfigKeys.BaselineDeltaMax, thrown.Message, StringComparison.Ordinal);
        Assert.Empty(RowsIn(connection));
    }

    /// <summary>
    /// A key already stored counts as present, so completing a partly written
    /// invariant is permitted. Without this the pair could never be repaired
    /// after a store was left half seeded.
    /// </summary>
    [Fact]
    public void An_operand_already_stored_satisfies_the_requirement()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        writer.AppendAll(
            [
                new ConfigEntry(ConfigKeys.GateMaxDte, "70"),
                new ConfigEntry(ConfigKeys.TrialMaxTrialDays, "120"),
            ],
            SetAt);

        Assert.Equal(2, writer.Append(ConfigKeys.GateMaxDte, "60", SetAt.AddDays(1)));
    }

    /// <summary>
    /// The other half of D-W34: a write touching no invariant key is permitted
    /// regardless of what else is absent. Into an empty store, so nothing else
    /// could be supplying the operands.
    /// </summary>
    [Fact]
    public void A_write_touching_no_invariant_key_succeeds_into_an_empty_store()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        Assert.Equal(1, new ConfigWriter(connection).Append("Gate:MinPremium", "0.30", SetAt));
    }

    // The seed's own values, through the same path.

    [Fact]
    public void Every_seeded_value_passes_both_invariants()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var outcome = new ConfigWriter(connection).AppendMissing(SeedValues.All, SetAt);

        Assert.Equal(SeedValues.All.Count, outcome.Written.Count);
        Assert.Empty(outcome.Skipped);
        Assert.All(RowsIn(connection), row => Assert.Equal(1, row.Version));
    }

    /// <summary>
    /// A second seed writes nothing rather than appending an identical version +
    /// 1, which would fill the history with revisions that revised nothing and
    /// would overwrite an operator's later value on every run.
    /// </summary>
    [Fact]
    public void A_second_seed_writes_nothing_and_leaves_the_rows_untouched()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        var writer = new ConfigWriter(connection);
        writer.AppendMissing(SeedValues.All, SetAt);

        var before = RowsIn(connection);
        var outcome = writer.AppendMissing(SeedValues.All, SetAt.AddDays(1));

        Assert.Empty(outcome.Written);
        Assert.Equal(SeedValues.All.Count, outcome.Skipped.Count);
        Assert.Equal(before, RowsIn(connection));
    }

    /// <summary>
    /// Every seeded key is documented and classed <c>rows</c>. A key written to
    /// the store and absent from <c>CONFIG_REFERENCE.md</c> would be invisible to
    /// every check that reads the document, and a key classed <c>app</c> is bound
    /// from appsettings and has no business being a row [D-W26].
    /// </summary>
    [Fact]
    public void Every_seeded_key_is_documented_as_a_rows_key()
    {
        var documented = ConfigReferenceParser
            .Parse(File.ReadAllText(RepoRoot.ConfigReferencePath))
            .Keys
            .ToDictionary(key => key.Key, key => key.Store, StringComparer.Ordinal);

        foreach (var entry in SeedValues.All)
        {
            Assert.True(
                documented.TryGetValue(entry.Key, out var store),
                $"'{entry.Key}' is seeded but has no row in CONFIG_REFERENCE.md.");

            Assert.Equal(ConfigReferenceParser.RowsClass, store);
        }
    }

    /// <summary>
    /// Every key the invariants name is seeded, since an invariant whose operands
    /// are never written can only ever refuse or pass vacuously.
    /// </summary>
    [Fact]
    public void Every_invariant_key_is_seeded()
    {
        var seeded = SeedValues.All.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(ConfigKeys.InvariantKeys, key => !seeded.Contains(key));
    }

    private sealed record ConfigRow(string Key, int Version, string Value, string SetAt);

    private static IReadOnlyList<ConfigRow> RowsIn(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT key, version, value, set_at FROM config_rows ORDER BY key, version;";

        var rows = new List<ConfigRow>();
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            rows.Add(new ConfigRow(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return rows;
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(SetAt);
        return store;
    }
}
