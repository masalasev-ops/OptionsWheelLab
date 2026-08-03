using OptionsWheelLab.Core.Positions;
using static OptionsWheelLab.Tests.TrialScenario;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-DividendReachesLedger: a dividend whose ex-date falls while a trial holds
/// assigned shares produces a ledger entry, and one whose ex-date falls after
/// the shares were called away does not [D-W41].
/// </summary>
/// <remarks>
/// Whether a dividend enters the record at all is this corpus's question rather
/// than a market's. It does, in both places: a dividend paid between assignment
/// and call-away is cash the trial received, and recording it against the trial
/// while leaving the control's return untouched would bias the exact comparison
/// the lab exists to make, in one direction. The control's half is asserted where
/// the control is built, which is not Phase 3.
/// <para>
/// <b>Entitlement is read off the state entering the session</b>, which is what
/// "holding the shares before the ex-dividend date" means: the state at the start
/// of the ex-date session is what the prior session's close left.
/// </para>
/// </remarks>
public sealed class FX_DividendReachesLedger
{
    private const decimal PerShare = 0.44m;

    [Fact]
    public void A_dividend_while_the_trial_holds_shares_is_an_entry()
    {
        var machine = Machine();
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;

        var paid = machine.Advance(
            holding,
            Session(SecondExpiry, close: 51.20m, actions: [Ordinary(SecondExpiry, PerShare)]));

        var entry = Assert.Single(paid.Entries);

        Assert.Equal(LedgerEntryKind.Dividend, entry.Kind);
        Assert.Equal(44.00m, entry.Amount);
        Assert.Equal(SecondExpiry, entry.EntryDate);
        Assert.Equal(SecondMonday, entry.KnownOn);
    }

    /// <summary>
    /// A dividend after the shares were called away is not the trial's.
    /// </summary>
    /// <remarks>
    /// The half that fails on a check reading the corporate action rather than
    /// the position. A trial that has returned to cash holds nothing to be
    /// entitled by, and a machine ledgering every dividend it sees would pass the
    /// case above and fail here.
    /// </remarks>
    [Fact]
    public void A_dividend_after_the_shares_were_called_away_is_not_an_entry()
    {
        var machine = Machine();
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;
        var written = machine.WriteCall(
            holding, MondayAfter, Call(52.50m, SecondExpiry), Sold(0.70m)).State;

        var calledAway = machine.Advance(written, Session(SecondExpiry, close: 53.00m)).State;

        Assert.Equal(TrialCloseKind.CalledAway, calledAway.CloseKind);

        var after = machine.Advance(
            calledAway,
            Session(ThirdExpiry, close: 53.40m, actions: [Ordinary(ThirdExpiry, PerShare)]));

        Assert.Empty(after.Entries);
    }

    /// <summary>
    /// A dividend before the shares arrive is not the trial's either.
    /// </summary>
    /// <remarks>
    /// The other end of the same window, and the one that would pass on a state
    /// machine reading the trial rather than its shares: the trial is open on
    /// this session and holds a short put, which no dividend pays.
    /// </remarks>
    [Fact]
    public void A_dividend_before_the_shares_were_assigned_is_not_an_entry()
    {
        var paid = Machine().Advance(
            OpenedTrial(),
            Session(
                new(2026, 4, 8),
                close: 45.80m,
                actions: [Ordinary(new(2026, 4, 8), PerShare)]));

        Assert.Empty(paid.Entries);
    }

    /// <summary>
    /// A hand-written scenario can state the dividend, which is the third of the
    /// three things [D-W41] named and could not add.
    /// </summary>
    /// <remarks>
    /// The other two were a <c>kind</c> the ledger admits and a
    /// <c>CorporateActionKind</c> beyond <c>Split</c>. Without this one the
    /// assertions above could only be built from objects constructed in a test,
    /// and no scenario file could express the case at all.
    /// </remarks>
    [Fact]
    public void A_scenario_can_state_the_dividend()
    {
        var chain = Core.Synthetic.SyntheticChainReader.Read(
            """
            {
              "symbol": "WDGT",
              "bars": [ { "date": "2026-05-15", "close": "51.20000000" } ],
              "chains": [],
              "actions": [
                {
                  "exDate": "2026-05-15",
                  "kind": "ordinary_dividend",
                  "amount": "0.44000000"
                }
              ]
            }
            """);

        var stated = Assert.Single(chain.Actions);

        Assert.Equal(Core.MarketData.CorporateActionKind.OrdinaryDividend, stated.Action.Kind);
        Assert.Equal(PerShare, stated.Action.Amount);
        Assert.Null(stated.StatedSuccessor);

        var machine = Machine();
        var holding = machine.Advance(OpenedTrial(), Session(FirstExpiry, close: 48.90m)).State;

        var paid = machine.Advance(
            holding, Session(SecondExpiry, close: 51.20m, actions: [stated]));

        Assert.Equal(44.00m, Assert.Single(paid.Entries).Amount);
    }
}
