# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Which coverage floors this working tree is about to leave behind, before CI says so.

.DESCRIPTION
    eng/check-coverage.ps1 refuses a floor that is too low exactly as it refuses one that is too
    high, so a batch that improves a file turns a CI run red with "N improved". That red used to be
    called the cost of doing business — CLAUDE.md said subir cobertura cuesta dos vueltas — and it
    was an excuse dressed as a rule: on 2026-08-31 a file was measured here at 83.33% of branches,
    CI answered 83, and the number was written into a handover as a warning instead of into
    eng/coverage-debt.txt as a fix.

    This answers the same question without spending the run. It measures the suites you name, reads
    the reports with the gate's own arithmetic, and lists every watched file whose floor no longer
    matches — plus every file new against the base ref that does not clear 96/96.

    It does NOT write eng/coverage-debt.txt. That file comes from CI's coverage-debt artefact and
    from nowhere else, because seven files depend on hardware a hosted runner does not have and a
    floor measured here would be a floor for a machine that never verifies anything.

    ITS SILENCE IS NOT A CERTIFICATE, and that is measured rather than cautionary. On 2026-09-01 it
    named one file and CI then named FIVE, four of which it had said nothing about at all; the run
    was lost to exactly the reassurance this paragraph now withdraws. It misses in both directions —
    the same day it reported NextLessonPolicy at 91/80 from three suites while CI, merging ten, did
    not raise it at all.

    The mechanism is the -Suites note below and it is worth stating plainly here: this reads the
    suites you name, CI merges twenty reports from ten. A file only some of its suites measure reads
    low, and a file none of them measure does not read at all. So a clean run here means "nothing
    among what I measured", never "nothing".

.PARAMETER Suites
    Test project names under tests/, without the ApSolutions.LocalMedia. prefix. The default three
    are the ones that run clean and fast here. Name more when your change reaches further: what
    matters is that every suite touching the files you changed is in the list, because a file
    measured by half its suites reads low.

.PARAMETER BaseRef
    The ref new files are decided against, the same one the gate uses.

.PARAMETER SkipRun
    Read the reports already in the results directory instead of running the suites again.

.EXAMPLE
    pwsh -NoProfile -File eng/preview-coverage-floors.ps1
    pwsh -NoProfile -File eng/preview-coverage-floors.ps1 -Suites Domain.Tests,UiTests,AccessibilityTests
#>
[CmdletBinding()]
param(
    [string[]]$Suites = @('Domain.Tests', 'Application.Tests', 'UiTests'),
    [string]$BaseRef = 'origin/main',
    [switch]$SkipRun
)

$ErrorActionPreference = 'Stop'

# `pwsh -File` hands a comma-separated value through as ONE string rather than an array, and every
# script in this folder is invoked that way. Without this the first run answers "No such suite:
# Domain.Tests,Application.Tests,UiTests", which reads like a typo and is the calling convention.
$Suites = @($Suites | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

$repoRoot = Split-Path -Parent $PSScriptRoot
$resultsRoot = Join-Path $repoRoot 'artifacts/coverage-preview'
$debtFile = Join-Path $PSScriptRoot 'coverage-debt.txt'

<#
    The merge, keyed by line number across every report: a line is covered if any report covered it,
    and a branch line keeps the best fraction any report reached. Then both are counted once.

    Both halves of that were got wrong before they were right, and each was a number that looked
    plausible. Choosing the best whole REPORT instead of the best line read AddLibraryRoot at 78%
    of branches where CI reads 92, because no single suite takes every branch and the merge does.
    And summing branches per <class> counts one line several times, because a type and each of its
    lambdas are separate <class> entries over the same lines: 28 branches where the file has 14.
    Keyed by number, this answers 100/92.86 — the 100/92 CI wrote.
#>
function Read-FileCoverage {
    param([string]$ResultsDirectory)

    $byFile = @{}
    $reports = @(Get-ChildItem -Path $ResultsDirectory -Filter 'coverage.cobertura.xml' -Recurse -ErrorAction SilentlyContinue)
    if ($reports.Count -eq 0) {
        throw "No coverage report under $ResultsDirectory, so this measured nothing at all."
    }

    foreach ($report in $reports) {
        [xml]$document = Get-Content -LiteralPath $report.FullName -Raw
        foreach ($class in $document.SelectNodes('//class')) {
            # Cobertura writes the path relative to its <sources> root, which is src/ — so the
            # filename arrives as "ApSolutions.LocalMedia.Application/…", with no src/ in it at all.
            # A filter looking for src/ therefore matches nothing and reports a confident zero, which
            # is what the first version of this script did.
            $filename = ($class.filename -replace '\\', '/').TrimStart('/')
            $relative = if ($filename.StartsWith('src/')) { $filename } else { "src/$filename" }
            if (-not $relative.EndsWith('.cs')) { continue }

            if (-not $byFile.ContainsKey($relative)) {
                $byFile[$relative] = [pscustomobject]@{ Hits = @{}; Branches = @{} }
            }

            $entry = $byFile[$relative]
            foreach ($line in $class.SelectNodes('.//line')) {
                $number = [int]$line.number
                $hits = [int]$line.hits
                if (-not $entry.Hits.ContainsKey($number) -or $entry.Hits[$number] -lt $hits) {
                    $entry.Hits[$number] = $hits
                }

                if ($line.branch -eq 'True' -and $line.'condition-coverage') {
                    $inner = ($line.'condition-coverage' -split '\(')[1].TrimEnd(')')
                    $parts = $inner -split '/'
                    $covered = [int]$parts[0]
                    if (-not $entry.Branches.ContainsKey($number) -or $entry.Branches[$number].Covered -lt $covered) {
                        $entry.Branches[$number] = [pscustomobject]@{ Covered = $covered; Total = [int]$parts[1] }
                    }
                }
            }
        }
    }

    $result = @{}
    foreach ($relative in $byFile.Keys) {
        $entry = $byFile[$relative]
        if ($entry.Hits.Count -eq 0) { continue }
        $covered = @($entry.Hits.Values | Where-Object { $_ -gt 0 }).Count
        $branchCovered = @($entry.Branches.Values | ForEach-Object { $_.Covered } | Measure-Object -Sum).Sum
        $branchTotal = @($entry.Branches.Values | ForEach-Object { $_.Total } | Measure-Object -Sum).Sum
        $result[$relative] = [pscustomobject]@{
            LinePct   = 100.0 * $covered / $entry.Hits.Count
            BranchPct = if ($branchTotal -gt 0) { 100.0 * $branchCovered / $branchTotal } else { 100.0 }
        }
    }

    $result
}

Push-Location $repoRoot
try {
    if (-not $SkipRun) {
        Remove-Item -LiteralPath $resultsRoot -Recurse -Force -ErrorAction SilentlyContinue
        foreach ($suite in $Suites) {
            $project = Join-Path $repoRoot "tests/ApSolutions.LocalMedia.$suite"
            if (-not (Test-Path -LiteralPath $project)) {
                throw "No such suite: $suite (looked in tests/ApSolutions.LocalMedia.$suite)."
            }

            Write-Output "Measuring $suite..."
            dotnet test $project -c Release -m:1 `
                --settings (Join-Path $PSScriptRoot 'test.runsettings') `
                --collect:"XPlat Code Coverage" `
                --results-directory (Join-Path $resultsRoot $suite) | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "$suite failed, so what it measured cannot be trusted."
            }
        }
    }

    $measured = Read-FileCoverage -ResultsDirectory $resultsRoot

    $debt = @(Get-Content -LiteralPath $debtFile |
        Where-Object { $_.Trim() -and -not $_.TrimStart().StartsWith('#') } |
        ForEach-Object {
            $parts = $_ -split '\s+'
            [pscustomobject]@{ File = $parts[0]; Lines = [int]$parts[1]; Branches = [int]$parts[2] }
        })

    <#
        These read higher here than on a hosted runner, so their floors move in this list every time
        and none of it means anything: a machine with audio devices, a real LibVLC and real timers
        takes branches the runner cannot. They are reported apart rather than hidden, because a
        warning that fires when it should not is what teaches people to ignore the warning.

        The list only grows by measurement — CLAUDE.md says seven files behave this way, and these
        are the ones that have actually shown up. WindowsAudioDeviceCatalog is the one the guide
        names: 79/61 here against 32/11 there.
    #>
    $readsDifferentlyHere = @(
        'src/ApSolutions.LocalMedia.Windows/Playback/WindowsAudioDeviceCatalog.cs',
        'src/ApSolutions.LocalMedia.Infrastructure/Playback/LibVlcAudioOutputAdapter.cs'
    )

    $improved = @()
    $hardware = @()
    foreach ($entry in $debt) {
        if (-not $measured.ContainsKey($entry.File)) { continue }
        $current = $measured[$entry.File]
        $lineFloor = [math]::Floor($current.LinePct)
        $branchFloor = [math]::Floor($current.BranchPct)
        if ($lineFloor -le $entry.Lines -and $branchFloor -le $entry.Branches) { continue }

        $row = [pscustomobject]@{
            File  = $entry.File
            Floor = "$($entry.Lines)/$($entry.Branches)"
            Now   = "$lineFloor/$branchFloor"
        }

        if ($readsDifferentlyHere -contains $entry.File) { $hardware += $row } else { $improved += $row }
    }

    # New files clear 96/96 with no measured ceilings, so they are worth the same look.
    $newFiles = @(git diff --name-only --diff-filter=A "$BaseRef...HEAD" -- 'src/*.cs' 2>$null)
    $newShort = @()
    foreach ($file in $newFiles) {
        $relative = ($file -replace '\\', '/')

        # THE SAME FILE ARRIVES UNDER TWO KEYS AND ONLY ONE OF THEM MATCHES GIT, which is measured
        # rather than defensive. A Cobertura report names a class by a path relative to ITS OWN
        # project, so Domain's own suite calls a file "Discovery/Thing.cs" - which this reader turns
        # into "src/Discovery/Thing.cs" - while every suite that merely loads the assembly reports
        # the full "src/ApSolutions.LocalMedia.Domain/Discovery/Thing.cs", with zero hits, because it
        # never ran there. Git only ever says the second. Looked up by that alone, a file covered
        # 100% by its own suite reads 0/0 and this script sends somebody to fix code that is already
        # right. Measured 2026-09-03 against two new Domain files whose own reports said line-rate=1.
        #
        # So the lookup takes the best of every key ending in this path, which is the same
        # "covered anywhere wins" arithmetic the merge above already applies one level down.
        $candidates = @($measured.Keys | Where-Object {
            $_ -eq $relative -or $relative.EndsWith('/' + ($_ -replace '^src/', ''))
        })

        if ($candidates.Count -eq 0) {
            $newShort += [pscustomobject]@{ File = $relative; Now = 'not measured by these suites' }
            continue
        }

        $current = [pscustomobject]@{
            LinePct   = (@($candidates | ForEach-Object { $measured[$_].LinePct }) | Measure-Object -Maximum).Maximum
            BranchPct = (@($candidates | ForEach-Object { $measured[$_].BranchPct }) | Measure-Object -Maximum).Maximum
        }
        if ([math]::Floor($current.LinePct) -lt 96 -or [math]::Floor($current.BranchPct) -lt 96) {
            $newShort += [pscustomobject]@{
                File = $relative
                Now  = "$([math]::Floor($current.LinePct))/$([math]::Floor($current.BranchPct))"
            }
        }
    }

    Write-Output ''
    Write-Output "Measured $($measured.Count) file(s) from $($Suites -join ', ')."

    if ($improved.Count -gt 0) {
        Write-Output ''
        Write-Output 'These floors are now too low, so CI will answer "N improved":'
        $improved | Format-Table -AutoSize | Out-String | Write-Output
        Write-Output 'Take the numbers from a CI run''s coverage-debt artefact, never from here.'
    }

    if ($newShort.Count -gt 0) {
        Write-Output ''
        Write-Output 'These new files do not clear 96/96, and new files admit no floor:'
        $newShort | Format-Table -AutoSize | Out-String | Write-Output
    }

    if ($improved.Count -eq 0 -and $newShort.Count -eq 0) {
        Write-Output 'No floor moves and no new file falls short, for the suites measured.'
    }

    if ($hardware.Count -gt 0) {
        Write-Output ''
        Write-Output 'Ignore these: they read higher here than on a runner with no audio device.'
        $hardware | Format-Table -AutoSize | Out-String | Write-Output
    }

    <#
        Two limitations, written here because a silence from this script is not a certificate.
        It only knows the suites it was told to run, so a file covered by one it did not run reads
        low; and seven files depend on audio, LibVLC or timers, so their numbers here are not the
        numbers CI gets — those are the ones that really do cost a second round.
    #>
    Write-Output ''
    Write-Output 'Limits: only the suites named above were measured, and the seven hardware-bound'
    Write-Output 'files never read here the way they read on a hosted runner.'
}
finally {
    Pop-Location
}
