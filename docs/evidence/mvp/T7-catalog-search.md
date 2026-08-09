# T7 — Catálogo, búsqueda FTS5 y vistas / Catalog, FTS5 search, and views

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit de tarea / Task commit: `feat: browse and search the local catalog`
- IDs: `LIB-004=IN_PROGRESS`, `UX-001=IN_PROGRESS`, `UX-004=IN_PROGRESS`

Este informe conserva español e inglés en cada sección. T7 entrega el catálogo
normalizado, consulta por cursor, búsqueda y la superficie de Biblioteca; los
presupuestos definitivos de 10.000 elementos cierran en T10 y el Inicio híbrido
continúa parcial. / This report keeps Spanish and English in every section. T7
delivers the normalized catalog, cursor query, search, and Library surface;
final 10,000-item budgets close in T10 and hybrid Home remains partial.

## Resultado RED / RED result

Las pruebas precedieron a sus implementaciones. Evidencia TRX:
`artifacts/test-results/T7/red/`. / Tests preceded their implementations. TRX
evidence is under the path above.

| Suite | RED demostrado / Proven RED |
|---|---|
| Contrato de catálogo / catalog contract | 0/1: faltaban repositorio, consulta y esquema FTS5 / repository, query, and FTS5 schema absent |
| Contrato de vistas / view contract | 0/1: faltaban Biblioteca y fichas / Library and details absent |
| Comportamiento de catálogo / catalog behavior | 0/8: stubs `NotSupportedException`; cancelación recibió el tipo incorrecto / repository stubs; cancellation got the wrong exception |
| Navegación y snapshots / navigation and snapshots | 0/2: carga de Biblioteca era un stub / Library loading was a stub |
| Integración con shell / shell integration | 0/1: el shell no exponía Biblioteca / shell did not expose Library |

## Resultado GREEN / GREEN result

| Verificación / Check | Resultado / Result |
|---|---|
| `CatalogQueryTests` | PASS, 10/10 |
| `LibraryNavigationTests` | PASS, 4/4 |
| Suite Release completa / full Release suite | PASS, 71/71 |
| Build Release `-warnaserror` y formato / build and format | PASS, 0 warnings, 0 errors |
| Arquitectura, accesibilidad y documentación / architecture, accessibility, docs | PASS |

La cobertura focal se guarda en `artifacts/test-results/T7/coverage/`; la unión
por archivo/línea cubre 349/390 líneas nuevas instrumentables (89,49 %). T7 no
añade una política de dominio ramificada; añade registros de catálogo y
contratos. / Focused coverage is under the path above; the per-file/line union
covers 349/390 new instrumentable lines (89.49%). T7 adds catalog records and
contracts, not a branching domain policy.

## Jerarquía, consulta y privacidad / Hierarchy, query, and privacy

- Película, serie, temporada y episodio se persisten en tablas normalizadas con
  claves foráneas e índices explícitos. / Movie, show, season, and episode are
  persisted in normalized tables with foreign keys and explicit indexes.
- Los filtros `Movie`, `Show`, `Available`, `Progress` y `Personal` son
  componibles; los órdenes `Title`, `Year`, `Added` y `LastPlayed` usan cursor
  opaco con desempate por `TitleId`. / All five filters compose; all four sorts
  use an opaque cursor with `TitleId` tie-breaking.
- La prueba recorre 100 páginas de tres elementos: 300 IDs únicos, cero
  duplicados y cursor final nulo; duración total 1,262 s incluyendo la creación
  del fixture. / The test traverses 100 three-item pages: 300 unique IDs, zero
  duplicates, null final cursor; total 1.262 s including fixture creation.
- FTS5 `unicode61 remove_diacritics 2` encuentra `Amélie` con `amelie`, además
  de título alternativo, reparto y género; cada caso completó en 28–125 ms
  incluyendo migración/fixture. / FTS5 finds the accented title without the
  accent plus alternate title, cast, and genre; each case completed in 28–125
  ms including migration/fixture.
- `catalog_fts` sólo contiene `title_id`, título principal, alternativos,
  reparto y géneros. No existe columna de ruta y la aserción negativa de ruta
  privada pasa. / FTS contains only the approved fields; it has no path column
  and the negative private-path assertion passes.

`EXPLAIN QUERY PLAN` confirma `ix_titles_title` para navegación ordenada y
`VIRTUAL TABLE INDEX` para FTS5. La migración `0004_catalog_fts.sql` usa SHA-256
`96FCD9758EDDE5EEBDE8F4F3A91CA1BD039EAD9CC44D5D2963AC6B4ADDC81B59`.
/ The query plan confirms the named title index and the FTS5 virtual-table
index. The migration uses the SHA-256 above.

## Interfaz y estado / UI and state

Biblioteca usa `VirtualizingStackPanel`, búsqueda, filtro, orden y aplicación
explícita. Película/serie abren fichas mínimas y Volver conserva búsqueda,
filtro, orden, elementos, cursor y ancla de desplazamiento. SQLite nunca se
expone a ViewModels: éstos dependen sólo de `ICatalogQueryService`. / Library
uses a virtualizing panel, search, filter, sort, and explicit apply. Movie/show
open minimal details, and Back preserves query, items, cursor, and scroll
anchor. ViewModels depend only on the query contract and never see SQLite.

Capturas headless revisadas: / Reviewed headless captures:

- `artifacts/ui-captures/T7/library-es-ES.png` — 9.734 bytes.
- `artifacts/ui-captures/T7/library-en-US.png` — 8.650 bytes.

Todo texto visible nuevo procede de recursos con paridad ES/EN. / All new
visible text comes from parity-checked ES/EN resources.
