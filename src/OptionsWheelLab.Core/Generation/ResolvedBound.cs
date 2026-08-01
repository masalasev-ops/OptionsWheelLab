using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Generation;

/// <summary>
/// Reads one gate bound as of the simulated date, or stops the evaluation
/// [D-W37].
/// </summary>
/// <remarks>
/// <b>One statement of D-W37's refusal, shared by every bound record.</b> The
/// message is what an operator reads when a run stops, and two spellings of it
/// would drift the way two copies of one query do. It was private to
/// <see cref="GateBounds"/> while that was the only record; 2.4 adds
/// <see cref="PortfolioBounds"/>, so it moved here rather than being written
/// twice.
/// <para>
/// Internal because it is a bypass: it reads a key directly, where the point of
/// the bound records is that a constraint is handed numbers and cannot reach
/// configuration at all. The records are the only callers, and both are in this
/// assembly.
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
    /// The message names the key and the date, because either alone leaves the
    /// reader guessing which of the two is wrong.
    /// </summary>
    private static InvalidOperationException Unresolvable(string key, DateOnly simulatedDate) =>
        new($"'{key}' has no value in force on {StoreDate.ToStored(simulatedDate)}. "
            + "A gate bound that cannot be resolved is not defaulted: the evaluation stops "
            + "rather than producing a feasible set under an unknown constraint [D-W37].");
}
