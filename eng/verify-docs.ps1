# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions

<#
.SYNOPSIS
    Checks the published documentation: every localized document has its counterpart, and every
    relative link resolves to something that is published too.

.DESCRIPTION
    The documents checked are the Markdown files under docs/ and at the root of the repository that
    git would publish: what it tracks plus what it would add, minus what it ignores. A working copy
    can hold files that never leave the machine, and a link that resolves only because one of those
    sits next to it is a link that breaks for everybody else. Outside a git work tree (an exported
    source archive, for one) the files on disk are the published set.

    A link is checked against that same set, not against the disk, so a published document pointing
    at an unpublished file fails here exactly as it would fail for a reader.

    An empty docs/ would otherwise pass every check by having nothing to check, so there is a floor
    on how many documents were read.
#>
[CmdletBinding()]
param(
    # The fewest Markdown files under docs/ a real run reads. The user guide, troubleshooting,
    # privacy, the changelog and the release notices are seven pairs today.
    [int]$MinimumDocuments = 12,

    # The fewest localized (.es.md / .en.md) files under docs/ a real run reads.
    [int]$MinimumLocalized = 12
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$errors = [System.Collections.Generic.List[string]]::new()

function ConvertTo-RepositoryPath([string]$path) {
    [IO.Path]::GetRelativePath($repoRoot, $path).Replace('\', '/')
}

# ------------------------------------------------------------------ what is published
$published = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$source = 'files on disk'
$insideGit = $false
if (Get-Command git -ErrorAction SilentlyContinue) {
    $probe = & git -c safe.directory=* -C $repoRoot rev-parse --is-inside-work-tree 2>$null
    $insideGit = ($LASTEXITCODE -eq 0 -and "$probe".Trim() -eq 'true')
}

if ($insideGit) {
    $listed = & git -c safe.directory=* -c core.quotepath=off -C $repoRoot ls-files --cached --others --exclude-standard
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed, so the published set is unknown.' }
    foreach ($path in $listed) {
        if ($path -and (Test-Path -LiteralPath (Join-Path $repoRoot $path) -PathType Leaf)) {
            [void]$published.Add($path)
        }
    }
    $source = 'git'
}
else {
    $skipped = '^(artifacts|\.git|\.vs|\.vendor)/|(^|/)(bin|obj)/'
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
        if ($entry.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    $false
}

$markdownFiles = @($published |
    Where-Object { $_ -match '\.md$' -and ($_ -match '^docs/' -or $_ -notmatch '/') } |
    Sort-Object)
$docsMarkdown = @($markdownFiles | Where-Object { $_ -match '^docs/' })
$localizedFiles = @($docsMarkdown | Where-Object { $_ -match '\.(es|en)\.md$' })

# ------------------------------------------------------------------ bilingual pairs
foreach ($path in $localizedFiles) {
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
$linkPattern = [regex]'\[[^\]]+\]\((?<target>[^)]+)\)'
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
