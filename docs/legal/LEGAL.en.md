# Legal status

What is settled legally, what has been corrected, and what stays open. It is written by the people
who build the program, not by a lawyer: this document **is not an opinion** and does not replace one.
Its use is that nobody has to guess where the edges are.

Last full review: 2026-08-10, over the public repository.

## The program's licence

AP Reelume by AP Solutions is published under `GPL-3.0-or-later`. The full text is in
[LICENSE](../../LICENSE) and the product attribution in [NOTICE](../../NOTICE).

Since the 2026-08-10 review, **every source file carries its SPDX header**: 556 `.cs` files, 51
`.axaml`, and 17 `.ps1` declare `SPDX-License-Identifier: GPL-3.0-or-later` next to the copyright
holder. A licence that lives only in `LICENSE` stops being attached to a file the moment somebody
copies it out of the tree; the header travels with it. The `IDE0073` rule demands it in
`.editorconfig`, so `dotnet format --verify-no-changes` — a gate that already ran — rejects a new
file without one.

## Warranty disclaimer

The program comes with **no warranty whatsoever**, to the extent permitted by applicable law.
Sections 15, 16, and 17 of the GPL-3.0 say so in full, and neither the README nor the application
promises anything else. In particular, because these are the confusions that actually happen:

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

Compatibility: `GPL-3.0-or-later` allows incorporating `LGPL-2.1-or-later`, `MIT`, `Apache-2.0`, and
`BSD-3-Clause`, which covers everything the package carries.

**VideoLAN's plugins — closed on 2026-08-10.** This was recorded as a question for the opinion: a
`GPL-2.0-only` plugin would be incompatible with GPL-3.0. It was checked at the source: VLC's tree
carries the GPL version 2 **with** the "either version 2 of the License, or (at your option) any later
version" clause, so the set is `GPL-2.0-or-later` and sits under GPL-3.0. The point leaves the list.

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
verified against the digest VideoLAN publishes — and the `LibVLCSharp 3.10.0` archive, and attaches
them beside the binaries. The written offer stays for channels where "the same place" means nothing,
such as a store, and it now explicitly stands for any third party. Two things were corrected along the
way: the notice named `libvlc 3.0.23.1` — a version whose source **does not exist**, that fourth digit
belonging to the NuGet package — and it did not mention that the work using the library is this
program, public under `GPL-3.0-or-later`, which is what 6(a) asks for so the library can be relinked.
Measured in [audit-corresponding-source.md](../evidence/stable/audit-corresponding-source.md).

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
- **Commercial use.** The terms reserve it for a separate written agreement. AP Reelume is free
  software and derives no revenue from TMDB or its content, so it does not apply today. If the
  program were ever charged for, this point changes and must be read again first.
- **Logo — closed on 2026-08-10.** The terms ask that TMDB's use be identified **with their logo**,
  less prominent than the product's own. Credits shows it as of this session, above the attribution
  sentence, with alternative text and no link: it identifies where the data comes from, it does not
  invite navigation. The file is the one TMDB publishes — its SHA-256 matches the digest they
  themselves embed in the asset's address, and a test checks it — and what the view draws is their
  vector rather than an imitation.

## GitHub's terms

The repository is hosted on GitHub, and the updater queries `api.github.com` and downloads from
`github.com` and its storage. Publishing code under a free licence in a public repository is exactly
the use their Terms of Service anticipate, and section F of those terms already grants other users
the right to view and fork the repository; `GPL-3.0-or-later` grants more. No GitHub API requiring
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
