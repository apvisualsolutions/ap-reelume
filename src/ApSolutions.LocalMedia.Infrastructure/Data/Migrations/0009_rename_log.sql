CREATE TABLE rename_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    plan_id TEXT NOT NULL,
    operation_sequence INTEGER NOT NULL,
    direction INTEGER NOT NULL,
    source_path TEXT NOT NULL,
    destination_path TEXT NOT NULL,
    status INTEGER NOT NULL,
    occurred_at TEXT NOT NULL,
    error TEXT NULL
);

CREATE INDEX ix_rename_log_plan
    ON rename_log (plan_id, id);
