-- How much compression noise to take out of the picture before the tone curve, remembered for
-- whichever scope the person chose, beside the three columns of the curve it belongs with.
--
-- NULL for every row already stored, like those three were when they arrived, and the reader
-- turns a NULL here into «off» rather than into «nobody stored one»: a row whose curve columns are
-- filled was a gamma somebody chose, and it has to keep reaching that film.
ALTER TABLE playback_preferences ADD COLUMN picture_denoise REAL NULL;
