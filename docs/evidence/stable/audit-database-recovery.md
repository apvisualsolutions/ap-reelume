# La única pantalla que existe porque algo se rompió lo decía en color de aviso amable / The one screen that exists because something broke said so in the gentle colour

Primer trabajo del tramo 7 de la §4. / §4's seventh tranche, first piece.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El acento donde tocaba el peligro / The accent where danger belonged

El detalle del fallo se pintaba sobre `AccentSubtleBrush`, que es la superficie que este árbol usa para
«aquí hay algo que considerar» — el consentimiento de arranque, el aviso de refuerzo de volumen, el
bloque de estado de privacidad. **Y ésta es la única pantalla de la aplicación que existe porque algo se
rompió**: si se ve, la base de datos no abrió y el programa no arrancó. El detalle no es una nota, es la
razón. Pasa a `DangerSurfaceBrush` con `DangerBorderBrush`. / This is the one screen in the application
that exists because something broke; the detail is not a note, it is the reason.

La prueba afirma además que **los dos pinceles son distintos**, porque comparar contra el que debe ser
sin comprobar que no es el que era aprueba igual si alguien los iguala. / The two brushes are asserted
different, because comparing against the right one without checking it differs from the old one passes
either way.

## Las dos rutas, y la medición que desactiva la preocupación de la §4 / The two paths, and the measurement that defuses §4's worry

Alguien lee esas rutas **para ir a buscar su copia a mano**, así que van en `FontFamilyMono` — cuarto
consumidor de la familia. La §4 pide `TextWrapping=WrapWithOverflow` «dentro de los 720 px» y llama a
esta pantalla **el peor caso de la geometría**, con rutas UNC largas.

Medido el 2026-08-22 con una ruta UNC de 105 caracteres en la columna de 720, dentro de una ventana de
900: / Measured with a 105-character UNC path in the 720 column, in a 900-wide window:

```
Wrap              bloque 686 × 33   borde derecho x=776
WrapWithOverflow  bloque 686 × 33   borde derecho x=776
NoWrap            bloque 720 × 17   borde derecho x=810
```

**Los dos primeros son idénticos**, porque una barra invertida **sí** es un punto de corte: la ruta no
se queda como una sola palabra impartible y no se desborda. La preocupación de la §4 no se materializa
aquí. Se usa `WrapWithOverflow`, que es lo que la fila pide, y **queda escrito que dan lo mismo** para
que nadie lo «arregle» de vuelta creyendo que cambia algo. / The first two are identical, because a
backslash is a break opportunity; written down so nobody reverses it believing it matters.

Y la prueba mide **dónde acaba el borde derecho en una ventana de 900**, que es el mínimo que la
aplicación permite, con la ruta larga de verdad y no con una corta. Un caso peor que no se alimenta con
el peor dato no es el caso peor. / A worst case not fed the worst data is not the worst case.

## Y dos de forma / And two of shape

Los dos botones pasan a `WrapPanel`, y el título gana **nivel 1**: no tenía ninguno, y no es una sección
de nada — es su propia ventana, y `NEXT-SESSION` registra que el shell **deliberadamente no tiene ruta
hasta ella**. / Level one, because it is its own window and the shell deliberately has no route to it.

## El verde / The green

```
UiTests             692/692
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```
