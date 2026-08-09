# T25 — Persistencia atómica de posición / Atomic position persistence

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `c3aedaf`
- Commit de tarea / Task commit: `feat: persist and resume playback within five seconds`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, PowerShell 7.6.3,
  Avalonia 12.1.1, SQLite en WAL / SQLite in WAL mode
- IDs: `PLY-008=VERIFIED`, `DAT-001=IMPLEMENTED`

## RED y GREEN / RED and GREEN

`ProgressPolicyTests`, `ProgressTrackerTests` y `CrashResumeTests` se escribieron antes que el modelo,
la política, el rastreador y el repositorio. RED falló con 27 errores de compilación por tipos
ausentes —`PersistenceTrigger`, `ContentKey`, `WatchState`, el espacio de nombres
`Application.Continuity` y `WatchStateRepository`—; la salida está en
`artifacts/test-results/T25/red/`. / The three plan-named test files were written first and RED failed
with 27 compilation errors, every one of them a missing type.

El aviso de reanudación se abordó en una **segunda tanda RED dentro de la misma tarea**: sus
aserciones se añadieron a `PlayerViewModelTests` —el archivo del reproductor ya existente, para no
crear pruebas que el plan no nombra— y se demostró su RED retirando `ResumePromptViewModel`, con lo que
la vista dejó de resolver su tipo (`AVLN2000`, en `artifacts/test-results/T25/red/prompt.log`). Se
declara aquí porque el orden no fue el ideal: el modelo de vista existía antes que su prueba y se
rehízo el ciclo. / The resume prompt was covered by a second RED pass inside the same task; this is
stated plainly because the prompt's view model briefly existed before its test.

GREEN ejecuta **474 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T25/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias y 0 errores. / GREEN runs 474 tests with zero failures
and zero skips; formatting and both builds are clean.

## Adaptación del número de migración / Migration number adaptation

El plan nombra `0009_watch_state.sql`, pero el manifiesto ya ocupaba `0001`–`0010` porque
`0010_playback_preferences.sql` la creó T20. Esta tarea usa **`0011_watch_state.sql` con
`"version": 11`**, sin renumerar ni sobrescribir nada, igual que se documentó en
[T20](T20-tracks-subtitles.md). El `sha256` del manifiesto se calcula sobre el texto UTF-8 del archivo,
como hace `MigrationRunner`. `SqliteBootstrapTests` se actualizó a 11 migraciones, 11 copias previas, la
lista de nombres con `watch_state` y la lista de tablas con `watch_state`. / The plan's number was
already taken, so this task uses migration eleven without renumbering anything.

## Qué se guarda y cuándo / What is stored and when

| Regla / Rule | Comportamiento verificado / Verified behaviour |
|---|---|
| Intervalo / Interval | `ProgressPolicy.SaveInterval` es exactamente `5 s` y es lo que el bucle pide al reloj inyectado |
| Disparadores críticos / Critical triggers | `Pause`, `Seek`, `ModeChange`, `FileChange`, `Close` y `EngineFailure` escriben **aunque la posición no se haya movido** |
| Debounce | un tick que repite la posición almacenada no escribe; con menos de `1 s` de diferencia tampoco |
| Último valor / Latest value | entre dos escrituras sólo sobrevive la última observación: tres observaciones producen una escritura con la tercera |
| Sin E/S al observar / No I/O while observing | mil observaciones seguidas no producen ninguna llamada al repositorio, así que el hilo que atiende al motor nunca escribe en disco |
| Rango / Range | la posición se limita a `0..duración`: `90 min` sobre un medio de `50 min` se guarda como `50 min` y `-40 s` como `0` |
| Serialización / Serialisation | cincuenta escrituras concurrentes no se solapan y la serie termina en la última observación |
| Cierre acotado / Bounded close | con un repositorio que tarda `30 s`, un `Close` con límite de `150 ms` devuelve el control de inmediato y queda registrado como escritura abandonada |
| Reanudación mínima / Minimum resume | por debajo de `30 s` no se ofrece reanudar; en `29,999 s` no y en `30 s` sí; en el final exacto tampoco |
| Sesión reanudada / Resumed session | la fecha de inicio original y la versión de origen se conservan; un override manual almacenado nunca lo borra el progreso |

## Veinte cierres forzados / Twenty forced closes

Cada ensayo lanza un proceso hijo que reproduce un medio simulado sobre un reloj comprimido —un segundo
simulado cada 20 ms reales— dejando en una traza el punto que alcanzó de verdad. El padre lo mata sin
aviso en un instante aleatorio con semilla fija y compara la traza contra lo que la base de datos
confirmó. El hijo no hereda el perfilador de cobertura, para que no sobrescriba los datos del padre. /
Each trial kills a child process at a random moment and compares what it actually reached against what
the database committed.

| Ensayo / Trial | Cierre a / Killed at (ms) | Alcanzado / Reached (s) | Persistido / Persisted (s) | Error (s) |
|---:|---:|---:|---:|---:|
| 1 | 1568 | 56 | 55 | 1 |
| 2 | 1029 | 36 | 36 | 0 |
| 3 | 395 | 14 | 14 | 0 |
| 4 | 1428 | 50 | 47 | 3 |
| 5 | 1147 | 39 | 39 | 0 |
| 6 | 2474 | 82 | 81 | 1 |
| 7 | 2369 | 78 | 78 | 0 |
| 8 | 2047 | 67 | 67 | 0 |
| 9 | 1507 | 51 | 50 | 1 |
| 10 | 850 | 29 | 28 | 1 |
| 11 | 1720 | 58 | 57 | 1 |
| 12 | 871 | 30 | 29 | 1 |
| 13 | 1225 | 41 | 39 | 2 |
| 14 | 1332 | 44 | 42 | 2 |
| 15 | 1090 | 36 | 35 | 1 |
| 16 | 861 | 29 | 28 | 1 |
| 17 | 1953 | 65 | 64 | 1 |
| 18 | 1622 | 54 | 53 | 1 |
| 19 | 1213 | 41 | 39 | 2 |
| 20 | 1994 | 66 | 63 | 3 |

**20/20 dentro de ±5 s; el peor error medido es 3 s.** La serie completa, regenerada en cada ejecución,
queda en `artifacts/test-results/T25/green/forced-close-trials.csv`. Tras cada muerte,
`PRAGMA integrity_check` devuelve `ok`: la escritura es una única sentencia `UPSERT` dentro de una
transacción, así que el archivo conserva la posición anterior o la nueva, nunca una fila a medias. /
All twenty trials land inside the tolerance, the worst measured error is three seconds, and the database
passes its integrity check after every kill.

Los ensayos anteriores a la primera escritura periódica no son un caso especial: sin fila almacenada la
posición reanudable es cero y el error sigue por debajo del límite. / A trial killed before the first
periodic write is not a special case.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Domain/Continuity/ContinuityModels.cs` | 30/30 — 100 % |
| `Domain/Continuity/ProgressPolicy.cs` | 13/13 — 100 % |
| `Application/Continuity/PlaybackProgressTracker.cs` | 98/100 — 98,0 % |
| `Application/Continuity/ResumePlayback.cs` | 12/12 — 100 % |
| `Infrastructure/Data/Repositories/WatchStateRepository.cs` | 73/73 — 100 % |
| `Presentation/Player/ResumePromptViewModel.cs` | 28/28 — 100 % |
| **Total del código nuevo / New code total** | **254/256 — 99,22 %** |

Las dos líneas sin cubrir del rastreador son las salidas del bucle periódico cuando la cancelación llega
entre la espera y la escritura. Las ramas de `ProgressPolicy`, la política de dominio de esta tarea,
están cubiertas al **100 %**. / The two uncovered lines are cancellation exits in the periodic loop;
the domain policy has full branch coverage.

## Privacidad y límites / Privacy and boundaries

- **Sin telemetría ni red**: T25 no añade cliente HTTP, socket ni resolución de nombres.
- **Sin rutas en el progreso**: la clave de contenido es `title:<guid>` o `title:<guid>/episode:<guid>`;
  la tabla `watch_state` no guarda ninguna ruta ni nombre de archivo.
- **Sin operaciones destructivas**: no se borra ni se escribe sobre ningún archivo multimedia; el hijo
  de prueba sólo escribe su propia traza en un directorio temporal que se elimina al terminar.
- **Artefactos ignorados**: los resultados y la tabla de ensayos viven bajo `artifacts/`, que sigue
  ignorada por Git.

/ No telemetry or network, no paths stored with progress, no destructive media operations, and every
artifact stays ignored.

`PLY-008` pasa a `VERIFIED`: la posición se guarda cada cinco segundos y en los seis disparadores, se
limita al rango observado, se escribe fuera del hilo que atiende al motor y veinte cierres forzados
reanudan dentro de ±5 s. `DAT-001` sigue `IMPLEMENTED`: esta tarea añade una migración versionada con
copia previa y escritura atómica verificada, pero la restauración completa se demuestra en I6. / The
progress identifier verifies; the data identifier stays implemented until the I6 restore proof.
