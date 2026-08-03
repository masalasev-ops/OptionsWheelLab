using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// A quote priced into cash, with the commission kept apart [D-W12, D-W50].
/// </summary>
/// <remarks>
/// Not a registered fixture: 3.4's registered check is the worked example's
/// total, and this is the arithmetic that check rests on, asserted where it can
/// be isolated.
/// </remarks>
public sealed class FillModelTests
{
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Session = new(2026, 3, 2);

    /// <summary>
    /// §4's fill table, which is the arithmetic every other figure rests on.
    /// </summary>
    /// <remarks>
    /// The document states gross credit, commission and net credit as three
    /// columns for three strikes. The model produces the first two and derives
    /// the third, which is the shape D-W50 chose: a stored net would state one
    /// fact twice.
    /// <para>
    /// <b>The cases are quoted and parsed, because <c>InlineData</c> cannot carry
    /// a decimal.</b> A theory taking <c>double</c> and casting is the shape the
    /// floating-point guard exists to refuse, and it refused this on its first
    /// run. It is the same reason a synthetic chain quotes its numbers: an
    /// unquoted one binds to a double and no scan in this repository would catch
    /// what the cast lost.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("0.30", "30.00", "29.35")]
    [InlineData("0.55", "55.00", "54.35")]
    [InlineData("0.95", "95.00", "94.35")]
    public void The_worked_examples_fill_table_is_reproduced(
        string bid,
        string gross,
        string net)
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var fill = new FillModel(new AsOfConfiguration(connection))
            .Sell(StoreDecimal.ParseStored(bid), Session);

        Assert.Equal(StoreDecimal.ParseStored(gross), fill.Premium);
        Assert.Equal(0.65m, fill.Commission);
        Assert.Equal(StoreDecimal.ParseStored(net), fill.Net);
    }

    /// <summary>
    /// A purchase pays the ask and is charged the same commission [D-W49].
    /// </summary>
    /// <remarks>
    /// The premium carries the direction and the commission does not, which is
    /// what makes <c>Premium - Commission</c> the net without a case: buying at
    /// 5.40 costs 540.00 and the commission adds to the cost rather than
    /// offsetting it.
    /// </remarks>
    [Fact]
    public void A_purchase_debits_the_premium_and_is_still_charged()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var fill = new FillModel(new AsOfConfiguration(connection)).Buy(5.40m, Session);

        Assert.Equal(-540.00m, fill.Premium);
        Assert.Equal(0.65m, fill.Commission);
        Assert.Equal(-540.65m, fill.Net);
    }

    /// <summary>
    /// The commission is per contract, so a larger order pays more.
    /// </summary>
    /// <remarks>
    /// Nothing sizes a position yet, so no path reaches this today. It is
    /// asserted because a model charging per order rather than per contract would
    /// agree with every single-contract figure in `WORKED_EXAMPLE.md` and be
    /// wrong from the first decision that sizes.
    /// </remarks>
    [Fact]
    public void The_commission_is_per_contract()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var fill = new FillModel(new AsOfConfiguration(connection)).Sell(0.95m, Session, 3);

        Assert.Equal(285.00m, fill.Premium);
        Assert.Equal(1.95m, fill.Commission);
    }

    /// <summary>
    /// The rate in force on the session is the one charged [D-W26].
    /// </summary>
    /// <remarks>
    /// A trial priced before a rate change keeps the rate it was actually
    /// charged. Re-scoring a decision under a rate it never paid would be a
    /// different trial, which is what as-of resolution buys in a cost path.
    /// </remarks>
    [Fact]
    public void A_fill_is_charged_the_rate_in_force_on_its_session()
    {
        using var store = SeededStore();
        using var connection = store.Connections.Open(StoreAccess.Write);

        new ConfigWriter(connection).Append(
            ConfigKeys.CostsCommissionPerContract,
            "1.00",
            new DateTimeOffset(2026, 6, 1, 21, 0, 0, TimeSpan.Zero));

        var model = new FillModel(new AsOfConfiguration(connection));

        Assert.Equal(0.65m, model.Sell(0.95m, Session).Commission);
        Assert.Equal(1.00m, model.Sell(0.95m, new DateOnly(2026, 7, 1)).Commission);
    }

    /// <summary>
    /// The machine's legs write two entries, and the second is the commission
    /// [D-W50].
    /// </summary>
    [Fact]
    public void An_opening_leg_writes_the_premium_and_the_commission()
    {
        var opened = OpenedTransition();

        Assert.Equal(
            [LedgerEntryKind.PremiumReceived, LedgerEntryKind.Commission],
            opened.Entries.Select(entry => entry.Kind));

        Assert.Equal(95.00m, opened.Entries[0].Amount);
        Assert.Equal(-0.65m, opened.Entries[1].Amount);
        Assert.Equal(94.35m, opened.State.PremiumBanked);
    }

    /// <summary>
    /// Net basis is the figure the separate commission exists for [D-W50].
    /// </summary>
    /// <remarks>
    /// §6.3 states 49.0565, which is 50.00 less the credit after commission. A
    /// ledger that netted the commission would reach the same figure by accident;
    /// one that separated it and did not fold it back would give 49.05, which is
    /// the half-cent this assertion is about.
    /// </remarks>
    [Fact]
    public void Net_basis_reads_the_credit_after_commission()
    {
        var assigned = Machine().Advance(
            OpenedTrial(), TrialScenario.Session(FirstExpiry, close: 48.90m)).State;

        Assert.Equal(50.00m, assigned.GrossBasis);
        Assert.Equal(49.0565m, assigned.NetBasis);
        Assert.NotEqual(49.05m, assigned.NetBasis);
    }

    /// <summary>
    /// A fee of zero writes no row, where an expiry that pays nothing does
    /// [D-W48, D-W50].
    /// </summary>
    /// <remarks>
    /// The distinction is between a cost and an event. An expiry is an event
    /// whose cash happens to be zero and the projection has to know it happened;
    /// a commission or a fee is a cost, and one that was not charged is not a
    /// fact about the trial.
    /// </remarks>
    [Fact]
    public void A_fee_of_zero_writes_no_row_and_a_fee_charged_does()
    {
        var free = Machine().Advance(
            OpenedTrial(), TrialScenario.Session(FirstExpiry, close: 48.90m));

        Assert.Equal(LedgerEntryKind.Assignment, Assert.Single(free.Entries).Kind);

        var charging = new WheelStateMachine(
            Calendar,
            TrialScenario.Seeded,
            Costs with { AssignmentFee = 1.10m });

        var charged = charging.Advance(
            OpenedTrial(), TrialScenario.Session(FirstExpiry, close: 48.90m));

        Assert.Equal(
            [LedgerEntryKind.Assignment, LedgerEntryKind.AssignmentFee],
            charged.Entries.Select(entry => entry.Kind));
        Assert.Equal(-1.10m, charged.Entries[1].Amount);
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
