namespace OptionsWheelLab.Core.Positions;

/// <summary>
/// The dates the market traded, transcribed and never derived [D-W46].
/// </summary>
/// <remarks>
/// <b>It answers one question</b>, which is what the next session after a date
/// is, and it redefines no day count. Days to expiry and a trial's day bound are
/// calendar days and stay calendar days [D-W24, D-W14], so nothing here is
/// reachable from either.
/// <para>
/// <b>A date it does not reach stops the caller rather than resolving either
/// way</b> [D-W37]. Guessing forward would put an unrecorded market assumption
/// inside a settlement date, and guessing that a date is a session would settle
/// proceeds on a day the market was shut. Both are the kind of wrong answer that
/// looks like an answer.
/// </para>
/// <para>
/// Held sorted, which the constructor does once rather than every lookup. The
/// sequence is small: a decade of sessions is around two and a half thousand
/// dates.
/// </para>
/// </remarks>
public sealed class SessionCalendar
{
    private readonly DateOnly[] _sessions;

    private SessionCalendar(DateOnly[] sessions)
    {
        _sessions = sessions;
    }

    /// <summary>The first session this calendar carries.</summary>
    public DateOnly First => _sessions[0];

    /// <summary>The last session this calendar carries.</summary>
    public DateOnly Last => _sessions[^1];

    /// <summary>
    /// A calendar over <paramref name="sessions"/>, deduplicated and sorted.
    /// </summary>
    /// <exception cref="ArgumentException">When no session is supplied.</exception>
    public static SessionCalendar Of(IEnumerable<DateOnly> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        var ordered = sessions.Distinct().Order().ToArray();

        if (ordered.Length == 0)
        {
            throw new ArgumentException(
                "A session calendar with no sessions answers every question with a refusal, "
                + "which is indistinguishable from a market that never opened. Supply the "
                + "sessions the scenario states [D-W46].",
                nameof(sessions));
        }

        return new SessionCalendar(ordered);
    }

    /// <summary>Whether <paramref name="date"/> is a session.</summary>
    public bool IsSession(DateOnly date) => Array.BinarySearch(_sessions, date) >= 0;

    /// <summary>
    /// The first session after <paramref name="date"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="date"/> need not itself be a session: the question a
    /// settlement asks is what comes next, and an exercise resolved against a
    /// session's close is asking about the session after that one whether or not
    /// the calendar was consulted about the date itself.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// When the calendar carries no session after that date.
    /// </exception>
    public DateOnly NextSessionAfter(DateOnly date)
    {
        foreach (var session in _sessions)
        {
            if (session > date)
            {
                return session;
            }
        }

        throw new InvalidOperationException(
            $"The session calendar reaches {Last:yyyy-MM-dd} and was asked for the session "
            + $"after {date:yyyy-MM-dd}. It does not guess forward, because a settlement date "
            + "invented here would be an unrecorded assumption about when the market was open "
            + "[D-W46, D-W37].");
    }
}
