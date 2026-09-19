# Where to pick up — 2026-09-20

> It is **overwritten** at every close and stays under 80 lines and 6 KB per language, measured by
> `eng/check-handoff.ps1`. The history up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree outranks this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything else.

## State

`main` and `codex/ap-reelume-mvp-x64` were left on the same commit, with their CI read green. This
handover is one commit ahead, being documentation only, and its CI is not waited for.

## What was done

· **The gate auditor over what the adoption brought**, and thirteen checks passed without catching
  the defect they were meant to catch. Each now has its mutant, seen surviving before and dying
  after. `ENG-038` closed: the closing record stored the list of escapes wrongly.
· **The frame of the video itself now reaches the grid** (`LIB-021`, plan 2 of 3): the pass runs when
  the window opens and after every scan, and the card changes its picture instead of the grid being
  rebuilt, which is what made it dangerous for the walk.
· `CatalogItemViewModel` leaves the coverage debt at 100/100 and the ratchet drops to **185**.
· Evidence: `audit-adoption-gates.md` and `LIB021-cover-origins-frame.md`.

## The traps measured

· **The coverage gate ADDS the branches of each suite**, it does not take «covered anywhere»: half a
  branch here and half there are two of four. It cost one CI red.
· **The floor preview stays silent about a new file that is not committed yet** (`ENG-016`), so it
  did not warn that the new file measured 100/50. It cost the other red.
· In PowerShell, an `if` used as a value unrolls its output: an empty list comes out as `null` and a
  list of one comes out loose. That was `ENG-038`'s defect, and the same pattern is in the common
  plugin.

## First thing next session

· `docs/TAREAS.md`, the first open row that is not stopped.
· Plan 3 of `LIB-021` — the cover order setting with its «Restore defaults» and the per-title
  override — closes `ENG-003`, open since 2026-09-05.

## What waits for the owner

· The IT session publishes **0.10.0** of the common system today; once they say so, the metrics
  register migrates to JSON Lines with `adopt-enrol` and ping, `prove` and the doctor are run again.
· `ENG-002` (a session with Narrator) and the rest of theirs, as it stands in `docs/TAREAS.md`.

## What is still open does not live here

Read it with `pwsh -NoProfile -File eng/list-pending.ps1` and in [TAREAS.md](TAREAS.md).
