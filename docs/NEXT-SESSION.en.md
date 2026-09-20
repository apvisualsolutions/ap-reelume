# Where to pick up — 2026-09-20 (closing, second batch)

> **Overwritten** at every closing, capped at 80 lines and 6 KB per language; `eng/check-handoff.ps1`
> measures it. History up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree outranks this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything.

## State

`main` and the branch are level, on the same commit. Read it with `git log --oneline -1 main` — the
sha is deliberately not written here, because the commit that writes it changes it.

Four runs today, **all four green**, each conclusion read twice: through the watcher and through
`gh run list --commit`. Every fast-forward was made from a read conclusion, never from a guess.

## What was done

· **The inherited red is closed.** `CompositionRoot.cs` floor went 90/64 → 90/65, copied from the
  coverage-debt artefact of the run that matched HEAD. One line of diff; the ratchet stays at 185.
· **`ENG-015`**: the guard over the walk ratchet now refuses what touches a row and lets through
  what only touches a `##` comment. Its battery is versioned beside it.
· **`ENG-016`**: the coverage preview now sees staged and untracked new files, measured by effect
  against a lying repository.
· **`ENG-045`** was born and closed the same day: it lives in IT as rule R12, not here.
· `CLAUDE.md` brought level with all three.

## The measured traps

· **The handover was wrong on two counts, and the tree won both.** `main` was on `ENG-044`, not
  `ENG-042`; and there were **two** red runs, not one, with the good artefact belonging to the
  later one — the run whose tree matched HEAD.
· **`grep -c $'\r'` lied again**, on both sides of a comparison, exactly as it did on 2026-08-29.
  What caught it was a plain `diff` saying `1,402c1,402` and a 402-byte size gap. Count with
  `tr -cd '\r' | wc -c`. This is now rule R12 of IT's guard.
· **`git log -S` cannot see a number change** — it counts occurrences of a string, and the string
  stays. `-G` answers. A whole ratchet history read as "never touched".
· **A mutant came out identical to the original** and scored 12 of 12 without measuring anything,
  because the `sed` pattern did not match. Diff the mutant before believing it.

## First thing next session

· **`gate-auditor` was NOT run over the tests `ENG-016` added.** That is the first step, in an
  isolated worktree, before any new work — and remember its copy makes `EvidenceLinkTests` red
  while it exists.
· **Two checks that only a NEW session can make**: whether this project's `second-brain` tools
  finally arrive — the cause was folder trust keyed by path spelling, now persisted — and the
  common system against **0.11.0**, published as this closed. `claude plugin update` should say
  `updated 0.10.1 → 0.11.0` and the ping answer `dependencias: OK`; in a terminal without
  permissions it will say it could not measure, or ask for approval instead of printing the line,
  which is a known IT defect and **not** a plugin failure. This session ran on 0.10.0.
· Then `ENG-017`, the first takeable row: nothing checks that the upscaler compiles its shader once
  per film, and deleting the guard leaves 1,446 tests green.

## Waiting on the owner

· **`ENG-002`** — a session with the real screen reader, to learn whether it reads upper-case
  headings worse. Nobody can answer it from memory, and it gates spending on a mechanism an
  earlier decision rejected on cost.
· **`ENG-005`** — a small diagnostic executable that **opens a window**, so it is asked for when he
  is not working. Nothing in the tree mounts the real Windows host today.
· Blocked on something that is not code: **`PRD-002`** needs the commercial signing certificate and
  **`PRD-003`** an ARM64 machine, which CI borrows for free but cannot fully answer from an x64 host.

## What is pending is not here

`pwsh -NoProfile -File eng/list-pending.ps1` answers it: **24 open of 75**, 21 of them work and 3
standing decisions not to build something. Scope lives in `FEATURES.md`; chores, gates, debt and
unmeasured questions in `TAREAS.md`, oldest at the top — `ENG-046` was added today.
