#Requires -Version 7
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# cierre-privacidad.ps1 -- los seis puntos por los que sale algo de un cierre, por el filtro comun.
#
# POR QUE EXISTE (2026-09-19). Este repositorio es publico y ya publico nombres internos de la casa
# durante dos semanas (ENG-032): se filtraban los ficheros a ojo y nadie miraba el mensaje del
# commit. El filtro comun (privacy-gate.ps1) mira los seis puntos de paso y exige que se le declaren
# todos: uno sin declarar sale 2, porque «no lo he mirado» no es «esta limpio». Este guion los
# reune desde git y se los pasa, en modo publico.
#
# Los seis, y de donde salen aqui:
#   - el mensaje del commit: los mensajes de todo lo que la rama lleva sobre la base;
#   - las lineas anadidas del diff: base...HEAD y el stage, con COPIAS detectadas. Medido el
#     2026-09-19: al congelar el relevo con git mv y escribir uno nuevo en su sitio, git no ve un
#     renombre sino un fichero nuevo, y sus 16.000 lineas ya publicadas hacian sonar el filtro 45
#     veces. Con --find-copies-harder quedan las 14 que de verdad cambiaron;
#   - el arbol que se toco: los nombres de los ficheros;
#   - el relevo: docs/NEXT-SESSION.{es,en}.md;
#   - el prompt de la sesion siguiente y la nota del cajon: con -Prompt y -Nota. Sin ellos se
#     declara un fichero vacio, que es decir por escrito que no hay nada, y deja rastro.
#
# Salida: la del filtro comun (0 limpio, 1 encontrado y redactado, 2 no se pudo medir).
[CmdletBinding()]
param(
    [string]$Herramientas = $env:AP_SHARED_TOOLS,
    [string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    [string]$Base = 'main',
    [string]$Prompt,
    [string]$Nota
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Herramientas)) { Write-Output 'UNMEASURED: la variable AP_SHARED_TOOLS no esta puesta'; exit 2 }
$puerta = Join-Path $Herramientas 'privacy-gate.ps1'
if (-not (Test-Path -LiteralPath $puerta)) { Write-Output 'UNMEASURED: no existe privacy-gate.ps1 en las herramientas compartidas'; exit 2 }

function Leer-Git([string[]]$argumentos) {
    $salida = & git -C $Repo @argumentos 2>$null
    if ($LASTEXITCODE -ne 0) { Write-Output "UNMEASURED: git $($argumentos -join ' ') salio $LASTEXITCODE"; exit 2 }
    $salida
}

$carpeta = Join-Path ([IO.Path]::GetTempPath()) ('cierre-privacidad-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $carpeta | Out-Null
try {
    $utf8 = [Text.UTF8Encoding]::new($false)
    function Punto([string]$nombre, [string[]]$lineas) {
        $ruta = Join-Path $carpeta $nombre
        [IO.File]::WriteAllText($ruta, ((@($lineas) -join "`n") + "`n"), $utf8)
        $ruta
    }

    $mensajes = Leer-Git @('log', '--format=%B', "$Base..HEAD")
    $diffs = @(Leer-Git @('diff', '--find-copies-harder', "$Base...HEAD")) + @(Leer-Git @('diff', '--find-copies-harder', '--cached'))
    $anadidas = $diffs | Where-Object { $_ -match '^\+' -and $_ -notmatch '^\+\+\+ ' } | ForEach-Object { $_.Substring(1) }
    $tocados = @(Leer-Git @('diff', '--name-only', "$Base...HEAD")) + @(Leer-Git @('diff', '--name-only', '--cached')) | Sort-Object -Unique

    $relevo = foreach ($idioma in 'es', 'en') {
        $ruta = Join-Path $Repo "docs/NEXT-SESSION.$idioma.md"
        if (-not (Test-Path -LiteralPath $ruta)) { Write-Output "UNMEASURED: no existe docs/NEXT-SESSION.$idioma.md"; exit 2 }
        [IO.File]::ReadAllText($ruta)
    }

    $argumentos = @(
        '-NoProfile', '-File', $puerta, '-Public',
        '-CommitMessage', (Punto 'commit.txt' $mensajes),
        '-AddedDiff', (Punto 'diff.txt' $anadidas),
        '-TouchedTree', (Punto 'arbol.txt' $tocados),
        '-Handoff', (Punto 'relevo.txt' $relevo),
        '-Prompt', $(if ($Prompt) { $Prompt } else { Punto 'prompt.txt' @() }),
        '-Note', $(if ($Nota) { $Nota } else { Punto 'nota.txt' @() })
    )
    & pwsh @argumentos
    exit $LASTEXITCODE
}
finally {
    Remove-Item -Recurse -Force -LiteralPath $carpeta -ErrorAction SilentlyContinue
}
