# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Lists everything the scope matrix does not call VERIFIED, grouped by the release it belongs to.

.DESCRIPTION
    docs/FEATURES.md is the canonical record of scope, but it is written to be audited rather than
    planned against: 65 rows in six tables, both languages inside every cell, and what is done mixed
    in with what is not. Answering "what is left before the first release" meant reading all of it,
    and reading all of it by hand is how rows get missed.

    This reads the matrix and prints only what is outstanding. It is deliberately not a second
    record: it owns no list of its own and holds no state, so it cannot drift from the matrix the way
    a hand-kept backlog does. Every fact it prints comes from the row it prints it for.

    THE POINT IS THAT IT CANNOT GO QUIET. On 2026-08-31 this same list was produced by hand with a
    pattern that asked for three capitals, and `UX` has two: eight rows -- including the one that was
    actually being asked about -- vanished with no error at all. A tool that skips input in silence
    is worse than no tool, because its answer looks complete. So:

      * the closed sets of statuses and targets are read from the matrix's own two legend tables,
        not written here, and a value outside them is an error rather than a row quietly dropped;
      * every row is counted twice, once while parsing the feature tables and once by scanning the
        whole file for anything that looks like a feature row, and the two counts must agree;
      * a row whose identifier, target or status does not parse is named with its line number.

    Any of those failing exits non-zero. There is no arrangement of the matrix that makes this print
    a short list and call it a day.

.EXAMPLE
    pwsh ./eng/list-pending.ps1
    pwsh ./eng/list-pending.ps1 -Json     # same answer, for something else to read
    pwsh ./eng/list-pending.ps1 -Target MVP
#>
[CmdletBinding()]
param(
    # Only this release. Anything the matrix declares -- MVP, STABLE, POST_STABLE.
    [string]$Target,

    # Emit the rows as JSON instead of a report.
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$matrixPath = Join-Path $repoRoot 'docs/FEATURES.md'

if (-not (Test-Path -LiteralPath $matrixPath)) {
    Write-Error "The scope matrix is missing: $matrixPath"
    exit 1
}

$lines = @(Get-Content -LiteralPath $matrixPath)

# ------------------------------------------------------------------ the legends
# The matrix declares its own statuses and releases in two tables near the top. Reading them from
# there rather than repeating them here is what keeps this from disagreeing with the document it
# describes: a status added to the legend works the day it is added, and one used without being
# declared is an error in the matrix that this reports rather than absorbs.
function Read-Legend {
    param([string[]]$Body, [string]$Heading)

    $values = [System.Collections.Generic.List[string]]::new()
    $inside = $false
    foreach ($line in $Body) {
        if ($line -match '^##\s') {
            if ($inside) { break }
            $inside = $line -match [regex]::Escape($Heading)
            continue
        }

        if ($inside -and $line -match '^\|\s*`(?<value>[^`]+)`\s*\|') {
            [void]$values.Add($Matches['value'])
        }
    }

    return $values
}

$statuses = Read-Legend -Body $lines -Heading 'Estados / Statuses'
$targets = Read-Legend -Body $lines -Heading 'Versiones / Releases'

if ($statuses.Count -eq 0 -or $targets.Count -eq 0) {
    Write-Error 'The matrix legends could not be read, so no row could be validated against them.'
    exit 1
}

# ------------------------------------------------------------------ the rows
# A feature table is one whose header starts with the ID column. Everything until the next heading
# belongs to it, minus the header and its separator.
$rows = [System.Collections.Generic.List[psobject]]::new()
$problems = [System.Collections.Generic.List[string]]::new()
$inTable = $false

for ($index = 0; $index -lt $lines.Count; $index++) {
    $line = $lines[$index]

    if ($line -match '^##\s') { $inTable = $false; continue }
    if ($line -match '^\|\s*ID\s*\|') { $inTable = $true; continue }
    if (-not $inTable) { continue }
    if ($line -match '^\|[-:\s|]+\|$') { continue }
    if ($line -notmatch '^\|') { $inTable = $false; continue }

    $cells = @($line -split '\|' | Select-Object -Skip 1)
    if ($cells.Count -lt 6) {
        $problems.Add("line $($index + 1): a feature row has $($cells.Count) columns, expected at least 6.")
        continue
    }

    # rowTarget and not target: PowerShell variable names are case-insensitive, so a local $target
    # IS the $Target parameter. Writing one per row left the parameter holding the last row's
    # release, and the "only this release" filter below then ran when nobody asked for it -- the
    # list came out a third of its true length with no error at all. Exactly the failure this file
    # was written to prevent, committed by the file itself.
    $id = $cells[0].Trim()
    $rowTarget = $cells[2].Trim()
    $status = $cells[3].Trim()

    # Two letters or four, digits in the prefix or not -- `UX-007` and `A11Y-001` are both real, and
    # both were dropped in silence by hand-written patterns on the day this was written. What matters
    # is that a row this does not recognise stops the run instead of leaving the list one entry
    # shorter than the truth.
    if ($id -notmatch '^[A-Z][A-Z0-9]{1,4}-[0-9]{3}$') {
        $problems.Add("line $($index + 1): '$id' is not a feature identifier.")
        continue
    }

    if ($status -notin $statuses) {
        $problems.Add("line $($index + 1): $id has status '$status', which the matrix does not declare.")
    }

    if ($rowTarget -notin $targets) {
        $problems.Add("line $($index + 1): $id targets '$rowTarget', which the matrix does not declare as a release.")
    }

    $rows.Add([pscustomobject]@{
        Id      = $id
        Feature = ($cells[1] -split '/')[0].Trim()
        Target  = $rowTarget
        Status  = $status
        Line    = $index + 1
    })
}

# ------------------------------------------------------------------ the second count
# Parsed one way above; counted another way here. A feature row is a table row whose first cell is
# a bare identifier, wherever it sits. If the two disagree, a table was missed entirely -- which is
# the failure this whole script exists to make impossible -- so it says which rows and stops.
$scanned = @($lines |
    Where-Object { $_ -match '^\|\s*[A-Z][A-Z0-9]{1,4}-[0-9]{3}\s*\|' } |
    ForEach-Object { ($_ -split '\|')[1].Trim() })

$missed = @($scanned | Where-Object { $_ -notin $rows.Id })
if ($missed.Count -gt 0) {
    $problems.Add("$($missed.Count) row(s) look like features but were not parsed: $($missed -join ', ').")
}

if ($problems.Count -gt 0) {
    Write-Output 'The scope matrix could not be read cleanly, so this list would be incomplete:'
    foreach ($problem in $problems) { Write-Output "  - $problem" }
    Write-Error 'Refusing to print a partial list of what is pending.'
    exit 1
}

$pending = @($rows | Where-Object { $_.Status -ne 'VERIFIED' })
if ($Target) {
    if ($Target -notin $targets) {
        Write-Error "Unknown release '$Target'. The matrix declares: $($targets -join ', ')."
        exit 1
    }

    $pending = @($pending | Where-Object { $_.Target -eq $Target })
}

if ($Json) {
    $pending | ConvertTo-Json -Depth 3
    exit 0
}

Write-Output "AP Reelume - what is still open, from $($rows.Count) rows of docs/FEATURES.md"
Write-Output ''

# Releases in the order the matrix declares them, so the first release reads first. A target used by
# a row but absent from the legend has already stopped the run above, so nothing can fall off here.
foreach ($release in $targets) {
    $group = @($pending | Where-Object { $_.Target -eq $release })
    if ($group.Count -eq 0) { continue }

    $done = @($rows | Where-Object { $_.Target -eq $release -and $_.Status -eq 'VERIFIED' }).Count
    Write-Output "$release - $($group.Count) open, $done verified"
    $group |
        Sort-Object Status, Id |
        Format-Table -AutoSize -Property Id, Status, @{ Name = 'Feature'; Expression = { $_.Feature } } |
        Out-String -Width 200 |
        Write-Output
}

# OUT_OF_SCOPE and DEFERRED are answers rather than work, and saying so is the point: a list that
# prints them beside the real tasks invites somebody to start one.
$decisions = @($pending | Where-Object { $_.Status -in @('OUT_OF_SCOPE', 'DEFERRED') })
$work = @($pending | Where-Object { $_.Status -notin @('OUT_OF_SCOPE', 'DEFERRED') })
Write-Output ("Totals: {0} open of {1}. {2} are work; {3} are standing decisions ({4}) that need a new decision before they become work." -f
    $pending.Count, $rows.Count, $work.Count, $decisions.Count, (($decisions | ForEach-Object { $_.Id }) -join ', '))
exit 0
