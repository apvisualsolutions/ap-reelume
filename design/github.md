repo: apvisualsolutions/ap-reelume
branch: codex/ap-reelume-mvp-x64
path: src/ApSolutions.LocalMedia.Presentation, docs

## Last sync

date: 2026-08-17T20:09:57Z

### Updated in this project

- Auditoría del inventario visible contra `docs/design/SURFACES.es.md`: 48 vistas confirmadas, 181 enlaces `IsVisible` medidos, 6 cadenas de vacío para 14 listas.
- Huecos documentados: superficies que no son `.axaml` (ventana principal, bandeja, diálogos del sistema, Explorador, arranque MSIX, cromo interno de `ShellView`).
- Temas: 3 diccionarios en `DesignTokens.axaml`, sólo 2 aplicables; alto contraste claro sin diccionario; `PlayerThemeVariant` fuerza oscuro.
- Recuento de tokens medido: 58 declaraciones / 40 nombres distintos + 3 de `Brand.axaml` (el inventario declara 61).
- Prototipo completado con la superficie de Actualización (23 mensajes: 15 estados + 7 motivos de rechazo) y el cuarto tema de alto contraste claro; acento de alto contraste corregido a `#00FFFF` y `--warn-fg` a `#FFFFFF` para reservar `#FFFF00` al foco.
- Compatibilidad AXAML documentada. Discrepancia detectada: el encargo dice Avalonia 11, `Directory.Packages.props` pina **12.1.1**.
- Copia y restauración construidas desde `BackupView.axaml` y `RestoreWizardView.axaml`: una zona viva por vista, etapas con barra, hallazgos con tono, y reasignación de raíces con campo editable por raíz ausente.
- **Las 48 vistas cubiertas.** Últimas cinco: `VideoStatusOverlay` (6 avisos, datos vs degradaciones), `NextEpisodeOverlay`, `DiagnosticsPreviewView` (condicional), `StartupView` y los siete motivos de `PlayerView`. Copia tomada de `Strings.es.axaml` en todos los casos.
- `PlayerView`: los siete motivos de fallo desde el AXAML, con la copia tomada de `Strings.es.axaml` (161-171) y «Elegir otra versión» como flag independiente del motivo, como lo modela `CanChooseAnotherVersion`.
- `RootOnboardingView`: primer arranque en sus cuatro formas (sin raíces, con raíces, confirmando borrado, consentimiento de escaneo) más el fallo al añadir; las dos zonas vivas como `Assertive`, no `Polite`. 43 vistas de 48.
- Marcadores: panel nuevo en el reproductor con `DetectedMarkerReviewView` (aceptar/corregir/descartar, con confianza) y `MarkerEditorView` (tipo, inicio, fin, errores de rango y de solape), más `SegmentDetectionSettingsView` como sección de Ajustes. 42 vistas de 48.
- Tráiler añadido al prototipo en sus dos formas (LIB-014): archivo local por `TrailerDiscoveryPolicy` (solo películas, nombrado desde el archivo de vídeo) y enlace al navegador por `TrailerLinkPolicy` (películas y series), con la explicación de por qué ese anfitrión no está en la lista declarada.

## Sync history

- 2026-08-02T14:34:15Z · commit dfa606f3756d · prototipo interactivo, tokens, rutas y documento de dirección de diseño.

## Screen map

| Pantalla del proyecto | Archivos del repositorio |
|---|---|
| Shell, barra de título, navegación | `src/ApSolutions.LocalMedia.Presentation/Shell/ShellView.axaml`, `Shell/ShellViewModel.cs`, `Navigation/NavigationService.cs` |
| Tokens y temas (claro/oscuro/alto contraste, foco, movimiento) | `src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml`, `Theme/FluentThemeService.cs`, `Theme/IReducedMotionService.cs`, `Theme/IBackdropService.cs` |
| Biblioteca (cuadrícula, filtros, disponibilidad) | `src/ApSolutions.LocalMedia.Presentation/Library/LibraryViewModel.cs`, `Library/UnavailableBadge.axaml`, `Library/ScanProgressViewModel.cs` |
| Revisión de coincidencias | `src/ApSolutions.LocalMedia.Presentation/Review/ReviewInboxViewModel.cs`, `Review/CandidateCardView.axaml` |
| Duplicados y versiones | `src/ApSolutions.LocalMedia.Presentation/Review/DuplicateReviewViewModel.cs`, `Review/DuplicateReviewView.axaml` |
| Editor de metadatos y renombrado seguro | `src/ApSolutions.LocalMedia.Presentation/Metadata/MetadataEditorViewModel.cs`, `Metadata/RenamePreviewViewModel.cs`, `Metadata/ArtworkPickerViewModel.cs` |
| Ajustes (apariencia, escaneo) | `src/ApSolutions.LocalMedia.Presentation/Settings/AppearanceSettingsViewModel.cs`, `Settings/ScanSettingsViewModel.cs` |
| Alcance funcional e identificadores (PLY, LIB, A11Y, PRI, SYS) | `docs/FEATURES.md`, `docs/adr/0001-public-product-name.md` |
| Auditoría del inventario (documento imprimible) | `docs/design/SURFACES.es.md`, `Shell/ShellView.axaml`, `Updates/UpdateView.axaml`, `Recovery/DatabaseRecoveryView.axaml`, `Player/MiniPlayerWindow.axaml`, `Settings/PrivacySettingsView.axaml`, `Theme/DesignTokens.axaml`, `Theme/ThemePreference.cs`, `Theme/FluentThemeService.cs`, `Windows/Tray/WindowsTrayService.cs`, `Windows/Backup/HandoffArchivePicker.cs`, `Windows.Package/Package.appxmanifest`, `eng/walk-pending.txt` |
