#!/usr/bin/env pwsh
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    Cuánto tarda CI hoy, y en qué se le va el tiempo. Se mide, no se recuerda.

.DESCRIPTION
    Existe porque la guía llevaba el número escrito y **siempre estaba desfasado**. Dijo 55-80,
    luego 42-53, luego 49-57, y el 2026-09-04 ya eran 41-43 — cuatro cifras en cinco días, todas
    correctas el día que alguien las midió y ninguna correcta después. Un número que se guarda
    envejece en silencio, y el silencio es lo que hace que acabe justificando la decisión
    equivocada: leer «cortado a los 90 es un atasco» sobre una cifra vieja es leer un rojo por reloj
    como si fuera un cuelgue.

    No hay forma de que una prueba conteste esto. La duración vive en el servidor, no en el árbol,
    así que el mecanismo de cifras medidas —el de `<!--medido:clave-->`— no la alcanza; y una prueba
    que fuera a buscarla abriría una conexión que ninguna finalidad declara, que es la regla 2 de
    este repositorio. Por eso es un guion que se corre cuando hace falta la respuesta, y por eso la
    guía apunta aquí en vez de llevar el número dentro.

    Sólo mira runs que **terminaron**, y por defecto sólo los verdes: un run cancelado o que falló
    a la mitad no mide el trabajo entero, y meterlo en la cuenta la baja sin que nada haya mejorado.

.PARAMETER Limit
    Cuántos runs terminados mirar. Por defecto 10.

.PARAMETER IncludeFailed
    Incluye también los rojos. Sirve para comparar un run que se cayó contra los sanos del mismo
    día, que es como se distingue una máquina lenta de una contención propia.

.PARAMETER Detailed
    Añade el reparto por suite, leído de los registros. Cuesta una descarga de registro por run, así
    que va aparte: la cifra de arriba se contesta sin bajar nada.

.EXAMPLE
    pwsh -NoProfile -File eng/measure-ci-time.ps1
    pwsh -NoProfile -File eng/measure-ci-time.ps1 -Detailed -Limit 4
#>

[CmdletBinding()]
param(
    [ValidateRange(1, 50)]
    [int]$Limit = 10,

    [switch]$IncludeFailed,

    [switch]$Detailed
)

$ErrorActionPreference = 'Stop'

function Get-Runs {
    param([int]$Count, [bool]$WithFailed)

    # Se piden de más y se filtra aquí: `gh` cuenta los cancelados y los que siguen en curso dentro
    # del límite, así que pedir exactamente los que se quieren devuelve menos de los que se quieren.
    # La lista de campos va entre comillas y no suelta: sin ellas PowerShell la parte por las comas
    # y `gh` recibe «conclusion,» como si fuera un comando suyo.
    $fields = 'databaseId,conclusion,status,createdAt,updatedAt,headSha,displayTitle'
    $raw = & gh run list --workflow CI --limit ([Math]::Min($Count * 4, 100)) --json $fields 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "gh no pudo listar los runs: $raw"
    }

    $wanted = if ($WithFailed) { @('success', 'failure') } else { @('success') }
    return $raw
    | ConvertFrom-Json
    | Where-Object { $_.status -eq 'completed' -and $wanted -contains $_.conclusion }
    | Select-Object -First $Count
}

function Get-Minutes {
    param($Run)
    return [Math]::Round((([datetime]$Run.updatedAt) - ([datetime]$Run.createdAt)).TotalMinutes, 1)
}

function Get-SuiteMinutes {
    param([long]$RunId)

    $log = & gh run view $RunId --log 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $log) {
        Write-Warning "No se pudo leer el registro del run $RunId; su reparto se omite."
        return @()
    }

    # «Duration: 6 m 37 s - ApSolutions.LocalMedia.AccessibilityTests.dll». Una suite aparece varias
    # veces por run —el recorrido corre como verificación Y como puerta—, y eso NO se promedia: cada
    # pasada es tiempo de reloj que el run paga, así que se suman y se dice cuántas fueron.
    $pattern = 'Duration:\s*(?:(?<h>\d+)\s*h\s*)?(?:(?<m>\d+)\s*m\s*)?(?:(?<s>[\d.]+)\s*(?:s|ms))?\s*-\s*ApSolutions\.LocalMedia\.(?<suite>[A-Za-z.]+)\.dll'
    $seen = @{}
    foreach ($line in $log) {
        foreach ($match in [regex]::Matches($line, $pattern)) {
            $minutes = 0.0
            if ($match.Groups['h'].Success) { $minutes += [double]$match.Groups['h'].Value * 60 }
            if ($match.Groups['m'].Success) { $minutes += [double]$match.Groups['m'].Value }
            if ($match.Groups['s'].Success -and $line -notmatch 'Duration:\s*[\d.]+\s*ms') {
                $minutes += [double]$match.Groups['s'].Value / 60
            }

            $suite = $match.Groups['suite'].Value
            if (-not $seen.ContainsKey($suite)) { $seen[$suite] = @{ Minutes = 0.0; Passes = 0 } }
            $seen[$suite].Minutes += $minutes
            $seen[$suite].Passes += 1
        }
    }

    return $seen.GetEnumerator() | ForEach-Object {
        [pscustomobject]@{
            Suite   = $_.Key
            Minutes = [Math]::Round($_.Value.Minutes, 1)
            Passes  = $_.Value.Passes
        }
    } | Sort-Object -Property Minutes -Descending
}

$runs = @(Get-Runs -Count $Limit -WithFailed:$IncludeFailed.IsPresent)
if ($runs.Count -eq 0) {
    Write-Output 'No hay ningún run terminado que mirar.'
    exit 0
}

$rows = foreach ($run in $runs) {
    [pscustomobject]@{
        Run        = $run.databaseId
        Conclusion = $run.conclusion
        Minutes    = Get-Minutes -Run $run
        Day        = ([datetime]$run.createdAt).ToString('yyyy-MM-dd')
        Sha        = $run.headSha.Substring(0, 7)
    }
}

# La banda se calcula SÓLO sobre los verdes, aunque la tabla enseñe los rojos. Un run que falla se
# para donde falló —el del 2026-09-04 murió a los 29,3 min en una prueba de documentación—, así que
# meterlo en la mediana hace que CI parezca más rápido sin que nada haya mejorado. Es el mismo error
# que este repositorio ya cometió con un solo run: una cifra que mezcla cosas que no se comparan.
$green = @($rows | Where-Object { $_.Conclusion -eq 'success' } | Sort-Object -Property Minutes)

Write-Output ''
Write-Output "Duración de CI, medida ahora sobre $($rows.Count) run(s) terminados."
if ($rows.Count -lt $Limit) {
    # Se pedían más de los que había EN LA VENTANA, no de los que existen: la ventana se calcula a
    # partir de -Limit, así que pedir pocos mira poco atrás. Se dice, porque una respuesta más corta
    # de lo que se pidió y sin explicación se lee como «no hay más runs».
    Write-Output "Se pidieron $Limit y sólo se encontraron $($rows.Count) en la ventana mirada; súbelo con -Limit."
}

Write-Output ''
$rows | Format-Table -AutoSize | Out-String | Write-Output

if ($green.Count -eq 0) {
    Write-Output 'Ningún run verde en esta ventana, así que no hay banda que dar: lo que tarda un run'
    Write-Output 'que se cayó a la mitad no es lo que tarda el trabajo. Amplía con -Limit.'
    exit 0
}

$fastest = $green[0].Minutes
$slowest = $green[-1].Minutes
$median = $green[[int][Math]::Floor($green.Count / 2)].Minutes

Write-Output "Sobre los $($green.Count) verde(s): más rápido $fastest min · mediana $median min · más lento $slowest min."
if ($green.Count -lt $rows.Count) {
    Write-Output 'Los rojos salen en la tabla pero NO en esa banda: un run que falla se para donde falló.'
}

Write-Output ''

# El corte del flujo. Se lee del propio flujo en vez de escribirlo aquí, porque un segundo sitio con
# el mismo número es un sitio que puede discrepar.
$workflow = Join-Path $PSScriptRoot '..' '.github' 'workflows' 'ci.yml'
if (Test-Path $workflow) {
    $timeout = [regex]::Match((Get-Content -Raw $workflow), 'timeout-minutes:\s*(?<value>\d+)')
    if ($timeout.Success) {
        $cut = [int]$timeout.Groups['value'].Value
        $margin = [Math]::Round($cut - $slowest, 1)
        Write-Output "El flujo se corta a los $cut min, así que el peor de estos deja $margin min de margen."
        if ($margin -lt 10) {
            Write-Output 'AVISO: menos de diez minutos de margen. Un sorteo malo daría un rojo por reloj'
            Write-Output '       con nada roto, y eso se lee como un atasco. Mide antes de subir el techo:'
            Write-Output '       un run sano acercándose al corte significa que el trabajo ha crecido.'
        }

        Write-Output ''
    }
}

if (-not $Detailed) {
    Write-Output 'Para ver en qué se va el tiempo: -Detailed (baja un registro por run).'
    exit 0
}

foreach ($row in $rows) {
    Write-Output "--- run $($row.Run) ($($row.Minutes) min, $($row.Conclusion), $($row.Sha))"
    $suites = @(Get-SuiteMinutes -RunId $row.Run)
    if ($suites.Count -eq 0) {
        continue
    }

    $suites | Format-Table -AutoSize | Out-String | Write-Output
}

Write-Output 'La columna Passes es cuántas veces corrió esa suite en ese run: el recorrido corre'
Write-Output 'como verificación y otra vez como puerta, y las dos son tiempo que el run paga.'
Write-Output ''
Write-Output 'Una sola lectura no es una tendencia: la suite de integración ha dado 7,5 y 27 minutos'
Write-Output 'el mismo día con el mismo trabajo. Antes de perseguir un tiempo raro, compara TODAS las'
Write-Output 'suites de ese run con otro del mismo día — si sólo una se disparó, es contención suya y'
Write-Output 'no una máquina lenta.'
