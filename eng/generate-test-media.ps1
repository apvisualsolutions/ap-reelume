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

    Every call to FFmpeg is bounded and every sample is announced before it is produced. Six of the
    ten CI runs of 2026-08-10 were cancelled at the sixty-minute ceiling with nothing in the log
    between "Build succeeded" and the cancellation fifty-six minutes later: this step had started an
    encoder that never came back, and an unbounded wait turned one wedged encode into an hour of
    silence reported as an infrastructure hiccup. The whole matrix takes 1,6 seconds to produce on a
    development machine, so the ceiling below is not a performance budget — it is the difference
    between a named failure and a job that dies without saying why.
#>
[CmdletBinding()]
param(
    [string]$Output = 'artifacts/test-media',

    [switch]$Force,

    # Six hundred times the measured cost of the slowest sample. Anything that reaches it is wedged,
    # not slow.
    [ValidateRange(1, 3600)]
    [int]$SampleTimeoutSeconds = 60
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

function Invoke-Encoder {
    <#
    .SYNOPSIS
        Runs FFmpeg with a ceiling, and kills the whole tree when it is reached.
    .DESCRIPTION
        `Start-Process -Wait` has no timeout, so a child that never exits is indistinguishable from
        one still working — which is exactly how a job spends an hour saying nothing. The tree is
        killed rather than the process alone: FFmpeg's own children would otherwise outlive it and
        keep the file handles that the next attempt needs.
    #>
    param(
        [Parameter(Mandatory)][string]$Arguments,
        [Parameter(Mandatory)][string]$What,
        [string]$StandardOutputPath
    )

    $startOptions = @{
        FilePath     = $encoder
        ArgumentList = $Arguments
        NoNewWindow  = $true
        PassThru     = $true
    }
    if ($StandardOutputPath) { $startOptions['RedirectStandardOutput'] = $StandardOutputPath }

    $process = Start-Process @startOptions
    if (-not $process.WaitForExit($SampleTimeoutSeconds * 1000)) {
        try { $process.Kill($true) } catch { }
        throw "The encoder did not finish $What within $SampleTimeoutSeconds s and was killed. Arguments: $Arguments"
    }

    return $process.ExitCode
}

$availableEncoders = @{}
$encoderList = [IO.Path]::GetTempFileName()
try {
    # Bounded like everything else here: a probe that never returns is the same hour of silence as an
    # encode that never returns, and it happens before a single sample has been named.
    $null = Invoke-Encoder `
        -Arguments '-hide_banner -loglevel error -encoders' `
        -What 'listing the encoders it provides' `
        -StandardOutputPath $encoderList
    foreach ($line in Get-Content -LiteralPath $encoderList) {
        $fields = $line -split '\s+' | Where-Object { $_ }
        if ($fields.Count -ge 2 -and $fields[0].Length -eq 6) {
            $availableEncoders[$fields[1]] = $true
        }
    }
}
finally {
    Remove-Item -LiteralPath $encoderList -Force -ErrorAction SilentlyContinue
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

    # Named before it is produced, not after. A build that dies mid-matrix has to say which recipe it
    # was on; the table at the end only prints for a run that finished.
    Write-Host "  encoding $($sample.id) …"
    $arguments = "-hide_banner -loglevel error -nostdin -y $recipe `"$destination`""
    $exitCode = Invoke-Encoder -Arguments $arguments -What "sample $($sample.id)"
    if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $destination)) {
        throw "The encoder failed to produce $($sample.id) (exit $exitCode)."
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
