CREATE TABLE schema_history (
    version INTEGER NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    applied_utc TEXT NOT NULL,
    checksum TEXT NOT NULL
) STRICT;
