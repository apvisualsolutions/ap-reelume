-- What the candidate is called, so the tray can name it. Without it the only thing a row could show
-- was its stable key -- "movie:761053" -- which asks somebody to decide about a title they are never
-- told. The provider already answers with the name and the year; nothing but this column was missing.
--
-- Nullable, and read as absent rather than as an empty name: rows written before this migration have
-- no title, and a candidate whose name is unknown falls back to the key rather than to a blank line.
ALTER TABLE match_candidates ADD COLUMN display_title TEXT NULL;
