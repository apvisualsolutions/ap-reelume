# El cromo del minirreproductor / The mini player's chrome

Evidencia de **PLY-007**: la franja del mini pasa de cinco botones sueltos a la composición del
prototipo —barra de progreso de tres píxeles, título y reloj—, y con ella se cierra un defecto de
contraste que ninguna puerta veía. / Evidence for **PLY-007**: the mini's band goes from five loose
buttons to the prototype's composition — a three pixel bar of progress, a title, and a clock — and
with it closes a contrast defect no gate could see.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-28.

## El defecto que ninguna puerta veía / The defect no gate could see

La franja pintaba `ShellSurfaceBrush` desde el día que se escribió, y sus cinco botones toman
`PlayerTextBrush` de la clase `player-chrome`. En los dos temas claros esos dos son **el mismo
color**. Medido sobre la vista montada, antes de tocar nada: / The band painted `ShellSurfaceBrush`
from the day it was written, and its five buttons take `PlayerTextBrush` from the `player-chrome`
class. In both light themes those two are **the same colour**. Measured on the mounted view, before
any change:

```
Light              glyphs 1,02:1   ink #F8FAFC   band #FBFCFE
HighContrastLight  glyphs 1,00:1   ink White     band White
Dark               pasa / passes
HighContrastDark   pasa / passes
```

**Cinco botones sin nada visible encima**, en la mitad de los temas que la aplicación ofrece.

**Por qué no lo cazó nada.** Todas las puertas de contraste de este repositorio leen **los cuatro
diccionarios**, y un diccionario es coherente consigo mismo: `ShellSurfaceBrush` es correcto como
fondo del shell y `PlayerTextBrush` es correcta como tinta del reproductor. Lo que estaba mal era el
**emparejamiento**, y un emparejamiento sólo existe en una vista. / **Why nothing caught it.** Every
contrast gate here reads the four dictionaries, and a dictionary is consistent with itself. What was
wrong was the **pairing**, and a pairing exists only in a view.

La corrección es la del prototipo, que pinta el mini entero sobre `#0B0D10`: la franja toma
`PlayerSurfaceBrush`, que es ese color en los cuatro temas. `MiniPlayerBandTests` mide el
emparejamiento —3:1 para los glifos, que es lo que WCAG pide de un objeto gráfico, y 4,5:1 para las
dos líneas de texto, que son palabras—. / The correction is the prototype's, which paints the whole
mini on `#0B0D10`: the band takes `PlayerSurfaceBrush`, that colour in all four themes.
`MiniPlayerBandTests` measures the pairing — 3:1 for the glyphs, which is what WCAG asks of a
graphical object, and 4.5:1 for the two lines of text, which are words.

## El ancho, en los dos idiomas / The width, in both languages

El aviso venía de lejos: **esos cinco plegaron en tres filas dentro de 480×270** el 2026-08-19, por
una palabra traducida. La franja deja de ser cinco glifos en cuanto un título y un reloj comparten su
línea, así que se mide en el mínimo que la ventana permite —320— y en los dos idiomas. / The warning
was old: **those five folded into three rows inside 480×270** on 2026-08-19, because of one
translated word. The band stops being five glyphs the moment a title and a clock share its line, so
it is measured at the window's own minimum — 320 — and in both languages.

```
es-ES: band=320  row=252x44  readout=36  filas/rows=1
en-US: band=320  row=252x44  readout=36  filas/rows=1
```

Los cinco toman 252 de los 296 que quedan tras los márgenes, más 8 de separación: al título y al
reloj les quedan **36 px**, que es una tira y por eso las dos líneas se recortan con puntos
suspensivos en vez de envolver. / The five take 252 of the 296 left after the margins, plus 8 of
separation: the title and the clock get **36 px**, which is a strip — and why both lines are trimmed
with an ellipsis instead of wrapping.

Las dos cifras son iguales en los dos idiomas, y eso es el resultado y no la premisa: los cinco son
glifos y las dos líneas son datos, no palabras de un diccionario. La prueba que fijaba `es-ES` pasó a
ser `[AvaloniaTheory]` con el idioma por parámetro, que es la misma corrección que las dos puertas de
ancho tomaron el 2026-08-26. / The two figures agree across languages, and that is the result rather
than the premise: the five are glyphs and the two lines are data, not dictionary words. The test that
pinned `es-ES` became an `[AvaloniaTheory]` with the language as a parameter — the same correction
the two width gates took on 2026-08-26.

## La barra, y por qué su pista no se va / The bar, and why its track never leaves

La barra sigue al reproductor y **está ausente hasta que el motor dice cuánto dura el archivo**, por
la razón que `DurationSeconds` lleva escrita: responde 1 mientras no se sabe, así que una posición de
cincuenta y dos minutos contra ese máximo se recorta y pinta una barra **llena** sobre una película
que acaba de empezar. / The bar follows the playhead and is **absent until the engine says how long
the file is**, for the reason `DurationSeconds` carries: it answers 1 until then, so a position of
fifty-two minutes against that maximum is clamped and paints a **full** bar over a film that has
barely started.

Lo que **no** se va es la pista: son tres píxeles que están desde el primer fotograma. La ventana
responde a un arrastre poniendo 16:9 sobre la imagen y **sumando la altura del cromo encima**, y ese
manejador sólo corre en un arrastre — una barra que apareciera al llegar la duración movería la
imagen debajo de una ventana que nadie ha tocado. Medido: la franja mide lo mismo antes y después. /
What does **not** leave is the track: three pixels there from the first frame. The window answers a
drag by putting 16:9 back on the picture and **adding the chrome's height on top**, and that handler
only runs on a drag — a bar that appeared when the duration arrived would move the picture under a
window nobody touched. Measured: the band is exactly as tall before and after.

Los tres setters —`Height`, `MinHeight` y `MinWidth`— son los mismos que la tarjeta de la biblioteca
y la fila de episodio ya necesitaron: el tema base da a un `ProgressBar` una altura mínima de cuatro
y una anchura mínima de 200. / The three setters — `Height`, `MinHeight`, `MinWidth` — are the ones
the library card and the episode row already needed: the base theme gives a `ProgressBar` a minimum
height of four and a minimum width of 200.

## El reloj es una cadena y no tres enlaces / The clock is one string, not three bindings

`PlaybackClock.Readout` compone `posición / duración · velocidad`. Los separadores son puntuación que
el prototipo dibuja y que ningún diccionario guarda, así que escribirlos en el marcado serían tres
`TextBlock` con dos más entre medias — y el del medio está vacío hasta que llega la duración. Esa
fila no se cierra cuando uno de sus miembros se queda en blanco: deja la puntuación colgando,
`0:12 /  · 1×`. La ausencia se responde una vez, en la composición. / `PlaybackClock.Readout`
composes `position / duration · speed`. The separators are punctuation the prototype draws and no
dictionary holds, so writing them into the markup means three `TextBlock`s with two more between them
— and the middle one is empty until the duration arrives. That row does not collapse when a member
goes blank: it leaves the punctuation stranded, `0:12 /  · 1×`. The absence is answered once, in the
composition.

## El orden de los cinco / The order of the five

Atrás, reproducir, adelante, ampliar, cerrar — el orden del prototipo, tomado aquí donde el
transporte grande no pudo tomarlo. Allí los tres de la sesión y los dos de los saltos vienen de
modelos construidos en momentos distintos, así que intercalarlos es mover comandos entre ellos; estos
cinco están en un panel y leen un modelo, así que el orden es del marcado. / Back, play, forward,
expand, close — the prototype's order, taken here where the large transport could not take it. There
the three of the session and the two of the skips come from models built at different moments, so
interleaving them means moving commands between them; these five sit in one panel and read one view
model, so the order is the markup's.

## Lo que queda medido / What stays measured

| Puerta / Gate | Qué afirma / What it asserts |
| --- | --- |
| `MiniPlayerBandTests` | El emparejamiento tinta/franja en los cuatro temas: 3:1 los glifos, 4,5:1 las palabras. / The ink-on-band pairing in all four themes: 3:1 glyphs, 4.5:1 words. |
| `TransportGlyphTests` | Los cinco comparten una línea a 320 **en los dos idiomas**, y nada de la franja se dibuja fuera de esos 320. / The five share a line at 320 **in both languages**, and nothing on the band is drawn past it. |
| `MiniPlayerChromeTests` | La barra mide 3 y su pie está sobre la fila; el título y el reloj son los del modelo; la barra sigue al reproductor y está ausente sin duración; la franja no cambia de altura. / The bar measures 3 and its foot is above the row; the title and clock come from the model; the bar follows the playhead and is absent without a duration; the band does not change height. |
| `ProgressWiringTests` | La línea compuesta con y sin duración, y que se **anuncia** al moverse el reproductor. / The composed line with and without a duration, and that it is **announced** when the playhead moves. |
