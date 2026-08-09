# T31 — Favoritos, ver más tarde y valoración / Favorites, watch later, and rating

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `b6123ac`
- Commit de tarea / Task commit: `feat: save local favorites watch later and ratings`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1, SQLite en WAL
- IDs: `UX-005=IMPLEMENTED` (cierra con la exportación de T36 / closes with the T36 export),
  `DAT-002=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`PersonalStateTests`, `PersonalStateWorkflowTests`, `PersonalActionsTests` y
`PersonalStateRepositoryTests` se escribieron antes que el modelo, la política, los dos casos de uso,
el repositorio, la migración y la superficie. RED falló porque los espacios de nombres
`Domain.Personalization` y `Application.Personalization` no existían, ni tampoco `PersonalState`,
`PersonalStatePolicy`, `IPersonalStateRepository`, `SetPersonalState`, `GetPersonalFilters`,
`PersonalStateRepository`, `PersonalActionsViewModel`, `PersonalActionRequest`, los filtros
`CatalogFilter.Favorite|WatchLater|Rated` ni la tabla `personal_state`. La salida está en
`artifacts/test-results/T31/red/build.log`. / The four test files were written first and RED failed on
the missing namespaces, types, filters, and table.

El ViewModel de esta tarea tiene prueba desde el ciclo RED. / The view model was covered from RED.

GREEN ejecuta **701 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T31/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias. La suite pasó de **644** a **701**. / GREEN runs 701
tests with zero failures and zero skips; the suite grew by 57.

## Adaptación del número de migración / Migration number adaptation

El plan nombra `0011_personal_state.sql`, pero `0011` es `watch_state` desde T25 y `0012` es
`intro_markers` desde T29. Esta tarea usa **`0013_personal_state.sql` con `"version": 13`**, el número
que el propietario reservó, con el `sha256` calculado sobre el texto UTF-8 con `LF` y sin BOM.

Con esto se cierra el hueco que T30 dejó al ocupar el `0014`: el manifiesto vuelve a ser contiguo,
`1`–`14`. Una base migrada por T30 recibe la `0013` después de la `0014`, que es exactamente lo que
`MigrationRunner` hace al aplicar por número las versiones que faltan. `SqliteBootstrapTests` se
actualizó a 14 migraciones, `MAX(version) = 14`, 14 copias previas y la tabla `personal_state`. / The
plan's number was taken twice over, so this task uses thirteen, which also closes the gap T30 left; the
manifest is contiguous again and the bootstrap test was updated in the same commit.

## Reglas verificadas / Verified rules

| Regla / Rule | Comportamiento verificado / Verified behaviour |
|---|---|
| Rango de valoración / Rating range | `1`–`10`; `0`, `-1`, `11`, `int.MaxValue` e `int.MinValue` se **rechazan**, no se limitan |
| Sin valoración / No rating | `null` es un valor legítimo y distinto de cero |
| Idempotencia / Idempotence | fijar el mismo valor dos veces devuelve el mismo estado y deja **una** fila |
| Alternar / Toggling | alterna y, al hacerlo dos veces, vuelve al punto de partida |
| Independencia / Independence | las tres marcas no se pisan entre sí en ningún orden |
| Fila vacía / Empty row | quitar la última marca **borra** la fila; el esquema también lo impide con un `CHECK` |
| Relectura / Re-read | cada escritura relee la fila almacenada, como hace el progreso desde T26 |
| Almacenamiento ausente / Unavailable store | el fallo sale a la superficie; no se finge un guardado |
| Contenido, no archivo / Content, not file | la clave es `ContentKey`; un episodio y su serie tienen filas separadas |

## Mil cambios aleatorios contra un modelo de referencia / A thousand random changes against a reference model

Con semilla fija `20260803` se aplican **1.000** operaciones aleatorias —marcar, desmarcar, valorar,
quitar valoración y alternar— sobre 39 contenidos en memoria y sobre 59 contenidos en SQLite. En ambos
casos el estado final coincide **elemento a elemento** con un modelo de referencia independiente. En la
prueba sobre SQLite, además, se limpian las conexiones y se vuelve a leer con un repositorio nuevo: el
resultado sobrevive al reinicio sin una sola diferencia. / A thousand seeded random operations match an
independent reference model exactly, in memory and in SQLite, and the SQLite result survives a restart.

## Filtros y concurrencia / Filters and concurrency

`CatalogQuery` gana tres banderas: `Favorite`, `WatchLater` y `Rated`. El flag `Personal` anterior
conserva su significado sobre `titles.is_personal` para no alterar consultas ya verificadas.

**Una marca sobre un episodio no arrastra a su serie**: los tres filtros de título miran sólo filas con
`episode_id IS NULL`, comprobado con una serie cuyo episodio es favorito y que, correctamente, no
aparece bajo el filtro de favoritos.

Marcar veinte títulos mientras se ejecutan veinte búsquedas concurrentes deja las veinte filas intactas
y el filtro devuelve exactamente veinte. / The three filters ignore episode-level marks, and marking
twenty titles while twenty searches run concurrently leaves every row intact.

## Superficie / Surface

Los tres controles tienen nombre de automatización, aceptan foco de teclado y **anuncian su estado en
palabras**: «Está en favoritos» frente a «No está en favoritos», «Guardado para más tarde» frente a
«No guardado para más tarde», y la valoración o «Sin valorar». Ninguna de las tres es una diferencia
sólo de color. Una valoración fuera de `1..10`, o que no sea un número, **no llega al host**: el comando
la rechaza antes. Ningún atributo de texto de la vista es un literal. / Every control is named,
focusable, and states itself in words; an invalid rating never reaches the host.

## Ningún perfil y ninguna lista / No profile and no list

- Ningún tipo del espacio de nombres `Domain.Personalization` contiene `Profile`, `Collection`,
  `Playlist` ni `CustomList`; lo comprueba una prueba por reflexión sobre el ensamblado.
- Una búsqueda en todo `src/` no encuentra ninguna de esas clases.
- `UX-007` (listas personalizadas) sigue `DEFERRED` a `POST_STABLE` y esta tarea no lo toca. El modelo
  admite añadirlas después sin migración destructiva, que es justamente lo que la matriz promete.

/ No profile or list type exists, and the deferred custom-lists identifier is untouched.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines | Ramas / Branches |
|---|---:|---:|
| `Domain/Personalization/PersonalState.cs` (modelo y política / model and policy) | 20/20 — 100 % | 16/16 — 100 % |
| `Application/Personalization/SetPersonalState.cs` | 22/22 — 100 % | 10/10 — 100 % |
| `Application/Personalization/GetPersonalFilters.cs` | 13/13 — 100 % | 10/10 — 100 % |
| `Infrastructure/Data/Repositories/PersonalStateRepository.cs` | 72/75 — 96,00 % | 18/20 — 90,00 % |
| `Presentation/Catalog/PersonalActionsViewModel.cs` | 66/66 — 100 % | 25/30 — 83,33 % |
| **Total del código nuevo / New code total** | **193/196 — 98,47 %** | **79/86 — 91,86 %** |

Las ramas de la política de dominio están al **100 %**. / Full branch coverage on the domain policy.

## Privacidad y límites / Privacy and boundaries

- **Sin red ni telemetría**: no hay `HttpClient`, `WebRequest`, `Socket`, `WebClient` ni resolución de
  nombres en ninguno de los cinco archivos nuevos; comprobado por búsqueda sobre el código de la tarea.
- **Sin rutas ni nombres de archivo**: la tabla guarda `content_key`, `title_id`, `episode_id`, las tres
  marcas y un sello de escritura. Nada más.
- **Sin operaciones destructivas sobre medios**: ningún `File.Delete`, `File.Move` ni escritura sobre
  archivos multimedia.
- **Artefactos ignorados**: `git status` no incluye `artifacts/` ni ningún archivo multimedia.
- **Sin datos personales versionados**: ningún archivo tocado contiene nombre de usuario, nombre de
  equipo ni ruta absoluta local.

/ No network, no telemetry, no paths stored, no media writes.

`UX-005` pasa a `IMPLEMENTED` y no más allá: persiste y filtra, pero su criterio de aceptación exige
además que entre en la copia y la exportación, y eso es T36. Decir `VERIFIED` ahora afirmaría un
trabajo que todavía no se ha hecho. / The identifier moves to implemented and no further, because its
acceptance criterion also requires the backup and export that T36 delivers.

## Corrección posterior / Later correction

Esta tarea colocó las tres marcas sólo en la ficha de película. El plan pide «acciones en
fichas/tarjetas», en plural, y dejar la ficha de serie sin ellas habría hecho que la auditoría de T33
recorriera una superficie incompleta. Se corrigió antes de empezar T33, en el commit
`fix: put the same personal marks on the series card`, que añade `PersonalActions` a
`ShowDetailsViewModel`, lo muestra en `ShowDetailsView` y enruta ambas fichas por el mismo camino en
`CompositionRoot`, de modo que un favorito significa lo mismo en una película y en una serie. / This
task put the three marks only on the film card; the gap was closed before T33 started so the audit
would not walk an incomplete surface.
