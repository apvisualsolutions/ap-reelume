# Where to pick up — 2026-09-25 (night closing)

> **Overwritten** at every closing and never over 80 lines or 6 KB per language; measured by
> `eng/check-handoff.ps1`. The history up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree outranks this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything else.

## State

`main` and the branch were left on the same commit, the last one with green CI, read by the watcher
and by `gh run list --commit`. Check it with `git log --oneline -1 main`; the SHA is not written here.
Only this handoff is ahead, which is documentation and whose CI was not waited for: look at it before
moving `main`.

The run of the `ENG-022` commit came out **red on a walk test that was not its own**: its only change
in `src/` was comment lines, the test passes three out of three here and passed on the next run. It
is recorded as the third appearance of `ENG-026`, and that next run came out green throughout.

## What was done

· **`ENG-022`, closed as decided and not built.** Seven families measured against the synthesised
  truth, through a harness that reproduces the player's figures exactly. The best candidate, a kernel
  steered along the edge, removes the step (0 against 16) with the same ramp, but lands at 34.9 %
  against 35.7 % and costs six times more in software. `EdgeDirectedUpscaleCandidateTests` guards the
  decision; `gate-auditor` found a band that was too wide, now narrowed.
· **A written claim fell**: there was no 13 % ceiling without a step. Corrected in the code, in the
  `PLY-016` evidence and in the backlog; the new evidence is linked from the matrix.
· **`ENG-052`** and **`ENG-053`** are born (a posters red under coverage, message not captured), and `ENG-026` gets its third appearance.

## The measured traps

· **Bounding to samples that were already resampled does not remove the ring**: the ring is already
  inside the range. The bound has to be over the texels the decoder produced.
· **Zero variation is the instrument**: the first sweep gave identical figures at three strengths
  because a text substitution did not match on line endings and the shader never read the parameter.
· **`gh run view --log-failed` returns nothing while the run is still going**: the workflow has a
  single job, so a failed step can only be read once it ends.
· **A commit message is not piped in from PowerShell**: `git commit -F -` with a here-string took it
  as a path. It is written to a scratchpad file.

## First thing next session

· Read this handoff's CI and, if green, move `main`.
· The first takeable row in `TAREAS.md`: **`ENG-023`**, dithering the tone curve. It needs a technical
  decision before code: what happens when upscaling is off, because dithering has to come after the
  enlargement and today only the shader runs there.

## Waiting on the owner

· **Test closing after watching a video by hand** (`ENG-020`): exit code 82 does not come out of this
  tree and only the real host answers it.
· **`ENG-049`** — the video resizes as the controls appear and hide; decided by looking at it.
· **Test `ENG-018`**, **`ENG-002`** (Narrator) and **`ENG-005`** (diagnostic window) by hand.
· Blocked by something that is not code: **`PRD-002`** (signing certificate) and **`PRD-003`** (ARM64).

## What is pending is not here

`pwsh -NoProfile -File eng/list-pending.ps1` answers it: **24 open out of 75**. Scope lives in
`FEATURES.md`; chores, gates, debt and unmeasured questions in `TAREAS.md`, oldest on top. Today
`ENG-022` closed and `ENG-052` and `ENG-053` came in.
