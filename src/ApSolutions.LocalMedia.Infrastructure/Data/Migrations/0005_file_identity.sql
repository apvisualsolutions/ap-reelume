CREATE TABLE media_file_identities (
    media_file_id TEXT NOT NULL PRIMARY KEY
        REFERENCES media_files(id) ON DELETE CASCADE,
    volume_id TEXT NULL,
    file_id TEXT NULL,
    fingerprint TEXT NULL,
    CHECK (
        (volume_id IS NOT NULL AND file_id IS NOT NULL)
        OR fingerprint IS NOT NULL
    )
) STRICT;

CREATE UNIQUE INDEX ux_media_file_identities_stable
ON media_file_identities(volume_id, file_id)
WHERE volume_id IS NOT NULL AND file_id IS NOT NULL;

CREATE INDEX ix_media_file_identities_fingerprint
ON media_file_identities(fingerprint)
WHERE fingerprint IS NOT NULL;
