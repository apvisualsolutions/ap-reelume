# La ficha 2:3, y la cuadrícula que esta vez gana / The 2:3 card, and the grid that wins this time

Pasos 1 y 2 del plan del rediseño: la pieza que se repite en cuatro sitios, y la reversión —con su
número— de la decisión de no hacer la cuadrícula fluida. / Steps 1 and 2 of the redesign plan: the
piece that repeats in four places, and the reversal — with its number — of the decision not to build
the fluid grid.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La ficha, y por qué las iniciales no son un hueco / The card, and why the initials are not a hole

`PosterCardView`, 148 × 222, que es 2:3 exacto y la anchura a la que el prototipo aprueba su
cuadrícula (`minmax(148px, 1fr)`; sus carriles usan 132). Las iniciales van sobre `ControlFillBrush`
en `FontSizeDisplay` y `TextSecondaryBrush`, que es lo que la §4 pide literalmente: «iniciales sobre
`ControlFillBrush`, **nunca un hueco**». / The initials are what the card shows, not a placeholder
waiting for a picture: this application ships with no TMDB token and no connection that could fetch
one.

**Los cuatro modelos implementan `IPosterCard`, y eso es lo que hace que las omisiones se declaren en
vez de pintarse solas.** / An interface rather than reflection bindings, so what a list does not know
cannot be painted as if it did:

| Modelo / Model | Pie / Caption | Barra / Bar | Distintivo / Badge |
| --- | --- | --- | --- |
| `CatalogItemViewModel` | año / year | **no** | sí / yes |
| `InProgressItemViewModel` | `T1 · E2` | **sí / yes** | sí / yes |
| `RecentlyAddedItemViewModel` | año / year | no | sí / yes |
| `RecommendationItemViewModel` | — | no | **no** |

- **El catálogo no dibuja barra, y no por falta de dato.** `CatalogItem.HasProgress` dice **que** se
  empezó y no **cuánto**; una barra a cero para algo visto a medias es peor respuesta que ninguna
  barra. / The catalogue knows a title was started and not how far.
- **`UnavailableBadge` se queda fuera de la ficha**, por lo mismo: dos carriles saben si el medio
  está a mano y el de recomendaciones no. Dentro de la ficha habría dicho «disponible» sobre algo que
  nadie consultó. / The suggestions rail does not know, so the card never claims it.

## Y lo que la ficha destapó: Inicio se dibujaba fuera de la ventana / What the card uncovered

`HomeView` daba el `*` a la fila del carril en curso. Con las tarjetas viejas —220 × 100— eso se
notaba poco; con las nuevas, medido **a 1600 × 1000 en la aplicación real**:

```
antes / before:  "Añadido recientemente" y "Quizá te interese" NO llegan a pantalla
                 el carril que sí tiene sitio enseña media ficha
después / after: las cinco secciones se alcanzan; Inicio se desplaza
```

Las seis filas pasan a `Auto` dentro de un `ScrollViewer`. **Coste medido**: la baseline `T30` se
mueve en `LibraryEntryBottom` **+3 px lógicos en 12 de sus 36 registros** —los de 4K con viewport de
2160 y 1440, que son justo aquellos en los que Inicio pasó a ser más alto que su ventana—, y
`LibraryEntryWithinFirstViewport` sigue en `True` en los 36. / The only field that moves, and the
guarantee it protects is unchanged.

**Y una trampa de AXAML que costó una roja**: `ProgressBar` trae `MinWidth 200` del tema base, así que
estirada dentro de una portada de 148 salía 54 px más ancha que la imagen a la que pertenece,
recortada por `ClipToBounds` en vez de ajustada. Es la misma forma que `MinHeight` con `Height`: **el
setter que nombra el número no siempre es el que decide.** / The setter that names a number is not
always the one that decides it.

## La cuadrícula fluida: la decisión se revierte, y este es el número / The fluid grid, reversed

El 2026-08-20 se registró como discrepancia con la razón «en Avalonia 12.1.1 no existe nada que
reflowe y virtualice a la vez». **Eso es cierto de los paneles y falso del problema.** Medido el
2026-08-22 sobre **diez mil fichas** en una ventana de 1600 × 1000, en Release: / Measured over ten
thousand cards, in Release:

```
VirtualizingStackPanel, una ficha por fila (lo de hoy)       13 ms        4 fichas vivas
WrapPanel                                                  4559 ms   10 000 fichas vivas
ListBox + VirtualizingStackPanel, filas de 9                108 ms       36 fichas vivas
ItemsControl en ScrollViewer, filas de 9                      6 ms       36 fichas vivas
```

**760× el tiempo y 278× los controles vivos.** Y el reflujo —reagrupar diez mil al cambiar el ancho—
cuesta lo que cuesta un fotograma: / And the reflow costs about one frame:

```
reflow a 1200 px (7 columnas)                                10 ms       28 fichas vivas
reflow a  900 px (5 columnas)                                 3 ms       20 fichas vivas
reflow a 1600 px (9 columnas)                                12 ms       36 fichas vivas
```

**Lo que faltaba no era un control que Avalonia no tiene: era agrupar los elementos antes de dárselos
al que sí tiene.** / What was missing was never a control Avalonia lacks.

### Y la premisa del plan era falsa, medida / And the plan's premise was false, measured

La nota decía «`Avalonia.Controls.ItemsRepeater` y `WrapLayout` **SÍ existen** en 12.1.1». **No
existen.** Una sonda de compilación contra las referencias reales del proyecto: / A compile probe
against the project's actual references:

```
error CS0234: 'ItemsRepeater' no existe en el espacio de nombres 'Avalonia.Controls'
error CS0234: 'WrapLayout'    no existe en el espacio de nombres 'Avalonia.Layout'
```

Y traerlos por paquete tampoco: `Avalonia.Controls.ItemsRepeater` se detiene en **12.0.0** —contra
una solución fijada en 12.1.1— y `WrapLayout` **no es de Avalonia**, vive en `Avalonia.Labs.Panels`,
que llega a 12.0.2. Ninguno de los dos hace falta. / Neither package reaches 12.1.1 and neither is
needed.

**Undécima alarma falsa, y de las caras: esta habría hecho añadir dos dependencias por detrás del
Avalonia que el proyecto fija, para resolver algo que el árbol ya podía.** / An absence is proven, not
inferred — and so is a presence.

## Dónde vive cada decisión / Where each decision lives

- **El píxel está en la vista y sólo en la vista.** `LibraryView.axaml.cs` mide su superficie y le
  dice al modelo cuántas caben; `LibraryViewModel` agrupa y no sabe de píxeles. / No pixel in the
  model.
- **La separación es una sola.** El padding de 8 del botón de la ficha, a los dos lados, es todo el
  hueco —horizontal y vertical—. Un `Spacing` encima habría sido un número más que la aritmética
  tendría que conocer, y **la primera medición de esta cuadrícula falló exactamente ahí**: contó ocho
  columnas en 1352 px y dibujó la octava 72 px fuera. / One source for the gap is what lets the column
  count be arithmetic.
- **Los dos números de la ficha son tokens** (`PosterCardWidth`, `PosterCardHeight`), porque los lee
  el marcado que la pinta **y** el C# que divide por ellos. Escritos dos veces se habrían
  contradicho. / Two readers, one declaration.

```
Suite / Suite: ApSolutions.LocalMedia.UiTests — 722 en verde / 722 green
```
