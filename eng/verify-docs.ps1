# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions

<#
.SYNOPSIS
    Checks the published documentation: every localized document has its counterpart, and every
    relative link resolves to something that is published too.

.DESCRIPTION
    The documents checked are every Markdown file the repository publishes, wherever it lives. The
    published set is the one the test suites use (tests/Shared/PublishedFiles.cs): what git tracks
    plus what it would add, minus anything the ignore rules exclude even though it is still in the
    index. A working copy can hold files that never leave the machine, and a link that resolves only
    because one of those sits next to it is a link that breaks for everybody else. Outside a git work
    tree (an exported source archive, for one) the files on disk are the published set.

    A link is checked against that same set, not against the disk, so a published document pointing
    at an unpublished file fails here exactly as it would fail for a reader. The comparison is exact,
    case included: GitHub serves a path as it is written, so a link that resolves only on a disk that
    ignores case is broken for anybody reading the repository online.

    An empty docs/ would otherwise pass every check by having nothing to check, so there is a floor
    on how many documents were read.
#>
[CmdletBinding()]
param(
    # The fewest Markdown files under docs/ a real run reads. The user guide, troubleshooting,
    # privacy, the changelog and the release notices are seven pairs today.
    [int]$MinimumDocuments = 12,

    # The fewest localized (.es.md / .en.md) files under docs/ a real run reads.
    [int]$MinimumLocalized = 12,

    # The repository to check. The one this script lives in, unless a test points it at a
    # repository made for the purpose.
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$errors = [System.Collections.Generic.List[string]]::new()

function ConvertTo-RepositoryPath([string]$path) {
    [IO.Path]::GetRelativePath($repoRoot, $path).Replace('\', '/')
}

# ------------------------------------------------------------------ what is published
$published = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$source = 'files on disk'
$insideGit = $false
if (Get-Command git -ErrorAction SilentlyContinue) {
    $probe = & git -c safe.directory=* -C $repoRoot rev-parse --is-inside-work-tree 2>$null
    $insideGit = ($LASTEXITCODE -eq 0 -and "$probe".Trim() -eq 'true')
}

function Get-GitPaths([string[]]$arguments) {
    $output = & git -c safe.directory=* -c core.quotepath=off -C $repoRoot ls-files -z @arguments
    if ($LASTEXITCODE -ne 0) { throw "git ls-files $arguments failed, so the published set is unknown." }
    @(($output -join "`n").Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
}

if ($insideGit) {
    # Ignoring a path stops it from being added, never removes it from the index, so a file
    # excluded by the rules in force can still be tracked. It is on its way out and nothing may
    # lean on it: the test suites read it as unpublished, and so does this.
    $excluded = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($path in Get-GitPaths @('--cached', '--ignored', '--exclude-standard')) { [void]$excluded.Add($path) }
    foreach ($path in Get-GitPaths @('--cached', '--others', '--exclude-standard')) {
        if (-not $excluded.Contains($path) -and (Test-Path -LiteralPath (Join-Path $repoRoot $path) -PathType Leaf)) {
            [void]$published.Add($path)
        }
    }
    $source = 'git'
}
else {
    # The same folders tests/Shared/PublishedFiles.cs leaves out when there is no repository to ask.
    $skipped = '(^|/)(bin|obj|artifacts|\.git|\.vs|\.vendor|node_modules)/'
    foreach ($file in Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Force) {
        $path = ConvertTo-RepositoryPath $file.FullName
        if ($path -notmatch $skipped) { [void]$published.Add($path) }
    }
}

if ($published.Count -eq 0) { throw "The published set read from $source is empty." }

function Test-Published([string]$path) {
    if ($published.Contains($path)) { return $true }
    $prefix = $path.TrimEnd('/') + '/'
    foreach ($entry in $published) {
        if ($entry.StartsWith($prefix, [StringComparison]::Ordinal)) { return $true }
    }
    $false
}

# Every published document, not only docs/ and the root: a guide beside a script is read by the
# same people, and its links break for them the same way.
$markdownFiles = @($published | Where-Object { $_ -match '\.md$' } | Sort-Object)
$docsMarkdown = @($markdownFiles | Where-Object { $_ -match '^docs/' })
$localizedFiles = @($docsMarkdown | Where-Object { $_ -match '\.(es|en)\.md$' })
$allLocalized = @($markdownFiles | Where-Object { $_ -match '\.(es|en)\.md$' })

# ------------------------------------------------------------------ bilingual pairs
foreach ($path in $allLocalized) {
    $counterpart = if ($path.EndsWith('.es.md', [StringComparison]::OrdinalIgnoreCase)) {
        $path.Substring(0, $path.Length - 6) + '.en.md'
    }
    else {
        $path.Substring(0, $path.Length - 6) + '.es.md'
    }

    if (-not $published.Contains($counterpart)) {
        $errors.Add("Missing bilingual counterpart for $path")
    }
}

# ------------------------------------------------------------------ relative links
# The text between the brackets may be empty: an image without alternative text still points at
# a file, and that file still has to be published.
$linkPattern = [regex]'\[[^\]]*\]\((?<target>[^)]+)\)'
foreach ($path in $markdownFiles) {
    $fullPath = Join-Path $repoRoot $path
    $content = Get-Content -LiteralPath $fullPath -Raw
    foreach ($match in $linkPattern.Matches($content)) {
        $target = $match.Groups['target'].Value.Trim().Trim('<', '>')
        if ($target -match '^(https?://|mailto:|#)') {
            continue
        }

        $relativeTarget = ($target -split '#', 2)[0]
        if ([string]::IsNullOrWhiteSpace($relativeTarget)) {
            continue
        }

        $decodedTarget = [Uri]::UnescapeDataString($relativeTarget)
        $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $fullPath) $decodedTarget))
        $resolvedPath = ConvertTo-RepositoryPath $resolved
        if ($resolvedPath.StartsWith('..') -or -not (Test-Published $resolvedPath)) {
            $errors.Add("Broken or unpublished link in ${path}: $target")
        }
    }
}

# ------------------------------------------------------------------ the floor
if ($docsMarkdown.Count -lt $MinimumDocuments) {
    $errors.Add("Only $($docsMarkdown.Count) Markdown file(s) under docs/ were read from $source; at least $MinimumDocuments are expected.")
}
if ($localizedFiles.Count -lt $MinimumLocalized) {
    $errors.Add("Only $($localizedFiles.Count) localized file(s) under docs/ were read from $source; at least $MinimumLocalized are expected.")
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

# The counts are read back from what was just measured, never written into the sentence by hand.
Write-Output "Documentation verification passed ($source): $($markdownFiles.Count) Markdown files, $($docsMarkdown.Count) under docs/, $($localizedFiles.Count) localized."
