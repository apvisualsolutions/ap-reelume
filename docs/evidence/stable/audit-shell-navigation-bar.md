# El primer tramo de la §4: la navegación dice cuál está abierta con dos señales / §4's first tranche: the navigation says which screen is open with two signals

Primera fila de la §4 de `design/Propuesta de diseño`, y por tanto el primer trabajo de la fase 6 del
paso 6. El Shell ya cumplía dos de sus tres puntos; faltaban la segunda señal del destino abierto y el
`WrapPanel` de las acciones de título. / First row of §4, and therefore the first work of step 6's
phase 6.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que ya estaba, medido antes de tocar nada / What was already there

La §4 pide para `ShellView` tres cosas, y **dos ya se cumplían**: la navegación de **248 px** sobre
`NavigationSurfaceBrush` (`ColumnDefinitions="248,*"`) y el **glifo** que distingue el destino abierto
(`● / ○`, desde `RouteStateConverter`). Medir antes ahorró rehacer lo hecho. / Two of §4's three asks
for `ShellView` were already met.

## El rojo / The red

```
Exactly_one_destination_shows_the_bar_that_says_it_is_open
  Expected: ["NavigationHome"]   Actual: []
The_bar_is_three_pixels_of_the_accent
  The open destination carries no bar at all.
The_title_actions_wrap_instead_of_running_off_the_side
  Expected: "WrapPanel"   Actual: "StackPanel"
```

## La corrección / The fix

1. **La barra de 3 px del acento, y es una barra que EXISTE O NO EXISTE**, no una que cambia de color.
   `RouteStateConverter` gana un tercer `Kind` —`IsCurrent`— que contesta un `bool`, porque
   **ausente no es lo mismo que atenuado**: una barra apagada sería una segunda cosa que interpretar,
   y el paquete le dedica una sección entera a esa distinción. / **A bar that is present or absent**,
   not one that dims: absent is not the same as disabled.
2. **`TitleActionsSurface` pasa de `StackPanel` horizontal a `WrapPanel`.** Tres botones con palabras
   traducidas es la forma que ha dibujado un control fuera de la ventana **siete veces** aquí, y la §4
   lo pide por su nombre. / The shape that has drawn a control off the side seven times.

**Y la segunda señal es la razón de todo esto:** el destino abierto se dice con **barra y glifo**,
nunca con color solo. Ninguna de las dos depende de distinguir tonos. / The open destination is told by
**two** signals, neither of which is colour on its own.

## Dos cosas que la ejecución corrigió / Two things the execution corrected

- **`x:Name` no puede repetirse en el ámbito de un `UserControl`.** Las cinco barras con el mismo
  nombre lanzaron `Control with the name 'NavigationCurrentBar' already registered` **al construir la
  segunda**. Pasan a una **clase**, `navigation-current-bar`, que es lo que esta casa ya usa como
  marcador (`navigation-destination`, `player-chrome`) y que además puede repetirse por diseño. /
  **`x:Name` cannot repeat inside a `UserControl`'s scope**; a class can, and this house already uses
  classes as markers.
- **`AccentBrush` no se resuelve con `TryFindResource` sobre la aplicación**: vive en los diccionarios
  de tema, uno por variante, así que se pide con `TryGetResource(clave, ActualThemeVariant, …)`. La
  primera versión de la aserción falló por eso, y **era la prueba la equivocada, no la barra**. /
  `AccentBrush` lives per theme variant; the first assertion was the thing that was wrong.

## El verde / The green

```
UiTests             617/617
AccessibilityTests  135/135
IntegrationTests    456/457 (1 omitida por diseño / 1 skipped by design)
DocumentationTests   87/87
El paseo / the walk: 134 declaraciones en 133 identidades; 133 pulsadas, 0 pendientes
```

**El trinquete del paseo no se movió**, y era previsible: una barra es un `Border`, no un control de
mando. Lo que sí añadirá deuda son los tramos siguientes de la §4, que traen **69 controles**, y cada
uno llega con su prueba de nombre accesible y su línea de paseo **en el mismo cambio**. / The walk
ratchet did not move — a bar is a `Border`. The tranches that follow bring **69 controls**, and each
arrives with its scene.
