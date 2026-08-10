# Where to pick up

Project state at the close of the **2026-08-10** session, the first with the repository already
public. The Spanish version is in [NEXT-SESSION.es.md](NEXT-SESSION.es.md). The canonical scope record
is still [FEATURES.md](FEATURES.md); the audit's remaining work lives in
[2026-08-08-audit-remediation.md](superpowers/plans/2026-08-08-audit-remediation.md). This is only the
pick-up point.

## Startup check

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version                                        # 10.0.302
git status --short --branch                             # clean, on origin/codex/ap-reelume-mvp-x64
git merge-base --is-ancestor main HEAD; $LASTEXITCODE   # 0
```

## Where the code lives now

`apvisualsolutions/ap-reelume` has been **public** since 2026-08-10, as a fresh cut with a single root
commit. The full development history stayed in `apvisualsolutions/ap-reelume-archive` (private), so
**the SHAs the evidence documents cite resolve in the archive, not here**. The local remotes are
`origin` (public) and `archive` (the history), and the old branches are kept as `archived/main` and
`archived/codex/…` pointing at the archive.

CI runs on hosted runners, free on public repositories. **Billing stopped mattering**: it was the
previous session's blocker and it is gone. The self-hosted runner is still installed under `.runner/`
(git-ignored) but **switched off**, and the workflow no longer has a way to call it: the
repository-variable escape hatch was removed this session precisely because a public repository turns
it into a risk.

## What this session finished

- **Full security audit over the public repository.** Fifteen phases, two independent explorations,
  and every finding verified. **Zero critical, zero high.** Three medium, all applied or scheduled:
  the self-hosted runner hatch (removed), unpinned ffmpeg in both pipelines (pinned to a specific
  version), and dependabot not covering NuGet (covered in WP-9, below). What the audit found **clean**
  is worth recording because it took building: fully parameterised SQL, triple zip-slip defence,
  updater verification in the correct order, and the host allowlist enforced on **every** redirect
  hop. The report is in `.gstack/security-reports/2026-08-10-comprehensive.json` (local, git-ignored).
- **Full legal review, correcting rather than reporting.** All 624 source files now carry an SPDX
  header and the formatting gate demands it; the third-party notices went from naming 8 components to
  naming the 30 the package actually carries, with a test keeping it so; the TMDB attribution states
  the exact sentence their terms require; and **nothing from TMDB is kept beyond 180 days**, which was
  a real deviation from those terms. Evidence in
  [audit-legal-public.md](evidence/stable/audit-legal-public.md), status in
  [LEGAL.en.md](legal/LEGAL.en.md).
- **WP-9 complete.** `CONTRIBUTING.md`, a root `CLAUDE.md`, issue and pull request templates,
  `CODEOWNERS`, and dependabot covering NuGet with groups for Avalonia and the test tooling.
- **A gate that lied, fixed.** `PinnedDependencyTests` scanned `*.csproj` from the root without
  filtering and failed on any machine with the runner installed inside the tree, while staying green
  in CI. Red locally and green in the pipeline is the worst way for a gate to be wrong.
- **ARQ-006 steps 2-3.** The registration is now nine modules across six partials, and
  `DatabaseStartup` left with the five tests its logic never had. The split exposed two things: the
  wiring tests opened `CompositionRoot.cs` by name (eight went red without a wire changing; they now
  read every partial, and so does the gate against the house defect), and the coverage gate decided
  "new" by path rather than by content. Fixing it did not stop it biting: it held
  `WindowsFilePickers`, which came out at 0 % because a Windows dialog cannot be exercised without a
  window, and that is why it went back. Detail in
  [audit-arq006-modules.md](evidence/stable/audit-arq006-modules.md).

## What comes next (in this order)

1. **The licence texts have to travel in the artifact.** It is the only item here that is an
   obligation rather than an improvement. The package carries AP Reelume's `LICENSE` and the notices
   but not the text of the other licences, and VideoLAN's NuGet package was found to carry no
   `COPYING` either: nobody is supplying it. LGPL-2.1 §6, GPL-2.0 §1, and Apache-2.0 §4a require the
   copy to accompany; MIT and BSD-3-Clause require the notice reproduced. The texts are canonical,
   `licenses/` already ships, and `ArtifactContentsTests` is where their arrival gets pinned. Detail
   in [LEGAL.en.md](legal/LEGAL.en.md).
2. **The TMDB logo**, to the specification already settled in [LEGAL.en.md](legal/LEGAL.en.md)
   (version-controlled file, 24 px in Credits, alternative text, a test pinning it). It closes the
   last open point of their terms.
3. **ARQ-001 / WIN-005 / the rest of BUG-004**: an `ApplicationHost : IAsyncDisposable` owning the
   `ServiceProvider` and releasing on `ShutdownRequested`. It is also the moment to extract
   `WindowLifecycle`, which ARQ-006 deliberately left so as not to move it twice. Then
   ARQ-004/005/010.
2. **The coverage debt** named in [TST1-coverage-gate.md](evidence/stable/TST1-coverage-gate.md)
   (error branches in three files): settle it when that area is touched; the gate does not demand it
   retroactively.
3. **Optional hardening the audit recorded as not exploitable**, should that area ever be touched:
   bound the backup ZIP copy to the declared size (today the caps rest on a figure the archive
   declares about itself), and revalidate the extension inside `ShellExternalPlaybackLauncher` rather
   than trusting that every caller already filters.

## Yours (only what an agent cannot do)

Technical review does not belong on this list: it gets done and decided inside the session. This
round's dependabot pull requests — `checkout` 7.0.1, `setup-dotnet` 6.0.0, and `upload-artifact`
7.0.1 — were reviewed by checking every SHA against the tag it claims and reading each major's
breaking changes, then applied on the working branch, which is where the house convention wants them;
dependabot closes its own once it sees the dependency already updated.


- **Add the `RELEASE_SIGNING_SECRET_KEY` secret to the public repository.** It could not be copied —
  secrets cannot be read — and **without it the release pipeline fails on purpose**: `release.yml`
  checks that `SHA256SUMS.txt.minisig` exists and verifies, and stops if it does not. It is the only
  thing standing between the project and cutting its first public release. The copy is where you left
  it (see `SECURITY.md`).
- The **ten-minute manual physical walk**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- The **encrypted backup** of the signing key.
- **The professional legal opinion** (`REL-004`) and the five points [LEGAL.en.md](legal/LEGAL.en.md)
  names: VideoLAN's plugins, the TMDB logo, the export notification for the cryptography the package
  carries, and trademark and domain.
- The usual economic decisions: Authenticode certificate, Store, ARM64 hardware.

## Things learned worth not learning twice

- **A gate green in CI and red locally is not an annoyance: it is the gate being wrong.** If it fails
  only on your machine, check whether it scans from the root without filtering what git ignores.
- **`dotnet format` knows how to place licence headers.** `file_header_template` in `.editorconfig`
  plus `IDE0073` turns a gate that already existed into the one demanding the header; no new gate was
  needed.
- **Avalonia's XAML compiler accepts a comment before the root element.** Confirmed by compiling one
  file before touching the other fifty, which is the only way to know.
- **A cache limit is not a retention limit.** The TTL decides when to ask again; retention decides
  when the data may no longer exist. The degraded paths — no credential, no network — are exactly
  where the second one gets forgotten.
- **Hand-written third-party notices fall behind silently.** How far behind only became known by
  comparing three sources that had to agree: the SBOM, the lock file closure, and the binaries that
  actually travel in the package.
