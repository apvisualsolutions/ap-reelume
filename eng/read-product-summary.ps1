# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    What the product says about itself in one language, taken from that language's README.

.DESCRIPTION
    The first paragraph under the title is the description. It is read rather than written down a
    second time, because two install channels now show it — the winget entry and what Windows reads
    from the MSIX — and a description typed in three places is a description that stops agreeing with
    itself at the third release.

    The documentation suite already keeps the two READMEs paired, so the pairing this relies on is
    enforced elsewhere rather than assumed here.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path
)

$ErrorActionPreference = 'Stop'

$collected = [Collections.Generic.List[string]]::new()
$started = $false
foreach ($line in Get-Content -LiteralPath $Path) {
    if ($line -match '^#\s') { $started = $true; continue }
    if (-not $started) { continue }
    if ([string]::IsNullOrWhiteSpace($line)) {
        if ($collected.Count -gt 0) { break }
        continue
    }

    $collected.Add($line.Trim())
}

$summary = (($collected -join ' ') -replace '\*\*', '').Trim()
if (-not $summary) { throw "$Path has no description under its title." }
$summary
