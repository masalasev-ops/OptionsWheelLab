namespace OptionsWheelLab.Tests;

/// <summary>
/// The clauses of a phase definition of done that no other test covers.
/// </summary>
/// <remarks>
/// Not a registered fixture, so deliberately not named <c>FX-*</c>:
/// FX-RegistryMatchesDisk requires every <c>FX-*.cs</c> to have a row in
/// <c>FIXTURES.md</c>, and a phase definition of done is not a fixture.
/// <para>
/// A definition of done that is inspected at sign-off rather than checked is
/// only as good as whoever inspects it, and it is checked once. Anything in it
/// that can be a standing assertion should be one.
/// </para>
/// </remarks>
public sealed class PhaseDefinitionOfDoneTests
{
    /// <summary>
    /// Phase 0: every <c>app</c>-classed key in <c>CONFIG_REFERENCE.md</c> is
    /// proven to bind.
    /// </summary>
    /// <remarks>
    /// The reverse of FX-EveryBoundKeyIsDocumented, which runs bound to
    /// documented. <c>CONFIG_REFERENCE.md</c> declines to make the reverse a
    /// standing check because most keys there are documented and deliberately
    /// unbound until their own phase, but that reasoning is about
    /// <c>rows</c>-classed keys. An <c>app</c>-classed key is bound from
    /// <c>appsettings</c> by definition, so for that class the reverse holds
    /// today and always will.
    /// <para>
    /// Without this the clause passes by coincidence. Every key currently
    /// matches, and an <c>app</c> key added at Phase 8 with no binding would
    /// satisfy every other test in the suite while failing the phase.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_app_classed_key_in_the_reference_is_bound()
    {
        var appKeys = AppClassedKeys();
        var bound = BoundKeys();

        var unbound = appKeys
            .Where(key => !bound.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unbound.Count == 0,
            $"These keys are classed 'app' in {RepoRoot.ConfigReferencePath} but no registered "
            + $"options type exposes them: {string.Join(", ", unbound)}. An app-classed key is "
            + "bound from appsettings by definition, so one that binds to nothing is either "
            + "misclassified or missing its property.");
    }

    /// <summary>
    /// Guards the guard. A parse that matched nothing, or a walk that found no
    /// keys, would pass the assertion above while testing nothing.
    /// </summary>
    [Fact]
    public void The_app_classed_set_and_the_bound_set_are_both_found()
    {
        Assert.NotEmpty(AppClassedKeys());
        Assert.NotEmpty(BoundKeys());
    }

    private static IReadOnlyList<string> AppClassedKeys() =>
        [.. ConfigReferenceParser
            .Parse(File.ReadAllText(RepoRoot.ConfigReferencePath))
            .Keys
            .Where(key => key.Store == ConfigReferenceParser.AppClass)
            .Select(key => key.Key)];

    private static IReadOnlySet<string> BoundKeys() =>
        OptionsKeyWalker
            .KeysOf(Composition.BoundSections(Composition.Services(Composition.Configuration())))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
