namespace OptionsWheelLab.Core.Synthetic;

/// <summary>
/// A synthetic chain that could not be read, and every reason why.
/// </summary>
/// <remarks>
/// <b>Every problem in one pass, not the first.</b> These files are written by
/// hand [D-W31], so one carries several typos as often as it carries one, and
/// reporting them one run at a time turns a minute into an afternoon. The same
/// reasoning as the rule that a gate records every failing reason rather than the
/// first, applied to a different subject; that rule governs gate constraints
/// [D-W22] and is cited here as an analogy rather than as authority.
/// <para>
/// Thrown rather than returned, because a partially loaded chain must not exist:
/// the caller either has the whole of what the file states or has none of it.
/// </para>
/// </remarks>
public sealed class MalformedChainException : Exception
{
    public MalformedChainException(IReadOnlyList<string> problems)
        : base(Describe(problems))
    {
        Problems = problems;
    }

    public MalformedChainException()
        : this([])
    {
    }

    public MalformedChainException(string message)
        : base(message)
    {
        Problems = [message];
    }

    public MalformedChainException(string message, Exception innerException)
        : base(message, innerException)
    {
        Problems = [message];
    }

    public MalformedChainException(IReadOnlyList<string> problems, Exception innerException)
        : base(Describe(problems), innerException)
    {
        Problems = problems;
    }

    /// <summary>Every reason the chain was refused, in the order found.</summary>
    public IReadOnlyList<string> Problems { get; } = [];

    private static string Describe(IReadOnlyList<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);

        return problems.Count == 0
            ? "The synthetic chain could not be read."
            : $"The synthetic chain could not be read, for {problems.Count} reason"
                + $"{(problems.Count == 1 ? string.Empty : "s")}:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, problems.Select(problem => "  " + problem));
    }
}
