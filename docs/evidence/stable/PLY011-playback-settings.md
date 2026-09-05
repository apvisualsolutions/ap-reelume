# La sección «Reproducción» que el criterio prometía / The «Playback» Section the Criterion Promised

- IDs: `PLY-011`, `CRS-004`
- Fecha / Date: 2026-09-05
- Alcance / Scope: `Settings/PlaybackSettingsView`, `Settings/PlaybackSettingsViewModel`, `Shell/SettingsSection`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Qué faltaba, y desde cuándo

El criterio de `PLY-011` dice, literalmente, que la cuenta atrás «es cancelable, **configurable** y
vuelve a la ficha si el siguiente archivo no existe». Lo primero y lo tercero eran ciertos. Lo
segundo no: la duración se guarda en una preferencia desde T28 —con cero apagando la cadena entera—,
se lee al reproducir, y **lo único que la escribía eran las pruebas**.

El comentario de la propia clase afirmaba que «the settings surface already reads and writes» esa
clave. **Esa superficie no existía.** La ficha estuvo `VERIFIED` sobre esa afirmación hasta que la
auditoría del 2026-09-04 la bajó a `IMPLEMENTED`, que no es una regresión: es que decía algo que
nunca fue cierto.

### La forma, y por qué son dos filas para un número

El prototipo dibuja **un interruptor**. El almacén guarda **segundos de 0 a 60**, con cero como
apagado. Las dos cosas responden preguntas distintas: el interruptor pregunta «¿empieza solo lo
siguiente?» y los segundos preguntan «¿cuánto tengo para decir que no?».

- **Sólo el interruptor** pierde la duración: apagar escribe cero, encender vuelve a diez, y quien
  quiera treinta segundos no tiene dónde pedirlos. La ficha seguiría prometiendo «configurable».
- **Sólo el deslizador** responde a la segunda y deja la primera a deducir de un cero.

El propietario eligió las dos filas el 2026-09-05, con las tres opciones dibujadas delante. El
interruptor manda, y la fila de segundos **aparece sólo cuando está encendido** — ausente y no
deshabilitada, que es como esta aplicación contesta ya donde una opción no puede ocurrir.

### La decisión que no se ve en el diff: el deslizador nunca escribe cero

Cero es la palabra del interruptor. Un deslizador que pudiera alcanzarlo daría a dos controles la
misma voz sobre un solo valor: arrastrar al extremo izquierdo apagaría la cadena en silencio
mientras el interruptor de arriba siguiera diciendo «Sí». Su suelo es por eso **cinco segundos**, y
hay una prueba por cada forma de intentarlo —cero, negativo y uno—.

**Y encender restaura la duración que estaba en vigor**, no la de fábrica. Sin eso, apagar la cadena
una tarde y volver a encenderla al día siguiente descartaría en silencio unos treinta segundos
elegidos a mano. Tiene su prueba propia, que es la que explica por qué las dos filas comparten
estado en vez de vivir cada una por su lado.

### Lo medido

| Puerta | Resultado |
| --- | --- |
| `UiTests` completa | **1.215 de 1.215**, cero omitidas |
| `PlaybackSettingsTests` | **13 de 13** |
| Escena del paseo con ratón real | pasa en 13 s |
| Trinquete del paseo | **223 pulsados, 23 pendientes** — se mantiene |
| Suelos de cobertura | ningún suelo se mueve, ningún archivo nuevo se queda corto |

**Los dos controles se pulsan con un ratón de verdad** en la escena de preferencias, y la aserción
no lee el control ni el modelo de la vista: lee **la misma fachada que el reproductor consulta al
encadenar**. Un interruptor que se mueve y no escribe nada es exactamente el defecto que esta
aplicación lleva encontrándose todo el año, y sólo el almacén distingue los dos casos.

### Lo que esta sección todavía no trae

El prototipo pone en «Reproducción» cuatro filas, y aquí van dos. Las otras dos quedan nombradas con
su medición:

- **Salto atrás y adelante.** `ControlPlayback.ConfigureSkipsAsync` existe con su recorte de rango, y
  **sólo lo llaman dos pruebas**: es el mismo defecto que la cuenta atrás, en la misma sección. Le
  falta la clave persistida, porque hoy los segundos viven en un campo en memoria que muere con la
  sesión.
- **Preferir reproductor externo.** Es alcance entero: el lanzador externo existe sólo como
  recuperación tras un fallo, y no hay preferencia ni bifurcación al abrir una sesión.

---

## English

### What was missing, and since when

`PLY-011`'s criterion says, literally, that the countdown is «cancelable, **configurable**, and
returns to details when the next file is missing». The first and third were true. The second was
not: the duration has been stored in a preference since T28 — with zero switching the whole chain off
— read at playback, and **the only thing writing it was the tests**.

That class's own comment claimed «the settings surface already reads and writes» the key. **That
surface did not exist.** The row sat `VERIFIED` on that claim until the audit of 2026-09-04 dropped
it to `IMPLEMENTED`, which is not a regression: it is that the row said something that was never
true.

### The shape, and why one number takes two rows

The prototype draws **a toggle**. The store keeps **seconds from 0 to 60**, with zero meaning off.
They answer different questions: the toggle asks «does the next thing start on its own?» and the
seconds ask «how long do I have to say no?».

- **The toggle alone** drops the length: off writes zero, on returns to ten, and somebody wanting
  thirty seconds has nowhere to ask. The row would go on promising «configurable».
- **The slider alone** answers the second and leaves the first to be inferred from a zero.

The owner chose both rows on 2026-09-05, with the three options drawn side by side. The toggle owns
on and off, and the seconds row **appears only while it is on** — absent rather than disabled, which
is how this application already answers where an option cannot happen.

### The decision the diff does not show: the slider never writes zero

Zero is the toggle's word. A slider that could reach it would give two controls the same say over one
value: dragging to the left edge would silently switch the chain off while the toggle above still
read «On». Its floor is therefore **five seconds**, with one test per way of trying — zero, negative
and one.

**And switching back on restores the length that was in force**, not the factory one. Without that,
turning the chain off for an evening and back on the next day would quietly discard a hand-chosen
thirty seconds. It has a test of its own, and it is the one that explains why the two rows share
state rather than living apart.

### What was measured

| Gate | Result |
| --- | --- |
| Full `UiTests` | **1,215 of 1,215**, zero skipped |
| `PlaybackSettingsTests` | **13 of 13** |
| Walk scene with a real mouse | passes in 13 s |
| Walk ratchet | **223 pressed, 23 pending** — unchanged |
| Coverage floors | no floor moves, no new file falls short |

**Both controls are pressed with a real mouse** in the preferences scene, and the assertion reads
neither the control nor the view model beside it: it reads **the same facade the player consults when
chaining**. A switch that moves and writes nothing is exactly the defect this application has been
finding in itself all year, and only the store tells the two apart.

### What this section does not yet carry

The prototype gives «Playback» four rows and this carries two. The other two are named with their
measurement:

- **Skip back and forward.** `ControlPlayback.ConfigureSkipsAsync` exists with its clamp, and **only
  two tests call it**: the same defect as the countdown, in the same section. It lacks the persisted
  key, because today the seconds live in an in-memory field that dies with the session.
- **Prefer external player.** Whole scope: the external launcher exists only as recovery after a
  failure, and there is neither a preference nor a branch when a session opens.
