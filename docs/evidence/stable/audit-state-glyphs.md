# El círculo de estado leía al 64 % del glifo con el que tenía que hacer juego / The state circle was reading at 64% of the glyph it had to match

Sexto trabajo del tramo 6 de la §4, **y el que lo cierra**. La fila pide que `○ ◐ ●` se queden y ganen
«el mismo tamaño óptico que los glifos Fluent», y la medición dice por qué. / §4's sixth tranche and
the piece that closes it: the row asks that the circles stay and gain the same optical size as the
Fluent glyphs, and the measurement says why.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La medición / The measurement

A `FontSizeBody`, con la tinta real de cada uno: / At `FontSizeBody`, measuring the ink of each:

```
glifo Fluent (U+E768)  tamaño 14  ->  14,0 × 14,0
●                      tamaño 14  ->   9,0 × 19,0
●                      tamaño 16  ->  10,0 × 22,0
●                      tamaño 18  ->  11,0 × 24,0
●                      tamaño 20  ->  13,0 × 27,0
●                      tamaño 22  ->  14,0 × 30,0
```

**Una fuente de iconos llena su caja em**: el glifo Fluent mide 14 de ancho a tamaño 14. El círculo mide
**9** — un **64 %**—, que es exactamente lo que hace que parezca un carácter suelto en vez de un estado.
/ An icon font fills its em box; the circle was reading at 64% of it.

`FontSizeSubtitle` lo lleva a **13, o el 93 %**, y es un escalón que la escala ya tiene. Entre 14 y 20
no hay nada en ella, y **un escalar declarado sólo para esto sería el token con un lector que este
repositorio rechaza** — así que se queda el escalón más cercano, y el 7 % que falta es una diferencia
que nadie puede ver entre dos pantallas distintas. / A scalar declared for this alone would be the
token-with-one-reader this repository refuses.

El ancho exacto se alcanzaría a 22, pero ahí la caja de línea sube a **30 sobre una fila de 19**: la §4
pide un tamaño óptico y no un ancho idéntico, y pagar once píxeles de alto en cada fila por el 7 % que
queda no es lo que pedía. / Matching the width exactly costs eleven pixels of line height per row.

## Trece, no tres / Thirteen, not three

La fila nombra dos vistas, pero **trece bloques de texto pintan uno de los tres círculos**: los tres del
control de visto, los cinco destinos del carril de navegación y las cinco píldoras de apariencia. Toman
**una sola clase**. Tres de trece a un tamaño y diez a otro es exactamente la inconsistencia que esta
tanda lleva encontrando al mirar las pantallas ensambladas. / Thirteen text blocks paint one of the
three, in three files; they take one class.

La prueba **afirma primero el recuento** —al menos trece con la clase— porque el carril y las píldoras
pintan el suyo por enlace: sin contexto de datos son cadenas vacías, y una prueba que sólo buscara el
literal encontraría tres de trece y lo daría por hecho. / The count is asserted first, because ten of
the thirteen paint theirs through a binding.

## ⚠ Y una carrera del motor real, medida y NO tapada / And a race in the real engine, measured and not papered over

En la primera pasada de `AccessibilityTests` cayó una escena del paseo: / One walk scene failed on the
first pass:

```
A_session_that_will_not_open_is_handed_over_and_retried_with_the_mouse
  the two-byte file opened, so there was no failure to recover from —
  idle=False opening=False playing=False stopped=True failed=False
```

**Repetida sola: verde. La suite entera repetida: 135/135.** No la toca este cambio —que son tamaños de
fuente en `TextBlock`— y la causa está en el motor: con un archivo de dos bytes, LibVLC unas veces
**falla** al abrir y otras **para**, según lo que decida el sondeo de demultiplexores. La escena espera
`failed`. / Re-run alone: green. The whole suite re-run: 135/135. This change does not touch it.

**No se ensancha la espera para aceptar `stopped`**, que es lo que la haría pasar siempre: la escena
existe para comprobar la pantalla de recuperación, y una espera que acepta «paró» pasaría sin que esa
pantalla apareciera nunca. **Una prueba se vuelve ciega antes que falsa**, y ésta prefiere fallar de vez
en cuando a no mirar. Queda anotada como roja conocida que no es del código: **CI corre el paseo dos
veces**, así que puede aparecer allí. / The wait is not widened to accept "stopped": that would pass
without the recovery screen ever appearing.

## El verde / The green

```
UiTests             689/689
AccessibilityTests  135/135 en la repetición / on the re-run
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```

**Con esto el tramo 6 cierra**: siete vistas, y de las cuatro cosas que la §4 pedía y no se hicieron,
las cuatro tienen su número — la portada de 92 px (no hay portadas en 0.2.0), el estado vacío de los
duplicados y el de la lista de hosts (inalcanzables), y los 13 px del volcado (la escala no los tiene).
/ The tranche closes, and the four things refused each have their number.
