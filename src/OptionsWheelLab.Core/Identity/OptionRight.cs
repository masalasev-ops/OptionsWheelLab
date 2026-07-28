namespace OptionsWheelLab.Core.Identity;

/// <summary>
/// Which side of the contract, being one of the four parts of a contract's
/// identity.
/// </summary>
/// <remarks>
/// <b>Deliberately not starting at zero.</b> <c>default(OptionRight)</c> is
/// therefore not a valid value and can be detected, rather than silently reading
/// as <see cref="Put"/>. An uninitialised field standing for the wrong side of a
/// trade is the kind of defect that produces plausible numbers.
/// <para>
/// The stored form is not this spelling. See
/// <see cref="Storage.StoreOptionRight"/>.
/// </para>
/// </remarks>
public enum OptionRight
{
    Put = 1,
    Call = 2,
}
