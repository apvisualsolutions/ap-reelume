# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Builds the Windows ARM64 MSIX and the independent ZIP from the current checkout.

.DESCRIPTION
    The ARM64 counterpart of eng/package-x64.ps1, and deliberately a separate file rather than a
    parameter on that one: the x64 script is what the approved MVP gate rests on, and generalising it
    would have put every x64 guarantee at risk to add an architecture. What the two must agree on is
    checked instead — Arm64PackageTests compares the payloads and the manifests — so a drift between
    them fails the suite rather than shipping.

    Three things here are not in the x64 script:

    - The manifest is rewritten as it is laid out. Package.appxmanifest is the single canonical
      manifest and declares x64; the architecture is the one attribute that legitimately differs, and
      writing a second manifest file would be two files to keep identical forever.
    - The payload is compared with the x64 layout, when one is present, so "the same application on
      both architectures" is a measured claim rather than an intention.
    - A matrix report is written that says, for each thing only ARM64 hardware can answer, whether it
      was answered or blocked. On an x64 machine everything in it is blocked, with a reason.

    Nothing here signs anything. The artifact is unsigned on purpose and says so in its own report.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Output = 'artifacts/package-arm64',

    # Where the x64 layout is, for the parity comparison. Absent is not an error; it is recorded.
    [string]$X64Layout = 'artifacts/package/layout',

    # A data folder written by the x64 build, carried here to prove an export survives the move
    # between architectures. Absent leaves that phase blocked, on an ARM64 machine as well.
    [string]$X64DataRoot = '',

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

[xml]$buildProps = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
$version = ([string]$buildProps.Project.PropertyGroup.Version).Trim()
if (-not $version) { throw 'Directory.Build.props declares no <Version>.' }
$packageVersion = "$version.0"

$msixName = "APSolutions.LocalMedia_${version}_arm64.msix"
$zipName = "ApReelume-${version}-win-arm64.zip"

# What only an ARM64 machine can answer. Every one of them is either a result from such a machine or
# a block that says why, and the suite refuses anything else.
$matrixPhases = @(
    @{ id = 'native-execution'; question = 'The published ARM64 host starts and reports ARM64.' }
    @{ id = 'codec-matrix'; question = 'The T19 container and codec matrix decodes natively on ARM64.' }
    @{ id = 'hdr-acceleration'; question = 'HDR10 detection, tone mapping, and the decode path on ARM64.' }
    @{ id = 'audio-output'; question = 'Device selection, hot switching, and the persisted preference on ARM64.' }
    @{ id = 'package-lifecycle'; question = 'The T40 install cycle against the ARM64 package.' }
    @{ id = 'cross-architecture-data'; question = 'A library exported on x64 opens on ARM64 without loss.' }
)

<#
    Starts the published host the way an installed copy starts, waits for a window, and closes it the
    way a person would. A run that has to be killed is a failed run. This only ever executes on an
    ARM64 machine: on anything else the binary it points at cannot run at all.
#>
function Invoke-NativeHost {
    param(
        [Parameter(Mandatory)][string]$InstallRoot,
        [Parameter(Mandatory)][string]$DataRoot,
        [int]$WindowTimeoutSeconds = 90,
        [int]$SettleSeconds = 6,
        [int]$ExitTimeoutSeconds = 45
    )

    New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null
    foreach ($name in @(
            'CORECLR_ENABLE_PROFILING', 'CORECLR_PROFILER', 'CORECLR_PROFILER_PATH',
            'CORECLR_PROFILER_PATH_32', 'CORECLR_PROFILER_PATH_64',
            'COR_ENABLE_PROFILING', 'COR_PROFILER', 'COR_PROFILER_PATH')) {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }

    $env:AP_LOCALMEDIA_DATA_ROOT = $DataRoot
    Remove-Item 'Env:AP_LOCALMEDIA_TMDB_TOKEN' -ErrorAction SilentlyContinue

    $exe = Join-Path $InstallRoot 'ApSolutions.LocalMedia.Windows.exe'
    try {
        $process = Start-Process -FilePath $exe -PassThru
        $deadline = [datetime]::UtcNow.AddSeconds($WindowTimeoutSeconds)
        $windowShown = $false
        while ([datetime]::UtcNow -lt $deadline) {
            if ($process.HasExited) { break }
            $process.Refresh()
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $windowShown = $true; break }
            Start-Sleep -Milliseconds 250
        }

        if ($windowShown) { Start-Sleep -Seconds $SettleSeconds }

        $closedPolitely = $false
        if (-not $process.HasExited) {
            $closedPolitely = $process.CloseMainWindow()
            if (-not $process.WaitForExit($ExitTimeoutSeconds * 1000)) {
                $process.Kill($true)
                $process.WaitForExit(15000) | Out-Null
                $closedPolitely = $false
            }
        }

        return [pscustomobject]@{
            windowShown    = $windowShown
            closedPolitely = $closedPolitely
            exitCode       = $process.ExitCode
        }
    }
    finally {
        Remove-Item 'Env:AP_LOCALMEDIA_DATA_ROOT' -ErrorAction SilentlyContinue
    }
}

<#
    Reads what a run actually executed out of its TRX.

    The counters come from the XML rather than the console summary because the summary is localised:
    this repository is developed on an es-ES machine and CI runs en-US, so matching 'Passed!' or
    'Superado:' asks a question about the runner's language instead of about the tests.

    A skipped test is counted as the difference between `total` and `executed`, not read from an
    attribute, because a dynamic skip and a statically disabled test are recorded under different
    ones and both mean the same thing here: nobody ran it.
#>
function Read-TrxCounters {
    param([Parameter(Mandatory)][string]$ResultsDirectory)

    if (-not (Test-Path -LiteralPath $ResultsDirectory)) { return $null }
    $trx = Get-ChildItem -LiteralPath $ResultsDirectory -Filter '*.trx' -Recurse -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc | Select-Object -Last 1
    if (-not $trx) { return $null }

    $counters = ([xml](Get-Content -LiteralPath $trx.FullName -Raw)).TestRun.ResultSummary.Counters
    if (-not $counters) { return $null }

    $total = [int]$counters.total
    return [pscustomobject]@{
        total   = $total
        passed  = [int]$counters.passed
        failed  = [int]$counters.failed
        skipped = $total - [int]$counters.executed
    }
}

<#
    Runs one media suite and reports what it observed. The suites are the verification: they open real
    files with the engine that shipped, so running them on ARM64 is what makes the matrix a
    measurement rather than a restatement of the build succeeding.

    A ZERO EXIT CODE IS NOT ENOUGH to call the phase passed, and that is the whole reason this reads
    the counters. CodecMatrixTests and HdrAccelerationTests call Assert.SkipWhen when ffmpeg is
    absent, so on a machine without it every test skips, `dotnet test` returns 0, and the phase would
    have been recorded as Passed with its detail filled in — without a single frame being decoded. A
    gate that passes by looking at nothing, inside the gate written to measure the hardware.

    WHERE THE LINE IS, AND WHY IT IS NOT "no skips", measured on 2026-09-04 by the first native run.
    The first draft refused any suite carrying a skipped test, which would have made this phase
    unpassable on any hosted machine for a reason with nothing to do with ARM64: Chocolatey's ffmpeg
    package carries neither libsvtav1 nor libxavs2, so two samples are never generated, and one more
    test skips because that build muxes the HDR sample without its colour-transfer metadata. THE X64
    RUNNER SKIPS THE SAME ONES: five in this suite, read from the run of 743af9a. The gap belongs to
    the ffmpeg package and is identical on both architectures, so tying PRD-003's unblocking to
    Chocolatey shipping an AV1 encoder would be a bar nobody can reach and nobody chose.

    So the line is that SOMETHING RAN AND PASSED: a suite that executed nothing measured nothing,
    whatever its exit code says. Skips are recorded in the detail either way, so nobody reads
    "Passed" without also reading how much of the question went unanswered.

    A suite that could not measure returns a reason, so the phase is recorded as Blocked rather than
    Failed: the distinction is what tells a missing tool apart from ARM64 decoding a file wrongly.
#>
function Invoke-MediaSuite {
    param(
        [Parameter(Mandatory)][string]$Filter,
        [Parameter(Mandatory)][string]$ResultsDirectory
    )

    $project = Join-Path $repoRoot 'tests/ApSolutions.LocalMedia.MediaTests'
    # Captured and then written to the host rather than piped onward. `| Write-Output` puts every
    # line of dotnet test into THIS FUNCTION'S output, so the caller receives an array whose last
    # element is the report, and `$run.PSObject.Properties.Name -contains 'reason'` then asks the
    # array about its own properties and answers no. Measured on the first native run: two phases
    # that carried a reason were recorded as Failed with the reason dropped.
    $output = & dotnet test $project -c $Configuration -m:1 `
        --settings (Join-Path $PSScriptRoot 'test.runsettings') `
        --filter $Filter `
        --logger 'trx;LogFileName=results.trx' `
        --results-directory $ResultsDirectory 2>&1
    $exitCode = $LASTEXITCODE
    foreach ($line in $output) { Write-Host $line }

    $counters = Read-TrxCounters -ResultsDirectory $ResultsDirectory
    if ($null -eq $counters) {
        return [pscustomobject]@{
            succeeded = $false
            summary   = 'No test results were produced.'
            reason    = "The suite matching '$Filter' left no TRX in $ResultsDirectory, so nothing was measured. Exit code $exitCode."
        }
    }

    $summary = "$($counters.passed) passed, $($counters.failed) failed, $($counters.skipped) skipped of $($counters.total); exit code $exitCode."
    if ($counters.passed -eq 0) {
        return [pscustomobject]@{
            succeeded = $false
            summary   = $summary
            reason    = "The suite matching '$Filter' executed nothing that passed, so it measured nothing: $summary The usual cause is a missing tool: CodecMatrixTests and HdrAccelerationTests skip themselves when ffmpeg is absent."
        }
    }

    return [pscustomobject]@{
        succeeded = $exitCode -eq 0 -and $counters.failed -eq 0
        summary   = $summary
    }
}

<#
    Splits a payload into the part that has to be identical across architectures and the part that
    cannot be.

    The application is the first: its assemblies are architecture-neutral, so a name present on one
    architecture and missing on the other is a capability that exists on one machine only, and that is
    what parity has to catch.

    The second is native third-party code. VideoLAN compiles a different plugin set per architecture —
    the x86 SIMD chroma converters, the OpenGL outputs, and Intel's Quick Sync decoder have no ARM64
    build — and the .NET runtime names some of its own files after the architecture. Demanding those
    match would demand VideoLAN ship code for an instruction set that does not exist. They are listed
    instead of ignored, because which ones are missing is a real difference in what the two packages
    can do.
#>
function Split-PayloadNames {
    param([string]$Root)

    $application = [Collections.Generic.List[string]]::new()
    $native = [Collections.Generic.List[string]]::new()

    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        $folded = $relative -replace '(^|/)(libvlc|runtimes)/win-(x64|arm64)(/|$)', '$1$2/<arch>$4'

        if ($folded -like 'libvlc/*' -or $file.Name -match '(^|[._-])(amd64|arm64|x64|x86)([._-]|$)') {
            $native.Add($folded)
        }
        else {
            $application.Add($folded)
        }
    }

    return [pscustomobject]@{
        Application = @($application | Sort-Object -Unique)
        Native      = @($native | Sort-Object -Unique)
    }
}

Push-Location $repoRoot
try {
    if (Test-Path -LiteralPath $outputRoot) { Remove-Item -LiteralPath $outputRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $layoutRoot | Out-Null

    Write-Output "Publishing $version (win-arm64, $Configuration) …"
    # PathMap is applied here and not repository-wide: a global map leaves coverlet unable to find the
    # sources its instrumentation points at, and the coverage report comes back empty.
    dotnet publish $projectPath `
        -c $Configuration `
        -r win-arm64 `
        --self-contained true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -p:Version=$version `
        -p:PathMap="$repoRoot=/_/" `
        -p:NuGetLockFilePath="obj/packages.win-arm64.lock.json" `
        -o $layoutRoot
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    # LibVLC arrives for three architectures whatever the runtime identifier says, because its targets
    # file selects on $(Platform) rather than on the runtime identifier.
    $strays = @(Get-ChildItem -LiteralPath $layoutRoot -Recurse -Directory |
        Where-Object { $_.Name -in @('win-x86', 'win-x64', 'win-arm') })
    foreach ($stray in $strays) {
        if (Test-Path -LiteralPath $stray.FullName) {
            Remove-Item -LiteralPath $stray.FullName -Recurse -Force
            Write-Output "Removed foreign runtime payload: $([IO.Path]::GetRelativePath($layoutRoot, $stray.FullName))"
        }
    }

    Get-ChildItem -LiteralPath $layoutRoot -Recurse -File -Filter '*.pdb' | Remove-Item -Force

    Write-Output 'Carrying the licence, the notices and the licence texts into the payload …'
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

    # Same as the x64 script, and for the same reason: the obligation belongs to shipping a binary,
    # not to one architecture's build. The payload parity check compares the two file lists, so an
    # omission here would surface as a difference rather than as a quiet gap.
    Copy-Item -Path (Join-Path $repoRoot 'docs/release/licenses/*') -Destination $licenceRoot -Recurse -Force

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

    # The one attribute that legitimately differs between the two packages. Everything else — identity,
    # publisher, capabilities, the file type list, the virtualisation switches that keep an uninstall
    # from deleting the library — is the same file, and a test compares them.
    if ($manifest.Package.Identity.ProcessorArchitecture -ne 'x64') {
        throw "Package.appxmanifest declares $($manifest.Package.Identity.ProcessorArchitecture); the ARM64 rewrite expects x64."
    }

    $manifest.Package.Identity.ProcessorArchitecture = 'arm64'
    $manifest.Save((Join-Path $layoutRoot 'AppxManifest.xml'))

    # Built from the ARM64 manifest rather than copied from the x64 build, for the same reason the
    # manifest is rewritten here: a resource file carrying another package's identity resolves to
    # nothing. The bytes come out identical anyway, because the identity name is what the two share.
    Write-Output 'Building the described resources …'
    $resources = & (Join-Path $PSScriptRoot 'build-package-resources.ps1') `
        -ManifestPath (Join-Path $layoutRoot 'AppxManifest.xml') `
        -StageRoot (Join-Path $outputRoot 'pri/stage') `
        -RepoRoot $repoRoot
    Copy-Item -LiteralPath $resources.Path -Destination (Join-Path $layoutRoot 'resources.pri') -Force
    Write-Output "Described in $(@($resources.Described | ForEach-Object { $_.language }) -join ', ') under $($resources.MapName)."

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
    $makeAppx = & (Join-Path $PSScriptRoot 'find-sdk-tool.ps1') -Name 'makeappx.exe'
    $msixPath = Join-Path $outputRoot $msixName
    # No /nv, for the same reason as x64: that flag skips the validation that decides whether Windows
    # could install the package, and throwing it away would leave installability resting on nothing.
    & $makeAppx pack /d $layoutRoot /p $msixPath /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx failed to seal the package, or the package would not install on Windows.' }

    Write-Output 'Writing the independent archive …'
    $zipPath = Join-Path $outputRoot $zipName
    Compress-Archive -Path (Join-Path $layoutRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

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

    Write-Output 'Comparing the payload with the x64 one …'
    $x64LayoutPath = [IO.Path]::GetFullPath($X64Layout, $repoRoot)
    $parity = if (Test-Path -LiteralPath $x64LayoutPath) {
        $ours = Split-PayloadNames -Root $layoutRoot
        $theirs = Split-PayloadNames -Root $x64LayoutPath
        [ordered]@{
            compared          = $true
            reason            = ''
            x64Layout         = [IO.Path]::GetRelativePath($repoRoot, $x64LayoutPath).Replace('\', '/')
            onlyInX64         = @($theirs.Application | Where-Object { $_ -notin $ours.Application })
            onlyInArm64       = @($ours.Application | Where-Object { $_ -notin $theirs.Application })
            nativeOnlyInX64   = @($theirs.Native | Where-Object { $_ -notin $ours.Native })
            nativeOnlyInArm64 = @($ours.Native | Where-Object { $_ -notin $theirs.Native })
        }
    }
    else {
        [ordered]@{
            compared          = $false
            reason            = "The x64 layout is not at $X64Layout, so the two payloads were not compared. Run eng/package-x64.ps1 first."
            x64Layout         = $X64Layout
            onlyInX64         = @()
            onlyInArm64       = @()
            nativeOnlyInX64   = @()
            nativeOnlyInArm64 = @()
        }
    }

    $sums = [Collections.Generic.List[string]]::new()
    foreach ($artifact in @($msixName, $zipName)) {
        $hash = (Get-FileHash -LiteralPath (Join-Path $outputRoot $artifact) -Algorithm SHA256).Hash.ToLowerInvariant()
        $sums.Add("$hash  $artifact")
    }

    Set-Content -LiteralPath (Join-Path $outputRoot 'SHA256SUMS.txt') -Value $sums -Encoding utf8NoBOM

    $contents = [ordered]@{
        version          = $version
        packageVersion   = $packageVersion
        runtime          = 'win-arm64'
        configuration    = $Configuration
        commit           = (git -C $repoRoot rev-parse HEAD).Trim()
        msix             = $msixName
        zip              = $zipName
        packagedManifest = 'packaged/AppxManifest.xml'
        layoutFileCount  = $layoutFiles.Count
        layoutBytes      = ($layoutFiles | Measure-Object Length -Sum).Sum
        onlyInLayout     = $onlyInLayout
        onlyInPackage    = $onlyInPackage
        packageOverhead  = @($entryNames | Where-Object { $_ -in $overhead -or $_.StartsWith('AppxMetadata/') } | Sort-Object)
        removedForeignRuntimes = @($strays | ForEach-Object { $_.Name } | Sort-Object -Unique)
        parityWithX64    = $parity
        secretScan       = [ordered]@{
            filesScanned = $scannable.Count
            hits         = $secretHits.Count
            matches      = $secretHits
        }
        sbomGaps         = $sbomGaps
        signed           = $false
        semanticValidation = $true
        signingNote      = 'No certificate is used. Windows SmartScreen will warn on first run; docs/release/SMARTSCREEN.es.md explains why and what to check instead.'
    }

    Set-Content `
        -LiteralPath (Join-Path $outputRoot 'contents.json') `
        -Value ($contents | ConvertTo-Json -Depth 8) `
        -Encoding utf8NoBOM

    # The matrix. Cross-building an ARM64 package proves the package; it proves nothing about running
    # it, so every phase that needs the hardware is blocked here and says so by name.
    $hostArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    $processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    $isArm64Host = $hostArchitecture -eq 'Arm64'
    $blockedReason = "This build ran on a $hostArchitecture host. The phase needs a Windows 11 ARM64 machine, and emulating one would answer a different question: what is being verified is that native ARM64 code runs."

    $observed = @{}
    if ($isArm64Host) {
        Write-Output 'Running the ARM64 matrix on this machine …'
        $matrixResults = Join-Path $outputRoot 'matrix'
        New-Item -ItemType Directory -Force -Path $matrixResults | Out-Null

        $launch = Invoke-NativeHost -InstallRoot $layoutRoot -DataRoot (Join-Path $matrixResults 'data')
        $observed['native-execution'] = [pscustomobject]@{
            passed = $launch.windowShown -and $launch.closedPolitely -and $launch.exitCode -eq 0
            detail = "Window shown: $($launch.windowShown); closed politely: $($launch.closedPolitely); exit code $($launch.exitCode). Host machine: ARM64."
        }

        foreach ($suite in @(
                @{ id = 'codec-matrix'; filter = 'FullyQualifiedName~CodecMatrixTests' }
                @{ id = 'hdr-acceleration'; filter = 'FullyQualifiedName~HdrAccelerationTests' }
                @{ id = 'audio-output'; filter = 'FullyQualifiedName~AudioChannelTests' })) {
            $run = Invoke-MediaSuite -Filter $suite.filter -ResultsDirectory (Join-Path $matrixResults $suite.id)
            $entry = @{
                passed = $run.succeeded
                detail = "Executed natively on ARM64. $($run.summary)"
            }
            # A suite that could not measure carries a reason, and a reason makes the phase Blocked
            # instead of Failed further down. Its detail is cleared so the record cannot claim an
            # observation it does not have.
            if ($run.PSObject.Properties.Name -contains 'reason' -and $run.reason) {
                $entry['reason'] = $run.reason
                $entry['detail'] = ''
            }
            $observed[$suite.id] = [pscustomobject]$entry
        }

        # The lifecycle report does not exist until something produces it, so this produces it.
        # Until 2026-09-04 the phase read `windows-lifecycle.json` from this folder while
        # verify-package.ps1 writes `lifecycle.json` into the package root it is handed: the name and
        # the folder both differed, so the phase would have reported itself blocked ON A REAL ARM64
        # MACHINE, for want of a file nobody had asked anyone to write. Invoking the verifier here is
        # what this script already does for three of the other phases, which run `dotnet test`
        # themselves.
        #
        # -SkipReproducibility: comparing two builds is the slow half of that script, and what it
        # keeps fresh is `reproducibility.json` for the x64 package that ReproducibleBuildTests reads.
        # This is not that package. -Work keeps its scratch folder out of the x64 verifier's.
        #
        # The verifier throws when it cannot complete — with no ffmpeg there is no sample and the
        # file-association phase says so. That is caught rather than fatal: an unanswerable phase is
        # this matrix's subject, not its accident, and the missing report below names it.
        $lifecyclePath = Join-Path $outputRoot 'lifecycle.json'
        try {
            & (Join-Path $PSScriptRoot 'verify-package.ps1') `
                -Mode Verify `
                -PackageRoot $outputRoot `
                -Work (Join-Path $outputRoot 'lifecycle-work') `
                -SkipReproducibility
        }
        catch {
            Write-Warning "The ARM64 lifecycle verification did not complete: $_"
        }

        $observed['package-lifecycle'] = if (Test-Path -LiteralPath $lifecyclePath) {
            $lifecycle = Get-Content -LiteralPath $lifecyclePath -Raw | ConvertFrom-Json
            $notPassed = @($lifecycle.phases | Where-Object { $_.outcome -ne 'Passed' })
            [pscustomobject]@{
                passed = $notPassed.Count -eq 0
                detail = "$(@($lifecycle.phases).Count) lifecycle phase(s), $($notPassed.Count) not passed."
            }
        }
        else {
            [pscustomobject]@{
                passed = $false
                detail = ''
                reason = "The ARM64 lifecycle report is not at $lifecyclePath, so eng/verify-package.ps1 did not get far enough to write one against this package."
            }
        }

        $observed['cross-architecture-data'] = if ($X64DataRoot -and (Test-Path -LiteralPath $X64DataRoot)) {
            $carried = Join-Path $matrixResults 'from-x64'
            Copy-Item -LiteralPath $X64DataRoot -Destination $carried -Recurse -Force
            $before = @(Get-ChildItem -LiteralPath $carried -Recurse -File).Count
            $reopened = Invoke-NativeHost -InstallRoot $layoutRoot -DataRoot $carried
            $database = Join-Path $carried 'library.db'
            [pscustomobject]@{
                passed = $reopened.windowShown -and $reopened.exitCode -eq 0 -and (Test-Path -LiteralPath $database)
                detail = "A data folder written by the x64 build ($before file(s)) was opened by the ARM64 build; window shown: $($reopened.windowShown), exit code $($reopened.exitCode)."
            }
        }
        else {
            [pscustomobject]@{
                passed = $false
                detail = ''
                reason = 'No x64 data folder was supplied with -X64DataRoot, so nothing was carried between architectures.'
            }
        }
    }

    $phases = foreach ($phase in $matrixPhases) {
        $result = $observed[$phase.id]
        if ($null -eq $result) {
            [ordered]@{
                id       = $phase.id
                kind     = 'native'
                question = $phase.question
                outcome  = 'Blocked'
                detail   = ''
                reason   = $blockedReason
            }
        }
        elseif ($result.passed) {
            [ordered]@{
                id       = $phase.id
                kind     = 'native'
                question = $phase.question
                outcome  = 'Passed'
                detail   = $result.detail
                reason   = ''
            }
        }
        else {
            [ordered]@{
                id       = $phase.id
                kind     = 'native'
                question = $phase.question
                # A phase that could run and did not pass is a failure, never a block: blocking is for
                # what the machine cannot answer, and this machine just answered it.
                outcome  = if ($result.PSObject.Properties.Name -contains 'reason' -and $result.reason) { 'Blocked' } else { 'Failed' }
                detail   = $result.detail
                reason   = if ($result.PSObject.Properties.Name -contains 'reason') { $result.reason } else { '' }
            }
        }
    }

    $matrix = [ordered]@{
        version     = $version
        runtime     = 'win-arm64'
        commit      = (git -C $repoRoot rev-parse HEAD).Trim()
        environment = [ordered]@{
            hostArchitecture    = $hostArchitecture
            processArchitecture = $processArchitecture
            arm64Host           = $isArm64Host
        }
        phases      = @($phases)
    }

    Set-Content `
        -LiteralPath (Join-Path $outputRoot 'arm64-matrix.json') `
        -Value ($matrix | ConvertTo-Json -Depth 8) `
        -Encoding utf8NoBOM

    Write-Output ''
    Write-Output "MSIX: $msixPath"
    Write-Output "ZIP:  $zipPath"
    Write-Output "Layout files: $($layoutFiles.Count); SBOM gaps: $($sbomGaps.Count); secret hits: $($secretHits.Count)."
    Write-Output "Parity with x64: compared=$($parity.compared), application only-x64=$($parity.onlyInX64.Count), only-arm64=$($parity.onlyInArm64.Count); native differences $($parity.nativeOnlyInX64.Count)/$($parity.nativeOnlyInArm64.Count)."
    Write-Output "ARM64 matrix: host=$hostArchitecture, $(@($phases | Where-Object { $_.outcome -ne 'Passed' }).Count) of $($phases.Count) phase(s) not passed."
    if ($onlyInLayout.Count -gt 0 -or $onlyInPackage.Count -gt 0) {
        throw "The package and the layout disagree: $($onlyInLayout.Count) verified-not-shipped, $($onlyInPackage.Count) shipped-not-verified."
    }
}
finally {
    Pop-Location
}
