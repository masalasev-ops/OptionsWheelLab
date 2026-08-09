using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-TrialCompleteIncludesAssignment: the assigned trial totals 498.05
/// [WORKED_EXAMPLE §6.3].
/// </summary>
/// <remarks>
/// The trial is measured from open through to return to cash, with the assigned
/// shares inside the number rather than treated as an exit [D-W17]. This is the
/// clause that keeps the strategy's downside inside the measurement, and the
/// seventh fixture reading that document: the six before it read its chain, its
/// verdicts and its bases, and this is the first to read its ledger.
/// <para>
/// <b>Nothing here supplies a figure the machine could have produced.</b> The
/// quotes are §2's and §5's, the commission comes from configuration through
/// <see cref="FillModel"/>, and every cash amount is what the state machine
/// wrote. A fixture handed 94.35 would assert that a document adds up rather than
/// that this lab reproduces it.
/// </para>
/// <para>
/// <b>The correspondence is per date, not row for row.</b> §6.3 nets the
/// commission into each cell, writing <c>+94.35</c> where the ledger writes
/// <c>+95.00</c> and <c>-0.65</c>, because the document nets what [D-W50]
/// separates. So each cash cell equals the SUM of that date's entries and the
/// ledger carries more rows than the table. A total-only assertion would miss
/// this entirely: a netted ledger produces one entry per date, matches every cell
/// exactly, and loses the commission.
/// </para>
/// </remarks>
public sealed class FX_TrialCompleteIncludesAssignment
{
    private static readonly DateTimeOffset Seeded =
        new(2026, 1, 1, 21, 0, 0, TimeSpan.Zero);

    /// <summary>§6.3's six cash cells, in the document's order.</summary>
    private static readonly (DateOnly Date, decimal Cash)[] Cells =
    [
        (Opened, 94.35m),
        (FirstExpiry, -5_000.00m),
        (MondayAfter, 69.35m),
        (SecondExpiry, 0.00m),
        (SecondMonday, 84.35m),
        (ThirdExpiry, 5_250.00m),
    ];

    [Fact]
    public void The_assigned_trial_totals_the_documents_figure()
    {
        var (state, entries) = WorkedExample();

        Assert.Equal(498.05m, entries.Sum(entry => entry.Amount));
        Assert.Equal(TrialCloseKind.CalledAway, state.CloseKind);
        Assert.Equal(ThirdExpiry, state.ClosedOn);
    }

    /// <summary>
    /// Each of §6.3's cash cells equals the sum of that date's entries.
    /// </summary>
    /// <remarks>
    /// The claim the total alone does not make. A machine that wrote one leg
    /// twice, or attributed a credit to the wrong session, would still total
    /// 498.05.
    /// </remarks>
    [Fact]
    public void Each_cash_cell_equals_the_sum_of_that_dates_entries()
    {
        var (_, entries) = WorkedExample();

        foreach (var (date, cash) in Cells)
        {
            Assert.Equal(
                cash,
                entries.Where(entry => entry.EntryDate == date).Sum(entry => entry.Amount));
        }

        // No entry falls outside the six dates the document states.
        Assert.All(entries, entry => Assert.Contains(entry.EntryDate, Cells.Select(c => c.Date)));
    }

    /// <summary>
    /// The ledger carries more rows than the table, which is what makes the
    /// correspondence a reconciliation.
    /// </summary>
    /// <remarks>
    /// Nine entries against six cells: three sales are two rows each, and the
    /// assignment, the worthless expiry and the call-away are one apiece. If this
    /// were six the commission would have been netted and every other assertion
    /// here would still pass.
    /// </remarks>
    [Fact]
    public void The_ledger_carries_more_rows_than_the_document_states_cells()
    {
        var (_, entries) = WorkedExample();

        Assert.Equal(6, Cells.Length);
        Assert.Equal(9, entries.Count);
    }

    /// <summary>
    /// The two questions the separate commission exists so the ledger can answer
    /// without arithmetic [D-W50].
    /// </summary>
    /// <remarks>
    /// What the trial paid in commission and what it received in premium, each
    /// read by kind rather than derived from the other. Three legs at 0.65 and
    /// three gross credits of 95.00, 70.00 and 85.00, which reconcile to the
    /// document: 250.00 less 1.95, less 5,000.00, plus 5,250.00.
    /// </remarks>
    [Fact]
    public void The_ledger_answers_what_was_paid_and_what_was_received()
    {
        var (_, entries) = WorkedExample();

        var commission = -entries
            .Where(entry => entry.Kind is LedgerEntryKind.Commission)
            .Sum(entry => entry.Amount);

        var premium = entries
            .Where(entry => entry.Kind is LedgerEntryKind.PremiumReceived)
            .Sum(entry => entry.Amount);

        Assert.Equal(1.95m, commission);
        Assert.Equal(250.00m, premium);
        Assert.Equal(498.05m, premium - commission - 5_000.00m + 5_250.00m);
    }

    /// <summary>
    /// Net basis reads the credit after commission, replayed from the ledger
    /// [D-W19, D-W50].
    /// </summary>
    /// <remarks>
    /// <b>Read from a replay rather than from the machine, and that is the whole
    /// point of the assertion.</b> The machine banks the fill's net whether the
    /// commission is a separate row or folded into the premium, so a state read
    /// from it gives 49.0565 either way and discriminates nothing. What the
    /// separate row costs is that a rebuild must fold it back, and a rebuild that
    /// treated <c>commission</c> as cash-only would give 49.05 here while every
    /// other figure in this fixture still reconciled.
    /// <para>
    /// Measured rather than assumed: a machine netting the commission passes
    /// three of this fixture's five cases and fails the row count and the two
    /// questions. This is the case that catches the other half of the grain, the
    /// projection's, which those two do not reach.
    /// </para>
    /// <para>
    /// <b>The state taken is the one the assignment produced, not the last one
    /// holding shares.</b> §6.3 states the basis at the moment of assignment, and
    /// net basis moves afterwards as premium accumulates: by the second covered
    /// call this trial's is 47.5195, since 248.05 has been banked against 100
    /// shares. That is [D-W19]'s two conventions diverging over a trial's life,
    /// which is the drift the gross-basis constraint exists to prevent, so it is
    /// the behaviour rather than an error to read past.
    /// </para>
    /// </remarks>
    [Fact]
    public void Net_basis_survives_the_round_trip_through_the_ledger()
    {
        var (_, entries) = WorkedExample();

        var assigned = TrialProjection
            .Replay(entries, TrialScenario.Seeded)
            .First(state => state.GrossBasis is not null);

        Assert.Equal(50.00m, assigned.GrossBasis);
        Assert.Equal(49.0565m, assigned.NetBasis);
        Assert.NotEqual(49.05m, assigned.NetBasis);
    }

    /// <summary>
    /// §6.3's trial, walked by the run.
    /// </summary>
    /// <remarks>
    /// <b>This fixture hand-inlined the walk until 3.5, and lifting it is what
    /// composed the run.</b> A run written fresh beside a test that walked the
    /// same trial would be two producers of one sequence, so the fixture calls
    /// the run and asserts its output, which is what it was always asserting.
    /// <para>
    /// §5 supplies the closes and §2 the bids. The two covered calls are chosen
    /// on the sessions the document writes them, which are the Mondays after each
    /// Friday expiry, and that is what next-session notification produces rather
    /// than a convenience [D-W39].
    /// </para>
    /// </remarks>
    private static (TrialState State, IReadOnlyList<LedgerEntry> Entries) WorkedExample()
    {
        var model = Model(out var store);

        using (store)
        {
            var run = new TrialRun(model, Calendar);

            var result = run.Walk(
                Machine(),
                WorkedExampleChain(),
                Opened,
                ThirdMonday,
                [
                    new OpenPut(Opened, Put(50.00m, FirstExpiry), Bid: 0.95m),
                    new WriteCoveredCall(MondayAfter, Call(52.50m, SecondExpiry), Bid: 0.70m),
                    new WriteCoveredCall(SecondMonday, Call(52.50m, ThirdExpiry), Bid: 0.85m),
                ]);

            return (result.State, result.Entries);
        }
    }

    /// <summary>
    /// §5's closes and the quotes the trial's own contracts carried.
    /// </summary>
    /// <remarks>
    /// Only the sessions §5 states, so the run steps six. The quotes are the ones
    /// the short is looked up by, and none is needed here: no session in this
    /// trial reaches a rule that reads a price, since nothing rolls, nothing hits
    /// a bound and no ex-dividend date falls inside it.
    /// </remarks>
    private static Core.Synthetic.SyntheticChain WorkedExampleChain() =>
        new(
            Symbol,
            [
                new Core.Synthetic.UnderlyingBar(Symbol, Opened, Close: 52.40m),
                new Core.Synthetic.UnderlyingBar(Symbol, FirstExpiry, Close: 48.90m),
                new Core.Synthetic.UnderlyingBar(Symbol, MondayAfter, Close: 48.95m),
                new Core.Synthetic.UnderlyingBar(Symbol, SecondExpiry, Close: 51.20m),
                new Core.Synthetic.UnderlyingBar(Symbol, SecondMonday, Close: 51.30m),
                new Core.Synthetic.UnderlyingBar(Symbol, ThirdExpiry, Close: 53.40m),
            ],
            [],
            [],
            []);

    /// <summary>A fill model over a seeded store, so the commission is configuration.</summary>
    private static FillModel Model(out TempStore store)
    {
        store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Seeded);

        var connection = store.Connections.Open(StoreAccess.Write);
        new ConfigWriter(connection).AppendAll(SeedValues.All, Seeded);

        return new FillModel(new AsOfConfiguration(connection));
    }
}
