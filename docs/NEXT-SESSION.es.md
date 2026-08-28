# Dónde retomar

## Estado al cierre del 2026-08-28 (quinta sesión) — la cola de cuatro puntos, cerrada entera

**`main`, la rama y el HEAD son el mismo commit: `935fcb0`, verde comprobado con
`gh run view --json conclusion`.** No queda nada sin publicar ni sin verificar. `main` avanzó **seis
veces** en la sesión —`8aa2304`, `a16a523`, `d7b5804`, `f0a94ac`, `7e86525`, `935fcb0`—, cada una con
la conclusión de su run leída **antes** de mover la referencia.

**Dos rojos por el camino, los dos reales y los dos corregidos**: un suelo de cobertura que había que
subir con el número de CI (`ArtworkCache.cs`, 96/70), y el clic «al lado» del paseo aterrizando en el
botón vecino porque la fila del transporte se recompone. Ninguno se reprodujo en local.

**Y una lección de ritmo**: empujar cuatro commits seguidos puso **tres runs solapados**, y el paso
`Verify` de uno pasó de 31 a **48 minutos** compitiendo con los otros. Salía más barato agrupar los
remates.

**El punto 1 de la cola está cerrado.** El minirreproductor abre sin marco, se arrastra por la
imagen, se redimensiona desde sus ocho bordes conservando el **16:9 del prototipo** —que es el de la
imagen, con la altura del cromo sumada encima— y **recuerda dónde se dejó entre sesiones**.

### Lo que decidió el diseño fueron dos mediciones, no dos razonamientos

1. **Un botón de Avalonia NO marca su pulsación como atendida.** El primer arrastre dejaba pasar lo
   que otro control ya hubiera atendido, y se midió con un `MouseDown` del arnés sobre
   `MiniPlayerPlayPause`, con un manejador registrado `handledEventsToo: true`: `seen=1 handled=0`.
   Avalonia marca el **soltar**, que es donde está el clic. Esa guarda no guardaba nada, y **los cinco
   controles del cromo habrían arrastrado la ventana en vez de funcionar**. Lo que decide ahora es
   dónde cae la pulsación: la imagen arrastra, la franja del cromo no.
2. **Un backend headless nunca levanta un redimensionado de usuario**: todos llegan con
   `reason=Layout`. Un filtro `e.Reason == User` enterrado en el `override` habría dejado toda la
   corrección detrás de una rama que ninguna prueba puede tomar, así que la decisión vive en
   `HandleResize(WindowResizeReason)`, público, y la prueba la llama.

### El defecto de la casa otra vez, y cerrado

`PlayerWindowCoordinator.Remember` y `Recall` existían desde el 2026-08-19 y **el código de producto
no los llamaba nunca**: `0` llamadas en `src/`, `3` en `tests/`. Registrado y nunca alimentado. Ahora
`ShellView` los llama, y el coordinador escribe la mitad que sobrevive al proceso a través de
`IMiniPlayerPlacementStore` → `StoredMiniPlayerPlacement` sobre el `ISettingsStore` de siempre.

La colocación se escribe **al cerrar la ventana y no al moverla**: un arrastre levanta un evento por
fotograma y esto va a un archivo. Y una colocación que ya no cae en ninguna pantalla se descarta **al
usarla**, no al guardarla: sin barra de título, una ventana en las coordenadas de un monitor
desconectado no habría forma de recuperarla.

### Lo que hay que mirar en el CI de esta tanda

**`ShellView.axaml.cs` sube por encima de su suelo, y eso es un rojo de la puerta de cobertura**, que
rechaza igual un archivo por debajo que uno por encima. Medido en esta máquina con la misma suite:
`97,50/67,85` antes → `98,03/69,23` después, tras quitar tres guardas que no guardaban nada (el `??=`
que reutilizaba una ventana que el shell ya había cerrado, el `sender is not MiniPlayerWindow` de un
manejador enganchado a una sola ventana, y el segundo `Screens.Primary?.Bounds ?? …` escrito al lado
del primero). **La corrección es el número del artefacto `coverage-debt` de ese mismo run**, nunca uno
medido aquí. `MiniPlayerWindow.axaml.cs` no está en la lista, su listón es 96/96 y mide 98,73/97,61.

### El punto 2, y la decisión de alcance que hizo falta preguntar

**El póster estaba a dos extremos de existir, y lo que faltaba no era código sino una decisión.**
Medido antes de tocar nada:

- `PosterPath` guarda el `poster_path` de TMDB tal cual —`/wXsQ….jpg`, una ruta **remota**—, no un
  archivo local.
- `ArtworkCache` existe **completa y probada**, con su techo de 10 MB (SEC-005), y `image.tmdb.org`
  lleva declarado en `NetworkPurposeRegistry` desde siempre.
- Pero estaba **fuera del contenedor** por ART-A01 (2026-08-09), y `CompositionDescriptorTests`
  **afirmaba su ausencia**. Más la decisión del 2026-08-21 de dejar las portadas fuera de 0.2.0.

Con dos decisiones registradas en contra y una prueba que ataba una de ellas, **la pregunta se hizo**
y el propietario respondió: revertir ART-A01 y hacerlo entero. Está hecho, en el orden que la propia
entrada de ART-A01 había escrito.

**Una ficha no abre nunca una conexión.** El puerto es asimétrico a propósito: `Find` sólo mira el
disco y `FetchAsync` es lo único que sale a la red, llamado una vez desde `ApplyIdentification` —el
único momento en que alguien ya ha consentido hablar con el proveedor—. Consecuencia que hay que
saber: **una biblioteca identificada antes de hoy recibe sus pósteres en su siguiente identificación
o refresco**, igual que un título escaneado recibió su nombre.

`PosterAddressPolicy` comprueba **antes** de componer, como la política del tráiler, y su prueba
afirma además una propiedad: lo que se construye siempre es `https`, sobre el host declarado, bajo el
segmento del tamaño, sin consulta ni fragmento.

### Lo que hay que mirar en el CI de esta tanda

**Y aquí la trampa del informe fusionado se cobró su décima alarma falsa, sobre esta misma tanda.**
El pronóstico escrito antes de que CI midiera decía que `ArtworkCache.cs` y `MovieDetailsViewModel.cs`
subían a 100/100 y que el primero saldría de la lista. **Los dos datos eran falsos**: CI midió
`ArtworkCache.cs` en **96/70** y no nombró a `MovieDetailsViewModel.cs` en absoluto.

La causa es exactamente la que la nota del 2026-08-25 ya avisaba: **la puerta mide con el informe
fusionado**, y la lectura local se hizo **tomando el mejor informe por suite**. Un archivo que una
suite no ejercita aparece en su informe con ceros, y la fusión los suma en vez de quedarse con el
mejor. Un script que toma el máximo **engaña**, y engañó.

El suelo queda en `96 70`, con el número del artefacto de ese run, y `ArtworkCache.cs` **sigue en la
lista**: 96 de línea llega al listón y 70 de rama no. El trinquete sigue en 212.

### Un rojo de CI que no se reproduce aquí, y su causa

`The_players_transport_is_operated_with_the_mouse` respondió en CI `Expected: Embedded, Actual:
Fullscreen`, en una aserción que se lee **antes** de pulsar nada de modo. **No se reproduce en local**:
el caso solo tres veces y la suite entera dos, todas verdes.

La causa está en la escena y no en el producto: manda la sesión a **1,5×** justo antes, y «Volver a
1×» **aparece** cuando la velocidad deja de ser 1×, así que la fila del transporte se recompone y todo
lo que está a su lado se mueve. `PressAsync` elige el punto que pulsa «al lado» de la **geometría en
pantalla**, y lo eligió antes de que el hueco del reset estuviera medido: aterrizó en el botón de
pantalla completa.

Corregido asentando el layout —`InvalidateMeasure()` + `RunJobs()`— entre el cambio que recompone la
fila y el `PressAsync` que apunta a ella, y leyendo el modo en ese punto para que un aterrizaje en el
vecino se cace donde ocurre. **Regla: si una línea de la escena cambia qué controles hay en una fila,
asienta el layout antes de apuntar a esa fila.**

### Una trampa local nueva: `PackagingTests` está roja aquí y verde en CI

Tres pruebas de empaquetado fallan **en esta máquina** —`Arm64PackageTests`, `ReproducibleBuildTests`
y dos de `MsixLifecycleTests`— y la primera dice `BackgroundColor="#08090C"` esperado contra
`#111827` medido. **No es de esta tanda**: se midió con `git stash`, y los mismos tres fallan sin
ninguno de los cambios. Y CI las pasa —191 superadas y 3 omitidas en el run de `d91b9d6`—, porque el
flujo genera los artefactos del paquete y esta máquina los tiene caducados desde hace días.

Así que **`PackagingTests` no es una suite afectada por trabajo de vistas o de casos de uso**, y su
rojo aquí no significa nada hasta que se corra el ciclo del sandbox entero.

### Tres decisiones que quedaban y se toman aquí

1. **La hipótesis (a) de «secciones cortadas» —con las listas llenas— NO recibe puerta propia.** Lo
   que una puerta así tendría que hacer es construir un contexto de datos para las 17 vistas que
   llevan un `ItemsControl`, y lo que mediría ya está cubierto por dos lados: **las filas y tarjetas
   que esas listas repiten son vistas por derecho propio y se miden solas** —`LibraryEntryView`,
   `EpisodeRowView`, `CandidateCardView`, `PosterCardView`— y **el paseo recorre la aplicación con
   datos sembrados y rechaza un clic que no aterriza**. El coste de la puerta es alto, su superficie
   nueva es pequeña, y una puerta frágil que hay que mantener es peor que una ausencia declarada.
   **Criterio si aparece un hallazgo real**: se ataca la vista concreta con su contexto, no las 17.

2. **El cromo del minirreproductor con la composición del prototipo —título, tiempo y barra de
   progreso de tres píxeles sobre los cinco botones— es una pieza propia y va a la cola, no a un
   remate.** Es composición de una vista, del tamaño de un tramo de la §4. **Y trae una medición
   hecha**: a 480×270 esos cinco botones ya plegaron en tres filas una vez por una palabra traducida,
   así que quien lo haga mide el ancho **en los dos idiomas** — que es justo lo que las dos puertas de
   ancho hacen desde hoy.

3. **`ArtworkCache.cs` NO se sube al listón ahora, y hay un techo que lo explica.** Su suelo quedó en
   `96 70` con el número de CI. Lo que le falta está medido, y casi todo es barato: las dos respuestas
   `image/png` e `image/webp` de `MediaExtension`, el lado nulo del `??` del `HttpClient` y el de
   `allowedHosts`, y los dos `Directory.Exists` en su rama contraria. **Lo que pone techo es
   `EnsureRemoteRootIsConfined`**: su `throw` es una guarda de invariante de seguridad que ningún
   llamador legítimo puede alcanzar, y **ésa no se borra** — no es una guarda redundante como las del
   2026-08-28, es la que mantiene la caché dentro de la raíz de datos. Subirlo exigiría reescribirla
   para que sea alcanzable, y eso cambia una promesa de seguridad por un punto de cobertura.

### La cola, con los puntos 1 y 2 tachados

1. ~~El mini como ventana PiP de verdad.~~ **Hecho.** Lo único que queda de su cromo es la
   composición del prototipo —título, tiempo y una barra de progreso de tres píxeles sobre los cinco
   botones—, que es otra pieza y no un remate de ésta.
2. ~~El póster de fondo del cabecero de la ficha.~~ **Hecho, en las dos fichas** — la de película y
   la de serie, que el prototipo levanta a 136×204 contra el mismo muro sangrado. Las portadas de la
   cuadrícula y de las tres filas de Inicio **siguen fuera**, con la razón medida del 2026-08-21
   intacta: arrastran la cuadrícula, que cuesta 7× el tiempo y 455× los controles vivos por perder la
   virtualización.
3. ~~El editor de metadatos como vista propia~~ (decisión 15). **Hecho.** Es la página del prototipo:
   «Volver · Biblioteca», cabecero de dos líneas, dos píldoras `segment` y la herramienta debajo. No
   es una `AppRoute` —los cinco destinos están afirmados por nombre y el paseo llega a cada uno por su
   botón del carril—, sino una página que **cubre** el hueco de Biblioteca, como hace una sesión.

   **Dos cosas que sólo aparecieron al medir**, y que valen para la próxima vista que se mueva:
   atar la lista a `!HasEditorPanel` a secas dibujó la biblioteca **sobre Ajustes** (lo cazó
   `ThemeTests` contando 16 botones donde hay 13), y **el paseo encontró un callejón** —
   `TitlePreviewRenameAction matched 0 controls` — porque la página tapa la ficha de la que se abrió.
   Por eso las dos píldoras **abren** además de seleccionar, que es lo que hace el prototipo.

4. ~~«Secciones cortadas por el ancho»~~ **Encontrado, y no era el ancho.** `HomeView` pide **683 px**
   y era el único destino montado en un `ContentControl` pelado, sin `ScrollViewer`: con `MinHeight`
   en 600, **83 px de Inicio no se podían alcanzar**. Es la hipótesis (c), la vertical.

   Lo cierran dos puertas nuevas: el idioma pasó a ser **parámetro** de las dos suites de ancho —
   fijaban `es-ES`, así que el otro idioma era una suposición, hipótesis (b) — y hay una tercera que
   afirma que **una vista más alta que la ventana está dentro de algo que se desplaza**, leyendo el
   árbol del shell y no una lista.

   **(d) también está medida y descartada**: a 1920 px sólo dos vistas se quedan cortas,
   `ContinueCardView` (332) y `PosterCardView` (148), **y las dos son tarjetas** — crecer con la
   ventana es justo lo que no deben hacer. Ninguna página se queda corta, y no lleva puerta porque las
   dos excepciones son legítimas y permanentes.

   **Queda sólo (a)**, con las listas llenas: la cubre en parte el paseo, que rechaza un clic que no
   aterriza, pero «rechazar un clic» no es «medir cada control», y esa diferencia es lo que sigue sin
   puerta.

## Estado al cierre del 2026-08-28 (cuarta sesión) — la grieta de la deuda, cerrada y con puerta

Ocho commits sobre la rama, **CI en verde en `c85b6cb`**, y **`main` avanzado por fast-forward hasta
ese mismo SHA**: la rama, `main` y el HEAD son el mismo commit verificado, y **no queda nada sin
publicar ni sin verificar**. `main` pasó por tres saltos en el día —`8ce6ef8`, `e49a5e6` y
`c85b6cb`—, cada uno con su verde comprobado con `gh run view --json conclusion` **antes** de mover la
referencia.

Las tres cifras del verde final, que son las que cierran la sesión:

- `Coverage gate: 212 file(s) still short of 96/96, ratchet 212, **212 measured under the bar**` — la
  lista y lo medido son el mismo número. Eran 212 y 216.
- `The walk: 228 declared command controls in 223 identities; 203 pressed, 20 pending` — el trinquete
  quieto con una identidad más.
- `2 new file(s) against origin/main ... are where they have to be` — `ScannedTitlePolicy.cs` y
  `NameScannedTitles.cs` llegan al 96/96 que un archivo nuevo tiene que traer.

Los cuatro puntos del encargo están cerrados. El quinto —el mini como ventana PiP de verdad y el
póster de fondo del cabecero— **no se ha tocado**: cada uno es una pieza entera y no un remate, y
abrir una a medias habría sido peor que dejarla nombrada.

**La cola, ordenada, es ésta:**

1. **El mini como ventana PiP de verdad.** Media hecha: ya no duplica la barra, ya es `Topmost` y ya
   tiene geometría por modo. Falta: sin marco (`SystemDecorations="None"`), arrastrable
   (`BeginMoveDrag`), conservando la relación de aspecto al redimensionar, y **recordando dónde se
   dejó entre sesiones** — hoy `PlayerWindowCoordinator.DefaultMiniGeometry` es una constante.
2. **El póster de fondo del cabecero de la ficha** (decisión 6). **Ojo, y está medido**: `PosterPath`
   existe en los metadatos y **no llega a ninguna vista** —`MovieDetailsViewModel` no lo lee—, así que
   son dos trabajos: llevarlo hasta la ficha y dibujarlo. Y en una biblioteca sin identificar no hay
   ningún póster, así que hace falta el arte generado detrás de todos modos.
3. **El editor de metadatos como vista propia.**
4. **«Secciones cortadas por el ancho»**, con el eje del ancho ya descartado — ver abajo.

### 1. El menú de velocidad es el desplegable del prototipo

Era un `MenuFlyout` de diez números y una undécima fila que reiniciaba. Ahora es la píldora que el
prototipo dibuja, con **nueve** filas de tres columnas —marca, nombre y nota—, abriendo **hacia
arriba**, y «Volver a 1×» como botón al lado.

**Lo que decidió la forma fue el paseo y no el gusto.** Nada dentro de un `Flyout` es alcanzable por
el arnés: las veinte entradas de `eng/walk-pending.txt` son exactamente eso, hijos de un flyout, y ese
trinquete no sube. Un `ComboBox` sí se pulsa y se afirma sobre `IsDropDownOpen`, como ya hacen los dos
filtros de Biblioteca, y sus filas son `ComboBoxItem`, que el inventario no cuenta. Así el inventario
gana **una** identidad —la del reinicio— y el paseo pasó de 202 a **203 pulsados**, con el trinquete
quieto en 20.

De paso: `PlaybackControlPolicy.SpeedSteps` **no lo leía nadie**. El menú escribía sus diez números en
su propio marcado y una prueba leía ese `.axaml` **como texto** para comparar. Ahora el menú se
construye de la política y la prueba pregunta al modelo. El `1,75×` que el prototipo no ofrece se fue
con ello.

### 2. Los glifos del transporte

Sólo uno estaba mal, y el resto ya coincidían con el prototipo carácter por carácter (eso lo ata
`PrototypeIconTests` desde el 2026-08-24). **El botón de pantalla completa dibujaba las flechas de
entrar también estando ya dentro**, y `IconExitFullscreen` llevaba en el diccionario desde el
principio: mismo defecto que el silencio tuvo en agosto.

Lo que lo tapaba: **la tabla que ata qué glifo lleva cada botón listaba once de trece.** Los dos
botones de modo llevaban tres días en la barra sin estar en ella, y el que estaba mal era uno de los
dos que faltaban.

### 3. El título de una película sin identificar

«El Faro de Piedra 2019» era el nombre del archivo tal cual, con el año dentro del título y la columna
del año vacía al lado. `ScannedTitlePolicy` (dominio, pura) dice cómo se llama la tarjeta, y
`NameScannedTitles` —hermano de los otros dos casos de uso post-escaneo— la escribe. Migración
**0021** para el año.

**Una biblioteca ya catalogada se renombra sola en el siguiente escaneo y sin re-sondear nada**,
porque el paso recorre todo el resumen, `Unchanged` incluidos — un archivo cuyo tamaño y fecha no se
han movido nunca vuelve a guardarse, así que una proyección escrita una vez se habría quedado con el
nombre crudo para siempre.

### 4. La grieta de la lista de deuda, cerrada y con puerta

**Medido**: tres ejecuciones seguidas de CI midieron **216** archivos bajo el listón mientras la lista
nombraba **212**, y los cuatro que faltaban miden lo mismo en las tres — no bailan. Y ningún archivo
de la lista `$watched` cae bajo el listón en CI, así que la diferencia son exactamente esos cuatro.

Los cuatro **se cerraron** en vez de escribirse, así que el trinquete sigue en **212**:

- `PlayerView.axaml.cs` **65/41**, la pareja más baja del árbol. Dos vistas lo montaban y ninguna le
  daba contexto, así que **ninguno de sus dos manejadores se había ejecutado jamás**.
- `PlayerViewModel.cs` **98/91**, quitando tres guardas que nada podía tomar.
- `PlaybackPreference.cs` **98/92** y `DisabledOutline.cs` **100/87**, con la rama que nadie tomaba.

Y `check-coverage.ps1` pide ahora que la lista sea **completa** y no sólo exacta. Fuera de CI informa
y no bloquea, igual que los suelos.

### Tres decisiones que estaban abiertas y ya no lo están

1. **El acento que cae sobre el anillo de foco se respeta tal cual.** La pregunta era «¿un paso o un
   ratio?» y la respuesta es ninguna: el adorno de foco son **dos anillos concéntricos** a 3:1 entre
   sí, así que la señal del teclado es **geometría** y sobrevive a cualquier acento. El paso de un
   byte era teatro y se ha ido, con el parámetro que lo alimentaba. Lo que sigue vigilado es lo que ve
   todo el mundo: los cuatro diccionarios, en `ContrastTokenTests`.

   **Y aquí la puerta nueva se ganó el sueldo en su primera vuelta, sobre un cambio de esta misma
   tanda.** Quitar el apartado adelgazó `AccentPalette.cs` lo justo para que su agujero de siempre
   pesara: CI lo midió en **99/93** y lo nombró por no estar en ninguna lista. El agujero era un
   `return` de reserva del recorrido de luminosidad que **ningún predicado del archivo podía
   alcanzar** —`EqualContrastLuminance` está donde negro y blanco contrastan igual, y ese contraste es
   4,58:1, por encima del 4,5 más estricto, así que un extremo siempre acepta—. Metido el extremo
   dentro del recorrido, el archivo queda en **100/100**. Sin la puerta, nadie lo habría visto.

2. **«Home queda totalmente vacío» ya estaba resuelto** —`HomeReadModel` hace `UNION ALL` con
   `scanned_titles` desde el 2026-08-25— **pero se destapó lo de al lado**: el año de la migración
   0021 se puso en la unión de Biblioteca y no en la de Inicio. Son la misma consulta escrita dos
   veces y ahora hay una aserción que las ata.

3. **«Secciones cortadas por el ancho»: medido y NO está en el ancho.** Era la primera limitación
   declarada de `ViewOverflowTests` —mide cada vista sola a 900, y dentro del shell el carril se lleva
   64—, así que `ViewOverflowInShellTests` las mide contra el hueco real, **836 px tomados del shell**,
   con el reproductor incluido porque el prototipo deja el carril a la vista en una sesión embebida.
   **Ninguna de las 48 se pasa.** La ausencia está probada.

   **Lo que queda vivo como hipótesis**, para no repetir el trabajo: (a) con las listas **llenas**, que
   es la segunda limitación que las dos puertas siguen declarando; (b) con las cadenas del **otro
   idioma**, que ya plegó el cromo del mini en tres filas una vez; (c) que «cortadas» sea **vertical**
   y no horizontal — Ajustes mide 1.797 px de alto, y eso encaja con la palabra; (d) con la ventana
   **ancha**, donde lo que falla no es que algo se salga sino que algo no crezca.

### Las trampas que costaron tiempo aquí

- **Un modelo que resuelve un recurso en su constructor convierte a todos sus llamadores en llamadores
  del hilo de UI.** `SpeedOptions` se construía ahí y dos `[Fact]` que sólo preguntaban por un playhead
  fallaron con «the calling thread cannot access this object». Se construye en la primera lectura.
- **`Gestures` es internal en Avalonia 12.1.1**; el evento público es `InputElement.DoubleTappedEvent`.
  Es la misma clase de premisa que ya falló con `ItemsRepeater`: se comprueba, no se supone.
- **Un `ContentControl` cuyo `IsVisible` se enlaza a una propiedad del modelo no se llena poniéndole
  `Content` a mano.** El transporte hay que dárselo al `PlayerViewModel`, o queda en un contenedor
  oculto sin hijos que encontrar.
- **Un script de PowerShell que reescribe un archivo puede cambiarle los finales de línea**, y
  `dotnet format` lo caza como `ENDOFLINE` en cada línea del archivo entero.
- **La versión del esquema tiene tres afirmaciones**: el conteo, el máximo y la lista de nombres de
  `SqliteBootstrapTests`. Una migración nueva mueve las tres.
- **Un `Test Case Cleanup Failure` con «the calling thread cannot access this object» NO es del
  código: es el arnés reciclándose.** CI falló así el 2026-08-28 en `TransportGlyphTests`, y la traza
  entera era de Avalonia —`HeadlessUnitTestSession.EnsureIsolatedApplication` → `Compositor..ctor` →
  `Dispatcher.VerifyAccess`— sin una sola línea nuestra. **Y la prueba que xUnit nombró no era la
  culpable**: duró 1 ms, ni llegó a correr; lo que falló fue preparar la aplicación **para** ella. La
  causa era la prueba nueva de al lado, que abría una cuarta ventana a mano en una clase cuyo scope ya
  abre tres por prueba, y que además no la cerraba si una aserción fallaba. **No se reproduce en
  local** —dos pasadas de 958 en verde—, así que la salida es la de siempre: la carrera se quita, no
  se busca. La prueba se mudó al archivo cuyo patrón de montaje acababa de pasar CI, con la ventana
  cerrada en un `finally`.

- **`App.ApplyLanguage` cambia los diccionarios y NO toca `CultureInfo.CurrentCulture`.** Una prueba
  que afirma «0,25×» tras aplicar el idioma pasa en una máquina en es-ES y **falla en el runner**, que
  está en en-US y escribe «0.25×». Lo cazó CI y no el árbol. La corrección no fue quitar la aserción
  sino **fijar las dos cosas por separado**, con lo que la prueba dice algo más fuerte que antes: el
  número sigue a la máquina y las palabras al idioma elegido, que es lo que ve alguien con Windows en
  inglés y la aplicación en español.
- **`MediaTests` se cuelga en esta máquina cuando corre dentro de la solución con
  `--collect:'XPlat Code Coverage'`**, y sola pasa en 1 m 37 s. Dos consecuencias, y las dos engañan:
  deja un `testhost` bloqueando los `.dll` —así que la siguiente compilación falla con `MSB3026` y
  parece un error del código— y **no deja informe**, así que cinco archivos de LibVLC aparecen en la
  puerta de cobertura como «fell to 3/2» cuando lo único que pasa es que nadie los midió. Se mide
  suite a suite. Los suelos siguen siendo los de CI, que es exactamente por lo que esa regla existe.

## Estado al cierre del 2026-08-25 (tercera sesión) — los ocho del encargo, cerrados y medidos

Seis commits sobre la rama. **Todo verde en local**: Domain 519, Application 246, Architecture 30,
Documentation 91, Ui 917, Accessibility 146, Integration 470, y el paseo en 202 pulsados con el
trinquete quieto en 20.

**CI en VERDE dos veces seguidas** (`7c1decb` y `10dedc9`) y **`main` avanzado por fast-forward a
`10dedc9` el 2026-08-26**, con la orden del propietario de cerrar las decisiones pendientes: 65
commits de varias sesiones publicados de una vez. La rama, `main` y el HEAD son el mismo SHA
verificado — no queda nada sin publicar ni sin verificar, y la próxima sesión arranca desde un verde.

**Lo que hubo que mirar primero:** la puerta de cobertura en CI. Los dos suelos que la sesión anterior
dejó abiertos —`ShellViewModel` y `CompositionRoot.cs`— siguen siendo lo único que separa esta rama
de un fast-forward. `ShellViewModel` queda **cerrado en esta sesión**: la rama que faltaba era el
término del panel de subtítulos dentro de `HasPlayerPanels`, que **nada podía tomar** —es
`Player?.Tracks is not null` y también lo es la mitad de `HasAudioPanel`, que se evalúa antes—, así
que se ha borrado en vez de escribirle una prueba imposible, y las cuatro alternativas que quedan se
piden ahora una a una. De `CompositionRoot.cs` no se ha tocado el lado nulo de `ShellHost.Shell` en el
`ModeHandler`, que sigue siendo la rama que falta.

### El encargo, punto por punto

Ocho puntos: dos mejoras y seis defectos. Los ocho están cerrados, cada uno con su número.

1. **La alineación vertical de los botones, por tercera vez, y esta vez con el número correcto.** Los
   cinco píxeles estaban en el **relleno del botón**, que mueve todo el contenido: un glifo y la
   palabra a su lado viajan juntos, así que seguían exactamente igual de separados. Medido en un
   botón de 44: glifo en 19,00, tinta en 21,43 — **2,43 px**, el mismo número de siempre, intacto bajo
   la corrección que debía arreglarlo. Ahora el margen va **en la etiqueta**, y
   `ButtonOpticalCentreTests` sostiene también el icono contra la palabra, que era la mitad que
   ninguna puerta miraba.

2. **Los recuadros de selección de los menús.** Eran 2 px de acento alrededor de cada fila elegida de
   toda la aplicación, carátulas de carril incluidas. El prototipo lo hace al revés: lavado neutro
   `rgba(127,145,170,.16)` y el acento en la barra de 3 px del destino. Dos tokens nuevos y el borde a
   un píxel. **La cesión medida**: el trazo no es transparente porque `ListRowStateTests` exige 3:1 y
   un lavado al 16 % da 1,1:1 — así que lleva el borde neutro de 3,88:1.

3. **Las píldoras de filtro de Biblioteca** llevaban borde de control y tinta primaria sin elegir, así
   que las tres opciones parecían tres elegidas. Y **el desplegable no decía nada al abrirse** salvo
   dar la vuelta al galón.

4. **Las carátulas de Home no llevaban a ninguna parte** (eran tarjetas dentro de un elemento de
   lista). De paso: **el carril de sugerencias dibujaba veinte portadas de las iniciales de nada**,
   porque su búsqueda de títulos era un parámetro opcional que la composición nunca pasó.

5. **«Desde el principio» en las dos superficies anchas de Home**, con una bandera en la petición y no
   un segundo enganche.

6. **Continuar volvía a preguntar** lo que continuar ya había contestado: la posición pedida mandaba
   sobre la política desde el cambio de versión, y la otra mitad de esa decisión —no construir el
   aviso— no se había hecho.

7. **El botón de reproducir desaparecía en la ficha** cuando no había progreso. Ahora es un solo botón
   cuyas palabras siguen al estado, que es lo que escribe el prototipo, y el glifo es la alternativa y
   sólo se dibuja mientras hay algo de lo que serlo.

8. **Las series.** Ver abajo: es lo más grande de la sesión.

### La puerta de cobertura, con los números de CI delante

La nota anterior decía que quedaban **dos** suelos abiertos. Eran **cuatro**, y el artefacto
`coverage-debt` de CI lo dice desde antes de esta sesión. Con las tres tandas de arriba:

- `ShellViewModel` **llega al listón** y sale de la lista: la rama que faltaba era la del panel de
  subtítulos, que nada podía tomar.
- `HardwareAccelerationFallback` sale también: su `Reset` no lo llamaba nadie —ni `src/` ni ninguna
  prueba— y con él fuera el archivo queda al 100 %. **Ojo:** esa salida es aritmética y no una
  medición de CI (13 de 17 líneas eran el 76 % que medía, y las cuatro que faltaban eran las de
  `Reset`). Si CI la desmiente, la línea vuelve a la lista.
- `CatalogRepository` (98/89), `LibraryRootRepository` (100/90) y `RecommendationsViewModel` (96/90)
  **suben**, medidos.
- `LibVlcMediaPlayerEngine` (91/79) y `CompositionRoot.cs` (90/65) **bajan un punto**, y eso no es
  aflojar: la regla dice que un suelo por encima de lo medido falla igual que uno por debajo, y esos
  dos llevaban un número de una ejecución con más suerte. Las ramas que a CI le faltan en el motor
  son la enumeración de dispositivos de audio de LibVLC y el evento `EncounteredError`: un runner
  hospedado no tiene hardware con el que levantarlas.
- El trinquete baja de **214 a 212**.

Y una nota para la próxima vez que haya que mover un suelo: **el informe que mide CI fusiona más
suites que una ejecución local de una sola.** `MovieDetailsViewModel` dio 82,54 % midiendo sólo
`UiTests` aquí y 83 allí, porque la suite de accesibilidad también recorre ese archivo. Medir en
local sirve para saber la **dirección** y para no gastar una vuelta de CI a ciegas; el número que se
escribe en el archivo es el de CI.


`CompositionRoot.Library.cs` cayó de estar en el listón a 97/50 con los enganches que añadió esta
tanda, y **volvió al listón** en cuanto el paseo presionó sus cinco arcos: sin shell, con una tarjeta
de un título que el catálogo ya no tiene, y con una tarjeta cuyo progreso desapareció. No hace falta
meterlo en la lista.

Lo que sigue abierto es la grieta por la que ese archivo estuvo cayendo sin que nadie se enterara:
**la lista sólo vigila lo que ya está en ella**, así que un archivo que estaba en el listón y se
degrada no lo ve nadie. CI mide cuatro así ahora mismo —`PlayerView.axaml.cs` a 65/41,
`PlayerViewModel.cs` a 98/91, `PlaybackPreference.cs` a 98/92 y `DisabledOutline.cs` a 100/87—, y son
de antes de esta sesión. Meterlos sube el trinquete, que sólo baja; cerrarlos es subirlos al 96/96.

### Las series, que eran el defecto de la casa en su forma más grande

`titles`, `seasons`, `episodes` y `episode_media` existen desde la migración **0004**, la ficha de
serie está dibujada y enrutada desde que se escribió, `MediaNameParser` lee `S01E01` desde el primer
día — y **nada había escrito jamás una fila en ninguna de las cuatro**. LIB-005 figuraba como
`VERIFIED` con una evidencia que mide el analizador, y el analizador funciona: lo que no había era
quien lo llamara.

Dos piezas, ninguna en la vista: `LocalSeriesPolicy` (dominio, pura) dice qué carpeta nombra la serie
—**la carpeta, nunca el archivo**— y `GroupScannedEpisodes` corre después de cada escaneo y escribe la
serie, sus temporadas, sus episodios y el archivo detrás de cada uno.

Medido de punta a punta sobre el árbol real: **99 archivos entran y 3 tarjetas salen** —dos series de
72 y 27 episodios con sus ocho y tres temporadas, y la película que había en la misma raíz—, con un
archivo detrás de cada episodio. La evidencia está en
[docs/evidence/stable/audit-lib005-a-folder-of-episodes-is-a-series.md](evidence/stable/audit-lib005-a-folder-of-episodes-is-a-series.md).

### El catálogo de elementos, escrito

[docs/design/ELEMENTS.es.md](design/ELEMENTS.es.md) pasa el catálogo del prototipo a los tokens de
este árbol, elemento por elemento y estado por estado, con una regla de precedencia: el prototipo
manda sobre el documento y el documento manda sobre el `.axaml`. Los iconos ya eran los del prototipo
—`Theme/Icons.axaml` los convirtió el 2026-08-24— y se ha comprobado que no queda ni un glifo de Segoe
Fluent en ninguna vista.

### Las trampas que costaron tiempo aquí

- **Un suelo que sube en una sola ejecución puede ser un baile, no una mejora.**
  `MarkerEditorViewModel` midió 79, 79, 79, **81**, 79 en cinco ejecuciones seguidas de CI. Lo subí a
  81 en la cuarta y la quinta falló por ello. La puerta dice «now reaches X; raise its floor» en
  cuanto **una** ejecución mide más, y un suelo lo tienen que cumplir **todas**: el artefacto de un
  run es una medición, no una tendencia. Antes de subir un suelo hay que mirar los artefactos de
  varios runs — se descargan con `gh run download <id> -n coverage-debt`.

- **Una escena del paseo que gasta el progreso tiene que reponerlo, no confiar en la máquina.** El
  héroe y la tarjeta del carril se dibujan sólo cuando hay algo que continuar; la muestra dura noventa
  segundos y «Continuar» la abre en el treinta, así que un runner ocupado deja que el resto se
  reproduzca, el rastreador guarda el final y las dos superficies desaparecen. CI lo midió el
  2026-08-25: la misma escena pasó tres ejecuciones y falló la cuarta con «Home came back without the
  hero's Details on it», sin que nada entre ellas tocara el paseo. Se repone el progreso después de
  cada sesión que lo gasta, que es la misma respuesta que ya necesitaban las dos pulsaciones de
  «desde el principio».

- **Una prueba nueva sobre un modelo que lee recursos necesita `[AvaloniaTheory]`**, no `[Theory]`.
  Pasó desapercibido en local por el orden de ejecución y CI lo cazó: «the calling thread cannot
  access this object».
- **Un comentario XML no puede contener dos guiones seguidos**, así que las variables CSS del
  prototipo citadas en un comentario de AXAML rompen la compilación del marcado, no la del código.
- **`Guid` guarda sus tres primeros campos en little-endian**, así que el byte que un UUID canónico
  llama séptimo —el de la versión— es el índice **7** del array, no el 6.
- **Hacer pulsables las dos carátulas destapó una ambigüedad real**: un mismo título puede estar a la
  vez en «Añadido recientemente» y en sugerencias, y el paseo se negó a pulsar un nombre que casaba
  con dos controles. Se resolvió como ya hacía el carril: el nombre accesible dice el carril y luego
  el título.
- **«Desde el principio» borra el progreso al cerrarse**, así que el héroe y la tarjeta del carril
  desaparecen. No hay orden en que las dos pulsaciones sobrevivan: el paseo repone el progreso entre
  ellas.

### Lo que queda

- **El menú de velocidad** sigue siendo un `MenuFlyout` de once filas numéricas.
- **Los glifos del transporte, uno a uno contra el prototipo.** Sin empezar.
- **El mini como ventana PiP de verdad.** Media hecha.
- **El póster de fondo en el cabecero de la ficha** (decisión 6).
- **El editor de metadatos como vista propia.**
- **«Secciones cortadas por el ancho»**, todavía sin localizar.
- **El título de una película sin identificar es su nombre de archivo tal cual** —«El Faro de Piedra
  2019»—. Está **afirmado** en la prueba de series en vez de corregido: ahora que el analizador se usa
  para agrupar, limpiarlo también para las películas es media hora, pero es una decisión de alguien.

## Estado al cierre del 2026-08-25 (madrugada) — nueve de los veinticuatro, y CI casi verde

Nueve commits sobre la rama. **Todo verde en local**: Domain 499, Application 241, Architecture 30,
Documentation 87, Ui 904, Accessibility 146, Integration 467, Media 149.

**Lo que hay que mirar primero:** CI del HEAD. La puerta de cobertura pasó de **siete** suelos caídos
a **dos** (`ShellViewModel` y `CompositionRoot.cs`, una rama cada uno tras esta tanda), y siete
suelos se subieron copiándolos del artefacto de CI. Si siguen rojos, están medidos abajo: la rama que
falta en el compositor es el lado nulo de `ShellHost.Shell` en el `ModeHandler` de la sesión, y la del
shell es una de las dieciséis de `HasPlayerPanels`.

### Lo cerrado, con su medición

Además de lo de la primera tanda (subtítulos, Home, proporción del vídeo, los dos botones de modo,
`:focus-visible`, tooltips, alineación de desplegables, contorno punteado):

1. **La valoración es de cinco estrellas** con migración **0020**, que divide entre dos y redondea
   hacia arriba. Lo que dice que una estrella está dada es su relleno. La regla vive también en el
   dominio (`PersonalStatePolicy.ToFiveStars`) porque una migración corre una vez contra un archivo y
   una copia restaurada es un número que llega después.
2. **El minuto del botón «Continuar» va dentro del botón**, no al lado.
3. **«Desde el principio» es el arco de reinicio con la flecha al otro lado**, y su botón mide 36 y
   es redondo, como los dos que tiene al lado.
4. **Todos los iconos bajan dos píxeles** con el trazo escalado (ancho ÷ 15).
5. **Ningún botón es cuadrado.** Ocho clases lo eran. El token de píldora valía 18 —la mitad de un
   control de 36— y ahora vale **999**, que el dibujo recorta a la mitad del lado corto: un objetivo
   cuadrado sale círculo y uno ancho sale píldora con un solo número. `ButtonShapeTests` lee los
   estilos del archivo de tokens **y mira el píxel de la esquina**, porque 999 satisfaría cualquier
   comparación numérica mientras el dibujo decide otra cosa.
6. **El aviso de aceleración por hardware ya no sale en cada reproducción.** El motor no pide una
   superficie de la tarjeta gráfica porque no puede componer subtítulos sobre ella; ni pedida ni
   activa es lo que pasa de verdad.

### Lo que queda de los veinticuatro

- **El menú de velocidad** sigue siendo un `MenuFlyout` de once filas numéricas. El prototipo lo
  dibuja con marca, nombre y nota. Cambia la identidad de once controles del inventario del paseo,
  así que sus pulsados viajan en el mismo commit.
- **Los glifos del transporte, uno a uno contra el prototipo.** Sin empezar.
- **El mini como ventana PiP de verdad**: sin marco, siempre encima, arrastrable, conservando la
  relación de aspecto y recordando dónde se dejó. Sólo está hecha la mitad — ya no duplica la barra.
- **«Reproducir» cuando no hay progreso.** Hoy una película sin progreso no enseña ningún botón de
  reproducir, sólo el glifo de empezar de nuevo. Pide un segundo botón o un nombre que se mueva con
  el estado, y las dos cosas cambian el inventario del paseo.
- **El póster de fondo en el cabecero de la ficha** (decisión 6). Ojo: `PosterArtView` dibuja **arte
  generado** a partir del tono del título; el póster de verdad viaja en `PosterPath` de los metadatos,
  y en una biblioteca sin identificar no hay ninguno.
- **El editor de metadatos como vista propia.**
- **«Secciones cortadas por el ancho»**, todavía sin localizar.

### Las trampas que costaron tiempo aquí

- **La puerta de cobertura mide con el informe fusionado** de
  `artifacts/test-results/verify-win-x64/coverage-gate/Cobertura.xml`, no con los informes sueltos.
  Un script que tome el máximo por línea entre los sueltos da otros números y **engaña**.
- **Los suelos se copian del artefacto `coverage-debt` de CI y sólo suben.** Varios archivos miden
  distinto aquí y allí, así que la lista no se cierra sin una vuelta de CI.
- **Un archivo nuevo contra `main` tiene que llegar a 96/96**: no puede entrar en la lista de deuda,
  porque el trinquete baja con cada archivo que sale y no libera hueco. Cuando un archivo de
  infraestructura no llega, la salida es **sacar la regla a una función pura** —`LibVlcTrackIdentity`
  lo hizo— porque una rama en el adaptador sólo la toma una máquina con decodificador.
- **Cambiar la escala de la valoración tocó 63 pruebas de integración por una sola causa**: un
  fixture siembra un número. Antes de dar por grande un cambio, mírale la causa al primer fallo.

### Un hallazgo que sigue sin decidir

El apartado del acento respecto del anillo de foco **mueve un solo paso**: para un anillo `#005A9C`
devuelve `#00599A`, otro color por el byte y el mismo a la vista. Está escrito en la prueba en vez de
afirmado a la baja. Si importa, la decisión es cuánto tiene que separarse.

## Estado al cierre del 2026-08-25 (noche, segunda sesión) — cinco de los veinticuatro, medidos

Cinco commits sobre la rama. **Todo verde en local**: Domain 498, Application 241, Architecture 30,
Documentation 87, Ui 897, Accessibility 146, Integration 467, Media 143.

**CI seguía en rojo al empezar, y no por lo que decía el encargo.** El run de `897dfda` no falló por
una puerta ya corregida: falló por la **puerta de cobertura**, con siete archivos por debajo de su
suelo y uno mejorado sin subirlo. Eso es lo que llevaba bloqueando el fast-forward a `main`, y buena
parte de esta sesión se ha ido en devolverlos.

### Lo que se cerró, con su medición

1. **Los subtítulos no llegaban a la pantalla, por tres causas y ninguna visible leyendo el código.**
   - La sesión los apagaba al abrir: sin preferencia guardada el valor resuelto es «apagados» y se
     aplicaba igual, mandándole `-1` al motor sobre la pista que el contenedor marca por omisión.
   - **El croma decidía si VLC componía el subtítulo.** Con `RV32`, `RGBA`, `ARGB`, `RV24`, `YUY2`,
     `VYUY` y `YVYU` no cambiaba **ni un byte** del fotograma al encenderlo; con `UYVY` cambiaban
     61 687. El motor pide `UYVY` y convierte a BGRA él mismo (`PackedYuvConverter`).
   - **Con D3D11VA la composición falla y VLC lo dice una vez por fotograma**: «no matching alpha
     blending routine (chroma: YUVA -> DX11)». 67 001 bytes cambian por software y ninguno por
     hardware. El motor decodifica por software y lo declara.
   - Verificado de punta a punta contra el episodio del propietario: el fotograma publicado lleva el
     subtítulo dentro, en las bandas 15 y 16 de 16.
2. **Home salía vacía con la biblioteca llena.** Medido contra su base de datos: 102 filas en
   `scanned_titles`, **cero** en `titles` y cuatro en `watch_state` que sólo casan con archivos
   escaneados. Las tres proyecciones leen la misma unión que lista Biblioteca. Y la ruta con la que
   abre la aplicación se anuncia como cualquier otra, porque Home sólo se leía al *llegar* a ella.
3. **El vídeo se deformaba al redimensionar.** `VideoFitPolicy` conserva la proporción y reparte las
   bandas; lo que se afirma es la **proporción**, no el tamaño.
4. **El reproductor**: pantalla completa y ventana flotante en la barra de controles (y **ya no en
   los dos sitios**, que el paseo rechaza), doble clic, las teclas oídas de bajada —que es por qué el
   espacio ponía pantalla completa—, el icono de PiP correcto y la barra que deja de duplicarse en la
   ventana pequeña.
5. **Transversal**: los diez selectores a `:focus-visible`, tooltip en todos los botones desde un
   estilo, la alineación vertical de los desplegables (2,43 px, el mismo número que los botones, y la
   prueba falla sin la corrección), y el contorno punteado gastado sólo en los dos contrastes altos —
   eran **299** rectángulos de puntos en todo el árbol sin datos cargados.

### Lo que queda de los veinticuatro

- **El menú de velocidad** sigue siendo un `MenuFlyout` de once filas numéricas. El prototipo lo
  dibuja con marca, nombre y nota. Es la pieza que más cuesta: cambia la identidad de once controles
  del inventario del paseo, así que hay que llevar sus pulsados en el mismo commit.
- **Los glifos del transporte, uno a uno contra el prototipo.** Sin empezar.
- **El mini como ventana PiP de verdad** (sin marco, siempre encima, arrastrable, con relación de
  aspecto y posición recordada). Sólo está hecha la mitad: ya no duplica la barra.
- **«Reproducir» cuando no hay progreso.** Hoy una película sin progreso no enseña ningún botón de
  reproducir, sólo el glifo de empezar de nuevo. Decir la palabra que toca pide un segundo botón o un
  nombre que se mueva con el estado, y las dos cosas cambian lo que el inventario del paseo guarda.
- **El póster de fondo en el cabecero de la ficha** (decisión 6: el póster, no un fotograma).
- **El editor de metadatos como vista propia.**
- **La valoración a cinco estrellas** con su migración numerada dividiendo entre dos.
- **«Secciones cortadas por el ancho»**, todavía sin localizar.

### Lo que hay que saber antes de tocar la cobertura

- **La puerta mide con el informe fusionado** de `artifacts/test-results/verify-win-x64/coverage-gate/`,
  no con los informes sueltos. Un script que tome el máximo por línea entre los informes sueltos da
  otros números y **engaña**.
- **Los suelos se copian del artefacto `coverage-debt` de CI y sólo se mueven hacia arriba.** Varios
  archivos miden distinto aquí y allí —`LibVlcMediaPlayerEngine` dio 91/81 en local y 91/78 en CI—,
  así que la lista no se puede cerrar sin una vuelta de CI.
- **Un archivo nuevo contra `main` tiene que llegar a 96/96**; no puede entrar en la lista de deuda,
  porque el trinquete baja con cada archivo que sale de ella y no libera hueco.
- Tres archivos no se podían medir y ahora sí: el servicio de apariencia recibe **qué ventana está en
  pantalla** en vez de buscarla (el ciclo de vida de una aplicación no se puede sustituir una vez
  arrancada), y un color que ya lee vuelve **byte a byte** para que el apartado del anillo de foco
  pueda ocurrir siquiera.

### Un hallazgo que no se ha decidido

El apartado del acento respecto del anillo de foco **mueve un solo paso**: para un anillo `#005A9C`
devuelve `#00599A`, que es otro color por el byte y el mismo a la vista. Está escrito en la prueba en
vez de afirmado a la baja. Si eso importa, la decisión es cuánto tiene que separarse.

## Estado al cierre del 2026-08-25 (noche) — el propietario probó la aplicación con su biblioteca

Esta tanda tiene cuatro commits y **todo está verde en local**: Domain 480, Application 236,
Architecture 30, Documentation 87, Ui 856, Accessibility 146, Integration 466, paseo con 198
pulsados y 20 declarados.

**Lo importante de esta sesión no es lo que se hizo, es lo que el propietario encontró probándola.**
Arrancó la aplicación contra su propia biblioteca —`E:\Series`, con Juego de Tronos y La casa del
dragón— y trajo treinta y cuatro cosas. Diez están hechas; veinticuatro no, y están abajo con sus
palabras.

### Lo que se cerró

- **El reproductor se encabeza con las píldoras del prototipo** —Audio, Subtítulos, Vídeo,
  Marcadores y Otras versiones— y la columna empieza cerrada, con cabecera propia y su «×». Los
  paneles agrupan por asunto y no por modelo. «Vídeo» es nuevo: decodificación, HDR y el alcance.
  La píldora «Sesión 1 · motor único activo» y el dispositivo de salida a la derecha del pie.
- **Al reproducir se va todo menos la imagen** y vuelve al mover el ratón o pulsar una tecla. Sin
  temporizador, con el coste escrito.
- **Ajustes → Apariencia con las once filas del prototipo**, con el acento derivado por
  `AccentPalette` para que cualquier color elegido siga cumpliendo sus cinco razones de contraste.
- **El acento llega ahora a toda la aplicación.** Escribía cuatro pinceles y los controles de Fluent
  leen los suyos, redirigidos con `<StaticResource>` —estático, resuelto una vez—. Se escriben las
  veinte redirecciones y `AccentTokenTests` exige que no falte ninguna.
- **El texto de los botones estaba 2,43 px bajo**, medido con las métricas de la fuente y corregido
  con cinco píxeles derivados, no ajustados a ojo.
- **Los dos colores de subtítulos** son seis muestras y un selector, como el acento.

## Lo que el propietario encontró y NO está hecho

Son sus palabras, agrupadas. **Nada de esto está medido todavía salvo donde se dice.**

### El reproductor, que es lo que más miró

1. **El doble clic no pone pantalla completa.**
2. **El atajo `F` no funciona**, y **la barra espaciadora sí pone pantalla completa** — que es
   justamente lo que no debe hacer: espacio es reproducir/pausar.
3. **No hay botón de pantalla completa en la barra de controles.**
4. **El icono del mini reproductor no es el correcto**, y **al usarlo el mini no se coloca en el
   sitio correcto**.
5. **Se duplica la barra de reproducción** en el mini.
6. **El mini debe funcionar como cualquier ventana PiP.**
7. **El icono de PiP debe ir en la barra de controles, junto al de pantalla completa.**
8. **El desplegable de velocidad debe ir a la derecha de la barra**, y **su diseño abierto no es
   igual al del prototipo** — el prototipo lo dibuja como un menú de nueve filas con marca, nombre y
   nota («Normal», «más lenta», «más rápida»), no como el `MenuFlyout` que hay.
9. **El vídeo se deforma al redimensionar**, tanto en PiP como en el reproductor principal.
10. **Los subtítulos no cargan.** Comprobado por él en VLC con
    `E:\Series\Juego de tronos\Temporada 1\Juego de tronos - 1x01 - Se acerca el invierno.mkv`.
11. **Los glifos del transporte no se han comparado uno a uno** con el prototipo (viene del encargo
    original y sigue pendiente).

### La ficha de película y de serie

12. **El botón de reproducir no es igual al del prototipo**: debe decir «Reproducir» o «Continuar»
    según el estado.
13. **«Reproducir desde el principio» debe ser un icono**, no un botón con palabras.
14. **El cabecero debe mostrar el póster de fondo, o incluso un fotograma del propio vídeo.**
15. **Editar metadatos se muestra dentro de la misma vista**, y en el prototipo es **una vista
    independiente con su propio diseño**.

### Transversal, en todas las pantallas

16. **Los contornos punteados salen donde no van**: Privacidad y diagnósticos, Copias,
    Actualizaciones, Duplicados, Revisión, Biblioteca y las dos fichas. También «algún elipse».
17. **Al hacer clic en una casilla sale un reborde azul**, y pasa en todas. **Está identificado sin
    corregir**: los diez selectores de foco de `DesignTokens.axaml` usan `:focus`, que se activa con
    el ratón; lo que hace falta es `:focus-visible`, que sólo responde al teclado.
18. **Los desplegables tienen el mismo problema de alineación vertical** que tenían los botones. La
    corrección de los botones ya está y es la misma receta: el relleno inferior compensa la
    asimetría de la fuente, y el número sale de las métricas.
19. **Todos los botones deben tener tooltip**, en especial los que sólo llevan icono.
20. **La valoración debe ser de 1 a 5 con estrellas** llenas o vacías según el estado, con «quitar
    valoración» al lado. Las típicas de Google. Hoy son diez botones numerados.

### Home y la biblioteca

21. **Home queda totalmente vacío** aunque haya series cargadas. **A medio medir**: `Home.LoadAsync`
    sólo corre en `OnNavigated`, y `ReadRecentlyAddedAsync` lee la tabla `titles` — si el escaneo
    deja los archivos en la bandeja de revisión sin promoverlos a títulos, Biblioteca los enseña y
    Home no. Falta comprobarlo contra su base de datos.
22. **«Secciones cortadas por el ancho»**, del encargo original y todavía sin localizar. Lo más
    parecido que apareció: la página de Ajustes mide 1.797 px de contenido.

## Lo que se aprendió y hay que recordar

- **El paseo autónomo no puede pulsar lo que hay bajo el primer viewport de una página con scroll.**
  El hit test de Avalonia en headless no sigue el desplazamiento de un `ScrollViewer`: reproducido en
  ocho líneas —la misma vista dentro de un scroller con offset 400, un botón que dice medir 123×36 en
  y=419, y un clic ahí que llega al borde del scroller— mientras que sin desplazar responde hasta el
  final de 1.700 px. Se probaron tres salidas y las tres fallan igual: barrer el offset, cambiar el
  contenido de la ventana y abrir una segunda ventana. **El trinquete del paseo subió de 0 a 20** con
  esa medición escrita en `eng/check-walk-coverage.ps1`, y sólo vuelve a bajar cuando el arnés sepa
  seguir un scroll, o cuando se decida pulsar con un evento de puntero dirigido al control en vez de
  con una coordenada de ventana — lo que conserva «se pulsó» y renuncia a «era alcanzable».
- **Un `<StaticResource>` se resuelve una vez.** Todo lo que redirige a un token que la aplicación
  escribe en caliente hay que escribirlo también. Vale para el acento y valdría para cualquier otro.
- **Centrar la caja de un texto no es centrar el texto.** La tinta va del alto de la mayúscula al pie
  del descendente y la fuente no es simétrica: 2,43 px en un botón de 44.

## Decisiones ya tomadas — no se vuelven a preguntar

Tomadas al cierre del 2026-08-25 para que la sesión siguiente construya en vez de consultar.

1. **Teclas del reproductor.** `Espacio` reproduce y pausa. `F` y el **doble clic** ponen y quitan
   pantalla completa. `N` el mini. `Esc` cierra. Que hoy el espacio ponga pantalla completa es un
   defecto, no una alternativa.
2. **El mini reproductor es una ventana PiP de verdad**: sin marco, siempre encima, arrastrable,
   redimensionable **conservando la relación de aspecto**, y con un transporte mínimo propio — no la
   barra entera del reproductor, que es lo que hoy se duplica. Se coloca en la esquina inferior
   derecha del área de trabajo, con un margen, y recuerda dónde la dejaron.
3. **La deformación del vídeo se corrige conservando la relación de aspecto** con letterbox, en el
   reproductor y en el PiP. El vídeo nunca se estira.
4. **El editor de metadatos pasa a ser una superficie propia**, como en el prototipo, y deja de vivir
   dentro de la ficha. Se llega desde la ficha y se vuelve con un enlace, igual que las dos fichas.
5. **La valoración pasa a cinco estrellas.** El dato guardado hoy va de 1 a 10; se migra dividiendo
   entre dos y redondeando hacia arriba, en una migración con su número. «Quitar valoración» queda a
   la derecha de la quinta estrella.
6. **El cabecero de la ficha usa el póster como fondo**, con el degradado que ya usa el prototipo —
   no un fotograma del vídeo. Extraer un fotograma obliga a decodificar desde el catálogo, que es
   superficie de ataque nueva y coste por título, y el póster ya existe. Si el título no tiene
   póster, se queda el arte generado que ya se dibuja.
7. **El anillo de foco sólo responde al teclado**: los diez selectores pasan de `:focus` a
   `:focus-visible`. El contorno punteado del deshabilitado se queda como está — es la única señal en
   los dos contrastes altos — pero hay que comprobar control por control que quien lo lleva está
   deshabilitado de verdad.
8. **Los tooltips se ponen en todos los botones**, y en los de sólo icono el tooltip repite el nombre
   accesible. Una cadena, dos lugares, y `ToolTip.Tip` nunca lleva un literal con letras:
   `ViewLiteralTests` lo refusa y tiene razón.

## Cómo se trabaja aquí

1. Las suites afectadas en local, commit, push a la rama, **CI verde**, y sólo entonces el
   fast-forward a `main`.
2. **La suite de accesibilidad entera después de tocar cualquier vista.**
3. `eng/coverage-debt.txt` se copia del artefacto `coverage-debt` de un run de CI, nunca se genera
   aquí. El trinquete está en **214** y sólo baja.
4. Un control nuevo llega con su escena de paseo en el mismo commit, salvo que el arnés no pueda
   alcanzarlo — y entonces se declara en `eng/walk-pending.txt` con la medición, no en silencio.
5. Para ver la aplicación: compilar en **Debug** y ejecutar ese binario. Mientras el propietario la
   tiene abierta, Release queda libre para las pruebas.

## Estado al cierre del 2026-08-25 (tarde) — lo que el propietario miró, y lo que falta

**Todo lo de esta tanda está verde en local** (Domain 472, Application 236, Architecture 30,
Documentation 87, Ui 836, Integration 466, Accessibility 146, paseo con **0 pendientes**) y la rama
lleva 33 commits por delante de `main`.

### Lo primero que se arregló no estaba en el código

Tres veces se dijo que una captura «salía oscura», y las tres veces el archivo estaba bien: **un PNG
de 1500 × 1000 se lee oscuro, y la misma imagen a 750 × 500 se lee como es**. Medido sobre la
biblioteca en tema claro —`#FBFCFE` en el lienzo, `#E9EEF4` en el raíl, 100 % opaco— y confirmado
reduciéndola. El color se decide midiendo o mirando la mitad; nunca a tamaño completo. Una alarma que
levantó —«las portadas están más claras abajo»— era un punto de medición mal elegido.

**Y `docs/assets/review.png`, en un repositorio público, imprimía la ruta del perfil de quien tomó la
captura.** Las cinco están rehechas con la biblioteca en una carpeta neutra, sin canal alfa.

### Lo que se cerró en la aplicación

- **Duplicados**: la copia elegida se marca en toda su fila, el título del grupo deja de ir en azul,
  y la columna de tamaño baja hasta los bytes.
- **Enlaces**: del acento a su tinta —9,03:1 frente a 5,62:1 en claro—, con el par medido.
- **Ficha de serie**: cada episodio con el tono de la serie caminado 7° por episodio; el panel de
  «Siguiente episodio» limita su columna y no su borde.
- **Bandeja**: dice de qué título habla (migración 0019, una columna) y sus cuatro rótulos van en el
  mismo estilo.
- **Reproductor**: un fallo ya no se borra solo cuando LibVLC informa del *stop* posterior.
- **Inicio/biblioteca**: ficha de tipo sólo con glifo en los carruseles, «+» en Añadir medios,
  segunda línea en la cabecera del reproductor, «VELOCIDAD» en vez del rótulo largo.
- **Otras acciones**: cinco filas iguales en vez de dos píldoras sobre tres filas.
- **Dos herramientas del título** aparecen sólo cuando tienen algo que hacer, y el tráiler externo
  dice «Ver tráiler» con su flecha.
- **El contorno punteado** de un control deshabilitado toma el radio del control.
- **`KindShapeConverter` retirado**: dos ramas que nada podía tomar, sustituidas por un estilo. El
  trinquete de cobertura baja de 215 a **214**.

## Lo que el propietario pidió y NO está hecho

**1. El reproductor tiene que ser copia exacta del prototipo, en diseño y en funcionalidad.**

Las referencias ya están tomadas, una por estado, en
`%TEMP%\claude\…\scratchpad\proto-player\`: `proto-player.png`, `proto-panel-audio.png`,
`proto-panel-subs.png`, `proto-panel-video.png`, `proto-panel-marks.png`, `proto-mini.png`,
`proto-fullscreen.png`. **Se miran a la mitad** (`half.ps1`).

Cómo se tomaron, que es lo que permite explorar cualquier estado del prototipo: la copia de trabajo
`scratchpad/proto/proto.html` acepta **`?press=A|B|C`** y pulsa esos nombres en orden, por
`aria-label` o por el texto del botón; `scratchpad/shoot-player.ps1` lo automatiza con Chrome sin
cabeza y `--force-prefers-reduced-motion`.

Lo que falta, medido contra esas capturas:

- Las cuatro píldoras —**Audio, Subtítulos, Vídeo, Marcadores**— van en la **cabecera** del
  reproductor y abren o cierran la columna. Aquí viven dentro de la columna, que está siempre puesta.
- La columna del prototipo tiene **cabecera propia con su «×»**.
- Falta la píldora **«Sesión 1 · motor único activo»** junto al título.
- Falta **«Altavoces del sistema · 2.0»** a la derecha del pie.
- Los paneles agrupan distinto: **Audio** = pistas de audio + dispositivo de salida + canales;
  **Subtítulos** = pistas + «Cargar subtítulo externo…» + su nota; **Vídeo** = decodificación + HDR +
  nota; **Marcadores** = detectados automáticamente + los de este título.
- **Los glifos del transporte no son idénticos**; hay que compararlos uno a uno.
- **Decidido por el propietario**: el botón de **detener** y el panel **«Otras versiones»** se quedan
  aunque el prototipo no los tenga, y se anotan como añadidos deliberados.

**2. Al reproducir se oculta todo menos el vídeo.** Decidido: vuelve **al mover el ratón o al pulsar
una tecla**. El prototipo no lo hace —se comprobó su código—, así que es un requisito propio.

**3. Ajustes → Apariencia con las mismas opciones que el prototipo**, y en general **las mismas
opciones y campos que el prototipo en todas las pantallas**. El prototipo tiene once filas donde esta
aplicación tiene dos: seguir el tema de Windows, color de acento (seis muestras + selector + el
hexadecimal), fondo Mica sutil, tinte de acento en los fondos, densidad, tamaño de las portadas,
redondeo de esquinas, mostrar títulos bajo las portadas, animaciones de la interfaz e idioma. Tres de
ellas tocan puertas que hay que declarar de nuevo: el acento personalizable contra
`ContrastTokenTests`, el redondeo contra `ScalarTokenTests`, y densidad y tamaño de portada contra
`ViewOverflowTests`.

**4. Los dos campos de color de los subtítulos son cajas de texto hexadecimal** y deben ser un
selector. El patrón del prototipo está a la vista en su fila de acento: seis muestras circulares de
28 px, un separador y el valor en monoespaciada.

**5. «Secciones cortadas por el ancho del diseño»** — el propietario lo vio «por ejemplo en la
vista». Falta localizarlo con una medición; `ViewOverflowTests` mide a 900 px sin contexto de datos y
no lo ha cazado, así que probablemente es una superficie con datos reales.

## Cómo se trabaja aquí

1. Las suites afectadas en local, commit, push a la rama, **CI verde**, y sólo entonces el
   fast-forward a `main`.
2. **La suite de accesibilidad entera después de tocar cualquier vista**: `TextScalingTests` cazó en
   CI un ancho fijo que aquí no se había ejecutado.
3. `eng/coverage-debt.txt` se copia del artefacto `coverage-debt` de un run de CI, nunca se genera
   aquí. El trinquete está en **214** y sólo baja.
4. Un control nuevo llega con su escena de paseo en el mismo commit; la puerta está en 0 y no sube.

## Estado al cierre del 2026-08-25 (tarde) — el prototipo, mirado a la resolución correcta

**Lo primero que se arregló no estaba en el código.** Tres veces se dijo que una captura «salía
oscura», y las tres veces el archivo estaba bien: un PNG de 1500 × 1000 **se lee** oscuro, y la misma
imagen a 750 × 500 se lee como es. Medido sobre la biblioteca en tema claro —`#FBFCFE` en el lienzo,
`#E9EEF4` en el raíl, 100 % opaco— y confirmado reduciéndola. Desde ahora el color se decide midiendo
o mirando la mitad; nunca a tamaño completo. Está en
[la evidencia](evidence/stable/audit-prototype-fidelity-round-three.md), y una alarma que levantó
—«las portadas están más claras abajo»— resultó ser un punto de medición mal elegido.

**Y lo segundo tampoco:** `docs/assets/review.png`, en un repositorio público, imprimía la ruta del
perfil de quien tomó la captura. La bandeja escribe la carpeta bajo cada archivo. Las cinco están
rehechas con la biblioteca en una carpeta neutra, sin canal alfa, y contra la aplicación de hoy.

Lo que cambió en la aplicación, por superficie:

- **Duplicados.** La copia elegida se marca en toda su fila —radio, borde de acento y lavado—, el
  título del grupo deja de ir en azul, y la columna de tamaño baja hasta los bytes: redondeaba a
  «0 MB» justo donde alguien decide qué copia conserva.
- **Los enlaces** pasan del acento a su tinta, que es lo que usa el prototipo: de 5,62:1 a 9,03:1 en
  claro y de 8,29:1 a 11,36:1 en oscuro, con el par nuevo medido en `ContrastTokenTests`.
- **Ficha de serie.** Cada miniatura de episodio se coloreaba con el hash de su propio nombre;
  ahora es el tono de la serie caminado 7° por episodio, que es `art(serie + episodio × 7)`. Y el
  panel de «Siguiente episodio» limita su columna en vez de su borde: con ancho fijo, al 200 % de
  escala de texto, «Continuar» caía fuera de la ventana.
- **Bandeja de revisión.** Dice **de qué título habla**. El proveedor ya devolvía el nombre y el año
  y la cadena entera los tiraba; ahora los lleva hasta la tarjeta (migración 0019, una columna).
- **Reproductor.** Un fallo ya no se borra solo: LibVLC informaba del *stop* del medio que acababa de
  desmontar y ese estado sustituía al fallo, así que la recuperación desaparecía de la pantalla
  mientras alguien la leía. Apareció como intermitencia antes que como defecto.
- **Inicio y biblioteca.** La ficha de tipo pierde su palabra en los carruseles y la conserva en la
  cuadrícula, «Añadir medios» recupera su signo más, y la cabecera del reproductor recupera su
  segunda línea para una película.

**Lo que sigue distinto, y por qué:** además de las seis de la vuelta anterior, la insignia de «no
disponible» es ámbar en los siete sitios donde se monta —el prototipo tiene dos formas y la puerta
que impide la segunda es anterior—, el transporte lleva un botón de detener que el prototipo no
tiene, el editor de metadatos vive dentro de la ficha en vez de en una página propia, y Ajustes no
ofrece las nueve preferencias de apariencia del prototipo: la paleta es canónica y sus pares están
medidos.

**Lo que hay que mirar primero en la próxima sesión:**

1. **`eng/coverage-debt.txt` se refresca desde el artefacto del último run verde.** El trinquete
   sigue en 215 y sólo baja.
2. **La suite de accesibilidad entera después de tocar cualquier vista.** `TextScalingTests` cazó en
   CI el ancho fijo del panel de siguiente episodio, y aquí no se había ejecutado tras el cambio.

## Estado al cierre del 2026-08-25 — la aplicación se parece al prototipo, vista a vista

**La comparación se hizo con las dieciséis capturas verificadas, no de memoria**
([evidencia](evidence/stable/audit-prototype-fidelity-round-three.md)). Diecisiete diferencias
cerradas y **cinco que se quedan, cada una con su medición escrita**.

Lo que cambió, por superficie:

- **Ficha de serie.** Línea de datos, barra de la serie con «10/16 vistos», panel de siguiente
  episodio con su botón —la única acción acentuada de la ficha—, temporadas en píldoras en vez de
  desplegable, y cada episodio como tarjeta con miniatura, nombre y «48 min · Visto». El nombre y la
  duración **no estaban en pantalla**: la proyección de episodios no los leía.
- **Las dos fichas.** Se desplazan como una página, la vuelta es un enlace, las marcas personales
  salen del banner a «Otras acciones», y las tres herramientas del título entran en la fila de
  acciones del banner (`TitleActionsView`, una vista montada por las dos).
- **Bandeja de revisión.** Cada tarjeta dice **de qué archivo habla** —la proyección de candidatos
  trae la ruta desde ahora—, con carátula, tipo, confianza y señales, y lleva **sus tres decisiones
  dentro**. Deja de ser una lista con selección: sus filas eran controles de mando y el paseo se
  quedaba sin sitio donde pulsar «al lado».
- **Duplicados.** La tabla de ocho columnas del prototipo, con el radio que fija qué copia se
  reproduce, leída en una sola consulta.
- **Reproductor.** La cabecera dice qué se reproduce —el título viaja con la petición, desde la
  tarjeta que pulsó—, el transporte vuelve a ser una fila con el orden del prototipo, y los atajos
  están escritos bajo ella.
- **Paleta.** `AccentInkBrush` en los cuatro modos, y la última parada de los degradados corregida:
  estaba escrita `#30` pensando en «30 %», que es el 19 %.

**Lo que se quedó distinto, y por qué** (las cinco están en la evidencia): las iniciales de la
portada, el punto de radio de los filtros —en los dos contrastes altos el relleno no distingue nada y
el glifo es toda la señal—, la columna de paneles del reproductor siempre abierta —cerrarla dejaría
fuera de alcance controles que el paseo pulsa, y esa cobertura está en cero pendientes—, el cuarto
botón de la bandeja y el editor por episodio, que esta aplicación no tiene porque sus metadatos van
por título.

**Lo que hay que mirar primero en la próxima sesión:**

1. **`eng/coverage-debt.txt` está por refrescar desde un artefacto de CI.** Varios archivos mejoraron
   (`CatalogQueries` llegó a la barra, `GetHome`, `HomeReadModel` y `EpisodeSequenceRepository`
   subieron) y **cuatro vistas nuevas entran en la lista con el 100/50 que mide todo archivo de
   vista**. El trinquete está en 215 y sólo baja: dos archivos ya salieron de la lista en esta tanda
   —`LifecycleSettingsViewModel` y `RootRemapRowViewModel`, ambos a 100/100— y hay que ver, con el
   artefacto delante, cuántos más hay que pagar.
2. **CI verifica lo que esta máquina no.** Dos carreras las encontró allí y aquí no: el raspador se
   pulsaba con la sesión en marcha —y desde que el transporte observa la posición del motor, eso
   mueve la propia sonda del paseo— y la ficha del héroe se buscaba sin una pasada de medida.
3. **Las capturas del README están rehechas** contra la aplicación de hoy, en inglés y a 1600 × 1000.

## Estado al cierre del 2026-08-24 (madrugada del 25) — el propietario miró y tenía razón

**Tres diferencias con el prototipo, las tres medidas y corregidas**
([evidencia](evidence/stable/audit-prototype-fidelity-round-two.md)):

1. **El texto de TODOS los botones estaba pegado arriba.** `VerticalContentAlignment` empieza en
   `Stretch` y este árbol nunca lo tocó: la etiqueta ocupaba 42 px de un botón de 44 y su línea se
   dibujaba arriba del todo. Puerta: `ButtonInkTests`, que mide los dos huecos **y** exige que cada
   uno sea real — una etiqueta estirada está centrada trivialmente y habría pasado.
2. **Faltaba la cuarta capa de las portadas**, la trama diagonal. `SpreadMethod="Repeat"` sobre un
   vector de diez píxeles a 115° la dibuja exactamente.
3. **Los iconos eran de otro alfabeto**: veintisiete glifos de Segoe Fluent contra los treinta y
   cinco dibujos de línea del prototipo. Portados a geometrías; **se desvía de una línea de la
   Propuesta**, que prescribe la fuente del sistema, y la regla que esa línea protege —no descargar
   nada— queda entera.

**Lo que sigue sin parecerse, ya medido y SIN tocar** (la tabla está en la evidencia): el distintivo
de tipo en la portada, la línea de datos (año · duración · género), la línea de estado, la marca de
visto, el «no disponible» dentro de la portada, los filtros con punto de radio y la insignia numérica
del riel. **Ocho diferencias, ninguna arreglada en esta tanda.** Ése es el siguiente trabajo de
fidelidad, y conviene hacerlo leyendo `proto-*.png` al lado de la captura de la aplicación en vez de
por memoria.

## Estado al cierre del 2026-08-24 (noche) — PASO 11 HECHO: la página del repositorio tiene sus capturas

**Los dos README abren con Inicio y llevan las cinco capturas decididas** —Inicio, Biblioteca, la
ficha de una serie, el reproductor con su columna y la bandeja de revisión—, en inglés, tema oscuro,
1600 × 1000, versionadas en `docs/assets/`. Con plataforma, licencia, enlace de descarga y la
atribución de TMDB, que ya estaba. **El paso 11 queda cerrado**, y con él se desbloquea el 12 —la
landing—, que esperaba precisamente a estas cinco imágenes.

**Y las capturas hicieron su trabajo: tres defectos, los tres con su rojo, su corrección y su
puerta** ([evidencia](evidence/stable/audit-readme-captures.md)):

1. **El reproductor no tenía ni barra ni reloj mientras reproducía.** `TransportControlsViewModel`
   sólo cambiaba de estado con sus propias órdenes, así que `HasDuration` seguía en falso toda la
   sesión; pulsar un salto los hacía aparecer de golpe. El cabezal ya llegaba a
   `CompositionRoot.OnPositionChanged`, que alimenta al rastreador y a la oferta de salto —las dos
   con su comentario de haber estado «alcanzables y sin alimentar»—; el transporte era el tercero.
   **Decimoquinta forma del defecto de la casa, y la que más se mira.**
2. **El selector de temporada escribía `…Show.SeasonViewModel` en pantalla.** La píldora
   `filter-pill` enlazaba `SelectionBoxItem` y no `ItemTemplate`. **Estaba en la matriz de paridad de
   ayer y nadie lo vio**: una captura sólo sirve si alguien la lee entera.
3. **La biblioteca sembrada listaba cada archivo dos veces** —«Cartas desde Antares» junto a
   «Cartas.desde.Antares.2017»—. Defecto del sembrador, no del producto: el id de un título **es el
   del archivo** (lo dice `ApplyIdentification` y lo exige la proyección de `CatalogRepository`), y
   el sembrador acuñaba un GUID nuevo. Corregido fuera del árbol; de paso desapareció que la
   reproducción escribiera el progreso bajo una clave que Inicio no lee.

**Lo que cambió en el arnés, todo medido** (vive en
`%USERPROFILE%\.claude\projects\D--Proyectos-ap-reelume\tools\`): `shoot.ps1` gana `-Screen`
(`CopyFromScreen` sobre `DWMWA_EXTENDED_FRAME_BOUNDS` con las esquinas cuadradas por
`DWMWA_WINDOW_CORNER_PREFERENCE`, que es lo que compone la capa de vídeo sin arrastrar el
escritorio), `-Downscale`, `-Click 'x,y;x,y'` y búsqueda acotada `'Padre>Hijo'`.

**Y tres cosas que quien tome capturas debe saber, porque costaron su medición:**

- **Pedir 1600 × 1000 daba a la aplicación 1067 × 667 lógicos**, porque esta pantalla pinta al 150 %.
  Con eso la banda de transporte cae fuera de la ventana y la ficha de serie nace desplazada. Se pide
  **2400 × 1500 y se guarda con `-Downscale 1.5`**: la app recibe los 1600 × 1000 que el diseño
  dibuja y el texto llega sobremuestreado. `AVALONIA_SCREEN_SCALE_FACTORS` **no** fuerza el 1:1: se
  midió y la captura salió idéntica.
- **Con el reproductor abierto, UIAutomation deja de poder recorrer la ventana**: `FindFirst` y
  `FindAll` contestan «Unexpected HRESULT has been returned from a call to a COM component» para todo
  lo que no encuentren antes de llegar a la superficie de vídeo. Por eso el aviso de reanudación se
  descarta con `-Click` y no por nombre.
- **El vídeo del reproductor es un fotograma generado con `ffmpeg`** —un faro sobre el mar, en la
  paleta del producto— codificado a 1 h 36 m, que es lo que el catálogo declara para «El Faro de
  Piedra», para que la posición sembrada del 54 % sea válida: la captura dice `52:12 / 1:36:00`.
  Los archivos que siembra `tools/seed` son de 2 bytes y no hay nada que reproducir.

**Sin insignia de CI, y es una decisión medida.** El flujo no corre en `main` a propósito, así que
`gh run list --branch main` se queda en el 2026-08-23 con `main` varios commits por delante: una
insignia ahí es verde para siempre y no significa nada. La página lo dice en palabras y enlaza al
flujo.

**Y un rojo de CI que no era del cambio, corregido cambiando el instrumento.**
`MediaPlayerReleaseOwnershipTests` afirmaba sobre `PendingDeferredReleaseCount`, que es un **nivel**
—lo que la cola de liberación tiene ahora mismo— y el drenaje lo vacía un segundo después de que
cada media llegue. En el ejecutor hospedado, cuatro veces más lento que esta máquina (6 m 28 s
contra 1 m 33 s en la misma suite), `StopAsync` dura más que esa ventana y el nivel ya ha vuelto a su
sitio cuando se lee. `LibVlcFactory` expone ahora `DeferredReleaseTotal`, un **total monótono**, y la
prueba afirma sobre él: lo que el contrato dice es que la media pasó por la cola. **Se cambió lo que
se mide, no la tolerancia.**

**Y una observación local que NO se tocó**: `PlaybackGateEnduranceTests` falló una de cuatro pasadas
aquí con 217 recursos contra un techo de 200, y en CI pasó. Es un techo de recursos nativos tras 50
ciclos y aflojarlo sería aflojar una puerta; queda anotado, sin tocar, para quien lo vea repetirse.

### Lo siguiente, y por qué es esto

**El paso 12, la landing**, que estaba esperando exactamente a esto: usa **las mismas cinco
capturas**, vive en `site/` declarada en `VersionedDirectories`, es autocontenida (sin CDN, sin
fuentes remotas, sin analítica), bilingüe, y se publica cuando haya versión que descargar. Está
enteramente decidida en este documento (buscar «12. La landing»). La bloquea a medias el vectorial de
la marca, que se puede sustituir por un marcador.

**Y el paso 7, el paseo físico del propietario**, que sigue siendo lo único que bloquea el corte de
0.2.0 (paso 8).

## Estado al cierre del 2026-08-24 (tarde) — F11 CERRADA: el plan de paridad está completo

**Las once fases del plan maestro están hechas.** PRD-006 pasa a `VERIFIED` con su
[matriz de paridad](evidence/stable/PRD006-parity-matrix.md): 21 capturas de la aplicación **real**
—compilada en Release, arrancada como proceso, con una biblioteca sembrada de 21 elementos y
navegada por UIAutomation— junto a las 16 del prototipo, en los cuatro diccionarios.

**Y la matriz sirvió para lo que sirven las capturas: cazar lo que ninguna puerta mira.** Tres
defectos, los tres con su rojo archivado y su corrección:

1. **Inicio arrancaba vacía** ([evidencia](evidence/stable/audit-initial-route-never-navigates.md)).
   La ruta con la que nace `NavigationService` no pasa por `Navigate`, así que `Navigated` nunca
   suena para la primera pantalla, y toda alimentación de superficies colgaba de ese evento. El
   shell reproduce ahora su ruta inicial por el mismo camino. Decimocuarta forma del defecto de la
   casa, y la que más se ve: el minuto en que todos miran.
2. **El título de una ficha se iba a dos líneas** y empujaba su año por debajo del año de la ficha
   vecina. Una línea con puntos suspensivos (decisión del propietario, mirando la app).
3. **Un `ToggleButton` no era una píldora**: la forma estaba declarada sólo para `Button` y ese
   selector no lo alcanza, así que «Favorito» y «Ver más tarde» llevaban la caja baja del tema base
   junto a las píldoras de su fila. `ControlStateTests` lo afirma comparando las dos geometrías.

**Fase B de fidelidad, hecha**: dieciocho bordes decorativos bajaron de `ShellBorderBrush` a
`ShellHairlineBrush` (el capilar del prototipo), la tarjeta de duplicados ganó su fondo de tarjeta,
y se quedan con borde fuerte —con aritmética— el vacío discontinuo de Biblioteca y las cinco
superposiciones del reproductor. El halo del acento ya estaba en 0.156; la nota anterior lo daba
por pendiente y era falso.

**Herramientas de captura mejoradas** (viven fuera del árbol, en
`%USERPROFILE%\.claude\projects\D--Proyectos-ap-reelume\tools\`): `shoot.ps1` acepta `-Theme` (fija
`theme.preference` antes de arrancar) e `-Invoke` con secuencia separada por `;`; ya no borra una
raíz de datos que no creó él. `preview` acepta `APR_PREVIEW_THEME` y `APR_PREVIEW_SCENE=player`.

**Una alarma falsa medida y descartada**: el panel de fallo del reproductor parecía solaparse con la
banda de transporte. Era el arnés —la vista apilada con un segundo control recibía una fracción del
alto—; montada sola a 900 px se pinta entera. Se comprobó cambiando el arnés, no razonando.

**Recuentos finales**: 53 vistas, **576** cadenas por idioma (el plan estimaba 517), 48 filas en
`LeadingActionTests`, 0 pendientes del paseo, deuda de cobertura 215.

**Cerrado y verificado**: `main` = `1c1eaa9`, con CI en verde leído por `--json conclusion` sobre
los cuatro commits. Ese run corre el superconjunto —`verify.ps1` + accesibilidad ×2 + recuperación
×2 + cobertura del paseo—, y la deuda de cobertura que emitió es **idéntica** a la del árbol: 215
entradas, ni una bajada.

**Un defecto del arnés, encontrado al cerrar y corregido**
([evidencia](evidence/stable/audit-harness-geometry-lied.md)): las 21 capturas salieron a
1600 × 2186 pidiendo 1600 × 1000 porque **en PowerShell `$H` y `$h` son la misma variable** y el
manejador de la ventana pisaba la altura. Cuatro hipótesis se midieron y se refutaron antes de dar
con ella —la anchura era correcta, y eso descarta la escala de DPI por sí solo—. `shoot.ps1` usa ya
`-Width`/`-Height` y **verifica su propia geometría** antes de disparar. No invalida la matriz: una
ventana más alta enseña más de cada vista, y la matriz nunca declaró resolución.

### Lo siguiente, y por qué es esto

**El paso 11 —la página del repositorio con capturas— queda DESBLOQUEADO hoy.** Su única condición
era «no se empieza antes de que la §4 termine», y la §4 terminó. Está **enteramente decidido** en
este mismo documento (buscar «11. La página del repositorio»): cinco capturas —Inicio, Biblioteca,
ficha de serie, reproductor con su columna, bandeja de revisión—, en **inglés**, tema **oscuro**, a
**1600 × 1000**, versionadas en `docs/assets/` porque un README de GitHub no puede enlazar a un
artefacto de CI. **No hay nada que deliberar: hay que ejecutar.**

Y ahora se puede: hasta hoy `shoot.ps1` no sabía dar 1600 × 1000 aunque se le pidiese. **La
herramienta está probada de punta a punta con las tres condiciones a la vez** —inglés, oscuro y
1600 × 1000— en `artifacts/ui-captures/T36-app/probe-en-1600x1000.png` (fuera del control de versiones), que dice «Home», «Continue
watching» y «Open library». El idioma se fija como el tema, escribiendo `ui.language` (valores `es`
o `en`, medidos en `StoredLanguageService`) en el `settings.json` de la raíz antes de arrancar.

La invocación, tal cual, para no reconstruirla: / The invocation, as is:

```powershell
# Desde la raíz del repositorio. / From the repository root.
$tools = "$env:USERPROFILE\.claude\projects\D--Proyectos-ap-reelume\tools"
$env:AP_LOCALMEDIA_DATA_ROOT = "$tools\matrix-root"
$exe = ".\src\ApSolutions.LocalMedia.Windows\bin\Release\net10.0-windows10.0.22621.0\ApSolutions.LocalMedia.Windows.exe"
& "$tools\shoot.ps1" -Out ".\docs\assets\home.png" -Wait 16 -Theme Dark -Language en -Width 1600 -Height 1000 -Exe $exe
& "$tools\shoot.ps1" -Out ".\docs\assets\show.png" -Wait 16 -Theme Dark -Language en -Width 1600 -Height 1000 -Invoke 'Library;Historias del Muelle' -Exe $exe
```

**Tres avisos medidos para quien las tome**: la aplicación tarda **más de 9 segundos** en dar
ventana, así que `-Wait 16`; con `-Language en` los nombres accesibles de `-Invoke` son los
**ingleses** (`Library`, `Review`, `Duplicates`, `Settings`, `Resume`, `Play from the start`),
porque UIAutomation busca por el nombre que la interfaz muestra; y `-Settle <segundos>` espera una
vez más antes de disparar, para una superficie que sigue trabajando después de la pulsación.

**Y un obstáculo real, ya medido, para la captura del reproductor** —la cuarta de las cinco—: los
archivos que siembra `tools/seed` son de **2 bytes**, así que no hay nada que reproducir. Con
`ffmpeg` —presente en esta máquina— se genera uno real encima del sembrado, conservando el nombre
para que la fila del catálogo siga apuntando a él:

```
ffmpeg -y -f lavfi -i "testsrc2=size=1920x1080:rate=24:duration=120" -f lavfi -i "sine=frequency=440:duration=120" -c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -shortest <media>/El.Faro.de.Piedra.2019.mkv
```

Hecho eso, `-Invoke 'Library;El Faro de Piedra;Resume'` **abre el reproductor de verdad** —sus
controles salen en la captura—, pero **`PrintWindow` no compone su capa de vídeo**: la superficie
llega transparente y se ve la ficha por debajo, con los botones del reproductor dibujados como
marcos vacíos. Es la trampa conocida del contenido acelerado por GPU. **Tres salidas, sin decidir
todavía porque la decisión pide medir cuál da la imagen que el README merece**: capturar la pantalla
en vez de la ventana (`BitBlt` sobre el escritorio, con la app al frente); usar
`Windows.Graphics.Capture`, que sí compone la GPU; o aceptar el reproductor con su fotograma en
negro, que es honesto pero pobre para una portada.

**La primera se probó ya, y no salió gratis**: `CopyFromScreen` sobre el rectángulo de la ventana sí
compone lo que la GPU pinta, pero **arrastra el escritorio en los bordes redondeados** —la ventana
tiene esquinas curvas y sombra, y el rectángulo incluye píxeles de lo que haya detrás—, así que pide
o un fondo controlado o un recorte de unos píxeles por lado. Y en esa misma pasada el reproductor
**no llegó a abrirse** pese a invocarse los tres botones, lo que apunta a que abrir un MKV de 220 MB
necesita más margen del que se le dio. Las dos cosas son medibles en una tanda corta; ninguna está
decidida, y quien la haga debe decidirla **habiendo medido**, no leyendo esta nota.

**Del alcance abierto, lo de siempre**: PRD-003 (ARM64, bloqueado por hardware), REL-001/REL-004 y
PLY-004 (5.1/7.1, bloqueado). Y el **paso 7, el paseo físico del propietario**, que sigue siendo lo
único que bloquea el corte de 0.2.0 (paso 8).

## Estado al cierre del 2026-08-24 (madrugada) — fidelidad de paleta HECHA y verificada en pantalla

**El propietario miró las capturas y pidió fidelidad** («ni los colores ni la elegancia»; React
descartado por decisión suya). La causa era real: los valores del árbol venían de una instantánea
anterior al prototipo. Hecho en `0667010`:
- **Paleta leída del `tokens()` del propio prototipo** y re-valorada en Dark y Light: fondo
  #050608/#08090C, tarjetas #12151B, rellenos #171B22, textos #EDF1F6/#8B97A8, estados como
  mezclas opacas. Única cesión al canón: el borde (su #3A424F da 1,96:1; quedó #5C6878 por el
  suelo de 3:1 de `ContrastTokenTests`).
- **El primario es la píldora clara del prototipo** (#F3F6FA/tinta #0A0C10 en oscuro) con familia
  `PrimaryAction*` propia en los 4 temas, pares en la puerta, y `ControlStateTests` re-declarado:
  bajo la mano SIGUE siendo primario; deshabilitado cae al gris común.
- **Elevaciones tipadas** (`ElevationShadow`/`Strong` como `BoxShadows`) gastadas en setting-row,
  PosterCard y el diálogo; HC sin sombras. `ScalarTokenTests` vigila que se gasten.
- Verificado en pantalla: `artifacts/ui-captures/T36-app/*-v2.png` (biblioteca con 21 elementos,
  Ajustes, Duplicados, Revisar) junto a `T36-proto/`.

**El data root de la matriz vive FUERA del árbol**:
`%USERPROFILE%\.claude\projects\D--Proyectos-ap-reelume\tools\matrix-root` — `artifacts/` lo
barren las suites (se comió el primero). `tools/shoot.ps1` ya respeta un
`AP_LOCALMEDIA_DATA_ROOT` puesto por el llamador.

**CI**: candidato `0667010` (paleta) tras `96bf0b3` (trinquete: 215, dos suelos subidos, dos pagos
—la rama de cargas solapadas de `IsLoading` y las cuatro lecturas de gramática del updater más la
noticia repetida—). **OJO al mirar runs**: `gh run watch --exit-status | tail` ENMASCARA el exit
—leer SIEMPRE la conclusión con `gh run view --json conclusion`—; ya costó un ff erróneo (deshecho
en un minuto con force-with-lease de vuelta a b21f8e3).

**Fidelidad, fase B (pendiente fino)**: tinte del shell del prototipo es rgba(98,174,232,.156) —el
halo actual usa 0.14—; bordes decorativos que aún usen `ShellBorderBrush` → hairline; capturas
light comparadas; la home de la matriz necesita los enlaces del escaneo en el seed («En curso» no
pinta con la siembra actual; ver SQL simple de `HomeReadModel` — watch_state+titles debería
bastar: DEPURAR por qué no sale, quizá la ruta inicial no dispara `NavigatedAsync` al arrancar,
que también explicaría la home vacía con click en su propio botón de riel ya activo).

## Estado al cierre del 2026-08-23 (noche) — F7–F10 cerradas, F11 a media matriz

**Hecho hoy**: F7 (menú de velocidad, botón de «elegir otra versión» con flyout de las filas
reales, MaxHeight en los tres overlays, los tres avisos de marcadores), F8 (pestañas
Metadatos | Renombrado seguro), F9 (las cuatro gramáticas del actualizador con expansor y escena,
bloque de la base en Copias, pasos numerados y avisos del asistente), F10 (apr-shim con
`IsLoading`, privacidad en positivo, los 16 comentarios; manija ELEVADA al propietario — CheckBox
es decisión medida), y de F11: SURFACES re-medido bilingüe, PRD-006 en FEATURES, 16 capturas del
prototipo en `artifacts/ui-captures/T36-proto/`. Cinco archivos salieron de la deuda de cobertura
(215, trinquete 215). Tres rojos de CI leídos y arreglados (ancla del repositorio, timeout del
check, preferida sin fijar en la escena de recuperación).

**CI**: el candidato a verde es `cce3b1f`. Con su verde → fast-forward de `main` (pendiente desde
b21f8e3; **mirar el run de cce3b1f antes de avanzar**).

**HALLAZGO RESUELTO (2026-08-23, madrugada)**: era LA HERRAMIENTA — `shoot.ps1` aísla cada
ejecución poniendo su PROPIO `AP_LOCALMEDIA_DATA_ROOT` temporal (línea 20), pisando el de la
matriz; la app estaba sana. El shoot ahora respeta un root ya puesto por el llamador, y la
captura de Biblioteca salió con los 21 elementos, portadas de color computado, años y «No
disponible». Queda fino de siembra para la HOME («En curso»/«Añadido recientemente» leen los
enlaces del escaneo que el seed aún no escribe — mirar el SQL de `GetHome` y sembrar lo que una);
`tools/seed` ya siembra por `UpsertTitleAsync` (el FTS lo exige). La crónica original del
hallazgo, abajo, se conserva como registro del método:

**HALLAZGO ABIERTO (bloquea la mitad-app de la matriz de capturas)**: la aplicación REAL con un
data root sembrado pinta el shell VACÍO (0 raíces, 0 títulos, onboarding de primer arranque)
mientras que los repos directos y el harness leen las filas de esa misma BD. Medido, para no
repetirlo: (1) la variable `AP_LOCALMEDIA_DATA_ROOT` LLEGA al hijo (un cmd hermano la imprime);
(2) la app SÍ la usa (un root virgen recibe library.db + 18 .bak al arrancar); (3) sobre la BD
sembrada, `LibraryRootRepository.ListAsync` da 1 raíz (herramienta `tools/seed` con `--check`);
(4) la siembra pasó de SQL a `UpsertTitleAsync` (el FTS del catálogo lo exige) y aun así 0.
Dos mediciones más de esta misma noche: (5) una sonda en `OnNavigated` (onFailure que escribe a
un archivo) quedó EN SILENCIO navegando a Biblioteca — la carga corre sin excepción y devuelve 0,
así que NO es una excepción tragada —; (6) ojo con la sonda misma: el primer intento no compiló
por el heredoc y dos capturas se hicieron con un binario viejo — verificar SIEMPRE `grep -c error`
del build antes de creer una sonda—. Sospechoso restante: la app abre OTRO archivo de BD distinto
del que se comprueba (¿normalización de la ruta? ¿cwd?) — confirmarlo imprimiendo
`AppDataPaths.DatabasePath` desde dentro (una línea temporal en `FinishShell`).
**VÍA ALTERNATIVA PARA LA MATRIZ (independiente del misterio)**: que LA PROPIA APP siembre —
arrancar con un root virgen, añadir `artifacts/matrix-root/media` por la interfaz (el diálogo de
añadir raíz, con Invoke), dejar que el escaneo real catalogue los archivos (quedan «sin
identificar», que pinta fichas igualmente), y capturar. Cero dependencia del data root sembrado. Herramientas de la sesión:
`%USERPROFILE%\.claude\projects\D--Proyectos-ap-reelume\tools\seed\` (siembra la matriz;
`--check` lee raíces) y `tools\preview\` (esta sesión lo dejó montando el reproductor de F7).

**F11 restante**: matriz-app (bloqueada por el hallazgo), capturas HC (shell + reproductor + un
formulario), recuento final contra el Inventario, CI verde + ff, checklist cerrado.

## EL PLAN MAESTRO DE PARIDAD (2026-08-23) — el hilo que no se vuelve a perder

**Encargo del propietario, con sus palabras: la aplicación idéntica al prototipo en diseño — una
copia exacta — y que todo funcione.** El plan por fases de abajo se acordó con él tras leer el
paquete `design/` ENTERO (README, PROMPT, Propuesta §1–§7, Inventario, Auditoría, Cadenas, Catálogo)
y tras verificar contra el proyecto remoto de Claude Design que el `design/` local ES la
especificación vigente: mismo recibo de sincronización (2026-08-17T20:09:57Z), y los dos documentos
que el proyecto remoto tiene de más —«Especificación de diseño» y «Direcciones»— son borradores
ANTERIORES ya superados (conservan el acento de alto contraste `#FFFF00` y el foco de anillo simple
que la Propuesta corrigió). La especificación operativa sigue siendo: **§4 de la Propuesta (48
filas) + Catálogo de elementos + el prototipo como referencia visual.**

### Decisiones del propietario del 2026-08-23, que REVOCAN dos de más abajo

- **Las 25 cadenas de consecuencia: APROBADAS tal cual** están en `Cadenas nuevas`. Con las 22 de
  vacíos ya prometidas: 470 → 517 claves por idioma.
- **Reparto entero de raíces**: diálogo superpuesto «Añadir raíz de medios» sobre la cuadrícula
  (así lo hace el prototipo: `addOpen`, centrado, 520 de ancho) + lista de raíces en Ajustes →
  «Biblioteca y escaneo». `RootOnboardingView` queda solo para el primer arranque con sus 4 formas.
- **Clave nueva para la acción primaria de la cabecera de Biblioteca**: `LibraryAddMediaAction` =
  «Añadir medios…» / «Add media…» — la elipsis es la convención de «abre un diálogo» y hace la
  cadena única; el «+» del riel conserva `NavigationAddMedia` intacto.
- **Los dos altos contrastes SÍ pasan a ser elegibles** (Apariencia con 5 píldoras y las claves
  `ThemeHighContrastLight`/`ThemeHighContrastDark`) — revoca la decisión de la mañana del
  2026-08-23 de abajo, por la vía que ella misma dejó abierta («si el propietario lo pide»): el
  propietario aprobó el plan que las incluye.
- **«Duplicados» SÍ entra al riel y «Copias» sale de él** — revoca la decisión del 2026-08-22 de
  abajo. La puerta de Copias no se pierde: se muda a Ajustes → «Copias y restauración», que es
  donde el prototipo la tiene (su mapa de navegación lo confirma, y el riel del prototipo son 5
  destinos + añadir).

### Las fases, con su casilla — se marca en el commit que cierra cada una

- [x] **F0** — este checklist volcado; copias `_proto-*` borradas del árbol; el árbol sucio de F1
  compilando.
- [x] **F1 — Biblioteca**: cabecera en una línea (título 28 + contador + búsqueda 280 a la
  derecha); píldoras Todo/Películas/Series sobre `TypeFilter` (bits de tipo separados del estado);
  desplegables «Estado» y «Orden» como píldora-menú que aplican al elegir; **«Aplicar» se quita**
  (el prototipo no lo tiene; desviación del Inventario, documentada); enlace «Quitar filtros» solo
  con filtros activos; estados de la cuadrícula (vacía con `LibraryEmpty*`, sin resultados,
  escaneando, con contenido); ficha con distintivo de tipo, ✓ visto, progreso 3 px, banda «No
  disponible».
- [x] **F2 — Raíces**: el diálogo superpuesto (ruta mono + Examinar…, tipo detectado desde la ruta
  con adaptador `DriveType` —plan B: las tres píldoras dentro del diálogo—, `RootKind*Hint`,
  Cancelar/`RootAddAction`, fallo Assertive dentro); se abre desde la cabecera, el «+» del riel y
  el vacío; Ajustes → «Biblioteca y escaneo» con la lista de raíces en filas-tarjeta y el borrado
  confirmado en Danger con `RootRemove*`; onboarding reducido a primer arranque; TODAS las escenas
  del paseo del formulario reescritas para abrir el diálogo primero (un panel oculto no está en el
  árbol visual).
- [x] **F3 — Inicio**: héroe a sangre (arte + degradado, antetítulo, Continuar píldora clara +
  Detalles fantasma); carril «Continuar viendo» 16:9 con acciones por tarjeta; «Añadido
  recientemente» + «Ver toda la biblioteca →» que absorbe `LibraryEntryView`; `HomeLayoutTests`
  actualizado.
- [x] **F4 — Fichas**: banner con arte + velo 90°, portada elevada, chips, sinopsis, acciones
  sobre el banner; secciones Versiones (filas elegibles, 5 formas) y «Otras acciones»; tráiler
  LIB-014 con sus tres `Trailer*`; serie con selector de temporada segmentado y filas de episodio
  de 56 px (número mono a la derecha, ○ ◐ ●).
- [x] **F5 — Tinte de acento**: halo radial del acento en lo alto del contenido; contraste
  verificado TAMBIÉN sobre el halo (la trampa de `ContrastTokenTests`: una puerta mide lo que
  enumera).
- [x] **F6 — Shell**: riel de 5 destinos + añadir (entra Duplicados con ruta propia, sale Copias);
  Ajustes con índice lateral `side-list` (los estilos declarados sin consumidor, por fin
  alimentados) y secciones una a la vez en el orden del prototipo (Apariencia · Biblioteca y
  escaneo · Reproducción · Audio · Subtítulos · Accesibilidad · Privacidad · Detección de
  segmentos · Atajos · Copias y restauración · Actualización · Créditos); Apariencia 3 → 5
  píldoras; `apr-in` SOLO si el shell pasa a hospedar rutas en un `ContentControl` (la medición de
  abajo sigue valiendo: alternar `IsVisible` no se puede animar).
- [x] **F7 — Reproductor (16)**: motivos × acciones verificados; `PlayerRecoveryChooseAnotherVersion`
  de `TextBlock` a botón; velocidad como menú (sustituye las 9 píldoras); `VideoStatusOverlay` en
  dos gramáticas y −2 fichas duplicadas; superposiciones con alineación + `MaxWidth` + `MaxHeight`;
  las 4 listas con sus vacíos (`MarkersEmpty*`, `DetectedMarkersEmpty*`, `TracksEmpty*`,
  `PlayerVersionsEmpty*`) y los tres `Marker*` de consecuencia; `AudioOutputView` en Warning;
  atajos a dos columnas + `ShortcutsEmpty*`; mini verificado a 320; `LooseFileBanner` diseñado
  (verificación bloqueada, documentada).
- [x] **F8 — Revisión y editor**: bandeja vacía POSITIVA (`ReviewInboxEmpty*`); Duplicados en
  `UniformGrid` 2 columnas, mono, `DuplicatesEmpty*`, sin acción de borrado ni desactivada; editor
  con pestañas Metadatos | Renombrado; los 3 mensajes con glifo; renombrado origen → destino mono.
- [x] **F9 — Copias/Recuperación/Actualización**: `BackupView` con estado de la base +
  `BackupHistoryEmpty*` + `BackupFailedIntactNotice`; `RestoreWizardView` con pasos numerados,
  reasignación 2 columnas truncada por la izquierda, `RestoreRootsEmpty*` y los 3 `Restore*` + los
  3 `RestoreRoot*Status`; `DatabaseRecoveryView` en Danger, mono, `WrapPanel`; **`UpdateView` con
  las cuatro gramáticas** (hoy 0 de 4, medido), un solo `Border` `Polite`, motivo POR ENCIMA del
  estado, expansor `UpdateRejectionDetail` + sus 3 cadenas; Créditos TMDB; `StartupView` costura
  MSIX.
- [x] **F10 — Animaciones y barrido final**: `apr-shim` cuando un modelo sepa que carga (empezar
  por `LibraryViewModel`); manija 160 ms; privacidad con las dos gramáticas + `PrivacyNoHosts*` +
  `DiagnosticsEmptyFieldsNotice`; Lifecycle en Warning; los 16 comentarios de diseño en sus 9
  archivos. [DECIDIDO 2026-08-24] La manija NO se migra: el árbol conmuta con `CheckBox` por decisión
  medida (18 usos, 73 recursos, `CheckBoxStateTests`); un `ToggleSwitch` costaría su constelación
  de estados en 4 temas y su transición queda fuera del alcance de las puertas de movimiento — el
  costo no compra elegancia proporcional y la conmutación con casilla es igual de legítima en
  Fluent. Si el propietario la quiere algún día, es un tramo propio con dos vueltas de CI. Inicio no lleva esqueleto: sus carriles
  pintan en el cuadro en que llega la respuesta.
- [x] **F11 — Cierre** (2026-08-24): `SURFACES.es/en.md` re-medido y declarado cerrado; PRD-006 pasa
  a VERIFIED en FEATURES con su evidencia; [matriz de paridad](evidence/stable/PRD006-parity-matrix.md)
  con 21 capturas de la aplicación REAL sobre biblioteca sembrada —16 parejas app-vs-prototipo en
  claro y oscuro, más shell, formulario, ficha y reproductor en alto contraste—; recuentos medidos
  (53 vistas, **576** cadenas por idioma —no las 517 que el plan estimó—, 48 filas de acción líder,
  0 pendientes del paseo) con la desviación −«Aplicar» documentada. La matriz cazó tres defectos
  que ninguna puerta veía: Inicio arrancaba vacía, el título de ficha se iba a dos líneas y un
  conmutador no era una píldora.

### Reglas por commit, sin excepción

Bilingüe; control nuevo = nombre accesible + escena del paseo EN EL MISMO COMMIT (el trinquete
está en 0 y no sube); vista tocada = su fila en `LeadingActionTests`; ningún número que tenga
token; nada fuera de 900; toda fila de acciones `WrapPanel`; todo panel superpuesto con alineación
y las DOS dimensiones acotadas; suites afectadas en local → commit → push a la rama → CI verde →
fast-forward a `main`; evidencia por fase en `artifacts/ui-captures/` con la captura del prototipo
al lado.

### Fuera del alcance, y por qué

Los 35 PNG de instalación y el icono de bandeja (bloqueados en el original vectorial de la marca);
la verificación de `LooseFileBanner` (defecto medido el 17-08); la barra de título dibujada, el
panel «Demostración», el selector de idioma de la barra y las 30 fichas de demostración (aparatos
del prototipo, no producto: contarlos llevaría el inventario de 202 a 232 sin que exista ninguno).

---

## LO PRIMERO (2026-08-23): el encargo cambió de tamaño, y hay que leer `design/` antes de tocar

**El encargo ya no es «acercar unas cuantas vistas»: es paridad con el prototipo en TODA la
aplicación.** Y la lección de esta sesión es de método, no de código: se trabajó tres piezas contra
las descripciones de esta nota en vez de contra `design/`, y el propietario dijo, con razón, que la
aplicación **no se parecía en nada** a su prototipo.

**Lee `design/README.md` entero antes de escribir una línea.** Dice cosas que no se deducen mirando
el `.dc.html` y que corrigen el rumbo:

- **Los `.dc.html` son referencia de diseño, NO código para copiar.** La especificación es la **§4 de
  `Propuesta de diseño`**: 48 filas, vista por vista, con los estados que hay que pintar.
- **Las píldoras son `CornerRadius=18`** —la mitad del alto de control—, y lo dice la §7 con ese
  número. No el `999` de CSS.
- **La barra de título dibujada a mano NO se traslada.** El paquete lo dice con todas las letras: «la
  aplicación usa el cromo del sistema». Mirando el prototipo parece que falta; no falta, está decidido.
- Y `Cadenas nuevas` trae las claves aprobadas en los dos idiomas. **Las 25 «de consecuencia» están
  propuestas y NO aprobadas**: hay que preguntar antes de escribirlas.

### Lo hecho en esta sesión — siete commits, todos con sus suites en verde

1. **Ajustes: la fila-tarjeta.** 18 tarjetas más la plantilla de los 11 atajos, en ocho de las diez
   secciones. Tres cadenas del prototipo: `SettingStateOn`, `SettingStateOff`, `AppearanceThemeLabel`.
2. **Revisión: la tarjeta de candidato.** Borde **entero** tintado —el prototipo tiñe `borderColor`,
   no un borde izquierdo—, distintivo arriba a la derecha, dos columnas y una barra de confianza.
3. **El panel conmutable del reproductor.** Cinco pestañas, cero C# y cero cadenas.
4. **El texto invisible de la columna del reproductor**, medido en **1,10:1** y corregido.
5. **El arte de portada**, que es el cambio que de verdad se nota. Ver abajo.
6. **Todo botón es una píldora**, en las diez pantallas a la vez.
7. La nota y el paquete `design/` leídos y contrastados.

### El hallazgo que desbloquea el parecido, y por qué estaba mal cerrado

**El prototipo NO tiene arte.** Leyendo su fuente: cada portada suya son cuatro degradados CSS
calculados sobre **un solo tono** —`art(h, v)` en la línea 1491 de `AP Reelume.dc.html`— y no hay ni
una imagen en el archivo. La razón por la que las portadas estaban fuera de 0.2.0 —«no hay arte ni
ficha de TMDB»— sigue siendo cierta y **no aplica** a esto: el muro de color no cuesta red, ni ficha,
ni un archivo en disco. Está hecho en `Library/PosterArt.cs`, con el tono derivado del título por un
hash propio —`string.GetHashCode` lo aleatoriza por proceso— y apagado en los dos altos contrastes.

**La lección general: antes de dar por imposible una parte del prototipo, mira CÓMO la hace él.**

### Las decisiones tomadas, para no re-deliberarlas

- **[REVOCADA el 2026-08-23 por el propietario — ver EL PLAN MAESTRO arriba.]** **Los dos altos contrastes NO pasan a ser opciones elegibles**, aunque la §4 pida cinco píldoras de
  tema con `ThemeHighContrastLight` y `ThemeHighContrastDark`. Razón: en Windows 11 el contraste es un
  ajuste **del sistema**, con su atajo y su página de Configuración; ofrecerlo como cuarta y quinta
  opción duplica un control del sistema y permite un estado que nadie modela —la aplicación en alto
  contraste mientras el escritorio no lo está—. `HighContrastPolicy` e `IHighContrastService` ya leen
  el estado real. Lo que la §4 quería —«hoy inalcanzable»— ya está: los cuatro diccionarios existen y
  se aplican. **Reversible**: si el propietario lo pide, son dos cadenas y dos valores en
  `ThemePreference`.
- **El arte generado entra aunque la §4 dijera «iniciales sobre `ControlFillBrush`».** El prototipo
  dibuja color, y las iniciales **se quedan encima**, así que se cumplen las dos: color para
  reconocer de un vistazo, letras para quien no distingue color.
- **`UpdateRejectionDetail`** —el noveno control que la §4 añade— entra cuando se haga `UpdateView`,
  no antes.
- **La barra de título** queda como está, por la decisión del propio paquete citada arriba.

### Lo que queda, por cuánto parecido mueve

1. **Biblioteca**: cabecera con contador, filtros píldora segmentados («Todo/Películas/Series»),
   búsqueda a la derecha, «Añadir medios» como acción primaria, y **el formulario de carpetas fuera de
   la mitad superior** — hoy ocupa media pantalla antes de la cuadrícula.
2. **Inicio**: el héroe a sangre con degradado, en vez de la tarjeta con borde; y los carriles.
3. **Detalle de película y de serie**: el prototipo pone la portada grande sobre un degradado del
   propio arte, con las acciones en píldora y una columna de «Otras acciones».
4. **El tinte de acento** en la parte alta del contenido, que hoy es fondo plano.
5. **El índice lateral de Ajustes y las pestañas de Metadatos** — la pieza que ya venía pendiente.
6. **`UpdateView`** con sus 23 mensajes en cuatro gramáticas, y **`PlayerView`** con sus 7 motivos.

### Dos hallazgos medidos que no son de esta tanda

- **Las razones de red salen en inglés dentro de la interfaz española.** Se ven al final de Ajustes ·
  Privacidad. Vienen de `NetworkPurpose.Reason`, que las escribe el registro en código y no los
  recursos, así que se saltan la regla de bilingüismo. No se tocó: es un cambio de datos, no de dibujo.
- **El rojo de CI del commit «The player column shows one panel at a time» no era del código.** El log
  dice `Coverage gate: 216 file(s) still short of 96/96, ratchet 216, **1 improved**`: el trinquete
  refunfuñó por una mejora de medición. Se comprobó descargando el artefacto `coverage-debt` del run
  verde siguiente: **idéntico al del repositorio**, así que fue ruido y se resolvió solo. La confusión
  la añade el `WARNING` del presupuesto de rendimiento, que aparece justo antes y **no** es la causa.

### Herramientas de esta sesión, que ahorran horas

**Las tres viven fuera del árbol, en `%USERPROFILE%\.claude\projects\D--Proyectos-ap-reelume\tools\`**,
que persiste entre sesiones: son herramientas de inspección, no puertas, y no ensucian el
repositorio.

- **`shoot.ps1`**: captura la app con conciencia de DPI. `-Invoke <nombre accesible>`
  navega por UIAutomation, `-ScrollPercent` desplaza el primer `ScrollViewer`, `-Exe` apunta a otro
  ejecutable.
- **Un proyecto `preview` de una clase** que pone `App.ShellFactory` y monta la vista que sea con el
  tema real, incluida la variante clara. Es lo que midió que un `TabControl` salta una pestaña
  invisible y lo que enseñó el texto invisible antes de tocar nada.
- **El prototipo, por rutas**: `python -m http.server 8765 --directory design`, copiar el `.dc.html`
  cambiando `route: 'home'` por la ruta que sea, y
  `chrome --headless=new --window-size=1500,1000 --virtual-time-budget=8000 --screenshot=…`. **Borra
  las copias del árbol al terminar.**
- **Los documentos de `design/` en texto plano**: los `.dc.html` de documento llevan el contenido
  inline, así que un `re.sub` de etiquetas basta para leerlos enteros.

## Lo que la sesión del 2026-08-22 (noche) dejó escrito, y sigue valiendo

### El plan de seis pasos del rediseño está HECHO (2026-08-22, noche)

**Los seis pasos de abajo están construidos y empujados**, cada uno con su commit, sus mediciones y su
comprobación con la aplicación abierta. Lo que sigue debajo es el plan tal como se escribió, con cada
paso tachado y lo que costó anotado dentro — se deja entero a propósito, porque las razones valen más
que la lista.

**Las cuatro animaciones: dos hechas, y las otras dos CONTESTADAS, no aplazadas.** El conducto ya
existe y es el que `ReducedMotionTests` describía: el token `MotionDuration` es un `TimeSpan` que las
animaciones leen y que **`FluentThemeService` escribe** —pone `TimeSpan.Zero` con movimiento
reducido—, así que el servicio deja de tener su copia del 160 y la preferencia llega de verdad.

- **`apr-tip`** — el tooltip de los destinos del carril entrando 6 px desde la izquierda. Es la
  animación que más se gana: los destinos perdieron sus palabras al pasar a pictogramas, y el tooltip
  es donde están ahora.
- **`apr-pulse`** — el punto junto a «Escaneando», que late mientras el escaneo corre. Es lo único de
  esa fila que distingue «sigue trabajando» de «se paró», porque el contador salta a tirones.
- **`apr-shim` NO SE HACE, y la razón está medida**: es el brillo sobre un esqueleto **mientras una
  lista carga**, y en esta aplicación **nada sabe que está cargando** —la auditoría de
  `ReviewInboxView` ya lo midió: ningún modelo de vista lleva estado de carga—. Llega con el primer
  modelo de lectura que lo informe.
- **`apr-in` NO SE HACE, y también está medida**: es la subida de 6 px en cada cambio de pantalla, y
  **el shell no cambia de pantalla**: monta las once y alterna `IsVisible`, que Avalonia no anima
  porque un control invisible no se dibuja y no hay fotograma del que partir. Conseguirla exige
  rehacer el shell alrededor de **un solo `ContentControl` cuyo contenido se sustituye**, que es un
  cambio en cómo se hospeda toda la aplicación, no una línea de marcado.

**Lo que queda del paquete de diseño, y no es poco:**

1. **Los iconos en el resto de las vistas.** `FontFamilyIcons` ya es token y el carril ya los usa; la
   lupa de la búsqueda y el `+` de añadir son traducciones vista por vista.
2. **El distintivo de película/serie sobre la portada**, que el prototipo dibuja. **DECIDIDO: no se
   hace todavía, y no por esfuerzo.** Necesita dos cadenas en singular —«Película» / «Serie»— que
   **el paquete `Cadenas nuevas` no propone**, y este árbol no inventa cadenas visibles: van en los
   dos idiomas y aprobadas, o no van. Además `RecommendationItemViewModel` **no sabe el tipo**, así
   que sería la misma omisión declarada que el año.
3. **«Añadir medios» al pie del carril. DECIDIDO: entra, y es la primera pieza de la siguiente
   sesión.** Es lo que el prototipo pone ahí y lo que más falta le hace a quien abre la aplicación con
   la biblioteca vacía. Cuesta un control nuevo con su cadena, su prueba de nombre accesible **y su
   escena de paseo en el mismo cambio** — el trinquete está en 0 y no sube.
4. **[REVOCADA el 2026-08-23 por el propietario — ver EL PLAN MAESTRO arriba.]** **El destino «Duplicados» del prototipo. DECIDIDO: no sustituye a «Copias».** Los cinco destinos de
   hoy son funciones reales de la aplicación y los duplicados viven dentro de Revisar; cambiar uno por
   otro sería quitarle la puerta a una función para dársela a una vista.

**Y una deuda de proceso, no de código: el trinquete de cobertura.** Los runs de CI del 2026-08-22
dieron **todas las suites en verde, el paseo incluido (135/135)**, y sólo falló `check-coverage`
porque cuatro archivos **mejoraron**. Cerrado el mismo día copiando el artefacto `coverage-debt`, con
**dos suelos que bajaban y no se aceptaron**:

- `RouteStateConverter` bajaba a 100/81 al perder la rama del glifo. Tenía **tres guardas que nada en
  este repositorio puede tomar**; quitadas, llega a **100/100 y sale de la lista**. El trinquete pasa
  de 217 a **216**.
- `App.axaml.cs` bajaba por dilución —dos líneas que ninguna suite ejecuta—, así que la barra de
  título se extrae a **`App.ApplyDesignedChrome`**, que sí se afirma, y con ella el **44 que vivía en
  dos idiomas** queda atado: `App.TitleBarHeight` y la primera fila de `ShellView` son el mismo
  número, y hay prueba.

### La lista de cuatro piezas, tal como se escribió (la 1 está hecha; la 2, empezada)

**El encargo sigue siendo el mismo: que la aplicación se parezca al prototipo.** Lo que queda no es
una fase, son piezas nombradas, y este es el orden por el que se gana más parecido por unidad de
riesgo:

1. **«Añadir medios» al pie del carril** — decidido arriba. Control nuevo: cadena en los dos idiomas,
   prueba de nombre accesible, línea en `LeadingActionTests` si lidera, **y su escena de paseo en el
   mismo commit**. Es lo que el prototipo pone ahí y lo primero que necesita quien abre con la
   biblioteca vacía.
2. **Recomponer el REPRODUCTOR contra el prototipo** — 17 vistas, el área más grande sin recomponer.
   Pasó por auditoría contra la §4 en el tramo 4, que no es lo mismo. El prototipo le da superficie
   propia `#0B0D10`, transporte con glifos de 44 px y la columna de 320.
3. **Ajustes, Revisión, Metadatos, Catálogo, Copias** — 16 vistas, mismo criterio.
4. **Los iconos de las demás vistas** — la lupa de la búsqueda, el `+` de añadir. Sin cadenas nuevas
   y sin controles nuevos: sólo `Content` y `FontFamilyIcons`, que ya es token.

**Lo que ninguna de esas cuatro puede hacer, y hay que pedirlo:** las **dos cadenas del distintivo de
tipo** (punto 2 de arriba) y **las portadas reales** (ART-A01), que son decisión del propietario.

**Cómo medir el parecido, y no es opinable:** `docs/design/SURFACES.es.md` lleva las 51 vistas por
área. Un área está recompuesta cuando cada una de sus vistas se ha abierto **al lado del prototipo**,
no cuando su fila de la §4 está tachada. Ver [[ap-reelume-implementar-no-auditar]].

## ⚠⚠ Por qué el plan existe: EL REDISEÑO SE ESTABA HACIENDO MAL, Y ESTO LO CORRIGE (2026-08-22, tarde)

**El encargo era que la aplicación se pareciese al prototipo de `design/`. Lo que se ha hecho durante
ocho tramos es otra cosa: auditar cada vista contra la §4 y corregir defectos.** Cuando la diferencia
con el documento era grande —«no hay portadas en toda la aplicación», «`LibraryEntryView` no es la
ficha que el documento describe», «la cuadrícula fluida no se hace»— se **registró como discrepancia**
y se pasó a la siguiente. Once veces. Eso convirtió «haz que se parezca a esto» en «documenta en qué se
diferencia».

**La prueba está en las capturas**: con la aplicación abierta al lado del prototipo, lo que se ve es
texto plano donde el prototipo tiene un héroe, carriles de fichas y un carril de iconos.

### Lo que sí está hecho y no hay que rehacer, medido el 2026-08-22

| Del paquete | Objetivo | Hoy |
| --- | --- | --- |
| Cadenas por idioma | 517 | **516** |
| Diccionarios de tema | 4 | **4** (`Light`, `Dark`, `HighContrastLight`, `HighContrastDark`) |
| Los cinco estados de control | 5 | **hechos**, por redirección de las claves Fluent |
| Anillo doble de foco | sí | **hecho**, `FocusAdornerTemplate` de dos bordes |
| Diez selectores de foco | 10 | **10**, `ToggleSwitch` y `RadioButton` incluidos |
| Punteado del deshabilitado | sí | **hecho**, en `Theme/DisabledOutline.cs` |
| Controles interactivos | 202 | **136** — faltan 66 |
| Animaciones | 4 | **0** |

Los estados están **medidos color a color** y funcionan: reposo `#e2e8f0` = `ControlFillBrush`, sobre
`#d6dfea` = `ControlFillHoverBrush`, pulsado `#c4d0df` = `ControlFillPressedBrush`, deshabilitado
`#eef3f7` = `ControlFillDisabledBrush`, y lo mismo en oscuro. **No vuelvas a medirlo.**

**⚠ Y una alarma falsa que costó una hora**: un `grep` de `{DynamicResource X}` dice que seis tokens de
estado no los gasta nadie. **Es falso.** El consumo es por sintaxis de elemento —
`<StaticResource x:Key="ButtonBackgroundPointerOver" ResourceKey="ControlFillHoverBrush" />` — y una
expresión que sólo busca llaves no lo ve.

### La decisión que faltaba, y que se toma aquí

`design/README.md` dice que los `.dc.html` «no son código para copiar» y que la implementación sigue la
§4. Pero **la §4 y el prototipo no coinciden**, y el propietario aprueba mirando el prototipo:

| | Prototipo | §4 |
| --- | --- | --- |
| Navegación | carril de **64 px** con iconos, sin texto | «Navegación de **248 px**… barra de 3 px y el glifo» |
| Destinos | Inicio · Biblioteca · Revisión · **Duplicados** · Ajustes, y **«Añadir medios»** al pie | los cinco de hoy, con Copias |

**Decisión: para la composición manda el prototipo; para tokens, estados, cadenas y accesibilidad manda
la §4.** Razón: la §4 describe retoques sobre el árbol actual y el prototipo describe el producto que
se aprobó. Donde uno pida menos que el otro, gana el que pida el diseño nuevo. Queda escrito que esto
contradice una línea del README, a sabiendas.

### El plan, en orden, y ninguna parte necesita red

**Nada de esto toca TMDB.** La §4 ya dice qué pintar sin portada: «**iniciales sobre
`ControlFillBrush`, nunca un hueco**». El prototipo tampoco usa fotos: pinta degradados.

1. ~~**La ficha 2:3** (`PosterCardView`, nueva).~~ **HECHA el 2026-08-22.** 148 × 222 —exactamente
   2:3, y el ancho al que el prototipo la aprueba en su cuadrícula—, iniciales sobre
   `ControlFillBrush` en `FontSizeDisplay` y `TextSecondaryBrush`, radio `CornerRadiusMedium`, borde
   `ShellHairlineBrush` —separa fichas, no delimita nada pulsable—, título a dos líneas y pie en
   secundario. Montada en **los tres carriles**; en la biblioteca entra con el paso 2, que es quien
   decide el panel. **Los cuatro modelos de vista implementan `IPosterCard`**, y eso es lo que hace
   que las omisiones se declaren en vez de pintarse solas: `RecommendationItemViewModel` no conoce el
   año ni la disponibilidad, así que la ficha no los inventa, y `CatalogItemViewModel` **no dibuja
   barra** porque `CatalogItem.HasProgress` dice *que* se empezó y no *cuánto* — una barra a cero para
   algo visto a medias es peor respuesta que ninguna barra. **`UnavailableBadge` se queda fuera de la
   ficha** por lo mismo: dos de los cuatro carriles saben si el medio está a mano y uno no.
   - **Y lo que la ficha destapó, que no se veía en ocho tramos verdes: Inicio se dibujaba fuera de la
     ventana.** `HomeView` daba el `*` a la fila del carril, así que «Añadido recientemente» y «Quizá
     te interese» **no llegaban a pantalla** —medido a 1600 × 1000 en la aplicación real, con las
     tarjetas viejas y con las nuevas— y el carril que sí tenía sitio enseñaba media ficha. Las seis
     filas pasan a `Auto` dentro de un `ScrollViewer`. **Coste medido**: la baseline `T30` se mueve en
     `LibraryEntryBottom` **+3 px lógicos en 12 de sus 36 registros** —los de 4K con viewport de 2160
     y 1440, que son justo aquellos en los que Inicio pasó a ser más alto que su ventana—, y
     `LibraryEntryWithinFirstViewport` sigue en `True` en los 36.
   - **Y una trampa de AXAML que costó una roja: `ProgressBar` trae `MinWidth 200` del tema base.**
     Estirada dentro de una portada de 148 salía 54 px más ancha que la imagen a la que pertenece,
     recortada por `ClipToBounds` en vez de ajustada. Es la misma forma que `MinHeight` con `Height`:
     **el setter que nombra el número no siempre es el que decide**.
   - **⚠ Y un hallazgo que NO se ha tocado y es del shell, no de la ficha:** en la aplicación real a
     1600 × 1000 el bloque «Tu biblioteca» **se dibuja más allá del borde derecho** y su borde nunca
     cierra. Ya pasaba antes de este cambio —está en la captura previa—, así que no es una regresión:
     es la primera limitación que `ViewOverflowTests` lleva escrita, «una vista demasiado ancha sólo
     una vez anidada», ocurriendo de verdad. Va con el paso 5, que es el que toca el shell.
2. ~~**La cuadrícula que reflowa y virtualiza.**~~ **HECHA el 2026-08-22, y la decisión revertida con
   su número** ([evidencia](evidence/stable/audit-poster-card-and-the-grid-that-won.md)). Diez mil
   fichas en 1600 × 1000, Release:

   ```
   VirtualizingStackPanel, una ficha por fila (lo de hoy)       13 ms        4 fichas vivas
   WrapPanel                                                  4559 ms   10 000 fichas vivas
   ListBox + VirtualizingStackPanel, filas de 9                108 ms       36 fichas vivas
   ItemsControl en ScrollViewer, filas de 9                      6 ms       36 fichas vivas
   reflujo a 1200 / 900 / 1600 px                          10 / 3 / 12 ms   28 / 20 / 36
   ```

   **760× el tiempo y 278× los controles vivos**, y el reflujo cuesta un fotograma. **Lo que faltaba
   no era un control que Avalonia no tiene: era agrupar los elementos antes de dárselos al que sí
   tiene.**
   - **⚠ Y la premisa de este punto era falsa, medida.** «`ItemsRepeater` y `WrapLayout` SÍ existen en
     12.1.1» **es falso**: una sonda de compilación contra las referencias reales del proyecto da
     `CS0234` en los dos. Y por paquete tampoco llegan —`Avalonia.Controls.ItemsRepeater` se detiene
     en **12.0.0** y `WrapLayout` **no es de Avalonia**, vive en `Avalonia.Labs.Panels`, que llega a
     12.0.2—. **Undécima alarma falsa, y de las caras: habría hecho añadir dos dependencias por
     detrás del Avalonia que el proyecto fija para resolver algo que el árbol ya podía.** Una
     presencia se prueba igual que una ausencia.
   - **El píxel vive en la vista y sólo ahí**: `LibraryView.axaml.cs` mide su superficie y le dice al
     modelo cuántas caben; `LibraryViewModel` agrupa sin saber de píxeles. Y los dos números de la
     ficha son **tokens** (`PosterCardWidth`, `PosterCardHeight`) porque los lee el marcado que la
     pinta **y** el C# que divide por ellos.
   - **La separación es una sola**: el padding de 8 del botón, a los dos lados, es todo el hueco. La
     primera medición de esta cuadrícula falló exactamente ahí — contó ocho columnas en 1352 px y
     dibujó la octava **72 px fuera**.
   - **Y una prueba desaparece porque la sustituye otra**:
     `LibraryNavigationTests.The_library_realises_a_handful_of_rows_out_of_ten_thousand` medía la
     virtualización de la lista de una columna y **nombraba la solución en su propio comentario**
     («agrupar en filas en el modelo de vista y dejar que el panel virtualice filas»).
     `LibraryGridTests` lo afirma sobre la ficha real, que es más.
3. ~~**Los tres carriles** con tarjetas.~~ **HECHOS con el paso 1**, que es de donde salieron: los
   tres montan `PosterCardView`, la barra de 3 px va **al pie de la portada** —que es donde la §4 la
   pide— y los tres `ListBox` pierden su caja (`ListBox.poster-rail`: un carril es una fila de fichas
   sobre la superficie del shell, no una lista dentro de un marco).
4. ~~**El héroe de Inicio** (`ResumeHeroView`).~~ **HECHO el 2026-08-22.** Antetítulo en `FontSizeCaption`
   y `TextSecondaryBrush`, título en `FontSizeDisplay` con peso **Light** —el prototipo lo escribe a
   52 px y peso 300, y la escala llega a 32; un `FontSizeHero` con un solo lector es la forma por la
   que se rechazó `FontSizeMono`, así que la diferencia la carga el peso y el espacio—, metadatos en
   una línea, barra de 300 × 3 con el porcentaje en palabras, y la acción en `WrapPanel`. Sin nada que
   reanudar **el héroe no se pinta**, que ya era así.
   - **Y dos cosas del prototipo que NO lleva, las dos por una razón medida y no por alcance.**
     **La portada**: unas iniciales junto a un título ya escrito a 32 px dicen dos veces lo mismo —las
     iniciales sirven en una rejilla, donde distinguen una ficha de la siguiente—; vuelve con portadas
     de verdad. **El botón «Detalles»**: abrir la ficha de un título pide un `CatalogItem`,
     `ResumeItem` **no lleva ni el año ni la disponibilidad** que ese record tiene, y ningún modelo de
     lectura contesta «dame el elemento con este id» —`ICatalogQueryService` consulta páginas con
     filtros—. Construirlo con lo que el héroe sabe sería **inventarse dos campos**. Es una vertical
     por tres capas para un botón secundario.
   - **Coste medido en la baseline `T30`**: el héroe crece **122 px**, así que `LibraryEntryBottom` se
     mueve en los 36 registros y en **6** —1366 × 768 al 200 %, viewport de 384 px lógicos— el bloque
     de biblioteca deja de caber en la primera pantalla. **Se aprueba en vez de esconderse**: el héroe
     del prototipo mide **398 px**, así que nada que se parezca al diseño aprobado cabe sobre un
     pliegue de 384, y desde el paso 1 Inicio se desplaza.
5. ~~**La barra de título propia y el carril de navegación.**~~ **HECHOS el 2026-08-22.** El carril
   pasa de **248 px de palabras a 64 px de pictogramas**; los destinos son 46 × 42 con radio
   `CornerRadiusMedium` —el prototipo dibuja 12 y esta escala tiene dos radios a propósito—, y el
   abierto lleva **relleno + barra de 3 px**: dos señales, y la barra existe o no existe, así que una
   no es color. La barra de título propia mide 44 px y hace de la ventana una sola superficie.
   - **Ningún control nuevo, y el paseo lo confirma: 135 identidades, 0 pendientes.** Sólo cambió el
     `Content`; `AutomationProperties.Name` sigue apuntando a la misma clave, que es lo que el paseo
     persigue y lo que un lector anuncia. Y cada destino gana `ToolTip.Tip` con su palabra, porque un
     pictograma que nadie ha visto antes no dice qué es.
   - **`ExtendClientAreaChromeHints` NO existe en Avalonia 12.1.1**: quedan
     `ExtendClientAreaToDecorationsHint` y `ExtendClientAreaTitleBarHeightHint`, y nada más. Windows
     sigue pintando minimizar, maximizar, cerrar **y el título** sobre el área extendida — medido con
     la aplicación abierta—, así que la barra propia **no repite el nombre**: lo dibujaba dos veces,
     solapado. Lleva la firma del editor a la derecha.
   - **Y con eso el nombre del producto deja de ser un encabezado de nivel 1.** Lo era en el carril
     mientras cada pantalla declaraba el suyo: dos H1 a la vez, que es exactamente lo que se corrigió
     en la página de Ajustes. El encabezado de una pantalla es el nombre de la pantalla.
   - **Dos piezas que quedan fuera, dichas**: el destino **«Duplicados»** del prototipo, porque los
     cinco de hoy son funciones reales y `Copias` no se sacrifica por una vista que vive dentro de
     Revisar; y **«Añadir medios» al pie del carril**, que sería un control nuevo con su cadena, su
     prueba de nombre y su escena de paseo.
   - **Y una regla que vivía dos veces, otra vez.** `ShellLocalizationTests` exigía que **todo**
     `Text`/`Content` del shell empezara por `{` — más estricto que el árbol, que desde el 2026-08-22
     dice «un literal pasa sólo si no contiene ninguna letra» sobre las 51 vistas. Saltó con los cinco
     pictogramas. Ahora dice lo que protege: el shell no pinta palabras que no haya traducido.
6. ~~**Los iconos**, glifos de Segoe Fluent Icons.~~ **HECHOS con el paso 5** para el carril:
   `FontFamilyIcons` se declara como token —dos consumidores, el cromo del reproductor y el carril—
   y `Button.player-chrome` deja de escribir la lista de fuentes a mano. **Lo que queda**: los SVG del
   prototipo en las demás vistas, que es una traducción vista por vista.

### La geometría del prototipo, ya medida — no la vuelvas a extraer

Servido con `python -m http.server 8765 --directory design` y medido en el navegador a 1600×1000:

```
barra de título     1600 x 44,  padding-left 14, gap 10
carril               64 de ancho; destinos de 46 x 42, radio 12, separación 6
                     cinco arriba (y=50, 98, 146, 194, 242) y uno al pie (y=944)
                     activo: fondo rgba(127,145,170,.16) + barra a la izquierda
contenido            empieza en x = 96
héroe: título        52 px, peso 300
héroe: primario      alto 44, radio 999, padding 0 26, gap 9, fondo claro sobre oscuro
héroe: secundario    alto 44, radio 999, padding 0 22, fondo blanco al 6 %
tarjeta de carril    281 x 268, radio 12; arte 16:9 de 279 x 157
ficha de portada     133 x 244, gap 8; arte 2:3 de 133 x 200, radio 10
fondo de la app      #0B0D10
```

`design/support.js` es el **runtime** de Claude Design —parsea `<x-dc>` y monta React—, no una fuente de
diseño. La plantilla y los estilos están en el `<x-dc>` de `AP Reelume.dc.html`.

### Lo que queda FUERA, y es decisión del propietario

**Las portadas de verdad.** `ArtworkCache` está entero —descarga, allowlist, techo de 10 MB, texto
alternativo— y **fuera del contenedor a propósito** desde el 2026-08-09 (ART-A01), porque el MVP se
publica **sin token de TMDB**: «No token, no connection… the shipped artifact carries none». Y
`CatalogItem` no tiene ningún campo de imagen. Traer portadas reales es una vertical por las cinco capas
más superficie de red. **No la abras sin que el propietario lo pida.** El diseño se implementa entero
sin ella.

## Estado al abrir (2026-08-22)

**`main` en `44f72ca`, verde; el tramo 7 CERRADO, y quedan el 8 y el 9.**

**Del 8, `UpdateView` CIERRA SIN TOCARLA, y está medido** ([evidencia](evidence/stable/audit-update-and-player-grammars.md)):
sus **14 estados + 7 rechazos + el aviso** ya llevan las cuatro gramáticas, y el mapa de los **diez**
valores de `UpdateRejection` contra las **siete** cadenas **cierra entero**. Los tres que parecían
huecos no lo son: `NoReleaseAvailable` y `NotNewer` son `UpToDate` a propósito —«no tener nada más
nuevo y haber preguntado a una fuente que no publicó nada son la misma noticia»—, y `UndeclaredHost`
**no sale de una comprobación**: sólo lo emite `VerifiedUpdateDownloader` al descargar, y llega por el
`catch`. Décima alarma falsa apagada midiendo.

**Y lo que SÍ queda del 8 es del reproductor, y es más grande que su fila.** `CanChooseAnotherVersion`
se decide **sólo por el código de fallo**, sin mirar si existe otra versión — al contrario que sus dos
hermanas, que sí comprueban que la acción se pueda ejecutar y son las dos que tienen botón. **En el
caso más común, un archivo sin otras versiones, la pantalla decía «Elige otra versión del mismo
contenido» a quien sólo tiene una**. **Corregido el mismo día**: ahora exige la acción del dominio **y**
que exista otra versión, preguntado con un `Func<bool>` y no guardado, porque `player` se construye
antes de leer el grupo.

**⚠ Y el botón que la §4 pide NO se hizo, con la medición delante.** `ShellView.axaml` pone `PlayerView`
en `Grid.Column="0"` y `PlayerVersionsView` en la columna lateral de 320: **las dos en pantalla a la
vez**, y con la misma condición que enciende la frase. El botón llevaría a un sitio que ya se ve desde
donde estaría el botón. Segunda razón medida: **`PlayerStage` viaja a la ventana del mini** —
`host.Content = stage` al volver— y `PlayerView` va dentro, así que el bloque de fallo puede verse **sin
columna al lado**; por eso la frase nueva tampoco dice dónde. Si alguien decide hacerlo igualmente,
**cuesta una escena de paseo que no existe**: hace falta un fallo de reproducción **y** un grupo de
versiones a la vez, y el paseo tiene las dos cosas por separado.

El 9 son las cuatro animaciones (`apr-in`, `apr-shim`, `apr-tip`, `apr-pulse`), que siguen a **0 en el
árbol**.

**⚠ Y el hallazgo del tramo 7 que no es de ninguna vista: un control que se ve y no se puede pulsar.**
`RootOnboardingView` creció 25 px y el paseo físico se puso en rojo **determinista** — 4 s en verde con
el árbol limpio, 66 s en rojo con el cambio. Medido dentro del paseo, en su ventana de 1600 × 1000: el
botón «Revisar versiones» quedaba en y=939 con 36 de alto, y el viewport del `ScrollViewer` acababa en
952. **Trece de sus treinta y seis píxeles dentro, y su punto medio fuera**, así que el clic llegaba al
`Grid` de detrás. El desplazamiento disponible eran 23 px para un control que necesitaba 23. El
`StackPanel` de la ruta de biblioteca gana `Margin="0,0,0,24"` — el colchón es el derecho de un
control a ser pulsado ([evidencia](evidence/stable/audit-root-onboarding.md)).

**⚠⚠ Y el margen NO era la causa: el arnés estaba ciego.** Con el margen puesto, la suite completa
seguía cayendo **una de cada dos veces**. `Fits` —la función con la que `Reveal` decide si hace falta
desplazar— preguntaba **sólo por la ventana**, y un `ScrollViewer` recorta su contenido: el botón
estaba dentro de la ventana de 1000 y cinco píxeles fuera del viewport que acaba en 952. Contestaba
«cabe», no se desplazaba nada, y las ocho pulsaciones iban a lo que el recorte dejaba detrás. Es
**«una prueba se vuelve ciega en vez de falsa»** otra vez. Ahora exige además el viewport de **cada**
visor entre el control y la ventana, lo que **endurece** la puerta. Tres pasadas completas seguidas:
135/135 las tres, ledger en 0 pendientes.

**Y lo que esa medición destapó y NO se ha tocado**: `HasOnboarding` es `Onboarding is not null`, así
que **nunca es falso**, y la vista de carpetas ocupa **426 px de los 904 del viewport**, siempre,
encima de la biblioteca de quien ya tiene quinientas películas. SURFACES le pide gramática **ausente** a
sus cuatro formas y la vista entera no es ausente nunca. Eso es el shell, no la vista, y es una pieza
con su propio alcance: `ShellAssemblyTests` afirma `HasOnboarding` en cuatro sitios.

De `RestoreWizardView` quedan anotadas dos cosas de su fila: los **pasos numerados**, que son una
reorganización de la vista entera y no una fila; y el **estado vacío**, que hay que medir antes porque
«sin raíces que reasignar» no es «la lista está vacía» sino «ninguna fila pide nada»
([evidencia](evidence/stable/audit-restore-roots.md)); `BackupView` queda hecha salvo **el bloque de ruta de la
base**, que la §4 pide y `BackupViewModel` no puede dar: **no conoce ninguna ruta**, así que traerlo es
cablear `IAppDataPaths` hasta él y es una pieza con su propio consumidor
([evidencia](evidence/stable/audit-backup-status.md)).

**⚠ LA FORMA QUE MÁS HA COSTADO ESTA TANDA: la guarda que nada puede tomar, CUATRO veces.** El
converter de marcadores, el de recursos, las notificaciones de `BackupViewModel` y el `StatusKey` de
`RootRemapRowViewModel`. Las cuatro las encontró **la cobertura, antes que el razonamiento**, y una de
ellas llegó a poner CI en rojo por bajar un suelo. **Al añadir una condición delante de algo que ya
decidía, pregunta qué camino acabas de dejar muerto** — y míralo con `--collect:'XPlat Code Coverage'`
antes de empujar, que cuesta un comando.

**⚠ Y una regla que vivía DOS VECES, unificada el 2026-08-22.** «Ningún literal en esta vista» estaba
copiada en `BackupViewTests` y en `LifecycleSettingsTests`, cada una vigilando su propio archivo, y
**las dos saltaron con el mismo glifo `⚠` en dos piezas seguidas**. Cubrían dos vistas de cincuenta y
eran **más estrictas que el árbol**. Ahora es `ViewLiteralTests`, una sola, sobre todos los `.axaml` de
`src/`, enunciada como lo que protege: **un literal pasa sólo si no contiene ninguna letra**. Medido
antes de escribirla: cero literales con letra en las cincuenta vistas.

Antes de esas dos: `DatabaseRecoveryView`
([evidencia](evidence/stable/audit-database-recovery.md)) y `CreditsView`
([evidencia](evidence/stable/audit-settings-sections-blind-spot.md)).

**⚠ Y una puerta propia que era ciega, corregida el 2026-08-22.**
`SettingsPageStructureTests` buscaba las secciones de Ajustes **por el nombre de su clase**
(`EndsWith("SettingsView")`) y **tres de las diez no se llaman así** —el estilo de subtítulos y los
atajos viven en `Player/`, el actualizador en `Updates/`, los créditos en `About/`—, así que medía
siete, las encontraba consistentes y pasaba mientras tres empezaban 158 px más a la izquierda. **Una
convención de nombres no es una estructura.** El panel se llama ahora `SettingsSections`, la prueba lo
recorre y afirma el recuento primero. **Vale para cualquier puerta que agrupe: identifica por dónde
está montado, no por cómo se llama.** La fase 6 va por **5 tramos
de 9 cerrados**
—Shell, Inicio, Biblioteca y fichas, el Reproductor entero y **Ajustes**—:
`AppearanceSettingsView` ([su evidencia](evidence/stable/audit-appearance-page.md)), las **tres del
«mismo esqueleto»** ([su evidencia](evidence/stable/audit-settings-skeleton.md)) y **la estructura de la
página entera** ([su evidencia](evidence/stable/audit-settings-page-structure.md)) están hechas.

**⚠ Y esa tercera CORRIGIÓ a la segunda el mismo día**, que es la lección más cara del tramo: la §4
describe cada vista en su propio artboard, así que «título 28» se leyó como «cada una es una página». No
lo son — **las siete están apiladas en un solo `ScrollViewer`**—, y aplicarlo puso cuatro encabezados de
nivel 1 en una página y un escalón de 158 px por el medio. **Una decisión sobre jerarquía se mide sobre
la pantalla ensamblada**, que es la mitad que una tabla por vista no puede ver. Es la segunda vez que
esta regla se cobra algo: la primera fue `LibraryEntryView`.

~~**Quedan dos piezas del tramo 5: `LifecycleSettingsView` y `DiagnosticsPreviewView`.**~~ **HECHAS el
2026-08-21 ([su evidencia](evidence/stable/audit-settings-notices.md)), y con eso EL TRAMO 5 CIERRA.**
La §4 va por **5 tramos de 9**. `PrivacySettingsView` cerró **sin tocarla**, y está medido: sus dos gramáticas ya conviven
—`CanRefreshAutomatically` gobierna un `IsVisible` y `DiagnosticsEnabled` un `IsEnabled`—, **un hijo
invisible no deja hueco** (medido: el hermano siguiente sube de y=72 a y=36, así que el `Spacing` del
`StackPanel` salta los invisibles), el **contorno punteado ya llega** a un botón deshabilitado (medido:
`DisabledOutline.IsShown` pasa de `False` a `True`), y **la lista de hosts no puede estar vacía**:
`NetworkPurposeRegistry.Declared` declara **4** y es estática. El estado vacío que la §4 le pide **no lo
puede ver nadie** — séptima discrepancia §4↔árbol.

**Lo siguiente, sin nada que deliberar: el tramo 6 — Revisión, Metadatos y Catálogo (7 vistas)**, cuya
fila está en la tabla de abajo. Lo de más calado que pide: **la bandeja vacía de `ReviewInboxView` es el
estado deseable**, así que se pinta en `PositiveSurfaceBrush` con glifo y no como un vacío triste; y
`CandidateCardView` lleva portada de 92 px — **que no existe**, porque no hay portadas en toda la
aplicación y eso está decidido fuera de 0.2.0.

**Antes de escribir nada, mide la vista contra su fila de la §4.** La regla se ha pagado sola **las
cinco veces**, y en el tramo 4 encontró de golpe el peor defecto medido hasta hoy: **las dos listas del
lateral pintaban el `ToString()` de un record de dominio**, dos GUID y el nombre de la clase, a la
vista de cualquiera.

**⚠ Y el tramo 4 estuvo a punto de cerrarse con una vista sin mirar.** Esta misma sección decía que le
quedaban **dos** piezas y no nombraba `LooseFileBanner`; **su viñeta nunca se había tachado**. Al
medirla: sí estaba superpuesta al vídeo, y además **viajaba a la ventana del mini reproductor** dentro
de `PlayerStage`, pidiendo 336 px de alto en una ventana de 270. **Lo que cierra un tramo es su lista
de vistas, no la frase que lo resume** — la misma forma con la que el 2026-08-20 se declaró cerrado el
paso 6 contra un resumen propio.

### Lo que la sesión del 2026-08-21 dejó, y no está sólo en los commits

**Cinco defectos que ninguna suite podía ver**, todos por medir la fila de la §4 contra el árbol
**antes** de escribir: los títulos recientes que se leían de SQLite y no pintaba nadie; el carril de
recomendaciones apagado afirmando que no había nada que sugerir sobre un catálogo que no se había
leído; el distintivo de «no disponible» copiado a mano en seis vistas; la búsqueda sin resultados que
no decía ni una palabra; y **la vista previa del subtítulo, que previsualizaba una cosa de cinco**.

**Y uno que se creía cerrado y estaba a medias:** los tres paneles superpuestos que el paseo cazó el
2026-08-17 se arreglaron con alineación explícita, y **centrar no impide crecer**: con una frase larga,
uno ocupaba **1278 px de un escenario de 1280**. La §4 pedía el `MaxWidth` que faltaba, y tenía razón.

**⚠ Y UN ERROR MÍO QUE LLEGÓ A ESTE DOCUMENTO Y HAY QUE NO REPETIR.** Escribí que «la columna fija de
320 px no existe en absoluto». **Existe y siempre existió**: la monta `ShellView.axaml` con
`ColumnDefinitions="*,320"`. Lo escribí buscando `320` en `PlayerView.axaml`, que es la vista del vídeo
y no la que monta la columna. **Un grep vacío en el archivo equivocado se lee igual que una ausencia**:
antes de escribir «esto no existe», pregunta **quién lo montaría** y mira ahí.

### Las cuatro discrepancias medidas entre la §4 y el árbol

Ninguna se improvisó y todas tienen su número:

1. **No hay portadas en toda la aplicación** (cero `<Image>` en `src/`). **Decidido: no entran en
   0.2.0**, con su razón abajo. **Y desde el 2026-08-22 eso ya no impide la ficha**: la §4 dice qué
   pintar sin portada —«iniciales sobre `ControlFillBrush`, nunca un hueco»— y eso es lo que
   `PosterCardView` pinta. La discrepancia sigue siendo cierta y ha dejado de bloquear nada.
2. ~~**La cuadrícula fluida de la biblioteca no se hace.**~~ **REVERTIDA el 2026-08-22 con su
   número**: `ItemsControl` en un `ScrollViewer` sobre filas agrupadas cuesta **6 ms y 36 controles
   vivos** contra los **4559 ms y 10 000** del `WrapPanel`. Y las dos mitades de la razón original
   eran falsas: no iba «con las portadas» —la ficha de iniciales es contenido suficiente— y
   `ItemsRepeater`/`WrapLayout` **no existen** en 12.1.1, que era lo que se buscaba y no lo que hacía
   falta.
3. **`LibraryEntryView` no es la ficha 2:3 que el documento describe**, sino el bloque de entrada a la
   biblioteca. **Sigue siendo cierto y ya no falta nada**: la ficha existe y es `PosterCardView`, así
   que lo que la fila de la §4 tiene mal es el **nombre de la vista**, no la pieza.
4. **Los datos del distintivo de vídeo conservan su caja**, aunque la §4 los quiera sin ninguna: ese
   distintivo **flota sobre la película**, así que un texto sin superficie se lee contra un fotograma
   arbitrario y no hay contraste que garantizar ni medir.

### Lo que cuesta cada tramo, para presupuestarlo

- **Dos vueltas de CI**, siempre. El trinquete de cobertura falla **también cuando algo mejora**, así
  que toda pieza con prueba nueva pide declarar su suelo en una segunda vuelta. Pasó cuatro veces.
- **⚠ Y un `.cs` NUEVO en `src/` no tiene deuda posible: 96/96 o rojo.** El trinquete de
  `eng/coverage-debt.txt` es para archivos que ya existían; para uno añadido contra `origin/main` la
  puerta dice `New files below 96% lines / 96% branches` y **no hay lista de excepciones** (sólo exime
  que el contenido ya existiera en la base, ≥85 %, que es un movimiento). Mídelo **antes** de empujar:
  `dotnet test <suite> --results-directory X --collect:'XPlat Code Coverage'` y lee `line-rate` y
  `branch-rate` de esa clase en el cobertura.
- **Y la causa típica no es que falten pruebas: es una guarda que nada puede tomar.** El converter del
  tramo 4 dio 100 % de líneas y **4 de 8 ramas de una sola línea**, y las cuatro eran defensivas
  (`Application.Current is not null` en algo que construye el AXAML, un `?.ToString() ?? key` sobre un
  recurso que siempre existe). **La respuesta es quitar la guarda, no escribirle una prueba
  imposible.**
- **Una vista nueva cuesta además una entrada en `eng/coverage-debt.txt`**, porque todos los `.axaml`
  miden 100/50, y **el trinquete no sube**: se paga sacando otro archivo de la lista, mejorándolo.

### La fase 6, área por área, en el orden de la §4

**El orden es el de la propia §4, que coincide con `SURFACES.es.md`.** Cada fila de abajo es un tramo
de trabajo; **la unidad de commit es la vista**, salvo donde la §4 agrupa varias bajo el mismo cambio
(las cuatro listas del reproductor, los tres paneles superpuestos, los tres ajustes de mismo esqueleto).

| # | Área | Lo que la §4 pide de más calado |
|---|---|---|
| ~~1~~ | ~~**Shell** (2)~~ **HECHA el 2026-08-20, y REHECHA el 2026-08-22** | [Su evidencia](evidence/stable/audit-shell-navigation-bar.md). Entonces: los 248 px y el glifo **ya estaban**; se añadió la barra de 3 px —que **existe o no existe**, no se atenúa— y `TitleActionsSurface` pasó a `WrapPanel`. **Ahora el carril es de 64 px con pictogramas y la ventana dibuja su propia barra de título**, que es la composición del prototipo; la barra de 3 px se queda y el `● / ○` se va con las palabras. |
| ~~2~~ | ~~**Inicio** (5)~~ **HECHA el 2026-08-20** | [Su evidencia](evidence/stable/audit-home-tranche.md). Los tres estados del carril, la barra de 3 px al pie y `Space24` en la rejilla. **Dos discrepancias con la §4**: no hay portadas en toda la aplicación (0 `<Image>` en `src/`) y `LibraryEntryView` **no es la ficha que el documento describe**. Y el hallazgo: `RecentlyAdded` se leía de SQLite y no lo pintaba nadie, así que Inicio gana una vista, `RecentlyAddedRailView`. |
| ~~3~~ | ~~**Biblioteca y fichas** (5)~~ **HECHA el 2026-08-20** | Sus evidencias: [el distintivo](evidence/stable/audit-unavailable-badge.md), [las filas y el peaje](evidence/stable/audit-wrapping-rows-and-the-ratchet-toll.md), [sin resultados y la fila de episodio](evidence/stable/audit-library-no-results-and-episode-row.md), [el botón de borrar](evidence/stable/audit-library-clear-search.md) y [el selector y la cuadrícula](evidence/stable/audit-season-picker-and-the-grid-that-lost.md). **La cuadrícula fluida NO se hace, y está medido**: `WrapPanel` cuesta 7× el tiempo y 455× los controles vivos, y en Avalonia 12.1.1 no existe nada que reflowe y virtualice a la vez. |
| 4 | **Reproductor** (16) | Superficie propia `#0B0D10` y columna fija de 320 px; el fallo pasa a `DangerSurfaceBrush` con glifo; `VideoStatusOverlay` **partido en dos gramáticas** (dato vs aviso); los tres superpuestos con **alineación explícita y `MaxWidth 420`** — es la forma que causó el panel de 1280×1400; las cuatro listas a filas de 36 px sin scroll horizontal. |
| 5 | **Ajustes** (7) | **Medido el 2026-08-21 sin escribir código, abajo.** Los 3 botones de tema **no pasan a 5** —`ThemePreference` tiene tres y el árbol lleva escrito por qué—; el `StackPanel` horizontal sí pasa a `WrapPanel`, pero **por la otra razón**. Las tres vistas del «mismo esqueleto» **no lo comparten**: cuatro titulan con `FontSizeSubtitle` y una con `FontSizeTitle`. `PrivacySettingsView` debe **distinguir ausente de deshabilitado**, y las dos gramáticas ya están localizadas en ella. |
| 6 | **Revisión, Metadatos, Catálogo** (7) | La bandeja vacía es **el estado deseable**: `PositiveSurfaceBrush` con glifo, no un vacío triste. |
| 7 | **Copias, Primeros pasos, Recuperación, Créditos** (5) — `DatabaseRecoveryView` **hecha** | `RestoreWizardView`: sólo la raíz ausente gana campo editable y su estado pasa a «Reasignada» al escribir; **se elimina el «Restaurar» duplicado siempre habilitado**. `DatabaseRecoveryView` no gana ruta desde el shell. |
| 8 | **`UpdateView` y `PlayerView`** | Ya tienen su maqueta; les falta **la gramática de sus mensajes**: 23 en cuatro gramáticas, y 6 motivos de fallo con acciones **condicionadas por motivo** (`CanChooseAnotherVersion` es un flag **independiente**). Y `PlayerRecoveryChooseAnotherVersion` **pasa de `TextBlock` a `Button`**: es el único cambio de tipo del paquete. |
| 9 | **Las cuatro animaciones** | `apr-in`, `apr-shim`, `apr-tip`, `apr-pulse`, más la transición de la manija. El conducto ya existe (`IReducedMotionService` → `MotionDuration`); **movimiento reducido las lleva a 0 ms, no las acorta**. Hoy no hay ni un `<Animation>` en el árbol. |

**Tres reglas que valen para las nueve filas, y ninguna es opcional:**

1. **Un control nuevo llega con su prueba de nombre accesible y su línea de paseo EN EL MISMO CAMBIO.**
   El trinquete está en 0 y la puerta rechaza lo que no case.
2. **Una cadena nueva va en los dos idiomas o no va.** Son **47** —22 de estado vacío y 25 de
   consecuencia— y están en `design/Cadenas nuevas`, con su texto en los dos. Las de consecuencia se
   aprueban una a una contra la regla del propio paquete: **si ayuda a decidir o a actuar se traduce; si
   explica por qué está diseñada así, es comentario del AXAML**.
3. **No se reescribe la etiqueta de un botón existente.** `Content` y `AutomationProperties.Name`
   apuntan a la misma clave, así que reescribir una etiqueta **es renombrar el control** y rompe el
   paseo. El paquete declara **0 renombrados** a propósito.

#### El tramo 6 (Revisión, Metadatos, Catálogo), medido el 2026-08-21 sin escribir código

**CERRADO el 2026-08-22.** Siete vistas, y lo primero que apareció fue el peor defecto medido en toda
la fase 6.

**⚠ Y una roja conocida que NO es del código, medida el 2026-08-22:** la escena del paseo
`A_session_that_will_not_open_is_handed_over_and_retried_with_the_mouse` falló una vez con
`stopped=True failed=False` sobre el archivo de dos bytes, y **al repetirla sola dio verde, y la suite
entera 135/135**. Con dos bytes LibVLC unas veces falla al abrir y otras para, según el sondeo de
demultiplexores. **No se ensancha la espera para aceptar `stopped`**: la escena existe para comprobar
la pantalla de recuperación, y aceptar «paró» la haría pasar sin que esa pantalla apareciera nunca —
una prueba se vuelve ciega antes que falsa. **CI corre el paseo dos veces, así que puede salir allí.**

- ~~**`CandidateCardView` pinta códigos internos en crudo**~~ **HECHO el 2026-08-21**
  ([su evidencia](evidence/stable/audit-explanation-codes.md)). `ExplanationCodes` llevaba rutas con
  puntos —`Identification.Error.KindConflict`, `Identification.Signal.Title`— y la plantilla las pintaba
  con `Text="{Binding}"`: **once códigos en el dominio y cero cadenas** para ellos. Era el corazón de la
  pantalla contestando con un espacio de nombres, y `ExplanationSummary` las recitaba además por
  `HelpText`. Tres cosas que valen para lo que queda:
  - **La clave ES el código.** Una transformación sería un segundo sitio donde se escribe el mismo
    nombre, y los dos divergirían al primer renombrado.
  - **La puerta recorre `src/` buscando el literal**, como la de hosts de red no declarados, así que el
    duodécimo código no puede nacer crudo. Afirma primero que el barrido encontró once: un barrido
    vacío recorre el bucle sin medir nada.
  - **`ResourceKeyConverter` aprendió a resolver una lista**, para que el `HelpText` se una en el
    converter y no en un modelo de vista — resolver un recurso necesita la aplicación y su variante.

**Y el título de la ficha es `StableKey`**, la clave estable del candidato, tanto en `Text` como en
`AutomationProperties.Name`. Hay que medir qué contiene antes de decidir: si es un identificador, la
ficha se titula con un identificador.

**Lo demás de la fila, medido vista por vista:**

| Vista | Lo que la §4 pide | Lo que hay |
|---|---|---|
| ~~`ReviewInboxView`~~ **HECHA** ([evidencia](evidence/stable/audit-review-inbox-empty.md)) | bandeja vacía en `PositiveSurfaceBrush` con glifo — **es el estado deseable** | `IsEmpty` llevaba en el modelo desde siempre **sin un lector**, y los dos pinceles `Positive*` estaban declarados en los cuatro temas **sin gastarlos nadie**. El estado «cargando» **no se hace**: nada en el modelo sabe que lo está |
| `CandidateCardView` | portada 92 px + título + año + puntuación, acciones en `WrapPanel` | **no hay portada** (decidido fuera de 0.2.0) y **no hay acciones en la ficha**; su borde usa `SystemControlForegroundBaseMediumBrush`, una clave del tema Fluent y no `ShellBorderBrush` |
| ~~`DuplicateReviewView`~~ **HECHA** ([evidencia](evidence/stable/audit-duplicate-review.md)) | `UniformGrid` de 2 columnas, la diferencia en monoespaciado, vacío con cadena nueva | Dos columnas y monoespaciado hechos, y el `Border` gana su pincel. **El vacío se rechaza**: `GroupMediaVersions` lanza con menos de dos versiones y la vista sólo se monta con grupo, así que esa cadena no la vería nadie — décima discrepancia |
| ~~`MetadataEditorView`~~ **HECHA** ([evidencia](evidence/stable/audit-metadata-editor.md)) | los 3 mensajes a bloques con glifo: conflicto y sin identificar en `WarningSurfaceBrush`, sin respuesta como dato neutro | **No pueden solaparse**: los tres salen del mismo `result.Outcome` en el mismo método — novena discrepancia §4↔árbol, y de la buena. Se separan igual, porque la garantía vive en un método privado. Y la medición encontró **ocho `TextBox` sin etiqueta a la vista**, con sus ocho cadenas ya existentes |
| ~~`RenamePreviewView`~~ **HECHA** ([evidencia](evidence/stable/audit-rename-preview.md)) | origen y destino monoespaciados; el `→` se queda con su nombre accesible | Las dos rutas se truncaban por el **final**, que es el nombre del archivo — lo único que cambia. Van a `PathSegmentEllipsis`, que quita del medio. **`FontFamilyMono` se declara por fin**: tres consumidores. ⚠ **Queda `RenameConflict.Detail`**, que pinta una frase inglesa de `SafeFileRenamer` o una ruta pelada, mientras `RenameConflictKind` no lo pinta nadie |
| ~~`PersonalActionsView`, `WatchStatusControl`~~ **HECHAS** ([evidencia](evidence/stable/audit-state-glyphs.md)) | `○ ◐ ●` se quedan y ganan el tamaño óptico de los glifos Fluent | Medido: un glifo Fluent llena su caja em (14×14 a tamaño 14) y el círculo mide **9 — un 64 %**. Pasan a `FontSizeSubtitle`, que da 13 (93 %) y es un escalón que la escala ya tiene. **Trece bloques y no tres**: los cinco del carril y las cinco píldoras también |

**⚠ Y una decisión que hay que tomar en este tramo y no antes: el monoespaciado.** La §4 lo pide aquí
**dos veces** (duplicados y renombrado) y ya se gastó una en `DiagnosticsPreviewView`, que lo lleva
literal (`"Consolas,Cascadia Mono,monospace"`). Con **tres consumidores**, una familia declarada deja de
ser el defecto de la casa y pasa a ser una escala que el árbol pide — que es exactamente el criterio de
`Space12`. **Ojo: lo que se rechazó fue `FontSizeMono` (un tamaño para un solo consumidor), no una
familia.** Son cosas distintas y conviene no confundirlas al releer esto.

#### El tramo 5 (Ajustes), medido el 2026-08-21 sin escribir código

**Siete vistas, y lo primero que la medición dice es que la fila de la §4 se equivoca en su cabecera.**

**⚠ HECHO el 2026-08-21 ([su evidencia](evidence/stable/audit-appearance-page.md)): los 3 botones de
tema NO pasan a 5, y la razón está escrita en el árbol con su porqué.**
`ThemePreference` tiene **exactamente tres** valores, y `Theme/ThemePreference.cs` lleva la decisión
escrita encima de los dos de alto contraste: «*son un estado, no una cuarta elección: las tres píldoras
de Apariencia se quedan como están, y cuál de estas se aplica se lee del sistema en vez de elegirse*».
El alto contraste de Windows **es un ajuste de accesibilidad del sistema**; una aplicación que ofrece su
propio selector o lo ignora o lo duplica. **Sexta discrepancia §4↔árbol, y manda el árbol** — y aquí no
por medición sino porque **la decisión contraria ya estaba tomada y razonada**.

**Y con eso se cae también el argumento del `WrapPanel`** («cinco no caben en 620 px»). Pero **la forma
sigue siendo la correcta por el otro motivo**, el que este árbol lleva medido ocho veces: un
`StackPanel` horizontal ofrece anchura infinita. Hay **dos** filas así en la vista —tres píldoras de
tema y dos de idioma—, y sus etiquetas cambian de largo con el idioma. Medir si caben en 620 px **en
los dos idiomas** antes de decidir.

**Lo que sí falta en `AppearanceSettingsView`, medido:** el aviso de movimiento reducido es **una frase
estática** («AP Reelume respeta la preferencia…») que no dice si está activo. `IReducedMotionService`
existe, está registrado y lo consume `FluentThemeService`: **la aplicación conoce el estado y no lo
pinta**. La §4 pide ese aviso «activo o no».

**⚠ HECHO el 2026-08-21 ([su evidencia](evidence/stable/audit-settings-skeleton.md)): el «mismo
esqueleto» de las tres vistas no era el mismo esqueleto, y `ScanSettingsView` tenía además un
`NumericUpDown` cuya etiqueta sólo oía un lector de pantalla.**

| Qué | `AppearanceSettingsView` | `ScanSettingsView` | `RecommendationSettingsView` | `SegmentDetectionSettingsView` |
|---|---|---|---|---|
| Título H1 | `FontSizeTitle` | `FontSizeSubtitle` | `FontSizeSubtitle` | `FontSizeSubtitle` |
| `Padding` del contenedor | 32 | **ninguno** | **ninguno** | **ninguno** |
| Anchura de los controles | `MaxWidth 620` | **ninguna** | `640` en los textos | **ninguna** |

`LifecycleSettingsView` también titula con `FontSizeSubtitle`. **Cuatro páginas de ajustes cuyo H1 es
más pequeño que el de la quinta**: eso es lo que la §4 llama «mismo esqueleto» y no lo es.

**`PrivacySettingsView`: las dos gramáticas conviven, y está medido dónde.** `CanRefreshAutomatically`
gobierna un `IsVisible` (**ausente**) y `DiagnosticsEnabled` un `IsEnabled` (**deshabilitado**), en la
misma vista. **Antes de añadir el borde punteado, comprueba si ya llega**: `DesignTokens.axaml` tiene un
estilo `:disabled` que cubre diez tipos de control. Y **la lista de hosts**: la §4 le pide estado vacío,
pero `NetworkPurposes` se **inyecta**, así que hay que medir si puede quedar vacía de verdad — una
cadena que nadie puede llegar a ver es peor que no tenerla.

**`DiagnosticsPreviewView`:** `MaxHeight="320"` ya está; la fuente es el literal
`"Consolas,Cascadia Mono,monospace"` y **no hay `FontSize`**, así que los 13 px de la §4 no están; y
lleva `TextWrapping="NoWrap"` donde la §4 pide que envuelva — es texto que alguien lee **para decidir si
lo comparte**, así que no debería pedir scroll lateral.

**`LifecycleSettingsView`:** hay un bloque `AccentSubtleBrush` (el consentimiento); **no hay ningún
`WarningSurfaceBrush`** en la vista, así que el aviso de «sin bandeja» que la §4 quiere como advertencia
hoy no lo es.

#### Lo que el tramo 2 (Inicio) dejó escrito, hecho el 2026-08-20

[Su evidencia](evidence/stable/audit-home-tranche.md). Tres cosas que valen para los siete tramos que
quedan:

1. **NO HAY PORTADAS EN TODA LA APLICACIÓN, y la §4 las da por hechas.** Medido: **cero `<Image>` en
   los `.axaml` de `src/`**, y el único mapa de bits del árbol es el fotograma de vídeo de LibVLC.
   `PosterPath` se produce, se fusiona y se persiste en SQLite, y **ninguna vista lo lee**. La §4 pide
   portadas en tres filas de Inicio y las volverá a pedir en Biblioteca y en las fichas. **No es
   trabajo de una vista**: una ruta de TMDB es remota, así que descargarla sería una conexión nueva que
   habría que declarar en `NetworkPurposeRegistry`, y con ella caché en disco, tamaño y caducidad.
   Cuando un tramo la pida, se salta esa parte y se anota — como aquí.
2. **`LibraryEntryView` NO ES LA FICHA QUE LA §4 DESCRIBE.** El documento la pinta como «ficha 2:3,
   título a dos líneas, año en `TextSecondaryBrush`, iniciales sin portada»; en el árbol es el bloque
   de **entrada a la biblioteca** —recuentos y un botón—. Manda el árbol. La decisión sobre las
   iniciales que esta nota traía escrita **no se gastó** y sigue disponible el día que existan las
   portadas: dos iniciales de las dos primeras palabras del título, una si sólo hay una palabra, y
   relleno liso si el título no da ninguna.
3. **El tramo destapó un defecto de la casa en su sexta forma.** `RecentlyAdded` se lee de SQLite,
   viaja por `GetHome` y llega a `RecentlyAddedItemViewModel` con su año formateado, y **ningún
   `.axaml` lo pintaba** — con tres suites en verde afirmando sobre él. Inicio gana una vista,
   `RecentlyAddedRailView`, y con ella `SURFACES` pasa a **50** y el área de Inicio a **6**. **Antes de
   dar por cerrado un tramo, pregunta qué datos de esa área produce la aplicación y no pinta nadie.**

**Y la razón escrita en una tabla cerrada puede ser falsa aunque la decisión sea correcta.**
`LeadingActionTests` decía que `LibraryEntryView` no lidera por ser «una fila o ficha que se repite»,
y no lo es. La decisión seguía siendo la buena por otro motivo —Inicio ya acentúa Continuar y la §4
pide **un solo acento sólido por pantalla**—, que ahora está escrito y **medido sobre la pantalla
ensamblada**, que es la mitad que una tabla por vista no puede ver.

#### El tramo 3 (Biblioteca y fichas), medido el 2026-08-20 sin escribir código

**Lo que ya está y no hay que rehacer:** el contador de escaneo de `LibraryView` (`ScanProgressSurface`
con `EnumeratedCount`), el vacío de `ShowDetailsView` (`ShowDetailsEmpty`), «sin sinopsis»
(`HasOverview` en las dos fichas) y los tres estados de visto de `EpisodeRowView` (`○ ◐ ●`).

**Lo que falta, por vista:**

- **`LibraryView`**, lo único que queda: **la cuadrícula fluida con mínimo de 180 px**, y tiene **una
  decisión dentro que hay que tomar con datos** — un `WrapPanel` da el reflujo y **pierde la
  virtualización** que hoy sostiene una biblioteca grande, así que la salida probable es
  `ItemsRepeater` con `UniformGridLayout`, **y lo primero es comprobar si el paquete está
  referenciado**. ~~La fila de filtros~~, ~~«buscando sin resultados»~~ y ~~el botón de borrar la
  búsqueda~~ están **HECHAS el 2026-08-20**
  ([1](evidence/stable/audit-library-no-results-and-episode-row.md),
  [2](evidence/stable/audit-library-clear-search.md)).
- **⚠ Y una trampa del paquete, medida:** de sus 22 cadenas de vacío, la primera pareja
  —`LibraryEmptyTitle` / `LibraryEmptyDescription`— **ya existe con otro nombre**: son
  `EmptyLibraryTitle` / `EmptyLibraryDescription`, y **las pinta `ShellView`**, no `LibraryView`.
  Añadir las del paquete sería duplicar. `LibrarySearchNoResultsTitle` / `…Description` **ya están
  gastadas** (2 de sus 22).
- ~~**`UnavailableBadge`**~~ **HECHO el 2026-08-20** ([su evidencia](evidence/stable/audit-unavailable-badge.md)):
  aviso con borde y glifo `⚠`, y **las cinco copias hechas a mano** —`InProgressRailView`,
  `RecentlyAddedRailView`, `MovieDetailsView`, `ShowDetailsView` y `EpisodeRowView`— montan ya el
  badge, con puerta que impide que vuelvan. Perdió el `x:DataType` para servir a seis modelos, y como
  **Avalonia rechaza un binding compilado sin él**, su visibilidad es `ReflectionBinding`.
- **⚠ `AccentSubtleBrush` es la caja de aviso de casi toda la aplicación: 18 vistas.** Los seis
  pinceles de gramática —`WarningSurfaceBrush`, `DangerSurfaceBrush`, `PositiveSurfaceBrush` y sus tres
  bordes— estaban declarados en los cuatro diccionarios **sin un solo lector**, esperando a la §4, que
  los gasta en los tramos 3, 4 y 6. **El badge gastó el primero**; los otros cinco siguen sin lectores.
  `ScalarTokenTests` no los vigila porque **no son escalares**.
- ~~**Las tres filas a `WrapPanel`**~~ **HECHAS el 2026-08-20**
  ([su evidencia](evidence/stable/audit-wrapping-rows-and-the-ratchet-toll.md)): la de filtros de
  `LibraryView` y las de acciones de las dos fichas, con **tabla cerrada** en `WrappingSurfaceTests`
  que absorbe la que el tramo 1 dejó suelta en `ShellNavigationBarTests`. De `MovieDetailsView`
  queda «dos columnas con portada fija de 320 px», que choca con que no hay portadas.
- **`ShowDetailsView`**: **no tiene selector de temporada**. Hoy apila todas las temporadas en un
  `ItemsControl`. El selector es **un control nuevo** (con su escena), y «una sola temporada oculta el
  selector» es un estado más. **Se empezó el 2026-08-20 y se revirtió entero** al llegar una prioridad
  nueva: el modelo ya tenía `SelectedSeason` y `HasSeasonChoice`, y dejar propiedades que ninguna
  vista pinta es la sexta forma del defecto de la casa. **Su decisión ya está tomada y no se
  re-delibera: con una sola temporada el selector es AUSENTE, no deshabilitado**, porque un control
  que sólo puede contestar lo que ya dice es una pregunta que nadie hizo.
- ~~**`EpisodeRowView`**~~ **HECHA el 2026-08-20**: 56 px y número monoespaciado en columna fija.
  **Lo que se afirma es que la columna cuadra** —el 9 y el 10 terminan en la misma x—, no cómo se
  llama la fuente: el ancho fijo con alineación es el fin y la familia es el medio.

**⚠ Y UNA TERCERA, QUE APARECIÓ AL AÑADIR LA PRIMERA VISTA DE LA FASE 6: AÑADIR UNA SUPERFICIE
CUESTA UNA ENTRADA EN EL TRINQUETE DE COBERTURA.** Todos los `.axaml` de vista de este árbol miden
**100/50** —es el código que genera Avalonia para el marcado, con una rama que nadie ejerce—, así que
**una vista nueva añade siempre una línea a `eng/coverage-debt.txt`**, y esa lista **sólo encoge**. El
trinquete ha ido 219 → 218 → 217 y **no sube**: subirlo porque hemos añadido superficie sería una
regla que se relaja sola. **Se paga sacando otro archivo de la lista, mejorándolo de verdad**, que es
la única forma de salir de ella. El 2026-08-20 lo pagó `CandidateScorer.cs`, de 95 a 100 % de ramas, y
la rama que faltaba se encontró **leyendo el informe línea a línea**: era «el nombre trae temporada o
episodio y el proveedor no los contesta». **Presupuesta esa mejora como parte del coste de cada vista
nueva.**

#### El tramo 4 (Reproductor, 16 vistas), medido el 2026-08-20 sin escribir código

Es **el más grande de los nueve**. Medido contra el árbol, vista por vista:

**Lo que ya está y no hay que rehacer:** `ResumePromptView`, `NextEpisodeOverlay` y `VersionSwitchDialog`
**ya llevan alineación explícita** (`Center`/`Bottom`), que es la mitad de lo que la §4 les pide;
`LooseFileBanner` ya tiene sus dos filas de acciones en `WrapPanel`; y `PlayerView` ya tiene una de sus
filas en `WrapPanel` desde el andamio.

**Lo que falta, por vista:**

- **`PlayerView`** — ~~(1) el bloque de fallo a `DangerSurfaceBrush` con glifo propio~~ y ~~(2)
  `RecoveryActionsSurface` a `WrapPanel`~~ están **HECHOS el 2026-08-20**
  ([su evidencia](evidence/stable/audit-player-failure-grammar.md)); la tabla de
  `WrappingSurfaceTests` sube a cinco filas. ~~(3) La superficie propia~~ está **HECHA el 2026-08-21**
  ([su evidencia](evidence/stable/audit-player-surface.md)): `PlayerSurfaceBrush` = `#0B0D10` en los
  cuatro temas, que es el **único token del árbol que no sigue el tema**, y `MiniPlayerWindow` lo
  gasta también.
- **⚠ Y (4), «la columna fija de 320 px», ERA UNA NOTA FALSA MÍA, corregida el 2026-08-21.** La
  columna **ya existe y siempre existió**: `ShellView.axaml` monta la zona del reproductor con
  `ColumnDefinitions="*,320"` y apila dentro los cinco paneles laterales, con un comentario que
  dice «this column is 320 px wide by definition». **Escribí que no existía buscando `320` en
  `PlayerView.axaml`**, que es la vista del vídeo y no la que monta la columna. Es la forma de
  siempre: **un grep vacío en el archivo equivocado se lee como ausencia**. Antes de escribir «esto
  no existe», pregunta **quién lo montaría** y mira ahí.
- **⚠ Y una puerta que salta con cualquier token de tema nuevo:** `ContrastTokenTests.RequiredKeys`
  es una **lista cerrada** —cada diccionario debe llevar exactamente esas claves— y una clave nueva
  la rompe hasta que se declara ahí. Además, si la superficie nueva **no** entra en la lista de
  contraste de texto, la razón se escribe **con su aserción**: dejarla fuera en silencio es
  indistinguible de haberlo olvidado.
- ~~**Las cuatro listas de la columna y sus ocho cadenas de vacío**~~ **HECHAS el 2026-08-21**
  ([su evidencia](evidence/stable/audit-side-list-empties.md)); van **10 de las 22** del paquete.
  Les siguen faltando **las filas de 36 px** y el truncado con tooltip.
- **⚠ Y «vacío» NO significa lo mismo en las cuatro, medido el 2026-08-21. Decidido, para no
  deliberarlo al escribirlo:**
  - `Markers` y `Detections`: vacío es `Count == 0`, y no hay más que hablar.
  - **`TrackSelectorView` NUNCA está a cero**: `SubtitleTracks` lleva siempre la opción
    «desactivado» que el propio modelo añade. Así que «sin pistas que elegir» es **una sola opción
    real por tipo**, no cero elementos — y el texto del paquete lo dice bien: «este archivo trae una
    sola pista de cada tipo». Contar elementos aquí daría un vacío que no llega nunca.
  - **`PlayerVersionsView` ya resuelve su vacío AL REVÉS que la §4**: hoy la vista entera se oculta
    con `IsVisible="{Binding HasAlternatives}"`, que es **ausente**; el paquete le pide una cadena,
    que es **presente y vacío**. **Manda la §4 aquí y no el árbol**, y la razón es que esta lista vive
    en una columna junto a otras tres: una que desaparece mueve las demás, y quien busca «¿hay otra
    versión?» merece leer «una sola» en vez de no encontrar dónde estaba. Es la excepción a la
    gramática de ausente, y se anota porque contradice a `PrivacySettingsView`.
- ~~**`VideoStatusOverlay` en dos gramáticas**~~ **HECHO el 2026-08-21**
  ([su evidencia](evidence/stable/audit-video-status-grammars.md)): los cuatro datos a texto de
  leyenda secundario y los dos avisos con su caja ámbar y el glifo — **tercer par de los seis
  pinceles de gramática gastado**. **⚠ Discrepancia decidida:** la §4 pide los datos **sin caja** y
  conservan la del distintivo, porque **flota sobre el vídeo** y un texto sin superficie se lee
  contra un fotograma arbitrario: no hay contraste que garantizar ni medir.
- ~~**Los cuatro superpuestos**~~ **HECHOS el 2026-08-21**
  ([su evidencia](evidence/stable/audit-overlay-caps.md)), y la medición encontró que **el defecto
  del 2026-08-17 seguía medio vivo**: con las dos alineaciones puestas y una frase larga dentro,
  `ResumePromptSurface` ocupó **1278 px de un escenario de 1280**. **Centrar impide que un panel se
  desplace, no que crezca.** Puestos los topes (420/420/520) y la esquina del botón de saltar.
- ~~**`TransportControlsView`: 44 px y los glifos**~~ **HECHOS el 2026-08-21**
  ([su evidencia](evidence/stable/audit-transport-glyphs.md)). Once botones cambiaron **sólo el
  `Content`** a los pictogramas de Windows; los ocho puntos de código se midieron **en las dos
  familias declaradas** (`Segoe Fluent Icons` y `Segoe MDL2 Assets`) y en ninguna de texto, porque el
  glifo cero es `.notdef` y preguntar por presencia sin excluirlo aprueba la fuente que no dibuja
  nada. Los cinco del mini reproductor caben ahora **en una sola fila a 320 px**, que es el mínimo de
  su ventana y más estrecho que los 480 donde se plegaron.
  - **⚠ Y el hallazgo, que es una regla: un cambio se anota contra la vista que lo recibe, no contra
    la fila que lo pidió.** Los 44 px del 2026-08-21 se anotaron contra la fila de la §4 que dice
    `TransportControlsView`, pero subieron la clase `player-chrome`, **que esta vista nunca ha
    llevado**: medido, sus tres botones seguían en `MinWidth 0` y `MinHeight 36`. La llevan ahora.
  - **Y una aserción heredada se reescribe, no se borra.** `MiniPlayerChromeAutomationTests`
    afirmaba `Content == AutomationProperties.Name`, cierto mientras los dos salían de la misma
    clave. Ahora afirma **la mitad que no puede moverse**: el nombre es la palabra de la clave, el
    contenido es un punto de código de uso privado, y los dos son distintos.
- ~~**Las cuatro listas: filas de 36 px, sin scroll horizontal y truncado con tooltip**~~ **HECHAS el
  2026-08-21** ([su evidencia](evidence/stable/audit-side-list-rows.md)). Lo que la medición encontró
  dentro era más de lo que la §4 pedía:
  - `MarkerEditorView` tiene `MinHeight=96` y `DetectedMarkerReviewView` `MinHeight=72` en sus
    `ListBox`, y **ninguna fija la altura de fila**: medida, la fila sale a **44 px** y sale de sumar
    el relleno del `ListBoxItem` al alto del texto, así que `Height` a solas no la baja a 36 —es la
    misma trampa del `ProgressBar`— y hay que tocar el relleno con ella.
  - **⚠ Y EL DEFECTO, séptima forma del de la casa: las dos listas pintan el `ToString()` de un
    record de dominio.** Literal, en la columna de 320 px: `IntroMarker { Id = …GUID…, SeriesId =
    SeriesId { Value = …GUID… }, Kind = Intro, Start = 00:00:30, … }`. Ninguna de las dos declara
    `ItemTemplate`, así que pinta el volcado que el compilador genera. Y **el selector de tipo de
    marcador pinta `Intro`/`Recap`/`Credits` sin traducir**, porque `MarkerKind` no tiene claves.
  - **Lo que ya está bien:** el scroll horizontal ya es `Disabled` en las dos, medido.
  - **La forma decidida:** un `ItemTemplate` con un `TextBlock` de `TextTrimming="CharacterEllipsis"`
    y `ToolTip.Tip` con el texto entero, y la etiqueta por **converter de presentación** —hay
    precedente: `ResourceKeyConverter`, `RouteStateConverter`, `SubtitleColourConverter`— para no
    cambiar el tipo de las colecciones y arrastrar a `SelectedMarker`, a `Selected` y al paseo. Las
    tres claves de `MarkerKind` **van en los dos idiomas**.
  - `TrackSelectorView` no es una lista sino dos `ComboBox`, y `PlayerVersionsView` es un
    `ItemsControl` cuya etiqueta hoy **envuelve**: 36 px fijos con `Wrap` cortan el texto, así que
    ahí el truncado sustituye al envoltorio.
- ~~**`LooseFileBanner`: banda superior no superpuesta al vídeo**~~ **HECHO el 2026-08-21**
  ([su evidencia](evidence/stable/audit-loose-file-band.md)), **y con eso el tramo 4 cierra**.
  - **Los 48 px de alto se rechazan, con su número.** El banner pide **660×286 a 1280 de ancho, 318 a
    900 y 336 a 480**: lleva encabezado, nombre de archivo, explicación que envuelve, la acción y un
    panel de confirmación con dos botones más. La §4 marca esa fila «Bloqueado … no puedo
    verificarlo»: está escrita sin haber visto el control. **Quinta discrepancia §4↔árbol.**
  - **Y la razón que el documento no podía ver:** `PlayerStage` es el control que el shell **entrega a
    la ventana del mini reproductor**, así que lo que esté dentro viaja — 336 px de banner en una
    ventana de 270. Sale a una fila propia **hermana de `PlayerHost`**, nunca dentro: al volver del
    modo mini `ShellView.axaml.cs` hace `host.Content = stage`, que **sustituiría cualquier cosa
    declarada ahí al lado y no la devolvería nunca**.
  - **`MaxHeight=320` estaba recortando** —el banner pide 336 en estrecho—; la fila `Auto` acota ahora
    por contenido. `VerticalAlignment="Top"` se queda: es la guarda que sigue haciendo algo cuando la
    puerta de desbordamiento lo monta a solas.
- ~~**Los tres avisos de `AudioOutputView`**~~ **HECHOS el 2026-08-21**
  ([su evidencia](evidence/stable/audit-audio-warnings.md)), y **`SubtitleStyleView` el 2026-08-21**
  ([su evidencia](evidence/stable/audit-subtitle-preview.md)) — donde medir encontró que **la vista
  previa previsualizaba una cosa de cinco**. ~~Y `ShortcutSettingsView`~~ **el mismo día**: ya estaba
  en columnas, así que sólo le faltaba su cadena de vacío — **12 de las 22 del paquete gastadas**.
- **⚠ Y dos trampas nuevas, medidas el 2026-08-21.** (1) `HighContrastTests.No_state_is_told_by_colour_alone`
  **prohíbe cualquier `Background`/`Foreground` con `{Binding`**, y una muestra de color legítima la
  dispara: se declara en su **lista de excepciones nombrada** —vista, propiedad y origen— que sólo
  encoge, nunca se relaja la regla. (2) **`eng/run-accessibility.ps1` no limpia
  `artifacts/accessibility` entre ejecuciones** y su recuento lee todos los JSON que encuentre, así
  que tras un fallo la siguiente ejecución informa defectos que ya no existen. Borra la carpeta
  antes de creerte el número.
- **⚠ Y una trampa que mordió dos veces seguidas el 2026-08-21, con la puerta de desbordamiento
  cazándola:** un glifo al lado de un texto que envuelve **no puede ir en un `StackPanel`
  horizontal** —ofrece anchura infinita, así que el texto no envuelve y se sale: `x=921` en una
  ventana de 900—. Va en `Grid ColumnDefinitions="Auto,*"`. **Los avisos del estado de vídeo
  tenían el mismo defecto sin que nadie lo hubiera visto**, y se corrigieron en el mismo cambio.

**⚠ Y la discrepancia que ya está anotada desde el 2026-08-18 y sigue valiendo:** la §4 dice «7 motivos
de fallo» y **son 6**. El séptimo, `UnsupportedCapability`, viaja en `VideoOutputDecision` y sale por
`VideoStatusOverlay` **con el vídeo reproduciéndose**; pintarlo como fallo le diría a alguien que no
hay imagen mientras la está viendo.

**⚠ DOS TRAMPAS DEL DOCUMENTO, medidas, que valen para los ocho tramos:**

1. **La §4 nombra los escalares con los nombres VIEJOS.** Cita `SpaceXSmall`, `SpaceSmall`,
   `SpaceMedium`, `SpaceLarge` y `SpaceXLarge`, y hoy son `Space4/8/12/16/24`. La traducción es directa
   por su valor —`SpaceLarge` era 24 → `Space24`— salvo la última.
2. **`SpaceXLarge` era 32 y NO EXISTE**, porque se decidió no declarar un escalar que nadie gasta. La
   §4 lo pide **dos veces**, y las dos como **`Padding SpaceXLarge`** — que **no se puede aplicar
   igualmente**, porque `Padding` es `Thickness` y los `Space*` son `x:Double`: el setter no convierte.
   Así que ahí va **literal 32**, como el resto de `Padding`/`Margin`, y `Space32` **sigue sin
   declararse**. Si algún día lo pide un `Spacing`, entonces sí.

**Dos decisiones que la fase 6 necesitaba y se toman el 2026-08-20, para no deliberarlas por tramo:**

1. **0.2.0 NO se corta hasta que la §4 termine.** El orden de los diez pasos es 6 → 7 → 8, y adelantar
   el corte publicaría una versión con el rediseño a medias: la mitad de las pantallas con la gramática
   nueva y la otra mitad sin ella es peor que ninguna de las dos. La única excepción sigue siendo la de
   siempre: **un hallazgo del paseo físico del propietario entra cuando llegue**, porque corregirlo
   después del corte cuesta rehacer el corte.
2. **Dentro de la fase 6, la unidad del trinquete es EL TRAMO, no la fase.** La regla de la casa dice
   que el trinquete del paseo sube dentro de una unidad y vuelve a **0** al cerrarla; con **69
   controles** repartidos en ocho tramos, tratar la fase entera como unidad dejaría la red rota
   mientras dura todo el rediseño — y **el paseo es la red del rediseño**, que es la razón de haberlo
   llevado a cero antes de empezar. Así que **cada tramo cierra con `eng/walk-pending.txt` vacío**.

**Y si la §4 contradice al árbol, manda el árbol**, con la discrepancia anotada: ya pasó con los «siete
motivos de fallo» de `PlayerView`, que son **seis** —el séptimo viaja en `VideoOutputDecision` y sale
por `VideoStatusOverlay`, con el vídeo reproduciéndose—. Pintarlo como fallo le diría a alguien que no
hay imagen mientras la está viendo.

**Y lo que ya está pagado, que es el andamio de todo esto:** las tres escalas con puerta —la §4 escribe
`SpaceLarge` y `CornerRadiusMedium` por todas partes—, la acción principal decidida para las 48, la
puerta de desbordamiento (que es exactamente la red de los `WrapPanel` que la §4 pide) y el paseo en 0.

**Los 35 activos de instalación NO entran**: bloqueados esperando el vectorial de la marca.

### Las portadas NO entran en 0.2.0, decidido el 2026-08-21

Es la decisión de alcance que quedaba abierta, y va aquí para que no se re-delibere.

**Qué son:** la §4 las da por hechas en tres filas de Inicio, en la cuadrícula de Biblioteca y en las
dos fichas. Hoy **no existe ni una imagen en toda la aplicación** —cero `<Image>` en los `.axaml` de
`src/`— y `PosterPath` se produce, se fusiona y se persiste sin que ninguna vista lo lea.

**Por qué no entran:**

1. **No son una vista, son una función.** Una ruta de TMDB es **remota**, así que traerla es una
   conexión que declarar en `NetworkPurposeRegistry`, una caché en disco con su tamaño y su caducidad
   —y la de TMDB tiene un **techo duro de 180 días** que sus términos imponen—, y una política de qué
   pasa cuando no hay red o el token no está. Eso es un tramo propio del tamaño de varios de los nueve.
2. **0.2.0 es el rediseño.** Meter funcionalidad nueva dentro del corte lo convierte en otra cosa, y el
   orden 6 → 7 → 8 se decidió precisamente para que el paseo físico juzgue **una interfaz terminada**,
   no una a la que aún le crecen partes.
3. **Y arrastran la cuadrícula.** Una rejilla de fichas sin imagen es una rejilla de cajas de texto,
   que no es mejor que la lista de hoy, y **cuesta 7× el tiempo y 455× los controles vivos** por perder
   la virtualización. **Las portadas y la cuadrícula son la misma tarea** y se hacen juntas o ninguna.

**Cuándo, entonces:** se decide **cuando 0.2.0 esté publicada**, con la hoja de ruta delante y no con
el paquete de diseño, porque a esas alturas la pregunta ya no es «qué pedía la §4» sino «qué le falta a
esto para que alguien lo instale». Hasta entonces, las tres filas de la §4 quedan **anotadas como
discrepancia medida**, que es lo que son.

### El orden, que ya estaba escrito y no se re-delibera

**Lo siguiente es la fase 6 del paso 6: la §4, una vista por commit.** Lo dice `design/PROMPT.md`
punto 6 y lo repite la tabla de diez pasos: el paso 7 (el paseo físico) y el 8 (cortar 0.2.0) van
**después**, y el paseo físico va antes del corte porque un hallazgo suyo obliga a rehacerlo entero.
Preguntar en qué orden seguir es re-deliberar algo decidido el 2026-08-17.

**El orden de las vistas dentro de la §4 es el de sus propias áreas**, que es también el de
`SURFACES.es.md`: Shell, Inicio, Biblioteca, fichas, Reproductor, Ajustes, Revisión, Metadatos,
Catálogo, Copias, Primeros pasos, Recuperación, Créditos, Actualización. Las dos que la §4 marca como
las de más trabajo —`UpdateView` y `PlayerView`— ya tienen su parte de maqueta hecha y les falta la
gramática de sus mensajes.

**Cada vista trae consigo tres cosas que no son opcionales**, y por eso una vista es un commit:
sus controles nuevos **con su prueba de nombre accesible y su línea de paseo en el mismo cambio**, sus
cadenas nuevas **en los dos idiomas**, y sus estados condicionales pintados. Un control nuevo sin
escena hace fallar la puerta del paseo, que está en 0.

**Si el propietario trae hallazgos de su paseo físico antes de que la §4 termine**, van **primero**:
cada uno como su escena con su medición. Si el hallazgo es «esto se ve mal» sin número, lo primero es
encontrar el número. Y un hallazgo de maqueta que ya tenga puerta —desbordamiento, escalas, acción
principal— debería haber fallado allí: si no lo hizo, **la puerta tiene un agujero y ése es el defecto
real**, no sólo la pantalla.

### Y cuando la §4 termine y el paseo físico pase, el paso 8 se abre solo

```bash
pwsh ./eng/prepare-release.ps1
```

**Ése es el checklist, y no hay que escribir otro.** El guion contesta si el árbol se puede publicar y
construye todo lo que una versión lleva; **no crea el tag, no publica, no empuja y no cambia ninguna
configuración**, así que correrlo es gratis y su informe dice qué falta. Con el artefacto recién
construido, `-SkipBuild` lo reutiliza.

**El informe ya se corrió el 2026-08-20 y dice esto**, así que la sesión siguiente no lo descubre:

```
AP Reelume - release readiness for 0.1.0
  ok      The repository is public, so the release address will answer.
  ok      688 file(s) identical across two clean builds.
  ok      winget manifest ready at artifacts/package/winget
  ok      Read arm64-matrix.json before deciding whether ARM64 ships. It is built, not certified.
  BLOCKS  The working tree has uncommitted changes.
  BLOCKS  SHA256SUMS.txt is not signed.
```

**Los dos bloqueadores son los esperados y ninguno es un defecto:** el árbol sucio era el trabajo de
esa misma sesión sin confirmar, y **firmar es del propietario** (paso 9). O sea: **cuando el paseo
físico pase, el corte no tiene ningún obstáculo técnico conocido**.

**Y la versión sigue en 0.1.0.** Subirla a 0.2.0 son **dos sitios**, y el guion comprueba que
coincidan: `Directory.Build.props` línea 24 (`<Version>`) y
`src/ApSolutions.LocalMedia.Windows.Package/Package.appxmanifest` línea 29 (`Version="0.1.0.0"`, con
sus cuatro componentes).

**⚠ TRAMPA MEDIDA AL CORRERLO: `prepare-release.ps1` lee `main` LOCAL, no `origin/main`.** Dio
`BLOCKS main is 9 commit(s) behind…` con `origin/main` perfectamente al día, porque la referencia
local se había quedado atrás. Se arregla con `git fetch origin main:main` antes de correrlo. **No se
cambió el guion**: mirar la remota sin un `fetch` previo no garantiza más, y meterle red a un guion de
release en vísperas del corte es riesgo por comodidad.

Lo que el corte tiene decidido de antemano, para no deliberarlo: **`A11Y-002` pasa a `BLOCKED`** con su
bloqueador nombrado en `eng/generate-verification-manifest.ps1` y en `release-readiness.md`; el
manifiesto **se regenera desde el paquete recién construido** y las evidencias se añaden a
`FEATURES.md` según el reparto de abajo, **ni una más**; y `release-readiness.md` se cuadra con el
manifiesto **en los dos idiomas**, porque llevaban tiempo discrepando y ninguna prueba lo vigilaba.

**Los techos por paso están puestos, y no dentro de los guiones.** La regla de la casa pide techo para
todo proceso hijo de `eng/`, y acotar el `dotnet test` desde PowerShell obliga a `Start-Process` y a
redirigir la salida, que es cambiar cómo CI captura su registro para no ganar nada. El techo lo pone
quien ya sabe: **`timeout-minutes` por paso en `ci.yml`** — 70 para la verificación, 35 para
accesibilidad, 15 para recuperación y 15 para el paseo, entre 1,5× y 5× lo medido el 2026-08-20 (39m26s,
13m20s, 2m09s y 3-4m). El techo del job (90) sólo podía decir que el job entero murió; éstos dicen
**cuál** paso colgó.

**No quedan hallazgos de producto abiertos.** Los dos que arrastraba la cola se cerraron el 2026-08-20:
«Reproducir desde el principio» estaba medido y correcto, y el progreso por archivo es un defecto real
**localizado en `CompositionRoot.cs:951` y `:964`** que se pospone a después de 0.2.0 por ser una
migración de datos — está abajo con su diseño correcto y su porqué.

**Y un aviso sobre cómo se lee la salud de CI**, que costó un diagnóstico equivocado en esta sesión:
para saber cuánto lleva un paso **hay que medir la hora actual**, no restarla de una hora supuesta. Un
run que parecía llevar 21 minutos en la puerta del paseo llevaba menos de uno; el run entero duró **61
minutos** y ese paso **4:16**, ambos sanos.

**Y la red del desbordamiento ya no se escribe vista por vista: hay puerta.**
`ViewOverflowTests` monta **las 48 vistas** sin contexto de datos —todas las ramas visibles a la vez,
que es la cota superior— en una ventana de 900 y afirma que ningún control termina fuera. Probada
fallando a 300: nombra nueve vistas con su control y su coordenada.
[Su evidencia](evidence/stable/audit-view-overflow-gate.md).

**Su limitación está dicha y hay que respetarla:** una vista sola recibe los 900 enteros, y anidada en
el shell recibe menos. Caza la vista demasiado ancha **por sí misma**; la que sólo lo es al anidarse la
sigue cazando el paseo. **Un silencio de esa puerta no es un certificado.**

**Y `primary-action` está decidida para las 48 vistas**, con
[su evidencia](evidence/stable/audit-leading-actions.md): **17 lideran** (las 3 anteriores más 14
nuevas) y **16 no lo hacen a propósito**, por seis razones distintas. La tabla vive en
`LeadingActionTests` y **una vista nueva falla hasta que alguien decida**, que es lo que impide que
envejezca. Probada fallando en tres direcciones: perder la acción, ganar una segunda, y estar en el
árbol sin estar en la tabla.

**La razón que más vale la pena recordar**, porque es de principio y no de maqueta: en las dos
pantallas que piden permiso —`LifecycleSettingsView` y `PrivacySettingsView`— **no se acentúa la
afirmativa**, porque destacar el sí de un consentimiento es un patrón oscuro y esta aplicación existe
para lo contrario.

**Con eso quedan hechos el andamio y las cinco primeras fases.** Lo que sigue es la fase 6: la §4,
una vista por commit.

**Los tokens ya no tienen deuda.** `NotSpentYet` está **vacía** y hay puerta para **las tres escalas**
—tipografía, espaciado y radios— que exige que el `.axaml` **no escriba el número**. Una vista nueva
que escriba a mano un tamaño, un espaciado o una esquina falla en `ScalarTokenTests`. **Eso es lo que
hace que «una vista por commit» cueste de verdad sólo maqueta**: lo que queda por vista es
`primary-action` donde la haya, la red del desbordamiento, y nada más.

**La escala de radios se decidió aquí y no venía en el plan** (`docs/evidence/stable/audit-corner-radius-scale.md`).
Se hizo de una vez por el mismo argumento que ganó en el espaciado —es un mapeo y no una decisión por
pantalla, y sin puerta las vistas siguientes pueden reintroducir literales—, y su lección es la
contraria de la que se esperaba: **un criterio recién probado es cuando más fácil es aplicarlo mal.**
Los tres radios grandes eran las tres tarjetas y parecía faltar un `CornerRadiusLarge`; medir el otro
lado dijo que **de las siete superficies de tarjeta, cuatro ya llevaban 8**. No era un escalón que el
árbol pidiera, era un reparto que nadie decidió. **La pregunta no es «¿tiene sentido el escalón?» sino
«¿lo contradice algo que ya está en el árbol?»**

**Dos advertencias que esta tanda compró caras, y valen para todas las vistas que quedan.**

**La primera, de la fase de escalares: cuando dos mediciones discrepan, se diffan los dos comandos.**
Un recuento propio dio 163 donde el documento decía 183, y lo primero que hice fue construir una
explicación de por qué el documento estaba mal —que además cuadraba con un tercer número—. El
documento tenía razón; mi patrón llevaba un `\b` que no veía `RowSpacing` ni `ColumnSpacing`. **Una
hipótesis que encaja con los números no es una medición**, y aquí habría dejado 23 sitios sin tokenizar
con la fase declarada terminada.

**La segunda, de `PlayerView`:**
la red del desbordamiento se escribe **aunque el cambio parezca sólo cosmético**. Ahí la única prueba
que encontró algo fue la que **pasaba antes del cambio** —no era su rojo, era su red—: midió que la
fila del transporte terminaba **74 píxeles fuera** de una ventana de 900, que es el mínimo que la
aplicación permite. Séptima vez que un `StackPanel` horizontal con etiquetas traducidas saca un
control de la pantalla. **Se mide contra la anchura mínima real, no contra una cómoda.**

**Tres vistas seguidas han costado sólo maqueta** —`MiniPlayerWindow`, `UpdateView` y `PlayerView`—
porque sus controles ya estaban en el paseo. Es lo normal a partir de aquí: el paseo llegó a cero
antes de que la interfaz cambiara, que era el sentido de hacerlo en ese orden.

**Lo que costó esta tanda, y que el resto de las vistas hereda:**

1. **Una ventana secundaria no es como una vista.** `PlayerWindowCoordinator.Apply` asignaba
   `window.Content`, lo que **sustituye el árbol entero del AXAML**: la ventana mini tiraba todo lo que
   declaraba para sí misma en cuanto llegaba una sesión. `Host()` y `MiniPlayerSurface` llevaban ahí
   desde el principio y **sólo los llamaba una prueba** — el defecto de la casa, forma once.
2. **`WalkLedger.Record` exige un `UserControl` ancestro**, y el inventario de la puerta usa el nombre
   del `.axaml`. Un control declarado dentro de un `Window` no puede casar las dos mitades **jamás**.
3. **El paseo no sabía salir de la ventana del shell**, y nadie lo había necesitado. El arnés ganó
   `Reachable`, `SecondaryWindows` y `RootOf`, y cada función de clic apunta ya a la ventana **del
   control**.
4. **Las etiquetas largas no se salen: dejan sin sitio al clic de control.**
5. **Una prueba que compara el VALOR no distingue un literal de un token** mientras los dos coincidan
   — y coinciden justo cuando la tokenización sería correcta, así que el falso verde es el caso
   normal. Se afirma que el `.axaml` **no escribe el número**.

**Los cuatro rojos de CI, y los cuatro fueron puertas haciendo su trabajo:**

- **El trinquete de cobertura encontró una rama que nadie recorría.** El primer intento del mini
  reproductor enrutó por una interfaz y **dejó viva la rama vieja de `Apply`**. La corrección fue
  **borrarla**, no cubrirla: una prueba escrita para alcanzar código muerto pone el número en verde y
  deja el defecto dentro.
- **Y al borrarla el archivo BAJÓ de 100/92 a 100/91**, porque quitar ramas cubiertas sube el peso de
  las que nunca lo estuvieron. Había dos así desde siempre, ambas garantías reales sin prueba.
  Cubiertas, el archivo mide 100/100 y **sale** de la deuda. **Un suelo que baja es una bajada**:
  buscar una explicación de proceso es la forma cómoda de no mirar el código.
- **Una red calibrada en una máquina acusa a otra.** La escena de cancelar la copia comparaba el
  tiempo de sus dos pulsaciones contra una duración medida aquí. Lo que el reloj infería lo dice la
  pantalla: ahora guarda los estados por los que pasa y afirma que `BackupStatusDone` no está.
- **Y el trinquete pidió declarar tres mejoras**, que es la mitad de su trabajo que caza lo que mejora
  sin decirlo.

**Y las tres de siempre siguen valiendo:** un cambio de vistas afecta a **cuatro** suites (`UiTests`,
`AccessibilityTests`, `IntegrationTests`, `DocumentationTests`); el trinquete de cobertura **falla
también cuando algo mejora**; y `verify.ps1` **aborta en el primer fallo**, así que un rojo esconde
los posteriores.

## La cola decidida el 2026-08-16 (no se re-delibera)

**El objetivo es cero.** Esta aplicación se publica gratis y **nadie la va a probar a mano**: lo que
la suite no cubra no lo cubre nadie. El trinquete de `eng/check-walk-coverage.ps1` va a **0
pendientes** —y **desde el 2026-08-18 lo está**: **128 de 128** controles pulsados con ratón, `eng/walk-pending.txt` vacío y el trinquete en 0, que no vuelve a subir. Queda la puerta de cobertura de
código, a vigilar el árbol entero. Todo lo de abajo está **decidido**; lo que queda es ejecutarlo
midiendo antes de corregir.

### El plan completo hasta 0.2.0, fijado el 2026-08-17

**Diez pasos, y el orden es una decisión, no una lista.** El paseo autónomo **es la red del
rediseño**, así que llega a cero **antes** de que la interfaz cambie; la puerta de cobertura pasa a
vigilar el árbol entero por la misma razón. Lo que le toca al propietario va en un bloque al final,
con una sola excepción: su paseo físico va **antes** del corte, porque un hallazgo suyo obliga a
rehacerlo entero.

| # | Paso | Quién | Deja el trinquete en |
|---|---|---|---|
| ~~1~~ | ~~La sesión suelta no se ve~~ **hecha el 2026-08-17, 6 → 3** | agente | 3 |
| ~~2~~ | ~~Los tres últimos de la tanda 1~~ **hecha el 2026-08-18, 3 → 0** | agente | **0** |
| ~~3~~ | ~~La prueba de los subtítulos~~ **hecha el 2026-08-18** | agente | 0 |
| ~~4~~ | ~~Cobertura a todo `src/`~~ **hecha el 2026-08-18 como trinquete: 219 y sólo baja; corregido el mismo día para que el suelo lo mida CI** | agente | 0 |
| ~~5~~ | ~~`ARQ-004`~~ **hecha el 2026-08-18: el comando enlazado, la notificación y la puerta de los siete** | agente | 0 |
| 6 | **El rediseño**, con el material de Claude Design — **fase 2, puerta de escalares, `primary-action`, la tipografía, `MiniPlayerWindow` y `UpdateView` hechas**; quedan **`PlayerView`, la fase de escalares de espacio y el resto de vistas**, todo decidido | agente | 0, con la regla de abajo |
| 7 | El paseo físico de diez minutos | **propietario** | — |
| 8 | Cortar 0.2.0, hasta el instante de firmar | agente | — |
| 9 | Firmar y publicar | **propietario** | — |
| 10 | `REL-004` y la restauración trimestral de la clave | **propietario** | — |
| **11** | **La página del repositorio en GitHub, con capturas** — pedida el 2026-08-20 | agente | 0 |
| **12** | **La landing, preparada para su dominio** — pedida el 2026-08-20 | agente + propietario | 0 |

#### 11 y 12: la página del repositorio y la landing, pedidas el 2026-08-20

**Las dos van después del paso 6 y comparten una misma pieza: las capturas.** Van al final de la tabla
porque una captura de una interfaz a medio rediseñar hay que rehacerla, que es el mismo argumento por
el que 0.2.0 no se corta antes. **No se empiezan antes de que la §4 termine**, salvo la parte que no
depende de la pantalla y está señalada abajo.

##### 11. La página del repositorio, con capturas

**Punto de partida medido el 2026-08-20:** `README.es.md` y `README.en.md` existen, están completos y
**no tienen ni una sola imagen**. Quien llega al repositorio lee una descripción excelente de algo que
no ha visto nunca.

**Decidido el 2026-08-21, para que la sesión que lo haga ejecute en vez de deliberar:**

- **De dónde salen las capturas.** Este proyecto tiene algo que casi ninguno: puede generarlas **por
  ejecución y de forma reproducible**. `LibraryNavigationTests` ya guarda PNG con
  `window.CaptureRenderedFrame()` en `artifacts/ui-captures/`, en los dos idiomas. Lo mismo vale para
  cualquier pantalla, en los cuatro temas y a la escala que se quiera. **Hechas a mano envejecen; hechas
  por un guion se rehacen en el commit que cambia la vista.**
- **⚠ Y hay una razón de privacidad, no de comodidad, para que sea así.** `RepositoryPrivacyTests`
  existe porque «una ruta en pantalla es como una captura deja de poder compartirse». Una captura
  tomada a mano de la biblioteca real del propietario lleva **títulos suyos y rutas suyas** dentro de
  un PNG que ninguna prueba puede leer. Las capturas se toman de una ejecución con **raíz de datos
  aislada y biblioteca sembrada**, como hace el paseo.
- **Si se versionan o no.** Un README de GitHub necesita las imágenes **en el repositorio**; enlazarlas
  a un artefacto de CI no funciona. Así que se versionan, y eso pide decidir **cuántas y a qué tamaño**
  — hoy el árbol versiona 7 imágenes en total.
- **Cuáles: cinco, y éstas.** Inicio, Biblioteca, la ficha de una serie, el reproductor con su columna
  y la bandeja de revisión. Cinco cubre lo que la aplicación es sin convertir el README en un catálogo,
  y **la ficha de serie antes que la de película** porque es la que enseña el selector de temporada y
  las filas de episodio, que es donde se ve que esto cataloga y no sólo reproduce.
- **Se versionan, en `docs/assets/`**, porque un README de GitHub necesita las imágenes en el
  repositorio y enlazarlas a un artefacto de CI no funciona. `docs/` ya está en `VersionedDirectories`,
  así que no hay carpeta nueva en la raíz y `RepositoryPrivacyTests` no se entera.
- **A 1600×1000 y en tema oscuro**, que es la variante sobre la que el paquete diseña y donde vive el
  reproductor. Una sola escala: dos juegos de capturas es el doble de cosas que envejecen.
- **En los dos idiomas no.** El README bilingüe comparte las mismas imágenes: una captura en español
  dentro del README inglés se lee peor que una sin texto, pero **duplicar cinco capturas para traducir
  cuatro etiquetas no lo compensa** — y el guion las regenera, así que la decisión se puede revisar sin
  coste. Se toma la versión en **inglés**, que es la que un visitante internacional de GitHub espera.
- **Y qué más lleva la página además de las capturas**: insignia de CI, licencia, plataforma, el
  enlace de descarga de la release, y la atribución de TMDB, que es **obligación legal** y ya está
  resuelta en la aplicación.

**Lo que se puede adelantar sin esperar a la §4:** el **guion** que genera las capturas, porque es
código de pruebas y no depende de cómo queden las vistas. Lo que no se adelanta son los PNG.

##### 12. La landing, preparada para su dominio

**El propietario la pidió «preparada para luego ponerla en un dominio»**, así que se **construye**
ahora y se **publica** cuando haya versión que descargar: una landing con un botón de descarga que no
lleva a ninguna parte es peor que no tenerla.

**Decidido el 2026-08-21; tres de estas cosas las decide el propio producto y no el gusto:**

- **⚠ Dónde vive, y es una trampa medida.** Una carpeta nueva **en la raíz** rompe
  `RepositoryPrivacyTests`, que trata todo directorio de raíz no declarado como carpeta personal y
  reporta cientos de líneas — pasó con `design/` el 2026-08-17. **Se declara en
  `VersionedDirectories`, nunca se relaja la prueba.**
- **Sin nada externo, y esto no es estética.** Una landing que carga tipografías de Google o una
  analítica **contradice el producto entero** —«nada sale de este equipo»— y la primera persona que
  mire la pestaña de red lo verá. Autocontenida: sin CDN, sin fuentes remotas, sin analítica.
- **Bilingüe**, como todo lo público de este repositorio.
- **Cómo se publica: GitHub Pages desde `docs/` de una rama propia**, con el dominio apuntando ahí por
  `CNAME`. No cuesta nada, no añade proveedor, y **el repositorio ya es público**, así que no expone
  nada que no estuviera expuesto. **El dominio es del propietario** y ya está en `REL-004`.
- **Dónde vive: `site/`**, declarada en `VersionedDirectories` en el mismo cambio. No en `docs/`, para
  que la landing no se mezcle con la documentación que `verify-docs.ps1` y `BilingualHeadingTests`
  vigilan con otras reglas — una página de marketing no tiene por qué tener los mismos encabezados que
  un documento técnico, y meterla ahí obligaría a relajar una puerta o a inventarle excepciones.
- **Qué lleva:** lo que la aplicación es en una frase, las cinco capturas del paso 11 —**las mismas, no
  otras**—, el botón de descarga apuntando a la release, la licencia, y la atribución de TMDB, que es
  **obligación legal** y ya está resuelta dentro de la aplicación.
- **Lo que la bloquea a medias:** el **vectorial de la marca**, que es lo mismo que bloquea los 35
  activos de instalación. Se puede construir entera con un marcador y cambiarlo el día que llegue.

**Y lo que la landing debe decir, que es lo que la hace distinta de la de cualquier otro reproductor:**
que no hay cuenta, que no hay nube, que no sube nada, y que el catálogo se queda donde está. Eso ya
está escrito y medido en la declaración de privacidad; **la landing lo repite, no lo inventa**.

#### ~~1. La sesión suelta no se ve~~ — hecha el 2026-08-17

**Hecha exactamente como estaba decidida** —
[la evidencia](evidence/stable/audit-walk-loose-session.md). `OpenLooseFile` valida y describe,
`ShellSurfaces.OpenLoosePlayer` es la vía única, y los dos llamantes pasan por ella. Tres cosas que no
estaban previstas y quedaron resueltas en el mismo cambio: **`OpenLooseFile` pasó a estático y salió
del contenedor** porque `CA1822` lo dijo al quedarse sin estado; **`ResumeWiringTests` se rompió por
leer la composición como texto** —cuarta vez— y se acotó a la declaración de `OpenPlayerAsync`; y
**`RepositoryPrivacyTests` señaló `design/`** como carpeta desconocida en la raíz, que es la
comprobación funcionando. El tráiler local **ya no está bloqueado**, pero sigue pendiente porque
necesita siembra propia, así que pasa al paso 2.

#### 2. Los tres últimos — decidido cómo se siembran y en qué orden

**Por primera vez en toda la cola, ningún pendiente está bloqueado por un defecto.** Son **dos
escenas, no tres**, porque dos de los tres viven en la misma ficha:

**(a) La ficha de película: «Continuar» y el tráiler local.** Siembra: la película en su carpeta, un
`WatchState` con posición **por encima del suelo de reanudación —30 s— y por debajo del final**, y un
archivo hermano **`<nombre-de-la-película>-trailer.mp4`**, que es lo que `TrailerDiscoveryPolicy`
busca (`Suffix = "-trailer"`, o dentro de una carpeta `Trailers`). **No hace falta grupo de
versiones**: `HasTrailer` sale del descubrimiento por nombre, no del catálogo — la nota antigua de la
cola decía lo contrario y estaba equivocada.

- **«Continuar»** — sonda: la sesión abre **en el punto guardado**, leído del motor y esperando a que
  el demultiplexor aplique la posición de inicio.
- **El tráiler** — sonda: la sesión pasa a reproducir **el archivo del tráiler**, y ahora eso se puede
  afirmar de verdad porque una sesión suelta ya llega a la pantalla: `Player.LooseFile.IsLooseSession`
  y la ruta del medio.
- **Y aquí se mide el primer hallazgo abierto:** con progreso guardado, **«Reproducir desde el
  principio» tiene que dejar el cabezal en 0**. Ahora que la posición pedida manda debería estar
  arreglado; lo que falta es el número.

**(b) La ficha de serie: la fila de episodio.** Siembra: serie con temporada y episodios —el arnés ya
tiene `SeedSeriesAsync`—, llegar a la ficha desde la biblioteca y pulsar la fila. Sonda: la sesión
abre **ese** episodio, por su ruta.

**Predicción medible, y se mide ANTES de pulsar:** la fila de acciones de `MovieDetailsView` es un
`StackPanel Orientation="Horizontal"` con **un `TextBlock` de anchura libre entre botones** —el texto
de la posición de reanudación— y cinco controles. Es **exactamente la forma que ha sacado un control
fuera de la ventana seis veces**, y esta escena es la primera que hace visibles a la vez «Continuar» y
el tráiler, así que la fila será la más larga que ha tenido nunca. Si se sale, pasa a `WrapPanel` como
las otras seis. Se mide con los `bounds` frente a la ventana antes de intentar el clic, no después del
rojo.

#### Los dos hallazgos abiertos: CERRADOS el 2026-08-20

**1. «Reproducir desde el principio» — CERRADO, y estaba medido desde antes.** La escena del paseo
afirma, con el motor real, que tras pulsarlo el cabezal queda **por debajo de 5 segundos**, y falla
diciendo dónde lo dejó (`Start over left the playhead at {x} s.`). Lo mide también
`PlayerViewModelTests`, que exige `(ResumeChoice.Restart, TimeSpan.Zero)`.

**2. El progreso por archivo — es un DEFECTO REAL, está localizado, y se pospone a después de 0.2.0
con su diseño correcto escrito.** Medido el 2026-08-20:

- **El dominio ya es por contenido y está bien.** `SwitchMediaVersion` guarda un `WatchState` por
  `ContentKey` y cambia `SourceMediaFileId` para decir qué versión lo produjo. No hay dos estados por
  título dentro del comando.
- **Quien pasa la clave equivocada es la composición.** `CompositionRoot.cs` líneas **951 y 964**
  construyen `ContentKey.ForTitle(new TitleId(mediaFileId.Value))`: meten el id del **archivo** donde
  el dominio espera el del **título**. Con eso, dos versiones del mismo título tienen dos claves.
- **La clave correcta ya se calcula en otro sitio**: `GroupScannedVersions.cs:139` usa
  `ContentKey.ForTitle(new TitleId(ordered[0].Value))` —el primer archivo del grupo— como identidad
  del grupo. La corrección es que la composición pregunte por el grupo cuando el archivo pertenece a
  uno, y use su propio id cuando no.
- **El síntoma es acotado, y el agudo ya está corregido.** Perder el segundo al confirmar un cambio de
  versión se arregló haciendo que mande la posición pedida, con el porqué escrito en el propio
  `CompositionRoot`. Lo que queda es más estrecho: abrir la otra versión **desde la biblioteca** —no
  con el conmutador— ofrece el punto de esa versión y no el más avanzado del título.
- **Por qué se pospone: cambiar la clave es una migración de datos**, y una migración en la víspera de
  un corte de versión es riesgo puro por un síntoma que no pierde nada (los dos `WatchState` existen,
  y `ProgressTransferPolicy` traduce entre metrajes distintos cuando el usuario pide el cambio).
- **Y por qué NO se escribe una prueba que lo fije**: una prueba que consagra un comportamiento que
  sabemos incorrecto es una trampa para quien venga a corregirlo. Lo que queda es esta nota, con la
  línea exacta.

#### Cómo era la nota antes de medirla (2026-08-16)

1. **«Reproducir desde el principio»** — se mide en la escena (a), como está dicho arriba.
2. **El progreso se guarda por archivo, no por grupo de versiones.**
   `ContentKey.ForTitle(new TitleId(mediaFileId.Value))` ata el progreso al archivo, así que tras
   cambiar de versión hay **dos `WatchState`** y volver a la anterior no reanudaría donde se dejó.
   **Decidido: se mide primero y sólo se corrige si la medición demuestra pérdida real** — la escena
   de versiones ya existe y basta con afirmar las dos claves tras volver. Cambiar la clave al grupo es
   un cambio de modelo con migración, y hoy la posición viaja en el encargo, así que el síntoma puede
   no existir.

#### 5. `ARQ-004` — decidido, y la nota vieja estaba incompleta

Medido el 2026-08-18: hay **ocho** archivos con `CanExecuteChanged` vacío, no nueve clases, y **sólo
uno tiene riesgo real**. Un `CanExecuteChanged` vacío sólo importa cuando `CanExecute` mira **estado
que cambia**; si mira el parámetro, cada consulta con el mismo parámetro da siempre lo mismo y no hay
nada que notificar.

| Archivo | `CanExecute` | Riesgo |
|---|---|---|
| `LibraryViewModel` | `Surface != LibrarySurface.Browse` | **sí, estado que cambia** |
| `RootOnboardingViewModel`, `ShortcutSettingsViewModel`, `LifecycleSettingsViewModel`, `WindowsTrayService` | `true` | no |
| `DatabaseRecoveryViewModel`, `AppearanceSettingsViewModel` (×2), `ShellViewModel` | sólo el parámetro | no |

**Hecho el 2026-08-18 —
[la evidencia](evidence/stable/audit-arq004-command-notification.md)— y la nota era cierta por una
causa falsa.** Decía que hoy no muerde «porque la vista se hace visible y el botón vuelve a
preguntar». Medido: **ningún AXAML enlazaba `BackCommand`**. Los dos botones «Volver» llamaban a
`BackToLibrary()` por el code-behind, así que el predicado no se evaluaba nunca — un comando público
con predicado que ninguna vista consumía, el defecto de la casa con cara de comando. Y el rojo
predicho no podía existir por una segunda razón: cada botón vive **dentro del `Grid` de su propia
superficie**, así que mientras es visible el predicado es verdadero por construcción.

Lo que se hizo, y en qué orden:

1. **Enlazar el comando** (`Command="{Binding BackCommand}"` en los dos botones, fuera `OnBackClick`)
   **y medir**. El rojo apareció en el acto, en la escena del paseo de la biblioteca:
   `Volver a la biblioteca is on screen but cannot be pressed: visible=True, enabled=False`. Las dos
   ramas de detalle viven en el árbol visual a la vez, así que el botón pregunta al adjuntarse con
   `Surface` todavía en `Browse` y el evento vacío tira la suscripción a la basura.
2. **La notificación**, con el evento real en el `RelayCommand` privado y el disparo en el único sitio
   donde `Surface` se asigna. Verde con la misma sonda, y una prueba de unidad nueva donde **la cuenta
   es la aserción**: el predicado solo pasa avisen a quien avisen.
3. **La puerta**, `CommandNotificationTests`, con la lista cerrada de los **siete** que quedan y **el
   predicado exacto** de cada uno: vigila la pareja evento-predicado, no el evento a solas. Se probó
   fallando en las dos direcciones —un octavo sin declarar y un predicado que cambia de forma— y lleva
   su propio suelo anticeguera.

#### Lo que queda decidido del paquete de diseño, para el paso 6

- ~~**Los diez cambios de `SURFACES.es.md` / `.en.md`**~~ **hechos el 2026-08-18**, midiendo cada uno
  contra el árbol antes de escribirlo — y de diez, **tres no eran ciertos**:
  - **`MiniPlayerWindow` ya estaba** en el inventario (Reproductor (16)); la nota que decía que
    faltaba estaba superada.
  - **«`BackupView` tiene historial» no aparece** en `SURFACES`: era un error de la auditoría del
    propio paquete, que lo admite por escrito.
  - **«los siete motivos de fallo de `PlayerView`» son seis.** `PlaybackFailureCode` tiene siete
    valores, pero el séptimo —`UnsupportedCapability`— viaja en `VideoOutputDecision` y sale por
    `VideoStatusOverlay`: el vídeo **se reproduce** con conversión de tono. Pintarlo como fallo diría
    que no hay imagen cuando sí la hay.
  - Dos más no aplicaban al documento (los tokens y la versión de Avalonia no se nombran en él), pero
    sus cifras sí se verificaron y entran en la sección de temas nueva: **58 declaraciones / 40
    nombres** +3 de `Brand.axaml`, y **8** selectores de foco.
  - Lo medido que sí faltaba: **23 listas con datos y sólo 4 con cadena de vacío**, y el vacío de la
    biblioteca lo pinta `ShellView`, así que **buscar sin resultados no muestra nada**; el alto
    contraste es **uno solo** y sobre `Light`, así que quien use el oscuro de Windows recibe el claro;
    y el icono de bandeja es el **sexto** activo, en otro proyecto.

#### Las dos decisiones que quedaban del paso 6, tomadas el 2026-08-18

**1. El alto contraste NO se elige en la aplicación: siguen siendo tres píldoras.** El paquete
sustituye los tres botones de tema por cuatro píldoras, y la cuarta sólo podría ser alto contraste
—`ThemePreference` tiene exactamente `System`, `Light` y `Dark`—. **Se rechaza**, y no por trabajo:
el alto contraste de Windows es un ajuste de **accesibilidad del sistema**, y ofrecer una copia en la
aplicación crea dos fuentes de verdad para la misma necesidad. Alguien con el sistema en alto
contraste y la aplicación en «Claro» tendría una aplicación contradiciendo una necesidad declarada,
que es peor que no ofrecer nada. Además, dejar el enum como está significa que **no hay migración de
ajustes** ni valores huérfanos en los guardados.

Lo que sí falta de verdad **es el cuarto diccionario**, y ése se hace: hoy `AppThemeVariants.HighContrast`
está declarado sobre `ThemeVariant.Light`, así que quien use el alto contraste **oscuro** de Windows
recibe el claro. Se añade `HighContrastLight`, se renombra el existente a `HighContrastDark`, y toca
`AppThemeVariants` y `FluentThemeService` — **no el enum**. Si el rediseño quiere enseñar una cuarta
píldora, que sea **un estado y no una opción**: decir que el sistema está en alto contraste y que se
está respetando. Eso informa sin duplicar el ajuste.

**2. ~~`DebouncedFileWatcher` se arregla por el constructor~~ — hecho el 2026-08-18, y tal como estaba
decidido.** El búfer es ya un parámetro opcional del constructor con el valor de producto por defecto;
la prueba pide el mínimo que la plataforma respeta, desborda de verdad y **lo afirma**. Nada se volvió
`internal` y `WatchSignal` sigue privado.

**La causa registrada era una de tres.** Al medir las dos tiradas enteras aparecieron otras dos
condiciones que ocurrían o no por azar: la otra mitad del mismo manejador —el error que **sí** termina
la vigilancia, que se ejecutaba por casualidad cuando un directorio de prueba desaparecía debajo de un
vigilante vivo— y el switch de coalescencia, cuyas parejas dependían de lo que el sistema entregara
durante la tormenta (13 de 16 ramas en una tirada, 11 de 16 en la otra). Cada una tiene ya su prueba,
y una cuarta cubre el debounce que expira **en el mismo instante** en que llega el cambio que lo
cancela, con un reloj cuya espera termina con éxito al cancelarse.
[La evidencia](evidence/stable/audit-watcher-overflow-determinism.md): de **88,54/73,81 y
93,75/71,43** en dos tiradas del mismo binario a **100/95,83 en tres seguidas**. Las dos ramas que
faltan no son alcanzables.

**Y lo que se midió sin esperarlo: una tormenta secuencial no desborda 4 KiB.** Dos mil archivos con
nombres de cien caracteres, uno tras otro, no desbordaron ni una vez: el cuello de botella no es el
vigilante vaciando el búfer, es crear el archivo, y esas tres décimas de milisegundo son todo el
respiro que su hilo necesita. En paralelo desborda en el primer segundo. El primer intento salió rojo
**por la aserción nueva y no por un tiempo agotado**, que es exactamente para lo que está.
- **La discrepancia de motivos de rechazo del actualizador se resuelve en OCHO**: `README.md` dice 8 y
  `github.md` dice 7, y el que cuadra con los 23 mensajes es el 8 (15 estados + 8 rechazos).
- **Las 25 cadenas de consecuencia se aprueban contra la regla que el propio paquete da** —«si la
  frase ayuda a decidir o a actuar, se traduce; si explica por qué está diseñada así, es un comentario
  del AXAML»— revisándolas una a una al escribirlas. **No bloquean el paso 6**; lo que no pase esa
  regla se queda como comentario.
- **Los 35 activos de instalación siguen bloqueados** en el original vectorial de la marca, y no se
  improvisan.

#### Y un rojo que CI trajo en medio, con un defecto de producto dentro (2026-08-18)

El run de `ba1502e` falló **1 de 117** en el paseo:
`ConfirmSwitchButton is on screen but cannot be pressed: visible=False, enabled=True` — la pregunta
del cambio de versión se iba de la pantalla entre resolver el botón y pulsarlo. Local da 117/117 en
dos pasadas, a 2 minutos por pasada frente a los 5 m 38 s del runner, así que la ventana la abre la
lentitud de allí.

**No era del paseo.** La fila de la otra versión seguía pulsable mientras su propio cambio estaba en
marcha, el arnés vuelve a pulsar a los 300 ms lo que parece no haber hecho nada, y **todo cambio vacía
la posición del reproductor antes de decidir**: una sesión recién abierta responde cero, cero está por
debajo del suelo de reanudación, y entonces la política deja de preguntar, **abre la otra versión sin
preguntar y deja la posición guardada en cero**. Es decir: un doble clic bastaba, sin runner lento.

Corregido donde vino la segunda petición —la fila se apaga mientras su cambio está en marcha, el
patrón que la barra de transporte ya tenía— y **el eslabón que era una deducción se midió**, no se
supuso: [la evidencia](evidence/stable/audit-version-switch-reentry.md). El caso de uso no se toca.

#### La fase 2 del paso 6: el botón hecho, y lo que la plantilla propia debe (2026-08-18)

**El botón está hecho** —95 de las vistas, contra 18 casillas y 15 cuadros de texto, así que es por
donde empieza—: sus cuatro estados de color salen ya de los tokens en los cuatro temas, con borde de
1 px, y en alto contraste el paso de ratón y la pulsación **invierten**.
[La evidencia](evidence/stable/audit-redesign-phase2-button-states.md).

**Lo que se midió y decide cómo se hace el resto:**

- **Un estilo de aplicación NO alcanza los elementos de plantilla de un `ControlTheme`.** Ni
  `Button /template/ ContentPresenter` ni con `#PART_ContentPresenter`. Lo que sí alcanza es **el
  recurso que esa plantilla consume**: `ButtonBackground`, `ButtonForeground`, `ButtonBorderBrush` y
  sus tres estados cada uno. Se redirigen con `<StaticResource x:Key="…" ResourceKey="…" />`, que
  Avalonia acepta como entrada de diccionario, así que **no se duplica ni un valor**. Los demás tipos
  se hacen igual, con sus propios recursos.
- **`ControlTextActiveBrush` es un token nuevo** (van catorce), y hace falta: sin él la inversión de
  alto contraste no se puede expresar sin una regla por tema.
- **Un pincel se lee entero.** `ButtonBackgroundPointerOver` dice `Black` y lleva `Opacity 0,1`;
  leyendo sólo `.Color` se concluyó «texto negro sobre fondo negro», que es falso.

**Lo que la plantilla propia debe, y no se re-delibera:**

1. **El borde punteado del deshabilitado** (`Rectangle` con `StrokeDashArray`). Sin él, **en alto
   contraste deshabilitado es hoy indistinguible de reposo** —el relleno deshabilitado *es* la
   superficie, por diseño—, y eso está medido y afirmado tal cual en la prueba, no aflojado.
2. **El borde de 2 px al pulsar en alto contraste**: la plantilla base tiene un solo grosor para todos
   los estados. Afirmado a 1 px, que es lo cierto hoy, y **el token del grosor llega con la plantilla
   que lo gaste**, no antes: se escribió, se quedó sin consumidor al cambiar de vía, y se retiró.
3. Después: los otros ocho tipos de control, empezando por `CheckBox` (18) y `TextBox` (15).

Y dos cosas del árbol que salieron midiendo: **`primary-action`** (en `ResumeHeroView`) es una clase
que **ningún estilo define y ninguna prueba busca**, y **`navigation-destination`** sí tiene uso, pero
como **marcador de las pruebas**, no como estilo.

#### Las siete decisiones que quedaban del paso 6, tomadas el 2026-08-18 (no se re-deliberan)

1. ~~**El borde punteado del deshabilitado se dibuja como ADORNO, no copiando plantillas.**~~
   **Hecha el 2026-08-18 como estaba decidida** — [la evidencia](evidence/stable/audit-redesign-phase2b-disabled-outline.md).
   Lo que sigue queda por lo que explica el porqué: Una
   propiedad adjunta en `Presentation/Theme` que añade un `Rectangle` con `StrokeDashArray` a la capa
   de adornos cuando el control se deshabilita. **Por qué y no un `ControlTheme` propio por tipo:** el
   anillo de foco ya demostró que la capa de adornos alcanza a los diez tipos con **una**
   implementación —incluidos el `ToggleSwitch`, que lo cuelga del `Grid` de su plantilla, y el
   `NumericUpDown`, que lo cuelga de su `TextBox`—; copiar nueve plantillas de Fluent son nueve
   superficies que se desincronizan con cada actualización de Avalonia, para una raya. Archivo nuevo,
   así que **96/96 desde el primer commit**: la prueba fuerza `IsEnabled=false` y afirma el
   `StrokeDashArray` en la capa.
2. **El borde de 2 px al pulsar en alto contraste NO se hace, y esto es una desviación consciente del
   paquete.** El pulsado en alto contraste ya invierte relleno **y** texto —medido, 21:1—, así que el
   grosor añade una tercera señal a un estado que ya tiene dos, y exigiría otro adorno o una plantilla
   propia. Se documenta como decisión, no como deuda. Si el paseo físico del propietario dice que no se
   distingue, se reabre **con esa medición**.
3. **`primary-action` recibe estilo de acción primaria**, no se borra: es «Continuar» en la portada, y
   el rediseño quiere jerarquía. Necesita un token nuevo, **`AccentTextBrush`** —~~blanco en claro,
   oscuro y alto contraste claro; **negro** en alto contraste oscuro, porque el acento allí es cian~~—, y
   `ContrastTokenTests` lo mide contra `AccentBrush` con el listón de texto, 4,5:1.
   **El token llegó el 2026-08-19 con la casilla, que lo necesitaba antes, y la mitad tachada era
   falsa**: el `AccentBrush` del tema **oscuro** es `#62AEE8`, un azul pálido, y blanco encima mide
   **2,40:1**. Queda `#FFFFFF` en claro y alto contraste claro, `#111827` en oscuro y `#000000` en
   alto contraste oscuro — el color sigue la **luminancia del acento**, no el nombre del tema. Lo mide
   ya la puerta, probada fallando.
4. **`ToggleSwitch` conserva su selector de foco y NO recibe estados.** Cero usos en las 48 vistas: dar
   estados a un tipo que nadie monta es declarar sin gastar. El foco se queda porque ya está escrito y
   cuesta cero.
5. **El orden de los ocho tipos que faltan es el del uso medido**: `CheckBox` (18), `ListBoxItem` (17),
   `TextBox` (15), `ComboBox` (8), `Slider` (5), `NumericUpDown` (5), `ToggleButton` (2),
   `RadioButton` (1). Cada uno por sus **propios recursos de tema**, como el botón.
6. **Los escalares se convierten en trinquete, no en promesa.** Una prueba nueva recorre los `.axaml`
   de `src/` y exige que **cada escalar declarado lo consuma al menos una vista**, con una lista de
   excepciones nombradas —hoy `SpaceXSmall`, `SpaceSmall`, `SpaceMedium`, `SpaceLarge`, `SpaceXLarge`,
   `CornerRadiusSmall`, `CornerRadiusMedium`— **que sólo puede encoger**, igual que la lista de
   huérfanos de `ServiceConsumptionTests` y que `eng/coverage-debt.txt`. Así la deuda es visible y no
   puede crecer en silencio.
7. ~~**La tipografía**~~ — **hecha el 2026-08-19**: [la evidencia](evidence/stable/audit-type-scale.md). **Cinco tokens, no seis**: `FontSizeMono` no se declara porque nada lo gasta y la puerta de escalares lo rechazaría — llega con la primera ruta o hash que lo pida. El `17` fue a `FontSizeBody` **por lo que el texto es** (un párrafo que se ajusta), no por distancia. La línea base de `HomeLayoutTests` movió **un solo campo** y hacia la consistencia. **La tipografía: los tamaños literales del árbol se mapean a seis tokens, y el mapeo es este.**
   **Medido el 2026-08-19: son TRECE, no doce — 52 usos en 30 archivos — y el que falta en el mapeo
   de abajo es el `17` de `ShellView.axaml:140`**, que hay que colocar al ejecutar (por proximidad va
   con el 18, a `FontSizeSubtitle`, pero se mide antes de decidirlo).
   34 y 32 → `FontSizeDisplay` 32; 30, 28 y 26 → `FontSizeTitle` 28; 24, 22, 20 y 18 →
   `FontSizeSubtitle` 20; 16 y 14 → `FontSizeBody` 14; 12 → `FontSizeCaption` 12; y `FontSizeMono` 13
   para rutas, hashes y códecs. Se declaran **en la primera vista que los gaste**, no antes.

#### ~~La fase 2b: el punteado del deshabilitado~~ — hecha el 2026-08-18

**Hecha exactamente como estaba decidida** —
[la evidencia](evidence/stable/audit-redesign-phase2b-disabled-outline.md)—. Propiedad adjunta
`DisabledOutline.IsShown` en `Presentation/Theme`, un `Rectangle` con `StrokeDashArray` en la capa de
adornos, y **el cuándo lo dice un selector** `:disabled` sobre los mismos diez tipos que el foco. El
archivo nuevo mide **100 % de líneas y 100 % de ramas** —comprobado en local antes de empujar, que es
lo que ahorra los 35 minutos de CI por intento— y las 2 170 activaciones no salen de sus pruebas sino
de las vistas reales de la suite de interfaz.

**Lo que la medición añadió, y es la lección de esta tanda: deshabilitar se hereda, y un estilo de
aplicación alcanza también los elementos de plantilla.** Ocho tipos recibían un adorno y **dos
recibían dos**: `ComboBox` y `NumericUpDown` llevan un `TextBox` dentro cuyo `IsEnabled` propio sigue
en `true`, así que se dibujaban dos rectángulos punteados a unos píxeles uno de otro. La condición
correcta **no es el `IsEnabled` local sino `TemplatedParent is null`**, y las dos respuestas difieren
justo donde importa: un control dentro de un panel deshabilitado entero conserva su `IsEnabled` en
`true`, y como un panel no es de los diez tipos, mirar el flag local dejaría ese caso **sin ninguna
raya**. Hoy ese caso no existe —medido: los once `IsEnabled` enlazados del árbol están en nueve
`Button` y dos `CheckBox`, ningún contenedor—, pero la regla se escribió para cuando exista.

**Y de paso se corrigió `SURFACES`**, cuya sección de temas seguía describiendo el árbol de antes de
la fase 1: decía 3 diccionarios (son **4**), 58 declaraciones en 40 nombres (son **140** en **35**,
más 13 escalares fuera), 8 selectores de foco (son **10**) y que no había alto contraste oscuro, que
lleva un día siendo falso.

**Lo que sigue de la fase 2, en orden y sin deliberar:** los ocho tipos por uso medido —`CheckBox`
(18), `ListBoxItem` (17), `TextBox` (15), `ComboBox` (8), `Slider` (5), `NumericUpDown` (5),
`ToggleButton` (2), `RadioButton` (1)—, cada uno por **sus propios recursos de tema** como el botón;
la puerta de escalares consumidos con lista que sólo encoge; `primary-action` con el token
`AccentTextBrush`; y después la tipografía y las vistas, una por commit.

#### El rojo de CI que trajo `4b2c326`, y era del arnés (2026-08-19)

`RestartSwitchButton is on screen but cannot be pressed: visible=False, enabled=True` en la escena del
cambio de versión — [la evidencia](evidence/stable/audit-walk-press-retry.md). **No lo causó el
commit**: toca tokens y pruebas de tema, y la escena pasa seis de seis en local.

**La causa, leída en el bucle de `PressAsync`:** repite una pulsación sin efecto y antes de repetirla
mira **sólo si el control está deshabilitado**. Contestar la pregunta del cambio de versión **la
cierra** —que es lo correcto, y la escena lo afirma—, así que el botón sale del árbol al pulsarlo
mientras su comando sigue habilitado; en un runner cargado el efecto tarda, el bucle da otra vuelta y
pulsa un botón que ya no está.

**Y con eso la regla de la casa gana una excepción, que es lo que hay que recordar:** `visible=False`
acusa al producto **salvo que pulsar ese control sea justamente lo que lo quita de la pantalla**. Los
tres botones de esa pregunta son de esa clase, y también cualquier confirmación que cierre lo que
confirma.

La decisión salió a `WalkPressPolicy`, con su prueba propia —porque el caso **sólo aparece en un
runner lento**, y una regla que se ejercita por suerte no la comprueba nadie— y probada fallando con
la regla vieja. Un control que no está en pantalla ya **no se vuelve a pulsar**: habla el tiempo de
espera del efecto, que es el fallo verdadero.

**Anotado y sin tocar, porque es otra cosa:** la fila que abre la pregunta **sigue pulsable mientras
la pregunta está en pantalla** — `SwitchToVersionAsync` retorna en cuanto llama a `Apply`, así que el
`finally` de la fila la rehabilita con el diálogo abierto, y una segunda pulsación vacía el cabezal,
contesta cero y se lleva la pregunta. Merece su propia medición.

#### Las ocho decisiones que cierran el paso 6, tomadas el 2026-08-19 (no se re-deliberan)

Con esto **no queda nada por deliberar en el paso 6**: lo que sigue es ejecutar, midiendo antes de
cada corrección.

1. ~~**El defecto del cambio de versión se corrige, y va PRIMERO**~~ — **hecho el 2026-08-19**,
   exactamente como estaba decidido:
   [la evidencia](evidence/stable/audit-version-switch-question-guard.md). Se hizo tal cual —
   parámetro obligatorio, `&& !_question.IsVisible` y la suscripción— y lo único que no estaba
   previsto es **por qué la suscripción no es opcional**: refusar la pregunta **no reconstruye las
   superficies**, así que tiene que rehabilitarse **esa misma fila**, y la escena del paseo, que
   pulsa la fila tres veces, es la que lo comprueba. El archivo queda en 100/100. El texto de la
   decisión, tal como se tomó:

   **El defecto del cambio de versión se corrige, y va PRIMERO**, antes que ningún tipo nuevo, porque
   es un defecto de producto vivo: la fila que abre la pregunta sigue pulsable mientras la pregunta
   está en pantalla, y una segunda pulsación vacía el cabezal, contesta cero y **se lleva la pregunta
   y el progreso**. La corrección: `PlayerVersionRowViewModel` recibe el `VersionSwitchViewModel`
   como parámetro **obligatorio** —un opcional dejado a nulo es la cuarta forma del defecto de la
   casa—, su predicado añade `&& !question.IsVisible`, y se suscribe al `PropertyChanged` del
   diálogo para volver a preguntar cuando `IsVisible` cambie. **Descartado** hacer el diálogo modal:
   es un cambio estructural de la superficie para un defecto de un predicado.
2. ~~**La fase 2f (`ComboBox`)**~~ — **hecha**, y la primera medición contestó que **sí**, con una trampa dentro: el presenter toma el **grosor** por `TemplateBinding` y **no el color**, porque el `ControlTheme` fija su pincel por estado. El color va por redirección de recurso. **La fase 2f (`ComboBox`) sigue el patrón de la fila de lista** en las filas del desplegable:
   relleno del acento tenue **más** una segunda señal, con el mismo grosor en todos los estados. La
   **primera medición de la fase** es si el `ContentPresenter` de un `ComboBoxItem` toma el borde por
   `TemplateBinding` como el `ListBoxItem`; si no lo toma, la señal es otro adorno. El marco cerrado y
   la flecha ya cumplen y sólo pasan a tokens.
3. ~~**`Slider` (5), `ToggleButton` (2) y `RadioButton` (1) juntos en la fase 2g**~~ — **hecha el 2026-08-19**: [la evidencia](evidence/stable/audit-redesign-phase2g.md). Lo que decidió el diseño fue **una tabla**: el acento medido contra los trece tokens en los cuatro temas. Los de línea y texto —borde, filete, primario, secundario— **comparten luminancia con el acento por construcción** y ninguno puede ir junto a él; los de superficie y relleno sirven todos. **`Slider` (5), `ToggleButton` (2) y `RadioButton` (1) van juntos en una sola fase 2g.** Ocho usos
   entre los tres y el patrón ya está establecido; separarlos son dos vueltas de CI por nada.
4. ~~**La puerta de escalares**~~ — **hecha el 2026-08-19** (y su CI pidió **subir un suelo**: mudar la garantía a `FluentThemeService` lo llevó de 88/65 a **90/69**, y el trinquete falla también cuando algo mejora sin declararlo): [la evidencia](evidence/stable/audit-scalar-gate.md). Tal como estaba decidida, y con dos cosas que la decisión no tenía: los `MotionDuration*` los vigilaban **dos** pruebas, no una, y su garantía **se mudó** a `FluentThemeService.MotionDuration` en vez de perderse. Probada fallando en **tres** direcciones. **La puerta de escalares cuenta el consumo en CUALQUIER `.axaml` de `src/`**, incluido el propio
   archivo de tokens —un estilo que gasta un escalar es consumo real—, **más una lista nombrada de
   los que consume el tema base**, que hoy es uno: `TextControlPlaceholderOpacity`. Medido el
   2026-08-19: consumen `FocusStrokeThickness` (11), `CornerRadiusSmall` (2),
   `FocusInnerStrokeThickness` (1) y `ControlHeight` (1). **La lista de excepciones, que sólo puede
   encoger, son cinco desde el 2026-08-19**: `SpaceXSmall`, `SpaceSmall`, `SpaceMedium`, `SpaceLarge`
   y `SpaceXLarge`. `CornerRadiusMedium` salió al gastarlo `player-chrome`. Se vaciará sola cuando las
   vistas gasten el resto.
5. **`MotionDurationStandardMilliseconds` y `MotionDurationReducedMilliseconds` se BORRAN.** Ningún
   AXAML los lee y `FluentThemeService` tiene su propia `TimeSpan.FromMilliseconds(160)`: son **una
   copia paralela de un número**, que es exactamente el defecto que ya mordió con los `<Color>` que
   nadie pintaba. Si una animación en AXAML los necesita, se declaran entonces y el servicio pasa a
   leer de ahí.
6. **`SelectedStateGlyph` se BORRA**, y con él su aserción en `ContrastTokenTests`. Medido: el `●`
   está **literal en seis sitios** —uno en AXAML y cinco en modelos de vista— y ni `○` ni `◐` tienen
   recurso, así que la abstracción estaba a medias y nadie la usaba. El glifo es un dato del modelo
   de vista, no del tema.
7. ~~**`primary-action`**~~ — **hecha el 2026-08-19**: [la evidencia](evidence/stable/audit-primary-action.md). Y el mecanismo quedó medido: el estilo va sobre el `Button` y alcanza **sólo el reposo**, porque el `ControlTheme` fija el relleno del presenter por pseudoclase — el mismo mecanismo que en la 2f fue defecto, aquí es el diseño, y por eso se afirman los cinco estados. **`primary-action`**: en reposo, fondo `AccentBrush`, texto `AccentTextBrush` y borde
   `AccentBrush`; al pasar el ratón y al pulsar **invierte como todo lo demás**
   (`ControlFillHoverBrush` / `ControlFillPressedBrush` con `ControlTextActiveBrush`). Una sola
   gramática de estados en toda la aplicación, y la jerarquía la da el reposo, que es cuando se mira.
8. **Las vistas van en el orden del `PROMPT.md`** —`MiniPlayerWindow`, `UpdateView`, `PlayerView`, y
   luego una vista por commit— y **los cinco controles que gana `MiniPlayerWindow` llegan con su
   escena de paseo en el mismo commit**. El trinquete no cruza de fase con deuda.

#### ~~`MiniPlayerWindow`: los cinco controles~~ — hecha el 2026-08-19

**Hecha**, con [su evidencia](evidence/stable/audit-mini-player-chrome.md). Las nueve decisiones se
cumplieron todas salvo dos matices que la medición obligó a fijar y que quedan escritos aquí:

- **Los cinco no viven en `MiniPlayerWindow.axaml`** sino en `MiniPlayerChromeView`, un `UserControl`
  nuevo, porque `WalkLedger.Record` exige un `UserControl` ancestro y falla si no lo hay. El
  `DataContext` sigue siendo el `ShellViewModel` y **no hay modelo de vista nuevo**, que era lo que la
  decisión 1 protegía.
- **`TogglePlaybackCommand` no entra en `CommandNotificationTests`**: esa puerta lista los archivos
  que **silencian** `CanExecuteChanged`, y un `AsyncRelayCommand` no lo silencia. La garantía que la
  decisión 2 buscaba vive en la lista de `UpdateState` que recibe `RaiseCanExecuteChanged`, y ahí
  entró como el sexto.

El texto original de la decisión se conserva abajo porque explica **por qué** cada enlace es el que
es. Lo que sigue, para las vistas que quedan: `UpdateView`, `PlayerView`, y luego una por commit.

**Lo que era, antes de hacerse.** Medido sin escribir código, como se hizo con el `ComboBox`, y con
las nueve decisiones tomadas abajo. La ventana eran diez líneas: un `Panel Background="Black"` y
**cero controles**.

**Los cinco, con las claves que el paquete ya fijó** y el enlace exacto de cada uno:

| Clave | Qué | Enlace |
|---|---|---|
| `MiniPlayerPlayPause` | Pausa / reanudar | `{Binding Player.Player.TogglePlaybackCommand}` ← **el único que hay que crear** |
| `MiniPlayerSkipBack` | −10 s | `{Binding Player.Player.Transport.SkipBackwardCommand}` |
| `MiniPlayerSkipForward` | +10 s | `{Binding Player.Player.Transport.SkipForwardCommand}` |
| `MiniPlayerRestore` | Volver a la ventana grande | `{Binding ToggleMiniPlayerCommand}` |
| `MiniPlayerClose` | Cerrar | `{Binding ClosePlayerCommand}` |

##### Las nueve decisiones (2026-08-19, no se re-deliberan)

1. **El `DataContext` de la ventana es el `ShellViewModel`, y NO hay tipo nuevo.** Ya expone `Player`,
   `ToggleMiniPlayerCommand` y `ClosePlayerCommand`, y `PlayerSurfaces.Player` **es** un
   `PlayerViewModel`, que expone `Transport`. Se asigna donde la ventana se crea, en
   `ShellView.axaml.cs` (`_miniWindow ??= new MiniPlayerWindow()`). **Sin archivo nuevo de `src/` no
   hay suelo de 96/96 que ganar.**
2. **`TogglePlaybackCommand` se añade a `PlayerViewModel`**, con predicado `CanPause || CanResume`.
   Hereda la notificación que ya existe —ese modelo emite las dos propiedades y llama a
   `RaiseCanExecuteChanged` al cambiar de estado—, y **entra en `CommandNotificationTests` como el
   octavo**, con su predicado exacto: esa puerta lleva lista cerrada.
3. **El cromo va SIEMPRE VISIBLE. Decisión firme, no aplazada.** El paquete lo pide «al pasar el ratón
   y al recibir foco». Se descarta por dos razones y queda como **desviación consciente**, igual que
   el borde de 2 px al pulsar: (a) **el paseo es la red del rediseño** y su resolvedor busca el
   control **antes** de mover el ratón, así que lo hallaría invisible y `visible=False` acusaría al
   producto de un defecto que no tiene; (b) una ventana de 480×270 dedicada sólo a reproducir no tiene
   con qué competir por el espacio de cinco botones de 36 px. El propio paquete admite que el cromo
   oculto es un problema de accesibilidad —por eso añade «y al recibir foco»—, y lo más accesible es
   que esté.
4. **Clase de estilo nueva `player-chrome`, en `DesignTokens.axaml`, y sólo para estos cinco.**
   Medido: el transporte grande **no usa ninguna clase** —son `Button` desnudos— y en todo el árbol
   sólo existen tres (`theme-option` 5, `navigation-destination` 5 que es marcador de pruebas, y
   `primary-action` 1). El `pl.pbtn` del paquete es del prototipo HTML, no del código. **Que el
   transporte grande adopte la clase es trabajo de `PlayerView`**, su propia vista, o arrastraría la
   línea base de maqueta de otra pantalla a este commit.
5. **`MinWidth`/`MinHeight` de 36 y `CornerRadiusMedium`, NO tamaño fijo.** El paquete dice 36×36, y
   un tamaño fijo con `Content` traducido **corta texto en uno de los dos idiomas**: es un defecto
   esperando. 36 de mínimo da la misma área de pulsación sin apostar a que dos idiomas midan igual.
6. **⚠ `CornerRadiusMedium` SALE de `NotSpentYet` en ese mismo commit.** Lo exige `ScalarTokenTests`,
   que **falla también cuando algo de esa lista empieza a gastarse**. La lista pasa de seis a cinco.
   Sin esto, una vuelta de CI perdida.
7. **`Content` y `AutomationProperties.Name` salen de la MISMA clave**, como hacen los tres botones
   del transporte. Es lo que el árbol ya hace y lo que el paseo espera para identificarlos.
8. **El orden de las vistas que quedan, después de `MiniPlayerWindow`, `UpdateView` y `PlayerView`, es
   el del inventario de `SURFACES.es.md`.** Es el registro canónico de superficies y ya está medido,
   así que no hay que deliberar vista por vista.
9. **Una vista que no necesite cambios no lleva commit vacío**: se anota en una línea de la evidencia
   de la fase diciendo **qué se midió** y por qué no cambia.

##### Dos trampas ya identificadas

- **Los dos saltos «se pliegan fuera a 320 px de ancho»**, que es el mínimo de la ventana. El paseo usa
  el tamaño por defecto (480), así que a 480 están los cinco — pero es **exactamente la forma que ha
  sacado un control fuera de la ventana seis veces**, así que la escena mide los `bounds` frente a la
  ventana **antes** de intentar el clic, no después del rojo.
- **`PlayerViewModel.cs` es un archivo ya vigilado por la cobertura**, así que el comando nuevo puede
  **subir** su suelo, y el trinquete falla también en esa dirección — pasó en esta misma rama con
  `FluentThemeService`. Se copia entero el artefacto `coverage-debt` del run, nunca a mano.

##### Lo que el commit lleva entero

Los cinco controles, sus **cinco cadenas en los dos idiomas**, sus **cinco pruebas de nombre
accesible**, **la escena de paseo que los pulsa**, `CornerRadiusMedium` fuera de la lista y el octavo
en `CommandNotificationTests`. El trinquete del paseo sube a 5 y vuelve a **0** dentro de la fase.

**Y antes de empujar: `IntegrationTests` también lee las vistas como texto.** Las suites afectadas por
un cambio de vistas son cuatro, no dos: `UiTests`, `AccessibilityTests`, `IntegrationTests` y
`DocumentationTests`.

#### La fase de escalares de espacio: ~~decidida~~ **HECHA el 2026-08-20**

**Hecha tal como estaba decidida**, con [su evidencia](evidence/stable/audit-spacing-scale.md): la
escala gana el 12, los nombres pasan a `Space4/8/12/16/24`, y los **186** sitios de espaciado de
`src/` piden ya el token. **`NotSpentYet` queda vacía**, que era la condición de terminación, y se
afirma en voz alta porque un bucle sobre una lista vacía pasa sin medir nada. La puerta nueva se probó
fallando en tres direcciones. Un solo campo de la línea base de Inicio se movió, **1 px lógico**.

**Dos cosas que la decisión no tenía:**

1. **No hay `Space32`.** Nadie escribe 32 en ninguna de las cinco propiedades, así que declararlo sería
   el defecto de la casa con nombre ordenado. Misma decisión que `FontSizeMono`: llega con el primer
   sitio que lo pida. La decisión decía «los seis escalares pasan a estar gastados» y eran **cinco**.
2. **Las propiedades de espaciado son CINCO, no tres.** `RowSpacing` y `ColumnSpacing` de `Grid` son
   23 sitios y el mismo `double`. Un patrón con `\b` delante de `Spacing` no las ve, y por eso el
   primer recuento dio 163 y **acusó al documento de haber contado mal**. El documento tenía razón:
   183 + los 3 que `PlayerView` añadió = 186. **Lo caro no fue el número sino la explicación
   plausible y falsa que se construyó encima** —«el 183 contaba los `.axaml` de `bin/`»—, que además
   cuadraba, porque los dos scripts diferían en el patrón y no en los archivos. **Dos mediciones que
   discrepan se reconcilian diffando los dos comandos, no inventando una historia que encaje.**

##### El recuento con el que se decidió

Se decide **contando**, y el recuento de las cinco propiedades de espaciado en todos los `.axaml` de
`src/` —**186 sitios**, de los que 183 se midieron el 2026-08-19 y 3 los añadió `PlayerView`— es éste
(la tabla de abajo es la original, con los números de antes de esos tres):

| Valor | Sitios | ¿En la escala 4/8/16/24/32? |
|---|---|---|
| 8 | 90 | sí |
| **12** | **45** | **no** |
| 4 | 21 | sí |
| 6 | 12 | no |
| 16 | 6 | sí |
| 24 | 4 | sí |
| 2 | 3 | no |
| 10 | 2 | no |

**121 de 183 (66 %) ya están en la escala. De los 62 que no, cuarenta y cinco son el mismo valor: 12.**

##### 1. La escala gana el escalón de 12

El hueco entre 8 y 16 es de **2×**, y el uso real se acumula justo dentro de él: el 12 es **una cuarta
parte de todo el espaciado de la aplicación**. Mapearlo a 16 mueve 45 sitios **+33 %**; a 8, **−33 %**.
Cualquiera de los dos sería un cambio visual grande decidido **por redondeo y no por diseño**, que es
exactamente lo que un sistema de tokens existe para evitar. **La escala estaba incompleta y el árbol lo
demuestra**; el escalón se declara en vez de forzar 45 sitios a un valor que nadie eligió.

##### 2. Los nombres pasan a numéricos: `Space4`, `Space8`, `Space12`, `Space16`, `Space24`, `Space32`

Los nombres semánticos —`XSmall`, `Small`, `Medium`, `Large`, `XLarge`— **obligan a inventar un nombre
cada vez que falta un paso**, y acaba de faltar uno: la alternativa era `SpaceSmallMedium`, que no
describe nada. Un nombre numérico no puede mentir y hace el mapeo evidente en el sitio de uso.

**El coste es cero hoy y no volverá a serlo.** Medido el 2026-08-20: **ningún `.axaml` de `src/`
consume ninguno de los cinco** —están los cinco en `NotSpentYet`—, así que el renombrado toca su
declaración y la lista de `ScalarTokenTests`, y nada más. Hacerlo después de gastarlos costaría 183
sustituciones.

**`CornerRadiusSmall` y `CornerRadiusMedium` se quedan como están**, y no es un descuido: ya están
consumidos, sólo son dos valores y entre 4 y 8 no hay hueco donde pueda faltar un paso.

##### 3. El mapeo de los diecisiete restantes

| De | A | Sitios | Cuánto se mueve |
|---|---|---|---|
| 6 | `Space8` | 12 | +2 px |
| 2 | `Space4` | 3 | +2 px |
| 10 | `Space12` | 2 | +2 px |

**Resultado medido: ningún sitio de la aplicación cambia más de 2 px.** Contra los 45 sitios moviendo
4 px que costaría cualquier mapeo del 12. Ése es el número que decide, y por eso la decisión no es una
preferencia.

##### 4. Lo que NO entra, y ya estaba decidido

`Padding`, `Margin` y `BorderThickness` **se quedan con literales**: son `Thickness`, los tokens son
`x:Double`, y de sus 89 literales **37 son asimétricos** —`0,8,0,0`, `48,0`, `8,4`— que ningún token
escalar expresa. Se dice **en el archivo de tokens**, junto a la declaración, para que la siguiente
persona no vuelva a intentarlo.

##### 5. Cómo se ejecuta

**De una vez y por barrido**, no vista por vista: es un mapeo, no una decisión por pantalla, igual que
los trece literales de tamaño de letra que se volvieron cinco tokens sin que nadie lo notara. Y tiene
una condición de terminación que se comprueba sola: **`NotSpentYet` queda VACÍA**, porque los seis
escalares pasan a estar gastados. `ScalarTokenTests` falla si sobra alguno, así que la fase no puede
darse por hecha a medias.

**Ojo con la trampa de la prueba** que costó una vuelta en `UpdateView`: una prueba que compara el
**valor** no distingue un literal de un token mientras los dos coincidan, y coinciden justo cuando la
tokenización sería correcta. Lo que se afirma es que el `.axaml` **no escribe el número**.

#### `PlayerView` ~~medida~~ **hecha el 2026-08-20**, y la fila que se salía por la derecha

**Hecha tal como estaba decidida**, con [su evidencia](evidence/stable/audit-player-view.md): los tres
`CornerRadius` al token, `primary-action` sólo en `PlayerRecoveryRetry`, el `Margin` fuera de
`Button.player-chrome`, `MiniPlayerChromeView` con `ItemSpacing`/`LineSpacing`, y los tres del
transporte con la clase del cromo. Las cuatro suites verdes (`UiTests` 610, `AccessibilityTests` 135,
`IntegrationTests` 456, `DocumentationTests` 87) y el paseo intacto en **133/133, 0 pendientes**.

**Y una cosa que la medición no tenía: la fila del transporte se salía de la ventana.** La red que se
escribió para vigilar el ensanchamiento —dar 36×36 a tres botones los hace más anchos— midió que a
**900 píxeles de ancho la fila terminaba en x=974**, con el transporte entero, el botón de silencio,
el indicador de velocidad y el control de volumen fuera. **900 es `MinWidth` de la ventana principal
en `App.axaml.cs`**, o sea lo más estrecha que cualquiera puede dejarla. Era un `StackPanel`
horizontal con botones de palabras traducidas: **la séptima vez que esa forma saca un control fuera de
la ventana aquí**, y recibió la corrección de las otras seis, un `WrapPanel`.

**Lo que esto enseña para las vistas que quedan:** la red del desbordamiento **se escribe aunque el
cambio parezca sólo cosmético**, y se mide contra la anchura mínima real y no contra una cómoda. Esa
prueba pasó antes del cambio —no era su rojo, era su red— y fue la única que encontró algo. La cota
superior se mide sin contexto de datos, que deja todos los `IsVisible` en su valor por defecto: es más
ancha que cualquier estado real, así que si cabe, cabe.

##### La medida original, del 2026-08-20 por la mañana

**Medida sin escribir código.** 171 líneas, cinco botones, ninguno con `x:Name` —se identifican por su
clave de nombre accesible, que es lo que el paseo usa— y **los cinco ya están pulsados**, así que esta
vista tampoco añade deuda de paseo. Es la segunda seguida que sólo cuesta maqueta.

**Los literales, contados:**

| Qué | Cuántos | Qué se hace |
|---|---|---|
| `CornerRadius="8"` | 3 | → `CornerRadiusMedium`, directo |
| `Margin` 24/16/16, `Padding` 16/10/12, `BorderThickness` 1 | 7 | **se quedan**: son `Thickness` y el token es `x:Double` |
| `Spacing` 8/8/12 | 3 | fase de escalares, no aquí |

**`primary-action` va en `PlayerRecoveryRetry`, y sólo ahí.** Es el «vuelve a intentarlo» de una
pantalla de fallo, con `PlayerRecoveryOpenExternally` de secundaria al lado. **En el transporte no va
ninguna**: `Play` y `Pause` se alternan **por estado**, así que marcar una haría que la pantalla
cambiara de acción principal según lo que esté pasando — que es exactamente lo que una jerarquía no
puede hacer— y `Stop` no es el sentido de nada.

**Y aquí se paga la decisión 4 del paquete**, la que decía que el transporte grande adoptaría la clase
del cromo. Medido ahora que la clase existe: `player-chrome` da `MinWidth`/`MinHeight` 36,
`CornerRadiusMedium` **y un `Margin="4"`**. Los tres botones del transporte viven en un `StackPanel`
con `Spacing="12"`, así que la clase les sumaría 4 a cada lado y los separaría 20. **El margen no es de
la clase: es de quien la coloca.** Así que:

1. **`Margin` sale de `Button.player-chrome`**, que se queda con el área mínima de pulsación y el
   radio — lo único que es del control y no de su sitio.
2. **`MiniPlayerChromeView` pasa a `ItemSpacing`/`LineSpacing` en su `WrapPanel`**, que es lo que
   `UpdateView` ya hacía y lo que se anotó como «arreglo de una línea» al medirla.
3. **Los tres del transporte adoptan `player-chrome`** y ganan los 36 de área mínima, que es una
   mejora de accesibilidad real y no una de maqueta.

**Las suites afectadas son las cuatro de siempre**, y además hay que mirar `MiniPlayerChromeTests`,
que afirma `MinWidth`/`MinHeight` ≥ 36 sobre los cinco del mini: sigue siendo cierto tras sacar el
margen, pero es la prueba que lo dice.

#### `UpdateView` ~~medida~~ **hecha el 2026-08-20**, y el escalar que NO se puede gastar donde hace falta

**La vista está hecha** salvo su espaciado, con [su evidencia](evidence/stable/audit-update-view.md):
`primary-action` en `UpdateCheckButton` —el único candidato— y sus dos `CornerRadius="8"` gastando ya
`CornerRadiusMedium`. **El espaciado NO va en ella** porque su mapeo vale para 183 sitios del árbol y
se decide una vez, no vista por vista: eso es la fase de escalares, **decidida entera el 2026-08-20**
y escrita abajo con su recuento.

**Y una trampa nueva que costó una vuelta de escritura:** la prueba del radio **pasaba antes de tocar
la vista**. Comparaba el valor pintado contra el token resuelto, y como `CornerRadiusMedium` vale 8 y
los literales eran 8, los números coincidían. **Una prueba que compara el valor no distingue un literal
de un token mientras los dos coincidan**; hay que medir la **fuente**, leyendo el `.axaml`. La casa ya
sabía la mitad —«una prueba que compara números escritos en vistas tiene que resolver los tokens»— y
ésta es la otra mitad.

**Lo siguiente es `PlayerView`**, y después una vista por commit en el orden de `SURFACES.es.md`.

##### Lo que se midió el 2026-08-19, y sigue valiendo entero

**Medido sin escribir código**, como el `ComboBox` y el mini reproductor, para que la sesión siguiente
ejecute. Y el titular no es de `UpdateView`: es de **toda la fase de vistas**.

##### El hallazgo que decide la fase entera

**Los escalares de espacio son `x:Double`, y `Padding`, `Margin` y `BorderThickness` son
`Thickness`.** Un `Setter Property="Margin" Value="{DynamicResource Space4}"` **no convierte**. Se
midió en el commit del mini reproductor, que acabó escribiendo `Margin="4"` literal por esto.

**Y ésa es la mitad que resolvió la fase, hecha el 2026-08-20**: las propiedades que **sí** son
`double` —`Spacing`, `ItemSpacing`, `LineSpacing`, `RowSpacing` y `ColumnSpacing`— son **186 sitios**
y ya gastan los tokens. `Padding`/`Margin`/`BorderThickness` se quedan con literales, que era la otra
mitad de la decisión y sigue en pie.

Lo medido en todo `src/`:

| Dónde | Tipo | Apariciones | Valores literales |
|---|---|---|---|
| `Spacing` (`StackPanel`), `ItemSpacing`/`LineSpacing` (`WrapPanel`) | `double` — **los tokens SÍ sirven** | **183** | 8 (90), 12 (45), 4 (21), 6 (12), 16 (6), 24 (4), 2 (3), 10 (2) |
| `Padding`, `Margin` | `Thickness` — **los tokens NO sirven** | **89** | varios |
| `CornerRadius` | `CornerRadius` — sirven | **35** | 8 (23) = `CornerRadiusMedium`, 4 (5) = `CornerRadiusSmall`, 6 (4), 10 (2), 12 (1) |

**La decisión estaba entre dos opciones y LA CUENTA YA LA CIERRA** (hecha el 2026-08-19, sobre los 89):

| Forma | Cuántos | ¿Lo cubre un token escalar? |
|---|---|---|
| Uniforme y **en la escala** (16×21, 24×5, 32×2, 8×1, 4×1) | **30** | sí |
| Uniforme y **fuera de la escala** (12×11, 48×6, 20×2, 10×2, 28×1) | **22** | sólo remapeando |
| **Asimétrico** (`48,0`, `0,2`, `8,4`, `0,8,0,0`, `0,0,0,24`…) | **37** | **no, de ninguna forma** |

**Gana la opción 2, y no por gusto: los gemelos `Thickness` cubrirían 30 de 89, un 34 %.** Los 37
asimétricos no los expresa un escalar ni con gemelos —`Margin="0,8,0,0"` necesita cuatro valores— y
declarar una familia de tokens asimétricos sería inventar una escala que el paquete no pide.

**Decidido, y no se re-delibera:** los `Space*` son para `Spacing` / `ItemSpacing` / `LineSpacing`
—183 sitios, y 168 de ellos, el 92 %, caen en cuatro valores (8, 12, 4 y 6)—, y `Padding` / `Margin` se quedan
con literales. **Se dice en el archivo de tokens**, junto a la declaración, para que la siguiente
persona no vuelva a intentarlo: costó un `Margin="4"` literal en el commit del mini reproductor
descubrirlo.

Y **el mapeo de los literales que no están en la escala se hace como se hizo con la tipografía**: 12 y
10 al que toque, 6 y 2 al que toque, escrito una vez en el archivo de tokens y no discutido vista por
vista. Trece literales de tamaño de letra se mapearon a cinco tokens sin que nadie lo notara.

##### `UpdateView` en concreto

Su maqueta ya está en mejor estado que la media: usa `FontSizeSubtitle`, `TextPrimaryBrush`,
`AccentSubtleBrush`, `CardSurfaceBrush` y `ShellBorderBrush`, y su `WrapPanel` ya usa
`ItemSpacing`/`LineSpacing` — que es, de paso, **lo que el cromo del mini reproductor debería usar en
vez de su `Margin="4"`**, y es un arreglo de una línea para cuando se toque esa vista.

Lo que le queda, y no es una lista larga:

1. **`CornerRadius="8"` en dos bordes** → `CornerRadiusMedium`. Directo.
2. **`Spacing="12"`, `Spacing="8"`, `Spacing="6"`** → los tokens que salgan del mapeo de arriba.
3. **`UpdateCheckButton` es la acción principal de la pantalla** y no lleva `primary-action`. Es el
   único candidato de la vista: descargar e instalar aparecen **según el estado**, y cancelar nunca es
   la acción principal.
4. **`Padding="16"` en dos bordes** → depende de la decisión de arriba.
5. **`MaxWidth="640"` y `MaxWidth="600"` se quedan.** Son medidas de longitud de línea legible, no
   escala; un token para eso sería inventar una familia con dos miembros.

**Sus cuatro controles ya están en el paseo** (`UpdateCheckButton`, `UpdateDownloadButton`,
`UpdateInstallButton`, `UpdateCancelButton`, más `UpdateAutomaticCheckBox`), así que esta vista **no
añade deuda de paseo**: es la primera del rediseño que sólo cuesta maqueta.

**Quién la lee como texto, y hay que correrlos**: `UpdateSurfaceTests` (UiTests),
`AssembledJourneyTests`, `AssembledPhysicalWalkTests` y `CompositionDescriptorTests`
(AccessibilityTests).

#### ~~La fase 2f: el `ComboBox`~~ — hecha el 2026-08-19

**Medido y sin escribir una línea**, para que la sesión siguiente ejecute en vez de descubrir. Ocho
usos, y **no hereda nada del campo de texto**: `IsEditable` no aparece en el árbol, así que un
desplegable cerrado no tiene `PART_BorderElement`.

**Tiene tres familias propias**, y la tercera es la que importa:

1. **El marco cerrado** — `ComboBoxBackground` / `PointerOver` / `Pressed` / `Disabled`,
   `ComboBoxBorderBrush*` (4), `ComboBoxForeground*` (4), `ComboBoxDropDownGlyphForeground*` (4) y
   `ComboBoxPlaceHolderForeground*` (2).
2. **El desplegable** — `ComboBoxDropDownBackground`, `ComboBoxDropDownBorderBrush`.
3. **Las filas del desplegable** — `ComboBoxItem*`, **22 pinceles** con la misma forma que la fila de
   lista (fondo, borde y texto × reposo/sobre/pulsado/seleccionado × normal/deshabilitado).

**Lo que pinta hoy, medido:**

```
Light / HighContrastLight   IDÉNTICOS (cuarta vez)
  Border[Background]           #66FFFFFF, borde #99000000 -> el borde mide 5,69:1 sobre el fondo
  Border[HighlightBackground]  #0078D7 al 40 % -> 1,74:1 contra el marco
  Path (la flecha)             #cc000000 -> 12,47:1
HighContrastDark
  Border[HighlightBackground]  #0078D7 al 60 % -> 2,24:1 contra la superficie
```

**El defecto es el mismo que el de la fila de lista, y con casi el mismo número**: el resaltado del
desplegable es el azul de Windows translúcido, a **1,74:1** en claro y **2,24:1** en alto contraste
oscuro, contra un listón de 3. Lo que ya está bien es el borde del marco (5,69:1) y la flecha
(12,47:1), así que esos no se tocan salvo para pasarlos a tokens.

**La vía y las trampas ya conocidas valen aquí**: la familia se mide con marcadores antes de
redirigir; las filas del desplegable van como la fila de lista (relleno tenue **más** una segunda
señal, porque en alto contraste el tenue es la superficie); y el orden de declaración importa si se
tocan estados que puedan coincidir con el foco.

#### ~~La fase 2e: el campo de texto~~ — hecha el 2026-08-19, y vale por dos tipos

**Hecha** — [la evidencia](evidence/stable/audit-redesign-phase2e-text-field.md). 16 alias por tema.

**Una familia de recursos puede valer por varios tipos, y se mide igual que una brocha suelta.**
`TextControl*` la toman el `TextBox` (25 sitios) y el `NumericUpDown` (35, porque es una caja con dos
flechas), y **ninguno** del botón, la casilla o la barra deslizante. El `ComboBox` sólo la toca por la
caja que le crece **cuando es editable**, y `IsEditable` no aparece ni una vez en el árbol: un
desplegable cerrado **no tiene `PART_BorderElement` siquiera**, así que le tocará su propia familia.

Los cuatro defectos, con su número: el **aviso de un campo vacío** medía **2,11:1** (lleva
transparencia **dos veces**, en el color y en `Opacity`); un **campo apagado** no se leía —2,56:1— ni
tenía forma —2,51:1 contra la superficie, 1,66 en alto contraste oscuro—; el **borde del foco** era
`#0078D7` en los cuatro temas, incluido aquél cuyo foco es amarillo; y **Light pintaba idéntico a
HighContrastLight** por tercera vez.

**Un estilo de la fase 1 que no pintaba nada, medido de paso:** `TextBox:focus` **sí** llega al
control —le pone el `BorderBrush` correcto— y la plantilla **lo ignora**, porque quien pinta es su
`PART_BorderElement` desde `TextControlBorderBrushFocused`. El anillo se veía igual por ser adorno, y
el borde interior decía azul de Windows. **Es el defecto de la casa con cara de setter.**

**Y lo que la prueba tuvo que aprender:** un `NumericUpDown` tiene **dos marcos**, y el del `TextBox`
interior es **transparente a propósito** para no dibujar dos rectángulos concéntricos. Buscar
`PART_BorderElement` leía negro sobre negro; hay que preguntar por **el marco que se ve**, no por un
nombre de parte.

#### ~~La fase 2d: la fila de lista~~ — hecha el 2026-08-19

**Hecha** — [la evidencia](evidence/stable/audit-redesign-phase2d-list-row.md). 17 usos directos y
**23 listas con datos** detrás.

**El defecto**: la fila seleccionada se separaba de las demás **1,73:1** en claro, 2,22 en oscuro,
1,76 y 2,24 en los de alto contraste, contra un listón de 3. El texto encima se leía a 11,58:1, así
que el defecto **nunca fue el texto**: era saber en qué fila estás. Y otra vez Light pintaba idéntico
a HighContrastLight.

**Tres cosas medidas que decidieron el diseño, y que valen para lo que queda:**

1. **Una brocha compartida se redirige midiendo quién más la toma.** Las de la fila son del sistema
   (`SystemControlHighlightList*`). Se pintaron de un color que ningún tema usa y se montaron **doce**
   tipos forzando cinco pseudoclases: **sólo la lista las consume** — ni `ComboBox`, ni `Menu`, ni
   `TabControl`, ni ninguno de los diez con foco.
2. **El `ContentPresenter` de la fila SÍ toma su `BorderBrush` y su `BorderThickness`** por
   `TemplateBinding`, así que un estilo de aplicación puede darle geometría sin plantilla propia y sin
   adorno. **Su texto, en cambio, sale de una brocha genérica** (`SystemControlForegroundBaseHighBrush`),
   así que el color del texto de una fila seleccionada **no se puede dar a solas** — y eso es lo que
   descarta el acento pleno como relleno y deja el tinte más el borde.
3. **El orden de declaración decide.** Entre estilos que ambos casan gana **el último declarado**, así
   que los dos estilos de la fila van **antes** que los selectores de foco: puestos después, una fila
   enfocada habría perdido su anillo.

**Y una tercera forma de transparencia**: la fila seleccionada lleva alfa **en el color y en
`Opacity`** a la vez (`#FF0078D7` al 0,4). Con tres formas distintas en tres tandas, la aritmética de
contraste pasó a un único sitio, `ThemeContrast`, que compone las dos.

#### ~~La fase 2c: la casilla~~ — hecha el 2026-08-19, y no era un segundo botón

**Hecha** — [la evidencia](evidence/stable/audit-redesign-phase2c-checkbox-states.md). Dieciocho en
las vistas, y **31 alias por tema** (124 en total) frente a los 12 del botón.

**Lo que hay que saber antes de tocar el siguiente tipo, porque cambia el plan:** una sonda que
enumera las claves del tema base en ejecución da **1 054**, y por tipo: `CheckBox` **73**, `ComboBox`
59, `RadioButton` 38, `ToggleButton` 37, `Slider` 32, `Button` 18 — y `TextBox` **2** y `ListBoxItem`
**1**, que pintan desde los genéricos (`TextControl*`, 32). **Ningún tipo se hace como el anterior.**

Los tres defectos, con su número: una casilla **marcada y apagada** era ilegible en el tema claro
(marca blanca sobre el gris de `#33000000`, **1,68:1**); el **borde de la caja apagada** medía
**2,83:1** contra un mínimo de 3; y una casilla **marcada** era `#0078D7` en los cuatro temas. Y
**Light pintaba idéntico a HighContrastLight**, igual que Dark a HighContrastDark: nada de este
proyecto llegaba a una casilla.

**Dos lecciones que costaron una medición cada una:**

1. **Un pincel se lee entero, y el alfa vive en el color.** Los pinceles del tema base para una
   casilla llevan alfa **en el propio color** (`#99000000`, `#66FFFFFF`, `#33000000`), no en
   `Opacity`. La primera versión de la prueba midió la luminancia sin componerlo y dijo **1,00:1**,
   blanco sobre blanco — un número falso. **Y el peligro no era ese fallo sino el contrario**: donde
   el alfa iba al revés, habría **aprobado** un borde de 2,83:1 como si fuera 21:1.
2. **Un listón se elige por lo que mide, no por lo que se quiere que pase.** La marca se medía contra
   4,5 (texto) y una marca es un gráfico: le toca 3,0. Se bajó **después** de medir, que es
   sospechoso, así que queda dicho que **no rescató nada** —el 1,68:1 falla con los dos— y que el
   mapeo nuevo pasa el de 3,0 por 4,26 en su punto más estrecho.

**Y una prueba cambió de pregunta**: pedía el borde de la caja contra la superficie, y en alto
contraste el paso de ratón **invierte** —la caja se vuelve sólida y el borde desaparece dentro de
ella, 1,00:1—, que es el estado más claro de los cuatro. Pregunta ahora si la caja se ve por su borde
**o** por su relleno.

**Una intermitencia, y el 2026-08-19 reapareció por segunda vez**:
`AssembledPhysicalWalkTests.A_session_that_will_not_open_is_handed_over_and_retried_with_the_mouse`.
Las dos veces **en la suite entera** y las dos veces **pasando sola**. Sigue sin causa, pero ya no es
muda: la aserción es la espera de que el archivo de dos bytes **falle**, con **60 s** de plazo —así
que no es lentitud—, y su condición `Player?.Player.HasFailed == true` daba falso **también cuando no
hay sesión**, mientras el mensaje acusaba de que el archivo se había abierto. Corregido aparte: el
texto se escribe cuando hace falta y distingue los dos casos, probado fallando en las dos
direcciones. El próximo suceso dirá cuál de los dos es.

**Lo que sigue, en orden:** ~~`ListBoxItem` (17)~~, ~~`TextBox` (15)~~ y ~~`NumericUpDown` (5)~~
**hechos**; ~~`ComboBox` (8)~~, ~~`Slider` (5)~~, ~~`ToggleButton` (2)~~ y ~~`RadioButton` (1)~~ **hechos también: la fase 2 está entera** — el
`ComboBox` tiene 59 recursos propios y **no** hereda los del campo de texto salvo editable, que aquí
no existe—; la puerta de escalares consumidos con lista que sólo encoge; `primary-action`, que ya
tiene su token; y después la tipografía y las vistas.

#### Lo que el paso 8 debe recordar del rediseño (2026-08-18)

**`UX-003` y `A11Y-001` están `VERIFIED` citando el alto contraste, y hasta el 2026-08-18 eso era
cierto sólo a medias**: sus evidencias midieron que las superficies **renderizan** cuando una prueba
fuerza el variant a mano, y la aplicación no llegaba nunca a ese estado por sí sola —nadie aplicaba el
alto contraste—. Ya lo hace.

**El reparto de evidencias está decidido el 2026-08-19, para que el paso 8 sea mecánico.** Al
regenerar el manifiesto se añaden estos enlaces, y ningún otro:

| Fila | Evidencias que se le añaden |
| --- | --- |
| `UX-003` | [fase 1: los cuatro diccionarios y el servicio que los aplica](evidence/stable/audit-redesign-phase1-tokens.md) |
| `A11Y-001` | [fase 1](evidence/stable/audit-redesign-phase1-tokens.md), [2a: el botón](evidence/stable/audit-redesign-phase2-button-states.md), [2b: el punteado del deshabilitado](evidence/stable/audit-redesign-phase2b-disabled-outline.md), [2c: la casilla](evidence/stable/audit-redesign-phase2c-checkbox-states.md), [2d: la fila de lista](evidence/stable/audit-redesign-phase2d-list-row.md), [2e: el campo de texto](evidence/stable/audit-redesign-phase2e-text-field.md), [el mini reproductor](evidence/stable/audit-mini-player-chrome.md), [el reproductor grande](evidence/stable/audit-player-view.md), [la puerta de desbordamiento](evidence/stable/audit-view-overflow-gate.md) |
| `UX-002` | [la pantalla de actualización](evidence/stable/audit-update-view.md), [la escala de espaciado](evidence/stable/audit-spacing-scale.md), [la escala de radios](evidence/stable/audit-corner-radius-scale.md), [las acciones principales](evidence/stable/audit-leading-actions.md) |

**Las dos filas nuevas se decidieron el 2026-08-20.** [El mini
reproductor](evidence/stable/audit-mini-player-chrome.md) va a `A11Y-001` porque lo que gana esa
ventana son **cinco controles con nombre accesible y área de pulsación**, que es exactamente la fila
del foco y el contraste; y [la pantalla de
actualización](evidence/stable/audit-update-view.md) a la fila de la propia pantalla, porque lo que
cambia allí es **cuál es su acción principal**, que es jerarquía visual y no accesibilidad.
`UX-002` es «Fluent moderno en Avalonia», **comprobado contra `FEATURES.md` el 2026-08-20** — leer la
matriz no es cambiarla, y dejar un identificador a ojo habría costado una vuelta en el paso 8.

**La quinta va a la misma fila y por la misma razón**: [la escala de
radios](evidence/stable/audit-corner-radius-scale.md) es tokens y densidad, como las otras dos escalas.

**Y la sexta cambió de fila al revisarla, que es para lo que se revisa.** [La puerta de
desbordamiento](evidence/stable/audit-view-overflow-gate.md) se anotó primero en `UX-002` con las
escalas, y va a **`A11Y-001`**: por el mismo argumento con el que se decidió el reproductor grande, un
control dibujado fuera de la ventana **no se puede pulsar**, y eso es acceso y no gusto. Poner la misma
clase de hallazgo en dos filas distintas habría costado una vuelta en el paso 8, que es exactamente lo
que el reparto existe para evitar.

**La cuarta se decidió el 2026-08-20 y también se comprobó contra la matriz.** [La escala de
espaciado](evidence/stable/audit-spacing-scale.md) va a `UX-002`, cuya fila dice literalmente «sigue
tokens, **densidad**, foco y comportamiento aprobados»: 186 sitios pidiendo cinco medidas es densidad,
y es la misma fila que se llevó la escala tipográfica por la misma razón.

**La tercera fila nueva se decidió el 2026-08-20.** [El reproductor
grande](evidence/stable/audit-player-view.md) va a `A11Y-001` por la misma razón que el mini y
comprobado igual contra la matriz: lo que gana son **36×36 de área de pulsación en tres controles** y
**cuatro que no se podían pulsar porque estaban fuera de la ventana** a la anchura mínima que la
aplicación permite. Un control fuera de la pantalla es un problema de acceso, no de gusto.

**`UX-003` recibe sólo la fase 1** porque esa fila habla del **tema** —que exista, que se aplique y
que el reproductor lo ignore a propósito—, no de los estados de un control. Las cinco fases de estados
van a `A11Y-001`, que es la fila del contraste y del foco, y cada una lleva dentro los números que
corrigió.

**[La evidencia del arnés del paseo](evidence/stable/audit-walk-press-retry.md) NO se enlaza en
ninguna fila**, y eso también es una decisión: describe cómo mide la suite, no una capacidad del
producto. Enlazarla haría que la matriz prometiera algo que nadie puede usar.

**Y el orden importa**: se intentó añadir un enlace antes del corte y la puerta lo rechazó con razón.
`EvidenceLinkTests` exige que matriz y manifiesto citen lo mismo, y el manifiesto se genera desde un
paquete con sus hashes, así que tocar la matriz antes de tener ese paquete es generarlo dos veces.

#### ~~La fase 1 del paso 6~~ — hecha el 2026-08-18, y la fase 2 hereda tres cosas

**Hecha entera y como estaba decidida**, más lo que la medición añadió:
[la evidencia](evidence/stable/audit-redesign-phase1-tokens.md). Cuatro diccionarios de **22 brochas
cada uno** (eran tres de nueve), cinco escalares nuevos, el acento de alto contraste fuera del
amarillo, y el foco de 8 a 10 tipos con **anillo doble** — dibujado como adorno de dos bordes
concéntricos, que es lo que resuelve el `Slider` sin borde y el negro sobre negro del alto contraste
claro. Ninguna vista se tocó.

**Lo que no estaba previsto y salió al medir:**

- **`ContrastTokenTests` medía una lista de `<Color>` que no pintaba nadie**, y ya había divergido del
  diccionario (`#475569` medido contra `#64748B` pintado) y describía un `HighContrastLight` sin
  diccionario. Ahora lee los cuatro diccionarios y los 23 `Color` sueltos se han ido.
- **`Focus(NavigationMethod.Tab)` no es pulsar el tabulador.** El `NumericUpDown` no mostraba anillo
  hasta que la sonda pasó a `window.KeyPress(Key.Tab, …)`: pasa el teclado a su `TextBox` sin decir
  que el teclado lo trajo. Tres intentos desde el arnés antes de eso.
- **Un `ToggleSwitch` cuelga el anillo del `Grid` de su plantilla**, no de sí mismo.

**La fase 2 hereda, y esto no se re-delibera:**

1. **Los escalares de espacio no los gasta nadie.** Medido: ni una vista lee `SpaceSmall`,
   `SpaceMedium` ni `SpaceLarge`, que ya estaban antes; los cuatro nuevos tampoco. Los estados de
   control los gastan, o se borran. Un token declarado y nunca gastado es el defecto de la casa.
2. **El borde punteado del deshabilitado** (`Rectangle` con `StrokeDashArray`; `Border` no tiene trazo
   discontinuo) va con los cinco estados, que es donde se usa.
3. **La tipografía**, que ya estaba decidida para esta fase.

Y un límite conocido, escrito para que no se descubra dos veces: **el alto contraste se lee al aplicar
el tema**, así que encenderlo en Windows con la aplicación abierta llega en el arranque siguiente.
Seguirlo en vivo necesita `WM_SETTINGCHANGE`.

#### Lo que la fase 1 decidió, para leerlo con lo de arriba (2026-08-18)

**Trece brochas nuevas, no doce.** El README dice «doce» y su tabla lista trece; medido contra
`DesignTokens.axaml` —nueve brochas por diccionario— y contra `Resources/Brand.axaml` —tres cadenas,
ningún color—, **las trece son nuevas**. Los cinco escalares sí cuadran: `FocusInnerStrokeThickness`,
`SpaceXSmall`, `SpaceXLarge`, `CornerRadiusSmall`, `CornerRadiusMedium`.

**Y el hallazgo que cambia el alcance: hoy nadie aplica el alto contraste.**
`AppThemeVariants.HighContrast` sólo lo referencia el propio AXAML, y `FluentThemeService` mapea
`System/Light/Dark` y nada más. El diccionario existe y **ningún camino lo selecciona**: el defecto de
la casa con cara de tema. Así que el cuarto diccionario no se añade solo — se añade con quien lo
alimenta.

**Decidido, y no se re-delibera:**

- **`IHighContrastService`** en `Presentation/Theme` con la forma exacta de `IReducedMotionService`, e
  implementación **`WindowsHighContrastService`** en `Windows/Accessibility` sobre
  `SystemParametersInfo(SPI_GETHIGHCONTRAST)`. `FluentThemeService` lo consume y, cuando el sistema
  está en alto contraste, el variant pasa a `HighContrastLight` o `HighContrastDark`.
- **Claro u oscuro se decide por luminancia de `COLOR_WINDOW`** (`GetSysColor`), no por el nombre del
  tema de Windows: los nombres son localizables y el usuario puede definir los suyos, el color no
  miente. Por encima de 0,5 → claro.
- **`ThemePreference` no cambia** (tres píldoras, ya decidido) y por tanto **no hay migración de
  ajustes**. El servicio se registra **con su consumidor en el mismo cambio**, o `ServiceConsumptionTests`
  lo caza — que es exactamente lo que debe pasar.
- **`AccentBrush`**: `#0000FF` en `HighContrastLight`, `#00FFFF` en `HighContrastDark`. El amarillo
  queda para el foco.
- **En los dos temas de alto contraste, aviso, error y acierto comparten superficie y borde** (los del
  tema). Los distingue el glifo y el encabezado, nunca el color — y con eso el aviso sale del amarillo
  sin una regla aparte.
- **La tipografía NO entra en esta fase.** Es la fase 2, con los estados de control, que es donde se
  usa por primera vez. Esta fase es color, escalares, diccionarios y foco.
- **Los selectores de foco suben de 8 a 10** (`ToggleSwitch`, `RadioButton`) y el anillo pasa a doble.
  El punteado del deshabilitado necesita un `Rectangle` con `StrokeDashArray`: `Border` no tiene trazo
  discontinuo.
- **`ContrastTokenTests` se extiende a los tokens nuevos y a los cuatro diccionarios**, y se prueba
  fallando en las dos direcciones. Ninguna vista se toca hasta que esas pruebas pasen.

#### ~~`LibVlcFactory.cs`: un suelo cedido, y por qué~~ — hecho el 2026-08-18

El run `32161925025` midió **93/85** donde el suelo decía **94/90**, sin que el archivo cambiara, y
el suelo se cedió copiando el artefacto entero. La causa se midió comparando **cinco ejecuciones de
CI línea a línea**: una sola línea y una sola rama separan la medición mala de las otras cuatro, y
las dos son el vaciado **agotando su techo de cinco segundos**. No era el temporizador diferido, que
es lo que decía esta nota: era que la cola de liberación es **una para todo el proceso**, así que un
runner cargado la deja llena más de cinco segundos y uno holgado no. Nadie pedía esa rama; la ejercía
el azar.

El producto no cambia, porque el techo hace exactamente lo que debe. Lo que faltaba era la prueba: se
pide un techo **por debajo de la ventana de quiescencia** y se afirma el abandono, con lo que
rendirse deja de depender del reloj. De **93,68/90 a 96,70/100 en tres tiradas idénticas**, y por el
camino tres decisiones más del desmontaje y una propiedad que nadie leía.
[La evidencia](evidence/stable/audit-libvlc-flush-determinism.md).

**El suelo sube con el artefacto del run que verifique ese commit**, entero y sin editar a mano.

Lo que sigue debajo se conserva porque describe la forma de la corrección, que es la referencia para
la vía suelta:

**Un archivo activado desde el Explorador se reproduce y no se ve.** Medido el 2026-08-17:

```
singleton.IsLooseSession=True  name='Arrival.2016.mp4'  engine=Playing  pos=00:00:00.15
player=False  playerVisible=False  stages=0  surfaces=0
```

La activación hace su parte entera —`OpenLooseFile` arranca el motor y el banner recibe su sesión—,
pero **nadie construye las superficies del reproductor**, y `HasLooseFile` es
`Player?.LooseFile is not null`. Así que el vídeo suena sin imagen y sin transporte, y el aviso de
«esto no está en tu biblioteca» no llega a la pantalla con sus tres botones dentro. Lo mismo le pasa
al **tráiler local**, que abre por la misma vía.

**La causa de raíz, y por eso la corrección es la que es: hay dos caminos que abren medios y sólo uno
construye pantalla.** `OpenLooseFile` arranca el coordinador por su cuenta; `PlayerViewModel.OpenAsync`
arranca y además tiene superficie. Mientras haya dos, esto vuelve.

**Decidido: `OpenLooseFile` valida y describe, y abrir es siempre del reproductor.** Deja de llamar
al coordinador; conserva sus dos negativas —extensión fuera de la lista aprobada y archivo ausente—,
que son las que hacen falta **antes** de tocar nada. Se añade una vía única que los dos llamantes
usan:

- `ShellSurfaces.OpenLoosePlayer` — `Func<string, CancellationToken, Task<PlayerSurfaces?>>`, con su
  `ShellViewModel.OpenLoosePlayerAsync`, al lado de `OpenPlayer` y por la misma razón que aquélla.
- En la composición: pide la sesión a `OpenLooseFile`, construye `PlayerSurfaces` con **`Player`,
  `LooseFile` y `VideoStatus`** —de ese `record` sólo `Player` es obligatorio— más el transporte del
  contenedor, y llama a `player.OpenAsync(session.MediaFileId, session.Path)`.
- **Sin seguimiento de progreso, sin marcadores, sin versiones y sin oferta de reanudar**, que es lo
  que mantiene la promesa: «una sesión suelta deja la base de datos como la encontró».
- Los dos llamantes pasan por ahí: la activación (`ConfigureWindow`) y el tráiler local
  (`onPlayTrailer` en `CompositionRoot`).

**Lo que se gana de paso, y es una mejora real:** hoy un archivo suelto que no se puede decodificar
hace que el `catch` limpie el banner y no quede nada en pantalla; abriendo por el reproductor, el
fallo llega a `Report` y **aparece la pantalla de recuperación** que la tanda 2e acaba de dejar
probada.

**Lo que no vale, y está comprobado:** reutilizar `OpenPlayerAsync` tal cual —empieza por
`FindByIdAsync` y un archivo suelto no está en el catálogo, y su camino arranca el seguimiento de
progreso—; y dejar que `OpenLooseFile` siga arrancando y abrir otra vez desde el reproductor, que es
un doble arranque sin razón.

**Antes de tocarlo, releer `FileActivationTests`**: afirma la promesa que no se puede perder —el censo
de más de veinte tablas idéntico antes y después de una activación— y **no** afirma que
`OpenLooseFile` arranque el motor, así que mover el arranque no la rompe. `OpenLooseFileTests` sí
habla del coordinador y se actualiza con el cambio.

**Cómo llega el arnés:** `ApplicationHost.PendingActivationPath` antes de `CreateShell` y
**`ConfigureWindow` después**, que es donde la activación se lee y en ningún otro sitio.

#### 3. La prueba de los subtítulos — decidido qué se mide

`A11Y-002` se bloquea **por medición y no por observación**. Primero se intenta lo directo:
decodificar un fotograma con estilo aplicado y otro sin él y comparar los mapas de bits; si el motor
no deja llegar al fotograma, el plan B mide **la causa**, que ya está diagnosticada — la instancia de
LibVLC se cachea por juego de opciones y ninguna de las cacheadas lleva opciones de subtítulos. En los
dos casos el resultado es el mismo bloqueador, con un número detrás. Va en `MediaTests`.

#### 6. El rediseño — la regla del trinquete, decidida antes de empezar

El paseo cuenta hoy **129 declaraciones en 128 identidades** leyendo los `.axaml`, y el guion sólo
sabe encoger. Un rediseño mueve ese inventario, así que:

- **Un control nuevo entra con su escena en el mismo cambio**, nunca con una línea en
  `eng/walk-pending.txt`. La lista de pendientes se cerró y no se reabre.
- **Un control renombrado** cambia su ancla en la escena que lo pulsa; el ancla es la clave de
  recurso tras `AutomationProperties.Name`, y un rediseño cambia la forma sin quitarla.
- **Un control que desaparece** sale del inventario solo, y el trinquete baja con él.
- Los **dos superpuestos que quedan sin dimensionar** —`SkipMarkerButton` y `LooseFileBanner`— se
  corrigen aquí si el rediseño los toca, y si no, cada uno en su escena con su medición.
- Los **cinco activos de marca** encajan en este paso, que es donde vive la dirección visual. Si
  vienen aquí, entran en 0.2.0 sin coste; el paquete se construye hoy sin ellos.

#### Las cinco decisiones del 2026-08-17, tomadas y no reabiertas

~~**1. Las dos respuestas que faltan del diálogo de versiones (2 controles).**~~ **Hecha el
2026-08-17, 10 → 8** — [la evidencia](evidence/stable/audit-walk-version-switch-answers.md). El
control desprendido se confirmó tal cual —`before detached=False en=True`, `after detached=True
en=False name=<null>`, con una fila viva ocupando su sitio—, **pero era el síntoma**: `PressAsync`
reintenta cuando la sonda no cambia, y para el segundo intento la sesión ya se había reconstruido. La
causa medida en la misma ejecución fue `asking=False`: **la pregunta no se levantaba**, porque el
suelo de reanudación son 30 s y la escena cambiaba a una versión de 20 — no existe posición que
cumpla las dos cosas. Duraciones a **60 s y 180 s** y el orden pasa a confirmar → refusar → empezar de
nuevo, que lo fija la misma aritmética. Y con las duraciones arregladas salió **un defecto de producto
del que nadie miraba**: confirmar un cambio calculaba el segundo trasladado, lo guardaba (`00:02:01`)
y luego **abría la otra versión desde cero** escribiendo ese cero encima (`playhead: 0, 0, 0, 1, 1,
2`). `PlayDetailsRequest.StartPosition` estaba **producida en cinco sitios, documentada, vigilada por
una prueba del lado del productor y leída en ninguno** — el defecto de la casa visto desde el
consumidor. Pasa a `TimeSpan?`, donde `null` es «decide tú con la política de reanudación».

**Hallazgo abierto que salió de ahí, sin medir y por tanto sin corregir:** «Reproducir desde el
principio» de la ficha de película pasaba `TimeSpan.Zero` a un anfitrión que lo ignoraba, así que con
progreso guardado **probablemente no empezaba por el principio**. Ahora que la posición pedida manda,
debería estar arreglado de paso; lo que falta es **medirlo**, y el sitio natural es la escena de la
tanda 1 que ya tiene que sembrar progreso para «Continuar».

~~**2. La novena salida de la regla de aislamiento (2 controles de la 2e).**~~ **Hecha el 2026-08-17,
8 → 6** — [la evidencia](evidence/stable/audit-walk-player-recovery.md). El anotador
(`RecordingExternalPlaybackLauncher`, verbo `play-externally <ruta>`) entra en los vigilados de
`check-coverage.ps1` al 100/100, y sus **dos negativas** —extensión fuera de la lista y archivo
ausente— se afirman en `IsolatedRunTests` junto a las dos mitades de la elección. Lo que **no** se
cumplió fue que las dos pulsaciones compartieran superficie: `corrupted=True canRetry=False
canOpenExternally=True`. La política da a un medio corrupto elegir otra versión y abrir fuera, **sin
reintentar**, y tiene razón —reabrir los mismos bytes falla igual—; reintentar se ofrece cuando
**falta el archivo**. La escena abre dos veces y cada pulsación encuentra el fallo que la ofrece.

**3. Los superpuestos que siguen sin dimensionarse** —ya sólo `SkipMarkerButton` y `LooseFileBanner`,
porque `VersionSwitchDialog` se corrigió el 2026-08-17 con su medición (`surfaces=1 [0, 0, 1280, 1400]`
sobre `stage=0, 0, 1280, 1400`)— se corrigen **cada uno en su escena y con su medición**: `Border` con
alineación, fondo y borde, y las filas de botones a `WrapPanel`. La medición se toma antes: los
`bounds` del control frente a los del escenario.

**4. `A11Y-002` en el corte de versión: pasa a `BLOCKED`.** El estilo de subtítulos llega a la base de
datos y **no a la imagen** —LibVLC toma su dibujado de las opciones con las que se construye la
instancia, y aquí hay una instancia cacheada por juego de opciones sin ninguna de subtítulos—, así que
«subtítulos personalizables» no está entregado por mucho que sus seis controles existan y persistan. Se
cambia **en el corte**, que es donde el manifiesto se regenera con un paquete recién construido, con el
bloqueador nombrado en `eng/generate-verification-manifest.ps1` y en `release-readiness.md`. El paseo
físico de diez minutos gana además una comprobación: **mirar si los subtítulos se ven como se
pidieron**.

~~**5. El apagado con una sesión activa se corrige, y así.**~~ **Hecha el 2026-08-17**, exactamente
como estaba decidida: `ApplicationHost.DisposeAsync` para la sesión (`StopAsync`, con
`ObjectDisposedException` tragada) antes de `_services.DisposeAsync()`. Lo que la prueba **no** es la
escena de la 2a sino la nueva de recuperación, que termina con un vídeo sonando por la misma razón:
cerrar el reproductor primero es lo que hacían todas las escenas y es lo que tapaba esto.

Después: la cobertura a todo `src/`, lo que queda de `ARQ-004`, y el rediseño.

~~**7c — «Cancelar» del actualizador.**~~ **Hecha el 2026-08-17, 34 → 33** —
[la evidencia](evidence/stable/audit-walk-update-cancel.md)—, tal como estaba decidida: campo
**opcional** `serveDelayMilliseconds` en el manifiesto, `await Task.Delay(delay, ct)` en el
transporte, escena propia y sonda de estado. **La ventana se midió y 3000 ms no valía:** las dos
pulsaciones gastan **950 ms**, pero la ventana también tiene que aguantar el presupuesto de
reintentos de `PressAsync` —ocho pulsaciones a un asentamiento de distancia, **2400 ms**—, así que
quedó en **5000 ms**, que no cuesta nada porque cancelar abandona el resto de la espera.

~~**6b — «Cancelar» de copias.**~~ **Hecha el 2026-08-17, 33 → 32** —
[la evidencia](evidence/stable/audit-walk-backup-cancel.md)—, y **el destino sigue siendo 0**: la
biblioteca honesta que tarda lo suficiente existe. Las dos palancas se midieron en el orden decidido y
ganó la segunda por una razón que no estaba prevista: **el coste por archivo pesa más que el coste por
megabyte**, así que la palanca es cuántas imágenes hay y no cuánto pesan. El catálogo llega al segundo
sólo con 50.000 filas (1.159 ms) y encarece la escena; **6.000 imágenes de 50 KiB dan 3.944 ms con
293 MB**, donde 6.000 de 100 KiB dan 4.377 ms con el doble de disco. Ni un gancho en la composición, y
sin defecto de producto: el control funcionaba y lo que faltaba era una ventana.

**Tanda 2 — DECIDIDO que se parte en cinco escenas por superficie**, no una sola de 29 controles:

| | Superficie | Controles |
|---|---|---|
| ~~**2a**~~ | ~~Pistas y salida de audio~~ | **hecha el 2026-08-17, 32 → 27** |
| ~~**2b**~~ | ~~Estilo de subtítulos~~ | **hecha el 2026-08-17, 27 → 23** |
| ~~**2c**~~ | ~~Marcadores: editor, revisión y salto~~ | **hecha el 2026-08-17, 23 → 16** |
| ~~**2d**~~ | ~~Reanudar, siguiente episodio y cambiar de versión, con las tres respuestas~~ | **hecha el 2026-08-17, 16 → 8** |
| **2e** | ~~Recuperación del reproductor~~ **hecha el 2026-08-17, 8 → 6**; el archivo suelto espera al defecto de arriba | 3 |

Es la única tanda que necesita **vídeo real**. Y la advertencia sigue medida: **los superpuestos que
quedan no fijan alineación** y se estiran sobre todo el escenario, igual que el de estado corregido el
2026-08-15; **cada uno se corrige en su escena con su medición**, nunca en bloque. Quedan dos.

**Lo que dejó la 2a, y ahorra tiempo en las cuatro que quedan:** un desplegable se prueba **abriéndolo**
—lo que se elige dentro cae en otra raíz de ventana— y se cierra con Escape antes del siguiente;
`RequireMultiTrackSampleAsync` produce y cachea una muestra con **dos pistas de audio y una de
subtítulos**; y el defecto que salió es de la familia de la casa vista del revés: un ámbito que la
aplicación **lee** y que nada dentro de ella podía **escribir** —
[la evidencia](evidence/stable/audit-walk-tracks-and-audio-output.md).

**Los tres de la tanda 1** son los que necesitan siembra que el paseo aún no hace: la fila de episodio
(exige serie, temporada y episodios), «Continuar» de la ficha de película (exige progreso guardado que
merezca volver) y su tráiler local (exige un archivo de tráiler junto a la película, con grupo de
versiones).

**El corte de versión.** Ya son **trece** evidencias esperando a entrar en `FEATURES.md`, y regenerar
el manifiesto es parte de cortar una versión, no de una sesión de trabajo. **Decidido**: cuando el
paseo llegue a su mínimo se corta **0.2.0** con un paquete recién construido, y ahí entran las trece
de golpe.

**El apagado desde la ventana y desde la bandeja se queda directo.** No son controles del inventario
—no hay AXAML detrás—, así que no tocan el trinquete, y una ejecución aislada no llega a ellos hoy. Se
revisa si el rediseño toca el ciclo de vida, y no antes: inventar ahí trabajo sería inventarlo.

### La decisión que lo desbloquea todo: una ejecución aislada no toca nada fuera de su raíz

Tres controles se declararon incubribles por la misma razón, y la tercera vez deja de ser casualidad
para ser una regla del proyecto:

- **Conceder el arranque con Windows** escribía en la clave que Windows lee al iniciar sesión.
  **Resuelto el 2026-08-16** con `IAppDataPaths.StartupRegistrySubKey`.
- **El enlace al tráiler del proveedor** (`DetailsTrailerLinkAction`, en la ficha de película y en la
  de serie) entrega la dirección al shell de Windows, que abre un navegador de verdad.
- **Los selectores de archivo y carpeta** (copias, restauración, añadir una raíz) abren un diálogo
  modal del sistema que ningún arnés puede contestar.

**Regla, y se aplica a los tres:** una ejecución cuya raíz de datos **no** es la del perfil —un
arnés, el paseo, una comprobación de ciclo de vida— **no escribe ni abre nada fuera de esa raíz**. En
vez de abrir el navegador, escribe la dirección que habría abierto; en vez de abrir el diálogo,
resuelve la ruta que su raíz declara. Quien es dueño del perfil se comporta exactamente igual que
hoy. No es sólo para poder pulsar: **hoy nadie comprueba que la dirección que se abriría sea la
correcta**, y con esto se comprueba.

### El orden

1. ~~**La regla de aislamiento, y con ella los dos `DetailsTrailerLinkAction`.**~~ **Hecha el
   2026-08-16.** `IAppDataPaths.SystemHandoffDirectory`: una raíz que no es la del perfil recibe una
   carpeta donde anotar lo que habría entregado a Windows, y quien es dueño recibe `null` porque la
   distinción no es dónde ocurre la entrega sino **si** ocurre. Las negativas del enlace salieron a
   `ExternalLinkPolicy`, en el dominio, y las usan las dos salidas. El paseo lee las dos direcciones
   —`FilmTrailer` en la película, `ShowTrailer` en la serie— en el orden en que se pulsaron. De regalo,
   la puerta de cobertura destapó una **guarda inalcanzable** (anfitrión vacío con `https` ya exigido)
   y se retiró con su medición. **75 → 73.**
2. **Tanda 5 — la bandeja de revisión (6) y los duplicados (1).** **Cuatro hechas el 2026-08-16**
   —cargar más, aceptar, rechazar y buscar a mano— con **dos defectos del producto corregidos**: el
   botón «Buscar» no se habilitaba nunca (una clase de comando privada con `CanExecuteChanged` vacío,
   superviviente de `ARQ-004`) y su evento **no lo escuchaba nadie** en `src/`; ahora llega a
   `SearchForMatch`, la contraparte manual de `IdentifyScannedFiles`. **73 → 69.**

   **Las dos reasignaciones, hechas el 2026-08-16**, con **dos defectos más del producto** y uno del
   arnés — [la evidencia](evidence/stable/audit-walk-reassignment.md). La receta previa se quedó corta
   en un punto que decidió todo lo demás: `FileReconciliationPolicy` contesta `Exact` para una
   identidad estable y para una huella única, así que **la única oferta que la aplicación produce es
   una colisión de huella**, y una colisión son **dos candidatos, y por tanto dos botones**. De ahí:
   (1) los dos «Es el mismo, reasignar» tenían el mismo nombre accesible y decidían entidades
   distintas — ahora llevan la ruta del candidato como texto de ayuda, como `EpisodeRowView`; (2) la
   fila era un `StackPanel` horizontal, que ofrece **ancho infinito**, así que la ruta no se plegaba
   nunca y empujaba el botón a **x=2234 en una ventana de 1600**: fuera de la pantalla, sin nada que
   desplazar, con la confirmación **imposible** para cualquier ruta de una biblioteca real. Es ahora un
   `Grid` con `*,Auto`. Y del arnés: `WalkLedger` leía la vista del control **después** del efecto, y
   confirmar retira la oferta en la que el botón vive, así que la identidad se toma antes de pulsar.
   **69 → 67.**
   **El radio de duplicados, hecho el 2026-08-16** y **sin un solo defecto**: el primero en cuatro
   sesiones — [la evidencia](evidence/stable/audit-walk-duplicate-version.md). Continúa la primera
   escena del paseo en vez de montar nada nuevo, se pulsa la copia que **no** es ya la efectiva, y la
   sonda es `preferred_media_file_id` del grupo —nulo antes, el archivo pulsado después—, porque sin
   preferencia guardada la política ya contesta con una de las dos y leer `IsEffective` habría llamado
   igual a «la copia mejor» y a «la que alguien eligió». **67 → 66, y la tanda 5 cerrada.**
   **Y la deuda, pagada el 2026-08-16** — [la evidencia](evidence/stable/audit-review-inbox-coverage.md).
   `ReviewInboxViewModel.cs` va de **92,13/59,26 vigilado por nadie** a **100/100 vigilado en cada
   ejecución**, y con él la lista de `eng/check-coverage.ps1` llega a seis archivos. Dos cosas que la
   medición enseñó y que valen para todo lo que viene: **pulsar los dos botones no movió una sola
   rama** —un paseo prueba que un control trabaja, nunca los caminos de cuando algo va mal—, y **una
   rama se cubre entera dentro de una sola suite o no se cubre**, porque al fusionar informes Cobertura
   se guarda el mejor de los dos para cada línea y no la unión: el lado «hay más página» lo tomaba el
   paseo, el lado «no hay más» las pruebas de interfaz, y la rama se leía a la mitad para siempre. De
   regalo, tres ramas **inalcanzables** por un `as AsyncRelayCommand` que no podía fallar pero sí dejar
   de coincidir, que es el camino de vuelta a `ARQ-004`.
3. ~~**Tanda 8 — el shell y el inicio (14).**~~ **Hecha el 2026-08-16, 66 → 52** — el mayor salto de
   una sola tanda desde que existe el trinquete, y [la evidencia](evidence/stable/audit-walk-shell-and-home.md)
   cuenta **dos defectos más**, uno de ellos el peor de la jornada. (1) **«Continuar» estaba cableado
   a nada**: `onResume: null` en el contenedor, con el botón habilitándose solo porque había progreso
   al que volver; la acción principal de la aplicación, en la primera pantalla, sin hacer nada. Ahora
   abre la sesión con la versión de la que salió la posición —`watch_state` la guarda para eso— y el
   shell se lee al pulsar, no al construir. (2) **«Mini reproductor» y «Pantalla completa» estaban
   fuera de la pantalla**, a x=1737 y más allá, y no por el tamaño de la ventana: su columna mide
   **320 px por definición** y los tres botones suman unos 800, así que no cabían a ninguna anchura.
   Ahora es un `WrapPanel`, como el de copias — tercera vez en el día que un `StackPanel` horizontal
   esconde un control. Del arnés: `Resolve` prefiere el **control de mando** cuando un nombre lo
   comparten una acción y la región a la que lleva (el botón «Inicio» y la pantalla «Inicio»), y sólo
   si eso deja exactamente uno, para no tapar el defecto de dos botones con un nombre.

   **Anotado, no re-deliberado:** `CompositionRoot.Library.cs` quedó en 97,14/100 y **no entra** en la
   lista de vigilados, porque esa lista es para archivos que **deciden**, no para los que declaran; y
   un gancho opcional que el contenedor deja a nulo **no lo caza `ServiceConsumptionTests`**, porque
   no es un registro sin consumidor. Quien lo vigila desde hoy es el paseo.
4. ~~**Tanda 9 — el onboarding de raíces (8).**~~ **Hecha el 2026-08-16, 52 → 44** —
   [la evidencia](evidence/stable/audit-walk-root-onboarding.md)—, y con **una premisa de esta cola
   desmentida**: no hay ningún selector de carpeta aquí. La carpeta **se escribe en una caja de
   texto**, así que la tanda no tenía condición previa ninguna y podría haberse hecho antes; los
   selectores de verdad (`OpenFilePickerAsync`, `SaveFilePickerAsync`) están en copias y restauración,
   o sea en la tanda 6, donde la condición **sí** sigue en pie. El defecto: **«Retirar» a x=2146 en una
   ventana de 1600**, otra vez un `StackPanel` horizontal con una ruta al lado. **Cuarta vez en el día,
   y ya es regla de la casa: lo que va junto a un dato de anchura libre se coloca en una rejilla.** Y
   del arnés: **una sonda se compara por valor** — devolver la lista de carpetas hacía que el clic de
   control «cambiara» siempre, porque cada lectura es un array nuevo; el caso vacío pasaba porque un
   array vacío es la misma instancia compartida.
5. ~~**Tanda 6a — copias y restauración (4).**~~ **Hecha el 2026-08-16, 44 → 40** —
   [la evidencia](evidence/stable/audit-walk-backup-and-restore.md)—, y **sin un solo defecto de
   producto**: el segundo en cinco sesiones. La regla de aislamiento llegó a los selectores de archivo
   sin interfaz nueva, decidida en la composición por `SystemHandoffDirectory` igual que el lanzador
   de enlaces; `HandoffArchivePicker` responde dentro de la carpeta de traspaso y responde `null`
   cuando no hay nada, que es lo que contesta un diálogo cancelado. La escena **no compone ninguna
   ruta**: exporta donde la aplicación dice y restaura lo que la aplicación encuentra allí.

   **Lo que costó una ejecución: una sonda de disco que pasa no significa que la pantalla haya
   terminado.** La carpeta de la copia se publica **antes** de que corra la continuación que fija el
   estado, así que `BackupStatusRunning` seguía en pantalla cuando la sonda ya estaba satisfecha. Se
   espera al reposo y **luego** se dice qué salió. Es la misma carrera que la escena de privacidad
   encontró desde el otro lado, y con dos apariciones ya es regla.

   **Y quedó comprobado por primera vez desde la aplicación ensamblada** que el intercambio funciona
   con el programa abierto y la biblioteca cargada: `SwapAsync` llama a `ClearAllPools()` antes de
   mover nada y el catálogo abre y cierra su conexión por operación.

   **Un hallazgo que era falso, corregido el 2026-08-17.** Esta entrada afirmaba que el segundo
   constructor de `StagedRestoreService` (`availableBytes`, `beforeSwap`) no lo usaba nadie. **Sí lo
   usa `DisasterRecoveryTests`**, y para lo que el gancho existe: `onBeforeSwap: cancellation.Cancel`
   prueba una cancelación justo antes del intercambio, y `onBeforeSwap: () => throw new IOException(…)`
   un intercambio interrumpido — los dos caminos que deciden si un fallo a mitad pierde la biblioteca
   de alguien. La llamada es `new(Paths, _ => availableBytes, onBeforeSwap)`, con el tipo inferido, y
   un `grep` de `new StagedRestoreService(` no la encuentra. **Se pregunta al compilador quién
   construye un tipo, no al buscador**: retirar el miembro y compilar cuesta un minuto y no puede
   equivocarse.

5b. ~~**Tanda 6b — «Cancelar» (1).**~~ **Hecha el 2026-08-17, 33 → 32** —
   [la evidencia](evidence/stable/audit-walk-backup-cancel.md)—. La condición previa era la de abajo y
   se cumplió sembrando: **6.000 imágenes de 50 KiB** dan una copia de **3.944 ms**, y las dos
   pulsaciones gastan **1.211 ms**. Lo que no se había previsto es cuál de las dos palancas gana: **el
   coste por archivo pesa más que el coste por megabyte**.

   - **La condición previa es dura y es la única**: lleva `IsEnabled="{Binding IsRunning}"` **y**
     `CanExecute => IsRunning`, así que el botón sólo existe mientras una copia está en marcha. Con
     la biblioteca de un arnés la copia acaba en **milisegundos** —medido el 2026-08-16 en la escena
     de la 6a, que la crea entera sin que se vea la barra—. Hay que **sembrar una biblioteca que
     tarde** —muchas filas, y artwork personal, que es lo que la copia recorre— y medir cuánto dura
     **antes** de escribir la escena. No se improvisa al final.
   - **Lo demás ya está puesto por la 6a**: la escena navega a copias, la regla de aislamiento
     responde los dos diálogos, y la sonda de una cancelación es `BackupStatusCancelled` con **ninguna
     carpeta nueva** publicada — porque una copia cancelada no debe dejar nada que restaurar.
6. ~~**El ciclo de vida en el sandbox, caducado desde `DES-001`.**~~ **Hecho el 2026-08-16** —
   [la evidencia](evidence/stable/audit-sandbox-lifecycle-reproduced.md)—, y **la premisa era medio
   falsa**: el informe archivado ya traía las nueve fases, así que lo caducado era **sólo la huella
   del manifiesto** (`402ae30c…` archivada contra `5e341b5f…` actual). Lo que de verdad faltaba era
   que el **guion versionado** supiera producirlas: `sandbox-handover.ps1` instalaba y lanzaba, y el
   propio `README-sandbox.md` declaraba manuales las cuatro del ciclo — que es exactamente lo que
   hacía depender la medición de que alguien se acordara.

   Ahora el guion hace `file-association`, `windows-upgrade`, `windows-downgrade-refused`,
   `windows-repair` y `windows-uninstall`, y `windows-launch` comprueba además que la base **no**
   acabó en la carpeta virtualizada del paquete. El anfitrión obtiene el paquete de la versión
   siguiente **resellando** el actual con la versión subida (`0.1.0.0` → `0.2.0.0`) en vez de
   construir la aplicación dos veces: lo que Windows lee para decidir si una instalación es una
   actualización es la versión del manifiesto y nada más. **Una sola ejecución escribe los dos
   informes**, porque un segundo ciclo instalaría el paquete dos veces para medir una instalación.

   Resultado: `lifecycle.json` con **doce fases en verde**, cinco nativas incluidas, y
   `PackagingTests` en 152. La base sobrevivió a la actualización con **372 736 bytes a los dos
   lados**, y desinstalar no se llevó la biblioteca.

   **Y una alarma falsa que costó un rato:** el campo de la negativa se leyó como `versi�n` y pareció
   evidencia estropeada; los bytes eran `\xc3\xb3`, o sea `ó` en UTF-8 correcto. La corrupción estaba
   en la consola que lo imprimió. Una corrección «obvia» habría tocado un guion que no tenía nada
   que corregir.
7. **Tanda 7 — el actualizador (5) y la recuperación de la base (2). 40 → 33.** Entra aquí la cadena
   decidida y pendiente: cuando el traspaso se rechaza, el mensaje debe decir **dónde está el paquete
   verificado** para que la persona lo abra ella misma — en los dos idiomas, y el diálogo de Windows
   **no se tapa ni se silencia**. **Investigado el 2026-08-16, para no redescubrirlo:**

   **Es más cara que la 6a, y el motivo es medible: la regla de aislamiento tiene que cubrir cuatro
   salidas más.** Van tres cubiertas —registro, navegador, selectores de archivo—; esta tanda
   necesita:

   - **La fuente y la descarga.** `IUpdateSource` es `GitHubReleaseUpdateProvider` contra
     `https://api.github.com/`, y `IUpdateDownloader` baja con `HttpClient` a `DataRoot/updates`
     (`CompositionRoot.Updates.cs:40` y `:45`). Un arnés no puede ni debe salir a la red. Aislado, las
     dos leen de la carpeta de traspaso.
   - **El lanzador.** `IUpdateLauncher` es `WindowsUpdateLauncher(OpenWithWindows)`, y
     `OpenWithWindows` es `Process.Start` con `UseShellExecute` (`CompositionRoot.cs:1224`). Aislado,
     anota el paquete que habría entregado — igual que el lanzador de enlaces anota la dirección.
   - **Abrir la carpeta de copias.** `HandleRecoveryAction` hace otro `Process.Start` sobre una
     carpeta (`CompositionRoot.cs:1298`).
   - **Salir.** `RecoveryExit` llama a `desktop.Shutdown()` **sólo si** el `ApplicationLifetime` es
     `IClassicDesktopStyleApplicationLifetime`, y bajo el arnés headless **no lo es**: hoy el botón no
     hace nada que sondear. Necesita un punto de apagado que la raíz decida, como los otros tres.

   **Y la recuperación tiene una condición previa propia y dura:** su vista **no está en el shell**.
   `CreateShell` la construye sólo cuando `PrepareDatabaseAsync` devuelve una negativa
   (`CompositionRoot.cs:303`), así que la escena tiene que **sembrar una base que falle** la
   integridad o la migración, y **no puede usar `ShowShell()`**, que afirma `IsType<ShellView>`.
   Necesita montaje propio.

   **Pero el montaje propio sale barato, y está medido:** `AssembledStartup.FinalContent` **ya**
   devuelve `DatabaseRecoveryView` cuando la base se niega, y en el arnés hay **sólo cinco usos** de
   `host.Shell`, **todos** `GetVisualDescendants()`. Basta con que `ShellHost.Shell` pase de
   `ShellView` a `Control`; los **67** usos de `host.ViewModel` no se tocan si el record guarda el
   modelo como opcional y expone la propiedad de siempre. No hace falta una segunda clase de anfitrión
   ni una segunda versión de `PressAsync`.

   **Los cinco del actualizador, con sus sondas:** el interruptor de comprobación automática —el
   ajuste guardado, y es el único sin condición previa—; buscar —la oferta en pantalla—; descargar
   —el paquete en `DataRoot/updates`—; instalar —lo anotado por el lanzador aislado—; y **«Cancelar»,
   que es el mismo obstáculo que la 6b**: `IsEnabled="{Binding IsBusy}"` y `CanExecute => IsBusy`.
   Con una fuente local la descarga acaba en milisegundos, **pero aquí hay una ventaja que copias no
   tiene**: la fuente es del arnés, así que puede servir despacio a propósito. Medirlo antes de
   escribir la escena.

   **DECIDIDO el 2026-08-17, no se re-delibera:**

   - **La tanda se parte, y la recuperación va PRIMERO.** **7b — recuperación (2 controles), 40 → 38**:
     necesita dos salidas (abrir carpeta, salir), su montaje cuesta un cambio de tipo, y **no depende
     de ninguna decisión abierta**. Después **7a — el actualizador (5), 38 → 33**.
   - **Cómo se sirve la fuente de actualización a una ejecución aislada.** `IUpdateSource` **se
     sustituye** por uno que lee un manifiesto de la carpeta de traspaso, y `VerifiedUpdateDownloader`
     **se conserva** con un transporte local, para que el hash, el tamaño y el `.partial` sigan siendo
     los de verdad. **Lo que NO se hace: hacer que `UpdateSigningKey.PublicKey` dependa de la raíz.**
     Eso movería una decisión de seguridad para poder probar, que es exactamente el razonamiento que
     este repositorio tiene prohibido. La verificación minisign se prueba donde ya se prueba: en sus
     pruebas unitarias, con sus propios vectores.
   - **El orden dentro de cada mitad**: las salidas primero, en un commit propio y con
     `IsolatedRunTests` cubriendo **las dos mitades de cada una** —aislada y dueña del perfil— en el
     mismo archivo, porque al fusionar Cobertura se guarda el mejor informe por línea y no la unión.
     Después los controles.
   - **«Cancelar» del actualizador va con la 7a**, no aparte: a diferencia del de copias, su fuente es
     del arnés y puede servir despacio a propósito. Si medida la siembra sale cara, se aparta a una
     7c y se dice en la evidencia, sin silencio.

   **7a — el actualizador. Empezada el 2026-08-17: 38 → 37**, con
   [el interruptor de comprobación automática](evidence/stable/audit-walk-update-automatic-check.md),
   el único de los cinco sin condición previa. **Quedan cuatro** —buscar, descargar, instalar y
   «Cancelar»—, y su investigación está cerrada; **no se re-delibera**:

   - **La dirección la trae el manifiesto, no el código.**
     `NetworkPrivacyTests.No_source_file_names_a_host_that_is_neither_declared_nor_handed_off` recorre
     `src/` buscando `https?://…` y falla con cualquier anfitrión que el registro no declare. Declarar
     uno de arnés **mentiría sobre lo que la aplicación conecta** y ensancharía `IsDeclaredHost`, que
     es en lo que confía el canario de red. Así que el manifiesto de la carpeta de traspaso trae la
     dirección **y** el anfitrión, y `VerifiedUpdateDownloader` recibe su allowlist por parámetro, que
     **ya admite** («tests hand in their loopback server explicitly»).
   - **`UpdatePolicy` exige `release.Sha256Signed`**, que es un veredicto que pone la fuente tras
     verificar minisign con la clave embebida. La fuente del arnés **lo afirma**, como un doble en una
     unitaria, y la evidencia lo dice con todas las letras: en una ejecución aislada la firma no se
     verifica porque no hay nada firmado. Lo real es lo que se conserva a propósito —hash, tamaño y
     `.partial`— sobre transporte local. **Sigue prohibido** hacer que `UpdateSigningKey.PublicKey`
     dependa de la raíz.
   - ~~**El lanzador.**~~ **Hecho el 2026-08-17** —
     [la evidencia](evidence/stable/audit-updater-handover-exit.md)—, y **sin clase ni interfaz
     nueva**: `WindowsUpdateLauncher` ya recibía la entrega como delegado, así que `ISystemHandoff`
     ganó `TryOpenPackage` y la composición se lo pasa. `OpenWithWindows` quedó sin llamantes y **lo
     dijo el compilador**. Con ello la regla de aislamiento cubre **seis** salidas. Y una asimetría
     que estaba en un comentario pasó a estar donde se decide: un proceso nulo es **éxito** para una
     carpeta —cae en una ventana ya abierta— y **rechazo** para un paquete, que es la negativa que de
     verdad ocurre en un Windows sin nada registrado para `.msix`.
   - ~~**La fuente.**~~ **Hecha el 2026-08-17, 37 → 36** —
     [la evidencia](evidence/stable/audit-walk-update-check.md)—, y con ella **«Buscar»**.
     `HandoffUpdateSource` lee `update-manifest.json` de la carpeta de traspaso; la dirección es
     **dato y no código**, y el hash y el tamaño se **declaran** en vez de calcularse del archivo,
     porque calcularlos allí haría que la verificación comprobara el archivo contra sí mismo. Las tres
     respuestas se mantienen aparte: sin manifiesto **no hay release**, ilegible es **inalcanzable**, y
     otra arquitectura es **un rechazo con su motivo**.
   - ~~**La descarga.**~~ **Hecha el 2026-08-17, 36 → 34** —
     [la evidencia](evidence/stable/audit-walk-update-download.md)—, y con ella **descargar e
     instalar**. Se sustituye **el transporte y la allowlist, nada más**:
     `VerifiedUpdateDownloader` hace el trabajo en los dos lados, así que el hash, el tamaño y el
     `.partial` son los de una instalación de verdad — y se comprueba lo contrario, que es lo que lo
     demuestra: con un paquete distinto del prometido, la descarga lo rechaza y **no deja nada**. El
     transporte **no** implementa `Range` (el descargador ya trata lo que no es `PartialContent` como
     «empezar de cero») y **no compone rutas** a partir de la petición.
7c. ~~**«Cancelar» del actualizador (1). 34 → 33.**~~ **Hecha el 2026-08-17** —
   [la evidencia](evidence/stable/audit-walk-update-cancel.md)—. El rojo archivado dice por qué
   llevaba dos tandas esperando: la pulsación de Descargar volvía con `UpdateStatusReady` donde la
   escena esperaba `UpdateStatusDownloading`, porque con el paquete en el disco al lado la descarga
   entera acaba en milisegundos. El manifiesto declara ahora una espera **opcional**, el transporte
   la sostiene con el token del que llama, y **no se tocó el producto**: lo que la cancelación
   recorre —token, interrupción, estado— es suyo.

   ~~**7b — la recuperación de la base (2).**~~ **Hecha el 2026-08-17, 40 → 38**, en dos commits y
   **sin un solo defecto de producto** —el tercero así en once tandas—:
   [las dos salidas](evidence/stable/audit-recovery-exits.md) y
   [la pantalla pulsada](evidence/stable/audit-walk-database-recovery.md).

   - **Las dos salidas son un puerto con dos métodos y un solo llamante**, elegido en la composición
     por `SystemHandoffDirectory` igual que las tres anteriores. Lo anotado es una línea por entrega
     con un verbo delante —`open-folder <ruta>`, `exit`—, y el verbo es lo que deja a una sonda
     distinguir las dos sin analizar nada.
   - **Un hallazgo que decidió el diseño, y lo decidió el compilador:**
     `IClassicDesktopStyleApplicationLifetime` **no es implementable por código de usuario** —Avalonia
     lleva un miembro cuyo nombre es el propio aviso—, así que ningún doble puede sustituirlo y la
     mitad «hay ciclo de vida» no se puede ejercitar en ningún sitio. La búsqueda se queda en
     `CompositionRoot`, donde ya vivían **dos copias literales** de la misma expresión y ahora hay
     una; lo que llega a la salida es la llamada, y las dos clases nuevas quedan al **100/100**.
   - **El montaje costó exactamente lo previsto: un cambio de tipo.** `ShellHost.Shell` de `ShellView`
     a `Control`, con el modelo de vista opcional **detrás de la misma propiedad**; los cinco usos de
     `Shell` sólo recorren el árbol visual y los sesenta y siete de `ViewModel` no se tocan. Salió
     además un `Mount` común, porque las dos formas de montar sólo se diferencian en qué contenido
     afirman.
   - **Lo que sigue fuera y está dicho:** cerrar la ventana y salir por la bandeja **siguen apagando
     directamente**. Es otro camino del producto, con el guardado de la posición alrededor, y una
     ejecución aislada que llegue por ahí sí apagaría — sin medir.
8. **Tanda 2 (resto) — el reproductor y sus superpuestos (29).** La más larga y la única que necesita
   vídeo real: pistas, salida de audio, estilo de subtítulos, marcadores, reanudar, siguiente
   episodio, cambio de versión, archivo suelto y recuperación del reproductor. **Advertencia medida:
   los cinco superpuestos restantes no fijan alineación** y se estiran sobre todo el escenario, igual
   que el de estado corregido el 2026-08-15; **cada uno se corrige en su tanda con su medición**, no
   en bloque. **32 → 3**, y los tres últimos con ellos: **0.**
9. **La cobertura de código, al mismo destino.** Hoy la puerta vigila los archivos nuevos y una lista
   corta —**trece desde el 2026-08-17**, con `AppDataPaths.cs`, `ShellExternalLinkLauncher.cs`,
   `HandoffArchivePicker.cs`, `WindowsSystemHandoff.cs`, `RecordingSystemHandoff.cs`,
   `HandoffUpdateSource.cs`, `HandoffUpdateManifest.cs`, `HandoffUpdateDownloader.cs` y
   `HandoffUpdateTransport.cs` al 100/100 porque son las **salidas** que la
   regla de aislamiento atravesó y las que deciden qué sale de la aplicación y qué alcanza—, así que
   **un archivo antiguo que empeora sigue sin vigilarse**. **Decidido**: cada archivo antiguo que una tanda toque y
   deje en el suelo entra en esa lista al cerrarla, y cuando el paseo llegue a 0,
   `check-coverage.ps1` pasa a medir **todo `src/`** con el suelo de siempre (96 % de líneas y de
   ramas) y una lista de excepciones con la regla de la casa: **sólo puede encoger**.
10. **Rediseño y documentación**, con el paseo entero como red.

**Una tarea decidida y medida el 2026-08-16, sin sitio propio en la cola todavía: lo que queda de
`ARQ-004`.** Sobreviven **nueve** clases de comando privadas con `CanExecuteChanged { add { } remove
{ } }` —en `LibraryViewModel`, `RootOnboardingViewModel`, `ShortcutSettingsViewModel`,
`DatabaseRecoveryViewModel`, `AppearanceSettingsViewModel` (dos), `LifecycleSettingsViewModel`,
`ShellViewModel` y `WindowsTrayService`—. **Ocho son inertes** porque su `CanExecute` es constante.
La novena, la de `LibraryViewModel`, **sí lleva predicado** (`BackCommand` con
`Surface != LibrarySurface.Browse`), que es exactamente la forma que dejó el botón «Buscar» apagado
para siempre; hoy **no muerde**, y eso está medido: el paseo pulsa `LibraryBackAction` y funciona,
porque la vista se hace visible y el botón vuelve a preguntar. **Esa última causa era falsa, y se
midió el 2026-08-18: el paseo funcionaba porque ningún AXAML enlazaba `BackCommand`.** Ver el paso 5.
**Decidido entonces**: no se tocan por ahora
—no hay defecto observable y sustituirlas es una migración mecánica sobre nueve archivos, que en esta
casa exige tres redes—, pero se hacen **en una tanda propia, después de la 2**, cuando el paseo cubra
los 128 controles y pueda servir de red. Si antes de eso alguien añade un `CanExecute` condicional a
cualquiera de las ocho, esa clase se sustituye **en ese mismo cambio**.

**Lo que ningún arnés headless puede probar, y por tanto no se disfraza de cubierto:** la imagen en
una pantalla física y TMDB contestando por red. Eso es el paseo físico de diez minutos, y es del
propietario.

**Una decisión aplazada, y por qué.** Las evidencias del 2026-08-16 **no se han añadido a
`FEATURES.md`**. `EvidenceLinkTests` exige que la matriz y
`docs/evidence/mvp/verification-manifest.json` citen exactamente lo mismo, y el manifiesto **describe
un artefacto**: su procedencia es la del paquete, así que regenerarlo con el `artifacts/package/` de
otra compilación escribiría una procedencia que no es la de nadie. Regenerar el manifiesto es parte de
cortar una versión, no de una sesión de trabajo. **Decidido**: entran en la matriz **cuando se
regenere el manifiesto con un paquete recién construido** —ya son quince, así que ese paso deja de ser
opcional en la próxima versión— y hasta entonces viven en `docs/evidence/stable/`, enlazadas aquí:

1. [el enlace al tráiler](evidence/stable/audit-walk-trailer-links.md)
2. [la bandeja de revisión](evidence/stable/audit-walk-review-inbox.md)
3. [las reasignaciones](evidence/stable/audit-walk-reassignment.md)
4. [la copia que se reproduce](evidence/stable/audit-walk-duplicate-version.md)
5. [la cobertura de la bandeja](evidence/stable/audit-review-inbox-coverage.md)
6. [el shell y el inicio](evidence/stable/audit-walk-shell-and-home.md)
7. [el onboarding de raíces](evidence/stable/audit-walk-root-onboarding.md)
8. [las dos salidas de la recuperación](evidence/stable/audit-recovery-exits.md)
9. [la pantalla de recuperación pulsada](evidence/stable/audit-walk-database-recovery.md)
10. [el permiso para buscar actualizaciones](evidence/stable/audit-walk-update-automatic-check.md)
11. [la entrega del paquete a Windows](evidence/stable/audit-updater-handover-exit.md)
12. [buscar actualizaciones sin red](evidence/stable/audit-walk-update-check.md)
13. [la descarga y la confirmación](evidence/stable/audit-walk-update-download.md)
14. [el comando que nadie escuchaba](evidence/stable/audit-arq004-command-notification.md)
15. [el suelo lo pone quien mide](evidence/stable/audit-coverage-debt-belongs-to-ci.md)

Estado al cerrar la **segunda sesión del 2026-08-16**, que ejecutó el paso 1 entero y cuatro
séptimos del 2. **Tres commits**: `1d80815` (una ejecución aislada dice a dónde habría ido el
navegador), `d91b497` (la puerta encontró una guarda que nunca corrió) y `a799f17` (el botón Buscar
de la bandeja no se podía pulsar, y no hacía nada si se pudiera). Antes, la primera sesión del día
dejó cinco: `5f85fbd` (el renombrado renombra), `3eab024` (el paseo dice dónde fue el clic),
`5f96ac3` (vuelve arriba antes de pulsar), `679d9f1` (arranque aislado y los veinte ajustes) y
`2596bf6` (esta cola). La versión inglesa está en
[NEXT-SESSION.en.md](NEXT-SESSION.en.md). El registro canónico del alcance sigue siendo
[FEATURES.md](FEATURES.md) —**43 verificados, 1 fuera de alcance, 2 bloqueados** (`PLY-004`,
`PRD-002`)—; el trabajo pendiente de la auditoría vive en
[2026-08-08-audit-remediation.md](superpowers/plans/2026-08-08-audit-remediation.md). Esto es sólo el
punto de retomada.

## Verificación de arranque

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version                                        # 10.0.302
git status --short --branch                             # limpio, sobre origin/codex/ap-reelume-mvp-x64
git merge-base --is-ancestor main HEAD; $LASTEXITCODE   # 0
```

## Dónde vive el código ahora

`apvisualsolutions/ap-reelume` es **público** desde el 2026-08-10, con un corte fresco de un solo
commit raíz. El historial completo de desarrollo quedó en `apvisualsolutions/ap-reelume-archive`
(privado), de modo que **los SHA que citan los documentos de evidencia resuelven en el archivo, no
aquí**. Los remotos locales son `origin` (público) y `archive` (el histórico), y las ramas viejas se
conservan como `archived/main` y `archived/codex/…` apuntando al archivo.

La CI corre en runners hospedados, gratuitos en repositorios públicos. El runner self-hosted sigue
instalado en `.runner/` (ignorado por git) pero **apagado**, y el workflow ya no tiene forma de
llamarlo.

## Lo terminado el 2026-08-10 (segunda sesión, la legal)

Cuatro commits, cada uno con su ciclo completo y su evidencia bilingüe.

- **El artefacto entrega las licencias que nombra.** Era el único incumplimiento legal abierto.
  `licenses/`, dentro de los dos artefactos, lleva los cinco textos canónicos —LGPL-2.1, GPL-2.0,
  Apache-2.0, MIT, BSD-3-Clause— y los avisos de copyright de ANGLE, SkiaSharp, HarfBuzzSharp,
  BouncyCastle, SQLite, SQLitePCLRaw y VideoLAN: quince archivos, 209 KiB. El aviso nativo de Skia y
  HarfBuzz destapó **veintitantas bibliotecas** que `libSkiaSharp.dll` transporta —freetype, ICU,
  libpng, libwebp, zlib— y que no aparecían en ningún documento del proyecto. Nada está transcrito:
  `LicenceTextTests` compara cada copia byte a byte contra el paquete NuGet que la compilación
  consumió, y lee cada copyright del `.nuspec` restaurado. Detalle en
  [audit-legal-licence-texts.md](evidence/stable/audit-legal-licence-texts.md).
- **El logotipo de TMDB está en Créditos**, que cierra el último punto abierto de sus términos. El
  archivo es el que TMDB publica y se puede demostrar: la huella SHA-256 que ellos incrustan en la
  dirección del recurso coincide con la del archivo versionado. Sólo publican SVG y Avalonia no dibuja
  SVG, así que la vista lleva la geometría del archivo —una prueba las compara carácter a carácter—
  en vez de traer un renderizador y media docena de paquetes con sus licencias. La especificación
  decía 24 px frente a 48 px del nombre del producto; ese 48 no existía en ninguna vista, así que se
  midió y quedó en 16 frente a 24. Detalle en
  [audit-legal-tmdb-logo.md](evidence/stable/audit-legal-tmdb-logo.md).
- **ARQ-001 / WIN-005 / resto de BUG-004.** El proveedor de servicios tiene dueño y se libera al
  salir; `PendingActivationPath` y el estado de la sesión de reproducción salieron de los estáticos.
  `DisableParallelization` se retiró de `AssembledShellSuites` y las 70 pruebas de accesibilidad pasan
  sin él, que es la prueba de que la propiedad es real. `WindowLifecycle` se extrajo, la puerta de
  cobertura lo midió al 70,89 % de líneas y 28,57 % de ramas, y **volvió** igual que
  `WindowsFilePickers`. Detalle en
  [audit-arq001-application-host.md](evidence/stable/audit-arq001-application-host.md).
- **La integración continua dejaba de hablar durante una hora.** Seis de las diez ejecuciones de esta
  sesión murieron al techo de sesenta minutos, con el registro mudo desde «compilación correcta»
  hasta la cancelación cincuenta y seis minutos después. El paso siguiente,
  `eng/generate-test-media.ps1`, arrancaba FFmpeg sin ningún tope. Ahora cada llamada tiene techo,
  cada muestra se anuncia antes de producirse, y una prueba con un codificador que nunca vuelve
  comprueba que el guion muere en segundos nombrando la receta. Producir la matriz entera cuesta
  1,6 s medidos, así que el techo no es un presupuesto de rendimiento. La primera ejecución con el
  techo puesto falló en cuatro minutos y nombró al culpable: `mkv-dual-audio-english-first`. Las once
  recetas que usaban `-shortest` —un bloqueo documentado de FFmpeg— fijan ahora la duración de salida
  de forma explícita. **El cuelgue nunca se reprodujo en local**: es una carrera, y lo que se retiró
  es la clase de riesgo, no una reproducción. Si vuelve a aparecer, ahora dirá en qué receta.
- **Los dos endurecimientos que la auditoría archivó como «no explotables».** Uno lo era mucho menos
  de lo anotado: el lanzador externo entregaba al shell de Windows un `.ps1`, un `.txt` y un archivo
  sin extensión. El otro no existía en la forma descrita, y sólo se supo forjando el archivo que lo
  habría explotado. Detalle en
  [audit-hardening-launcher-and-restore.md](evidence/stable/audit-hardening-launcher-and-restore.md).

## Lo que sigue

La cola de dos —la última deuda de cobertura y la instrumentación del rojo intermitente— **se
ejecutó entera el 2026-08-10** (quinta sesión), y `BUG-010` cayó detrás. **La cola siguiente está
decidida entera en
[el plan](superpowers/plans/2026-08-08-audit-remediation.md): forma, sitio, primera medición y
criterio de aceptación de cada una.** Se ejecuta en este orden y no se re-delibera; lo que sí se
hace en cada una es **medir antes de corregir**, porque tres premisas escritas se han caído esta
semana al medirlas.

1. ~~**`BUG-011`**~~ **Hecho el 2026-08-14**: una sola cola de liberación para el proceso. La
   fábrica aprendió a vaciar a petición con techo que no lanza; el motor soltó su cola, su candado,
   su bandera, su drenaje y su constante de reposo; la lista que sólo puede encoger de
   `NativeInstanceOwnershipTests` quedó **vacía**. El orden «medios antes que reproductor» y la
   ventana de 1 s siguen intactos.
   [audit-bug011-engine-release-queue.md](evidence/stable/audit-bug011-engine-release-queue.md).
2. ~~**`ARQ-013`**~~ **Hecho el 2026-08-14**: la lectura de referencias salió a `SurfaceReferences`
   y ahora quita los comentarios antes de casar. Medido antes de tocar: **nada** se escondía detrás
   de un comentario, así que la puerta era ciega pero no estaba tapando ningún huérfano.
   [audit-arq013-reachability-comments.md](evidence/stable/audit-arq013-reachability-comments.md).
3. ~~**`ARQ-014`**~~ **Hecho el 2026-08-14**: la versión sale del ensamblado y la prueba afirma sobre
   la cabecera que sale de verdad, con la versión esperada leída de `Directory.Build.props`.
   [audit-arq014-updater-identity.md](evidence/stable/audit-arq014-updater-identity.md).
4. ~~**`ARQ-012`**~~ **Hecho el 2026-08-14**, y adelantado a `ARQ-014` porque ese necesita leer
   `Directory.Build.props` desde la raíz y sin ancla compartida habría escrito una copia más. La
   estimación del plan se quedó **cinco veces corta**: 59 archivos buscaban la raíz y 56 nombraban
   el ancla.
   [audit-arq012-repository-anchor.md](evidence/stable/audit-arq012-repository-anchor.md).
5. ~~**`QA-001`**~~ **Hecho el 2026-08-14**, y la medición dio la vuelta a la tarea: **cero avisos**
   en toda la solución, así que no era deuda sino puerta — y hacía falta igual, porque las tres
   reglas vienen apagadas. El cero se comprobó con un canario antes de creérselo.
   [audit-qa001-culture-gate.md](evidence/stable/audit-qa001-culture-gate.md).
6. **Documentación al final**: `DOC-101`, `DOC-201`, `T44.1`-`T44.6` y el manual de usuario, que se
   escribe desde la aplicación construida y no desde el código.

**Decisión sobre la primera publicación**: la tubería ya no está bloqueada —el secreto de firma está
puesto—, pero **no se corta `v0.1.0` todavía**. Faltan dos cosas que no son de código y no las
decide un agente: el dictamen `REL-004` y el paseo físico. Publicar antes sería cambiar una
verificación pendiente por una fecha.

## Un rojo intermitente que hay que vigilar, no tapar

El 2026-08-10, la fase `first-launch` de `verify-package.ps1` **falló una vez** en la rama y **pasó
con el mismo commit** en `main`. Mismo código, mismo flujo de trabajo, resultado distinto: es
intermitente, y por eso no es un defecto que se pueda encontrar leyendo.

Lo que se sabe, medido del registro:

- La fase duró **137 s**, que es exactamente el plazo de ventana (90 s) más el de cierre (45 s). Los
  dos se agotaron y el proceso acabó matado, de ahí el `exit code -1`.
- En **esa misma ejecución**, `repair`, `downgrade-refused`, `open-with` y las cuatro fases
  `windows-*` arrancaron la aplicación y la vieron pintar. Sólo falló el primer arranque.
- ~~El primer arranque es el único que migra de verdad, y migrar bloquea el hilo de interfaz.~~
  **Medido el 2026-08-10 y desmentido**: cada ciclo recibe su propia carpeta, así que los cinco
  migran una base nueva, y un arranque entero con sus dieciséis migraciones cuesta **2 292 ms**
  frente a los **90 000 ms** de plazo que aquel fallo agotó. Lo que falló allí no fue un arranque
  lento sino uno que no llegó a ocurrir, y la causa candidata sigue abierta. Detalle en
  [audit-arq005-startup-baseline.md](evidence/stable/audit-arq005-startup-baseline.md).
- **Frecuencia observada: una de cuatro.** No se repitió en las tres ejecuciones siguientes que
  llevaban ese mismo código, ni en las ocho posteriores hasta `fa968de`. Es la cifra contra la que
  comparar si vuelve a verse.
- **Y media pregunta ya estaba contestada en el registro archivado, sin que nadie lo hubiera
  leído.** La línea entera de aquella fase decía `16 migration(s) applied to a new database`: el
  proceso vivió lo suficiente para aplicar las dieciséis, así que «murió antes de migrar» estaba
  descartado desde el principio y las dos hipótesis nunca fueron dos. Lo que no dice ningún registro
  es la otra mitad —si al llegar el plazo quedaba algo vivo que pintar—, porque `exit code -1` es el
  matarile del propio arnés.

**Lo que ya no falta**: desde el 2026-08-10 la verificación **deja diagnóstico** cuando la ventana no
llega. Antes de matar el proceso anota si seguía vivo, cuánto procesador gastó y en cuántos hilos —lo
que separa girar de esperar—, el estado de `library.db` y de `schema_history`, y qué hay en la
carpeta de datos, todo en la misma línea que CI imprime. Detalle en
[audit-first-launch-instrumentation.md](evidence/stable/audit-first-launch-instrumentation.md).

**Lo que no se hace**: subir el plazo de 90 s. Eso convierte la única señal que hay en silencio, que
es el error que ya costó seis ejecuciones con los `cancelled` del generador de medios. Si vuelve a
aparecer, deja de ser trabajo pendiente y pasa a ser la corrección urgente — y ahora dirá algo al
hacerlo.

## Lo terminado el 2026-08-10 (tercera sesión)

Cuatro commits, cada uno con su ciclo, su evidencia bilingüe y sus puertas.

- **ARQ-010 — el contenedor se revisa al construirse.** `ValidateOnBuild` encendido, con una prueba
  que le pasa una colección rota **por la ruta del producto**; afirmar sobre una copia de las opciones
  sólo demuestra la copia. **No destapó ningún registro roto**, que era lo que el plan esperaba, y el
  límite quedó medido: valida 109 de 156 registros, porque los 45 construidos con una factoría son
  opacos por construcción. Cuesta +0,22 ms.
  [audit-arq010-container-validation.md](evidence/stable/audit-arq010-container-validation.md).
- **ARQ-004 — un fallo tiene dónde caer, y ya no puede cerrar la aplicación.** La medición invirtió
  el orden de sus dos mitades: `AppDomain.UnhandledException` **no** impide que el proceso termine,
  sólo deja constancia, así que un comando tiene que capturar siempre — y algo que captura siempre
  necesita un destino siempre. Ese destino no existía (2 de 24 superficies tienen estado de fallo), y
  buscándolo apareció que **el informe de diagnóstico se construía de una sola fuente**, la auditoría
  de renombrados: en una sesión sin renombrados, una aplicación que fallaba parecía sana. Luego, de
  **27 `async void` a 2**, y los dos capturan. −582/+227 líneas.
  [audit-arq004-failure-net.md](evidence/stable/audit-arq004-failure-net.md) y
  [audit-arq004-single-command.md](evidence/stable/audit-arq004-single-command.md).
- **ARQ-005, primera mitad — el candado que nadie podía abrir.** La espera de las teclas multimedia
  salió del `lock` y recibió techo. Bloqueaba el hilo de interfaz en **cada apertura de vídeo**, y sin
  contestación el hilo atrapado sujetaba el candado que la cancelación necesitaba.
  [audit-arq005-media-keys.md](evidence/stable/audit-arq005-media-keys.md).

## Lo terminado el 2026-08-10 (cuarta sesión)

Tres commits, cada uno con su ciclo, su evidencia bilingüe y su verificación completa.

- **La verificación dice cuánto se espera a la ventana, no sólo que llegó.** Y en su primera
  medición desmintió dos cosas que estaban escritas aquí: los **cinco** ciclos migran una base nueva
  —contadas las migraciones en cada carpeta—, no sólo el primero, y el primer arranque tampoco es el
  más lento de los tres. Se miden tres fases y no una **a propósito**, y esa decisión se pagó sola:
  al comparar el antes y el después, `open-with` se repitió con 6 ms de dispersión mientras
  `first-launch` variaba 1245 ms entre ejecuciones del mismo código.
  [audit-arq005-startup-baseline.md](evidence/stable/audit-arq005-startup-baseline.md).
- **ARQ-005, segunda mitad: la ventana existe mientras migra.** La medición previa decidió la forma
  y evitó la corrección falsa: `MigrateAsync` **no cede el hilo en ninguno de sus `await`s** —140 ms
  de 140—, así que un `await` habría dejado la ventana igual de bloqueada con aspecto de arreglada.
  El trabajo va a un hilo propio y la ventana sale en el primer fotograma. De regalo, la prueba de
  superficies huérfanas cazó `StartupView` en el acto y pasó a ser la tercera raíz del grafo.
  [audit-arq005-async-startup.md](evidence/stable/audit-arq005-async-startup.md).
- **TST-001: la puerta de cobertura ya vigila código que no es nuevo.** Re-medir fue lo primero y
  el resultado no era el esperado: dos archivos exactamente igual que el día anterior y el tercero
  **quince puntos peor**, porque ARQ-004 se llevó por delante sus líneas cubiertas sin que nada
  avisara. Hay una lista de vigilados con trinquete en los dos sentidos, y dos de las tres deudas
  quedaron al 100 %. [audit-tst1-coverage-debt.md](evidence/stable/audit-tst1-coverage-debt.md).

## Lo terminado el 2026-08-10 (quinta sesión)

Tres commits de código, cada uno con su ciclo, su evidencia bilingüe y su verificación completa.

- **`BUG-010`: la instancia nativa tiene un dueño.** El sondeo de medios levantaba su propia LibVLC
  con las mismas tres opciones que la de reproducción, así que un proceso que catalogaba y
  reproducía mantenía dos motores nativos — y el contador que afirma «una por juego de opciones» no
  podía ver el segundo. Con la cola pasó lo mismo y era peor: desechaba el medio **sin guarda** y
  dejaba su bandera en alto, de modo que un único fallo al liberar habría matado al trabajador para
  siempre. La regla es de código fuente a propósito, porque en ejecución la segunda instancia es
  invisible; y encontró la clase donde el plan nombraba un caso, con una **tercera** cola en el motor
  de reproducción anotada como `BUG-011`.
  [audit-bug010-native-instance.md](evidence/stable/audit-bug010-native-instance.md).

- **TST-001 queda saldado: la última deuda pasa de 86,73 %/76,00 % al 100 % de líneas y de ramas**,
  con nueve unitarias que apuntan a lo que `ReconcileScannedFiles` **decide** —un escaneo cancelado,
  un resultado que el escaneo no pudo catalogar, una ruta sin fila, una identidad ilegible que cuenta
  como fallo sin costarle el escaneo al resto, contenido `Updated` que refresca la identidad, un
  catálogo que lanza, una cancelación que no es un fallo—. Medir la lista antes de escribir la
  recortó en un tercio: cinco de los puntos anotados leyendo el código ya estaban cubiertos. Y
  apareció uno que la lectura no da: la propiedad `AttemptedCount` no la leía **ninguna** prueba,
  porque comparar registros enteros va por campos y no por propiedades. El suelo de la puerta sube a
  100/100. [audit-tst1-reconcile-coverage.md](evidence/stable/audit-tst1-reconcile-coverage.md).
- **El rojo intermitente ya deja diagnóstico.** Y lo primero fue leer el registro archivado de la
  única ejecución que falló, que contestaba media pregunta: `16 migration(s) applied to a new
  database`. Lo que se instrumenta es la mitad que sigue sin recogerse, más el procesador y los
  hilos, que separan girar de esperar. Nada del diagnóstico puede lanzar —sustituiría al fallo que
  viene a explicar—, así que cada lectura va guardada y lo que salga mal se cuenta dentro de la
  frase. `LaunchDiagnosisTests` saca las funciones del guion publicado **parseándolo** y las ejerce
  contra procesos de estado conocido, incluida una `library.db` que no es una base de datos.
  [audit-first-launch-instrumentation.md](evidence/stable/audit-first-launch-instrumentation.md).

## Por dónde se sigue (decidido, no se re-delibera)

1. ~~**`LIB-015`**~~ **Hecho el 2026-08-14**, en el orden que fijaba el plan. Tres cosas cambiaron al
   medirlas: el lanzador endurecido que se iba a reutilizar **no existía** —los tres `Process.Start`
   del árbol abren un `.msix`, una carpeta y un archivo de medios, y ninguno una dirección—; la clave
   de la caché **no incluye la dirección**, así que `append_to_response` habría servido el payload
   anterior como respuesta nueva, y **subir `ProviderVersion` habría sido peor** —esas filas dejarían
   de leerse y el techo de 180 días sólo se aplica al leer esa misma clave, así que nada podría
   borrarlas nunca—, de modo que la migración vacía lo de TMDB; y la puerta de red cazó
   `www.youtube.com` en `src/`, resuelto con una **segunda lista cerrada** (`HandedOff`) en vez de
   declarar una conexión que no existe.
   [audit-lib015-provider-trailer.md](evidence/stable/audit-lib015-provider-trailer.md).
2. ~~**El eslabón que falta**~~ **Hecho el 2026-08-15**, en cuatro commits y con `LIB-006` de vuelta
   en `VERIFIED` (43 verificados, 2 bloqueados). La primera medición que el plan exigía dio más de lo
   esperado: el puente `media_file_id` → `title_id` **es la identidad del GUID** y no costó ninguna
   migración —`titles` no lo escribe nadie en `src/`, así que todo título del catálogo es un archivo
   escaneado—, y lo que sí faltaba era el **nombre del proveedor**, que no viaja con el candidato y
   sin el cual `GetDetailsAsync` lanza.
   [audit-apply-identification.md](evidence/stable/audit-apply-identification.md).
   Detrás salieron **dos defectos que nadie buscaba**: guardar por primera vez en una ficha sin
   editar devolvía `NotFound` en silencio, y **ningún llamante subía la revisión al guardar**, de modo
   que el control optimista comparaba contra un número que nunca se movía y dos ventanas podían ganar
   las dos. Los dobles en memoria sí la subían, que es por qué ninguna prueba unitaria podía verlo.
   [audit-refresh-resolves-itself.md](evidence/stable/audit-refresh-resolves-itself.md).
   Y el clic destapó el tercero: **el paseo ensamblado montaba la ventana de una forma que la
   aplicación no usa**, dejando el shell fuera del árbol lógico y **todos** los botones enlazados por
   `Command` declarándose deshabilitados. Sólo se veía haciendo clic, y nadie hacía clic.
   [audit-walk-clicks-the-editor.md](evidence/stable/audit-walk-clicks-the-editor.md).

   ~~**El eslabón que falta, tal como se describió el 2026-08-14.**~~ La primera medición de `LIB-016` destapó
   que **nada convierte una identificación en metadata guardada**: `catalog_metadata` sólo lo
   escriben el editor y un `RefreshMetadata` que nadie alimenta —la única asignación de su entrada en
   todo el repositorio está **en una prueba**—, `ResolveMatch` publica un evento que no escucha nadie,
   y `ReviewState.Automatic` sólo se calcula. La sinopsis de `LIB-013` y la clave de `LIB-015` sólo
   llegan a la base a mano.
   **Decidido entero, con el orden de los commits**, en
   [audit-identification-never-reaches-the-catalogue.md](evidence/stable/audit-identification-never-reaches-the-catalogue.md):
   un caso de uso `ApplyIdentification` con sus dos llamantes, `RefreshMetadata` resolviendo por la
   referencia guardada, el editor sin la propiedad que nadie rellena, y el paseo ensamblado llegando
   al editor con clics. **La migración `0018` ya dejó la base preparada.**
   **La primera medición, antes de escribir una línea**: cómo se llega del `media_file_id` de los
   candidatos al `title_id` de `catalog_metadata`. Ese puente no está medido.
   **Ya se corrigió el estado de la matriz**: `LIB-006` pasó a `BLOCKED` el 2026-08-14 con su bloqueo
   en el manifiesto —42 verificados, 3 bloqueados—, y `LIB-007` **se queda `VERIFIED`** a propósito,
   porque su criterio es de umbrales y de corrección persistente, y ambos se cumplen. `LIB-006` vuelve
   a `VERIFIED` sólo cuando el recorrido con clics esté verde.
3. ~~**`BUG-012` — el vigilante que muere al desbordar su buffer.**~~ **Hecho el 2026-08-15**, y la
   primera medición desmintió la última mitad de lo que estaba escrito aquí: un root `Continuous`
   **nunca sale** de `_watching`, porque `StartAsync` es un `Task.WhenAll` con el planificador de
   reserva y ése **no termina nunca**, así que ni un escaneo manual podía revivir al vigilante —sólo
   volver a arrancar la aplicación—. Un desbordamiento es ahora «he perdido eventos»
   (`WatchErrorPolicy` en el dominio), viaja como `FileChangeBatch.EventsLost`, se convierte en **un**
   escaneo de recuperación y **la vigilancia sigue**; el buffer se pide al techo de 64 KiB; y un
   vigilante que muere de verdad se reintenta en la siguiente pasada de reserva, que es el latido que
   esta parte ya tenía. **El desbordamiento real no se reproduce en esta máquina** —64 000
   operaciones, cero desbordamientos, con 8 KiB y con 64 KiB—, así que la decisión se prueba en el
   dominio y no se finge una prueba de integración determinista.
   [audit-bug012-watcher-survives-overflow.md](evidence/stable/audit-bug012-watcher-survives-overflow.md).
4. ~~**`LIB-016`**~~ **Hecho el 2026-08-15.** Apagado por defecto, rancio a los 90 días —por debajo
   del techo de 180, con prueba de la desigualdad—, 20 por pasada, las más rancias primero, sólo
   identificadas, y cediendo ante un escaneo o un vídeo abierto, comprobado **antes de cada ficha**.
   El interruptor vive en Ajustes → Privacidad y **no existe sin conexión consentida**. El texto del
   propósito de red cambió con el código. **La medición añadió lo que no estaba previsto**: un
   `refreshed_utc` nulo con `provider_key` no lo escribe **ninguna** ruta de producción hoy
   (`identifiedWithNoDate=0`), así que los nulos primero son la guarda de una fila que nadie escribe,
   no un caso del campo. Aceptación por ejecución: el canario de red cuenta **0** conexiones con el
   interruptor apagado y **2** encendido, en el mismo proceso hijo.
   [audit-lib016-automatic-refresh.md](evidence/stable/audit-lib016-automatic-refresh.md).

5. **El paseo autónomo de toda la aplicación. Decidido entero el 2026-08-15 y va por delante de
   `DES-001` y del rediseño**, porque es su red y no un extra. Todo lo que sigue está medido, no
   supuesto.

   **Lo medido.** 129 controles de mando en las 48 vistas —95 `Button`, 18 `CheckBox`, 8 `ComboBox`,
   5 `Slider`, 2 `ToggleButton`, 1 `RadioButton`— más 17 `ListBox`. Con ratón se pulsan **dos**:
   `RefreshProviderMetadata` y el candado del título. `MouseDown`/`MouseUp` no aparecen en ningún
   otro archivo.

   **El ancla, ya probada.** Sólo 60 de los 129 tienen `x:Name`, así que el paseo localiza por la
   **clave de recurso** tras `AutomationProperties.Name` —239 elementos la llevan, 80 pruebas la
   exigen y un rediseño no la quita—. Probado contra un control sin `x:Name`. **Los dos que no tienen
   clave sí tienen nombre**, por `{Binding}`: son elementos de lista —el título de la tarjeta, la
   ruta del duplicado— y su ancla es **el dato que el propio paseo sembró**, que es mejor todavía
   porque ata el clic a algo que la prueba controla. No hay defecto de accesibilidad ahí.

   **Una premisa que se cayó al mirarla.** Aquí se escribió que estaba «por medir si el reproductor
   es alcanzable en headless con LibVLC». Ya estaba contestado en el propio archivo del paseo: sus
   escenas corren **con el motor real decodificando fotogramas**, y una de ellas reproduce, pausa con
   la barra espaciadora y guarda un marcador a mitad de sesión. Lo que falta del reproductor no es
   llegar: es **pulsar su transporte con el ratón**.

   **La forma, decidida.** Una puerta que compara los controles de mando del árbol contra los que la
   suite **pulsó de verdad**, con una lista de pendientes que **sólo puede encoger**. El registro es
   **en ejecución** —el propio `Click` anota lo que pulsa y la puerta lee ese informe, como ya hacen
   `run-accessibility.ps1` y `run-recovery.ps1`— y **no** leyendo el fuente como texto, que es lo que
   ya se ha roto tres veces al mover código. Un control cuenta como cubierto sólo con las tres cosas:
   clic real, afirmación **sobre el efecto**, y un clic **al lado** que no hace nada.

   **El orden de las tandas**, por uso y por riesgo: (1) biblioteca y ficha —abrir, filtrar, ordenar,
   favorito, visto, valoración—, (2) transporte del reproductor, (3) editor y renombrado, (4) ajustes
   —incluido el interruptor de `LIB-016`—, (5) bandeja de revisión y duplicados, (6) copia y
   restauración, (7) actualización y recuperación. Cada tanda cierra su área y saca sus entradas de
   la lista.

   **La tanda 1 está hecha (2026-08-15).** De **2** controles pulsados con ratón a **15**, sobre
   **128 identidades** —129 declaraciones, y el único colapso es el botón Atrás, declarado dos veces
   en las dos ramas excluyentes de la biblioteca—. La puerta es `eng/check-walk-coverage.ps1`, la
   lista con motivos es `eng/walk-pending.txt` y el trinquete está en **113**. Detalle en
   [audit-walk-first-batch.md](evidence/stable/audit-walk-first-batch.md).
   - **El ancla entregada no alcanzaba el botón Atrás.** Las dos ramas de detalle viven en el árbol
     visual a la vez, así que casar por la clave encontraba **dos** controles donde un clic sólo
     llega a uno. Sólo lo que está en pantalla es candidato, y los diez botones de valoración
     —que comparten nombre accesible **por diseño**— se desempatan por el `HelpText`.
   - **Y la puerta cazó un segundo defecto en su primera ejecución**: el botón de refresco del editor
     se pulsaba por su `x:Name` y las vistas lo declaran por su clave, así que **el mismo control
     tenía dos nombres** y quedaba pulsado bajo uno y pendiente bajo el otro.
   - **El tercero es el peor y sólo apareció al buscarlo**: el clic «al lado» se ponía a un control
     de altura por encima, y en una fila que envuelve eso es **la fila anterior**. El clic de control
     de «Quitar la nota» **apagaba el interruptor de favorito**, y el paseo callaba porque su aserción
     sólo preguntaba por la nota. Un clic de control que pulsa otra cosa **es una segunda pulsación
     sin registrar**. Ahora el punto se elige **por geometría** —fuera de todo control de mando en
     pantalla— y no con `InputHitTest`, que ya se midió que no predice a dónde va un clic.
   - **Inalcanzables, medidos y nombrados**: los dos `DetailsTrailerLinkAction` —el de la ficha de
     película y el de la de serie—, porque pulsarlos entrega la dirección al shell de Windows y abre
     un navegador de verdad en la máquina que corre la puerta. Es lo que `LIB-015` decidió, así que
     es el límite del paseo, no un defecto.
   - **Pendientes de esta misma área por sembrado, no por alcance**: `MovieResumeAction` (progreso
     guardado), `MovieTrailerAction` (un archivo de tráiler descubierto por grupo de versiones) y
     `EpisodePlayAction` (la ficha de serie).
   - **Al contar los 129 se puede contar mal**: la primera medición dio **142** porque
     `<ComboBoxItem>` casa con `<ComboBox` sin un límite de palabra.

   **La tanda 2 está hecha (2026-08-15): el transporte del reproductor.** De **15** a **22**
   pulsados; el trinquete baja a **106**. Es la tanda que justifica el trabajo: encontró **tres
   defectos del producto**, los tres visibles, activos e incapaces de hacer nada, y los tres vivos
   porque **el reproductor responde al teclado él mismo**. Detalle en
   [audit-walk-second-batch.md](evidence/stable/audit-walk-second-batch.md).
   - **El recuadro de estado de vídeo cubría todo el escenario** —medido en 1280×1200 sobre 1280×1200,
     opaco— encima del vídeo y de la barra, tragándose cada clic. No fijaba alineación; ahora es un
     distintivo en una esquina. **Los otros cinco superpuestos tampoco la fijan**: se corrigió sólo el
     que la medición demostró que estorbaba, y los demás se verán en sus tandas.
   - **`PlayerViewModel` no llamaba a `RaiseCanExecuteChanged` en ninguna parte**, así que el estado
     habilitado de los botones se congelaba: se pausaba con el ratón y **Reanudar quedaba deshabilitado
     para siempre**.
   - **El deslizador de volumen era el único `OneWay` de los cinco** y su vista no tenía manejador;
     `SetVolumeAsync` tenía dos llamantes y los dos eran teclado.
   - **Y cuatro trampas del arnés**: el desmontaje **reemplazaba** el fallo de la escena dentro del
     `using`; un clic fuera de la ventana no decía nada; **el centro de un control de rango suele ser
     donde ya está** (0-200 arrancando en 100); y la caché de muestras **ignoraba la duración pedida**,
     así que un salto de 30 s se salía de un archivo de 12 y dejaba Detener deshabilitado por una
     razón ajena.

   **La tanda 3 está a medias a propósito (2026-08-15): el editor sí, el renombrado no.** 22 → **30**
   pulsados, trinquete **98**. El editor quedó entero —seis candados, Guardar y Restaurar—, y
   **Guardar afirma sobre la base**, que es el primer control del paseo cuyo efecto no está en la
   pantalla. Detalle en [audit-walk-third-batch.md](evidence/stable/audit-walk-third-batch.md).
   - **El renombrado no puede renombrar.** La aplicación ensamblada pide renombrar cada archivo **al
     nombre que ya tiene** —`new RenameRequest(file.Path, Path.GetFileName(file.Path))`— y
     `RenamePolicy` contesta correctamente `NoChange` sin operación. El plan sale **siempre vacío**,
     Renombrar y Deshacer no pueden hacer nada, y la casilla de consentimiento guarda una decisión que
     no se ofrece.
   - **Y no hay nada que componga un nombre**: ése es el único `RenameRequest` de producción del
     repositorio. **No falta un cable, falta una decisión** —cómo se llama un archivo renombrado—, y
     es del propietario. El paseo la registra en vez de inventarse un convenio, y los tres controles
     siguen pendientes nombrando esto.
5. **`DES-001`, la mitad del agente: hecha el 2026-08-15.** La descripción del manifiesto ya no es
   una cadena con una barra: es `ms-resource:AppDescription`, y `eng/build-package-resources.ps1`
   construye un recurso por idioma **declarado en el propio manifiesto**, con el texto leído del
   primer párrafo de cada README —de donde winget ya sacaba el suyo—, así que las dos vías de
   instalación dicen lo mismo. Detalle en
   [audit-des001-package-description.md](evidence/stable/audit-des001-package-description.md).
   - **Dos mediciones decidían si era posible**: `makepri.exe` está junto a `makeappx.exe`, y su
     salida es **determinista** —mismo hash desde dos directorios distintos—, que es lo que la
     comparación de reproducibilidad exige.
   - **Y una trampa que ningún error nombra**: el DOM de XML escribe `xml:space` como `d2p1:space`
     con su propio espacio de nombres, y `makepri` contesta «PRI224: root node not found», que no
     nombra ni el atributo ni el archivo. Los `.resw` se escriben como texto.
   - **Tocar el manifiesto caducó dos mediciones manuales** —`windows-lifecycle.json` degrada a
     «bloqueado» y la suite lo acepta; `updater-handover.json` pone `UpdateHandoverTests` en rojo—, y
     **se rehizo con permiso del propietario**. De paso, el ciclo **quedó versionado**:
     `eng/run-sandbox-handover.ps1`, `eng/sandbox-handover.ps1` y
     `eng/measure-handover-with-handler.ps1`. Antes el documento describía los pasos y el guion vivía
     fuera del repositorio.
   - **Tres defectos del arnés del sandbox, ninguno con un mensaje que lo nombrara**: el `&` de
     PowerShell rompe el `.wsb` porque es **texto XML** («el archivo de configuración no es válido»);
     cerrar el sandbox matando `WindowsSandboxServer` —del anfitrión— tumba la ejecución siguiente; y
     la ventana la tiene `WindowsSandboxRemoteSession`, no un `WindowsSandboxClient`, que en esta
     compilación no existe.
   - **Y un hallazgo del producto, reproducido dos veces**: sin nada registrado para `.msix`,
     `Process.Start` devuelve **nulo** y la aplicación dice «Windows no lo aceptó» —cierto: la base
     quedó en 372 736 bytes antes y después—, **pero Windows deja el diálogo «Elegir una aplicación»
     en pantalla**. Es la imagen espejo de lo que la mitad `withHandler` descarta. Medido y nombrado;
     **no tocado**.
   - **Sigue abierto**: los cinco activos de marca, que son del propietario.
5. **`DES-001` — la instalación también se ve, y hoy no está diseñada.** Los cinco activos de
   `src/ApSolutions.LocalMedia.Windows.Package/Assets/` son marcadores de posición del 3 de agosto
   —de 576 B a 7 KiB— y son **lo primero que alguien ve del producto**, antes que ninguna vista. Y
   hay un defecto medido: el manifiesto declara `es-ES` y `en-US` pero su `Description` es **una sola
   cadena con una barra dentro** («Biblioteca y reproductor de vídeo local / Local video library and
   player»), que Windows enseña tal cual en los dos idiomas. La localización de verdad va con
   `ms-resource:` y un recurso por idioma, como **ya se hace en winget**, que sí tiene sus dos
   `locale.es-ES.yaml` y `locale.en-US.yaml`. Ojo: tocar el paquete obliga a reempaquetar o las
   pruebas de empaquetado fallan.
6. **El rediseño visual**, que el propietario está preparando con Claude Design. Lo que este
   repositorio le debe es el inventario de **todo lo que se ve**, y ya está escrito y medido en
   [docs/design/SURFACES.es.md](design/SURFACES.es.md) y [SURFACES.en.md](design/SURFACES.en.md): 48
   vistas en 15 áreas, las 468 cadenas por idioma, los 23 mensajes distintos de la vista de
   actualización —15 estados y 8 motivos de rechazo, que no son errores del usuario y piden otro
   tratamiento— y los cinco activos de instalación. **`LIB-016` añade superficie visible nueva** (el
   interruptor de refresco automático y su texto), así que el inventario no se cierra hasta que esa
   entrada esté hecha.
7. **Documentación**: `DOC-101`, `DOC-201`, `T44.1`-`T44.6` y el manual de usuario, que se escribe
   desde la aplicación construida y no desde el código — por eso va el último, y sus capturas
   dependen del rediseño.

## Lo terminado el 2026-08-15 (octava sesión)

Cuatro commits, y la cadena que tenía `LIB-006` en `BLOCKED` quedó cerrada: el manifiesto lee **43
verificados, 2 bloqueados**. `ApplyIdentification` escribe lo que el proveedor sabe por sus dos
llamantes —la bandeja y el camino automático de ≥90 %, que no existía—, `RefreshMetadata` resuelve
por la referencia guardada, el editor perdió la propiedad que nadie rellenaba, y el paseo ensamblado
**pulsa el botón con el ratón**. Detalle en
[audit-apply-identification.md](evidence/stable/audit-apply-identification.md),
[audit-refresh-resolves-itself.md](evidence/stable/audit-refresh-resolves-itself.md) y
[audit-walk-clicks-the-editor.md](evidence/stable/audit-walk-clicks-the-editor.md).

**Dos defectos que no estaban en ninguna lista y salieron midiendo**: guardar por primera vez en una
ficha sin editar devolvía `NotFound` en silencio —el editor traducía eso a nada, ni conflicto ni
cambio—, y **ningún llamante subía la revisión al guardar**, de modo que `WHERE revision = $expected`
comparaba contra un número que nunca se movía y dos ventanas podían ganar las dos.

## Lo terminado el 2026-08-14 (sexta sesión)

- **`BUG-011`: una sola cola de liberación en todo el proceso.** El motor de reproducción guardaba
  la tercera, y desechaba el medio nativo **dentro de su candado y sin guarda**: una excepción allí
  salía del bucle con la bandera del trabajador en alto, así que un único fallo lo mataba para
  siempre y todo lo abierto después se filtraba en silencio. El rojo fue **doble a propósito** —la
  regla de origen, que se satisface moviendo texto, y una prueba de comportamiento que mide desde
  fuera dónde descansa el medio cuando el motor lo suelta—. `LibVlcFactory` ganó un vaciado con
  techo que **no lanza** al agotarse, el motor soltó cola, candado, bandera, drenaje y constante de
  reposo (−52/+27), y la lista que sólo puede encoger quedó **vacía**. La prueba de resistencia no
  se escribió de cero: `HandleGrowthTests` ya abría y cerraba el motor treinta veces en un proceso
  hijo, que es el único sitio donde un contador de proceso se puede leer sin que las demás suites
  escriban en él. Y esa columna **no podía ser roja antes**, lo que se dice en la evidencia en vez
  de presentarla como prueba.
  [audit-bug011-engine-release-queue.md](evidence/stable/audit-bug011-engine-release-queue.md).

- **La cola de la auditoría quedó cerrada entera**: `ARQ-013`, `ARQ-012` (−836/+196 en 88 archivos),
  `ARQ-014` y `QA-001` —esta última con **cero** violaciones que corregir: era puerta, no deuda—.
  Más dos rojos ajenos que aparecieron por el camino: el canario de red pedía un puerto de un rango
  que el sistema reserva ([audit-canary-port.md](evidence/stable/audit-canary-port.md)) y una prueba
  del corpus borraba un archivo que otra suite podía estar leyendo
  ([audit-corpus-shared-file-race.md](evidence/stable/audit-corpus-shared-file-race.md)).
- **Y un rojo que habría aparecido en la primera publicación**: la herramienta que firma **no
  compilaba** —faltaba el encabezado de licencia y su proyecto estaba **fuera de la solución**, así
  que ninguna puerta lo construía—, y `release.yml` la ejecuta con `dotnet run` en el paso que
  verifica la firma. Corregido; el proyecto entra en la solución y una regla nueva impide que otro se
  quede fuera. **No lo encontró una puerta de este repositorio**: lo encontró la sesión de IT del
  propietario ejecutando la prueba de restauración de la clave.
  [audit-release-signing-tool-build.md](evidence/stable/audit-release-signing-tool-build.md).

## El bloque nuevo: sinopsis y tráiler (2026-08-14)

Pedido por el propietario y **decidido entero** en
[2026-08-14-synopsis-and-trailer.md](superpowers/plans/2026-08-14-synopsis-and-trailer.md). Va antes
de la documentación a propósito: el manual se escribe desde la aplicación construida, y escribirlo
antes de este bloque sería escribirlo dos veces.

- **`LIB-013` hecho**: la sinopsis se lee en las dos fichas. Ya estaba guardada de punta a punta; lo
  único que faltaba era el camino de lectura.
- **`LIB-014` hecho**: el tráiler **local** se reproduce desde la ficha, con la convención de Plex,
  Jellyfin y Kodi. No hizo falta camino de reproducción nuevo: `OpenLooseFile` ya valida extensión,
  existencia y no escribe fila de catálogo.
- **`LIB-015` pendiente**: la clave de YouTube al navegador. **Cuesta una migración** —campo nuevo en
  `MetadataDetails` y columna—, más `append_to_response=videos` en la petición que ya se hace. No hay
  host nuevo.
- **`LIB-016` pendiente**: el refresco automático, apagado por defecto. **Toca un contrato de
  privacidad**: hoy el propósito declarado de TMDB dice «the metadata a person explicitly asked to
  identify or refresh», y con un refresco automático deja de ser verdad, así que ese texto cambia con
  el código. El techo de 180 días de la caché no se sube nunca.

**La decisión sobre el tráiler remoto no se re-delibera**: dentro de la aplicación sólo si es un
archivo local. La vía de LibVLC contra YouTube incumple sus términos, y su incrustación oficial
pediría un WebView con hosts sin declarar, publicidad y cookies.

## Dos cosas aprendidas el 2026-08-14 que salieron caras

- **Un reemplazo mecánico validado sólo con «compila» es un cambio sin medir.** El guion que migró
  las 59 copias del buscador de raíz se llevó el método **equivocado en catorce archivos**, porque
  buscaba el primero con la forma correcta en vez del que contiene el paseo. Ninguno llegó a un
  commit, y no por suerte: dos los cazó el compilador, **trece los cazó la regla nueva** —la propia
  puerta que ese trabajo añadía midió su propio destrozo— y tres más las suites, porque devolvían
  `…/src/…Presentation` y no la raíz. Lo que cerró el hueco no fue esperar a que algo fallara sino
  buscar en el diff **toda** devolución de un subdirectorio.
- **Un error que habla del arnés esconde el del sistema.** El canario de red falló con un
  `ObjectDisposedException` en su propio constructor, que no dice nada. La primera corrección no hizo
  pasar la prueba: la hizo **decir la verdad** —«ningún puerto libre en el rango»—, y esa frase se
  midió en un comando: Windows reserva 50996-51095 y el rango fijo de la prueba estaba entero dentro.
  No era intermitente; las exclusiones se asignan al arrancar el anfitrión.

## Pendiente tuyo (sólo lo que un agente no puede hacer)

- ~~Añadir el secreto `RELEASE_SIGNING_SECRET_KEY` al repositorio público.~~ **Hecho el 2026-08-10
  (22:46 UTC)**, y era lo único que impedía cortar la primera versión pública: `release.yml` exige
  que `SHA256SUMS.txt.minisig` exista y verifique, y se detenía ahí. Comprobado por nombre y fecha
  con `gh secret list`, que nunca enseña el valor. La copia sigue donde la dejaste (ver
  `SECURITY.md`), y **el respaldo cifrado sigue siendo la única red**: un secreto de Actions no se
  relee.
- El **paseo físico manual de diez minutos**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- La **copia de seguridad cifrada** de la clave de firma. El destino y el cifrado están decididos
  fuera de este repositorio (bóveda de IT); lo que importa aquí es cómo se comprueba que la copia
  sirve, y **no es que el archivo descifre**: se firma algo trivial con la copia restaurada y se
  verifica contra [`eng/release-signing.pub`](../eng/release-signing.pub). Repetir cada trimestre,
  porque un respaldo corrupto no avisa.
  - **Hecho el 2026-08-14**: la sesión de IT del propietario ejecutó la prueba que vale —restaurar,
    firmar y verificar— y el respaldo **sirve**. Comprobado por ejecución, que es la única forma.
  - **Y de paso, dos avisos sobre cómo se comprueba esto.** Desde aquí se midió el respaldo por
    **tamaño** contra un umbral, y esa medición dio «no cumple» cuando la realidad era que sí: un
    proxy puede estar exactamente en su umbral y no significar nada. Después se dedujo, de un archivo
    de 58 bytes, que no podía ser la clave porque no cabe en el **formato de fichero de minisign** —
    y este proyecto no usa ese formato de fichero, sólo su verificación. **Las dos veces el error fue
    el mismo**: sustituir la ejecución por una deducción sobre metadatos. Del tamaño de un archivo
    cifrado no se deduce su contenido, y de un formato que no se ha comprobado que se use, tampoco.
  - Repetir la prueba **cada trimestre**, porque un respaldo corrupto no avisa.
- **La notificación de exportación** a `crypt@bis.doc.gov` y `enc@nsa.gov`: el texto está redactado
  entero en [LEGAL.es.md](legal/LEGAL.es.md) y sale de tu identidad, por eso es tuya.
- **El dictamen jurídico profesional** (`REL-004`), **reducido el 2026-08-14**. Sus dos preguntas de
  licencia están cerradas por ingeniería, no por dictamen: en vez de encargar quién interpretaba el
  §6 de la LGPL-2.1, la versión pasó a la opción que ambas licencias enuncian **sin condiciones**
  —§6(d) y el último párrafo del §3 de la GPL-2.0: ofrecer el fuente desde el mismo sitio del que se
  descarga el binario—, y `release.yml` adjunta ahora `vlc-3.0.23.tar.xz` verificado y el archivo de
  LibVLCSharp. **Lo que queda del encargo es marca, dominio y la notificación de exportación**, que es
  donde el criterio ajeno aporta algo.
  [audit-corresponding-source.md](evidence/stable/audit-corresponding-source.md).
- Las decisiones económicas de siempre: certificado Authenticode, Store, hardware ARM64.

## Cosas aprendidas que conviene no volver a aprender

- **`eng/verify.ps1` no es lo que ejecuta CI**: CI corre además
  `eng/run-accessibility.ps1 -Mode Verify -Passes 2` y `eng/run-recovery.ps1 -Mode Verify -Passes 2`.
  Conviene correrlas. Pero **cuidado con la conclusión fácil**: el rojo que llegó a `main` el
  2026-08-10 apareció en **tres sitios distintos** en tres ejecuciones —pasada 1, pasada 2, y la
  suite dentro de `verify.ps1`—, así que no faltaba una puerta: **la carrera no se reproduce en
  esta máquina**. Más pasadas son más tiradas, no determinismo. Contra una carrera, lo que vale es
  quitarla, no buscarla.
- **Observar un estado transitorio obliga a esperar a que termine antes de salir.** La prueba que
  comprueba la vista de arranque afirma sobre algo que dura lo que dura el trabajo de fondo, y se
  marchaba en mitad: el `Task.Run` seguía con la base abierta cuando el desmontaje borraba la
  carpeta. Aquí terminaba a tiempo y pasaba; en un runner más lento, no. La aserción no necesita la
  espera, pero el desmontaje sí.
- **Una línea base de una sola medida no es una línea base.** La fase que había que vigilar resultó
  ser la más ruidosa de las tres —1245 ms de variación con el mismo código—, y la señal la dieron
  los dos controles que se añadieron «de más». Medir el sujeto sin medir nada con qué compararlo
  produce un número que no puede distinguir el cambio del día que hacía.
- **Un número heredado se re-mide aunque se dé por hecho que ha mejorado.** De los tres de TST-001,
  dos estaban exactamente donde los dejaron y el tercero había **retrocedido**. Lo esperado era lo
  contrario, y suponerlo habría dejado la deuda peor de como se creía.
- **Una premisa escrita en un documento propio también hay que medirla.** «El primer arranque es el
  único que migra» estaba en dos sitios y era falsa: contar `schema_history` en cada carpeta cuesta
  un minuto y descartó la causa candidata del único rojo abierto.
- **Una prueba que falla porque la corrección funcionó no se arregla en el código.** La del nombre
  accesible perdió una carrera contra el propio arranque asíncrono: para cuando miraba, el shell ya
  había ocupado el sitio. Ahí lo que se reapunta es la prueba, y decir por qué evita que la próxima
  lectura la tome por un defecto.

- **Verificar con el teclado no es verificar con el ratón.** El paseo ensamblado conducía la
  aplicación con `Window.KeyPress` y **nadie usaba los clics** de `Avalonia.Headless`. El primer clic
  destapó que el propio paseo montaba la ventana de una forma que la aplicación no usa —
  `AssembledStartup.FinalContent` **saca** el `ShellView` de su contenedor y el paseo lo remontaba en
  otra ventana—, dejando el shell **fuera del árbol lógico**. Un `Button` sólo consulta el
  `CanExecute` de su comando estando en el árbol lógico, así que **todos** los botones enlazados por
  `Command` se declaraban deshabilitados. Los que usan `Click=` no, y por eso era invisible.
- **`Window.InputHitTest` no predice a dónde va un clic** en Avalonia headless: nombraba el
  `ScrollContentPresenter` mientras el clic llegaba al botón. La guarda escrita «para asegurar» el
  clic era lo único que fallaba, y creerla habría declarado roto algo que funciona. Se afirma sobre
  el **efecto**, con un clic **al lado** como control.
- **Antes de instrumentar un fallo, léelo entero donde quedó archivado.** Las dos hipótesis del rojo
  intermitente se daban por indistinguibles en dos documentos, y la línea de aquella ejecución
  —`16 migration(s) applied to a new database`— descartaba una de las dos desde el primer día. Costó
  un `gh run view --log` filtrado. Un registro que nadie ha leído no es una pregunta abierta.
- **Una lista de huecos escrita leyendo el código es una hipótesis.** La de las ramas que faltaban en
  `ReconcileScannedFiles` se recortó **un tercio** al medirla, y el hueco que no estaba en ella
  —una propiedad que ninguna prueba leía— era el que ninguna lectura podía dar, porque la igualdad
  de un registro va por campos.
- **La puerta de cobertura lee de `HEAD`, no del disco.** Con los archivos nuevos sólo preparados en
  el índice declara «ningún archivo nuevo» y sale verde. Hay que confirmar el commit y volver a
  ejecutar `eng/check-coverage.ps1` **antes** del push, o CI será quien encuentre el rojo.
- **Un hallazgo archivado como «no explotable» es un hallazgo sin medir.** De los dos que la auditoría
  dejó anotados, uno era una entrega directa al shell de Windows y el otro no existía. No se supo
  hasta escribir la prueba que lo habría explotado.
- **Una prueba que sale verde antes de la corrección no es una buena noticia**, es la hipótesis
  avisando de que estaba mal. Ahí es donde hay que parar y volver a medir.
- **`eng/verify-package.ps1` compara dos checkouts limpios**, así que se niega a correr con archivos
  sin preparar en el índice. Un `git add -A` antes de `eng/verify.ps1` ahorra media hora.
- **Se extrae una clase cuando sus pruebas pueden seguirla**, y la puerta de cobertura es quien lo
  decide, no la intuición. `WindowLifecycle` compilaba y los recorridos ensamblados estaban en verde;
  aun así volvió, como `WindowsFilePickers` antes.
- **Las pruebas que leen la composición como texto se rompen cada vez que algo se mueve.** Van tres.
  Al sacar código de `CompositionRoot`, actualizar `CompositionSourceText` y `CompositionGraph` es
  parte del traslado, no un arreglo posterior.
- **`AppDomain.UnhandledException` no impide que el proceso termine**, sólo deja constancia. Eso
  invirtió el orden de las dos mitades de ARQ-004: si un comando no puede permitirse dejar escapar
  nada, tiene que capturar siempre — y algo que captura siempre necesita un destino siempre, así que
  el destino va antes.
- **Antes de escribir «asíncrono», comprobar que algo cede el hilo.** `Microsoft.Data.Sqlite`
  implementa buena parte de su superficie `Async` de forma síncrona. Un `await` sobre algo que no cede
  deja el hilo igual de bloqueado **con aspecto de arreglado**, que es peor que no tocarlo.
- **Migrar N clases a una supone que las N hacían lo mismo, y no lo hacían.** Dos de veinticuatro
  guardaban comportamiento propio, y lo cazó la suite, no la lectura: una comprobaba `CanExecute`
  dentro de `Execute` y de ahí colgaba una validación real. Nunca se migra sin correr la suite entera.
- **Dentro de un `async void`, una guarda de argumento no es una guarda**: lanza dentro de la máquina
  de estados y se postea al contexto, de modo que quien se equivocó nunca se entera. Hay que partir el
  método en uno síncrono que valida y otro que espera.
- **Hay rojos que no son rojos, son cuelgues.** Un candado retenido por un hilo que no vuelve no
  produce un aserto roto, produce una suite que no termina. Ahí el «rojo archivado» no existe, y se
  dice en la evidencia en vez de fingir uno.
