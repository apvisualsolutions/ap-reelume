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
    served, which is what the cache records next to it.

    Until 2026-09-18 it also read the application's packages.lock.json for the version. That stops
    being possible the day the application ships the rebuilt tree instead of this package, which is
    the whole point of ENG-013, so the version is now held against the VLC tag build-nogpl.sh builds:
    the day the build moves to another VLC, this refuses to compare against the old reference
    instead of doing it quietly.

.PARAMETER Destination
    The folder to expand the package into; the trees are under build/x64 and build/arm64.
#>
[CmdletBinding()]
param([Parameter(Mandatory)] [string] $Destination)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pinnedVersion = '3.0.23.1'
$pinnedSha512 = 'Ji3p6cv6bn9FFgXS+M35nzWEvjzpZ5Kig/AvMl5iRAGv4z0QbeMDX+Knuoh2hStsVLb8AATig+LX2HLVey50/A=='

$buildScript = Get-Content (Join-Path $PSScriptRoot 'build-nogpl.sh') -Raw
if ($buildScript -notmatch '(?m)^VLC_TAG=(\S+)$') { throw "build-nogpl.sh does not declare VLC_TAG=." }
$vlcTag = $Matches[1]
# The package's fourth digit is its own packaging revision: 3.0.23.1 is VLC 3.0.23.
if (-not $pinnedVersion.StartsWith("$vlcTag.")) {
    throw "build-nogpl.sh builds VLC $vlcTag and this reference is VideoLAN.LibVLC.Windows ${pinnedVersion}: re-pin it to the package of the same VLC."
}

New-Item -ItemType Directory -Force $Destination | Out-Null
$package = Join-Path $Destination 'reference.nupkg'
Invoke-WebRequest "https://www.nuget.org/api/v2/package/VideoLAN.LibVLC.Windows/$pinnedVersion" -OutFile $package
$actual = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package)))
# -cne: Base64 is case-sensitive and PowerShell's -ne is not.
if ($actual -cne $pinnedSha512) { throw "VideoLAN.LibVLC.Windows $pinnedVersion from nuget.org is not the pinned package: $actual" }
Expand-Archive $package $Destination -Force
"reference: VideoLAN.LibVLC.Windows $pinnedVersion, SHA-512 matches"
