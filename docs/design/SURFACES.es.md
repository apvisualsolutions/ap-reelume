# Todo lo que se ve

Inventario de las superficies visibles de AP Reelume, para que un rediseño pueda cubrirlas **todas**
y no sólo las que se recuerdan. La versión inglesa está en [SURFACES.en.md](SURFACES.en.md).

Este documento no decide estética. Dice **qué existe, dónde vive y en qué estados aparece**, medido
del árbol el 2026-08-15 y vuelto a medir el **2026-08-18**, para que nada visible se quede sin
diseñar.

## La regla que ya se cumple, y no hay que rehacer

- **Las 48 vistas usan cadenas localizadas.** Ninguna tiene texto sin traducir: la medición no
  encontró ni una sola vista sin `DynamicResource`.
- **470 claves de cadena en español y 470 en inglés**, en
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
verificar. Merece un tratamiento visual distinto del de un fallo.

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

Medido el 2026-08-18: **23 listas con datos** en el árbol —entendiendo por lista un `ListBox`,
`ItemsControl` o `ItemsRepeater` con `ItemsSource`—, y **sólo cuatro tienen escrita una cadena para
cuando están vacías**:

| Lista con cadena de vacío | Vista que la pinta |
| --- | --- |
| Biblioteca | `ShellView` (`EmptyLibraryTitle`, `EmptyLibraryDescription`) |
| En curso | `InProgressRailView` (`HomeInProgressEmpty`) |
| Recomendaciones | `RecommendationsRailView` (`RecommendationsEmpty`) |
| Episodios de una serie | `ShowDetailsView` (`ShowDetailsEmpty`) |

Las **diecinueve restantes no dicen nada cuando están vacías**, y ninguna dice nada mientras carga ni
cuando falla. Un rediseño tiene que decidir esos tres estados por lista, no sólo el lleno.

**Y hay uno que nadie ve venir**: el vacío de la biblioteca lo pinta `ShellView`, no `LibraryView`,
así que **buscar y no encontrar nada no muestra ningún texto** — ni el de biblioteca vacía, que
además diría algo falso: la biblioteca no está vacía, es la búsqueda la que no encuentra.

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

## Los temas, medidos

| Medida | Valor |
| --- | --- |
| Diccionarios en `Theme/DesignTokens.axaml` | **4**: `Light`, `Dark`, `HighContrastLight` y `HighContrastDark` |
| Declaraciones de token en los diccionarios | **280**, en **70 nombres**: 24 brochas y 46 alias (12 del botón, 31 de la casilla, 3 de las listas) |
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
