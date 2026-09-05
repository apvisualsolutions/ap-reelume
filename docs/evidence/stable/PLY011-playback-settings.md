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
| `UiTests` completa | **1.233 de 1.233**, cero omitidas |
| `PlaybackSettingsTests` | **16 de 16**, y el ViewModel a **100/100** |
| Escena del paseo con ratón real | pasa en 13 s |
| Trinquete del paseo | **223 pulsados, 23 pendientes** — se mantiene |
| Suelos de cobertura | el ViewModel a 100/100; la vista a la lista, trinquete 188 → 189 |

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

### El segundo rojo, y por qué la previsualización de suelos no lo vio

El run de `9a2aae6` arregló las dos pruebas de arquitectura y salió rojo por otra puerta: **la de
cobertura**, con los dos archivos nuevos de la sección.

| Archivo | Medido | Qué se hizo |
| --- | --- | --- |
| `PlaybackSettingsView.axaml` | **100/50** | a la lista de deuda, trinquete **188 → 189** |
| `PlaybackSettingsViewModel.cs` | **90/95** | **cubierto hasta 100/100** |

**Los dos casos son distintos y confundirlos habría sido el error.** La mitad de ramas de un `.axaml`
es **la única rama que el compilador de Avalonia genera** para él, en la línea del elemento raíz:
las sesenta vistas del árbol miden exactamente eso, así que no es deuda y una vista nueva sube el
trinquete en uno. La propia puerta lo autoriza por escrito. El ViewModel **sí podía mejorar**, y la
regla dice que un archivo entra en la lista sólo cuando no puede: el JSON de coverlet nombró las
cuatro líneas y las dos ramas —los dos topes del deslizador, leer la duración con la cuenta atrás
apagada, y escribirla en ese mismo estado—, y tres pruebas lo llevaron a **100/100**.

**Y la previsualización de suelos se corrió, con cuatro suites, y dijo que ningún archivo nuevo se
quedaba corto.** Su límite está escrito dentro y aquí se cobró: **sólo mide lo que le nombras**, y
lo que faltaba no era una suite sino el momento — se corrió con la sección ya escrita pero antes de
que la vista existiera como archivo compilado que el informe pudiera medir. **Su silencio no es un
certificado**, que es exactamente lo que su propia cabecera advierte.


### Y un tercer rojo que NO era del código, medido antes de tocar nada

El mismo commit falló después en la **puerta de accesibilidad**, y sólo en la **segunda pasada**:
`The_other_version_is_switched_to_with_the_mouse_and_its_question_answered` esperaba `00:00:00` y
leyó `00:01:00`.

**Ese minuto es exactamente la duración observada del vídeo sembrado**, sesenta segundos, y eso es lo
que lo delata. La escena comprueba la posición **guardada**, y quien la escribe es el bucle periódico
de progreso; si ese bucle escribe justo antes de que el reinicio se aplique, guarda dónde estaba la
sesión anterior — que había llegado al final. La escena espera a que la cabeza de lectura esté por
debajo de diez segundos, y esa espera no cubre al escritor.

**No se tocó nada, porque primero se midió**, y las dos mediciones dicen lo mismo:

| Medición | Resultado |
| --- | --- |
| La escena sola, dos veces seguidas | pasa las dos |
| **`eng/run-accessibility.ps1 -Passes 2` entero**, el mismo guion que corre CI | **149 de 149 en las dos pasadas, 0 críticos, 0 mayores, 0 menores** |

Es una carrera de tiempo que un runner compartido destapa y esta máquina no. **Un rojo que no es del
código no se arregla cambiando el código**, y menos aún relajando la aserción: el cero que esa escena
exige es el significado entero de «volver a empezar». Se reintentó el trabajo y queda anotado como la
causa que es, para que el siguiente que lo vea no lo persiga como una regresión.

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
| Full `UiTests` | **1,233 of 1,233**, zero skipped |
| `PlaybackSettingsTests` | **16 of 16**, and the view model at **100/100** |
| Walk scene with a real mouse | passes in 13 s |
| Walk ratchet | **223 pressed, 23 pending** — unchanged |
| Coverage floors | the view model at 100/100; the view onto the list, ratchet 188 → 189 |

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

### The second red, and why the coverage-floor preview did not see it

The run of `9a2aae6` fixed both architecture tests and came back red on another gate: **coverage**,
with the section's two new files.

| File | Measured | What was done |
| --- | --- | --- |
| `PlaybackSettingsView.axaml` | **100/50** | onto the debt list, ratchet **188 → 189** |
| `PlaybackSettingsViewModel.cs` | **90/95** | **covered up to 100/100** |

**The two cases are different, and confusing them would have been the mistake.** An `.axaml`'s half
of branches is **the only branch Avalonia's compiler generates** for it, on the root element's line:
all sixty views in the tree measure exactly that, so it is not debt and a new view raises the ratchet
by one. The gate authorises that in writing. The ViewModel **could** improve, and the rule is that a
file enters the list only when it cannot: coverlet's JSON named the four lines and two branches — the
slider's two bounds, reading the length while the countdown is off, and writing it in that same state
— and three tests took it to **100/100**.

**And the floor preview was run, with four suites, and said no new file fell short.** Its limit is
written inside it and was collected here: **it only measures what you name**, and what was missing
was not a suite but the moment — it ran with the section written but before the view existed as a
compiled artifact its report could measure. **Its silence is not a certificate**, which is exactly
what its own header warns.


### And a third red that was NOT the code's, measured before touching anything

The same commit then failed the **accessibility gate**, and only on the **second pass**:
`The_other_version_is_switched_to_with_the_mouse_and_its_question_answered` expected `00:00:00` and
read `00:01:00`.

**That minute is exactly the seeded video's observed duration**, sixty seconds, and that is what
gives it away. The scene checks the **stored** position, and what writes it is the periodic progress
loop; if that loop writes just before the restart lands, it stores where the previous session was —
which had reached the end. The scene waits for the playhead to be under ten seconds, and that wait
does not cover the writer.

**Nothing was touched, because it was measured first**, and both measurements say the same:

| Measurement | Result |
| --- | --- |
| The scene alone, twice in a row | passes both |
| **The whole `eng/run-accessibility.ps1 -Passes 2`**, the same script CI runs | **149 of 149 on both passes, 0 critical, 0 major, 0 minor** |

It is a timing race a shared runner exposes and this machine does not. **A red that is not the code's
is not fixed by changing the code**, and least of all by relaxing the assertion: the zero that scene
demands is the entire meaning of «start over». The job was rerun and this is written down as the
cause it is, so the next person to see it does not chase it as a regression.