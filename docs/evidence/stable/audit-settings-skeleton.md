# El «mismo esqueleto» que la §4 describe no lo era, y una etiqueta sólo la oía un lector de pantalla / The "same skeleton" §4 describes was not one, and one label was heard by a screen reader only

Segundo trabajo del tramo 5 de la §4. La fila agrupa tres vistas bajo `Mismo esqueleto: título 28,
descripción, controles a 620 px, Padding SpaceXLarge`. **Ninguna de las tres lo tenía**, y midiéndolas
apareció algo que la fila no sabía. / §4 groups three views under "same skeleton"; none of the three
had it, and measuring them turned up something the row did not know.

**⚠ Corrección: la mitad del título de este trabajo se deshizo el mismo día.** Subir esos títulos a
`FontSizeTitle` (28) y nivel 1 fue leer la §4 vista por vista; **medido sobre el shell ensamblado**, las
siete vistas de ajustes están apiladas en un solo `ScrollViewer`, así que eso puso cuatro encabezados de
nivel 1 en una página y un escalón de 158 px por el medio. La superficie y la columna **sí** eran
correctas, y se extendieron a las siete. Ver [su auditoría](audit-settings-page-structure.md). /
Correction: the title half of this was undone the same day by the assembled measurement.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que era el «mismo esqueleto» / What the "same skeleton" was

Medido antes de escribir nada: / Measured before writing anything:

| | `ScanSettingsView` | `RecommendationSettingsView` | `SegmentDetectionSettingsView` | `AppearanceSettingsView` |
|---|---|---|---|---|
| Título H1 | `FontSizeSubtitle` (20) | `FontSizeSubtitle` (20) | `FontSizeSubtitle` (20) | **`FontSizeTitle` (28)** |
| `Padding` del contenedor | ninguno | ninguno | ninguno | **32** |
| Columna de los controles | ninguna | `640` en tres textos | ninguna | **`MaxWidth 620`** |
| Descripción | **ninguna** | sí | sí | sí |

`FontSizeTitle` **vale 28**, que es exactamente el número que la §4 pide, así que el token existía y
tres vistas de cuatro no lo gastaban. **Cuatro páginas de ajustes cuyo primer encabezado era más pequeño
que el de la quinta**: no se nota mirando una página, se nota recorriéndolas. / Four settings pages
whose first heading was smaller than the fifth's — invisible one page at a time, obvious walking through
them.

Y el `MaxWidth="640"` de tres bloques de texto de recomendaciones **se fue con el cambio**: un tope más
ancho que la columna en la que vive es un número que nadie puede gastar. / A cap wider than the column
it sits in is a number nobody can ever spend.

## El defecto que la fila no sabía / The defect the row did not know about

`ScanSettingsView` tenía **un título y dos controles y nada entre ellos**, y uno de esos controles era un
`NumericUpDown` con esto: / one of those controls was a spinner with this:

```xml
<NumericUpDown
    automation:AutomationProperties.Name="{DynamicResource ScanSettingsFallbackMinutes}"
    Minimum="1" Maximum="1440" Value="{Binding FallbackIntervalMinutes}" />
```

`ScanSettingsFallbackMinutes` dice «Intervalo de recuperación en minutos». **Un lector de pantalla lo
oía y quien mirase la pantalla veía una caja de números sin nada al lado.** La cadena existía en los dos
idiomas desde siempre; lo que faltaba era pintarla. / A screen reader heard it and anybody looking saw a
bare number box. The string had always existed; what was missing was painting it.

**Una casilla lleva sus palabras en su contenido; un `Slider` y un `NumericUpDown` no**, así que en
cuanto uno recibe nombre accesible y ninguna etiqueta visible, las palabras están escritas y no se las
enseña a nadie. Por eso la prueba **no es sobre ese control**: afirma que **todo** `Slider` y todo
`NumericUpDown` de estas páginas pinta las palabras que anuncia, porque el siguiente que se añada se
equivocaría igual. / The assertion is general, because the next one added would be wrong the same way.

Y la página gana la descripción que le faltaba —cadena nueva, en los dos idiomas—: **una página de
interruptores sin una frase que diga qué hace encenderlos es una página que alguien adivina**. / A page
of switches with no sentence saying what turning them on does is a page somebody guesses at.

## Lo que la prueba compara, y por qué no compara 28 / What the test compares, and why not 28

El tamaño se afirma contra **el token resuelto**, no contra el número: una prueba con su propia copia de
28 estaría de acuerdo consigo misma el día que la escala se moviera. Y la cuarta página entra en la
comparación **aunque ya estuviera bien**: si la escala cambia bajo las cuatro, esto lo dice en vez de
sujetar cuatro páginas a un valor que nadie eligió. / The resolved token and not the number, and the
page that was already right is included so a moved scale says so.

## El verde / The green

```
UiTests             667/667
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
ArchitectureTests   30/30
verify-docs.ps1     217 Markdown, 32 localizados / localised, 58 feature IDs, 46 MVP IDs
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```
