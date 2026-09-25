# Where to pick up — 2026-09-25 (closing)

> **Overwritten** at every closing, capped at 80 lines and 6 KB per language; `eng/check-handoff.ps1`
> measures it. History up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree outranks this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything.

## State

`main` and the branch are level, on the same commit. Read it with `git log --oneline -1 main` — the
sha is deliberately not written here, because the commit that writes it changes it.

Two runs today: the first **red** on coverage and the second **green**, read twice — through the
watcher and through `gh run list --commit`. The fast-forward was made on the green, with 184/184 in
the debt list and 23 pending in the walk.

## What was done

· **`ENG-018`**: the player's controls leave by themselves after 3 s while the film plays, the
  pointer with them, a click on the picture pauses, the wheel moves the volume and Escape steps back
  one layer. The decision that forbade it was reopened in `ADR-0014`, with the clock as a port and
  the walk at infinite except for one scene that lowers it to 200 ms. Twelve mutants killed.
· **`ShellView.axaml.cs` leaves the coverage debt** (100/97) and the ratchet drops to 184.
· `ENG-002` and `ENG-005` become `DEL PROPIETARIO`: the previous handover skipped the ordering rule,
  and `ENG-018` was the oldest takeable row.

## The measured traps

· **A new gesture turns the picture into a control for the walk**: its "click beside" paused it and
  took five scenes down. A hit test with two caveats fixes it, written in the project's drawer.
· **Tunnelled Escape closed the gear when closing a drop-down.** Fixing it by the event's source did
  not work: the key comes from whatever holds focus. The good test raises it on the player.
· **The coverage preview cannot see a file that was at the bar and falls**: that is how
  `PlayerView.axaml.cs`'s red reached CI (`ENG-050`).
· A constant-condition mutant does not compile here (`CS0162`); a compile error is not a killed
  mutant.

## First thing next session

· **`gate-auditor` was NOT run over this batch's tests** (`ChromeIdleTests`, `ShellEscapeTests`,
  `ShellViewEdgeTests`, the new `PlayerViewInputTests` and the walk scene). First step, in an
  isolated worktree; its copy makes `EvidenceLinkTests` red while it exists.
· Then the first takeable row of `TAREAS.md`: **`ENG-020`**, closing the application throws and
  exits with code 82.

## Waiting on the owner

· **`ENG-049`** — with the film in the window, the picture resizes every time the controls appear or
  leave. It is measured with him watching before deciding whether they should float.
· **Try `ENG-018` by hand**: nobody opened the application, so as not to take his screen.
· **`ENG-002`** (a session with Narrator) and **`ENG-005`** (a diagnostic window when he is not
  working).
· Blocked on something that is not code: **`PRD-002`** (commercial signing certificate) and
  **`PRD-003`** (full ARM64).

## What is pending is not here

`pwsh -NoProfile -File eng/list-pending.ps1` answers it: **24 open of 75**, 21 of them work and 3
standing decisions not to build something. Scope lives in `FEATURES.md`; chores, gates, debt and
unmeasured questions in `TAREAS.md`, oldest at the top — `ENG-049` and `ENG-050` were added today.
