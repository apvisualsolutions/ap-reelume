# Third-party notices

AP Reelume by AP Solutions is released under `GPL-3.0-or-later`. This document records the
third-party components the solution consumes today and the licence declared by the restored package.
This file is updated by every increment that adds or removes a dependency, and **travels inside the
artifact**, under `licenses/`, alongside its Spanish version.

The bill of materials for the exact build you are running is in `sbom/`, inside the same artifact, in
CycloneDX 1.5 and SPDX 2.3 formats. It is generated from the lock files, so it describes what the
build resolved rather than what the projects ask for.

## Components distributed with the application

| Component | Version | Declared licence |
|---|---|---|
| Avalonia | 12.1.1 | MIT |
| Avalonia.Desktop | 12.1.1 | MIT |
| Avalonia.Themes.Fluent | 12.1.1 | MIT |
| LibVLCSharp | 3.10.0 | LGPL-2.1-or-later |
| VideoLAN.LibVLC.Windows | 3.0.23.1 | LGPL-2.1-or-later |
| Microsoft.Data.Sqlite | 10.0.10 | MIT |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 | Apache-2.0 over SQLite, which is public domain |
| Microsoft.Extensions.DependencyInjection | 10.0.10 | MIT |

`GPL-3.0-or-later` is compatible with incorporating `LGPL-2.1-or-later`, `MIT`, and `Apache-2.0`
components. The native LibVLC library is distributed unmodified and keeps its own licence notice
inside the package; the MVP artifact must ship it in full.

## Components used only during development and testing

| Component | Version | Declared licence |
|---|---|---|
| Avalonia.Headless.XUnit | 12.1.1 | MIT |
| BenchmarkDotNet | 0.15.8 | MIT |
| coverlet.collector | 10.0.1 | MIT |
| FlaUI.Core | 5.0.0 | MIT |
| FlaUI.UIA3 | 5.0.0 | MIT |
| FsCheck | 3.3.4 | BSD-3-Clause |
| Microsoft.NET.Test.Sdk | 18.8.1 | MIT |
| NSubstitute | 6.0.0 | BSD-3-Clause |
| xunit.v3 | 3.2.2 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 |

## Declared but unconsumed versions

`Directory.Packages.props` pins two versions no project currently references. They stay declared so a
future adoption cannot introduce a floating range, and they are part of no artifact:

- `LibVLCSharp.Avalonia` 3.10.0 — not adopted because it targets Avalonia 11.x and would expose the
  engine's player object to the view.
- `NetArchTest.Rules` 1.3.2 — not restored by the current solution; architecture rules are checked by
  reading the project files.

## External tools that are not redistributed

The container and codec matrix is generated with **FFmpeg**, which must be installed on the
development machine and is located through `FFMPEG_PATH` or `PATH`. FFmpeg is not included in the
repository or in any published artifact, and its licence depends on the build each person installs.
The samples it produces come from its `testsrc2` and `sine` synthetic generators, so the resulting
content incorporates no third-party work.

## Media content

No video, audio, or subtitle file is version-controlled. The personal library is never read or
copied during the tests.
