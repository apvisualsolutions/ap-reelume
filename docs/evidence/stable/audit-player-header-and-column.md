# La sesión se pone cabecera, y devuelve lo que no usa / The session heads itself, and gives back what it does not use

Segundo tramo del reproductor contra el prototipo: dónde empieza la superficie, quién encabeza la
sesión, y una columna de 320 px que estaba siempre. / The player's second tranche.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Tres diferencias con el prototipo, y las tres eran de composición / Three differences

| | Prototipo | Antes | Ahora |
| --- | --- | --- | --- |
| Dónde empieza la sesión | `left: 64` — el carril se queda | las dos columnas: el carril desaparecía | `Grid.Column="1"` |
| Los tres botones de sesión | pictogramas en una franja sobre la imagen | palabras encabezando la columna de paneles | pictogramas en `PlayerHeaderSurface` |
| La columna lateral | aparece con un panel | 320 px siempre | `Auto` + `IsVisible` |

**Abrir una película se llevaba por delante los cinco destinos.** `PlayerSurface` ocupaba
`Grid.ColumnSpan="2"`, así que la superficie del reproductor cubría el carril entero. Nada de una
sesión significa que alguien haya dejado de poder llegar a Ajustes, y el prototipo lo dice con su
propia geometría: su reproductor es `left: 64`, no `left: 0`. / Nothing about a session means somebody
has stopped being able to reach Settings.

**La columna de 320 px estaba existiera o no alguno de sus cinco paneles.** Un archivo con una sola
pista de audio, sin marcadores y sin otra versión dejaba un rectángulo vacío ocupando un quinto del
ancho de la imagen. `HasPlayerPanels` es la unión de los cinco, la columna pasa a `Auto` y la anchura
vuelve a la película. / An empty rectangle taking a fifth of the picture's width.

**Y los tres botones cambian sólo su `Content`.** `PlayerCloseAction`, `PlayerMiniModeAction` y
`PlayerFullscreenAction` conservan su clave de recurso como nombre accesible, así que un lector de
pantalla sigue diciendo «Cerrar el reproductor» y **el paseo sigue pulsando las mismas tres
identidades**. Es exactamente el movimiento que hizo el carril de navegación. Los glifos son `E8BB`,
`E73F` y `E740`, y **están comprobados dibujados** en un render del shell con sesión abierta, no
supuestos: un glifo que la fuente no tiene pinta una caja.

## Lo que la cabecera NO lleva, y está medido / What the header does not carry

El prototipo pone en el centro de esa franja **el título de lo que se reproduce, su subtítulo y el
distintivo de sesión**. Aquí no, y no por esfuerzo: **`PlayerSurfaces` no lleva el título del medio y
`PlayerViewModel` sólo tiene `MediaPath`**. Pintar la ruta del archivo de alguien como encabezado es
lo contrario de lo que hace esta aplicación —ni rutas de una máquina concreta, ni nombres de la
biblioteca de nadie— y además `RepositoryPrivacyTests` existe justo por eso. Es la misma omisión
declarada que la ficha de recomendación hace con el año que no conoce. / Painting somebody's file path
as a heading is the opposite of what this application is for.

Tampoco lleva los cuatro botones de panel —Audio, Subtítulos, Vídeo, Marcadores—, porque el panel
conmutable es el tramo siguiente: hoy las cinco vistas de la columna se montan todas a la vez cuando
sus modelos existen.

## Cómo se afirma, y por qué por geometría / How it is asserted

`ShellPlayerWindowTests` mide **dónde empieza la superficie de la sesión en coordenadas de ventana** y
exige 64. Buscar el carril en el árbol no habría servido: **un destino que está en el árbol detrás de
una superficie opaca es un destino que nadie puede pulsar**, y sólo la geometría distingue una cosa de
la otra. / Only the geometry tells one from the other.

Los tres botones se afirman **por el nombre que un lector oye**, en el idioma en vigor, y no por su
glifo — el glifo es donde cae el ojo y el nombre es lo que se oye, y son dos garantías distintas.

Y la columna se afirma en los dos sentidos: `HasPlayerPanels` es falso y su anchura pintada es **0**.
Sólo lo primero dejaría pasar una columna invisible que siguiera reservando sus 320 px.

## Un token más, y la misma regla de alto contraste / One more token

`PlayerPanelBrush` — `#111820`, el del prototipo, en Light y Dark; **igual a la superficie del
reproductor en los dos altos contrastes**, porque una superficie casi idéntica a la de al lado es una
superficie cuyo borde no se encuentra, y ahí quien separa es el hairline. Los tres tokens del
reproductor están en la lista cerrada de `ContrastTokenTests`, que exige que **los cuatro diccionarios
lleven todas las claves**: un token en tres de cuatro es un tema que cae en silencio a lo que dibuje
Fluent.
