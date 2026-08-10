# Where to pick up

The state of the project at the close of the second session of **2026-08-10**, the one that settled
the legal debt. The Spanish version is in [NEXT-SESSION.es.md](NEXT-SESSION.es.md). The canonical
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

## What this session finished

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

## What comes next (in this order, and already decided)

The ARQ-010 → ARQ-004 → ARQ-005 queue was run on 2026-08-10. One half and the debt are left, and the
design of both is decided in the plan: **it does not need re-deliberating, it needs running**.

1. **The baseline, which is half an hour and pays for the whole task after it.** Have
   `eng/verify-package.ps1`'s `first-launch` phase report **the time until the window appears**, not
   just a yes or a no. It turns the intermittent red below into a comparable series, and the same
   number before and after point 2 is the proof that it fixed it. That is *why* it goes first.
2. **ARQ-005, second half: the asynchronous startup.** `FinishShell` blocks the interface thread to
   migrate the database and — only when that migration rewrote the file — to check its integrity. The
   other four `GetAwaiter().GetResult()` calls in `CompositionRoot` are diagnostics-report reads on
   demand, and the one in `Program.cs` is `Main`'s `finally`, which is legitimate.

   **A measurement comes first, and it is not negotiable.** `MigrateAsync` is written with real
   `await`s, but that does not mean it yields the thread: `Microsoft.Data.Sqlite` implements much of
   its `Async` surface synchronously, because SQLite has no asynchronous I/O. If it does not yield,
   swapping `GetAwaiter().GetResult()` for `await` leaves the window **just as blocked while looking
   fixed**, which is worse than leaving it alone. Measure it by timing whether the calling thread is
   free while it migrates; if it does not yield, the work goes to `Task.Run`.
   `SqliteConnectionFactory` opens a connection per call and guards its configuration with a
   semaphore, so it supports the move.

   **The shape is decided**: `FinishShell` returns a `ContentControl` holding the startup view; `App`
   already sets that control as the window's `Content`, so **the window appears on the first frame
   without touching `App` or `ConfigureWindow`**. When the work ends the content is swapped for the
   shell or the recovery view — which one is the same decision as today, only its timing changes. The
   work's failure goes through `GuardedEvent`, which already exists. The view carries the product name
   and a line of status, **with no indeterminate progress bar**: nobody knows how much is left, and a
   bar moving without meaning anything is a visual lie. Strings in both languages and its automation
   name.

   **Only two sites call `CreateShell()`**, both in assembled walks and both asserting
   `Assert.IsType<ShellView>`. They move to waiting for the final content with a ceiling, and their
   failure message has to **name what was left in its place** — startup or recovery — because a
   ceiling that only says "it never arrived" diagnoses nothing.
3. **The coverage debt, and the guard it is missing.** The three are `ReconcileScannedFiles`,
   `CompositeFileIdentityProvider` and `PlayerVersionsViewModel`.
   [The document's numbers are stale](evidence/stable/TST1-coverage-gate.md): an approximate
   measurement on 2026-08-10 puts them considerably better on lines and still thin on branches, and
   `PlayerVersionsViewModel` **got smaller** when it lost its command class in ARQ-004. Re-measure
   with `eng/check-coverage.ps1` before anything else, and start with `PlayerVersionsViewModel`, the
   one ARQ-004 has just touched.

   **And what actually matters here**: the gate measures **only files that are new by content**
   against `origin/main`, so these three, being old, **are watched by nobody**. Settling the debt
   without closing that does not stop it coming back tomorrow. On settling it, `check-coverage.ps1`
   gets an explicit watch-list measured every time, under the same rule as
   `ServiceConsumptionTests`'s orphan list: **it can only shrink**.

## An intermittent red to watch, not to paper over

On 2026-08-10 the `first-launch` phase of `verify-package.ps1` **failed once** on the branch and
**passed on the same commit** on `main`. Same code, same workflow, different outcome: it is
intermittent, which is why it is not a defect anybody can find by reading.

What is known, measured from the log:

- The phase took **137 s**, which is exactly the window deadline (90 s) plus the close deadline
  (45 s). Both ran out and the process was killed, hence the `exit code -1`.
- In **that same run**, `repair`, `downgrade-refused`, `open-with` and the four `windows-*` phases
  started the application and watched it paint. Only the first launch failed.
- The first launch is the only one that actually **migrates** — sixteen migrations against a new
  database — and migrating **blocks the interface thread**. That is the candidate cause, and it is
  exactly what is left of ARQ-005.
- **Observed frequency: one in four.** It did not recur across the three following runs carrying that
  same code. That is the figure to compare against if it is seen again.

**What is not done**: raising the 90 s deadline. That turns the only signal there is into silence,
which is the mistake the media generator's `cancelled` runs already charged six runs for. If it comes
back, it stops being pending work and becomes the urgent fix.

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

## Yours (only what an agent cannot do)

- **Add the `RELEASE_SIGNING_SECRET_KEY` secret to the public repository.** It could not be copied —
  secrets cannot be read — and **without it the publishing pipeline fails on purpose**: `release.yml`
  checks that `SHA256SUMS.txt.minisig` exists and verifies, and stops if it does not. It is the only
  thing between the project and cutting its first public release. The copy is where you left it (see
  `SECURITY.md`).
- The **manual ten-minute physical walk**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- The **encrypted backup** of the signing key.
- **The export notification** to `crypt@bis.doc.gov` and `enc@nsa.gov`: the text is drafted in full in
  [LEGAL.en.md](legal/LEGAL.en.md) and goes from your identity, which is why it is yours.
- **The professional legal opinion** (`REL-004`). Two concrete licence questions are left for it, and
  both are about form rather than delivery: which subsection of LGPL-2.1 §6 covers the way LibVLC
  travels here, and whether the written offer of corresponding source recorded in
  `NOTICE-VideoLAN.txt` is enough as the accompaniment GPL-2.0 §3 asks for.
- The usual economic decisions: Authenticode certificate, Store, ARM64 hardware.

## Things learned worth not learning twice

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
