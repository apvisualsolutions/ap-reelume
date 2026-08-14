# La identificación llega al catálogo / The identification reaches the catalogue

Primer commit de los cuatro que
[la auditoría del 2026-08-14](audit-identification-never-reaches-the-catalogue.md) dejó decididos.
Cierra el eslabón que faltaba: quien convierte una identificación en metadata guardada. / First of
the four commits that audit left decided: the link that turns an identification into stored metadata.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La primera medición, que era la condición para escribir nada / The first measurement

El plan exigía medir, antes de una sola línea, **cómo se llega del `media_file_id` que llevan los
candidatos al `title_id` que lleva `catalog_metadata`**, y advertía que si ese puente no existía era
parte de esta entrada «y probablemente de otra migración». / The plan required measuring the bridge
first, and warned that if it did not exist it would cost another migration.

**El puente existe, es la identidad del GUID, y no cuesta ninguna migración.** Medido en tres sitios
independientes: / The bridge exists, it is the GUID identity, and it costs no migration. Measured in
three independent places:

1. **La proyección del catálogo.** `CatalogRepository` compone lo que la biblioteca enseña como la
   unión de dos orígenes, y el segundo usa el identificador del archivo **como** identificador de
   título: / The catalogue projection uses the file id **as** the title id:

   ```sql
   SELECT t.id, ... FROM titles t
   UNION ALL
   SELECT scanned.media_file_id, 2, ... FROM scanned_titles scanned
   ```

2. **La otra mitad de esa unión está vacía en la aplicación construida.** `titles` sólo tiene un
   escritor, `ICatalogRepository.UpsertTitleAsync`, y **no lo llama nadie en `src/`**: sus dos
   apariciones son su declaración y su implementación, y sus únicos llamantes están en cuatro
   archivos de pruebas de integración. Así que **todo** título que el catálogo proyecta es un archivo
   escaneado. / `titles` has exactly one writer and no caller outside integration tests, so every
   title the catalogue projects is a scanned file.
3. **La composición ya cruzaba el puente, en las dos direcciones.** `OpenRenameAsync` busca el
   archivo con `new MediaFileId(titleId.Value)`; `OpenPlayerAsync` y `GroupScannedVersions` componen
   la clave de contenido con `new TitleId(mediaFileId.Value)`. No es una convención nueva: es la que
   ya sostiene renombrar y reanudar. / The composition root already crossed it in both directions.

**Lo que sí faltaba, y el plan no lo había previsto: el nombre del proveedor.** Un candidato guarda
su clave (`match_candidates.stable_key`) y su tipo (`content_kind`), pero **no de quién es la clave**
—la auditoría ya lo había anotado—, y `TmdbMetadataProvider.GetDetailsAsync` **lanza** si la
referencia que recibe no lleva el suyo. El literal `"tmdb"` estaba además escrito **dos veces**, en
el proveedor y en `MetadataCandidateSource`, donde compone la clave de caché: dos copias de la
cadena son dos ocasiones de escribir filas que nunca podrían refrescarse. Ahora el puerto lo expone
—`IMetadataProvider.Name`— y hay una sola copia. / What was missing was the provider's own name: a
candidate carries the key but not whose key it is, the provider throws on a reference that is not
its own, and the literal was written twice.

**La migración `0018` bastó.** `provider`, `provider_key` y `refreshed_utc` son exactamente lo que
hace falta; el tipo de contenido **no** se guarda a propósito, porque vive dentro del formato de
clave del proveedor (`movie:` / `tv:`) y reconstruirlo es trabajo suyo, no del esquema. / The
eighteenth migration was enough, and the content kind is deliberately not stored: it belongs to the
provider's key format.

## El rojo / The red

Aceptando un candidato por la ruta de la bandeja y preguntando por la fila que el catálogo lee: /
Accepting a candidate the way the inbox does, and asking for the row the catalogue reads:

```
ApplyIdentificationTests.Accepting_a_candidate_writes_the_identified_metadata_to_the_catalogue [FAIL]
  Assert.Single() Failure: The collection was empty
```

Vacía: `ResolveMatch` marcaba la revisión, publicaba su evento y no escribía nada que nadie pudiera
ver. / Empty.

## La corrección / The fix

- **`ApplyIdentification`** (`Application/Identification`): recibe el archivo, la clave del proveedor
  y el tipo; pide los detalles; los fusiona con `MetadataMergePolicy` sobre lo que la fila ya tuviera
  —de modo que **lo que una persona bloqueó sigue ganando**— y guarda con la referencia y la fecha.
  Escribe por el repositorio y no por `UpdateMetadata` porque ese exige que la fila ya exista.
- **Sin respuesta del proveedor no pasa nada, y no es un fallo.** Sin token el proveedor sirve sólo
  lo que tenga cacheado, así que una biblioteca que nadie ha consultado se queda como la dejó el
  analizador de nombres. El resultado lo dice con un valor propio (`Unavailable`) en vez de fingir
  éxito. / No answer is not a failure, and it says so.
- **Los dos llamantes, y el segundo no existía.** La bandeja, desde `ResolveMatch`; y el camino
  automático, desde `IdentifyScannedFiles`: un candidato que el puntuador coloca en
  `ReviewState.Automatic` —el umbral de 0,90— se aplica sin preguntar, que es **la mitad de
  `LIB-007` que se calculaba y se tiraba**. Un fallo del proveedor ahí se cuenta donde se cuentan los
  demás, porque un escaneo de mil archivos no puede perder su identificación por uno; en la bandeja
  **no** se captura, porque quien pulsa aceptar merece enterarse.

## Lo que queda verde / What is green

| Suite | Resultado |
| --- | --- |
| `Application.Tests` | 212 / 212 |
| `IntegrationTests` | 434 / 435 (1 omitida) |
| `UiTests` | 435 / 435 |
| `ArchitectureTests` | 26 / 26 |
| `AccessibilityTests` | 79 / 79 |

La prueba que cierra la medición no es unitaria: en `ScanIdentificationTests`, sobre SQLite real y
archivos reales, la ficha de «Dune» queda guardada **bajo el identificador de su propio archivo**,
con la sinopsis, el año, los géneros, el proveedor y la fecha; y la ficha que no llegó al umbral
sigue sin fila y sigue en la bandeja. / The measurement closes on real SQLite: the confident match is
stored under its own file's id, and the doubtful one has no row and stays in the inbox.

## Un segundo defecto, medido de paso y no corregido aquí / A second defect, measured in passing

`CompositionRoot.OpenMetadataEditorAsync` dice, en su propio comentario, que la fila de un título se
crea «en la primera edición». **No se crea**: `UpdateMetadata` sale por `NotFound` cuando no hay fila,
y el editor traduce ese resultado a **nada** —ni conflicto ni cambio—, así que guardar en un título
que nadie ha editado es un botón que no hace nada. Medido, no leído: / Measured, not read:

```
Saving_a_title_with_no_row_yet [FAIL]
  Assert.Equal() Failure: Values differ
  Expected: Applied
  Actual:   NotFound
```

No viaja en este commit —un rojo se archiva, no se confirma en el árbol— y se corrige en el
siguiente, que ya toca el editor. / It travels with the next commit, which already touches the editor.
