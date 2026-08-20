# El reproductor, y la fila que se salía 74 píxeles por la derecha / The player, and the row that ran 74 pixels off the right

`PlayerView` es la segunda vista seguida que **sólo cuesta maqueta**: sus cinco botones ya estaban en
el paseo, así que no añade deuda de ninguna clase. Lo que traía era jerarquía, tres radios escritos
como número, y la generalización de la clase del cromo del mini reproductor al transporte grande. Lo
que **no** estaba previsto es lo que esa generalización descubrió al medirla. / `PlayerView` is the
second view in a row that **costs layout only**: its five buttons were already in the walk, so it adds
no debt of any kind. What was not planned is what generalising the chrome class turned up when it was
measured.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El rojo / The red

```
The_large_transport_wears_the_chrome_the_mini_player_defined
  PlayerPlayAction is not on the transport.
The_bordered_surfaces_take_their_corner_from_the_theme
  PlayerView.axaml still writes 3 corner radius as a number.
The_chrome_class_carries_no_margin_of_its_own
  Assert.DoesNotContain() Failure: Sub-string found -> Property="Margin"
Exactly_one_button_leads_the_player_screen
  Expected: ["PlayerRecoveryRetry"]  Actual: []

Con error: 4, Superado: 1, Total: 5
```

**La quinta pasaba desde el principio, y por eso es la que importa.**
`The_transport_row_stays_inside_the_window` no es el rojo de este cambio: es su **red**. El cambio da
a tres botones un área mínima de pulsación, o sea que los ensancha, y esa prueba es lo que mide si el
ensanchamiento saca algo de la pantalla. / **The fifth passed from the start, and that is why it
matters.** It is not this change's red but its net: the change makes three buttons wider, and that
test is what measures whether the widening pushes anything off screen.

## Lo que la red midió, y no era lo que se buscaba / What the net measured, and it was not what it was looking for

**A 900 píxeles de ancho la fila del transporte terminaba en x=974.** Setenta y cuatro fuera, con el
transporte entero, su botón de silencio, su indicador de velocidad y su control de volumen del lado de
allá del borde. / **At 900 pixels wide the transport row ended at x=974** — 74 past the edge, with the
transport itself, its mute button, its speed readout and its volume slider all outside.

**900 no es un número elegido para que la prueba dijera algo.** Es `MinWidth` de la ventana principal
en `App.axaml.cs`, o sea lo más estrecha que cualquiera puede dejar esta pantalla. / **900 is not a
number picked to make the test say something**: it is `MinWidth` on the main window in
`App.axaml.cs`, the narrowest anybody can make this screen.

La forma era la de siempre: un `StackPanel Orientation="Horizontal"` con botones que llevan palabras
traducidas. **Es la séptima vez que esta forma dibuja un control fuera de la ventana en este
repositorio**, y la corrección es la misma que recibieron las otras seis: un `WrapPanel`. Con él, la
misma medición pasa. / The shape was the usual one, and this is the **seventh** time it has drawn a
control outside the window here. The fix is the one the other six got: a `WrapPanel`.

**Lo medido es una cota superior, no una escena.** La vista se monta sin contexto de datos, así que
todos los `IsVisible` quedan en su valor por defecto y **las cinco etiquetas de estado están en
pantalla a la vez**, igual que `Play` y `Pause` — cosa que la aplicación nunca hace, porque son
excluyentes. Si la cota superior cabe, el caso real cabe. Lo contrario no vale: una prueba que
reconstruyera la lógica de visibilidad del modelo de vista estaría midiendo su propia copia de esa
lógica. / **What is measured is an upper bound, not a scene.** With no data context every branch is on
screen at once, which is wider than the application can ever be. If the upper bound fits, the real
case fits.

## La corrección / The fix

1. **Los tres `CornerRadius="8"` pasan a `{DynamicResource CornerRadiusMedium}`**, y la prueba afirma
   que el `.axaml` **no escribe el número** además de que lo pintado es el token resuelto. Sin la
   primera mitad el verde sería falso: el token vale 8 y los literales eran 8. / **The three literals
   become the token**, with the markup asserted as well as the painted value.
2. **`primary-action` en `PlayerRecoveryRetry`, y sólo ahí.** En el transporte no va ninguna:
   `Play` y `Pause` se alternan **por estado**, así que marcar una haría que la pantalla cambiara de
   acción principal según lo que esté pasando —que es justo lo que una jerarquía no puede hacer— y
   `Stop` no es el sentido de nada. Se afirma como **la única**. / **`primary-action` on the retry
   button and nowhere else**, asserted as the only one.
3. **El `Margin` sale de `Button.player-chrome`.** La clase nació con la separación dentro porque el
   mini reproductor era lo único que la llevaba; el transporte grande coloca sus tres en un panel que
   **ya** los separa, así que los cuatro por lado caían encima de ese espaciado y los apartaban veinte.
   **El margen no es del control: es de quien lo coloca**, y ahora lo dicen los dos paneles.
   `MiniPlayerChromeView` pasa a `ItemSpacing`/`LineSpacing`, que es lo que `UpdateView` ya hacía. /
   **The margin leaves the class.** A margin belongs to whoever places the control, and both panels
   now say so themselves.
4. **Los tres del transporte adoptan `player-chrome`** y ganan los 36×36 de área mínima de pulsación,
   que es una mejora de accesibilidad real y no de maqueta. / **The transport's three adopt the
   chrome class** and gain a real minimum target area.

**La prueba del margen lee el archivo de tokens como texto**, y eso no es capricho: un margen de cero
es también lo que informa un control que no lleva la clase, así que el valor pintado por sí solo no
distingue un setter retirado de uno que nunca se aplicó. / **The margin test reads the theme file as
text**, because zero is also what a control with no class at all reports.

## La red se probó fallando / The net was proved by failing

Una puerta nueva se prueba fallando, con mutaciones que se revierten. Ésta se probó dos veces, y la
segunda es la que encontró el defecto: / A new gate is proved by failing.

```
ventana / window 400 -> 40 controles fuera; la fila termina en x=974
ventana / window 900 -> 24 controles fuera; TransportControlsHost, MuteButton,
                        SpeedReadout y VolumeSlider del lado de fuera
ventana / window 900, con WrapPanel -> verde
```

El mensaje nombra cada control y la coordenada donde acaba, porque un fallo que sólo dice «algo se
sale» obliga a repetir la medición a mano. / The message names every control and where it ends.

## El verde / The green

```
UiTests             610/610
AccessibilityTests  135/135
IntegrationTests    456/457 (1 omitida por diseño / 1 skipped by design)
DocumentationTests   87/87
El paseo / the walk: 134 declaraciones en 133 identidades; 133 pulsadas, 0 pendientes
```

Las cuatro suites que un cambio de vistas afecta, y el trinquete del paseo intacto: esta vista no
declara ningún control nuevo, así que el inventario no se movió. CI es la verificación real. / The
four suites a view change affects, with the walk ratchet untouched. CI is the real verification.
