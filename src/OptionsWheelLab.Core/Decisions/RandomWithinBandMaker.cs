using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Generation;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Positions;

namespace OptionsWheelLab.Core.Decisions;

/// <summary>
/// Draws uniformly among the candidates its own band admits [D-W4].
/// </summary>
/// <remarks>
/// <b>It separates selection skill from the return to being short volatility,
/// which it can only do if it is not itself constrained by a band someone
/// chose</b> [SYSTEM_DESIGN §3.5]. So its band is its own and wider than the
/// baseline's, sitting exactly at the gate's delta ceiling, and it draws inside
/// the baseline's expiry window so a difference between the two arms is not
/// partly a difference in what they were offered.
/// <para>
/// <b>This is the one arm that differs in rule rather than only in rows.</b> The
/// baseline and the learner share an algorithm and differ by policy; a uniform
/// draw is not that algorithm with a different band, because a control that
/// preferred anything would be measuring the thing it exists to be a floor for.
/// </para>
/// <para>
/// <b>The generator is built inside the call and never held</b> [D-W51]. A held
/// one makes a session's draw depend on how many draws preceded it, so re-running
/// one day in isolation would differ from that day inside a walk-forward, and a
/// decision has to be re-scorable from the record alone [D-W3]. It is also the
/// failure a same-process test cannot see: a held generator passes a byte-identical
/// comparison of two invocations while the per-session property is false.
/// </para>
/// <para>
/// <b>The seed is resolved as of the session</b>, like every other configuration
/// read, so a re-seeded experiment is a new config version rather than a rebuild.
/// </para>
/// </remarks>
public sealed class RandomWithinBandMaker : IDecisionMaker
{
    private readonly AsOfConfiguration _configuration;

    public RandomWithinBandMaker(AsOfConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
    }

    public string MakerId => MakerIds.Random;

    public MakerDecision Decide(
        Ticker symbol,
        DateOnly session,
        PositionState state,
        BookState book,
        IReadOnlyList<GatedCandidate> offered,
        OpenShort? openShort = null)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(offered);

        var policy = Policy.ForRandom(_configuration, session);

        // The draw is built per call from a seed derived per session and name, so
        // a roll and an open on one session draw the same index and a roll on the
        // next draws its own [D-W51].
        EnumeratedCandidate Draw(IReadOnlyList<EnumeratedCandidate> among)
        {
            var seed = ResolvedBound.RequiredInt(_configuration, ConfigKeys.RandomSeed, session);

            return among[new Random(MakerSeed.For(seed, symbol, session)).Next(among.Count)];
        }

        if (openShort is { } held)
        {
            return MakerSelection.ForOpenShort(policy, held, session, offered, Draw);
        }

        var admitted = MakerSelection.Admitted(policy, session, offered);

        if (admitted.Count == 0)
        {
            return new MakerDecision(DecisionKind.None, null, null, policy.Version);
        }

        return MakerSelection.Taking(Draw(admitted), policy.Version);
    }
}
