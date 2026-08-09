# Where to pick up

Project state at the close of the **2026-08-09 (night)** session, and what comes next. The
Spanish version is [NEXT-SESSION.es.md](NEXT-SESSION.es.md). The canonical scope record remains
[FEATURES.md](FEATURES.md); the audit's remaining work lives in
[2026-08-08-audit-remediation.md](superpowers/plans/2026-08-08-audit-remediation.md). This is
only the pick-up point.

## Startup verification

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version                                        # 10.0.302
git status --short --branch                             # clean
git merge-base --is-ancestor main HEAD; $LASTEXITCODE   # 0
```

## ⚠ First thing: GitHub Actions billing is broken

**Almost no CI run from this session could execute.** Jobs die without starting a single step,
annotated: "The job was not started because recent account payments have failed or your spending
limit needs to be increased. Please check the 'Billing & plans' section in your settings." This
is an account decision (yours): fix the payment or the spending limit under GitHub → Billing &
plans. The quota was intermittent: one run got a runner (the spike's on `main`, left watched at
close); the rest died instantly, and several on `main` no longer accept `gh run rerun`. **After
fixing billing**: rerun the `aa930d1` runs (both branches) if allowed, or let the next push
verify the full state — `aa930d1` contains everything from today. Every gate passed **locally**
today (format, `-warnaserror`, affected suites, verify-docs, personal-pattern guard). The
watcher-storm flake run (31319008700) stays red in the history with no retry possible; its
occurrence is already recorded under CI-005.

## What is finished (this session: `dec5ac3`, `230602e`, `aa930d1`)

- **PLY-016 resolved the honest way: `DEFERRED` with its measurement.** The spike measured the
  plan's four candidates as media options (hardware **and** software decoding) plus two
  instance-level controls (RV32 and I420): **no VLC 3 video filter processes a single frame on
  the callback video path** — VLC builds the chain and removes it whole with `Failed to
  compensate for the format changes, removing all filters`, captured from the native log. The
  metric (Laplacian variance) proved sensitive: hw vs sw differ (1169→927). The spike stays
  re-runnable (`LowResEnhancementSpikeTests`, MediaTests, with its own noisy 480p MPEG-2
  sample); re-running it after a future LibVLC upgrade answers whether the blocker persists.
  Phase 2 did not run (its condition failed); the alternatives (VLC 4, managed enhancement over
  the BGRA frames, another engine) are named with their cost in
  [PLY16-low-res-spike.md](evidence/stable/PLY16-low-res-spike.md) — reopening is an owner scope
  decision.
- **TST-001 (WP-7 complete): the coverage gate exists and bites.** `eng/check-coverage.ps1` as a
  blocking step of `verify.ps1`: every source file new against `origin/main` must arrive with
  ≥96% lines and branches (tree comparison — CI's shallow checkout cannot break it; an
  unreachable base is a loud red; `*.g.cs` excluded). `reportgenerator` in
  `.config/dotnet-tools.json` and `CoverageGateTests` pinning script, thresholds, and
  invocation. Calibrated against `797c8cb`: three **true** reds from the previous session
  (`ReconcileScannedFiles` 86.7% lines, `CompositeFileIdentityProvider` 66.7%,
  `PlayerVersionsViewModel` 60.6% — happy paths walked, error branches not), named as visible
  debt in [TST1-coverage-gate.md](evidence/stable/TST1-coverage-gate.md) without lowering the
  bar. Its teeth are local (main fast-forwards with the branch, so the CI diff is usually empty
  and says so).
- **ARQ-006 step 1 complete.** The four remaining textual assertions over `CompositionRoot.cs`
  are now descriptor assertions in `CompositionDescriptorTests`: the migration runner's explicit
  constructor, the single session coordinator, the update surface's singleton, and the updater's
  address asserted on the composed **object** (`GitHubReleaseUpdateProvider` exposes
  `RepositoryOwner/Name`) against both changelogs. Two invocation halves stay declared as text
  (the automatic check's startup; `videoStatus.Apply` in `OpenPlayerAsync`) until the startup
  path leaves the file (steps 2-3/ARQ-001).

## What comes next (in this order)

1. **WP-9**: CONTRIBUTING.md, root CLAUDE.md, issue/PR templates, CODEOWNERS, dependabot NuGet
   (SECURITY.md is done). None of it depends on billing.
2. **ARQ-006 steps 2-3** (modules `AddData`/`AddPlayback`/…, extract `WindowsFilePickers`,
   `DatabaseStartup`, `WindowLifecycle`), then ARQ-001/004/005/010.
3. The coverage debt named under TST-001 (the three files' error branches) can be paid when that
   area is next touched; the gate does not demand it retroactively.

## Yours, not the agent's

- **Fix GitHub Billing & plans** (blocks all CI).
- The ten-minute manual physical walk
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- The encrypted backup of the signing key.
- Reviewing the three dependabot PRs (checkout/upload-artifact v7, setup-dotnet v6).
- The standing economic decisions (certificate, Store, ARM64, legal, `.superpowers/` logs).

## Things learned that should stay learned

- **A CI failure with the job in `failure` and 0 steps is not code**: it is billing; the
  check-run annotation says so. There is nothing to fix in the tree.
- **VLC 3's vout filters are inert with callback output** (vmem), regardless of chroma, decoder,
  or activation route; measuring frames (not accepted options) is what exposed it. The native
  log (`libVlc.Log`) names the cause.
- **Laplacian variance tells decoders apart** (hw vs sw differ on the same sample): if a filter
  runs, the metric sees it.
- **The coverage gate reads the diff before the reports**, so the empty case (CI after the
  fast-forward) costs nothing and demands no coverage where there is nothing to hold.
