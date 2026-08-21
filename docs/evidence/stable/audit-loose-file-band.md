# El aviso de la sesión suelta dejó de estar encima de la película, y sus 48 px se rechazan con su número / The loose-session notice stopped sitting on the film, and its 48 px is refused with a number

Noveno trabajo del tramo 4 de la §4 y **el que lo cierra de verdad**. La fila que quedaba pedía dos
cosas: **una es correcta y se hace; la otra está escrita sin haber podido ver el control y se rechaza
con su medición.** / §4's fourth tranche, and the piece that actually closes it: of the two things the
remaining row asked for, one is right and one was written blind.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## ⚠ Y estuvo a punto de darse por cerrado sin mirarlo / And it was nearly written off unlooked at

La sección de estado que abría el día decía que al tramo 4 le quedaban **dos** piezas y no nombraba
esta. Pero **su viñeta nunca se tachó**. Al medirla: el banner **sí** estaba superpuesto al vídeo. /
The day's opening state said two pieces remained and did not name this one — but its bullet had never
been struck through, and measuring found the banner was indeed drawn over the video.

**Lo que cierra un tramo es su lista de vistas, no la frase que lo resume.** Es la misma forma que el
2026-08-20 declaró cerrado el paso 6 contra un resumen propio en vez de contra el documento que lo
define. / A summary of one's own always confirms what one believes one has done.

## La mitad que se rechaza, con su número / The half that is refused, with its number

La §4 pide `Banda superior de 48 px, no superpuesta al vídeo, con la acción a la derecha en WrapPanel`,
y la propia fila se marca **«Bloqueado: el defecto medido el 17-08 impide que llegue a pantalla, así
que no puedo verificarlo»**. Está escrita sin haber podido ver el control. / The row marks itself
blocked and admits it could not be verified.

Medido el 2026-08-21, lo que el banner pide de alto: / Measured, what the banner asks for:

```
solo, a 1280 de ancho:  660 × 286   (su superficie dentro de la ventana)
solo, a  900 de ancho:  692 × 318
solo, a  480 de ancho:  480 × 336
```

No es un aviso de una línea: lleva **encabezado, el nombre del archivo, una explicación que envuelve,
la acción, y un panel de confirmación con su propia explicación y dos botones más**. En 48 px cabe el
encabezado y se cae todo lo que hace que el aviso signifique algo. **Quinta discrepancia §4↔árbol, y
manda el árbol.** / Forty-eight keeps the heading and drops everything that makes the notice mean
anything.

## La mitad que se hace, y una segunda razón que el documento no podía ver / The half that is done, and a second reason the document could not see

`LooseFileBanner` vivía dentro del `Panel` de superpuestos de `ShellView`, sobre el escenario. Medido:
/ Measured:

```
stage bounds = 0,0,960,800
banner insideStage = True   surface = 150,16,660,286
```

Sale a una **fila propia encima del escenario**. Y la segunda razón: **`PlayerStage` no es sólo la
imagen** — es el control que el shell entrega a la ventana del mini reproductor y recupera
(`window.Host(stage)`). Lo que esté dentro **viaja**, y este banner pide **336 px de alto a los 480 del
mini reproductor, en una ventana de 270**. / Whatever is inside the stage travels, and this banner asks
for 336 px of height in a 270-tall window.

**Y no puede ir dentro de `PlayerHost` tampoco**, que es la trampa en la que esta casa ya ha caído: al
volver del modo mini, `ShellView.axaml.cs` ejecuta `host.Content = stage`, así que **cualquier cosa
declarada al lado del escenario dentro de ese host la sustituye el escenario en el primer regreso y no
vuelve nunca**. Un árbol declarado en marcado que otro sustituye al llegar es el defecto de la casa.
Va **hermano** de `PlayerHost`, y la prueba afirma las dos ausencias. / It goes beside `PlayerHost`,
and the test asserts both absences.

## Y dos guardas se fueron con la mudanza, con su razón / And two guards left with the move

`MaxHeight="320"` **estaba recortando**: el banner pide 336 a 480 px de ancho y 318 a 900, así que el
tope cortaba la confirmación justo donde una ventana estrecha la necesitaba. Estaba para impedir que el
control se estirara a un escenario de 1280×1400 —el defecto del 2026-08-17— y **la fila `Auto` en la
que vive ahora acota la altura por lo que el contenido pide**. `MaxWidth="720"` se fue porque una banda
mide lo que mide aquello sobre lo que está; la explicación conserva su propio `MaxWidth`. /
`MaxHeight` was clipping; the `Auto` row bounds the height by content now.

**`VerticalAlignment="Top"` se queda**, y es la guarda que sigue haciendo algo: montado a solas —que es
lo que hace la puerta de desbordamiento— `Stretch` se llevaría el alto entero de la ventana. / The one
guard that still does something.

## Lo que la prueba afirma, y por qué son dos y no una / What the test asserts, and why it is two and not one

**Estructura y geometría dicen cosas distintas.** Un control puede ser hermano del escenario y aun así
pintarse encima de él, así que además de las dos ausencias se afirma que **su borde inferior queda a la
altura o por encima del borde superior del escenario**, en coordenadas del shell. Y se afirma que su
alto es mayor que cero: una geometría medida sobre un control que no midió nada aprueba sin comprobar
nada. / Structure and geometry say different things, and a measured geometry over a control that
measured nothing proves nothing.

## El verde / The green

```
UiTests             657/657
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
ArchitectureTests   30/30
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```

Las 135 de accesibilidad importan aquí más que en cualquier otro cambio del tramo: **el paseo entra y
sale del modo mini**, así que si la mudanza hubiera roto la entrega del escenario, lo habría dicho. /
The walk enters and leaves mini mode, so a broken hand-off would have said so.
