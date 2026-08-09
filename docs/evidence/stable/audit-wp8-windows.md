# WP-8 — Windows y experiencia de errores / Windows and the error experience

Evidencia de la primera ola del paquete WP-8 de la auditoría profunda del 2026-08-08. Lo que sigue
pendiente del paquete queda listado al final. / Evidence for the first wave of the WP-8 package;
what remains of the package is listed at the end.

## WIN-002 — El proceso declara lo que pide de Windows / The process declares what it asks of Windows

**El defecto / The defect.** El host no tenía manifiesto de aplicación: el proceso corría bajo el
límite de 260 caracteres incluso en máquinas cuya política ya lo levantó — una biblioteca bajo una
carpeta profunda simplemente pierde archivos — y la conciencia de DPI era la que el runtime
adivinara, no el modo por monitor para el que las ventanas del reproductor están escritas. / The
host had no application manifest: the 260-character limit applied even where policy lifted it, and
DPI awareness was whatever the runtime guessed.

**RED (archivado / archived).** `WindowsHostManifestTests`, dos aserciones, ambas en rojo a la
primera ejecución: el proyecto no declaraba manifiesto y el archivo no existía. / two assertions,
both red on their first run.

**La corrección / The fix.** `app.manifest` propio con `longPathAware=true` y
`dpiAwareness=PerMonitorV2`, declarado en el proyecto; el porqué de cada ajuste está escrito en el
propio manifiesto. **GREEN**: `WindowsHostManifestTests` 2/2; ArchitectureTests 16/16.

## BUG-012 — La historia de migraciones se re-verifica y la integridad se pregunta una vez / The migration history is re-verified and integrity is asked once

**El defecto / The defect.** El arranque sólo comparaba **números de versión** de las migraciones
aplicadas: una migración cuyo texto cambió tras aplicarse pasaba desapercibida, con el esquema del
disco y el que este build asume divergiendo en silencio. Y la integridad se preguntaba dos veces en
cada arranque — el runner antes de migrar y el ensamblado justo después — duplicando la parte más
lenta de abrir una biblioteca grande. / Startup only compared applied **version numbers**, and
integrity was asked twice on every start.

**La corrección / The fix.**

- `schema_history` guarda el checksum de cada migración aplicada desde siempre; ahora se **lee y se
  compara** con el que este build trae: una discrepancia rehúsa el archivo antes de escribir una
  sola fila, nombrando versión y ambos checksums (la pantalla de recuperación ya sabía recibirlo). /
  the stored checksum of every applied migration is now read and compared; a mismatch refuses the
  file before a single row is written.
- `AppliedMigrationCount` en el runner: el segundo `integrity_check` del arranque sólo corre cuando
  una migración reescribió el archivo — el runner ya lo comprobó en este mismo arranque antes de
  tocar nada. / the second startup `integrity_check` only runs when a migration actually rewrote
  the file.

**Pruebas / Tests.** `MigrationHistoryTests` 2/2 (el texto reescrito se rehúsa nombrando el
checksum; la historia intacta migra sin queja y cuenta cero). Las dos se escribieron junto a la
corrección: el estado anterior lo demuestra el código sustituido, que sólo leía `version`. /
written alongside the fix; the prior state is shown by the replaced code, which read only
`version`.

## BUG-009 — Una detección no puede salirse de su episodio / A detection cannot outrun its episode

**El defecto / The defect.** Las marcas manuales siempre validaron contra la duración del episodio;
las detecciones se juzgaban con `duration: null` y el detector emitía sin recortar: una ventana
recurrente que rebasa el final de un episodio corto se almacenaba como un rango que ninguna
reproducción puede alcanzar. / Manual markers always validated against the episode's duration;
detections were judged blind and emitted unclamped.

**RED (archivado / archived).**
`A_detected_range_never_outruns_the_episode_it_was_measured_in` (tres episodios de 50 s con una
intro compartida de 60 s: el detector emitía `End = 60 s`) y la sobrecarga con duraciones de
`MergeDetections` que no existía. / three 50 s episodes sharing a 60 s intro: the detector emitted
`End = 60 s`; and the duration-aware `MergeDetections` overload did not exist.

**La corrección / The fix.** `Emit` recorta al episodio en que se midió (descarta lo que empieza
tras el final, recorta lo que lo rebasa) y `MergeDetections` acepta duraciones por archivo y aplica
a las detecciones **la misma regla** que a las marcas manuales cuando el llamante las conoce.
**GREEN**: MediaTests 108/108 (corpus incluido), Domain.Tests 355/355.

## REL-A08 — Desinstalar no borra sus datos, y ahora lo dice / Uninstalling keeps your data, and now it says so

El manual de usuario dice en ambos idiomas que desinstalar deja `%LOCALAPPDATA%` intacto, que
reinstalar reencuentra el catálogo, cómo borrarlo de verdad, y que los vídeos nunca están dentro. /
The user guide now says, in both languages, that uninstalling leaves `%LOCALAPPDATA%` untouched,
that a reinstall finds the catalog again, how to really erase it, and that videos are never inside.

## GREEN transversal / Cross-cutting GREEN

MediaTests 108/108, IntegrationTests 363/363 (+1 skip declarado), UiTests 349/349,
Application.Tests 194/194, Domain.Tests 355/355, ArchitectureTests 16/16, `dotnet format` limpio,
`-warnaserror` 0/0, `verify-docs` en verde.

## Segunda ola (2026-08-09) / Second wave

- **WIN-004 — el renombrado bloqueado dice qué hacer.** RED: la vista del renombrado no mostraba
  nada de un fallo y la auditoría guardaba «IOException», que no pide ninguna acción. Corrección:
  `SafeFileRenamer` clasifica el fallo por lo que una persona puede hacer (`FileInUse` para
  violación de compartición/bloqueo, `AccessDenied`, o el tipo), el modelo expone `FailureKey` y la
  vista muestra el mensaje accionable ES/EN. Paseo real en
  `A_rename_blocked_by_an_open_file_says_so_and_says_what_to_do`: archivo retenido por un handle
  real, la interfaz dice que otro programa lo tiene abierto, la auditoría dice `FileInUse` y el
  archivo no se movió. / the renamer classifies by what a person can do, the surface says it in
  both languages, and the walk holds a real handle.
- **WIN-003 — la ventana vuelve a donde estaba.** `MainWindowPlacement`: coloca la ventana al
  arrancar (posición física exacta, tamaño lógico), sigue su última geometría normal, y escribe una
  vez al cerrar por cualquier camino; maximizada guarda los límites de restauración, y una posición
  que ninguna pantalla actual muestra se descarta con la misma regla `IsVisibleOn` del mini
  reproductor. `MainWindowPlacementTests` 3/3. / places at start, follows the last normal
  geometry, writes once on close; an invisible stored position is discarded by the mini player's
  own rule.
- **REL-A03 — la entrada `Run` huérfana, documentada.** El manual ES/EN explica que desinstalar con
  «iniciar con Windows» activo deja una entrada inocua, que reinstalar la repara sola y cómo
  quitarla a mano. / the manual explains the orphaned entry, that reinstalling heals it, and how to
  remove it by hand.
- **BUG-011 — un solo idioma para toda la aplicación.** RED: la interfaz iba fija en español
  mientras el resumen del actualizador y los metadatos TMDB seguían la cultura de la máquina — dos
  fuentes de verdad. `StoredLanguageService` resuelve la preferencia guardada (o español, el
  declarado de siempre), y aplicarla mueve juntos los diccionarios de recursos y la cultura del
  hilo que leen el actualizador y los metadatos; Ajustes → Apariencia gana el selector con su
  indicio no cromático. `LanguageServiceTests` en verde; los metadatos usan el idioma nuevo al
  reiniciar y la descripción lo dice. / one stored preference moves the resources and the thread
  culture together; the settings surface gains the selector.
- **ARQ-009 — el diagnóstico dice lo que la máquina hizo.** RED: `HardwareAccelerationAvailable:
  true`, `LibraryItemCount: 0` y `Errors: []` eran constantes. Ahora la aceleración es la respuesta
  del motor para el último medio abierto (false sin evidencia), el recuento sale del resumen real
  de la biblioteca, y los errores son los que la máquina escribió de verdad — el registro de
  renombrados agrupado por código, sin rutas ni nombres, con las cubetas de siempre. / the
  acceleration is the engine's own answer, the count is the real library summary, and the errors
  are the rename audit's, bucketed as ever.

## GREEN de la segunda ola / Second-wave GREEN

UiTests 354/354, AccessibilityTests 57/57, IntegrationTests 368/368 (+1 skip declarado),
Domain.Tests 356/356, Application.Tests 194/194, ArchitectureTests 16/16, `dotnet format` limpio,
`-warnaserror` 0/0, `verify-docs` en verde. **WP-8 completo.** / WP-8 complete.
