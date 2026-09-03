# Cambios

Todo cambio relevante de AP Reelume. La versión inglesa está en [CHANGELOG.en.md](CHANGELOG.en.md).

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el versionado es
[SemVer](https://semver.org/lang/es/). El registro canónico del alcance, con su estado y su
evidencia, es [FEATURES.md](FEATURES.md).

## [Sin publicar] / [Unreleased]

### Añadido

- **La fila de una lección pasa a ser una sola línea, que es lo que el prototipo dibuja.** El árbol
  apilaba los dos botones **debajo** de la fila, así que cada lección medía cerca del doble de alto
  que la del diseño y una lista de veinte se leía como una página de formularios. Ahora van al
  extremo derecho, en el orden del prototipo —marcar y luego reproducir—, con el estado, el número y
  la lección entre medias.

  Con ello llegan los demás números de esa fila: el número de lección deja la letra de ancho fijo y
  pasa a negrita en la tinta secundaria como el diseño la dibuja, el símbolo de estado se tiñe del
  acento en cuanto la lección se ha abierto, la barra de avance deja de cruzar la fila entera y se
  queda en los 220 px del prototipo, y la etiqueta «Siguiente en el hilo» toma su tamaño, su peso y
  su relleno reales. **La altura se mide contra la que no podría tener si los botones siguieran
  apilados**, en vez de comprobar una estructura que cualquier disposición podría declarar.

### Corregido

- **La caja de fila del diseño deja de llamarse «fila de ajustes».** El prototipo la construye una
  vez y la gasta en toda fila que sea una caja; el árbol le había puesto el nombre de su primer
  usuario, cierto en sus treinta y un sitios y falso en cuanto la lección quiso llevarla. **Un
  nombre que describe a quien lo usó primero invita al segundo a escribir un duplicado**, que es lo
  que ya costó una tanda con otra clase de este mismo fichero.

- **Y esa caja dibujaba un píxel de menos por arriba y por abajo.** El diseño escribe su relleno y
  su radio en la misma declaración; el radio se leía del diseño y el relleno estaba escrito a mano,
  con 12 donde pone 13. Medía la mitad de una decisión y copiaba la otra, en treinta y dos filas.
  Ahora las dos mitades se leen del diseño.

- **Los radios escritos en el marcado bajan de 84 a 82**, con la fila de lección y su etiqueta.

- **La tarjeta del hilo de un curso pasa a ser la que el prototipo dibuja, entera.** El árbol
  escribía «Dónde lo dejaste» como subtítulo de 20 px sobre una tarjeta corriente; el diseño la
  dibuja como versalita de 10 en la tinta del acento sobre una tarjeta **lavada en el acento y
  rodeada por él**, dando el peso a la lección de debajo, que es la línea por la que alguien abre
  esa pantalla. **Cambiar sólo la letra habría dejado la tarjeta a medio camino entre dos diseños**,
  así que las dos mitades van juntas.

  Con ellas van los demás números de esa tarjeta —el relleno, los tres huecos, el tamaño de la
  lección y el del minuto—, la regla sobre «Lo último que viste» con su viñeta en el acento, y **la
  nota que explica el hilo, que estaba dentro de la tarjeta y el prototipo pone fuera**: dentro se
  leía como una quinta línea de la respuesta en vez de como una aclaración sobre ella.

  **Cada número se lee del diseño y no se copia**, por las dos mitades que este repositorio ya
  exige: `CourseThreadCardTests` afirma que el árbol dibuja lo que la tabla dice y que la tabla dice
  lo que el diseño dibuja. **Y el lavado se cuenta en píxeles**, no en una propiedad: la tarjeta
  cubre ahora más de cuatro veces la tinta que cubría, medido **con tres umbrales distintos** porque
  un umbral es un parámetro de la medición y no una constante.

### Corregido

- **Los ochenta y seis radios escritos en el marcado empiezan a bajar: 86 → 84.** Ninguna puerta los
  veía, porque todas hablan de clases. El trinquete que los sujeta sólo puede encoger y **falla en
  las dos direcciones**, así que cada sitio emparejado tiene que bajarlo en el mismo cambio. Los dos
  primeros son los de la tarjeta del hilo.

  **Y su propia nota estaba equivocada**: decía que un sitio se cierra «moviéndolo a una clase o
  tomando el literal del diseño». Lo segundo no cierra nada — el trinquete cuenta lo que el marcado
  escribe, así que cambiar el token por el número del prototipo deja el sitio exactamente donde
  estaba.

- **La versalita deja de ser una y pasa a ser seis, que es lo que el prototipo dibuja.** El árbol
  gastaba tres clases en dieciséis sitios; el diseño dibuja **nueve combinaciones** distintas de
  tamaño, peso y separación entre letras en treinta y cinco. Poner una sola clase en los treinta y
  cinco habría inventado una uniformidad que el diseño no tiene, que es el defecto de
  [ADR-0007](adr/0007-every-element-matches-the-prototype.md) apuntando en la otra dirección.

  **Las seis se emparejan con su elemento del prototipo y el número se LEE del diseño**, no se
  repite, que es la forma que aquel documento puso sobre los botones. `OverlineTests` lo hace cumplir
  por las dos mitades: el árbol dibuja lo que la tabla dice, y la tabla dice lo que el diseño dibuja.

  **Tres discrepancias que se ven**: el antetítulo de la portada dibujaba 12 donde el diseño dibuja
  10,5 —y su comentario afirmaba una separación de 0,16em cuando es 0,18em, acertando el número por
  casualidad mientras erraba el tamaño—; las ocho cabeceras de la tabla de duplicados eran las
  **únicas mayúsculas de toda la aplicación sin separación entre letras**; y el rótulo de «siguiente
  episodio» llevaba la clase de la portada, que es otra versalita.

- **Los dos campos de tiempo de un marcador ganan su etiqueta visible.** El diseño dibuja «INICIO» y
  «FIN» sobre ellos y el árbol sólo tenía un nombre accesible, así que la única forma de contestar
  «¿cuál de estos dos números es el principio?» era tabular hasta él y escuchar.

### Corregido

- **Dos mediciones nuevas abrían ventanas que no se cerraban si una comprobación fallaba**, y eso
  rompe el aislamiento del arnés: aparece como un fallo de limpieza que **nombra una prueba distinta,
  que ni siquiera llegó a ejecutarse**. Es la misma trampa que este repositorio ya había medido el
  2026-08-28, con la misma traza y el mismo remedio — cerrar en un `finally`—, y se volvió a pisar el
  2026-09-03. **No se reproduce en local**: es una carrera, y la salida es quitarla, no buscarla.

  **Y el arreglo tenía su propia trampa**: cerrar la ventana antes de leer el valor devuelve el del
  tema, no el que la clase dibuja. El valor se lee con la ventana viva y la comprobación va después.


- **Cinco superficies dibujaban un radio que el prototipo no dibuja**, y ahora dibujan el suyo: la
  fila de ajustes y la tarjeta del candidato aceptado pasan de 8 a **10**, la etiqueta de estado y el
  distintivo sobre la carátula pasan a **cápsula** —dibujaban una caja redondeada donde el diseño
  dibuja una píldora— y la fila de la lista lateral pasa de 4 a **7**.

  **Emparejadas por elemento y nunca por número, y eso está medido**: los radios pequeños del diseño
  tienen dos significados según dónde estén —7 es la fila de la lista **y** la mitad del botón que
  corre dentro de un interruptor; 10 es la fila de ajustes **y** la mitad del carril de ese mismo
  interruptor—, así que una tabla ordenada por el número emparejaría una fila con un botón siendo
  perfectamente coherente consigo misma. **Y varios de los «doce radios distintos» del diseño son una
  sola decisión —la píldora— escrita como la mitad de la altura que toque**: 26 en un círculo de 52,
  16 en un botón de 32, 15 en uno de 30.

  **10 y 7 se escriben como números y no es un descuido**: la escala tiene tres tokens —4, 8 y la
  píldora— y redondear al más cercano dibujaría una forma que el diseño no tiene. Es la primera
  consecuencia de [ADR-0007](adr/0007-every-element-matches-the-prototype.md).

  **Que la diferencia se ve está contado en píxeles**, porque dos píxeles de radio es justo lo que un
  rasterizador puede tragarse: se mide **cuánta tinta falta en la esquina**, que es lo que una persona
  ve y lo único que un radio recortado o ignorado no puede fingir.


- **El vigía de CI avisa ahora de cada paso que termina, no sólo del final.** El paso pesado de este
  flujo dura más de media hora, así que un fallo dentro de él **sólo se conocía por la conclusión del
  run, cuarenta minutos después**. Son pasos y no trabajos porque el flujo tiene **un solo trabajo**:
  un aviso por trabajo llegaría en el mismo segundo que el final. Medido contra un run vivo, que es
  también lo que confirmó que se puede preguntar por los pasos **mientras el run sigue en curso**.

  **El andamiaje del runner se filtra mientras pasa y nunca cuando falla** —un checkout que falla es
  el run fallando—, cada paso se anuncia **una sola vez** y el paso que falla se nombra **por encima**
  de la línea del veredicto, no por debajo. Las cuatro decisiones están medidas por mutación: cada una
  tumba exactamente su prueba.


- **«Confianza» deja de estar en mayúsculas**, porque el diseño no la grita: la dibuja a 12 px en la
  tinta secundaria y en minúscula, con la cifra en semi-negrita al lado.

- **La ruta de la carpeta se separa en dos cadenas**, la que se pinta y la que se anuncia. Ese texto
  es también el nombre accesible del campo, así que escribirlo en mayúsculas hacía que un lector de
  pantalla anunciara el nombre del campo a gritos. Es la pareja que el panel de audio ya usaba.

- **Un comentario del fichero de estilos nombraba un mecanismo que no existe.** Decía que las
  mayúsculas vienen de un conversor «que es lo que AXAML tiene en lugar de `text-transform`» y que no
  se escriben en el recurso. **No hay tal conversor en este árbol** y dieciséis recursos están
  escritos en mayúsculas; la vista de al lado explica lo contrario y es la mitad que era cierta.

- **`card-eyebrow` pasa a llamarse `card-caption` y se queda con un solo lector.** Una «eyebrow» es
  una versalita y esa clase no lleva separación ninguna, así que once de sus doce sitios dibujaban
  mayúsculas apretadas. Su propio comentario ya decía «sin que ninguno grite» mientras once de sus
  lectores gritaban: **un nombre que invita al uso equivocado es cómo eso ocurre dos veces.**

### Añadido

- **Las tres listas del panel del reproductor dejan de ser desplegables y pasan a ser listas de
  radios**, que es lo que el prototipo dibuja: pista de audio, dispositivo de salida y subtítulos.
  Con tres o cuatro opciones que caben, un desplegable cobra un clic sólo por enterarse de cuáles
  son las otras. Los números salen del objeto de estilo que el diseño escribe tres veces —una por
  lista—: fila de `minHeight:34`, `padding:'0 8px'`, `borderRadius:4`, radio de 15×15 y etiqueta a
  13, con la fila elegida lavada con el acento.

  **El trinquete del paseo NO se mueve, y eso se midió antes de tocar nada.** El relevo anterior
  avisaba de que «un `ComboBox` es un control y N radios son N», y la premisa resultó **falsa**: el
  inventario de `eng/check-walk-coverage.ps1` lee las declaraciones del `.axaml`, no las instancias
  en pantalla, así que N filas nacidas de una plantilla son **una** identidad. Medido en el mismo
  guion antes y después: **246 declaraciones y 241 identidades** las dos veces, con las tres claves
  intactas.

  **Y hay una trampa en ese mismo mecanismo que costó elegir el nombre accesible.** Las listas de
  audio y de subtítulos se declaran **en el mismo archivo**, así que un `AutomationProperties.Name`
  de `{Binding Display}` en las dos habría dado **una sola** identidad para ambas: pulsar una fila de
  audio habría dado por cubierta la de subtítulos y la puerta se habría quedado verde sobre un
  control que nadie pulsó nunca. El nombre es la clave de la lista y la pista va en `HelpText`, que
  es la forma que los botones de valoración ya tenían.

  **La escena del paseo mide más que antes, no menos.** Un desplegable sólo podía contestar si se
  había abierto; una fila contesta si la pista que nombra es la que suena. Y la del dispositivo
  **siembra su propia segunda fila** en vez de esperar que la máquina ofrezca dos: los endpoints son
  de la máquina, y una escena que pulsara «cuando haya dos» dejaría este control pendiente en un sitio
  y pulsado en otro — la lista imposible de acertar en dos máquinas que esta misma semana ya costó
  una corrección.

- **`FontSizeControl` (13) entra en la escala tipográfica**, por la regla con la que entró
  `FontSizeFootnote`: no «tiene sentido el escalón» sino «lo contradice el diseño». Contados en el
  prototipo, 13 aparece **setenta veces** —tercero tras las setenta y cinco de 12 y las cincuenta y
  tres de 11, y más de cuatro veces lo que el 14 que esta escala llama cuerpo—. El cuerpo se queda
  en 14 y no se mueve a su encuentro: lo que la cuenta dice es que a la escala le faltaba un escalón,
  no que el que tiene esté mal.

- **`OptionRowShapeTests`, medida con veintiuna mutaciones en dos vueltas.** Lee los números **del
  control** con `AppearanceService` corriendo —no del archivo de tokens, que es como dos formas
  llegaron a estar certificadas y mal— y la otra mitad de cada afirmación la lee **del diseño**, con
  el patrón que encuentra el número dentro del documento. Diez mutaciones propias cayeron; **el
  auditor encontró once agujeros más y todos están cerrados y vueltos a medir.**

  **El peor era el de la propia forma: la fila dibujaba un hueco de 10,5 px donde el diseño escribe
  9.** La tabla lo declaraba y ninguna prueba lo preguntaba — 1,5 px certificados como medidos. La
  causa, medida: la plantilla base reserva **20 px de columna** para el círculo mida lo que mida, así
  que una elipse de 15 queda centrada y empieza en 2,5. Ahora las dos elipses se alinean a la
  izquierda —el círculo empieza justo en el padding de 8 de la fila, que es donde el prototipo lo
  pone— y el relleno del contenido baja a 4. **Y ese arreglo destapó otro**: el punto interior se
  quedaba centrado en la columna vieja, **2,5 px descentrado** dentro de su propio círculo.

  **Los otros diez, todos medidos mutando lo que protegen**: borrar la segunda cadena de la fila de
  dispositivos dejaba la aplicación sin el «7.1» y las suites en verde; quitar `Mark(...)` del setter
  del dispositivo **y** del de subtítulos convertía el lavado en una foto; quitar `row-label` y su
  tooltip cortaba el nombre de una pista sin elipsis y sin forma de leer el resto; quitar los dos
  `Stretch` encogía el control de **304 px a 115** en una fila de 320; cambiar el `Grid` por un
  `StackPanel` empujaba la capacidad fuera del panel; y compartir el `GroupName` entre las dos listas
  hacía que elegir un subtítulo apagara la pista de audio.

  **Dos cosas más salieron de escribirla, y ninguna era la que se buscaba.** El patrón que lee el
  diseño casaba **cinco** filas y no tres: las dos de más eran otros controles que comparten la
  forma, así que va anclado al nombre de cada lista. Y `Application.FindResource` contesta
  `UnsetValue` para un token que vive en un diccionario de tema — hay que preguntarlo en la variante
  en la que el control se dibuja, o se lee nada y se llama a eso una comparación.

  **Y una tercera, con una causa que no era la que esta sesión creyó.** CI contestó
  `NullReferenceException` dentro de Avalonia en una prueba ajena —`ShellAssemblyTests`—, verde aquí
  cinco ejecuciones seguidas de la suite entera. La hipótesis de aquí era el estado de recursos que
  esta clase dejaba; **la causa real la midió la sesión que aisló esa suite**, y es otra:
  `AvaloniaTestIsolationLevel.PerTest` es el valor por defecto, así que **cada `[AvaloniaFact]`
  construye y destruye su propia `Application`**, mientras `ShellAssemblyTests` era `[Fact]` y leía
  `Application.Current` desde otro hilo. Reproducido: **4 excepciones en 6 vueltas** con un lector en
  un hilo de trabajo, y **3.827.981 lecturas sin un solo fallo** en el hilo del despachador.

  **Del camino sí quedan dos mediciones que valen por sí solas.** Un `ThemeVariant` nulo **no** lanza
  ante una clave ausente, así que el `null` de `CourseText.Resource` nunca fue la causa. Y el scope
  original capturaba **cero de sus treinta claves** —los tokens del acento viven en diccionarios de
  tema, no en `Application.Resources`—, así que no restauraba nada y lo poco que reponía iba a
  **otro** diccionario: `App.ApplyLanguage` hace `application.Resources = new ResourceDictionary()`
  y **reemplaza el objeto**. El scope se queda por eso, no como arreglo de aquel rojo: guarda el
  diccionario y lo repone entero, y construye el idioma y el servicio de apariencia en el único
  orden que funciona — el idioma primero, porque aplicarlo reemplaza el diccionario, y el servicio
  después, o cada número se leería con su acento ya descartado.

- **Los tres botones del prototipo, en vez del desplegable.** `chList` es una fila de tres —Estéreo,
  5.1, 7.1— con el elegido acentuado y los que el dispositivo no admite atenuados en vez de
  ausentes; eso es lo que se dibuja ahora. **Y el desplegable enseñaba los nombres internos del
  programa**: «Surround51», idéntico en los dos idiomas, que es la regla del bilingüismo rota por un
  control cuyo contenido nadie había leído.

  **Dos trinquetes se mueven, y los dos con su razón medida.** El del paseo sube de 20 a **22**:
  5.1 y 7.1 salen deshabilitados dondequiera que el paseo corra —cada endpoint físico de esta máquina
  declara dos canales y un runner hospedado no tiene ninguno—, y el arnés se niega a pulsar un
  control deshabilitado, con razón, porque una persona tampoco puede. Lo que la escena afirma en su
  lugar es la correspondencia en los dos sentidos: atenuado exactamente cuando el controlador lo
  rechaza. El de cobertura sube de 189 a **190** por `WindowsAudioEndpointConfigurator.cs`, que es el
  octavo archivo que depende de hardware que el runner no tiene: 64/54 aquí y **23/20** allí.

  **Y uno que no baja.** `LibVlcAudioOutputAdapter.cs` cayó a 77/75 en el run que destapó esto,
  porque ese run midió el código nuevo antes de que existieran las cuatro pruebas que lo cubren.
  Entran en el mismo cambio y lo devuelven a **88/87**, así que el suelo se queda en 86/87: un suelo
  que baja es una bajada, y la salida a una bajada es cubrir, no rebajar.

- **La disposición de canales dejó de ser un botón que no hacía nada y pasa a cambiar el sonido de
  verdad.** «Si quiero 7.1, ¿por qué iba a sonar estéreo?», preguntó el propietario, y la respuesta
  medida fue que **ya suena 7.1**: LibVLC negocia con el dispositivo y entrega lo que éste admite.
  Lo que no se podía era lo contrario —pedir **menos** de lo que el equipo da, que es lo que hace
  falta con un altavoz roto—, y ahora sí.

  **Seis vías medidas antes de escribir una línea, y cinco no hacen nada.** La única API de canales
  en caliente de LibVLC enumera `Stereo, RStereo, Left, Right, Dolbys` y, sobre un dispositivo de
  ocho canales, no movió **ni un decibelio** de los ocho tonos. `--stereo-mode=1` tampoco;
  `--stereo-mode=7` sí, pero da mono; `--audio-filter=mono` nada; `--audio-channels` **no existe** y
  la instancia no arranca; y los otros módulos de salida no llegaron al dispositivo.

  **Lo que decide el número de canales es el formato del dispositivo en Windows**, así que es eso lo
  que se escribe, con la misma interfaz que usa el panel de sonido. Medido: devuelve éxito **sin
  privilegios de administrador**, y un vídeo 7.1 sobre un dispositivo puesto en dos canales sale
  plegado con los coeficientes de la convención —centro a −3 dB, los cuatro traseros a −12 dB y el
  LFE descartado—.

  **El orden importa y está escrito donde se hace.** Escribir el formato invalida el cliente de
  audio de todos los programas (`AUDCLNT_E_DEVICE_INVALIDATED`, documentado), y la recuperación de
  LibVLC **descarta el dispositivo elegido y cae al predeterminado** —`DeviceSelect(aout, NULL)` en
  su `mmdevice.c`—. Por eso la disposición se escribe **antes** de enrutar: el enrutado posterior es
  lo que devuelve el sonido al dispositivo que se eligió. Al revés, el sonido se va a otros
  altavoces y la interfaz afirma lo contrario.

  **Y lo que se ofrece lo dice el controlador, no lo que el dispositivo tiene puesto.** El catálogo
  lee la disposición **actual**, y preguntarle aquí habría hecho del control una puerta de un solo
  sentido: quien bajara a estéreo una vez no habría podido volver a subir nunca. Se pregunta al
  controlador en modo exclusivo, y con **PCM entero** — copiar el formato del mezclador hizo que un
  dispositivo de ocho canales dijera «sólo estéreo», porque el mezclador va en coma flotante.

  **La interfaz avisa antes, no después**: cambiar la disposición cambia un ajuste de Windows y
  afecta a todos los programas del equipo, y eso se dice junto al control. Donde no se puede
  escribir, los tres valores son un indicador y la interfaz lo dice, en vez de ofrecer una elección
  que no puede cumplir.

  **La interfaz que esto usa no está documentada por Microsoft**, y se acepta a sabiendas porque no
  hay equivalente documentado. `WindowsAudioEndpointConfiguratorTests` existe para que el día que
  Windows la cambie se vea, en vez de dejar un control que calladamente no hace nada.

- **El panel «Lecciones» del reproductor y la lección siguiente al terminar una (`CRS-004`).** El
  curso entero en los 320 px de la columna, con la lección que suena marcada y cualquier otra a un
  clic. **Ausente y no deshabilitado** fuera de una sesión de lección, que es lo que la ficha pedía
  en negrita: un «Lecciones» apagado junto a una película promete que la película tiene lecciones.

  **Cómo sabe la sesión que es una lección: se le pregunta al archivo.** `ICourseRepository` gana
  `FindLessonByFileAsync`, espejo del de episodios, y la razón está medida — la cuenta atrás abre la
  siguiente con `PlayDetailsRequest(nextFileId, TimeSpan.Zero)` y nada más, así que un curso que
  viajara en la petición **desaparecería por cada camino que olvidara reenviarlo**, en silencio,
  porque el modo de fallo del panel es la ausencia. De paso, `ix_lessons_media_file` **existía desde
  la migración 0022 sin una sola consulta que lo usara**: el defecto de la casa en forma de índice.

  **La cuenta atrás es la de PLY-011 literalmente, no una copia.** La espera, la longitud y la
  cancelación salen a `ContinuityCountdown` y las dos cadenas usan **ese objeto**; la clave de ajuste
  sigue siendo la de episodios, porque una persona configura «cuánto tarda en empezar lo siguiente» y
  renombrarla habría dejado la elección de cada instalación atrás en la clave vieja.
  `StartNextEpisodeCountdown` conserva su superficie entera y **sus 300 pruebas siguen verdes sin
  tocar una sola**.

### Corregido

- **`ShellAssemblyTests` fallaba porque leía la aplicación de otra prueba, y el arreglo del día
  anterior había silenciado la frase dejando la causa en pie.** Dos rojos en dos días —cuatro pruebas
  con «the calling thread cannot access this object» sobre `ActualThemeVariant`, y luego
  `Consenting_to_the_first_scan_starts_it_and_reloads_the_library` con un `NullReferenceException`
  dentro de `Avalonia.Styling.Styles.TryGetResource`—, siempre dentro de la suite entera y siempre
  verdes en solitario.

  **La causa se midió, y no era la que decía el relevo.** La documentación del propio paquete
  headless dice que `AvaloniaTestIsolationLevel.PerTest` es el defecto cuando no se declara ninguno,
  y `TestAppBuilder` no declara ninguno: cada `[AvaloniaFact]` **construye y destruye su propia
  `Application`**. Medido con una sonda, tres `[AvaloniaFact]` seguidos vieron tres instancias
  distintas. `ShellAssemblyTests` usaba `[Fact]`, que corre en un hilo de xunit **en paralelo** con
  esas otras colecciones, y `FullSurfaces()` llega a `Application.Current` por
  `ShortcutSettingsViewModel` → `CourseText.Resource`. Leía, por tanto, una aplicación que otra
  prueba está montando o ya destruyó.

  **Reproducido en local, que es lo que faltaba.** Una sonda que lee desde un hilo de trabajo
  mientras otras pruebas reciclan aplicaciones dio **cuatro `NullReferenceException` en seis
  vueltas**, con la pila idéntica a la de CI. El mismo lector en el hilo del despachador hizo
  **3.827.981 lecturas sin un solo fallo**. El experimento natural ya estaba en el árbol:
  `EditorPageTests` y `ShellWindowModeTests` construyen **las mismas** superficies por
  `EditorSurfaces()`, siempre han sido `[AvaloniaFact]` y nunca han fallado. Las 27 pruebas de
  `ShellAssemblyTests` pasan a `[AvaloniaFact]`.

  **Y un verde de CI no sostiene esta corrección, que conviene decirlo porque este árbol ya se ha
  creído uno.** El run de `4406741` dio `Verify: success` **con el defecto puesto**: el fallo es
  intermitente también en el runner, así que un verde dice que no se ha roto nada y no dice nada más.
  Lo que sostiene la corrección son los **4 `NullReferenceException` en 6 vueltas** desde un hilo de
  trabajo contra **3.827.981 lecturas sin fallo** en el del despachador, y las **tres mutaciones** de
  la puerta nueva. Quien lea el verde como la prueba está leyendo la evidencia floja.

  **Y la guarda obvia se midió antes de escribirla, porque era ciega.**
  `Dispatcher.UIThread.CheckAccess()` contesta `True` **también en un `[Fact]`** —cuatro vueltas, los
  dos tipos, siempre `True`—, así que habría aprobado justo el hilo que existía para cazar. Contar
  con que `Application.Current` sea nula tampoco vale: lo es el 99,4 % del tiempo, que es una puerta
  que acierta casi siempre. `ShellSurfaceIsolationTests` la sustituye con dos mitades, cada una
  probada mutando lo que protege: devolver **un** atributo a `[Fact]` pone roja la primera, y tanto
  sacar una clase de la tabla como añadir una cuarta consumidora ponen roja la segunda.

  **El parche del 2026-09-02 no se retira, pero su comentario sí se corrige.** Pasar `null` como
  `ThemeVariant` sigue siendo correcto por su propio motivo —una cadena no cambia con el tema, y
  estas viven en `Application.Resources` y no en un diccionario de tema—, pero **no era un arreglo de
  hilo**: la pila del segundo rojo pasa por él. Un comentario que atribuye una corrección a lo que no
  la hizo es el que envía a la siguiente sesión al sitio equivocado.
- **El vigía de CI afirmaba que un push no había disparado el flujo mientras el run corría.**
  `eng/watch-ci.ps1` listaba con `gh run list --branch`, y `-Branch` tomaba por defecto **la rama
  local**, que en un worktree no es la rama a la que se empuja. Un commit escrito en
  `claude/goofy-aryabhata-1e2f4a` y empujado a `codex/shell-assembly-isolation` no tenía runs bajo el
  nombre que el vigía preguntaba —`ci.yml` sólo dispara en `codex/**`—, así que dijo «NO RUN EXISTS
  — the push did not trigger the workflow» y salió con 1. El run existía y estaba `in_progress`.

  **Y eso es peor que el silencio contra el que el propio guion está escrito.** Su docstring dice que
  un vigía callado es indistinguible de un run que sigue; un silencio se espera, pero una respuesta
  segura se obedece, y lo que se obedece aquí es «CI nunca corrió». Se observó dos veces el mismo día
  y desde dos worktrees distintos, una de ellas en uso real.

  **Desde ahora pregunta por el commit**, que es a quien pertenece un run, y el mensaje **nombra
  dónde miró**: «NO RUN EXISTS for that commit», o «on branch», con la rama nombrada, porque la frase
  anterior se leía como un hecho sobre el push cuando sólo era una respuesta sobre una rama.
  `-Branch` sigue ahí para cuando la pregunta sea de verdad una rama.

  **El segundo agujero es el que se traga un arreglo apuntado sólo al primero.** `gh run list
  --commit` exige los **cuarenta** caracteres: con un prefijo contesta `[]` y sale **0**, que tiene
  exactamente la forma de «aún no hay run» — y `.claude/hooks/post-push.sh` emitía `rev-parse --short
  HEAD`, así que el comando que ofrecía tras cada push llevaba un prefijo. Ahora el hook emite el SHA
  entero y el guion resuelve cualquier prefijo con git antes de preguntar; si no puede resolverlo
  —fuera de un árbol, o con un commit que no está aquí— **ensancha** la búsqueda a los runs recientes
  de cualquier rama en vez de estrecharla mal.

  **Y `--commit` sí devuelve runs en este repositorio, contra lo que decía una nota anterior.**
  Medido con tres SHA reales y los tres estados: `in_progress`, `success` y `failure`; los tres
  devolvieron su run. Lo que no funciona es el prefijo, que es lo más probable que hubiera detrás de
  aquella nota.

  **Los dos arreglos se demuestran mutando lo que protegen.** Sobre una escena por tubería —un repo
  cuya rama local no es la empujada, y un `gh` que contesta como se midió que contesta el de verdad—,
  devolver el filtro a la rama local y pasarle a `gh` el SHA corto **reponen el falso negativo, cada
  uno por su vía**; `WatchCiScopeTests` caza los dos y nombra el defecto en el mensaje. Una cuarta
  prueba mide lo que un arreglo así se lleva por delante con facilidad: que un commit **sin** run se
  siga reportando como tal, o el vigía habría comprado su verde no diciendo nunca que no.

  **Y el arreglo deja falsa una línea en otro documento, que se corrige aquí y no después.** La skill
  de cierre decía que el aviso del hook tras el fast-forward a `main` es «un falso positivo
  conocido». Ya no lo es: `main` sigue sin disparar el flujo, pero el vigía busca por commit y
  encuentra el run que la rama de trabajo ya produjo, así que devuelve **su conclusión** — medido con
  `3cdeeb3`, que llegó a `main` por fast-forward y cuyo run contesta `success`. Armarlo ahí es una
  segunda lectura del verde que autorizó el avance, no un aviso que ignorar.

- **La misma cifra vivía en cuatro sitios diciendo tres cosas.** Cuánto tarda un run: `CLAUDE.md` y
  la skill de cierre decían 42-53, `.claude/hooks/post-push.sh` decía 42-50 y `eng/watch-ci.ps1`
  seguía en 55-80. Los dos primeros se corrigieron el 2026-08-31, y quien lo hizo no miró fuera de
  los `*.md`. Las cuatro dicen ahora 42-53, y `RunDurationFigureTests` barre `.md`, `.sh` y `.ps1`
  para que no vuelvan a separarse.

  **Lo que esa puerta puede y lo que no está escrito dentro de ella**, porque una puerta que aparenta
  medir más de lo que mide es peor que ninguna: comprueba que el árbol **no se contradiga**, no que
  la cifra sea verdad — nada aquí puede volver a medir doce runs de un día de agosto, y cuatro copias
  diciendo 55-80 pasarían. Lo que sí ata a algo real son los valores por defecto del guion, que su
  docstring dice sacados de esa cifra: el latido tiene que sonar antes de que acabe el run más rápido
  —o un run sano calla hasta el final— y el techo quedar por encima del más lento, o el vigía
  abandona runs que iban a terminar.

  **Y la puerta nació con dos defectos que no se veían desde dentro de un worktree.** Corrida desde
  el árbol principal se puso roja nombrando diez citas: barría `.claude/worktrees/` —copias enteras
  del repositorio que pertenecen a otras sesiones, excluidas en `.git/info/exclude` y **ausentes en
  un runner**— y contaba como afirmación una línea de changelog que sólo **narraba** la cifra vieja.
  Era, por tanto, **roja en la máquina de quien programa y verde en CI**, que es la clase de puerta
  que se aprende a ignorar; y prohibía contar que la cifra se había corregido, dentro del mismo
  cambio que la corregía. Ahora salta las copias ajenas y sólo lee frases con verbo en presente.

  **Lo que cierra el arreglo es haberlo medido contra el defecto de verdad y no sólo contra el
  ruido.** Devolver `eng/watch-ci.ps1` a su línea de `3cdeeb3` —«A run in this repository takes
  55-80 minutes», una afirmación en presente que contradecía la cifra medida— vuelve a ponerla roja
  con archivo, línea y valor. Ése era el caso que un patrón más estrecho podía perder, y perderlo
  habría sido arreglar el falso positivo rompiendo el verdadero.

- **Siete puertas de esta misma tanda no medían lo que decían medir, y seis se comprobaron mutando el
  código que deberían proteger.** El auditor de puertas corrió antes de cerrar, que es para lo que
  está, y lo que encontró incluye **el defecto que originó la tanda, repetido dentro de ella**:
  `SectionHeadingTests` se escribió para impedir que la etiqueta y su encabezado se separaran, y leía
  las dos cadenas del diccionario sin construir nunca la vista — borrar los dos `TextBlock` de
  `AudioOutputView.axaml` la dejaba verde. Ahora construye el panel, cuenta los encabezados que
  pinta y afirma que cada uno **es** su etiqueta en mayúsculas.

  **La peor era la del orden**, porque sus propias notas decían que el orden es el arreglo: la
  disposición se escribe **antes** de enrutar, y la prueba llevaba dos registros separados, uno por
  doble, así que afirmaba que las dos cosas ocurrieron —cierto en los dos órdenes—. Un solo registro
  compartido, y `["layout", "pause", "device", "play"]`: invertir los dos bloques del adaptador la
  pone roja, medido, y deja verdes las otras ocho.

  Las otras cinco: la lista de atajos sólo rechazaba etiquetas **idénticas** en los dos idiomas, así
  que borrar un brazo del `switch` la hacía caer en el `_ =>` y tomar prestadas las palabras de otro
  comando —traducidas, distintas, y de otro sitio—; se añade que las diez sean **distintas** y que
  sean **diez** y no «nueve o más». La frase de conflicto, que era el undécimo literal, no la cubría
  nada. «A cualquiera de las dos profundidades» modelaba un solo controlador, y borrar la pregunta de
  24 bits de producción no rompía nada mientras borrar la de 16 sí: la asimetría era la prueba. El
  orden de la pantalla completa se afirmaba por su consecuencia, que el backend headless produce en
  los dos sentidos; ahora se escucha `PropertyChanged` y se afirma que el estado baja **antes** de
  que se mueva el ancho. Y la línea de `CompositionRoot` que conecta el informe del cambio de
  disposición no la vigilaba nadie: borrarla deja los dos avisos inalcanzables para siempre.

  **Y una de las correcciones nació ciega y hubo que medirla dos veces.** La primera versión de la
  puerta de la frase de conflicto comparaba las dos frases enteras y pasaba con el formato vuelto a
  ser un literal español, porque una de sus dos casillas es una etiqueta de comando **que sí está
  traducida**. Lo que tiene que diferir es el **marco**, así que se vacían las dos casillas antes de
  comparar. Es la misma forma del defecto que el archivo entero persigue, un nivel más adentro.

  **Lo que NO se hizo, y por qué está escrito dentro de la suite en vez de aquí.** Ninguna prueba de
  `WindowsAudioEndpointConfiguratorTests` llega a una escritura con éxito, así que sustituir el
  cuerpo de esa llamada por `=> 0;` —S_OK y no escribe nada— la deja verde. Llegar ahí exigiría
  escribir un formato que el dispositivo no lleva y luego restaurarlo, y **una ejecución de pruebas
  no cambia lo que sale por los altavoces de nadie**: un proceso muerto entre la escritura y la
  restauración lo dejaría cambiado. Las dos mitades de esa forma se miden por la costura en
  `EndpointFormatArithmeticTests`, y el hueco queda nombrado en las notas de la clase para que un
  verde no se lea como más de lo que es.

- **Dos ayudantes para leer una cadena traducida, y los dos con el mismo defecto.** El arreglo de las
  etiquetas de atajos escribió su propio lector de recursos cuando `CourseText.Resource` ya hacía
  exactamente eso y lo usaban veintisiete sitios. El duplicado se retira, y con él las ramas que
  ninguna prueba alcanzaba — la razón por la que ese archivo apareció en la lista de deuda.

  **Y el original tenía el mismo defecto de hilo**: pedía el tema en curso para leer una cadena, y
  una cadena no cambia con el tema. Ahora no lo pide, así que los veintisiete usos dejan de tocar un
  objeto que pertenece al hilo de la interfaz.

- **La pantalla completa cambiaba una flecha y nada más.** «Aun no funciona […] me refiero a todo el
  monitor, no que se vea el menú de Windows», dijo el propietario. `ApplyPlaybackMode` ponía dos
  banderas —cuál de las dos flechas dibuja el botón del transporte— y después construía una ventana
  **sólo para el mini reproductor**. A la ventana del shell no se la tocaba.

  `PlayerWindowCoordinator` tiene la geometría de pantalla completa escrita, comentada, probada y
  alcanzable, y **nadie la llamaba nunca con ese modo**: las dos únicas llamadas a `Apply` de todo
  `src/` son del mini reproductor. **Registrado y nunca alimentado, en forma de modo.** Ninguna
  puerta lo vio porque la suite que cubre esto afirma sobre `shell.PlaybackMode`, y el modelo sí
  cambiaba, siempre; lo que faltaba era la mitad que nadie preguntaba.

  Ahora el modo llega a la ventana del shell, y al entrar en pantalla completa se **recuerda** la que
  había para devolverla al salir. Además se pone `WindowState.FullScreen`, que es lo que el sistema
  entiende por pantalla completa, sin quitar el tamaño en unidades lógicas que la medición de agosto
  pedía: **se hacen las dos cosas**, y el estado se suelta antes de escribir la geometría y se pone
  después, porque una ventana en un estado tiene su tamaño guardado y no dibujado.

  **Y dos deducciones mías murieron midiendo por el camino**, las dos anotadas en la evidencia: que
  esta pantalla no estaba escalada —lo está, a factor 1,5; lo leí con una herramienta que informa en
  unidades lógicas— y que una ventana del tamaño de la pantalla no tapa la barra de tareas —la tapa,
  medido sobre la pantalla real, 960 de 960 muestras—. De haberme quedado en la segunda habría
  «arreglado» algo que ya funcionaba con el defecto todavía puesto.
  Evidencia: [la pantalla completa no llegaba a nada](evidence/stable/audit-fullscreen-reached-nothing.md).

- **La lista de atajos del reproductor hablaba español con la aplicación en inglés.** Diez nombres de
  comando —«Reproducir o pausar», «Detener», «Pantalla completa»…— y la frase que avisa de una tecla
  ya asignada eran **literales dentro del `.cs`**, así que esa pantalla entera se leía en español en
  los dos idiomas. **Ninguna puerta lo vio**: el bilingüismo se comprueba sobre el marcado de las
  vistas, y una cadena visible que vive en un archivo de código queda fuera de lo que miran.

  Once claves nuevas en los dos idiomas, y una prueba que afirma **la propiedad que esas puertas no
  pueden**: que las diez etiquetas **difieren** entre español e inglés, que es lo que hace una cadena
  traducida y lo que un literal no puede hacer. Comprobar que una vale una palabra concreta habría
  pasado igual con las otras nueve incrustadas. Probada mutando una a literal: la nombra.

  **Y una segunda cosa medida al hacerlo**: leer un recurso pidiendo el tema en curso toca un objeto
  que pertenece al hilo de la interfaz, y cuatro `ShellAssemblyTests` contestaron «el hilo que llama
  no puede acceder a este objeto» **dentro de la suite completa** mientras pasaban en aislado. Una
  cadena no cambia con el tema, así que no se pregunta por él.

- **El asentado del layout del paseo estaba donde dolió, no donde está la regla — y una sonda había
  medido el caso que no era.** La limpieza que el relevo dejó decidida partía de que
  `UpdateLayout()` e `InvalidateMeasure()+RunJobs()` son equivalentes. Lo son sobre un árbol sucio,
  que es lo que medía la sonda de la fila de tres botones; sobre un árbol que **se declara limpio**,
  `UpdateLayout()` no ejecuta nada.

  Instrumentando `BesidePoint` sobre el paseo completo —**250 clics «al lado»** en 37 escenas—, la
  ventana contestó `IsMeasureValid` **en los 250**, y forzar el pase de todas formas movió el
  rectángulo de un control **en cinco**. Un árbol que se declara limpio no es un árbol cuya geometría
  esté vigente.

  El forzado se muda a `Reveal`, por donde pasan **las dos** rutas que leen geometría — y eso destapa
  que **el press ordinario estaba fuera de la protección**: desde el 2026-09-01 el clic de al lado
  asentaba y el clic al centro de un control no, leyendo exactamente la staleness de la que el otro
  estaba a salvo. Con `Reveal` forzando, la copia de `BesidePoint` mueve **0 de 250** y se retira, y
  con ella los **33** `host.Window.InvalidateMeasure()` de las escenas. Queda **una** forma en **un**
  sitio, y de paso deja de asentarse la ventana equivocada cuando hay dos en pantalla.

  Verificado con las dos pasadas de accesibilidad que corre CI —147 de 147 cada una, sin hallazgos—
  y con la puerta del paseo intacta en 219 pulsados y 20 pendientes.
  Evidencia: [el asentado pertenece a Reveal](evidence/stable/audit-walk-settling-belongs-to-reveal.md).

- **Una preferencia estaba dibujando todas las esquinas de la aplicación, y la puerta escrita el día
  anterior para impedirlo certificaba las que nadie veía.** `AppearanceService` escribía el ajuste
  «Redondeo de esquinas» **sobre los dos tokens de radio**, y el contenedor lo resuelve antes de
  construir superficie alguna: con la opción por defecto, todo lo que gastaba `CornerRadiusMedium`
  dibujaba **10** y todo lo que gastaba `CornerRadiusSmall` dibujaba **5**, mientras el archivo de
  tokens declaraba 8 y 4.

  **`ButtonShapeTests` leía ese archivo**, así que aprobaba el 8 de `pbtn` y el 4 de `pbtnLessons`
  —las dos clases que [`ADR-0007`](adr/0007-every-element-matches-the-prototype.md) había devuelto
  al prototipo la víspera— mientras la pantalla enseñaba 10 y 5. Medido con una sonda que construye
  el control, deja correr el servicio como hace el arranque y **lee la esquina del control**.

  **El prototipo gasta esa preferencia en un solo sitio**: `st.opt.radius` sólo lo lee `artBox`, la
  caja de la carátula. El comentario que justificaba el alcance ancho afirmaba lo contrario. Así que
  la preferencia se muda a `PosterCornerRadius`, conserva sus tres opciones, y los dos tokens vuelven
  a valer en pantalla lo que declaran. **Mientras el radio de todo fuera una preferencia, ningún
  elemento podía afirmar que dibujaba su número.**

  Evidencia: [una preferencia dibujando todas las esquinas](evidence/stable/audit-corner-radius-preference-over-the-design.md).

### Cambiado

- **Los encabezados de sección del panel del reproductor, en versalita como el prototipo.** Tercera
  de las seis diferencias. El diseño escribe «DISPOSITIVO DE SALIDA» y «CANALES» a 11 px, espaciados
  y en mayúsculas, y esa forma aparece **treinta y cinco veces** en el prototipo, así que es un patrón
  del sistema y no un adorno de este panel.

  **Son dos overlines y no uno**, que es lo que la medición separó: el del héroe va a 0,16 em y éste a
  0,06 em —trece usos contra siete en el diseño—, así que una sola clase tendría que elegir una y
  estar equivocada en la otra.

  **Y las mayúsculas van en un recurso propio, no en un conversor.** AXAML no tiene
  `text-transform`, y las cadenas son recursos dinámicos porque siguen al idioma: componer las dos
  cosas habría pedido una extensión de marcado propia. El precio de una segunda cadena es que las dos
  se separen —alguien edita la etiqueta y el encabezado se queda con las palabras viejas en
  mayúsculas—, así que no se les permite: una prueba afirma que cada encabezado **es** su etiqueta en
  mayúsculas, en los dos idiomas.

- **El adaptador que escribe el formato del dispositivo pasa de 23/20 a 100/100, y sale de la lista
  de deuda el mismo día que entró.** La puerta de archivos nuevos lo rechazaba —exige 96/96 y no
  admite excepción, a diferencia de la lista de deuda, que sí sabe decir «esto depende de hardware
  que el servidor no tiene»—.

  **Lo que lo movió no fue un suelo sino una costura.** La clase era COM de arriba abajo, así que la
  aritmética que decide cuántos canales salen por los altavoces sólo podía ejecutarse en una máquina
  con la tarjeta. Detrás de dos interfaces —`IEndpointFormatStore` e `IEndpointFormatProbe`, públicas
  por la misma razón que `IAudioOutputTarget` ya lo era— se ejecuta en cualquier sitio, y diecisiete
  pruebas la recorren: el recuento de canales y la máscara de altavoces de cada disposición, el
  alineado de bloque y los bytes por segundo que se derivan de ellos, la profundidad y la frecuencia
  que **no** se tocan, un dispositivo que ya lleva la disposición pedida, un controlador que la
  rechaza, una escritura que falla, y un sondeo que sólo acepta 16 bits.

  **Y lo único que queda fuera es la creación de los objetos del sistema y sus `catch`**, marcada con
  el atributo que la documentación de coverlet describe para «métodos difíciles o imposibles de
  probar directamente». No se excluye lo que decide; se excluye lo que sólo puede fallar si Windows
  falla.

  La regla que deja: un archivo nuevo que no llega al listón por depender de hardware casi siempre
  tiene dentro dos cosas distintas —lo que habla con la máquina y lo que decide—, y separarlas cuesta
  menos que discutir la puerta.

- **Dos de las seis diferencias del panel del reproductor con el prototipo.** La etiqueta de la fila
  de canales decía «Disposición de canales» y el prototipo dice **«Canales»**. Y el aviso de mezcla
  era uno de los tres recuadros de advertencia con borde y ⚠, mientras el prototipo lo dibuja como
  **nota gris pequeña** bajo los botones. Preguntado cuál de las dos formas conservar —la decisión
  escrita en la vista o la del diseño—, el propietario contestó: «el prototipo manda». Los otros dos
  avisos siguen siendo advertencias, que es como el prototipo también los dibuja.

  **Y la escala tipográfica gana su escalón de abajo**, con la cuenta que lo justifica: el prototipo
  escribe 11 px **cincuenta y nueve veces** —segundo sólo tras las ochenta y seis del 12— y esta
  escala se paraba en 12, así que cada uno de esos cincuenta y nueve iba a llegar como literal o como
  un pie de foto un píxel más grande. La regla a la que se somete esta escala no es «¿tiene sentido
  el escalón?» sino «¿lo contradice el diseño?», que es lo que rechazó a `FontSizeMono` por ser un
  escalón para un solo consumidor.

- **Catorce clases de botón emparejadas con su control del prototipo, contra las cuatro que
  `ADR-0007` dejó.** Cinco cambian de forma: los dos botones del riel pasan de píldora a **12**, la
  baldosa de la biblioteca de 10 a **12**, y las dos filas de «Otras acciones» de píldora a **5** —
  el prototipo escribe los otros dos números de ese estilo literalmente, `min-height:36px` y
  `padding:0 12px`, y su esquina al lado es 5—.

  **Y una sexta vuelve por otro camino.** Medido contra el commit anterior, la regla retirada del
  2026-08-25 movió **siete** clases y no las dos que `ADR-0007` registró; la séptima es
  `colour-cell`, cuyo comentario seguía diciendo «Square rather than round» encima de un setter que
  escribía la píldora. El diseño no contesta por ella, así que vuelve a lo que era **antes de una
  regla que ya no existe** — que no es inventarle una forma. Las cuatro clases que el prototipo no
  dibuja se miden igual que las catorce que sí, porque son las más fáciles de mover sin que nadie
  lo note.

  **Y la puerta gana el censo que no tenía**: toda clase de botón del archivo de tokens está
  emparejada con un control del prototipo **o** escrita en una lista cerrada de cuatro que el diseño
  no contesta, cada una con lo que se buscó y no existe. Sin ese censo, una clase que nadie emparejó
  es indistinguible de una que nadie ha emparejado todavía, que es el estado en el que `ADR-0007`
  encontró diez. Las tres mutaciones que lo demuestran están en la evidencia.

### Corregido

- **La carrera del paseo volvió porque la corrección de agosto fue a la línea que alguien recordó, no
  a la regla.** `The_players_transport_is_operated_with_the_mouse` respondió otra vez `Expected:
  Embedded, Actual: Fullscreen`, **y sólo en la segunda pasada de CI**: la primera dio 147 de 147 y la
  segunda 146, con el mismo binario. El clic «al lado» del paseo aterrizó en el botón vecino.

  La corrección del 2026-08-28 puso el asentado del layout **en la escena**, antes del reset de
  velocidad. Pero pulsar «Volver a 1×» **quita ese botón de la fila**, así que la fila se recompone
  otra vez y el apuntado siguiente se toma sobre la geometría vieja. El propio comentario había
  escrito la regla general dos párrafos más arriba y la corrección no la siguió.

  Ahora vive en `BesidePoint`, que es donde está la causa: todo lo que hay dentro es geometría —el
  centro, los rectángulos ocupados, los offsets— y cualquier press anterior puede haberla cambiado. Y
  usa `window` en vez de `host.Window`, porque con el mini reproductor en pantalla son dos ventanas
  distintas y asentar una para leer la otra dejaba el defecto puesto.

  **Tres hipótesis verosímiles murieron medidas por el camino**, y ninguna era la causa: que el cambio
  de radio de `player-chrome` hubiera agrandado el área efectiva —`BesidePoint` construye sus
  rectángulos con `Bounds.Size`, que ningún radio toca—; que `InvalidateMeasure()` no bastara sin
  `UpdateLayout()` —una sonda quitó el botón central de una fila de tres y el tercero se movió de 80 a
  40 con `InvalidateMeasure+RunJobs`, sin movimiento adicional—; y que `Reveal` no asentara, cuando lo
  hace tras cada `BringIntoView()` y cada incremento de scroll. **No se reproduce en la máquina de
  quien programa**: cuatro pasadas locales verdes, antes y después.

- **La guarda que decía por escrito haber corregido un defecto seguía teniéndolo, escondido en un
  espacio.** `.claude/hooks/post-push.sh` tira los heredocs antes de buscar un `git push`, y su propio
  comentario explica por qué: la primera versión sonó en su propio commit. El regex que le pusieron
  era `<<-?['"]?DELIM` **sin admitir espacio**, así que sólo reconocía la forma pegada y dejaba pasar
  `<< 'EOF'` —la que este repositorio escribe en cada commit— como si no fuera un heredoc. Sonó al
  escribir un relevo que citaba una orden de push dentro de uno. Medido por tubería antes y después,
  con ocho casos: cuatro que suenan —incluido un push que va **detrás** de un heredoc— y cuatro que
  callan.

### Cambiado

- **La lista de deuda sube por primera vez, de 188 a 189, y el único que la sube no es deuda.** El run
  de `CRS-004` dio **todas las suites en verde** y falló sólo en la puerta de cobertura, nombrando
  **cinco** archivos bajo el listón. **Cuatro salieron mejorando**, que es el único camino que la
  lista admite, y las ramas que faltaban **se nombraron con el JSON de coverlet** —línea y offset— en
  vez de adivinarse: dos eran la misma forma, **un `Ticked?.Invoke` sin nadie suscrito**, el brazo que
  ninguno de los dos llamadores toma porque ambos enganchan un handler en su constructor. Una quinta
  rama se **quitó** en vez de probarse, porque `NextLessonPolicy` la hacía inalcanzable.

  El quinto archivo es `LessonsPanelView.axaml` a **100/50**, y **eso no es deuda**: es la única rama
  que el compilador de Avalonia genera para un `.axaml`, en la línea del elemento raíz, y todas las
  vistas del árbol miden exactamente eso. Por él sube el trinquete, con la razón escrita, que es la
  excepción que la propia puerta autoriza. **Una vista nueva lo sube en uno.**

  **Y `eng/preview-coverage-floors.ps1` calló sobre cuatro de los cinco.** Su límite estaba escrito
  como nota al pie de un parámetro; ahora la advertencia está en su cabecera y con las **dos**
  direcciones del fallo medidas, porque también da falsos positivos: el mismo día dio un archivo en
  91/80 con tres suites cuando CI, fusionando diez, no lo levantó.

### Corregido

- **La cuenta atrás no se podía cancelar con el teclado ni con la tecla multimedia, y su ficha decía
  que sí desde T28.** La prueba `Any_input_method_cancels_the_countdown` construye **su propio**
  enrutador cuyo callback llama a `Cancel()`: prueba el enrutador, no el cableado. El callback **de la
  aplicación** no tocaba la cuenta atrás en ninguno de sus diez brazos, y la única llamada a
  `Cancel()` de todo `src/` estaba en los dos botones del overlay.

  **La consecuencia era peor que «no se cancela»:** pulsar Stop cerraba la sesión mientras la cuenta
  atrás seguía corriendo por debajo, de modo que diez segundos después se abría el episodio siguiente
  sobre un reproductor que alguien acababa de parar — lo contrario exacto de «Nada se reproduce
  solo». Cableado ahora para las dos cadenas.

### Cambiado

- **Los botones vuelven a la forma del prototipo, y la regla que los apartó de él queda retirada.**
  «Todos los botones o son redondos o son píldoras, pero nunca cuadrados» era del 2026-08-25; el
  propietario la retiró el 2026-09-01 al medirse contra el diseño: **un elemento es idéntico al
  prototipo**. Aquella regla había movido dos clases **lejos** de él — `pbtn`, los botones de icono
  del reproductor, es `borderRadius: 8` y se había vuelto círculo; `pbtnAudio` y sus cuatro hermanas
  son `borderRadius: 4` y se habían vuelto píldoras.

  `ButtonShapeTests` deja de afirmar «redondo o píldora» y pasa a afirmar la correspondencia **en dos
  mitades que no pueden caducar igual**: que el árbol dibuja lo que la tabla dice, y que la tabla dice
  lo que el diseño dibuja, leyendo el número de `design/AP Reelume.dc.html` en vez de repetirlo. Sin
  la segunda mitad la tabla volvería a ser un número copiado a mano, que es cómo la regla retirada
  sobrevivió una semana. **El objetivo de 44 px se queda**: es una decisión distinta y de
  accesibilidad, y el radio nunca fue lo que separaba ese control del diseño.

### Cambiado

- **El archivo que bailaba llega a 100/100, y la puerta no necesitaba ninguna banda de tolerancia.**
  El defecto abierto decía que `eng/check-coverage.ps1` no sabe tratar un archivo cuya medición
  oscila, y proponía decidir la forma de la puerta si la oscilación resultaba inherente. **No lo era,
  y la hipótesis de partida tampoco se sostuvo.**

  La hipótesis era que a `MarkerEditorViewModel` le quedaban ramas que **sólo alcanza el paseo
  autónomo** —que pulsa con un ratón real y llega a ese estado unos runs sí y otros no—, como las tres
  que se corrigieron esa misma mañana. Se midió antes de escribir nada, leyendo `UiTests` y
  `AccessibilityTests` **por separado** y comparando rama a rama: **cero**. Ninguna rama del archivo
  dependía del paseo. La oscilación estaba resuelta desde la corrección anterior.

  Lo que quedaba era otra cosa: **siete ramas que no tomaba nada determinista**. Cuatro guardas que
  contestan «aquí no hay nada que hacer» —saltar sin rango bajo el cursor, saltar sin manejador,
  guardar sin manejador, borrar sin nada seleccionado—, una notificación **sin nadie escuchando**, y
  el brazo de la búsqueda que **pasa de largo** por una fila antes de encontrar la suya, que las demás
  pruebas esquivaban borrando el único marcador de la lista. Los seis estados ocurren de verdad, y
  ahora hay una prueba por cada uno.

  **Y una octava línea no se podía cubrir de ninguna manera**: `SeriesId`, una propiedad pública que
  **no leía nadie** —ninguna vista la enlazaba, ninguna prueba la afirmaba, y el manejador de guardado
  lleva su propia serie—. `Load` recibía un `SeriesId` y no hacía nada con él. Fuera los dos, campo y
  parámetro: el defecto característico de esta casa deja exactamente ese rastro, una línea imposible
  de cubrir tirando hacia abajo de la cifra del archivo entero.

  **Resultado medido: 44 de 44 ramas y ninguna línea sin ejecutar.** El archivo sale de
  `eng/coverage-debt.txt` y el trinquete baja **189 → 188**. Los tres métodos que las pruebas
  necesitan esperar pasan a públicos, que es la forma que ya tienen los otros modelos de este árbol:
  un comando no devuelve tarea, así que dirigirlo desde una prueba es esperar otra cosa y confiar.

- **«Subir cobertura cuesta dos vueltas de CI» era una excusa disfrazada de regla, y ahora hay un
  guion que la sustituye.** Lo decía `CLAUDE.md`, lo repetía la skill de cierre, y esta misma tanda
  actuó en consecuencia: midió un archivo en **83,33 %** de ramas, CI contestó **83**, y el número se
  usó para escribir un aviso en el relevo en vez de para corregir `eng/coverage-debt.txt`. **Anunciar
  un rojo previsible no es evitarlo.**

  Lo cierto es más estrecho: la puerta rechaza igual un suelo que se queda corto y uno que se queda
  largo, así que una tanda que mejora un archivo pone el run en rojo — **salvo que los suelos que van
  a subir se midan antes de empujar y entren en el mismo commit**, con lo que el artefacto de CI pasa
  a **confirmar** en vez de a descubrir. Dos vueltas sólo son inevitables para los **siete archivos
  que dependen de hardware** que un runner hospedado no tiene.

  **Y una regla en prosa no dispara**, que es algo que este repositorio ya aprendió caro, así que
  `eng/preview-coverage-floors.ps1` contesta la pregunta con un comando. Reproduce la aritmética de la
  puerta y **está validado contra el run que lo motivó**: nombra `AddLibraryRoot` en **100/92** y
  `ResourceKeyConverter` en **100/83**, que son los dos números que escribió CI, y calla en cuanto
  esos suelos están puestos.

  **Sus tres versiones equivocadas dieron cifras verosímiles**, y por eso están escritas dentro:
  filtrar los nombres por `src/`, que Cobertura no escribe nunca —un **cero rotundo sobre 442
  archivos**—; quedarse con el mejor informe entero en vez de la mejor línea —78 % de ramas donde CI
  lee 92, porque ninguna suite sola toma todas y la fusión sí—; y sumar ramas por `<class>`, que
  cuenta una línea una vez por lambda —**28 ramas para un archivo que tiene 14**—.

  Los dos archivos de audio que aquí leen más alto que en el runner se informan **aparte**, no
  ocultos: un aviso que suena cuando no toca es lo que enseña a ignorar el aviso.

- **La skill de cierre de tanda cubre ahora la tanda entera, y tres de sus datos habían dejado de ser
  ciertos.** `/cerrar-tanda` describía seis pasos —puertas, un commit, push, vigía, fast-forward y
  documentos— y se quedaba ahí, así que lo que viene después del verde no lo cubría nadie: cerrar las
  decisiones que quedaron abiertas, poner al día las tareas de fondo y preparar la sesión siguiente
  con su prompt. Pasa de 6 pasos a 10.

  **Y arrastraba cifras viejas, que es el defecto que la propia sesión cazó tres veces.** Decía que un
  run de CI tarda **55-80 minutos** cuando `CLAUDE.md` ya llevaba **42-53** medidos sobre doce runs;
  daba `0 new file(s)` como la condición del verde cuando lo que se lee es la frase final —el mismo
  día, un verde legítimo dijo **14**—; y proponía mover `main` con `git checkout && git merge
  --ff-only`, que cambia de rama dos veces y puede dejar a alguien en `main` por descuido, en vez de
  `git branch -f`.

  **Entran además los tres rojos que no son de quien empuja**, porque los tres se han diagnosticado
  desde cero más de una vez: el «N improved» de la primera vuelta al subir cobertura, el archivo cuya
  medición oscila, y una prueba del paseo que falla sola —con el `git diff` que distingue la
  intermitencia de una regresión en un comando—.

- **`eng/list-pending.ps1` contaba las aplazadas como decisiones, y la hoja de ruta dice lo
  contrario.** La regla de publicación cuenta las `DEFERRED` —aplazado no es rechazado— y excluye
  las `OUT_OF_SCOPE`. El guion las metía a las dos en el mismo saco, así que llamaba «decisión en
  pie» a `UX-007` y `PLY-016`, que la regla ya había metido dentro. Ahora dice **13 de trabajo y 2
  decisiones** en vez de 11 y 4. **Una lista derivada que contradice al documento que decide es
  exactamente lo único que no puede hacer**, y duró unas horas.

### Añadido

- **`.flv` entra, y con él salen tres copias de la lista que nadie vigilaba.** Es la única extensión
  de vídeo que la aplicación no reconocía de las dos raíces de cursos del propietario —**10 archivos,
  un curso entero invisible**—, mientras las ocho aprobadas cubren el **98,3 %** de su vídeo, medido.
  Va **al final** de la lista y no en orden alfabético: es la única adición posterior a la
  especificación del MVP, tres archivos citan esa secuencia y la puerta de empaquetado los compara
  **en orden**.

  **La lista existía seis veces y sólo tres estaban vigiladas.** Las tres que compara
  `FileAssociationPackageTests` —el dominio, el fragmento autorizado y el manifiesto— no se pueden
  unificar, porque cada una la lee algo distinto y por eso hay una prueba que las ata. Las otras tres
  sí, y **una ya se había quedado atrás**:

  - **`MediaNameParser` tenía la suya**, y la usa para quitar la extensión del nombre antes de
    interpretarlo. Una extensión que el escáner acepta y ese parser no es **un título con la
    extensión pegada**: el archivo se cataloga y se lee «Lección 01.flv». Ahora pregunta a
    `MediaFileExtensions`.
  - **`MediaFileExtensions` guardaba dos**, un conjunto y una lista ordenada, escritas a mano las
    dos. El conjunto se construye ahora de la lista: una clase que existe porque dos listas derivan
    era el primer sitio donde iba a pasar.
  - **Dos suites llevaban la lista literal** —`OpenLooseFileTests` e `IncrementalScanTests`— para
    afirmar «todos los contenedores que la biblioteca reconoce». Un literal ahí **sigue pasando** el
    día que la biblioteca reconoce uno más, que es exactamente lo que hacía. Leen el dominio.

  **Y el número suelto estaba escrito ocho veces más**, en dos pruebas que sí se pusieron rojas y lo
  dijeron: `MsixLifecycleTests` comparaba el literal `«8 of 8»` contra el informe del ciclo de vida, e
  `IncrementalScanTests` afirmaba **siete veces** el número 8 sobre un árbol con un archivo por
  contenedor. Las dos se derivan ahora de la lista: una prueba sobre «todos los contenedores
  declarados» con una cifra dentro es una prueba sobre esa cifra.

  **El ciclo de empaquetado se pagó entero**, que es la razón por la que esto llevaba varias sesiones
  aplazado. El manifiesto caduca dos mediciones fijadas por su SHA-256, así que se rehizo el ciclo del
  sandbox: **las doce fases coinciden desenlace a desenlace con las archivadas** y la única diferencia
  es la buscada —la asociación pasa de **8 de 8** a **9 de 9** contenedores con entrada «Abrir
  con»—. `verify-package.ps1` confirma **12 fases, 0 bloqueadas** y **688 archivos idénticos** entre
  dos construcciones del mismo commit.

  **De paso cayó un rojo local que llevaba nueve días**: el artefacto ARM64 era del 22 de agosto y el
  color de fondo del manifiesto cambió el 24, así que `Arm64PackageTests` comparaba con un paquete
  anterior al cambio. Reempaquetado, `PackagingTests` da **194 de 194** aquí — los «30 rojos en local»
  que la guía daba por normales son **artefactos ausentes**, no la máquina.

- **«Curso (carpeta de lecciones)»: la opción que faltaba para declarar un curso (CRS-001).** Todo lo
  demás de Cursos estaba construido y no se podía usar, porque **nada permitía decir «esta carpeta es
  un curso»**. El diálogo de añadir gana su segunda mitad, y con ella vuelven al contenedor
  `MarkCoursesInRoot` e `ICourseRootDeclarationStore`, fuera desde que `ServiceConsumptionTests` los
  rechazó por no tener quien los resolviera. El rechazo era correcto y ahora tienen consumidor.

  **La profundidad se señala y no se teclea**, que es la enmienda 1 del `ADR-0006`: se apunta a **una**
  carpeta de curso y `CourseRootDeclarationPolicy` deriva el nivel del gesto. Si la carpeta ya está
  dentro de una raíz catalogada, esa raíz es la que se declara y el nivel es su profundidad relativa;
  si no, el **padre** pasa a ser raíz y el nivel es 1. Una raíz añadida así es `Manual` y nunca de
  arranque, porque la ayuda del propio diálogo promete que no se recorre el resto de la unidad.

  **Las vecinas se preguntan, no se reclaman.** A la profundidad derivada suele haber carpetas sobre
  las que nadie ha dicho nada, así que se marca la señalada y las demás se cuentan: «Hemos encontrado
  {0} carpetas más. ¿Son todas cursos?». La frase es del propietario y pregunta por **el hecho** —lo
  único que el programa no puede saber—, no por la acción. Decir que sí es una segunda pasada que
  **relee la raíz** en vez de fiarse de lo que contó la primera.

  **El aviso lleva el esquema de un curso como árbol en monoespaciada**, decisión del propietario: sin
  binario que mantener, sin traducir dos veces, y escala con el tipo. Se escribe con `&#10;` y eso
  **se midió** en vez de suponerse — el MCP de Avalonia no tiene página de `TextBlock`, y «no results»
  también es un dato—: llega a la superficie como **seis líneas** en los dos idiomas.

  **Y una puerta encontró lo que la lectura no.** `ViewHeightTests` midió el diálogo en **640 px**
  contra una ventana cuyo mínimo son **600**: el fondo del panel era contenido que nadie podía
  alcanzar. Baja a **560** y su contenido se desplaza dentro, y no el panel dentro del shell, porque
  un hijo centrado de un `ScrollViewer` deja de estar centrado.

  El diálogo tiene **una sola acción** y no dos: dos botones con `primary-action` son una pantalla que
  no ha decidido para qué es, y `LeadingActionTests` lo rechaza. Lo que dice y lo que hace siguen a la
  píldora elegida.

  **Y el paseo autónomo encontró un defecto que ninguna prueba unitaria podía ver.** «Marcar todas»
  se construye cuando todavía no hay vecinas, contesta que no puede ejecutarse y **se queda
  deshabilitado toda la vida del diálogo**: en pantalla, con el aspecto correcto, y sin poder
  pulsarse. Un botón atado a un comando pregunta `CanExecute` **una vez** y luego espera a que le
  avisen, y nadie avisaba. Leer `CanExecute` del modelo da la respuesta buena se haya avisado o no,
  así que lo que se afirma ahora es **el evento**.

- **Tres cifras de la guía eran falsas el mismo día, y ahora hay una puerta que las mide.** `CLAUDE.md`
  decía que el trinquete estaba en **205** cuando el guion decía 191; la skill de cierre daba un run
  de CI por **55-80 minutos** cuando la cifra medida era 42-53; y la guía hablaba de **«las 48
  vistas»** en tres sitios cuando el árbol tiene **59** según la definición de vista del propio
  proyecto. Ninguna se había notado, y `.claude/` **no estaba bajo ninguna puerta** — ni las pruebas
  ni los guiones de `eng/` lo leían.

  **Se midió antes de diseñar, y la medición descartó el diseño obvio.** Un barrido que buscara
  números no puede funcionar: **«96» aparece 272 veces** en estos documentos y casi ninguna es el
  listón. Y lo segundo pesa más: **la mayoría de los números de este repositorio son historia y NO
  deben comprobarse** — el changelog y todo `docs/evidence/` son actas de lo que se midió aquel día,
  así que «las 48 vistas» es **correcto ahí para siempre**. Sólo se leen los documentos que afirman
  el presente.

  **Así que una cifra se apunta sola**, con `<!--medido:clave-->` detrás, y la marca nombra su fuente.
  Seis fuentes —el trinquete, los archivos en deuda, el trinquete del paseo, las vistas, los
  identificadores de alcance y el listón—, deliberadamente pocas: una puerta que cubre lo que de
  verdad se cita vale más que un sistema general que nadie mantiene.

  **Y tiene dos suelos contra la ceguera, que es de lo que murió la vigilancia anterior.** Una clave
  desconocida **falla** en vez de ignorarse, porque una errata apagaría la comprobación en silencio;
  una fuente que nadie cita **también falla**, que es la regla que ya se aplica a los servicios
  huérfanos; y si se encuentran menos de cuatro cifras marcadas, la prueba se declara ciega. Ese
  último suelo se ganó el sueldo en el acto: las marcas se pusieron primero en la línea siguiente a
  su número, el patrón las buscaba en la misma, y **encontró 1 de 9** — sin ese suelo habría pasado en
  verde midiendo casi nada.

  **Probada mordiendo**, no sólo pasando: falseadas a propósito las dos cifras que hoy estaban mal de
  verdad, la puerta las nombró con documento, línea, lo dicho y lo medido.

- **`eng/list-pending.ps1`: qué falta, leído de la matriz y no de la memoria de nadie.** La pregunta
  «¿qué queda antes de publicar?» no tenía respuesta consultable: cero incidencias en el
  repositorio, 65 filas repartidas en seis tablas de `FEATURES.md` con los dos idiomas dentro de
  cada celda, y 5.900 líneas de relevo narrativo. Ahora hay un comando, y contesta **15 abiertas de
  65**, agrupadas por versión y separando las once que son trabajo de las cuatro que son decisiones
  en pie.

  **No es un segundo registro y por eso no puede desviarse**: no guarda lista propia, lee los
  estados y las versiones de las **dos leyendas del propio documento** —una etiqueta añadida a la
  leyenda funciona el día que se añade, y una usada sin declarar es un error que se reporta en vez
  de absorberse— y cada dato que imprime sale de la fila que lo imprime.

  **Lo que de verdad tenía que hacer era no callarse.** La lista se había producido antes a mano con
  un patrón que pedía tres mayúsculas, y `UX` tiene dos: **ocho filas desaparecieron sin un solo
  error**, incluida `UX-007`, que era exactamente la que se estaba preguntando. Así que el guion
  cuenta las filas por **dos caminos independientes** y exige que cuadren, valida cada identificador,
  estado y versión contra las leyendas, y ante cualquier fila que no entienda **se niega a imprimir**
  en lugar de imprimir una lista más corta.

  **Y encontró tres defectos reales el primer día, dos de ellos en la matriz**: una **línea en blanco
  en mitad** de la tabla de biblioteca, que la partía en dos y dejaba `LIB-016` y `LIB-017` fuera de
  la tabla —se renderizan mal también en GitHub—; la versión `Post-MVP`, usada por cinco filas y
  **nunca declarada** en la leyenda; y un patrón que descartaba `A11Y-001` y `A11Y-002` por llevar
  dígitos en el prefijo. Ese último explicaba una discrepancia que llevaba tiempo a la vista: la
  puerta de documentación contaba 65 identificadores y cualquier recuento a mano daba 63.

  **El tercer defecto era del propio guion, y es el que va escrito dentro de él.** Su parámetro
  `-Target` y la variable `$target` de cada fila **son la misma variable**, porque PowerShell no
  distingue mayúsculas en los nombres. Al terminar el bucle el parámetro contenía la versión de la
  última fila leída, y el filtro «sólo esta versión» se aplicaba sin que nadie lo pidiera: **la lista
  salía con 3 entradas en vez de 15, sin un solo error**. Exactamente el fallo que el archivo existe
  para impedir, cometido por el archivo. La variable se renombró y el motivo quedó en el comentario,
  porque esto vuelve.

### Corregido

- **El archivo cuya cobertura oscilaba ya no oscila, y la causa no era la puerta.**
  `MarkerEditorViewModel` medía 79, 79, 79, 81, 79 en cinco runs seguidos y volvió a dar 81 el
  2026-08-31, tumbando un run que **sólo tocaba un `.md`**. Se subió su suelo una vez y el run
  siguiente falló por lo contrario, así que la puerta no tenía **ninguna** posición correcta para él:
  con 79 falla cuando mide 81, y con 81 falla cuando mide 79.

  **Antes de tocar la puerta se midió por qué se movía**, y lo que se encontró es que **tres de sus
  ramas se cubrían por accidente**: el condicional nulo de la línea 171 y las dos guardas de índice de
  182 y 205 sólo las tomaba el **paseo autónomo**, que pulsa con un ratón de verdad y alcanza ese
  estado unos runs sí y otros no. Ninguna prueba unitaria las tocaba, y el motivo es preciso: todas
  guardaban un marcador **nuevo**, así que el brazo que **sustituye** uno existente no corría nunca; y
  ninguna cambiaba la lista durante el `await` de un borrado, así que el brazo donde la fila **ya no
  está** tampoco. Los dos son estados reales —editar un marcador, y cualquier cosa que recargue la
  ficha a mitad de un borrado— y los dos tienen ya su prueba.

  **Medido después: `UiTests` sola pasa de 34/44 a 37/44**, por encima del 36 que el archivo llegó a
  alcanzar mientras oscilaba. Como la fusión se queda con el mejor informe de cada línea, la cifra
  fusionada **ya no puede bajar de 37**, y el número deja de moverse.

  **Segunda vuelta: CI lo confirmó y el suelo subió de 79 a 84.** El rojo de la primera era el
  esperado y lo predijo la propia corrección: en cuanto un archivo mejora, la puerta exige sacarlo de
  la lista o subirle el suelo. El número que midió CI —**84**— es **exactamente** el que se había
  medido aquí antes de empujar: 37 de 44 ramas.

  **Y esta subida sí es legítima, a diferencia de la de agosto**: no es un run con suerte, es una
  rama que una prueba determinista toma siempre. Si la cifra vuelve a moverse, la pregunta es **qué
  rama volvió a cubrirse por accidente**, no qué tolerancia debería crecerle a la puerta. Que es todo
  el asunto: la corrección correcta era **cubrir la rama a propósito, no aflojar la puerta** — y
  empezar por el parche habría escrito una banda de tolerancia alrededor de un hueco de pruebas.

- **Los dos ViewModels de Cursos entraron sin una sola prueba, y son lo único que le quedaba a `main`
  para desbloquearse.** `CoursesViewModel` medía 96,15 % de líneas y **58,33 % de ramas**, y
  `CourseDetailsViewModel` 93,91 % y **58,51 %**; la puerta de archivos **nuevos** exige 96/96 y, a
  diferencia de la lista de deuda, **no admite techos medidos**. Un `grep` sobre `tests/` no
  mencionaba ninguno de los dos: 627 líneas cubiertas sólo por el paseo autónomo a través del shell
  ensamblado, que mide **comportamiento y no ramas**. Ahora leen **100 / 97,22** y **100 / 96,81**.

  **Las cifras de partida no se dedujeron del informe de nadie: se reprodujeron.** El artefacto
  `test-results` del run de CI, fusionado aquí con el mismo `reportgenerator`, dio los **cuatro
  números exactos** que CI había escrito en su registro. Lo que **no** funciona es correr
  `eng/check-coverage.ps1` entero contra ese artefacto descargado: sus 430 nombres de archivo llegan
  como `ApSolutions.LocalMedia.Presentation/…` sin el `src/` inicial, la puerta los busca con
  `EndsWith('src/…')` y **ninguno casa**, así que declara «PASS (no instrumentable lines)» sobre todo
  el árbol. Es un falso verde de manual —una puerta que se vuelve ciega en vez de falsa—, y la
  reproducción se hace **leyendo el Cobertura fusionado**, no ejecutando el guion.

  **Todo lo nuevo se cubre dentro de `UiTests` y eso es aritmética, no gusto.** El informe fusionado
  se queda con **el mejor informe de una línea y no con la unión** de ellos, así que un par cuyos dos
  lados se toman en suites distintas lee la mitad para siempre — que es lo que hundió a los tres
  `*Rename` el 2026-08-30. El barrido de constructores pasa un null a cada constructor de
  Presentation y se lleva el brazo que lanza; estas pruebas construyen los modelos con un `GetCourses`
  y un `SetWatchStatus` **reales**, en esa misma suite, y cierran el par.

  **Quedan cuatro ramas y las cuatro son inalcanzables. Están medidas y escritas en la prueba que
  alguien volverá a mirar**, no sólo en la evidencia: los patrones de propiedad de `Progress` y
  `ThreadMinute` emiten una comprobación de null por cada miembro que atraviesan, y `Summary` y
  `Thread` son miembros posicionales **no anulables** de un `record`; la guarda de `ResumeThreadAsync`
  la refuta su propio `CanExecute`; y `Application.Current is { }` no tiene lado falso mientras haya
  aplicación viva. **Esa última no se supuso**: un `[Fact]` llano llegó hasta
  `Application.get_ActualThemeVariant` y reventó, que es la prueba de que `Current` no era null.

  **Y la trampa del hilo se cobró una variante nueva: no es la prueba quien decide si toca el hilo de
  UI, es el DATO que se le pasa.** `CourseModuleViewModel` construido con un módulo **con título**
  arma su etiqueta a través de `CourseText.Resource` —que lee `ActualThemeVariant`, que verifica el
  hilo— y revienta con la suite entera; con el título nulo no lo llama siquiera y pasa. La misma
  llamada, el mismo `[Fact]`, y lo único que cambia es el dato. Las cinco de las seis ramas
  alcanzables de `Resource` se toman escribiendo en `Application.Current.Resources`: sin la clave, con
  la clave, y con algo que no es una cadena bajo ella.

  **Segunda vuelta, y CI confirmó las dos cifras: 100/97 y 100/96.** El rojo de la primera era el
  esperado y está descrito en la guía: la puerta falla igual ante un suelo que se queda corto **y
  ante uno que se queda largo**, así que en cuanto un archivo mejora pide sacarlo de la lista. Los
  dos salen, el trinquete baja de **191 a 189**, y la nota de cabecera que decía que «sus ViewModels
  no tienen pruebas unitarias propias» se corrige, porque ya no era cierta y `-WriteDebt` **conserva
  la cabecera** en vez de regenerarla — una frase falsa ahí se arrastra sola de run en run.

  **Y una cifra de la guía llevaba una tanda entera mintiendo**: decía que el trinquete estaba en
  **205** cuando el guion decía **191**. Se copió a mano y nadie volvió a mirarla, que es exactamente
  el defecto que ese mismo párrafo describe. Queda escrito que la única fuente es `$debtRatchet` y
  que lo de la guía es una referencia que se comprueba antes de citarla.

- **La lista de deuda sube por primera vez, de 186 a 193, y el motivo es que siete archivos NACEN
  por debajo del listón.** La regla de que sólo encoge se escribió contra la degradación —un archivo
  que estaba arriba y empeora— y un archivo nuevo no es eso: no hay nada que recuperar. La propia
  puerta lo permite en su mensaje de error, y hasta hoy nadie lo había ejercido, porque las 48 vistas
  del árbol entraron el día que la lista se creó. Lo que la regla sigue impidiendo queda intacto: un
  archivo de la lista que empeore falla igual. Las dos frases del guion que decían «la lista sólo
  encoge» se corrigen en el mismo cambio.

  **Dos de los siete tienen techo medido, y por eso entran en vez de perseguirse.** El primero
  explica un número que llevaba meses en el árbol: los tres `.axaml` miden `100/50`, y ese par **no
  es deuda suya sino lo que mide toda vista**. Sobre las 48 del árbol, **todas tienen exactamente una
  línea con ramas —la del elemento raíz— y siempre a `1/2`**; `App.axaml` está a `0/2`. Es la rama
  que el compilador de Avalonia genera al convertir el `.axaml`, y **ninguna prueba la ha tomado
  jamás**. De ahí que **63 de los 69 archivos del árbol con ese par exacto sean vistas**.

  El segundo es la cuarta rama inalcanzable que `Domain` encuentra de la misma forma:
  `CourseThreadPolicy` se queda en 100/93 por el **caché de delegado** de un `TakeWhile` cuya lambda
  captura una variable, así que la clausura se reconstruye en cada llamada y el campo nace nulo. **Se
  midió haciendo**: reescrito con un bucle, la rama desaparece del informe — y el cambio se revirtió,
  porque el bucle aporta ocho ramas propias y deja el archivo en 94, peor negocio que el techo.

  **Y la corrección de fondo queda nombrada y no hecha**: si la puerta ignorase la cobertura de
  **ramas** de un `.axaml` —no el archivo, cuyas líneas sí miden algo— saldrían 63 archivos de la
  lista y el trinquete caería a unos 123. Cambiar la puerta central para que deje de mirar algo es lo
  que aquí se llama aflojar una puerta: merece su tanda, no ir de rondón en una que desbloquea `main`.

- **Ocho constructores aceptaban un null y fallaban más tarde; ahora lo rechazan donde se causa.**
  Los ocho declaraban sus dependencias como constructor primario, que no tiene dónde poner la guarda
  sin declarar un campo, así que se tragaban el null y reventaban en el primer uso del campo
  capturado: una `NullReferenceException` desde dentro de un método, sin nombre de parámetro y sin
  relación visible con la composición que la causó. La guarda no es defensa contra nadie, es el sitio
  donde un error de cableado se dice a sí mismo con su propio nombre. Promovidos a constructor
  explícito: `ExecuteRename`, `PreviewRename`, `UndoRename`, `UpdateMetadata` e `IntegrityChecker`
  con un parámetro cada uno, y `RefreshMetadata`, `ApplyIdentification` y `RefreshStaleMetadata` con
  cinco o seis.

  **Veintidós parámetros, y el barrido los cuenta**: Application pasa de 127 a **148** guardas
  ejercidas e Infrastructure de 64 a **65** —325 en total con las 112 de Presentation, que no tenía
  ninguno—. La suma cuadra exactamente con los veintidós, que es lo que dice que no se ha añadido una
  guarda que nadie toma ni perdido una que ya estaba.

  **Y con ellos se van las dos listas cerradas que el barrido había dejado, que no se borran: se
  vacían solas.** La segunda prueba de cada suite —la que existe para que la lista sólo pueda
  encoger— falló nombrando los ocho en cuanto la guarda llegó, antes de tocar una línea de las
  pruebas. Eso es lo que separa una lista de deuda de una de exenciones: **una exención calla cuando
  deja de hacer falta y una deuda protesta**. La regla vuelve a ser estructural, sin nada que
  mantener a mano.

  **Lo que había que vigilar no era la conversión sino quién lee esos constructores.**
  `ServiceConsumptionTests` los analiza **como texto** para decidir qué servicio alimenta a cuál, así
  que cambiarles la forma cambia justo lo que ese analizador mira — y el riesgo no era que fallara,
  que se ve, sino que **dejara de ver** y aprobara por silencio. Sus 30 pruebas quedaron verdes, con
  la suya propia contra la ceguera incluida. El renombrado de los usos saltó las líneas de comentario
  por un motivo medido: `RefreshMetadata` lleva una frase donde `provider` es una palabra inglesa, y
  un barrido ciego habría escrito `_provider's` dentro de ella.

  **Y lo que costó de verdad no fue la conversión: fueron tres archivos que CAYERON.**
  `PreviewRename` pasó de 100/100 a **100/50**, `UndoRename` a 100/75 y `ExecuteRename` a 100/83, los
  tres por debajo del listón y en ninguna lista. La primera explicación era falsa —«no tienen
  pruebas»; sí las tienen— así que se reprodujo la fusión de CI aquí, bajando el artefacto del run y
  fusionando sus veinte informes con el mismo `reportgenerator` que usa la puerta: la línea del
  constructor lee `1/2` en **todos** los informes y `1/2` fusionada.

  Son **dos causas encadenadas y ninguna del código**. Un archivo **sin ninguna rama** mide 100 % de
  ramas por definición, así que su primer `?? throw` es también su primera ocasión de quedar a
  medias: con dos ramas, una sin cubrir es el 50 %. Y quedó a medias porque **los dos lados del par
  se toman en suites distintas** —el barrido pasa el null en `Application.Tests` y las pruebas de
  renombrado pasan la dependencia real en `IntegrationTests`—, y el Cobertura fusionado **se queda
  con el mejor informe de una línea en vez de la unión**. `ReviewInboxViewModel` chocó con la misma
  pared el 2026-08-28.

  La corrección no es otra aserción sino la misma en un solo sitio: `Application.Tests` ahora rechaza
  el null **y** construye los tres con algo real. La línea pasa de `1/2` a **`2/2`**, y la segunda
  vuelta lo confirmó con **186 en la lista y 186 medidos bajo el listón, que cuadran**. Tres suelos
  suben —`RefreshMetadata` a 97/95, `UpdateMetadata` a 81/88, `IntegrityChecker` a 94/87— y el
  trinquete se queda en **186**, porque un suelo que sube no saca a nadie de la lista.

- **Una biblioteca en la raíz de un disco vuelve a moverse cuando se le dice que se ha movido.**
  `RootRemapPolicy` devolvía la decisión como `Remapped` y después `Rewrite` no reescribía ni una
  ruta. `IsUnder` preguntaba si la ruta empieza por la raíz seguida de `\`, y para `D:\` —que
  conserva su separador a propósito, porque `D:` en Windows nombra el directorio actual de esa unidad
  y no su raíz— eso es `D:\\`, con lo que no empieza ninguna ruta real. La restauración terminaba
  diciendo que había ido bien con cada archivo apuntando al disco que la persona acababa de decir que
  ya no usa, **y nada lo anunciaba**. La misma costura por el otro lado duplicaba el separador cuando
  el destino era una raíz: `F:\` seguido de `\shows\episodio.mkv` daba `F:\\shows\episodio.mkv`. Las
  dos caras se unen ahora por un separador exacto, y las dos llegan con su prueba: el rojo medido
  decía `D:\shows\episode.mkv` donde tenía que decir `F:\library\shows\episode.mkv`.

- **Tres campos que se rellenaban en cada lectura y que no leía nadie** — el defecto de la casa, esta
  vez en los datos. El `Language` de `MetadataSearchResult` y `MetadataDetails` no guardaba el idioma
  en el que vino la respuesta sino **el que se pidió**: TMDB sirve el título con lo que tenga cuando
  no hay traducción, así que quien lo leyera habría recibido lo contrario de lo que su nombre
  promete. `WatchedTitle.Id` es el tercero: `Summarize` promedia géneros, reparto, nota y año, y la
  señal de «esto ya se ha visto» llega al marcador por el otro lado, como
  `RecommendationCandidate.IsWatched`. Ninguna lectura de los tres existía en `src/`. Con ellos se
  van dos pruebas que afirmaban que el valor **existía**, no que fuera cierto, que es la forma exacta
  de una puerta que pasa por no mirar nada — y una de ellas llevaba escrita en su propio comentario
  la asimetría que la invalidaba.

- **La escala de los iconos, alineada con el prototipo contexto a contexto.** Restituido el lienzo, la
  clase pasa a ser el tamaño real, así que por primera vez se puede comparar cada sitio con lo que el
  prototipo dibuja ahí. Seis de nueve ya casaban; tres se corrigen: el cromo del reproductor baja de
  20 a **18**, el volumen y el silencio **suben** de 16 a 18 —iban más pequeños que el prototipo, al
  revés que todos los demás— y el glifo de tipo sobre una portada baja de 14 a **12**. Tres clases
  nuevas, `size-12`, `size-15` y `size-18`, cada una con su grosor `1,6 × N ÷ 24`.

  **El cuarto desvío no se corrige, y su precio está medido**: el play de una tarjeta se queda en 14
  donde el prototipo dibuja 15, porque subirlo movió la entrada de biblioteca **44 px** hacia abajo en
  6 de las 36 combinaciones de `HomeLayoutTests` —1366 × 768 a escala 150 en español, la más apretada
  que la aplicación admite— al hacer envolver una línea. La ganancia eran **0,55 px** de tinta.

  **Y una puerta que nació de un descuido de ese mismo barrido**: al revertir el play en dos vistas,
  `MovieDetailsView` se quedó en el tamaño nuevo, dejando el mismo botón «Reproducir» a 15 en la ficha
  de una película y a 14 en las otras cuatro.
  `The_play_of_a_catalogue_action_is_one_size_in_every_view_that_draws_it` lleva las cinco en tabla
  cerrada y nombra a la descolgada.

  **Y una afirmación falsa, corregida donde había viajado.** Se dijo que el prototipo no usa el tamaño
  22 en ningún sitio; lo usa, en el conmutador de reproducción. El error fue **inferir una ausencia de
  un patrón**: exigía una cadena literal como primer argumento de `icon(n, s)` y **diez llamadas pasan
  una expresión**, entre ellas `icon(p.playing && !err ? 'pause' : 'play', 22)`. Corregido en
  `ELEMENTS`, en la evidencia y en la nota. La puerta nueva
  —`Every_size_class_is_its_own_number_and_one_the_prototype_spends`— lee los tamaños del prototipo
  con el patrón que sí acepta expresiones y exige que **el nombre de cada clase sea su `Width`** y que
  ese número sea uno que el prototipo gaste.

- **Los iconos vuelven a su lienzo de 24 × 24, y con eso se van tres defectos a la vez.** Al portarlos
  del prototipo el 2026-08-24 se copió el trazo y **no el `viewBox`**, así que los límites de cada
  geometría pasaron a ser los de su propia tinta. `Stretch="Uniform"` escala por esos límites hasta
  llenar la caja y ancla lo que sobra arriba-izquierda, de modo que cada icono se agrandaba **por un
  factor distinto** y se descentraba lo que le sobrase.

  Medido rasterizando: los iconos salían entre **1,12× y 1,74×** más grandes que el prototipo —una
  dispersión de 0,62— y hasta **4,5 px** descentrados, y **casi todos medían lo mismo en pantalla**
  (16,8 px) fuera cual fuera el tamaño que el prototipo quiso para cada uno. Ahora el exceso va de
  **0,90 a 1,06** y veintitrés de los treinta y uno están a **+0,00**.

  La corrección es el prefijo `M0 0 M24 24` delante de los 31 trazos: dos `moveto` que **no dibujan
  nada** —comprobado rasterizando, porque un remate redondo sobre un subtrazo de longitud cero es
  justo como aparece un punto donde no se dibujó— y que devuelven la caja a `0,0 24×24`. Con el
  lienzo puesto, el `-2` de las clases de tamaño se retira: `size-20` es `Width="20"`, que es
  literalmente `icon(n, 20)` del prototipo. Ese `-2` era el exceso corregido a ojo con una resta fija
  contra factores que iban de 1,12 a 1,74, así que no podía funcionar.

  **Y arregla una puerta cuya premisa era falsa sin decirlo**: `TransportGlyphTests` comprobaba el
  grosor con `1,6 · Width / 24`, que **asume que la tinta llena el lienzo**. Lo era para ninguna de
  las 31, y la puerta pasaba igual. Evidencia:
  [el lienzo que la portación no copió](evidence/stable/audit-icon-canvas.md).

### Añadido

- **Marcar una lección lo dice en voz alta, en una sola zona viva.** Marcar mueve el hilo, y el hilo
  es el sentido de la ficha: quien lee con los ojos ve el distintivo saltar a la fila siguiente, y
  quien lee con un lector de pantalla no recibiría **nada**, porque un glifo que cambió en otro sitio
  de la página no es un anuncio. Va como frase, en **un solo** `Border` con `LiveSetting="Polite"`, y
  se dice **después** de releer la ficha: la frase afirma que el hilo se movió, y sólo se ha movido
  cuando la ficha se ha vuelto a leer. Se limpia en cada carga, para que reabrir un curso no vuelva a
  anunciar una marca de la vez anterior.

- **Estados de alcance movidos con lo que hay medido**: `CRS-002`, `CRS-003` y `CRS-005` pasan a
  `IMPLEMENTED` con su evidencia enlazada; `CRS-001` a `IN_PROGRESS`, porque marcar una carpeta aún
  no tiene puerta; `CRS-004` sigue en `DESIGN_APPROVED`. Para que las tres lleguen a `VERIFIED` falta
  la matriz de capturas junto al prototipo y una prueba de que el progreso de una lección **sobrevive
  a mover el archivo** — hoy eso está garantizado por construcción y no demostrado.

- **Novena discrepancia §4↔árbol, y ésta es de vocabulario.** `CourseLastOpenedFormat` es «Última vez
  hace {0}», y ese `{0}` pide unidades —«3 días», «2 semanas»— que **el paquete no trae y el árbol no
  tiene**: medido, no hay una sola cadena de tiempo relativo en las 711. Escribirlas sería inventar
  copia, que es del dueño. La clave se queda declarada y sin pintar, y la tarjeta dice el progreso y
  el restante, que es lo que hace falta para decidir. **Quedan 12 claves más sin consumir**, y las 12
  son de los dos tramos que faltan: cuatro del diálogo de marcado, tres del menú del riel y cuatro
  del panel del reproductor.

- **Y una corrección de lo dicho antes en este mismo registro.** Se escribió que los dos consumidores
  que tratan `CatalogTitleKind.Course` por su rama por defecto —`CatalogItemViewModel.KindKey` y el
  encaminamiento de `LibraryViewModel`— «se cierran en el tramo que trae las vistas». **No se
  cerraron, y ahora se sabe por qué**: los cursos viven en sus propias tablas y **nada escribe un
  título de tipo curso**, así que esas dos ramas son hoy **inalcanzables**. Añadirles un brazo sería
  añadir código que ninguna prueba puede tomar, que es justo lo que este árbol evita. Se cierran el
  día que un curso aparezca en la biblioteca, y no antes.

- **La ficha del curso y su fila de lección, con el hilo dentro.** `CourseDetailsView` se apila bajo
  la cuadrícula —el patrón que la biblioteca y Duplicados ya usan: lista arriba, detalle abajo—, así
  que volver de un curso es subir con la rueda y no un botón que alguien tiene que encontrar. Su
  título es de **nivel 2**: el nivel 1 del destino es de la cuadrícula, y dos en un mismo destino es
  el defecto que la columna de Ajustes ya costó una vez.

  El panel del hilo va en la **columna fija de 320 px de un `Grid`** y no pegado al viewport, que es
  lo que el paquete de diseño pide y lo que este marco permite: AXAML no tiene `position: sticky`, y
  la columna de Ajustes ya resolvió esa misma forma poniendo el `ScrollViewer` en una columna y
  dejando la otra quieta.

  `LessonRowView` es el espejo de la fila de episodio: los mismos tres estados, los mismos glifos
  **○ ◐ ●** —forma antes que color, para quien no distinga el acento del texto secundario—, la misma
  barra parcial debajo, y una marca que la mano gana. Los glifos salen del árbol de automatización
  porque el nombre de la fila ya dice el estado con palabras: anunciado dos veces se lee dos veces.

- **Marcar una lección vista escribe donde PLY-008 ya escribe.** No hay caso de uso nuevo: se llama a
  `SetWatchStatus` con la clave de `CourseProgressKey`, así que la marca de una lección es un estado
  de visto como cualquier otro — que es lo que hace que sobreviva a mover el archivo.

- **La escena del paseo existe y pulsa las seis cosas** con el ratón: marcar carpeta, abrir el curso,
  marcar una lección, retomar el hilo, reproducir una lección y continuar desde la tarjeta. **La
  marca se comprueba contra el almacén, no contra el glifo**, porque un glifo sólo demostraría que la
  fila se volvió a dibujar. El trinquete del paseo **se queda en 20**: 238 controles declarados, 213
  pulsados, y ninguno nuevo en la lista de pendientes.

- **Y una que faltaba y no se veía.** `IsCoursesVisible` no estaba entre las propiedades que el shell
  anuncia al navegar, así que el destino existía, la ruta cambiaba y **la pantalla no se dibujaba**.
  La encontró el paseo al no hallar el botón, que es exactamente para lo que sirve pulsar con el
  ratón en vez de comprobar un booleano.

- **Cursos es el tercer destino del riel, y la cuadrícula existe.** `CoursesView` con su estado
  vacío en positivo, sus tarjetas de 280 px con progreso, restante y la etiqueta que el estado gana
  —«Continuar · M2·L06», «Retomar · …», «Terminado · abrir» o «Se abrirá al escanear», que son cuatro
  ofertas distintas y no cuatro maneras de decir lo mismo—, y la nota de detección al pie.

  La acción líder es **marcar una carpeta**, y vive en la cabecera y no dentro de la caja vacía: una
  oferta que desaparece en cuanto empieza a funcionar es una oferta que alguien tiene que ir a
  buscar. Los botones de las tarjetas no van acentuados, porque una cuadrícula donde toda tarjeta
  grita no tiene acción líder.

- **El icono se convirtió del prototipo, no se dibujó.** Su `course` son dos rectángulos redondeados
  y un triángulo de reproducción; los dos rectángulos pasan a los arcos que los dibujan, como ya se
  hizo con los otros diez, y el triángulo se traza en vez de rellenarse porque es lo que `IconPlay`
  hace con el suyo. Una tradición de dibujo por riel.

- **Cinco puertas declaradas se mueven, ninguna se afloja**: las rutas del shell y el contrato de
  navegación pasan de cinco destinos a seis —afirmados **por nombre y en orden**, no por conteo,
  porque una ruta que entrara en el enum donde el riel no la dibuja seguiría pasando un conteo—, el
  inventario de iconos suma el suyo, `LeadingActionTests` gana su fila, y el recorrido de teclado de
  accesibilidad pasa de cinco botones a seis, cada uno pulsado y comprobado contra la ruta que abre.

- **Y una que saltó y tenía razón.** `ServiceConsumptionTests` rechazó dos registros nuevos:
  `MarkCoursesInRoot` y, colgando de él, `ICourseRootDeclarationStore`. Nada los resuelve hasta que
  el diálogo de añadir medios ofrezca «Curso (carpeta de lecciones)», y un servicio que nadie resuelve
  es el defecto característico de este repositorio. **Salen del contenedor** y entran en el tramo que
  los alimenta, con el porqué escrito donde estaban.

  El comando de marcar del riel **no construye una puerta propia**: el shell le pasa la suya, la del
  diálogo de añadir medios, igual que hace con el abridor de la lista de duplicados. Una segunda
  puerta a la misma habitación es una que puede desviarse de la primera.

- **El hilo de un curso, y el progreso que ya existía.** `CourseProgressKey` no inventa un almacén:
  guarda la posición de una lección bajo la clave que PLY-008 ya usa, con el curso donde va un título
  y la lección donde va un episodio. Así reanudar, el umbral de visto de PLY-009, la marca manual que
  gana sobre él y la cuenta atrás de PLY-011 siguen funcionando **sin saber que existen los cursos**.
  Inventar una tabla `lesson_progress` habría sido una segunda respuesta a «¿por dónde iba?», y dos
  respuestas a una pregunta es como empiezan a contradecirse.

  `CourseThreadPolicy` decide dónde apunta el hilo, y la regla es la que usaría una persona: **la
  primera lección en orden que no está vista**. No la última reproducida —eso devolvería a alguien a
  una lección que ya terminó— ni la más avanzada, que tras un salto hacia adelante abandonaría en
  silencio todo lo de en medio. De ahí salen también el restante —que cuenta entera cada lección sin
  ver y **sólo lo que queda** de la que está a medias— y «Lo último que viste», que son las dos
  últimas vistas **antes** del hilo y no las dos últimas del curso.

- **Un curso vacío no está terminado**, y se pregunta aparte por eso: una carpeta recién marcada cuyo
  recorrido no ha corrido todavía se dibujaría como completa, felicitando a alguien por un curso que
  nadie ha leído.

- **La duración de una lección se une, no se copia.** El paquete de diseño la pedía como columna de
  `lessons`; sale del archivo que el catálogo ya sondeó, porque una copia es algo que se queda
  desfasado. `CourseLessonReader` responde la tarjeta con **un solo `SELECT`** sobre las tres tablas
  que la contestan —la lección, el archivo que le da longitud y el estado de visto—, con los dos
  `JOIN` a la izquierda: una lección sin archivo sondeado no tiene longitud todavía y una que nadie
  ha abierto no tiene estado, que es exactamente lo que significa «sin empezar».

  **Y lo que había que medir era la clave**, porque se compone en dos sitios: en SQL por
  concatenación y en C# desde `ContentKey`. Hay una prueba de integración que **escribe por uno y lee
  por el otro** contra una base real.

- **El número de una lección cuenta a lo largo del curso y no del módulo.** El prototipo escribe
  «L06» junto a una lección del módulo 2, y una numeración que reiniciara por módulo llamaría igual a
  dos lecciones distintas.

- **La migración `0022` y lo que la escribe, en el mismo cambio.** `courses` y `lessons`, más una
  sola columna nueva en `library_roots`: `course_depth`. Esa columna es **las dos decisiones del ADR
  a la vez** — una raíz tiene cursos exactamente cuando tiene profundidad, y su valor es a qué nivel
  están—. Dos columnas, una bandera y una profundidad, podrían contradecirse; y una raíz que dijera
  «tengo cursos» sin decir dónde sería justo la que obliga a adivinar.

  `lessons` no lleva progreso y no lo llevará: el progreso es el almacén de PLY-008 y el umbral de
  visto es el de PLY-009, y un segundo almacén sería una segunda respuesta a la misma pregunta. Lo
  que sí lleva es `media_file_id`, que es la identidad de LIB-009 — por eso mover o renombrar el
  archivo conserva el progreso—, **anulable y con `ON DELETE SET NULL`** en vez de en cascada: una
  lección cuyo archivo desapareció es una lección que falta, y eso una vista tiene que poder decirlo;
  borrar la fila la convertiría en una lección que nunca existió.

  `MarkCoursesInRoot` reutiliza `IMediaFileEnumerator` en lugar de abrir una segunda forma de recorrer
  una carpeta: un solo enumerador es un solo juego de códigos de error y un solo sitio donde se toca
  el disco. No sale a la red —no hay nada que identificar—, no copia, no mueve y no renombra.

- **Cuatro afirmaciones de `SqliteBootstrapTests` pasan de 21 a 22**, y con ellas la lista de nombres
  y la de tablas. El ADR decía tres y son cuatro: el conteo, el máximo, las copias previas y el
  conteo tras la segunda pasada.

  **Y la lista de tablas enseñó algo que no se deduce leyendo**: `lessons` va **antes** que
  `library_roots`, porque la consulta ordena con la colación binaria de SQLite y ahí `e` va antes que
  `i`. Puesto en orden alfabético «de leer», la prueba se pone roja en el índice 13.

- **Tres defectos los encontraron las pruebas antes que nadie**, y ninguno se veía leyendo:

  1. `CourseStructurePolicy` normaliza las rutas a `/` y el caso de uso indexaba el diccionario con
     las barras tal y como venían del enumerador. El mapa se construía con unas claves y se leía con
     otras, así que la primera lección lanzaba `KeyNotFoundException`.
  2. El módulo de una lección guardaba el **nombre de la carpeta** (`01 - Módulo uno`) donde la ficha
     pide el **título** (`Módulo uno`), porque el número ya viaja aparte en `module_sort_major` y
     «Módulo {0} · {1}» los quiere separados.
  3. En `ORDER BY`, **SQLite pone los NULL primero**, así que una lección sin número de cabecera
     habría abierto todos los cursos. La consulta ordena por `sort_major IS NULL` antes que por
     `sort_major`, y hay una prueba que lo fija.

- **`CatalogTitleKind.Course` y las dos políticas puras que deciden qué es un curso y en qué orden se
  ve.** `CourseStructurePolicy` lee los cursos de una raíz **a la profundidad que la raíz declara**, y
  no adivina: a esa profundidad la carpeta es el curso, una subcarpeta con vídeo es una sección, lo
  que cuelgue por debajo se aplana contra ella, y un vídeo más arriba de esa profundidad no pertenece
  a ningún curso. Que una carpeta de recursos sin un solo vídeo no sea sección sale gratis por
  alimentarla con rutas de vídeo en vez de con un listado de directorio, y no es un detalle: de 1955
  archivos medidos en una colección real sólo 595 eran vídeo.

  `CourseLessonOrderPolicy` ordena por el número de cabecera de `NN -`, `NN-`, `NN.` y `NN_`, que
  cubren el 80,8 % de 595 lecciones medidas, y **conserva `N.N` como par ordenado** en vez de
  destruirlo — hoy el limpiador de nombres de cine convierte `1.3 Título` en `1 3 Título`, que
  destruye el orden y el título de una vez—. El 19,2 % restante no se toca: va al final en orden
  alfabético estable, que es exactamente lo que ordena bien los esquemas codificados con relleno de
  ceros. Y **el número se lee hasta tres dígitos**, porque cuatro son un año: ése es el límite que
  evita reproducir el falso positivo que el parser de películas comete sobre la misma colección.

  Las dos entran a **100 % de líneas y de ramas**, medido sobre el Cobertura de la propia suite, que
  aquí es representativo porque nadie más las ejecuta todavía.

- **El tercer tipo de título se añade al FINAL del enum, y eso no es estética.** `CatalogTitleKind` se
  escribe en SQLite como su ordinal —`(CatalogTitleKind)reader.GetInt32(1)`—, así que poner `Course`
  donde mejor se lee, junto a `Movie` y `Show`, habría renumerado `Unidentified` de 2 a 3 y **cada
  título sin identificar de cualquier base ya existente habría vuelto convertido en curso**, en
  silencio y en la primera lectura. El porqué queda escrito en el propio enum, que es donde alguien
  irá a reordenarlo.

  **Dos consumidores siguen tratando el valor nuevo por su rama por defecto, y se dicen aquí para que
  no se pierdan**: `CatalogItemViewModel.KindKey` lo resolvería como `CatalogKindFile` y
  `LibraryViewModel` lo llevaría a la ficha de película. Ninguno es alcanzable todavía porque nada
  construye un título de tipo curso, y los dos se cierran en el tramo que trae las vistas. Un `switch`
  con `default` es justo donde un tipo nuevo se pierde sin ruido, que es lo que el ADR ya avisaba.

- **Las cadenas de Cursos entran en los dos archivos, y son 42, no las 41 que el paquete anuncia.**
  `Strings.es.axaml` y `Strings.en.axaml` pasan de **668 a 710 claves cada uno**, en el mismo orden y
  sin una sola diferencia entre ellos. La cifra se contó del documento en vez de leerse de su
  encabezado, y por eso se sabe que no cuadraba: el grupo «Ficha del curso y su hilo» se rotula 15 y
  enumera 16, porque dos de sus filas empaquetan una pareja de claves en una celda —«Marcar como
  vista / Quitar la marca» y sus dos avisos— y una de las dos parejas se contó una sola vez. Un
  número que nadie recuenta es el que acaba justificando una lista incompleta, así que la corrección
  va como comentario dentro de los dos archivos, donde la vuelve a leer quien añada la siguiente.

  Aún no las consume nadie, y eso es deliberado: `PROMPT.md` pone las cadenas primero porque el
  bilingüismo se comprueba por pareja de archivos y una clave nueva va en los dos o no va. Los tramos
  que vienen —modelo, marcado, las cuatro vistas y el panel del reproductor— son los que las gastan.

  **Y la puerta que las prohibía se acota, no se quita.** `ScopeBoundaryTests` tenía una fila que
  vetaba las palabras «Curso», «Course», «Leccion» y «Lesson» en cualquier clave de recurso, y con
  ellas vetaba catalogar una carpeta de vídeos numerados que ya está en el disco, que es a lo que
  esta aplicación se dedica. Lo que la frase de la hoja de ruta protege es la **plataforma**, así que
  los marcadores pasan a ser las palabras que el producto tendría que usar si matriculara a alguien,
  certificara algo o llevara un expediente: matrícula, certificado, diploma, cuestionario, racha,
  progreso de formación, porcentaje completado, estadísticas de estudio. «Badge» se queda fuera de la
  lista a propósito, porque diez claves existentes lo llevan empezando por `UnavailableBadge`. Y en
  la puerta del esquema, `courses` sale de la lista de tablas prohibidas y entran las cuatro que sí
  serían una plataforma: matrículas, certificados, cuestionarios y rachas. Una tabla que se publica
  no se puede deshacer.

- **Cursos entra en la matriz como `CRS-001`…`CRS-005`, y el no-objetivo que lo prohibía se acota en
  vez de borrarse.** `ADR-0006` pasa a `ACCEPTED`: un curso es un tercer tipo de título, una raíz se
  **declara** de cursos y **declara a qué profundidad** están, y el programa no adivina ni una cosa ni
  la otra. Adivinarlo se intentó y se midió que no funciona: la regla candidata —hoja con vídeo, curso
  como ancestro a distancia 0 o 1, secciones por número de cabecera— devolvió **31 cursos donde hay
  12** sobre una colección real de 595 vídeos, y sus cuatro modos de fallo son todos reales. Con la
  profundidad declarada la detección es exacta por construcción, y no queda heurística que mantener.

  La hoja de ruta decía «no es una plataforma de cursos. No hay lecciones, ni progreso de formación,
  ni certificados», y esa frase **se acota, no se borra**: sigue fuera todo lo que la motivaba
  —matrículas, certificados, cuestionarios, rachas, estadísticas de estudio, porcentaje de formación
  completada— y entra lo que la aplicación ya hace con una serie. El progreso es el que existe
  (`PLY-008`, `PLY-009`) y la identidad es la que existe (`LIB-009`): no se inventa un segundo
  almacén.

  La puerta de documentación pasa de **60 a 65** identificadores declarados. Esa cifra se afirma en
  vez de dejarse abierta justo para esto: una fila nueva en la matriz obliga a tocar el guion, que es
  donde alguien se entera de que los documentos localizados también la necesitan.

- **El paquete de diseño se pone al día, y trae 57 vistas en 57 archivos.** El proyecto remoto no se
  leía desde el 17-08-2026 y desde entonces ha declarado el rediseño cerrado en este árbol y ha
  abierto una sola propuesta. Llegan `design/vistas/` —un archivo por vista, nueve líneas cada uno,
  que abren el prototipo ya situado en esa vista por la prop `view`, así que hay una sola fuente y
  ninguna divergencia—, las claves de Cursos en `Cadenas nuevas` con su texto en los dos idiomas,
  y `README`, `PROMPT` y `github` reescritos para esta fase. `Auditoría del inventario` se retira
  porque el proyecto la retiró.

  **El prototipo navegable NO se actualiza, y es una limitación del transporte y no una elección**:
  la herramienta de diseño corta una lectura en 256 KiB y el archivo es mayor, así que volvió
  truncado en exactamente 262144 bytes. Cambiar un prototipo completo de agosto por uno nuevo y roto
  es cambiar un archivo viejo por uno que no abre, así que se queda el que abre.

- **Un barrido que le da un null a cada constructor, y con él 303 guardas que nunca se habían
  tomado.** Noventa de los doscientos cinco archivos por debajo del listón de cobertura lo estaban
  por una sola forma repetida: un `?? throw new ArgumentNullException` que ninguna prueba había
  ejercido nunca. Un `throw` que nadie toma es media pareja de ramas, y por eso **seis** archivos de
  Application medían exactamente 100 de líneas y 50 de ramas —el análisis previo decía ocho, y
  contarlos dice seis—. `ConstructorGuardSweep` construye cada
  tipo con un sustituto en todas las posiciones menos una y un null en ésa, y exige un
  `ArgumentNullException` que **nombre ese parámetro**: **127** parámetros en Application, **112** en
  Presentation y **64** en Infrastructure.

  **Lo que eso vale, medido por CI y no estimado aquí**: 66 archivos mejoran y **diecinueve alcanzan
  96/96 y salen de la lista**, que baja de 205 a **186** — el mayor movimiento que ha tenido de una
  vez. Ninguno entra: nada se degradó. Entre los que salen están **cuatro de los seis** que estaban
  clavados en 100/50; `SetPreferredVersion` sube a 100/75 y `RemoveLibraryRoot` se queda, porque su
  archivo tiene más ramas que la guarda.

  Tres exclusiones, todas estructurales en lugar de una lista, porque una lista es algo que
  mantener: los records, que son los portadores de datos de este repositorio y legítimamente no
  validan —190 de los 319 parámetros de referencia que midió el primer sondeo—; las excepciones,
  cuyo mensaje es anulable por convención de .NET; y lo que escribió el compilador, como el
  `JsonSerializerContext` del generador. Cada suite lleva además un suelo de parámetros alcanzados,
  porque la reflexión **se queda muda en vez de roja** cuando deja de casar, y ése es el fallo que
  más veces se ha medido aquí.

  **Ocho constructores siguen aceptando un null y están escritos con su motivo** —y dejan de estarlo
  en el cambio de más arriba, que los promueve—: los ocho declaran
  sus dependencias como constructor primario, que no tiene dónde poner la guarda sin un campo, así
  que se tragan el null y fallan más tarde en el primer uso —una `NullReferenceException` desde
  dentro de un método en lugar de una `ArgumentNullException` desde la composición que la causó—. Van
  en una lista cerrada como la `PendingWiring` de `ServiceConsumptionTests` —una deuda con nombre, no
  una exención—, y una segunda prueba fuerza la salida de cada entrada en cuanto su guarda llega.

  **Y una firma que mentía, corregida de camino**: `RecommendationItemViewModel` declaraba su título
  como `string` no anulable y lo trataba con `title ?? string.Empty`. Ahora la firma dice lo que el
  cuerpo hace.

- **Un cuarto hook: armar el vigía de CI deja de ser una frase.** «Para mirar CI se usa
  `eng/watch-ci.ps1`, nunca un bucle a mano» estaba escrito en `CLAUDE.md` desde hacía tandas, y el
  2026-08-30 el propietario tuvo que pedirlo igualmente. `post-push.sh` corre en `PostToolUse` sobre
  `Bash|PowerShell` y, tras cualquier `git push`, escribe por stderr y sale con código 2 el comando
  del monitor **con el SHA ya resuelto**, listo para copiar.

  **Y la queja era correcta aunque el monitor estuviera vivo**, que es lo que hacía falta medir antes
  de responder: `TaskOutput` lo daba como `running` con **0 KB de salida**, porque el guion late cada
  30 minutos y el push llevaba menos. **Corriendo y callado se ve exactamente igual que no existir** —
  la misma trampa que este repositorio persigue en sus puertas, esta vez vista desde fuera. Medido por
  tubería con tres casos, uno que debe sonar y dos que deben callar, y luego disparado de verdad.

  **Y entonces sonó en su propio commit**, que es lo que enseñó a escribirlo bien: buscaba la cadena
  suelta y el mensaje —un heredoc— hablaba de «after any git push». Ahora **tira los heredocs primero**
  y exige `git push` en **posición de comando**. Diez casos por tubería: cuatro que suenan, seis que
  callan, incluidos ese heredoc y `git pushd`. Un aviso que suena cuando no toca enseña a ignorarlo,
  que es peor que no avisar.

- **La capa `Domain` llega al listón de cobertura, y dos archivos no pueden: su techo está medido.**
  Los nueve que estaban por debajo de 96/96 tenían entre todos **15 ramas y 4 líneas** sin cubrir. Se
  cierran 12 ramas y las 4 líneas, y siete archivos alcanzan el listón —`RenameOperation`,
  `RootRemapPolicy`, `MetadataMergePolicy`, `RecommendationPolicy`, `RecommendationModels`,
  `IMetadataProvider` y `MediaNameParser`—. 24 pruebas nuevas, todas sobre guardas que existían y a
  las que nadie llamaba por su lado falso: que un renombrado cuyo destino es su propio origen **no**
  es la excepción que deja sobrescribir un archivo existente, que un plan sin operaciones no se
  ejecuta aunque no tenga conflictos, que un conflicto entre dos raíces deja en paz a la tercera, que
  el remoto sin año no borra el año guardado, y que dos gustos del mismo tamaño con claves distintas
  no son el mismo gusto.

  **Las tres ramas que quedan no las puede tomar ninguna entrada, y eso se leyó en vez de suponerse.**
  La de `SegmentDetectionPolicy` es un `dup; brtrue.s` en el offset 139 del IL —el caché del delegado
  del `GroupBy`, alojado en la clase de cierre que el método crea **una por llamada**, así que el
  campo nace nulo y el salto no se toma jamás—. La de `MatchModels` exigiría que `GetRelativePath`
  devolviera una cadena vacía, y ese camino **lanza** antes. La de `MediaNameParser` exigiría una
  temporada negativa, y los tres patrones capturan `\d{1,3}`. Las tres quedan escritas en la prueba
  donde se buscarán la próxima vez, no sólo en la evidencia.

  **El trinquete baja de 212 a 205**, y ese número lo dijo CI, no esta máquina: la lista se copió del
  artefacto `coverage-debt` del run que la midió. Su diff contra la anterior mueve **ocho filas y
  ninguna más** —siete que salen y `MatchModels` que sube de 80 a 90—, que es como se ve que nada del
  resto del árbol se degradó. Evidencia en
  [audit-domain-coverage.md](evidence/stable/audit-domain-coverage.md), con los suelos de partida
  reproducidos **exactos** desde el artefacto de un run verde, que es lo que dice que el instrumento
  mide lo mismo que la puerta.

- **El repositorio gana su propia configuracion de Claude Code, y con ella la regla 0 deja de
  depender de una maquina.** `.mcp.json` declara el MCP de Avalonia —`https://docs-mcp.avaloniaui.net/mcp`,
  sin credenciales ni rutas locales—, que hasta hoy estaba en la configuracion personal de quien
  programaba: la regla mas nueva de `CLAUDE.md` exigia consultarlo y el servidor no venia en el arbol.

  Con el, tres **puertas de proceso** que hasta ahora eran frases: un hook rechaza escribir
  `eng/coverage-debt.txt` y `eng/walk-pending.txt` —que `CLAUDE.md` declara producidos por CI y que
  nada impedia editar—, otro rechaza un fuente sin la cabecera SPDX **antes** de escribirlo, y el
  tercero avisa si se toca un `.es.md` y su pareja `.en.md` se queda como está en `HEAD`. Dos skills,
  `/cerrar-tanda` y `/medir-pixeles`, y dos agentes, `gate-auditor` —que busca puertas que pasan sin
  medir nada— y `prototype-fidelity`.

  **Los seis casos de los hooks se probaron uno a uno antes de escribirlos**, y el pipe-test cazo un
  defecto que ninguna lectura habria visto: `jq` en Windows emite **CRLF**, asi que la ruta llegaba
  terminada en `
`, ningun patron casaba y el hook habria callado siempre — que es la forma que ya
  tiene el defecto de esta casa.

  **Y el tercero se midió antes de creerlo, que es como se descubrió que sonaba siempre.** Comparaba
  `mtime`, y como cada escritura es una llamada aparte el `.es.md` queda siempre más nuevo que su
  pareja: avisaba **también cuando los dos idiomas se habían tocado**, que es justo el trabajo bien
  hecho. Ahora pregunta a git —el `.es.md` difiere de `HEAD` y el `.en.md` no—, probado con **diez
  casos** de los que cuatro son de los que debe dejar pasar.

  **Y sólo avisaba cuando el archivo se reescribía entero.** Su matcher era `Write` a secas, así que
  toda edición quedaba muda — y `Edit` es justo la herramienta con la que se toca un `.es.md` que ya
  existe, porque `Write` reescribe el archivo completo. La guarda cubría el camino menos transitado.
  Medido con el mismo `Edit` antes y después de ampliar el matcher a `Edit|Write|MultiEdit`: mudo
  primero, avisando después, y **en caliente**, sin recargar la sesión. El del SPDX se queda en
  `Write`, y ahí no es un descuido: lee `tool_input.content`, que un `Edit` no trae entero.
  `MultiEdit` queda declarado en el matcher pero **sin medir**.

  **El silencio hubo que medirlo por otra vía, porque dentro de la aplicación no se distingue de no
  haber corrido**: un hook deja rastro en el registro de la sesión **sólo cuando produce salida**. Con
  los dos idiomas tocados, el comando literal del `settings.json` calla por tubería — con el caso que
  sí debe sonar al lado, que es lo único que hace valer al que calla.

  **Y lo que el aviso no hacía, medido el mismo día: no llegaba a la pantalla.** Con el propietario
  delante del PC, los dos avisos provocados a propósito pasaron sin que viera nada, y un
  `systemMessage` tampoco entra en el contexto del agente que escribe. Se emitían para nadie: corrían,
  acertaban, y su salida moría en el registro de la sesión.

  **Así que se cambió el canal.** Los dos avisos del `PostToolUse` escriben ahora a stderr y salen con
  código 2, que sí llega al agente que escribe el archivo — medido con el caso conocido al lado del
  desconocido, y después por tubería con **siete casos, cuatro de ellos de los que debe dejar pasar**.
  Y como llega etiquetado de «error» tras una escritura que sí funcionó, los tres mensajes **empiezan
  diciendo que la escritura no falló** — sin eso, el aviso se lee como un fallo y se reintenta la
  misma escritura.

  **Su precio se midió y después se bajó.** El harness imprime el comando entero dos veces delante del
  texto, así que en línea costaba **2.712 caracteres de contexto por aviso**. El comando vive ahora en
  `.claude/hooks/post-write.sh` y el `settings.json` sólo lo llama: **488 caracteres**, un 82 % menos,
  leídos del registro y no calculados — la cuenta a mano decía 528. Los otros dos hooks se quedan en
  línea porque son cortos y **deniegan**, así que su texto nunca se imprime dos veces.

  **Y el mismo archivo apaga ahora los conectores de nube de claude.ai en este repositorio.** Medidos
  desde el contexto de una sesión: costaban **298,8k fichas, el 30 % de la ventana, y 212,9k eran de
  un solo conector** — 102 herramientas de anuncios que este proyecto no llama nunca.
  `disableClaudeAiConnectors` la gana cualquier fuente que la ponga en `true`, así que el proyecto se
  sale sin tocar la configuración personal de nadie; `avalonia-docs` no se ve afectado porque viene
  del `.mcp.json` local. La puerta actúa al arrancar, así que se verifica con `/context` en la sesión
  siguiente y no en la que lo escribe.

  **Y tres afirmaciones que este árbol hacía sobre sus propias herramientas eran falsas.** `CLAUDE.md`
  decía que el trinquete del paseo estaba en **0 y no volvía a subir**; está en **20** y subió el
  2026-08-25, por el arnés y no por la aplicación. `eng/walk-pending.txt` abría diciendo que estaba
  **vacío** mientras enumeraba veinte entradas más abajo — un párrafo viejo que se quedó al lado del
  nuevo. Y el hook que rechaza escribirlo daba como motivo **«lo produce CI»**: `ci.yml` publica
  `eng/coverage-debt.txt` y nada más, así que el rechazo mandaba a quien tuviera un cambio legítimo a
  esperar un artefacto que no llega nunca. Los dos rechazos llevan ahora motivos separados, y el del
  paseo dice lo que cuesta de verdad subir el trinquete. Medido de paso: un `deny` llega **sólo con
  su motivo**, sin el comando —al revés que un aviso de `PostToolUse`—, que es la razón de que esos
  dos se queden en línea.

  **Y los dos servidores locales que fallaban en cada arranque quedan denegados para este proyecto.**
  `gbrain` (`REQUEST_TIMEOUT`) y `MCP_DOCKER` (`CONNECTION_CLOSED`) van a `deniedMcpServers` en vez de
  borrarse de la máquina: son de quien programa y se usan fuera de este repositorio. Ésta sí se pudo
  medir en el acto, al revés que la clave de los conectores — `claude mcp list` enseñaba tres
  servidores antes y enseña sólo `avalonia-docs` después.

- **El cromo del minirreproductor es la composición del prototipo: barra de progreso, título y
  reloj.** La franja eran cinco botones y nada más, así que una ventana flotante no decía **qué** se
  estaba viendo ni **por dónde iba** — que es la mayor parte de para qué existe una imagen sobre
  imagen. Ahora lleva la barra de tres píxeles del prototipo cruzando el ancho, el título a la
  izquierda y debajo `posición / duración · velocidad`, con los cinco a la derecha en el orden que el
  prototipo dibuja: atrás, reproducir, adelante, ampliar, cerrar.

  **La pista de la barra no se va nunca y sólo el relleno aparece.** La ventana responde a un
  arrastre poniendo 16:9 sobre la imagen y sumando la altura del cromo encima, y ese manejador sólo
  corre en un arrastre: una barra que apareciera al llegar la duración movería la imagen debajo de
  una ventana que nadie ha tocado. El relleno sí espera, por la razón que `DurationSeconds` lleva
  escrita — responde 1 mientras no se sabe, así que cincuenta y dos minutos contra ese máximo se
  recortan y pintan una barra **llena** sobre una película que acaba de empezar.

  El reloj es **una** cadena del modelo y no tres enlaces seguidos: los separadores son puntuación
  que ningún diccionario guarda, y una fila de tres con el del medio vacío deja la puntuación
  colgando, `0:12 /  · 1×`. Evidencia:
  [el cromo del minirreproductor](evidence/stable/audit-mini-player-band.md).

- **El cabecero de las dos fichas dibuja el póster de verdad, con el arte generado debajo.** `PosterPath`
  se producía, se fusionaba y se persistía desde el principio, y **ninguna vista lo leía**: un valor
  sin lector, que es el defecto característico de este repositorio visto desde el otro extremo. Cierra
  **ART-A01** (2026-08-09), que había retirado el registro de `ArtworkCache` en vez de dejarlo mudo, y
  lo cierra en el orden que aquella misma entrada dejó escrito.

  **Una ficha no abre nunca una conexión.** El puerto tiene dos miembros y son asimétricos a
  propósito: buscar sólo mira el disco, y todo lo que sale a la red está detrás de traer, que se llama
  una vez y desde la identificación — el único momento en que alguien ya ha consentido hablar con el
  proveedor. Medido: buscar antes de traer responde nada con **0 peticiones**, después responde el
  archivo con **1**, y otra dirección del mismo título responde nada todavía con **1**.

  **Una ruta de TMDB es una entrada no confiable**, así que `PosterAddressPolicy` comprueba **antes**
  de componer, por la misma razón que la política del tráiler: componer primero deja una dirección
  malformada en existencia y a partir de ahí todo lector tiene que acordarse de desconfiar. Rechaza
  una segunda barra —que sacaría la ruta del segmento del tamaño—, un `..`, una dirección entera de
  otro, un esquema, una consulta, un fragmento y codificación por porcentaje.

  Un tamaño y no dos —`w780`, y la cesión está escrita—, una descarga y **un solo descodificado** para
  las cuatro superficies: el prototipo levanta el póster de la serie a 136×204 contra el mismo muro
  sangrado que usa la película, así que es una cadena y dos vistas. Y la caché de imágenes **tiene
  tope**: un póster descodificado son unos 3,5 MB en memoria pese el archivo lo que pese, así que un
  diccionario sin límite pondría un tercio de gigabyte encima de quien pasee por cien películas. Y una biblioteca sin identificar no cambia: el arte generado es lo que ya
  enseñaba, sigue debajo, y las iniciales sólo se van cuando hay una imagen que responda por ellas.

### Cambiado

- **`ButtonOpticalCentreTests` se retira y la sustituye `ButtonPixelCentreTests`, que rasteriza.**
  No es aflojar una puerta: su método tenía un fallo demostrable — calculaba el pie de la tinta
  asumiendo **siempre** un descendente, y sobre «Guardar el informe», que no tiene ninguno,
  contestaba 2,43 px de separación donde el rasterizado mide 0,0. Sus tres afirmaciones —la palabra
  centrada en el botón, el icono centrado en el botón, y los dos en el mismo medio— están las tres en
  la puerta nueva, y ahora en píxeles y con la palabra como **parámetro**: el centro de la tinta no
  es propiedad de la fuente sino de la cadena, y va de +0,62 («Guardar el informe») a +3,82 («ppp»)
  según lleve descendente.

- **`CLAUDE.md` gana una regla 0 inquebrantable: el MCP de Avalonia antes que nada.** Se consulta
  antes de escribir AXAML o de afirmar cómo se comporta un control. Con su factura escrita: una
  hipótesis falsa perseguida hasta el final —que el render ajustaba la línea base a la rejilla, que
  la medición desmiente— y seis vueltas de compilación adivinando la API. Y su corolario, que es el
  hallazgo del día: **medir el layout no es medir lo que se ve**.

- **La prueba que afirma que los cinco del mini caben en una línea deja de suponer el otro idioma.**
  Fijaba `es-ES`, y esos cinco ya plegaron en tres filas dentro de 480×270 por una palabra
  traducida; ahora el idioma es parámetro, como en las dos puertas de ancho desde el 2026-08-26. Se
  le suma una segunda que afirma que **nada de la franja se dibuja fuera de los 320** que la ventana
  permite — `ViewOverflowTests` mide a 900, que es el mínimo de la ventana principal y la única
  anchura a la que esta vista no puede fallar. Medido a 320 en los dos idiomas: la fila de los cinco
  ocupa 252×44 en **una** fila y al título y al reloj les quedan 36 px.

- **El editor de metadatos y el renombrado seguro son una vista propia, no un panel bajo la
  biblioteca.** Eran un `TabControl` al final del propio desplazamiento de Biblioteca, así que abrir
  una de las dos herramientas ponía un panel **debajo de una rejilla entera de fichas** — hasta el
  punto de que el paseo necesitaba una ventana de 2.000 px para alcanzarlo. Ahora es la página que el
  prototipo dibuja: «Volver · Biblioteca» arriba, un cabecero de dos líneas con el título de la ficha
  a tamaño de display, **dos píldoras** y la herramienta debajo.

  **No es un sexto destino del carril, y eso está medido**: los cinco aprobados se afirman por nombre
  y el paseo llega a cada uno **por su botón del carril**, así que un sexto valor del enum habría roto
  la primera aserción y dejado al paseo navegando a un sitio sin puerta. La página **cubre** el hueco
  de Biblioteca, igual que una sesión cubre la ruta que haya debajo.

  **Y el paseo encontró un callejón el mismo día.** Con la página cubriendo la ficha,
  «Previsualizar renombrado» dejó de estar en pantalla: quien abría el editor de metadatos ya no
  tenía forma de llegar a la otra herramienta. Por eso las dos píldoras **abren** además de
  seleccionar, que es lo que hace el prototipo — y se dibujan siempre las dos.

- **El minirreproductor es una ventana flotante de verdad: sin marco, arrastrable, con la forma de la
  imagen y con memoria.** Era una ventana normal de Windows con su barra de título encima del vídeo.
  Ahora abre sin decoraciones (`WindowDecorations.None`), se mueve arrastrando la imagen, se
  redimensiona desde **cualquiera de sus ocho bordes**, y cada cambio de tamaño la devuelve a la
  relación **16:9 del prototipo** —que es la de la imagen, con la altura del cromo sumada encima, no
  la de la ventana—. Como ya no tiene marco, un borde de un píxel dice dónde acaba.

  **Y ahora recuerda dónde se dejó, entre sesiones.** `PlayerWindowCoordinator.Remember` existía desde
  el 2026-08-19 y **no lo llamaba nadie**: se guardaba en un diccionario que sólo leían sus propias
  pruebas, que es el defecto característico de este repositorio con forma de coordinador. La
  colocación se escribe al cerrar la ventana —y no al moverla: un arrastre levanta un evento por
  fotograma y esto va a un archivo— y se lee en el arranque siguiente. Una colocación que ya no cae en
  ninguna pantalla se descarta al usarla, no al guardarla: sin barra de título, una ventana en las
  coordenadas de un monitor desconectado no habría forma de recuperarla.

  **Lo que decidió cómo se arrastra fue una medición y no un razonamiento.** El primer intento dejaba
  pasar el gesto que otro control ya hubiera atendido, dando por hecho que un botón marca su propia
  pulsación: **no lo hace** —Avalonia marca el *soltar*, que es donde está su clic—, así que esa
  guarda no guardaba nada y los cinco controles del cromo habrían arrastrado la ventana en vez de
  funcionar. Lo que decide es **dónde** cae la pulsación: la imagen arrastra y la franja del cromo no.

- **El menú de velocidad es el desplegable del prototipo y ya no once filas numéricas.** Era un
  `MenuFlyout` con diez números y una undécima fila que reiniciaba: una cosa que no es una velocidad,
  dentro de una lista de velocidades, escondida detrás del clic que abre esa lista. Ahora es la
  píldora que el prototipo dibuja —la palabra en pequeño, el multiplicador en semi-negrita y el galón
  que se da la vuelta al abrirse—, sus **nueve** pasos llevan marca, nombre y nota (`● Normal · 1×`,
  `2× · más rápida`), abre **hacia arriba** porque el transporte es el borde inferior del reproductor,
  y «Volver a 1×» es un botón al lado que sólo existe mientras hay algo de lo que volver.

  Es un `ComboBox` y no un botón con desplegable, y eso lo decide el paseo y no el gusto: **nada
  dentro de un `Flyout` es alcanzable por el arnés** —las veinte entradas de `eng/walk-pending.txt`
  son exactamente eso, hijos de un flyout, y ese trinquete no sube—, mientras que un `ComboBox` se
  pulsa y se afirma sobre `IsDropDownOpen` como ya hacen los dos filtros de Biblioteca. Así el menú
  gana forma y el inventario gana **una** identidad, la del reinicio, pulsada en el mismo commit.

  Y el paso que sobraba se ha ido: la lista del prototipo tiene nueve y ésta tenía un `1,75×` que
  nadie dibujó nunca.

- **El recorrido de luminosidad de `AccentPalette` termina en el extremo de la escala en vez de
  detrás de él**, y con eso el archivo pasa de 99/93 a **100/100**. Tenía un `return` de reserva —«si
  el bucle se agota, negro o blanco»— que **ningún predicado de este archivo podía alcanzar**, junto
  con las dos ramas de la comprobación de límites que lo precedía. Y no es suerte que no se alcance:
  `EqualContrastLuminance` es la luminancia donde negro y blanco contrastan igual, y ese contraste es
  **4,58:1**, por encima del 4,5 que pide el predicado más estricto — así que uno de los dos extremos
  siempre acepta, vaya el recorrido hacia donde vaya.

  **Lo destapó la puerta de cobertura nueva, sobre un cambio de esta misma tanda**: quitar el apartado
  del anillo de foco adelgazó el archivo lo justo para que el agujero de siempre pesara por encima del
  listón, y como el archivo no estaba en ninguna lista, **antes de hoy nadie lo habría visto**.

- **Un acento elegido que cae justo sobre el anillo de foco se respeta tal cual.** Se apartaba un paso
  de la escala de luminosidad, y eso devolvía `#00599A` para un anillo `#005A9C`: otro color por el
  byte y el mismo a la vista. La pregunta abierta era si bastaba un paso o hacía falta un ratio, y la
  respuesta es **ninguna de las dos**, porque lo que se protegía ya lo protege la geometría: el adorno
  de foco son **dos anillos concéntricos en dos colores**, sostenidos a 3:1 entre sí —«dos anillos del
  mismo color son un anillo»—, así que un acento que aterriza sobre el exterior deja al interior
  dibujando la forma. Exigir un ratio habría sido peor que inútil: convierte «elegí este azul» en
  «aquí tienes otro azul» y no compra nada que la geometría no dé ya. Lo que sí sigue vigilado es el
  caso que ve todo el mundo: los cuatro diccionarios eligen su acento y su anillo a mano, y
  `ContrastTokenTests` rechaza un tema que los pinte iguales.

### Corregido

- **La corrección de los botones dejó cuatro defectos propios, y los encontró una revisión.** Los
  cuatro están cerrados:

  1. **El selector nuevo alcanzaba siete botones de sólo icono.** `Button > Panel` movía 1 px los
     dos del transporte que apilan iconos alternantes y **los cinco destinos del carril** — y ahí es
     peor, porque `navigation-destination` fija `VerticalContentAlignment="Stretch"`: el margen no
     los desplaza, **los encoge**. El lavado de selección dejaba de llenar su botón y la barra de
     acento pasaba de 26 a 25 px. Medido: **ningún `Panel` dentro de un botón lleva texto y todos
     los `Grid` sí**, así que la corrección es quitar `Panel` del selector. Estaba ahí desde antes
     como `Button > Panel > :is(TextBlock)`, donde **nunca alcanzó nada**; moverlo al contenedor es
     lo que lo despertó para hacer daño.
  2. **`ButtonInkTests` se había vuelto ciega.** Su tolerancia es de 1,5 y la constante bajó a 1,0,
     así que la banda `[-0,5 ; 2,5]` **admitía el cero**: borrar el setter entero la dejaba en verde.
     Con el cinco, el 1,5 aún rechazaba una caja centrada. Ahora la tolerancia es 0,5.
  3. **`eng/watch-ci.ps1` podía quedarse mudo para siempre**, que es la única cosa que ese guion
     existe para impedir. Su contador de respuestas ilegibles se reseteaba en cada vuelta con éxito
     de `gh`, así que iba de 0 a 1 eternamente sin alcanzar el límite, y los `continue` saltaban por
     encima del techo de tiempo. Lo alcanza un `gh` que sale con código 0 e imprime un aviso de
     actualización en stdout. Verificado con un `gh` falso: ahora sale con `UNREADABLE RESPONSE`.
  4. **El cinco seguía vivo en el desplegable.** `ComboBox.filter-pill` escribe su margen a mano en
     dos sitios, con un comentario que dice «el número es el de los botones» — y los botones habían
     pasado a uno. Dejaba la etiqueta y el valor de cada desplegable 2 px altos: la sobrecorrección
     recién retirada de los botones, reintroducida por la mitad.

- **`docs/design/ELEMENTS.es.md` decía tres cosas falsas sobre la alineación vertical, y la sección
  se reescribió entera.** Decía que la compensación era de **5 px**, derivada de una asimetría de
  **2,43 px** calculada con las métricas de la fuente, y que un margen en el `TextBlock` movía «sólo
  la palabra». El rasterizado desmiente las tres: el error en pantalla es **1 px**, los cinco movían
  la palabra **tres**, y un margen en la etiqueta **hace crecer el panel donde vive** y arrastra
  también al icono — que es la premisa que dejó 53 botones con sus dos piezas 2 px separadas.

  Importa más de lo que parece: `ELEMENTS` es **el documento con precedencia** sobre el `.axaml`, así
  que una discrepancia ahí no es una errata sino una instrucción equivocada para quien venga. La
  sección conserva al final lo que decía y por qué era falso, porque el error enseña más que la
  corrección.

- **Los botones dibujaban su icono y su palabra 2 px separados, con dos puertas verdes encima.**
  Medido rasterizando un botón real con `CaptureRenderedFrame()`: la compensación óptica de la
  etiqueta valía **5 px** y el error en pantalla era **1**, así que movía la palabra tres — de 1 px
  baja a 2 px alta. Y movía lo que no era: un margen sobre la etiqueta hace crecer el panel donde
  vive, así que en los **53 botones** con icono al lado el icono se desplazaba también, y el icono es
  geometría y ya estaba centrado al píxel.

  Los cinco venían de las métricas de la fuente —2,43 px de asimetría entre ascendente y
  descendente—, y ese número **no es el de la pantalla**. Ahora la compensación es de **1 px y va
  sobre el contenido del botón**, no sobre la etiqueta: mueve por igual todo lo que el botón dibuja y
  no puede separar dos cosas que van juntas. Medido después: el icono, la palabra y el centro del
  botón caen en la **misma fila de píxeles** con «Guardar», «Reproducir», «Añadir medios…» y «Save
  the report» — con descendente y sin, en los dos idiomas. Evidencia:
  [el píxel contra la caja](evidence/stable/audit-button-pixel-centre.md).

- **En los dos temas claros, el cromo del minirreproductor era invisible.** La franja pintaba
  `ShellSurfaceBrush` y sus botones toman `PlayerTextBrush` de la clase `player-chrome`, y esos dos
  son el mismo color: medido sobre la vista montada, **1,02:1** en claro (`#F8FAFC` sobre `#FBFCFE`)
  y **1,00:1** en alto contraste claro, blanco sobre blanco. Cinco botones sin nada visible encima.

  **Ninguna puerta lo veía, y la razón importa**: todas las de contraste leen los cuatro
  diccionarios, y un diccionario es coherente consigo mismo — `ShellSurfaceBrush` es correcto como
  fondo del shell y `PlayerTextBrush` como tinta del reproductor. Lo que estaba mal era el
  **emparejamiento**, y un emparejamiento sólo existe en una vista. La corrección es la del
  prototipo, que pinta el mini entero sobre `#0B0D10`, y `MiniPlayerBandTests` mide el
  emparejamiento en los cuatro temas: 3:1 para los glifos y 4,5:1 para las dos líneas de texto.

- **Inicio no se desplazaba, y en la ventana más pequeña se perdían 83 px por abajo.** Era el único
  destino montado en un `ContentControl` pelado mientras Biblioteca, Revisión, Duplicados y Ajustes
  tenían cada uno su `ScrollViewer`. Con `MinHeight` en 600 y `HomeView` pidiendo **683**, el final de
  Inicio era contenido que nadie podía alcanzar.

  **Es el hallazgo «secciones cortadas por el ancho», y no era el ancho.** El eje horizontal estaba
  descartado y medido —ninguna de las 48 vistas se pasa de los 836 px que da el shell—, así que
  quedaban cuatro hipótesis; ésta era la tercera. Ahora hay una puerta que la vigila: **una vista más
  alta que la ventana tiene que estar dentro de algo que se desplace**, leído del árbol del shell y no
  de una lista escrita a mano.

  De paso, las dos puertas de ancho **miden ahora en los dos idiomas**. Fijaban `es-ES` como
  constante, así que el otro idioma era una suposición — y el cromo del mini ya plegó en tres filas
  una vez por una palabra inglesa más larga.

- **El año de una película escaneada llegaba a Biblioteca y no a Inicio.** Las dos superficies leen la
  misma proyección con **la misma consulta escrita dos veces** —cada una lleva su propia copia de
  «oculta el archivo que ya es un episodio»— y la columna del año, que llegó con la migración 0021, se
  puso en una y no en la otra. Ahora hay una aserción que las ata: el mismo título y el mismo año en
  las dos, sobre el mismo escaneo.

- **La puerta de desbordamiento medía cada vista sola a 900 px**, que es más ancho del que ninguna
  recibe: dentro del shell el carril se lleva 64. Era **la primera de las dos limitaciones que esa
  puerta declara de sí misma**, y es la forma exacta de «secciones cortadas por el ancho» del encargo
  original. Ahora se miden también contra el hueco real —**836 px, tomados del shell y no escritos a
  mano**—, el reproductor incluido, porque el prototipo deja el carril a la vista en una sesión
  embebida.

  **Y el resultado es que el defecto no está en este eje**: ninguna de las 48 vistas se pasa. La
  ausencia queda medida en vez de supuesta, que era justo lo que faltaba para poder buscarlo en otro
  sitio.

- **La lista de deuda de cobertura sólo vigilaba lo que ya estaba en ella.** Un archivo que llegaba al
  96/96 y luego se degradaba no lo miraba nadie: la puerta de archivos nuevos no puede verlo —la
  novedad se decide contra la referencia base y el archivo se publicó hace meses— y el bucle de la
  deuda sólo recorre nombres ya escritos. **Medido, no temido**: tres ejecuciones seguidas de CI
  midieron **216** archivos por debajo del listón mientras `eng/coverage-debt.txt` nombraba **212**, y
  los cuatro que faltaban llevaban días degradados. El peor, `PlayerView.axaml.cs` a **65/41**, es la
  pareja más baja del árbol.

  Ahora la lista se comprueba **completa** y no sólo exacta: cualquier archivo bajo el listón que no
  esté en ella se nombra en la puerta, que es la única forma en que una degradación se anuncia. Fuera
  de CI informa y no bloquea, igual que los suelos y por la misma razón: siete archivos miden distinto
  en un runner hospedado.

  **Los cuatro se han cerrado en vez de escribirse**, así que el trinquete sigue en 212 y las dos
  cuentas son el mismo número:

  - `PlayerView.axaml.cs` **65/41 → el listón**. Dos vistas lo montaban y ninguna le daba contexto de
    datos, así que **ninguno de sus dos manejadores se había ejecutado jamás** — ni el del teclado que
    túnela (el arreglo entero de «la barra espaciadora pone pantalla completa») ni el del doble clic.
    Los dos se publicaron con una mirada a mano por toda evidencia.
  - `PlayerViewModel.cs` **98/91 → el listón**, quitando tres guardas que nada podía tomar: dos `as
    AsyncRelayCommand` sobre dos propiedades que sólo se asignan en un sitio, y dos comprobaciones de
    nulo que repiten lo que el `CanExecute` de su comando ya exige —y `AsyncRelayCommand.Execute`
    pregunta antes de correr nada, que es una promesa escrita en esa clase—.
  - `PlaybackPreference.cs` **98/92 → el listón**. `SubtitleStyle.IsColour` es público porque las
    muestras preguntan antes de ofrecer, y su rama de «aquí no hay nada» no la tomaba nadie.
  - `DisabledOutline.cs` **100/87 → el listón**. El radio de reserva existe para un `Control` que no
    es `TemplatedControl`, y los diez tipos que el estilo nombra son todos plantillados: escrito,
    documentado y medido por nadie.

- **Una película sin identificar se llamaba por su nombre de archivo, tal cual.** «El Faro de Piedra
  2019» en la tarjeta, con el año dentro del título y la columna del año vacía a su lado — y antes de
  eso, «Neon.Sobre.el.Rio.2022.2160p». Mientras tanto `MediaNameParser` lleva desde la primera semana
  descomponiendo ese mismo nombre, y **tres** casos de uso ya lo llaman: la bandeja de revisión lo
  compara con el proveedor, la agrupación de versiones compara copias con él, y desde el 2026-08-25 la
  de series decide episodios con él. Dos lecturas del mismo nombre, y la que salía en pantalla era la
  cruda.

  Ahora hay una política pura (`ScannedTitlePolicy`) que dice cómo se llama la tarjeta —el título
  limpio, y el nombre de archivo cuando limpiarlo no deja nada, porque «2019.mkv» es un año sin
  título y una tarjeta en blanco es peor que una sucia— y un caso de uso hermano de los otros dos
  (`NameScannedTitles`) que corre después de cada escaneo. La migración **0021** añade la columna del
  año a la proyección: limpiar el título sin sitio donde poner el año habría quitado el año de la
  pantalla, así que las dos cosas viajan juntas.

  **Una biblioteca ya catalogada se renombra sola en el siguiente escaneo**, y sin volver a sondear un
  solo archivo: el paso recorre todo el resumen del escaneo, `Unchanged` incluidos, que es justo lo
  que un archivo cuyo tamaño y fecha no se han movido nunca vuelve a ser. Ese es el estado en el que
  está todo lo ya catalogado el día que esto llega.

  La aserción de `ScanSeriesGroupingTests` que decía «El Faro de Piedra 2019» estaba escrita a
  propósito, para que cambiarla fuera una decisión y no una sorpresa. Ésta es esa decisión.

- **`PlaybackControlPolicy.SpeedSteps` no lo leía nadie.** El defecto de la casa, número dieciséis:
  el menú escribía sus diez números en su propio marcado y una prueba **leía ese archivo como texto**
  para comparar los dos, así que la política no decidía nada mientras el comentario que tiene encima
  afirmaba que el teclado la recorría. Nada la recorría. Ahora el menú se construye de ella, y la
  prueba pregunta al modelo en vez de a un `Regex` sobre un `.axaml` — que es la forma en que tres
  suites de este árbol ya se quedaron ciegas: un archivo que deja de casar con el patrón devuelve una
  lista vacía, y una lista vacía comparada con otra vacía pasa.

- **El botón de pantalla completa dibujaba las flechas de entrar también estando ya dentro.** El
  prototipo escribe `icon(mode === 'fullscreen' ? 'exitfull' : 'full')` y `IconExitFullscreen` estaba
  en el diccionario desde el principio, dibujado sólo por el restaurar de la ventana pequeña. Es el
  mismo defecto que el botón de silencio tenía en agosto y por el que se corrigió: un control que dice
  lo mismo hiciera lo que hiciera.

  De paso, la tabla que ata qué glifo lleva cada botón listaba **once de trece**: los dos botones de
  modo llevaban tres días en la barra y no estaban en ella, y el que estaba mal era uno de los dos que
  faltaban. Una imagen sobre un botón que nadie nombra es una imagen que nadie comprueba.

- **`HardwareAccelerationFallback.Reset` no lo llamaba nadie.** Decía ser «para cuando se crea un
  motor nuevo», y un motor nuevo construye uno de éstos nuevo, así que ni `src/` ni ninguna prueba
  lo habían invocado jamás: el defecto de la casa con sombrero pequeño, otra vez. La puerta de
  cobertura fue lo que lo vio —una línea de un archivo pequeño es un punto porcentual entero— y se
  ha borrado.

- **Los iconos son los del prototipo, y ahora lo dice una puerta y no un comentario.**
  `PrototypeIconTests` lee `design/AP Reelume.dc.html`, saca el mapa de su función `icon(n, s)` y
  compara **carácter por carácter** las dieciséis formas que están hechas sólo de trazados. Las que
  llevan un rectángulo o un círculo se convirtieron en los arcos que los dibujan, así que no hay
  cadena que comparar y están **nombradas** como conversiones en vez de saltadas en silencio — un
  salto que nadie escribe es como una puerta se vuelve ciega en vez de roja. Y la suma cuadra: toda
  geometría que el tema declara está o copiada, o convertida, o declarada como propia de esta
  aplicación con su razón.

- **`HasPlayerPanels` tenía una rama que nada podía tomar.** El término del panel de subtítulos es
  `Player?.Tracks is not null`, y también lo es la mitad de `HasAudioPanel`, que se evalúa antes: en
  el momento en que la cadena llegaba al subtítulo, la lista de pistas ya se sabía ausente y el
  término sólo podía contestar «no». La puerta de cobertura fue lo que lo vio. Se ha borrado —la
  regla de este árbol para una rama así es hacerla alcanzable o quitarla, nunca escribirle una prueba
  imposible— y las cuatro alternativas que quedan se piden ahora una a una.

- **Una carpeta de episodios ya es una serie.** Es el defecto característico de este repositorio en
  su forma más grande: `titles`, `seasons`, `episodes` y `episode_media` existen desde la migración
  0004, la ficha de serie está dibujada y enrutada desde que se escribió, y **nada había escrito
  jamás una fila en ninguna de las cuatro**. `MediaNameParser` lee `S01E01`, `1x04`, «Temporada 1
  Episodio 2» y `Cap.803` desde que se escribió, y nadie le preguntó nunca dónde iba el episodio: así
  que dos series con once temporadas entre las dos llegaron como noventa y nueve tarjetas sueltas.
  Ahora hay una política pura (`LocalSeriesPolicy`) que dice qué carpeta nombra la serie —la carpeta,
  nunca el archivo— y un caso de uso (`GroupScannedEpisodes`) que corre después de cada escaneo, igual
  que la agrupación de versiones, y escribe la serie, sus temporadas, sus episodios y el archivo
  detrás de cada uno. Medido de punta a punta contra la disposición real: noventa y nueve archivos
  entran, **tres tarjetas salen** —las dos series y la película que había en la misma raíz—, con 72 y
  27 episodios contados, sus ocho y tres temporadas, y cada episodio con su archivo detrás.
  Los identificadores se derivan de la clave de la serie y de los números, así que un segundo
  escaneo actualiza en vez de duplicar; la clave lleva la raíz, así que un respaldo no se funde con
  el original; y una película sigue siendo una película.

- **Continuar volvía a preguntar lo que continuar ya había contestado.** La sesión abría en el minuto
  correcto y acto seguido ofrecía decidir el minuto, sobre una imagen ya en marcha — «al hacer click
  en continuar vuelve a pedir confirmación de continuar o volver a ver desde el inicio en la vista
  del reproductor». La posición pedida por quien abre ya mandaba sobre la política desde el cambio de
  versión; lo que no se hizo entonces fue la otra mitad de esa misma decisión, y las dos están ahora
  escritas juntas: **sin aviso cuando la petición nombra una posición**. Vale igual para «desde el
  principio», donde ofrecer reanudar era discutir con el botón que acababa de pulsarse. El aviso
  sigue apareciendo donde tiene sentido: una sesión que nadie abrió con un minuto, que es lo que es
  abrir un archivo desde el Explorador.

- **En la ficha de película desaparecía el botón de reproducir.** Estaba oculto de plano cuando no
  había nada que reanudar, así que una película sin empezar ofrecía el glifo de «desde el principio»
  y ninguna forma de simplemente verla. Ahora es **un solo botón cuyas palabras siguen al estado** —
  «Continuar · 49:00» con progreso y «Reproducir» sin él, que es lo que escribe el prototipo— y el
  glifo pasa a ser lo que siempre fue, la alternativa: sólo se dibuja mientras hay algo de lo que ser
  alternativa.

- **Las carátulas de Home no llevaban a ninguna parte.** Eran tarjetas dentro de un elemento de
  lista, así que pulsar una la seleccionaba y nada más — «al hacer click en las tarjetas en home no
  redirige a la vista detalle del vídeo». El prototipo envuelve la carátula entera en un botón, y eso
  es lo que hay ahora en los dos carriles de póster, con la misma clase `poster-card` que la rejilla
  de la biblioteca: una tarjeta es una sola forma y un solo objetivo en los tres sitios. El paseo
  destapó de paso una ambigüedad real al pulsarlas: un mismo título puede estar a la vez en «Añadido
  recientemente» y en las sugerencias, y dos controles con un nombre es el defecto por el que los
  botones del carril ya se habían renombrado, así que la carátula anuncia el carril y después el
  título.

- **Y el carril de sugerencias dibujaba veinte portadas de las iniciales de nada.** Su búsqueda de
  títulos era un parámetro opcional que la composición nunca pasó, así que resolvía la cadena vacía:
  registrado y nunca alimentado, en su forma más callada — el carril se dibujaba, las tarjetas tenían
  la forma correcta y no había error en ninguna parte. Ahora se pide una vez por carga y en lote,
  porque el catálogo se consulta por una conexión y una búsqueda por tarjeta sólo se puede contestar
  bloqueando el hilo que las pinta.

- **Los recuadros de selección de los menús ya no son un rectángulo de acento.** Eran dos píxeles del
  acento alrededor de cada fila elegida de toda la aplicación —el índice de ajustes, los menús del
  carril, y también una carátula dentro de un carril, donde se lee como una caja que alguien dejó
  caer sobre una foto—. El prototipo lo dibuja al revés: un lavado neutro, `rgba(127,145,170,.16)`,
  y el acento gastado en la barra de 3 px del destino actual. Eso es lo que hay ahora, con dos
  tokens nuevos (`SelectionFillBrush` y `SelectionStrokeBrush`) y el borde a un solo píxel. El trazo
  no es transparente y ésa es la única cesión, medida: `ListRowStateTests` exige que una fila elegida
  se distinga de sus vecinas por 3:1, y un lavado al 16 % da 1,1:1 — así que lleva el borde neutro de
  3,88:1, que responde a la puerta sin responder en acento. La selección deja además de seguir al
  acento elegido en Apariencia, porque ya no es acento.

- **Las píldoras de filtro de la Biblioteca no eran las del prototipo.** La que no está elegida lleva
  `border: 1px solid transparent` sobre el relleno llano y la tinta secundaria; ésta llevaba el borde
  de control y la tinta primaria, así que las tres opciones se dibujaban como tres elegidas con una
  un poco más azul. Y **el desplegable no decía nada al abrirse** salvo dar la vuelta al galón: le
  faltaba la otra mitad que el prototipo escribe, el acento sutil de relleno y el acento de borde.
  `SelectionSurfaceTests` mide las dos cosas sobre controles realizados, que es distinto de leerlas
  del marcado.

- **Los botones seguían sin alinearse en vertical, y la compensación estaba en el sitio equivocado.**
  Los cinco píxeles vivían en el relleno inferior del botón, que mueve **todo** el contenido: un
  glifo y la palabra a su lado viajan juntos, así que quedaban exactamente igual de separados que
  antes y lo único que cambiaba era dónde se apoyaba la fila entera. Medido en un botón de 44 px: el
  centro del glifo en 19,00 y la tinta de la palabra en 21,43 — **2,43 px**, el mismo número de
  siempre, intacto bajo el relleno que debía corregirlo; y el glifo, además, 3 px por encima del
  centro del propio botón, levantado por una línea base que no tiene. Ahora los cinco píxeles son un
  margen inferior **de la etiqueta**, que es lo único con línea base a la que responder, y el glifo
  se queda centrado por su geometría. `ButtonOpticalCentreTests` sostiene las dos afirmaciones a un
  píxel, la del icono contra la palabra incluida — que era la mitad que ninguna puerta miraba.

- **El glifo de «desde el principio» es el arco de reinicio con la flecha al otro lado**, que es el
  que llevaba antes leído al revés, y su botón mide lo mismo que los dos que tiene al lado: 36 como
  ellos y redondo, en vez de los 44 del reproductor. Un círculo de 44 en una fila de píldoras de 36
  es el único control que no alinea.

- **Y ya no queda ninguno cuadrado en ninguna parte.** La puerta lee los estilos de botón del archivo
  de tokens y rechaza cualquiera que declare un radio que no sea el de píldora: eran seis más de los
  dos que se veían —las filas de «Otras acciones», los dos del carril, la muestra de acento y las
  celdas de la rejilla de color—, y la muestra escribía además su propio 22 al lado del token.

- **El aviso de «la aceleración por hardware no estaba disponible» salía en cada reproducción**, y
  era una queja sobre la máquina donde lo cierto es una decisión: este motor compone los subtítulos
  dentro de la imagen que entrega, y a una superficie de la tarjeta gráfica no se le puede pedir eso,
  así que **no la pide**. Ni pedida ni activa, que es lo que pasa de verdad.

- **La valoración es de cinco estrellas** y no diez casillas numeradas. Lo que ya estaba guardado
  viene con ella: la migración 0020 lo divide entre dos y **redondea hacia arriba**, así que un 1
  sobrevive como una estrella en vez de caer a un cero que esta aplicación no sabe tener. Lo que dice
  que una estrella está dada es su relleno y no una marca al lado, porque «tres de cinco» es lo que
  significa una valoración. «Quitar valoración» sigue justo al lado.

- **El minuto del botón «Continuar» iba por fuera**, como un pie de la fila en vez de como parte de
  lo que el botón va a hacer. Va dentro, que es lo que Home lleva haciendo desde que se dibujó.

- **El icono de «desde el principio» es ahora el espejo del de reproducir**, que es lo que dice
  «atrás» antes que ninguna palabra, y su botón es **redondo**.

- **Todos los iconos bajan dos píxeles**, y el trazo con ellos: las cuatro escalas guardan la
  proporción con la que se dibujaron —el ancho entre quince—, así que un glifo dos píxeles más
  estrecho se lee como el mismo dibujo y no como uno más grueso.

- **Ningún botón es cuadrado.** Dos clases lo eran: los del reproductor, 44 × 44 con el radio medio,
  que es un cuadrado con las esquinas quitadas, y una llamada `player-pill` que dibujaba un radio
  pequeño. Los de icono son círculos y los que llevan palabra son píldoras, con un solo token.

- **Y redondos de verdad.** El token de píldora valía 18 —la mitad de un control de 36— y eso deja
  un píxel de recta en cualquier cosa más alta: los objetivos de 44 del reproductor salían como
  cuadrados con las esquinas quitadas. Vale 999, que el dibujo recorta a la mitad del lado más
  corto, así que un objetivo cuadrado sale círculo y uno ancho sale píldora con un solo número. Es
  el `border-radius: 999px` del prototipo, y la razón de que ese modismo exista. La prueba mira el
  **píxel de la esquina**, no el número: 999 satisfaría cualquier comparación mientras el dibujo
  decide otra cosa.

- **La ficha de película lleva ahora el mismo triángulo que la de serie**, que era lo que el
  propietario echaba en falta —«el botón de reproducir no es igual al del prototipo»—, y
  **«Reproducir desde el principio» es un glifo** y no cuatro palabras en una fila que ya llevaba
  otras tres etiquetas. Lo que dice no cambia: el nombre que oye un lector y la ayuda que encuentra
  un puntero son la misma frase de siempre.

- **El contorno punteado del deshabilitado salía en los cuatro temas, y sólo hace falta en dos.** La
  razón por la que existe estaba escrita desde el principio: en claro y oscuro un control
  deshabilitado se lee por su relleno, que es un tercer gris, y las dos paletas de contraste alto no
  tienen un tercer color que gastar. Dibujarlo también en los temas ordinarios ponía un rectángulo de
  puntos encima de un gris que ya lo decía — el propietario los contó en siete pantallas y el
  instrumento contó **299 en todo el árbol** sin datos cargados, todos ellos órdenes sin nada sobre lo
  que actuar. La señal se gasta donde es la única, y el relleno la lleva donde puede llevarla.

- **Los desplegables llevaban su texto 2,43 px bajo**, exactamente el mismo número que los botones y
  por la misma razón: la tinta de una fuente no es simétrica alrededor de la caja que la contiene. La
  receta es la de los botones —cinco píxeles de margen inferior, derivados de las métricas y no
  ajustados a ojo— puestos sobre las palabras y no sobre el galón, que es geometría y se centra solo.
  Medido antes y después: 24,43 sobre un centro de 22,00, y dentro del píxel al corregirlo.

- **El reborde azul salía al hacer clic, y no sólo al llegar con el teclado.** Los diez selectores de
  foco decían `:focus`, que un ratón también levanta, así que cada casilla pulsada respondía con un
  anillo de dos píxeles que se quedaba hasta pulsar en otro sitio. Ahora dicen `:focus-visible`. Un
  puntero ya dice dónde está estando ahí; el anillo dice dónde está el teclado.

- **Pantalla completa y ventana flotante no estaban en la barra de controles.** Los dos modos sólo se
  alcanzaban desde la fila de píldoras sobre la imagen y desde el teclado, que es donde el propietario
  no los buscó. Están al final de la barra, alcanzan el modelo del propio reproductor —la barra viaja
  a la ventana pequeña, donde no hay concha encima a la que subir— y **ya no están en los dos sitios**:
  dos botones respondiendo a «Pantalla completa» en una pantalla es un nombre que no nombra a ninguno,
  y el paseo lo dice en voz alta porque no puede apuntar el clic.

- **El doble clic sobre la imagen pone y quita la pantalla completa.** No había nada escuchándolo.

- **La barra espaciadora ponía pantalla completa y `F` no hacía nada.** Ninguna de las dos era el
  mapa: espacio siempre fue reproducir/pausar y `F` siempre fue pantalla completa. Era **quién oye la
  tecla primero** — un botón de la barra toma el foco al pulsarlo y un botón enfocado contesta al
  espacio activándose. El reproductor las oye ahora de bajada, antes de que ningún botón enfocado
  pueda gastarlas.

- **El icono del mini reproductor era el de salir de pantalla completa**, cuatro flechas hacia
  dentro. El de imagen sobre imagen ya estaba en el diccionario y no lo usaba nadie.

- **La barra de reproducción se duplicaba en la ventana flotante.** La imagen que esa ventana recibe
  se lleva la barra entera consigo, y la ventana ya dibuja cinco controles propios. La barra de la
  imagen se retira mientras está ahí y vuelve con ella.

- **Los subtítulos no llegaban nunca a la pantalla, y ahora hay tres razones medidas de por qué.** El
  propietario lo trajo con su prueba hecha: el mismo episodio los muestra en VLC y no aquí. Ninguna de
  las tres se ve leyendo el código, y las tres tuvieron que medirse contra un archivo real con un
  `.srt` de control que cubre la película entera.
  1. **La sesión los apagaba al abrir.** `ApplyPlaybackPreferences` aplicaba el valor resuelto
     hubiera o no una preferencia guardada, y sin ninguna ese valor es «apagados»: cada primera
     reproducción le mandaba `-1` al motor y desactivaba la pista que el contenedor marca por
     omisión. Un ámbito que no contesta es silencio, y el silencio no es «no». Ahora sólo se aplica
     lo que alguien decidió, y lo que el motor eligió por su cuenta se lee y se enseña.
  2. **El croma del sumidero de memoria decidía si el subtítulo se componía.** Con `RV32`, `RGBA`,
     `ARGB`, `RV24`, `YUY2`, `VYUY` y `YVYU` **no cambiaba ni un byte** del fotograma al encender un
     subtítulo; con `UYVY` cambiaban 61 687. El motor pide `UYVY` y convierte a BGRA él mismo.
  3. **Con decodificación por hardware el subtítulo se pierde con un error que nadie leía.** VLC lo
     dice una vez por fotograma —«no matching alpha blending routine (chroma: YUVA -> DX11)»— y
     publica el fotograma sin él: dibuja el subtítulo sobre la imagen antes de que llegue a este
     búfer, y con D3D11VA esa imagen todavía es una superficie de la tarjeta gráfica. 67 001 bytes
     cambian por software y ninguno por hardware. El motor decodifica por software y **lo declara**:
     lo pedido sigue siendo lo pedido y lo que se anuncia como activo es lo que corre.

- **Los subtítulos que viven junto al archivo no se cargaban nunca.** `ExternalSubtitleDiscovery`
  estaba escrito, confinado a su raíz y probado en los dos codificados — y sin nadie que lo llamara:
  la sesión entregaba una lista vacía en cada apertura. El defecto de la casa, otra vez.

- **El vídeo se deformaba al redimensionar.** Se dibujaba sobre todo el alto y el ancho disponibles,
  así que un episodio 16:9 en una ventana estirada salía estirado, en el reproductor y en el mini.
  `VideoFitPolicy` conserva la forma y reparte las bandas; lo que se afirma es la **proporción**, no
  el tamaño.

- **Home salía vacía con la biblioteca llena.** Leía la tabla `titles`, que **nada de la aplicación
  escribe** —`ApplyIdentification` ya lo decía con sus palabras—, mientras que Biblioteca lista la
  unión de los títulos identificados con los archivos escaneados. Medido contra la base de datos del
  propietario: 102 filas en `scanned_titles`, cero en `titles` y cuatro en `watch_state` que sólo
  casaban con archivos escaneados. Las tres proyecciones de Home leen ahora esa misma unión.

- **Home tampoco se cargaba al arrancar.** El servicio de navegación empieza en Home y no anuncia
  nada, así que el único sitio que lee Home —la llegada a una ruta— no corría hasta que alguien se
  iba y volvía. La ruta con la que abre la aplicación se anuncia como cualquier otra.

- **El panel de pistas decía «sin subtítulos» sobre subtítulos que se estaban leyendo.** El motor
  elige mientras se decodifican los primeros fotogramas, que es después de construirse el panel:
  medido, ninguna en vigor al abrir y la pista 6 tres segundos más tarde. Ahora el panel sigue al
  motor.


- **El texto de los botones se veía bajo aunque su caja estuviera centrada, y esta vez con el número
  delante.** `ButtonInkTests` centró la caja en agosto y dijo por escrito que no medía los glifos; el
  propietario siguió viéndolo torcido y tenía razón. Medido con las métricas de la propia fuente: el
  trazo de tinta —del alto de una mayúscula al pie de un descendente— caía **2,43 px por debajo** del
  centro de un botón de 44. Cinco píxeles de relleno inferior lo suben dos y medio, el número se
  deriva y no se ajusta a ojo, y `ButtonOpticalCentreTests` lo sostiene al píxel. Los botones cuyo
  contenido es un glifo no lo llevan: un icono se centra por su geometría y no tiene línea base que
  compensar.

- **El acento no cambiaba casi nada de la aplicación.** Se escribían cuatro pinceles y los controles
  de Fluent leen los suyos, que el archivo de tokens redirige al acento con `<StaticResource>` — una
  referencia **estática**, resuelta una sola vez al cargar el diccionario. Así que ni los deslizadores,
  ni las casillas, ni los radios, ni la selección de las listas seguían al color elegido. Ahora se
  escriben las veinte redirecciones además de los cuatro tokens, y `AccentTokenTests` lee el mismo
  archivo para exigir que no falte ninguna: una redirección añadida el año que viene y olvidada sería
  un control que se queda con el acento con el que se compiló, y eso no se ve mirando la pantalla
  porque diecinueve de veinte sí cambian.

- **Las muestras de color llevaban un círculo dentro.** Era el ● y ○ que gasta cada fila de píldoras
  de este árbol, y sobre un círculo de color se lee como un botón de opción que alguien dejó caer
  encima. El prototipo lo dice con el borde de la propia muestra, y un anillo es geometría igual que
  el glifo: los dos contrastes altos pintan un borde, así que no se pierde la señal donde el color no
  dice nada.

### Añadido

- **«Desde el principio» está también en las dos superficies anchas de Home**, que es donde faltaba:
  el mismo arco con la flecha al otro lado, el mismo círculo de 36, junto al botón del que es la
  alternativa — «en la tarjeta ancha del inicio justo después habría que poner el icono de reproducir
  desde el inicio, como en la vista detalle del vídeo». Una bandera en la petición y no un segundo
  enganche: lo que cambia es el minuto en que abre la sesión, y el anfitrión ya es quien lo lee.

- **El catálogo de elementos, leído y escrito**: [docs/design/ELEMENTS.es.md](design/ELEMENTS.es.md)
  pasa `design/Catálogo de elementos - AP Reelume.dc.html` a los tokens de este árbol, elemento por
  elemento y estado por estado. Dice tres cosas que se venían dibujando mal —la píldora sin elegir no
  lleva borde, un menú no se pinta como un desplegable, y la compensación óptica va en la etiqueta y
  no en el relleno del botón— y una regla de precedencia: el prototipo manda sobre el documento, y el
  documento manda sobre el `.axaml`. `BilingualHeadingTests` lo sostiene en los dos idiomas junto con
  `SURFACES`, que hasta ahora no tenía puerta ninguna.

- **Todos los botones dicen lo que hacen al posar el puntero**, y lo dicen con las mismas palabras
  que oye un lector de pantalla. Un estilo y no un atributo por botón: son más de doscientos, y
  escritos uno a uno serían doscientas ocasiones de que las dos frases se separen sin que nadie lo
  note en un año.


- **Un selector de color en las tres filas que eligen uno**: el acento y los dos de los subtítulos.
  El prototipo abre el del navegador; este abre la misma forma con controles que esta aplicación ya
  tiene — una rejilla de ocho tonos por cinco luminosidades sobre una fila de grises, tres
  deslizadores para cualquier color intermedio, una muestra grande de lo que hacen y el valor en
  monoespaciada. La rejilla se construye en el dominio y no se lista: los tonos van repartidos por la
  rueda y las luminosidades por la escala, que es lo que hace que una rejilla se lea como una rejilla.

### Añadido

- **Ajustes → Apariencia con las once filas del prototipo**, donde tenía dos. A las de tema e idioma
  se suman: seguir el tema de Windows, color de acento con sus seis muestras y el valor en
  monoespaciada, fondo Mica sutil, tinte de acento en los fondos, densidad, tamaño de las portadas,
  redondeo de esquinas, mostrar títulos bajo las portadas, animaciones de la interfaz, y la fila sin
  control que dice que la superficie del reproductor es fija. Cada una escribe en el recurso que la
  interfaz ya leía —la misma vía por la que el movimiento reducido llega a las animaciones desde que
  se implementó—, así que ninguna es una preferencia guardada que no cambia nada.

  **Tres tocaban puertas y las tres se declaran de nuevo.** El acento personalizable contra
  `ContrastTokenTests`: sus cinco obligaciones se cumplían eligiendo dos colores a mano por tema, y
  eso no es algo que pueda hacer quien elige el suyo. Ahora la familia se **deriva** —tono y
  saturación son de quien elige, la luminosidad se camina hasta cumplir la razón contra la página en
  la que se va a dibujar— y lo mide `AccentPaletteTests` barriendo la rueda entera contra las dos
  superficies, 600 colores por cada una. El redondeo contra `ScalarTokenTests`: los dos radios se
  escriben desde una sola elección, y `DensityGutter` se gasta en la tarjeta en vez de quedar
  declarado y sin lector. La densidad y el tamaño de portada contra `ViewOverflowTests`: la
  cuadrícula ya contaba sus columnas leyendo el ancho del token, así que mover el token mueve la
  cuenta.

  En alto contraste el acento **no** se toca: allí es una necesidad y no un gusto, y lo que hace el
  servicio es retirar sus propias escrituras en vez de añadir otra encima.

### Añadido

- **Al reproducir se va todo menos la imagen, y vuelve al mover el ratón o pulsar una tecla.** La
  barra de título, el raíl de navegación, la cabecera de la sesión, la columna de paneles y el
  transporte: mientras la película corre, la ventana es la película. El prototipo no lo hace —se
  comprobó en su código— así que es un requisito propio. Las dos bandas del shell miden ahora su
  contenido en vez de llevar el número escrito, porque un control oculto dentro de una fila de 44 px
  deja los 44 px: lo que reserva el espacio es la fila. Y el par `RevealControls`/`HideControls` que
  el reproductor declaraba desde siempre y **nadie llamaba** por fin tiene quien lo llame.

  Vuelve con el gesto y con nada más: no hay temporizador que lo esconda otra vez. Es una decisión
  con su coste escrito —quien mueve el ratón una vez conserva el cromo hasta que pause y reanude— y
  la alternativa sería un reloj decidiendo qué hay en pantalla, al que ninguna prueba puede
  preguntar sin esperarlo y con el que el paseo autónomo competiría en cada escena.

### Cambiado

- **El reproductor se encabeza con las píldoras del prototipo y su columna empieza cerrada.** Audio,
  Subtítulos, Vídeo y Marcadores —más «Otras versiones», que este reproductor conserva— van en la
  cabecera y abren la columna; pulsar la que está abierta devuelve sus 320 px a la imagen. Antes era
  una tira de pestañas, y una tira de pestañas no sabe decir «ninguna»: siempre había un panel
  ocupando un quinto del ancho de la película, tuviera o no algo que decir. La columna lleva ahora
  cabecera propia con el nombre de lo que está abierto y su «×», que es la segunda forma de
  cerrarla.

- **Los cuatro paneles agrupan por asunto y no por modelo.** «Audio» reúne las pistas de audio, el
  dispositivo de salida y sus canales; «Subtítulos», las pistas de subtítulos; «Marcadores», los
  detectados y los de este título. Cada mitad viene de un modelo distinto y ninguna se fusionó: la
  agrupación es de quien mira, y un panel dibuja sólo las mitades que la sesión tiene.

- **La etiqueta del selector de subtítulos dice «Pista de subtítulos».** Decía «Subtítulos», que es
  exactamente el nombre de la píldora que ahora abre su panel: dos controles con un nombre es una
  ambigüedad real, y el paseo la encontró antes que nadie.

### Añadido

- **«Vídeo», el panel que faltaba.** Dice si la decodificación va por hardware o por software y si la
  salida es HDR10 o SDR, con la frase que lo explica debajo de cada fila y el alcance escrito al pie:
  Dolby Vision y el passthrough de Dolby y DTS quedan fuera. Son los mismos hechos que la insignia
  sobre la imagen ya llevaba; esto es donde alguien va a buscarlos en vez de esperar a que aparezcan.

- **La píldora de sesión, «Sesión 1 · motor único activo».** Es cierta y medida: LibVLC se construye
  una vez y una sesión cada vez lo sostiene, así que el número es cuántas veces se ha abierto una en
  esta ejecución. Una sesión nueva la sube y cierra la columna, porque el panel que estaba abierto
  era del archivo que acaba de irse.

- **A la derecha del pie, adónde va el sonido.** El dispositivo elegido y lo que puede llevar —
  «Altavoces del sistema · 2.0»—, que es la píldora que el prototipo dibuja al final de su
  transporte. Aquí va en la fila de abajo y no en la del transporte: esa ya lleva dos deslizadores y
  dos lecturas, y a 900 px —la ventana más estrecha que esta aplicación permite— añadirle un nombre
  de dispositivo es la forma que ha dibujado un control fuera del borde nueve veces.

### Corregido

- **La casilla «Recordar para toda la serie» medía con ancho infinito.** Una `CheckBox` mide su
  contenido sin límite y lo dibuja donde caiga, así que su etiqueta dependía de que el español
  cupiera por casualidad en una columna de 320 px. Ahora envuelve. Es la misma forma que este
  repositorio lleva cazadas nueve veces, y la única de la columna del reproductor que quedaba.


### Corregido

- **El contorno punteado de un control deshabilitado tenía la forma equivocada.** Se dibujaba con un
  radio fijo de 4 px para los diez tipos, así que en una píldora —«Quitar valoración», cualquier
  acción principal, cualquier fila de «Otras acciones»— quedaba un rectángulo casi recto cuyas
  esquinas caían **fuera** del borde del propio botón: se lee como un contorno más grande que lo que
  contornea. Medido: el adorno siempre tuvo el tamaño exacto del control, así que «más grande» eran
  las esquinas y nunca la caja. Ahora toma el radio del control, leído cuando llega al árbol —los dos
  son ajustes de estilo, y leerlo al construir daba el 0 que el estilo aún no había escrito—.

- **Cuatro controles que el paseo pulsaba y el inventario no reconocía.** Los que se llaman por sus
  propios datos —las dos acciones de una tarjeta del carrusel y las píldoras de temporada— se
  registran bajo el enlace con el que están declarados; sin eso, el paseo anotaba «Temporada 1» y el
  inventario buscaba `{Binding SeasonLabel}`. La puerta vuelve a 0 pendientes.


### Corregido

- **Dos de las tres herramientas del título aparecían sin tener nada que hacer.** «Revisar versiones»
  abre una comparación, y una película con una sola copia nunca se agrupa: la superficie de detrás
  respondía con nada y la ruta no cambiaba, así que era una puerta a una habitación que no existe —y
  en una serie no existe nunca, porque sus episodios son archivos con claves propias—.
  «Previsualizar renombrado» abre un plan, y un archivo que ya se llama como esta aplicación lo
  llamaría produce un plan sin operaciones; lo dice la propia `RenamePolicy`, que convierte ese caso
  en un conflicto `NoChange`. Las dos se preguntan al abrir la ficha y sólo se dibujan si la
  respuesta es sí. «Editar metadatos» es la tercera y no tiene condición: siempre se puede.

- **El botón del tráiler externo dice menos y muestra más.** «Ver el tráiler en el navegador» era tan
  ancho como las tres herramientas juntas; ahora dice «Ver tráiler» con la flecha de salida que el
  prototipo dibuja al lado, y que sale al navegador se lo cuenta al lector el texto de ayuda. El
  tráiler local pasa a «Reproducir tráiler», que es lo que hace y lo distingue del otro.


### Corregido

- **La forma de la ficha de tipo la decide un estilo, no un convertidor.** `KindShapeConverter` tenía
  que pedir la aplicación y buscar un recurso por nombre, y sus dos ramas de «no encontrado» no las
  puede tomar nada: las dos claves de icono están declaradas y hay una puerta sobre ese inventario.
  Se retira entero. La tarjeta pregunta `IsShow` —que la interfaz responde desde la clave que los
  cuatro modelos ya dan— y dos reglas de estilo ponen la película o la pantalla. El trinquete de
  cobertura baja de 215 a 214 con él.


### Corregido

- **Los rótulos de la tarjeta de revisión iban en dos estilos.** «ARCHIVO PENDIENTE» estaba en
  versalitas y «Candidato propuesto», «Confianza» y «Por qué» no, dentro de la misma tarjeta. Los
  cuatro van ahora como los escribe el prototipo, y el tercero pasa a decir lo que dice allí:
  «SEÑALES CONSIDERADAS», que es lo que la lista de debajo contiene.


### Corregido

- **«Otras acciones» tenía dos gramáticas en la misma columna.** Las marcas personales ya eran filas
  de ancho completo con icono, que es como las dibuja el prototipo; las tres decisiones de estado de
  visto seguían siendo píldoras sueltas de anchos distintos encima de ellas. Ahora son cinco filas
  iguales. Mismos nombres, mismos mandos, mismo orden: sólo cambió la forma.


### Corregido

- **Las cinco capturas del repositorio enseñaban la ruta de una máquina concreta.** La bandeja de
  revisión escribe la carpeta bajo cada nombre de archivo, y la biblioteca de las capturas vivía bajo
  el perfil de quien las tomó: `C:\Users\<nombre>\.claude\projects\…` quedó impreso
  en `docs/assets/review.png`, en un repositorio público. Se vuelven a tomar con la biblioteca en una carpeta neutra, y de paso con la
  aplicación al día: la ficha de tipo sin palabra en el carrusel, el signo más de «Añadir medios», el
  nombre del candidato y la segunda línea del reproductor.


### Corregido

- **Un fallo de reproducción ya no se borra solo.** LibVLC rechaza un archivo y, un instante después,
  informa de que ha detenido el medio que acababa de desmontar; ese estado sustituía al fallo, así
  que las acciones de recuperación desaparecían de la pantalla mientras alguien las leía —incluida la
  de abrir el archivo con otro programa—. Apareció primero como intermitencia: el paseo físico
  esperaba un minuto entero por un fallo que ya había ocurrido y había sido pisado. Salir de un fallo
  sigue siendo posible por las tres vías que decide la aplicación: reabrir, volver a fallar por otro
  motivo, o quedar en reposo.

- **El estado «Disponible» de la ficha se dice en verde**, que es como lo pinta el prototipo. La ficha
  de al lado sigue neutra: «Sin empezar» es un dato, «está» y «no está» son las dos respuestas que
  esa tarjeta existe para dar.


### Añadido

- **La bandeja de revisión dice de qué título habla.** Pedía aceptar o rechazar «movie:761053»: el
  proveedor ya devolvía el nombre y el año, y la cadena entera —hechos, puntuación, fila guardada,
  proyección— los tiraba. Ahora los lleva hasta la tarjeta, que escribe «Tormenta de Sal (2016)» y
  cae a la clave sólo cuando no hay nombre guardado, que es toda fila anterior a esta versión. El
  nombre también es lo que anuncia el lector de pantalla en los tres botones de la tarjeta.

### Corregido

- **El panel de «Siguiente episodio» limita su columna, no su borde.** Un ancho fijo no cede: al
  200 % de escala de texto ponía «Continuar» en el píxel 714 de una ventana de 683, fuera de alcance
  del ratón. Medido en CI. Una columna «* hasta 540» es el `max-width` que el prototipo usa: todo lo
  ancha que quepa, nunca más, y pegada a la izquierda igual.


### Corregido

- **Cuatro detalles que el prototipo tenía y esta aplicación no.** La píldora de tipo pierde su
  palabra en los carruseles y la conserva en la cuadrícula, que es donde el prototipo la escribe —una
  ficha que dice «Película» sobre un tercio de la miniatura compite con la imagen—. «Añadir medios…»
  recupera su signo más. La cabecera del reproductor recupera su segunda línea: lo que se le pasaba
  sólo tenía valor para un episodio, así que una película llegaba con el título y nada debajo. Y la
  píldora de velocidad se escribe «VELOCIDAD», que es lo que cabe: el rótulo largo la hacía más ancha
  que los cuatro botones del transporte juntos, y sigue siendo el nombre accesible del control.


### Corregido

- **Una temporada vuelve a parecerse a sí misma.** Cada miniatura de episodio se coloreaba con el
  hash de **su propio nombre**, así que dieciséis episodios de una serie eran dieciséis colores sin
  relación. El prototipo dibuja `art(serie + episodio × 7)`: el tono de la serie, caminado unos
  grados por episodio. La carátula acepta ahora ese desplazamiento y la fila lo pide.

- **El panel de «Siguiente episodio» deja de encogerse.** Se ajustaba a su contenido, así que
  «Continuar» cambiaba de sitio según lo largo que fuese el nombre del episodio. El prototipo le da
  540 px fijos y deja que el texto se lleve la holgura.


### Corregido

- **La copia elegida se marca en toda su fila, y el enlace lleva la tinta del acento.** El prototipo
  señala la elección tres veces a la vez —el radio, el borde de acento y el lavado del acento detrás
  de la fila— y la vista sólo tenía la primera: una marca de quince píxeles en una fila de mil. El
  título del grupo deja de ir en azul, que es como el prototipo lo escribe, y los tres enlaces de la
  aplicación pasan del acento a su **tinta**: mismo sitio, mismo tamaño, y de 5,62:1 a 9,03:1 en
  claro y de 8,29:1 a 11,36:1 en oscuro. El par nuevo se mide en `ContrastTokenTests`.

- **La columna de tamaño deja de decir «0».** Bajaba hasta megabytes y redondeaba: un archivo de dos
  bytes se leía como vacío justo en la pantalla donde alguien decide qué copia conserva. La escala
  llega ahora hasta los bytes, y sólo un tamaño de cero —o ninguno registrado— se queda en blanco.


### Corregido

- **Cuatro ramas que nada podía ejecutar, y las dos que sí y nadie miraba.** El lector de duplicados
  comprobaba si eran nulas tres columnas que el esquema declara `NOT NULL`, y el modelo volvía a
  preguntar por su parámetro después de que `CanExecute` lo hubiera exigido: código inalcanzable, que
  se retira en vez de medirse. Las que sí ocurren llevaban sin prueba y ya la tienen: progreso
  guardado antes de que el motor sepa la duración —el estado normal de los primeros segundos— y un
  códec escrito antes de que esa columna guardara JSON.


### Corregido

- **Tres cosas declaradas y sin alimentar, retiradas o alimentadas.** La ficha roja de estado —lo que
  dice que un archivo no está es la insignia compartida, y una segunda forma diciendo lo mismo es lo
  que `UnavailableBadgeTests` existe para impedir—, el icono de carpeta —esta aplicación no abre el
  Explorador desde una ficha— y el de «sale de la aplicación», que sí tiene dónde ir: el botón de
  recuperación del reproductor que abre el archivo con otro programa.

### Corregido

- **Dos mandos que ya no leía nadie, y dos ramas que nada podía ejecutar.** Al llevar las decisiones
  a la tarjeta, `AcceptSelectedCommand` y `RejectSelectedCommand` quedaron declarados sin consumidor
  —el defecto de la casa, introducido por el propio cambio— y se retiran. Las guardas de nulo dentro
  del trabajo de los tres mandos de la tarjeta también: `AsyncRelayCommand` pregunta a `CanExecute`
  antes de correr, así que una segunda comprobación era una rama que nada puede tomar.

- **El raspador del reproductor se pulsa con la sesión detenida.** Desde que el transporte observa la
  posición del motor, una sesión en marcha mueve la propia sonda del paseo: el clic de al lado, que
  debe no cambiar nada, cambiaba algo en cuanto el ejecutor iba un poco lento. Medido en CI, dos
  veces.

### Corregido

- **El héroe de Inicio termina en la página, no en una línea.** El prototipo dibuja dos velos sobre
  la obra: el direccional que ya estaba y el color de la propia página subiendo desde el borde
  inferior. Va como pincel de superficie tras una máscara de opacidad y no como degradado de colores,
  porque el color tiene que ser el del tema —son cuatro diccionarios, y un hexadecimal escrito ahí
  acierta en uno.

### Corregido

- **Las tres herramientas del título están en la ficha, no bajo la biblioteca.** «Editar metadatos»,
  «Previsualizar renombrado» y «Revisar versiones» eran una fila de botones bajo la cuadrícula, que
  actuaban sobre «el título abierto» en una pantalla donde puede no haber ninguno abierto. El
  prototipo las pone en la fila de acciones del banner, y ahí están —una sola vista montada por las
  dos fichas, para que la identidad que el paseo pulsa siga siendo una.

### Corregido

- **La bandeja de revisión deja de ser una lista con selección.** Sus filas eran controles de mando, y
  con las decisiones dentro de la tarjeta eso dejó a la prueba del paseo sin sitio donde pulsar «al
  lado» —medido en CI: ningún punto alrededor de «Aceptar» cae fuera de otra tarjeta—. La bandeja se
  dibuja como la de duplicados, que nunca tuvo el problema: tarjetas en una lista sin selección, y
  las flechas arriba/abajo se van con ella porque lo que un teclado recorre ahora son botones.

### Corregido

- **El reproductor dice qué está reproduciendo.** La banda superior tenía tres glifos y nada en medio,
  con la razón escrita en la propia vista: la sesión guarda una ruta, y pintar la ruta del archivo de
  alguien como encabezado es lo contrario de para lo que sirve esta aplicación. Ahora el título y su
  línea **viajan con la petición**, desde la tarjeta que pulsó Reproducir — que es la que lo sabe—, y
  la cabecera escribe «El Faro de Piedra / 2019 · Drama · Misterio · 96 min».

- **El transporte vuelve a ser una fila.** Reproducir, pausar y parar vivían en una segunda línea
  porque sus mandos son del coordinador de sesión y los saltos son de `ControlPlayback`. Eso es un
  hecho sobre los modelos, no sobre la fila: los botones cruzan hasta el modelo del reproductor a
  través de la vista que los hospeda, y el orden es el del prototipo —atrás, reproducir, adelante—
  con la velocidad llevando su palabra y no sólo su número.

- **Los atajos del reproductor están escritos donde se usan.** Espacio, las flechas, F, N y Escape
  estaban todos enlazados y anunciados de uno en uno; quien no abre un lector de pantalla los
  aprendía aquí o no los aprendía. El prototipo escribe esa línea bajo el transporte y ahora está.

### Corregido

- **La bandeja de revisión enseña el archivo del que habla.** Pedía una decisión sobre
  «movie:761053» y no mostraba de qué archivo hablaba: la proyección de candidatos ya no sólo trae el
  identificador, trae la ruta. Cada tarjeta lleva ahora la carátula, «ARCHIVO PENDIENTE» con el
  nombre y la carpeta, el candidato con su tipo, la confianza y las señales — y **las tres decisiones
  dentro de la tarjeta**, que es donde el prototipo las pone. Estaban una fila más abajo actuando
  sobre «lo seleccionado», que es una decisión por bandeja en vez de una por archivo.

- **Enter sobre «Rechazar» aceptaba.** Medido: el atajo de la lista contestaba antes que el botón con
  el foco, así que el teclado aceptaba justo lo que se intentaba rechazar. El atajo se retira; una
  tarjeta con tres acciones no puede tener una tecla que elija una de ellas en secreto.

- **La página de duplicados es la tabla del prototipo.** Listaba títulos y un número, con la
  comparación a un clic. Ahora cada grupo trae su tabla —archivo, resolución, códec, audio, tamaño,
  duración, ubicación y disponibilidad— con el radio que fija qué copia se reproduce por defecto, sin
  abrir nada. El lector hace el trabajo en una sola consulta, y el título sigue siendo la puerta a la
  comparación de siempre.

- **«1 episodios» dejó de escribirse.** El recuento de la ficha de serie cambia de palabra en el
  singular, en los dos idiomas.

### Añadido

- Cobertura de lo que llegó con el rediseño y CI midió a la baja: la igualdad de una fila del
  catálogo con sus seis miembros nuevos, las dos ausencias del título y la duración de un episodio,
  el año y los géneros que Inicio lee —presentes y ausentes—, las líneas de las dos fichas con cada
  pieza que puede faltar, los dos botones de cada tarjeta del riel, y la tabla de duplicados leída
  del almacén real.

### Corregido

- **La ficha de serie es la del prototipo.** Bajo el título va la línea «2020 · Drama · 3 temporadas
  · 16 episodios», la barra de la serie con «10/16 vistos», y el panel que nombra el episodio que
  falta —«T02·E05 · Puerto de invierno», «Reanudar en 17:00»— con su botón «▶ Continuar», que es la
  única acción acentuada de la ficha. Las temporadas dejan de ser un desplegable y pasan a ser
  **píldoras**, las tres a la vista; y cada episodio pasa de una tira de 56 px con un número a la
  **tarjeta** del prototipo: el número, la miniatura apaisada con su barra de progreso, el nombre del
  episodio y «48 min · Visto». El nombre y la duración no estaban en pantalla en absoluto —la
  proyección de episodios no los leía—, así que una temporada se leía como una columna de números.

- **Las dos fichas se desplazan como una página.** Había dos regiones de desplazamiento en una misma
  pantalla —el banner fijo y la lista con su propia barra—, así que la rueda contestaba una cosa u
  otra según dónde estuviera el puntero. Y la vuelta a la biblioteca es un enlace, «Volver ·
  Biblioteca», como en el prototipo, en lugar de una píldora rellena compitiendo con la acción de la
  ficha.

- **Las marcas personales salen del banner.** Diez botones de valoración sobre la obra de arte
  empujaban los episodios fuera de la primera pantalla. Van a la columna «Otras acciones», donde la
  ficha de película ya las tenía, y con la forma del prototipo: filas de ancho completo con su icono,
  su nombre y lo que la marca vale ahora mismo.

- **Un tercio del degradado de los banners estaba mal medido.** La última parada se escribió `#30`
  pensando en «30 %», y `0x30` es el 19 %: la imagen salía la mitad más clara de lo que el prototipo
  pinta en el borde derecho. Corregido en las dos fichas, y el héroe de Inicio pasa a su propio
  degradado, que es otro —`rgba(5,6,8,…)` y con las paradas dentro del marco, no en sus bordes—.

- **CI no verificaba nada desde el 2026-08-24.** Los tres últimos runs murieron en «Install ffmpeg»
  con un 503 del repositorio de Chocolatey, antes de compilar una sola línea. El paso reintenta tres
  veces con espera creciente; la versión sigue clavada, así que lo que un reintento puede cambiar es
  el transporte y nada más.

### Añadido

- `AccentInkBrush`, la tinta que se escribe **sobre** el lavado del acento, que es de lo que está
  hecha una píldora elegida. Sale del `--accent-ink` del prototipo en los cuatro modos y trae su
  pareja de contraste medida.

### Corregido

- **La referencia con la que se comparaba estaba a media luz.** Las dieciséis capturas del prototipo
  archivadas se tomaron a mitad de su animación de entrada (`apr-in`, de opacidad 0 a 1): medido
  sobre las ocho vistas, todo lo que anima salía entre **1,3 y 1,9 veces más oscuro**, mientras el
  fondo de la página —que no anima— coincidía con su token. Un póster medía `#2A1722` donde el
  prototipo pinta `#6A2C46`, que es exactamente lo que da su propia fórmula `hsl(330 38% 30%)`. La
  referencia se ha vuelto a tomar con `--force-prefers-reduced-motion`, que es lo que el prototipo
  ya prevé, y **cada una está comprobada contra una segunda captura**: dieciséis vistas, dieciséis
  coincidencias píxel a píxel.

- **Inicio es la portada del prototipo.** El héroe sangra hasta el borde, sin tarjeta ni margen, con
  su antetítulo espaciado, la línea «2019 · Drama · Misterio · quedan 44:00», la barra sin
  porcentaje y **dos** botones —«▶ Continuar · 52:00» y «Detalles»—, que es el par que el prototipo
  siempre tuvo y el árbol dejó a medias con una nota diciendo que el segundo llegaría «el día que el
  modelo de lectura pueda responder por él». El raíl de en curso deja de ser una fila de carátulas y
  pasa a ser lo que el prototipo dibuja: **tarjetas apaisadas** con su imagen, su barra al pie, el
  título, «Película · 2019» y los mismos dos botones en cada una. Y el enlace a la biblioteca se va
  al encabezado de «Añadido recientemente», con su punta de flecha, que es donde el prototipo lo
  escribe.

- **El arte de las portadas se pinta en un solo sitio.** Cinco superficies deletreaban las mismas
  cuatro capas, y a tres les faltaba la trama. Ahora todas montan `PosterArtView`, y con eso **la
  lista de excepciones de la puerta de color-por-sí-solo pasa de cuatro entradas a una**: esa lista
  sólo puede encoger, y así es como encogió.

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
