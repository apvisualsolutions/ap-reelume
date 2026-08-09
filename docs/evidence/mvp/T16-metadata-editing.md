# T16 — Edición protegida de metadatos e imágenes / Protected metadata and artwork editing

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `aae2387`
- Commit de tarea / Task commit: `feat: edit and lock catalog metadata safely`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, Avalonia Headless
- IDs: `LIB-011=VERIFIED`, `DAT-002=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`MetadataEditingTests`, `ArtworkCacheTests` y `MetadataEditorTests` se
escribieron antes del código de producción. RED falló porque aún no existían
los casos de uso, el almacén de imágenes ni el editor; la salida se conserva
en `artifacts/test-results/T16/red/T16-red.log`. / The three plan-named test
files were written before production code. RED failed because the use cases,
artwork store, and editor did not exist; its output is retained at the path
above.

GREEN conserva 3/3 pruebas de aplicación, 3/3 de integración y 4/4 de UI, sin
fallos. Los TRX, logs y Cobertura están bajo
`artifacts/test-results/T16/green/`. / GREEN retains 3/3 application tests,
3/3 integration tests, and 4/4 UI tests, all passing. TRX, logs, and Cobertura
are stored under the path above.

La puerta transversal `eng/verify.ps1 -Configuration Release -Runtime
win-x64` compiló con 0 advertencias/errores y ejecutó 201 pruebas con 0 fallos.
La verificación documental aprobó 24 Markdown, 2 recursos localizados, 53 IDs
y 46 IDs MVP. El análisis de dependencias halló 0 paquetes vulnerables o en
desuso y el escaneo de secretos permitido halló 0 coincidencias. / The
Release/win-x64 cross-cutting gate built with zero warnings/errors and ran 201
tests with zero failures. Documentation passed 24 Markdown files, 2 localized
resources, 53 IDs, and 46 MVP IDs. Dependency and secret scans found no
vulnerable/deprecated packages and no secret matches.

La cobertura focal combinada del código nuevo es 294/310 líneas (94,84 %):
`UpdateMetadata` 95,45 %, `RefreshMetadata` 91,30 %, `ArtworkCache` 89,87 %,
`ArtworkPickerViewModel` 93,75 % y `MetadataEditorViewModel` 98,48 %. T16 no
añade una política de dominio nueva; reutiliza `MetadataMergePolicy`, cuya
cobertura de ramas quedó demostrada en T13. / Combined focused new-code
coverage is 294/310 lines (94.84%), with the per-file percentages listed
above. T16 adds no domain policy and reuses the merge policy verified in T13.

## Edición y concurrencia / Editing and concurrency

- `UpdateMetadataCommand(TitleId, FieldChanges, LockedFields,
  ExpectedRevision)` aplica el cambio completo mediante un único puerto de
  repositorio y devuelve `Conflict` ante una revisión obsoleta. / The command
  applies one complete change through a single repository port and reports a
  stale optimistic revision as a conflict.
- Los siete campos se pueden bloquear y desbloquear individualmente. Tres
  ciclos editar→refrescar→reiniciar conservan el título manual bloqueado. /
  All seven fields can be locked and unlocked independently. Three
  edit→refresh→restart cycles preserve the manually locked title.
- La actualización remota conserva todo campo bloqueado; la acción separada
  “Restaurar campos del proveedor” elimina los bloqueos y vuelve a aplicar los
  valores del proveedor. / Remote refresh preserves every locked field; the
  separate “Restore provider fields” action clears locks and reapplies
  provider values.

## Arte, privacidad y seguridad / Artwork, privacy, and safety

El arte elegido localmente se copia bajo `personal-artwork/<TitleId>/`, exige
texto alternativo y se marca exportable. Las imágenes remotas se descargan
mediante `HttpClient` bajo `cache/artwork/<TitleId>/`, no son exportables y se
pueden borrar y regenerar. Un fallo remoto conserva la referencia anterior. /
Locally selected artwork is copied under the personal-artwork root, requires
alternative text, and is exportable. Remote artwork uses HttpClient and a
separate regenerable, non-exportable cache; a failed request preserves the
previous reference.

La limpieza valida una ruta exacta confinada al directorio de datos y elimina
únicamente `cache/artwork/`; el test confirma que el arte personal sigue
intacto. Ninguna ruta privada se envía al proveedor y T16 no renombra, mueve,
copia ni elimina archivos de vídeo. / Cache clearing validates the exact root
under application data and deletes only remote artwork; personal artwork
remains intact. No private path is sent to the provider, and T16 never
renames, moves, copies, or deletes a video file.

## UI bilingüe y accesible / Bilingual accessible UI

Los siete bloqueos, todos los campos editables y las tres acciones tienen
nombres de automatización localizados. El selector no permite aplicar arte
local o remoto sin texto alternativo. La prueba headless inspecciona los
controles como lector de pantalla y genera ambas capturas. / All seven locks,
editable fields, and actions have localized automation names. The picker does
not allow local or remote artwork without alternative text. The headless test
inspects screen-reader metadata and creates both screenshots.

| Artefacto / Artifact | Ruta / Path |
|---|---|
| Captura ES / ES screenshot | `artifacts/ui-captures/T16/metadata-editor-es-ES.png` |
| Captura EN / EN screenshot | `artifacts/ui-captures/T16/metadata-editor-en-US.png` |

`LIB-011` queda `VERIFIED`. `DAT-002` continúa `IN_PROGRESS`: T16 demuestra
la separación entre dato personal y caché regenerable; T36 debe verificar su
inclusión/exclusión real en exportación, restauración y copias rotatorias. /
Metadata editing is verified. Backup/export remains in progress until T36
verifies actual inclusion and exclusion through export, restore, and rotating
backups.
