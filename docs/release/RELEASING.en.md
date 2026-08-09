# Cutting a release

How an AP Reelume artifact for Windows 11 x64 is produced and verified. The Spanish version is at
[RELEASING.es.md](RELEASING.es.md).

## What you need

| Tool | For | Check |
|---|---|---|
| .NET SDK 10.0.302 | building and publishing | `dotnet --version` |
| Windows 10 or 11 SDK | sealing the MSIX | `MakeAppx.exe` under `C:\Program Files (x86)\Windows Kits\10\bin` |
| PowerShell 7 | running the scripts | `pwsh --version` |
| Git | reproducibility and SBOM | `git --version` |

Visual Studio is not needed. The package is not built from a `.wapproj`; the reasoning is in
[ADR-0004](../adr/0004-seal-the-package-with-makeappx.md).

## Where the version comes from

One place: `<Version>` in `Directory.Build.props`. Everything else is derived.

| Source | Value | Rule |
|---|---|---|
| `Directory.Build.props` | `0.1.0` | SemVer, chosen by hand |
| `Package.appxmanifest` | `0.1.0.0` | SemVer plus the revision MSIX reserves |
| MSIX file name | `APSolutions.LocalMedia_0.1.0_x64.msix` | identity, version, architecture |
| ZIP file name | `ApReelume-0.1.0-win-x64.zip` | public name, version, runtime |
| ARM64 MSIX | `APSolutions.LocalMedia_0.1.0_arm64.msix` | same rule, other architecture |
| ARM64 ZIP | `ApReelume-0.1.0-win-arm64.zip` | same rule, other runtime |

The manifest carries its version written out rather than substituted, so it stays valid XML a test
can read. `FileAssociationPackageTests` compares the two, and `eng/package-x64.ps1` stops when they
differ. **Bumping the version is two edits, and a test says so when only one is made.**

## The steps

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
pwsh ./eng/package-x64.ps1
pwsh ./eng/verify-package.ps1 -Mode Verify
pwsh ./eng/package-arm64.ps1
pwsh ./eng/verify.ps1 -Configuration Release -Runtime win-x64
```

`eng/package-arm64.ps1` comes after the x64 one deliberately: it compares its payload against that
layout to check both architectures ship the same application, and without it the comparison cannot be
made. `eng/verify.ps1` builds both on its own, so an ordinary verification needs only that last
command.

**The ARM64 artifact is not certified on hardware.** It is built, sealed, and verified in everything
that can be verified without an ARM64 machine, and `arm64-matrix.json` names the six things that
cannot. Publishing it is a decision to take having read that file, not a consequence of its
existing.

### 1. `eng/package-x64.ps1`

Publishes self-contained for `win-x64`, assembles the layout, seals it, and writes everything that
travels with the artifact. Two things a plain `dotnet publish` does not do:

- **Removes the LibVLC payloads for other architectures.** A `win-x64` publish of this application
  also brings `win-x86` and `win-arm64`: 512 MB that become 234 MB once they are gone.
- **Puts the licence inside.** `LICENSE`, `NOTICE`, and the third-party notices in both languages
  travel in the payload, because that is a condition of shipping the binary rather than a nicety of
  the download page.

It leaves in `artifacts/package/`: the MSIX, the ZIP, `SHA256SUMS.txt`, `sbom/`, `contents.json`, and
`packaged/AppxManifest.xml`.

### 2. `eng/verify-package.ps1`

Walks the lifecycle and compares two builds. Writes `lifecycle.json` and `reproducibility.json`.

The lifecycle runs against the **unpacked package**, with a different data folder per cycle through
`AP_LOCALMEDIA_DATA_ROOT`. Redirecting `LOCALAPPDATA` would not work: .NET resolves that folder
through `SHGetFolderPath` and never reads the variable.

The four phases that belong to Windows — installing, upgrading, repairing, and removing a package
through the system itself — are **declared blocked** when there is no clean virtual machine, no
elevation, and no signature. `MsixLifecycleTests` requires them to stay blocked while the environment
is that one, and requires them to pass the moment it is not. A block cannot be read as a pass.

The reproducibility comparison creates two clean copies of the tree — including staged changes, via
`git stash create` — in two different directories and compares the payload file by file. **Stage what
you are about to release before running it**: an untracked file does not exist to a clean copy, and
the script stops when it finds one.

### 3. `eng/verify.ps1`

The full verification. It builds the package, runs the lifecycle, and then the whole suite, the
formatting, the documentation, and the dependency audit.

### 4. `eng/generate-verification-manifest.ps1`

Regenerates `docs/evidence/mvp/verification-manifest.json` from the matrix and from the package that
was just built. **Run it when cutting the release, not on every build**: the manifest is versioned,
and an MSIX records the moment it was sealed, so its hashes change with every sealing even when the
contents are identical. The commit and hashes it records are the published package's, not the working
copy's.

It refuses to write a manifest where an unsettled commitment declares no block, or where a settled
one still carries one.

## Publishing on GitHub

`.github/workflows/release.yml` triggers on a `v*` tag. It does what you would do by hand and uploads
the MSIX, the ZIP, the hashes with their signature, and the SBOM as run artifacts. It does **not**
publish a release on its own and uploads nothing to any Store. It uses one secret,
`RELEASE_SIGNING_SECRET_KEY`: the minisign key that signs `SHA256SUMS.txt` so the updater can verify
the digests against the public key embedded in the binary (SEC-003). Authenticode signing still does
not exist, and nothing here changes that.

To sign locally instead of in the workflow, point `RELEASE_SIGNING_KEY_FILE` at your copy of the
private key (which lives outside every repository) before running `eng/package-x64.ps1`. Without a
key the package still builds, but `prepare-release` blocks the publication: an unsigned release is
one no installation will accept.

Before publishing:

1. `SHA256SUMS.txt` **and** `SHA256SUMS.txt.minisig` travel with the files, in the same place.
2. The notes link [SMARTSCREEN.en.md](SMARTSCREEN.en.md) and its Spanish version.
3. The notes say the artifact carries no Authenticode signature. Do not leave that to be inferred.

### The release notes are what the updater reads

The independent updater (`REL-003`) downloads nothing the notes do not describe. It reads the release
marked `latest` and takes from it the version in the tag, the asset for its architecture, and three
more things from the body of the notes. Without any one of them the release is **not offered**, and
the application says which one is missing.

**Do not write them by hand.** `eng/package-x64.ps1` generates them at
`artifacts/package/release-notes.md` from the two changelogs and the hashes it has just computed, and
publishing is a matter of pasting that file. `ReleaseNotesTests` takes what was generated, hands it
to the real provider inside the payload GitHub would return, and asks the real policy whether it
would offer that version: it does not check a format, it checks that somebody running the published
artifact would actually receive the update.

The shape it produces, which is the one the updater expects, is this:

````markdown
## Español

Qué cambia, en una o dos frases.

## English

What changed, in a sentence or two.

## SHA256SUMS

```
<hash>  APSolutions.LocalMedia_<version>_x64.msix
<hash>  APSolutions.LocalMedia_<version>_arm64.msix
```

## Firma / Signature

```
untrusted comment: signature from AP Reelume release key
<base64 signature>
trusted comment: timestamp:<unix>	file:SHA256SUMS.txt	prehashed
<base64 global signature>
```
````

Four rules worth keeping in mind:

- **Both languages or neither.** Confirming an update means reading what changed, and a summary the
  person cannot read turns the confirmation into a formality.
- **The hash line is matched by file name**, so it has to be exactly the asset's. These are the same
  lines as `SHA256SUMS.txt`, copied verbatim.
- **The signature is the content of `SHA256SUMS.txt.minisig`, verbatim.** The updater verifies it
  against the embedded key before believing any hash; without it, or with altered lines, the version
  is not offered (SEC-003).
- **A pre-release or a draft is never offered**, even when marked `latest`.

Whether to switch on the automatic check belongs to whoever uses the application; it ships off, and
while it is off the application opens no connection of its own.

## Publishing to winget

`eng/package-x64.ps1` also leaves the Windows Package Manager manifest in
`artifacts/package/winget/`, generated from the archive itself: the hash is the published one, the
executable it declares has been found inside the ZIP, and the descriptions come from the two READMEs.
`WingetManifestTests` checks all three against the real artifact.

winget is the distribution channel that **costs nothing and needs no certificate**: it accepts a ZIP
carrying a portable application and verifies the SHA-256 that is already published. Submitting means
opening a pull request against [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) with
that folder.

**Two conditions have to hold first, and today neither does:**

1. **The download must be public.** The manifest points at the GitHub release, and in a private
   repository that address answers nobody. Check it with
   `pwsh ./eng/build-winget-manifest.ps1 -Verify`, which asks the address rather than assuming it.
2. **That release has to exist.** The manifest names `v<version>`; without the published tag there is
   nothing to download.

The same thing blocks the independent updater: it queries the GitHub API, and for a private
repository that answers 404. Since the absence of a release is a settled answer, the application
would tell everybody they are up to date. **Making the repository public and cutting a release are
requirements for the updater to work at all, not decoration.**

Only x64 is declared. The ARM64 artifact is built and verified on every run but is not published
until `PRD-003` is settled, and a package manager entry is publication.

## Start here: `eng/prepare-release.ps1`

```bash
pwsh ./eng/prepare-release.ps1
```

It answers one question — whether this tree could be published — and produces everything a release
needs. It checks the conditions nobody remembers: that the tree is clean and pushed, that the version
was bumped **in both places**, that the repository answers to strangers, that no verified MVP
commitment lost its evidence, that the package does not claim a signature, that no lifecycle phase
failed, and that two clean builds are still identical.

**It does nothing irreversible.** No tag, no release, no push, no repository setting. When something
blocks, it says so and stops; when nothing does, it prints the five remaining steps, which a person
performs. Publishing stays a deliberate act by somebody who has read that report.

`-SkipBuild` reuses whatever artifact is already in `artifacts/package` instead of building it again.

## What to check before tagging

- `pwsh ./eng/verify.ps1 -Configuration Release -Runtime win-x64` finishes clean, twice.
- `docs/FEATURES.md` has no MVP commitment without linked evidence.
- `contents.json` says `"signed": false` and its note mentions SmartScreen.
- `lifecycle.json` has no phase in `Failed`, and blocked ones carry their reason.
- `reproducibility.json` has no differences and no exclusions.
- `artifacts/package-arm64/arm64-matrix.json` is read before deciding whether ARM64 ships, and its
  `parityWithX64` shows no application file on one architecture only.
