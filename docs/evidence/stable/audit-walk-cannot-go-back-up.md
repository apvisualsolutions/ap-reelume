# El paseo aprende a volver hacia arriba / The walk learns to go back up

La tanda 4 —los ajustes— es la primera página del paseo que **no cabe en la ventana**, y ahí se
descubrieron dos cosas que ninguna tanda anterior podía descubrir. La primera mitad de la tanda ya
está pulsada; la segunda espera. / Batch four is the first page that does not fit in the window, and
it turned up two things no earlier batch could.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 33 | **40** |
| Pendientes / Pending | 95 | **88** |

Siete controles: los tres temas, los dos idiomas, la vigilancia de raíces locales y la detección de
segmentos. Quedan trece de la tanda —ciclo de vida, privacidad, recomendaciones y atajos— para la
siguiente. / Seven controls; thirteen of the batch remain.

## Primero: el paseo sólo sabía bajar / First: the walk only knew how to go down

Los ajustes miden **3680 px de extensión en una ventana de 2000**. El arnés empujaba el
desplazamiento 120 px por pasada mientras buscaba un control y no lo devolvía, así que después de
pulsar tres botones el desplazamiento estaba en **444** y el siguiente control —más arriba en la
página— quedaba en **y = -102**, fuera por arriba. Bajar más lo alejaba. / Each press left the page
where its search stopped, so the next control higher up was out of reach.

La corrección es `Reveal`, y su regla es **desplazar lo mínimo, y preferir no desplazar**: la página
vuelve arriba, y sólo un control que aun así no cabe se busca desplazando. Es lo que hace una persona
y, sobre todo, hace que una pulsación no dependa de cuál se pulsó antes. / The page goes back to the
top, and only a control that still does not fit is scrolled to.

## Y después: aplicar un tema deja fuera de alcance lo que está por encima / And then: applying a theme puts what is above it out of reach

Con la página arriba del todo y el botón dentro de la ventana, **ocho pulsaciones seguidas en el
mismo punto no llegaron**. La causa se aisló midiendo el mismo punto en tres momentos: / With the page
at the top and the button inside the window, eight presses in a row did not arrive:

| Momento / When | ¿El clic alcanza el botón? / Does the click reach it? |
|---|---|
| Recién abierta / Freshly opened | sí / yes |
| Tras un cambio de tema / After one theme change | sí / yes |
| Tras los tres / After all three | **no** |

Aplicar un tema reconstruye los recursos con los que se dibuja la página, y después de eso un clic en
la posición que da la disposición **ya no alcanza** un control situado por encima del que se acaba de
pulsar. La regla que sale de la medición es de orden: **los temas se pulsan los últimos**. Con los
idiomas antes que los temas, la escena pasa entera a la primera. / Applying a theme rebuilds the
resources the page is drawn from; the rule is that themes are pressed last.

Esto **matiza la nota del 2026-08-15** sobre `InputHitTest`. Sigue sin predecir a dónde va un clic —
en este mismo caso contestó `under=True` y `under=False` en el mismo punto y el mismo desplazamiento,
en dos momentos distintos—, así que no puede decidir si un clic vale. Sólo la sonda del efecto puede.
/ Hit testing still cannot decide whether a click is allowed; only the effect probe can.

## Lo demás que queda en el arnés / What else the harness keeps

- **Reintenta.** Una pulsación que no cambia nada se repite hasta ocho veces recolocando antes,
  porque es lo que hace una persona cuyo clic falla. Una que sí cambia algo **no se repite nunca**.
- **Dice dónde fue el clic.** La queja nombra el número de pulsaciones, el punto y la cadena de
  controles bajo él; la de fuera de ventana nombra desplazamiento, visor y extensión de cada visor
  entre el control y la ventana. Antes eran sesenta segundos de silencio. / The complaint names the
  presses, the point, and the chain under it.

## Lo que se probó y no era / What was tried and was not it

Paso bidireccional, paso fino de 20 px, barrido de 48 pasadas, `BringIntoView` sólo en la primera
pasada, actualizar la disposición antes de pedirlo, forzar un fotograma con `CaptureRenderedFrame`, y
volver arriba antes de cada pulsación. Ninguno bastaba solo. Y una sonda desechable con un
`ScrollViewer` desnudo en una ventana desnuda **se comportó correctamente** en los tres
desplazamientos probados, que es lo que descartó a Avalonia como culpable y devolvió la búsqueda al
shell ensamblado. / A bare ScrollViewer in a bare window behaved correctly, which ruled Avalonia out.
