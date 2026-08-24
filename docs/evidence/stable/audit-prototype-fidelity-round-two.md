# Tres diferencias con el prototipo que el propietario vio y ninguna puerta miraba / Three differences from the prototype the owner saw and no gate was watching

El propietario miró la aplicación construida y dijo tres cosas concretas: los iconos del reproductor
no son los del prototipo, el texto de los botones no está centrado verticalmente, y faltan los fondos
rayados. Las tres eran ciertas y las tres estaban medidas en menos de una hora. / The owner looked at
the built application and named three things: the player's icons are not the prototype's, the buttons'
text is not vertically centred, and the striped backgrounds are missing. All three were true, and all
three were measured within the hour.

Fecha / Date: 2026-08-24. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## 1. El texto de los botones / The buttons' text

**Medición.** Un botón de 44 px, con los estilos de este árbol, montado y leído: la caja de la
etiqueta salió de **42 px** —el botón menos su borde— y el texto se dibuja **arriba** de esa caja. En
la píldora de 36 px eso deja las palabras unos siete píxeles por encima del centro. / **Measured.** A
44 px button under this tree's styles holds a 42 px label box — the button minus its border — and the
text draws at the top of it.

**Causa.** `ContentControl.VerticalContentAlignment` empieza en `Stretch`. El estilo
`Button, ToggleButton` de este repositorio fijaba alto, radio y relleno, y no tocaba la alineación. Un
`TextBlock` estirado ocupa toda la caja y pinta su línea arriba. / **Cause.** The alignment default is
`Stretch`, and this tree's button style never set it.

**Corrección.** `HorizontalContentAlignment` y `VerticalContentAlignment` a `Center` en el estilo
compartido, que es la misma línea que el prototipo escribe en cada botón
(`display:inline-flex; align-items:center; justify-content:center`). Las dos clases del riel y la
tarjeta de póster declaran la suya y la conservan. / **Fix.** Both alignments centred in the shared
style, which is the prototype's own line of CSS. The rail's two classes and the poster card declare
their own and keep it.

**Puerta.** `ButtonInkTests`, en tres clases de botón: los huecos de arriba y de abajo se diferencian
como mucho en un píxel —uno, porque 25 píxeles no se reparten en dos mitades iguales—, **y** cada
hueco mide al menos cuatro. La segunda mitad no es adorno: una etiqueta estirada está centrada
trivialmente, con un píxel a cada lado, mientras el texto va arriba. Sin ella la puerta pasaba con el
defecto puesto. / **Gate.** Equal gaps within a pixel **and** each gap at least four: a stretched
label is trivially centred, so comparing the gaps alone would have passed with the defect in place.

## 2. La trama de las portadas / The covers' hatch

**Medición.** `art(h, v)` del prototipo devuelve **cuatro** fondos: `[str, ring, glow, base]`. Este
árbol construía `base` y `glow`. El que faltaba es
`repeating-linear-gradient(115deg, rgba(255,255,255,.055) 0 2px, transparent 2px 10px)`. / **Measured.**
The prototype's cover is four backgrounds; this tree built two of them.

**Corrección.** Avalonia no tiene degradado repetido, y no le hace falta: un `LinearGradientBrush` con
`SpreadMethod="Repeat"` repite su propio vector. El vector mide diez píxeles en el ángulo del
prototipo —115° en sentido horario desde el norte es (sen 115, −cos 115) = (0,906, 0,423), o sea
(9,06, 4,23)— y dos paradas duras a un quinto de él pintan los dos primeros píxeles de cada diez.
Puntos absolutos y no porcentajes, o la trama sería un dibujo distinto en cada tarjeta. / **Fix.** A
repeating linear gradient over a ten-pixel vector at the prototype's angle, in absolute points.

**Puerta.** Ninguna vista pinta el halo sin pintar la trama, contado por archivo. Cuenta la
**referencia** (`{DynamicResource PosterHatchBrush}`) y no el nombre, porque el diccionario que la
declara no pinta portada ninguna. / **Gate.** No view paints the glow without the hatch, counting the
reference rather than the name.

## 3. Los iconos / The icons

**Medición.** El prototipo dibuja **treinta y cinco** pictogramas con una sola función: SVG de 24×24,
`fill:none`, `stroke:currentColor`, `stroke-width:1.6`, extremos y uniones redondos. La aplicación
pintaba glifos de **Segoe Fluent Icons** en **veintisiete** sitios de nueve archivos. Un glifo sólido
y un dibujo de línea son dos alfabetos distintos, y eso es lo que se ve. / **Measured.** Thirty-five
stroked SVG line drawings there; twenty-seven solid Segoe glyphs here, across nine files.

**Y esto se desvía del paquete de diseño, que hay que decirlo.** La Propuesta y el README del paquete
prescriben la fuente: «`FontFamilyIcons` · Segoe Fluent Icons · los glifos de estado y el cromo del
reproductor», y «los iconos son glifos de Segoe Fluent Icons». Es una decisión de **traducción** —el
prototipo es HTML, donde un SVG no cuesta nada, y la traducción eligió la fuente que trae Windows—.
El propietario ha mirado el resultado y ha dicho que no se parece. La regla que esa línea protege es
la de **no descargar nada**, y portar las formas al repositorio la respeta entera. / **This departs
from the design package, and that is worth stating.** The Proposal and the package README both
prescribe the system icon font. That was a translation decision; the owner has looked at the result
and said it does not match. The rule the line protects — download nothing — is kept intact.

**Corrección.** Las formas se convierten, no se redibujan: los `path` van literales y los `rect` y
`circle` del prototipo pasan a los arcos que los dibujan. Veintidós geometrías en `Theme/Icons.axaml`,
fusionado en el diccionario de `DesignTokens.axaml` —que es lo que cargan la aplicación **y las cuatro
aplicaciones de prueba**, así que ningún icono existe en el producto y falta en las puertas—. Dos
formas son de esta aplicación y lo dicen: `Stop`, porque su transporte tiene una parada donde el
prototipo tiene un solo botón que alterna, y `ChevronUp`, que es el de abajo del revés. / **Fix.**
Twenty-two geometries converted from the prototype, merged into the dictionary every test application
already loads. Two shapes are this application's own and say so.

**El peso no es un número.** Avalonia escala la geometría al tamaño del control y **estampa el trazo
en las coordenadas del control**, así que un único 1,6 dibujaría un icono de 20 con el peso de uno de
24. Cada clase de tamaño lleva el suyo: 1,6 × tamaño / 24. / **The weight is not one number.**
Avalonia scales the geometry and not the pen, so each size class carries 1.6 × size / 24.

**La tinta se hereda.** Medido: un `Path` sí toma `TextElement.Foreground` del botón que lo contiene,
así que un icono sigue a su control por encima, pulsado y deshabilitado igual que hacía el glifo. Sin
eso, un icono blanco se quedaría blanco sobre la píldora clara de una acción primaria. / **The ink is
inherited**, measured, which is what keeps an icon following its control's states.

**Y un defecto de paso:** el botón de silencio dibujaba el altavoz **tachado** estuviera callado o
sonando. El prototipo cambia de forma; ahora éste también. / **And a defect on the way:** the mute
button drew the crossed speaker in both states.

## Lo que sigue sin parecerse, medido y sin arreglar todavía / What still does not match, measured and not yet fixed

Comparando la captura del prototipo (`proto-library-dark.png`) con la de la aplicación: / Comparing
the prototype's library capture with the application's:

| Diferencia | Prototipo | Aplicación |
| --- | --- | --- |
| Distintivo de tipo en la portada | «Película» / «Serie», arriba a la izquierda | no existe |
| Línea de datos de la tarjeta | año · duración · género | año |
| Línea de estado de la tarjeta | «Sin empezar» / «En curso» / «10/16 episodios» | no existe |
| Visto | marca de verificación arriba a la derecha | no existe |
| No disponible | dentro de la portada, abajo | debajo de la tarjeta |
| Iniciales | no las hay: sólo el aro | dos letras, decisión propia y documentada |
| Filtros | píldoras sin punto | píldoras con círculo de radio |
| Insignia de revisión en el riel | número sobre el icono | no existe |

Ninguna de estas ocho está tocada en esta tanda. / None of these eight is touched in this batch.
