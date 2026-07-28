namespace OptionsWheelLab.Core.Storage;

/// <summary>One schema change, applied once and recorded.</summary>
public sealed record Migration(int Id, string Name, string Sql);

/// <summary>
/// Every migration, in order.
/// </summary>
/// <remarks>
/// Only configuration exists at 0.3. Market data tables are Phase 1.
/// </remarks>
public static class Migrations
{
    public static IReadOnlyList<Migration> All { get; } =
    [
        new Migration(
            1,
            "config_rows",
            """
            CREATE TABLE config_rows (
                key     TEXT    NOT NULL,
                version INTEGER NOT NULL,
                value   TEXT    NOT NULL,
                set_at  TEXT    NOT NULL,
                note    TEXT    NULL,
                PRIMARY KEY (key, version)
            );

            -- Append-only, enforced by the store rather than by convention, so
            -- the guard holds against any writer and not only ours [D-W8].
            CREATE TRIGGER config_rows_no_update
            BEFORE UPDATE ON config_rows
            BEGIN
                SELECT RAISE(ABORT, 'config_rows is append-only: a change inserts version + 1');
            END;

            CREATE TRIGGER config_rows_no_delete
            BEFORE DELETE ON config_rows
            BEGIN
                SELECT RAISE(ABORT, 'config_rows is append-only: rows are never deleted');
            END;
            """),
    ];
}
