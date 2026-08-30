repo: apvisualsolutions/ap-reelume
branch: codex/ap-reelume-mvp-x64
path: src/ApSolutions.LocalMedia.Presentation, docs

## Last sync

date: 2026-08-30T13:33:42Z

### Updated in this project

- Censo releído de `docs/design/SURFACES.es.md`: el rediseño está **cerrado en el árbol** (53 vistas, paridad PRD-006 verificada el 2026-08-24, 576 claves por idioma). Los documentos de auditoría de este proyecto cumplieron su función y se retiraron.
- **Vistas por separado**: `vistas/` con 57 archivos — las 53 del censo + 4 de Cursos — que abren el prototipo directamente en cada vista (prop `view`, sin duplicar fuente).
- **Propuesta nueva: Cursos (CRS-001…CRS-005)** — carpetas de videocursos con hilo (última lección, minuto, fecha y resumen), construida sobre PLY-008/PLY-009/LIB-009 y sin red. Prototipada completa; pendiente de decisión de alcance en `FEATURES.md`.
- Prototipo puesto al día con el censo: carril de recomendaciones en Inicio (`RecommendationsRailView`, apagado = ausente) y sección de Ajustes Recomendaciones con el umbral de visto, como lo modela WP-2.
- Limpieza: quedan el prototipo, `vistas/`, Propuesta, Catálogo, Cadenas (+41 claves CRS propuestas), README y PROMPT reescritos para la fase de Cursos.

## Sync history

- 2026-08-17T20:09:57Z · auditoría del inventario contra `SURFACES.es.md` (48 vistas entonces), temas, copia/restauración, actualización, las 48 vistas cubiertas en el prototipo.
- 2026-08-02T14:34:15Z · commit dfa606f3756d · prototipo interactivo, tokens, rutas y documento de dirección de diseño.

## Screen map

| Pantalla del proyecto | Archivos del repositorio |
|---|---|
| Shell, barra de título, navegación (`vistas/ShellView`) | `src/ApSolutions.LocalMedia.Presentation/Shell/ShellView.axaml`, `Shell/ShellViewModel.cs`, `Navigation/NavigationService.cs` |
| Tokens y temas (claro/oscuro/alto contraste, foco, movimiento) | `src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml`, `Theme/FluentThemeService.cs`, `Theme/IReducedMotionService.cs` |
| Inicio y carriles (`vistas/HomeView`, `ResumeHeroView`, `InProgressRailView`, `RecentlyAddedRailView`, `RecommendationsRailView`) | `Home/HomeView.axaml` y carriles de `Home/` |
| Biblioteca (`vistas/LibraryView`, `PosterCardView`, `UnavailableBadge`) | `Library/LibraryViewModel.cs`, `Library/UnavailableBadge.axaml`, `Library/ScanProgressViewModel.cs` |
| Fichas (`vistas/MovieDetailsView`, `ShowDetailsView`, `EpisodeRowView`) | `Details/` |
| Reproductor y superposiciones (17 archivos de `vistas/`) | `Player/PlayerView.axaml`, `Player/TransportControlsView.axaml`, `Player/MiniPlayerWindow.axaml` y compañía |
| Revisión y duplicados (`vistas/ReviewInboxView`, `CandidateCardView`, `DuplicateReviewView`, `DuplicatesOverviewView`) | `Review/` |
| Metadatos y renombrado (`vistas/MetadataEditorView`, `RenamePreviewView`) | `Metadata/` |
| Ajustes (8 archivos de `vistas/`) | `Settings/` |
| Copia y restauración (`vistas/BackupView`, `RestoreWizardView`) | `Backup/BackupView.axaml`, `Backup/RestoreWizardView.axaml` |
| Primeros pasos, arranque, recuperación (`vistas/RootOnboardingView`, `AddRootDialogView`, `StartupView`, `DatabaseRecoveryView`) | `Onboarding/`, `Shell/StartupView.axaml`, `Recovery/DatabaseRecoveryView.axaml` |
| Actualización y créditos (`vistas/UpdateView`, `CreditsView`) | `Updates/UpdateView.axaml`, `Credits/CreditsView.axaml` |
| **Cursos — propuesta, sin archivos en el repo aún** (`vistas/CoursesView`, `CourseDetailsView`, `LessonRowView`, `LessonsPanelView`) | destino propuesto: `Courses/` en Presentation + migración en datos (ver PROMPT.md) |
| Alcance funcional e identificadores | `docs/FEATURES.md`, `docs/design/SURFACES.es.md` |
