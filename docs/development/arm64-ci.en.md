# Testing on ARM64 with GitHub's runners

How AP Reelume runs on a Windows 11 ARM64 machine, what it answers, and the traps it carries. The
Spanish version is at [arm64-ci.es.md](arm64-ci.es.md).

This guide exists because the ARM64 side will be touched again every time something OS-dependent is
implemented, and what it cost to work out the first time should not cost twice.

## What is set up

The `arm64-matrix` job in [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) runs on
`runs-on: windows-11-arm` and executes [`eng/package-arm64.ps1`](../../eng/package-arm64.ps1), which
publishes the ARM64 package and, **only when the host is ARM64**, runs the six-phase matrix.

- **It costs nothing.** `windows-11-arm` runners are free and unlimited on public repositories, and
  this is one.
- **It runs in parallel with the x64 job** and carries its own clock, so it cannot eat into the
  margin [`eng/measure-ci-time.ps1`](../../eng/measure-ci-time.ps1) reports.
- **It does not block yet**, on purpose. The image is maintained by Arm Limited and is not the x64
  one; a red caused by a tool that image happens not to ship reads as broken code, which is the one
  thing a red must never mean.

## How the result is read, which is NOT the colour of the run

**A green run does not mean the phases passed**, because the job does not block and because the
script does not fail over an unpassed phase. What answers is the artifact:

```powershell
gh run download <id> -n arm64-matrix-native -D <folder>
```

It carries four things:

| File | What it answers |
|---|---|
| `arm64-probe.txt` | What the machine carried: architecture, SDK, Chocolatey, `ffmpeg`, `makeappx` |
| `package-arm64/arm64-matrix.json` | The six phases, with `outcome`, `detail` and `reason` |
| `package-arm64/lifecycle.json` | The install-cycle phases against the ARM64 package |
| `package-arm64/matrix/**/*.trx` | **Which** test skipped, not only how many |

The `.trx` files are there because the first time the skips had to be deduced from the log of an
already-finished job, and that is archaeology rather than evidence.

## What the image carries is NOT read: it is measured

**Its public documentation lies about itself.** The `actions/partner-runner-images` manifest
advertised `.NET 10.0.101` and `Chocolatey 2.6.0`; the machine carried `10.0.302` — the exact one
`global.json` pins — and `2.7.4`. That is why the job's first real step is a probe that runs whatever
happens next, and why the SDK is installed regardless: **a guard that depends on a third party not
changing its image is not a guard**.

What is worth knowing in advance, knowing it can change without notice:

- **It carries** Chocolatey, Visual Studio 2022, several Windows SDKs with `makeappx.exe` under
  `x64` **and** `arm64`, PowerShell 7 and `git`. That `makeappx` exists under `x64` is what lets
  [`eng/find-sdk-tool.ps1`](../../eng/find-sdk-tool.ps1) go on looking only there.
- **It does not carry `ffmpeg`.** It is installed from the same package at the same pinned version as
  on x64, and runs emulated there. That does not contaminate the measurement: `ffmpeg` **produces**
  the samples, which are files; what decodes them is native ARM64 LibVLC, which is what `PRD-003`
  commits to.
- **A hosted machine does show GUI windows.** The `native-execution` phase needs that, and passes.

## The six phases

The list lives in **two places that cross-check each other**, and a test fails if they stop
agreeing: the `$matrixPhases` array in `eng/package-arm64.ps1` and `RequiredPhases` in
`tests/ApSolutions.LocalMedia.MediaTests/Playback/Arm64PlaybackTests.cs`. **If you add a phase, you
touch both.**

| Phase | What it asks | What it needs |
|---|---|---|
| `native-execution` | The ARM64 host starts and reports ARM64 | Nothing but the machine |
| `codec-matrix` | The T19 matrix decodes natively | Samples from `ffmpeg` |
| `hdr-acceleration` | HDR10, tone mapping and the decode path | Samples from `ffmpeg` |
| `audio-output` | Device selection and the persisted preference | Nothing: it runs the engine muted |
| `package-lifecycle` | The install cycle against the ARM64 package | `lifecycle.json`, which the script produces by invoking `eng/verify-package.ps1` |
| `cross-architecture-data` | A library created on x64 opens on ARM64 | A data folder written on x64, passed with `-X64DataRoot` |

## Five traps, all already paid for

1. **The job does NOT run `verify.ps1` or the whole suite, and that is not an oversight.**
   `Arm64PlaybackTests` lives in `MediaTests` and, **on an ARM64 host, refuses any phase that is not
   `Passed`**. Running it while phases remain unpassed guarantees a red. Once all six pass, that
   suite becomes this job's natural gate.

2. **A zero exit code does not mean anything was measured.** `CodecMatrixTests` and
   `HdrAccelerationTests` skip themselves when `ffmpeg` is absent, and `dotnet test` returns 0 all
   the same. `Invoke-MediaSuite` counts what ran by reading the `.trx`, **not the console summary**,
   which is localised: this is developed in `es-ES` and CI runs `en-US`.

3. **The bar is «something ran and passed», not «zero skips».** Chocolatey's `ffmpeg` package
   carries neither `libsvtav1` nor `libxavs2`, and muxes the HDR10 sample without its colour-transfer
   metadata, so three tests skip — **and the x64 runner skips the same ones**. Demanding zero skips
   would tie `PRD-003`'s unblocking to a third party packaging an encoder.

4. **`| Write-Output` inside a PowerShell function destroys its return value.** The caller receives
   an array whose last element is the object, and asking the array for the object's properties
   **answers no**, silently. A test forbids it, and it looks at statements rather than comments —
   because it caught itself the first time.

5. **The install-cycle report is called `lifecycle.json` and `eng/verify-package.ps1` writes it into
   the root it is handed.** The phase looked for it under another name in another folder, so it said
   «there is no machine» with the machine right there. A block means the machine cannot answer,
   **never** that the script asked the wrong question.

## What it costs

**No fixed duration is written here**, because that is a figure that will always end up stale. The
first native run cost nine minutes end to end, six of them packaging, and the job's ceiling is set at
thirty with that measurement beside it. To find out what it costs today:

```powershell
pwsh -NoProfile -File eng/measure-ci-time.ps1 -Detailed
```

If the healthy job approaches the ceiling, what grew is the work rather than the ceiling being too
low.

## What is not done

**Emulating ARM64 is no substitute for this.** What `PRD-003` commits to is that **native** ARM64
code decodes and plays; a translation layer measures the translation layer. It is written down in
[`docs/evidence/stable/T42-arm64.md`](../evidence/stable/T42-arm64.md), along with what each phase
answered the first time.
