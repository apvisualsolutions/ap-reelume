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
| LibVLC, built by AP Solutions without GPL | 3.0.23-nogpl.1 | LGPL-2.1-or-later |
| GNU MP (GMP), inside four LibVLC plugins | 6.3.0 | LGPL-3.0-or-later, chosen from its dual licence with GPL-2.0-or-later |
| GNU Nettle, inside four LibVLC plugins | 3.7.3 | LGPL-3.0-or-later, chosen from its dual licence with GPL-2.0-or-later |
| LIVE555 Streaming Media, inside one LibVLC plugin | 2016.11.28 | LGPL-3.0-or-later |
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
[`licenses/NOTICE-Skia-HarfBuzz-natives.txt`](licenses/NOTICE-Skia-HarfBuzz-natives.txt) — and since
2026-09-18 also inside LibVLC, which is built without GPL (`ENG-013`) and uses it to draw subtitle
text. The acknowledgement is written once and covers both.

### The .NET runtime

The artifact is self-contained: it carries its own copy of the .NET 10 runtime and base class
library (`coreclr.dll`, `System.*.dll`, `mscorlib.dll` and their companions), plus the Windows SDK
projection (`Microsoft.Windows.SDK.NET.dll`, `WinRT.Runtime.dll`). All of it is published by
Microsoft under `MIT`. Nobody has to install a runtime to run AP Reelume, and that convenience is
what puts several hundred Microsoft-licensed files inside the package.

### LibVLC, its core and its plugins

**Since 2026-09-18 the engine is not VideoLAN's package but a build of our own without GPL**
(`ENG-013`). It is built from the same VLC release, 3.0.23, with VideoLAN's build script and build
images, third-party libraries configured with `--disable-gpl`, and FreeType under its FTL. Every plugin
whose source code is GPL is then removed, and a gate demands that none is left. This repository
publishes it as `libvlc-3.0.23-nogpl.1`, pinned by the hash of each file. **It is modified**, and both
modifications take GPL pieces out: the yadif deinterlacing algorithm from the `libdeinterlace` plugin,
and the libdvdread library from the build script. The files touched say so in their header, with a
date, as LGPL-2.1 §2(b) asks.

Everything that travels — `libvlc.dll`, `libvlccore.dll` and the plugins in `plugins/` — is
`LGPL-2.1-or-later`. The text travels as `licenses/LGPL-2.1.txt`, and `licenses/NOTICE-VideoLAN.txt`
details the build and where its source code is. Which plugins are missing compared with VideoLAN's
package, and why, is recorded in the `manifest.json` published with the engine: the ones that were GPL,
and the ones neither that package nor this build uses.

**Three third-party libraries are linked inside five plugins under `LGPL-3.0-or-later`**, and
VideoLAN's package carried them too: GNU MP and GNU Nettle inside `libgnutls`, `libaccess_srt`,
`libaccess_output_srt` and `libdcp`, and LIVE555 inside `liblive555`, on both architectures. GMP and
Nettle are offered under a dual licence, LGPL-3.0 or GPL-2.0, and the LGPL is the one used. Their
copyright notices and the detail are in `licenses/NOTICE-VideoLAN.txt`, and the texts in
`licenses/LGPL-3.0.txt` and `licenses/GPL-3.0.txt`, because the LGPL-3.0 is written on top of the
GPL-3.0 and asks for both. How that licence fits a proprietary program was read on 2026-09-18
(`ENG-028`) and is in `LEGAL`.

**And «everything is LGPL-2.1» is VLC's licence, not the inventory of what its plugins carry
inside.** Besides those three, other third-party libraries are linked in under licences of their own
— SRT, for one, is MPL-2.0 — and that full inventory has not been made (`ENG-029`).

**What was there before, and why it had to change.** The `VideoLAN.LibVLC.Windows` 3.0.23.1 package
carried fourteen GPL plugins on x64 and eleven on ARM64. `libavcodec_plugin.dll` and
`libswscale_plugin.dll` were GPL because their FFmpeg was built with `--enable-gpl`. Eleven more were
because their source code is GPL, among them `liblua`, `libdeinterlace` and `libhqdn3d`, and
`libts_plugin.dll` because it links aribb24. The list comes from `eng/libvlc/scan-plugin-licenses.ps1`
(evidence `audit-eng027-plugin-gpl-sources.md`).

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

**While those plugins travelled here the artifact could not be distributed, and that is what the build
of our own closed.** It costs no formats: the decoding library is permissive by default, and what was
copyleft were optional pieces enabled at build time — a legacy post-processing filter and some
optimisations — **with no decoder among them**. Measured on real Windows x64 and ARM64: the fourteen
promised codecs decode with the same figures as VideoLAN's package, subtitles included (evidence
`ENG013-reproducible-build.md`).

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
professional legal opinion under REL-004 answers them. **The first was closed by engineering on
2026-09-18**: since 2026-09-13 it had been a finding — the GPL plugins in VideoLAN's package were not
compatible with the program's own licence and blocked release — and since 2026-09-18 the engine is
built without them. The `LGPL-3.0` of three libraries was read the same day (`ENG-028`): both that licence and the
LGPL-2.1 ask that the program may be modified for one's own use and reverse engineered to debug that,
and `LICENSE` has permitted it since then through an express exception. The
second is still a question: which subsection of
LGPL-2.1 §6 covers the way LibVLC travels here, now that the §6(a) route — publishing our source under
a free licence — is no longer available. Both are named here so nobody mistakes this document for the
opinion.
