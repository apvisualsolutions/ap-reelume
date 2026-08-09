CREATE TABLE episode_media (
    episode_id TEXT NOT NULL PRIMARY KEY REFERENCES episodes(id) ON DELETE CASCADE,
    media_file_id TEXT NOT NULL REFERENCES media_files(id) ON DELETE CASCADE
) STRICT;

CREATE INDEX ix_episode_media_file ON episode_media(media_file_id);
