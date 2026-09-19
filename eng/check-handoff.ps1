# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions

<#
.SYNOPSIS
    El relevo cabe en lo que alguien lee al empezar, y dice lo mismo en los dos idiomas.

.DESCRIPTION
    Por que existe (ENG-035, 2026-09-19). El relevo llego a 9.134 lineas y 670 KB en espanol, con
    7.618 en ingles y 42 encabezados de diferencia, y ninguna prueba lo miraba. Quien empieza una
    sesion no lo lee entero, asi que se perdia justo lo que habia que hacer: el bloque util del
    ultimo cierre media 56 lineas. La historia se congelo en docs/history/ y el relevo pasa a
    SOBRESCRIBIRSE en cada cierre.

    Lo que exige, a cada idioma:
      - que exista (si no, 2: no se pudo medir, nunca un verde);
      - 80 lineas como mucho y 6.144 bytes como mucho;
      - una fecha AAAA-MM-DD en la primera linea, para ver de un vistazo si esta caducado;
    y entre los dos idiomas:
      - la misma fecha en la primera linea;
      - la misma secuencia de niveles de encabezado. Contar encabezados no verifica una traduccion,
        pero caza el fallo que de verdad ocurre: una seccion que se anade en un idioma y se olvida
        en el otro.

.PARAMETER Root
    Raiz del arbol que se mide. Por defecto, este repositorio. La bateria la apunta a arboles de
    mentira bajo el temporal.

.OUTPUTS
    Codigos: 0 limpio - 1 hallazgos (FAIL: por cada uno) - 2 no se pudo medir.
#>
[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [int]$MaxLines = 80,
    [int]$MaxBytes = 6144
)

$ErrorActionPreference = 'Stop'

$languages = @('es', 'en')
$measured = @{}
foreach ($language in $languages) {
    $path = Join-Path $Root "docs/NEXT-SESSION.$language.md"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Write-Output "UNMEASURED: no existe docs/NEXT-SESSION.$language.md"
        exit 2
    }
    $bytes = [IO.File]::ReadAllBytes($path)
    $text = [Text.UTF8Encoding]::new($false).GetString($bytes)
    $lines = @($text -split "`r?`n")
    if ($lines.Count -gt 0 -and $lines[-1] -eq '') { $lines = @($lines[0..($lines.Count - 2)]) }
    $date = [regex]::Match($lines[0], '\b\d{4}-\d{2}-\d{2}\b').Value
    $headings = @($lines | Where-Object { $_ -match '^#{1,6}\s+\S' } | ForEach-Object { ([regex]::Match($_, '^#+')).Value.Length })
    $measured[$language] = [pscustomobject]@{ Lines = $lines.Count; Bytes = $bytes.Length; Date = $date; Headings = $headings }
}

$findings = @()
foreach ($language in $languages) {
    $m = $measured[$language]
    if ($m.Lines -gt $MaxLines) { $findings += "NEXT-SESSION.$language.md tiene $($m.Lines) lineas y el tope es $MaxLines" }
    if ($m.Bytes -gt $MaxBytes) { $findings += "NEXT-SESSION.$language.md pesa $($m.Bytes) bytes y el tope es $MaxBytes" }
    if (-not $m.Date) { $findings += "NEXT-SESSION.$language.md no lleva la fecha AAAA-MM-DD en la primera linea" }
}
if ($measured.es.Date -and $measured.en.Date -and $measured.es.Date -ne $measured.en.Date) {
    $findings += "los dos idiomas llevan fechas distintas en la primera linea ($($measured.es.Date) y $($measured.en.Date))"
}
if (($measured.es.Headings -join ',') -ne ($measured.en.Headings -join ',')) {
    $findings += "los dos idiomas no tienen la misma estructura de encabezados (es: $($measured.es.Headings -join ','); en: $($measured.en.Headings -join ','))"
}

Write-Output ("RELEVO: es {0} lineas / {1} bytes, en {2} lineas / {3} bytes, fecha {4}" -f `
    $measured.es.Lines, $measured.es.Bytes, $measured.en.Lines, $measured.en.Bytes, $measured.es.Date)
foreach ($f in $findings) { Write-Output "FAIL: $f" }
if ($findings.Count -gt 0) { exit 1 }
exit 0
