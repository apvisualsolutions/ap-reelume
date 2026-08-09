CREATE TABLE media_files (
    id TEXT NOT NULL PRIMARY KEY,
    library_root_id TEXT NOT NULL,
    normalized_path TEXT NOT NULL COLLATE NOCASE,
    size_bytes INTEGER NOT NULL CHECK (size_bytes >= 0),
    last_write_utc TEXT NOT NULL,
    duration_ticks INTEGER NULL,
    container TEXT NOT NULL,
    video_codecs TEXT NOT NULL,
    audio_codecs TEXT NOT NULL,
    width INTEGER NULL,
    height INTEGER NULL,
    is_available INTEGER NOT NULL CHECK (is_available IN (0, 1)),
    UNIQUE (library_root_id, normalized_path)
) STRICT;

CREATE INDEX ix_media_files_root_path
ON media_files(library_root_id, normalized_path COLLATE NOCASE);

CREATE TABLE scan_checkpoints (
    library_root_id TEXT NOT NULL PRIMARY KEY,
    resume_after_path TEXT NOT NULL COLLATE NOCASE,
    updated_utc TEXT NOT NULL
) STRICT;
