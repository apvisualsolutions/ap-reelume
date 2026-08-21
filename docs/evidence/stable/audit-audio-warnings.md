# Lo que sale por los altavoces no es lo que se pidió, y se decía en la misma tinta que las etiquetas / What comes out of the speakers is not what was asked for, and it was said in the same ink as the labels

Sexto trabajo del tramo 4 de la §4, **y la puerta de desbordamiento cazó al vuelo la forma que este
repositorio ya ha medido ocho veces**. / §4's fourth tranche, and the overflow gate caught the shape
this repository has now measured eight times.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Los tres avisos / The three notices

`AudioOutputView` decía tres cosas en texto llano, del mismo color que las etiquetas de encima: **sin
dispositivo**, **mezcla degradada** y **dispositivo perdido a mitad de sesión**. Las tres significan lo
mismo en el fondo: **lo que se oye no es lo que se pidió**. Ninguna es un fallo —el sonido sigue o va a
seguir— y ninguna es un dato, porque en las tres **la elección de alguien no se cumplió**. / None is a
failure and none is a fact: in all three, somebody's choice was not honoured.

Pasan a `WarningSurfaceBrush` con `WarningBorderBrush` y el glifo `⚠`, la misma forma que el distintivo
de «no disponible» y que los dos avisos del estado de vídeo. **El glifo es lo que mantiene la señal
fuera del color a solas.** / The glyph is what keeps the signal off colour alone.

## Y lo que la puerta encontró en el mismo movimiento / And what the gate caught on the way

```
No_view_is_wider_than_the_narrowest_window_the_application_allows
  ShellView: TextBlock ends at x=921 in a 900-wide window
```

**Un `StackPanel` horizontal ofrece a sus hijos anchura infinita**, así que un `TextBlock` con
`TextWrapping="Wrap"` al lado de un glifo **no envuelve nunca**: crece hasta donde diga su texto y se
sale por el lado. Es la misma forma que este árbol lleva medida **ocho veces**, y sus dos víctimas aquí
eran los avisos que acababa de escribir — los de audio **y los del estado de vídeo del cambio
anterior**, que tenían el mismo defecto sin que nadie lo hubiera visto todavía. / A horizontal
StackPanel offers infinite width, so a wrapping TextBlock never wraps.

Los dos pasan a `Grid ColumnDefinitions="Auto,*"`, que es lo que da al texto una anchura finita contra
la que envolver. **La puerta funcionó exactamente para lo que existe**: encontró en segundos algo que
sólo se habría visto en una ventana estrecha con un mensaje largo. / The gate found in seconds what
would otherwise have needed a narrow window and a long message.

## El verde / The green

```
UiTests             635/635
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
```
