-- Courses, their lessons, and the one column that declares a root holds courses (CRS-001, ADR-0006).
--
-- `course_depth` on library_roots is the whole of decision 2 and decision 3 in one column: a root
-- holds courses exactly when the column is not NULL, and its value is how many folder levels down a
-- course sits. Two columns — a flag and a depth — could disagree with each other, and a root that
-- says it holds courses while saying nothing about where they are is a root the detection has to
-- guess about, which is what the ADR refuses. NULL for every root already stored, which is the
-- correct answer for all of them: nobody has declared one yet.
--
-- The depth is not guessed because guessing was measured not to work. The candidate rule — a video
-- leaf, the course as the ancestor at distance 0 or 1, sections by a leading number — returned 31
-- courses where a real collection has 12, and its four failure modes are ordinary. The two real
-- roots measured have different depths, so no constant serves both.
ALTER TABLE library_roots ADD COLUMN course_depth INTEGER;

CREATE TABLE courses (
    id TEXT NOT NULL PRIMARY KEY,
    root_id TEXT NOT NULL REFERENCES library_roots(id) ON DELETE CASCADE,
    relative_path TEXT NOT NULL COLLATE NOCASE,
    title TEXT NOT NULL,
    marked_at TEXT NOT NULL,
    last_opened_at TEXT
) STRICT;

-- One folder is one course. Marking the same folder twice is the same course with a newer title,
-- not a second one, and the uniqueness is what lets the marking use case say so with an upsert
-- rather than by reading first and racing itself.
CREATE UNIQUE INDEX ix_courses_root_path
ON courses(root_id, relative_path COLLATE NOCASE);

-- `media_file_id` is the identity of LIB-009 and is what makes progress survive a move or a rename:
-- the lesson is anchored to the file's identity and not to its path. It is nullable and cleared
-- rather than cascaded away, because a lesson whose file went missing is a lesson that is missing,
-- which the surface has to be able to say — deleting the row would make it a lesson that never was.
--
-- `sort_major` and `sort_minor` are what CourseLessonOrderPolicy read out of the name. They are
-- stored rather than recomputed on read so that ordering is one decision taken once, and they are
-- both nullable because 19.2 % of measured lesson names carry no leading number at all; those sort
-- last, alphabetically and stably, by `name`.
--
-- There is no progress column here and there will not be one. Progress is PLY-008's store and the
-- watched threshold is PLY-009's, and a second store would be a second answer to the same question.
CREATE TABLE lessons (
    id TEXT NOT NULL PRIMARY KEY,
    course_id TEXT NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
    media_file_id TEXT REFERENCES media_files(id) ON DELETE SET NULL,
    module TEXT,
    module_sort_major INTEGER,
    module_sort_minor INTEGER,
    sort_major INTEGER,
    sort_minor INTEGER,
    name TEXT NOT NULL,
    title TEXT NOT NULL,
    relative_path TEXT NOT NULL COLLATE NOCASE
) STRICT;

CREATE UNIQUE INDEX ix_lessons_course_path
ON lessons(course_id, relative_path COLLATE NOCASE);

CREATE INDEX ix_lessons_media_file ON lessons(media_file_id);
