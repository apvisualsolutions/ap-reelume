# La portada elegida vive aparte de la del proveedor / The Picked Cover Lives Apart From the Provider's

- IDs: `LIB-021`, `LIB-018`, `ENG-003`
- Fecha / Date: 2026-09-18
- Alcance / Scope: `EditableMetadata`, `CoverOrderPolicy`, `ResolveTitlePoster`, migración / migration 24, `CatalogMetadataRepository`, `CatalogRepository`, `UpdateMetadata`, `MetadataEditorViewModel`, `LibraryViewModel`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Qué se cerró

Es el primero de los tres planes de `LIB-021`
(`docs/superpowers/plans/2026-09-18-lib-021-cover-origins-storage.md`), y cierra el defecto que la
`ADR-0009` describe. Hasta hoy un solo campo, `poster_path`, guardaba dos cosas: la dirección que
mandaba el proveedor y la portada que alguien elegía de su disco. Por eso:

- **elegir una portada pisaba la del proveedor**, y el editor ponía el candado del campo para que el
  siguiente refresco no la devolviera;
- **«Restaurar campos del proveedor» quita todos los candados**, así que borraba la elección y dejaba
  su archivo huérfano en `personal-artwork`, sin nada que lo nombrara, dentro de cada copia de
  seguridad.

### El cambio

- `EditableMetadata.PersonalCover` guarda sólo el nombre del archivo elegido (64 hexadecimales y una
  extensión aprobada). **`MetadataMergePolicy` no lo asigna nunca**, y ése es el arreglo: un refresco
  no tiene campo por el que llegar a él.
- La migración 24 añade `catalog_metadata.personal_cover`. Las filas guardadas a la antigua se
  trasladan **al leer**, con `PersonalCoverPathPolicy`, que es la única regla que sabe cómo es el
  nombre de una portada elegida; escribir otra copia de esa regla en SQL habría sido tener dos.
- `CoverOrderPolicy.Default` fija el orden: la elegida gana y la del proveedor la sigue.
  `ResolveTitlePoster` recorre ese orden, así que si el archivo elegido desaparece se dibuja la del
  proveedor en vez de una tarjeta vacía.
- El editor ya no escribe en `PosterPath` ni pone su candado: rellena su propio campo y guarda.
- La cuadrícula y las fichas entregan los dos campos, y cuál se dibuja lo decide el orden.

### Las pruebas, y cuáles se vieron fallar

| Prueba | Qué afirma | Control |
| --- | --- | --- |
| `MetadataMergePolicyTests.A_refresh_never_touches_the_hand_picked_cover_even_with_nothing_locked` | la fusión no toca el campo, ni sin candados | roja con `PersonalCover = null` en la fusión |
| `MetadataEditingTests.Restoring_the_provider_fields_keeps_the_hand_picked_cover` | **el defecto de la ADR, reproducido**: restaurar deja la elegida | roja con el mismo mutante |
| `ResolveTitlePosterTests` (tres nuevas) | la elegida gana; si falta, dibuja la del proveedor; un valor que no es nombre de portada no se lee nunca como ruta | — |
| `CatalogMetadataRepositoryTests` (tres nuevas) | sobrevive a un póster nuevo; lo guardado a la antigua se lee como elegida; **una dirección del proveedor nunca se mueve** | rojas antes de la columna |
| `MetadataEditorTests.An_imported_cover_is_saved_apart_and_leaves_the_provider_poster_alone` | elegir rellena su campo, no toca el póster ni su candado, y guardar lo escribe | — |
| `LibraryPosterLookupTests` | la cuadrícula entrega los dos campos | roja pasando `null` como portada propia |
| la escena del editor en `AssembledPhysicalWalkTests` | con la aplicación entera: elegir, guardar, ver la tarjeta y restaurar sin perder la elección | roja con `UpdateMetadata` sin llevar el campo |

**Dos pruebas afirmaban el defecto** y se reescribieron diciéndolo: la del editor se llamaba «una
portada importada llega al campo del póster y lo bloquea», y la escena del paseo afirmaba que la ruta
elegida quedaba en el campo del proveedor con el candado puesto. Las dos eran correctas para el diseño
de un solo campo, y eran justo lo que había que cambiar.

**Y una afirmación de la escena no medía nada.** Su primera versión comprobaba tras restaurar el campo
del editor, y siguió verde quitando la recarga del editor, porque la propiedad conservaba lo que había
puesto el selector. Ahora lee la fila guardada. Ese mutante resultó no tener efecto visible —ninguna
pantalla muestra todavía la portada elegida, y guardar nunca la borra—, así que la escena se validó con
el que sí lo tiene: un `UpdateMetadata` que no lleva el campo, y la tarjeta se queda sin portada.

### Lo que queda

Los planes 2 y 3 de `LIB-021`: el fotograma como tercer origen, y el ajuste general del orden con su
excepción por título en la galería del prototipo.

---

## English

### What was closed

It is the first of `LIB-021`'s three plans
(`docs/superpowers/plans/2026-09-18-lib-021-cover-origins-storage.md`), and it closes the defect
`ADR-0009` describes. Until today one field, `poster_path`, held two things: the address the provider
sent and the cover somebody picked from their disk. So:

- **picking a cover overwrote the provider's**, and the editor set the field's lock so the next refresh
  would not bring it back;
- **«Restore provider fields» clears every lock**, so it erased the choice and left its file orphaned
  in `personal-artwork`, with nothing naming it, inside every backup.

### The change

- `EditableMetadata.PersonalCover` holds only the picked file's name (64 hex digits and an approved
  extension). **`MetadataMergePolicy` never assigns it**, and that is the fix: a refresh has no field to
  reach it through.
- Migration 24 adds `catalog_metadata.personal_cover`. Rows stored the old way are moved **on read**,
  with `PersonalCoverPathPolicy`, the one rule that knows what a picked cover's name looks like; writing
  another copy of it in SQL would have meant two.
- `CoverOrderPolicy.Default` fixes the order: the picked one wins and the provider's follows.
  `ResolveTitlePoster` walks that order, so if the picked file goes missing the provider's is drawn
  rather than an empty card.
- The editor no longer writes `PosterPath` nor sets its lock: it fills its own field and saves.
- The grid and the detail cards hand over both fields, and which one draws is the order's call.

### The tests, and which ones were seen failing

| Test | What it asserts | Control |
| --- | --- | --- |
| `MetadataMergePolicyTests.A_refresh_never_touches_the_hand_picked_cover_even_with_nothing_locked` | the merge never touches the field, not even unlocked | red with `PersonalCover = null` in the merge |
| `MetadataEditingTests.Restoring_the_provider_fields_keeps_the_hand_picked_cover` | **the ADR's defect, reproduced**: restoring keeps the picked one | red with the same mutant |
| `ResolveTitlePosterTests` (three new) | the picked one wins; if missing, the provider's draws; a value that is not a cover name is never read as a path | — |
| `CatalogMetadataRepositoryTests` (three new) | survives a new poster; stored the old way reads as picked; **a provider address is never moved** | red before the column |
| `MetadataEditorTests.An_imported_cover_is_saved_apart_and_leaves_the_provider_poster_alone` | picking fills its field, leaves the poster and its lock, and saving writes it | — |
| `LibraryPosterLookupTests` | the grid hands over both fields | red passing `null` as the picked cover |
| the editor scene in `AssembledPhysicalWalkTests` | with the whole application: pick, save, see the card, and restore without losing the choice | red with `UpdateMetadata` not carrying the field |

**Two tests asserted the defect** and were rewritten saying so: the editor's was called «an imported
cover reaches the poster field and locks it», and the walk scene asserted that the picked path ended up
in the provider's field with the lock set. Both were right for the one-field design, and were exactly
what had to change.

**And one of the scene's assertions measured nothing.** Its first version checked the editor's field
after restoring, and stayed green with the editor's reload removed, because the property still held
what the picker had put there. It now reads the stored row. That mutant turned out to have no visible
effect — no screen shows the picked cover yet, and saving never erases it — so the scene was validated
with one that does: an `UpdateMetadata` that does not carry the field, and the card is left without a
cover.

### What remains

`LIB-021`'s plans 2 and 3: the frame as the third origin, and the general setting for the order with
its per-title override in the prototype's gallery.
