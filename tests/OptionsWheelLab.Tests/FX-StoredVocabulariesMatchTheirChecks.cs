using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using OptionsWheelLab.Core.Identity;
using OptionsWheelLab.Core.MarketData;
using OptionsWheelLab.Core.Membership;
using OptionsWheelLab.Core.Positions;
using OptionsWheelLab.Core.Storage;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-StoredVocabulariesMatchTheirChecks: every declared stored vocabulary and
/// the CHECK enforcing it admit exactly the same values.
/// </summary>
/// <remarks>
/// <b>A standing property rather than one checkpoint's behaviour</b>, which is
/// what puts it in the registry where <see cref="BarsSchemaTests"/> and
/// <see cref="CorporateActionsSchemaTests"/> stay out. Its shape is
/// FX-EveryPolicyBandIsChecked's and FX-EveryBoundKeyIsDocumented's, which hold a
/// declaration and a document together; this holds a declaration and a schema
/// together, one layer down.
/// <para>
/// <b>Each vocabulary is written twice and nothing has held them together.</b>
/// The <c>Store*</c> class declares the permitted values, deliberately rather
/// than deriving them from a member's spelling, and the migration repeats them in
/// a <c>CHECK</c>. Until 3.3 there were two such pairs and both were one or two
/// values long; there are now six, one of them eleven values long, which is where
/// a pair silently disagreeing stops being unlikely.
/// </para>
/// <para>
/// <b>Both directions, and they fail differently.</b> A value the code produces
/// and the <c>CHECK</c> refuses is a write that throws at run time. A value the
/// <c>CHECK</c> admits and the code cannot produce is worse and quieter: nothing
/// writes it, so nothing fails, and it sits in the schema looking like a
/// supported case. That second direction is why this reads the DDL rather than
/// only inserting.
/// </para>
/// </remarks>
public sealed class FX_StoredVocabulariesMatchTheirChecks
{
    private static readonly DateTimeOffset Instant =
        new(2026, 7, 30, 9, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Every column whose stored vocabulary the database enforces, with the
    /// values the code can produce for it.
    /// </summary>
    /// <remarks>
    /// Enumerated from the enums rather than listed, so a member added without a
    /// <c>ToStored</c> arm throws here rather than being quietly omitted from the
    /// comparison.
    /// </remarks>
    public static TheoryData<string, string, string[]> EnforcedVocabularies() =>
        new()
        {
            {
                "contracts", "right",
                Stored<OptionRight>(StoreOptionRight.ToStored)
            },
            {
                "watchlist_membership", "kind",
                Stored<MembershipKind>(StoreMembershipKind.ToStored)
            },
            {
                "corporate_actions", "kind",
                Stored<CorporateActionKind>(StoreCorporateActionKind.ToStored)
            },
            {
                "positions", "state",
                Stored<PositionState>(StorePositionState.ToStored)
            },
            {
                "ledger_entries", "kind",
                Stored<LedgerEntryKind>(StoreLedgerEntryKind.ToStored)
            },
            {
                "trials", "close_kind",
                Stored<TrialCloseKind>(StoreTrialCloseKind.ToStored)
            },
        };

    [Theory]
    [MemberData(nameof(EnforcedVocabularies))]
    public void The_check_carries_exactly_the_values_the_code_produces(
        string table,
        string column,
        string[] produced)
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        var enforced = CheckValues(connection, table, column);

        Assert.Equal(
            produced.OrderBy(value => value, StringComparer.Ordinal),
            enforced.OrderBy(value => value, StringComparer.Ordinal));
    }

    /// <summary>
    /// Every stored form round-trips, so the two halves of each mapping agree.
    /// </summary>
    /// <remarks>
    /// <c>ToStored</c> and <c>ParseStored</c> are two switch statements over the
    /// same list, and a member added to one and not the other is a mapping that
    /// writes a value it cannot read back.
    /// </remarks>
    [Fact]
    public void Every_stored_form_round_trips()
    {
        AssertRoundTrip<OptionRight>(StoreOptionRight.ToStored, StoreOptionRight.ParseStored);
        AssertRoundTrip<MembershipKind>(
            StoreMembershipKind.ToStored, StoreMembershipKind.ParseStored);
        AssertRoundTrip<CorporateActionKind>(
            StoreCorporateActionKind.ToStored, StoreCorporateActionKind.ParseStored);
        AssertRoundTrip<PositionState>(
            StorePositionState.ToStored, StorePositionState.ParseStored);
        AssertRoundTrip<LedgerEntryKind>(
            StoreLedgerEntryKind.ToStored, StoreLedgerEntryKind.ParseStored);
        AssertRoundTrip<TrialCloseKind>(
            StoreTrialCloseKind.ToStored, StoreTrialCloseKind.ParseStored);
    }

    /// <summary>
    /// The DDL reader finds something, so a theory over an empty set would not
    /// pass by asserting nothing.
    /// </summary>
    /// <remarks>
    /// The vacuity guard this corpus puts on every scanning check. A regex that
    /// stopped matching would make every case above compare an empty set with an
    /// empty set on the day someone reformatted a migration.
    /// </remarks>
    [Fact]
    public void The_ddl_reader_finds_every_vocabulary_it_is_asked_for()
    {
        using var store = MigratedStore();
        using var connection = store.Connections.Open(StoreAccess.ReadOnly);

        foreach (var row in EnforcedVocabularies())
        {
            var table = (string)row[0];
            var column = (string)row[1];

            Assert.NotEmpty(CheckValues(connection, table, column));
        }
    }

    /// <summary>
    /// An uninitialised value is refused rather than stored, in every mapping.
    /// </summary>
    /// <remarks>
    /// Every one of these enums starts at one, so <c>default</c> is not a member.
    /// Asserting it here rather than per type is what makes "deliberately not
    /// starting at zero" a property of the set rather than a comment repeated six
    /// times.
    /// </remarks>
    [Fact]
    public void A_default_value_is_refused_by_every_mapping()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StoreOptionRight.ToStored(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => StoreMembershipKind.ToStored(default));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => StoreCorporateActionKind.ToStored(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorePositionState.ToStored(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => StoreLedgerEntryKind.ToStored(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => StoreTrialCloseKind.ToStored(default));
    }

    private static string[] Stored<T>(Func<T, string> toStored)
        where T : struct, Enum =>
        [.. Enum.GetValues<T>().Select(toStored)];

    private static void AssertRoundTrip<T>(Func<T, string> toStored, Func<string, T> parseStored)
        where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            Assert.Equal(value, parseStored(toStored(value)));
        }
    }

    /// <summary>
    /// The values a column's <c>CHECK ... IN (...)</c> admits, read out of the
    /// stored DDL.
    /// </summary>
    /// <remarks>
    /// <c>pragma_table_info</c> does not report constraints, so the DDL text in
    /// <c>sqlite_master</c> is the only place the store says what it enforces.
    /// The pattern is anchored on the column name so a table with two checked
    /// columns reads each separately, and it tolerates the line breaks a long
    /// vocabulary is written across.
    /// </remarks>
    private static IReadOnlyList<string> CheckValues(
        SqliteConnection connection,
        string table,
        string column)
    {
        using var read = connection.CreateCommand();
        read.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name;";
        read.Parameters.AddWithValue("$name", table);

        var ddl = read.ExecuteScalar() as string;

        Assert.NotNull(ddl);

        var clause = Regex.Match(
            ddl,
            $@"CHECK\s*\(\s*{Regex.Escape(column)}\s+IN\s*\((?<values>[^)]*)\)",
            RegexOptions.IgnoreCase);

        Assert.True(
            clause.Success,
            $"'{table}.{column}' has no CHECK ... IN clause in its stored DDL, so the store "
            + "enforces nothing and the code's declared vocabulary stands alone.");

        return
        [
            .. Regex.Matches(clause.Groups["values"].Value, @"'(?<value>[^']*)'")
                .Select(match => match.Groups["value"].Value),
        ];
    }

    private static TempStore MigratedStore()
    {
        var store = TempStore.Empty();
        new MigrationRunner(store.Connections).Run(Instant);
        return store;
    }
}
