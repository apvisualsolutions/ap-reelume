# La identificación no llega al catálogo / Identification never reaches the catalogue

Hallazgo del 2026-08-14, medido al preparar `LIB-016`. No es una entrada de la cola: es lo que la
primera medición de esa entrada destapó, y es su prerrequisito. / Found on 2026-08-14 while measuring
for `LIB-016`. It is not a queue entry: it is what that entry's first measurement uncovered, and it
is its prerequisite.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Qué se buscaba / What was being looked for

El plan de `LIB-016` pide, antes de escribir el trabajo de fondo, **cuántas fichas de una biblioteca
identificada pasarían de 90 días**. Para responder eso hacen falta dos datos por título: cuándo se
refrescó por última vez, y a qué referencia del proveedor corresponde. / The plan asks how many
titles would be older than ninety days, which needs two things per title: when it was last refreshed,
and which provider reference it belongs to.

**Ninguno de los dos existe**, y buscándolos apareció el motivo. / Neither exists, and looking for
them showed why.

## La cadena, medida eslabón a eslabón / The chain, measured link by link

```
escaneo / scan ─────────────► titles                (nombre de archivo parseado / parsed file name)
                └───────────► match_candidates      (candidatos de TMDB / TMDB candidates)

aceptar / accept ───────────► review_state = Accepted
                └───────────► ReviewInboxChanged ──► (nadie lo consume / consumed by nobody)

catalog_metadata ◄────────── UpdateMetadata      (el editor, al guardar / the editor, on save)
                 ◄────────── RefreshMetadata     (nadie lo alimenta / fed by nobody)
```

Cada flecha está medida, no leída: / Every arrow was measured, not read:

- **`catalog_metadata` se escribe desde dos sitios y los dos son el editor manual.** Búsqueda de
  `TrySaveAsync` en `src/`: `UpdateMetadata` y `RefreshMetadata`, nada más.
- **`RefreshMetadata` no lo alimenta nadie.** Su entrada es `MetadataEditorViewModel.ProviderMetadata`,
  y la **única** asignación de esa propiedad en todo el repositorio está **en una prueba**
  (`MetadataEditorTests.cs:116`). `CompositionRoot.OpenMetadataEditorAsync` construye el editor con la
  fila del catálogo, dos casos de uso y el selector de arte, y nunca la rellena. Así que los botones
  «Actualizar desde el proveedor» y «Restaurar del proveedor» están visibles, habilitados, y no pueden
  hacer nada: `RefreshAsync` sale por su primera guarda.
- **`ResolveMatch` no escribe metadata.** Marca `review_state` y publica `ReviewInboxChanged`, que no
  consume nadie en `src/`.
- **`ReviewState.Automatic` sólo se calcula.** `ConfidencePolicy` lo devuelve y `IdentifyMediaFile` lo
  usa para decidir si hace falta ir a la red. Nada lo aplica.
- **Ni `catalog_metadata` ni `titles` guardan la referencia del proveedor.** Sólo existe como
  `match_candidates.stable_key`, indexada por archivo y **sin el nombre del proveedor**: la clave
  viaja, `"tmdb"` no.

## Qué significa / What it means

Una identificación —automática con ≥90 % de confianza, o aceptada a mano en la bandeja— **no cambia
nada de lo que el catálogo muestra**. La biblioteca enseña lo que el analizador de nombres sacó del
archivo. / An identification changes nothing the catalogue shows.

Y por tanto: / And therefore:

- La sinopsis que `LIB-013` hizo visible sólo aparece si **una persona la escribe a mano** en el
  editor. El camino de lectura que faltaba se construyó sobre un camino de escritura que tampoco
  estaba.
- La clave de tráiler de `LIB-015` **nunca se rellenará sola**, por lo mismo. Cada capa de esa entrada
  —migración, proveedor, política, lanzador, interfaz— es correcta y está probada de punta a punta;
  lo que no existe es quien escriba el dato que las alimenta.
- `LIB-016` no se puede construir encima: un refresco automático necesita exactamente los dos datos
  que faltan y el eslabón que falta.

Esto **toca el estado de la matriz**: `LIB-006` (identificación híbrida y metadatos TMDB) y `LIB-007`
(umbrales y bandeja de revisión) figuran como `VERIFIED`, y sus evidencias son ciertas sobre lo que
midieron —el proveedor responde, la caché funciona, los umbrales son exactos, la bandeja persiste la
corrección—. Ninguna midió que el resultado llegara al catálogo. / This touches the matrix: both
features are recorded as verified, and every one of those measurements is true about what it measured.
None of them measured that the result reaches the catalogue.

## El rojo / The red

Construyendo el editor **como lo construye la aplicación** —sin que la prueba rellene nada— y pidiendo
el refresco: / Building the editor the way the application builds it and asking for the refresh:

```
MetadataEditorTests.The_editor_the_application_builds_can_refresh_from_the_provider [FAIL]
  Assert.NotEqual() Failure: Values are equal
```

La prueba no viaja en este commit: un rojo se archiva aquí, no se confirma en el árbol. Llega con su
corrección. / The test does not travel in this commit: a red is archived here, not committed.

La suite estaba verde porque **el doble rellenaba exactamente el hueco que hay en producción**, y
había una prueba —`Refresh_without_provider_metadata_is_a_safe_no_op`— que describía como decisión
segura el estado en el que la aplicación construida vive siempre. / The suite was green because the
double filled exactly the hole production has.

## La forma decidida / The shape decided

Orden intocable, el mismo criterio que `LIB-015`: la base primero, y la interfaz al final. / The same
order: the database first, the interface last.

1. **Migración `0018`**: `catalog_metadata` gana el proveedor, su clave y `refreshed_utc`. Nulos,
   porque las filas que ya existen no los tienen y eso no es un error: es una ficha que nadie
   identificó.
2. **Quien escribe**: una identificación aceptada —automática o de la bandeja— guarda la metadata del
   proveedor con su referencia y su fecha. Es el eslabón que falta y es el trabajo de verdad.
3. **`RefreshMetadata` resuelve** por la referencia guardada en vez de recibir un `MetadataDetails`
   que nadie le da; sin referencia guardada, refrescar no es un fallo, es una ficha sin identificar.
4. **El editor pierde `ProviderMetadata`**: una entrada que alguien tiene que acordarse de rellenar es
   la clase de defecto, no su instancia. Desaparece la propiedad y desaparece la clase entera.
5. **El paseo ensamblado alcanza el editor.** Hoy no llega, y por eso esto sobrevivió: las pruebas del
   editor construyen su modelo de vista a mano. `AssembledPhysicalWalkTests` ya conduce la aplicación
   real con `Window.KeyPress`; le faltan los clics —`Avalonia.Headless` los ofrece en 12.1.1 y nadie
   los usa— y le falta esta superficie.
