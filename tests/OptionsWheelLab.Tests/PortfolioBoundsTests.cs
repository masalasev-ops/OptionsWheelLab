using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The caps resolve as of the simulated date, and stop the evaluation when they
/// cannot.
/// </summary>
/// <remarks>
/// <see cref="GateBoundsTests"/>' shape for the second bound record, and 2.4's
/// definition of done asserted rather than restated: a cap reads its value as of
/// the simulated date [D-W26], never as it stands now, and an unresolvable one
/// stops [D-W37].
/// <para>
/// The as-of case is the one this record makes concrete rather than theoretical.
/// Equity is the operator's statement of the account it is running, and an
/// operator who revises it has not retrospectively changed what the caps were on
/// a date already decided.
/// </para>
/// </remarks>
public sealed class PortfolioBoundsTests
{
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Revised =
        new(2026, 6, 1, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_seeded_caps_resolve_after_the_seed()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var bounds = PortfolioBounds.ResolveFor(
            new AsOfConfiguration(connection), new DateOnly(2026, 3, 1));

        Assert.Equal(100_000.00m, bounds.Equity);
        Assert.Equal(0.25m, bounds.PerNameCapFraction);
        Assert.Equal(0.60m, bounds.TotalCapFraction);
        Assert.Equal(0.60m, bounds.SimultaneousAssignmentLimitFraction);
    }

    /// <summary>
    /// A deposit does not loosen the caps on a date already decided [D-W26].
    /// </summary>
    /// <remarks>
    /// This is the case the equity key exists to make answerable. A denominator
    /// computed from the run's own state would move with the run, so the caps
    /// would have been whatever the account happened to hold; resolved as-of,
    /// a later version answers only for later dates.
    /// </remarks>
    [Fact]
    public void A_later_equity_version_does_not_reach_an_earlier_date()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(ConfigKeys.RiskEquity, "150000.00", Revised);

        var configuration = new AsOfConfiguration(connection);

        Assert.Equal(
            100_000.00m,
            PortfolioBounds.ResolveFor(configuration, new DateOnly(2026, 3, 1)).Equity);

        Assert.Equal(
            150_000.00m,
            PortfolioBounds.ResolveFor(configuration, new DateOnly(2026, 7, 1)).Equity);
    }

    /// <summary>
    /// The two equal fractions come from their own keys, shown by revising one.
    /// </summary>
    /// <remarks>
    /// The seeded values are both 0.60, so asserting them cannot tell a record
    /// reading two keys from one reading a single key twice. Revising one is
    /// what separates them, and it costs nothing: config rows are versioned, so
    /// the later version answers only for later dates and the seeded pair is
    /// still what an earlier date resolves.
    /// </remarks>
    [Fact]
    public void The_two_equal_fractions_resolve_from_their_own_keys()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(
            ConfigKeys.RiskSimultaneousAssignmentLimitFraction, "0.30", Revised);

        var later = PortfolioBounds.ResolveFor(
            new AsOfConfiguration(connection), new DateOnly(2026, 7, 1));

        Assert.Equal(0.60m, later.TotalCapFraction);
        Assert.Equal(0.30m, later.SimultaneousAssignmentLimitFraction);
    }

    /// <summary>
    /// An unresolvable cap stops the evaluation naming the key and the date
    /// [D-W37].
    /// </summary>
    /// <remarks>
    /// Admitting would silently drop a structural risk control [D-W11] and leave
    /// a run that looks normal and is unconstrained, which is the outcome this
    /// record's four values exist to prevent. It is the read-side of D-W34.
    /// </remarks>
    [Fact]
    public void A_date_before_the_caps_were_written_stops_the_evaluation()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => PortfolioBounds.ResolveFor(
                new AsOfConfiguration(connection), new DateOnly(2025, 12, 31)));

        Assert.Contains("Risk:", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2025-12-31", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("D-W37", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// It stops rather than admitting or rejecting, and no overload fills a
    /// missing value in.
    /// </summary>
    [Fact]
    public void An_unresolvable_cap_yields_no_bounds_at_all()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var configuration = new AsOfConfiguration(connection);

        Assert.Throws<InvalidOperationException>(
            () => PortfolioBounds.ResolveFor(configuration, new DateOnly(2025, 6, 1)));

        Assert.NotNull(PortfolioBounds.ResolveFor(configuration, new DateOnly(2026, 3, 1)));
    }

    /// <summary>
    /// Each of the four is required, shown one at a time.
    /// </summary>
    /// <remarks>
    /// 2.3 found that a mutation confined to one site is not a mutation of the
    /// behaviour: defaulting either half of the bound resolution passed every
    /// test, because each left the other still raising. The same trap is here
    /// with four halves, so each key is withheld on its own and the message is
    /// checked to name that key rather than merely to be a raise.
    /// </remarks>
    [Theory]
    [InlineData(ConfigKeys.RiskEquity)]
    [InlineData(ConfigKeys.RiskPerNameCapFraction)]
    [InlineData(ConfigKeys.RiskTotalCapFraction)]
    [InlineData(ConfigKeys.RiskSimultaneousAssignmentLimitFraction)]
    public void Withholding_any_one_cap_stops_the_evaluation_naming_it(string withheld)
    {
        var store = TempStore.Empty();

        try
        {
            new MigrationRunner(store.Connections).Run(Seeded);

            using var connection = store.Connections.Open(StoreAccess.Write);

            new ConfigWriter(connection).AppendAll(
                [.. SeedValues.All.Where(entry => entry.Key != withheld)], Seeded);

            var thrown = Assert.Throws<InvalidOperationException>(
                () => PortfolioBounds.ResolveFor(
                    new AsOfConfiguration(connection), new DateOnly(2026, 3, 1)));

            Assert.Contains(withheld, thrown.Message, StringComparison.Ordinal);
        }
        finally
        {
            store.Dispose();
        }
    }

    private static TempStore SeededStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Seeded);

        using var connection = store.Connections.Open(StoreAccess.Write);
        new ConfigWriter(connection).AppendAll(SeedValues.All, Seeded);

        return store;
    }
}
