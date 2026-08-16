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

# The three commitments this hardware cannot settle. Each one names what would settle it, so the
# block is a piece of work rather than a permanent footnote.
$blockers = @{
    'PLY-004' = [ordered]@{
        reason           = 'Ningún endpoint de render activo declara más de dos canales, así que la selección de 5.1 y 7.1 no se ha ejercido sobre hardware real. Los auriculares conectados son estéreo con virtualización, que Windows expone como dos canales. / No active render endpoint declares more than two channels, so 5.1 and 7.1 selection has not been exercised on real hardware.'
        owner            = 'Engineering'
        unblockCondition = 'Cualquier endpoint de render que declare seis u ocho canales, y repetir la matriz de salida de audio: un receptor A/V o una televisión por HDMI, una salida S/PDIF con codificación multicanal habilitada en el controlador, o unos auriculares USB que expongan 5.1 o 7.1 de verdad en lugar de virtualizarlo. / Any render endpoint declaring six or eight channels, and a repeat of the audio output matrix: an A/V receiver or television over HDMI, an S/PDIF output with multichannel encoding enabled in the driver, or USB headphones that expose real 5.1 or 7.1 rather than virtualising it.'
    }

    # LIB-006 was blocked here on 2026-08-14 and unblocked on 2026-08-15, once the chain it named ran
    # end to end: ApplyIdentification writes what the provider knows on both callers, RefreshMetadata
    # resolves through the stored reference, and the assembled walk clicks the button and sees the
    # entry change. The condition was the click, and the click is green.

    'LIB-012' = [ordered]@{
        reason           = 'El renombrado no puede proponer ningún nombre: la aplicación ensamblada pide renombrar cada archivo al nombre que ya tiene —`new RenameRequest(file.Path, Path.GetFileName(file.Path))`, el único de producción del repositorio— y `RenamePolicy` lo descarta correctamente como `NoChange`. El plan sale siempre vacío, así que Renombrar y Deshacer no pueden ejecutarse nunca y la casilla de consentimiento guarda una decisión que no se ofrece. Las pruebas que lo daban por verificado midieron la política y la seguridad —que no se mueven carpetas, que un conflicto impide ejecutar— y ninguna midió que un plan llegara a contener una operación. / The rename can propose no name at all: the assembled application asks to rename each file to the name it already has, and the policy correctly discards that as NoChange, so the plan is always empty and neither button can ever run. What was verified was the safety, never that a plan contained an operation.'
        owner            = 'Product'
        unblockCondition = 'Un componente de dominio que componga el nombre a partir de la ficha, con el convenio ya decidido —el que comparten Plex, Jellyfin y Kodi, que este proyecto ya sigue en `TrailerDiscoveryPolicy`—: `Título (Año).ext` para películas y `Serie (Año) - SxxEyy - Título.ext` para episodios; su llamante en `OpenRenameAsync`; y el paseo pulsando consentimiento, Renombrar y Deshacer con el efecto leído del sistema de archivos. / A domain component composing the name from the entry in the already-decided convention, its caller, and the walk pressing consent, Rename and Undo with the effect read off the file system.'
    }

    # The 2026-08-08 audit found a family of one defect: a component built, registered, and tested,
    # that nothing in the assembled application ever invokes. Each row below names its instance.
    'PRD-002' = [ordered]@{
        reason           = 'El ciclo MSIX se verificó contra una copia resellada con un certificado desechable; el artefacto publicado no va firmado y Windows lo rechaza con 0x80073D2C en una máquina corriente. / The MSIX cycle was verified against a copy re-signed with a throwaway certificate; the published artifact is unsigned and Windows refuses it with 0x80073D2C on an ordinary machine.'
        owner            = 'Product Owner'
        unblockCondition = 'Una decisión de firma (certificado, Store, o canal ZIP como única vía) y repetir el ciclo de instalación sobre el artefacto realmente publicado. / A signing decision (certificate, Store, or ZIP as the only channel) and a repeat of the install cycle on the artifact actually published.'
    }
}

$matrix = Get-Content -LiteralPath (Join-Path $repoRoot 'docs/FEATURES.md') -Raw
$rowPattern = '(?m)^\|\s*(?<id>[A-Z0-9]+-[0-9]+)\s*\|(?<feature>[^|]*)\|\s*(?<target>MVP|STABLE|POST_STABLE)\s*\|\s*(?<status>[A-Z_]+)\s*\|(?<criterion>[^|]*)\|(?<evidence>.*)\|\s*$'

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
