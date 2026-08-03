using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;

namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// What one session shows a trial: the close, the actions dated around it, and
/// what the short contract was bid.
/// </summary>
/// <remarks>
/// <b>Every action known for the underlying, not the ones dated today.</b> The
/// early-assignment rule reads an ex-date that has not arrived yet [D-W42], so a
/// shape carrying only today's would make the one transition that looks forward
/// impossible to express.
/// <para>
/// <b>The bid, because it is the only price this lab reads.</b> [D-W12] fixes the
/// fill at the bid on the ground that end-of-day granularity never shows the
/// realised price, and the same argument reaches any price read off a daily
/// quote. For the early-assignment test it is also the conservative direction: a
/// lower price is a lower time value, so more exercises are modelled, which is
/// the outcome adverse to the lab.
/// </para>
/// </remarks>
public sealed record SessionFacts(
    DateOnly Session,
    decimal UnderlyingClose,
    IReadOnlyList<ActionOnUnderlying> Actions,
    decimal? ShortContractBid = null);

/// <summary>
/// The two bounds a rolled chain terminates at [D-W14], resolved once.
/// </summary>
/// <remarks>
/// <see cref="Generation.GateBounds"/>'s shape and its argument. Both are read as
/// of the simulated date rather than as-now [D-W26], and an unresolvable bound
/// stops the evaluation rather than admitting or refusing [D-W37]: a trial run
/// with no roll bound would roll for ever and look like a long-lived position
/// rather than a misconfiguration.
/// <para>
/// This is what makes `Trial:MaxRolls` and `Trial:MaxTrialDays` verified
/// consumers rather than specified ones [`CONFIG_REFERENCE.md`]. Both carried
/// <b>Unverified</b> against "State machine" from 0.8 until here.
/// </para>
/// </remarks>
public sealed record TrialBounds(int MaxRolls, int MaxTrialDays)
{
    /// <summary>The bounds in force on <paramref name="simulatedDate"/>.</summary>
    /// <exception cref="InvalidOperationException">
    /// When either bound has no value in force on that date.
    /// </exception>
    public static TrialBounds ResolveFor(AsOfConfiguration configuration, DateOnly simulatedDate)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new TrialBounds(
            Generation.ResolvedBound.RequiredInt(
                configuration, ConfigKeys.TrialMaxRolls, simulatedDate),
            Generation.ResolvedBound.RequiredInt(
                configuration, ConfigKeys.TrialMaxTrialDays, simulatedDate));
    }
}

/// <summary>
/// One entry the state machine produced, before anything writes it.
/// </summary>
/// <remarks>
/// <b>Two dates, because assignment is known after it happens</b> [D-W39].
/// <see cref="EntryDate"/> is the session the event occurred in and
/// <see cref="KnownOn"/> the session the account could act on it, and both are
/// carried because a projection rebuilt from the ledger has to reproduce what was
/// known when [D-W35].
/// <para>
/// The amount is signed from the account's side: a credit is positive and a debit
/// negative, which is what makes a trial's total the sum of its entries and is
/// how <c>WORKED_EXAMPLE.md</c> §6.3 adds to 498.05. The sign is not what
/// distinguishes the kinds; four pairs exist precisely because one cash direction
/// covers two events [D-W48].
/// </para>
/// </remarks>
public sealed record LedgerEntry(
    DateOnly EntryDate,
    DateOnly KnownOn,
    LedgerEntryKind Kind,
    decimal Amount,
    ContractIdentity? Contract = null,
    string? Note = null);

/// <summary>What one session did to a trial.</summary>
public sealed record Transition(TrialState State, IReadOnlyList<LedgerEntry> Entries)
{
    /// <summary>A session that changed nothing.</summary>
    internal static Transition Unchanged(TrialState state) => new(state, []);
}
