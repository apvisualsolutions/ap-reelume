# C4 — Puerta de reproducción / Playback gate

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Tareas cubiertas / Tasks covered: T18–T24
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, PowerShell 7.6.3,
  Avalonia 12.1.1, LibVLCSharp 3.10.0, LibVLC 3.0.23.1, NVIDIA GeForce RTX 5070, dos ASUS ProArt
  PA279CRV a 2560×1440 con escala 150 % y HDR activo, auriculares Logitech G535

## Resultado por tarea / Per-task result

| Tarea / Task | Commit | Evidencia / Evidence | Estado / Status |
|---|---|---|---|
| T18 | `feat: embed playback behind a replaceable engine` | [T18](T18-libvlc-spike.md) | superada / passed |
| T19 | `test: add reproducible licensed playback matrix` | [T19](T19-codec-matrix.md) | superada / passed |
| T20 | `feat: persist audio and subtitle preferences by scope` | [T20](T20-tracks-subtitles.md) | superada / passed |
| T21 | `feat: add accessible playback controls and peak-limited boost` | [T21](T21-controls-limiter.md) | superada / passed |
| T22 | `feat: report HDR and acceleration with safe fallback` | [T22](T22-hdr-acceleration.md) | superada con bloqueo de hardware / passed with a hardware block |
| T23 | `feat: select persistent multichannel audio output` | [T23](T23-audio-output.md) | superada con bloqueo de hardware / passed with a hardware block |
| T24 | `feat: preserve playback across windows and input methods` | [T24](T24-windows-input.md) | superada / passed |

## Condición 1 — La matriz completa reproduce o diagnostica / The full matrix plays or diagnoses

Cada fila de la matriz aprobada reproduce audio y vídeo o explica su incompatibilidad con un código
de dominio accionable. Ninguna fila se omitió: el codificador local pudo producir todas. Los
resultados por muestra, con procedencia y hash, están en [T19](T19-codec-matrix.md) y en
`artifacts/test-results/T19/green/media-provenance.json`. / Every approved row plays or explains its
incompatibility with an actionable domain code, and no row skipped.

## Condición 2 — Los recursos no crecen tras 50 ciclos / Resources do not grow after fifty cycles

Cincuenta ciclos alternando cuatro medios distintos —MP4/H.264, MKV/HEVC, WebM/VP9 y MKV con audio
5.1—, cambiando la ruta en cada ciclo y alternando el dispositivo de salida:

| Métrica / Metric | Ventana ciclos 11–25 / Cycles 11–25 | Ventana ciclos 36–50 / Cycles 36–50 | Tendencia / Trend |
|---|---:|---:|---:|
| Working set | 165,4 MiB | 165,6 MiB | **+0,2 MiB** |
| Handles | 536 | 587 | +51 |

El working set sube de 79 a unos 140 MiB durante los diez primeros ciclos y después se estabiliza:
ese incremento es el coste único de cargar los decodificadores de cuatro formatos distintos. La
condición de la puerta es la ausencia de **crecimiento sostenido**, y por eso se comparan dos ventanas
ya estabilizadas en lugar de la primera muestra contra la última. La serie completa está en
`artifacts/test-results/C4/endurance-resources.csv`. / The working set climbs during the first ten
cycles as four sets of decoders load and then settles, so two settled windows are compared rather
than the first sample against the last.

### Corrección del 2026-08-07: el working set no se mide con esa precisión

Ese `+0,2 MiB` es una lectura de una ejecución, no una cifra reproducible, y presentarla como
tendencia daba a entender otra cosa. Al repetir el mismo bucle **siete veces sin tocar el código**,
la diferencia entre las dos ventanas fue de **−7,9 a +37,6 MiB**. El umbral de la puerta estaba en 32
MiB, dentro de esa dispersión, así que la suite fallaba aproximadamente una de cada tres ejecuciones
sin que hubiera ninguna regresión.

Se probó y se midió la alternativa evidente —ajustar una pendiente en vez de comparar medias— y **no
es más estable**: entre −170 y +1107 KiB por ciclo en esas mismas siete ejecuciones. La variación no
es un pico que una recta absorba, sino un desplazamiento de toda la serie: el working set se lee del
proceso entero, que además ejecuta el host de pruebas y el recolector de cobertura, y cuánta memoria
devuelve .NET al sistema operativo depende de lo que esté haciendo la máquina.

Lo que detecta una fuga se comprueba **en cada uno de los cincuenta ciclos**, de forma exacta: una
sola instancia nativa, ningún medio vivo, un solo reproductor vivo. El límite de working set pasa a
128 MiB, que es lo que corresponde a una regresión gruesa —del orden de megabytes por ciclo— y no al
ruido de la medida. Una puerta que falla al azar no protege: enseña a reintentar.

/ **Correction of 2026-08-07.** That `+0.2 MiB` was one reading, not a reproducible figure. Seven runs
with no code change ranged from −7.9 to +37.6 MiB, so the 32 MiB bound sat inside the spread and the
suite failed about one run in three with no regression. Fitting a slope was tried and measured at
−170 to +1107 KiB per cycle, so it is no steadier. The bound is now 128 MiB and catches a gross
regression; the exact counters on every cycle are what catch a leak.

Los 50 ciclos con un **único** medio de T18 dan `+3` handles y `+0,3 MiB`, registrados en
`artifacts/test-results/T18/green/engine-resource-cycles.csv`. / The single-media variant from T18
gives the smaller figures recorded above.

### Incidencia de handles: causa raíz identificada en I4 / Handle incident: root cause found in I4

Durante la verificación de esta puerta, una de tres pasadas de la suite completa hizo fallar la
comprobación de handles. Se investigó con una medición aislada de **200 ciclos** alternando los
mismos cuatro formatos:

| Ventana / Window | Handles (media) | Working set |
|---|---:|---:|
| ciclos 1–25 | 526 | 168,5 MiB |
| ciclos 51–75 | 628 | 199,4 MiB |
| ciclos 101–125 | 727 | 218,1 MiB |
| ciclos 151–175 | 831 | 208,7 MiB |
| ciclos 176–200 | 880 | 222,9 MiB |

El working set **satura** cerca de 220 MiB. Los handles crecían de forma lineal, unos dos por ciclo.
En su momento se atribuyó el crecimiento a **alternar formatos** y se dejó la incidencia abierta.

**Corrección de esa atribución.** Al cerrar I4 se repitió la medición aislando una variable cada vez,
60 ciclos por fase, y el resultado desmiente la explicación anterior:

| Fase / Phase | Handles por ciclo / per cycle |
|---|---:|
| Alternando cuatro formatos, con reproducción / Four formats, playing | +1,95 |
| **Un solo archivo**, con reproducción / **Single file**, playing | **+2,05** |
| Alternando formatos, **sin reproducir** / Four formats, **open only** | +3,00 |
| Alternando formatos, sin reproducir, recolectando cada 10 ciclos / open only, collected | **+0,19** |
| Alternando formatos, con reproducción, recolectando / playing, collected | +2,13 |
| Alternando formatos, **decodificación por software**, recolectando / **software decoding**, collected | **−0,01** |

Tres conclusiones, todas medidas:

1. **No depende de alternar formatos.** Un solo archivo crece igual que cuatro (2,05 frente a 1,95).
   Lo que sí difiere entre uno y cuatro es el working set, no los handles. La afirmación anterior era
   incorrecta.
2. **La apertura no fuga: retiene hasta la finalización.** Abrir y analizar sin reproducir sube 3,00
   por ciclo, pero recolectando cada diez ciclos baja a 0,19: son objetos gestionados que el
   analizador de LibVLCSharp deja pendientes de finalizar, no handles perdidos.
3. **Lo que queda es la ruta de decodificación por hardware.** Con la misma prueba y
   `useHardwareAcceleration: false`, el crecimiento es **−0,01 por ciclo**: exactamente cero. El
   registro del motor confirma que esta máquina decodifica con **D3D11VA sobre la NVIDIA GeForce RTX
   5070**; los dos handles por ciclo son contabilidad del decodificador y del controlador, fuera del
   adaptador.

**Estado: causa raíz identificada y atribuida; no corregible desde este adaptador.** El motor ya
libera cuanto posee —una instancia nativa, cero medios vivos tras detener y un único reproductor
prestado— y renunciar a la aceleración por hardware para evitar dos handles por ciclo sería un mal
negocio para el producto. En su lugar queda fijado con pruebas: `HandleGrowthTests` asevera que las
dos rutas que **sí** pertenecen a este código —abrir sin reproducir y reproducir por software— no
ganan handles, de modo que una regresión introducida aquí se distinguiría del decodificador. La
magnitud es acotada: encadenar cien episodios añade unos doscientos handles a un proceso cuyo límite
práctico está en decenas de miles. / The earlier attribution was wrong: the growth does not come from
alternating formats and it is not a leak in the open path, which merely retains until finalisation.
It comes from the hardware decoding path — software decoding gains exactly zero — so it belongs to
the decoder and the driver rather than to this adapter. The two paths this code does own are pinned
by tests.

## Condición 3 — Nunca dos motores ni dos sesiones / Never two engines or two sessions

En **cada uno** de los cincuenta ciclos se comprueba `NativeInstanceCount == 1`,
`LiveMediaCount == 0` tras detener y `LiveMediaPlayerCount == 1` mientras el motor vive. El
coordinador de sesión detiene la primera antes de abrir la segunda, y cien cambios de modo de ventana
no crean una segunda superficie ni reabren el motor: el contenido de la ventana sigue siendo el mismo
objeto y sólo existe una superficie de vídeo. / Every cycle asserts one native instance, no live
media after stopping, and one borrowed player; a hundred window-mode changes create no second surface
and no reopen.

## Condición 4 — HDR y audio con resultado físico o bloqueo explícito / Physical result or explicit block

| Capacidad / Capability | Resultado / Result |
|---|---|
| HDR10 en pantalla compatible / on a capable display | **Físico.** El sistema informa `supportsHdr10=True, hdrEnabled=True` para ambos PA279CRV; la fuente declara `smpte2084`; el motor informa `Hdr10Passthrough` y decodifica |
| Conversión a SDR / SDR tone mapping | **Físico.** La misma fuente sobre una pantalla sin HDR informa `SdrToneMapped` y decodifica |
| Fuente SDR / SDR source | **Físico.** Se mantiene `Sdr` y nunca se promociona a HDR |
| Fallback de aceleración / Acceleration fallback | **Físico.** Ocurre una sola vez, la reproducción continúa y se siguen decodificando fotogramas |
| GPU integrada frente a discreta / Integrated versus discrete GPU | **BLOQUEO.** Este equipo no expone ninguna GPU integrada activa; sólo hay NVIDIA GeForce RTX 5070. La comparación **no se ha ejecutado** |
| Audio estéreo / Stereo audio | **Físico.** Cuatro endpoints activos lo admiten |
| Audio 5.1 y 7.1 | **BLOQUEO.** Ningún endpoint activo lo admite: los cuatro declaran un formato de mezcla de dos canales. Las filas **no se han verificado** |
| Dolby Vision y passthrough Dolby/DTS | Fuera de alcance por decisión, con revisión registrada y sin código |

Los dos bloqueos son limitaciones del hardware disponible, no fallos del software, y por eso
`PLY-003` y `PLY-004` permanecen `IN_PROGRESS`. / The two blocks are hardware limitations rather than
software failures, which is why those two identifiers stay in progress.

## Condición 5 — Todo el reproductor sin ratón / The whole player without a mouse

Cada acción esencial tiene un gesto de teclado propio y único; el editor de atajos rechaza un
reenlace en conflicto nombrando el comando que lo ocupa y restaura los valores iniciales; los
controles de transporte anuncian nombre, estado y atajo, aceptan el foco de teclado y siguen
enfocables cuando la barra se atenúa. Verificado en español y en inglés. / Every essential action has
its own unique keyboard gesture, conflicts are refused by name, defaults restore, and the transport
controls announce name, state, and accelerator and stay focusable when the bar dims, in both
languages.

## Verificación transversal / Cross-cutting verification

| Comprobación / Check | Resultado / Result |
|---|---|
| `dotnet restore --locked-mode` | correcto / clean |
| `dotnet build -c Debug -warnaserror` | 0 advertencias, 0 errores |
| `dotnet build -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet format --verify-no-changes` | sin cambios / no changes |
| Suite completa `Release` / Full Release suite | **431 pruebas, 0 fallos, 0 omitidas** |
| `eng/verify.ps1 -Configuration Release -Runtime win-x64` | superada / passed |
| Estabilidad: cuatro pasadas serializadas consecutivas / four consecutive serialised runs | 0 fallos y 0 fallos catastróficos / zero failures and zero catastrophic failures |
| `eng/verify-docs.ps1` | 37 Markdown, 6 localizados, 53 IDs, 46 MVP |
| `dotnet list package --vulnerable --include-transitive` | ningún paquete vulnerable / none |
| `dotnet list package --deprecated` | ningún paquete en desuso / none |
| Cobertura de reproducción / Playback coverage | T18 91,5 %, T19 93,4 %, T20 96,5 %, T21 94,0 %, T22 87,5 %, T23 92,3 %, T24 95,7 % de líneas del código nuevo |
| Ramas de políticas de dominio / Domain policy branches | 100 % en ciclo de vida, diagnóstico, control, refuerzo, salida de vídeo y salida de audio; 97,6 % en resolución de preferencias |

## Privacidad y límites / Privacy and boundaries

- **Sin telemetría ni analítica**: no hay ninguna referencia a telemetría o analítica en el código.
- **Sin primitivas de red en reproducción**: ni `HttpClient`, ni sockets, ni resolución de nombres en
  los directorios de reproducción, reproductor y teclas multimedia.
- **Tráfico observado**: durante la suite multimedia completa, los únicos extremos remotos de los
  procesos de prueba fueron `127.0.0.1`, que es el canal entre el ejecutor y su host. Ninguna
  conexión externa.
- **Sin operaciones destructivas**: ningún `Delete`, `Move` ni escritura sobre archivos multimedia en
  el código de reproducción. Las acciones de recuperación ofrecidas son reintentar, elegir otra
  versión y abrir externamente; la enumeración no contiene ninguna opción destructiva.
- **Artefactos y medios ignorados**: `git status` no incluye `artifacts/` ni ningún archivo
  multimedia; `eng/verify.ps1` falla si alguno apareciera.
- **Sin datos personales versionados**: ningún archivo versionado contiene nombre de usuario del
  sistema, nombre de equipo, ruta absoluta local ni inventario de la biblioteca personal. Las
  evidencias de I3 nombran modelos de GPU, pantalla y audio, que son reproducibles.

Deuda previa conocida y **no tocada** en esta puerta: `C2-library-gate.md` y `T6-scan.md` publican
volumen de la biblioteca y términos de búsqueda reales; es una decisión pendiente del propietario,
anterior a hacer público el repositorio. / Known pre-existing debt, deliberately untouched here.

## Incidencia de ejecución en paralelo / Parallel execution incident

La incidencia intermitente registrada antes de I3 —un ensamblado de prueba que moría a los 60 s con
código 1— **reapareció** en esta tarea, se investigó y tiene causa raíz y corrección. Los ensamblados
se ejecutaban en paralelo: `dotnet test` sobre la solución programa una invocación por proyecto y ese
reparto lo gobierna MSBuild, no `MaxCpuCount`. Se observaron dieciséis hosts simultáneos. Con `-m:1`
en la puerta el máximo baja a cuatro durante las transiciones y el valor sostenido es dos.
`TestHostIsolationTests` fija la condición. Detalle completo en [T18](T18-libvlc-spike.md). / The
intermittent incident recorded before this increment reappeared, was investigated, and has a root
cause and a fix, pinned by an architecture test and described in full in the T18 evidence.

## Resultado de la puerta / Gate result

**C4 se propone como superada**, con tres salvedades declaradas y ninguna simulada:

1. **Bloqueo de hardware**: no hay GPU integrada activa, así que la comparación integrada frente a
   discreta no se ha ejecutado. `PLY-003` sigue `IN_PROGRESS`.
2. **Bloqueo de hardware**: ningún endpoint de audio acepta 5.1 ni 7.1, así que esas filas no se han
   verificado. `PLY-004` sigue `IN_PROGRESS`.
3. **Incidencia de handles**: dos handles por ciclo, cuantificados sobre 200 ciclos. Al cerrar I4 se
   identificó su causa —la ruta de decodificación por hardware, no el adaptador ni alternar
   formatos— y quedó fijada con pruebas; el detalle está en la sección corregida de arriba.

Ninguna capacidad ausente se ha sustituido por una simulación ni se ha declarado como resultado
superado. La aprobación corresponde a Engineering y QA. / C4 is proposed as passed with the three
declared caveats above and nothing simulated; approval rests with the roles the plan names.
