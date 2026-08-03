namespace OptionsWheelLab.Core.MarketData;

/// <summary>The kind of corporate action a `corporate_actions` row records.</summary>
/// <remarks>
/// <b>OCC's own enumeration, complete before the transitions that read it
/// exist</b> [D-W47]. The adjustment provisions reach the "declaration of
/// dividends or distributions, stock splits, rights offerings, reorganizations,
/// or the merger or liquidation of an issuer", and this carries all of them, plus
/// the ordinary and non-ordinary dividend that [D-W44] separates and the spin-off
/// that a distribution can be. <c>Split</c> alone until 3.3, which is what the
/// obligation raised at 1.5 was about: a rebuild migration should not have ridden
/// 1.5 silently.
/// <para>
/// <b>What the lab models is a different question and this enum does not answer
/// it.</b> An action here that no transition handles stops the trial and is
/// recorded as its reason [D-W47], so a name present with nothing reading it is
/// the designed state rather than an oversight. Deferring the transitions was
/// permitted; deferring the vocabulary was not.
/// </para>
/// <para>
/// <b>A reverse split is <see cref="Split"/> with a ratio below one.</b> The
/// ratio is a recorded fact about the event [D-W36], so a second name would be a
/// second place to get one event wrong.
/// </para>
/// <para>
/// Which side of the dividend line an event falls on is OCC's determination per
/// event and is transcribed, never derived [D-W36, D-W44]. Nothing here computes
/// it from an amount, and the $12.50 figure D-W44 repeats is a general rule
/// rather than a bound to compute with.
/// </para>
/// <para>
/// The stored form is not this spelling. See
/// <see cref="Storage.StoreCorporateActionKind"/>.
/// </para>
/// </remarks>
public enum CorporateActionKind
{
    OrdinaryDividend = 1,
    NonOrdinaryDividend = 2,
    Split = 3,
    RightsOffering = 4,
    Reorganization = 5,
    Merger = 6,
    Liquidation = 7,
    SpinOff = 8,
}
