#Requires -Version 7
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# Bateria de cierre-privacidad.ps1. Cada caso monta un repositorio desechable con main y una rama,
# planta una fuga en UNO de los seis puntos de paso y exige que el filtro la encuentre ahi y en
# ningun otro sitio. Existe porque la sonda del manifiesto llamaba al filtro comun directamente y
# ninguna prueba ejecutaba este guion: vaciar el relevo que le pasa al filtro no lo veia nadie, y
# ese es el camino del incidente de ENG-032. -Script apunta a una copia para el control de mutacion.
#
# Necesita el filtro comun (AP_SHARED_TOOLS); sin el, sale 2: no se pudo medir.
[CmdletBinding()]
param(
    [string]$Script = (Join-Path $PSScriptRoot '../cierre-privacidad.ps1'),
    [string]$Herramientas = $env:AP_SHARED_TOOLS
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Herramientas) -or -not (Test-Path -LiteralPath (Join-Path $Herramientas 'privacy-gate.ps1'))) {
    Write-Output 'BATERIA: NO SE PUDO MEDIR -- AP_SHARED_TOOLS no apunta al filtro comun.'
    exit 2
}
$raiz = Join-Path ([IO.Path]::GetTempPath()) ("privacidad-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $raiz | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
$fallos = 0

# Una ruta de una maquina, partida para que este fichero no la lleve escrita entera.
$fuga = 'ver C' + ':\Usuarios\alguien\notas.md'
# Un nombre de fichero que el filtro reconoce como carpeta de una persona.
$nombreConFuga = 'docs/Us' + 'ers/algu' + 'ien/notas.md'

function Escribir([string]$dir, [string]$ruta, [string]$texto) {
    $p = Join-Path $dir $ruta; New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null
    [IO.File]::WriteAllText($p, "$texto`n", $utf8)
}

# Un repositorio con main (lo ya publicado) y la rama «tanda». El relevo va en el arbol sin anadir,
# para que solo cuente por su punto y no tambien por el diff.
function Repo([hashtable]$publicado = @{}, [hashtable]$commit = @{}, [string]$mensaje = 'tanda limpia',
    [hashtable]$preparado = @{}, [string]$relevo = 'relevo limpio', [switch]$SinRelevo, [string[]]$copias = @()) {
    $dir = Join-Path $raiz ('r-' + [guid]::NewGuid().ToString('N'))
    git init -q -b main $dir
    git -C $dir config user.email t@t; git -C $dir config user.name t
    Escribir $dir 'README.md' 'x'
    foreach ($k in $publicado.Keys) { Escribir $dir $k $publicado[$k] }
    git -C $dir add -A; git -C $dir commit -qm base
    git -C $dir checkout -q -b tanda
    foreach ($k in $commit.Keys) { Escribir $dir $k $commit[$k] }
    foreach ($c in $copias) { $de, $a = $c -split '=>'; Copy-Item (Join-Path $dir $de) (Join-Path $dir $a) }
    git -C $dir add -A; git -C $dir commit -qm $mensaje --allow-empty
    foreach ($k in $preparado.Keys) { Escribir $dir $k $preparado[$k]; git -C $dir add -- $k }
    if (-not $SinRelevo) { foreach ($i in 'es', 'en') { Escribir $dir "docs/NEXT-SESSION.$i.md" $relevo } }
    $dir
}

function Caso([string]$nombre, [int]$esperado, [string[]]$puntos, [string]$repo, [string]$prompt, [string]$nota,
    [string]$herramientas = $Herramientas) {
    $argumentos = @('-NoProfile', '-File', $Script, '-Repo', $repo, '-Herramientas', $herramientas)
    if ($prompt) { $p = Join-Path $raiz ('p-' + [guid]::NewGuid().ToString('N')); [IO.File]::WriteAllText($p, "$prompt`n", $utf8); $argumentos += @('-Prompt', $p) }
    if ($nota) { $p = Join-Path $raiz ('n-' + [guid]::NewGuid().ToString('N')); [IO.File]::WriteAllText($p, "$nota`n", $utf8); $argumentos += @('-Nota', $p) }
    $salida = & pwsh @argumentos 2>&1 | Out-String
    $codigo = $LASTEXITCODE
    # Los puntos que sonaron, por su nombre: la fuga tiene que salir por el suyo y por ninguno mas.
    $sonaron = @([regex]::Matches($salida, '(?m)^FAIL: (\w+) \(') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    $ok = ($codigo -eq $esperado) -and (($sonaron -join ',') -eq ((@($puntos) | Sort-Object) -join ','))
    if (-not $ok) { $script:fallos++; Write-Output $salida }
    Write-Output ("{0}  {1}  (salida {2}, sonaron [{3}], esperado {4} [{5}])" -f ($(if ($ok) { 'ok' } else { 'FALLA' })), $nombre, $codigo, ($sonaron -join ','), $esperado, ($puntos -join ','))
}

try {
    Caso 'todo limpio'                          0 @()               (Repo -commit @{ 'docs/a.md' = 'texto' })
    Caso 'fuga en el mensaje del commit'        1 @('CommitMessage') (Repo -commit @{ 'docs/a.md' = 'texto' } -mensaje "docs: $fuga")
    Caso 'fuga en una linea commiteada'         1 @('AddedDiff')     (Repo -commit @{ 'docs/a.md' = $fuga })
    Caso 'fuga en una linea solo preparada'     1 @('AddedDiff')     (Repo -commit @{ 'docs/a.md' = 'texto' } -preparado @{ 'docs/b.md' = $fuga })
    Caso 'fuga en el nombre de un fichero'      1 @('TouchedTree')   (Repo -commit @{ $nombreConFuga = 'texto' })
    Caso 'fuga en el relevo (ENG-032)'          1 @('Handoff')       (Repo -relevo $fuga)
    Caso 'fuga en el prompt siguiente'          1 @('Prompt')        (Repo) $fuga
    Caso 'fuga en la nota del cajon'            1 @('Note')          (Repo) '' $fuga
    # Lo ya publicado copiado entero no es una linea nueva: git lo ve como copia, no como anadido.
    Caso 'una copia de lo publicado no suena'   0 @()               (Repo -publicado @{ 'docs/historia.md' = "$fuga`nmas texto`ny mas" } -copias @('docs/historia.md=>docs/historia-congelada.md'))
    Caso 'sin relevo: no se pudo medir'         2 @()               (Repo -SinRelevo)
    Caso 'sin herramientas: no se pudo medir'   2 @()               (Repo) '' '' ' '
}
finally {
    Remove-Item -Recurse -Force $raiz -ErrorAction SilentlyContinue
}

if ($fallos -gt 0) { Write-Output "BATERIA: $fallos caso(s) en rojo."; exit 1 }
Write-Output 'BATERIA: todos los casos en verde.'
exit 0
