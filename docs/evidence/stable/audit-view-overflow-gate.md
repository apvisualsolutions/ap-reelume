# La forma que ha sacado un control de la pantalla siete veces, ahora con puerta / The shape that has drawn a control off the screen seven times, now with a gate

Las 48 vistas de la aplicación se miden de una vez contra la ventana más estrecha que la aplicación
permite. Es la red que faltaba: hasta ahora ese defecto lo encontraba el paseo **de uno en uno**, y su
fallo nombraba el clic en vez de la maqueta. / All 48 views are measured at once against the narrowest
window the application allows.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Por qué existe / Why it exists

**Siete veces.** Siempre la misma forma —una fila horizontal de botones con palabras traducidas— y
siempre encontrada tarde: la séptima fue esta misma tarde, en el transporte de `PlayerView`, y la
encontró una prueba escrita a mano para esa vista. Con 26 vistas por delante en la fase de rediseño,
escribir esa prueba una por una es repetir el mismo trabajo veintiséis veces y confiar en acordarse.
/ **Seven times**, always the same shape, always found late.

**Y el inventario dice que no es hipotético:** ocho vistas del árbol tienen hoy una fila horizontal
con dos o más botones. / Eight views in the tree carry a horizontal row of two or more buttons today.

## Qué mide / What it measures

Cada `UserControl` público del ensamblado de presentación se instancia, se monta en una ventana de
**900** —que es `MinWidth` de la ventana principal en `App.axaml.cs`— y se recorre su árbol visual
comprobando que ningún control termina más allá del borde derecho ni antes del izquierdo.

**Sin contexto de datos, y eso es la mitad del diseño:** los `IsVisible` enlazados quedan en su valor
por defecto, así que **todas las ramas de todas las vistas están en pantalla a la vez**. Es más ancho
de lo que la aplicación puede llegar a ser, así que es una **cota superior**: si cabe aquí, cabe. /
**Without a data context**, which leaves every branch on screen at once — an upper bound.

## La limitación, dicha y no escondida / The limitation, stated rather than hidden

**Una vista montada sola recibe los 900 enteros; dentro del shell recibe 900 menos lo que el cromo del
shell ocupe.** Así que esta puerta caza una vista demasiado ancha **por sí misma** y no puede cazar
una que sólo lo sea al anidarse. Esa mitad la cubre el paseo, desde el otro lado y con el ratón. Un
silencio aquí no es un certificado. / A view mounted alone gets the whole 900; nested it gets less.
This gate catches a view too wide on its own and cannot catch one that is only too wide once nested.

## Probada fallando, y con dos suelos anticeguera / Proved by failing, with two anti-blindness floors

Bajando la ventana a 300 —una mutación revertida— la puerta nombra nueve vistas con su control y su
coordenada:

```
9 view(s) draw a control outside the narrowest window the application allows:
  EpisodeRowView: TextBlock ends at x=546 in a 300-wide window
  LibraryView: ScanProgressIdleText ends at x=369 ...
  MetadataEditorView: RefreshProviderMetadata ends at x=357 ...
  MovieDetailsView: Button ends at x=393 ...
  PlayerView: Button ends at x=348 ...
  RenamePreviewView: Button ends at x=457 ...
  ShellView: ProgressBar ends at x=513 ...
  SkipMarkerButton: SkipCreditsText ends at x=322 ...
  TransportControlsView: BoostWarningText ends at x=530 ...
```

**Los dos suelos, y el segundo es el que importa:** el primero exige encontrar al menos 40 vistas por
reflexión; el segundo exige haber medido al menos **3000 controles ya maquetados** —hoy son **3543 en
48 vistas**—. Encontrar las vistas no prueba nada si sus árboles miden cero: una maqueta que no llegara
a ejecutarse dejaría todos los `Bounds` a cero, todos los controles saltados por la guarda, y la puerta
verde sobre una cuenta vacía. **Ésa es la forma en que esta prueba se habría quedado ciega**, y por eso
se cuenta lo medido y no lo encontrado. / **Two floors, and the second is the one that matters.**

Y una vista que no se pueda construir sola **se nombra**, no se salta: una vista que dejó de medirse en
silencio es una vista que no vigila nadie. Hoy las 48 se construyen. / A view that cannot be
constructed alone is named, not skipped.

## El verde / The green

```
UiTests  613/613
```
