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

> **Corrección del 2026-08-29, y el error importa más que el dato.** Esta sección afirmó que el
> prototipo **no usa 22 en ningún sitio**. Es **falso**, y lo era por la razón que este repositorio
> ya tiene escrita: **una ausencia inferida de un `grep` en vez de medida**. El patrón usado exigía
> una cadena literal como primer argumento —`icon\('[a-z]+',\s*[0-9]+\)`— y **diez llamadas del
> prototipo pasan una expresión**, entre ellas la que decide el conmutador de reproducción:
> `icon(p.playing && !err ? 'pause' : 'play', 22)`. / **Correction of 2026-08-29, and the error
> matters more than the datum.** This section claimed the prototype **never uses 22**. That is
> **false**, for the reason this repository already has written down: **an absence inferred from a
> `grep` rather than measured.** The pattern required a string literal as the first argument, and
> **ten of the prototype's calls pass an expression**, among them the one deciding the play toggle.

Contado con `icon\([^)]*,\s*[0-9]+\)`, que sí captura las expresiones, el prototipo gasta **nueve**
tamaños: / Counted with `icon\([^)]*,\s*[0-9]+\)`, which does capture the expressions, the prototype
spends **nine** sizes:

```
 12 → 2 usos     14 → 8     16 → 5     20 → 3     26 → 1
 13 → 2          15 → 10    18 → 5     22 → 1
```

**`size-22` está donde el prototipo la pone**: `TransportControlsView` y `MiniPlayerChromeView` la
gastan en play, pausa y parada, que es exactamente `icon(…, 22)`. `ELEMENTS` tenía razón al nombrarla.
/ **`size-22` is where the prototype puts it**: the transport and the mini spend it on play, pause and
stop, which is exactly `icon(…, 22)`. `ELEMENTS` was right to name it.

## Y la escala, alineada con el prototipo contexto a contexto / And the scale, aligned context by context

Con el lienzo restituido **la clase pasa a ser el tamaño real**, así que por primera vez se puede
comparar cada contexto de la aplicación con lo que el prototipo dibuja ahí. Medido el 2026-08-29: /
With the canvas restored **the class becomes the real size**, so for the first time each context can
be compared against what the prototype draws there. Measured on 2026-08-29:

```
contexto / context                                     antes/before  prototipo  ahora/now
play, pausa y parada del transporte y del mini            22             22        22
atrás y adelante, destinos del rail                       20             20        20
cromo del reproductor: mini, pantalla completa, cerrar     20             18        18
volumen y silencio                                        16             18        18
búsqueda, fila de menú                                    16             16        16
acciones personales                                       16             15        15
galón, aviso                                              14             14        14
el glifo de tipo sobre una portada                        14             12        12
el play de una tarjeta                                    14             15        14  ←
```

Seis contextos ya casaban. Cuatro no, y **tres se corrigieron**: el cromo bajó de 20 a 18, el volumen
subió de 16 a 18 —iba **más pequeño** que el prototipo, al contrario que todos los demás— y el glifo
de tipo bajó de 14 a 12. / Six already matched. Four did not, and **three were corrected**: the chrome
went 20 to 18, the volume went 16 to 18 — it ran **smaller** than the prototype, unlike every other —
and the kind glyph went 14 to 12.

### El cuarto no se corrigió, y el porqué está medido / The fourth was not, and the why is measured

**El play de una tarjeta se queda en 14 donde el prototipo dibuja 15.** Subirlo se probó y **movió la
entrada de biblioteca 44 px hacia abajo en 6 de las 36 combinaciones** de `HomeLayoutTests` — las de
1366 × 768 a escala 150 en español, la más apretada que la aplicación admite —, porque un píxel de más
en ese botón hace envolver una línea. / **A card's play stays at 14 where the prototype draws 15.**
Raising it was tried and **moved the library entry 44 px down in 6 of the 36 combinations** in
`HomeLayoutTests` — 1366 x 768 at 150 scale in Spanish, the tightest the application supports —
because one pixel more in that button wraps a line.

```
ganancia / gain      0,55 px de tinta   (7,70 → 8,25)
coste / cost           44 px de desplazamiento en 6 de 36
```

**Ochenta a uno en contra.** La fidelidad al prototipo es el encargo permanente de este proyecto, y
por eso la desviación se escribe con su precio en vez de callarse: si esa fila deja de ir justa, el
cambio son dos caracteres. / **Eighty to one against.** Fidelity to the prototype is this project's
standing commission, which is why the deviation is written down with its price rather than left
silent: if that row stops running tight, the change is two characters.

## Una ausencia que se afirmó sin medirla / An absence asserted without measuring it

Esta evidencia dijo, en su primera versión, que **el prototipo no usa el tamaño 22 en ningún sitio**.
Era falso, y el error es más instructivo que el dato: el patrón usado —`icon\('[a-z]+',\s*[0-9]+\)`—
exigía **una cadena literal** como primer argumento, y **diez llamadas del prototipo pasan una
expresión**. Entre ellas, precisamente, la que decide el conmutador de reproducción:
`icon(p.playing && !err ? 'pause' : 'play', 22)`. / This evidence said, in its first version, that the
prototype **never uses size 22**. That was false, and the error teaches more than the datum: the
pattern required **a string literal** as the first argument, and **ten of the prototype's calls pass
an expression** — among them the very one deciding the play toggle.

**Y la afirmación viajó**: llegó a `ELEMENTS` en los dos idiomas, a la nota de la sesión siguiente y
al mensaje de un commit, todo con la seguridad que da un número. Una ausencia se prueba; un patrón
que no casó nada no es una prueba de nada. / **And the claim travelled**: it reached `ELEMENTS` in
both languages, the next session's note and a commit message, all with the confidence a number gives.
An absence is proven; a pattern that matched nothing proves nothing.

**Lo que lo vigila ahora**: `Every_size_class_is_its_own_number_and_one_the_prototype_spends` lee los
tamaños **del prototipo** con un patrón que acepta expresiones, y exige dos cosas de cada clase — que
su nombre sea su `Width`, y que ese número sea uno que el prototipo gaste. Probada contra sus dos
mutaciones: `size-15` con `Width="13"` y una `size-17` inventada. / **What watches it now**:
`Every_size_class_is_its_own_number_and_one_the_prototype_spends` reads the sizes **from the
prototype** with a pattern that accepts expressions, and demands two things of every class — that its
name is its `Width`, and that the number is one the prototype spends. Proved against both its
mutations.

## Y el descuido que dejó el mismo botón a dos tamaños / And the slip that left one button at two sizes

Alinear el play movió su clase en **tres** vistas. Medido el coste, se puso atrás en **dos**, y
`MovieDetailsView` se quedó descolgada: el mismo botón «Reproducir» a 15 en la ficha de una película y
a 14 en las otras cuatro pantallas que lo dibujan. **Peor que cualquiera de los dos tamaños**, porque
quien navega entre ellas compara el botón consigo mismo. / Aligning the play moved its class in
**three** views. With the cost measured it went back in **two**, and `MovieDetailsView` was left
behind: the same «Reproducir» button at 15 on a film's page and at 14 on the four other screens that
draw it. **Worse than either size**, because a reader moving between them compares the button against
itself.

Lo cazó un `grep` al revisar el diff, no una puerta — así que ahora hay puerta:
`The_play_of_a_catalogue_action_is_one_size_in_every_view_that_draws_it`, con las cinco vistas en
**tabla cerrada**, de modo que una sexta que dibuje ese play falla hasta que alguien diga qué tamaño
toma. Probada reintroduciendo el descuido: nombra la vista culpable. / A `grep` while reviewing the
diff caught it, not a gate — so there is a gate now, with the five views in a **closed table**, so a
sixth drawing that play fails until somebody says what size it takes. Proved by reintroducing the
slip: it names the offending view.

**La lección, y es de las que se repiten**: un cambio revertido «a medias» no deja el estado anterior
ni el nuevo, deja un tercero que nadie eligió. Al deshacer parte de un barrido, la comprobación no es
que el diff encoja sino que **lo que queda sea coherente**. / **The lesson, and it is one that
recurs**: a change reverted «halfway» leaves neither the old state nor the new, but a third one
nobody chose. When undoing part of a sweep, the check is not that the diff shrinks but that **what
remains is coherent**.
