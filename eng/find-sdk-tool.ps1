# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Locates one Windows SDK command-line tool, newest SDK first.

.DESCRIPTION
    Both packaging scripts need MakeAppx to seal a package and MakePri to build the resources Windows
    reads the description from, and the sandbox cycle needs SignTool to sign the throwaway copy it
    installs, so the search lives here instead of being written out once per script per tool.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('makeappx.exe', 'makepri.exe', 'signtool.exe')]
    [string]$Name
)

$ErrorActionPreference = 'Stop'

# @() because a machine with exactly one SDK yields a bare string, and indexing a string returns its
# first character — the packaging step then tries to run a program called 'C'.
$candidates = @(Get-ChildItem -LiteralPath 'C:/Program Files (x86)/Windows Kits/10/bin' -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^10\.' } |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName "x64/$Name" } |
    Where-Object { Test-Path -LiteralPath $_ })
if (-not $candidates) {
    throw "$Name was not found. Install the Windows 10/11 SDK to build the package."
}

$candidates[0]
