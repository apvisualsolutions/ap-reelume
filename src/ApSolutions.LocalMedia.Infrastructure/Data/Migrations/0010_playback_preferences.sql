CREATE TABLE playback_preferences (
    scope INTEGER NOT NULL,
    scope_key TEXT NOT NULL,
    audio_language TEXT NULL,
    audio_channels INTEGER NULL,
    audio_codec TEXT NULL,
    audio_prefer_external INTEGER NULL,
    subtitle_language TEXT NULL,
    subtitle_channels INTEGER NULL,
    subtitle_codec TEXT NULL,
    subtitle_prefer_external INTEGER NULL,
    subtitles_enabled INTEGER NULL,
    speed_multiplier REAL NULL,
    volume_percent INTEGER NULL,
    audio_output_device_id TEXT NULL,
    subtitle_font_size_percent REAL NULL,
    subtitle_font_family TEXT NULL,
    subtitle_foreground TEXT NULL,
    subtitle_background TEXT NULL,
    subtitle_background_opacity REAL NULL,
    subtitle_outline_thickness REAL NULL,
    updated_at TEXT NOT NULL,
    PRIMARY KEY (scope, scope_key)
);

CREATE INDEX ix_playback_preferences_scope
    ON playback_preferences (scope);
