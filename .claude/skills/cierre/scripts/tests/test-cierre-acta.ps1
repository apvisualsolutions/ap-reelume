#Requires -Version 7
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# Bateria de cierre-acta.ps1. Cada caso fabrica un transcript, un repositorio git y los ficheros de
# efecto de las comprobaciones compartidas, todo desechable, y comprueba el codigo de salida, lo que
# el acta nombra y el recibo que deja. -Script apunta a una copia para el control de mutacion: una
# bateria que nunca ha fallado no mide nada.
[CmdletBinding()]
param([string]$Script = (Join-Path $PSScriptRoot '../cierre-acta.ps1'))

$ErrorActionPreference = 'Stop'
$raiz = Join-Path ([System.IO.Path]::GetTempPath()) ("acta-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $raiz | Out-Null
$fallos = 0
$marca = '2026-09-19T10:00:00Z'
$despues = '2026-09-19T11:00:00Z'
$antes = '2026-09-19T09:00:00Z'

$todas = @(
    'pwsh -NoProfile -File eng/watch-ci.ps1 -Sha abc',
    'dotnet format --verify-no-changes --severity warn',
    'dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1',
    'dotnet test tests/X -c Release',
    'pwsh -NoProfile -File eng/verify-docs.ps1',
    'pwsh -NoProfile -File eng/preview-coverage-floors.ps1 -Suites Domain.Tests',
    'pwsh -NoProfile -File eng/list-pending.ps1',
    'pwsh -NoProfile -File eng/check-handoff.ps1'
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

# Un repositorio con main y una rama «tanda». Lo que va en $archivos se commitea en la tanda; lo que
# va en $sueltos se deja en el arbol sin anadir.
function Repo([string[]]$archivos, [string]$base = 'main', [string[]]$sueltos = @()) {
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
    foreach ($s in $sueltos) {
        $p = Join-Path $dir $s; New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null
        Set-Content $p 'x'
    }
    $dir
}

# Los ficheros de efecto que deja cierre-compartidas.ps1. $codigos: nombre -> EXIT; $horas: nombre -> AT.
function Efectos([hashtable]$codigos = @{}, [hashtable]$horas = @{}, [string[]]$faltan = @()) {
    $dir = Join-Path $raiz ("s-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir | Out-Null
    foreach ($nombre in 'contexto', 'lenguaje', 'memorias') {
        if ($faltan -contains $nombre) { continue }
        $codigo = if ($codigos.ContainsKey($nombre)) { $codigos[$nombre] } else { 0 }
        $hora = if ($horas.ContainsKey($nombre)) { $horas[$nombre] } else { $despues }
        Set-Content -LiteralPath (Join-Path $dir "closing-$nombre.txt") -Value @('salida', "EXIT=$codigo", "AT=$hora") -Encoding utf8
    }
    $dir
}

function Caso([string]$nombre, [int]$esperado, [string]$debeNombrar, [string]$transcript, [string]$repo,
    [string]$salidas = (Efectos), [string]$subido = 'tanda', [string]$desde = $marca) {
    $recibo = Join-Path $raiz ("recibo-" + [guid]::NewGuid().ToString('N') + '.json')
    $argumentos = @('-NoProfile', '-File', $Script, '-Repo', $repo, '-HerramientasCompartidas', '',
        '-Subido', $subido, '-Salidas', $salidas, '-Desde', $desde, '-Recibo', $recibo)
    if ($transcript) { $argumentos += @('-Transcript', $transcript) }
    $salida = & pwsh @argumentos 2>&1 | Out-String
    $codigo = $LASTEXITCODE
    # El recibo es lo que leen las puertas del plugin: tiene que llevar el mismo codigo que la salida.
    $enRecibo = if (Test-Path $recibo) { (Get-Content $recibo -Raw | ConvertFrom-Json).exitCode } else { 'sin recibo' }
    $ok = ($codigo -eq $esperado) -and ($enRecibo -eq $esperado) -and (-not $debeNombrar -or $salida -match [regex]::Escape($debeNombrar))
    if (-not $ok) { $script:fallos++; Write-Output $salida; Write-Output "recibo: $enRecibo" }
    Write-Output ("{0}  {1}  (salida {2}, recibo {3}, esperado {4})" -f ($(if ($ok) { 'ok' } else { 'FALLA' })), $nombre, $codigo, $enRecibo, $esperado)
}

$relevo = @('docs/NEXT-SESSION.es.md', 'docs/NEXT-SESSION.en.md')
try {
    Caso 'todas las fases con evidencia'            0 'completa'                (Transcript $todas) (Repo $relevo)
    Caso 'sin verify-docs'                          1 'verify-docs'             (Transcript ($todas -notmatch 'verify-docs')) (Repo $relevo)
    Caso 'sin vigilar el CI'                        1 'watch-ci'                (Transcript ($todas -notmatch 'watch-ci')) (Repo $relevo)
    Caso 'relevo solo en un idioma'                 1 'NEXT-SESSION'            (Transcript $todas) (Repo @('docs/NEXT-SESSION.es.md'))
    Caso 'sin medir el tope del relevo'             1 'check-handoff'           (Transcript ($todas -notmatch 'check-handoff')) (Repo $relevo)
    Caso 'codigo tocado sin prever suelos'          1 'preview-coverage-floors' (Transcript ($todas -notmatch 'preview-coverage')) (Repo ($relevo + 'src/A.cs')) (Efectos) 'main'
    Caso 'sin codigo, prever suelos no hace falta'  0 'completa'                (Transcript ($todas -notmatch 'preview-coverage')) (Repo $relevo)
    Caso 'citado en un grep no es ejecutado'        1 'watch-ci'                (Transcript (($todas -notmatch 'watch-ci') + 'grep -n watch-ci.ps1 SKILL.md')) (Repo $relevo)
    Caso 'tras un comentario no es ejecutado'       1 'verify-docs'             (Transcript (($todas -notmatch 'verify-docs') + 'git status # luego eng/verify-docs.ps1')) (Repo $relevo)
    Caso 'una orden que fallo no cuenta'            1 'suite entera'            (Transcript $todas @('dotnet test tests/X -c Release')) (Repo $relevo)
    Caso 'dotnet test con --filter no es la suite'  1 'suite entera'            (Transcript (($todas -notmatch '^dotnet test') + 'dotnet test tests/X --filter Y')) (Repo $relevo)
    Caso 'invocado tras && cuenta'                  0 'completa'                (Transcript (($todas -notmatch 'verify-docs') + 'cd x && pwsh -NoProfile -File eng/verify-docs.ps1')) (Repo $relevo)

    # ENG-037: las tres comprobaciones compartidas se miden por su efecto, no por el texto.
    Caso 'rama no tomada: el comando esta y el efecto no' 1 'closing-context' (Transcript ($todas + 'if ($it) { pwsh -NoProfile -File "$it\closing-context.ps1" -Repo x }')) (Repo $relevo) (Efectos -faltan @('contexto'))
    Caso 'una comprobacion que no pudo medir no cuenta' 1 'closing-language' (Transcript $todas) (Repo $relevo) (Efectos -codigos @{ lenguaje = 2 })
    Caso 'una comprobacion que senala si cuenta'    0 'completa'                (Transcript $todas) (Repo $relevo) (Efectos -codigos @{ contexto = 1; lenguaje = 1 })
    Caso 'un efecto anterior a la marca no cuenta'  1 'closing-memories'        (Transcript $todas) (Repo $relevo) (Efectos -horas @{ memorias = $antes })

    # El paso 0 que hacia cumplir pre-push-closing.sh, y el agujero que dejaba.
    Caso 'codigo sin subir en el cierre'            1 'solo toca docs'          (Transcript $todas) (Repo ($relevo + 'src/A.cs')) (Efectos) 'main'
    Caso 'eng/ sin subir en el cierre'              1 'eng/x.ps1'               (Transcript $todas) (Repo ($relevo + 'eng/x.ps1')) (Efectos) 'main'
    Caso 'solo docs sin subir en el cierre'         0 'completa'                (Transcript $todas) (Repo $relevo) (Efectos) 'main'
    Caso 'fichero nuevo sin anadir fuera de docs'   1 'src/nuevo.cs'            (Transcript $todas) (Repo $relevo 'main' @('src/nuevo.cs'))

    Caso 'sin la rama base: no se pudo medir'       2 'NO SE PUDO MEDIR'        (Transcript $todas) (Repo $relevo 'trunk')
    Caso 'transcript inexistente: no se pudo medir' 2 'NO SE PUDO MEDIR'        (Join-Path $raiz 'no-hay.jsonl') (Repo $relevo)
    Caso 'transcript sin ordenes: no se pudo medir' 2 'NO SE PUDO MEDIR'        (Transcript @()) (Repo $relevo)
    Caso 'sin herramientas compartidas ni -Transcript' 2 'no configuradas'     '' (Repo $relevo)
    Caso 'sin marca de cierre ni -Desde: no se pudo medir' 2 'marca de cierre' (Transcript $todas) (Repo $relevo) (Efectos) 'tanda' ''
}
finally {
    Remove-Item -Recurse -Force $raiz -ErrorAction SilentlyContinue
}

if ($fallos -gt 0) { Write-Output "BATERIA: $fallos caso(s) en rojo."; exit 1 }
Write-Output 'BATERIA: todos los casos en verde.'
exit 0
