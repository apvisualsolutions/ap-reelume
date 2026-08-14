# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Fetches the corresponding source of the copyleft libraries this artifact carries.

.DESCRIPTION
    So that a release can offer the source from the same place as the binary. Both licences say the
    same thing about that: LGPL-2.1 section 6(d) and the last paragraph of GPL-2.0 section 3 accept
    equivalent access to copy the source from the designated place the executable is offered from,
    and neither needs interpreting. The written offer this project also carries stays as the answer
    for channels where "the same place" means nothing, like a store.

    What this script does not do is guess. Where upstream publishes a digest, the download is checked
    against it and a mismatch stops the release; where upstream generates the archive on request and
    there is nothing to check against, that is stated in the manifest and the proof of what was
    distributed is the digest of the attached file, which SHA256SUMS.txt records and the release
    signs.
#>
[CmdletBinding()]
param(
    [string]$Output = 'artifacts/package/source'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath($Output, $repoRoot)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$registryPath = Join-Path $PSScriptRoot 'corresponding-source.json'
$registry = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
if ($registry.formatVersion -ne 1) {
    throw "Unsupported corresponding-source manifest format $($registry.formatVersion)."
}

$written = [System.Collections.Generic.List[object]]::new()
foreach ($source in $registry.sources) {
    $destination = Join-Path $outputRoot $source.fileName
    Write-Output "Fetching $($source.name) $($source.version) from $($source.url)"
    Invoke-WebRequest -Uri $source.url -OutFile $destination -MaximumRedirection 5 -UseBasicParsing

    $actualSize = (Get-Item -LiteralPath $destination).Length
    $actualHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash

    # A download that answered is not a download that worked. The first run of this script asked
    # code.videolan.org for the LibVLCSharp archive and received an anti-bot page: 4,445 bytes of
    # HTML, saved under the archive's name, on its way to being attached to a release as source
    # code. The name of a file proves nothing about it, so the format and a floor on the size are
    # checked before anything downstream believes it.
    $magic = [System.IO.File]::ReadAllBytes($destination)
    $prefix = -join ($magic | Select-Object -First ($source.magic.Length / 2) | ForEach-Object { $_.ToString('X2') })
    if ($prefix -ne $source.magic) {
        throw "$($source.fileName) starts with $prefix, not $($source.magic): what came back is not the archive."
    }

    if ($actualSize -lt $source.minimumSizeBytes) {
        throw "$($source.fileName) is $actualSize bytes, below the floor of $($source.minimumSizeBytes)."
    }

    if ($null -ne $source.sizeBytes -and $actualSize -ne $source.sizeBytes) {
        throw "$($source.fileName) is $actualSize bytes; the manifest records $($source.sizeBytes)."
    }

    if ($null -ne $source.sha256) {
        if ($actualHash -ne $source.sha256.ToUpperInvariant()) {
            throw "$($source.fileName) hashes to $actualHash; the manifest records $($source.sha256)."
        }

        Write-Output "  verified against the digest $($source.name) publishes"
    }
    else {
        # Not a weaker promise about what is distributed, only about what it can be compared with:
        # the file attached to the release is the one this digest belongs to, and SHA256SUMS.txt
        # carries it into the signature.
        Write-Output "  no published digest upstream; recording the digest of what was fetched"
    }

    $written.Add([ordered]@{
        name = $source.name
        version = $source.version
        carriedBy = $source.carriedBy
        fileName = $source.fileName
        url = $source.url
        sizeBytes = $actualSize
        sha256 = $actualHash
        verifiedAgainstPublishedDigest = $null -ne $source.sha256
    })
}

$manifest = [ordered]@{
    formatVersion = 1
    purpose = 'Corresponding source offered from the same place as the binary: LGPL-2.1 6(d), GPL-2.0 3.'
    sources = $written
}
$manifestPath = Join-Path $outputRoot 'corresponding-source.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
Write-Output "Corresponding source written to $outputRoot"
