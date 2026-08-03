namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// What a `ledger_entries` row records [D-W48].
/// </summary>
/// <remarks>
/// <b>Events, not only cash.</b> An expiry that pays nothing is an entry with a
/// zero amount, because the projection rebuilt from the ledger has to know the
/// short closed and no other table says so. <c>WORKED_EXAMPLE.md</c> §6.3 already
/// reads this way, giving its worthless expiry a leg beside the five that move
/// money.
/// <para>
/// <b>Four pairs, each because one cash direction hides two events.</b> A short
/// leaves by expiring, by assignment or by being bought back, and only the last
/// is a premium. Shares leave at the strike when called away or at market when a
/// bound binds [D-W14], and the two prices are not the same fact. A buy-back
/// either rolls into a new leg or ends the trial, and after the fact a trial
/// closed at its last permitted roll and one closed by choice look identical, so
/// <see cref="PremiumPaid"/> and <see cref="BoughtToClose"/> are separate names
/// rather than one name and an inference. And the two premium kinds are named
/// rather than carried as a sign, because a roll is a debit and a credit on one
/// day.
/// </para>
/// <para>
/// <b><see cref="Commission"/> and <see cref="AssignmentFee"/> exist before
/// anything writes them.</b> Whether the fill model gives them entries of their
/// own or nets them into the premium is 3.4's, and [D-W12] requires them explicit
/// without saying where. A value nothing writes costs nothing; a migration adding
/// one costs a table rebuild.
/// </para>
/// <para>
/// <b>Deliberately not starting at zero</b>, on <see cref="PositionState"/>'s
/// precedent: <c>default</c> reading as a real kind would put a premium in the
/// ledger where an uninitialised value was meant.
/// </para>
/// <para>
/// The stored form is not this spelling. See
/// <see cref="Storage.StoreLedgerEntryKind"/>.
/// </para>
/// </remarks>
public enum LedgerEntryKind
{
    PremiumReceived = 1,
    PremiumPaid = 2,
    BoughtToClose = 3,
    ExpiredWorthless = 4,
    Assignment = 5,
    CallAway = 6,
    SharesSold = 7,
    Dividend = 8,
    Commission = 9,
    AssignmentFee = 10,
    Stopped = 11,
}

/// <summary>
/// How a trial returned to cash, being `trials.close_kind` [DATA_AND_SCHEMA §4.3].
/// </summary>
/// <remarks>
/// <b>Five values, and they are what ends a trial rather than what the schema
/// found convenient.</b> A trial runs from first open through to return to cash
/// [D-W14], so these are its exits: the short expired with no shares ever held
/// [D-W38]; shares were taken at the strike [D-W19]; the position closed at market
/// when a bound bound [D-W14]; a maker bought the short back to end the trial
/// rather than to roll it; or an action the lab does not model ended it [D-W47].
/// <para>
/// <b><see cref="ClosedAtBound"/> is one value, not two.</b> [D-W14] names one
/// mechanism with two triggers, <c>Trial:MaxRolls</c> and
/// <c>Trial:MaxTrialDays</c>, whichever binds first. Which of them fired is read
/// from <c>rolls_used</c> beside <c>opened_on</c> and <c>closed_on</c>, so a
/// second value would state one fact twice.
/// </para>
/// <para>
/// <b>Nothing writes <see cref="ClosedByChoice"/> before Phase 4 has a maker</b>,
/// and it is recoverable from the day one does, being a
/// <see cref="LedgerEntryKind.BoughtToClose"/> with no
/// <see cref="LedgerEntryKind.PremiumReceived"/> following. Every value here has
/// to be recoverable from the ledger, because <c>trials</c> is a projection
/// [D-W35] and a value the rebuild cannot reconstruct fails the test that permits
/// rewriting it at all.
/// </para>
/// <para>
/// An open trial has no close kind, and the column is nullable for that reason: a
/// trial that has not closed has not closed in any particular way, and forcing a
/// value would make the state machine invent one at its first write.
/// </para>
/// </remarks>
public enum TrialCloseKind
{
    ExpiredWorthless = 1,
    CalledAway = 2,
    ClosedAtBound = 3,
    ClosedByChoice = 4,
    Stopped = 5,
}
