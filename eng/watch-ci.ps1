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

    A sixth way to be wrong is not an outcome but a question: looking where the run is not. Until
    2026-09-02 the runs were listed with `--branch`, defaulting to the local branch, and in a
    worktree the local branch is not the branch the commit was pushed to. `ci.yml` triggers on
    `codex/**`, so a commit written on `claude/goofy-aryabhata-1e2f4a` and pushed to
    `codex/shell-assembly-isolation` had no runs under the name this script asked about, and the
    script answered that the push had not triggered the workflow. It had: the run was
    `in_progress`, and `gh run list --commit` returned it. That is worse than the silence this
    file is written against — a silence reads as "still going" and gets waited on, a confident
    wrong answer gets acted on.

    So the default no longer names a branch. A run belongs to a commit, not to a reference, and
    the commit is what is asked for. -Branch stays for when a branch really is the question.

    A run in this repository takes 42-53 minutes: the twelve complete runs of 2026-08-30 gave 42.7
    for the fastest and 52.6 for the slowest. The defaults are set from that. This line said 55-80
    until 2026-09-02, copied from an era nobody re-measured — the figure had already been fixed in
    CLAUDE.md and in the closing skill, and nobody looked here. It is now held by a test.

.PARAMETER Sha
    The commit to watch. A short prefix is enough: it is resolved to the full forty characters
    with git before being handed to gh, which needs them — given a prefix, `gh run list --commit`
    answers `[]` and exits 0, which reads exactly like "no run yet". Measured 2026-09-02.

.PARAMETER Branch
    Restricts the search to one branch's runs. Empty by default, and deliberately: the local
    branch is not necessarily the branch the commit was pushed to.

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

$short = $Sha.Substring(0, [Math]::Min(7, $Sha.Length))

# gh wants the full forty characters for --commit: given a prefix it answers `[]` and exits 0,
# which has the same shape as "no run yet". So the prefix is resolved here, and the search only
# asks about a commit once it has one that can be asked about.
$fullSha = $null
if ($Sha -match '^[0-9a-fA-F]{40}$') {
    $fullSha = $Sha.ToLowerInvariant()
}
else {
    try {
        $candidate = (& git rev-parse --verify --quiet "$Sha^{commit}" 2>$null | Out-String).Trim()
        if ($candidate -match '^[0-9a-fA-F]{40}$') { $fullSha = $candidate.ToLowerInvariant() }
    }
    catch {
        # Not a git repository, or no git on the path. Neither is fatal here: it only means the
        # commit filter is unavailable, and the search widens instead of narrowing wrongly.
        $fullSha = $null
    }
}

# Where to look, and what to say about the place when nothing is found. The message names the
# place on purpose: "NO RUN EXISTS" was read as a fact about the push when it was only ever an
# answer about one branch.
if ($Branch) {
    $filter = @('--branch', $Branch)
    $limit = 10
    $scope = "on branch '$Branch'"
    $verdict = 'the push did not trigger the workflow, or it landed on another branch'
}
elseif ($fullSha) {
    $filter = @('--commit', $fullSha)
    $limit = 10
    $scope = 'for that commit'
    $verdict = 'the push did not trigger the workflow'
}
else {
    # The prefix could not be resolved, so --commit would be a question that can only be answered
    # wrongly. Widen rather than narrow: every branch's recent runs, matched with StartsWith.
    $filter = @()
    $limit = 30
    $scope = "among the $limit most recent runs of any branch"
    $verdict = "the push did not trigger the workflow, or its run is older than those $limit"
}

$minutes = 0
$queryFailures = 0
$unreadable = 0
$missing = 0

while ($true) {
    $minutes++

    # The query is deliberately NOT silenced. Its failure is one of the outcomes being watched for,
    # and a swallowed error is the difference between "CI is broken" and "CI is slow".
    $raw = $null
    $problem = $null
    try {
        $raw = & gh run list @filter --limit $limit --json headSha,status,conclusion 2>&1
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

        if ($minutes -ge $TimeoutMinutes) {
            Write-Output "CI ${short}: gh has been failing for $minutes min — check it by hand"
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
        # Its own counter, and not $queryFailures: that one is reset to zero on the line above every
        # time gh exits 0, so an unreadable body would have incremented 0 to 1 for ever and never
        # reached the limit. Measured as a real hole on 2026-08-29 — the state that reaches it is gh
        # exiting 0 with a banner or an update notice on stdout, and the watcher would have gone
        # silent permanently, which is the one thing this script exists to prevent.
        $unreadable++
        if ($unreadable -ge $QueryFailureLimit) {
            Write-Output "CI ${short}: UNREADABLE RESPONSE from gh after $unreadable tries"
            exit 1
        }

        # The ceiling is checked before the sleep on this path too. Every `continue` used to jump
        # over it, so a failure that repeats below its own limit outlived the timeout as well.
        if ($minutes -ge $TimeoutMinutes) {
            Write-Output "CI ${short}: gh has been unreadable for $minutes min — check it by hand"
            exit 1
        }

        Start-Sleep -Seconds $PollSeconds
        continue
    }

    $unreadable = 0

    if (-not $run) {
        $missing++
        if ($missing -ge $MissingLimit) {
            Write-Output "CI ${short}: NO RUN EXISTS $scope after $missing min — $verdict"
            exit 1
        }

        if ($minutes -ge $TimeoutMinutes) {
            Write-Output "CI ${short}: no run $scope for $Sha after $minutes min — check it by hand"
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
