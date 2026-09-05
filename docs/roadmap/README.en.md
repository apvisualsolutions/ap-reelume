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

**What this rule turns into a publishing blocker, worth knowing early:** `PRD-002` cannot reach
`VERIFIED` without the **commercial signing certificate**, because its cycle was verified on a
re-signed copy and the unsigned artifact cannot repeat it — which chains it to `REL-001`.

**And `PRD-003` stopped being what this line said, on 2026-09-04.** It said it depended on «a
Windows 11 ARM64 machine that does not exist here». There is one and it is free: GitHub offers
hosted Windows 11 ARM64 runners — `windows-11-arm` — **free and unlimited on public repositories**,
and this one has been public since 2026-08-10.

**And all six phases can be attempted, because none of them needs hardware.** That cost two false
assumptions before anybody read the tests each phase runs: the audio phase runs the engine muted and
checks **what the video carries**, not what leaves the speakers; the HDR phase **injects** a fake
display for both cases and decodes in software on purpose. The matrix said so from the start: all six
carry the **same** blocking reason — «this build ran on a X64 host» — and not one of them mentions
sound or a screen. `VideoLAN.LibVLC.Windows` ships native ARM64 binaries with its plugins, checked in
the downloaded package.

**What is still unknown is whether that image — maintained by Arm, LLC, and not the same one as the
x64 image — carries the tooling the workflow expects**, starting with `ffmpeg`. That is only known by
running it, and it is the next session's priority batch. Until it is measured `PRD-003` stays
`BLOCKED`: what changes is that clearing it no longer requires buying anything.

**And a third one that is now settled, on that same 2026-09-01:** `PLY-004` was blocked because this
machine's four physical endpoints all declare a two-channel mix format. The owner decided that a
**virtual** eight-channel endpoint verifies it, with the evidence recording as much; VoiceMeeter
Banana was installed — VB-CABLE was ruled out because its own forum documents that it delivers the
eight channels over Kernel Streaming and not always over shared WASAPI, which is the path the
application uses — and on that endpoint the output was **recorded and its eight channels counted**,
each carrying its own tone at a minimum contrast of 86 dB. `PLY-004` moves to `VERIFIED`, and **two
of the three publishing blockers remain**.

**That paragraph was written on 2026-09-01 and said «both of them purchases: the ARM64 machine and
the signing certificate». They are no longer two purchases but one**, and the 2026-09-04 block above
refutes it: GitHub's `windows-11-arm` runners are free and unlimited on public repositories. There
are still two blockers and `PRD-003` is still `BLOCKED` until its six phases are measured; what is no
longer true is the reason it was. It is corrected here because **no test crosses the two claims**:
`ScopeBoundaryTests` only requires that `PRD-003` be named in both languages, not that what is said
about it agree with itself.

### Decided on 2026-09-05 and not yet built

**Covers have three origins and an order, written down in
[ADR-0009](../adr/0009-a-cover-has-three-origins-and-an-order.md).** The hand-picked one wins, then
the provider's, and failing both a frame is taken from the video — for films and shows too, not only
for courses. The order is changed in a general setting and can be overridden on one title, with the
gallery the prototype already draws. It closes a measured defect: today one field holds two things,
and a provider refresh leaves somebody's cover orphaned inside every backup.

**And twelve things remain built that no screen shows**, out of the eighteen found by
[the audit of 2026-09-04](../evidence/stable/audit-built-and-not-drawn.md). The six closed are the
covers in the grid, the library that stopped at fifty titles, the countdown row that promised to be
configurable without being so — **actually closed on 2026-09-05**, with Settings' «Playback» section
— the mini player's three names, the orphaned strings, which also gained
[a gate](../evidence/stable/audit-orphaned-strings.md) so they cannot come back, and **the scan that
could be cancelled from inside and not from outside**, closed that same afternoon with
[the notices strip](../evidence/stable/audit-lib002-the-notices-strip.md). The remaining twelve come
in two groups: what only needs showing, and what the design has and the application does not.

**And one turned up that the audit did not have, because it only shows in pixels**: the Courses
screen was drawn **under** the welcome card, with both titles and both descriptions overlapping and
unreadable. It came out of photographing the application beside the prototype, in the first pair
nobody had ever looked at. It is
[closed, with its gate](../evidence/stable/audit-courses-under-the-welcome-card.md), which now covers
**every** destination rather than one.

**The «Permissions» button on the access-denied notice is left out, and that is the owner's
decision.** The prototype draws it: it opens Windows' settings for that share. Here that means
**starting a system process**, which lives in the host layer and has isolation rules of its own — it
is not «one more button» in a view. The recommendation is **not to build it for now**: the notice
already says what is happening and that the application never changes permissions on its own, which
is the part that stops anybody expecting from it something it does not do. It stands as new scope,
awaiting a yes or a no.

**Visual parity gets another pass.** `PRD-006` is `VERIFIED` over «the 53 views» and the tree has 60;
and of those 53 only **eight screens** were photographed beside the prototype. On top of that, the
per-view files it would be compared against arrived six days after it was called done. It drops to
`IMPLEMENTED` and rises when it covers all sixty.

**A notice describing a state takes space; one narrating an event floats**, and it is written in
[ADR-0010](../adr/0010-a-state-takes-space-and-an-event-floats.md). It was decided because nobody
ever had: neither the notices strip nor the transient message was in the controls inventory or in its
exclusion list, so the same question could be answered two ways. **The prototype's notices were NOT
broken** — they push 77 px on purpose — and this matches Microsoft, Material and Carbon, and what
this application already decided in August for the loose-file band. Two owner decisions follow from
it: **the disconnected-disk notice goes in the Library only**, not following you around the
application; and **the scan is drawn two ways** — the full strip when a person launches it, a quiet
marker when it starts on its own at opening.

**Undoing a decision in the review inbox is deferred, with its measurement written down.** It looked
like «adding a button» and is not: today there are **three locks** — the store refuses to return an
entry to pending, the row keeps its decision locked, and no previous state is stored — and,
moreover, accepting has already rewritten the title's metadata with no copy of what was there. The
prototype promises in writing «you can change it later», so the promise is on record and the decision
is taken with that number in front of it, not before.


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
| `PRD-003` | ARM64 parity. The build and the native package are done and verified; what remains is running the six phases on an ARM64 machine. **Since 2026-09-04 there is no need to buy one**: GitHub's `windows-11-arm` runners are free on public repositories, and not one of the six phases needs hardware. It blocks the stable release until measured. [T42](../evidence/stable/T42-arm64.md) |
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

