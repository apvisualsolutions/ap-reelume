#Requires -Version 7
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# cierre-acta.ps1 -- el acta del cierre: que cada paso de /cierre se ejecuto de verdad.
#
# POR QUE EXISTE (2026-09-19). El propietario pidio mejorar /cierre con lo que ya resolvio el de la
# sesion de IT, y lo primero que alli se aprendio es que un skill con la instruccion delante se
# ejecuta a medias igual. Un aviso mas en prosa no lo arregla; una comprobacion si. Es el MECANISMO
# de su acta con NUESTROS pasos: una tabla {Fase, Que, Ok}, la evidencia por una orden ejecutada de
# verdad en el transcript de ESTA sesion o por un hecho de git, y salida distinta de cero si falta
# algo. Lo reutilizado sin copiar es su libreria de transcript, que se carga por ruta.
#
# LA RUTA DE ESA LIBRERIA NO SE ESCRIBE AQUI. Este repositorio es publico, y una ruta de la red de
# la casa en el arbol la publicaria. Sale de la variable de entorno de usuario AP_SHARED_TOOLS, que
# no se versiona; sin ella, y sin -Transcript, el acta sale 2.
#
# LO QUE CUENTA COMO EJECUTADO, afinado con la revision de la sesion de IT el mismo dia:
#   - la orden tiene que estar INVOCADA, no citada: el nombre al principio de un segmento del
#     comando, o como -File de pwsh/powershell, o detras de bash; un grep, un Get-Content o un echo
#     que lo nombran no cuentan, y lo que va tras un # tampoco;
#   - su resultado no puede ser un error: se empareja cada tool_use con su tool_result por id y se
#     descarta el que llega con is_error o con «Exit code N» distinto de cero;
#   - `dotnet test` con --filter no cuenta como suite corrida: una prueba sola no es la puerta.
#
# CONTRATO DE SALIDA, el de la casa: 0 todo con evidencia, 1 falta algo (se nombra), 2 NO SE PUDO
# MEDIR -- sin transcript, sin libreria o con git sin contestar --, que nunca es un verde. Git se
# comprueba llamada a llamada: un diff que falla no puede dejar la lista corta y eximir una fila.
#
# SE CORRE ANTES DEL COMMIT DEL RELEVO, porque mira el stage.
[CmdletBinding()]
param(
    # Transcript a medir. Sin el, se busca el de ESTA sesion con la libreria compartida.
    [string]$Transcript,
    # Repositorio cuyo git se consulta. Por defecto, la raiz de este.
    [string]$Repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path,
    # Carpeta de las herramientas compartidas del cierre. Por defecto, la variable de usuario.
    [string]$HerramientasCompartidas = $env:AP_SHARED_TOOLS,
    # Rama de referencia: lo que la tanda cambio es lo que hay entre ella y HEAD, mas el stage.
    [string]$Base = 'main'
)

$ErrorActionPreference = 'Stop'

function Salir-SinMedir([string]$motivo) {
    Write-Output "ACTA: NO SE PUDO MEDIR -- $motivo"
    exit 2
}

# --- El transcript de esta sesion -----------------------------------------------------------
$verificado = 'dado a mano con -Transcript'
if (-not $Transcript) {
    if (-not $HerramientasCompartidas) {
        Salir-SinMedir 'herramientas compartidas no configuradas (variable de usuario AP_SHARED_TOOLS); pasa -Transcript'
    }
    $libreria = Join-Path $HerramientasCompartidas 'cierre-transcript.ps1'
    if (-not (Test-Path -LiteralPath $libreria)) {
        Salir-SinMedir 'no se encuentra la libreria de transcript en las herramientas compartidas (unidad sin montar o carpeta movida)'
    }
    . $libreria
    $encontrado = Get-TranscriptDeEstaSesion
    if ($encontrado.Motivo) { Salir-SinMedir "la libreria no identifica esta sesion: $($encontrado.Motivo)" }
    $Transcript = $encontrado.Ruta
    $verificado = if ($encontrado.Verificado) { 'marca de sesion comprobada dentro del fichero' } else { 'SIN la marca de sesion dentro del fichero' }
}
if (-not (Test-Path -LiteralPath $Transcript)) { Salir-SinMedir "no existe el transcript $Transcript" }

# Cada orden ejecutada (Bash o PowerShell) y su resultado, sacados del JSON y no con un grep crudo:
# el transcript guarda los acentos escapados, y un grep sobre el texto no los ve.
$usos = [ordered]@{}
$fallidos = [System.Collections.Generic.HashSet[string]]::new()
foreach ($linea in [System.IO.File]::ReadLines($Transcript)) {
    if ([string]::IsNullOrWhiteSpace($linea)) { continue }
    try { $entrada = $linea | ConvertFrom-Json -Depth 64 } catch { continue }
    $contenido = $entrada.message.content
    if ($contenido -isnot [System.Array]) { continue }
    foreach ($bloque in $contenido) {
        if ($bloque.type -eq 'tool_use' -and $bloque.input.command) {
            $usos[[string]$bloque.id] = [string]$bloque.input.command
        }
        elseif ($bloque.type -eq 'tool_result') {
            $texto = if ($bloque.content -is [string]) { $bloque.content } else { ($bloque.content | ForEach-Object { $_.text }) -join "`n" }
            if ($bloque.is_error -eq $true -or $texto -match 'Exit code [1-9]') { [void]$fallidos.Add([string]$bloque.tool_use_id) }
        }
    }
}
if ($usos.Count -eq 0) { Salir-SinMedir 'el transcript no tiene ninguna orden ejecutada: o no es el de esta sesion, o no se pudo leer' }
$ordenes = @($usos.GetEnumerator() | Where-Object { -not $fallidos.Contains($_.Key) } | ForEach-Object { $_.Value })

# Los segmentos de una orden donde algo puede estar INVOCADO: se parte por lineas y por ; && || |,
# se quita lo que va tras un # y el & o el . de llamada de PowerShell.
function Segmentos([string]$orden) {
    foreach ($trozo in ($orden -split "`r?`n|;|&&|\|\||\|")) {
        $limpio = ($trozo -replace '#.*$', '').Trim() -replace '^[&.]\s+', ''
        if ($limpio) { $limpio }
    }
}

# Un guion invocado: al principio del segmento, como -File de pwsh/powershell, o detras de bash.
function Invocado([string]$guion) {
    $nombre = [regex]::Escape($guion)
    $patron = "^(?:(?:pwsh|powershell)(?:\.exe)?\b.*-File\s+|bash\s+)?[`"']?[^\s`"']*\b$nombre\b"
    foreach ($orden in $ordenes) { foreach ($s in (Segmentos $orden)) { if ($s -match $patron) { return $true } } }
    $false
}

# Una orden de dotnet invocada: al principio del segmento, con lo que pide el patron.
function Dotnet([string]$patron, [string]$excluir) {
    foreach ($orden in $ordenes) {
        foreach ($s in (Segmentos $orden)) {
            if ($s -match "^dotnet\s+$patron" -and (-not $excluir -or $s -notmatch $excluir)) { return $true }
        }
    }
    $false
}

# --- Lo que la tanda cambio, con git comprobado llamada a llamada ---------------------------
# No se llama «Git»: PowerShell no distingue mayusculas, y una funcion con ese nombre que llama a
# git se llama a si misma hasta desbordar la pila. Paso en la primera version, medido.
function Leer-Git([string[]]$argumentos) {
    $salida = & git -C $Repo @argumentos 2>$null
    if ($LASTEXITCODE -ne 0) { Salir-SinMedir "git $($argumentos -join ' ') salio $LASTEXITCODE en el repositorio" }
    $salida
}
$null = Leer-Git @('rev-parse', '--verify', '--quiet', $Base)
$tocados = @(Leer-Git @('diff', '--name-only', "$Base...HEAD")) + @(Leer-Git @('diff', '--cached', '--name-only')) |
    Where-Object { $_ } | Sort-Object -Unique
$tocoCodigo = [bool]($tocados | Where-Object { $_ -match '^(src|tests)/' })
function Tocado([string]$ruta) { $tocados -contains $ruta }

# --- Los pasos de /cierre, cada uno con su evidencia ----------------------------------------
$filas = @(
    [pscustomobject]@{ Fase = '0'; Que = 'CI vigilado con eng/watch-ci.ps1'; Ok = (Invocado 'watch-ci.ps1') }
    [pscustomobject]@{ Fase = '1'; Que = 'formato verificado (dotnet format --verify-no-changes)'; Ok = (Dotnet 'format\b.*--verify-no-changes') }
    [pscustomobject]@{ Fase = '1'; Que = 'compilacion estricta (dotnet build -warnaserror)'; Ok = (Dotnet 'build\b.*-warnaserror') }
    [pscustomobject]@{ Fase = '1'; Que = 'una suite entera corrida (dotnet test sin --filter)'; Ok = (Dotnet 'test\b' '--filter') }
    [pscustomobject]@{ Fase = '1'; Que = 'documentacion verificada (eng/verify-docs.ps1)'; Ok = (Invocado 'verify-docs.ps1') }
    [pscustomobject]@{ Fase = '1'; Que = 'suelos previstos (eng/preview-coverage-floors.ps1), si la tanda toco codigo'; Ok = ((-not $tocoCodigo) -or (Invocado 'preview-coverage-floors.ps1')) }
    [pscustomobject]@{ Fase = '8'; Que = 'peticiones sin rastro buscadas (cierre-contexto.ps1, compartida)'; Ok = (Invocado 'cierre-contexto.ps1') }
    [pscustomobject]@{ Fase = '8'; Que = 'nombres del sector revisados (cierre-lenguaje.ps1, compartida)'; Ok = (Invocado 'cierre-lenguaje.ps1') }
    [pscustomobject]@{ Fase = '8'; Que = 'memoria revisada (cierre-memorias.ps1, compartida)'; Ok = (Invocado 'cierre-memorias.ps1') }
    [pscustomobject]@{ Fase = '10'; Que = 'relevo escrito en los dos idiomas (docs/NEXT-SESSION.{es,en}.md)'; Ok = ((Tocado 'docs/NEXT-SESSION.es.md') -and (Tocado 'docs/NEXT-SESSION.en.md')) }
    [pscustomobject]@{ Fase = '10'; Que = 'pendientes en su registro (eng/list-pending.ps1)'; Ok = (Invocado 'list-pending.ps1') }
)

Write-Output ("ACTA de /cierre -- transcript: {0} ({1} ordenes, {2} fallidas descartadas; {3})   <-- control positivo" -f (Split-Path $Transcript -Leaf), $usos.Count, $fallidos.Count, $verificado)
foreach ($fila in $filas) {
    $marca = if ($fila.Ok) { 'ok' } else { '!!' }
    Write-Output ("  {0,-3} paso {1,-3} {2}" -f $marca, $fila.Fase, $fila.Que)
}
$faltan = @($filas | Where-Object { -not $_.Ok })
if ($faltan.Count -gt 0) {
    Write-Output "ACTA: FALTAN $($faltan.Count) paso(s) -- no se hace el commit del relevo hasta completarlos."
    exit 1
}
Write-Output 'ACTA: completa.'
exit 0
