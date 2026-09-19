# LIB-021, plan 2 de 3: el fotograma del propio vídeo, cuando no hay otra portada

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** que una película, serie o archivo sin identificar que no tiene portada elegida ni del proveedor muestre un fotograma de su propio vídeo, sacado solo, una vez, y guardado.

**Architecture:** `CoverOrigin` gana `Frame`, al final del orden. Una pasada de fondo, `CaptureTitleFrames`, calcada de `RefreshStaleMetadata` (cede ante un escaneo o una reproducción, va por tandas), pide a un puerto nuevo la lista de títulos con su vídeo fuente y captura con el capturador que ya existe, renombrado a lo que es: `IVideoFrameGrabber`. El archivo vive en `cache/title-frames/<id:N>.png` con su marca `.from`, y reutiliza la decisión de `CourseThumbnailPolicy` (sacar, conservar o imposible). `ResolveTitlePoster` sólo **encuentra** el archivo; nunca decodifica. La pasada corre al arrancar y al acabar cada escaneo, y tras cada tanda que capturó algo recarga la cuadrícula.

**Tech Stack:** C# .NET 10, Avalonia 12, SQLite, LibVLC 3 por la ruta de callbacks, xUnit v3.

**Spec:** `docs/adr/0009-a-cover-has-three-origins-and-an-order.md` (decisión 3 y 5, y la consecuencia «la aplicación abrirá vídeos por su cuenta»); plan 1 en `2026-09-18-lib-021-cover-origins-storage.md`.

## Decisiones tomadas aquí, con su porqué

- **Una pasada de fondo y no una captura al pintar la tarjeta.** La cuadrícula construye sus tarjetas de forma síncrona y decodificar es lento (hasta 3 s por archivo, y 4,5 s en rendirse con uno ilegible, medido en el spike de `CRS-006`). Pedirlo al pintar bloquearía o llenaría la cuadrícula de tareas sueltas.
- **Tandas de 25 y recarga tras cada una que capturó algo.** La cuadrícula no escucha ningún evento; recargarla tras cada fotograma la haría parpadear en una biblioteca grande, y esperar al final dejaría una biblioteca nueva sin imágenes durante minutos.
- **Serie: el primer episodio no especial y disponible.** Un especial (temporada 0) suele ser un extra y no representa la serie.
- **Caché, no copia de seguridad.** Se regenera; `BackupContentPolicy` ya rechaza todo `cache/*`, como con las miniaturas de los cursos.
- **Renombrar el puerto, no duplicarlo.** La ADR dice que el capturador sirve para cualquier vídeo y que lo de cursos es sólo el envoltorio; dos puertos iguales con dos nombres serían dos sitios que mantener.

## Global Constraints

- Los mismos del plan 1. Además: **nada de LibVLC se abre con una ruta que no pase `MediaFileExtensions.IsApproved`** (ya lo hace el adaptador, y no se toca); **una sola instancia nativa** (`NativeInstanceOwnershipTests`); **todo servicio registrado tiene quien lo resuelva** (`ServiceConsumptionTests`); regla 10: lo que habla con la máquina se separa de lo que decide.

---

### Task 1: el capturador se llama por lo que es

**Files:** renombrar `ICourseFrameGrabber` → `IVideoFrameGrabber` (moverlo a `src/ApSolutions.LocalMedia.Application/Playback/IVideoFrameGrabber.cs`), `LibVlcCourseFrameGrabber` → `LibVlcVideoFrameGrabber` (mismo directorio, archivo renombrado con `git mv`), y sus usos: `GetCourseThumbnail.cs`, `GetCourseThumbnailTests.cs`, `CourseFrameGrabberTests.cs` → `VideoFrameGrabberTests.cs`, y cualquier otra mención (`grep -rn "CourseFrameGrabber" src tests docs eng`). `eng/coverage-debt.txt` no lo nombra (verificar).

- [ ] **Step 1:** `git mv` de los dos archivos y del de pruebas; renombrar tipos con la herramienta de edición; el `<remarks>` del puerto dice que sirve a cursos y a portadas (ADR-0009, decisión 5).
- [ ] **Step 2:** compilar la solución con `-warnaserror`; `Application.Tests` y `MediaTests --filter VideoFrameGrabberTests` verdes. Es un renombrado: nada cambia de comportamiento, y por eso no lleva prueba nueva.
- [ ] **Step 3:** Commit: `refactor: LIB-021, el capturador de fotogramas sirve a cualquier vídeo y se llama así`.

### Task 2: el tercer origen en el dominio y en la resolución

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Domain/Metadata/CoverOrderPolicy.cs`
- Modify: `src/ApSolutions.LocalMedia.Application/Storage/IAppDataPaths.cs` y `src/ApSolutions.LocalMedia.Windows/AppDataPaths.cs` (`TitleFrameDirectory` = `cache/title-frames`)
- Modify: `src/ApSolutions.LocalMedia.Application/Metadata/ResolveTitlePoster.cs`
- Test: `CoverOrderPolicyTests.cs`, `ResolveTitlePosterTests.cs`, y los dobles de `IAppDataPaths` que haya en las pruebas (`grep -rln ": IAppDataPaths" tests`).

**Interfaces:**
- Produces: `CoverOrigin.Frame`; `CoverOrderPolicy.Default = [Personal, Provider, Frame]`; `IAppDataPaths.TitleFrameDirectory`; `ResolveTitlePoster(IArtworkStore artwork, IAppDataPaths paths)`; `static string ResolveTitlePoster.FrameFileFor(IAppDataPaths paths, TitleId id)` = `Path.Combine(paths.TitleFrameDirectory, id.Value.ToString("N") + ".png")`.

- [ ] **Step 1: pruebas que fallan**
  - `CoverOrderPolicyTests.The_hand_picked_cover_wins_and_the_provider_follows` pasa a `..._then_the_frame`: `[Personal, Provider, Frame]`.
  - `ResolveTitlePosterTests`: con un directorio temporal como `TitleFrameDirectory` y el `.png` creado, **sin** portada propia ni del proveedor → devuelve el fotograma; **con** la del proveedor en disco → devuelve la del proveedor y no mira el fotograma; sin archivo de fotograma → `null`.
- [ ] **Step 2:** rojo.
- [ ] **Step 3:** el enum gana `Frame` con su `<summary>` («un fotograma del propio vídeo, sacado por la aplicación cuando no hay otra»); el orden lo añade al final. `ResolveTitlePoster` recibe `IAppDataPaths`; el recorrido pasa de ternario a `switch` con los tres brazos y **sin** brazo por defecto inalcanzable (un `_ => null` no se puede tomar y baja las ramas; usar la expresión `switch` exhaustiva sobre un enum con los tres valores y dejar que el compilador avise). La rama `Frame` es `File.Exists(FrameFileFor(...)) ? path : null`.
- [ ] **Step 4:** verdes `Domain.Tests`, `Application.Tests`, y la compilación de la solución (el registro en `CompositionRoot.Identification.cs:136` es `AddTransient<ResolveTitlePoster>()` y resuelve el segundo parámetro solo).
- [ ] **Step 5:** Commit: `feat: LIB-021, el fotograma es el tercer origen y la resolución lo encuentra`.

### Task 3: de dónde sale el vídeo de cada título

**Files:**
- Create: `src/ApSolutions.LocalMedia.Application/Metadata/ITitleFrameSources.cs`
- Create: `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/TitleFrameSourceRepository.cs`
- Test: `tests/ApSolutions.LocalMedia.IntegrationTests/Metadata/TitleFrameSourceRepositoryTests.cs`

**Interfaces:**
- Produces:

```csharp
/// <summary>A title's video to take a frame from, and what identifies that file's current state.</summary>
public sealed record TitleFrameSource(TitleId TitleId, string VideoPath, TimeSpan? Duration, CourseThumbnailStamp Stamp);

public interface ITitleFrameSources
{
    /// <summary>
    /// Every film, series and unidentified file with no picked cover and no provider poster, with the
    /// video its frame comes from. A series answers with its first available non-special episode.
    /// </summary>
    Task<IReadOnlyList<TitleFrameSource>> ListWithoutCoverAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 1: pruebas que fallan** (sobre `MigratedSchemaTemplate`, sembrando con los repositorios que ya usan las pruebas de catálogo):
  - una película sin `catalog_metadata` sale con su `media_files.normalized_path`, `duration_ticks`, `size_bytes` y `last_write_utc`;
  - una película con `poster_path` **o** con `personal_cover` no sale;
  - una serie sale con la ruta del primer episodio de temporada ≥ 1 disponible, aunque exista un especial de temporada 0;
  - un archivo escaneado sin identificar sale; uno enlazado como episodio no sale dos veces;
  - un archivo no disponible no sale.
- [ ] **Step 2:** rojo.
- [ ] **Step 3:** la consulta, una `UNION ALL` de tres ramas (película, serie por `episode_media` con `ORDER BY season_number, episode_number` y `season_number > 0`, escaneado sin título ni episodio), filtrada por `NOT EXISTS (SELECT 1 FROM catalog_metadata m WHERE m.title_id = … AND (m.poster_path IS NOT NULL OR m.personal_cover IS NOT NULL))` y `is_available = 1`. Leer las columnas reales de `media_files` antes de escribirla (`0001_initial.sql` y las que la alteran): **no suponer nombres**.
- [ ] **Step 4:** verde `IntegrationTests --filter TitleFrameSourceRepositoryTests`. Commit: `feat: LIB-021, cada título sin portada sabe de qué vídeo sale su fotograma`.

### Task 4: la pasada que captura

**Files:**
- Create: `src/ApSolutions.LocalMedia.Application/Metadata/CaptureTitleFrames.cs`
- Test: `tests/ApSolutions.LocalMedia.Application.Tests/Metadata/CaptureTitleFramesTests.cs`

**Interfaces:**
- Consumes: `ITitleFrameSources`, `IVideoFrameGrabber`, `IAppDataPaths`, `IPlaybackActivity`, `IScanActivity`, `CourseThumbnailPolicy.Decide/SeekPosition`, `ResolveTitlePoster.FrameFileFor`.
- Produces: `Task<int> ExecuteAsync(Func<Task>? afterBatch = null, CancellationToken ct = default)` — devuelve cuántos capturó; `public const int BatchSize = 25;`.

- [ ] **Step 1: pruebas que fallan** (con dobles en memoria, como `GetCourseThumbnailTests`):
  - sin fuentes, no captura y no llama a `afterBatch`;
  - captura al 10 % de la duración (`SeekPosition`) en `FrameFileFor(...)` y escribe la marca `.from`;
  - un título con fotograma y marca que coincide no se vuelve a decodificar; con otro tamaño o fecha, sí;
  - con una reproducción en curso o un escaneo activo **antes de cada título**, se para y devuelve lo capturado hasta ahí;
  - un archivo que el capturador rechaza deja el título sin fotograma y la pasada sigue;
  - 30 fuentes → `afterBatch` se llama dos veces (25 y 5); si una tanda no capturó nada, no se llama.
- [ ] **Step 2:** rojo.
- [ ] **Step 3:** implementación. La marca `.from` se lee y escribe igual que en `GetCourseThumbnail` (`"{Length}|{ModifiedUtc:O}"` invariante); **extraer** esas dos funciones a un ayudante compartido en `Application/Playback/FrameStampFile.cs` y hacer que `GetCourseThumbnail` lo use, en vez de copiarlas (y sus pruebas siguen verdes sin tocarlas).
- [ ] **Step 4:** verde `Application.Tests`. Commit: `feat: LIB-021, una pasada de fondo saca el fotograma de los títulos sin portada`.

### Estado al 2026-09-18, al parar

Hechas las tareas 1 a 4 y dos piezas de la 5, **sin registrar nada**: `LibraryViewModel.RefreshPosters()`
(refresca las imágenes de lo ya cargado sin volver a consultar, con sus dos pruebas), el aviso
`scanFinished` de `IdentifyingScanCoordinator` (con su prueba de orden, vista fallar) y la guarda de
`CaptureTitleFrames` contra dos pasadas a la vez (con una prueba que falla limpia en vez de colgarse:
su primera versión se colgó). **Se decidió cambiar la recarga**: no `LoadAsync`, que vuelve a la
primera página y devuelve arriba a quien esté recorriendo la cuadrícula, sino `RefreshPosters()`.

**Lo que paró la conexión, y hay que medir antes de hacerla.** Las pruebas de paseo
(`AssembledPhysicalWalkTests`, `AssembledJourneyTests`) montan la aplicación entera con
`ConfigureWindow`. Conectada, la pasada decodificaría vídeos reales en mitad de sus escenas y
`RefreshPosters` sustituiría las tarjetas mientras el paseo las pulsa, en un paseo que ya tiene rojos
sueltos (`ENG-026`). Tres preguntas, por este orden: ¿esos paseos pasan por `ConfigureWindow` y por el
aviso de fin de escaneo? (leer el código, no suponer); ¿sus bibliotecas tienen títulos sin portada, y
por tanto trabajo para la pasada? (medir); y si lo tienen, la salida que no esconde nada: que el paseo
espere a la pasada antes de pulsar, igual que ya espera a que acabe un escaneo, en vez de apagarla en
pruebas. **Una pasada que sólo se desactiva en pruebas es una pasada que ninguna prueba ejercita.**

### Estado al 2026-09-19: conectada

Las tres preguntas, contestadas: sólo dos escenas montan la ventana, pero todas las que escanean pasan
por el coordinador; y todas tienen trabajo para la pasada, porque el paseo no tiene proveedor. **Se
cambió la salida**: en vez de que el paseo espere, `RefreshPosters` ya no sustituye las tarjetas —
`CatalogItemViewModel.ShowPoster` cambia la imagen de la misma—, así que nada sale de debajo de una
pulsación. La recarga es `RefreshPosters` y no `LoadAsync`, como se decidió al parar. Evidencia en
`docs/evidence/stable/LIB021-cover-origins-frame.md`. `CRS-006` no cupo en la tanda y sigue en su fila
de la matriz.

### Task 5: conectarla, y que la cuadrícula lo vea

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Windows/CompositionRoot.Identification.cs` (registrar `ITitleFrameSources` → `TitleFrameSourceRepository`, `IVideoFrameGrabber` → `LibVlcVideoFrameGrabber` sobre el `LibVlcFactory` único, y `CaptureTitleFrames`)
- Modify: `src/ApSolutions.LocalMedia.Windows/CompositionRoot.cs` (lanzarla con `PostSafely` junto a `RefreshStaleMetadata`, con `afterBatch` = recargar `Shell.Library` en el hilo de la interfaz)
- Modify: `src/ApSolutions.LocalMedia.Application/Identification/IdentifyingScanCoordinator.cs` (un `Func<CaptureTitleFrames>?` opcional, invocado al final del encadenado, como los otros cinco pasos)
- Test: `ServiceConsumptionTests` (debe quedar verde sin lista pendiente), `NativeInstanceOwnershipTests`, `IdentifyingScanCoordinatorTests` (la pasada corre después de nombrar), y una escena en `AssembledPhysicalWalkTests` o `AssembledJourneyTests`: una película sin portada de proveedor muestra, tras la pasada, una `PosterFile` bajo `cache/title-frames`.

- [ ] **Step 1:** pruebas que fallan (coordinador y escena).
- [ ] **Step 2:** rojo.
- [ ] **Step 3:** registros y lanzamientos. La recarga tras una tanda llama a `LoadAsync` sólo si la biblioteca ya cargó algo (`Items.Count > 0`), para no competir con la carga inicial.
- [ ] **Step 4:** verdes `ArchitectureTests`, `Application.Tests`, `IntegrationTests`, `AccessibilityTests`, `MediaTests --filter VideoFrameGrabberTests`.
- [ ] **Step 5:** Commit: `feat: LIB-021, la pasada de fotogramas corre al arrancar y tras cada escaneo, y la cuadrícula los dibuja`.

### Task 6: evidencia, cursos y cierre

- [ ] **`CRS-006`**: la ADR dice que deja de ser una fila de cursos y pasa a ser el tercer origen. Con el capturador ya registrado, conectar `GetCourseThumbnail` a la tarjeta del curso es el mismo trabajo en pequeño; **si cabe en la tanda**, se hace con su prueba; si no, se registra en `TAREAS.md` con este hallazgo, no se deja implícito.
- [ ] Evidencia bilingüe `docs/evidence/stable/LIB021-cover-origins-frame.md` con: una biblioteca de prueba sin proveedor antes y después (fotograma en disco, tarjeta que lo dibuja), el coste medido de una pasada de 25 con la muestra real de `MediaTests`, y el control de que un archivo no aprobado no se abre.
- [ ] `FEATURES.md` (`LIB-021` sigue `IN_PROGRESS`, enlaza la evidencia), `CHANGELOG.{es,en}.md` («Añadido»), `TAREAS.md` (`ENG-003`).
- [ ] Previsualización de suelos, puertas, commit, push, `watch-ci.ps1`, fast-forward con la conclusión leída.
