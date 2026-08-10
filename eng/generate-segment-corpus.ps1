# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Materialises the frozen segment-detection evaluation corpus from its manifest.

.DESCRIPTION
    The corpus structure and ground truth live in
    tests/ApSolutions.LocalMedia.MediaTests/Fixtures/segment-corpus-manifest.json; the recipes are
    derived from that structure by one C# implementation (SegmentCorpus), so this script does not
    duplicate them. It simply runs the corpus materialisation test, which synthesises every episode
    under artifacts/test-media/segments. Everything is deterministic tone chords and colour frames;
    no user or third-party media is ever involved.
#>
[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

if ($Force) {
    $segments = Join-Path $repoRoot 'artifacts/test-media/segments'
    if (Test-Path -LiteralPath $segments) {
        Remove-Item -LiteralPath $segments -Recurse -Force
    }
}

# Test child processes must never inherit an instrumentation profiler.
$env:CORECLR_ENABLE_PROFILING = ''
Get-ChildItem env: | Where-Object { $_.Name -like 'CORECLR_PROFILER*' -or $_.Name -like 'COR_*' } |
    ForEach-Object { Set-Item -Path "env:$($_.Name)" -Value '' }

dotnet test (Join-Path $repoRoot 'tests/ApSolutions.LocalMedia.MediaTests/ApSolutions.LocalMedia.MediaTests.csproj') `
    -m:1 --settings (Join-Path $repoRoot 'eng/test.runsettings') `
    --filter 'FullyQualifiedName~SegmentCorpusTests.The_whole_corpus_materialises_from_the_manifest'
exit $LASTEXITCODE
