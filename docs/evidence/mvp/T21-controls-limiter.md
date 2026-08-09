# T21 — Controles, velocidad, saltos y limitador / Controls, speed, skips, and limiter

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `decb8c1`
- Commit de tarea / Task commit: `feat: add accessible playback controls and peak-limited boost`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, LibVLC 3.0.23.1, Avalonia 12.1.1,
  auriculares Logitech G535 y salida DisplayPort de las pantallas ASUS ProArt PA279CRV
- IDs: `PLY-006=VERIFIED`, `PLY-014=IN_PROGRESS`, `A11Y-001=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`PlaybackControlPolicyTests`, `VolumeBoostPolicyTests`, `PeakLimiterTests` y
`TransportControlsAutomationTests` se escribieron antes que las políticas, el filtro y la vista. RED
falló porque `PlaybackControlPolicy` y `VolumeBoostPolicy` no existían; la salida se conserva en
`artifacts/test-results/T21/red/`. / The four plan-named test files were written before the
policies, the filter, and the view existed; RED failed for missing types and is retained above.

GREEN ejecuta 339 pruebas con 0 fallos y 0 omitidas en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T21/green/`. La cobertura combinada de líneas del código nuevo es
93,95 % (264/281) y las políticas de dominio alcanzan 100 % de ramas. `dotnet format` y ambas
compilaciones terminan con 0 advertencias. / GREEN runs 339 tests with zero failures and zero skips;
new-code line coverage is 93.95% and the domain policies reach 100% branch coverage.

## Rangos aprobados / Approved ranges

| Control | Rango / Range | Comportamiento fuera de rango / Out-of-range behaviour |
|---|---|---|
| Velocidad / Speed | `0.25×` – `4.0×`, pasos `0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 3, 4` | Se recorta al extremo; nunca se rechaza / clamped, never refused |
| Salto atrás / Backward skip | inicial `10 s`, configurable `1 s` – `10 min` | Cero o negativo pasa al mínimo / clamped to the minimum |
| Salto adelante / Forward skip | inicial `30 s`, configurable `1 s` – `10 min` | Excesivo pasa al máximo / clamped to the maximum |
| Posición / Position | `0` – duración observada / observed duration | Aterriza exactamente en el límite / lands exactly on the boundary |
| Volumen normal / Normal volume | `0 %` – `100 %` | Negativo pasa a `0` |
| Refuerzo / Boost | `101 %` – `200 %` | Por encima pasa a `200 %` |

Un salto cerca de cualquier extremo aterriza **exactamente** en él en lugar de pasarse. Cien
cambios rápidos alternando velocidad y volumen terminan en un estado determinista, comprobado tanto
en la política pura como en el caso de uso. / A skip near either boundary lands exactly on it, and a
hundred rapid alternating changes end deterministically in both the pure policy and the use case.

## Refuerzo y limitador / Boost and limiter

El refuerzo y el limitador son **inseparables por construcción**: `VolumeBoostPolicy.Decide` marca
`LimiterEngaged` y `RequiresWarning` siempre que el porcentaje supera 100, y el adaptador LibVLC
rechaza con `EngineUnavailable` cualquier decisión que pida refuerzo sin limitador. Silenciar
conserva el nivel elegido y devuelve ganancia lineal cero; restaurar recupera el nivel exacto. /
Boost and limiter are inseparable by construction: the decision always carries the limiter and the
warning above one hundred percent, and the adapter refuses a boosted decision without its limiter.
Muting keeps the chosen level and returns zero gain.

`PeakLimiterAudioFilter` es un limitador de pico con ataque de 1,5 ms, liberación de 60 ms, umbral
`0,98` y recorte final de garantía. Medido:

| Señal / Signal | Ganancia / Gain | Pico bruto / Raw peak | Pico limitado / Limited peak | Limitador / Limiter |
|---|---:|---:|---:|---|
| Barrido 20 Hz–18 kHz a fondo de escala / full-scale sweep | 1,01 / 1,50 / 2,00 | 1,0000 | `≤0,9800` en las tres | enganchado / engaged |
| Barrido a 0,4 de amplitud / sweep at 0.4 amplitude | 1,00 | 0,4000 | 0,4000 (idéntico) | no engancha / transparent |
| Escalón de fondo de escala / full-scale step | 2,00 | 1,0000 | `≤0,9800` desde la primera muestra | enganchado / engaged |
| `mp4-h264-aac` decodificado real / real decoded | 2,00 | 0,1883 | 0,3765 | no engancha / not needed |
| `mp4-h264-aac` normalizado a fondo de escala / normalised | 2,00 | 1,0000 | 0,9800 | enganchado / engaged |

Los valores medidos se conservan en `artifacts/test-results/T21/green/limiter-peaks.csv`. Ninguna
muestra de salida supera el pico normalizado en ninguna combinación probada. / The measured values
are retained at the path above and no output sample exceeds the normalised peak in any tested
combination.

**Qué está demostrado y qué no.** Está demostrado que el limitador gestionado nunca supera el techo,
con señal sintética y con PCM real decodificado, y que el motor conecta además el limitador nativo
de LibVLC como opción de medio en **toda** apertura —`audio-filter=compressor` con umbral `-1 dB`,
ratio `20:1`, ataque `1,5 ms` y liberación `60 ms`—, de modo que subir el nivel nunca puede
adelantarlo. No se ha medido la salida analógica de la tarjeta de sonido: eso exigiría captura de
audio del sistema y no forma parte de esta tarea. / What is proven: the managed limiter never
exceeds the ceiling, on synthetic and on real decoded audio, and the engine also attaches the native
LibVLC limiter to every media so raising the level cannot outrun it. What is not: the analogue
output was not captured, which would require system audio capture outside this task.

## Controles accesibles / Accessible controls

`TransportControlsView` declara cuatro controles —retroceder, avanzar, silenciar y volumen—, todos
con nombre de automatización, tecla anunciada y foco de teclado, en español e inglés. El árbol se
conserva en `artifacts/ui-captures/T21/transport-uia-es-ES.txt` y `-en-US.txt`. / The transport view
declares four controls, each with an automation name, an announced accelerator, and keyboard focus,
in both languages; the trees are retained above.

El aviso de refuerzo es **visual y textual**: un indicador y una frase que explica que el nivel
supera el 100 % y que el limitador está activo. Aparece al pasar a 150 % y desaparece al volver a
100 %, comprobado sobre el árbol visual real. / The boost warning is both visual and textual and
follows the level in real time.

Ocultar la barra sólo cambia su opacidad a cero: el borde sigue en el árbol visual, los botones
siguen siendo enfocables y ninguna acción se pierde para quien navega con teclado. / Hiding the bar
only changes its opacity, so the controls stay in the visual and focus tree.

## Límites y privacidad / Boundaries and privacy

T21 no añade cliente de red ni telemetría. El limitador procesa muestras en memoria y no escribe
audio en disco salvo el PCM temporal que la prueba decodifica bajo `artifacts/test-media/`, ignorado
por Git. Ningún archivo de la biblioteca personal se lee ni se modifica. / T21 adds no network client
or telemetry; the limiter works in memory and the only decoded PCM lands in an ignored directory.

`PLY-006` pasa a `VERIFIED`: velocidad, saltos configurables y volumen amplificado con limitador
demostrado y recordado por ámbito. `PLY-014` continúa `IN_PROGRESS` porque ratón, teclas multimedia
y atajos configurables cierran en T24. `A11Y-001` continúa `IN_PROGRESS` hasta la auditoría integral.
/ The controls identifier is verified; the input identifier waits for T24 and the accessibility
identifier for the later audit.
