# T37 — Restauración validada y reasignación de raíces / Validated Restore and Root Remapping

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `8dea753`
- Commit de tarea / Task commit: `feat: validate stage and remap library restores`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  SQLite en WAL, FlaUI UIA3 5.0.0, NVIDIA GeForce RTX 5070
- IDs: `DAT-001=VERIFIED`, `DAT-002=VERIFIED`; `LIB-009` y `LIB-010` suman la prueba de rutas nuevas /
  both gain their new-path proof

## RED y GREEN / RED and GREEN

`RootRemapPolicyTests`, `RestoreValidationTests`, `DisasterRecoveryTests` y `RestoreWizardTests` se
escribieron antes que la política, el validador, el servicio por etapas y el asistente. RED falló en
compilación porque no existían `RootRemapPolicy`, `RestoreFindingKind`, `RestorePreview`,
`BackupValidator`, `StagedRestoreService`, `RestoreBackup` ni `RestoreWizardViewModel`. La salida está
en `artifacts/test-results/T37/red/build.log`. / The four suites were written first and RED failed on
every missing type.

Los dos ViewModel que crea esta tarea tienen prueba desde el ciclo RED. / Both view models this task
creates are covered from RED.

GREEN ejecuta **970 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T37/green/`. `dotnet format --verify-no-changes` no informa cambios, Debug
y Release terminan con 0 advertencias bajo `-warnaserror`, `eng/verify.ps1` pasa entera y
`eng/run-accessibility.ps1 -Mode Verify -Passes 2` da **0 críticos, 0 mayores, 0 menores** con el
asistente ya dentro del recorrido canónico. La suite pasó de **897** a **970**. / GREEN runs 970 tests
with no failures and no skips; the suite grew by 73.

## La invariante: la base activa no cambia en ningún fallo / The invariant

Diez fallos inyectados, uno por fase, comparando la base activa **byte a byte** antes y después y
volviendo a comprobar su integridad:

| Fallo inyectado / Injected failure | Fase / Phase | Base activa / Active database |
|---|---|---|
| Archivo que no es un ZIP | inspección | idéntica / identical |
| Base con hash que no cuadra | inspección | idéntica |
| Entrada `../escaped.json` (**Zip Slip**) | inspección | idéntica |
| Entrada `payload.mp4` | inspección | idéntica |
| Base con hashes correctos pero corrupta | apertura | idéntica |
| Manifiesto ausente | inspección | idéntica |
| Dos raíces al mismo destino | remap | idéntica |
| Espacio insuficiente | previo | idéntica |
| Cancelación justo antes del swap | swap | idéntica |
| Fallo de E/S durante el swap | swap | idéntica |

En los diez casos `Restored=false`, `PRAGMA integrity_check` sigue dando `ok`, las preferencias no
cambian y **no queda ni un directorio de staging**. El fallo durante el swap tiene además su propia
prueba: la base se aparta primero y vuelve a su sitio si la sustitución no se completa. / Ten injected
failures, ten identical databases, no staging left behind.

## Lo que el archivo tiene que demostrar antes de que se desempaquete / What an archive must prove

| Caso / Case | Resultado / Result |
|---|---|
| Archivo bien formado | sin hallazgos / no findings |
| Formato 99 | `UnsupportedFormat` |
| Sin manifiesto | `MissingEntry`, manifiesto nulo |
| Manifiesto que no es JSON, o que es `null` | `UnsupportedFormat` |
| Base que no cuadra con su hash | `HashMismatch` |
| Falta un arte que el manifiesto prometía | `MissingEntry` |
| `../escaped.json`, `..\escaped.json`, `personal-artwork/../../x.jpg`, `C:/…`, `/etc/passwd` | `PathEscape` o `ForbiddenEntry`, y **fuera de la lista de entradas aceptadas** |
| `payload.mp4` | `ForbiddenEntry`, nombrado en el hallazgo |
| Entrada mayor que el límite | `EntryTooLarge` |
| Archivo inexistente o que no es un ZIP | `UnreadableArchive`, sin excepción |
| Base íntegra según los hashes pero ilegible al abrirla | `DatabaseUnreadable` |
| Volumen demasiado pequeño | `NotEnoughSpace` |

El desempaquetado repite la comprobación: aunque el archivo cambiara entre la inspección y la
extracción, `StagedRestoreService` rechaza toda entrada fuera de la lista permitida y toda ruta que
saliera de su propia carpeta, y limpia lo que llevara escrito. / Unpacking re-checks what the validator
checked, and cleans up after a refusal.

## Reasignación de raíces / Root remapping

| Caso / Case | Decisión / Decision |
|---|---|
| Carpeta que sigue ahí | `Unchanged` |
| Misma carpeta escrita distinto (`d:/media\` → `D:\MEDIA`) | `Unchanged` |
| Carpeta antigua → nueva | `Remapped` |
| Carpeta que ya no existe y nadie reasigna | `Missing`, **no bloquea** |
| Dos raíces distintas al mismo destino | `Conflict` en ambas, **bloquea** |
| La misma raíz nombrada dos veces con el mismo destino | `Remapped`, no es conflicto |
| Reasignación de una raíz que la copia nunca tuvo | rechazada por nombre |
| Raíces anidadas | gana la más larga; los archivos no se cruzan |

El conflicto es lo único que detiene una restauración porque es lo único irreversible: fundir dos
bibliotecas en una carpeta no se puede deshacer después. Una carpeta ausente, en cambio, es un hecho
que se informa y ya. / A conflict is the one blocking case, because merging two libraries cannot be
undone afterwards.

## Verificación física — sin VM limpia / Physical verification without a clean VM

Este equipo no tiene hipervisor con Windows —comprobado: `Get-VM` no existe y no hay VirtualBox,
VMware ni QEMU—, así que la restauración se verifica en **tres clases de ruta distinta del mismo
equipo**, que es lo que la prueba realmente demuestra: que el catálogo restaurado apunta a donde la
persona diga, no a donde estaba.

| Destino / Destination | Restaurado | Raíz | Rutas movidas | Marcas | Progreso | Marcadores | Preferencias y arte | Reescaneo | Duplicados | Staging |
|---|---|---|---|---|---|---|---|---|---|---|
| Carpeta nueva / New folder | sí | `Remapped` | 1 | favorito y 8 | 1260 s | 1 | sí | 1 enum, 0 sondeos, 1 sin cambios | **0** | 0 |
| Unidad sustituida con `subst` / Substituted drive | sí | `Remapped` | 1 | favorito y 8 | 1260 s | 1 | sí | 1 enum, 0 sondeos, 1 sin cambios | **0** | 0 |
| UNC local `\\localhost\<recurso>\…` | sí | `Remapped` | 1 | favorito y 8 | 1260 s | 1 | sí | 1 enum, 0 sondeos, 1 sin cambios | **0** | 0 |

La ruta UNC se hizo **sin elevación y sin crear nada**: se usó un recurso compartido que el sistema ya
publicaba. En las tres, la base sustituida quedó conservada junto a la activa. / All three destinations
restore identically, and the replaced database is kept in each.

Y sobre la aplicación real, lanzada en `Release` y recorrida con automatización UIA:

| Comprobación / Check | Resultado / Result |
|---|---|
| El asistente está en la ruta Copias / The wizard is on the Backups destination | sí |
| «Elegir un archivo y ver qué haría» / “Choose an archive…” | presente y accionable |
| «Restaurar ahora» **antes** de una previsualización / “Restore now” before a dry run | presente y **deshabilitado** |
| Estado inicial / Initial status | «No has elegido ningún archivo todavía.» |

## El asistente no confirma lo que no puede hacer / The wizard offers no confirmation it cannot honour

`CanRestore` sale de la previsualización, no de la interfaz: mientras haya un solo hallazgo el botón de
confirmar no se puede pulsar. Editar la carpeta nueva de una fila vuelve a ejecutar la simulación con
ese remap, y las filas se conservan en lugar de reconstruirse para no sacar el cursor de donde alguien
está escribiendo. Al terminar, la pantalla nombra **el archivo** de la base conservada y nunca su
carpeta. / The confirmation comes from the dry run, and the screen names the preserved file, never its
folder.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Domain/Backup/RootRemapPolicy.cs` | 49/49 — 100 % |
| `Application/Backup/RestoreBackup.cs` | 36/36 — 100 % |
| `Application/Backup/PreviewRestore.cs` | 88/89 — 98,88 % |
| `Infrastructure/Backup/StagedRestoreService.cs` | 178/182 — 97,80 % |
| `Infrastructure/Backup/BackupValidator.cs` | 74/77 — 96,10 % |
| `Presentation/Backup/RestoreWizardViewModel.cs` | 128/128 — 100 % |
| `Presentation/Backup/RootRemapRowViewModel.cs` | 29/30 — 96,67 % |
| **Total del código nuevo / New code total** | **582/591 — 98,48 %** |

Lo que queda sin cubrir son guardas de tamaño total —haría falta un archivo de dos gigabytes para
alcanzarlas— y una rama de lectura de espacio libre. / What is uncovered are total-size guards and a
free-space branch.

## Privacidad y límites / Privacy and boundaries

- **Sin red**: ningún archivo de esta tarea abre un socket ni resuelve un nombre.
- **Sin rutas en la interfaz**: el asistente muestra las carpetas de la biblioteca porque son
  exactamente lo que hay que reasignar, y nombra la base conservada por su archivo.
- **Sin escrituras fuera de su sitio**: el staging vive bajo la carpeta de copias, se rechaza cualquier
  entrada que apunte fuera y se descarta pase lo que pase.
- **Sin operaciones destructivas sobre medios**: la restauración toca la base, las preferencias y el
  arte personal; ningún vídeo se lee, se mueve ni se borra.
- **La base sustituida se conserva siempre**, con marca de tiempo, junto a la activa.
- **Artefactos y medios ignorados**: `git status` no incluye `artifacts/` ni ningún medio.
- **Sin datos personales versionados**: esta evidencia describe la forma de las rutas UNC, nunca la
  ruta real.

## Salvedades declaradas / Declared caveats

1. **No hay VM limpia de Windows**, comprobado en este equipo. La restauración se verifica en tres
   clases de ruta distinta del mismo equipo. El bloqueo se declara y **no se convierte en PASS**: lo
   que no se ha probado es una instalación de Windows recién hecha, no la reasignación de rutas. /
   No clean VM exists here; three kinds of different path on this machine stand in, and the block is
   declared rather than waved through.
2. **La ruta UNC usa un recurso compartido preexistente.** Crear uno nuevo exige elevación, que esta
   sesión no tiene, y ninguna tarea de recuperación debería introducir esa clase de cambio. / The UNC
   path reuses a share the system already published.
3. **`RestoreDatabaseFacts` no expone el catálogo**, sólo raíces y rutas de archivo, que es lo mínimo
   que la simulación necesita para decir cuántas rutas cambiarían.

`DAT-001` y `DAT-002` pasan a `VERIFIED`: la base local sobrevive a diez fallos distintos sin cambiar
un byte, y una copia se exporta y se restaura íntegra en rutas nuevas sin producir un solo duplicado. /
Both data identifiers verify.
