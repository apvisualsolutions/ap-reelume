# T23 — Dispositivos y canales de audio / Audio devices and channels

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `2833200`
- Commit de tarea / Task commit: `feat: select persistent multichannel audio output`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, LibVLC 3.0.23.1,
  auriculares Logitech G535, salida digital Realtek y audio DisplayPort de dos ASUS ProArt PA279CRV
- IDs: `PLY-004=IN_PROGRESS`, `PLY-015=OUT_OF_SCOPE`

## RED y GREEN / RED and GREEN

`AudioDeviceLifecycleTests` y `AudioChannelTests` se escribieron antes que el adaptador y el
catálogo. RED falló porque `IAudioOutputTarget` y `LibVlcAudioOutputAdapter` no existían; la salida
se conserva en `artifacts/test-results/T23/red/`. `AudioOutputPolicyTests` se escribió junto a su
política en la misma sesión y **no** tuvo una fase roja propia; se declara aquí para no atribuirle un
rigor que no tuvo. / The two adapter-facing test files were written before the adapter and the
catalog existed and their RED is retained above. The pure policy test was written alongside its
policy in the same sitting and had no red phase of its own; that is stated rather than implied.

GREEN ejecuta 389 pruebas con 0 fallos y 0 omitidas en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T23/green/`. La cobertura de líneas del código nuevo es 92,27 %
(215/233) y `AudioOutputPolicy` alcanza 100 % de ramas. / GREEN runs 389 tests with zero failures and
zero skips; new-code line coverage is 92.27% and the output policy reaches 100% branch coverage.

## Adaptación documentada respecto al plan / Documented adaptation

El plan sitúa `WindowsAudioDeviceCatalog.cs` en el proyecto de infraestructura. Ese proyecto tiene
como destino `net10.0` y la prueba de arquitectura `Only_the_Windows_host_targets_Windows` exige que
sólo el host apunte a Windows; leer el registro de endpoints y consultar el enumerador COM de audio
son APIs exclusivas de Windows. El catálogo vive por tanto en
`src/ApSolutions.LocalMedia.Windows/Playback/WindowsAudioDeviceCatalog.cs`, detrás del puerto de
dominio `IAudioDeviceCatalog`, y el adaptador LibVLC permanece en infraestructura como el plan
indica. La regla de dependencias no cambia. / The plan places the Windows catalog in the
infrastructure project, but that project targets a platform-neutral framework and an architecture
test requires only the host to target Windows; the catalog therefore lives in the host behind its
domain port, while the LibVLC adapter stays in infrastructure as planned.

## Selección, persistencia y fallback / Selection, persistence, and fallback

- La selección usa el **identificador de endpoint estable** del sistema, no el nombre ni la posición,
  de modo que sobrevive a un reinicio y a una reconexión.
- Si el dispositivo guardado no está presente, responde el predeterminado y se informa
  `FellBackToDefaultDevice`. **La preferencia no se reescribe**: al volver a conectar el dispositivo,
  vuelve a ser el elegido. Verificado desconectando y reconectando en la prueba de ciclo de vida.
- Sin ningún endpoint disponible la política devuelve *nada* en lugar de inventar uno, y la interfaz
  lo dice.
- Un cambio en caliente **pausa, cambia y reanuda**, en ese orden exacto y una sola vez; nunca
  detiene ni reabre el medio, así que la posición y las pistas se conservan.
- La preferencia se guarda en la columna `audio_output_device_id` de la migración `0010`, la misma
  que T20 creó, sin migración adicional.

/ Selection uses the stable endpoint identifier, falls back to the default without rewriting the
preference, reports nothing when there is no output at all, and a hot switch pauses, switches, and
resumes exactly once, storing the choice in the column the earlier migration already created.

## Disposiciones de canales / Channel layouts

Lo que **el contenido** transporta, verificado con medios generados y decodificados de verdad:

| Muestra / Sample | Códec de audio / Audio codec | Canales observados / Observed channels |
|---|---|---:|
| `mkv-audio-stereo` | AAC | 2 |
| `mkv-audio-51` | E-AC-3 | 6 |
| `mkv-audio-71` | FLAC | 8 |

E-AC-3 en la compilación local de ffmpeg rechaza 7.1, así que la fila de ocho canales usa FLAC, que
transporta la disposición sin pérdida. La adaptación queda en el manifiesto. / The local FFmpeg build
refuses 7.1 in E-AC-3, so the eight-channel row uses FLAC, which is recorded in the manifest.

Lo que **este equipo** puede reproducir, leído de los endpoints activos:

| Disposición / Layout | Endpoints que la admiten / Endpoints accepting it | Veredicto / Verdict |
|---|---:|---|
| Estéreo / Stereo | 4 | verificable / verifiable |
| 5.1 | 0 | **bloqueo de hardware** / hardware block |
| 7.1 | 0 | **bloqueo de hardware** / hardware block |

Los cuatro endpoints activos —dos salidas de audio DisplayPort de los ASUS ProArt PA279CRV, los
auriculares Logitech G535 y la salida digital Realtek— declaran un formato de mezcla de **dos
canales**. Ninguno acepta 5.1 ni 7.1, de modo que esas dos filas **no se han verificado** y se
registran como bloqueo de hardware, no como resultado superado. El registro se conserva en
`artifacts/test-results/T23/green/audio-endpoints.csv`. / All four active endpoints declare a
two-channel mix format, so the surround rows were not verified and are recorded as a hardware block
rather than as a pass; the raw record is retained at the path above.

Un origen 7.1 sobre un endpoint estéreo se informa como **reducido**, con la disposición que se pidió
y la que se usó, en lugar de anunciarse como envolvente. / A 7.1 source on a stereo endpoint is
reported as reduced, with both the requested and the applied layout.

## Passthrough / Passthrough

`PLY-015` sigue `OUT_OF_SCOPE` y no recibe código. `AudioOutputPolicy.SupportsBitstreamPassthrough`
es constante `false`, el enumerado de disposiciones sólo contiene `Stereo`, `Surround51` y
`Surround71`, y una prueba comprueba que ningún nombre menciona Dolby, DTS ni bitstream. La interfaz
no ofrece ninguna opción de passthrough. / The passthrough identifier stays out of scope with no
code: the constant is false, the layout enumeration names no vendor format, and a test asserts it.

## Límites y privacidad / Boundaries and privacy

T23 no añade cliente de red ni telemetría. El catálogo hace dos lecturas locales: la clave de
endpoints de render del registro y el enumerador de dispositivos de audio para conocer el
predeterminado. La evidencia registra **nombres de modelo** de los dispositivos, que son
reproducibles, y deja fuera los identificadores de endpoint. Ningún archivo multimedia se modifica. /
T23 adds no network client or telemetry; the catalog performs two local reads, and the evidence
records device model names while leaving endpoint identifiers out.

`PLY-004` continúa `IN_PROGRESS`: la selección de dispositivo, la persistencia, el fallback y el
cambio en caliente están verificados, pero las disposiciones 5.1 y 7.1 no pueden comprobarse en este
equipo. Pasará a `VERIFIED` cuando exista un endpoint multicanal, no antes. / The audio identifier
stays in progress: device selection, persistence, fallback, and hot switching are verified, but the
surround layouts cannot be checked on this machine and the identifier will only move when a
multichannel endpoint exists.
