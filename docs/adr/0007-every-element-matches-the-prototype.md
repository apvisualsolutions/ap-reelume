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
`ComboBox` gastan los mismos tres tokens en ochenta y cinco sitios mientras el prototipo dibuja doce
radios distintos. Eso es la tanda siguiente, y ya puede medirse contra la pantalla.

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
`ComboBox` spend the same three tokens across eighty-five sites while the design draws twelve
distinct radii. That is the next batch, and it can now be measured against the screen.
