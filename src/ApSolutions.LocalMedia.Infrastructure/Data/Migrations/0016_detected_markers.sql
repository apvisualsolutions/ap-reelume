CREATE TABLE detected_markers (
    id TEXT NOT NULL PRIMARY KEY,
    series_id TEXT NOT NULL,
    file_id TEXT NOT NULL,
    kind INTEGER NOT NULL CHECK (kind IN (0, 1, 2)),
    start_ticks INTEGER NOT NULL CHECK (start_ticks >= 0),
    end_ticks INTEGER NOT NULL,
    confidence REAL NOT NULL CHECK (confidence >= 0.0 AND confidence <= 1.0),
    detector_version INTEGER NOT NULL,
    user_corrected INTEGER NOT NULL CHECK (user_corrected IN (0, 1)),
    updated_utc TEXT NOT NULL,
    CHECK (end_ticks > start_ticks)
) STRICT;

CREATE INDEX ix_detected_markers_series
    ON detected_markers (series_id, file_id, kind, start_ticks);

CREATE UNIQUE INDEX ux_detected_markers_file_kind
    ON detected_markers (file_id, kind);
