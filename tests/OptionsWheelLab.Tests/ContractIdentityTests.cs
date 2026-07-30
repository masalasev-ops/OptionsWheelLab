using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// Contract identity: the tuple, and the order over it.
/// </summary>
/// <remarks>
/// Not a registered fixture, so not named <c>FX-*</c>.
/// </remarks>
public sealed class ContractIdentityTests
{
    private static readonly DateOnly Expiry = new(2026, 9, 18);

    /// <summary>
    /// The property C3 exists for. Two spellings of one strike are one contract.
    /// </summary>
    /// <remarks>
    /// This also pins the CLR behaviour the record inherits rather than trusting
    /// it: a synthesised <c>GetHashCode</c> defers to
    /// <c>EqualityComparer&lt;decimal&gt;.Default</c>, and the whole identity
    /// rests on that ignoring scale.
    /// </remarks>
    [Fact]
    public void Two_contracts_differing_only_in_how_the_strike_was_written_are_equal()
    {
        var written = Identity(50m);
        var writtenDifferently = Identity(50.00m);

        Assert.Equal(written, writtenDifferently);
        Assert.Equal(written.GetHashCode(), writtenDifferently.GetHashCode());
        Assert.Equal(StoreDecimal.ToStored(written.Strike), StoreDecimal.ToStored(writtenDifferently.Strike));
    }

    /// <summary>
    /// What canonicalising actually buys, which is not equality.
    /// </summary>
    /// <remarks>
    /// Equality and hashing on <c>decimal</c> already ignore scale, so the two
    /// identities above would match without any canonicalisation at all. What
    /// would differ is the rendering: <c>50m.ToString()</c> and
    /// <c>50.00m.ToString()</c> are different strings, and a byte-identical run
    /// cannot survive a value that stringifies two ways.
    /// </remarks>
    [Fact]
    public void A_strike_renders_one_way_however_it_was_written()
    {
        Assert.Equal(Identity(50m).ToString(), Identity(50.00m).ToString());
        Assert.Equal(Identity(50m).Strike.ToString(), Identity(50.000m).Strike.ToString());
    }

    [Fact]
    public void Any_part_of_the_tuple_differing_makes_a_different_contract()
    {
        var baseline = Identity(50m);

        Assert.NotEqual(baseline, ContractIdentity.Of(Ticker.Normalise("MSFT"), Expiry, OptionRight.Put, 50m));
        Assert.NotEqual(baseline, ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry.AddDays(7), OptionRight.Put, 50m));
        Assert.NotEqual(baseline, ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry, OptionRight.Call, 50m));
        Assert.NotEqual(baseline, Identity(52.50m));
    }

    /// <summary>
    /// The vendor symbol is not part of the key, because symbol conventions move
    /// on splits and a stored key that moves breaks historical joins silently.
    /// </summary>
    [Fact]
    public void The_vendor_symbol_is_carried_beside_the_identity_and_not_inside_it()
    {
        var identity = Identity(50m);

        var one = new Contract(identity, "AAPL260918P00050000", 100, 100);
        var renamed = new Contract(identity, "AAPL260918P00050000-ADJ", 100, 100);

        Assert.Equal(one.Identity, renamed.Identity);
        Assert.NotEqual(one, renamed);
    }

    /// <summary>
    /// The multiplier and the deliverable are two quantities, and identity carries
    /// neither.
    /// </summary>
    /// <remarks>
    /// This is the §2 finding in test form. A three-for-two split takes a 90 strike
    /// to 60 with a 150-share deliverable, and a standard 60 strike with 100 shares
    /// lists alongside it. The two contracts are economically different and share
    /// one identity, which is why the tuple is not identity. Recorded here so the
    /// claim is visible in the suite rather than only in a document banner.
    /// <para>
    /// It asserts the current behaviour, not the desired behaviour. When the
    /// identity decision lands, this test is the one that has to change, and that
    /// is the point of it.
    /// </para>
    /// </remarks>
    [Fact]
    public void An_adjusted_contract_and_a_standard_one_can_share_an_identity()
    {
        var identity = Identity(60m);

        var adjusted = new Contract(identity, "WDGT1260918P00060000", 100, 150);
        var standard = new Contract(identity, "WDGT260918P00060000", 100, 100);

        Assert.Equal(adjusted.Identity, standard.Identity);
        Assert.Equal(adjusted.Multiplier, standard.Multiplier);
        Assert.NotEqual(adjusted.DeliverableShares, standard.DeliverableShares);
    }

    /// <summary>
    /// A strike the store cannot hold exactly is refused at construction, not at
    /// the moment of writing.
    /// </summary>
    [Fact]
    public void A_strike_beyond_the_stored_scale_is_refused_at_construction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Identity(50.000000001m));
    }

    /// <summary>
    /// <c>default(OptionRight)</c> is not a right, and the factory says so
    /// rather than accepting it as a put.
    /// </summary>
    [Fact]
    public void An_uninitialised_right_is_refused()
    {
        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry, default, 50m));

        Assert.Contains("uninitialised", thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A shuffled list sorts to one order, and sorting twice is stable.
    /// </summary>
    /// <remarks>
    /// Byte-identical candidate sets across three makers [D-W4] need this, so it
    /// is a requirement rather than a convenience.
    /// </remarks>
    [Fact]
    public void A_shuffled_list_sorts_to_one_order_and_sorting_is_stable()
    {
        var contracts = new[]
        {
            ContractIdentity.Of(Ticker.Normalise("MSFT"), Expiry, OptionRight.Put, 50m),
            ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry, OptionRight.Call, 45m),
            ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry, OptionRight.Put, 47.50m),
            ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry.AddDays(-7), OptionRight.Put, 50m),
            ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry, OptionRight.Put, 45m),
        };

        var sorted = contracts.Order().ToList();
        var sortedAgain = sorted.Order().ToList();

        Assert.Equal(
            [
                ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry.AddDays(-7), OptionRight.Put, 50m),
                ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry, OptionRight.Put, 45m),
                ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry, OptionRight.Put, 47.50m),
                ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry, OptionRight.Call, 45m),
                ContractIdentity.Of(Ticker.Normalise("MSFT"), Expiry, OptionRight.Put, 50m),
            ],
            sorted);

        Assert.Equal(sorted, sortedAgain);
    }

    /// <summary>
    /// Two shuffles of one set sort to the same order, which is the property a
    /// byte-identical run actually depends on.
    /// </summary>
    [Fact]
    public void Two_different_input_orders_sort_to_the_same_order()
    {
        var one = new[] { Identity(50m), Identity(45m), Identity(47.50m) }.Order();
        var other = new[] { Identity(47.50m), Identity(50m), Identity(45m) }.Order();

        Assert.Equal(one, other);
    }

    /// <summary>
    /// The strike orders numerically, not as its stored text. Lexicographically
    /// "45.00000000" sorts above "9.00000000", which is the property D-W29 says
    /// the stored form lacks.
    /// </summary>
    [Fact]
    public void The_strike_orders_numerically_rather_than_as_stored_text()
    {
        var cheap = Identity(9m);
        var dear = Identity(45m);

        Assert.True(cheap.CompareTo(dear) < 0);
        Assert.True(
            StoreDecimal.ToStored(cheap.Strike).CompareTo(StoreDecimal.ToStored(dear.Strike)) > 0,
            "The stored form is expected NOT to be order-preserving; if it now is, D-W29's "
            + "reasoning and FX-NoDecimalOrderingInSql both need revisiting.");
    }

    private static ContractIdentity Identity(decimal strike) =>
        ContractIdentity.Of(Ticker.Normalise("AAPL"), Expiry, OptionRight.Put, strike);
}
