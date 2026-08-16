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

    $addedFiles = @(git diff --name-only --diff-filter=A $BaseRef HEAD -- src |
            Where-Object { $_ -like '*.cs' })
    if ($LASTEXITCODE -ne 0) { Write-Error 'git diff failed.'; exit 1 }

    # A new path is not the same thing as new code. Splitting a large file into partials (ARQ-006)
    # creates paths whose every line already shipped, and holding moved code to a coverage bar it
    # never had to meet would price refactoring out of the repository — the gate would be pushing
    # against the tidying it exists to make safe.
    #
    # So "new" is decided by content: a file whose non-trivial lines nearly all already existed
    # somewhere in the base ref is a move, and it is exempted out loud rather than silently. Nothing
    # is weakened by this. To smuggle uncovered code past the gate a person would have to have
    # written it into the base ref first, where this same gate would have held it.
    # Only executable code counts on either side. Comments carry no coverage, and a moved block
    # arriving with the explanation it always deserved would otherwise read as new code.
    $isCode = {
        param($text)
        $text.Length -ge 12 -and
        -not $text.StartsWith('using ') -and
        -not $text.StartsWith('//') -and
        -not $text.StartsWith('*') -and
        -not $text.StartsWith('/*')
    }

    $baseCorpus = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($tracked in @(git ls-tree -r --name-only $BaseRef -- src | Where-Object { $_ -like '*.cs' })) {
        foreach ($line in @(git show "${BaseRef}:${tracked}" 2>$null)) {
            $trimmed = $line.Trim()
            if (& $isCode $trimmed) { [void]$baseCorpus.Add($trimmed) }
        }
    }

    $newFiles = @()
    $movedFiles = @()
    foreach ($file in $addedFiles) {
        # Read the file out of HEAD, not off disk: the comparison is HEAD against the base ref, and a
        # path added in HEAD may already have been moved or deleted again in the working tree.
        $lines = @(git show "HEAD:$file" 2>$null |
                ForEach-Object { $_.Trim() } |
                Where-Object { & $isCode $_ })
        $known = @($lines | Where-Object { $baseCorpus.Contains($_) }).Count
        $movedShare = if ($lines.Count -gt 0) { 1.0 * $known / $lines.Count } else { 0.0 }
        # 0.85 rather than a rounder 0.9 because a split's unavoidable scaffolding — the partial
        # class declaration and one signature per module — is genuinely new text, and it weighs
        # proportionally more the smaller the module is. A file that is one seventh new text and
        # six sevenths lines lifted verbatim is a move.
        if ($lines.Count -gt 0 -and $movedShare -ge 0.85) {
            $movedFiles += [pscustomobject]@{ File = $file; MovedPct = [math]::Round(100.0 * $movedShare, 1) }
        }
        else {
            $newFiles += $file
        }
    }

    if ($movedFiles.Count -gt 0) {
        Write-Output "Coverage gate: these paths are new but their code is not, so they are held to the bar their code already met:"
        $movedFiles | Format-Table -AutoSize | Out-String -Width 200 | Write-Output
    }

    if ($newFiles.Count -eq 0) {
        Write-Output "Coverage gate: no source file is new against $BaseRef."
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

    # Per file, the same arithmetic the gate uses, so a watched file and a new one are judged alike.
    function Measure-File {
        param([hashtable]$Coverage, [string]$Path)

        $normalised = $Path -replace '\\', '/'
        $key = $Coverage.Keys | Where-Object { $_.EndsWith($normalised, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
        if (-not $key) { return $null }

        $entry = $Coverage[$key]
        $totalLines = $entry.Lines.Count
        $coveredLines = @($entry.Lines.Values | Where-Object { $_ }).Count
        return [pscustomobject]@{
            LinePct   = if ($totalLines -gt 0) { 100.0 * $coveredLines / $totalLines } else { 100.0 }
            BranchPct = if ($entry.BranchesTotal -gt 0) { 100.0 * $entry.BranchesCovered / $entry.BranchesTotal } else { 100.0 }
        }
    }

    # ------------------------------------------------------------- watched files
    <#
        What the gate above cannot see. Newness is decided against the base ref, so a file that
        shipped long ago and gets worse is watched by nobody — and that is measured, not feared:
        ARQ-004 thinned PlayerVersionsViewModel, took its covered lines with it, and dropped it from
        60.61/27.27 to 45.45/14.29 without a single gate saying a word.

        Each entry carries the floor its code meets today, so the list works like the orphan list in
        ServiceConsumptionTests: a file below its floor fails, and so does one above it, because the
        floor has to be raised to what was actually measured. The debt can only shrink, and lowering
        a floor is a visible line in a diff rather than a quiet drift.
    #>
    $watched = @(
        # TST-001 named all three on 2026-08-09 and all three are paid: two on 2026-08-10 and this
        # one, the last, with unit tests aimed at its decisions rather than at the scans that
        # already walked its happy path. The list stays whatever the numbers say — a file that
        # reaches the bar is watched at the bar, not dropped.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Application/Discovery/ReconcileScannedFiles.cs'
            Lines    = 100.00
            Branches = 100.00
        }
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Infrastructure/FileSystem/CompositeFileIdentityProvider.cs'
            Lines    = 100.00
            Branches = 100.00
        }
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Presentation/Player/PlayerVersionsViewModel.cs'
            Lines    = 100.00
            Branches = 100.00
        }

        # The two files the isolation rule went through on 2026-08-16. Neither was new, so neither
        # would have been measured by the gate above, and both decide where something leaves the
        # application: which registry key a startup entry is written to, and whether an address
        # reaches a browser at all. They arrive at the top and are held there.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/AppDataPaths.cs'
            Lines    = 100.00
            Branches = 100.00
        }
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Metadata/ShellExternalLinkLauncher.cs'
            Lines    = 100.00
            Branches = 100.00
        }
    )

    $watchRows = @()
    $watchFailures = @()
    foreach ($entry in $watched) {
        $measured = Measure-File -Coverage $coverageByFile -Path $entry.File
        if (-not $measured) {
            $watchFailures += "$($entry.File) is watched but absent from the coverage report; a watched file that cannot be measured is not watched."
            $watchRows += [pscustomobject]@{ File = $entry.File; LinePct = 'absent'; BranchPct = 'absent'; Floor = "$($entry.Lines)/$($entry.Branches)"; Verdict = 'FAIL' }
            continue
        }

        $linePct = [math]::Round($measured.LinePct, 2)
        $branchPct = [math]::Round($measured.BranchPct, 2)
        $verdict = 'PASS'
        if ($linePct -lt $entry.Lines -or $branchPct -lt $entry.Branches) {
            $verdict = 'FAIL (below its floor)'
            $watchFailures += "$($entry.File) fell to $linePct% lines / $branchPct% branches, below the $($entry.Lines)/$($entry.Branches) floor it is held to."
        }
        elseif ($linePct -gt $entry.Lines -or $branchPct -gt $entry.Branches) {
            $verdict = 'FAIL (raise the floor)'
            $watchFailures += "$($entry.File) now reaches $linePct% lines / $branchPct% branches; raise its floor in eng/check-coverage.ps1 so the debt cannot come back."
        }

        $watchRows += [pscustomobject]@{
            File      = $entry.File
            LinePct   = $linePct
            BranchPct = $branchPct
            Floor     = "$($entry.Lines)/$($entry.Branches)"
            Verdict   = $verdict
        }
    }

    Write-Output "Coverage gate: watched files, held at every run whatever their age:"
    $watchRows | Format-Table -AutoSize | Out-String -Width 200 | Write-Output

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

    if ($rows.Count -gt 0) {
        $rows | Format-Table -AutoSize | Out-String -Width 200 | Write-Output
    }

    @{ new = $rows; watched = $watchRows } | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $resultsRoot 'coverage-gate.json') -Encoding utf8NoBOM

    foreach ($problem in $watchFailures) { Write-Error $problem }
    if ($failures.Count -gt 0) {
        Write-Error ("New files below {0}% lines / {1}% branches against {2}: {3}" -f
            $MinimumLinePercent, $MinimumBranchPercent, $BaseRef, ($failures -join ', '))
    }

    if ($watchFailures.Count -gt 0 -or $failures.Count -gt 0) { exit 1 }

    Write-Output ("Coverage gate: {0} new file(s) against {1} and {2} watched file(s) are where they have to be." -f
        $newFiles.Count, $BaseRef, $watched.Count)
    exit 0
}
finally {
    Pop-Location
}
