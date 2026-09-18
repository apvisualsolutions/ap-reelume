# Legal status

What is settled legally, what has been corrected, and what stays open. It is written by the people
who build the program, not by a lawyer: this document **is not an opinion** and does not replace one.
Its use is that nobody has to guess where the edges are.

Last full review: 2026-08-10, over the public repository.

## The program's licence

AP Reelume by AP Solutions is published under **a licence of its own**, identified as
`LicenseRef-APSolutions`: free of charge for whoever uses it, with no right to modify, redistribute or
sell it. The full text is in [LICENSE](../../LICENSE) and the product attribution in
[NOTICE](../../NOTICE).

**Until 2026-09-13 the program was `GPL-3.0-or-later`**, and the owner changed it for a business
reason rather than a technical one: the free licence granted anyone the right to modify, redistribute
and sell the program, which are the three things he wants to keep. Everything published up to that
date keeps its rights forever; this licence governs from then on. What made it possible is a measured
fact: **the repository's 646 commits have a single author**, so there is no third-party copyright to
relicense.

**The identifier carries no version, and that is deliberate.** It used to name a specific licence, so
the day it changed **1,033 files** had to change with it. A `LicenseRef-` names "this project's own
licence" and points at the document; the version and the date live inside `LICENSE`, which is where a
reader has to go anyway to learn the terms. Rewording those terms now touches no source file at all.

Since the 2026-08-10 review, **every source file carries its SPDX header**: 925 `.cs` files, 71
`.axaml`, and 29 `.ps1` declare `SPDX-License-Identifier: LicenseRef-APSolutions` next to the copyright
holder. (The earlier figures — 556, 51 and 17 — had been stale for a long time: **no gate checks
them**, and that is still true.) A licence that lives only in `LICENSE` stops being attached to a file the moment somebody
copies it out of the tree; the header travels with it. The `IDE0073` rule demands it in
`.editorconfig`, so `dotnet format --verify-no-changes` — a gate that already ran — rejects a new
file without one.

## Warranty disclaimer

The program comes with **no warranty whatsoever**, to the extent permitted by applicable law.
Sections 6 and 7 of [LICENSE](../../LICENSE) say so in full — including that, the program being free
of charge, the liability cap is zero euros — and neither the README nor the application promises
anything else. In particular, because these are the confusions that actually happen:

- The artifact is **not Authenticode-signed**, and SmartScreen will warn. This is explained in
  [SMARTSCREEN.en.md](../release/SMARTSCREEN.en.md); the release signature — minisign over the
  digests — is a different layer proving a different thing.
- Automatic segment detection, title identification, and recommendations are **local estimates**, not
  assertions about the works themselves.
- Nothing the application infers about a library implies any right over its content. Whoever plays a
  file is responsible for having it.

## Third-party components

The inventory checked against the real build, with each component's licence, is in
[the third-party notices](../release/THIRD-PARTY-NOTICES.en.md), and `ThirdPartyNoticeTests` stops a
dependency from entering the artifact without appearing there.

Compatibility: a proprietary licence allows incorporating `LGPL-2.1-or-later`, `MIT`, `Apache-2.0`
and `BSD-3-Clause` while meeting their conditions — which for the LGPL are those of §6, and they are
met because the libraries travel as separate files anyone may replace. **What it does not allow is
GPL code.**

**VideoLAN's GPL plugins — CLOSED by engineering on 2026-09-18 (`ENG-013`).** The application no
longer carries VideoLAN's package: it carries LibVLC built by this repository from the same VLC
release, without GPL code, verified by a gate that reads each plugin's licence in its sources, and
pinned by hash (`eng/libvlc/libvlc.lock.json`). It is **modified** — the yadif algorithm and
libdvdread, the two GPL pieces that were not a whole plugin, were taken out — and the files touched
say so with a date, as LGPL-2.1 §2(b) asks. Its corresponding source travels with every release (see
below).

**The `LGPL-3.0` of gmp, nettle and live555 — read on 2026-09-18 (`ENG-028`), with one condition
pending on the owner.** The three are linked statically inside five plugins, measured in the binaries
of both architectures: gmp and nettle in `libgnutls`, in the two SRT ones and in `libdcp`; live555 in
`liblive555`. `libvlc.dll` and `libvlccore.dll` carry none of them. gmp and nettle are dual-licensed —
LGPL-3.0-or-later or GPL-2.0-or-later, read in their own sources — and the LGPL is the one used;
live555 is LGPL-3.0-or-later. Against LGPL-3.0 §4:

- **The combined work is each plugin**, and all of its code is open; its complete source, with the
  scripts that link it, travels with every release. That is §4(d)(0). The plugins are separate files
  the program loads at start-up and anybody may replace.
- **Notice and texts (§4a and §4b)**: the third-party notices name them with their plugins and their
  copyrights, and `licenses/` carries `LGPL-3.0.txt` and `GPL-3.0.txt`, because the LGPL-3.0 is written
  on top of the GPL-3.0 and asks for both. They were missing until this day; `LicenceTextTests` keeps
  them from going missing again.
- **Installation Information (§4e)**: not owed. It comes from GPL-3.0 §6 and only reaches a «User
  Product», a tangible consumer device, which a downloaded program is not.
- **What remains, and it is not only the LGPL-3.0's**: §4 asks that the terms of the whole do not
  restrict modifying the library's portions nor «reverse engineering for debugging such
  modifications». LGPL-2.1 §6 asks the same for LibVLC, and more plainly, because the program is the
  work that uses that library: its terms must permit «modification of the work for the customer's own
  use and reverse engineering for debugging such modifications». **`LICENSE` 2.1 and 2.4 forbid both.**
  Clause 4 preserves the rights third-party licences grant over their components, but grants nothing
  over the program, which is what is asked for. The way out is an express exception in `LICENSE`,
  which is the owner's decision and is pending. Until it goes in, **releasing would breach the
  LGPL-2.1**, with or without these three libraries.

`--disable-gnuv3` is not the way out: it would remove the three libraries — and, presumably with
nettle, access to encrypted streams, which is not measured — and leave the LGPL-2.1 problem untouched.

What follows is how it opened, and it is kept because it is the example of a correct conclusion
ceasing to be one without anybody touching the code. This point was closed on 2026-08-10 **with the
opposite reasoning**, and how it inverted is worth reading. The argument
then was: VLC's tree carries the GPL version 2 **with** the "either version 2 of the License, or (at
your option) any later version" clause, so a `GPL-2.0-or-later` plugin rises to GPL-3.0 and **sits
inside a GPL-3.0 program**. The reasoning was valid and still is; what changed is the premise — **the
program is no longer GPL**, so there is no version to rise to.

**It is fourteen plugins on x64 and eleven on ARM64, for three different reasons**, read on 2026-09-18
from the VLC 3.0.23 sources with `eng/libvlc/scan-plugin-licenses.ps1` (evidence
`audit-eng027-plugin-gpl-sources.md`). Until that day this paragraph said "two", and then "three":
both figures came from searching the binaries for `--enable-gpl`, and only FFmpeg writes that string.

- **FFmpeg built as GPL**: `libavcodec_plugin.dll` and `libswscale_plugin.dll`, which share one build
  and carry the string inside.
- **VLC's own modules with GPL source files**, which carry no string that says so: `libx26410b`
  (x264), `liblua`, `libdeinterlace` (for its yadif algorithm), `libhqdn3d`,
  `libheadphone_channel_mixer`, `libdolby_surround_decoder`, `libvisual` and `libremoteosd`; on x64
  also `libglspectrum`, `libi420_rgb_mmx` and `libi420_rgb_sse2`.
- **A GPL library linked into an LGPL module**: `libts_plugin.dll`, the `.ts` demuxer, carries
  aribb24, recognisable by its own log messages inside the binary.

**The core, `libvlc.dll` and `libvlccore.dll`, is clean.** No artifact has been distributed: the
repository has no releases.

**The practical consequence was hard while it lasted**: with those plugins inside the package, **the
artifact could not be distributed** under the proprietary licence, and packaging was suspended from
2026-09-13 to 2026-09-18.

**The way out was measured and removes nothing the program plays.** The decoding library is
`LGPL` by default, and in FFmpeg what is copyleft are **optional** pieces enabled at build time — a
legacy post-processing filter and some optimisations; **no decoder is among them**. VLC's GPL modules
are features the application does not offer (web playlists, visualisations, VNC, headphone filters),
a deinterlacing algorithm that is not the default and is removed with a patch, and MMX/SSE2 colour
conversion that `libswscale` also does. `libts` without aribb24 still reads `.ts`; it loses Japanese
broadcast subtitles. The route was to build LibVLC and its dependencies without GPL, for both
architectures, drop those modules, and maintain that build (`ENG-013`): the fourteen promised codecs
decode with the same figures as VideoLAN's package on real Windows x64 and ARM64, subtitles included.
That is permanent infrastructure work, not a loss of formats.

**The licence texts now travel — closed on 2026-08-10.** This was the open breach: the artifact
carried AP Reelume's `LICENSE` and the third-party notices, but **not the text of the other
licences**, and VideoLAN's NuGet package carries no `COPYING` at all, so nobody was supplying it. The
obligations are explicit and a table naming the component does not meet them: LGPL-2.1 §6, GPL-2.0 §1,
and Apache-2.0 §4a each require a copy of the licence to **accompany** the binary distribution, and
MIT and BSD-3-Clause require their copyright notice to be reproduced. `licenses/`, inside both
artifacts, now carries the full text of LGPL-2.1, GPL-2.0, Apache-2.0, MIT and BSD-3-Clause, plus the
copyright notices of ANGLE, Skia, HarfBuzz, BouncyCastle, SQLitePCLRaw, SQLite and VideoLAN. The ones
a package publishes are copied verbatim from it — `LicenceTextTests` compares them byte for byte
against the package the build consumed, so a version bump that changes a notice turns the test red —
and the canonical ones were taken from a source that already distributed them and contrasted with a
second, independent copy. The inventory and the provenance of each text are in
[licenses/README.en.md](../release/licenses/README.en.md), and what was measured is in
[audit-legal-licence-texts.md](../evidence/stable/audit-legal-licence-texts.md).

**This question was closed on 2026-08-14, by choosing the option that needs no interpreting rather
than by commissioning someone to interpret.** It asked which subsection of LGPL-2.1 §6 covers the way
LibVLC travels here, and whether the written offer is enough for GPL-2.0 §3. Subsection 6(b) — the one
you would expect for a dynamic library — wants a mechanism that uses "a copy of the library already
present on the user's computer system", and here the DLLs arrive with the artifact, so its first
condition is not **literally** met. But 6(d) and the last paragraph of §3 say the same thing and say
it unconditionally: where the executable is offered for download from a designated place, offering the
source from that same place is distributing it.

That is what the release does now: `eng/fetch-corresponding-source.ps1` fetches `vlc-3.0.23.tar.xz` —
verified against the digest VideoLAN publishes — along with the `LibVLCSharp 3.10.0` archive and, since
2026-09-18, the source of the engine's own build — patches, scripts and the tarball of every
third-party library it used, verified against the SHA-512 this repository pins — and attaches them
beside the binaries. The written offer stays for channels where "the same place" means nothing,
such as a store, and it now explicitly stands for any third party. Two things were corrected along the
way: the notice named `libvlc 3.0.23.1` — a version whose source **does not exist**, that fourth digit
belonging to the NuGet package — and it did not mention that the work using the library is this
program. Measured in
[audit-corresponding-source.md](../evidence/stable/audit-corresponding-source.md).

**And §6(a) stopped being available on 2026-09-13.** Until then it was met the easy way: publishing
this program's source, which was free software, was enough for anyone to relink LibVLC. A proprietary
licence closes that door, and compliance now rests on the option the LGPL offers right beside it and
which was already true here in fact: **the libraries travel as separate files** that anyone may
replace with their own build without touching the program. That is the usual way a closed program
uses an LGPL library, and it is sound; what no longer holds is the earlier argument, which is why it
is written here rather than deleted.

None of this is a legal opinion or a substitute for one: it is compliance checkable against the text of
the licences, and what it achieves is that no interpretation is needed.

## The TMDB API

The application queries `api.themoviedb.org` only if you place a token in
`AP_LOCALMEDIA_TMDB_TOKEN`; **the artifact carries none**. On their terms of use:

- **Attribution.** The terms fix the sentence, not its gist. Until the 2026-08-10 review the program
  displayed a summary — "uses the TMDB API… not endorsed or certified" — and it now states the
  required sentence, in both languages, in Credits, in `NOTICE`, and in both READMEs, with a test
  pinning it character by character.
- **Retention.** The terms forbid keeping anything obtained from TMDB for longer than six months. The
  cache's soft expiry (one day) was not enough on its own: when the network failed or the token went
  away, the program served the stored copy **with no age limit at all**. There is now a hard floor of
  180 days (`TmdbOptions.RetentionLimit`): past it the entry is not served and **is deleted**.
- **Commercial use.** The terms reserve it for a separate written agreement. AP Reelume is supplied
  free of charge and derives no revenue from TMDB or its content, so it does not apply today. **The
  2026-09-13 licence change does not trigger it** — what decides is whether money is charged, not how
  the program is licensed — but it does bring the day closer: the new licence exists precisely to
  leave the door to charging open. If the program is ever charged for, this point changes and must be
  read again first.
- **Logo — closed on 2026-08-10.** The terms ask that TMDB's use be identified **with their logo**,
  less prominent than the product's own. Credits shows it as of this session, above the attribution
  sentence, with alternative text and no link: it identifies where the data comes from, it does not
  invite navigation. The file is the one TMDB publishes — its SHA-256 matches the digest they
  themselves embed in the asset's address, and a test checks it — and what the view draws is their
  vector rather than an imitation.

## GitHub's terms

The repository is hosted on GitHub, and the updater queries `api.github.com` and downloads from
`github.com` and its storage. Publishing code in a public repository is the use their Terms of
Service anticipate, **but since 2026-09-13 the relationship has inverted and it is worth being clear
about it**: those terms grant any user, of their own force, the right to view the repository and to
**fork** it within the platform itself, and the program's licence now grants **less** than that.
GitHub does not grant it on AP Solutions' behalf, and AP Solutions cannot withdraw it while the
repository is public; section 5 of [LICENSE](../../LICENSE) acknowledges this in writing and reserves
everything else, rather than forbidding something the platform already permits. **The repository
stays public on purpose**: the project's free build machines depend on it, the ARM64 ones included. No GitHub API requiring
authentication or an additional agreement is used: the updater's requests are anonymous reads of
public releases.

## Cryptography and export

The artifact carries cryptography in two places: **BouncyCastle** (Ed25519 and Blake2b) to verify the
minisign signature over the published digests, and .NET's own runtime for TLS. There is no
encryption of user data at rest.

That places the program in the category of encryption software published as publicly available source
code. Under the United States export regulations (EAR), that category normally relies on the TSU
exception at §740.13(e), which **requires an email notification** to BIS and to the ENC Encryption
Request Coordinator stating the address where the code is available. The repository is hosted in the
United States, so the rule applies.

**Status: no record that the notification was sent.** It is an owner action of practically no cost —
one email with the repository URL — and part of what the professional opinion should confirm. It is
named here so it does not get lost.

## Trademark, domain, and public name

`REL-004` in [the feature matrix](../FEATURES.md) records the formal trademark, domain, and Store
clearance for "AP Reelume by AP Solutions". The naming decision is in
[ADR-0001](../adr/0001-public-product-name.md), with a preliminary check that **does not replace**
the final report.

## What stays with the owner

None of these can be closed by whoever writes code, and none of them blocks development:

| Point | What is missing | Where it lives |
|---|---|---|
| Professional legal opinion | Engage a professional covering licence, third parties, TMDB, export, and trademark | `REL-004` |
| Export notification | Email BIS and ENC with the repository URL. It goes from your identity, which is why it is yours; the text is below, ready to copy | this page |
| Trademark and domain | Formal `REL-004` report | `REL-004`, ADR-0001 |
| Authenticode signing | Postponed economic decision, already documented | SMARTSCREEN |

Two points left this list on 2026-08-10, settled rather than delegated: **VideoLAN's plugins**
(checked to be `GPL-2.0-or-later`, which is compatible) and the **TMDB logo**, which was never a
decision but a condition of their terms. It has been in since that same date; below is how, and what
was measured to correct the figure the specification carried wrong.

### The TMDB logo, incorporated

Their terms ask that TMDB's use be identified with their logo, "less prominent" than the product's
own. That was not a branding choice worth postponing: it is part of the condition under which the API
is used, exactly like the attribution sentence. This is how it landed:

- The official file came from TMDB's brand page and ships version-controlled at
  `src/ApSolutions.LocalMedia.Presentation/Assets/tmdb-logo.svg`; it is never fetched at runtime. Its
  authenticity is checkable without trusting whoever downloaded it: TMDB embeds the asset's SHA-256
  in its own address, and `TmdbLogoTests` compares the file against it.
- It sits in Credits, above the attribution sentence. **It is drawn at 16 px against the 24 px the
  navigation rail draws the product name at.** The specification said "24 px against 48 px": that 48
  existed in no view — the product name is drawn at 24 — and with the logo at 24, "less prominent"
  would have stopped being checkable. It was measured and corrected; both numbers are read out of the
  AXAML by the tests, and another compares the two once rendered.
- Avalonia draws no SVG, and pulling in a renderer for one 16-pixel mark would have put half a dozen
  packages — and their licences — inside the artifact. The view carries the file's geometry, and a
  test compares the two character for character: an approximation of somebody's trademark would
  survive a screenshot review and dies here.
- It carries alternative text in both languages for the screen reader and is not a link: it
  identifies where the data comes from, it does not invite navigation.

What was measured is in [audit-legal-tmdb-logo.md](../evidence/stable/audit-legal-tmdb-logo.md).

### The export notification, drafted

Sending it is yours because it goes from your identity. Nothing in the content is open: recipients
`crypt@bis.doc.gov` and `enc@nsa.gov`, subject "TSU notification — publicly available encryption
source code", body naming the project, the URL `https://github.com/apvisualsolutions/ap-reelume`, and
the statement that source code incorporating cryptography (Ed25519 and Blake2b via BouncyCastle, to
verify release signatures) is publicly available at that address under EAR §740.13(e).

## How to report a legal problem

If you believe this project infringes a licence, a trademark, or a right of yours, write through the
same private channel [SECURITY.md](../../SECURITY.md) describes for vulnerabilities. Everything gets
an answer, and a wrong attribution is corrected without arguing about the correction.
