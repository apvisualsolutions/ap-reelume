CREATE TABLE match_candidates (
    candidate_id TEXT PRIMARY KEY NOT NULL,
    media_file_id TEXT NOT NULL,
    stable_key TEXT NOT NULL,
    content_kind INTEGER NOT NULL,
    score REAL NOT NULL CHECK (score >= 0.0 AND score <= 1.0),
    scoring_model_version INTEGER NOT NULL,
    review_state INTEGER NOT NULL,
    signals_json TEXT NOT NULL,
    explanation_codes_json TEXT NOT NULL,
    revision INTEGER NOT NULL DEFAULT 0,
    decision_locked INTEGER NOT NULL DEFAULT 0 CHECK (decision_locked IN (0, 1)),
    FOREIGN KEY (media_file_id) REFERENCES media_files(id) ON DELETE CASCADE,
    UNIQUE (media_file_id, stable_key)
);

CREATE INDEX ix_match_candidates_inbox
    ON match_candidates (review_state, score DESC, stable_key);
