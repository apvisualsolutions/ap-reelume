# Las dos salidas de una sesión que no abre / The two ways out of a session that will not open

Entregar el archivo al reproductor del sistema y reintentar, pulsados con el ratón — la **novena
salida** de la regla de aislamiento, el apagado con una sesión viva, y el defecto que deja los tres
botones del archivo suelto fuera de la pantalla. / Handing the file to the system's player and
retrying, pressed with the mouse — the isolation rule's ninth exit, shutdown with a live session, and
the defect that keeps the loose-file banner off the screen entirely.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 120 | **122** |
| Pendientes / Pending | 8 | **6** |

```
The walk: 129 declared command controls in 128 identities; 122 pressed, 6 pending.
```

## La novena salida: «Abrir con una aplicación externa» / The ninth exit

`ShellExternalPlaybackLauncher` arranca un **proceso real** —`UseShellExecute` entrega la ruta a lo
que Windows tenga registrado—, así que pulsar ese botón en la máquina que mide abriría el reproductor
del sistema de quien corre la suite. Se resuelve como las ocho anteriores: la composición elige **una
vez, por la raíz de datos**, entre el lanzador real y `RecordingExternalPlaybackLauncher`, que anota
la línea con el verbo delante. / The real launcher starts a process, so the composition chooses once,
by the data root, between it and a recorder that writes the line down with its verb first.

```
play-externally <ruta>
```

**Las dos negativas se repiten en el anotador a propósito**, y ésa es la razón de que pueda sustituir
al otro: una sonda que lee una entrega anotada sólo dice algo del lanzador real si los dos coinciden
en **lo que nunca se entrega**. Una extensión fuera de la lista aprobada y un archivo que no está son
negativas del real, así que son negativas aquí — afirmadas en `IsolatedRunTests`, en las dos mitades
de la elección. / The two refusals are repeated on purpose: a probe reading a written-down handover
only says something about the real launcher if both agree on what never gets handed over.

El contrato entero se afirma **junto al del lanzador real**, en `ExternalPlaybackLauncherTests`, y en
una sola prueba: los informes Cobertura fusionados conservan la mejor de las dos lecturas por línea en
vez de su unión, así que una rama repartida entre suites se lee medio cubierta para siempre. Y ahí el
anotador paga una deuda vieja: prueba **la mitad que el lanzador real no puede probar**, porque un
archivo aprobado que sí está llega a la entrega sin abrir un reproductor en la máquina que mide. / The
whole contract is asserted beside the real launcher's, in one test, and the recorder drives the half
the real launcher cannot be asked for without opening a player on whoever runs it.

**La puerta de cobertura mordió, y lo que le faltaba no era lo que parecía.** Dio **83,33 % de ramas**
—5 de 6— y la suposición razonable eran las guardas de entrada; añadirlas no movió el número. Leído el
informe línea a línea, la que estaba a la mitad era **el `?? throw` del constructor**: un anotador sin
sitio donde escribir no es un anotador más callado, es un lanzador que no entrega nada mientras una
sonda lee un registro vacío y lo llama negativa. / The gate said 83.33% of branches, and the guard
clauses were the reasonable guess; adding them moved nothing. Read line by line, the half-covered one
was the constructor's `?? throw`.

```
line=36 hits=12 cov=50% (1/2)     antes / before
line=36 hits=2  cov=100% (2/2)    después / after — branch-rate=1
```

## El botón de reintentar no comparte fallo con el de entregar / They are offered for different failures

Estaba decidido que las dos pulsaciones compartieran superficie: abrir dos bytes con extensión
aprobada, entregar, sustituir el archivo por una muestra buena y reintentar. **La medición lo negó:**
/ It was decided that both presses would share one failure. The measurement refused:

```
corrupted=True canRetry=False canOpenExternally=True canChooseAnother=True
```

`PlaybackDiagnosticsPolicy.RecoveryActionsFor` da a un medio corrupto **elegir otra versión y abrir
fuera**, sin reintentar — y tiene razón: reabrir unos bytes que siguen siendo los mismos bytes falla
igual. El reintento se ofrece cuando **falta el archivo**, que es el disco que alguien vuelve a
conectar. Así que la escena abre dos veces y cada pulsación encuentra el fallo que la ofrece. / The
policy gives corrupted media no retry, and it is right: reopening the same bytes fails the same way.
Retry is offered for a missing file, which is the disk somebody plugs back in.

| Control | Efecto afirmado / Asserted effect |
|---|---|
| Abrir con una aplicación externa / Open externally | la línea `play-externally <ruta>` aparece en el registro de entregas / the handover line appears |
| Reintentar / Retry | el archivo vuelve entre las dos pulsaciones y la sesión **pasa a reproducir** / the file comes back and the session plays |

## El apagado con una sesión viva / Shutting down while something is playing

Medido dos veces el 2026-08-17, la primera por accidente: en cuanto una escena termina **sin cerrar el
reproductor**, el desmontaje revienta. / Measured twice, the first time by accident: the moment a
scene ends without closing the player, the teardown throws.

```
ObjectDisposedException: LibVlcMediaPlayerEngine
  at PlaybackSessionCoordinator.StopActiveSessionAsync
  at PlaybackSessionCoordinator.DisposeAsync
  at ApplicationHost.DisposeAsync
```

**Terminar los enganches de una sesión no es pararla.** `EndPlaybackSession` desengancha los
manejadores y detiene el bucle de guardado; el medio sigue abierto, así que el desechado del
contenedor llega al coordinador, que intenta parar un reproductor que el desechado del motor ya se
llevó — el motor está registrado **tres veces**, y la última se resuelve cuando un vídeo empieza a
dibujarse, así que entra en la lista después del coordinador y sale antes. La corrección va donde ya
estaba su razón: `ApplicationHost.DisposeAsync` **para la sesión** antes de desechar los servicios. /
Ending a session's hooks is not stopping it. The fix goes where its reason already was.

**Quien cierra la ventana a media película toma ese camino**, así que la escena termina con el vídeo
sonando, a propósito: cerrar el reproductor primero es lo que hacen todas las demás escenas y es lo
que tapaba esto. / Somebody who closes the window mid-film takes that path, so the scene ends with a
video still playing.

## El hallazgo que bloquea los tres del archivo suelto / What blocks the loose file's three

Medido, y no corregido aquí porque es un rediseño con su propia medición: **un archivo activado desde
el Explorador se reproduce y no se ve**. / Measured, and not corrected here because it is a redesign
of its own: a file activated from Explorer plays and cannot be seen.

```
singleton.IsLooseSession=True  name='Arrival.2016.mp4'  engine=Playing  pos=00:00:00.15
player=False  playerVisible=False  stages=0  surfaces=0
```

La activación hace su parte entera: `OpenLooseFile` arranca el motor y el banner recibe su sesión. Lo
que no ocurre es que se construyan las **superficies del reproductor**, y `HasLooseFile` es
`Player?.LooseFile is not null` — así que la pantalla no se monta, el vídeo suena sin imagen ni
transporte, y el aviso de «esto no está en tu biblioteca» **no llega nunca**, con sus tres botones
dentro. `OpenPlayerAsync` no sirve tal cual: empieza por `FindByIdAsync` y un archivo suelto no está
en el catálogo — y su camino arranca el seguimiento de progreso, que es exactamente lo que
`OpenLooseFile` promete no tocar. / The activation does its whole part; what never happens is the
player surfaces being built.

**Afecta también al tráiler local** (`onPlayTrailer` abre por la misma vía), que es uno de los tres
pendientes de la tanda 1: corregirlo desbloquea **cuatro** controles, no tres. / It blocks the local
trailer too, so correcting it unblocks four controls rather than three.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn                      # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
dotnet test …UiTests                                                   # 448 / 448
dotnet test …ArchitectureTests                                         # 26 / 26
dotnet test …IntegrationTests                                          # 451 / 452, 1 omitida / skipped
dotnet test …AccessibilityTests                                        # 114 / 114
eng/run-accessibility.ps1 -Mode Verify -Passes 2                       # 0 críticos / 0 critical
eng/check-walk-coverage.ps1                                            # 122 pulsados, 6 pendientes / pressed, pending
eng/check-coverage.ps1                                                 # el anotador nuevo, vigilado a 100/100
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```
