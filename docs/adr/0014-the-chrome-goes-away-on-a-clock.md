# ADR-0014 — Los controles se van solos con un reloj / The Chrome Goes Away on a Clock

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-09-25
- Decisor / Decision owner: Product Owner
- Relacionado / Related: `ENG-018` en [`TAREAS.md`](../TAREAS.md), [`PLY-014`](../FEATURES.md),
  [`ADR-0010`](0010-a-state-takes-space-and-an-event-floats.md),
  `src/ApSolutions.LocalMedia.Presentation/Shell/ShellViewModel.cs`

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

El propietario lo dijo el 2026-09-13: «la ocultación de los controles y el funcionamiento normal de
estos no es como los de cualquier reproductor». Medido en el código el 2026-09-25:

- Al empezar la película se ocultaba todo, y **un solo movimiento del ratón lo traía de vuelta para
  siempre**, hasta pausar y reanudar. Estaba escrito como decisión: un reloj sería «una segunda cosa
  decidiendo qué hay en pantalla», al que ninguna prueba podía preguntar sin esperarlo y con el que
  el paseo autónomo competiría en cada escena.
- El puntero no se ocultaba nunca. Un clic sobre la imagen y la rueda no hacían nada. Esc sólo salía
  de pantalla completa: con un panel abierto, o con la película en la ventana, no hacía nada.

Los reproductores comunes —YouTube, VLC, MPC— coinciden en todo lo que aquí faltaba.

### Decisión

**Los controles se van solos a los tres segundos sin mover el ratón ni pulsar una tecla, mientras la
película se reproduce.** No se van con un panel o el engranaje abiertos, ni con el puntero quieto
sobre un control, ni en pausa. El puntero se oculta con ellos sobre la imagen.

**Los dos costes que tumbaron el reloj eran reales y se pagan, no se esquivan:**

- **El reloj es un puerto** (`IClock`, el mismo que usa el resto de la aplicación), y las pruebas le
  pasan uno que avanza a mano. Ninguna espera.
- **El paseo pone el reloj en infinito** al montar la aplicación, porque cada escena mueve el ratón y
  después pulsa sobre una sesión real, en un servidor cuyo ritmo no controla nadie. **Una sola
  escena lo baja a 200 ms** y comprueba con el motor real que los controles se van solos; quitar la
  línea que conecta el reloj la pone en rojo.

**Y tres gestos más, los de cualquier reproductor:** un clic sobre la imagen pausa y reanuda —sólo
sobre la imagen: el clic de un botón de la barra también sube hasta ella—, la rueda mueve el volumen
el mismo paso que las flechas, y **Esc retrocede una capa**: engranaje, panel, pantalla completa o
mini reproductor, y por último cierra el reproductor. Es el orden del manejador de teclas del
prototipo.

### Consecuencias

- La espera no se cancela en cada movimiento, que llegan a cientos por segundo: el movimiento anota
  la hora y la espera, al despertar, calcula cuánto le falta. Parar la espera es avanzar una
  generación, así que no queda nada que liberar en un ViewModel que no tiene final propio.
- **En la ventana, ocultar la barra de título, el carril y la columna cambia el tamaño del vídeo**, y
  con reloj eso pasa más a menudo. No se decide aquí: pide que el propietario lo vea antes de elegir
  si esos controles deben flotar sobre la imagen en vez de ocupar sitio. Queda como tarea.

### Alternativas consideradas

- **Mantener la decisión anterior.** Rechazada: es la queja del propietario, y sus dos motivos tenían
  solución.
- **Un temporizador del framework dentro de la vista.** Rechazado: es exactamente lo que ninguna
  prueba puede preguntar sin esperar.
- **Ocultar sólo la barra y dejar el resto.** Rechazado: el encargo del reproductor es que en
  reproducción se vea sólo la imagen.

---

## English

### Context

The owner said it on 2026-09-13: the controls were not hiding or behaving like any player's. Measured
in the code on 2026-09-25: once playback started everything hid, and **one movement of the mouse
brought it back for good** until a pause and a resume. That was written down as a decision — a clock
would be a second thing deciding what is on screen, which no test could ask without waiting for it
and which the autonomous walk would race on every scene. The pointer never hid; a click on the
picture and the wheel did nothing; Escape only left fullscreen.

### Decision

**The chrome goes away by itself three seconds after the last movement or key while the film plays**
— not with a panel or the gear open, not with the pointer resting on a control, not while paused —
and the pointer hides with it over the picture.

**Both costs that ruled the clock out were real, and both are paid rather than dodged.** The clock is
a port (`IClock`, the one the rest of the application uses) and the tests hand in one they move by
hand. The walk sets it to infinite when it mounts the application, because every scene moves the
mouse and then presses on a real session; **one scene lowers it to 200 ms** and asserts on the real
engine that the chrome leaves by itself, and removing the line that wires the clock turns it red.

**And three more gestures, the ones every player has:** a click on the picture pauses and resumes —
only on the picture, since a bar button's click bubbles up to it too — the wheel moves the volume by
the arrow keys' step, and **Escape steps back one layer**: gear, panel, fullscreen or mini player,
and finally the player itself, which is the prototype's key handler's order.

### Consequences

The wait is not cancelled on every movement, which arrive by the hundred per second: a movement
writes the time and the wait works out what is left when it wakes. Stopping it moves a generation on,
so nothing disposable is left in a view model with no end of its own. **Embedded, hiding the title
bar, the rail and the column resizes the picture**, and with a clock that happens more often; it is
not decided here and stays as a task, because it needs the owner to see it.

### Alternatives considered

Keeping the previous decision, rejected as the very complaint; a framework timer inside the view,
rejected as exactly what no test can ask without waiting; hiding only the bar, rejected because the
player's brief is that only the picture shows while playing.
