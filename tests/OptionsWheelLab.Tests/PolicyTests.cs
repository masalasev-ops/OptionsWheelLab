using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Decisions;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The policy record and its band. Not a registered fixture, so not named
/// <c>FX-*</c>.
/// </summary>
public sealed class PolicyTests
{
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Simulated = new(2026, 3, 2);

    [Fact]
    public void The_baseline_resolves_the_band_and_window_the_document_states()
    {
        using var store = Seed();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var policy = Policy.ForBaseline(new AsOfConfiguration(connection), Simulated);

        Assert.Equal(new Policy(0.20m, 0.30m, 30, 60, Version: 1), policy);
    }

    /// <summary>
    /// The learner's band is the baseline's width shifted down by one width.
    /// </summary>
    [Fact]
    public void The_learner_resolves_its_own_band_on_the_baselines_window()
    {
        using var store = Seed();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var policy = Policy.ForLearner(new AsOfConfiguration(connection), Simulated);

        Assert.Equal(new Policy(0.10m, 0.20m, 30, 60, Version: 1), policy);
    }

    /// <summary>
    /// The random control's band, on the baseline's window rather than one of its
    /// own.
    /// </summary>
    [Fact]
    public void The_random_control_resolves_its_band_on_the_baselines_window()
    {
        using var store = Seed();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var policy = Policy.ForRandom(new AsOfConfiguration(connection), Simulated);

        Assert.Equal(new Policy(0.10m, 0.35m, 30, 60, Version: 1), policy);
    }

    /// <summary>
    /// The coupling made observable: without the baseline's window the random
    /// control cannot resolve at all.
    /// </summary>
    /// <remarks>
    /// <c>Policy:Random:</c> carrying no DTE keys is stated in
    /// <c>CONFIG_REFERENCE.md</c> as the coupling rather than an omission, and
    /// prose is all that says it. This is the case that would fail if the random
    /// factory quietly grew its own window keys, and it names the baseline's key
    /// because that is the key that could not resolve.
    /// </remarks>
    [Fact]
    public void The_random_control_stops_without_the_baselines_window()
    {
        using var store = Seed(without: "Policy:Baseline:DteMin");
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => Policy.ForRandom(new AsOfConfiguration(connection), Simulated));

        Assert.Contains("Policy:Baseline:DteMin", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2026-03-02", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A candidate at exactly <c>DeltaMin</c> is in the band.
    /// </summary>
    /// <remarks>
    /// <b>The only case that notices the convention being read the other way.</b>
    /// The worked example's 45.00 put carries a delta of exactly 0.10 and the
    /// learner's floor is exactly 0.10, and that candidate loses on credit either
    /// way, so a fixture reproducing the document passes under an exclusive
    /// reading too. A band admits its own bounds, which is the fifth constraint
    /// in this lab to state its own boundary [D-W24's precedent].
    /// </remarks>
    [Theory]
    [InlineData("0.10", true)]
    [InlineData("0.20", true)]
    [InlineData("0.0999", false)]
    [InlineData("0.2001", false)]
    public void The_band_admits_its_own_bounds(string delta, bool admitted)
    {
        var learner = new Policy(0.10m, 0.20m, 30, 60, Version: 1);

        Assert.Equal(admitted, learner.Admits(StoreDecimal.ParseStored(delta), 46));
    }

    /// <summary>
    /// The window admits its own bounds too, on the same convention.
    /// </summary>
    [Theory]
    [InlineData(30, true)]
    [InlineData(60, true)]
    [InlineData(29, false)]
    [InlineData(61, false)]
    public void The_window_admits_its_own_bounds(int daysToExpiry, bool admitted)
    {
        var learner = new Policy(0.10m, 0.20m, 30, 60, Version: 1);

        Assert.Equal(admitted, learner.Admits(0.16m, daysToExpiry));
    }

    /// <summary>
    /// The band compares magnitude, as the gate's ceiling does [D-W23].
    /// </summary>
    /// <remarks>
    /// The chain states a put's delta as negative and a band written 0.20 to 0.30
    /// means magnitudes, so a comparison on the signed value would admit nothing
    /// at all on the put side and the failure would look like an empty feasible
    /// set rather than a sign error.
    /// </remarks>
    [Fact]
    public void The_band_compares_magnitude_rather_than_sign()
    {
        var baseline = new Policy(0.20m, 0.30m, 30, 60, Version: 1);

        Assert.True(baseline.Admits(-0.24m, 46));
        Assert.True(baseline.Admits(0.24m, 46));
    }

    /// <summary>
    /// A candidate with no delta is admitted by no band.
    /// </summary>
    /// <remarks>
    /// The gate's ceiling only fires when a delta is present, so a deltaless
    /// quote reaches a maker as feasible, and a band is a claim about delta that
    /// an absent one cannot satisfy. No synthetic chain currently omits a delta:
    /// the only deltaless quote in the tree is the unit case asserting the gate
    /// does not treat one as a breach, so a green suite is not evidence that this
    /// cannot arise.
    /// </remarks>
    [Fact]
    public void No_band_admits_a_candidate_with_no_delta()
    {
        Assert.False(new Policy(0.10m, 0.20m, 30, 60, Version: 1).Admits(null, 46));
        Assert.False(new Policy(0.00m, 1.00m, 1, 999, Version: 1).Admits(null, 46));
    }

    /// <summary>
    /// The recorded version is the newest among the rows the policy read, and the
    /// random maker's moves when the window it borrows moves.
    /// </summary>
    /// <remarks>
    /// <b>The borrowing made observable in the record.</b> Step 2 made it
    /// observable in a refusal, by seeding everything but the baseline's window
    /// and watching the random factory stop. This is the other half: a
    /// prefix-scoped maximum would miss the two rows the random maker does not
    /// own, so its version would not move when the window it actually uses did,
    /// and a re-score would attribute a decision to a policy generation that was
    /// not the one in force.
    /// </remarks>
    [Fact]
    public void A_new_baseline_window_moves_the_random_version_and_not_the_learners()
    {
        using var store = Seed();
        using var write = store.Connections.Open(StoreAccess.Write);

        var configuration = new AsOfConfiguration(write);

        var randomBefore = Policy.ForRandom(configuration, Simulated).Version;
        var learnerBefore = Policy.ForLearner(configuration, Simulated).Version;

        new ConfigWriter(write).Append("Policy:Baseline:DteMin", "31", Seeded);

        Assert.Equal(randomBefore + 1, Policy.ForRandom(configuration, Simulated).Version);
        Assert.Equal(learnerBefore, Policy.ForLearner(configuration, Simulated).Version);

        // And the baseline's moves too, since it owns the row.
        Assert.Equal(randomBefore + 1, Policy.ForBaseline(configuration, Simulated).Version);
    }

    private static TempStore Seed(string? without = null)
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Seeded);

        using var write = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(write).AppendAll(
            [.. SeedValues.All.Where(entry => entry.Key != without)],
            Seeded);

        return store;
    }
}
