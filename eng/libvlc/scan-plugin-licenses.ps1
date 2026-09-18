# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions

<#
.SYNOPSIS
    Names every LibVLC plugin in a directory that carries GPL code, from the VLC sources that build it.

.DESCRIPTION
    ENG-014 closed on 2026-09-14 saying three plugins of the shipped package were GPL, and the
    ENG-013 spike the same day called its own build "without GPL". Both measured one thing: the
    `--enable-gpl` string FFmpeg embeds in its configure line. That string is real, but it only sees
    FFmpeg. VLC's own configure has NO GPL switch at all -- `contrib/bootstrap --disable-gpl` only
    drops third-party libraries -- so a module whose own source file is GPL is built either way and
    carries no string that says so. Measured on 2026-09-18: the shipped package has eleven such
    plugins, lua and the deinterlacer among them.

    This reads the licence where it is written: the header of every source file each plugin is built
    from, following `#include "..."` into the tree, because that is how `libi420_rgb_sse2_plugin`
    gets GPL code -- it includes a GPL header of its MMX sibling and lists no GPL file of its own.
    It reports, per plugin in -PluginDir:

      * SourceHeader -- a source or included header says "GNU General Public" and not "Lesser".
      * FfmpegGplBuild -- the binary carries `--enable-gpl` (FFmpeg compiled as GPL).

    What it cannot see is a GPL third-party library linked statically into an LGPL module: that
    leaves no licence text behind. On 2026-09-18 the shipped `libts_plugin.dll` was such a case
    (aribb24 inside, found by its log strings). For a build of our own that class is closed another
    way -- `contrib/bootstrap --disable-gpl` refuses to build any library whose recipe says
    REQUIRE_GPL -- and verify-nogpl.ps1 checks the contrib prefix for exactly that. This script says
    so in its output instead of letting a clean result read as a certificate.

    IT CANNOT GO QUIET. A plugin in the directory that no Makefile.am declares, a declared source
    that does not exist, or a scan that classifies nothing as GPL while x264's GPL header is in the
    tree, all fail the run instead of shortening the list.

.PARAMETER VlcSource
    A VLC source tree at the tag the plugins were built from.

.PARAMETER PluginDir
    The directory holding the *_plugin.dll files (searched recursively).

.PARAMETER Json
    Emit the result as JSON instead of text.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $VlcSource,
    [Parameter(Mandatory)] [string] $PluginDir,
    [switch] $Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$modules = Join-Path $VlcSource 'modules'
if (-not (Test-Path (Join-Path $modules 'Makefile.am'))) {
    throw "'$VlcSource' is not a VLC source tree: modules/Makefile.am is missing."
}
if (-not (Test-Path $PluginDir)) { throw "Plugin directory '$PluginDir' does not exist." }

$licenceCache = @{}
function Get-Licence([string] $path) {
    if ($licenceCache.ContainsKey($path)) { return $licenceCache[$path] }
    $head = [IO.File]::ReadAllText($path)
    if ($head.Length -gt 4000) { $head = $head.Substring(0, 4000) }
    $licence = if ($head -match 'Lesser General Public') { 'LGPL' }
               elseif ($head -match 'GNU General Public') { 'GPL' }
               else { 'other' }
    $licenceCache[$path] = $licence
    return $licence
}

$includeDirs = @('include', 'src', 'modules') | ForEach-Object { Join-Path $VlcSource $_ }
$includePattern = [regex]'(?m)^\s*#\s*include\s*"([^"]+)"'
function Get-IncludeClosure([string[]] $files) {
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $stack = [System.Collections.Generic.Stack[string]]::new()
    foreach ($f in $files) { $stack.Push($f) }
    while ($stack.Count -gt 0) {
        $file = $stack.Pop()
        if (-not $seen.Add($file)) { continue }
        foreach ($m in $includePattern.Matches([IO.File]::ReadAllText($file))) {
            foreach ($dir in @((Split-Path $file -Parent)) + $includeDirs) {
                $candidate = [IO.Path]::GetFullPath((Join-Path $dir $m.Groups[1].Value))
                if (Test-Path $candidate -PathType Leaf) { $stack.Push($candidate); break }
            }
        }
    }
    return $seen
}

# Every Makefile.am under modules/ is included from modules/Makefile.am, so its paths are relative
# to modules/; the SUBDIRS ones (hw/mmal) are relative to their own folder. Both are tried.
$assignment = [regex]'^(lib\w+?_plugin)_la_SOURCES\s*\+?=\s*(.*)$'
$reference = [regex]'\$\((lib\w+?_plugin)_la_SOURCES\)'
$declared = @{}
$aliases = @{}
$missing = [System.Collections.Generic.List[string]]::new()
foreach ($makefile in Get-ChildItem $modules -Recurse -Filter 'Makefile.am') {
    $text = [IO.File]::ReadAllText($makefile.FullName) -replace "\\\r?\n", ' '
    foreach ($line in $text -split "\r?\n") {
        $m = $assignment.Match($line.Trim())
        if (-not $m.Success) { continue }
        $name = $m.Groups[1].Value
        if (-not $declared.ContainsKey($name)) { $declared[$name] = [System.Collections.Generic.HashSet[string]]::new() }
        foreach ($r in $reference.Matches($m.Groups[2].Value)) { $aliases[$name] = $r.Groups[1].Value }
        foreach ($token in $m.Groups[2].Value -split '\s+') {
            if ($token -notmatch '\.(c|cpp|m|h)$' -or $token -match '\$\(') { continue }
            $hit = @((Join-Path $modules $token), (Join-Path $makefile.DirectoryName $token)) |
                ForEach-Object { [IO.Path]::GetFullPath($_) } |
                Where-Object { Test-Path $_ -PathType Leaf } | Select-Object -First 1
            # dummy.cpp is a placeholder automake is told about to force C++ linking; it never exists.
            if ($hit) { [void]$declared[$name].Add($hit) }
            elseif ((Split-Path $token -Leaf) -ne 'dummy.cpp') { $missing.Add("$name -> $token") }
        }
    }
}
foreach ($alias in $aliases.GetEnumerator()) {
    foreach ($f in $declared[$alias.Value]) { [void]$declared[$alias.Key].Add($f) }
}

if ($missing.Count -gt 0) {
    throw "Declared sources that do not exist, so their licence cannot be read:`n  $($missing -join "`n  ")"
}

$x264 = Join-Path $modules 'codec/x264.c'
if ((Get-Licence $x264) -ne 'GPL') {
    throw "Control failed: modules/codec/x264.c is GPL and was not classified as GPL. The licence reader is blind."
}

$root = [IO.Path]::GetFullPath($VlcSource)
$gplMarker = [Text.Encoding]::ASCII.GetBytes('--enable-gpl')
function Test-Contains([byte[]] $haystack, [byte[]] $needle) {
    $first = $needle[0]
    for ($i = [Array]::IndexOf($haystack, $first); $i -ge 0 -and $i -le $haystack.Length - $needle.Length; $i = [Array]::IndexOf($haystack, $first, $i + 1)) {
        $match = $true
        for ($j = 1; $j -lt $needle.Length; $j++) { if ($haystack[$i + $j] -ne $needle[$j]) { $match = $false; break } }
        if ($match) { return $true }
    }
    return $false
}

$plugins = @(Get-ChildItem $PluginDir -Recurse -Filter '*_plugin.dll' | Sort-Object Name)
if ($plugins.Count -eq 0) { throw "No *_plugin.dll under '$PluginDir'." }

$unmapped = [System.Collections.Generic.List[string]]::new()
$findings = [System.Collections.Generic.List[object]]::new()
foreach ($plugin in $plugins) {
    $name = $plugin.BaseName
    if (-not $declared.ContainsKey($name)) { $unmapped.Add($name); continue }
    $gplSources = @(Get-IncludeClosure @($declared[$name]) |
        Where-Object { (Get-Licence $_) -eq 'GPL' } |
        ForEach-Object { [IO.Path]::GetRelativePath($root, $_).Replace('\', '/') } | Sort-Object)
    $reasons = @()
    if ($gplSources.Count -gt 0) { $reasons += 'SourceHeader' }
    if (Test-Contains ([IO.File]::ReadAllBytes($plugin.FullName)) $gplMarker) { $reasons += 'FfmpegGplBuild' }
    if ($reasons.Count -gt 0) {
        $findings.Add([pscustomobject]@{
            plugin     = $plugin.FullName.Substring((Resolve-Path $PluginDir).Path.Length).TrimStart('\', '/').Replace('\', '/')
            reasons    = $reasons
            gplSources = $gplSources
        })
    }
}

if ($unmapped.Count -gt 0) {
    throw "Plugins no Makefile.am declares, so their licence is unknown:`n  $($unmapped -join "`n  ")"
}

$result = [pscustomobject]@{
    vlcSource      = $root
    pluginCount    = $plugins.Count
    gplCount       = $findings.Count
    gpl            = $findings
    notSeen        = 'A GPL third-party library linked statically into an LGPL module leaves no licence text; see verify-nogpl.ps1.'
}

if ($Json) { $result | ConvertTo-Json -Depth 5; return }

"Plugins scanned: $($plugins.Count)"
"Plugins with GPL code: $($findings.Count)"
foreach ($f in $findings) {
    "  $($f.plugin)  [$($f.reasons -join ', ')]"
    foreach ($s in $f.gplSources) { "      $s" }
}
"Not seen by this scan: $($result.notSeen)"
