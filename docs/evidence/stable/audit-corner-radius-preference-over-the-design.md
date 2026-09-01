# Una preferencia estaba dibujando todas las esquinas de la aplicación, y la puerta de ADR-0007 certificaba las que nadie veía / A preference was drawing every corner in the application, and ADR-0007's gate certified the ones nobody could see

Primer trabajo del barrido de fidelidad que [ADR-0007](../../adr/0007-every-element-matches-the-prototype.md)
dejó abierto. Empezó por emparejar las diez clases de botón que aquel documento dejó sin pareja y
acabó en un sitio distinto: **ninguna clase de este árbol dibujaba su radio de diseño**, incluidas
las dos que el ADR daba por devueltas al prototipo el día anterior. / It began as the pairing job
ADR-0007 left open and ended somewhere else: **no class in this tree was drawing its design radius**,
including the two that ADR had reported returned to the prototype the day before.

Fecha / Date: 2026-09-01. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que se midió, y no es lo que el archivo declara / What was measured, and it is not what the file declares

`ButtonShapeTests` leía el `CornerRadius` escrito en `DesignTokens.axaml` y traducía
`CornerRadiusMedium` a 8 y `CornerRadiusSmall` a 4, que es lo que ese archivo declara. Una sonda que
construye el control, deja correr `AppearanceService` como hace el arranque y **lee la esquina del
control** contestó otra cosa: / A probe that builds the control, lets `AppearanceService` run as
startup does and **reads the corner off the control** answered otherwise:

```
static  CornerRadiusMedium=absent          CornerRadiusSmall=absent
  static Button       player-chrome            => 8,8,8,8
  static Button       player-pill              => 4,4,4,4
runtime CornerRadiusMedium=10,10,10,10     CornerRadiusSmall=5,5,5,5   rounding=Soft
  runtime Button      player-chrome            => 10,10,10,10
  runtime Button      player-pill              => 5,5,5,5
```

La causa es una sola línea de `AppearanceService.Write`: la preferencia «Redondeo de esquinas»
—`Soft` por defecto— se escribía **sobre los dos tokens de radio**, y el contenedor la resuelve
*antes de construir superficie alguna*. Así que el 8 y el 4 del archivo de tokens eran lo que se
habría dibujado si el servicio no hubiera corrido nunca. / One line of `AppearanceService.Write` was
writing the Rounding preference over both radius tokens, and the composition root resolves it before
any surface is built.

**El prototipo gasta esa misma preferencia en un solo sitio.** `st.opt.radius` aparece dos veces en
`design/AP Reelume.dc.html`: donde se declara el control de tres opciones (4 · 10 · 18) y donde
`artBox` —la caja de la carátula— lo lee. Ningún otro elemento del diseño lo consulta. El comentario
que justificaba el alcance ancho decía «the prototype offers one control over both», y el prototipo
no lo hace. / The prototype spends that preference in exactly one place, `artBox`, the cover box.
The comment that justified the wider reach claimed otherwise.

**La consecuencia no era que una preferencia se comportara raro**: era que mientras el radio de todo
fuese una preferencia, **ningún elemento podía afirmar que dibujaba su número**, y la puerta escrita
el día anterior para garantizar justamente eso pasaba comparando dos números que sólo existían en un
archivo. / The consequence was not a preference behaving oddly: while every radius was a preference,
no element could claim to draw its number, and the gate written the previous day to guarantee exactly
that passed by comparing two numbers that existed only in a file.

## Los emparejamientos, medidos contra el diseño / The pairings, measured against the design

Catorce clases de botón emparejadas, contra cuatro. Los radios se **leen** de
`design/AP Reelume.dc.html` con el patrón que viaja junto a cada fila. / Fourteen button classes
paired, against four; every radius is read from the design rather than restated.

| Clase / Class | Control del prototipo / Prototype control | Diseño | Antes | Ahora |
|---|---|---:|---:|---:|
| `Button, ToggleButton` | `btnPri` | 999 | 999 | 999 |
| `Button.player-chrome` | `pbtn` | 8 | **10** | **8** |
| `Button.player-pill` | `pbtnLessons` | 4 | **5** | **4** |
| `Button.lesson-row` | la fila de lección | 7 | 7 | 7 |
| `Button.navigation-destination` | los destinos del riel | 12 | **999** | **12** |
| `Button.navigation-action` | `railAdd` | 12 | **999** | **12** |
| `Button.poster-card` | la baldosa de la biblioteca | 12 | **10** | **12** |
| `Button.action-row` | la fila de otras acciones | 5 | **999** | **5** |
| `ToggleButton.action-row` | la fila de otras acciones | 5 | **999** | **5** |
| `ToggleButton.segment` | `seg` | 999 | 999 | 999 |
| `Button.accent-swatch` | los seis del acento | 999 | 999 | 999 |
| `Button.compact` | `btnSecSm` | 999 | 999 | 999 |
| `Button.primary-action` | `btnPri` | 999 | 999 | 999 |
| `Button.theme-option` | `seg` | 999 | 999 | 999 |

Las tres últimas **no escriben esquina propia y no deben escribirla**: su control del prototipo
tampoco la redeclara, la toma del botón base. La tabla lo dice con una columna y una prueba lo
comprueba en los dos sentidos, porque el número medido pasaría igual por casualidad. / The last three
declare no corner of their own and should not: their prototype control does not redeclare one
either. A column says so and a test checks it both ways, because the measured number would pass by
coincidence.

`action-row` es el caso que mejor enseña lo que costó la regla retirada: el prototipo escribe los
otros dos números de ese estilo **literalmente** —`min-height:36px` y `padding:0 12px`— y su esquina
al lado es `border-radius:5px`. La píldora nunca fue la respuesta del diseño ahí. / The prototype
writes that style's other two numbers verbatim and its corner beside them is 5. The pill was never
the design's answer there.

### La regla retirada movió siete clases, no dos / The withdrawn rule moved seven classes, not two

`ADR-0007` registró dos —`player-chrome` y `player-pill`—, que son las que su autor recordaba.
Comparando el archivo de tokens contra `49a0502^`, el commit anterior a la regla, salen **siete**: /
ADR-0007 recorded two, which are the ones its author remembered. Diffing the token file against the
commit before the rule gives **seven**:

| Clase / Class | Antes de la regla | Con la regla | Diseño | Ahora |
|---|---:|---:|---:|---:|
| `Button.player-chrome` | píldora | píldora | 8 | **8** |
| `Button.player-pill` | píldora | píldora | 4 | **4** |
| `Button.action-row` | Small | píldora | 5 | **5** |
| `ToggleButton.action-row` | Small | píldora | 5 | **5** |
| `Button.navigation-destination` | Medium | píldora | 12 | **12** |
| `Button.navigation-action` | Medium | píldora | 12 | **12** |
| `Button.colour-cell` | Small | píldora | — | **Small** |

Dos casos más quedan como estaban y por razones distintas: `accent-swatch` era el literal **22** —la
mitad de su lado— y la regla lo hizo píldora, que es lo que el diseño dibuja, así que **acertó por
casualidad**; y `primary-action` perdió un setter que repetía lo que la clase base ya decía. /
Two more stay as they are: `accent-swatch` was the literal 22 and the rule made it the pill, which is
what the design draws — right by accident; `primary-action` lost a setter repeating the base class.

**`colour-cell` es el que mejor lo enseña**: su comentario decía «Square rather than round» y el
setter de dos líneas más abajo escribía la píldora, y así llevaba una semana. El diseño no contesta
por ella —el prototipo abre el control de color del sistema—, así que vuelve a lo que era **antes de
una regla retirada**, que no es lo mismo que inventarle una forma. / Its comment said «square» while
the setter two lines below wrote the pill. The design does not answer for it, so it goes back to what
it was before a retired rule changed it — which is not the same as inventing a shape.

### Las cuatro que el diseño no contesta / The four the design does not answer

Lista cerrada, con lo que se buscó y no existe, para que el siguiente no repita la búsqueda: /
A closed list, with what was searched for and did not exist:

- **`colour-cell`** — el prototipo abre el control de color del sistema para el acento personalizado
  y **nunca dibuja una rejilla** de muestras. / opens the operating system's own colour control.
- **`rating-choice`** — la valoración personal no está en ninguno de los cuatro documentos de
  diseño; buscado por estrellas, «valoración» y «rating» en los cuatro. / a personal rating is in
  none of the four design documents.
- **`icon-action`** — nace de «en la tarjeta ancha del inicio justo después habría que poner el icono
  de reproducir desde el inicio», del 2026-08-25, **posterior al prototipo**. / born of a request
  made after the prototype was drawn.
- **`link-action`** — `btnLink` no lleva radio: sin fondo y sin borde, no hay esquina que dibujar. /
  has no radius at all, so there is no corner to draw.

**Estar aquí no autoriza a dibujar nada.** Dice que la forma la decidió este árbol porque el diseño
no contesta, que es una afirmación distinta de «lo dice el diseño» y se escribe distinto. / Being on
this list is not permission to draw anything.

## Lo que cambia en la aplicación / What changes in the application

- La preferencia «Redondeo de esquinas» conserva sus tres opciones y **pasa a mover sólo la
  carátula**, que es su único consumidor en el prototipo. El recurso es `PosterCornerRadius`, y lo
  leen la caja del arte de `PosterCardView` y el esqueleto `apr-shim`, que es el hueco de una
  carátula mientras carga. / The preference keeps its three options and now moves only the cover.
- `CornerRadiusMedium` y `CornerRadiusSmall` vuelven a valer 8 y 4 en pantalla, que es lo que
  declaran. Los ochenta y cinco sitios que los gastan pasan de 10 y 5 a 8 y 4. / The two tokens are
  8 and 4 on screen again, which is what they declare.
- Cinco clases de botón cambian de forma: las dos del riel, la baldosa, y las dos filas de acciones.
  / Five button classes change shape.

## La puerta, probada fallando / The gate, proved by failing

Tres mutaciones, cada una contra una mitad distinta. / Three mutations, one per half.

**1. El riel vuelve a la píldora** — la mitad medida: / the measured half:

```
Every_button_draws_the_corner_the_prototype_draws [FAIL]
  Button.navigation-destination draws 999, and the rail's destinations draws 12
```

**2. El servicio vuelve a escribir los dos tokens** — que es el defecto original, reproducido: /
the original defect, reproduced:

```
Every_button_draws_the_corner_the_prototype_draws [FAIL]
  Button.player-chrome draws 10, and pbtn draws 8;
  Button.player-pill draws 5, and pbtnLessons draws 4
```

**Ésa es la demostración de que la puerta vieja era ciega y la nueva no**: con esa misma mutación
puesta, la versión anterior de este archivo pasaba, porque leía el 8 y el 4 del diccionario. / With
that mutation in place the previous version of this file passed, because it read the file.

**3. Una clase de botón nueva sin emparejar** — el censo, que es la mitad que ADR-0007 no tenía: /
the census, which is the half ADR-0007 lacked:

```
Every_button_class_in_the_token_file_is_accounted_for [FAIL]
  Every button class is paired with a prototype control, or written into the unpaired list
  with its reason: Button.invented-thing
```

Sin ese censo, **una clase que nadie emparejó es indistinguible de una que nadie ha emparejado
todavía**, que es el estado en el que ADR-0007 encontró diez clases sin que nada se pusiera rojo. /
Without it, a class nobody paired is indistinguishable from one nobody has got round to pairing.

**4. Una clase sin pareja se sale de lo decidido** — porque las cuatro que el diseño no contesta se
miden igual que las catorce que sí, y son las más fáciles de mover: / because the four the design
does not answer are measured like the fourteen it does, and are the easiest to move:

```
Every_button_draws_the_corner_the_prototype_draws [FAIL]
  Button.colour-cell draws 999, and the decision written beside it draws 4
```

## Lo que queda sin medir / What is still unmeasured

**El resto de la superficie.** ADR-0007 dice «todos los elementos» y los botones son donde hay
puerta. Los `Border`, `TextBox` y `ComboBox` del árbol gastan los mismos tres tokens en ochenta y
cinco sitios, y el prototipo dibuja **doce radios distintos** —1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 14,
15, 16, 17, 19, 22, 26 y 999 entre sus dos notaciones—. Emparejarlos es la tanda siguiente, y ahora
puede medirse contra la pantalla en vez de contra el diccionario. / The rest of the surface: the
same three tokens are spent at eighty-five sites while the design draws twelve distinct radii.
Pairing them is the next batch, and it can now be measured against the screen.

**Y una trampa para quien la haga**: no todo `border-radius` de `design/AP Reelume.dc.html` es
diseño de la aplicación. Las primeras cuarenta líneas son el cromo del propio prototipo —su selector
de idioma, su panel de demostración y unos botones de ventana falsos—, y emparejar contra ellos es
copiar el número equivocado con toda la confianza. / Not every `border-radius` in the design file is
the application's: the first forty lines are the prototype's own chrome.
