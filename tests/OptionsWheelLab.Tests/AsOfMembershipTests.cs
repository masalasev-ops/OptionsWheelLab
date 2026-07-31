using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// An as-of membership read resolves the transition sequence, not any single
/// latest row.
/// </summary>
/// <remarks>
/// Not a registered fixture: the one check registered against 1.3 is
/// FX-PitMembershipExcludesLaterJoiner, which has its own file.
/// <para>
/// The divergence test is the argument for the governing axis. Latest version
/// and latest effective date agree everywhere except when a correction carries
/// an earlier effective date than an existing later transition, and there they
/// give different members, so the choice is exercised rather than asserted.
/// </para>
/// </remarks>
public sealed class AsOfMembershipTests
{
    private static readonly Ticker Symbol = Ticker.Normalise("WDGT");

    private static readonly Ticker Other = Ticker.Normalise("ACME");

    private static DateOnly Day(int year, int month, int day) => new(year, month, day);

    private static DateTimeOffset EveningOf(DateOnly day) =>
        new(day, new TimeOnly(21, 0), TimeSpan.Zero);

    /// <summary>
    /// Joined in March, left in August, returned in January: three intervals,
    /// three answers, from one sequence of transitions.
    /// </summary>
    [Fact]
    public void A_name_that_left_and_returned_resolves_correctly_in_each_interval()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Symbol, MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));
        writer.Append(Symbol, MembershipKind.Left, Day(2026, 8, 1), EveningOf(Day(2026, 8, 1)));
        writer.Append(Symbol, MembershipKind.Joined, Day(2027, 1, 10), EveningOf(Day(2027, 1, 10)));

        var reads = new AsOfMembership(connection);
        var asOf = Day(2027, 2, 1);

        Assert.Contains(Symbol, reads.MembersOn(Day(2026, 6, 15), asOf));
        Assert.DoesNotContain(Symbol, reads.MembersOn(Day(2026, 9, 1), asOf));
        Assert.Contains(Symbol, reads.MembersOn(Day(2027, 1, 20), asOf));
    }

    /// <summary>
    /// The second axis, mirroring the market-data reads: a correction recorded
    /// after a simulated instant is invisible as of that instant and visible
    /// after. The correction here ties the original's date, so it also
    /// exercises version breaking the tie.
    /// </summary>
    [Fact]
    public void A_correction_recorded_after_an_instant_is_invisible_as_of_it_and_visible_after()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Symbol, MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));
        writer.Append(
            Symbol,
            MembershipKind.Left,
            Day(2026, 3, 1),
            EveningOf(Day(2026, 3, 5)),
            reason: "the join was recorded in error");

        var reads = new AsOfMembership(connection);
        var queried = Day(2026, 3, 2);

        Assert.Contains(Symbol, reads.MembersOn(queried, asOf: Day(2026, 3, 3)));
        Assert.DoesNotContain(Symbol, reads.MembersOn(queried, asOf: Day(2026, 3, 6)));
    }

    /// <summary>
    /// The case where the two candidate axes disagree, which is why the
    /// governing axis is the effective date. A correction fixing an old join
    /// date arrives as the highest version; under latest-version it would
    /// govern September and override the genuine August departure.
    /// </summary>
    [Fact]
    public void A_correction_to_an_old_join_date_does_not_override_a_later_departure()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Symbol, MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));
        writer.Append(Symbol, MembershipKind.Left, Day(2026, 8, 1), EveningOf(Day(2026, 8, 1)));
        writer.Append(
            Symbol,
            MembershipKind.Joined,
            Day(2026, 2, 15),
            EveningOf(Day(2026, 8, 15)),
            reason: "the join date was recorded two weeks late");

        var reads = new AsOfMembership(connection);
        var asOf = Day(2026, 9, 1);

        Assert.DoesNotContain(Symbol, reads.MembersOn(Day(2026, 9, 1), asOf));
        Assert.Contains(Symbol, reads.MembersOn(Day(2026, 2, 20), asOf));
    }

    [Fact]
    public void Before_anything_was_observed_the_set_is_empty()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(Symbol, MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));

        Assert.Empty(new AsOfMembership(connection).MembersOn(Day(2026, 3, 1), asOf: Day(2026, 2, 28)));
    }

    [Fact]
    public void Members_return_in_symbol_order_not_insertion_order()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        writer.Append(
            Ticker.Normalise("ZZZZ"), MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));
        writer.Append(
            Ticker.Normalise("AAAA"), MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));

        var members = new AsOfMembership(connection).MembersOn(Day(2026, 3, 2), asOf: Day(2026, 3, 2));

        Assert.Equal(
            [Ticker.Normalise("AAAA"), Ticker.Normalise("ZZZZ")], members);
    }

    /// <summary>
    /// The declared stored form is what migration 4's frozen CHECK carries, and
    /// the read's filter is rendered from the declaration. This is the one
    /// place the declaration and the frozen DDL are tied together: the
    /// migration's text cannot change, so if the declared form ever moved, this
    /// fails and names the coupling instead of the read silently returning
    /// empty.
    /// </summary>
    [Fact]
    public void The_declared_stored_forms_match_the_frozen_check_vocabulary()
    {
        Assert.Equal("joined", StoreMembershipKind.ToStored(MembershipKind.Joined));
        Assert.Equal("left", StoreMembershipKind.ToStored(MembershipKind.Left));
    }

    /// <summary>
    /// The per-symbol read and the set read give the same answer everywhere.
    /// </summary>
    /// <remarks>
    /// <b>Evidence, not the property.</b> The property is that there is one
    /// ranking and the per-symbol member narrows its input rather than restating
    /// its rule, which no test can assert. This is the tripwire if that choice is
    /// ever undone: a second copy of the query would pass every case above and
    /// fail here the moment the two drifted.
    /// <para>
    /// Swept over a grid rather than checked at chosen points, because a second
    /// copy would agree at the points its author thought of. The probe dates are
    /// every transition and observation date in the history with its immediate
    /// neighbours, since an inclusive boundary that slipped a day is invisible to
    /// a coarse sweep, plus a coarse sweep so agreement is not asserted only
    /// where the history has an event.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_per_symbol_read_agrees_with_the_set_read_across_the_whole_history()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.Write);
        var writer = new MembershipWriter(connection);

        // Every shape the cases above exercise, in one history: a departure and
        // a return, a correction carrying an earlier effective date than a later
        // genuine transition, and on the second symbol a correction that governs
        // by tying a date.
        writer.Append(Symbol, MembershipKind.Joined, Day(2026, 3, 1), EveningOf(Day(2026, 3, 1)));
        writer.Append(Symbol, MembershipKind.Left, Day(2026, 8, 1), EveningOf(Day(2026, 8, 1)));
        writer.Append(
            Symbol,
            MembershipKind.Joined,
            Day(2026, 2, 15),
            EveningOf(Day(2026, 8, 15)),
            reason: "the join date was recorded two weeks late");
        writer.Append(Symbol, MembershipKind.Joined, Day(2027, 1, 10), EveningOf(Day(2027, 1, 10)));

        writer.Append(Other, MembershipKind.Joined, Day(2026, 5, 1), EveningOf(Day(2026, 5, 1)));
        writer.Append(
            Other,
            MembershipKind.Left,
            Day(2026, 5, 1),
            EveningOf(Day(2026, 6, 1)),
            reason: "the join was recorded in error");

        var reads = new AsOfMembership(connection);
        var probes = Probes();

        // A sweep in which nobody was ever a member would agree vacuously, and
        // a sweep in which nobody was ever absent would agree for the other
        // vacuous reason.
        var everMember = false;
        var everAbsent = false;

        foreach (var date in probes)
        {
            foreach (var asOf in probes)
            {
                var members = reads.MembersOn(date, asOf);

                foreach (var symbol in (Ticker[])[Symbol, Other])
                {
                    var inSet = members.Contains(symbol);

                    everMember |= inSet;
                    everAbsent |= !inSet;

                    Assert.Equal(inSet, reads.WasMemberOn(symbol, date, asOf));
                }
            }
        }

        Assert.True(everMember);
        Assert.True(everAbsent);
    }

    /// <summary>
    /// The dates the agreement sweep asks about.
    /// </summary>
    private static IReadOnlyList<DateOnly> Probes()
    {
        DateOnly[] events =
        [
            Day(2026, 2, 15), Day(2026, 3, 1), Day(2026, 5, 1), Day(2026, 6, 1),
            Day(2026, 8, 1), Day(2026, 8, 15), Day(2027, 1, 10),
        ];

        var days = new List<DateOnly>();

        foreach (var day in events)
        {
            days.Add(day.AddDays(-1));
            days.Add(day);
            days.Add(day.AddDays(1));
        }

        for (var day = Day(2026, 1, 1); day < Day(2027, 4, 1); day = day.AddDays(37))
        {
            days.Add(day);
        }

        return [.. days.Distinct().Order()];
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(EveningOf(Day(2026, 1, 1)));
        return store;
    }
}
