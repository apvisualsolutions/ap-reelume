# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Runs the end-to-end accessibility audit of the approved journey.

.DESCRIPTION
    Two modes, and the difference matters.

    `Audit` inventories every finding in one pass and always exits zero. It is how the first pass of a
    RED cycle collects the whole defect list instead of stopping at the first one.

    `Verify` is the MVP gate: any critical or major finding fails the run. It is also the mode that
    must pass twice in a row before the journey can be called clean.

    Neither mode can suppress a check or lower a severity; both write the same JSON and Markdown
    findings under the results directory.
#>
[CmdletBinding()]
param(
    [ValidateSet('Audit', 'Verify')]
    [string]$Mode = 'Verify',

    [ValidateRange(1, 5)]
    [int]$Passes = 1,

    [string]$Output = 'artifacts/accessibility',

    # Launches the real x64 shell and records the UIA tree a screen reader would read from it, plus
    # the accessibility preferences Windows is actually reporting. Headless automation proves the
    # tree the application builds; this proves the tree Windows publishes.
    [switch]$RealApp
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests/ApSolutions.LocalMedia.AccessibilityTests'
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $Output))

function Get-WindowsAccessibilityPreferences {
    Add-Type -Namespace ApReelume -Name Spi -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
public static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint update);
'@ -ErrorAction SilentlyContinue

    $clientAreaAnimation = 1
    [void][ApReelume.Spi]::SystemParametersInfo(0x1042, 0, [ref]$clientAreaAnimation, 0)
    $highContrastFlags = 0
    try {
        $highContrastFlags = [int](Get-ItemPropertyValue 'HKCU:\Control Panel\Accessibility\HighContrast' 'Flags')
    }
    catch { $highContrastFlags = 0 }
    $textScale = 100
    try {
        $textScale = [int](Get-ItemPropertyValue 'HKCU:\Software\Microsoft\Accessibility' 'TextScaleFactor')
    }
    catch { $textScale = 100 }

    [pscustomobject]@{
        ClientAreaAnimationEnabled = [bool]$clientAreaAnimation
        HighContrastOn = ($highContrastFlags -band 1) -eq 1
        TextScaleFactor = $textScale
        NarratorInstalled = Test-Path (Join-Path $env:WINDIR 'System32\Narrator.exe')
    }
}

function Export-RealApplicationTree {
    param([string]$Destination)

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $binaries = Join-Path $repoRoot 'tests/ApSolutions.LocalMedia.AccessibilityTests/bin/Release/net10.0-windows10.0.22621.0'
    Add-Type -Path (Join-Path $binaries 'FlaUI.Core.dll')
    Add-Type -Path (Join-Path $binaries 'FlaUI.UIA3.dll')

    $exe = Join-Path $repoRoot 'src/ApSolutions.LocalMedia.Windows/bin/Release/net10.0-windows10.0.22621.0/ApSolutions.LocalMedia.Windows.exe'
    if (-not (Test-Path $exe)) { throw "The release shell is not built: $exe" }

    $application = [FlaUI.Core.Application]::Launch($exe)
    try {
        $automation = [FlaUI.UIA3.UIA3Automation]::new()
        try {
            $window = $application.GetMainWindow($automation, [TimeSpan]::FromSeconds(30))
            if ($null -eq $window) { throw 'The shell never presented a main window.' }

            $rows = [System.Collections.Generic.List[string]]::new()
            $rows.Add('Depth|ControlType|Name|AutomationId|Enabled|KeyboardFocusable|Patterns')
            $stack = [System.Collections.Generic.Stack[object]]::new()
            $stack.Push([pscustomobject]@{ Element = $window; Depth = 0 })
            while ($stack.Count -gt 0) {
                $current = $stack.Pop()
                $element = $current.Element
                $patterns = ($element.Patterns.GetType().GetProperties() |
                    Where-Object { $_.Name -notin @('Item', 'BasicAutomationElement') } |
                    ForEach-Object {
                        $candidate = $null
                        try { $candidate = $element.Patterns.$($_.Name) } catch { $candidate = $null }
                        if ($null -ne $candidate -and $candidate.IsSupported.Value) { $_.Name }
                    }) -join ','
                $rows.Add(('{0}|{1}|{2}|{3}|{4}|{5}|{6}' -f
                    $current.Depth,
                    $element.ControlType,
                    $element.Name,
                    $element.AutomationId,
                    $element.IsEnabled,
                    $element.Properties.IsKeyboardFocusable.ValueOrDefault,
                    $patterns))
                foreach ($child in $element.FindAllChildren()) {
                    $stack.Push([pscustomobject]@{ Element = $child; Depth = $current.Depth + 1 })
                }
            }

            Set-Content -LiteralPath (Join-Path $Destination 'real-uia-tree.txt') -Value $rows -Encoding utf8

            # The tab walk only means anything while our own window owns the foreground. Anything
            # focused outside this process is recorded as such and never by name: it belongs to
            # whatever else the desktop happens to be running.
            # A synthetic Tab walk needs the foreground, and Windows refuses to hand it to a
            # background process. What UIA does allow is asking each control for focus the way a
            # screen reader does, so that is what gets recorded: every keyboard-focusable control of
            # the running shell, in tree order, with whether it actually took focus when asked.
            $focusOrder = [System.Collections.Generic.List[string]]::new()
            $position = 0
            foreach ($candidate in $window.FindAllDescendants()) {
                if (-not $candidate.Properties.IsKeyboardFocusable.ValueOrDefault) { continue }
                $position++
                $took = $false
                try {
                    $candidate.Focus()
                    Start-Sleep -Milliseconds 80
                    $took = $candidate.Properties.HasKeyboardFocus.ValueOrDefault
                }
                catch { $took = $false }
                $focusOrder.Add(('{0}. {1} [{2}] tookFocus={3}' -f
                    $position, $candidate.Name, $candidate.ControlType, $took))
            }

            Set-Content -LiteralPath (Join-Path $Destination 'real-focus-order.txt') `
                -Value $focusOrder -Encoding utf8
            Write-Output "Captured $($rows.Count - 1) UIA elements and $($focusOrder.Count) focusable controls."
        }
        finally {
            $automation.Dispose()
        }
    }
    finally {
        if (-not $application.HasExited) { $application.Close() | Out-Null }
        Start-Sleep -Seconds 1
        if (-not $application.HasExited) { $application.Kill() }
    }
}

Push-Location $repoRoot
try {
    if ($RealApp) {
        $realRoot = Join-Path $outputRoot 'real-app'
        New-Item -ItemType Directory -Force -Path $realRoot | Out-Null
        Get-WindowsAccessibilityPreferences |
            ConvertTo-Json |
            Set-Content -LiteralPath (Join-Path $realRoot 'windows-preferences.json') -Encoding utf8
        Export-RealApplicationTree -Destination $realRoot
        return
    }

    $failedPasses = [System.Collections.Generic.List[int]]::new()
    for ($pass = 1; $pass -le $Passes; $pass++) {
        $passRoot = Join-Path $outputRoot "$($Mode.ToLowerInvariant())/pass-$pass"
        New-Item -ItemType Directory -Force -Path $passRoot | Out-Null
        $env:APSOLUTIONS_LOCALMEDIA_A11Y_MODE = $Mode
        $env:APSOLUTIONS_LOCALMEDIA_A11Y_RESULTS = $passRoot

        dotnet test $project -c Release --no-build -m:1 `
            --settings (Join-Path $PSScriptRoot 'test.runsettings') `
            --logger "trx;LogFileName=accessibility-pass-$pass.trx" `
            --results-directory $passRoot
        if ($LASTEXITCODE -ne 0) {
            $failedPasses.Add($pass)
        }
    }

    $findings = Get-ChildItem -LiteralPath $outputRoot -Recurse -Filter '*.json' |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json } |
        Where-Object { $null -ne $_ }
    $critical = @($findings | Where-Object { $_.Severity -eq 'Critical' -or $_.Severity -eq 2 }).Count
    $major = @($findings | Where-Object { $_.Severity -eq 'Major' -or $_.Severity -eq 1 }).Count
    $minor = @($findings | Where-Object { $_.Severity -eq 'Minor' -or $_.Severity -eq 0 }).Count

    Write-Output "Accessibility $Mode over $Passes pass(es): $critical critical, $major major, $minor minor."

    if ($Mode -eq 'Verify' -and $failedPasses.Count -gt 0) {
        throw "The accessibility gate failed on pass(es): $($failedPasses -join ', ')."
    }
}
finally {
    Remove-Item Env:APSOLUTIONS_LOCALMEDIA_A11Y_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:APSOLUTIONS_LOCALMEDIA_A11Y_RESULTS -ErrorAction SilentlyContinue
    Pop-Location
}
