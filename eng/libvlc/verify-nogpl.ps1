# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions

<#
.SYNOPSIS
    Turns a build-nogpl.sh install prefix into the LibVLC tree the application ships, and refuses it
    unless nothing in it is GPL (ENG-013, ENG-027).

.DESCRIPTION
    Four checks, each with a control that proves the instrument can see what it looks for. The
    reference is VideoLAN's own NuGet package for the same version, which IS built as GPL; a check
    that finds nothing there is blind, and the run fails instead of reporting a clean build.

      1. VLC's own modules. scan-plugin-licenses.ps1 reads every plugin's sources. Whatever it
         names is removed from the tree, recorded in the manifest, and the scan runs again: the
         second pass must name nothing. Control: the reference must have plugins named by it.
      2. FFmpeg compiled as GPL. The same scan reads `--enable-gpl` inside each binary. Control:
         the reference's libavcodec carries it.
      3. GPL third-party libraries. contrib/bootstrap writes `GPL := 1` into the Makefile it
         generates in contrib/contrib-<arch> when GPL is on; it must be absent, and
         `AD_CLAUSES := 1` present (FreeType under the FTL, ENG-025). The presence is also the
         control: this check read config.mak until 2026-09-18 — build.sh's compiler flags, where
         bootstrap writes nothing — and "GPL absent" passed on a file that could never contain it.
         Only the AD_CLAUSES half, which demands something, gave it away. Canary for the one such library the shipped package was found
         carrying (aribb24 inside libts, 2026-09-18): its log string must be in the reference and
         absent here.
      4. Subtitles still render: the FreeType text renderer must be in the tree, because the whole
         point of --enable-ad-clauses was to keep it.

    Plugins the reference has and this tree does not are listed in the manifest with the reason
    (removed as GPL, or never built). That list is not a failure — most of it is exactly the point —
    but it is the list a person reviews before the tree replaces the NuGet package.

.PARAMETER Install
    The install prefix build-nogpl.sh left (holds bin/ and lib/vlc/plugins/).

.PARAMETER VlcSource
    The patched VLC tree the plugins were built from (build-nogpl.sh leaves it next to install/).

.PARAMETER Reference
    VideoLAN.LibVLC.Windows for the same version and architecture, e.g.
    ~/.nuget/packages/videolan.libvlc.windows/3.0.23.1/build/x64.

.PARAMETER Destination
    Where to assemble the tree: libvlc.dll, libvlccore.dll, plugins/, hrtfs/ and manifest.json.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Install,
    [Parameter(Mandatory)] [string] $VlcSource,
    [Parameter(Mandatory)] [string] $Reference,
    [Parameter(Mandatory)] [string] $Destination
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scanner = Join-Path $PSScriptRoot 'scan-plugin-licenses.ps1'
function Invoke-Scan([string] $pluginDir) {
    $json = & $scanner -VlcSource $VlcSource -PluginDir $pluginDir -Json
    return ($json | Out-String | ConvertFrom-Json)
}
function Get-PluginNames([string] $dir) {
    return @(Get-ChildItem $dir -Recurse -Filter '*_plugin.dll' | ForEach-Object BaseName | Sort-Object -Unique)
}
function Test-FileContains([string] $path, [string] $text) {
    # Latin1 maps every byte to one char, so a binary reads without decoding errors.
    return [IO.File]::ReadAllText($path, [Text.Encoding]::Latin1).Contains($text)
}

$failures = [System.Collections.Generic.List[string]]::new()

# --- assemble ------------------------------------------------------------------------------------
$bin = Join-Path $Install 'bin'
$pluginsIn = Join-Path $Install 'lib/vlc/plugins'
foreach ($p in @((Join-Path $bin 'libvlc.dll'), (Join-Path $bin 'libvlccore.dll'), $pluginsIn)) {
    if (-not (Test-Path $p)) { throw "The install prefix lacks '$p': build-nogpl.sh did not finish." }
}
if (Test-Path $Destination) { Remove-Item $Destination -Recurse -Force }
New-Item -ItemType Directory $Destination | Out-Null
Copy-Item (Join-Path $bin 'libvlc.dll'), (Join-Path $bin 'libvlccore.dll') $Destination
Copy-Item $pluginsIn (Join-Path $Destination 'plugins') -Recurse
if (Test-Path (Join-Path $Install 'hrtfs')) { Copy-Item (Join-Path $Install 'hrtfs') $Destination -Recurse }
# Only the DLLs travel: import libraries, libtool files and a plugin cache that would list plugins
# this script is about to remove.
Get-ChildItem (Join-Path $Destination 'plugins') -Recurse -File |
    Where-Object { $_.Extension -ne '.dll' } | Remove-Item -Force
$plugins = Join-Path $Destination 'plugins'

# --- 1 and 2: the licence scan, with the reference as control -----------------------------------
$referenceScan = Invoke-Scan $Reference
if (-not ($referenceScan.gpl | Where-Object { $_.reasons -contains 'SourceHeader' })) {
    $failures.Add('Control failed: the reference package has no plugin with a GPL source, so the source scan is blind.')
}
if (-not ($referenceScan.gpl | Where-Object { $_.reasons -contains 'FfmpegGplBuild' })) {
    $failures.Add('Control failed: the reference package has no plugin carrying --enable-gpl, so the FFmpeg check is blind.')
}

$firstScan = Invoke-Scan $plugins
$removed = @(foreach ($finding in $firstScan.gpl) {
    # The scan names paths relative to the directory it was given, which here is plugins/.
    Remove-Item (Join-Path $plugins $finding.plugin) -Force
    [pscustomobject]@{ plugin = [IO.Path]::GetFileNameWithoutExtension($finding.plugin); reasons = $finding.reasons; gplSources = $finding.gplSources }
})
if (@($firstScan.gpl | Where-Object { $_.reasons -contains 'FfmpegGplBuild' }).Count -gt 0) {
    # FFmpeg as GPL is not something to prune: libavcodec is the decoder. It means the contribs
    # were built with GPL on, and the build is wrong.
    $failures.Add("FFmpeg was built with --enable-gpl: $(@($firstScan.gpl | Where-Object { $_.reasons -contains 'FfmpegGplBuild' } | ForEach-Object plugin) -join ', ')")
}
$secondScan = Invoke-Scan $plugins
if ($secondScan.gplCount -ne 0) {
    $failures.Add("After removal the scan still names: $(@($secondScan.gpl | ForEach-Object plugin) -join ', ')")
}

# --- 3: third-party libraries -------------------------------------------------------------------
$configs = @(Get-ChildItem (Join-Path $VlcSource 'contrib') -Directory -Filter 'contrib-*' |
    ForEach-Object { Join-Path $_.FullName 'Makefile' } | Where-Object { Test-Path $_ })
if ($configs.Count -ne 1) {
    $failures.Add("Expected one contrib Makefile under $VlcSource/contrib/contrib-*, found $($configs.Count).")
} else {
    $config = Get-Content $configs[0]
    # The absence below can only mean something if the pattern can match what bootstrap writes.
    $gplLine = '^GPL\s*:=\s*1'
    if (-not ('GPL := 1' -match $gplLine)) { $failures.Add('Control failed: the GPL pattern does not match the line bootstrap writes.') }
    if ($config -match $gplLine) { $failures.Add("the contrib Makefile enables GPL: $($configs[0])") }
    if (-not ($config -match '^AD_CLAUSES\s*:=\s*1')) { $failures.Add("the contrib Makefile lacks AD_CLAUSES: either FreeType was refused or this is not bootstrap's Makefile.") }
}
$aribCanary = 'arib parser was created'
$referenceTs = Get-ChildItem $Reference -Recurse -Filter 'libts_plugin.dll' | Select-Object -First 1
if (-not $referenceTs -or -not (Test-FileContains $referenceTs.FullName $aribCanary)) {
    $failures.Add('Control failed: the reference libts_plugin.dll does not carry the aribb24 string, so the canary is blind.')
}
$aribHits = @(Get-ChildItem $plugins -Recurse -Filter '*.dll' | Where-Object { Test-FileContains $_.FullName $aribCanary } | ForEach-Object Name)
if ($aribHits.Count -gt 0) { $failures.Add("aribb24 is linked into: $($aribHits -join ', ')") }

# The source scan reads the PATCHED tree, so it calls libdeinterlace clean whether or not the binary
# was built from that tree: measured on 2026-09-18, VideoLAN's own libdeinterlace — yadif inside —
# passed it. The binary has to say it too: the patch removes the "yadif" mode names, and the
# reference carries them.
$referenceDeint = Get-ChildItem $Reference -Recurse -Filter 'libdeinterlace_plugin.dll' | Select-Object -First 1
if (-not $referenceDeint -or -not (Test-FileContains $referenceDeint.FullName 'yadif2x')) {
    $failures.Add('Control failed: the reference libdeinterlace_plugin.dll does not name yadif2x, so the yadif check is blind.')
}
$ourDeint = Get-ChildItem $plugins -Recurse -Filter 'libdeinterlace_plugin.dll' | Select-Object -First 1
if (-not $ourDeint) {
    $failures.Add('libdeinterlace_plugin.dll is missing: interlaced video would not be deinterlaced.')
} elseif (Test-FileContains $ourDeint.FullName 'yadif2x') {
    $failures.Add('libdeinterlace_plugin.dll still names yadif2x: it was not built from the patched tree.')
}

# --- 4: subtitles --------------------------------------------------------------------------------
if (-not (Get-ChildItem $plugins -Recurse -Filter 'libfreetype_plugin.dll')) {
    $failures.Add('libfreetype_plugin.dll is missing: subtitles would not render.')
}

# --- what the reference has and this tree does not ----------------------------------------------
$removedNames = @($removed | ForEach-Object plugin)
$ours = Get-PluginNames $plugins
$missing = @(Get-PluginNames $Reference | Where-Object { $ours -notcontains $_ } | ForEach-Object {
    [pscustomobject]@{ plugin = $_; reason = if ($removedNames -contains $_) { 'removed: GPL source' } else { 'not built' } }
})
$extra = @($ours | Where-Object { (Get-PluginNames $Reference) -notcontains $_ })

# --- manifest ------------------------------------------------------------------------------------
$root = (Resolve-Path $Destination).Path
$files = @(Get-ChildItem $Destination -Recurse -File | Where-Object Name -ne 'manifest.json' | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path   = $_.FullName.Substring($root.Length).TrimStart('\', '/').Replace('\', '/')
        size   = $_.Length
        sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$manifest = [ordered]@{
    vlcCommit       = (git -C $VlcSource rev-parse HEAD)
    patches         = @(Get-ChildItem (Join-Path $PSScriptRoot 'patches') -Filter '*.patch' | Sort-Object Name | ForEach-Object Name)
    pluginCount     = $ours.Count
    removedAsGpl    = $removed
    missingVsReference = $missing
    extraVsReference   = $extra
    failures        = $failures
    files           = $files
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $Destination 'manifest.json') -Encoding utf8NoBOM

"Plugins: $($ours.Count) (reference: $((Get-PluginNames $Reference).Count))"
"Removed as GPL: $($removed.Count)"
foreach ($r in $removed) { "  $($r.plugin)  [$($r.reasons -join ', ')]" }
"Missing vs reference: $($missing.Count) ($(@($missing | Where-Object reason -eq 'not built').Count) never built)"
foreach ($m in $missing | Where-Object reason -eq 'not built') { "  $($m.plugin)" }
"Extra vs reference: $($extra.Count)"
if ($failures.Count -gt 0) {
    "FAILED:"
    foreach ($f in $failures) { "  $f" }
    exit 1
}
"verify-nogpl: no GPL in $Destination"
