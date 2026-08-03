using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Identity;

/// <summary>
/// What makes two option contracts the same contract: the tuple of underlying,
/// expiry, right and strike, together with the deliverable.
/// </summary>
/// <remarks>
/// <b>Five components, not four, per §2 as corrected at 1.5.</b> An adjusted
/// series can share underlying, expiry, right and strike with a standard
/// contract, differing only in what it delivers: a three-for-two split's
/// successor at a 60 strike with 150 shares lists alongside a standard 60 with
/// 100. The store's uniqueness constraint has carried all five since 1.1; this
/// type carries them from 1.5, and the adjusted terms it carries are transcribed
/// from what the adjusting authority states, never derived [D-W36].
/// <para>
/// The vendor's contract symbol is stored but is not the key, because symbol
/// conventions change on splits and special dividends and a stored key that
/// moves would silently break historical joins. It therefore lives on
/// <see cref="Contract"/> and not here: a record's synthesised equality covers
/// every declared member, so "carried alongside but not part of identity" is not
/// something this type could express about one of its own members.
/// </para>
/// <para>
/// <b>A reference type with a private constructor, not a record struct.</b>
/// A struct admits <c>default</c>, which would produce an identity with a null
/// ticker and an uncanonicalised strike without going through the factory. The
/// members are get-only rather than <c>init</c> for the same reason: <c>with</c>
/// reaches the compiler-generated copy constructor and would bypass the factory
/// exactly as <c>default</c> would.
/// </para>
/// <para>
/// <b>Not stable across a corporate action, deliberately.</b> An adjusted
/// contract is a NEW identity with a recorded predecessor link, so a join by
/// identity across a split returns nothing rather than the wrong thing. The link
/// lives on the row, not here. Phase 1 owns the adjustment; this defines only
/// the identity.
/// </para>
/// </remarks>
public sealed record ContractIdentity : IComparable<ContractIdentity>
{
    private ContractIdentity(
        Ticker underlying,
        DateOnly expiry,
        OptionRight right,
        decimal strike,
        int deliverableShares)
    {
        Underlying = underlying;
        Expiry = expiry;
        Right = right;
        Strike = strike;
        DeliverableShares = deliverableShares;
    }

    public Ticker Underlying { get; }

    public DateOnly Expiry { get; }

    public OptionRight Right { get; }

    /// <summary>The strike, in the canonical stored form.</summary>
    public decimal Strike { get; }

    /// <summary>
    /// What one contract conveys on exercise, a stated term [D-W36]. One
    /// hundred for a standard contract, and the component that separates an
    /// adjusted series from a standard one at the same strike [§2].
    /// </summary>
    public int DeliverableShares { get; }

    /// <summary>
    /// The identity of a contract, with the strike canonicalised.
    /// </summary>
    /// <remarks>
    /// <b>Canonicalising buys determinism and validation, not equality.</b>
    /// <c>decimal</c> equality and hashing already ignore scale, so
    /// <c>50m</c> and <c>50.00m</c> were always the same identity. What it buys
    /// is that anything stringifying a strike without a format produces one
    /// answer rather than two, which is what a byte-identical run needs, and
    /// that a strike carrying more places than the store can hold is refused
    /// here rather than at the moment of writing.
    /// <para>
    /// It goes through the refusing path, never the rounding one. A strike is
    /// exact.
    /// </para>
    /// </remarks>
    public static ContractIdentity Of(
        Ticker underlying,
        DateOnly expiry,
        OptionRight right,
        decimal strike,
        int deliverableShares = 100)
    {
        ArgumentNullException.ThrowIfNull(underlying);

        if (right is not (OptionRight.Put or OptionRight.Call))
        {
            throw new ArgumentOutOfRangeException(
                nameof(right),
                right,
                "A contract is a put or a call. This is most likely an uninitialised value.");
        }

        if (deliverableShares <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deliverableShares),
                deliverableShares,
                "A contract conveys a positive number of shares. The default is the "
                + "standard 100; an adjusted deliverable is transcribed from what the "
                + "adjusting authority states [D-W36].");
        }

        return new ContractIdentity(
            underlying, expiry, right, StoreDecimal.Canonicalise(strike), deliverableShares);
    }

    /// <summary>
    /// Total order over identity: underlying, then expiry, then right, then
    /// strike, then deliverable.
    /// </summary>
    /// <remarks>
    /// <b>A requirement rather than a convenience.</b> Three decision-makers
    /// receive byte-identical candidate sets [D-W4], and a simulated run produces
    /// byte-identical output across two invocations. Neither is achievable if the
    /// order candidates arrive in depends on anything but the candidates.
    /// <para>
    /// <see cref="StringComparer.Ordinal"/> is named rather than defaulted.
    /// <c>InvariantGlobalization</c> makes a culture-sensitive comparison look
    /// correct here and be wrong nowhere this suite could catch, which is the
    /// worst combination available.
    /// </para>
    /// </remarks>
    public int CompareTo(ContractIdentity? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byUnderlying = StringComparer.Ordinal.Compare(Underlying.Value, other.Underlying.Value);

        if (byUnderlying != 0)
        {
            return byUnderlying;
        }

        var byExpiry = Expiry.CompareTo(other.Expiry);

        if (byExpiry != 0)
        {
            return byExpiry;
        }

        var byRight = Right.CompareTo(other.Right);

        if (byRight != 0)
        {
            return byRight;
        }

        var byStrike = Strike.CompareTo(other.Strike);

        return byStrike != 0 ? byStrike : DeliverableShares.CompareTo(other.DeliverableShares);
    }

    public override string ToString() =>
        $"{Underlying.Value} {StoreDate.ToStored(Expiry)} {StoreOptionRight.ToStored(Right)} "
        + $"{StoreDecimal.ToStored(Strike)} x{DeliverableShares}";
}

/// <summary>
/// A contract as the vendor describes it: its identity, plus the facts that hang
/// off the identity without being part of it.
/// </summary>
/// <remarks>
/// <c>VendorSymbol</c> is here rather than on <see cref="ContractIdentity"/>
/// because record equality covers every declared member, so putting it there
/// would make it part of the key, which is the one thing the schema forbids.
/// <para>
/// <b>The deliverable is not here, because identity carries it</b> [§2, as
/// corrected at 1.5]. It was, until the five-component identity landed; keeping
/// a second copy beside the identity's would be a fact in two places. The
/// multiplier stays: what a quoted premium multiplies by to give the cash paid
/// for one contract, which an adjustment does not change, where the deliverable
/// is what one contract conveys on exercise, which an adjustment does. Both are
/// stated terms, transcribed and never derived [D-W36].
/// </para>
/// <para>
/// <b>Which of the two the outcome metric uses was settled at 3.1: the
/// multiplier</b> [D-W17, as amended]. An adjustment moves the deliverable and
/// leaves the strike and the aggregate exercise price alone, so committed capital
/// is strike times multiplier and a metric reading the deliverable would misprice
/// every adjusted position. The deliverable keeps its other job, separating an
/// adjusted series from a standard one at the same strike, which is why it is a
/// component of identity and the multiplier is not.
/// </para>
/// </remarks>
public sealed record Contract(
    ContractIdentity Identity,
    string? VendorSymbol,
    int Multiplier);
