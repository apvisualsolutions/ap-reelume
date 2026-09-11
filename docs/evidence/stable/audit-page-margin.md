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
| Borde derecho del contenido | 32 | 48 | **32** |
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

### Lo que queda, con nombre

- La columna de secciones de Ajustes empieza en 618 donde el prototipo la pone en 330: es el índice,
  y va en su turno.
- El relleno interior de la portada de Inicio (40,44 contra `54px 40px 40px`).
- El ancho de la tarjeta no se toca: la rejilla del prototipo es fluida.

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
| Right edge of the content | 32 | 48 | **32** |
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

### What remains, by name

- The settings section column starts at 618 where the prototype puts it at 330: that is the index,
  and it comes in its turn.
- The home hero's inner padding (40,44 against `54px 40px 40px`).
- The card width is not touched: the prototype's grid is fluid.
