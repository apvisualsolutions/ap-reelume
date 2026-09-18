# Third-party notices

AP Reelume by AP Solutions is released under a licence of its own, `LicenseRef-APSolutions`,
whose text is in `LICENSE`. This document records the
third-party components the published artifact carries and the licence each one declares. This file is
updated by every increment that adds or removes a dependency, and **travels inside the artifact**,
under `licenses/`, alongside its Spanish version.

The bill of materials for the exact build you are running is in `sbom/`, inside the same artifact, in
CycloneDX 1.5 and SPDX 2.3 formats. It is generated from the lock files, so it describes what the
build resolved rather than what the projects ask for. The tables below are checked against that bill
of materials by `ThirdPartyNoticeTests`, so a dependency cannot enter the artifact without appearing
here.

## Components distributed with the application

### Managed libraries and their native assets

Every component in this table ships inside the `win-x64` and `win-arm64` artifacts. Transitive
dependencies are listed by name because a licence obligation does not care whether a package was
asked for directly.

| Component | Version | Declared licence |
|---|---|---|
| Avalonia | 12.1.1 | MIT |
| Avalonia.Desktop | 12.1.1 | MIT |
| Avalonia.Themes.Fluent | 12.1.1 | MIT |
| Avalonia.BuildServices | 11.3.2 | MIT |
| Avalonia.FreeDesktop | 12.1.1 | MIT |
| Avalonia.FreeDesktop.AtSpi | 12.1.1 | MIT |
| Avalonia.HarfBuzz | 12.1.1 | MIT |
| Avalonia.Native | 12.1.1 | MIT |
| Avalonia.Remote.Protocol | 12.1.1 | MIT |
| Avalonia.Skia | 12.1.1 | MIT |
| Avalonia.Win32 | 12.1.1 | MIT |
| Avalonia.X11 | 12.1.1 | MIT |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | BSD-3-Clause, by The ANGLE Project Authors |
| SkiaSharp | 3.119.4 | MIT |
| SkiaSharp.NativeAssets.Win32 | 3.119.4 | MIT, over Skia, which is BSD-3-Clause by Google |
| HarfBuzzSharp | 8.3.1.3 | MIT |
| HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.3 | MIT, over HarfBuzz, which is MIT |
| MicroCom.Runtime | 0.11.6 | MIT |
| Tmds.DBus.Protocol | 0.94.1 | MIT |
| BouncyCastle.Cryptography | 2.7.0 | MIT |
| LibVLCSharp | 3.10.0 | LGPL-2.1-or-later |
| VideoLAN.LibVLC.Windows | 3.0.23.1 | LGPL-2.1-or-later for the core; see the plugins below |
| Microsoft.Data.Sqlite | 10.0.10 | MIT |
| Microsoft.Data.Sqlite.Core | 10.0.10 | MIT |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.11 | Apache-2.0 |
| SQLitePCLRaw.core | 2.1.11 | Apache-2.0 |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.11 | Apache-2.0 |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 | Apache-2.0 over SQLite, which is public domain |
| Microsoft.Extensions.DependencyInjection | 10.0.10 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.10 | MIT |

A proprietary licence is compatible with incorporating `LGPL-2.1-or-later`, `MIT`, `Apache-2.0`, and
`BSD-3-Clause` components. The MIT and BSD-3-Clause licences require their copyright notice to travel
with the binary, which is what this file and the `licenses/` folder inside the artifact are for.

That folder also carries **the full text of every licence** and the copyright notices each package
publishes. What is in it and where each text came from is in
[licenses/README.en.md](licenses/README.en.md), which travels with them.

### FreeType, and why it carries its own acknowledgement

> Portions of this software are copyright © The FreeType Project (www.freetype.org).
> All rights reserved.

**That sentence is an obligation, not a courtesy**, and this section exists to meet it. FreeType is
distributed under **two mutually exclusive licences** and one must be chosen: the **FreeType License**
(FTL), BSD-like, or the **GPL-2.0**. AP Solutions chooses the **FTL**, decided by the owner on
2026-09-14, because the other route is incompatible with a proprietary licence — and FreeType's own
`LICENSE.TXT` says the FTL "is suited to products which don't use the GNU General Public License".

The price of that route is its advertising clause, which asks literally to "acknowledge somewhere in
your documentation that you have used the FreeType code". That is what the box above does.

**And it arrives by two paths, one of them from before this decision**: today it comes inside Skia's
native assets — its full text is in
[`licenses/NOTICE-Skia-HarfBuzz-natives.txt`](licenses/NOTICE-Skia-HarfBuzz-natives.txt) — and it will
also come inside LibVLC once the engine is built without GPL (`ENG-013`), because it is what draws
subtitle text. The acknowledgement is written once and covers both.

### The .NET runtime

The artifact is self-contained: it carries its own copy of the .NET 10 runtime and base class
library (`coreclr.dll`, `System.*.dll`, `mscorlib.dll` and their companions), plus the Windows SDK
projection (`Microsoft.Windows.SDK.NET.dll`, `WinRT.Runtime.dll`). All of it is published by
Microsoft under `MIT`. Nobody has to install a runtime to run AP Reelume, and that convenience is
what puts several hundred Microsoft-licensed files inside the package.

### LibVLC, its core and its plugins

The `VideoLAN.LibVLC.Windows` package declares `LGPL-2.1-or-later`, which covers `libvlc.dll` and
`libvlccore.dll`. It also ships roughly three hundred plugins in `plugins/`, and **those carry their
own licences**, some of them GPL rather than LGPL: fourteen on x64 and eleven on ARM64.
`libavcodec_plugin.dll` and `libswscale_plugin.dll` are GPL because their FFmpeg build was compiled
with `--enable-gpl`; `libx26410b`, `liblua`, `libdeinterlace`, `libhqdn3d`,
`libheadphone_channel_mixer`, `libdolby_surround_decoder`, `libvisual`, `libremoteosd` and, on x64,
`libglspectrum`, `libi420_rgb_mmx` and `libi420_rgb_sse2`, because their sources are; and
`libts_plugin.dll` because it links the aribb24 library. The list comes from
`eng/libvlc/scan-plugin-licenses.ps1` (evidence `audit-eng027-plugin-gpl-sources.md`). The library is distributed unmodified. VideoLAN's
NuGet package carries **no `COPYING` file at all**, so nobody supplies the text but this artifact: it
carries it as `licenses/LGPL-2.1.txt`, `licenses/GPL-2.0.txt` and `licenses/NOTICE-VideoLAN.txt`.

**This was closed on 2026-08-10 and reopened on 2026-09-13, and not because it was wrong.** The
reasoning then was: for a program released under `GPL-3.0-or-later`, a `GPL-2.0-or-later` plugin is
compatible, because the "or later" makes the two meet at GPL-3.0. That was correct and still is.
**What changed is the program**: since 2026-09-13 it carries a licence of its own, so there is no
common version to meet at, and a copyleft plugin inside a proprietary program is a breach rather than
a fit.

**And trimming the plugin set is no longer optional: it is the only way out.** Of the fourteen, the
ones that matter are **not the x264 encoder** — a player does not encode and that one can go — but
**`libavcodec_plugin.dll` and `libswscale_plugin.dll`, the decoders**: their build configuration line
begins with `--enable-gpl`, read inside both binaries. VLC's GPL modules are features the application
does not offer, a deinterlacing algorithm that is not the default and a colour conversion
`libswscale` also does; `libts` without aribb24 still reads `.ts`.

**While that plugin travels here, the artifact cannot be distributed.** The measured way out costs no
formats: the decoding library is permissive by default, and what is copyleft are optional pieces
enabled at build time — a legacy post-processing filter and some optimisations — **with no decoder
among them**. LibVLC and its dependencies have to be built without that option, for both
architectures, and that build maintained.

**The licence texts now travel.** LGPL-2.1 (§6), GPL-2.0 (§1), and Apache-2.0 (§4a) each require a
copy of the licence to accompany a binary distribution, and MIT and BSD-3-Clause require their
copyright notice to be reproduced. Naming the component and its licence, which is what this document
does, is not the same as accompanying it, so the package's `licenses/` folder carries the full texts
alongside these notices. The ones a package publishes are copied verbatim from it and a test compares
them byte for byte against the package the build consumed; the canonical ones were taken from a source
that already distributed them and contrasted with a second, independent copy. The detail is in
[licenses/README.en.md](licenses/README.en.md).

### Ported code, which no automatic gate sees

**This is written by hand, and that is why it stands apart.** Every other row in this document comes
from `packages.lock.json` and `ThirdPartyNoticeTests` demands it; a **ported** source file is not a
packaged dependency, so it appears in no lock file and no test would miss it. The licence obligation
is the same.

| Origin | Source file | Declared licence | What was taken |
| --- | --- | --- | --- |
| VideoLAN / VLC | `modules/video_output/win32/d3d11_scaler.cpp` | `LGPL-2.1-or-later` | The identifiers of NVIDIA's and Intel's super-resolution extensions, their payloads, and the Direct3D 11 video processor call sequence, in `src/ApSolutions.LocalMedia.Windows/Playback/`. |
| The Chromium Authors | `ui/gl/swap_chain_presenter.cc` | `BSD-3-Clause` | Which door each Intel call goes through — the first two are output extensions and only the third is a stream one — and that NVIDIA's driver accepts the request and ignores it while the feature is switched off. |

A proprietary licence admits incorporating both. The full texts of `LGPL-2.1` and `BSD-3-Clause`
already travel in `licenses/` for other dependencies, so neither needs adding.

## Components used only during development and testing

These never enter an artifact. They build it, test it, or measure it.

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

## What this document does not settle

This file states what each component declares and how those declarations fit together. It is written
by the people who assembled the software, not by a lawyer, and two questions stay open until the
professional legal opinion under REL-004 answers them. **Since 2026-09-13 the first is no longer a
question but a finding**: the VideoLAN plugins shipped in the pinned build are **not** compatible with
the program's own licence, because at least the decoder is copyleft, and that **does block release**
even though it does not block development. The second is still a question: which subsection of
LGPL-2.1 §6 covers the way LibVLC travels here, now that the §6(a) route — publishing our source under
a free licence — is no longer available. Both are named here so nobody mistakes this document for the
opinion.
