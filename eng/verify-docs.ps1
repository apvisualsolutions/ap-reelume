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
# 74 since 2026-09-12 (evening), when UX-010 was opened: the owner asked for a way to put any group
# of options back the way it came, and measuring what exists found TWO reset controls in the whole
# application — «back to 1x» on the speed, and «restore the provider's fields» on one title's
# record. Not one settings section can be undone. Its gate is a closed list that fails from both
# sides, so the next panel somebody writes cannot be born without the button.
#
# 73 since 2026-09-12 (evening), when PLY-018 was opened against POST_STABLE: the owner's own
# library turned out to be the case nothing covered. A real episode measured 720x404 in a 2003 codec
# at 1.5 Mbit/s, and its mean luma across five scenes ran between 28 and 69 out of 235 — the picture
# is not soft, it is crushed into black, and no row promised a person could do anything about that.
# Upscaling was being built for a defect the owner does not have.
#
# 72 since 2026-09-12, when PLY-017 was opened against STABLE and not MVP: the MVP manifest is a
# closed record of 46 commitments with the tasks that built them, and a row born today has no task to
# name — inventing one would falsify the record. The colour defect showed that NO row promised a
# video would be decoded with the colour space it belongs to, which is why nothing caught it for as
# long as it lasted. A defect that no row covers is a defect no register is watching.
# 71 since 2026-09-03, and it took three sessions in one day to get there. 65 came from 2026-08-30,
# when ADR-0006 was accepted and CRS-001..005 arrived. Then CRS-006 (a course card's picture, taken
# from the video) and LIB-018 (setting your own cover) made it 67. Then the rail menu was measured
# and rejected as UX-009, and the three gaps it had been covering entered on their own: LIB-019
# (rescanning a root on request), LIB-020 (filtering the review inbox and the duplicate groups) and
# CRS-007 (filtering the courses grid). The count is asserted rather than left open so that a row
# added to the matrix has to be added here too, which is where somebody notices that the manifest and
# the localised documents need it as well.
if ($featureIds.Count -ne 74) {
    $errors.Add("Expected 74 feature IDs, found $($featureIds.Count).")
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
