# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Answers whether this tree could be published, and produces everything a release needs.

.DESCRIPTION
    Cutting a release is a dozen conditions that have to hold at once, and the ones that matter are
    the ones nobody remembers: that the repository answers to strangers, that the version was bumped
    in both places, that the notes the updater reads were generated, that no MVP commitment is
    unevidenced. This checks them, builds what a release carries, and reports what is still missing.

    It never does anything that cannot be undone. It creates no tag, publishes no release, pushes
    nothing, and changes no repository setting. Publishing stays a deliberate act by a person who has
    read the report below — which is the same rule the release workflow already follows.

.EXAMPLE
    pwsh ./eng/prepare-release.ps1
    pwsh ./eng/prepare-release.ps1 -SkipBuild   # when the artifact was just built
#>
[CmdletBinding()]
param(
    # Reuses the artifact already in artifacts/package instead of building it again.
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$blockers = [Collections.Generic.List[string]]::new()
$warnings = [Collections.Generic.List[string]]::new()
$notes = [Collections.Generic.List[string]]::new()

function Add-Blocker([string]$text) { $blockers.Add($text) }
function Add-Warning2([string]$text) { $warnings.Add($text) }
function Add-Note([string]$text) { $notes.Add($text) }

Push-Location $repoRoot
try {
    [xml]$buildProps = Get-Content -LiteralPath 'Directory.Build.props' -Raw
    $version = ([string]$buildProps.Project.PropertyGroup.Version).Trim()
    Write-Output "AP Reelume - release readiness for $version"
    Write-Output ''

    # ---------------------------------------------------------------- the tree
    if ((git status --porcelain | Measure-Object).Count -ne 0) {
        Add-Blocker 'The working tree has uncommitted changes. A release describes a commit, not a desk.'
    }

    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    git merge-base --is-ancestor main HEAD 2>$null
    if ($LASTEXITCODE -ne 0) {
        Add-Blocker "main is not an ancestor of $branch, so this would publish a tree that diverged from it."
    }

    # The default branch is the face of a public repository: its tree is what a stranger sees first,
    # and a main left behind keeps showing what later commits removed. The personal-library
    # redaction lived only on the working branch until main caught up, which is exactly the kind of
    # thing nobody remembers at publishing time.
    $mainBehind = (git rev-list --count 'main..HEAD' 2>$null)
    if ($LASTEXITCODE -eq 0 -and [int]$mainBehind -gt 0) {
        Add-Blocker "main is $mainBehind commit(s) behind ${branch}: the default branch is what a public repository shows first. Fast-forward it to this commit before publishing."
    }

    $ahead = (git rev-list --count '@{upstream}..HEAD' 2>$null)
    if ($LASTEXITCODE -eq 0 -and [int]$ahead -gt 0) {
        Add-Blocker "$ahead commit(s) are not pushed. The release names a commit strangers have to be able to fetch."
    }

    # ---------------------------------------------------------------- the version
    $manifestPath = 'src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest'
    $packageVersion = ([xml](Get-Content -LiteralPath $manifestPath -Raw)).Package.Identity.Version
    if ($packageVersion -ne "$version.0") {
        Add-Blocker "Directory.Build.props says $version and the package manifest says $packageVersion. Bumping a version is two edits."
    }

    $tag = "v$version"
    if ((git tag --list $tag) -eq $tag) {
        Add-Blocker "$tag already exists locally. A version is published once."
    }

    # ---------------------------------------------------------------- can anybody reach it
    # This is the condition that has no symptom. A private repository answers 404, the updater reads
    # that as "no newer version", and every installation is told it is up to date - forever.
    $visibility = $null
    try {
        $visibility = (gh repo view --json visibility --jq '.visibility' 2>$null)
    }
    catch {
        Add-Warning2 'The GitHub CLI could not report the repository visibility; check it by hand.'
    }

    if ($visibility -and $visibility -ne 'PUBLIC') {
        Add-Blocker "The repository is $visibility. A private repository answers 404 to the updater and to winget, and 404 reads as 'no newer version'."
    }
    elseif ($visibility -eq 'PUBLIC') {
        Add-Note 'The repository is public, so the release address will answer.'
    }

    # ---------------------------------------------------------------- the artifact
    if (-not $SkipBuild) {
        Write-Output 'Building the artifact and everything that verifies it …'
        & (Join-Path $PSScriptRoot 'package-x64.ps1') -Configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }
        & (Join-Path $PSScriptRoot 'verify-package.ps1') -Mode Verify
        if ($LASTEXITCODE -ne 0) { throw 'Package verification failed.' }
        & (Join-Path $PSScriptRoot 'package-arm64.ps1') -Configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'ARM64 packaging failed.' }
        Write-Output ''
    }

    $packageRoot = Join-Path $repoRoot 'artifacts/package'
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot 'contents.json'))) {
        Add-Blocker 'There is no artifact. Run without -SkipBuild.'
    }
    else {
        $contents = Get-Content -LiteralPath (Join-Path $packageRoot 'contents.json') -Raw | ConvertFrom-Json
        if ($contents.version -ne $version) {
            Add-Blocker "The artifact was built as $($contents.version) and this tree is $version."
        }

        if ($contents.signed) { Add-Blocker 'contents.json claims a signature this release does not have.' }
        if ($contents.secretScan.hits -ne 0) { Add-Blocker "The payload matched $($contents.secretScan.hits) secret pattern(s)." }
        if ($contents.sbomGaps.Count -ne 0) { Add-Blocker "$($contents.sbomGaps.Count) package(s) are missing from the SBOM." }

        $lifecycle = Get-Content -LiteralPath (Join-Path $packageRoot 'lifecycle.json') -Raw | ConvertFrom-Json
        $failed = @($lifecycle.phases | Where-Object { $_.outcome -eq 'Failed' })
        if ($failed.Count -gt 0) { Add-Blocker "$($failed.Count) lifecycle phase(s) failed: $(($failed.id) -join ', ')." }
        $blocked = @($lifecycle.phases | Where-Object { $_.outcome -eq 'Blocked' })
        if ($blocked.Count -gt 0) { Add-Note "$($blocked.Count) lifecycle phase(s) are declared blocked, each with its reason." }

        $repro = Get-Content -LiteralPath (Join-Path $packageRoot 'reproducibility.json') -Raw | ConvertFrom-Json
        $differing = @($repro.differingFiles).Count + @($repro.onlyInFirst).Count + @($repro.onlyInSecond).Count
        if ($differing -ne 0) {
            Add-Blocker "The build is not reproducible: $differing file(s) differ between two clean builds."
        }
        elseif (@($repro.identicalFiles).Count -eq 0 -and [int]$repro.identicalFiles -eq 0) {
            Add-Blocker 'The reproducibility report compared nothing, which is not the same as finding no differences.'
        }
        else {
            Add-Note "$($repro.identicalFiles) file(s) identical across two clean builds."
        }

        if (@($repro.excludedFromComparison).Count -ne 0) {
            Add-Blocker "$(@($repro.excludedFromComparison).Count) file(s) were excluded from the comparison."
        }

        foreach ($required in @('release-notes.md', 'SHA256SUMS.txt')) {
            if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $required))) {
                Add-Blocker "$required was not produced, and the updater reads it."
            }
        }

        # SEC-003: every installation refuses a release whose checksums are not signed by the
        # embedded key, so publishing without the signature ships a version nobody will ever be
        # offered — and worse, one whose hash proves nothing about who published it.
        $sumsFile = Join-Path $packageRoot 'SHA256SUMS.txt'
        $signatureFile = "$sumsFile.minisig"
        if (-not (Test-Path -LiteralPath $signatureFile)) {
            Add-Blocker 'SHA256SUMS.txt is not signed. Sign it with the release key (RELEASE_SIGNING_SECRET_KEY or RELEASE_SIGNING_KEY_FILE) and regenerate the notes.'
        }
        else {
            dotnet run --project (Join-Path $repoRoot 'eng/tools/ReleaseSigning') -- verify $sumsFile $signatureFile (Join-Path $repoRoot 'eng/release-signing.pub') | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Add-Blocker 'The signature on SHA256SUMS.txt does not verify against the embedded public key.'
            }
            elseif ((Get-Content -LiteralPath (Join-Path $packageRoot 'release-notes.md') -Raw) -notmatch 'untrusted comment:') {
                Add-Blocker 'The release notes do not carry the checksum signature, and the updater reads it from there.'
            }
            else {
                Add-Note 'The checksums are signed and the signature verifies against the embedded public key.'
            }
        }

        $wingetFolder = Join-Path $packageRoot "winget/manifests/a/APSolutions/APReelume/$version"
        if (-not (Test-Path -LiteralPath $wingetFolder)) {
            Add-Blocker 'The winget manifest was not produced.'
        }
        else {
            Add-Note "winget manifest ready at artifacts/package/winget (submit as a pull request to microsoft/winget-pkgs)."
        }
    }

    # ---------------------------------------------------------------- the record
    $matrix = Get-Content -LiteralPath 'docs/FEATURES.md' -Raw
    $unevidenced = @([regex]::Matches($matrix, '(?m)^\|\s*(?<id>[A-Z0-9]+-[0-9]+)\s*\|[^|]*\|\s*MVP\s*\|\s*VERIFIED\s*\|[^|]*\|(?<evidence>[^|]*)\|') |
        Where-Object { $_.Groups['evidence'].Value -notmatch '\]\(' } |
        ForEach-Object { $_.Groups['id'].Value })
    if ($unevidenced.Count -gt 0) {
        Add-Blocker "Verified MVP commitments with no linked evidence: $($unevidenced -join ', ')."
    }

    $arm64Matrix = Join-Path $repoRoot 'artifacts/package-arm64/arm64-matrix.json'
    if (Test-Path -LiteralPath $arm64Matrix) {
        Add-Note 'Read artifacts/package-arm64/arm64-matrix.json before deciding whether ARM64 ships. It is built, not certified.'
    }

    # ---------------------------------------------------------------- the verdict
    Write-Output ''
    foreach ($note in $notes) { Write-Output "  ok      $note" }
    foreach ($warning in $warnings) { Write-Warning $warning }
    foreach ($blocker in $blockers) { Write-Output "  BLOCKS  $blocker" }
    Write-Output ''

    if ($blockers.Count -gt 0) {
        Write-Output "$($blockers.Count) thing(s) block this release. Nothing was tagged or published."
        exit 1
    }

    Write-Output @"
Nothing blocks this release. What remains is deliberate and is done by a person:

  1. git tag $tag && git push origin $tag
  2. Create the release on GitHub for $tag, pasting artifacts/package/release-notes.md as the body.
  3. Upload the MSIX, the ZIP, SHA256SUMS.txt and SHA256SUMS.txt.minisig from artifacts/package.
  4. Submit artifacts/package/winget as a pull request to microsoft/winget-pkgs.
  5. Regenerate docs/evidence/mvp/verification-manifest.json from the published artifact.

The ARM64 artifact is built but is not published until PRD-003 is settled.
"@
}
finally {
    Pop-Location
}
