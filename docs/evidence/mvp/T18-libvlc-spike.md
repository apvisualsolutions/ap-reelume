# T18 — Contrato de motor y riesgo Avalonia/LibVLC / Engine contract and Avalonia/LibVLC risk

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `2cab708`
- Commit de tarea / Task commit: `feat: embed playback behind a replaceable engine`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, PowerShell 7.6.3, Avalonia 12.1.1,
  LibVLCSharp 3.10.0, LibVLC 3.0.23.1, GPU NVIDIA GeForce RTX 5070, dos pantallas ASUS ProArt
  PA279CRV configuradas a 2560×1440 con escala de Windows al 150 % (144 ppp)
- IDs: `PLY-001=IN_PROGRESS`, `PLY-007=IN_PROGRESS`, `PRD-004=VERIFIED`

## RED y GREEN / RED and GREEN

`PlaybackContractTests` y `PlaybackSessionCoordinatorTests` se escribieron antes que el
contrato, el coordinador y el adaptador. RED falló porque esos tipos no existían y se
conserva en `artifacts/test-results/T18/red/T18-red-domain.log` y
`T18-red-application.log`; `T18-red-media.log` conserva el RED del smoke con LibVLC real. /
Both plan-named test files were written before the contract, coordinator, and adapter existed.
RED failed because those types were missing and is retained at the paths above; the third log
retains the RED of the real-LibVLC smoke suite.

GREEN ejecuta 241 pruebas con 0 fallos y 0 omitidas en `Release`, con TRX, registros y
Cobertura bajo `artifacts/test-results/T18/green/`. La cobertura combinada de líneas del
código nuevo de T18 es 91,51 % (614/671) y `PlaybackStatePolicy` alcanza 100 % de ramas
(29/29). Por archivo: contratos de dominio 96,8 %, coordinador 94,7 %, `PlayerViewModel`
97,2 %, `LibVlcFactory` 92,5 %, `LibVlcMediaPlayerEngine` 86,7 %, `VideoFrameView` 76,7 %. /
GREEN runs 241 tests with zero failures and zero skips in Release, with TRX, logs, and
Cobertura under the path above. Combined new-code line coverage is 91.51% and the domain
lifecycle policy reaches 100% branch coverage; per-file numbers follow in the same order.

`dotnet format --verify-no-changes`, `dotnet build -c Debug -warnaserror` y
`dotnet build -c Release -warnaserror` terminan con 0 advertencias y 0 errores. / Formatting
verification and both configurations build with zero warnings and zero errors.

## Superposición de controles / Control overlay

`PlayerOverlayTests` compone `PlayerView` sobre una ventana de 1280×720 unidades lógicas,
publica un fotograma BGRA de color plano y repite la comprobación a escalas 100 %, 150 % y
200 %. En las tres escalas la barra de transporte tiene tamaño no nulo, su borde inferior y
su borde derecho quedan dentro del contenido, se compone después de la superficie de vídeo
en el mismo `Panel`, y el píxel central de la barra ya no es el color del fotograma mientras
el píxel superior sí lo es. Las capturas resultantes son
`artifacts/ui-captures/T18/player-overlay-scale-100.png`, `-150.png` y `-200.png`. / The
overlay test composes the player over a 1280×720 logical window, publishes one flat-coloured
BGRA frame, and repeats at 100%, 150%, and 200%. At every scaling the transport bar has a
non-empty size, its bottom and right edges stay inside the content, it is composed after the
video surface in the same panel, and its centre pixel is no longer the frame colour while a
pixel above it still is. The captures are listed above.

Esta prueba es una **regresión**, no un RED de comportamiento ausente: la barra ya se componía
correctamente en ventana normal y la prueba pasó en su primera ejecución completa. Se conserva
porque fija el criterio de aceptación de superposición de T18 y protege el trabajo posterior de
T21 y T24. / This is a regression test, not a RED for missing behaviour: the bar already
composed correctly in a normal window and the test passed on its first complete run. It is kept
because it pins the T18 overlay criterion and protects later work.

## Superficie de vídeo / Video surface

El motor decodifica a memoria del proceso con `SetVideoFormatCallbacks` y `SetVideoCallbacks`,
formato `RV32` limitado a 3840×2160, y publica cada fotograma por `IVideoFrameSource`.
`VideoFrameView` copia los píxeles a un `WriteableBitmap` y los dibuja como un visual
ordinario, de modo que los controles accesibles se componen encima sin airspace. El
`ViewModel` nunca recibe un `MediaPlayer`: sólo estado, comandos y la fuente de fotogramas. /
The engine decodes into process memory through the format and video callbacks, publishes each
frame through the domain frame source, and the view copies pixels into a bitmap drawn as an
ordinary visual, so accessible controls compose above it without airspace. The view model never
receives an engine object.

## Consumo y ciclo de vida / Resources and lifecycle

Cincuenta ciclos abrir/reproducir/detener con una muestra H.264 generada localmente, medidos
por ciclo en `artifacts/test-results/T18/green/engine-resource-cycles.csv`:

| Métrica / Metric | Inicio / Baseline | Ciclo 50 / Cycle 50 | Final tras quiescencia / After quiescence |
|---|---:|---:|---:|
| Handles del proceso / Process handles | 487 | 487 | 490 |
| Working set (MiB) | 88,0 | 88,0 | 88,3 |
| `LiveMediaCount` | 0 | 0 | 0 |
| `NativeInstanceCount` | 1 | 1 | 1 |

Los handles observados durante los 50 ciclos oscilan entre 480 y 490 y el working set entre
85,1 y 91,0 MiB, sin tendencia creciente. El recuento de medios nativos es 0 tras cada ciclo y
la instancia LibVLC es exactamente una durante todo el proceso. / Handles range from 480 to 490
and the working set from 85.1 to 91.0 MiB across the fifty cycles with no upward trend; the
native media count is zero after each cycle and exactly one LibVLC instance exists for the
whole process.

Las demás pruebas del smoke cubren cancelación durante `Opening`, archivo ausente con código
de dominio accionable y motor reutilizable, y liberación forzada durante la reproducción. En
todos los casos `LiveMediaCount` y `LiveMediaPlayerCount` terminan en 0. / The remaining smoke
tests cover cancellation during opening, a missing file with an actionable domain code and a
reusable engine, and forced disposal during playback; every case ends with zero live media and
zero live players.

## Decisión de T18.5: sustituir detrás del mismo contrato / T18.5 decision: replace behind the same contract

La decisión es **sustituir**, no continuar con la integración nativa, y el contrato
`IMediaPlayerEngine` no cambia. / The decision is to replace, not to continue with the native
integration, and the engine contract is unchanged.

1. `NativeControlHost` no permite la superposición requerida. La ventana hija nativa cubre la
   composición de Avalonia y además exige manifiesto de aplicación: el arranque terminó con
   `InvalidOperationException: Unable to create child window for native control host.
   Application manifest with supported OS list might be required.`, conservado en
   `artifacts/ui-captures/T18/spike-stderr.log`. / The native control host cannot deliver the
   overlay: its child window covers the composition and it also requires an application
   manifest, as the retained crash log shows.
2. El módulo de salida de vídeo `dummy` de LibVLC es la causa raíz de las caídas observadas al
   abrir y liberar medios en secuencia. Aislado con una matriz de configuraciones: `--vout=dummy`
   cae; `--aout=dummy`, `--no-video` y la salida predeterminada pasan 8/8. Las ejecuciones sin
   ventana usan por tanto un decodificador real y descartan los fotogramas por callbacks. / The
   dummy video output is the root cause of the observed faults and was isolated with a
   configuration matrix; headless runs therefore keep a real decoder and discard frames through
   callbacks.
3. `libvlc_media_player_stop()` sobre un reproductor en estado *Playing* provoca `0xC0000005`.
   El adaptador pausa antes de detener. / Stopping a playing player faults, so the adapter
   pauses first.
4. Liberar el `MediaPlayer` antes que su `Media` también aborta el proceso. La cola de medios se
   drena con una ventana de quiescencia de 1 s y el reproductor se libera después. / Releasing
   the player before its media also aborts, so media drain through a one-second quiescence
   window before the player is released.
5. La instancia LibVLC es única por proceso y por conjunto de opciones y no se destruye; sólo se
   liberan reproductores y medios. / The LibVLC instance is one per process and option set and is
   never destroyed; only players and media are released.
6. `LibVLCSharp.Avalonia` no se adopta: apunta a Avalonia 11.x y filtraría `MediaPlayer` a la
   vista. Queda declarado en `Directory.Packages.props` y sin usar. / The Avalonia binding is not
   adopted because it targets Avalonia 11.x and would leak the engine object into the view; it
   stays declared and unused.

## Defecto de pantalla completa trasladado a T24 / Fullscreen defect handed to T24

Con la ventana forzada a `WindowState.FullScreen` sobre una pantalla al 150 %, la barra de
transporte no aparece. La captura `artifacts/ui-captures/T18/player-overlay-no-video.png` mide
2560×1440 y el aviso de fallo, centrado por XAML, ocupa `x=[1524,2316]`, `y=[1020,1138]`, es
decir centro observado `(1920, 1079)`. Con un `ClientSize` lógico correcto de 1706,67×960 el
centro debería dibujarse en el píxel físico `(1280, 720)`. La posición observada es exactamente
1,5× la esperada: en pantalla completa el tamaño de cliente llega en píxeles físicos y el render
aplica además el factor de escala. Por el mismo factor, la barra anclada abajo cae cerca de
`y≈2070` físicos, fuera de una pantalla de 1440. / With the window forced to fullscreen on a
150% display the transport bar does not appear. The retained capture is 2560×1440 and the
centred failure notice sits at the measured box above, giving an observed centre of (1920, 1079)
where a correct 1706.67×960 logical client size would place it at (1280, 720). The observed
position is exactly 1.5× the expected one: in fullscreen the client size arrives in physical
pixels while rendering still applies the scale factor, and the bottom-anchored bar therefore
lands near y≈2070, outside a 1440-pixel display.

`PlayerView.axaml` y `VideoFrameView.cs` no están implicados: la misma composición es correcta
en ventana normal a 100 %, 150 % y 200 %, como demuestra la sección de superposición. El modo
pantalla completa pertenece a T24, que implementará `PlayerWindowCoordinator` sin depender de
`WindowState.FullScreen` y añadirá la regresión correspondiente a 150 %. `PLY-007` permanece
`IN_PROGRESS` por este motivo. / The view and the frame surface are not implicated: the same
composition is correct in a normal window at all three scalings. Fullscreen belongs to T24,
which will implement the window coordinator without relying on that window state and add the
matching 150% regression, so the fullscreen identifier stays in progress.

## Incidencia de ejecución en paralelo: causa raíz y corrección / Parallel execution incident: root cause and fix

Se había registrado un fallo intermitente en el que un ensamblado de prueba moría a los 60 s
con código 1. Reapareció durante esta tarea en `DocumentationTests`, que reportó 2 de 5 pruebas
tras `[FATAL ERROR] Xunit.Sdk.TestPipelineException / Catastrophic failure: Test process crashed
with exit code 1` a `00:01:00.65`. / An intermittent failure in which one test assembly died
after sixty seconds with exit code 1 had been recorded. It reappeared during this task in the
documentation suite, which reported two of five tests after the fatal pipeline error above.

Causa raíz: los ensamblados se ejecutaban en paralelo. Al muestrear los procesos de prueba
durante `dotnet test` sobre la solución se observaron **16 hosts simultáneos**, con y sin
`--settings eng/test.runsettings`. `MaxCpuCount` sólo acota una invocación de VSTest que recibe
varios ensamblados; cuando `dotnet test` apunta a la solución, MSBuild programa una invocación
por proyecto y ese reparto lo gobierna el número de nodos de MSBuild. / Root cause: the
assemblies ran in parallel. Sampling the test processes during a solution-wide run showed
sixteen simultaneous hosts with and without the run-settings file, because that setting only
bounds a single VSTest invocation while MSBuild schedules one invocation per project.

Corrección: la puerta `eng/verify.ps1` pasa ahora `-m:1` junto al archivo de configuración. Con
esa opción el máximo observado baja a 4 procesos —un host y su recolector durante la transición
entre ensamblados— y el valor sostenido es 2. `TestHostIsolationTests` fija ambas condiciones.
Cinco pasadas serializadas consecutivas de la solución completa terminan sin fallo catastrófico;
el registro está en `artifacts/test-results/T18/green/T18-serialised-stability.log`. / Fix: the
verification gate now passes `-m:1` alongside the settings file. The sampled maximum drops to
four processes during assembly transitions and rests at two. An architecture test pins both
conditions, and five consecutive serialised runs of the whole solution finish without a
catastrophic failure, as the retained log shows.

## Privacidad y límites / Privacy and boundaries

T18 no contiene cliente de red ni telemetría. La única muestra multimedia es un `testsrc2` más
una onda sinusoidal generados en el momento por el codificador local bajo `artifacts/test-media`,
que está ignorado por `.gitignore`; no se lee, copia ni modifica ningún archivo de la biblioteca
personal. El motor abre los archivos en modo de sólo lectura y no ejecuta ninguna operación
destructiva. Ninguna ruta absoluta local, nombre de usuario ni nombre de equipo aparece en el
código, la documentación o este informe. / T18 has no network client or telemetry. The only
media sample is a synthetic pattern plus a sine tone generated on demand by the local encoder
under an ignored artifacts directory; no personal library file is read, copied, or modified. The
engine opens files read-only and performs no destructive operation, and no local absolute path,
user name, or machine name appears in code, documentation, or this report.

`PRD-004` se mantiene `VERIFIED` y suma esta evidencia: el dominio y los casos de uso de
reproducción no referencian LibVLC, Avalonia ni Windows, y `PlaybackContractTests` lo comprueba
sobre los ensamblados referenciados. `PLY-001` y `PLY-007` continúan `IN_PROGRESS`: la matriz
legal de contenedores y códecs cierra en T19 y los modos de ventana en T24. / The decoupled-core
identifier stays verified and gains this evidence, because the domain and playback use cases
reference none of the frameworks and the contract test asserts it over referenced assemblies.
The two playback identifiers remain in progress: the codec matrix closes in T19 and the window
modes in T24.
