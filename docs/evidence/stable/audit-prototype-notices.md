# La franja que nadie declaró, y cinco defectos del prototipo / The Strip Nobody Declared, and Five Prototype Defects

- IDs: `PRD-006`
- Fecha / Date: 2026-09-05
- Alcance / Scope: `design/AP Reelume.dc.html`, `design/README.md`, `design/Propuesta de diseño`, `design/Inventario de controles`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Por qué se miró el prototipo

El propietario dijo que los avisos del prototipo «se ven rotos: no flotan, desplazan». El prototipo
es la herramienta con la que decide el diseño visual, así que un defecto suyo se paga en todas las
decisiones que salgan de él. Se midió antes de tocar nada.

### Lo que se midió, y por qué NO está roto

Con el prototipo servido y cada escenario encendido, midiendo la posición del título de Inicio antes
y después:

| Aviso | Alto | Desplazamiento del contenido |
| --- | --- | --- |
| Escaneo en curso | 76 px | **89 px** |
| Sin conexión | 64 px | **77 px** |
| Raíz desconectada | 64 px | **77 px** |
| Acceso denegado | 64 px | **77 px** |

Y con la página ya desplazada 400 px, **el salto sigue siendo de 77 px**: el contenido se mueve bajo
el cursor. La franja es `position: static` y vive fuera del contenedor con desplazamiento, así que
ocupa sitio por construcción.

El mensaje flotante hace lo contrario, y también se midió: `position: fixed`, **desviación 0** del
centro de la ventana, 26 px del borde inferior, **ningún ancestro** que rompa su posición, y por
encima del panel. Funciona.

**Y las dos cosas están bien así**, por cuatro evidencias que apuntan al mismo sitio:

1. **Microsoft** lo dice de su `InfoBar` sin ambigüedad: «will take up space in your layout […] It
   will not cover up other content or float on top of it». Su ejemplo de tarea larga es una copia en
   curso con la barra dentro y sin botón de cerrar. Material dice lo mismo del banner —«it pushes
   content downwards»— y Carbon lo remata: «Do not cover other content with inline notifications».
2. **La regla que sale de las cinco fuentes** no es la gravedad: empuja lo que describe un estado,
   flota lo que narra un suceso. La gravedad decide el color y el icono.
3. **La aplicación ya decidió esto**, el 2026-08-21, para el caso más parecido que hay: la banda de
   archivo suelto empezó sobre el vídeo y se movió a empujarlo, con dos pruebas geométricas escritas
   para que no vuelva.
4. **El prototipo cumple una regla sin una sola excepción**: flota lo que va sobre el fotograma o
   sustituye a la aplicación entera —arranque, primeros pasos, recuperación, modal, mini, panel de
   demostración—; empuja todo lo del armazón, cabecera y raíl incluidos.

**El matiz que sí es un hallazgo**, de Core Web Vitals: el salto daña cuando ocurre **sin que nadie
haya hecho nada**. Un empujón dentro de la ventana de causa-efecto de una acción propia «is generally
fine». De ahí la decisión del propietario: franja completa cuando el escaneo lo lanza él, marca
discreta cuando arranca solo.

### El defecto de fondo: nadie lo había declarado

Ni la franja ni el mensaje flotante aparecen en el inventario de controles, que cuenta 202, **ni en
su lista de exclusiones** —donde sí están el panel «Demostración» entero y la barra de título
dibujada a mano—. Tampoco en la fila `ShellView` de la §4 de la Propuesta, que es justo donde se
enumeran «las formas condicionales que hay que pintar». Ni una sola de las palabras «empuja»,
«desplaza», «flotante» o «superpuesto» aparece en ningún documento del paquete referida a ellas.

**Por eso nadie decidió nunca cómo debían comportarse.** La regla medida queda escrita en el
inventario, junto a las exclusiones, que es donde alguien la buscará.

### Los cinco defectos corregidos

1. **`design/README.md` decía «28 estados» y hay 30.** Contados por código sobre la lista canónica y
   confirmados en el navegador: treinta radio-botones.
2. **El botón «Permisos» no llevaba a ningún sitio.** Es el único de los cuatro avisos al que no se
   le pasó destino, así que caía al respaldo y mostraba un flotante con el título del propio aviso.
   Ahora explica qué haría, como los otros cincuenta y tantos flotantes del prototipo.
3. **«Identificación ambigua» no cambiaba nada.** Su bandera se calculaba y **nadie la leía**: el
   estado sólo navegaba a Revisión, así que pulsarlo enseñaba la misma bandeja que «Normal». Ahora
   ordena la bandeja por confianza ascendente y deja arriba el caso que obliga a decidir a mano —el
   de 34 % con «tres candidatos con la misma puntuación»—.
4. **Un rojo de reserva no cuadraba**: `#FDE7E9` en el aviso de renombrado bloqueado contra `#FDECEE`
   en el resto del prototipo.
5. **La pista de maquetado de la lista de estados decía 13** y hay 30.

### Y una corrección al documento, no al dibujo

La §4 de la Propuesta pedía para la banda de archivo suelto «48 px, no superpuesta al vídeo» y se
marcaba **`Bloqueado`: «el defecto medido el 17-08 impide que llegue a pantalla, así que no puedo
verificarlo»**. Las dos cosas están caducadas:

- **La colocación se cumple** y tiene dos pruebas geométricas que la fijan, más siete de
  comportamiento. Llega a pantalla desde el 2026-08-21.
- **Los 48 px se rechazaron entonces con su número**: el aviso lleva encabezado, nombre del archivo,
  explicación que envuelve, su acción y un panel de confirmación con dos botones más, y pide 286 px a
  1280, 318 a 900 y 336 a 480. A 48 sólo cabe el encabezado.

La fila queda con la altura rechazada y el estado verificado, que es lo que la medición dijo.

---

## English

### Why the prototype was measured

The owner said the prototype's notices «look broken: they do not float, they push». The prototype is
the tool the visual design is decided with, so a defect in it is paid for in every decision that
comes out of it. It was measured before anything was touched.

### What was measured, and why it is NOT broken

With the prototype served and each scenario switched on, measuring Home's title before and after:

| Notice | Height | Content displacement |
| --- | --- | --- |
| Scan running | 76 px | **89 px** |
| Offline | 64 px | **77 px** |
| Root disconnected | 64 px | **77 px** |
| Access denied | 64 px | **77 px** |

And with the page already scrolled 400 px, **the jump is still 77 px**: content moves under the
cursor. The strip is `position: static` and lives outside the scrolling container, so it takes space
by construction.

The floating message does the opposite, and was measured too: `position: fixed`, **zero deviation**
from the window's centre, 26 px from the bottom edge, **no ancestor** breaking its positioning, and
above the panel. It works.

**And both are right as they are**, on four pieces of evidence pointing the same way:

1. **Microsoft** says it of `InfoBar` without ambiguity: «will take up space in your layout […] It
   will not cover up other content or float on top of it». Its long-task example is a backup in
   progress with the bar inside and no close button. Material says the same of the banner — «it
   pushes content downwards» — and Carbon finishes it: «Do not cover other content with inline
   notifications».
2. **The rule the five sources yield** is not severity: what describes a state pushes, what narrates
   an event floats. Severity decides colour and icon.
3. **The application already decided this**, on 2026-08-21, for the closest case there is: the
   loose-file band started over the video and moved to pushing it, with two geometric tests written
   so it cannot go back.
4. **The prototype follows one rule without a single exception**: what sits over the frame or
   replaces the whole application floats — startup, onboarding, recovery, modal, mini player, the
   demo panel; everything belonging to the shell pushes, header and rail included.

**The finding that is real**, from Core Web Vitals: a shift harms when it happens **with nobody
having done anything**. A push inside the cause-and-effect window of one's own action «is generally
fine». Hence the owner's decision: full strip when the scan is launched by hand, quiet marker when it
starts on its own.

### The underlying defect: nobody had declared it

Neither the strip nor the floating message appears in the controls inventory, which counts 202, **nor
in its exclusion list** — where the whole «Demostración» panel and the hand-drawn title bar do sit.
Nor in the Proposal's §4 `ShellView` row, which is exactly where «the conditional shapes that must be
painted» are enumerated. Not one of the words «pushes», «displaces», «floating» or «overlaid» appears
in any document of the package about them.

**That is why nobody ever decided how they should behave.** The measured rule is now written into the
inventory beside the exclusions, which is where somebody will look for it.

### The five defects fixed

1. **`design/README.md` said «28 states» and there are 30.** Counted by code over the canonical list
   and confirmed in the browser: thirty radio buttons.
2. **The «Permissions» button led nowhere.** It is the only one of the four notices given no
   destination, so it fell through to the fallback and showed a floating message carrying the
   notice's own title. It now explains what it would do, like the fifty-odd others.
3. **«Ambiguous match» changed nothing.** Its flag was computed and **read by nobody**: the state
   only navigated to Review, so pressing it showed the same inbox as «Normal». It now sorts the inbox
   by ascending confidence and leaves at the top the case that forces a hand decision — the 34 % one
   with «three candidates share the same score».
4. **A fallback red did not match**: `#FDE7E9` on the blocked-rename notice against `#FDECEE`
   everywhere else.
5. **The state list's layout hint said 13** and there are 30.

### And one correction to the document, not to the drawing

The Proposal's §4 asked the loose-file band for «48 px, not overlaid on the video» and marked itself
**`Blocked`: «the defect measured on 17-08 stops it reaching the screen, so I cannot verify it»**.
Both are stale:

- **The placement holds** and has two geometric tests fixing it, plus seven of behaviour. It has
  reached the screen since 2026-08-21.
- **The 48 px were refused then with their number**: the notice carries a heading, the file's name, a
  wrapping explanation, its action and a confirmation panel with two more buttons, and asks for
  286 px at 1280, 318 at 900 and 336 at 480. Forty-eight holds the heading and nothing else.

The row now carries the refused height and the verified state, which is what the measurement said.
