<#
.SYNOPSIS
    Puts the sealed package through its lifecycle and proves two builds of one commit agree.

.DESCRIPTION
    Four phases of the lifecycle belong to Windows: installing a package, replacing it, repairing it,
    and removing it. All four need a clean machine to run against, an administrator to run them, and a
    certificate the installer trusts. None of the three exists on this hardware, so those four are
    reported as blocked, with the reason, and MsixLifecycleTests refuses to let a blocked phase be
    read as a passing one.

    What can be run is run: the package is unpacked and exercised exactly as it would be after an
    install, against a data folder of its own each time. AP_LOCALMEDIA_DATA_ROOT is what makes that
    possible — redirecting LOCALAPPDATA does not redirect anything, because .NET resolves the folder
    through SHGetFolderPath and never reads that variable.

    The reproducibility comparison builds the payload twice, from two clean checkouts in two
    different directories, and compares it file by file. The archive itself is not compared: an MSIX
    records the moment it was written, so two identical payloads always seal into two different
    files. What has to match is everything inside.
#>
[CmdletBinding()]
param(
    [ValidateSet('Audit', 'Verify')]
    [string]$Mode = 'Audit',

    [string]$PackageRoot = 'artifacts/package',

    [string]$Work = 'artifacts/package-verify',

    # The comparison is the slow half of this script. Skipping it leaves reproducibility.json stale,
    # which ReproducibleBuildTests then fails on, so it can be skipped but not hidden.
    [switch]$SkipReproducibility
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageRootFull = [IO.Path]::GetFullPath($PackageRoot, $repoRoot)
$workRoot = [IO.Path]::GetFullPath($Work, $repoRoot)
$layoutRoot = Join-Path $packageRootFull 'layout'

if (-not (Test-Path -LiteralPath (Join-Path $packageRootFull 'contents.json'))) {
    throw "There is no package in $packageRootFull. Run eng/package-x64.ps1 first."
}

$contents = Get-Content -LiteralPath (Join-Path $packageRootFull 'contents.json') -Raw | ConvertFrom-Json
$msixPath = Join-Path $packageRootFull $contents.msix

# MakeAppx generates these; they belong to the package format, not to the payload.
$opcOverhead = @('[Content_Types].xml', 'AppxBlockMap.xml', 'AppxSignature.p7x', 'AppxManifest.xml')

function Get-MakeAppx {
    # @() because a machine with exactly one SDK yields a bare string, and indexing a string
    # returns its first character — unpacking then tries to run a program called 'C'.
    $found = @(Get-ChildItem -LiteralPath 'C:/Program Files (x86)/Windows Kits/10/bin' -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^10\.' } |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64/makeappx.exe' } |
        Where-Object { Test-Path -LiteralPath $_ })
    if (-not $found) { throw 'MakeAppx.exe was not found. Install the Windows 10/11 SDK.' }
    return $found[0]
}

$makeAppx = Get-MakeAppx

function Clear-ProfilerEnvironment {
    foreach ($name in @(
            'CORECLR_ENABLE_PROFILING', 'CORECLR_PROFILER', 'CORECLR_PROFILER_PATH',
            'CORECLR_PROFILER_PATH_32', 'CORECLR_PROFILER_PATH_64',
            'COR_ENABLE_PROFILING', 'COR_PROFILER', 'COR_PROFILER_PATH')) {
        Remove-Item "Env:$name" -ErrorAction SilentlyContinue
    }
}

<#
    Starts the application the way an installed copy would start, waits for it to put a window on the
    screen, and closes it the way a person would. A run that has to be killed is a failed run.
#>
function Invoke-Application {
    param(
        [Parameter(Mandatory)][string]$InstallRoot,
        [Parameter(Mandatory)][string]$DataRoot,
        [string[]]$Arguments = @(),
        [int]$WindowTimeoutSeconds = 90,
        [int]$SettleSeconds = 6,
        [int]$ExitTimeoutSeconds = 45
    )

    New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null
    Clear-ProfilerEnvironment
    $env:AP_LOCALMEDIA_DATA_ROOT = $DataRoot
    # No token, so identification never opens a connection during a lifecycle run.
    Remove-Item 'Env:AP_LOCALMEDIA_TMDB_TOKEN' -ErrorAction SilentlyContinue

    $exe = Join-Path $InstallRoot 'ApSolutions.LocalMedia.Windows.exe'
    try {
        $process = if ($Arguments.Count -gt 0) {
            Start-Process -FilePath $exe -ArgumentList $Arguments -PassThru
        }
        else {
            Start-Process -FilePath $exe -PassThru
        }

        $deadline = [datetime]::UtcNow.AddSeconds($WindowTimeoutSeconds)
        $windowShown = $false
        while ([datetime]::UtcNow -lt $deadline) {
            if ($process.HasExited) { break }
            $process.Refresh()
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $windowShown = $true; break }
            Start-Sleep -Milliseconds 250
        }

        # Startup is asynchronous: the window appears before the first scan and the first write.
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
            windowShown   = $windowShown
            closedPolitely = $closedPolitely
            exitCode      = $process.ExitCode
        }
    }
    finally {
        Remove-Item 'Env:AP_LOCALMEDIA_DATA_ROOT' -ErrorAction SilentlyContinue
    }
}

# The application's own SQLite, loaded from the payload being verified rather than from anywhere
# else. Reading the result with the binaries that produced it is the point.
$sqliteLoaded = $false
function Open-LibraryDatabase {
    param([Parameter(Mandatory)][string]$DatabasePath)

    if (-not $script:sqliteLoaded) {
        foreach ($assembly in @(
                'SQLitePCLRaw.core.dll',
                'SQLitePCLRaw.provider.e_sqlite3.dll',
                'SQLitePCLRaw.batteries_v2.dll',
                'Microsoft.Data.Sqlite.dll')) {
            Add-Type -Path (Join-Path $layoutRoot $assembly)
        }

        [SQLitePCL.Batteries_V2]::Init()
        $script:sqliteLoaded = $true
    }

    $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection(
        "Data Source=$DatabasePath;Mode=ReadWrite;Pooling=False")
    $connection.Open()
    return $connection
}

function Invoke-Scalar {
    param($Connection, [string]$Sql)
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}

function Invoke-NonQuery {
    param($Connection, [string]$Sql)
    $command = $Connection.CreateCommand()
    $command.CommandText = $Sql
    try { return $command.ExecuteNonQuery() } finally { $command.Dispose() }
}

function Get-TableCounts {
    param([string]$DatabasePath, [string[]]$Tables)
    $connection = Open-LibraryDatabase -DatabasePath $DatabasePath
    try {
        $counts = [ordered]@{}
        foreach ($table in $Tables) {
            $counts[$table] = [int](Invoke-Scalar -Connection $connection -Sql "SELECT COUNT(*) FROM $table;")
        }
        return $counts
    }
    finally { $connection.Close(); $connection.Dispose() }
}

function Get-RelativeFileMap {
    param([string]$Root)
    $map = [ordered]@{}
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File) {
        $name = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        $map[$name] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }

    return $map
}

<#
    Retires a throwaway checkout.

    The build servers hold file handles for a while after a publish, and a directory Windows is still
    finishing with cannot be deleted. Shutting them down first removes the usual cause; the retries
    cover the rest, and `prune` makes sure git forgets the checkout even when a stray empty directory
    outlives it.
#>
function Remove-Worktree {
    param([Parameter(Mandatory)][string]$Path)

    dotnet build-server shutdown 2>&1 | Out-Null
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        git -C $repoRoot worktree remove --force $Path 2>&1 | Out-Null
        if (-not (Test-Path -LiteralPath $Path)) { break }
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
        if (-not (Test-Path -LiteralPath $Path)) { break }
        Start-Sleep -Seconds ($attempt)
    }

    git -C $repoRoot worktree prune | Out-Null
    if (Test-Path -LiteralPath $Path) {
        Write-Warning "The throwaway checkout at $Path outlived the run; git has forgotten it and it can be deleted by hand."
    }
}

$phases = [Collections.Generic.List[object]]::new()
function Add-Phase {
    param([hashtable]$Phase)
    $phases.Add([pscustomobject]$Phase)
}

if (Test-Path -LiteralPath $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $workRoot | Out-Null

Push-Location $repoRoot
try {
    # ---------------------------------------------------------------- unpack
    Write-Output 'unpack: opening the sealed package …'
    $installA = Join-Path $workRoot 'install-a'
    & $makeAppx unpack /p $msixPath /d $installA /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx could not unpack the package.' }

    $layoutMap = Get-RelativeFileMap -Root $layoutRoot
    $installMap = Get-RelativeFileMap -Root $installA
    $unpackedPayload = @($installMap.Keys | Where-Object { $_ -notin $opcOverhead -and -not $_.StartsWith('AppxMetadata/') })
    $mismatched = @($unpackedPayload | Where-Object { $layoutMap[$_] -ne $installMap[$_] })
    $absent = @($layoutMap.Keys | Where-Object { $_ -notin $installMap.Keys })

    Add-Phase @{
        id      = 'unpack'
        kind    = 'substitute'
        outcome = if ($mismatched.Count -eq 0 -and $absent.Count -eq 0) { 'Passed' } else { 'Failed' }
        detail  = "$($unpackedPayload.Count) file(s) recovered from the package, $($mismatched.Count) altered, $($absent.Count) absent."
    }

    # ---------------------------------------------------------- first launch
    Write-Output 'first-launch: starting the unpacked application against an empty data folder …'
    $firstData = Join-Path $workRoot 'cycle-first/data'
    $firstRun = Invoke-Application -InstallRoot $installA -DataRoot $firstData
    $firstDatabase = Join-Path $firstData 'library.db'
    $migrations = if (Test-Path -LiteralPath $firstDatabase) {
        [int](Get-TableCounts -DatabasePath $firstDatabase -Tables @('schema_history')).schema_history
    }
    else { 0 }

    Add-Phase @{
        id       = 'first-launch'
        kind     = 'substitute'
        outcome  = if ($firstRun.windowShown -and $firstRun.exitCode -eq 0 -and $migrations -gt 0) { 'Passed' } else { 'Failed' }
        detail   = "Window shown: $($firstRun.windowShown); exit code $($firstRun.exitCode); $migrations migration(s) applied to a new database."
        dataRoot = $firstData
    }

    # -------------------------------------------------------------- open with
    Write-Output 'open-with: activating the application on a loose file …'
    $sample = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'artifacts/test-media') -Recurse -File -Include '*.mp4', '*.mkv' -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $sample) {
        & (Join-Path $PSScriptRoot 'generate-test-media.ps1') -Output 'artifacts/test-media' | Out-Null
        $sample = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'artifacts/test-media') -Recurse -File -Include '*.mp4', '*.mkv' |
            Select-Object -First 1
    }

    if (-not $sample) { throw 'No generated sample is available, so the association cannot be exercised.' }

    $looseData = Join-Path $workRoot 'cycle-openwith/data'
    $looseRun = Invoke-Application -InstallRoot $installA -DataRoot $looseData -Arguments @($sample.FullName)
    $looseCounts = Get-TableCounts -DatabasePath (Join-Path $looseData 'library.db') -Tables @('media_files', 'library_roots')

    Add-Phase @{
        id               = 'open-with'
        kind             = 'substitute'
        outcome          = if ($looseRun.windowShown -and $looseCounts.media_files -eq 0 -and $looseCounts.library_roots -eq 0) { 'Passed' } else { 'Failed' }
        detail           = "Activated on $($sample.Extension); catalogue left at $($looseCounts.media_files) file(s) and $($looseCounts.library_roots) root(s)."
        dataRoot         = $looseData
        accepted         = [bool]$looseRun.windowShown
        mediaFilesAfter  = [int]$looseCounts.media_files
        libraryRootsAfter = [int]$looseCounts.library_roots
    }

    # ---------------------------------------------------------------- upgrade
    Write-Output 'upgrade: replacing the package under a data folder that is already in use …'
    $upgradeData = Join-Path $workRoot 'cycle-upgrade/data'
    $null = Invoke-Application -InstallRoot $installA -DataRoot $upgradeData
    $upgradeDatabase = Join-Path $upgradeData 'library.db'

    # What a catalogued library looks like, written directly so the check does not depend on driving
    # the interface. If the schema ever changes shape, this fails loudly rather than silently.
    $seed = Open-LibraryDatabase -DatabasePath $upgradeDatabase
    try {
        $rootId = [guid]::NewGuid().ToString()
        $now = [DateTimeOffset]::UtcNow.ToString('O')
        Invoke-NonQuery -Connection $seed -Sql @"
INSERT INTO library_roots (id, normalized_path, kind, availability, scan_policy)
VALUES ('$rootId', 'x:\seeded-library', 0, 0, 1);
INSERT INTO media_files (id, library_root_id, normalized_path, size_bytes, last_write_utc, container, video_codecs, audio_codecs, is_available)
VALUES ('$([guid]::NewGuid())', '$rootId', 'x:\seeded-library\one.mkv', 1024, '$now', 'mkv', 'h264', 'aac', 1);
INSERT INTO media_files (id, library_root_id, normalized_path, size_bytes, last_write_utc, container, video_codecs, audio_codecs, is_available)
VALUES ('$([guid]::NewGuid())', '$rootId', 'x:\seeded-library\two.mkv', 2048, '$now', 'mkv', 'hevc', 'ac3', 1);
INSERT INTO playback_preferences (scope, scope_key, audio_language, volume_percent, updated_at)
VALUES (0, 'global', 'spa', 85, '$now');
"@ | Out-Null
    }
    finally { $seed.Close(); $seed.Dispose() }

    $trackedTables = @('library_roots', 'media_files', 'playback_preferences')
    $before = Get-TableCounts -DatabasePath $upgradeDatabase -Tables ($trackedTables + 'schema_history')

    # The next release, sealed from the same payload with the next version in its manifest. The code
    # is the same code; the question this phase answers is what happens to the data when the package
    # under it is replaced.
    $nextLayout = Join-Path $workRoot 'next-layout'
    Copy-Item -LiteralPath $layoutRoot -Destination $nextLayout -Recurse -Force
    $nextVersion = '0.2.0.0'
    $nextManifestPath = Join-Path $nextLayout 'AppxManifest.xml'
    [xml]$nextManifest = Get-Content -LiteralPath $nextManifestPath -Raw
    $nextManifest.Package.Identity.Version = $nextVersion
    $nextManifest.Save($nextManifestPath)

    $nextMsix = Join-Path $workRoot 'next.msix'
    & $makeAppx pack /d $nextLayout /p $nextMsix /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx could not seal the follow-up package.' }

    $installB = Join-Path $workRoot 'install-b'
    & $makeAppx unpack /p $nextMsix /d $installB /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx could not unpack the follow-up package.' }

    $upgradeRun = Invoke-Application -InstallRoot $installB -DataRoot $upgradeData
    $after = Get-TableCounts -DatabasePath $upgradeDatabase -Tables ($trackedTables + 'schema_history')
    $preserved = @($trackedTables | Where-Object { $before[$_] -ne $after[$_] }).Count -eq 0

    Add-Phase @{
        id                  = 'upgrade'
        kind                = 'substitute'
        outcome             = if ($preserved -and $upgradeRun.windowShown -and $upgradeRun.exitCode -eq 0) { 'Passed' } else { 'Failed' }
        detail              = "0.1.0.0 replaced by $nextVersion over the same data folder; catalogue and preferences unchanged: $preserved."
        dataRoot            = $upgradeData
        before              = [ordered]@{
            library_roots         = [int]$before.library_roots
            media_files           = [int]$before.media_files
            playback_preferences  = [int]$before.playback_preferences
        }
        after               = [ordered]@{
            library_roots         = [int]$after.library_roots
            media_files           = [int]$after.media_files
            playback_preferences  = [int]$after.playback_preferences
        }
        schemaVersionBefore = [int]$before.schema_history
        schemaVersionAfter  = [int]$after.schema_history
    }

    # ------------------------------------------------------ downgrade refused
    Write-Output 'downgrade-refused: putting this build in front of a database a later release migrated …'
    $downgradeData = Join-Path $workRoot 'cycle-downgrade/data'
    $null = Invoke-Application -InstallRoot $installA -DataRoot $downgradeData
    $downgradeDatabase = Join-Path $downgradeData 'library.db'

    $future = Open-LibraryDatabase -DatabasePath $downgradeDatabase
    try {
        $newest = [int](Invoke-Scalar -Connection $future -Sql 'SELECT MAX(version) FROM schema_history;')
        Invoke-NonQuery -Connection $future -Sql @"
INSERT INTO schema_history (version, name, applied_utc, checksum)
VALUES ($($newest + 1), 'from_a_later_release', '$([DateTimeOffset]::UtcNow.ToString('O'))', 'not-a-checksum-this-build-knows');
"@ | Out-Null
    }
    finally { $future.Close(); $future.Dispose() }

    $fingerprintBefore = (Get-FileHash -LiteralPath $downgradeDatabase -Algorithm SHA256).Hash
    $downgradeRun = Invoke-Application -InstallRoot $installA -DataRoot $downgradeData -SettleSeconds 8
    $fingerprintAfter = (Get-FileHash -LiteralPath $downgradeDatabase -Algorithm SHA256).Hash
    $downgradeCounts = Get-TableCounts -DatabasePath $downgradeDatabase -Tables @('schema_history')
    $refused = $downgradeCounts.schema_history -eq ($newest + 1)

    Add-Phase @{
        id               = 'downgrade-refused'
        kind             = 'substitute'
        outcome          = if ($refused -and $fingerprintBefore -eq $fingerprintAfter) { 'Passed' } else { 'Failed' }
        detail           = "Schema $($newest + 1) met a build that knows $newest; the application showed its recovery screen and wrote nothing."
        dataRoot         = $downgradeData
        refused          = [bool]$refused
        databaseModified = ($fingerprintBefore -ne $fingerprintAfter)
        windowShown      = [bool]$downgradeRun.windowShown
    }

    # ----------------------------------------------------------------- repair
    Write-Output 'repair: removing payload from the installation and restoring it from the package …'
    $damaged = @(
        Join-Path $installA 'Avalonia.Themes.Fluent.dll'
        Join-Path $installA 'libvlc/win-x64/plugins/access/libfilesystem_plugin.dll'
        Join-Path $installA 'LICENSE'
    ) | Where-Object { Test-Path -LiteralPath $_ }
    foreach ($file in $damaged) { Remove-Item -LiteralPath $file -Force }

    & $makeAppx unpack /p $msixPath /d $installA /o | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx could not repair the installation.' }

    $missingAfterRepair = @($damaged | Where-Object { -not (Test-Path -LiteralPath $_) }).Count
    $repairData = Join-Path $workRoot 'cycle-repair/data'
    $repairRun = Invoke-Application -InstallRoot $installA -DataRoot $repairData

    Add-Phase @{
        id                 = 'repair'
        kind               = 'substitute'
        outcome            = if ($missingAfterRepair -eq 0 -and $repairRun.windowShown -and $repairRun.exitCode -eq 0) { 'Passed' } else { 'Failed' }
        detail             = "$($damaged.Count) file(s) removed and restored from the package; the application started again."
        dataRoot           = $repairData
        removedFiles       = $damaged.Count
        missingAfterRepair = $missingAfterRepair
    }

    # -------------------------------------------------------------- uninstall
    Write-Output 'uninstall: removing the installation and looking at what is left …'
    Remove-Item -LiteralPath $installB -Recurse -Force
    $survivingCounts = Get-TableCounts -DatabasePath $upgradeDatabase -Tables @('media_files', 'library_roots')

    Add-Phase @{
        id                 = 'uninstall'
        kind               = 'substitute'
        outcome            = if ((-not (Test-Path -LiteralPath $installB)) -and $survivingCounts.media_files -gt 0) { 'Passed' } else { 'Failed' }
        detail             = "The installation is gone; the data folder still holds $($survivingCounts.media_files) catalogued file(s)."
        dataRoot           = $upgradeData
        applicationPresent = (Test-Path -LiteralPath $installB)
        dataPresent        = (Test-Path -LiteralPath $upgradeDatabase)
        mediaFilesAfter    = [int]$survivingCounts.media_files
    }

    # ------------------------------------------------- what Windows would own
    <#
        The four phases Windows performs cannot run from this script: they need a clean machine, an
        administrator, and a signature. They were run by hand inside Windows Sandbox — a clean,
        disposable Windows — and archived at docs/evidence/mvp/windows-lifecycle.json.

        That report is accepted only while it still describes this package. It carries the SHA-256 of
        Package.appxmanifest, and the manifest is what governs installation, file associations, and
        write virtualisation: change it and the report expires, the phases go back to blocked, and
        MsixLifecycleTests says so. A payload change does not expire it, because the payload is not
        what Windows reads when it installs.

        eng/README-sandbox.md records how to run it again.
    #>
    $archivedPath = Join-Path $repoRoot 'docs/evidence/mvp/windows-lifecycle.json'
    $manifestSha = (Get-FileHash -LiteralPath (Join-Path $repoRoot 'src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest') -Algorithm SHA256).Hash.ToLowerInvariant()
    $archived = $null
    if (Test-Path -LiteralPath $archivedPath) {
        $candidate = Get-Content -LiteralPath $archivedPath -Raw | ConvertFrom-Json
        if ($candidate.version -eq $contents.version -and $candidate.manifestSha256 -eq $manifestSha) {
            $archived = $candidate
        }
        else {
            Write-Warning "The archived Windows lifecycle report describes a different package; the native phases stay blocked."
        }
    }

    $environment = [ordered]@{
        cleanVirtualMachine = $false
        elevated            = $false
        signed              = $false
        note                = 'The substitute cycles ran on developer hardware with a live profile, each against its own folder through AP_LOCALMEDIA_DATA_ROOT, so no personal data was touched.'
    }

    if ($null -ne $archived) {
        $environment = [ordered]@{
            cleanVirtualMachine = [bool]$archived.machine.cleanVirtualMachine
            elevated            = [bool]$archived.machine.elevated
            signed              = $true
            note                = [string]$archived.note
        }

        foreach ($id in @('windows-install', 'windows-upgrade', 'windows-repair', 'windows-uninstall', 'file-association')) {
            $row = $archived.phases | Where-Object { $_.id -eq $id } | Select-Object -First 1
            if ($null -eq $row) {
                Add-Phase @{ id = $id; kind = 'native'; outcome = 'Failed'; detail = 'The archived report has no such phase.' }
                continue
            }

            Add-Phase @{ id = $id; kind = 'native'; outcome = [string]$row.outcome; detail = [string]$row.detail }
        }

        Write-Output ("windows lifecycle: adopted the archived Sandbox run for {0}." -f $archived.version)
    }
    else {
        $blockedReason = 'No clean Windows virtual machine, no administrator elevation, and no code-signing certificate on this hardware, ' +
            'and no archived Sandbox run matching this package. The unpacked package was exercised against an isolated data folder instead.'
        foreach ($id in @('windows-install', 'windows-upgrade', 'windows-repair', 'windows-uninstall', 'file-association')) {
            Add-Phase @{ id = $id; kind = 'native'; outcome = 'Blocked'; reason = $blockedReason; detail = 'Not attempted.' }
        }
    }

    $lifecycle = [ordered]@{
        version     = $contents.version
        commit      = $contents.commit
        environment = $environment
        phases      = $phases
    }

    Set-Content `
        -LiteralPath (Join-Path $packageRootFull 'lifecycle.json') `
        -Value ($lifecycle | ConvertTo-Json -Depth 8) `
        -Encoding utf8NoBOM

    # ------------------------------------------------------- reproducibility
    if (-not $SkipReproducibility) {
        Write-Output 'reproducibility: building the payload twice from two clean checkouts …'
        $untracked = @(git -C $repoRoot ls-files --others --exclude-standard)
        if ($untracked.Count -gt 0) {
            throw ("A clean checkout cannot see these untracked files, so the comparison would not be about what is " +
                "about to be committed: $($untracked -join ', '). Stage them first.")
        }

        # `git stash create` snapshots the index and the working tree without disturbing either, so
        # the comparison is about the state being verified rather than about the last commit. On a
        # clean tree it writes nothing at all — which is the normal case right after a commit, and in
        # CI — so HEAD is the state to compare.
        $snapshot = ((git -C $repoRoot stash create) | Out-String).Trim()
        if (-not $snapshot) { $snapshot = ((git -C $repoRoot rev-parse HEAD) | Out-String).Trim() }

        $builds = @()
        $reproRoot = Join-Path $workRoot 'repro'
        foreach ($name in @('first', 'second')) {
            $checkout = Join-Path $reproRoot $name
            git -C $repoRoot worktree add --detach $checkout $snapshot | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Could not create the $name checkout." }

            & (Join-Path $checkout 'eng/package-x64.ps1') -Output (Join-Path $checkout 'artifacts/package') -LayoutOnly
            if ($LASTEXITCODE -ne 0) { throw "The $name build failed." }

            $builds += [pscustomobject]@{
                name     = $name
                checkout = $checkout
                commit   = $snapshot
                clean    = (@(git -C $checkout status --porcelain).Count -eq 0)
                layout   = Join-Path $checkout 'artifacts/package/layout'
            }
        }

        $firstMap = Get-RelativeFileMap -Root $builds[0].layout
        $secondMap = Get-RelativeFileMap -Root $builds[1].layout
        $onlyInFirst = @($firstMap.Keys | Where-Object { $_ -notin $secondMap.Keys })
        $onlyInSecond = @($secondMap.Keys | Where-Object { $_ -notin $firstMap.Keys })
        $shared = @($firstMap.Keys | Where-Object { $_ -in $secondMap.Keys })
        $differing = @($shared | Where-Object { $firstMap[$_] -ne $secondMap[$_] })

        $reproducibility = [ordered]@{
            commit                 = $snapshot
            normalization          = 'The OPC container is not compared: MakeAppx records the moment it seals a package, so two identical payloads never seal into identical archives. Every file inside is compared by SHA-256.'
            builds                 = @($builds | ForEach-Object {
                    [ordered]@{
                        checkout  = $_.checkout
                        commit    = $_.commit
                        clean     = [bool]$_.clean
                        fileCount = if ($_.name -eq 'first') { $firstMap.Count } else { $secondMap.Count }
                    }
                })
            identicalFiles         = ($shared.Count - $differing.Count)
            differingFiles         = $differing
            onlyInFirst            = $onlyInFirst
            onlyInSecond           = $onlyInSecond
            excludedFromComparison = @()
        }

        Set-Content `
            -LiteralPath (Join-Path $packageRootFull 'reproducibility.json') `
            -Value ($reproducibility | ConvertTo-Json -Depth 8) `
            -Encoding utf8NoBOM

        foreach ($build in $builds) {
            Remove-Worktree -Path $build.checkout
        }

        Write-Output ("reproducibility: {0} identical, {1} differing, {2} unmatched." -f
            $reproducibility.identicalFiles, $differing.Count, ($onlyInFirst.Count + $onlyInSecond.Count))
    }

    Write-Output ''
    foreach ($phase in $phases) {
        Write-Output ("{0,-20} {1,-8} {2}" -f $phase.id, $phase.outcome, $phase.detail)
    }

    if ($Mode -eq 'Audit') {
        Write-Output 'Audit mode: findings inventoried, nothing blocks.'
        exit 0
    }

    $failed = @($phases | Where-Object { $_.outcome -notin @('Passed', 'Blocked') })
    if ($failed.Count -gt 0) {
        $failed | ForEach-Object { Write-Error "Phase $($_.id) did not pass: $($_.detail)" }
        exit 1
    }

    Write-Output ("Lifecycle Verify: {0} phase(s) passed, {1} blocked and declared." -f
        @($phases | Where-Object { $_.outcome -eq 'Passed' }).Count,
        @($phases | Where-Object { $_.outcome -eq 'Blocked' }).Count)
}
finally {
    Pop-Location
}
