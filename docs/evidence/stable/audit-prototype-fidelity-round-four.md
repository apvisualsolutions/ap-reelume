# Verificar no es mirar, y el registro contra el que se verificaba no existía / Verifying Is Not Looking, and the Register It Was Verified Against Did Not Exist

- IDs: `PRD-006`
- Fecha / Date: 2026-09-06
- Alcance / Scope: las 19 parejas miradas en la vuelta anterior, `docs/design/ELEMENTS.es.md`,
  `docs/evidence/stable/audit-built-and-not-drawn.md`, `About/CreditsView`,
  `Settings/RecommendationSettingsView`, `docs/FEATURES.md`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Por qué existe este documento

La vuelta anterior dejó el aparejo fiable y **miró** las diecinueve parejas de superficie navegable,
anotando unos cuarenta candidatos. Ninguno era todavía un defecto: el trabajo que faltaba era
cruzarlos con lo que este árbol ya tiene decidido por escrito, y medir con números los que
sobrevivieran.

Al empezar ese cruce, **el registro contra el que había que cruzar no existía tal como se creía**, y
dos de los cuatro números del único hallazgo geométrico de la vuelta anterior resultaron ser del
arnés y no de la aplicación. Las dos cosas se archivan aquí porque las dos habrían convertido esta
vuelta en una lista de defectos inventados, que es exactamente contra lo que se escribió la anterior.

### 1. Las «treinta y ocho cesiones» son cinco, y el error tiene causa

Las notas de la vuelta anterior mandaban cruzar cada candidato «con las 38 cesiones escritas de
`docs/design/ELEMENTS.es.md` §Las cesiones, con su razón», y cerraban varios candidatos citando «la
cesión 11», «la 12», «la 15» y «la 25». Medido:

| Lo que se afirmaba | Lo que hay |
| --- | --- |
| 38 cesiones numeradas en `ELEMENTS.es.md` | **cinco viñetas, sin numerar** (líneas 373-388 de 388) |
| una numeración global de cesiones | **ninguna**: `cesi[oó]n\s+\d+` no casa en `docs/`, `design/`, `src/`, `tests/` ni `.claude/` |

Las cinco son: los controles miden 36 y no 32 ni 38; el radio de píldora es 999; el acento del borde
de foco se aparta un paso; en los dos altos contrastes el relleno no dice nada; y el aviso de acceso
denegado no lleva el botón «Permisos».

**Y los números citados corresponden a algo, que es lo que hace el error caro.** «Mantener
pendiente» es el punto **4** de la sección «Lo que sigue siendo distinto» de
[la comparación vista a vista](audit-prototype-fidelity-round-three.md); la fila de episodio es el
**5**; las dos acciones de la ficha, el **6**. Las notas concatenaron tres numeraciones locales en
una global imaginaria. No es que el número no exista: es que existe otro con el mismo dígito en otro
sitio, que es la forma más cara de estar equivocado — una cita así se comprueba, se encuentra algo, y
se da por buena.

**La causa raíz es la palabra, no la aritmética.** «Cesión» no es un término definido en este árbol:
se usa en cuatro sentidos distintos —las cinco viñetas de `ELEMENTS.es.md`, un veredicto del
`ADR-0007` («**Defecto** salvo cesión escrita»), cualquier compromiso medido del changelog, y
punteros del relevo que unas veces apuntan a `ELEMENTS` y otras a una prueba—. Suena a registro
cerrado y numerable, y es un adjetivo. Cualquiera que lea «esas cesiones están escritas al final con
su razón» y luego vea el ADR usarla como veredicto deduce un registro que nunca existió.

**Las razones sí existen, repartidas en siete casas**, y ésa es la lista contra la que se cruza:

| Casa | Qué guarda |
| --- | --- |
| `docs/design/ELEMENTS.es.md` §cesiones | las cinco formales |
| el resto de `ELEMENTS.es.md` | las razones pegadas a su control, una por ficha |
| `audit-prototype-fidelity-round-three.md` | las diferencias que se quedan, numeradas 1-6 y 7-8 dentro de su sección |
| `PRD006-parity-matrix.md` | la desviación deliberada de que «Aplicar» no existe |
| las listas cerradas de `tests/` | `ButtonShapeTests` y `OrphanedResourceTests`, cada entrada con lo que se buscó y no existe |
| `design/README.md` | las equivalencias vista↔pantalla del prototipo |
| `docs/adr/` | 0007 con sus enmiendas, 0009 y 0010 |

**Y la causa raíz tiene guarda, porque una frase no dispara.** `EvidenceLinkTests` se niega desde hoy
a que **ningún documento del árbol cite una cesión por número**. Hoy son cero coincidencias, así que
nace verde con un suelo real —al menos cien documentos leídos, o el barrido está midiendo la nada— y
se pone roja el día que alguien reinvente la numeración.

**Nació roja sobre un caso real, y no era el previsto**: cazó **este mismo documento**, que cita las
cuatro citas falsas porque archivarlas es para lo que existe. La salida no fue aflojar el patrón sino
una lista cerrada de un elemento con su razón dentro de la prueba, que es como este árbol resuelve ya
`ButtonShapeTests` y `OrphanedResourceTests`. Comprobada después en las dos direcciones: un documento
canario que cita «la cesión 25» se nombra con su línea y su texto, y el árbol sin él calla.

**Y su lado ciego va escrito dentro, no escondido**: las notas que hicieron la afirmación viven
**fuera del repositorio**, bajo el perfil de quien programa. Ninguna puerta de aquí puede verlas. Lo
que esta guarda impide es que la invención **llegue al árbol**; no impide que la próxima vuelta la
vuelva a escribir en sus notas.

### 2. El hallazgo geométrico de la vuelta anterior estaba mal en dos de sus cuatro números

La vuelta anterior escribió que la aplicación dibuja tarjetas de **146 px** donde el prototipo pone
**154**, con el contenido empezando en **x = 130** frente a **x = 98**, y **siete tarjetas por fila
en vez de ocho**. Medido de nuevo, contando tarjeta y hueco por luminosidad sobre una fila lisa —las
carátulas son degradados, así que un recorrido por color constante las parte en decenas de
fragmentos—:

| Medida | Prototipo @1500 | Aplicación @1500 | Prototipo @1600 |
| --- | --- | --- | --- |
| Tarjetas por fila | **8** | **8** | **9** |
| Ancho de tarjeta | 154 | 146 | **145** |
| Hueco | 20 | 20-21 | 20 |
| Riel | 64 px | ~62 px más 8 de cromo | 64 px |
| Margen entre el riel y la primera tarjeta | **32 px** | **56 px** | 32 px |

**Lo primero que cae es la columna que faltaba: son ocho en las dos.** No hay ninguna columna
perdida.

**Lo segundo es más útil: el prototipo no tiene una tarjeta de 154 px.** Su rejilla es **fluida**, y
154 es lo que sale de repartir 1500 px entre ocho columnas. A 1600 —que es el ancho que
`ELEMENTS.es.md` declara canónico para los números del prototipo, «CSS sobre una página de 1600
px»— la misma vista pone **nueve** tarjetas de **145**, es decir *menos* que la aplicación. Comparar
146 contra 154 como si fueran dos constantes del diseño es comparar dos resultados de una fórmula a
un ancho arbitrario.

**Lo que sí queda, y es el defecto real: el margen.** El prototipo deja **32 px** entre el riel y la
primera tarjeta y la aplicación deja **56**. Los 8 px de tarjeta y los 32 de contenido desplazado son
la consecuencia aritmética de eso, no tres hallazgos distintos.

**El arnés estaba comparando a 1500 los dos lados, que es correcto**, así que el error no fue del
arnés: fue leer un número fluido como si fuera un número del diseño. La prueba que lo destapa es
barata y ahora está escrita: **rehacer la vista del prototipo a otro ancho y ver si el número se
mueve.** Si se mueve, no es un número del diseño.

**Y una trampa de método de la vuelta anterior, medida de paso**: sus capturas archivadas
(`L-app-*`, `L-proto-*`) están a **750 × 500**, ya reducidas a la mitad. Los anchos de tarjeta
salieron de medir ahí y multiplicar por dos. `half.ps1` remuestrea con bicúbica: sirve para **saber
dónde mirar**, nunca para medir. Las capturas nativas de 1500 × 1000 viven al lado y son las que
contestan.

### 3. Los dieciocho hallazgos tenían tres cuentas, y ninguna era la buena

Reconciliadas midiendo uno a uno: son **seis cerrados y doce abiertos**, y la tabla vive en
[la auditoría que los levantó](audit-built-and-not-drawn.md). El doce coincide con el de la hoja de
ruta **por un camino distinto**: ella cuenta entre sus seis cerrados «las portadas en la rejilla»,
que fue el detonante de esa auditoría y no uno de sus dieciocho, y no había visto que el **15** se
cerraba.

**El 15 se cierra porque su premisa es falsa.** Decía que no hay ruta al editor desde la ficha de
serie, y la hay: `Show/ShowDetailsView.axaml:203` monta `TitleActionsView`, que ofrece «Editar
metadatos», «Previsualizar renombrado» y «Revisar duplicados». Lo que queda en pie —que el editor
abre por título y no por episodio— es la diferencia ya escrita con su razón tres días antes.

**Dos cuentas que coinciden no son una cuenta confirmada**, y ése es el corolario que se lleva esta
vuelta.

### 4. El libro de candidatos: entraron 108 y salieron 108 con veredicto

Las notas anotaban «unos cuarenta»; al desglosarlos uno a uno salieron **108**, porque varias líneas
de las notas contenían dos o tres diferencias distintas. Cada uno se cruzó contra el registro de
razones, contra los dieciocho hallazgos y contra la siembra, y sólo después se le asignó una fila del
criterio:

| Veredicto | Cuántos |
| --- | --- |
| `DEFECTO` | **49**, de los que **43** sobreviven a la refutación |
| `YA ESCRITO` — lo cierra una razón del árbol | 32 |
| `NO ES HALLAZGO` — la aplicación tiene de más, sin coste | 22 |
| `NO COMPARABLE` — la siembra no alimenta una de las dos mitades | 5 |
| **Total** | **108** |

**Y el dato que justifica por sí solo haber construido el registro**: de los 32 que se cierran por
razón escrita, **sólo dos se apoyan en `ELEMENTS.es.md`**, que es la única casa que las notas
mandaban consultar. Los otros treinta se apoyan en **comentarios del propio marcado** (8), en la
comparación de la vuelta tres (5), en `design/README.md` (4), en los ADR (6), en las listas cerradas
de las puertas (4), y en la matriz de paridad, la auditoría y el changelog (3). **Cruzar sólo contra
«las cesiones» habría cerrado 2 de 32 y levantado los otros 30 como defectos.**

#### Los cuarenta y tres que se sostienen

**Ficha de película** — el prototipo pone un radio y un «Reproducir» en cada fila de versión y la
aplicación ninguno de los dos; dice la disponibilidad en los dos sentidos donde la aplicación sólo
dibuja algo cuando falta; ofrece «Abrir con reproductor externo» como acción normal, mientras aquí
sólo aparece como rescate tras un fallo; y el mismo botón se llama «Renombrado seguro» allí y
«Previsualizar renombrado» aquí.

**El índice de Ajustes** — doce entradas contra trece: faltan «Audio» y «Accesibilidad». Y encontrado
al verificar, no estaba en las notas: **el orden tampoco es el del prototipo, y el código afirma por
escrito que sí lo es.**

**Privacidad** — el prototipo pinta cada anfitrión en monoespaciada dentro de su caja y la aplicación
en texto corrido; y «Consentir consultas de metadatos» no existe **en ninguna pantalla**, porque aquí
el consentimiento es una variable de entorno.

**Actualizaciones** — falta la fila «Anfitrión declarado» con su explicación; el prototipo pone un
glifo de tono en los quince estados y la aplicación en tres; «Comprobar automáticamente» es casilla
donde el prototipo pone conmutador; y la línea inglesa de las notas de versión se queda en tinta
primaria donde el prototipo la baja a secundaria.

**Atajos de teclado** — falta la fila «Teclas multimedia», y las cuatro teclas están registradas en
el sistema sin que ninguna pantalla las anuncie; el prototipo da dos teclas a reproducir/pausar y la
aplicación una; agrupa por pares donde aquí van cuatro filas sueltas; y dibuja cada tecla como una
tecla y no como texto monoespaciado. **Y uno que no estaba en las notas y ata un cabo suelto: el
prototipo da `N` al minirreproductor y la aplicación `Ctrl+P`** — que es exactamente lo que el
hallazgo 3 de la otra auditoría lleva meses diciendo que el rótulo del reproductor promete y no
cumple. El rótulo copió el prototipo y el atajo no.

**Subtítulos** — faltan «Idioma preferido» y la fila «Subtítulos externos · Confinado», y ninguna de
las dos vive en otra pantalla. Y **la única fila que las dos comparten difiere**: «Tamaño de
subtítulos», 75-200 % en pasos de 5 en el prototipo, contra «Tamaño del texto (porcentaje)»,
50-300 % en pasos de 10 aquí.

**Copias** — falta el deslizador «Copias rotatorias» de 1 a 10, que aquí es un cinco fijo dicho en
prosa; falta la fila que dice **dónde** se escriben las copias, y en su lugar se dibuja la ruta de la
base de datos; y los dos botones compartidos llevan otra palabra en los dos idiomas.

**Biblioteca y escaneo** — el prototipo dice cuántos elementos tiene cada raíz y aquí sólo el tipo;
faltan «Escanear al iniciar», «Ignorar muestras y extras» y la fila «Raíces de medios · N»; la única
conmutación compartida cambia de nombre y pierde la frase que la explica; y los distintivos y el
botón de retirar dicen otras palabras. **Y el más serio de toda la vuelta: la confirmación de
retirada dice lo contrario en cada mitad** — el prototipo promete que el catálogo conserva sus
elementos marcados como no disponibles, y la aplicación avisa de que los títulos salen del catálogo
con sus marcas y su progreso. No es una diferencia de texto: son dos promesas distintas sobre los
datos de quien usa el programa.

**Editor de metadatos** — una columna contra dos, sin la tarjeta con borde que el prototipo dibuja,
el bloqueo de campo como casilla contra botón de estado, sin la frase que explica qué hace el
candado, y cuatro de seis rótulos compartidos que no coinciden.

**Cursos** — la tarjeta del prototipo abre con un panel de imagen 16:9 y la de aquí no tiene imagen
ninguna. **Detección de segmentos** — falta la fila «Lo que nunca hace · Sugiere».

#### Los seis que la refutación tumbó, y por qué importan

Los seis cayeron **por razón escrita, no por error de observación**: lo que describían era cierto.
Dos de ellos son los candidatos estructurales que las notas daban por la diferencia más visible de
toda la superficie de Ajustes —la casilla contra el conmutador, y la densidad como píldoras contra
desplegable—, y los dos tienen su decisión escrita, uno de ellos **en el modelo que alimenta la vista
y no en la vista**, un archivo más allá de donde miraba quien lo anotó. El del color del recuadro
vacío de Cursos cayó además por medición: la aplicación **no puede** pintar el color del prototipo
sin poner tres puertas en rojo.

**Eso es el 12 % de los defectos, y es la razón de que la verificación no sea opcional.**

### 5. Lo que se corrigió, y por qué estas cuatro y no otras

La comparación **registra y no construye** — decisión del propietario del 2026-09-05. Las cuatro que
entraron son las que ya tenían decisión tomada o no admitían espera.

**La atribución de LibVLC, que el prototipo dibuja y la pantalla de Créditos no tenía.** No era
incumplimiento —`NOTICE`, `licenses/` y el fuente correspondiente viajan dentro del paquete—, pero la
LGPL-2.1 §6 pide aviso prominente de que la biblioteca se usa, y quien usa una aplicación de
escritorio no abre el `NOTICE`.

**Y el texto NO es el del prototipo, por decisión del propietario del 2026-09-06.** El prototipo
escribe «LibVLC · LGPL», y «LGPL» no es una licencia: el núcleo es `LGPL-2.1-or-later`, sus
trescientos complementos llevan las suyas —algunas `GPL-2.0-or-later`, como el x264 detrás de
`libx26410b_plugin.dll`— y la biblioteca viaja **sin modificar**. Ésos son los tres hechos que
establece `docs/release/THIRD-PARTY-NOTICES.es.md`, y son los que dice la fila nueva. El `NOTICE`
gana la frase que TMDB ya tenía: que esa atribución también se lee en la aplicación.

**La razón junto al botón «Aplicar umbral».** `PRD006-parity-matrix.md` dice por escrito que
«Aplicar» no existe en este árbol, «porque el prototipo aplica al elegir y un botón cuyo único
trabajo era repetir lo que el control de al lado ya había dicho es la definición de un control de
más». El botón existe, y hasta hoy sin ninguna razón al lado: un control que contradice una regla del
árbol sin decir por qué es justo lo que esas reglas existen para impedir.

**La razón se midió aquí, y ése es el punto.** La salida que se traía escrita era citar los 4 s → 63 s
de una escena del recorrido, y **esa medición es de otra cosa**: pertenece a la franja de avisos —el
enlace de la orden al botón de cancelar, con el escaneo publicando progreso por lote—, no a este
deslizador. Trasplantarla habría metido un número falso en el árbol, que es peor que el botón
desnudo: un botón sin razón se nota, y una razón con número no se vuelve a mirar.

Lo medido aquí: `ConfigureWatchedThreshold.ExecuteAsync` lee **todos** los estados de reproducción
con `GetAllAsync` y reescribe cada uno que cambie de estado; el deslizador va de **50 a 100** con
`TickFrequency="1"` y `IsSnapToTickEnabled`, así que aplicar al elegir barrería la tabla entera
**51 veces** en un solo arrastre de extremo a extremo. La lección del bucle se cita a su fuente como
analogía, no como medición propia.

**El criterio de `PRD-006` prometía «las 53 vistas» y el árbol mide 61.** El 53 sale de contar sólo
los ficheros cuyo nombre acaba en `View`, y ocho superficies no lo hacen —`WatchStatusControl`,
`UnavailableBadge`, `LooseFileBanner`, `MiniPlayerWindow`, `NextEpisodeOverlay`, `SkipMarkerButton`,
`VersionSwitchDialog`, `VideoStatusOverlay`—. **Una fila no puede pasar a `VERIFIED` contra un censo
que ya no la describe**, así que se corrige a 61, y de paso el criterio nombra las **42 pantallas**
del prototipo en vez de «una matriz de capturas» sin número.

**Y la cifra deja de poder caducar en silencio.** `docs/FEATURES.md` entra en la lista de documentos
que vigila `QuotedFigureTests`, que hasta hoy sólo miraba `CLAUDE.md`, `CONTRIBUTING.md` y `.claude/`
— es decir, la matriz canónica de alcance era el único sitio donde una cifra falsa costaba más y
nadie la miraba. Comprobado en las dos direcciones: con el 61 la puerta pasa, y escribiendo 60
contesta `docs/FEATURES.md:36 says 60 for 'medido:vistas', and the tree measures 61`.

---

## English

### Why this document exists

The previous round left the rig trustworthy and **looked at** the nineteen navigable-surface pairs,
noting some forty candidates. None was a defect yet: the work left was to cross them against what
this tree already has decided in writing, and to measure with numbers whichever survived.

On starting that cross-check, **the register to cross against did not exist as believed**, and two of
the four numbers in the previous round's only geometric finding turned out to belong to the rig
rather than to the application. Both are archived here because both would have turned this round into
a list of invented defects — exactly what the previous one was written against.

### 1. The «thirty-eight concessions» are five, and the error has a cause

The previous round's notes required crossing every candidate «against the 38 written concessions in
`docs/design/ELEMENTS.es.md` §The concessions, with their reason», and closed several candidates by
citing «concession 11», «12», «15» and «25». Measured:

| What was claimed | What is there |
| --- | --- |
| 38 numbered concessions in `ELEMENTS.es.md` | **five bullets, unnumbered** (lines 373-388 of 388) |
| a global numbering of concessions | **none**: `cesi[oó]n\s+\d+` matches nothing in `docs/`, `design/`, `src/`, `tests/` or `.claude/` |

The five are: controls measure 36 and not 32 or 38; the pill radius is 999; the focus border's accent
steps aside when it would coincide; in both high contrasts the fill says nothing; and the
access-denied notice carries no «Permissions» button.

**And the cited numbers do correspond to something, which is what makes the error expensive.** «Keep
pending» is point **4** of the «What is still different» section of
[the view-by-view comparison](audit-prototype-fidelity-round-three.md); the episode row is **5**; the
film card's two actions, **6**. The notes concatenated three local numberings into one imaginary
global one. It is not that the number does not exist: it is that another one with the same digit
exists elsewhere, which is the most expensive way to be wrong — a citation like that gets checked,
finds something, and passes.

**The root cause is the word, not the arithmetic.** «Concession» is not a defined term in this tree:
it is used in four different senses — the five bullets of `ELEMENTS.es.md`, a verdict of `ADR-0007`
(«**Defect** unless a written concession»), any measured commitment in the changelog, and handover
pointers that sometimes aim at `ELEMENTS` and sometimes at a test. It sounds like a closed,
numberable register, and it is an adjective.

**The reasons do exist, spread across seven houses**, and that is the list to cross against:

| House | What it holds |
| --- | --- |
| `docs/design/ELEMENTS.es.md` §concessions | the five formal ones |
| the rest of `ELEMENTS.es.md` | the reasons sitting against their control, one per entry |
| `audit-prototype-fidelity-round-three.md` | the differences that stay, numbered 1-6 and 7-8 within their section |
| `PRD006-parity-matrix.md` | the deliberate deviation that «Apply» does not exist |
| the closed lists in `tests/` | `ButtonShapeTests` and `OrphanedResourceTests`, each entry with what was looked for and is not there |
| `design/README.md` | the prototype's view↔screen equivalences |
| `docs/adr/` | 0007 with its amendments, 0009 and 0010 |

**And the root cause has a gate, because a sentence does not fire.** `EvidenceLinkTests` refuses from
today that **any document in the tree cites a concession by number**. Today that is zero matches, so
it is born green with a real floor — at least a hundred documents read, or the sweep is measuring
nothing — and turns red the day somebody reinvents the numbering.

**It was born red on a real case, and not the expected one**: it caught **this very document**, which
quotes the four false citations because archiving them is what it is for. The way out was not to
loosen the pattern but a closed list of one entry with its reason inside the test, which is how this
tree already resolves `ButtonShapeTests` and `OrphanedResourceTests`. Checked afterwards in both
directions: a canary document citing «la cesión 25» is named with its line and its text, and the tree
without it stays quiet.

**And its blind side is written inside rather than hidden**: the notes that made the claim live
**outside the repository**, under the developer's profile. No gate here can see them. What this gate
prevents is the invention **reaching the tree**; it does not prevent the next round writing it again
in its notes.

### 2. The previous round's geometric finding was wrong in two of its four numbers

The previous round wrote that the application draws **146 px** cards where the prototype puts
**154**, with content starting at **x = 130** against **x = 98**, and **seven cards per row instead
of eight**. Measured again, splitting card from gap by brightness along a smooth row — the covers are
gradients, so a constant-colour walk shreds them into dozens of fragments:

| Measure | Prototype @1500 | Application @1500 | Prototype @1600 |
| --- | --- | --- | --- |
| Cards per row | **8** | **8** | **9** |
| Card width | 154 | 146 | **145** |
| Gap | 20 | 20-21 | 20 |
| Rail | 64 px | ~62 px plus 8 of chrome | 64 px |
| Margin between rail and first card | **32 px** | **56 px** | 32 px |

**The first thing to fall is the missing column: there are eight on both sides.** No column is lost.

**The second is more useful: the prototype does not have a 154 px card.** Its grid is **fluid**, and
154 is what comes out of dividing 1500 px among eight columns. At 1600 — the width `ELEMENTS.es.md`
declares canonical for the prototype's numbers, «CSS over a 1600 px page» — the same view puts
**nine** cards of **145**, that is, *less* than the application. Comparing 146 against 154 as if they
were two constants of the design is comparing two results of a formula at an arbitrary width.

**What does remain, and is the real defect: the margin.** The prototype leaves **32 px** between the
rail and the first card and the application leaves **56**. The 8 px of card and the 32 px of shifted
content are the arithmetic consequence of that, not three separate findings.

**The rig was comparing both sides at 1500, which is correct**, so the error was not the rig's: it was
reading a fluid number as if it were a number of the design. The test that uncovers it is cheap and
is now written down: **re-shoot the prototype's view at another width and see whether the number
moves.** If it moves, it is not a number of the design.

**And a method trap of the previous round, measured along the way**: its archived captures
(`L-app-*`, `L-proto-*`) are **750 × 500**, already halved. The card widths came from measuring there
and doubling. `half.ps1` resamples bicubically: it is for **knowing where to look**, never for
measuring. The native 1500 × 1000 captures sit beside them and are the ones that answer.

### 3. The eighteen findings had three counts, and none was right

Reconciled by measuring one by one: **six closed and twelve open**, and the table lives in
[the audit that raised them](audit-built-and-not-drawn.md). The twelve matches the roadmap's **by a
different route**: it counts «the posters in the grid» among its six closed, which triggered that
audit and is not one of its eighteen, and it had not seen that **15** closes.

**15 closes because its premise is false.** It said there is no route to the editor from the show
card, and there is: `Show/ShowDetailsView.axaml:203` mounts `TitleActionsView`, which offers «Edit
metadata», «Preview rename» and «Review duplicates». What remains standing — that the editor opens
per title and not per episode — is the difference already written with its reason three days earlier.

**Two counts that agree are not a confirmed count**, and that is the corollary this round takes away.

### 4. The candidate book: 108 went in and 108 came out with a verdict

The notes recorded «some forty»; broken down one by one they came to **108**, because several lines
of the notes held two or three separate differences. Each was crossed against the register of
reasons, against the eighteen findings and against the seeded data, and only then assigned a row of
the criterion:

| Verdict | How many |
| --- | --- |
| `DEFECT` | **49**, of which **43** survive refutation |
| `ALREADY WRITTEN` — closed by a reason in the tree | 32 |
| `NOT A FINDING` — the application has more, at no cost | 22 |
| `NOT COMPARABLE` — the seed does not feed one of the halves | 5 |
| **Total** | **108** |

**And the figure that alone justifies having built the register**: of the 32 closed by a written
reason, **only two rest on `ELEMENTS.es.md`**, the one house the notes required consulting. The other
thirty rest on **comments in the markup itself** (8), on round three's comparison (5), on
`design/README.md` (4), on the ADRs (6), on the gates' closed lists (4), and on the parity matrix,
the audit and the changelog (3). **Crossing only against «the concessions» would have closed 2 of 32
and raised the other 30 as defects.**

#### The forty-three that stand

**Film card** — the prototype puts a radio and a «Play» on every version row and the application
neither; it states availability both ways where the application only draws something when it is
missing; it offers «Open with an external player» as a normal action, while here it appears only as a
rescue after a playback failure; and the same button is called «Safe rename» there and «Preview
rename» here.

**The Settings index** — twelve entries against thirteen: «Audio» and «Accessibility» are missing.
And found while verifying, not in the notes: **the order is not the prototype's either, and the code
states in writing that it is.**

**Privacy** — the prototype paints each host in monospace inside its own box and the application as
running text; and «Consent to metadata queries» exists **on no screen at all**, because consent here
is an environment variable.

**Updates** — the «Declared host» row with its explanation is missing; the prototype puts a tone
glyph on all fifteen states and the application on three; «Check automatically» is a checkbox where
the prototype puts a toggle; and the English line of the release notes stays in primary ink where the
prototype drops it to secondary.

**Keyboard shortcuts** — the «Media keys» row is missing, and the four keys are registered with the
system without any screen announcing them; the prototype gives two keys to play/pause and the
application one; it groups in pairs where four separate rows go here; and it draws every key as a
key rather than as monospaced text. **And one that was not in the notes and ties a loose end: the
prototype gives `N` to the mini player and the application `Ctrl+P`** — which is exactly what finding
3 of the other audit has been saying for months that the player's hint promises and does not keep.
The hint copied the prototype and the shortcut did not.

**Subtitles** — «Preferred language» and the «External subtitles · Confined» row are missing, and
neither lives on another screen. And **the one row both share differs**: «Subtitle size», 75-200 % in
steps of 5 in the prototype, against «Text size (percentage)», 50-300 % in steps of 10 here.

**Backups** — the «Rolling backups» slider from 1 to 10 is missing, which here is a fixed five stated
in prose; the row saying **where** backups are written is missing, and the database's path is drawn
instead; and the two shared buttons carry different words in both languages.

**Library and scanning** — the prototype says how many items each root holds and here only the kind;
«Scan on start», «Ignore samples and extras» and the «Media roots · N» row are missing; the one
shared toggle changes name and loses the sentence that explains it; and the badges and the remove
button say different words. **And the most serious of the whole round: the removal confirmation says
the opposite on each side** — the prototype promises the catalogue keeps its items marked
unavailable, and the application warns that the titles leave the catalogue with their markers and
their progress. That is not a wording difference: they are two different promises about the user's
data.

**Metadata editor** — one column against two, without the bordered card the prototype draws, field
locking as a checkbox against a state button, without the sentence explaining what the lock does, and
four of six shared labels that do not match.

**Courses** — the prototype's card opens with a 16:9 image panel and the one here has no image at
all. **Segment detection** — the «What it never does · Suggests» row is missing.

#### The six refutation knocked down, and why they matter

All six fell **on a written reason, not on an observation error**: what they described was true. Two
of them are the structural candidates the notes called the most visible difference across the whole
Settings surface — checkbox against toggle, and density as pills against a dropdown — and both have
their decision written down, one of them **in the model that feeds the view rather than in the
view**, one file beyond where whoever noted it was looking. The one about the empty-state dashed
border in Courses also fell on measurement: the application **cannot** paint the prototype's colour
without turning three gates red.

**That is 12 % of the defects, and it is why verification is not optional.**

### 5. What was corrected, and why these four and not others

The comparison **records and does not build** — the owner's decision of 2026-09-05. The four that got
in are those that already had a decision taken or could not wait.

**LibVLC's attribution, which the prototype draws and the Credits screen did not have.** It was not a
compliance gap — `NOTICE`, `licenses/` and the corresponding source all travel inside the package —
but LGPL-2.1 §6 asks for a prominent notice that the library is used, and nobody using a desktop
application opens `NOTICE`.

**And the wording is NOT the prototype's, by the owner's decision of 2026-09-06.** The prototype
writes «LibVLC · LGPL», and «LGPL» is not a licence: the core is `LGPL-2.1-or-later`, its three
hundred plugins carry their own — some `GPL-2.0-or-later`, like the x264 behind
`libx26410b_plugin.dll` — and the library ships **unmodified**. Those are the three facts
`docs/release/THIRD-PARTY-NOTICES.es.md` establishes, and they are what the new row says. `NOTICE`
gains the sentence TMDB already had: that this attribution is readable in the application too.

**The reason beside the «Apply threshold» button.** `PRD006-parity-matrix.md` says in writing that
«Apply» does not exist in this tree, «because the prototype applies on choosing and a button whose
only job was to repeat what the control beside it already said is the definition of one control too
many». The button exists, and until today with no reason beside it: a control contradicting a rule of
the tree without saying why is exactly what those rules exist to prevent.

**The reason was measured here, and that is the point.** The way out that came written down was to
cite the 4 s → 63 s of a walk scene, and **that measurement belongs to something else**: it is the
notices strip's — the command bound to the cancel button, with the scan publishing progress per batch
— not this slider's. Transplanting it would have put a false number into the tree, which is worse than
the bare button: a button with no reason gets noticed, and a reason with a number never gets looked
at again.

What was measured here: `ConfigureWatchedThreshold.ExecuteAsync` reads **every** watch state through
`GetAllAsync` and writes back each one whose status changed; the slider runs from **50 to 100** with
`TickFrequency="1"` and `IsSnapToTickEnabled`, so applying on choosing would sweep the whole table
**51 times** in a single drag from end to end. The loop's lesson is cited to its source as an
analogy, not as a measurement of its own.

**`PRD-006`'s criterion promised «the 53 views» and the tree measures 61.** The 53 comes from counting
only the files whose name ends in `View`, and eight surfaces do not — `WatchStatusControl`,
`UnavailableBadge`, `LooseFileBanner`, `MiniPlayerWindow`, `NextEpisodeOverlay`, `SkipMarkerButton`,
`VersionSwitchDialog`, `VideoStatusOverlay`. **A row cannot go to `VERIFIED` against a census that no
longer describes it**, so it is corrected to 61, and along the way the criterion names the
prototype's **42 screens** rather than «a captures matrix» with no number.

**And the figure can no longer go stale in silence.** `docs/FEATURES.md` joins the documents
`QuotedFigureTests` watches, which until today were only `CLAUDE.md`, `CONTRIBUTING.md` and
`.claude/` — that is, the canonical scope matrix was the one place where a false figure cost the most
and nobody was looking. Checked in both directions: with 61 the gate passes, and writing 60 it
answers `docs/FEATURES.md:36 says 60 for 'medido:vistas', and the tree measures 61`.
