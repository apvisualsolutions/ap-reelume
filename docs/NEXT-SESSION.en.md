# Where to pick up — 2026-09-19

> It is **overwritten** at every close and stays under 80 lines and 6 KB per language, measured by
> `eng/check-handoff.ps1`. The history up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree outranks this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything else.

## State

The repository adopts the house's common working system on the **local** branch
`codex/adopt-ap-smart-tech`, two commits ahead of the working branch. **No push, no PR and no
merge**: the local diff waits for the owner's review. `main` and the working branch were not touched
and stay as the previous close left them, with their CI green.

## What was done

· **Manifest** (`.claude/project-manifest.json`): measured backlog, the nine closing phases with
  their command, a CI receipt and twelve gates, each with a case that sounds and one that stays quiet.
· **`ENG-033`**: the closing marker lives in git's common directory and the plugin's two gates read
  it. The repository's own push guard was retired in the same commit, after its battery passed.
· **`ENG-034`**: `.gitignore` excludes the local settings, measured with the global config off.
· **`ENG-035`**: the handover is overwritten, capped and in parity; history is in `NEXT-SESSION-HISTORY`.
· **`ENG-036`**: the shared-tools variable lives in the local settings.
· **`ENG-037`**: the closing record measures IT's checks by the file they leave, not by the text.
· Evidence: `docs/evidence/stable/audit-adopt-common-system.md`.

## The traps measured

· Freezing a document with `git mv` and writing another in its place is not a rename to git: the
  privacy filter saw 16,000 new lines. With `--find-copies-harder`, fourteen.
· The common filter reads a regular expression's double backslash as a network path: the manifest
  sounds in every close that touches it.

## What waits for the owner

· Review the adoption branch's local diff and decide whether to merge it.
· Remove the dead pattern written with a backslash from the global git configuration.
· Seven findings about the common system, for the IT session; they are in the evidence.

## What is still open does not live here

Read it with `pwsh -NoProfile -File eng/list-pending.ps1` and in [TAREAS.md](TAREAS.md).
