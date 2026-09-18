# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions

<#
.SYNOPSIS
    Puts the LibVLC tree libvlc.lock.json pins where the build copies it from, and accepts it only if
    its bytes are the ones pinned (ENG-013).

.DESCRIPTION
    Until 2026-09-18 LibVLC came from the VideoLAN.LibVLC.Windows NuGet package, which carries GPL
    plugins. It now comes from the trees libvlc-nogpl.yml builds without GPL code, published by
    libvlc-publish.yml as a prerelease of this repository. Two checks, in this order:

      1. The zip's SHA-512 is the one in the lock file. -cne, because PowerShell's -ne ignores case
         and a hash written in the other case would then match anything that differs only in case.
      2. Every file the zip expands to is listed in the manifest.json verify-nogpl.ps1 wrote beside
         it, with the SHA-256 written there, and nothing else is. The zip hash already covers the
         bytes; this covers the NAMES, so a tree that gained or lost a plugin between verification
         and publication cannot pass as the verified one.

    Idempotent: a tree already in place whose marker records the pinned hash is left alone, which is
    what lets eng/libvlc/LibVlc.targets call this on every build. Several builds may call it at
    once, so the whole of it runs under one named mutex and the tree is swapped in with a rename.

.PARAMETER Architecture
    x64, arm64 or both.

.PARAMETER Destination
    Root of the trees; each lands in <Destination>/win-<arch>, beside a win-<arch>.sha512 marker that
    records which pinned tree it is. Defaults to artifacts/libvlc.

.PARAMETER FromTree
    Before a tree is published there is no hash to pin, and the suites still have to run against it.
    This takes a tree downloaded from a libvlc-nogpl.yml run (gh run download <id> -n
    libvlc-nogpl-x64), checks it against its own manifest and installs it marked "unpinned". A build
    over an unpinned tree warns on every call, and the lock file cannot reach main that way:
    LibVlcLockTests demands the hashes.
#>
[CmdletBinding()]
param(
    # A comma-separated string and not an array: MSBuild's Exec calls this with -File, which passes
    # 'x64,arm64' as one string.
    [string] $Architecture = 'x64,arm64',
    [string] $Destination = (Join-Path $PSScriptRoot '../../artifacts/libvlc'),
    [string] $FromTree
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$lock = Get-Content (Join-Path $PSScriptRoot 'libvlc.lock.json') -Raw | ConvertFrom-Json
$architectures = @($Architecture -split ',' | ForEach-Object Trim | Where-Object { $_ })
foreach ($a in $architectures) { if ($a -cnotin 'x64', 'arm64') { throw "Unknown architecture '$a': x64 or arm64." } }
if ($FromTree -and $architectures.Count -ne 1) { throw '-FromTree installs one architecture: name it with -Architecture.' }

function Assert-MatchesManifest([string] $tree) {
    $manifestPath = Join-Path $tree 'manifest.json'
    if (-not (Test-Path $manifestPath)) { throw "The tree at $tree has no manifest.json: it is not one verify-nogpl.ps1 assembled." }
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if (@($manifest.failures).Count -ne 0) { throw "The manifest at $tree records failures: $(@($manifest.failures) -join '; ')" }
    $root = (Resolve-Path $tree).Path
    $actual = @(Get-ChildItem $tree -Recurse -File | Where-Object Name -ne 'manifest.json' |
        ForEach-Object { $_.FullName.Substring($root.Length).TrimStart('\', '/').Replace('\', '/') } | Sort-Object)
    $listed = @($manifest.files | ForEach-Object path | Sort-Object)
    $unlisted = @($actual | Where-Object { $listed -notcontains $_ })
    $absent = @($listed | Where-Object { $actual -notcontains $_ })
    if ($unlisted.Count -gt 0) { throw "Files the manifest does not list: $($unlisted -join ', ')" }
    if ($absent.Count -gt 0) { throw "Files the manifest lists and the tree lacks: $($absent -join ', ')" }
    foreach ($entry in $manifest.files) {
        $hash = (Get-FileHash (Join-Path $tree $entry.path) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hash -cne $entry.sha256) { throw "$($entry.path) does not have the SHA-256 its manifest records." }
    }
    return $listed.Count
}

function Install-Tree([string] $staged, [string] $target, [string] $marker, [string] $markerValue) {
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    Move-Item $staged $target
    Set-Content $marker $markerValue -NoNewline -Encoding ascii
}

$mutex = [System.Threading.Mutex]::new($false, 'Local\ApReelumeFetchLibVlc')
[void]$mutex.WaitOne()
try {
    $root = $Destination
    New-Item -ItemType Directory -Force $root | Out-Null
    foreach ($arch in $architectures) {
        $asset = $lock.assets.$arch
        $target = Join-Path $root "win-$arch"
        $marker = "$target.sha512"
        $staged = Join-Path $root ".staging-$arch-$PID"
        if (Test-Path $staged) { Remove-Item $staged -Recurse -Force }

        if ($FromTree) {
            Copy-Item $FromTree $staged -Recurse
            $count = Assert-MatchesManifest $staged
            Install-Tree $staged $target $marker 'unpinned'
            Write-Warning "LibVLC win-${arch}: installed an UNPINNED tree from $FromTree ($count files). Only for testing before publication."
            continue
        }

        $expected = $asset.sha512
        $current = if (Test-Path $marker) { Get-Content $marker -Raw } else { $null }
        if (-not $expected) {
            if ($current -ceq 'unpinned' -and (Test-Path $target)) {
                Write-Warning "LibVLC win-${arch}: libvlc.lock.json pins nothing yet; using the unpinned tree installed with -FromTree."
                continue
            }
            throw "libvlc.lock.json pins no $arch tree yet: publish one with libvlc-publish.yml, or install one with -FromTree."
        }
        if ($current -ceq $expected -and (Test-Path $target)) { continue }

        $zip = "$staged.zip"
        $url = "https://github.com/$($lock.repository)/releases/download/$($lock.tag)/$($asset.fileName)"
        foreach ($attempt in 1..3) {
            try { Invoke-WebRequest $url -OutFile $zip; break }
            catch { if ($attempt -eq 3) { throw }; Start-Sleep -Seconds (10 * $attempt) }
        }
        $actual = [Convert]::ToHexString([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($zip))).ToLowerInvariant()
        if ($actual -cne $expected) {
            Remove-Item $zip -Force
            throw "$url is not the pinned $arch tree: SHA-512 $actual"
        }
        Expand-Archive $zip $staged -Force
        Remove-Item $zip -Force
        $count = Assert-MatchesManifest $staged
        Install-Tree $staged $target $marker $expected
        "LibVLC win-${arch}: $($lock.tag), SHA-512 matches, $count files match the manifest"
    }
}
finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
