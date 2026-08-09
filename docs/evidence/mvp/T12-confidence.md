# T12 — Confianza explicable y candidatos persistentes / Explainable confidence and persistent candidates

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `24cdad4`
- Commit de tarea / Task commit: `feat: score and classify explainable media matches`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302
- IDs: `LIB-006=IMPLEMENTED`, `LIB-007=IMPLEMENTED`, `LIB-008=IMPLEMENTED`

## RED y GREEN / RED and GREEN

Las pruebas de dominio, aplicación y migración se escribieron antes del código
de producción. RED falló porque aún no existían `CandidateScorer`,
`ConfidencePolicy`, la política conservadora de duplicados, el caso de uso de
identificación ni la tabla `match_candidates`; la salida se conserva en
`artifacts/test-results/T12/red/T12-red.log`. / Domain, application, and
migration tests were written before production code. RED failed because the
scorer, confidence policy, conservative duplicate policy, identification use
case, and candidate table did not exist yet; output is retained at the path
above.

La verificación focal GREEN cubre 25/25 pruebas de política, 2/2 del caso de
uso y 8/8 del contrato SQLite y migraciones, todas sin fallos. Los TRX y la
cobertura Cobertura están bajo `artifacts/test-results/T12/green/`. / Focused
GREEN verification covers 25/25 policy tests, 2/2 use-case tests, and 8/8
SQLite repository and migration tests, all with zero failures. TRX and
Cobertura files are retained under the path above.

La puerta transversal `pwsh ./eng/verify.ps1 -Configuration Release -Runtime
win-x64` terminó GREEN: compilación con 0 advertencias y 0 errores, 159 pruebas
ejecutadas y 0 fallos. La verificación documental aprobó 20 Markdown, 2
archivos localizados, 53 IDs y 46 IDs MVP. Los proyectos Media y Packaging aún
no contienen pruebas en I2, tal como informa el runner. / The cross-cutting
gate completed GREEN: build with 0 warnings and 0 errors, 159 executed tests,
and 0 failures. Documentation verification passed 20 Markdown files, 2
localized files, 53 IDs, and 46 MVP IDs. Media and Packaging test projects do
not contain tests yet in I2, as reported by the runner.

## Política v1 / V1 policy

| Señal / Signal | Peso / Weight |
|---|---:|
| Título / Title | 0,50 |
| Episodio / Episode | 0,20 |
| Temporada / Season | 0,15 |
| Año / Year | 0,10 |
| Duración / Duration | 0,05 |

Solo se normalizan las señales aplicables. Un conflicto película/episodio
rechaza el candidato; una contradicción de temporada o episodio limita la
confianza a 0,59; un nombre compacto ambiguo la limita a 0,89. Los códigos de
señal y explicación son localizables. / Only applicable signals are
renormalized. A movie/episode kind conflict rejects the candidate; a season or
episode contradiction caps confidence at 0.59; an ambiguous compact name caps
it at 0.89. Signal and explanation codes are localizable.

Los límites se prueban exactamente: `<0,60` pendiente, `0,60–0,8999` sugerido
y `≥0,90` automático. Los empates se ordenan por clave estable y repetir la
identificación reemplaza el conjunto de forma idempotente. Una coincidencia
local automática evita consultar la fuente remota. / Exact boundaries are
tested: `<0.60` pending, `0.60–0.8999` suggested, and `≥0.90` automatic. Ties
are ordered by stable key and repeated identification replaces the set
idempotently. An automatic local match avoids the remote source.

## Persistencia, duplicados y cobertura / Persistence, duplicates, and coverage

La migración secuencial `0007_match_candidates.sql` persiste candidatos,
modelo de puntuación, estado, señales, explicaciones, revisión y bloqueo. El
contrato real SQLite demuestra reemplazo atómico, lectura ordenada y rollback
ante un candidato perteneciente a otro archivo. / Sequential migration
`0007_match_candidates.sql` persists candidates, scoring model, state,
signals, explanations, revision, and lock state. The real SQLite contract
demonstrates atomic replacement, ordered reads, and rollback when a candidate
belongs to another media file.

Cobertura focal del código nuevo: `CandidateScorer` 100 % líneas / 93,18 %
ramas; `ConfidencePolicy` 100/100 %; `DuplicateGroupingPolicy` 100/100 %;
`MatchCandidateRepository` 100 % de líneas. / Focused coverage for new code:
`CandidateScorer` 100% lines / 93.18% branches; `ConfidencePolicy` 100/100%;
`DuplicateGroupingPolicy` 100/100%; `MatchCandidateRepository` 100% lines.

La política solo agrupa coincidencias exactas de contenido y episodio o
película. Mantiene visibles todos los `MediaFileId`, no expone acciones de
borrado u ocultación y remite cualquier discrepancia a confirmación. No se
renombra, mueve, copia ni elimina ningún archivo y T12 no realiza tráfico de
red. / The policy only groups exact content and episode or movie matches. It
keeps every `MediaFileId` visible, exposes no delete or hide action, and sends
any discrepancy to confirmation. No file is renamed, moved, copied, or deleted,
and T12 performs no network traffic.

Los tres IDs quedan `IMPLEMENTED`, no `VERIFIED`: sus criterios completos se
cierran en T13–T15 y C3. / All three IDs remain `IMPLEMENTED`, not `VERIFIED`:
their full criteria close in T13–T15 and C3.
