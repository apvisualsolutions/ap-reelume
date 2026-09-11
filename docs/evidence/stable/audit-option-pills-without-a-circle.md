# Las píldoras de opción llevaban un botón de opción dentro / The Option Pills Carried a Radio Button Inside

- IDs: `PRD-006`
- Fecha / Date: 2026-09-11
- Alcance / Scope: `Presentation` (cinco vistas y cinco modelos), `UiTests/Theme/OptionPillTests`,
  `AccessibilityTests/ContrastTokenTests`, `docs/design/ELEMENTS`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Qué se veía

El propietario lo dijo mirando los filtros de la Biblioteca: «aparece el selector del radial, no
están bien ese tipo de botones en toda la app». Cada píldora de opción llevaba un `●` o un `○`
delante de la palabra, y eso se lee como un botón de opción metido dentro de una píldora.

### Lo que dibuja el prototipo, medido y no supuesto

Se fotografió el prototipo en claro y en sus dos altos contrastes, en la Biblioteca, en Apariencia y
en el reproductor. **No dibuja el círculo en ninguna píldora y en ningún tema.** La elegida lleva
borde de acento, relleno de acento y peso; las demás, ningún borde. En alto contraste los rellenos
desaparecen y lo que queda distinguiendo a la elegida es el borde, que las demás no tienen. Los
círculos del prototipo viven sólo en las **filas** de las listas de pistas y de dispositivos de
salida, que son botones de opción de verdad y aquí siguen igual.

### Lo que había detrás del círculo

Veintisiete píldoras en cinco vistas. **En dieciocho el círculo era la única señal**: las cinco de
tema, las tres de densidad, las tres de esquinas y las dos de idioma de Apariencia, las tres de tipo
de carpeta de la primera ejecución y las dos del diálogo de añadir nunca enlazaron el estilo de
elegida, así que se dibujaban iguales y sólo el círculo decía cuál estaba en vigor. Las tres de la
Biblioteca y las seis del reproductor sí lo enlazaban y llevaban el círculo además.

**La razón escrita para el círculo era cierta y no bastaba.** Decía que en los dos altos contrastes
el relleno de la elegida y el de las demás son el mismo color —medido: `ControlFillBrush` y
`AccentSubtleBrush` son el mismo blanco o el mismo negro— y concluía que el círculo era lo único que
quedaba. Pero la elegida tiene además un borde que las otras no tienen, y va en semi-negrita. Es la
misma respuesta que las muestras de acento ya habían recibido cuando el propietario puso la misma
objeción allí.

### La corrección

- Las veintisiete dejan de dibujar el círculo, y las dieciocho que no lo tenían enlazan el estilo de
  elegida desde su modelo, que pasa a exponer un indicador por opción en vez de una cadena con el
  círculo dentro.
- **El borde de la elegida contra la superficie en la que se dibuja**, medido en el diccionario de
  cada tema: 5,77:1 en claro, 7,61:1 en oscuro, 8,59:1 en alto contraste claro y 16,75:1 en alto
  contraste oscuro, contra la tarjeta de una fila de ajustes; todos por encima del 3:1 que WCAG pide
  a lo que no es texto.
- En las pestañas del reproductor la píldora en reposo ya tiene borde propio, así que lo que dice cuál
  está abierta sin color es el peso de la palabra y la columna misma, que se abre al lado del vídeo
  con ese nombre como título. Esa prueba ya existía y ahora lee la píldora elegida en vez del círculo.

### Las puertas

- `OptionPillTests` nació roja contra el código anterior, y por las dos razones: encontró círculos en
  las píldoras, y en Apariencia no encontró **ninguna** píldora pintada como elegida. Tiene suelo
  antiblindaje: tiene que encontrar las veintisiete antes de juzgar.
- `ContrastTokenTests` contaba **siete círculos** en Apariencia como la señal que no es color. Ahora
  exige el borde transparente en reposo y de acento en la elegida, la semi-negrita, el contraste del
  borde en los cuatro temas, y que toda píldora del marcado enlace el estilo de elegida. **No se borró
  la puerta: se cambió lo que mide por lo que de verdad dice el estado.**
- `StateGlyphTests` baja su suelo de trece a **diez**, medido: lo que queda con círculo son estados de
  verdad —el progreso de lo visto y el escaneo en marcha—, no elecciones.
- `docs/design/ELEMENTS` pedía «siempre un glifo de estado» en la píldora de opción. Se corrige en los
  dos idiomas, con la razón.

### Lo que la auditoría de puertas encontró el mismo día, y cómo se mide ahora

El agente `gate-auditor` aplicó a estas puertas las mutaciones que deberían cazar. **Cuatro pasaban**,
y las cuatro se vieron fallar con la puerta nueva antes de darla por buena:

- **Un círculo dibujado como figura pasaba**: la prueba buscaba sólo el carácter. Una `Ellipse` de
  10 px junto a «Claro» dejó las dos suites en verde. Ahora ninguna píldora puede tener una figura
  dentro.
- **El suelo de 27 tenía holgura**, porque el shell dibuja 30 y la cuenta encontraba 48 con las
  vistas duplicadas. Una píldora escondida por el valor de reserva de un enlace seguía contando, con
  su círculo dentro y sin que nadie lo mirara. Ahora la cuenta es exacta, 30, y cada píldora tiene
  que estar en pantalla con su palabra.
- **La fila de Apariencia nunca probó una instalación nueva**: Sistema, Cómoda y Suave no se
  encendían nunca, y un indicador que respondía por otro tema pasaba. Ahora empieza ahí y enciende
  las trece.
- **Que toda píldora enlace el estilo de elegida se leía en el marcado, no en pantalla.** Intercambiar
  los enlaces de Películas y Series, de las dos mitades del diálogo de añadir o de Audio y Vídeo en
  el reproductor encendía la equivocada y nada fallaba. La disposición de audio tenía la misma forma:
  exigía «exactamente una encendida», que es también lo que dan dos enlaces cruzados. Ahora cada
  grupo se pulsa, píldora a píldora, y se nombra la que se enciende.

**Y encontró un defecto real, sin mutar nada.** En Claro y Oscuro la aplicación no pinta el acento del
diccionario: lo sustituye al arrancar por uno derivado del color elegido, y esa derivación sólo lo
llevaba a 3:1 contra la página. Las píldoras están sobre tarjetas, y en Oscuro la tarjeta es más
clara que la página: **tres de los seis acentos del prototipo pintaban el borde de la elegida por
debajo de 3:1 sobre su tarjeta** —2,86 el verde y 2,78 el morado, medidos en píxeles, y 2,78 el
marrón, calculado—, mientras `ContrastTokenTests` decía 7,61 leyendo un acento que en Oscuro no se
pinta nunca. Ahora el acento y su tinta se derivan para leerse sobre
la página y sobre la tarjeta; `AppearanceServiceTests` exige los seis en los dos temas contra las
dos superficies, y nació roja con esos tres.

### Y la cobertura, que es la trampa de mover código otra vez

Los círculos se llevaron **ramas cubiertas**. En `LibraryViewModel.cs` eso bajaba las ramas de 92 a
**91** con la cifra de CI del run anterior como base, y la puerta lo habría leído como un retroceso.
**`eng/preview-coverage-floors.ps1` no lo habría avisado**: leído su código, sólo lista suelos que
suben y archivos nuevos. La cifra se reconstruyó a mano con el Cobertura fusionado de ese run y la
medición local encima, línea por línea.

La salida fue la de la casa —cubrir, nunca rebajar—, y con las ramas que nadie tomaba: una segunda
ficha del mismo tipo, pedir más sin cursor, fijar el filtro o el orden que ya se tiene, comandos que
rechazan un valor ajeno y el cambio de idioma sin nadie escuchando. `LibraryViewModel.cs` y
`AppearanceSettingsViewModel.cs` llegan al listón y **salen de la lista de deuda**, y el trinquete
baja de 189 a **187**.

**Una de esas pruebas no podía fallar en la mitad que importaba**, y la encontró la misma auditoría:
«fijar el orden que ya se tiene no vuelve a consultar» corría sin haber cargado la página, y antes de
la primera carga la consulta está apagada haga lo que haga el orden. Sacar la consulta de la guarda
dejaba la prueba en verde. Ahora carga primero, y esa mutación la pone roja.

## English

### What was on screen

The owner said it looking at the library's filters: «the radio selector shows up, and that kind of
button is wrong all over the app». Every option pill carried a `●` or a `○` before its word, and it
reads as a radio button dropped inside a pill.

### What the prototype draws, measured rather than assumed

The prototype was captured in light and in both of its high contrast modes, on the library, the
appearance page and the player. **It draws the circle on no pill and in no theme.** The chosen one
carries an accent border, an accent fill and weight; the others carry no border at all. In high
contrast the fills disappear, and what is left telling the chosen one apart is the border the others
do not have. The prototype's circles live only in the **rows** of the track and output-device lists,
which are real radio buttons and stay as they were here.

### What was behind the circle

Twenty-seven pills in five views. **In eighteen of them the circle was the only signal**: the
appearance page's five theme, three density, three corner and two language pills, the first run's
three folder kinds and the add dialog's two never bound the chosen style, so they were drawn alike and
only the circle said which was in force. The library's three and the player's six did bind it, and
carried the circle on top.

**The reason written down for the circle was true and not enough.** It said that in both high
contrasts the chosen fill and the others' are the same colour — measured: `ControlFillBrush` and
`AccentSubtleBrush` are the same white or the same black — and concluded that the circle was all that
was left. But the chosen pill also has a border the others do not have, and a semi-bold word. It is
the same answer the accent swatches had already been given when the owner raised the same objection
there.

### The fix

- The twenty-seven stop drawing the circle, and the eighteen that lacked it bind the chosen style from
  their model, which now exposes one flag per option instead of a string holding the circle.
- **The chosen pill's border against the surface it is drawn on**, measured in each theme's
  dictionary: 5.77:1 in light, 7.61:1 in dark, 8.59:1 in high contrast light and 16.75:1 in high
  contrast dark, against the card of a settings row; all above the 3:1 WCAG asks of non-text.
- On the player's tabs the resting pill already has a border of its own, so what says which one is
  open without colour is the weight of the word and the column itself, which opens beside the video
  with that name as its heading. That test already existed and now reads the chosen pill instead of
  the circle.

### The gates

- `OptionPillTests` was born red against the previous code, for both reasons: it found circles on the
  pills, and on the appearance page it found **no** pill painted as chosen. It carries an
  anti-blindness floor: it has to find the twenty-seven before it judges.
- `ContrastTokenTests` counted **seven circles** on the appearance page as the non-colour signal. It
  now requires the transparent border at rest and the accent one when chosen, the semi-bold word, the
  border's contrast in all four themes, and every pill in the markup to bind the chosen style. **The
  gate was not deleted: what it measures was changed to what actually says the state.**
- `StateGlyphTests` lowers its floor from thirteen to **ten**, measured: what keeps a circle is real
  state — the progress of what was watched and the running scan — not a choice.
- `docs/design/ELEMENTS` asked for «always a state glyph» on the option pill. It is corrected in both
  languages, with the reason.

### What the gate audit found the same day, and how it is measured now

The `gate-auditor` agent applied to these gates the mutations they should catch. **Four got
through**, and all four were seen failing with the new gate before it was taken as good:

- **A circle drawn as a shape got through**: the test only looked for the character. A 10 px
  `Ellipse` beside «Claro» left both suites green. Now no pill may hold a shape.
- **The floor of 27 had slack**, because the shell draws 30 and the count found 48 with the
  duplicated views. A pill hidden by a binding's fallback value still counted, with its circle inside
  and nobody looking. Now the count is exact, 30, and every pill has to be on screen with its word.
- **The appearance row never tried a new installation**: System, Comfortable and Soft were never
  lit, and a flag that answered for another theme passed. Now it starts there and lights all
  thirteen.
- **That every pill binds the chosen style was read in the markup, not on screen.** Swapping the
  bindings of Movies and Series, of the add dialog's two halves, or of Audio and Video in the player
  lit the wrong one and nothing failed. The audio layout had the same shape: it required «exactly one
  lit», which is also what two crossed bindings give. Now every group is pressed, pill by pill, and
  the one that lights is named.

**And it found a real defect, without mutating anything.** In light and dark the application does not
paint the dictionary's accent: it replaces it at start with one derived from the chosen colour, and
that derivation only took it to 3:1 against the page. The pills sit on cards, and in dark a card is
lighter than the page: **three of the prototype's six accents painted the chosen pill's border below
3:1 on its card** — 2.86 the green and 2.78 the purple, measured in pixels, and 2.78 the brown,
calculated — while `ContrastTokenTests` said 7.61 reading an accent that dark never paints. Now the accent and its ink are derived to read on the page and on
the card; `AppearanceServiceTests` requires all six in both themes against both surfaces, and was
born red with those three.

### And the coverage, which is the moving-code trap once more

The circles took **covered branches** with them. In `LibraryViewModel.cs` that dropped branches from
92 to **91** with the previous run's CI figure as the baseline, and the gate would have read it as
going backwards. **`eng/preview-coverage-floors.ps1` would not have warned**: read in its code, it
only lists floors that rise and new files. The figure was rebuilt by hand from that run's merged
Cobertura with the local measurement laid over it, line by line.

The way out was the house's — cover, never lower — with the branches nobody took: a second card of
the same kind, asking for more without a cursor, setting the filter or the order already held,
commands refusing a value that is not theirs, and the language changing with nobody listening.
`LibraryViewModel.cs` and `AppearanceSettingsViewModel.cs` reach the bar and **leave the debt list**,
and the ratchet goes from 189 to **187**.

**One of those tests could not fail in the half that mattered**, and the same audit found it:
«setting the order already held does not query again» ran without the page ever loading, and before
the first load the query is off whatever the order does. Moving the query out of its guard left the
test green. It now loads first, and that mutation turns it red.
