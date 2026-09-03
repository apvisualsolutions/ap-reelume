# La versalita no era una, eran seis, y la que el árbol usaba más no llevaba separación ninguna / The small caps were not one overline but six, and the one the tree used most carried no tracking at all

Segundo trabajo del barrido de fidelidad que
[ADR-0007](../../adr/0007-every-element-matches-the-prototype.md) dejó abierto, después del de los
radios. El relevo lo describía como «el prototipo la usa en 35 sitios y el árbol en dos». **Las dos
mitades eran cortas**, y medirlas antes de escribir nada fue lo que cambió el trabajo. / The second
job of the fidelity sweep ADR-0007 left open, after the radii. The handover described it as «the
prototype uses it in 35 places and the tree in two». **Both halves were short**, and measuring them
before writing anything is what changed the job.

Fecha / Date: 2026-09-03. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que contestó la medición / What the measurement answered

El prototipo dibuja versalita en 35 sitios, sí, pero **no es una versalita: son nueve combinaciones
distintas** de tamaño, peso y separación entre letras. Y el árbol no la dibujaba en dos sitios sino
en **dieciséis, con tres clases**. / The prototype does draw small caps in 35 places, but **it is not
one overline: it is nine distinct combinations** of size, weight and tracking. And the tree drew it
in **sixteen places across three classes**.

```
Diseño / Design            sitios   Árbol / Tree
11 / 400 / .06em              9      section-overline   (coincidía / agreed)
11 / 700 / .10em              7      —
10 / 700 / .14em              7      —
10.5 / 400 / .06em            4      —
10.5 / 400 / .18em            3      hero-overline dibujaba 12 / .16em
10 / 400 / .05em              2      card-eyebrow dibujaba 12 y NINGUNA separación
11 / 700 / .12em              1      — (andamiaje del prototipo / prototype scaffolding)
10 / 700 / .16em              1      — (menú que este árbol no tiene / a menu this tree lacks)
11 / 400 / .05em              1      — (la cara de un desplegable / a drop-down's face)
```

**Poner una sola clase en los 35 sitios habría inventado una uniformidad que el diseño no tiene**,
que es el defecto de ADR-0007 apuntando en la otra dirección. / **Putting one class on all 35 sites
would have invented a uniformity the design does not have**, which is ADR-0007's defect pointing the
other way.

## Las tres discrepancias que se ven / The three visible divergences

· **El antetítulo de la portada dibujaba 12 donde el diseño dibuja 10,5**, y su propio comentario
  afirmaba que la separación del diseño era 0,16em cuando es 0,18em. La separación estaba bien a dos
  decimales **por casualidad** —0,16 × 12 = 1,92 y 0,18 × 10,5 = 1,89, y estaba escrito 1,9— mientras
  el tamaño se iba en un tamaño y medio. Dos números copiados a mano, uno de los cuales coincidía por
  accidente. / **The hero's kicker drew 12 where the design draws 10.5**, and its own comment claimed
  the design's tracking was 0.16em when it is 0.18em. The tracking was right to two decimals **by
  coincidence** while the size was a size and a half out.

· **Las ocho cabeceras de la tabla de duplicados eran las únicas mayúsculas de toda la aplicación sin
  separación entre letras**, que es la única cosa que todas las versalitas del diseño hacen. /
  **The eight column headings of the duplicates table were the only capitals in the whole application
  with no tracking**, which is the one thing every overline in the design does.

· **Los dos campos de tiempo de un marcador no tenían etiqueta visible.** El diseño dibuja «INICIO» y
  «FIN» sobre ellos; el árbol sólo tenía un nombre accesible, así que la única forma de contestar
  «¿cuál de estos dos números es el principio?» era tabular hasta él y escuchar. / **A marker's two
  time fields had no visible label.** The design draws «INICIO» and «FIN» over them; the tree had
  only an accessible name, so the only way to answer «which of these two numbers is the start?» was
  to tab into it and listen.

## Dos textos que gritaban y no debían / Two strings shouting when they should not

· **«CONFIANZA» no es versalita en el diseño**: la dibuja a 12 px en la tinta secundaria y en
  minúscula, con la cifra en semi-negrita al lado. El árbol la gritaba. / **«CONFIANZA» is not an
  overline in the design**: it is 12 px in the secondary ink in ordinary case. The tree shouted it.

· **La ruta de la carpeta se gritó y hubo que separarla en dos.** Ese texto es también el nombre
  accesible del campo, así que gritar el recurso hace que un lector de pantalla anuncie el nombre del
  campo a gritos. Se separó en la pareja que `AudioOutputView` ya usaba: uno se pinta, otro se
  anuncia. / **The folder path was shouted and had to be split in two.** That string is also the
  field's accessible name, so shouting the resource has a screen reader announce the field's name in
  capitals.

## Y un comentario nombraba un mecanismo que no existe / And a comment named a mechanism that does not exist

`DesignTokens.axaml` decía que las mayúsculas «vienen de `UpperCaseConverter`, que es lo que AXAML
tiene en lugar de `text-transform`», y que **no** se escriben en el recurso. Buscado el 2026-09-03:
**no existe tal conversor en este árbol**, y dieciséis recursos están escritos en mayúsculas.
`AudioOutputView` explica lo contrario y es la mitad que era cierta —AXAML no tiene `text-transform`
ni forma de componer un conversor con un recurso que sigue al idioma, así que un encabezado es su
propia cadena—. **Un comentario que nombra un mecanismo que nadie construyó es el defecto de la casa
apuntado a su propia documentación.** / A comment claimed the capitals came from an
`UpperCaseConverter`. **No such converter exists in this tree**, and sixteen resources are written in
capitals. The view next door explains the opposite, and that half was true.

## Que las seis se ven, contado en píxeles / That the six are six, counted in pixels

Tres propiedades pueden diferir sin que la tinta lo haga: media unidad de tamaño y media de
separación son justo las magnitudes que un rasterizador puede tragarse. Rasterizado sobre «SEÑALES
CONSIDERADAS»: / Three properties can differ while the ink does not. Rasterised over «SEÑALES
CONSIDERADAS»:

```
column-overline   122 px      notice-overline   146 px
player-overline   131 px      group-overline    153 px
section-overline  137 px      hero-overline     154 px
```

**Media unidad de tamaño SÍ se ve**: 10,5 dibuja 131 donde 10 dibuja 122 y 11 dibuja 137. Y cada una
dibuja más ancha que el mismo texto a su mismo tamaño sin separación, así que la separación llega a
la pantalla. / **Half a unit of size IS visible.** And each one draws wider than the same words at
the same size with no tracking.

### El umbral de tinta era el de otro tamaño de letra / The ink threshold belonged to another size

La primera lectura dijo que la separación más pequeña **no llegaba a la pantalla** con una palabra
corta. Era falso, y lo dijo medir el aparato en vez de creerle: con el umbral de 110 que usa la
puerta de los botones, este lector **perdía la «I» inicial de «INICIO» en cuatro de las seis** y casi
toda la clase más pequeña —contaba 12 px de tinta donde la clase dibuja 30—. La letra de 10 px es
fina: las astas de sus glifos más claros nunca alcanzan un umbral calibrado para una etiqueta de 14.
/ The first reading said the smallest tracking **was not reaching the screen** with a short word. It
was false, and measuring the instrument rather than trusting it is what said so: at the threshold of
110 the button gate uses, this reader **lost the leading «I» of «INICIO» in four of the six**.

**200 tampoco es una adivinanza**: la lectura es idéntica a 200, 230 y 245, así que cualquier valor
de esa banda encuentra la misma tinta y el blanco de la escena —255— se queda fuera. **Ese tramo
plano es lo que hace el número seguro en vez de afortunado.** / **200 is not a guess either**: the
reading is identical at 200, 230 and 245, so anything in that band finds the same ink. **That flat
stretch is what makes the number safe rather than lucky.**

## Lo que la puerta NO ve, escrito dentro de ella / What the gate does NOT see, written inside it

El censo encuentra los sitios donde **el árbol** dibuja mayúsculas. Un sitio que el diseño dibuja en
versalita y el árbol dibuja plano **es invisible para él**, porque no hay cadena gritada que
encontrar. Nada mide esa dirección, así que se lleva a mano en una lista cerrada, y una entrada es el
único registro de que alguien miró. / The census finds the places **the tree** draws capitals. A
place the design draws in capitals and the tree draws flat **is invisible to it**. Nothing measures
that direction, so it is kept by hand in a closed list.

Hay cuatro entradas, y una de ellas es la que enseña dónde acaba esta tanda: **el rótulo del hilo de
un curso**. El árbol dibuja el texto, en su sitio y con sus palabras, como subtítulo de 20 px en
semi-negrita; el diseño lo dibuja como versalita de 10 px en la tinta del acento y le da el peso a la
lección de debajo. **Cambiar sólo la tipografía dejaría esa tarjeta a medio camino entre dos
diseños**, así que pertenece a la tanda que toma esa vista entera contra el prototipo. / There are
four entries, and one of them is where this batch stops: **the course thread's kicker**. Changing
only the typography would leave that card half in each design.

## Lo verificado / What was verified

```
UiTests             1168 / 1168
DocumentationTests    97 / 97
ArchitectureTests     34 / 34
AccessibilityTests    37 / 37
dotnet format         limpio / clean
build -warnaserror    limpio / clean
El paseo / the walk   246 declaradas, 241 identidades, 218 pulsadas, 23 pendientes — sin mover
```
