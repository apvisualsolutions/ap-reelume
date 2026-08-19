# El campo de texto, y una familia que vale por dos tipos / The text field, and one family worth two types

Cuarto tipo de la fase 2, quince usos, y el primero cuyos recursos alcanzan a más de un control. /
Phase 2's fourth type, fifteen uses, and the first whose resources reach more than one control.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## A quién alcanza, medido sobre doce tipos / Who it reaches, measured over twelve types

Un `TextBox` tiene **dos** claves propias (`TextBoxClearButtonData`, `TextBoxTopHeaderMargin`) y pinta
desde la familia `TextControl*`, que es compartida. Se pintó cada candidata de un color que ningún
tema usa y se montaron doce tipos: / A `TextBox` owns **two** keys and paints from the shared
`TextControl*` family. Each candidate was painted a colour no theme uses and twelve types mounted:

```
TextBox 25 sitios   NumericUpDown 35   ComboBox 2
Button 0   ToggleButton 0   ToggleSwitch 0   RadioButton 0   CheckBox 0   Slider 0   Menu 0
```

Así que una familia cubre **los 15 campos de texto y los 5 numéricos**. El `ComboBox` la toca sólo
por la caja que le crece **cuando es editable**, y el árbol no tiene ni uno (`IsEditable`: 0
apariciones), así que un desplegable cerrado no tiene `PART_BorderElement` siquiera — le tocará su
propia familia. / One family covers the 15 text boxes and the 5 numeric fields. The combo box only
touches it through the box it grows when editable, and the tree has none.

## Lo que pintaba antes, y sus cuatro números / What painted before, and its four numbers

```
Light / HighContrastLight   IDÉNTICOS otra vez / IDENTICAL again
  :rest         fondo #66FFFFFF  borde #99000000   hint #99000000 con Opacity 0,5 encima
  :pointerover  fondo #99FFFFFF  borde #cc000000
  :focus        fondo White      borde #FF0078D7   <- en LOS CUATRO temas
  :disabled     fondo #33000000  borde #33000000   texto #7A7A7A
```

1. **El aviso dentro de un campo vacío medía 2,11:1** en claro (2,63 en oscuro, 2,12 y 2,45 en los de
   alto contraste). Es texto, y es el que dice para qué sirve el campo mientras está vacío. Lleva
   transparencia **dos veces**: `#99000000` en el color **y** `Opacity 0,5` en el elemento. / **The
   hint inside an empty field read 2.11:1.** It carries transparency twice over.
2. **Un campo apagado no era legible ni tenía forma**: su texto medía **2,56:1** contra su relleno y
   su borde **2,51:1** contra la superficie (1,66:1 en alto contraste oscuro). / **A switched-off
   field was neither readable nor a shape.**
3. **El borde del foco era `#0078D7` en los cuatro temas** — incluido aquél cuyo color de foco es
   `#FFFF00`. / **The focus border was `#0078D7` in all four themes.**
4. Y **Light pintaba idéntico a HighContrastLight**, como la casilla y la fila antes que él. / And
   **Light painted identically to HighContrastLight**.

## Un estilo de la fase 1 que no pintaba nada / A phase-1 style that painted nothing

Medido de paso: el estilo `TextBox:focus` que la fase 1 escribió **sí** llega al control —el
`TextBox` lleva su `BorderBrush` a `#FF005A9C` en claro y a `Yellow` en alto contraste oscuro— y la
plantilla **lo ignora**, porque quien pinta es su `PART_BorderElement` desde
`TextControlBorderBrushFocused`. El anillo de foco se veía igual, porque es un adorno; el borde
interior decía azul de Windows. Es el defecto de la casa con cara de setter: declarado, aplicado, y
sin efecto. / Measured along the way: the `TextBox:focus` style does reach the control and the
template ignores it. The focus ring still showed, because it is an adorner; the inner border said
Windows blue.

## El mapeo / The mapping

| | Relleno / Fill | Borde / Border | Texto / Text | Aviso / Hint |
| --- | --- | --- | --- | --- |
| reposo | `ControlFillBrush` | `ShellBorderBrush` | `TextPrimaryBrush` | `TextSecondaryBrush` |
| sobre | `ControlFillHoverBrush` | `ShellBorderBrush` | `ControlTextActiveBrush` | `ControlTextActiveBrush` |
| foco | `ControlFillBrush` | **`FocusStrokeBrush`** | `TextPrimaryBrush` | `TextSecondaryBrush` |
| apagado | `ControlFillDisabledBrush` | `ShellBorderBrush` | `TextDisabledBrush` | `TextDisabledBrush` |

**El campo invierte al pasar el ratón, igual que el botón, y es una decisión.** En alto contraste el
relleno de paso de ratón *es* el color del borde, así que sin invertir el texto quedaría a 1,00:1. Se
podría haber dejado el campo sin señal de paso de ratón —su affordance es el cursor— pero dos
controles en el mismo estado no deberían decirlo de dos formas distintas. / **The field inverts on
hover, as the button does, and that is a decision.**

**Y `TextControlPlaceholderOpacity` pasa a 1**, con el color diciendo lo tenue que es el aviso. Una
segunda tenuidad encima del color es lo que lo llevó a 2,11:1. / **And the placeholder's opacity goes
to 1**: a second faintness on top of the colour is what took it there.

El peor número del mapeo en los cuatro temas: **4,26:1** (el texto apagado sobre su relleno, en
claro), contra 3,0. El resto va de 5,72 a 21. / The mapping's worst number is **4.26:1** against 3.0.

## Lo que la prueba tuvo que aprender del `NumericUpDown` / What the test had to learn

La primera versión buscaba su `PART_BorderElement` y leía **negro sobre negro**. Medido, no supuesto:
un `NumericUpDown` tiene **dos marcos**, y el del `TextBox` interior es **transparente a propósito**
para no dibujar dos rectángulos concéntricos. Quien pinta es el `Border` del `ButtonSpinner`, y ése
ya salía correcto. La prueba pregunta ahora por **el marco que se ve** —grosor mayor que cero y un
borde que no sea la superficie— en vez de por un nombre de parte. / The first version read black on
black. A `NumericUpDown` has **two frames**, and the inner text box's is deliberately transparent.

## Las puertas, probadas fallando / The gates, proved failing

```
la opacidad del aviso vuelve a su valor base   -> RED  2,20 / 2,52 / 3,95
el borde de foco vuelve al borde del control   -> RED  (3 temas; en alto contraste claro coinciden)
sin los alias                                  -> RED  (15 de 15)
```

## Verde / Green

```
UiTests              Con error: 0, Superado: 514, Total: 514
AccessibilityTests   dos pasadas, 135 y 135, 0 critical / 0 major / 0 minor
El paseo / the walk  129 declared command controls in 128 identities; 128 pressed, 0 pending
```

Los diccionarios quedan en **344 declaraciones**, 86 por tema: 24 brochas y 62 alias. / The
dictionaries stand at **344 declarations**, 86 per theme.
