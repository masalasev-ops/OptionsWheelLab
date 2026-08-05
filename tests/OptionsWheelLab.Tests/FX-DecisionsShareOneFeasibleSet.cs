using System.Text.RegularExpressions;
using OptionsWheelLab.Core.Generation;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-DecisionsShareOneFeasibleSet: two decisions made against the same symbol,
/// session and right reference one stored set rather than two copies, and their
/// portfolio verdicts differ where their books do [D-W52].
/// </summary>
/// <remarks>
/// <b>[D-W4] by construction rather than by assertion.</b> That decision requires
/// makers holding the same position in a name to be offered byte-identical
/// candidate sets. Storing the set once and referencing it makes the property
/// true of the record because there is only one set, where storing it per decision
/// would make it a thing three writes have to agree about.
/// <para>
/// <b>The half that is not shared is what stops this being a storage
/// optimisation.</b> The portfolio verdicts are computed against a book, and two
/// makers do not share one once their positions diverge, so a record folding them
/// into the shared set would attribute one maker's cap breach to another.
/// </para>
/// </remarks>
public sealed class FX_DecisionsShareOneFeasibleSet
{
    /// <summary>
    /// A book already committing enough in this name that the 50.00 put breaches
    /// the per-name cap, where an empty book does not.
    /// </summary>
    /// <remarks>
    /// The put commits 5,000.00, being strike times multiplier [D-W17]. The
    /// seeded equity is 100,000.00 at a per-name fraction of 0.25, so the cap is
    /// 25,000.00 and 21,000.00 already committed leaves 4,000.00, which the
    /// candidate breaches where an empty book's 25,000.00 does not.
    /// <para>
    /// <b>The figures are the seeded configuration's, not the worked example's.</b>
    /// That document derives a per-name headroom of 5,100.00 from its own account,
    /// and a fixture reaching for it here would be reading an equity this store
    /// does not hold. Measured from `SeedValues` rather than borrowed.
    /// </para>
    /// <para>
    /// Only the per-name cap binds at this book: the total and assignment limits
    /// both sit at 60,000.00 and leave 39,000.00, so exactly one verdict differs
    /// between the two makers and it is the one under test.
    /// </para>
    /// </remarks>
    private static readonly BookState Committed = new(CommittedInName: 21_000m, CommittedTotal: 21_000m);

    [Fact]
    public void Two_makers_reference_one_set_rather_than_two_copies()
    {
        using var scenario = new DecisionScenario([GateScenario.Quote(50.00m)]);

        var baseline = scenario.Record("baseline", scenario.Gated());
        var random = scenario.Record("random", scenario.Gated());

        Assert.NotEqual(baseline, random);

        // One set and one candidate row, referenced twice.
        Assert.Equal(1, CountOf(scenario, "feasible_sets"));
        Assert.Equal(1, CountOf(scenario, "candidates"));
        Assert.Equal(2, CountOf(scenario, "decisions"));

        var first = scenario.Reader.Read(baseline);
        var second = scenario.Reader.Read(random);

        Assert.Equal(
            first.FeasibleSet.Select(candidate => candidate.CandidateId),
            second.FeasibleSet.Select(candidate => candidate.CandidateId));
    }

    /// <summary>
    /// The verdicts differ where the books do, which is the half that is not
    /// shared.
    /// </summary>
    [Fact]
    public void Their_portfolio_verdicts_differ_where_their_books_do()
    {
        using var scenario = new DecisionScenario([GateScenario.Quote(50.00m)]);

        var empty = scenario.Record("baseline", scenario.Gated());
        var committed = scenario.Record("random", scenario.Gated(Committed));

        var withEmptyBook = Assert.Single(scenario.Reader.Read(empty).FeasibleSet);
        var withCommitted = Assert.Single(scenario.Reader.Read(committed).FeasibleSet);

        // Same candidate row, different verdicts.
        Assert.Equal(withEmptyBook.CandidateId, withCommitted.CandidateId);

        Assert.True(withEmptyBook.IsFeasible);
        Assert.Contains(GateReason.PerNameCap, withCommitted.Reasons);

        // And the difference is entirely portfolio-level: nothing a contract
        // earns from its own quote moved.
        Assert.DoesNotContain(withEmptyBook.Reasons, GateReasonFamily.IsContractLevel);
        Assert.DoesNotContain(withCommitted.Reasons, GateReasonFamily.IsContractLevel);
    }

    /// <summary>
    /// A maker arriving with a different set is refused rather than merged.
    /// </summary>
    /// <remarks>
    /// The failure this prevents is not a crash. Merging would leave a record in
    /// which two makers appear to have seen one set while one of them saw
    /// another, and nothing written afterwards could say which, so the whole
    /// day's re-scoring would be quietly wrong for one arm.
    /// </remarks>
    [Fact]
    public void A_maker_offered_a_different_set_is_refused()
    {
        using var scenario = new DecisionScenario(
        [
            GateScenario.Quote(47.50m),
            GateScenario.Quote(50.00m),
        ]);

        var offered = scenario.Gated();
        scenario.Record("baseline", offered);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => scenario.Record("random", [offered[0]]));

        Assert.Contains("1 candidates", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("holding 2", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("D-W4", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The declared families agree with which evaluator raises each reason, read
    /// off their source.
    /// </summary>
    /// <remarks>
    /// <b>The split decides which table a verdict is written to, so a reason in
    /// the wrong family is a decision attributing one maker's book to every
    /// maker.</b> Declared rather than inferred from the member values, because
    /// the contract reasons are one to six today and a reason added in the middle
    /// would silently change families under a range test.
    /// </remarks>
    [Fact]
    public void The_declared_families_match_the_evaluators_that_raise_them()
    {
        var contract = ReasonsRaisedIn("ContractConstraints.cs");
        var portfolio = ReasonsRaisedIn("PortfolioConstraints.cs");

        // The vacuity guard: a scan finding nothing would agree with anything.
        Assert.NotEmpty(contract);
        Assert.NotEmpty(portfolio);

        Assert.Equal(GateReasonFamily.ContractLevel.Order(), contract.Order());
        Assert.Equal(GateReasonFamily.PortfolioLevel.Order(), portfolio.Order());

        // Every reason has a family, so a tenth added without one cannot be
        // written at all.
        Assert.All(
            Enum.GetValues<GateReason>(),
            reason => GateReasonFamily.IsContractLevel(reason));
    }

    private static int CountOf(DecisionScenario scenario, string table)
    {
        using var read = scenario.Connection.CreateCommand();

        // The table name is this fixture's own literal, never a caller's.
        read.CommandText = table switch
        {
            "feasible_sets" => "SELECT COUNT(*) FROM feasible_sets;",
            "candidates" => "SELECT COUNT(*) FROM candidates;",
            "decisions" => "SELECT COUNT(*) FROM decisions;",
            _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Not a counted table."),
        };

        return (int)(long)read.ExecuteScalar()!;
    }

    /// <summary>Every reason the file adds, read off its source.</summary>
    private static IReadOnlySet<GateReason> ReasonsRaisedIn(string fileName)
    {
        var path = Directory
            .EnumerateFiles(RepoRoot.SourcePath, fileName, SearchOption.AllDirectories)
            .Single();

        return Regex
            .Matches(File.ReadAllText(path), @"reasons\.Add\(GateReason\.([A-Za-z]+)\)")
            .Select(match => Enum.Parse<GateReason>(match.Groups[1].Value))
            .ToHashSet();
    }
}
