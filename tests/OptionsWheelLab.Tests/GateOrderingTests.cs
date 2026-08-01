using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Synthetic;

namespace OptionsWheelLab.Tests;

/// <summary>
/// The gate returns one sequence, in contract identity order, and returns it
/// again for the same inputs.
/// </summary>
/// <remarks>
/// Not a registered fixture: 2.5's registered check is FX-GateRecordsAllReasons,
/// and this is that checkpoint's definition of done asserted rather than
/// restated, on <see cref="GateBoundsTests"/>' precedent.
/// <para>
/// <b>The property is a sequence, not a set, and the distinction is the whole
/// point.</b> Three decision-makers act on the same feasible set [D-W4], so what
/// they receive has to be the same thing in the same arrangement; a comparison
/// that sorted before comparing would pass against a generator whose order
/// varied between runs, which is exactly what the guarantee forbids.
/// </para>
/// <para>
/// <b>The byte-level form of that guarantee is not asserted here and cannot be.</b>
/// Nothing serialises a candidate at 2.5 and <c>candidates</c> is Phase 4's, so
/// there are no bytes to compare. `FX-ThreeMakersSameFeasibleSet` is registered
/// at Phase 4 and is where it lands. What is available now is the sequence, and
/// that is what these assert.
/// </para>
/// </remarks>
public sealed class GateOrderingTests
{
    /// <summary>
    /// Two expiries and four strikes, supplied in neither order.
    /// </summary>
    /// <remarks>
    /// Identity orders on expiry before strike, so a chain of one expiry cannot
    /// tell an identity comparison from a strike comparison. These are written
    /// deliberately scrambled: the later expiry first, and the strikes
    /// descending inside each, so the returned order cannot be the order they
    /// were handed over.
    /// </remarks>
    private static IReadOnlyList<ContractQuote> Scrambled() =>
    [
        Quote(50.00m, 60),
        Quote(45.00m, 60),
        Quote(52.50m, 46),
        Quote(45.00m, 46),
        Quote(50.00m, 46),
        Quote(47.50m, 60),
    ];

    [Fact]
    public void The_gated_sequence_is_in_identity_order()
    {
        var gated = GateScenario.EnumeratedAndGated(Scrambled()).Gated;

        var identities = gated
            .Select(candidate => candidate.Candidate.Quote.Contract)
            .ToList();

        Assert.Equal(6, identities.Count);

        // Sorted through the identity's own comparison rather than against a
        // hand-written list, so the assertion cannot agree with the code by
        // having been copied from it.
        Assert.Equal(identities.Order().ToList(), identities);

        // And the comparison is doing work: the input was not in this order.
        Assert.NotEqual(
            Scrambled().Select(quote => quote.Contract).ToList(), identities);
    }

    /// <summary>
    /// The expiry component is exercised, not just the strike.
    /// </summary>
    /// <remarks>
    /// Every expiry's contracts arrive together and the earlier expiry first,
    /// which a comparison on strike alone would interleave. The worked example
    /// is one expiry, so no fixture reading it could tell the two apart.
    /// </remarks>
    [Fact]
    public void The_earlier_expiry_arrives_whole_and_first()
    {
        var expiries = GateScenario.EnumeratedAndGated(Scrambled())
            .Gated
            .Select(candidate => candidate.Candidate.Quote.Contract.Expiry)
            .ToList();

        Assert.Equal(expiries.Order().ToList(), expiries);
        Assert.Equal(3, expiries.Count(expiry => expiry == GateScenario.Simulated.AddDays(46)));
        Assert.Equal(3, expiries.Count(expiry => expiry == GateScenario.Simulated.AddDays(60)));
    }

    /// <summary>
    /// The same inputs produce the same sequence, compared as identities and
    /// reasons in order, which is 2.5's definition of done [D-W4].
    /// </summary>
    /// <remarks>
    /// <see cref="GatedCandidate"/>, <see cref="EnumeratedCandidate"/>,
    /// <see cref="ContractQuote"/> and <see cref="ContractIdentity"/> are all
    /// records, so comparing the lists is deep value equality including order.
    /// The reasons travel inside that comparison rather than beside it, which is
    /// what makes this the whole verdict rather than the survivors.
    /// <para>
    /// <b>The projection is asserted beside the record comparison on purpose.</b>
    /// This test is what found that <see cref="GatedCandidate"/>'s synthesised
    /// equality compared its reasons by reference, so two identical verdicts were
    /// unequal; the projection says what the definition of done says, identities
    /// and reasons in order, and does not depend on the type getting its own
    /// equality right.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_same_inputs_gate_to_the_same_sequence()
    {
        var first = GateScenario.EnumeratedAndGated(Scrambled()).Gated;
        var second = GateScenario.EnumeratedAndGated(Scrambled()).Gated;

        Assert.NotEmpty(first);

        Assert.Equal(
            first.Select(Verdict).ToList(), second.Select(Verdict).ToList());

        Assert.Equal(first, second);
    }

    /// <summary>
    /// A verdict as the definition of done words it: an identity and its reasons
    /// in order.
    /// </summary>
    private static (ContractIdentity Contract, string Reasons) Verdict(
        GatedCandidate candidate) =>
        (candidate.Candidate.Quote.Contract,
            string.Join(",", candidate.Reasons.Select(reason => reason.ToString())));

    /// <summary>
    /// Two verdicts with the same contract and the same reasons are the same
    /// verdict, and two with the reasons in different orders are not.
    /// </summary>
    /// <remarks>
    /// Asserted directly as well as through the sequence comparison, because the
    /// synthesised equality compared the reasons by reference and the sequence
    /// comparison is an indirect way to find that out. A change reverting it
    /// should fail on the property rather than on a test about ordering.
    /// <para>
    /// The empty case is the one a reference comparison passes by accident when
    /// both lists happen to be the same shared instance, so it is asserted
    /// against two separately constructed empties.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_verdict_equals_an_identical_verdict()
    {
        var candidate = new EnumeratedCandidate(Quote(45.00m, 46));

        GateReason[] refused = [GateReason.SpreadCap, GateReason.PremiumFloor];

        Assert.Equal(
            new GatedCandidate(candidate, [.. refused]),
            new GatedCandidate(candidate, [.. refused]));

        Assert.Equal(
            new GatedCandidate(candidate, [.. refused]).GetHashCode(),
            new GatedCandidate(candidate, [.. refused]).GetHashCode());

        Assert.Equal(
            new GatedCandidate(candidate, []), new GatedCandidate(candidate, []));

        Assert.NotEqual(
            new GatedCandidate(candidate, [.. refused]),
            new GatedCandidate(candidate, [.. refused.Reverse()]));

        Assert.NotEqual(
            new GatedCandidate(candidate, [.. refused]),
            new GatedCandidate(candidate, [GateReason.SpreadCap]));
    }

    /// <summary>
    /// The order is inherited from the chain read, not imposed a second time.
    /// </summary>
    /// <remarks>
    /// <b>This is the assertion that keeps one sort the only statement of the
    /// guarantee.</b> The chain read has sorted on the identity total order
    /// since 1.2 and gained its fifth component at 1.5 without changing, so the
    /// gate has nothing to add: it filters nothing out and reorders nothing, and
    /// filtering preserves order. A gate that sorted its own output would pass
    /// every other assertion in this file while making the read's sort
    /// unnecessary, and the day the two disagreed there would be no way to tell
    /// which was authoritative.
    /// </remarks>
    [Fact]
    public void The_gate_returns_what_enumeration_returned_in_the_order_it_returned_it()
    {
        var (enumerated, gated) = GateScenario.EnumeratedAndGated(Scrambled());

        Assert.NotEmpty(enumerated);

        Assert.Equal(
            enumerated,
            gated.Select(candidate => candidate.Candidate).ToList());
    }

    /// <summary>
    /// Rejected candidates keep their place rather than being moved to one end.
    /// </summary>
    /// <remarks>
    /// The refusal record is the sequence with its verdicts attached [D-W5,
    /// D-W10], so a rejected candidate sits where its identity puts it. Grouping
    /// the survivors first would still be an order, and a stable one, which is
    /// why the property is asserted against identity rather than against
    /// determinism alone.
    /// </remarks>
    [Fact]
    public void A_refused_candidate_keeps_its_place_in_the_sequence()
    {
        // The middle strike breaches the premium floor and nothing else: bid
        // 0.20 is below the 0.30 floor, and 0.01 on a mid of 0.205 is under five
        // percent of mid, well inside the twelve percent cap. A wider quote
        // would carry the spread reason too and stop this being one refusal in
        // the middle of a sequence.
        var gated = GateScenario.EnumeratedAndGated(
        [
            Quote(45.00m, 46),
            Quote(50.00m, 46) with { Bid = 0.20m, Ask = 0.21m },
            Quote(52.50m, 46),
        ]).Gated;

        Assert.Equal(
            [45.00m, 50.00m, 52.50m],
            gated.Select(candidate => candidate.Candidate.Quote.Contract.Strike));

        Assert.Empty(gated[0].Reasons);
        Assert.Equal([GateReason.PremiumFloor], gated[1].Reasons);
        Assert.Empty(gated[2].Reasons);
    }

    /// <summary>
    /// One quote, everything not under test set to a value that passes.
    /// </summary>
    private static ContractQuote Quote(decimal strike, int daysToExpiry) =>
        GateScenario.Quote(strike, expiry: GateScenario.Simulated.AddDays(daysToExpiry));
}
