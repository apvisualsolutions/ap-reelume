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
  proveedor». Ninguna sección de ajustes se podía deshacer.

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
todo» escondido es donde nacen los sustos. Los catorce usan **la misma clave de traducción**, no el
mismo literal.

### Consecuencias

- **Tres secciones bajan de Ajustes al reproductor**: la superficie del reproductor, la cuenta atrás
  del siguiente episodio y saltar intros. Las otras seis se quedan.
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
controls in the whole application**, neither of them in a settings section.

### Decision

**An option decided while watching lives in the player; one configured once lives in Settings.** And
the place in the player is **a panel drawn over the video with two-level navigation — the second
replaces the first — never a popup menu.** The shape is YouTube's; the mechanism cannot be.

The mechanism's reason is measured and belongs to this tree: nothing inside a `Flyout` is reachable
by the automated walk, because its content lives in a separate popup root. Today's panels use
`IsVisible` for exactly that reason.

**Every group of options, wherever it lives, carries its «Restore default values» inside it** — in
the group and not in a separate menu — and all fourteen use **the same resource key**, not the same
literal.

### Consequences

Three sections move down from Settings; the five existing panels lose their own button on the bar and
move inside the gear. The gate cannot judge the criterion, so what it requires is **the written
decision**: a closed list classifies every group into one of the two places and fails on an
unclassified one. The button's gate is symmetric — a group off the list fails, and a listed group
without its button fails.

### Alternatives considered

A popup menu like YouTube's, rejected for the mechanism; a panel with vertical tabs, rejected because
the label column stops fitting as groups grow; a bottom sheet, rejected because it covers where
subtitles go; and a single global «reset everything», rejected as the hardest action to undo and the
one that warns least about its own reach.
