# Test media fixtures

## What they are and why they are not in the repository

The container and codec matrix is generated while the tests run, from FFmpeg's own synthetic
generators: the `testsrc2` video pattern and the `sine` tone. No third-party recording, no personal
library file, and no separately licensed redistributable asset is involved.

The repository stores **the recipe**, never the file. Samples are materialised under
`artifacts/test-media/`, a path Git ignores, and `eng/verify.ps1` fails if one reaches the working
tree.

## Manifest

`tests/ApSolutions.LocalMedia.MediaTests/Fixtures/media-manifest.json` is the source of truth. Each
row declares an identifier, relative path, container, video and audio codec, resolution, duration,
track counts, whether it is HDR, the encoders it needs, the exact recipe, and the expected outcome:
`Playable` or `ActionableUnsupported` with its domain failure code.

## Generating the matrix

```powershell
pwsh ./eng/generate-test-media.ps1 -Output artifacts/test-media
```

The script locates the encoder through the `FFMPEG_PATH` environment variable and, when that is not
set, through `PATH`. It reuses samples that already exist; `-Force` regenerates them. It finishes by
printing the size and SHA-256 of every sample, which is the verifiable provenance of that run.

## Required encoders

| Sample | Encoders |
|---|---|
| MP4 / H.264 / AAC | `libx264`, `aac` |
| Matroska / HEVC / E-AC-3 | `libx265`, `eac3` |
| AVI / MPEG-4 Part 2 / MP3 | `mpeg4`, `libmp3lame` |
| QuickTime / H.264 / PCM S16LE | `libx264`, `pcm_s16le` |
| WebM / VP9 / Opus | `libvpx-vp9`, `libopus` |
| Matroska / AV1 / Opus | `libsvtav1`, `libopus` |
| Matroska / H.264 without audio | `libx264` |
| Matroska / unsupported AVS2 | `libxavs2` |
| Truncated MP4 | derived from the first row |

When the local FFmpeg cannot provide an encoder, that row **skips with the exact reason**. It is
never replaced by an equivalent and never reported as passing.

## Failure rows

Three rows prove that a file which cannot be played produces an actionable diagnosis and leaves both
the file and the catalogue entity untouched:

- **AVS2 in Matroska**: a real, standardised codec the pinned LibVLC build has no decoder for. The
  container parses and the track is announced, but the engine reports no codec description and
  decodes no frame, so the diagnosis is `UnsupportedCodec`.
- **Truncated MP4**: the first 35% of the bytes of the first row, so the trailing index never
  arrives. The diagnosis is `CorruptedMedia`.
- **Absent file**: diagnosed as `FileNotFound` with a retry offered.

No recovery action deletes or rewrites the file.
