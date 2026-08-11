# Where to pick up

The state of the project at the close of the **fifth** session of **2026-08-10**, the one that paid
the last coverage debt and instrumented the intermittent red's failure path. The Spanish version is in [NEXT-SESSION.es.md](NEXT-SESSION.es.md). The canonical
scope record is still [FEATURES.md](FEATURES.md); the audit's outstanding work lives in
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

1. **`BUG-011`**, which came out of doing `BUG-010` and inherits its place:
   `LibVlcMediaPlayerEngine` keeps the **third** deferred-release queue, with the same unguarded
   dispose. It is not the same change: its `DisposeAsync` awaits its own drain **before** releasing
   the player, and that order is what keeps the native teardown from crashing, so unifying it asks
   `LibVlcFactory` to be able to flush on request. It sits on the shrink-only list in
   `NativeInstanceOwnershipTests`, in view on every run.
   Decided: `LibVlcFactory` gains a flush on request **with a ceiling** that does not throw when it
   runs out, the engine changes two ordering lines, and the 1 s quiescence window **stays put**.
2. **`ARQ-013`**, the reachability gate that believes a comment: a commented-out reference counts as
   reached, so the orphan surface that test exists to catch hides behind `<!-- -->`. The most
   valuable of the three small ones, because the defect is **in a gate**. Decided: strip comments
   before matching, and **the red first**.
3. **`ARQ-014`**, the User-Agent announcing `1.0` while the declared version is `0.1.0`: the brand
   stays and the version comes from the assembly, pinned against `Directory.Build.props`'s
   `<Version>`.
4. **`ARQ-012`**, one repository root and one anchor: there are **two** today — `docs/FEATURES.md`
   and the `.sln` — plus a dozen copies of the same `while`. Decided: the `.sln` is the anchor, one
   shared file in `tests/Shared/`, and a shrink-only rule.
5. **`QA-001`**, culture: **no hand-rolled regex** — turn `CA1305`/`CA1304`/`CA1310` into errors.
   Count the warnings per project first; when fixing, invariant for what is stored or sent, interface
   culture for what a person reads.
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

## Yours (only what an agent cannot do)

- ~~Add the `RELEASE_SIGNING_SECRET_KEY` secret to the public repository.~~ **Done on 2026-08-10
  (22:46 UTC)**, and it was the only thing between the project and cutting its first public release:
  `release.yml` requires `SHA256SUMS.txt.minisig` to exist and verify, and stopped there. Confirmed by
  name and date with `gh secret list`, which never shows the value. The copy is still where you left
  it (see `SECURITY.md`), and **the encrypted backup is still the only net**: an Actions secret
  cannot be read back.
- The **manual ten-minute physical walk**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- The **encrypted backup** of the signing key, which **does not exist today**: measured on
  2026-08-10, the local file is the **only** copy and no backup reaches it. Destination and
  encryption are decided outside this repository (the IT vault); what matters here is how the copy
  is checked, and it is not that the file decrypts: sign something trivial with the restored copy
  and verify it against [`eng/release-signing.pub`](../eng/release-signing.pub). Repeat that check
  quarterly, because a corrupt backup does not announce itself.
- **The export notification** to `crypt@bis.doc.gov` and `enc@nsa.gov`: the text is drafted in full in
  [LEGAL.en.md](legal/LEGAL.en.md) and goes from your identity, which is why it is yours.
- **The professional legal opinion** (`REL-004`). Two concrete licence questions are left for it, and
  both are about form rather than delivery: which subsection of LGPL-2.1 §6 covers the way LibVLC
  travels here, and whether the written offer of corresponding source recorded in
  `NOTICE-VideoLAN.txt` is enough as the accompaniment GPL-2.0 §3 asks for.
- The usual economic decisions: Authenticode certificate, Store, ARM64 hardware.

## Things learned worth not learning twice

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
