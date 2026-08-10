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
`BSD-3-Clause`, which covers everything the package carries except one open point: VideoLAN's plugins
carry their own licences, some of them `GPL-2.0-or-later`. Compatible as long as they are "or later";
a `GPL-2.0-only` one would not be. Confirming that plugin by plugin in the pinned build is work for
the professional opinion.

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
- **Logo — pending.** The terms ask that TMDB's use be identified **with their logo**, less prominent
  than the product's own. Credits today shows the word "TMDB" and the attribution sentence, but not
  the logo. Incorporating a third party's mark is the owner's decision, not the programmer's, and it
  is named here as a pending action.

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
| VideoLAN plugins | Confirm none is `GPL-2.0-only` in the pinned build | third-party notices |
| TMDB logo | Decide on and incorporate the mark in Credits | this page |
| Export notification | Email BIS/ENC with the repository URL | this page |
| Trademark and domain | Formal `REL-004` report | `REL-004`, ADR-0001 |
| Authenticode signing | Postponed economic decision, already documented | SMARTSCREEN |

## How to report a legal problem

If you believe this project infringes a licence, a trademark, or a right of yours, write through the
same private channel [SECURITY.md](../../SECURITY.md) describes for vulnerabilities. Everything gets
an answer, and a wrong attribution is corrected without arguing about the correction.
