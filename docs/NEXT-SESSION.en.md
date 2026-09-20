# Where to pick up — 2026-09-20

> It is **overwritten** at every close and stays under 80 lines and 6 KB per language;
> `eng/check-handoff.ps1` measures it. The history up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree beats this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything.

## State

`main` and `codex/ap-reelume-mvp-x64` were left level, and every fast-forward was made with its CI
read green. This handoff is one commit ahead, being documentation only, and its CI is not waited for.

## What was done

· **`LIB-021` whole, and with it `ENG-003`**, open since 2026-09-05. The cover order is changed in a
  settings section of its own — two buttons over a list, with its «Restore default values» — and a
  title can override it from its editor, with a row of four options.
· **Migration 25** (`cover_order`), storing **the whole order** and not the winning origin: moving
  the general order later cannot change what that title was told to do.
· `MetadataFieldChanges.CoverOrder` carries **two sentinels** — `null` leaves the override alone, an
  empty list removes it — which is the hole `PersonalCover` still has.
· Debt ratchet **185 → 186** for the new view. `MetadataEditorViewModel.cs`'s floor **rises** to 95
  by improvement, and `CatalogRepository.cs`'s **did not fall**: the branch the new column brought
  was covered.
· Amendment to `ADR-0009`, and evidence `LIB021-cover-order-setting.md`.

## The traps measured

· **A migration moves FIVE schema assertions, not three.** The three written down — count, maximum
  and name list — left a fourth test red: `Migration_is_idempotent_...` counts **one backup per
  migration** and counts the history again.
· **A drop-down is a control the autonomous walk cannot click**, because nothing inside a popup is
  reachable, and its ratchet only shrinks. Seven gates refused that control from seven different
  places and **none had to be loosened**; the right shape is the row of radio options the audio
  device list already used.
· **A `Test Case Cleanup Failure` in `UiTests` is not the code**: it is the harness. It took down a
  commit touching only the two handoffs. Third appearance, now with a row (`ENG-040`).

## First thing next session

· `docs/TAREAS.md`, the first open one not stopped. `ENG-002` and `ENG-005` need the owner (a real
  screen reader, and opening a window), so the first that can be taken is **`ENG-009`**: running
  `gate-auditor` in a worktree turns `EvidenceLinkTests` red, since it treats its copies as project
  documents. `.claude/worktrees/` has to be excluded from the sweep.
· **`ENG-041` before raising coverage**: two written rules about how that gate counts contradict each
  other, and the row says how to measure it.

## What waits for the owner

· The IT session publishes the common system's **0.10.0**; when it says so, update the plugin, check
  that it answers, migrate its metrics log to JSON Lines and repeat its checks. Those tools' names
  are **not written here**: this repository is public.
· `ENG-002` (a session with Narrator), `ENG-005` (opening a window when he is not working) and
  `ENG-042` (whether «complemento» is deliberate in the engine's legal documents or a drift).

## What is pending is not here

Read it with `pwsh -NoProfile -File eng/list-pending.ps1` and in [TAREAS.md](TAREAS.md).
