using OptionsWheelLab.Core.Configuration;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-EveryAppKeyBinds: every <c>app</c>-classed row in
/// <c>CONFIG_REFERENCE.md</c> has a bound settable property on a registered
/// options type.
/// </summary>
/// <remarks>
/// The mirror of FX-EveryBoundKeyIsDocumented, which walks the types and checks
/// the document. This walks the document and checks the types. Between them the
/// loop closes for <c>app</c> keys, which is what the two directions were always
/// meant to do and only half did.
/// <para>
/// The reverse direction is deliberately not checked for <c>rows</c> keys: most
/// are documented and unbound until their own phase, so a standing assertion
/// would fire on all of them. That reasoning does not reach an <c>app</c> key. An
/// <c>app</c> key is bound from <c>appsettings</c> by definition [D-W27], so one
/// that binds to nothing is a defect today rather than a future phase's work.
/// </para>
/// <para>
/// Without it the Phase 0 clause passes by coincidence. Every key matches today,
/// and an <c>app</c> key added at Phase 8 with no binding would satisfy every
/// other test in the suite while failing the phase.
/// </para>
/// <para>
/// This assertion landed at 0.4, in a suite deliberately outside the registry
/// because a phase definition of done was held not to be a fixture. 0.8 registers
/// it instead, so it is discoverable from `FIXTURES.md` rather than only from the
/// file it lived in, and the suite it came from held nothing else.
/// </para>
/// </remarks>
public sealed class FX_EveryAppKeyBinds
{
    [Fact]
    public void Every_app_classed_key_in_the_reference_binds_to_a_registered_options_type()
    {
        var appKeys = AppKeys();
        var bound = BoundKeys();

        // A parse that matched nothing, or a walk that returned nothing, would
        // pass the assertion below without testing anything.
        Assert.NotEmpty(appKeys);
        Assert.NotEmpty(bound);

        var unbound = appKeys
            .Where(key => !bound.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unbound.Count == 0,
            $"These keys are classed 'app' in {RepoRoot.ConfigReferencePath} but no registered "
            + $"options type exposes them: {string.Join(", ", unbound)}. An 'app' key is bound "
            + "from appsettings by definition [D-W27], so one that binds to nothing is a key an "
            + "operator can set and nothing will read. Either bind it at composition, or "
            + "reclassify the row.");
    }

    /// <summary>
    /// Permanent cover for the comparison itself, so the check does not rest on
    /// a live tree that happens to be correct today.
    /// </summary>
    [Fact]
    public void An_app_row_that_nothing_binds_is_reported()
    {
        const string Markdown =
            """
            | Key | Store | Meaning | Consumer | Notes |
            |---|---|---|---|---|
            | `Eodhd:BaseUrl` | app | API root | Ingest | |
            | `Phantom:Setting` | app | bound by nothing | Nobody | |
            | `Gate:MaxDelta` | rows | not this check's business | Risk gate | |
            """;

        var appKeys = ConfigReferenceParser.Parse(Markdown).Keys
            .Where(key => key.Store == ConfigReferenceParser.AppClass)
            .Select(key => key.Key)
            .ToList();

        var bound = BoundKeys();

        Assert.Equal(
            ["Phantom:Setting"],
            appKeys.Where(key => !bound.Contains(key)).ToList());
    }

    /// <summary>
    /// An unbound <c>rows</c> key is ignored, which is the property that keeps
    /// this check narrow enough to survive.
    /// </summary>
    /// <remarks>
    /// Nineteen of the twenty-three <c>rows</c> keys have a value and none of
    /// them binds to anything, deliberately [D-W26, D-W27]. A check that fired
    /// on those would be deleted rather than fixed, and the <c>app</c> direction
    /// would go with it.
    /// </remarks>
    [Fact]
    public void An_unbound_rows_key_is_not_reported()
    {
        const string Markdown =
            """
            | Key | Store | Meaning | Consumer | Notes |
            |---|---|---|---|---|
            | `Gate:MaxDelta` | rows | bound by nothing, correctly | Risk gate | |
            """;

        var appKeys = ConfigReferenceParser.Parse(Markdown).Keys
            .Where(key => key.Store == ConfigReferenceParser.AppClass)
            .ToList();

        Assert.Empty(appKeys);
    }

    private static IReadOnlyList<string> AppKeys() =>
        ConfigReferenceParser.Parse(File.ReadAllText(RepoRoot.ConfigReferencePath)).Keys
            .Where(key => key.Store == ConfigReferenceParser.AppClass)
            .Select(key => key.Key)
            .ToList();

    private static IReadOnlySet<string> BoundKeys() =>
        OptionsKeyWalker
            .KeysOf(Composition.BoundSections(Composition.Services(Composition.Configuration())))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
