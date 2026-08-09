# Plan de implementación de AP Reelume para Windows / AP Reelume Windows Implementation Plan

> **Para trabajadores agénticos / For agentic workers:** SUB-HABILIDAD OBLIGATORIA / REQUIRED SUB-SKILL: usar `superpowers:subagent-driven-development` (recomendado) o `superpowers:executing-plans` para ejecutar este plan tarea por tarea. Los pasos usan casillas `- [ ]` para el seguimiento. / Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to execute this plan task by task. Steps use `- [ ]` checkboxes for tracking.

**Objetivo / Goal:** construir primero un MVP x64 instalable y útil de **AP Reelume by AP Solutions** para Windows 11 y, solo después de superar su puerta de salida, completar la primera versión estable y registrar el trabajo `POST_STABLE`. / Build an installable, useful x64 MVP of **AP Reelume by AP Solutions** for Windows 11 first and, only after passing its exit gate, complete the first stable release and record `POST_STABLE` work.

**Arquitectura / Architecture:** solución .NET modular con dependencias `Presentation → Application → Domain ← Infrastructure`; el host de Windows compone adaptadores de Avalonia, SQLite, archivos, TMDB, LibVLC y Windows sin filtrarlos al dominio. Cada incremento es una sección vertical demostrable y conserva datos mediante contratos, eventos tipados, migraciones hacia delante y adaptadores reemplazables. / Modular .NET solution with `Presentation → Application → Domain ← Infrastructure` dependencies; the Windows host composes Avalonia, SQLite, file-system, TMDB, LibVLC, and Windows adapters without leaking them into the domain. Each increment is a demonstrable vertical slice and preserves data through contracts, typed events, forward migrations, and replaceable adapters.

**Stack tecnológico / Tech Stack:** C# y .NET 10 LTS; Avalonia 12.1, XAML y MVVM; LibVLCSharp 3 estable y LibVLC; SQLite con WAL y FTS5; xUnit, FsCheck, NSubstitute, aserciones xUnit, Avalonia.Headless, FlaUI y BenchmarkDotNet; MSIX y artefactos `win-x64` autocontenidos. / C# and .NET 10 LTS; Avalonia 12.1, XAML, and MVVM; stable LibVLCSharp 3 and LibVLC; SQLite with WAL and FTS5; xUnit, FsCheck, NSubstitute, xUnit assertions, Avalonia.Headless, FlaUI, and BenchmarkDotNet; MSIX and self-contained `win-x64` artifacts.

## Restricciones globales / Global Constraints

- Fuentes de verdad, en orden operativo / Sources of truth, in operational order: [`diseño aprobado / approved design`](../specs/2026-08-01-local-media-library-design.md), [`matriz canónica / canonical matrix`](../../FEATURES.md), [`ADR-0001`](../../adr/0001-public-product-name.md).
- Producto visible / Visible product: **AP Reelume**. Presentación completa / Full presentation: **AP Reelume by AP Solutions**. Firma / Signature: **by AP Solutions**.
- Identificadores internos estables / Stable internal identifiers: espacio de nombres raíz `ApSolutions.LocalMedia`, identidad de paquete `APSolutions.LocalMedia`, esquema URI `apsolutions-localmedia`; no contienen `Reelume`. / Root namespace `ApSolutions.LocalMedia`, package identity `APSolutions.LocalMedia`, URI scheme `apsolutions-localmedia`; none contains `Reelume`.
- Plataforma inicial / Initial platform: Windows 11 x64. `win-arm64` se compila como comprobación temprana de dependencias, pero solo es entregable en `STABLE`. / Windows 11 x64. `win-arm64` is built as an early dependency check but is only a `STABLE` deliverable.
- Presupuesto obligatorio / Mandatory budget: cero; no se presupone firma de pago, backend propio ni servicio de nube. / Zero; no paid signing, proprietary backend, or cloud service is assumed.
- Datos / Data: un usuario y un PC, sin cuenta, sincronización ni copia de vídeos. Toda persistencia personal es local. / One user and one PC, without accounts, sync, or video copying. All personal persistence is local.
- Dependencias / Dependencies: versiones exactas, administración central y archivo de bloqueo; se elige la revisión estable más alta compatible dentro de Avalonia `12.1.x` y LibVLCSharp `3.x`, se registra en el primer commit y no se usan rangos flotantes. / Exact versions, central management, and lock file; select the highest compatible stable patch within Avalonia `12.1.x` and LibVLCSharp `3.x`, record it in the first commit, and use no floating ranges.
- Secretos / Secrets: el token de lectura TMDB entra por secretos de CI o configuración local fuera del repositorio; nunca aparece en fuentes, registros, diagnósticos ni evidencias. / The TMDB read token enters through CI secrets or local configuration outside the repository; it never appears in source, logs, diagnostics, or evidence.
- Privacidad predeterminada / Privacy default: cero telemetría sin consentimiento; los diagnósticos están desactivados, se construyen por lista permitida y en el MVP solo se exportan manualmente tras previsualización. / Zero telemetry without consent; diagnostics are off, allowlist-built, and only manually exported after preview in the MVP.
- Idiomas / Languages: UI inicial en español mediante recursos y toda documentación pública/técnica en pares español/inglés o en un documento bilingüe sincronizado. / Initial UI in Spanish through resources and all public/technical documentation in synchronized Spanish/English pairs or one synchronized bilingual document.
- Licencia / License: `GPL-3.0-or-later`; todas las dependencias, códecs y muestras se auditan y el artefacto publica SBOM y avisos. / `GPL-3.0-or-later`; every dependency, codec, and sample is audited and artifacts publish an SBOM and notices.
- Multimedia de prueba / Test media: solo muestras generadas durante la prueba o redistribuibles; nunca se incorporan vídeos del usuario. / Only test-generated or redistributable samples; user videos are never added.
- Presupuestos de rendimiento / Performance budgets: ventana útil `<3 s`, primera página de búsqueda `<150 ms`, biblioteca a `60 FPS`, bloqueo del hilo UI por escaneo `<50 ms`, con catálogo caliente de 10.000 archivos. / Useful window `<3 s`, first search page `<150 ms`, library at `60 FPS`, scan UI-thread blocking `<50 ms`, with a warm 10,000-file catalog.
- Puerta de estado / Status gate: una función solo pasa a `VERIFIED` al enlazar evidencia reproducible desde `docs/FEATURES.md`; el ejecutor cambia primero `DESIGN_APPROVED → PLANNED → IN_PROGRESS → IMPLEMENTED → VERIFIED` según corresponda. / A feature only moves to `VERIFIED` after reproducible evidence is linked from `docs/FEATURES.md`; the implementer moves `DESIGN_APPROVED → PLANNED → IN_PROGRESS → IMPLEMENTED → VERIFIED` as appropriate.
- Prohibiciones del MVP / MVP exclusions: múltiples sesiones, cursos/vídeo genérico, estructura de vídeos administrada, marcadores/notas personales, listas personalizadas, Dolby Vision, passthrough Dolby/DTS y macOS/Linux. / Multiple sessions, courses/generic video, managed video structure, personal bookmarks/notes, custom lists, Dolby Vision, Dolby/DTS passthrough, and macOS/Linux.

### Constantes de política aprobadas para ejecución / Policy Constants Approved for Execution

| Política / Policy | Valor inicial exacto / Exact initial value |
|---|---|
| Confianza / Confidence | automático / automatic `≥0.90`; sugerido / suggested `0.60–0.8999`; pendiente / pending `<0.60`. |
| Scoring v1 | título / title `0.50`, episodio / episode `0.20`, temporada / season `0.15`, año / year `0.10`, duración / duration `0.05`; pesos no aplicables se renormalizan. Conflicto de tipo invalida; contradicción de temporada/episodio o warning ambiguo limita el resultado a `0.59`/`0.89` respectivamente. / Non-applicable weights are renormalized. Kind conflict rejects; season/episode contradiction or ambiguous warning caps the score at `0.59`/`0.89` respectively. |
| Firma ligera v1 / Lightweight fingerprint v1 | SHA-256 sobre tamaño, duración, contenedor, códecs, resolución y hasta 64 KiB del inicio, centro y final (`≤192 KiB` leídos). / SHA-256 over size, duration, container, codecs, resolution, and up to 64 KiB each from start, middle, and end (`≤192 KiB` read). |
| Escaneo / Scanning | debounce `750 ms`; una operación activa por raíz; extensiones `.mp4,.mkv,.avi,.mov,.webm,.m4v,.ts,.m2ts`; archivos sin cambios nunca se sondean. / One active operation per root; unchanged files are never probed. |
| Continuidad / Continuity | posición de reanudación mínima `30 s`; `InProgress` al alcanzar el menor de `60 s` o `2 %`; persistencia cada `5 s`; visto `90 %` por defecto, configurable `50–100 %`. / Minimum resume `30 s`; in progress at the lesser of `60 s` or `2%`; save every `5 s`; watched at default `90%`, configurable `50–100%`. |
| Cambio de versión / Version transfer | exacto si diferencia `≤max(5 s,1 %)`, proporcional si diferencia `≤10 %` y estructura compatible, confirmación en los demás casos. / Exact within `max(5 s,1%)`, proportional within `10%` with compatible structure, confirmation otherwise. |
| Siguiente episodio / Next episode | cuenta predeterminada `10 s`, configurable `0–60 s`, donde `0` desactiva. / Default countdown `10 s`, configurable `0–60 s`, with `0` disabling it. |
| Copias / Backups | cinco copias rotatorias válidas; nunca se elimina la última válida. / Five valid rotating backups; never delete the last valid one. |
| Recomendaciones v1 / Recommendations v1 | similitud de géneros `0.40`, reparto `0.25`, afinidad de valoración `0.20`, proximidad de año `0.10`, novedad/no visto `0.05`; explicación = señales no nulas de mayor peso. / Genre similarity `0.40`, cast `0.25`, rating affinity `0.20`, year proximity `0.10`, freshness/unwatched `0.05`; explanation = highest-weight non-zero signals. |

Estos valores pueden endurecerse tras evidencia, pero cualquier relajación o cambio semántico exige actualizar primero la especificación/matriz o registrar un ADR, según alcance. / These values may be tightened after evidence, but any relaxation or semantic change requires updating the specification/matrix first or recording an ADR, depending on scope.

---

## 1. Método de ejecución TDD y evidencia / TDD Execution and Evidence Method

Cada tarea es una puerta de revisión independiente y sigue este ciclo; no se agrupan varios estados rojos o verdes en un único commit. / Every task is an independent review gate and follows this cycle; multiple red or green states are not bundled into one commit.

1. **RED:** crear exactamente las pruebas nombradas en la tarea, con datos deterministas y aserciones del criterio. / Create exactly the tests named by the task, with deterministic data and criterion assertions.
2. **Demostrar RED / Prove RED:** ejecutar el filtro indicado y guardar en `artifacts/test-results/<task>/red/`; debe fallar por comportamiento ausente, no por compilación accidental o entorno roto. / Run the stated filter and save it under `artifacts/test-results/<task>/red/`; it must fail because behavior is absent, not due to accidental compilation or a broken environment.
3. **GREEN:** implementar el mínimo comportamiento descrito, sin ampliar alcance. / Implement the minimum described behavior without widening scope.
4. **Demostrar GREEN / Prove GREEN:** repetir el filtro, pruebas del proyecto y análisis estático; guardar TRX/JUnit y cobertura. / Repeat the filter, project tests, and static analysis; save TRX/JUnit and coverage.
5. **Refactor y verificación transversal / Refactor and cross-check:** eliminar duplicación, ejecutar pruebas de arquitectura/localización y las suites afectadas. / Remove duplication, run architecture/localization tests, and affected suites.
6. **Evidencia y documentación / Evidence and documentation:** crear o actualizar `docs/evidence/<release>/<task>.md` en español/inglés, enlazar ejecuciones, hardware/datos, resultado y cada ID; actualizar matriz solo al nivel demostrado. / Create or update `docs/evidence/<release>/<task>.md` in Spanish/English, link runs, hardware/data, result, and each ID; update the matrix only to the demonstrated level.
7. **Commit:** usar el prefijo `test:` para el RED cuando se conserve por separado y uno de `feat:`, `fix:`, `docs:` o `build:` para GREEN; nunca mezclar la tarea siguiente. / Use the `test:` prefix for RED when preserved separately and one of `feat:`, `fix:`, `docs:`, or `build:` for GREEN; never mix the next task.

Los bloques de implementación de este documento especifican contratos y comportamientos, no contienen código de la aplicación, conforme a la orden de planificación. / Implementation blocks in this document specify contracts and behavior and contain no application code, as required by the planning-only instruction.

### Comandos base / Baseline commands

Ejecutar desde la raíz en PowerShell 7 sobre Windows 11 con .NET 10 SDK y Visual Studio Build Tools + Windows 11 SDK para MSIX. / Run from the repository root in PowerShell 7 on Windows 11 with .NET 10 SDK and Visual Studio Build Tools + Windows 11 SDK for MSIX.

```powershell
dotnet --info
dotnet restore ApSolutions.LocalMedia.sln --locked-mode
dotnet build ApSolutions.LocalMedia.sln -c Debug --no-restore -warnaserror
dotnet test ApSolutions.LocalMedia.sln -c Debug --no-build --logger "trx;LogFileName=all.trx" --results-directory artifacts/test-results/all --collect:"XPlat Code Coverage"
pwsh ./eng/verify.ps1 -Configuration Release -Runtime win-x64
```

Filtros y verificaciones especializadas / Specialized filters and checks:

```powershell
dotnet test tests/ApSolutions.LocalMedia.Domain.Tests -c Debug --filter "FullyQualifiedName~<TestName>"
dotnet test tests/ApSolutions.LocalMedia.IntegrationTests -c Debug --filter "Category=Integration"
dotnet test tests/ApSolutions.LocalMedia.UiTests -c Debug --filter "Category=UI"
dotnet test tests/ApSolutions.LocalMedia.AccessibilityTests -c Release --filter "Category=Accessibility"
dotnet test tests/ApSolutions.LocalMedia.MediaTests -c Release --filter "Category=RealMedia"
dotnet run -c Release --project benchmarks/ApSolutions.LocalMedia.Benchmarks -- --filter "*"
pwsh ./eng/verify-docs.ps1
pwsh ./eng/generate-test-media.ps1 -Output artifacts/test-media
pwsh ./eng/package-x64.ps1 -Configuration Release -Output artifacts/package/win-x64
pwsh ./eng/verify-package.ps1 -Package artifacts/package/win-x64
```

### Definición de terminado por tarea / Per-task Definition of Done

- La prueba falla primero por la razón esperada y después pasa. / The test fails first for the expected reason and then passes.
- Cobertura de líneas del código nuevo `≥80 %` y cobertura de ramas de políticas de dominio `≥90 %`; las exclusiones requieren justificación en evidencia. / New-code line coverage `≥80%` and domain-policy branch coverage `≥90%`; exclusions require evidence rationale.
- Cero advertencias del compilador/analizadores, cero secretos detectados y ninguna dependencia vulnerable de gravedad alta/crítica. / Zero compiler/analyzer warnings, zero detected secrets, and no high/critical vulnerable dependency.
- Archivos de recursos y documentación conservan paridad española/inglesa. / Resource and documentation files preserve Spanish/English parity.
- El informe de evidencia nombra commit, comandos, entorno, muestras, resultados y IDs. / The evidence report names commit, commands, environment, samples, results, and IDs.
- No se modifica un archivo de vídeo salvo una operación de renombrado confirmada, registrada y confinada a su raíz. / No video file is modified except a confirmed, logged rename confined to its root.

## 2. Mapa de solución y responsabilidades / Solution Map and Responsibilities

Todas las rutas son relativas a la raíz del repositorio. Cuando una lista escribe primero una ruta completa y después nombres de archivos hermanos sin repetir el directorio, esos nombres pertenecen exactamente al directorio de la ruta completa inmediatamente anterior. / All paths are repository-root relative. When a list gives one full path followed by sibling filenames without repeating the directory, those filenames belong exactly to the immediately preceding full path's directory.

### Proyectos de producción / Production projects

| Proyecto / Project | Responsabilidad / Responsibility | Dependencias permitidas / Allowed dependencies |
|---|---|---|
| `src/ApSolutions.LocalMedia.Domain/ApSolutions.LocalMedia.Domain.csproj` | Entidades, valores, políticas y puertos de repositorio/motor/proveedor. / Entities, values, policies, and repository/engine/provider ports. | BCL únicamente / BCL only |
| `src/ApSolutions.LocalMedia.Application/ApSolutions.LocalMedia.Application.csproj` | Casos de uso, comandos/consultas, eventos y coordinación cancelable. / Use cases, commands/queries, events, and cancelable coordination. | `Domain`, abstractions `Microsoft.Extensions.*` reviewed |
| `src/ApSolutions.LocalMedia.Infrastructure/ApSolutions.LocalMedia.Infrastructure.csproj` | SQLite, migraciones, archivos, TMDB, LibVLC, copias, diagnósticos. / SQLite, migrations, files, TMDB, LibVLC, backups, diagnostics. | `Application`, `Domain`, adapter packages |
| `src/ApSolutions.LocalMedia.Presentation/ApSolutions.LocalMedia.Presentation.csproj` | Recursos, XAML, ViewModels, navegación, temas y accesibilidad Avalonia. / Resources, XAML, ViewModels, navigation, themes, and Avalonia accessibility. | `Application`, Avalonia; nunca `Infrastructure` / never `Infrastructure` |
| `src/ApSolutions.LocalMedia.Windows/ApSolutions.LocalMedia.Windows.csproj` | Host/composición, rutas Windows, Mica, bandeja, inicio, teclas y shell. / Host/composition, Windows paths, Mica, tray, startup, keys, and shell. | Los cuatro proyectos / all four projects |
| `src/ApSolutions.LocalMedia.Windows.Package/ApSolutions.LocalMedia.Windows.Package.wapproj` | Manifiesto MSIX x64/ARM64 y asociación de archivos. / x64/ARM64 MSIX manifest and file association. | Salida publicada del host / published host output |

### Proyectos de prueba y herramientas / Test and Tool Projects

| Proyecto/ruta / Project/path | Responsabilidad / Responsibility |
|---|---|
| `tests/ApSolutions.LocalMedia.Domain.Tests/` | Unitarias y propiedades de entidades/políticas. / Entity/policy unit and property tests. |
| `tests/ApSolutions.LocalMedia.Application.Tests/` | Casos de uso, eventos, cancelación y concurrencia. / Use cases, events, cancellation, and concurrency. |
| `tests/ApSolutions.LocalMedia.ArchitectureTests/` | Regla de dependencias, API pública e IDs internos. / Dependency rule, public API, and internal IDs. |
| `tests/ApSolutions.LocalMedia.IntegrationTests/` | SQLite, migraciones, archivos temporales, TMDB, copias y shell falso. / SQLite, migrations, temp file systems, TMDB, backups, and fake shell. |
| `tests/ApSolutions.LocalMedia.UiTests/` | Avalonia.Headless, navegación, XAML, temas, recursos y snapshots. / Avalonia.Headless, navigation, XAML, themes, resources, and snapshots. |
| `tests/ApSolutions.LocalMedia.AccessibilityTests/` | Automation peers, FlaUI, teclado y aserciones de contraste/escalado. / Automation peers, FlaUI, keyboard, and contrast/scaling assertions. |
| `tests/ApSolutions.LocalMedia.MediaTests/` | Motor simulado y matriz LibVLC real, pistas, HDR/SDR y archivos dañados. / Simulated engine and real LibVLC matrix, tracks, HDR/SDR, and corrupt files. |
| `tests/ApSolutions.LocalMedia.PerformanceTests/` | Presupuestos automatizados y detectores de bloqueo UI. / Automated budgets and UI-block detectors. |
| `tests/ApSolutions.LocalMedia.PackagingTests/` | Instalación, actualización, downgrade, reparación y desinstalación. / Install, update, downgrade, repair, and uninstall. |
| `tests/ApSolutions.LocalMedia.DocumentationTests/` | Pares ES/EN, enlaces, encabezados, IDs y evidencias. / ES/EN pairs, links, headings, IDs, and evidence. |
| `benchmarks/ApSolutions.LocalMedia.Benchmarks/` | BenchmarkDotNet para 10.000 elementos, búsqueda, escaneo y recomendaciones. / BenchmarkDotNet for 10,000 items, search, scan, and recommendations. |
| `eng/` | Orquestación reproducible de compilación, medios, verificación, SBOM y paquete. / Reproducible build, media, verification, SBOM, and package orchestration. |

### Contratos que quedan fijados antes de los adaptadores / Contracts Fixed Before Adapters

Los tipos exactos se crean en el archivo indicado y no se renombran en tareas posteriores sin ADR y migración de consumidores. / Exact types are created in the stated file and are not renamed later without an ADR and consumer migration.

| Archivo / File | Contrato producido / Produced contract |
|---|---|
| `Domain/Catalog/Ids.cs` | `TitleId`, `MediaFileId`, `MediaVersionId`, `LibraryRootId`, `SeriesId`, `EpisodeId` como valores inmutables. / Immutable value types. |
| `Domain/Discovery/LibraryRoot.cs` | `LibraryRoot`, `RootKind`, `RootAvailability`, `ScanPolicy`. |
| `Domain/Catalog/MediaModels.cs` | `Title`, `Season`, `Episode`, `MediaFile`, `MediaVersion`, `TechnicalMetadata`. |
| `Domain/Identification/MatchModels.cs` + `ConfidencePolicy.cs` | `ParsedMediaName`, `MatchCandidate`, `MatchSignal`, `ReviewState`, `ConfidencePolicy`. |
| `Domain/Playback/PlaybackContracts.cs` | `IMediaPlayerEngine`, `PlaybackRequest`, `PlaybackSnapshot`, `MediaTrack`, `PlaybackCapabilities`. |
| `Domain/Continuity/ContinuityModels.cs`, `PlaybackPreference.cs`, `ProgressTransferPolicy.cs`, `IntroMarker.cs` | `WatchState`, `WatchStatus`, `PlaybackPreference`, `ProgressTransferDecision`, `IntroMarker`. |
| `Domain/Personalization/PersonalState.cs` + `RecommendationModels.cs` | `PersonalState`, `Recommendation`, `RecommendationReason`. |
| `Domain/Discovery/ILibraryRootRepository.cs`, `Domain/Catalog/IMediaFileRepository.cs`, `Domain/Catalog/ICatalogRepository.cs`, `Domain/Identification/IMatchCandidateRepository.cs` | Repositorios de descubrimiento, catálogo e identificación. / Discovery, catalog, and identification repositories. |
| `Domain/Continuity/IWatchStateRepository.cs`, `IPlaybackPreferenceRepository.cs`, `IIntroMarkerRepository.cs`, `Domain/Personalization/IPersonalStateRepository.cs` | Repositorios de continuidad y datos personales. / Continuity and personal repositories. |
| `Domain/Discovery/IMediaProbe.cs`, `IFileIdentityProvider.cs`, `Domain/Metadata/IMetadataProvider.cs`, `IMetadataCache.cs`, `Domain/Common/IClock.cs` | Puertos externos estrechos. / Narrow external ports. |
| `Application/Events/IApplicationEventPublisher.cs`, `CatalogEvents.cs`, `Identification/ReviewInboxChanged.cs`, `Playback/PlaybackEvents.cs` | Publicador y eventos tipados `ScanProgressChanged`, `CatalogChanged`, `ReviewInboxChanged`, `PlaybackSessionChanged`. / Publisher and typed events. |
| `Application/Data/IUnitOfWork.cs` + `Application/Storage/IAppDataPaths.cs` | Límite transaccional y rutas de datos sin tipos SQLite/Windows. / Transaction and app-data paths without SQLite/Windows types. |
| `Application/Discovery/ScanContracts.cs` | `StartScanCommand`, `ScanItemResult`, `ScanSummary`, `IScanCoordinator`. |
| `Application/Catalog/CatalogQueries.cs` | `CatalogQuery`, `CatalogPage`, `CatalogItemSummary`, `ICatalogQueryService`. |
| `Application/Backup/BackupContracts.cs` | `BackupManifest`, `RootRemap`, `RestorePreview`, `IBackupService`. |
| `Application/Privacy/DiagnosticsContracts.cs` | `DiagnosticsConsent`, `SanitizedDiagnostic`, `IDiagnosticsBuilder`. |

### Estructura documental y de evidencia / Documentation and Evidence Structure

```text
docs/
  FEATURES.md
  adr/
  roadmap/README.es.md + README.en.md
  development/README.es.md + README.en.md
  user-guide/README.es.md + README.en.md
  troubleshooting/README.es.md + README.en.md
  privacy/PRIVACY.es.md + PRIVACY.en.md
  release/RELEASING.es.md + RELEASING.en.md
  release/THIRD-PARTY-NOTICES.es.md + THIRD-PARTY-NOTICES.en.md
  evidence/mvp/*.md
  evidence/stable/*.md
  evidence/post-stable/*.md
  superpowers/plans/2026-08-01-ap-reelume-windows-mvp-implementation.md
```

## 3. Orden obligatorio y puertas / Mandatory Order and Gates

```mermaid
flowchart LR
  I0["I0 Shell local"] --> I1["I1 Biblioteca buscable"]
  I1 --> I2["I2 Identificación revisable"]
  I2 --> I3["I3 Reproducción real"]
  I3 --> I4["I4 Continuidad"]
  I4 --> I5["I5 Experiencia personal y A11Y"]
  I5 --> I6["I6 Recuperación y privacidad"]
  I6 --> I7["I7 Paquete MVP x64"]
  I7 --> S1["STABLE: ARM64, segmentos, updater, Store"]
  S1 --> P1["POST_STABLE: listas y nuevas evaluaciones"]
```

No se inicia un incremento si la demo, las pruebas obligatorias o la actualización bilingüe de la matriz del anterior fallan. `STABLE` no comienza hasta que todos los IDs MVP comprometidos estén `VERIFIED` y los límites `OUT_OF_SCOPE` hayan superado revisión de alcance. / An increment does not start while the prior demo, required tests, or bilingual matrix update fails. `STABLE` does not begin until all committed MVP IDs are `VERIFIED` and `OUT_OF_SCOPE` boundaries pass scope review.

---

# Parte A — MVP Windows 11 x64 / Part A — Windows 11 x64 MVP

## Incremento I0 — Shell local, persistente y bilingüe / Increment I0 — Local, Persistent, Bilingual Shell

**Demo utilizable / Usable demo:** un ejecutable x64 abre en español sin cuenta, muestra Inicio/Biblioteca/Ajustes, cambia tema sin reinicio, crea una base SQLite válida y no realiza tráfico. / An x64 executable opens in Spanish without an account, shows Home/Library/Settings, switches theme without restart, creates a valid SQLite database, and makes no network traffic.

### Tarea 1 — Repositorio compilable y límites de arquitectura / Task 1 — Buildable Repository and Architecture Boundaries

**IDs:** `PRD-004`, `PRD-005`, `DOC-001`.

**Archivos / Files:** crear / create `global.json`, `ApSolutions.LocalMedia.sln`, `Directory.Build.props`, `Directory.Packages.props`, `.config/dotnet-tools.json`, `.editorconfig`, `LICENSE`, `NOTICE`, los cinco `.csproj` de producción salvo el paquete y los diez `.csproj` de pruebas enumerados en la sección 2, `benchmarks/ApSolutions.LocalMedia.Benchmarks/ApSolutions.LocalMedia.Benchmarks.csproj`, `.github/workflows/ci.yml`, `eng/verify.ps1`, `eng/verify-docs.ps1`, `docs/development/README.es.md`, `docs/development/README.en.md`; modificar / modify `.gitignore`, `docs/FEATURES.md`.

**Interfaces / Interfaces:** produce la regla de referencias de la sección 2, `net10.0` para núcleo y `net10.0-windows10.0.22621.0` solo para host/pruebas Windows; administración central y `packages.lock.json`. / Produces the section 2 reference rule, `net10.0` for the core and `net10.0-windows10.0.22621.0` only for Windows host/tests; central management and `packages.lock.json`.

- [x] **T1.1 RED:** crear `ArchitectureDependencyTests`, `StableInternalIdentityTests`, `BilingualDocumentationPairTests` y `PinnedDependencyTests`; exigir que Domain no referencie paquetes, Application no referencie Infrastructure/Avalonia/Windows, Presentation no referencie Infrastructure y ningún ensamblado/ID persistente contenga `Reelume`. / Create those tests and enforce the stated boundaries and identity rule.
- [x] **T1.2 Demostrar RED / Prove RED:** ejecutar `dotnet test tests/ApSolutions.LocalMedia.ArchitectureTests` y `dotnet test tests/ApSolutions.LocalMedia.DocumentationTests`; esperar proyectos/archivos ausentes, no un fallo del SDK. / Expect missing projects/files, not an SDK failure.
- [x] **T1.3 GREEN:** crear la solución/referencias mínimas, seleccionar y fijar revisiones estables compatibles, activar nullable/analyzers/warnings-as-errors/deterministic builds y documentar instalación del SDK en ambos idiomas. Verificar también `dotnet restore -r win-arm64` para descubrir una dependencia nativa bloqueante temprano, sin declarar ARM64 entregado. / Create the minimal solution/references, pin compatible stable patches, enable the stated build controls, document SDK setup bilingually, and run an early ARM64 restore check without claiming delivery.
- [x] **T1.4 Verificar / Verify:** ejecutar `dotnet restore --locked-mode`, `dotnet build -warnaserror`, ambas suites y `pwsh ./eng/verify-docs.ps1`; esperar PASS y cero referencias prohibidas. / Run restore, build, both suites, and docs verification; expect PASS and zero forbidden references.
- [x] **T1.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T1-foundation.md` contiene grafo de proyectos, versiones fijadas, resultado x64/restore ARM64, licencias iniciales y enlaces CI; `PRD-004` puede quedar `VERIFIED`, `PRD-005` y `DOC-001` quedan como máximo `IN_PROGRESS`. / Evidence contains project graph, pinned versions, x64/ARM64 restore result, initial licenses, and CI links; only `PRD-004` may become `VERIFIED`.
- [x] **T1.6 Commit:** `build: establish local media architecture and verification`.

### Tarea 2 — Shell Avalonia, nombre y localización / Task 2 — Avalonia Shell, Name, and Localization

**IDs:** `PRD-001`, `UX-002`, `UX-004`, `REL-004`, `DOC-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Presentation/App.axaml`, `src/ApSolutions.LocalMedia.Presentation/App.axaml.cs`, `src/ApSolutions.LocalMedia.Presentation/Shell/ShellView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Shell/ShellViewModel.cs`, `src/ApSolutions.LocalMedia.Presentation/Navigation/NavigationService.cs`, `src/ApSolutions.LocalMedia.Presentation/Resources/Strings.es.axaml`, `src/ApSolutions.LocalMedia.Presentation/Resources/Strings.en.axaml`, `src/ApSolutions.LocalMedia.Presentation/Resources/Brand.axaml`, `src/ApSolutions.LocalMedia.Windows/Program.cs`, `src/ApSolutions.LocalMedia.Windows/CompositionRoot.cs`, `tests/ApSolutions.LocalMedia.UiTests/Shell/ShellLocalizationTests.cs`, `tests/ApSolutions.LocalMedia.AccessibilityTests/Shell/ShellAutomationTests.cs`; modificar / modify `docs/FEATURES.md`.

**Interfaces / Interfaces:** `INavigationService.Navigate(AppRoute)` produce rutas `Home`, `Library`, `Review`, `Backups`, `Settings`; el recurso `ProductDisplayName` vale `AP Reelume` y `PublisherSignature` vale `by AP Solutions`. / Produces the stated navigation routes and exact brand resource values.

- [x] **T2.1 RED:** probar arranque sin autenticación, ruta Home predeterminada, cinco destinos navegables, cero texto XAML visible fuera de recursos, paridad de claves ES/EN, nombre visible exacto y firma solo en Acerca de/superficies de marca. / Test account-free startup, default Home, five routes, no visible hard-coded XAML strings, ES/EN key parity, exact visible name, and signature placement.
- [x] **T2.2 Demostrar RED / Prove RED:** `dotnet test tests/ApSolutions.LocalMedia.UiTests --filter "FullyQualifiedName~Shell"`; esperar fallo por shell/recursos ausentes. / Expect failure because shell/resources are absent.
- [x] **T2.3 GREEN:** implementar host y shell mínimos con DI, navegación por comandos, recursos españoles predeterminados e inglés alternativo; no crear login, perfil, cliente HTTP ni servicio remoto. / Implement the minimal host/shell with DI, command navigation, default Spanish and alternate English resources; create no login, profile, HTTP client, or remote service.
- [x] **T2.4 Verificar / Verify:** ejecutar tests UI, accesibilidad del shell, `rg -n 'Text="[^{]' src/ApSolutions.LocalMedia.Presentation -g '*.axaml'` (resultado vacío salvo valores no visibles permitidos) y captura headless ES/EN. / Run UI/accessibility tests, localization scan, and ES/EN headless captures.
- [x] **T2.5 Aceptación/evidencia / Acceptance/evidence:** demo abre en español, navega íntegramente con teclado y Acerca de muestra `AP Reelume by AP Solutions`; evidencia en `docs/evidence/mvp/T2-shell-localization.md`. `REL-004` permanece `DESIGN_APPROVED` o `PLANNED`: el ADR está aplicado, la autorización formal no. / Demo opens in Spanish, is keyboard navigable, and About shows the full presentation; formal clearance remains unverified.
- [x] **T2.6 Commit:** `feat: add bilingual AP Reelume application shell`.

### Tarea 3 — Tema Fluent y preferencias locales / Task 3 — Fluent Theme and Local Preferences

**IDs:** `UX-002`, `UX-003`, `UX-004`, `A11Y-001`, `A11Y-002`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Settings/ISettingsStore.cs`, `src/ApSolutions.LocalMedia.Presentation/Theme/IBackdropService.cs`, `src/ApSolutions.LocalMedia.Presentation/Theme/IReducedMotionService.cs`, `src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml`, `src/ApSolutions.LocalMedia.Presentation/Theme/FluentThemeService.cs`, `src/ApSolutions.LocalMedia.Presentation/Theme/ThemePreference.cs`, `src/ApSolutions.LocalMedia.Presentation/Settings/AppearanceSettingsView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Settings/AppearanceSettingsViewModel.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Settings/JsonSettingsStore.cs`, `src/ApSolutions.LocalMedia.Windows/Windowing/MicaBackdropService.cs`, `src/ApSolutions.LocalMedia.Windows/Accessibility/WindowsReducedMotionService.cs`, `tests/ApSolutions.LocalMedia.UiTests/Theme/ThemeTests.cs`, `tests/ApSolutions.LocalMedia.AccessibilityTests/ContrastTokenTests.cs`.

**Interfaces / Interfaces:** `IThemeService.Apply(ThemePreference)` con `System`, `Light`, `Dark`; recursos semánticos de color, espaciado, tipografía, foco y movimiento; `IReducedMotionService.IsEnabled`. / Produces theme application, semantic tokens, and reduced-motion state.

- [x] **T3.1 RED:** probar tema de sistema predeterminado, cambio claro/oscuro sin reinicio, persistencia, reproductor siempre oscuro, foco ≥2 px, contraste WCAG AA de texto/controles y animaciones desactivadas con reducción de movimiento. / Test default system theme, live switching, persistence, always-dark player, focus thickness, WCAG AA contrast, and reduced motion.
- [x] **T3.2 Demostrar RED / Prove RED:** ejecutar filtros `ThemeTests|ContrastTokenTests`; esperar ausencia de tokens/servicio. / Expect missing tokens/service.
- [x] **T3.3 GREEN:** añadir tokens Fluent de azul sereno, densidad de escritorio, estilos de foco/alto contraste y almacenamiento JSON atómico; aislar Mica en Windows y usar fondo sólido en fallback. / Add calm-blue Fluent tokens, desktop density, focus/high-contrast styles, atomic JSON settings, and isolated Mica with solid fallback.
- [x] **T3.4 Verificar / Verify:** tests headless en cuatro combinaciones de tema/contraste, snapshot a 100/150/200 % y comprobación de recursos ES/EN. / Run headless theme/contrast combinations, scaling snapshots, and resource checks.
- [x] **T3.5 Aceptación/evidencia / Acceptance/evidence:** informe `docs/evidence/mvp/T3-theme.md` con matriz visual y contraste; los IDs A11Y siguen `IN_PROGRESS` hasta auditoría de extremo a extremo. / Evidence includes visual/contrast matrix; A11Y IDs remain in progress until end-to-end audit.
- [x] **T3.6 Commit:** `feat: add persistent accessible Fluent theming`.

### Tarea 4 — SQLite, migraciones e integridad al iniciar / Task 4 — SQLite, Migrations, and Startup Integrity

**IDs:** `PRD-001`, `DAT-001`, `PRD-004`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Data/IUnitOfWork.cs`, `src/ApSolutions.LocalMedia.Application/Data/IMigrationRunner.cs`, `src/ApSolutions.LocalMedia.Application/Data/IDatabaseIntegrityChecker.cs`, `src/ApSolutions.LocalMedia.Application/Storage/IAppDataPaths.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/SqliteConnectionFactory.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/MigrationRunner.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/IntegrityChecker.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0001_initial.sql`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`, `src/ApSolutions.LocalMedia.Windows/AppDataPaths.cs`, `src/ApSolutions.LocalMedia.Presentation/Recovery/DatabaseRecoveryView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Recovery/DatabaseRecoveryViewModel.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Data/SqliteBootstrapTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Data/MigrationFailureTests.cs`, `tests/ApSolutions.LocalMedia.ArchitectureTests/SqliteIsolationTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Recovery/DatabaseRecoveryViewTests.cs`.

**Interfaces / Interfaces:** `IUnitOfWork.ExecuteAsync`, `IAppDataPaths.DatabasePath`, `IMigrationRunner.MigrateAsync`, `IDatabaseIntegrityChecker.CheckAsync`; base en `%LOCALAPPDATA%/APSolutions/LocalMedia/library.db`. Los repositorios se añaden con su entidad en el incremento vertical propietario. / Produces transaction, app-data path, migration, and integrity contracts at the stable local path. Repositories arrive with their owning entity in each vertical slice.

- [x] **T4.1 RED:** probar creación local, `journal_mode=WAL`, `foreign_keys=ON`, `busy_timeout`, migración idempotente, transacción atómica, copia previa, conservación de base válida cuando una migración inyectada falla y vista de recuperación que nunca ofrece sobrescribir el respaldo. / Test local creation, WAL, foreign keys, busy timeout, idempotent migration, atomic transaction, pre-copy, valid database preservation on injected migration failure, and a recovery view that never offers to overwrite the backup.
- [x] **T4.2 Demostrar RED / Prove RED:** ejecutar `SqliteBootstrapTests|MigrationFailureTests|SqliteIsolationTests|DatabaseRecoveryViewTests`; esperar falta de adaptador/esquema/vista. / Expect missing adapter/schema/view.
- [x] **T4.3 GREEN:** crear bootstrap mínimo y migración inicial para `schema_history`; las tablas funcionales llegan en migraciones de su tarea propietaria. Abrir UI en modo recuperación si integridad falla, sin sobrescribir base ni respaldo. / Create minimal bootstrap and the initial `schema_history`; functional tables arrive in their owning task's migrations. Open recovery UI on failed integrity without overwriting data.
- [x] **T4.4 Verificar / Verify:** repetir pruebas en archivo temporal real, forzar cierre del proceso de prueba y ejecutar `PRAGMA integrity_check`; esperar `ok` y pérdida nula de transacción confirmada. / Repeat against a real temp file, force-close the test process, and expect integrity `ok` with committed transaction preserved.
- [x] **T4.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T4-sqlite.md` adjunta esquema, migración normal/fallida y resultado de integridad; `DAT-001` queda `IMPLEMENTED`, todavía no `VERIFIED` hasta probar copia previa/restauración en I6. / Evidence attaches schema, migration paths, and integrity; final verification waits for I6 recovery.
- [x] **T4.6 Commit:** `feat: persist local data with WAL migrations`.

**Puerta I0 / I0 gate:** demo aprobada, T1–T4 verdes, sin tráfico en captura de 60 s, documentación de desarrollo emparejada y ningún ID interno dependiente de marca. / Demo approved, T1–T4 green, no traffic in a 60-second capture, paired development docs, and no brand-dependent internal ID.

---

## Incremento I1 — Biblioteca local buscable / Increment I1 — Searchable Local Library

**Demo utilizable / Usable demo:** añadir una raíz local/USB/UNC, ver progreso cancelable, navegar y buscar sus películas/episodios sin copiar archivos, desconectar/reconectar una unidad y conservar catálogo. / Add a local/USB/UNC root, see cancelable progress, browse/search its movies/episodes without copying files, disconnect/reconnect storage, and preserve the catalog.

### Tarea 5 — Gestión segura de raíces / Task 5 — Safe Root Management

**IDs:** `LIB-001`, `LIB-010`, `PRD-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Catalog/Ids.cs`, `src/ApSolutions.LocalMedia.Domain/Discovery/LibraryRoot.cs`, `src/ApSolutions.LocalMedia.Domain/Discovery/ILibraryRootRepository.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/IPathNormalizer.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/AddLibraryRoot.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/RemoveLibraryRoot.cs`, `src/ApSolutions.LocalMedia.Infrastructure/FileSystem/WindowsPathNormalizer.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/LibraryRootRepository.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0002_library_roots.sql`, `src/ApSolutions.LocalMedia.Presentation/Onboarding/RootOnboardingView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Onboarding/RootOnboardingViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Discovery/LibraryRootTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Discovery/RootLifecycleTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** `AddLibraryRootCommand(Path, RootKind, ScanPolicy)`, `RemoveLibraryRootCommand(LibraryRootId, PreserveCatalog)`, `IPathNormalizer.NormalizeAndValidate`; quitar raíz usa `PreserveCatalog=true` por defecto y jamás toca vídeos. / Root removal defaults to preserving catalog and never touches videos.

- [x] **T5.1 RED:** probar rutas locales/USB/UNC válidas por separado, equivalencia de mayúsculas/separadores, raíz contenida/duplicada rechazada, acceso denegado accionable y que añadir/quitar no crea/copia/mueve/elimina ningún vídeo. / Test each root kind, normalization, duplicate/nested rejection, actionable denial, and zero video mutation.
- [x] **T5.2 Demostrar RED / Prove RED:** ejecutar `LibraryRootTests|RootLifecycleTests`; esperar comandos/entidad ausentes. / Expect missing entity/commands.
- [x] **T5.3 GREEN:** implementar entidad, repositorio, selector y onboarding; registrar tipo/disponibilidad/política, validar cada raíz de manera independiente y presentar consentimiento antes del primer escaneo. / Implement entity, repository, picker, and onboarding with independent validation and scan consent.
- [x] **T5.4 Verificar / Verify:** ejecutar integración sobre directorio temporal, unidad sustituta y recurso UNC de prueba; comparar inventario SHA-256 de vídeos antes/después (solo en la prueba, no en escaneo normal). / Run temp/substitute/UNC integration and compare before/after video inventory hashes only in the test.
- [x] **T5.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T5-roots.md` contiene tres tipos, errores y prueba de no copia; `LIB-001` puede quedar `VERIFIED`. / Evidence covers all three types, errors, and no-copy proof.
- [x] **T5.6 Commit:** `feat: manage local USB and UNC library roots`.

### Tarea 6 — Escaneo cancelable, incremental y sonda / Task 6 — Cancelable Incremental Scan and Probe

**IDs:** `LIB-002`, `LIB-004`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Catalog/MediaModels.cs`, `src/ApSolutions.LocalMedia.Domain/Catalog/IMediaFileRepository.cs`, `src/ApSolutions.LocalMedia.Domain/Discovery/IMediaProbe.cs`, `src/ApSolutions.LocalMedia.Application/Events/IApplicationEventPublisher.cs`, `src/ApSolutions.LocalMedia.Application/Events/CatalogEvents.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/IMediaFileEnumerator.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/ScanContracts.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/ScanCoordinator.cs`, `src/ApSolutions.LocalMedia.Infrastructure/FileSystem/MediaFileEnumerator.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Media/LibVlcMediaProbe.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/MediaFileRepository.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0003_media_files_scans.sql`, `src/ApSolutions.LocalMedia.Presentation/Library/ScanProgressViewModel.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Discovery/ScanCoordinatorTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Discovery/IncrementalScanTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** `IScanCoordinator.StartAsync(StartScanCommand, CancellationToken)`, `IMediaProbe.ProbeAsync(path, token)`, `ScanProgressChanged`; extensiones admitidas `.mp4,.mkv,.avi,.mov,.webm,.m4v,.ts,.m2ts`. / Produces scan/probe/event contracts and the explicit extension allowlist.

- [x] **T6.1 RED:** probar enumeración por lotes, error aislado por raíz/archivo, cancelación con resumen parcial reanudable, exclusión de extensiones, sonda solo de nuevo/modificado y cero bloqueo síncrono de UI. / Test batched enumeration, isolated failures, resumable cancellation, extension filtering, probe-only-new/changed, and no synchronous UI blocking.
- [x] **T6.2 Demostrar RED / Prove RED:** ejecutar `ScanCoordinatorTests|IncrementalScanTests`; esperar coordinador ausente. / Expect missing coordinator.
- [x] **T6.3 GREEN:** implementar pipeline idempotente con concurrencia limitada por raíz, checkpoint persistido, progreso tipado y transacciones pequeñas; la sonda real queda detrás de `IMediaProbe`. / Implement an idempotent per-root bounded pipeline, persisted checkpoint, typed progress, and small transactions behind `IMediaProbe`.
- [x] **T6.4 Verificar / Verify:** pruebas con 1 archivo, 1.000 archivos falsos, permiso denegado y cancelación a mitad; un segundo escaneo sin cambios debe registrar `ProbeCount=0`. / Test small/large/denied/canceled cases; unchanged second scan must report zero probes.
- [x] **T6.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T6-scan.md` incluye conteos, checkpoints, errores y tiempo máximo de despacho UI; `LIB-002` permanece `IMPLEMENTED` hasta pruebas de vigilancia/rendimiento. / Evidence includes counts, checkpoints, errors, and max UI dispatch time; final verification waits.
- [x] **T6.6 Commit:** `feat: scan media incrementally with cancellation`.

### Tarea 7 — Catálogo, FTS5 y vistas de biblioteca / Task 7 — Catalog, FTS5, and Library Views

**IDs:** `LIB-004`, `UX-001`, `UX-004`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Catalog/ICatalogRepository.cs`, `src/ApSolutions.LocalMedia.Application/Catalog/CatalogQueries.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/CatalogRepository.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0004_catalog_fts.sql`, `src/ApSolutions.LocalMedia.Presentation/Library/LibraryView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Library/LibraryViewModel.cs`, `src/ApSolutions.LocalMedia.Presentation/Library/CatalogItemViewModel.cs`, `src/ApSolutions.LocalMedia.Presentation/Movie/MovieDetailsView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Show/ShowDetailsView.axaml`, `tests/ApSolutions.LocalMedia.IntegrationTests/Catalog/CatalogQueryTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Library/LibraryNavigationTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** `ICatalogQueryService.QueryAsync(CatalogQuery, CancellationToken)` devuelve `CatalogPage` estable con cursor, búsqueda, filtros `Movie/Show/Available/Progress/Personal` y orden `Title/Year/Added/LastPlayed`. / Returns a stable cursor page with explicit search, filters, and sorts.

- [x] **T7.1 RED:** probar inserción película/serie/temporada/episodio, FTS por título alternativo/reparto/género, ausencia de rutas en FTS, paginación estable, filtros/orden y navegación Biblioteca→ficha→volver conservando posición. / Test catalog hierarchy, FTS fields and path exclusion, stable paging, filters/sorts, and navigation state.
- [x] **T7.2 Demostrar RED / Prove RED:** ejecutar `CatalogQueryTests|LibraryNavigationTests`; esperar esquema/consulta/vista ausentes. / Expect missing schema/query/view.
- [x] **T7.3 GREEN:** crear esquema normalizado, triggers FTS5, consulta proyectada cancelable, lista virtualizada y fichas mínimas; nunca exponer conexión SQLite a ViewModels. / Create normalized schema, FTS triggers, cancelable projected query, virtualized list, and minimal details; never expose SQLite to ViewModels.
- [x] **T7.4 Verificar / Verify:** ejecutar búsqueda Unicode/diacríticos, 100 páginas sin duplicados, test de ruta privada ausente y snapshots ES/EN. / Run Unicode/diacritic search, 100 stable pages, private-path exclusion, and ES/EN snapshots.
- [x] **T7.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T7-catalog-search.md` enlaza plan de consulta SQLite, resultados y capturas; UX Home queda aún parcial. / Evidence links query plan, results, and captures; Home remains partial.
- [x] **T7.6 Commit:** `feat: browse and search the local catalog`.

### Tarea 8 — Identidad, movimiento y disponibilidad / Task 8 — Identity, Movement, and Availability

**IDs:** `LIB-009`, `LIB-010`, `PLY-010`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Discovery/FileIdentity.cs`, `src/ApSolutions.LocalMedia.Domain/Discovery/IFileIdentityProvider.cs`, `src/ApSolutions.LocalMedia.Domain/Discovery/FileReconciliationPolicy.cs`, `src/ApSolutions.LocalMedia.Infrastructure/FileSystem/NtfsFileIdentityProvider.cs`, `src/ApSolutions.LocalMedia.Infrastructure/FileSystem/LightweightFingerprintProvider.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/ReconcileScanResults.cs`, `src/ApSolutions.LocalMedia.Presentation/Library/UnavailableBadge.axaml`, `src/ApSolutions.LocalMedia.Presentation/Library/ManualReassignmentViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Discovery/FileReconciliationTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Discovery/MoveAndDeviceLossTests.cs`.

**Interfaces / Interfaces:** `IFileIdentityProvider.GetAsync`, `FileIdentity(VolumeId, FileId, Fingerprint)`, `ReconciliationDecision.Exact/Probable/New/Missing`; firma v1 usa la constante de sección global y `Probable` exige confirmación. / Defines identity and reconciliation decisions; fingerprint v1 uses the global constant and probable matches require confirmation.

- [x] **T8.1 RED:** probar movimiento NTFS exacto, firma ligera en sistema sin ID, colisión probable no fusionada, ruta anterior conservada hasta commit, unidad ausente marcada no disponible y reconexión sin duplicado/progreso perdido. / Test exact NTFS move, lightweight fallback, unmerged probable collision, old-path preservation, unavailable storage, and duplicate-free recovery.
- [x] **T8.2 Demostrar RED / Prove RED:** ejecutar `FileReconciliationTests|MoveAndDeviceLossTests`; esperar política/proveedores ausentes. / Expect missing policy/providers.
- [x] **T8.3 GREEN:** implementar muestreo acotado de bytes + tamaño/duración/técnicos, ID NTFS por handle seguro y reconciliación transaccional; nunca calcular hash completo en escaneo normal. / Implement bounded fingerprinting, safe-handle NTFS ID, and transactional reconciliation; never full-hash during normal scan.
- [x] **T8.4 Verificar / Verify:** retirar unidad durante enumeración y entre sonda/commit, reconectar con letra distinta y ejecutar reasignación manual; todos los casos conservan entidades y estado. / Remove storage at multiple phases, reconnect under a different letter, and manually reassign while preserving data.
- [x] **T8.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T8-identity-availability.md` contiene matriz local/USB/UNC y conteo de duplicados cero; `LIB-009` y `LIB-010` pueden quedar `VERIFIED`. / Evidence contains local/USB/UNC matrix and zero duplicate count.
- [x] **T8.6 Commit:** `feat: reconcile moved and unavailable media safely`.

### Tarea 9 — Vigilancia y recuperación de eventos / Task 9 — Watching and Event Recovery

**IDs:** `LIB-002`, `LIB-003`, `LIB-010`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Common/IClock.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/IRootWatcher.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/RootWatchCoordinator.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Time/SystemClock.cs`, `src/ApSolutions.LocalMedia.Infrastructure/FileSystem/DebouncedFileWatcher.cs`, `src/ApSolutions.LocalMedia.Infrastructure/FileSystem/FallbackScanScheduler.cs`, `src/ApSolutions.LocalMedia.Presentation/Settings/ScanSettingsView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Settings/ScanSettingsViewModel.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Discovery/WatchCoordinatorTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Discovery/FileWatcherRecoveryTests.cs`.

**Interfaces / Interfaces:** `IRootWatcher.StartAsync(LibraryRoot, CancellationToken)`, lote `FileChangeBatch`, debounce predeterminado `750 ms`, escaneo de respaldo en inicio/manual y al recuperar raíz. / Produces watcher contract, 750 ms default debounce, and explicit fallback triggers.

- [x] **T9.1 RED:** probar consolidación create/change/rename/delete, tormenta de 1.000 eventos, evento perdido recuperado por fallback, watcher UNC no fiable degradado sin perder catálogo y límites de una operación por raíz. / Test event coalescing/storms, fallback recovery, unreliable UNC degradation, and one-operation-per-root limit.
- [x] **T9.2 Demostrar RED / Prove RED:** ejecutar `WatchCoordinatorTests|FileWatcherRecoveryTests`; esperar watcher/coordinador ausentes. / Expect missing watcher/coordinator.
- [x] **T9.3 GREEN:** implementar watcher cancelable y scheduler inactivo fuera de eventos/intervalos; traducir lotes a escaneo incremental idempotente, no a mutaciones directas. / Implement cancelable watcher and idle scheduler; translate batches into idempotent incremental scans, never direct mutations.
- [x] **T9.4 Verificar / Verify:** medir aparición local ≤5 s, desconectar UNC, recuperar eventos con escaneo y confirmar `ProbeCount=0` en elementos sin cambios. / Measure local appearance within 5 s, disconnect UNC, recover events, and preserve zero probes for unchanged items.
- [x] **T9.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T9-watching.md` incluye temporización, tormenta y fallback; `LIB-002` y `LIB-003` pueden quedar `VERIFIED` salvo presupuesto de UI que cierra T10. / Evidence includes timing, storm, and fallback; performance closure waits for T10.
- [x] **T9.6 Commit:** `feat: watch roots with incremental fallback recovery`.

### Tarea 10 — Presupuesto de 10.000 archivos / Task 10 — 10,000-File Budget

**IDs:** `LIB-002`, `LIB-004`.

**Archivos / Files:** crear / create `tests/ApSolutions.LocalMedia.PerformanceTests/Fixtures/Catalog10kBuilder.cs`, `tests/ApSolutions.LocalMedia.PerformanceTests/StartupBudgetTests.cs`, `tests/ApSolutions.LocalMedia.PerformanceTests/SearchBudgetTests.cs`, `tests/ApSolutions.LocalMedia.PerformanceTests/UiThreadBudgetTests.cs`, `benchmarks/ApSolutions.LocalMedia.Benchmarks/CatalogBenchmarks.cs`, `eng/run-performance.ps1`, `docs/evidence/mvp/performance-baseline.md`; modificar / modify `src/ApSolutions.LocalMedia.Presentation/Library/LibraryView.axaml`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/CatalogRepository.cs` y, si el perfil demuestra que es necesario, crear una nueva migración numerada inmediatamente después de la última del manifiesto. / Modify the library query/view and create the next sequential migration only when profile evidence requires it.

**Interfaces / Interfaces:** fixture determinista con 10.000 `MediaFile`, mezcla 60/40 episodios/películas, 10 % no disponible, 5 % duplicados y textos Unicode; hardware de referencia queda registrado, no inferido. / Deterministic 10,000-item fixture with stated distribution and recorded reference hardware.

- [x] **T10.1 RED:** fijar pruebas de ventana útil `<3 s`, primera página `<150 ms`, 60 FPS/p95 frame `<16.7 ms`, bloqueo UI por escaneo `<50 ms` y escaneo sin cambios `ProbeCount=0`; ejecutar antes de optimizar. / Fix the stated performance assertions and run before optimization.
- [x] **T10.2 Demostrar RED / Prove RED:** `pwsh ./eng/run-performance.ps1 -Baseline none`; guardar métricas iniciales aunque alguna incumpla, distinguiendo warm-up de medición. / Save initial measurements, distinguishing warm-up from measured runs.
- [x] **T10.3 GREEN:** ajustar índices, proyecciones, paginación, virtualización y tamaño de lote únicamente según perfiles; no relajar umbrales. / Tune indexes, projections, paging, virtualization, and batch size only from profiles; never relax thresholds.
- [x] **T10.4 Verificar / Verify:** cinco ejecuciones limpias, reportar mediana/p95 y prueba concurrente búsqueda+escaneo; comparar con baseline versionada. / Run five clean repetitions, report median/p95, and concurrent search+scan against versioned baseline.
- [x] **T10.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T10-10k-performance.md` contiene hardware, commit, DB, métricas y perfiles; `LIB-002` y `LIB-004` pasan a `VERIFIED` solo si todos los límites se cumplen. / Evidence contains hardware, commit, database, metrics, and profiles; verify IDs only when all budgets pass.
- [x] **T10.6 Commit:** `perf: meet 10000 item library budgets`.

**Puerta I1 / I1 gate:** carpeta heterogénea real produce biblioteca buscable; cancelación/reanudación funciona; USB/UNC ausente no borra nada; los cinco presupuestos de T10 pasan. / A heterogeneous real folder produces a searchable library; cancel/resume works; unavailable USB/UNC deletes nothing; all five T10 budgets pass.

---

## Incremento I2 — Identificación revisable y segura / Increment I2 — Reviewable, Safe Identification

**Demo utilizable / Usable demo:** una carpeta con `S01E02`, `1x02`, `Cap.803`, películas con año y nombres ruidosos produce coincidencias explicadas; los casos dudosos entran en revisión, TMDB enriquece en español y el usuario corrige, agrupa versiones o previsualiza un renombrado sin riesgo. / A folder containing `S01E02`, `1x02`, `Cap.803`, year-tagged movies, and noisy names produces explained matches; uncertain cases enter review, TMDB enriches Spanish metadata, and the user safely corrects, groups versions, or previews a rename.

### Tarea 11 — Analizador de nombres basado en propiedades / Task 11 — Property-Based Media Name Parser

**IDs:** `LIB-005`, `UX-008`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Identification/MatchModels.cs`, `src/ApSolutions.LocalMedia.Domain/Identification/MediaNameParser.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Identification/MediaNameParserTests.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Identification/MediaNameParserProperties.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Fixtures/media-name-cases.json`; modificar / modify `docs/FEATURES.md`.

**Interfaces / Interfaces:** `IMediaNameParser.Parse(FileNameContext)` devuelve `ParsedMediaName(Kind, CleanTitle, Year, Season, Episode, AbsoluteEpisode, IsSpecial, NoiseTags, ParseWarnings)` sin I/O. / Returns the stated immutable parse result without I/O.

- [x] **T11.1 RED:** parametrizar `S01E02`, `s1e2`, `1x02`, `Cap.803→S08E03` con contexto, `Cap.800→warning/special review`, temporadas escritas ES/EN, año válido, grupos `[1080p][HEVC]`, Unicode, rutas largas y entradas malformadas; propiedades: nunca lanza, no pierde el nombre original y números fuera de rango nunca se autoclasifican. / Parameterize all stated forms and properties.
- [x] **T11.2 Demostrar RED / Prove RED:** `dotnet test tests/ApSolutions.LocalMedia.Domain.Tests --filter "FullyQualifiedName~MediaNameParser"`; esperar tipo/parser ausente. / Expect missing types/parser.
- [x] **T11.3 GREEN:** implementar tokenización y reglas ordenadas sin regex con retroceso catastrófico; separar señales de conclusión y conservar advertencias para scoring/revisión. / Implement ordered tokenization/rules without catastrophic regex backtracking; retain signals and warnings.
- [x] **T11.4 Verificar / Verify:** ejecutar 10.000 casos FsCheck con semilla guardada, fixture completo y timeout de 2 s para corpus; revisar que no se crean modelos de marcadores/notas personales. / Run 10,000 seeded FsCheck cases, full fixture, and corpus timeout; confirm no personal bookmark/note model exists.
- [x] **T11.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T11-name-parser.md` enlaza fixture, semilla y resultados; `LIB-005` pasa a `VERIFIED`; `UX-008` recibe evidencia de revisión de alcance, no implementación. / Evidence links fixture, seed, and results; `UX-008` gets scope-review evidence, not implementation.
- [x] **T11.6 Commit:** `feat: parse movie and episode filenames safely`.

### Tarea 12 — Puntuación explicable y umbrales / Task 12 — Explainable Scoring and Thresholds

**IDs:** `LIB-006`, `LIB-007`, `LIB-008`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Identification/ConfidencePolicy.cs`, `src/ApSolutions.LocalMedia.Domain/Identification/CandidateScorer.cs`, `src/ApSolutions.LocalMedia.Domain/Identification/DuplicateGroupingPolicy.cs`, `src/ApSolutions.LocalMedia.Domain/Identification/IMatchCandidateRepository.cs`, `src/ApSolutions.LocalMedia.Application/Identification/IdentifyMediaFile.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/MatchCandidateRepository.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0005_match_candidates.sql`, `tests/ApSolutions.LocalMedia.Domain.Tests/Identification/CandidateScorerTests.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Identification/ConfidencePolicyTests.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Identification/DuplicateGroupingPolicyTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Identification/IdentifyMediaFileTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** `ICandidateScorer.Score(ParsedMediaName, CandidateFacts)` produce `MatchCandidate` con fórmula Scoring v1 de la sección global y señales título/temporada/episodio/año/duración; `ConfidencePolicy` clasifica `Automatic ≥0.90`, `Suggested 0.60–0.8999`, `Pending <0.60`. / Produces candidates using the global Scoring v1 formula and exact confidence bands.

- [x] **T12.1 RED:** probar fronteras `0.5999/0.60/0.8999/0.90`, penalización por contradicción, ausencia de año como señal neutral, `Cap.800` nunca automático, dos `5x10` como versiones y candidatos con explicaciones localizables, no texto final. / Test exact boundaries, contradictions, neutral missing data, ambiguous compact episode, duplicate grouping, and localizable explanation codes.
- [x] **T12.2 Demostrar RED / Prove RED:** ejecutar los cuatro filtros de identificación; esperar políticas ausentes. / Run all four identification filters; expect missing policies.
- [x] **T12.3 GREEN:** implementar pesos deterministas versionados `ScoringModelVersion=1`, clasificación pura y caso de uso que persiste candidato/estado sin consultar proveedor si la evidencia local basta. / Implement deterministic versioned weights, pure bands, and candidate persistence without needless provider calls.
- [x] **T12.4 Verificar / Verify:** pruebas mutacionales o tabla exhaustiva de señales, orden estable ante empate y repetición idempotente del mismo archivo. / Run signal-table/mutation coverage, stable tie order, and idempotent repeats.
- [x] **T12.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T12-confidence.md` publica pesos, fronteras y fixtures; los IDs quedan `IMPLEMENTED` hasta UI/proveedor/duplicados completos. / Evidence publishes weights, boundaries, and fixtures; final verification waits for downstream tasks.
- [x] **T12.6 Commit:** `feat: score and classify explainable media matches`.

### Tarea 13 — Adaptador TMDB, caché e idioma / Task 13 — TMDB Adapter, Cache, and Language

**IDs:** `LIB-006`, `LIB-011`, `PRI-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Metadata/IMetadataProvider.cs`, `src/ApSolutions.LocalMedia.Domain/Metadata/IMetadataCache.cs`, `src/ApSolutions.LocalMedia.Domain/Metadata/MetadataMergePolicy.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Metadata/TmdbMetadataProvider.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Metadata/TmdbOptions.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Metadata/TmdbRateLimiter.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Metadata/SqliteMetadataCache.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0006_metadata_cache.sql`, `src/ApSolutions.LocalMedia.Presentation/About/CreditsView.axaml`, `tests/ApSolutions.LocalMedia.IntegrationTests/Metadata/TmdbContractTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Metadata/TmdbCacheTests.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Metadata/MetadataMergePolicyTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** `IMetadataProvider.SearchAsync` y `GetDetailsAsync` aceptan `MetadataLanguage(Primary="es-ES", Fallback)`; caché normalizada guarda proveedor, clave, idioma, versión, ETag/fecha y respuesta; `MetadataMergePolicy` respeta `LockedFields`. / Defines language-aware provider/cache contracts and locked-field merge policy.

- [x] **T13.1 RED:** servidor HTTP falso cubre español, fallback configurado, caché offline, TTL/ETag, 429 con `Retry-After`, 5xx con backoff cancelable, token ausente con modo local, token nunca registrado y campos bloqueados preservados. / Fake server covers all stated provider, failure, secret, and merge cases.
- [x] **T13.2 Demostrar RED / Prove RED:** ejecutar `TmdbContractTests|TmdbCacheTests|MetadataMergePolicyTests`; esperar adaptadores ausentes. / Expect missing adapters.
- [x] **T13.3 GREEN:** implementar `HttpClient` inyectado, limitador por proveedor, caché transaccional, fallback y atribución TMDB en Créditos; leer token de variable `AP_LOCALMEDIA_TMDB_TOKEN` o recurso CI, sin proxy. / Implement injected client, provider limiter, transactional cache, fallback, credits attribution, and external token sources without a proxy.
- [x] **T13.4 Verificar / Verify:** ejecutar contrato online simulado, cortar red, repetir desde caché y escanear logs/artefactos con token señuelo; esperar cero coincidencias del secreto. / Run simulated online/offline path and scan logs/artifacts for a canary token; expect zero secret matches.
- [x] **T13.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T13-tmdb.md` contiene matriz de contrato, caché y sanitización; `LIB-006` pasa a `VERIFIED` tras integrar T14; `PRI-001` sigue parcial. / Evidence covers provider/cache/sanitization; final IDs wait as stated.
- [x] **T13.6 Commit:** `feat: enrich metadata through cached TMDB adapter`.

### Tarea 14 — Bandeja de revisión y corrección / Task 14 — Review Inbox and Correction

**IDs:** `LIB-006`, `LIB-007`, `A11Y-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Identification/ReviewInboxChanged.cs`, `src/ApSolutions.LocalMedia.Application/Identification/GetReviewInbox.cs`, `src/ApSolutions.LocalMedia.Application/Identification/ResolveMatch.cs`, `src/ApSolutions.LocalMedia.Application/Identification/RejectMatch.cs`, `src/ApSolutions.LocalMedia.Presentation/Review/ReviewInboxView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Review/ReviewInboxViewModel.cs`, `src/ApSolutions.LocalMedia.Presentation/Review/CandidateCardView.axaml`, `tests/ApSolutions.LocalMedia.Application.Tests/Identification/ReviewWorkflowTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Review/ReviewInboxTests.cs`, `tests/ApSolutions.LocalMedia.AccessibilityTests/ReviewInboxAutomationTests.cs`.

**Interfaces / Interfaces:** `ResolveMatchCommand(MediaFileId, CandidateId, ExpectedRevision)` y `RejectMatchCommand`; aplica control optimista y publica `ReviewInboxChanged`. / Defines optimistic review commands and event.

- [x] **T14.1 RED:** probar aplicación automática solo ≥90 %, filas sugeridas 60–89 %, pendientes <60 %, explicación visible no basada solo en color, corrección/rechazo por teclado, conflicto de revisión y conservación de la selección del usuario en rescaneo. / Test all thresholds, accessible explanations, keyboard actions, optimistic conflicts, and preserved choices.
- [x] **T14.2 Demostrar RED / Prove RED:** ejecutar `ReviewWorkflowTests|ReviewInboxTests|ReviewInboxAutomationTests`; esperar casos de uso/vista ausentes. / Expect missing workflow/view.
- [x] **T14.3 GREEN:** implementar consulta paginada, tarjetas candidatas, aceptar/rechazar/buscar manualmente y bloqueo de decisión manual; usar nombres/roles/estados AutomationProperties. / Implement paged inbox, candidate cards, accept/reject/manual search, decision lock, and automation metadata.
- [x] **T14.4 Verificar / Verify:** headless ES/EN, navegación completa con Tab/flechas/Enter/Escape y escenario de actualización simultánea; cero pérdida de corrección. / Run bilingual headless, full keyboard navigation, and concurrent-update scenario.
- [x] **T14.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T14-review-inbox.md` adjunta capturas, árbol UIA y resultados; `LIB-006` y `LIB-007` pasan a `VERIFIED`. / Evidence attaches captures, UIA tree, and results; both IDs may verify.
- [x] **T14.6 Commit:** `feat: review and correct uncertain matches`.

### Tarea 15 — Duplicados como versiones / Task 15 — Duplicates as Versions

**IDs:** `LIB-008`, `PLY-010`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Catalog/MediaVersionSelectionPolicy.cs`, `src/ApSolutions.LocalMedia.Application/Catalog/GroupMediaVersions.cs`, `SetPreferredVersion.cs`, `src/ApSolutions.LocalMedia.Presentation/Review/DuplicateReviewView.axaml`, `DuplicateReviewViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Catalog/MediaVersionSelectionTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Catalog/GroupMediaVersionsTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Review/DuplicateReviewTests.cs`.

**Interfaces / Interfaces:** `MediaVersionSelectionPolicy.Select(versions)` ordena disponibilidad, decisión manual, resolución, HDR preferido configurable, códec y tamaño; ninguna operación devuelve una orden de borrado. / Selects by the stated factors and never returns a delete command.

- [x] **T15.1 RED:** probar dos `5x10`, ediciones con duración distinta, versión no disponible, preferencia manual, empate estable, todos los archivos visibles y ausencia de acción de borrado/ocultación. / Test duplicate grouping, editions, unavailable/manual/tie behavior, visibility, and no deletion.
- [x] **T15.2 Demostrar RED / Prove RED:** ejecutar filtros `MediaVersionSelection|GroupMediaVersions|DuplicateReview`; esperar política/caso de uso/vista ausentes. / Expect missing components.
- [x] **T15.3 GREEN:** implementar agrupación confirmable, selección preferida y panel que enumera ruta abreviada, calidad y disponibilidad; conservar relación de cada archivo. / Implement confirmable grouping, preferred selection, and a panel listing every file and attributes.
- [x] **T15.4 Verificar / Verify:** reagrupar idempotentemente, desconectar la preferida y comprobar fallback sin cambiar preferencia almacenada; reconectar y recuperar. / Test idempotent regrouping and temporary fallback/recovery.
- [x] **T15.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T15-duplicates.md` enlaza fixtures y capturas; `LIB-008` pasa a `VERIFIED`, `PLY-010` sigue parcial hasta T27. / Evidence links fixtures/captures; progress transfer remains partial.
- [x] **T15.6 Commit:** `feat: preserve duplicates as selectable media versions`.

### Tarea 16 — Edición protegida de metadatos e imágenes / Task 16 — Protected Metadata and Artwork Editing

**IDs:** `LIB-011`, `DAT-002`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Metadata/UpdateMetadata.cs`, `RefreshMetadata.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Metadata/ArtworkCache.cs`, `src/ApSolutions.LocalMedia.Presentation/Metadata/MetadataEditorView.axaml`, `MetadataEditorViewModel.cs`, `ArtworkPickerViewModel.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Metadata/MetadataEditingTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Metadata/ArtworkCacheTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Metadata/MetadataEditorTests.cs`.

**Interfaces / Interfaces:** `UpdateMetadataCommand(TitleId, FieldChanges, LockedFields, ExpectedRevision)`; arte local elegida se trata como dato personal exportable, arte remota como caché regenerable. / Defines optimistic field edits and distinguishes exportable user artwork from regenerable remote cache.

- [x] **T16.1 RED:** probar edición/bloqueo/desbloqueo por campo, refresh remoto que preserva bloqueos, conflicto optimista, imagen local copiada al área de datos personales, imagen remota regenerable y texto alternativo. / Test field locks, remote refresh, conflicts, artwork semantics, and alt text.
- [x] **T16.2 Demostrar RED / Prove RED:** ejecutar `MetadataEditingTests|ArtworkCacheTests|MetadataEditorTests`; esperar casos de uso/vista ausentes. / Expect missing use cases/view.
- [x] **T16.3 GREEN:** implementar comandos transaccionales, merge de T13, almacenamiento separado `personal-artwork/` y `cache/artwork/`, editor accesible y acción explícita de restaurar proveedor. / Implement transactional commands, merge policy, separated artwork stores, accessible editor, and provider reset.
- [x] **T16.4 Verificar / Verify:** tres ciclos editar→refrescar→reiniciar, borrar caché remota y regenerar, conservar arte personal; prueba ES/EN y lector de pantalla. / Run repeated edit/refresh/restart, cache regeneration, personal-art preservation, bilingual and screen-reader checks.
- [x] **T16.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T16-metadata-editing.md`; `LIB-011` pasa a `VERIFIED`, inclusión en exportación se verifica en T36. / Evidence recorded; export inclusion closes later.
- [x] **T16.6 Commit:** `feat: edit and lock catalog metadata safely`.

### Tarea 17 — Renombrado con previsualización, auditoría y deshacer / Task 17 — Rename Preview, Audit, and Undo

**IDs:** `LIB-012`, `PRI-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Discovery/RenameOperation.cs`, `src/ApSolutions.LocalMedia.Domain/Discovery/RenamePolicy.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/ISafeFileRenamer.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/PreviewRename.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/ExecuteRename.cs`, `src/ApSolutions.LocalMedia.Application/Discovery/UndoRename.cs`, `src/ApSolutions.LocalMedia.Infrastructure/FileSystem/SafeFileRenamer.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0007_rename_log.sql`, `src/ApSolutions.LocalMedia.Presentation/Metadata/RenamePreviewView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Metadata/RenamePreviewViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Discovery/RenamePolicyTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Discovery/RenameTransactionTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** `RenamePlan` contiene operaciones origen/destino, conflictos y capacidad de deshacer; `ISafeFileRenamer.ExecuteAsync(plan, token)` solo recibe rutas ya normalizadas dentro de una raíz. / Defines prevalidated plans and a root-confined renamer.

- [x] **T17.1 RED:** probar caracteres inválidos/reservados, rutas largas, destino fuera de raíz, conflicto de lote, sensibilidad de mayúsculas, fallo UNC a mitad, previsualización sin I/O, log por elemento y deshacer solo cuando origen/destino siguen seguros. / Test all path, conflict, network-failure, preview, audit, and undo conditions.
- [x] **T17.2 Demostrar RED / Prove RED:** ejecutar `RenamePolicyTests|RenameTransactionTests`; esperar política/renamer ausentes. / Expect missing policy/renamer.
- [x] **T17.3 GREEN:** implementar prevalidación completa, nombres deterministas, ejecución secuencial conservadora, reconciliación tras cada éxito y recuperación guiada; no mover carpetas, no construir comandos de shell y no prometer atomicidad UNC. / Implement full validation, deterministic names, conservative execution, reconciliation, and guided recovery without folder moves or shell commands.
- [x] **T17.4 Verificar / Verify:** ejecutar lote local exitoso+undo, conflicto que realiza cero operaciones y UNC simulado que falla tras una operación dejando log recuperable; inventario de contenidos se conserva. / Test local success/undo, zero-op conflict, and recoverable partial UNC result while preserving content inventory.
- [x] **T17.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T17-safe-rename.md` contiene previews, log y recuperación; `LIB-012` pasa a `VERIFIED`. / Evidence contains previews, audit, and recovery; ID may verify.
- [x] **T17.6 Commit:** `feat: preview audit and undo safe media renames`.

**Puerta I2 / I2 gate:** el corpus aprobado se clasifica según umbrales exactos, la revisión es operable con teclado, TMDB puede apagarse sin inutilizar la biblioteca, ningún duplicado se borra y los tres escenarios de renombrado de T17 pasan. / The approved corpus follows exact thresholds, review is keyboard operable, TMDB can be disabled without breaking the library, no duplicate is deleted, and all T17 rename scenarios pass.

---

## Incremento I3 — Reproducción integrada real / Increment I3 — Real Embedded Playback

**Demo utilizable / Usable demo:** desde una ficha se abre una única sesión integrada, reproduce la matriz base MP4/MKV/AVI/MOV/WebM, cambia pistas/controles/modos y ofrece apertura externa con mensajes accionables cuando el motor no puede reproducir. / From a details page, one embedded session opens, plays the base MP4/MKV/AVI/MOV/WebM matrix, changes tracks/controls/window modes, and offers actionable external fallback when the engine cannot play.

### Tarea 18 — Contrato de motor y riesgo Avalonia/LibVLC / Task 18 — Engine Contract and Avalonia/LibVLC Risk

**IDs:** `PLY-001`, `PLY-007`, `PRD-004`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Playback/PlaybackContracts.cs`, `src/ApSolutions.LocalMedia.Application/Playback/PlaybackEvents.cs`, `src/ApSolutions.LocalMedia.Application/Playback/StartPlayback.cs`, `src/ApSolutions.LocalMedia.Application/Playback/StopPlayback.cs`, `src/ApSolutions.LocalMedia.Application/Playback/PlaybackSessionCoordinator.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Playback/LibVlcMediaPlayerEngine.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Playback/LibVlcFactory.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/PlayerView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/PlayerViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Playback/PlaybackContractTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Playback/PlaybackSessionCoordinatorTests.cs`, `tests/ApSolutions.LocalMedia.MediaTests/Playback/LibVlcSmokeTests.cs`.

**Interfaces / Interfaces:** `IMediaPlayerEngine.InitializeAsync`, `OpenAsync(PlaybackRequest)`, `PlayAsync`, `PauseAsync`, `SeekAsync`, `StopAsync`, `GetSnapshotAsync`; eventos tipados `StateChanged`, `PositionChanged`, `Failure`; una instancia activa controlada por `IPlaybackSessionCoordinator`. / Defines the engine lifecycle, typed events, and one-active-session coordinator.

- [x] **T18.1 RED:** motor falso prueba transición Idle→Opening→Playing→Paused→Stopped, cancelación, fallo de apertura liberando recursos, segundo inicio que detiene/confirma el primero y dominio sin tipos LibVLC/Avalonia/Windows. / Fake engine tests lifecycle, cancellation, failure cleanup, singleton coordination, and framework-free domain.
- [x] **T18.2 Demostrar RED / Prove RED:** ejecutar `PlaybackContractTests|PlaybackSessionCoordinatorTests`; esperar contratos/coordinador ausentes. / Expect missing contracts/coordinator.
- [x] **T18.3 GREEN:** implementar contrato, coordinador y adaptador mínimo; inicializar LibVLC una vez, disponer handles determinísticamente, renderizar superficie de vídeo y controles Avalonia sin meter `MediaPlayer` en ViewModel. / Implement the contract, coordinator, minimal adapter, deterministic handles, and Avalonia video surface without framework engine objects in ViewModels.
- [x] **T18.4 Verificar / Verify:** smoke manual/automatizado con muestra H.264, 50 ciclos abrir/cerrar, cambio de ruta durante Opening y cierre inesperado; registrar handles/proceso y cero fugas crecientes. / Run H.264 smoke, 50 open/close cycles, route change during opening, and forced close; record handle/process behavior with no growing leak.
- [x] **T18.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T18-libvlc-spike.md` documenta superposición de controles, superficie, consumo y decisión continuar/sustituir detrás del mismo contrato; no avanzar si overlay o liberación falla. / Evidence documents overlay, surface, resources, and continue/replace decision behind the same contract; do not proceed on overlay or cleanup failure.
- [x] **T18.6 Commit:** `feat: embed playback behind a replaceable engine`.

### Tarea 19 — Matriz legal de contenedores y códecs / Task 19 — Legal Container and Codec Matrix

**IDs:** `PLY-001`, `PLY-002`, `PRD-005`.

**Archivos / Files:** crear / create `eng/generate-test-media.ps1`, `tests/ApSolutions.LocalMedia.MediaTests/Fixtures/media-manifest.json`, `tests/ApSolutions.LocalMedia.MediaTests/Playback/CodecMatrixTests.cs`, `tests/ApSolutions.LocalMedia.MediaTests/Playback/CorruptMediaTests.cs`, `docs/development/media-fixtures.es.md`, `docs/development/media-fixtures.en.md`, `docs/release/THIRD-PARTY-NOTICES.es.md`, `docs/release/THIRD-PARTY-NOTICES.en.md`; modificar / modify `eng/verify.ps1`.

**Interfaces / Interfaces:** manifiesto por muestra: licencia/origen o receta generada, contenedor, códec vídeo/audio, resolución, duración, pistas, HDR, hash y resultado esperado `Playable` o `ActionableUnsupported`. / Media manifest records provenance, technical facts, hash, and expected outcome.

- [x] **T19.1 RED:** definir muestras mínimas MP4/H.264-AAC, MKV/HEVC-EAC3, AVI/MPEG4-MP3, MOV/H.264-PCM, WebM/VP9-Opus, MKV/AV1-Opus y archivos truncado/sin pista/codec ausente; probar inicio A/V, duración aproximada, final y diagnóstico. / Define and test the explicit playable/error matrix.
- [x] **T19.2 Demostrar RED / Prove RED:** generar activos en `artifacts/test-media`, ejecutar `CodecMatrixTests|CorruptMediaTests`; esperar fallos de capacidades/diagnóstico aún ausentes. / Generate assets and expect missing capability/diagnostic behavior.
- [x] **T19.3 GREEN:** mapear eventos/errores LibVLC a códigos de dominio localizables, detectar ausencia de pista y ofrecer reintento/otra versión/apertura externa sin eliminar la entidad. / Map engine failures to localizable domain codes and actionable recovery without deleting entities.
- [x] **T19.4 Verificar / Verify:** ejecutar matriz en software y aceleración cuando aplique, comprobar hashes/procedencia y que `git status` no incluya medios generados; validar avisos/licencias. / Run matrix, verify provenance/hashes, confirm generated media is untracked, and validate notices/licenses.
- [x] **T19.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T19-codec-matrix.md` presenta una fila por muestra y hardware; `PLY-002` pasa a `VERIFIED` si cada fila reproduce o explica incompatibilidad. / Evidence provides one row per sample and hardware; verify when every row passes or gives the expected explanation.
- [x] **T19.6 Commit:** `test: add reproducible licensed playback matrix`.

### Tarea 20 — Pistas, subtítulos y preferencias por ámbito / Task 20 — Tracks, Subtitles, and Scoped Preferences

**IDs:** `PLY-004`, `PLY-005`, `A11Y-002`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Continuity/PlaybackPreference.cs`, `src/ApSolutions.LocalMedia.Domain/Continuity/PreferenceResolutionPolicy.cs`, `src/ApSolutions.LocalMedia.Domain/Continuity/IPlaybackPreferenceRepository.cs`, `src/ApSolutions.LocalMedia.Application/Playback/SelectTrack.cs`, `src/ApSolutions.LocalMedia.Application/Playback/ApplyPlaybackPreferences.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Playback/ExternalSubtitleDiscovery.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/PlaybackPreferenceRepository.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0008_playback_preferences.sql`, `src/ApSolutions.LocalMedia.Presentation/Player/TrackSelectorView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/TrackSelectorViewModel.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/SubtitleStyleView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/SubtitleStyleViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Continuity/PreferenceResolutionTests.cs`, `tests/ApSolutions.LocalMedia.MediaTests/Playback/TrackAndSubtitleTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Player/SubtitleStyleTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** resolución `File > Series > Global > EngineDefault`; `PlaybackPreference` guarda idiomas, pista preferida por características, velocidad, boost, salida y estilo de subtítulo; descubrimiento externo solo mismo directorio/base con `.srt,.ass,.vtt`. / Defines explicit preference precedence and subtitle discovery allowlist.

- [x] **T20.1 RED:** probar precedencia, pista ausente con fallback por idioma/canal, audio/subtítulo interno, SRT/ASS/VTT externos, codificaciones UTF-8/UTF-16, archivo externo fuera de raíz rechazado y reaplicación al siguiente episodio. / Test precedence, track fallback, internal/external formats and encodings, root confinement, and next-episode reapplication.
- [x] **T20.2 Demostrar RED / Prove RED:** ejecutar `PreferenceResolutionTests|TrackAndSubtitleTests|SubtitleStyleTests`; esperar políticas/selectores ausentes. / Expect missing policies/selectors.
- [x] **T20.3 GREEN:** implementar persistencia por ámbito, selección resiliente por atributos en lugar de índice volátil, carga segura externa y UI accesible; añadir tamaño, familia segura, color, fondo, borde y opacidad de subtítulos. / Implement scoped persistence, attribute-based resilient selection, safe external loading, and accessible subtitle customization.
- [x] **T20.4 Verificar / Verify:** dos episodios con orden de pistas distinto, reinicio, escalado 200 %, alto contraste y Narrator; preferencia se reaplica sin ocultar texto. / Test reordered tracks across episodes, restart, scaling, high contrast, and Narrator.
- [x] **T20.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T20-tracks-subtitles.md` enlaza matriz y capturas; `PLY-005` pasa a `VERIFIED`; `PLY-004` y `A11Y-002` siguen parciales. / Evidence links matrix/captures; downstream IDs remain partial.
- [x] **T20.6 Commit:** `feat: persist audio and subtitle preferences by scope`.

### Tarea 21 — Controles, velocidad, saltos y limitador / Task 21 — Controls, Speed, Skips, and Limiter

**IDs:** `PLY-006`, `PLY-014`, `A11Y-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Playback/PlaybackControlPolicy.cs`, `VolumeBoostPolicy.cs`, `src/ApSolutions.LocalMedia.Application/Playback/ControlPlayback.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Playback/PeakLimiterAudioFilter.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/TransportControlsView.axaml`, `TransportControlsViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Playback/PlaybackControlPolicyTests.cs`, `VolumeBoostPolicyTests.cs`, `tests/ApSolutions.LocalMedia.MediaTests/Playback/PeakLimiterTests.cs`, `tests/ApSolutions.LocalMedia.AccessibilityTests/TransportControlsAutomationTests.cs`.

**Interfaces / Interfaces:** velocidades permitidas `0.25–4.0`, saltos configurables positivos hacia atrás/adelante con valores iniciales `10/30 s`, volumen normal `0–100 %`, boost `101–200 %` siempre con limitador y advertencia. / Defines exact control ranges/defaults and mandatory limiter/warning for boost.

- [x] **T21.1 RED:** probar clamp de seek/velocidad/volumen, silencio reversible, saltos cerca de límites, boost persistido por archivo, señal sintética que nunca supera el pico normalizado y controles con nombre/estado/atajo. / Test clamps, mute, boundary skips, per-file boost, normalized peak, and accessible controls.
- [x] **T21.2 Demostrar RED / Prove RED:** ejecutar `PlaybackControlPolicyTests|VolumeBoostPolicyTests|PeakLimiterTests|TransportControlsAutomationTests`; esperar políticas/filtro/UI ausentes. / Expect missing policies/filter/UI.
- [x] **T21.3 GREEN:** implementar comandos serializados contra sesión activa, filtro limitador o mecanismo LibVLC equivalente demostrado, indicador visual+texto y controles que se ocultan sin perder foco lógico. / Implement serialized active-session controls, demonstrated limiter, text+visual warning, and focus-safe controls.
- [x] **T21.4 Verificar / Verify:** matriz de velocidades/saltos, señal de barrido, teclado/ratón y 100 cambios rápidos; posición/estado final determinista, cero excepción. / Run speeds/skips, sweep signal, keyboard/mouse, and 100 rapid changes with deterministic final state.
- [x] **T21.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T21-controls-limiter.md` contiene waveform/picos y árbol UIA; `PLY-006` pasa a `VERIFIED`, input total cierra en T24. / Evidence contains waveform/peaks and UIA tree; full input closes later.
- [x] **T21.6 Commit:** `feat: add accessible playback controls and peak-limited boost`.

### Tarea 22 — Aceleración, HDR10 y conversión SDR / Task 22 — Acceleration, HDR10, and SDR Tone Mapping

**IDs:** `PLY-003`, `PLY-015`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Playback/VideoOutputPolicy.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Playback/LibVlcVideoCapabilities.cs`, `HardwareAccelerationFallback.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/VideoStatusOverlay.axaml`, `VideoStatusViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Playback/VideoOutputPolicyTests.cs`, `tests/ApSolutions.LocalMedia.MediaTests/Playback/HdrAccelerationTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Player/VideoStatusOverlayTests.cs`, `docs/evidence/mvp/hardware-video-matrix.md`.

**Interfaces / Interfaces:** `PlaybackCapabilities` informa aceleración solicitada/activa, HDR de fuente/pantalla y ruta HDR passthrough o SDR tone-map; Dolby Vision retorna `UnsupportedCapability` en MVP. / Reports requested/active acceleration, source/display HDR, output path, and explicit Dolby Vision unsupported capability.

- [x] **T22.1 RED:** probar HDR10→HDR cuando pantalla compatible, HDR10→tone-map SDR cuando no, aceleración fallida→software sin caída, indicador basado en estado real y Dolby Vision no anunciado/seleccionable. / Test HDR/tone-map/fallback/indicator and explicit Dolby boundary.
- [x] **T22.2 Demostrar RED / Prove RED:** ejecutar `VideoOutputPolicyTests|HdrAccelerationTests|VideoStatusOverlayTests`; aceptar SKIP documentado solo si el agente de CI carece de HDR, nunca PASS simulado de hardware. / Allow documented SKIP only for missing CI HDR hardware, never simulated hardware PASS.
- [x] **T22.3 GREEN:** implementar consulta de capacidades, opciones LibVLC revisadas, fallback una vez y overlay accionable; no añadir rutas Dolby Vision ni passthrough. / Implement capability query, reviewed LibVLC options, one-time fallback, and actionable overlay without Dolby paths.
- [x] **T22.4 Verificar / Verify:** ejecutar en GPU integrada y discreta disponibles, pantalla HDR y SDR; capturar logs sanitizados, frames de referencia y estado reportado. / Run on available integrated/discrete GPUs and HDR/SDR displays; capture sanitized logs, reference frames, and reported state.
- [x] **T22.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T22-hdr-acceleration.md`; `PLY-003` solo pasa a `VERIFIED` con matriz física aprobada. `PLY-015` recibe revisión de alcance `POST_STABLE/OUT_OF_SCOPE`, sin código. / Physical matrix is required; Dolby gets scope-review evidence only.
- [x] **T22.6 Commit:** `feat: report HDR and acceleration with safe fallback`.

### Tarea 23 — Dispositivos y canales de audio / Task 23 — Audio Devices and Channels

**IDs:** `PLY-004`, `PLY-015`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Playback/AudioOutputPolicy.cs`, `src/ApSolutions.LocalMedia.Domain/Playback/IAudioDeviceCatalog.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Playback/WindowsAudioDeviceCatalog.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Playback/LibVlcAudioOutputAdapter.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/AudioOutputView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/AudioOutputViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Playback/AudioOutputPolicyTests.cs`, `tests/ApSolutions.LocalMedia.MediaTests/Playback/AudioChannelTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Playback/AudioDeviceLifecycleTests.cs`.

**Interfaces / Interfaces:** `IAudioDeviceCatalog.GetOutputsAsync`, selección por ID estable con fallback predeterminado; capacidades `Stereo`, `5.1`, `7.1`; passthrough retorna no admitido en MVP. / Defines stable-ID output selection, fallback, supported channel layouts, and unsupported passthrough.

- [x] **T23.1 RED:** probar estéreo/5.1/7.1, dispositivo desaparecido durante reproducción, cambio seguro, persistencia/reinicio, fallback a predeterminado y ausencia de bitstream Dolby/DTS en opciones. / Test layouts, hot removal/switch, persistence/fallback, and no passthrough option.
- [x] **T23.2 Demostrar RED / Prove RED:** ejecutar `AudioOutputPolicyTests|AudioChannelTests|AudioDeviceLifecycleTests`; usar adaptador falso para RED determinista y hardware físico para evidencia final. / Use fake adapter for deterministic RED and physical hardware for final evidence.
- [x] **T23.3 GREEN:** implementar catálogo Windows aislado, adaptación LibVLC, cambio serializado con pausa/reanudación y mensaje si el layout se degrada. / Implement isolated Windows catalog, LibVLC mapping, serialized switch, and degraded-layout message.
- [x] **T23.4 Verificar / Verify:** matriz altavoces/HDMI/auriculares disponible, conectar/desconectar y comparar canales de la muestra; cero crash y preferencia coherente. / Run available speaker/HDMI/headphone matrix with hot-plug and channel verification.
- [x] **T23.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T23-audio-output.md`; `PLY-004` pasa a `VERIFIED` con matriz física aprobada; `PLY-015` sigue fuera de alcance. / Physical approval verifies audio; passthrough remains out of scope.
- [x] **T23.6 Commit:** `feat: select persistent multichannel audio output`.

### Tarea 24 — Pantalla completa, mini reproductor y entradas / Task 24 — Fullscreen, Mini Player, and Input

**IDs:** `PLY-007`, `PLY-014`, `SYS-001`, `A11Y-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Playback/IInputCommandRouter.cs`, `src/ApSolutions.LocalMedia.Application/Playback/ChangePlaybackMode.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/PlayerWindowCoordinator.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/MiniPlayerWindow.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/ShortcutMap.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/ShortcutSettingsView.axaml`, `src/ApSolutions.LocalMedia.Windows/MediaKeys/WindowsMediaKeyService.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Playback/PlaybackModeTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Player/WindowLifecycleTests.cs`, `tests/ApSolutions.LocalMedia.AccessibilityTests/PlaybackInputTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Windows/MediaKeyTests.cs`.

**Interfaces / Interfaces:** modos `Embedded`, `Fullscreen`, `Mini`; mini `Topmost=true`; `IInputCommandRouter` resuelve teclado/ratón/SMTC sin duplicar acciones, detecta conflictos y permite restaurar valores. / Defines three modes and one conflict-aware input command router.

- [x] **T24.1 RED:** probar transición entre tres modos conservando motor/posición/preferencias, una ventana/sesión activa, Escape/foco, atajos reconfigurables sin conflicto, botones de ratón, play/pause/next de teclas multimedia y servicio liberado al cerrar. / Test mode preservation, singleton, focus/Escape, configurable conflict-free shortcuts, mouse/media keys, and cleanup.
- [x] **T24.2 Demostrar RED / Prove RED:** ejecutar `PlaybackModeTests|WindowLifecycleTests|PlaybackInputTests|MediaKeyTests`; esperar coordinadores/servicio ausentes. / Expect missing coordinators/service.
- [x] **T24.3 GREEN:** mover la misma superficie/sesión mediante coordinador, persistir geometría visible, registrar SMTC solo durante sesión y presentar editor de atajos con validación inmediata. / Move the same session/surface through a coordinator, persist visible geometry, register SMTC only during playback, and validate shortcuts live.
- [x] **T24.4 Verificar / Verify:** 100 cambios de modo, dos monitores/DPI distintos cuando estén disponibles, solo teclado y Narrator; confirmar posición continua y un único engine. / Run 100 mode changes, available multi-monitor/DPI, keyboard-only, and Narrator checks with continuous position and one engine.
- [x] **T24.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T24-windows-input.md` contiene lifecycle, mapa de atajos y árbol UIA; `PLY-007` y `PLY-014` pasan a `VERIFIED`; `SYS-001` solo cubre teclas, no bandeja/inicio. / Evidence covers lifecycle, shortcuts, and UIA; tray/startup remains partial.
- [x] **T24.6 Commit:** `feat: preserve playback across windows and input methods`.

**Puerta I3 / I3 gate:** la matriz T19 completa reproduce o diagnostica, los recursos del motor no crecen tras 50 ciclos, HDR/audio tienen resultado físico o bloqueo explícito de hardware, y todas las acciones esenciales del reproductor funcionan sin ratón. / The full T19 matrix plays or diagnoses, engine resources do not grow after 50 cycles, HDR/audio have a physical result or explicit hardware block, and all essential player actions work without a mouse.

---

## Incremento I4 — Continuidad fiable / Increment I4 — Reliable Continuity

**Demo utilizable / Usable demo:** cerrar o interrumpir una película/episodio permite reanudar dentro de ±5 s; estados manuales prevalecen, el progreso cambia entre versiones seguras, el siguiente episodio inicia tras cuenta atrás cancelable y las marcas manuales aparecen solo en rangos válidos. / Closing or interrupting a movie/episode resumes within ±5 s; manual states win, progress transfers across safe versions, next episode starts after a cancelable countdown, and manual markers appear only in valid ranges.

### Tarea 25 — Persistencia atómica de posición / Task 25 — Atomic Position Persistence

**IDs:** `PLY-008`, `DAT-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Continuity/ContinuityModels.cs`, `src/ApSolutions.LocalMedia.Domain/Continuity/ProgressPolicy.cs`, `src/ApSolutions.LocalMedia.Domain/Continuity/IWatchStateRepository.cs`, `src/ApSolutions.LocalMedia.Application/Continuity/PlaybackProgressTracker.cs`, `src/ApSolutions.LocalMedia.Application/Continuity/ResumePlayback.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/WatchStateRepository.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0009_watch_state.sql`, `src/ApSolutions.LocalMedia.Presentation/Player/ResumePromptView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/ResumePromptViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Continuity/ProgressPolicyTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Continuity/ProgressTrackerTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Continuity/CrashResumeTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** `WatchState` conserva `Title/EpisodeId`, posición, duración observada, versión origen, estado, timestamps y override; posiciones `<30 s` no producen reanudación; `PlaybackProgressTracker` persiste cada `5 s` y en `Pause`, `Seek`, `ModeChange`, `FileChange`, `Close`, `EngineFailure`. / Defines watch state, the 30-second minimum resume point, and exact persistence triggers.

- [x] **T25.1 RED:** reloj falso prueba tick exacto, triggers, debounce de escrituras, posiciones triviales cercanas a inicio ignoradas, clamp `0..duration`, escritura atómica y prompt reanudar/reiniciar; proceso hijo forzado reanuda a ±5 s. / Fake-clock and child-process tests cover every persistence rule and ±5 s recovery.
- [x] **T25.2 Demostrar RED / Prove RED:** ejecutar `ProgressPolicyTests|ProgressTrackerTests|CrashResumeTests`; esperar modelo/tracker/repositorio ausentes. / Expect missing model/tracker/repository.
- [x] **T25.3 GREEN:** implementar tracker con `PeriodicTimer` inyectable, cola serial de último valor, flush con timeout en eventos críticos y repositorio UPSERT transaccional; no bloquear hilo UI. / Implement injectable timer, serialized latest-value queue, bounded critical flush, and transactional upsert off the UI thread.
- [x] **T25.4 Verificar / Verify:** 20 cierres forzados en segundos aleatorios, pausa/seek/modo/cambio de archivo, DB caliente y fallo de motor; medir error absoluto máximo ≤5 s. / Run 20 randomized forced closes and all triggers; max absolute resume error must be ≤5 s.
- [x] **T25.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T25-progress-resume.md` publica tabla de 20 ensayos, error y transacciones; `PLY-008` pasa a `VERIFIED`. / Evidence publishes all trials and transactions; verify exact progress.
- [x] **T25.6 Commit:** `feat: persist and resume playback within five seconds`.

### Tarea 26 — Máquina de estados y umbral visto / Task 26 — Watch State Machine and Threshold

**IDs:** `PLY-009`, `UX-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Continuity/WatchStatePolicy.cs`, `src/ApSolutions.LocalMedia.Application/Continuity/SetWatchStatus.cs`, `ConfigureWatchedThreshold.cs`, `src/ApSolutions.LocalMedia.Presentation/Catalog/WatchStatusControl.axaml`, `WatchStatusViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Continuity/WatchStatePolicyTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Continuity/ManualWatchOverrideTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Catalog/WatchStatusControlTests.cs`.

**Interfaces / Interfaces:** estados `NotStarted`, `InProgress`, `Watched`; `InProgress` comienza al menor de `60 s` o `2 %`; umbral global visto predeterminado `0.90`, configurable `0.50..1.00`; override manual persiste hasta acción manual inversa. / Defines exact states, significant-progress threshold, watched default/range, and persistent manual override.

- [x] **T26.1 RED:** probar avance trivial, avance significativo, fronteras 89.99/90 %, cambio de umbral, duración desconocida, marcar visto/no visto, reproducción posterior que no pisa override y reinicio. / Test transitions, exact threshold boundary, unknown duration, manual overrides, later playback, and restart.
- [x] **T26.2 Demostrar RED / Prove RED:** ejecutar `WatchStatePolicyTests|ManualWatchOverrideTests|WatchStatusControlTests`; esperar política/comandos/control ausentes. / Expect missing policy/commands/control.
- [x] **T26.3 GREEN:** implementar política pura, comandos transaccionales y control accesible con icono+texto; recalcular solo estados automáticos cuando cambia umbral. / Implement pure policy, transactional commands, accessible icon+text control, and automatic-only recalculation.
- [x] **T26.4 Verificar / Verify:** tabla exhaustiva de porcentajes 0–100, concurrencia progreso+override y snapshots ES/EN; override siempre gana. / Run exhaustive percentage table, progress/override race, and bilingual snapshots; override always wins.
- [x] **T26.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T26-watch-status.md`; `PLY-009` pasa a `VERIFIED`; Home usa estos estados en T30. / Evidence recorded; Home consumes state later.
- [x] **T26.6 Commit:** `feat: track watched state with manual overrides`.

### Tarea 27 — Transferencia de progreso entre versiones / Task 27 — Progress Transfer Across Versions

**IDs:** `PLY-010`, `LIB-008`, `LIB-009`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Continuity/ProgressTransferPolicy.cs`, `src/ApSolutions.LocalMedia.Application/Continuity/SwitchMediaVersion.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/VersionSwitchDialog.axaml`, `VersionSwitchViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Continuity/ProgressTransferPolicyTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Continuity/SwitchMediaVersionTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Player/VersionSwitchDialogTests.cs`.

**Interfaces / Interfaces:** `ProgressTransferDecision` es `Exact(second)`, `Proportional(second)`, `Confirm(suggestedSecond, reason)` o `Restart`; tolerancia exacta predeterminada `max(5 s, 1 %)` y proporción solo si diferencia `≤10 %` y estructura técnica compatible. / Defines exact decision variants and thresholds.

- [x] **T27.1 RED:** probar duraciones iguales, diferencia dentro de tolerancia, 2–10 % proporcional, >10 % confirmación, duración desconocida, ediciones con capítulos/pistas incompatibles, cancelación y auditoría de versión origen. / Test every boundary, unknown/incompatible editions, cancellation, and source audit.
- [x] **T27.2 Demostrar RED / Prove RED:** ejecutar `ProgressTransferPolicyTests|SwitchMediaVersionTests|VersionSwitchDialogTests`; esperar política/caso de uso ausentes. / Expect missing policy/use case.
- [x] **T27.3 GREEN:** implementar decisión pura, confirmación accesible y cambio atómico que guarda sesión anterior antes de abrir nueva; no alterar el progreso si la nueva versión falla. / Implement pure decision, accessible confirmation, and atomic save-before-open without changing progress on failure.
- [x] **T27.4 Verificar / Verify:** fixtures de versiones T15, fallos de apertura y retorno a versión original; segundo esperado y origen quedan auditables. / Run T15 version fixtures, open failures, and rollback while preserving expected second and audit.
- [x] **T27.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T27-version-progress.md`; `PLY-010` pasa a `VERIFIED`. / Evidence recorded; progress transfer verifies.
- [x] **T27.6 Commit:** `feat: transfer progress safely between media versions`.

### Tarea 28 — Siguiente episodio con cuenta atrás / Task 28 — Next Episode Countdown

**IDs:** `PLY-011`, `PLY-014`, `LIB-010`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Continuity/NextEpisodePolicy.cs`, `src/ApSolutions.LocalMedia.Application/Continuity/GetNextEpisode.cs`, `StartNextEpisodeCountdown.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/NextEpisodeOverlay.axaml`, `NextEpisodeViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Continuity/NextEpisodePolicyTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Continuity/NextEpisodeCountdownTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Player/NextEpisodeOverlayTests.cs`.

**Interfaces / Interfaces:** orden estándar temporada/episodio con especiales explícitos; cuenta predeterminada `10 s`, configurable `0` (desactivada) a `60 s`, cancelable; solo selecciona versión disponible. / Defines ordering, explicit specials, configurable countdown, cancellation, and available-only selection.

- [x] **T28.1 RED:** probar siguiente episodio normal, final de temporada, especial, hueco, archivo no disponible durante cuenta, cancelación teclado/ratón, preferencia desactivada y volver a ficha cuando no hay siguiente reproducible. / Test all ordering, availability, cancellation, disabled, and fallback cases.
- [x] **T28.2 Demostrar RED / Prove RED:** ejecutar `NextEpisodePolicyTests|NextEpisodeCountdownTests|NextEpisodeOverlayTests`; esperar política/overlay ausentes. / Expect missing policy/overlay.
- [x] **T28.3 GREEN:** implementar consulta ordenada, temporizador inyectable y overlay con anuncio no intrusivo para lector; revalidar disponibilidad en cero antes de abrir. / Implement ordered query, injectable timer, accessible announcement, and last-moment availability validation.
- [x] **T28.4 Verificar / Verify:** simular retirada de USB a 1 s, cancelar en cada método de entrada y encadenar tres episodios; nunca abre dos sesiones. / Remove USB at one second, cancel through each input, and chain three episodes without concurrent sessions.
- [x] **T28.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T28-next-episode.md`; `PLY-011` pasa a `VERIFIED`. / Evidence recorded; next episode verifies.
- [x] **T28.6 Commit:** `feat: play the next available episode after countdown`.

### Tarea 29 — Marcas manuales de introducción y créditos / Task 29 — Manual Intro and Credits Markers

**IDs:** `PLY-012`, `PLY-013`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Continuity/IntroMarker.cs`, `src/ApSolutions.LocalMedia.Domain/Continuity/MarkerPolicy.cs`, `src/ApSolutions.LocalMedia.Domain/Continuity/IIntroMarkerRepository.cs`, `src/ApSolutions.LocalMedia.Application/Continuity/SaveManualMarker.cs`, `src/ApSolutions.LocalMedia.Application/Continuity/DeleteManualMarker.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/IntroMarkerRepository.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0010_intro_markers.sql`, `src/ApSolutions.LocalMedia.Presentation/Player/SkipMarkerButton.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/MarkerEditorView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/MarkerEditorViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Continuity/MarkerPolicyTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Continuity/ManualMarkerTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Player/MarkerUiTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** `IntroMarker(SeriesId, MarkerKind Intro|Recap|Credits, Start, End, Origin Manual|Detected, Confidence, UserCorrected)`; en MVP solo se crea `Origin=Manual`, por serie con override futuro compatible. / Defines the forward-compatible marker model; MVP creates manual origin only.

- [x] **T29.1 RED:** probar `0≤start<end≤duration`, solapamiento rechazado por tipo, edición/eliminación por serie, botón visible solo dentro de rango y seek al final; asegurar que ningún servicio de detección automática se registra en MVP. / Test bounds, overlap, series editing, range-only button, skip target, and absence of auto-detection registration.
- [x] **T29.2 Demostrar RED / Prove RED:** ejecutar `MarkerPolicyTests|ManualMarkerTests|MarkerUiTests`; esperar modelo/política/UI ausentes. / Expect missing model/policy/UI.
- [x] **T29.3 GREEN:** implementar repositorio, editor accesible y proyección de botón según posición; conservar campos de origen/confianza para `STABLE` sin ejecutar detección. / Implement repository, accessible editor, and position projection while retaining stable-ready fields without detection.
- [x] **T29.4 Verificar / Verify:** dos episodios de duraciones diferentes, marcadores inválidos, cambio de serie y navegación teclado/Narrator; botones nunca aparecen fuera de rango. / Test differing episode lengths, invalid markers, series switch, keyboard, and Narrator.
- [x] **T29.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T29-manual-markers.md`; `PLY-012` pasa a `VERIFIED`; `PLY-013` permanece `PLANNED` para STABLE, nunca se marca implementado por compartir modelo. / Evidence verifies manual markers; automatic detection remains planned only.
- [x] **T29.6 Commit:** `feat: edit and use manual intro and credits markers`.

**Puerta I4 / I4 gate:** 20/20 cierres reanudan dentro de ±5 s, todas las transiciones/overrides pasan, un cambio de versión fallido no corrompe progreso, la retirada del siguiente episodio vuelve a ficha y no existe detección automática habilitada en el MVP. / All 20 forced closes resume within ±5 s, state/override tests pass, failed version switch preserves progress, missing next episode returns to details, and automatic detection is not enabled in MVP.

---

## Incremento I5 — Experiencia personal, accesible y Windows-first / Increment I5 — Personal, Accessible, Windows-First Experience

**Demo utilizable / Usable demo:** Inicio prioriza reanudar y deja la biblioteca visible; favoritos/ver más tarde/valoración y recomendaciones locales funcionan; toda la ruta onboarding→reproducción→ajustes se completa con teclado/Narrator; bandeja, inicio y “Abrir con…” son opt-in y seguros. / Home prioritizes resume while keeping the library visible; favorites/watch later/rating and local recommendations work; onboarding→playback→settings completes with keyboard/Narrator; tray, startup, and “Open with…” are safe opt-ins.

### Tarea 30 — Inicio híbrido y fichas completas / Task 30 — Hybrid Home and Complete Details

**IDs:** `UX-001`, `UX-002`, `UX-003`, `UX-004`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Home/GetHome.cs`, `src/ApSolutions.LocalMedia.Presentation/Home/HomeView.axaml`, `HomeViewModel.cs`, `ResumeHeroView.axaml`, `InProgressRailView.axaml`, `LibraryEntryView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Movie/MovieDetailsViewModel.cs`, `src/ApSolutions.LocalMedia.Presentation/Show/ShowDetailsViewModel.cs`, `SeasonViewModel.cs`, `EpisodeRowView.axaml`, `tests/ApSolutions.LocalMedia.Application.Tests/Home/GetHomeTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Home/HomeLayoutTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Details/DetailsNavigationTests.cs`.

**Interfaces / Interfaces:** `GetHomeQuery` devuelve `ResumeItem?`, `InProgress`, `RecentlyAdded`, `LibrarySummary`; Continue es primer foco solo si hay progreso válido y Biblioteca permanece en primer viewport a 1366×768/100 %. / Defines Home data and exact primary-action/viewport behavior.

- [x] **T30.1 RED:** probar Home vacío, con progreso, elemento no disponible, película/serie, primer foco, Biblioteca en viewport, restauración de navegación, temas/tamaños y cero texto fuera de recursos. / Test empty/progress/unavailable/movie/show states, focus, viewport, navigation, themes/scaling, and localization.
- [x] **T30.2 Demostrar RED / Prove RED:** ejecutar `GetHomeTests|HomeLayoutTests|DetailsNavigationTests`; esperar consulta/vistas ausentes o incompletas. / Expect missing/incomplete query/views.
- [x] **T30.3 GREEN:** implementar proyección Home, hero condicional, rail virtualizado, acceso Biblioteca y fichas con versiones/estado/episodios/acciones; usar tokens, no tamaños/colores ad hoc. / Implement Home projection, conditional hero, virtualized rail, library entry, and full details using tokens only.
- [x] **T30.4 Verificar / Verify:** regresión visual 1366×768 y 4K a 100/150/200 %, claro/oscuro/alto contraste, ES/EN; aprobar diferencias intencionales con baseline versionada. / Run visual regression across resolutions, scaling, themes, contrast, and languages with reviewed baselines.
- [x] **T30.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T30-home-details.md`; `UX-001`, `UX-002`, `UX-003`, `UX-004` pasan a `VERIFIED` si no quedan textos incrustados ni fallos visuales críticos. / Evidence verifies the four UX IDs only with clean localization/design review.
- [x] **T30.6 Commit:** `feat: complete the hybrid home and title details`.

### Tarea 31 — Favoritos, ver más tarde y valoración / Task 31 — Favorites, Watch Later, and Rating

**IDs:** `UX-005`, `DAT-002`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Personalization/PersonalState.cs`, `src/ApSolutions.LocalMedia.Domain/Personalization/IPersonalStateRepository.cs`, `src/ApSolutions.LocalMedia.Application/Personalization/SetPersonalState.cs`, `src/ApSolutions.LocalMedia.Application/Personalization/GetPersonalFilters.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/PersonalStateRepository.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0011_personal_state.sql`, `src/ApSolutions.LocalMedia.Presentation/Catalog/PersonalActionsView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Catalog/PersonalActionsViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Personalization/PersonalStateTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Personalization/PersonalStateWorkflowTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Catalog/PersonalActionsTests.cs`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`.

**Interfaces / Interfaces:** `PersonalState(ContentId, IsFavorite, IsWatchLater, Rating?)`; rating entero `1..10` o `null`; filtros se integran en `CatalogQuery`. / Defines exact personal state and catalog filters.

- [x] **T31.1 RED:** probar toggles idempotentes, rating fronteras/nulo, persistencia/reinicio, filtros combinados, unidad ausente y controles accesibles con estado anunciado; verificar que no existe lista personalizada. / Test idempotence, rating boundaries, persistence, filters, unavailable storage, accessibility, and absence of custom lists.
- [x] **T31.2 Demostrar RED / Prove RED:** ejecutar `PersonalStateTests|PersonalStateWorkflowTests|PersonalActionsTests`; esperar modelo/repositorio/UI ausentes. / Expect missing components.
- [x] **T31.3 GREEN:** implementar UPSERT local, acciones en fichas/tarjetas y filtros; no crear concepto de perfil ni colección/lista arbitraria. / Implement local upsert, actions, and filters without profiles or custom collections.
- [x] **T31.4 Verificar / Verify:** 1.000 cambios aleatorios con modelo de referencia, reinicio y búsqueda concurrente; estado final coincide y no hay tráfico. / Run 1,000 randomized state changes against a reference model, restart, and concurrent search with no traffic.
- [x] **T31.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T31-personal-state.md`; `UX-005` pasa a `IMPLEMENTED` hasta exportación T36. / Evidence recorded; export closes final verification.
- [x] **T31.6 Commit:** `feat: save local favorites watch later and ratings`.

### Tarea 32 — Recomendaciones privadas y explicables / Task 32 — Private, Explainable Recommendations

**IDs:** `UX-006`, `PRI-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Personalization/RecommendationModels.cs`, `src/ApSolutions.LocalMedia.Domain/Personalization/RecommendationPolicy.cs`, `src/ApSolutions.LocalMedia.Application/Personalization/GetRecommendations.cs`, `src/ApSolutions.LocalMedia.Presentation/Home/RecommendationsRailView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Home/RecommendationsViewModel.cs`, `src/ApSolutions.LocalMedia.Presentation/Settings/RecommendationSettingsView.axaml`, `tests/ApSolutions.LocalMedia.Domain.Tests/Personalization/RecommendationPolicyTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Personalization/GetRecommendationsTests.cs`, `tests/ApSolutions.LocalMedia.PerformanceTests/RecommendationBudgetTests.cs`.

**Interfaces / Interfaces:** algoritmo local determinista aplica los pesos Recommendation v1 de la sección global a género/reparto/año/valoración/historial mínimo; devuelve `Recommendation(ContentId, Score, ReasonCodes)`; desactivado devuelve vacío y omite cálculo. / Uses the global Recommendation v1 weights, returns explainable reason codes, and performs no work when disabled.

- [x] **T32.1 RED:** probar catálogo nuevo, señales positivas/negativas, exclusión no disponible configurable, desempate estable, explicación localizable, desactivación persistente, consulta sin `HttpClient`/proveedor y 10.000 elementos dentro de 200 ms tras calentamiento. / Test cold start, signals, availability, stable ties, explanations, persistent disable, no network/provider, and 10k budget.
- [x] **T32.2 Demostrar RED / Prove RED:** ejecutar `RecommendationPolicyTests|GetRecommendationsTests|RecommendationBudgetTests`; esperar política/caso de uso ausentes. / Expect missing policy/use case.
- [x] **T32.3 GREEN:** implementar puntuación versionada, consulta SQLite proyectada y rail con “Por qué”; no serializar ni transmitir biblioteca/historial. / Implement versioned scoring, projected SQLite query, and “Why” rail without serializing/transmitting library or history.
- [x] **T32.4 Verificar / Verify:** semilla fija, reinicio, desactivación y captura de red con servidor señuelo; resultado determinista y cero solicitudes. / Run fixed seed, restart, disable, and network capture against a canary server; deterministic results and zero requests.
- [x] **T32.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T32-recommendations.md` contiene razones, benchmark y PCAP/resumen sanitizado; `UX-006` pasa a `VERIFIED`. / Evidence includes reasons, benchmark, and sanitized network proof; verify recommendations.
- [x] **T32.6 Commit:** `feat: recommend titles locally with explanations`.

### Tarea 33 — Auditoría integral de accesibilidad / Task 33 — End-to-End Accessibility Audit

**IDs:** `A11Y-001`, `A11Y-002`, `PLY-014`, `UX-002`, `UX-003`.

**Archivos / Files:** crear / create `tests/ApSolutions.LocalMedia.AccessibilityTests/EndToEnd/KeyboardJourneyTests.cs`, `NarratorMetadataTests.cs`, `HighContrastTests.cs`, `TextScalingTests.cs`, `ReducedMotionTests.cs`, `SubtitleCustomizationTests.cs`, `eng/run-accessibility.ps1`, `docs/evidence/mvp/accessibility-report.md`; modificar / modify cualquier `*.axaml`/ViewModel que falle, siempre en el mismo módulo y con prueba de regresión. / Modify any failing XAML/ViewModel in its owning module with a regression test.

**Interfaces / Interfaces:** recorrido canónico: primer inicio→añadir raíz→buscar→revisar→abrir ficha→reproducir→controlar→reanudar→favorito→copia→ajustes; severidad `Critical/Major/Minor`, puerta MVP: `0 Critical`, `0 Major` sin excepción aprobada. / Defines the canonical journey, severities, and zero-critical/major gate.

- [x] **T33.1 RED:** automatizar orden de tabulación, foco visible, nombres/roles/estados, activación, anuncios de trabajo largo, contraste, 200 % texto, alto contraste, reducción de movimiento y subtítulos; ejecutar revisión manual Narrator con guion bilingüe. / Automate all stated checks and script a bilingual manual Narrator review.
- [x] **T33.2 Demostrar RED / Prove RED:** `pwsh ./eng/run-accessibility.ps1 -Mode Audit`; registrar cada defecto con vista, control, severidad, reproducción y captura/árbol UIA. / Record each defect with view, control, severity, repro, and UIA evidence.
- [x] **T33.3 GREEN:** corregir un defecto por ciclo RED/GREEN en el archivo propietario; no suprimir chequeos ni cambiar severidad para pasar. / Fix one defect per RED/GREEN cycle in the owning file; never suppress checks or lower severity.
- [x] **T33.4 Verificar / Verify:** repetir automatización y recorrido manual ES/EN con teclado, Narrator, 200 %, alto contraste y reducción de movimiento; dos pasadas consecutivas sin crítico/mayor. / Repeat automation and manual bilingual journey; require two consecutive clean passes.
- [x] **T33.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T33-accessibility.md` enlaza informe firmado, entorno, árbol UIA y defectos cerrados; `A11Y-001` y `A11Y-002` pasan a `VERIFIED`. / Evidence links signed report, environment, UIA tree, and closed defects; verify both A11Y IDs.
- [x] **T33.6 Commit:** `fix: close MVP accessibility audit findings`.

### Tarea 34 — Bandeja e inicio con Windows opt-in / Task 34 — Opt-In Tray and Windows Startup

**IDs:** `SYS-001`, `PRI-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Domain/Lifecycle/AppLifecyclePolicy.cs`, `src/ApSolutions.LocalMedia.Application/Lifecycle/ITrayService.cs`, `src/ApSolutions.LocalMedia.Application/Lifecycle/IStartupService.cs`, `src/ApSolutions.LocalMedia.Windows/Tray/WindowsTrayService.cs`, `src/ApSolutions.LocalMedia.Windows/Startup/WindowsStartupService.cs`, `src/ApSolutions.LocalMedia.Presentation/Settings/LifecycleSettingsView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Settings/LifecycleSettingsViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Lifecycle/AppLifecyclePolicyTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Windows/TrayLifecycleTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Windows/WindowsStartupTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Settings/LifecycleSettingsTests.cs`.

**Interfaces / Interfaces:** preferencias predeterminadas `TrayEnabled=false`, `StartWithWindows=false`, `CloseBehavior=Exit`; autoinicio solo cambia tras consentimiento explícito y es reversible. / Defines disabled-by-default lifecycle settings and explicit reversible consent.

- [x] **T34.1 RED:** probar defaults, cerrar con/sin bandeja, sesión de reproducción activa, enable/disable autoinicio idempotente, entrada inválida reparada, uninstall sin entrada huérfana y bandeja inactiva sin eventos. / Test defaults, close modes, active playback, idempotent startup registration, repair, uninstall cleanup, and idle behavior.
- [x] **T34.2 Demostrar RED / Prove RED:** ejecutar `AppLifecyclePolicyTests|TrayLifecycleTests|WindowsStartupTests|LifecycleSettingsTests`; esperar servicios ausentes. / Expect missing services.
- [x] **T34.3 GREEN:** implementar bandeja y registro de inicio con APIs Windows/MSIX aisladas, consentimiento UI y cierre que primero persiste progreso; no almacenar credenciales. / Implement isolated Windows/MSIX tray/startup APIs, consent UI, and progress-safe close without credentials.
- [x] **T34.4 Verificar / Verify:** reinicios reales de sesión Windows en VM limpia, medir CPU/red 10 min en bandeja inactiva y desinstalar; esperar sin autoinicio predeterminado, CPU media <1 % y cero tráfico. / Run clean-VM session restarts, 10-minute idle CPU/network check, and uninstall; expect no default startup, average CPU <1%, and no traffic.
- [x] **T34.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T34-tray-startup.md`; `SYS-001` pasa a `VERIFIED`. / Evidence recorded; lifecycle ID verifies.
- [x] **T34.6 Commit:** `feat: add opt-in tray and Windows startup`.

### Tarea 35 — “Abrir con…” sin importar / Task 35 — “Open With…” Without Importing

**IDs:** `SYS-002`, `PLY-001`, `PRI-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Playback/OpenLooseFile.cs`, `src/ApSolutions.LocalMedia.Windows/Shell/FileActivationHandler.cs`, `src/ApSolutions.LocalMedia.Presentation/Player/LooseFileBanner.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/LooseFileViewModel.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Playback/OpenLooseFileTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Windows/FileActivationTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Player/LooseFileTests.cs`, `src/ApSolutions.LocalMedia.Windows/Packaging/FileAssociations.xml`; modificar / modify `src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest` cuando se cree en T40. / Modify the final package manifest when T40 creates it.

**Interfaces / Interfaces:** `OpenLooseFileCommand(path)` crea sesión efímera sin `Title`, `MediaFile` ni `WatchState`; acción secundaria explícita `AddContainingFolder` entra en flujo T5. / Defines an ephemeral session with no persistent catalog/progress records unless the user explicitly adds the folder.

- [x] **T35.1 RED:** probar activación `.mp4/.mkv/.avi/.mov/.webm`, ruta inexistente/denegada, segunda activación, cierre y consulta DB antes/después idéntica; botón “Añadir carpeta” requiere confirmación y no importa solo el archivo. / Test associations, errors, second activation, unchanged DB, and explicit folder-add path.
- [x] **T35.2 Demostrar RED / Prove RED:** ejecutar `OpenLooseFileTests|FileActivationTests|LooseFileTests`; esperar handler/caso de uso ausentes. / Expect missing handler/use case.
- [x] **T35.3 GREEN:** implementar parser de argumentos sin shell, validación de extensión/ruta, sesión efímera y banner; reutilizar coordinador T18 y mensajes T19. / Implement shell-free argument parsing, path/extension validation, ephemeral session, and banner using existing coordinator/errors.
- [x] **T35.4 Verificar / Verify:** activar desde Explorer/PowerShell con espacios, Unicode y ruta larga; comparar todas las tablas persistentes antes/después y confirmar cero filas nuevas. / Activate through Explorer/PowerShell with spaces, Unicode, and long paths; assert zero persistent row changes.
- [x] **T35.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T35-open-with.md`; `SYS-002` pasa a `IMPLEMENTED` y a `VERIFIED` tras repetir con MSIX T40. / Evidence recorded; final verification waits for packaged activation.
- [x] **T35.6 Commit:** `feat: open loose media without catalog import`.

**Puerta I5 / I5 gate:** la regresión visual está aprobada, el recorrido accesible tiene cero defectos críticos/mayores, recomendaciones y datos personales generan cero tráfico, bandeja/inicio están desactivados por defecto y una activación suelta deja la DB sin cambios. / Visual regression is approved, the accessibility journey has zero critical/major defects, recommendations and personal data cause zero traffic, tray/startup are off by default, and loose-file activation leaves the database unchanged.

---

## Incremento I6 — Copia, recuperación y privacidad demostradas / Increment I6 — Proven Backup, Recovery, and Privacy

**Demo utilizable / Usable demo:** crear una copia/ZIP sin vídeos, restaurarla en rutas distintas mediante simulación, recuperarse de base/migración dañada sin sustituir datos válidos y generar diagnósticos opt-in que el usuario puede inspeccionar y que nunca contienen datos privados. / Create a backup/video-free ZIP, restore it to different paths through a dry run, recover from database/migration failure without replacing valid data, and generate inspectable opt-in diagnostics that never contain private data.

### Tarea 36 — Copias rotatorias y exportación ZIP / Task 36 — Rotating Backups and ZIP Export

**IDs:** `DAT-002`, `UX-005`, `LIB-011`, `PRD-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Backup/BackupContracts.cs`, `src/ApSolutions.LocalMedia.Application/Backup/CreateBackup.cs`, `ExportLibrary.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Backup/SqliteBackupService.cs`, `RotatingBackupStore.cs`, `ZipExportService.cs`, `src/ApSolutions.LocalMedia.Presentation/Backup/BackupView.axaml`, `BackupViewModel.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Backup/BackupWorkflowTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Backup/RotatingBackupTests.cs`, `ZipExportTests.cs`.

**Interfaces / Interfaces:** `BackupManifest(FormatVersion, AppVersion, CreatedUtc, DatabaseSha256, PreferencesSha256, PersonalArtwork[], Roots[])`; retención predeterminada 5 copias; ZIP incluye snapshot consistente, preferencias, datos personales/bloqueos/arte personal y manifiesto, excluye vídeos, caché remota, tokens y diagnósticos. / Defines versioned manifest, five-backup retention, exact inclusions, and exact exclusions.

- [x] **T36.1 RED:** probar snapshot durante escritura, rotación 6→5 sin borrar última válida, espacio insuficiente, cancelación, hashes, lista blanca ZIP y búsqueda de extensiones/rutas/tokens prohibidos; favoritos/valoración/bloqueos/arte personal presentes. / Test live snapshot, rotation, disk-full, cancellation, hashes, allowlist, prohibited content, and all personal data.
- [x] **T36.2 Demostrar RED / Prove RED:** ejecutar `BackupWorkflowTests|RotatingBackupTests|ZipExportTests`; esperar contratos/servicios ausentes. / Expect missing contracts/services.
- [x] **T36.3 GREEN:** usar API de backup SQLite o snapshot transaccional, escritura temporal+rename, retención que protege última válida y ZIP por lista permitida; mostrar progreso/cancelación y destino elegido. / Implement consistent snapshot, temp+rename, safe retention, allowlisted ZIP, progress, and cancellation.
- [x] **T36.4 Verificar / Verify:** exportar catálogo T10 mientras cambia progreso, abrir ZIP con herramienta independiente, verificar hashes y confirmar cero vídeo/caché/secreto. / Export the T10 catalog during progress writes, inspect independently, verify hashes, and confirm excluded data.
- [x] **T36.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T36-backup-export.md` incluye inventario ZIP y prueba de datos personales; `UX-005` y `LIB-011` pasan a `VERIFIED`; `DAT-002` espera restauración. / Evidence includes ZIP inventory and personal data; restore closes DAT-002.
- [x] **T36.6 Commit:** `feat: create rotating backups and safe exports`.

### Tarea 37 — Importación validada y reasignación de raíces / Task 37 — Validated Import and Root Remapping

**IDs:** `DAT-001`, `DAT-002`, `LIB-009`, `LIB-010`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Backup/PreviewRestore.cs`, `RestoreBackup.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Backup/BackupValidator.cs`, `StagedRestoreService.cs`, `src/ApSolutions.LocalMedia.Presentation/Backup/RestoreWizardView.axaml`, `RestoreWizardViewModel.cs`, `RootRemapRowViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Backup/RootRemapPolicyTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Backup/DisasterRecoveryTests.cs`, `RestoreValidationTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Backup/RestoreWizardTests.cs`.

**Interfaces / Interfaces:** `RestorePreview` enumera compatibilidad, integridad, espacio, raíces existentes/ausentes, `RootRemap(old,new)` y cambios simulados; `RestoreBackup` solo hace swap tras validar staging completo y conservar copia de activa. / Defines dry-run preview and a validated staged swap preserving the active database.

- [x] **T37.1 RED:** probar ZIP válido, hash/manifiesto/versiones inválidos, Zip Slip, espacio insuficiente, base dañada, ruta igual, antigua→nueva, dos antiguas a una conflictivas, cancelación y fallo tras staging; activa no cambia en todos los fallos. / Test valid and every invalid/attack/remap/cancel/failure case with unchanged active data.
- [x] **T37.2 Demostrar RED / Prove RED:** ejecutar `RootRemapPolicyTests|DisasterRecoveryTests|RestoreValidationTests|RestoreWizardTests`; esperar políticas/servicios/wizard ausentes. / Expect missing components.
- [x] **T37.3 GREEN:** validar ZIP por lista/tamaños/rutas, hashes, versión e integridad SQLite; aplicar remap en copia staged, presentar simulación y hacer swap recuperable solo al confirmar. / Implement strict archive validation, hashes/version/integrity, staged remap, dry run, and recoverable confirmed swap.
- [x] **T37.4 Verificar / Verify:** restaurar export T36 en VM con rutas distintas, comprobar títulos/progreso/preferencias/personales/bloqueos, reescanear y obtener cero duplicados; inyectar fallo en cada fase. / Restore T36 in a different-path VM, verify all data and zero duplicates, and inject failure at every phase.
- [x] **T37.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T37-disaster-recovery.md` contiene manifiesto, remap, integridad y tabla de fallos; `DAT-001` y `DAT-002` pasan a `VERIFIED`. / Evidence covers manifest, remap, integrity, and failures; both data IDs verify.
- [x] **T37.6 Commit:** `feat: validate stage and remap library restores`.

### Tarea 38 — Privacidad de red y diagnósticos sanitizados / Task 38 — Network Privacy and Sanitized Diagnostics

**IDs:** `PRI-001`, `PRI-002`, `UX-006`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Privacy/DiagnosticsContracts.cs`, `src/ApSolutions.LocalMedia.Application/Privacy/CreateDiagnostics.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Privacy/AllowlistedDiagnosticsBuilder.cs`, `NetworkPurposeRegistry.cs`, `src/ApSolutions.LocalMedia.Presentation/Settings/PrivacySettingsView.axaml`, `PrivacySettingsViewModel.cs`, `DiagnosticsPreviewView.axaml`, `tests/ApSolutions.LocalMedia.Domain.Tests/Privacy/DiagnosticsAllowlistTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Privacy/DiagnosticsPayloadTests.cs`, `NetworkPrivacyTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Settings/PrivacyConsentTests.cs`, `docs/privacy/PRIVACY.es.md`, `docs/privacy/PRIVACY.en.md`.

**Interfaces / Interfaces:** `IDiagnosticsBuilder.Build(consent, inputs)` permite versión app, Windows/.NET, capacidades agregadas, códigos de error y conteos acotados; prohíbe rutas, filenames, títulos, IDs de contenido/proveedor, biblioteca, historial, token y dumps crudos. Transporte MVP es exportación manual tras preview; no envío automático. / Defines an allowlisted payload, explicit prohibited data, and previewed manual export only in MVP.

- [x] **T38.1 RED:** sembrar canarios en rutas/nombres/títulos/token/IDs/historial y una credencial NAS señuelo; probar consentimiento false→sin payload, true→solo lista, preview idéntica a export, desactivación, logs sanitizados, ausencia de almacén propio de credenciales NAS y tráfico limitado a TMDB solicitado. / Seed canaries in paths/names/titles/token/IDs/history plus a fake NAS credential; test consent, allowlist, preview parity, disable, sanitized logs, no application-owned NAS credential store, and requested-TMDB-only traffic.
- [x] **T38.2 Demostrar RED / Prove RED:** ejecutar `DiagnosticsAllowlistTests|DiagnosticsPayloadTests|NetworkPrivacyTests|PrivacyConsentTests`; esperar builder/registro/vista ausentes. / Expect missing builder/registry/view.
- [x] **T38.3 GREEN:** implementar DTO cerrado sin serialización reflexiva de entidades, sanitizador de excepciones, preview/export manual, preferencias predeterminadas off y registro de propósito para cada `HttpClient`. / Implement closed DTOs, exception sanitizer, preview/manual export, off-by-default settings, and purpose registry per HTTP client.
- [x] **T38.4 Verificar / Verify:** recorrido 30 min con proxy de captura: offline, TMDB consentido, recomendaciones, reproducción, bandeja y diagnóstico; escanear payload/logs/PCAP por todos los canarios, esperar cero. / Run a 30-minute proxy-captured journey and scan payload/logs/PCAP for every canary; expect zero.
- [x] **T38.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T38-privacy.md` contiene tabla de conexiones, esquema de payload y resultados de canarios sin exponerlos completos; `PRI-001` y `PRI-002` pasan a `VERIFIED`. / Evidence contains connection table, payload schema, and redacted canary results; verify both privacy IDs.
- [x] **T38.6 Commit:** `feat: enforce offline privacy and inspectable diagnostics`.

### Tarea 39 — Recuperación y carga simultánea / Task 39 — Recovery and Concurrent Load

**IDs:** `LIB-002`, `LIB-010`, `PLY-001`, `PLY-008`, `DAT-001`, `SYS-001`.

**Archivos / Files:** crear / create `tests/ApSolutions.LocalMedia.IntegrationTests/Recovery/ForcedShutdownTests.cs`, `RemovedDriveTests.cs`, `DamagedDatabaseTests.cs`, `FailedMigrationTests.cs`, `MediaEngineFailureTests.cs`, `tests/ApSolutions.LocalMedia.PerformanceTests/ConcurrentPlaybackScanTests.cs`, `SlowNasTests.cs`, `TrayIdleTests.cs`, `eng/run-recovery.ps1`, `docs/evidence/mvp/recovery-matrix.md`; modificar / modify componentes propietarios solo mediante una prueba de regresión específica. / Modify owning components only with a specific regression test.

**Interfaces / Interfaces:** matriz inyecta nueve fallos de la especificación; cada resultado declara `Continued`, `Degraded`, `Recoverable`, o `AbortedSafely`, nunca éxito falso. / The matrix injects all nine specified failure classes and reports explicit recovery outcomes.

- [x] **T39.1 RED:** automatizar cierre forzado, USB/NAS retirado, acceso denegado, TMDB 429/caído, archivo corrupto, motor fallido, base dañada, migración fallida y conflicto de rename; añadir reproducción mientras escanea NAS lento y bandeja inactiva. / Automate all specified failures plus concurrent playback/slow NAS and idle tray.
- [x] **T39.2 Demostrar RED / Prove RED:** `pwsh ./eng/run-recovery.ps1 -Mode Audit`; guardar fallos reales por caso y comprobar que el arnés sí detecta una corrupción sembrada. / Save real failures and prove the harness detects seeded corruption.
- [x] **T39.3 GREEN:** corregir un escenario por ciclo; trabajos largos publican progreso/cancelación/resultado por elemento, motor libera sesión guardando posición y DB nunca reemplaza copia válida. / Fix one scenario per cycle while enforcing long-job, engine, and database recovery rules.
- [x] **T39.4 Verificar / Verify:** dos pasadas completas, segunda sin flakiness; durante reproducción+escaneo no hay dropout >250 ms atribuible al escaneo, bloqueo UI <50 ms y NAS respeta concurrencia por raíz. / Require two non-flaky full passes; enforce playback/UI/NAS concurrency thresholds.
- [x] **T39.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T39-recovery-load.md` contiene una fila por fallo, resultado y enlace; cualquier `AbortedSafely` no esperado bloquea el MVP. / Evidence contains one row per failure; any unexpected safe abort still blocks MVP.
- [x] **T39.6 Commit:** `fix: pass recovery and concurrent workload matrix`.

**Puerta I6 / I6 gate:** export/import recupera íntegramente datos personales en rutas nuevas, toda corrupción deja intacta la última base válida, la captura de privacidad contiene solo llamadas TMDB solicitadas y la matriz de recuperación/concurrencia pasa dos veces. / Export/import fully restores personal data to new paths, every corruption preserves the last valid database, privacy capture contains only requested TMDB calls, and recovery/concurrency passes twice.

---

## Incremento I7 — Artefacto MVP x64 instalable / Increment I7 — Installable x64 MVP Artifact

**Demo utilizable / Usable demo:** en una VM Windows 11 x64 limpia se instala AP Reelume, cataloga y reproduce, actualiza conservando datos, rechaza downgrade, repara y desinstala; el ZIP GitHub independiente lleva hash, SBOM, licencia y explicación SmartScreen. / On a clean Windows 11 x64 VM, AP Reelume installs, catalogs and plays, updates while preserving data, rejects downgrade, repairs, and uninstalls; the independent GitHub ZIP carries hash, SBOM, license, and SmartScreen explanation.

### Tarea 39B — Ensamblar la aplicación / Task 39B — Assemble the Application

**Decidida en / Decided in:** [ADR-0003](../../adr/0003-assemble-the-application-before-packaging-it.md). Se inserta antes de T40 y **no renumera ninguna tarea existente**. / Inserted before T40 and renumbering nothing.

**IDs:** `PLY-001`, `LIB-001`, `LIB-006`, `LIB-007`, `LIB-011`, `UX-001`, `PRD-005`.

**Archivos / Files:** modificar / modify `src/ApSolutions.LocalMedia.Presentation/Shell/ShellView.axaml`, `ShellViewModel.cs`, `src/ApSolutions.LocalMedia.Presentation/Navigation/NavigationService.cs`, `src/ApSolutions.LocalMedia.Windows/CompositionRoot.cs`; crear / create `tests/ApSolutions.LocalMedia.UiTests/Shell/SurfaceReachabilityTests.cs`, `tests/ApSolutions.LocalMedia.AccessibilityTests/EndToEnd/AssembledJourneyTests.cs`, `docs/evidence/mvp/T39B-assembly.md`.

**Interfaces / Interfaces:** toda superficie que el producto declara es alcanzable desde `ShellView` por navegación o desde otra superficie alcanzable; el reproductor se abre desde una ficha; la identificación se cablea con `IMetadataProvider` y `ArtworkCache` y respeta el consentimiento de red de T38. / Every declared surface is reachable, playback starts from a title card, and identification is wired behind T38's network consent.

- [x] **T39B.1 RED:** una prueba enumera las superficies declaradas y exige que cada una sea alcanzable; catorce fallan hoy —onboarding de raíz, bandeja de revisión, duplicados, editor de metadatos, renombrado, reproductor, pistas, subtítulos, salida de audio, marcadores, reanudar, atajos, ajustes de escaneo y créditos—. / One test enumerates declared surfaces and demands reachability; fourteen fail today.
- [x] **T39B.2 Demostrar RED / Prove RED:** ejecutar `dotnet test tests/ApSolutions.LocalMedia.UiTests --filter SurfaceReachability`; esperar catorce huérfanas nombradas una a una. / Expect fourteen named orphans.
- [x] **T39B.3 GREEN:** cablear rutas y ventanas, abrir el reproductor desde una ficha, registrar el proveedor de metadatos y la caché de arte, y exponer los créditos de TMDB; ninguna superficie nueva, sólo las ya construidas. / Wire routes and windows, start playback from a card, register the metadata provider and artwork cache, and expose the TMDB credits.
- [x] **T39B.4 Verificar / Verify:** recorrido real con automatización UIA: añadir una carpeta, escanear, identificar con consentimiento, revisar, abrir la ficha, reproducir, cambiar pista, marcar favorito, cerrar; comprobar que el catálogo y el progreso quedan escritos. / Walk the real application end to end and check what reached storage.
- [x] **T39B.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T39B-assembly.md` con el mapa de alcanzabilidad antes y después; `PLY-001` pasa a `VERIFIED` si el recorrido reproduce de verdad. / Evidence carries the before/after reachability map.
- [x] **T39B.6 Commit:** `feat: assemble every built surface into the application`.

### Tarea 40 — MSIX x64 y artefacto GitHub reproducible / Task 40 — x64 MSIX and Reproducible GitHub Artifact

**IDs:** `PRD-002`, `PRD-005`, `SYS-002`, `REL-002`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Windows.Package/ApSolutions.LocalMedia.Windows.Package.wapproj`, `src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/Square44x44Logo.png`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/Square150x150Logo.png`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/Wide310x150Logo.png`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/StoreLogo.png`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/SplashScreen.png`, `eng/package-x64.ps1`, `eng/verify-package.ps1`, `eng/generate-sbom.ps1`, `.github/workflows/release.yml`, `tests/ApSolutions.LocalMedia.PackagingTests/MsixLifecycleTests.cs`, `tests/ApSolutions.LocalMedia.PackagingTests/FileAssociationPackageTests.cs`, `tests/ApSolutions.LocalMedia.PackagingTests/ArtifactContentsTests.cs`, `tests/ApSolutions.LocalMedia.PackagingTests/ReproducibleBuildTests.cs`, `docs/release/RELEASING.es.md`, `docs/release/RELEASING.en.md`, `docs/release/SMARTSCREEN.es.md`, `docs/release/SMARTSCREEN.en.md`; modificar / modify `src/ApSolutions.LocalMedia.Windows/Packaging/FileAssociations.xml`, `Directory.Build.props`.

**Interfaces / Interfaces:** identidad persistente `APSolutions.LocalMedia`, publisher de prueba documentado, arquitecturas manifiesto `x64` en MVP; ZIP autocontenido `win-x64`; versión SemVer mapeada de forma determinista a MSIX. / Defines stable package identity, documented test publisher, x64 MVP manifest, self-contained ZIP, and deterministic version mapping.

- [x] **T40.1 RED:** pruebas PowerShell/VM exigen contenido/arquitectura/nombre/firma correctos, instalación limpia, asociación “Abrir con…”, actualización conservando DB/preferencias, downgrade rechazado, reparación, desinstalación sin borrar datos personales salvo elección explícita, ZIP allowlist, SHA-256, SBOM y dos builds con contenido binario normalizado idéntico. / Tests enforce package metadata/lifecycle, associations, data preservation, artifact contents, hashes, SBOM, and normalized reproducibility.
- [x] **T40.2 Demostrar RED / Prove RED:** ejecutar `pwsh ./eng/package-x64.ps1` y `dotnet test tests/ApSolutions.LocalMedia.PackagingTests`; esperar proyecto/manifiesto/scripts ausentes. / Expect missing package project/manifest/scripts.
- [x] **T40.3 GREEN:** crear MSIX de prueba y publish autocontenido, asociaciones permitidas, generación CycloneDX/SPDX revisada, inventario/avisos/hashes y workflow sin secretos en artefacto; documentar SmartScreen sin afirmar firma inexistente. / Create test MSIX and self-contained publish, associations, reviewed SBOM, inventory/notices/hashes, secret-safe workflow, and accurate SmartScreen docs.
- [x] **T40.4 Verificar / Verify:** VM snapshot limpia ejecuta install→launch→catalog/play→upgrade→repair→uninstall y segunda ruta ZIP; repetir “Abrir con…” y comparar DB; ejecutar dos builds desde checkouts limpios. / Run full clean-VM lifecycle plus ZIP path, packaged activation, DB comparison, and two clean-checkout builds.
- [x] **T40.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/mvp/T40-x64-packaging.md` enlaza vídeos/capturas de ciclo, manifiesto, hashes, SBOM y diff reproducible; `PRD-002`, `SYS-002`, `REL-002` pasan a `VERIFIED`. / Evidence links lifecycle, manifest, hashes, SBOM, and reproducibility diff; verify the three IDs.
- [x] **T40.6 Commit:** `build: package and verify the Windows x64 MVP`.

### Tarea 41 — Cierre documental y puerta MVP / Task 41 — Documentation Closure and MVP Gate

**IDs:** todos los `MVP`; cierre especial / special closure `PRD-001`, `PRD-005`, `UX-008`, `DOC-001`.

**Archivos / Files:** crear / create `docs/roadmap/README.es.md`, `docs/roadmap/README.en.md`, `docs/user-guide/README.es.md`, `docs/user-guide/README.en.md`, `docs/troubleshooting/README.es.md`, `docs/troubleshooting/README.en.md`, `docs/CHANGELOG.es.md`, `docs/CHANGELOG.en.md`, `docs/evidence/mvp/verification-manifest.json`, `docs/evidence/mvp/release-readiness.md`, `tests/ApSolutions.LocalMedia.DocumentationTests/FeatureCoverageTests.cs`, `EvidenceLinkTests.cs`, `BilingualHeadingTests.cs`, `ScopeBoundaryTests.cs`; modificar / modify `README.es.md`, `README.en.md`, `docs/FEATURES.md`, `NOTICE`, ambos avisos de terceros y toda documentación afectada. / Modify all stated canonical/public documents and notices.

**Interfaces / Interfaces:** manifest de evidencia mapea cada ID MVP a estado, tareas, pruebas, artefactos, commit y enlaces; linter falla por ID ausente, enlace roto, pareja lingüística ausente o `VERIFIED` sin evidencia. / Evidence manifest maps every MVP ID to status, tasks, tests, artifacts, commit, and links; lint fails on any omission or invalid verification.

- [x] **T41.1 RED:** enumerar automáticamente IDs target `MVP`, exigir cobertura exacta, pares ES/EN, secciones roadmap/manual/changelog/desarrollo/contribución/release/privacidad/licencias/SBOM y revisión negativa de cuentas, sync, múltiples vídeos, cursos, gestión de vídeos, notas, listas, Dolby/passthrough y macOS/Linux. / Enumerate every MVP ID and require exact coverage, bilingual public docs, and negative scope review.
- [x] **T41.2 Demostrar RED / Prove RED:** ejecutar `dotnet test tests/ApSolutions.LocalMedia.DocumentationTests` y `pwsh ./eng/verify-docs.ps1`; esperar fallos por documentos/evidencias finales aún ausentes. / Expect missing final documents/evidence.
- [x] **T41.3 GREEN:** redactar/actualizar ambos idiomas, enlazar una evidencia por criterio, registrar limitaciones x64/SmartScreen/external progress, instrucciones de backup/privacidad/solución de problemas y estados exactos de matriz; `UX-008` permanece `OUT_OF_SCOPE` con evidencia de ausencia, no se reetiqueta como función. / Write/update both languages, link every criterion, document limitations and operations, and preserve negative-scope semantics.
- [x] **T41.4 Verificar / Verify:** ejecutar `pwsh ./eng/verify.ps1 -Configuration Release -Runtime win-x64`, suite completa dos veces, auditorías docs/licencias/vulnerabilidades/secretos y smoke desde artefactos T40; comparar lista de 46 IDs MVP con manifest sin faltantes/extra. / Run full release verification twice, audits, artifact smoke, and exact 46-ID MVP manifest comparison.
- [x] **T41.5 Aceptación/evidencia / Acceptance/evidence:** Product Owner revisa `docs/evidence/mvp/release-readiness.md`; MVP solo se aprueba con 46/46 IDs correctamente resueltos (`VERIFIED` para compromisos, `OUT_OF_SCOPE` para `UX-008`), cero críticos/mayores, presupuestos cumplidos y documentos bilingües. / Product Owner approves only with all 46 MVP IDs correctly resolved, no critical/major defects, met budgets, and bilingual docs.
- [x] **T41.6 Commit:** `docs: close the bilingual x64 MVP release gate`.

**Puerta final MVP / Final MVP gate:** no iniciar `STABLE` si un compromiso MVP no está `VERIFIED`, si `docs/FEATURES.md` no enlaza su evidencia, si el artefacto probado no coincide con el publicado o si alguna exclusión aparece accidentalmente en UI/API/esquema. / Do not begin `STABLE` if any MVP commitment is not verified, the matrix lacks evidence, tested and published artifacts differ, or any excluded capability leaks into UI/API/schema.

---

# Parte B — Primera versión estable / Part B — First Stable Release

Estas tareas solo se ejecutan después de la puerta MVP. Los cinco IDs `STABLE` son bloqueantes; ninguno puede quedar informalmente pendiente. / These tasks run only after the MVP gate. All five `STABLE` IDs are blocking; none may remain informally pending.

## Incremento S1 — Paridad ARM64 / Increment S1 — ARM64 Parity

### Tarea 42 — Compilación, paquete y reproducción ARM64 / Task 42 — ARM64 Build, Package, and Playback

**IDs:** `PRD-003`, `PRD-004`, `PLY-002`, `PLY-003`, `PLY-004`.

**Archivos / Files:** crear / create `eng/package-arm64.ps1`, `tests/ApSolutions.LocalMedia.PackagingTests/Arm64PackageTests.cs`, `tests/ApSolutions.LocalMedia.MediaTests/Playback/Arm64PlaybackTests.cs`, `docs/evidence/stable/T42-arm64.md`; modificar / modify `src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest`, `Directory.Packages.props`, `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `eng/verify.ps1`.

**Interfaces / Interfaces:** mismo contrato/DB/manifiesto funcional que x64; RuntimeIdentifier `win-arm64`; cualquier sustitución nativa queda detrás del adaptador existente. / Same functional contract/database/manifest as x64 with `win-arm64`; native substitutions stay behind existing adapters.

- [x] **T42.1 RED:** exigir restore/build/package ARM64, arquitectura PE/nativos correctos, instalación/upgrade desde versión ARM64 previa y matriz de reproducción/HDR/audio en hardware Windows 11 ARM64. / Require ARM64 restore/build/package, correct PE/native architecture, lifecycle, and physical playback matrix.
- [x] **T42.2 Demostrar RED / Prove RED:** ejecutar `pwsh ./eng/package-arm64.ps1` y filtros ARM64; esperar scripts/manifiesto o dependencia nativa aún incompletos, no sustituir con emulación x64. / Expect genuine missing ARM64 support; never substitute x64 emulation.
- [x] **T42.3 GREEN:** fijar runtimes nativos compatibles, extender MSIX/CI y corregir solo adaptadores específicos; conservar datos al mover un export entre x64 y ARM64. / Pin compatible native runtimes, extend MSIX/CI, adjust only platform adapters, and preserve cross-architecture exported data.
- [ ] **T42.4 Verificar / Verify:** VM/hardware ARM64 ejecuta ciclo T40, matriz T19 y pruebas HDR/audio aplicables; comparar resultados con x64 y documentar diferencias aceptadas. / Run full lifecycle/media/hardware checks on ARM64 and compare with x64.
- [ ] **T42.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/stable/T42-arm64.md` firmado con modelo de hardware; `PRD-003` pasa a `VERIFIED`. / Signed physical evidence verifies ARM64.
- [x] **T42.6 Commit:** `build: add verified Windows ARM64 artifacts`.

**T42.4 y T42.5 siguen sin marcar a propósito.** El paquete ARM64 existe, es nativo y se verifica en
todo lo que no exige la máquina; certificar la reproducción sí la exige y no hay ninguna, así que
`PRD-003` queda `BLOCKED` con su condición de desbloqueo en lugar de `VERIFIED`. Las seis fases
físicas están declaradas en `arm64-matrix.json` y una prueba impide que el bloqueo se cierre editando
la matriz. Detalle en [T42-arm64.md](../../evidence/stable/T42-arm64.md). /
**T42.4 and T42.5 are deliberately unticked:** the ARM64 package is built and verified in everything
that does not need the hardware, and `PRD-003` stays `BLOCKED` rather than `VERIFIED` until a machine
exists.

## Incremento S2 — Segmentos detectados con corrección humana / Increment S2 — Detected Segments with Human Correction

### Tarea 43 — Detección automática de intro/resumen/créditos / Task 43 — Automatic Intro, Recap, and Credits Detection

**IDs:** `PLY-013`, `PLY-012`, `PRI-001`.

**Archivos / Files:** crear / create `docs/superpowers/specs/automatic-segment-detection.es.md`, `docs/superpowers/specs/automatic-segment-detection.en.md`, `src/ApSolutions.LocalMedia.Domain/Continuity/SegmentDetectionPolicy.cs`, `src/ApSolutions.LocalMedia.Domain/Continuity/IAutomaticSegmentDetector.cs`, `src/ApSolutions.LocalMedia.Application/Continuity/DetectSeriesSegments.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Media/LocalSegmentFeatureExtractor.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Media/AutomaticSegmentDetector.cs`, `src/ApSolutions.LocalMedia.Presentation/Settings/SegmentDetectionSettingsView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Player/DetectedMarkerReviewView.axaml`, `tests/ApSolutions.LocalMedia.Domain.Tests/Continuity/SegmentDetectionPolicyTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Continuity/AutomaticSegmentDetectionTests.cs`, `tests/ApSolutions.LocalMedia.PerformanceTests/SegmentDetectionBenchmarks.cs`, `tests/ApSolutions.LocalMedia.MediaTests/Fixtures/segment-corpus-manifest.json`.

**Interfaces / Interfaces:** antes de código se congela corpus multi-serie redistribuible/generado y umbrales de precisión, recall y falsos positivos por tipo aprobados por Product Owner; detector local cancelable produce `Origin=Detected`, confianza y versión, nunca reemplaza corrección/manual. / Before code, freeze an approved multi-show corpus and publication metrics; the local cancelable detector produces detected markers and never overrides manual/user-corrected data.

- [x] **T43.1 RED:** crear primero la subespecificación bilingüe con corpus/verdad terreno/umbrales; pruebas cubren coincidencia entre episodios, cold open variable, recap opcional, créditos, episodios sin segmento, corrección manual, cancelación y cero red. / First freeze the bilingual subspec and tests for all content patterns, correction, cancellation, and no network.
- [x] **T43.2 Demostrar RED / Prove RED:** ejecutar corpus contra detector nulo/baseline y guardar métricas; debe incumplir el umbral aprobado, demostrando sensibilidad del benchmark. / Run null/baseline detector and prove the benchmark fails approved thresholds.
- [x] **T43.3 GREEN:** implementar extracción local acotada y comparación intra-serie con caché; programar en baja prioridad, pausable durante reproducción, y exponer revisión/corrección. / Implement bounded local feature extraction and within-series comparison with low-priority scheduling and human review.
- [x] **T43.4 Verificar / Verify:** benchmark en corpus retenido no usado para ajuste, rendimiento mientras reproduce y auditoría privacidad; no aceptar promedio que esconda falsos positivos de una serie. / Evaluate a held-out corpus, concurrent playback performance, and privacy; do not hide per-show false positives in averages.
- [x] **T43.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/stable/T43-segment-detection.md` publica métricas agregadas y por serie, correcciones y recursos; `PLY-013` pasa a `VERIFIED` solo al cumplir cada umbral aprobado. / Evidence publishes aggregate/per-show metrics and resource use; verify only when every approved threshold passes.
- [x] **T43.6 Commit:** `feat: detect and review recurring playback segments locally`.

## Incremento S3 — Actualización confirmada / Increment S3 — Confirmed Updating

### Tarea 44 — Actualizador independiente seguro / Task 44 — Safe Independent Updater

**IDs:** `REL-003`, `DAT-001`, `PRI-001`, `DOC-001`.

**Archivos / Files:** crear / create `src/ApSolutions.LocalMedia.Application/Updates/UpdateContracts.cs`, `CheckForUpdates.cs`, `ConfirmUpdate.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Updates/GitHubReleaseUpdateProvider.cs`, `VerifiedUpdateDownloader.cs`, `src/ApSolutions.LocalMedia.Windows/Updates/WindowsUpdateLauncher.cs`, `src/ApSolutions.LocalMedia.Presentation/Updates/UpdateView.axaml`, `UpdateViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Updates/UpdateManifestTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Updates/UpdateWorkflowTests.cs`, `tests/ApSolutions.LocalMedia.PackagingTests/InterruptedUpdateTests.cs`.

**Interfaces / Interfaces:** manifiesto firmado por hash contiene versión, RID, URL HTTPS, SHA-256, tamaño y resumen ES/EN; check es solicitado/configurable, descarga puede ser background, instalación exige confirmación y copia/migración segura. / Defines a bilingual hash-verified update manifest, optional checks, background download, explicit install confirmation, and safe backup/migration.

- [ ] **T44.1 RED:** probar no update/downgrade/RID incorrecto, hash/tamaño/HTTPS inválido, resumen ES/EN ausente, cancelación, red caída, descarga interrumpida/reanudada, confirmación negativa, migración fallida y cero instalación silenciosa. / Test every manifest, network, interruption, consent, and migration failure path.
- [ ] **T44.2 Demostrar RED / Prove RED:** ejecutar `UpdateManifestTests|UpdateWorkflowTests|InterruptedUpdateTests`; esperar contratos/proveedor ausentes. / Expect missing contracts/provider.
- [ ] **T44.3 GREEN:** implementar proveedor GitHub detrás de contrato, descarga temporal verificada, UI resumen/confirmación y launcher Windows recuperable; Store usa su canal y no duplica actualizador independiente. / Implement GitHub provider, verified staged download, summary/confirmation, and recoverable launcher while leaving Store updates to Store.
- [ ] **T44.4 Verificar / Verify:** servidor falso y VM ejecutan update correcto, cancelado, manipulado e interrumpido; DB y binario activo quedan válidos en todos. / Run valid, canceled, tampered, and interrupted updates in fake server/VM; active DB/binary remain valid.
- [ ] **T44.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/stable/T44-updater.md`; `REL-003` pasa a `VERIFIED`. / Evidence recorded; updater verifies.
- [ ] **T44.6 Commit:** `feat: verify summarize and confirm independent updates`.

## Incremento S4 — Autorización de marca y Store / Increment S4 — Brand Clearance and Store

### Tarea 45 — Comprobación formal de AP Reelume / Task 45 — Formal AP Reelume Clearance

**IDs:** `REL-004`.

**Archivos / Files:** crear / create `docs/release/brand-clearance/AP-Reelume-current.es.md`, `docs/release/brand-clearance/AP-Reelume-current.en.md`, `docs/evidence/stable/T45-brand-clearance.md`; modificar / modify `docs/adr/0001-public-product-name.md`, `docs/FEATURES.md` solo con el resultado aprobado. El propio informe contiene la fecha efectiva y el commit lo conserva históricamente. / Create bilingual current clearance reports whose content records the effective date and whose history is preserved by Git; update ADR/matrix only with the approved outcome.

**Interfaces / Interfaces:** informe identifica responsable, fecha, territorios, clases/ámbito, fuentes oficiales de marcas/nombres/dominios/Store, consultas exactas, capturas/enlaces y decisión `Cleared`, `ClearedWithConditions` o `NotCleared`; IDs internos no cambian. / Defines auditable clearance inputs/outcomes while preserving internal IDs.

- [ ] **T45.1 RED:** el test documental exige informe vigente, pareja lingüística, responsable y cuatro ámbitos; falla con la comprobación preliminar del ADR porque no es autorización formal. / Documentation test requires a current, owned, four-scope report and rejects the preliminary ADR check.
- [ ] **T45.2 Demostrar RED / Prove RED:** ejecutar `FeatureCoverageTests --filter REL-004`; esperar fallo por informe formal ausente. / Expect missing formal report.
- [ ] **T45.3 GREEN:** el responsable jurídico/producto realiza y firma la búsqueda formal inmediatamente antes de reservar/publicar; registrar hechos sin convertir el plan en asesoramiento jurídico. / Legal/product owner performs and signs the formal search immediately before reservation/publication; record facts without treating this plan as legal advice.
- [ ] **T45.4 Verificar / Verify:** revisión por Product Owner de fuentes, fecha y condiciones; si no se autoriza, bloquear publicación y abrir ADR de cambio de nombre conservando `ApSolutions.LocalMedia`/DB/package identity. / Product Owner reviews sources/date/conditions; on failure, block release and open a naming ADR while preserving internal IDs.
- [ ] **T45.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/stable/T45-brand-clearance.md` enlaza informe aprobado y `REL-004` pasa a `VERIFIED`; `NotCleared` mantiene `BLOCKED`. / Approved evidence verifies; a negative outcome remains blocked.
- [ ] **T45.6 Commit:** `docs: record formal AP Reelume release clearance`.

### Tarea 46 — Certificación Microsoft Store / Task 46 — Microsoft Store Certification

**IDs:** `REL-001`, `PRD-002`, `PRD-003`, `REL-004`.

**Archivos / Files:** crear / create `store/listing.es-ES.md`, `store/listing.en-US.md`, `store/privacy-url.es.md`, `store/privacy-url.en.md`, `store/submission-checklist.es.md`, `store/submission-checklist.en.md`, `tests/ApSolutions.LocalMedia.PackagingTests/StoreComplianceTests.cs`, `docs/evidence/stable/T46-store-submission.md`; modificar / modify `src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/Square44x44Logo.png`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/Square150x150Logo.png`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/Wide310x150Logo.png`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/StoreLogo.png`, `src/ApSolutions.LocalMedia.Windows.Package/Assets/SplashScreen.png`, `.github/workflows/release.yml` con identidad/publisher asignados por Store sin cambiar schema/namespace. / Update the exact manifest/assets with Store-assigned identity/publisher without changing schema/namespace.

**Interfaces / Interfaces:** listing usa nombre/firma exactos y URLs públicas bilingües; paquete Store x64+ARM64; actualización gestionada por Store probada desde vuelo privado. / Defines exact bilingual listing, dual-architecture package, and private-flight update path.

- [ ] **T46.1 RED:** compliance tests validan WACK, capacidades mínimas, privacidad, contenido/listing ES/EN, iconos, asociaciones, x64/ARM64 y migración de identidad desde paquete de prueba documentada. / Validate WACK, minimal capabilities, privacy/listing/assets, architectures, associations, and documented identity migration.
- [ ] **T46.2 Demostrar RED / Prove RED:** ejecutar WACK/compliance sobre paquete pre-Store; registrar fallos y no marcar certificación por validación local únicamente. / Run local WACK/compliance and do not equate it with certification.
- [ ] **T46.3 GREEN:** aplicar identidad reservada tras T45, corregir capacidades/listing, subir a vuelo privado de coste cero y probar instalación/actualización en x64 y ARM64. / Apply reserved identity after clearance, fix listing/capabilities, upload to private flight, and test both architectures.
- [ ] **T46.4 Verificar / Verify:** obtener certificación real, instalar desde Store, actualizar a segundo vuelo, comprobar datos y desinstalar; archivar reporte sin secretos. / Obtain real certification and run Store install/update/data/uninstall lifecycle.
- [ ] **T46.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/stable/T46-store-submission.md` enlaza certificación y vuelo; `REL-001` pasa a `VERIFIED`. / Evidence links real certification/flight; Store ID verifies.
- [ ] **T46.6 Commit:** `build: record verified Microsoft Store distribution`.

## Incremento S5 — Auditoría y publicación estable / Increment S5 — Stable Audit and Publication

### Tarea 47 — Puerta final estable / Task 47 — Final Stable Gate

**IDs:** los cinco `STABLE`: `PRD-003`, `PLY-013`, `REL-001`, `REL-003`, `REL-004`; revalidación / revalidation `PRD-005`, `A11Y-001`, `A11Y-002`, `PRI-001`, `PRI-002`, `DOC-001`.

**Archivos / Files:** crear / create `docs/evidence/stable/verification-manifest.json`, `docs/evidence/stable/release-readiness.md`, `docs/release/LICENSE-AUDIT.es.md`, `docs/release/LICENSE-AUDIT.en.md`, `docs/release/SECURITY-PRIVACY-AUDIT.es.md`, `docs/release/SECURITY-PRIVACY-AUDIT.en.md`; modificar / modify `docs/FEATURES.md`, roadmap, changelog, manual, troubleshooting, release guide, privacy, notices, SBOM y listings. / Update every release-facing bilingual document, notice, SBOM, and listing.

**Interfaces / Interfaces:** manifest estable extiende el de MVP con artefactos x64/ARM64/Store/GitHub, hashes, SBOM, cinco IDs bloqueantes y reauditorías; cero excepción informal. / Stable manifest extends MVP with all artifacts, hashes, SBOM, five blockers, and re-audits; no informal exceptions.

- [ ] **T47.1 RED:** linter exige 5/5 IDs STABLE `VERIFIED`, matriz automática, auditoría accesibilidad/Narrator, privacidad de red, licencias/códecs, vulnerabilidades, marca, Store y docs ES/EN sobre los artefactos exactos. / Lint requires all five blockers and every final audit against exact artifacts.
- [ ] **T47.2 Demostrar RED / Prove RED:** ejecutar verificación estable antes de completar informes; esperar lista precisa de puertas abiertas. / Run stable verification before final reports and expect an exact open-gate list.
- [ ] **T47.3 GREEN:** ejecutar todas las matrices x64/ARM64, cerrar documentación/auditorías y enlazar cada evidencia; una desviación de alcance requiere primero spec+FEATURES+ADR aprobados. / Run all matrices, close docs/audits, and link evidence; scope deviations require approved source changes first.
- [ ] **T47.4 Verificar / Verify:** reconstruir artefactos publicados desde tag limpio, comparar hashes normalizados, instalar desde Store/GitHub y ejecutar smoke; dos auditores revisan manifest sin ID faltante. / Rebuild from clean tag, compare normalized hashes, install both channels, smoke, and have two reviewers check the manifest.
- [ ] **T47.5 Aceptación/evidencia / Acceptance/evidence:** Product Owner firma `docs/evidence/stable/release-readiness.md`; solo entonces publicar tag/Store y declarar primera estable. / Product Owner signs readiness before tag/Store publication and stable declaration.
- [ ] **T47.6 Commit:** `docs: close first stable release evidence`.

---

# Parte C — POST_STABLE / Part C — POST_STABLE

Estas tareas preservan toda funcionalidad pendiente de la matriz, pero no se ejecutan ni se prometen antes de la estable. Cada una comienza por decisión/diseño aprobado. / These tasks preserve every remaining matrix feature but are neither executed nor promised before stable. Each starts with an approved decision/design.

### Tarea 48 — Listas personalizadas / Task 48 — Custom Lists

**IDs:** `UX-007`, compatibilidad / compatibility `UX-005`, `DAT-002`.

**Archivos / Files:** crear / create `docs/superpowers/specs/custom-lists.es.md`, `docs/superpowers/specs/custom-lists.en.md`, `docs/adr/0002-custom-lists-data-model.md` si la revisión exige una decisión material, `src/ApSolutions.LocalMedia.Domain/Personalization/CustomList.cs`, `src/ApSolutions.LocalMedia.Domain/Personalization/ICustomListRepository.cs`, `src/ApSolutions.LocalMedia.Application/Personalization/CustomListCommands.cs`, `src/ApSolutions.LocalMedia.Application/Personalization/CustomListQueries.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/CustomListRepository.cs`, `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0012_custom_lists.sql`, `src/ApSolutions.LocalMedia.Presentation/Personalization/CustomListsView.axaml`, `src/ApSolutions.LocalMedia.Presentation/Personalization/CustomListsViewModel.cs`, `tests/ApSolutions.LocalMedia.Domain.Tests/Personalization/CustomListTests.cs`, `tests/ApSolutions.LocalMedia.IntegrationTests/Personalization/CustomListMigrationTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Personalization/CustomListsUiTests.cs`, `docs/evidence/post-stable/T48-custom-lists.md`; modificar / modify `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`, `docs/FEATURES.md`, roadmap y versión del manifest de copia. / Create the exact model, repository, migration, UI, tests, and evidence after the bilingual subspec is approved.

**Interfaces / Interfaces:** `CustomList`, `CustomListItem`, `ICustomListRepository`, `CreateCustomListCommand`, `AddCustomListItemCommand`, `ReorderCustomListItemCommand`, `RemoveCustomListItemCommand`, `DeleteCustomListCommand` y `GetCustomListsQuery`; ninguna cambia `PersonalState`. / Produces the explicit list model, repository, commands, and query without changing `PersonalState`.

- [ ] **T48.1 RED:** primero aprobar subespecificación que fija orden, duplicados, borrado, elementos ausentes, export/import y accesibilidad; prueba de migración demuestra que `PersonalState` existente no cambia. / First approve exact semantics and a non-destructive migration test.
- [ ] **T48.2 Demostrar RED / Prove RED:** ejecutar pruebas de contrato/migración contra schema estable sin tablas de listas; esperar comportamiento ausente y datos personales intactos. / Run contract/migration tests against stable schema; expect missing feature and intact data.
- [ ] **T48.3 GREEN:** implementar el incremento vertical crear→añadir→ordenar→filtrar→exportar/restaurar conforme a la subespecificación, sin reutilizar flags de `PersonalState`. / Implement the full create-to-restore slice without overloading PersonalState flags.
- [ ] **T48.4 Verificar / Verify:** migración desde MVP/estable, recuperación DAT-002, 10.000 elementos, teclado/Narrator y docs ES/EN. / Verify migration, recovery, performance, accessibility, and bilingual docs.
- [ ] **T48.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/post-stable/T48-custom-lists.md`; `UX-007` cambia de `DEFERRED` solo tras aprobación y evidencia. / Status changes only after approval and evidence.
- [ ] **T48.6 Commit:** `feat: add accessible custom media lists`.

### Tarea 49 — Decisión Dolby Vision y passthrough / Task 49 — Dolby Vision and Passthrough Decision

**IDs:** `PLY-015`.

**Archivos / Files:** crear / create `docs/adr/0003-dolby-vision-passthrough.md`, `docs/evidence/post-stable/T49-dolby-evaluation.md`; modificar / modify `docs/FEATURES.md` y roadmap solo tras decisión. Si T48 no requiere ADR, renumerar este ADR al siguiente número libre antes de crearlo y actualizar el enlace en este plan en el mismo commit. No se crean archivos de producción hasta aprobación. / Create decision/evidence only; if T48 does not require an ADR, assign the next free number before creation and update this plan in the same commit. No production files are created before approval.

**Interfaces / Interfaces:** esta tarea produce una decisión documental `Adopt`, `Defer` o `Reject` con condiciones; no produce interfaz de código. Una decisión `Adopt` debe nombrar los nuevos contratos en una especificación y plan posteriores. / This task produces a documented `Adopt`, `Defer`, or `Reject` decision with conditions; it produces no code interface. An `Adopt` decision must name new contracts in a later specification and plan.

- [ ] **T49.1 RED:** definir matriz técnica/legal/demanda: perfiles Dolby Vision, licencias/marcas, LibVLC/hardware/driver, passthrough Dolby/DTS, dispositivos, fallback, accesibilidad y coste cero; el test documental falla mientras no exista ADR aprobada. / Define the full technical/legal/demand matrix; docs test fails until an approved ADR exists.
- [ ] **T49.2 Demostrar RED / Prove RED:** ejecutar revisión de capacidades sobre artefactos estables y confirmar que UI/claims no anuncian soporte. / Review stable artifacts and confirm they make no support claim.
- [ ] **T49.3 Decidir / Decide:** Product Owner aprueba `Adopt`, `Defer` o `Reject` con responsables, coste y condiciones; `Adopt` exige una nueva especificación/plan TDD antes de código. / Product Owner records an explicit outcome; adoption requires a new spec and TDD plan before code.
- [ ] **T49.4 Verificar / Verify:** auditor técnico y jurídico revisan evidencia y compatibilidad GPL/coste; no inferir soporte de que una muestra reproduzca accidentalmente. / Technical/legal reviewers validate evidence; accidental playback is not support.
- [ ] **T49.5 Aceptación/evidencia / Acceptance/evidence:** `docs/evidence/post-stable/T49-dolby-evaluation.md`; `PLY-015` conserva `OUT_OF_SCOPE` si no hay `Adopt`, o cambia según la nueva decisión aprobada. / Preserve out-of-scope unless an approved adoption decision changes it.
- [ ] **T49.6 Commit:** `docs: decide post-stable Dolby media scope`.

---

## 4. Matriz completa de trazabilidad / Complete Traceability Matrix

La tabla enumera los 53 IDs de `docs/FEATURES.md`; la prueba T41 compara esta lista con la matriz canónica y falla ante cualquier omisión, duplicado de objetivo o ID inventado. / This table enumerates all 53 IDs in `docs/FEATURES.md`; T41 compares it with the canonical matrix and fails on any omission, target mismatch, or invented ID.

### MVP — 46 IDs / MVP — 46 IDs

| ID | Tareas / Tasks | Evidencia de cierre / Closing evidence |
|---|---|---|
| `PRD-001` | T2, T4, T5, T31, T36 | T41 manifest: sin cuenta/red y persistencia local / no account/network and local persistence |
| `PRD-002` | T40 | `T40-x64-packaging.md` |
| `PRD-004` | T1, T4, T18 | T1 architecture graph + tests |
| `PRD-005` | T1, T19, T40, T41 | licencia, notices, SBOM, artifact audit / license, notices, SBOM, artifact audit |
| `LIB-001` | T5 | `T5-roots.md` |
| `LIB-002` | T6, T9, T10, T39 | scan + watch + budget + recovery evidence |
| `LIB-003` | T9 | `T9-watching.md` |
| `LIB-004` | T7, T10 | `T10-10k-performance.md` |
| `LIB-005` | T11 | `T11-name-parser.md` |
| `LIB-006` | T12, T13, T14 | confidence + provider + inbox evidence |
| `LIB-007` | T12, T14 | `T14-review-inbox.md` |
| `LIB-008` | T12, T15, T27 | duplicate/version fixtures |
| `LIB-009` | T8, T27, T37 | identity + restore remap evidence |
| `LIB-010` | T5, T8, T9, T28, T37, T39 | device-loss and recovery matrix |
| `LIB-011` | T13, T16, T36 | merge, artwork, export evidence |
| `LIB-012` | T17 | `T17-safe-rename.md` |
| `PLY-001` | T18, T19, T35, T39 | engine, fallback, loose-file, recovery evidence |
| `PLY-002` | T19 | `T19-codec-matrix.md` |
| `PLY-003` | T22 | physical GPU/display matrix |
| `PLY-004` | T20, T23 | physical audio matrix + persistence |
| `PLY-005` | T20 | `T20-tracks-subtitles.md` |
| `PLY-006` | T21 | `T21-controls-limiter.md` |
| `PLY-007` | T18, T24 | `T24-windows-input.md` |
| `PLY-008` | T25, T39 | 20 crash/resume trials + recovery |
| `PLY-009` | T26 | `T26-watch-status.md` |
| `PLY-010` | T15, T27 | `T27-version-progress.md` |
| `PLY-011` | T28 | `T28-next-episode.md` |
| `PLY-012` | T29 | `T29-manual-markers.md` |
| `PLY-014` | T21, T24, T28, T33 | input/UIA/accessibility evidence |
| `UX-001` | T7, T26, T30 | `T30-home-details.md` |
| `UX-002` | T2, T3, T30, T33 | design QA + accessibility report |
| `UX-003` | T3, T30, T33 | theme/contrast/scaling evidence |
| `UX-004` | T2, T3, T7, T30, T41 | localization lint + bilingual snapshots |
| `UX-005` | T31, T36, T37 | personal state + round-trip export evidence |
| `UX-006` | T32, T38 | local recommendation + privacy capture |
| `UX-008` | T11, T41 | negative scope review; remains `OUT_OF_SCOPE` |
| `A11Y-001` | T2, T3, T14, T21, T24, T30, T33 | signed end-to-end accessibility report |
| `A11Y-002` | T3, T20, T33 | reduced-motion/subtitle/scaling report |
| `DAT-001` | T4, T25, T37, T39 | database, migration, restore, recovery evidence |
| `DAT-002` | T16, T31, T36, T37 | complete video-free round-trip restore |
| `PRI-001` | T13, T17, T32, T34, T35, T38 | 30-minute network/privacy capture |
| `PRI-002` | T38 | payload contract + canary scan |
| `SYS-001` | T24, T34, T39 | lifecycle, consent, idle evidence |
| `SYS-002` | T35, T40 | packaged shell activation + unchanged DB |
| `REL-002` | T40 | GitHub artifact, hashes, SBOM, reproducibility |
| `DOC-001` | T1, T2, T19, T38, T40, T41 | documentation CI + final bilingual manifest |

### Primera versión estable — 5 IDs / First stable release — 5 IDs

| ID | Tareas / Tasks | Evidencia de cierre / Closing evidence |
|---|---|---|
| `PRD-003` | T42, T46, T47 | ARM64 physical/package/Store report |
| `PLY-013` | T29, T43, T47 | held-out multi-show detection benchmark |
| `REL-001` | T46, T47 | Store certification and flight update |
| `REL-003` | T44, T47 | interrupted/tampered/confirmed update matrix |
| `REL-004` | T2, T45, T46, T47 | ADR + current formal clearance + Store reservation |

### POST_STABLE — 2 IDs / POST_STABLE — 2 IDs

| ID | Tareas / Tasks | Estado/evidencia / Status/evidence |
|---|---|---|
| `UX-007` | T31 (límite / boundary), T48 | `DEFERRED` hasta subespecificación y evidencia / until subspec and evidence |
| `PLY-015` | T22–T23 (límite / boundary), T49 | `OUT_OF_SCOPE` salvo nueva decisión / unless a new decision is approved |

## 5. Matriz de pruebas obligatoria / Mandatory Test Matrix

| Tipo / Type | Automatización mínima / Minimum automation | Revisión no automatizable / Non-automatable review | Puerta / Gate |
|---|---|---|---|
| Unitarias / Unit | Todas las políticas y casos de uso puros; ≥90 % ramas de dominio. / All pure policies/use cases; ≥90% domain branches. | Revisión de nombres/contratos públicos. / Public naming/contracts review. | Cada tarea / Every task |
| Propiedades/fuzz | Parser, rutas, identidad, estados, rename, manifest/ZIP; semillas guardadas. / Parser, paths, identity, state, rename, manifest/ZIP; saved seeds. | Triage de cualquier caso minimizado. / Triage minimized cases. | I2, I6 |
| Integración | SQLite/migraciones, FS temporal+UNC falso, TMDB HTTP falso, backup/import, Windows adapters. / SQLite/migrations, temp/fake UNC FS, fake TMDB HTTP, backup/import, Windows adapters. | UNC/USB físico representativo. / Representative physical UNC/USB. | I1, I2, I6 |
| Contrato / Contract | Cada adapter contra su puerto; fake y real comparten suite. / Every adapter against its port; fake and real share a suite. | Comparar capacidades no simulables. / Compare non-simulatable capabilities. | Antes de integrar adapter / Before adapter integration |
| Multimedia real / Real media | Manifiesto T19, hashes, A/V, pistas, errores, HDR/SDR. / T19 manifest, hashes, A/V, tracks, errors, HDR/SDR. | GPU, pantalla y audio físicos x64; ARM64 en estable. / Physical x64 GPU/display/audio; ARM64 at stable. | I3, S1 |
| UI | Avalonia.Headless, navegación, recursos, snapshots, temas, DPI. / Headless navigation, resources, snapshots, themes, DPI. | QA visual Fluent. / Fluent visual QA. | Cada incremento visible / Every visible increment |
| Accesibilidad | UIA, orden Tab, nombres/roles/estado, contraste, 200 %, movimiento/subtítulos. / UIA, Tab order, names/roles/state, contrast, 200%, motion/subtitles. | Teclado+Narrator+alto contraste ES/EN. / Keyboard+Narrator+high contrast ES/EN. | I5 y estable / I5 and stable |
| Rendimiento | 10k, búsqueda, frame times, UI block, slow NAS, scan+playback. / 10k, search, frames, UI block, slow NAS, scan+playback. | Hardware mínimo Windows 11 registrado. / Recorded minimum Windows 11 hardware. | I1, I6, S2 |
| Recuperación | 9 fallos, child-process kill, DB/migration corruption, interrupted update. / Nine failures, child-process kill, DB/migration corruption, interrupted update. | Retirada real USB/NAS y VM snapshots. / Real USB/NAS removal and VM snapshots. | I6, S3 |
| Empaquetado | install/update/downgrade/repair/uninstall, associations, contents, hashes. / Lifecycle, associations, contents, hashes. | VM limpia; Store flight; SmartScreen behavior. / Clean VM; Store flight; SmartScreen behavior. | I7, S1, S4 |
| Privacidad/seguridad | PCAP/proxy, canarios, secrets, dependencies, archive traversal, path confinement. / PCAP/proxy, canaries, secrets, dependencies, archive traversal, path confinement. | Auditoría de payload, licencias y permisos. / Payload, license, and capability audit. | I6, estable / stable |
| Documentación | IDs, enlaces, evidencia, pares ES/EN, recursos, headings. / IDs, links, evidence, ES/EN pairs, resources, headings. | Exactitud editorial y de marca. / Editorial and brand accuracy. | Cada gate / Every gate |

### Comandos de verificación por puerta / Gate Verification Commands

```powershell
# Preparación reproducible / Reproducible setup
dotnet tool restore
dotnet restore ApSolutions.LocalMedia.sln --locked-mode
dotnet format ApSolutions.LocalMedia.sln --verify-no-changes --no-restore
dotnet build ApSolutions.LocalMedia.sln -c Release --no-restore -warnaserror

# Unitarias, integración y arquitectura / Unit, integration, architecture
dotnet test tests/ApSolutions.LocalMedia.Domain.Tests -c Release --no-build --logger trx
dotnet test tests/ApSolutions.LocalMedia.Application.Tests -c Release --no-build --logger trx
dotnet test tests/ApSolutions.LocalMedia.ArchitectureTests -c Release --no-build --logger trx
dotnet test tests/ApSolutions.LocalMedia.IntegrationTests -c Release --no-build --logger trx

# UI, accesibilidad, multimedia y rendimiento / UI, accessibility, media, performance
dotnet test tests/ApSolutions.LocalMedia.UiTests -c Release --no-build --logger trx
pwsh ./eng/run-accessibility.ps1 -Mode Verify
pwsh ./eng/generate-test-media.ps1 -Output artifacts/test-media
dotnet test tests/ApSolutions.LocalMedia.MediaTests -c Release --filter "Category=RealMedia" --logger trx
pwsh ./eng/run-performance.ps1 -Baseline docs/evidence/mvp/performance-baseline.md
pwsh ./eng/run-recovery.ps1 -Mode Verify

# Documentación, dependencias, seguridad y evidencia / Docs, dependencies, security, evidence
pwsh ./eng/verify-docs.ps1
dotnet list ApSolutions.LocalMedia.sln package --vulnerable --include-transitive
dotnet list ApSolutions.LocalMedia.sln package --deprecated
pwsh ./eng/generate-sbom.ps1 -Runtime win-x64 -Output artifacts/sbom
pwsh ./eng/verify.ps1 -Configuration Release -Runtime win-x64

# Paquete MVP / MVP package
pwsh ./eng/package-x64.ps1 -Configuration Release -Output artifacts/package/win-x64
pwsh ./eng/verify-package.ps1 -Package artifacts/package/win-x64
dotnet test tests/ApSolutions.LocalMedia.PackagingTests -c Release --filter "Architecture=x64" --logger trx

# Puerta estable adicional / Additional stable gate
pwsh ./eng/package-arm64.ps1 -Configuration Release -Output artifacts/package/win-arm64
dotnet test tests/ApSolutions.LocalMedia.PackagingTests -c Release --filter "Architecture=arm64" --logger trx
dotnet test tests/ApSolutions.LocalMedia.MediaTests -c Release --filter "Architecture=arm64" --logger trx
```

## 6. Dependencias y precondiciones / Dependencies and Preconditions

| ID | Dependencia / Dependency | Necesaria antes de / Needed before | Comprobación / Check | Respuesta si falla / Response if missing |
|---|---|---|---|---|
| `D-01` | Windows 11 x64, PowerShell 7, .NET 10 SDK, VS Build Tools y Windows 11 SDK/MSIX. / Windows toolchain. | T1, T40 | `dotnet --info`, `msbuild -version`, SDK 22621+. | Bloquear solo compilación/paquete afectado y registrar instalación requerida. / Block affected build/package only and record requirement. |
| `D-02` | Avalonia 12.1.x, LibVLCSharp 3.x y runtimes LibVLC x64/ARM64 estables compatibles. / Compatible native runtimes. | T1, T18, T42 | restore bloqueado + smoke por RID. / Locked restore + RID smoke. | Detener en T18/T42; evaluar revisión compatible detrás de adapters, sin cambiar stack silenciosamente. / Stop and review compatible patch behind adapters. |
| `D-03` | Token de lectura TMDB para integración real; servidor falso siempre disponible. / TMDB read token and always-available fake server. | T13 | secreto CI/local + canary scan. / CI/local secret + canary scan. | Continuar modo offline/contrato; bloquear solo evidencia real TMDB. / Continue offline/contract; block only real-provider evidence. |
| `D-04` | Corpus multimedia legal/generable y herramienta fijada de generación. / Legal/generable media corpus and pinned generator. | T19, T43 | manifiesto, licencia/receta, hash. / Manifest, license/recipe, hash. | Reemplazar muestra por equivalente legal; nunca usar colección del usuario. / Replace with legal equivalent; never use user media. |
| `D-05` | Hardware x64 de referencia: GPU HDR/SDR, audio estéreo+5.1/7.1, USB y NAS/UNC. / Reference x64 hardware. | T10, T22–T23, T33, T39 | inventario firmado en evidencia. / Signed inventory. | Mantener ID sin `VERIFIED`; no convertir SKIP en PASS. / Keep ID unverified; no SKIP-to-PASS. |
| `D-06` | Hardware Windows 11 ARM64 equivalente. / Equivalent ARM64 hardware. | T42, T46 | inventario + paquete nativo + matriz. / Inventory + native package + matrix. | Bloquea estable, no MVP. / Blocks stable, not MVP. |
| `D-07` | Responsable jurídico/producto y acceso a fuentes oficiales para marca. / Legal/product owner and official sources. | T45 | informe firmado y vigente. / Signed current report. | `REL-004=BLOCKED`; no reservar/publicar Store. / Block release and Store reservation. |
| `D-08` | Cuenta Partner Center/Store y vuelo privado de coste aprobado cero. / Partner Center and zero-cost-approved private flight. | T46 | acceso, identidad reservada tras T45. / Access and post-clearance identity. | Bloquea `REL-001`, conserva GitHub MVP. / Blocks Store stable; GitHub MVP remains. |
| `D-09` | Runner Windows x64 y almacenamiento de artefactos/evidencias CI. / Windows CI and artifact storage. | T1 en adelante / onward | workflow smoke + retención. / Workflow smoke + retention. | Ejecutar localmente con evidencia provisional; no `VERIFIED` final hasta CI reproducible. / Local provisional evidence only. |

## 7. Registro de riesgos / Risk Register

| Riesgo / Risk | Impacto / Impact | Señal temprana / Trigger | Mitigación y punto de control / Mitigation and checkpoint | Responsable / Owner |
|---|---|---|---|---|
| Avalonia/LibVLC no permite overlay, HDR o mini estable. / Integration cannot meet overlay/HDR/mini needs. | Alto / High | fuga, superficie separada, overlay roto en T18. | Spike T18 antes de continuidad; conservar `IMediaPlayerEngine`, detener I3 si falla. / T18 spike before continuity; keep adapter boundary and stop I3. | Engineering |
| Runtime nativo ARM64 incompleto. / Missing ARM64 native runtime. | Alto para estable / High for stable | restore RID o smoke T1/T42 falla. | Comprobación temprana T1; sustitución solo detrás de adapter y certificación física T42. / Early restore; adapter-only substitute and physical certification. | Engineering/Release |
| Token TMDB extraído o cuota agotada. / Token extraction or quota exhaustion. | Medio / Medium | canario en log, 429, latencia. | token limitado, caché, rate limiter/backoff, rotación, modo offline; proxy propio requiere nueva decisión. / Limited token, cache, limiter, rotation, offline; proxy requires new decision. | Engineering/Product |
| Eventos NAS perdidos/duplicados o NAS saturado. / Lost/duplicate NAS events or saturation. | Medio / Medium | divergencia watch/fallback, latencia elevada. | reconciliación idempotente, límites por raíz, fallback T9, slow-NAS T39. / Idempotency, per-root limits, fallback, slow-NAS gate. | Engineering/QA |
| Renombrado de red falla parcialmente. / Network rename partially fails. | Alto / High | desconexión tras primera operación. | prevalidación, log por ítem, ejecución conservadora, recuperación guiada T17; nunca afirmar atomicidad. / Prevalidate, per-item log, conservative execution, guided recovery. | Engineering/QA |
| Rendimiento 10k o reproducción+escaneo incumple. / 10k or concurrent playback misses budget. | Alto / High | p95 excede T10/T39. | perf tests desde I1, perfilar antes de optimizar, no relajar presupuesto sin ADR. / Early tests, profile-first, no budget relaxation without ADR. | Engineering |
| Accesibilidad Avalonia/UIA insuficiente. / Avalonia/UIA accessibility gap. | Alto / High | control sin peer/estado en T2/T14. | automation metadata desde cada vista, FlaUI + Narrator T33, sustituir control aislado si es necesario. / Build metadata early, audit, replace isolated control if needed. | Design/QA |
| Detección de segmentos no alcanza calidad. / Segment detection misses quality. | Alto para estable / High for stable | falsos positivos por serie en baseline/held-out. | subespecificación y umbral antes de código, corpus retenido, corrección humana; cambio de bloqueo solo por alcance aprobado. / Frozen thresholds, held-out corpus, human correction; scope change requires approval. | Product/Engineering |
| Licencias de códec/dependencias/muestras incompatibles. / Codec/dependency/sample license conflict. | Alto / High | auditoría T19/T40/T47 detecta obligación no satisfecha. | GPL review, muestras generadas/licenciadas, SBOM/notices por artefacto; retirar/sustituir antes de publicar. / GPL review, legal samples, per-artifact SBOM/notices; replace before release. | Release/Legal |
| Identidad MSIX de prueba no migra limpiamente a Store. / Test MSIX identity does not migrate cleanly. | Alto para estable / High for stable | upgrade/association falla T46. | package identity estable desde T40, documentar canal paralelo/migración, vuelo privado y backup previo. / Stable identity early, documented migration/channel, private flight and backup. | Release |
| Alcance amplio retrasa valor usable. / Broad scope delays usable value. | Alto / High | una puerta acumula tareas de otro incremento. | demo vertical y gate por incremento; no empezar siguiente con evidencia/matriz atrasada. / Vertical demos and strict gates. | Product Owner |
| Nombre no recibe autorización formal. / Name fails formal clearance. | Alto para estable / High for stable | resultado T45 `NotCleared`. | no cambiar IDs internos; bloquear Store y abrir ADR de nombre antes de material público final. / Preserve internal IDs, block Store, open naming ADR. | Product Owner/Legal |

## 8. Puntos de control y aprobaciones / Checkpoints and Approvals

| Punto / Checkpoint | Entrada obligatoria / Required input | Salida para continuar / Exit to continue | Aprobador / Approver |
|---|---|---|---|
| `C0 Plan` | Este documento + spec + matriz + ADR. / This plan + source docs. | Aprobación explícita del plan; actualizar IDs MVP a `PLANNED` al iniciar, no al aprobar el documento. / Explicit plan approval; move IDs to planned only when work starts. | Product Owner |
| `C1 Foundation` | T1–T4. | Demo I0, CI verde, dependencias fijadas, DB íntegra, cero tráfico. / I0 demo, green CI, locked deps, valid DB, no traffic. | Engineering lead |
| `C2 Library` | T5–T10. | Biblioteca real/10k, USB/UNC, todos los presupuestos. / Real/10k library, USB/UNC, all budgets. | Product + QA |
| `C3 Identification` | T11–T17. | Corpus/umbrales/revisión/TMDB offline/rename seguro. / Corpus, thresholds, review, offline TMDB, safe rename. | Product + QA |
| `C4 Playback` | T18–T24. | Matriz legal + hardware x64 + teclado. / Legal matrix + x64 hardware + keyboard. | Engineering + QA |
| `C5 Continuity` | T25–T29. | ±5 s, estados/versiones/siguiente/marcas. / Resume, states, versions, next, markers. | Product + QA |
| `C6 Experience` | T30–T35. | QA visual, 0 defectos A11Y críticos/mayores, opt-ins. / Visual QA, zero critical/major A11Y, opt-ins. | Design + QA |
| `C7 Recovery` | T36–T39. | Restore distinto, privacidad y matriz de fallos dos veces. / Different-path restore, privacy, two recovery passes. | Security/QA |
| `C8 MVP x64` | T40–T41. | 46/46 IDs resueltos, artefacto probado, docs ES/EN. / 46/46 IDs resolved, tested artifact, bilingual docs. | Product Owner |
| `C9 Stable tech` | T42–T44. | ARM64, detector y updater verificados. / Verified ARM64, detector, updater. | Product + Engineering |
| `C10 Stable public` | T45–T47. | Marca autorizada, Store certificada, 5/5 bloqueantes, auditorías finales. / Clearance, Store certification, 5/5 blockers, final audits. | Product Owner/Legal/Release |
| `C11 Post-stable` | Telemetría real de uso no privada, feedback y nueva decisión. / Privacy-safe real usage signals, feedback, and new decision. | Spec/ADR aprobado antes de T48/T49. / Approved spec/ADR before work. | Product Owner |

## 9. Auditoría de coherencia realizada / Completed Coherence Audit

- [x] **Cobertura / Coverage:** los 53 IDs de la matriz aparecen en la sección 4; 46 MVP, 5 STABLE y 2 POST_STABLE. / All 53 matrix IDs appear: 46 MVP, 5 stable, 2 post-stable.
- [x] **Orden / Ordering:** el MVP x64 termina antes de ARM64, Store, updater y detección automática; las exclusiones no reciben implementación accidental. / x64 MVP finishes before ARM64, Store, updater, and automatic detection; exclusions receive no accidental implementation.
- [x] **Arquitectura / Architecture:** Domain/Application no dependen de Avalonia, Windows, SQLite, TMDB o LibVLC; todos los adaptadores tienen contrato y prueba. / Core layers avoid framework/vendor dependencies; every adapter has a contract and test.
- [x] **Modelo / Model:** nombres de contratos, IDs, repositorios, eventos y migraciones son consistentes entre productor y consumidor; `IntroMarker` permite stable sin habilitarlo en MVP. / Contract/type names and migrations are consistent; marker model is forward-compatible without enabling stable scope.
- [x] **Datos / Data:** progreso pertenece al contenido, conserva versión origen, duplicados siguen visibles, desconexión conserva catálogo y restore usa staging. / Progress belongs to content with source audit; duplicates remain visible, disconnect preserves catalog, restore stages.
- [x] **Marca / Brand:** solo recursos/superficies públicas usan AP Reelume; namespace/package/schema son estables; autorización formal sigue en T45. / Only public surfaces use AP Reelume; internal identifiers are stable; formal clearance remains T45.
- [x] **Pruebas / Testing:** unitarias, integración, contrato, fuzz, UI, accesibilidad, rendimiento, multimedia, recuperación, privacidad y empaquetado tienen tareas/comandos/puertas. / Every required test class has tasks, commands, and gates.
- [x] **Documentación / Documentation:** pares ES/EN y CI documental se crean en T1 y bloquean cada release en T41/T47. / ES/EN pairs and docs CI start in T1 and block both release gates.
- [x] **Marcadores vacíos / Empty markers:** el plan no depende de marcadores de trabajo abierto, tareas genéricas ni referencias “igual que otra tarea”; las decisiones futuras tienen criterio, propietario y puerta explícitos. / The plan has no open-work markers or generic “same as” work; future decisions have criteria, owners, and gates.

## 10. Inicio de ejecución tras aprobación / Execution Start After Approval

Al aprobar este plan, el primer bloque de implementación es exclusivamente **T1–T4 (I0)**. El ejecutor debe actualizar los 46 IDs MVP de `DESIGN_APPROVED` a `PLANNED`, crear la rama `codex/ap-reelume-mvp-x64`, aplicar TDD tarea por tarea y volver al punto `C1` con demo y evidencias antes de iniciar I1. No se escribe código de `STABLE` o `POST_STABLE` durante el MVP salvo los modelos/contratos explícitamente diseñados para compatibilidad y las comprobaciones tempranas de dependencia ARM64. / After approval, implementation starts only with **T1–T4 (I0)**. The implementer updates all 46 MVP IDs from `DESIGN_APPROVED` to `PLANNED`, creates branch `codex/ap-reelume-mvp-x64`, follows task-by-task TDD, and returns at checkpoint `C1` with demo and evidence before I1. No stable or post-stable production code is written during MVP except explicitly forward-compatible models/contracts and early ARM64 dependency checks.
