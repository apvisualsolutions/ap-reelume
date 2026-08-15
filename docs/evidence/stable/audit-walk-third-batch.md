# La tercera tanda, y un renombrado que no puede renombrar / The third batch, and a rename that cannot rename

Tercera tanda del paseo: editor de metadatos y renombrado. El editor quedó entero; el renombrado
**no**, y la razón es la que importa. / The editor is complete; the rename is not, and the reason is
the point.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 22 | **30** |
| Pendientes / Pending | 106 | **98** |

Ocho de los once de la tanda. Los tres que faltan son los del renombrado, y no están pendientes por
falta de tiempo. / Eight of the batch's eleven.

## El editor, entero / The editor, complete

Los **seis candados** restantes —título original, sinopsis, año, géneros, cartel, fondo—, más
**Guardar** y **Restaurar**. Cada candado se pulsa y se lee de vuelta: son los campos que una persona
protege del siguiente refresco, así que un candado que parece puesto y no lo está devolvería los datos
de otro sobre su trabajo. / Each lock pressed and read back.

**Guardar se afirma sobre la base, no sobre la pantalla.** Es el primer control del paseo cuyo efecto
no está en la superficie, y afirmar sobre el editor sólo demostraría que el editor conserva lo que se
tecleó en él — cosa que haría igual si no se escribiera nada. Por eso `PressAsync` acepta ahora una
sonda asíncrona. / Save asserts on the row, which is why the probe can be asynchronous now.

## El renombrado: la pieza que falta no es cableado / The rename: what is missing is not wiring

La aplicación ensamblada pide renombrar cada archivo **al nombre que ya tiene**: / The assembled
application asks to rename each file **to the name it already has**:

```csharp
new RenameRequest(file.Path, Path.GetFileName(file.Path))
```

`RenamePolicy` contesta a eso **correctamente**: `source == destination` es
`RenameConflictKind.NoChange` y no genera operación. Así que el plan sale **siempre vacío**, Renombrar
y Deshacer no pueden hacer nada nunca, y la casilla de consentimiento guarda una decisión que no se
ofrece. Medido abriendo la vista sobre un título identificado: cero operaciones, un conflicto
`NoChange`, y `ExecuteCommand.CanExecute` en falso. / The plan is always empty and the consent box
guards a decision that is never offered.

**Y no hay nada que componga un nombre.** El único `RenameRequest` fuera de las pruebas en todo el
repositorio es ése. No falta un cable: falta **una decisión** —cómo se llama un archivo renombrado—,
y ésa es del propietario del producto. El paseo la registra en vez de inventarse un convenio. / There
is no naming component at all. What is missing is a decision, and it is not the walk's to invent.

Los tres controles siguen en `eng/walk-pending.txt` nombrando esto, y la medición queda como escena:
`The_rename_preview_can_only_ever_offer_the_name_the_file_already_has`. Una escena que afirma lo que
hoy ocurre vale más que una que no se puede escribir. / The measurement is kept as a scene.

## Lo que esto es / What this is

El cuarto defecto que encuentra el paseo, y de la misma familia que los tres del reproductor:
**registrado, alcanzable y nunca alimentado**. `RenamePolicy` está nombrada en la guía del proyecto
como una de las decisiones de seguridad del dominio; lo que nadie había comprobado es que la
superficie que la conduce se alimenta de su propia salida. / The fourth defect the walk has found, and
the same family as the three in the player.
