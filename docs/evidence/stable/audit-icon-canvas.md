# El lienzo que la portación no copió / The canvas the port did not copy

Los iconos de esta aplicación se dibujaban entre **1,12× y 1,74× más grandes que el prototipo, cada
uno por un factor distinto**, y hasta **4,5 px descentrados**. La causa era una sola omisión: al
portarlos se copió el trazo y no el `viewBox` de 24 × 24. / This application's icons were drawn
between **1.12x and 1.74x larger than the prototype, each by a different factor**, and up to **4.5 px
off centre**. The cause was a single omission: the port copied the stroke and not the 24 by 24
`viewBox`.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-29.

## La causa, medida / The cause, measured

`Stretch="Uniform"` escala la geometría **por sus propios límites** hasta llenar el control, y ancla
lo que sobra arriba-izquierda. Sin el lienzo, los límites de cada geometría son los de su tinta, y
cada una ocupa una fracción distinta: / `Stretch="Uniform"` scales the geometry **by its own bounds**
until it fills the control, and pins the remainder top left. Without the canvas, each geometry's
bounds are its own ink, and each one spans a different fraction:

```
                     sin lienzo / no canvas        con / with «M0 0 M24 24»
IconHome              3,00  4,00  18,00x16,00      0,00  0,00  24,00x24,00
IconSettings          4,00  4,80  13,20x14,40      0,00  0,00  24,00x24,00
IconPlay              8,00  5,40  11,00x13,20      0,00  0,00  24,00x24,00
IconClose             6,20  6,20  11,60x11,60      0,00  0,00  24,00x24,00
IconChevronDown       5,50  9,00  13,00x 6,50      0,00  0,00  24,00x24,00
```

**Las 31 geometrías del diccionario miden ahora exactamente `0,0 24×24`**, y no una de muestra: la
puerta las recorre todas. / **All 31 geometries in the dictionary now measure exactly `0,0 24x24`**,
and not one sample: the gate walks every one of them.

## Dos `moveto` que no dibujan / Two movetos that draw nothing

La corrección es el prefijo `M0 0 M24 24` delante de cada trazo. La pregunta que había que contestar
antes de escribirla 31 veces es si esos dos `moveto` **pintan un punto** bajo un remate redondo, que
es exactamente como aparece tinta donde no se dibujó nada. Rasterizando el triángulo de reproducción
en un `Path` de 96 px: / The fix is the `M0 0 M24 24` prefix in front of each stroke. The question to
answer before writing it 31 times is whether those two movetos **paint a dot** under a round line
cap, which is exactly how ink appears where nothing was drawn. Rasterising the play triangle in a
96 px `Path`:

```
                      tinta / ink px      caja de la tinta / ink box
trazado  sin prefijo        1112          (10,10)..(93,109)
trazado  con prefijo         607          (42,32)..(89,87)
relleno  sin prefijo        3824          (12,12)..(90,107)
relleno  con prefijo        1158          (44,34)..(86,85)
```

**No pintan.** Con el prefijo la tinta empieza en (42,32) y no en la esquina; si los `moveto`
dibujaran, la caja arrancaría en (0,0). Y las esquinas del lienzo son justo 0,0 y 24,24, así que el
caso peor estaba cubierto. / **They do not paint.** With the prefix the ink starts at (42,32) and not
at the corner; if the movetos drew, the box would start at (0,0). And the canvas corners are exactly
0,0 and 24,24, so the worst case was the one measured.

## Lo que se ve, antes y después / What is seen, before and after

Rasterizando cada icono a la medida del carril y contando píxeles de tinta — antes con el `-2` de la
escala de clases (18 px, trazo 1,2) y después sin él (20 px, trazo 1,33) —, contra lo que el
prototipo dibuja con `icon(n, 20)`: / Rasterising each icon at the rail's size and counting ink
pixels — before with the class scale's `-2` (18 px, 1.2 stroke) and after without it (20 px, 1.33) —
against what the prototype draws with `icon(n, 20)`:

```
icono / icon         antes/before   después/after   prototipo   antes  después   dy antes  dy después
IconHome                 16,8           14,7          15,0      1,12    0,98      -1,00      +0,00
IconLibrary              16,8           14,7          14,2      1,19    1,04      +0,00      +0,00
IconSettings             18,8           12,7          12,0      1,57    1,06      +0,00      -0,50
IconSearch               16,8           12,7          12,8      1,31    0,99      +0,00      +0,00
IconPlay                 16,8           10,7          11,0      1,53    0,97      +0,00      +0,00
IconStop                 16,8           10,7          10,8      1,55    0,98      +0,00      +1,00
IconVolume               16,8           12,7          13,0      1,30    0,98      -2,00      +0,00
IconMute                 16,8           12,7          13,3      1,26    0,95      -2,00      +0,00
IconClose                16,8            8,7           9,7      1,74    0,90      +0,00      +0,00
IconChevronDown          16,8           10,7          10,8      1,55    0,98      -4,50      +0,00
IconChevronUp            16,8           10,7          10,8      1,55    0,98      -4,50      +0,00
IconFilm                 16,8           14,7          14,2      1,19    1,04      -2,00      +0,00
IconCheck                16,8           10,7          10,9      1,54    0,98      -2,50      +0,50
IconReset                17,8           11,7          12,1      1,47    0,97      +0,00      -0,50
```

**Los tres defectos son el mismo, y se van juntos.** / **The three defects are one, and they go
together.**

1. **El tamaño.** Antes, la columna «antes» es **16,8 para casi todos**: sin lienzo cada trazo se
   estira hasta llenar la caja, así que todos los iconos salían del mismo tamaño en pantalla sea cual
   fuere el que el prototipo quiso. Ahora cada uno mide lo suyo. / **Size.** The «before» column is
   **16.8 for nearly all of them**: without a canvas every stroke stretches to fill the box, so every
   icon came out the same size on screen whatever the prototype intended. Now each measures its own.
2. **La proporción.** El exceso iba de **1,12 a 1,74** — una dispersión de 0,62, un factor distinto
   por icono, que es lo que hacía imposible corregirlo restando dos. Ahora va de **0,90 a 1,06**, una
   dispersión de 0,16 que es ruido de cuantización sobre trazos de 10 a 15 px. / **Proportion.** The
   excess ran from **1.12 to 1.74** — a spread of 0.62, a different factor per icon, which is what
   made a subtraction of two useless. It now runs **0.90 to 1.06**, a spread of 0.16 that is
   quantisation noise over strokes 10 to 15 px wide.
3. **El centrado.** Los desplazamientos verticales de hasta **4,5 px** son **+0,00** en veintitrés de
   los treinta y uno, y ninguno pasa de 1 px. / **Centring.** Vertical offsets of up to **4.5 px** are
   **+0.00** in twenty-three of the thirty-one, and none now exceeds 1 px.

**Una cifra de esa tabla es un artefacto y no un dato**: `IconAdd` mide 0,8 en la columna «antes». Es
la única geometría hecha sólo de una recta vertical y una horizontal, y a trazo 1,2 el suavizado la
reparte entre dos columnas sin que ninguna baje del umbral de 110. La medición no encontró su tinta;
el icono estaba tan mal como los demás. Se deja escrito porque un cero que se lee como una medida es
justo la trampa que esta clase de arnés tiende. / **One figure in that table is an artefact and not a
datum**: `IconAdd` reads 0.8 in the «before» column. It is the only geometry made of nothing but one
vertical and one horizontal line, and at a 1.2 stroke the antialiasing splits it across two columns
with neither falling under the 110 threshold. The measurement did not find its ink; the icon was as
wrong as the rest. It is written down because a zero read as a measurement is exactly the trap this
kind of harness sets.

## Lo que lo vigila / What watches it

- **`PrototypeIconTests.Every_geometry_measures_the_prototypes_own_canvas`** parsea cada geometría y
  exige `0,0 24×24`. Parsear y no leer la cadena es el punto: el prefijo estuvo ausente cinco días
  sin que una comparación de caracteres lo notara, porque el prototipo declara su caja en el `svg` y
  esa comparación leía los `path` de dentro. / parses every geometry and requires `0,0 24x24`.
  Parsing rather than reading the string is the point: the prefix was absent for five days without a
  character comparison noticing, because the prototype declares its box in the `svg` and that
  comparison read the `path` elements inside it.
- **`PrototypeIconTests.A_shape_made_of_paths_is_the_prototypes_own_string`** exige el prefijo y
  luego lo retira antes de comparar el trazo. La puerta queda **más** estricta, no menos: antes
  afirmaba una cosa y ahora dos. / requires the prefix and then strips it before comparing the
  stroke. The gate ends up **stricter**, not looser: it asserted one thing and now asserts two.
- **`TransportGlyphTests`** ya comprobaba `1,6 · Width / 24`, y esa fórmula **asumía que la tinta
  llena el lienzo**. Era falso para las 31 y la puerta pasaba igual. Restituido el lienzo, la premisa
  de la puerta es cierta por primera vez. / already checked `1.6 · Width / 24`, and that formula
  **assumed the ink fills the canvas**. It was false for all 31 and the gate passed anyway. With the
  canvas restored, the gate's premise is true for the first time.

## Una puerta que se apoyaba en el defecto / A gate that leaned on the defect

`CatalogCardTextTests` distinguía la tarjeta de serie de la de película afirmando
`Assert.NotEqual(show.Bounds, film.Bounds)`. Eso **sólo funcionaba porque los iconos estaban mal**:
sus límites diferían por la omisión del lienzo. Con las 31 en `0,0 24×24` la aserción comparaba dos
cuadrados idénticos y se puso roja. / `CatalogCardTextTests` told a series card from a film card by
asserting `Assert.NotEqual(show.Bounds, film.Bounds)`. That **only worked because the icons were
wrong**: their bounds differed through the missing canvas. With all 31 at `0,0 24x24` the assertion
compared two identical squares and went red.

La corrección no es aflojarla sino decir lo que quería decir: **cada tarjeta lleva el recurso que le
toca**, `Assert.Same(Resource("IconShow"), show)`. Es más fuerte que la anterior, porque dos
geometrías distintas pueden estar las dos equivocadas. / The fix is not to loosen it but to say what
it meant: **each card carries the resource named for it**, `Assert.Same(Resource("IconShow"), show)`.
That is stronger than what it replaced, because two different geometries can both be wrong.

**Es una forma nueva del defecto de la casa**: no una puerta que mide el modelo de lo que vigila,
sino **una puerta cuyo verde dependía del defecto**. Vale la pena mirarla de frente, porque el
reflejo al ver ese rojo es relajar la aserción — y eso habría dejado la afirmación entera fuera. /
**It is a new shape of this repository's characteristic defect**: not a gate that measures the model
of what it watches, but **a gate whose green depended on the defect**. It is worth naming, because
the reflex on seeing that red is to relax the assertion — and that would have dropped the claim
entirely.

## Y una discrepancia que no se toca aquí / And a discrepancy not touched here

`docs/design/ELEMENTS.es.md` nombra cinco tamaños —14, 16, 18, 20 y 22— y las clases del tema
declaran cuatro: `size-14`, `size-16`, `size-20` y `size-22`. Contadas las llamadas a `icon(n, s)` en
el prototipo, los tamaños que gasta son **13, 14, 15, 16, 18, 20 y 26**, y el más frecuente con
diferencia es **15**, con diez usos. **No usa 22 en ningún sitio.** Queda anotado y sin cambiar: es
una decisión de diseño y no un defecto de esta pieza. / `docs/design/ELEMENTS.es.md` names five sizes
— 14, 16, 18, 20 and 22 — and the theme declares four classes. Counting the `icon(n, s)` calls in the
prototype, the sizes it spends are **13, 14, 15, 16, 18, 20 and 26**, and the most frequent by a wide
margin is **15**, with ten uses. **It never uses 22.** Noted and left alone: that is a design
decision and not a defect of this piece.
