# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Watches one CI run and prints a line for every way it can end, including the ways that are not
    an ending.

.DESCRIPTION
    Emits exactly one line per event on stdout, so it can be driven by an agent's background monitor
    or read by a person. It exists because a watcher written inline gets written badly: the obvious
    filter asks for `status == "completed"` and is silent about everything else, and a silent
    watcher is indistinguishable from a run that is still going.

    Five things can happen to a run and this reports all five:

    - It completes. The conclusion is printed **literally** — `success`, `failure`, `cancelled`,
      `timed_out`, `action_required`, `neutral`, `skipped`, `stale` — never translated into a word
      of this script's choosing. A completed run with an empty conclusion is itself reported, since
      that is a state the API can return and a naive reader prints as nothing at all.
    - No run exists for the commit. A push that did not trigger the workflow looks exactly like a
      run that has not started, so it is reported after -MissingLimit consecutive misses.
    - The query itself fails: expired auth, no network, a rate limit. This is the one an inline
      watcher always loses, because `2>/dev/null` on the query buries the error and `|| true` turns
      it into an empty string that reads as "not finished yet".
    - The run is queued or running longer than expected. A heartbeat every -HeartbeatMinutes says so
      out loud rather than leaving a healthy silence and a stuck one looking the same.
    - The run never ends. -TimeoutMinutes is a hard ceiling: the watcher says it is giving up and
      exits, instead of staying armed and mute.

    A run in this repository takes 55-80 minutes: the `Verify` step alone is 33-55, and the
    accessibility, recovery and walk gates follow it. The defaults are set from that.

.PARAMETER Sha
    The commit to watch. A short prefix is enough; it is matched with StartsWith.

.PARAMETER Branch
    The branch whose runs are listed. Defaults to the current one.

.EXAMPLE
    pwsh -NoProfile -File eng/watch-ci.ps1 -Sha 6057dda
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Sha,

    [string]$Branch,

    [int]$PollSeconds = 60,

    [int]$HeartbeatMinutes = 30,

    [int]$TimeoutMinutes = 120,

    [int]$MissingLimit = 5,

    [int]$QueryFailureLimit = 3
)

$ErrorActionPreference = 'Stop'

if (-not $Branch) {
    $Branch = (git rev-parse --abbrev-ref HEAD 2>$null)
    if (-not $Branch) {
        Write-Output "CI ${Sha}: CANNOT DETERMINE BRANCH — not in a git repository?"
        exit 1
    }
}

$short = $Sha.Substring(0, [Math]::Min(7, $Sha.Length))
$minutes = 0
$queryFailures = 0
$missing = 0

while ($true) {
    $minutes++

    # The query is deliberately NOT silenced. Its failure is one of the outcomes being watched for,
    # and a swallowed error is the difference between "CI is broken" and "CI is slow".
    $raw = $null
    $problem = $null
    try {
        $raw = & gh run list --branch $Branch --limit 10 --json headSha,status,conclusion 2>&1
        if ($LASTEXITCODE -ne 0) {
            $problem = ($raw | Out-String).Trim()
        }
    }
    catch {
        $problem = $_.Exception.Message
    }

    if ($problem) {
        $queryFailures++
        if ($queryFailures -ge $QueryFailureLimit) {
            $first = ($problem -split "`n" | Select-Object -First 1)
            Write-Output "CI ${short}: CANNOT QUERY GITHUB after $queryFailures tries — $first"
            exit 1
        }

        Start-Sleep -Seconds $PollSeconds
        continue
    }

    $queryFailures = 0

    $run = $null
    try {
        $run = ($raw | Out-String | ConvertFrom-Json) |
            Where-Object { $_.headSha -and $_.headSha.StartsWith($Sha) } |
            Select-Object -First 1
    }
    catch {
        # Malformed output is a query failure by another name.
        $queryFailures++
        if ($queryFailures -ge $QueryFailureLimit) {
            Write-Output "CI ${short}: UNREADABLE RESPONSE from gh after $queryFailures tries"
            exit 1
        }

        Start-Sleep -Seconds $PollSeconds
        continue
    }

    if (-not $run) {
        $missing++
        if ($missing -ge $MissingLimit) {
            Write-Output "CI ${short}: NO RUN EXISTS after $missing min — the push did not trigger the workflow"
            exit 1
        }

        Start-Sleep -Seconds $PollSeconds
        continue
    }

    $missing = 0

    if ($run.status -eq 'completed') {
        # The conclusion literally, whatever it is. An empty one is reported as empty rather than
        # printed as nothing.
        if ([string]::IsNullOrWhiteSpace($run.conclusion)) {
            Write-Output "CI ${short}: completed with an EMPTY conclusion"
            exit 1
        }

        Write-Output "CI ${short}: $($run.conclusion)"
        if ($run.conclusion -eq 'success') { exit 0 } else { exit 1 }
    }

    if ($HeartbeatMinutes -gt 0 -and ($minutes % $HeartbeatMinutes) -eq 0) {
        Write-Output "CI ${short}: still '$($run.status)' after $minutes min"
    }

    if ($minutes -ge $TimeoutMinutes) {
        Write-Output "CI ${short}: STUCK in '$($run.status)' after $minutes min — check it by hand"
        exit 1
    }

    Start-Sleep -Seconds $PollSeconds
}
