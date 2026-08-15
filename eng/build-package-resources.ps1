# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Builds the resources Windows reads the application's description from, in every language the
    manifest declares.

.DESCRIPTION
    The manifest used to carry its description as **one string with a slash inside it** — "Biblioteca
    y reproductor de vídeo local / Local video library and player" — which Windows shows exactly like
    that to a Spanish reader and to an English one alike. Declaring `<Resource Language="es-ES" />`
    and `<Resource Language="en-US" />` does not localise anything on its own: what localises is an
    `ms-resource:` reference and one resource per language, which is what this builds.

    The text is read from the two READMEs rather than written here, so the MSIX and the winget entry
    say the same thing in each language. The languages come from the manifest, so declaring a third
    one is the only edit that adds it.

    The output is deterministic: two builds of one commit in two different directories produce the
    same bytes, measured on 2026-08-15, which is what the reproducibility comparison requires.
#>
[CmdletBinding()]
param(
    # The manifest as it will ship, so the resource map is named after the package identity Windows
    # will resolve `ms-resource:` against.
    [Parameter(Mandatory)]
    [string]$ManifestPath,

    # A scratch folder outside the payload. Whatever is under it is indexed.
    [Parameter(Mandatory)]
    [string]$StageRoot,

    [Parameter(Mandatory)]
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'

$makePri = & (Join-Path $PSScriptRoot 'find-sdk-tool.ps1') -Name 'makepri.exe'
$readSummary = Join-Path $PSScriptRoot 'read-product-summary.ps1'

[xml]$manifest = Get-Content -LiteralPath $ManifestPath -Raw
$languages = @($manifest.Package.Resources.Resource | ForEach-Object { $_.Language })
if ($languages.Count -eq 0) { throw "$ManifestPath declares no languages, so there is nothing to build." }

if (Test-Path -LiteralPath $StageRoot) { Remove-Item -LiteralPath $StageRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $StageRoot | Out-Null

foreach ($language in $languages) {
    $readme = Join-Path $RepoRoot "README.$($language.Split('-')[0]).md"
    if (-not (Test-Path -LiteralPath $readme)) {
        throw "The manifest declares $language and there is no $([IO.Path]::GetFileName($readme)) to describe the product in it."
    }

    $summary = & $readSummary -Path $readme
    # 2048 is the manifest's own limit for a description, and Windows elides long ones anyway.
    if ($summary.Length -gt 2048) { throw "The $language description is $($summary.Length) characters; the manifest allows 2048." }

    New-Item -ItemType Directory -Force -Path (Join-Path $StageRoot $language) | Out-Null
    # Written as text rather than built with the XML DOM, and that is not a shortcut: the DOM emits
    # `xml:space` as a namespace of its own invention — `d2p1:space` with a declaration beside it —
    # and makepri answers "PRI224: root node not found", which names neither the attribute nor the
    # file. Measured on 2026-08-15.
    $resw = @"
<?xml version="1.0" encoding="utf-8"?>
<root>
  <data name="AppDescription" xml:space="preserve"><value>$([Security.SecurityElement]::Escape($summary))</value></data>
</root>
"@
    Set-Content -LiteralPath (Join-Path $StageRoot "$language/Resources.resw") -Value $resw -Encoding utf8
}

# The manifest is indexed with them: without it the resource map takes the folder's name and
# `ms-resource:AppDescription` resolves against an identity Windows never looks for.
Copy-Item -LiteralPath $ManifestPath -Destination (Join-Path $StageRoot 'AppxManifest.xml') -Force

# The configuration is written outside the indexed folder on purpose; inside, it indexes itself.
$configPath = Join-Path (Split-Path -Parent $StageRoot) 'priconfig.xml'
& $makePri createconfig /cf $configPath /dq ($languages -join '_') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri could not write its configuration.' }

$priPath = Join-Path (Split-Path -Parent $StageRoot) 'resources.pri'
& $makePri new /pr $StageRoot /cf $configPath /of $priPath /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri could not build the resources.' }

# Dumped and read back, because a resource file that built is not the same as one that carries what
# the manifest asks for. What comes out of here is what the packaging report records.
$dumpPath = Join-Path (Split-Path -Parent $StageRoot) 'resources-dump.xml'
& $makePri dump /if $priPath /of $dumpPath /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'makepri could not read back the resources it had just built.' }

[xml]$dump = Get-Content -LiteralPath $dumpPath -Raw
$described = @($dump.PriInfo.ResourceMap.ResourceMapSubtree |
    Where-Object { $_.name -eq 'Resources' } |
    ForEach-Object { $_.NamedResource } |
    Where-Object { $_.name -eq 'AppDescription' })
if ($described.Count -ne 1) {
    throw "The built resources carry $($described.Count) AppDescription entries; the manifest references exactly one."
}

# Kept as pairs rather than two lists side by side: the candidates come out in the order the default
# language wins, so a sorted list of languages beside an unsorted list of texts pairs each language
# with somebody else's sentence.
$described = @($described[0].Candidate | ForEach-Object {
        [ordered]@{
            language = ($_.qualifiers -replace '^Language-', '')
            value    = $_.Value
        }
    } | Sort-Object { $_.language })

$missing = @($languages | Where-Object { $_ -notin @($described | ForEach-Object { $_.language }) })
if ($missing.Count -gt 0) {
    throw "The manifest declares $($missing -join ', ') and the resources carry no description in $($missing -join ', ')."
}

$values = @($described | ForEach-Object { $_.value })
if (@($values | Sort-Object -Unique).Count -ne $values.Count) {
    throw 'Two languages carry the same description, so one of the READMEs is not describing the product in its own language.'
}

[ordered]@{
    Path      = $priPath
    MapName   = $dump.PriInfo.ResourceMap.name
    Described = $described
}
