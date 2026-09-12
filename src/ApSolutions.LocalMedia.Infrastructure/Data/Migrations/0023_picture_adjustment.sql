-- Brightness, contrast and gamma, remembered for whichever scope the person chose (PLY-018, ADR-0012).
--
-- Three columns and not one, because the value a person lands on is three numbers and a single
-- encoded string would have to be parsed by something, and that something would be a second place
-- where the shape of the adjustment is written down. The repository already stores a six-column
-- subtitle style the same way, and reads it back with the same «the first column is NULL, so nobody
-- stored one» rule.
--
-- They are NULL for every row already stored, and that is the correct answer rather than a gap: a
-- NULL means this scope says nothing and the next one answers, while a stored neutral means somebody
-- decided to undo the adjustment here. Writing the neutral into every existing row would turn every
-- silence into that decision, and a global adjustment would then never reach a film anyone had ever
-- opened.
ALTER TABLE playback_preferences ADD COLUMN picture_brightness REAL NULL;

ALTER TABLE playback_preferences ADD COLUMN picture_contrast REAL NULL;

ALTER TABLE playback_preferences ADD COLUMN picture_gamma REAL NULL;
