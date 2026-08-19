# La fase 2g: la barra, el interruptor y la opción / Phase 2g: the slider, the toggle and the radio

Los tres últimos tipos de estados, juntos porque suman ocho usos y el patrón ya estaba hecho. Un
defecto de raíz compartido, uno propio de cada uno, y **la medición que decidió el diseño no fue de
contraste sino de qué token separa del acento**. / The last three state types, together because they
add up to eight uses. One shared root defect, one of their own each, and the measurement that decided
the design was not a contrast reading but a search for which token stands apart from the accent.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Los usos y las claves / The uses and the keys

Cinco `Slider`, dos `ToggleButton` y un `RadioButton` — y **cero `ToggleSwitch`**, que aparece en los
selectores de foco y de deshabilitado sin que ninguna vista lo use. Enumeradas del tema base en
ejecución: `Slider` 32, `ToggleButton` 37, `RadioButton` 38. / Five, two and one — and zero
`ToggleSwitch`, which appears in the focus and disabled selectors without a single view using it.

## Los números de antes / The numbers before

```
Slider    recorrido vs resto / travelled vs rest   1,52 · 1,04 · 1,58 · 1,28
          el pulgar / the handle                   2,44 – 2,89 contra la superficie / against surface
          apagado / disabled                       ambas mitades #FFCCCCCC / both halves one colour
Toggle    encendido vs apagado / on vs off         2,68 · 2,10 · 2,80 · 2,81
          el borde / the outline                   1,00:1 en los cuatro (Transparent × 10 estados)
          deshabilitado / disabled                 idéntico a reposo / byte-identical to resting
Radio     el punto / the dot                       White en los cuatro temas / in all four themes
Los tres / All three                               #FF0078D7 idéntico en Light y HighContrastDark
```

**El defecto de raíz es uno solo**: el azul de Windows, byte a byte igual en el tema claro y en el de
alto contraste oscuro. No es el acento de esta aplicación en ningún tema, y en los dos de alto
contraste no es siquiera un color de su paleta. / The root defect is one: Windows' blue, byte-identical
in Light and in HighContrastDark.

**Y dos que son de cada tipo**: un `ToggleButton` no tenía **forma** —borde `Transparent` en sus diez
estados, el mismo defecto que tenía el botón antes de la fase 2a— y un `Slider` **apagado dejaba de
decir su valor**, porque las dos mitades de la pista pasaban al mismo gris. Estar no disponible no es
lo mismo que no tener valor, y la barra de transporte está deshabilitada siempre que no hay nada
reproduciéndose. / A toggle had no shape, and a disabled slider stopped saying its value at all.

## La medición que decidió el diseño / The measurement that decided the design

La pista vacía se apuntó primero a `ShellBorderBrush`, que es lo que parecía: el borde de todo lo
demás. Rojo, y con un número raro —1,21:1—, así que en vez de probar otro token se midió **el acento
contra los trece**, en los cuatro temas: / The empty track was first pointed at `ShellBorderBrush`,
which is what a border is everywhere else. Red, at 1.21:1 — so instead of trying another token, the
accent was measured against all thirteen, in all four themes:

```
                      Light   Dark    HCL     HCD
ShellBorderBrush      1,21    1,07    2,44    1,25    <- falla en los cuatro / fails in all four
ShellHairlineBrush    3,09    2,40    2,44    1,25
TextPrimaryBrush      3,07    2,30    2,44    1,25
ControlFillBrush      4,68    6,11    8,59   16,75    <- pasa en los cuatro / passes in all four
ShellSurfaceBrush     5,52    7,38    8,59   16,75
ControlFillDisabled   5,17    6,77    8,59   16,75
AccentSubtleBrush     4,71    4,81    8,59   16,75
```

**Una tabla se lee una vez y decide todo lo que queda de la fase.** Los tokens de línea y de texto
—borde, filete, texto primario y secundario— **comparten luminancia con el acento** por construcción,
así que ninguno puede ir junto a él; los de superficie y relleno sirven todos. La pista vacía es
`ControlFillBrush`. / Line and text tokens share the accent's luminance by construction, so none of
them can sit next to it. Surface and fill tokens all work.

**Lo que esto cuesta, dicho**: en los dos temas de alto contraste `ControlFillBrush` **es** la
superficie, así que la pista vacía se confunde con la página y lo que se ve es la parte recorrida y el
pulgar. Con dos colores y un acento no hay forma de separarse a la vez del acento y del fondo, y lo
que queda legible es lo que importa: dónde llega el valor. / In the two high contrast themes the
empty track merges with the page. With two colours and an accent there is no third option, and what
stays legible is the part that matters.

## Una aserción que se corrigió, y contra qué precedente / An assertion corrected, and against what

`A_switched_off_toggle_does_not_look_like_a_resting_one` falló en los dos temas de alto contraste
**con la corrección ya puesta**, y no era la corrección: allí el relleno deshabilitado, el de reposo y
la superficie son **un solo color**, y quien dice «apagado» es el punteado. La aserción pasó a ser la
que `ControlStateTests` ya hace para el botón —el relleno **es** la superficie— en vez de inventar
una regla nueva. / It failed in the two high contrast themes with the fix already in place, and it
was not the fix: there the disabled fill, the resting fill and the surface are one colour, and what
says "off" is the dotted outline. The assertion became the one `ControlStateTests` already makes.

## Dos familias que NO se redirigieron / Two families deliberately left alone

- **`ToggleButtonIndeterminate*`** (10 claves): `IsThreeState` **no aparece en el árbol**.
- **`Slider*TickBar*`**: ninguna vista pone `TickPlacement`, así que **no se dibuja un solo tick**.
  Las cinco ponen `TickFrequency`, pero eso sólo hace que el valor salte.

Redirigirlas serían **cuarenta alias por tema que nada puede alcanzar**, que es exactamente el defecto
de la casa con cara de recurso. / Redirecting them would be forty aliases per theme that nothing can
reach — the house defect wearing a resource's face.

## El verde / The green

```
SliderStateTests + ToggleAndRadioStateTests   42/42
ApSolutions.LocalMedia.UiTests               586/586
ApSolutions.LocalMedia.AccessibilityTests    134/135  (ver abajo / see below)
dotnet build -c Release -warnaserror          0 advertencias / 0 warnings
dotnet format --verify-no-changes             limpio / clean
```

**El 134 de 135 es la intermitencia ya anotada en la fase 2c, y ha reaparecido**:
`A_session_that_will_not_open_is_handed_over_and_retried_with_the_mouse`. Falla en la suite entera y
pasa sola (1/1, 4 s), igual que la vez anterior. **No es de esta tanda** —una redirección de color no
puede cambiar si LibVLC abre un archivo de dos bytes— y la misma suite dio 135/135 con la fase 2f ya
puesta, media hora antes. / The 134 of 135 is the intermittency already recorded in phase 2c, and it
has come back. It fails in the whole suite and passes alone, and it is not this batch's: a colour
redirect cannot change whether LibVLC opens a two-byte file.

**Lo que esta vez sí se sabe, y no se sabía**: la aserción exacta es la espera de que el archivo de
dos bytes **falle**, en `AssembledPhysicalWalkTests.cs:2399`, con un plazo de **60 s** — que no es
poco tiempo, así que no es lentitud de la máquina: la sesión **nunca llegó a fallar**. Y su condición
es `host.ViewModel.Player?.Player.HasFailed == true`, que con `Player` en nulo da falso y se queja de
que «el archivo se abrió», **que es lo contrario de lo que ocurrió**. Es una sonda que no distingue
«abrió bien» de «no hay sesión». Se corrige aparte, en su propio cambio, porque no es de esta fase. /
What is known this time and was not: the assertion is the wait for the two-byte file to fail, on a
60-second deadline, and its condition reads false when `Player` is null while complaining that the
file opened — the opposite of what happened. A probe that cannot tell "opened fine" from "no session
at all". Corrected separately, in its own change.

Con esto **la fase 2 está entera**: los diez tipos de control tienen sus estados medidos y sus colores
en los cuatro temas. Lo que queda del paso 6 son la puerta de escalares, `primary-action`, la
tipografía y las vistas. / With this, phase 2 is complete: all ten control types have their states
measured and their colours in all four themes.
