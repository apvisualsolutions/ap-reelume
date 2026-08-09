CREATE TABLE intro_markers (
    id TEXT NOT NULL PRIMARY KEY,
    series_id TEXT NOT NULL,
    kind INTEGER NOT NULL CHECK (kind IN (0, 1, 2)),
    start_ticks INTEGER NOT NULL CHECK (start_ticks >= 0),
    end_ticks INTEGER NOT NULL,
    origin INTEGER NOT NULL CHECK (origin IN (0, 1)),
    confidence REAL NULL CHECK (confidence IS NULL OR (confidence >= 0.0 AND confidence <= 1.0)),
    user_corrected INTEGER NOT NULL CHECK (user_corrected IN (0, 1)),
    updated_utc TEXT NOT NULL,
    CHECK (end_ticks > start_ticks)
) STRICT;

CREATE INDEX ix_intro_markers_series
    ON intro_markers (series_id, kind, start_ticks);
