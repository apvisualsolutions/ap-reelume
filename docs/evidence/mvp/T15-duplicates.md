# T15 — Duplicados como versiones / Duplicates as versions

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `881ebae`
- Commit de tarea / Task commit: `feat: preserve duplicates as selectable media versions`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, Avalonia Headless
- IDs: `LIB-008=VERIFIED`, `PLY-010=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`MediaVersionSelectionTests`, `GroupMediaVersionsTests` y
`DuplicateReviewTests` se escribieron antes del código de producción. RED
falló por la ausencia de modelos, política, casos de uso y panel de versiones;
se conserva en `artifacts/test-results/T15/red/T15-red.log`. / The three
plan-named test files were written before production code. RED failed because
the version models, policy, use cases, and panel did not exist; output is
retained at the path above.

GREEN conserva 6/6 pruebas de dominio, 3/3 de aplicación y 1/1 prueba UI
bilingüe, todas sin fallos, con TRX y Cobertura bajo
`artifacts/test-results/T15/green/`. / GREEN retains 6/6 domain tests, 3/3
application tests, and 1/1 bilingual UI test, all passing, with TRX and
Cobertura under the path above.

La puerta transversal Release/win-x64 terminó con 0 advertencias, 0 errores,
191 pruebas ejecutadas y 0 fallos. La documentación aprobó 23 Markdown, 2
archivos localizados, 53 IDs y 46 IDs MVP. / The Release/win-x64 cross-cutting
gate completed with 0 warnings, 0 errors, 191 executed tests, and 0 failures.
Documentation passed 23 Markdown files, 2 localized files, 53 IDs, and 46 MVP
IDs.

## Agrupación y selección / Grouping and selection

- Dos archivos `5x10` con duración compatible se agrupan idempotentemente con
  un ID derivado de la clave de contenido; repetir no duplica relaciones. / Two
  compatible `5x10` files group idempotently under a content-derived ID;
  repeating does not duplicate relationships.
- Duraciones materialmente distintas se tratan como ediciones y exigen
  `ConfirmDifferentEditions`; sin confirmación no se guarda un grupo. /
  Materially different durations are treated as editions and require explicit
  confirmation; no group is saved beforehand.
- La elección efectiva ordena disponibilidad, preferencia manual, resolución,
  HDR configurable, códec, tamaño y finalmente ID estable. / Effective
  selection orders availability, manual preference, resolution, configurable
  HDR preference, codec, size, and finally stable ID.
- Si la preferida no está disponible, se usa temporalmente otra versión sin
  cambiar `PreferredMediaFileId`; al reconectar se recupera la preferida. / If
  the preferred file is unavailable, a temporary fallback is used without
  changing the stored preference; reconnecting restores it.
- `SetPreferredVersion` rechaza cualquier archivo que no pertenezca al grupo.
  / The preferred-version command rejects any file outside the group.

## Visibilidad, UI y seguridad / Visibility, UI, and safety

El panel enumera cada archivo con una ruta abreviada (`…\\carpeta\\archivo`),
calidad y disponibilidad; la raíz privada completa no aparece. Las dos
versiones permanecen visibles incluso cuando una está desconectada. / The
panel lists every file with an abbreviated path, quality, and availability;
the full private root is absent. Both versions remain visible even when one is
disconnected.

No existe comando ni miembro de borrar, ocultar o quitar en la selección o en
la vista. T15 no abre ni modifica archivos de vídeo: solo crea modelos,
decisiones y una interfaz de persistencia. / No delete, hide, or remove command
or member exists in the selection or view. T15 does not open or modify video
files; it only creates models, decisions, and a persistence port.

| Artefacto / Artifact | Ruta / Path |
|---|---|
| Captura ES / ES screenshot | `artifacts/ui-captures/T15/duplicates-es-ES.png` |
| Captura EN / EN screenshot | `artifacts/ui-captures/T15/duplicates-en-US.png` |

Cobertura focal: `MediaVersionSelectionPolicy` 100 % líneas / 100 % ramas;
`GroupMediaVersions` 94,73 % líneas; `SetPreferredVersion`,
`DuplicateReviewViewModel` y `MediaVersionItemViewModel` 100 % líneas. /
Focused coverage reports the same percentages for the named new-code
components.

`LIB-008` queda `VERIFIED`. `PLY-010` continúa `IN_PROGRESS`: T15 establece
identidad/selección, pero la transferencia de progreso entre versiones se
implementa en T27. / The duplicate feature is verified; progress transfer
remains in progress until T27.
