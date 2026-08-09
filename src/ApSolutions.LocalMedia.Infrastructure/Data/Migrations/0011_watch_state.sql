CREATE TABLE watch_state (
    content_key TEXT NOT NULL PRIMARY KEY,
    title_id TEXT NOT NULL,
    episode_id TEXT NULL,
    position_ticks INTEGER NOT NULL CHECK (position_ticks >= 0),
    observed_duration_ticks INTEGER NULL
        CHECK (observed_duration_ticks IS NULL OR observed_duration_ticks >= 0),
    source_media_file_id TEXT NOT NULL,
    status INTEGER NOT NULL CHECK (status IN (0, 1, 2)),
    is_manual_override INTEGER NOT NULL CHECK (is_manual_override IN (0, 1)),
    started_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
) STRICT;

CREATE INDEX ix_watch_state_updated
    ON watch_state (updated_utc DESC, content_key);
