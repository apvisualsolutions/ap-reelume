# La casilla, que no era un segundo botón / The checkbox, which was not a second button

Dieciocho en las vistas, y el segundo tipo de la fase 2 por uso medido. Empieza como el botón —
midiendo qué pinta cada estado— y la respuesta es distinta en todo. / Eighteen across the views, and
phase 2's second type by measured use. It starts like the button did, by measuring what paints each
state, and every answer differs.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo primero medido: no se parece al botón / First measured: it is not the button

Una sonda que enumera las claves del tema base en ejecución —1 054 en total— da el tamaño de cada
tipo: / A probe enumerating the base theme's keys at runtime — 1,054 of them — gives each type's size:

```
CheckBox 73   ComboBox 59   RadioButton 38   ToggleButton 37   Slider 32   Button 18
TextBox 2     ListBoxItem 1            (esos dos pintan desde TextControl*, 32)
```

El botón se hizo con **12** recursos. La casilla tiene **73**: seis familias por tres estados de
marcado por cuatro de puntero. De las seis, dos pintan el control entero —transparente, y así se
queda: una casilla vive sobre la superficie en la que está— y cuatro pintan la caja, la marca y la
etiqueta. **Nada del árbol pone `IsThreeState`**, así que el tercio indeterminado es inalcanzable y
se deja donde está en vez de apuntarlo a tokens que nadie puede ver. Quedan **31 alias por tema**. /
The button took **12** resources. The checkbox has **73**, and nothing in the tree sets
`IsThreeState`, so the indeterminate third stays where it is. That leaves **31 aliases per theme**.

## Lo que pintaba antes / What painted before

```
sin marcar   :rest   caja borde=#99000000 relleno=Transparent   :pointerover  #cc000000
                     :pressed borde=Black relleno=#66000000     :disabled     #66000000
marcada      :rest   caja relleno Y borde = #0078D7 EN LOS CUATRO TEMAS   marca=White
                     :disabled relleno=#33000000  borde=Transparent      marca=White
```

**Light y HighContrastLight pintaban idéntico. Dark y HighContrastDark, también.** Nada de este
proyecto llegaba a una casilla, así que poner Windows en alto contraste cambiaba todos los controles
menos éste. / **Light and HighContrastLight painted identically, and so did Dark and
HighContrastDark.** Nothing of this project's reached a checkbox.

Tres defectos, con su número: / Three defects, with their numbers:

1. **Una casilla marcada y apagada era ilegible en el tema claro**: la marca blanca sobre el gris que
   deja `#33000000`, **1,68:1**. En oscuro el mismo caso da 5,74:1, así que sólo fallaba en claro. /
   **A checked, switched-off box was unreadable in the light theme**: the white mark over the grey
   that `#33000000` leaves, **1.68:1**.
2. **El borde de la caja apagada medía 2,83:1**, por debajo del 3:1 que pide un componente de
   interfaz. En reposo eran 5,74:1. / **The disabled box's outline read 2.83:1**, under the 3:1 an
   interface component is held to.
3. **Una casilla marcada era `#0078D7` en los cuatro temas** — el azul de Windows 10, que no es token
   de nadie aquí y en alto contraste tampoco es el acento (`#0000FF` y `#00FFFF`). Mide 4,50:1 sobre
   blanco y 4,67:1 sobre negro, así que **pasa** el listón: no es un fallo de contraste, es una
   paleta ajena apareciendo en un tema que declara dos colores. / **A checked box was `#0078D7` in
   all four themes.** It clears 3:1, so this is not a contrast failure: it is somebody else's palette
   turning up in a theme that declares two colours.

## Un número FALSO, y qué lo produjo / A FALSE number, and what produced it

La primera versión de la prueba dijo **1,00:1** en el tema oscuro —blanco sobre blanco— para el caso
1. Es falso. Todos los pinceles que el tema base da a una casilla llevan **alfa en el propio color**
(`#99000000`, `#66FFFFFF`, `#33000000`), no en `Opacity`, y una luminancia que ignora el canal alfa
mide un color que nadie ve. / The first version of the test reported **1.00:1** in the dark theme —
white on white. It is false: every brush the base theme gives a checkbox carries **alpha in the
colour itself**, and a luminance that ignores the alpha channel measures a colour nobody sees.

**Y lo peligroso no era ese fallo, era el contrario:** donde el alfa iba al revés, la misma prueba
habría **aprobado** un borde que se ve a 2,83:1 como si fuera 21:1. Un fallo ruidoso se corrige; una
aprobación falsa se queda. Se compone el alfa sobre la superficie del tema antes de medir. / **And
the danger was not that failure but its opposite:** where the alpha ran the other way, the same test
would have **passed** a border seen at 2.83:1 as if it were 21:1.

## Una decisión escrita que la medición desmintió / A written decision the measurement refuted

`AccentTextBrush` estaba decidido así: «blanco en claro, oscuro y alto contraste claro; **negro** en
alto contraste oscuro, porque el acento allí es cian». La mitad del tema oscuro es falsa: su
`AccentBrush` es **`#62AEE8`, un azul pálido**, y blanco encima mide **2,40:1**. El color del texto
sobre el acento sigue la **luminancia del acento**, no el nombre del tema. / The decision said white
in light, dark and high contrast light. The dark theme's accent is a pale blue and white on it reads
**2.40:1**. The colour follows the accent's luminance, never the theme's name.

Queda: `#FFFFFF` en claro y en alto contraste claro; `#111827` en oscuro; `#000000` en alto contraste
oscuro. Y `ContrastTokenTests` lo mide ahora contra `AccentBrush` con el listón de texto, para que la
próxima vez lo diga una puerta y no una revisión. / `ContrastTokenTests` now measures it against
`AccentBrush` at the text bar, so next time a gate says it instead of a review.

## El mapeo, con sus contrastes calculados antes de escribirlo / The mapping, with its contrasts computed before it was written

| Estado / State | Relleno / Fill | Borde / Outline | Marca / Mark |
| --- | --- | --- | --- |
| sin marcar, reposo | `ControlFillBrush` | `ShellBorderBrush` | — |
| sin marcar, sobre / pulsada | `ControlFillHoverBrush` / `PressedBrush` | `ShellBorderBrush` | — |
| sin marcar, apagada | `ControlFillDisabledBrush` | `ShellBorderBrush` | — |
| marcada, reposo | `AccentBrush` | el del relleno | `AccentTextBrush` |
| marcada, sobre / pulsada | `ControlFillHoverBrush` / `PressedBrush` | `ShellBorderBrush` | `ControlTextActiveBrush` |
| marcada, apagada | `ControlFillDisabledBrush` | `ShellBorderBrush` | `TextDisabledBrush` |

La etiqueta va siempre a `TextPrimaryBrush` y a `TextDisabledBrush`, y **no invierte** con la caja:
vive fuera de ella, sobre la superficie. / The label is always the text tokens and **does not invert**
with the box: it sits outside it, on the surface.

El peor número del mapeo, en los cuatro temas: **4,26:1** (la marca apagada sobre su relleno, en
claro), contra un listón de 3,0. El resto va de 4,55 a 21. / The mapping's worst number across all
four themes is **4.26:1**, against a bar of 3.0.

## Dos listones, y uno se bajó a la vista de los números / Two bars, and one was lowered in sight of the numbers

La marca se medía contra 4,5 —el listón del **texto**— y una marca es un **gráfico**: el listón que
le toca es 3,0 (WCAG 1.4.11). Se dice claro porque bajar un listón después de medir es sospechoso:
**no rescató nada**. El 1,68:1 de hoy falla con los dos, y el mapeo que lo sustituye pasa el de 3,0
por 4,26 en su punto más estrecho. Lo que cambió es que el listón mide lo que hay. / The mark was
held to the text bar of 4.5 and a mark is a graphic, whose bar is 3.0. Said plainly because lowering
a bar after measuring deserves suspicion: **it rescued nothing.** Today's 1.68:1 fails both.

Y la otra prueba cambió de pregunta. Preguntaba por el borde contra la superficie, y en alto
contraste el paso de ratón **invierte**: la caja se vuelve sólida y su borde desaparece dentro de
ella —1,00:1—, que es el estado **más** claro de los cuatro. Pregunta ahora si la caja se ve, por su
borde **o** por su relleno, que es lo que de verdad importa. / The other test changed its question.
It asked about the outline against the surface, and in high contrast hovering **inverts**: the box
goes solid and its outline vanishes into it, which is the clearest of the four states.

## Las puertas, probadas fallando / The gates, proved failing

```
el relleno de una marcada deja de ser el acento     -> RED (4, los cuatro temas)
AccentTextBrush vuelve a blanco en el tema oscuro   -> RED (Dark text on the accent contrast was 2,40:1)
sin los alias                                       -> RED (13 de 17)
```

## Verde / Green

```
UiTests              Con error: 0, Superado: 487, Total: 487
AccessibilityTests   dos pasadas, 132 y 132, 0 critical / 0 major / 0 minor
```

**Y una intermitencia, registrada porque ocurrió:** en una ejecución de la suite entera falló
`AssembledPhysicalWalkTests.A_session_that_will_not_open_is_handed_over_and_retried_with_the_mouse`,
y no volvió a fallar ni corriendo el paseo solo (33/33) ni en las dos pasadas siguientes. Se anota en
vez de darla por resuelta: quien decide sobre las carreras es CI con sus dos pasadas. / **And one
intermittent, recorded because it happened**: it did not recur in the walk alone or in the two passes
after. CI's two passes are what decides.
