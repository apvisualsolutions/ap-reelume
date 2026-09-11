# La tarjeta de la Biblioteca lleva el ritmo y la forma del prototipo / The Library Card Carries the Prototype's Rhythm and Shape

- IDs: `PRD-006`
- Fecha / Date: 2026-09-12
- Alcance / Scope: `Presentation/Library` (`PosterCardView`, `UnavailableBadge`, `LibraryView`),
  `Presentation/Theme` (`DesignTokens`, `AppearanceService`), `UiTests/Library`
  (`PosterCardRhythmTests`, `PosterCardShapeTests`, `UnavailableBadgeTests`), `UiTests/Theme`
  (`SurfaceCornerTests`), `AccessibilityTests/ContrastTokenTests`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### La decisión

[La evidencia de la rejilla fluida](audit-fluid-library-grid.md) dejó registrado por escrito lo que
le quedaba a la Biblioteca: **el ritmo vertical de la tarjeta, que va junto, y cuatro diferencias de
forma**. El propietario eligió el 2026-09-12 terminar la Biblioteca antes de `PLY-016`, y esto es esa
tanda.

### Lo que dice el prototipo, medido y no leído

El prototipo se midió el mismo día con Chrome **sin cabeza** a 1500 y 1600 px y en las tres
densidades, con dos instrumentos —el DOM (`getBoundingClientRect`, `getComputedStyle`) y los píxeles
de una captura— y con `devicePixelRatio` forzado a 1 y una ventana de 2200 px de alto para que la
página no tuviera barra de desplazamiento. **Los dos instrumentos coinciden al píxel** salvo en lo
que se dice al final.

| Distancia | Prototipo | Depende de |
| --- | --- | --- |
| Barra de filtros → primera portada | **17** | nada: igual a los dos anchos y en las tres densidades |
| Portada → caja del título | **10** | nada (es el `row-gap` de la baldosa) |
| Título → metadatos (cajas) | **0**, cajas de 20,25 y 17,25 | nada |
| Metadatos → estado (cajas) | **3** (`margin-top`) | nada |
| Última línea → portada siguiente | **hueco de fila + 2** = 14 / 20 / 28 | la densidad |
| Paso de fila | **portada + 69,75 + hueco de fila** | la densidad y el ancho |

Y las cuatro marcas sobre la portada:

| Marca | Prototipo |
| --- | --- |
| Chip de tipo (`:2374`) | 21,75 de alto a **9 px** de los bordes exteriores, relleno `3px 9px 3px 7px`, hueco 5, radio 999, `rgba(9,12,16,.62)` con `backdrop-filter: blur(8px)`, **10,5 px peso 600** en blanco, icono de 12 |
| Marca de visto (`:313`) | **20 × 20 a 7 px** de los bordes exteriores superior y derecho, fondo del acento, check de 13 |
| Pista de progreso (`:312`) | **3 px** al pie, de borde a borde, pista `rgba(255,255,255,.25)`, relleno del acento, **sin radio propio** |
| Velo de «no disponible» (`:311`) | cubre la portada entera, `rgba(9,12,16,.55)`, relleno 8, texto blanco de **11 px peso 600** abajo a la izquierda, icono de aviso de 14 |

**El desenfoque del chip se midió por su efecto**, que es lo que se puede reproducir: la trama
diagonal del arte tiene una energía de alta frecuencia de **2,730** al lado del chip y **0,066**
dentro de él — un factor de **41,4×**—. Y la descomposición honesta: el tinte al 62 % por sí solo
dejaría 1,037, así que **el desenfoque se lleva el 93,6 % de lo que el tinte no se lleva** (15,7×).

**El orden de pintado es arte → chip → velo → pista → marca**, y con dos consecuencias medidas: el
chip **queda atenuado** bajo el velo (su texto blanco puro lee `rgb(120,122,124)`, que es el
compuesto previsto) y la pista **no**, porque va encima.

### Lo que dibujaba la aplicación

| Pieza | Aplicación | Prototipo |
| --- | --- | --- |
| Filtros → primera portada | 21 / 17 / 29 según densidad | 17 en las tres |
| Portada → título | 8 (el hueco de la densidad) | 10 |
| Entre las tres líneas | 8 px de `Space8` entre cada una | apiladas, con 3 px sólo en la tercera |
| Última línea → portada siguiente | 11 / 19 / 35 | 14 / 20 / 28 |
| Chip | 12 px peso normal, tinte `.72` plano, relleno `7,2` | 10,5 peso 600, `.62` desenfocado, `3px 9px 3px 7px` |
| Marca de visto | 22 a 9 px, check de 14, visible con el velo | 20 a 7 px, check de 13, ausente con el velo |
| Pista | `ControlFillBrush` opaca, extremos redondeados, 4 px de relleno a cada lado | blanco al 25 %, extremos rectos, de borde a borde |
| «No disponible» | píldora ámbar en la esquina | velo sobre toda la portada |

### La corrección

- **La tarjeta tiene dos formas y los rieles de Inicio no se tocan.** Lo nuevo entra por la clase
  `grid-tile`, que pone la Biblioteca donde la monta. El prototipo dibuja sus rieles distintos
  —hueco 8, título de 13, subtítulo de 11 y ninguna línea de estado—, así que los números de la
  baldosa serían los números equivocados allí.
- **Los valores por defecto pasaron de atributo a estilo**, porque un atributo es un valor local y
  gana a cualquier `Setter`. Ése fue el primer intento del velo: sus `Setter` se aplicaron, perdieron,
  y lo que se pintó fue la píldora estirada de lado a lado de la portada.
- **El redondeo de maquetación se apaga en el pie.** Avalonia redondea el tamaño deseado de cada
  control **hacia arriba**, así que cajas de 20,25 y 17,25 medían 21 y 18 y las tres líneas ocupaban
  60 px donde el prototipo las apila en 57,75. Con el redondeo apagado apilan exactamente igual, y el
  texto sigue cayendo en píxeles enteros porque Skia ajusta la línea base por su cuenta.
- **El relleno vertical de la tarjeta es fijo en 8, como la baldosa del prototipo**, y lo que varía
  con la densidad es el hueco: `DensityRowGap` (12/18/26, los números del prototipo) menos los 16 que
  las dos tarjetas ya gastan. A la densidad compacta eso es **−4**, y el prototipo también solapa ahí.
- **El velo es una forma de `UnavailableBadge`, no un aviso nuevo**: un solo control, dos formas, como
  el prototipo. La puerta que prohíbe un segundo aviso sigue en pie.
- **El desenfoque del chip se emula, y la vía sale de la fuente de Avalonia**, consultada ese día: no
  hay desenfoque de fondo dentro de la ventana —`BlurEffect` difumina la capa del propio elemento y
  el acrílico sólo tiñe, apoyado en la transparencia de la ventana—. Lo que sí hay es un pincel de
  otro visual: el chip pinta **un pincel del arte de su propia tarjeta**, con su propia caja recortada
  de él y **repetida en espejo hacia fuera**, que es el `edgeMode="mirror"` que la especificación de
  filtros exige para un desenfoque de fondo, y lo difumina con un radio que Skia convierte en el
  mismo sigma que piden los 8 px de CSS: `sigma = 0,288675 · 26 + 0,5 = 8,0055`.
- **El velo va a `.57` y no al `.55` del prototipo**, y es una cesión por contraste con su número: la
  palabra blanca de 11 px sobre una portada **blanca** —la peor que puede llegar— lee **4,33:1** con
  el valor del prototipo y **4,65:1** con éste, donde WCAG pide 4,5 para texto de ese tamaño. Los dos
  velos no se distinguen uno al lado del otro. El chip no necesita nada: su `.62` lee **5,55:1**.
- **La pista pierde el radio que le daba el tema base**, que es lo que el prototipo no dibuja: medido,
  un relleno del 42 % daba **57 px** de acento donde el prototipo dibuja **61**. Lo que redondea esa
  regla es la portada en la que vive.
- **El glifo del aviso pasa a ser una geometría.** Este árbol llevó veintisiete glifos de Segoe a
  dibujos de línea el 2026-08-24 y dejó este atrás, un pictograma sólido de otro alfabeto al lado de
  treinta y cinco trazados.

### Medido en píxeles, con la aplicación montada en memoria

La aplicación real no se abrió. La escena pinta sus propios colores y cada prueba afirma primero que
encontró lo que va a medir.

| Medida | 1500 | 1600 |
| --- | --- | --- |
| Portada → tinta del título | 15 | 15 |
| Tinta del título → tinta de metadatos | 6 | 5 |
| Tinta de metadatos → tinta del estado | 8 | 8 |
| Paso de fila | **321** | **309** |
| Filtros → portada | 17 | 17 |

Y las cuatro marcas, a 1600: chip de **21,75** de alto a **9 / 9** de los bordes, con el tinte al
**.62** compuesto sobre el arte y la energía de alta frecuencia bajo él **por debajo de un quinto**
de lo que el tinte solo dejaría; marca de visto de **20** de diámetro a **7 / 7**, ausente cuando el
medio no está disponible; pista de **3** filas al pie, blanco al **25 %** sobre el arte de esa misma
fila, de borde a borde y con el relleno acabando en el 42 % de la regla; velo sobre **toda** la
portada al alfa del token, con la palabra blanca a **9** px del borde izquierdo y del inferior, el
chip atenuado debajo y la pista **por encima**.

Las tres densidades dan **17** de los filtros a la portada y un paso de fila de
`portada + 69,75 + hueco`, que es la regla del prototipo.

### Las puertas, y la rotura que caza cada una

| Mutación | Lo que la cazó |
| --- | --- |
| La marca de visto a 22, o su margen a 8 | El diámetro y las dos distancias, medidos por área y centroide |
| La marca visible con el velo | La tarjeta no disponible, con la disponible como control |
| La pista opaca otra vez (`ControlFillBrush`) | La composición al 25 % contra la misma tarjeta sin progreso |
| El radio del tema base en la pista | El borde del relleno, a 57 px en vez de 61 |
| El tinte del chip a `.72`, o su letra a 12 | El compuesto medio y el alto de 21,75 |
| El chip sin desenfoque | La energía de alta frecuencia bajo el chip, con el arte de al lado como control |
| El velo a `.55` | El control negativo de la prueba de contraste |
| El velo debajo del chip, o la pista debajo del velo | Las dos composiciones, que se distinguen en 34 niveles |
| El hueco de la portada al título a 8 | La caja del título y la tinta, a 15 |
| `Space8` entre las tres líneas | Las tres cajas y las dos distancias de tinta |
| El redondeo de maquetación encendido en el pie | El paso de fila, 311 en vez de 309 |
| Los filtros a 12 de margen | Los 17 px, en las tres densidades |
| El relleno vertical siguiendo la densidad | Los 17 px en compacta y en amplia |
| Reescribir el relleno o el margen sin que cambien | El contador de avisos de `LibraryGridTests` |
| El glifo del aviso a otra geometría | La instancia del recurso, no su `ToString` |
| Borrar el hueco de fila por densidad, o cambiar sus números | Los tres, leídos del prototipo en `AppearanceServiceTests` |
| El check de la marca a 14, o sin su clase | La tinta del glifo por área: 6,89 px a 13, 7,78 a 14 |
| Quitar `.grid-tile` de un selector de la baldosa | La tarjeta montada **sin** la clase, con los tres números del riel |
| La palabra del chip a `TextPrimaryBrush` | El color efectivo de la palabra |
| El fondo desenfocado 24 px fuera de su caja | Los dos rectángulos del pincel contra la caja del chip |

**Las seis últimas las propuso la auditoría de puertas del mismo día** (agente `gate-auditor`, en
copia aislada, con cada mutación aplicada, medida y deshecha), y cinco tenían la forma de siempre —una
comprobación que no podía fallar—:

- **La puerta del glifo comparaba dos geometrías como texto**, y `StreamGeometry` no sobrescribe
  `ToString`: los dos lados decían `Avalonia.Media.StreamGeometry`, así que el aviso podía dibujar la
  estrella, el marcador o el reloj. Ahora compara la instancia que entrega el recurso.
- **El hueco de fila por densidad no lo medía nada.** Las tres pruebas de ritmo lo inyectan en los
  recursos de su ventana, así que **borrar el bloque entero** de `AppearanceService` dejaba la suite
  verde. Ahora `AppearanceServiceTests` lo afirma leyendo los tres números **del propio prototipo**,
  con el patrón anclado a su expresión: sin anclar leía 3, 13 y 6 de otros tres atajos del archivo.
- **La marca de visto medía el disco y no el check.** La escena pintaba el glifo del mismo verde que
  el disco para que no abriera un agujero en el área, y con eso el tamaño del check era invisible:
  volver al 14 del que venía pasaba. Ahora el check se pinta en un tercer color y su tinta se mide
  por **área**, que va con el cuadrado del tamaño.
- **Sólo una dirección de las dos formas estaba vigilada.** Quitar `Classes="grid-tile"` de la vista
  rompe las cinco pruebas de ritmo, pero quitar `.grid-tile` de un **selector** dejaba la suite
  verde, y los rieles habrían adoptado en silencio el título de 13,5 y las líneas apiladas. Ahora
  hay una prueba que monta la tarjeta **sin** la clase y afirma los tres números del riel.
- **La tinta del chip no estaba atada al pincel que el chip usa**: la teoría de contraste leía dos
  veces el diccionario, así que pasar la palabra a `TextPrimaryBrush` —casi negro sobre un tinte
  oscuro— pasaba las 1333 de `UiTests` y las 150 de `AccessibilityTests`. Ahora se lee el color
  efectivo de la palabra.
- **El sitio del fondo desenfocado no se medía**: un desenfoque puesto 24 px fuera de su caja es
  igual de plano y, sobre la trama sintética, tiene la misma media. Sobre una carátula real el chip
  enseñaría el borrón de otra parte del cuadro, que es lo único que un desenfoque de fondo existe
  para evitar.

**Y dejó dos avisos que no se corrigen aquí, con su medición:** el velo a `.55` **no** lo ve la
composición de píxeles —±2 por canal—, así que su valor descansa entero en el control negativo de la
prueba de contraste, que sí aguanta; y la suite `UiTests` **enrojece al azar** —cinco rojos en cuatro
pruebas preexistentes sobre quince ejecuciones completas, ninguna de esta tanda y ninguna repetible—,
registrado como tarea de fondo con el mecanismo plausible: las pruebas corren en paralelo dentro del
ensamblado y varias escriben el diccionario de recursos que todas comparten.

### Trampas medidas hoy

- **La captura del arnés viene en `Rgba8888`, no en BGRA.** Un lector que suponga BGRA **intercambia
  rojo y azul**, y todo lo que este árbol había medido en píxeles era gris, blanco o verde, que
  sobreviven al intercambio. El velo, que es `9,12,16`, no: leía un alfa de **0,488** donde pinta
  **0,569**, y así se encontró. El lector pregunta el formato.
- **Avalonia redondea hacia arriba el tamaño deseado de cada control**, así que una caja de línea
  fraccionaria engorda un píxel entero.
- **Avalonia reparte el sobrante de `LineHeight` como CSS**, mitad arriba y mitad abajo: medido con
  +2,29 de sobrante la tinta baja 1 px, y con +6,04 baja 3.
- **Un atributo gana a un `Setter`**, y un estilo que pierde no deja rastro: se aplica, no se ve.
- **`Space8` es un número y un `Padding` es un grosor**: un `Setter` de `Padding` a `{DynamicResource
  Space8}` revienta al cargar la vista con un `InvalidCastException`.
- **La misma palabra no tinta igual en dos tipografías.** La métrica vertical de `Segoe UI` y de
  `Segoe UI Variable Text` es **idéntica** —las dos miden 17,96 de alto con la línea base en 14,57 a
  13,5 px—, pero «Vidrio Templado» tinta **13 filas aquí y 15 allí**. Las cajas se comparan exactas;
  la tinta, con dos píxeles de holgura y el porqué escrito.
- **La caja del color exacto de un disco suavizado miente**: un círculo de 20 px lee 18. Se mide por
  área y centroide, que no dependen de ningún umbral.
- **Los extremos de una regla y la esquina de la portada se comen píxeles**: la pista se lee lejos de
  los extremos, y su extensión se compara con el arte de su propia fila, que pierde los mismos.
- **El azul del texto secundario es 117**, así que un umbral de 110 **no lo ve**: el barrido encontró
  una portada, un título y nada más. Con 200 aparecen las tres líneas.
- **Una línea de texto fina puede tener un hueco de dos filas** donde el trazo suavizado queda por
  encima del umbral; las bandas separadas por menos de tres filas son una sola línea.

### Lo que queda, con nombre

- **La familia tipográfica**: el prototipo pide `Segoe UI Variable Text` y esta aplicación dibuja
  `Segoe UI`. Medido el mismo día: el tema Fluent declara una familia compuesta `Inter, $Default` y
  **Inter no está referenciada como paquete**, así que resuelve al predeterminado del sistema —aunque
  Inter esté instalada en la máquina, que lo está—. Dos sitios del árbol dicen «Inter a 14 px» y son
  falsos. Cambiarla afecta a todo el texto de la aplicación: tanda propia.
- **La mitad horizontal de la densidad**: huecos de columna de 10/16/22 contra 8/16/32, que cambian la
  cuenta de columnas.
- **Los filtros miden 36 de alto donde el prototipo pone 32**, y la fila de avisos ocupa 12 px aunque
  esté vacía, con un comentario que dice 0.
Y uno que se registró y **resultó no serlo**, medido el mismo día: **el hover de la tarjeta en alto
contraste**. Leyendo la plantilla Fluent parecía que el título se volvía blanco sobre una página
blanca, porque su pincel pasa a `ControlTextActiveBrush` y el botón deja su `Background` en
transparente. En píxeles no ocurre: la plantilla pinta el fondo en el **presenter**, no en el botón,
así que en alto contraste claro el hover da una placa negra con la palabra en blanco —fila del
título: el más oscuro pasa de 18 a 0 y el más claro sigue en 255— y en oscuro la inversión
contraria. Sigue siendo legible; la deducción era falsa y la medición la descarta.

---

## English

### The decision

[The fluid grid's evidence](audit-fluid-library-grid.md) registered in writing what the Library had
left: **the card's vertical rhythm, which goes together, and four differences of shape**. On
2026-09-12 the owner chose to finish the Library before `PLY-016`, and this is that batch.

### What the prototype says, measured rather than read

The prototype was measured the same day with **headless** Chrome at 1500 and 1600 px and at all three
densities, with two instruments — the DOM (`getBoundingClientRect`, `getComputedStyle`) and the pixels
of a capture — with `devicePixelRatio` forced to 1 and a 2200 px tall window so the page carried no
scrollbar. **The two instruments agree to the pixel** except where the end of this says otherwise.

| Distance | Prototype | Depends on |
| --- | --- | --- |
| Filter row → first cover | **17** | nothing: the same at both widths and all three densities |
| Cover → the title's line box | **10** | nothing (it is the tile's own `row-gap`) |
| Title → meta (boxes) | **0**, boxes of 20.25 and 17.25 | nothing |
| Meta → status (boxes) | **3** (`margin-top`) | nothing |
| Last line → next cover | **row gap + 2** = 14 / 20 / 28 | the density |
| Row step | **cover + 69.75 + row gap** | the density and the width |

And the four marks over a cover:

| Mark | Prototype |
| --- | --- |
| Kind chip (`:2374`) | 21.75 tall at **9 px** from the outer edges, `3px 9px 3px 7px` of padding, a gap of 5, radius 999, `rgba(9,12,16,.62)` with `backdrop-filter: blur(8px)`, **10.5 px at 600** in white, a 12 px icon |
| Watched tick (`:313`) | **20 × 20 at 7 px** from the outer top and right edges, the accent behind it, a 13 px check |
| Progress track (`:312`) | **3 px** at the foot, edge to edge, `rgba(255,255,255,.25)` for the track, the accent for the fill, **no radius of its own** |
| Unavailable veil (`:311`) | covers the whole cover, `rgba(9,12,16,.55)`, 8 of padding, a white **11 px at 600** word at the bottom left, a 14 px warning icon |

**The chip's blur was measured by its effect**, which is what can be reproduced: the artwork's
diagonal hatch carries **2.730** of high-frequency energy beside the chip and **0.066** inside it — a
factor of **41.4×**. And the honest decomposition: the 62 % tint alone would leave 1.037, so **the
blur takes 93.6 % of what the tint does not** (15.7×).

**The painting order is artwork → chip → veil → track → tick**, with two measured consequences: the
chip **is dimmed** under the veil — its pure white word reads `rgb(120,122,124)`, the composite the
arithmetic predicts — and the track is not, because it goes over.

### What the application drew

| Piece | Application | Prototype |
| --- | --- | --- |
| Filters → first cover | 21 / 17 / 29 by density | 17 at all three |
| Cover → title | 8 (the density's gutter) | 10 |
| Between the three lines | 8 px of `Space8` between each | stacked, with 3 px on the third alone |
| Last line → next cover | 11 / 19 / 35 | 14 / 20 / 28 |
| Chip | 12 px at a normal weight, a flat `.72` tint, `7,2` of padding | 10.5 at 600, a blurred `.62`, `3px 9px 3px 7px` |
| Watched tick | 22 at 9 px, a 14 px check, shown under the veil | 20 at 7 px, a 13 px check, absent under it |
| Track | an opaque `ControlFillBrush`, rounded ends, 4 px inset at each end | white at 25 %, square ends, edge to edge |
| «No disponible» | an amber pill in the corner | a veil over the whole cover |

### The fix

- **The card has two forms and Home's rails are not touched.** What is new arrives through the
  `grid-tile` class, which the Library puts on it where it mounts it. The prototype draws its rails
  differently — a gap of 8, a title at 13, a subtitle at 11 and no status line — so the tile's
  numbers would be the wrong numbers there.
- **The defaults moved from attributes into styles**, because an attribute is a local value and beats
  every setter. That was the veil's first run: its setters applied, lost, and what was painted was
  the pill stretched from one side of the cover to the other.
- **Layout rounding goes off on the caption.** Avalonia rounds every control's desired size **up**, so
  line boxes of 20.25 and 17.25 measured 21 and 18 and the three lines took 60 px where the prototype
  stacks them in 57.75. With the rounding off they stack exactly as the browser stacks them, and the
  text still lands on whole pixels because Skia snaps a baseline on its own.
- **The card's vertical padding is a fixed 8, like the prototype's tile**, and what the density moves
  is the gap: `DensityRowGap` (12/18/26, the prototype's own numbers) less the 16 the two cards have
  already spent. At the compact density that is **−4**, and the prototype overlaps there too.
- **The veil is a form of `UnavailableBadge`, not a new notice**: one control, two forms, as in the
  prototype. The gate that forbids a second notice still stands.
- **The chip's blur is emulated, and the way was read in Avalonia's source** that day: there is no
  backdrop blur inside a window — `BlurEffect` blurs the element's own layer and the acrylic only
  tints, leaning on the window's transparency. What there is, is a brush of another visual: the chip
  paints **a brush of its own card's artwork**, with its own box taken out of it and **mirrored
  outwards**, which is the `edgeMode="mirror"` the filter specification requires of a backdrop blur,
  blurred at a radius Skia turns into the same sigma CSS's 8 px asks for:
  `sigma = 0.288675 · 26 + 0.5 = 8.0055`.
- **The veil is `.57` rather than the prototype's `.55`**, and it is a contrast trade with its number:
  over a **white** cover — the worst that can arrive — the white 11 px word reads **4.33:1** with the
  prototype's value and **4.65:1** with this one, where WCAG asks 4.5 of text that small. The two
  veils cannot be told apart side by side. The chip needs nothing: its `.62` reads **5.55:1**.
- **The track loses the radius the base theme gave it**, which is what the prototype does not draw:
  measured, a 42 % fill drew **57 px** of accent where the prototype draws **61**. What rounds that
  rule is the cover it lives in.
- **The notice's glyph becomes a geometry.** This tree carried twenty-seven Segoe glyphs into line
  drawings on 2026-08-24 and left this one behind, a solid pictogram from another alphabet beside
  thirty-five stroked ones.

### Measured in pixels, with the application mounted in memory

The real application was not opened. The scene paints its own colours and every test states that it
found what it is about to measure first.

| Measure | 1500 | 1600 |
| --- | --- | --- |
| Cover → the title's ink | 15 | 15 |
| The title's ink → the meta's ink | 6 | 5 |
| The meta's ink → the status's ink | 8 | 8 |
| Row step | **321** | **309** |
| Filters → cover | 17 | 17 |

And the four marks, at 1600: a chip **21.75** tall at **9 / 9** from the edges, with its **.62** tint
composited over the artwork and the high-frequency energy under it **below a fifth** of what the tint
alone would leave; a watched tick **20** across at **7 / 7**, absent when the medium is out of reach;
a **3** row track at the foot, white at **25 %** over the artwork of that same row, edge to edge and
with its fill ending at 42 % of the rule; a veil over the **whole** cover at the token's alpha, with
the white word **9** px from the left and bottom edges, the chip dimmed underneath and the track
**over** it.

All three densities give **17** from the filters to the cover and a row step of
`cover + 69.75 + gap`, which is the prototype's rule.

### The gates, and the break each one catches

| Mutation | What caught it |
| --- | --- |
| The tick back to 22, or its margin to 8 | The diameter and the two distances, measured by area and centroid |
| The tick shown under the veil | The unavailable card, with the reachable one as the control |
| The track opaque again (`ControlFillBrush`) | The 25 % composition against the same card without progress |
| The base theme's radius on the track | The fill's far edge, at 57 px instead of 61 |
| The chip's tint back to `.72`, or its word to 12 | The mean composite and the height of 21.75 |
| The chip with no blur | The high-frequency energy under it, with the artwork beside it as the control |
| The veil at `.55` | The contrast test's negative control |
| The veil under the chip, or the track under the veil | The two compositions, which differ by 34 levels |
| The cover-to-title gap back to 8 | The title's box and its ink, at 15 |
| `Space8` between the three lines | The three boxes and the two ink distances |
| Layout rounding back on in the caption | The row step, 311 instead of 309 |
| The filters back to a margin of 12 | The 17 px, at all three densities |
| The vertical padding following the density | The 17 px at the compact and roomy densities |
| Rewriting the padding or the margin when neither changed | `LibraryGridTests`' notification counter |
| The notice's glyph pointed at another geometry | The resource's instance, not its `ToString` |
| Deleting the density's row gap, or changing its numbers | All three, read from the prototype in `AppearanceServiceTests` |
| The tick's check back to 14, or with no class at all | The glyph's ink by area: 6.89 px at 13, 7.78 at 14 |
| Taking `.grid-tile` off one of the tile's selectors | The card mounted **without** the class, with the rail's three numbers |
| The chip's word pointed at `TextPrimaryBrush` | The word's effective colour |
| The blurred backdrop put 24 px off its own box | The brush's two rects against the chip's box |

**The last six were proposed by the same day's gate audit** (the `gate-auditor` agent, in an isolated
copy, with every mutation applied, measured and undone), and five had the usual shape — a check that
could not fail:

- **The glyph gate compared two geometries as text**, and `StreamGeometry` does not override
  `ToString`: both sides said `Avalonia.Media.StreamGeometry`, so the notice could have drawn the
  star, the marker or the clock. It now compares the instance the resource hands over.
- **Nothing measured the density's row gap.** The three rhythm tests inject it into their own
  window's resources, so **deleting the whole block** from `AppearanceService` left the suite green.
  `AppearanceServiceTests` now asserts it by reading the three numbers **out of the prototype**, with
  the pattern anchored on its expression: unanchored it read 3, 13 and 6 from three other shorthands.
- **The tick measured the disc and not the check.** The scene painted the glyph in the same green as
  the disc so it would not punch a hole in the area, and that left the check's size invisible: going
  back to the 14 it came from passed. The check is now painted in a third colour and its ink measured
  by **area**, which goes as the square of the size.
- **Only one direction of the two forms was guarded.** Taking `Classes="grid-tile"` off the view
  breaks all five rhythm tests, but taking `.grid-tile` off a **selector** left the suite green, and
  the rails would have taken the 13.5 px title and the stacked lines in silence. There is now a test
  that mounts the card **without** the class and asserts the rail's three numbers.
- **The chip's ink was not tied to the brush the chip uses**: the contrast theory read the dictionary
  twice, so moving the word to `TextPrimaryBrush` — near black on a dark tint — passed `UiTests`'
  1333 and `AccessibilityTests`' 150. The word's effective colour is now read.
- **The blurred backdrop's place was not measured**: a blur put 24 px off its own box is just as flat
  and, over the synthetic hatch, carries the same mean. Over a real cover the chip would show the
  smear of another part of the picture, which is the one thing a backdrop blur exists to avoid.

**And it left two warnings that are not fixed here, with their measurements:** the veil at `.55` is
**not** visible to the pixel composition — ±2 per channel — so its value rests entirely on the
contrast test's negative control, which does hold; and the `UiTests` suite **reddens at random** —
five reds across four pre-existing tests over fifteen full runs, none of them this batch's and none
repeatable — registered as a background task with the plausible mechanism: tests run in parallel
inside the assembly and several of them write the resource dictionary they all share.

### Traps measured today

- **The harness's capture arrives as `Rgba8888`, not BGRA.** A reader that assumes BGRA **swaps red
  and blue**, and everything this tree had measured in pixels was grey, white or green, which survive
  the swap. The veil, at `9,12,16`, does not: it read an alpha of **0.488** where it paints **0.569**,
  and that is how it was found. The reader asks for the format.
- **Avalonia rounds every control's desired size up**, so a fractional line box grows a whole pixel.
- **Avalonia distributes the leading `LineHeight` adds the way CSS does**, half above and half below:
  measured, 2.29 of extra leading moves the ink down 1 px and 6.04 moves it 3.
- **An attribute beats a setter**, and a style that loses leaves no trace: it applies and is not seen.
- **`Space8` is a number and a `Padding` is a thickness**: a `Padding` setter pointed at
  `{DynamicResource Space8}` throws an `InvalidCastException` as the view loads.
- **The same word does not ink the same in two typefaces.** `Segoe UI` and `Segoe UI Variable Text`
  have **identical** vertical metrics — both measure 17.96 tall with a baseline at 14.57 at 13.5 px —
  but «Vidrio Templado» inks **13 rows here and 15 there**. The boxes are compared exactly; the ink,
  with two pixels of slack and the reason written down.
- **The exact-colour box of an antialiased disc lies**: a 20 px circle reads 18. It is measured by
  area and centroid, which need no threshold at all.
- **A rule's ends and the cover's corner eat pixels**: the track is read away from the ends, and its
  span is compared against the artwork on its own row, which loses the same ones.
- **The secondary text's blue channel is 117**, so a threshold of 110 **cannot see it**: the scan
  found a cover, a title and nothing else. At 200 the three lines appear.
- **A thin line of text can carry a two row hole** where an antialiased stroke lands above the
  threshold; bands less than three rows apart are one line.

### What is left, by name

- **The typeface family**: the prototype asks for `Segoe UI Variable Text` and this application draws
  `Segoe UI`. Measured the same day: the Fluent theme declares a composite family `Inter, $Default`
  and **Inter is not referenced as a package**, so it resolves to the system's default — even though
  Inter is installed on this machine, and it is. Two places in the tree say «Inter at 14 px» and both
  are false. Changing it reaches every word the application draws: a batch of its own.
- **The horizontal half of the density**: column gaps of 10/16/22 against 8/16/32, which change the
  column count.
- **The filters are 36 tall where the prototype puts 32**, and the notices row takes 12 px even when
  it is empty, with a comment that says 0.
And one that was registered and **turned out not to be one**, measured the same day: **the card's
hover in high contrast**. Reading the Fluent template it looked as though the title went white on a
white page, because its brush becomes `ControlTextActiveBrush` and the button leaves its own
`Background` transparent. In pixels it does not happen: the template paints the background on the
**presenter** and not on the button, so in high contrast light the hover gives a black plate with the
word in white — on the title's row the darkest pixel goes from 18 to 0 and the lightest stays at 255
— and in dark the opposite inversion. It stays legible; the deduction was false and the measurement
discards it.
