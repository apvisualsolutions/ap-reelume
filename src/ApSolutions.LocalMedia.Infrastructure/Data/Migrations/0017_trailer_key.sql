ALTER TABLE catalog_metadata ADD COLUMN trailer_key TEXT NULL;

DELETE FROM metadata_cache WHERE provider = 'tmdb';
