CREATE TABLE metadata_cache (
    provider TEXT NOT NULL,
    cache_key TEXT NOT NULL,
    language TEXT NOT NULL,
    provider_version INTEGER NOT NULL,
    payload TEXT NOT NULL,
    etag TEXT NULL,
    stored_utc TEXT NOT NULL,
    expires_utc TEXT NOT NULL,
    PRIMARY KEY (provider, cache_key, language, provider_version)
) STRICT;

CREATE INDEX ix_metadata_cache_expiry
ON metadata_cache(expires_utc);
