# El margen de página era 48 donde el prototipo pone 32 / The Page Margin Was 48 Where the Prototype Puts 32

- IDs: `PRD-006`
- Fecha / Date: 2026-09-11
- Alcance / Scope: `Presentation/Shell/ShellView`, `Presentation/Library/LibraryView`,
  `Presentation/Home/HomeView`, `Presentation/Theme` (`DesignTokens`, `AppearanceService`),
  `UiTests/Shell/PageMarginTests`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Lo que se sabía, y lo que no era cierto de ello

La vuelta cuatro dejó en pie un defecto geométrico: **32 px entre el riel y la primera tarjeta en el
prototipo, 56 en la aplicación**. La hoja de ruta lo llamó «una sola cifra». Medido hoy, no lo era.

### Lo que dice el prototipo, en su código y en sus píxeles

- **Un único contenedor de página para todos los destinos**: `padding:28px 32px 48px` — 28 bajo la
  barra de título, 32 a cada lado, 48 abajo— sobre el contenedor que se desplaza.
- **Siete de sus páginas medidas a 1500 px abren en x = 96**, 32 después de su riel de 64: Biblioteca,
  Inicio, Cursos, Revisión, Duplicados, Ajustes y la ficha de serie.
- **La baldosa de una tarjeta es `padding:8px; margin:-8px`**: lleva 8 px de relleno alrededor de la
  imagen —el área que se ilumina al pasar por encima— y se sale de su celda esos mismos 8, de modo
  que la imagen empieza en la línea del título.

### Lo que dibujaba la aplicación

`Margin="48"` repetido en cada destino, y la tarjeta con su relleno de 8 sin compensar:

| Superficie | Prototipo | Aplicación |
| --- | --- | --- |
| Título y filtros de la Biblioteca | 32 | 48 |
| Primera portada | 32 | **56** |
| Cursos, Revisión, Duplicados, Ajustes | 32 | 48 |
| Filas de Inicio | 32 | 24 |
| Título bajo la barra | 28 | 48 |

### La corrección

- **`PageMargin` (32,28,32,48) es el relleno del prototipo como un solo valor**, y va en el
  contenido **dentro** de cada `ScrollViewer`, no en el `ScrollViewer`: es donde lo pone el
  prototipo y lo que muestra el ejemplo de la documentación de Avalonia para una página que se
  desplaza. La barra queda pegada al borde de la ventana, como allí.
- **La portada vuelve a la línea del título con la misma técnica que el prototipo.** El primer
  intento —sacar la rejilla 8 px de la vista— lo **rechazó con razón** la puerta de desbordamiento:
  la vista, montada sola, se salía 8 px de su ventana. La versión buena mete el contenido de la vista
  un margen de tarjeta (`PosterGutterX`), deja que la rejilla lo recupere (`NegativePosterGutterX`) y
  es la página la que coloca la vista un margen fuera. Ninguna vista se sale de sí misma, y los tres
  valores los escribe `AppearanceService` a partir de la densidad, que es la que decide el margen de
  una tarjeta: 4, 8 o 16.
- **Inicio sube de 24 a 32**, y su portada sigue a sangre como en el prototipo.
- **Ajustes**: el título y el índice pasan a 32 del riel y 28 bajo la barra. **Sus secciones no se
  tocan**: van centradas en su columna, y moverles sólo el margen derecho las descuadró 8 px entre
  sí — lo cazó `SettingsPageStructureTests`. Dónde empieza la columna de secciones es parte del
  defecto del índice de Ajustes, que tiene su turno en el orden del propietario.

### Medido en píxeles, antes y después

Capturas de la aplicación real a 1500 × 1000 lógicos, en claro, sobre la biblioteca sembrada; el
riel de la aplicación acaba en x = 72 (lleva 8 px de marco de ventana) y el del prototipo en x = 64.

| Medida | Prototipo | Antes | Después |
| --- | --- | --- | --- |
| Primera portada, desde el riel | 32 | 56 | **32** |
| Tinta del título, desde el riel | 35 | 50 | **34** |
| Primera fila de tinta del título | 83 | 101 | **81** |
| Borde derecho de la fila del título | 32 | 48 | **32** |
| Cursos, Revisión, Duplicados, Ajustes, desde el riel | 32 | 48 | **32** |
| Filas de Inicio, desde el riel | 32 | 24 | **32** |

El píxel de diferencia en la tinta del título es el lado de la «B» en cada fuente, no la caja.

### Las puertas

- `PageMarginTests` mide sobre el shell montado que cada página arranca a 32 del riel y 28 bajo la
  barra, y que la rejilla de la Biblioteca empieza un margen de tarjeta antes que el título. Se vio
  fallar devolviendo el margen a 48: 112 donde se esperaba 96.
- `AppearanceServiceTests` exige que los dos márgenes laterales sigan a la densidad; se vio fallar
  quitando la línea que los escribe.
- `ViewOverflowTests` y `ViewOverflowInShellTests` quedan en verde **con la segunda versión**; la
  primera las ponía rojas en los dos idiomas.

### A 1600 px, y lo que destapó

La fila del título se midió a 1500. **Las portadas, no**, y a 1500 no podían enseñar nada: la rejilla
pone ocho de ancho fijo y le sobra sitio. La segunda lectura se hizo a 1600, el ancho canónico de
`ELEMENTS.es.md`, en claro y en alto contraste oscuro, contando tarjeta y hueco por luminosidad con el
mismo umbral en las dos capturas:

| Medida a 1600 | Prototipo | Aplicación |
| --- | --- | --- |
| Primera portada, desde el riel | 34 | 34 |
| Portadas por fila | 9 | 9 |
| Última portada, hasta el borde de la página | 34 | **22** |

Los 34 son los 32 del margen más los 2 px que el umbral recorta de cada borde; la izquierda coincide.
**La novena portada de la aplicación se metía 9 px en el margen derecho.** La causa no estaba en el
margen, sino en la cuenta de columnas:

- La rejilla divide el ancho por la portada y su margen, 148 + 2 × 8 = 164. **La tarjeta mide 166**
  —medido en el arnés en memoria, no deducido—, porque lleva un borde de 1 px a cada lado: el
  `border:1px solid transparent` de la baldosa del prototipo, que aparece al pasar el ratón.
- Así que cerca del límite de cada columna contaba una que no cabía. A 1600 la rejilla mide 1488 px:
  la cuenta decía nueve (1488 / 164) y caben ocho (1488 / 166). Con los márgenes de 48 medía 1440 y
  daba ocho por las dos cuentas, por eso nadie lo había visto; el defecto ya existía en otros anchos.
- **Y con el foco del teclado la tarjeta medía 168**: `Button:focus-visible` pone 2 px de borde a
  todos los botones, y en la tarjeta ese borde es transparente —el anillo que se ve es el adorno de
  foco—, así que no dibujaba nada y empujaba al resto de la fila.

**La corrección**: el borde es un token, `PosterCardBorderThickness`, que pinta el estilo de la
tarjeta y suma la cuenta, y la tarjeta lo conserva en todos sus estados. A 1600 la aplicación pone
ahora ocho columnas y ninguna tarjeta pasa del borde de la rejilla. **Esto último está medido sobre
el modelo, en el arnés en memoria, y no en una captura**: la aplicación real se abre en el escritorio
de quien trabaja en esa máquina, y el propietario pidió ese día que no se abriera. El «antes» sí está
fotografiado, y su desbordamiento medido —9 px— es el que predice el modelo.

Las puertas: `LibraryGridTests` lee el ancho de una tarjeta ya dibujada y la pone al límite de cinco,
ocho y nueve columnas —un píxel antes y justo en él—; se vio fallar sin el borde en la cuenta («at 829
px the grid counted 5 columns of cards 166 px wide, and 4 fit») y con el token a 0. Otra prueba exige
que la tarjeta con el foco mida lo mismo, y se vio fallar antes de fijar su borde.

### Lo que queda, con nombre

- La columna de secciones de Ajustes empieza en 618 donde el prototipo la pone en 330: es el índice,
  y va en su turno.
- El relleno interior de la portada de Inicio (40,44 contra `54px 40px 40px`).
- **La rejilla fluida.** El prototipo escribe `repeat(auto-fill,minmax(148px,1fr))` con 16 px entre
  columnas: a 1600 le caben nueve celdas de unos 149 y las estira hasta llenar el ancho. La tarjeta
  de la aplicación es fija, así que a 1600 pone ocho y deja unos 160 px libres a la derecha.
  Registrado en la hoja de ruta para el orden del propietario.

  **El motivo que `PosterCardView` daba para no hacerla era falso**, y se repitió aquí sin
  comprobarlo hasta que el propietario pidió consultar el MCP de Avalonia: «Avalonia no tiene
  relación de aspecto». No tiene una propiedad como el `aspect-ratio` de CSS, pero la documentación da
  dos formas de mantenerla: `Viewbox`, que escala su contenido conservando la proporción —es su modo
  por defecto, `Stretch="Uniform"`—, y un panel propio que calcule el alto a partir del ancho. Las
  imágenes conservan la suya con el mismo `Stretch`. La rejilla fluida es trabajo, no un límite del
  marco.

## English

### What was known, and what was not true about it

Round four left one geometric defect standing: **32 px between the rail and the first card in the
prototype, 56 in the application**. The roadmap called it «a single figure». Measured today, it was
not.

### What the prototype says, in its code and in its pixels

- **One page container for every destination**: `padding:28px 32px 48px` — 28 under the title bar,
  32 on each side, 48 at the bottom — on the container that scrolls.
- **Seven of its pages measured at 1500 px open at x = 96**, 32 past its 64 px rail: library, home,
  courses, review, duplicates, settings and the series card.
- **A card's tile is `padding:8px; margin:-8px`**: it carries 8 px of padding around the picture —
  the area that lights up on hover — and reaches out of its cell by those same 8, so the picture
  starts on the title's line.

### What the application drew

`Margin="48"` repeated on every destination, and the card's 8 px padding left uncompensated:

| Surface | Prototype | Application |
| --- | --- | --- |
| Library title and filters | 32 | 48 |
| First cover | 32 | **56** |
| Courses, review, duplicates, settings | 32 | 48 |
| Home rows | 32 | 24 |
| Title under the bar | 28 | 48 |

### The fix

- **`PageMargin` (32,28,32,48) is the prototype's padding as a single value**, and it goes on the
  content **inside** each `ScrollViewer`, not on the `ScrollViewer`: that is where the prototype puts
  it and what Avalonia's documentation shows for a scrolling page. The scroll bar keeps to the
  window's edge, as there.
- **The cover returns to the title's line with the prototype's own technique.** The first attempt —
  pulling the grid 8 px out of the view — was **rightly refused** by the overflow gate: the view,
  mounted alone, reached 8 px outside its window. The good version insets the view's content by a
  card gutter (`PosterGutterX`), lets the grid take it back (`NegativePosterGutterX`), and has the
  page place the view one gutter out. No view reaches outside itself, and `AppearanceService` writes
  all three from the density, which is what decides a card's gutter: 4, 8 or 16.
- **Home goes from 24 to 32**, and its hero still bleeds as in the prototype.
- **Settings**: the title and the index move to 32 from the rail and 28 under the bar. **Its sections
  are not touched**: they are centred in their column, and moving only their right margin set them
  8 px apart from each other — `SettingsPageStructureTests` caught it. Where the section column
  starts is part of the settings-index defect, which has its turn in the owner's order.

### Measured in pixels, before and after

Captures of the real application at 1500 × 1000 logical, in light, over the seeded library; the
application's rail ends at x = 72 (it carries an 8 px window frame) and the prototype's at x = 64.

| Measure | Prototype | Before | After |
| --- | --- | --- | --- |
| First cover, from the rail | 32 | 56 | **32** |
| Title ink, from the rail | 35 | 50 | **34** |
| First row of title ink | 83 | 101 | **81** |
| Right edge of the title row | 32 | 48 | **32** |
| Courses, review, duplicates, settings, from the rail | 32 | 48 | **32** |
| Home rows, from the rail | 32 | 24 | **32** |

The one pixel of difference in the title's ink is each font's side bearing on the «B», not the box.

### The gates

- `PageMarginTests` measures on the mounted shell that every page starts 32 from the rail and 28
  under the bar, and that the library's grid starts one card gutter before the title. It was seen
  failing with the margin put back to 48: 112 where 96 was expected.
- `AppearanceServiceTests` requires both side gutters to follow the density; it was seen failing with
  the line that writes them removed.
- `ViewOverflowTests` and `ViewOverflowInShellTests` stay green **with the second version**; the
  first turned them red in both languages.

### At 1600 px, and what it uncovered

The title row was measured at 1500. **The covers were not**, and at 1500 they could not show
anything: the grid lays out eight of fixed width and has room to spare. The second reading was taken
at 1600, the canonical width in `ELEMENTS.en.md`, in light and in high contrast dark, counting card
and gap by brightness with the same threshold on both captures:

| Measure at 1600 | Prototype | Application |
| --- | --- | --- |
| First cover, from the rail | 34 | 34 |
| Covers per row | 9 | 9 |
| Last cover, to the page's edge | 34 | **22** |

The 34 are the 32 of the margin plus the 2 px the threshold trims from each edge; the left side
matches. **The application's ninth cover ran 9 px into the right margin.** The cause was not in the
margin but in the column count:

- The grid divides the width by the cover and its padding, 148 + 2 × 8 = 164. **The card is 166** —
  measured in the in-memory harness, not deduced — because it carries a 1 px border on each side: the
  `border:1px solid transparent` of the prototype's tile, which appears under the pointer.
- So close to the edge of each column it counted one that did not fit. At 1600 the grid is 1488 px
  wide: the count said nine (1488 / 164) and eight fit (1488 / 166). With the 48 margins it was 1440
  wide and both counts said eight, which is why nobody had seen it; the defect already existed at
  other widths.
- **And under the keyboard the card was 168**: `Button:focus-visible` gives every button a 2 px
  border, and on the card that border is transparent — the ring a person sees is the focus adorner —
  so it drew nothing and pushed the rest of the row along.

**The fix**: the border is a token, `PosterCardBorderThickness`, which the card's style paints and
the count adds, and the card keeps it in every state. At 1600 the application now lays out eight
columns and no card passes the edge of the grid. **That last part is measured on the model, in the
in-memory harness, and not on a capture**: the real application opens on the desktop of whoever is
working on that machine, and the owner asked that day for it not to be opened. The «before» is
photographed, and its measured overflow — 9 px — is the one the model predicts.

The gates: `LibraryGridTests` reads the width of a card already laid out and puts it at the edge of
five, eight and nine columns — one pixel short and exactly on it; it was seen failing without the
border in the count («at 829 px the grid counted 5 columns of cards 166 px wide, and 4 fit») and with
the token at 0. Another test requires the focused card to keep its width, and was seen failing before
its border was held.

### What remains, by name

- The settings section column starts at 618 where the prototype puts it at 330: that is the index,
  and it comes in its turn.
- The home hero's inner padding (40,44 against `54px 40px 40px`).
- **The fluid grid.** The prototype writes `repeat(auto-fill,minmax(148px,1fr))` with 16 px between
  columns: at 1600 nine cells of about 149 fit and it stretches them to fill the width. The
  application's card is fixed, so at 1600 it lays out eight and leaves about 160 px free on the
  right. Registered in the roadmap for the owner's order.

  **The reason `PosterCardView` gave for not building it was false**, and it was repeated here
  without checking until the owner asked for Avalonia's MCP to be consulted: «Avalonia has no aspect
  ratio». It has no property like CSS's `aspect-ratio`, but the documentation gives two ways to keep
  one: `Viewbox`, which scales its content keeping its proportion — its default,
  `Stretch="Uniform"` — and a panel of one's own that computes the height from the width. Images keep
  theirs with the same `Stretch`. The fluid grid is work, not a limit of the framework.
