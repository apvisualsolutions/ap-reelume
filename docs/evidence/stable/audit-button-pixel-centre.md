# El píxel contra la caja / The pixel against the box

Los botones de esta aplicación dibujaban su icono y su palabra **2 px separados**, con dos puertas
verdes encima. Esta es la medición que lo encontró y la que ahora lo vigila. / The buttons drew their
icon and their word **2 px apart**, with two gates green over it. This is the measurement that found
it and the one that watches it now.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-28.

## Lo que estaba mal, en píxeles / What was wrong, in pixels

Rasterizando un botón real con `window.CaptureRenderedFrame()` —que aquí funciona porque
`TestAppBuilder` levanta Skia de verdad con `UseHeadlessDrawing = false`—, un icono de 12 px junto a
una palabra: / Rasterising a real button with `window.CaptureRenderedFrame()` — which works here
because `TestAppBuilder` brings up real Skia with `UseHeadlessDrawing = false` — a 12 px icon beside
a word:

```
                    icono/icon   palabra/word   botón/button   icono↔palabra   palabra↔botón
margen/margin 5        39,5          37,5           39,5            +2,0            -2,0
sin/without            40,5          40,5           39,5             0,0            +1,0
margen/margin 2        38,5          38,5           39,5             0,0            -1,0
margen/margin 1        39,5          39,5           39,5             0,0             0,0
```

**El error real era 1 px y la compensación movía 3.** Los cinco píxeles venían de las métricas de la
fuente —2,43 px de asimetría entre ascendente y descendente, doblados porque un margen a un lado
mueve una caja centrada la mitad—. En pantalla la palabra pasaba de estar 1 px baja a estar 2 px
alta. / **The real error was 1 px and the compensation moved 3.** The five came from the font's
metrics — a 2.43 px asymmetry, doubled because a margin on one side moves a centred box by half of
it. On screen the word went from 1 px low to 2 px high.

**Y movía lo que no era.** Un margen sobre la etiqueta hace crecer el panel donde la etiqueta vive,
así que en los **53 botones** que llevan icono al lado de la palabra el icono se desplazaba también —
y el icono es geometría y ya estaba centrado al píxel. / **And it moved the wrong thing.** A margin
on the label grows the panel it sits in, so across the **53 buttons** carrying an icon beside a word
the icon moved too — and the icon is geometry, already centred to the pixel.

La corrección va sobre el **contenido del botón**: la etiqueta cuando es lo único que hay, el panel
cuando hay un icono al lado. Ahí mueve todo lo que el botón dibuja por igual y no puede separar dos
cosas que van juntas. / The correction goes on the **button's content**: the label when that is all
there is, the panel when there is an icon beside it.

## El verde, con y sin descendente y en los dos idiomas / The green, with and without a descender

```
es-ES  «Guardar»           icono 39,5   palabra 39,5   botón 39,5    0,0 / 0,0
es-ES  «Reproducir»        icono 39,5   palabra 39,5   botón 39,5    0,0 / 0,0
es-ES  «Añadir medios…»    icono 39,5   palabra 39,5   botón 39,5    0,0 / 0,0
en-US  «Save the report»   icono 39,5   palabra 39,5   botón 39,5    0,0 / 0,0
```

Las palabras son **parámetros** y una lleva descendente, porque el centro de la tinta no es una
propiedad de la fuente sino de la cadena. Medido en Inter a 14 px, respecto al centro de la caja de
línea: / The words are **parameters** and one carries a descender, because the middle of the ink is a
property of the string and not of the font. Measured in Inter at 14 px, against the middle of the
line box:

```
'Guardar el informe'  +0,62      'Reproducir'       +2,23
'Guardar'             +0,70      'Play'             +2,26
'Añadir medios…'      +0,70      'Ap'               +2,51
'Save' / 'MMM'        +0,90      'ppp'              +3,82
```

Un rango de **3,2 px** que abre y cierra una sola letra, y que se mueve al traducir. Una puerta que
fijara una palabra certificaría una compensación calibrada para esa palabra. / A **3.2 px** range
that a single letter opens and closes, and that moves with translation.

## Por qué ninguna de las dos puertas lo veía / Why neither gate saw it

**`ButtonInkTests`** mide la caja de la etiqueta. **`ButtonOpticalCentreTests`** medía la tinta
calculada desde las métricas de la fuente. Ninguna dibujaba nada, y el defecto vivía sólo en lo
dibujado. / Neither rendered anything, and the defect lived only in what was rendered.

**Y la segunda tenía un fallo de método demostrable**: calculaba el pie de la tinta asumiendo
**siempre** un descendente. Sobre «Guardar el informe», que no tiene ninguno, contestaba 2,43 px de
separación donde el rasterizado mide 0,0. No se aflojó su umbral —eso sería convertirla en ciega—:
**se retira y la sustituye una medición estrictamente mejor**, y sus tres afirmaciones (la palabra
centrada en el botón, el icono centrado en el botón, y los dos en el mismo medio) están las tres en
`ButtonPixelCentreTests`. / **And the second had a demonstrable flaw of method**: it computed the
foot of the ink assuming a descender **always**. Over «Guardar el informe», which has none, it
answered 2.43 px where rasterising measures 0.0. Its threshold was not loosened — that would make it
blind — it is **retired and replaced by a strictly better measurement**, with all three of its
assertions carried over.

## Tres hipótesis medidas y descartadas / Three hypotheses measured and dismissed

1. **`TextOptions` no interviene.** Los cuatro modos —por defecto, `BaselinePixelAlignment
   Unaligned`, `TextHintingMode None`, y ambos— dan **el mismo píxel**. / **`TextOptions` plays no
   part.** All four modes give **the same pixel**.
2. **Los iconos no eran el problema y una fuente de iconos lo empeoraría.** El icono medía `+0,00`
   contra el centro del botón. Es geometría, libre de la asimetría ascendente/descendente que la
   tabla de arriba muestra; como fuente la heredaría. / **The icons were not the problem.**
3. **«Centrar la tinta» no es una regla**, es una regla distinta por palabra — ver la tabla. Lo
   estable es la métrica de la fuente, y lo que decide es el píxel. / **"Centre the ink" is not one
   rule**, it is a different rule per word.

## La regla que deja / The rule it leaves

**Medir el layout no es medir lo que se ve.** `Bounds`, `TranslatePoint` y las métricas de una fuente
describen el modelo; cuando lo que se investiga es algo que alguien **ve**, se rasteriza y se cuentan
píxeles. Es la forma nueva del defecto de esta casa: no un servicio que nadie resuelve, sino **una
puerta que mide el modelo de lo que promete mirar**. / **Measuring layout is not measuring what is
seen.** It is the new shape of this repository's characteristic defect: not a service nobody
resolves, but **a gate measuring the model of the thing it promises to watch**.
