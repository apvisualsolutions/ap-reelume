# Automatic segment detection — frozen subspecification

- Status: `FROZEN` — approved by the owner on 2026-08-07
- Language: English; the Spanish original is in
  [automatic-segment-detection.es.md](automatic-segment-detection.es.md)
- IDs: `PLY-013`; related `PLY-012`, `PRI-001`
- Task: T43 of the [implementation plan](../plans/2026-08-01-ap-reelume-windows-mvp-implementation.md)
- Documents: [`docs/FEATURES.md`](../../FEATURES.md),
  [T29 — manual markers](../../evidence/mvp/T29-manual-markers.md)

This subspecification is frozen **before** any detection code is written. The thresholds and
corpus below are the approved ones; changing them requires a new owner approval and is recorded as
a change to this page in both languages.

## 1. What it detects and what it does not

The detector finds **recurring segments** within a single series: stretches of audio that repeat
across episodes. From those it classifies the three kinds of the shared T29 model (`MarkerKind`):

- **Intro**: the recurring stretch present in most episodes inside the opening window.
- **Recap**: a recurring stretch distinct from the intro that, in the episodes where it appears,
  precedes the intro inside the opening window.
- **Credits**: the recurring stretch inside the closing window.

What does not repeat is not detected. A real recap whose content changes completely every episode
is only detectable in its recurring portion (its sting or jingle); the corpus ground truth defines
recaps exactly that way. This limitation is deliberate and declared.

Version 1 of the detector uses **audio only**. The corpus video exists so the episodes are real
playable files, not as a signal.

## 2. Detector contract

- **Local and offline.** No extraction or comparison ever opens a connection. Verified with the
  same canary pattern as `PRI-001`.
- **Cancelable.** Cancellation responds promptly and leaves no half-written markers.
- **Low priority and pausable.** The work runs at low priority and pauses while a playback is
  active; the pause is measured, not declared.
- **Bounded.** Only the opening and closing windows of each episode are extracted; never the whole
  episode. Extracted fingerprints are cached so nothing is re-extracted.
- **Produces, never overrides.** The T29 manual markers are per series; a detection is per
  episode, because a variable cold open moves the intro around in every episode and one fixed
  range per series cannot represent that. Every detection is persisted per episode with its kind,
  range, confidence, and detector version, and the player consumes it in the same `IntroMarker`
  shape (`Origin = Detected`). When the series has a manual marker of a kind, detections of that
  kind are not used; a re-detection replaces uncorrected detections and **never** touches a
  `Manual` marker or a detection with `UserCorrected = true`.
- **Reviewable.** The interface allows reviewing, accepting, correcting, or deleting every
  detection. Correcting a detection turns it into `UserCorrected = true`.
- **Switchable.** Detection is an option; when off, nothing is extracted or compared.

## 3. Evaluation corpus

Approved: an **entirely synthetic, generated** corpus — never the personal library nor
third-party material. The repository stores the structure and ground truth
(`tests/ApSolutions.LocalMedia.MediaTests/Fixtures/segment-corpus-manifest.json`); the video is
materialised under `artifacts/test-media/segments/` (git-ignored) with ffmpeg and native encoders
(`mpeg4` + `aac`), reproducible from nothing on any machine.

Every episode is composed in this order: `[recap] [cold open] [intro] body [credits]`, where each
piece is optional per series. Each piece's audio is a deterministic chord sequence (one two-sine
chord every 2.5 s) derived from a seed: recurring pieces use the series seed and unique pieces a
per-episode seed, so recurring material genuinely repeats and unique material resembles nothing
else. The chord instead of a single tone makes the per-step space large enough that two different
seeds never share a four-chord run by accident, and a corpus test checks exactly that.

### Series

| Series | Split | Ep. | Pattern it contributes |
|---|---|---:|---|
| S01 | development | 10 | Exact 25 s intro from 0 s; 30 s credits. The base case. |
| S02 | development | 10 | Variable cold open (5–45 s) before the 25 s intro; 30 s credits. |
| S03 | development | 9 | 15 s recap in 6 of 9 episodes, then 20 s intro; 25 s credits. |
| S04 | development | 8 | One special with no segments at all; the rest variable cold open + 24 s intro + 30 s credits. |
| S05 | development | 12 | Short 12 s intro; 15 s recap in 6 of 12; long 45 s credits. |
| S06 | development | 8 | **No recurring segment at all.** False-positive control. |
| S07 | held-out | 10 | Variable cold open + 22 s intro + 30 s credits. |
| S08 | held-out | 9 | 15 s recap in 5 of 9; 18 s intro; 28 s credits; one special with no segments. |
| S09 | held-out | 11 | 30 s intro from 0 s; 35 s credits. |
| S10 | held-out | 8 | **No recurring segment at all.** Held-out false-positive control. |

95 episodes; bodies of 120–200 s per episode, of varying length. Each episode's ground truth is
the recap, intro, and credits ranges that follow from its structure; the cold open and the body
are not segments.

### Held-out protocol

- The detector is tuned only against the **development** series (S01–S06).
- The **held-out** series (S07–S10) run only in the verification phase (T43.4) and are the ones
  the thresholds are judged on. Tuning the detector after looking at the held-out set invalidates
  the measurement and requires generating a fresh held-out set before measuring again.
- Metrics are published **aggregated and per series**; an average never hides a series.

## 4. Definition of a hit, and metrics

- A detection **hits** when the kind matches and both of its boundaries fall within ≤ 2.0 s of a
  ground-truth range of the episode (`|start−start| ≤ 2 s` and `|end−end| ≤ 2 s`).
- **Precision** per kind: hitting detections / emitted detections of that kind.
- **Recall** per kind: hit ground-truth ranges / existing ranges of that kind.
- **Spurious detection**: an episode with no segment in the ground truth that receives at least
  one detection. The rate is over the set of segment-free episodes.

## 5. Approved thresholds

Measured on the held-out series, with the ±2.0 s per-boundary tolerance:

| Metric | Threshold |
|---|---|
| Intro: precision | ≥ 0.90 |
| Intro: recall | ≥ 0.80 |
| Credits: precision | ≥ 0.90 |
| Credits: recall | ≥ 0.80 |
| Recap: precision | ≥ 0.85 |
| Recap: recall | ≥ 0.70 |
| Segment-free episodes: aggregate spurious rate | ≤ 5 % |
| Every individual series: precision | ≥ 0.80 |
| Every individual series: spurious rate | ≤ 10 % |

With 9 held-out segment-free episodes, the 5 % aggregate bound tolerates in practice **zero**
episodes with a spurious detection; it is recorded that this severity is intentional.

`PLY-013` becomes `VERIFIED` only if **all** thresholds pass, the aggregates and each series'. If
any fails, the status stays at whatever honest value corresponds, with its condition.

## 6. Benchmark sensitivity (T43.2)

Before implementing anything, the corpus runs against a **null detector** (emits nothing) and a
trivial fixed-position baseline (for example "intro = first 30 s, credits = last 30 s"). Both must
**fail** the thresholds; if either passed, the benchmark measures nothing and is fixed before
continuing. Both sets of metrics are archived with the RED evidence.

## 7. Declared limitations

- The synthetic corpus measures the **mechanism** of within-series comparison (alignment,
  boundaries, classification, false positives), not the aesthetic variety of real shows.
  Thresholds over real shows are out of T43's scope and would be revisited with redistributable
  real material.
- A recap is defined as its recurring portion; a recap without a recurring portion is not
  detectable and does not count in the ground truth.
- Version 1 uses no video signal and no container chapter metadata.
