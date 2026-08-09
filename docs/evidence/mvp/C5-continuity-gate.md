# C5 — Puerta de continuidad / Continuity gate

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Tareas cubiertas / Tasks covered: T25–T29
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, PowerShell 7.6.3,
  Avalonia 12.1.1, LibVLCSharp 3.10.0, LibVLC 3.0.23.1, SQLite en WAL, NVIDIA GeForce RTX 5070, dos
  ASUS ProArt PA279CRV a 2560×1440 con escala 150 % y HDR activo

## Resultado por tarea / Per-task result

| Tarea / Task | Commit | Evidencia / Evidence | Estado / Status |
|---|---|---|---|
| T25 | `e9bb491` `feat: persist and resume playback within five seconds` | [T25](T25-progress-resume.md) | superada / passed |
| T26 | `78ec82b` `feat: track watched state with manual overrides` | [T26](T26-watch-status.md) | superada / passed |
| T27 | `9d72373` `feat: transfer progress safely between media versions` | [T27](T27-version-progress.md) | superada / passed |
| T28 | `f36dcea` `feat: play the next available episode after countdown` | [T28](T28-next-episode.md) | superada con alcance declarado / passed with a declared scope note |
| T29 | `500c636` `feat: edit and use manual intro and credits markers` | [T29](T29-manual-markers.md) | superada / passed |
| Incidencia de handles / Handle incident | `3e42d45` `fix: attribute the handle growth to hardware decoding` | [C4 corregida / corrected](C4-playback-gate.md) | causa raíz identificada / root cause found |

## Condición 1 — Veinte cierres forzados dentro de ±5 s / Twenty forced closes within ±5 s

Cada ensayo mata sin aviso un proceso hijo que reproduce sobre un reloj comprimido y compara el punto
que alcanzó de verdad contra lo que la base de datos confirmó.

| Ensayo | Error (s) | Ensayo | Error (s) | Ensayo | Error (s) | Ensayo | Error (s) |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 2 | 6 | 3 | 11 | 4 | 16 | 1 |
| 2 | 3 | 7 | 3 | 12 | 3 | 17 | 0 |
| 3 | 0 | 8 | 1 | 13 | 3 | 18 | 0 |
| 4 | 2 | 9 | 3 | 14 | 2 | 19 | 2 |
| 5 | 3 | 10 | 0 | 15 | 1 | 20 | 2 |

**20/20 dentro de ±5 s; peor error 4 s.** Tras cada muerte, `PRAGMA integrity_check` devuelve `ok`. La
serie completa se regenera en cada ejecución en
`artifacts/test-results/T25/green/forced-close-trials.csv`. / All twenty trials land inside the
tolerance with a worst error of four seconds, and the database passes its integrity check every time.

## Condición 2 — Todas las transiciones y overrides pasan / Every transition and override passes

Los 101 porcentajes enteros de 0 a 100 caen en el estado esperado; la frontera está exactamente en el
umbral (`89,99 %` en curso, `90,00 %` visto); el umbral se limita a `0,50–1,00` y persiste; cambiarlo
recalcula **sólo** los estados automáticos. El override manual gana siempre: sobrevive a la
reproducción posterior, gana una carrera contra veinticinco escrituras de progreso concurrentes y sólo
se deshace con la acción inversa. / The exhaustive percentage table, the exact boundary, the clamped
and persisted threshold, the automatic-only recalculation, and an override that wins the race all pass.

Esa carrera **encontró un defecto real** en la implementación de T25: el rastreador guardaba en memoria
el estado leído al empezar la sesión, así que marcar algo visto mientras se reproducía quedaba borrado
en la siguiente escritura periódica. Ahora relee la fila dentro de cada escritura. / The race test
found a real defect and it is fixed.

## Condición 3 — Un cambio de versión fallido no corrompe el progreso / A failed switch preserves progress

La sesión anterior se escribe **antes** de abrir nada, comprobado por orden en el registro de la
prueba. Con una versión que se niega a abrir, el segundo almacenado, la duración observada y la versión
de origen quedan **idénticos**: nada se escribe para la versión nueva porque la escritura ocurre
después de que el motor acepte. Una versión marcada como no disponible se rechaza antes de pedir nada
al motor, y una confirmación pendiente no abre ni cambia nada. / The previous session reaches storage
first, a refused open leaves progress and audit untouched, and a pending confirmation changes nothing.

## Condición 4 — La retirada del siguiente episodio vuelve a la ficha / A missing next episode returns to details

Con un segundo por delante en la cuenta atrás, retirar el archivo hace que la revalidación en cero lo
detecte: el resultado es «no disponible» y no se abre nada. Sin siguiente reproducible el resultado es
«no hay siguiente», que es lo que devuelve la interfaz a la ficha. La cuenta se cancela desde teclado,
ratón y tecla multimedia. Tres episodios encadenados registran un máximo de **una** sesión simultánea.
/ Removal at one second is caught by the last-moment revalidation, an empty result returns to the
details, all three input origins cancel, and three chained episodes never hold two sessions.

## Condición 5 — Ninguna detección automática habilitada / No automatic detection enabled

- Ningún tipo cuyo nombre contenga «Detect» existe en el dominio ni en los casos de uso.
- `CompositionRoot` no registra ningún servicio de detección.
- El editor de marcas no expone ninguna propiedad, método ni control de detección.
- `SaveManualMarkerCommand` no tiene campo de origen: sólo puede producir `Origin = Manual`.
- `DeleteManualMarker` se niega a borrar un rango con `Origin = Detected`.

`PLY-013` pasa a `PLANNED` con objetivo `STABLE` —está incluido en un plan aprobado, la Tarea 43— y no
más allá: nada lo implementa. / Nothing detects anything; the detection identifier moves to planned
because it sits in an approved plan, and no further.

## Verificación transversal / Cross-cutting verification

| Comprobación / Check | Resultado / Result |
|---|---|
| `dotnet restore --locked-mode` | correcto / clean |
| `dotnet build -c Debug -warnaserror` | 0 advertencias, 0 errores |
| `dotnet build -c Release -warnaserror` | 0 advertencias, 0 errores |
| `dotnet format --verify-no-changes` | sin cambios / no changes |
| Suite completa `Release` / Full Release suite | **616 pruebas, 0 fallos, 0 omitidas** |
| `eng/verify.ps1 -Configuration Release -Runtime win-x64` | superada / passed |
| `eng/verify-docs.ps1` | 43 Markdown, 6 localizados, 53 IDs, 46 MVP |
| `dotnet list package --vulnerable --include-transitive` | ningún paquete vulnerable / none |
| `dotnet list package --deprecated` | ningún paquete en desuso / none |
| Cobertura de líneas del código nuevo de I4 / New I4 code line coverage | **891/903 — 98,67 %** |
| Ramas de las cinco políticas de dominio / Branches of the five domain policies | **90/90 — 100 %** |
| Migraciones / Migrations | 12 aplicadas, 12 copias previas válidas, `integrity_check` en `ok` |

La suite pasó de **431** pruebas al cerrar I3 a **616** al cerrar I4. / The suite grew from 431 to 616.

## Cobertura por tarea / Per-task coverage

| Tarea / Task | Líneas del código nuevo / New-code lines |
|---|---:|
| T25 | 254/256 — 99,22 % |
| T26 | 102/103 — 99,03 % |
| T27 | 142/143 — 99,30 % |
| T28 | 130/132 — 98,48 % |
| T29 | 250/256 — 97,66 % |

## Hardware / Hardware

I4 no introduce ninguna capacidad que dependa de hardware ausente: la continuidad es persistencia,
política y interfaz. Los dos bloqueos declarados en C4 —sin GPU integrada activa y ningún endpoint de
audio que acepte 5.1 o 7.1— **siguen vigentes y sin cambios**, y por eso `PLY-003` y `PLY-004`
permanecen `IN_PROGRESS`. Nada se ha sustituido por una simulación. / I4 introduces no
hardware-dependent capability; the two C4 blocks stand unchanged and nothing was simulated.

La investigación de la incidencia de handles sí es un resultado físico de esta máquina: con
decodificación por hardware D3D11VA sobre la RTX 5070 el proceso gana unos dos handles por ciclo, y con
decodificación por software gana **cero**. / The handle investigation is a physical result of this
machine.

## Privacidad y límites / Privacy and boundaries

- **Sin telemetría ni red**: no hay `HttpClient`, socket, `WebRequest` ni resolución de nombres en
  ningún archivo de continuidad.
- **Tráfico observado**: muestreando las conexiones TCP establecidas de los procesos de prueba durante
  las suites de continuidad y multimedia, los únicos extremos remotos fueron cuatro puertos de
  `127.0.0.1`, que es el canal entre el ejecutor y su host. **Ninguna conexión externa.**
- **Sin rutas en los datos de continuidad**: el progreso se guarda por clave de contenido
  (`title:<guid>` o `title:<guid>/episode:<guid>`) y las marcas por identificador de serie. Ninguna
  tabla nueva almacena rutas ni nombres de archivo.
- **Sin operaciones destructivas sobre medios**: ningún `File.Delete`, `File.Move` ni escritura sobre
  archivos multimedia en el código de continuidad, reproductor o catálogo.
- **Artefactos y medios ignorados**: `git status` no incluye `artifacts/` ni ningún archivo
  multimedia; `eng/verify.ps1` falla si alguno apareciera, y no apareció.
- **Sin datos personales versionados**: ningún archivo tocado entre `c3aedaf` y `HEAD` contiene nombre
  de usuario del sistema, nombre de equipo ni ruta absoluta local.

Deuda previa conocida y **no tocada**: `C2-library-gate.md` y `T6-scan.md` publican volumen de la
biblioteca y términos de búsqueda reales; es una decisión pendiente del propietario, anterior a hacer
público el repositorio. / Known pre-existing debt, deliberately untouched again.

## Salvedades declaradas / Declared caveats

1. **T28 sin adaptador de persistencia.** El plan de T28 no nombra migración ni repositorio de
   infraestructura, y el esquema no relaciona `episodes` con `media_files`. La secuencia de episodios
   se define como puerto de dominio y se prueba contra un repositorio en memoria, igual que T15 hizo
   con `IMediaVersionGroupRepository`. El adaptador SQLite llega con las fichas completas en T30.
2. **Numeración de migraciones adaptada.** `watch_state` es la `0011` y `intro_markers` la `0012`,
   porque los números que el plan citaba estaban ocupados. Nada se renumeró ni se sobrescribió.
3. **Incidencia de handles atribuida, no corregible aquí.** La causa está identificada y medida —la
   ruta de decodificación por hardware— y las dos rutas que sí pertenecen a este código quedan fijadas
   con pruebas. No se declara resuelta porque la corrección no está en este adaptador.

/ Three caveats, all declared rather than papered over.

## Resultado de la puerta / Gate result

**C5 se propone como superada.** Las cinco condiciones de la puerta I4 están demostradas con pruebas
reproducibles, la suite completa pasa sin fallos ni omisiones, la cobertura del código nuevo supera
ampliamente el mínimo y las ramas de las cinco políticas de dominio están completas. Ninguna capacidad
ausente se ha sustituido por una simulación ni se ha declarado como resultado superado.

La rama `codex/ap-reelume-mvp-x64` queda publicada, sin commits `wip:`, con `main` como antepasado
directo. **No se integra en `main`:** la aprobación corresponde al propietario antes de comenzar I5. /
C5 is proposed as passed with the three declared caveats above; the branch stays published and
unmerged for the owner's review.
