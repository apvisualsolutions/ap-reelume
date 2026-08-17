# Where to resume

## The queue decided on 2026-08-16 (not up for re-deliberation)

**The destination is zero.** This application ships free and **nobody is going to test it by hand**:
whatever the suite does not cover, nothing covers. The ratchet in `eng/check-walk-coverage.ps1` goes
to **0 pending** — **34** today, with **94 of 128** controls pressed by mouse — and the code coverage
gate goes to watching the whole tree. Everything below is **decided**; what remains is carrying it out,
measuring before correcting.

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

5b. **Batch 6b — Cancel (1).** **40 → 39.** What is left of batch 6, and it may wait behind batch 7:

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
7c. **The updater's Cancel (1). 34 → 33.** All that is left of 7a, apart and with its measured
   reason: it carries `IsEnabled="{Binding IsBusy}"` **and** `CanExecute => IsBusy`, and with the
   package on disk beside it the whole download finishes in **milliseconds**. The advantage over 6b
   stands — the source belongs to the harness and **can serve slowly on purpose** — so the manifest
   has to be able to declare a wait, and it has to be **measured before** the scene is written.

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
   **eleven since 2026-08-17**, with `AppDataPaths.cs`, `ShellExternalLinkLauncher.cs`,
   `HandoffArchivePicker.cs`, `WindowsSystemHandoff.cs`, `RecordingSystemHandoff.cs`,
   `HandoffUpdateSource.cs` and `HandoffUpdateManifest.cs` at 100/100 because they are the **exits**
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
works, because the view becomes visible and the button asks again. **Decided**: leave them for now —
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
the matrix **when the manifest is regenerated against a freshly built package** — there are thirteen now,
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
