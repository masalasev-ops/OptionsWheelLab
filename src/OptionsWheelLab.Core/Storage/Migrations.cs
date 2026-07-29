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
            -- the guard holds against any writer and not only ours [D-W26].
            -- Not D-W8: that governs snapshots, which carry observed_at and
            -- correct by appending a new observation. This table carries set_at
            -- and version, and a written version is never altered.
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

        new Migration(
            2,
            "config_rows_set_at_monotonic",
            """
            -- version is MAX + 1 and always increases; set_at is supplied and
            -- was unconstrained. Resolution filters on set_at and then orders
            -- by version, so an out-of-order timestamp makes "in force on date
            -- T" depend on insertion order rather than on time, and the
            -- append-only guards make that permanent.
            --
            -- Equal is allowed: two versions of one key can legitimately share
            -- an instant, and version breaks the tie, which is what the as-of
            -- resolution already does [D-W26].
            --
            -- Per key, because keys are versioned independently.
            CREATE TRIGGER config_rows_set_at_not_earlier
            BEFORE INSERT ON config_rows
            WHEN NEW.set_at < (SELECT MAX(set_at) FROM config_rows WHERE key = NEW.key)
            BEGIN
                SELECT RAISE(ABORT, 'config_rows set_at moves forward: a new version cannot predate the newest version of the same key');
            END;
            """),
    ];
}
