# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Writes the release notes the independent updater reads.

.DESCRIPTION
    The updater does not download anything the notes do not describe: it takes the bilingual summary
    and the published SHA-256 from the body of the release, and refuses the version when either is
    missing. That makes the notes part of the artifact rather than prose written afterwards, so they
    are generated from things that already exist and are already checked — the two changelogs, which
    a documentation suite keeps paired, and the hashes the packaging script just computed.

    Nothing is invented here. A version with no section in a changelog produces no section in the
    notes, and the updater then refuses it by name, which is the correct outcome: a release nobody
    described is one nobody should be asked to install.
#>
[CmdletBinding()]
param(
    [string]$PackageRoot = 'artifacts/package',

    [string]$Output = 'release-notes.md'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$packageRootFull = [IO.Path]::GetFullPath($PackageRoot, $repoRoot)

[xml]$buildProps = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = ([string]$buildProps.Project.PropertyGroup.Version).Trim()

# One version's worth of a changelog: everything under its heading, up to the next one. The link
# definitions at the foot of the file are not part of any version and are dropped.
function Get-VersionSection {
    param([string]$Path, [string]$Version)

    $lines = Get-Content -LiteralPath $Path
    $collected = [Collections.Generic.List[string]]::new()
    $collecting = $false
    foreach ($line in $lines) {
        if ($line -match '^##\s') {
            if ($collecting) { break }
            $collecting = $line -match ("^##\s+\[" + [regex]::Escape($Version) + "\]")
            continue
        }

        if ($collecting -and $line -notmatch '^\[[^\]]+\]:\s') {
            $collected.Add($line)
        }
    }

    ($collected -join "`n").Trim()
}

$spanish = Get-VersionSection -Path (Join-Path $repoRoot 'docs/CHANGELOG.es.md') -Version $version
$english = Get-VersionSection -Path (Join-Path $repoRoot 'docs/CHANGELOG.en.md') -Version $version

$sumsPath = Join-Path $packageRootFull 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $sumsPath)) {
    throw "There are no published hashes at $sumsPath. Run eng/package-x64.ps1 first."
}

$sums = @(Get-Content -LiteralPath $sumsPath | Where-Object { $_.Trim() })
if ($sums.Count -eq 0) { throw "$sumsPath is empty, so the notes would promise nothing checkable." }

$notes = [Collections.Generic.List[string]]::new()
$notes.Add('## Español')
$notes.Add('')
$notes.Add($spanish)
$notes.Add('')
$notes.Add('## English')
$notes.Add('')
$notes.Add($english)
$notes.Add('')
$notes.Add('## SHA256SUMS')
$notes.Add('')
$notes.Add('```')
$sums | ForEach-Object { $notes.Add($_.Trim()) }
$notes.Add('```')
$notes.Add('')

# The detached minisign signature over those exact lines (SEC-003). The updater verifies it against
# the key embedded in the binary before believing any hash above; notes without it are refused by
# every installation, so prepare-release blocks a release whose notes would carry none.
$signaturePath = "$sumsPath.minisig"
$signed = Test-Path -LiteralPath $signaturePath
if ($signed) {
    $notes.Add('## Firma / Signature')
    $notes.Add('')
    $notes.Add('```')
    Get-Content -LiteralPath $signaturePath | Where-Object { $_.Trim() } | ForEach-Object { $notes.Add($_.Trim()) }
    $notes.Add('```')
    $notes.Add('')
}

$outputPath = [IO.Path]::GetFullPath($Output, $packageRootFull)
Set-Content -LiteralPath $outputPath -Value ($notes -join "`n") -Encoding utf8NoBOM

$missing = @()
if (-not $spanish) { $missing += 'Español' }
if (-not $english) { $missing += 'English' }

Write-Output "Release notes: $outputPath"
Write-Output ("Version {0}; {1} hash(es); {2}; {3}." -f
    $version,
    $sums.Count,
    $(if ($missing.Count -eq 0) { 'both languages present' } else { "missing: $($missing -join ', ')" }),
    $(if ($signed) { 'checksums signed' } else { 'checksums UNSIGNED' }))
