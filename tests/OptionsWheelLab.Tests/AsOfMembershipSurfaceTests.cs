using System.Reflection;
using OptionsWheelLab.Core.Membership;

namespace OptionsWheelLab.Tests;

/// <summary>
/// No read serving a simulated date returns current membership, checked the way
/// the market-data surface is.
/// </summary>
/// <remarks>
/// Not a registered fixture, for the same reason as
/// <see cref="AsOfMembershipTests"/>.
/// <para>
/// A mirrored copy of <see cref="AsOfMarketDataSurfaceTests"/> rather than a
/// shared helper, because the two surfaces carry different guarantees: market
/// data's says no current type can ever exist, membership's says none exists
/// until a decision creates one. A check is not a fact; two copies do not drift
/// the way duplicated facts do, and each stays free to diverge with its
/// surface.
/// </para>
/// <para>
/// The membership read has two axes like the market-data reads, so the same
/// leak exists: a member taking the query date but not <c>asOf</c> would
/// return the latest recorded transitions while looking compliant. The as-of
/// parameter is asserted by NAME and type.
/// </para>
/// </remarks>
public sealed class AsOfMembershipSurfaceTests
{
    [Fact]
    public void Every_value_returning_member_takes_the_as_of_date_by_name()
    {
        var offenders = ValueReturningMembers(typeof(AsOfMembership))
            .Where(member => !TakesAsOf(member))
            .Select(member => $"{member.DeclaringType!.Name}.{member.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"These members of {nameof(AsOfMembership)} return a value without taking a DateOnly "
            + "named asOf. A two-axis read can take the query date and still leak the latest "
            + $"recorded transitions, so the as-of axis is asserted by name: {string.Join(", ", offenders)}.");
    }

    /// <summary>
    /// Guards the guard: a reflection that found nothing would pass the
    /// assertion above while testing nothing.
    /// </summary>
    [Fact]
    public void The_surface_has_members_to_check()
    {
        Assert.NotEmpty(ValueReturningMembers(typeof(AsOfMembership)));
    }

    /// <summary>
    /// No current-value membership type exists to cast to.
    /// </summary>
    /// <remarks>
    /// A name-based tripwire rather than a proof, as the market-data one is. If
    /// Phase 8's ingest ever justifies a current-membership read, it arrives as
    /// a recorded decision and this test is amended by it, which is the point:
    /// the tripwire makes the addition a deliberate act rather than a drive-by.
    /// </remarks>
    [Fact]
    public void No_current_membership_type_exists()
    {
        var offenders = typeof(AsOfMembership).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(AsOfMembership).Namespace)
            .Where(type => type.Name.Contains("Current", StringComparison.OrdinalIgnoreCase))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These types in the Membership namespace look like a current-value surface, which "
            + "no decision has justified: every consumer in the design so far serves a "
            + $"simulated date [D-W9]: {string.Join(", ", offenders)}.");
    }

    private static IReadOnlyList<MemberInfo> ValueReturningMembers(Type surface)
    {
        var methods = surface
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Where(method => method.ReturnType != typeof(void))
            .Cast<MemberInfo>();

        var properties = surface
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Cast<MemberInfo>();

        return [.. methods, .. properties];
    }

    private static bool TakesAsOf(MemberInfo member) =>
        member is MethodInfo method
        && method.GetParameters().Any(parameter =>
            parameter.Name == "asOf"
            && (parameter.ParameterType == typeof(DateOnly)
                || parameter.ParameterType == typeof(DateOnly?)));
}
