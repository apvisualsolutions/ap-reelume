# T10 — Rendimiento con 10.000 archivos / 10,000-file performance

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit de tarea / Task commit: `perf: meet 10000 item library budgets`
- Commit base medido / Measured base commit: `f434c5ca3463f01461fcf660da20ab444426d5ff`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, SQLite WAL/FTS5, Avalonia headless Skia
- IDs: `LIB-002=VERIFIED`, `LIB-004=VERIFIED`

Este informe conserva español e inglés en cada sección. T10 fija una línea base
versionada, mide cinco ejecuciones limpias y cierra los presupuestos de I1 sin
relajar ningún umbral. / This report keeps Spanish and English in every section.
T10 versions the baseline, measures five clean runs, and closes the I1 budgets
without relaxing any threshold.

## Resultado RED / RED result

Las pruebas se escribieron antes del comportamiento. El RED de contrato fue
0/3 porque faltaban el fixture, las medidas y la sonda de frames; su TRX está en
`artifacts/test-results/T10/red/T10-contract-red.trx`. / Tests preceded the
behavior. The contract RED was 0/3 because the fixture, measurements, and frame
probe were absent; its TRX is retained at the path above.

La primera ejecución funcional no aislada incumplió búsqueda concurrente con
`p95=159,7476 ms`: las tres clases construían simultáneamente su propia base de
10.000 elementos. Tras serializar únicamente el arnés, cinco ejecuciones antes
de optimizar volvieron a fallar en las repeticiones 4 y 5 con `166,3125 ms` y
`162,7998 ms`. El perfil atribuyó el coste repetido a
`PRAGMA journal_mode=WAL` en cada una de las más de 79 conexiones del escaneo.
/ The first unisolated functional run missed concurrent search at 159.7476 ms
because three fixtures were built at once. After serializing only the harness,
pre-optimization repetitions 4 and 5 still failed at 166.3125 ms and
162.7998 ms. Profiling traced the repeated cost to setting WAL journal mode on
each of more than 79 scan connections.

La integración escaneo→búsqueda tuvo además un RED de contrato 0/1 y fallos de
compilación de comportamiento: no existían el estado `Unidentified` ni la
proyección buscable. / The scan-to-search integration also had a 0/1 contract
RED and behavior compilation failures: neither the `Unidentified` state nor a
searchable projection existed.

## Resultado GREEN / GREEN result

| Verificación / Check | Resultado / Result |
|---|---|
| Presupuestos, cinco repeticiones / budgets, five repetitions | PASS, 40/40 |
| Integración SQLite completa / full SQLite integration | PASS, 47/47 |
| Suite Release con cobertura / Release suite with coverage | PASS, 113/113 |
| Cobertura de líneas nuevas instrumentadas / instrumented new-line coverage | 78/80, 97,50 % |
| BenchmarkDotNet búsqueda / search | media / mean 3,207 ms; mediana / median 2,893 ms; 34,96 KiB/op |
| Analizadores, arquitectura y documentación / analyzers, architecture, and docs | PASS |

La cobertura combina los informes Cobertura de la solución y toma el máximo de
hits por línea añadida instrumentable en Domain/Infrastructure; migraciones SQL
y arnés no se cuentan como líneas de producción instrumentables. / Coverage
combines the solution's Cobertura reports and takes the maximum hits for each
instrumentable added Domain/Infrastructure line; SQL migrations and harness
code are not counted as instrumentable production lines.

## Hardware y corpus / Hardware and corpus

- Windows 11 Pro `10.0.26200`, x64; Micro-Star International `MS-7D91`. /
  Windows 11 Pro on the stated x64 system.
- Intel Core i7-14700K: 20 núcleos físicos y 28 procesadores lógicos; RAM
  102.841.982.976 bytes. / 20 physical cores, 28 logical processors, and the
  stated RAM.
- NVIDIA GeForce RTX 5070, driver `32.0.16.1062`; volumen de prueba `E:`. /
  The stated GPU/driver and test volume.

El fixture contiene exactamente 10.000 `MediaFile`: 6.000 episodios, 4.000
películas, 1.000 no disponibles, 500 copias duplicadas y títulos Unicode
`Amélie`, `Ñandú` y `東京`. El warm-up precede a las muestras y queda excluido
de mediana/p95. / The fixture contains exactly 10,000 media files: 6,000
episodes, 4,000 movies, 1,000 unavailable items, 500 duplicate copies, and the
listed Unicode titles. Warm-up precedes and is excluded from measured samples.

## Cinco ejecuciones limpias / Five clean runs

Fuente / Source: `artifacts/performance/T10/measured/summary.json`.

| Métrica / Metric | Mediana / Median | p95 | Presupuesto / Budget | Resultado / Result |
|---|---:|---:|---:|---|
| Ventana útil / `useful-window` | 3,9008 ms | 6,5102 ms | <3000 ms | PASS |
| Primera página / `first-search-page` | 3,5665 ms | 4,6083 ms | <150 ms | PASS |
| Búsqueda + escaneo / `concurrent-search` | 9,2854 ms | 32,7533 ms | <150 ms | PASS |
| Frame con scroll / `frame-p95` | 0,6143 ms | 0,9858 ms | <16,7 ms | PASS |
| Bloqueo de UI / `scan-ui-block` | 0,0043 ms | 9,6947 ms | <50 ms | PASS |
| Escaneo sin cambios / `unchanged-probes` | 0 | 0 | =0 | PASS |

El frame usa `LibraryView` real a 1024×720, Skia headless, scroll de un
`ScrollViewer` con `VirtualizingStackPanel` y 60 capturas. La prueba concurrente
ejecuta FTS5 mientras recorre 10.000 rutas sin cambios. / Frame timing uses the
real 1024×720 Library view, headless Skia, actual scrolling over a virtualizing
panel, and 60 captures. The concurrent test runs FTS5 while an unchanged scan
walks all 10,000 paths.

## Perfil y cambios mínimos / Profile and minimum changes

`SqliteConnectionFactory` configura WAL una vez por instancia bajo un lock y
mantiene `foreign_keys`, `busy_timeout` y `synchronous=FULL` por conexión. La
consulta, paginación, índice FTS5 y virtualización existentes ya cumplían; por
eso no se relajaron umbrales ni se cambió `LibraryView.axaml`. / The connection
factory now configures WAL once per instance under a lock while retaining the
per-connection safety pragmas. Existing query paging, FTS5 index, and view
virtualization already met budget, so no threshold or Library view change was
needed.

El perfil de C2 reveló una necesidad funcional independiente del rendimiento:
el lote de `media_files` no alimentaba la búsqueda. La migración v6
`scanned_catalog_projection` crea una proyección FTS5 atómica con el lote. Sólo
indexa el nombre sin extensión, nunca la ruta completa, y usa
`CatalogTitleKind.Unidentified`; la clasificación sigue perteneciendo a I2. /
C2 profiling exposed a separate functional need: scanned media did not feed
search. Migration v6 adds an atomic FTS5 projection to the media batch. It
indexes only the extension-free display name, never the full path, and uses the
unidentified state; classification remains in I2.

## Integridad, archivos y red / Integrity, files, and network

La base termina en esquema v6, `PRAGMA integrity_check=ok`, WAL activo y copia
previa válida por migración. El corpus usa rutas sintéticas: las pruebas sólo
escriben SQLite y artefactos ignorados, nunca copian, mueven, borran ni modifican
multimedia. / The database ends at schema v6 with a successful integrity check,
WAL, and a valid pre-migration backup. The corpus uses synthetic paths; tests
write only SQLite and ignored artifacts and never copy, move, delete, or modify
media.

T10 no añade cliente HTTP. Las rutas UNC del corpus son simuladas y no se abre
ninguna conexión de red; la comprobación de tráfico de C2 se ejecuta de nuevo
sobre la demo real. / T10 adds no HTTP client. Corpus UNC paths are simulated
and open no network connection; C2 repeats traffic observation around the real
demo.

## Repetición final C2 / Final C2 repetition

Cinco ejecuciones adicionales completaron 40/40 comprobaciones. Sus p95 fueron
5,8076 ms para ventana útil, 4,4349 ms para primera búsqueda, 23,8466 ms para
búsqueda concurrente, 0,9010 ms por frame, 9,9391 ms de bloqueo UI y cero
sondeos sin cambios. Todos permanecen dentro de los presupuestos sin modificar
umbrales. / Five additional runs completed all 40 checks. Their p95 values were
5.8076 ms for useful window, 4.4349 ms for first search, 23.8466 ms for
concurrent search, 0.9010 ms per frame, 9.9391 ms for UI blocking, and zero
unchanged probes. Every result remains within budget without changing a
threshold. Full evidence is in [`C2-library-gate.md`](C2-library-gate.md).
