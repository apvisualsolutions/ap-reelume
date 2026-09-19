#Requires -Version 7
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# gate-probe.ps1 -- provoca una puerta del cierre contra un arbol de mentira y traduce lo que hizo.
#
# POR QUE EXISTE (2026-09-19, adopcion del sistema comun). La fase `prove` del comun exige a cada
# puerta sus tres resultados: que SUENE cuando debe, que CALLE cuando debe y que diga NO SE PUDO
# MEDIR cuando no puede. Cada puerta se declara en el manifiesto (x-gates) con dos casos, y este
# guion los monta. Nunca contra este repositorio: siempre bajo el temporal, y se retira al acabar.
#
# EL CONTRATO DE SALIDA ES EL DE `prove`, no el de cada instrumento: 1 sono, 0 callo, 2 no se pudo
# medir. Cada puerta traduce el suyo --la de commits del plugin bloquea saliendo 2, el acta falta
# saliendo 1--, y cualquier cosa que no sea ninguno de los dos desenlaces esperados es 2.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('push-cierre', 'fin-de-turno', 'acta', 'relevo', 'privacidad', 'ignorado')]
    [string]$Gate,
    [Parameter(Mandatory = $true)][ValidateSet('sound', 'silent')][string]$Case,
    [string]$Herramientas = $env:AP_SHARED_TOOLS,
    [string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
)

$ErrorActionPreference = 'Stop'
function Sin-Medir([string]$motivo) { Write-Output "UNMEASURED: $motivo"; exit 2 }
if ([string]::IsNullOrWhiteSpace($Herramientas) -or -not (Test-Path -LiteralPath $Herramientas)) {
    Sin-Medir 'la variable AP_SHARED_TOOLS no apunta a las herramientas compartidas'
}
$ganchos = Join-Path (Split-Path -Parent $Herramientas) 'hooks'
$scripts = $PSScriptRoot

$raiz = Join-Path ([IO.Path]::GetTempPath()) ("gate-probe-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $raiz | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)

function Arbol([hashtable]$ficheros = @{}) {
    $dir = Join-Path $raiz ('r-' + [guid]::NewGuid().ToString('N'))
    & git init -q -b main $dir
    & git -C $dir config user.email t@t; & git -C $dir config user.name t
    [IO.File]::WriteAllText((Join-Path $dir 'README.md'), "x`n", $utf8)
    foreach ($f in $ficheros.Keys) {
        $p = Join-Path $dir $f; New-Item -ItemType Directory -Force (Split-Path $p) | Out-Null
        [IO.File]::WriteAllText($p, $ficheros[$f], $utf8)
    }
    & git -C $dir add -A; & git -C $dir commit -qm base
    $dir
}
function Comun([string]$dir) { (& git -C $dir rev-parse --path-format=absolute --git-common-dir).Trim() }
function Relevo([int]$lineas) {
    $cuerpo = @('# Relevo 2026-09-19') + @(1..$lineas | ForEach-Object { "· linea $_" })
    ($cuerpo -join "`n") + "`n"
}
# Ejecuta un proceso con la entrada que se le de y devuelve su codigo.
function Correr([string[]]$argumentos, [string]$entrada = '') {
    $in = Join-Path $raiz ('in-' + [guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllText($in, $entrada, $utf8)
    $p = Start-Process -FilePath 'pwsh' -ArgumentList $argumentos -NoNewWindow -Wait -PassThru `
        -RedirectStandardInput $in -RedirectStandardOutput "$in.out" -RedirectStandardError "$in.err"
    $p.ExitCode
}
function Traducir([int]$codigo, [int]$suena, [int]$calla) {
    if ($codigo -eq $suena) { return 1 }
    if ($codigo -eq $calla) { return 0 }
    2
}

try {
    switch ($Gate) {
        'push-cierre' {
            # La puerta de commits y pushes del plugin: con marca y sin acta en verde, bloquea (2).
            $dir = Arbol
            if ($Case -eq 'sound') { [IO.File]::WriteAllText((Join-Path (Comun $dir) 'closing-marker.json'), '{}', $utf8) }
            $payload = @{ cwd = $dir; tool_input = @{ command = 'git push origin main' } } | ConvertTo-Json -Compress
            $codigo = Correr @('-NoProfile', '-File', ('"' + (Join-Path $ganchos 'commit-gate.ps1') + '"'), '-ProjectDir', ('"' + $dir + '"')) $payload
            exit (Traducir $codigo 2 0)
        }
        'fin-de-turno' {
            # La puerta del final de turno del plugin: con marca y acta en 1, no deja terminar (2).
            $dir = Arbol
            $comun = Comun $dir
            [IO.File]::WriteAllText((Join-Path $comun 'closing-marker.json'), '{}', $utf8)
            $recibo = @{ exitCode = $(if ($Case -eq 'sound') { 1 } else { 0 }); phase = 'prueba'; blocks = 0 } | ConvertTo-Json
            [IO.File]::WriteAllText((Join-Path $comun 'closing-acta-receipt.json'), $recibo, $utf8)
            $payload = @{ cwd = $dir; stop_hook_active = $false } | ConvertTo-Json -Compress
            $codigo = Correr @('-NoProfile', '-File', ('"' + (Join-Path $ganchos 'stop-gate.ps1') + '"'), '-ProjectDir', ('"' + $dir + '"')) $payload
            exit (Traducir $codigo 2 0)
        }
        'acta' {
            # El acta de /cierre: sin el vigia de CI en el transcript falta un paso (1).
            $dir = Arbol
            & git -C $dir checkout -q -b tanda
            New-Item -ItemType Directory -Force (Join-Path $dir 'docs') | Out-Null
            foreach ($idioma in 'es', 'en') { [IO.File]::WriteAllText((Join-Path $dir "docs/NEXT-SESSION.$idioma.md"), "x`n", $utf8) }
            & git -C $dir add -A; & git -C $dir commit -qm relevo
            $salidas = Join-Path $raiz 'salidas'; New-Item -ItemType Directory $salidas | Out-Null
            foreach ($n in 'contexto', 'lenguaje', 'memorias') {
                [IO.File]::WriteAllLines((Join-Path $salidas "closing-$n.txt"), [string[]]@('EXIT=0', 'AT=2026-09-19T11:00:00Z'), $utf8)
            }
            $ordenes = @('dotnet format --verify-no-changes', 'dotnet build x -warnaserror', 'dotnet test tests/X',
                'pwsh -NoProfile -File eng/verify-docs.ps1', 'pwsh -NoProfile -File eng/list-pending.ps1',
                'pwsh -NoProfile -File eng/check-handoff.ps1')
            if ($Case -eq 'silent') { $ordenes += 'pwsh -NoProfile -File eng/watch-ci.ps1 -Sha abc' }
            $n = 0
            $transcript = Join-Path $raiz 't.jsonl'
            $lineas = foreach ($o in $ordenes) {
                $n++
                @{ type = 'assistant'; message = @{ content = @(@{ type = 'tool_use'; id = "t$n"; name = 'Bash'; input = @{ command = $o } }) } } | ConvertTo-Json -Depth 10 -Compress
                @{ type = 'user'; message = @{ content = @(@{ type = 'tool_result'; tool_use_id = "t$n"; is_error = $false; content = 'ok' }) } } | ConvertTo-Json -Depth 10 -Compress
            }
            [IO.File]::WriteAllLines($transcript, [string[]]$lineas, $utf8)
            $codigo = Correr @('-NoProfile', '-File', ('"' + (Join-Path $scripts 'cierre-acta.ps1') + '"'), '-Repo', ('"' + $dir + '"'),
                '-Transcript', ('"' + $transcript + '"'), '-Subido', 'tanda', '-Salidas', ('"' + $salidas + '"'),
                '-Desde', '2026-09-19T10:00:00Z', '-Recibo', ('"' + (Join-Path $raiz 'recibo.json') + '"'))
            exit (Traducir $codigo 1 0)
        }
        'relevo' {
            # El tope del relevo: 81 lineas suenan (1).
            $lineas = if ($Case -eq 'sound') { 80 } else { 10 }
            $dir = Arbol @{ 'docs/NEXT-SESSION.es.md' = (Relevo $lineas); 'docs/NEXT-SESSION.en.md' = (Relevo 10) }
            $codigo = Correr @('-NoProfile', '-File', ('"' + (Join-Path $Repo 'eng/check-handoff.ps1') + '"'), '-Root', ('"' + $dir + '"'))
            exit (Traducir $codigo 1 0)
        }
        'privacidad' {
            # El filtro comun en modo publico: una ruta de una maquina en el relevo suena (1).
            $puntos = @{}
            foreach ($p in 'CommitMessage', 'AddedDiff', 'TouchedTree', 'Handoff', 'Prompt', 'Note') {
                $ruta = Join-Path $raiz "$p.txt"
                $texto = if ($p -eq 'Handoff' -and $Case -eq 'sound') { "ver " + 'C' + ':\Usuarios\alguien\notas.md' } else { 'texto limpio' }
                [IO.File]::WriteAllText($ruta, "$texto`n", $utf8)
                $puntos[$p] = $ruta
            }
            $argumentos = @('-NoProfile', '-File', ('"' + (Join-Path $Herramientas 'privacy-gate.ps1') + '"'), '-Public')
            foreach ($p in $puntos.Keys) { $argumentos += @("-$p", ('"' + $puntos[$p] + '"')) }
            $codigo = Correr $argumentos
            exit (Traducir $codigo 1 0)
        }
        'ignorado' {
            # ENG-034: el ajuste local lo ignora el .gitignore DEL REPOSITORIO, sin contar el global.
            # El caso que calla usa el .gitignore real de este repositorio: es lo que se mide.
            $ignorar = if ($Case -eq 'silent') { [IO.File]::ReadAllText((Join-Path $Repo '.gitignore')) } else { "bin/`n" }
            $dir = Arbol @{ '.gitignore' = $ignorar }
            & git -C $dir -c core.excludesFile=/dev/null check-ignore -q .claude/settings.local.json
            $codigo = $LASTEXITCODE
            # check-ignore: 0 ignorado (calla), 1 no ignorado (suena), otro no se pudo medir.
            exit (Traducir $codigo 1 0)
        }
    }
}
catch { Sin-Medir $_.Exception.Message }
finally { Remove-Item -Recurse -Force -LiteralPath $raiz -ErrorAction SilentlyContinue }
