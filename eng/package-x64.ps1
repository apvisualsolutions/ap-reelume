# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Builds the Windows x64 MSIX and the independent ZIP from the current checkout.

.DESCRIPTION
    There is no .wapproj here. Building one needs Microsoft.DesktopBridge.targets, which ships with a
    Visual Studio workload rather than with the .NET SDK, so a packaging project would be a file this
    repository could not build and no test could verify. The layout is assembled here instead and
    sealed with MakeAppx from the Windows SDK; ADR-0004 records the decision.

    Two things this script does that a default publish does not:

    - It removes the LibVLC payloads for the architectures this package does not target. A
      self-contained win-x64 publish of this application ships win-x86 and win-arm64 as well, which
      is two thirds of half a gigabyte the loader will never open.
    - It carries the licence and the third-party notices inside the payload, in both languages,
      because that is a condition of shipping the binary rather than a nicety of the download page.

    Nothing here signs anything. The artifact is unsigned on purpose and says so in its own report;
    docs/release/SMARTSCREEN.es.md explains what that means for whoever downloads it.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Output = 'artifacts/package',

    # The reproducibility comparison needs a payload and nothing else; sealing it twice would double
    # the slowest part of the run for a file it then throws away.
    [switch]$LayoutOnly
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputRoot = [IO.Path]::GetFullPath($Output, $repoRoot)
$layoutRoot = Join-Path $outputRoot 'layout'
$projectPath = Join-Path $repoRoot 'src/ApSolutions.LocalMedia.Windows/ApSolutions.LocalMedia.Windows.csproj'
$packageProject = Join-Path $repoRoot 'src/ApSolutions.LocalMedia.Windows.Package'

# The one place the release version is read from.
[xml]$buildProps = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = ([string]$buildProps.Project.PropertyGroup.Version).Trim()
if (-not $version) { throw 'Directory.Build.props declares no <Version>.' }
$packageVersion = "$version.0"

$msixName = "APSolutions.LocalMedia_${version}_x64.msix"
$zipName = "ApReelume-${version}-win-x64.zip"

function Get-MakeAppx {
    # @() because a machine with exactly one SDK yields a bare string, and indexing a string
    # returns its first character — the packaging step then tries to run a program called 'C'.
    $candidates = @(Get-ChildItem -LiteralPath 'C:/Program Files (x86)/Windows Kits/10/bin' -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^10\.' } |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64/makeappx.exe' } |
        Where-Object { Test-Path -LiteralPath $_ })
    if (-not $candidates) {
        throw 'MakeAppx.exe was not found. Install the Windows 10/11 SDK to seal the package.'
    }

    return $candidates[0]
}

Push-Location $repoRoot
try {
    if (Test-Path -LiteralPath $outputRoot) { Remove-Item -LiteralPath $outputRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $layoutRoot | Out-Null

    Write-Output "Publishing $version (win-x64, $Configuration) …"
    # Two builds of one commit have to produce one payload, and a payload that embeds the directory
    # it was built in cannot. Each build maps its own checkout to the same placeholder, so two
    # checkouts in two directories compile to the same bytes. It is applied here rather than in
    # Directory.Build.props because a repository-wide map leaves coverlet unable to find the sources
    # its own instrumentation points at, and the coverage report comes back empty.
    dotnet publish $projectPath `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -p:Version=$version `
        -p:PathMap="$repoRoot=/_/" `
        -p:NuGetLockFilePath="obj/packages.win-x64.lock.json" `
        -o $layoutRoot
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    # LibVLC arrives for three architectures whatever the runtime identifier says.
    $strays = @(Get-ChildItem -LiteralPath $layoutRoot -Recurse -Directory |
        Where-Object { $_.Name -in @('win-x86', 'win-arm64', 'win-arm') })
    foreach ($stray in $strays) {
        if (Test-Path -LiteralPath $stray.FullName) {
            Remove-Item -LiteralPath $stray.FullName -Recurse -Force
            Write-Output "Removed foreign runtime payload: $([IO.Path]::GetRelativePath($layoutRoot, $stray.FullName))"
        }
    }

    # Symbols are not distributed, so anything the publish left behind goes.
    Get-ChildItem -LiteralPath $layoutRoot -Recurse -File -Filter '*.pdb' | Remove-Item -Force

    Write-Output 'Carrying the licence and the notices into the payload …'
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $layoutRoot 'LICENSE') -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'NOTICE') -Destination (Join-Path $layoutRoot 'NOTICE') -Force
    $licenceRoot = Join-Path $layoutRoot 'licenses'
    New-Item -ItemType Directory -Force -Path $licenceRoot | Out-Null
    foreach ($language in @('es', 'en')) {
        Copy-Item `
            -LiteralPath (Join-Path $repoRoot "docs/release/THIRD-PARTY-NOTICES.$language.md") `
            -Destination (Join-Path $licenceRoot "THIRD-PARTY-NOTICES.$language.md") `
            -Force
    }

    Write-Output 'Writing the bill of materials …'
    & (Join-Path $PSScriptRoot 'generate-sbom.ps1') -Output (Join-Path $outputRoot 'sbom') -Version $version
    if ($LASTEXITCODE -ne 0) { throw 'SBOM generation failed.' }
    Copy-Item -Path (Join-Path $outputRoot 'sbom') -Destination (Join-Path $layoutRoot 'sbom') -Recurse -Force

    Write-Output 'Placing the manifest and the tile images …'
    Copy-Item -Path (Join-Path $packageProject 'Assets') -Destination (Join-Path $layoutRoot 'Assets') -Recurse -Force
    $manifestSource = Join-Path $packageProject 'Package.appxmanifest'
    [xml]$manifest = Get-Content -LiteralPath $manifestSource -Raw
    if ($manifest.Package.Identity.Version -ne $packageVersion) {
        throw "Package.appxmanifest declares $($manifest.Package.Identity.Version) but the build is $packageVersion."
    }

    Copy-Item -LiteralPath $manifestSource -Destination (Join-Path $layoutRoot 'AppxManifest.xml') -Force

    $layoutFiles = @(Get-ChildItem -LiteralPath $layoutRoot -Recurse -File)
    $layoutNames = @($layoutFiles | ForEach-Object {
            [IO.Path]::GetRelativePath($layoutRoot, $_.FullName).Replace('\', '/')
        } | Sort-Object)
    Write-Output "Layout: $($layoutFiles.Count) file(s), $([Math]::Round((($layoutFiles | Measure-Object Length -Sum).Sum / 1MB), 1)) MB."

    if ($LayoutOnly) {
        Write-Output "Layout only: $layoutRoot"
        return
    }

    Write-Output 'Sealing the package …'
    $makeAppx = Get-MakeAppx
    $msixPath = Join-Path $outputRoot $msixName
    # No /nv. That flag skips the validation that decides whether Windows could install the package —
    # the manifest's protocols and file type associations, and whether every declared file is there —
    # and with the real install cycle blocked for want of a clean machine and a certificate, this is
    # the closest thing to an install that can be run here. Throwing it away would leave the
    # artifact's installability resting on nothing.
    & $makeAppx pack /d $layoutRoot /p $msixPath /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx failed to seal the package, or the package would not install on Windows.' }

    Write-Output 'Writing the independent archive …'
    $zipPath = Join-Path $outputRoot $zipName
    Compress-Archive -Path (Join-Path $layoutRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

    # What Windows will read, extracted so a test can read it too.
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $packagedRoot = Join-Path $outputRoot 'packaged'
    New-Item -ItemType Directory -Force -Path $packagedRoot | Out-Null
    $archive = [IO.Compression.ZipFile]::OpenRead($msixPath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName })
        $manifestEntry = $archive.GetEntry('AppxManifest.xml')
        if (-not $manifestEntry) { throw 'The sealed package has no AppxManifest.xml.' }
        [IO.Compression.ZipFileExtensions]::ExtractToFile(
            $manifestEntry, (Join-Path $packagedRoot 'AppxManifest.xml'), $true)
    }
    finally {
        $archive.Dispose()
    }

    # MakeAppx adds the OPC bookkeeping every package carries. It is named rather than ignored.
    $overhead = @('[Content_Types].xml', 'AppxBlockMap.xml', 'AppxSignature.p7x')
    $packageNames = @($entryNames | Where-Object {
            $_ -notin $overhead -and -not $_.StartsWith('AppxMetadata/')
        } | Sort-Object)

    $onlyInLayout = @($layoutNames | Where-Object { $_ -notin $packageNames })
    $onlyInPackage = @($packageNames | Where-Object { $_ -notin $layoutNames })

    Write-Output 'Scanning the payload for anything that should not travel …'
    $secretPatterns = @(
        'ghp_[A-Za-z0-9]{36}',
        'github_pat_[A-Za-z0-9_]{20,}',
        '-----BEGIN [A-Z ]*PRIVATE KEY-----',
        'AP_LOCALMEDIA_TMDB_TOKEN\s*=\s*\S',
        'Bearer\s+[A-Za-z0-9._~+/-]{20,}',
        '(?i)api[_-]?key["'':=\s]+[A-Za-z0-9]{16,}'
    )
    $textLike = @('.json', '.txt', '.md', '.xml', '.config', '.appxmanifest', '.ps1')
    $scannable = @($layoutFiles | Where-Object {
            $_.Name.StartsWith('ApSolutions.') -or ($textLike -contains $_.Extension.ToLowerInvariant())
        })
    $secretHits = @()
    foreach ($file in $scannable) {
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $text = [Text.Encoding]::ASCII.GetString($bytes)
        foreach ($pattern in $secretPatterns) {
            if ([regex]::IsMatch($text, $pattern)) {
                $secretHits += "$([IO.Path]::GetRelativePath($layoutRoot, $file.FullName)) matches $pattern"
            }
        }
    }

    # A dependency that ships and is not in the bill of materials is the one an audit will not find.
    $sbom = Get-Content -LiteralPath (Join-Path $outputRoot 'sbom/sbom.cyclonedx.json') -Raw | ConvertFrom-Json
    $listed = @($sbom.components | ForEach-Object { "$($_.name)/$($_.version)" })
    $resolvedPackages = @()
    foreach ($lockFile in @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Recurse -Filter 'packages.lock.json' -File)) {
        $lock = Get-Content -LiteralPath $lockFile.FullName -Raw | ConvertFrom-Json
        foreach ($framework in $lock.dependencies.PSObject.Properties) {
            foreach ($dependency in $framework.Value.PSObject.Properties) {
                if ($dependency.Value.resolved) {
                    $resolvedPackages += "$($dependency.Name)/$($dependency.Value.resolved)"
                }
            }
        }
    }

    $sbomGaps = @($resolvedPackages | Sort-Object -Unique | Where-Object { $_ -notin $listed })

    $sums = [Collections.Generic.List[string]]::new()
    foreach ($artifact in @($msixName, $zipName)) {
        $hash = (Get-FileHash -LiteralPath (Join-Path $outputRoot $artifact) -Algorithm SHA256).Hash.ToLowerInvariant()
        $sums.Add("$hash  $artifact")
    }

    # Line feeds and a trailing one, no BOM: these exact bytes are what the release signature is
    # made over and what the updater reconstructs from the notes to verify it (SEC-003). CRLF here
    # would be a second, invisible variable in a signature check.
    $sumsPath = Join-Path $outputRoot 'SHA256SUMS.txt'
    [IO.File]::WriteAllText($sumsPath, (($sums -join "`n") + "`n"))

    # The signature travels with every release; the key never travels with the repository. The
    # release workflow signs with its secret, the owner signs with the guarded local copy, and a
    # build with neither stays honest: it says so, and prepare-release blocks on it.
    $signingSource =
        if ($env:RELEASE_SIGNING_SECRET_KEY) { 'RELEASE_SIGNING_SECRET_KEY' }
        elseif ($env:RELEASE_SIGNING_KEY_FILE -and (Test-Path -LiteralPath $env:RELEASE_SIGNING_KEY_FILE)) { $env:RELEASE_SIGNING_KEY_FILE }
        else { $null }
    if ($signingSource) {
        Write-Output 'Signing the checksums …'
        dotnet run --project (Join-Path $repoRoot 'eng/tools/ReleaseSigning') -- sign $sumsPath $signingSource
        if ($LASTEXITCODE -ne 0) { throw 'Signing the checksums failed.' }
        dotnet run --project (Join-Path $repoRoot 'eng/tools/ReleaseSigning') -- verify $sumsPath "$sumsPath.minisig" (Join-Path $repoRoot 'eng/release-signing.pub')
        if ($LASTEXITCODE -ne 0) { throw 'The fresh signature does not verify against the embedded public key.' }
    }
    else {
        Write-Output 'No signing key is reachable: SHA256SUMS.txt travels unsigned. prepare-release blocks a release like this.'
    }

    $contents = [ordered]@{
        version         = $version
        packageVersion  = $packageVersion
        runtime         = 'win-x64'
        configuration   = $Configuration
        commit          = (git -C $repoRoot rev-parse HEAD).Trim()
        msix            = $msixName
        zip             = $zipName
        packagedManifest = 'packaged/AppxManifest.xml'
        layoutFileCount = $layoutFiles.Count
        layoutBytes     = ($layoutFiles | Measure-Object Length -Sum).Sum
        onlyInLayout    = $onlyInLayout
        onlyInPackage   = $onlyInPackage
        packageOverhead = @($entryNames | Where-Object { $_ -in $overhead -or $_.StartsWith('AppxMetadata/') } | Sort-Object)
        removedForeignRuntimes = @($strays | ForEach-Object { $_.Name } | Sort-Object -Unique)
        secretScan      = [ordered]@{
            filesScanned = $scannable.Count
            hits         = $secretHits.Count
            matches      = $secretHits
        }
        sbomGaps        = $sbomGaps
        signed          = $false
        # MakeAppx sealed this without /nv, so it ran the checks that decide whether Windows could
        # install the package. It is not an install; it is what can be verified without one.
        semanticValidation = $true
        signingNote     = 'No certificate is used. Windows SmartScreen will warn on first run; docs/release/SMARTSCREEN.es.md explains why and what to check instead.'
    }

    Set-Content `
        -LiteralPath (Join-Path $outputRoot 'contents.json') `
        -Value ($contents | ConvertTo-Json -Depth 8) `
        -Encoding utf8NoBOM

    # The notes travel with the artifact because the updater reads them: a release whose body does
    # not carry the bilingual summary and the published hash is one the application refuses. They are
    # generated here rather than written by hand at publication time, when nobody is checking.
    & (Join-Path $PSScriptRoot 'build-release-notes.ps1') -PackageRoot $outputRoot
    if ($LASTEXITCODE -ne 0) { throw 'Release notes generation failed.' }

    # The package manager entry, generated from this archive rather than typed. winget is the one
    # channel that needs no certificate and no store account, so its manifest is part of what a
    # release produces instead of something somebody assembles afterwards from memory.
    & (Join-Path $PSScriptRoot 'build-winget-manifest.ps1') -PackageRoot $outputRoot
    if ($LASTEXITCODE -ne 0) { throw 'winget manifest generation failed.' }

    Write-Output ''
    Write-Output "MSIX: $msixPath"
    Write-Output "ZIP:  $zipPath"
    Write-Output "Layout files: $($layoutFiles.Count); SBOM gaps: $($sbomGaps.Count); secret hits: $($secretHits.Count)."
    if ($onlyInLayout.Count -gt 0 -or $onlyInPackage.Count -gt 0) {
        throw "The package and the layout disagree: $($onlyInLayout.Count) verified-not-shipped, $($onlyInPackage.Count) shipped-not-verified."
    }
}
finally {
    Pop-Location
}
