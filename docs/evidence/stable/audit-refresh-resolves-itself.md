# El refresco se resuelve solo / The refresh resolves itself

Segundo commit de los cuatro de
[la auditoría del 2026-08-14](audit-identification-never-reaches-the-catalogue.md), sobre el primero
([audit-apply-identification.md](audit-apply-identification.md)). / Second of the four commits, on
top of the first.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Los rojos, y fueron tres / The reds, and there were three

**Uno venía escrito** desde la sesión anterior: el editor **que construye la aplicación** no puede
refrescar. Todas las demás pruebas del refresco rellenaban `ProviderMetadata` a mano, y esa asignación
era la **única** del repositorio entero. / The editor the application builds cannot refresh; every
other test filled the property by hand, and that assignment was the only one in the repository.

```
MetadataEditorTests.The_editor_the_application_builds_can_refresh_from_the_provider [FAIL]
  Assert.NotEqual() Failure: Values are equal
```

**El segundo salió midiendo el primero.** `CompositionRoot.OpenMetadataEditorAsync` dice en su
comentario que la fila de un título se crea «en la primera edición». No se creaba: `UpdateMetadata`
salía por `NotFound`, y el editor traduce ese resultado a **nada** —ni conflicto ni cambio—, así que
Guardar en un título que nadie había editado era un botón que no hacía nada. / The row was not
created on first edit, so Save on a fresh title did nothing.

```
Saving_a_title_with_no_row_yet [FAIL]
  Expected: Applied
  Actual:   NotFound
```

**El tercero es el que nadie buscaba, y es el peor.** Al escribir la nueva forma del refresco apareció
que ningún llamante subía la revisión: pasaban la fila leída tal cual, y `TrySaveAsync` guardaba
`catalog.Revision`, que era la misma que acababa de leer. Medido contra SQLite real: / Measured
against real SQLite:

```
Two_windows_editing_from_the_same_copy_cannot_both_win [FAIL]
  Assert.Equal() Failure: Values differ
  Expected: 2
  Actual:   1
```

Es decir: **el control optimista comparaba contra un número que nunca se movía**. Dos ventanas
editando el mismo título podían ganar las dos, y la segunda se llevaba por delante la primera sin que
nada avisara. La comprobación existía, la cláusula `WHERE catalog_metadata.revision = $expected`
estaba escrita, y no protegía nada. / The optimistic check was comparing against a number that never
moved.

**Ninguna prueba unitaria podía verlo**, porque los dobles en memoria hacían
`Revision = expectedRevision + 1` — subían la revisión que el repositorio real no subía. Es la misma
lección que la auditoría dejó anotada, en su tercera aparición esta semana: **si el doble hace lo que
producción no hace, la suite miente**. / No unit test could see it: the doubles raised the revision
the real repository did not.

## La corrección / The fix

- **`RefreshMetadata` resuelve** por lo que la fila guarda. Ya no recibe un `MetadataDetails` que
  nadie le daba: lee `provider` y `provider_key`, pide al proveedor que interprete su propia clave y
  se dirige a él. `RefreshMetadataCommand` pierde `ProviderMetadata` y el editor pierde la propiedad
  entera, que era una entrada que alguien tenía que acordarse de rellenar — la clase de defecto, no
  su instancia.
- **El proveedor lee sus propias claves.** `IMetadataProvider.TryCreateReference` devuelve la
  referencia que una clave guardada representa, o nada si no es suya. El tipo de contenido vive
  dentro del formato de la clave (`movie:` / `tv:`), así que ni el esquema ni el caso de uso tienen
  que saberlo. De paso, ese par prefijo→tipo queda en **un solo sitio** dentro del adaptador; antes
  estaba escrito dos veces en el mismo archivo.
- **La revisión la fija quien escribe.** `TrySaveAsync` guarda `expectedRevision + 1` y devuelve la
  fila con esa revisión, en vez de confiar en que cada llamante se acuerde. Un control que sólo
  funciona si todos sus llamantes hacen lo mismo es un control que ya ha fallado.
- **La primera edición crea la fila**, que es lo que su propio comentario llevaba diciendo desde
  siempre.
- **El editor dice lo que pasa.** Un título sin identificar y un proveedor sin respuesta son estados
  distintos, y ninguno es un fallo: `MetadataWriteOutcome` gana `NotIdentified` y `Unavailable`, la
  vista gana sus dos mensajes en los dos idiomas, y la prueba
  `Refresh_without_provider_metadata_is_a_safe_no_op` **cambia de sentido** — describía como guarda
  deliberada el estado en el que la aplicación construida vivía siempre; ahora dice que un título
  **sin identificar** no refresca nada.

## Lo que queda verde / What is green

| Suite | Resultado |
| --- | --- |
| `Application.Tests` | 214 / 214 |
| `IntegrationTests` | 435 / 436 (1 omitida) |
| `UiTests` | 437 / 437 |
| `ArchitectureTests` | 26 / 26 |
| `AccessibilityTests` | 79 / 79 |
