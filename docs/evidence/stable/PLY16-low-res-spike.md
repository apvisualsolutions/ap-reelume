# PLY-016 — Spike de mejora para baja resolución / Low-resolution enhancement spike

- Fecha / Date: 2026-08-09
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `1c505eb`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, LibVLCSharp 3.10.0,
  LibVLC 3.0.23.1, GPU NVIDIA GeForce RTX 5070
- IDs: `PLY-016=DEFERRED`
- Plan: [2026-08-09-low-res-enhancement.md](../../superpowers/plans/2026-08-09-low-res-enhancement.md)

## Veredicto / Verdict

**Ninguno de los cuatro candidatos paga y PLY-016 queda `DEFERRED`.** La causa no es que los
filtros carezcan de efecto visual, sino que **nunca llegan a procesar un fotograma**: el
constructor de la cadena de filtros del vout de VLC 3 falla al compensar los formatos con la
salida de vídeo por callbacks que esta aplicación necesita, y retira todos los filtros con el
error `Failed to compensate for the format changes, removing all filters`. El fallo es
independiente de la vía de activación (opción de medio o de instancia), del decodificador
(D3D11 o software) y del chroma de salida (RV32 e I420 planar). La fase 2 del plan no se
ejecuta: su condición era implementar sólo lo que el spike demostrara. / **None of the four
candidates pays and PLY-016 goes to `DEFERRED`.** The cause is not that the filters lack visual
effect — they **never process a single frame**: VLC 3's vout filter-chain builder fails to
compensate formats against the callback video output this application requires and removes every
filter with the error above. The failure is independent of the activation route (media or
instance option), the decoder (D3D11 or software), and the output chroma (RV32 and planar I420).
Phase 2 does not run: its condition was to implement only what the spike proved.

## Método / Method

`LowResEnhancementSpikeTests` (MediaTests, decodificación real) mide cada combinación sobre dos
muestras sintéticas locales: la nueva `PLY16/mpeg2-480p-noisy.mkv` (720×480 MPEG-2 a 900 kb/s
con ruido temporal de origen — el material de era DVD que motiva PLY-016) y la
`T18/h264-aac.mp4` existente (320×240 H.264). La métrica es la **varianza del laplaciano 3×3
sobre el plano gris** de cada fotograma entregado durante una ventana de 2,5 s, promediada; los
fotogramas salen de la misma ruta RV32 por callbacks que el motor publica por
`IVideoFrameSource` (la línea base RED usa el motor real; los candidatos usan un arnés que
replica su sink y añade las opciones que el motor aún no acepta). El coste se mide con la misma
instrumentación en línea base y candidatos, así que los deltas son atribuibles al filtro. / The
spike measures each combination on two locally generated synthetic samples — a new 720×480
MPEG-2 at a starved bitrate with temporal source noise, and the existing 320×240 H.264 — using
the variance of the 3×3 Laplacian over each delivered frame's grey plane, averaged across a
2.5 s window. Frames come from the same RV32 callback path the engine publishes through its
frame source; the RED baseline uses the production engine, the candidates use a harness that
replicates its sink and adds the options the engine cannot yet carry. Cost is measured with
identical instrumentation on baseline and candidates, so deltas are attributable to the filter.

## RED — línea base sin filtros / RED — unfiltered baseline

Por el motor real e `IVideoFrameSource`, archivada en
`artifacts/test-results/PLY16/red/PLY16-baseline-engine.csv`: / Through the production engine
and its frame source, archived at the path above:

| Muestra / Sample | Fotogramas / Frames | Varianza laplaciana media / Mean Laplacian variance | Apertura→1er fotograma (ms) / Open→first frame (ms) |
|---|---:|---:|---:|
| `mpeg2-480p-noisy` (720×480) | 63 | 394,04 | 256 |
| `h264-aac` (320×240) | 38 | 1169,35 | 332 |

## Matriz de candidatos / Candidate matrix

Las cadenas del plan, aplicadas como opciones de medio con decodificación por hardware (la
predeterminada del motor) y por software (`:avcodec-hw=none`, también aplicable por medio).
Archivada en `artifacts/test-results/PLY16/green/PLY16-candidates.csv`. / The plan's chains,
applied as media options against hardware decoding (the engine default) and software decoding
(also a per-media option). Archived at the path above.

| Muestra / Sample | Candidato / Candidate | Varianza media / Mean variance | Δ vs línea base / Δ vs baseline |
|---|---|---:|---:|
| `mpeg2-480p-noisy` | *(sin filtro, hw / none, hw)* | 394,04 | — |
| `mpeg2-480p-noisy` | `sharpen` (σ=1,0) | 394,04 | 0 |
| `mpeg2-480p-noisy` | `hqdn3d` | 394,04 | 0 |
| `mpeg2-480p-noisy` | `postproc` (q=6) | 394,04 | 0 |
| `mpeg2-480p-noisy` | `swscale-mode=9` (lanczos) | 394,04 | 0 |
| `mpeg2-480p-noisy` | `hqdn3d`+`sharpen` | 394,04 | 0 |
| `mpeg2-480p-noisy` | *(sin filtro, sw / none, sw)* | 393,99 | — |
| `mpeg2-480p-noisy` | los cinco con decodificación sw / all five, sw decode | 393,99 | 0 |
| `h264-aac` | *(sin filtro, hw / none, hw)* | 1169,35 | — |
| `h264-aac` | los cinco con hw / all five, hw | 1169,35 | 0 |
| `h264-aac` | *(sin filtro, sw / none, sw)* | 926,88 | — |
| `h264-aac` | los cinco con sw / all five, sw | 926,88 | 0 |

**La métrica es sensible**: el mero cambio de decodificador (hw→sw) mueve la varianza de
1169,35 a 926,88 en la muestra H.264 — una diferencia real de píxeles que la métrica detecta.
Si un filtro hubiera procesado un solo fotograma, se habría visto. / **The metric is
sensitive**: merely switching decoders moves the H.264 variance from 1169.35 to 926.88 — a real
pixel difference the metric detects. Had any filter processed a single frame, it would show.

## Controles: atribución de la causa / Controls: attributing the cause

1. **Instancia, RV32** (`--video-filter=sharpen --sharpen-sigma=2.0` en la propia LibVLC — la
   vía que el diseño rechazó, usada como grupo de control): varianza idéntica (394,04 = 394,04).
   El log nativo capturado muestra la cadena montándose y desmontándose:
   `using video filter module "sharpen"` → `Adding a filter to compensate for format changes` →
   decenas de `Filter 'Swscale' appended/removed` → **`[Error] Failed to compensate for the
   format changes, removing all filters`** → `removing module "sharpen"`. Con decodificación
   D3D11 el log también muestra `A filter to adapt decoder DX11 to display RV32 is needed`. /
   **Instance level, RV32**: identical variance. The captured native log shows the chain being
   built and torn down, ending in the error above.
2. **Instancia, I420** (un sink de diagnóstico que pide el chroma planar que los filtros de CPU
   de VLC procesan, midiendo el plano de luma directamente): mismo error, misma varianza
   intacta (356,31 = 356,31). El chroma de salida no es el bloqueador. / **Instance level,
   I420** (a diagnostic sink requesting the planar chroma VLC's CPU filters process): same
   error, same untouched variance. The output chroma is not the blocker.

Los logs quedan en `artifacts/test-results/PLY16/green/PLY16-control-vlc-log-*.txt` y los CSV de
ambos controles junto a la matriz. / The logs and both control CSVs sit next to the matrix.

## Veredicto por candidato / Per-candidate verdict

| Candidato / Candidate | Veredicto / Verdict | Motivo / Reason |
|---|---|---|
| `sharpen` | DESCARTADO / DISCARDED | Nunca procesa: la cadena del vout se retira entera en la ruta por callbacks. / Never processes: the vout chain is removed whole on the callback path. |
| `hqdn3d` | DESCARTADO / DISCARDED | Mismo mecanismo y mismo resultado medido. / Same mechanism, same measured result. |
| `postproc` | DESCARTADO / DISCARDED | Mismo mecanismo y mismo resultado medido. / Same mechanism, same measured result. |
| `swscale-mode` | DESCARTADO / DISCARDED | Sin efecto medible: en esta ruta no hay reescalado de VLC — el fotograma sale al tamaño de origen y escala la composición de la interfaz. / No measurable effect: there is no VLC rescale on this path — frames leave at source size and the UI composition scales. |

## Alternativas futuras, con su coste nombrado / Future alternatives, with their cost named

Ninguna se adopta ahora; se documentan para que el hueco nunca sea silencioso: / None adopted
now; documented so the gap is never silent:

- **VLC 4 / LibVLCSharp 4** (`d3d11-upscale-mode`, super-resolución NVIDIA/Intel): la vía
  correcta a largo plazo; exige esperar a LibVLCSharp estable 4.x y re-evaluar toda la
  disciplina nativa del motor. / The right long-term route; requires stable LibVLCSharp 4 and
  re-validating the engine's native discipline.
- **Realce administrado sobre los fotogramas BGRA** (unsharp mask propio en el sink del motor,
  antes de publicar cada fotograma): el único camino por sesión que funciona con VLC 3 en esta
  ruta, porque los píxeles ya están en memoria del proceso; cuesta CPU por fotograma (a 480p,
  del orden de milisegundos por fotograma en este equipo) y es código de imagen propio que hay
  que probar y presupuestar. Decisión de alcance del propietario, no un defecto pendiente. /
  **Managed enhancement over the BGRA frames** (an own unsharp mask in the engine sink): the
  only per-session route that works with VLC 3 on this path, since pixels are already in process
  memory; costs per-frame CPU and is own image code to test and budget. An owner scope decision,
  not a pending defect.
- **Cambiar de motor (mpv/madVR)**: otra decisión de arquitectura, fuera de este alcance. /
  **Switching engines**: a separate architecture decision, out of this scope.

## Reproducibilidad y privacidad / Reproducibility and privacy

El spike es re-ejecutable: las cuatro pruebas de `LowResEnhancementSpikeTests` quedan en la
suite MediaTests y regeneran sus muestras y mediciones en cada corrida (tras una futura subida
de LibVLC, re-correrlas responde si el bloqueo persiste). Las dos muestras son sintéticas
(`testsrc2` + seno, más ruido generado), producidas localmente bajo `artifacts/test-media`
(ignorado por Git); ningún archivo personal se lee ni se copia, y ninguna ruta local, usuario o
equipo aparece en código o documentación. / The spike is re-runnable: the four tests stay in the
MediaTests suite and regenerate their samples and measurements on every run — after a future
LibVLC upgrade, re-running them answers whether the blocker persists. Both samples are
synthetic, produced locally under an ignored artifacts tree; no personal file is read or copied
and no local path, user, or machine name appears in code or documentation.
