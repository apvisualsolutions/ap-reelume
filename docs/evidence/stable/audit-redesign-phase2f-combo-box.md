# La fase 2f: el desplegable / Phase 2f: the drop-down

Ocho usos, tres familias propias y **59 claves**. Dos defectos medidos que la nota previa no tenía, y
uno que la nota tenía **al revés**. / Eight uses, three families of its own and **59 keys**. Two
measured defects the earlier note did not have, and one it had **backwards**.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que se midió antes de escribir / What was measured before writing

**La primera medición de la fase estaba decidida**: si el `ContentPresenter` de un `ComboBoxItem`
toma el borde por `TemplateBinding` como el del `ListBoxItem`. Puesto fucsia a 2 px sobre el ítem: /
**The phase's first measurement was decided in advance**: whether a `ComboBoxItem`'s content
presenter takes the border by template binding as a `ListBoxItem`'s does. Fuchsia at 2 px on the item:

```
item th=2,2,2,2 bd=Fuchsia -> pres th=2,2,2,2 bd=Fuchsia     (los cuatro temas / all four themes)
```

**Y esa medición, tomada así, engaña** — se explica abajo, porque es la lección de la tanda.

Las tres familias, enumeradas del tema base en ejecución: **59 claves `ComboBox*`**, de las cuales
**23** son de las filas (8 de fondo, **7** de borde —no hay una de reposo— y 8 de texto). La nota
previa decía 22; son 23. No hereda nada del campo de texto: `IsEditable` no aparece en el árbol. /
Three families, enumerated from the running base theme: 59 keys, 23 of them the rows' — 8 fills,
**7** borders (there is none for the resting state) and 8 foregrounds. The earlier note said 22.

## Los números de antes / The numbers before

```
Light / HighContrastLight   IDÉNTICOS (quinta vez) — Dark / HighContrastDark también
  fila seleccionada / selected row    1,71:1  (claros)   1,93:1  (oscuros)
  panel del desplegable / drop-down   1,38:1  (claros)   1,20:1  (oscuros)
  grosor del borde / border thickness 0 en los cinco estados / 0 in all five states
  marco cerrado / closed frame        ya cumplía / already passed
```

La fila seleccionada se mide contra **el panel del desplegable**, no contra la superficie de la
ventana: una fila se ve sobre el panel que la contiene. Por eso 1,71 y no el 1,74 de la nota. /
The selected row is measured against **the drop-down's panel**, not the window's surface.

**El panel del desplegable es un defecto que no estaba previsto**: su borde era `Black` al 14 % de
opacidad —1,38:1 en claro, 1,20:1 en oscuro—, y un desplegable abierto flota sobre la ventana, así
que su borde es lo único que dice dónde termina. / The panel's own border was `Black` at 0.14
opacity. An open drop-down floats over the window and its edge is the only thing saying where it ends.

## La lección: un `TemplateBinding` se comprueba donde algo compite por él / The lesson

La medición del fucsia dijo que sí, y **era cierta y no servía**. Con las redirecciones puestas y un
estilo de aplicación dando el borde de acento, medido otra vez: / The fuchsia measurement said yes,
and it was **true and useless**. With the redirects in place and an application style giving the
accent border, measured again:

```
:selected   item[bd=#ff1769aa th=2,2,2,2]   pres[bd=Transparent th=2,2,2,2]
```

**El grosor viaja y el color no.** El estilo llega al `ComboBoxItem` —ahí está el acento— y el
`ContentPresenter` lo ignora, porque el `ControlTheme` fija el pincel del presenter **por estado**
desde `ComboBoxItemBorderBrush*`. El fucsia se probó **en reposo**, que es el único estado donde
nadie compite por esa propiedad. / **The thickness travels and the colour does not.** The control
theme sets the presenter's brush per state, and the fuchsia was set in the resting state — the one
state where nothing competes for it.

Es la trampa de la fase 2e con otra cara —un estilo que llega al control y no pinta nada— y la regla
que ya estaba escrita es la que resuelve: **lo que alcanza a una plantilla es el recurso que
consume**. El grosor se queda en el estilo, el color pasa a redirección. / It is phase 2e's trap
wearing a different face, and the rule already written is the one that solves it: what reaches a
template is the resource it consumes.

## El segundo defecto, que sólo apareció al mirar los cinco estados / The second defect

En alto contraste, pasar el ratón y pulsar **invierten**: el relleno toma el color del borde. El
texto de la fila seguía saliendo del token de texto primario, así que: / In high contrast, hovering
and pressing **invert**, and the row's label still came from the primary text token:

```
HighContrastLight  :pointerover   bg=Black  fg=Black    1,00:1
```

**Negro sobre negro, y nada lo medía**, porque las demás aserciones de la fase miran la fila
seleccionada. Un estado sobre el que nadie afirma nada es un estado que nadie pinta bien. Los cuatro
textos de estados que invierten pasan a `ControlTextActiveBrush`, que es para lo que existe ese
token, y hay una prueba nueva que recorre **los cinco** estados. / Black on black, and nothing
measured it, because the phase's other assertions all look at the selected row.

## Los listones, y por qué no se inventó ninguno / The bars, and why none was invented

Dos aserciones nuevas cambiaron de forma **después** de ver su rojo, que es sospechoso, así que queda
dicho qué se hizo y qué precedente se siguió: / Two new assertions changed shape **after** seeing
their red, which is suspicious, so here is what was done and which precedent it followed:

1. **El texto de una fila apagada** medía 4,17:1 en Dark contra un listón de 4,5. Se mide contra
   **3,0**, que es el listón que este repositorio **ya** da al texto apagado en
   `TextFieldStateTests`, con su razón escrita allí: WCAG 1.4.3 lo exime, y esa exención es
   justamente por lo que nadie lo mide y acaba ilegible. No se bajó un listón: se usó el que ya
   existía para ese caso. / Measured against the bar this repository already gives disabled text.
2. **El relleno del ratón encima** medía 1,35:1 en claro y 1,20:1 en oscuro contra el panel. La
   aserción pedía 3,0 y **ese listón no lo cumple ningún tipo de esta aplicación**: a un botón,
   `ControlStateTests` le pide **identidad** —que el relleno sea el token del estado— y que el texto
   encima se lea. La prueba pasó a pedir lo mismo. El número medido **no se tira**: es del token de
   relleno compartido, no de este tipo, y queda anotado para la fase que revise los rellenos. /
   The assertion asked for a bar no type in this application meets; it now asks what the button is
   asked. The measured number belongs to the shared fill token, and is recorded for the phase that
   revisits fills.

## Lo que la nota tenía al revés / What the note had backwards

El texto de una fila del desplegable **sí tiene ocho recursos propios** (`ComboBoxItemForeground*`),
a diferencia del de una fila de lista, que sale de una brocha genérica que ningún estado alcanza. Es
decir: **aquí el acento pleno sí sería pintable**. Se queda en tinte más borde de todas formas,
porque una fila de lista y una fila de desplegable son la misma idea y leerlas de dos maneras
distintas es peor que cualquiera de las dos. / A drop-down row's text **does** have eight resources
of its own, unlike a list row's. A solid accent fill would be paintable here. It stays a tint plus a
border anyway, because reading the two the same way matters more.

## El verde / The green

```
ComboBoxStateTests                        29/29  (7 aserciones × 4 temas + 1)
ApSolutions.LocalMedia.UiTests           544/544
ApSolutions.LocalMedia.AccessibilityTests 135/135  (el paseo entero / the whole walk)
dotnet build -c Release -warnaserror     0 advertencias / 0 warnings
dotnet format --verify-no-changes        limpio / clean
```

## Lo que no se tocó, y por qué / What was left alone, and why

- **La flecha y el marco cerrado** ya cumplían (12,47:1 y 5,69:1) y sólo pasan a tokens. / Already
  passed; moved to tokens only.
- **`ComboBoxItem` no entra en la lista del punteado de deshabilitado ni en la del anillo de foco.**
  Un `ComboBoxItem` no toma el foco por su cuenta —lo lleva el `ComboBox`— y ninguna vista deshabilita
  filas sueltas. Añadirlo sería un registro que nada alimenta. / A `ComboBoxItem` does not take focus
  on its own and no view disables individual rows; adding it would be a registration nothing feeds.
