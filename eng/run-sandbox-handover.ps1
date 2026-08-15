# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Runs the updater's handover measurement: the sandbox half here, the App Installer half beside it,
    and archives the two together.

.DESCRIPTION
    Until now this measurement existed only as prose. eng/README-sandbox.md described the steps and
    the script that performed them lived outside the repository, so re-running it after a manifest
    change depended on a file nothing versioned. It is versioned now, which is the point: the report
    expires with the manifest by design, so producing it again has to be something the repository can
    do rather than something somebody remembers.

    What happens on this machine, and nothing more:

    - A code-signing certificate is created in the CURRENT USER's personal store, used to sign a
      COPY of the package, and removed again at the end. Nothing on this machine is made to trust it:
      the trust is granted inside the sandbox, which is created when it opens and destroyed when it
      closes.
    - The published artifact stays unsigned. Only the copy under the staging folder is signed.
    - The App Installer half opens the installer window and closes it. It installs nothing, and it
      cannot: the certificate is one this machine does not trust.
#>
[CmdletBinding()]
param(
    [string]$PackageRoot = 'artifacts/package',

    [string]$Stage = 'artifacts/sandbox',

    # How long to wait for the sandbox to leave its report behind. A sandbox that never writes one is
    # a failure that says so, not a wait without end.
    [int]$SandboxTimeoutSeconds = 900,

    # Runs only the half this machine can do without the sandbox.
    [switch]$WithHandlerOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = [IO.Path]::GetFullPath($PackageRoot, $repoRoot)
$stageRoot = [IO.Path]::GetFullPath($Stage, $repoRoot)
$subject = 'CN=AP Solutions Test Publisher, O=AP Solutions'

$msix = Get-ChildItem -LiteralPath $packageRoot -Filter '*.msix' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $msix) { throw "No package in $packageRoot. Run eng/package-x64.ps1 first." }

[xml]$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest') -Raw
if ($manifest.Package.Identity.Publisher -ne $subject) {
    throw "The manifest's publisher is $($manifest.Package.Identity.Publisher); the test certificate is issued to $subject and Windows refuses a package whose signer does not match."
}

$manifestSha = (Get-FileHash -LiteralPath (Join-Path $repoRoot 'src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest') -Algorithm SHA256).Hash.ToLowerInvariant()
$version = ([xml](Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$version = ([string]$version).Trim()

if (Test-Path -LiteralPath $stageRoot) { Remove-Item -LiteralPath $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null

$certificate = $null
try {
    Write-Output 'Creating the throwaway signing certificate ...'
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -KeyUsage DigitalSignature `
        -FriendlyName 'AP Reelume sandbox cycle (throwaway)' `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}Subject Type:End Entity')

    $cerPath = Join-Path $stageRoot 'sandbox-test.cer'
    Export-Certificate -Cert $certificate -FilePath $cerPath -Type CERT | Out-Null

    $signedMsix = Join-Path $stageRoot $msix.Name
    Copy-Item -LiteralPath $msix.FullName -Destination $signedMsix -Force

    $signTool = & (Join-Path $PSScriptRoot 'find-sdk-tool.ps1') -Name 'signtool.exe'
    Write-Output 'Signing the copy ...'
    & $signTool sign /fd SHA256 /sha1 $certificate.Thumbprint $signedMsix | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'signtool refused to sign the staged copy.' }

    # --------------------------------------------------- the machine with a handler
    Write-Output 'Measuring the half this machine answers: what an App Installer does with it ...'
    $withHandler = & (Join-Path $PSScriptRoot 'measure-handover-with-handler.ps1') -PackagePath $signedMsix

    $withoutHandler = $null
    if (-not $WithHandlerOnly) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'sandbox-handover.ps1') -Destination $stageRoot -Force

        # The logon command is a staged .cmd rather than an inline PowerShell one-liner, and that is
        # the whole lesson of 2026-08-15. Inline, it has to survive being XML text and being parsed
        # as a command line at once: PowerShell's call operator `&` alone makes the configuration
        # malformed, and all Windows Sandbox says to that is "the configuration file is not valid",
        # naming no character, no line and no element. A file path carries no quotes, no ampersand
        # and nothing to escape.
        #
        # The .cmd waits for the mapping before it reads anything, because the logon command can run
        # before the folder is there and a script that reads too early reads nothing.
        $startCmd = Join-Path $stageRoot 'start.cmd'
        Set-Content -LiteralPath $startCmd -Encoding ASCII -Value @'
@echo off
setlocal
set TRIES=0
:wait
if exist C:\share\sandbox-handover.ps1 goto run
set /a TRIES+=1
if %TRIES% GEQ 150 goto run
timeout /t 2 /nobreak >nul
goto wait
:run
powershell -ExecutionPolicy Bypass -NoProfile -File C:\share\sandbox-handover.ps1
'@

        $wsbPath = Join-Path $stageRoot 'handover.wsb'
        $command = 'C:\share\start.cmd'
        $wsb = @"
<Configuration>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$([Security.SecurityElement]::Escape($stageRoot))</HostFolder>
      <SandboxFolder>C:\share</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>$([Security.SecurityElement]::Escape($command))</Command>
  </LogonCommand>
</Configuration>
"@
        # Read back as XML before handing it over: a malformed configuration has to fail here, where
        # the failure can say what is wrong, and not on a dialog that cannot.
        try { [void][xml]$wsb }
        catch { throw "The sandbox configuration this script built is not valid XML: $($_.Exception.Message)" }
        Set-Content -LiteralPath $wsbPath -Value $wsb -Encoding utf8NoBOM

        Write-Output 'Launching Windows Sandbox. Closing it is what destroys it, so that happens here.'
        Start-Process -FilePath (Join-Path $env:WINDIR 'System32\WindowsSandbox.exe') -ArgumentList "`"$wsbPath`""

        $reportPath = Join-Path $stageRoot 'handover-sandbox.json'
        $deadline = (Get-Date).AddSeconds($SandboxTimeoutSeconds)
        try {
            while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $reportPath)) {
                Start-Sleep -Seconds 5
            }
        }
        finally {
            # The sandbox is destroyed when its window closes, so it is closed whatever happened -
            # including a timeout, which must not leave a virtual machine open on somebody's desktop.
            #
            # Only the window, and the window belongs to WindowsSandboxRemoteSession - measured, not
            # assumed: there is no WindowsSandboxClient process on this build, so naming that one
            # would have closed nothing at all.
            #
            # WindowsSandboxServer and vmmemWindowsSandbox belong to the host. Killing the server is
            # what this script did on 2026-08-15, and the next sandbox started and then ended its own
            # session with "the remote environment is ending the session" - a message that names
            # neither the cause nor the run before it. Closing a sandbox means closing its window.
            Start-Sleep -Seconds 5
            Get-Process -Name 'WindowsSandboxRemoteSession' -ErrorAction SilentlyContinue |
                ForEach-Object {
                    [void]$_.CloseMainWindow()
                    if (-not $_.WaitForExit(30000)) { $_.Kill() }
                }
        }

        if (-not (Test-Path -LiteralPath $reportPath)) {
            throw "The sandbox left no report at $reportPath within $SandboxTimeoutSeconds seconds."
        }

        # The file appears before it is finished being written, so the read retries rather than
        # parsing half a document.
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            try {
                $withoutHandler = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
                break
            }
            catch { Start-Sleep -Seconds 2 }
        }
        if ($null -eq $withoutHandler) { throw 'The sandbox report never became readable JSON.' }
    }

    $result = [ordered]@{
        version = $version
        manifestSha256 = $manifestSha
        note = 'The handover the updater performs, measured on both kinds of machine: one with nothing registered for .msix and one with the App Installer. Nothing was installed on the second; the package carries a certificate it does not trust. The sandbox half ran inside Windows Sandbox, created at launch and destroyed when the window closed.'
        withoutHandler = $withoutHandler
        withHandler = $withHandler
    }

    $outputPath = Join-Path $stageRoot 'updater-handover.json'
    Set-Content -LiteralPath $outputPath -Value ($result | ConvertTo-Json -Depth 8) -Encoding utf8NoBOM
    Write-Output ''
    Write-Output "Report: $outputPath"
    Write-Output "Archive it at docs/evidence/stable/updater-handover.json once you have read it."
}
finally {
    if ($certificate) {
        Remove-Item -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force -ErrorAction SilentlyContinue
        Write-Output 'The throwaway certificate has been removed from this machine.'
    }
}
