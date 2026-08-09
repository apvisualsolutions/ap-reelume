CREATE TABLE scanned_titles (
    media_file_id TEXT NOT NULL PRIMARY KEY
        REFERENCES media_files(id) ON DELETE CASCADE,
    display_title TEXT NOT NULL,
    sort_title TEXT NOT NULL COLLATE NOCASE,
    added_utc TEXT NOT NULL
) STRICT;

CREATE INDEX ix_scanned_titles_title
ON scanned_titles(sort_title COLLATE NOCASE, media_file_id);

CREATE VIRTUAL TABLE scanned_catalog_fts USING fts5(
    media_file_id UNINDEXED,
    display_title,
    tokenize = 'unicode61 remove_diacritics 2'
);

CREATE TRIGGER scanned_titles_catalog_fts_insert AFTER INSERT ON scanned_titles BEGIN
    INSERT INTO scanned_catalog_fts (media_file_id, display_title)
    VALUES (new.media_file_id, new.display_title);
END;

CREATE TRIGGER scanned_titles_catalog_fts_update AFTER UPDATE OF display_title ON scanned_titles BEGIN
    UPDATE scanned_catalog_fts
    SET display_title = new.display_title
    WHERE media_file_id = new.media_file_id;
END;

CREATE TRIGGER scanned_titles_catalog_fts_delete AFTER DELETE ON scanned_titles BEGIN
    DELETE FROM scanned_catalog_fts WHERE media_file_id = old.media_file_id;
END;
