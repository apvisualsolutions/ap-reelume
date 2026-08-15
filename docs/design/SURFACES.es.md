# Todo lo que se ve

Inventario de las superficies visibles de AP Reelume, para que un rediseño pueda cubrirlas **todas**
y no sólo las que se recuerdan. La versión inglesa está en [SURFACES.en.md](SURFACES.en.md).

Este documento no decide estética. Dice **qué existe, dónde vive y en qué estados aparece**, medido
del árbol el 2026-08-15, para que nada visible se quede sin diseñar.

## La regla que ya se cumple, y no hay que rehacer

- **Las 48 vistas usan cadenas localizadas.** Ninguna tiene texto sin traducir: la medición no
  encontró ni una sola vista sin `DynamicResource`.
- **468 claves de cadena en español y 468 en inglés**, en
  `src/ApSolutions.LocalMedia.Presentation/Resources/Strings.es.axaml` y `Strings.en.axaml`.
  `BilingualHeadingTests` compara la estructura de los documentos públicos, y una cadena visible
  nueva va en los dos archivos o no va.
- **Los únicos tres textos literales del árbol son símbolos, no idioma**: `○ ◐ ●` (estado de visto),
  `→` (origen y destino de un renombrado) y `!` (aviso del transporte). Si el rediseño los sustituye
  por iconos, el nombre accesible sigue viniendo de `AutomationProperties`, que ya está puesto.
- **Cada control interactivo tiene nombre accesible**, y hay 80 pruebas de accesibilidad que lo
  exigen. Un rediseño puede cambiar la forma, no quitarle el nombre.

## Las 48 vistas, por área

| Área | Vistas |
| --- | --- |
| Shell (2) | `ShellView`, `StartupView` |
| Inicio (5) | `HomeView`, `ResumeHeroView`, `InProgressRailView`, `RecommendationsRailView`, `LibraryEntryView` |
| Biblioteca (2) | `LibraryView`, `UnavailableBadge` |
| Ficha de película (1) | `MovieDetailsView` |
| Ficha de serie (2) | `ShowDetailsView`, `EpisodeRowView` |
| Reproductor (16) | `PlayerView`, `TransportControlsView`, `VideoStatusOverlay`, `ResumePromptView`, `NextEpisodeOverlay`, `SkipMarkerButton`, `MarkerEditorView`, `DetectedMarkerReviewView`, `TrackSelectorView`, `AudioOutputView`, `SubtitleStyleView`, `ShortcutSettingsView`, `PlayerVersionsView`, `VersionSwitchDialog`, `LooseFileBanner`, `MiniPlayerWindow` |
| Ajustes (7) | `AppearanceSettingsView`, `PrivacySettingsView`, `ScanSettingsView`, `LifecycleSettingsView`, `RecommendationSettingsView`, `SegmentDetectionSettingsView`, `DiagnosticsPreviewView` |
| Revisión (3) | `ReviewInboxView`, `CandidateCardView`, `DuplicateReviewView` |
| Metadatos (2) | `MetadataEditorView`, `RenamePreviewView` |
| Catálogo (2) | `PersonalActionsView`, `WatchStatusControl` |
| Copia de seguridad (2) | `BackupView`, `RestoreWizardView` |
| Primeros pasos (1) | `RootOnboardingView` |
| Recuperación (1) | `DatabaseRecoveryView` |
| Créditos (1) | `CreditsView` |
| Actualización (1) | `UpdateView` |

Todas viven en `src/ApSolutions.LocalMedia.Presentation/<área>/`.

## La actualización, que es más de lo que parece

`UpdateView` es **una vista con veintitrés mensajes distintos**, y por eso se nombra aparte: un
rediseño que sólo contemple «buscando» y «listo» deja fuera la mayoría.

- **Estados del proceso (15)**: en reposo, comprobando, al día, hay versión, sin conexión, versión
  inservible, descargando, lista, interrumpida, verificación fallida, cancelada, sin confirmar,
  manipulada, entregada a Windows, y lanzamiento rechazado.
- **Motivos de rechazo (8)**, que son los que explican por qué una actualización **no** se aplica:
  descarga insegura, hash inservible, sumas sin firmar, tiempo de ejecución equivocado, tamaño no
  declarado, resumen incompleto y anfitrión no declarado.
- **Controles**: comprobar, descargar, instalar, cancelar, la casilla de comprobación automática y el
  aviso de confirmación.
- Hay **barra de progreso** y la zona de estado se anuncia sola a los lectores de pantalla
  (`LiveSetting="Polite"`): lo que el rediseño ponga ahí tiene que seguir siendo una región viva.

Un rechazo **no es un error del usuario**: es la actualización negándose a instalar algo que no pudo
verificar. Merece un tratamiento visual distinto del de un fallo.

## La instalación, que también se ve

Lo que Windows enseña al instalar y en el menú Inicio sale de
`src/ApSolutions.LocalMedia.Windows.Package/`:

| Activo | Uso | Estado |
| --- | --- | --- |
| `Assets/Square44x44Logo.png` | Barra de tareas, lista de aplicaciones | 576 B — provisional |
| `Assets/Square150x150Logo.png` | Mosaico del menú Inicio | 1,7 KiB — provisional |
| `Assets/Wide310x150Logo.png` | Mosaico ancho | 3,0 KiB — provisional |
| `Assets/StoreLogo.png` | Ficha de la tienda | 628 B — provisional |
| `Assets/SplashScreen.png` | Pantalla de arranque | 7,0 KiB — provisional |

Los cinco son del 3 de agosto y su tamaño delata que son marcadores de posición, no marca. **Es la
primera cosa que alguien ve del producto**, antes que ninguna vista.

**Y hay un defecto medido en los textos.** El manifiesto declara los dos idiomas
—`<Resource Language="es-ES"/>` y `<Resource Language="en-US"/>`— pero su descripción es **una sola
cadena con una barra dentro**:

```xml
Description="Biblioteca y reproductor de vídeo local / Local video library and player"
```

Windows la enseña tal cual en los dos idiomas, barra incluida. La localización de verdad se hace con
`ms-resource:` y un recurso por idioma, como ya se hace en winget, que **sí** tiene sus dos archivos
`locale.es-ES.yaml` y `locale.en-US.yaml` con descripciones propias y bien escritas. El color de
fondo del mosaico y del arranque (`#111827`) también se decide aquí.

## Lo que este documento no cubre

- **El manual de usuario** (`DOC-101`, `DOC-201`, `T44.1`-`T44.6`) se escribe desde la aplicación
  construida, así que sus capturas dependen del rediseño y van después.
- **Los temas y fichas de color** viven en `src/ApSolutions.LocalMedia.Presentation/Theme/` y en
  `Resources/Brand.axaml`; este inventario nombra las superficies, no la paleta.
