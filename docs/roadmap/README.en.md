# Roadmap

What AP Reelume does today, what it will do next, and what it has decided not to do. The Spanish
version is at [README.es.md](README.es.md). The canonical scope record is
[FEATURES.md](../FEATURES.md); this is its prose reading.

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
- **Not a course platform.** No lessons, no training progress, no certificates.
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

