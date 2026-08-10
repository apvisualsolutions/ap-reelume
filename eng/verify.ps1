# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    # The packaging suites read the built artifact and its reports, so a verification that skipped
    # building them would leave them failing rather than passing. This exists for the release
    # workflow, which has already built and walked the package before it gets here.
    [switch]$SkipPackaging,

    # CI-003/CI-005: on shared runners the performance budgets measure the neighbour's workload as
    # much as this code, so CI runs them, archives their result, and does not let their verdict
    # block. The budgets stay blocking wherever this switch is absent — the physical harness, where
    # they mean something.
    [switch]$NonBlockingPerformance
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'ApSolutions.LocalMedia.sln'
$resultsDirectory = Join-Path $repoRoot "artifacts/test-results/verify-$Runtime"

Push-Location $repoRoot
try {
    # A fresh results directory per run, because the coverage gate merges every report it finds
    # here. Left to accumulate, a local machine that verifies repeatedly ends up measuring code that
    # no longer exists — a line covered by a run from an hour ago counts as covered today — and the
    # merge command eventually outgrows the command line and fails with a message about a filename
    # being too long, which says nothing about the cause. CI never saw this because it starts clean.
    if (Test-Path -LiteralPath $resultsDirectory) {
        Remove-Item -LiteralPath $resultsDirectory -Recurse -Force
    }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    dotnet restore $solution --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }

    dotnet restore $solution -r $Runtime --force-evaluate -p:NuGetLockFilePath="obj/packages.$Runtime.lock.json"
    if ($LASTEXITCODE -ne 0) { throw "Runtime dependency restore failed for $Runtime." }

    dotnet format $solution --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Formatting verification failed.' }

    dotnet build $solution -c $Configuration --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

    # The container/codec matrix is generated from recipes, never redistributed. A missing encoder
    # is reported and its rows skip with a reason; it is never replaced by a substitute.
    & (Join-Path $PSScriptRoot 'generate-test-media.ps1') -Output 'artifacts/test-media'
    if ($LASTEXITCODE -ne 0) { throw 'Test media generation failed.' }

    # The package is part of what is being verified, not something produced afterwards from it. The
    # packaging suites read the sealed artifact and the reports below, and fail when they are absent,
    # so building them here is what keeps those suites from being quietly unenforceable.
    if (-not $SkipPackaging) {
        if ($Runtime -eq 'win-x64') {
            & (Join-Path $PSScriptRoot 'package-x64.ps1') -Configuration $Configuration
            if ($LASTEXITCODE -ne 0) { throw 'Packaging failed.' }

            & (Join-Path $PSScriptRoot 'verify-package.ps1') -Mode Verify
            if ($LASTEXITCODE -ne 0) { throw 'Package lifecycle or reproducibility verification failed.' }
        }

        # Both architectures are built on every verification, whichever one is being verified. The
        # ARM64 suites are part of the same run as everything else, and a suite whose artifact is only
        # built when someone remembers to ask for it is a suite that stops being enforced. It is built
        # after the x64 one because it compares its payload against that layout.
        & (Join-Path $PSScriptRoot 'package-arm64.ps1') -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) { throw 'ARM64 packaging failed.' }
    }

    # `-m:1` keeps MSBuild from starting one VSTest invocation per project at the same time. Parallel
    # hosts destabilise LibVLC and made a host die on its sixty-second connection timeout.
    if ($NonBlockingPerformance) {
        dotnet test $solution -c $Configuration --no-build -m:1 --settings (Join-Path $PSScriptRoot 'test.runsettings') --filter 'FullyQualifiedName!~PerformanceTests' --logger trx --results-directory $resultsDirectory --collect:'XPlat Code Coverage'
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

        # The budgets still run and their verdict is still written down — it just cannot block a
        # runner whose noise is not the phenomenon under test. The physical harness blocks as ever.
        dotnet test (Join-Path $repoRoot 'tests/ApSolutions.LocalMedia.PerformanceTests/ApSolutions.LocalMedia.PerformanceTests.csproj') -c $Configuration --no-build -m:1 --settings (Join-Path $PSScriptRoot 'test.runsettings') --logger trx --results-directory $resultsDirectory
        $performanceOutcome = if ($LASTEXITCODE -eq 0) { 'passed' } else { 'failed-nonblocking' }
        Set-Content -LiteralPath (Join-Path $resultsDirectory 'performance-nonblocking.json') -Value (@{
                outcome  = $performanceOutcome
                exitCode = $LASTEXITCODE
                note     = 'Performance budgets are non-blocking on shared runners (CI-003/CI-005); they block on the physical harness via eng/run-performance.ps1.'
            } | ConvertTo-Json) -Encoding utf8NoBOM
        if ($performanceOutcome -ne 'passed') {
            Write-Warning 'Performance budgets failed on this runner; the result is archived and does not block CI.'
        }
    }
    else {
        dotnet test $solution -c $Configuration --no-build -m:1 --settings (Join-Path $PSScriptRoot 'test.runsettings') --logger trx --results-directory $resultsDirectory --collect:'XPlat Code Coverage'
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    }

    # TST-001: every source file that is new against main must arrive with its coverage. The diff
    # is usually empty on CI because main advances by fast-forward with the branch, so the gate's
    # teeth are local, before the push; on a pull request from elsewhere the diff is real.
    & (Join-Path $PSScriptRoot 'check-coverage.ps1') -ResultsDirectory $resultsDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Coverage gate failed.' }

    & (Join-Path $PSScriptRoot 'verify-docs.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Documentation verification failed.' }

    # Generated media and test artifacts must never reach the index.
    $tracked = git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'Git status failed.' }
    $leaked = $tracked | Where-Object { $_ -match '(artifacts/|\.(mp4|mkv|avi|mov|webm|m4v|ts|m2ts|srt|ass|vtt)$)' }
    if ($leaked) {
        $leaked | ForEach-Object { Write-Error "Generated media or artifacts leaked into the working tree: $_" }
        throw 'Generated media must stay ignored.'
    }
}
finally {
    Pop-Location
}
