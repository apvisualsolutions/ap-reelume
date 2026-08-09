# C2 — Puerta de biblioteca I1 / I1 library gate

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Alcance / Scope: T5–T10 exclusivamente / T5–T10 only
- Plataforma / Platform: Windows 11 x64, .NET SDK 10.0.302

Este informe reúne la evidencia final bilingüe de I1. No inicia I2 ni cambia
las decisiones de producto aprobadas. / This report collects I1's final
bilingual evidence. It does not start I2 or alter approved product decisions.

## Biblioteca real y búsqueda / Real library and search

La demo ejecutó los adaptadores de producción sobre la biblioteca real de
trabajo, sin copiarla a un fixture. El inventario acotado antes/después incluyó
tamaño, fecha de modificación y SHA-256 de tres muestras de 64 KiB por archivo.
/ The demo ran the production adapters against the real working library rather
than copying it into a fixture. Its bounded before/after inventory included
size, last-write time, and SHA-256 over three 64 KiB samples per file.

El recuento, el volumen y los términos de búsqueda reales quedan **redactados**: son inventario de
una biblioteca personal y este repositorio se publica. `N` es el número de archivos de esa biblioteca
y se usa igual en todas las filas, de modo que la coherencia que la puerta demuestra —todo lo
enumerado se sondea, todo lo sondeado se indexa— sigue siendo comprobable. / File count, byte volume,
and real search terms are redacted; `N` stands for the same number throughout, so the consistency the
gate proves remains checkable.

| Medida / Measurement | Resultado / Result |
|---|---:|
| Archivos / files | `N`, en dos contenedores / across two containers |
| Bytes | redactado / redacted |
| Primer escaneo / first scan | `N` enumerados, `N` sondeos, 0 errores / `N` enumerated, `N` probes, 0 errors |
| Tiempo del primer escaneo / first-scan time | 27.998,4153 ms |
| Segundo escaneo / second scan | `N` sin cambios, `ProbeCount=0` / `N` unchanged, zero probes |
| Búsqueda de un término presente en dos títulos / a term present in two titles | 2 resultados / results |
| Primera página de un término frecuente / first page of a frequent term | 50 resultados / results |
| SQLite | esquema / schema v6, `integrity_check=ok`, WAL, `N` filas FTS / FTS rows |
| Inventario antes/después / before-after inventory | idéntico / identical |

La sonda LibVLC sólo usa análisis local. C2 descubrió primero un
`0xC0000005` reproducible al destruir inmediatamente sesiones nativas tras
varios MKV. La prueba RED y las ejecuciones reales conservaron el fallo; la
corrección mínima serializa sondas y libera cada sesión tras un periodo de
quiescencia de un segundo en segundo plano. La regresión generada y lotes reales
de diez archivos y de la biblioteca completa terminan sin fallo y liberan el lote final sin bloquear el
escaneo. / The LibVLC probe uses local parsing only. C2 first exposed a
reproducible `0xC0000005` when native sessions were destroyed immediately
after consecutive MKVs. RED evidence and real runs retained that failure; the
minimum fix serializes probes and releases each session in the background after
a one-second quiescence period. The generated regression plus real batches of
ten files and the whole library complete successfully and drain the final batch without
blocking scanning.

## Matriz funcional / Functional matrix

| Caso / Case | Evidencia GREEN / GREEN evidence |
|---|---|
| Local, USB simulada y UNC / local, simulated USB, and UNC | validación independiente, `subst R:` y UNC local; inventario SHA-256 idéntico / independent validation, substituted drive, local UNC, identical SHA-256 inventory |
| Cancelación y reanudación / cancellation and resume | checkpoint tras 5/10; reanuda los cinco restantes y lo limpia / checkpoint after 5/10; resumes the remaining five and clears it |
| Desconexión y reconexión / disconnect and reconnect | catálogo conservado, misma entidad disponible al volver, cero duplicados / catalog retained, same entity available on return, zero duplicates |
| Colisión de firma / fingerprint collision | no fusiona; conserva ruta anterior hasta confirmación manual / does not merge; retains old path until manual confirmation |
| Evento UNC perdido / lost UNC event | fallback recupera el archivo; pasada sin cambios con cero sondeos / fallback recovers the file; unchanged pass has zero probes |
| Búsqueda y navegación / search and navigation | Unicode/diacríticos, ruta privada excluida, 100 páginas sin duplicados y estado de retorno / Unicode/diacritics, private-path exclusion, 100 duplicate-free pages, and restored navigation state |
| Suite focal I1 / focused I1 suite | 66/66: dominio 12, aplicación 11, integración 39, UI 4 / domain 12, application 11, integration 39, UI 4 |

Los TRX focales están en `artifacts/test-results/C2/i1-gate/`; la demo real y
su inventario están en
`artifacts/c2-demo/full-library-20260801175312777/full-library-report.json`. /
Focused TRX files and the real demo report are retained at the paths above.

## Corpus de 10.000 y presupuestos / 10,000-item corpus and budgets

El corpus determinista contiene 6.000 episodios, 4.000 películas, 1.000 no
disponibles, 500 copias duplicadas y texto Unicode. Cinco ejecuciones C2
completaron 40/40 pruebas. / The deterministic corpus contains 6,000 episodes,
4,000 movies, 1,000 unavailable items, 500 duplicate copies, and Unicode text.
Five C2 runs completed all 40 checks.

| Métrica / Metric | Mediana / Median | p95 | Presupuesto / Budget |
|---|---:|---:|---:|
| Ventana útil / `useful-window` | 4,1367 ms | 5,8076 ms | <3.000 ms |
| Primera búsqueda / `first-search-page` | 3,2381 ms | 4,4349 ms | <150 ms |
| Búsqueda + escaneo / `concurrent-search` | 14,6045 ms | 23,8466 ms | <150 ms |
| Frame / `frame-p95` | 0,6019 ms | 0,9010 ms | <16,7 ms |
| Bloqueo UI / `scan-ui-block` | 0,0693 ms | 9,9391 ms | <50 ms |
| Sondas sin cambios / `unchanged-probes` | 0 | 0 | =0 |

Fuente / Source: `artifacts/performance/C2/measured/summary.json`.

## Archivos, red e identidad interna / Files, network, and internal identity

La demo observó el proceso cada 100 ms: 164 muestras, cero sockets TCP y cero
endpoints UDP. El inventario real resultó idéntico antes/después. El análisis
estático de `src/` encuentra cero APIs de red y ninguna mutación multimedia;
las dos mutaciones de archivo son el reemplazo atómico del JSON de ajustes, no
medios. / The demo observed the process every 100 ms: 164 samples, zero TCP
sockets, and zero UDP endpoints. The real inventory was identical before and
after. Static analysis of `src/` finds zero network APIs and no media mutation;
its two file mutations implement atomic settings-JSON replacement, not media
handling.

Todos los namespaces y ensamblados de producción usan
`ApSolutions.LocalMedia.*`; paquete, URI y ruta persistente permanecen bajo la
identidad estable aprobada, independiente de la marca pública. / Every
production namespace and assembly uses `ApSolutions.LocalMedia.*`; package,
URI, and persistent path retain the approved stable identity independently of
the public brand.

## Resultado de puerta / Gate result

T5–T10 y los IDs `LIB-001`, `LIB-002`, `LIB-003`, `LIB-004`, `LIB-009` y
`LIB-010` quedan respaldados por la evidencia enlazada. I1 se detiene en C2 para
revisión de Product + QA; I2 no ha comenzado. / T5–T10 and the listed IDs are
backed by the linked evidence. I1 stops at C2 for Product + QA review; I2 has
not started.
