# Changelog

Every notable change to AP Reelume. The Spanish version is at [CHANGELOG.es.md](CHANGELOG.es.md).

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the versioning is
[SemVer](https://semver.org/). The canonical scope record, with status and evidence, is
[FEATURES.md](FEATURES.md).

## [Unreleased] / [Sin publicar]

### Fixed

- **The debt list rises for the first time, 186 to 193, because seven files are BORN below the bar.**
  The rule that it only shrinks was written against degradation — a file that was up and got worse —
  and a new file is not that: there is nothing to bring back. The gate's own error message allows it,
  and nobody had used that path until today, because the tree's 48 views entered the day the list was
  created. What the rule still forbids is untouched: a listed file that degrades fails as before. The
  two sentences in the script saying "the list only shrinks" are corrected in the same change.

  **Two of the seven have a measured ceiling, which is why they enter rather than being chased.** The
  first explains a number that had been in the tree for months: the three `.axaml` measure `100/50`,
  and that pair **is not their debt but what every view measures**. Across the tree's 48, **all have
  exactly one line with branches — the root element's — and always at `1/2`**; `App.axaml` sits at
  `0/2`. It is the branch Avalonia's compiler emits for an `.axaml`, and **no test has ever taken
  it**. Hence **63 of the 69 files measuring exactly that pair are views**.

  The second is the fourth unreachable branch `Domain` has found of the same shape:
  `CourseThreadPolicy` stays at 100/93 because of the **delegate cache** of a `TakeWhile` whose
  lambda captures a variable, so the closure is rebuilt on every call and the cached field is null
  every time. **Measured by doing**: rewritten with a loop, the branch vanishes from the report — and
  the change was reverted, because the loop brings eight branches of its own and leaves the file at
  94, a worse deal than the ceiling.

  **And the deep fix is named rather than taken**: if the gate ignored the **branch** coverage of an
  `.axaml` — not the file, whose lines do measure something — 63 files would leave the list and the
  ratchet would fall to about 123. Changing the central gate so it stops looking at something is what
  this repository calls loosening a gate: it deserves its own batch, not a ride in one that exists to
  unblock `main`.

- **Eight constructors accepted a null and failed later; they now refuse it where it is caused.** All
  eight declared their dependencies as a primary constructor, which has nowhere to put a guard
  without declaring a field, so they took the null and broke at first use of the captured parameter:
  a `NullReferenceException` from inside a method, with no parameter name and no visible connection
  to the composition that caused it. The guard defends against nobody — it is the place where a
  wiring mistake says itself by its own name. Promoted to explicit constructors: `ExecuteRename`,
  `PreviewRename`, `UndoRename`, `UpdateMetadata` and `IntegrityChecker` with one parameter each, and
  `RefreshMetadata`, `ApplyIdentification` and `RefreshStaleMetadata` with five or six.

  **Twenty-two parameters, and the sweep counts them**: Application goes from 127 to **148** guards
  exercised and Infrastructure from 64 to **65** — 325 in total with Presentation's 112, which had
  none. The sum matches the twenty-two exactly, which is what says no guard nothing takes was added
  and none that already existed was lost.

  **And the two closed lists the sweep had left go with them — not deleted, but emptied.** The second
  test in each suite, the one that exists so the list can only shrink, failed naming all eight the
  moment the guards landed, before a line of the tests was touched. That is what separates a debt
  list from an exemption list: **an exemption goes quiet when it stops being needed, and a debt
  complains**. The rule is structural again, with nothing to keep true by hand.

  **What had to be watched was not the conversion but who reads those constructors.**
  `ServiceConsumptionTests` analyses them **as text** to decide which service feeds which, so
  changing a constructor's shape changes exactly what that analyser looks at — and the risk was not
  that it would fail, which shows, but that it would **stop seeing** and pass by silence. All 30 of
  its tests stayed green, its own floor against blindness included. The rename skipped comment lines
  for a measured reason: `RefreshMetadata` carries a sentence where `provider` is an English word,
  and a blind sweep would have written `_provider's` inside it.

  **And what it actually cost was not the conversion: it was three files that FELL.**
  `PreviewRename` went from 100/100 to **100/50**, `UndoRename` to 100/75 and `ExecuteRename` to
  100/83, all three under the bar and on no list. The first explanation was false — "they have no
  tests"; they do — so the CI merge was reproduced here, downloading the run's artefact and merging
  its twenty reports with the same `reportgenerator` the gate uses: the constructor line reads `1/2`
  in **every** report and `1/2` merged.

  Two chained causes, **neither in the code**. A file with **no** branches measures 100 % branch
  coverage by definition, so its first `?? throw` is also its first chance to be half covered: with
  two branches, one uncovered is 50 %. And it stayed half covered because **the two sides of the pair
  are taken in different suites** — the sweep hands the null in `Application.Tests` and the rename
  tests hand the real dependency in `IntegrationTests` — and merged Cobertura **keeps the better
  report for a line rather than their union**. `ReviewInboxViewModel` hit the same wall on
  2026-08-28.

  The fix is not another assertion but the same one in a single place: `Application.Tests` now both
  refuses the null **and** builds all three with something real. The line goes from `1/2` to
  **`2/2`**, and the second run confirmed it with **186 listed and 186 measured under the bar, which
  agree**. Three floors rise — `RefreshMetadata` to 97/95, `UpdateMetadata` to 81/88,
  `IntegrityChecker` to 94/87 — and the ratchet stays at **186**, because a floor that rises takes
  nobody off the list.

- **A library at the top of a disk moves again when it is told it has moved.** `RootRemapPolicy`
  returned the decision as `Remapped` and then `Rewrite` rewrote nothing. `IsUnder` asked whether the
  path starts with the root followed by `\`, and for `D:\` — which keeps its separator on purpose,
  because `D:` on Windows names that drive's current directory rather than its root — that is `D:\\`,
  which no real path begins with. The restore ended by reporting success with every file still
  pointing at the disk the person had just said they no longer use, **and nothing announced it**. The
  same seam from the other side doubled the separator when the destination was a root: `F:\` followed
  by `\shows\episode.mkv` gave `F:\\shows\episode.mkv`. Both sides now join on exactly one separator,
  and both arrive with their test: the measured red read `D:\shows\episode.mkv` where it had to read
  `F:\library\shows\episode.mkv`.

- **Three fields written on every read and read by nothing** — the house defect, this time in the
  data. `Language` on `MetadataSearchResult` and `MetadataDetails` did not hold the language the
  answer came back in but **the one that was asked for**: TMDB serves a title in whatever it has when
  there is no translation, so a reader would have been told the opposite of what the name promises.
  `WatchedTitle.Id` is the third: `Summarize` averages genres, cast, rating and year, and the "this
  has been seen" signal reaches the score from the other side, as
  `RecommendationCandidate.IsWatched`. No read of any of the three existed in `src/`. Two tests go
  with them — tests that asserted a value **existed** rather than that it was true, which is the
  exact shape of a gate that passes by looking at nothing, and one of them carried the asymmetry that
  invalidated it in its own comment.

- **The icon scale, aligned with the prototype context by context.** With the canvas restored the
  class becomes the real size, so for the first time each place can be compared against what the
  prototype draws there. Six of nine already matched; three are corrected: the player chrome drops
  from 20 to **18**, volume and mute **rise** from 16 to 18 — they ran smaller than the prototype,
  unlike every other — and the kind glyph over a poster drops from 14 to **12**. Three new classes,
  `size-12`, `size-15` and `size-18`, each with its `1.6 × N ÷ 24` stroke.

  **The fourth deviation is not corrected, and its price is measured**: a card's play stays at 14
  where the prototype draws 15, because raising it moved the library entry **44 px** down in 6 of the
  36 combinations in `HomeLayoutTests` — 1366 x 768 at 150 scale in Spanish, the tightest the
  application supports — by wrapping a line. The gain was **0.55 px** of ink.

  **And a gate born of a slip in that same sweep**: reverting the play in two views left
  `MovieDetailsView` at the new size, so the same «Reproducir» button read 15 on a film's page and 14
  on the four others. `The_play_of_a_catalogue_action_is_one_size_in_every_view_that_draws_it` keeps
  the five in a closed table and names the one left behind.

  **And a false claim, corrected everywhere it had travelled.** It was said the prototype never uses
  size 22; it does, in the play toggle. The error was **inferring an absence from a pattern**: it
  required a string literal as `icon(n, s)`'s first argument, and **ten calls pass an expression**,
  among them `icon(p.playing && !err ? 'pause' : 'play', 22)`. Corrected in `ELEMENTS`, in the
  evidence and in the note. The new gate —
  `Every_size_class_is_its_own_number_and_one_the_prototype_spends` — reads the prototype's sizes with
  the pattern that does accept expressions, and requires that **each class name is its `Width`** and
  that the number is one the prototype spends.

- **The icons get their 24 by 24 canvas back, and three defects go with it.** Porting them from the
  prototype on 2026-08-24 copied the stroke and **not the `viewBox`**, so each geometry's bounds
  became its own ink. `Stretch="Uniform"` scales by those bounds until the box is full and pins the
  remainder top left, so every icon grew **by a different factor** and drifted off centre by whatever
  was left over.

  Measured by rasterising: the icons came out between **1.12x and 1.74x** larger than the prototype —
  a spread of 0.62 — and up to **4.5 px** off centre, and **nearly all of them measured the same on
  screen** (16.8 px) whatever size the prototype intended for each. The excess now runs **0.90 to
  1.06** and twenty-three of the thirty-one sit at **+0.00**.

  The fix is the `M0 0 M24 24` prefix in front of all 31 strokes: two movetos that **draw nothing** —
  checked by rasterising, because a round cap on a zero length subpath is exactly how a dot appears
  where nothing was drawn — restoring the box to `0,0 24x24`. With the canvas back, the size classes'
  `-2` goes: `size-20` is `Width="20"`, which is literally the prototype's `icon(n, 20)`. That `-2`
  was the excess corrected by eye with a fixed subtraction against factors running 1.12 to 1.74, so
  it could not work.

  **And it fixes a gate whose premise was quietly false**: `TransportGlyphTests` checked the stroke
  with `1.6 · Width / 24`, which **assumes the ink fills the canvas**. It did for none of the 31, and
  the gate passed anyway. Evidence:
  [the canvas the port did not copy](evidence/stable/audit-icon-canvas.md).

### Added

- **Marking a lesson says so out loud, in one live region.** Marking moves the thread, and the
  thread is the point of the card: somebody reading with their eyes sees the chip jump to the next
  row, and somebody reading with a screen reader would get **nothing**, because a glyph that changed
  elsewhere on the page is not an announcement. It goes in as a sentence, in **one** `Border` with
  `LiveSetting="Polite"`, and it is said **after** the card is re-read: the sentence claims the
  thread moved, and it has only moved once the card has been read again. It clears on every load, so
  reopening a course never re-announces last time's mark.

- **Scope statuses moved to what is measured**: `CRS-002`, `CRS-003` and `CRS-005` go to
  `IMPLEMENTED` with their evidence linked; `CRS-001` to `IN_PROGRESS`, because marking a folder has
  no door yet; `CRS-004` stays `DESIGN_APPROVED`. Before the three reach `VERIFIED` they need the
  captures matrix beside the prototype and a test that a lesson's progress **survives the file being
  moved** — today that is guaranteed by construction and not demonstrated.

- **A ninth §4-versus-tree discrepancy, and this one is vocabulary.** `CourseLastOpenedFormat` is
  "Last opened {0} ago", and that `{0}` wants units — "3 days", "2 weeks" — that **the package does
  not carry and the tree does not have**: measured, there is not one relative-time string among the
  711. Writing them would be inventing copy, which is the owner's. The key stays declared and
  unpainted, and the card says progress and what is left, which is what a decision needs. **Twelve
  more keys are unconsumed**, and all twelve belong to the two remaining tranches: four for the
  marking dialog, three for the rail menu, and four for the player panel.

- **And a correction to what this log said earlier.** It said the two consumers that take
  `CatalogTitleKind.Course` on their default arm — `CatalogItemViewModel.KindKey` and
  `LibraryViewModel`'s routing — "close in the tranche that brings the views". **They did not, and
  now the reason is known**: courses live in tables of their own and **nothing writes a course
  title**, so both arms are today **unreachable**. Adding a branch to them would be adding code no
  test can take, which is exactly what this tree avoids. They close the day a course appears in the
  library, and not before.

- **The course card and its lesson row, with the thread inside.** `CourseDetailsView` stacks under
  the grid — the pattern the library and Duplicates already use, list above and detail below — so
  coming back from a course is scrolling up rather than a button somebody has to find. Its title is a
  **level-2** heading: the destination's level 1 belongs to the grid, and two in one destination is
  the defect the settings column already cost once.

  The thread panel sits in a `Grid`'s **fixed 320 px column** rather than stuck to the viewport,
  which is what the design package asks for and what this framework allows: AXAML has no
  `position: sticky`, and the settings column already solved that shape by putting the `ScrollViewer`
  on one column and leaving the other alone.

  `LessonRowView` mirrors the episode row: the same three states, the same **○ ◐ ●** glyphs — shape
  before colour, for anybody who cannot tell the accent from the secondary text — the same partial
  bar underneath, and a mark a person's hand wins with. The glyphs leave the automation tree because
  the row's own name already says the state in words; announced twice it is read twice.

- **Marking a lesson watched writes where PLY-008 already writes.** No new use case: it calls
  `SetWatchStatus` with `CourseProgressKey`'s key, so a lesson's mark is a watch state like any other
  — which is what makes it survive the file being moved.

- **The walk scene exists and presses all six things** with the mouse: mark a folder, open the
  course, mark a lesson, pick up the thread, play a lesson, and continue from the card. **The mark is
  asserted against the store rather than the glyph**, because a glyph would only prove the row
  redrew itself. The walk ratchet **stays at 20**: 238 declared command controls, 213 pressed, and
  nothing new on the pending list.

- **And one that was missing and could not be seen.** `IsCoursesVisible` was not among the properties
  the shell announces on navigation, so the destination existed, the route changed and **the screen
  never drew**. The walk found it by failing to find the button, which is exactly what pressing with
  a mouse is for rather than checking a boolean.

- **Courses is the rail's third destination, and the grid exists.** `CoursesView` with its positive
  empty state, its 280 px cards carrying progress, what is left and the label the state earns —
  "Continue · M2·L06", "Pick up · …", "Finished · open" or "Opens when scanned", which are four
  different offers rather than four ways of saying one thing — and the detection note at the foot.

  The leading action is **marking a folder**, and it lives in the header rather than inside the empty
  box: an offer that disappears the moment it starts working is an offer somebody has to go looking
  for. The cards' own buttons are not accented, because a grid where every card shouts has no leading
  action at all.

- **The icon was converted from the prototype, not drawn.** Its `course` shape is two rounded
  rectangles and a play triangle; both rectangles become the arcs that draw them, as the other ten
  converted ones already did, and the triangle is stroked rather than filled because that is what
  `IconPlay` does with its own. One drawing tradition per rail.

- **Five declared gates move and none is loosened**: the shell's routes and the navigation contract
  go from five destinations to six — asserted **by name and in order** rather than by count, because
  a route that entered the enum where the rail does not draw it would still pass a count — the icon
  inventory takes its own, `LeadingActionTests` gains its row, and the accessibility keyboard walk
  goes from five buttons to six, each pressed and checked against the route it opens.

- **And one that fired and was right.** `ServiceConsumptionTests` refused two new registrations:
  `MarkCoursesInRoot` and, hanging off it, `ICourseRootDeclarationStore`. Nothing resolves them until
  the add-media dialog offers "Course (folder of lessons)", and a service nobody resolves is this
  repository's characteristic defect. **They leave the container** and return with the tranche that
  feeds them, with the reason written where they were.

  The rail's mark-folder command **builds no door of its own**: the shell hands it the one it already
  owns, the add-media dialog's, exactly as it does with the duplicates list's opener. A second door
  onto the same room is one that can drift from the first.

- **A course's thread, and the progress that already existed.** `CourseProgressKey` invents no
  store: it keeps a lesson's position under the key PLY-008 already uses, with the course where a
  title goes and the lesson where an episode goes. Resume, PLY-009's watched threshold, the manual
  mark that wins over it, and PLY-011's countdown all keep working **without knowing courses exist**.
  Inventing a `lesson_progress` table would have been a second answer to "how far in was I", and two
  answers to one question is how they start disagreeing.

  `CourseThreadPolicy` decides where the thread points, and the rule is the one a person would use:
  **the first lesson in order that is not watched**. Not the last one played — that would send
  somebody back to a lesson they finished — and not the furthest reached, which after a jump ahead
  would quietly abandon everything in between. The same policy gives what remains — counting an
  unwatched lesson whole and **only the rest** of one in progress — and "what you watched last",
  which is the last two watched **before** the thread rather than the last two in the course.

- **An empty course is not a finished one**, and it is asked separately for that reason: a folder
  just marked whose walk has not run would draw as complete, congratulating somebody for a course
  nobody has read.

- **A lesson's length is joined, not copied.** The design package asked for it as a column on
  `lessons`; it comes from the file the catalogue already probed, because a copy is a thing that goes
  stale. `CourseLessonReader` answers a card with **one `SELECT`** over the three tables that answer
  it — the lesson, the file that gives it a length, and the watch state — with both `JOIN`s on the
  left: a lesson whose file was never probed has no length yet, and one nobody opened has no state,
  which is exactly what "not started" means.

  **What had to be measured was the key**, because it is composed in two places: in SQL by
  concatenation and in C# from `ContentKey`. An integration test **writes through one and reads
  through the other** against a real database.

- **A lesson's number counts through the course and not through the module.** The prototype writes
  "L06" beside a lesson in module 2, and a per-module number would call two different lessons the
  same thing.

- **Migration `0022` and what writes it, in one change.** `courses` and `lessons`, plus a single new
  column on `library_roots`: `course_depth`. That column is **both of the ADR's decisions at once** —
  a root holds courses exactly when it has a depth, and the value is the level they sit at. Two
  columns, a flag and a depth, could contradict each other; and a root that said "I hold courses"
  without saying where would be precisely the one that forces a guess.

  `lessons` carries no progress and will not: progress is PLY-008's store and the watched threshold
  is PLY-009's, and a second store would be a second answer to one question. What it does carry is
  `media_file_id`, LIB-009's identity — which is why moving or renaming the file keeps the progress —
  **nullable and `ON DELETE SET NULL`** rather than cascading: a lesson whose file has gone is a
  lesson that is missing, and a surface has to be able to say so; dropping the row would make it a
  lesson that never existed.

  `MarkCoursesInRoot` reuses `IMediaFileEnumerator` instead of opening a second way to walk a folder:
  one enumerator is one set of error codes and one place where the disk is touched at all. It reaches
  no network — there is nothing to identify — and copies, moves and renames nothing.

- **Four assertions in `SqliteBootstrapTests` move from 21 to 22**, and with them the name list and
  the table list. The ADR said three and there are four: the count, the maximum, the pre-migration
  backups, and the count after the second pass.

  **The table list also taught something reading cannot**: `lessons` sorts **before** `library_roots`,
  because the query orders under SQLite's binary collation and there `e` precedes `i`. Put in
  read-it-aloud alphabetical order, the test goes red at index 13.

- **Three defects were found by the tests before anybody else**, and none was visible by reading:

  1. `CourseStructurePolicy` normalises paths to `/` and the use case keyed its dictionary with the
     separators exactly as the enumerator handed them over. The map was built with one set of keys
     and read with another, so the first lesson threw `KeyNotFoundException`.
  2. A lesson's module stored the **folder name** (`01 - Módulo uno`) where the card wants the
     **title** (`Módulo uno`), because the number already travels apart in `module_sort_major` and
     "Módulo {0} · {1}" wants them separated.
  3. In `ORDER BY`, **SQLite puts NULLs first**, so a lesson with no leading number would have opened
     every course. The query orders on `sort_major IS NULL` before `sort_major`, and a test pins it.

- **`CatalogTitleKind.Course`, and the two pure policies that decide what a course is and the order
  it is watched in.** `CourseStructurePolicy` reads the courses of a root **at the depth the root
  declares**, and guesses nothing: at that depth the folder is the course, a subfolder holding video
  is a section, anything beneath flattens against it, and a video shallower than that depth belongs
  to no course at all. That a resource folder with no video is not a section comes free from feeding
  it video paths rather than a directory listing, and it is not a detail: of 1955 files measured in a
  real collection only 595 were video.

  `CourseLessonOrderPolicy` orders by the leading number of `NN -`, `NN-`, `NN.` and `NN_`, which
  cover 80.8 % of 595 measured lessons, and **keeps `N.N` as an ordered pair** instead of destroying
  it — today the film name cleaner turns `1.3 Title` into `1 3 Title`, which destroys the order and
  the title in one move. The remaining 19.2 % is left alone: it sorts last, alphabetically and
  stably, which is exactly what puts the zero-padded encoded schemes right. And **a number is read up
  to three digits**, because four are a year: that limit is what keeps this from reproducing the
  false positive the film parser makes over the same collection.

  Both land at **100 % of lines and of branches**, measured off the suite's own Cobertura, which is
  representative here because nothing else exercises them yet.

- **The third kind of title is appended to the END of the enum, and that is not taste.**
  `CatalogTitleKind` is written to SQLite as its ordinal — `(CatalogTitleKind)reader.GetInt32(1)` —
  so putting `Course` where it reads best, beside `Movie` and `Show`, would have renumbered
  `Unidentified` from 2 to 3 and **every unidentified title in any existing database would have come
  back a course**, silently and on the first read. The reason is written into the enum itself, which
  is where somebody will go to reorder it.

  **Two consumers still take the new value on their default arm, and are named here so they are not
  lost**: `CatalogItemViewModel.KindKey` would resolve it to `CatalogKindFile` and `LibraryViewModel`
  would route it to the film card. Neither is reachable yet, because nothing constructs a course
  title, and both close in the tranche that brings the views. A `switch` with a `default` is exactly
  where a new kind goes missing quietly, which is what the ADR warned about.

- **The Courses strings land in both files, and there are 42 of them rather than the 41 the package
  announces.** `Strings.es.axaml` and `Strings.en.axaml` go from **668 to 710 keys each**, in the same
  order and with not one difference between them. The number was counted out of the document instead
  of read off its heading, which is how the mismatch showed: the "course card and its thread" group
  is labelled 15 and enumerates 16, because two of its rows pack a pair of keys into one cell — "mark
  as watched / unmark" and their two notices — and one of the pairs was counted once. A number nobody
  recounts is the one that ends up justifying an incomplete list, so the correction goes in as a
  comment inside both files, where whoever adds the next key reads it again.

  Nothing consumes them yet, and that is deliberate: `PROMPT.md` puts the strings first because
  bilingualism is checked as a pair of files and a new key goes into both or into neither. The
  tranches that follow — model, marking, the four views, and the player panel — are what spend them.

  **And the gate that forbade them is narrowed, not removed.** `ScopeBoundaryTests` carried a row
  banning the words "Curso", "Course", "Leccion" and "Lesson" in any resource key, and with them it
  banned cataloguing a folder of numbered videos already on the disk, which is what this application
  does. What the roadmap sentence protects is the **platform**, so the markers become the words the
  product would have to use if it enrolled somebody, certified something, or kept a record:
  enrolment, certificate, diploma, quiz, streak, training progress, completion percentage, study
  statistics. "Badge" is deliberately left out, because ten existing keys carry it starting with
  `UnavailableBadge`. And in the schema gate, `courses` leaves the list of forbidden tables and the
  four that would really be a platform join it: enrolments, certificates, quizzes, streaks. A table
  that ships cannot be undone.

- **Courses enters the matrix as `CRS-001`…`CRS-005`, and the non-goal that forbade it is narrowed
  rather than deleted.** `ADR-0006` moves to `ACCEPTED`: a course is a third kind of title, a root is
  **declared** to hold courses and **declares the depth** they sit at, and the program guesses
  neither. Guessing was tried and measured not to work: the candidate rule — video leaves, the course
  as the ancestor at distance 0 or 1, sections recognised by a leading number — returned **31 courses
  where there are 12** over a real collection of 595 videos, and its four failure modes are all real.
  With the depth declared, detection is exact by construction and there is no heuristic left to
  maintain.

  The roadmap said "not a course platform. No lessons, no training progress, no certificates", and
  that sentence is **narrowed, not deleted**: everything that motivated it stays out — enrolments,
  certificates, quizzes, streaks, study statistics, percentage of training completed — and what comes
  in is what the application already does with a show. Progress is the progress that exists
  (`PLY-008`, `PLY-009`) and identity is the identity that exists (`LIB-009`): no second store is
  invented.

  The documentation gate goes from **60 to 65** declared identifiers. That number is asserted rather
  than left open for exactly this: a new row in the matrix forces a change to the script, which is
  where somebody notices that the localised documents need it too.

- **The design package catches up, and brings 57 views in 57 files.** The remote project had not been
  read since 2026-08-17 and has since declared the redesign closed in this tree and opened a single
  proposal. In come `design/vistas/` — one file per view, nine lines each, opening the prototype
  already positioned on that view through the `view` prop, so there is one source and no divergence —
  the Courses keys appended to `Cadenas nuevas` with their text in both languages, and `README`,
  `PROMPT` and `github` rewritten for this phase. `Auditoría del inventario` is withdrawn because the
  project withdrew it.

  **The navigable prototype is NOT updated, and that is a limitation of the transport rather than a
  choice**: the design tool caps a read at 256 KiB and the file is larger, so it came back truncated
  at exactly 262144 bytes. Trading a complete August prototype for a new broken one is trading an old
  file for one that does not open, so the one that opens stays.

- **A sweep that hands every constructor a null, and with it 303 guards nothing had ever taken.**
  Ninety of the two hundred and five files short of the coverage bar were short of it for one
  repeated reason: a `?? throw new ArgumentNullException` no test had ever exercised. An untaken
  `throw` is half a branch pair, which is why **six** Application files measured exactly 100 lines
  and 50 branches — the earlier analysis said eight, and counting them says six. `ConstructorGuardSweep` builds each type with a stand-in in every position but one and
  a null in that one, and requires an `ArgumentNullException` **naming that parameter**: **127**
  parameters in Application, **112** in Presentation and **64** in Infrastructure.

  **What that is worth, measured by CI rather than estimated here**: 66 files improve and **nineteen
  reach 96/96 and leave the list**, which comes down from 205 to **186** — the largest single move it
  has had. None enter: nothing degraded. **Four of the six** files stuck at exactly 100/50 are among
  those that leave; `SetPreferredVersion` rises to 100/75 and `RemoveLibraryRoot` stays, because its
  file has branches beyond the guard.

  Three exclusions, all structural rather than a list, because a list is a thing to maintain:
  records, this repository's data carriers, which legitimately validate nothing — 190 of the 319
  reference parameters the first probe measured; exceptions, whose message is nullable by .NET
  convention; and what the compiler wrote, such as the generator's `JsonSerializerContext`. Each
  suite also carries a floor of parameters reached, because reflection **goes quiet rather than red**
  when it stops matching, and that is the failure this repository has measured more often than any
  other.

  **Eight constructors still accept a null and are written down with the reason** — and stop being so
  in the change above, which promotes them: all eight declare
  their dependencies as a primary constructor, which has nowhere to put a guard without a field, so
  they take the null and fail later at first use — a `NullReferenceException` from inside a method
  instead of an `ArgumentNullException` from the composition that caused it. They sit in a closed
  list like `PendingWiring` in `ServiceConsumptionTests` — a debt with a name on it, not an exemption
  — and a second test forces an entry out the moment its guard lands.

  **And one signature that lied, fixed on the way**: `RecommendationItemViewModel` declared its title
  as a non-nullable `string` and treated it as `title ?? string.Empty`. The signature now says what
  the body does.

- **A fourth hook: arming the CI watcher stops being a sentence.** "CI is watched with
  `eng/watch-ci.ps1`, never a hand-written loop" had been in `CLAUDE.md` for batches, and on
  2026-08-30 the owner had to ask for it anyway. `post-push.sh` runs on `PostToolUse` over
  `Bash|PowerShell` and, after any `git push`, writes the monitor command to stderr and exits 2 —
  **with the SHA already resolved**, ready to copy.

  **And the complaint was right even though the monitor was alive**, which is what had to be measured
  before answering: `TaskOutput` reported it `running` with **0 KB of output**, because the script
  beats every 30 minutes and the push was younger than that. **Running and silent looks exactly like
  not existing** — the same trap this repository hunts in its own gates, seen from outside this time.
  Measured by pipe with three cases, one that must sound and two that must stay quiet, then fired for
  real.

  **And then it fired on its own commit**, which is what taught it to be written properly: it searched
  for the bare string, and the message — a heredoc — spoke of "after any git push". It now **strips
  heredocs first** and requires `git push` in **command position**. Ten cases by pipe: four that
  sound, six that stay quiet, including that heredoc and `git pushd`. A warning that fires when it
  should not teaches you to ignore it, which is worse than not warning.

- **The `Domain` layer reaches the coverage bar, and two files cannot: their ceiling is measured.**
  The nine that sat below 96/96 held **15 branches and 4 lines** between them. Twelve branches and all
  four lines close, and seven files reach the bar — `RenameOperation`, `RootRemapPolicy`,
  `MetadataMergePolicy`, `RecommendationPolicy`, `RecommendationModels`, `IMetadataProvider` and
  `MediaNameParser`. 24 new tests, every one of them on a guard that existed and that nobody had ever
  called on its false side: that a rename whose destination equals its own source is **not** the
  exception allowed to overwrite an existing file, that a plan with no operations does not execute
  however few conflicts it has, that a conflict between two roots leaves a third one alone, that a
  remote answer without a year does not erase the year already stored, and that two tastes of the
  same size with different keys are not the same taste.

  **The three branches left cannot be taken by any input, and that was read rather than assumed.**
  `SegmentDetectionPolicy`'s is a `dup; brtrue.s` at IL offset 139 — the `GroupBy` lambda's delegate
  cache, held on the closure class the method allocates **once per call**, so the field is born null
  and the jump is never taken. `MatchModels`'s would need `GetRelativePath` to return an empty string,
  and that path **throws** first. `MediaNameParser`'s would need a negative season, and all three
  patterns capture `\d{1,3}`. All three are written into the test someone will look at next time, not
  only into the evidence.

  **The ratchet goes from 212 to 205**, and CI said that number, not this machine: the list was copied
  from the `coverage-debt` artefact of the run that measured it. Its diff against the previous one
  moves **eight rows and no others** — seven leaving and `MatchModels` rising 80 to 90 — which is how
  you see that nothing else in the tree degraded. Evidence in
  [audit-domain-coverage.md](evidence/stable/audit-domain-coverage.md), with the starting floors
  reproduced **exactly** from a green run's artefact, which is what says the instrument measures what
  the gate measures.

- **The repository gets its own Claude Code configuration, and with it rule 0 stops depending on one
  machine.** `.mcp.json` declares the Avalonia MCP — `https://docs-mcp.avaloniaui.net/mcp`, no
  credentials or local paths — which until today lived in the personal config of whoever was working:
  the newest rule in `CLAUDE.md` required consulting it and the server was not in the tree.

  With it, three **process gates** that were sentences until now: a hook refuses to write
  `eng/coverage-debt.txt` and `eng/walk-pending.txt` — which `CLAUDE.md` declares CI-produced and
  which nothing prevented editing — another refuses a source file without its SPDX header **before**
  it is written, and the third warns when an `.es.md` is touched and its `.en.md` pair stays as it is
  in `HEAD`. Two skills, `/cerrar-tanda` and `/medir-pixeles`, and two agents, `gate-auditor` — which
  hunts for gates that pass without measuring anything — and `prototype-fidelity`.

  **All six hook cases were tested one by one before being written**, and the pipe-test caught a
  defect no amount of reading would have found: `jq` on Windows emits **CRLF**, so the path arrived
  ending in `
`, no pattern matched, and the hook would have been silent for ever — which is the
  shape this repository's defect already has.

  **And the third was measured before it was believed, which is how it turned out to fire always.**
  It compared `mtime`, and since each write is a separate call the `.es.md` always ends up newer than
  its pair: it fired **when both languages had been touched** too, which is exactly the well-done
  work. It now asks git — the `.es.md` differs from `HEAD` and the `.en.md` does not — tested with
  **ten cases**, four of them among those it must let through.

  **And it only warned when the file was rewritten whole.** Its matcher was `Write` alone, so every
  edit stayed silent — and `Edit` is exactly the tool used to touch an `.es.md` that already exists,
  because `Write` rewrites the entire file. The guard covered the least travelled path. Measured with
  the same `Edit` before and after widening the matcher to `Edit|Write|MultiEdit`: silent first,
  warning afterwards, and **live**, with no session reload. The SPDX one stays on `Write`, and there
  it is no oversight: it reads `tool_input.content`, which an `Edit` does not carry whole.
  `MultiEdit` is declared in the matcher but **not measured**.

  **The silence had to be measured another way, because inside the application it cannot be told
  apart from not running at all**: a hook leaves a trace in the session log **only when it produces
  output**. With both languages touched, the literal command from `settings.json` stays quiet through
  a pipe — with the case that must fire beside it, which is the only reason the quiet one counts.

  **And what the warning did not do, measured the same day: it did not reach the screen.** With the
  owner in front of the PC, both warnings provoked on purpose went by unseen, and a `systemMessage`
  does not enter the writing agent's context either. They were emitted for nobody: they ran, they
  were right, and their output died in the session log.

  **So the channel changed.** Both `PostToolUse` warnings now write to stderr and exit 2, which does
  reach the agent writing the file — measured with the known case beside the unknown one, then
  through a pipe with **seven cases, four of them among those it must let through**. And since it
  arrives labelled an error after a write that did succeed, all three messages **open by saying the
  write did not fail** — without that, the warning reads as a failure and the same write gets retried.

  **Its price was measured and then paid down.** The harness prints the whole command twice ahead of
  the text, so inline it cost **2,712 characters of context per warning**. The command now lives in
  `.claude/hooks/post-write.sh` and `settings.json` only calls it: **488 characters**, 82% less, read
  off the log rather than calculated — the arithmetic said 528. The other two hooks stay inline
  because they are short and they **deny**, so their text is never printed twice.

  **And the same file now turns this repository's claude.ai cloud connectors off.** Measured from a
  session's own context: they cost **298.8k tokens, 30% of the window, and 212.9k of that was a
  single connector** — 102 advertising tools this project never calls.
  `disableClaudeAiConnectors` is won by any source that sets it true, so the project opts out
  without touching anyone's personal configuration; `avalonia-docs` is unaffected because it comes
  from the local `.mcp.json`. The gate applies at startup, so it is verified with `/context` in the
  next session rather than the one that writes it.

  **And three claims this tree made about its own tooling were false.** `CLAUDE.md` said the walk
  ratchet stood at **0 and would not rise**; it stands at **20** and rose on 2026-08-25, for the
  harness rather than the application. `eng/walk-pending.txt` opened by saying it was **empty** while
  listing twenty entries further down — an old paragraph left in place beside the new one. And the
  hook that refuses to write it gave the reason **"CI produces it"**: `ci.yml` publishes
  `eng/coverage-debt.txt` and nothing else, so the refusal sent anyone with a legitimate change
  waiting for an artefact that never arrives. The two refusals now carry separate reasons, and the
  walk one says what raising the ratchet actually costs. Measured in passing: a `deny` arrives with
  its reason **alone**, without the command — unlike a `PostToolUse` warning, which is why those two
  stay inline.

  **And the two local servers that failed on every start are denied for this project.** `gbrain`
  (`REQUEST_TIMEOUT`) and `MCP_DOCKER` (`CONNECTION_CLOSED`) go into `deniedMcpServers` rather than
  being deleted from the machine: they belong to whoever is programming and are used outside this
  repository. This one was measurable on the spot, unlike the connector key — `claude mcp list`
  showed three servers before and shows only `avalonia-docs` after.

- **The mini player's chrome is the prototype's composition: a bar of progress, a title, and a
  clock.** The band was five buttons and nothing else, so a floating window said neither **what** was
  playing nor **how far in** it was — which is most of what a picture-in-picture is for. It now
  carries the prototype's three pixel bar across its width, the title on the left with
  `position / duration · speed` under it, and the five on the right in the order the prototype draws:
  back, play, forward, expand, close.

  **The bar's track never leaves and only the fill arrives.** The window answers a drag by putting
  16:9 back on the picture and adding the chrome's height on top, and that handler only runs on a
  drag: a bar that appeared when the duration arrived would move the picture under a window nobody
  touched. The fill does wait, for the reason `DurationSeconds` carries — it answers 1 until the
  length is known, so fifty-two minutes against that maximum is clamped and paints a **full** bar
  over a film that has barely started.

  The clock is **one** string from the model rather than three bindings in a row: the separators are
  punctuation no dictionary holds, and a row of three with an empty middle leaves the punctuation
  stranded, `0:12 /  · 1×`. Evidence:
  [the mini player's chrome](evidence/stable/audit-mini-player-band.md).

- **Both cards' headers draw the real poster, with the generated art beneath them.** `PosterPath`
  was produced, merged and persisted from the beginning and **no surface read it**: a value with no
  reader, which is this repository's characteristic defect seen from the far end. It closes
  **ART-A01** (2026-08-09), which had retired `ArtworkCache`'s registration rather than leave it
  silent, and it closes it in the order that same entry wrote down.

  **A card never opens a connection.** The port has two members and they are asymmetric on purpose:
  finding only ever looks at the disk, and everything that reaches the network is behind fetching,
  which is called once and from the identification — the one moment somebody has already consented to
  talk to the provider. Measured: finding before anything is fetched answers nothing at **0
  requests**, after it answers the file at **1**, and a different address for the same title still
  answers nothing at **1**.

  **A TMDB path is untrusted input**, so `PosterAddressPolicy` checks **before** it composes, for the
  reason the trailer's policy does: composing first leaves a malformed address in existence and from
  then on every reader has to remember to distrust it. It refuses a second slash — which would climb
  out of the size segment — a `..`, a whole address of somebody else's, a scheme, a query, a fragment
  and percent encoding.

  One size and not two — `w780`, and the cession is written down — one download, and **one decode**
  across all four surfaces: the prototype raises the show's poster at 136×204 against the same bled
  wall the film uses, so it is one chain and two views. And the picture cache is **bounded**: a
  decoded poster is about 3.5 MB in memory whatever the file weighs, so an unbounded dictionary
  would put a third of a gigabyte on somebody browsing a hundred films. And an unidentified library does not change: the generated art is what it
  already showed, it stays underneath, and the initials leave only when there is a picture to answer
  for them.

### Changed

- **`ButtonOpticalCentreTests` is retired and `ButtonPixelCentreTests` replaces it, rasterising.**
  This is not loosening a gate: its method had a demonstrable flaw — it computed the foot of the ink
  assuming a descender **always**, and over «Guardar el informe», which has none, it answered 2.43 px
  of separation where rasterising measures 0.0. All three of its assertions — the word centred in the
  button, the icon centred in the button, and the two on one middle — are in the new gate, now in
  pixels and with the word as a **parameter**: the middle of the ink is a property of the string and
  not of the font, ranging from +0.62 («Guardar el informe») to +3.82 («ppp») with the descender.

- **`CLAUDE.md` gains an unbreakable rule 0: the Avalonia MCP before anything.** It is consulted
  before writing AXAML or asserting how a control behaves, with its bill written down: one false
  hypothesis chased to the end — that the renderer snapped the baseline to the pixel grid, which the
  measurement refutes — and six compile rounds guessing at the API. And its corollary, the day's
  finding: **measuring layout is not measuring what is seen**.

- **The test asserting the mini's five fit on one line stops supposing the other language.** It
  pinned `es-ES`, and those five already folded into three rows inside 480×270 over one translated
  word; the language is a parameter now, as in both width gates since 2026-08-26. A second one joins
  it, asserting that **nothing on the band is drawn past the 320** the window allows —
  `ViewOverflowTests` measures at 900, which is the main window's minimum and the one width at which
  this view cannot fail. Measured at 320 in both languages: the row of five takes 252×44 in **one**
  row and the title and clock get 36 px.

- **The metadata editor and safe renaming are a surface of their own, not a panel under the library.**
  They were a `TabControl` at the bottom of the library's own scroll, so opening either tool put a
  panel **under a whole grid of cards** — far enough down that the walk needed a 2,000 px window to
  reach it at all. It is now the page the prototype draws: "Back · Library" on top, a two-line header
  with the card's title at display size, **two pills**, and the tool below.

  **It is not a sixth rail destination, and that is measured**: the five approved ones are asserted by
  name and the walk reaches each of them **by its rail button**, so a sixth enum value would have
  broken the first assertion and left the walk navigating to a place with no door. The page **covers**
  the library's slot, the way a session covers whatever route is underneath it.

  **And the walk found a dead end the same day.** With the page covering the card, "Preview rename"
  was no longer on screen: whoever opened the metadata editor had no way left to reach the other tool.
  So both pills **open** as well as select, which is what the prototype does — and both are always
  drawn.

- **The mini player is a real floating window: no frame, dragged by the picture, kept in shape, and
  it remembers.** It was an ordinary Windows window with its title bar sitting above the video. It
  now opens without decorations (`WindowDecorations.None`), moves when the picture is dragged,
  resizes from **any of its eight edges**, and every resize brings it back to the prototype's
  **16:9** — which belongs to the picture, with the chrome's height added on top, not to the window.
  With no frame left, a one-pixel border is what says where it ends.

  **And it now remembers where it was left, between sessions.** `PlayerWindowCoordinator.Remember`
  had existed since 2026-08-19 and **nothing called it**: it wrote to a dictionary that only its own
  tests ever read, which is this repository's characteristic defect wearing the shape of a
  coordinator. The placement is written when the window closes — not while it moves: a drag raises an
  event per frame and this goes to a file — and read at the next launch. A placement that no longer
  lands on any screen is dropped when it would be used rather than when it was stored: with no title
  bar, a window at the coordinates of an unplugged monitor could not be brought back.

  **What decided how it drags was a measurement and not an argument.** The first draft let a gesture
  through that another control had already handled, on the assumption that a button marks its own
  press: **it does not** — Avalonia marks the *release*, which is where its click is — so that guard
  protected nothing and all five chrome controls would have dragged the window instead of working.
  What decides is **where** the press lands: the picture drags, and the strip the chrome sits in
  does not.

- **The speed menu is the prototype's drop-down and no longer eleven bare numbers.** It was a
  `MenuFlyout` of ten numbers with an eleventh row that reset: a thing that is not a speed, inside a
  list of speeds, hidden behind the click that opens that list. It is now the pill the prototype
  draws — the word in small type, the multiplier in semi-bold, and the caret that turns over when the
  panel opens — its **nine** steps carry a mark, a name and a note (`● Normal · 1×`, `2× · faster`),
  it opens **upward** because the transport is the player's bottom edge, and «Back to 1×» is a button
  beside it that exists only while there is something to come back from.

  It is a `ComboBox` and not a button with a flyout, and that is the walk rather than taste: **nothing
  inside a `Flyout` can be reached by the harness** — all twenty entries of `eng/walk-pending.txt` are
  exactly that, flyout children, and that ratchet does not go up — while a `ComboBox` is pressed and
  asserted on `IsDropDownOpen` the way the library's two filters already are. So the menu gains a
  shape and the ledger gains **one** identity, the reset, pressed in the same commit.

  And the step that did not belong is gone: the prototype's list holds nine, and this one carried a
  `1.75×` nothing ever drew.

- **`AccentPalette`'s lightness walk ends at the end of the scale rather than behind it**, taking the
  file from 99/93 to **100/100**. It had a fallback `return` — "if the loop runs out, black or white" —
  that **no predicate in this file could reach**, along with the two arms of the bounds check in front
  of it. And it is not luck that it cannot be reached: `EqualContrastLuminance` is the luminance where
  black and white contrast equally, and that contrast is **4.58:1**, above the 4.5 the strictest
  predicate wants — so one of the two ends always accepts, whichever way the walk goes.

  **The new coverage gate surfaced it, on a change from this very batch**: removing the focus-ring
  nudge thinned the file just enough for its long-standing hole to weigh above the bar, and since the
  file was on no list, **before today nobody would have seen it**.

- **A picked accent that lands exactly on the focus ring is now kept as picked.** It used to be nudged
  one step of the lightness walk, which returned `#00599A` for a ring of `#005A9C`: a different colour
  by the byte and the same colour to a person. The open question was whether one step was enough or a
  ratio was needed, and the answer is **neither**, because what it was protecting is already protected
  by geometry: the focus adorner is **two concentric rings in two colours** held at 3:1 against each
  other — "two rings the same colour are one ring" — so an accent landing on the outer ring leaves the
  inner one drawing the shape. Demanding a ratio would have been worse than useless: it turns "I chose
  this blue" into "here is a different blue" and buys nothing the geometry does not already give. What
  is still watched is the case everybody sees: the four dictionaries pick their accent and their ring
  by hand, and `ContrastTokenTests` refuses a theme that paints them alike.

### Fixed

- **Fixing the buttons introduced four defects of its own, and a review found them.** All four are
  closed:

  1. **The new selector reached seven icon-only buttons.** `Button > Panel` moved the two transport
     buttons that stack alternating icons by 1 px, and **the rail's five destinations** — worse
     there, because `navigation-destination` sets `VerticalContentAlignment="Stretch"`: the margin
     does not shift them, it **shrinks** them. The selection wash stopped filling its button and the
     accent bar went from 26 to 25 px. Measured: **no `Panel` inside a button carries text and every
     `Grid` does**, so the fix is dropping `Panel` from the selector. It had been there as
     `Button > Panel > :is(TextBlock)`, where it **never reached anything**; moving the setter onto
     the container is what woke it up to do harm.
  2. **`ButtonInkTests` had gone blind.** Its tolerance is 1.5 and the constant dropped to 1.0, so
     the band `[-0.5, 2.5]` **admitted zero**: deleting the setter outright left it green. At five,
     1.5 still refused a centred box. The tolerance is 0.5 now.
  3. **`eng/watch-ci.ps1` could go silent for ever**, the one thing that script exists to prevent.
     Its unreadable-response counter was reset on every successful `gh` round, so it went from 0 to 1
     for ever without reaching the limit, and the `continue`s jumped over the timeout ceiling. It is
     reached by a `gh` that exits 0 and prints an update notice on stdout. Verified with a fake
     `gh`: it now exits with `UNREADABLE RESPONSE`.
  4. **The five was still alive in the dropdown.** `ComboBox.filter-pill` writes its margin by hand
     in two places, under a comment saying "the number is the buttons' own" — and the buttons had
     moved to one. It left every dropdown's label and value 2 px high: the overcorrection just taken
     off the buttons, reintroduced by half.

- **`docs/design/ELEMENTS.en.md` said three false things about vertical alignment, and the section
  was rewritten entire.** It said the compensation was **5 px**, derived from a **2.43 px** asymmetry
  computed from the font's metrics, and that a margin on the `TextBlock` moved "only the word".
  Rasterising refutes all three: the error on screen is **1 px**, the five moved the word by
  **three**, and a margin on the label **grows the panel it sits in** and drags the icon with it —
  the premise that left 53 buttons with their two pieces 2 px apart.

  It matters more than it looks: `ELEMENTS` is **the document that takes precedence** over the
  `.axaml`, so a discrepancy there is not a typo but a wrong instruction for whoever comes next. The
  section keeps what it used to say, and why it was false, at the end — the error teaches more than
  the correction.

- **The buttons drew their icon and their word 2 px apart, with two gates green over it.** Measured
  by rasterising a real button with `CaptureRenderedFrame()`: the label's optical compensation was
  **5 px** and the error on screen was **1**, so it moved the word by three — from 1 px low to 2 px
  high. And it moved the wrong thing: a margin on the label grows the panel it sits in, so across the
  **53 buttons** carrying an icon beside a word the icon moved too, and the icon is geometry and was
  already centred to the pixel.

  The five came from the font's metrics — a 2.43 px asymmetry between ascent and descent — and that
  number **is not the screen's**. The compensation is now **1 px on the button's content** rather
  than on the label: it moves everything the button draws by the same amount and cannot separate two
  things that belong together. Measured after: the icon, the word and the button's middle land on the
  **same pixel row** for «Guardar», «Reproducir», «Añadir medios…» and «Save the report» — with a
  descender and without, in both languages. Evidence:
  [the pixel against the box](evidence/stable/audit-button-pixel-centre.md).

- **In both light themes the mini player's chrome was invisible.** The band painted
  `ShellSurfaceBrush` and its buttons take `PlayerTextBrush` from the `player-chrome` class, and
  those two are the same colour: measured on the mounted view, **1.02:1** in light (`#F8FAFC` on
  `#FBFCFE`) and **1.00:1** in high contrast light, white on white. Five buttons with nothing visible
  on them.

  **No gate could see it, and the reason matters**: every contrast gate reads the four dictionaries,
  and a dictionary is consistent with itself — `ShellSurfaceBrush` is right as the shell's ground and
  `PlayerTextBrush` is right as the player's ink. What was wrong was the **pairing**, and a pairing
  exists only in a view. The correction is the prototype's, which paints the whole mini on `#0B0D10`,
  and `MiniPlayerBandTests` measures the pairing in all four themes: 3:1 for the glyphs and 4.5:1 for
  the two lines of text.

- **Home did not scroll, and on the smallest window 83 px of it were lost off the bottom.** It was the
  one destination mounted in a bare `ContentControl` while Library, Review, Duplicates and Settings
  each had a `ScrollViewer` of their own. With `MinHeight` at 600 and `HomeView` asking for **683**,
  the end of Home was content nobody could reach.

  **This is the "sections cut off by the width" finding, and it was not the width.** The horizontal
  axis had been ruled out and measured — none of the 48 views exceeds the 836 px the shell gives them
  — leaving four hypotheses; this was the third. There is a gate on it now: **a view taller than the
  window has to be inside something that scrolls**, read off the shell's own tree rather than off a
  list written by hand.

  Both width gates now measure **in both languages** as well. They pinned `es-ES` as a constant, so
  the other language was a guess — and the mini player's chrome had already folded into three rows
  once because an English word was longer.

- **A scanned film's year reached the library and not Home.** Both surfaces read the same projection
  with **the same query written twice** — each carries its own copy of "hide a file that is already an
  episode" — and the year column, which arrived with migration 0021, was put into one and not the
  other. There is now an assertion tying the two: the same title and the same year on both, over one
  scan.

- **The overflow gate measured each view alone at 900 px**, which is wider than any of them ever gets:
  inside the shell the rail takes 64. That was **the first of the two limitations that gate declares
  about itself**, and it is the exact shape of «secciones cortadas por el ancho» from the original
  brief. They are now measured against the real room as well — **836 px, taken off the shell rather
  than written down** — the player included, because the prototype leaves the rail on screen for an
  embedded session.

  **And the result is that the defect is not on this axis**: not one of the 48 views exceeds it. The
  absence is measured rather than assumed, which is what was missing before it could be looked for
  somewhere else.

- **The coverage debt list only watched what was already on it.** A file that reached 96/96 and then
  got worse was watched by nobody: the new-file gate cannot see it — newness is decided against the
  base ref and the file shipped months ago — and the debt loop only walks names already written down.
  **Measured, not feared**: three consecutive CI runs measured **216** files below the bar while
  `eng/coverage-debt.txt` named **212**, and the four it did not name had been degraded for days. The
  worst, `PlayerView.axaml.cs` at **65/41**, is the lowest pair in the tree.

  The list is now checked for being **complete** and not merely accurate: anything under the bar that
  is not on it is named by the gate, which is the only way a degradation announces itself. Off CI it
  reports and does not block, exactly like the floors and for the same reason: seven files measure
  differently on a hosted runner.

  **All four were closed rather than written down**, so the ratchet stays at 212 and the two counts
  are the same number:

  - `PlayerView.axaml.cs` **65/41 → the bar**. Two views mounted it and neither gave it a data
    context, so **neither of its two handlers had ever run** — not the tunnelling key handler (the
    whole fix for «la barra espaciadora pone pantalla completa») nor the double click. Both shipped on
    the strength of a manual look.
  - `PlayerViewModel.cs` **98/91 → the bar**, by removing three guards nothing could take: two
    `as AsyncRelayCommand` casts over properties assigned in exactly one place, and two null checks
    repeating what their command's `CanExecute` already requires — and `AsyncRelayCommand.Execute`
    asks before it runs anything, which is a promise written into that class.
  - `PlaybackPreference.cs` **98/92 → the bar**. `SubtitleStyle.IsColour` is public because the
    swatches ask before they offer, and its "there is nothing here" arm had been taken by nobody.
  - `DisabledOutline.cs` **100/87 → the bar**. The fallback corner radius exists for a `Control` that
    is not a `TemplatedControl`, and all ten types the style names are templated: written,
    documented, and measured by nobody.

- **An unidentified film was called by its file name, verbatim.** «El Faro de Piedra 2019» on the
  card, with the year inside the title and the year column empty beside it — and before that,
  «Neon.Sobre.el.Rio.2022.2160p». Meanwhile `MediaNameParser` has been taking that same name apart
  since the first week, and **three** use cases already call it: the review inbox matches its output
  against a provider, version grouping compares copies with it, and since 2026-08-25 the series
  grouping decides episodes with it. Two readings of one file name, and the one on the screen was the
  raw one.

  There is now a pure policy (`ScannedTitlePolicy`) that says what the card is called — the clean
  title, and the file name when cleaning leaves nothing, because «2019.mkv» is a year with no title
  and a blank card is worse than a messy one — and a use case that is a sibling of the other two
  (`NameScannedTitles`), run after every scan. Migration **0021** adds the year column to the
  projection: cleaning the title with nowhere to put the year would have taken the year off the
  screen, so the two travel together.

  **An already-catalogued library renames itself on the next scan**, without re-probing a single file:
  the pass walks the whole scan summary, `Unchanged` included, which is exactly what a file whose size
  and date have not moved always is. That is the state everything already catalogued is in on the day
  this ships.

  The assertion in `ScanSeriesGroupingTests` that read «El Faro de Piedra 2019» was written on purpose
  so that changing it would be a decision rather than a surprise. This is that decision.

- **`PlaybackControlPolicy.SpeedSteps` was read by nobody.** The defect of the house, number sixteen:
  the menu wrote its own ten numbers into its own markup and a test **read that file as text** to
  compare the two, so the policy decided nothing while the comment above it claimed the keyboard
  walked it. Nothing walked it. The menu is built from it now, and the test asks the model instead of
  a `Regex` over an `.axaml` — which is how three suites in this tree have already gone blind: a file
  that stops matching the pattern returns an empty list, and an empty list compared against another
  empty one passes.

- **The full-screen button drew the entering arrows while already full screen.** The prototype writes
  `icon(mode === 'fullscreen' ? 'exitfull' : 'full')`, and `IconExitFullscreen` had been in the
  dictionary the whole time, drawn only by the mini window's restore. It is the same defect the mute
  button was found with in August and fixed for: a control that says the same thing whatever it has
  done.

  Along the way, the table that pins which glyph each button carries listed **eleven of thirteen**:
  the two mode buttons had been on the bar for three days and were not on it, and the one that was
  wrong was one of the two that were missing. A picture on a button nothing names is a picture nothing
  checks.

- **`HardwareAccelerationFallback.Reset` had no caller.** It claimed to be "for when a new engine is
  created", and a new engine builds a new one of these, so neither `src/` nor any test had ever
  invoked it: this house's defect wearing a small hat, again. The coverage gate is what saw it — one
  line of a small file is a whole percentage point — and it has been deleted.

- **The icons are the prototype's, and now a gate says so rather than a comment.**
  `PrototypeIconTests` reads `design/AP Reelume.dc.html`, pulls the map out of its `icon(n, s)`
  function, and compares **character for character** the sixteen shapes that are made of paths alone.
  The ones carrying a rectangle or a circle became the arcs that draw them, so there is no string to
  compare and they are **named** as conversions rather than quietly skipped — a skip nobody writes
  down is how a gate becomes blind instead of red. And the set adds up: every geometry the theme
  declares is either copied, converted, or declared as this application's own with its reason.

- **`HasPlayerPanels` carried a branch nothing could take.** The subtitle panel term is
  `Player?.Tracks is not null`, and so is half of `HasAudioPanel`, which is evaluated first: by the
  time the chain reached the subtitle term, the track list was already known to be absent and the
  term could only ever answer no. The coverage gate is what saw it. It has been deleted — this
  tree's rule for a branch like that is to make it reachable or remove it, never to write it an
  impossible test — and the four alternatives left are now asked for one at a time.

- **A folder of episodes is a series now.** It is this repository's characteristic defect in its
  largest form: `titles`, `seasons`, `episodes` and `episode_media` have existed since migration
  0004, the series card has been drawn and routed to since it was written, and **nothing had ever
  written a row into any of the four**. `MediaNameParser` has read `S01E01`, `1x04`, «Temporada 1
  Episodio 2» and `Cap.803` since it was written, and nobody ever asked it where the episode
  belonged — so two shows with eleven seasons between them arrived as ninety-nine loose cards. There
  is now a pure policy (`LocalSeriesPolicy`) that says which folder names the series — the folder,
  never the file — and a use case (`GroupScannedEpisodes`) that runs after every scan, exactly as
  version grouping does, and writes the show, its seasons, its episodes and the file behind each
  one. Measured end to end against the real layout: ninety-nine files go in and **three cards come
  out** — the two shows and the film that was in the same root — with 72 and 27 episodes counted,
  their eight and three seasons, and a file behind every episode. The identifiers are derived from
  the series key and the numbers, so a second scan updates instead of duplicating; the key carries
  the root, so a backup does not fold into the original; and a film is still a film.

- **Continue asked again what pressing Continue had already answered.** The session opened at the
  right minute and then offered to decide the minute, over a picture already playing — «al hacer
  click en continuar vuelve a pedir confirmación de continuar o volver a ver desde el inicio en la
  vista del reproductor». The position the caller names had won over the policy since the version
  switch; what was never done then was the other half of that same decision, and the two are written
  next to each other now: **no offer when the request names a position**. It holds for «from the
  start» too, where offering to resume was the offer arguing with the button that had just been
  pressed. The offer still appears where it means something: a session nobody opened with a minute,
  which is what opening a file from Explorer is.

- **The film card's play button disappeared.** It was hidden outright when there was nothing to
  resume, so a film nobody had started offered the «from the start» glyph and no way to simply watch
  it. It is now **one button whose words follow the state** — «Continuar · 49:00» with progress and
  «Reproducir» without, which is what the prototype writes — and the glyph becomes what it always
  was, the alternative: drawn only while there is something to be an alternative to.

- **Home's covers led nowhere.** They were cards inside a list item, so pressing one selected it and
  nothing else — «al hacer click en las tarjetas en home no redirige a la vista detalle del vídeo».
  The prototype wraps the whole cover in a button, and that is what both poster rails carry now, with
  the same `poster-card` class the library grid uses: a card is one shape and one target area in all
  three places. Pressing them turned up a real ambiguity on the way: one title can be on «Recently
  added» and on the suggestions rail at the same moment, and two controls with one name is the very
  defect the rail's buttons were renamed to avoid — so a cover announces its rail and then its title.

- **And the suggestions rail was drawing twenty covers of the initials of nothing.** Its title lookup
  was an optional parameter the composition never passed, so it resolved the empty string: registered
  and never fed, in its quietest form — the rail rendered, the cards had the right shape, and there
  was no error anywhere. It is asked for once per load and in a batch now, because the catalogue is
  read over a connection and a per-card lookup can only be answered by blocking the thread that draws
  them.

- **A menu's selection box is no longer an accent rectangle.** It was two pixels of accent around
  every chosen row in the application — the settings index, the rail's menus, and a poster inside a
  rail too, where it reads as a box somebody dropped on a picture. The prototype draws it the other
  way round: a neutral wash, `rgba(127,145,170,.16)`, with the accent spent on the 3 px bar of the
  current destination. That is what is there now, with two new tokens (`SelectionFillBrush` and
  `SelectionStrokeBrush`) and a one-pixel border. The stroke is not transparent and that is the one
  concession, measured: `ListRowStateTests` asks a chosen row to stand out from its neighbours by
  3:1, and a 16 % wash reads 1.1:1 — so it carries the neutral 3.88:1 border, which answers the gate
  without answering it in accent. Selection also stops following the accent chosen in Appearance,
  because it is no longer accent.

- **The library's filter pills were not the prototype's.** The unchosen one carries
  `border: 1px solid transparent` over the plain fill with the quiet ink; this carried the control
  border and the primary ink, so three options were drawn as three chosen ones with one a little
  bluer. And **the drop-down said nothing when it opened** beyond turning its caret over: it was
  missing the other half the prototype writes, the subtle accent as fill and the accent as border.
  `SelectionSurfaceTests` measures both on realised controls, which is a different question from
  reading them out of the markup.

- **The buttons still did not line up vertically, and the compensation was in the wrong place.** The
  five pixels lived in the button's bottom padding, which moves **everything**: a glyph and the word
  beside it travel together, so the two stayed exactly as far apart as before and all that changed
  was where the whole row sat. Measured in a 44 px button: the glyph's middle at 19.00 and the
  word's ink at 21.43 — **2.43 px**, the same number as always, untouched by the padding that was
  meant to fix it; and the glyph a further 3 px above the button's own middle, lifted for a baseline
  it does not have. The five pixels are now a bottom margin **on the label**, which is the one thing
  with a baseline to answer for, and the glyph stays centred by its geometry.
  `ButtonOpticalCentreTests` holds both claims to a pixel, the icon against the word included — which
  was the half no gate was looking at.

- **The "from the start" glyph is the restart arc with its arrow on the other side**, which is the
  one it carried before read the other way round, and its button is the size of the two beside it:
  36 like them and round, rather than the player's 44. A 44 px circle in a row of 36 px pills is the
  one control that does not line up.

- **And there is no square button left anywhere.** The gate reads the button styles out of the token
  file and refuses any that names a radius other than the pill's: there were six beyond the two that
  showed — the "other actions" rows, the two on the rail, the accent swatch and the colour grid's
  cells — and the swatch was writing its own 22 beside the token as well.

- **The "hardware acceleration was not available" warning appeared on every playback**, and it was a
  complaint about the machine where the truth is a decision: this engine composes subtitles into the
  picture it hands out, and a graphics-card surface cannot be asked to do that, so it **does not ask
  for one**. Neither requested nor active, which is what actually happens.

- **The rating is five stars** rather than ten numbered squares. What was already stored comes with
  it: migration 0020 halves it and **rounds up**, so a 1 survives as one star instead of falling to a
  zero this application cannot hold. What says a star is given is its fill and not a mark beside it,
  because "three of five" is what a rating means. "Clear rating" stays right beside them.

- **The minute on the "Continue" button was outside it**, reading as a caption about the row rather
  than as part of what the button will do. It rides inside now, which is what Home has done since it
  was drawn.

- **The "from the start" glyph is the play triangle mirrored**, which says "back" before any word
  does, and its button is **round**.

- **Every icon is two pixels smaller**, and the stroke with it: the four scales keep the ratio they
  were built with — width over fifteen — so a glyph two pixels narrower reads as the same drawing
  rather than as a bolder one.

- **No button is square.** Two classes were: the player's, 44 by 44 with the medium radius, which is
  a square with its corners taken off, and one called `player-pill` that drew a small radius. The
  icon ones are circles and the ones carrying a word are pills, from one token either way.

- **And round for real.** The pill token was 18 — half of a 36 px control — which leaves a pixel of
  straight edge on anything taller: the player's 44 px targets came out as squares with their
  corners taken off. It is 999 now, which the renderer clamps to half the shorter side, so a square
  target is a circle and a wide one is a pill from one number. It is the prototype's own
  `border-radius: 999px`, and the reason that idiom exists. The test looks at the **corner pixel**
  and not at the number: 999 would satisfy any comparison while the renderer decided otherwise.

- **The film card carries the same play triangle the series card has**, which is what the owner
  missed — "el botón de reproducir no es igual al del prototipo" — and **"play from the start" is a
  glyph** rather than four words in a row that already carried three other labels. What it says is
  unchanged: the name a reader hears and the tip a pointer finds are the sentence they always were.

- **The disabled outline was drawn in all four themes, and only two need it.** The reason it exists
  was written down from the start: in light and dark a disabled control reads off its fill, which is
  a third grey, and the two high contrast palettes have no third colour to spend. Drawing it in the
  ordinary themes too put a dotted rectangle on top of a grey that already said so — the owner
  counted them on seven screens and the instrument counted **299 across the tree** with no data
  loaded, every one of them a command with nothing to act on. The cue is spent where it is the only
  one, and the fill carries it where the fill can.

- **The drop-downs drew their text 2.43 px low**, the same number the buttons had and for the same
  reason: a font's ink is not symmetric about the box that holds it. The recipe is the buttons' own —
  five pixels of bottom margin, derived from the metrics rather than tuned — put on the words and not
  on the chevron, which is geometry and centres itself. Measured before and after: 24.43 against a
  middle of 22.00, and inside a pixel once corrected.

- **The blue ring appeared on a click, not only on arriving with the keyboard.** All ten focus
  selectors said `:focus`, which a mouse raises too, so every checkbox somebody clicked answered with
  a two-pixel ring that stayed until they clicked elsewhere. They say `:focus-visible` now. A pointer
  already says where it is by being there; the ring says where the keyboard is.

- **Full screen and the floating window were not in the transport bar.** Both modes were reachable
  only from the pill row above the picture and from the keyboard, which is where the owner did not
  look for them. They are at the end of the bar, they reach the player's own model — the bar travels
  to the small window, where there is no shell above it to reach up to — and they are **no longer in
  both places**: two buttons answering to "Full screen" on one screen is a name that names neither,
  and the walk says so out loud, because it cannot aim the click.

- **A double click on the picture puts it on the whole screen, and takes it back off.** Nothing was
  listening for it.

- **The space bar put the picture full screen and `F` did nothing.** Neither was about the map: space
  has always been play/pause and `F` has always been full screen. It was **who hears the key first** —
  a button in the bar takes focus when it is clicked, and a focused button answers the space bar by
  activating itself. The player hears them on the way down now, before any focused button can spend
  them.

- **The mini player's icon was the one for leaving full screen**, four arrows pointing inwards. The
  picture-in-picture one was already in the dictionary and used by nobody.

- **The transport bar was duplicated inside the floating window.** The picture that window is handed
  brings the whole bar with it, and the window already draws five controls of its own. The picture's
  bar stands down while it is there and comes back with it.

- **Subtitles never reached the screen, and there are now three measured reasons why.** The owner
  brought it with the test already done: the same episode shows them in VLC and not here. None of the
  three is visible by reading the code, and all three had to be measured against a real file with a
  control `.srt` covering the whole film.
  1. **The session switched them off on the way in.** `ApplyPlaybackPreferences` applied the resolved
     value whether or not a preference had been stored, and with none stored that value is "off":
     every first playback handed the engine `-1` and disabled the track the container marks as its
     default. A scope that does not answer is silence, and silence is not "no". Only a decision
     somebody took is applied now, and whatever the engine chose on its own is read and shown.
  2. **The memory sink's chroma decided whether the subtitle was composed at all.** With `RV32`,
     `RGBA`, `ARGB`, `RV24`, `YUY2`, `VYUY` and `YVYU`, **not one byte** of the frame changed when a
     subtitle was switched on; with `UYVY`, 61 687 did. The engine asks for `UYVY` and converts to
     BGRA itself.
  3. **With hardware decoding the subtitle is lost to an error nobody was reading.** VLC says it once
     per frame — "no matching alpha blending routine (chroma: YUVA -> DX11)" — and publishes the frame
     without it: it draws the subtitle onto the picture before it reaches this buffer, and with
     D3D11VA that picture is still a graphics card surface. 67 001 bytes change in software and none
     in hardware. The engine decodes in software and **says so**: the request stays the caller's, and
     what is announced as active is what is running.

- **Subtitles sitting beside the media were never loaded.** `ExternalSubtitleDiscovery` was written,
  confined to its root and tested in both encodings — and called by nobody: the session handed an
  empty list on every open. The house defect, again.

- **The video was stretched when the window was resized.** It was drawn across the whole width and
  height available, so a 16:9 episode in a dragged window came out dragged too, in the player and in
  the picture-in-picture. `VideoFitPolicy` keeps the shape and shares the bars; what is asserted is
  the **ratio**, not the size.

- **Home was empty on a full library.** It read the `titles` table, which **nothing in the application
  writes** — `ApplyIdentification` already said so in its own words — while the library lists the union
  of identified titles with scanned files. Measured against the owner's own database: 102 rows in
  `scanned_titles`, zero in `titles`, and four in `watch_state` that matched scanned files and nothing
  else. All three Home projections now read that same union.

- **Home did not load at startup either.** The navigation service starts on Home and announces
  nothing, so the only place that reads Home — arriving at a route — never ran until somebody left and
  came back. The route the application opens on is announced like any other.

- **The track panel said "no subtitles" over subtitles somebody was reading.** The engine chooses
  while the first frames decode, which is after the panel was built: measured, none in force at open
  and track 6 three seconds later. The panel follows the engine now.


- **Button labels looked low even with their box centred, and this time with the number first.**
  `ButtonInkTests` centred the box in August and said in writing that it did not measure glyphs; the
  owner kept seeing it crooked and was right. Measured through the font's own metrics: the run of ink
  — the top of a capital to the foot of a descender — sat **2.43 px below** the middle of a 44 px
  button. Five pixels of bottom padding move it up two and a half, the number is derived rather than
  tuned by eye, and `ButtonOpticalCentreTests` holds it to the pixel. Buttons whose content is a
  glyph do not carry it: an icon is centred by its own geometry and has no baseline to compensate.

- **The accent changed almost nothing in the application.** Four brushes were written and Fluent's
  controls read their own, which the token file points at the accent with `<StaticResource>` — a
  **static** reference, resolved once when the dictionary loads. So neither the sliders, the check
  boxes, the radios nor list selection followed the chosen colour. All twenty redirections are
  written now as well as the four tokens, and `AccentTokenTests` reads the same file to insist none
  is missing: a redirection added next year and forgotten here would be a control keeping whichever
  accent it was built with, and that cannot be caught by looking, because nineteen of the twenty do
  change.

- **The colour swatches carried a circle inside them.** It was the ● and ○ every pill row in this
  tree spends, and on a circle of colour it reads as a radio button somebody dropped there. The
  prototype says it with the swatch's own edge, and a ring is geometry exactly as the glyph was: both
  high contrast dictionaries paint a border, so nothing is lost where colour says nothing.

### Added

- **«From the start» is on Home's two wide surfaces as well**, which is where it was missing: the
  same arc with its arrow the other way, the same 36 px circle, beside the button it is the
  alternative to — «en la tarjeta ancha del inicio justo después habría que poner el icono de
  reproducir desde el inicio, como en la vista detalle del vídeo». One flag on the request rather
  than a second hook: what changes is the minute the session opens at, and the host is already what
  reads it.

- **The element catalogue, read and written down**:
  [docs/design/ELEMENTS.en.md](design/ELEMENTS.en.md) carries
  `design/Catálogo de elementos - AP Reelume.dc.html` over into this tree's tokens, element by
  element and state by state. It says three things that had been drawn wrong — an unchosen pill
  carries no border, a menu is not painted like a drop-down, and the optical compensation goes on the
  label and not in the button's padding — and one rule of precedence: the prototype wins over the
  document, and the document wins over the `.axaml`. `BilingualHeadingTests` holds it in both
  languages along with `SURFACES`, which until now had no gate at all.

- **Every button says what it does when the pointer rests on it**, in the same words a screen reader
  is told. One style rather than an attribute per button: there are more than two hundred of them,
  and written out one by one that is two hundred chances for the two sentences to drift apart with
  nobody noticing for a year.


- **A colour picker on all three rows that choose one**: the accent and the two subtitle colours.
  The prototype opens the browser's; this opens the same shape out of controls this application
  already owns — a grid of eight hues by five lightnesses over a row of greys, three sliders for
  anything between them, a large swatch of what they make, and the value in a fixed-width face. The
  grid is built in the domain rather than listed: the hues are spaced round the wheel and the
  lightnesses up it, which is what makes a grid read as a grid.

### Added

- **Settings → Appearance with the prototype's eleven rows**, where it had two. Beside the theme and
  the language: follow the Windows theme, accent colour with its six swatches and the value in a
  fixed-width face, subtle Mica backdrop, accent tint on backgrounds, density, cover size, corner
  rounding, show titles under covers, interface animations, and the row with no control saying the
  player's surface is fixed. Each one writes into the resource the interface already read — the same
  route reduced motion has reached the animations by since it was implemented — so none of them is a
  stored preference that changes nothing.

  **Three of them touched gates, and all three are declared again.** The custom accent against
  `ContrastTokenTests`: its five obligations were met by hand-picking two colours per theme, which is
  not something a person choosing their own can do. The family is **derived** now — hue and
  saturation are theirs, lightness is walked until the ratio against the page it will be drawn on is
  met — and `AccentPaletteTests` measures it by sweeping the whole wheel against both surfaces, 600
  colours each. Corner rounding against `ScalarTokenTests`: both radii are written from one choice,
  and `DensityGutter` is spent on the card rather than left declared with no reader. Density and
  cover size against `ViewOverflowTests`: the grid already counted its columns by reading the token's
  width, so moving the token moves the count.

  In high contrast the accent is left alone: there it is a need rather than a taste, and what the
  service does is remove its own writes instead of adding another on top.

### Added

- **Playing takes everything but the picture away, and the mouse or a key brings it back.** The
  title bar, the navigation rail, the session header, the panel column and the transport: while the
  film runs, the window is the film. The prototype does not do this — its own source was checked —
  so it is a requirement of this application's own. The shell's two bands now measure their contents
  instead of carrying the number, because a control hidden inside a fixed 44 px row leaves the 44 px
  behind: the row is what reserves them. And the `RevealControls`/`HideControls` pair the player has
  always declared and **nothing ever called** finally has a caller.

  It comes back on the gesture and on nothing else: no timer puts it away again. That is a decision
  with its cost written down — a person who moves the mouse once keeps the chrome until they pause
  and play again — and the alternative is a clock deciding what is on screen, one no test can ask a
  question of without waiting for it and that the autonomous walk would race on every scene.

### Changed

- **The player is headed by the prototype's pills and its column starts closed.** Audio, Subtitles,
  Video and Markers — plus «Other versions», which this player keeps — sit on the header and open the
  column; pressing the open one gives its 320 px back to the picture. It was a tab strip before, and
  a tab strip has no way to say "none of them": some panel was always holding a fifth of the film's
  width, whether or not it had anything to say. The column now carries a header of its own with the
  name of what is open and its «×», which is the second way to close it.

- **The four panels group by subject rather than by model.** «Audio» gathers the audio tracks, the
  output device and its channels; «Subtitles», the subtitle tracks; «Markers», the detected ranges
  and the ones this title keeps. Each half comes from a different model and none was merged: the
  grouping belongs to the person looking, and a panel draws only the halves the session has.

- **The subtitle picker's label reads «Subtitle track».** It read «Subtitles», which is exactly the
  name of the pill that now opens its panel: two controls under one name is a real ambiguity, and the
  walk found it before anybody else did.

### Added

- **«Video», the panel that was missing.** It says whether decoding is on the hardware or in
  software and whether the output is HDR10 or SDR, with the sentence that explains it under each row
  and the scope written at the foot: Dolby Vision and Dolby and DTS passthrough are out. They are the
  same facts the badge over the picture already carried; this is where somebody goes looking for them
  instead of waiting for them to appear.

- **The session badge, «Session 1 · single active engine».** It is true and measured: LibVLC is built
  once and one session at a time holds it, so the number is how many have been opened in this run. A
  new session raises it and closes the column, because the panel standing open belonged to the file
  that just left.

- **At the right of the foot, where the sound is going.** The chosen device and what it can carry —
  «System speakers · 2.0» — which is the pill the prototype draws at the end of its transport. Here
  it sits on the row below rather than the transport's own: that one already holds two sliders and
  two readouts, and at 900 px — the narrowest window this application allows — adding a device name
  to it is the shape that has drawn a control off the edge nine times.

### Fixed

- **The «Remember for the whole series» check box measured with infinite width.** A `CheckBox`
  measures its content without a limit and draws it where it falls, so its label depended on Spanish
  happening to fit a 320 px column. It wraps now. It is the same shape this repository has caught
  nine times, and the last one left in the player's column.


### Fixed

- **The dotted outline on a disabled control had the wrong shape.** It was drawn with a fixed 4 px
  radius for all ten types, so on a pill — «Clear rating», any leading action, any «Other actions»
  row — it became a nearly square rectangle whose corners fell **outside** the button's own edge,
  which reads as an outline bigger than the thing it outlines. Measured: the adorner was always
  exactly the control's size, so «bigger» was the corners and never the box. It takes the control's
  radius now, read when it reaches the tree — both are style setters, and reading at construction
  caught a 0 the style had not written yet.

- **Four controls the walk pressed and the inventory did not recognise.** The ones named by their own
  data — a rail card's two actions and the season pills — are recorded under the binding they are
  declared with; without that the walk wrote «Season 1» while the inventory looked for
  `{Binding SeasonLabel}`. The gate is back at zero pending.


### Fixed

- **Two of the three title tools appeared with nothing to do.** «Review versions» opens a comparison,
  and a film with a single copy is never grouped: the surface behind it answered with nothing and the
  route never changed, so it was a door onto a room that does not exist — and on a series it never
  exists, because its episodes are files under keys of their own. «Preview rename» opens a plan, and
  a file already called what this application would call it produces a plan of no operations, which
  `RenamePolicy` itself says by turning that case into a `NoChange` conflict. Both are asked when the
  card opens and drawn only if the answer is yes. «Edit details» is the third and has no condition.

- **The external trailer button says less and shows more.** «Watch the trailer in your browser» was
  as wide as the three title tools together; it says «Watch trailer» now, with the leaving arrow the
  prototype draws beside it, and the help text tells a reader where it goes. The local trailer
  becomes «Play trailer», which is what it does and what tells it from the other one.


### Fixed

- **The kind chip's shape is decided by a style, not by a converter.** `KindShapeConverter` had to
  ask for the application and look a resource up by name, and neither of its "not found" arms can be
  taken: both icon keys are declared and there is a gate over that inventory. It is removed. The card
  asks `IsShow` — which the interface answers from the key the four models already give — and two
  style rules draw the film or the screen. The coverage ratchet drops from 215 to 214 with it.


### Fixed

- **The review card's labels came in two styles.** «PENDING FILE» was in small capitals and
  «Proposed candidate», «Confidence» and «Why» were not, inside the same card. All four are now
  written the way the prototype writes them, and the third says what it says there: «SIGNALS
  CONSIDERED», which is what the list under it holds.


### Fixed

- **«Other actions» held two grammars in one column.** The personal marks were already full-width
  rows with an icon, which is how the prototype draws them; the three watch-state decisions were
  still loose pills of differing widths above them. They are five matching rows now. Same names, same
  commands, same order — only the shape moved.


### Fixed

- **The repository's five screenshots showed one machine's own path.** The review tray writes the
  folder under every file name, and the library behind those captures lived under the profile of
  whoever took them: `C:\Users\<name>\.claude\projects\…` was printed
  into `docs/assets/review.png`, in a public repository. They are retaken with the library in a neutral folder — and with the application as it
  stands now: the kind chip without its word in the rail, the plus on «Add media», the candidate's
  name, and the player's second line.


### Fixed

- **A playback failure no longer erases itself.** LibVLC refuses a file and then, a moment later,
  reports the stop of the media it had just torn down; that state replaced the failure, so the
  recovery actions vanished from the screen while somebody was reading them — including the offer to
  hand the file to another player. It appeared first as a flake: the physical walk waited a full
  minute for a failure that had already happened and been overwritten. The three ways out of a
  failure that this application decides — reopening, failing again for another reason, going idle —
  all still apply.

- **The card's «Available» state is said in green**, which is how the prototype paints it. The chip
  beside it stays neutral: «Not started» is a fact, «it is there» and «it is not» are the two answers
  that card exists to give.


### Added

- **The review tray says which title it is talking about.** It asked somebody to accept or reject
  «movie:761053»: the provider already answered with the name and the year, and the whole chain —
  facts, scoring, stored row, projection — threw them away. It carries them to the card now, which
  writes «Tormenta de Sal (2016)» and falls back to the key only when no name was stored, which is
  every row written before this version. The name is also what a screen reader announces on the
  card's three buttons.

### Fixed

- **The «Next episode» panel limits its column, not its border.** A fixed width does not give way: at
  200 % text scaling it put «Continue» at pixel 714 of a 683 px viewport, out of the mouse's reach.
  Measured in CI. A column of «* up to 540» is the `max-width` the prototype uses: as wide as there
  is room for, never wider, and still against the left edge.


### Fixed

- **Four details the prototype had and this application did not.** The kind chip drops its word in
  the rails and keeps it in the grid, which is where the prototype writes it — a chip saying «Film»
  across a third of a thumbnail competes with the picture. «Add media…» gets its plus back. The
  player's header gets its second line back: what it was handed only ever had a value for an episode,
  so a film arrived with a title and nothing under it. And the speed pill is written «SPEED», which
  is what fits: the long label made it wider than the four transport buttons together, and it stays
  the control's accessible name.


### Fixed

- **A season looks like one season again.** Every episode still was coloured from the hash of **its
  own name**, so sixteen episodes of one series were sixteen unrelated colours. The prototype draws
  `art(show + episode × 7)`: the show's hue, walked a few degrees per episode. The cover now takes
  that shift and the row asks for it.

- **The «Next episode» panel stops shrink-wrapping.** It fitted its contents, so «Continue» moved
  every time the episode's name changed length. The prototype gives it a fixed 540 px and lets the
  text take the slack.


### Fixed

- **The chosen copy is marked on its whole row, and the link carries the accent's ink.** The
  prototype marks the choice three ways at once — the radio, the accent border and the accent's wash
  behind the row — and the view had only the first: a fifteen pixel mark on a row a thousand wide.
  The group's heading stops being blue, which is how the prototype writes it, and the application's
  three links move from the accent to its **ink**: same place, same size, and 9.03:1 instead of
  5.62:1 in light, 11.36:1 instead of 8.29:1 in dark. The new pair is measured in
  `ContrastTokenTests`.

- **The size column stops saying «0».** It went down to megabytes and rounded, so a file of two bytes
  read as empty on the very screen where somebody decides which copy to keep. The ladder now reaches
  the bytes themselves, and only a size of zero — or none recorded — stays blank.


### Fixed

- **Four branches nothing could take, and the two that happen and no one watched.** The duplicates
  reader tested three columns for null that the schema declares `NOT NULL`, and the destination asked
  after its parameter again once `CanExecute` had already demanded it: unreachable code, removed
  rather than measured around. The ones that do occur had no test and now have one — progress stored
  before the engine knows the length, which is the ordinary state of the first seconds, and a codec
  written before that column held JSON.


### Fixed

- **Three things declared and never fed, taken out or fed.** The red state chip — what says a file is
  not there is the shared badge, and a second shape saying the same thing is what
  `UnavailableBadgeTests` exists to refuse — the folder icon, because this application opens no
  Explorer window from a card, and the «leaves the application» arrow, which had somewhere to go: the
  player's recovery button that opens the file with another program.

### Fixed

- **Two commands nothing read any more, and two branches nothing could take.** Moving the decisions
  into the card left `AcceptSelectedCommand` and `RejectSelectedCommand` declared with no consumer —
  this repository's characteristic defect, introduced by the change itself — and they are gone. So
  are the null guards inside the card's three commands: `AsyncRelayCommand` asks `CanExecute` before
  it runs, so a second check was a branch nothing can take.

- **The player's scrubber is pressed with the session paused.** Since the transport observes the
  engine's position, a session left playing moves the walk's own probe: the click beside, which has
  to change nothing, changed something on any runner slow enough for a frame to go by. Measured on
  CI, twice.

### Fixed

- **Home's hero ends in the page rather than at a line across it.** The prototype draws two veils
  over the artwork: the directional one that was already here, and the page's own colour rising from
  the bottom edge. It is painted as the surface brush behind an opacity mask rather than as a
  gradient of colours, because the colour has to be the theme's — four dictionaries, and a hex
  written there is right in one of them.

### Fixed

- **The three title tools are in the card rather than under the library.** «Edit metadata», «Preview
  rename» and «Review versions» were a row of buttons under the grid, acting on «the open title» on a
  screen where none need be open. The prototype puts them in the banner's action row, and that is
  where they are — one view mounted by both cards, so the identity the walk presses stays one.

### Fixed

- **The review tray stops being a list with a selection.** Its rows were command controls, and with
  the decisions inside the card that left the walk's own proof with nowhere to click «beside» —
  measured in CI: no point around «Accept» falls outside another card. The tray is drawn the way the
  duplicates destination draws its rows, which never had the problem: cards in a list with no
  selection, and the up/down arrows go with it because what a keyboard walks now is buttons.

### Fixed

- **The player says what it is playing.** The top band had three glyphs and nothing between them,
  with the reason written into the view itself: the session holds a path, and painting somebody's
  file path as a heading is the opposite of what this application is for. The title and its line now
  **travel with the request**, from the card that pressed Play — the one that knows — and the header
  writes «El Faro de Piedra / 2019 · Drama · Misterio · 96 min».

- **The transport is one row again.** Play, pause and stop lived on a second line because their
  commands belong to the session coordinator while the skips belong to `ControlPlayback`. That is a
  fact about the models rather than about the row: the buttons reach across to the player's own model
  through the view that hosts them, and the order is the prototype's — back, play, forward — with the
  speed carrying its word and not only its number.

- **The player's shortcuts are written where they are used.** Space, the arrows, F, N and Escape were
  all bound and announced one control at a time; a person who never opens a screen reader learned them
  here or not at all. The prototype writes that line under the transport, and now so does this.

### Fixed

- **The review tray shows the file it is asking about.** It asked for a decision on «movie:761053»
  and never showed which file that was: the candidate projection now carries the path as well as the
  identifier. Every card holds the cover, «PENDING FILE» with the name and the folder, the candidate
  with its kind, the confidence and the signals — and **the three decisions inside the card**, which
  is where the prototype puts them. They lived one row below, acting on «the selection», which is one
  decision per tray rather than one per file.

- **Enter on «Reject» accepted.** Measured: the list's shortcut answered before the focused button
  did, so the keyboard accepted exactly what somebody was trying to refuse. The shortcut is gone; a
  card holding three actions cannot have a key that silently picks one of them.

- **The duplicates page is the prototype's table.** It listed titles and a count with the comparison
  a click away. Every group now brings its table — file, resolution, codec, audio, size, duration,
  location and availability — with the radio that decides which copy plays by default, without
  opening anything. The reader does it in one query, and the title is still the way into the
  comparison it always was.

- **«1 episodes» is not written any more.** The series card's count changes word in the singular, in
  both languages.

### Added

- Coverage for what the redesign brought and CI measured downwards: the equality of a catalogue row
  with its six new members, both absences of an episode's title and running time, the year and the
  genres Home reads — present and absent — the two cards' lines with every piece that can be missing,
  the two buttons on each rail card, and the duplicates table read from the real store.

### Fixed

- **The series card is the prototype's.** Under the title: «2020 · Drama · 3 seasons · 16 episodes»,
  the series' own bar with «10/16 watched», and the panel that names the episode it is waiting on —
  «S02·E05 · Puerto de invierno», «Resume at 17:00» — with its «▶ Continue» button, the card's only
  accented action. The seasons stop being a drop-down and become **pills**, all three on the surface;
  and every episode goes from a 56 px strip holding a number to the prototype's **card**: the number,
  the wide still with its progress bar, the episode's name and «48 min · Watched». The name and the
  running time were not on screen at all — the episode projection did not read them — so a season
  read as a column of numbers.

- **Both detail cards scroll as one page.** There were two scrolling regions on one screen — a fixed
  banner over a list with a scrollbar of its own — so the wheel answered one or the other depending
  on where the pointer happened to be. And the way back is a link, «Back · Library», as in the
  prototype, rather than a filled pill competing with the card's own action.

- **The personal marks leave the banner.** Ten rating buttons over the artwork pushed the episodes
  off the first screen. They move to the «Other actions» column, where the film card already kept
  them, in the prototype's shape: full-width rows with an icon, the name, and what the mark currently
  is.

- **A third of the banners' gradient was mismeasured.** The last stop was written `#30` meaning
  «30 %», and `0x30` is 19 %: the picture came through half again as bright as the prototype paints
  it at the right-hand edge. Corrected on both cards, and Home's hero moves to its own gradient,
  which is a different one — `rgba(5,6,8,…)`, with its stops inside the frame rather than at its
  edges.

- **CI had verified nothing since 2026-08-24.** The last three runs died at «Install ffmpeg» with a
  503 from the Chocolatey feed, before compiling a single line. The step now retries three times with
  a widening wait; the version stays pinned, so what a retry can change is the transport and nothing
  else.

### Added

- `AccentInkBrush`, the ink written **on** the accent's wash, which is what a chosen pill is made of.
  It comes from the prototype's `--accent-ink` in all four modes and arrives with its contrast pair
  measured.

### Fixed

- **The reference everything was compared against was half-lit.** The sixteen archived prototype
  captures were taken mid fade-in (`apr-in`, opacity 0 to 1): measured across the eight views,
  everything that animates came out **1.3 to 1.9 times darker**, while the page background — which
  does not animate — matched its token exactly. A poster read `#2A1722` where the prototype paints
  `#6A2C46`, which is what its own `hsl(330 38% 30%)` gives. The reference was retaken with
  `--force-prefers-reduced-motion`, which the prototype already provides for, and **each capture is
  checked against a second one**: sixteen views, sixteen pixel-for-pixel matches.

- **Home is the prototype's front page.** The hero bleeds to the edge with no card and no margin,
  with its spaced overline, the line "2019 · Drama · Misterio · 44:00 left", the bar without a
  percentage, and **two** buttons — "▶ Continue · 52:00" and "Details" — which is the pair the
  prototype always had and this tree left half-built with a note saying the second would arrive "the
  day the read model can answer for it". The continue rail stops being a row of covers and becomes
  what the prototype draws: **landscape cards** with their picture, the bar across its foot, the
  title, "Film · 2019" and the same two buttons on each. And the way into the library moves to the
  "Recently added" heading with its chevron, which is where the prototype writes it.

- **The cover is painted in one place.** Five surfaces spelled out the same four layers and three of
  them were missing the hatch. They all mount `PosterArtView` now, and with it **the colour-alone
  gate's exception list goes from four entries to one**: that list may only shrink, and this is how
  it shrank.

### Fixed

- **Every button's label sat at the top of it rather than in the middle.** The owner saw it before
  any gate did. `VerticalContentAlignment` starts at `Stretch`, this tree's own style set a height, a
  radius and a padding and never touched it, and a stretched `TextBlock` fills the whole button and
  draws its line at the **top** of that box: measured, a 36 px pill held a 34 px label box with the
  words about seven pixels above centre. The prototype writes the rule as one line of CSS —
  `display:inline-flex; align-items:center; justify-content:center` — and that is what is there now,
  with `ButtonInkTests` measuring both gaps and that the box is the size of a line, not of a button.

- **The covers' diagonal hatch was missing.** The prototype's covers are **four** backgrounds over
  one hue and this application painted two: the gradient and the glow. The missing one is
  `repeating-linear-gradient(115deg, rgba(255,255,255,.055) 0 2px, transparent 2px 10px)`, which is
  why a wall of covers here read flat where the prototype's reads woven. Avalonia has no repeating
  gradient and does not need one: `SpreadMethod="Repeat"` over a ten-pixel vector at the prototype's
  own angle paints exactly those two stripes in every ten. Gate: no surface paints the glow without
  painting the hatch.

- **The icons were a different alphabet.** The prototype draws **thirty-five** pictograms as 24×24
  SVGs stroked at 1.6 with round caps; the application painted solid Segoe Fluent glyphs — another
  drawing tradition — in twenty-seven places, and that is the difference the owner named first. The
  shapes now come from the prototype, converted into geometries that live in the repository: the
  rail, the whole player, the mini player, the magnifier, the pills' caret and the dialog's cross.
  **It departs from a line of the design package** — the Proposal and its README both prescribe
  "glyphs from Segoe Fluent Icons" — and the rule that line protects, downloading nothing, is intact.
  On the way, the mute button now **says which state it is in**: it drew the crossed speaker whether
  the session was muted or not.

### Added

- **The repository page, with captures of the real application.** Both READMEs open on Home and
  carry four more captures — the library, a series card, the player with its column, and the review
  inbox with its three candidates — in English, dark theme and 1600 × 1000, versioned under
  `docs/assets/`. A script takes them against the built application over an isolated data root with
  a **fictional** library: a capture of the real one carries somebody's titles and somebody's paths
  inside a PNG no test can read, and the page says so itself. The page also carries the platform,
  the licence, the download link, and what it means for a commit to reach `main`. **It carries no CI
  badge, for a measured reason**: the workflow deliberately does not run on `main` — which receives
  the same SHA by fast-forward — so a badge pointing there freezes on whatever it last saw and goes
  on saying it, which is a blind check under another name.

### Fixed

- **The player had neither a scrubber nor a clock while it played.** Found while taking the fourth
  capture against a real film: `TransportControlsViewModel` changed state on its own commands and on
  nothing else, so `HasDuration` stayed false for the whole session and the scrubber row and both
  clocks never came to exist — pressing a skip was enough to make them appear. The playhead already
  reached `CompositionRoot`, whose handler feeds the tracker and the skip offer, each with its own
  comment about having been "reachable and never fed"; the transport was the third, and the one a
  person watches. Fifteenth shape of the house defect. With it, the two physical walks that press
  skips now measure them on a **paused** session: with a live playhead, the click beside a skip moves
  the position as surely as the skip does.

- **A series' season picker printed a class name on screen.** The `filter-pill` template binds
  `SelectionBoxItem` into its `ContentControl` and did not bind `ItemTemplate`, so every series card
  read `ApSolutions.LocalMedia.Presentation.Show.SeasonViewModel` where it should read "Season 1".
  It was in the previous day's parity matrix and nobody saw it; the README's capture caught it. The
  library's two drop-downs never showed it because their rows are `ComboBoxItem`s with text inside.

### Added

- **The palette is the prototype's, measured from its own code.** The owner looked at the captures
  and said the truth: neither the colours nor the elegance matched. The cause was twofold: the
  tree's values came from an earlier snapshot (bluish #111827 where the prototype paints
  #050608/#08090C near-black) and the finishing layer was missing. Both ordinary dictionaries are
  now re-valued to the canon read from the prototype's own `tokens()` — background, #12151B cards,
  fills, subtle #5C6878 borders (the one place the canon yields to the gate: its #3A424F reads
  1.96:1 against the 3:1 floor), #EDF1F6/#8B97A8 text, states translated to opaque mixes —; the
  **primary is at last the prototype's light pill** (#F3F6FA with near-black ink in dark), with its
  own token family, its four pairs measured in the gate, and the state test re-declared: under the
  hand it STAYS the primary — a leader that dressed down exactly when about to be pressed stopped
  leading —; and the **prototype's two elevations** exist as typed tokens and are spent where it
  spends them: settings rows, library cards, and the floating dialog. High contrast untouched: no
  shadows, and its pair already declared. Verified on screen with the seeded library beside the
  prototype capture.

- **The Library's first visit wears a skeleton, and privacy says its two best answers.** Six ghost
  cards breathe at the single motion token's pace while the first query runs — and only the first:
  a skeleton over cards somebody is reading is worse than the wait it decorates. The prototype
  sweeps for 1.2 s; here the shimmer is the scan dot's own breathing, because this repository
  holds ONE motion token and a second, slower one would be the parallel-copy defect returning
  dressed as a sweep — the deviation is commented on the style. Privacy paints the empty
  connection list in positive terms (“No connection declared · Nothing leaves this machine”) and
  explains under the preview why empty fields are listed: seen-empty is proof, omitted is a claim.
  The package's 16 design comments stand in their 9 files — the six missing ones were written
  today. Two phase limits, decided and noted: Home carries no skeleton because its rails paint
  their answer the frame it arrives, and the switch handle is raised to the owner — the tree
  toggles with CheckBox by a measured decision (18 uses, 73 theme resources, its own suite) and
  migrating to ToggleSwitch revokes it.

- **Updates, Backups and Restore speak §4's four grammars.** The updater's status lives on ONE
  living border — a screen reader subscribed to a region must not have it swapped out — that now
  dresses by the news: neutral process, up-to-date in positive, a guardian's refusal in Warning
  with **the reason as the headline above the status** and the technical identifier folded behind
  “See the technical detail” — a new control, with its scene: the walk serves a release for an
  architecture that does not exist, watches the refusal arrive with its reason, and unfolds the
  detail with the mouse — and a failure of the world in Danger. Backups gains the active
  database's block — the path reachable without needing a failure —, the empty history says what
  the first copy costs, and the failure adds that nothing was left half-written. Restore numbers
  its three steps in the order a person walks them — Confirm moved down to its own step: offering
  it before anything was chosen was reading order lying about task order —, says in positive when
  there is nothing to remap, marks under each unresolved folder its exact consequence, and the
  rows' status labels are the approved ones from the package.

- **A title's two tools share one panel behind real tabs.** Editing metadata and previewing the
  rename lived stacked — opening both meant reading them one under the other —; now a single
  panel holds them behind Metadatos | Renombrado tabs, each door selects its own tab as it opens,
  and a tab whose surface is not open hides its header rather than offering a blank page — the
  player side panel's pattern. The assembled journey asserts the new semantics: one surface
  materialised at a time, and the one behind comes forward when its tab is chosen. For the rest of
  the phase, measurement ruled: the positive empty inbox, the two-column comparison with its
  figures in monospace, and the editor's three glyph messages were already in the tree with their
  gates green.

- **The player closes its phase: speed is the prototype's menu and a failure's third exit is at
  last a button.** The speed readout — text a mouse could only look at while the keyboard did
  change the step — is now a button carrying the policy's nine steps as a menu, with “Back to
  1×” only while there is something to come back from; a test compares the markup's steps against
  `PlaybackControlPolicy.SpeedSteps` so mouse and keyboard cannot drift apart. On the failure
  surface, “choose another version” stopped being an informative sentence: the button unfolds the
  very rows the side column lists — the same object, handed over by the composition — so choosing
  there IS the switch and there is no second grammar to learn. The three overlays that declared a
  single dimension (`ResumePrompt`, `NextEpisode`, `VersionSwitchDialog`) now declare both, and
  the three marker consequence notices approved on 2026-08-23 stand where each consequence
  happens. Two walk scenes open both menus with the mouse — a drop-down is measured by opening:
  what is chosen inside lands in a window of its own.
  - Along the way, three transport members nobody consumed (`SpeedSteps`, `Duration`,
    `ConfigureSkipsAsync`) leave the model: the house defect's fourteenth form.

- **Duplicates joins the rail and Copias moves into Settings**, which is the prototype's map and
  the owner's 2026-08-23 decision. The new route lists **every group** — a query that did not
  exist: a reader in Infrastructure, a use case in Application, and a row opens the SAME comparison
  the card's action opens, through the same shell door, so the two ways in can never disagree. The
  empty state is the desirable one and says so with the approved strings. Backups and restore now
  live behind their index entry with their peers' section skeleton, and the walk's scenes arrive
  the way a person does.
  - The house gate caught the new reader's registration without an explicit resolution in the very
    commit that introduced it, which is exactly what it exists for.

- **Both high contrasts are choosable: Appearance goes from three pills to five**, with the
  `ThemeHighContrastLight` and `ThemeHighContrastDark` keys the package carried and the decision the
  owner revoked on 2026-08-23 through the door it left open. The system's own setting still wins
  over whichever pill is on, the `WrapPanel` decides where five fold in each language, and the walk
  applies them for real — the whole application wears each high contrast for a moment and System
  puts it back. Three gates that counted three were declared up to five.

- **Settings is the prototype's page: heading, fixed side index, one section at a time.** The
  construction is §7's to the letter — no sticky, a Grid whose ScrollViewer lives only in the right
  column — and the `side-list` styles the theme declared with no consumer finally have one: the
  house defect, run backwards. A section that is not open is NOT in the visual tree, so every walk
  scene that presses inside one opens it from the index first — the same press a person makes, and
  the press that proves the index: all ten entries end up pressed by the scenes that need them.
  - The structure contract was rewritten to the redesign's: the H1 lives above the index, and what
    holds is that the open section starts where its peers start — the model-less harness still sees
    all ten at once, which is what lets them be compared.
  - «Biblioteca y escaneo» finally groups folders and scanning under one entry, with the index's
    new string in both languages.

- **The accent tint at the top of the content**: the prototype's 260 px radial halo, with no new
  brush — it is `AccentBrush` at the prototype's strength under a radial opacity mask, inside the
  same token that switches the art off in both high contrasts, where a decorative glow is exactly
  the colour the theme exists to remove. The rail's and title band's opaque surfaces clip it on
  their own.

- **The film and series cards carry the prototype's banner**: the poster raised over the title's own
  colour wall under the directional veil, the display-size title, the synopsis and the actions on
  the dark ground — the hero's frame coin for coin, with the same high-contrast payment the
  colour-alone gate now asserts for all four surfaces that spend it. §4's two columns survive
  inside: poster fixed, text fluid.
  - The season picker wears the drop-down pill the Library taught, label inside.
  - **The trailer's three approved consequences land**: what a local one costs (nothing), what the
    link does (leaves through your browser, not this process), and how to make one exist when none
    does — the convention is `TrailerDiscoveryPolicy`'s, and the series explains in a comment why it
    has no local trailer to offer.

- **Home's hero bleeds, which is what the prototype draws**: the title's own colour wall under the
  directional veil, over `PlayerSurfaceBrush` — the one surface that is `#0B0D10` in all four
  themes, with the player's text brushes and their measured 19.46:1. In both high contrasts the art
  layer switches off through `PosterArtOpacity` and the text stands on the plain ground: zero new
  brushes. The reason it did not bleed — "there is no artwork" — expired the day the generated art
  landed; "Details" stays out and stays blocked on data, not effort.
  - The "no state told by colour alone" gate's list gains the hero's row for the card's own coin:
    the colour repeats a title written beside it, and the payment — switching off in high
    contrast — is asserted by the same test.
- **«Abrir biblioteca» moves to the «En curso» rail header** and the entry card leaves the tree:
  same command, same words, same control name, on the only rail the gate can hold inside the first
  viewport at 1366×768. Home's structural baseline was re-recorded with the single changing field
  (the access rises from 430 to 404 px) verified across all 36 combinations.

- **«Añadir raíz de medios» is now the prototype's floating panel**, opened from the Library
  header's primary action — new key «Add media…», the ellipsis that promises a dialog — and from the
  rail's plus, which no longer navigates: the panel floats over whichever route, veil behind it and
  both dimensions bounded, which is §4's grammar for every overlay.
  - **The kind is detected from the path**, the prototype dialog's grammar: UNC from the prefix, USB
    by asking the drive, local otherwise — with the package's three approved consequences riding
    along. Where no detector is wired — previews, tests — the three pills stand in.
  - **Browse asks with the Windows picker** starting at the Videos library, or answers from the
    handover folder for a run that does not own the profile: the fourth answer of the exit the
    backups already had.
  - A successful add **closes the dialog itself** and leaves the first scan's consent to the
    route's surface; the folder list refreshes itself on add, which was the house defect in its
    list shape.
  - The first run keeps its inline form with its four §4 shapes, and retires when it stops being
    the first run; the Settings confirmation refuses with «Conservar» — the inventory's word —
    because the updater already says «Cancelar» on the same page, and two visible commands
    answering to one name is the ambiguity the walk refuses to walk past.

- **The library's folders gain their place in Settings**, which is where the prototype keeps them:
  the list as row-cards — the path in monospace, the kind in words, the availability chip with the
  shared badge — and removal behind the same red confirmation the first run taught, now carrying the
  two consequences the design package wrote for it: what stays (the files) and what leaves (the
  titles with their marks). The view shares the onboarding's model — one list, two surfaces never on
  screen together — and two gates corrected the first attempt: the unavailable badge is the whole
  application's, not a homemade drawing, and a Settings section brings no geometry of its own.

- **The Library now leads with the prototype's row: the count beside the title, the search against
  the right edge, and the kind as three pills — Todo, Películas, Series — that query as they are
  pressed.** The pills write the kind bits separately from the status bits, which the repository
  could always combine: "films I have not started" was expressible in the query and unreachable from
  the screen, because a single `ComboBox` bound to the whole flags value made exclusive what never
  was. The house defect, in its filter shape.
  - Both drop-downs become pills with their name inside — «Filtrar Todo», «Ordenar Título» — over a
    template that keeps Fluent's part names, which is what the five states hang from. They apply on
    pick, so **Apply is removed**: a button whose whole job was repeating what the control beside it
    had already said. A deliberate deviation from the control inventory, which never listed it among
    the eliminated.
  - «Quitar filtros» exists only while something narrows the grid, and «Borrar la búsqueda» moves
    into the no-results state, which is the exit the inventory added it for.
  - The grid gains §4's fourth state — truly empty, with `LibraryEmpty*` in both languages — and the
    scan row only exists while a scan runs.
  - The walk's scene now seeds a genuinely identified film, because an unidentified file is neither
    a film nor a show — the catalogue lists it under a third kind — and the pills over a library of
    loose files were legitimately empty on both sides.


- **Every button is a pill, across all ten screens at once.** That is what the prototype draws on
  every one of its own — `btnPri` and `btnSec` are both `border-radius: 999` — and §7 of the design
  proposal **gives the number rather than leaving it to the eye**: `CornerRadius=18`, half the control
  height, not CSS's 999. A third radius where the scale deliberately had two, and what earns it is the
  rule the scale itself is held to: the question is not "does the step make sense" but "does anything
  in the tree contradict it". Nothing does — **every** button spends it — so it is not the
  step-for-one-consumer `FontSizeMono` was refused for.
  - Three classes say they are not pills, and they say it themselves: the rail's destination, the
    player's chrome and the poster card. They win because a class selector declared later beats the
    base one.
  - The search field too, which is how the prototype draws it.


- **Every cover is painted in its title's own colour, which is what makes a library look like a
  library.** They were all the same grey rectangle with two letters, and the reason written down was
  that this application ships with no artwork and no token to fetch any. Both are still true and the
  conclusion was false: **the prototype has no artwork either**. Reading its source on 2026-08-22,
  every cover in it is four CSS gradients computed from **a single hue** —
  `linear-gradient(200deg, hsl(H 38% 30%), hsl(H+34 46% 12%))` under a radial glow, a hatch and a ring
  — with not one image in the file. So the wall of colour costs no network, no TMDB token and no file
  on disk, and **none of the reasons artwork was ruled out of 0.2.0 apply to it**.
  - The hue comes from the **title**, the one thing the four lists behind `IPosterCard` share. With a
    rolling hash rather than `string.GetHashCode`, which .NET randomises per process: a library would
    be a different set of colours on every launch, and a colour that changes is one nobody can learn.
  - Three layers of the four. The diagonal hatch is a repeating gradient, which Avalonia has no brush
    for, and it is exactly the layer at 5.5 % alpha nobody would miss.
  - **The initials stay**, though the prototype has none: two letters say which title this is before
    the colour has taught anybody anything, and a colour alone is a distinction somebody who cannot
    see colour does not get.
  - **In the two high contrasts the colour is not drawn.** `PosterArtOpacity` is 0 there and the card
    falls back to the fill and its initials, whose contrast is measured: a hue picked by a hash is a
    ratio nobody decided, and somebody who asked for high contrast asked for the opposite of a
    decorative colour.
  - **And the gate that forbade it was declared against rather than relaxed.**
    `No_state_is_told_by_colour_alone` refuses a colour bound to the model, rightly. Instead of adding
    two rows to its exception list — which only shrinks — there is a **second list with its own
    reason**: here the colour stands in for nothing, it repeats what is already written under the
    card, over it, and in the accessible name. And the exception **is paid for**: a view on that list
    has to switch its colour off in high contrast, and a new test measures both halves — that the view
    reads the token and that the token is 0.


- **The player's column shows one panel at a time, with their names at the head of it.** That is what
  the prototype does and it was the largest change left: until now the five panels — tracks, audio
  output, series markers, detected segments and other versions — **were all mounted at once**, and
  reaching the last one meant scrolling 320 px of column. Each has its own tab now, and **a tab only
  exists if its panel does**.
  - **No view model decides which one opens, and that is measured.** Avalonia 12.1.1 **skips an
    invisible tab** when it picks the first — checked on 2026-08-22 with the first of three hidden,
    which opened on the second — so a session with no tracks opens on whatever it does have, with
    nothing in the shell working it out. Zero lines of code and zero new strings: each tab carries the
    name its own panel already declared.
  - **Five tabs and not the prototype's four.** Its four names — Audio, Subtitles, Video, Markers —
    are its division, not this application's, which keeps five panels; grouping two under one tab
    would need either a word nobody has approved or a sixth boolean on the shell.
  - **And the walk caught it, which is exactly what it is for.** Four scenes went red at once saying
    "matched 0 controls on screen", and they were right: a panel that is not the open tab **is not in
    the tree**, so the click had nowhere to land. All four now open their tab with the mouse before
    pressing inside it, and the ratchet stays at **0 pending** (137 identities, 137 pressed).


- **In the light theme the player's panel column had invisible text, and the gate that should have
  caught it was looking elsewhere.** Measured on 2026-08-22: `TextPrimaryBrush` (#111827) on
  `PlayerSurfaceBrush` (#0B0D10) is **1.10:1** where WCAG AA asks for 4.5:1. The player's surfaces are
  dark **in all four modes** — decided from the start, so the picture keeps its contrast — while the
  theme's ink is dark in two of them. The result: on any machine with Windows set to light, the track
  pickers, the audio output, the marker list, the detections and the versions panel **could not be
  read**, and neither could the transport's clocks or its three pictograms.
  - `ContrastTokenTests` measured primary text on **seven** surfaces, and **the player's three were
    not among them**. The hole was exactly the shape of the defect. There are nine now, across all
    four modes, with the transport's translucent band left out for the reason already written there —
    a contrast ratio against a translucent colour is a guess at what is behind it — and measured
    instead against the surface it is drawn over.
  - The fix is two brushes, `PlayerTextBrush` and `PlayerTextSecondaryBrush`, light in all four modes,
    and **set on the container** wherever that is possible: Avalonia inherits `Foreground`, so the
    header, the column and the transport band cover in one go all the text that carries no brush of
    its own. The three amber notices in the audio output and the five overlays above the picture **do
    not change**: they paint their own background from the theme's colours, and there the theme's ink
    is the right one.


- **The review inbox draws the prototype's candidate card: the border tinted by state, the badge in
  the top right corner, and two columns underneath.** It used to be a neutral-bordered rectangle where
  the key, the percentage, the state word and the "Why" heading ran together over a line and a half,
  with nothing saying which of them was the answer. Now **the whole border is tinted** — accent when
  the match is suggested, amber when it is pending — which is what the prototype does and **the only
  signal that survives both high contrasts**, where the card's surface and the page's are the same
  colour. Underneath, what is proposed on the left with **a confidence bar that never existed** —
  drawn from the same number the percentage is written from, so the two cannot disagree — and the
  reasons on the right, as bullets.
  - Two strings, both the prototype's own and in both languages: `ReviewProposedCandidate` and
    `ReviewConfidence`. "Signals considered" does not join them: `ReviewExplanationHeading` — "Why" —
    already said that and had been saying it for two days.
  - **Three things the prototype draws and this cannot, and they are measured omissions rather than
    oversights.** `MatchCandidate` carries an id, a key, a kind, a score and its signals, and **no
    artwork**: an empty 2:3 thumbnail in every row would promise a picture that does not exist. The
    candidate's **title** is not there — what is there is the provider's key, `movie:329865` — and the
    **kind** would need the words "Película" / "Serie", which the string package does not propose and
    which was already decided against on 2026-08-22. And **the four buttons at the card's foot** exist
    one row lower: Accept and Reject act on whatever the list has selected, and moving them into every
    card would turn one decision per inbox into one decision per row — a change to how the surface
    works, not to how it is drawn.
  - The state picks the class from the model, with `Classes.suggested` and `Classes.pending`, rather
    than through a converter: the two states are already two booleans on the card, and a converter
    would be a third place deciding which is which.


- **Settings moves to the prototype's row-card: the name, the sentence underneath it, and the
  control against the right edge.** That is the unit the prototype draws a setting with, and there
  was not one here: a switch with its label inside it, then a loose sentence about that switch, then
  the next one — which reads as six things where there are three. There are now **eighteen cards plus
  one template that produces the eleven shortcut rows, across eight of the page's ten sections**, and
  the sentences describing them are the ones the page already wrote; none were invented. The switches
  lose the label they carried inside and **keep the accessible name they already declared**, so a
  screen reader hears exactly the same thing and only the paint moved: the same trade the rail made
  when its destinations became pictograms.
  - **The state word to the left of the switch** — "Activado" / "Desactivado" — which the prototype
    carries in both languages and this tree did not have. It is **silent to the screen reader**: the
    checkbox beside it already announces whether it is checked, and a second text saying the same
    thing would say it twice, the second time in a voice that cannot be wrong about it. The same
    decision the magnifier took.
  - **The eleven keyboard shortcuts and the six subtitle-style controls** join the same grammar, and
    on the way **the three subtitle sliders show the number they are setting for the first time**.
    The size is written with `StringFormat` and the per-cent sign **outside** the numeric specifier,
    where it is a literal: inside it, `0 %` multiplies by a hundred and writes 8000 for eighty.
  - **In Appearance the pills sit under the name rather than against the right edge, and that is
    measured rather than conceded.** A `WrapPanel` in an `Auto` column is measured with infinite
    width, so it lays every pill on one line and only re-wraps at arrange time — the infinite-width
    shape this repository has now caught nine times, and exactly the one `AppearanceSettingsTests`
    exists to refuse around these buttons. The card changes; the geometry inside it does not.
  - And one more string, `AppearanceThemeLabel` — "Theme" — because that setting **had no name of
    its own**: the one on screen was its section's, and a section title has to stay outside the cards
    or it stops starting where the other nine start.


- **The magnifier inside the search field and the `+` beside "Add folder".** Both come from the
  prototype and both change only what is drawn: the magnifier is **decoration rather than a control**
  — the field already carries the accessible name, and a second name there would have a screen reader
  say "Search the library" twice — and the `+` sits **beside the word rather than instead of it**,
  because a pictogram alone is right in a 64 px rail with no room for a word and wrong on the one
  action of a screen somebody is seeing for the first time.


- **The player leaves the rail in view, heads itself, and only takes the column it uses.** Three
  things the prototype draws and this did differently: the session covered both columns, so **opening
  a film took the five destinations away with it**; its three buttons — close, mini player and
  fullscreen — carried words at the head of a column of panels, and are now three pictograms in a band
  above the picture, with the same names for anybody using a screen reader; and that 320 px column was
  there **whether or not one of its five panels existed**, so a file with a single audio track, no
  markers and no other version left an empty rectangle taking a fifth of the picture's width. The
  width goes back to the film when there is nothing to put in it.


- **The player says where you are and how much is left, and its transport is a band rather than a
  card.** The model had a position bar all along — position, duration and a jump to a chosen minute —
  and **nothing painted it**: all three were read on every state change and thrown away, so somebody
  watching a film could not see where they were in it or take it somewhere else with a pointer. It is
  there now, elapsed on the left and length on the right, with **the hour shown only when there is
  one**. Below it the controls sit on one line, with the volume level written as a number — which was
  also missing — and the speed. The bar **stays away until the engine says how long the file is**: a
  thumb halfway along a bar of unknown length points at nothing, and a greyed bar would say "not for
  you" where the truth is "not yet".


- **«Add media», at the foot of the navigation rail.** It is what the prototype puts there and the
  first thing somebody opening the application with an empty library needs. It goes to the screen a
  folder is added on **and arrives with the form empty**, which is the half that tells it apart from
  the Library destination: nothing cleared the path after a folder was accepted, so somebody who
  added one and came back found the previous folder still typed in and a second press answered "it is
  already in the library" — a refusal caused by the screen rather than by them. The same for a warning
  about a rejected path, and for a removal somebody left half-confirmed.


- **The brand and the publisher's signature move to Credits, which is where they belong.** They were
  at the foot of the 248 px navigation; the 64 px rail has no room for them, and repeating the name in
  the title bar would have written it twice — Windows already draws it there. **And this was not only
  placement**: TMDB's terms ask for their logo to be **less prominent than the product's own name**,
  and taking the name out of the rail left the application not writing its own name anywhere. Both are
  on one screen now, which is what that condition actually means.


- **Two of the package's four animations, and a system preference that really switches them off.**
  Each rail destination's tooltip slides in, and the dot beside "Scanning" breathes while a scan runs
  — the only thing on that row that says it is still working between two jumps of the counter. With
  "Show animations in Windows" off they last **zero**, not less: the theme writes the duration the
  animations read.


- **Navigation becomes a 64 px rail of icons, and the application draws its own title bar.** It is
  the prototype's composition: the five destinations are pictograms from the font Windows 11 ships,
  the open one is marked by a fill **and** a 3 px bar — two signals, one of them not colour — and each
  destination's name still reaches the tooltip and the screen reader. **No label was rewritten**, so
  all five answer to the names they always had. The 44 px title bar makes the window one unbroken
  surface from the top down; Windows keeps drawing minimise, maximise and close over it.


- **The "Continue watching" block is now the hero the design asks for.** The title is large and
  light, an overline labels it without spending a heading level, and the progress bar is the same
  3 px rule the cards carry, with the percentage in words beside it. **What it does not carry, and
  why**: no artwork, because initials next to a title already written large say the same thing twice;
  and not the prototype's "Details" button, because opening a title's card needs catalogue data Home
  does not hold and no query can return by id.


- **The library looks like a grid that follows the window.** Where there was a one-column list there
  are now cards in a grid, and narrowing the window rearranges them. Measured over ten thousand
  titles: the grid takes **6 ms** and keeps **36** cards alive, against **4559 ms** and **ten
  thousand** for the naive shape. On a large library that is the difference between scrolling and
  waiting.


- **Films and series look like cards.** Where there was a line of text per title there is now a 2:3
  card carrying the title's initials, the title on at most two lines and the year underneath, in the
  library and across the three rails on Home. There is no artwork and there will be none in this version — this
  application ships with no connection that could fetch any — so the initials are not a hole waiting
  for a picture: they are what the card shows, and they differ from card to card, which is what makes
  a wall of them scannable.


- **Choosing a folder kind shows which one is chosen.** The three buttons - Local, USB, UNC or NAS -
  set a choice the screen never showed anywhere: pressing "USB" left everything exactly as pressing
  "Local" did. They now carry the same circle the theme and language options use. The path box also
  gains its visible label: it was written in both languages and only the screen reader heard it.


- **An empty library says it is empty.** With no folder added - which is how everybody starts - the
  first-steps screen said nothing at all: no list, no heading, no explanation. It now invites you to
  add the first one and steps aside as soon as there is one.


- **The shortcut list says something when nothing is bound.** A blank panel suggested the application
  ignores the keyboard; what is happening is that nothing is bound, and the system's own media keys
  keep working either way.


- **The four lists in the player's side column say something when they are empty.** Markers,
  detections, tracks, and versions used to go blank, with no way to tell "there is nothing" from
  "still loading". Each now explains its own empty: markers say nothing is written to your video file,
  the track selector says this file carries a single track of each kind, and the versions list says
  "only one version" **instead of disappearing**, so the column does not shift and the answer is where
  you looked for it.


- **The library search gains a button to clear it.** Getting the whole library back meant selecting
  what you had typed, deleting it, and pressing Apply. It is now one press. With the box empty the
  button stays where it is, greyed out, so the row of controls does not move on every keystroke.


- **Searching and finding nothing now tells you.** Until now a search with no matches left the screen
  blank, without a single line of text. It now says there are no results and why — the current search
  and filters — and it does **not** say your library is empty, because it is not.


- **A check that stops a control from ending up off the screen.** It is the defect that has come up
  most often in this application — seven times — always the same: a row of buttons with translated
  text that does not fit and leaves the last one half outside, where nobody can press it. All
  forty-eight screens are now measured at once against the smallest window the application allows,
  with every notice and state visible simultaneously, which is wider than they ever really are.

- **The update screen says which action is its own.** "Check for updates" was painted exactly like
  the other three buttons, so nothing on screen said what the screen was for.
- **The mini player gains its five controls.** Pause/resume, two ten-second skips, back to the main
  window, and close — always visible, and each named for a screen reader. The window used to drop
  everything it declared for itself the moment a session arrived, because the coordinator assigned
  its whole content; now the chrome and the picture share the window.

- **An independent updater.** It checks for a newer version only when you ask or have allowed it,
  tells you what changed in Spanish and English, downloads into a folder of its own while verifying
  the published hash and size, and hands the package to Windows only after a confirmation that names
  that version. A download that is cut off resumes from where it stopped; one that does not match is
  deleted. Your library takes no part in the download: the database and the running binary were
  measured to be untouched after a correct, cancelled, tampered, and interrupted update. The Store
  keeps using its own channel.
- **A Windows Package Manager entry.** Every build leaves the winget manifest generated from the
  archive itself: the hash is the published one, the executable it declares has been found inside the
  ZIP, and the descriptions come from the two READMEs. winget costs nothing and requires no
  certificate, so it is the first way to install this that will be available.
- **A native ARM64 package.** `win-arm64` MSIX and ZIP, built and verified: every binary in the
  payload carries ARM64 in its header, LibVLC travels where the loader looks for it, and the
  application is identical to the x64 one file for file. It is built on every verification and every
  release.
- **Automatic intro, recap, and credits detection.** It compares each series' episodes with each
  other locally and finds the audio that recurs; nothing leaves the machine and not a single
  connection is opened. Detections are stored per episode with their confidence, a manual marker of
  the same kind always suppresses them, and accepting or correcting one protects it from every
  later run. The work yields to playback, and the option stays off until you turn it on. Evaluated
  against a held-out corpus of synthetic series: every approved threshold passed on the first
  measurement, with zero spurious detections on the segment-free episodes.

- **Refresh the oldest entries on their own, if you turn it on.** When the application opens, entries
  whose details are more than 90 days old can be asked about again: at most 20 at a time, oldest
  first, and only for titles that are already identified. It ships off, the switch lives in Settings
  → Privacy, and it **is not even offered if you have not consented to the connection**, because
  without one there would be nothing to ask. It never happens while a scan is running or a video is
  open, and that is checked before each entry rather than only at the start. With the switch off no
  connection is opened at all: measured with the network canary, which in the same run counts zero
  with it off and two with it on.
- **A permanent guard against the house defect.** The audit found components registered that the
  application never invokes, and every case was hunted by hand; an architecture test now requires
  every registered service to be resolved at least once outside its own registration. Its first run
  enumerated 32 orphans — not the estimated ~12 — and uncovered new faces: the audio device
  selection never reaches the engine, stored playback preferences are never applied, the watched
  toggle is wired to nothing, there is no way to remove a library folder, and choosing a duplicate
  version does nothing. Each debt lives inside the test under its own identifier, and a second
  assertion evicts the entry the moment its wiring lands: the list can only shrink.

- **Opening a video can no longer leave the window sitting still waiting on the keyboard.** As each
  playback starts, the application claims the keyboard's media keys, and it stood still — the thread
  that draws the window included — until that registration answered, with no deadline at all. If it
  never answered there was no way out: the same trapped thread was holding the latch needed to cancel
  it. The wait now has a deadline and happens outside the latch, and if it runs out playback starts
  anyway: the keyboard's media keys are an extra, and a session without them beats a session that does
  not start.

- **No button can close the application by failing any more.** Every surface with buttons brought a
  hand-written command class of its own — twenty-four of them — and not one picked up a failure, so an
  error in the work behind a button ended the program. There is one class now, and it always picks the
  failure up: two places in the whole codebase are left where an await can end with nobody watching,
  and both of them catch. Unifying them surfaced two behaviours only one surface had: the rating on a
  card checked the value before storing it — a check that would have been lost in silence had a test
  not been there — and the player's skips refuse the next one while one is under way. Both stayed.

- **A failure can no longer take the application down with it.** Until now, when the work behind a
  button went wrong, the exception was returned to nobody: it was rethrown on the interface thread,
  where the only thing waiting for it was the end of the program. There is a net now — what reaches
  the top of the process is written down as a code instead of ending it, and a task that fails with
  nobody watching is caught before it becomes a shutdown. And the diagnostics report, which has
  existed for a while and could only ever talk about renames, finally covers the rest: until now, in a
  session where nobody renamed anything, an application that was failing looked like a healthy one.
  What is written down lives in memory and only for that session: nothing is written to your disk, and
  what travels from an exception is its type, never its message.

- **The application checks its own wiring as it is built.** A component asking for something nobody
  registered used to be a failure that waited for the first screen that needed it — possibly yours,
  in a corner no test opened. That check now happens as the application is assembled, so the failure
  shows up at any test's startup instead of in front of somebody. It covers 109 of the 156
  registrations: the 45 built through a function of their own stay opaque to the check, and saying so
  beats letting anyone believe they are covered. It costs 0.22 milliseconds per startup. On its first
  run it found not one broken wire.

- **Language preference.** Settings → Appearance lets you choose Spanish or English. The interface,
  the update summaries, and the metadata speak the same language — the interface used to be pinned
  to Spanish while the updater's summary and the metadata followed the machine's language, and
  could arrive in another. Metadata uses the new language after a restart.
- **The window returns to where it was.** Position, size, and state (maximized or not) survive the
  close, whatever path closes it. A position stored on a monitor that is no longer connected is
  discarded instead of opening the window off every screen, and a window closed maximized reopens
  maximized over its restore bounds.

- **Home shows what was added recently.** The application already read the titles that most recently
  entered the library from its own database — twelve on every load, ordered by when they were added —
  and painted them nowhere: they were read and thrown away. There is now a rail for them, with the
  title on at most two lines, the year in a secondary tone, and the badge for a medium that is not
  reachable right now.

### Changed

- **A card's title takes one line, and a toggle is the same pill as the button beside it.** Two
  things seen by looking at the real application. The first: a title that went to a second line
  pushed its own year below the year of the card next to it, and a row of cards read as a ragged
  edge; it now ends in an ellipsis — the full title is still announced by the button that wraps the
  card, which carries it as its name. The second: the pill shape was declared for `Button` alone,
  and a `ToggleButton` does not match that selector, so "Favorito" and "Ver más tarde" kept the
  base theme's shorter box, its square corner and its own padding, beside the pills in their own
  row. The tree's three toggles now line up with the buttons, and a test asserts it by comparing
  the two controls' geometry.

- **Decorative borders are finally the prototype's hairline.** Eighteen outlines — the notice and
  status panels, the add-folder dialog, the detected-kind chip, the loose-file banner, the rail's
  seam and the updater's two cards — wore the strong border the house reserves for control
  boundaries, where 3:1 is an obligation. The prototype paints them with its hairline (white at
  7% in dark, ink at 9% in light), which this tree already had traced and spent only on setting
  rows and cards. The duplicates comparison card also gains the card ground the prototype gives
  it, instead of floating as a drawn box. Two stay as they were, with arithmetic: the library's
  dashed empty state (the prototype draws it with its strong border) and the player's five
  overlays, whose prototype white at 18%, composed over their ground, is exactly the strong
  border they already wore.

### Fixed

- **Home started empty even with something half-watched, and only filled itself after leaving and
  coming back.** The route the application is born on never goes through the route navigator, so
  the "navigated" notice — the only place surfaces are fed — never sounded for the first screen:
  Home knew how to read itself and nobody asked until another section was visited and abandoned.
  It is the house defect's fourteenth shape, this time in the minute every user sees. Now the
  shell, as soon as it is built, replays its initial route through the same path a real navigation
  takes, so the first screen and the navigated ones share the only load path there is.

- **The position bar could take playback to second one by itself.** It never shipped: it was
  measured while writing its test, the same day the bar arrived. An Avalonia `Slider` clamps whatever
  is written into its value against the maximum it holds **at that instant**, and the model announced
  the position before the duration — so 120 seconds went into a bar whose maximum was still 1, the bar
  clamped them to 1, and the handler turned that clamp into a real seek. The first state after a
  two-minute seek came back reading 0:01.


- **Home stopped halfway and the rest was drawn outside the window.** "Recently added" and
  "You might like" existed, had their text in both languages and never reached the screen: the rail's
  row took all the leftover space and the two sections under it fell past the bottom edge. Measured at
  1600 x 1000. Home scrolls now and all five sections can be reached.


- **The failure screen no longer offers another version when there is no other version.** It was
  decided by the failure reason alone, without looking at whether the content had any alternative
  catalogued, so in the commonest case - a file that is the only one of its title - it invited you to
  choose between one. It now appears only when there is genuinely something to switch to, and says so
  instead of sending you to another screen.


- **A button in the library could be seen and could not be pressed.** "Review versions" sat flush with
  the bottom edge of the scrolling area: thirteen of its thirty-six pixels were inside it and its
  middle was not, so a click on it never reached the button. The list now leaves a margin at the end,
  which is what makes the last control on a screen usable.


- **A refusal, a folder removal, and a request for permission no longer look alike.** All three warned
  in the same colour on the folders screen. A refusal now reads as a warning, removing a folder from
  the catalog reads as what it is, and the request to allow the first scan keeps its neutral tone.


- **The notices that appear over the video can no longer take the whole screen.** The resume offer, the
  next-episode notice, and the version-switch question were centred but still grew without limit: with
  a long text inside, one of them took 1278 pixels of a 1280-pixel screen. They now have a maximum
  width. And the skip button sits at the bottom right, out of the way.

### Fixed

- **The subtitle preview finally shows the subtitle you chose.** It only ever showed the font: the text
  colour, the background colour, the opacity, and the outline all changed with nothing to see for it.
  And it now sits on the same black as the player, because a colour judged against the grey of a
  settings page is not the one you will see over a film.

### Added

- **The review inbox says when there is nothing left to review, and says it as the good news it is.**
  An empty tray means AP Reelume identified everything it found without needing you; it used to be a
  blank panel, which reads more like something failed to load.

### Changed

- **Settings finishes lining up.** Three of its ten sections — subtitle style, updates, and credits —
  still started further left than the other seven. They no longer do.


- **The state circles stop looking small.** The `○ ◐ ●` that mark whether you have watched something,
  which destination you are in, and which theme is on were drawn at two thirds the size of the player's
  symbols — just enough to read as a stray character instead of a state. All thirteen move to one size.


- **Duplicates are compared side by side.** Copies of the same title were stacked one under another,
  so comparing them meant scrolling between them — which is what that screen exists to avoid. They now
  sit in two columns, with the quality figures in a fixed-width font so they line up under each other,
  and a third copy drops to the next row on its own.

### Fixed

- **Restoring asked for a new folder for every root, including the ones that are where they should be.**
  The box for typing a path now appears only where it is needed: when the folder is not there, or when
  there is a conflict. And as soon as you type one, the row stops saying the folder is missing.


- **A backup that fails no longer looks like one that worked.** "There is not enough room on the disk"
  was painted on the same background as "Done". Cancelling is still not a failure: nothing was left
  half-written.


- **The backup screen did not show the copy you had just made.** The program kept the name of the last
  copy and the last export and showed them nowhere, so the only way to check was to open a file
  manager.


- **The database recovery screen said something had broken in the gentle colour.** The failure detail
  sat on the same background as friendly notices, when that screen only appears because your library
  could not be opened. It now looks like what it is. And both paths are in a fixed-width font, which is
  what you need to go and find your backup by hand.


- **The rename preview was hiding the part that changes.** Both paths were cut off at the end, and the
  end is the file name — the only thing that tells the source from the destination. They now shorten in
  the middle, keeping both ends, and use a fixed-width font so they line up under each other. The arrow
  between them now says what it means to a screen reader.


- **The metadata editor had eight fields with no visible label.** Title, original title, overview,
  year, genres, poster, backdrop, and the artwork's alternative text: all eight announced their name to
  a screen reader and were eight identical boxes on screen. Each now carries its label above it.


- **The metadata editor's notices looked like loose text.** A conflict and an unidentified title are
  two forms of "what you asked for did not happen", so they now carry an amber box and a symbol; a
  provider with no answer right now is nobody's failure and stays a plain fact.


- **The review inbox explained its decisions with the program's internal names.** When reviewing why AP
  Reelume thinks a file is a particular film, the list of reasons said things like
  `Identification.Signal.Title` — which is exactly what that screen is for. It now says "The title
  matches", "The year matches", "The file name reads more than one way", and the other eight, in both
  languages. A screen reader recited them too, and now hears words as well.


- **The "this system has no tray" notice looked like another fact.** It was a plain sentence in the
  same colour as the labels beside it, when what it says is that something you asked for could not be
  done. It now carries an amber box and a symbol, like every other warning.


- **The diagnostics preview made you scroll sideways.** It is the text you read to decide whether to
  share something, so it now wraps and can be read in full.


- **One scan setting had no visible label.** The recovery-interval box announced "Recovery interval in
  minutes" to a screen reader and showed nothing at all on screen: just a number box. The label is now
  written where it can be read.


- **The scan page did not say what it did.** It had a title and two controls and nothing between them.


- **The reduced-motion notice said the same thing whether the setting was on or off.** "AP Reelume
  respects the Windows reduced-motion preference" is a sentence about the program's intentions, not
  about the state of your machine — and the program already knew the answer. It now says which of the
  two states you are in.


- **The marker and detection lists were showing the program's own internals.** Each row painted
  something like `IntroMarker { Id = 1111…, SeriesId = SeriesId { Value = d1f7… }, Kind = Intro, … }`:
  two internal identifiers and a class name, cut off at the edge of the column with no way to read the
  rest. Each row now says what it is — "Intro · 0:30–2:00" — and when the text does not fit it ends in
  an ellipsis with the whole of it in the tooltip.


- **The marker kind picker was untranslated.** It offered "Intro", "Recap", and "Credits" in Spanish,
  which are the three kinds' internal names. It now uses real words, and the same words appear in the
  lists.


- **Accepting a detection changed nothing you could see.** Accepting or correcting a detected segment
  is what protects it from the next detector run, and the list looked exactly as it had before. The row
  now says so: "Credits · 46:40–50:00 · confirmed".

### Changed

- **Settings is one page and now reads like one.** Its seven sections started in two different places,
  four of them titled at the size of a page title, and the page had no title of its own. The page is
  now headed "Settings" and the seven sections are aligned and one size. For anybody navigating by
  heading with a screen reader, this used to be one destination with four top levels inside it.


- **The settings pages look like each other.** Scanning, recommendations, and segment detection had a
  smaller title than the appearance page, no margin around them, and no reading width. All four now
  share the same skeleton.


- **The "this file is not in your library" notice is no longer drawn over the film.** It moves to a
  band of its own above the picture, with its action on the right. It used to sit on top of the video
  and, because it carries a background, swallowed clicks meant for what was behind it; and switching to
  the mini player **took it along**, into a window where it asked for more height than the window has.


- **The four lists in the player's side column have rows of one height.** Markers, detections, tracks,
  and versions move to 36-pixel rows that never scroll sideways: what does not fit is cut with an
  ellipsis and read in full in the tooltip. In the versions list a long label used to take several
  lines of varying height.


- **The player buttons carry symbols instead of words.** Play, pause, stop, both skips, mute, and the
  mini player's five now use the Windows pictograms. This is not a preference: with translated words
  the mini player folded its five buttons into three rows inside a 480x270 window, and its minimum
  width is narrower still. **What each button tells a screen reader has not changed**, and it is still
  translated into both languages; what changed is what you see. The seconds each skip covers are still
  announced the same way.


- **The player buttons are easier to hit.** Play, pause, stop, and the two skips go from 36 to 44
  pixels of target area: that is what the accessibility guidelines ask for a comfortable target, and
  these are the controls people press in a hurry, in the dark, and sometimes on a laptop trackpad.


- **The three sound notices look like notices.** No device at all, a downmixed layout, or a device that
  disappears mid-session were plain sentences in the same colour as the labels beside them. All three
  mean the same thing: what you are hearing is not what you asked for. They now carry an amber box and
  a symbol, like every other warning in the application.


- **The video status badge tells a fact apart from a warning.** It said six things that all looked the
  same: HDR passing through, or the graphics card decoding — facts about a video playing perfectly —
  looked exactly like "this fell back to software decoding". The facts are now secondary text and the
  two warnings carry their own amber box with a symbol. None of the six is an error: the video is
  playing.


- **The player has a background of its own, and it is the same under every theme.** Everything else in
  the application follows the light or dark theme you pick; the player does not, because what sits on
  it is the picture. It is a very dark grey rather than pure black, so the bars above and below do not
  read as a hole beside a frame that is almost never fully black.


- **A video that will not open looks like a failure rather than one more piece of information.** The
  notice that something went wrong was painted on the same surface as everything else, so the one
  screen that has to say "this did not work" looked like the one that tells you which codec you are
  using. It now has its own fill, border, and symbol — and its symbol **differs** from the "not
  available right now" one: two things told apart by colour alone are not told apart for everybody.
  Its two buttons also wrap when they do not fit.


- **A series' card shows one season at a time.** It used to stack them all, so an eight-season series
  was a page with no end in sight. There is now a picker at the top and the chosen season's episodes
  below it. With a single season the picker **does not appear**, because there would be nothing to
  choose.


- **A series' episode rows are all the same height and their numbers line up in a column.** Every row
  is the same height, so a season no longer looks like a list that is half loaded, and the episode
  number is right-aligned in a fixed column: 9 and 10 end at the same point.


- **The rows of buttons in the library and on the cards wrap onto more than one line when they do not
  fit.** The search and filter row, and the action rows on a film and on a series, used to leave the
  last button off the window as soon as the screen was narrow or the translation long. They now move
  down a line, which is what the title bar buttons already did.


- **A medium that is not there stops looking like an error.** The "not available right now" badge — the
  one on titles from an unplugged drive or a network share that is down — takes the shape of a warning:
  amber fill, a border, and a symbol in front, so it never depends on telling a colour apart. And it is
  said one way across the six screens that showed it, each of which used to draw its own.


- **With recommendations off, Home no longer says there is nothing to suggest.** It said "there is
  nothing to suggest right now", which was false: switched off, nothing is computed and the catalogue
  is never read, so the application was making a claim about films nobody had looked at. The rail now
  says what actually happens, and the empty state — on, with no results — is told apart from the off
  one.

- **Progress on in-progress cards is a thin rule at the foot, not a bar in the middle.** Three pixels
  of the accent under each card, with the percentage written above it as before: the bar is never the
  only thing that says it. And Home's blocks are further apart, so the rails do not read as part of
  whatever sits above them.

- **The sidebar says which screen you are on in two ways, neither of them colour.** The open
  destination now carries an accent bar to its left as well as the filled dot it already had, so
  anyone who cannot tell those tones apart still knows where they are. And the three title action
  buttons wrap onto more than one line when they do not fit, instead of pushing the last one off the
  window.

- **Every screen now says what its action is.** Fourteen more screens paint the button that is their
  point in the accent colour — resume the film, save the record, create the copy, add the folder,
  accept the match — with the rest clearly beside it. **Sixteen screens highlight nothing, and that is
  a decision too**: a frame is not for anything in particular, a row that repeats cannot highlight
  anything, and on the two screens that ask your permission — start with Windows, export a diagnostic
  — **the yes is not highlighted**, because nudging towards yes on a permission question is exactly
  what this application does not do.

- **Rounded corners are two measurements now, not five.** Corners were chosen screen by screen — 4, 6,
  8, 10 and 12 pixels across twenty-six views — so two identical cards could round differently with
  nobody having decided it: of the application's seven card surfaces, four used one measurement and
  three used another. There are now **two**, and seven sites fall into line with the rest.

- **Spacing across the whole application is a scale now, not a hundred and eighty-six separate
  decisions.** Every view chose on its own how far apart to put things: eight distinct values — 2, 4,
  6, 8, 10, 12, 16 and 24 — spread across fifty-four screens. There are now **five measurements** for
  the whole application, and changing them changes the whole application at once instead of file by
  file. Seventeen sites move by 2 pixels and no more; the only thing that changes on the home screen
  is one pixel of a card's bottom edge.

- **The player's buttons are easier to hit, and the failure screen says what to do.** Play, pause and
  stop now have a minimum 36 by 36 pixel target area, the same one the mini player introduced. And
  when something fails, "Try again" is painted as that screen's leading action instead of looking
  like the button beside it.

- **Font sizes are a scale now, not thirty separate decisions.** Every screen chose the size of
  its own text: **thirteen distinct sizes** spread across thirty files, with headings that resembled
  each other without ever matching. There are now **five sizes** for the whole application, and
  changing them changes the whole application at once instead of file by file. The only thing that
  moves on the home screen is one pixel of a card's edge, and in the right direction: it used to
  depend on the system scale and is now the same at 100 %, 150 % and 200 %.

- **The "keep watching" button finally stands apart from the others.** It carried the mark for
  "primary action" and **no style defined it**, so the button that is the point of the home screen
  was painted like any secondary button beside it. At rest it now carries the application's colour,
  and on hover or press it answers **exactly like every other control**, which is what makes an
  application feel like one piece.

- **A check requires every measurement in the theme to be spent by something.** The numbers behind
  the look — spacings, corner radii, thicknesses — are declared in one place, and until now nothing
  stopped one being declared and never used. Three had got in: two animation durations that
  **repeated a number the application already held elsewhere** — with two tests watching the copy
  while nothing watched the original — and a symbol written by hand in all six places it appears.
  All three are gone, the guarantee those two tests gave has moved to where the real number lives,
  and a declared measurement must now either be spent or appear on a list that **can only shrink**.
  None of this changes what you see; it stops two copies of one number drifting apart.

- **Buttons have a shape and react, and their colours come from the theme.** Until now a button's
  border was transparent in all four of its states — it had no shape of its own — its resting,
  hovered and pressed colours came from the graphics library's base theme, and **disabled was painted
  exactly like resting**: the only thing between them was the text's grey. All four states now come
  from the same tokens as the rest of the application, with a one-pixel border visible in all four
  themes, and in high contrast hovering or pressing **inverts** the button — the fill takes the
  border's colour and the text the background's — because in those palettes a lighter shade would say
  nothing.

- **Sliders, toggles and options stop being painted by Windows too.** The five sliders — subtitle
  size and outline, video position, recommendation weight — the two buttons that stay pressed in, and
  the duplicate version picker **all** came out of the same system blue, byte-identical in the light
  theme and in the high contrast dark one. On top of that, a **disabled slider stopped saying where
  its value was** — both halves of the track went to one grey, and the video's slider is disabled
  whenever nothing is playing — a **button that stays pressed in had no border** in any of its ten
  states, so it had no shape of its own, and **disabled was painted exactly like resting**. The dot
  of a chosen option was white in all four themes. All three now come from the application's colours,
  a disabled slider still says its value, and a toggle has an outline.

- **Drop-down lists can be seen, open and closed.** The chosen row inside a drop-down stood apart
  from the others by **less than it takes to tell two shades apart**, exactly as a list row did and
  for the same reason: Windows' translucent blue. The panel that opens had no visible edge either —
  black at fourteen per cent — and an open drop-down floats over the window, so its edge is the only
  thing saying where it ends. And in the two high contrast themes, hovering a row painted it **the
  same colour as its own text**: black on black. The chosen row now carries a border in the
  application's colour as well as a fill, the panel has the same edge as everything else, and the
  text of the rows that invert comes from the colour that exists for it. All eight drop-downs.

- **Text fields can be read.** The grey hint that says what an empty field is for was painted with
  two layers of transparency on top of its colour, and came out at **less than half** the contrast it
  takes to read text. A switched-off field could not be read either, and had no shape: neither its
  text nor its outline reached the minimum. And the blue rectangle marking the field with the cursor
  was **the same blue in all four themes**, including the one whose focus colour is yellow. The
  background, the border, the text and the hint now come from the application's colours, focus uses
  each theme's focus colour, and in high contrast hovering **inverts** the field exactly as the button
  does. It reaches the five numeric fields too, which are a text box with two arrows.

- **A list says which row you are on.** The selected row was painted a translucent blue that stood
  apart from the others by **less than it takes to tell two shades apart**; the text on it read
  perfectly, so the problem was never reading the row but knowing which one it was. The selected row
  now carries **a border in the application's colour** as well as a fill, and in the two high contrast
  themes — where that fill is the page's own colour — the border is the whole cue. Every row carries
  the same border and only its colour changes on selection, so selecting one does not move its text.
  This reaches all 23 lists with data.

- **Checkboxes are no longer painted by Windows.** All eighteen in the application took their
  colours from the graphics library's theme, with three consequences anyone could see. A **checked,
  switched-off** box was unreadable in the light theme: a white mark over the grey beneath it, with
  less difference than it takes to make out a shape. The **outline of a switched-off box** did not
  reach the minimum either. And a **checked** box was always the same Windows blue, which is not this
  application's colour in any theme, nor the high contrast one in the two that are. The box, the mark
  and the label now come from the same colours as everything else, in all four themes, and in high
  contrast hovering or pressing **inverts** the box exactly as the button does. **In high contrast a
  checkbox used to be painted exactly as in the ordinary theme**, so turning high contrast on in
  Windows changed every control but this one.

- **A switched-off control looks different from a working one, in high contrast too.** In the light
  and dark themes the colour said it: a duller fill and greyer text. In the two high contrast themes
  **nothing said it** — those palettes have two colours and no third one to spend, so a switched-off
  control's fill, border and text were exactly a working one's. It is now the **dotted outline** the
  design asks for, drawn over the control and on all ten kinds that can be switched off: buttons,
  checkboxes, text fields, drop-down lists, list rows, sliders and the rest. One outline per control
  rather than one per piece: a drop-down list or a number picker holds a text field inside it, and two
  dashed rectangles a few pixels apart are not a signal, they are noise.

- **The application goes into high contrast when Windows is.** Until now the high contrast theme
  existed in the code and **no path selected it**: anyone with it turned on in Windows saw the same
  application as everybody else. It is now read from the system at startup and overrides the three
  appearance options, because it is a need rather than a taste — so the three options stay three and
  there is nothing to reconfigure. Whether that system theme is a light one or a dark one is decided
  by the colour Windows draws windows with, not by the theme's name, which is translated and which
  anyone can change. Turning it on while the application is open arrives on the next launch.

- **The focus rectangle is now a double one, and it shows on all ten kinds of control.** It used to
  be drawn by thickening the control's own border, which left two measured holes: a slider has no
  border to paint it on, and in high contrast light the border and the focus colour are the same
  black, so focusing changed one pixel of thickness and nothing anyone could see. It is now two
  concentric rectangles, one in the focus colour and one in the background's: what marks the focused
  control is its **shape**, which still reads in a theme where everything is black and white. In high
  contrast the yellow is now reserved for focus and the brand colour moves to blue or cyan, where
  both used to be the same yellow.

- **Closing a video now has a test for its slow case.** When a video closes, the application waits
  for its data to be released before letting go of the player — doing it the other way round is what
  brings the decoder down — and that wait has a ceiling so a close can never hang: if the ceiling
  runs out, the release still finishes on its own a moment later. That works, but **it was only ever
  exercised when the verification machine happened to be busy enough**: across five measurements of
  the same version, the ceiling ran out on one and did not on four. A test now asks for a ceiling
  shorter than the wait, so giving up is the only outcome the clock allows, and it checks that it
  happened. Three more closing decisions came with it — closing twice, closing while a player is
  still borrowed, and a ceiling that cannot wait at all — and a piece of data the video factory
  published and nothing read was removed. The file goes from measuring differently on the
  integration machine to measuring the same three times running.

- **The test that watches for avalanches of changes no longer passes when there is no avalanche.**
  When more changes arrive than Windows can record, the application learns that it has lost notices
  and walks the whole folder again instead of quietly ceasing to follow it; that has worked since it
  was fixed. What did not work was its test: it provoked the avalanche and **never checked that one
  happened**, so on the runs where none did it passed without exercising any of what it protects.
  The buffer size can now be asked for when the watcher is built — the application still asks for the
  maximum — the test asks for the minimum, overflows for real and asserts it. Measuring that turned
  up two more conditions left to chance — the error that does end the watching, and which changes are
  merged into one notice — and each now has its own test. The file goes from measuring differently on
  every run to measuring the same thing three times over.

- **Coverage watching is now measured where it is verified.** Each file's bar had been measured on
  the developer's machine and checked on the integration one, which has no sound card: seven files
  covering audio, video and timers read differently in each place, and one of them — the audio device
  catalogue — went from 79/61 to 32/11, because there is nothing there to enumerate. Coverage was not
  getting worse; it was being measured in the wrong place. The list now comes from integration
  itself, which publishes it on every build, and here it is only reported. What does not change: it
  still cannot get worse, and a file still leaves the list only by improving.

- **The library's Back button now uses the library's command, and that command announces when what
  it can do changes.** The button called the screen's code directly, so the rule that decides when
  Back makes sense — only away from the list — was never consulted. Wiring it up showed at once why
  that rule needs to announce itself: the film card and the series card both exist at the same time
  even though only one is seen, so the button asks on start-up, is told no, and without an
  announcement it stays **visible on screen and dead to the touch** forever. Nothing you can see
  changes today; what changes is that the redesign can no longer break it quietly. And so it cannot
  come back, a check carries the closed list of the seven commands that stay silent on purpose, each
  with the reason it may: if an eighth appears, or if one of the seven has its rule changed, it fails
  in the same change that introduces it.

- **Carrying on where you left off is now really verified, and with the mouse.** Until today only the
  *request* to open at the stored point was checked; now what is checked is that it **opens there**,
  against the real video engine, and that the four buttons of the two offers — resume, start over,
  play the next episode, and don't — do what they say. Each offer answers once and withdraws, so the
  check opens the player four times, the way a person would.

- **Making a marker, skipping it, and deciding what detection proposes are now verified with the
  mouse.** The seven controls of the three marker surfaces are pressed against an episode that is
  really playing, and what is checked for each is **the row in the database**, not the list on
  screen: a surface that removed something from its own list and stored nothing would look identical.

- **Choosing the audio track, the subtitles and the sound output is now verified with the mouse.**
  The five controls in the player's side column — the two track lists, the series box, the output
  device and the channel layout — are pressed against a session with real video decoding, using a
  sample that carries **two audio tracks and one subtitle track** so the lists have something to
  offer.

- **Cancelling a backup halfway is now verified by pressing the button while one copies.** Cancel only
  exists while the copy runs, and with a test-sized library the whole copy is over in **51 ms**: there
  was no window to press it in. The check now seeds a real person's library — 3,000 titles with a
  poster and a backdrop, 293 MB of images — and with that the copy takes four seconds, room enough to
  press. What is checked is what matters: that cancelling says cancelled **and that nothing is left in
  the backups folder**, because a half-copy published would be worse than none. The application did
  not change: what takes time is copying what is there.

- **Cancelling a download halfway is now verified by pressing the button while one runs.** Cancel
  only exists while something is in flight, and in an automated check the package sits in the folder
  next door: the whole download finished in milliseconds, before there was anything to cancel. A test
  run can now ask its own folder to answer slowly, and the button is pressed with the download still
  arriving. What is exercised is the real path — the same cancellation your click produces, the same
  interruption, the same "Cancelled. Nothing was installed." on screen — and the staging folder is
  checked to hold no package afterwards. **Nothing changes in your installation**: the wait lives in
  the file a test run writes for itself, not in the application.

- **Downloading the update and confirming it are now verified end to end.** And what is verified is
  the real thing: the download that runs is **the same one** your installation uses, so the hash and
  the size the release promises are checked against what arrives, and the file lives under a
  provisional name until they match. All that changes in a test run is where the bytes come from —
  its own folder instead of the network. The opposite is checked too: given a package that is not the
  promised one, the download refuses it and leaves nothing behind, which is what shows no check was
  loosened in order to test.

- **Checking for updates is now verified without asking anybody anything.** The button asked GitHub
  what had been published, so any automated check would have made that query from whichever machine
  was doing the measuring. A test run now reads the version it has itself described in its own
  folder, and no connection is opened. What decides whether a version is worth offering — newer, your
  architecture, a hash, and notes in both languages — is unchanged. Your installation still asks
  GitHub.

- **Handing the update to Windows is now checked too, without starting an installer.** Installing
  here means giving Windows the package and stepping aside, so any automated check started a real
  installer on the machine doing the measuring. A test run now writes down which package it would
  have handed over, as it already did for the backup folder. On the way, something that lived in a
  comment moved to where it is decided: on a Windows with nothing registered for `.msix` the call
  does not fail — it simply starts nothing — and that is a **refusal**, not a success; for a folder,
  starting nothing means it landed in a window you already had open.

- **The permission to look for updates on its own is checked by pressing it.** It is the switch that
  decides whether the application opens a connection you did not ask for, so what it decides has to
  survive closing the window: the check now presses it with the mouse and goes and reads **the file**
  it is stored in, rather than believing the box. Pressing it contacts nothing.

- **The screen that appears when your library will not open can now be checked without destroying
  the check.** That screen offers two things: showing you the folder where the backup copy would be,
  and leaving. Neither had ever been pressed by anything but a person, for a reason that explains
  itself: a check that pressed the first would open an Explorer window on the machine doing the
  measuring, and one that pressed the second would **end the program doing the measuring**. A test
  run — the one that is not yours — now writes down what it would have handed to Windows instead of
  handing it over, exactly as it already did for the trailer link and the backup dialogs, and both
  buttons are pressed with the mouse while what folder they would have shown is checked. It is the
  one screen in the application no route leads to — it appears only if your library will not open —
  so until today nobody had walked it at all. Your installation is unchanged: Explorer still opens,
  and leaving still leaves.

- **Checking that the program installs, upgrades, repairs and uninstalls properly is now done by the
  repository, not by somebody remembering.** Those four things are Windows' to do, not ours, so they
  can only be measured by really installing on a clean Windows; until today that was a written
  procedure somebody had to follow by hand, and the measurement expired every time the file Windows
  reads to install it changed. One command now prepares the package, builds one of the next version
  to test the upgrade, carries it all into a disposable virtual machine, runs the whole cycle and
  brings the result back. Measured this time: the "Open with" association is registered for all
  eight video types, the library survives the upgrade untouched, Windows refuses to go back to an
  older version, repair works, and uninstalling **does not take your library with it**.

- **Copying your library out and bringing it back are now tested by pressing their buttons, not by
  calling their code.** The two buttons that ask where to save or what to read ask a Windows dialog,
  and no automated check can answer a dialog: so creating a copy, exporting it, choosing an archive
  and confirming the restore were four things nobody had ever exercised the way you use them. A test
  run — one that is not yours — now answers those two questions inside a folder of its own, and the
  check exports the library, reads it back and restores the whole thing, looking at the disk at every
  step instead of believing what the screen says. Your installation is unchanged: the usual Windows
  dialog still opens. Along the way it confirmed, for the first time from the complete application,
  that a restore works with the program open and the library loaded.

- **The tool that signs releases did not compile, and nothing could say so until a publication.** It
  was missing the licence header this project requires in every file, and that rule is a build error
  — but its project **was not in the solution**, so none of the checks that run on every change ever
  built it. The first place it would have surfaced was a real publication, at the step that verifies
  the signature is good. The project is inside now, so every check that already existed covers it,
  and a new test fails the moment another project appears outside.
- **Every published release now carries, beside it, the source code of the libraries that play your
  video.** LibVLC's licence and its plugins' require that code to be within your reach, and the
  clearest way to do that is the one they describe themselves: offer it from the same place you
  download the program from. So the release attaches `vlc-3.0.23.tar.xz` — checked against the digest
  VideoLAN publishes — and the LibVLCSharp archive, on top of the written offer that already existed,
  which stays for anyone who receives the program by another route. It also corrected a notice that
  named a VLC version which does not exist: the package's `3.0.23.1` is its own numbering, and the
  source is published as `3.0.23`.
- **And if you do not have the trailer, the card opens it in your browser.** When TMDB knows one, a
  button appears on the film's or the series' card and opens it in the browser you already use.
  **The application does not connect to YouTube**: it hands the address to Windows and your browser
  is what goes there, with your settings and your extensions — which is why the list of connections
  this application declares does not grow by a single host. What is stored is the video's key and
  never an address, and only a key with the exact shape YouTube uses ever composes a link: anything
  else offers no button. The value rides on the same metadata request that was already being made,
  so there is no extra call. It does not play inside the application, and that is not a technical
  limitation: doing so would break YouTube's terms.
- **If you keep the trailer next to the film, the card plays it.** The convention Plex, Jellyfin and
  Kodi already use works: a `<film>-trailer.<extension>` file beside it, or a `Trailers` folder inside
  the film's own. The button only appears when that file really exists, it opens the way a video you
  drag onto the application opens — without being added to your library — and it accepts only the
  containers this release plays. **Nothing is downloaded**: if your trailer is on YouTube, that is
  not a file of yours and it does not play in here.
- **A film's or a series' card shows its synopsis.** The overview was already downloaded, stored,
  merged while respecting whatever you had locked, and editable by hand — but it was readable
  nowhere: you could write it in the editor and never find it again. It appears on both cards now,
  wrapped and capped so it cannot push the versions or the seasons off the screen, and announced by
  name for anybody using a screen reader. A blank synopsis takes no room. No new connection is
  opened: the text was already on your disk.

- **Renaming files proposed no name at all, and now it is known why.** The rename preview opens,
  shows its confirmation box and its two buttons, and **never offers a single operation**: the
  application was asking to rename each file to the name it already had, so the safety check
  correctly discarded it as "no change". What is missing is the piece that decides what a file should
  be called from its entry, and that is a product decision rather than a loose wire: it is written
  down rather than invented. Nothing on your disk was touched, and none is.

- **The player's controls can be used with a mouse now.** Three separate things had them unusable,
  and all three looked the same: a button on screen that did nothing. The video status notice — the
  one saying whether it is running on hardware or at standard range — **covered the whole player
  surface**, opaque, over the video and over the control bar, so it swallowed every click; it is a
  badge in a corner now. The buttons never re-checked whether they could be used, so **you could
  pause with the mouse and then not resume**. And the volume slider moved without reaching playback:
  it changed a number on screen and nothing you could hear. All of it still worked from the keyboard,
  which is exactly why nobody had seen it.

- **Windows now describes the application in your language before you open it.** The package
  declared Spanish and English all along, but its description was **one sentence with a slash in the
  middle** — "Biblioteca y reproductor de vídeo local / Local video library and player" — which
  Windows showed the same way to everybody: declaring a language translates nothing by itself. The
  package now carries one text per language and Windows picks yours. The text is not written by hand
  anywhere: it comes from the first paragraph of the matching README, which is where the winget entry
  already took its own, so both installation routes say exactly the same thing. The name is
  deliberately not translated: "AP Reelume" is the product's name in both languages.

- **A gate counts how many of the application's buttons are really pressed with a mouse.** Until now
  the automatic walk drove the application from the keyboard and **two** of its 129 command controls
  ever received a click; the rest could be visible, enabled and incapable of doing anything with
  nothing to say so — which is exactly what happened to a pair of buttons that survived a whole
  audit. There is now an inventory of all 129, a list of what is not pressed yet **with the reason
  written beside it**, and a ratchet: that list may only shrink. The first batch covers the library
  and the card — filter, sort, apply, open an entry, go back, mark watched, favourite, watch later,
  rate and play — and a control only counts once it has been really pressed, once what changed has
  been checked, and once a click beside it has done nothing. What counts is recorded while running,
  not by reading the test code. Turning it on found **three** defects in the harness itself: it could
  not tell apart two identical Back buttons with only one of them on screen; the same button was
  pressed under one name and reported missing under another; and the checking click that was meant to
  do nothing **turned the favourite button back off** in the row above, with nothing to say so.

- **The coverage gate now watches code that is not new.** It only looked at files appearing for the
  first time, so an old one that got worse was watched by nobody — and that is not a hypothesis: on
  re-measuring the three files carrying debt, two were exactly where they were a day earlier and the
  third had **gone backwards** by fifteen points, because an earlier tidy-up removed code from it
  and took the tested parts with it. Nothing said a word. There is now an explicit list of watched
  files measured on every run, each with the bar its code meets today: if it drops, the verification
  fails; if it rises, it **also** fails until the new bar is recorded, so a debt that gets paid
  cannot quietly come back. Two of the three ended up fully covered along the way; the third stays
  watched, with its name and its number in view on every run.
- **The last coverage debt is paid.** The use case that reconciles what a scan finds — the one that
  keeps a video moved to another folder being the same entry — had its happy path tested and not its
  decisions: what it refuses to touch, what it counts as a failure without costing the rest of the
  scan, and what it stores. Those are tested one by one now, and the file goes from 86.73% of lines
  and 76.00% of branches to 100% of both, with the bar raised behind it. Measuring the list of gaps
  before writing anything cut it by a third — five of the entries noted by reading the code were
  already covered — and turned up one no reading would have found: no test ever read the counter of
  files attempted.
- **The window no longer waits for the database to be ready before it exists.** On startup the
  application brings the database up to date — and checks its integrity if that rewrote the file —
  and until now it did that work on the very thread that draws: nothing was on screen until it
  finished, and on a large library the check grows with the file. The window now appears
  immediately with a startup screen, the work happens elsewhere, and when it ends the library takes
  its place, or, if the database cannot be opened, the same recovery screen as always: what changes
  is when the decision is taken, not what is decided. There is no progress bar, on purpose —
  nothing at that moment knows how much is left, and a bar that moves without meaning anything is a
  picture of progress rather than progress. The measurement that settled it: writing "await" in the
  code is not enough to free the thread, so it was checked, and the thread was not free for a
  single millisecond.
- **The package verification now says how long the window took to appear, not just that it did.** It
  recorded a yes or a no, and that yes covered both an instant launch and one that arrived just
  before the ninety-second deadline ran out, so a degradation stayed invisible until it was a
  failure. It now reports the time — measured from before the process starts, because starting is
  part of the wait — for the three cycles that open the application, and a test demands the figure
  and bounds it against that deadline. Three numbers rather than one, because a single number cannot
  say whether it was the launch that got worse or the machine. The first measurement settled two
  things: all five cycles migrate a new database rather than only the first one, as had been
  written, and the first launch is not the slowest of the three either.
- **Cataloguing and playing share the native engine instead of starting one each.** Reading a file's
  technical data spun up its own LibVLC instance with the same options as the playback one, so a
  process that catalogued and played kept two native engines open — and the count that says "one per
  option set" could not see the second. There is one owner now, and a test that fails the moment
  another appears. The second media-release queue goes with it: the probe's did not guard its own
  disposal, so a single failing release would have left its worker dead for good and everything
  catalogued afterwards would have leaked with nothing saying a word. The one that remains already
  carries that guard.
- **The compiler watches that a stored number does not depend on the system's language.** A size, a
  date or a comparison written with the rules of the reader's language is read wrongly on another
  machine, and that error does not announce itself: it shows up once it is already stored. Three
  checks that shipped switched off are on as errors now. There was nothing to fix — measured first:
  zero cases across the project — and that zero was checked by compiling a deliberate violation of
  each rule, to know the checks were actually running.
- **The updater introduces itself with the version you actually have.** Asking whether a new release
  exists, it identified itself as "1.0", a number typed by hand that never existed: the declared
  version is 0.1.0. It comes from the program itself now, and a test compares it against the one
  place this project declares its version, reading the header that actually leaves rather than the
  constant in the code.
- **The tests find the project root in one place.** The same walk upwards was pasted into fifty-nine
  files, and it was not even the same walk: two of them looked for a document and the rest for the
  solution file, so the repository held two definitions of its own root. There is one now, shared,
  and a test that fails if anybody writes their own again. Eight hundred lines lighter.
- **The test that watches for unreachable screens no longer believes a comment.** It looked for the
  view's name in the files' text, so a **commented-out** reference counted as if the screen could be
  opened: the very orphan screen that test exists to find could hide behind a comment while the gate
  stayed green. Comments are stripped before matching now. Whether anything was already hiding that
  way was measured first — nothing was — and the trimming errs the safe way: cutting too much loses a
  reference and produces a loud warning, never a silent pass.
- **And the player no longer keeps one of its own either.** A third queue was left, the video
  engine's, with the same unguarded disposal: one failing release would have ended its worker and
  everything opened from then on would have leaked in silence. There is **one** queue for the whole
  process now, the one that already carries the guard. Closing the player waits for its videos to be
  let go before handing it back — that order is what keeps the native teardown from taking the
  process with it — and that wait has a ceiling, so a library busy cataloguing cannot hold up an
  exit. The one-second rest before a video is released, which is the number that stopped the
  crashes, is untouched.
- **A launch that never paints now leaves a diagnosis instead of a mute exit code.** The
  verification kills the process when the window deadline runs out, and the only thing written down
  was that kill — `exit code -1` — which says nothing about the launch. Now, **before** killing it,
  the verification records whether the process was still alive, how much processor time it had used
  across how many threads — which is what separates spinning from waiting — whether the database
  exists and how many migrations it has been through, and what the data folder holds. All of it
  lands in the same line CI prints when the phase fails. None of those reads can break anything: a
  diagnosis that fails would replace the failure it was called to explain, so whatever goes wrong is
  reported inside the sentence itself. The ninety-second deadline has not been raised, which would
  turn the only signal there is into silence.
- **On the way out, the application lets go of what it took.** The native player, the database, the
  tray icon, the media-key registrations and the network clients lived in a static field that nothing
  ever released: the process ended and left Windows to reclaim its own, which is trusting rather than
  closing. The application is now an object with an owner and it is released on exit, whether the exit
  comes from the window or from the tray. There is a second effect, more visible in the tests than on
  screen: two applications can exist at once in one process without seeing each other, and the clause
  forcing the two full-journey suites to run one after another came off — which is how the ownership
  is shown to be real rather than merely tidier. What is still not released, on purpose and written
  down, is the native LibVLC instance: creating and destroying it repeatedly is a known failure mode,
  so it lives as long as the process does.
- **The application's registration stops being a three-hundred-line list.** Everything the
  application assembles was declared in one chain, and finding out what a piece depended on meant
  reading all of it. There are now eight modules by area — data, playback, personalisation, library,
  settings and backups, updates, appearance, and identification — each short enough that a missing
  piece is visible. Behaviour is unchanged, and the tests that walk the genuinely assembled
  application are what say so.
- **The logic that picks which copy to offer when the library will not open can now be measured.** It
  lived inside the composition file, where the only way to reach it was to make a real database fail,
  and it decides what you are offered on the worst day your library has. It is now a piece of its own
  with five tests, two of them covering what nobody checked before: that a copy the record names but
  which is not on disk is not offered, and that another database's copy is not mistaken for yours.

### Legal

- **Credits shows TMDB's logo, not only their name.** Their terms ask that TMDB's use be identified
  with their mark, less prominent than the product's own; it was the last point of those terms left
  open. The file is the one TMDB publishes, and that it is can be checked: the SHA-256 they embed in
  the asset's address matches the versioned file's, and a test compares them. What is drawn is their
  vector rather than an imitation — another test contrasts the view's geometry against the file's,
  character for character — and it is drawn at 16 px against the 24 px of the product name. The
  specification said 48 px for that name; it was measured, it did not exist, and the figure was
  corrected instead of inherited. It carries alternative text in both languages and is not a link.
- **The text of every third-party licence travels inside the package.** Naming a component and its
  licence is not delivering the licence, and several of them require it: LGPL-2.1, GPL-2.0 and
  Apache-2.0 ask for a copy to accompany the binary, and MIT and BSD-3-Clause for their copyright
  notice to be reproduced. VideoLAN's package carries none, so nobody was supplying them. The
  `licenses/` folder of both artifacts now carries the five full texts and the notices of ANGLE, Skia,
  HarfBuzz, BouncyCastle, SQLitePCLRaw, SQLite and VideoLAN — including Microsoft's own file covering
  the twenty-odd libraries Skia and HarfBuzz carry inside them, from freetype to zlib, which appeared
  nowhere until now. Notices a package publishes are copied from it and a test compares them byte for
  byte against the package the build consumed, so a version bump that changes a notice turns red
  instead of quietly distributing the previous one. The canonical texts were taken from a source that
  already distributed them and contrasted with a second, independent copy; GPL-2.0 came from VLC's own
  tree, which is the licence binding its plugins.
- **Every source file states the licence it is under.** The licence lived only in `LICENSE`, and a
  licence that lives only there stops being attached to a file the moment somebody copies it out of
  the tree. All 556 code files, 51 interface files, and 17 build scripts now carry their
  `SPDX-License-Identifier: GPL-3.0-or-later` header next to the copyright holder, and the formatting
  gate that already ran rejects a new file that arrives without one.
- **The third-party notices name what the package actually carries.** They listed eight components —
  the ones somebody remembered asking for — while the artifact carried thirty, among them ANGLE under
  BSD-3-Clause, Skia, HarfBuzz, BouncyCastle, and the .NET runtime itself, all with notices that must
  travel with the binary. The list now comes from the build's real inventory, explains the
  self-contained runtime and VideoLAN's plugins separately, and a test stops a dependency from
  entering the artifact without appearing in both languages' notices.
- **The TMDB attribution says the sentence TMDB requires.** It displayed a summary of the mandated
  sentence; it now states the required one — "uses TMDB and the TMDB APIs but is not endorsed,
  certified, or otherwise approved" — in Credits, in the notice, and in both READMEs, with a test
  pinning it word for word in both languages.
- **Nothing from TMDB is kept longer than six months.** Their terms forbid it and the cache's expiry
  did not guarantee it: when the network failed or you removed the token, the program kept serving the
  stored copy however old it was. There is now a hard floor of 180 days; past it the entry is not
  served and is deleted from disk.
- **VideoLAN's plugins are confirmed compatible.** This was recorded as a question for an opinion: a
  `GPL-2.0-only` plugin would clash with this program's licence. It was checked at the source and they
  carry the "or any later version" clause, so they fit. The same pass turned up the opposite of good
  news: VideoLAN's package carries no licence file at all, and the artifact did not carry the text of
  the other licences either, which several of them require to accompany it. That is the gap the first
  entry in this section closes.
- **A legal status that also states what is missing.** A new page in both languages gathers the
  licence, the warranty disclaimer, third parties, TMDB's and GitHub's terms, and the export note for
  the cryptography the package carries, and names without decoration the five points that remain the
  owner's, the professional legal opinion among them.

### Security

- **Playing outside the application opens video and nothing else.** The button that hands a file to
  whatever player Windows has registered trusted its callers to have filtered by the container list
  already. What happened otherwise was measured: of five file types the library does not catalogue,
  **three opened their handler**, among them a `.ps1` and a file with no extension. The check now sits
  where the call is made, against the same list that decides what enters your library.
- **Restoring a backup applies its own size limits.** The ceilings — 512 MB per file, 2 GB in total —
  were set by the inspection step, which is a step somebody has to remember; unpacking now applies
  them too. Along the way it was checked, by forging an archive that declares one byte where it holds
  a whole database, that no entry can hand back more than it declares: the risk was in the
  declaration, not in the copy, and that is where it is now cut off.
- **Continuous integration can no longer reach a personal machine.** While the repository was
  private, verification could be routed to a self-hosted runner through a repository variable; now
  that it is public, that hatch was a standing invitation for a fork pull request's CI to run on the
  owner's machine the moment the variable reappeared. The workflow no longer consults it and always
  runs on hosted runners, which are free on public repositories.
- **The pipeline's three actions move up to their current major.** `checkout` 7.0.1, `setup-dotnet`
  6.0.0, and `upload-artifact` 7.0.1, with every SHA checked against the tag it claims to be.
  `checkout`'s breaking change hardens exactly the triggers this project does not use, and
  `upload-artifact`'s only alters naming when archiving is turned off, which here it is not.
- **The one external tool that seals a release is pinned by version.** Every action was already
  SHA-pinned and NuGet was locked, but ffmpeg was installed from the community feed at whatever the
  latest version happened to be: it was the only unpinned third-party executable on the machine that
  packages and signs. It now installs a specific version that moves only by a deliberate edit, just
  like the action SHAs.
- **The published digests are signed, and the updater demands the signature.** The hash an update
  is checked against travelled in the same unsigned answer as the package it vouches for: whoever
  altered the answer could alter both at once. Every release now signs its digests with a minisign
  key whose public half travels inside the binary; the updater verifies that signature before
  believing any hash, reads the hash only from the signed block, and a version without the
  signature — or with altered lines — is refused with its reason. The private key lives outside the
  repository, the release pipeline signs and refuses to publish unsigned, and the privacy statement
  explains what this layer proves and what it still does not (Windows code signing is a separate
  layer, still pending its economic decision).
- **The updater only accepts bytes from the declared domains, on every hop.** Redirects already
  required HTTPS but accepted any destination; every hop now has to stay inside the list the
  privacy statement publishes (GitHub and its storage), a hop outside is refused with its reason on
  screen, and the statement finally names the storage domain GitHub redirects to — with a test that
  keeps code and promise from diverging again. Title artwork is equally confined to its declared
  domain.
- **Every network answer has a ceiling.** Release metadata is cut off at one megabyte, the package
  is cut off the moment more bytes arrive than the release declared (and the poisoned partial is
  deleted), and a poster over ten megabytes is refused mid-stream while the previous artwork
  survives.

### Fixed

- **The player's controls no longer run off the side of the window.** In a narrow window — 900 pixels
  wide, which is the smallest the application lets you make it — the transport row ended 74 pixels
  past the right edge: the volume control, the mute button and the speed readout were off screen with
  no way to press them. The row now wraps onto more than one line when it does not fit on one, as the
  others already did.

- **The scene that tested cancelling a copy blamed the slower machine.** It compared the time its two
  presses took against a duration measured on one machine, so a slower runner turned it red with
  nothing wrong. What the clock was trying to infer — whether the copy had finished on its own — is
  something the surface says out loud, and the scene now watches for it instead of inferring it.

- **Switching version twice in a row no longer skips the question or leaves your place at zero.** The
  other version's row stayed pressable while its own switch was in flight, so a double click — or a
  second click while the question was already on screen — started a second switch. And every switch
  saves the player's position first: if the session had only just opened and the engine still answered
  zero, that zero fell below the point resuming is offered from, so the second switch decided there
  was nothing to carry across, opened the other version **without asking**, and left the stored
  position at zero. The row now greys out while its switch is in flight — the way the transport bar's
  skip greys out while it seeks — **and while its question is still on screen**, which is the longer
  of the two gaps: a switch that asks finishes at once and waits for your answer, so the row came
  back to life directly underneath the question. Answering it hands the row back.

- **A trailer kept beside a film only appeared if that film was duplicated.** The card looked for the
  trailer file starting from the version group, and a title with no copies has no group: the file sat
  right there and the button never appeared. It is now found from the film itself, copies or not.

- **Opening a video from Explorer played it without showing it.** The file started playing and the
  application stayed on the home screen: no picture, no controls, no way to stop it, and the notice
  saying "this is not in your library" — with its offer to add the folder — never appeared at all. An
  activation now opens the player like any other playback, and a file that cannot be decoded offers
  you a retry or an external application instead of leaving you an empty screen. The same was true of
  a local trailer.

- **Closing the application with a video open broke the shutdown.** Ending a session's hooks is not
  stopping it: the media stayed open, so the teardown tried to stop a player that had already been
  disposed and threw. The session is now stopped before the services that were feeding it. Nothing
  saw this because every check closed the player first, which is exactly what somebody closing the
  window mid-film does not do.

- **Switching version lost the point you had just agreed to.** The application asked what to do with
  your progress, worked out the equivalent second in the other version and stored it — and then opened
  that version **from the beginning** and wrote that zero over what it had just stored: measured, the
  playhead at 0, 0, 0, 1, 1, 2 against a carried-across position of 2:01. Whoever opens the player
  knowing where to open it now decides, and Start over really does start at the beginning.

- **The version switch's question was drawn over the whole screen.** Like the resume offer and the
  next-episode offer before it, it stretched to the player's entire stage — 1280x1400 measured — with
  its three answers in the corner. It now has its own size, background and border, and its answers
  wrap when they do not fit.

- **The button that switches version was drawn outside the window.** In each alternative version's
  row, the quality label pushed the button as far right as its text was long: measured 74 pixels
  outside a 1600 px window, with nothing to scroll. It was a version nobody could switch to with a
  mouse. The row is now split between a label that wraps and a button that keeps its place.

- **The resume offer and the next-episode offer were drawn over the whole screen.** Both stretched to
  the player's entire stage — 1280x1400 measured — with their buttons in the corner instead of being
  drawn as the card they are. They now have their own size, background and border, and their buttons
  wrap when they do not fit.

- **The marker editor's Delete was drawn off the screen.** In the player's column Save fitted and the
  button beside it sat **eleven pixels outside the window**, with nothing to scroll: there was no way
  to delete a marker with the mouse. The three buttons for proposed detections had the same problem.
  All four groups now wrap onto more lines when they do not fit.

- **The subtitle style you choose is no longer lost on closing.** The size, the typeface, the
  background opacity and the outline thickness were saved **nowhere**: you changed all four controls,
  closed the window, and started from scratch. Every change is now stored as you make it and comes
  back when you open the application. **What is still missing, said out loud:** that style reaches the
  database but **not yet the picture** — the video engine takes its subtitle rendering at startup, and
  connecting it is separate work that only a screen can confirm.

- **"Remember for this series" can be ticked now.** The box that makes your audio or subtitle choice
  apply to the **whole series** rather than to that one episode was **always disabled**: the
  application never told it which series you were watching. It was worse than a dead button, because
  on opening an episode it **did** look for a preference stored for the series — one nothing could
  store. It ticks now, and what you pick afterwards is stored for the whole show, so the next episode
  starts the way you left the last one.

- **The automated verification now presses the whole of settings with the mouse.** The settings page
  is taller than the window, and the walk that drives the built application only knew how to go down
  it: once something had been pressed, everything above it became unreachable. It now returns to the
  top of the page and scrolls only when it has to, so each press stands on its own. All twenty
  settings controls are covered by mouse: the three themes, both languages, local-folder watching,
  segment detection, the tray and closing to it, start-with-Windows — asked for, declined, asked
  again and granted — the diagnostics consent, the automatic refresh, the preview, the report export,
  the recommendations switch, its threshold and its recalculation, and restoring the shortcuts. Each
  one is checked by its real effect rather than by its checkbox: the export is read from the file on
  disk and the startup entry from the registry.
- **A copy of the application that keeps its data somewhere of its own now keeps its
  start-with-Windows entry there too.** Every copy used to write its sign-in entry to the same place,
  so an automated check could not even test that button without registering the copy it was testing
  on your machine. Your normal installation is unchanged: it still writes where Windows reads.
- **The review inbox's Search button can be pressed now, and it actually searches.** It had two
  faults stacked on each other: typing into the box **did not enable it** — it stayed off however much
  you typed — and had it been on, pressing it would have done nothing, because what it asked for was
  listened to by nobody. Now you type the title and year with a card selected from the list, and the
  application searches **for that file**: if what it finds leaves no doubt it is applied without
  asking, and if it does, it waits in the inbox for you to decide. The button is available when both
  halves are there — something typed and a card chosen — because without a card there is no file to
  search about.
- **Accept and Reject are now checked with the mouse to decide the card you chose.** That is not
  boilerplate: the automated check turned out to be able to decide a different card than the one it
  clicked, so the check now asks **which** card ends up accepted or rejected, reading it from the
  catalogue rather than from the screen. "Load more" is pressed too, and brings in the rest of the list.
- **The button that confirms a moved file sat off the side of the screen, so there was no way to press
  it.** When the application finds a file that may be one you already had, it shows you each
  candidate's path with a "Same file, reassign" button beside it. The path laid itself out across the
  row without ever wrapping, and with a real library path — yours are — it pushed the button out of
  the window, with nothing to scroll sideways to reach it: **the reassignment could not be confirmed
  at all**. The path now wraps into the space there is, and the button stays in view.
- **And when there are several candidates, which one each button confirms is now clear.** The
  application only asks you when **two** entries in your catalogue could be that file, so the button
  appears more than once; both were called the same, and choosing wrong is no detail: it decides which
  of your entries keeps its progress and your decisions under the new path. Each button now says which
  path it belongs to, for screen reader users as well. Both decisions — "same file" and "it is a new
  file" — are covered by mouse, reading from the catalogue which entry ended up decided.
- **The Remove button for a library folder fell off the screen.** The folder path laid itself out
  across the row without wrapping, so with real paths — yours — the button ended up outside the window
  and there was **no way to remove a folder**. The path wraps now and the button stays in view. That
  surface — the first one you see after installing — is also covered by mouse end to end: the three
  kinds of folder, adding one, the permission for the first scan, and removing one **both cancelled
  and confirmed**, checking that the folder is still on your disk.
- **The Continue button on the home surface did nothing.** It is the primary action of the whole
  application: home offered to carry on with whatever you last left part way through, the button
  enabled itself because there was something to return to, and pressing it **did nothing at all**. It
  now opens the session on the same copy your position came from, at the point you left it.
- **And two of the three player buttons sat off the screen.** "Mini player" and "Fullscreen" were
  drawn past the edge of the window, and no amount of resizing helped: the column they live in is a
  fixed width, so they **never fitted at any size**. All three now lay out across as many lines as
  they need and stay in view.
- **The review surface is watched whole now, including when things go wrong.** It is where you correct
  what the automatic reading got wrong, so it is now checked line by line and path by path: what
  happens if somebody decided before you, if the card is no longer there, if you have chosen nothing.
  With it went a check that **could not fail but could stop being worth anything** — the same kind that
  had already left the Search button off for good — so that fault has no way back.
- **Choosing which copy plays is now checked with the mouse to store your choice.** When you have two
  copies of one title, the comparison lets you mark the one you want; the verification presses the one
  that is **not** the copy that would play anyway, and reads the result from your catalogue rather than
  from the screen, so "the better copy" and "the copy you chose" cannot be confused.
- **Where "Watch the trailer" leads is now checked, on the film card and on the series card.** The
  button opens whatever browser you use, which is why no automated check could press it: it would
  have opened windows on the machine doing the checking. A copy that keeps its data somewhere of its
  own now **writes the address down** instead of opening it, so the verification presses the button
  on both cards and reads the exact address that would have opened — that entry's trailer and no
  other. Your normal installation still opens the browser as before, and what may be opened is
  unchanged: `https` only, and only to the site the address names.
- **The rename now renames.** It proposed the name the file already had, so the preview was always
  empty and the Rename and Undo buttons could do nothing however often they were pressed. It now
  proposes the name the entry deserves, in the convention Plex, Jellyfin and Kodi all read:
  `Title (Year).ext` for a film and `Show (Year) - SxxEyy - Title.ext` for an episode. Across the
  twelve name shapes the catalogue recognises, eight get a name different from the current one and
  the one already following the convention is left alone. When nobody has identified the entry and
  the name cannot be read with confidence, nothing is proposed: a rename does not guess. What decides
  which characters are safe, and what to do about two files that want the same name, is unchanged.
- **A watched folder no longer stops being watched exactly when a lot of files arrive.** When Windows
  reported that it had dropped changes — which is what happens when a whole season is copied in at
  once — live watching of that folder ended in silence and did not come back until the application
  was started again. No file was lost, because a full pass over the folder was run, but from then on
  anything added took until the next pass to appear. A report like that now means "sweep the whole
  folder and keep watching", the space Windows uses to report changes is asked for at the maximum it
  allows, and watching that really does fall over — an unplugged disk, a network folder that stops
  answering — starts itself again on the next pass.
- **The automated verification now presses buttons with the mouse.** The walk that drives the built
  application only ever used the keyboard, and that gap is how a pair of buttons that were on screen
  and did nothing got through. It now opens an identified film's entry, clicks "Refresh from
  provider" with the mouse and checks the entry changes — and, first, that clicking beside it changes
  nothing, so the result cannot be down to something else.
- **The editor's two provider buttons now do something.** "Refresh from provider" and "Restore
  provider fields" were visible and enabled and could not work: they waited on details only a test
  ever gave them. The refresh now works out for itself which provider entry the title is, from what
  was stored when it was identified. And when it can do nothing, it says so: an unidentified entry
  and a provider with no answer are different things, and neither is an error.
- **Saving a freshly added entry now saves.** Editing a title nobody had touched did not create its
  row, so Save was pressed and nothing happened, with no warning.
- **Two windows editing the same entry can no longer both win.** The check that stops one person
  overwriting another's work was comparing against a number that never changed, so the second window
  silently overwrote the first. The second one is now told its copy is stale, which is what should
  always have happened.
- **Identifying a film now changes what the library shows.** Until now it did not: accepting a match
  in the inbox marked the review and nothing else, so the entry went on showing whatever the parser
  pulled out of the file name. The synopsis and the trailer key existed end to end and were only ever
  filled in by hand. An identification — accepted by you, or automatic once confidence passes 90% —
  now asks the provider for the details and stores them along with whose they are and when they were
  asked for. **Anything you locked still wins**: the identification merges over what was already
  there, exactly as a manual refresh does. With no consented connection the provider serves only what
  it already holds, so a library without network permission stays exactly as it was, which is not an
  error.
- **A verification that wedged no longer spends an hour saying nothing.** Six of the ten continuous
  integration runs of 10 August died at the sixty-minute ceiling, and the log said nothing between
  "build succeeded" and the cancellation fifty-six minutes later. The step after the build produces
  the container matrix with FFmpeg and started it with no bound: one wedged encode burned the whole
  job and reported as an infrastructure hiccup. Every call to the encoder now has a ceiling, every
  sample is announced before it is produced, and a process that does not come back is killed with the
  recipe it was on named. Producing all sixteen samples costs 1.6 seconds, so that ceiling is not a
  performance budget: it is the difference between a named failure and a job that dies without saying
  why.
- **And with the name in front of us, the recipe that wedged.** The first run with the ceiling in
  place failed in four minutes instead of dying at sixty, and pointed at
  `mkv-dual-audio-english-first`. The eleven recipes that ended in `-shortest` now bound their output
  duration explicitly: `-shortest` has a documented deadlock where interleaving and flushing cross,
  which is exactly what made some runs take twenty-two minutes and others hang forever. Every input
  already lasted three seconds, so the file is the same one: the 114 container-matrix tests confirm it
  property by property.
- **Stored playback preferences are actually applied.** The audio track and subtitles you chose —
  per file, per series, or globally, falling back by language when a track is absent — were
  resolved and never applied: every session opened with whatever the engine picked. They now apply
  the moment the video opens, and the track selector shows what was actually applied. Along the
  way, six dead container registrations (duplicates of what the application builds another way)
  are removed instead of left silent.
- **Choosing the audio output device now changes where the sound goes.** The selector existed and
  the engine never heard about it: your choice was stored while the audio kept coming out wherever
  VLC pleased. Picking a device now pauses, reroutes, and resumes — never restarting the video —,
  the stored global choice applies when each session opens, and a device that vanishes mid-play
  never cuts playback: it falls back to the default without forgetting your preference.
- **Switch versions without leaving playback.** If the title you are watching has more than one
  version (another resolution, another codec, HDR), the player lists them and you can jump to one
  mid-session. Your position is written first; when the lengths make a safe transfer impossible,
  the application asks with the proposed second in sight — continue there, start again, or cancel.
  An unavailable version is never offered as openable.
- **Moving a video to another folder no longer costs it its history.** Every scan captures a
  lightweight identity of what it catalogs (the disk's stable id and a bounded content
  fingerprint) and reconciles: a file that appeared under a new path and vanished from the old one
  becomes the same entry again, progress and your decisions intact. A copy that coexists with the
  original keeps being treated as a copy (versions, as always). And when there is doubt — two
  known copies and an identical newcomer — the review inbox asks you: "same file, reassign" keeps
  the history under the new path; "it is a new file" leaves it as its own entry. The offer returns
  with every scan until you decide; nothing is decided silently.
- **A folder can be removed from the library.** The library finally lists its folders, and each
  one has a removal action with a confirmation that tells the truth: the folder leaves the
  catalog, no video on disk is touched, and adding it again catalogs it anew. The catalog on
  screen reloads right away.
- **Marking something watched is now actually stored.** The card's watched toggle was built with
  no handler: every mark went nowhere and the card forgot it on reload. A decision of yours is now
  stored as manual — nothing the player computes afterwards changes it — and undoing it hands the
  state back to the automatic rules. The watched threshold (how much you need to reach) is finally
  configurable under recommendation settings, between 50 and 100 %; moving it recomputes only the
  automatic states and says how many moved.
- **A video that fails to open no longer drags the preferences down with it.** Applying preferences
  assumed a live session: with a file the engine could not open, the track selection blew up
  internally after the screen had already shown the diagnosis. A session that never opened, or
  closed underneath, now simply has nothing to apply; any other failure still speaks up.
- **Renaming with the file open in another program now says what to do.** The failure was stored in
  the audit as "IOException" and the screen said nothing: the rename simply did not happen. The
  surface now says whether another program has the file open, whether Windows denied permission, or
  whether the drive failed — each with its action — and the audit keeps the reason under a useful
  name.
- **Diagnostics say what your machine did, not what a constant promised.** The reported video
  acceleration is what the engine actually used for the last video, the library size is the real
  one (bucketed, as ever), and the errors are the ones the application recorded — no paths, no file
  names, as the allowlist demands.
- **The manual explains the orphaned startup entry.** Uninstalling with "start with Windows" on
  leaves a harmless registry entry; the manual says why it does nothing, how to remove it by hand,
  and that reinstalling repairs it on its own.
- **The screen follows the engine for the whole session.** The on-screen state only changed at
  open: pausing paused the engine while the interface kept saying “playing” forever, with the
  resume controls unreachable. The method that applied the transitions existed and was tested; in
  the assembled application nobody called it. It was found by the physical walk of the packaged
  artifact — three re-runnable scenes with a real disk, real SQLite, and real decoding: the watcher
  cataloguing a dropped file and grouping two copies, the keys operating a playing video, and two
  episodes chaining on their own — which stays on as a permanent guard.
- **Long paths and per-monitor scaling, declared instead of inherited.** The application shipped no
  manifest of its own: the 260-character limit still applied even where Windows had lifted it — a
  library under a deep folder silently lost files — and DPI awareness was whatever the runtime
  guessed. Both are now written into the process manifest.
- **A rewritten migration no longer goes unnoticed, and integrity is asked once.** Startup only
  compared version numbers: if an applied migration's text changed, the schema on disk and the one
  the code assumes diverged silently. Every stored checksum is now compared against the build's,
  and a mismatch is refused by name. Along the way, the integrity check — the slowest part of
  opening a large library — runs once per start, not twice.
- **A detection can no longer outrun its episode.** Manual markers always validated against the
  duration; detected ones were judged blind. The detector now clamps what it emits to the episode
  it measured, and the policy applies the manual-marker rule to detections.
- **The manual says what happens to your data when you uninstall.** Nothing is deleted: catalog,
  progress, and backups stay in their folder and a reinstall finds them; the manual also explains
  how to really erase everything.
- **The player window coordinator had two owners.** It was registered in the service container and
  simultaneously built by hand by the main view: two instances, one holding geometry nobody would
  ever read. The view — the one that owns the mini player window — stays as the single owner and
  the dead registration is removed.
- **A marker made during playback did not work until the episode was reopened.** The session's
  markers were a photo taken at open: saving, deleting, accepting, or correcting a marker changed
  the stores while the skip button kept reading the old photo. Every change now recomposes the
  session's markers on the spot, so the button appears (or disappears) without closing anything.
- **Two copies of the same film never grouped on their own.** Version grouping existed with its
  repository, its conservative policy, and its tests, and nothing invoked it: groups were only ever
  created by tests. Every scan now groups the copies whose names declare them the same, a material
  duration difference waits for confirmation instead of grouping silently, the preference you pin
  survives rescans, the group is found from any of the copies, and no file is ever deleted or
  hidden.
- **Neither keyboard shortcuts nor media keys did anything.** Every piece existed — the shortcut
  map with its defaults, the conflict-refusing editor, the router that prevents duplicate actions,
  the media key service — and none of them touched another: the player never read the keyboard and
  the service never started. The player now answers the shared map (Space pauses, arrows skip, M
  mutes, F goes fullscreen…), the hardware media keys operate the session while one exists and are
  released when it closes, a key arriving through two paths acts exactly once, and the Settings
  editor edits the very map the keys read.
- **Finishing an episode never offered the next one.** The engine did not even learn the video had
  ended — the state stayed at "playing" forever —, the countdown that was tested end to end was
  never registered, and the card's buttons did nothing. The end of the media is now a real state,
  finishing an episode offers the next with its cancelable countdown, "Play now" opens without the
  wait, and when there is no next episode or its file is gone, the application returns to the
  details.
- **Folder watching never started.** The watch coordinator, the debounced watcher, and the fallback
  scheduler existed, were tested, and nothing started them: the application only scanned when a
  button was pressed. Watching now starts with the window and stops on the way out, a freshly added
  folder is followed from its first scan without a relaunch, a root configured as manual is not
  watched behind its owner's back, and the fallback scan for USB and NAS roots actually recovers
  lost events every fifteen minutes. And every scan — watcher-triggered or manual — hands what it
  found to identification, not only the manual one.
- **Identification never ran, so the review inbox was always empty.** The use case existed complete
  — it parses the name, scores candidates, asks the provider only when needed, and stores the
  result — and nothing ever invoked it. Every scan now hands what it found to identification: the
  confident match resolves on its own, the ambiguity shows up in the inbox, a file somebody already
  decided is left alone on every later scan, and files no earlier scan ever identified heal on the
  next one. Without the provider token everything stays local and no connection is opened.
- **"Continue where you left off" left the video at zero.** The resume decision was computed after
  the media had opened, nobody passed the start position to the engine (which has always accepted
  one), and the prompt's buttons were wired to nothing. The decision now exists before the open, the
  media opens at the stored position, and "Restart" genuinely seeks to zero. Three assembly
  assertions, one unit test, and one real-decoding test cover the whole chain.
- **Background detection could not be stopped and survived exit.** The use case has accepted
  cancellation from the start and the scheduler called it bare; closing the application could leave
  a process decoding in the background. Every detection now runs under a shutdown token and leaving
  the application stops it, together with the session's save loop.
- **An unreadable answer from the update source could take the application down at startup.** A
  captive portal (hotel wifi) answers `200` with a login page; that threw an untranslated exception
  which, on the startup automatic check, escaped onto the interface thread. A body that is not a
  release now reads as "source unreachable", the update surface always lands on a state, and the
  three startup jobs (check, tray exit, loose file) observe their exceptions instead of handing
  them to the interface thread.
- **The periodic position save never started.** The five-second loop was only ever invoked by
  tests: in the application only the orderly close and the version switch wrote, so a power cut
  lost the whole session. The session now starts the loop on open and cancels it on close, pausing
  writes the position, and every seek — from the transport, the skips, or the skip button — writes
  the chosen target. Along the way, position handlers detach from the engine when each session
  ends, instead of stacking one per episode.
- **Segment detection released LibVLC in the order that crashes the process.** The fingerprint
  extractor stopped a still-playing player, released it before its media, and disposed the media
  with no quiescence window — the three rules the code itself has written down as the native
  failure mode, on the same instance playback uses. It now keeps the engine's own order, with a
  deferred-release queue on the factory whose drain survives a failing dispose. A twenty-cycle
  drill over ten episodes stays behind as a permanent check.
- **Packaging failed on machines with exactly one Windows SDK.** The `makeappx.exe` lookup returned
  a bare string instead of a list when a single version was installed, and indexing it produced its
  first character: sealing then tried to run a program called `C`. The lookup was copied into three
  scripts (package x64, package ARM64, and verify the package) and all three copies carried the same
  defect. This is what broke continuous verification on the updated runner; it was never seen
  locally because two SDKs are installed. The same image update removed ffmpeg from the runner, so
  the workflow now installs it explicitly: the codec matrix, the segment corpus, and the
  file-association phase are measured again on every push.
- **The suite's first full pass in CI uncovered seven machine assumptions.** Two backup progress
  tests were flaky by construction (`Progress<T>` queues its callbacks and a loaded machine reaches
  the assertion before the last stages; they now report synchronously). The other five depended on
  the machine: with no audio endpoint the catalog declares a block instead of failing, an HDR sample
  generated without colour metadata declares its precondition broken, and the ±5 s promise and the
  frame budget declare themselves out of a shared runner's reach — their gates keep being measured
  on the local physical harness, as always.
- **The privacy statement did not mention the updater.** The connection table still described the
  application from before T44: it listed the two metadata destinations and denied that any update
  check existed, when the updater — optional and off out of the box — talks to `api.github.com` and
  `github.com`. The table now lists all four destinations, the other documents scope "no connection"
  to metadata, and a new test fails if the network purpose registry and the table diverge again in
  either language.
- **Ten matrix statuses said more than was demonstrated.** The 2026-08-08 audit found a family of one
  defect: components built, registered, and tested that no path in the assembled application invokes
  — identification, folder watching, duplicate grouping, the periodic save loop, resume, the
  next-episode countdown, and the shortcut and media keys — plus an MSIX cycle verified against a
  re-signed copy that the unsigned artifact cannot repeat. Those rows (PRD-002, LIB-002/003/006/007/008,
  PLY-008/011/014, and REL-003) return to `IMPLEMENTED`, each with its blocker, owner, and unblock
  condition in the verification manifest. The per-component evidence still stands; what was missing
  was the assembly, and the record now says so.
- **The automatic-check box did nothing.** It was in Settings and was written to disk, but no path in
  the application ever asked for an automatic check: it only ever checked when the button was
  pressed. T44's physical verification found it, not the tests.
- **The updater asked a repository that does not exist.** It asked for `ap-solutions/ap-reelume`
  rather than `apvisualsolutions/ap-reelume`; GitHub would have answered 404 and the application
  would have said "you already have the most recent version" indefinitely. A test now compares that
  address against the one the changelogs publish.
- **A summary that began with a subheading arrived empty,** because the section reader broke on `###`
  as well as `##`. A release like that would have been offered to nobody.
- **The notice that Windows did not open the package said "you can try again".** On a machine with no
  App Installer, trying again never works. It now says the file is downloaded and verified, and how
  to install it by hand.
- **The playback resource gate failed about one run in three with no regression at all.** It compared
  the working set of the whole process — test host and coverage collector included — against a 32 MiB
  bound, and seven runs with no code change ranged from −7.9 to +37.6 MiB. Fitting a slope does not
  help either: measured, it ranges from −170 to +1107 KiB per cycle. The bound is now 128 MiB, which
  is what a gross regression looks like; what catches a leak are the exact counters already asserted
  on all fifty cycles.
- **The test media matrix could not be generated from nothing.** Several samples mux a subtitle track
  from a companion file, and their recipe named it with a placeholder the generator never wrote or
  substituted. Two separate things hid it: on a machine that already had the output tree the samples
  were reused rather than produced, and without ffmpeg the script returns before it could try.
  Moving the project to another folder is what found it.
- **An earlier redaction of the personal library was incomplete.** A show's Spanish title had been
  replaced and the same title in English was left, because the pattern being searched for was written
  in Spanish. The check no longer depends on anybody remembering: `RepositoryPrivacyTests` walks the
  versioned files on every run and derives what it looks for from the machine, writing no personal
  data into the code.
- **A recovery test failed about one run in two,** because it waited for the signal file to *exist*
  and then read it: existing and being finished are different moments, and reading between them
  collides with the process still holding it open. The wait is now the read.
- **The skip-marker button never received any data.** It had been built since the MVP and nothing
  in the assembly ever handed it the markers or the position, so skipping intros existed only in
  the tests. It now follows the playhead with the composed markers — manual and detected — the
  editor loads the series' real markers, and an episode resolves its real series instead of
  treating every file as a series of its own.
- **The default branch can no longer be left behind at publishing time.** The personal-library
  redaction lived only on the working branch, and `main`'s tree kept showing what had been redacted
  until the audit found it; `main` was fast-forwarded to the branch and `prepare-release.ps1` now
  blocks any publication with `main` left behind.
- **The ARM64 build had never worked.** The Windows project pinned `PlatformTarget` to `x64`
  unconditionally, so `-r win-arm64` failed with `NETSDK1032`. The early check that existed to catch
  this was a `dotnet restore`, which resolves packages without compiling anything: it stayed green
  while the build was impossible. CI now builds the ARM64 package, which contains that check and also
  answers it.

### Note

- **Four assembly guards stopped reading the code as text.** They asserted their promise by
  searching for characters in the composition file — which a comment or a dead registration can
  satisfy, and had already done three times. They now assert the registered descriptors and, for
  the updater, the address on the object the application actually builds, compared against the
  one both changelogs publish. The two halves no descriptor can express stay declared as text
  until the startup path is reformed.
- **New code can no longer arrive without its tests.** Every verification now requires each
  source file that is new against `main` to arrive with at least 96% of its lines and branches
  covered by the suites, with the per-file verdict written next to the results. The gate was
  calibrated against the previous session and found three real files below the bar — the happy
  paths were walked end to end; their error branches were not — which are named as visible debt
  in the evidence rather than lowering the bar to hide them.
- **The low-resolution quality enhancement is deferred, with its measurement.** A measurable
  spike over real media investigated whether VLC 3's video filters (sharpening, denoising,
  deblocking, scaling) can improve sub-720p shows: none of them processes a single frame on this
  application's video path — VLC itself builds the chain and removes it whole when it cannot
  match the formats, under every decoder and output format. The feature is not promised: its row
  is deferred with the measured evidence, and the real alternatives (VLC 4, an own enhancement
  over the already-decoded frames, or another engine) are documented with their cost, so the
  decision is made with knowledge rather than intuition.
- **The performance budgets no longer block on shared runners.** Two budgets failed in CI measuring
  the neighbour's noise, never locally. CI still runs them and archives their verdict with the test
  results, but cannot fail because of them; they keep blocking on the local physical harness, which
  is where they mean something. The WAL durability test also gains a bounded retry (3 attempts /
  1 s) only around the reopen after the kill: the runner disk's transient "disk I/O error" is not
  the phenomenon under test.
- **Neither the updater nor winget works while the repository is private.** Both read the GitHub
  releases address, which for a private repository answers nobody; since the absence of a release is
  a settled answer, the application would tell everybody they are up to date. Making the repository
  public and cutting a release are requirements for either to work, not decoration.
  `eng/build-winget-manifest.ps1 -Verify` checks it by asking the address.
- `PRD-003` is **blocked**, not verified: there is no Windows 11 ARM64 machine on which to certify
  playback, and emulating one would measure the emulation. The six physical phases are declared in
  `arm64-matrix.json` with their reason. Detail in [T42-arm64.md](evidence/stable/T42-arm64.md).
- Intel Quick Sync decoding and the OpenGL video outputs do not exist on ARM64: VideoLAN does not
  build them for that architecture. All fifteen native differences between the two packages are
  listed in the package report.

## [0.1.0] — 2026-08-04

The first installable artifact. It catalogues, identifies, plays, and remembers where you left off,
in Spanish and English, with no account and without sending anything.

### Added

- **Local library.** Local, USB, and UNC/NAS folders in their original location, copying and moving
  no video. Initial, startup, manual, and incremental scanning, cancellable and resumable, with
  continuous watching and a fallback scan for drives watching does not cover.
- **Hybrid identification.** Movie, show, season, and episode detection from names and folders, with
  TMDB metadata in Spanish and a fallback language. Confidence thresholds: automatic from 90%,
  suggested between 60% and 89%, pending below. Anything ambiguous goes to a review inbox.
- **Duplicates as versions.** No file is deleted or hidden; a version is chosen by quality and
  availability.
- **Protected metadata and artwork editing,** and optional rename with preview, audit log, and undo.
- **Embedded LibVLC player,** with external opening as a fallback. The usual containers and codecs,
  HDR10 with SDR tone mapping, internal and external tracks and subtitles, speed, skips, and boosted
  volume with a limiter, fullscreen, and a mini player.
- **Continuity.** Exact progress saved every five seconds and on pause, seek, and close; resume
  within ±5 s; watch statuses with a configurable threshold; progress transferred between compatible
  versions; a cancellable countdown to the next episode; manual intro and credits markers.
- **Personal experience.** A hybrid home with resume and library, favourites, watch later, a rating,
  and local recommendations that explain themselves and can be turned off.
- **Accessibility.** Full keyboard, visible focus, screen readers, scaling, high contrast, reduced
  motion, and customisable subtitles.
- **Data and privacy.** Local SQLite with WAL and versioned migrations, rotating backups with a
  manifest, and video-free ZIP export/import. Zero telemetry without consent; opt-in, sanitised
  diagnostics.
- **Windows integration.** Configurable tray and startup, off by default, media keys, and
  "Open with…" that plays without importing into the catalogue.
- **Distribution.** An x64 MSIX and an independent ZIP, with published SHA-256 sums, an SBOM in
  CycloneDX and SPDX, licence and third-party notices inside the artifact, and a reproducible build.
- **The application can be told where it lives.** `AP_LOCALMEDIA_DATA_ROOT` names the data folder; it
  is read once at startup and a blank value is the same as not setting it.

### Fixed

While assembling and packaging, walking the real application found defects no headless test could
see:

- Consenting to the first scan scanned nothing, so a new install stayed empty forever.
- Adding a folder twice closed the process instead of refusing it with a sentence.
- A scanned, unidentified file opened the series card, which offers nothing to play.
- Choosing an audio track neither applied nor stored it.
- The session never fed the progress tracker, so the resume offer never came back.
- Withdrawing diagnostics consent left the exported report on disk.
- The video status indicator was never fed: it stayed blank while the engine decoded on the GPU.
- An older release opened and wrote over a database a later one had already migrated.
- **Installed as an MSIX, the data was not going where this documentation promises**: Windows
  redirected the writes into the package container, and **uninstalling deleted the whole library**,
  backups included. The package now turns that redirection off, so the MSIX and the ZIP share one
  data folder and uninstalling removes the application alone.

### Security

- The artifact **carries no access token**. Remote identification requires placing one by hand in
  `AP_LOCALMEDIA_TMDB_TOKEN`, and without it no connection is opened.
- The package declares one capability, `runFullTrust`, and none for network, location, or system
  libraries.
- The payload is examined before publication for keys, tokens, and local paths.

### Known limitations

- **No code signing.** Windows will show a SmartScreen warning, and the documentation does not claim
  otherwise. Check the published hash; the build is reproducible.
- **An unsigned MSIX will not install.** Windows requires a signature it trusts, so this release's
  MSIX is for inspection and archival; use the ZIP, which needs no installer.
- **One class of video adapter.** The matrix ran in full on the available discrete adapter; this
  machine has no integrated graphics, so Intel Quick Sync's decode path has never been exercised.
- **`PLY-004` blocked:** 5.1 and 7.1 selection has not been exercised because no audio endpoint
  declares more than two channels.
- **No ARM64,** no Store, and no updater: those arrive with the first stable release.
- **Automatic version grouping is not wired.** The version comparison exists and is tested, but
  nothing creates groups today, so in the artifact it appears only if a group arrived some other way.

[0.1.0]: https://github.com/apvisualsolutions/ap-reelume/releases/tag/v0.1.0
