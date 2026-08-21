# Una serie se ve por temporadas, y la cuadrícula de la §4 pierde contra la virtualización / A series is seen a season at a time, and §4's grid loses to virtualisation

Cierre del tramo 3 de la §4: el selector de temporada, y la única fila del tramo que **no se hace**,
con el número que lo decide. / §4's third tranche closes: the season picker, and the one row that is
not done, with the number that decided it.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El selector de temporada / The season picker

Hasta ahora `ShowDetailsView` **apilaba todas las temporadas** en un `ItemsControl`: una serie de ocho
temporadas era una página cuyo final nadie alcanzaba, y las siete temporadas por encima de la que se
está viendo eran peso muerto en cada visita. / Every season stacked is a page nobody reaches the end of.

- **Con una sola temporada el selector es AUSENTE, no deshabilitado.** Un control cuyo único
  contestable es lo que ya dice es una pregunta que nadie hizo, y esta ficha no tiene sitio que gastar
  en una. Es la misma gramática que `PrivacySettingsView` dibuja para su interruptor de conexión. /
  Absent, not disabled.
- **Y lo que la prueba afirma es que elegir otra temporada cambia los episodios dibujados**, no que el
  desplegable esté ahí: una prueba que sólo mirara su presencia aprobaría un selector que no selecciona
  nada. / Choosing another season has to change which episodes are drawn.
- **Su escena de paseo siembra dos temporadas a propósito.** Con una sola el control es ausente, así
  que una escena que lo pulsara contra una serie de una temporada estaría pulsando algo que la
  aplicación **deliberadamente no dibuja**. El sembrado gana un parámetro opcional para que las demás
  escenas sigan viendo la serie de una temporada que ya tenían. / The seed gains an optional second
  season so the other scenes keep the state they were written against.

```
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
```

## La cuadrícula fluida, que no se hace / The fluid grid, which is not done

La §4 pide para `LibraryView` una «cuadrícula de anchura fluida con mínimo de 180 px por ficha». **Se
midió antes de escribirla**, y el resultado la descarta:

| Panel | Tiempo de montaje | Fichas materializadas |
| --- | --- | --- |
| `VirtualizingStackPanel` (hoy) | **285 ms** | **22** |
| `WrapPanel` | **2079 ms** | **10 003** |

Diez mil entradas en una ventana de 1280×800, **con una fila de texto plano y no con la ficha real**, y
en una máquina rápida. Son **7× el tiempo y 455× los controles vivos**. / Seven times the time and 455
times the live controls.

**Y no hay tercera opción en la plataforma.** Enumerando los tipos de `Avalonia.Controls` 12.1.1:
existen `WrapPanel` y `UniformGrid`, y **no existen `ItemsRepeater` ni `UniformGridLayout`**. Lo que
reflowa no virtualiza y lo que virtualiza no reflowa. / Listing the assembly's types: what reflows does
not virtualise and what virtualises does not reflow.

**El `README` promete diez mil archivos sin bloquear la interfaz**, así que la cuadrícula pierde. La
salida existe y está escrita para cuando se decida pagarla: **agrupar las fichas en filas en el modelo
de vista** y dejar que el panel virtualice filas — un control de verdad, no una línea de marcado.

**Y hay una razón para no pagarla todavía, que es la otra discrepancia de este rediseño:** una
cuadrícula de fichas **sin portadas** es una rejilla de cajas de texto, que no es mejor que una lista.
**La cuadrícula y las portadas son la misma tarea**, y ninguna de las dos es de una vista. / A grid of
cards with no artwork in them is a grid of text boxes.

**La garantía se mudó en vez de perderse**: hay prueba de que la biblioteca materializa menos de 200
filas de diez mil, con los números de arriba escritos dentro, para que quien lea «¿por qué no es una
cuadrícula?» encuentre la respuesta en el sitio donde se cambiaría. / The guarantee moved rather than
being lost.

## El verde / The green

```
UiTests             629/629
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
```
