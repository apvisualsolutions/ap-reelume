# La fase 1 del rediseño: los tokens, y el tema que nadie podía ver / Redesign phase 1: the tokens, and the theme nobody could reach

La primera fase del paso 6 no toca ninguna vista. Añade los tokens que las fases siguientes van a
gastar, parte el alto contraste en dos temas y —lo que no estaba previsto— le da por primera vez
alguien que lo aplique. / Phase one of step 6 touches no view. It adds the tokens the later phases
will spend, splits high contrast into two themes and — the part that was not planned — gives it
someone to apply it for the first time.

Fecha / Date: 2026-08-18. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que había, medido / What was there, measured

| Medida / Measure | Antes / Before | Después / After |
| --- | --- | --- |
| Diccionarios de tema / Theme dictionaries | 3 | **4** |
| Brochas por diccionario / Brushes per dictionary | 9 | **22** |
| Escalares / Scalars | 8 | **13** |
| Tipos de control con foco propio / Focus-styled control types | 8 | **10** |
| Anillos de foco / Focus rings | 1 | **2** |
| `Color` sueltos que nadie pintaba / Loose `Color`s nothing painted | 23 | **0** |

## El hallazgo que cambió la prueba / The finding that changed the test

`ContrastTokenTests` medía una lista de `<Color>` guardada **al lado** de los diccionarios, y ningún
`.axaml` del árbol leía esa lista: medido archivo por archivo, sus únicas apariciones fuera del
propio `DesignTokens.axaml` eran los recursos compilados bajo `obj/`. La prueba llevaba tiempo
midiendo su propia copia de los valores, y ya había divergido de lo que la aplicación pinta: /
`ContrastTokenTests` measured a list of `<Color>` resources kept **beside** the dictionaries, and no
`.axaml` in the tree read that list — its only appearances outside the token file were the compiled
resources under `obj/`. It had been measuring its own copy of the values, and it had already drifted
from what the application paints:

```
LightControlBorderColor  #475569   <- lo que la prueba medía / what the test measured
ShellBorderBrush (Light) #64748B   <- lo que la aplicación pinta / what the application paints
```

Y describía un modo **HighContrastLight** entero —superficie, texto, relleno, borde y foco— del que
**no existía diccionario**: la comprobación aprobaba un tema que la aplicación no podía mostrar. /
And it described an entire **HighContrastLight** mode — surface, text, fill, border and focus — for
which **no dictionary existed**: the check passed for a theme the application could not show.

La prueba pasa a leer los cuatro `ThemeDictionaries`, que es lo que se pinta, y los `Color` sueltos
se retiran. / The test now reads the four `ThemeDictionaries`, which is what gets painted, and the
loose colours are gone.

## Y el alto contraste no lo aplicaba nadie / And nothing applied high contrast

`AppThemeVariants.HighContrast` sólo lo nombraba el propio AXAML: `FluentThemeService` mapeaba
`System`, `Light` y `Dark`, y nada más. El diccionario existía y ningún camino lo seleccionaba — el
defecto de la casa con cara de tema. / `AppThemeVariants.HighContrast` was named by the AXAML alone:
the theme service mapped `System`, `Light` and `Dark` and nothing else. The dictionary existed and no
path selected it — the house defect wearing a theme's face.

Ahora `IHighContrastService` lo pregunta al sistema y `FluentThemeService` lo consume, registrado con
su consumidor en el mismo cambio. **Claro u oscuro se decide por la luminancia de `COLOR_WINDOW`**,
no por el nombre del tema: Windows trae cuatro, cualquiera puede definir el suyo y los nombres están
traducidos — «Contraste alto negro» y «High Contrast Black» son el mismo tema. Un color no se
traduce. / High contrast is now read from the system and consumed by the theme service, registered
with its consumer in the same change. **Light or dark is decided by the luminance of
`COLOR_WINDOW`**, never by the theme's name: Windows ships four, anyone can define their own and the
names are localised. A colour is not translated.

**Y la puerta de cobertura decidió la forma.** La primera versión metía la lectura y su interpretación
en la misma clase de `Windows`, y ahí el número no llegaba: un archivo nuevo tiene que arrancar en
96/96 de líneas **y de ramas**, y las ramas de `SystemParametersInfo` dependen de si la máquina que
mide está en alto contraste — en un runner hospedado, nunca. Partido en dos, cada mitad se mide sola:
`HighContrastPolicy` es aritmética pura en `Presentation` (**12/12 líneas, 2/2 ramas**) y
`WindowsHighContrastService` sólo pregunta, sin una sola rama que dependa de la respuesta
(**4/4 líneas, 0 ramas**). Un `SystemParametersInfo` que falla deja su estructura como estaba, así que
las banderas leen cero y la respuesta es «no hay alto contraste», que es la correcta cuando el sistema
no ha dado ninguna. / **And the coverage gate decided the shape.** The first version put the reading
and its meaning in one class under `Windows`, and there the figure could not be reached: a new file
has to arrive at 96/96 of lines **and branches**, and the branches of `SystemParametersInfo` depend on
whether the measuring machine is in high contrast — on a hosted runner, never. Split in two, each half
measures on its own: the policy is pure arithmetic in `Presentation` (**12/12 lines, 2/2 branches**)
and the host only asks, with no branch depending on the answer (**4/4 lines, 0 branches**).

El alto contraste **manda sobre la preferencia**, porque es una necesidad y no un gusto; y por eso
`ThemePreference` sigue teniendo tres valores y ningún ajuste guardado migra. Se lee al aplicar el
tema, así que encenderlo en Windows con la aplicación abierta llega en el arranque siguiente:
seguirlo en vivo necesita el mensaje de cambio de configuración, que no es esta fase. / High contrast
**overrides the preference**, because it is a need rather than a taste — which is why the preference
still has three values and no stored setting migrates. It is read when the theme is applied, so
turning it on while the application is open arrives on the next launch; following it live needs the
settings-change message, which is not this phase.

## El anillo doble, y las dos veces que la sonda mintió / The double ring, and the two times the probe lied

El foco se dibujaba prestándole al control su propio borde. Dos agujeros: un `Slider` no tiene borde
donde pintarlo, y en alto contraste claro el borde y el foco son **el mismo negro**, así que enfocar
cambiaba un píxel de grosor y nada más. Ahora es un adorno de dos bordes concéntricos —2 px del color
de foco fuera, 1 px del color de la superficie dentro—: la señal es **geometría**, y la geometría
sobrevive a un tema cuya paleta entera es un color. / Focus was drawn by lending the control its own
border. Two holes: a `Slider` has no border to paint on, and in high contrast light the border and
the focus colour are **the same black**, so focusing changed one pixel of thickness. It is now an
adorner of two concentric borders — 2 px of the focus colour outside, 1 px of the surface colour
inside: the cue is **geometry**, and geometry survives a palette of one colour.

Dos cosas se midieron por el camino, y ninguna era lo que la primera lectura decía: / Two things were
measured on the way, and neither was what the first reading said:

1. **Un `ToggleSwitch` cuelga el anillo del `Grid` de su propia plantilla**, no de sí mismo. La
   primera aserción exigía que el elemento adornado fuera el control y acusaba al producto de no
   dibujar nada; lo dibujaba, sobre el elemento donde está el interruptor. / A `ToggleSwitch` hangs
   the ring on the `Grid` inside its own template. The first assertion demanded the adorned element
   *be* the control and accused the product of drawing nothing; it was drawing, on the element where
   the switch actually is.
2. **`Focus(NavigationMethod.Tab)` no es pulsar el tabulador.** Un `NumericUpDown` pasa el teclado al
   `TextBox` de su plantilla, y lo pasa **sin decir que el teclado lo trajo**, así que el anillo —que
   sólo se dibuja para el foco de teclado— no aparecía. Con la tecla de verdad
   (`window.KeyPress(Key.Tab, …)`) aparece. Tres intentos de arreglarlo desde el arnés fallaron antes
   de sustituir la llamada por la pulsación: **una sonda tiene que provocar el efecto, no declararlo.**
   / **`Focus(NavigationMethod.Tab)` is not pressing tab.** A `NumericUpDown` hands the keyboard to
   the `TextBox` in its template, and hands it over **without saying the keyboard brought it**, so
   the ring never appeared. With the real key it does. Three attempts to fix it from the harness
   failed before the call was replaced by the key press: **a probe has to provoke the effect, not
   declare it.**

## La puerta, probada fallando en las dos direcciones / The gate, proved failing in both directions

Cuatro mutaciones sobre el árbol bueno, una por aserción nueva, cada una revertida después: / Four
mutations against the good tree, one per new assertion, each reverted afterwards:

```
dark secondary text loses contrast                   -> RED   Dark secondary text contrast was 1,82:1; expected at least 4,5:1.
a brush goes missing from Light                      -> RED   Assert.Equal() Failure: Collections differ at index 12
high contrast dark paints accent and focus alike     -> RED   HighContrastDark paints the accent and the focus ring in the same colour…
high contrast light tells warning apart by colour    -> RED   Assert.Equal() Failure: Strings differ
```

La tercera es el defecto que el paquete de diseño señaló: en alto contraste el acento y el foco eran
el mismo `#FFFF00`. El acento pasa a `#0000FF` en claro y `#00FFFF` en oscuro, y el amarillo queda
para el foco. / The third is the defect the design package pointed at: in high contrast the accent
and the focus ring were the same `#FFFF00`. The accent becomes `#0000FF` light and `#00FFFF` dark,
and the yellow is left to focus.

## Verde / Green

```
Correctas! - Con error: 0, Superado: 455, Omitido: 0, Total: 455  (UiTests, 6 s)
Correctas! - Con error: 0, Superado: 119, Omitido: 0, Total: 119  (AccessibilityTests, 2 m 3 s)
The walk: 129 declared command controls in 128 identities; 128 pressed, 0 pending.
```

## Lo que esta fase deja debiendo / What this phase leaves owing

Los escalares de espacio y radio (`SpaceXSmall`, `SpaceXLarge`, `CornerRadiusSmall`,
`CornerRadiusMedium`) **no los consume ninguna vista todavía**, y tampoco los de siempre: medido, ni
una sola vista lee `SpaceSmall`, `SpaceMedium` o `SpaceLarge`, que llevan ahí desde antes de esta
fase. Los gasta la fase 2 al pasar los cinco estados de control, o se van. Un token declarado y nunca
gastado es la misma clase de cosa que el tema que nadie aplicaba. / The space and radius scalars are
consumed by no view yet, and neither are the older ones: measured, not one view reads `SpaceSmall`,
`SpaceMedium` or `SpaceLarge`, which predate this phase. Phase 2 spends them when it does the five
control states, or they go. A token declared and never spent is the same kind of thing as the theme
nobody applied.
