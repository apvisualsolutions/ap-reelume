-- The cover somebody picked from their own disk, apart from the provider's (LIB-021, ADR-0009).
--
-- One column held both until now, and that is the defect this closes: choosing a cover overwrote the
-- provider's, and restoring the provider's fields overwrote the choice and orphaned its file in
-- personal-artwork. Apart, a refresh has no field to reach it through.
--
-- NULL for every existing row. A row that stored a picked cover the old way keeps it in poster_path,
-- and the repository moves it on read with PersonalCoverPathPolicy — the one place that knows what a
-- picked cover's name looks like, instead of a second copy of that rule written here in SQL.
ALTER TABLE catalog_metadata ADD COLUMN personal_cover TEXT NULL;
