# Todo lo que se ve

Inventario de las superficies visibles de AP Reelume, para que un rediseño pueda cubrirlas **todas**
y no sólo las que se recuerdan. La versión inglesa está en [SURFACES.en.md](SURFACES.en.md).

Este documento no decide estética. Dice **qué existe, dónde vive y en qué estados aparece**, medido
del árbol el 2026-08-15, vuelto a medir el 2026-08-18, el 2026-08-20 y otra vez el **2026-08-23**,
para que nada visible se quede sin diseñar.

## La regla que ya se cumple, y no hay que rehacer

- **Las 53 vistas usan cadenas localizadas.** Ninguna tiene texto sin traducir: la medición no
  encontró ni una sola vista sin `DynamicResource`.
- **576 claves de cadena en español y 576 en inglés**, en
  `src/ApSolutions.LocalMedia.Presentation/Resources/Strings.es.axaml` y `Strings.en.axaml`.
  `BilingualHeadingTests` compara la estructura de los documentos públicos, y una cadena visible
  nueva va en los dos archivos o no va.
- **Los textos literales del árbol son símbolos y cifras, no idioma**: `○ ◐ ●` (estado de visto),
  `→` (origen y destino de un renombrado), `!` (aviso), `⚠` (medio fuera de alcance), `✕` (una
  sesión que no abre), `✓` (la bandeja de revisión vacía, que es la buena noticia) y los diez pasos
  del menú de velocidad (`0,25×` … `4×`, cifra y multiplicador). **`⚠` y `✕` son distintos a
  propósito**: un fallo y un aviso que compartieran glifo se distinguirían sólo por el color. Si el
  rediseño los sustituye por iconos, el nombre accesible sigue viniendo de `AutomationProperties`,
  que ya está puesto.
- **Cada control interactivo tiene nombre accesible**, y hay 80 pruebas de accesibilidad que lo
  exigen. Un rediseño puede cambiar la forma, no quitarle el nombre.

## Las 53 vistas, por área

| Área | Vistas |
| --- | --- |
| Shell (2) | `ShellView`, `StartupView` |
| Inicio (5) | `HomeView`, `ResumeHeroView`, `InProgressRailView`, `RecentlyAddedRailView`, `RecommendationsRailView` — `LibraryEntryView` se retiró el 2026-08-23: su enlace vive en la cabecera del carril |
| Biblioteca (3) | `LibraryView`, `UnavailableBadge`, `PosterCardView` |
| Ficha de película (1) | `MovieDetailsView` |
| Ficha de serie (2) | `ShowDetailsView`, `EpisodeRowView` |
| Reproductor (17) | `PlayerView`, `TransportControlsView`, `VideoStatusOverlay`, `ResumePromptView`, `NextEpisodeOverlay`, `SkipMarkerButton`, `MarkerEditorView`, `DetectedMarkerReviewView`, `TrackSelectorView`, `AudioOutputView`, `SubtitleStyleView`, `ShortcutSettingsView`, `PlayerVersionsView`, `VersionSwitchDialog`, `LooseFileBanner`, `MiniPlayerWindow`, `MiniPlayerChromeView` |
| Ajustes (8) | `AppearanceSettingsView`, `PrivacySettingsView`, `ScanSettingsView`, `LifecycleSettingsView`, `RecommendationSettingsView`, `SegmentDetectionSettingsView`, `DiagnosticsPreviewView`, `RootManagementView` |
| Revisión (4) | `ReviewInboxView`, `CandidateCardView`, `DuplicateReviewView`, `DuplicatesOverviewView` |
| Metadatos (2) | `MetadataEditorView`, `RenamePreviewView` |
| Catálogo (2) | `PersonalActionsView`, `WatchStatusControl` |
| Copia de seguridad (2) | `BackupView`, `RestoreWizardView` |
| Primeros pasos (2) | `RootOnboardingView`, `AddRootDialogView` |
| Recuperación (1) | `DatabaseRecoveryView` |
| Créditos (1) | `CreditsView` |
| Actualización (1) | `UpdateView` |

Todas viven en `src/ApSolutions.LocalMedia.Presentation/<área>/`.

## Las superficies que no son vistas

Ocho cosas que el usuario ve y que **no tienen `.axaml`**: su forma la dibuja Windows y no se puede
rediseñar. Lo que sí es nuestro es **el texto y el activo**, y eso es exactamente lo que se ve.

| Superficie | Lo que decidimos nosotros |
| --- | --- |
| Icono y menú de bandeja | El tooltip, las dos entradas del menú y cuál es la de por defecto al doble clic. El icono es el sexto activo (ver abajo) |
| Diálogo «elegir carpeta de medios» | El título y la carpeta inicial |
| Diálogo «guardar la copia» | El nombre propuesto, con fecha, y el filtro de tipo |
| Diálogo «abrir la copia» | El filtro y la carpeta inicial |
| La entrega sin diálogo | Cuando la ejecución no es dueña del perfil, la carpeta se decide sola y el usuario no ve nada: hace falta una confirmación posterior que diga dónde acabó el archivo |
| Explorador de Windows | Las ocho extensiones en «Abrir con», el nombre visible y el icono de asociación |
| Arranque de MSIX | La pantalla de bienvenida y su color de fondo |
| Barra de título y cromo de la ventana | El título, el tamaño recordado y su mínimo; el cromo es del sistema a propósito, porque es lo que garantiza minimizar, ajustar y cerrar con los gestos que el usuario ya tiene |

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
  (`LiveSetting="Polite"`): lo que el rediseño ponga ahí tiene que seguir siendo una región viva, y
  en **un solo** contenedor: partirla en dos parte el anuncio.

Un rechazo **no es un error del usuario**: es la actualización negándose a instalar algo que no pudo
verificar. Desde el 2026-08-23 **lo tiene**: el borde vivo se viste por gramática —proceso neutro,
al día en positivo, rechazo en Warning con el motivo como titular y el identificador técnico tras un
plegado, fallo del mundo en Danger— y suma tres cadenas (`UpdateRejectionReasonLabel`,
`UpdateNothingInstalledNotice`, `UpdateRejectionDetailAction`), lo que deja la vista en veintiséis
mensajes.

## El reproductor, que tampoco tiene un solo estado

Medido el 2026-08-18, y la distinción importa porque son **dos gramáticas, no una**:

- **`PlayerView` muestra seis motivos de fallo**, cada uno con su propia cadena y su propio
  `TextBlock`: archivo no encontrado, no se pudo abrir, motor no disponible, códec no soportado,
  archivo dañado y sin pista reproducible. Son fallos: **no hay imagen**.
- `PlaybackFailureCode` tiene **siete** valores, y el séptimo —`UnsupportedCapability`— **no es uno
  de esos seis**: viaja en `VideoOutputDecision` y sale por `VideoStatusOverlay`. El vídeo **se
  reproduce**, con conversión de tono; lo que se avisa es que el formato queda fuera de alcance. Un
  rediseño que lo pintase como fallo diría que no hay imagen cuando sí la hay.
- **`VideoStatusOverlay` tiene seis avisos**: aceleración por hardware, caída a software, HDR10
  directo, conversión de tono, rango estándar y formato no soportado. No son errores: son el estado
  de la imagen.

## Las listas y su estado vacío

Vuelto a medir el 2026-08-23, después del rediseño: **catorce vistas pintan una cadena de vacío**,
y las que llegaron con las fases nuevas dicen su vacío **en positivo cuando el vacío es la buena
noticia**:

| Lista con cadena de vacío | Vista que la pinta |
| --- | --- |
| Biblioteca | `LibraryView` (`LibraryEmptyTitle/Description`), que también distingue **buscar y no encontrar** (`LibrarySearchEmpty*`) — el hueco que este documento señaló el 2026-08-20 |
| En curso, Recomendaciones, Añadido recientemente | sus tres carriles, como estaban |
| Episodios de una serie | `ShowDetailsView` |
| Marcadores, Detectados, Pistas, Versiones | las cuatro listas de la columna del reproductor (`MarkersEmpty*`, `DetectedMarkersEmpty*`, `TracksEmpty*`, `PlayerVersionsEmpty*`) |
| Bandeja de revisión | `ReviewInboxView`, en superficie positiva con `✓` |
| Grupos de duplicados | `DuplicatesOverviewView` (`DuplicatesEmpty*`, «nada se borró para llegar aquí») |
| Historial de copias | `BackupView` (`BackupHistoryEmpty*`) |
| Raíces por reasignar | `RestoreWizardView` (`RestoreRootsEmpty*`, «todas las carpetas existen») |
| Conexiones declaradas | `PrivacySettingsView` (`PrivacyNoHosts*`, «nada sale de este equipo») |
| Atajos | `ShortcutSettingsView` (`ShortcutsEmpty*`) |

La carga tiene su primer estado dibujado: la primera visita de la Biblioteca viste **esqueleto**
(`apr-shim`) mientras corre su consulta. El resto de listas pintan su respuesta en el cuadro en que
llega, y un esqueleto de un cuadro es un parpadeo, no información.

## Ausente no es lo mismo que deshabilitado

Doce superficies cambian de forma según el estado, y el rediseño tiene que poder pintarlas en todas
ellas. La distinción es la que ya modela `PrivacySettingsView`: **ausente** significa que el control
no existe y no deja hueco; **deshabilitado** significa que existe y no se puede usar.

| Superficie | Qué cambia | Gramática |
| --- | --- | --- |
| `PrivacySettingsView` | El interruptor de refresco automático **no existe** sin conexión consentida (LIB-016): ofrecerlo sería ofrecer algo que no puede ocurrir | ausente |
| `PrivacySettingsView` | La vista previa del diagnóstico, según haya algo que enseñar | ausente |
| `PrivacySettingsView` | Exportar diagnósticos existe siempre, se pueda o no | deshabilitado |
| `UpdateView` | Los cuatro controles, según el estado del proceso | deshabilitado |
| `UpdateView` | El aviso de confirmación, que sólo existe con una versión descargada | ausente |
| `ShellView` | Los ocho bloques de Ajustes | ausente |
| `PlayerView` | Los cinco paneles opcionales de la columna lateral | ausente |
| `RootOnboardingView` | Sus cuatro formas: sin raíces, con raíces, confirmando borrado y pidiendo consentimiento | ausente |
| `LooseFileBanner` | Sólo con una sesión suelta | ausente |
| `MetadataEditorView` | Sus tres mensajes | ausente |
| `RecommendationsRailView` | Vacía no es lo mismo que apagada por ajuste | ausente |
| `RestoreWizardView` | Sólo la raíz que falta gana campo editable | ausente |

**Las dos gramáticas conviven en la misma pantalla** en privacidad y en actualización, y por eso hay
que distinguirlas a la vista.

## Las animaciones, y las dos que no existen

El paquete pide cuatro. Dos están, y las otras dos **no están aplazadas: están contestadas**, con lo
que se midió.

| Animación | Dónde | Estado |
| --- | --- | --- |
| `apr-tip` | Tooltip de los seis destinos del carril | **Hecha** |
| `apr-pulse` | El punto junto a «Escaneando» en `LibraryView` | **Hecha** |
| `apr-shim` | Esqueleto de la primera consulta de la Biblioteca | **Hecha** el 2026-08-23: `LibraryViewModel.IsLoading` existe y seis tarjetas-fantasma respiran al paso del único token; el barrido de 1,2 s del prototipo se rehusó a propósito —un segundo token de movimiento sería la copia paralela volviendo— |
| `apr-in` | Subida de 6 px en cada cambio de pantalla | **No se hace**: el shell no cambia de pantalla, monta las once y alterna `IsVisible`, que Avalonia no anima |

**Todas leen `MotionDuration`, y `FluentThemeService` escribe ese recurso.** Con movimiento reducido
escribe `TimeSpan.Zero`, que es lo que hace que la preferencia llegue a una animación — una animación
no puede preguntarle nada a un servicio, lee un recurso.

## Los temas, medidos

| Medida | Valor |
| --- | --- |
| Diccionarios en `Theme/DesignTokens.axaml` | **4**: `Light`, `Dark`, `HighContrastLight` y `HighContrastDark` |
| Declaraciones de token en los diccionarios | **344**, en **86 nombres**: 24 brochas y 62 alias (12 del botón, 31 de la casilla, 3 de las listas, 16 de los campos de texto) |
| Escalares, fuera de los diccionarios | **13** |
| Además, en `Resources/Brand.axaml` | 3 (cadenas, ningún color) |
| Selectores de foco | **10**: `Button`, `ToggleButton`, `ToggleSwitch`, `RadioButton`, `TextBox`, `ComboBox`, `CheckBox`, `Slider`, `NumericUpDown`, `ListBoxItem` |
| Tipos con punteado de deshabilitado | **los mismos 10**, por adorno |

Cuatro cosas que el rediseño tiene que saber:

- **El alto contraste son dos, y el sistema los elige.** Había un solo diccionario declarado sobre
  `ThemeVariant.Light` y **ningún camino lo seleccionaba**. Hoy `IHighContrastService` pregunta a
  Windows y `FluentThemeService` pasa a `HighContrastLight` o `HighContrastDark` según la luminancia
  de `COLOR_WINDOW`, nunca según el nombre del tema, que es localizable.
- **El alto contraste no se elige en la aplicación**: `ThemePreference` tiene tres valores y sigue
  teniéndolos. Es una necesidad declarada al sistema, no una preferencia de esta aplicación, y
  ofrecer una copia crearía dos fuentes de verdad para la misma necesidad.
- **El reproductor ignora el tema elegido.** `PlayerThemeVariant` devuelve `ThemeVariant.Dark`
  siempre, a propósito, porque una sala a oscuras no quiere una interfaz blanca. Es la única
  superficie que no obedece la preferencia.
- **En alto contraste, deshabilitado se dice con geometría y no con color.** Las dos paletas no
  tienen un tercer color que gastar —relleno deshabilitado, relleno de reposo y superficie son el
  mismo, el borde es uno para los cuatro estados y el texto deshabilitado es el primario—, así que
  la diferencia es el **borde punteado**, dibujado como adorno sobre los diez tipos.
- **Cada tipo de control entra por sus propios recursos, y no hay dos iguales.** Un botón consume
  **12** recursos del tema base; una casilla, **73**; una lista desplegable, 59; un `RadioButton`,
  38; un `ToggleButton`, 37; un `Slider`, 32. Un `TextBox` tiene **2** propios y un `ListBoxItem`
  **1**: ésos pintan desde brochas **compartidas** del sistema. Suponer que el siguiente se hace como
  el anterior es la forma de equivocarse aquí.
- **Una familia de recursos puede valer por varios tipos, y también se mide.** `TextControl*` la
  toman el `TextBox` (25 sitios) y el `NumericUpDown` (35, porque es una caja con dos flechas), y
  **ninguno** del botón, la casilla o la barra deslizante. El `ComboBox` sólo la toca por la caja que
  le crece **cuando es editable**, y el árbol no tiene ninguno.
- **Una brocha compartida se redirige midiendo quién más la toma.** Las tres de las listas
  (`SystemControlHighlightList*`) se comprobaron pintándolas de un color que ningún tema usa y
  montando doce tipos de control: **sólo la lista las consume**. Y lo que decidió el diseño de la
  fila: su `ContentPresenter` **sí** toma el borde de la fila por `TemplateBinding`, pero su texto
  sale de una brocha genérica, así que el color del texto de una fila seleccionada **no se puede dar**
  — de ahí que el relleno sea un tinte y la señal, el borde.

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
| `Presentation/Assets/tray-icon.png` | Icono de la bandeja | **El sexto, y vive en otro proyecto** |

Los cinco primeros son del 3 de agosto y su tamaño delata que son marcadores de posición, no marca.
**Es la primera cosa que alguien ve del producto**, antes que ninguna vista.

**Y cinco archivos no son cinco activos.** MSIX escala cada uno, así que lo que hay que producir son
**35**: el cuadrado de 44 px sale en cinco escalas más cinco tamaños objetivo y su variante sin
placa; los otros cuatro, en cinco escalas cada uno; y el de bandeja en cinco tamaños **reales**,
16/20/24/32/48, que no son escalas de nada. Un icono de bandeja con fondo pintado se ve como un
cuadrado en cuanto alguien cambia el color de la barra, así que ése necesita alfa de verdad.

El color de fondo del mosaico y del arranque (`#111827`) también se decide aquí.

## Lo que este documento no cubre

- **El manual de usuario** (`DOC-101`, `DOC-201`, `T44.1`-`T44.6`) se escribe desde la aplicación
  construida, así que sus capturas dependen del rediseño y van después.
- **La paleta.** Los valores de color viven en `Theme/DesignTokens.axaml` y en `Resources/Brand.axaml`;
  este inventario cuenta cuántos son y qué temas existen, no decide sus valores.
