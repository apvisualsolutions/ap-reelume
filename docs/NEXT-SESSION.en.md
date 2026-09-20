# Where to pick up — 2026-09-20 (closing)

> **Overwritten** at every closing, and no more than 80 lines or 6 KB per language; measured by
> `eng/check-handoff.ps1`. The history up to 2026-09-19 is frozen in
> [NEXT-SESSION-HISTORY.en.md](NEXT-SESSION-HISTORY.en.md). **The tree beats this document**:
> `git log --oneline -1`, `git log --oneline -1 main` and `gh run list --limit 3` before anything.

## State

`main` stands at the `ENG-042` commit, with its CI read green, and stays there.

**The `ENG-011` commit came out RED and was not fixed here**, which is the step-0 rule. The cause is
measured and is a two-minute job: **all ten suites passed** — 891, 406, 55, 684, 1,474, 155, 201,
202, 118, 17 — and the only failure was the coverage gate with «1 improved»:
`CompositionRoot.cs now reaches 90/65`, because the new wiring gave it one more covered branch while
its floor says 90/64. **First thing tomorrow**: `gh run download 35515823303 -n coverage-debt`, copy
the artefact over `eng/coverage-debt.txt` normalising CRLF to LF, check the only difference is that
row, and push. With that green, `main` moves to `ENG-011`.

## What was done

· **`ENG-044` closed, and `ENG-010` with it**: folder watching switches on, is governed from
  Settings and takes effect without a restart. `LIB-003` returns to `VERIFIED`, blocker withdrawn.
· **`ENG-042` closed**: the three documents that travel with the package say `plugin`.
· **`ENG-011` closed**: playback speed is stored and applied as a file opens.
· The debt ratchet dropped to **185**: `FallbackScanScheduler.cs` reached 100/100 and left the list.

## The measured traps

· **Two faults of the same kind on the same day, and it is the house defect**: watching and speed
  were built, stored and resolved, and **nobody called them**. The grep that finds it asks who READS
  the resolved value, with a positive control beside it — `resolved.Picture` returned one and
  `resolved.SpeedMultiplier` returned zero.
· **The fallback sweep ran for nobody either**, held by the same flag as watching. That left USB and
  network roots with no recovery — the other half of `LIB-003` — and a dead watcher with no retry. It
  was on no row: it surfaced while finishing `ENG-044`.
· **Two blind guards**, found by mutating: a `Dispose` test green without the code it claimed to
  check, and a branch nothing could take. And **a textual assertion** — `RootWatchWiringTests`
  looking for a constant in the source — stayed green for weeks while what it named ran for nobody.
· **A zero gets measured twice, and today I failed three times**: I read `tail`'s exit code after a
  pipe and accused an IT guard; I measured two states as one because another session changed the
  machine between my readings; and I called a drawer absent from files that return zero by design.

## First thing next session

· **Read the `ENG-011` CI and move `main` if green.** It is the only thing left to publish.
· **Repeat the closing marker and the doctor of the shared system** once IT publishes **0.10.1** —
  see below.

## Waiting on the owner

· **The shared system is broken over the NAS and this closing was done WITHOUT its marker.**
  The tool that sets the marker exits 2 because its scripts resolve paths with
  `(Resolve-Path X).Path`, which over a network drive returns the provider-prefixed PSPath that git
  rejects with 128; with `.ProviderPath` it exits 0. Isolated with positive and negative controls, and reproduced by IT on
  their side: it affects **every** project since the migration. Their grep found **33 uses of the
  pattern, 9 in production**. Fix commissioned as **0.10.1**; they will tell us when it ships.
  **Consequence for this closing**: the plugin's two gates did not read the record, and the record
  could not be computed.
· **The second-brain drawer exists but did NOT reach this session**, so no checkpoint was written; IT
  has re-wired it. **Mind how this is checked**: the connector lives in the per-project user
  configuration, not in `.mcp.json` or the local settings — an IT decision after an internal name was
  published in a public repository — so those two return zero **by design**. Ask whether the session
  has the drawer's tools, and only that; its name is not written here, for the same reason as
  `ENG-043`. It is already wired to the new path: check it and write the previous batch's checkpoint.
  Here a zero was taken for an absence when the pattern was looking in the wrong place.
· **`ENG-015` has a finding that blocks it**: the hook protecting `eng/walk-pending.txt` denies every
  write without telling apart adding a row from fixing a stale comment, so its three out-of-date
  figures **cannot be fixed** with the editing tools. And `CLAUDE.md` contradicts itself there too:
  the real sequence has to be reconstructed before rewriting anything.
· **`ENG-002`** (a session with Narrator) and **`ENG-005`** (opening a window when he is not working).

The privacy filter ends at 1 finding, and it is a **false positive IT has accepted**: VideoLAN's
public IP, quoted in the `ENG-042` commit while documenting a network failure. History is not
rewritten (`ENG-032`). Convention until they refine it: cite a third party by host name, never by IP.

## What is pending is not here

Read it with `pwsh -NoProfile -File eng/list-pending.ps1` and in [TAREAS.md](TAREAS.md). Today: 24
open of 75.
