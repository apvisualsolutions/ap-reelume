# T13 — Adaptador TMDB, caché e idioma / TMDB adapter, cache, and language

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `7e04647`
- Commit de tarea / Task commit: `feat: enrich metadata through cached TMDB adapter`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302
- IDs: `LIB-006=IMPLEMENTED`, `LIB-011=IMPLEMENTED`, `PRI-001=IN_PROGRESS`

## RED y GREEN / RED and GREEN

Las tres pruebas nombradas por el plan se escribieron antes del código de
producción. RED falló únicamente por la ausencia de los contratos
`ApSolutions.LocalMedia.Domain.Metadata`, el adaptador TMDB, la caché SQLite y
la superficie de Créditos; se conserva en
`artifacts/test-results/T13/red/T13-red.log`. / The three plan-named test files
were written before production code. RED failed only because the metadata
contracts, TMDB adapter, SQLite cache, and Credits surface did not exist; the
output is retained at the path above.

GREEN conserva 4/4 pruebas de dominio y 15/15 pruebas de contrato, caché y
migraciones, todas sin fallos, junto con TRX y Cobertura bajo
`artifacts/test-results/T13/green/`. / GREEN retains 4/4 domain tests and 15/15
contract, cache, and migration tests, all passing, with TRX and Cobertura under
the path above.

La puerta transversal Release/win-x64 terminó con 0 advertencias, 0 errores,
172 pruebas ejecutadas y 0 fallos. La documentación aprobó 21 Markdown, 2
archivos localizados, 53 IDs y 46 IDs MVP. / The Release/win-x64 cross-cutting
gate completed with 0 warnings, 0 errors, 172 executed tests, and 0 failures.
Documentation passed 21 Markdown files, 2 localized files, 53 IDs, and 46 MVP
IDs.

## Matriz del proveedor / Provider matrix

| Caso / Case | Resultado demostrado / Demonstrated result |
|---|---|
| Español y fallback / Spanish and fallback | Se consulta `es-ES`; si no hay resultados se usa el idioma alternativo configurado. / `es-ES` is queried first; configured fallback is used when it has no results. |
| Detalle / Details | Película, título original, resumen, año, géneros y rutas de arte se normalizan. / Movie title, original title, overview, year, genres, and artwork paths are normalized. |
| Caché fresca / Fresh cache | La segunda consulta produce cero peticiones HTTP. / The second query produces zero HTTP requests. |
| Caché caducada / Stale cache | Envía `If-None-Match`; `304` renueva el TTL sin perder el cuerpo. / Sends `If-None-Match`; `304` refreshes TTL without losing the body. |
| Sin red / Offline | Una entrada caducada sigue siendo utilizable cuando HTTP falla. / A stale entry remains usable when HTTP fails. |
| `429` | Respeta `Retry-After` (7 s en el contrato falso). / Honors `Retry-After` (7 s in the fake contract). |
| `5xx` | Backoff exponencial inicial de 2 s y espera cancelable. / Initial 2-second exponential backoff with cancelable wait. |
| Sin token / No token | Modo local, resultado vacío y cero peticiones. / Local mode, empty result, and zero requests. |

`TmdbOptions` solo lee el token de `AP_LOCALMEDIA_TMDB_TOKEN` o de un recurso
CI inyectado; `ToString()` informa únicamente `configured/absent`. No se
introduce proxy. / `TmdbOptions` only reads the token from the named environment
variable or an injected CI resource; `ToString()` only reports
`configured/absent`. No proxy is introduced.

La migración secuencial `0008_metadata_cache.sql` guarda proveedor, clave,
idioma, versión, cuerpo, ETag, fecha y expiración con reemplazo transaccional.
/ Sequential migration `0008_metadata_cache.sql` stores provider, key,
language, version, payload, ETag, timestamps, and expiry with transactional
replacement.

## Privacidad, atribución y cobertura / Privacy, attribution, and coverage

El contrato de búsqueda solo admite título, año y tipo; no admite ruta ni
nombre de archivo. Las URI capturadas contienen solo endpoint, consulta,
idioma y año, o el ID de proveedor en detalle. El barrido de fuentes y
artefactos encontró `TOKEN_MATCH_FILES=0`; los cuatro marcadores de ruta/query
privada produjeron cero coincidencias en artefactos T13. / The search contract
only accepts title, year, and content kind; it cannot accept a path or file
name. Captured URIs contain only endpoint, query, language, and year, or the
provider ID for details. Source/artifact scanning found zero token matches and
zero occurrences for all four private path/query markers in T13 artifacts.

Créditos incluye la declaración oficial en español e inglés y nombra TMDB. La
formulación se verificó contra la [FAQ oficial de TMDB](https://developer.themoviedb.org/docs/faq).
/ Credits contains the official notice in Spanish and English and identifies
TMDB. Wording was checked against the official TMDB FAQ linked above.

Cobertura focal: `MetadataMergePolicy` 100 % líneas / 91,66 % ramas;
`TmdbMetadataProvider` 97,64 % líneas; `SqliteMetadataCache` 100 % líneas;
`TmdbOptions` 95 % líneas; `TmdbRateLimiter` ≥83,33 % líneas. / Focused coverage
reports the same percentages for the named new-code components.

T13 no crea telemetría y solo permite HTTP al solicitar metadatos con token.
`PRI-001` continúa parcial hasta la auditoría global; `LIB-006` espera la
integración de revisión T14 y `LIB-011` espera el editor T16 antes de poder
quedar `VERIFIED`. / T13 creates no telemetry and only permits HTTP for
token-authorized metadata requests. `PRI-001` remains partial until the global
audit; the library IDs wait for their stated downstream integrations before
becoming `VERIFIED`.
