#Requires -Version 7
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# cierre-compartidas.ps1 -- una de las tres comprobaciones de IT del cierre, con su EFECTO escrito.
#
# POR QUE EXISTE (ENG-037, 2026-09-19). El acta daba por ejecutadas las tres comprobaciones porque
# veia el texto del comando en el transcript, y en aquel cierre estaban dentro de la rama de un `if`
# que no se tomo: la variable AP_SHARED_TOOLS no existia y ninguna corrio. Un texto no prueba que
# algo corriera; un fichero con su codigo de salida, si. Este guion corre la comprobacion y deja
# en el directorio COMUN de git un fichero con toda su salida, la linea EXIT=<codigo> y la hora.
# El acta lee ese fichero y no el transcript.
#
# POR QUE EN EL DIRECTORIO COMUN DE GIT: no se versiona (ningun `git add -A` se lo lleva) y es el
# mismo desde cualquier copia de trabajo paralela, que es donde vive tambien la marca del cierre.
#
# LA CARPETA DE LAS HERRAMIENTAS no se escribe aqui: este repositorio es publico. Sale de la
# variable AP_SHARED_TOOLS de la configuracion local. Sin ella se escribe igualmente el fichero, con
# EXIT=2, porque «no se pudo medir» tiene que dejar rastro y no puede leerse como un verde.
#
# LA CARPETA DE MEMORIA se calcula como la nombra Claude Code --la ruta del repositorio con `:` y las
# barras cambiadas por guiones-- y se pasa a mano: el barrido comun solo sabe leerla de los ajustes
# versionados, y en un repositorio publico ahi no puede ir una ruta de esta maquina.
#
# Salida: la de la comprobacion (0 limpio, 1 hallazgos, 2 no se pudo medir).
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('contexto', 'lenguaje', 'memorias')][string]$Comprobacion,
    [string]$Herramientas = $env:AP_SHARED_TOOLS,
    [string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    # Donde dejar el efecto. Por defecto, closing-outputs dentro del directorio comun de git.
    [string]$Salidas,
    # Carpeta de memoria. Por defecto, la que Claude Code asigna a este repositorio.
    [string]$Memorias
)

$ErrorActionPreference = 'Stop'

if (-not $Salidas) {
    $comun = & git -C $Repo rev-parse --path-format=absolute --git-common-dir 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $comun) { Write-Output 'UNMEASURED: no se pudo leer el directorio comun de git'; exit 2 }
    $Salidas = Join-Path $comun 'closing-outputs'
}
New-Item -ItemType Directory -Force -Path $Salidas | Out-Null
$efecto = Join-Path $Salidas "closing-$Comprobacion.txt"

function Escribir-Efecto([string[]]$lineas, [int]$codigo) {
    $pie = @("EXIT=$codigo", "AT=$((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ'))")
    [IO.File]::WriteAllLines($efecto, [string[]](@($lineas) + $pie), [Text.UTF8Encoding]::new($false))
}

if ([string]::IsNullOrWhiteSpace($Herramientas)) {
    Escribir-Efecto @('UNMEASURED: la variable AP_SHARED_TOOLS no esta puesta') 2
    Write-Output "UNMEASURED: la variable AP_SHARED_TOOLS no esta puesta (efecto en $efecto)"
    exit 2
}

$guion, $argumentos = switch ($Comprobacion) {
    'contexto' { 'closing-context.ps1', @('-Repo', $Repo) }
    'lenguaje' { 'closing-language.ps1', @('-RepoRuta', $Repo) }
    'memorias' {
        if (-not $Memorias) {
            $Memorias = Join-Path $env:USERPROFILE ('.claude\projects\' + ($Repo -replace '[:\\/]', '-') + '\memory')
        }
        'closing-memories.ps1', @('-Memorias', $Memorias)
    }
}
$ruta = Join-Path $Herramientas $guion
if (-not (Test-Path -LiteralPath $ruta)) {
    Escribir-Efecto @("UNMEASURED: no existe $guion en las herramientas compartidas") 2
    Write-Output "UNMEASURED: no existe $guion en las herramientas compartidas (efecto en $efecto)"
    exit 2
}

$salida = @(& pwsh -NoProfile -File $ruta @argumentos 2>&1 | ForEach-Object { [string]$_ })
$codigo = $LASTEXITCODE
# Un codigo que no es del contrato no ha medido nada.
if ($codigo -notin 0, 1, 2) { $salida += "UNMEASURED: $guion salio con $codigo, que no es del contrato"; $codigo = 2 }
Escribir-Efecto $salida $codigo

# A la conversacion, solo el codigo y las lineas que importan: se cierra con el contexto lleno.
$salida | Where-Object { $_ -match '^(FAIL|UNMEASURED|ABORTADO)' } | Select-Object -First 20
Write-Output "$Comprobacion : salio $codigo (salida entera en $efecto)"
exit $codigo
