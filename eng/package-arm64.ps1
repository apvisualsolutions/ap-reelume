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
    Runs one media suite and reports what it observed. The suites are the verification: they open real
    files with the engine that shipped, so running them on ARM64 is what makes the matrix a
    measurement rather than a restatement of the build succeeding.
#>
function Invoke-MediaSuite {
    param(
        [Parameter(Mandatory)][string]$Filter,
        [Parameter(Mandatory)][string]$ResultsDirectory
    )

    $project = Join-Path $repoRoot 'tests/ApSolutions.LocalMedia.MediaTests'
    $output = & dotnet test $project -c $Configuration -m:1 `
        --settings (Join-Path $PSScriptRoot 'test.runsettings') `
        --filter $Filter `
        --results-directory $ResultsDirectory 2>&1
    $summary = @($output | Select-String -Pattern 'Superado:|Passed!|Failed!|Con error:' | Select-Object -Last 1)

    return [pscustomobject]@{
        succeeded = $LASTEXITCODE -eq 0
        summary   = if ($summary) { ($summary[0].ToString()).Trim() } else { 'No test summary was produced.' }
    }
}

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

    # The one attribute that legitimately differs between the two packages. Everything else — identity,
    # publisher, capabilities, the file type list, the virtualisation switches that keep an uninstall
    # from deleting the library — is the same file, and a test compares them.
    if ($manifest.Package.Identity.ProcessorArchitecture -ne 'x64') {
        throw "Package.appxmanifest declares $($manifest.Package.Identity.ProcessorArchitecture); the ARM64 rewrite expects x64."
    }

    $manifest.Package.Identity.ProcessorArchitecture = 'arm64'
    $manifest.Save((Join-Path $layoutRoot 'AppxManifest.xml'))

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
            $observed[$suite.id] = [pscustomobject]@{
                passed = $run.succeeded
                detail = "Executed natively on ARM64. $($run.summary)"
            }
        }

        $lifecyclePath = Join-Path $outputRoot 'windows-lifecycle.json'
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
                reason = "The ARM64 lifecycle report is not at $lifecyclePath. Run eng/verify-package.ps1 against this package."
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
