# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Materialises the approved container/codec matrix from its recipes.

.DESCRIPTION
    Every sample is produced from FFmpeg's own synthetic generators, so the matrix is reproducible
    on any machine and no third-party or personal media is ever involved. The output tree is ignored
    by Git; the repository stores the recipes, never the files.

    A sample whose encoder the local FFmpeg build cannot provide is reported as skipped with the
    exact missing encoder. It is never replaced by a substitute.
#>
[CmdletBinding()]
param(
    [string]$Output = 'artifacts/test-media',

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot 'tests/ApSolutions.LocalMedia.MediaTests/Fixtures/media-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "The media manifest was not found at $manifestPath."
}

function Resolve-Encoder {
    if ($env:FFMPEG_PATH -and (Test-Path -LiteralPath $env:FFMPEG_PATH)) {
        return $env:FFMPEG_PATH
    }

    $command = Get-Command 'ffmpeg' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    return $null
}

$encoder = Resolve-Encoder
if (-not $encoder) {
    Write-Warning 'ffmpeg was not found. Set FFMPEG_PATH or install ffmpeg; no sample was generated.'
    exit 0
}

$availableEncoders = @{}
& $encoder -hide_banner -loglevel error -encoders 2>$null | ForEach-Object {
    $fields = $_ -split '\s+' | Where-Object { $_ }
    if ($fields.Count -ge 2 -and $fields[0].Length -eq 6) {
        $availableEncoders[$fields[1]] = $true
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$outputRoot = if ([IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $repoRoot $Output }
$generated = 0
$reused = 0
$skipped = @()

function Get-SamplePath([object]$sample) {
    Join-Path $outputRoot ($sample.relativePath -replace '/', [IO.Path]::DirectorySeparatorChar)
}

function New-Sample([object]$sample) {
    $destination = Get-SamplePath $sample
    if ((Test-Path -LiteralPath $destination) -and -not $Force) {
        $script:reused++
        return $destination
    }

    $missing = @($sample.requiredEncoders | Where-Object { -not $availableEncoders.ContainsKey($_) })
    if ($missing.Count -gt 0) {
        $script:skipped += [pscustomobject]@{ Id = $sample.id; MissingEncoders = ($missing -join ', ') }
        return $null
    }

    $directory = Split-Path -Parent $destination
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    if ($sample.derivedFrom) {
        $parent = $manifest.samples | Where-Object { $_.id -eq $sample.derivedFrom }
        if (-not $parent) { throw "Sample $($sample.id) derives from an unknown parent." }
        $parentPath = New-Sample $parent
        if (-not $parentPath) {
            $script:skipped += [pscustomobject]@{ Id = $sample.id; MissingEncoders = "parent $($sample.derivedFrom) unavailable" }
            return $null
        }

        $bytes = [IO.File]::ReadAllBytes($parentPath)
        $length = [Math]::Max(1, [int]($bytes.Length * $sample.truncateToFraction))
        [IO.File]::WriteAllBytes($destination, $bytes[0..($length - 1)])
        $script:generated++
        return $destination
    }

    # Some samples mux a subtitle track from a companion file, and their recipe names it with a
    # placeholder. Writing it here is what makes the matrix reproducible from nothing: on a machine
    # that already had the output tree the placeholder never mattered, because the sample was reused
    # rather than produced, and on a machine without ffmpeg the script returns before it could.
    $recipe = [string]$sample.recipe
    if ($sample.companionTextPath) {
        $companion = Join-Path $outputRoot ($sample.companionTextPath -replace '/', [IO.Path]::DirectorySeparatorChar)
        $companionDirectory = Split-Path -Parent $companion
        if (-not (Test-Path -LiteralPath $companionDirectory)) {
            New-Item -ItemType Directory -Force -Path $companionDirectory | Out-Null
        }

        Set-Content -LiteralPath $companion -Value ([string]$sample.companionText) -Encoding utf8NoBOM -NoNewline
        $recipe = $recipe.Replace('{{companion}}', ($companion -replace '\\', '/'))
    }

    if ($recipe.Contains('{{')) {
        throw "The recipe for $($sample.id) still carries an unresolved placeholder."
    }

    $arguments = "-hide_banner -loglevel error -nostdin -y $recipe `"$destination`""
    $process = Start-Process -FilePath $encoder -ArgumentList $arguments -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $destination)) {
        throw "The encoder failed to produce $($sample.id) (exit $($process.ExitCode))."
    }

    $script:generated++
    return $destination
}

$rows = foreach ($sample in $manifest.samples) {
    $path = New-Sample $sample
    if (-not $path) { continue }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    [pscustomobject]@{
        Id       = $sample.id
        Outcome  = $sample.expectedOutcome
        Bytes    = (Get-Item -LiteralPath $path).Length
        Sha256   = $hash
    }
}

$rows | Format-Table -AutoSize | Out-String | Write-Output
Write-Output "Generated $generated sample(s), reused $reused, skipped $($skipped.Count)."
foreach ($entry in $skipped) {
    Write-Warning "Skipped $($entry.Id): missing $($entry.MissingEncoders)."
}
