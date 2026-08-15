# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    What the updater's handover does on a machine that has an App Installer.

.DESCRIPTION
    The other half of the measurement, and the reason the first half is not enough. The application
    reports "Windows took it" from whether Process.Start returned something. On a clean Windows that
    is null and the refusal is right. On a machine with an App Installer it has to return a process,
    because if it returned null there while the installer was opening, the application would report a
    refusal to somebody watching an installer appear.

    Nothing is installed here and nothing can be: the package carries a throwaway certificate this
    machine does not trust. The installer window is opened, observed, and closed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath,

    [int]$WindowTimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'

$appInstaller = Get-AppxPackage -Name 'Microsoft.DesktopAppInstaller' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $appInstaller) {
    throw 'This machine has no App Installer, so it cannot answer the half it is here to answer.'
}

$before = @(Get-Process -Name 'AppInstaller' -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })

$returnedProcess = $false
$exceptionType = ''
try {
    # The same call the application makes, rather than an imitation of it.
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = (Resolve-Path -LiteralPath $PackagePath).Path
    $startInfo.UseShellExecute = $true
    $started = [System.Diagnostics.Process]::Start($startInfo)
    $returnedProcess = $null -ne $started
}
catch {
    $exceptionType = $_.Exception.GetType().FullName
}

$titles = New-Object System.Collections.ArrayList
$appeared = @()
$deadline = (Get-Date).AddSeconds($WindowTimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    $appeared = @(Get-Process -Name 'AppInstaller' -ErrorAction SilentlyContinue |
        Where-Object { $before -notcontains $_.Id })
    if ($appeared.Count -gt 0 -and @($appeared | Where-Object { $_.MainWindowTitle }).Count -gt 0) { break }
    Start-Sleep -Milliseconds 500
}

foreach ($process in $appeared) {
    $process.Refresh()
    if ($process.MainWindowTitle) { [void]$titles.Add($process.MainWindowTitle) }
}

# Closed the way a person closes it, and killed only if it will not go.
foreach ($process in $appeared) {
    try {
        [void]$process.CloseMainWindow()
        if (-not $process.WaitForExit(10000)) { $process.Kill() }
    }
    catch { }
}

$somethingOpened = $appeared.Count -gt 0

[ordered]@{
    runner = 'Developer hardware'
    os = (Get-CimInstance Win32_OperatingSystem).Caption
    appInstallerVersion = $appInstaller.Version
    returnedProcess = $returnedProcess
    exceptionType = $exceptionType
    handlerProcessesAppeared = $appeared.Count
    windowTitles = @($titles | Sort-Object -Unique)
    launcherWouldSay = if ($returnedProcess) { 'HandedToWindows' } else { 'Refused' }
    somethingOpened = $somethingOpened
    # The failure this half exists to rule out: the application saying "refused" while an installer
    # is on screen.
    misreportsARefusal = ((-not $returnedProcess) -and $somethingOpened)
}
