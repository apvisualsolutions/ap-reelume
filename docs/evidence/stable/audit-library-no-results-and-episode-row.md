# Buscar y no encontrar nada ya se dice, y una fila de episodio mide lo mismo que la de al lado / Finding nothing now says so, and an episode row measures the same as the one beside it

Tercer trabajo del tramo 3 de la §4: el estado que el documento marca como «hoy no existe» y la fila
de episodio. / §4's third tranche continues with the state the document marks as missing.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El hueco que ya estaba medido y nadie había cerrado / The gap already measured

`docs/design/SURFACES` lo dice desde el 2026-08-18 y lo llama «el que nadie ve venir»: **el vacío de
la biblioteca lo pinta `ShellView`, no `LibraryView`**, así que una búsqueda que no encontraba nada
**no mostraba ningún texto**. Y el texto de biblioteca vacía habría dicho algo falso: la biblioteca no
está vacía, es la búsqueda la que no encuentra. / The empty-library sentence would have been false
anyway.

```
Searching_and_finding_nothing_says_so_without_claiming_the_library_is_empty
  LibrarySearchNoResultsTitle is not declared, so nothing can paint it.
```

**Las dos cadenas vienen del paquete tal cual** (`design/Cadenas nuevas`, primera sección), que es
donde tenían que estar. Son las **primeras dos de sus 22** que se gastan. / The first two of the
package's 22 empty-state strings to be spent.

**La distinción se afirma con tres casos, y el tercero es el que la mantiene honesta:** con búsqueda y
sin resultados, se dice; con búsqueda y resultados, no; **y sin búsqueda ni filtro y sin resultados,
tampoco** — porque ése es el vacío de la biblioteca y lo dice el shell. Decirlo dos veces sería dar dos
respuestas a una pregunta. / The third case is what keeps it honest.

## La fila de episodio / The episode row

La §4 pide **56 px** y el número **monoespaciado alineado a la derecha «para que la columna cuadre»**.

```
An_episode_row_is_56_px_tall_and_its_numbers_end_on_the_same_pixel
  Expected: 56   Actual: 36
```

**Lo que se afirma es que la columna cuadra, no cómo se llama la fuente**: el episodio 9 y el 10
terminan en la **misma x**. Es el fin, y la familia es el medio — una fuente proporcional en una
columna fija y alineada a la derecha cumpliría el propósito de la fila, y una monoespaciada en una
columna suelta no. Lo que hace cuadrar la columna es el **ancho fijo con alineación**; la familia sólo
mantiene parejos los dígitos entre sí. / What is asserted is the end, not the means.

Y el alto fijo tiene su razón escrita al lado: una temporada es una columna de estas filas, y **filas
de alturas distintas se leen como una lista que todavía está cargando**. / Rows of different heights
read as a list that is still loading.

## El verde / The green

```
UiTests             627/627
IntegrationTests    456/457 (1 omitida por diseño / 1 skipped by design)
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 134 declaraciones en 133 identidades; 133 pulsadas, 0 pendientes
```

**El paseo no se movió y era previsible**: ninguna de las dos piezas declara un control de mando. Lo
que sí lo moverá es lo que queda del tramo —el botón de borrar la búsqueda y el selector de temporada—,
y cada uno llega con su escena en el mismo cambio. / Neither piece declares a command control; the two
that remain in this tranche do.
