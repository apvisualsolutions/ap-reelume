# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions

<#
.SYNOPSIS
    Refuses a LibVLC build whose third-party sources are not exactly the ones contrib-licences.json names.

.DESCRIPTION
    The notice of the libraries linked inside the engine (docs/release/licenses/NOTICE-LibVLC-contribs.txt)
    is written from contrib-licences.json, and that list was read from one build. A new VLC tag or a
    contrib recipe that pulls in one more library would publish an engine whose notice leaves it out;
    one that stops fetching a library would leave the notice describing a binary that is not the one
    published. Both are refused here, by name, before anything is uploaded.

    The *.githash files the build leaves beside an archive it made from git are fetch stamps, not
    sources, and are not compared.

.PARAMETER Tarballs
    The directory the publication assembles as contrib-tarballs/, with the per-architecture
    subdirectories it keeps for archives whose bytes differ between the two builds.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Tarballs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Tarballs -PathType Container)) {
    throw "'$Tarballs' is not a directory: there is nothing to compare the inventory with."
}

$inventory = Get-Content (Join-Path $PSScriptRoot 'contrib-licences.json') -Raw | ConvertFrom-Json
$named = [System.Collections.Generic.HashSet[string]]::new([string[]]@($inventory.sources | ForEach-Object tarball), [StringComparer]::Ordinal)

$root = (Resolve-Path $Tarballs).Path.TrimEnd('\', '/')
$fetched = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
Get-ChildItem $root -Recurse -File |
    Where-Object Extension -ne '.githash' |
    ForEach-Object { [void]$fetched.Add($_.FullName.Substring($root.Length + 1).Replace('\', '/')) }

$unknown = @($fetched | Where-Object { -not $named.Contains($_) } | Sort-Object)
$gone = @($named | Where-Object { -not $fetched.Contains($_) } | Sort-Object)

foreach ($name in $unknown) { Write-Output "NOT IN THE INVENTORY: $name - read its licence and whether it ships, then add it" }
foreach ($name in $gone) { Write-Output "NO LONGER FETCHED: $name - take it out of the inventory and the notice" }

if ($unknown.Count + $gone.Count -gt 0) {
    Write-Output "contrib-licences.json does not describe this build: $($unknown.Count) unknown, $($gone.Count) gone."
    exit 1
}

Write-Output "contrib-licences.json names the $($fetched.Count) sources this build fetched, and no other."
