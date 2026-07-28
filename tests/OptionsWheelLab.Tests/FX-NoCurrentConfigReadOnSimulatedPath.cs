using System.Reflection;
using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-NoCurrentConfigReadOnSimulatedPath: no simulated-date component reads
/// current config.
/// </summary>
/// <remarks>
/// Enforced by the shape of the as-of surface rather than by scanning callers.
/// A scan would assert over an empty set today, since nothing serves a
/// simulated date yet, and would afterwards only ever be as complete as the
/// scan. A type that cannot express the misuse needs neither.
/// </remarks>
public sealed class FX_NoCurrentConfigReadOnSimulatedPath
{
    /// <summary>
    /// Every value-returning member of the as-of surface takes a date. A
    /// property returning a value has no parameters at all, so this catches
    /// those too.
    /// </summary>
    [Fact]
    public void No_member_of_the_as_of_surface_returns_a_value_without_a_date()
    {
        var offenders = ValueReturningMembers(typeof(AsOfConfiguration))
            .Where(member => !TakesADate(member))
            .Select(Describe)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"These members of {nameof(AsOfConfiguration)} return a value without taking a date, "
            + "so a component serving a simulated date could read configuration that session never "
            + $"ran under: {string.Join(", ", offenders)}.");
    }

    /// <summary>
    /// Guards the guard. If the reflection found nothing, the assertion above
    /// would pass while testing nothing.
    /// </summary>
    [Fact]
    public void The_as_of_surface_has_members_to_check()
    {
        Assert.NotEmpty(ValueReturningMembers(typeof(AsOfConfiguration)));
    }

    /// <summary>
    /// The two surfaces are separate types. A shared implementation could be
    /// cast back to the current-value surface, and the guarantee would be a
    /// convention again.
    /// </summary>
    [Fact]
    public void The_as_of_surface_is_not_assignable_to_the_current_surface()
    {
        Assert.False(typeof(CurrentConfiguration).IsAssignableFrom(typeof(AsOfConfiguration)));
        Assert.False(typeof(AsOfConfiguration).IsAssignableFrom(typeof(CurrentConfiguration)));
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

    private static bool TakesADate(MemberInfo member) =>
        member is MethodInfo method
        && method.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(DateOnly)
            || parameter.ParameterType == typeof(DateOnly?));

    private static string Describe(MemberInfo member) =>
        $"{member.DeclaringType!.Name}.{member.Name}";
}
