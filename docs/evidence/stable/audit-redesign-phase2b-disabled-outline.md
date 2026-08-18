# El punteado del deshabilitado, y los dos que se dibujaban dos veces / The disabled outline, and the two that drew it twice

En alto contraste, un control deshabilitado era **píxel por píxel** uno en reposo. La fase 2a lo
midió, lo afirmó tal cual en la prueba en vez de aflojarla, y lo dejó nombrado. Esto lo cierra. / In
high contrast, a disabled control was **pixel for pixel** a resting one. Phase 2a measured it,
asserted it as it was rather than loosening the test, and named it. This closes it.

Fecha / Date: 2026-08-18. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Por qué el color no podía decirlo / Why colour could not say it

Leído de los dos diccionarios de alto contraste, antes de escribir una línea: / Read from the two
high contrast dictionaries, before a line was written:

```
HighContrastLight   ControlFillBrush = ControlFillDisabledBrush = ShellSurfaceBrush = #FFFFFF
HighContrastDark    ControlFillBrush = ControlFillDisabledBrush = ShellSurfaceBrush = #000000
ambos / both        ButtonBorderBrush(×4 estados) = ShellBorderBrush
ambos / both        TextDisabledBrush = TextPrimaryBrush
```

Las tres señales que un tema usa para decir «deshabilitado» —relleno, borde y texto— son en estas dos
paletas **idénticas a las de reposo**, y no por descuido: no hay un tercer color que gastar. La
señal tiene que ser geometría. / The three signals a theme uses to say "disabled" — fill, border and
text — are in these two palettes **identical to the resting ones**, and not by oversight: there is no
third colour to spend. The cue has to be geometry.

## El rojo / The red

```
Con error: 4, Superado: 0, Total: 4
  A_disabled_control_is_drawn_with_a_dotted_outline
  Enabling_the_control_takes_the_outline_away
  Every_control_type_that_takes_focus_also_shows_the_outline
  The_outline_takes_its_colour_from_the_theme_in_force

Button is disabled and nothing was drawn over it, so in high contrast it is pixel for pixel a
resting control.
```

## Adorno, no nueve plantillas / An adorner, not nine templates

Copiar el `ControlTheme` de Fluent por tipo son **nueve superficies** que se desincronizan con cada
versión de Avalonia, para una raya. El anillo de foco ya había demostrado que la capa de adornos
alcanza los diez tipos con **una** implementación, incluidos los dos que no se adornan a sí mismos.
Así que el punteado es una propiedad adjunta, `DisabledOutline.IsShown`, y **el cuándo lo dice un
selector**: `:disabled` sobre los mismos diez tipos que el foco. Habilitar un control retira el
setter, la propiedad vuelve a `false` y el adorno se va — eso no lo escribe nadie, es de Avalonia. /
Copying Fluent's `ControlTheme` per type is **nine surfaces** drifting with every Avalonia release,
for one dashed line. The focus ring had already shown the adorner layer reaches all ten types from
**one** implementation. So the outline is an attached property and **a selector says when**.

## Lo que la medición cambió: dos se dibujaban dos veces / What measuring changed: two drew it twice

Con el estilo puesto, la prueba de los diez tipos falló con `Sequence contains more than one matching
element`. La sonda dio la cuenta exacta: / With the style in place, the ten-type test failed with
`Sequence contains more than one matching element`. The probe gave the exact count:

```
Button          1 outline   over Button          isEnabled=False  templated=null
ToggleButton    1           over ToggleButton    isEnabled=False  templated=null
ToggleSwitch    1           over ToggleSwitch    isEnabled=False  templated=null
RadioButton     1           over RadioButton     isEnabled=False  templated=null
TextBox         1           over TextBox         isEnabled=False  templated=null
ComboBox        2           over ComboBox        isEnabled=False  templated=null
                            over TextBox         isEnabled=True   templated=ComboBox
CheckBox        1           over CheckBox        isEnabled=False  templated=null
Slider          1           over Slider          isEnabled=False  templated=null
NumericUpDown   2           over NumericUpDown   isEnabled=False  templated=null
                            over TextBox         isEnabled=True   templated=NumericUpDown
ListBoxItem     1           over ListBoxItem     isEnabled=False  templated=null
```

Deshabilitar se hereda y un estilo de aplicación alcanza también los elementos de plantilla, así que
el `TextBox` que vive **dentro** de un `ComboBox` y de un `NumericUpDown` recibía el suyo: dos
rectángulos punteados a unos píxeles uno de otro, donde el diseño pide uno. / Disabling is inherited
and an application style reaches template elements too, so the `TextBox` **inside** a `ComboBox` and a
`NumericUpDown` got one of its own.

**La condición no es el `IsEnabled` local, es el padre de plantilla.** Las dos respuestas coinciden
aquí y **difieren** en el caso que importa: un control dentro de un panel deshabilitado entero tiene
su `IsEnabled` propio en `true`, y como un panel no es de los diez tipos, mirar el flag local dejaría
ese caso **sin ninguna raya**. `TemplatedParent is null` dice lo que de verdad se quiere decir: se
dibuja alrededor de lo que la vista pone, no alrededor de las piezas de otro control. / **The test is
the templated parent, not the local `IsEnabled`.** The two answers agree here and **differ** where it
matters: a control inside a wholly disabled panel keeps its own `IsEnabled` true, and a panel is not
one of the ten types, so keying off the local flag would leave that case with **no outline at all**.

## Cobertura del archivo nuevo, medida en local antes de empujar / The new file's coverage, measured locally before pushing

Un archivo nuevo arranca en 96/96 de líneas y de ramas, y una rama que sólo CI puede ejercer cuesta
treinta y cinco minutos por intento. Medido aquí: / A new file starts at 96/96 of lines and branches,
and a branch only CI can exercise costs thirty-five minutes per attempt. Measured here:

```
ApSolutions.LocalMedia.Presentation.Theme.DisabledOutline  line-rate=1  branch-rate=1
   line=57  hits=2171  cov=100% (2/2)     <- shown && TemplatedParent is null
   line=74  hits=2170  cov=100% (2/2)     <- Wanted(...) ? Draw() : null
```

Las 2 170 activaciones no son de estas pruebas: son las **vistas reales** de la suite de interfaz
montando controles deshabilitados. El estilo llega a donde tiene que llegar. / The 2,170 hits are not
from these tests: they are the **real views** of the UI suite mounting disabled controls.

## Las pruebas, probadas fallando / The tests, proved failing

```
sin el estilo :disabled                          -> RED (4, "nothing was drawn over it")
sin la regla del padre de plantilla              -> RED ("Sequence contains more than one")
el trazo pasa a StaticResource                   -> RED (blanco donde el tema pinta negro)
el radio del rectángulo deja de ser el del token -> RED
```

## Verde / Green

```
UiTests              Con error: 0, Superado: 470, Total: 470
AccessibilityTests   Con error: 0, Superado: 132, Total: 132
El paseo / the walk  129 declared command controls in 128 identities; 128 pressed, 0 pending
```

El adorno no es pulsable (`IsHitTestVisible=false`) y el paseo lo confirma: **ningún clic de los 128
cambió**, con el punteado dibujándose sobre los controles apagados que el paseo ya sabía esperar. /
The adorner takes no input, and the walk confirms it: **none of the 128 clicks changed**.
