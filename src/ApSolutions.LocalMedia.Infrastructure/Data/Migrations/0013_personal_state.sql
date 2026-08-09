CREATE TABLE personal_state (
    content_key TEXT NOT NULL PRIMARY KEY,
    title_id TEXT NOT NULL,
    episode_id TEXT NULL,
    is_favorite INTEGER NOT NULL CHECK (is_favorite IN (0, 1)),
    is_watch_later INTEGER NOT NULL CHECK (is_watch_later IN (0, 1)),
    rating INTEGER NULL CHECK (rating IS NULL OR (rating >= 1 AND rating <= 10)),
    updated_utc TEXT NOT NULL,
    CHECK (is_favorite = 1 OR is_watch_later = 1 OR rating IS NOT NULL)
) STRICT;

CREATE INDEX ix_personal_state_title
    ON personal_state (title_id, episode_id);

CREATE INDEX ix_personal_state_favorite
    ON personal_state (title_id)
    WHERE is_favorite = 1 AND episode_id IS NULL;

CREATE INDEX ix_personal_state_watch_later
    ON personal_state (title_id)
    WHERE is_watch_later = 1 AND episode_id IS NULL;

CREATE INDEX ix_personal_state_rated
    ON personal_state (title_id)
    WHERE rating IS NOT NULL AND episode_id IS NULL;
