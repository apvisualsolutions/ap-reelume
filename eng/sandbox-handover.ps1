# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Measures, inside Windows Sandbox, what Windows does with the package the updater hands it.

.DESCRIPTION
    This runs INSIDE the sandbox, not on the host. eng/run-sandbox-handover.ps1 stages the folder,
    signs a copy of the package and launches the sandbox on it.

    Three rules this file obeys, and each of them was learnt the hard way; eng/README-sandbox.md
    records them:

    - It is pure ASCII. The sandbox ships Windows PowerShell 5.1, which reads a file without a BOM as
      ANSI, and any other byte becomes a syntax error rather than a wrong character.
    - Nothing waits on anything without a deadline.
    - Every phase is wrapped and the report is written in a finally, because a run that leaves no
      trace when something goes wrong hides exactly what it was run to find.

    The handover itself is the same call the application makes - Process.Start with UseShellExecute
    on the package path, see CompositionRoot.OpenWithWindows - rather than an imitation of it. What
    is being measured is what that call returns on a machine with nothing registered for .msix, and
    an imitation would measure the imitation.
#>
[CmdletBinding()]
param(
    [string]$ShareRoot = 'C:\share',

    # The updater waits this long before it decides Windows did nothing with the package.
    [int]$HandoverWaitSeconds = 45
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$phases = New-Object System.Collections.ArrayList
$reportPath = Join-Path $ShareRoot 'handover-sandbox.json'
$tracePath = Join-Path $ShareRoot 'handover-trace.txt'
$dataRoot = Join-Path $env:LOCALAPPDATA 'APSolutions\LocalMedia'
$databasePath = Join-Path $dataRoot 'library.db'
$identityName = 'APSolutions.LocalMedia'

# The report is written in a finally, and that only helps if the run reaches the end of the try. A
# hang - Add-AppxPackage on a hundred megabytes is the candidate - leaves nothing at all, so each
# step says where it got to as it gets there.
function Write-Trace {
    param([string]$Message)
    try {
        Add-Content -LiteralPath $tracePath -Value ("{0} {1}" -f (Get-Date -Format 'HH:mm:ss'), $Message) -Encoding ASCII
    }
    catch { }
}

function Add-Phase {
    param([hashtable]$Row)
    [void]$phases.Add([pscustomobject]$Row)
    Write-Trace "phase $($Row.id) = $($Row.outcome)"
}

function Get-DatabaseBytes {
    if (Test-Path -LiteralPath $databasePath) { (Get-Item -LiteralPath $databasePath).Length } else { 0 }
}

function Get-InstalledVersion {
    $package = Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($package) { $package.Version } else { '' }
}

try {
    Write-Trace 'started'

    # Two packages are staged: this version, and one of the next. They are told apart by name rather
    # than by reading a version out of each, because a name is what the staging step controls and a
    # mix-up would install the upgrade first and measure nothing at all.
    $staged = @(Get-ChildItem -LiteralPath $ShareRoot -Filter '*.msix' -ErrorAction SilentlyContinue)
    $msix = $staged | Where-Object { $_.Name -notlike '*-next.msix' } | Select-Object -First 1
    $next = $staged | Where-Object { $_.Name -like '*-next.msix' } | Select-Object -First 1
    if (-not $msix) { throw "No package was staged in $ShareRoot." }
    Write-Trace "staged: $($msix.Name); next: $(if ($next) { $next.Name } else { 'none' })"
    $certificate = Get-ChildItem -LiteralPath $ShareRoot -Filter '*.cer' -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $certificate) { throw "No test certificate was staged in $ShareRoot." }

    # ---------------------------------------------------------- developer mode
    try {
        $unlock = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
        if (-not (Test-Path -LiteralPath $unlock)) { New-Item -Path $unlock -Force | Out-Null }
        New-ItemProperty -Path $unlock -Name 'AllowDevelopmentWithoutDevLicense' -Value 1 -PropertyType DWord -Force | Out-Null
        Add-Phase @{ id = 'developer-mode'; outcome = 'Passed'; detail = 'Enabled inside the sandbox; it dies with the window.' }
    }
    catch {
        Add-Phase @{ id = 'developer-mode'; outcome = 'Failed'; detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)" }
    }

    # ------------------------------------------------- trust the test certificate
    try {
        Import-Certificate -FilePath $certificate.FullName -CertStoreLocation 'Cert:\LocalMachine\Root' -ErrorAction Stop | Out-Null
        Add-Phase @{ id = 'trust-test-certificate'; outcome = 'Passed'; detail = 'Trusted inside the sandbox only.' }
    }
    catch {
        Add-Phase @{ id = 'trust-test-certificate'; outcome = 'Failed'; detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)" }
    }

    # --------------------------------------------------- nothing handles .msix here
    # The whole point of this half: a clean Windows has no App Installer, so the call the updater
    # makes has nothing to start. Recorded rather than assumed.
    $fileClass = ''
    try {
        $key = 'HKLM:\SOFTWARE\Classes\.msix'
        if (Test-Path -LiteralPath $key) {
            $fileClass = [string](Get-ItemProperty -LiteralPath $key -Name '(default)' -ErrorAction SilentlyContinue).'(default)'
        }
    }
    catch { $fileClass = '' }
    $appInstaller = Get-AppxPackage -Name 'Microsoft.DesktopAppInstaller' -ErrorAction SilentlyContinue
    Add-Phase @{
        id = 'app-installer-absent'
        outcome = if ($appInstaller) { 'Recorded' } else { 'Absent' }
        detail = if ($appInstaller) { "An App Installer is present: $($appInstaller.Version)" } else { 'A clean Windows Sandbox ships without one.' }
        fileClass = $fileClass
    }

    # ------------------------------------------------------------------- install
    $installedVersion = ''
    try {
        Write-Trace "installing $($msix.Name) ($([int]($msix.Length / 1MB)) MB)"
        Add-AppxPackage -Path $msix.FullName -ErrorAction Stop
        $package = Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $package) { throw 'Windows reported no error and registered nothing.' }
        $installedVersion = $package.Version
        Add-Phase @{
            id = 'windows-install'
            outcome = 'Passed'
            detail = "Windows registered $identityName $($package.Version)"
            family = $package.PackageFamilyName
            version = $installedVersion
            installLocation = $package.InstallLocation
        }
    }
    catch {
        Add-Phase @{
            id = 'windows-install'
            outcome = 'Failed'
            detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)"
            family = ''
            version = ''
            installLocation = ''
        }
    }

    # --------------------------------------------------------- file associations
    # The Open-with entry only exists once Windows has processed the manifest during an install,
    # which is why unpacking the package can neither produce it nor check it. What is asked here is
    # what the manifest declares - never a list written down twice - and where Windows put it.
    try {
        $package = Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $package) { throw 'Nothing is installed, so nothing could have registered an association.' }

        $declared = @()
        $extensions = (Get-AppxPackageManifest $package).Package.Applications.Application.Extensions.Extension
        foreach ($extension in @($extensions)) {
            foreach ($type in @($extension.FileTypeAssociation.SupportedFileTypes.FileType)) {
                $text = [string]$type
                if ($text -and $declared -notcontains $text) { $declared += $text }
            }
        }
        if ($declared.Count -eq 0) { throw 'The installed manifest declares no file type association.' }

        $registered = @()
        $locations = @()
        foreach ($extension in $declared) {
            $key = "HKCU:\SOFTWARE\Classes\$extension\OpenWithProgids"
            if (-not (Test-Path -LiteralPath $key)) { continue }
            $names = @((Get-Item -LiteralPath $key).GetValueNames() | Where-Object { $_ -like 'AppX*' })
            if ($names.Count -eq 0) { continue }
            $registered += $extension
            $locations += "$extension -> $key :: $($names -join ', ')"
        }

        Add-Phase @{
            id = 'file-association'
            outcome = if ($registered.Count -eq $declared.Count) { 'Passed' } else { 'Failed' }
            detail = "$($registered.Count) of $($declared.Count) containers registered an Open-with entry"
            registered = $registered
            locations = $locations
        }
    }
    catch {
        Add-Phase @{
            id = 'file-association'
            outcome = 'Failed'
            detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)"
            registered = @()
            locations = @()
        }
    }

    # -------------------------------------------------------------------- launch
    try {
        $package = Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $package) { throw 'Nothing is installed to launch.' }
        $application = (Get-AppxPackageManifest $package).Package.Applications.Application
        $applicationId = $application.Id
        Start-Process "shell:AppsFolder\$($package.PackageFamilyName)!$applicationId"

        $deadline = (Get-Date).AddSeconds(90)
        while ((Get-Date) -lt $deadline -and (Get-DatabaseBytes) -le 0) { Start-Sleep -Milliseconds 500 }
        Start-Sleep -Seconds 5

        $bytes = Get-DatabaseBytes
        foreach ($process in @(Get-Process -Name 'ApSolutions.LocalMedia.Windows' -ErrorAction SilentlyContinue)) {
            [void]$process.CloseMainWindow()
            if (-not $process.WaitForExit(20000)) { $process.Kill() }
        }

        # Where the library ended up matters as much as that it exists. A packaged application whose
        # writes are virtualised keeps its database inside the package's LocalCache, and somebody
        # looking for their library at the documented path would not find it - so both places are
        # asked, and the answer is only right if it is the documented one.
        $virtualisedRoot = Join-Path $env:LOCALAPPDATA "Packages\$($package.PackageFamilyName)\LocalCache\Local\APSolutions\LocalMedia"
        $virtualisedDatabase = Join-Path $virtualisedRoot 'library.db'
        $atVirtualised = Test-Path -LiteralPath $virtualisedDatabase

        Add-Phase @{
            id = 'windows-launch'
            outcome = if ($bytes -gt 0 -and -not $atVirtualised) { 'Passed' } else { 'Failed' }
            detail = "Window shown: $($bytes -gt 0); database at the documented path: $($bytes -gt 0); virtualised copy: $atVirtualised"
            databaseBytes = [int64]$bytes
            documentedPath = $dataRoot
            databaseAtDocumented = ($bytes -gt 0)
            virtualisedPath = $virtualisedRoot
            databaseAtVirtualised = $atVirtualised
        }
    }
    catch {
        Add-Phase @{
            id = 'windows-launch'
            outcome = 'Failed'
            detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)"
            databaseBytes = [int64](Get-DatabaseBytes)
            documentedPath = $dataRoot
            databaseAtDocumented = ((Get-DatabaseBytes) -gt 0)
            virtualisedPath = ''
            databaseAtVirtualised = $false
        }
    }

    # ------------------------------------------------------------------ handover
    # The call itself, as the application makes it. A null process is the refusal; an exception is the
    # other outcome the launcher has to survive, and both are recorded rather than one of them assumed.
    $before = Get-DatabaseBytes
    $startedAHandler = $false
    $exceptionType = ''
    $exceptionMessage = ''
    $caughtByLauncher = $true
    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $msix.FullName
        $startInfo.UseShellExecute = $true
        $started = [System.Diagnostics.Process]::Start($startInfo)
        $startedAHandler = $null -ne $started
    }
    catch [System.ComponentModel.Win32Exception] {
        $exceptionType = $_.Exception.GetType().FullName
        $exceptionMessage = $_.Exception.Message
    }
    catch {
        # Anything the shipped launcher does not catch would escape into the update surface.
        $exceptionType = $_.Exception.GetType().FullName
        $exceptionMessage = $_.Exception.Message
        $caughtByLauncher = $false
    }

    Start-Sleep -Seconds $HandoverWaitSeconds
    $after = Get-DatabaseBytes
    $versionAfter = Get-InstalledVersion
    $installedWithoutHelp = ($installedVersion -ne '') -and ($versionAfter -ne '') -and ($versionAfter -ne $installedVersion)

    Add-Phase @{
        id = 'updater-handover'
        outcome = if ($startedAHandler) { 'HandedToWindows' } else { 'Refused' }
        detail = "Windows started a handler: $startedAHandler. Version after $($HandoverWaitSeconds)s: $versionAfter. Exception: $(if ($exceptionType) { $exceptionType } else { 'none' })"
        startedAHandler = $startedAHandler
        versionAfter = $versionAfter
        exceptionType = $exceptionType
        exceptionMessage = $exceptionMessage
        databaseBytesBefore = [int64]$before
        databaseBytesAfter = [int64]$after
        databaseSurvived = ((Test-Path -LiteralPath $databasePath) -and $after -gt 0)
        installedWithoutHelp = $installedWithoutHelp
        caughtByLauncher = $caughtByLauncher
    }

    # ---------------------------------------------------------------- upgrade
    # The promise the release makes: a newer package replaces the older one and takes nothing with
    # it. The database is measured on both sides of the install rather than merely looked for,
    # because a library that is replaced by an empty one of the same name still exists.
    try {
        if (-not $next) { throw 'No package of the next version was staged, so an upgrade could not be attempted.' }
        $databaseBefore = Get-DatabaseBytes
        Write-Trace "upgrading with $($next.Name)"
        Add-AppxPackage -Path $next.FullName -ForceApplicationShutdown -ErrorAction Stop
        $upgraded = Get-InstalledVersion
        if ($upgraded -eq $installedVersion) { throw "Windows reported no error and kept $installedVersion." }
        $databaseAfter = Get-DatabaseBytes

        Add-Phase @{
            id = 'windows-upgrade'
            outcome = if ($databaseAfter -gt 0 -and $databaseAfter -eq $databaseBefore) { 'Passed' } else { 'Failed' }
            detail = "$installedVersion replaced by $upgraded; the database survived at $databaseAfter bytes, was $databaseBefore"
            version = $upgraded
            databaseBefore = [int64]$databaseBefore
            databaseAfter = [int64]$databaseAfter
        }
    }
    catch {
        Add-Phase @{
            id = 'windows-upgrade'
            outcome = 'Failed'
            detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)"
            version = (Get-InstalledVersion)
            databaseBefore = [int64]0
            databaseAfter = [int64](Get-DatabaseBytes)
        }
    }

    # ------------------------------------------------------- downgrade refused
    # An older package must not be able to replace a newer one, and the refusal is Windows' own. The
    # message is recorded rather than summarised: a refusal that stops being 0x80073D06 one day is a
    # different refusal, and the report should say so instead of reading the same either way.
    try {
        $before = Get-InstalledVersion
        $refusal = ''
        try {
            Add-AppxPackage -Path $msix.FullName -ErrorAction Stop
        }
        catch { $refusal = $_.Exception.Message }
        $after = Get-InstalledVersion

        Add-Phase @{
            id = 'windows-downgrade-refused'
            outcome = if ($refusal -and $after -eq $before) { 'Passed' } else { 'Failed' }
            detail = if ($refusal) { "Windows refused the older package and kept $after" } else { "Windows accepted the older package: $before became $after" }
            version = $after
            refusal = $refusal
        }
    }
    catch {
        Add-Phase @{
            id = 'windows-downgrade-refused'
            outcome = 'Failed'
            detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)"
            version = (Get-InstalledVersion)
            refusal = ''
        }
    }

    # ----------------------------------------------------------------- repair
    # Re-registering from the manifest Windows itself holds is what a repair is: the package store
    # is the source, so nothing staged here can stand in for it and a repair that reinstalled from
    # the share would be measuring the share.
    try {
        $package = Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $package) { throw 'Nothing is installed to repair.' }
        $manifestPath = Join-Path $package.InstallLocation 'AppxManifest.xml'
        if (-not (Test-Path -LiteralPath $manifestPath)) { throw "The installed package has no manifest at $manifestPath." }

        Add-AppxPackage -Register $manifestPath -DisableDevelopmentMode -ErrorAction Stop
        $repaired = Get-InstalledVersion
        Add-Phase @{
            id = 'windows-repair'
            outcome = if ($repaired) { 'Passed' } else { 'Failed' }
            detail = "Re-registered $repaired from the package Windows holds"
            version = $repaired
        }
    }
    catch {
        Add-Phase @{
            id = 'windows-repair'
            outcome = 'Failed'
            detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)"
            version = (Get-InstalledVersion)
        }
    }

    # -------------------------------------------------------------- uninstall
    # Leaving without taking anyone's library along. The data folder is asked about before and
    # after, because "the folder is there" says nothing unless it was there to begin with.
    try {
        $dataBefore = Test-Path -LiteralPath $dataRoot
        $package = Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $package) { throw 'Nothing is installed to remove.' }

        Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
        $gone = -not (Get-AppxPackage -Name $identityName -ErrorAction SilentlyContinue)
        $dataAfter = Test-Path -LiteralPath $dataRoot

        Add-Phase @{
            id = 'windows-uninstall'
            outcome = if ($gone -and $dataBefore -and $dataAfter) { 'Passed' } else { 'Failed' }
            detail = "Package gone: $gone; library survived: $dataAfter, existed before: $dataBefore"
            packageGone = $gone
            dataBefore = $dataBefore
            dataAfter = $dataAfter
        }
    }
    catch {
        Add-Phase @{
            id = 'windows-uninstall'
            outcome = 'Failed'
            detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)"
            packageGone = $false
            dataBefore = $false
            dataAfter = (Test-Path -LiteralPath $dataRoot)
        }
    }

    # ------------------------------------------------------------ what is on screen
    try {
        $titles = @(Get-Process | Where-Object { $_.MainWindowTitle } | ForEach-Object { $_.MainWindowTitle } | Sort-Object -Unique)
        Add-Phase @{ id = 'visible-windows'; outcome = 'Recorded'; detail = ($titles -join ', ') }
    }
    catch {
        Add-Phase @{ id = 'visible-windows'; outcome = 'Recorded'; detail = '' }
    }
}
catch {
    Add-Phase @{ id = 'harness'; outcome = 'Failed'; detail = "$($_.Exception.GetType().Name): $($_.Exception.Message)" }
}
finally {
    $operatingSystem = Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue
    $report = [ordered]@{
        runner = 'Windows Sandbox'
        os = if ($operatingSystem) { $operatingSystem.Caption } else { '' }
        build = if ($operatingSystem) { $operatingSystem.Version } else { '' }
        phases = @($phases)
    }
    # The script has to be ASCII; its report does not, and writing it as ASCII is how the window
    # title "Elegir una aplicacion" arrived with a question mark in it on 2026-08-15. Evidence that
    # mangles what it recorded is evidence about the harness.
    Set-Content -LiteralPath $reportPath -Value ($report | ConvertTo-Json -Depth 6) -Encoding UTF8
}
