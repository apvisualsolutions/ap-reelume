# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions

<#
.SYNOPSIS
    Downloads VideoLAN.LibVLC.Windows — the reference every ENG-013 check measures itself against —
    and accepts it only if its bytes are the ones pinned here.

.DESCRIPTION
    The first version compared the download with the `contentHash` in packages.lock.json and failed
    on the right package. Measured on 2026-09-18: nuget.org, the local NuGet cache and its
    .nupkg.sha512 all agree byte for byte, and the lock file does not, because for a SIGNED package
    NuGet's content hash leaves the signature file out. So the pin is the SHA-512 of the .nupkg as
    served, which is what the cache records next to it; and the lock file is still read, for the
    version, so the day the application moves to another LibVLC this refuses to compare against
    the old one instead of doing it quietly.

.PARAMETER Destination
    The folder to expand the package into; the trees are under build/x64 and build/arm64.
#>
[CmdletBinding()]
param([Parameter(Mandatory)] [string] $Destination)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pinnedVersion = '3.0.23.1'
$pinnedSha512 = 'Ji3p6cv6bn9FFgXS+M35nzWEvjzpZ5Kig/AvMl5iRAGv4z0QbeMDX+Knuoh2hStsVLb8AATig+LX2HLVey50/A=='

$lockPath = Join-Path $PSScriptRoot '../../src/ApSolutions.LocalMedia.Infrastructure/packages.lock.json'
$lock = Get-Content $lockPath -Raw | ConvertFrom-Json
$locked = @($lock.dependencies.PSObject.Properties.Value | ForEach-Object { $_.'VideoLAN.LibVLC.Windows' } | Where-Object { $_ })
if ($locked.Count -eq 0) { throw "packages.lock.json does not reference VideoLAN.LibVLC.Windows." }
foreach ($entry in $locked) {
    if ($entry.resolved -ne $pinnedVersion) {
        throw "The application now uses VideoLAN.LibVLC.Windows $($entry.resolved) and this reference is pinned to ${pinnedVersion}: re-pin it, and rebuild against the matching VLC tag."
    }
}

New-Item -ItemType Directory -Force $Destination | Out-Null
$package = Join-Path $Destination 'reference.nupkg'
Invoke-WebRequest "https://www.nuget.org/api/v2/package/VideoLAN.LibVLC.Windows/$pinnedVersion" -OutFile $package
$actual = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package)))
if ($actual -ne $pinnedSha512) { throw "VideoLAN.LibVLC.Windows $pinnedVersion from nuget.org is not the pinned package: $actual" }
Expand-Archive $package $Destination -Force
"reference: VideoLAN.LibVLC.Windows $pinnedVersion, SHA-512 matches"
