# La sinopsis llega a las fichas / The synopsis reaches the cards

Evidencia de **LIB-013**: el resumen de una película o una serie se podía editar y no se podía leer
en ninguna parte. / Evidence for **LIB-013**: the synopsis of a film or a series could be edited and
read nowhere.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-14.

## La medición previa / The measurement first

Antes de decidir la forma, qué había ya. Medido sobre `src/`: / What was already there, measured
before deciding the shape:

```
Domain          MetadataDetails.Overview, MetadataField.Overview, MetadataMergePolicy    existe
Infrastructure  columna `overview` en SQLite; TmdbMetadataProvider la lee                existe
Presentation    MetadataEditorView la muestra y la bloquea, cadenas ES/EN                existe
Presentation    MovieDetailsView / ShowDetailsView                                       NO la pintan
Application     CatalogItem —lo que las fichas reciben—                                  NO la lleva
```

El dato llevaba guardándose desde el principio; lo que no existía era el **camino de lectura**. Una
persona podía escribir un resumen en el editor y no encontrarlo en ningún sitio después, que es la
forma que toma aquí el defecto característico de esta casa: construido, probado y sin alcanzar. /
The value had always been stored; the read path was what did not exist.

## El rojo / The red

Tres aserciones que **sí se podían medir antes de que la API existiera**, y por eso son las que
abrieron: el marcado de las dos fichas y el cableado del cargador. / Three assertions that could be
measured before the API existed:

```
Both_cards_bind_the_synopsis_and_announce_it(view: "Movie/MovieDetailsView.axaml")  [FAIL]
Both_cards_bind_the_synopsis_and_announce_it(view: "Show/ShowDetailsView.axaml")    [FAIL]
The_details_loader_hands_the_card_the_stored_synopsis                               [FAIL]
```

Las de modelo de vista llegaron con la corrección, porque una prueba que no compila no es un rojo
archivado sino un archivo a medias — y se dice aquí en vez de presentarlas como si hubieran fallado.
/ The view-model assertions arrived with the fix, because a test that does not compile is not an
archived red, and saying so beats presenting them as if they had failed.

## La corrección / The fix

El cargador de detalles pide la metadata guardada a `ICatalogMetadataRepository` —que ya se resolvía
en ese mismo sitio— y se la entrega a las dos fichas por su nombre (`overview:`). Los modelos de
vista **no consultan nada**: reciben, igual que reciben el estado de visionado y las versiones.
`CatalogItem` no se tocó a propósito: es la proyección de la consulta del catálogo, y cargar el
resumen de cada fila de la biblioteca sería pagar por lo que sólo la ficha muestra. / The details
loader asks the repository and hands it over by name; the view models query nothing, and
`CatalogItem` was deliberately left alone.

Tres decisiones pequeñas que no son de estilo: / Three small decisions that are not style:

- **En blanco es ausente.** `HasOverview` exige contenido, no `null`. Un bloque con nombre accesible
  y sin texto le anuncia a un lector de pantalla un párrafo vacío, que se lee como defecto y no como
  ausencia.
- **Se envuelve y se acota** (`TextWrapping`, `MaxHeight`, elipsis). Un resumen es un párrafo, y una
  ficha que crece sin tope empuja las versiones o las temporadas fuera de la pantalla en el único
  título cuyo proveedor se enrolló.
- **Reutiliza la cadena bilingüe que ya existía** (`MetadataOverviewLabel`, «Resumen» / «Overview»)
  como nombre accesible, en vez de añadir un par nuevo que mantener en dos idiomas.

**Ninguna conexión nueva**: `NetworkPurposeRegistry` no cambia, porque el dato ya estaba en el disco.
/ No new connection: the registry is untouched, because the value was already on disk.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `SynopsisTests` | 8 de 8 / of 8 |
| `ApSolutions.LocalMedia.UiTests` | 418 de 418 / of 418 (410 + 8) |
| `ApSolutions.LocalMedia.AccessibilityTests` | 79 de 79 / of 79 |
| `eng/verify.ps1` completo / full | verde / green |
