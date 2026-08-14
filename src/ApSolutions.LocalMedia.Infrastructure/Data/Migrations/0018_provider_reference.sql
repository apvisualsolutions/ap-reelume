ALTER TABLE catalog_metadata ADD COLUMN provider TEXT NULL;

ALTER TABLE catalog_metadata ADD COLUMN provider_key TEXT NULL;

ALTER TABLE catalog_metadata ADD COLUMN refreshed_utc TEXT NULL;
