# T9 — Vigilancia y recuperación de eventos / Watching and event recovery

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit de tarea / Task commit: `feat: watch roots with incremental fallback recovery`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, NTFS temporal real y UNC simulada / real temporary NTFS and simulated UNC
- IDs: `LIB-002=IMPLEMENTED`, `LIB-003=VERIFIED`, `LIB-010=VERIFIED`

Este informe conserva español e inglés en cada sección. T9 añade eventos de
sistema de archivos consolidados, coordinación por raíz y escaneos de respaldo;
el presupuesto definitivo de escaneo/UI de `LIB-002` sigue perteneciendo a T10.
/ This report keeps Spanish and English in every section. T9 adds coalesced
file-system events, per-root coordination, and fallback scans; the final
scan/UI budget for `LIB-002` remains owned by T10.

## Resultado RED / RED result

Las pruebas se escribieron antes del comportamiento. Los TRX están en
`artifacts/test-results/T9/red/`. / Tests preceded the behavior; TRX evidence
is retained under the path above.

| Suite | RED demostrado / Proven RED |
|---|---|
| Contrato de aplicación / application contract | 0/1: faltaban reloj, lote, watcher, scheduler y coordinador / clock, batch, watcher, scheduler, and coordinator were absent |
| Contrato integrado / integration contract | 0/1: faltaban watcher, fallback, reloj y ajustes / watcher, fallback, clock, and settings were absent |
| `WatchCoordinatorTests` | 0/5: `RootWatchCoordinator` era un stub / coordinator was a stub |
| `FileWatcherRecoveryTests` | 0/3: watcher y scheduler eran stubs / watcher and scheduler were stubs |

Todos los RED fueron fallos funcionales por ausencia del comportamiento, no
fallos de SDK, compilación accidental ni entorno. / Every RED was a functional
missing-behavior failure, not an SDK, accidental compilation, or environment
failure.

## Resultado GREEN / GREEN result

| Verificación / Check | Resultado / Result |
|---|---|
| `WatchCoordinatorTests` | PASS, 6/6 |
| `FileWatcherRecoveryTests` | PASS, 7/7 |
| Suite Release completa / full Release suite | PASS, 103/103 |
| Cobertura focal de líneas / focused line coverage | 305/329, 92,71 % |
| Política de dominio / domain policy | No aplica: T9 añade puertos/coordinación, no una política ramificada / Not applicable: T9 adds ports/orchestration, not a branching domain policy |
| Build, analizadores, formato, arquitectura, localización y documentación / build, analyzers, format, architecture, localization, and docs | PASS |

Los resultados GREEN y cobertura están en `artifacts/test-results/T9/green/`
y `artifacts/test-results/T9/coverage/`; la verificación transversal está en
`artifacts/test-results/verify-win-x64/`. / GREEN, coverage, and cross-cutting
results are retained under the paths above.

## Eventos, consolidación y límites / Events, coalescing, and limits

- `DebouncedFileWatcher` usa `FileSystemWatcher`, incluye subdirectorios y
  consolida `Created`, `Changed`, `Renamed` y `Deleted` por ruta sin escribir en
  el catálogo. El debounce predeterminado es exactamente 750 ms. /
  `DebouncedFileWatcher` includes subdirectories and coalesces all four event
  kinds by path without writing to the catalog. Its default debounce is exactly
  750 ms.
- La prueba local real detectó el archivo visible en 0,872 s de prueba total,
  dentro del límite de 5 s. / The real local test observed the visible file in
  0.872 s total test time, within the 5 s limit.
- Una tormenta real de 1.000 escrituras, seguida de rename/delete, quedó en un
  lote final de una o dos rutas y completó en 0,407 s. / A real 1,000-write
  storm followed by rename/delete coalesced to one or two final paths and
  completed in 0.407 s.
- Cada lote provoca exactamente un `StartScanCommand(Watcher)`; nunca mutaciones
  directas. Watcher y fallback concurrentes mantienen máximo observado de una
  operación por raíz. / Each batch causes exactly one watcher-triggered scan,
  never direct mutations. Concurrent watcher/fallback work observed a maximum
  of one operation per root.
- La espera sin eventos es cancelable. El scheduler emite `Startup` y permanece
  inactivo hasta un intervalo configurado; manual y recuperación usan el mismo
  coordinador idempotente. / Idle waiting is cancelable. The scheduler emits
  startup and stays idle until a configured interval; manual and recovery use
  the same idempotent coordinator.

## Fallback y almacenamiento no fiable / Fallback and unreliable storage

La prueba UNC simulada usa una raíz marcada `Unc` sobre un directorio temporal,
evitando tráfico real. Tras el primer escaneo se crea un segundo archivo sin
entregar evento y el watcher lanza un error de desconexión simulado. El fallback
`Recovery` descubre el archivo perdido: dos filas, dos sondas acumuladas y cero
pérdida de catálogo. Un segundo fallback sin cambios informa `ProbeCount=0`.
/ The simulated UNC test uses an `Unc` root over a temporary directory, avoiding
real network traffic. After the initial scan, a second file is created without
delivering an event and the watcher raises a simulated disconnect error.
Recovery discovers the missed file: two rows, two cumulative probes, and no
catalog loss. A second unchanged fallback reports `ProbeCount=0`.

Una segunda prueba simula la caída durante enumeración: la única fila permanece
en SQLite pero cambia a `IsAvailable=false`; al reconectar, vuelve a disponible,
conserva el mismo conteo y el escaneo informa `ProbeCount=0`. / A second test
simulates loss during enumeration: the single row remains in SQLite but changes
to `IsAvailable=false`; after reconnection it becomes available again, retains
the same count, and the scan reports `ProbeCount=0`.

El inventario SHA-256 de ambas muestras generadas coincide antes y después de
los escaneos. Producción sólo observa el sistema de archivos; no copia, mueve,
borra ni modifica multimedia. / The SHA-256 inventory of both generated samples
matches before and after scanning. Production only observes the file system; it
does not copy, move, delete, or modify media.

## UI, energía y red / UI, power, and network

La superficie bilingüe `ScanSettingsView` expone vigilancia local e intervalo
de recuperación, y composición registra reloj, watcher, scheduler y coordinador
sin filtrar infraestructura a Presentation o Domain. Sin eventos no hay sondeo:
el watcher espera en canal y el scheduler sólo despierta para inicio o intervalo
configurado. T9 no contiene cliente HTTP; las pruebas locales y UNC simulada no
generan tráfico. / The bilingual settings surface exposes local watching and
the recovery interval, and composition registers the clock, watcher, scheduler,
and coordinator without leaking infrastructure into Presentation or Domain.
With no events there is no polling: the watcher waits on a channel and the
scheduler wakes only for startup or a configured interval. T9 has no HTTP
client; local and simulated-UNC tests generate no network traffic.
