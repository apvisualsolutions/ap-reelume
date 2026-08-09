# T39B — Ensamblar la aplicación / T39B — Assemble the Application

- IDs: `PLY-001`, `LIB-001`, `LIB-006`, `LIB-007`, `LIB-011`, `UX-001`, `PRD-005`
- Decidida en / Decided in: [ADR-0003](../../adr/0003-assemble-the-application-before-packaging-it.md)
- Commit: `feat: assemble every built surface into the application`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts must be updated together.

---

## Español

### Qué se cerró

ADR-0003 midió catorce superficies construidas, probadas y con evidencia que **no eran alcanzables desde la aplicación**, y una raíz de composición que no registraba `IMetadataProvider` ni `ArtworkCache`. T39B las cablea. No se creó ninguna superficie nueva: cada una ya existía con su propia suite y su propia evidencia; lo que faltaba era que alguien las pidiera.

### Mapa de alcanzabilidad, antes y después

Alcanzabilidad medida como en el ADR: una superficie es alcanzable cuando `ShellView` la instancia, o cuando lo hace otra superficie alcanzable o su code-behind. Las dos raíces son las dos que la raíz de composición puede entregar a la ventana principal: el shell y la pantalla de recuperación.

| Superficie | ID | Antes | Después | Dónde vive ahora |
|---|---|---|---|---|
| `RootOnboardingView` | `LIB-001` | no | sí | Biblioteca, encima del catálogo |
| `ScanSettingsView` | `LIB-002` | no | sí | Ajustes |
| `ReviewInboxView` | `LIB-007` | no | sí | Revisar |
| `DuplicateReviewView` | `LIB-008` | no | sí | Revisar, al pedir las versiones de una ficha |
| `MetadataEditorView` | `LIB-011` | no | sí | Biblioteca, desde la ficha abierta |
| `RenamePreviewView` | `LIB-012` | no | sí | Biblioteca, desde la ficha abierta |
| `PlayerView` | `PLY-001` | no | sí | Capa de sesión, abierta desde una ficha |
| `AudioOutputView` | `PLY-004` | no | sí | Panel de la sesión |
| `TrackSelectorView` | `PLY-005` | no | sí | Panel de la sesión |
| `ResumePromptView` | `PLY-008` | no | sí | Sobre el reproductor |
| `MarkerEditorView` | `PLY-012` | no | sí | Panel de la sesión |
| `ShortcutSettingsView` | `PLY-014` | no | sí | Ajustes |
| `CreditsView` | `PRD-005` | no | sí | Ajustes |
| `SubtitleStyleView` | `A11Y-002` | no | sí | Ajustes |

Además quedan alcanzables las siete superficies que acompañaban a las catorce y que el ADR no enumeró por separado: `CandidateCardView`, `TransportControlsView`, `VideoStatusOverlay`, `SkipMarkerButton`, `NextEpisodeOverlay`, `VersionSwitchDialog`, `LooseFileBanner` y `MiniPlayerWindow`.

`CreditsView` no tenía `x:Class` ni nombre de automatización: era un fragmento que ninguna superficie podía alojar. Ahora es una superficie con nombre, y la atribución de TMDB y la licencia GPL-3.0-or-later se leen en Ajustes. Es una condición de licencia, no decoración.

### La puerta es una prueba, no una lista

[`SurfaceReachabilityTests`](../../../tests/ApSolutions.LocalMedia.UiTests/Shell/SurfaceReachabilityTests.cs) enumera las superficies del proyecto de presentación, construye el grafo desde las dos raíces y exige que cada una sea alcanzable. Tiene dos niveles:

1. las catorce que el producto declara, nombradas una a una con su ID, de modo que un fallo dice qué compromiso no está en la aplicación;
2. la regla estructural del ADR: **ninguna superficie con `AutomationProperties.Name` puede quedar fuera**. Una vista huérfana futura es un fallo de la suite, no un descubrimiento manual.

RED archivada en `artifacts/test-results/T39B/red/`: **18 fallos, 0 pasadas**, con las catorce nombradas de una en una. GREEN en `artifacts/test-results/T39B/green/`.

### Lo que la verificación física encontró

Las pruebas construyen la superficie; la aplicación la ensambla. Son dos preguntas distintas, y la segunda encontró **cinco defectos que ninguna prueba sin cabeza podía ver**. Todos están cerrados en este commit, cada uno con su prueba:

1. **Consentir el primer escaneo no escaneaba nada.** La superficie preguntaba y guardaba la respuesta; nadie actuaba sobre ella. Una instalación nueva añadía una carpeta y se quedaba vacía para siempre. Ahora el shell arranca el escaneo y recarga la biblioteca al terminar.
2. **Añadir una carpeta repetida cerraba la aplicación.** `AddLibraryRoot` rechaza duplicados y anidados con una excepción; el comando la dejaba escapar hasta el bucle de despacho y el proceso moría. Ahora el rechazo es una frase en pantalla, con nombre de automatización y `LiveSetting="Assertive"`.
3. **Un archivo escaneado y sin identificar se abría como serie sin episodios**, así que no ofrecía reproducir. Un título que nadie ha identificado es un archivo con una ruta: ahora abre la ficha de título único, que es la que sabe reproducirlo.
4. **Elegir una pista no la aplicaba ni la guardaba.** La lista cambiaba su etiqueta y el medio seguía igual.
5. **La sesión no alimentaba el registro de progreso.** Se reproducía y no se escribía nada, así que la oferta de reanudar no volvía a aparecer.

### El recorrido, sobre la aplicación real

Automatización UIA sobre el ejecutable publicado, ventana maximizada, invocación por patrón `Invoke`, carpeta de datos vacía antes de empezar. Veinticinco pasos, **veinticinco en verde**:

| Paso | Resultado |
|---|---|
| Arrancar la aplicación | ✅ |
| Ir a Biblioteca | ✅ |
| Escribir la ruta de la carpeta | ✅ |
| Añadir la carpeta (sin rechazo) | ✅ |
| Permitir el primer escaneo | ✅ |
| Aplicar y listar | ✅ 2 elementos |
| Ir a Revisar y ver la bandeja | ✅ |
| Volver y abrir la ficha | ✅ |
| Reproducir desde la ficha | ✅ |
| Reproductor, pistas, salida de audio y marcas en pantalla | ✅ |
| Cambiar la pista de audio | ✅ |
| Cerrar el reproductor | ✅ |
| Marcar favorito | ✅ |
| Ir a Ajustes y leer los créditos | ✅ |
| Cerrar la aplicación | ✅ salida limpia |

LibVLC decodificó con D3D11VA sobre la GPU discreta durante el recorrido.

**Qué llegó a la base de datos** después de cerrar, leído sobre `library.db`:

| Tabla | Filas |
|---|---|
| `library_roots` | 1 |
| `media_files` | 2 |
| `personal_state` | 1 |
| `playback_preferences` | 1 |
| `watch_state` | 1 |

### Privacidad: retirar el consentimiento borra el informe

Retirar el consentimiento de diagnósticos limpiaba la pantalla y dejaba el informe exportado en el disco. Ahora `CreateDiagnostics.DiscardAsync` borra el informe y su carpeta, y la pantalla de privacidad lo llama al apagar el interruptor. Una carpeta vacía sigue siendo el rastro de un informe que nadie consintió conservar, así que se borra entera.

### Esquema y composición

- Migración `0015_catalog_metadata_versions`: `catalog_metadata`, `media_version_groups` y `media_version_group_members`. Sin ella el editor de metadatos y la comparación de versiones no tenían dónde escribir. `SqliteBootstrapTests` fija ahora quince migraciones.
- `CompositionRoot` registra `IMetadataProvider` (TMDB), `ArtworkCache`, `IIdentificationCandidateSource`, los repositorios de metadatos y de grupos de versiones, y el renombrado seguro.
- **Consentimiento de red:** la búsqueda remota sólo ocurre si existe un token de acceso, que se pone a mano en `AP_LOCALMEDIA_TMDB_TOKEN` y se quita igual de fácil. El artefacto no lleva ninguno, así que sin ese acto deliberado la identificación trabaja únicamente sobre la caché local y no abre ninguna conexión.

### La aplicación puede decir dónde vive

`AP_LOCALMEDIA_DATA_ROOT` nombra la carpeta donde la aplicación guarda base, ajustes, copias, arte y diagnósticos. Se lee una sola vez al arrancar y un valor en blanco equivale a no ponerla.

Se añade aquí porque el recorrido físico lo pidió y T40 lo necesita. Redirigir `LOCALAPPDATA` **no** redirige nada: .NET resuelve la carpeta con `SHGetFolderPath` y no lee esa variable, así que un recorrido «aislado» escribe igualmente en la carpeta real de quien lo ejecuta. Sin esta variable, la comprobación de ciclo de vida de T40 —instalar, arrancar, actualizar, reparar, desinstalar— sólo puede hacerse en una VM limpia, y en este equipo no hay ninguna. Con ella, cada ciclo corre sobre su propia carpeta y no destruye la de nadie.

### Estados

Ningún identificador se degrada, como decidió el ADR. `PLY-001` y `PRD-005` siguen `IN_PROGRESS`: el primero por los bloqueos de hardware de C4 (`PLY-003` sin GPU integrada, `PLY-004` sin endpoint 5.1/7.1) y el segundo porque su SBOM se produce en T40.

### Cobertura

| Archivo nuevo | Cobertura |
|---|---|
| `CatalogMetadataRepository.cs` | 100 % |
| `MediaVersionGroupRepository.cs` | 100 % |
| `MetadataCandidateSource.cs` | 98,7 % |
| `PlayerSurfaces.cs` | 100 % |
| `ShellSurfaces.cs` | 100 % |
| `CreditsView.axaml.cs` | 100 % |

Archivos modificados: `ShellViewModel.cs` 100 %, `MediaFileRepository.cs` 98,6 %, `RootOnboardingViewModel.cs` 98,8 %, `PrivacySettingsViewModel.cs` 96,6 %, `ShellView.axaml.cs` 96,6 %. `CompositionRoot.cs` queda en 56,4 %: lo que no se ejercita es el arranque de la ventana real —bandeja, cierre a bandeja, activación por «Abrir con…»— que sólo ocurre con la aplicación en marcha y se comprueba en el recorrido físico y en T35.

### Verificación

- `dotnet format --verify-no-changes`: sin cambios.
- Debug y Release con `-warnaserror`: 0 avisos, 0 errores.
- Suite completa en Release: **1156 pruebas, 0 fallos, 0 omitidas**.
- `eng/verify.ps1 -Configuration Release -Runtime win-x64`: correcta.
- `eng/verify-docs.ps1`: 62 Markdown, 53 IDs, 46 MVP.
- `eng/run-accessibility.ps1 -Mode Verify -Passes 2`: 0 críticos, 0 mayores, 0 menores.
- `eng/run-recovery.ps1 -Mode Verify -Passes 2`: 9 filas, dos pasadas iguales.
- `eng/run-performance.ps1`: 12 de 12 métricas en presupuesto.

---

## English

### What was closed

ADR-0003 measured fourteen surfaces that were built, tested, and evidenced yet **unreachable from the application**, and a composition root that registered neither `IMetadataProvider` nor `ArtworkCache`. T39B wires them. No new surface was created: each one already existed with its own suite and its own evidence; what was missing was anyone asking for it.

### Reachability map, before and after

Reachability is measured as in the ADR: a surface is reachable when `ShellView` instantiates it, or when another reachable surface or its code-behind does. The two roots are the two controls the composition root can hand to the main window: the shell, and the recovery screen.

| Surface | ID | Before | After | Where it lives now |
|---|---|---|---|---|
| `RootOnboardingView` | `LIB-001` | no | yes | Library, above the catalogue |
| `ScanSettingsView` | `LIB-002` | no | yes | Settings |
| `ReviewInboxView` | `LIB-007` | no | yes | Review |
| `DuplicateReviewView` | `LIB-008` | no | yes | Review, when a card asks for its versions |
| `MetadataEditorView` | `LIB-011` | no | yes | Library, from the open card |
| `RenamePreviewView` | `LIB-012` | no | yes | Library, from the open card |
| `PlayerView` | `PLY-001` | no | yes | Session layer, opened from a card |
| `AudioOutputView` | `PLY-004` | no | yes | Session panel |
| `TrackSelectorView` | `PLY-005` | no | yes | Session panel |
| `ResumePromptView` | `PLY-008` | no | yes | Over the player |
| `MarkerEditorView` | `PLY-012` | no | yes | Session panel |
| `ShortcutSettingsView` | `PLY-014` | no | yes | Settings |
| `CreditsView` | `PRD-005` | no | yes | Settings |
| `SubtitleStyleView` | `A11Y-002` | no | yes | Settings |

The seven surfaces that travelled with the fourteen and were not listed separately are reachable too: `CandidateCardView`, `TransportControlsView`, `VideoStatusOverlay`, `SkipMarkerButton`, `NextEpisodeOverlay`, `VersionSwitchDialog`, `LooseFileBanner`, and `MiniPlayerWindow`.

`CreditsView` had neither `x:Class` nor an accessible name: it was a fragment no surface could host. It is now a named surface, and the TMDB attribution and the GPL-3.0-or-later licence can be read in Settings. That is a licence condition, not decoration.

### The gate is a test, not a checklist

[`SurfaceReachabilityTests`](../../../tests/ApSolutions.LocalMedia.UiTests/Shell/SurfaceReachabilityTests.cs) enumerates the presentation project's surfaces, builds the graph from the two roots, and demands that each one be reachable. It has two levels:

1. the fourteen the product declares, named one by one with their ID, so a failure says which commitment is not in the application;
2. the ADR's structural rule: **no surface carrying an `AutomationProperties.Name` may be left out**. A future orphan is a failing suite rather than a manual discovery.

RED archived under `artifacts/test-results/T39B/red/`: **18 failures, 0 passes**, naming the fourteen one at a time. GREEN under `artifacts/test-results/T39B/green/`.

### What physical verification found

Tests build the surface; the application assembles it. Those are two different questions, and the second found **five defects no headless test could see**. All are closed in this commit, each with its own test:

1. **Consenting to the first scan scanned nothing.** The surface asked and recorded the answer; nothing acted on it, so a new install added a folder and stayed empty forever. The shell now starts the scan and reloads the library when it finishes.
2. **Adding a folder twice closed the application.** `AddLibraryRoot` refuses duplicates and nested roots by throwing; the command let it escape to the dispatcher and the process died. The refusal is now a sentence on screen, with an accessible name and `LiveSetting="Assertive"`.
3. **A scanned, unidentified file opened on the series card** with no episodes, so it offered nothing to play. A title nobody has identified is one file with one path: it now opens the single-title card, which knows how to play it.
4. **Choosing a track neither applied nor stored it.** The list changed its label and the media stayed as it was.
5. **The session never fed the progress tracker.** It played and nothing was written, so the resume offer never came back.

### The walk, on the real application

UI Automation over the published executable, window maximised, `Invoke` pattern rather than clicks, data folder emptied first. Twenty-five steps, **twenty-five green**: launch, Library, type the path, add the folder with no refusal, consent to the first scan, apply and list (2 items), Review and its inbox, back and open the card, play from the card, player plus tracks plus audio output plus markers on screen, change the audio track, close the player, mark a favourite, Settings and the credits, and a clean exit. LibVLC decoded through D3D11VA on the discrete GPU during the walk.

**What reached storage** after closing, read from `library.db`: 1 library root, 2 media files, 1 personal state, 1 playback preference, 1 watch state.

### Privacy: withdrawing consent deletes the report

Withdrawing diagnostics consent cleared the screen and left the exported report on disk. `CreateDiagnostics.DiscardAsync` now deletes the report and its folder, and the privacy screen calls it when the switch goes off. An empty folder is still the trace of a report nobody consented to keep, so the folder goes too.

### Schema and composition

- Migration `0015_catalog_metadata_versions`: `catalog_metadata`, `media_version_groups`, and `media_version_group_members`. Without them the metadata editor and the version comparison had nowhere to write. `SqliteBootstrapTests` now fixes fifteen migrations.
- `CompositionRoot` registers `IMetadataProvider` (TMDB), `ArtworkCache`, `IIdentificationCandidateSource`, the metadata and version-group repositories, and safe rename.
- **Network consent:** the remote search runs only when an access token exists, placed by hand in `AP_LOCALMEDIA_TMDB_TOKEN` and removed just as easily. The artifact ships none, so without that deliberate act identification works from the local cache alone and opens no connection.

### The application can be told where it lives

`AP_LOCALMEDIA_DATA_ROOT` names the folder the application keeps its database, settings, backups, artwork, and diagnostics in. It is read once at startup and a blank value is the same as not setting it.

It is added here because the physical walk asked for it and T40 needs it. Redirecting `LOCALAPPDATA` does **not** redirect anything: .NET resolves the folder through `SHGetFolderPath` and never reads that variable, so an "isolated" walk still writes into the real folder of whoever runs it. Without this variable, T40's lifecycle check — install, launch, upgrade, repair, uninstall — can only run on a clean virtual machine, and there is none on this hardware. With it, each cycle runs against its own folder and destroys nobody's.

### Statuses

No identifier is downgraded, as the ADR decided. `PLY-001` and `PRD-005` remain `IN_PROGRESS`: the first for C4's hardware blocks (`PLY-003` with no integrated GPU, `PLY-004` with no 5.1/7.1 endpoint) and the second because its SBOM is produced in T40.

### Coverage

New files: `CatalogMetadataRepository.cs` 100%, `MediaVersionGroupRepository.cs` 100%, `MetadataCandidateSource.cs` 98.7%, `PlayerSurfaces.cs` 100%, `ShellSurfaces.cs` 100%, `CreditsView.axaml.cs` 100%. Changed files: `ShellViewModel.cs` 100%, `MediaFileRepository.cs` 98.6%, `RootOnboardingViewModel.cs` 98.8%, `PrivacySettingsViewModel.cs` 96.6%, `ShellView.axaml.cs` 96.6%. `CompositionRoot.cs` stands at 56.4%: what is not exercised is real window startup — tray, close-to-tray, "Open with…" activation — which only happens with the application running and is checked by the physical walk and by T35.

### Verification

`dotnet format --verify-no-changes` clean; Debug and Release with `-warnaserror` at 0 warnings and 0 errors; full Release suite **1156 tests, 0 failures, 0 skipped**; `eng/verify.ps1 -Configuration Release -Runtime win-x64` passed; `eng/verify-docs.ps1` 62 Markdown files, 53 IDs, 46 MVP; accessibility over two passes 0/0/0; recovery over two passes 9 rows, identical; performance 12 of 12 metrics within budget.

---

> **Nota (2026-08-08).** Acta anterior a T44: la afirmación «no abre ninguna conexión» describía la
> aplicación sin actualizador. Desde T44 existen dos destinos más (`api.github.com`, `github.com`),
> opcionales y desactivados de fábrica; la tabla vigente vive en
> [PRIVACY.es.md](../../privacy/PRIVACY.es.md).
>
> **Note (2026-08-08).** This record predates T44: the claim "opens no connection" described the
> application before the updater. Since T44 there are two more destinations (`api.github.com`,
> `github.com`), optional and off by default; the current table lives in
> [PRIVACY.en.md](../../privacy/PRIVACY.en.md).
