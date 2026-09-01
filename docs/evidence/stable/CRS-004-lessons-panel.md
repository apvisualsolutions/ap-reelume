<!--
SPDX-FileCopyrightText: 2026 AP Solutions
SPDX-License-Identifier: GPL-3.0-or-later
-->
# CRS-004 — el panel «Lecciones» y la lección siguiente / the "Lessons" panel and the next lesson

**2026-09-01.** El panel ocupa los 320 px de la columna del reproductor y está **ausente** —no
deshabilitado— fuera de una sesión de lección; al terminar una, la cuenta atrás es la de PLY-011 y
ofrece la lección siguiente. / The panel takes the player column's 320 px and is **absent**, not
disabled, outside a lesson session; when one ends, the countdown is PLY-011's and it offers the next
lesson.

## Cómo sabe la sesión que es una lección / How the session knows it is a lesson

**Se le pregunta al archivo, no se lo dice quien abrió la sesión.** `ICourseRepository`
gana `FindLessonByFileAsync`, el espejo de `IEpisodeSequenceRepository.FindByFileAsync`, y
`GetLessonSession` lo usa para devolver el curso entero alrededor de la lección que suena. / The file
is asked rather than the caller trusted.

**La razón está medida y no es de estilo:** la cuenta atrás abre la lección siguiente con
`PlayDetailsRequest(nextFileId, TimeSpan.Zero)` y nada más, y «Retomar el hilo» hace lo mismo desde
Inicio. Un curso que viajase en la petición desaparecería por cada camino que olvidara reenviarlo, y
**el modo de fallo del panel es la ausencia** — se iría en silencio en vez de verse mal, que es el
defecto que este repositorio lleva encontrándose. / The countdown opens the next lesson with nothing
but an id, so a course riding on the request would go quietly missing.

**Y el índice ya existía sin consulta.** `ix_lessons_media_file` está en la migración `0022` desde que
Cursos llegó, y **ninguna consulta del árbol lo usaba**: registrado y nunca alimentado, en su forma
de índice. Ésta es la consulta para la que se creó. / The index existed with nothing querying it.

## Lo que el panel dibuja, contra el prototipo / What the panel draws, against the prototype

| Elemento / Element | Prototipo / Prototype | Aplicación / Application |
|---|---|---|
| Cabecera / Head | `{hechas}/{total} lecciones · {tiempo} restantes` | igual, y sólo la primera mitad al terminar |
| Fila / Row | glifo, nombre, duración | igual, y la fila entera es el botón |
| Glifos / Glyphs | `●` vista, `◐` parcial **o actual**, `○` sin empezar | igual |
| Módulos / Modules | `Módulo N · título`, ausente si no hay | igual |
| Nota / Note | el hilo se guarda solo, cada 5 s | igual |

**`partial \|\| curNow` del prototipo es una decisión y no un detalle**: la lección que está sonando se
dibuja empezada aunque no tenga un solo segundo escrito todavía. Leída sólo del estado sería `○` — la
fila que alguien está viendo dibujada como no empezada. / The row being played never draws as not
started.

## La cadena de la lección siguiente / The next-lesson chain

**La cuenta atrás es la de PLY-011 literalmente, no una copia de su comportamiento.** La espera, la
longitud configurada y la cancelación salen a `ContinuityCountdown`, y las dos cadenas usan **ese
objeto**. La clave de ajuste sigue siendo la de episodios: una persona configura «cuánto tarda en
empezar lo siguiente», no una respuesta para series y otra para cursos, y renombrarla habría dejado
la elección de cada instalación existente atrás en la clave vieja. / One object, one setting key.

`StartNextEpisodeCountdown` conserva su superficie pública entera —`SettingKey`,
`CountdownSeconds`, `ConfigureCountdown`, `Cancel`, `Ticked`, `ExecuteAsync`— y **sus 300 pruebas de
`Application.Tests` siguen en verde sin tocar una sola**. / T28's suite passes untouched.

Lo propio de la cadena de cursos son los dos extremos: qué lección viene después
(`NextLessonPolicy`), y confirmar en cero que su archivo sigue ahí. **La revalidación es una
relectura y no una recomprobación de lo que ya se tenía**, que es la mitad de T28 que una copia
habría dejado fuera por parecer redundante. / The revalidation is a re-read.

`NextEpisodeOutcome` se reutiliza en vez de escribir un enumerado gemelo: las cinco formas de acabar
son las mismas cinco, y `NoNextEpisode` es de donde sale «Curso terminado». / One outcome
enumeration, not two.

## El defecto que apareció al medir la ficha / The defect the ficha's own words uncovered

La ficha promete «sus tres formas de cancelarla». **Medido el 2026-09-01, en la aplicación real no
había ninguna de las tres.**

- La evidencia de T28 dice que se cancela desde teclado, ratón y tecla multimedia. Su prueba
  `Any_input_method_cancels_the_countdown` construye **su propio** `InputCommandRouter` cuyo callback
  llama a `Cancel()`. Prueba el enrutador, no el cableado.
- `ExecutePlaybackInputAsync`, que es el callback **de la aplicación**, no tocaba la cuenta atrás en
  **ninguno** de sus diez brazos.
- La única llamada a `Cancel()` en todo `src/` estaba en `HandleNextEpisodeActionAsync`, que sólo
  invocan los dos botones del overlay.

**Y la consecuencia era peor que «no se cancela»:** pulsar Stop cerraba la sesión mientras la cuenta
atrás seguía corriendo por debajo, de modo que diez segundos después se abría el episodio siguiente
sobre un reproductor que alguien acababa de parar — lo contrario exacto de «Nada se reproduce solo».
/ Stop closed the session and the countdown kept running underneath.

Cableado ahora antes del `switch`, porque no es lo que Stop le hace a la sesión sino lo que cualquier
parada deliberada significa para una oferta en pie. Vale para las dos cadenas. / Wired for both
chains.

## El paseo, que es quien lo prueba de verdad / The walk, which is what really proves it

La escena de Cursos ya abría una lección con el ratón; ahora sigue: **pulsa la píldora «Lecciones»,
abre la columna, y pulsa una fila que no es la que suena**, afirmando que la sesión se mueve a esa
lección. Verde. / Green.

Eso prueba de punta a punta lo que ninguna prueba unitaria ve: que el shell preguntó al catálogo, que
la píldora existe, y que una fila dentro de la columna es alcanzable con un ratón real. El trinquete
del paseo **no sube**. / The walk ratchet does not rise.

## La regla de forma que el propietario retiró / The shape rule the owner withdrew

Preguntado por el radio de la fila —el prototipo la dibuja a 7, y la puerta exigía píldora—, el
propietario **retiró la regla del 2026-08-25**: «esa afirmación mía era equivocada, los botones deben
ser al igual que todos los elementos de la app, idénticos al 100 % al prototipo». / The owner
withdrew the rule.

**Medido contra el diseño el mismo día, aquella regla había apartado dos clases del prototipo:**

| Clase / Class | Control del prototipo | Diseño / Design | Antes / Before | Ahora / Now |
|---|---|---:|---:|---:|
| `Button.player-chrome` | `pbtn` | 8 | 999 | **8** |
| `Button.player-pill` | `pbtnLessons` | 4 | 999 | **4** |
| `Button.lesson-row` | la fila / the row | 7 | — | **7** |
| `Button, ToggleButton` | `btnPri` | 999 | 999 | 999 |

`ButtonShapeTests` deja de afirmar «redondo o píldora» y pasa a afirmar la correspondencia, **en dos
mitades que no pueden caducar igual**: que el árbol dibuja lo que la tabla dice, y que la tabla dice
lo que el diseño dibuja —leyendo el número de `design/AP Reelume.dc.html` en vez de repetirlo—. Sin
la segunda mitad la tabla sería otra vez un número copiado a mano, que es exactamente cómo la regla
retirada sobrevivió una semana. / The gate now asserts the correspondence, in two halves.

**El objetivo de 44 px de `player-chrome` se queda y es una decisión distinta**: el prototipo dibuja
`pbtn` a 36×36, y encogerlo cambiaría un suelo de accesibilidad medible por ocho píxeles de forma. El
radio nunca fue lo que separaba ese control del diseño; el tamaño sí, deliberadamente. / The 44 px
target stays, and it is a separate decision.

## Suites / Suites

| Suite | Resultado / Result |
|---|---|
| `Domain.Tests` | `NextLessonPolicy`, 9 de 9 |
| `Application.Tests` | 300 de 300, con 10 nuevas de la cadena de lecciones |
| `UiTests` | 1.126 de 1.126, con 24 nuevas del panel |
| `ArchitectureTests` | 30 de 30 — los dos servicios nuevos registrados **y** consumidos |
| `DocumentationTests` | 93 de 93 |
| `AccessibilityTests` | la escena de Cursos con el panel dentro, verde |
