# El segundo tramo de la §4: Inicio, y el carril que se leía de la base y no se pintaba / §4's second tranche: Home, and the rail that was read from the database and never painted

Segunda fila de la §4 de `design/Propuesta de diseño`, y por tanto el segundo trabajo de la fase 6 del
paso 6. Cinco vistas, dos discrepancias con el documento y **un defecto de la casa en su sexta forma**
que sólo apareció al medir la fila contra el árbol. / Second row of §4, and therefore step 6's phase 6
second piece of work: five views, two discrepancies with the document, and one house defect.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que ya estaba, medido antes de tocar nada / What was already there

De las cinco filas, **dos ya se cumplían enteras**: el héroe se retira cuando no hay nada que reanudar
(`IsVisible="{Binding HasResume}"`, ausente y no hueco) y `LibraryEntryView` ya usa
`CornerRadiusMedium`. Ninguna de las dos tenía prueba, así que las dos estaban a una edición de
perderse. / Two of the five rows already held, and neither was watched by anything.

## Dos discrepancias entre la §4 y el árbol, y manda el árbol / Two discrepancies, and the tree wins

1. **No hay portadas en toda la aplicación.** La §4 pide «portada + progreso» en el héroe, «barra al
   pie de cada **portada**» en los carriles e «iniciales cuando no hay portada» en la ficha. Medido:
   **cero `<Image>` en los `.axaml` de `src/`**, y el único mapa de bits del árbol es el fotograma de
   vídeo de LibVLC. `PosterPath` se produce, se fusiona y se persiste en SQLite, y **ninguna vista lo
   lee**. Traerlo no es trabajo de una vista: una ruta de TMDB es remota, y descargarla sería una
   conexión que habría que declarar en `NetworkPurposeRegistry`. Queda anotado, no improvisado. /
   **The application paints no artwork at all**; bringing it in is not one view's work.
2. **`LibraryEntryView` no es la ficha que la §4 describe.** El documento la pinta como «ficha 2:3,
   título a dos líneas, año en `TextSecondaryBrush`, iniciales sin portada». En el árbol es el bloque
   de **entrada a la biblioteca**: recuento de películas y series y un botón «Abrir biblioteca». No
   tiene título de obra, no tiene año y no se repite. / It is the library entry block, not a title
   card.

## El defecto que la segunda discrepancia destapó / The defect the second discrepancy uncovered

**La §4 pedía una ficha con año, y el único modelo de Inicio que tiene año no lo pintaba nadie.**

`RecentlyAddedItemViewModel` existe con `Title`, `YearText`, `HasYear`, `IsAvailable` e `IsShow`;
`HomeViewModel` lo expone en `RecentlyAdded` con `HasRecentlyAdded`; `GetHome` pide **doce** por carga;
`HomeReadModel` los lee de SQLite ordenados por fecha de alta. Y `grep RecentlyAdded` sobre los
`.axaml` de `src/` devuelve **nada**. Es la **sexta forma** del defecto de la casa —producido en todas
partes, consumido en ninguna— con la agravante de que **tres capas de pruebas pasaban en verde**:
`GetHomeTests` afirma que el caso de uso los trae, `EpisodeSequenceRepositoryTests` que SQLite los
ordena, y `HomeLayoutTests.The_library_summary_counts_titles_and_names_unavailable_ones` **siembra dos
y afirma sobre su año**. Una prueba que afirma sobre lo que el modelo produce no dice nada sobre si
alguien lo pinta. / Produced in three layers, asserted by three suites, painted by nobody.

## El rojo / The red

```
Home_paints_the_recently_added_titles_it_already_reads
  Assert.Contains() Failure: Item not found in collection
  Collection: ["Inicio", "Tu biblioteca", "1", "películas", "·", ···]
  Not found:  "Arrival"

The_in_progress_card_ends_with_a_three_pixel_bar_in_the_accent
  Expected: 3   Actual: 4

Switched_off_is_not_the_same_as_empty_and_the_rail_says_which
  RecommendationsOffDescription is not declared, so nothing can paint it.
```

## La corrección, vista por vista / The fix, view by view

1. **`RecommendationsRailView` distingue sus tres estados.** Con el ajuste apagado, la vista pintaba
   `RecommendationsEmpty` —«No hay nada que sugerir ahora mismo»— porque `GetRecommendations` devuelve
   `[]` **antes de leer nada**. Era una afirmación sobre un catálogo que nadie había mirado, y es justo
   la que el interruptor existe para no hacer. Ahora el vacío se enlaza a `IsEmpty` (encendido y sin
   resultados) y el apagado tiene su propia frase, `RecommendationsOffDescription`, que dice la
   consecuencia real: nada se calcula y el catálogo no se lee. / Off and empty were the same sentence,
   and one of them was false.
2. **`InProgressRailView`: la barra al pie, 3 px, acento sobre `ControlFillBrush`.** Medida antes: el
   `ProgressBar` del tema base mide **4 px** y `Height` sola no basta, porque su `ControlTheme` fija un
   `MinHeight` propio. Y lo que la prueba lee no son las propiedades del control sino **los `Border`
   que pintan**, porque un setter que llega y no pinta es el defecto de la casa con cara de estilo.
   El porcentaje en palabras se queda encima: la barra nunca es el único portador. / Measured from the
   elements that paint, not from the control's properties.
3. **`HomeView`: `Space12` → `Space24`**, que es el `SpaceLarge` de la §4 traducido por valor. Inicio
   es una pila de bloques independientes y al paso corto los carriles se leían como parte de lo que
   tenían encima. La línea base estructural se regeneró: **`LibraryEntryBottom` 284 → 308 en las 36
   combinaciones**, y `LibraryEntryWithinFirstViewport` sigue siendo cierto en todas. / The approved
   baseline moved by exactly the extra spacing, and the library entry still fits the first viewport.
4. **`RecentlyAddedRailView`, nueva.** Un carril con la forma de los otros dos: encabezado, cadena de
   vacío, `ListBox` horizontal virtualizado. La ficha lleva el **título a dos líneas** (`MaxLines` con
   `TextWrapping`, que se necesitan mutuamente) y el **año en `TextSecondaryBrush`**. Sin portada, por
   la primera discrepancia. / A rail shaped like the other two, minus the artwork that does not exist.
5. **`LibraryEntryView` no cambia, y su razón sí.** La tabla cerrada de `LeadingActionTests` decía que
   no lidera por ser «una fila o ficha que se repite», que es **falso**: es un bloque único con un solo
   botón. La decisión era correcta por otra razón — Inicio ya acentúa Continuar, y la §4 pide **un solo
   acento sólido por pantalla**—. Escrita la razón verdadera, y añadida la prueba que la tabla no puede
   hacer: **la pantalla ensamblada tiene exactamente un `primary-action`**, y ninguno cuando no hay
   nada que reanudar. / The decision was right and the reason written beside it was false.

## Las cuatro cadenas nuevas, en los dos idiomas / The four new strings, in both languages

| Clave / Key | Español | English |
| --- | --- | --- |
| `RecommendationsOffDescription` | Nada se calcula mientras está apagado: el catálogo no se lee para esto. | Nothing is computed while it is off: the catalogue is not read for this. |
| `HomeRecentlyAddedAccessibleName` | Añadido recientemente | Recently added |
| `HomeRecentlyAddedHeading` | Añadido recientemente | Recently added |
| `HomeRecentlyAddedEmpty` | Nada nuevo desde el último escaneo. | Nothing new since the last scan. |

Ninguna estaba en `design/Cadenas nuevas`: sus 47 son de Biblioteca, Revisión, Reproductor, Ajustes,
Copias y Restauración, y **ni una del área Inicio**. La primera se aprueba contra la regla del propio
paquete —dice en voz alta algo que el código ya garantiza, y lo garantiza
`A_disabled_rail_never_asks_the_read_model_for_anything`—; las otras tres son el encabezado, el nombre
accesible y el vacío de una superficie nueva. / None of the four came from the package's 47, which
carry nothing for Home.

## El verde / The green

```
UiTests             622/622
AccessibilityTests  135/135 en las dos pasadas / on both passes
IntegrationTests    456/457 (1 omitida por diseño / 1 skipped by design)
DocumentationTests   87/87
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 134 declaraciones en 133 identidades; 133 pulsadas, 0 pendientes
```

**El trinquete del paseo no se movió, y estaba previsto:** el inventario cuenta `Button`, `CheckBox`,
`ComboBox`, `Slider`, `ToggleButton`, `RadioButton` y `ToggleSwitch`, y un carril de sólo lectura no
declara ninguno — igual que los otros dos, cuyas fichas tampoco se pulsan. La regla de la casa se
cumple por vacío, no por excepción. / The ratchet did not move because a read-only rail declares no
command control, exactly like the two rails beside it.

## Lo que este tramo deja anotado / What this tranche leaves written down

- **Las portadas siguen sin existir**, y con ellas las iniciales, la proporción 2:3 y el relleno de
  «cargando» que la §4 pide en tres filas. No es una vista: es una decisión sobre de dónde sale la
  imagen. / Artwork remains undone and is not one view's work.
- **`docs/design/SURFACES`** pasa a **50 vistas** (Inicio, de 5 a 6), **480 claves por idioma**, **24
  listas con datos** y **cinco** con cadena de vacío escrita. / The inventory moved with the tree.
