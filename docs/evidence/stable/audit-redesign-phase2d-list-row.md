# Qué fila está seleccionada, que era lo único que una lista tiene que decir / Which row is selected, the one thing a list has to say

Diecisiete usos directos y **23 listas con datos** detrás. Tercer tipo de la fase 2 por uso medido, y
tampoco se parece a los dos anteriores. / Seventeen direct uses and **23 lists with data** behind
them. Phase 2's third type by measured use, and it is not like either of the last two.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo medido antes de escribir nada / Measured before anything was written

Una fila tiene **un** recurso propio (`ListBoxItemPadding`), **ningún borde en ningún estado**
(`BorderBrush = null`), y su propio `Background` transparente: quien pinta es el `ContentPresenter`,
desde brochas que el tema base comparte entre listas. / A row owns **one** theme resource, has **no
border in any state**, and its own background is transparent: the content presenter paints it.

```
Light / HighContrastLight   (IDÉNTICOS / IDENTICAL)
  :rest         bg=Transparent          fg=Black
  :pointerover  bg=#19000000            :pressed bg=#33000000
  :selected     bg=#0078D7 Opacity 0,4  fg=Black
HighContrastDark
  :selected     bg=#0078D7 Opacity 0,6  fg=White
```

**Aquí el alfa está en las dos partes**: `Color=#FF0078D7`, opaco, **y** `Opacity 0,4`. Componer sólo
el canal alfa del color no basta. Es la tercera forma distinta de transparencia que este proyecto se
encuentra en tres tandas, así que la aritmética pasó a un sitio único, `ThemeContrast`. / **The alpha
is in both halves here.** It is the third distinct shape of transparency in three batches, so the
arithmetic moved to one place.

Compuesto sobre la superficie de su tema, lo que separaba a la fila seleccionada de las demás: / What
set the selected row apart from the others, composited over its theme's surface:

```
Light 1,73:1    Dark 2,22:1    HighContrastLight 1,76:1    HighContrastDark 2,24:1
```

contra un listón de 3,0. **El texto encima sí se leía** —11,58:1 en claro— así que el defecto nunca
fue el texto: era **saber en qué fila estás**. / against a bar of 3.0. **The label on it was
perfectly legible**, so the defect was never the text: it was knowing which row you are on.

## Qué se podía tocar, medido sobre doce tipos / What could be touched, measured over twelve types

Las brochas que la plantilla consume son del sistema (`SystemControlHighlightList*`), y una brocha
compartida no se redirige a ciegas. Se pintó cada candidata de un color que ningún tema usa y se
montaron **doce** tipos de control forzando cinco pseudoclases cada uno: / The template's brushes are
system ones, and a shared brush is not redirected blind. Each candidate was painted a colour no theme
uses and **twelve** control types were mounted, forcing five pseudo-classes each:

```
Button 0   ToggleButton 0   ToggleSwitch 0   RadioButton 0   TextBox 0   ComboBox 0
CheckBox 0   Slider 0   NumericUpDown 0   Menu 0   TabControl 0
ListBox 5  <- :pointerover, :pressed, :selected y las dos del deshabilitado
```

**Sólo la lista las toma.** Ni la lista desplegable, ni el menú, ni las pestañas, ni ninguno de los
diez tipos con foco. / **Only the list takes them.**

Y la segunda pregunta, la que decidió el diseño: **el `ContentPresenter` de la fila sí toma el
`BorderBrush` y el `BorderThickness` de la fila** por `TemplateBinding`, así que un estilo de
aplicación puede darle borde sin plantilla propia y sin adorno. En cambio **su texto sale de
`SystemControlForegroundBaseHighBrush`**, que es genérica: no hay forma de cambiar el color del texto
de la fila seleccionada a solas. / And the second question, which decided the design: the presenter
**does** take the row's `BorderBrush` and `BorderThickness` by template binding. Its text, though,
comes from a generic foreground: there is no way to recolour the selected row's label alone.

## El diseño que sale de ahí / The design that follows

Eso es lo que descarta el relleno de acento pleno: sin poder dar color al texto, un acento sólido
dejaría la etiqueta a merced del tema. Así que: / That is what rules out a solid accent fill: unable
to colour the text, a solid accent would leave the label at the theme's mercy. So:

| Estado / State | Relleno / Fill | Borde / Border |
| --- | --- | --- |
| reposo / rest | — | `Transparent`, 2 px |
| sobre / hover | `ControlFillHoverBrush` | `Transparent`, 2 px |
| pulsada / pressed | `ControlFillPressedBrush` | `Transparent`, 2 px |
| **seleccionada / selected** | `AccentSubtleBrush` | **`AccentBrush`**, 2 px |

El grosor es el mismo en los cinco estados y sólo cambia el color, porque un borde que **aparece** al
seleccionar empuja el texto de esa fila y de ninguna otra. / The thickness is the same in all five
states and only the colour changes, because a border that *appears* on selection shoves that row's
text and no other's.

Lo que la fila seleccionada mide ahora contra la superficie: **5,52:1** en claro, **7,38:1** en
oscuro, **8,59:1** en alto contraste claro y **16,75:1** en alto contraste oscuro. / What the selected
row now measures against the surface.

**En los dos temas de alto contraste `AccentSubtleBrush` *es* la superficie**, así que allí el borde
es la señal entera — y eso está medido, no supuesto: quitándolo, esos dos temas caen a **1,00:1**. /
**In both high contrast themes the subtle accent *is* the surface**, so there the border is the whole
cue — measured, not assumed: without it those two fall to **1.00:1**.

## Un orden que importa / An order that matters

Los dos estilos de la fila se declaran **antes** que los selectores de foco. Entre estilos que ambos
casan gana el último declarado, y el foco tiene que ganar a la selección: puestos después, una fila
enfocada habría perdido su anillo. / The row's two styles are declared **before** the focus selectors.
Between styles that both match, the last declared wins, and focus has to beat selection.

## Las puertas, probadas fallando / The gates, proved failing

```
la seleccionada pierde el borde      -> RED  1,17 / 1,54 / 1,00 / 1,00
el borde llega sólo al seleccionar   -> RED  (4, los cuatro temas: la fila se movería)
sin los alias                        -> RED  (9 de 13)
```

## Verde / Green

```
UiTests              Con error: 0, Superado: 500, Total: 500
AccessibilityTests   dos pasadas, 132 y 132, 0 critical / 0 major / 0 minor
El paseo / the walk  129 declared command controls in 128 identities; 128 pressed, 0 pending
```

Los 2 px de borde en todas las filas cambian la geometría de las 23 listas, y el paseo —que pulsa
filas— sigue en 128 de 128. / The 2 px border on every row changes the geometry of all 23 lists, and
the walk, which presses rows, is still at 128 of 128.
