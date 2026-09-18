# Licence texts that travel inside the artifact

This folder is what packaging copies into the package as `licenses/`. It is not documentation about
the licences: **it is the delivery of the licences**. The Spanish version is in
[README.es.md](README.es.md).

The [third-party notices](../THIRD-PARTY-NOTICES.en.md) say which component carries which licence.
That is not enough. LGPL-2.1 (§6), GPL-2.0 (§1) and Apache-2.0 (§4a) require a copy of the licence to
**accompany** the binary distribution, and MIT and BSD-3-Clause require the copyright notice to be
reproduced. Naming the component in a table is neither of those.

## What is here

| File | What it is | What it satisfies |
|---|---|---|
| `Apache-2.0.txt` | Canonical text | SQLitePCLRaw (§4a) |
| `BSD-3-Clause.txt` | Canonical text | ANGLE and Skia |
| `GPL-2.0.txt` | Canonical text | No plugin since 2026-09-18; it is VLC's licence as a program, which `libvlc` displays for itself |
| `GPL-3.0.txt` | Canonical text | Travels with the LGPL-3.0, which is written on it and asks for both (§4b) |
| `LGPL-2.1.txt` | Canonical text | LibVLC, libvlccore and LibVLCSharp (§6) |
| `LGPL-3.0.txt` | Canonical text | GMP, Nettle and LIVE555, inside five LibVLC plugins (§4) |
| `MIT.txt` | Canonical text plus the notices of those who publish none | Avalonia, MicroCom, Tmds.DBus.Protocol, Microsoft and the .NET runtime |
| `NOTICE-ANGLE.txt` | Verbatim copy from the package | Avalonia.Angle.Windows.Natives |
| `NOTICE-BouncyCastle.txt` | Verbatim copy from the package | BouncyCastle.Cryptography |
| `NOTICE-HarfBuzzSharp.txt` | Verbatim copy from the package | HarfBuzzSharp |
| `NOTICE-SkiaSharp.txt` | Verbatim copy from the package | SkiaSharp |
| `NOTICE-Skia-HarfBuzz-natives.txt` | Verbatim copy from the package | Everything Skia and HarfBuzz carry inside: ANGLE, freetype, ICU, libpng, libwebp, zlib and twenty more |
| `NOTICE-SQLite.txt` | Verbatim copy from the package | SQLite (public domain) |
| `NOTICE-SQLitePCLRaw.txt` | Assembled notice | SQLitePCLRaw |
| `NOTICE-VideoLAN.txt` | Assembled notice | LibVLC and its plugins |

There is no catalogue licence text for the program itself: its own licence's own licence travels as `LICENSE` at the root of the package,
which is where anyone looks for it.

## Where each text came from

A licence text written from memory is not a copy of the licence. Each one was taken from a source
that already distributed it and contrasted with a second, independent copy before being accepted:

- **LGPL-2.1** and **Apache-2.0**: from the SPDX directory of a Blender installation, contrasted
  respectively with the copy Git for Windows ships with `xz` (identical byte for byte) and with the
  one in `dotnet-reportgenerator` (identical apart from an appendix already filled in with its
  holder).
- **GPL-2.0**: from VLC's own tree. VideoLAN's package carries it as the string `vlc_about.h`
  compiles into `libvlc`; it was extracted from there and contrasted with the copy HandBrake
  distributes, and they agree apart from one trailing blank line. It is the licence VLC displays for
  itself, which is exactly the one binding its plugins.
- **LGPL-3.0** and **GPL-3.0**: GMP 6.3.0's `COPYING.LESSERv3` and `COPYINGv3`, as they travel in the
  engine's source archive, checked against Nettle 3.7.3's: identical byte for byte. They are the texts
  those libraries deliver with their code, which is what binds them.
- **BSD-3-Clause**: from the same SPDX directory. Its reproduction with a concrete holder is
  `NOTICE-ANGLE.txt`, which is the file ANGLE's own package publishes.
- **MIT**: the canonical text, with the copyright notice each package declares in its metadata.

Verbatim copies are taken from the restored NuGet package, never transcribed. `LicenceTextTests`
compares them byte for byte against the package the build consumed, so a version bump that changes a
notice turns the test red instead of leaving the artifact distributing the previous version's notice.

## What holds this in place

- `LicenceTextTests` — every licence the notices declare has its text here, every text is whole,
  every verbatim copy matches its package, no new identifier passes unfiled, and the LGPL-3.0 never
  travels without the GPL-3.0.
- `ArtifactContentsTests` and `Arm64PackageTests` — everything in this folder reaches `licenses/`
  inside both artifacts, with the same contents.

## What is still open

The package carries the licences; the `REL-004` legal opinion is still pending and belongs to
whoever publishes, not to whoever writes code. Since 2026-09-18 the engine is a build of our own
without GPL, so no plugin needs the GPL-2.0 §3 offer any more. The point left for it is which
subsection of LGPL-2.1 §6 covers the way LibVLC travels here — a modified dynamic library, with its
source attached, and replaceable. The `LGPL-3.0` of gmp, nettle and live555 was read on 2026-09-18
(`ENG-028`) and the conclusion is in `LEGAL`.
