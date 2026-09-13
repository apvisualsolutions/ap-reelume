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

**And on 2026-09-11 the owner hardened it: «every improvement has to be applied as soon as possible
or there will be no release».** An improvement that is found or registered goes to the immediate
order, not to a list with no date. It was said while widening `PLY-016`, the picture enhancement for
low-resolution videos: it covers any video below the screen's resolution — 720p or 1080p on a 4K one
— and **complete**, with the vendor's own super resolution on NVIDIA, Intel and AMD and a portable
upscaler everywhere else. **It does not wait for VLC 4**: that day VideoLAN published 3.0.23 as
stable and 4 only as an «unstable», unsupported nightly build, and the package feed held no 4 at all.
**And it is not needed**: the 3.0.23 this project installs already carries the three vendors' super
resolution in its D3D11 output — NVIDIA and Intel since 3.0.19 and AMD since 3.0.21, checked in the
binary — although it only acts in VLC's own window and not in this application's composition, which
draws its controls over the video. Bringing it into that composition, and the portable upscaler for
every other card, is the work of `PLY-016`; August's claim that it needed VLC 4 was false and is
corrected in its evidence.

**What this rule turns into a publishing blocker, worth knowing early:** `PRD-002` cannot reach
`VERIFIED` without the **commercial signing certificate**, because its cycle was verified on a
re-signed copy and the unsigned artifact cannot repeat it — which chains it to `REL-001`.

**And since 2026-09-12 there is a second one, and it is about money: `PLY-016`'s AMD half.** Put to
the owner with its recommendation — switch RTX Video Super Resolution on in the NVIDIA App, enable
the i7's integrated graphics, and authorise a machine with an AMD card in the cloud, around
$0.11/h on spot or $0.62 on demand — he authorised the first two and **not the third**. The first two
are measured: Windows sees the RTX 5070 and the UHD 770. With no AMD machine its super resolution can
be written and **cannot be verified**, and a switch that says «on» without any pixels compared is
exactly what that row forbids. So `PLY-016` will only reach `VERIFIED` in two thirds until that
spending decision is made: it is the owner's, it goes back to him once AMD's chain is written, and
the recommendation is still yes — these are a few hours of a machine, not a purchase.

**And on 2026-09-13 `PLY-016` stopped being only a measurement: its portable link draws.** A video
below the box it is drawn in looks sharper **with nobody switching anything on**, and it is measured in
ink: the ramp across a hard edge enlarged four times falls from **4 pixels to 2**. It runs on any card,
so it is the part that reaches 100 % of whoever uses the program — which is what the owner asked for.
Evidence: [PLY16-portable-upscaler.md](../evidence/stable/PLY16-portable-upscaler.md).

**That reorders what is left, and it is worth saying precisely**: the row stays `IN_PROGRESS`, because
its criterion also promises the on-screen indicator and the **vendor's** super resolution on all three
makes. And that half **cannot reach a frame in today's architecture**, measured two ways: the compositor
answers that it has no GPU interop, and the only remaining route — reading the texture back into memory
— is 33 MB per frame at 4K. What unblocks it is building LibVLC without the copyleft option and then
the RTX video kit, which runs inside the process and needs neither a shared texture nor a read-back. So
**AMD's spending blocker is no longer first in the queue**: that build goes ahead of it.

**And on 2026-09-12 `PLY-016` stopped being an intention: a third of it is verified.** The video
processor probe measures that **Intel's super resolution works on this machine's own UHD 770** — it
changes 54.6 % of the picture, with its negative control at zero and a positive control proving the
processor drew at all — and that the format this application already produces is accepted by both
cards with no colour conversion. **NVIDIA accepts the request and moves no pixel, and that is
recorded as INCONCLUSIVE**: the call is identical to Chromium's, which documents that the driver
accepts it and ignores it while RTX Video Super Resolution is off in the NVIDIA app, which is how it
ships. Switching it on and measuring again is what is left, and it is not code. The evidence, with
what getting Intel's call wrong twice cost, is in
[PLY16-d3d11-probe.md](../evidence/stable/PLY16-d3d11-probe.md).

**And the owner set the condition that orders the design: the improvement has to work without the
person using the application changing anything.** It was investigated and **it holds**, by a route
that depends on no vendor: the standard Direct3D video processor filters — noise reduction and edge
enhancement — are declared by **both cards**, and the enhancement changes 8.08 million bytes on each,
**NVIDIA included**. Intel's super resolution, which already works on its own, and the portable
upscaler for everything else sit on top of that. **What cannot be done is switching RTX Video Super
Resolution's global toggle on from the application**, and on 2026-09-13 the four routes were
exhausted with controls beside each: writing the registry value that does exist —
`_User_Global_VAL_SuperResolution`, 5 on and 0 off — moves no pixel even with administrator rights;
restarting the driver service does not help; the driver's profile database does not change a single
byte when the toggle moves; and NVIDIA's own app does not read that value. It tells the driver live,
over a channel that leaves nothing writable behind.

**What does exist, and changes the sentence that stood here**: the **RTX Video SDK** carries the
super resolution in a component that runs inside the process, with no toggle and nothing for the user
to touch, and its agreement permits shipping it inside an application. It is **no longer "rejected on
licence"** as of `ADR-0013`: what blocks it now is VideoLAN's decoder built with the copyleft option,
and it only runs on RTX cards.

**So the vendor's super resolution is still a bonus and never the promise**: what is promised always
runs, on any card, and the vendor's is taken advantage of only if comparing it changes pixels.

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
[the audit of 2026-09-04](../evidence/stable/audit-built-and-not-drawn.md). **The count was measured
one by one on 2026-09-06** and the six closed are the library that stopped at fifty titles, the
countdown row that promised to be configurable without being so — **actually closed on 2026-09-05**,
with Settings' «Playback» section — the mini player's three names, the orphaned strings, which also
gained [a gate](../evidence/stable/audit-orphaned-strings.md) so they cannot come back, **the scan
that could be cancelled from inside and not from outside**, closed that same afternoon with
[the notices strip](../evidence/stable/audit-lib002-the-notices-strip.md), and **editing a single
episode's card, which closes because its premise was false** — the show card does have a route to the
editor. The remaining twelve come in two groups: what only needs showing, and what the design has and
the application does not.

**This paragraph counted «the covers in the grid» among the six, and that was counting wrong even
though the total came out right**: that cover was the audit's **trigger**, not one of its eighteen,
and in exchange it held open the one already closed. Two counts that agree are not a confirmed
count.

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

**Visual parity gets another pass.** `PRD-006` was `VERIFIED` over «the 53 views» and the tree has
**61**; and of those 53 only **eight screens** were photographed beside the prototype. On top of
that, the per-view files it would be compared against arrived six days after it was called done. It
dropped to `IMPLEMENTED` and rises when it covers all sixty.

**On 2026-09-06 its criterion was corrected to 61 and its comparison reached nineteen pairs of the
prototype's forty-two screens**, with **43 measured defects** and another 65 candidates closed with a
written verdict, in
[round four](../evidence/stable/audit-prototype-fidelity-round-four.md). **That is what the
publishing rule counts**: `PRD-006` is a commitment, so those 43 are fixed before anything is
published. Still to compare: the states that have to be manufactured, and the whole player.

**The owner decided on 2026-09-06 in what order they are attacked: screen by screen**, starting with
Library and scanning, which holds nine of them and contained the most serious of all. The order is
Library and scanning, film details, keyboard shortcuts, metadata editor, updates, the Settings index,
subtitles, backups, privacy, courses and segment detection. **Its price is measured**: seven of the
forty-three are shared labels spread over six screens, and one sweep would close them at once; going
screen by screen they are touched in passing, on condition that none is left for a label pass that no
longer exists.

**The first is closed**: removing a folder deletes its catalogue, after a question stating what will
be lost. It is the only one of the forty-three that promised something false about the data of the
person using the program, and its decision is in
[ADR-0011](../adr/0011-a-destructive-question-floats-and-blocks.md). **42** remain.

**The margin between the rail and the first card is closed since 2026-09-11, and it was not a single
figure**, as this paragraph said: the prototype writes one page padding — 28 under the bar, 32 on
the sides, 48 at the bottom — for all its destinations, and the application put 48 on each of them;
the cover also carried its tile's 8 px uncompensated, 56 in all. Pages now open at 32 and the cover on
the title's line, with the prototype's own technique, in
[the margin's evidence](../evidence/stable/audit-page-margin.md). **It was not one of the 42** — it
was the geometric defect round four left standing — so the count does not move.

**Measuring it at 1600 px uncovered another, closed the same day**: the grid did not count each
card's 1 px border and at that width took a column that did not fit, so the last cover ate 9 px of
the right margin. **And it left one registered that was missing: the fluid grid, closed on
2026-09-11 too** as soon as the owner put it first in the parity order. The application used a fixed
148 card that fitted eight times at 1600, with about 160 px free on the right; it now counts its
columns with the prototype's rule and shares the width out to the pixel, as a browser does: nine
covers of 147 and 148 at 1600, eight of 155 and 156 at 1500, and 33 px inside the page on each side,
counted in pixels. All of it is in [the grid's evidence](../evidence/stable/audit-fluid-library-grid.md).
**It was not one of the 42**, so the count does not move.

**And measuring it against the prototype left seven by name, which under the owner's rule of the
same day — «every improvement has to be applied as soon as possible or there will be no release» —
go to the immediate order and not to «later»**: the card's vertical rhythm, fixed in one go — from
the last line to the next cover 18 against 20, from the cover to the title 8 against 10, and the
three lines under the cover spaced 8 px apart where the prototype stacks them —; four differences of
shape on the card — the progress track, the «unavailable» veil, the kind chip and the watched mark —;
the density, which only matches the prototype at comfortable; Home's two rails, which the prototype
draws as a fluid grid with a 132 minimum; the first load's skeleton; posters decoded at 148, now drawn
up to about 187; and the scroll position that moves on resize. **And one that is not about parity**: switching language wipes
the chosen appearance until a restart, read in the code and still to be reproduced.

**And one that was not in the count either, raised by the owner the same day and closed**: the
twenty-seven option pills drew a radio button inside, and in eighteen it was the only thing saying
which one was chosen. The prototype draws it on none; now the chosen one says so with its border, in
[its evidence](../evidence/stable/audit-option-pills-without-a-circle.md).

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

**And two defects that removing a folder brought into view, registered on 2026-09-06 and not yet
fixed.** The first: after a folder is removed, its watcher stays alive until the program closes,
because the watching service knows how to start on a folder and how to stop them all, but not how to
stop watching one. It cannot undo the deletion, and every change in that folder raises an exception
that is swallowed, which is the kind of failure this house does not let through. The second: every
removal made before 2026-09-06 left its catalogue in the database, marked available with no folder
behind it. **That debris can only exist in development databases**, because no version has ever been
published — the repository has no releases and no tags — and that is what decides whether a
migration is needed or writing down why not is enough.


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

