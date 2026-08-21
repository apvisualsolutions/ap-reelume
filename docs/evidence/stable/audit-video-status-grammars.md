# «HDR10 está pasando tal cual» se pintaba igual que «esto cayó a software» / "HDR10 is passing through" was painted exactly like "this fell back to software"

Quinto trabajo del tramo 4 de la §4. / §4's fourth tranche, fifth piece.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Qué estaba mal / What was wrong

`VideoStatusOverlay` decía **seis cosas con un solo pincel**. Cuatro de ellas son **datos** sobre un
vídeo que se está reproduciendo perfectamente —por qué camino salió la imagen, si la GPU está
decodificando— y dos son **avisos**: lo que suena o se ve **no es exactamente lo que se pidió**. Con la
misma caja para las seis, «HDR10 está pasando tal cual» tenía el mismo aspecto que «esto cayó a
software». / Six lines, one brush, two completely different kinds of thing.

**Y ninguna de las seis es un fallo.** Esa superficie es la de `PlayerView`, y dice otra cosa: que no
hay imagen. Un aviso aquí convive con una imagen que se ve. / None of the six is a failure; that
surface belongs to PlayerView.

## Lo aplicado / What was applied

- **Los cuatro datos** pasan a texto de leyenda en `TextSecondaryBrush`: se pueden leer, no piden
  nada.
- **Los dos avisos** ganan su propia caja dentro del distintivo: `WarningSurfaceBrush`,
  `WarningBorderBrush` y el glifo `⚠`. Es el **tercer** par de los seis pinceles de gramática que se
  gasta.
- El modelo gana `HasDecodeFacts` y `HasDecodeWarnings`, que es lo que hace la división expresable —
  **y las dos entran en la lista de `Apply`**, porque una propiedad derivada que no se anuncia deja el
  enlace clavado en lo que valía al construirse. Eso fue el rojo intermedio de este cambio: el modelo
  decía la verdad y la caja no aparecía. / A derived property that never announces leaves the binding
  on whatever it was at construction.

**Lo que se afirma es que la caja de aviso está AUSENTE mientras sólo hay datos.** Una prueba que sólo
buscara la caja cuando hay un aviso aprobaría un distintivo que la dibujara siempre. / The assertion is
the absence, not the presence.

## Una discrepancia con la §4, decidida y escrita / A discrepancy, decided and written down

**La §4 pide los datos SIN CAJA NINGUNA, y conservan la del distintivo.** La razón es dónde vive esto:
**el distintivo flota sobre el vídeo**, así que un texto sin superficie debajo se lee contra un
fotograma arbitrario y **no hay contraste que nadie pueda garantizar** — ni medir, porque el fondo es
la película. La jerarquía que la fila busca se mantiene igual: un dato es más callado que un aviso. /
Text with no surface under it is read against an arbitrary frame; the hierarchy is kept without making
a line some films render unreadable.

## El verde / The green

```
UiTests             634/634
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
```

Y una prueba vecina se corrigió de paso: indexaba los `TextBlock` **por nombre**, y el glifo del aviso
no tiene, así que la clave nula la habría hecho lanzar. / A neighbouring test keyed TextBlocks by name,
and the new glyph has none.
