CREATE TABLE catalog_metadata (
    title_id TEXT NOT NULL PRIMARY KEY,
    title TEXT NOT NULL,
    original_title TEXT NULL,
    overview TEXT NULL,
    release_year INTEGER NULL CHECK (release_year IS NULL OR (release_year >= 1870 AND release_year <= 2200)),
    genres TEXT NOT NULL DEFAULT '',
    poster_path TEXT NULL,
    backdrop_path TEXT NULL,
    locked_fields TEXT NOT NULL DEFAULT '',
    revision INTEGER NOT NULL CHECK (revision >= 0)
) STRICT;

CREATE TABLE media_version_groups (
    id TEXT NOT NULL PRIMARY KEY,
    content_key TEXT NOT NULL UNIQUE,
    preferred_media_file_id TEXT NULL
) STRICT;

CREATE TABLE media_version_group_members (
    group_id TEXT NOT NULL
        REFERENCES media_version_groups(id) ON DELETE CASCADE,
    media_file_id TEXT NOT NULL,
    path TEXT NOT NULL,
    is_available INTEGER NOT NULL CHECK (is_available IN (0, 1)),
    duration_ticks INTEGER NULL CHECK (duration_ticks IS NULL OR duration_ticks >= 0),
    width INTEGER NULL CHECK (width IS NULL OR width > 0),
    height INTEGER NULL CHECK (height IS NULL OR height > 0),
    is_hdr INTEGER NOT NULL CHECK (is_hdr IN (0, 1)),
    video_codec TEXT NOT NULL,
    size_bytes INTEGER NOT NULL CHECK (size_bytes >= 0),
    ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
    PRIMARY KEY (group_id, media_file_id)
) STRICT;

CREATE INDEX ix_media_version_group_members_order
    ON media_version_group_members (group_id, ordinal);
