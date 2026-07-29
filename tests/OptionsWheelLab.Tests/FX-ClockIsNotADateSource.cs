using System.Reflection;
using System.Text.RegularExpressions;
using OptionsWheelLab.Core.Configuration;
using OptionsWheelLab.Core.Time;

namespace OptionsWheelLab.Tests;

/// <summary>
/// FX-ClockIsNotADateSource: no simulated-date path derives its date from the
/// clock.
/// </summary>
/// <remarks>
/// Enforced by shape rather than by scanning callers, which is the same choice
/// FX-NoCurrentConfigReadOnSimulatedPath made and for the same reason: a scan
/// over callers would assert about an empty set today and would afterwards only
/// ever be as complete as the scan.
/// <para>
/// The failure this exists to prevent is not a crash. The lab has two kinds of
/// time and they are unrelated, so a component that wants the simulated date and
/// reaches for the clock gets an answer that is plausible, non-null and wrong
/// [D-W30]. Nothing downstream can tell.
/// </para>
/// <para>
/// FX-NoAmbientClock is the other half and is a source guard rather than a
/// fixture, because it must fail even when the build does not. That one says the
/// ambient clock is not called; this one says the injected clock cannot be
/// mistaken for a date and is not held anywhere it could be.
/// </para>
/// </remarks>
public sealed class FX_ClockIsNotADateSource
{
    /// <summary>
    /// SQLite's own clock functions. A column defaulted to one of these is the
    /// store reading a wall clock, which is the same leak with the store as the
    /// culprit.
    /// </summary>
    /// <remarks>
    /// <b>The bare-call forms are the ones that hide.</b> SQLite's date and time
    /// functions default their time-value argument to <c>'now'</c> when it is
    /// omitted, so <c>datetime()</c>, <c>date()</c>, <c>time()</c>,
    /// <c>julianday()</c> and <c>strftime('%Y')</c> all return the current time
    /// while carrying neither <c>'now'</c> nor <c>CURRENT_</c>. Measured against
    /// the SQLite that <c>Microsoft.Data.Sqlite</c> bundles here, 3.53.3, rather
    /// than taken from the documentation.
    /// <para>
    /// A catch-list, not an exemption list, exactly as the source guard's is: an
    /// incomplete one still catches what is on it, so adding a form never needs
    /// a decision.
    /// </para>
    /// </remarks>
    private static readonly (string Pattern, string Form)[] SqlClockFunctions =
    [
        (@"\bCURRENT_(TIMESTAMP|DATE|TIME)\b", "CURRENT_TIMESTAMP and friends"),
        (@"'now'", "an explicit 'now' time value"),
        (@"\bunixepoch\s*\(", "unixepoch()"),
        (@"\b(datetime|date|time|julianday)\s*\(\s*\)", "a date or time function called with no time value"),
        (@"\bstrftime\s*\(\s*'[^']*'\s*\)", "strftime with the time value omitted"),
    ];

    /// <summary>
    /// The clock returns an instant and offers nothing else. Converting an
    /// instant to a trading date needs a market calendar and a session timezone
    /// and is Phase 1, so a clock that could return a date would make the leak a
    /// one-line mistake rather than a visible conversion.
    /// </summary>
    [Fact]
    public void The_clock_hands_out_an_instant_and_nothing_else()
    {
        var members = typeof(IClock)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(member => member is not MethodInfo { IsSpecialName: true })
            .ToList();

        var member = Assert.Single(members);
        var property = Assert.IsAssignableFrom<PropertyInfo>(member);

        Assert.Equal(typeof(DateTimeOffset), property.PropertyType);
        Assert.False(property.CanWrite);
    }

    /// <summary>
    /// No type in <c>Core</c> holds a clock. The clock is read at composition and
    /// entry points only, and <c>Core</c> is below both.
    /// </summary>
    /// <remarks>
    /// Constructor parameters, fields and properties together, because a
    /// dependency taken any of those three ways is a dependency. The two hosts
    /// are deliberately out of scope: the Worker's entry point is where the clock
    /// is meant to be read.
    /// </remarks>
    [Fact]
    public void No_type_in_core_holds_a_clock()
    {
        var types = CoreTypes();

        // A reflection walk that found no types would pass while asserting
        // nothing.
        Assert.NotEmpty(types);

        var holders = types
            .Where(HoldsAClock)
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            holders.Count == 0,
            $"These types in {typeof(IClock).Assembly.GetName().Name} take an {nameof(IClock)}: "
            + $"{string.Join(", ", holders)}. It is read at composition and entry points only, and "
            + "everything below them takes an instant as a parameter [D-W30].");
    }

    /// <summary>
    /// The simulated-date shape, named rather than assumed.
    /// </summary>
    /// <remarks>
    /// A type exposing a member that takes a <c>DateOnly</c> is serving a
    /// simulated date, which is the predicate the as-of guard already uses. This
    /// anchors the assertion above to the types that would actually do the
    /// damage, instead of resting on a blanket absence that would still pass if
    /// every such type disappeared.
    /// </remarks>
    [Fact]
    public void The_types_that_serve_a_simulated_date_hold_no_clock()
    {
        var servers = CoreTypes().Where(ServesASimulatedDate).ToList();

        Assert.NotEmpty(servers);
        Assert.Contains(typeof(AsOfConfiguration), servers);
        Assert.Contains(typeof(AsOfBoundary), servers);

        Assert.All(servers, type => Assert.False(HoldsAClock(type)));
    }

    /// <summary>
    /// The store is not a date source either.
    /// </summary>
    /// <remarks>
    /// This is the one place the source guard structurally cannot reach: it
    /// strips raw string literals before scanning, by design, and every SQL
    /// statement in this repository lives in one. A <c>DEFAULT
    /// CURRENT_TIMESTAMP</c> on a Phase 1 <c>observed_at</c> column would be an
    /// ambient clock inside the database, invisible to a token scan and fatal to
    /// as-of reads.
    /// <para>
    /// <b>Known limit.</b> The extraction keys on a statement keyword, so a form
    /// carrying none of them is not scanned. Recorded rather than widened,
    /// because widening it would change what FX-NoDecimalOrderingInSql sees.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_sql_in_the_codebase_asks_the_store_for_the_time()
    {
        var statements = RepoRoot
            .SourceFilesUnder(RepoRoot.SourcePath)
            .SelectMany(file => DecimalOrderingInSql
                .SqlIn(File.ReadAllText(file))
                .Select(sql => (File: Path.GetFileName(file), Sql: sql)))
            .ToList();

        // The same vacuity guard FX-NoDecimalOrderingInSql carries, for the same
        // reason: an extraction that found nothing would pass here silently.
        Assert.NotEmpty(statements);
        Assert.Contains(statements, statement => statement.Sql.Contains("config_rows", StringComparison.Ordinal));

        var offences = statements
            .SelectMany(statement => ClockFunctionsIn(statement.Sql)
                .Select(form => $"{statement.File}: {form}"))
            .OrderBy(offence => offence, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offences.Count == 0,
            "This SQL asks the store for the current time, which is an ambient clock the source "
            + $"guard cannot see because it strips raw strings: {string.Join(", ", offences)}. An "
            + "instant is supplied by the caller, from a clock read at an entry point [D-W30].");
    }

    /// <summary>
    /// The detector, on synthetic SQL, so it is known to fire.
    /// </summary>
    [Fact]
    public void A_column_defaulted_to_the_stores_clock_would_be_reported()
    {
        const string Sql = """
            CREATE TABLE contract_quotes (
                contract_id TEXT NOT NULL,
                observed_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            """;

        Assert.NotEmpty(ClockFunctionsIn(Sql));
    }

    /// <summary>
    /// The bare-call forms, which carry neither <c>'now'</c> nor
    /// <c>CURRENT_</c> and are the reason the list is not three entries long.
    /// </summary>
    /// <remarks>
    /// Measured rather than assumed: on the bundled SQLite 3.53.3, each of these
    /// returns the current time with its time-value argument omitted.
    /// </remarks>
    [Theory]
    [InlineData("observed_at TEXT NOT NULL DEFAULT (datetime())")]
    [InlineData("session_date TEXT NOT NULL DEFAULT (date())")]
    [InlineData("observed_at TEXT NOT NULL DEFAULT (time())")]
    [InlineData("observed_at REAL NOT NULL DEFAULT (julianday())")]
    [InlineData("session_year TEXT NOT NULL DEFAULT (strftime('%Y'))")]
    [InlineData("observed_at INTEGER NOT NULL DEFAULT (unixepoch())")]
    public void A_date_function_with_its_time_value_omitted_is_reported(string column)
    {
        var sql = $"CREATE TABLE contract_quotes (contract_id TEXT NOT NULL, {column});";

        Assert.NotEmpty(ClockFunctionsIn(sql));
    }

    /// <summary>
    /// And does not fire on the shapes already in the tree, which is what makes
    /// the check worth more than its synthetic case.
    /// </summary>
    [Fact]
    public void Supplying_the_instant_as_a_parameter_is_not_an_offence()
    {
        const string Sql = """
            INSERT INTO config_rows (key, version, value, set_at, note)
            SELECT $key, COALESCE(MAX(version), 0) + 1, $value, $setAt, $note
            FROM config_rows WHERE key = $key;
            SELECT MAX(set_at) FROM config_rows WHERE key = $key;
            SELECT strftime('%Y', set_at) FROM config_rows WHERE key = $key;
            """;

        Assert.Empty(ClockFunctionsIn(Sql));
    }

    /// <summary>
    /// The two predicates above assert absences, and an absence assertion passes
    /// just as well when the predicate is broken. These probe types are what
    /// prove each one fires, without putting a clock into <c>Core</c> to find
    /// out.
    /// </summary>
    [Fact]
    public void The_predicates_fire_on_something()
    {
        Assert.True(HoldsAClock(typeof(TakesOneByConstructor)));
        Assert.True(HoldsAClock(typeof(TakesOneByField)));
        Assert.True(HoldsAClock(typeof(TakesOneByProperty)));
        Assert.False(HoldsAClock(typeof(TakesAnInstant)));

        Assert.True(ServesASimulatedDate(typeof(TakesADate)));
        Assert.False(ServesASimulatedDate(typeof(TakesAnInstant)));
    }

    private sealed class TakesOneByConstructor(IClock clock)
    {
        public DateTimeOffset When => clock.UtcNow;
    }

    private sealed class TakesOneByField
    {
        private readonly IClock _clock = SystemClock.Instance;

        public DateTimeOffset When => _clock.UtcNow;
    }

    private sealed class TakesOneByProperty
    {
        private IClock Clock => SystemClock.Instance;

        public DateTimeOffset When => Clock.UtcNow;
    }

    private sealed class TakesADate
    {
        public static string Of(DateOnly date) => date.ToString();
    }

    private sealed class TakesAnInstant
    {
        public static string Of(DateTimeOffset instant) => instant.ToString();
    }

    private static IReadOnlyList<string> ClockFunctionsIn(string sql) =>
        [.. SqlClockFunctions
            .Where(function => Regex.IsMatch(
                sql,
                function.Pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            .Select(function => function.Form)];

    private static IReadOnlyList<Type> CoreTypes() =>
        [.. typeof(IClock).Assembly
            .GetTypes()
            .Where(type => type != typeof(IClock) && !type.IsAssignableTo(typeof(IClock)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)];

    private static bool HoldsAClock(Type type) =>
        type.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IClock))
        || type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(field => field.FieldType == typeof(IClock))
        || type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(property => property.PropertyType == typeof(IClock));

    private static bool ServesASimulatedDate(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(DateOnly)
                || parameter.ParameterType == typeof(DateOnly?)));
}
