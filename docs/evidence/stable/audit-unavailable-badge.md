# Un medio que no está deja de pintarse como un error, y se dice una sola vez / A medium that is not there stops being painted as an error, and is said once

Primer trabajo del tramo 3 de la §4 (`UnavailableBadge`). La fila pide un cambio de una vista y la
medición encontró **seis**: el mismo distintivo estaba copiado a mano en cinco sitios más. / §4 asks
for one view; the tree had six copies of it.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que la fila pide, y por qué / What the row asks for

`UnavailableBadge` pasa de `AccentSubtleBrush` a `WarningSurfaceBrush` **más borde y glifo**. La razón
está en el propio dominio: un USB desenchufado o un recurso de red caído **no es algo que haya fallado**
—es algo que no está—, y sus títulos **se quedan en el catálogo a propósito**, marcados. Pintarlo con
el mismo azul que el resto de avisos de la aplicación lo hacía indistinguible de un dato, y pintarlo
como error diría que algo se rompió. El borde y el glifo son lo que impide que la señal sea el color. /
An unplugged drive is not a failure, and its titles stay in the catalogue on purpose.

## Lo que la medición añadió a la fila / What measuring added

El distintivo estaba **repetido a mano** en `InProgressRailView`, `RecentlyAddedRailView`,
`MovieDetailsView`, `ShowDetailsView` y `EpisodeRowView`: el mismo `Border`, el mismo
`AccentSubtleBrush`, la misma clave de texto. Cambiar sólo la vista que la §4 nombra habría dejado la
aplicación **diciendo lo mismo de dos maneras**, que es peor que decirlo de la vieja en las seis. / One
view changed and five copies left behind is worse than six copies that agree.

## El rojo / The red

```
The_badge_is_a_warning_with_a_border_and_a_glyph_rather_than_the_accent
  Border { Background = #ffdceaf6, BorderThickness = 0,0,0,0 }

No_other_view_draws_its_own_unavailable_badge
  Collection: ["InProgressRailView.axaml", "RecentlyAddedRailView.axaml",
               "MovieDetailsView.axaml", "EpisodeRowView.axaml", "ShowDetailsView.axaml"]

The_badge_shows_itself_only_when_the_medium_is_out_of_reach
  Expected: False   Actual: True
```

El tercero es el que decidió la forma del arreglo: con `x:DataType="library:CatalogItemViewModel"` el
binding compilado **no resuelve `IsAvailable`** en ningún otro modelo, así que `IsVisible` se quedaba
en su valor por defecto —visible— y el badge se habría pintado **siempre** en las cinco vistas nuevas.
/ The third red is what decided the shape of the fix.

## La corrección / The fix

1. **El badge es aviso**: `WarningSurfaceBrush` con `WarningBorderBrush` de 1 px y el glifo `⚠` delante
   del texto. Los seis pinceles de gramática —aviso, peligro, positivo, con sus bordes— estaban
   **declarados en los cuatro temas y sin un solo lector**; éste es el primero que se gasta. / The first
   of the six grammar brushes to be spent.
2. **Las cinco copias pasan a montar el badge.** Para que pueda servir a seis modelos pierde su
   `x:DataType`, y como **Avalonia rechaza un binding compilado sin él**, su visibilidad es
   `ReflectionBinding` — la misma salida que `LibraryView` ya usa para las etiquetas de sus filtros. /
   Avalonia refuses a compiled binding with no `x:DataType`, so the visibility is a reflection binding.
3. **Lo que el compilador deja de comprobar se afirma en su lugar**, y sobre el efecto: el badge
   aparece con el medio fuera de alcance y **no aparece** cuando está, montado con un modelo que no es
   el del catálogo. Es más fuerte que la comprobación que se cede, porque mide lo que se ve. / What the
   compiler stops checking is asserted on the effect instead.
4. **Y una puerta para que no vuelvan las copias**: ningún `.axaml` de `src/` fuera del badge declara
   un `Border` cuya visibilidad dependa de `!IsAvailable`. Se lee del marcado y no de una pantalla
   montada, porque una copia en una rama que necesita datos para aparecer no se montaría nunca — y son
   justo esas las que se desvían. / Read from the markup, because the branches that drift are the ones
   a mounted screen never reaches.

## El cuarto símbolo del árbol / The tree's fourth symbol

`⚠` se suma a `○ ◐ ●`, `→` y `!` como texto literal que **no es idioma**, y queda anotado en
`SURFACES`. El nombre accesible sigue viniendo de `AutomationProperties`, así que un lector de pantalla
oye la frase traducida y no el símbolo. / The glyph is not language; the accessible name still is.

## El verde / The green

```
UiTests             625/625
AccessibilityTests  135/135 en las dos pasadas / on both passes
IntegrationTests    456/457 (1 omitida por diseño / 1 skipped by design)
DocumentationTests   87/87
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 134 declaraciones en 133 identidades; 133 pulsadas, 0 pendientes
```
