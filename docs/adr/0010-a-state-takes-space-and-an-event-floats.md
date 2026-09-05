# ADR-0010 — Un estado ocupa sitio y un suceso flota / A State Takes Space and an Event Floats

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-09-05
- Decisor / Decision owner: Product Owner
- Relacionado / Related: [`LIB-002`](../FEATURES.md), [`PRD-006`](../FEATURES.md),
  [los avisos del prototipo no estaban rotos](../evidence/stable/audit-prototype-notices.md),
  `design/Inventario de controles`, `src/ApSolutions.LocalMedia.Presentation/Player/LooseFileBanner.axaml`

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

El propietario dijo el 2026-09-05 que los avisos del prototipo «se ven rotos: no flotan, desplazan».
El prototipo es la herramienta con la que se decide el diseño visual, así que la duda había que
resolverla midiendo.

**Y no estaban rotos.** Los cuatro avisos empujan el contenido **77 px** —89 el del escaneo— y lo
hacen también con la pantalla a medio recorrer. El mensaje efímero, en cambio, flota: `position:
fixed`, desviación cero del centro, 26 px del borde inferior, nada que lo tape.

**Lo que sí faltaba era la decisión.** Ni la franja de avisos ni el mensaje efímero aparecen en el
inventario de controles del paquete de diseño, que cuenta 202, **ni en su lista de exclusiones** —
donde sí están el panel «Demostración» entero y la barra de título dibujada a mano—. Tampoco en la
fila del armazón de la §4 de la Propuesta, que es justo donde se enumeran «las formas condicionales
que hay que pintar». Ni una sola de las palabras «empuja», «desplaza», «flotante» o «superpuesto»
aparecía en ningún documento referida a ellos. **Por eso nadie había decidido nunca cómo debían
comportarse**, y por eso la pregunta podía hacerse dos veces con dos respuestas.

### Decisión

**Un aviso que describe un ESTADO ocupa sitio y empuja el contenido. Un aviso que narra un SUCESO ya
ocurrido flota y se retira solo.**

1. **Un estado dura lo que dura la condición** —un disco desconectado, un escaneo en curso, una red
   caída— y tiene que seguir ahí cuando la persona vuelva a mirar. Taparle contenido durante horas es
   peor que moverlo una vez.
2. **Un suceso ya pasó** —«diagnósticos escritos», «coincidencia confirmada»— y no hay nada que
   volver a mirar. Empujar y devolver el contenido tres segundos después son **dos** saltos gratis.
3. **La gravedad decide el color y el icono, nunca la posición.** Un error grave y momentáneo flota;
   un aviso leve pero permanente empuja.
4. **La excepción es el fotograma**: lo que se dibuja sobre el vídeo flota, porque una imagen no es
   contenido que se pueda empujar. Con alineación y tope de ancho explícitos, que es lo que ya exige
   la prueba de los paneles del reproductor.
5. **Y el salto se paga cuando nadie lo ha pedido.** Un empujón dentro de la ventana de causa y
   efecto de una acción propia es aceptable; uno que ocurre solo, no. Un escaneo lanzado a mano puede
   empujar; uno que arranca al abrir el programa, no.

### Por qué así, y no de otra manera

**No es una invención de esta casa, y las fuentes coinciden.** Microsoft lo dice de su `InfoBar` sin
ambigüedad — «will take up space in your layout […] It will not cover up other content or float on
top of it» — y su ejemplo de tarea larga es una copia en curso con la barra dentro. Material dice lo
mismo de su banner —«it pushes content downwards»— y reserva el mensaje flotante para lo que se va
solo entre cuatro y diez segundos. Carbon lo remata: «Do not cover other content with inline
notifications».

**Y esta aplicación ya lo había decidido**, el 2026-08-21, para el caso más parecido que hay: la
banda de archivo suelto empezó sobre el vídeo y se movió a empujarlo, con dos pruebas geométricas
escritas para que no vuelva. Aquella decisión rechazó además la altura de 48 px que el documento
pedía, con su número: el aviso necesita entre 286 y 336 px según el ancho.

**El punto 5 es el único que no sale de las guías de componentes** sino de la investigación de
rendimiento web: un desplazamiento de la disposición daña cuando ocurre sin intervención, y «is
generally fine» cuando sigue a una acción de la persona dentro de una ventana corta.

**Dónde discrepan las fuentes, y por qué no cambia la decisión**: Material 3 retiró el banner y
manda al diálogo o al mensaje flotante, pero nació para el móvil; Carbon admite un flotante
persistente con acción, que Material prohibiría; y Apple no tiene banner y prefiere marcar el
elemento afectado en su contexto. Ese último matiz **sí** se recoge, en el punto siguiente.

### Consecuencias

- **El aviso va donde vive el problema, no persiguiendo a la persona.** El de disco desconectado se
  dibuja en la Biblioteca, que es donde están los títulos afectados y donde se puede actuar; no en el
  reproductor ni en Ajustes. Los títulos siguen marcados uno a uno como ya lo están.
- **El escaneo se dibuja de dos maneras según quién lo lanzó**, por el punto 5.
- **La regla queda escrita en el inventario de controles del paquete de diseño**, junto a las
  exclusiones, que es donde alguien la buscará.
- **`LIB-002` gana su botón de cancelar en esa franja**, y con él el aviso de que el escaneo terminó,
  que hoy está traducido y sin dibujar.
- **Nada de esto exige tokens nuevos**: los colores de aviso ya existen en el árbol con los valores
  exactos del prototipo.

---

## English

### Context

The owner said on 2026-09-05 that the prototype's notices «look broken: they do not float, they
push». The prototype is the tool the visual design is decided with, so the doubt had to be settled by
measuring.

**They were not broken.** All four notices push content by **77 px** — 89 for the scan — and do so
with the page already scrolled. The transient message, by contrast, floats: `position: fixed`, zero
deviation from centre, 26 px from the bottom edge, nothing covering it.

**What was missing was the decision.** Neither the notices strip nor the transient message appears in
the design package's controls inventory, which counts 202, **nor in its exclusion list** — where the
whole «Demostración» panel and the hand-drawn title bar do sit. Nor in the Proposal's §4 shell row,
which is exactly where «the conditional shapes that must be painted» are enumerated. Not one of the
words «pushes», «displaces», «floating» or «overlaid» appeared in any document about them. **That is
why nobody had ever decided how they should behave**, and why the question could be asked twice and
answered two ways.

### Decision

**A notice describing a STATE takes space and pushes content. A notice narrating an EVENT that has
already happened floats and withdraws on its own.**

1. **A state lasts as long as the condition** — a disconnected disk, a scan in progress, a dropped
   network — and has to still be there when the person looks back. Covering content for hours is
   worse than moving it once.
2. **An event has passed** — «diagnostics written», «match confirmed» — and there is nothing to look
   back at. Pushing and returning content three seconds later is **two** free jumps.
3. **Severity decides colour and icon, never position.** A serious momentary error floats; a mild
   permanent notice pushes.
4. **The exception is the frame**: what is drawn over video floats, because a picture is not content
   that can be pushed. With explicit alignment and width cap, which is what the player-panel test
   already demands.
5. **And a shift is paid for when nobody asked for it.** A push inside the cause-and-effect window of
   one's own action is acceptable; one that happens on its own is not. A scan launched by hand may
   push; one starting when the program opens may not.

### Why this shape and not another

**It is not a house invention, and the sources agree.** Microsoft says it of `InfoBar` without
ambiguity — «will take up space in your layout […] It will not cover up other content or float on top
of it» — and its long-task example is a backup in progress with the bar inside. Material says the
same of its banner — «it pushes content downwards» — and reserves the floating message for what
withdraws on its own between four and ten seconds. Carbon finishes it: «Do not cover other content
with inline notifications».

**And this application had already decided it**, on 2026-08-21, for the closest case there is: the
loose-file band started over the video and moved to pushing it, with two geometric tests written so
it cannot go back. That decision also refused the 48 px height the document asked for, with its
number: the notice needs between 286 and 336 px depending on width.

**Point 5 is the only one not from component guidance** but from web-performance research: a layout
shift harms when it happens without intervention, and «is generally fine» when it follows a person's
action within a short window.

**Where the sources disagree, and why it does not change the decision**: Material 3 retired the
banner and sends you to a dialog or a floating message, but it was born for mobile; Carbon allows a
persistent floating message with an action, which Material would forbid; and Apple has no banner and
prefers marking the affected element in its own context. That last nuance **is** taken up, in the
next section.

### Consequences

- **The notice goes where the problem lives, not following the person around.** The disconnected-disk
  one is drawn in the Library, which is where the affected titles are and where something can be
  done; not in the player, not in Settings. Titles stay marked one by one as they already are.
- **The scan is drawn two ways depending on who launched it**, by point 5.
- **The rule is written into the design package's controls inventory**, beside the exclusions, which
  is where somebody will look for it.
- **`LIB-002` gains its cancel button in that strip**, and with it the notice that the scan finished,
  which today is translated and undrawn.
- **None of this needs new tokens**: the notice colours already exist in the tree with the
  prototype's exact values.
