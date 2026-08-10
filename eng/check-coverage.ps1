# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    TST-001: the automated coverage gate. Every source file that is new against the base ref
    must arrive with at least the minimum line and branch coverage in this run's reports.

.DESCRIPTION
    The run's Cobertura files are merged with reportgenerator and read per file. A new file is
    one that exists in HEAD and not in the base ref (a plain tree comparison, so a shallow CI
    checkout needs no merge base). A new file that never appears in the merged report has no
    instrumentable lines — interfaces and pure contracts — and passes with that stated; a file
    with real code that no test executes appears with zero hits and fails loudly.

    In this repository main advances by fast-forward together with the working branch, so on CI
    the diff is usually empty and the gate's teeth are local: it runs inside eng/verify.ps1
    before anything is pushed. On a pull request from elsewhere the diff is real and CI bites.
#>
[CmdletBinding()]
param(
    [string]$ResultsDirectory = 'artifacts/test-results/verify-win-x64',

    [string]$BaseRef = 'origin/main',

    [double]$MinimumLinePercent = 96.0,

    [double]$MinimumBranchPercent = 96.0
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$resultsRoot = if ([IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $repoRoot $ResultsDirectory }

Push-Location $repoRoot
try {
    git rev-parse --verify --quiet "$BaseRef^{commit}" *> $null
    if ($LASTEXITCODE -ne 0) {
        # A gate that cannot see its base ref must say so and fail, never quietly pass.
        git fetch origin main *> $null
        git rev-parse --verify --quiet "$BaseRef^{commit}" *> $null
        if ($LASTEXITCODE -ne 0) {
            Write-Error "The base ref '$BaseRef' does not exist and could not be fetched; the coverage gate has nothing to compare against."
            exit 1
        }
    }

    $newFiles = @(git diff --name-only --diff-filter=A $BaseRef HEAD -- src |
            Where-Object { $_ -like '*.cs' })
    if ($LASTEXITCODE -ne 0) { Write-Error 'git diff failed.'; exit 1 }

    if ($newFiles.Count -eq 0) {
        Write-Output "Coverage gate: no source file is new against $BaseRef; nothing to hold."
        exit 0
    }

    $reports = @(Get-ChildItem -LiteralPath $resultsRoot -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue)
    if ($reports.Count -eq 0) {
        Write-Error "No Cobertura report found under $resultsRoot; run the test suites with coverage first."
        exit 1
    }

    $mergedDirectory = Join-Path $resultsRoot 'coverage-gate'
    # Generated sources live under obj/ and are gone by the time reports merge; excluding them
    # keeps the merge quiet and no hand-written file matches the filter.
    dotnet tool run reportgenerator `
        "-reports:$(($reports | ForEach-Object { $_.FullName }) -join ';')" `
        "-targetdir:$mergedDirectory" `
        '-reporttypes:Cobertura' `
        '-filefilters:-*.g.cs' `
        '-verbosity:Off'
    if ($LASTEXITCODE -ne 0) { Write-Error 'reportgenerator failed to merge the coverage reports.'; exit 1 }

    # Per file: line number → executed, plus branch covered/total. Partial classes repeat a file
    # across <class> nodes, so lines merge by number and a line executed anywhere counts once.
    $coverageByFile = @{}
    $merged = [xml](Get-Content -LiteralPath (Join-Path $mergedDirectory 'Cobertura.xml') -Raw)
    foreach ($class in $merged.SelectNodes('//class')) {
        $filename = ($class.filename -replace '\\', '/')
        if (-not $coverageByFile.ContainsKey($filename)) {
            $coverageByFile[$filename] = @{ Lines = @{}; BranchesCovered = 0; BranchesTotal = 0 }
        }

        $entry = $coverageByFile[$filename]
        foreach ($line in $class.SelectNodes('lines/line')) {
            $number = [int]$line.number
            $hit = [int64]$line.hits -gt 0
            if (-not $entry.Lines.ContainsKey($number) -or $hit) {
                $entry.Lines[$number] = $hit
            }

            if ($line.'condition-coverage' -match '\((?<covered>\d+)/(?<total>\d+)\)') {
                $entry.BranchesCovered += [int]$Matches['covered']
                $entry.BranchesTotal += [int]$Matches['total']
            }
        }
    }

    $rows = @()
    $failures = @()
    foreach ($file in $newFiles) {
        $normalised = $file -replace '\\', '/'
        $key = $coverageByFile.Keys | Where-Object { $_.EndsWith($normalised, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
        if (-not $key) {
            $rows += [pscustomobject]@{ File = $file; LinePct = 'n/a'; BranchPct = 'n/a'; Verdict = 'PASS (no instrumentable lines)' }
            continue
        }

        $entry = $coverageByFile[$key]
        $totalLines = $entry.Lines.Count
        $coveredLines = @($entry.Lines.Values | Where-Object { $_ }).Count
        $linePct = if ($totalLines -gt 0) { 100.0 * $coveredLines / $totalLines } else { 100.0 }
        $branchPct = if ($entry.BranchesTotal -gt 0) { 100.0 * $entry.BranchesCovered / $entry.BranchesTotal } else { 100.0 }

        $passes = $linePct -ge $MinimumLinePercent -and $branchPct -ge $MinimumBranchPercent
        if (-not $passes) { $failures += $file }
        $rows += [pscustomobject]@{
            File      = $file
            LinePct   = [math]::Round($linePct, 2)
            BranchPct = [math]::Round($branchPct, 2)
            Verdict   = if ($passes) { 'PASS' } else { 'FAIL' }
        }
    }

    $rows | Format-Table -AutoSize | Out-String -Width 200 | Write-Output
    $rows | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $resultsRoot 'coverage-gate.json') -Encoding utf8NoBOM

    if ($failures.Count -gt 0) {
        Write-Error ("New files below {0}% lines / {1}% branches against {2}: {3}" -f
            $MinimumLinePercent, $MinimumBranchPercent, $BaseRef, ($failures -join ', '))
        exit 1
    }

    Write-Output ("Coverage gate: {0} new file(s) against {1} all reach {2}% lines / {3}% branches." -f
        $newFiles.Count, $BaseRef, $MinimumLinePercent, $MinimumBranchPercent)
    exit 0
}
finally {
    Pop-Location
}
