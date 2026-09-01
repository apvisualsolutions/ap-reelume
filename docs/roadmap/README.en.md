# Roadmap

What AP Reelume does today, what it will do next, and what it has decided not to do. The Spanish
version is at [README.es.md](README.es.md). The canonical scope record is
[FEATURES.md](../FEATURES.md); this is its prose reading.

## The publishing rule

**Nothing ships until everything committed to is verified.** The owner's decision of 2026-08-31, and
it overrides the usual reading of the three releases below: no partial first release gets cut and
improved afterwards. The three releases still order **what gets built first**; they no longer
authorise **publishing** once the first one is done.

What counts as "everything", so the rule is checkable rather than an intention:

- **Counts**: every row of [FEATURES.md](../FEATURES.md) the matrix treats as a commitment —
  `DESIGN_APPROVED`, `PLANNED`, `IN_PROGRESS`, `IMPLEMENTED`, `BLOCKED` — and the `DEFERRED` ones
  too, which are postponed commitments rather than rejected ones. All must reach `VERIFIED`.
- **Does not count**: anything `OUT_OF_SCOPE`, because that is not an outstanding feature but a
  written decision not to build it — today `UX-008` and `PLY-015`. Bringing those in takes a new
  decision, not this rule.

`pwsh -NoProfile -File eng/list-pending.ps1` answers how much is left at any moment, and separates
the two categories on its own.

**What this rule turns into a publishing blocker, worth knowing early:** `PRD-003` does not depend on
writing code but on **a Windows 11 ARM64 machine that does not exist here**; and `PRD-002` cannot
reach `VERIFIED` without the **commercial signing certificate**, because its cycle was verified on a
re-signed copy and the unsigned artifact cannot repeat it — which chains it to `REL-001`.

**And a third one which, unlike the two above, is resolvable without buying hardware:** `PLY-004` is
blocked because this machine's four output endpoints all declare a two-channel mix format, so the
5.1 and 7.1 layouts have never been measured. **The owner decided on 2026-09-01 that a virtual
eight-channel endpoint verifies it**, with the evidence recording that the endpoint was virtual; they
chose VoiceMeeter Banana, and ruled out VB-CABLE because its own forum documents that it delivers the
eight channels over Kernel Streaming and not always over shared WASAPI, which is the path the
application uses. Installing it is the owner's. The harness that will measure it is already written
and green.

## The three releases

| Release | What it means |
|---|---|
| `MVP` | An installable x64 application, useful for validating a real collection. Gate approved on 2026-08-05. |
| `STABLE` | The first complete public release, ARM64 included. This is where we are. |
| `POST_STABLE` | Improvements that do not block the first stable release. |

## Where we are

The MVP catalogues, identifies, plays, and remembers where you left off, in Spanish and English,
with no account and without sending anything anywhere. It ships as an x64 MSIX and as an independent
ZIP, both with a published hash and a reproducible build.

Of the 46 MVP commitments: **44 verified**, **1 deliberately out of scope**, and **1 blocked** by
hardware or environment this machine does not have. None is informally pending: every block names its
owner and what would clear it.
[release-readiness.md](../evidence/mvp/release-readiness.md) sets them out.

The Product Owner approved the MVP gate on **2026-08-05** with that block declared. Approving the
gate does not settle it: `PLY-004` stays blocked under the same condition, and the risks the MVP
leaves open are inherited by `STABLE` rather than closed. Part B begins with the approval.

## What comes next: `STABLE`

| ID | What is missing |
|---|---|
| `PRD-003` | ARM64 parity. The build and the native package are done and verified; what remains is certifying playback on a Windows 11 ARM64 machine, of which there is none. It blocks the stable release. [T42](../evidence/stable/T42-arm64.md) |
| `REL-001` | Microsoft Store as the primary distribution, with its certification. It carries two known debts from the MVP: justifying the restricted `unvirtualizedResources` capability to the Store — without it the package deletes the library on uninstall — and deciding when to sign, because a commercial certificate changes the package identity. |
| `REL-004` | Formal trademark, domain, and Store clearance for the public name. |

Two of that list are already done: `REL-003` and `PLY-013`. The independent updater checks,
summarises in both languages, downloads into a folder of its own while verifying hash and size,
and hands nothing to Windows without a confirmation that names the version that was on screen; the
Store keeps its own channel. [T44](../evidence/stable/T44-updater.md) And automatic segment
detection compares each series' episodes locally, meets every approved threshold on a held-out
corpus, and never overrides a manual marker or a human correction.
[T43](../evidence/stable/T43-segment-detection.md)

## What comes after that: `POST_STABLE`

| ID | What it is |
|---|---|
| `UX-007` | Custom lists. The current model can take them without a destructive migration. |
| `PLY-015` | Dolby Vision and Dolby/DTS passthrough. It needs a technical, legal, and demand review that has not been done. |

## What this release does **not** do

This is not a backlog: these are decisions. Changing one means updating the specification and the
matrix first, in both languages.

- **No accounts and no remote session.** One person, one PC. No sign-up, no password, no profile.
- **No cross-device sync and no cloud.** What the application sees is on your disk.
- **No simultaneous playback of several videos.** There is one playback session, and only one.
- **Not a course platform**, and since `ADR-0006` that sentence is narrowed rather than deleted. What
  stays out is the part that motivated it: no enrolments, no certificates, no quizzes, no streaks, no
  study statistics, no percentage of training completed, and nothing that talks to a platform. What
  comes in (`CRS-001`…`CRS-005`) is what the application already does with a show: recognise what is
  on the disk, order it, play it in order, and remember where you were.
- **No video management beyond cataloguing.** It does not transcode, trim, or export video. Safe
  rename is the only operation that touches files, and it previews before doing anything.
- **No personal notes or bookmarks on the timeline** (`UX-008`). Intro and credits markers exist
  (`PLY-012`), but they belong to the show rather than being a personal notebook.
- **No custom lists yet** (`UX-007`, deferred).
- **No Dolby Vision and no audio passthrough** (`PLY-015`, out of scope).
- **No macOS and no Linux.** The core is decoupled and references neither Windows nor Avalonia APIs
  (`PRD-004`), so porting would be possible; it is not planned.

## How this roadmap changes

A feature only moves to `VERIFIED` when its evidence is linked in the matrix. A scope change — adding
something from the list above, or removing something from the list below — is recorded first as a
decision in an [ADR](../adr) and then in the matrix, in Spanish and English.

