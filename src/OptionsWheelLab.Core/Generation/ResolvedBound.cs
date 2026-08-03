using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// Reads one gate bound as of the simulated date, or stops the evaluation
/// [D-W37].
/// </summary>
/// <remarks>
/// <b>One statement of D-W37's refusal, shared by every bound record.</b> It was
/// private to <see cref="GateBounds"/> while that was the only record; 2.4 adds
/// <see cref="PortfolioBounds"/>, so it moved here rather than being written
/// twice. That is this corpus's rule against two statements of one fact reaching
/// a string: what D-W37 buys is that the message names the key and the date, so
/// a second copy is a second thing that has to keep naming both, and the copy
/// that stopped would be found by an operator reading a run that halted rather
/// than by a test.
/// <para>
/// Internal because it is a bypass: it reads a key directly, where the point of
/// the bound records is that a constraint is handed numbers and cannot reach
/// configuration at all. The records are the only callers, and all of them are in
/// this assembly.
/// </para>
/// <para>
/// <b>The message does not name which record could not resolve, and today the
/// key implies it.</b> Read at 2.4 over two records and again at 3.4 over four:
/// <see cref="GateBounds"/> resolves six keys all prefixed <c>Gate:</c>,
/// <see cref="PortfolioBounds"/> four all prefixed <c>Risk:</c>,
/// <see cref="Positions.TrialBounds"/> two all prefixed <c>Trial:</c>, and
/// <see cref="Positions.CostBounds"/> three all prefixed <c>Costs:</c>. The
/// fifteen still partition cleanly and a reader can tell the family from the
/// message. <b>That holds by convention rather than by construction.</b> Nothing
/// states that a bound record's keys share a section, and nothing checks it, so a
/// record reading across sections would make the message ambiguous without
/// anything failing. <c>Gate:MaxDte</c> is the near case: it already appears in a
/// cross-key invariant beside a <c>Trial:</c> key [D-W24, D-W34], on the write
/// path rather than here. A record that broke the partition would want the family
/// in the message, which is a change to what D-W37 says a message carries and so
/// a decision rather than an edit.
/// </para>
/// <para>
/// <b>What the message calls the thing is now wrong for three families out of
/// four, and 3.4 raises it rather than fixing it.</b> It reads "a gate bound",
/// which has covered <c>Risk:</c> caps since 2.4 and now covers a trial bound and
/// a cost. Generalising the wording is the same change the paragraph above calls
/// a decision, since what D-W37's refusal says is the reason this type exists at
/// all.
/// </para>
/// </remarks>
internal static class ResolvedBound
{
    /// <exception cref="InvalidOperationException">
    /// When the key has no value in force on that date.
    /// </exception>
    internal static decimal RequiredDecimal(
        AsOfConfiguration configuration,
        string key,
        DateOnly simulatedDate) =>
        configuration.ResolveDecimal(key, simulatedDate) ?? throw Unresolvable(key, simulatedDate);

    /// <exception cref="InvalidOperationException">
    /// When the key has no value in force on that date.
    /// </exception>
    internal static int RequiredInt(
        AsOfConfiguration configuration,
        string key,
        DateOnly simulatedDate) =>
        configuration.ResolveInt(key, simulatedDate) ?? throw Unresolvable(key, simulatedDate);

    /// <summary>
    /// A key whose value is a bare word rather than a number.
    /// </summary>
    /// <remarks>
    /// <c>Costs:FillPoint</c> is the first and today the only one. The typed
    /// accessors above parse, so an unparseable value reads as absent and stops
    /// here; this one hands the text on, and the caller's stored form is what
    /// refuses a word outside the vocabulary. That split is deliberate: a
    /// misspelled word is a different failure from a missing row, and the message
    /// naming which is the point of both refusals.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// When the key has no value in force on that date.
    /// </exception>
    internal static string RequiredWord(
        AsOfConfiguration configuration,
        string key,
        DateOnly simulatedDate) =>
        configuration.Resolve(key, simulatedDate) ?? throw Unresolvable(key, simulatedDate);

    /// <summary>
    /// The message names the key and the date, because either alone leaves the
    /// reader guessing which of the two is wrong.
    /// </summary>
    private static InvalidOperationException Unresolvable(string key, DateOnly simulatedDate) =>
        new($"'{key}' has no value in force on {StoreDate.ToStored(simulatedDate)}. "
            + "A gate bound that cannot be resolved is not defaulted: the evaluation stops "
            + "rather than producing a feasible set under an unknown constraint [D-W37].");
}
