#Requires -Version 7
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# Bateria de cierre-acta.ps1. Cada caso fabrica un transcript y un repositorio git desechables, y
# comprueba el codigo de salida y lo que el acta nombra. -Script apunta a una copia para el control
# de mutacion: una bateria que nunca ha fallado no mide nada.
[CmdletBinding()]
param([string]$Script = (Join-Path $PSScriptRoot '../cierre-acta.ps1'))

$ErrorActionPreference = 'Stop'
$raiz = Join-Path ([System.IO.Path]::GetTempPath()) ("acta-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $raiz | Out-Null
$fallos = 0

$todas = @(
    'pwsh -NoProfile -File eng/watch-ci.ps1 -Sha abc',
    'dotnet format --verify-no-changes --severity warn',
    'dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1',
    'dotnet test tests/X -c Release',
    'pwsh -NoProfile -File eng/verify-docs.ps1',
    'pwsh -NoProfile -File eng/preview-coverage-floors.ps1 -Suites Domain.Tests',
    'pwsh -NoProfile -File "$it\cierre-contexto.ps1" -Repo D:/x > s.txt',
    'pwsh -NoProfile -File "$it\cierre-lenguaje.ps1" -RepoRuta D:/x',
    'pwsh -NoProfile -File "$it\cierre-memorias.ps1" -Memorias C:/m',
    'pwsh -NoProfile -File eng/list-pending.ps1'
)

# Un transcript con cada orden como tool_use y su tool_result emparejado por id. Las que se pasan en
# $fallan llevan is_error, como deja el harness una orden que salio distinto de cero.
function Transcript([string[]]$ordenes, [string[]]$fallan = @()) {
    $ruta = Join-Path $raiz ("t-" + [guid]::NewGuid().ToString('N') + '.jsonl')
    $n = 0
    $lineas = foreach ($o in $ordenes) {
        $n++; $id = "toolu_$n"
        @{ type = 'assistant'; message = @{ content = @(@{ type = 'tool_use'; id = $id; name = 'Bash'; input = @{ command = $o } }) } } |
            ConvertTo-Json -Depth 10 -Compress
        $error = $fallan -contains $o
        @{ type = 'user'; message = @{ content = @(@{ type = 'tool_result'; tool_use_id = $id; is_error = $error; content = $(if ($error) { 'Exit code 1' } else { 'ok' }) }) } } |
            ConvertTo-Json -Depth 10 -Compress
    }
    Set-Content -LiteralPath $ruta -Value $lineas -Encoding utf8
    $ruta
}

function Repo([string[]]$archivos, [string]$base = 'main') {
    $dir = Join-Path $raiz ("r-" + [guid]::NewGuid().ToString('N'))
    git init -q -b $base $dir
    git -C $dir config user.email t@t; git -C $dir config user.name t
    Set-Content (Join-Path $dir 'README.md') 'x'; git -C $dir add -A; git -C $dir commit -qm base
    git -C $dir checkout -q -b tanda
    foreach ($a in $archivos) {
        $p = Join-Path $dir $a; New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null
        Set-Content $p 'x'
    }
    if ($archivos) { git -C $dir add -A; git -C $dir commit -qm tanda }
    $dir
}

function Caso([string]$nombre, [int]$esperado, [string]$debeNombrar, [string]$transcript, [string]$repo, [hashtable]$extra = @{}) {
    $argumentos = @('-NoProfile', '-File', $Script, '-Repo', $repo, '-HerramientasCompartidas', '')
    if ($transcript) { $argumentos += @('-Transcript', $transcript) }
    $salida = & pwsh @argumentos 2>&1 | Out-String
    $codigo = $LASTEXITCODE
    $ok = ($codigo -eq $esperado) -and (-not $debeNombrar -or $salida -match [regex]::Escape($debeNombrar))
    if (-not $ok) { $script:fallos++; Write-Output $salida }
    Write-Output ("{0}  {1}  (salida {2}, esperado {3})" -f ($(if ($ok) { 'ok' } else { 'FALLA' })), $nombre, $codigo, $esperado)
}

$relevo = @('docs/NEXT-SESSION.es.md', 'docs/NEXT-SESSION.en.md')
try {
    Caso 'todas las fases con evidencia'            0 'completa'                (Transcript $todas) (Repo $relevo)
    Caso 'sin verify-docs'                          1 'verify-docs'             (Transcript ($todas -notmatch 'verify-docs')) (Repo $relevo)
    Caso 'sin vigilar el CI'                        1 'watch-ci'                (Transcript ($todas -notmatch 'watch-ci')) (Repo $relevo)
    Caso 'relevo solo en un idioma'                 1 'NEXT-SESSION'            (Transcript $todas) (Repo @('docs/NEXT-SESSION.es.md'))
    Caso 'codigo tocado sin prever suelos'          1 'preview-coverage-floors' (Transcript ($todas -notmatch 'preview-coverage')) (Repo ($relevo + 'src/A.cs'))
    Caso 'sin codigo, prever suelos no hace falta'  0 'completa'                (Transcript ($todas -notmatch 'preview-coverage')) (Repo $relevo)
    Caso 'citado en un grep no es ejecutado'        1 'watch-ci'                (Transcript (($todas -notmatch 'watch-ci') + 'grep -n watch-ci.ps1 SKILL.md')) (Repo $relevo)
    Caso 'leido con Get-Content no es ejecutado'    1 'cierre-memorias'         (Transcript (($todas -notmatch 'cierre-memorias') + 'Get-Content S:\x\cierre-memorias.ps1')) (Repo $relevo)
    Caso 'tras un comentario no es ejecutado'       1 'verify-docs'             (Transcript (($todas -notmatch 'verify-docs') + 'git status # luego eng/verify-docs.ps1')) (Repo $relevo)
    Caso 'una orden que fallo no cuenta'            1 'suite entera'            (Transcript $todas @('dotnet test tests/X -c Release')) (Repo $relevo)
    Caso 'dotnet test con --filter no es la suite'  1 'suite entera'            (Transcript (($todas -notmatch '^dotnet test') + 'dotnet test tests/X --filter Y')) (Repo $relevo)
    Caso 'invocado tras && cuenta'                  0 'completa'                (Transcript (($todas -notmatch 'verify-docs') + 'cd x && pwsh -NoProfile -File eng/verify-docs.ps1')) (Repo $relevo)
    Caso 'sin la rama base: no se pudo medir'       2 'NO SE PUDO MEDIR'        (Transcript $todas) (Repo $relevo 'trunk')
    Caso 'transcript inexistente: no se pudo medir' 2 'NO SE PUDO MEDIR'        (Join-Path $raiz 'no-hay.jsonl') (Repo $relevo)
    Caso 'transcript sin ordenes: no se pudo medir' 2 'NO SE PUDO MEDIR'        (Transcript @()) (Repo $relevo)
    Caso 'sin herramientas compartidas ni -Transcript' 2 'no configuradas'     '' (Repo $relevo)
}
finally {
    Remove-Item -Recurse -Force $raiz -ErrorAction SilentlyContinue
}

if ($fallos -gt 0) { Write-Output "BATERIA: $fallos caso(s) en rojo."; exit 1 }
Write-Output 'BATERIA: todos los casos en verde.'
exit 0
