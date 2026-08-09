# T14 — Bandeja de revisión y corrección / Review inbox and correction

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `42a37a6`
- Commit de tarea / Task commit: `feat: review and correct uncertain matches`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, Avalonia Headless
- IDs: `LIB-006=VERIFIED`, `LIB-007=VERIFIED`, `A11Y-001=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`ReviewWorkflowTests`, `ReviewInboxTests` y `ReviewInboxAutomationTests` se
escribieron antes del código de producción. RED falló por la ausencia de los
casos de uso, contratos de revisión, métodos persistentes y vistas; se conserva
en `artifacts/test-results/T14/red/T14-red.log`. / The three plan-named test
files were written before production code. RED failed because the review use
cases, contracts, persistent methods, and views did not exist; output is
retained at the path above.

GREEN ejecuta 3/3 pruebas de aplicación, 3/3 de repositorio SQLite, 3/3 de UI
headless y 2/2 de accesibilidad, sin fallos. TRX y Cobertura se conservan bajo
`artifacts/test-results/T14/green/`. / GREEN executes 3/3 application tests,
3/3 SQLite repository tests, 3/3 headless UI tests, and 2/2 accessibility
tests, with zero failures. TRX and Cobertura are retained under the path above.

La puerta transversal Release/win-x64 terminó con 0 advertencias, 0 errores,
181 pruebas ejecutadas y 0 fallos. La verificación documental aprobó 22
Markdown, 2 archivos localizados, 53 IDs y 46 IDs MVP. / The Release/win-x64
cross-cutting gate completed with 0 warnings, 0 errors, 181 executed tests, and
0 failures. Documentation passed 22 Markdown files, 2 localized files, 53 IDs,
and 46 MVP IDs.

## Flujo y persistencia / Workflow and persistence

- La bandeja excluye `Automatic`, `Accepted` y `Rejected`; prioriza `Pending`
  y después `Suggested`, con puntuación y clave estable, y pagina de 25 en 25.
  / The inbox excludes automatic and resolved states, prioritizes pending over
  suggested with score/stable-key ordering, and pages 25 at a time.
- Los límites exactos se prueban con 0,5999, 0,60, 0,8999 y 0,90; solo 0,90
  queda automático y fuera de revisión. / Exact boundary probes demonstrate
  that only 0.90 is automatic and excluded from review.
- Aceptar y rechazar escriben `revision + 1`, bloquean la decisión y publican
  `ReviewInboxChanged` únicamente tras aplicar el cambio. / Accept and reject
  increment revision, lock the decision, and publish the typed event only
  after a successful write.
- Una revisión simultánea con revisión esperada obsoleta devuelve `Conflict`,
  conserva la elección ya guardada y no publica un segundo evento. / A stale
  optimistic revision returns conflict, preserves the stored choice, and does
  not publish a second event.
- El contrato SQLite real demuestra que una aceptación manual sobrevive a un
  rescaneo con otra puntuación y permanece fuera de la bandeja. / The real
  SQLite contract proves a manual acceptance survives rescoring and remains
  outside the inbox.

## UI, teclado y automatización / UI, keyboard, and automation

La UI headless se ejecutó en español e inglés. Cada tarjeta muestra estado
textual, porcentaje y explicaciones; el significado nunca depende solo del
color. La búsqueda manual, aceptar, rechazar y cargar más son acciones
separadas. / The headless UI ran in Spanish and English. Every card exposes
text state, percentage, and explanations; meaning never relies on color alone.
Manual search, accept, reject, and load-more are distinct actions.

Se probaron Tab, flechas arriba/abajo, Enter sobre la lista para aceptar, Enter
sobre el botón para rechazar y Escape para limpiar selección. Enter está
acotado a la lista para no competir con los botones. Controles y listado tienen
nombres de automatización localizados y las tarjetas exponen `HelpText`. / Tab,
arrow navigation, list Enter-to-accept, button Enter-to-reject, and Escape were
tested. Enter is scoped to the list so it cannot conflict with buttons.
Controls carry localized automation names and cards expose help text.

| Artefacto / Artifact | Ruta / Path |
|---|---|
| Captura ES / ES screenshot | `artifacts/ui-captures/T14/review-es-ES.png` |
| Captura EN / EN screenshot | `artifacts/ui-captures/T14/review-en-US.png` |
| Árbol UIA ES / ES UIA tree | `artifacts/ui-captures/T14/review-uia-es-ES.txt` |
| Árbol UIA EN / EN UIA tree | `artifacts/ui-captures/T14/review-uia-en-US.txt` |

Cobertura focal del código nuevo: `GetReviewInbox` y `RejectMatch` 100 % de
líneas, `ResolveMatch` 84,61 %, `MatchCandidateRepository` 100 % y
`ReviewInboxViewModel` 90 %. / Focused new-code line coverage reports the same
percentages for the named components.

`LIB-006` y `LIB-007` quedan `VERIFIED`. `A11Y-001` permanece `IN_PROGRESS`
porque su auditoría global con Narrator/alto contraste se cierra en tareas
posteriores. / The two library IDs are verified; the accessibility ID remains
in progress until its later global audit.
