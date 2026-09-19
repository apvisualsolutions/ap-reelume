# Where to pick up — 2026-09-19

> It is **overwritten** at every close and stays under 80 lines and 6 KB per language, measured by
> `eng/check-handoff.ps1`. The history up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree outranks this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything else.

## State

`main` and `codex/ap-reelume-mvp-x64` were left on the same commit, with their CI read green. This
handover is one commit ahead, being documentation only, and its CI is not waited for.

## What was done

· **The repository adopted the house's common working system** (the IT session's pilot). The
  manifest is in `.claude/project-manifest.json`, with twelve gates proved in both their cases.
· Closed `ENG-033` to `ENG-037`: the closing marker, in git's common directory; `.gitignore`
  ignores the local settings; the handover is overwritten under a cap; the shared-tools variable
  lives in the local settings; and the closing record measures by effect.
· The repository's own hook `pre-push-closing.sh` was retired. The plugin's two gates and the
  record's step-0 row replace it. **This close is the first one with the common system.**
· Evidence: `docs/evidence/stable/audit-adopt-common-system.md`.

## The traps measured

· Freezing a document with `git mv` and writing another in its place is not a rename to git: the
  privacy filter saw 16,000 new lines. With `--find-copies-harder`, fourteen.
· The common filter reads a regular expression's double backslash as a network path: the manifest
  sounds in every close that touches it. Writing one in prose sets it off too.

## First thing next session

· **The gate auditor** over the adoption's new tests: `HandoffLimitsTests`, the record's battery
  and `gate-probe.ps1`. It was not run before closing.
· Then `docs/TAREAS.md`, the first open row that is not stopped.

## What waits for the owner

· Seven findings about the common system, with the IT session, which received and read them.
· `ENG-002` (a session with Narrator) and the rest of theirs, as it stood in `docs/TAREAS.md`.

## What is still open does not live here

Read it with `pwsh -NoProfile -File eng/list-pending.ps1` and in [TAREAS.md](TAREAS.md).
