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
