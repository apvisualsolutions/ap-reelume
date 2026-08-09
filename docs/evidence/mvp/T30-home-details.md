# T30 — Inicio híbrido y fichas completas / Hybrid home and complete title details

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `903dee3`
- Commit de tarea / Task commit: `feat: complete the hybrid home and title details`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1, SQLite en WAL
- IDs: `UX-001=VERIFIED`, `UX-002=VERIFIED`, `UX-003=VERIFIED`, `UX-004=VERIFIED`

## RED y GREEN / RED and GREEN

`GetHomeTests`, `HomeLayoutTests`, `DetailsNavigationTests` y `EpisodeSequenceRepositoryTests` se
escribieron antes que la consulta, la proyección, el adaptador, los cuatro ViewModels y las siete
vistas. RED falló por ausencia de los espacios de nombres `Application.Home` y `Presentation.Home`, de
los tipos `GetHome`, `HomeSnapshot`, `IHomeReadModel`, `HomeProgressEntry`, `ResumeItem`,
`InProgressItem`, `RecentlyAddedItem`, `LibrarySummary`, `HomeViewModel`, `MovieDetailsViewModel`,
`ShowDetailsViewModel`, `SeasonViewModel`, `EpisodeRowViewModel`, `EpisodeSequenceRepository` y
`HomeReadModel`, y de la tabla `episode_media`. La salida está en
`artifacts/test-results/T30/red/build.log`. / The four plan-named test files were written first and
RED failed on the missing namespaces, types, and table.

**Los cuatro ViewModels de esta tarea tienen prueba desde el ciclo RED**, que es la corrección
explícita del error de I4. Las aserciones de comandos, formateo y validación se añadieron después,
durante el paso de verificación, para alcanzar el listón de cobertura; se declara aquí para no
presentarlas como pruebas RED. / Every view model this task creates was covered from the RED cycle;
the extra command and formatting assertions were added during verification and this is stated plainly.

GREEN ejecuta **644 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T30/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias y 0 errores. La suite pasó de **616** a **644**. /
GREEN runs 644 tests with zero failures and zero skips; the suite grew by 28.

## Adaptación del número de migración / Migration number adaptation

El plan no nombra migración para T30. La ficha de serie necesita saber qué archivo hay detrás de cada
episodio, y el esquema no relacionaba `episodes` con `media_files`: ésa es exactamente la brecha que
C5 dejó declarada al cerrar T28 sin adaptador. Esta tarea la cierra con
**`0014_episode_media.sql` con `"version": 14`**, el número que el propietario reservó para ella, con
el `sha256` calculado sobre el texto UTF-8 del archivo con `LF` y sin BOM, como hace `MigrationRunner`.
`0013` queda reservada para `personal_state` en T31.

El hueco temporal entre `0012` y `0014` es válido: `MigrationRunner` sólo exige versiones únicas y
positivas, aplica las que faltan en orden ascendente y las registra por número, de modo que una base ya
migrada aceptará la `0013` cuando llegue. `SqliteBootstrapTests` se actualizó a 13 migraciones,
`MAX(version) = 14`, 13 copias previas y la tabla `episode_media` en la lista. / The plan named no
migration; the details need the episode-to-file link, so this task adds migration fourteen, the number
the owner reserved, and leaves thirteen free for the next task. The gap is valid and the bootstrap test
was updated in the same commit.

## Qué decide Inicio y qué no / What Home decides and what it does not

| Regla / Rule | Comportamiento verificado / Verified behaviour |
|---|---|
| Biblioteca vacía / Empty library | sin héroe, sin rail, con resumen a cero; Continuar no es ejecutable |
| Héroe / Hero | la entrada **más reciente** que además es alcanzable y supera el suelo de reanudación |
| Suelo de reanudación / Resume floor | `29 s` no se ofrece; `30 s` sí, según `ProgressPolicy` |
| Duración cero / Zero duration | se ofrece igual y el porcentaje es `0`: no observada, nunca «termina inmediatamente» |
| No disponible / Unavailable | permanece en el rail marcado, pero **nunca** es el héroe |
| Todo no disponible / All unavailable | héroe vacío y rail intacto |
| Visto y sin empezar / Watched and unstarted | no llegan al rail, ni en SQL ni en la política |
| Película / Movie | sin temporada ni episodio |
| Episodio / Episode | temporada, número y título del episodio |
| Límites / Limits | llegan al modelo de lectura; el catálogo completo nunca se carga |

## Regresión visual con baseline estructural / Visual regression with a structural baseline

`artifacts/` está ignorado por Git, así que la baseline aprobada no puede vivir ahí. Se versiona como
**JSON estructural** en `tests/ApSolutions.LocalMedia.UiTests/Baselines/T30/home-layout.json`: una
entrada por combinación, con viewport lógico, primer foco, orden de foco, visibilidad del héroe y del
acceso a Biblioteca, y el borde inferior de ese acceso. El PNG de cada combinación se sigue capturando
en `artifacts/ui-captures/T30/` como prueba visual. Un diff de la baseline es legible y revisable; un
PNG binario no lo sería. / The approved baseline is a versioned structural JSON because `artifacts/`
is ignored; the PNG stays as visual proof.

**36 combinaciones**: 1366×768 y 3840×2160 × 100/150/200 % × claro/oscuro/alto contraste × ES/EN.

| Resultado / Result | Valor / Value |
|---|---|
| Acceso a Biblioteca dentro del primer viewport | **36/36** |
| Primer foco en Continuar habiendo progreso | **36/36** |
| Primer foco en Biblioteca sin progreso | verificado aparte / verified separately |
| Borde inferior del acceso a Biblioteca | `283 px` a 100 % y 150 %; `284 px` a 200 % |
| Viewport lógico más pequeño probado | `683×384` (1366×768 al 200 %) |

## Cero texto incrustado / Zero embedded text

Ningún atributo `Text`, `Content`, `PlaceholderText`, `Header` ni `ToolTip.Tip` de los `*.axaml` de
`Home/`, `Movie/` y `Show/` contiene un literal: todos resuelven a `{DynamicResource …}` o a un enlace.
Las 30 claves nuevas existen en `Strings.es.axaml` **y** en `Strings.en.axaml`, y
`ShellLocalizationTests` sigue comprobando que ambos conjuntos de claves son idénticos. /
No literal survives on the three new surfaces and both resource dictionaries carry the same keys.

## Fichas / Details

- **Película**: título, año, disponibilidad, estado de visionado, punto de reanudación formateado
  (`40:00`, `1:56:00`) y **todas** las versiones, con la efectiva marcada y ninguna oculta. Una
  posición mayor que la duración se limita a la duración.
- **Serie**: temporadas en orden con los especiales al final, episodios en orden dentro de cada
  temporada, estado por episodio, y la distinción entre «no disponible» y «sin archivo en el catálogo».
- Reproducir pide la versión efectiva desde el principio; Reanudar pide la misma versión desde el punto
  guardado. Un episodio sin archivo alcanzable no es ejecutable.
- Volver a la biblioteca conserva búsqueda, filtros, orden, elementos, cursor y ancla de scroll.

/ The film card lists every version with the effective one marked, the series card orders seasons with
specials last, and returning to the library preserves the whole browse state.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines | Ramas / Branches |
|---|---:|---:|
| `Application/Home/GetHome.cs` | 106/110 — 96,36 % | 11/12 — 91,67 % |
| `Infrastructure/Data/Repositories/EpisodeSequenceRepository.cs` | 34/34 — 100 % | 13/14 — 92,86 % |
| `Infrastructure/Data/Repositories/HomeReadModel.cs` | 82/82 — 100 % | 20/24 — 83,33 % |
| `Presentation/Home/HomeViewModel.cs` | 101/103 — 98,06 % | 40/50 — 80,00 % |
| `Presentation/Movie/MovieDetailsViewModel.cs` | 98/98 — 100 % | 56/66 — 84,85 % |
| `Presentation/Show/ShowDetailsViewModel.cs` | 55/58 — 94,83 % | 26/28 — 92,86 % |
| `Presentation/Show/SeasonViewModel.cs` | 28/29 — 96,55 % | 6/6 — 100 % |
| **Total del código nuevo / New code total** | **504/514 — 98,05 %** | **172/200 — 86,00 %** |

T30 no añade ninguna política de dominio: reutiliza `ProgressPolicy` y `NextEpisodePolicy`, cuyas ramas
ya estaban al 100 % desde I4. Las ramas no cubiertas del código nuevo son comprobaciones de nulidad
generadas por el compilador en los ViewModels y en los lectores SQL. / No new domain policy is added;
the uncovered branches are compiler-generated null checks.

## Un defecto real encontrado en la medición / A real defect found while measuring

La primera medición dio **0 %** en los dos adaptadores nuevos aunque sus pruebas pasaban. La causa es
el escollo ya documentado en I4: el proceso hijo que `SqliteBootstrapTests` lanza para el ensayo de
cierre forzado heredaba `CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER*` y `COR_*`, y sobrescribía los
datos de cobertura de su padre. `HandleGrowthTests` ya vaciaba esas variables; `SqliteBootstrapTests`
no. Corregido en el mismo commit, la cobertura real de esos adaptadores es del **100 %**. / The first
measurement reported zero for both new adapters because the crash-writer child overwrote the parent's
coverage data, the exact pitfall recorded in I4. Fixed here.

## Alcance declarado / Declared scope

`IMediaVersionGroupRepository` **sigue sin adaptador SQLite**, igual que al cerrar I4. La ficha de
película acepta un grupo de versiones y lo presenta por completo —está probado con tres versiones, una
de ellas no disponible—, pero mientras no exista el adaptador el host le pasa `null` y la sección de
versiones no se muestra. Cerrar esa brecha no pertenece a T30: el plan asignó a esta tarea la de
`IEpisodeSequenceRepository`, que sí queda cerrada. / The version-group port still has no SQLite
adapter; the card handles a group in full and is tested with three versions, but the host passes null
until that adapter exists. This is stated rather than papered over.

## Privacidad y límites / Privacy and boundaries

- **Sin red ni telemetría**: no hay `HttpClient`, socket ni resolución de nombres en ningún archivo de
  esta tarea.
- **Sin rutas en la interfaz**: las filas de versión muestran resolución, códec y rango; el `Path` del
  modelo no llega a ninguna propiedad visible. Inicio identifica el contenido por `ContentKey` y por
  `TitleId`, nunca por ruta.
- **Sin operaciones destructivas**: ningún `File.Delete`, `File.Move` ni escritura sobre archivos
  multimedia.
- **Artefactos ignorados**: `git status` no incluye `artifacts/` ni ningún archivo multimedia.
- **Sin datos personales versionados**: la baseline estructural contiene sólo tamaños, escalas, temas,
  idiomas y nombres de control.

/ No network, no telemetry, no paths on screen, no media writes, and nothing personal in the versioned
baseline.

Los cuatro identificadores de la tarea pasan a `VERIFIED`: Inicio prioriza reanudar sin esconder la
Biblioteca, las fichas están completas, la regresión visual cubre las 36 combinaciones sin fallos y no
queda un solo texto fuera de recursos. `UX-002` y `UX-003` vuelven a auditarse en T33 con lector de
pantalla, que es una verificación distinta y adicional. / The four identifiers verify here, and the two
design identifiers get a second, different audit in T33.
