# El menú del riel que el prototipo nunca dibuja / The rail menu the prototype never draws

Medición del menú contextual que `design/AP Reelume.dc.html` lleva escrito para cada destino del riel
de navegación, y de qué parte de lo que ofrece existe ya en la aplicación. / A measurement of the
context menu that `design/AP Reelume.dc.html` carries for every rail destination, and of how much of
what it offers already exists in the application.

Rama / Branch: `apsolutionscode/nifty-proskuriakova-3be693`. Fecha / Date: 2026-09-03.

Motivo: se pidió proponer el menú como alcance nuevo. La medición dice que el prototipo no lo dibuja
y que la mayor parte de lo que ofrece ya está en pantalla, así que el menú se rechaza y lo que sí
falta entra por su cuenta. / Reason: the menu was raised as new scope. The measurement says the
prototype does not draw it and that most of what it offers is already on screen, so the menu is
rejected and what is genuinely missing enters on its own.

## 1 · El menú está escrito y desconectado / The menu is written and unwired

El prototipo define seis listas completas —`menuDefs`, con estilos, velo de cierre, manejo de
`Escape` y un caso especial para Ajustes— y **nada las abre**. / The prototype defines six complete
lists — `menuDefs`, with styles, a dismiss veil, `Escape` handling and a special case for Settings —
and **nothing opens them**.

Leído en el código: / Read in the code:

```
withPanel = {}                                   la tabla de destinos con panel está VACÍA
n.go = setState({ route, menu: null, tip: null }) el clic navega y CIERRA el menú
aria-haspopup                                     NUNCA se emite en el riel (hasPanel siempre false)
```

Medido ejecutándolo, servido por HTTP y con el guion vivo (no el snapshot estático): / Measured by
running it, served over HTTP with live script (not the static snapshot):

```
botones del riel con aria-haspopup                     0 de 7
elementos [role=menu] tras clic en «Biblioteca»        0
elementos [role=menu] tras contextmenu                 0
elementos [role=menu] tras contextmenu con la ruta ya activa  0
```

**No es una regresión de esta copia.** Las dos versiones del prototipo que existen en el historial
—`d9226e8` y `4a572b5`— traen `withPanel = {}` vacío: el menú nunca estuvo vivo aquí. / **This is not
a regression in this copy.** Both prototype revisions in the history carry an empty `withPanel`: the
menu was never alive here.

Para leer su contenido hubo que forzarlo con una copia desechable —poblar `withPanel` y hacer que el
clic escribiera `menu: n.id`—, que no se ha versionado. / Reading its contents required forcing it
with a throwaway copy — populating `withPanel` and making the click write `menu: n.id` — which was
not committed.

**Y la captura headless tenía su propia trampa**: el menú se dibuja con la animación `apr-in`, que
arranca en `opacity: 0`. Chrome headless fotografía antes de que termine, así que las tres primeras
capturas salieron con el panel invisible y el contenido de detrás leyéndose a través — creíbles y
falsas. Se leyó el estilo calculado (`opacity: "0"`) y se anuló la animación antes de volver a
capturar. / **And the headless capture had a trap of its own**: the menu is drawn with the `apr-in`
animation, which starts at `opacity: 0`. Headless Chrome shot before it finished, so the first three
captures showed an invisible panel with the content behind reading through — believable and false.
The computed style was read (`opacity: "0"`) and the animation disabled before capturing again.

## 2 · Qué ofrecería, y qué existe ya / What it would offer, and what already exists

Diecinueve entradas en seis listas, ninguna repetida entre destinos: / Nineteen entries in six
lists, none repeated across destinations:

| Destino / Destination | Entradas / Entries | En la aplicación hoy / In the application today |
|---|---|---|
| Inicio / Home | Continuar viendo · Añadido recientemente · Escanear ahora | Los dos primeros **son los carriles de esa pantalla**. «Escanear ahora» **no existe** (§3). / The first two **are that screen's own rails**. "Scan now" **does not exist** (§3). |
| Biblioteca / Library | Todo · Películas · Series · En curso · No disponible · Añadir medios · Gestionar raíces | **Existe todo y más**: tres píldoras de tipo, un desplegable de estado con siete opciones, uno de orden con cuatro y un «limpiar», todo a la vista en la fila de filtros. / **All of it exists and more.** |
| Cursos / Courses | Todos los cursos · Con hilo pendiente · Terminados · Marcar una carpeta como curso | Sólo la cuarta. Los tres filtros **no existen** (§4). / Only the fourth. The three filters **do not exist** (§4). |
| Revisión / Review | Todos los pendientes · Sugeridos 60–89 % · Pendientes < 60 % | Ninguno. La confianza se muestra en la tarjeta del candidato y no filtra. / None. Confidence shows on the candidate card and does not filter. |
| Duplicados / Duplicates | Todos los grupos · Preferidas por calidad | Ninguno. / None. |
| Ajustes / Settings | Sus propias secciones | Existen, y se navegan desde la propia pantalla de Ajustes. / They exist, navigated from the Settings screen itself. |

El riel de la aplicación tiene los mismos seis botones con su tooltip y su comando de navegación, y
ningún `Flyout` ni menú — igual que el prototipo ejecutado. / The application's rail carries the same
six buttons with their tooltip and navigation command, and no `Flyout` or menu — exactly like the
running prototype.

## 3 · El escaneo manual está construido y no tiene botón / The manual scan is built and has no button

`LIB-002` compromete «escaneo inicial, al iniciar, **manual** e incremental» y está `VERIFIED`. El
motor lo soporta; la interfaz no lo ofrece. / `LIB-002` commits to "initial, startup, **manual**, and
incremental scanning" and is `VERIFIED`. The engine supports it; the interface does not offer it.

```
ScanTrigger.Manual        declarado en Application/Discovery/ScanContracts.cs
                          usado SÓLO por pruebas: 0 llamadores en src/
ShellSurfaces.StartScan   un único disparador desde la interfaz, y es el escaneo INICIAL
                          al añadir una raíz (ShellViewModel, rama CanStartInitialScan)
```

Una raíz ya añadida no se puede volver a escanear a petición: sólo la vigilancia y el temporizador de
respaldo la vuelven a mirar. El criterio de `LIB-002` verificado no promete un control —dice que los
cambios aparecen sin bloquear la interfaz y que los escaneos se cancelan y reanudan—, así que la
fila no queda desmentida; lo que falta es **el acceso**, y por eso entra como fila propia. / An
already-added root cannot be rescanned on request: only watching and the fallback timer look again.
The verified `LIB-002` criterion promises no control, so the row is not contradicted; what is missing
is **the access**, which is why it enters as a row of its own.

## 4 · Tres cadenas traducidas que no usa nadie / Three translated strings nobody uses

Las tres entradas del filtro de Cursos llegaron con el paquete de diseño —el documento «Cadenas
nuevas» las lista bajo «Menú del riel (3)»— y están en el árbol en los dos idiomas: / The three
Courses filter entries arrived with the design package and sit in the tree in both languages:

```
CoursesMenuAll            Todos los cursos        All courses
CoursesMenuThreadPending  Con hilo pendiente      Thread pending
CoursesMenuFinished       Terminados              Finished

consumidores fuera de Strings.es.axaml / Strings.en.axaml:   0
```

Registrado y nunca alimentado, en su forma de cadena. Se conservan porque `CRS-007` las va a usar; si
esa fila se retirara, se retiran con ella. / Registered and never fed, in string form. They are kept
because `CRS-007` will use them; if that row were dropped, they go with it.

## 5 · Por qué el menú se rechaza / Why the menu is rejected

Tres razones medidas, no de gusto: / Three measured reasons, not taste:

1. **El prototipo no lo dibuja** (§1), así que «el prototipo manda» no puede mandar aquí. / **The
   prototype does not draw it**, so "the prototype rules" cannot rule here.
2. **Duplicaría lo que ya está a la vista** en el destino donde más se trabajó —Biblioteca— y
   taparía esa misma fila de filtros al abrirse. / **It would duplicate what is already on screen**
   in the destination it was worked on most, and cover that very filter row when opening.
3. **El paseo autónomo no puede pulsar nada dentro de un desplegable**: el contenido de un `Flyout`
   aterriza en su propia raíz de popup, que es la razón por la que las entradas de
   `eng/walk-pending.txt` son hijas de flyout. Construirlo llevaría hasta diecinueve controles a un
   trinquete que **sólo puede encoger**. / **The autonomous walk cannot press anything inside a
   flyout**, which is why `eng/walk-pending.txt`'s entries are flyout children. Building it would
   add up to nineteen controls to a ratchet that **can only shrink**.

## 6 · Lo que la medición no cubre / What the measurement does not cover

- **La aplicación se fotografió sobre la biblioteca real de quien la usa**, con rutas y nombres de
  carpeta suyos. Esas capturas **no entran en el árbol**: la regla de que nada personal vive aquí
  tiene su propia puerta. Lo que queda escrito son los números. / **The application was captured over
  its owner's real library.** Those captures **do not enter the tree**; only the numbers stay.
- **Una de esas capturas mostró dos titulares superpuestos** al llegar a Cursos —el de la portada
  encima del de la vista—. No se persiguió aquí porque no es lo que se medía, y **no está dicho si es
  un defecto de la vista o un artefacto de fotografiar durante la transición**. Queda nombrado para
  que alguien lo mida. / **One of those captures showed two overlapping headings** on arriving at
  Courses. It was not chased here, and **it is not settled whether it is a defect or an artifact of
  photographing mid-transition**. It is named so somebody measures it.
