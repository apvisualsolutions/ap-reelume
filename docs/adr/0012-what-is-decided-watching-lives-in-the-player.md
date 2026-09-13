# ADR-0012 — Lo que se decide mirando vive en el reproductor, y se dibuja, no flota / What Is Decided While Watching Lives in the Player, and Is Drawn, Not Floated

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-09-12
- Decisor / Decision owner: Product Owner
- Relacionado / Related: [`UX-010`](../FEATURES.md), [`PLY-018`](../FEATURES.md),
  la regla 11 de `CLAUDE.md`, [`ADR-0010`](0010-a-state-takes-space-and-an-event-floats.md),
  `src/ApSolutions.LocalMedia.Presentation/Player/`

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

El propietario pidió el 2026-09-12 que las opciones de imagen fueran al reproductor y añadió, en la
misma conversación, que **el resto de configuraciones de esa vista deberían ir también**, al modo del
reproductor de YouTube. Contar lo que había dio la medida:

- **Nueve secciones en Ajustes** y **cinco grupos ya en el reproductor** —pistas, salida de audio,
  estilo de subtítulos, velocidad y atajos—, sin ninguna regla escrita que dijera por qué unas
  estaban a un lado y otras al otro.
- **Dos controles de restablecer en toda la aplicación**: «Volver a 1×» y «Restaurar campos del
  proveedor». Ninguna sección de ajustes se podía deshacer. **Las dos mitades resultaron falsas** al
  medirlas el 2026-09-13 — ver la enmienda del final.

Nadie había decidido nunca el criterio, y por eso cada panel se escribió donde le tocó al que lo
escribió.

### Decisión

**Una opción que se decide mirando el vídeo vive en el reproductor; una que se configura una vez vive
en Ajustes.** El brillo, la velocidad, la pista de audio o cuántos segundos espera el siguiente
episodio piden estar viendo algo para elegir su valor; el tema, las raíces, las copias y las
actualizaciones no.

**Y el sitio en el reproductor es un panel dibujado sobre el vídeo, con navegación de dos niveles —el
segundo sustituye al primero—, nunca un menú emergente.** La forma es la de YouTube; el mecanismo no
puede serlo.

**El motivo del mecanismo está medido y es de este árbol**: dentro de un `Flyout` el recorrido
automático no alcanza nada, porque su contenido vive en una raíz de ventana emergente aparte. Los
paneles actuales usan `IsVisible` por esa misma razón, y el comentario que lo dice lleva en
`TransportControlsView.axaml` desde que se escribió el menú de velocidad. Un menú emergente dejaría
todos los ajustes del reproductor fuera de la cobertura del paseo de una vez.

**Cada grupo de opciones, esté donde esté, lleva dentro su «Restaurar valores por defecto».** Dentro
del grupo y no en un menú aparte: restaurar lo que se está mirando es predecible, y un «restaurar
todo» escondido es donde nacen los sustos. Los doce usan **la misma clave de traducción**, no el
mismo literal.

### Consecuencias

- **Tres secciones bajan de Ajustes al reproductor**: la cuenta atrás del siguiente episodio, saltar
  intros y el estilo de subtítulos. Las otras se quedan. *(Esta línea decía «la superficie del
  reproductor» en vez del estilo de subtítulos, y se corrigió el 2026-09-13 al medir que esa fila no
  tiene ningún mando — ver la enmienda del final.)*
- **Los cinco paneles de hoy pierden su botón propio en la barra** y pasan dentro del engranaje. La
  barra queda con reproducir, volumen, engranaje, miniatura y pantalla completa.
- **La puerta no puede juzgar el criterio**, porque «se decide mirando» no es medible. Lo que exige es
  **la decisión escrita**: una lista cerrada clasifica cada grupo en uno de los dos sitios y falla
  ante un grupo sin clasificar. Quien escriba un panel tiene que decidir y dejarlo puesto — es lo
  único automatizable de una regla de criterio, y es lo mismo que hace `LeadingActionTests` con la
  acción principal de cada vista.
- **La puerta del botón es simétrica**: un grupo fuera de la lista falla y un grupo listado sin botón
  falla. Sin las dos mitades, el próximo panel nace sin botón y nadie se entera, que es exactamente
  cómo se llegó a tener dos en toda la aplicación.

### Enmienda del 2026-09-12 (noche), al construirlo

Tres precisiones, y las tres salen de medir en vez de razonar. No cambian la decisión: la afinan
donde estaba escrita en general.

- **«Dibujado sobre el vídeo» es dentro de la banda del transporte, no flotando sobre la imagen.**
  Un panel de 380 px alineado abajo a la derecha del panel raíz cae **encima del extremo derecho de
  la propia barra** —justo sobre los botones con los que alguien lo cerraría—, porque la barra
  también está alineada abajo. Dentro de la banda no puede solaparse con nada: la banda lo mide como
  un hijo más, crece mientras está abierto y devuelve la altura al cerrarse, que es además lo que
  hace la forma que se copia.
- **El ámbito de un grupo del engranaje es la serie para un episodio y el archivo para todo lo
  demás.** Es el mismo par que `ApplyPlaybackPreferences` lee al abrir, y las dos mitades tienen que
  coincidir o el panel escribiría en una fila que nadie vuelve a mirar. Coincide además con lo que
  una persona quiere: diez episodios oscuros de una serie son una decisión, y una película es la
  suya.
- **La barra conserva parar y los dos saltos.** La frase «la barra queda con reproducir, volumen,
  engranaje, miniatura y pantalla completa» enumera lo que queda **de los controles que esta decisión
  mueve** —los cinco botones de panel y la velocidad—, y nada en el razonamiento de arriba pide
  quitar controles de transporte. Quitarlos sería una pérdida que ninguna sección de este documento
  justifica.

**Y una cifra de este documento estaba por comprobar**: «los catorce». El estilo de subtítulos y los
atajos se contaban aquí como dos de los cinco grupos del reproductor **y** como dos de las nueve
secciones de Ajustes, cuando entonces vivían sólo en Ajustes. Se midió el 2026-09-13 al escribir la
lista cerrada, **y no eran catorce**: la enmienda siguiente lo cuenta.

### Enmienda del 2026-09-13, al escribir la lista cerrada

Cuatro correcciones, y las cuatro salen de que la puerta de `UX-010` enumerara el árbol en vez de que
alguien lo leyera. Ninguna cambia la decisión; tres cambian hechos que este documento afirmaba.

- **Son doce grupos, no catorce.** Cuatro en el reproductor —imagen, estilo de subtítulos, cuenta
  atrás del siguiente episodio y detección de segmentos— y ocho en Ajustes: apariencia, idioma,
  escaneo, recomendaciones, atajos, ciclo de vida, privacidad y actualizaciones. **La cifra vive en
  `Groups`, dentro de `OptionGroupTests`, y de ahí se lee**; escribirla en un documento fue justo lo
  que produjo el catorce.
- **Los controles de restablecer eran cuatro, no dos, y uno estaba en una sección de ajustes.**
  Faltaban `ShortcutSettingsRestore` —«Restaurar valores iniciales», en los atajos— y él solo
  desmiente las dos mitades de la frase del contexto. Es además el más engañoso de los cuatro: su
  botón ya se llama `RestoreDefaultsButton`, así que parece cumplir la regla y no la cumple. Que la
  puerta afirme **la clave** y no el nombre ni el texto es lo único que lo distingue.
- **«La superficie del reproductor» no puede bajar ni llevar botón.** No es una sección ni un grupo:
  es una fila de sólo lectura sin un solo mando, y su propio comentario en el marcado dice que es
  «una decisión y no una preferencia». Su sitio entre las tres que bajan lo ocupa **el estilo de
  subtítulos**, que sí se juzga mirando la imagen y que este documento ya contaba entre los grupos
  del reproductor.
- **Ni las raíces de medios ni las copias son grupos**, así que `UX-010` **no necesita ninguna
  confirmación destructiva** — lo contrario de lo que su criterio de aceptación prometía. Ninguna de
  las dos guarda una sola preferencia: son datos y acciones. «Devolver las carpetas a sus valores de
  fábrica» sería vaciar la biblioteca detrás de la palabra más inocente, que es la misma trampa que
  este documento rechaza al descartar un «restaurar todo» global. La lista cerrada las **rechaza por
  escrito**, porque un candidato ausente se lee igual que uno olvidado.

**Y una restricción mecánica gobierna dónde puede vivir un grupo**, medida y no razonada: el paseo
autónomo resuelve un control por su nombre accesible y **falla ante dos coincidencias visibles**.
Como los doce botones dicen lo mismo por la misma clave, dos grupos alcanzables a la vez son una
escena que no se puede pulsar. Por eso el idioma sale de Apariencia a una entrada propia en vez de
quedarse como una tarjeta dentro de ella.

### Alternativas consideradas

- **Un menú emergente, como el de YouTube.** Rechazada por el mecanismo: deja el contenido fuera del
  alcance del paseo. La forma se conserva; el contenedor no.
- **Un panel con pestañas verticales.** Todo a un clic y sin navegar, pero la columna de etiquetas
  ocupa ancho y deja de caber cuando los grupos crecen — y van a crecer, porque tres bajan de
  Ajustes.
- **Una hoja inferior sobre la barra.** Tapa la parte baja del vídeo, que es donde van los subtítulos.
- **Un «restaurar todo» global en vez de uno por grupo.** Rechazada: es la acción que más cuesta
  deshacer y la que menos avisa de su alcance.

---

## English

### Context

On 2026-09-12 the owner asked for the picture options to move into the player, and added in the same
conversation that **the rest of that view's settings should move too**, the way YouTube's player
does it. Counting what existed gave the measure: **nine settings sections** and **five groups already
in the player**, with no written rule saying why any of them was where it was; and **two reset
controls in the whole application**, neither of them in a settings section. **Both halves of that
turned out to be false** when measured on 2026-09-13 — see the final amendment.

### Decision

**An option decided while watching lives in the player; one configured once lives in Settings.** And
the place in the player is **a panel drawn over the video with two-level navigation — the second
replaces the first — never a popup menu.** The shape is YouTube's; the mechanism cannot be.

The mechanism's reason is measured and belongs to this tree: nothing inside a `Flyout` is reachable
by the automated walk, because its content lives in a separate popup root. Today's panels use
`IsVisible` for exactly that reason.

**Every group of options, wherever it lives, carries its «Restore default values» inside it** — in
the group and not in a separate menu — and all twelve use **the same resource key**, not the same
literal.

### Consequences

Three sections move down from Settings — the next-episode countdown, skipping intros and the subtitle
style; the five existing panels lose their own button on the bar and
move inside the gear. The gate cannot judge the criterion, so what it requires is **the written
decision**: a closed list classifies every group into one of the two places and fails on an
unclassified one. The button's gate is symmetric — a group off the list fails, and a listed group
without its button fails.

### Amendment of 2026-09-12 (evening), while building it

Three refinements, all three measured rather than reasoned. They do not change the decision.

- **«Drawn over the video» means inside the transport band, not floating over the picture.** A 380 px
  panel aligned to the bottom right of the root panel lands **on top of the bar's own right-hand
  end** — over the very buttons somebody would close it with — because the bar is bottom-aligned too.
  Inside the band it cannot overlap anything: the band measures it as one more child, grows while it
  is open and gives the height back when it is not.
- **A gear group's scope is the series for an episode and the file for anything else**, which is the
  same pair `ApplyPlaybackPreferences` reads on the way in. The two sides have to agree or the panel
  would write into a row nothing ever looks at.
- **The bar keeps stop and the two skips.** The sentence listing what the bar is left with enumerates
  what remains **of the controls this decision moves**, and nothing in the reasoning asks for
  transport controls to go.

**And one figure here was unverified**: «the fourteen». Subtitle style and shortcuts were counted
both as player groups and as two of the nine settings sections, while at the time they lived only in
Settings. It was measured on 2026-09-13 when `UX-010`'s closed list was written, **and it was not
fourteen**.

### Amendment of 2026-09-13, while writing the closed list

Four corrections, all four from letting the gate enumerate the tree instead of somebody reading it.
None changes the decision; three change facts this document asserted.

- **There are twelve groups, not fourteen.** Four in the player — picture, subtitle style,
  next-episode countdown and segment detection — and eight in Settings: appearance, language,
  scanning, recommendations, shortcuts, lifecycle, privacy and updates. **The figure lives in
  `Groups`, inside `OptionGroupTests`, and is read from there**; writing it into a document is
  exactly what produced the fourteen.
- **There were four reset controls, not two, and one of them was in a settings section.**
  `ShortcutSettingsRestore` — «Restaurar valores iniciales», on the shortcuts — was missing, and on
  its own it disproves both halves of the context's sentence. It is also the most deceiving of the
  four: its button is already called `RestoreDefaultsButton`, so it looks like it obeys the rule and
  does not. That the gate asserts **the key** rather than the name or the text is the only thing
  that tells them apart.
- **«The player's surface» can neither move down nor carry a button.** It is not a section and not a
  group: it is a read-only row with no control at all, and its own comment in the markup says it is
  «a decision rather than a preference». Its place among the three that move down is taken by **the
  subtitle style**, which is judged against the picture and which this document already counted
  among the player's groups.
- **Neither the media roots nor the backups are groups**, so `UX-010` **needs no destructive
  confirmation at all** — the opposite of what its acceptance criterion promised. Neither stores a
  single preference: they are data and actions. «Returning the folders to their factory values»
  would empty the library behind the most innocent word, which is the same trap this document
  rejects when it turns down a global «reset everything». The closed list **rejects them in
  writing**, because an absent candidate reads exactly like a forgotten one.

**And a mechanical restriction governs where a group may live**, measured rather than reasoned: the
autonomous walk resolves a control by its accessible name and **fails on two visible matches**. Since
all twelve buttons say the same thing through the same key, two groups reachable at once are a scene
that cannot be clicked. That is why the language moves out of Appearance into a destination of its
own rather than staying as a card inside it.

### Alternatives considered

A popup menu like YouTube's, rejected for the mechanism; a panel with vertical tabs, rejected because
the label column stops fitting as groups grow; a bottom sheet, rejected because it covers where
subtitles go; and a single global «reset everything», rejected as the hardest action to undo and the
one that warns least about its own reach.
