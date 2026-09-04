# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Writes docs/evidence/mvp/verification-manifest.json from the scope record.

.DESCRIPTION
    The manifest maps every MVP commitment to how it was resolved: status, the tasks that built it,
    the suites that hold it up, the evidence that settles it, and — when it is not settled — what is
    blocking it and what would clear the block.

    Status and evidence are read from docs/FEATURES.md rather than typed again, because two records
    that can disagree are one record and one rumour. The tasks come from the evidence filenames. Only
    two things are declared here: which suites cover which area, and the blocks.

    A block is written down once, in this file, and DocumentationTests refuses any unsettled
    commitment that has no reason, owner, and unblock condition. That is what stops a block from
    quietly reading as a pass.
#>
[CmdletBinding()]
param(
    [string]$Output = 'docs/evidence/mvp/verification-manifest.json',

    [string]$PackageRoot = 'artifacts/package'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputPath = [IO.Path]::GetFullPath($Output, $repoRoot)
$packageRootFull = [IO.Path]::GetFullPath($PackageRoot, $repoRoot)

# Which suites stand behind each area of the matrix. A commitment is covered by the suites of its
# area plus anything named for it below.
$suitesByPrefix = @{
    'PRD'  = @('ApSolutions.LocalMedia.ArchitectureTests', 'ApSolutions.LocalMedia.DocumentationTests')
    'LIB'  = @('ApSolutions.LocalMedia.Domain.Tests', 'ApSolutions.LocalMedia.Application.Tests', 'ApSolutions.LocalMedia.IntegrationTests', 'ApSolutions.LocalMedia.UiTests')
    'PLY'  = @('ApSolutions.LocalMedia.Domain.Tests', 'ApSolutions.LocalMedia.MediaTests', 'ApSolutions.LocalMedia.UiTests')
    'UX'   = @('ApSolutions.LocalMedia.UiTests', 'ApSolutions.LocalMedia.Application.Tests')
    'A11Y' = @('ApSolutions.LocalMedia.AccessibilityTests', 'ApSolutions.LocalMedia.UiTests')
    'DAT'  = @('ApSolutions.LocalMedia.IntegrationTests', 'ApSolutions.LocalMedia.Application.Tests')
    'PRI'  = @('ApSolutions.LocalMedia.IntegrationTests', 'ApSolutions.LocalMedia.UiTests')
    'SYS'  = @('ApSolutions.LocalMedia.PackagingTests', 'ApSolutions.LocalMedia.UiTests')
    'REL'  = @('ApSolutions.LocalMedia.PackagingTests')
    'DOC'  = @('ApSolutions.LocalMedia.DocumentationTests')
}

$extraSuites = @{
    'PRD-002' = @('ApSolutions.LocalMedia.PackagingTests')
    'PRD-005' = @('ApSolutions.LocalMedia.PackagingTests')
    'LIB-004' = @('ApSolutions.LocalMedia.PerformanceTests')
    'LIB-002' = @('ApSolutions.LocalMedia.PerformanceTests')
    'PLY-001' = @('ApSolutions.LocalMedia.IntegrationTests', 'ApSolutions.LocalMedia.PackagingTests')
    'PLY-008' = @('ApSolutions.LocalMedia.IntegrationTests')
    'PLY-014' = @('ApSolutions.LocalMedia.AccessibilityTests')
    'SYS-001' = @('ApSolutions.LocalMedia.PerformanceTests')
    'DAT-001' = @('ApSolutions.LocalMedia.PerformanceTests')
    'DAT-002' = @('ApSolutions.LocalMedia.IntegrationTests')
    'UX-005'  = @('ApSolutions.LocalMedia.IntegrationTests')
    'UX-006'  = @('ApSolutions.LocalMedia.IntegrationTests')
}

# The commitments this hardware cannot settle. Each one names what would settle it, so the block is
# a piece of work rather than a permanent footnote.
$blockers = @{
    # PLY-011 fell back here on 2026-09-04. Its criterion says «cancelable, CONFIGURABLE, and returns
    # to details when the next file is missing», and the first and third are true. The second is not:
    # ContinuityCountdown stores its duration in a preference and zero switches the whole chain off,
    # it is read at playback, and the only thing that writes it is the tests. That class's own comment
    # claims «the settings surface already reads and writes» the key — the surface does not exist. The
    # prototype puts it in a «Reproducción» section of Settings the application does not have.
    'PLY-011' = @{
        reason = 'The countdown is cancelable and revalidates, but nothing in the application configures it: the preference is written only by tests, and the Settings section the prototype draws for it does not exist.'
        owner = 'Engineering'
        unblockCondition = 'A Settings surface that reads and writes continuity.next-episode-countdown-seconds, with zero switching the chain off, reachable by the autonomous walk in the same change.'
    }

    # PLY-004 was blocked here from 2026-08-10 and unblocked on 2026-09-02, once the channel
    # layout could be exercised without the hardware: the block said no render endpoint on this
    # machine declares more than two channels, and that stayed true. What changed is that "the
    # player cannot" was never "it cannot be done" — Windows writes the layout, so a virtual
    # eight-channel endpoint exercised 5.1 and 7.1 end to end. The manifest recorded the
    # unblocking that day; this file kept the block, so the generator refused to run at all and
    # the manifest it produces became a document nobody could reproduce.

    # LIB-006 was blocked here on 2026-08-14 and unblocked on 2026-08-15, once the chain it named ran
    # end to end: ApplyIdentification writes what the provider knows on both callers, RefreshMetadata
    # resolves through the stored reference, and the assembled walk clicks the button and sees the
    # entry change. The condition was the click, and the click is green.

    # LIB-012 was blocked here on 2026-08-16 and unblocked the same day, once the rename could
    # rename: TitleFileNamePolicy composes the name in the Plex/Jellyfin/Kodi convention, its caller
    # in OpenRenameAsync stopped asking for the name the file already had, and the walk presses
    # consent, Rename and Undo with the effect read off the file system.

    # The 2026-08-08 audit found a family of one defect: a component built, registered, and tested,
    # that nothing in the assembled application ever invokes. Each row below names its instance.
    'PRD-002' = [ordered]@{
        reason           = 'El ciclo MSIX se verificó contra una copia resellada con un certificado desechable; el artefacto publicado no va firmado y Windows lo rechaza con 0x80073D2C en una máquina corriente. / The MSIX cycle was verified against a copy re-signed with a throwaway certificate; the published artifact is unsigned and Windows refuses it with 0x80073D2C on an ordinary machine.'
        owner            = 'Product Owner'
        unblockCondition = 'Una decisión de firma (certificado, Store, o canal ZIP como única vía) y repetir el ciclo de instalación sobre el artefacto realmente publicado. / A signing decision (certificate, Store, or ZIP as the only channel) and a repeat of the install cycle on the artifact actually published.'
    }
}

$matrix = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/FEATURES.md') -Raw
# The release is captured as written rather than as one of a list spelled out here. On
# 2026-09-03 this same pattern, copied into the test suite, named three releases while five
# rows carried a fourth: those rows did not fail to parse, they were simply not there, and
# every count taken from the matrix was short by five with nothing to show for it. Here the
# 46-commitment check below is what catches an MVP row going missing.
$rowPattern = '(?m)^\|\s*(?<id>[A-Z][A-Z0-9]{1,4}-[0-9]{3})\s*\|(?<feature>[^|]*)\|\s*(?<target>[^|]+?)\s*\|\s*(?<status>[^|]+?)\s*\|(?<criterion>[^|]*)\|(?<evidence>.*)\|\s*$'

$features = @()
foreach ($match in [regex]::Matches($matrix, $rowPattern)) {
    if ($match.Groups['target'].Value -ne 'MVP') { continue }

    $id = $match.Groups['id'].Value
    $status = $match.Groups['status'].Value
    $evidence = @([regex]::Matches($match.Groups['evidence'].Value, '\[[^\]]+\]\((?<target>[^)]+)\)') |
        ForEach-Object { $_.Groups['target'].Value.Trim() } |
        Where-Object { $_ -notmatch '^https?://' } |
        ForEach-Object { 'docs/' + ($_ -split '#', 2)[0] } |
        Select-Object -Unique)

    # The task that produced a piece of evidence is the stem of its filename: T39B-assembly.md is
    # T39B's, C7-recovery-gate.md is the C7 gate's.
    $tasks = @($evidence |
        ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) } |
        ForEach-Object { ($_ -split '-', 2)[0] } |
        Where-Object { $_ -match '^(T|C)[0-9]+' } |
        Select-Object -Unique |
        Sort-Object)

    $prefix = ($id -split '-')[0]
    $tests = @($suitesByPrefix[$prefix] + $extraSuites[$id] | Where-Object { $_ } | Select-Object -Unique | Sort-Object)

    $feature = [ordered]@{
        id       = $id
        status   = $status
        tasks    = $tasks
        tests    = $tests
        evidence = @($evidence | Sort-Object)
    }

    if ($blockers.ContainsKey($id)) {
        if ($status -in @('VERIFIED', 'OUT_OF_SCOPE')) {
            throw "$id is $status but still carries a blocker. Remove the block or the status."
        }

        $feature['blocker'] = $blockers[$id]
    }
    elseif ($status -notin @('VERIFIED', 'OUT_OF_SCOPE')) {
        throw "$id is $status with no blocker declared. An unsettled commitment has to say what is blocking it."
    }

    $features += $feature
}

if ($features.Count -ne 46) { throw "Expected 46 MVP commitments, found $($features.Count)." }

[xml]$buildProps = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = ([string]$buildProps.Project.PropertyGroup.Version).Trim()

# The manifest describes an artifact, so its provenance is the artifact's own — the commit the
# package was built from, recorded by eng/package-x64.ps1 — rather than whatever HEAD happens to be
# when this runs. Regenerating the manifest is part of cutting a release; see docs/release/RELEASING.
$contentsPath = Join-Path $packageRootFull 'contents.json'
if (-not (Test-Path -LiteralPath $contentsPath)) {
    throw "There is no package at $packageRootFull. Run eng/package-x64.ps1 first."
}

$contents = Get-Content -LiteralPath $contentsPath -Raw | ConvertFrom-Json

$artifacts = @()
$sumsPath = Join-Path $packageRootFull 'SHA256SUMS.txt'
foreach ($line in Get-Content -LiteralPath $sumsPath | Where-Object { $_.Trim() }) {
    $parts = $line -split '\s+', 2
    $artifacts += [ordered]@{ name = $parts[1].Trim().TrimStart('*'); sha256 = $parts[0].Trim() }
}

if ($artifacts.Count -eq 0) {
    throw "No published hashes were found at $sumsPath. Run eng/package-x64.ps1 first."
}

if ($contents.version -ne $version) {
    throw "The package was built as $($contents.version) but Directory.Build.props declares $version."
}

$manifest = [ordered]@{
    release   = 'MVP'
    version   = $version
    commit    = [string]$contents.commit
    runtime   = 'win-x64'
    signed    = $false
    artifacts = $artifacts
    summary   = [ordered]@{
        total       = $features.Count
        verified    = @($features | Where-Object { $_.status -eq 'VERIFIED' }).Count
        outOfScope  = @($features | Where-Object { $_.status -eq 'OUT_OF_SCOPE' }).Count
        blocked     = @($features | Where-Object { $_.Contains('blocker') }).Count
    }
    features  = $features
}

Set-Content -LiteralPath $outputPath -Value ($manifest | ConvertTo-Json -Depth 10) -Encoding utf8NoBOM

Write-Output ("Verification manifest: {0}" -f $outputPath)
Write-Output ("{0} commitments — {1} verified, {2} out of scope, {3} blocked." -f
    $manifest.summary.total, $manifest.summary.verified, $manifest.summary.outOfScope, $manifest.summary.blocked)
