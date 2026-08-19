# El mini reproductor, cuya ventana se tiraba a sí misma / The mini player, the window that threw itself away

`MiniPlayerWindow` declaraba un `Panel` negro y **nada más lo veía**: `PlayerWindowCoordinator.Apply`
asignaba `window.Content`, que **sustituye el árbol entero** que construyó el AXAML. En el único modo
en que esa ventana existe, todo lo que declaraba para sí misma había desaparecido. Pasó inadvertido
porque lo único declarado era un panel negro vacío y el escenario que lo reemplaza también es negro. /
`MiniPlayerWindow` declared a black `Panel` and **nothing ever saw it**: `PlayerWindowCoordinator.Apply`
assigned `window.Content`, which **replaces the whole tree** the AXAML built. In the one mode that
window exists for, everything it declared for itself was gone. It went unnoticed because the only
thing declared was an empty black panel, and the stage that replaces it is also black.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El rojo / The red

```
MiniPlayerChromeTests.The_mini_window_carries_its_five_controls_once_a_session_moves_in
  The mini player is missing MiniPlayerPlayPause, MiniPlayerSkipBack, MiniPlayerSkipForward,
  MiniPlayerRestore, MiniPlayerClose. It holds AddContainingFolderButton, CancelAddFolderButton,
  CancelNextButton, CancelSwitchButton, ConfirmAddFolderButton, ConfirmSwitchButton,
  PlayNextNowButton, RestartButton, RestartSwitchButton, ResumeButton, SkipMarkerButtonControl.
```

Once botones dentro de la ventana mini y **ninguno suyo**: todos del escenario que la ocupa entera. La
prueba se hace **después** de un cambio de modo y no sobre una ventana recién construida, porque una
ventana que sólo tiene su cromo antes de que llegue la sesión no tiene cromo ninguno. / Eleven buttons
inside the mini window and **none of its own**. The assertion is made **after** a mode change rather
than on a freshly constructed window, because a window that only holds its chrome before the session
arrives holds no chrome at all.

## Tres hechos que las nueve decisiones no contemplaban / Three facts the nine decisions did not cover

1. **`Apply` sustituye `Content`.** Lo confirmaba su propia prueba: `Assert.Same(stage, mini.Content)`.
   `MiniPlayerWindow.Host(Control)` y `MiniPlayerSurface` existían desde el principio y **sólo los
   llamaba una prueba** — el defecto de la casa, forma once: alimentado únicamente por quien lo mide. /
   **`Apply` replaces `Content`**, as its own test asserted. `Host(Control)` and `MiniPlayerSurface`
   were there from the start and **only one test ever called them**.
2. **`WalkLedger.Record` exige un `UserControl` ancestro** y falla con aserción si no lo hay. Un
   `Button` declarado directamente dentro de un `Window` no tiene identidad de paseo posible: el
   inventario de `check-walk-coverage.ps1` lo llamaría `MiniPlayerWindow#…` y el registro no lo
   escribiría nunca. / **`WalkLedger.Record` requires a `UserControl` ancestor**, so a button declared
   straight inside a `Window` could never be written down.
3. **El paseo no sabía salir de la ventana del shell.** `Resolve` buscaba en `host.Shell` y
   `Click`/`ClickBeside` traducían cada punto a `host.Window`. `MiniPlayerWindow` es la **única**
   ventana secundaria del árbol, así que el arnés nunca había tenido que hacerlo. / **The walk could
   not leave the shell's window.** `MiniPlayerWindow` is the only secondary window in the tree, so the
   harness had never had to.

## La corrección / The fix

- **`MiniPlayerChromeView`**, un `UserControl` con los cinco controles, cuyo `DataContext` es el del
  shell: no hay modelo de vista nuevo, porque la sesión, el modo y el cierre ya viven ahí. /
  **`MiniPlayerChromeView`**, a `UserControl` holding the five, its data context the shell's own.
- **La ventana pasa a `DockPanel`**: el cromo abajo y el escenario rellenando. `Host()` deja de ser
  código muerto y pasa a ser la ruta real, con `Release()` para soltar el escenario al volver; `Apply`
  pregunta por `IPlayerSurfaceHost` y sólo asigna `Content` a las ventanas que no saben hospedar. /
  **The window becomes a `DockPanel`**; `Host()` stops being dead code and becomes the real path.
- **El arnés aprende a apuntar**: `Reachable`, `SecondaryWindows` y `RootOf`, y cada función de clic
  trabaja sobre la ventana del control en vez de sobre la del shell. / **The harness learns to aim.**
- **`TogglePlaybackCommand`** en `PlayerViewModel`, predicado `CanPause || CanResume`, y **en la lista
  que recibe `RaiseCanExecuteChanged`**, que es donde estaba la garantía y no en
  `CommandNotificationTests` — esa puerta lista archivos que **silencian** el evento, y un
  `AsyncRelayCommand` no lo silencia. / **`TogglePlaybackCommand`**, and it belongs in the list that
  gets `RaiseCanExecuteChanged`, not in `CommandNotificationTests`.

## Lo que costó una medición / What cost a measurement

**Las etiquetas largas no caben, y el síntoma no es que se salgan.** Con «Pausar o reanudar»,
«Retroceder 10 s» y compañía, los cinco botones se plegaban en **tres filas** dentro de una ventana de
480×270, y la escena murió así:

```
MiniPlayerRestore at 129, 248 sized 214, 36 is surrounded by other command controls, so there is
nowhere to put the click that proves the press did the work.
```

No «se salió de la ventana»: **no quedaba sitio para el clic de control**, que es la mitad de la
prueba de que la pulsación hizo el trabajo. Con etiquetas cortas —`Pausa`, `Atrás`, `Adelante`,
`Ampliar`, `Cerrar`— el cromo cabe y el vídeo deja hueco libre encima. / **Long labels do not fit, and
the symptom is not that they overflow**: it is that there is nowhere left to put the *beside* click.

## Las desviaciones conscientes / The deliberate deviations

- **El cromo va siempre visible**, no al pasar el ratón. Decidido el 2026-08-19: el resolvedor del
  paseo busca el control antes de mover el ratón, y el propio paquete admite que el cromo oculto es un
  problema de accesibilidad. / **The chrome is always visible.**
- **`MinWidth`/`MinHeight` de 36, no tamaño fijo**, porque `Content` va traducido y un tamaño fijo
  corta el texto en uno de los dos idiomas. / **A minimum and not a fixed size.**
- **`WrapPanel` y no una fila**: el mínimo de la ventana son 320 px, y envolver mantiene los cinco
  dentro a cualquier anchura que la ventana permita. / **A `WrapPanel` and not a row.**

## El verde / The green

```
UiTests                598/598
AccessibilityTests     el paseo pulsa los cinco / the walk presses all five
DocumentationTests      87/87
IntegrationTests       456/457 (1 omitida por diseño / 1 skipped by design)
```

`CornerRadiusMedium` sale de `NotSpentYet` en este mismo cambio, que es lo que `ScalarTokenTests`
exige: esa lista falla también cuando algo empieza a gastarse. Pasa de seis a cinco. /
`CornerRadiusMedium` leaves `NotSpentYet` in this same change; the list goes from six to five.
