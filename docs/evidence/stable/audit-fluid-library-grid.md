# La rejilla de la Biblioteca se estira como la del prototipo / The Library Grid Stretches the Way the Prototype's Does

- IDs: `PRD-006`
- Fecha / Date: 2026-09-11
- Alcance / Scope: `Presentation/Library` (`LibraryView`, `PosterRowPanel`, `PosterCardView`),
  `Presentation/Theme/DesignTokens`, `UiTests/Library/LibraryGridTests`,
  `UiTests/Library/PosterRowPanelTests`, `UiTests/Shell/PageMarginTests`,
  `UiTests/Home/HomeLayoutTests`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### La decisión

[La evidencia del margen](audit-page-margin.md) dejó registrada la rejilla fluida y su sitio en el
orden de paridad para el propietario. **El propietario decidió el 2026-09-11 ponerla la primera**,
antes de «Biblioteca y escaneo» de Ajustes, porque era la diferencia más visible que le quedaba a la
Biblioteca en el ancho canónico.

Esto deroga, por escrito, lo que decía la «Propuesta de diseño»: traducía `auto-fill` a «`WrapPanel`
e `ItemWidth` fijo» y su §4 pedía un mínimo de 180. El árbol ya había seguido al código del
prototipo en el 148; ahora lo sigue también en el estirado.

### Lo que dice el prototipo, en su código

- `design/AP Reelume.dc.html:3549`: `repeat(auto-fill, minmax(COVER px, 1fr))`, con COVER el tamaño
  de portada elegido (148 por defecto) y `gap: 18px 16px` en la densidad cómoda.
- La baldosa (`:307`) es `border:1px solid transparent; padding:8px; margin:-8px`, y hay
  `*{box-sizing:border-box}` (`:12`): el 148 es la baldosa **con su borde dentro**, la portada mide
  la pista menos 2, y su alto es 1,5 veces su ancho (`aspect-ratio: 2/3` sobre la caja con borde).
- En la aplicación la rejilla ocupa el contenido más un margen de tarjeta a cada lado, así que la
  regla queda **N = ⌊S / (COVER + 2 × 8)⌋**: una columna cuesta 164, no los 166 que medía una
  tarjeta fija. La portada es la celda menos 18.

### Lo que dibujaba la aplicación

Tarjetas fijas de 148 con su borde por fuera, y la cuenta dividiendo por 166:

| Ancho de ventana | Prototipo | Aplicación |
| --- | --- | --- |
| 1600 | 9 portadas de ~147 que llenan la fila | 8 de 148 y **~160 px vacíos** |
| 1500 | 8 portadas de ~155,5 que llenan la fila | 8 de 148 y ~60 px vacíos |

### La corrección

- **La cuenta divide por 164.** El borde sale de la cuenta, porque el mínimo del prototipo ya lo
  contiene, y entra en el ancho de la portada.
- **`PosterRowPanel`, un panel nuevo para cada fila**, que reparte su ancho en N celdas iguales con
  cada borde ajustado al píxel del dispositivo, redondeando la mitad lejos de cero como hace el
  navegador. La fila incompleta conserva las celdas de la llena, como `auto-fill`.
- **`UniformGrid` se descartó por su fuente, no por intuición**: en Avalonia 12.1.1 redondea el ancho
  de celda una vez y lo multiplica por el índice, así que a 1600 la fila acabaría en 1485 y a 1500 en
  1392, cuatro píxeles fuera de una superficie de 1388; y sin número de columnas pinta un cuadrado.
- **La tarjeta llena su celda sin tocar `PosterCardView`**: en la superficie de la rejilla,
  `PosterCardWidth` pasa a automático y `PosterCardHeight` al alto calculado, y el botón de la
  tarjeta se estira en la plantilla de la Biblioteca, no en el estilo que comparte con Inicio. El
  chip, la marca de visto y los textos conservan su tamaño, como en el prototipo: por eso el alto lo
  calcula la rejilla y no un `Viewbox`, que los escalaría con la portada.
- **El número de columnas llega a cada fila por herencia** desde la superficie. Enlazarlo desde
  dentro de la fila con `$parent[ItemsControl]` habría encontrado el `ItemsControl` de la propia fila.
- **La cuenta sigue a los ajustes de Apariencia.** Con tarjetas fijas, un tamaño de portada nuevo
  cambiaba todas las tarjetas y el redimensionado que venía después volvía a contar. Con tarjetas
  fluidas nada cambia de tamaño por sí solo, así que la cuenta se quedaba en 9 donde caben 7: medido
  en rojo antes de corregirlo, con la portada a 176 y con la densidad amplia. Ahora dos propiedades de
  la vista, enlazadas a esos dos valores, vuelven a contar cuando cambian, y sólo cuando cambian.
- **El alto sólo se escribe cuando cambia**: un recurso reescrito avisa a todas las tarjetas aunque
  su valor sea el mismo, y en la aplicación la rejilla cambia de alto con cada página de títulos.

### Medido en píxeles, con la aplicación montada en memoria

La aplicación real no se abrió: el arnés en memoria dibuja con Skia de verdad. La escena pinta sus
propios colores —portadas negras con el hilo en negro, sin arte ni sombra—, así que los bordes son
los exteriores de la portada.

| Ancho | Portadas | Del riel a la primera | De la última al borde | Anchos | Alto |
| --- | --- | --- | --- | --- | --- |
| 1500 | 8 | 33 | 33 | 156 y 155 alternos | 233 |
| 1600 | 9 | 33 | 33 | 147 y 148, una de 148 cada tres | 221 |

**33 son los 32 del margen de página más el borde de 1 px de la baldosa.** La evidencia del margen
midió 34 en el prototipo porque su hilo es claro y el umbral con que se contó lo recorta; lo que se
compara es la simetría, y ahora es la misma a los dos lados.

**Y el prototipo, medido el mismo día con Chrome sin cabeza a los mismos anchos** (agente
`prototype-fidelity`, contando el hilo, con el DOM como segundo instrumento), da **exactamente lo
mismo**: 8 y 9 portadas, 33 y 33, los anchos en la misma secuencia —156, 155… a 1500; 147, 148, 147,
147, 148… a 1600—, altos de 233 y 221, y 18 px de hilo a hilo entre portadas. Rehecha a mano, la
aritmética de `PosterRowPanel.CellEdge` y `LibraryView.CoverHeight` predice sus 16 y 18 bordes.
**Una condición de medida**: si la página del prototipo se desplaza, su barra de 12 px le quita ese
ancho a la rejilla —a 1600×700, portadas de 146 y 45 px al borde—, y la de la aplicación no lo
reserva; se compara siempre sin desplazamiento.

Y a escalas de 125, 150 y 175 %: todos los bordes caen en píxeles enteros del dispositivo, del 0 al
borde. Un barrido de 1,17 millones de celdas —3000 anchos, de 1 a 12 columnas, cinco escalas—
comprueba que ninguna crece al pasar por el redondeo hacia arriba que Avalonia aplica al colocar
cada tarjeta.

### Las puertas, y la rotura que caza cada una

Cada prueba nueva se vio fallar, antes del código o con una mutación, y cada tanda de mutaciones
puso en rojo exactamente las pruebas previstas:

| Mutación | Lo que la cazó |
| --- | --- |
| Volver a dividir por 166 | 12 pruebas, entre ellas la cuenta en el borde de cada columna |
| Un solo ancho de celda redondeado, como `UniformGrid` | 16, entre ellas la aplicación montada a 1500 y 1600 |
| Bordes redondeados al par | Sólo la de los bordes literales a 1500, que existe por eso |
| Sin la guarda de ancho infinito | La fila sin ancho que repartir |
| Sin apretar las tarjetas sobrantes | La fila con más tarjetas que columnas |
| Alto redondeado al par, o sin tope en cero | Los casos 1485/9 y 0 del alto |
| Sin fijar el borde de la tarjeta con el foco | La portada con el foco del teclado |
| Sin heredar el número de columnas | La fila incompleta |
| Reescribir el alto aunque no cambie | El contador de avisos |
| Sin seguir a Apariencia | Las dos pruebas de portada y densidad |
| El ancho automático en los recursos compartidos | La tarjeta de Inicio, de 148 a 94 |
| Tarjetas fijas otra vez | La aplicación montada: 37 px a la izquierda a 1500, 1 px dentro del margen a 1600 |
| El panel coloca con el redondeo de Avalonia | Las posiciones literales a 1500 y la secuencia de anchos en píxeles |
| Bordes con techo en vez de mitad lejos de cero | Las posiciones literales a 1600 y la secuencia de anchos |
| El alto con un «menos 18» fijo | Las densidades amplia y compacta, y un borde de 2 |
| El alto escrito en los recursos de toda la aplicación | Inicio después de contar la Biblioteca, y la portada mayor |
| Reescribir el alto al cambiar la cuenta y no el alto | El paso de 1488 a 1476, que conserva nueve columnas |
| Reescribir el alto sólo al cambiar el ancho | Las dos pruebas de Apariencia |
| La escala leída como 1 | 277 píxeles a 125 % donde van 276 |

**Las siete últimas las propuso la auditoría de puertas del mismo día** (agente `gate-auditor`), y
las siete tenían la misma forma: la regla se comprobaba en la función de cálculo o con los ajustes
por defecto, y ninguna prueba leía lo que el panel dibujaba ni lo que cambiaba después. Se corrigió
cada prueba y cada una se vio fallar con su mutación. La auditoría también pasó al hilo de Avalonia
las pruebas de cálculo, porque ahora tocan clases que registran propiedades.

### Trampas medidas hoy

- **La aplicación montada sin modelo deja visibles todos los destinos a la vez.** Para medir el
  layout da igual; para contar píxeles es fatal: la primera captura encontró dos portadas. La medición
  en píxeles monta la aplicación con modelo y navega a la Biblioteca.
- **Una columna a 4 px del borde cae en la esquina de 10**: leyó 229 donde hay 233.
- **`LayoutHelper.RoundLayoutValue` redondea la mitad al par** y el navegador lejos de cero; sobre
  ocho pistas de 173,5 discrepan en dos bordes.
- **La prueba del foco medía el botón**, que ahora siempre llena su celda: se habría quedado ciega.
  Mide la portada.

### Lo que queda, con nombre

- **El ritmo vertical de la tarjeta, que va junto**: de la última línea a la portada siguiente 18
  contra 20, de la portada al título 8 contra 10, y —nuevo— las tres líneas bajo la portada van
  separadas 8 px entre sí donde el prototipo las apila (tinta de línea a línea 26 y 24 contra 20 y
  20). Hoy cada fila sale unos 5 px más alta; corregir sólo los dos primeros la dejaría 9 px más
  alta, así que se corrigen los tres a la vez. Y de los filtros a la primera portada, 21 contra 17
  según el código.
- **La forma de la tarjeta, cuatro diferencias visibles**: la pista del progreso es un gris casi
  blanco opaco donde el prototipo pone blanco al 25 % sobre el arte; «no disponible» es una píldora
  ámbar donde el prototipo vela la portada entera y escribe en blanco; el chip de tipo lleva 12 px de
  peso normal donde el prototipo pone 10,5 seminegrita sobre un fondo desenfocado; y la marca de visto
  mide 22 a 9 px del borde donde el prototipo pone 20 a 7.
- **La densidad**: sólo la cómoda coincide con el prototipo. Él deja la baldosa en 8 y cambia el hueco
  (10/16/22 entre columnas, 12/18/26 entre filas); la aplicación usa 4/8/16 como relleno y medio
  hueco.
- **Inicio**: el prototipo pinta sus dos rieles como rejilla fluida de mínimo 132 y hueco 14; la
  aplicación, rieles horizontales de 148 fijos.
- **El esqueleto de la primera carga**: seis cajas fijas de 158×237 contra ocho celdas de la misma
  rejilla con dos barras de texto. Con `PosterRowPanel` pasa a ser barato.
- **La decodificación de portadas a 148**, con portadas que llegan a unos 187 en la ventana más
  estrecha: una portada propia se ve algo más blanda. Seguir el ancho fluido volvería a decodificar en
  cada redimensionado. Y `CachedPosterConverter` declara dos constantes que nadie lee.
- **Redimensionar con la Biblioteca desplazada mueve lo que se ve**: el navegador ancla la posición y
  `VirtualizingStackPanel` no.

---

## English

### The decision

[The page-margin evidence](audit-page-margin.md) registered the fluid grid and left its place in the
parity order to the owner. **On 2026-09-11 the owner put it first**, ahead of Settings' «Library and
scanning», because it was the most visible difference the Library had left at the canonical width.

This overrides, in writing, what the design proposal said: it translated `auto-fill` into «a
`WrapPanel` with a fixed `ItemWidth`», and its §4 asked for a 180 minimum. The tree had already
followed the prototype's code on the 148; it now follows it on the stretch too.

### What the prototype says, in its code

- `design/AP Reelume.dc.html:3549`: `repeat(auto-fill, minmax(COVER px, 1fr))`, COVER being the
  chosen cover size (148 by default), and `gap: 18px 16px` at the comfortable density.
- The tile (`:307`) is `border:1px solid transparent; padding:8px; margin:-8px`, and there is
  `*{box-sizing:border-box}` (`:12`): the 148 is the tile **with its border inside**, the cover is
  the track less 2, and its height is one and a half times its width (`aspect-ratio: 2/3` on the
  bordered box).
- In the application the grid spans the content plus one card gutter on each side, so the rule
  becomes **N = ⌊S / (COVER + 2 × 8)⌋**: a column costs 164, not the 166 a fixed card measured. The
  cover is the cell less 18.

### What the application drew

Fixed 148 cards with their border outside, and the count dividing by 166:

| Window width | Prototype | Application |
| --- | --- | --- |
| 1600 | 9 covers of ~147 filling the row | 8 of 148 and **~160 px empty** |
| 1500 | 8 covers of ~155.5 filling the row | 8 of 148 and ~60 px empty |

### The fix

- **The count divides by 164.** The border leaves the count, because the prototype's minimum already
  holds it, and enters the cover's width.
- **`PosterRowPanel`, a new panel for each row**, shares its width out into N equal cells with every
  edge snapped to the device pixel, rounding half away from zero as a browser does. A partial row
  keeps the full row's cells, as `auto-fill` does.
- **`UniformGrid` was ruled out by its source, not by hunch**: in Avalonia 12.1.1 it rounds the cell
  width once and multiplies it by the index, so at 1600 the row would end at 1485 and at 1500 at 1392,
  four pixels past a 1388 surface; and with no column count it lays out a square.
- **The card fills its cell without touching `PosterCardView`**: on the grid's surface
  `PosterCardWidth` becomes automatic and `PosterCardHeight` the computed height, and the card's
  button stretches in the Library's template, not in the style it shares with Home. The chip, the
  watched mark and the words keep their size, as in the prototype: that is why the grid computes the
  height rather than a `Viewbox`, which would scale them with the cover.
- **The column count reaches every row by inheritance** from the surface. Binding it from inside the
  row with `$parent[ItemsControl]` would have found the row's own `ItemsControl`.
- **The count follows the Appearance settings.** With fixed cards a new cover size changed every
  card, and the resize that followed recounted. With fluid cards nothing changes size by itself, so
  the count stayed at 9 where 7 fit: measured red before the fix, with the cover at 176 and with the
  roomy density. Now two properties of the view, bound to those two values, recount when they
  change, and only then.
- **The height is only written when it changes**: a resource written again notifies every card even
  when its value is the same, and in the application the grid changes height with every page of
  titles.

### Measured in pixels, with the application mounted in memory

The real application was not opened: the in-memory harness draws with real Skia. The scene paints its
own colours — black covers with a black hairline, no art, no shadow — so the edges are the covers'
outer edges.

| Width | Covers | Rail to the first | Last to the edge | Widths | Height |
| --- | --- | --- | --- | --- | --- |
| 1500 | 8 | 33 | 33 | 156 and 155 alternating | 233 |
| 1600 | 9 | 33 | 33 | 147 and 148, one of 148 in every three | 221 |

**33 is the page's 32 margin plus the tile's 1 px border.** The page-margin evidence measured 34 on
the prototype because its hairline is light and the threshold it was counted with trims it; what is
compared is the symmetry, and it is now the same on both sides.

**And the prototype, measured the same day with headless Chrome at the same widths** (the
`prototype-fidelity` agent, counting the hairline, with the DOM as a second instrument), gives
**exactly the same**: 8 and 9 covers, 33 and 33, the widths in the same sequence — 156, 155… at
1500; 147, 148, 147, 147, 148… at 1600 —, heights of 233 and 221, and 18 px hairline to hairline
between covers. Redone by hand, the arithmetic of `PosterRowPanel.CellEdge` and
`LibraryView.CoverHeight` predicts its 16 and 18 edges. **One measuring condition**: when the
prototype's page scrolls, its 12 px scrollbar takes that width from the grid — at 1600×700, covers of
146 and 45 px to the edge — and the application's does not reserve it; compare without scrolling.

And at 125, 150 and 175 %: every edge lands on a whole device pixel, from 0 to the edge. A sweep of
1.17 million cells — 3000 widths, 1 to 12 columns, five scales — checks that none grows when it goes
through the round-up Avalonia applies when it places each card.

### The gates, and the break each one catches

Every new test was seen failing, before the code or under a mutation, and every batch of mutations
turned exactly the expected tests red:

| Mutation | What caught it |
| --- | --- |
| Dividing by 166 again | 12 tests, among them the count at the edge of each column |
| One rounded cell width, as `UniformGrid` does | 16, among them the mounted application at 1500 and 1600 |
| Edges rounded half to even | Only the literal edges at 1500, which is why it exists |
| No infinite-width guard | The row with no width to share |
| No squeezing of extra cards | The row with more cards than columns |
| Height rounded half to even, or with no floor at zero | The 1485/9 and 0 cases of the height |
| Not pinning the card's border under focus | The cover under keyboard focus |
| Not inheriting the column count | The partial row |
| Rewriting the height when it has not changed | The notification counter |
| Not following Appearance | The two cover and density tests |
| The automatic width in the shared resources | Home's card, from 148 to 94 |
| Fixed cards again | The mounted application: 37 px on the left at 1500, 1 px into the margin at 1600 |
| The panel places with Avalonia's rounding | The literal positions at 1500 and the sequence of widths in pixels |
| Edges rounded up instead of half away from zero | The literal positions at 1600 and the sequence of widths |
| The height with a fixed «less 18» | The roomy and compact densities, and a border of 2 |
| The height written into the whole application's resources | Home after the Library has counted, and the larger cover |
| Rewriting the height when the count changes rather than the height | The step from 1488 to 1476, which keeps nine columns |
| Rewriting the height only when the width changes | The two Appearance tests |
| The scale read as 1 | 277 pixels at 125 % where 276 belong |

**The last seven were proposed by the same day's gate audit** (the `gate-auditor` agent), and all
seven had the same shape: the rule was checked in the arithmetic function or with the default
settings, and no test read what the panel drew or what changed afterwards. Each test was fixed and
each one was seen failing under its mutation. The audit also moved the arithmetic tests onto
Avalonia's thread, because they now touch classes that register properties.

### Traps measured today

- **The shell mounted with no model leaves every destination visible at once.** For layout it does
  not matter; for counting pixels it is fatal: the first capture found two covers. The pixel
  measurement mounts the shell with a model and navigates to the Library.
- **A column 4 px from the edge falls into the 10 px corner**: it read 229 where there are 233.
- **`LayoutHelper.RoundLayoutValue` rounds half to even** and the browser away from zero; over eight
  tracks of 173.5 they disagree on two edges.
- **The focus test measured the button**, which now always fills its cell: it would have gone blind.
  It measures the cover.

### What is left, by name

- **The card's vertical rhythm, which goes together**: from the last line to the next cover 18
  against 20, from the cover to the title 8 against 10, and — new — the three lines under the cover
  are spaced 8 px apart where the prototype stacks them (ink line to line 26 and 24 against 20 and
  20). Each row comes out about 5 px taller today; fixing only the first two would leave it 9 px
  taller, so the three are fixed at once. And from the filters to the first cover, 21 against 17 by
  the code.
- **The card's shape, four visible differences**: the progress track is an opaque near-white grey
  where the prototype puts white at 25 % over the art; «unavailable» is an amber pill where the
  prototype veils the whole cover and writes in white; the kind chip carries 12 px at normal weight
  where the prototype puts 10.5 semibold over a blurred backdrop; and the watched mark is 22 at 9 px
  from the edge where the prototype puts 20 at 7.
- **Density**: only comfortable matches the prototype. It keeps the tile at 8 and changes the gap
  (10/16/22 between columns, 12/18/26 between rows); the application uses 4/8/16 as padding and half
  gap.
- **Home**: the prototype draws its two rails as a fluid grid with a 132 minimum and a 14 gap; the
  application draws horizontal rails of fixed 148 cards.
- **The first load's skeleton**: six fixed 158×237 boxes against eight cells of the same grid with
  two text bars. With `PosterRowPanel` it becomes cheap.
- **Posters decoded at 148**, with covers reaching about 187 at the narrowest window: a personal cover
  looks a little softer. Following the fluid width would decode again on every resize. And
  `CachedPosterConverter` declares two constants nothing reads.
- **Resizing with the Library scrolled moves what is on screen**: the browser anchors the position and
  `VirtualizingStackPanel` does not.
