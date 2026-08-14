# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$docsRoot = Join-Path $repoRoot 'docs'
$errors = [System.Collections.Generic.List[string]]::new()

$localizedFiles = Get-ChildItem -LiteralPath $docsRoot -Recurse -File |
    Where-Object { $_.Name -match '\.(es|en)\.md$' }

foreach ($file in $localizedFiles) {
    $counterpartName = if ($file.Name.EndsWith('.es.md', [StringComparison]::OrdinalIgnoreCase)) {
        $file.Name.Substring(0, $file.Name.Length - 6) + '.en.md'
    }
    else {
        $file.Name.Substring(0, $file.Name.Length - 6) + '.es.md'
    }

    $counterpart = Join-Path $file.DirectoryName $counterpartName
    if (-not (Test-Path -LiteralPath $counterpart -PathType Leaf)) {
        $relative = [IO.Path]::GetRelativePath($repoRoot, $file.FullName)
        $errors.Add("Missing bilingual counterpart for $relative")
    }
}

$markdownFiles = Get-ChildItem -LiteralPath $docsRoot -Recurse -Filter '*.md' -File
$linkPattern = [regex]'\[[^\]]+\]\((?<target>[^)]+)\)'
foreach ($file in $markdownFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
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
        $resolvedTarget = [IO.Path]::GetFullPath((Join-Path $file.DirectoryName $decodedTarget))
        if (-not (Test-Path -LiteralPath $resolvedTarget)) {
            $relativeFile = [IO.Path]::GetRelativePath($repoRoot, $file.FullName)
            $errors.Add("Broken link in ${relativeFile}: $target")
        }
    }
}

$featureMatrixPath = Join-Path $docsRoot 'FEATURES.md'
$featureMatrix = Get-Content -LiteralPath $featureMatrixPath -Raw
$featureIds = [regex]::Matches($featureMatrix, '(?m)^\| (?<id>[A-Z0-9]+-[0-9]+) \|')
$mvpIds = [regex]::Matches($featureMatrix, '(?m)^\| (?<id>[A-Z0-9]+-[0-9]+) \|.*\| MVP \|')
if ($featureIds.Count -ne 57) {
    $errors.Add("Expected 57 feature IDs, found $($featureIds.Count).")
}
if ($mvpIds.Count -ne 46) {
    $errors.Add("Expected 46 MVP feature IDs, found $($mvpIds.Count).")
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

# The counts are read back from what was just measured. They used to be written into the sentence by
# hand, so raising the ratchet to 57 left the gate checking one number and announcing another.
Write-Output "Documentation verification passed: $($markdownFiles.Count) Markdown files, $($localizedFiles.Count) localized files, $($featureIds.Count) feature IDs, $($mvpIds.Count) MVP IDs."
