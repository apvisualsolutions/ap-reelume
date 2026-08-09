CREATE TABLE library_roots (
    id TEXT NOT NULL PRIMARY KEY,
    normalized_path TEXT NOT NULL COLLATE NOCASE,
    kind INTEGER NOT NULL CHECK (kind BETWEEN 0 AND 2),
    availability INTEGER NOT NULL CHECK (availability BETWEEN 0 AND 2),
    scan_policy INTEGER NOT NULL CHECK (scan_policy BETWEEN 1 AND 7)
) STRICT;

CREATE UNIQUE INDEX ix_library_roots_normalized_path
ON library_roots(normalized_path COLLATE NOCASE);
