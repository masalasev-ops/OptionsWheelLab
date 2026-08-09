using OptionsWheelLab.Core.Identity;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// The three caps of [D-W11] and the gross-basis rule of [D-W19], asking whether
/// the book can carry this position [SYSTEM_DESIGN §3.4].
/// </summary>
/// <remarks>
/// <b>A different question from <see cref="ContractConstraints"/>.</b> That
/// family asks whether a contract belongs in the opportunity set at all and
/// reads only the quote; this one asks what the account already carries, which
/// is why it needs <see cref="BookState"/> and why the two are separate types
/// with separate bound records.
/// <para>
/// <b>Pure, and handed its values.</b> Nothing here reads configuration, a clock
/// or a table: <see cref="PortfolioBounds"/> resolves once per evaluation
/// [D-W37] and the book arrives as a parameter, so this is a function of its
/// arguments and can be tested without a store. The same property
/// <see cref="ContractConstraints"/> has, for the second of two reasons.
/// </para>
/// <para>
/// <b>Every constraint is evaluated and every failing reason recorded, never the
/// first</b> [D-W22, CLAUDE.md §2]. Reasons come back in
/// <see cref="GateReason"/>'s declared order, which is what three makers
/// receiving byte-identical sets rests on [D-W4].
/// </para>
/// <para>
/// <b>The two capital caps cannot be told apart by a rejection today, and that
/// is recorded rather than hidden.</b> Both fractions are 0.60
/// [CONFIG_REFERENCE], and assignment exposure never exceeds committed capital
/// on any book this lab can hold, so a candidate breaching one breaches the
/// other. The headroom functions below are what distinguishes them, which is why
/// they are public: a cap whose bound is never reached passes whether or not it
/// is wired to the right figure.
/// </para>
/// </remarks>
public static class PortfolioConstraints
{
    /// <summary>
    /// Every reason <paramref name="candidate"/> fails, in
    /// <see cref="GateReason"/>'s declared order, empty when the book can carry
    /// it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When a call candidate arrives with no gross basis to bind against.
    /// </exception>
    public static IReadOnlyList<GateReason> Evaluate(
        EnumeratedCandidate candidate,
        PortfolioBounds bounds,
        BookState book)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(book);

        var reasons = new List<GateReason>();
        var committing = CommittedCapital.For(candidate);

        // Each cap compares the same figure against its own headroom, so a cap
        // reading the wrong exposure is a change to one of these three lines
        // rather than to arithmetic repeated three times.
        if (committing > PerNameHeadroom(bounds, book))
        {
            reasons.Add(GateReason.PerNameCap);
        }

        if (committing > TotalHeadroom(bounds, book))
        {
            reasons.Add(GateReason.TotalCap);
        }

        if (committing > AssignmentHeadroom(bounds, book))
        {
            reasons.Add(GateReason.AssignmentStress);
        }

        if (BreachesGrossBasis(candidate, book))
        {
            reasons.Add(GateReason.GrossBasis);
        }

        return reasons;
    }

    /// <summary>
    /// What this name can still commit before the per-name cap binds [D-W11].
    /// </summary>
    /// <remarks>
    /// Public because a cap whose bound is never reached is not exercised by a
    /// verdict. WORKED_EXAMPLE §1 derives 5,100.00 here and §3 rests on it, and
    /// §1 derives a total headroom of 22,000.00 that no candidate on that chain
    /// approaches; asserting the figures is what tells a working cap from one
    /// reading the wrong exposure or nothing at all.
    /// </remarks>
    public static decimal PerNameHeadroom(PortfolioBounds bounds, BookState book)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(book);

        return (bounds.Equity * bounds.PerNameCapFraction) - book.CommittedInName;
    }

    /// <summary>
    /// What the account can still commit before the total cap binds [D-W11].
    /// </summary>
    public static decimal TotalHeadroom(PortfolioBounds bounds, BookState book)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(book);

        return (bounds.Equity * bounds.TotalCapFraction) - book.CommittedTotal;
    }

    /// <summary>
    /// What the account can still commit before the simultaneous-assignment
    /// limit binds [D-W11].
    /// </summary>
    /// <remarks>
    /// <b>The exposure it subtracts is <see cref="BookState.CommittedTotal"/>,
    /// and this is the site that states why.</b> A cash-secured put's committed
    /// capital is what would be owed if it assigned, so the two aggregates are
    /// one figure while every open position is a cash-secured put, which is
    /// every position the lab can hold before Phase 3's state machine. A covered
    /// call commits shares rather than cash and holds no assignment exposure of
    /// its own, so a book holding shares separates them and this line is what
    /// Phase 3 changes.
    /// <para>
    /// The stress figure asks what would be owed and what would be held if every
    /// open short put assigned at once [SYSTEM_DESIGN §3.4], which is the wheel's
    /// real cash-loss event: not one bad position, but every short put assigning
    /// together in a correlated selloff while the account lacks the cash to fund
    /// them.
    /// </para>
    /// </remarks>
    public static decimal AssignmentHeadroom(PortfolioBounds bounds, BookState book)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(book);

        return (bounds.Equity * bounds.SimultaneousAssignmentLimitFraction)
            - book.CommittedTotal;
    }

    /// <summary>
    /// Whether a covered call would cap recovery below the cash outlay [D-W19].
    /// </summary>
    /// <remarks>
    /// <b>Gross basis, never net.</b> Netting premium into basis permits call
    /// strikes below the cash outlay and lets accumulated premium subsidise
    /// progressively worse strike selection, and the total stays positive while
    /// the banked premium covers the gap, which is why the drift has to be
    /// prevented structurally rather than detected in the profit and loss.
    /// <para>
    /// <b>A strike exactly at basis is admitted</b> [D-W19, as amended]. The
    /// constraint exists to prevent a call capping recovery below the outlay and
    /// a strike at basis recovers it exactly, so excluding it would forbid the
    /// break-even strike for no stated reason.
    /// </para>
    /// <para>
    /// <b>A call with no basis stops rather than resolving either way.</b>
    /// <see cref="Positions.PositionState.HoldingShares"/> and
    /// <see cref="Positions.PositionState.ShortCall"/> both enumerate calls from
    /// 4.4 [D-W54], and both carry a gross basis, because a short call is
    /// reachable only from held shares and the basis is carried forward with it.
    /// So a call reaching here with no basis is a caller that has lost the
    /// position it is gating for. This once said only holding shares enumerates
    /// calls, which 4.4 made false; the conclusion survives and the reason given
    /// for it did not. Admitting would drop D-W19 silently and rejecting would
    /// blame the strike, which is D-W37's argument arriving through book state
    /// rather than through configuration.
    /// </para>
    /// </remarks>
    private static bool BreachesGrossBasis(EnumeratedCandidate candidate, BookState book)
    {
        if (candidate.Quote.Contract.Right != OptionRight.Call)
        {
            return false;
        }

        if (book.GrossBasis is not { } basis)
        {
            throw new InvalidOperationException(
                $"'{candidate.Quote.Contract}' is a call and the book carries no gross basis "
                + "to bind it against. A call is enumerated only against held shares, so this "
                + "is a book that has lost its position rather than a candidate to judge, and "
                + "the gate does not guess a basis [D-W19].");
        }

        return candidate.Quote.Contract.Strike < basis;
    }
}
