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

| Clase | Control del prototipo | Diseño | Con la regla | Ahora |
|---|---|---:|---:|---:|
| `Button.player-chrome` | `pbtn` | 8 | 999 | **8** |
| `Button.player-pill` | `pbtnAudio`…`pbtnLessons` | 4 | 999 | **4** |
| `Button.lesson-row` | la fila de lección | 7 | — | **7** |
| `Button, ToggleButton` | `btnPri`, `btnSec` | 999 | 999 | 999 |

Sólo la clase base coincidía por casualidad. **Una regla dicha de memoria le ganó durante una semana
a un diseño que nadie volvió a leer**, que es el defecto característico de este repositorio aplicado
a una decisión en vez de a un servicio.

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

### Lo que queda pendiente

**Sólo cuatro clases están emparejadas.** Siguen sin pareja `action-row`, `navigation-destination`,
`navigation-action`, `poster-card`, `accent-swatch`, `colour-cell`, `segment`, `rating-choice`,
`compact` e `icon-action`. Emparejarlas es una tanda propia, porque tocar la navegación, las tarjetas
y los ajustes es un cambio visual de toda la aplicación y pide su paseo y sus capturas.

**Y la decisión no se limita a los botones**: dice «todos los elementos». Los botones son donde
apareció y donde hay puerta; el resto de la superficie está sin medir contra el diseño.

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

| Class | Prototype control | Design | Under the rule | Now |
|---|---|---:|---:|---:|
| `Button.player-chrome` | `pbtn` | 8 | 999 | **8** |
| `Button.player-pill` | `pbtnAudio`…`pbtnLessons` | 4 | 999 | **4** |
| `Button.lesson-row` | the lesson row | 7 | — | **7** |
| `Button, ToggleButton` | `btnPri`, `btnSec` | 999 | 999 | 999 |

Only the base class agreed by coincidence. **A rule stated from memory beat a design nobody re-read
for a week**, which is this repository's characteristic defect applied to a decision rather than to a
service.

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

### What remains

**Only four classes are paired.** Still unpaired are `action-row`, `navigation-destination`,
`navigation-action`, `poster-card`, `accent-swatch`, `colour-cell`, `segment`, `rating-choice`,
`compact` and `icon-action`. Pairing them is a batch of its own, because touching navigation, cards
and settings is an application-wide visual change and needs its walk and its captures.

**And the decision is not limited to buttons**: it says "every element". Buttons are where it
surfaced and where the gate is; the rest of the surface is unmeasured against the design.
