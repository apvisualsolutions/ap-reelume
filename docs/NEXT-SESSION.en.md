# Where to pick up — 2026-09-25 (afternoon closing)

> **Overwritten** at every closing, capped at 80 lines and 6 KB per language; `eng/check-handoff.ps1`
> measures it. History up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree outranks this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything.

## State

`main` and the branch were left on the same commit, the last one with green CI, read twice: by the
watcher and by `gh run list --commit`. Check it with `git log --oneline -1 main`; the SHA is not
written here. Only this handoff is ahead. It is documentation, and its CI was not waited for, so read
it before moving `main`.

Today's two earlier runs were **red on coverage only, and because of an improvement**: all eleven
suites passed and the gate asked for two floors to rise. The third, with the floors copied from the
artifact, was green: 184 of 184 in the debt and 23 pending in the walk.

## What was done

· **`ENG-020`**: closing the application after watching a video no longer ends on an exception. The
  host lets go of the tray icon before the teardown's first wait, and releasing it from another
  thread hands the work to its own thread instead of throwing. Two reds archived, two mutants killed.
· **The audit of `ENG-018`'s tests**: `gate-auditor` found two blind gates and four weak ones. All six
  are fixed, and each now kills the mutant that used to fool it. The worst was the walk's clock
  scene, which passed with the clock unplugged.
· Two coverage floors rise: the tray to 97/64 and the window wiring to 90/66.
· **`ENG-051`** is born.

## The measured traps

· **Closing only changes thread after something has played**: the player waits for its media to
  rest, and no test yielded. The host test provokes that wait and requires that it happens.
· **`isolation: worktree` does not work with the repository on the NAS**: git refuses it over
  ownership. Make the copy by hand in the scratchpad and use it with `git -c safe.directory=*`,
  without touching the global config. Details in the project's drawer.
· **In the Bash tool, `dotnet` cannot find the SDK**: a mutant loop gave five empty outputs. Run the
  tests from PowerShell.
· **`ENG-051` knocks the coverage preview over**, so it did not announce the two improvements; CI
  confirmed them with a red.

## First thing next session

· Read this handoff's CI and, if green, move `main`.
· The first takeable row in `TAREAS.md`: **`ENG-022`**, the edge-directed upscaler. It is design work:
  measure the options and their cost with `UpscaleCostPolicy` before writing the shader.

## Waiting on the owner

· **Try closing after a video by hand** (`ENG-020`): exit code 82 does not come from this tree, and
  only the real host can answer it.
· **`ENG-049`**: the video changes size as the controls come and go. Decide it by watching it.
· **Try `ENG-018` by hand**, **`ENG-002`** (Narrator) and **`ENG-005`** (diagnostic window).
· Blocked on something that is not code: **`PRD-002`** (signing certificate) and **`PRD-003`** (ARM64).

## What is pending is not here

`pwsh -NoProfile -File eng/list-pending.ps1` answers it: **24 open out of 75**. Scope lives in
`FEATURES.md`; chores, gates, debt and unmeasured questions in `TAREAS.md`, oldest first. Today
`ENG-020` closed and `ENG-051` came in.
