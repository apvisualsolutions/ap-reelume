# ADR-0007 — Todo elemento es idéntico al prototipo / Every Element Matches the Prototype

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-09-01
- Decisor / Decision owner: Product Owner
- Relacionado / Related: [`FEATURES.md`](../FEATURES.md),
  [CRS-004](../evidence/stable/CRS-004-lessons-panel.md), `design/AP Reelume.dc.html`,
  `tests/ApSolutions.LocalMedia.UiTests/Theme/ButtonShapeTests.cs`

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

El 2026-08-25 el propietario dijo: «todos los botones o son redondos o son píldoras, pero nunca
cuadrados». Se escribió una puerta —`ButtonShapeTests`— y se cambiaron dos clases para cumplirla:
`Button.player-chrome`, que pasó del radio medio a la píldora, y `Button.player-pill`, que pasó del
pequeño a la píldora.

El 2026-09-01, construyendo el panel «Lecciones» (`CRS-004`), apareció una tercera clase que la
incumplía: la fila de lección, que el prototipo dibuja con `borderRadius: 7`. Preguntado si ampliar
la excepción o forzar la píldora, el propietario **retiró la regla**:

> «Esa afirmación mía era equivocada, los botones deben ser al igual que todos los elementos de la
> app, idénticos al 100 % al prototipo, deberás usar todos los métodos disponibles para asegurarte de
> eso.»

### Lo que se midió al retirarla

La regla no era sólo discutible: **había apartado del diseño las dos clases que cambió**, no
acercado.

| Clase | Control del prototipo | Diseño | Con la regla | Escrito |
|---|---|---:|---:|---:|
| `Button.player-chrome` | `pbtn` | 8 | 999 | **8** |
| `Button.player-pill` | `pbtnAudio`…`pbtnLessons` | 4 | 999 | **4** |
| `Button.lesson-row` | la fila de lección | 7 | — | **7** |
| `Button, ToggleButton` | `btnPri`, `btnSec` | 999 | 999 | 999 |

Sólo la clase base coincidía por casualidad. **Una regla dicha de memoria le ganó durante una semana
a un diseño que nadie volvió a leer**, que es el defecto característico de este repositorio aplicado
a una decisión en vez de a un servicio.

**Enmienda del 2026-09-01: fueron siete clases, no dos.** Esta tabla nombra las dos que su autor
recordaba. Comparado el archivo de tokens contra el commit anterior a la regla, la lista completa
añade `action-row` en sus dos formas —de `CornerRadiusSmall` a píldora, con el diseño en 5—, los dos
botones del riel —de `CornerRadiusMedium` a píldora, con el diseño en 12— y `colour-cell`, cuyo
comentario siguió diciendo «Square rather than round» encima de un setter que escribía la píldora.
**Una regla se recuerda por donde dolió, no por donde llegó**, que es la misma razón por la que la
enmienda de abajo existe.

### Decisión

**El prototipo es la fuente de la forma.** Un elemento de la aplicación se parece a su control del
prototipo, y cuando una regla general y el diseño discrepan, **manda el diseño**.

Consecuencias que esto acepta explícitamente:

1. **Un valor sin token se escribe como literal.** La escala de radios tiene tres tokens —4, 8 y la
   píldora— y el prototipo usa nueve valores distintos. Redondear 7 al token más cercano dibuja una
   forma que el diseño no tiene, así que se escribe 7. `ScalarTokenTests` sigue prohibiendo escribir
   un número **que tenga token**, que es otra cosa.
2. **No hay una gramática de formas por encima del diseño.** «Redondo o píldora» era una gramática de
   esas; se retira y no se sustituye por otra.

### Lo que NO cambia esta decisión

**El objetivo de 44 px de `player-chrome` se queda**, aunque el prototipo dibuje `pbtn` a 36×36. Es
el tamaño de objetivo accesible, es una decisión anterior y distinta —de accesibilidad, no de forma—,
y encogerlo cambiaría un suelo medible por ocho píxeles. **El radio nunca fue lo que separaba ese
control del diseño; el tamaño sí, y a propósito.**

Una discrepancia con el prototipo que exista **por accesibilidad medida** es legítima y se escribe
junto al control. Lo que esta decisión prohíbe es la discrepancia por gramática inventada.

### Cómo se hace cumplir

`ButtonShapeTests` deja de afirmar una forma y afirma **la correspondencia**, en dos mitades que no
pueden caducar de la misma manera:

1. **El árbol dibuja lo que la tabla dice**: cada clase emparejada con su control del prototipo.
2. **La tabla dice lo que el diseño dibuja**: el radio se **lee** de `design/AP Reelume.dc.html`, no
   se repite.

Sin la segunda mitad la tabla sería otra vez un número copiado a mano, que es exactamente cómo la
regla retirada sobrevivió una semana.

### La enmienda del 2026-09-01: un radio no puede ser una preferencia

Emparejar las diez clases restantes destapó que **ninguna clase de este árbol dibujaba su radio**,
incluidas las dos que la tabla de arriba daba por devueltas al diseño. `AppearanceService` escribía
el ajuste «Redondeo de esquinas» sobre `CornerRadiusMedium` y `CornerRadiusSmall`, y el contenedor lo
resuelve antes de construir superficie alguna: `player-chrome` dibujaba **10** donde el diseño dibuja
8, y `player-pill` **5** donde dibuja 4. La tabla no mentía sobre el diseño; mentía sobre el árbol.

El prototipo gasta esa preferencia **en un solo elemento**, `artBox`, que es la carátula. Así que
esta decisión gana una tercera consecuencia:

3. **Un radio que el diseño fija no puede depender de una preferencia.** Una preferencia sólo alcanza
   los elementos que el prototipo le da; cualquier otro alcance convierte el número de diseño en un
   valor por defecto, y entonces ningún elemento puede afirmar que dibuja el suyo. La preferencia
   vive ahora en `PosterCornerRadius`, con la carátula y su esqueleto como únicos consumidores.

### Lo que queda pendiente

**Los catorce botones están emparejados y el resto de la superficie no.** La puerta lleva ahora un
censo: toda clase de botón está emparejada con un control del prototipo o escrita en una lista
cerrada de las que el diseño no contesta —`colour-cell`, `rating-choice`, `icon-action` y
`link-action`—, cada una con lo que se buscó y no existe.

**Enmienda del 2026-09-02: la lista cerrada admite una quinta entrada por un motivo distinto, y la
puerta medía el control equivocado.** Las tres listas del panel del reproductor pasaron a ser filas
de radios, y `RadioButton.option` entró en esa lista **sin que el diseño calle**: el prototipo dibuja
esa fila y le da `borderRadius: 4`. Lo que ocurre es que **la superficie que dibuja la esquina no es
el control** — medido ese día, la plantilla base del `RadioButton` construye tres elipses y un
presentador de contenido y **ningún `Border`**, así que un `CornerRadius` puesto en la clase es un
número que nada lee. El 4 lo lleva el `Border` de la fila y lo mide `OptionRowShapeTests`. La lista
sigue siendo cerrada y sigue exigiendo una razón escrita; lo que se aprende es que hay **dos** clases
de razón, y confundirlas dejaría un elemento del diseño sin emparejar creyendo que el diseño no lo
dibuja.

**Y la puerta de este ADR medía un control que no era el suyo.** `Corner(kind, class)` construía un
`Button` para cualquier tipo que no fuese `ToggleButton`, así que la primera medición de
`RadioButton.option` leyó **999** —la píldora de un `Button`— donde un `RadioButton` real dibuja
**3**. Corregido con un `switch` que se niega ante un tipo que no sabe construir, y con una línea que
ata el tipo de cada fila al que nombra su selector. **Una puerta puede medir el control equivocado y
dar un número perfectamente creíble**, que es esta misma decisión aplicada a su propia herramienta.

**Y la decisión no se limita a los botones**: dice «todos los elementos». Los `Border`, `TextBox` y
`ComboBox` gastan los mismos tres tokens mientras el prototipo dibuja doce radios distintos.

### La enmienda del 2026-09-03 (radios): el número no basta para emparejar, y varios de los doce son el mismo

Emparejadas las diez superficies que declaran esquina propia, **cinco dibujaban un radio que el
diseño no dibuja** —la fila de ajustes y la tarjeta del candidato en 8 contra 10, la etiqueta de
estado y el distintivo de la carátula en una caja redondeada contra una píldora, y la fila de la
lista lateral en 4 contra 7—. `SurfaceCornerTests` las sujeta con el mecanismo de este ADR.

**Y aparece una precisión que la tabla de los botones no había necesitado: emparejar por número es
emparejar mal.** Los radios pequeños del diseño tienen dos significados según dónde estén —7 es la
fila de la lista lateral **y** la mitad del botón de 14 px que corre dentro de un interruptor; 10 es
la fila de ajustes **y** la mitad del carril de 40×20 de ese mismo interruptor—, así que una tabla
ordenada por el número emparejaría una fila con un botón **siendo perfectamente coherente consigo
misma**.

**Además, «doce radios distintos» cuenta de más.** Varios son una sola decisión —la píldora— escrita
como la mitad de la altura que toque: 26 en un círculo de 52, 16 en un botón de 32, 15 en uno de 30.
Tratarlos como escalones distintos infla el trabajo e invita a inventar una escala que el diseño no
tiene, que es la gramática que la decisión de arriba retiró.

**Lo que queda, escrito como número y no como advertencia**: la puerta habla de **clases**, y una
vista que escribe el radio en su propio marcado le es invisible. El 2026-09-03 había **86 sitios así
en treinta vistas**. Emparejarlos no es trabajo de clase —cada uno pertenece a un elemento de una
pantalla concreta— así que va vista por vista, y mientras tanto lo sujeta un trinquete que sólo puede
encoger.

### La enmienda del 2026-09-03: la versalita no era una, y la puerta que la mide tiene un lado ciego

Lo mismo que esta decisión encontró en los radios volvió a aparecer en la tipografía, y con la misma
forma: **el árbol gastaba tres clases donde el diseño dibuja nueve combinaciones** de tamaño, peso y
separación entre letras. Emparejadas una a una, seis tienen sitio en este árbol y las otras tres no
—una es andamiaje del propio prototipo, otra pertenece a un menú que aquí no existe y la tercera la
pinta la plantilla de un desplegable—.

**El mecanismo es el de esta decisión sin cambios**: cada clase emparejada con su elemento del
prototipo, y el número **leído** del diseño en vez de repetido. Lo hace cumplir `OverlineTests`.

**Lo que añade es una tercera clase de razón para no emparejar.** Los botones tenían dos —el diseño
calla, o la superficie que dibuja no es el control—. Aquí aparece la que faltaba: **el diseño habla y
este árbol no tiene dónde decirlo**, porque la superficie no existe todavía. «El diseño no lo dibuja»
sería falso sobre las cuatro entradas de esa lista, así que se escribe como lo que es.

**Y una consecuencia sobre las puertas de este ADR, que conviene tener a mano**: el censo de la
versalita encuentra los sitios donde **el árbol** dibuja mayúsculas, así que un sitio que el diseño
dibuja en versalita y el árbol dibuja plano **es invisible para él**. Nada mide esa dirección. Se
lleva a mano en la lista cerrada, y una entrada es el único registro de que alguien miró — que es
exactamente la situación en la que este ADR encontró diez clases de botón.

### La enmienda del 2026-09-05: paridad es «no peor que el prototipo», no «idéntico»

**Esta decisión nunca contestó qué pasa cuando la aplicación tiene ALGO MÁS**, y la comparación
pantalla a pantalla lo convirtió en la pregunta más frecuente. Todo lo escrito arriba trata
discrepancias de **forma** —un radio, una versalita, un tamaño— donde los dos dibujan lo mismo de
manera distinta. Nada decía qué hacer con una pantalla que dibuja algo que el prototipo no dibuja.

Preguntado el 2026-09-05 con la lista delante, el propietario contestó: **«no peor que el
prototipo»**.

**Lo que eso cambia.** Un elemento que la aplicación tiene y el prototipo no **deja de ser una
desviación que justificar una a una**. Hasta hoy lo era, y se pagaba caro: cada comparación volvía a
levantar los mismos añadidos —el botón de detener, el panel «Otras versiones», las iniciales sobre la
carátula— y cada uno necesitaba su párrafo. Eran ya **añadidos deliberados** por decisión del
propietario del 2026-08-25; esta enmienda generaliza aquel precedente en vez de repetirlo.

**Lo que NO cambia, y es la mitad que sostiene el ADR entero.** La forma de lo que los dos dibujan
sigue siendo **idéntica**: un radio, un tamaño de letra o un color de lo compartido no es negociable
por «es que así queda mejor», y todas las puertas de arriba siguen exigiéndolo. La gramática
inventada sigue prohibida. Esta enmienda toca **qué elementos existen**, no **cómo se dibujan los
que existen**.

**Y «no peor» tiene un lado que sigue siendo defecto**, o la regla no serviría para nada:

1. **Menos que el prototipo es un defecto.** Una promesa que el diseño dibuja y la aplicación no
   cumple no se cierra apelando a esta enmienda. Es el caso de los dieciocho hallazgos de
   [lo construido que ninguna pantalla enseña](../evidence/stable/audit-built-and-not-drawn.md).
2. **Un añadido que empeora algo medible es un defecto.** Si cuesta accesibilidad, contraste,
   rendimiento o una regla de este árbol, «lo tiene de más» no lo salva — lo condena.
3. **Un añadido sigue necesitando su razón escrita junto al control**, exactamente como una
   discrepancia por accesibilidad medida. Lo que se retira es la obligación de **justificar su
   existencia** ante el diseño; no la de decir qué hace y por qué.

**El criterio operativo para comparar**, que es para lo que se escribe esta enmienda:

| Lo que se ve | Veredicto |
|---|---|
| Los dos lo dibujan, de forma distinta | **Defecto** salvo cesión escrita |
| El prototipo lo dibuja y la aplicación no | **Defecto** |
| La aplicación lo dibuja y el prototipo no, sin coste medible | **No es hallazgo** — se anota y sigue |
| La aplicación lo dibuja y el prototipo no, con coste medible | **Defecto**, y el coste se escribe |

**Qué la motivó, con nombres.** En la comparación del 2026-09-05 la aplicación resultó ir **por
delante** del prototipo en cuatro sitios: sus atajos se pueden cambiar y el prototipo los dibuja
fijos; los subtítulos tienen un editor de estilo completo con previsualización donde el prototipo
pone cuatro ajustes; el editor de metadatos ofrece tres acciones donde el prototipo una; y la bandeja
avisa de su propia dependencia, cosa que el prototipo no hace. Con la regla anterior, los cuatro eran
desviaciones a justificar. Con ésta, son lo que son.

---

## English

### Context

On 2026-08-25 the owner said: "todos los botones o son redondos o son píldoras, pero nunca
cuadrados". A gate was written — `ButtonShapeTests` — and two classes were changed to obey it:
`Button.player-chrome`, moved from the medium radius to the pill, and `Button.player-pill`, moved
from the small one to the pill.

On 2026-09-01, while building the "Lessons" panel (`CRS-004`), a third class broke it: the lesson
row, which the prototype draws at `borderRadius: 7`. Asked whether to widen the exception or force
the pill, the owner **withdrew the rule**:

> "Esa afirmación mía era equivocada, los botones deben ser al igual que todos los elementos de la
> app, idénticos al 100 % al prototipo, deberás usar todos los métodos disponibles para asegurarte de
> eso."

### What was measured on withdrawing it

The rule was not merely arguable: **it had moved the two classes it changed away from the design**,
not towards it.

| Class | Prototype control | Design | Under the rule | Written |
|---|---|---:|---:|---:|
| `Button.player-chrome` | `pbtn` | 8 | 999 | **8** |
| `Button.player-pill` | `pbtnAudio`…`pbtnLessons` | 4 | 999 | **4** |
| `Button.lesson-row` | the lesson row | 7 | — | **7** |
| `Button, ToggleButton` | `btnPri`, `btnSec` | 999 | 999 | 999 |

Only the base class agreed by coincidence. **A rule stated from memory beat a design nobody re-read
for a week**, which is this repository's characteristic defect applied to a decision rather than to a
service.

**2026-09-01 amendment: it was seven classes, not two.** This table names the two its author
remembered. Diffed against the commit before the rule, the full list adds `action-row` in both its
forms — from `CornerRadiusSmall` to the pill, with the design at 5 — the rail's two buttons — from
`CornerRadiusMedium` to the pill, with the design at 12 — and `colour-cell`, whose comment went on
saying "Square rather than round" above a setter writing the pill. **A rule is remembered by where it
hurt, not by where it reached**, which is why the amendment below exists at all.

### Decision

**The prototype is the source of shape.** An element of the application matches its prototype
control, and where a general rule and the design disagree, **the design wins**.

Consequences this accepts explicitly:

1. **A value with no token is written as a literal.** The radius scale has three tokens — 4, 8 and
   the pill — and the prototype uses nine distinct values. Rounding 7 to the nearest token draws a
   shape the design does not have, so 7 is written. `ScalarTokenTests` still forbids writing a number
   **that has a token**, which is a different thing.
2. **There is no grammar of shapes above the design.** "Round or pill" was one such grammar; it is
   withdrawn and not replaced.

### What this decision does not change

**`player-chrome`'s 44 px target stays**, even though the prototype draws `pbtn` at 36×36. It is the
accessible target size, it is an earlier and separate decision — accessibility, not shape — and
shrinking it would trade a measurable floor for eight pixels. **The radius was never what set that
control apart from the design; the size was, deliberately.**

A divergence from the prototype that exists **for measured accessibility** is legitimate and is
written beside the control. What this decision forbids is divergence by invented grammar.

### How it is enforced

`ButtonShapeTests` stops asserting a shape and asserts **the correspondence**, in two halves that
cannot go stale the same way:

1. **The tree draws what the table says**: each class paired with its prototype control.
2. **The table says what the design draws**: the radius is **read** from `design/AP Reelume.dc.html`
   rather than restated.

Without the second half the table would be a hand-copied number again, which is exactly how the
withdrawn rule survived a week.

### The 2026-09-01 amendment: a radius cannot be a preference

Pairing the ten remaining classes uncovered that **no class in this tree was drawing its radius**,
including the two the table above reported returned to the design. `AppearanceService` wrote the
"Corner rounding" setting over `CornerRadiusMedium` and `CornerRadiusSmall`, and the composition root
resolves it before any surface is built: `player-chrome` drew **10** where the design draws 8, and
`player-pill` **5** where it draws 4. The table was not lying about the design; it was lying about
the tree.

The prototype spends that preference on **one element**, `artBox`, the cover. So this decision gains
a third consequence:

3. **A radius the design fixes cannot depend on a preference.** A preference reaches only the
   elements the prototype gives it; any wider reach turns the design number into a default, and then
   no element can claim to draw its own. The preference now lives in `PosterCornerRadius`, with the
   cover and its skeleton as its only consumers.

### What remains

**The fourteen buttons are paired and the rest of the surface is not.** The gate now carries a
census: every button class is paired with a prototype control or written into a closed list of those
the design does not answer — `colour-cell`, `rating-choice`, `icon-action` and `link-action` — each
with what was searched for and did not exist.

**Amendment of 2026-09-02: the closed list takes a fifth entry for a different reason, and the gate
was measuring the wrong control.** The player panel's three lists became rows of radios, and
`RadioButton.option` joined that list **without the design being silent**: the prototype draws that
row and gives it `borderRadius: 4`. What happens is that **the surface drawing the corner is not the
control** — measured that day, the base theme's `RadioButton` template builds three ellipses and a
content presenter and **no `Border` at all**, so a `CornerRadius` set on the class is a number nothing
reads. The 4 is carried by the row's `Border` and measured by `OptionRowShapeTests`. The list stays
closed and still demands a written reason; what is learnt is that there are **two** kinds of reason,
and confusing them would leave an element of the design unpaired in the belief that the design does
not draw it.

**And this ADR's own gate was measuring a control that was not its own.** `Corner(kind, class)` built
a `Button` for any kind that was not `ToggleButton`, so the first measurement of
`RadioButton.option` read **999** — a `Button`'s pill — where a real `RadioButton` draws **3**.
Corrected with a `switch` that refuses a kind it cannot build, and with a line tying each row's kind
to the one its selector names. **A gate can measure the wrong control and return a perfectly
plausible number**, which is this same decision applied to its own tooling.

**And the decision is not limited to buttons**: it says "every element". `Border`, `TextBox` and
`ComboBox` spend the same three tokens while the design draws twelve distinct radii.

### The 2026-09-03 amendment (radii): the number is not enough to pair by, and several of the twelve are the same one

With the ten corner-declaring surfaces paired, **five drew a radius the design does not draw** — the
settings row and the candidate's card at 8 against 10, the state tag and the cover's badge as rounded
boxes against pills, and the side list's row at 4 against 7. `SurfaceCornerTests` holds them with
this ADR's mechanism.

**And a precision the button table never needed appears: pairing by number is pairing wrongly.** The
design's small radii carry two meanings depending on where they sit — 7 is the side list's row **and**
half the 14 px knob inside a switch; 10 is the settings row **and** half that switch's 40×20 track —
so a table keyed on the number would pair a row with a knob **while being perfectly self-consistent**.

**"Twelve distinct radii" also over-counts.** Several are one decision — the pill — written as
whatever half the height happens to be: 26 on a 52 px circle, 16 on a 32 px button, 15 on a 30 px
one. Treating them as separate steps inflates the work and invites a scale the design does not have,
which is the grammar the decision above withdrew.

**What remains, written as a number rather than a caveat**: the gate is about **classes**, and a view
writing its corner into its own markup is invisible to it. On 2026-09-03 there were **86 such sites
across thirty views**. Pairing them is not a class-shaped job, so it goes view by view, held
meanwhile by a ratchet that can only shrink.

### The 2026-09-03 amendment: the overline was not one, and the gate that measures it has a blind side

What this decision found in the radii turned up again in the typography, in the same shape: **the
tree spent three classes where the design draws nine combinations** of size, weight and tracking.
Paired one by one, six have somewhere to live in this tree and three do not — one is the prototype's
own scaffolding, one belongs to a menu that does not exist here, and the third is painted by a
drop-down's template.

**The mechanism is this decision's, unchanged**: each class paired with its prototype control, and
the number **read** from the design rather than restated. `OverlineTests` enforces it.

**What it adds is a third kind of reason not to pair.** The buttons had two — the design is silent,
or the surface drawing it is not the control. Here the missing one appears: **the design speaks and
this tree has nowhere to say it**, because the surface does not exist yet. "The design does not draw
it" would be false about all four entries in that list, so it is written as what it is.

**And a consequence about this ADR's gates worth keeping to hand**: the overline census finds the
places **the tree** draws capitals, so a place the design draws in capitals and the tree draws flat
**is invisible to it**. Nothing measures that direction. It is kept by hand in the closed list, and
an entry is the only record that somebody looked — which is exactly the situation this ADR found ten
button classes in.

### The 2026-09-05 amendment: parity is "no worse than the prototype", not "identical"

**This decision never answered what happens when the application has SOMETHING MORE**, and the
screen-by-screen comparison turned that into the most frequent question. Everything written above
deals with differences of **form** — a radius, an overline, a size — where both draw the same thing
differently. Nothing said what to do with a screen that draws something the prototype does not.

Asked on 2026-09-05 with the list in hand, the owner answered: **"no worse than the prototype"**.

**What that changes.** An element the application has and the prototype does not **stops being a
deviation to justify one by one**. Until today it was, and it cost: every comparison raised the same
additions again — the stop button, the "Other versions" panel, the initials over the artwork — and
each needed its paragraph. They were already **deliberate additions** by the owner's decision of
2026-08-25; this amendment generalises that precedent rather than repeating it.

**What does NOT change, and it is the half that holds the whole ADR up.** The form of what both draw
stays **identical**: a radius, a font size or a colour of what is shared is not negotiable on "it
looks better that way", and every gate above still demands it. Invented grammar stays forbidden.
This amendment touches **which elements exist**, not **how the existing ones are drawn**.

**And "no worse" has a side that is still a defect**, or the rule would be useless:

1. **Less than the prototype is a defect.** A promise the design draws and the application does not
   keep is not closed by appealing to this amendment. That is the case of the eighteen findings in
   [what is built and no screen shows](../evidence/stable/audit-built-and-not-drawn.md).
2. **An addition that makes something measurable worse is a defect.** If it costs accessibility,
   contrast, performance or a rule of this tree, "it has more" does not save it — it condemns it.
3. **An addition still needs its reason written beside the control**, exactly like a difference by
   measured accessibility. What is withdrawn is the duty to **justify its existence** against the
   design; not the duty to say what it does and why.

**The operating rule for comparing**, which is what this amendment is written for:

| What is seen | Verdict |
|---|---|
| Both draw it, differently | **Defect** unless a written concession |
| The prototype draws it and the application does not | **Defect** |
| The application draws it and the prototype does not, at no measurable cost | **Not a finding** — note it and move on |
| The application draws it and the prototype does not, at a measurable cost | **Defect**, and the cost is written |

**What motivated it, with names.** In the 2026-09-05 comparison the application turned out to be
**ahead** of the prototype in four places: its shortcuts can be changed while the prototype draws
them fixed; subtitles have a full style editor with a preview where the prototype puts four settings;
the metadata editor offers three actions where the prototype offers one; and the tray warns about its
own dependency, which the prototype does not. Under the previous rule all four were deviations to
justify. Under this one, they are what they are.
