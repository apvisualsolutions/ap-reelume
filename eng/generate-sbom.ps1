# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-AP-Reelume

<#
.SYNOPSIS
    Writes the bill of materials for the artifact, in both formats.

.DESCRIPTION
    The list comes from the lock files, so it describes what the build actually resolved rather than
    what the project files ask for. Licences are read from the packages themselves, because a bill of
    materials whose licence column is empty answers the one question it exists to answer with a
    shrug.

    Nothing in the output varies between two runs of the same commit: the timestamp is the commit's
    own, and the document identity is derived from it. An SBOM that changed on every run would make
    the artifact irreproducible by itself.
#>
[CmdletBinding()]
param(
    [string]$Output = 'artifacts/package/sbom',

    [Parameter(Mandatory)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath($Output, $repoRoot)
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$commit = (git -C $repoRoot rev-parse HEAD).Trim()
$commitDate = (git -C $repoRoot log -1 --format=%cI).Trim()

# The program's own licence, which is not on the SPDX list and therefore cannot be named the way a
# third-party licence is. SPDX calls that a `LicenseRef-`, and requires the document to carry the
# text of any it names; CycloneDX refuses one in `id` and takes it in `name`. Both are fed from here
# so the two documents cannot drift apart, and the text is read from LICENSE rather than repeated,
# because a licence quoted twice is a licence that will eventually say two things.
$ownLicenceId = 'LicenseRef-AP-Reelume'
$ownLicenceName = 'AP Reelume Licence'
$ownLicencePath = Join-Path $repoRoot 'LICENSE'
if (-not (Test-Path -LiteralPath $ownLicencePath)) { throw "LICENSE is missing, so the bill of materials cannot state the program's own licence." }
$ownLicenceText = Get-Content -LiteralPath $ownLicencePath -Raw

# Every lock file the shipped code is built from. Test projects are deliberately absent: their
# dependencies are not in the artifact, and listing them would describe a payload nobody downloads.
$lockFiles = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Recurse -Filter 'packages.lock.json' -File)
$runtimeLock = Join-Path $repoRoot 'src/ApSolutions.LocalMedia.Windows/obj/packages.win-x64.lock.json'
if (Test-Path -LiteralPath $runtimeLock) { $lockFiles += Get-Item -LiteralPath $runtimeLock }
if ($lockFiles.Count -eq 0) { throw 'No lock file was found, so the dependency list cannot be established.' }

$packageCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }

function Get-DeclaredLicence {
    param([string]$Id, [string]$PackageVersion)

    $nuspec = Join-Path $packageCache "$($Id.ToLowerInvariant())/$PackageVersion/$($Id.ToLowerInvariant()).nuspec"
    if (-not (Test-Path -LiteralPath $nuspec)) { return 'NOASSERTION' }

    try {
        [xml]$document = Get-Content -LiteralPath $nuspec -Raw
        $metadata = $document.package.metadata
        if ($metadata.license -and $metadata.license.type -eq 'expression') { return [string]$metadata.license.'#text' }
        if ($metadata.license -and $metadata.license -is [string]) { return [string]$metadata.license }
        if ($metadata.licenseUrl) { return [string]$metadata.licenseUrl }
        return 'NOASSERTION'
    }
    catch {
        return 'NOASSERTION'
    }
}

$resolved = [ordered]@{}
foreach ($lockFile in $lockFiles) {
    $lock = Get-Content -LiteralPath $lockFile.FullName -Raw | ConvertFrom-Json
    foreach ($framework in $lock.dependencies.PSObject.Properties) {
        foreach ($dependency in $framework.Value.PSObject.Properties) {
            $entry = $dependency.Value
            if (-not $entry.resolved) { continue }
            $key = "$($dependency.Name)/$($entry.resolved)"
            if ($resolved.Contains($key)) { continue }
            $resolved[$key] = [pscustomobject]@{
                name        = $dependency.Name
                version     = [string]$entry.resolved
                contentHash = [string]$entry.contentHash
                licence     = Get-DeclaredLicence -Id $dependency.Name -PackageVersion ([string]$entry.resolved)
            }
        }
    }
}

$components = @($resolved.Values | Sort-Object name, version)
Write-Output "Resolved $($components.Count) package(s) from $($lockFiles.Count) lock file(s)."

$cyclone = [ordered]@{
    bomFormat    = 'CycloneDX'
    specVersion  = '1.5'
    serialNumber = "urn:uuid:$([guid]::new($commit.Substring(0, 32)))"
    version      = 1
    metadata     = [ordered]@{
        timestamp = $commitDate
        component = [ordered]@{
            type    = 'application'
            name    = 'AP Reelume'
            version = $Version
            purl    = "pkg:generic/APSolutions.LocalMedia@$Version"
            # `name`, not `id`: CycloneDX only accepts an SPDX list identifier in `id`, and the
            # program's own licence is not on that list. Declaring it there produces a document a
            # strict validator rejects, which is worse than the plain name a reader can act on.
            licenses = @(@{ license = @{ name = $ownLicenceName } })
        }
        properties = @(
            [ordered]@{ name = 'apsolutions:commit'; value = $commit },
            [ordered]@{ name = 'apsolutions:runtime'; value = 'win-x64' },
            [ordered]@{ name = 'apsolutions:signed'; value = 'false' }
        )
    }
    components   = @($components | ForEach-Object {
            $component = [ordered]@{
                type    = 'library'
                name    = $_.name
                version = $_.version
                purl    = "pkg:nuget/$($_.name)@$($_.version)"
            }
            if ($_.contentHash) {
                # NuGet publishes the content hash as base64 SHA-512; CycloneDX wants it in hex.
                $component['hashes'] = @(@{
                        alg     = 'SHA-512'
                        content = [BitConverter]::ToString([Convert]::FromBase64String($_.contentHash)).Replace('-', '').ToLowerInvariant()
                    })
            }
            $component['licenses'] = @(
                if ($_.licence -eq 'NOASSERTION') { @{ license = @{ name = 'NOASSERTION' } } }
                elseif ($_.licence -like 'http*') { @{ license = @{ name = $_.licence } } }
                else { @{ license = @{ id = $_.licence } } }
            )
            $component
        })
}

$spdx = [ordered]@{
    spdxVersion       = 'SPDX-2.3'
    dataLicense       = 'CC0-1.0'
    SPDXID            = 'SPDXRef-DOCUMENT'
    name              = "APSolutions.LocalMedia-$Version-win-x64"
    documentNamespace = "https://github.com/apsolutions/localmedia/sbom/$commit"
    creationInfo      = [ordered]@{
        created  = $commitDate
        creators = @('Tool: eng/generate-sbom.ps1', 'Organization: AP Solutions')
    }
    packages          = @(
        [ordered]@{
            SPDXID           = 'SPDXRef-Package-APReelume'
            name             = 'AP Reelume'
            versionInfo      = $Version
            downloadLocation = 'NOASSERTION'
            filesAnalyzed    = $false
            licenseConcluded = $ownLicenceId
            licenseDeclared  = $ownLicenceId
        }
    ) + @($components | ForEach-Object {
            $package = [ordered]@{
                SPDXID           = "SPDXRef-Package-$($_.name -replace '[^A-Za-z0-9.\-]', '-')-$($_.version -replace '[^A-Za-z0-9.\-]', '-')"
                name             = $_.name
                versionInfo      = $_.version
                downloadLocation = "https://www.nuget.org/api/v2/package/$($_.name)/$($_.version)"
                filesAnalyzed    = $false
                licenseConcluded = 'NOASSERTION'
                licenseDeclared  = $_.licence
            }
            if ($_.contentHash) {
                $package['checksums'] = @(@{
                        algorithm     = 'SHA512'
                        checksumValue = [BitConverter]::ToString([Convert]::FromBase64String($_.contentHash)).Replace('-', '').ToLowerInvariant()
                    })
            }
            $package
        })
    # SPDX 2.3 §10: a document that names a `LicenseRef-` has to carry its text, or a reader is left
    # with an identifier that resolves to nothing. This is the section that makes the reference real.
    hasExtractedLicensingInfos = @(
        [ordered]@{
            licenseId   = $ownLicenceId
            name        = $ownLicenceName
            extractedText = $ownLicenceText
        }
    )
}

$cyclonePath = Join-Path $outputRoot 'sbom.cyclonedx.json'
$spdxPath = Join-Path $outputRoot 'sbom.spdx.json'
Set-Content -LiteralPath $cyclonePath -Value ($cyclone | ConvertTo-Json -Depth 12) -Encoding utf8NoBOM
Set-Content -LiteralPath $spdxPath -Value ($spdx | ConvertTo-Json -Depth 12) -Encoding utf8NoBOM

Write-Output "CycloneDX: $cyclonePath"
Write-Output "SPDX: $spdxPath"
