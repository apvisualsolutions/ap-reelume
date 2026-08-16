# El paseo no sabe volver hacia arriba / The walk cannot go back up

La tanda 4 —los ajustes— **no se pudo escribir**, y el motivo no es del producto: el arnés del paseo
no puede pulsar un control que quede **por encima** del área visible después de haber pulsado otro más
abajo. Queda medido aquí para que la próxima sesión empiece por la causa y no por el síntoma. / Batch
four could not be written, and the reason is the harness, not the product.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que se intentó / What was attempted

Veinte controles en siete vistas —apariencia, ciclo de vida, privacidad, escaneo, recomendaciones,
detección de segmentos y atajos—, todos en la **misma página desplazable**, que es la diferencia con
las tres tandas anteriores: en ellas cada superficie cabía en la ventana. / Twenty controls on one
scrolling page, which is what the earlier batches never had.

## Lo que se midió / What was measured

Con la ventana a 1600×2000, el visor de ajustes tiene un **extent de 3680**, así que la mitad de la
página está siempre fuera. Y entonces: / Half the page is always off-screen, and then:

1. **Cada pulsación deja la página donde la dejó.** El arnés empuja el desplazamiento 120 px por
   pasada mientras busca el control, y no lo devuelve. Tras los tres botones de tema el
   desplazamiento estaba en **444**. / Each press leaves the page where it stopped.
2. **El empuje sólo sabía bajar.** El botón de inglés, que está *arriba*, quedó en **y = -102** —
   fuera de la ventana por arriba—, y bajar más lo aleja. / The nudge only knew how to go down.
3. **`BringIntoView` lo devuelve.** Llamado en cada pasada, volvía a poner el control en y = -102 con
   el desplazamiento en 444, deshaciendo cada corrección: veinticuatro pasadas terminaban exactamente
   donde la primera. / Called every pass, it put the control back where it was.
4. **Y donde la disposición dice que está el control, el clic no llega.** Con el desplazamiento en 0
   el botón estaba en y = 342 según `TranslatePoint`, y un manejador sobre `Button.ClickEvent` midió
   que **ningún botón recibía el clic**: la cadena bajo el punto era el presentador del visor. Con el
   desplazamiento en 106 el mismo botón sí lo recibía. / Where the layout says the control is, the
   click does not arrive: a handler on `Button.ClickEvent` measured that no button received it.

El punto 4 **corrige a medias la nota del 2026-08-15**, que decía que `InputHitTest` no predice a
dónde va un clic. Sigue sin predecir que uno **sí** llegue; pero cuando la cadena bajo el punto es el
presentador del visor, se midió que **no llega ninguno**. / It half-corrects the earlier note: hit
testing still does not predict a click arriving, but when the chain is the viewer's presenter,
nothing arrives.

Lo que se probó y **no** resolvió: paso bidireccional, paso fino de 20 px, barrido de 48 pasadas,
`BringIntoView` sólo en la primera pasada, actualizar la disposición antes de pedirlo, forzar un
fotograma con `CaptureRenderedFrame`, y volver al principio de la página antes de cada pulsación.
Ninguno hizo que el clic llegara. / None of these made the click arrive.

## Lo que sí queda / What does remain

El arnés ahora **dice dónde fue el clic**. Antes, una pulsación que no llegaba se manifestaba como
sesenta segundos de espera y «el efecto nunca llegó», que no señala nada; ahora la queja nombra el
punto y la cadena de controles bajo él, y la de fuera de ventana nombra el desplazamiento, el visor y
la extensión de cada visor entre el control y la ventana. Es diagnóstico y no aserción, justamente
porque el impacto no puede decidir si un clic vale. / The harness now names where the press went.

## Qué lo desbloquea / What unblocks it

Averiguar por qué la posición que `TranslatePoint` da y la que el impacto usa **discrepan en unos cien
píxeles** dentro de un `ScrollViewer` desplazado, en Avalonia headless 12.1.1. Hasta entonces la tanda
4 no puede escribirse con honestidad: un clic que no llega registrado como control cubierto sería
exactamente lo que este paseo existe para impedir. / Until that is understood, batch four cannot be
written honestly.
