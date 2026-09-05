# La franja de avisos, y el aviso que ahogó el hilo de la interfaz / The Notices Strip, and the Announcement That Drowned the Interface Thread

- IDs: `LIB-002`, `LIB-010`, `PRD-006`
- Fecha / Date: 2026-09-05
- Alcance / Scope: `Application/Events`, `Application/Discovery/ScanCoordinator`,
  `Presentation/Library`, `Presentation/Theme/DesignTokens`, `AccessibilityTests/EndToEnd`
- Decisión que manda / Governing decision:
  [ADR-0010](../../adr/0010-a-state-takes-space-and-an-event-floats.md)

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Lo que faltaba, y no era «un botón»

`LIB-002` está `VERIFIED` y promete que «los escaneos pueden cancelarse». El modelo tenía
`CanCancel`, un `Cancel()` completo y su origen de cancelación; la fila de progreso dibujaba el punto,
la frase y el recuento, **y ningún botón**. La auditoría del 2026-09-04 lo llamó «cancelable por
dentro y no por fuera».

Pero al medirlo aparecieron dos cosas más que nadie había escrito:

- **Un escaneo que arranca solo no dibujaba nada.** `IsRunning` sólo lo encendía `Begin`, y a `Begin`
  sólo lo llama la ruta que lanza una persona. Un escaneo de arranque o del vigilante publicaba su
  progreso a una superficie que se quedaba invisible. La «marca discreta» que pide el ADR **no tenía
  quién la encendiera**.
- **«Quién lanzó el escaneo» no llegaba a la pantalla.** El dato existe con sus cinco valores desde
  que existe el escaneo, y el aviso de progreso no lo llevaba. Sin él, la superficie no podía
  distinguir los dos casos y no dibujaba ninguno.

### Lo que dice el prototipo, medido y no supuesto

| Lo que dibuja | Medido en |
| --- | --- |
| Cuatro avisos apilados, cada uno con tono, icono, título, cuerpo y acción | `banner(tone, ic, …)` |
| 12 px de hueco, 12 por 14 de relleno, borde de 1 px del tono, radio **10** | el estilo de `banner` |
| «Escaneo incremental en curso» con **Cancelar** | `flags.scanning` |
| «Raíz desconectada: …» en tono de aviso, y «Acceso denegado: …» en tono de error | `flags.usb`, `flags.denied` |

El radio de 10 no tiene token, y por eso la clase lo escribe: es el mismo número que `Border.row-box`
ya escribía, y la puerta de esquinas lo empareja con `banner` en este mismo cambio.

### La barra de progreso se probó y se rechazó, con un número

El prototipo dibuja una barra al 34 %. **Aquí sería inventada**: el escaneo no conoce su total hasta
haber enumerado todo. Se probó una barra indeterminada y la puerta de desbordes la rechazó:

```
LibraryView: IndeterminateProgressBarIndicator2 ends at x=-75 in a 900-wide window
```

La plantilla de Avalonia anima su indicador **entrando desde fuera del control**. En su lugar va el
punto pulsante que esta casa ya usa para decir que un escaneo está vivo, que está medido y que se
queda dentro.

### El defecto que costó nueve mediciones, y no estaba donde parecía

Al montar la franja, **una escena del recorrido automático pasó de 4 segundos a agotar su espera a
los 63**: la que suelta una copia en una carpeta vigilada y espera a que el vigilante agrupe las dos.
Catalogaba las dos copias y no las agrupaba nunca.

La bisección, paso a paso:

| Qué se quitó | Resultado |
| --- | --- |
| El oyente del aviso de carpeta | sigue fallando |
| La publicación del estado de la raíz | sigue fallando |
| Los cambios del coordinador de escaneo | sigue fallando |
| El registro y el cableado en el arranque | sigue fallando |
| **La vista de la Biblioteca entera** | **pasa en 4 s** |
| La lista de avisos de carpeta | sigue fallando |
| El punto pulsante de la cabecera | sigue fallando |
| **El enlace de la orden al botón de cancelar** | **pasa en 4 s** |

**No era una excepción: era saturación.** El escaneo publica su progreso una vez por lote y `Apply`
corre en el hilo del escaneo. Anunciar «puede que ahora se pueda pulsar» en cada uno ponía a la
interfaz a recalcular el estado de un botón cientos de veces por segundo, y el catálogo esperaba
detrás de la avalancha.

**La corrección es no decir lo que no ha cambiado**, que además es la respuesta honesta: durante un
escaneo automático ninguno de los tres valores se mueve nunca. Con la guarda puesta, la escena vuelve
a pasar en 4 segundos **con la vista entera montada**.

**La lección, que es nueva en esta casa**: un modelo que se suscribe a un bus se ejecuta en el hilo
de quien publica, y hasta ahora eso sólo movía texto. Una orden enlazada a un botón es otra cosa —
cada aviso suyo cruza al hilo de la interfaz—, así que **un aviso incondicional dentro de un bucle de
progreso es un cuello de botella, no un detalle de estilo**.

### La cuarta pata, que casi se olvida

La banda de archivo suelto —el precedente— llegó el 2026-08-21 con **cuatro** cosas a la vez: el
control, sus pruebas geométricas, su entrada en la tabla de acción líder y su escena de paseo. Esta
franja llevaba tres.

**Faltaba la que sostiene la decisión.** Sin ella el ADR sería un párrafo: la franja podría
convertirse mañana en una superposición y **todas las demás puertas seguirían verdes** — los tokens
son los mismos, la acción líder es la misma y no desborda de ninguna de las dos maneras. Se escribió,
y se comprobó que ve de verdad convirtiéndola en superposición a propósito:

```
The_strip_pushes_the_grid_down_rather_than_covering_it [FAIL]
the filters start at y=54 with a notice on screen and at y=54 without one, so the strip is drawn
over the grid instead of pushing it.
```

Son tres: que empuja, que vuelve a medir cero cuando la carpeta regresa, y que **un escaneo que
arrancó solo no mueve nada** — que es el quinto punto del ADR medido en píxeles y no en una bandera.

### El verde

| Suite | Resultado |
| --- | --- |
| `Domain.Tests` | 743 de 743 |
| `Application.Tests` | 353 de 353 |
| `ArchitectureTests` | 39 de 39 |
| `UiTests` | 1.237 de 1.237 |
| `IntegrationTests` | 563 de 563, 3 omitidas |
| `AccessibilityTests` | 150 de 150, con la escena nueva |

La puerta del paseo: **224 pulsados, 23 pendientes** — el trinquete no se movió y el botón nuevo se
pulsa con un ratón de verdad, afirmando sobre el origen de cancelación y no sobre la pantalla.

### Lo que queda fuera, y por qué se dice

El prototipo pone un botón **«Permisos»** en el aviso de acceso denegado, que abre los ajustes de
Windows para ese recurso. Eso es abrir un proceso del sistema, vive en otra capa y tiene sus propias
reglas de aislamiento: es alcance nuevo y necesita una decisión antes que código.

---

## English

### What was missing, and it was not «a button»

`LIB-002` is `VERIFIED` and promises that «scans can be cancelled». The view model had `CanCancel`, a
complete `Cancel()` and its cancellation source; the progress row drew the dot, the sentence and the
count, **and no button**. The 2026-09-04 audit called it «cancellable from inside and not from
outside».

Measuring it turned up two more things nobody had written down:

- **A scan that starts on its own drew nothing.** `IsRunning` was only ever set by `Begin`, and only
  the hand-launched route calls `Begin`. A startup or watcher scan published its progress into a
  surface that stayed invisible. The «discreet mark» the ADR asks for **had nothing to light it**.
- **«Who launched the scan» never reached the screen.** The value has existed with its five members
  since scanning did, and the progress event did not carry it. Without it the surface could not tell
  the two cases apart, and drew neither.

### What the prototype says, measured rather than assumed

| What it draws | Measured in |
| --- | --- |
| Four stacked notices, each with tone, icon, title, body and action | `banner(tone, ic, …)` |
| 12 px gap, 12 by 14 padding, a 1 px border in the tone, radius **10** | `banner`'s style |
| «Incremental scan running» with **Cancel** | `flags.scanning` |
| «Root disconnected: …» in the warning tone, «Access denied: …» in the error tone | `flags.usb`, `flags.denied` |

The radius of 10 has no token, which is why the class writes it: it is the same number `Border.row-box`
already wrote, and the corners gate pairs it with `banner` in this same change.

### The progress bar was tried and rejected, on a number

The prototype draws a bar at 34 %. **Here it would be invented**: a scan does not know its total until
it has enumerated everything. An indeterminate bar was tried and the overflow gate refused it:

```
LibraryView: IndeterminateProgressBarIndicator2 ends at x=-75 in a 900-wide window
```

Avalonia's template animates its indicator **in from outside the control**. In its place goes the
pulsing dot this house already uses to say a scan is alive, which is measured and stays inside.

### The defect that took nine measurements, and was not where it looked

Mounting the strip took one walk scene from 4 seconds to timing out at 63: the one that drops a copy
into a watched folder and waits for the watcher to group the two. It catalogued both copies and never
grouped them.

The bisection, step by step:

| What was removed | Result |
| --- | --- |
| The root-notice listener | still fails |
| Publishing the root's availability | still fails |
| The scan coordinator's changes | still fails |
| The registration and the startup wiring | still fails |
| **The whole Library view** | **passes in 4 s** |
| The root notices list | still fails |
| The header's pulsing dot | still fails |
| **The cancel button's Command binding** | **passes in 4 s** |

**It was not an exception: it was saturation.** A scan publishes progress once per batch and `Apply`
runs on the scanning thread. Announcing «this may be pressable now» on every one of them put a
button's enabled-state recalculation on the interface thread hundreds of times a second, and the
catalogue waited behind the flood.

**The fix is not saying what has not changed**, which is the honest answer anyway: during an automatic
scan none of the three values ever moves. With the guard in place the scene passes in 4 seconds again
**with the whole view mounted**.

**The lesson, and it is new in this house**: a view model subscribed to a bus runs on the publisher's
thread, and until now that only moved text. A command bound to a button is another matter — each of
its announcements crosses to the interface thread — so **an unconditional announcement inside a
progress loop is a bottleneck, not a matter of style**.

### The fourth leg, which was nearly forgotten

The loose-file band — the precedent — arrived on 2026-08-21 with **four** things at once: the control,
its geometric tests, its entry in the leading-action table, and its walk scene. This strip had three.

**The missing one is what holds the decision up.** Without it the ADR would be a paragraph: the strip
could be turned into an overlay tomorrow and **every other gate would stay green** — the tokens are
the same, the leading action is the same, and it overflows neither way. It was written, and shown to
see by deliberately turning the strip into an overlay:

```
The_strip_pushes_the_grid_down_rather_than_covering_it [FAIL]
the filters start at y=54 with a notice on screen and at y=54 without one, so the strip is drawn
over the grid instead of pushing it.
```

There are three: that it pushes, that it measures nothing again once the root returns, and that **a
scan which started on its own moves nothing** — the ADR's fifth point measured in pixels rather than
in a flag.

### Green

| Suite | Result |
| --- | --- |
| `Domain.Tests` | 743 of 743 |
| `Application.Tests` | 353 of 353 |
| `ArchitectureTests` | 39 of 39 |
| `UiTests` | 1,237 of 1,237 |
| `IntegrationTests` | 563 of 563, 3 skipped |
| `AccessibilityTests` | 150 of 150, with the new scene |

The walk gate: **224 pressed, 23 pending** — the ratchet did not move and the new button is pressed
with a real mouse, asserted on the cancellation source rather than on the screen.

### What is left out, and why it is said

The prototype puts a **«Permissions»** button on the access-denied notice, opening Windows' settings
for that share. That is starting a system process, it lives in another layer and has isolation rules
of its own: it is new scope and needs a decision before code.
