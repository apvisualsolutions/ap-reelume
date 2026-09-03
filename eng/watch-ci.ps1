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

    Since 2026-09-03 it also reports PROGRESS, which is not an ending and is the reason the list
    above says "including the ways that are not an ending". Every step that finishes gets a line
    when it finishes rather than when the run does: this workflow's heaviest step runs for over
    half an hour, so a failure inside it used to be knowable only from the run's own conclusion,
    forty minutes later.

    Steps and not jobs, and that was measured rather than chosen: this workflow has exactly one
    job, so a job-level event lands in the same second the run ends and adds nothing. The thirteen
    real gates are steps, and `gh run view <id> --json jobs` returns each one with its own status
    while the run is still in_progress.

    The runner's own bookkeeping is filtered while it passes — it is the same four lines on every
    run — and never when it fails, because scaffolding that fails is the run failing. Each step is
    announced once and not on every poll: this thing looks once a minute for the better part of an
    hour, and an alert that fires forty times is the alert this file already says teaches people to
    ignore it.

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

.PARAMETER NoStepEvents
    Turns off the per-step progress and leaves only the outcome. On by default, because hearing
    about a failure when it happens is the point; the switch is for a reader who wants the verdict
    and nothing else.

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

    [int]$QueryFailureLimit = 3,

    # Steps are on by default: the whole point is to hear about a failure when it happens rather
    # than forty minutes later. The switch exists for a reader who wants the outcome and nothing
    # else, and for the tests, which assert both shapes.
    [switch]$NoStepEvents
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

# One line per STEP that finishes, so a failure inside a run is known when it happens rather than
# when the run ends. Measured 2026-09-03 against a live run: `gh run view <id> --json jobs` returns
# every step with its own status while the run is still `in_progress`, so this costs one extra
# query per poll and no waiting.
#
# WHY STEPS AND NOT JOBS. The obvious unit is the job, and here it says nothing: this workflow has
# exactly one job, `verify`, so a job-level event lands at the same moment the run ends and adds
# nothing to the line that was already printed. The steps are where the thirteen real gates live,
# and the heaviest of them runs for over half an hour.
#
# WHAT IS FILTERED, AND WHY IT IS NOT EVERYTHING. Checkout, the SDK, ffmpeg and the runner's own
# `Set up job` / `Post Run …` bookkeeping say nothing about this repository's code: they are the
# same four lines on every run, and this file already has it written down that an alert which
# fires when it should not teaches people to ignore it. What is left is around nine events across
# forty-five minutes.
#
# A FAILED STEP IS NEVER FILTERED, whatever it is called. Scaffolding that fails is the run
# failing, and it is the one case where the noise is the news.
function Get-FinishedSteps {
    param(
        [Parameter(Mandatory = $true)][long]$RunId,
        # AllowEmptyCollection, because a Mandatory parameter refuses an empty collection and the
        # set is empty on the first poll — which is exactly the call that matters. Measured
        # 2026-09-03: without it the watcher died on its first cycle with «Cannot bind argument».
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.HashSet[string]]$Announced
    )

    # This query is accessory: it reports progress, it does not decide the outcome. So unlike the
    # run query — which is deliberately not silenced, because its failure IS one of the outcomes —
    # a failure here must not kill the watcher over a commit whose verdict is still coming.
    try {
        $raw = & gh run view $RunId --json jobs 2>&1
        if ($LASTEXITCODE -ne 0) { return @() }
        $jobs = ($raw | Out-String | ConvertFrom-Json).jobs
    }
    catch {
        return @()
    }

    $events = @()
    foreach ($job in $jobs) {
        foreach ($step in $job.steps) {
            if ($step.status -ne 'completed') { continue }

            $key = "$($job.name)/$($step.number)"
            if ($Announced.Contains($key)) { continue }
            [void]$Announced.Add($key)

            $failed = $step.conclusion -and $step.conclusion -ne 'success' -and $step.conclusion -ne 'skipped'

            # Scaffolding is skipped only while it passes. A checkout that fails is the run failing.
            # «Install ffmpeg» is named here and not covered by the patterns above, which is how
            # this filter was caught on its first live run: the comment claimed ffmpeg was
            # scaffolding while the code let it through. A comment that describes what the code
            # does not do is the defect this repository documented the same day.
            $scaffolding = $step.name -match '^(Set up job|Complete job|Run actions/|Post Run |Post Set up|Install ffmpeg)'
            if ($scaffolding -and -not $failed) { continue }

            if ($failed) {
                $events += "STEP FAILED '$($step.name)' — $($step.conclusion)"
            }
            else {
                $events += "step ok '$($step.name)'"
            }
        }
    }

    return $events
}

$announcedSteps = [System.Collections.Generic.HashSet[string]]::new()

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
        $raw = & gh run list @filter --limit $limit --json databaseId,headSha,status,conclusion 2>&1
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

    # The steps that finished since the last poll, including the ones that finished in the very
    # cycle that saw the run complete: they are emitted BEFORE the conclusion, so the line naming
    # the gate that failed arrives above the line saying the run failed rather than after it.
    if (-not $NoStepEvents -and $run.databaseId) {
        foreach ($event in Get-FinishedSteps -RunId $run.databaseId -Announced $announcedSteps) {
            Write-Output "CI ${short}: $event"
        }
    }

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
