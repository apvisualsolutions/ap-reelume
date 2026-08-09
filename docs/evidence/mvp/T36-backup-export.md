# T36 — Copias rotatorias y exportación segura / Rotating Backups and Safe Export

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `58b77b3`
- Commit de tarea / Task commit: `feat: create rotating backups and safe exports`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  SQLite en WAL, FlaUI UIA3 5.0.0, NVIDIA GeForce RTX 5070
- IDs: `UX-005=VERIFIED`; `LIB-011=VERIFIED` suma la prueba de exportación / gains its export proof;
  `DAT-002=IN_PROGRESS`, cierra con la restauración de T37 / closes with T37's restore;
  `PRD-001=IN_PROGRESS`, suma evidencia / gains evidence

## RED y GREEN / RED and GREEN

`BackupWorkflowTests`, `RotatingBackupTests`, `ZipExportTests` y `BackupViewTests` se escribieron antes
que los contratos. RED falló en compilación porque no existían `BackupManifest`, `BackupContentPolicy`,
`CreateBackup`, `ExportLibrary`, `SqliteBackupService`, `RotatingBackupStore`, `ZipExportService` ni
`BackupViewModel`. La salida está en `artifacts/test-results/T36/red/build.log`. / The four suites were
written first and RED failed on every missing type.

El ViewModel que crea esta tarea tiene prueba desde el ciclo RED. / The one view model this task creates
is covered from RED.

GREEN ejecuta **897 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura bajo
`artifacts/test-results/T36/green/`. `dotnet format --verify-no-changes` no informa cambios, Debug y
Release terminan con 0 advertencias bajo `-warnaserror`, `eng/verify.ps1` pasa entera y
`eng/run-accessibility.ps1 -Mode Verify -Passes 2` da **0 críticos, 0 mayores, 0 menores** con la
superficie nueva ya dentro del recorrido canónico. La suite pasó de **828** a **897**. / GREEN runs 897
tests with no failures and no skips; the suite grew by 69.

## Qué entra en una copia y qué no / What a backup carries and what it refuses

`BackupContentPolicy` es el único sitio que lo decide, y lo comprueban dos capas: al construir la carga y
otra vez al empaquetarla. Una entrada no permitida hace **fallar** la exportación en lugar de
descartarse en silencio, porque una exportación que calla lo que dejó fuera no es inspeccionable.

| Entra / Included | Se queda fuera / Excluded |
|---|---|
| `library.db` — instantánea consistente | vídeos de cualquier contenedor |
| `settings.json` — preferencias | `cache/artwork` — imágenes descargadas |
| `personal-artwork/**` — imágenes elegidas por la persona | `diagnostics/**` |
| `manifest.json` | cualquier `*.token`, `library.db-wal` y `library.db-shm` |

Las marcas personales, el progreso, los marcadores manuales y las decisiones bloqueadas viajan **dentro
de la base**, así que la prueba no se conforma con que el `.db` exista: reabre el ZIP, abre la base
extraída y comprueba fila a fila. / Personal data is verified row by row after reopening the archive.

| Comprobación tras reabrir el ZIP / Check after reopening | Resultado / Result |
|---|---|
| Favorito, ver más tarde y valoración 9 | presentes / present |
| Progreso en 12 min | presente / present |
| Marcador manual de serie | presente / present |
| `match_candidates` con `decision_locked = 1` | 1 fila / 1 row |
| `library_roots` | 1 fila / 1 row |
| `PRAGMA integrity_check` de la base extraída | `ok` |

## La instantánea se toma mientras se escribe / The snapshot is taken during writes

`SqliteBackupService` reutiliza el mecanismo que el corredor de migraciones ya usaba —
`PRAGMA wal_checkpoint(FULL)`, `BackupDatabase` y `PRAGMA integrity_check` — y sólo mueve el archivo a
su destino cuando ha pasado esa comprobación. Con un escritor concurrente de progreso golpeando la base,
la copia abre sola, pasa integridad y conserva las catorce migraciones. Un destino que no admite el
archivo deja cero temporales. / The snapshot survives concurrent writes and leaves no temporary file.

## La retención nunca borra la última copia restaurable / Retention never deletes the last restorable copy

Una copia es restaurable cuando cada archivo que su manifiesto promete sigue ahí y sigue dando el mismo
SHA-256. Esa respuesta no depende de ningún motor de base de datos: un byte cambiado basta para
descartarla.

| Escenario / Scenario | Resultado / Result |
|---|---|
| Seis copias válidas, retención 5 | se borra la más antigua; quedan 5 |
| Seis copias dañadas más recientes y una válida antigua | **la válida sobrevive**; se borra una dañada |
| Copia sin manifiesto o con manifiesto ilegible | se lista como inválida, no falla |
| Manifiesto de otro formato o con preferencias cambiadas | inválida |
| Dos copias en el mismo segundo | nombres distintos; ninguna se pisa |
| Publicar o descartar algo fuera de la carpeta | rechazado |
| Espacio insuficiente | se rechaza **antes** de crear nada |

## Verificación física / Physical verification

Primero, un archivo real abierto con herramientas ajenas al código de la aplicación —`Expand-Archive` y
`Get-FileHash`—, exportado mientras un escritor concurrente añadía filas de progreso:

| Comprobación / Check | Resultado / Result |
|---|---|
| Entradas del ZIP / Archive entries | `library.db`, `manifest.json`, `personal-artwork/…/poster.jpg`, `settings.json` |
| Hashes del manifiesto contra `Get-FileHash` | **todos coinciden / all match** |
| Cabecera de la base extraída / Extracted database header | `SQLite format 3` |
| Vídeo, caché remota, token, `-wal` o `-shm` / Video, remote cache, token, WAL or SHM | **0** |
| Canario del token sembrado / Seeded token canary | **0 apariciones / 0 hits** |
| Raíces registradas en el manifiesto / Roots recorded | 1 |

Después, la aplicación real: se lanza el ejecutable `Release`, se navega a **Copias** por su botón de
navegación y se pulsa **Crear una copia ahora** con automatización UIA.

| Comprobación / Check | Resultado / Result |
|---|---|
| Superficie presente / Surface present | tres botones y el estado, todos con nombre accesible |
| «Cancelar» en reposo / “Cancel” at rest | presente y **deshabilitado** |
| Copia creada / Backup created | sí, con manifiesto de formato 1 |
| Hash de la base contra el manifiesto / Database hash against manifest | coincide / matches |
| Entradas prohibidas en la copia real / Forbidden entries in the real copy | **0** |
| Estado tras terminar / Status afterwards | «Listo.» |
| Restos de staging / Staging leftovers | **0** |

Y la retención sobre la aplicación real, pulsando el botón siete veces en total:

| Pulsación / Click | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---:|---:|---:|---:|---:|---:|
| Copias en disco / Copies on disk | 2 | 3 | 4 | 5 | 5 | 5 |

Se estabiliza en cinco, la más antigua desaparece y no queda ningún `.staging-`. / Retention settles at
five on the real application, with no staging left behind.

## La pantalla no enseña carpetas / The screen shows no folders

`BackupViewModel` publica **el nombre** de la copia y **el nombre** del archivo exportado, nunca la ruta
que lleva a ellos, y una prueba lo fija exportando a una carpeta llamada `personal` y comprobando que
esa palabra no aparece en ninguna propiedad visible. El destino lo elige Windows con su propio diálogo:
la aplicación no ve ninguna carpeta que no le hayan entregado, y cancelar el diálogo simplemente no
exporta nada. / The screen names files, never folders, and the destination comes from the Windows picker.

Los estados y las etapas viajan como **claves de recurso**, así que la pantalla habla el idioma elegido;
las veintiuna claves nuevas están en los dos diccionarios y `ShellLocalizationTests` compara los
conjuntos. / Status and stages travel as resource keys present in both dictionaries.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Application/Backup/BackupContracts.cs` | 49/49 — 100 % |
| `Application/Backup/CreateBackup.cs` | 107/107 — 100 % |
| `Application/Backup/ExportLibrary.cs` | 26/26 — 100 % |
| `Infrastructure/Backup/RotatingBackupStore.cs` | 97/97 — 100 % |
| `Infrastructure/Backup/ZipExportService.cs` | 45/45 — 100 % |
| `Infrastructure/Backup/SqliteBackupService.cs` | 48/50 — 96,00 % |
| `Presentation/Backup/BackupViewModel.cs` | 116/116 — 100 % |
| `Windows/AppDataPaths.cs` | 22/22 — 100 % |
| **Total del código nuevo / New code total** | **510/512 — 99,61 %** |

Las dos líneas sin cubrir son la guarda que rechaza una instantánea que no supera su propia comprobación
de integridad: forzarla exigiría que SQLite produjera una copia corrupta a partir de una base sana. /
The two uncovered lines are the guard against a snapshot failing its own integrity check.

## Privacidad y límites / Privacy and boundaries

- **Sin red**: ningún archivo de esta tarea abre un socket ni resuelve un nombre.
- **Sin rutas en la interfaz**: la pantalla nombra archivos, nunca carpetas.
- **Sin secretos ni caché en el archivo**: comprobado por lista permitida en dos capas, por censo del
  ZIP extraído y con un canario de token sembrado que da cero.
- **Sin operaciones destructivas sobre medios**: la copia lee la base, las preferencias y el arte
  personal; no toca ningún vídeo.
- **Borrado acotado**: lo único que se borra es una copia rotatoria sobrante, y nunca la más reciente que
  siga siendo restaurable.
- **Artefactos y medios ignorados**: `git status` no incluye `artifacts/`, ningún `.zip` ni ningún medio.
- **Sin datos personales versionados**: ningún archivo tocado contiene nombre de usuario, nombre de
  equipo ni ruta absoluta local; esta evidencia describe la forma de las copias, no su contenido.

## Salvedades declaradas / Declared caveats

1. **`DAT-002` sigue `IN_PROGRESS`.** Exportar es la mitad del identificador; la otra mitad es restaurar
   en rutas distintas, y eso es T37. / Export is half of `DAT-002`; the restore closes it.
2. **`IAppDataPaths` se amplió** de una ruta a la carpeta de datos completa. Sin ese contrato la lista
   permitida tendría que recomponer carpetas a mano, que es justo lo que no debe hacer el código que
   decide qué sale del equipo. `SqliteIsolationTests` sigue verde: el contrato no nombra ningún tipo
   SQLite. / The app-data contract now covers the whole data folder.
3. **La copia real del equipo no incluyó `settings.json` ni arte personal** porque esa instalación aún no
   los tiene escritos. La ausencia se declara en el manifiesto (`preferencesSha256` nulo) en lugar de
   inventarse. / The real machine had no preferences file yet, and the manifest says so.

`UX-005` pasa a `VERIFIED`: las marcas personales persisten, filtran **y** viajan en la copia y en la
exportación, comprobadas fila a fila tras reabrir el archivo. / Personal marks now travel in the backup
and the export, verified row by row.
