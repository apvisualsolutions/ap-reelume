CREATE TABLE titles (
    id TEXT NOT NULL PRIMARY KEY,
    kind INTEGER NOT NULL CHECK (kind IN (0, 1)),
    primary_title TEXT NOT NULL,
    sort_title TEXT NOT NULL COLLATE NOCASE,
    release_year INTEGER NULL,
    added_utc TEXT NOT NULL,
    last_played_utc TEXT NULL,
    has_progress INTEGER NOT NULL CHECK (has_progress IN (0, 1)),
    is_personal INTEGER NOT NULL CHECK (is_personal IN (0, 1)),
    is_available INTEGER NOT NULL CHECK (is_available IN (0, 1))
) STRICT;

CREATE INDEX ix_titles_title ON titles(sort_title COLLATE NOCASE, id);
CREATE INDEX ix_titles_year ON titles(release_year, id);
CREATE INDEX ix_titles_added ON titles(added_utc, id);
CREATE INDEX ix_titles_last_played ON titles(last_played_utc, id);

CREATE TABLE alternate_titles (
    title_id TEXT NOT NULL REFERENCES titles(id) ON DELETE CASCADE,
    value TEXT NOT NULL,
    PRIMARY KEY (title_id, value)
) STRICT;

CREATE TABLE title_cast (
    title_id TEXT NOT NULL REFERENCES titles(id) ON DELETE CASCADE,
    person_name TEXT NOT NULL,
    PRIMARY KEY (title_id, person_name)
) STRICT;

CREATE TABLE title_genres (
    title_id TEXT NOT NULL REFERENCES titles(id) ON DELETE CASCADE,
    genre TEXT NOT NULL,
    PRIMARY KEY (title_id, genre)
) STRICT;

CREATE TABLE seasons (
    show_id TEXT NOT NULL REFERENCES titles(id) ON DELETE CASCADE,
    season_number INTEGER NOT NULL,
    title TEXT NOT NULL,
    PRIMARY KEY (show_id, season_number)
) STRICT;

CREATE TABLE episodes (
    id TEXT NOT NULL PRIMARY KEY,
    show_id TEXT NOT NULL,
    season_number INTEGER NOT NULL,
    episode_number INTEGER NOT NULL,
    absolute_number INTEGER NULL,
    title TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    is_available INTEGER NOT NULL CHECK (is_available IN (0, 1)),
    FOREIGN KEY (show_id, season_number)
        REFERENCES seasons(show_id, season_number) ON DELETE CASCADE,
    UNIQUE (show_id, season_number, episode_number)
) STRICT;

CREATE VIRTUAL TABLE catalog_fts USING fts5(
    title_id UNINDEXED,
    primary_title,
    alternate_titles,
    cast_names,
    genres,
    tokenize = 'unicode61 remove_diacritics 2'
);

CREATE TRIGGER titles_catalog_fts_insert AFTER INSERT ON titles BEGIN
    INSERT INTO catalog_fts (title_id, primary_title, alternate_titles, cast_names, genres)
    VALUES (new.id, new.primary_title, '', '', '');
END;

CREATE TRIGGER titles_catalog_fts_update AFTER UPDATE OF primary_title ON titles BEGIN
    UPDATE catalog_fts SET primary_title = new.primary_title WHERE title_id = new.id;
END;

CREATE TRIGGER titles_catalog_fts_delete AFTER DELETE ON titles BEGIN
    DELETE FROM catalog_fts WHERE title_id = old.id;
END;

CREATE TRIGGER alternate_titles_catalog_fts_insert AFTER INSERT ON alternate_titles BEGIN
    UPDATE catalog_fts
    SET alternate_titles = COALESCE((
        SELECT group_concat(value, ' ') FROM alternate_titles WHERE title_id = new.title_id
    ), '')
    WHERE title_id = new.title_id;
END;

CREATE TRIGGER alternate_titles_catalog_fts_delete AFTER DELETE ON alternate_titles BEGIN
    UPDATE catalog_fts
    SET alternate_titles = COALESCE((
        SELECT group_concat(value, ' ') FROM alternate_titles WHERE title_id = old.title_id
    ), '')
    WHERE title_id = old.title_id;
END;

CREATE TRIGGER title_cast_catalog_fts_insert AFTER INSERT ON title_cast BEGIN
    UPDATE catalog_fts
    SET cast_names = COALESCE((
        SELECT group_concat(person_name, ' ') FROM title_cast WHERE title_id = new.title_id
    ), '')
    WHERE title_id = new.title_id;
END;

CREATE TRIGGER title_cast_catalog_fts_delete AFTER DELETE ON title_cast BEGIN
    UPDATE catalog_fts
    SET cast_names = COALESCE((
        SELECT group_concat(person_name, ' ') FROM title_cast WHERE title_id = old.title_id
    ), '')
    WHERE title_id = old.title_id;
END;

CREATE TRIGGER title_genres_catalog_fts_insert AFTER INSERT ON title_genres BEGIN
    UPDATE catalog_fts
    SET genres = COALESCE((
        SELECT group_concat(genre, ' ') FROM title_genres WHERE title_id = new.title_id
    ), '')
    WHERE title_id = new.title_id;
END;

CREATE TRIGGER title_genres_catalog_fts_delete AFTER DELETE ON title_genres BEGIN
    UPDATE catalog_fts
    SET genres = COALESCE((
        SELECT group_concat(genre, ' ') FROM title_genres WHERE title_id = old.title_id
    ), '')
    WHERE title_id = old.title_id;
END;
