# Once botones dejaron de decir su nombre para decirlo mejor / Eleven buttons stopped saying their name in order to say it better

Séptimo trabajo del tramo 4 de la §4, **y el que encontró que un cambio dado por hecho no había
llegado a la vista que el documento nombra**. / §4's fourth tranche, and the one that found a change
recorded as done had never reached the view the document names by name.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Por qué glifos, que no es gusto / Why glyphs, which is not taste

El 2026-08-19 el cromo del mini reproductor **se plegó en tres filas dentro de 480×270** con cinco
botones que llevaban palabras traducidas, y la sonda del paseo murió con «is surrounded by other
command controls». Las palabras no caben en la ventana que el mini reproductor tiene permitido ser. /
On 2026-08-19 the mini player's chrome folded into three rows inside 480x270 and the walk's
beside-point probe died; the words do not fit in the window that chrome is allowed to be.

La §4 pide `Glifos de Segoe Fluent Icons, 44 px de área pulsable; el nombre accesible sigue viniendo
de la clave de recurso, no del glifo`. **Se cambia sólo el `Content`.** / Only `Content` moves.

## Lo que la medición encontró antes de escribir nada / What measuring found before anything was written

**`TransportControlsView` no llevaba la clase.** El 2026-08-21 `player-chrome` subió de 36 a 44 px y
el trabajo se anotó contra la fila de la §4 que dice `TransportControlsView`. Pero esos tres botones
—retroceder, avanzar, silenciar— **nunca han llevado esa clase**: la llevan los tres de `PlayerView` y
los cinco del mini reproductor. Medido: / Measured:

```
name=SkipBackwardButton classes=[:disabled] MinW=0 MinH=36 Content='Retroceder'
name=SkipForwardButton  classes=[:disabled] MinW=0 MinH=36 Content='Avanzar'
name=MuteButton         classes=[:disabled] MinW=0 MinH=36 Content='Silenciar'
```

**Un cambio se anota contra la vista que lo recibe, no contra la fila que lo pidió.** La clase subió,
la fila se tachó, y los tres botones que alguien pulsa para saltar y para callar el sonido se quedaron
en 36. Ahora la llevan, y con ella los 44. / A change is recorded against the view that receives it,
not against the row that asked for it.

## Los ocho puntos de código, medidos en las dos familias / The eight codepoints, measured in both families

Ninguno se eligió de memoria. Los ocho existen **en las dos** familias que el marcado declara, y en
ninguna de las de texto — que es la mitad que convierte la comprobación en una aserción y no en un
trámite: una fuente que contesta a todo no contesta a nada. / None was chosen from memory: all eight
resolve in both declared families and in none of the text ones, which is the half that makes the
lookup an assertion instead of a formality.

| Control | Punto / Codepoint | Segoe Fluent Icons | Segoe MDL2 Assets | Segoe UI |
|---|---|---|---|---|
| `TransportSkipBackward`, `MiniPlayerSkipBack` | `U+E72B` | 1404 | 132 | 0 |
| `TransportSkipForward`, `MiniPlayerSkipForward` | `U+E72A` | 1403 | 131 | 0 |
| `TransportToggleMute` | `U+E74F` | 1416 | 149 | 0 |
| `PlayerPlayAction`, `MiniPlayerPlayPause` | `U+E768` | 135 | 160 | 0 |
| `PlayerPauseAction` | `U+E769` | 136 | 161 | 0 |
| `PlayerStopAction` | `U+E71A` | 1399 | 121 | 0 |
| `MiniPlayerRestore` | `U+E73F` | 121 | 144 | 0 |
| `MiniPlayerClose` | `U+E8BB` | 277 | 350 | 0 |

Las dos familias indexan el mismo punto de código **en sitios distintos** —`U+E768` es el glifo 135 en
una y el 160 en la otra—, que es la señal de que lo que se leyó fueron dos fuentes y no dos veces la
misma. / The two families index the same codepoint at different places, which is how one can tell two
fonts were read rather than one read twice.

El glifo cero es `.notdef`, que es la caja vacía: preguntar por presencia sin excluir el cero aprueba
justo la fuente que no dibuja nada. **Las dos familias van declaradas**, no una: `Segoe Fluent Icons`
es de Windows 11, que es el único destino, y `Segoe MDL2 Assets` es su predecesora y está en todo
Windows desde el 10. / Glyph zero is `.notdef`; two families are declared so a host without the newer
one still draws the pictogram.

## Lo que no se movió, y se afirma / What did not move, and is asserted

`AutomationProperties.Name` sigue apuntando a su clave de recurso en los once. Es lo que el paseo
persigue, lo que lee un lector de pantalla y lo único que queda llevando la identidad del control una
vez que el texto visible es un pictograma. **Reescribir la clave habría renombrado once controles sin
que ningún diseño se viera mal.** / The accessible name still comes from the key in all eleven, which
is the only thing left carrying identity once the visible text is a pictogram.

Y una aserción heredada tuvo que reescribirse en vez de borrarse.
`MiniPlayerChromeAutomationTests` afirmaba `Content == AutomationProperties.Name`, que era cierto
mientras los dos salían de la misma clave. Ahora afirma **la mitad que no puede moverse**: el nombre
es la palabra que la clave guarda, el contenido es un punto de código de uso privado, y **los dos son
distintos**. / An inherited assertion was rewritten rather than deleted: it now asserts the half that
must not move.

## Y el defecto que resuelve, medido en la ventana más estrecha / And the defect it fixes, at the narrowest width

El mínimo de la ventana del mini reproductor es **320**, más estrecho que los 480 donde se plegó. Con
glifos, 5×44 + 4×8 = **252**, y los cinco comparten una sola fila. El `WrapPanel` se queda: un glifo
estrecha la fila, no demuestra que no pueda envolver nunca. / The window's own minimum is 320; the five
now share one line there. The wrapping panel stays.

## El verde / The green

```
UiTests             641/641   (las 4 nuevas incluidas / the 4 new ones included)
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
DocumentationTests  87/87
verify-docs.ps1     214 Markdown, 32 localizados / localised, 58 feature IDs, 46 MVP IDs
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
                     ningún control nuevo / no new control
```
