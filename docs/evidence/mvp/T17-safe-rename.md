# T17 — Renombrado seguro con previsualización, auditoría y deshacer / Safe rename preview, audit, and undo

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `04df557`
- Commit de tarea / Task commit: `feat: preview audit and undo safe media renames`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, SQLite WAL
- IDs: `LIB-012=VERIFIED`, `PRI-001=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`RenamePolicyTests` y `RenameTransactionTests` se escribieron antes del código
de producción. RED falló por la ausencia de política, modelos, casos de uso,
renombrador y ViewModel; se conserva en
`artifacts/test-results/T17/red/T17-red.log`. / Both plan-named test files were
written before production code. RED failed because the policy, models, use
cases, renamer, and ViewModel did not exist; output is retained at the path
above.

GREEN conserva 4/4 pruebas de dominio y 6/6 de integración con TRX, logs y
Cobertura bajo `artifacts/test-results/T17/green/`. La cobertura combinada de
código nuevo es 83,91 % de líneas; `RenamePolicy` alcanza 100 % de líneas y
100 % de ramas, `SafeFileRenamer` 82,13 % de líneas y
`RenamePreviewViewModel` 83,75 %. / GREEN retains 4/4 domain and 6/6
integration tests with TRX, logs, and Cobertura under the path above. Combined
new-code line coverage is 83.91%; the domain policy has 100% line and branch
coverage, the filesystem adapter 82.13% line coverage, and the ViewModel
83.75%.

La puerta transversal `eng/verify.ps1 -Configuration Release -Runtime
win-x64` compiló con 0 advertencias/errores y ejecutó 211 pruebas con 0 fallos.
La documentación aprobó 25 Markdown, 2 recursos localizados, 53 IDs y 46 IDs
MVP. Los análisis hallaron 0 dependencias vulnerables/en desuso, 0 secretos y
0 primitivas de red en el código T17. / The Release/win-x64 gate built with
zero warnings/errors and ran 211 tests with zero failures. Documentation
passed 25 Markdown files, 2 localized resources, 53 IDs, and 46 MVP IDs.
Scans found no vulnerable/deprecated dependencies, secrets, or network
primitives in T17 code.

## Previsualización y política / Preview and policy

- Los caracteres Windows inválidos/control se sustituyen de forma
  determinista, los nombres reservados (`CON`, `PRN`, `AUX`, `NUL`,
  `COM1–9`, `LPT1–9`) reciben prefijo y nombres/rutas excesivos bloquean el
  plan. / Invalid/control Windows characters are deterministically replaced,
  reserved device names are prefixed, and overlong names/paths block the plan.
- Orígenes o destinos fuera de la raíz, movimientos entre carpetas, operación
  sin cambio y destinos duplicados con cualquier capitalización producen
  conflictos visibles. Un cambio sólo de mayúsculas usa un intermedio seguro.
  / Outside-root paths, folder moves, no-op requests, and case-insensitive
  duplicate destinations become visible conflicts. Case-only renames use a
  safe same-directory intermediate.
- `PreviewRename` sólo calcula `RenamePlan`; no recibe sistema de archivos. El
  ViewModel enumera cada origen→destino y deshabilita ejecutar hasta que la
  persona marca la confirmación explícita. / PreviewRename only computes a
  plan and has no filesystem dependency. The ViewModel lists every
  source→destination and disables execution until explicit confirmation.

## Escenarios transaccionales / Transaction scenarios

| Escenario / Scenario | Resultado / Result | Auditoría y recuperación / Audit and recovery |
|---|---|---|
| Dos archivos locales confirmados / Two confirmed local files | `Succeeded`; sólo cambian los nombres y el inventario de contenidos permanece idéntico / only names change and content inventory remains identical | Dos filas `Execute/Completed`; deshacer en orden inverso crea dos `Undo/Completed` y restaura ambos nombres / two execute rows; reverse undo adds two rows and restores both names |
| Destino creado después de previsualizar / Destination created after preview | `BlockedByConflict`; cero movimientos, ambos contenidos exactos / zero moves, exact contents retained | Cero filas de auditoría porque la prevalidación completa termina antes del primer I/O / zero audit rows because full prevalidation precedes I/O |
| UNC simulado falla en la segunda operación / Simulated UNC fails on operation two | `PartiallyCompleted`; primer destino presente, segundo origen intacto, inventario idéntico / first destination present, second source intact, same inventory | Estados `Completed, Failed`, `CanUndo=true`; recuperación revierte sólo el éxito confirmado y conserva ambos archivos / recoverable log and guided undo of only the completed item |
| Estado cambiado antes de deshacer / State changes before undo | `UnsafeToUndo`; cero operaciones / zero operations | Se conservan origen conflictivo y destino; requiere resolución guiada / conflicting source and destination remain for guided resolution |

Cada fila se inserta como `Started` antes de tocar el archivo y se reconcilia a
`Completed` o `Failed` inmediatamente después. El registro usa la migración
monótona `0009_rename_log.sql`; las versiones 7 y 8 ya pertenecen a
coincidencias y caché TMDB aprobadas. / Every row is inserted as Started before
file I/O and immediately reconciled to Completed or Failed. The audit uses the
next monotonic migration, version 9, because approved migrations already own
versions 7 and 8.

## Consentimiento, límites y privacidad / Consent, boundaries, and privacy

`ExecuteRename` y `UndoRename` devuelven `NotConfirmed` sin invocar el
adaptador cuando falta consentimiento. Todo el lote se prevalida con rutas
normalizadas confinadas a la misma carpeta; no se mueven carpetas ni se
construyen comandos de shell. La ejecución es secuencial y no promete
atomicidad en UNC: devuelve un estado parcial recuperable. / The application
use cases return NotConfirmed without invoking the adapter when consent is
missing. The entire batch is prevalidated as normalized, root-confined,
same-directory paths; no folders move and no shell command is built. UNC work
is deliberately sequential and reports a recoverable partial result rather
than promising atomicity.

T17 no contiene cliente de red ni telemetría; rutas y auditoría permanecen en
SQLite local. Ningún archivo se copia, elimina o mueve a otra carpeta. Sólo
los archivos temporales creados por las pruebas se renombran tras confirmación
y se restauran. / T17 has no network or telemetry client; paths and audit data
stay in local SQLite. No file is copied, deleted, or moved to another folder.
Only test-created temporary files are renamed after confirmation and restored.

`LIB-012` queda `VERIFIED`. `PRI-001` continúa `IN_PROGRESS` hasta la auditoría
integral de tráfico del MVP; T17 añade evidencia de que el flujo de renombrado
es exclusivamente local. / Safe rename is verified. The privacy ID remains in
progress until the complete MVP traffic audit; T17 proves this flow is local
only.
