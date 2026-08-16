# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Compares the command controls the application declares against the ones the autonomous walk
    actually pressed with a mouse.

.DESCRIPTION
    Two halves, and they are gathered in two different ways on purpose.

    The inventory is read from the views, because a view is where a control is declared and there is
    nowhere else to learn that it exists. The pressed half is read from what the walk wrote **while
    running**: `PressAsync` records a control only after the click beside it changed nothing, the
    real click landed, and the effect arrived. Counting `Press(...)` calls by reading the walk's
    source would count a call somebody wrote, not a control anybody reached — and three suites in
    this repository have already gone stale reading source as text.

    What is not yet pressed lives in eng/walk-pending.txt, one identity per line with the reason it
    is still there. That list may only shrink: the script refuses a list longer than the ratchet
    below, and refuses any difference between the list and what the run measured, in either
    direction. An entry that has been covered has to leave the file.
#>
[CmdletBinding()]
param(
    # Reads the report a previous run left instead of running the walk again.
    [switch]$SkipRun,

    [string]$Output = 'artifacts/walk'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
$pendingFile = Join-Path $PSScriptRoot 'walk-pending.txt'

# Lowered by each batch of the walk and never raised. 126 before the first batch, 113 after the
# library and the film card, 106 after the player's transport, 98 after the metadata editor, 95
# once the rename could rename and its three controls stopped being blocked.
$maximumPending = 95

function Get-CommandControlInventory {
    param([string]$SourceRoot)

    # ToggleSwitch is here for the shape the redesign may bring, not because one exists today.
    $elements = 'Button', 'CheckBox', 'ComboBox', 'Slider', 'ToggleButton', 'RadioButton', 'ToggleSwitch'
    # The (?![\w.]) is not decoration: without it <ComboBoxItem> counts as <ComboBox> and the total
    # reads 142 instead of 129.
    $pattern = '<(?<el>' + ($elements -join '|') + ')(?![\w.])(?<attrs>[^>]*?)/?>'
    $inventory = [ordered]@{}

    foreach ($file in Get-ChildItem -LiteralPath $SourceRoot -Recurse -Filter *.axaml) {
        $view = [IO.Path]::GetFileNameWithoutExtension($file.Name)
        $text = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($match in [regex]::Matches($text, $pattern, 'Singleline')) {
            $attrs = $match.Groups['attrs'].Value
            # The anchor is the resource key behind the accessible name; the two controls named by
            # their own data are recorded under the binding they are declared with.
            $anchor = $null
            if ($attrs -match 'AutomationProperties\.Name\s*=\s*"\{(?:DynamicResource|StaticResource)\s+([^}"]+)\}"') {
                $anchor = $Matches[1].Trim()
            }
            elseif ($attrs -match 'AutomationProperties\.Name\s*=\s*"(\{[^"]*\})"') {
                $anchor = $Matches[1].Trim()
            }
            elseif ($attrs -match '(?:x:)?Name\s*=\s*"([^"]+)"') {
                $anchor = $Matches[1].Trim()
            }

            if (-not $anchor) {
                throw "$($file.Name) declares a <$($match.Groups['el'].Value)> with no accessible name and no x:Name, so the walk has nothing to aim at."
            }

            $id = "$view#$anchor"
            if (-not $inventory.Contains($id)) { $inventory[$id] = 0 }
            $inventory[$id]++
        }
    }

    $inventory
}

function Read-PendingList {
    param([string]$Path)

    # A whole-line comment goes first: an identity carries a '#' of its own, so the reason is only
    # ever the '#' that follows whitespace.
    Get-Content -LiteralPath $Path |
        Where-Object { $_.Trim() -and -not $_.TrimStart().StartsWith('#') } |
        ForEach-Object { ($_ -split '\s#\s', 2)[0].Trim() }
}

Push-Location $repoRoot
try {
    if (-not $SkipRun) {
        Remove-Item -LiteralPath $outputRoot -Recurse -Force -ErrorAction SilentlyContinue
        $env:APSOLUTIONS_LOCALMEDIA_WALK_RESULTS = $outputRoot
        try {
            dotnet test (Join-Path $repoRoot 'tests/ApSolutions.LocalMedia.AccessibilityTests') `
                -c Release --no-build -m:1 `
                --settings (Join-Path $PSScriptRoot 'test.runsettings') `
                --filter 'FullyQualifiedName~AssembledPhysicalWalkTests'
            if ($LASTEXITCODE -ne 0) { throw 'The walk failed, so what it pressed cannot be trusted.' }
        }
        finally {
            Remove-Item Env:APSOLUTIONS_LOCALMEDIA_WALK_RESULTS -ErrorAction SilentlyContinue
        }
    }

    $reportPath = Join-Path $outputRoot 'pressed.json'
    if (-not (Test-Path -LiteralPath $reportPath)) {
        throw "The walk left no report at $reportPath, so nothing was pressed or nothing was recorded."
    }

    $inventory = Get-CommandControlInventory -SourceRoot (Join-Path $repoRoot 'src')
    $declared = ($inventory.Values | Measure-Object -Sum).Sum
    $identities = @($inventory.Keys)
    $pressed = @(Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json)

    $unknown = @($pressed | Where-Object { $identities -notcontains $_ })
    if ($unknown.Count -gt 0) {
        throw ("The walk pressed controls no view declares, so the inventory and the walk disagree " +
            "about what this application has: $($unknown -join ', ')")
    }

    $measuredPending = @($identities | Where-Object { $pressed -notcontains $_ } | Sort-Object)
    $declaredPending = @(Read-PendingList -Path $pendingFile | Sort-Object)

    $newlyUncovered = @($measuredPending | Where-Object { $declaredPending -notcontains $_ })
    $staleEntries = @($declaredPending | Where-Object { $measuredPending -notcontains $_ })

    Write-Output ("The walk: $declared declared command controls in $($identities.Count) identities; " +
        "$($pressed.Count) pressed, $($measuredPending.Count) pending.")

    if ($newlyUncovered.Count -gt 0) {
        throw ("These command controls are pressed by nobody and are not on the pending list. Press " +
            "them, or name them there with the reason: $($newlyUncovered -join ', ')")
    }

    if ($staleEntries.Count -gt 0) {
        throw ("These are on the pending list and are not pending: either the walk now presses them, " +
            "or no view declares them any more. Take them out of eng/walk-pending.txt — it is only " +
            "allowed to shrink: $($staleEntries -join ', ')")
    }

    if ($declaredPending.Count -gt $maximumPending) {
        throw ("The pending list holds $($declaredPending.Count) controls and the ratchet is " +
            "$maximumPending. The list only shrinks; lower the ratchet with it, never the other way.")
    }
}
finally {
    Pop-Location
}
