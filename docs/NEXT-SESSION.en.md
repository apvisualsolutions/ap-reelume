# Where to pick up

## State at the close of 2026-08-28 (fifth session) — the queue of four points, closed entire

**`main`, the branch and HEAD are the same commit: `935fcb0`, green checked with
`gh run view --json conclusion`.** Nothing is left unpublished or unverified. `main` moved **six
times** in the session — `8aa2304`, `a16a523`, `d7b5804`, `f0a94ac`, `7e86525`, `935fcb0` — each with
its run's conclusion read **before** the reference moved.

**Two reds along the way, both real and both fixed**: a coverage floor that had to rise with CI's own
number (`ArtworkCache.cs`, 96/70), and the walk's beside-click landing on the neighbouring button
because the transport row recomposes. Neither reproduced locally.

**And a lesson about pace**: pushing four commits in a row put **three runs in flight at once**, and
one run's `Verify` step went from 31 to **48 minutes** competing with the others. Grouping the
finishing touches would have been cheaper.

**Point 1 of the queue is closed.** The mini player opens with no frame, is dragged by the picture,
resizes from its eight edges at the prototype's **16:9** — which belongs to the picture, with the
chrome's height added on top — and **remembers where it was left between sessions**.

### What decided the design was two measurements, not two arguments

1. **An Avalonia button does NOT mark its press as handled.** The first draft of the drag skipped
   what another control had already handled, and it was measured with a harness `MouseDown` on
   `MiniPlayerPlayPause`, with a handler registered `handledEventsToo: true`: `seen=1 handled=0`.
   Avalonia marks the **release**, which is where the click is. That guard protected nothing, and
   **all five chrome controls would have dragged the window instead of working**. What decides now is
   where the press lands: the picture drags, the strip the chrome sits in does not.
2. **A headless backend never raises a user resize**: every one arrives as `reason=Layout`. A filter
   `e.Reason == User` buried in the override would have left the whole correction behind a branch no
   test can take, so the decision lives in `HandleResize(WindowResizeReason)`, public, and the test
   calls it.

### The house defect again, and closed

`PlayerWindowCoordinator.Remember` and `Recall` had existed since 2026-08-19 and **the product code
never called them**: `0` calls in `src/`, `3` in `tests/`. Registered and never fed. `ShellView` now
calls them, and the coordinator writes the half that outlives the process through
`IMiniPlayerPlacementStore` → `StoredMiniPlayerPlacement` over the same old `ISettingsStore`.

The placement is written **when the window closes and not while it moves**: a drag raises an event
per frame and this goes to a file. And a placement that no longer lands on any screen is dropped
**when it would be used**, not when it was stored: with no title bar, a window at the coordinates of
an unplugged monitor could not be brought back.

### What to watch in this batch's CI

**`ShellView.axaml.cs` rises above its floor, and that is a coverage-gate red**, which refuses a file
above its floor exactly as it refuses one below. Measured on this machine with the same suite:
`97.50/67.85` before → `98.03/69.23` after, once three guards that guarded nothing came out (the
`??=` reusing a window the shell had already closed, the `sender is not MiniPlayerWindow` of a handler
attached to exactly one window, and the second `Screens.Primary?.Bounds ?? …` written beside the
first). **The fix is the number from that run's `coverage-debt` artefact**, never one measured here.
`MiniPlayerWindow.axaml.cs` is not in the list, its bar is 96/96, and it measures 98.73/97.61.

### Point 2, and the scope decision that had to be asked for

**The poster was two ends away from existing, and what was missing was not code but a decision.**
Measured before touching anything:

- `PosterPath` holds TMDB's `poster_path` exactly as it comes — `/wXsQ….jpg`, a **remote** path — not
  a local file.
- `ArtworkCache` exists **complete and tested**, with its 10 MB ceiling (SEC-005), and
  `image.tmdb.org` has been declared in `NetworkPurposeRegistry` all along.
- But it was **out of the container** by ART-A01 (2026-08-09), and `CompositionDescriptorTests`
  **asserted its absence**. Plus the 2026-08-21 decision leaving covers out of 0.2.0.

With two recorded decisions against it and a test tying one of them down, **the question was asked**
and the owner answered: reverse ART-A01 and do the whole thing. It is done, in the order ART-A01's own
entry had written.

**A card never opens a connection.** The port is asymmetric on purpose: `Find` only looks at the disk
and `FetchAsync` is the only thing that reaches the network, called once from `ApplyIdentification` —
the one moment somebody has already consented to talk to the provider. The consequence worth knowing:
**a library identified before today gets its posters at its next identification or refresh**, the same
way a scanned title got its name.

`PosterAddressPolicy` checks **before** it composes, as the trailer's policy does, and its test also
asserts a property: whatever is built is always `https`, on the declared host, under the size segment,
with no query and no fragment.

### What to watch in this batch's CI

**And here the merged-report trap took its tenth false alarm, on this very batch.** The forecast
written before CI measured said `ArtworkCache.cs` and `MovieDetailsViewModel.cs` both rose to 100/100
and that the first would leave the list. **Both were false**: CI measured `ArtworkCache.cs` at
**96/70** and did not name `MovieDetailsViewModel.cs` at all.

The cause is exactly the one the 2026-08-25 note warned about: **the gate measures from the merged
report**, and the local reading was taken by **best report per suite**. A file a suite does not
exercise appears in its report as zeros, and the merge adds those in rather than keeping the best. A
script that takes the maximum **misleads**, and it did.

The floor is `96 70`, from that run's artefact, and `ArtworkCache.cs` **stays on the list**: 96 lines
meets the bar, 70 branches does not. The ratchet stays at 212.

### A CI red that does not reproduce here, and its cause

`The_players_transport_is_operated_with_the_mouse` answered `Expected: Embedded, Actual: Fullscreen`
on CI, in an assertion read **before** any mode button is pressed. **It does not reproduce locally**:
the case alone three times and the whole suite twice, all green.

The cause is in the scene and not in the product: it sends the session to **1.5×** just before, and
"Back to 1×" **appears** when the speed leaves 1×, so the transport row recomposes and everything
beside it moves. `PressAsync` picks the point it clicks "beside" a control from the **geometry on
screen**, and it picked before the room made for the reset had been measured: it landed on the
fullscreen button next door.

Fixed by settling the layout — `InvalidateMeasure()` + `RunJobs()` — between the line that recomposes
the row and the `PressAsync` that aims at it, and by reading the mode at that point so a
beside-click that moves it is caught where it happens. **Rule: if a line of the scene changes which
controls are in a row, settle the layout before aiming at that row.**

### A new local trap: `PackagingTests` is red here and green on CI

Three packaging tests fail **on this machine** — `Arm64PackageTests`, `ReproducibleBuildTests` and
two of `MsixLifecycleTests` — and the first says `BackgroundColor="#08090C"` expected against
`#111827` measured. **It is not from this batch**: measured with `git stash`, and the same three fail
with none of the changes in place. And CI passes them — 191 passed and 3 skipped on `d91b9d6`'s run —
because the workflow generates the package artefacts and this machine's have been stale for days.

So **`PackagingTests` is not a suite affected by view or use-case work**, and its red here means
nothing until the whole sandbox cycle has been run.

### Three decisions that were open, taken here

1. **Hypothesis (a) of "sections cut off" — with full lists — gets NO gate of its own.** Such a gate
   would have to build a data context for the 17 views that hold an `ItemsControl`, and what it would
   measure is already covered from two sides: **the rows and cards those lists repeat are views in
   their own right and are measured alone** — `LibraryEntryView`, `EpisodeRowView`,
   `CandidateCardView`, `PosterCardView` — and **the walk covers the application with seeded data and
   refuses a click that does not land**. The gate costs a lot, its new surface is small, and a fragile
   gate somebody has to maintain is worse than a stated absence. **Criterion if a real finding
   appears**: attack the one view with its context, not all 17.

2. **The mini player's chrome in the prototype's composition — title, time and a three-pixel progress
   bar above the five buttons — is a piece of its own and goes on the queue, not in as a finishing
   touch.** It is one view's composition, the size of a §4 tranche. **And it comes with a measurement
   already made**: at 480×270 those five buttons folded into three rows once because of a translated
   word, so whoever does it measures the width **in both languages** — which is exactly what both
   width gates do as of today.

3. **`ArtworkCache.cs` is NOT raised to the bar now, and there is a ceiling that explains it.** Its
   floor is `96 70` from CI's own number. What it lacks is measured, and most of it is cheap: the
   `image/png` and `image/webp` answers of `MediaExtension`, the null side of the `??` on the
   `HttpClient` and on `allowedHosts`, and both `Directory.Exists` in their opposite branch. **What
   sets the ceiling is `EnsureRemoteRootIsConfined`**: its `throw` is a security invariant no
   legitimate caller can reach, and **that one is not deleted** — it is not a redundant guard like the
   ones removed on 2026-08-28, it is what keeps the cache inside the data root. Raising it would mean
   rewriting that guard to be reachable, trading a security promise for a coverage point.

### The queue, with points 1 and 2 struck out

1. ~~The mini as a real PiP window.~~ **Done.** All that is left of its chrome is the prototype's
   composition — the title, the time, and a three-pixel progress bar above the five buttons — which is
   another piece and not a finishing touch on this one.
2. ~~The poster behind the film card's header.~~ **Done, on both cards** — the film's and the show's,
   which the prototype raises at 136×204 against the same bled wall. The library grid's covers and
   Home's three rows **are still out**, with the measured reason of 2026-08-21 intact: they drag the
   grid along, which costs 7× the time and 455× the live controls for losing virtualisation.
3. ~~The metadata editor as a surface of its own~~ (decision 15). **Done.** It is the prototype's
   page: "Back · Library", a two-line header, two `segment` pills, and the tool below. It is not an
   `AppRoute` — the five destinations are asserted by name and the walk reaches each by its rail
   button — but a page that **covers** the library's slot, the way a session does.

   **Two things that only showed up when measured**, and they hold for the next view that moves:
   binding the list to `!HasEditorPanel` alone drew the library **over Settings** (`ThemeTests` caught
   it by counting 16 buttons where 13 exist), and **the walk found a dead end** —
   `TitlePreviewRenameAction matched 0 controls` — because the page covers the card it was opened
   from. So both pills **open** as well as select, which is what the prototype does.

4. ~~"Sections cut off by the width"~~ **Found, and it was not the width.** `HomeView` asks for
   **683 px** and was the one destination mounted in a bare `ContentControl` with no `ScrollViewer`:
   with `MinHeight` at 600, **83 px of Home could not be reached**. That is hypothesis (c), the
   vertical one.

   Two new gates close it: the language became a **parameter** of both width suites — they pinned
   `es-ES`, so the other language was a guess, hypothesis (b) — and a third asserts that **a view
   taller than the window is inside something that scrolls**, reading the shell's own tree rather than
   a list.

   **(d) is measured and ruled out too**: at 1920 px only two views stay short, `ContinueCardView`
   (332) and `PosterCardView` (148), **and both are cards** — growing with the window is exactly what
   they must not do. No page stays short, and it carries no gate because the two exceptions are
   legitimate and permanent.

   **Only (a) is left**, with full lists: the walk covers part of it by refusing a click that does not
   land, but refusing a click is not measuring every control, and that difference is what has no gate.

## State at the close of 2026-08-28 (fourth session) — the debt list's crack, closed and gated

Eight commits on the branch, **CI green on `c85b6cb`**, and **`main` fast-forwarded to that same
SHA**: the branch, `main` and HEAD are one verified commit, and **nothing is left unpublished or
unverified**. `main` moved three times in the day — `8ce6ef8`, `e49a5e6` and `c85b6cb` — each with its
green checked through `gh run view --json conclusion` **before** the reference moved.

The three numbers from the final green, which are what close the session:

- `Coverage gate: 212 file(s) still short of 96/96, ratchet 212, **212 measured under the bar**` — the
  list and the measurement are the same number. They were 212 and 216.
- `The walk: 228 declared command controls in 223 identities; 203 pressed, 20 pending` — the ratchet
  still, with one identity more.
- `2 new file(s) against origin/main ... are where they have to be` — `ScannedTitlePolicy.cs` and
  `NameScannedTitles.cs` reach the 96/96 a new file has to arrive with.

All four points of the brief are closed. The fifth — the mini as a real PiP window and the poster
behind the details header — **was not touched**: each is a whole piece rather than a finishing touch,
and opening one halfway would have been worse than leaving it named.

**The queue, in order:**

1. **The mini as a real PiP window.** Half done: it no longer duplicates the bar, it is already
   `Topmost`, and it already has per-mode geometry. Missing: undecorated (`SystemDecorations="None"`),
   draggable (`BeginMoveDrag`), aspect ratio kept while resizing, and **remembering where it was left
   between sessions** — today `PlayerWindowCoordinator.DefaultMiniGeometry` is a constant.
2. **The poster behind the details header** (decision 6). **Note, and it is measured**: `PosterPath`
   exists in the metadata and **reaches no view** — `MovieDetailsViewModel` does not read it — so it
   is two jobs: carrying it to the card and drawing it. And an unidentified library holds no poster at
   all, so the generated art has to stay behind it regardless.
3. **The metadata editor as a view of its own.**
4. **«Sections cut off by the width»**, with the width axis now ruled out — see below.

### 1. The speed menu is the prototype's drop-down

It was a `MenuFlyout` of ten numbers with an eleventh row that reset. It is now the pill the prototype
draws, with **nine** rows of three columns — mark, name and note — opening **upward**, and «Back to
1×» as a button beside it.

**What decided the shape was the walk and not taste.** Nothing inside a `Flyout` can be reached by the
harness: all twenty entries of `eng/walk-pending.txt` are exactly that, flyout children, and that
ratchet does not go up. A `ComboBox` is pressed and asserted on `IsDropDownOpen`, the way the
library's two filters already are, and its rows are `ComboBoxItem`s, which the inventory does not
count. So the ledger gains **one** identity — the reset — and the walk went from 202 to **203
pressed**, with the ratchet still at 20.

Along the way: `PlaybackControlPolicy.SpeedSteps` **was read by nobody**. The menu wrote its own ten
numbers into its own markup and a test read that `.axaml` back **as text** to compare. The menu is
built from the policy now and the test asks the model. The `1.75×` the prototype does not offer went
with it.

### 2. The transport's glyphs

Only one was wrong, and the rest already matched the prototype character for character — that has
been pinned by `PrototypeIconTests` since 2026-08-24. **The full-screen button drew the entering
arrows while already full screen**, and `IconExitFullscreen` had been in the dictionary the whole
time: the same defect the mute button had in August.

What hid it: **the table pinning which glyph each button carries listed eleven of thirteen.** The two
mode buttons had been on the bar for three days without being on it, and the one that was wrong was
one of the two that were missing.

### 3. An unidentified film's title

«El Faro de Piedra 2019» was the file name verbatim, with the year inside the title and the year
column empty beside it. `ScannedTitlePolicy` (domain, pure) says what the card is called, and
`NameScannedTitles` — a sibling of the other two post-scan use cases — writes it. Migration **0021**
for the year.

**An already-catalogued library renames itself on the next scan with nothing re-probed**, because the
pass walks the whole summary, `Unchanged` included — a file whose size and date have not moved is
never re-stored, so a projection written once would have kept the raw name forever.

### 4. The debt list's crack, closed and gated

**Measured**: three consecutive CI runs measured **216** files below the bar while the list named
**212**, and the four missing ones measure the same in all three — they do not dance. And no file on
the `$watched` list falls below the bar on CI, so the difference is exactly those four.

All four were **closed** rather than written down, so the ratchet stays at **212**:

- `PlayerView.axaml.cs` **65/41**, the lowest pair in the tree. Two views mounted it and neither gave
  it a context, so **neither of its two handlers had ever run**.
- `PlayerViewModel.cs` **98/91**, by removing three guards nothing could take.
- `PlaybackPreference.cs` **98/92** and `DisabledOutline.cs` **100/87**, with the arm nobody took.

And `check-coverage.ps1` now asks the list to be **complete** and not merely accurate. Off CI it
reports and does not block, exactly like the floors.

### Three decisions that were open and are not any more

1. **An accent landing on the focus ring is kept as picked.** The question was "one step or a ratio?"
   and the answer is neither: the focus adorner is **two concentric rings** at 3:1 against each other,
   so the keyboard's signal is **geometry** and survives any accent. The one-byte nudge was theatre
   and it is gone, along with the parameter that fed it. What is still watched is what everybody sees:
   the four dictionaries, in `ContrastTokenTests`.

   **And here the new gate earned its keep on its first run, over a change from this very batch.**
   Removing the nudge thinned `AccentPalette.cs` just enough for its long-standing hole to weigh: CI
   measured it at **99/93** and named it for being on no list. The hole was a fallback `return` in the
   lightness walk that **no predicate in the file could reach** — `EqualContrastLuminance` is where
   black and white contrast equally, and that contrast is 4.58:1, above the strictest 4.5, so one end
   always accepts. With the end moved inside the walk, the file sits at **100/100**. Without the gate,
   nobody would have seen it.

2. **"Home comes up empty" was already fixed** — `HomeReadModel` has done `UNION ALL` over
   `scanned_titles` since 2026-08-25 — **but the thing beside it surfaced**: migration 0021's year was
   put into the library's union and not into Home's. They are one query written twice, and there is
   now an assertion tying them.

3. **«Secciones cortadas por el ancho»: measured, and NOT on the width axis.** It was the first
   limitation `ViewOverflowTests` declares about itself — each view measured alone at 900, while
   inside the shell the rail takes 64 — so `ViewOverflowInShellTests` measures them against the real
   room, **836 px taken off the shell**, the player included because the prototype leaves the rail on
   screen for an embedded session. **Not one of the 48 exceeds it.** The absence is proved.

   **What stays alive as a hypothesis**, so the work is not repeated: (a) with the lists **filled**,
   which is the second limitation both gates still declare; (b) with the **other language's** strings,
   which already folded the mini chrome into three rows once; (c) that "cut off" is **vertical** and
   not horizontal — Settings measures 1,797 px tall, and that fits the word; (d) at a **wide** window,
   where the failure is not something spilling out but something not growing.

### The traps that cost time here

- **A model that resolves a resource in its constructor makes every one of its callers a UI-thread
  caller.** `SpeedOptions` was built there and two `[Fact]`s that only asked about a playhead failed
  with "the calling thread cannot access this object". It is built on first read.
- **`Gestures` is internal in Avalonia 12.1.1**; the public event is `InputElement.DoubleTappedEvent`.
  It is the same class of premise that already failed over `ItemsRepeater`: checked, not assumed.
- **A `ContentControl` whose `IsVisible` is bound to a model property is not filled by setting its
  `Content` by hand.** The transport has to be given to the `PlayerViewModel`, or it sits in a hidden
  container with no children to find.
- **A PowerShell script that rewrites a file can change its line endings**, and `dotnet format` catches
  it as `ENDOFLINE` on every line of the whole file.
- **The schema version has three assertions**: the count, the maximum and the list of names in
  `SqliteBootstrapTests`. A new migration moves all three.
- **A `Test Case Cleanup Failure` reading "the calling thread cannot access this object" is NOT the
  code: it is the harness recycling itself.** CI failed that way on 2026-08-28 in
  `TransportGlyphTests`, and the whole stack was Avalonia's —
  `HeadlessUnitTestSession.EnsureIsolatedApplication` → `Compositor..ctor` →
  `Dispatcher.VerifyAccess` — without one line of ours. **And the test xUnit named was not the
  culprit**: it lasted 1 ms and never ran; what failed was preparing the application *for* it. The
  cause was the new test beside it, opening a fourth window by hand in a class whose scope already
  opens three per test, and not closing it when an assertion failed. **It does not reproduce
  locally** — two passes of 958 green — so the way out is the usual one: a race is removed, not
  hunted. The test moved to the mounting pattern that had just gone green on CI, with its window
  closed in a `finally`.

- **`App.ApplyLanguage` replaces the dictionaries and does NOT touch `CultureInfo.CurrentCulture`.** A
  test asserting «0,25×» after applying the language passes on a machine in es-ES and **fails on the
  runner**, which is in en-US and writes «0.25×». CI caught it and the tree did not. The fix was not to
  drop the assertion but to **set the two separately**, which makes the test say something stronger
  than before: the number follows the machine and the words follow the chosen language, which is what
  somebody running Windows in English with the application in Spanish actually sees.
- **`MediaTests` hangs on this machine when it runs inside the solution with
  `--collect:'XPlat Code Coverage'`**, and passes in 1 m 37 s on its own. Two consequences, both
  misleading: it leaves a `testhost` holding the `.dll`s — so the next build fails with `MSB3026` and
  looks like a code error — and it **leaves no report**, so five LibVLC files show up in the coverage
  gate as "fell to 3/2" when all that happened is that nobody measured them. Measure suite by suite.
  The floors are still CI's, which is exactly why that rule exists.

## State at the close of 2026-08-25 (third session) — the eight of the brief, closed and measured

Six commits on the branch. **Everything green locally**: Domain 519, Application 246, Architecture
30, Documentation 91, Ui 917, Accessibility 146, Integration 470, and the walk at 202 pressed with
the ratchet still at 20.

**CI GREEN twice in a row** (`7c1decb` and `10dedc9`) and **`main` fast-forwarded to `10dedc9` on
2026-08-26**, under the owner's instruction to close the pending decisions: 65 commits from several
sessions published at once. The branch, `main` and HEAD are the same verified SHA — nothing is left
unpublished or unverified, and the next session starts from a green.

**What had to be looked at first:** the coverage gate in CI. The two floors the previous session left open —
`ShellViewModel` and `CompositionRoot.cs` — are still the only thing between this branch and a
fast-forward. `ShellViewModel` is **closed in this session**: the missing branch was the subtitle
panel term inside `HasPlayerPanels`, which **nothing could take** — it is `Player?.Tracks is not null`
and so is half of `HasAudioPanel`, which is evaluated first — so it was deleted rather than written an
impossible test, and the four alternatives left are now asked for one at a time. `CompositionRoot.cs`
was not touched on the null side of `ShellHost.Shell` in the `ModeHandler`, which is still the branch
that is missing.

### The brief, point by point

Eight points: two improvements and six defects. All eight are closed, each with its number.

1. **The buttons' vertical alignment, for the third time, and this time with the right number.** The
   five pixels lived in the **button's padding**, which moves the whole content: a glyph and the word
   beside it travel together, so they stayed exactly as far apart as before. Measured in a 44 px
   button: glyph at 19.00, ink at 21.43 — **2.43 px**, the same number as always, untouched by the
   correction that was meant to fix it. The margin now goes **on the label**, and
   `ButtonOpticalCentreTests` also holds the icon against the word, which was the half no gate was
   looking at.

2. **The selection boxes in menus.** They were 2 px of accent around every chosen row in the whole
   application, rail covers included. The prototype does it the other way round: a neutral wash,
   `rgba(127,145,170,.16)`, with the accent on the destination's 3 px bar. Two new tokens and a
   one-pixel border. **The measured concession**: the stroke is not transparent because
   `ListRowStateTests` asks for 3:1 and a 16 % wash reads 1.1:1 — so it carries the neutral 3.88:1
   border.

3. **The library's filter pills** carried the control border and the primary ink when unchosen, so
   three options looked like three chosen ones. And **the drop-down said nothing when it opened**
   beyond turning its caret over.

4. **Home's covers led nowhere** (they were cards inside a list item). On the way: **the suggestions
   rail was drawing twenty covers of the initials of nothing**, because its title lookup was an
   optional parameter the composition never passed.

5. **"From the start" on Home's two wide surfaces**, with a flag on the request rather than a second
   hook.

6. **Continue asked again** what pressing Continue had already answered: the position the caller names
   had won over the policy since the version switch, and the other half of that decision — not
   building the offer — had never been made.

7. **The play button disappeared on the card** when there was no progress. It is now one button whose
   words follow the state, which is what the prototype writes, and the glyph is the alternative and is
   drawn only while there is something to be one to.

8. **Series.** See below: it is the largest thing in the session.

### The coverage gate, with CI's numbers in front of it

The previous note said **two** floors were open. There were **four**, and CI's `coverage-debt`
artefact said so from before this session. With the three batches above:

- `ShellViewModel` **reaches the bar** and leaves the list: the missing branch was the subtitle
  panel's, which nothing could take.
- `HardwareAccelerationFallback` leaves too: its `Reset` had no caller — not in `src/`, not in any
  test — and with it gone the file is at 100 %. **Careful:** that removal is arithmetic and not a CI
  measurement (13 of 17 lines were the 76 % it measured, and the four missing ones were `Reset`'s).
  If CI says otherwise, the line goes back on the list.
- `CatalogRepository` (98/89), `LibraryRootRepository` (100/90) and `RecommendationsViewModel`
  (96/90) **go up**, measured.
- `LibVlcMediaPlayerEngine` (91/79) and `CompositionRoot.cs` (90/65) **come down by a point**, and
  that is not a relaxation: the rule says a floor above what was measured fails exactly like one
  below it, and those two carried a number from a luckier run. The branches CI cannot take in the
  engine are LibVLC's audio-device enumeration and the `EncounteredError` event: a hosted runner has
  no hardware to raise them with.
- The ratchet goes from **214 to 212**.

And a note for the next time a floor has to move: **the report CI measures merges more suites than a
local run of one of them.** `MovieDetailsViewModel` read 82.54 % measuring `UiTests` alone here and
83 there, because the accessibility suite walks that file too. Measuring locally is worth it for the
**direction** and for not spending a CI round blind; the number written into the file is CI's.


`CompositionRoot.Library.cs` fell from the bar to 97/50 with the hooks this batch added, and **came
back to the bar** as soon as the walk pressed its five arms: with no shell, with a card for a title
the catalogue no longer holds, and with a card whose progress is gone. It does not need to go on the
list.

What is still open is the crack it was falling through unnoticed: **the list only watches what is
already on it**, so a file that was at the bar and degrades is seen by nobody. CI measures four like
that right now — `PlayerView.axaml.cs` at 65/41, `PlayerViewModel.cs` at 98/91,
`PlaybackPreference.cs` at 98/92 and `DisabledOutline.cs` at 100/87 — and they predate this session.
Adding them raises the ratchet, which only comes down; closing them means taking them to 96/96.

### Series, which was this house's defect in its largest form

`titles`, `seasons`, `episodes` and `episode_media` have existed since migration **0004**, the series
card has been drawn and routed to since it was written, `MediaNameParser` has read `S01E01` since day
one — and **nothing had ever written a row into any of the four**. LIB-005 stood as `VERIFIED` on
evidence that measures the parser, and the parser works: what was missing was a caller.

Two pieces, neither in the view: `LocalSeriesPolicy` (domain, pure) says which folder names the
series — **the folder, never the file** — and `GroupScannedEpisodes` runs after every scan and writes
the show, its seasons, its episodes and the file behind each one.

Measured end to end over the real tree: **99 files go in and 3 cards come out** — two shows of 72 and
27 episodes with their eight and three seasons, and the film that was in the same root — with a file
behind every episode. The evidence is in
[docs/evidence/stable/audit-lib005-a-folder-of-episodes-is-a-series.md](evidence/stable/audit-lib005-a-folder-of-episodes-is-a-series.md).

### The element catalogue, written down

[docs/design/ELEMENTS.en.md](design/ELEMENTS.en.md) carries the prototype's catalogue over into this
tree's tokens, element by element and state by state, with a rule of precedence: the prototype wins
over the document and the document wins over the `.axaml`. The icons were already the prototype's —
`Theme/Icons.axaml` converted them on 2026-08-24 — and it has been checked that not one Segoe Fluent
glyph is left in any view.

### The traps that cost time here

- **A floor that goes up in a single run can be a dance, not an improvement.**
  `MarkerEditorViewModel` measured 79, 79, 79, **81**, 79 over five consecutive CI runs. I raised it
  to 81 on the fourth and the fifth failed on it. The gate says "now reaches X; raise its floor" as
  soon as **one** run measures higher, and a floor has to be met by **every** run: one run's artefact
  is a measurement, not a trend. Before raising a floor, look at several runs' artefacts — they come
  down with `gh run download <id> -n coverage-debt`.

- **A walk scene that spends the progress has to put it back rather than trust the machine.** The
  hero and the rail card are drawn only while there is something to continue; the sample is ninety
  seconds long and Continue opens it at thirty, so a busy runner lets the rest play out, the tracker
  stores the end, and both surfaces disappear. CI measured it on 2026-08-25: the same scene passed
  three runs and failed the fourth on "Home came back without the hero's Details on it", with
  nothing between them that touched the walk. The progress is seeded again after every session that
  spends it, which is the same answer the two «from the start» presses already needed.

- **A new test over a model that reads resources needs `[AvaloniaTheory]`**, not `[Theory]`. It went
  unnoticed locally because of execution order and CI caught it: "the calling thread cannot access
  this object".
- **An XML comment cannot contain two consecutive hyphens**, so the prototype's CSS variables quoted
  inside an AXAML comment break the markup build, not the code build.
- **`Guid` keeps its first three fields little-endian**, so the byte a canonical UUID calls the
  seventh — the version byte — is index **7** of the array and not 6.
- **Making both covers pressable turned up a real ambiguity**: one title can be on "Recently added"
  and on the suggestions rail at the same moment, and the walk refused to press a name that matched
  two controls. It was resolved the way the rail already did it: the accessible name says the rail and
  then the title.
- **"From the start" erases the progress when it closes**, so the hero and the rail card disappear.
  There is no order in which both presses survive: the walk puts the progress back between them.

### What is left

- **The speed menu** is still a `MenuFlyout` of eleven numeric rows.
- **The transport's glyphs, one by one against the prototype.** Not started.
- **The mini as a real PiP window.** Half done.
- **The poster behind the card's header** (decision 6).
- **The metadata editor as a view of its own.**
- **"Sections cut off by the width"**, still not located.
- **The title of an unidentified film is its file name verbatim** — "El Faro de Piedra 2019". It is
  **asserted** in the series test rather than corrected: now that the parser is used for grouping,
  cleaning it up for films too is half an hour, but it is somebody's decision.

## State at the close of 2026-08-25 (small hours) — nine of the twenty-four, and CI nearly green

Nine commits on the branch. **Everything green locally**: Domain 499, Application 241, Architecture
30, Documentation 87, Ui 904, Accessibility 146, Integration 467, Media 149.

**What to look at first:** CI on HEAD. The coverage gate went from **seven** fallen floors to **two**
(`ShellViewModel` and `CompositionRoot.cs`, one branch each after this round), and seven floors were
raised by copying them from CI's artefact. If they are still red, they are measured below: the branch
missing in the composition root is the null side of `ShellHost.Shell` in the session's `ModeHandler`,
and the shell's is one of the sixteen in `HasPlayerPanels`.

### What was closed, with its measurement

Besides the first round (subtitles, Home, the picture's ratio, the two mode buttons,
`:focus-visible`, tooltips, the drop-downs' alignment, the dotted outline):

1. **The rating is five stars** with migration **0020**, which halves and rounds up. What says a star
   is given is its fill. The rule lives in the domain as well (`PersonalStatePolicy.ToFiveStars`)
   because a migration runs once against a file and a restored backup is a number arriving later.
2. **The minute on "Continue" rides inside the button**, not beside it.
3. **"From the start" is the restart arc with its arrow on the other side**, and its button is 36 px
   and round, like the two beside it.
4. **Every icon is two pixels smaller** with its stroke scaled (width ÷ 15).
5. **No button is square.** Eight classes were. The pill token was 18 — half of a 36 px control — and
   is **999** now, which the renderer clamps to half the shorter side: a square target is a circle and
   a wide one is a pill from one number. `ButtonShapeTests` reads the button styles out of the token
   file **and looks at the corner pixel**, because 999 would satisfy any numeric comparison while the
   renderer decided otherwise.
6. **The hardware-acceleration warning no longer appears on every playback.** The engine does not ask
   for a graphics-card surface because it cannot compose subtitles onto one; neither requested nor
   active is what actually happens.

### What is left of the twenty-four

- **The speed menu** is still a `MenuFlyout` of eleven numeric rows. The prototype draws it with a
  mark, a name and a note. It changes the identity of eleven controls in the walk's inventory, so
  their presses travel in the same commit.
- **The transport glyphs, one against one with the prototype.** Not started.
- **The mini as a real picture-in-picture window**: frameless, always on top, draggable, keeping its
  ratio and remembering where it was left. Only half done — it no longer duplicates the bar.
- **"Play" when there is no progress.** Today a film with no progress shows no play button at all,
  only the start-again glyph. It needs a second button or a name that moves with the state, and both
  change the walk's inventory.
- **The poster behind the card's header** (decision 6). Note: `PosterArtView` draws **generated art**
  from the title's hue; the real poster travels in the metadata's `PosterPath`, and an unidentified
  library has none.
- **The metadata editor as a surface of its own.**
- **"Sections cut off by the width"**, still not located.

### The traps that cost time here

- **The coverage gate measures from the merged report** at
  `artifacts/test-results/verify-win-x64/coverage-gate/Cobertura.xml`, not from the individual ones.
  A script that takes the maximum per line across the individual reports gives different numbers and
  **misleads**.
- **Floors are copied from CI's `coverage-debt` artefact and only ever move up.** Several files
  measure differently here and there, so the list cannot be closed without a CI round.
- **A file new against `main` has to reach 96/96**: it cannot join the debt list, because the ratchet
  drops with every file that leaves it and frees no slot. When an infrastructure file cannot get
  there, the way out is to **lift the rule into a pure function** — `LibVlcTrackIdentity` did —
  because a branch in the adapter is only taken by a machine with a decoder.
- **Moving the rating scale broke 63 integration tests from one cause**: a fixture seeds a number.
  Before calling a change large, look at what the first failure actually says.

### One finding still undecided

The nudge that moves the accent off the focus ring **moves one step**: for a ring of `#005A9C` it
returns `#00599A`, a different colour by the byte and the same one to the eye. It is written into the
test rather than asserted away. If that matters, the decision is how far apart they have to be.

## State at the close of 2026-08-25 (evening, second session) — five of the twenty-four, measured

Five commits on the branch. **Everything green locally**: Domain 498, Application 241, Architecture
30, Documentation 87, Ui 897, Accessibility 146, Integration 467, Media 143.

**CI was still red when this started, and not for the reason the brief gave.** The run for `897dfda`
did not fail on a gate already corrected: it failed on the **coverage gate**, with seven files below
their floor and one improved without its floor being raised. That is what had been blocking the
fast-forward to `main`, and a good part of this session went into giving them back.

### What was closed, with its measurement

1. **Subtitles never reached the screen, for three causes and none of them visible by reading.**
   - The session switched them off on the way in: with nothing stored the resolved value is "off" and
     it was applied anyway, handing the engine `-1` over the track the container marks as default.
   - **The chroma decided whether VLC composed the subtitle at all.** With `RV32`, `RGBA`, `ARGB`,
     `RV24`, `YUY2`, `VYUY` and `YVYU`, **not one byte** of the frame changed when it was switched
     on; with `UYVY`, 61 687 did. The engine asks for `UYVY` and converts to BGRA itself
     (`PackedYuvConverter`).
   - **With D3D11VA the compositing fails and VLC says so once per frame**: "no matching alpha
     blending routine (chroma: YUVA -> DX11)". 67 001 bytes change in software and none in hardware.
     The engine decodes in software and says that it does.
   - Verified end to end against the owner's own episode: the published frame carries the subtitle
     inside it, in bands 15 and 16 of 16.
2. **Home was empty on a full library.** Measured against his database: 102 rows in `scanned_titles`,
   **zero** in `titles`, and four in `watch_state` that match scanned files and nothing else. All
   three projections read the same union the library lists. And the route the application opens on is
   announced like any other, because Home was only ever read on *arriving* at it.
3. **The video was stretched when the window was resized.** `VideoFitPolicy` keeps the ratio and
   shares the bars; what is asserted is the **ratio**, not the size.
4. **The player**: full screen and the floating window in the transport bar (and **no longer in both
   places**, which the walk refuses), the double click, the keys heard on the way down — which is why
   space put the picture full screen — the right picture-in-picture glyph, and the bar that stops
   being duplicated inside the small window.
5. **Across the tree**: the ten selectors moved to `:focus-visible`, a tooltip on every button from
   one style, the drop-downs' vertical alignment (2.43 px, the buttons' own number, and the test
   fails without the fix), and the dotted outline spent only in the two high contrasts — it was
   **299** dotted rectangles across the tree with no data loaded.

### What is left of the twenty-four

- **The speed menu** is still a `MenuFlyout` of eleven numeric rows. The prototype draws it with a
  mark, a name and a note. It is the costliest piece: it changes the identity of eleven controls in
  the walk's inventory, so their presses have to travel in the same commit.
- **The transport glyphs, one against one with the prototype.** Not started.
- **The mini as a real picture-in-picture window** (frameless, always on top, draggable, keeping its
  ratio and remembering where it was left). Only half is done: it no longer duplicates the bar.
- **"Play" when there is no progress.** Today a film with no progress shows no play button at all,
  only the start-again glyph. Saying the right word for the state needs a second button or a name
  that moves with it, and both change what the walk's inventory holds.
- **The poster behind the card's header** (decision 6: the poster, not a frame of the video).
- **The metadata editor as a surface of its own.**
- **The five-star rating**, with its numbered migration halving what is stored.
- **"Sections cut off by the width"**, still not located.

### What to know before touching coverage

- **The gate measures from the merged report** in `artifacts/test-results/verify-win-x64/coverage-gate/`,
  not from the individual ones. A script that takes the maximum per line across the individual reports
  gives different numbers and **misleads**.
- **Floors are copied from CI's `coverage-debt` artefact and only ever move up.** Several files
  measure differently here and there — `LibVlcMediaPlayerEngine` gave 91/81 locally and 91/78 on CI —
  so the list cannot be closed without a CI round.
- **A file new against `main` has to reach 96/96**; it cannot join the debt list, because the ratchet
  drops with every file that leaves it and frees no slot.
- Three files could not be measured and now can: the appearance service is told **which window is on
  screen** instead of looking it up (an application's lifetime cannot be replaced once it has
  started), and a colour that already reads comes back **byte for byte**, so the nudge off the focus
  ring can happen at all.

### One finding nobody has decided

The nudge that moves the accent off the focus ring **moves one step**: for a ring of `#005A9C` it
returns `#00599A`, a different colour by the byte and the same one to the eye. It is written into the
test rather than asserted away. If that matters, the decision is how far apart they have to be.

## State at the close of 2026-08-25 (evening) — the owner ran the application against his library

This batch is four commits and **everything is green locally**: Domain 480, Application 236,
Architecture 30, Documentation 87, Ui 856, Accessibility 146, Integration 466, the walk at 198
pressed with 20 declared.

**What matters about this session is not what was built, it is what the owner found while using it.**
He ran the application against his own library — `E:\Series`, with Game of Thrones and House of the
Dragon — and brought back thirty-four things. Ten are done; twenty-four are not, and they are below
in his own words.

### What was closed

- **The player is headed by the prototype's pills** — Audio, Subtitles, Video, Markers and Other
  versions — and the column starts closed, with a header of its own and its «×». The panels group by
  subject rather than by model. «Video» is new: decoding, HDR and the scope. The «Session 1 · single
  active engine» badge and the output device at the right of the foot.
- **Playing takes everything but the picture away** and the mouse or a key brings it back. No timer,
  with the cost written down.
- **Settings → Appearance with the prototype's eleven rows**, with the accent derived by
  `AccentPalette` so any chosen colour still meets its five contrast obligations.
- **The accent now reaches the whole application.** Four brushes were written and Fluent's controls
  read their own, redirected with `<StaticResource>` — static, resolved once. All twenty redirections
  are written and `AccentTokenTests` insists none is missing.
- **Button labels sat 2.43 px low**, measured through the font's metrics and corrected with five
  derived pixels rather than an eyeballed nudge.
- **The two subtitle colours** are six swatches and a picker, like the accent.

## What the owner found and is NOT done

These are his words, grouped. **None of it is measured yet except where it says so.**

### The player, which is what he looked at most

1. **Double click does not go fullscreen.**
2. **The `F` shortcut does nothing**, and **the space bar goes fullscreen** — which is exactly what
   it must not do: space is play/pause.
3. **There is no fullscreen button in the transport.**
4. **The mini player's icon is wrong**, and **the mini window does not land in the right place**.
5. **The transport is drawn twice** in the mini.
6. **The mini has to behave like any PiP window.**
7. **The PiP icon belongs in the transport, next to the fullscreen one.**
8. **The speed menu belongs at the right of the bar**, and **its open design is not the prototype's**
   — the prototype draws nine rows with a mark, a name and a note («Normal», «slower», «faster»), not
   the `MenuFlyout` that is there.
9. **The video distorts when resized**, both in PiP and in the main player.
10. **Subtitles do not load.** He checked it in VLC with
    `E:\Series\Juego de tronos\Temporada 1\Juego de tronos - 1x01 - Se acerca el invierno.mkv`.
11. **The transport glyphs have not been compared one by one** with the prototype — from the original
    brief and still open.

### The film and show cards

12. **The play button is not the prototype's**: it has to say «Play» or «Continue» by state.
13. **«Play from the beginning» has to be an icon**, not a button carrying words.
14. **The banner should show the poster behind it, or even a frame of the video itself.**
15. **Editing metadata happens inside the same view**, and in the prototype it is **a page of its own
    with its own design**.

### Across every screen

16. **Dotted outlines appear where they do not belong**: Privacy and diagnostics, Backups, Updates,
    Duplicates, Review, Library and both cards. Also "some ellipse".
17. **Clicking a check box draws a blue ring**, on all of them. **Identified and not fixed**: the ten
    focus selectors in `DesignTokens.axaml` use `:focus`, which fires for the mouse too; what is
    wanted is `:focus-visible`, which answers the keyboard alone.
18. **Drop-downs have the same vertical alignment problem** the buttons had. The button fix is in and
    the recipe is the same: bottom padding compensates the font's asymmetry, and the number comes
    from the metrics.
19. **Every button needs a tooltip**, especially the icon-only ones.
20. **Rating has to be one to five stars**, filled or empty by state, with «clear rating» beside it.
    The usual Google ones. Today it is ten numbered buttons.

### Home and the library

21. **Home is completely empty** even with shows catalogued. **Half measured**: `Home.LoadAsync` only
    runs from `OnNavigated`, and `ReadRecentlyAddedAsync` reads the `titles` table — if the scan
    leaves files in the review inbox without promoting them to titles, Library shows them and Home
    does not. It still has to be checked against his database.
22. **"Sections cut off by the width"**, from the original brief and still not located. The closest
    thing found: the settings page measures 1,797 px of content.

## What was learned and has to be remembered

- **The autonomous walk cannot press anything below the first viewport of a scrolled page.**
  Avalonia's headless hit testing does not follow a `ScrollViewer`'s offset: reproduced in eight
  lines — the same view inside a scroller at offset 400, a button reporting 123x36 at y=419, and a
  click there reaching the scroller's own border — while unscrolled it answers to the bottom of
  1,700 px. Three ways round it were tried and all fail the same: sweeping the offset, swapping the
  window's content, and opening a second window. **The walk ratchet went from 0 to 20** with that
  measurement written into `eng/check-walk-coverage.ps1`, and it only comes down when the harness can
  follow a scroll, or when somebody chooses to press through a pointer event directed at the control
  instead of a window coordinate — which keeps "it was pressed" and gives up "it was reachable".
- **A `<StaticResource>` resolves once.** Anything redirecting to a token the application writes at
  runtime has to be written too. True of the accent and true of anything like it.
- **Centring a label's box is not centring the label.** The ink runs from a capital's top to a
  descender's foot and the font is not symmetric: 2.43 px in a 44 px button.

## Decisions already taken — do not ask again

Taken at the close of 2026-08-25 so the next session builds instead of consulting.

1. **Player keys.** `Space` plays and pauses. `F` and **double click** toggle fullscreen. `N` the
   mini. `Esc` closes. Space going fullscreen today is a defect, not an alternative.
2. **The mini player is a real PiP window**: frameless, always on top, draggable, resizable **keeping
   its aspect ratio**, with a minimal transport of its own — not the player's whole bar, which is
   what is drawn twice today. It lands in the bottom-right of the work area with a margin, and
   remembers where it was left.
3. **The video distortion is fixed by keeping the aspect ratio** with letterboxing, in the player and
   in PiP. The picture is never stretched.
4. **The metadata editor becomes a surface of its own**, as in the prototype, and stops living inside
   the card. It is reached from the card and left by a link, like both cards already do.
5. **Rating becomes five stars.** The stored value runs 1 to 10 today; it migrates by halving and
   rounding up, in a numbered migration. «Clear rating» sits to the right of the fifth star.
6. **The card's banner uses the poster behind it**, with the gradient the prototype already draws —
   not a frame of the video. Pulling a frame means decoding from the catalogue, which is new attack
   surface and a cost per title, and the poster already exists. A title with no poster keeps the
   generated art that is drawn today.
7. **The focus ring answers the keyboard alone**: the ten selectors move from `:focus` to
   `:focus-visible`. The disabled dotted outline stays — it is the only cue in both high contrasts —
   but every control carrying it has to be checked for being genuinely disabled.
8. **Tooltips go on every button**, and on icon-only ones the tooltip repeats the accessible name.
   One string, two places, and `ToolTip.Tip` never carries a literal with letters in it:
   `ViewLiteralTests` refuses that and is right.

## How the work goes here

1. The affected suites locally, commit, push the branch, **CI green**, and only then the fast-forward
   to `main`.
2. **The whole accessibility suite after touching any view.**
3. `eng/coverage-debt.txt` is copied from a CI run's `coverage-debt` artefact, never generated here.
   The ratchet is at **214** and only comes down.
4. A new control arrives with its walk scene in the same change — unless the harness cannot reach it,
   and then it is declared in `eng/walk-pending.txt` with the measurement, never in silence.
5. To see the application: build **Debug** and run that binary. While the owner has it open, Release
   stays free for the tests.

## State at the close of 2026-08-25 (afternoon) — what the owner looked at, and what is left

**Everything in this batch is green locally** (Domain 472, Application 236, Architecture 30,
Documentation 87, Ui 836, Integration 466, Accessibility 146, the walk at **zero pending**) and the
branch is 33 commits ahead of `main`.

### The first thing fixed was not in the code

A capture was called "dark" three times, and three times the file was right: **a 1500 × 1000 PNG
reads dark, and the same image at 750 × 500 reads as it is**. Measured over the library in light
theme — `#FBFCFE` on the canvas, `#E9EEF4` on the rail, 100 % opaque — and confirmed by halving it.
Colour is decided by measuring, or at half size; never at full size.

**And `docs/assets/review.png`, in a public repository, printed the profile path of whoever took
it.** All five are retaken with the library in a neutral folder, without an alpha channel.

### What was closed in the application

- **Duplicates**: the chosen copy marked on its whole row, the group heading no longer blue, the size
  column reaching the bytes.
- **Links**: from the accent to its ink — 9.03:1 against 5.62:1 in light — with the pair measured.
- **Series card**: each episode in the show's hue walked 7° per episode; the next-episode panel
  limits its column rather than its border.
- **Tray**: it says which title it is talking about (migration 0019, one column) and its four labels
  share one style.
- **Player**: a failure no longer erases itself when LibVLC reports the stop that follows it.
- **Home/library**: glyph-only kind chip in the rails, a plus on Add media, the header's second line,
  «SPEED» instead of the long label.
- **Other actions**: five matching rows instead of two pills above three rows.
- **Two title tools** appear only when they have something to do, and the external trailer says
  «Watch trailer» with its arrow.
- **The dotted outline** on a disabled control takes the control's own radius.
- **`KindShapeConverter` removed**: two branches nothing could take, replaced by a style. The coverage
  ratchet drops from 215 to **214**.

## What the owner asked for and is NOT done

**1. The player has to be an exact copy of the prototype, in design and in behaviour.**

The references are taken, one per state, in `%TEMP%\claude\…\scratchpad\proto-player\`. **They
are read at half size** (`half.ps1`).

How they were taken, which is what lets any prototype state be explored: the working copy
`scratchpad/proto/proto.html` accepts **`?press=A|B|C`** and presses those names in order, by
`aria-label` or by button text; `scratchpad/shoot-player.ps1` automates it with headless Chrome and
`--force-prefers-reduced-motion`.

What is missing, measured against those captures:

- The four pills — **Audio, Subtitles, Video, Markers** — belong in the player's **header** and open
  or close the column. Here they live inside the column, which is always present.
- The prototype's column has **a heading of its own with an «×»**.
- The **«Session 1 · single engine active»** pill beside the title is missing.
- **«System speakers · 2.0»** at the right of the footer is missing.
- The panels group differently: **Audio** = audio tracks + output device + channels; **Subtitles** =
  tracks + «Load external subtitle…» + its note; **Video** = decoding + HDR + note; **Markers** =
  automatically detected + this title's own.
- **The transport glyphs are not identical**; they have to be compared one by one.
- **Decided by the owner**: the **stop** button and the **«Other versions»** panel stay even though
  the prototype has neither, and are recorded as deliberate additions.

**2. Playing hides everything but the video.** Decided: it comes back **on mouse movement or a key
press**. The prototype does not do this — its code was checked — so it is a requirement of our own.

**3. Settings → Appearance with the prototype's own options**, and in general **the same options and
fields as the prototype on every screen**. The prototype has eleven rows where this application has
two. Three of them touch gates that would have to be declared again: a custom accent against
`ContrastTokenTests`, corner rounding against `ScalarTokenTests`, and density and cover size against
`ViewOverflowTests`.

**4. The two subtitle colour fields are hexadecimal text boxes** and should be a picker. The
prototype's pattern is in its accent row: six 28 px swatches, a separator, and the value in monospace.

**5. «Sections cut off by the design's width»** — the owner saw it «for example in the view».
It still has to be located by measurement; `ViewOverflowTests` measures at 900 px with no data
context and has not caught it, so it is probably a surface with real data in it.

## How work is done here

1. The affected suites locally, commit, push to the branch, **CI green**, and only then the
   fast-forward to `main`.
2. **The whole accessibility suite after touching any view**: `TextScalingTests` caught a fixed width
   in CI that had not been run here.
3. `eng/coverage-debt.txt` is copied from a CI run's `coverage-debt` artifact, never generated here.
   The ratchet is at **214** and only goes down.
4. A new control arrives with its walk scene in the same commit; that gate is at zero and does not
   rise.

## State at the close of 2026-08-25 (afternoon) — the prototype, read at the right size

**The first thing fixed was not in the code.** A capture was called "dark" three times, and three
times the file was right: a 1500 × 1000 PNG *reads* dark, and the same image at 750 × 500 reads as it
is. Measured over the library in light theme — `#FBFCFE` on the canvas, `#E9EEF4` on the rail, 100 %
opaque — and confirmed by halving it. Colour is decided by measuring, or by looking at half size;
never at full size. It is in
[the evidence](evidence/stable/audit-prototype-fidelity-round-three.md), and an alarm it raised —
"the covers are lighter at the bottom" — turned out to be a badly chosen measuring point.

**Nor was the second:** `docs/assets/review.png`, in a public repository, printed the profile path of
whoever took it. The tray writes the folder under every file. All five are retaken with the library
in a neutral folder, without an alpha channel, against the application as it stands.

What changed in the application, by surface:

- **Duplicates.** The chosen copy is marked on its whole row — radio, accent border and wash — the
  group's heading stops being blue, and the size column reaches the bytes: it rounded to "0 MB" on
  the very screen where somebody decides which copy to keep.
- **Links** move from the accent to its ink, which is what the prototype uses: 9.03:1 instead of
  5.62:1 in light and 11.36:1 instead of 8.29:1 in dark, with the new pair measured in
  `ContrastTokenTests`.
- **Series card.** Every episode still was coloured from the hash of its own name; it is the show's
  hue walked 7° per episode now, which is `art(show + episode × 7)`. And the next-episode panel
  limits its column rather than its border: with a fixed width, at 200 % text scaling, "Continue"
  fell outside the window.
- **Review tray.** It says **which title it is talking about**. The provider already answered with
  the name and the year and the whole chain threw them away; it carries them to the card now
  (migration 0019, one column).
- **Player.** A failure no longer erases itself: LibVLC reported the stop of the media it had just
  torn down, and that state replaced the failure, so the recovery vanished from the screen while
  somebody was reading it. It appeared as a flake before it appeared as a defect.
- **Home and library.** The kind chip drops its word in the rails and keeps it in the grid, "Add
  media" gets its plus back, and the player's header gets its second line back for a film.

**What is still different, and why:** besides the six from the previous round, the unavailable badge
is amber in all seven places it is mounted — the prototype has two shapes and the gate that forbids a
second predates this — the transport carries a stop button the prototype does not have, the metadata
editor lives inside the card rather than on a page of its own, and Settings does not offer the
prototype's nine appearance preferences: the palette is canonical and its pairs are measured.

**What to look at first next session:**

1. **`eng/coverage-debt.txt` is refreshed from the artifact of the last green run.** The ratchet
   stays at 215 and only goes down.
2. **The whole accessibility suite after touching any view.** `TextScalingTests` caught the fixed
   width of the next-episode panel in CI, and it had not been run here after the change.

## State at the close of 2026-08-25 — the application looks like the prototype, view by view

**The comparison was made against the sixteen verified captures rather than from memory**
([evidence](evidence/stable/audit-prototype-fidelity-round-three.md)). Seventeen differences closed
and **five that stay, each with its measurement written down**.

What changed, by surface:

- **Series card.** The data line, the series' own bar with «10/16 watched», the next-episode panel
  with its button — the card's only accented action — seasons as pills rather than a drop-down, and
  every episode as a card with a still, a name and «48 min · Watched». The name and the running time
  **were not on screen at all**: the episode projection did not read them.
- **Both cards.** They scroll as one page, the way back is a link, the personal marks leave the
  banner for «Other actions», and the three title tools move into the banner's action row
  (`TitleActionsView`, one view mounted by both).
- **Review tray.** Every card says **which file it is about** — the candidate projection carries the
  path now — with a cover, the kind, the confidence and the signals, and it holds **its three
  decisions**. It stops being a list with a selection: its rows were command controls and the walk
  had nowhere to click «beside».
- **Duplicates.** The prototype's eight-column table, with the radio that decides which copy plays,
  read in one query.
- **Player.** The header says what is playing — the title travels with the request, from the card
  that pressed — the transport is one row again in the prototype's order, and the shortcuts are
  written under it.
- **Palette.** `AccentInkBrush` in all four modes, and the gradients' last stop corrected: it was
  written `#30` meaning «30 %», which is 19 %.

**What stayed different, and why** (all five are in the evidence): the poster initials, the filter
radio dot — in both high contrasts the fill distinguishes nothing and the glyph is the whole signal —
the player's panel column always open — closing it would put controls the walk presses out of reach,
and that coverage is at zero pending — the tray's fourth button, and the per-episode editor this
application does not have because its metadata is keyed by title.

**What to look at first next session:**

1. **`eng/coverage-debt.txt` is waiting to be refreshed from a CI artefact.** Several files improved
   (`CatalogQueries` reached the bar; `GetHome`, `HomeReadModel` and `EpisodeSequenceRepository` rose)
   and **four new views enter the list at the 100/50 every view file measures**. The ratchet is 215
   and only goes down: two files already left the list in this batch — `LifecycleSettingsViewModel`
   and `RootRemapRowViewModel`, both at 100/100 — and how many more have to be paid is a question for
   the artefact.
2. **CI verifies what this machine does not.** Two races were found there and not here: the scrubber
   was pressed with the session playing — and since the transport observes the engine's position,
   that moves the walk's own probe — and the hero's Details was looked for without a layout pass.
3. **The README captures are re-taken** against today's application, in English, at 1600 × 1000.

## State at close (2026-08-24) — F11 closed: the parity plan is complete

**All eleven phases of the master plan are done.** PRD-006 moves to `VERIFIED` with its
[parity matrix](evidence/stable/PRD006-parity-matrix.md): 21 captures of the **real** application —
built in Release, started as a process, over a seeded 21-item library, navigated by UIAutomation —
beside the prototype's 16, across the four dictionaries.

**And the matrix did what captures are for: catching what no gate looks at.** Three defects, each
with its archived red and its fix:

1. **Home started empty.** The route `NavigationService` is born on never goes through `Navigate`,
   so `Navigated` never fires for the first screen, and every surface feed hung off that event. The
   shell now replays its initial route through the same path. The house defect's fourteenth shape,
   in the minute every user sees.
2. **A card's title took two lines** and pushed its year below its neighbour's. One line with an
   ellipsis (the owner's decision, looking at the application).
3. **A `ToggleButton` was not a pill**: the shape was declared for `Button` alone and that selector
   does not reach it, so "Favorito" and "Ver más tarde" wore the base theme's short box beside the
   pills in their row. `ControlStateTests` asserts it by comparing the two geometries.

**Fidelity phase B, done**: eighteen decorative borders dropped from `ShellBorderBrush` to
`ShellHairlineBrush` (the prototype's hairline), the duplicates card gained its card ground, and two
keep the strong border by the prototype's own arithmetic — the library's dashed empty state and the
player's five overlays. The accent halo was already at 0.156; the previous note called it pending
and was wrong.

**Final counts**: 53 views, **576** string keys per language (the plan estimated 517), 48 rows in
`LeadingActionTests`, 0 walk scenes outstanding, coverage debt 215.

**What remains**: read CI for `3d4fd49` and fast-forward `main` to the last green. Of the open
scope, the usual: PRD-003 (ARM64, blocked on hardware), REL-001/REL-004 and PLY-004 (5.1/7.1,
blocked).

## State on opening (2026-08-23)

**The brief grew: it is now parity with the prototype across the whole application**, not a few
views. The Spanish note is the one kept current — read [NEXT-SESSION.es.md](NEXT-SESSION.es.md) for
the detail. The short version:

- **Read `design/README.md` in full before writing a line.** The `.dc.html` files are design
  references, not code to copy; the specification is **§4 of `Propuesta de diseño`**, 48 rows view by
  view. Pills are `CornerRadius=18`, half the control height. The hand-drawn title bar **is not
  carried over** — the package says the application uses the system chrome.
- **Seven commits this session**, all green locally: the settings row-card, the review candidate
  card, the player's switchable column, a 1.10:1 contrast defect fixed, poster artwork, and every
  button a pill.
- **The finding that unlocked the resemblance**: the prototype has no artwork either. Every cover in
  it is four CSS gradients over one hue. The reason covers were out of 0.2.0 — no artwork, no TMDB
  token — is still true and does not apply. **Before calling part of the prototype impossible, look
  at how it does it.**
- **Decided, not to be re-litigated**: the two high contrasts do not become selectable theme options,
  because Windows owns that setting; generated artwork ships even though §4 said initials, because
  the initials stay on top of it; `UpdateRejectionDetail` lands with `UpdateView`.
- **The session's tools live outside the tree**, in
  `%USERPROFILE%\.claude\projects\D--Proyectos-ap-reelume	ools\`: a DPI-aware screenshot script
  and a one-class `preview` project that mounts any view with the real theme.
- **Left, by how much resemblance it moves**: Library's header and pill filters, Home's full-bleed
  hero, the two detail screens, the accent tint, Settings' side index and the metadata tabs, and
  then `UpdateView` and `PlayerView`.

### What the earlier tranches left, and still holds

- **`RootOnboardingView` closed tranche 7.** Three buttons set a kind no view painted; a label written
  in both languages that only the screen reader heard; a refusal, a folder removal and a request for
  permission all wearing one brush; and the fourth form SURFACES lists — no roots at all — with
  nothing to paint, which is how the screen starts.
- **A control could be seen and could not be pressed.** Growing that view by 25 px put "Review
  versions" at y=939 with a height of 36 inside a viewer whose viewport ended at 952: thirteen pixels
  in, its middle out, so the click reached the shell behind. The library stack gained a bottom margin.
- **And the harness was blind, not wrong.** `Fits` asked the window alone, and a scroller clips its
  content, so `Reveal` scrolled nothing and eight presses went to whatever the clip left behind. It
  now asks every viewer between the control and the window, which hardens the gate rather than
  loosening it. Three consecutive full passes, 135/135, ledger at 0 pending.
- **`UpdateView` closed untouched, measured**: fourteen states, seven rejections and the confirmation
  notice already carry the four grammars, and the ten-value rejection map closes with no hole.
- **The failure screen offered another version to somebody who had one file**, because the recovery
  was decided by failure code alone. Fixed. The button §4 asks for is deliberately not made, and the
  measurement is written down in the evidence.

### What the previous note said (2026-08-20, end of the afternoon session)

**`main` and the branch are level, CI green, working tree clean, nothing in flight.** Phase 6 has
started: **tranche 1 (Shell) is done** and **tranche 2 (Home) is measured** without writing code.

**Both CI reds this batch were the coverage gate, and they taught different things:** a real
improvement nobody declared (`RouteStateConverter.cs` at 100/85 — expect a second CI round per
tranche, it is the price of CI owning the floors), and **a floor that rose without being an
improvement** (`PlaybackProgressTracker.cs`, 83 in three runs and 85 in a fourth with one line of a
`.txt` as the only change between them). **What tells an improvement from a dance is the tree diff
between the two runs**, and raising a dancing floor is worse than leaving it low.

**And a loose end from tranche 1, closed:** `StartupView` was written off as needing nothing without
checking what §4 asks of it — that its background match the MSIX splash so the seam does not show.
They match, both `#111827`, but **nothing was watching**, so it was one edit from becoming a flash on
every launch. **It can only match one theme, and that is a decision**: a manifest colour is static,
painted before any of our code runs.

**STEP 6 IS ON PHASE 6 OF 6, AND THAT PHASE IS ALMOST ENTIRELY UNDONE.** What closed on 2026-08-20
were **phases 1 to 5 of `design/PROMPT.md`** plus phase 6's prerequisites — and it was declared "step 6
closed", which was **false**. Measured against the package that same day:

| What the package asks for | Proposed | Today | Missing |
|---|---|---|---|
| Controls | 202 | **133** | **69** |
| New strings | +47 | **0** | **47** |
| Animations | 4 | **0** | **4** |

**The error was not in the execution but in the checking: a step was called done against this
document's own wording rather than against the document that defines it**, `design/PROMPT.md`, whose
point 6 reads "the rest of the views, one change per view, following §4 of `Propuesta de diseño`".

**What was done is not wasted: it is §4's scaffolding.** The three scales, the leading action across
all 48 views and the overflow gate are exactly what §4 spends.

### Phase 6, area by area, in §4's order

**The order is §4's own, which matches `SURFACES.en.md`.** Each row is one tranche; **the unit of a
commit is the view**, except where §4 groups several under one change.

| # | Area | What §4 asks for that carries the most work |
|---|---|---|
| ~~1~~ | ~~**Shell** (2)~~ **DONE 2026-08-20** | [Its evidence](evidence/stable/audit-shell-navigation-bar.md). The 248px navigation and the glyph **were already there**; what was added is the 3px bar — which **exists or does not**, rather than dimming — and `TitleActionsSurface` became a `WrapPanel`. |
| 2 | **Home** (5) | One-column grid at `SpaceLarge`; 3px progress at the foot of each cover; 2:3 card with **initials when there is no artwork, never a hole**; three states for the recommendations rail (empty, switched off, with content) — **not the same thing**. |
| 3 | **Library and details** (5) | **Fluid** grid with a 180px minimum; filters row to `WrapPanel`; search gains a clear button; **"searching, no results", which does not exist today**; `UnavailableBadge` goes from error to **warning**. |
| 4 | **Player** (16) | Own surface `#0B0D10` and a fixed 320px column; failure moves to `DangerSurfaceBrush` with a glyph; `VideoStatusOverlay` **split into two grammars** (data vs warning); the three overlays with **explicit alignment and `MaxWidth 420`**; the four lists at 36px rows with no horizontal scroll ever. |
| 5 | **Settings** (7) | `AppearanceSettingsView` from 3 theme buttons to **5**, and its horizontal `StackPanel` **must** become a `WrapPanel`: five do not fit in 620px and **the split is not fixed by hand**, because Spanish wraps 4+1 and English falls elsewhere. `PrivacySettingsView` must **tell absent from disabled**. |
| 6 | **Review, Metadata, Catalog** (7) | An empty inbox is **the desirable state**: `PositiveSurfaceBrush` with a glyph, not a sad void. |
| 7 | **Backup, Onboarding, Recovery, Credits** (5) | `RestoreWizardView`: only the missing root gains an editable field; **the duplicate always-enabled "Restore" is removed**. `DatabaseRecoveryView` gains no route from the shell. |
| 8 | **`UpdateView` and `PlayerView`** | Layout is done; **the grammar of their messages is not**: 23 messages in four grammars, and 6 failure reasons with actions **conditioned by reason**. `PlayerRecoveryChooseAnotherVersion` **goes from `TextBlock` to `Button`** — the package's only type change. |
| 9 | **The four animations** | `apr-in`, `apr-shim`, `apr-tip`, `apr-pulse`, plus the handle transition. The conduit exists; **reduced motion takes them to 0 ms rather than shortening them**. There is not one `<Animation>` in the tree today. |

**Three rules that hold for all nine, none optional:**

1. **A new control arrives with its accessible-name test and its walk line IN THE SAME CHANGE.**
2. **A new string goes in both languages or not at all.** There are **47**, in `design/Cadenas nuevas`.
3. **No existing button label is rewritten.** `Content` and `AutomationProperties.Name` point at the
   same key, so rewriting a label **renames the control**. The package declares **0 renames**.

**Two decisions phase 6 needed, taken on 2026-08-20:**

1. **0.2.0 is NOT cut until §4 is finished.** The ten-step order is 6 → 7 → 8, and cutting early would
   publish half the screens with the new grammar and half without, which is worse than either. The one
   exception stays: **a finding from the owner's physical walk goes in whenever it arrives.**
2. **Inside phase 6 the ratchet's unit is THE TRANCHE, not the phase.** With **69 controls** across
   eight tranches, treating the whole phase as one unit would leave the net broken for the entire
   redesign — and **the walk is the redesign's net**. So **each tranche closes with
   `eng/walk-pending.txt` empty**.

**And where §4 contradicts the tree, the tree wins**, with the discrepancy noted: it already happened
with `PlayerView`'s "seven failure reasons", which are **six**.

### The order, which was already written and is not up for re-deliberation

**Next is phase 6 of step 6: §4, one view per commit.** `design/PROMPT.md` point 6 says so and the
ten-step table repeats it: step 7 (the physical walk) and step 8 (cutting 0.2.0) come **after**.

**The order of the views inside §4 is that of its own areas**, which is also `SURFACES.en.md`'s.

**Each view carries three things that are not optional**, which is why a view is a commit: its new
controls **with their accessible-name test and their walk line in the same change**, its new strings
**in both languages**, and its conditional states painted.

**If the owner brings findings from the physical walk before §4 finishes**, those go **first**, each as
its own scene with its own measurement.

### And when §4 is finished and the physical walk passes, step 8 opens by itself

```bash
pwsh ./eng/prepare-release.ps1
```

**That is the checklist and there is no need to write another.** It answers whether the tree could be
published and builds everything a release carries; it **creates no tag, publishes nothing, pushes
nothing and changes no setting**, so running it is free and its report says what is missing.
`-SkipBuild` reuses a freshly built artifact.

**The report was already run on 2026-08-20**, so the next session does not discover it: the repository
is public, 688 files are identical across two clean builds, the winget manifest is ready, and ARM64 is
built but not certified. **Two blockers, both expected and neither a defect**: an uncommitted working
tree (that session's own work) and an unsigned `SHA256SUMS.txt`, which is **the owner's** to sign
(step 9). Once the physical walk passes, **the cut has no known technical obstacle**.

**The version is still 0.1.0.** Raising it to 0.2.0 means **two places**, and the script checks they
agree: `Directory.Build.props` line 24 and
`src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest` line 29 (four components there).

**⚠ TRAP MEASURED WHILE RUNNING IT: `prepare-release.ps1` reads the LOCAL `main`, not `origin/main`.**
It reported `main is 9 commit(s) behind…` with `origin/main` perfectly up to date. Run
`git fetch origin main:main` first. **The script was not changed**: reading the remote without a fetch
guarantees no more, and adding network to a release script on the eve of a cut is risk for
convenience.

What the cut has decided in advance: **`A11Y-002` goes to `BLOCKED`** with its blocker named in
`eng/generate-verification-manifest.ps1` and in `release-readiness.md`; the manifest is **regenerated
from the freshly built package** and evidence is added to `FEATURES.md` per the split below, **not one
link more**; and `release-readiness.md` is squared against the manifest **in both languages**.

**Per-step ceilings are in place, and not inside the scripts.** The house rule asks for a ceiling on
every child process of `eng/`, and bounding `dotnet test` from PowerShell means `Start-Process` and
redirected output — changing how CI captures its log to gain nothing. The ceiling goes where the tool
already knows how: **`timeout-minutes` per step in `ci.yml`** — 70 for verification, 35 for
accessibility, 15 for recovery, 15 for the walk, between 1.5x and 5x what they measured on 2026-08-20.
The job's 90 could only say the whole job died; these say **which** step hung.

**No product findings are open.** Both the queue carried were closed on 2026-08-20: "start over" was
already measured and correct, and progress-per-file is a real defect **localised to
`CompositionRoot.cs:951` and `:964`** that is deferred past 0.2.0 because fixing it is a data
migration.

**And a warning about reading CI's health**, which cost a wrong diagnosis this session: to know how
long a step has been running you must **measure the current time**, not subtract from an assumed one.
A run that looked like it had spent 21 minutes in the walk gate had spent less than one; the whole run
took **61 minutes** and that step **4:16**, both healthy.

**And the overflow net is no longer written view by view: there is a gate.**
`ViewOverflowTests` mounts **all 48 views** without a data context — every branch visible at once,
which is the upper bound — in a 900-wide window and asserts no control ends outside it. Proved by
failing at 300: it names nine views with their control and coordinate.
[Its evidence](evidence/stable/audit-view-overflow-gate.md).

**Its limitation is stated and must be respected:** a view mounted alone gets the whole 900, and
nested in the shell it gets less. It catches a view too wide **on its own**; one that is only too wide
once nested is still the walk's to catch. **Silence from that gate is not a certificate.**

**And `primary-action` is decided for all 48 views**, with
[its evidence](evidence/stable/audit-leading-actions.md): **17 lead** (the previous 3 plus 14 new) and
**16 deliberately do not**, for six distinct reasons. The table lives in `LeadingActionTests` and **a
new view fails until somebody decides**, which is what keeps it from ageing. Proved by failing in three
directions: losing the action, gaining a second, and being in the tree without being in the table.

**The reason most worth remembering**, because it is a matter of principle rather than layout: on the
two screens that ask permission — `LifecycleSettingsView` and `PrivacySettingsView` — **the affirmative
is not accented**, because highlighting the yes of a consent is a dark pattern and this application
exists for the opposite.

**With that, the scaffolding and the first five phases are done.** What follows is phase 6: §4, one
view per commit.

**The tokens carry no debt any more.** `NotSpentYet` is **empty**, and **all three scales** — type,
spacing and corners — have a gate that requires the `.axaml` **not to write the number**. A new view
that writes a size, a spacing or a corner by hand fails `ScalarTokenTests`. **That is what makes "one
view per commit" genuinely cost layout only**: what is left per view is `primary-action` where there
is one, the overflow net, and nothing else.

**The corner scale was decided here and was not in the plan** (`docs/evidence/stable/audit-corner-radius-scale.md`).
It was done in one sweep for the argument that won in spacing — it is a mapping, not a per-screen
decision, and without a gate the remaining views can reintroduce literals — and its lesson is the
opposite of the expected one: **a criterion that has just been proved is when it is easiest to
misapply.** The three large radii were the three cards and a `CornerRadiusLarge` looked due; measuring
the other side said **four of the seven card surfaces already carried 8**. Not a step the tree was
asking for, a split nobody decided. **The question is not "does the step make sense?" but "does
anything already in the tree contradict it?"**

**Two warnings this batch bought dearly, and they apply to every view that is left.**

**The first, from the scalars phase: when two measurements disagree, diff the two commands.** A count
of my own said 163 where the note said 183, and the first thing I did was build an explanation of why
the note was wrong — one that even agreed with a third number. The note was right; my pattern carried
a `\b` that never saw `RowSpacing` or `ColumnSpacing`. **A hypothesis that fits the numbers is not a
measurement**, and here it would have left 23 sites untokenised with the phase declared finished.

**The second, from `PlayerView`:** the overflow net gets written **even when the change looks purely
cosmetic**. There, the only test that found anything was the one that **passed before the change** —
it was the net, not the red: it measured the transport row ending **74 pixels outside** a 900-wide
window, which is the minimum the application allows. Seventh time a horizontal `StackPanel` with
translated labels has drawn a control off the screen. **Measure against the real minimum width, not a
comfortable one.**

**Three views in a row have cost layout only** — `MiniPlayerWindow`, `UpdateView` and `PlayerView` —
because their controls were already in the walk. That is the norm from here on: the walk reached zero
before the interface changed, which was the point of that order.

**What this batch cost, and what the remaining views inherit:**

1. **A secondary window is not like a view.** `PlayerWindowCoordinator.Apply` assigned `window.Content`,
   which **replaces the whole AXAML tree**: the mini window threw away everything it declared for
   itself the moment a session arrived. `Host()` and `MiniPlayerSurface` had been there from the start
   and **only one test ever called them** — the house defect, form eleven.
2. **`WalkLedger.Record` requires a `UserControl` ancestor**, and the gate's inventory keys on the
   `.axaml` file name. A control declared inside a `Window` can never match the two halves.
3. **The walk could not leave the shell's window**, and nobody had needed it to. The harness gained
   `Reachable`, `SecondaryWindows` and `RootOf`, and every click function now aims at the control's
   **own** window.
4. **Long labels do not overflow; they leave nowhere for the beside click.**
5. **A test that compares the VALUE cannot tell a literal from a token** while the two agree — and they
   agree exactly when the tokenisation would be correct, so the false green is the ordinary case. What
   gets asserted is that the `.axaml` **does not write the number**.

**Four CI reds, and all four were gates doing their job:**

- **The coverage ratchet found a branch nothing walked.** The mini player's first attempt routed
  through an interface and **left the old `Apply` branch alive**. The fix was **deleting** it, not
  covering it: a test written to reach dead code turns the number green and leaves the defect in.
- **And deleting it dropped the file from 100/92 to 100/91**, because removing covered branches raises
  the weight of the ones never covered. Two had been there all along, both real guarantees with no
  test. Covered, the file measures 100/100 and **leaves** the debt list. **A floor that drops is a
  drop**: reaching for a process explanation is the comfortable way not to look at the code.
- **A net calibrated on one machine accuses another.** The copy-cancel scene compared its two presses
  against a duration measured here. What the clock inferred, the surface says: it now records the
  statuses it passes through and asserts `BackupStatusDone` is not among them.
- **And the ratchet asked for three improvements to be declared**, which is the half of its job that
  catches things getting better without saying so.

**And the usual three still hold:** a view change affects **four** suites (`UiTests`,
`AccessibilityTests`, `IntegrationTests`, `DocumentationTests`); the coverage ratchet **also fails when
something improves**; and `verify.ps1` **aborts on the first failure**, so one red hides the ones after
it.

## The queue decided on 2026-08-16 (not up for re-deliberation)

**The destination is zero.** This application ships free and **nobody is going to test it by hand**:
whatever the suite does not cover, nothing covers. The ratchet in `eng/check-walk-coverage.ps1` goes
to **0 pending** — and **since 2026-08-18 it is there**: **128 of 128** controls pressed by mouse, `eng/walk-pending.txt` empty and the ratchet at 0, which does not go up again. What remains is the code coverage
gate goes to watching the whole tree. Everything below is **decided**; what remains is carrying it out,
measuring before correcting.

### The whole plan to 0.2.0, fixed on 2026-08-17

**Ten steps, and the order is a decision rather than a list.** The autonomous walk **is the redesign's
net**, so it reaches zero **before** the interface changes; the coverage gate goes to watching the
whole tree for the same reason. What belongs to the owner goes in one block at the end, with a single
exception: the physical walk goes **before** the cut, because a finding there means doing the cut
again from scratch.

| # | Step | Who | Leaves the ratchet at |
|---|---|---|---|
| ~~1~~ | ~~The loose session cannot be seen~~ **done on 2026-08-17, 6 → 3** | agent | 3 |
| ~~2~~ | ~~The last three of batch 1~~ **done on 2026-08-18, 3 → 0** | agent | **0** |
| ~~3~~ | ~~The subtitle measurement~~ **done on 2026-08-18** | agent | 0 |
| ~~4~~ | ~~Coverage over all of `src/`~~ **done 2026-08-18 as a ratchet: 219, and it only drops; corrected the same day so CI measures the floor** | agent | 0 |
| ~~5~~ | ~~`ARQ-004`~~ **done 2026-08-18: the command bound, the notification, and the gate on the seven** | agent | 0 |
| 6 | **The redesign**, from Claude Design's material — **phase 2, the scalar gate, `primary-action`, the type scale, `MiniPlayerWindow` and `UpdateView` all done**; **`PlayerView`, the spacing scalars phase and the remaining views** are left, all decided | agent | 0, under the rule below |
| 7 | The ten-minute physical walk | **owner** | — |
| 8 | Cut 0.2.0, up to the moment of signing | agent | — |
| 9 | Sign and publish | **owner** | — |
| 10 | `REL-004` and the quarterly key restore | **owner** | — |

#### ~~1. The loose session cannot be seen~~ — done on 2026-08-17

**Done exactly as decided** — [the evidence](evidence/stable/audit-walk-loose-session.md).
`OpenLooseFile` validates and describes, `ShellSurfaces.OpenLoosePlayer` is the one path, and both
callers go through it. Three things were not foreseen and were settled in the same change:
**`OpenLooseFile` became static and left the container** because `CA1822` said so once it held no
state; **`ResumeWiringTests` broke by reading the composition as text** — fourth time — and was
narrowed to the declaration of `OpenPlayerAsync`; and **`RepositoryPrivacyTests` flagged `design/`**
as an unknown directory at the root, which is the check working. The local trailer is **no longer
blocked**, but it still needs seeding of its own, so it moves to step 2.

#### 2. The last three — how they get seeded and in what order, decided

**For the first time in the whole queue, no pending control is blocked by a defect.** They are **two
scenes, not three**, because two of the three live on the same card:

**(a) The film card: Resume and the local trailer.** Seeding: the film in its folder, a `WatchState`
with a position **above the resume floor — 30 s — and below the end**, and a sibling file
**`<the-film's-name>-trailer.mp4`**, which is what `TrailerDiscoveryPolicy` looks for
(`Suffix = "-trailer"`, or inside a `Trailers` folder). **No version group is needed**: `HasTrailer`
comes from discovery by name, not from the catalogue — the old queue note said otherwise and was
wrong.

- **Resume** — probe: the session opens **at the stored point**, read from the engine and waited for
  until the demuxer has applied the start position.
- **The trailer** — probe: the session plays **the trailer file**, and that can now be asserted for
  real because a loose session reaches the screen: `Player.LooseFile.IsLooseSession` and the media
  path.
- **And the first open finding is measured here:** with stored progress, **"Play from the start" has
  to leave the playhead at 0**. Now that the requested position wins it should be fixed; what is
  missing is the number.

**(b) The series card: the episode row.** Seeding: a show with a season and episodes — the harness
already has `SeedSeriesAsync` — reaching the card from the library and pressing the row. Probe: the
session opens **that** episode, by its path.

**A measurable prediction, and it gets measured BEFORE pressing:** the action row of
`MovieDetailsView` is a `StackPanel Orientation="Horizontal"` with **a free-width `TextBlock` between
buttons** — the resume position text — and five controls. That is **exactly the shape that has put a
control outside the window six times**, and this scene is the first to make Resume and the trailer
visible at once, so the row will be the longest it has ever been. If it overflows, it becomes a
`WrapPanel` like the other six. Measure the bounds against the window before attempting the click,
not after the red.

#### The two open findings, and what happens to each

1. **"Play from the start"** — measured in scene (a), as above.
2. **Progress is stored per file, not per version group.**
   `ContentKey.ForTitle(new TitleId(mediaFileId.Value))` ties progress to the file, so after switching
   version there are **two `WatchState` rows** and returning to the previous one would not resume where
   it was left. **Decided: measure first and correct only if the measurement shows a real loss** — the
   version scene already exists and asserting both keys after switching back is enough. Moving the key
   to the group is a model change with a migration, and the position travels in the request today, so
   the symptom may not exist.

#### 5. `ARQ-004` — decided, and the old note was incomplete

Measured on 2026-08-18: there are **eight** files with an empty `CanExecuteChanged`, not nine classes,
and **only one carries real risk**. An empty `CanExecuteChanged` only matters when `CanExecute` looks
at **state that changes**; if it looks at the parameter, every query with the same parameter always
answers the same and there is nothing to notify.

| File | `CanExecute` | Risk |
|---|---|---|
| `LibraryViewModel` | `Surface != LibrarySurface.Browse` | **yes, changing state** |
| `RootOnboardingViewModel`, `ShortcutSettingsViewModel`, `LifecycleSettingsViewModel`, `WindowsTrayService` | `true` | no |
| `DatabaseRecoveryViewModel`, `AppearanceSettingsViewModel` (x2), `ShellViewModel` | parameter only | no |

**Done on 2026-08-18 —
[the evidence](evidence/stable/audit-arq004-command-notification.md) — and the note was true for a
false reason.** It said Back does not bite today "because the view becomes visible and the button
asks again". Measured: **no AXAML bound `BackCommand`**. Both Back buttons called `BackToLibrary()`
through the code-behind, so the predicate was never evaluated — a public command with a predicate
that no view consumed, the house defect wearing a command's face. And the predicted red could not
exist for a second reason: each button lives **inside the grid of its own surface**, so while it is
visible the predicate is true by construction.

What was done, and in which order:

1. **Bind the command** (`Command="{Binding BackCommand}"` on both buttons, `OnBackClick` gone) **and
   measure**. The red appeared at once, in the library's walk scene: `Volver a la biblioteca is on
   screen but cannot be pressed: visible=True, enabled=False`. Both detail branches sit in the visual
   tree at once, so the button asks on attach while `Surface` is still `Browse`, and the empty event
   throws the subscription away.
2. **The notification**, with a real event on the private `RelayCommand` and the raise in the one
   place `Surface` is assigned. Green under the same probe, plus a new unit test where **the count is
   the assertion**: the predicate alone passes whoever was or was not told.
3. **The gate**, `CommandNotificationTests`, holding the closed list of the **seven** that remain and
   **each one's exact predicate**: it watches the event-predicate pair rather than the event alone.
   It was tested by failing in both directions — an undeclared eighth, and a predicate that changed
   shape — and it carries its own anti-blindness floor.

#### What is decided about the design package, for step 6

- ~~**The ten `SURFACES.es.md` / `.en.md` changes**~~ **done on 2026-08-18**, each measured against the
  tree before it was written — and of the ten, **three were not true**:
  - **`MiniPlayerWindow` was already there** (Player (16)); the note saying it was absent had been
    overtaken.
  - **"`BackupView` has history" does not appear** in `SURFACES`: it was an error in the package's own
    audit, which admits it in writing.
  - **"the seven failure reasons in `PlayerView`" are six.** `PlaybackFailureCode` has seven values,
    but the seventh — `UnsupportedCapability` — travels in `VideoOutputDecision` and surfaces through
    `VideoStatusOverlay`: the video **does play**, tone mapped. Painting it as a failure would say
    there is no picture when there is one.
  - Two more did not apply to the document (it names neither the tokens nor the Avalonia version), but
    their figures were verified and feed the new themes section: **58 declarations / 40 names** plus 3
    in `Brand.axaml`, and **8** focus selectors.
  - What was genuinely missing, measured: **23 lists with data and only 4 with an empty string**, and
    the library's empty state is painted by `ShellView`, so **searching with no results shows
    nothing**; high contrast is **one** dictionary and it sits over `Light`, so anyone on the Windows
    dark high-contrast theme gets the light one; and the tray icon is the **sixth** asset, in another
    project.

#### The two decisions step 6 still owed, taken on 2026-08-18

**1. High contrast is NOT chosen inside the application: it stays three pills.** The package replaces
the three theme buttons with four pills, and the fourth could only be high contrast —
`ThemePreference` holds exactly `System`, `Light` and `Dark`. **Rejected**, and not for the work:
Windows' high contrast is a **system accessibility** setting, and offering a copy inside the
application creates two sources of truth for one need. Someone with the system in high contrast and
the application on "Light" would have an application contradicting a declared need, which is worse
than offering nothing. Leaving the enum alone also means **no settings migration** and no orphaned
stored values.

What is genuinely missing **is the fourth dictionary**, and that does get done: today
`AppThemeVariants.HighContrast` is declared over `ThemeVariant.Light`, so anyone on the Windows
**dark** high-contrast theme gets the light one. `HighContrastLight` is added, the existing one is
renamed `HighContrastDark`, and it touches `AppThemeVariants` and `FluentThemeService` — **not the
enum**. If the redesign wants to show a fourth pill, it should be **a state and not an option**:
saying the system is in high contrast and that it is being honoured. That informs without duplicating
the setting.

**2. ~~`DebouncedFileWatcher` is fixed through its constructor~~ — done on 2026-08-18, exactly as
decided.** The buffer is now an optional constructor parameter defaulting to the product value; the
test asks for the smallest the platform honours, overflows for real and **asserts it**. Nothing
became `internal` and `WatchSignal` stays private.

**The recorded cause was one of three.** Measuring the two full runs turned up two more conditions
left to chance: the other half of the same handler — the error that **does** end the watching, which
ran by accident when a test directory vanished under a live watcher — and the coalescing switch,
whose pairs depended on what the system happened to deliver during the storm (13 of 16 branches on
one run, 11 of 16 on the other). Each now has its own test, and a fourth covers the debounce elapsing
**in the very instant** the change that cancels it arrives, with a clock whose wait ends successfully
when cancelled. [The evidence](evidence/stable/audit-watcher-overflow-determinism.md): from
**88.54/73.81 and 93.75/71.43** across two runs of the same binary to **100/95.83 three times in a
row**. The two missing branches are unreachable.

**And what was measured without expecting it: a sequential storm does not overflow 4 KiB.** Two
thousand files with hundred-character names, one after another, never overflowed once: the bottleneck
is not the watcher draining the buffer, it is creating the file, and those three tenths of a
millisecond are all the breathing room its thread needs. In parallel it overflows within the first
second. The first attempt came out red **through the new assertion rather than through a timeout**,
which is exactly what it is there for.
- **The updater's rejection count resolves to EIGHT**: `README.md` says 8 and `github.md` says 7, and
  the one that adds up to 23 messages is 8 (15 states + 8 rejections).
- **The 25 consequence strings are approved against the package's own rule** — "if the phrase helps
  decide or act, it is translated; if it explains why something is designed that way, it is an AXAML
  comment" — reviewed one by one as they are written. **They do not block step 6**; whatever fails
  that rule stays a comment.
- **The 35 installation assets stay blocked** on the brand's vector original, and are not improvised.

#### And a red CI brought in the middle, with a product defect inside it (2026-08-18)

The run for `ba1502e` failed **1 of 117** in the walk:
`ConfirmSwitchButton is on screen but cannot be pressed: visible=False, enabled=True` — the version
switch's question left the screen between the button being resolved and being pressed. Locally it is
117/117 over two passes, at two minutes a pass against the runner's 5 m 38 s, so the window is opened
by the slowness there.

**It was not the walk's.** The other version's row stayed pressable while its own switch was in
flight, the harness presses again after 300 ms of apparent silence, and **every switch flushes the
playhead before it decides**: a session that has just opened answers zero, zero is below the resume
floor, and the policy then stops asking, **opens the other version unasked and leaves the stored
position at zero**. So a double click was enough, no slow runner required.

Fixed where the second request came from — the row greys out while its switch is in flight, the
pattern the transport bar already had — and **the one link that was a deduction was measured** rather
than assumed: [the evidence](evidence/stable/audit-version-switch-reentry.md). The use case is left
alone.

#### Step 6's phase 2: the button done, and what the template owes (2026-08-18)

**The button is done** — 95 of them across the views, against 18 checkboxes and 15 text boxes, so it
is where this starts: its four colour states now come from the tokens in all four themes, with a 1 px
border, and in high contrast hovering and pressing **invert**.
[The evidence](evidence/stable/audit-redesign-phase2-button-states.md).

**What was measured, and what it decides about the rest:**

- **An application style does NOT reach the template elements of a `ControlTheme`.** Neither
  `Button /template/ ContentPresenter` nor the same with `#PART_ContentPresenter`. What does reach
  them is **the resource that template consumes**: `ButtonBackground`, `ButtonForeground`,
  `ButtonBorderBrush` and three states of each. They are pointed at our tokens with
  `<StaticResource x:Key="…" ResourceKey="…" />`, which Avalonia accepts as a dictionary entry, so
  **not one value is duplicated**. The other types go the same way, with their own resources.
- **`ControlTextActiveBrush` is a new token** (fourteen now), and it is needed: without it the high
  contrast inversion cannot be expressed without a per-theme rule.
- **A brush is read whole.** `ButtonBackgroundPointerOver` says `Black` and carries `Opacity 0.1`;
  reading only `.Color` produced "black text on a black fill", which is false.

**What the template owes, and this is not reopened:**

1. **The dotted disabled border** (a `Rectangle` with `StrokeDashArray`). Without it, **in high
   contrast disabled is today indistinguishable from rest** — the disabled fill *is* the surface, by
   design — and that is measured and asserted as such, not loosened away.
2. **The 2 px pressed border in high contrast**: the base template keeps one thickness for every
   state. Asserted at 1 px, which is what is true today, and **the thickness token arrives with the
   template that spends it**: it was written, lost its consumer when the approach changed, and was
   taken back out.
3. Then the other eight control types, starting with `CheckBox` (18) and `TextBox` (15).

And two things the measuring turned up: **`primary-action`** (in `ResumeHeroView`) is a class **no
style defines and no test looks for**, and **`navigation-destination`** is used, but as a **test
marker** rather than as a style.

#### The seven decisions step 6 had left, taken on 2026-08-18 (not reopened)

1. ~~**The dotted disabled border is drawn as an ADORNER, not by copying templates.**~~
   **Done on 2026-08-18 exactly as decided** — [the evidence](evidence/stable/audit-redesign-phase2b-disabled-outline.md).
   What follows is kept for the reasoning: An attached
   property in `Presentation/Theme` adds a `Rectangle` with `StrokeDashArray` to the adorner layer when
   a control is disabled. **Why not a `ControlTheme` per type:** the focus ring already proved the
   adorner layer reaches all ten types with **one** implementation — including the `ToggleSwitch`,
   which hangs it on the `Grid` in its template, and the `NumericUpDown`, which hangs it on its
   `TextBox`. Copying nine Fluent templates is nine surfaces that drift with every Avalonia update, for
   one dashed line. A new file, so **96/96 from its first commit**: the test forces `IsEnabled=false`
   and asserts the `StrokeDashArray` in the layer.
2. **The 2 px pressed border in high contrast will NOT be done, and this is a deliberate departure from
   the package.** Pressing in high contrast already inverts fill **and** text — 21:1 measured — so the
   thickness adds a third cue to a state that has two, and would need another adorner or a template of
   our own. Recorded as a decision, not as debt. If the owner's physical walk says it does not read, it
   reopens **with that measurement**.
3. **`primary-action` gets a primary-action style** rather than being deleted: it is "Continue" on the
   home surface, and the redesign wants hierarchy. It needs a new token, **`AccentTextBrush`** — ~~white
   in light, dark and high contrast light; **black** in high contrast dark, where the accent is cyan~~ —
   and `ContrastTokenTests` measures it against `AccentBrush` at the text bar of 4.5:1.
   **The token arrived on 2026-08-19 with the checkbox, which needed it sooner, and the struck-out
   half was false**: the **dark** theme's `AccentBrush` is `#62AEE8`, a pale blue, and white on it
   reads **2.40:1**. It is `#FFFFFF` in light and high contrast light, `#111827` in dark and
   `#000000` in high contrast dark — the colour follows the **accent's luminance**, never the theme's
   name. The gate measures it now, proved failing.
4. **`ToggleSwitch` keeps its focus selector and gets NO states.** Zero uses across the 48 views:
   giving states to a type nobody mounts is declaring without spending. The focus selector stays
   because it is already written and costs nothing.
5. **The order of the remaining eight types is the measured order of use**: `CheckBox` (18),
   `ListBoxItem` (17), `TextBox` (15), `ComboBox` (8), `Slider` (5), `NumericUpDown` (5),
   `ToggleButton` (2), `RadioButton` (1). Each through its **own theme resources**, as the button was.
6. **The scalars become a ratchet rather than a promise.** A new test walks the `.axaml` of `src/` and
   requires that **every declared scalar be consumed by at least one view**, with a named exception
   list — today `SpaceXSmall`, `SpaceSmall`, `SpaceMedium`, `SpaceLarge`, `SpaceXLarge`,
   `CornerRadiusSmall`, `CornerRadiusMedium` — **which can only shrink**, exactly like the orphan list
   in `ServiceConsumptionTests` and like `eng/coverage-debt.txt`.
7. ~~**Typography**~~ — **done on 2026-08-19**: [the evidence](evidence/stable/audit-type-scale.md). **Five tokens, not six**: `FontSizeMono` is not declared because nothing spends it and the scalars gate would refuse it — it arrives with the first path or hash that asks. The 17 went to `FontSizeBody` **by what the text is** (a wrapping paragraph), not by distance. `HomeLayoutTests`' baseline moved **one field**, toward consistency. **Typography: the tree's literal sizes map onto six tokens, and this is the mapping.**
   **Measured on 2026-08-19: there are THIRTEEN, not twelve — 52 uses across 30 files — and
   the one missing from the map below is the `17` in `ShellView.axaml:140`**, to be placed
   when this runs (by proximity it goes with the 18, onto `FontSizeSubtitle`, but that gets
   measured first).
   34 and 32 → `FontSizeDisplay` 32; 30, 28 and 26 → `FontSizeTitle` 28; 24, 22, 20 and 18 →
   `FontSizeSubtitle` 20; 16 and 14 → `FontSizeBody` 14; 12 → `FontSizeCaption` 12; and `FontSizeMono`
   13 for paths, hashes and codecs. Declared **in the first view that spends them**, not before.

#### ~~Phase 2b: the dotted disabled outline~~ — done on 2026-08-18

**Done exactly as decided** —
[the evidence](evidence/stable/audit-redesign-phase2b-disabled-outline.md). An attached property,
`DisabledOutline.IsShown` in `Presentation/Theme`, a `Rectangle` with `StrokeDashArray` in the adorner
layer, and **a `:disabled` selector says when**, over the same ten types the focus ring covers. The
new file measures **100 % of lines and 100 % of branches** — checked locally before pushing, which is
what saves CI's thirty-five minutes per attempt — and its 2,170 hits come not from its own tests but
from the UI suite's real views.

**What measuring added, and it is this batch's lesson: disabling is inherited, and an application
style reaches template elements too.** Eight types took one adorner and **two took two**: a `ComboBox`
and a `NumericUpDown` hold a `TextBox` whose own `IsEnabled` is still `true`, so two dashed rectangles
were drawn a few pixels apart. The right test is **not the local `IsEnabled` but
`TemplatedParent is null`**, and the two answers differ exactly where it matters: a control inside a
wholly disabled panel keeps its own `IsEnabled` at `true`, and since a panel is not one of the ten
types, keying off the local flag would leave that case with **no outline at all**. That case does not
exist today — measured: the tree's eleven bound `IsEnabled` are on nine `Button` and two `CheckBox`,
no container — but the rule is written for when it does.

**And `SURFACES` was corrected along the way**, its themes section still describing the tree from
before phase 1: it said 3 dictionaries (there are **4**), 58 declarations across 40 names (there are
**140** across **35**, plus 13 scalars outside them), 8 focus selectors (there are **10**), and that
there was no high contrast dark, which has been false for a day.

**What is left of phase 2, in order and not up for deliberation:** the eight types by measured use —
`CheckBox` (18), `ListBoxItem` (17), `TextBox` (15), `ComboBox` (8), `Slider` (5), `NumericUpDown`
(5), `ToggleButton` (2), `RadioButton` (1) — each through **its own theme resources**, as the button
was; the consumed-scalars gate with a list that only shrinks; `primary-action` with the
`AccentTextBrush` token; and then the typography and the views, one per commit.

#### The CI red `4b2c326` brought, and it was the harness (2026-08-19)

`RestartSwitchButton is on screen but cannot be pressed: visible=False, enabled=True` in the version
switch scene — [the evidence](evidence/stable/audit-walk-press-retry.md). **The commit did not cause
it**: it touches tokens and theme tests, and the scene passes six of six locally.

**The cause, read in `PressAsync`'s retry loop:** it repeats a press that showed no effect, and before
repeating it looks **only at whether the control is disabled**. Answering the version-switch question
**closes it** — which is right, and the scene asserts it — so the button leaves the tree the moment it
is pressed while its command stays enabled; on a loaded runner the effect takes longer, the loop goes
round again and presses a button that is no longer there.

**So the house rule gains an exception, which is the part to remember:** `visible=False` accuses the
product **unless pressing that control is precisely what takes it off the screen**. All three buttons
of that question are of that kind, as is any confirmation that closes what it confirms.

The decision moved out to `WalkPressPolicy`, with a test of its own — because the case **only appears
on a slow runner**, and a rule exercised by luck is a rule nobody checks — and proved failing against
the old rule. A control that is not on screen is **never pressed again**: the effect's own timeout
speaks, which is the true failure.

**Noted and untouched, because it is a different thing:** the row that opens the question **stays
pressable while the question is on screen** — `SwitchToVersionAsync` returns as soon as it calls
`Apply`, so the row's `finally` re-enables it with the dialogue open, and a second press flushes the
playhead, answers zero and takes the question with it. It deserves its own measurement.

#### The eight decisions that close step 6, taken on 2026-08-19 (not reopened)

With these, **nothing in step 6 is left to deliberate**: what follows is execution, measuring before
each correction.

1. ~~**The version-switch defect is fixed, and it goes FIRST**~~ — **done on 2026-08-19**, exactly as
   decided: [the evidence](evidence/stable/audit-version-switch-question-guard.md). It was done as
   written — required parameter, `&& !_question.IsVisible`, and the subscription — and the only
   thing not foreseen is **why the subscription is not optional**: refusing the question does **not**
   rebuild the surfaces, so it has to be **that very row** that becomes pressable again, and the walk
   scene, which presses the row three times, is what checks it. The file holds 100/100. The decision
   as it was taken:

   **The version-switch defect is fixed, and it goes FIRST**, before any new type, because it is a
   live product defect: the row that opens the question stays pressable while the question is on
   screen, and a second press flushes the playhead, answers zero and **takes the question and the
   progress with it**. The fix: `PlayerVersionRowViewModel` takes the `VersionSwitchViewModel` as a
   **required** parameter — an optional left null is the house defect's fourth form — its predicate
   adds `&& !question.IsVisible`, and it subscribes to the dialogue's `PropertyChanged` to ask again
   when `IsVisible` changes. **Rejected**: making the dialogue modal, a structural change to the
   surface for a defect in a predicate.
2. ~~**Phase 2f (`ComboBox`)**~~ — **done**, and the first measurement answered **yes**, with a trap inside it: the presenter takes the **thickness** by template binding and **not the colour**, because the control theme sets its brush per state. The colour goes through a resource redirect. **Phase 2f (`ComboBox`) follows the list row's pattern** for the drop-down's rows: a subtle accent
   fill **plus** a second cue, at the same thickness in every state. The **first measurement of the
   phase** is whether a `ComboBoxItem`'s content presenter takes the border by template binding as
   the `ListBoxItem` does; if it does not, the cue is another adorner. The closed frame and the arrow
   already pass and only move to tokens.
3. ~~**`Slider` (5), `ToggleButton` (2) and `RadioButton` (1) together in phase 2g**~~ — **done on 2026-08-19**: [the evidence](evidence/stable/audit-redesign-phase2g.md). What decided the design was **a table**: the accent measured against all thirteen tokens in all four themes. The line and text tokens — border, hairline, primary, secondary — **share the accent's luminance by construction** and none of them can sit beside it; the surface and fill tokens all work. **`Slider` (5), `ToggleButton` (2) and `RadioButton` (1) go together as one phase 2g.** Eight uses
   between them and the pattern is established; splitting them is two CI rounds for nothing.
4. ~~**The scalars gate**~~ — **done on 2026-08-19** (and its CI asked for **a floor to rise**: moving the guarantee onto `FluentThemeService` took it from 88/65 to **90/69**, and the ratchet fails when something improves without saying so): [the evidence](evidence/stable/audit-scalar-gate.md). As decided, with two things the decision did not have: the `MotionDuration*` pair was watched by **two** tests, not one, and their guarantee **moved** onto `FluentThemeService.MotionDuration` rather than being lost. Proved failing in **three** directions. **The scalars gate counts consumption in ANY `.axaml` under `src/`**, the token file included — a
   style that spends a scalar is real consumption — **plus a named list of the ones the base theme
   consumes**, which today is one: `TextControlPlaceholderOpacity`. Measured on 2026-08-19:
   `FocusStrokeThickness` (11), `CornerRadiusSmall` (2), `FocusInnerStrokeThickness` (1) and
   `ControlHeight` (1) are spent. **The exception list, which may only shrink, is five
   since 2026-08-19**: `SpaceXSmall`, `SpaceSmall`, `SpaceMedium`, `SpaceLarge` and `SpaceXLarge`.
   `CornerRadiusMedium` left it when `player-chrome` spent it. It empties itself as the views spend
   the rest.
5. **`MotionDurationStandardMilliseconds` and `MotionDurationReducedMilliseconds` are DELETED.** No
   AXAML reads them and `FluentThemeService` holds its own `TimeSpan.FromMilliseconds(160)`: they are
   **a parallel copy of a number**, which is exactly the defect that bit with the `<Color>` list
   nobody painted. If an AXAML animation needs them, they are declared then and the service reads
   from there.
6. **`SelectedStateGlyph` is DELETED**, and its assertion in `ContrastTokenTests` with it. Measured:
   `●` is **literal in six places** — one AXAML and five view models — and neither `○` nor `◐` has a
   resource, so the abstraction was half-built and unused. The glyph is a view model's datum, not the
   theme's.
7. ~~**`primary-action`**~~ — **done on 2026-08-19**: [the evidence](evidence/stable/audit-primary-action.md). And the mechanism is measured: the style goes on the `Button` and reaches **rest only**, because the control theme sets the presenter's fill per pseudo-class — the mechanism phase 2f met as a defect is the design here, which is why all five states are asserted. **`primary-action`**: at rest, `AccentBrush` fill, `AccentTextBrush` text and `AccentBrush` border;
   hovering and pressing **invert like everything else** (`ControlFillHoverBrush` /
   `ControlFillPressedBrush` with `ControlTextActiveBrush`). One grammar of states across the whole
   application, and the hierarchy comes from the resting state, which is when it is looked at.
8. **The views follow `PROMPT.md`'s order** — `MiniPlayerWindow`, `UpdateView`, `PlayerView`, then one
   view per commit — and **the five controls `MiniPlayerWindow` gains arrive with their walk scene in
   the same commit**. The ratchet does not cross a phase boundary carrying debt.

#### ~~`MiniPlayerWindow`: the five controls~~ — done on 2026-08-19

**Done**, with [its evidence](evidence/stable/audit-mini-player-chrome.md). All nine decisions held
except two details the measurement forced, written down here:

- **The five do not live in `MiniPlayerWindow.axaml`** but in `MiniPlayerChromeView`, a new
  `UserControl`, because `WalkLedger.Record` requires a `UserControl` ancestor and asserts without
  one. The data context is still `ShellViewModel` and **there is no new view model**, which is what
  decision 1 was protecting.
- **`TogglePlaybackCommand` does not belong in `CommandNotificationTests`**: that gate lists the files
  that **silence** `CanExecuteChanged`, and an `AsyncRelayCommand` does not. The guarantee decision 2
  was after lives in `UpdateState`'s list of commands that get `RaiseCanExecuteChanged`, where it
  became the sixth.

The original decision text is kept below because it explains **why** each binding is the one it is.
What follows, for the views that remain: `UpdateView`, `PlayerView`, then one per commit.

**What it was, before it was done.** Measured without writing code, as the `ComboBox` was, with the
nine decisions below taken. The window was ten lines: a `Panel Background="Black"` and **zero
controls**.

**The five, with the keys the package already fixed** and each one's exact binding:

| Key | What | Binding |
|---|---|---|
| `MiniPlayerPlayPause` | Pause / resume | `{Binding Player.Player.TogglePlaybackCommand}` ← **the only one to create** |
| `MiniPlayerSkipBack` | −10 s | `{Binding Player.Player.Transport.SkipBackwardCommand}` |
| `MiniPlayerSkipForward` | +10 s | `{Binding Player.Player.Transport.SkipForwardCommand}` |
| `MiniPlayerRestore` | Back to the big window | `{Binding ToggleMiniPlayerCommand}` |
| `MiniPlayerClose` | Close | `{Binding ClosePlayerCommand}` |

##### The nine decisions (2026-08-19, not reopened)

1. **The window's `DataContext` is the `ShellViewModel`, and there is NO new type.** It already
   exposes `Player`, `ToggleMiniPlayerCommand` and `ClosePlayerCommand`, and `PlayerSurfaces.Player`
   **is** a `PlayerViewModel`, which exposes `Transport`. It is assigned where the window is created,
   in `ShellView.axaml.cs` (`_miniWindow ??= new MiniPlayerWindow()`). **With no new file under
   `src/` there is no 96/96 floor to earn.**
2. **`TogglePlaybackCommand` is added to `PlayerViewModel`**, with predicate
   `CanPause || CanResume`. It inherits the notification that already works — that model raises both
   properties and calls `RaiseCanExecuteChanged` when the state moves — and **enters
   `CommandNotificationTests` as the eighth**, with its exact predicate: that gate carries a closed
   list.
3. **The chrome is ALWAYS VISIBLE. A firm decision, not a postponed one.** The package asks for
   "on hover and on focus". It is declined for two reasons and recorded as a **deliberate deviation**,
   exactly like the 2 px pressed border: (a) **the walk is the redesign's net** and its resolver looks
   for a control **before** moving the mouse, so it would find it invisible and `visible=False` would
   accuse the product of a defect it does not have; (b) a 480×270 window that does nothing but play
   has nothing competing for the space of five 36 px buttons. The package itself concedes that hidden
   chrome is an accessibility problem — that is why it adds "and on focus" — and the most accessible
   answer is for it to be there.
4. **A new style class `player-chrome`, in `DesignTokens.axaml`, for these five only.** Measured: the
   big transport **uses no class at all** — bare `Button`s — and the whole tree holds only three
   (`theme-option` 5, `navigation-destination` 5 which is a test marker, and `primary-action` 1). The
   package's `pl.pbtn` belongs to the HTML prototype, not to the code. **The big transport adopting
   the class is `PlayerView`'s job**, its own view, or it would drag another screen's layout baseline
   into this commit.
5. **`MinWidth`/`MinHeight` of 36 and `CornerRadiusMedium`, NOT a fixed size.** The package says
   36×36, and a fixed size with translated `Content` **clips text in one of the two languages**: a
   defect waiting to happen. A minimum of 36 gives the same hit area without betting that two
   languages measure the same.
6. **⚠ `CornerRadiusMedium` LEAVES `NotSpentYet` in that same commit.** `ScalarTokenTests` requires
   it: the gate **also fails when something on that list starts being spent**. The list goes from six
   to five. Without this, one CI round is wasted.
7. **`Content` and `AutomationProperties.Name` come from the SAME key**, as the transport's three
   buttons do. It is what the tree already does and what the walk expects in order to identify them.
8. **The order of the remaining views, after `MiniPlayerWindow`, `UpdateView` and `PlayerView`, is
   the order of the `SURFACES.es.md` inventory.** It is the canonical record of surfaces and is
   already measured, so there is no deliberating view by view.
9. **A view that needs no change gets no empty commit**: it is recorded in one line of the phase's
   evidence saying **what was measured** and why nothing changes.

##### Two traps already identified

- **The two skips "fold out at 320 px wide"**, the window's minimum. The walk uses the default size
  (480), so all five are there at 480 — but this is **exactly the shape that has pushed a control out
  of a window six times**, so the scene measures bounds against the window **before** attempting the
  click, not after the red.
- **`PlayerViewModel.cs` is already watched by the coverage ratchet**, so the new command may **raise**
  its floor, and the ratchet fails in that direction too — it happened on this very branch with
  `FluentThemeService`. Copy the run's `coverage-debt` artifact whole, never by hand.

##### What the commit carries whole

The five controls, their **five strings in both languages**, their **five accessible-name tests**,
**the walk scene that presses them**, `CornerRadiusMedium` off the list, and the eighth entry in
`CommandNotificationTests`. The walk ratchet rises to 5 and returns to **0** inside the phase.

**And before pushing: `IntegrationTests` reads the views as text too.** The suites affected by a change
to views are four, not two: `UiTests`, `AccessibilityTests`, `IntegrationTests` and
`DocumentationTests`.

#### The spacing scalars phase: FULLY DECIDED on 2026-08-20, and not reopened

It is the last design decision step 6 had open. It is settled by **counting**, and the count of
`Spacing` / `ItemSpacing` / `LineSpacing` across every `.axaml` under `src/` — **183 sites** — is this:

| Value | Sites | On the 4/8/16/24/32 scale? |
|---|---|---|
| 8 | 90 | yes |
| **12** | **45** | **no** |
| 4 | 21 | yes |
| 6 | 12 | no |
| 16 | 6 | yes |
| 24 | 4 | yes |
| 2 | 3 | no |
| 10 | 2 | no |

**121 of 183 (66%) are already on the scale. Of the 62 that are not, forty-five are the same value: 12.**

##### 1. The scale gains the 12 step

The gap between 8 and 16 is **2×**, and real usage piles up inside it: 12 is **a quarter of all the
spacing in the application**. Mapping it to 16 moves 45 sites **+33%**; to 8, **−33%**. Either would be
a large visual change decided **by rounding rather than by design**, which is the thing a token system
exists to prevent. **The scale was incomplete and the tree proves it**; the step gets declared instead
of forcing 45 sites onto a value nobody chose.

##### 2. The names go numeric: `Space4`, `Space8`, `Space12`, `Space16`, `Space24`, `Space32`

Semantic names — `XSmall`, `Small`, `Medium`, `Large`, `XLarge` — **force a new name to be invented
every time a step is missing**, and one just was: the alternative here was `SpaceSmallMedium`, which
describes nothing. A numeric name cannot lie and makes the mapping obvious at the point of use.

**The cost is zero today and will not be again.** Measured 2026-08-20: **no `.axaml` under `src/`
consumes any of the five** — all five are in `NotSpentYet` — so the rename touches their declaration
and `ScalarTokenTests`'s list, and nothing else. Doing it after they are spent would cost 183
substitutions.

**`CornerRadiusSmall` and `CornerRadiusMedium` stay as they are**, and that is not an oversight: they
are already consumed, they are only two values, and between 4 and 8 there is no gap a step could go
missing from.

##### 3. The mapping for the remaining seventeen

| From | To | Sites | How far it moves |
|---|---|---|---|
| 6 | `Space8` | 12 | +2 px |
| 2 | `Space4` | 3 | +2 px |
| 10 | `Space12` | 2 | +2 px |

**Measured result: no site in the application moves more than 2 px.** Against the 45 sites moving 4 px
that any mapping of the 12 would cost. That number is what decides, which is why this is not a
preference.

##### 4. What does not enter, already decided

`Padding`, `Margin` and `BorderThickness` **keep their literals**: they are `Thickness`, the tokens are
`x:Double`, and of their 89 literals **37 are asymmetric** — `0,8,0,0`, `48,0`, `8,4` — which no scalar
token expresses. This is said **in the token file**, beside the declaration, so the next person does
not try again.

##### 5. How it is carried out

**In one sweep, not view by view**: it is a mapping, not a per-screen decision, exactly like the
thirteen font-size literals that became five tokens without anyone noticing. And it has a termination
condition that checks itself: **`NotSpentYet` ends up EMPTY**, because all six scalars become spent.
`ScalarTokenTests` fails if any is left over, so the phase cannot be called done half way.

**Mind the test trap** that cost a round in `UpdateView`: a test comparing the **value** cannot tell a
literal from a token while the two agree, and they agree exactly when the tokenisation would be
correct. What gets asserted is that the `.axaml` **does not write the number**.

#### `PlayerView`: measured 2026-08-20, and the chrome class generalises with it

**Measured without writing code.** 171 lines, five buttons, none with an `x:Name` — they are
identified by their accessible-name key, which is what the walk uses — and **all five are already
pressed**, so this view adds no walk debt either. It is the second in a row that costs layout only.

**The literals, counted:**

| What | How many | What happens |
|---|---|---|
| `CornerRadius="8"` | 3 | → `CornerRadiusMedium`, straight swap |
| `Margin` 24/16/16, `Padding` 16/10/12, `BorderThickness` 1 | 7 | **they stay**: `Thickness`, and the token is `x:Double` |
| `Spacing` 8/8/12 | 3 | scalars phase, not here |

**`primary-action` goes on `PlayerRecoveryRetry`, and only there.** It is the "try again" of a failure
screen, with `PlayerRecoveryOpenExternally` beside it as the secondary. **Nothing in the transport
takes it**: `Play` and `Pause` alternate **by state**, so marking one would make the screen's primary
action change with what is happening — exactly what a hierarchy cannot do — and `Stop` is the point of
nothing.

**And this is where the package's decision 4 gets paid**, the one saying the large transport would
adopt the chrome class. Measured now that the class exists: `player-chrome` gives `MinWidth`/
`MinHeight` 36, `CornerRadiusMedium` **and a `Margin="4"`**. The three transport buttons live in a
`StackPanel` with `Spacing="12"`, so the class would add 4 a side and space them 20 apart. **The
margin does not belong to the class; it belongs to whatever places it.** So:

1. **`Margin` leaves `Button.player-chrome`**, which keeps the minimum press area and the radius — the
   only parts that are about the control rather than about its place.
2. **`MiniPlayerChromeView` moves to `ItemSpacing`/`LineSpacing` on its `WrapPanel`**, which is what
   `UpdateView` already did and what was written down as a "one-line fix" when it was measured.
3. **The three transport buttons adopt `player-chrome`** and gain the 36 minimum press area, which is
   a real accessibility improvement rather than a layout one.

**The affected suites are the usual four**, plus a look at `MiniPlayerChromeTests`, which asserts
`MinWidth`/`MinHeight` >= 36 on the mini's five: still true once the margin leaves, but that is the
test that says so.

#### `UpdateView` ~~measured~~ **done on 2026-08-20**, and the scalar that CANNOT be spent where it is needed

**The view is done** apart from its spacing, with [its evidence](evidence/stable/audit-update-view.md):
`primary-action` on `UpdateCheckButton` — the only candidate — and both `CornerRadius="8"` now spending
`CornerRadiusMedium`. **Spacing is not in it**, because that mapping covers 183 sites across the tree
and is decided once rather than view by view: that is the scalars phase, **fully decided on
2026-08-20** and written below with its count.

**And a new trap that cost one writing round:** the radius test **passed before the view was touched**.
It compared the painted value against the resolved token, and since `CornerRadiusMedium` is 8 and the
literals were 8, the numbers agreed. **A test that compares the value cannot tell a literal from a
token while the two agree**; the *source* is what has to be measured, by reading the `.axaml`. The
house already knew half of this — "a test comparing numbers written in views has to resolve the
tokens" — and this is the other half.

**Next is `PlayerView`**, then one view per commit in `SURFACES.en.md`'s order.

##### What was measured on 2026-08-19, and still holds entirely

**Measured without writing code**, as the `ComboBox` and the mini player were, so the next session
executes. And the headline is not about `UpdateView`: it is about **the whole view phase**.

##### The finding that decides the entire phase

**All five `Space*` are `x:Double`, and the properties that need them are `Thickness`.** That is why
all five are still in `NotSpentYet`, and why they stay there until somebody decides: a
`Setter Property="Margin" Value="{DynamicResource SpaceXSmall}"` **does not convert**, and the same
goes for `Padding` and `BorderThickness`. It was measured in the mini player commit, which ended up
writing a literal `Margin="4"` because of it.

Measured across all of `src/`:

| Where | Type | Occurrences | Literal values |
|---|---|---|---|
| `Spacing` (`StackPanel`), `ItemSpacing`/`LineSpacing` (`WrapPanel`) | `double` — **tokens DO work** | **183** | 8 (90), 12 (45), 4 (21), 6 (12), 16 (6), 24 (4), 2 (3), 10 (2) |
| `Padding`, `Margin` | `Thickness` — **tokens do NOT work** | **89** | mixed |
| `CornerRadius` | `CornerRadius` — they work | **35** | 8 (23) = `CornerRadiusMedium`, 4 (5) = `CornerRadiusSmall`, 6 (4), 10 (2), 12 (1) |

**The decision was between two options and THE COUNT SETTLES IT** (made 2026-08-19, over the 89):

| Shape | How many | Can a scalar token cover it? |
|---|---|---|
| Uniform and **on the scale** (16×21, 24×5, 32×2, 8×1, 4×1) | **30** | yes |
| Uniform and **off the scale** (12×11, 48×6, 20×2, 10×2, 28×1) | **22** | only by remapping |
| **Asymmetric** (`48,0`, `0,2`, `8,4`, `0,8,0,0`, `0,0,0,24`…) | **37** | **no, not in any form** |

**Option 2 wins, and not by preference: `Thickness` twins would cover 30 of 89, or 34%.** The 37
asymmetric ones cannot be expressed by a scalar even with twins — `Margin="0,8,0,0"` needs four
values — and declaring a family of asymmetric tokens would be inventing a scale the package never
asked for.

**Decided, and not reopened:** `Space*` are for `Spacing` / `ItemSpacing` / `LineSpacing` — 183 sites,
168 of them — 92% — landing on four values (8, 12, 4 and 6) — and `Padding` / `Margin` keep their literals. **This is
said in the token file**, beside the declaration, so the next person does not try again: finding it
out cost a literal `Margin="4"` in the mini player commit.

And **the literals that are not on the scale get mapped the way the type scale was**: 12 and 10 to
whichever fits, 6 and 2 to whichever fits, written once in the token file and not argued view by view.
Thirteen font-size literals mapped to five tokens and nobody noticed.

##### `UpdateView` itself

Its layout is already better than average: it uses `FontSizeSubtitle`, `TextPrimaryBrush`,
`AccentSubtleBrush`, `CardSurfaceBrush` and `ShellBorderBrush`, and its `WrapPanel` already uses
`ItemSpacing`/`LineSpacing` — which is, incidentally, **what the mini player's chrome should use
instead of its `Margin="4"`**, a one-line fix for whenever that view is touched.

What is left, and it is not a long list:

1. **`CornerRadius="8"` on two borders** → `CornerRadiusMedium`. Straight swap.
2. **`Spacing="12"`, `Spacing="8"`, `Spacing="6"`** → whatever the mapping above produces.
3. **`UpdateCheckButton` is the screen's primary action** and carries no `primary-action`. It is the
   only candidate here: download and install appear **by state**, and cancel is never primary.
4. **`Padding="16"` on two borders** → depends on the decision above.
5. **`MaxWidth="640"` and `MaxWidth="600"` stay.** They are readable line lengths, not scale; a token
   for that would be inventing a family with two members.

**Its four controls are already in the walk** (`UpdateCheckButton`, `UpdateDownloadButton`,
`UpdateInstallButton`, `UpdateCancelButton`, plus `UpdateAutomaticCheckBox`), so this view **adds no
walk debt**: it is the first of the redesign that costs layout only.

**Who reads it as text, and must be run**: `UpdateSurfaceTests` (UiTests), `AssembledJourneyTests`,
`AssembledPhysicalWalkTests` and `CompositionDescriptorTests` (AccessibilityTests).

#### ~~Phase 2f: the `ComboBox`~~ — done on 2026-08-19

**Measured with not a line written**, so the next session executes instead of discovering. Eight
uses, and it inherits nothing from the text field: `IsEditable` appears nowhere in the tree, so a
closed combo box has no `PART_BorderElement`.

**It has three families of its own**, and the third is the one that matters:

1. **The closed frame** — `ComboBoxBackground` / `PointerOver` / `Pressed` / `Disabled`,
   `ComboBoxBorderBrush*` (4), `ComboBoxForeground*` (4), `ComboBoxDropDownGlyphForeground*` (4) and
   `ComboBoxPlaceHolderForeground*` (2).
2. **The drop-down** — `ComboBoxDropDownBackground`, `ComboBoxDropDownBorderBrush`.
3. **The drop-down's rows** — `ComboBoxItem*`, **22 brushes** shaped exactly like the list row's.

**What paints today, measured:**

```
Light / HighContrastLight   IDENTICAL (fourth time)
  Border[Background]           #66FFFFFF, border #99000000 -> the border reads 5.69:1 on the fill
  Border[HighlightBackground]  #0078D7 at 40 % -> 1.74:1 against the frame
  Path (the arrow)             #cc000000 -> 12.47:1
HighContrastDark
  Border[HighlightBackground]  #0078D7 at 60 % -> 2.24:1 against the surface
```

**The defect is the list row's, at almost the same number**: the drop-down's highlight is Windows'
translucent blue at **1.74:1** in light and **2.24:1** in high contrast dark, against a bar of 3. The
frame's border (5.69:1) and the arrow (12.47:1) are already fine and only move to tokens.

**The way in and the known traps carry over**: measure the family with markers before redirecting;
the drop-down rows follow the list row (a faint fill **plus** a second cue, because in high contrast
the faint one is the surface); and declaration order matters wherever a state can coincide with focus.

#### ~~Phase 2e: the text field~~ — done on 2026-08-19, and it is worth two types

**Done** — [the evidence](evidence/stable/audit-redesign-phase2e-text-field.md). 16 aliases per theme.

**One family of resources can be worth several types, and it is measured like a single brush.**
`TextControl*` is taken by the `TextBox` (25 places) and the `NumericUpDown` (35, because it is a box
with two arrows), and by **none** of the button, checkbox or slider. The `ComboBox` only touches it
through the box it grows **when editable**, and `IsEditable` appears nowhere in the tree: a closed
combo box **has no `PART_BorderElement` at all**, so it gets its own family.

The four defects, with their numbers: the **hint in an empty field** read **2.11:1** (it carries
transparency **twice**, in the colour and in `Opacity`); a **switched-off field** could not be read —
2.56:1 — and had no shape — 2.51:1 against the surface, 1.66 in high contrast dark; the **focus
border** was `#0078D7` in all four themes, including the one whose focus is yellow; and **Light
painted identically to HighContrastLight** for the third time.

**A phase-1 style that painted nothing, measured along the way:** `TextBox:focus` **does** reach the
control — it sets the right `BorderBrush` — and the template **ignores it**, because what paints is
its `PART_BorderElement` from `TextControlBorderBrushFocused`. The ring still showed, being an
adorner, and the inner border said Windows blue. **It is the house defect wearing a setter's face.**

**And what the test had to learn:** a `NumericUpDown` has **two frames**, and the inner text box's is
**deliberately transparent** so it does not draw two concentric rectangles. Looking for
`PART_BorderElement` read black on black; ask for **the frame that shows**, not for a part name.

#### ~~Phase 2d: the list row~~ — done on 2026-08-19

**Done** — [the evidence](evidence/stable/audit-redesign-phase2d-list-row.md). 17 direct uses and
**23 lists with data** behind them.

**The defect**: the selected row stood apart from the others by **1.73:1** in light, 2.22 in dark,
1.76 and 2.24 in the high contrast pair, against a bar of 3. The label on it read 11.58:1, so the
defect **was never the text**: it was knowing which row you are on. And once again Light painted
identically to HighContrastLight.

**Three measured things that decided the design, and that carry to what is left:**

1. **A shared brush is redirected by measuring who else takes it.** The row's are system brushes
   (`SystemControlHighlightList*`). Painted a colour no theme uses, with **twelve** control types
   mounted and five pseudo-classes forced: **only the list consumes them** — not `ComboBox`, not
   `Menu`, not `TabControl`, not one of the ten focus types.
2. **The row's content presenter DOES take its `BorderBrush` and `BorderThickness`** by template
   binding, so an application style can give it geometry with no template of our own and no adorner.
   **Its text, though, comes from a generic brush** (`SystemControlForegroundBaseHighBrush`), so a
   selected row's label colour **cannot be given on its own** — which is what rules out a solid accent
   fill and leaves the tint plus the border.
3. **Declaration order decides.** Between styles that both match, **the last declared wins**, so the
   row's two styles go **before** the focus selectors: after them, a focused row would have lost its
   ring.

**And a third shape of transparency**: the selected row carries alpha **in the colour and in
`Opacity`** at once (`#FF0078D7` at 0.4). With three shapes in three batches, the contrast arithmetic
moved to one place, `ThemeContrast`, which composites both.

#### ~~Phase 2c: the checkbox~~ — done on 2026-08-19, and it was not a second button

**Done** — [the evidence](evidence/stable/audit-redesign-phase2c-checkbox-states.md). Eighteen across
the views, and **31 aliases per theme** (124 in all) against the button's 12.

**What to know before touching the next type, because it changes the plan:** a probe enumerating the
base theme's keys at runtime gives **1,054**, and per type: `CheckBox` **73**, `ComboBox` 59,
`RadioButton` 38, `ToggleButton` 37, `Slider` 32, `Button` 18 — and `TextBox` **2** and `ListBoxItem`
**1**, which paint from the generic ones (`TextControl*`, 32). **No type works like the last one.**

The three defects, with their numbers: a **checked, switched-off** box was unreadable in the light
theme (white mark over the grey `#33000000` leaves, **1.68:1**); the **disabled box's outline** read
**2.83:1** against a minimum of 3; and a **checked** box was `#0078D7` in all four themes. And
**Light painted identically to HighContrastLight**, as did Dark to HighContrastDark: nothing of this
project's reached a checkbox.

**Two lessons that cost a measurement each:**

1. **A brush is read whole, and the alpha lives in the colour.** The base theme's checkbox brushes
   carry alpha **in the colour itself** (`#99000000`, `#66FFFFFF`, `#33000000`), not in `Opacity`.
   The first version of the test measured luminance without compositing it and reported **1.00:1**,
   white on white — a false number. **And the danger was not that failure but its opposite**: where
   the alpha ran the other way it would have **passed** a 2.83:1 border as if it were 21:1.
2. **A bar is chosen by what it measures, not by what you want to pass.** The mark was held to 4.5
   (text) and a mark is a graphic, whose bar is 3.0. It was lowered **after** measuring, which
   deserves suspicion, so it is written down that **it rescued nothing** — 1.68:1 fails both — and
   that the new mapping clears 3.0 by 4.26 at its narrowest.

**And one test changed its question**: it asked for the box's outline against the surface, and in
high contrast hovering **inverts** — the box goes solid and the outline vanishes into it, 1.00:1 —
which is the clearest of the four states. It now asks whether the box can be seen by its outline
**or** by its fill.

**One intermittent, and on 2026-08-19 it came back a second time**:
`AssembledPhysicalWalkTests.A_session_that_will_not_open_is_handed_over_and_retried_with_the_mouse`.
Both times **in the whole suite** and both times **passing alone**. Still no cause, but no longer
mute: the assertion is the wait for the two-byte file to **fail**, on a **60-second** deadline — so
not slowness — and its condition `Player?.Player.HasFailed == true` read false **when there is no
session either**, while the complaint accused the file of having opened. Corrected separately: the
text is written when it is needed and tells the two apart, proved failing in both directions. The
next occurrence will say which one it is.

**What is next, in order:** ~~`ListBoxItem` (17)~~, ~~`TextBox` (15)~~ and ~~`NumericUpDown` (5)~~
**done**; ~~`ComboBox` (8)~~, ~~`Slider` (5)~~, ~~`ToggleButton` (2)~~ and ~~`RadioButton` (1)~~ **all done too, phase 2 is complete** — the
`ComboBox` has 59 resources of its own and does **not** inherit the text field's unless editable,
which does not exist here; the consumed-scalars gate with a list that only shrinks; `primary-action`,
which now has its token; and then the typography and the views.

#### What step 8 has to remember about the redesign (2026-08-18)

**`UX-003` and `A11Y-001` are `VERIFIED` citing high contrast, and until 2026-08-18 that was only
half true**: their evidence measured that the surfaces **render** when a test forces the variant by
hand, and the application never reached that state on its own. It does now.

**The split is decided as of 2026-08-19, so step 8 is mechanical.** Regenerating the manifest adds
these links and no others:

| Row | Evidence it gains |
| --- | --- |
| `UX-003` | [phase 1: the four dictionaries and the service that applies them](evidence/stable/audit-redesign-phase1-tokens.md) |
| `A11Y-001` | [phase 1](evidence/stable/audit-redesign-phase1-tokens.md), [2a: the button](evidence/stable/audit-redesign-phase2-button-states.md), [2b: the disabled outline](evidence/stable/audit-redesign-phase2b-disabled-outline.md), [2c: the checkbox](evidence/stable/audit-redesign-phase2c-checkbox-states.md), [2d: the list row](evidence/stable/audit-redesign-phase2d-list-row.md), [2e: the text field](evidence/stable/audit-redesign-phase2e-text-field.md), [the mini player](evidence/stable/audit-mini-player-chrome.md) |
| `UX-002` | [the update screen](evidence/stable/audit-update-view.md) |

**Both new rows were decided on 2026-08-20.** [The mini
player](evidence/stable/audit-mini-player-chrome.md) goes to `A11Y-001` because what that window gains
is **five controls with accessible names and a press area**, which is exactly the focus-and-contrast
row; and [the update screen](evidence/stable/audit-update-view.md) to that screen's own row, because
what changes there is **which action is primary**, which is visual hierarchy rather than accessibility.
`UX-002` is "Modern Fluent design in Avalonia", **checked against `FEATURES.md` on 2026-08-20** —
reading the matrix is not changing it, and leaving an identifier to guesswork would have cost a round
in step 8.

**`UX-003` gets phase 1 alone** because that row is about the **theme** — that it exists, that it is
applied, and that the player ignores it on purpose — not about a control's states. The five state
phases go to `A11Y-001`, the contrast and focus row, each carrying the numbers it corrected.

**[The walk harness's evidence](evidence/stable/audit-walk-press-retry.md) is linked from NO row**,
and that is a decision too: it describes how the suite measures, not a capability of the product.
Linking it would have the matrix promise something nobody can use.

**And the order matters**: adding a link before the cut was tried and the gate refused, rightly.
`EvidenceLinkTests` requires matrix and manifest to cite the same documents, and the manifest is
generated from a package and its hashes, so touching the matrix before that package exists means
generating it twice.

#### ~~Step 6's phase 1~~ — done on 2026-08-18, and phase 2 inherits three things

**Done in full and as decided**, plus what the measuring added:
[the evidence](evidence/stable/audit-redesign-phase1-tokens.md). Four dictionaries of **22 brushes
each** (they were three of nine), five new scalars, the high contrast accent out of the yellow, and
focus from 8 to 10 types with a **double ring** — drawn as an adorner of two concentric borders,
which is what solves the borderless `Slider` and the black-on-black of high contrast light. No view
was touched.

**What was not planned and came out of measuring:**

- **`ContrastTokenTests` measured a list of `<Color>` resources nothing painted**, which had already
  drifted from the dictionary (`#475569` measured against `#64748B` painted) and described a
  `HighContrastLight` with no dictionary. It now reads the four dictionaries and the 23 loose colours
  are gone.
- **`Focus(NavigationMethod.Tab)` is not pressing tab.** The `NumericUpDown` showed no ring until the
  probe became `window.KeyPress(Key.Tab, …)`: it hands the keyboard to its `TextBox` without saying
  the keyboard brought it. Three attempts from the harness before that.
- **A `ToggleSwitch` hangs the ring on the `Grid` in its template**, not on itself.

**Phase 2 inherits, and this is not reopened:**

1. **Nothing spends the space scalars.** Measured: not one view reads `SpaceSmall`, `SpaceMedium` or
   `SpaceLarge`, which were already there; nor the four new ones. The control states spend them, or
   they go. A token declared and never spent is the house defect.
2. **The dotted border of the disabled state** (a `Rectangle` with `StrokeDashArray`; `Border` has no
   dashed stroke) goes with the five states, which is where it is used.
3. **The typography**, already decided for that phase.

And one known limit, written down so it is not discovered twice: **high contrast is read when the
theme is applied**, so turning it on in Windows while the application is open arrives on the next
launch. Following it live needs `WM_SETTINGCHANGE`.

#### What phase 1 decided, to be read with the above (2026-08-18)

**Thirteen new brushes, not twelve.** The README says twelve and its table lists thirteen; measured
against `DesignTokens.axaml` — nine brushes per dictionary — and against `Resources/Brand.axaml` —
three strings, no colours — **all thirteen are new**. The five scalars do add up:
`FocusInnerStrokeThickness`, `SpaceXSmall`, `SpaceXLarge`, `CornerRadiusSmall`, `CornerRadiusMedium`.

**And the finding that changes the scope: nothing applies high contrast today.**
`AppThemeVariants.HighContrast` is referenced only by the AXAML itself, and `FluentThemeService` maps
`System/Light/Dark` and nothing else. The dictionary exists and **no path selects it**: the house
defect wearing a theme's face. So the fourth dictionary does not arrive alone — it arrives with
whatever feeds it.

**Decided, and not re-deliberated:**

- **`IHighContrastService`** in `Presentation/Theme` shaped exactly like `IReducedMotionService`, with
  **`WindowsHighContrastService`** in `Windows/Accessibility` over
  `SystemParametersInfo(SPI_GETHIGHCONTRAST)`. `FluentThemeService` consumes it and, when the system
  is in high contrast, the variant becomes `HighContrastLight` or `HighContrastDark`.
- **Light or dark is decided by the luminance of `COLOR_WINDOW`** (`GetSysColor`), never by the name
  of the Windows theme: names are localised and users define their own, the colour does not lie. Above
  0.5 is light.
- **`ThemePreference` does not change** (three pills, already decided), so **there is no settings
  migration**. The service is registered **with its consumer in the same change**, or
  `ServiceConsumptionTests` catches it — which is exactly what should happen.
- **`AccentBrush`**: `#0000FF` in `HighContrastLight`, `#00FFFF` in `HighContrastDark`. Yellow is left
  to focus.
- **In both high-contrast themes, warning, error and success share surface and border** (the theme's).
  The glyph and the heading tell them apart, never the colour — and that takes the warning out of
  yellow without a rule of its own.
- **Typography is NOT in this phase.** It belongs to phase 2, with the control states, which is where
  it is first used. This phase is colour, scalars, dictionaries and focus.
- **Focus selectors go from 8 to 10** (`ToggleSwitch`, `RadioButton`) and the ring becomes double. The
  disabled dotted border needs a `Rectangle` with `StrokeDashArray`: `Border` has no dashed stroke.
- **`ContrastTokenTests` extends to the new tokens and the four dictionaries**, proved failing in both
  directions. No view is touched until those tests pass.

#### ~~`LibVlcFactory.cs`: a floor given back, and why~~ — done on 2026-08-18

Run `32161925025` measured **93/85** where the floor said **94/90**, with the file unchanged, and the
floor was given back by copying the artefact whole. The cause was measured by comparing **five CI
runs line by line**: a single line and a single branch separate the bad measurement from the other
four, and both are the flush **exhausting its five-second ceiling**. It was not the deferred timer,
which is what this note said: it is that the release queue is **one for the whole process**, so a
busy runner leaves it full for more than five seconds and an idle one does not. Nobody asked for that
branch; chance exercised it.

The product does not change, because the ceiling does exactly what it should. What was missing was
the test: it asks for a ceiling **below the quiescence window** and asserts the giving up, so that
outcome stops depending on the clock. From **93.68/90 to 96.70/100 across three identical runs**, and
three more teardown decisions and a property nothing read along the way.
[The evidence](evidence/stable/audit-libvlc-flush-determinism.md).

**The floor rises with the artefact of the run that verifies that commit**, whole and never edited by
hand.

What follows is kept because it describes the shape of the correction, which is the reference for the
loose path:

**A file activated from Explorer plays and cannot be seen.** Measured on 2026-08-17:

```
singleton.IsLooseSession=True  name='Arrival.2016.mp4'  engine=Playing  pos=00:00:00.15
player=False  playerVisible=False  stages=0  surfaces=0
```

The activation does its whole part — `OpenLooseFile` starts the engine and the banner receives its
session — but **nobody builds the player surfaces**, and `HasLooseFile` is
`Player?.LooseFile is not null`. So the video plays with no picture and no transport, and the notice
saying "this is not in your library" never reaches the screen with its three buttons inside. The
**local trailer** opens the same way and has the same problem.

**The root cause, and why the correction is what it is: two paths open media and only one builds a
screen.** `OpenLooseFile` starts the coordinator itself; `PlayerViewModel.OpenAsync` starts it and has
a surface. While there are two, this comes back.

**Decided: `OpenLooseFile` validates and describes, and opening is always the player's.** It stops
calling the coordinator and keeps its two refusals — an extension outside the approved list and an
absent file — which are the ones needed **before** anything is touched. One path is added and both
callers use it:

- `ShellSurfaces.OpenLoosePlayer` — `Func<string, CancellationToken, Task<PlayerSurfaces?>>`, with its
  `ShellViewModel.OpenLoosePlayerAsync`, beside `OpenPlayer` and for the same reason.
- In the composition: ask `OpenLooseFile` for the session, build `PlayerSurfaces` with **`Player`,
  `LooseFile` and `VideoStatus`** — only `Player` is required on that record — plus the container's
  transport, and call `player.OpenAsync(session.MediaFileId, session.Path)`.
- **No tracker, no markers, no versions and no resume offer**, which is what keeps the promise: "a
  loose session leaves the database as it found it".
- Both callers go through it: the activation (`ConfigureWindow`) and the local trailer
  (`onPlayTrailer` in `CompositionRoot`).

**What is gained in passing, and it is a real improvement:** today a loose file that cannot be decoded
has its `catch` clear the banner and nothing is left on screen; opening through the player, the
failure reaches `Report` and **the recovery screen appears** — the one batch 2e has just left proven.

**What does not work, and this is checked:** reusing `OpenPlayerAsync` as it stands — it begins with
`FindByIdAsync` and a loose file is not in the catalogue, and its path starts the progress tracker;
and letting `OpenLooseFile` keep starting and opening again from the player, which is a double open
for no reason.

**Before touching it, re-read `FileActivationTests`**: it asserts the promise that cannot be lost — a
census of more than twenty tables identical before and after an activation — and does **not** assert
that `OpenLooseFile` starts the engine, so moving the start does not break it. `OpenLooseFileTests`
does speak about the coordinator and is updated with the change.

**How the harness gets there:** `ApplicationHost.PendingActivationPath` before `CreateShell` and
**`ConfigureWindow` after**, which is where the activation is read and nowhere else.

#### 3. The subtitle measurement — what gets measured, decided

`A11Y-002` is blocked **by measurement rather than by observation**. The direct way is tried first:
decode one frame with the style applied and one without it and compare the bitmaps; if the engine will
not hand over a frame, plan B measures **the cause**, which is already diagnosed — the LibVLC instance
is cached per option set and none of the cached ones carries subtitle options. Either way the outcome
is the same blocker with a number behind it. It goes in `MediaTests`.

#### 6. The redesign — the ratchet rule, decided before starting

The walk counts **129 declarations in 128 identities** today by reading the `.axaml`, and the script
only knows how to shrink. A redesign moves that inventory, so:

- **A new control arrives with its scene in the same change**, never with a line in
  `eng/walk-pending.txt`. The pending list closed and does not reopen.
- **A renamed control** has its anchor changed in the scene that presses it; the anchor is the resource
  key behind `AutomationProperties.Name`, and a redesign changes the shape without removing it.
- **A control that disappears** leaves the inventory by itself, and the ratchet drops with it.
- The **two overlays still undimensioned** — `SkipMarkerButton` and `LooseFileBanner` — are corrected
  here if the redesign touches them, and otherwise each in its own scene with its own measurement.
- The **five brand assets** belong in this step, where the visual direction lives. If they arrive here
  they reach 0.2.0 at no cost; the package builds today without them.

#### The five decisions of 2026-08-17, taken and not reopened

~~**1. The version dialogue's two remaining answers (2 controls).**~~ **Done on 2026-08-17, 10 → 8** —
[the evidence](evidence/stable/audit-walk-version-switch-answers.md). The detached control reproduced
exactly — `before detached=False en=True`, `after detached=True en=False name=<null>`, with a live row
in its place — **but it was the symptom**: `PressAsync` presses again when the probe does not change,
and by the second attempt the session had been rebuilt. The cause, measured in the same run, was
`asking=False`: **the question was never raised**, because the resume floor is 30 s and the scene
switched onto a twenty-second version — no position satisfies both. The lengths become **60 s and
180 s** and the order becomes confirm → refuse → start over, fixed by the same arithmetic. And with
the lengths fixed, **a product defect nobody was watching** came out: confirming a switch worked out
the transferred second, stored it (`00:02:01`) and then **opened the other version at zero**, writing
that zero over it (`playhead: 0, 0, 0, 1, 1, 2`). `PlayDetailsRequest.StartPosition` was **produced in
five places, documented, guarded by a test on the producer's side, and read in none** — the house
defect seen from the consumer. It becomes `TimeSpan?`, where `null` means "decide with the resume
policy".

**An open finding that came out of it, unmeasured and therefore uncorrected:** the film card's "Play
from the start" passed `TimeSpan.Zero` to a host that ignored it, so with stored progress it
**probably did not start at the start**. Now that the requested position wins it should be fixed in
passing; what is missing is **measuring it**, and the natural place is the batch 1 scene that already
has to seed progress for Resume.

~~**2. The ninth exit for the isolation rule (2 of 2e's controls).**~~ **Done on 2026-08-17, 8 → 6** —
[the evidence](evidence/stable/audit-walk-player-recovery.md). The recorder
(`RecordingExternalPlaybackLauncher`, verb `play-externally <path>`) joins `check-coverage.ps1`'s
watched list at 100/100, and its **two refusals** — an extension outside the list and an absent file —
are asserted in `IsolatedRunTests` beside both halves of the choice. What did **not** hold was the two
presses sharing one surface: `corrupted=True canRetry=False canOpenExternally=True`. The policy gives
corrupted media another version and an external open, **no retry**, and it is right — reopening the
same bytes fails the same way; retry is offered when the **file is missing**. The scene opens twice
and each press meets the failure that offers it.

**3. The overlays that still do not size themselves** — now only `SkipMarkerButton` and
`LooseFileBanner`, because `VersionSwitchDialog` was corrected on 2026-08-17 with its measurement
(`surfaces=1 [0, 0, 1280, 1400]` over `stage=0, 0, 1280, 1400`) — are corrected **each in its own scene
with its own measurement**: a `Border` with alignment, background and border, and their button rows as
`WrapPanel`s. The measurement comes first: the control's bounds against the stage's.

**4. `A11Y-002` at the version cut: it goes to `BLOCKED`.** The subtitle style reaches the database and
**not the picture** — LibVLC takes its rendering from the options its instance is built with, and here
there is one cached instance per option set with no subtitle options in it — so "customizable
subtitles" is not delivered however much its six controls exist and persist. It changes **at the cut**,
which is where the manifest is regenerated from a freshly built package, with the blocker named in
`eng/generate-verification-manifest.ps1` and in `release-readiness.md`. The ten-minute physical walk
also gains a check: **whether subtitles look the way they were asked to**.

~~**5. Shutting down with an active session gets fixed, and this is how.**~~ **Done on 2026-08-17**,
exactly as decided: `ApplicationHost.DisposeAsync` stops the session (`StopAsync`, with
`ObjectDisposedException` swallowed) before `_services.DisposeAsync()`. What proves it is **not** the
2a scene but the new recovery one, which ends with a video still playing for the same reason: closing
the player first is what every scene did and what hid this.

Then: coverage over all of `src/`, what is left of `ARQ-004`, and the redesign.

~~**7c — the updater's Cancel.**~~ **Done on 2026-08-17, 34 → 33** —
[the evidence](evidence/stable/audit-walk-update-cancel.md) — exactly as decided: an **optional**
`serveDelayMilliseconds` in the manifest, `await Task.Delay(delay, ct)` in the transport, a scene of
its own, and the status as the probe. **The window was measured and 3000 ms would not do:** both
presses spend **950 ms**, but the window also has to hold `PressAsync`'s retry budget — eight presses
a settle apart, **2400 ms** — so it is **5000 ms**, which costs nothing because cancelling abandons
the rest of the wait.

~~**6b — backup's Cancel.**~~ **Done on 2026-08-17, 33 → 32** —
[the evidence](evidence/stable/audit-walk-backup-cancel.md) — and **the destination is still 0**: the
honest library that takes long enough exists. Both levers were measured in the decided order and the
second won for a reason nobody had predicted: **the cost per file outweighs the cost per megabyte**,
so the lever is how many images there are rather than how much they weigh. The catalogue only reaches
a second at 50,000 rows (1,159 ms) and makes the scene expensive; **6,000 images of 50 KiB give
3,944 ms for 293 MB**, where 6,000 of 100 KiB give 4,377 ms for twice the disk. No hook in the
composition, and no product defect: the control worked, and what was missing was a window.

**Batch 2 — DECIDED that it splits into five scenes by surface**, not one of 29 controls:

| | Surface | Controls |
|---|---|---|
| ~~**2a**~~ | ~~Tracks and audio output~~ | **done on 2026-08-17, 32 → 27** |
| ~~**2b**~~ | ~~Subtitle style~~ | **done on 2026-08-17, 27 → 23** |
| ~~**2c**~~ | ~~Markers: editor, review and skip~~ | **done on 2026-08-17, 23 → 16** |
| ~~**2d**~~ | ~~Resume, next episode and switching version, with all three answers~~ | **done on 2026-08-17, 16 → 8** |
| **2e** | ~~Player recovery~~ **done on 2026-08-17, 8 → 6**; the loose file waits on the defect above | 3 |

It is the only batch that needs **real video**. And the warning stands measured: **the remaining
overlays set no alignment** and stretch over the whole stage, exactly like the status one corrected on
2026-08-15; **each is corrected in its own scene with its own measurement**, never in bulk. Two left.

**What 2a left behind, and what saves time in the four that remain:** a drop-down is tested by
**opening** it — what is chosen inside lands in another window root — and closed with Escape before
the next; `RequireMultiTrackSampleAsync` produces and caches a sample with **two audio tracks and one
subtitle track**; and the defect it found is the house's own seen from the other side: a scope the
application **reads** and that nothing in it could **write** —
[the evidence](evidence/stable/audit-walk-tracks-and-audio-output.md).

**The three from batch 1** are the ones needing seeding the walk does not do yet: the episode row (a
show, a season and episodes), the film card's Resume (stored progress worth returning to) and its
local trailer (a trailer file beside the film, with a version group).

**Cutting a release.** There are now **thirteen** evidence documents waiting to enter `FEATURES.md`,
and regenerating the manifest is part of cutting a release rather than of a working session.
**Decided**: when the walk reaches its floor, **0.2.0** is cut against a freshly built package, and
all thirteen go in at once.

**Shutting down from the window and from the tray stays direct.** They are not inventory controls —
there is no AXAML behind them — so they do not touch the ratchet, and no isolated run reaches them
today. It is revisited if the redesign touches the lifecycle, and not before: inventing work there
would be inventing it.

### The decision that unblocks the rest: an isolated run touches nothing outside its own root

Three controls were declared uncoverable for the same reason, and the third time stops being a
coincidence and becomes a rule of the project:

- **Granting start-with-Windows** wrote to the key Windows reads at sign-in. **Resolved on
  2026-08-16** with `IAppDataPaths.StartupRegistrySubKey`.
- **The provider trailer link** (`DetailsTrailerLinkAction`, on the film card and the series card)
  hands the address to the Windows shell, which opens a real browser.
- **The file and folder pickers** (backup, restore, adding a root) open a modal system dialog no
  harness can answer.

**The rule, and it applies to all three:** a run whose data root is **not** the profile's — a harness,
the walk, a lifecycle check — **writes and opens nothing outside that root**. Instead of opening the
browser it writes the address it would have opened; instead of opening the dialog it resolves the path
its root declares. The run that owns the profile behaves exactly as it does today. And this is not
only about being able to press: **nothing checks today that the address that would open is the right
one**, and this checks it.

### The order

1. ~~**The isolation rule, and with it both `DetailsTrailerLinkAction`.**~~ **Done on 2026-08-16.**
   `IAppDataPaths.SystemHandoffDirectory`: a root that is not the profile's gets a folder to write
   what it would have handed to Windows, and the owning run gets `null`, because the distinction is
   not where the handover goes but **whether** it happens. The link's refusals moved into
   `ExternalLinkPolicy` in the domain, and both exits ask it. The walk reads both addresses —
   `FilmTrailer` on the film, `ShowTrailer` on the series — in the order they were pressed. As a
   bonus, the coverage gate found an **unreachable guard** (empty host, with `https` already required)
   and it was removed with its measurement. **75 → 73.**
2. **Batch 5 — the review inbox (6) and duplicates (1).** **Four done on 2026-08-16** — load more,
   accept, reject and manual search — with **two product defects fixed**: the Search button never
   enabled (a private command class with an empty `CanExecuteChanged`, a survivor of `ARQ-004`) and
   its event was **listened to by nobody** in `src/`; it now reaches `SearchForMatch`, the manual
   counterpart of `IdentifyScannedFiles`. **73 → 69.**

   **Both reassignments, done on 2026-08-16**, with **two more product defects** and one harness
   defect — [the evidence](evidence/stable/audit-walk-reassignment.md). The earlier recipe was short
   on one point that decided everything else: `FileReconciliationPolicy` answers `Exact` for a stable
   identity and for a unique fingerprint, so **the only offer the application produces is a
   fingerprint collision**, and a collision means **two candidates, and therefore two buttons**. Hence:
   (1) both "Same file, reassign" buttons carried the same accessible name while deciding different
   entities — they now carry the candidate's path as help text, like `EpisodeRowView`; (2) the row was
   a horizontal `StackPanel`, which offers **infinite width**, so the path never wrapped and pushed the
   button to **x=2234 in a 1600 px window**: off the screen, with nothing to scroll, making
   confirmation **impossible** for any real library path. It is a `Grid` with `*,Auto` now. And from
   the harness: `WalkLedger` read the control's view **after** the effect, and confirming removes the
   offer the button lives in, so identity is taken before the press. **69 → 67.**
   **The duplicate radio, done on 2026-08-16** and with **no defect at all**: the first such in four
   sessions — [the evidence](evidence/stable/audit-walk-duplicate-version.md). It continues the walk's
   first scene rather than staging anything new, presses the copy that is **not** already effective,
   and probes the group's `preferred_media_file_id` — null before, the pressed file after — because
   without a stored preference the policy already answers with one of the two, so reading `IsEffective`
   would have called "the better copy" and "the one somebody chose" the same thing.
   **67 → 66, and batch 5 closed.**
   **And the debt, paid on 2026-08-16** — [the evidence](evidence/stable/audit-review-inbox-coverage.md).
   `ReviewInboxViewModel.cs` goes from **92.13/59.26 watched by nobody** to **100/100 held at every
   run**, taking the list in `eng/check-coverage.ps1` to six files. Two things the measurement taught,
   and they apply to everything ahead: **pressing both buttons moved no branch at all** — a walk proves
   a control works, never the paths taken when something goes wrong — and **a branch is covered whole
   within one suite or it is not covered**, because merging Cobertura keeps the better report for a
   line rather than the union: the walk took the "there is more" side, the UI tests took the "there is
   not" side, and the branch read as half-covered forever. As a bonus, three **unreachable** branches
   behind an `as AsyncRelayCommand` that could not fail but could stop matching, which is the way back
   to `ARQ-004`.
3. ~~**Batch 8 — the shell and home (14).**~~ **Done on 2026-08-16, 66 → 52** — the largest
   single-batch step since the ratchet existed, and [the evidence](evidence/stable/audit-walk-shell-and-home.md)
   carries **two more defects**, one of them the worst of the day. (1) **Continue was wired to
   nothing**: `onResume: null` in the container, with the button enabling itself because there was
   progress to return to — the primary action of the application, on the first surface anybody sees,
   doing nothing. It now opens the session on the version the position came from (`watch_state` keeps
   it for exactly that), and the shell is read at press time rather than captured. (2) **Mini player
   and Fullscreen sat off the screen**, at x=1737 and beyond, and not because of the window size:
   their column is **320 px by definition** and the three buttons are about 800, so they fitted at no
   width at all. It is a `WrapPanel` now, like the backup actions — the third time in one day that a
   horizontal `StackPanel` hid a control. From the harness: `Resolve` prefers the **command control**
   when a name is shared by an action and the region it leads to (the Home button and the Home
   surface), and only when that leaves exactly one, so it cannot mask two buttons with one name.

   **Noted, not re-deliberated:** `CompositionRoot.Library.cs` measured 97.14/100 and **stays off**
   the watched list, because that list is for files that **decide** rather than declare; and an
   optional hook the container leaves at null **is not caught by `ServiceConsumptionTests`**, since it
   is not a registration without a consumer. What watches it from today is the walk.
4. ~~**Batch 9 — root onboarding (8).**~~ **Done on 2026-08-16, 52 → 44** —
   [the evidence](evidence/stable/audit-walk-root-onboarding.md) — and with **a premise of this queue
   disproved**: there is no folder picker here. The folder is **typed into a box**, so the batch had no
   precondition at all and could have been done earlier; the real pickers (`OpenFilePickerAsync`,
   `SaveFilePickerAsync`) are on backup and restore, which is batch 6, where the precondition **does**
   still hold. The defect: **Remove at x=2146 in a 1600 px window**, another horizontal `StackPanel`
   with a path beside it. **The fourth time in one day, and now a rule of the house: whatever sits
   beside a value of free width goes in a grid.** And from the harness: **a probe is compared by
   value** — returning the list of folders made the beside click "change" it every time, because each
   read is a new array; the empty case passed because an empty array is one shared instance.
5. ~~**Batch 6a — backup and restore (4).**~~ **Done on 2026-08-16, 44 → 40** —
   [the evidence](evidence/stable/audit-walk-backup-and-restore.md) — and **without a single product
   defect**, the second such batch in five sessions. The isolation rule reached the file pickers with
   no new interface, decided in the composition by `SystemHandoffDirectory` the way the link launcher
   was; `HandoffArchivePicker` answers inside the handover folder and answers `null` when it holds
   nothing, which is what a cancelled dialog answers. The scene **composes no path at all**: it
   exports where the application says and restores whatever the application finds there.

   **What cost a run: a disk probe passing does not mean the screen has finished.** The copy's folder
   is published **before** the continuation that sets the status runs, so `BackupStatusRunning` was
   still on screen while the probe was already satisfied. The outcome is waited for, and only then
   stated. Same race the privacy scene met from the other side; two appearances make it a rule.

   **And it confirmed, for the first time from the assembled application**, that the swap works with
   the program open and the library loaded: `SwapAsync` calls `ClearAllPools()` before moving
   anything, and the catalogue opens and closes its connection per operation.

   **A finding that was wrong, corrected on 2026-08-17.** This entry claimed the second constructor of
   `StagedRestoreService` (`availableBytes`, `beforeSwap`) was used by nobody. **`DisasterRecoveryTests`
   does use it**, and for exactly what the hook is for: `onBeforeSwap: cancellation.Cancel` tests a
   cancellation right before the swap, and `onBeforeSwap: () => throw new IOException(…)` an
   interrupted one — the two paths that decide whether a half-way failure loses somebody's library.
   The call is `new(Paths, _ => availableBytes, onBeforeSwap)`, target-typed, which a grep for
   `new StagedRestoreService(` never finds. **Ask the compiler who constructs a type, not the search**:
   removing the member and building takes a minute and cannot be wrong.

5b. ~~**Batch 6b — Cancel (1).**~~ **Done on 2026-08-17, 33 → 32** —
   [the evidence](evidence/stable/audit-walk-backup-cancel.md). The precondition was the one below and
   it was met by seeding: **6,000 images of 50 KiB** give a copy of **3,944 ms**, and both presses
   spend **1,211 ms**. What had not been predicted is which lever wins: **the cost per file outweighs
   the cost per megabyte**.

   - **The precondition is hard and it is the only one**: `IsEnabled="{Binding IsRunning}"` **and**
     `CanExecute => IsRunning`, so the button exists only while a copy is running. Against a harness
     library a copy finishes in **milliseconds** — measured on 2026-08-16 in the 6a scene, which
     creates a whole one without the progress bar ever being seen. A library that **takes long
     enough** has to be seeded — many rows, and personal artwork, which is what the copy walks — and
     measured **before** the scene is written. Not improvised at the end.
   - **Everything else is already in place from 6a**: the scene navigates to backups, the isolation
     rule answers both dialogs, and the probe for a cancellation is `BackupStatusCancelled` with **no
     new folder** published — because a cancelled copy must leave nothing to restore.
6. ~~**The sandbox lifecycle, expired since `DES-001`.**~~ **Done on 2026-08-16** —
   [the evidence](evidence/stable/audit-sandbox-lifecycle-reproduced.md) — and **the premise was half
   wrong**: the archived report already carried all nine phases, so what had expired was **only the
   manifest digest** (`402ae30c…` archived against `5e341b5f…` current). What was actually missing was
   the **versioned script** being able to produce them: `sandbox-handover.ps1` installed and launched,
   and `README-sandbox.md` itself declared the four lifecycle phases manual — which is exactly what
   made the measurement depend on somebody remembering.

   The script now does `file-association`, `windows-upgrade`, `windows-downgrade-refused`,
   `windows-repair` and `windows-uninstall`, and `windows-launch` also checks the database did **not**
   end up in the package's virtualised folder. The host gets the next-version package by **resealing**
   the current one with its version raised (`0.1.0.0` → `0.2.0.0`) rather than building the
   application twice: the manifest version is the whole of what Windows reads to decide whether an
   install is an upgrade. **One run writes both reports**, because a second cycle would install the
   package twice to measure one install.

   Result: `lifecycle.json` with **twelve phases green**, the five native ones included, and
   `PackagingTests` at 152. The database survived the upgrade at **372,736 bytes on both sides**, and
   uninstalling did not take the library with it.

   **And a false alarm worth an hour:** the refusal field read as `versi�n` and looked like corrupted
   evidence; the bytes were `\xc3\xb3`, which is `ó` in correct UTF-8. The corruption was in the
   console that printed it. An "obvious" fix would have changed a script with nothing wrong with it.
7. **Batch 7 — the updater (5) and database recovery (2). 40 → 33.** The decided, still-pending string
   belongs here: when the handover is refused, the message must say **where the verified package is**
   so the person can open it themselves — in both languages, and Windows' own dialog is **neither
   covered nor silenced**. **Investigated on 2026-08-16 so it need not be rediscovered:**

   **It is dearer than 6a, and the reason is measurable: the isolation rule has to cover four more
   exits.** Three are covered — the registry, the browser, the file pickers. This batch needs:

   - **The source and the download.** `IUpdateSource` is `GitHubReleaseUpdateProvider` against
     `https://api.github.com/`, and `IUpdateDownloader` fetches with `HttpClient` into
     `DataRoot/updates` (`CompositionRoot.Updates.cs:40` and `:45`). A harness cannot and must not go
     to the network. Isolated, both read from the handover folder.
   - **The launcher.** `IUpdateLauncher` is `WindowsUpdateLauncher(OpenWithWindows)`, and
     `OpenWithWindows` is `Process.Start` with `UseShellExecute` (`CompositionRoot.cs:1224`).
     Isolated, it writes down the package it would have handed over, as the link launcher does with
     the address.
   - **Opening the backup folder.** `HandleRecoveryAction` makes another `Process.Start` on a folder
     (`CompositionRoot.cs:1298`).
   - **Exit.** `RecoveryExit` calls `desktop.Shutdown()` **only if** the `ApplicationLifetime` is
     `IClassicDesktopStyleApplicationLifetime`, and under the headless harness **it is not**: today
     the button does nothing that could be probed. It needs a shutdown point the root decides, like
     the other three.

   **And recovery has a hard precondition of its own:** its view is **not in the shell**.
   `CreateShell` builds it only when `PrepareDatabaseAsync` returns a refusal
   (`CompositionRoot.cs:303`), so the scene has to **seed a database that fails** integrity or
   migration, and **cannot use `ShowShell()`**, which asserts `IsType<ShellView>`. It needs a mount of
   its own.

   **That mount is cheap, and it is measured:** `AssembledStartup.FinalContent` **already** returns
   `DatabaseRecoveryView` when the database refuses, and the harness has **only five** uses of
   `host.Shell`, **all** of them `GetVisualDescendants()`. It is enough for `ShellHost.Shell` to go
   from `ShellView` to `Control`; the **67** uses of `host.ViewModel` need no change if the record
   holds the model as optional and keeps exposing the same property. No second host class and no
   second `PressAsync` are needed.

   **The updater's five, with their probes:** the automatic-check switch — the stored setting, and
   the only one with no precondition; check — the offer on screen; download — the package in
   `DataRoot/updates`; install — what the isolated launcher wrote down; and **Cancel, which is the
   same obstacle as 6b**: `IsEnabled="{Binding IsBusy}"` and `CanExecute => IsBusy`. Against a local
   source the download finishes in milliseconds, **but here there is an advantage backup does not
   have**: the source belongs to the harness, so it can serve slowly on purpose. Measure it before
   writing the scene.

   **DECIDED on 2026-08-17, not up for re-deliberation:**

   - **The batch splits, and recovery goes FIRST.** **7b — recovery (2 controls), 40 → 38**: it needs
     two exits (open folder, exit), its mount costs one type change, and it **depends on no open
     decision**. Then **7a — the updater (5), 38 → 33**.
   - **How the update source is served to an isolated run.** `IUpdateSource` is **replaced** by one
     reading a manifest from the handover folder, and `VerifiedUpdateDownloader` is **kept** with a
     local transport, so the hash, the size and the `.partial` stay real. **What is NOT done: making
     `UpdateSigningKey.PublicKey` depend on the root.** That would move a security decision in order
     to test, which is precisely the reasoning this repository forbids. Minisign verification is
     tested where it already is: in its own unit tests, with its own vectors.
   - **Order within each half**: the exits first, in a commit of their own, with `IsolatedRunTests`
     covering **both halves of each** — isolated and profile-owning — in the same file, because merged
     Cobertura keeps the better report per line rather than the union. Then the controls.
   - **The updater's Cancel goes with 7a**, not separately: unlike backup's, its source belongs to the
     harness and can serve slowly on purpose. If the seeding measures expensive, it moves to a 7c and
     the evidence says so, rather than going quiet.

   **7a — the updater. Started on 2026-08-17: 38 → 37**, with
   [the automatic-check switch](evidence/stable/audit-walk-update-automatic-check.md), the only one of
   the five with no precondition. **Four are left** — check, download, install and Cancel — and their
   investigation is closed; **not up for re-deliberation**:

   - **The address comes from the manifest, not from the code.**
     `NetworkPrivacyTests.No_source_file_names_a_host_that_is_neither_declared_nor_handed_off` walks
     `src/` for `https?://…` and fails on any host the registry does not declare. Declaring a harness
     host would **lie about what the application connects to** and would widen `IsDeclaredHost`, which
     is what the network canary trusts. So the handover manifest carries the address **and** the host,
     and `VerifiedUpdateDownloader` takes its allowlist as a parameter, which it **already does**
     ("tests hand in their loopback server explicitly").
   - **`UpdatePolicy` requires `release.Sha256Signed`**, a verdict the source sets after verifying
     minisign against the embedded key. The harness source **asserts it**, like a double in a unit
     test, and the evidence says so outright: in an isolated run the signature is not verified because
     nothing is signed. What stays real is what is kept on purpose — hash, size and `.partial` — over
     a local transport. Making `UpdateSigningKey.PublicKey` depend on the root **stays forbidden**.
   - ~~**The launcher.**~~ **Done on 2026-08-17** —
     [the evidence](evidence/stable/audit-updater-handover-exit.md) — and with **no new class and no
     new interface**: `WindowsUpdateLauncher` already took the handover as a delegate, so
     `ISystemHandoff` gained `TryOpenPackage` and the composition hands it over. `OpenWithWindows` was
     left with no callers, and **the compiler said so**. The isolation rule now covers **six** exits.
     An asymmetry that lived in a comment moved to where it is decided: a null process is **success**
     for a folder — it lands in a window already open — and a **refusal** for a package, which is the
     refusal that really happens on a Windows with nothing registered for `.msix`.
   - ~~**The source.**~~ **Done on 2026-08-17, 37 → 36** —
     [the evidence](evidence/stable/audit-walk-update-check.md) — and with it **Check**.
     `HandoffUpdateSource` reads `update-manifest.json` from the handover folder; the address is
     **data and not code**, and the hash and size are **declared** rather than computed from the file,
     because computing them there would make the verification check the file against itself. The
     three answers stay apart: no manifest means **no release**, an unreadable one is
     **unreachable**, and another architecture is **a refusal with its reason**.
   - ~~**The download.**~~ **Done on 2026-08-17, 36 → 34** —
     [the evidence](evidence/stable/audit-walk-update-download.md) — and with it **download and
     install**. Only **the transport and the allowlist** are replaced: `VerifiedUpdateDownloader`
     does the work on both sides, so the hash, the size and the `.partial` are a real installation's
     — and the opposite is checked, which is what proves it: given a package that is not the promised
     one, the download refuses it and **leaves nothing**. The transport does **not** implement
     `Range` (the downloader already treats a non-partial answer as "start from zero") and composes
     **no paths** from the request.
7c. ~~**The updater's Cancel (1). 34 → 33.**~~ **Done on 2026-08-17** —
   [the evidence](evidence/stable/audit-walk-update-cancel.md). The archived red says why it had
   waited two batches: the press of Download came back with `UpdateStatusReady` where the scene
   expected `UpdateStatusDownloading`, because with the package on the disk beside it the whole
   download finishes in milliseconds. The manifest now declares an **optional** wait, the transport
   holds it on the caller's own token, and **the product was not touched**: what the cancellation
   travels — token, interruption, status — is its own.

   ~~**7b — the database recovery (2).**~~ **Done on 2026-08-17, 40 → 38**, in two commits and with
   **no product defect at all** — the third such batch in eleven:
   [the two exits](evidence/stable/audit-recovery-exits.md) and
   [the screen pressed](evidence/stable/audit-walk-database-recovery.md).

   - **The two exits are one port with two methods and one caller**, chosen in the composition by
     `SystemHandoffDirectory` the way the other three are. What is written down is one line per
     handover with a verb in front — `open-folder <path>`, `exit` — and the verb is what lets a probe
     tell the two apart without parsing.
   - **A finding that decided the design, and the compiler decided it:**
     `IClassicDesktopStyleApplicationLifetime` is **not implementable by user code** — Avalonia
     carries a member whose name is the warning itself — so no double can stand in for one and the
     "there is a lifetime" half cannot be exercised anywhere. The lookup stays in `CompositionRoot`,
     where **two literal copies** of it already lived and now there is one; what reaches the exit is
     the call, and both new classes land at **100/100**.
   - **The mount cost exactly what was predicted: one type change.** `ShellHost.Shell` from
     `ShellView` to `Control`, with the view model optional **behind the property it always had**; the
     five uses of `Shell` only walk the visual tree and the sixty-seven of `ViewModel` are untouched.
     A shared `Mount` came out of it too, because the two ways of mounting differ only in which
     settled content they assert.
   - **What stays out, and is said:** the window's close and the tray's exit **still shut down
     directly**. That is another path, with the placement saving around it, and an isolated run
     arriving that way would still shut down — unmeasured.
8. **Batch 2 (the rest) — the player and its overlays (29).** The longest, and the only one needing
   real video: tracks, audio output, subtitle style, markers, resume, next episode, version switch,
   loose file, and player recovery. **Measured warning: the five remaining overlays set no
   alignment** and stretch over the whole stage, exactly like the status one corrected on 2026-08-15;
   **each is corrected in its own batch with its own measurement**, not in bulk. **32 → 3**, and the
   last three with them: **0.**
9. **Code coverage, to the same destination.** The gate watches new files and a short list today —
   **thirteen since 2026-08-17**, with `AppDataPaths.cs`, `ShellExternalLinkLauncher.cs`,
   `HandoffArchivePicker.cs`, `WindowsSystemHandoff.cs`, `RecordingSystemHandoff.cs`,
   `HandoffUpdateSource.cs`, `HandoffUpdateManifest.cs`, `HandoffUpdateDownloader.cs` and
   `HandoffUpdateTransport.cs` at 100/100 because they are the **exits**
   the isolation rule went through and what decides what leaves the application and what it reaches
   for — so **an old file that gets worse is still watched by nobody**. **Decided**: every old file a batch touches and leaves at the
   floor joins that list when the batch closes, and once the walk reaches 0, `check-coverage.ps1`
   measures **all of `src/`** against the usual floor (96 % of lines and of branches) with an
   exception list under the house rule: **it can only shrink**.
10. **Redesign and documentation**, with the whole walk as the net.

**One task decided and measured on 2026-08-16, without a place of its own in the queue yet: what is
left of `ARQ-004`.** **Nine** private command classes still carry `CanExecuteChanged { add { } remove
{ } }` — in `LibraryViewModel`, `RootOnboardingViewModel`, `ShortcutSettingsViewModel`,
`DatabaseRecoveryViewModel`, `AppearanceSettingsViewModel` (two), `LifecycleSettingsViewModel`,
`ShellViewModel` and `WindowsTrayService`. **Eight are inert**, because their `CanExecute` is
constant. The ninth, in `LibraryViewModel`, **does carry a predicate** (`BackCommand` with
`Surface != LibrarySurface.Browse`), which is exactly the shape that left the Search button off for
good; today it **does not bite**, and that is measured: the walk presses `LibraryBackAction` and it
works, because the view becomes visible and the button asks again. **That last reason was false, and
it was measured on 2026-08-18: the walk worked because no AXAML bound `BackCommand`.** See step 5.
**Decided then**: leave them for now —
there is no observable defect, and replacing them is a mechanical migration across nine files, which
in this house needs three nets — and do them **in a batch of their own, after batch 2**, once the walk
covers all 128 controls and can serve as the net. If anybody adds a conditional `CanExecute` to any of
the eight before then, that class is replaced **in the same change**.

**What no headless harness can prove, and is therefore not dressed up as covered:** the picture on a
physical screen and TMDB answering over the network. That is the ten-minute physical walkthrough, and
it belongs to the owner.

**One decision deferred, and why.** The evidence from 2026-08-16 has **not been added to
`FEATURES.md`**. `EvidenceLinkTests` requires the matrix and
`docs/evidence/mvp/verification-manifest.json` to cite exactly the same things, and the manifest
**describes an artifact**: its provenance is the package's own, so regenerating it against an
`artifacts/package/` from another build would write a provenance belonging to nobody. Regenerating
the manifest is part of cutting a release, not part of a working session. **Decided**: they go into
the matrix **when the manifest is regenerated against a freshly built package** — there are fifteen now,
so that step stops being optional at the next release — and until then they live in
`docs/evidence/stable/`, linked from here:

1. [the trailer link](evidence/stable/audit-walk-trailer-links.md)
2. [the review inbox](evidence/stable/audit-walk-review-inbox.md)
3. [the reassignments](evidence/stable/audit-walk-reassignment.md)
4. [the copy that plays](evidence/stable/audit-walk-duplicate-version.md)
5. [the inbox's coverage](evidence/stable/audit-review-inbox-coverage.md)
6. [the shell and home](evidence/stable/audit-walk-shell-and-home.md)
7. [root onboarding](evidence/stable/audit-walk-root-onboarding.md)
8. [the recovery screen's two exits](evidence/stable/audit-recovery-exits.md)
9. [the recovery screen pressed](evidence/stable/audit-walk-database-recovery.md)
10. [the permission to look for updates](evidence/stable/audit-walk-update-automatic-check.md)
11. [handing the package to Windows](evidence/stable/audit-updater-handover-exit.md)
12. [checking for updates without the network](evidence/stable/audit-walk-update-check.md)
13. [the download and the confirmation](evidence/stable/audit-walk-update-download.md)
14. [the command nobody listened to](evidence/stable/audit-arq004-command-notification.md)
15. [the floor belongs to whoever measures](evidence/stable/audit-coverage-debt-belongs-to-ci.md)

The state at the close of the **second session of 2026-08-16**, which carried out step 1 in full and
four sevenths of step 2. **Three commits**: `1d80815` (an isolated run says where the browser would
have gone), `d91b497` (the gate found a guard that had never run) and `a799f17` (the inbox Search
button could not be pressed, and did nothing if it were). Before it, the day's first session left
five: `5f85fbd` (the rename renames), `3eab024` (the walk says where a press went), `5f96ac3` (it
returns to the top before pressing), `679d9f1` (isolated startup entry and all twenty settings) and
`2596bf6` (this queue). The Spanish version is in
[NEXT-SESSION.es.md](NEXT-SESSION.es.md). The canonical scope record is still
[FEATURES.md](FEATURES.md) — **43 verified, 1 out of scope, 2 blocked** (`PLY-004`, `PRD-002`); the
audit's outstanding work lives in
[2026-08-08-audit-remediation.md](superpowers/plans/2026-08-08-audit-remediation.md). This is only the
place to resume from.

## Startup check

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version                                        # 10.0.302
git status --short --branch                             # clean, on origin/codex/ap-reelume-mvp-x64
git merge-base --is-ancestor main HEAD; $LASTEXITCODE   # 0
```

## Where the code lives now

`apvisualsolutions/ap-reelume` has been **public** since 2026-08-10, as a fresh cut with a single root
commit. The full development history stayed in `apvisualsolutions/ap-reelume-archive` (private), so
**the SHAs the evidence documents cite resolve there, not here**. The local remotes are `origin`
(public) and `archive` (the history), and the old branches are kept as `archived/main` and
`archived/codex/…` pointing at the archive.

CI runs on hosted runners, free for public repositories. The self-hosted runner is still installed
under `.runner/` (git-ignored) but **switched off**, and the workflow has no way to call it.

## Finished on 2026-08-10 (second session, the legal one)

Four commits, each with its full cycle and its bilingual evidence.

- **The artifact delivers the licences it names.** It was the one open legal breach. `licenses/`,
  inside both artifacts, carries the five canonical texts — LGPL-2.1, GPL-2.0, Apache-2.0, MIT,
  BSD-3-Clause — and the copyright notices of ANGLE, SkiaSharp, HarfBuzzSharp, BouncyCastle, SQLite,
  SQLitePCLRaw and VideoLAN: fifteen files, 209 KiB. The Skia and HarfBuzz native notice turned up
  **twenty-odd libraries** that `libSkiaSharp.dll` carries — freetype, ICU, libpng, libwebp, zlib —
  and that appeared in no project document. Nothing is transcribed: `LicenceTextTests` compares every
  copy byte for byte against the NuGet package the build consumed, and reads every copyright out of
  the restored `.nuspec`. Detail in
  [audit-legal-licence-texts.md](evidence/stable/audit-legal-licence-texts.md).
- **TMDB's logo is in Credits**, which closes the last open point of their terms. The file is the one
  TMDB publishes and that can be shown: the SHA-256 they embed in the asset's address matches the
  versioned file's. They publish SVG only and Avalonia draws no SVG, so the view carries the file's
  geometry — a test compares the two character for character — rather than pulling in a renderer and
  half a dozen packages with their licences. The specification said 24 px against a 48 px product
  name; that 48 existed in no view, so it was measured and settled at 16 against 24. Detail in
  [audit-legal-tmdb-logo.md](evidence/stable/audit-legal-tmdb-logo.md).
- **ARQ-001 / WIN-005 / the rest of BUG-004.** The service provider has an owner and is released on
  exit; `PendingActivationPath` and the playback session's state left the statics.
  `DisableParallelization` came off `AssembledShellSuites` and the seventy accessibility tests pass
  without it, which is the proof the ownership is real. `WindowLifecycle` was extracted, the coverage
  gate put it at 70.89% of lines and 28.57% of branches, and it **went back**, like
  `WindowsFilePickers` before it. Detail in
  [audit-arq001-application-host.md](evidence/stable/audit-arq001-application-host.md).
- **Continuous integration went quiet for an hour at a time.** Six of this session's ten runs died at
  the sixty-minute ceiling, with the log silent from "build succeeded" to the cancellation fifty-six
  minutes later. The step after it, `eng/generate-test-media.ps1`, started FFmpeg with no bound.
  Every call now has a ceiling, every sample is announced before it is produced, and a test with an
  encoder that never returns checks that the script dies in seconds naming the recipe. Producing the
  whole matrix costs a measured 1.6 s, so the ceiling is not a performance budget. The first run with
  the ceiling in place failed in four minutes and named the culprit:
  `mkv-dual-audio-english-first`. The eleven recipes that used `-shortest` — a documented FFmpeg
  deadlock — now bound their output duration explicitly. **The hang never reproduced locally**: it is
  a race, and what was removed is the class of hazard, not a reproduction. If it comes back, it will
  now say which recipe.
- **The two hardenings the audit filed as "not exploitable".** One was far less so than noted: the
  external launcher handed the Windows shell a `.ps1`, a `.txt` and a file with no extension. The
  other did not exist in the form described, and that was only learned by forging the archive that
  would have exploited it. Detail in
  [audit-hardening-launcher-and-restore.md](evidence/stable/audit-hardening-launcher-and-restore.md).

## What comes next

The two-part queue — the last coverage debt and the intermittent red's instrumentation — **was run
in full on 2026-08-10** (fifth session), and `BUG-010` fell right after. **The next queue is decided
in full in [the plan](superpowers/plans/2026-08-08-audit-remediation.md): shape, place, first
measurement and acceptance for each.** It runs in this order without re-deliberating; what each one
does do is **measure before fixing**, because three written premises collapsed this week when they
were measured.

1. ~~**`BUG-011`**~~ **Done on 2026-08-14**: one deferred-release queue for the process. The factory
   learnt to flush on request with a ceiling that does not throw; the engine let go of its queue, its
   lock, its flag, its drain and its quiescence constant; the shrink-only list in
   `NativeInstanceOwnershipTests` is now **empty**. The media-before-player order and the 1 s window
   are untouched.
   [audit-bug011-engine-release-queue.md](evidence/stable/audit-bug011-engine-release-queue.md).
2. ~~**`ARQ-013`**~~ **Done on 2026-08-14**: the reference reading moved out to `SurfaceReferences`
   and strips comments before matching. Measured before touching it: **nothing** was hiding behind a
   comment, so the gate was blind but was not covering an orphan.
   [audit-arq013-reachability-comments.md](evidence/stable/audit-arq013-reachability-comments.md).
3. ~~**`ARQ-014`**~~ **Done on 2026-08-14**: the version comes from the assembly and the test asserts
   on the header that actually leaves, with the expected version read from `Directory.Build.props`.
   [audit-arq014-updater-identity.md](evidence/stable/audit-arq014-updater-identity.md).
4. ~~**`ARQ-012`**~~ **Done on 2026-08-14**, and brought forward past `ARQ-014` because that one
   needs to read `Directory.Build.props` from the root and would have written one more copy of the
   walk. The plan's estimate was **five times short**: 59 files found the root themselves and 56
   named the anchor.
   [audit-arq012-repository-anchor.md](evidence/stable/audit-arq012-repository-anchor.md).
5. ~~**`QA-001`**~~ **Done on 2026-08-14**, and the measurement turned the task around: **zero
   warnings** across the solution, so this was a gate rather than a debt — and it was needed all the
   same, because the three rules ship switched off. The zero was proven with a canary before being
   believed. [audit-qa001-culture-gate.md](evidence/stable/audit-qa001-culture-gate.md).
6. **Documentation last**: `DOC-101`, `DOC-201`, `T44.1`-`T44.6` and the user manual, which is
   written from the built application rather than from the code.

**Decision on the first release**: the pipeline is no longer blocked — the signing secret is in
place — but **`v0.1.0` is not cut yet**. Two things are missing that are not code and are not an
agent's to decide: the `REL-004` opinion and the physical walk. Releasing before them would trade a
pending verification for a date.

## An intermittent red to watch, not to paper over

On 2026-08-10 the `first-launch` phase of `verify-package.ps1` **failed once** on the branch and
**passed on the same commit** on `main`. Same code, same workflow, different outcome: it is
intermittent, which is why it is not a defect anybody can find by reading.

What is known, measured from the log:

- The phase took **137 s**, which is exactly the window deadline (90 s) plus the close deadline
  (45 s). Both ran out and the process was killed, hence the `exit code -1`.
- In **that same run**, `repair`, `downgrade-refused`, `open-with` and the four `windows-*` phases
  started the application and watched it paint. Only the first launch failed.
- ~~The first launch is the only one that actually migrates, and migrating blocks the interface
  thread.~~ **Measured on 2026-08-10 and refuted**: every cycle gets a data folder of its own, so all
  five migrate a new database, and a whole launch with its sixteen migrations costs **2,292 ms**
  against the **90,000 ms** of deadline that failure burned. What failed there was not a slow launch
  but one that never happened, and the candidate cause is open again. Detail in
  [audit-arq005-startup-baseline.md](evidence/stable/audit-arq005-startup-baseline.md).
- **Observed frequency: one in four.** It did not recur across the three following runs carrying that
  same code, nor across the eight after them up to `fa968de`. That is the figure to compare against
  if it is seen again.
- **And half the question was already answered in the archived log, unread.** That phase's full line
  said `16 migration(s) applied to a new database`: the process lived long enough to apply all
  sixteen, so "died before migrating" was ruled out from the start and the two hypotheses were never
  two. What no record says is the other half — whether anything was still alive to paint when the
  deadline came — because `exit code -1` is the harness's own kill.

**What is no longer missing**: since 2026-08-10 the verification **leaves a diagnosis** when the
window does not arrive. Before killing the process it records whether it was still alive, how much
processor time across how many threads — which separates spinning from waiting — the state of
`library.db` and `schema_history`, and what the data folder holds, all in the line CI prints. Detail
in [audit-first-launch-instrumentation.md](evidence/stable/audit-first-launch-instrumentation.md).

**What is not done**: raising the 90 s deadline. That turns the only signal there is into silence,
which is the mistake the media generator's `cancelled` runs already charged six runs for. If it comes
back, it stops being pending work and becomes the urgent fix — and now it will say something when it
does.

## Finished on 2026-08-10 (third session)

Four commits, each with its cycle, its bilingual evidence and its gates.

- **ARQ-010 — the container checks itself as it is built.** `ValidateOnBuild` on, with a test that
  hands it a broken collection **through the product's own path**; asserting on a copy of the options
  would only prove the copy. **It exposed no broken registration**, which is what the plan expected,
  and the limit was measured: it validates 109 of 156 registrations, because the 45 built through a
  factory are opaque by construction. It costs +0.22 ms.
  [audit-arq010-container-validation.md](evidence/stable/audit-arq010-container-validation.md).
- **ARQ-004 — a failure has somewhere to land, and can no longer close the application.** Measuring
  inverted the order of its two halves: `AppDomain.UnhandledException` does **not** stop the process
  from ending, it only records, so a command must always catch — and something that always catches
  always needs somewhere to put it. That somewhere did not exist (2 of 24 surfaces own failure state),
  and looking for it turned up that **the diagnostics report was built from one source**, the rename
  audit: in a session with no renames, an application that was failing looked healthy. Then, from
  **27 `async void` to 2**, and both of those catch. −582/+227 lines.
  [audit-arq004-failure-net.md](evidence/stable/audit-arq004-failure-net.md) and
  [audit-arq004-single-command.md](evidence/stable/audit-arq004-single-command.md).
- **ARQ-005, first half — the lock nobody could open.** The wait for the media keys left the `lock`
  and got a ceiling. It blocked the interface thread on **every video opening**, and with no answer
  the trapped thread was holding the very lock the cancellation needed.
  [audit-arq005-media-keys.md](evidence/stable/audit-arq005-media-keys.md).

## Finished on 2026-08-10 (fourth session)

Three commits, each with its cycle, its bilingual evidence and its full verification.

- **The verification says how long the window keeps you waiting, not just that it arrived.** Its
  first measurement refuted two things written here: **all five** cycles migrate a new database —
  migrations counted in every folder — rather than only the first, and the first launch is not the
  slowest of the three either. Three phases are measured rather than one **on purpose**, and that
  decision paid for itself immediately: comparing before with after, `open-with` repeated within
  6 ms while `first-launch` varied by 1245 ms between runs of the same code.
  [audit-arq005-startup-baseline.md](evidence/stable/audit-arq005-startup-baseline.md).
- **ARQ-005, second half: the window exists while it migrates.** The measurement came first and
  ruled out the false correction: `MigrateAsync` **yields at none of its awaits** — 140 ms of
  140 — so an await would have left the window just as blocked while looking fixed. The work goes to
  a thread of its own and the window appears on the first frame. As a bonus, the orphan-surface test
  caught `StartupView` on the spot and it became the graph's third root.
  [audit-arq005-async-startup.md](evidence/stable/audit-arq005-async-startup.md).
- **TST-001: the coverage gate now watches code that is not new.** Re-measuring came first and the
  result was not the expected one: two files exactly where they were the day before, and the third
  **fifteen points worse**, because ARQ-004 took its covered lines with it and nothing said a word.
  There is a watchlist with a ratchet in both directions, and two of the three debts are at 100%.
  [audit-tst1-coverage-debt.md](evidence/stable/audit-tst1-coverage-debt.md).

## Finished on 2026-08-10 (fifth session)

Three code commits, each with its cycle, its bilingual evidence and its full verification.

- **`BUG-010`: the native instance has one owner.** The media probe built its own LibVLC with the
  same three options as the playback one, so a process that catalogued and played kept two native
  engines — and the count that states "one per option set" could not see the second. The queue was
  the same story and worse: it disposed the media **unguarded** and left its flag raised, so a single
  failing release would have killed the worker for good. The rule is a source rule on purpose,
  because at runtime the second instance is invisible; and it found the class where the plan named a
  case, with a **third** queue in the playback engine filed as `BUG-011`.
  [audit-bug010-native-instance.md](evidence/stable/audit-bug010-native-instance.md).

- **TST-001 is paid off: the last debt goes from 86.73%/76.00% to 100% of lines and branches**, with
  nine unit tests aimed at what `ReconcileScannedFiles` **decides** — a cancelled scan, a result the
  scan could not catalogue, a path with no row, an unreadable identity that counts as a failure
  without costing the rest of the scan, `Updated` content refreshing the identity, a catalogue that
  throws, a cancellation that is not a failure. Measuring the list before writing anything cut it by
  a third: five of the entries noted by reading the code were already covered. And one turned up that
  reading does not give: **no** test had ever read the `AttemptedCount` property, because whole-record
  comparisons go through fields rather than properties. The gate's floor rises to 100/100.
  [audit-tst1-reconcile-coverage.md](evidence/stable/audit-tst1-reconcile-coverage.md).
- **The intermittent red now leaves a diagnosis.** The first move was reading the archived log of the
  only run that ever failed, which answered half the question: `16 migration(s) applied to a new
  database`. What gets instrumented is the half still unrecorded, plus processor time and thread
  count, which separate spinning from waiting. Nothing in the diagnosis may throw — it would replace
  the failure it explains — so every read is guarded and whatever goes wrong is reported inside the
  sentence. `LaunchDiagnosisTests` takes the functions out of the shipped script by **parsing** it and
  exercises them against processes whose state is known, including a `library.db` that is not a
  database.
  [audit-first-launch-instrumentation.md](evidence/stable/audit-first-launch-instrumentation.md).

## Where it continues (decided, not re-deliberated)

1. ~~**`LIB-015`**~~ **Done on 2026-08-14**, in the order the plan fixed. Three things changed on
   being measured: the hardened launcher meant to be reused **did not exist** — the tree's three
   `Process.Start` calls open a `.msix`, a folder and a media file, and none of them an address; the
   cache key **does not include the address**, so `append_to_response` would have served the previous
   payload as the new answer, and **raising `ProviderVersion` would have been worse** — those rows
   would stop being read and the 180-day limit is only enforced on the read of that same key, so
   nothing could ever delete them — which is why the migration empties what belongs to TMDB; and the
   network gate caught `www.youtube.com` in `src/`, settled with a **second closed list**
   (`HandedOff`) rather than by declaring a connection that is never made.
   [audit-lib015-provider-trailer.md](evidence/stable/audit-lib015-provider-trailer.md).
2. ~~**The missing link**~~ **Done on 2026-08-15**, in four commits, with `LIB-006` back in
   `VERIFIED` (43 verified, 2 blocked). The first measurement the plan demanded gave more than it
   asked for: the `media_file_id` → `title_id` bridge **is the GUID identity** and cost no migration
   — nothing in `src/` writes `titles`, so every title the catalogue projects is a scanned file — and
   what was actually missing was the **provider's own name**, which does not travel with the
   candidate and without which `GetDetailsAsync` throws.
   [audit-apply-identification.md](evidence/stable/audit-apply-identification.md).
   Behind it came **two defects nobody was looking for**: the first save on an unedited entry
   returned `NotFound` in silence, and **no caller raised the revision when it saved**, so the
   optimistic check was comparing against a number that never moved and two windows could both win.
   The in-memory doubles did raise it, which is why no unit test could see it.
   [audit-refresh-resolves-itself.md](evidence/stable/audit-refresh-resolves-itself.md).
   And the click uncovered the third: **the assembled walk mounted the window in a way the
   application does not**, leaving the shell off the logical tree and **every** command-bound button
   reporting itself disabled. It was only visible by clicking, and nothing clicked.
   [audit-walk-clicks-the-editor.md](evidence/stable/audit-walk-clicks-the-editor.md).

   ~~**The missing link, as described on 2026-08-14.**~~ That entry's first measurement uncovered that
   **nothing turns an identification into stored metadata**: `catalog_metadata` is written only by the
   editor and by a `RefreshMetadata` nobody feeds — the only assignment of its input in the whole
   repository is **in a test** — `ResolveMatch` publishes an event nobody listens to, and
   `ReviewState.Automatic` is only ever calculated. The synopsis of `LIB-013` and the key of `LIB-015`
   reach the database only by hand.
   **Decided in full, down to the order of the commits**, in
   [audit-identification-never-reaches-the-catalogue.md](evidence/stable/audit-identification-never-reaches-the-catalogue.md):
   an `ApplyIdentification` use case with its two callers, `RefreshMetadata` resolving from the stored
   reference, the editor without the property nobody fills, and the assembled walk reaching the editor
   with mouse clicks. **Migration `0018` already prepared the database.**
   **The first measurement, before a line is written**: how one gets from the candidates'
   `media_file_id` to `catalog_metadata`'s `title_id`. That bridge has not been measured.
   **The matrix was already corrected**: `LIB-006` moved to `BLOCKED` on 2026-08-14 with its blocker
   in the manifest — 42 verified, 3 blocked — and `LIB-007` **stays `VERIFIED`** deliberately, because
   its criterion is about thresholds and a correction that persists, and both hold. `LIB-006` returns
   to `VERIFIED` only when the click-driven walk is green.
3. ~~**`BUG-012` — the watcher that dies when its buffer overflows.**~~ **Done on 2026-08-15**, and the
   first measurement disproved the last half of what was written here: a `Continuous` root **never
   leaves** `_watching`, because `StartAsync` is a `Task.WhenAll` with the fallback scheduler and that
   one **never ends**, so not even a manual scan could revive the watcher — only starting the
   application again. An overflow now means "I have lost events" (`WatchErrorPolicy`, in the domain),
   travels as `FileChangeBatch.EventsLost`, becomes **one** recovery scan, and **the watching goes
   on**; the buffer is asked for at its 64 KiB ceiling; and a watcher that really dies is retried on
   the next fallback pass, which is the heartbeat this slice already had. **A real overflow does not
   reproduce on this machine** — 64 000 operations, zero overflows, at 8 KiB and at 64 KiB — so the
   decision is tested in the domain and no deterministic integration test is pretended.
   [audit-bug012-watcher-survives-overflow.md](evidence/stable/audit-bug012-watcher-survives-overflow.md).
4. ~~**`LIB-016`**~~ **Done on 2026-08-15.** Off by default, stale at 90 days — under the 180-day
   ceiling, with a test on the inequality — 20 per pass, stalest first, identified only, and yielding
   to a scan or an open video, checked **before each entry**. The switch lives in Settings → Privacy
   and **does not exist without a consented connection**. The declared network purpose changed with
   the code. **The measurement added what was not foreseen**: no production path writes a null
   `refreshed_utc` alongside a `provider_key` today (`identifiedWithNoDate=0`), so nulls-first is the
   guard for a row nobody writes rather than a case in the field. Acceptance by execution: the
   network canary counts **0** connections with the switch off and **2** with it on, in the same
   child process.
   [audit-lib016-automatic-refresh.md](evidence/stable/audit-lib016-automatic-refresh.md).

5. **The autonomous walk over the whole application. Decided in full on 2026-08-15 and it goes ahead
   of `DES-001` and the redesign**, because it is their safety net rather than an extra. Everything
   below is measured, not assumed.

   **What was measured.** 129 command controls across the 48 views — 95 `Button`, 18 `CheckBox`, 8
   `ComboBox`, 5 `Slider`, 2 `ToggleButton`, 1 `RadioButton` — plus 17 `ListBox`. **Two** are pressed
   with a mouse: `RefreshProviderMetadata` and the title lock. `MouseDown`/`MouseUp` appear in no
   other file.

   **The anchor, already proven.** Only 60 of the 129 carry an `x:Name`, so the walk looks controls up
   by the **resource key** behind `AutomationProperties.Name` — 239 elements carry one, 80 tests
   require it, and a redesign does not remove it. Proven against a control with no `x:Name`. **The two
   without a key do have a name**, via `{Binding}`: they are list items — the card's title, the
   duplicate's path — and their anchor is **the data the walk itself seeded**, which is better still
   because it ties the click to something the test controls. There is no accessibility defect there.

   **A premise that collapsed when looked at.** This document said it was "left to measure whether the
   player is reachable headless with LibVLC". The walk's own file had already answered it: its scenes
   run **with the real engine decoding frames**, and one of them plays, pauses with the space bar and
   saves a marker mid-session. What the player is missing is not reach — it is **its transport being
   pressed with a mouse**.

   **The shape, decided.** A gate comparing the tree's command controls against the ones the suite
   **actually pressed**, with a shrink-only list of outstanding ones. Recording happens **at runtime**
   — `Click` itself notes what it presses and the gate reads that report, the way
   `run-accessibility.ps1` and `run-recovery.ps1` already do — and **not** by reading source as text,
   which has broken three times already when code moved. A control counts as covered only with all
   three: a real click, an assertion **on the effect**, and a click **beside it** that does nothing.

   **The order of the batches**, by use and by risk: (1) library and cards — open, filter, sort,
   favourite, watched, rating — (2) the player's transport, (3) editor and rename, (4) settings —
   including `LIB-016`'s switch — (5) review inbox and duplicates, (6) backup and restore, (7) update
   and recovery. Each batch closes its area and takes its entries off the list.

   **Batch 1 is done (2026-08-15).** From **2** controls pressed with a mouse to **15**, out of
   **128 identities** — 129 declarations, and the one collapse is the Back button, declared twice
   across the library's two mutually exclusive branches. The gate is `eng/check-walk-coverage.ps1`,
   the list with reasons is `eng/walk-pending.txt`, and the ratchet stands at **113**. Detail in
   [audit-walk-first-batch.md](evidence/stable/audit-walk-first-batch.md).
   - **The shipped anchor could not reach the Back button.** Both detail branches live in the visual
     tree at once, so matching on the key found **two** controls where a click can only reach one.
     Only what is on screen is a candidate, and the ten rating buttons — which share one accessible
     name **by design** — are told apart by their `HelpText`.
   - **And the gate caught a second defect on its first run**: the editor's refresh button was
     pressed by its `x:Name` while the views declare it by its key, so **the same control had two
     names** and was pressed under one and pending under the other.
   - **The third is the worst and only appeared because it was looked for**: the beside click sat one
     control-height above its target, and in a wrapping row that is **the row before it**. The
     control click for *Clear rating* **turned the favourite toggle back off**, and the walk stayed
     quiet because its assertion only asked about the rating. A control click that presses something
     else **is a second, unrecorded press**. The point is now chosen **by geometry** — outside every
     command control on screen — and not with `InputHitTest`, already measured not to predict where a
     click goes.
   - **Unreachable, measured and named**: both `DetailsTrailerLinkAction` buttons — the film card's
     and the series card's — because pressing them hands the address to the Windows shell and opens
     a real browser on the machine running the gate. That is what `LIB-015` decided, so it is the
     walk's limit rather than a defect.
   - **Still pending in this area for seeding, not for reach**: `MovieResumeAction` (stored
     progress), `MovieTrailerAction` (a trailer file discovered through a version group) and
     `EpisodePlayAction` (the series card).
   - **Counting the 129 can be got wrong**: the first measurement read **142**, because
     `<ComboBoxItem>` matches `<ComboBox` without a word boundary.

   **Batch 2 is done (2026-08-15): the player's transport.** From **15** to **22** pressed; the
   ratchet drops to **106**. It is the batch that pays for the work: it found **three product
   defects**, all of them visible, enabled and incapable of doing anything, and all three alive
   because **the player answers the keyboard itself**. Detail in
   [audit-walk-second-batch.md](evidence/stable/audit-walk-second-batch.md).
   - **The video status badge covered the whole stage** — measured at 1280×1200 over 1280×1200,
     opaque — over the video and over the bar, swallowing every click. It set no alignment; it is a
     badge in a corner now. **The other five overlays set none either**: only the one the measurement
     proved was in the way was corrected, and the rest will come with their own batches.
   - **`PlayerViewModel` never called `RaiseCanExecuteChanged`**, so the buttons' enabled state froze:
     you could pause with the mouse and **Resume stayed disabled for good**.
   - **The volume slider was the only `OneWay` one of the five** and its view had no handler;
     `SetVolumeAsync` had two callers and both were the keyboard.
   - **And four harness traps**: teardown **replaced** the scene's failure inside the `using`; a click
     outside the window said nothing; **the centre of a range control is usually where it already is**
     (0–200 starting at 100); and the sample cache **ignored the requested duration**, so a 30 s skip
     ran off a 12 s file and left Stop disabled for an unrelated reason.

   **Batch 3 is deliberately half done (2026-08-15): the editor yes, the rename no.** 22 → **30**
   pressed, ratchet **98**. The editor is complete — six locks, Save and Restore — and **Save asserts
   on the row**, the first control in the walk whose effect is not on the screen. Detail in
   [audit-walk-third-batch.md](evidence/stable/audit-walk-third-batch.md).
   - **The rename cannot rename.** The assembled application asks to rename each file **to the name it
     already has** — `new RenameRequest(file.Path, Path.GetFileName(file.Path))` — and `RenamePolicy`
     correctly answers `NoChange` with no operation. The plan is always empty, Rename and Undo can
     never do anything, and the consent box guards a decision never offered.
   - **And nothing composes a name**: that is the only production `RenameRequest` in the repository.
     **What is missing is not wiring but a decision** — what a renamed file is called — and it is the
     owner's. The walk records it rather than inventing a convention, and the three controls stay
     pending naming this.
5. **`DES-001`, the agent's half: done on 2026-08-15.** The manifest's description is no longer a
   string with a slash in it: it is `ms-resource:AppDescription`, and
   `eng/build-package-resources.ps1` builds one resource per language **the manifest itself
   declares**, with the text read from the first paragraph of each README — where the winget entry
   already took its own — so both installation routes say the same thing. Detail in
   [audit-des001-package-description.md](evidence/stable/audit-des001-package-description.md).
   - **Two measurements decided whether it was possible**: `makepri.exe` sits beside `makeappx.exe`,
     and its output is **deterministic** — the same hash from two different directories — which is
     what the reproducibility comparison requires.
   - **And a trap no error message names**: the XML DOM writes `xml:space` as `d2p1:space` with a
     namespace of its own, and `makepri` answers "PRI224: root node not found", naming neither the
     attribute nor the file. The `.resw` files are written as text.
   - **Touching the manifest expired two manual measurements** — `windows-lifecycle.json` degrades to
     "blocked" and the suite accepts that; `updater-handover.json` turns `UpdateHandoverTests` red —
     and **it was redone with the owner's permission**. The cycle is **versioned** now:
     `eng/run-sandbox-handover.ps1`, `eng/sandbox-handover.ps1` and
     `eng/measure-handover-with-handler.ps1`. Before, the document described the steps and the script
     lived outside the repository.
   - **Three defects in the sandbox harness, none of them named by any message**: PowerShell's `&`
     breaks the `.wsb` because it is **XML text** ("the configuration file is not valid"); closing the
     sandbox by killing `WindowsSandboxServer` — a host service — takes the next run down with it; and
     the window belongs to `WindowsSandboxRemoteSession`, not to a `WindowsSandboxClient`, which does
     not exist on this build.
   - **And a product finding, reproduced twice**: with nothing registered for `.msix`,
     `Process.Start` returns **null** and the application says Windows refused it — true: the database
     read 372,736 bytes before and after — **but Windows leaves the "choose an app" dialog on
     screen**. It is the mirror image of what the `withHandler` half rules out. Measured and named;
     **not touched**.
   - **Still open**: the five brand assets, which are the owner's.
5. **`DES-001` — the installation is seen too, and today it is undesigned.** The five assets in
   `src/ApSolutions.LocalMedia.Windows.Package/Assets/` are placeholders from 3 August — 576 B to
   7 KiB — and they are **the first thing anyone sees of the product**, before any view. And there is
   a measured defect: the manifest declares `es-ES` and `en-US` but its `Description` is **a single
   string with a slash inside it** ("Biblioteca y reproductor de vídeo local / Local video library
   and player"), which Windows shows exactly like that in both languages. Real localisation uses
   `ms-resource:` and one resource per language, as **winget already does** with its two
   `locale.es-ES.yaml` and `locale.en-US.yaml`. Note: touching the package means repackaging, or the
   packaging tests fail.
6. **The visual redesign**, which the owner is preparing with Claude Design. What this repository
   owes it is the inventory of **everything that is seen**, and it is written and measured in
   [docs/design/SURFACES.en.md](design/SURFACES.en.md) and [SURFACES.es.md](design/SURFACES.es.md):
   48 views across 15 areas, the 468 strings per language, the update view's 23 distinct messages —
   15 states and 8 refusal reasons, which are not user errors and ask for different treatment — and
   the five installation assets. **`LIB-016` adds new visible surface** (the automatic-refresh switch
   and its text), so the inventory does not close until that entry is done.
7. **Documentation**: `DOC-101`, `DOC-201`, `T44.1`-`T44.6` and the user manual, written from the
   built application rather than from the code — which is why it comes last, and why its screenshots
   depend on the redesign.

## Finished on 2026-08-15 (eighth session)

Four commits, and the chain that held `LIB-006` in `BLOCKED` is closed: the manifest reads **43
verified, 2 blocked**. `ApplyIdentification` writes what the provider knows through both of its
callers — the inbox and the automatic path above 90%, which did not exist — `RefreshMetadata`
resolves through the stored reference, the editor lost the property nobody filled, and the assembled
walk **presses the button with the mouse**. Detail in
[audit-apply-identification.md](evidence/stable/audit-apply-identification.md),
[audit-refresh-resolves-itself.md](evidence/stable/audit-refresh-resolves-itself.md) and
[audit-walk-clicks-the-editor.md](evidence/stable/audit-walk-clicks-the-editor.md).

**Two defects on nobody's list, both found by measuring**: the first save on an unedited entry
returned `NotFound` in silence — the editor turned that into neither a conflict nor a change — and
**no caller raised the revision when it saved**, so `WHERE revision = $expected` was comparing
against a number that never moved and two windows could both win.

## Finished on 2026-08-14 (sixth session)

- **`BUG-011`: one deferred-release queue for the whole process.** The playback engine kept the
  third, and disposed the native media **inside its lock and with no guard**: an exception there left
  the loop with the worker flag raised, so a single failure killed it for good and everything opened
  afterwards leaked in silence. The red was **deliberately double** — the source rule, which can be
  satisfied by moving text, and a behaviour test that measures from outside where the media rests
  once the engine lets go of it. `LibVlcFactory` gained a flush with a ceiling that **does not
  throw** when exhausted, the engine let go of queue, lock, flag, drain and quiescence constant
  (−52/+27), and the shrink-only list is now **empty**. The resistance test was not written from
  scratch: `HandleGrowthTests` already opened and closed the engine thirty times in a child process,
  which is the only place a process-wide counter can be read without the other suites writing into
  it. And that column **could not have been red before**, which the evidence says rather than
  presenting it as proof.
  [audit-bug011-engine-release-queue.md](evidence/stable/audit-bug011-engine-release-queue.md).

- **The audit queue was closed in full**: `ARQ-013`, `ARQ-012` (−836/+196 across 88 files), `ARQ-014`
  and `QA-001` — the last with **zero** violations to fix: a gate, not a debt. Plus two unrelated
  reds found on the way: the network canary asked for a port from a range the system reserves
  ([audit-canary-port.md](evidence/stable/audit-canary-port.md)) and a corpus test deleted a file
  another suite could be reading
  ([audit-corpus-shared-file-race.md](evidence/stable/audit-corpus-shared-file-race.md)).
- **And a red that would have surfaced at the first publication**: the signing tool **did not
  compile** — the licence header was missing and its project sat **outside the solution**, so no gate
  built it — and `release.yml` runs it with `dotnet run` at the step that verifies the signature.
  Fixed; the project is in the solution and a new rule stops another one from staying outside. **No
  gate of this repository found it**: the owner's IT session did, while running the signing key's
  restore check.
  [audit-release-signing-tool-build.md](evidence/stable/audit-release-signing-tool-build.md).

## The new block: synopsis and trailer (2026-08-14)

Asked for by the owner and **decided in full** in
[2026-08-14-synopsis-and-trailer.md](superpowers/plans/2026-08-14-synopsis-and-trailer.md). It runs
before the documentation on purpose: the manual is written from the built application, and writing it
before this block would mean writing it twice.

- **`LIB-013` done**: the synopsis is read on both cards. It was stored end to end already; the read
  path was all that was missing.
- **`LIB-014` done**: the **local** trailer plays from the card, in the convention Plex, Jellyfin and
  Kodi share. No new playback route was needed: `OpenLooseFile` already checks the extension and the
  file, and writes no catalogue row.
- **`LIB-015` pending**: the YouTube key to the browser. **It costs a migration** — a new field on
  `MetadataDetails` and a column — plus `append_to_response=videos` on the request already made. No
  new host.
- **`LIB-016` pending**: the automatic refresh, off by default. **It touches a privacy contract**:
  TMDB's declared purpose says "the metadata a person explicitly asked to identify or refresh", and
  an automatic refresh makes that untrue, so the text changes with the code. The cache's 180-day
  ceiling is never raised.

**The remote-trailer decision is not re-deliberated**: in-app only for a local file. Going through
LibVLC to YouTube breaks their terms, and the official embed would need a WebView with undeclared
hosts, advertising and cookies.

## Two things learned on 2026-08-14 that came dear

- **A mechanical replacement verified only by "it compiles" is an unmeasured change.** The script
  that migrated the 59 copies of the root finder removed the **wrong method in fourteen files**,
  because it matched the first method of the right shape instead of the one containing the walk. None
  reached a commit, and not by luck: the compiler caught two, **the new rule caught thirteen** — the
  very gate that work was adding measured its own damage — and the suites caught three more, which
  returned `…/src/…Presentation` rather than the root. What closed the gap was not waiting for
  something to fail but searching the diff for **every** return of a subdirectory.
- **An error about the harness hides the host's.** The network canary failed with an
  `ObjectDisposedException` in its own constructor, which says nothing. The first fix did not make
  the test pass: it made it **tell the truth** — "no free port in the range" — and that sentence was
  measured in one command: Windows reserves 50996-51095 and the test's fixed range sat entirely
  inside it. Not intermittent; the exclusions are assigned when the host boots.

## Yours (only what an agent cannot do)

- ~~Add the `RELEASE_SIGNING_SECRET_KEY` secret to the public repository.~~ **Done on 2026-08-10
  (22:46 UTC)**, and it was the only thing between the project and cutting its first public release:
  `release.yml` requires `SHA256SUMS.txt.minisig` to exist and verify, and stopped there. Confirmed by
  name and date with `gh secret list`, which never shows the value. The copy is still where you left
  it (see `SECURITY.md`), and **the encrypted backup is still the only net**: an Actions secret
  cannot be read back.
- The **manual ten-minute physical walk**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- The **encrypted backup** of the signing key. Destination and encryption are decided outside this
  repository (the IT vault); what matters here is how the copy is checked, and **it is not that the
  file decrypts**: sign something trivial with the restored copy and verify it against
  [`eng/release-signing.pub`](../eng/release-signing.pub). Repeat quarterly, because a corrupt backup
  does not announce itself.
  - **Done on 2026-08-14**: the owner's IT session ran the check that counts — restore, sign, verify
    — and the backup **works**. Proven by execution, which is the only way.
  - **And two warnings about how this gets checked.** From here the backup was measured by **size**
    against a threshold, and that measurement said "fails" when the truth was that it works: a proxy
    can sit exactly on its threshold and mean nothing. Then, from a 58-byte file, it was deduced that
    it could not be the key because it does not fit **minisign's file format** — and this project
    does not use that file format, only its verification. **Both times the mistake was the same**:
    substituting a deduction over metadata for an execution. The size of an encrypted file does not
    tell you its contents, and neither does a format nobody checked was in use.
  - Repeat the check **quarterly**, because a corrupt backup does not announce itself.
- **The export notification** to `crypt@bis.doc.gov` and `enc@nsa.gov`: the text is drafted in full in
  [LEGAL.en.md](legal/LEGAL.en.md) and goes from your identity, which is why it is yours.
- **The professional legal opinion** (`REL-004`), **narrowed on 2026-08-14**. Its two licence
  questions are closed by engineering rather than by opinion: instead of commissioning someone to
  interpret LGPL-2.1 §6, the release moved to the option both licences state **unconditionally** —
  §6(d) and the last paragraph of GPL-2.0 §3, offering the source from the same place the binary is
  downloaded from — and `release.yml` now attaches a verified `vlc-3.0.23.tar.xz` and the LibVLCSharp
  archive. **What is left of the engagement is trademark, domain and the export notification**, which
  is where outside judgement adds something.
  [audit-corresponding-source.md](evidence/stable/audit-corresponding-source.md).
- The usual economic decisions: Authenticode certificate, Store, ARM64 hardware.

## Things learned worth not learning twice

- **Verifying with the keyboard is not verifying with the mouse.** The assembled walk drove the
  application with `Window.KeyPress` and **nobody used** `Avalonia.Headless`'s clicks. The first
  click uncovered that the walk itself mounted the window in a way the application does not —
  `AssembledStartup.FinalContent` **lifts** the `ShellView` out of its container and the walk
  remounted it in a window of its own — leaving the shell **off the logical tree**. A `Button` only
  consults its command's `CanExecute` once it is on the logical tree, so **every** command-bound
  button reported itself disabled. Buttons wired with `Click=` did not, which is why it was invisible.
- **`Window.InputHitTest` does not predict where a click goes** in Avalonia headless: it named the
  `ScrollContentPresenter` while the click reached the button. The guard written to "make the click
  safe" was the only thing failing, and believing it would have declared something broken that works.
  Assert on the **effect**, with a click **beside** it as the control.

- **`eng/verify.ps1` is not what CI runs**: CI also runs
  `eng/run-accessibility.ps1 -Mode Verify -Passes 2` and `eng/run-recovery.ps1 -Mode Verify
  -Passes 2`, and they are worth running. But **beware the easy conclusion**: the red that reached
  `main` on 2026-08-10 appeared in **three different places** across three runs — pass 1, pass 2,
  and the suite inside `verify.ps1` — so no single gate was missing: **the race does not reproduce
  on this machine**. More passes are more rolls, not determinism. Against a race, what works is
  removing it, not hunting it.
- **Observing a transient state means waiting for it to end before leaving.** The test that checks
  the startup view asserts on something that lasts as long as the background work, and it left
  mid-way: the `Task.Run` still had the database open when the teardown deleted the folder. Here it
  finished in time and passed; on a slower runner it did not. The assertion does not need the wait,
  but the teardown does.
- **A baseline of one measurement is not a baseline.** The phase that had to be watched turned out
  to be the noisiest of the three — 1245 ms of variation on the same code — and the signal came from
  the two controls added "unnecessarily". Measuring the subject with nothing to compare it against
  produces a number that cannot tell a change from the weather.
- **A inherited number is re-measured even when it is taken for granted that it improved.** Of
  TST-001's three, two were exactly where they were left and the third had **gone backwards**.
- **A premise written in your own document has to be measured too.** "The first launch is the only
  one that migrates" was in two places and was false; counting `schema_history` in each folder takes
  a minute and ruled out the candidate cause of the one open red.
- **A test that fails because the correction worked is not fixed in the code.** The accessible-name
  one lost a race against the asynchronous startup itself: by the time it looked, the shell had
  already taken over. What gets re-aimed there is the test.

- **Before instrumenting a failure, read the whole of it where it was archived.** Two documents
  recorded the intermittent red's two hypotheses as indistinguishable, and that run's line —
  `16 migration(s) applied to a new database` — ruled one of them out from day one. It cost one
  filtered `gh run view --log`. A log nobody has read is not an open question.
- **A gap list written by reading the code is a hypothesis.** The one for the missing branches in
  `ReconcileScannedFiles` shrank by **a third** when measured, and the gap that was not on it — a
  property no test ever read — was the one no reading could give, because record equality goes
  through fields.
- **The coverage gate reads from `HEAD`, not from disk.** With new files only staged it announces "no
  new file" and exits green. Commit and re-run `eng/check-coverage.ps1` **before** pushing, or CI will
  be the one to find the red.
- **A finding filed as "not exploitable" is a finding that was never measured.** Of the two the audit
  left noted, one was a direct hand-off to the Windows shell and the other did not exist. Neither was
  known until the test that would have exploited it was written.
- **A test that passes before the fix is not good news**, it is the hypothesis announcing it was
  wrong. That is where to stop and measure again.
- **`eng/verify-package.ps1` compares two clean checkouts**, so it refuses to run with unstaged files.
  A `git add -A` before `eng/verify.ps1` saves half an hour.
- **A class comes out when its tests can follow it**, and the coverage gate decides that, not
  intuition. `WindowLifecycle` compiled and the assembled walks were green; it went back anyway, like
  `WindowsFilePickers` before it.
- **The tests that read the composition as text break every time something moves.** Three times now.
  When code leaves `CompositionRoot`, updating `CompositionSourceText` and `CompositionGraph` is part
  of the move, not a fix afterwards.
- **`AppDomain.UnhandledException` does not stop the process from ending**, it only records. That
  inverted the order of ARQ-004's two halves: if a command cannot afford to let anything escape it
  must always catch, and something that always catches always needs somewhere to put it — so the
  somewhere comes first.
- **Before writing "asynchronous", check that something yields the thread.**
  `Microsoft.Data.Sqlite` implements much of its `Async` surface synchronously. An `await` on
  something that never yields leaves the thread just as blocked **while looking fixed**, which is
  worse than leaving it alone.
- **Replacing N classes with one assumes the N did the same thing, and they did not.** Two of
  twenty-four held behaviour of their own, and the suite caught it, not the reading: one checked
  `CanExecute` inside `Execute` and a real validation hung off that. Never migrate without running the
  whole suite.
- **Inside an `async void`, a guard clause is not a guard**: it throws into the state machine and is
  posted to the context, so the caller that got it wrong never hears. Split the method into a
  synchronous one that checks and another that waits.
- **Some reds are not reds, they are hangs.** A lock held by a thread that never returns produces a
  suite that never ends, not a broken assertion. There is no "archived red" there, and the evidence
  says so rather than inventing one.
