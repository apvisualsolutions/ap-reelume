# Cambios

Todo cambio relevante de AP Reelume. La versión inglesa está en [CHANGELOG.en.md](CHANGELOG.en.md).

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el versionado es
[SemVer](https://semver.org/lang/es/). El registro canónico del alcance, con su estado y su
evidencia, es [FEATURES.md](FEATURES.md).

## [Sin publicar] / [Unreleased]

### Corregido

- **El texto de todos los botones estaba pegado arriba, no centrado.** Lo vio el propietario antes
  que ninguna puerta. `VerticalContentAlignment` empieza en `Stretch`, el estilo de este árbol fijaba
  alto, radio y relleno y nunca lo tocaba, y un `TextBlock` estirado llena el botón entero y dibuja
  su línea **arriba del todo**: medido, una píldora de 36 px con una caja de etiqueta de 34 y las
  palabras siete píxeles por encima del centro. El prototipo escribe la regla en una línea de CSS
  —`display:inline-flex; align-items:center; justify-content:center`— y eso es lo que hay ahora, con
  `ButtonInkTests` midiendo los dos huecos y que la caja sea del tamaño de una línea y no del botón.

- **Faltaba la trama diagonal de las portadas.** Las carátulas del prototipo son **cuatro** fondos
  sobre un tono y esta aplicación pintaba dos: el degradado y el halo. El que faltaba es
  `repeating-linear-gradient(115deg, rgba(255,255,255,.055) 0 2px, transparent 2px 10px)`, y por eso
  un muro de portadas se veía liso donde el del prototipo se ve tejido. Avalonia no tiene degradado
  repetido y no le hace falta: `SpreadMethod="Repeat"` sobre un vector de diez píxeles en el ángulo
  del prototipo dibuja exactamente esas dos rayas de cada diez. Puerta: ninguna superficie pinta el
  halo sin pintar la trama.

- **Los iconos eran de otro alfabeto.** El prototipo dibuja **treinta y cinco** pictogramas con SVG
  de 24×24, trazo de 1,6 y extremos redondos; la aplicación pintaba glifos de Segoe Fluent —sólidos,
  de otra tradición— en veintisiete sitios, y ahí estaba la diferencia que el propietario nombró
  primero. Ahora las formas vienen del prototipo, convertidas a geometrías que viven en el
  repositorio: el riel, el reproductor entero, el mini reproductor, la lupa, el calderón de las
  píldoras y el aspa del diálogo. **Se desvía de una línea del paquete de diseño** —la Propuesta y su
  README prescriben «glifos de Segoe Fluent Icons»— y la regla que esa línea protege, la de no
  descargar nada, queda intacta. De paso, el botón de silencio **dice ahora en qué estado está**:
  dibujaba el altavoz tachado tanto callado como sonando.

### Añadido

- **La página del repositorio, con capturas de la aplicación de verdad.** Los dos README abren con
  Inicio y llevan cuatro capturas más —Biblioteca, la ficha de una serie, el reproductor con su
  columna, y la bandeja de revisión con sus tres candidatos—, en inglés, tema oscuro y 1600 × 1000,
  versionadas en `docs/assets/`. Las toma un guion contra la aplicación compilada, sobre una raíz de
  datos aislada con una biblioteca **ficticia**: una captura de la biblioteca real lleva dentro
  títulos y rutas de alguien, en un PNG que ninguna prueba puede leer, y eso se dice en la propia
  página. La página añade además plataforma, licencia, enlace de descarga y qué significa que un
  commit llegue a `main`. **No lleva insignia de CI, y por una razón medida**: el flujo no corre en
  `main` a propósito —recibe el mismo SHA por avance rápido—, así que una insignia apuntando ahí se
  congela en lo último que vio y sigue diciéndolo, que es una prueba ciega con otro nombre.

### Corregido

- **El reproductor no tenía ni barra ni reloj mientras reproducía.** Encontrado al tomar la cuarta
  captura con una película real: `TransportControlsViewModel` sólo cambiaba de estado con sus
  propias órdenes, así que `HasDuration` seguía en falso toda la sesión y la fila del scrubber y los
  dos relojes no llegaban a existir; bastaba pulsar un salto para que aparecieran. El cabezal ya
  llegaba a `CompositionRoot` —el manejador que lo reparte alimenta al rastreador y a la oferta de
  salto, ambos con su comentario de haber estado «alcanzables y sin alimentar»—; el transporte era
  el tercero, y el único que una persona mira. Decimoquinta forma del defecto de la casa. Con ella,
  los dos paseos físicos que pulsan saltos pasan a medirlos con la sesión **en pausa**: con el
  cabezal vivo, el clic de al lado mueve la posición tanto como el salto.

- **El selector de temporada de una serie escribía un nombre de clase en pantalla.** La píldora
  `filter-pill` enlaza `SelectionBoxItem` en su `ContentControl` y no enlazaba `ItemTemplate`, así
  que la ficha de cualquier serie mostraba `ApSolutions.LocalMedia.Presentation.Show.SeasonViewModel`
  donde debía leerse «Temporada 1». Estaba en la matriz de paridad del día anterior y nadie lo vio;
  lo cazó la captura del README. Los dos desplegables de la biblioteca nunca lo enseñaron porque sus
  filas son `ComboBoxItem` con texto dentro.

### Añadido

- **La paleta es la del prototipo, medida de su propio código.** El propietario miró las capturas
  y dijo la verdad: ni los colores ni la elegancia eran los del prototipo. La causa era doble: los
  valores del árbol venían de una instantánea anterior (#111827 azulado donde el prototipo pinta
  #050608/#08090C casi negro) y faltaba la capa de acabado. Hoy los dos diccionarios ordinarios se
  re-valoran al canón leído del propio `tokens()` del prototipo —fondo, tarjetas #12151B, rellenos,
  bordes sutiles #5C6878 (el único punto donde el canón cede ante la puerta: su #3A424F da 1,96:1 y
  el límite de 3:1 es de la casa), textos #EDF1F6/#8B97A8, estados tradu¬idos a mezclas opacas—;
  el **primario es por fin la píldora clara del prototipo** (#F3F6FA con tinta casi negra en
  oscuro), con familia de tokens propia, sus cuatro pares medidos en la puerta y la prueba de
  estados re-declarada: bajo la mano SIGUE siendo el primario —un líder que se viste de gris justo
  cuando van a pulsarlo dejaba de liderar—; y las **dos elevaciones del prototipo** existen como
  tokens tipados y se gastan donde él las gasta: filas de ajustes, fichas de la biblioteca y el
  diálogo flotante. Alto contraste intacto: sin sombras y con su par ya declarado. Verificado en
  pantalla con la biblioteca sembrada junto a la captura del prototipo.

- **La primera visita a la Biblioteca viste esqueleto, y la privacidad dice sus dos mejores
  respuestas.** Seis tarjetas-fantasma respiran al paso del único token de movimiento mientras
  corre la primera consulta —y sólo la primera: un esqueleto sobre tarjetas que alguien ya lee es
  peor que la espera que decora—. El barrido del prototipo dura 1,2 s; aquí el brillo es el mismo
  aliento del punto de escaneo porque este repositorio tiene UN token de movimiento y un segundo
  más lento sería el defecto de la copia paralela volviendo vestido de barrido —la desviación
  está comentada en el estilo—. Privacidad pinta en positivo la lista de conexiones vacía
  («Ninguna conexión declarada · Nada sale de este equipo») y explica bajo la previsualización
  por qué los campos vacíos se listan: verlos vacíos es prueba; omitirlos, una promesa. Los 16
  comentarios de diseño del paquete están en sus 9 archivos —los seis que faltaban se escribieron
  hoy—. Dos límites de la fase, decididos y anotados: Inicio no lleva esqueleto porque sus
  carriles pintan su respuesta en el mismo cuadro en que llega, y la manija de interruptor queda
  elevada al propietario —el árbol conmuta con CheckBox por decisión medida (18 usos, 73 recursos
  de tema, suite propia) y migrar a ToggleSwitch la revoca—.

- **Actualización, Copias y Restauración hablan las cuatro gramáticas de la §4.** El estado del
  actualizador vive en UN solo borde vivo —un lector de pantalla suscrito a una región no debe ver
  esa región sustituida— que ahora se viste según la noticia: proceso neutro, al día en positivo,
  rechazo de un guardián en Warning con **el motivo como titular por encima del estado** y el
  identificador técnico plegado tras «Ver el detalle técnico» —un control nuevo, con su escena:
  el paseo sirve una versión para una arquitectura que no existe, ve llegar el rechazo con su
  motivo y despliega el detalle con el ratón—, y fallo del mundo en Danger. Copias gana el bloque
  de la base activa —ruta alcanzable sin necesitar un fallo—, el historial vacío dice lo que
  cuesta la primera copia, y el fallo añade que nada quedó a medias. La restauración numera sus
  tres pasos en el orden en que una persona los camina —Confirmar bajó a su paso: ofrecerlo antes
  de elegir nada era el orden de lectura mintiendo sobre el orden de la tarea—, dice en positivo
  cuando no hay nada que reasignar, marca bajo cada carpeta sin resolver su consecuencia exacta, y
  las etiquetas de estado de las filas son las aprobadas del paquete.

- **Las dos herramientas de un título comparten un panel con pestañas de verdad.** Editar
  metadatos y previsualizar el renombrado vivían apilados —abrir los dos era leerlos uno debajo
  del otro—; ahora un solo panel los sostiene con pestañas Metadatos | Renombrado, cada puerta
  selecciona su pestaña al abrirse, y una pestaña cuya superficie no está abierta esconde su
  cabecera en vez de ofrecer una página en blanco —el patrón del panel lateral del reproductor—.
  La travesía ensamblada afirma la semántica nueva: una superficie materializada a la vez, y la
  de atrás vuelve cuando su pestaña se elige. Del resto de la fase, la medición mandó: la bandeja
  vacía positiva, la comparación en dos columnas con las cifras en monoespaciado y los tres
  mensajes con glifo del editor ya estaban en el árbol con sus puertas en verde.

- **El reproductor cierra su fase: la velocidad es el menú del prototipo y la tercera salida de un
  fallo es por fin un botón.** El indicador de velocidad —un texto que el ratón sólo podía mirar
  mientras el teclado sí cambiaba el paso— es ahora un botón con los nueve pasos de la política
  como menú y «Volver a 1×» sólo mientras hay de dónde volver; una prueba compara los pasos del
  marcado con `PlaybackControlPolicy.SpeedSteps` para que ratón y teclado no puedan discrepar. En
  la superficie de fallo, «elegir otra versión» dejó de ser una frase informativa: el botón
  despliega las mismas filas que lista la columna lateral —el mismo objeto, entregado por la
  composición—, de modo que elegir ahí ES el cambio de versión y no hay una segunda gramática
  que aprender. Las tres superposiciones que declaraban una sola dimensión (`ResumePrompt`,
  `NextEpisode`, `VersionSwitchDialog`) declaran ahora las dos, y los tres avisos de consecuencia
  de marcadores aprobados el 2026-08-23 están donde ocurre cada consecuencia. Dos escenas del
  paseo abren los dos menús con el ratón —un desplegable se mide por abrirse: lo elegido dentro
  cae en una ventana propia—.
  - De camino, tres miembros del transporte que nadie consumía (`SpeedSteps`, `Duration`,
    `ConfigureSkipsAsync`) salen del modelo: la forma catorce del defecto de la casa.

- **Duplicados entra al riel y Copias se muda a Ajustes**, que es el mapa del prototipo y la
  decisión que el propietario tomó el 2026-08-23. La ruta nueva lista **todos los grupos** —una
  consulta que no existía: lector en Infrastructure, caso de uso en Application, y la fila abre la
  MISMA comparación que la acción de la ficha, por la misma puerta del shell, para que las dos
  entradas no puedan discrepar—. El vacío es el estado deseable y lo dice en positivo con las
  cadenas aprobadas. Copias y restauración viven ahora bajo su entrada del índice con el esqueleto
  de sección de sus pares, y las escenas del paseo llegan por donde llega una persona.
  - La puerta de la casa cazó el registro sin resolución explícita del lector nuevo en el mismo
    commit que lo introducía, que es exactamente para lo que existe.

- **Los dos altos contrastes son elegibles: Apariencia pasa de tres a cinco píldoras**, con las
  claves `ThemeHighContrastLight` y `ThemeHighContrastDark` que el paquete traía y la decisión que
  el propietario revocó el 2026-08-23 por la vía que ella misma dejó abierta. El ajuste del sistema
  sigue mandando sobre la píldora elegida, el `WrapPanel` decide dónde pliegan las cinco en cada
  idioma, y el paseo las aplica de verdad — la aplicación entera viste cada alto contraste un
  instante y Sistema repone al final. Tres puertas que contaban tres se declararon a cinco.

- **Ajustes es la página del prototipo: encabezado, índice lateral fijo y una sección a la vez.**
  La construcción es la de la §7 al pie de la letra —sin sticky, un Grid cuyo ScrollViewer vive
  sólo en la columna derecha— y los estilos `side-list` que el tema declaraba sin consumidor por
  fin lo tienen: el defecto de la casa, al revés. Una sección que no está abierta NO está en el
  árbol visual, así que cada escena del paseo que pulsa dentro de una la abre primero desde el
  índice — la misma pulsación que hace una persona, y la que prueba el índice: sus diez entradas
  quedan todas pulsadas por las escenas que las necesitan.
  - El contrato de estructura se reescribió al del rediseño: el H1 vive sobre el índice, y lo que
    se sostiene es que la sección abierta empieza donde empiezan sus pares — el arnés sin modelo
    sigue viendo las diez a la vez, que es lo que permite compararlas.
  - «Biblioteca y escaneo» agrupa por fin carpetas y escaneo bajo una entrada, con la cadena nueva
    del índice en los dos idiomas.

- **El tinte de acento en lo alto del contenido**: el halo radial de 260 px del prototipo, sin
  ninguna brocha nueva — es `AccentBrush` a la intensidad del prototipo bajo una máscara de opacidad
  radial, dentro del mismo token que apaga el arte en los dos altos contrastes, donde un brillo
  decorativo es justo el color que el tema existe para quitar. Las superficies opacas del riel y de
  la banda de título lo recortan solas.

- **Las fichas de película y de serie llevan el banner del prototipo**: la portada elevada sobre el
  muro de color del propio título con el velo direccional, el título a tamaño display, la sinopsis y
  las acciones sobre el fondo oscuro — el marco del héroe, moneda por moneda, con el mismo pago en
  alto contraste que la puerta de color-solo afirma ahora para las cuatro superficies que lo gastan.
  Las dos columnas de la §4 sobreviven dentro: portada fija, texto fluido.
  - El selector de temporada viste la píldora de desplegable que la Biblioteca enseñó, con su
    etiqueta dentro.
  - **Las tres consecuencias del tráiler entran aprobadas**: qué cuesta el local (nada), qué hace el
    enlace (sale por tu navegador, no por este proceso) y cómo hacer que exista uno cuando no hay —
    la convención es de `TrailerDiscoveryPolicy`, y la serie explica en comentario por qué no tiene
    tráiler local que ofrecer.

- **El héroe de Inicio sangra, que es lo que el prototipo dibuja**: el muro de color del propio
  título con el velo direccional encima, sobre `PlayerSurfaceBrush` — la única superficie que es
  `#0B0D10` en los cuatro temas, con los pinceles de texto del reproductor y su contraste ya medido
  (19,46:1). En los dos altos contrastes la capa de arte se apaga por `PosterArtOpacity` y el texto
  queda sobre el fondo liso: cero brochas nuevas. La razón por la que no sangraba —«no hay arte»—
  caducó el día que el arte generado entró; «Detalles» sigue fuera y sigue bloqueado en datos, no en
  esfuerzo.
  - La lista de la puerta «ningún estado se dice sólo con color» gana la fila del héroe con la misma
    moneda que la ficha: el color repite un título escrito al lado, y el pago —apagarse en alto
    contraste— queda afirmado por la misma prueba.
- **«Abrir biblioteca» pasa a la cabecera del carril «En curso»** y la tarjeta de acceso se retira
  del árbol: mismo comando, mismas palabras, mismo nombre de control, en el único carril que la
  puerta puede sostener dentro del primer viewport a 1366×768. La línea base estructural de Inicio
  se regrabó con el único campo que cambia (el acceso sube de 430 a 404 px) verificado en las 36
  combinaciones.

- **«Añadir raíz de medios» es ahora el panel flotante del prototipo**, abierto desde la acción
  primaria de la cabecera de Biblioteca —clave nueva «Añadir medios…», con la elipsis que promete un
  diálogo— y desde el «+» del riel, que ya no navega: el panel flota sobre la ruta que esté, con velo
  detrás y las dos dimensiones acotadas, que es la gramática de la §4 para todo panel superpuesto.
  - **El tipo se detecta desde la ruta**, que es la gramática del diálogo del prototipo: UNC por el
    prefijo, USB preguntándole a la unidad, local en el resto — y las tres consecuencias aprobadas
    del paquete («se vigila en continuo…», «no siempre está conectada…», «por red…») acompañan al
    tipo detectado. Donde no hay detector —previsualizaciones, pruebas— las tres píldoras siguen.
  - **«Examinar…» pregunta con el selector de Windows** empezando en la biblioteca de Vídeos, o
    responde desde la carpeta de entrega para una ejecución que no es dueña del perfil: la cuarta
    respuesta de la misma salida que ya tenían las copias.
  - Añadir con éxito **cierra el diálogo solo** y deja el consentimiento del primer escaneo a la
    superficie de la ruta; añadir la primera lista se refresca sola, que era el defecto de la casa
    en su forma de lista.
  - El primer arranque conserva su formulario en línea con sus cuatro formas, y se retira cuando
    deja de ser el primer arranque; la confirmación de borrado de Ajustes rehúsa con «Conservar» —
    la palabra del inventario — porque «Cancelar» ya lo dice el actualizador en la misma página y
    dos órdenes visibles con un nombre es la ambigüedad que el paseo se niega a pisar.

- **Las carpetas de la biblioteca ganan su sitio en Ajustes**, que es donde el prototipo las tiene:
  la lista como filas-tarjeta —ruta en monoespaciada, el tipo en palabras, el chip de disponibilidad
  con el distintivo compartido— y el borrado tras la misma confirmación en rojo que enseñó el primer
  arranque, ahora con las dos consecuencias que el paquete de diseño escribió para ella: qué se queda
  (los archivos) y qué se va (los títulos con sus marcas). La vista comparte el modelo del onboarding
  —una lista, dos superficies que nunca coinciden en pantalla— y dos puertas corrigieron el primer
  intento: el distintivo de no disponible es el de toda la aplicación, no un dibujo propio, y una
  sección de Ajustes no trae geometría propia.

- **La Biblioteca lidera con la fila del prototipo: el contador junto al título, la búsqueda contra
  el borde derecho, y el tipo como tres píldoras —Todo, Películas, Series— que consultan al
  pulsarse.** Las píldoras escriben los bits de tipo separados de los de estado, que el repositorio
  siempre supo combinar: «películas sin empezar» era expresable en la consulta e inalcanzable desde
  la pantalla, porque un solo `ComboBox` atado al valor completo hacía excluyente lo que no lo era.
  El defecto de la casa, en su forma de filtro.
  - Los dos desplegables pasan a píldora con su nombre dentro —«Filtrar Todo», «Ordenar Título»—
    sobre una plantilla que conserva los nombres de parte de Fluent, que es de donde cuelgan los
    cinco estados. Aplican al elegir, así que **«Aplicar» se retira**: era un botón cuyo único
    trabajo era repetir lo que el control de al lado ya había dicho. Desviación deliberada del
    inventario de controles, que no lo listaba entre los eliminados.
  - «Quitar filtros» existe sólo mientras algo estrecha la cuadrícula, y «Borrar la búsqueda» se
    muda al estado sin resultados, que es la salida para la que el inventario lo añadió.
  - La cuadrícula gana el cuarto estado de la §4 —vacía de verdad, con `LibraryEmpty*` en los dos
    idiomas— y la fila del escaneo sólo existe mientras escanea.
  - La escena del paseo siembra ahora una película identificada de verdad, porque un archivo sin
    identificar no es película ni serie —el catálogo lo lista bajo un tercer tipo— y las píldoras
    sobre una biblioteca de archivos sueltos daban vacío legítimo por los dos lados.


- **Todo botón es una píldora, en las diez pantallas a la vez.** Es lo que el prototipo dibuja en
  todas las suyas —`btnPri` y `btnSec` son los dos `border-radius: 999`—, y la §7 de la propuesta de
  diseño **da el número en vez de dejarlo a ojo**: `CornerRadius=18`, la mitad del alto de control, no
  el 999 de CSS. Un tercer radio donde la escala tenía dos a propósito, y lo que se lo gana es la regla
  a la que la propia escala está sometida: la pregunta no es «¿tiene sentido el escalón?» sino «¿lo
  contradice algo del árbol?». No lo contradice nada — lo gastan **todos** los botones—, así que no es
  el escalón-para-un-consumidor por el que se rechazó `FontSizeMono`.
  - Tres clases dicen que no son píldoras y lo dicen ellas: el destino del carril, el cromo del
    reproductor y la ficha de portada. Ganan porque un selector de clase declarado después vence.
  - El campo de búsqueda también, que es como lo dibuja el prototipo.


- **Cada portada se pinta del color de su título, que es lo que hace que una biblioteca parezca una
  biblioteca.** Hasta ahora eran todas el mismo rectángulo gris con dos letras, y la razón escrita era
  que esta aplicación no trae arte ni ficha con qué pedirlo. Las dos cosas siguen siendo ciertas y la
  conclusión era falsa: **el prototipo tampoco tiene arte**. Leyendo su fuente el 2026-08-22, cada
  portada suya son cuatro degradados CSS calculados a partir de **un solo tono** —
  `linear-gradient(200deg, hsl(H 38% 30%), hsl(H+34 46% 12%))` bajo un halo radial, una trama y un
  anillo—, y no hay ni una imagen en el archivo. Así que el muro de color no cuesta red, ni ficha de
  TMDB, ni un archivo en disco, y **ninguna de las razones por las que el arte quedó fuera de 0.2.0 le
  aplica**.
  - El tono sale del **título**, que es lo único que las cuatro listas detrás de `IPosterCard` tienen
    en común. Con un hash propio y no con `string.GetHashCode`, que .NET aleatoriza por proceso: una
    biblioteca sería otro juego de colores en cada arranque, y un color que cambia no lo aprende nadie.
  - Tres capas de las cuatro. La trama diagonal es un degradado repetido, que Avalonia no tiene como
    pincel, y es justo la capa al 5,5 % de alfa que nadie echa de menos.
  - **Las iniciales se quedan**, aunque el prototipo no las tenga: dos letras dicen qué título es antes
    de que el color le haya enseñado nada a nadie, y un color a solas es una distinción que no recibe
    quien no ve color.
  - **En los dos altos contrastes el color no se pinta.** `PosterArtOpacity` vale 0 ahí y la ficha
    vuelve al relleno y sus iniciales, con su contraste medido: un tono elegido por un hash es una
    relación de contraste que no decidió nadie, y quien pide alto contraste está pidiendo lo contrario
    de un color decorativo.
  - **Y la puerta que lo prohibía se declaró en vez de aflojarse.** `No_state_is_told_by_colour_alone`
    rechaza un color enlazado al modelo, con razón. En vez de meter dos filas en su lista de
    excepciones —que sólo encoge—, hay una **segunda lista con su propia razón**: aquí el color no
    sustituye a nada, repite lo que ya está escrito debajo, encima y en el nombre accesible. Y la
    excepción **se paga**: una vista de esa lista tiene que apagar su color en alto contraste, y hay
    una prueba nueva que lo mide por los dos lados — que la vista lee el token y que el token es 0.


- **La columna del reproductor enseña un panel a la vez, con sus nombres al principio.** Es lo que
  hace el prototipo y era el cambio más grande que quedaba: hasta ahora los cinco paneles —pistas,
  salida de audio, marcas de la serie, segmentos detectados y otras versiones— **se montaban todos a
  la vez** y había que bajar por 320 px de columna para llegar al último. Ahora cada uno tiene su
  pestaña, y **la pestaña sólo existe si su panel existe**.
  - **Ningún modelo decide cuál se abre, y eso está medido.** Avalonia 12.1.1 **salta una pestaña
    invisible** al elegir la primera —comprobado el 2026-08-22 con la primera de tres oculta, que abrió
    en la segunda—, así que una sesión sin pistas abre en lo que sí tenga, sin que nada en el shell
    tenga que calcularlo. Cero líneas de código y cero cadenas nuevas: cada pestaña lleva el nombre que
    su propio panel ya declaraba.
  - **Cinco pestañas y no las cuatro del prototipo.** Sus cuatro nombres —Audio, Subtítulos, Vídeo,
    Marcadores— son su reparto, no el de esta aplicación, que guarda cinco paneles; agrupar dos bajo
    una pestaña pediría o una palabra que nadie ha aprobado o un sexto booleano en el shell.
  - **Y lo cazó el paseo, que es exactamente para lo que existe.** Cuatro escenas se pusieron en rojo a
    la vez diciendo «coincidió con 0 controles en pantalla», y tenían razón: un panel que no es la
    pestaña abierta **no está en el árbol**, así que el clic no tenía dónde caer. Las cuatro abren
    ahora su pestaña con el ratón antes de pulsar dentro, y el trinquete sigue en **0 pendientes**
    (137 identidades, 137 pulsadas).


- **En el tema claro, la columna del reproductor tenía el texto invisible, y la puerta que debía
  cazarlo miraba a otro lado.** Medido el 2026-08-22: `TextPrimaryBrush` (#111827) sobre
  `PlayerSurfaceBrush` (#0B0D10) da **1,10:1** donde WCAG AA pide 4,5:1. Las superficies del
  reproductor son oscuras **en los cuatro modos** —eso está decidido desde el principio, para que la
  imagen conserve su contraste—, mientras que la tinta del tema es oscura en dos de ellos. El
  resultado: en cualquier equipo con Windows en claro, el selector de pistas, la salida de audio, la
  lista de marcadores, las detecciones y el panel de versiones **no se podían leer**, y tampoco los
  relojes del transporte ni sus tres pictogramas.
  - `ContrastTokenTests` medía el texto primario sobre **siete** superficies, y **las tres del
    reproductor no estaban entre ellas**. El agujero tenía exactamente la forma del defecto. Ahora
    son nueve, en los cuatro modos, con la banda translúcida del transporte fuera por la razón que ya
    estaba escrita —una relación de contraste contra un color translúcido es una conjetura sobre lo
    que hay detrás— y medida en su lugar contra la superficie sobre la que se dibuja.
  - La corrección son dos pinceles, `PlayerTextBrush` y `PlayerTextSecondaryBrush`, claros en los
    cuatro modos, y **puestos en el contenedor** allí donde se puede: Avalonia hereda `Foreground`, así
    que la cabecera, la columna y la banda del transporte cubren de una vez todo el texto que no lleva
    pincel propio. Los tres avisos ámbar de la salida de audio y los cinco superpuestos sobre la imagen
    **no cambian**: pintan su propio fondo con los colores del tema, y ahí la tinta del tema es la
    correcta.


- **La bandeja de revisión pinta la tarjeta de candidato del prototipo: el borde tintado por el
  estado, el distintivo arriba a la derecha y dos columnas debajo.** Antes era un rectángulo con
  borde neutro donde la clave, el porcentaje, la palabra del estado y el encabezado «Por qué» iban
  seguidos en una línea y media, sin decir cuál de ellos era la respuesta. Ahora **el borde entero se
  tiñe** —acento cuando la coincidencia es sugerida, ámbar cuando está pendiente—, que es lo que hace
  el prototipo y **la única señal que sobrevive a los dos altos contrastes**, donde la superficie de
  la tarjeta y la de la página son el mismo color. Debajo, lo propuesto a la izquierda con **una barra
  de confianza que nunca existió** —dibujada del mismo número que escribe el porcentaje, así que no
  pueden discrepar— y las razones a la derecha, en viñetas.
  - Dos cadenas, las dos del prototipo y en los dos idiomas: `ReviewProposedCandidate` y
    `ReviewConfidence`. «Señales consideradas» no entra: `ReviewExplanationHeading` —«Por qué»— ya
    decía eso y llevaba dos días diciéndolo.
  - **Tres cosas que el prototipo dibuja y esto no puede, y son omisiones medidas, no descuidos.**
    `MatchCandidate` lleva un id, una clave, un tipo, una puntuación y sus señales, y **ningún arte**:
    una miniatura 2:3 vacía en cada fila prometería una imagen que no existe. El **título** del
    candidato no está —lo que hay es la clave del proveedor, `movie:329865`— y el **tipo** necesitaría
    las palabras «Película» / «Serie», que el paquete de cadenas no propone y que ya se decidió el
    2026-08-22 no inventar. Y **los cuatro botones al pie de la tarjeta** existen una fila más abajo:
    Aceptar y Rechazar actúan sobre lo que la lista tenga seleccionado, y meterlos en cada tarjeta
    convertiría una decisión por bandeja en una decisión por fila — un cambio de cómo funciona la
    superficie, no de cómo se dibuja.
  - El estado escoge la clase desde el modelo, con `Classes.suggested` y `Classes.pending`, y no a
    través de un convertidor: los dos estados ya son dos booleanos de la tarjeta, y un convertidor
    sería un tercer sitio decidiendo cuál es cuál.


- **Ajustes pasa a la fila-tarjeta del prototipo: nombre, la frase debajo, y el control contra el
  borde derecho.** Es la unidad con la que el prototipo dibuja un ajuste, y aquí no existía ninguna:
  un interruptor con la etiqueta dentro, luego una frase suelta sobre ese interruptor, luego el
  siguiente — que se lee como seis cosas donde hay tres. Ahora son **dieciocho tarjetas más una
  plantilla que produce las once de los atajos, en ocho de las diez secciones de la página**, y las
  frases que las describen son las que la página ya escribía; no se ha inventado ninguna. Los
  interruptores pierden la etiqueta que llevaban dentro y **conservan el nombre accesible que ya
  declaraban**, así que un lector de pantalla oye exactamente lo mismo y sólo se ha movido la pintura:
  la misma permuta que hizo el carril cuando sus destinos pasaron a pictogramas.
  - **La palabra de estado a la izquierda del interruptor** —«Activado» / «Desactivado»—, que el
    prototipo trae en los dos idiomas y este árbol no tenía. Es **muda para el lector de pantalla**:
    la casilla de al lado ya anuncia si está marcada, y un segundo texto diciendo lo mismo lo diría
    dos veces, la segunda con una voz que no puede equivocarse. La misma decisión que tomó la lupa.
  - **Los once atajos de teclado y los seis controles del estilo de subtítulos** entran en la misma
    gramática, y de paso **los tres deslizadores de subtítulos enseñan por primera vez el número que
    están fijando**. El tamaño se escribe con `StringFormat` y el signo de porcentaje **fuera** del
    especificador numérico, donde es un literal: dentro de él, `0 %` multiplica por cien y escribe
    8000 para ochenta.
  - **En Apariencia las píldoras van debajo del nombre y no a la derecha, y eso está medido, no
    concedido.** Un `WrapPanel` en una columna `Auto` se mide con anchura infinita, así que pone
    todas las píldoras en una línea y sólo vuelve a repartirlas al colocar — que es la forma de
    anchura infinita que este repositorio lleva nueve medidas cazando, y justo la que
    `AppearanceSettingsTests` existe para prohibir alrededor de estos botones. La tarjeta cambia; la
    geometría de dentro, no.
  - Y una cadena más, `AppearanceThemeLabel` —«Tema»—, porque ese ajuste **no tenía nombre propio**:
    el que se le veía era el de su sección, y el título de una sección tiene que quedarse fuera de
    las tarjetas o deja de empezar donde empiezan las otras nueve.


- **La lupa dentro del campo de búsqueda y el `+` junto a «Añadir carpeta».** Los dos son del
  prototipo y los dos cambian sólo lo que se dibuja: la lupa es **decoración y no control** —el campo
  ya lleva el nombre accesible, y un segundo nombre ahí haría que un lector de pantalla dijera «Buscar
  en la biblioteca» dos veces—, y el `+` va **al lado de la palabra y no en su lugar**, porque un
  pictograma solo está bien en un carril de 64 px donde no cabe una palabra y mal en la única acción
  de una pantalla que alguien ve por primera vez.


- **El reproductor deja el carril a la vista, se pone cabecera propia y sólo ocupa la columna que
  usa.** Tres cosas que el prototipo dibuja y que aquí eran distintas: la sesión tapaba las dos
  columnas, así que **abrir una película se llevaba por delante los cinco destinos**; sus tres botones
  —cerrar, minirreproductor y pantalla completa— iban con palabras encabezando una columna de paneles,
  y ahora son tres pictogramas en una franja sobre la imagen, con los mismos nombres para quien usa un
  lector de pantalla; y esa columna de 320 px estaba ahí **existiera o no alguno de sus cinco
  paneles**, así que un archivo con una sola pista de audio, sin marcadores y sin otra versión dejaba
  un rectángulo vacío ocupando un quinto del ancho de la imagen. Ahora la anchura vuelve a la película
  cuando no hay nada que poner en ella.


- **El reproductor dice dónde vas y cuánto queda, y su transporte es una franja y no una tarjeta.**
  Había una barra de posición en el modelo desde siempre —posición, duración y el salto a un minuto
  concreto— y **no se pintaba en ninguna parte**: se leía en cada cambio de estado y se tiraba, así
  que quien veía una película no podía saber por dónde iba ni llevarla a otro punto con el ratón.
  Ahora está, con el tiempo transcurrido a la izquierda y la duración a la derecha, y **la hora sólo
  aparece cuando la hay**. Debajo, los mandos en una línea con el nivel de volumen escrito en cifras
  —que tampoco estaba— y la velocidad. La barra **no aparece hasta que el motor dice cuánto dura el
  archivo**: un cursor a mitad de una barra de longitud desconocida no señala nada, y una barra en
  gris diría «no es para ti» donde la verdad es «todavía no».


- **«Añadir medios», al pie del carril de navegación.** Es lo que el prototipo pone ahí y lo primero
  que necesita quien abre la aplicación con la biblioteca vacía. Lleva a la pantalla donde se añade
  una carpeta **y deja el formulario vacío al llegar**, que es la mitad que lo distingue del destino
  «Biblioteca»: hasta ahora nada limpiaba la ruta después de aceptar una carpeta, así que quien
  añadía una y volvía se encontraba la anterior escrita y un segundo intento contestaba «ya está en
  la biblioteca» — una negativa causada por la pantalla y no por la persona. Lo mismo con un aviso de
  ruta rechazada y con una retirada que alguien dejó a medio confirmar.


- **La marca y la firma del editor vuelven a Créditos, que es su sitio.** Estaban al pie de la
  navegación de 248 px; con el carril de 64 no caben, y repetir el nombre en la barra de título lo
  habría escrito dos veces —Windows ya lo dibuja ahí—. **Y esto no era sólo colocación**: los términos
  de TMDB piden que su logotipo se vea **menos prominente que el nombre del producto**, y al quitarlo
  del carril la aplicación dejó de escribir su propio nombre en ninguna parte. Ahora los dos están en
  la misma pantalla, que es lo que esa condición significa de verdad.


- **Dos de las cuatro animaciones del paquete, y una preferencia del sistema que de verdad las apaga.**
  El tooltip de cada destino del carril entra deslizándose, y el punto junto a «Escaneando» late
  mientras el escaneo corre — que es lo único en toda la pantalla que dice que sigue trabajando entre
  dos saltos del contador. Con «Mostrar animaciones en Windows» desactivado **duran cero**, no menos:
  el tema escribe la duración que las animaciones leen.


- **La navegación pasa a un carril de 64 px con iconos, y la aplicación dibuja su propia barra de
  título.** Es la composición del prototipo: los cinco destinos son pictogramas de la fuente que
  Windows 11 trae de serie, el abierto se marca con relleno **y** con una barra de 3 px —dos señales,
  y una no es color—, y el nombre de cada destino sigue estando en el tooltip y en lo que lee un
  lector de pantalla. **Ninguna etiqueta se ha reescrito**, así que los cinco responden a los mismos
  nombres de siempre. La barra de título de 44 px hace que la ventana sea una sola superficie de
  arriba abajo; Windows sigue dibujando minimizar, maximizar y cerrar sobre ella.


- **El bloque de «Continuar viendo» es ahora el héroe que el diseño pide.** El título va en grande y
  en peso ligero, el antetítulo lo etiqueta sin gastar un nivel de encabezado, y la barra de progreso
  es la misma regla de 3 px que llevan las fichas, con el porcentaje en palabras al lado. **Lo que no
  lleva, y por qué**: no lleva portada, porque unas iniciales junto a un título ya escrito en grande
  dicen dos veces lo mismo; y no lleva el botón «Detalles» del prototipo, porque abrir la ficha de un
  título pide un dato del catálogo que la pantalla de inicio no tiene y que ninguna consulta sabe
  devolver por identificador.


- **La biblioteca se ve como una cuadrícula que se adapta a la ventana.** Donde había una lista de una
  columna hay ahora fichas en rejilla, y al estrechar la ventana se reacomodan solas. Medido sobre
  diez mil títulos: la cuadrícula tarda **6 ms** y mantiene **36** fichas vivas, frente a los **4559 ms**
  y las **diez mil** de la forma ingenua. Con una biblioteca grande la diferencia es entre desplazarse
  y esperar.


- **Las películas y las series se ven como fichas.** Donde había una línea de texto por título hay
  ahora una ficha de 2:3 con las iniciales del título, el título a dos líneas como mucho y el año
  debajo, en la biblioteca y en los tres carriles de Inicio. No hay portadas y no las va a haber en esta versión —esta
  aplicación se publica sin ninguna conexión que las traiga—, así que las iniciales no son un hueco
  esperando una imagen: son lo que la ficha enseña, y son distintas en cada una, que es lo que hace
  que una pared de fichas se pueda recorrer con la vista.


- **Al elegir el tipo de carpeta se ve cuál está elegido.** Los tres botones —Local, USB, UNC o NAS—
  cambiaban un ajuste que la pantalla no enseñaba en ninguna parte: pulsar «USB» dejaba todo
  exactamente igual que pulsar «Local». Ahora llevan el mismo círculo que las opciones de tema y de
  idioma. La caja de la ruta gana además su etiqueta a la vista: estaba escrita en los dos idiomas y
  sólo la oía el lector de pantalla.


- **La biblioteca vacía dice que está vacía.** Con ninguna carpeta añadida —que es como empieza todo
  el mundo— la pantalla de primeros pasos no decía nada en absoluto: ni lista, ni encabezado, ni
  explicación. Ahora invita a añadir la primera y desaparece en cuanto hay una.


- **La lista de atajos dice algo cuando no hay ninguno asignado.** Un panel en blanco daba a entender
  que la aplicación no escucha el teclado; lo que ocurre es que no hay nada asignado, y las teclas
  multimedia del sistema siguen funcionando igual.


- **Las cuatro listas del lateral del reproductor dicen algo cuando están vacías.** Marcadores,
  detecciones, pistas y versiones se quedaban en blanco, sin forma de distinguir «no hay nada» de
  «todavía está cargando». Ahora cada una explica su propio vacío: los marcadores dicen que nada se
  escribe en tu archivo de vídeo, el selector de pistas que este archivo trae una sola de cada tipo, y
  la lista de versiones dice «una sola versión» **en vez de desaparecer**, para que la columna no se
  mueva y la respuesta esté donde la buscas.


- **La búsqueda de la biblioteca gana un botón para borrarla.** Volver a ver la biblioteca entera era
  seleccionar lo escrito, borrarlo y pulsar Aplicar. Ahora es una pulsación. Con la caja vacía el botón
  sigue ahí, apagado, para que la fila de controles no se mueva cada vez que escribes una letra.


- **Buscar y no encontrar nada ya te lo dice.** Hasta ahora una búsqueda sin resultados dejaba la
  pantalla en blanco, sin una sola línea de texto. Ahora dice que no hay resultados y por qué —la
  búsqueda y los filtros actuales—, y **no** dice que tu biblioteca esté vacía, porque no lo está.


- **Una comprobación que impide que un mando quede fuera de la pantalla.** Es el defecto que más veces
  ha aparecido en esta aplicación —siete—, siempre igual: una fila de botones con textos traducidos
  que no cabe y deja el último medio fuera, donde nadie puede pulsarlo. Ahora las cuarenta y ocho
  pantallas se miden de una vez contra la ventana más pequeña que la aplicación permite, y con todos
  sus avisos y estados visibles a la vez, que es más ancho de lo que llegan a estar nunca.

- **La pantalla de actualización dice cuál es su acción.** «Buscar actualizaciones» se pintaba igual
  que los otros tres botones, así que nada en pantalla decía para qué está esa pantalla.
- **El mini reproductor gana sus cinco controles.** Pausa/reanudar, dos saltos de diez segundos,
  volver a la ventana grande y cerrar, siempre visibles y con nombre para un lector de pantalla. La
  ventana dejaba caer todo lo que declaraba para sí misma en cuanto llegaba una sesión —el
  coordinador le asignaba el contenido entero— y ahora el cromo y el vídeo comparten la ventana.

- **Actualizador independiente.** Comprueba si hay una versión nueva sólo cuando lo pides o lo has
  permitido, te dice qué cambia en español y en inglés, descarga a una carpeta aparte comprobando el
  hash y el tamaño publicados, y entrega el paquete a Windows únicamente tras una confirmación que
  nombra esa versión. Una descarga que se corta se reanuda desde donde iba; una que no coincide se
  borra. Tu biblioteca no participa en la descarga: se midió que la base de datos y el binario en uso
  quedan intactos tras una actualización correcta, cancelada, manipulada e interrumpida. La Store
  sigue usando su propio canal.
- **Entrada para el gestor de paquetes de Windows.** Cada compilación deja el manifiesto de winget
  generado desde el propio archivo: el hash es el publicado, el ejecutable que declara está
  comprobado dentro del ZIP y las descripciones salen de los dos README. winget no cuesta nada y no
  exige certificado, así que es la primera vía de instalación que estará disponible.
- **Paquete ARM64 nativo.** MSIX y ZIP `win-arm64` construidos y verificados: cada binario del
  payload lleva ARM64 en su cabecera, LibVLC viaja donde el cargador lo busca, y la aplicación es
  idéntica a la de x64 archivo por archivo. Se construye en cada verificación y en cada publicación.
- **Detección automática de introducciones, resúmenes y créditos.** Compara localmente los
  episodios de cada serie entre sí y encuentra el audio que se repite; nada sale de la máquina y no
  se abre una sola conexión. Las detecciones se guardan por episodio con su confianza, una marca
  manual del mismo tipo las suprime siempre, y aceptar o corregir una la protege de todas las
  ejecuciones posteriores. El trabajo cede el paso a la reproducción y la opción está desactivada
  hasta que la enciendas. Evaluada contra un corpus retenido de series sintéticas: cada umbral
  aprobado se cumplió en la primera medición, con cero detecciones espurias en los episodios sin
  segmento.

- **Actualizar solo las fichas más antiguas, si tú lo enciendes.** Al abrir la aplicación se pueden
  volver a pedir al proveedor las fichas cuyos datos tienen más de 90 días: como mucho 20 por vez,
  las más antiguas primero y sólo las de títulos ya identificados. Viene apagado, el interruptor está
  en Ajustes → Privacidad y **no se ofrece siquiera si no has consentido la conexión**, porque sin
  ella no habría nada que pedir. Nunca ocurre mientras se escanea ni con un vídeo abierto, y se
  comprueba antes de cada ficha, no sólo al empezar. Con el interruptor apagado no se abre ninguna
  conexión: medido con el vigilante de red, que en la misma ejecución cuenta cero apagado y dos
  encendido.
- **Una guardia permanente contra el defecto de la casa.** La auditoría encontró componentes
  registrados que la aplicación nunca invoca, y cada caso se cazó a mano; ahora una prueba de
  arquitectura exige que cada servicio registrado tenga al menos una resolución fuera de su propio
  registro. Su primera ejecución enumeró 32 huérfanos —no los ~12 estimados— y destapó caras nuevas:
  la selección de dispositivo de audio nunca llega al motor, las preferencias de reproducción
  guardadas no se aplican, el conmutador de «visto» no está conectado a nada, no hay manera de
  retirar una carpeta de la biblioteca, y elegir una versión duplicada no hace nada. Cada deuda vive
  en la propia prueba con su identificador, y una segunda aserción expulsa la entrada en cuanto su
  cableado aterriza: la lista sólo puede encoger.

- **Abrir un vídeo ya no puede dejar la ventana quieta esperando al teclado.** Al empezar cada
  reproducción, la aplicación reclama las teclas multimedia del teclado, y se quedaba parada —el hilo
  que dibuja la ventana incluido— hasta que ese registro contestaba, sin ningún plazo. Si no
  contestaba, no había salida: el mismo hilo atrapado sujetaba el cerrojo que hacía falta para
  cancelarlo. Ahora la espera tiene plazo y ocurre fuera del cerrojo, y si se agota la reproducción
  empieza igual: las teclas del teclado son un extra, y una sesión sin ellas es mejor que una sesión
  que no arranca.

- **Ningún botón puede ya cerrar la aplicación al fallar.** Cada superficie con botones traía su
  propia clase de comando escrita a mano —veinticuatro— y ninguna recogía un fallo, así que un error
  en el trabajo de detrás terminaba el programa. Ahora hay una sola, y recoge siempre: quedan dos
  sitios en todo el código donde una espera puede terminar sin dueño, y los dos capturan. Al
  unificarlas aparecieron dos comportamientos que sólo una superficie tenía: la valoración de una
  ficha comprobaba el valor antes de guardarlo —y esa comprobación se habría perdido en silencio si
  una prueba no llega a estar ahí—, y los saltos del reproductor rechazan el siguiente mientras uno
  está en curso. Los dos siguen.

- **Un fallo deja de poder llevarse la aplicación por delante.** Hasta ahora, si algo salía mal en el
  trabajo que hay detrás de un botón, la excepción no volvía a nadie: se relanzaba sobre el hilo de la
  interfaz, y ahí lo único que esperaba era el final del programa. Ahora hay una red: lo que llega a lo
  alto del proceso queda anotado como un código en vez de tumbarlo, y una tarea que falla sin dueño se
  recoge antes de que se convierta en un cierre. Y el informe de diagnóstico —que existe desde hace
  tiempo y sólo sabía hablar de renombrados— por fin cuenta lo demás: hasta ahora, en una sesión donde
  nadie renombraba nada, una aplicación que fallaba parecía sana. Lo anotado vive en memoria y sólo
  durante esa sesión: nada se escribe en tu disco, y de una excepción viaja su tipo, nunca su mensaje.

- **La aplicación revisa sus propios cables al construirse.** Un componente que pide algo que nadie
  registró era hasta ahora un fallo que esperaba a la primera pantalla que lo necesitara —quizá la
  tuya, en un rincón que ninguna prueba abrió—. Ahora esa revisión ocurre al montar la aplicación, de
  modo que el fallo aparece al arrancar cualquier prueba en vez de delante de alguien. Cubre 109 de
  los 156 registros: los 45 que se construyen con una función propia siguen siendo opacos a la
  revisión, y decirlo es mejor que dejar creer que están cubiertos. Cuesta 0,22 milisegundos por
  arranque. En su primera ejecución no encontró ni un cable roto.

- **Preferencia de idioma.** En Ajustes → Apariencia puedes elegir español o inglés. La interfaz,
  los resúmenes de las actualizaciones y los metadatos hablan el mismo idioma — antes la interfaz
  iba fija en español mientras el resumen del actualizador y los metadatos seguían el idioma de la
  máquina, y podían llegar en otro. Los metadatos usan el idioma nuevo al reiniciar.
- **La ventana vuelve a donde estaba.** Posición, tamaño y estado (maximizada o no) sobreviven al
  cierre por cualquier camino. Una posición guardada en un monitor que ya no está conectado se
  descarta en vez de abrir la ventana fuera de toda pantalla, y una ventana cerrada maximizada
  reabre maximizada sobre sus límites de restauración.

- **Inicio enseña lo que has añadido hace poco.** La aplicación ya leía de su base los últimos títulos
  que entraron en la biblioteca —doce en cada carga, ordenados por fecha de alta—, y no los pintaba en
  ninguna parte: se leían y se tiraban. Ahora hay un carril con ellos, con el título en dos líneas como
  mucho, el año en un tono secundario y el aviso de que un medio no está disponible ahora mismo.

### Cambiado

- **El título de una ficha ocupa una línea, y un conmutador es la misma píldora que el botón de al
  lado.** Dos cosas que se vieron mirando la aplicación de verdad. La primera: un título que se
  iba a una segunda línea empujaba su propio año por debajo del año de la ficha vecina, y una fila
  de fichas se leía como un borde dentado; ahora se corta con puntos suspensivos —el título
  completo lo sigue anunciando el botón que envuelve la ficha, que lo lleva como nombre—. La
  segunda: la forma de píldora estaba declarada sólo para `Button`, y un `ToggleButton` no
  coincide con ese selector, así que «Favorito» y «Ver más tarde» conservaban la caja más baja del
  tema base, su esquina cuadrada y su relleno propio, junto a las píldoras de su misma fila. Los
  tres conmutadores del árbol se alinean ya con los botones, y una prueba lo afirma comparando la
  geometría de los dos controles.

- **Los bordes decorativos son por fin la línea capilar del prototipo.** Dieciocho contornos —los
  paneles de aviso y estado, el diálogo de añadir carpeta, el chip del tipo detectado, el banner
  del archivo suelto, la costura del riel y las dos tarjetas del actualizador— vestían el borde
  fuerte que la casa reserva para los límites de control, donde el 3:1 es obligación. El prototipo
  los pinta con su línea capilar (blanco al 7 % en oscuro, tinta al 9 % en claro), que este árbol
  ya tenía calcada y gastaba solo en las filas de ajustes y las fichas. La tarjeta de comparación
  de duplicados gana además el fondo de tarjeta que el prototipo le da, en vez de flotar como una
  caja dibujada. Se quedan como estaban, con aritmética: el vacío discontinuo de la Biblioteca
  (el prototipo lo traza con su borde fuerte) y las cinco superposiciones del reproductor, cuyo
  blanco al 18 % del prototipo, compuesto sobre su fondo, es justo el borde fuerte que ya tenían.

### Corregido

- **Inicio arrancaba vacío aunque hubiera algo a medio ver, y se llenaba solo al salir y volver.**
  La ruta con la que nace la aplicación no pasa por el navegador de rutas, así que el aviso de
  «se ha navegado» —el único sitio donde las superficies se alimentan— no sonaba nunca para la
  primera pantalla: Inicio sabía leerse y nadie se lo pedía hasta que se visitaba otra sección y
  se regresaba. Es la decimocuarta forma del defecto de la casa, esta vez en el minuto que todos
  los usuarios ven. Ahora el shell, nada más construirse, reproduce su ruta inicial por el mismo
  camino que una navegación de verdad, de modo que la primera pantalla y las navegadas comparten
  la única vía de carga que existe.

- **La barra de posición podía llevarse la reproducción al segundo 1 ella sola.** No llegó a
  publicarse: se midió al escribir su prueba, el mismo día que la barra. Un `Slider` de Avalonia
  recorta lo que se escribe en su valor contra el máximo que tiene **en ese instante**, y el modelo
  anunciaba la posición antes que la duración — así que los 120 segundos entraban en una barra cuyo
  máximo seguía siendo 1, la barra los recortaba a 1, y el manejador convertía ese recorte en un
  salto de verdad. El primer estado tras un salto a los dos minutos volvía leyendo 0:01.


- **Inicio llegaba hasta la mitad y el resto se dibujaba fuera de la ventana.** «Añadido
  recientemente» y «Quizá te interese» existían, tenían su texto en los dos idiomas y no aparecían en
  pantalla: la fila del carril se quedaba con todo el espacio sobrante y las dos secciones de debajo
  caían por el borde inferior. Medido a 1600 × 1000. Ahora Inicio se desplaza y las cinco secciones se
  alcanzan.


- **La pantalla de fallo ya no ofrece elegir otra versión cuando no hay otra versión.** Se decidía
  sólo por el motivo del fallo, sin mirar si el contenido tenía alguna alternativa catalogada, así
  que en el caso más corriente —un archivo que es el único de su título— invitaba a elegir entre uno.
  Ahora aparece únicamente cuando de verdad hay a qué cambiar, y dice eso en lugar de mandarte a otra
  pantalla.


- **Un botón de la biblioteca se veía y no se podía pulsar.** «Revisar versiones» quedaba a ras del
  borde inferior de la zona desplazable: trece de sus treinta y seis píxeles de alto estaban dentro y
  su punto medio no, así que un clic sobre él no llegaba al botón. Ahora la lista deja un margen al
  final, que es lo que hace que el último control de una pantalla se pueda usar.


- **Un rechazo, una retirada de carpeta y una petición de permiso dejan de pintarse iguales.** Las
  tres avisaban con el mismo color en la pantalla de carpetas. Ahora un rechazo se lee como un aviso,
  la retirada de una carpeta del catálogo se lee como lo que es, y la petición de permiso para el
  primer escaneo mantiene su tono neutro.


- **Los avisos que aparecen sobre el vídeo dejan de poder ocupar la pantalla entera.** La oferta de
  continuar, el aviso del episodio siguiente y la pregunta de cambio de versión se centraban, pero
  seguían creciendo sin límite: con un texto largo dentro, uno de ellos ocupaba 1278 píxeles de una
  pantalla de 1280. Ahora tienen un ancho máximo. Y el botón de saltar la cabecera se coloca abajo a la
  derecha, fuera del paso.

### Corregido

- **La vista previa del subtítulo enseña por fin el subtítulo que has elegido.** Sólo mostraba la
  tipografía: el color del texto, el del fondo, la opacidad y el contorno cambiaban sin que se viera
  nada. Y ahora se ve sobre el mismo negro del reproductor, porque un color juzgado contra el gris de
  una pantalla de ajustes no es el que verás sobre una película.

### Añadido

- **La bandeja de revisión dice cuando no queda nada que revisar, y lo dice como la buena noticia que
  es.** Estar vacía significa que AP Reelume identificó todo lo que encontró sin necesitar tu ayuda;
  antes era un panel en blanco, que parece más bien que algo no cargó.

### Cambiado

- **Ajustes termina de alinearse.** Tres de sus diez apartados —estilo de subtítulos, actualizaciones y
  créditos— seguían empezando más a la izquierda que los otros siete. Ya no.


- **Los círculos de estado dejan de verse pequeños.** Los `○ ◐ ●` que marcan si has visto algo, en qué
  destino estás y qué tema tienes puesto se dibujaban a dos tercios del tamaño de los símbolos del
  reproductor, lo justo para parecer un carácter suelto en vez de un estado. Los trece pasan al mismo
  tamaño.


- **Los duplicados se comparan lado a lado.** Las copias del mismo título se apilaban una debajo de
  otra, así que compararlas era desplazarse entre ellas — que es justo lo que esa pantalla existe para
  evitar. Ahora van en dos columnas, con las cifras de calidad en ancho fijo para que se lean unas
  debajo de otras, y una tercera copia baja sola a la fila siguiente.

### Corregido

- **Restaurar pedía una carpeta nueva para cada raíz, incluidas las que están donde deben.** Ahora la
  caja para escribir una ruta sólo aparece donde hace falta: cuando la carpeta no está o cuando hay un
  conflicto. Y en cuanto escribes una, la fila deja de decir que falta.


- **Una copia de seguridad que falla ya no se ve igual que una que salió bien.** «No hay espacio
  suficiente en el disco» se pintaba sobre el mismo fondo que «Listo». Cancelar sigue sin ser un fallo:
  no se creó nada a medias.


- **La pantalla de copias no enseñaba la copia que acababas de hacer.** El programa guardaba el nombre
  de la última copia y de la última exportación y no los mostraba en ninguna parte, así que sólo podías
  comprobarlo abriendo el explorador de archivos.


- **La pantalla de recuperación de la base decía en color suave que algo se había roto.** El detalle del
  fallo iba sobre el mismo fondo que los avisos amables, cuando esa pantalla sólo aparece si tu
  biblioteca no ha podido abrirse. Ahora se ve como lo que es. Y las dos rutas van en ancho fijo, que es
  lo que necesitas para ir a buscar tu copia a mano.


- **La vista previa de renombrado escondía justo la parte que cambia.** Las dos rutas se cortaban por
  el final, y el final es el nombre del archivo — lo único que distingue el origen del destino. Ahora
  se acortan por el medio, conservando los dos extremos, y van en ancho fijo para que se lean una
  debajo de otra. La flecha entre ambas dice ya qué significa a un lector de pantalla.


- **El editor de metadatos tenía ocho campos sin etiqueta a la vista.** Título, título original,
  sinopsis, año, géneros, cartel, fondo y el texto alternativo: los ocho anunciaban su nombre a un
  lector de pantalla y en la pantalla eran ocho cajas idénticas. Ahora cada uno lleva su etiqueta
  escrita encima.


- **Los avisos del editor de metadatos parecían texto suelto.** Un conflicto y un título sin
  identificar son dos formas de «lo que pediste no ocurrió», así que ahora llevan recuadro ámbar y
  símbolo; que el proveedor no conteste ahora mismo no es un fallo de nadie y se queda como dato.


- **La bandeja de revisión explicaba sus decisiones con nombres internos del programa.** Al revisar por
  qué AP Reelume cree que un archivo es una película concreta, la lista de motivos decía cosas como
  `Identification.Signal.Title` — y eso es exactamente para lo que existe esa pantalla. Ahora dice «El
  título coincide», «El año coincide», «El nombre del archivo admite más de una lectura» y los otros
  ocho, en los dos idiomas. Un lector de pantalla también los recitaba, y también los dice ya en
  palabras.


- **El aviso de «este sistema no tiene bandeja» parecía un dato más.** Se decía en texto llano, del
  mismo color que las etiquetas de al lado, cuando lo que dice es que algo que pediste no se pudo
  hacer. Ahora lleva recuadro ámbar y símbolo, como el resto de avisos.


- **La vista previa de diagnósticos obligaba a desplazarse de lado.** Es el texto que lees para decidir
  si compartes algo, así que ahora se ajusta al ancho y se lee entero.


- **Un ajuste de escaneo no tenía etiqueta a la vista.** La casilla del intervalo de recuperación
  anunciaba «Intervalo de recuperación en minutos» a un lector de pantalla y en la pantalla no se veía
  nada: sólo una caja de números. Ahora la etiqueta está escrita donde se lee.


- **La página de escaneo no decía qué hacía.** Tenía un título y dos controles y nada entre ellos.


- **El aviso de movimiento reducido decía lo mismo estuviera activo o no.** «AP Reelume respeta la
  preferencia de reducción de movimiento de Windows» es una frase sobre las intenciones del programa,
  no sobre el estado de tu equipo — y el programa ya sabía la respuesta. Ahora dice cuál de los dos
  estados tienes.


- **Las listas de marcas y de detecciones enseñaban el contenido interno del programa.** Cada fila
  pintaba algo como `IntroMarker { Id = 1111…, SeriesId = SeriesId { Value = d1f7… }, Kind = Intro,
  … }`: dos identificadores internos y el nombre de una clase, cortados por el borde de la columna sin
  manera de leer el resto. Ahora cada fila dice lo que es —«Introducción · 0:30–2:00»— y, si el texto
  no cabe, termina en puntos suspensivos con el texto entero en el globo de ayuda.


- **El selector de tipo de marca estaba sin traducir.** Ofrecía «Intro», «Recap» y «Credits» en
  español, que son los nombres internos de los tres tipos. Ahora dice «Introducción», «Resumen» y
  «Créditos», y las mismas palabras aparecen en las listas.


- **Aceptar una detección no cambiaba nada visible.** Aceptar o corregir un segmento detectado es lo
  que lo protege de la siguiente pasada del detector, y la lista quedaba exactamente igual que antes.
  Ahora la fila lo dice: «Créditos · 46:40–50:00 · confirmada».

### Cambiado

- **Ajustes es una página y ahora se lee como una.** Sus siete apartados empezaban en dos sitios
  distintos, cuatro de ellos con el título del tamaño de un título de página, y la página no tenía
  título propio. Ahora la página se titula «Ajustes» y los siete apartados están alineados y al mismo
  tamaño. Para quien navega por encabezados con un lector de pantalla, esto era antes un destino con
  cuatro primeros niveles dentro.


- **Las páginas de ajustes se parecen entre ellas.** Escaneo, recomendaciones y detección de segmentos
  tenían el título más pequeño que la de apariencia, sin margen alrededor y sin ancho de lectura. Las
  cuatro comparten ahora el mismo esqueleto.


- **El aviso de «este archivo no está en tu biblioteca» ya no se dibuja encima de la película.** Pasa a
  una banda propia encima de la imagen, con su acción a la derecha. Antes se superponía al vídeo y,
  como lleva fondo, se comía las pulsaciones destinadas a lo que había detrás; y al pasar al mini
  reproductor **se iba con él**, a una ventana en la que pedía más alto del que la ventana tiene.


- **Las cuatro listas del lateral del reproductor tienen filas de la misma altura.** Marcadores,
  detecciones, pistas y versiones pasan a filas de 36 píxeles que nunca se desplazan de lado: lo que no
  cabe se corta con puntos suspensivos y se lee entero en el globo de ayuda. En la lista de versiones,
  una etiqueta larga ocupaba varias líneas de altura variable.


- **Los botones del reproductor llevan símbolos en vez de palabras.** Reproducir, pausar, detener,
  los dos saltos, silenciar y los cinco del mini reproductor pasan a los pictogramas de Windows. No es
  una preferencia: con palabras traducidas, el mini reproductor plegaba sus cinco botones en tres filas
  dentro de una ventana de 480×270, y su ancho mínimo es aún menor. **Lo que cada botón le dice a un
  lector de pantalla no ha cambiado**, y sigue traducido en los dos idiomas; lo que cambia es lo que se
  ve. Los segundos que cubre cada salto se siguen anunciando igual.


- **Los botones del reproductor son más fáciles de acertar.** Reproducir, pausar, detener y los dos
  saltos pasan de 36 a 44 píxeles de zona pulsable: es lo que piden las guías de accesibilidad para un
  objetivo cómodo, y son los controles que se pulsan deprisa, a oscuras y a veces en el panel táctil de
  un portátil.


- **Los tres avisos de sonido se ven como avisos.** Que no haya ningún dispositivo, que la mezcla se
  haya degradado o que el dispositivo desaparezca a mitad de sesión se decían en texto llano, del mismo
  color que las etiquetas de al lado. Los tres significan lo mismo: lo que oyes no es lo que pediste.
  Ahora llevan recuadro ámbar y símbolo, como el resto de avisos de la aplicación.


- **El distintivo de estado del vídeo distingue lo que es un dato de lo que es un aviso.** Decía seis
  cosas con el mismo aspecto: que el HDR pasa tal cual o que decodifica la tarjeta gráfica —que son
  datos sobre un vídeo que va perfectamente— se veían igual que «esto ha caído a decodificación por
  software». Ahora los datos son texto secundario y los dos avisos llevan su propio recuadro ámbar con
  símbolo. Ninguno de los seis es un error: el vídeo se está viendo.


- **El reproductor tiene su propio fondo, y es el mismo con cualquier tema.** Todo lo demás en la
  aplicación sigue el tema claro u oscuro que elijas; el reproductor no, porque lo que se apoya encima
  es la imagen. Es un negro muy oscuro pero no negro puro, para que las bandas de arriba y abajo no
  parezcan un agujero al lado de un fotograma que casi nunca es negro del todo.


- **Un vídeo que no se puede abrir se ve como un fallo, no como un dato más.** El aviso de que algo no
  ha funcionado se pintaba sobre la misma superficie que usa el resto de la aplicación, así que la
  única pantalla que tiene que decir «esto no ha salido» se parecía a la que dice qué códec estás
  usando. Ahora tiene fondo, borde y símbolo propios, y su símbolo es **distinto** del de «no
  disponible ahora mismo»: dos cosas que sólo se distinguieran por el color no se distinguirían para
  todo el mundo. Y sus dos botones se reparten en varias líneas cuando no caben.


- **La ficha de una serie enseña una temporada cada vez.** Antes apilaba todas: una serie de ocho
  temporadas era una página sin final a la vista. Ahora hay un selector arriba y debajo los episodios
  de la temporada elegida. Con una sola temporada el selector **no aparece**, porque no habría nada que
  elegir.


- **Las filas de episodios de una serie miden todas lo mismo y sus números cuadran en columna.** Cada
  fila tiene la misma altura, así que una temporada ya no parece una lista a medio cargar, y el número
  del episodio va alineado a la derecha en una columna fija: el 9 y el 10 terminan en el mismo punto.


- **Las filas de botones de la biblioteca y de las fichas se reparten en varias líneas cuando no
  caben.** La fila de búsqueda y filtros, y las de acciones de una película y de una serie, dejaban al
  último botón fuera de la ventana en cuanto la pantalla era estrecha o la traducción larga. Ahora
  bajan de línea, que es lo que ya hacían los botones de la barra de título.


- **Un medio que no está deja de parecer un error.** El distintivo de «no disponible ahora mismo» —el
  que llevan los títulos de un USB desenchufado o de una carpeta de red caída— pasa a la forma de aviso:
  fondo ámbar, borde y un símbolo delante, para que no dependa de distinguir un color. Y se dice de una
  sola manera en las seis pantallas que lo enseñaban, que hasta ahora lo dibujaban cada una por su
  cuenta.


- **Con las recomendaciones apagadas, Inicio ya no dice que no haya nada que sugerir.** Decía «no hay
  nada que sugerir ahora mismo», que era falso: apagado, no se calcula nada y el catálogo no se llega a
  leer, así que la aplicación afirmaba algo sobre unas películas que nadie había mirado. Ahora el
  carril dice lo que de verdad pasa, y el estado vacío —encendido y sin resultados— se distingue del
  apagado.

- **El progreso de las fichas en curso es una regla fina al pie, no una barra en medio.** Tres píxeles
  de acento bajo cada ficha, con el porcentaje escrito encima como hasta ahora: la barra nunca es lo
  único que lo dice. Y los bloques de Inicio se separan más entre sí, para que los carriles no se lean
  como parte de lo que tienen encima.

- **La barra lateral dice en qué pantalla estás de dos maneras, y ninguna es el color.** El destino
  abierto lleva ya una barra de acento a su izquierda, además del punto relleno que ya tenía: quien no
  distinga esos tonos sigue sabiendo dónde está. Y los tres botones de acción del título se reparten en
  varias líneas cuando no caben, en vez de empujar al último fuera de la ventana.

- **Cada pantalla dice ya cuál es su acción.** Catorce pantallas más pintan con el color de acento el
  botón que es su sentido —continuar la película, guardar la ficha, crear la copia, añadir la carpeta,
  aceptar la coincidencia—, y el resto de botones quedan claramente al lado. **Dieciséis pantallas no
  destacan ninguno, y también es una decisión**: un marco no está para nada en concreto, una fila que
  se repite no puede destacar nada, y en las dos pantallas donde se te pide permiso —arrancar con
  Windows y exportar un diagnóstico— **no se destaca el sí**, porque empujar hacia el sí en una
  pregunta de permiso es exactamente lo que esta aplicación no hace.

- **Los bordes redondeados son ya dos medidas y no cinco.** Las esquinas se elegían pantalla por
  pantalla —4, 6, 8, 10 y 12 píxeles repartidos por veintiséis vistas—, así que dos tarjetas iguales
  podían redondearse distinto sin que nadie lo hubiera decidido: de las siete superficies de tarjeta
  de la aplicación, cuatro llevaban una medida y tres otra. Ahora hay **dos**, y siete sitios se
  igualan con el resto.

- **El espaciado de toda la aplicación es ya una escala y no ciento ochenta y seis decisiones
  sueltas.** Cada vista elegía por su cuenta cuánto separar sus cosas: ocho valores distintos
  —2, 4, 6, 8, 10, 12, 16 y 24— repartidos por cincuenta y cuatro pantallas. Ahora hay **cinco
  medidas** para toda la aplicación, y cambiarlas cambia la aplicación entera de una vez en lugar de
  archivo por archivo. Diecisiete sitios se mueven 2 píxeles y ninguno más; lo único que cambia en la
  pantalla de inicio es un píxel del borde inferior de una tarjeta.

- **Los botones del reproductor son más fáciles de acertar, y la pantalla de fallo dice qué hacer.**
  Reproducir, pausar y detener tienen ya un área mínima de pulsación de 36 por 36 píxeles, la misma
  que estrenó el mini reproductor. Y cuando algo falla, «Volver a intentarlo» se pinta como la acción
  principal de esa pantalla en lugar de igual que el botón de al lado.

- **Los tamaños de letra son ya una escala y no treinta decisiones sueltas.** Cada pantalla elegía
  el tamaño de su texto por su cuenta: **trece tamaños distintos** repartidos por treinta archivos,
  con títulos que se parecían entre sí sin llegar a coincidir. Ahora hay **cinco tamaños** para toda
  la aplicación, y cambiarlos cambia la aplicación entera de una vez en lugar de archivo por archivo.
  Lo único que se mueve en la pantalla de inicio es un píxel del borde de una tarjeta, y en la
  dirección buena: antes dependía de la escala del sistema y ahora es el mismo al 100 %, al 150 % y
  al 200 %.

- **El botón de «continuar viendo» por fin se distingue de los demás.** Llevaba puesta la marca de
  «acción principal» y **ningún estilo la definía**, así que el botón que es el sentido de la pantalla
  de inicio se pintaba igual que cualquier botón secundario a su lado. Ahora en reposo lleva el color
  de la aplicación, y al pasar el ratón o pulsarlo responde **igual que todos los demás controles**,
  que es lo que hace que la aplicación se sienta de una pieza.

- **Una comprobación exige que cada medida del tema la gaste alguien.** Los números del aspecto
  —espaciados, redondeos, grosores— se declaran en un solo sitio, y hasta ahora nada impedía declarar
  uno y no usarlo nunca. Se habían colado tres: dos duraciones de animación que **repetían un número
  que la aplicación ya tenía en otro sitio** —y que dos pruebas vigilaban en la copia mientras nadie
  miraba el original— y un símbolo que se escribía a mano en los seis sitios donde aparece. Los tres
  se han quitado, la garantía que daban las dos pruebas se ha movido al sitio donde vive el número de
  verdad, y ahora una medida declarada tiene que gastarse o figurar en una lista que **sólo puede
  encoger**. Nada de esto cambia lo que se ve; evita que dos copias del mismo número acaben
  discrepando.

- **Los botones tienen forma y reaccionan, y los colores salen del tema.** Hasta ahora el borde de un
  botón era transparente en sus cuatro estados —no tenía forma propia—, el color de reposo, de paso de
  ratón y de pulsado venía del tema base de la biblioteca gráfica, y **deshabilitado se pintaba igual
  que en reposo**: lo único que los separaba era el gris del texto. Ahora los cuatro estados salen de
  los mismos tokens que el resto de la aplicación, con un borde de un píxel que se ve en los cuatro
  temas, y en alto contraste pasar el ratón o pulsar **invierte** el botón —el relleno toma el color
  del borde y el texto el del fondo— porque en esas paletas un tono más claro no diría nada.

- **Las barras, los interruptores y las opciones también dejan de pintarlas Windows.** Los cinco
  controles de barra —el tamaño y el borde de los subtítulos, la posición del vídeo y el peso de las
  recomendaciones—, los dos botones que se quedan pulsados y el selector de versión duplicada salían
  **todos** del mismo azul del sistema, idéntico en el tema claro y en el de alto contraste oscuro.
  Además, una **barra apagada dejaba de decir dónde estaba su valor** —las dos mitades quedaban del
  mismo gris, y la barra del vídeo está apagada siempre que no hay nada reproduciéndose—, un **botón
  que se queda pulsado no tenía borde** en ninguno de sus diez estados, así que no tenía forma
  propia, y **apagado se pintaba igual que en reposo**. El punto de la opción elegida era blanco en
  los cuatro temas. Ahora los tres salen de los colores de la aplicación, la barra apagada sigue
  diciendo su valor y el interruptor tiene borde.

- **Las listas desplegables se ven, abiertas y cerradas.** La fila elegida dentro de un desplegable
  se separaba de las demás **menos de lo que hace falta para distinguir dos tonos**, igual que le
  pasaba a la fila de una lista, y por la misma causa: el azul translúcido de Windows. El panel que
  se abre tampoco tenía un borde que se viera —era negro al catorce por ciento—, y un desplegable
  abierto flota sobre la ventana, así que su borde es lo único que dice dónde termina. Y en los dos
  temas de alto contraste, pasar el ratón por una fila la pintaba **del mismo color que su texto**:
  negro sobre negro. Ahora la fila elegida lleva borde del color de la aplicación además del fondo,
  el panel tiene el borde de todo lo demás, y el texto de las filas que invierten sale del color que
  existe para eso. Los ocho desplegables de la aplicación.

- **Los campos de texto se leen.** El aviso gris que dice para qué sirve un campo vacío estaba
  pintado con dos capas de transparencia encima del color, y quedaba **por debajo de la mitad** del
  contraste que hace falta para leer un texto. Un campo apagado tampoco se leía ni se distinguía del
  fondo: ni su texto ni su borde llegaban al mínimo. Y el recuadro azul que marcaba el campo con el
  cursor era **el mismo azul en los cuatro temas**, incluido aquél en el que el foco es amarillo.
  Ahora el fondo, el borde, el texto y el aviso salen de los colores de la aplicación, el foco usa el
  color de foco de cada tema, y en alto contraste pasar el ratón **invierte** el campo igual que hace
  el botón. Alcanza también a los cinco campos numéricos, que son una caja de texto con dos flechas.

- **Una lista dice en qué fila estás.** La fila seleccionada se pintaba de un azul translúcido que,
  sobre el fondo, se separaba de las demás **menos de lo que hace falta para distinguir dos tonos**;
  el texto encima se leía perfectamente, así que el problema nunca fue leer la fila, era saber cuál
  era. Ahora la fila seleccionada lleva **un borde del color de la aplicación** además del fondo, y
  en los dos temas de alto contraste —donde ese fondo es el mismo de la página— el borde es toda la
  señal. Todas las filas llevan el mismo borde y sólo cambia de color al seleccionarse, así que
  seleccionar una no mueve su texto. Afecta a las 23 listas con datos.

- **Las casillas de verificación ya no las pinta Windows.** Las dieciocho de la aplicación tomaban
  sus colores del tema de la biblioteca gráfica, y eso tenía tres consecuencias que se veían. Una
  casilla **marcada y apagada** era ilegible en el tema claro: la marca blanca sobre el gris de
  debajo, con menos diferencia de la que hace falta para ver una forma. El **borde de una casilla
  apagada** tampoco llegaba al mínimo. Y una casilla **marcada** salía siempre del mismo azul de
  Windows, que no es el color de esta aplicación en ningún tema, ni el de alto contraste en los dos
  que lo son. Ahora la caja, la marca y la etiqueta salen de los mismos colores que el resto, en los
  cuatro temas, y en alto contraste pasar el ratón o pulsar **invierte** la caja igual que hace el
  botón. **En alto contraste una casilla se pintaba exactamente igual que en el tema normal**, así
  que encender el alto contraste en Windows cambiaba todos los controles menos éste.

- **Un control apagado se distingue de uno normal, también en alto contraste.** En los temas claro y
  oscuro se sabía por el color: un relleno más apagado y un texto más gris. En los dos temas de alto
  contraste **no se sabía de ninguna manera** — esas paletas son de dos colores y no les queda un
  tercero que gastar, así que el relleno, el borde y el texto de un control apagado eran exactamente
  los de uno normal. Ahora lo dice el **borde punteado** que el diseño pide, dibujado por encima del
  control y en los diez tipos que pueden apagarse: botones, casillas, campos de texto, listas
  desplegables, filas de una lista, barras deslizantes y los demás. Se dibuja uno por control y no
  uno por pieza: una lista desplegable o un selector numérico llevan un campo de texto dentro, y dos
  rectángulos punteados a unos píxeles uno de otro no son una señal, son ruido.

- **La aplicación se pone en alto contraste cuando Windows lo está.** Hasta ahora el tema de alto
  contraste existía en el código y **ningún camino lo seleccionaba**: quien lo tuviera encendido en
  Windows veía la aplicación igual que todos los demás. Ahora se lee del sistema al arrancar y manda
  sobre las tres opciones de apariencia, porque es una necesidad y no un gusto —así que las tres
  opciones siguen siendo tres y no hay nada que reconfigurar—. Si el tema del sistema es claro u
  oscuro se decide por el color con el que Windows dibuja las ventanas y no por el nombre del tema,
  que está traducido y que cualquiera puede cambiar. Encenderlo con la aplicación abierta llega en el
  arranque siguiente.

- **El recuadro del foco es ahora doble, y se ve en los diez tipos de control.** Antes se dibujaba
  engordando el borde del propio control, y eso dejaba dos huecos medidos: una barra deslizante no
  tiene borde donde pintarlo, y en alto contraste claro el borde y el foco son el mismo negro, así
  que enfocar cambiaba un píxel de grosor y nada que se pudiera ver. Ahora son dos recuadros
  concéntricos, uno del color del foco y otro del color del fondo: lo que distingue al control
  enfocado es la **forma**, que sigue viéndose en un tema donde todo es blanco y negro. En alto
  contraste el amarillo queda reservado al foco y la marca pasa al azul o al cian, que antes eran el
  mismo amarillo.

- **El cierre de un vídeo tiene ya prueba de su caso lento.** Al cerrar un vídeo, la aplicación
  espera a que se liberen sus datos antes de soltar el reproductor —hacerlo al revés es lo que hace
  caer al descodificador—, y esa espera tiene un tope para que un cierre nunca se quede colgado: si
  se agota, la liberación termina igual por su cuenta un instante después. Eso funciona, pero **sólo
  se ejercía cuando la máquina de verificación iba lo bastante cargada**: de cinco mediciones de la
  misma versión, el tope se agotó en una y en cuatro no. Ahora una prueba pide un tope más corto que
  la espera, así que rendirse es el único desenlace que el reloj permite, y lo comprueba. Con ella se
  cubrieron las otras tres decisiones del cierre —cerrar dos veces, cerrar con un reproductor
  todavía prestado y un tope que no puede esperar nada— y se retiró un dato que la fábrica de vídeo
  publicaba y que no leía nadie. El archivo pasa de medir distinto en la máquina de integración a
  medir lo mismo tres veces seguidas.

- **La prueba que vigila las avalanchas de cambios ya no aprueba cuando no hay avalancha.** Cuando
  llegan más cambios de los que Windows alcanza a anotar, la aplicación se entera de que ha perdido
  avisos y vuelve a recorrer la carpeta entera en vez de dejar de seguirla en silencio; eso funciona
  desde que se corrigió. Lo que no funcionaba era su prueba: provocaba la avalancha y **no comprobaba
  que hubiera ocurrido**, así que las veces en que no ocurría aprobaba sin ejercer nada de lo que
  protege. Ahora el tamaño del búfer se puede pedir al construir el vigilante —la aplicación sigue
  pidiendo el máximo—, la prueba pide el mínimo, desborda de verdad y lo afirma. Midiéndolo salieron
  otras dos condiciones que ocurrían o no por azar —el error que sí termina la vigilancia, y qué
  cambios se juntan en un mismo aviso—, y cada una tiene ya su prueba. El archivo pasa de medir
  distinto en cada ejecución a medir lo mismo tres veces seguidas.

- **La vigilancia de cobertura pasa a medirse donde se verifica.** El listón de cada archivo se había
  medido en la máquina de quien programa y se comprobaba en la de integración, que no tiene tarjeta
  de sonido: siete archivos de audio, vídeo y temporizadores daban números distintos en cada sitio, y
  uno de ellos —el catálogo de dispositivos de audio— pasaba de 79/61 a 32/11, porque allí no hay
  nada que enumerar. No era que la cobertura empeorase; era que se medía en el sitio equivocado.
  Ahora la lista sale de la propia integración, que la publica en cada compilación, y aquí sólo se
  informa. Lo que no cambia: sigue sin poder empeorar, y de la lista sólo se sale mejorando.

- **El botón «Volver» de la biblioteca usa ya el comando de la biblioteca, y ese comando avisa cuando
  cambia lo que puede hacerse.** El botón llamaba directo al código de la pantalla, así que la regla
  que decide cuándo «Volver» tiene sentido —sólo fuera de la lista— no se consultaba nunca. Al
  conectarlo se vio en el acto por qué esa regla necesita avisar: la ficha de película y la de serie
  existen a la vez aunque sólo se vea una, así que el botón pregunta al arrancar, le dicen que no, y
  sin aviso se queda **visible en la pantalla y apagado al tacto** para siempre. Hoy no cambia nada de
  lo que ves; lo que cambia es que el rediseño ya no puede romperlo en silencio. Y para que no vuelva,
  una comprobación lleva la lista cerrada de los siete comandos que callan a propósito, cada uno con
  la razón por la que puede callar: si aparece un octavo, o si a uno de los siete le cambia la regla,
  falla en el mismo cambio que lo introduce.

- **Continuar donde lo dejaste se comprueba ahora de verdad, y con el ratón.** Hasta hoy sólo se
  comprobaba que la aplicación *pedía* abrir en el punto guardado; ahora se comprueba que **abre ahí**,
  con el motor de vídeo real, y que los cuatro botones de las dos ofertas —continuar, empezar de
  nuevo, poner el siguiente episodio y no ponerlo— hacen lo que dicen. Cada oferta se contesta una vez
  y se retira, así que la comprobación abre el reproductor cuatro veces, como haría una persona.

- **Hacer una marca, saltarla y decidir lo que la detección propone se comprueba ahora con el ratón.**
  Los siete mandos de las tres superficies de marcas se pulsan sobre un episodio reproduciéndose de
  verdad, y lo que se comprueba en cada uno es **la fila de la base de datos**, no la lista en
  pantalla: una superficie que quitara algo de su propia lista sin guardar nada se vería igual.

- **Elegir pista de audio, subtítulos y salida de sonido se comprueba ahora con el ratón.** Los cinco
  mandos del lateral del reproductor —las dos listas de pistas, la casilla de la serie, el dispositivo
  de salida y la disposición de canales— se pulsan sobre una sesión reproduciendo vídeo de verdad, con
  una muestra que trae **dos pistas de audio y una de subtítulos** para que las listas tengan algo que
  ofrecer.

- **Cancelar una copia de seguridad a medias se comprueba ahora pulsando el botón mientras copia.**
  «Cancelar» sólo existe mientras la copia corre, y con una biblioteca de prueba la copia entera acaba
  en **51 ms**: no había ventana en la que pulsarlo. Ahora la comprobación siembra la biblioteca de
  alguien real —3.000 títulos con póster y fondo, 293 MB de imágenes— y con eso la copia tarda cuatro
  segundos, tiempo de sobra para pulsar. Se comprueba lo que de verdad importa: que al cancelar la
  pantalla dice cancelado **y que en la carpeta de copias no queda nada**, porque una copia a medias
  publicada sería peor que ninguna. La aplicación no cambió: lo que tarda es copiar lo que hay.

- **Cancelar una descarga a medias se comprueba ahora pulsando el botón mientras corre.** «Cancelar»
  sólo existe mientras algo está en marcha, y en una comprobación automática el paquete está en la
  carpeta de al lado: la descarga entera terminaba en milisegundos, antes de que hubiera nada que
  cancelar. Ahora la ejecución de prueba puede pedirle a su propia carpeta que conteste despacio, y
  el botón se pulsa con la descarga en vuelo. Lo que se ejercita es el camino de verdad —el mismo
  aviso de cancelación que produce tu clic, la misma interrupción, el mismo «Se ha cancelado. No se
  ha instalado nada.» en pantalla— y se comprueba además que en la carpeta de preparación no queda
  ningún paquete. **En tu instalación no cambia nada**: la espera vive en el archivo que una
  ejecución de prueba escribe para sí misma, no en la aplicación.

- **Descargar la actualización y confirmarla se comprueban ahora de principio a fin.** Y lo que se
  comprueba es lo de verdad: la descarga que corre es **la misma** que usa tu instalación, así que el
  hash y el tamaño que la versión promete se verifican contra lo que llega, y el archivo vive con un
  nombre provisional hasta que coinciden. Lo único que cambia en una ejecución de prueba es de dónde
  vienen los bytes: de su propia carpeta en vez de la red. Se comprueba además lo contrario —con un
  paquete que no es el prometido, la descarga lo rechaza y no deja nada—, que es lo que demuestra que
  no se aflojó ninguna comprobación para poder probar.

- **Buscar actualizaciones se comprueba ahora sin preguntarle nada a nadie.** El botón preguntaba a
  GitHub qué se ha publicado, así que cualquier comprobación automática habría hecho esa consulta
  desde la máquina que estuviera midiendo. Ahora una ejecución de prueba lee la versión que ella misma
  ha dejado descrita en su carpeta, y no se abre ninguna conexión. Lo que decide si una versión merece
  ofrecerse —que sea más nueva, que sea de tu arquitectura, que traiga hash y notas en los dos
  idiomas— sigue siendo exactamente lo mismo. Tu instalación sigue preguntando a GitHub.

- **Entregar la actualización a Windows también se comprueba ahora sin arrancar un instalador.**
  Instalar aquí significa darle el paquete a Windows y apartarse, así que cualquier comprobación
  automática arrancaba un instalador de verdad en la máquina que estaba midiendo. Una ejecución de
  prueba anota qué paquete habría entregado, como ya hacía con la carpeta de copias. De paso quedó
  donde se decide una cosa que estaba en un comentario: en un Windows sin nada registrado para
  `.msix`, la llamada no falla —simplemente no arranca nada—, y eso es un **rechazo**, no un éxito;
  para una carpeta, en cambio, que no arranque nada significa que se abrió en una ventana que ya
  tenías.

- **El permiso para buscar actualizaciones por su cuenta se comprueba pulsándolo.** Es el interruptor
  que decide si la aplicación abre una conexión que no le has pedido, así que lo que decide tiene que
  sobrevivir a cerrar la ventana: ahora la comprobación lo pulsa con el ratón y va a mirar **el
  archivo** donde queda guardado, en vez de creerse la casilla. Pulsarlo no contacta con nada.

- **La pantalla que aparece cuando tu biblioteca no abre ya se puede comprobar sin destruir la
  comprobación.** Esa pantalla ofrece dos cosas: enseñarte la carpeta donde estaría la copia de
  seguridad, y salir. Ninguna de las dos la había pulsado nunca nada que no fuera una persona, y por
  una razón que se explica sola: una comprobación que pulsara la primera abriría una ventana del
  Explorador en la máquina que está midiendo, y una que pulsara la segunda **terminaría el programa
  que está midiendo**. Ahora una ejecución de prueba —la que no es la tuya— anota lo que le habría
  entregado a Windows en vez de entregárselo, igual que ya hacía con el enlace al tráiler y con los
  diálogos de copia, y los dos botones se pulsan con el ratón comprobando qué carpeta habrían
  enseñado. Es la única pantalla de la aplicación a la que no lleva ninguna ruta —aparece sólo si tu
  biblioteca no abre—, así que hasta hoy nadie la había recorrido entera. Tu instalación no cambia en
  nada: sigue abriéndose el Explorador, y salir sigue saliendo.

- **La comprobación de que el programa se instala, se actualiza, se repara y se desinstala bien la
  hace ahora el repositorio, no una persona acordándose.** Esas cuatro cosas las hace Windows, no
  nosotros, así que sólo se pueden medir instalando de verdad en un Windows limpio; hasta hoy eso
  eran pasos escritos que alguien tenía que seguir a mano, y la medición caducaba cada vez que
  cambiaba el archivo que Windows lee para instalar. Ahora un solo comando prepara el paquete, crea
  uno de la versión siguiente para probar la actualización, lo lleva todo a una máquina virtual
  desechable, ejecuta el ciclo entero y trae el resultado. Lo medido esta vez: la asociación de
  «Abrir con» queda registrada para los ocho tipos de vídeo, la biblioteca sobrevive intacta a la
  actualización, Windows rechaza volver a una versión anterior, la reparación funciona y desinstalar
  **no se lleva tu biblioteca**.

- **Copiar la biblioteca y devolverla se prueban ahora pulsando sus botones, no llamando a su
  código.** Los dos botones que preguntan dónde guardar o de dónde leer se lo preguntan a un diálogo
  de Windows, y un diálogo no lo puede contestar ninguna comprobación automática: así que crear una
  copia, exportarla, elegir un archivo y confirmar la restauración eran cuatro cosas que nadie había
  llegado a probar como las usas tú. Ahora una ejecución de prueba —la que no es la tuya— responde
  esas dos preguntas dentro de su propia carpeta, y la comprobación exporta la biblioteca, la vuelve
  a leer y la restaura entera, mirando el disco en cada paso en vez de creerse lo que dice la
  pantalla. Tu instalación no cambia en nada: sigue abriéndose el diálogo de Windows de siempre. De
  camino quedó comprobado, por primera vez desde la aplicación completa, que la restauración
  funciona con el programa abierto y la biblioteca cargada.

- **La herramienta que firma las publicaciones no compilaba, y nadie podía enterarse hasta publicar.**
  Le faltaba el encabezado de licencia que este proyecto exige en cada archivo, y esa regla es un
  error de compilación — pero su proyecto **no estaba en la solución**, así que ninguna de las
  comprobaciones que se ejecutan en cada cambio llegaba a construirlo. El primer sitio donde habría
  saltado era una publicación real, en el paso que comprueba que la firma sirve. Ahora el proyecto
  está dentro, con lo que todas las comprobaciones que ya existían pasan a cubrirlo, y una prueba
  nueva falla en cuanto aparezca otro proyecto fuera.
- **Cada versión publicada lleva al lado el código fuente de las bibliotecas que reproduce el vídeo.**
  Las licencias de LibVLC y de sus complementos obligan a que ese código esté a tu alcance, y la forma
  más clara de cumplirlo es la que ellas mismas describen: ofrecerlo desde el mismo sitio del que te
  descargas el programa. Así que la versión adjunta `vlc-3.0.23.tar.xz` —comprobado contra la huella
  que publica VideoLAN— y el de LibVLCSharp, además de la oferta por escrito que ya existía, que se
  mantiene para quien reciba el programa por otra vía. De paso se corrigió que el aviso nombraba una
  versión de VLC que no existe: el `3.0.23.1` del paquete es su propia numeración, y el código fuente
  se publica como `3.0.23`.
- **Y si el tráiler no lo tienes, la ficha te lo abre en el navegador.** Cuando TMDB conoce uno,
  aparece un botón en la ficha de la película o de la serie que lo abre en tu navegador de siempre.
  **La aplicación no se conecta a YouTube**: le pasa la dirección a Windows y quien entra es tu
  navegador, con tus ajustes y tus extensiones — por eso la lista de conexiones que esta aplicación
  declara no crece ni un host. Lo que se guarda es la clave del vídeo y nunca una dirección, y sólo
  una clave con la forma exacta que usa YouTube llega a componer un enlace: cualquier otra cosa no
  ofrece botón. El dato viaja en la misma consulta de metadatos que ya se hacía, así que no hay una
  petición más. Dentro de la aplicación **no se reproduce**, y no es una limitación técnica: hacerlo
  incumpliría los términos de YouTube.
- **Si tienes el tráiler junto a la película, la ficha lo reproduce.** Sirve la convención que ya
  usan Plex, Jellyfin y Kodi: un archivo `<película>-trailer.<extensión>` al lado, o una carpeta
  `Trailers` dentro de la de la película. El botón sólo aparece cuando ese archivo existe de verdad,
  se abre igual que un vídeo que arrastras a la aplicación —sin añadirlo a tu biblioteca— y sólo
  acepta los contenedores que esta versión reproduce. **No se descarga nada**: si tu tráiler está en
  YouTube, eso no es un archivo tuyo y no se reproduce aquí dentro.
- **La ficha de una película o una serie muestra su sinopsis.** El resumen ya se descargaba, se
  guardaba, se fusionaba respetando lo que hubieras bloqueado y se podía editar a mano — pero no se
  leía en ninguna parte: podías escribirlo en el editor y no encontrarlo después. Ahora aparece en
  las dos fichas, envuelto y acotado para que no empuje a las versiones o a las temporadas fuera de
  la pantalla, y anunciado con su nombre para quien use un lector de pantalla. Un resumen en blanco
  no ocupa sitio. No se abre ninguna conexión nueva: el texto ya estaba en tu disco.

- **Renombrar archivos no proponía ningún nombre, y ahora se sabe por qué.** La vista previa de
  renombrado se abre, enseña su casilla de confirmación y sus dos botones, y **no ofrece nunca una
  sola operación**: la aplicación pedía renombrar cada archivo al nombre que ya tenía, así que la
  comprobación de seguridad lo descartaba, correctamente, como «sin cambios». Falta la pieza que
  decide cómo debe llamarse un archivo a partir de su ficha, y eso es una decisión de producto, no un
  cable suelto: queda anotada en vez de inventada. Nada de tu disco se tocó ni se toca.

- **Los mandos del reproductor ya se pueden usar con el ratón.** Tres cosas distintas los tenían
  inservibles, y las tres se veían igual: un botón en pantalla que no hacía nada. El aviso de estado
  del vídeo —el que dice si va por hardware o si el rango es estándar— **ocupaba toda la superficie
  del reproductor**, opaco, encima del vídeo y encima de la barra de mandos, así que se tragaba cada
  clic; ahora es un distintivo en una esquina. Los botones no volvían a comprobar si podían usarse,
  de modo que **pausabas con el ratón y ya no podías reanudar**. Y el deslizador de volumen se movía
  sin llegar a la reproducción: cambiaba el número en pantalla y nada que pudieras oír. Todo esto
  seguía funcionando con el teclado, que es exactamente por qué nadie lo había visto.

- **Windows ya describe la aplicación en tu idioma antes de que la abras.** El paquete declaraba
  español e inglés desde el principio, pero su descripción era **una sola frase con una barra en
  medio** —«Biblioteca y reproductor de vídeo local / Local video library and player»—, que Windows
  enseñaba igual a todo el mundo: declarar un idioma no traduce nada por sí solo. Ahora el paquete
  lleva un texto por idioma y Windows elige el tuyo. El texto no está escrito a mano en ninguna
  parte: sale del primer párrafo del README correspondiente, que es de donde ya salía el de winget,
  así que las dos vías de instalación dicen exactamente lo mismo. El nombre no se traduce, a
  propósito: «AP Reelume» es el nombre del producto en los dos idiomas.

- **Una puerta cuenta cuántos botones de la aplicación se pulsan de verdad con el ratón.** Hasta
  ahora el recorrido automático conducía la aplicación con el teclado y **dos** de sus 129 controles
  de mando llegaban a recibir un clic; el resto podía estar visible, activo e incapaz de hacer nada
  sin que nada avisara — que es exactamente lo que le pasó a un par de botones que sobrevivieron a
  una auditoría entera. Ahora hay un inventario de los 129, una lista de lo que aún no se pulsa
  **con el motivo escrito al lado**, y un trinquete: esa lista sólo puede encoger. La primera tanda
  cubre la biblioteca y la ficha —filtrar, ordenar, aplicar, abrir una entrada, volver, marcar
  visto, favorito, ver más tarde, puntuar y reproducir—, y un control sólo cuenta cuando se ha
  pulsado de verdad, se ha comprobado **lo que cambió** y un clic al lado no ha hecho nada. Lo que
  se cuenta se anota mientras se ejecuta, no leyendo el código de las pruebas. Al estrenarla
  aparecieron **tres** defectos del propio arnés: no sabía distinguir dos botones Atrás idénticos y
  sólo uno visible; el mismo botón se pulsaba con un nombre y se contaba pendiente con otro; y el
  clic de comprobación que debía no hacer nada **apagaba el botón de favorito** de la fila de
  encima, sin que nada avisara.

- **La puerta de cobertura ya vigila código que no es nuevo.** Sólo miraba los archivos que
  aparecían por primera vez, así que uno antiguo que empeorase no lo miraba nadie — y no es una
  hipótesis: al re-medir los tres archivos que arrastraban deuda, dos estaban igual que hace un día
  y el tercero había **retrocedido** quince puntos, porque una limpieza anterior le quitó código y
  se llevó por delante justo las partes que sí estaban probadas. Nada avisó. Ahora hay una lista
  explícita de archivos vigilados que se miden siempre, cada uno con el listón que cumple hoy: si
  baja, la verificación falla; si sube, **también** falla hasta que se anota el nuevo listón, de
  modo que una deuda saldada no puede volver en silencio. Dos de los tres quedaron cubiertos por
  completo por el camino; el tercero queda vigilado con su nombre y su número a la vista en cada
  ejecución.
- **La última deuda de cobertura está saldada.** Al que reconcilia lo que un escaneo encuentra —el
  que hace que un vídeo cambiado de carpeta siga siendo la misma ficha— le estaba probado el camino
  feliz y no sus decisiones: qué se niega a tocar, qué cuenta como fallo sin costarle el escaneo al
  resto, y qué guarda. Ahora están probadas una a una, y el archivo pasa de 86,73 % de líneas y
  76,00 % de ramas al 100 % de las dos, con el listón subido detrás. Medir la lista de huecos antes
  de escribir nada la recortó en un tercio —cinco de los puntos anotados leyendo el código ya
  estaban cubiertos— y destapó uno que ninguna lectura habría encontrado: el contador de archivos
  intentados no lo leía ninguna prueba.
- **La ventana ya no espera a que la base de datos esté lista para existir.** Al arrancar, la
  aplicación pone al día la base —y comprueba su integridad si eso reescribió el archivo— y hasta
  ahora hacía ese trabajo en el mismo hilo que dibuja: no había nada en pantalla hasta que
  terminaba, y en una biblioteca grande la comprobación crece con el archivo. Ahora la ventana
  aparece de inmediato con una pantalla de inicio, el trabajo ocurre aparte, y al acabar su sitio lo
  ocupa la biblioteca o, si la base no se puede abrir, la misma pantalla de recuperación de siempre:
  cambia cuándo se decide, no qué se decide. No hay barra de progreso, y es a propósito — nada en
  ese momento sabe cuánto falta, y una barra que se mueve sin significar nada es una imagen de
  progreso en lugar de progreso. La medición que lo decidió: escribir «espera» en el código no basta
  para que el hilo quede libre, así que se comprobó, y no quedaba libre ni un milisegundo.
- **La verificación del paquete ya dice cuánto tardó la ventana en aparecer, no sólo que apareció.**
  Anotaba un sí o un no, y ese sí cubría por igual un arranque instantáneo y uno que llegó justo
  antes de agotar el plazo de noventa segundos, así que una degradación no se veía hasta ser un
  fallo. Ahora informa el tiempo —desde antes de arrancar el proceso, porque el arranque también se
  espera— en los tres ciclos que abren la aplicación, y una prueba lo exige y lo acota contra ese
  plazo. Tres cifras y no una, porque un número solo no dice si el que empeoró fue el arranque o la
  máquina. La primera medición dejó dos cosas claras: los cinco ciclos migran una base nueva, no
  sólo el primero como estaba escrito, y el primer arranque tampoco es el más lento de los tres.
- **Catalogar y reproducir comparten el motor nativo, en lugar de arrancar uno cada uno.** Leer los
  datos técnicos de un archivo levantaba su propia instancia de LibVLC con las mismas opciones que la
  de reproducción, así que un proceso que catalogaba y reproducía mantenía dos motores nativos
  abiertos — y el contador que dice «uno por juego de opciones» no podía ver el segundo. Ahora hay un
  dueño y una prueba que falla si aparece otro. De paso desaparece la segunda cola de liberación de
  medios: la que tenía el sondeo no protegía su propio cierre, de modo que un único fallo al liberar
  habría dejado su trabajador muerto para siempre y todo lo catalogado después se habría ido
  filtrando sin que nada avisara. La que queda ya vive protegida contra eso.
- **El compilador vigila que un número guardado no dependa del idioma del sistema.** Un tamaño, una
  fecha o una comparación escritos con las reglas del idioma de quien usa el programa se leen mal en
  otra máquina, y ese error no avisa: aparece cuando ya está guardado. Tres comprobaciones que venían
  apagadas quedan encendidas como error. No hubo nada que corregir —se midió antes: cero casos en
  todo el proyecto—, y ese cero se comprobó compilando una violación deliberada de cada regla para
  saber que las comprobaciones se estaban ejecutando de verdad.
- **El actualizador se presenta con la versión que de verdad tienes.** Al preguntar si hay una
  versión nueva se identificaba como «1.0», un número escrito a mano que nunca existió: la versión
  declarada es 0.1.0. Ahora sale del propio programa, y una prueba la compara contra el único sitio
  donde este proyecto declara su versión, leyendo la cabecera que sale de verdad y no la constante
  del código.
- **Las pruebas encuentran la raíz del proyecto en un solo sitio.** El mismo recorrido hacia arriba
  estaba pegado en cincuenta y nueve archivos, y ni siquiera era el mismo: dos de ellos buscaban un
  documento y el resto el archivo de solución, así que el repositorio tenía dos definiciones de su
  propia raíz. Ahora hay una, compartida, y una prueba que falla si alguien vuelve a escribir la
  suya. Ochocientas líneas menos.
- **La prueba que vigila que ninguna pantalla quede inalcanzable ya no se cree un comentario.**
  Buscaba el nombre de la vista en el texto de los archivos, así que una referencia **comentada**
  contaba como si la pantalla se pudiera abrir: justo la pantalla huérfana que esa prueba existe para
  encontrar podía esconderse detrás de un comentario y la puerta seguía en verde. Ahora se quitan los
  comentarios antes de buscar. Se comprobó primero si algo se estaba escondiendo ya de esa forma —no
  lo había—, y el recorte se hizo hacia el lado seguro: quitar de más pierde una referencia y produce
  un aviso ruidoso, nunca un permiso silencioso.
- **Y el reproductor tampoco guarda ya la suya.** Quedaba una tercera cola, la del propio motor de
  vídeo, con el mismo desecho sin proteger: un solo fallo al liberar habría acabado con su trabajador
  y todo lo abierto a partir de ahí se habría filtrado en silencio. Ahora hay **una** cola para todo
  el proceso, la que ya vive protegida. Cerrar el reproductor espera a que sus vídeos estén sueltos
  antes de devolverlo —ese orden es lo que impide que la destrucción nativa se lleve el proceso por
  delante— y esa espera tiene techo, así que una biblioteca ocupada catalogando no puede retener una
  salida. El segundo de reposo antes de soltar un vídeo, que es el número que dejó de hacer crashear,
  no se ha tocado.
- **Un arranque que no llega a pintar ya deja diagnóstico en vez de un código de salida mudo.** La
  verificación mata el proceso cuando agota el plazo de la ventana, y lo único que quedaba escrito
  era ese matarile —`exit code -1`—, que no habla del arranque. Ahora, **antes** de matarlo, la
  verificación anota si el proceso seguía vivo, cuánto procesador había gastado y en cuántos hilos
  —que es lo que separa girar de esperar—, si la base de datos existe y por cuántas migraciones ha
  pasado, y qué hay en la carpeta de datos. Todo eso va en la misma línea que CI imprime al fallar.
  Ninguna de esas lecturas puede romper nada: un diagnóstico que falla sustituiría al fallo que venía
  a explicar, así que lo que salga mal se cuenta dentro de la propia frase. No se ha subido el plazo
  de noventa segundos, que sería convertir la única señal que hay en silencio.
- **Al salir, la aplicación suelta lo que había tomado.** El reproductor nativo, la base de datos, el
  icono de bandeja, los registros de teclas multimedia y los clientes de red vivían en un campo
  estático que nada liberaba nunca: el proceso terminaba y le dejaba a Windows recoger lo suyo, que no
  es cerrar sino confiar. Ahora la aplicación es un objeto con dueño y se libera al salir, venga la
  salida de la ventana o de la bandeja. Tiene un segundo efecto que se nota en las pruebas más que en
  la pantalla: dos aplicaciones pueden existir a la vez en un proceso sin verse la una a la otra, y la
  cláusula que obligaba a las dos suites de recorrido completo a ejecutarse en fila se retiró — que es
  la manera de comprobar que la propiedad es real y no sólo más ordenada. Lo que sigue sin liberarse,
  a propósito y documentado, es la instancia nativa de LibVLC: crearla y destruirla repetidamente es
  un modo de fallo conocido, así que vive lo que vive el proceso.
- **El registro de la aplicación deja de ser una lista de trescientas líneas.** Todo lo que la
  aplicación monta se declaraba en una sola cadena, y averiguar de qué dependía una pieza obligaba a
  leerla entera. Ahora hay ocho módulos por área —datos, reproducción, personalización, biblioteca,
  ajustes y copias, actualizaciones, apariencia e identificación— cada uno lo bastante corto como para
  que una pieza que falta se vea. El comportamiento es el mismo: lo garantizan las pruebas que
  recorren la aplicación ensamblada de verdad.
- **La lógica que elige qué copia ofrecerte cuando la biblioteca no abre ya se puede medir.** Vivía
  dentro del archivo de composición, donde la única forma de alcanzarla era hacer fallar una base de
  datos real; decide qué se te ofrece el peor día que tiene tu biblioteca. Ahora es una pieza aparte
  con cinco pruebas, entre ellas las dos que antes nadie comprobaba: que una copia que el registro
  nombra pero que no está en el disco no se ofrezca, y que la copia de otra base de datos no se
  confunda con la tuya.

### Legal

- **Créditos muestra el logotipo de TMDB, no sólo su nombre.** Sus términos piden identificar el uso
  de TMDB con su marca, menos prominente que la del propio producto; era el último punto de esos
  términos que quedaba abierto. El archivo es el que TMDB publica y se puede comprobar que lo es: la
  huella SHA-256 que ellos incrustan en la dirección del recurso coincide con la del archivo
  versionado, y una prueba las compara. Lo que se dibuja es su vector, no una imitación —otra prueba
  contrasta la geometría de la vista contra la del archivo carácter a carácter—, y se dibuja a 16 px
  frente a los 24 px del nombre del producto. La especificación decía 48 px para ese nombre; se midió
  y no existía, así que se corrigió la cifra en vez de heredarla. Lleva texto alternativo en los dos
  idiomas y no es un enlace.
- **El texto de cada licencia ajena viaja dentro del paquete.** Nombrar un componente y su licencia no
  es entregar la licencia, y varias lo exigen: la LGPL-2.1, la GPL-2.0 y la Apache-2.0 piden acompañar
  una copia con el binario, y la MIT y la BSD-3-Clause, reproducir su aviso de copyright. El paquete de
  VideoLAN no trae ninguno, así que nadie los aportaba. La carpeta `licenses/` de los dos artefactos
  lleva ahora los cinco textos íntegros y los avisos de ANGLE, Skia, HarfBuzz, BouncyCastle,
  SQLitePCLRaw, SQLite y VideoLAN —incluido el archivo de Microsoft que cubre las veintitantas
  bibliotecas que Skia y HarfBuzz llevan dentro, de freetype a zlib, que hasta ahora no aparecían en
  ningún sitio—. Los avisos que un paquete publica se copian de él y una prueba los compara byte a byte
  contra el paquete que la compilación consumió, de modo que una subida de versión que cambie un aviso
  se pone en rojo en vez de distribuir en silencio el aviso anterior. Los textos canónicos se tomaron
  de una fuente que ya los distribuía y se contrastaron con una segunda copia independiente; el de la
  GPL-2.0 salió del propio árbol de VLC, que es la licencia que obliga a sus complementos.
- **Cada archivo fuente dice bajo qué licencia está.** La licencia vivía solo en `LICENSE`, y una
  licencia que solo vive ahí deja de estar unida al archivo en cuanto alguien lo copia fuera del
  árbol. Los 556 archivos de código, los 51 de interfaz y los 17 de compilación llevan ahora su
  cabecera `SPDX-License-Identifier: GPL-3.0-or-later` junto al titular del copyright, y la puerta de
  formato que ya se ejecutaba rechaza un archivo nuevo que llegue sin ella.
- **Los avisos de terceros nombran lo que el paquete lleva de verdad.** Listaban ocho componentes —
  los que alguien recordaba haber pedido— mientras el artefacto transportaba treinta, entre ellos
  ANGLE bajo BSD-3-Clause, Skia, HarfBuzz, BouncyCastle y el propio motor de .NET, todos con avisos
  que deben viajar con el binario. Ahora la lista sale del inventario real de la compilación, explica
  aparte el motor autocontenido y los complementos de VideoLAN, y una prueba impide que una
  dependencia entre en el artefacto sin aparecer en los avisos de los dos idiomas.
- **La atribución de TMDB dice la frase que TMDB exige.** Mostraba un resumen de la frase obligatoria;
  ahora dice la exigida —«usa TMDB y las API de TMDB, pero no está avalado, certificado ni aprobado de
  ningún otro modo»— en Créditos, en el aviso y en los dos README, con una prueba que la fija palabra
  por palabra en ambos idiomas.
- **Nada de TMDB se conserva más de seis meses.** Sus términos lo prohíben y la caducidad de la caché
  no lo garantizaba: cuando la red fallaba o usted quitaba el token, el programa seguía sirviendo la
  copia guardada por vieja que fuera. Ahora hay un suelo duro de 180 días; pasado ese plazo la entrada
  no se sirve y se borra del disco.
- **Los complementos de VideoLAN quedan confirmados como compatibles.** Estaba anotado como duda para
  un dictamen: si alguno fuera `GPL-2.0-only` chocaría con la licencia de este programa. Se comprobó
  en la fuente y llevan la cláusula «o cualquier versión posterior», así que encajan. En el mismo
  repaso apareció lo contrario de una buena noticia: el paquete de VideoLAN no trae ningún archivo de
  licencia, y el artefacto tampoco incluía el texto de las licencias ajenas, que varias de ellas
  exigen acompañar. Ese hueco es el que cierra la primera entrada de esta sección.
- **Un estado legal que dice también lo que falta.** Una página nueva en los dos idiomas reúne la
  licencia, el descargo de garantía, los terceros, los términos de TMDB y de GitHub y la nota de
  exportación por la criptografía que el paquete lleva, y nombra sin adornos los cinco puntos que
  siguen siendo del propietario, entre ellos el dictamen jurídico profesional.

### Seguridad

- **Reproducir fuera de la aplicación sólo abre vídeo.** El botón que entrega un archivo al
  reproductor que tengas registrado en Windows confiaba en que quien lo llamara ya hubiese filtrado
  por la lista de contenedores. Se midió qué pasaba si no: de cinco tipos de archivo que la biblioteca
  no cataloga, **tres abrieron su manejador**, entre ellos un `.ps1` y un archivo sin extensión. Ahora
  la comprobación está donde se hace la llamada, contra la misma lista que decide qué entra en tu
  biblioteca.
- **Restaurar una copia aplica sus propios límites de tamaño.** Los topes —512 MB por archivo, 2 GB en
  total— los ponía el paso de inspección, que es un paso que hay que acordarse de dar; desempaquetar
  ahora los aplica también. De paso se comprobó, forjando un archivo que declara un byte donde guarda
  una base de datos entera, que ninguna entrada puede entregar más de lo que declara: el riesgo estaba
  en la declaración, no en la copia, y ahí es donde se corta.
- **La integración continua ya no puede salir a una máquina personal.** Cuando el repositorio era
  privado, la verificación podía enrutarse a un runner propio a través de una variable del
  repositorio; ahora que es público, esa puerta era una invitación a que el CI de un pull request
  ajeno se ejecutara en la máquina del propietario en cuanto la variable reapareciera. El flujo de
  trabajo ya no la consulta y corre siempre en los runners hospedados, que son gratuitos en los
  repositorios públicos.
- **Las tres acciones de la tubería suben a su versión mayor vigente.** `checkout` 7.0.1,
  `setup-dotnet` 6.0.0 y `upload-artifact` 7.0.1, con cada SHA comprobado contra la etiqueta que dice
  representar. El cambio de ruptura de `checkout` endurece precisamente los disparadores que este
  proyecto no usa, y el de `upload-artifact` sólo altera el nombrado cuando se desactiva el
  empaquetado, que aquí no se desactiva.
- **La única herramienta externa que sella la publicación queda fijada por versión.** Todas las
  acciones ya iban ancladas por SHA y NuGet en modo bloqueado, pero ffmpeg se instalaba desde el
  canal de la comunidad tomando siempre la última versión: era el único ejecutable de terceros sin
  fijar en la máquina que empaqueta y firma. Ahora se instala una versión concreta que sólo se mueve
  por una edición deliberada, igual que los SHA de las acciones.
- **Las huellas publicadas van firmadas y el actualizador exige la firma.** El hash con el que se
  comprueba una actualización viajaba en la misma respuesta sin firmar que el paquete al que avala:
  quien alterase la respuesta podía alterar ambos a la vez. Ahora cada publicación firma sus
  huellas con una clave minisign cuya mitad pública viaja dentro del binario; el actualizador
  verifica esa firma antes de creer ningún hash, lee el hash únicamente del bloque firmado, y una
  versión sin firma —o con las líneas alteradas— se rechaza diciendo por qué. La clave privada vive
  fuera del repositorio, la tubería de publicación firma y se niega a publicar sin firma, y la
  declaración de privacidad explica qué prueba esta capa y qué sigue sin probar (la firma de código
  de Windows es otra capa y sigue pendiente de su decisión económica).
- **El actualizador sólo acepta bytes de los dominios declarados, en cada salto.** Las
  redirecciones ya exigían HTTPS pero aceptaban cualquier destino; ahora cada salto tiene que
  quedar dentro de la lista que la declaración de privacidad publica (GitHub y su almacenamiento),
  un salto fuera se rechaza con su motivo en pantalla, y la declaración nombra por fin el dominio
  de almacenamiento al que GitHub redirige — con una prueba que impide que código y promesa vuelvan
  a divergir. El arte de los títulos queda igualmente limitado a su dominio declarado.
- **Toda respuesta de red tiene techo.** Los metadatos de una versión se cortan en un megabyte, el
  paquete se corta en cuanto llegan más bytes de los que la versión declaró (y el parcial
  envenenado se borra), y un póster de más de diez megabytes se rehúsa a medio camino conservando
  el arte anterior.

### Corregido

- **Los mandos del reproductor ya no se salen de la ventana.** En una ventana estrecha —900 píxeles
  de ancho, que es lo más pequeña que la aplicación permite dejarla— la fila del transporte terminaba
  74 píxeles pasado el borde derecho: el control de volumen, el botón de silencio y el indicador de
  velocidad quedaban fuera de la pantalla y no había forma de pulsarlos. Ahora la fila se reparte en
  varias líneas cuando no cabe en una, como ya hacían las demás.

- **La escena que probaba cancelar una copia acusaba a la máquina lenta.** Comparaba el tiempo de sus
  dos pulsaciones contra una duración medida en una sola máquina, así que un runner más lento la ponía
  roja sin que nada estuviera mal. Lo que el reloj intentaba deducir —si la copia había terminado
  sola— lo dice la propia pantalla, y ahora la escena lo observa en vez de inferirlo.

- **Cambiar de versión dos veces seguidas ya no se salta la pregunta ni deja en cero por dónde ibas.**
  La fila de la otra versión seguía pulsable mientras su propio cambio estaba en marcha, así que un
  doble clic —o un segundo clic mientras la pregunta ya estaba en pantalla— lanzaba un segundo cambio.
  Y todo cambio guarda antes la posición del reproductor: si la sesión acababa de abrirse y el motor
  aún respondía cero, ese cero quedaba por debajo del punto desde el que se ofrece reanudar, así que
  el segundo cambio decidía que no había nada que llevarse, abría la otra versión **sin preguntar** y
  dejaba la posición guardada en cero. Ahora la fila se apaga mientras su cambio está en marcha
  —igual que el salto de la barra de transporte se apaga mientras busca— **y mientras su pregunta
  sigue en pantalla**, que es el hueco más largo de los dos: un cambio que pregunta termina en el
  acto y se queda esperando a que contestes, así que la fila volvía a estar viva justo debajo de la
  pregunta. Al contestarla, la fila vuelve.

- **El tráiler que guardas junto a una película sólo aparecía si esa película estaba duplicada.** La
  ficha buscaba el archivo del tráiler partiendo del grupo de versiones, y un título sin copias no
  tiene grupo: el archivo estaba ahí al lado y el botón no salía nunca. Ahora se busca a partir de la
  película, tenga copias o no.

- **Abrir un vídeo desde el Explorador lo reproducía sin enseñarlo.** El archivo empezaba a sonar y la
  aplicación se quedaba en la pantalla de inicio: sin imagen, sin controles y sin forma de pararlo, y
  el aviso de «esto no está en tu biblioteca» —con su oferta de añadir la carpeta— no llegaba a
  aparecer nunca. Ahora una activación abre el reproductor como cualquier otra reproducción, y un
  archivo que no se puede decodificar te ofrece reintentar o abrirlo con otra aplicación en vez de
  dejarte una pantalla vacía. Lo mismo valía para el tráiler local.

- **Cerrar la aplicación con un vídeo abierto reventaba el apagado.** Terminar los enganches de una
  sesión no es pararla: el medio seguía abierto, así que el desmontaje intentaba parar un reproductor
  que ya se había desechado y saltaba una excepción. Ahora la sesión se para antes que los servicios
  que la alimentaban. Nadie lo veía porque toda comprobación cerraba el reproductor primero, que es
  justo lo que quien cierra la ventana a media película no hace.

- **Cambiar de versión perdía el punto que acababas de aceptar.** La aplicación preguntaba qué hacer
  con tu progreso, calculaba el segundo equivalente en la otra versión y lo guardaba — y después abría
  esa versión **desde el principio** y escribía ese cero encima de lo que acababa de guardar: medido,
  el cabezal en 0, 0, 0, 1, 1, 2 sobre un progreso trasladado de 2:01. Ahora quien abre el reproductor
  sabiendo dónde quiere abrirlo manda, y «Empezar de nuevo» empieza de verdad por el principio.

- **La pregunta del cambio de versión se dibujaba sobre toda la pantalla.** Como la oferta de
  continuar y la del siguiente episodio antes que ella, se estiraba al escenario entero del reproductor
  —1280×1400 medidos— con sus tres respuestas en la esquina. Ahora tiene su tamaño, su fondo y su
  borde, y sus respuestas se reordenan si no caben.

- **El botón para cambiar de versión se dibujaba fuera de la ventana.** En la fila de cada versión
  alternativa, la etiqueta de calidad empujaba el botón tan a la derecha como largo fuera su texto:
  medido a 74 píxeles fuera de una ventana de 1600, sin nada que desplazar. Era una versión a la que
  no se podía cambiar con el ratón. La fila se reparte ahora entre una etiqueta que se pliega y un
  botón que conserva su sitio.

- **La oferta de continuar y la del siguiente episodio se dibujaban sobre toda la pantalla.** Las dos
  se estiraban al escenario entero del reproductor —1280×1400 medidos— con sus botones en la esquina,
  en vez de dibujarse como la tarjeta que son. Ahora tienen su tamaño, su fondo y su borde, y sus
  botones se reordenan si no caben.

- **«Borrar» del editor de marcas se dibujaba fuera de la pantalla.** En la columna del reproductor
  cabía «Guardar» y el botón de al lado quedaba **once píxeles fuera de la ventana**, sin nada que
  desplazar: no había forma de borrar una marca con el ratón. Le pasaba lo mismo a los tres botones de
  las detecciones propuestas. Los cuatro grupos se reordenan ahora en varias líneas cuando no caben.

- **El estilo de subtítulos que eliges ya no se pierde al cerrar.** El tamaño, la tipografía, la
  opacidad del fondo y el grosor del contorno se guardaban **en ninguna parte**: cambiabas los cuatro
  mandos, cerrabas la ventana y volvías a empezar de cero. Ahora cada cambio se guarda al hacerlo y
  vuelve al abrir. **Lo que todavía falta, y se dice en voz alta:** ese estilo llega a la base de
  datos pero **aún no a la imagen** — el motor de vídeo recibe su dibujado de subtítulos al arrancar,
  y conectarlo es trabajo aparte que sólo se puede confirmar mirando una pantalla.

- **«Recordar para esta serie» ya se puede marcar.** La casilla que hace que tu elección de idioma o
  de subtítulos valga para **toda la serie** y no sólo para ese episodio estaba **deshabilitada
  siempre**: la aplicación nunca le decía a qué serie pertenece lo que estás viendo. Era peor que un
  botón muerto, porque al abrir un episodio **sí** buscaba una preferencia guardada para la serie —
  una que nada podía guardar—. Ahora se marca, y lo que elijas después queda guardado para el programa
  entero, así que el siguiente episodio empieza como dejaste el anterior.

- **La verificación automática ya pulsa los ajustes enteros con el ratón.** La página de ajustes es
  más alta que la ventana, y el recorrido que conduce la aplicación construida sólo sabía bajar por
  ella: una vez pulsado algo, todo lo que quedaba más arriba dejaba de alcanzarse. Ahora vuelve al
  principio de la página y sólo se desplaza cuando hace falta, así que cada pulsación vale por sí
  sola. Quedan comprobados con el ratón los veinte controles de ajustes: los tres temas, los dos
  idiomas, la vigilancia de carpetas locales, la detección de segmentos, la bandeja y el cierre a
  ella, el arranque con Windows —pedido, denegado, vuelto a pedir y concedido—, el consentimiento de
  diagnóstico, el refresco automático, la previsualización, la exportación del informe, el
  interruptor de recomendaciones, su umbral y su recálculo, y la restauración de los atajos. De cada
  uno se comprueba el efecto de verdad, no la casilla: la exportación se lee del archivo escrito en
  el disco y el arranque, de la entrada del registro.
- **Una copia de la aplicación que guarda sus datos aparte guarda también aparte su arranque con
  Windows.** Antes, cualquier copia escribía su entrada de inicio de sesión en el mismo sitio, así que
  una comprobación automática no podía siquiera probar ese botón sin dejar registrada en tu equipo la
  copia que estaba probando. Tu instalación normal no cambia: sigue escribiendo donde Windows lo lee.
- **El botón «Buscar» de la bandeja de revisión ya se puede pulsar, y ahora busca de verdad.** Tenía
  dos averías, una encima de otra: escribir en la caja **no lo habilitaba** —seguía apagado por mucho
  que escribieras—, y aunque lo hubiera estado, pulsarlo no habría hecho nada, porque lo que pedía no
  lo escuchaba nadie. Ahora buscas escribiendo el título y el año, con una ficha de la lista
  seleccionada, y la aplicación busca **para ese archivo**: si lo que encuentra no deja lugar a dudas,
  lo aplica sin preguntarte; si lo deja, te lo pone en la bandeja para que decidas tú. El botón está
  disponible cuando hay las dos cosas —algo escrito y una ficha elegida—, porque sin ficha no hay
  archivo sobre el que buscar.
- **Ya se comprueba con el ratón que Aceptar y Rechazar deciden la ficha que has elegido.** No es una
  frase de manual: la comprobación automática destapó que la propia verificación podía decidir una
  ficha distinta de la pulsada, y ahora se comprueba **cuál** queda aceptada o rechazada, leyéndolo del
  catálogo y no de la pantalla. También se pulsa «Cargar más», que trae el resto de la lista.
- **El botón que confirma un archivo movido se salía de la pantalla, así que no había manera de
  pulsarlo.** Cuando la aplicación encuentra un archivo que puede ser uno que ya tenías, te enseña la
  ruta de cada candidato y un botón «Es el mismo, reasignar» al lado. La ruta se colocaba a lo ancho
  sin plegarse nunca, y con una ruta de una biblioteca de verdad —las tuyas lo son— empujaba el botón
  fuera de la ventana, sin nada que desplazar para alcanzarlo: **la reasignación no se podía confirmar
  en absoluto**. Ahora la ruta se pliega en el espacio que hay y el botón se queda a la vista.
- **Y cuando hay varios candidatos, ya se distingue cuál confirma cada botón.** La aplicación sólo te
  pregunta cuando **dos** fichas de tu catálogo podrían ser ese archivo, así que el botón aparece
  repetido; los dos se llamaban igual, y elegir mal no es un detalle: decide cuál de tus fichas
  conserva su progreso y tus decisiones bajo la ruta nueva. Cada botón dice ahora a qué ruta
  pertenece, también para quien use un lector de pantalla. Las dos decisiones —«es el mismo» y «es un
  archivo nuevo»— quedan comprobadas con el ratón, leyendo del catálogo cuál ficha quedó decidida.
- **El botón «Retirar» de una carpeta de la biblioteca se salía de la pantalla.** La ruta de la
  carpeta se escribía a lo ancho sin plegarse, así que con las rutas de verdad —las tuyas— el botón
  quedaba fuera de la ventana y **no había forma de quitar una carpeta**. Ahora la ruta se pliega y el
  botón se queda a la vista. De paso, esa pantalla —la primera que ves al instalar— queda comprobada
  entera con el ratón: las tres clases de carpeta, añadirla, el permiso para el primer escaneo, y
  retirarla **cancelando y confirmando**, comprobando que la carpeta sigue en tu disco.
- **El botón «Continuar» de la pantalla de inicio no hacía nada.** Es la acción principal de toda la
  aplicación: la pantalla te ofrecía seguir con lo último que dejaste a medias, el botón se activaba
  solo porque había algo a lo que volver, y al pulsarlo **no pasaba nada**. Ahora abre la sesión con
  la misma copia de la que salió tu marca de tiempo y en el punto donde la dejaste.
- **Y dos de los tres botones del reproductor estaban fuera de la pantalla.** «Mini reproductor» y
  «Pantalla completa» se dibujaban más allá del borde de la ventana, y no era cuestión de agrandarla:
  la columna donde viven mide lo que mide, así que **no cabían a ningún tamaño**. Ahora los tres se
  colocan en las líneas que hagan falta y se ven siempre.
- **La pantalla de revisión queda vigilada entera, también cuando algo va mal.** Es donde corriges lo
  que la lectura automática no acertó, así que ahora se comprueba línea por línea y camino por camino:
  qué pasa si otro decidió antes que tú, si la ficha ya no existe, si no has elegido ninguna. Con ello
  se retiró una comprobación que **no podía fallar pero sí dejar de valer** —la que ya había apagado
  el botón «Buscar» para siempre—, para que esa avería no tenga camino de vuelta.
- **Ya se comprueba con el ratón que elegir qué copia se reproduce guarda tu elección.** Cuando tienes
  dos copias del mismo título, la comparación te deja marcar cuál quieres; la verificación pulsa la
  que **no** es la que se reproduciría de todos modos y lo lee de tu catálogo, no de la pantalla, para
  que «la copia mejor» y «la copia que elegiste» no se confundan.
- **Ya se comprueba a dónde lleva «Ver el tráiler», en la ficha de película y en la de serie.** El
  botón abre el navegador que uses, y por eso ninguna comprobación automática podía pulsarlo: habría
  abierto ventanas en la máquina que estaba comprobando. Ahora una copia que guarda sus datos aparte
  **anota la dirección** en vez de abrirla, así que la verificación pulsa el botón de las dos fichas y
  lee la dirección exacta que se habría abierto — la del tráiler de esa ficha y de ninguna otra. Tu
  instalación normal sigue abriendo el navegador igual que antes, y lo que se puede abrir no ha
  cambiado: sólo `https` y sólo hacia el sitio que la dirección dice.
- **El renombrado ya renombra.** Proponía el nombre que el archivo ya tenía, así que la
  previsualización salía siempre vacía y los botones de Renombrar y Deshacer no podían hacer nada por
  mucho que se pulsaran. Ahora propone el nombre que la ficha merece, con el convenio que leen Plex,
  Jellyfin y Kodi: `Título (Año).ext` para una película y `Serie (Año) - SxxEyy - Título.ext` para un
  episodio. Sobre las doce formas de nombre que el catálogo reconoce, ocho reciben un nombre distinto
  del actual y la que ya seguía el convenio se deja en paz. Cuando nadie ha identificado la ficha y el
  nombre no se deja leer con seguridad, no se propone nada: un renombrado no adivina. Lo que decide
  qué caracteres son seguros y qué hacer con dos archivos que quieren llamarse igual no ha cambiado.
- **Una carpeta vigilada ya no deja de vigilarse justo cuando le llegan muchos archivos.** Cuando
  Windows avisaba de que había perdido cambios —lo que pasa al copiar una temporada entera de golpe—,
  la vigilancia en vivo de esa carpeta terminaba en silencio y no volvía hasta el siguiente arranque
  de la aplicación. No se perdía ningún archivo, porque se lanzaba un repaso completo, pero a partir
  de ahí lo que se añadiera tardaba en aparecer hasta el repaso siguiente. Ahora un aviso de esa
  clase significa «repasa la carpeta entera y sigue vigilando», el espacio que Windows usa para
  avisar se pide al máximo que permite, y una vigilancia que se cae de verdad —un disco desconectado,
  una carpeta de red que deja de contestar— se reintenta sola en el siguiente repaso.
- **La verificación automática ya pulsa botones con el ratón.** El recorrido que conduce la
  aplicación construida sólo usaba el teclado, y por ese hueco pasaron unos botones que estaban a la
  vista y no hacían nada. Ahora abre la ficha de una película identificada, pulsa «Actualizar desde
  el proveedor» con el ratón y comprueba que la ficha cambia — y, antes, que pulsar al lado no cambia
  nada, para que el resultado no pueda deberse a otra cosa.
- **Los dos botones del proveedor en el editor ya hacen algo.** «Actualizar desde el proveedor» y
  «Restaurar campos del proveedor» estaban visibles y activos, y no podían funcionar: esperaban unos
  datos que sólo una prueba les daba. Ahora el refresco averigua por sí mismo a qué ficha del
  proveedor corresponde el título, con lo que quedó guardado al identificarlo. Y cuando no puede
  hacer nada, lo dice: una ficha sin identificar y un proveedor sin respuesta son cosas distintas, y
  ninguna es un error.
- **Guardar en una ficha recién añadida ya guarda.** Editar por primera vez un título que nunca se
  había tocado no creaba su ficha, así que el botón Guardar se pulsaba y no ocurría nada, sin aviso.
- **Dos ventanas editando la misma ficha ya no pueden ganar las dos.** La comprobación que impide
  pisar el trabajo ajeno comparaba contra un número que nunca cambiaba, de modo que la segunda
  ventana sobrescribía a la primera en silencio. Ahora la segunda recibe el aviso de que su copia
  está anticuada, que es lo que siempre debió pasar.
- **Identificar una película ahora cambia lo que la biblioteca enseña.** Hasta aquí no lo cambiaba:
  aceptar una coincidencia en la bandeja marcaba la revisión y nada más, de modo que la ficha seguía
  mostrando lo que el analizador sacó del nombre del archivo. La sinopsis y la clave de tráiler
  existían de punta a punta y sólo se rellenaban escribiéndolas a mano. Ahora una identificación
  —aceptada por ti, o automática cuando la confianza pasa del 90 %— pide los datos al proveedor y los
  guarda junto con de quién son y cuándo se pidieron. **Lo que hayas bloqueado sigue ganando**: la
  identificación se funde con lo que ya hubiera, igual que un refresco manual. Sin conexión
  consentida el proveedor sólo sirve lo que tenga guardado, así que una biblioteca sin permiso de red
  se queda exactamente como estaba, que no es un error.
- **Una verificación que se colgaba ya no se lleva una hora en silencio.** Seis de las diez
  ejecuciones de integración continua del 10 de agosto murieron al llegar al techo de sesenta
  minutos, y el registro no decía nada entre «compilación correcta» y la cancelación cincuenta y seis
  minutos después. El paso siguiente genera la matriz de contenedores con FFmpeg y lo arrancaba sin
  ningún tope: un encode atascado se comía el trabajo entero y se reportaba como un hipo de
  infraestructura. Ahora cada llamada al codificador tiene techo, cada muestra se anuncia antes de
  producirse y un proceso que no vuelve se mata nombrando la receta en la que estaba. Producir las
  dieciséis muestras cuesta 1,6 segundos, así que ese techo no es un presupuesto de rendimiento: es
  la diferencia entre un fallo con nombre y un trabajo que se muere sin decir por qué.
- **Y con el nombre delante, la receta que se atascaba.** La primera ejecución con el techo puesto
  falló en cuatro minutos en vez de morir a los sesenta, y señaló a `mkv-dual-audio-english-first`.
  Las once recetas que terminaban con `-shortest` pasan a fijar la duración de salida de forma
  explícita: `-shortest` tiene un bloqueo documentado cuando la mezcla de flujos y el vaciado se
  cruzan, que es justo lo que hacía que unas ejecuciones tardaran veintidós minutos y otras se
  colgaran para siempre. Todas las entradas ya duraban tres segundos, así que el resultado es el
  mismo archivo: las 114 pruebas de la matriz de contenedores lo confirman propiedad por propiedad.
- **Las preferencias de reproducción guardadas se aplican de verdad.** La pista de audio y los
  subtítulos que elegiste — por archivo, por serie o globales, con repliegue por idioma cuando una
  pista no está — se resolvían y nunca se aplicaban: cada sesión abría con lo que el motor
  eligiera. Ahora se aplican en cuanto el vídeo abre, y el selector de pistas muestra lo
  efectivamente aplicado. De paso, seis registros muertos del contenedor (duplicados de lo que la
  aplicación construye por otra vía) quedan retirados en vez de en silencio.
- **Elegir el dispositivo de salida de audio ahora cambia dónde suena.** El selector existía y el
  motor no se enteraba: tu elección se guardaba y el audio seguía saliendo por donde quisiera VLC.
  Ahora elegir un dispositivo pausa, cambia la ruta y reanuda — sin reiniciar el vídeo —, la
  elección global guardada se aplica al abrir cada sesión, y un dispositivo que desaparece a mitad
  jamás corta la reproducción: se repliega al predeterminado sin olvidar tu preferencia.
- **Cambiar de versión sin salir de la reproducción.** Si el título que estás viendo tiene más de
  una versión (otra resolución, otro códec, HDR), el reproductor las lista y puedes saltar a otra
  en plena sesión. Tu posición se guarda antes de nada; si las duraciones no permiten trasladar el
  punto con seguridad, la aplicación te lo pregunta con el segundo propuesto a la vista — continuar
  ahí, empezar de nuevo, o cancelar. Una versión que no está disponible no se ofrece como abierta.
- **Mover un vídeo de carpeta ya no le hace perder su historia.** Cada escaneo captura una
  identidad ligera de lo que cataloga (el identificador estable del disco y una huella acotada del
  contenido) y reconcilia: un archivo que apareció en una ruta nueva y desapareció de la vieja
  vuelve a ser la misma entrada, con su progreso y tus decisiones intactos. Una copia que convive
  con la original sigue tratándose como copia (versiones, como siempre). Y cuando hay duda — dos
  copias conocidas y una nueva idéntica — la bandeja de revisión te pregunta: «es el mismo,
  reasignar» conserva la historia bajo la ruta nueva; «es un archivo nuevo» lo deja como entrada
  propia. La oferta reaparece en cada escaneo hasta que decidas; nada se decide en silencio.
- **Una carpeta puede retirarse de la biblioteca.** La biblioteca lista por fin sus carpetas, y
  cada una tiene una acción de retirada con confirmación que dice la verdad: la carpeta se retira
  del catálogo, ningún vídeo del disco se toca, y si vuelves a añadirla se cataloga de nuevo. El
  catálogo en pantalla se recarga al momento.
- **Marcar algo como visto ahora se guarda de verdad.** El conmutador de «visto» de la ficha se
  construía sin manejador: cada marca iba a ninguna parte y la tarjeta la olvidaba al recargar.
  Ahora una decisión tuya se guarda como manual — nada que el reproductor calcule después la
  cambia — y quitarla devuelve el estado a las reglas automáticas. Además, el umbral de «visto»
  (qué porcentaje hay que alcanzar) se configura por fin en los ajustes de recomendaciones, entre
  el 50 y el 100 %; moverlo recalcula sólo los estados automáticos y te dice cuántos movió.
- **Un vídeo que no abre ya no arrastra consigo a las preferencias.** Aplicar las preferencias
  daba por hecha una sesión viva: con un archivo que el motor no pudo abrir, la selección de pista
  estallaba por dentro después de que la pantalla ya mostrara el diagnóstico. Ahora una sesión que
  no abrió, o que se cerró debajo, simplemente no tiene nada que aplicar; cualquier otro fallo
  sigue avisando.
- **Renombrar con el archivo abierto en otro programa ya dice qué hacer.** El fallo se guardaba en
  la auditoría como «IOException» y la pantalla no decía nada: el renombrado simplemente no ocurría.
  Ahora la superficie dice si otro programa tiene el archivo abierto, si Windows denegó el permiso o
  si la unidad falló — cada caso con su acción — y la auditoría guarda el motivo con nombre útil.
- **El diagnóstico dice lo que tu máquina hizo, no lo que una constante prometía.** La aceleración
  de vídeo informada es la que el motor usó de verdad en el último vídeo, el tamaño de la
  biblioteca es el real (en tramos, como siempre), y los errores son los que la aplicación registró
  — sin rutas ni nombres de archivo, como exige la lista de lo permitido.
- **El manual explica la entrada de arranque huérfana.** Si desinstalas con «iniciar con Windows»
  activado, queda una entrada inocua en el registro; el manual dice por qué no hace nada, cómo
  quitarla a mano y que reinstalar la repara sola.
- **La pantalla sigue al motor durante toda la sesión.** El estado en pantalla sólo cambiaba al
  abrir: la pausa pausaba el motor pero la interfaz seguía diciendo «reproduciendo» para siempre,
  con los controles de reanudar inalcanzables. El método que aplicaba las transiciones existía y
  estaba probado; en la aplicación ensamblada nadie lo llamaba. Lo encontró el paseo físico del
  artefacto empaquetado —tres escenas re-ejecutables con disco, SQLite y decodificación reales:
  vigilancia catalogando un archivo soltado y agrupando dos copias, las teclas operando un vídeo en
  reproducción, y dos episodios encadenándose solos— que queda como guardia permanente.
- **Rutas largas y escalado por monitor, declarados en vez de heredados.** La aplicación no traía
  manifiesto propio: el límite de 260 caracteres seguía aplicándose aunque Windows ya lo hubiera
  levantado —una biblioteca en una carpeta profunda perdía archivos en silencio— y la conciencia de
  escalado era la que el runtime adivinara. Ahora ambas cosas están escritas en el manifiesto del
  proceso.
- **Una migración reescrita ya no pasa desapercibida, y la integridad se pregunta una vez.** El
  arranque sólo comparaba números de versión: si el texto de una migración aplicada cambiaba, el
  esquema del disco y el que el código asume divergían en silencio. Ahora cada checksum guardado se
  compara con el del build y una discrepancia se rehúsa con nombre y apellidos. De paso, la
  comprobación de integridad —la parte más lenta de abrir una biblioteca grande— corre una vez por
  arranque, no dos.
- **Una detección ya no puede salirse de su episodio.** Las marcas manuales siempre validaron
  contra la duración; las detectadas se juzgaban a ciegas. Ahora el detector recorta lo que emite
  al episodio en que lo midió y la política aplica a las detecciones la misma regla que a las
  marcas manuales.
- **El manual dice qué pasa con sus datos al desinstalar.** Nada se borra: catálogo, progreso y
  copias siguen en su carpeta y una reinstalación los reencuentra; el manual explica también cómo
  borrarlo todo de verdad.
- **El coordinador de ventanas del reproductor tenía dos dueños.** Estaba registrado en el
  contenedor de servicios y a la vez construido a mano por la vista principal: dos instancias, una
  de ellas guardando geometría que nadie leería. La vista —que es quien posee la ventana del mini
  reproductor— queda como único dueño y el registro muerto se retira.
- **Una marca creada durante la reproducción no funcionaba hasta reabrir el episodio.** Las marcas
  de la sesión eran una foto tomada al abrir: guardar, borrar, aceptar o corregir una marca
  cambiaba lo almacenado y el botón de saltar seguía leyendo la foto vieja. Ahora cada cambio
  recompone las marcas de la sesión al momento, así que el botón aparece (o desaparece) sin cerrar
  nada.
- **Dos copias de la misma película nunca se agrupaban solas.** La agrupación de versiones existía
  con su repositorio, su política conservadora y sus pruebas, y nada la invocaba: los grupos sólo
  se creaban en los tests. Ahora cada escaneo agrupa las copias que su nombre declara iguales, una
  diferencia notable de duración espera confirmación en vez de agruparse en silencio, la
  preferencia que fijes sobrevive a los reescaneos, el grupo se encuentra desde cualquiera de las
  copias, y ningún archivo se borra ni se oculta jamás.
- **Ni los atajos de teclado ni las teclas multimedia hacían nada.** Cada pieza existía —el mapa de
  atajos con sus valores de fábrica, el editor que impide conflictos, el enrutador que evita
  acciones duplicadas, el servicio de teclas multimedia— y ninguna tocaba a otra: el reproductor no
  leía el teclado y el servicio nunca se arrancaba. Ahora el reproductor responde al mapa
  compartido (espacio pausa, flechas saltan, M silencia, F pantalla completa…), las teclas
  multimedia del hardware operan la sesión mientras existe y se sueltan al cerrarla, una tecla que
  llega por dos caminos actúa una sola vez, y el editor de Ajustes edita el mismo mapa que las
  teclas leen.
- **Al terminar un episodio nunca se ofrecía el siguiente.** El motor ni siquiera se enteraba de
  que el vídeo había terminado —el estado se quedaba en «reproduciendo» para siempre—, la cuenta
  atrás probada de punta a punta no estaba registrada, y los botones del cartel no hacían nada.
  Ahora el fin del medio es un estado de verdad, terminar un episodio ofrece el siguiente con su
  cuenta atrás cancelable, «Reproducir ya» abre sin esperar, y si no hay siguiente episodio o su
  archivo ya no está, se vuelve a la ficha.
- **La vigilancia de carpetas nunca arrancaba.** El coordinador de vigilancia, el vigilante con
  amortiguación y el planificador de respaldo existían, estaban probados y nada los arrancaba: la
  aplicación sólo escaneaba al pulsar un botón. Ahora la vigilancia arranca con la ventana y se
  detiene al salir, una carpeta recién añadida se sigue desde su primer escaneo sin reiniciar, una
  raíz configurada como manual no se vigila a espaldas de su dueño, y el escaneo de respaldo para
  USB y NAS recupera eventos perdidos cada quince minutos de verdad. Además, todo escaneo —del
  vigilante o manual— entrega lo que encontró a la identificación, no sólo el manual.
- **La identificación nunca se ejecutaba, así que la bandeja de revisión estaba siempre vacía.** El
  caso de uso existía completo — analiza el nombre, puntúa candidatos, consulta el proveedor sólo si
  hace falta y guarda el resultado — y nada lo invocaba jamás. Ahora cada escaneo entrega lo que
  encontró a la identificación: lo seguro se resuelve solo, la duda aparece en la bandeja, un
  archivo ya decidido se deja en paz en todos los escaneos siguientes, y los archivos que ningún
  escaneo anterior identificó sanan en el próximo. Sin el token del proveedor todo queda local y no
  se abre ninguna conexión.
- **«Continuar donde lo dejaste» dejaba el vídeo en cero.** La decisión de reanudar se calculaba
  después de abrir el medio, nadie pasaba la posición inicial al motor (que la acepta desde siempre)
  y los botones del cartel no estaban conectados a nada. Ahora la decisión existe antes de abrir, el
  medio se abre ya en la posición guardada, y «Empezar de nuevo» busca a cero de verdad. Tres pruebas
  de ensamblado, una unitaria y una con decodificación real cubren la cadena completa.
- **La detección en segundo plano no podía pararse y sobrevivía a la salida.** El caso de uso acepta
  cancelación desde siempre y el planificador lo llamaba sin token; cerrar la aplicación podía dejar
  un proceso decodificando en segundo plano. Ahora cada detección corre bajo un token de apagado y
  salir de la aplicación la detiene, junto con el bucle de guardado de la sesión.
- **Una respuesta ilegible del origen de actualizaciones podía tumbar la aplicación al arrancar.**
  Un portal cautivo (el wifi de un hotel) responde `200` con una página de acceso; eso lanzaba una
  excepción sin traducir que, en la comprobación automática del arranque, salía por el hilo de
  interfaz. Ahora un cuerpo que no es una versión se lee como «origen inalcanzable», la pantalla de
  actualizaciones siempre aterriza en un estado, y los tres trabajos de arranque (comprobación,
  salida por bandeja, archivo suelto) observan sus excepciones en vez de entregárselas al hilo de
  interfaz.
- **El guardado periódico de la posición nunca arrancaba.** El bucle de los cinco segundos sólo lo
  invocaban las pruebas: en la aplicación escribían únicamente el cierre ordenado y el cambio de
  versión, así que un corte de luz perdía la sesión entera. Ahora la sesión arranca el bucle al
  abrir y lo cancela al cerrar, pausar escribe la posición, y cada búsqueda —del transporte, de los
  saltos o del botón de saltar— escribe el destino elegido. De paso, los manejadores de posición se
  desenganchan del motor al terminar cada sesión, en vez de acumularse uno por episodio.
- **La detección de segmentos liberaba LibVLC en el orden que estrella el proceso.** El extractor de
  huellas paraba un player aún reproduciendo, lo liberaba antes que su media y disponía el media sin
  ventana de quiescencia — las tres reglas que el propio código tiene escritas como modo de fallo
  nativo, sobre la misma instancia que usa la reproducción. Ahora sigue el mismo orden que el motor,
  con una cola de liberación diferida en la fábrica cuyo drenaje sobrevive a un fallo de liberación.
  Un simulacro de veinte ciclos con diez episodios queda como prueba permanente.
- **El empaquetado fallaba en máquinas con un solo SDK de Windows.** La búsqueda de `makeappx.exe`
  devolvía un texto en lugar de una lista cuando había exactamente una versión instalada, e indexarlo
  producía su primer carácter: el sellado intentaba ejecutar un programa llamado `C`. La búsqueda
  estaba copiada en tres scripts (empaquetar x64, empaquetar ARM64 y verificar el paquete) y las tres
  copias llevaban el mismo defecto. Es lo que rompía la verificación continua en el runner
  actualizado; en local nunca se vio porque hay dos SDK. La misma actualización de imagen retiró
  ffmpeg del runner, así que el flujo lo instala ahora explícitamente: la matriz de códecs, el corpus
  de segmentos y la fase de asociación de archivos vuelven a medirse en cada push.
- **La primera pasada completa de la suite en CI destapó siete supuestos de máquina.** Dos pruebas de
  progreso de copia eran inestables por construcción (`Progress<T>` encola sus avisos y una máquina
  cargada llega al assert antes que las últimas etapas; ahora reportan síncronamente). Las otras
  cinco dependían del equipo: sin ningún endpoint de audio el catálogo se declara bloqueado en vez de
  fallar, una muestra HDR generada sin metadatos de color declara su precondición rota, y la promesa
  de ±5 s y el presupuesto de frame se declaran fuera del alcance de un runner compartido — sus
  puertas siguen midiéndose en el arnés físico local, como siempre.
- **La declaración de privacidad no mencionaba al actualizador.** La tabla de conexiones seguía
  describiendo la aplicación anterior a T44: enumeraba los dos destinos de metadatos y negaba que
  existiera comprobación de actualizaciones, cuando el actualizador —opcional y desactivado de
  fábrica— habla con `api.github.com` y `github.com`. La tabla enumera ahora los cuatro destinos, el
  resto de documentos acota «ninguna conexión» a los metadatos, y una prueba nueva falla si el
  registro de propósitos de red y la tabla vuelven a divergir en cualquiera de los dos idiomas.
- **Diez estados de la matriz decían más de lo demostrado.** La auditoría del 2026-08-08 encontró una
  familia de un solo defecto: componentes construidos, registrados y probados que ningún camino de la
  aplicación ensamblada invoca — la identificación, la vigilancia de carpetas, la agrupación de
  duplicados, el bucle de guardado periódico, la reanudación, la cuenta atrás del siguiente episodio
  y las teclas de atajo y multimedia — más un ciclo MSIX verificado sobre una copia resellada que el
  artefacto sin firma no puede repetir. Esas filas (PRD-002, LIB-002/003/006/007/008, PLY-008/011/014
  y REL-003) vuelven a `IMPLEMENTED`, cada una con su bloqueo, responsable y condición de desbloqueo
  en el manifiesto de verificación. La evidencia por componente sigue siendo válida; lo que faltaba
  era el ensamblaje, y ahora el registro lo dice.
  pero ningún camino de la aplicación pedía nunca una comprobación automática: sólo se comprobaba al
  pulsar el botón. Lo encontró la verificación física de T44, no las pruebas.
- **El actualizador consultaba un repositorio que no existe.** Pedía `ap-solutions/ap-reelume` en vez
  de `apvisualsolutions/ap-reelume`; GitHub habría respondido 404 y la aplicación habría dicho «ya
  tienes la versión más reciente» indefinidamente. Ahora una prueba compara esa dirección con la que
  publican los changelogs.
- **Un resumen que empezara por un subtítulo llegaba vacío,** porque el lector de secciones cortaba
  en `###` además de en `##`. Una versión así no se habría ofrecido a nadie.
- **El aviso de que Windows no abrió el paquete decía «puedes intentarlo otra vez».** En una máquina
  sin instalador de aplicaciones, reintentar no funciona nunca. Ahora dice que el archivo está
  descargado y comprobado, y cómo instalarlo a mano.
- **La puerta de recursos de reproducción fallaba una de cada tres ejecuciones sin regresión alguna.**
  Comparaba el conjunto de trabajo del proceso entero —host de pruebas y recolector de cobertura
  incluidos— con un margen de 32 MiB, y siete ejecuciones sin tocar el código dieron entre −7,9 y
  +37,6 MiB. Ajustar una pendiente tampoco sirve: se midió y va de −170 a +1107 KiB por ciclo. El
  límite pasa a 128 MiB, que es lo que corresponde a una regresión gruesa; lo que detecta una fuga
  son los recuentos exactos que ya se comprueban en los cincuenta ciclos.
- **La matriz de medios de prueba no se podía generar desde cero.** Varias muestras mezclan una pista
  de subtítulos desde un archivo acompañante, y su receta lo nombraba con un marcador que el
  generador nunca sustituía ni escribía. Estaba tapado por dos caminos a la vez: en una máquina que
  ya tenía el árbol de salida, las muestras se reutilizaban en vez de producirse; y sin ffmpeg, el
  guion termina antes de intentarlo. Lo descubrió mudar el proyecto de carpeta.
- **Una redacción anterior de la biblioteca personal estaba incompleta.** Se había sustituido el
  título de una serie en español y quedaba el mismo título en inglés, porque el patrón que se buscaba
  estaba escrito en español. Ahora la comprobación no depende de que nadie se acuerde:
  `RepositoryPrivacyTests` recorre los archivos versionados en cada ejecución y deriva lo que busca
  de la máquina, sin escribir ningún dato personal en el código.
- **Una prueba de recuperación fallaba una de cada dos ejecuciones,** porque esperaba a que el
  archivo de señal *existiera* y lo leía acto seguido: existir y estar terminado son momentos
  distintos, y leer entre ambos choca con el proceso que aún lo tiene abierto. Ahora la espera
  consiste en leerlo.
- **El botón de saltar marcas nunca recibía datos.** Estaba construido desde el MVP y ninguna
  parte del ensamblado le entregaba las marcas ni la posición, así que el salto de introducciones
  sólo existía en las pruebas. Ahora sigue al playhead con las marcas compuestas —manuales y
  detectadas—, el editor carga las marcas reales de la serie, y un episodio resuelve su serie de
  verdad en lugar de tratar cada archivo como serie propia.
- **La rama por defecto ya no puede quedarse atrás al publicar.** La redacción de la biblioteca
  personal vivía sólo en la rama de trabajo y el árbol de `main` siguió mostrando lo redactado
  hasta que la auditoría lo encontró; `main` se avanzó hasta la rama y `prepare-release.ps1`
  bloquea desde entonces cualquier publicación con `main` por detrás.
- **La compilación ARM64 nunca había funcionado.** El proyecto de Windows fijaba `PlatformTarget` a
  `x64` sin condición, así que `-r win-arm64` fallaba con `NETSDK1032`. La comprobación temprana que
  existía para detectarlo era un `dotnet restore`, que resuelve paquetes sin compilar nada: daba
  verde mientras el build era imposible. Ahora la CI construye el paquete ARM64, que contiene esa
  comprobación y además la responde.

### Nota

- **Cuatro guardias del ensamblado dejaron de leer el código como texto.** Afirmaban su promesa
  buscando caracteres en el archivo de composición — lo que un comentario o un registro muerto
  puede satisfacer, y ya había pasado tres veces. Ahora afirman los descriptores registrados y,
  en el caso del actualizador, la dirección del objeto que la aplicación construye de verdad,
  comparada con la que publican los dos changelogs. Las dos mitades que ningún descriptor puede
  expresar quedan declaradas como texto a la espera de la reforma del arranque.
- **El código nuevo ya no puede llegar sin sus pruebas.** Cada verificación exige ahora que todo
  archivo fuente nuevo respecto a `main` llegue con al menos el 96 % de sus líneas y ramas
  cubiertas por las suites, con el veredicto por archivo escrito junto a los resultados. La
  puerta se calibró contra la sesión anterior y encontró tres archivos reales por debajo del
  listón —los caminos felices estaban paseados de punta a punta; sus ramas de error, no—, que
  quedan nombrados como deuda visible en la evidencia en lugar de bajar el listón para taparlos.
- **La mejora de calidad para vídeos de baja resolución queda aplazada con su medición.** Se
  investigó con un spike medible sobre medios reales si los filtros de vídeo de VLC 3 (nitidez,
  reducción de ruido, deblocking, escalado) pueden mejorar las series de menos de 720p: ninguno
  procesa un solo fotograma en la ruta de vídeo de esta aplicación — el propio VLC monta la
  cadena y la retira entera al no poder casar los formatos, con cualquier decodificador y
  formato de salida. La función no se promete: su fila queda aplazada con la evidencia medida y
  las alternativas reales (VLC 4, realce propio sobre los fotogramas ya decodificados u otro
  motor) documentadas con su coste, para decidirse con conocimiento y no por intuición.
- **Los presupuestos de rendimiento ya no bloquean en runners compartidos.** Dos presupuestos
  fallaron en CI midiendo el ruido del vecino, nunca en local. La CI los sigue ejecutando y archiva
  su veredicto con los resultados, pero no puede fallar por ellos; siguen bloqueando en el arnés
  físico local, que es donde significan algo. La prueba de durabilidad WAL gana además un reintento
  acotado (3 intentos / 1 s) solo en la reapertura posterior al kill: el «disk I/O error»
  transitorio del disco del runner no es el fenómeno bajo prueba.
- **Ni el actualizador ni winget funcionan mientras el repositorio sea privado.** Los dos leen la
  dirección de publicaciones de GitHub, que para un repositorio privado no responde a nadie; como la
  ausencia de publicación es una respuesta resuelta, la aplicación diría «ya tienes la versión más
  reciente». Publicar el repositorio y cortar una versión son requisitos de que funcionen, no
  adornos. `eng/build-winget-manifest.ps1 -Verify` lo comprueba preguntando a la dirección.
- `PRD-003` queda **bloqueado**, no verificado: no hay máquina Windows 11 ARM64 donde certificar la
  reproducción, y emularla mediría la emulación. Las seis fases físicas están declaradas en
  `arm64-matrix.json` con su razón. Detalle en
  [T42-arm64.md](evidence/stable/T42-arm64.md).
- En ARM64 no existe la decodificación por Intel Quick Sync ni las salidas de vídeo OpenGL: VideoLAN
  no las compila para esa arquitectura. Las quince diferencias de código nativo entre las dos
  versiones quedan listadas en el informe del paquete.

## [0.1.0] — 2026-08-04

Primer artefacto instalable. Cataloga, identifica, reproduce y recuerda dónde se quedó, en español y
en inglés, sin cuenta y sin enviar nada.

### Añadido

- **Biblioteca local.** Carpetas locales, USB y UNC/NAS en su ubicación original, sin copiar ni mover
  ningún vídeo. Escaneo inicial, al iniciar, manual e incremental, cancelable y reanudable, con
  vigilancia continua y escaneo de respaldo para unidades que la vigilancia no cubre.
- **Identificación híbrida.** Detección de película, serie, temporada y episodio por nombre y
  carpeta, con metadatos de TMDB en español e idioma alternativo. Umbrales de confianza: automático
  desde el 90 %, sugerido entre el 60 % y el 89 %, pendiente por debajo. Lo dudoso va a una bandeja
  de revisión.
- **Duplicados como versiones.** Ningún archivo se borra ni se oculta; se elige versión por calidad y
  disponibilidad.
- **Edición protegida de metadatos y arte,** y renombrado opcional con previsualización, registro y
  deshacer.
- **Reproductor LibVLC integrado,** con apertura externa como alternativa. Contenedores y códecs
  habituales, HDR10 con conversión de tono a SDR, pistas y subtítulos internos y externos, velocidad,
  saltos y volumen amplificado con limitador, pantalla completa y mini reproductor.
- **Continuidad.** Progreso exacto guardado cada cinco segundos y en pausa, búsqueda y cierre;
  reanudación dentro de ±5 s; estados de visionado con umbral configurable; progreso trasladado entre
  versiones compatibles; cuenta atrás cancelable para el siguiente episodio; marcas manuales de
  introducción y créditos.
- **Experiencia personal.** Inicio híbrido con reanudar y biblioteca, favoritos, ver más tarde,
  valoración y recomendaciones locales que se explican y se pueden desactivar.
- **Accesibilidad.** Teclado completo, foco visible, lectores de pantalla, escalado, alto contraste,
  reducción de movimiento y subtítulos personalizables.
- **Datos y privacidad.** SQLite local con WAL y migraciones versionadas, copias rotatorias con
  manifiesto y exportación/importación ZIP sin vídeos. Cero telemetría sin consentimiento;
  diagnósticos opt-in y sanitizados.
- **Integración con Windows.** Bandeja e inicio automático configurables y desactivados por defecto,
  teclas multimedia y «Abrir con…» que reproduce sin importar al catálogo.
- **Distribución.** MSIX x64 y ZIP independiente, con SHA-256 publicado, SBOM en CycloneDX y SPDX,
  licencia y avisos de terceros dentro del artefacto, y compilación reproducible.
- **La aplicación puede decir dónde vive.** `AP_LOCALMEDIA_DATA_ROOT` nombra la carpeta de datos; se
  lee una vez al arrancar y en blanco equivale a no ponerla.

### Corregido

Durante el ensamblado y el empaquetado, recorrer la aplicación real encontró defectos que ninguna
prueba sin cabeza veía:

- Consentir el primer escaneo no escaneaba nada, así que una instalación nueva se quedaba vacía para
  siempre.
- Añadir una carpeta repetida cerraba el proceso en lugar de rechazarla con una frase.
- Un archivo escaneado y sin identificar abría la ficha de serie, que no ofrece reproducir.
- Elegir una pista de audio ni la aplicaba ni la guardaba.
- La sesión no alimentaba el registro de progreso, así que la oferta de reanudar no volvía.
- Retirar el consentimiento de diagnósticos dejaba el informe exportado en el disco.
- El indicador de estado del vídeo no se alimentaba nunca: quedaba en blanco mientras el motor
  decodificaba por hardware.
- Una versión antigua abría y escribía sobre una base que una versión posterior ya había migrado.
- **Instalado como MSIX, los datos no iban donde esta documentación promete**: Windows redirigía las
  escrituras al contenedor del paquete, y **desinstalarlo borraba la biblioteca entera**, copias
  incluidas. El paquete desactiva ahora esa redirección, de modo que el MSIX y el ZIP comparten una
  sola carpeta de datos y desinstalar retira sólo la aplicación.

### Seguridad

- El artefacto **no lleva ningún token de acceso**. La identificación remota exige poner uno a mano
  en `AP_LOCALMEDIA_TMDB_TOKEN`, y sin él no se abre ninguna conexión.
- El paquete declara una sola capacidad, `runFullTrust`, y ninguna de red, ubicación o biblioteca del
  sistema.
- El payload se examina antes de publicarse en busca de claves, tokens y rutas locales.

### Limitaciones conocidas

- **Sin firma de código.** Windows mostrará un aviso de SmartScreen, y la documentación no afirma lo
  contrario. Compruebe el hash publicado; la compilación es reproducible.
- **El MSIX sin firma no se instala.** Windows exige una firma en la que confíe, así que el MSIX de
  esta publicación sirve para inspección y archivo; use el ZIP, que no necesita instalador.
- **Una sola clase de adaptador de vídeo.** La matriz se ejecutó entera sobre el adaptador discreto
  disponible; este equipo no tiene gráficos integrados, de modo que la ruta de decodificación de
  Intel Quick Sync no se ha ejercido nunca.
- **`PLY-004` bloqueado:** la selección de 5.1 y 7.1 no se ha ejercido porque ningún punto final de
  audio declara más de dos canales.
- **Sin ARM64,** sin Store y sin actualizador: llegan con la primera publicación estable.
- **La agrupación automática de versiones no está cableada.** La comparación de versiones existe y
  está probada, pero hoy nada crea grupos, de modo que en el artefacto sólo aparece si un grupo
  llegara por otra vía.

[0.1.0]: https://github.com/apvisualsolutions/ap-reelume/releases/tag/v0.1.0
