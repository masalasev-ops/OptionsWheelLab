using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Identity;

/// <summary>
/// What makes two option contracts the same contract: the tuple of underlying,
/// expiry, right and strike.
/// </summary>
/// <remarks>
/// The vendor's contract symbol is stored but is not the key, because symbol
/// conventions change on splits and special dividends and a stored key that
/// moves would silently break historical joins. It therefore lives on
/// <see cref="Contract"/> and not here: a record's synthesised equality covers
/// every declared member, so "carried alongside but not part of identity" is not
/// something this type could express about one of its own members.
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
    private ContractIdentity(Ticker underlying, DateOnly expiry, OptionRight right, decimal strike)
    {
        Underlying = underlying;
        Expiry = expiry;
        Right = right;
        Strike = strike;
    }

    public Ticker Underlying { get; }

    public DateOnly Expiry { get; }

    public OptionRight Right { get; }

    /// <summary>The strike, in the canonical stored form.</summary>
    public decimal Strike { get; }

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
        decimal strike)
    {
        ArgumentNullException.ThrowIfNull(underlying);

        if (right is not (OptionRight.Put or OptionRight.Call))
        {
            throw new ArgumentOutOfRangeException(
                nameof(right),
                right,
                "A contract is a put or a call. This is most likely an uninitialised value.");
        }

        return new ContractIdentity(underlying, expiry, right, StoreDecimal.Canonicalise(strike));
    }

    /// <summary>
    /// Total order over identity: underlying, then expiry, then right, then
    /// strike.
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

        return byRight != 0 ? byRight : Strike.CompareTo(other.Strike);
    }

    public override string ToString() =>
        $"{Underlying.Value} {StoreDate.ToStored(Expiry)} {StoreOptionRight.ToStored(Right)} "
        + StoreDecimal.ToStored(Strike);
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
/// <b><c>Multiplier</c> and <c>DeliverableShares</c> are two quantities, and this
/// record said they were one.</b> The multiplier is what a quoted premium
/// multiplies by to give the cash paid for one contract, and an adjustment does not
/// change it. The deliverable is what one contract conveys on exercise, and an
/// adjustment does: a three-for-two split takes a 90 strike to 60 and the
/// deliverable to 150. Both are one hundred for a standard contract, which is why
/// one column read as sufficient.
/// </para>
/// <para>
/// <b>Which of the two the outcome metric uses is open.</b> D-W17's first paragraph
/// says the contract multiplier and its third says the deliverable. That is a
/// carried obligation owed at Phase 3, which computes committed capital, and
/// nothing here presumes the answer.
/// </para>
/// <para>
/// Neither is part of identity, and after the §2 finding that is a statement about
/// what this record holds rather than an argument. The deliverable is what
/// distinguishes an adjusted series from a standard one at the same strike, so
/// identity not carrying it is precisely why the tuple maps two contracts to one
/// identity. §2 records that; it is not settled here.
/// </para>
/// </remarks>
public sealed record Contract(
    ContractIdentity Identity,
    string? VendorSymbol,
    int Multiplier,
    int DeliverableShares);
