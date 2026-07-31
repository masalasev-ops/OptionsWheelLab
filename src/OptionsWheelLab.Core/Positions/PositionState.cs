namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// What a trial holds: the discriminated union tag of <c>positions.state</c>
/// [DATA_AND_SCHEMA §4.3].
/// </summary>
/// <remarks>
/// <b>The concept, not the row.</b> <c>positions</c> is §4.3 and unbuilt, and
/// 2.2 needs only what a state makes sellable. The table arrives with the wheel
/// state machine; nothing here presumes its shape beyond the four tags it
/// names.
/// <para>
/// <b>Deliberately not starting at zero</b>, on <see cref="Identity.OptionRight"/>'s
/// precedent and for a sharper reason. <c>default(PositionState)</c> reading as
/// <see cref="Cash"/> would enumerate puts against an account that holds shares,
/// which is a wrong answer made of plausible parts: the set would be non-empty,
/// ordered, and every candidate in it a real contract.
/// </para>
/// <para>
/// The stored form is not this spelling. See
/// <see cref="Storage.StorePositionState"/>.
/// </para>
/// </remarks>
public enum PositionState
{
    Cash = 1,
    ShortPut = 2,
    HoldingShares = 3,
    ShortCall = 4,
}
