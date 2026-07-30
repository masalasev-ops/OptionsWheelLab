using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-PitMembershipExcludesLaterJoiner: as-of membership excludes later
/// joiners.
/// </summary>
/// <remarks>
/// Applying today's watchlist retrospectively selects names that survived,
/// which excludes exactly the cases the risk machinery exists to catch and
/// makes a historical run incapable of failing [D-W9]. The exclusion has to
/// hold on both axes: a name that joined after the queried date, and a name
/// whose join was recorded after the as-of instant, are both later joiners
/// from the point of view of the question being asked.
/// </remarks>
public sealed class FX_PitMembershipExcludesLaterJoiner
{
    private static readonly Ticker Early = Ticker.Normalise("EARL");
    private static readonly Ticker Late = Ticker.Normalise("LATE");

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static DateTimeOffset EveningOf(DateOnly day) =>
        new(day, new TimeOnly(21, 0), TimeSpan.Zero);

    /// <summary>
    /// The effective axis: a name that joined after the queried date is not a
    /// member on it, however current the knowledge.
    /// </summary>
    [Fact]
    public void A_name_that_joined_after_the_queried_date_is_excluded()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Early, MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));
        writer.Append(Late, MembershipKind.Joined, Day(2026, 6, 1), EveningOf(Day(2026, 6, 1)));

        var members = new AsOfMembership(connection).MembersOn(Day(2026, 4, 1), asOf: Day(2026, 7, 1));

        Assert.Contains(Early, members);
        Assert.DoesNotContain(Late, members);
    }

    /// <summary>
    /// The knowledge axis: a join backfilled with an effective date before the
    /// queried date is still invisible to a read as of an instant before it was
    /// recorded. A read that saw it would be applying later knowledge
    /// retrospectively, which is the survivorship D-W9 exists to prevent.
    /// </summary>
    [Fact]
    public void A_join_recorded_after_the_as_of_instant_is_excluded_even_when_backfilled()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Early, MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));
        writer.Append(
            Late,
            MembershipKind.Joined,
            Day(2026, 4, 1),
            EveningOf(Day(2026, 6, 1)),
            reason: "backfilled two months after the fact");

        var reads = new AsOfMembership(connection);

        Assert.DoesNotContain(Late, reads.MembersOn(Day(2026, 4, 15), asOf: Day(2026, 5, 1)));
        Assert.Contains(Late, reads.MembersOn(Day(2026, 4, 15), asOf: Day(2026, 6, 2)));
    }

    /// <summary>
    /// Both boundaries are inclusive: a name that joined on the queried date is
    /// a member on it, and a join recorded on the as-of date is visible to it.
    /// </summary>
    [Fact]
    public void A_name_that_joined_on_the_queried_date_is_included()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Early, MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));

        Assert.Contains(
            Early,
            new AsOfMembership(connection).MembersOn(Day(2026, 3, 1), asOf: Day(2026, 3, 1)));
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(EveningOf(Day(2026, 1, 1)));
        return store;
    }
}
