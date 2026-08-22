# El transporte pasa a ser una franja, y dice dónde vas / The transport becomes a band, and says where you are

Primer tramo del reproductor contra el prototipo: la superficie del pie, la barra de posición que el
modelo tenía y nadie pintaba, y un defecto que la propia prueba de esa barra encontró. / The player's
first tranche against the prototype, and a defect its own test found.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que el prototipo dibuja, medido en su código / What the prototype draws

`design/AP Reelume.dc.html`, líneas 1179-1223 y el estilo `pl.shell` de la 2557:

| Del prototipo | Valor | Hoy |
| --- | --- | --- |
| Superficie del reproductor | `#0B0D10`, de borde a borde bajo el carril | ya estaba, y ahora la franja también |
| Franja del transporte | ancho completo, `border-top` de 1 px, fondo del reproductor al 90 % | **hecha** |
| Fila de posición | transcurrido · barra · duración, con los relojes a 58 px de mínimo | **hecha** |
| Fila de mandos | una sola línea: saltos, silencio, volumen, cifra, velocidad | **hecha** |
| Orden `atrás · reproducir · adelante` | los tres juntos | **no**, y la razón está abajo |
| Cabecera con título, sesión y los cuatro paneles | sí | pendiente, es el tramo siguiente |
| Línea de atajos al pie | `Espacio reproduce · ← → salta…` | **decidido que no**: en esta aplicación los atajos se configuran en `ShortcutSettingsView`, así que una línea fija diría algo que puede ser falso |

## Lo que estaba y no se veía / What was there and could not be seen

`TransportControlsViewModel` llevaba **`Position`, `Duration` y `SeekAsync` desde siempre**, los tres
actualizados en cada cambio de estado, y **ninguna vista los leía**. Es el defecto que este
repositorio tiene bautizado, aplicado a lo más básico que un reproductor hace: quien veía una
película no podía ver por dónde iba, ni llevarla a otro minuto con el ratón. `SeekAsync` tenía
llamantes en el teclado y en los botones de salto, y ninguno en una barra, porque no había barra. /
The model held all three all along and no view read them.

Lo mismo, más pequeño, con el volumen: el pulgar se movía y **ningún número decía a qué nivel**, que
es justo donde más importa — por encima del 100 % entra el limitador, y el aviso de al lado explica
por qué.

Lo que **no** se inventa: la barra **está ausente**, no deshabilitada, mientras el motor no ha dicho
cuánto dura el archivo. Un cursor a mitad de una barra de longitud desconocida no señala nada, y una
barra en gris diría «no es para ti» donde la verdad es «todavía no». `DurationLabel` contesta vacío en
ese estado en vez de «0:00», que sería decir que la película no dura nada.

## El defecto que la prueba encontró, y que no llegó a publicarse / The defect the test found

Escrita la prueba, la primera medición volvió con la posición en **0:01** después de pedir dos
minutos. La causa no se deduce de leer el XAML:

**Un `Slider` de Avalonia recorta lo que se escribe en `Value` contra el `Maximum` que tiene en ese
instante.** `DurationSeconds` contesta `1` mientras no hay duración, y el bucle de notificación del
modelo anunciaba `PositionSeconds` **antes** que `DurationSeconds`. Así que los 120 segundos entraban
en una barra cuyo máximo seguía siendo 1, la barra los recortaba a 1, el enlace de una vía escribía
ese 1 de vuelta, y el manejador —que no distingue un recorte de una elección— lo convertía en un
salto de verdad. **La barra movía la película ella sola.** / The bar moved the film by itself.

Se corrige por los dos lados, a propósito:

1. **El orden de las notificaciones**: `HasDuration`, `DurationSeconds`, `DurationLabel` y después
   `PositionSeconds`, `PositionLabel`. La escala antes que el valor.
2. **Una guarda en el manejador**: si el `Maximum` del control no es la duración del medio, el cambio
   es un recorte y no una elección. Sin ella, cualquier camino futuro que escribiese el valor antes
   que la escala volvería a llevar la película de alguien al segundo uno.

El primero solo bastaría hoy. El segundo es el que sigue bastando mañana.

## Y una trampa de formato, cazada por afirmar el texto / A format trap, caught by asserting the text

`ToString("0 %")` **no** escribe «80 %»: escribe «8000 %». El `%` sin comillas en una cadena de
formato numérico es el **especificador** de porcentaje, y multiplica por cien. Lo cazó la prueba que
compara el texto; una que hubiese comparado `VolumePercent` habría pasado. / A test comparing the
number rather than the text would have passed.

## El paseo, y por qué la escena manda la sesión al final antes de pulsar / The walk

El primer intento falló con `a click reaches Border inside thumb inside PART_Track`: el paseo pulsa un
control de rango **a un cuarto de su ancho**, y después de los dos saltos la cabeza de reproducción
estaba justo en ese cuarto — así que el clic caía **sobre el propio pulgar**, que inicia un arrastre y
no cambia ningún valor. La escena manda la sesión al segundo 80 antes de pulsar, y eso es el arnés y
no la prueba. / The click landed on the thumb itself.

La sonda es la posición del motor y no el pulgar: una barra que moviese su propio pulgar dejando la
sesión donde estaba es exactamente el estado en el que vivió el deslizador de volumen durante meses.

## Y una decisión de dibujo que sale de una lista cerrada / A drawing decision that leaves a closed list

`TransportControlsSurface` era la tercera de las tres superficies que `PlayerViewDesignTests` exige
con esquina del tema. Deja de serlo porque **deja de ser una tarjeta**: era un panel flotante con 16
px de margen por los cuatro lados y la imagen se veía por debajo a izquierda y derecha. Una franja
llega a tres bordes de la ventana, y una esquina redondeada contra un borde recto es un hueco.

No sale de la lista en silencio: **se afirma que no tiene esquina y que sólo dibuja su borde
superior**, que es la mitad que si no se pudre. / It is asserted to have no corner rather than dropped
from the list quietly.

## Lo que no se hizo, y por qué / What was not done

El prototipo pone **atrás · reproducir · adelante** juntos. Aquí reproducir, pausar y detener quedan
después de los saltos, y es una medición y no un olvido: los tres son del **coordinador de sesión** y
los saltos son de **`ControlPlayback`**, dos modelos que se construyen en momentos distintos.
Intercalarlos no es mover botones dentro de un panel, es mover órdenes entre dos modelos — y eso es un
cambio de construcción que toca `CompositionRoot` y varias escenas, por el orden de dos botones. /
Interleaving them means moving commands between two models, not buttons within a panel.
