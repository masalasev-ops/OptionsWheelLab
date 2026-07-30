using System.Reflection;
using OptionsWheelLab.Core.MarketData;

namespace OptionsWheelLab.Tests;

/// <summary>
/// No read serving a simulated date returns current market data, checked the way
/// the configuration surfaces are.
/// </summary>
/// <remarks>
/// Not a registered fixture, for the same reason as <see cref="AsOfMarketDataTests"/>.
/// <para>
/// <b>The rule is stronger than the configuration surface's, and the difference is
/// the second axis.</b> FX-NoCurrentConfigReadOnSimulatedPath asserts every member
/// takes A date, which suffices when there is only one date to take. A market-data
/// read has two, and a member taking the session date but not the as-of date would
/// satisfy "takes a date" while returning the latest observation. So this asserts
/// the as-of parameter by NAME and type: every value-returning member has a
/// <c>DateOnly</c> parameter called <c>asOf</c>.
/// </para>
/// <para>
/// There is no assignability assertion because there is no current-value type to
/// assign to, and that absence is itself asserted: the one-surface decision is
/// that the strongest form of "cannot read current" is that no current-reading
/// type exists. The name probe is a tripwire, not a proof, and says so.
/// </para>
/// </remarks>
public sealed class AsOfMarketDataSurfaceTests
{
    [Fact]
    public void Every_value_returning_member_takes_the_as_of_date_by_name()
    {
        var offenders = ValueReturningMembers(typeof(AsOfMarketData))
            .Where(member => !TakesAsOf(member))
            .Select(member => $"{member.DeclaringType!.Name}.{member.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"These members of {nameof(AsOfMarketData)} return a value without taking a DateOnly "
            + "named asOf. A two-axis read can take the session date and still leak the latest "
            + $"observation, so the as-of axis is asserted by name: {string.Join(", ", offenders)}.");
    }

    /// <summary>
    /// Guards the guard: a reflection that found nothing would pass the assertion
    /// above while testing nothing.
    /// </summary>
    [Fact]
    public void The_surface_has_members_to_check()
    {
        Assert.NotEmpty(ValueReturningMembers(typeof(AsOfMarketData)));
    }

    /// <summary>
    /// No current-value market-data type exists to cast to.
    /// </summary>
    /// <remarks>
    /// A name-based tripwire rather than a proof: it catches the obvious act of
    /// adding <c>CurrentMarketData</c> beside the as-of surface, which is the move
    /// the one-surface decision forbids. A current-reading type under an unrelated
    /// name is caught by review, not by this.
    /// </remarks>
    [Fact]
    public void No_current_market_data_type_exists()
    {
        var offenders = typeof(AsOfMarketData).Assembly
            .GetTypes()
            .Where(type => type.Namespace == typeof(AsOfMarketData).Namespace)
            .Where(type => type.Name.Contains("Current", StringComparison.OrdinalIgnoreCase))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These types in the MarketData namespace look like a current-value surface, which "
            + "the one-surface decision forbids: no operational path reads current market data, "
            + $"so there is nothing for such a type to serve: {string.Join(", ", offenders)}.");
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
