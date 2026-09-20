# Where to pick up — 2026-09-20 (afternoon)

> **Overwritten** at every close, and never above 80 lines or 6 KB per language; measured by
> `eng/check-handoff.ps1`. The history up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree beats this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything.

## State

`main` is up to date with the `ENG-009` commit, its CI read green before the fast-forward.

**The branch is ahead with `ENG-044` pushed and its CI running, unread.** It went to the branch and
not to `main`, which is where a red does not get in the way. **And that run is expected to come back
red**, with the exact cause already measured here: see the first bullet below. `main` does not move
until it is read.

## What was done

· **`ENG-009` closed.** The document sweep no longer reads other sessions' checkouts, so running
  `gate-auditor` in a worktree stops turning `EvidenceLinkTests` red. It was the **third** time that
  exclusion had been written by hand, in two different spellings; the rule now lives in
  `RepositoryLayout.IsInsideAnotherCheckout`, with both of its lessons inside.
· **`ENG-043` closed by the Product Owner's decision**: what was published is accepted, with no
  rename and no history rewrite, consistent with `ENG-032`. The reasoning is written into the row so
  it does not get reopened.
· **`ENG-044` opened and half built** — born while measuring `ENG-010`, which is its symptom. Below.
· `LIB-003` lowered from `VERIFIED` to `IMPLEMENTED`, with its blocker declared in the manifest.

## The traps measured

· **`LIB-003` promised continuous watching and never switched on.** The only thing activating it was
  `ScanPolicy.Continuous`, and **nothing in `src/` assigned it**: the three ways of adding a root
  give `Startup | Manual` or `Manual`, and no screen offers the choice. Negative control done: the
  same grep does find the other two flags.
· **Why no gate saw it: the tests hand themselves the flag.** Twelve places in `tests/` set it and
  zero in `src/`. They all measure that watching works **when it is on**; none that it ever comes
  on. The missing guard is not another test of the watcher.
· **A test that already existed corrected the design**, and it was the most valuable thing of the
  batch. `A_manual_root_is_not_watched_behind_its_owners_back` forced the setting to reach only the
  roots carrying `Startup`: `DeclareCourseFolder` gives plain `Manual` **on purpose**, because the
  dialog promises the rest of the drive is left alone.
· **The CI watcher needs the forty-character SHA.** It was armed once with a SHA invented from the
  short one; `gh` answers `[]` and that reads exactly like «no run yet». Resolve it with
  `git rev-parse HEAD`, never by hand.
· **A new row in `docs/TAREAS.md` goes in its place by identifier, not at the end of the done ones.**
  `TareasRegisterTests` requires it and said so.

## First thing next session

· **Push the local `ENG-044` commit and read its CI.** Every local gate passed — format, build,
  `Domain` 879, `Application` 393, `Architecture` 55, `Documentation` 118 and `Integration` 683 —
  but only CI verifies for real. **And that run will come back red with «1 improved», measured here
  before pushing**: `RootWatchCoordinator.cs` rises from **96/89 to 97/90** because the new tests
  walk through it. That is not a defect: download that run's `coverage-debt` artefact and raise its
  row in the same change, never editing the file by hand. No new file falls short.
· **Finish `ENG-044`, which also closes `ENG-010`**: make the two Settings controls govern the
  setting and store it, the assembly guard — a root created the real way ends up watched —, reread
  `audit-wp2-assembly.md`, and decide the interval (the control says 30 and the code uses 15).
· **`ENG-041` before raising coverage**: two written rules about how that gate counts contradict
  each other, and the row says how to measure it.

## What waits for the Product Owner

· **`ENG-042`**: the language check flags 31 places in living files, **all of them older than this
  batch** — the only one it introduced was corrected before committing. Whether the manufacturer's
  term is deliberate in the video engine's legal documents has to be decided before the sweep.
· **`ENG-002`** (a session with the Narrator) and **`ENG-005`** (opening a window when he is not
  working).
· The IT session will publish the new version of the shared system; when it says so, update and
  repeat its checks, using the names held in the local settings.
· The IT session will migrate this repository to the NAS once told «ready to migrate». Measured
  today: **zero live worktrees**, and the two empty shells already removed.

## What is pending is not here

Read it with `pwsh -NoProfile -File eng/list-pending.ps1` and in [TAREAS.md](TAREAS.md). Today: 25
open of 75.
