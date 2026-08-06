using System.Text;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// The seed a drawing maker uses for one name on one session, derived from the
/// configured seed and reproducible from it alone [D-W51].
/// </summary>
/// <remarks>
/// <b>Per session and name rather than once per run.</b> A run-scoped generator
/// makes a session's draw depend on how many draws preceded it, so re-running one
/// day in isolation gives a different answer from that day inside a walk-forward.
/// A decision has to be re-scorable from the record alone [D-W3], and a draw that
/// needs the days before it is not.
/// <para>
/// <b>Not <see cref="HashCode"/> and not <see cref="string.GetHashCode()"/>,
/// which are the two things nearest to hand and both wrong.</b> Both are
/// randomised per process, so they agree with themselves inside one run and
/// disagree between runs.
/// <see cref="Generation.GatedCandidate.GetHashCode"/> uses the first, correctly,
/// for in-memory equality, and it is on the very type a maker consumes, so the
/// wrong idiom is one step away from the right one.
/// </para>
/// <para>
/// <b>The failure would be invisible to every same-process test.</b>
/// FX-RunIsByteIdentical compares two invocations inside one process, so a
/// per-process hash would make both agree and the fixture would pass while the
/// property was false. That is why the pins in <c>MakerSeedTests</c> compare
/// against literals: an assertion against another invocation is exactly what a
/// randomised hash satisfies.
/// </para>
/// <para>
/// <b>FNV-1a, because its constants are published and its result is a function of
/// its input alone.</b> No process seed, no runtime version, no culture. The
/// input is the canonical stored forms, which is the text the store holds, so the
/// derivation reads the same values a reader would see in the database rather
/// than a rendering of them.
/// </para>
/// </remarks>
public static class MakerSeed
{
    private const uint OffsetBasis = 2166136261;

    private const uint Prime = 16777619;

    /// <summary>
    /// The seed for this name on this session.
    /// </summary>
    /// <remarks>
    /// The three parts are separated by a character that cannot appear in any of
    /// them, so no two different triples render to one string: a ticker carries
    /// letters, digits and a dash, a stored date digits and dashes, and the seed
    /// digits.
    /// </remarks>
    public static int For(int configuredSeed, Ticker symbol, DateOnly session)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        var text = $"{configuredSeed}|{symbol.Value}|{StoreDate.ToStored(session)}";
        var hash = OffsetBasis;

        foreach (var octet in Encoding.UTF8.GetBytes(text))
        {
            hash = unchecked((hash ^ octet) * Prime);
        }

        return unchecked((int)hash);
    }
}
