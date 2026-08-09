# T26 — Máquina de estados y umbral visto / Watch state machine and threshold

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `e9bb491`
- Commit de tarea / Task commit: `feat: track watched state with manual overrides`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1
- IDs: `PLY-009=VERIFIED`, `UX-001=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`WatchStatePolicyTests`, `ManualWatchOverrideTests` y `WatchStatusControlTests` se escribieron antes
que la política, los comandos y el control. RED falló porque `WatchStatePolicy`, `SetWatchStatus`,
`ConfigureWatchedThreshold`, `WatchStatusViewModel` y `WatchStatusControl` no existían; la salida está
en `artifacts/test-results/T26/red/`. / The three plan-named test files were written first and RED
failed on the missing policy, commands, and control.

GREEN ejecuta **513 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T26/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias. / GREEN runs 513 tests with zero failures and zero
skips.

## Un defecto encontrado por la prueba de concurrencia / A defect the race test found

La prueba que hace competir progreso y decisión manual **falló primero contra la implementación de
T25**: el rastreador guardaba en memoria el estado leído al empezar la sesión, así que marcar algo como
visto desde el catálogo mientras se reproducía quedaba borrado en la siguiente escritura periódica.
`PlaybackProgressTracker` ahora **relee la fila almacenada dentro de cada escritura** en lugar de fiarse
de su copia, con el coste de una lectura cada cinco segundos. Sin esa corrección el requisito «el
override siempre gana» era falso en el caso más natural: marcar visto sin parar la reproducción. / The
race test failed against the T25 implementation because the tracker cached the row it read at session
start; it now re-reads inside every write, which is what makes the override rule true while playing.

## Estados y fronteras / States and boundaries

| Regla / Rule | Comportamiento verificado / Verified behaviour |
|---|---|
| Constantes / Constants | umbral predeterminado `0,90`, rango `0,50`–`1,00` |
| Avance significativo / Significant progress | el menor de `60 s` y el `2 %`: `60 s` en un episodio de 50 min, `24 s` en uno de 20 min, `12 s` en uno de 10 min |
| Duración desconocida / Unknown duration | vuelve a la regla de `60 s` y **nunca** alcanza «visto» |
| Sin empezar / Not started | `0 s`, `30 s` y `59,9 s` siguen sin empezar; `60 s` pasa a en curso |
| Frontera del umbral / Threshold boundary | `89,99 %` es en curso y `90,00 %` es visto, igual que el final exacto |
| Tabla exhaustiva / Exhaustive table | los 101 porcentajes enteros de 0 a 100 caen en el estado esperado |
| Umbral más bajo / Lower threshold | al `50 %`, un `55 %` ya cuenta como visto |
| Umbral fuera de rango / Out-of-range threshold | `0,20` se limita a `0,50`, `1,50` a `1,00` y un valor no numérico vuelve al predeterminado |
| Duración cero o negativa / Zero or negative duration | se trata como desconocida en lugar de dividir por cero |

## El override manual / The manual override

- Marcar visto o no visto **persiste** con la marca de decisión manual.
- Marcar como no visto **conserva la posición almacenada**: es una afirmación sobre el estado, no una
  orden de olvidar dónde se quedó, así que la reanudación sigue disponible.
- La reproducción posterior avanza la posición pero **no cambia** ni el estado ni la marca.
- Deshacer la decisión devuelve el estado a lo que dice la posición: `10 min` de `50` vuelve a «en
  curso».
- Deshacer algo que nunca se marcó no escribe nada.
- Veinticinco escrituras de progreso concurrentes seguidas de una decisión manual y una escritura de
  cierre terminan en «visto» con la marca puesta.

/ Manual decisions persist, keep the stored position, survive later playback, can be undone, and win a
race against twenty-five concurrent progress writes.

## El umbral configurable / The configurable threshold

Se guarda en el almacén de ajustes local bajo `continuity.watched-threshold`, se limita al rango al
leerlo y al escribirlo, y sobrevive a una instancia nueva del comando. Cambiarlo **sólo** recalcula los
estados automáticos: en la prueba con dos contenidos al `60 %`, bajar el umbral al `55 %` mueve el
automático a «visto» y deja intacto el que tiene decisión manual. Un cambio que no mueve ningún estado
no produce ninguna escritura. / The threshold is clamped, stored locally, and only ever recomputes
automatic states.

## El control / The control

Icono **y** palabras para cada estado, nunca color solo; exactamente una línea visible por estado; la
decisión manual se anuncia con una frase y el botón de deshacer sólo se habilita cuando hay algo que
deshacer; cada botón pide el cambio que su nombre indica; todos los controles tienen nombre de
automatización. Capturado en español e inglés en `artifacts/ui-captures/T26/`. / Icon and words per
state, one visible line, an announced override, an enabled-only-when-useful undo, named controls, and
bilingual captures.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Domain/Continuity/WatchStatePolicy.cs` | 15/15 — 100 % |
| `Application/Continuity/SetWatchStatus.cs` | 32/32 — 100 % |
| `Application/Continuity/ConfigureWatchedThreshold.cs` | 23/23 — 100 % |
| `Presentation/Catalog/WatchStatusViewModel.cs` | 32/33 — 96,97 % |
| **Total del código nuevo / New code total** | **102/103 — 99,03 %** |

Las ramas de `WatchStatePolicy` están cubiertas al **100 %** (16/16). / Full branch coverage on the
domain policy.

## Privacidad y límites / Privacy and boundaries

T26 no añade red ni telemetría, no escribe fuera del equipo y no toca ningún archivo multimedia. El
estado de visionado se guarda por clave de contenido, sin rutas ni nombres de archivo. / No network, no
telemetry, no media writes, and no paths in the stored state.

`PLY-009` pasa a `VERIFIED`. `UX-001` sigue `IN_PROGRESS`: esta tarea entrega los estados que Inicio
consumirá, pero el inicio híbrido es T30. / The status identifier verifies; the hybrid home waits for
its own task.
