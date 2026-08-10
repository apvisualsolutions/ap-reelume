# ARQ-006 pasos 2-3: el registro en módulos / ARQ-006 steps 2-3: the registration in modules

Evidencia de partir la composición por áreas y de extraer las dos clases que el plan nombra. El
comportamiento no cambia; lo que cambia es que ahora se puede leer. / Evidence for splitting the
composition by area and extracting the two classes the plan names. Behaviour does not change; what
changes is that it can now be read.

Rama / Branch: `codex/ap-reelume-mvp-x64`, 2026-08-10.

## Lo que se midió / What was measured

| | Antes / Before | Después / After |
|---|---|---|
| `CompositionRoot.cs` | 1.857 líneas / lines | 1.503 |
| `AddLocalMedia` (la cadena / the chain) | 311 líneas / lines | 11 |
| Archivos de composición / Composition files | 1 | 7 (63–117 líneas cada parcial / lines per partial) |
| Pruebas sobre `FindLatestBackup` | 0 (inalcanzable / unreachable) | 5 |

## Los módulos / The modules

`AddLocalMedia` es ahora una lista de once líneas que se lee como un índice: `AddData`,
`AddPlayback`, `AddPersonalisation`, `AddLibrary`, `AddSettingsAndBackup`, `AddUpdates`,
`AddAppearanceAndLifecycle`, `AddIdentification`, `AddCatalogEditing`, y el ensamblado de la carcasa.
/ reads as an index.

Se usa `partial class` en vez de clases separadas por una razón concreta: los registros llaman a una
docena de ayudantes privados —`CreateLibraryViewModel`, `DescribeThisMachine`, `ReadResource`,
`CreateUpdateClient`, `ChooseArchiveSourceAsync`— y sacarlos a clases propias habría obligado a
hacerlos públicos o a pasarlos como delegados, es decir, a ampliar la superficie pública del anfitrión
para conseguir un efecto de lectura. Los parciales dan el mismo efecto sin pagar ese precio. / Partial
classes give the same readability without widening the host's public surface.

## Las dos clases extraídas / The two extracted classes

`DatabaseStartup` (`Startup/`) y `WindowsFilePickers` (`Shell/`) salieron del archivo con sus
comentarios intactos. La primera **llegó con las pruebas que su lógica nunca tuvo**: `FindLatestBackup`
elige qué copia se te ofrece cuando la base de datos no abre, y mientras fue un método privado dentro
de la composición la única forma de ejercitarla era hacer fallar una base de datos real. Dos de las
cinco pruebas cubren casos que nadie había comprobado: una ruta de migración que nombra un archivo que
no está en el disco (se descarta en vez de prometer una restauración imposible) y la copia de **otra**
base de datos en la misma carpeta (no se ofrece). / The first arrived with the tests its logic never
had.

`WindowLifecycle` **no** se extrae. `ConfigureWindow` está tejido con el arranque que ARQ-001 va a
mover de todas formas; sacarlo ahora significaría moverlo dos veces. Queda dicho aquí para que la
omisión sea una decisión y no un olvido. / `WindowLifecycle` is deliberately left for ARQ-001.

## La deuda que la partición destapó / The debt the split exposed

Ocho pruebas de cableado se pusieron rojas sin que cambiara un solo cable. Abrían
`CompositionRoot.cs` por su nombre, de modo que «la composición» significaba en realidad «un archivo»:
en cuanto los registros se repartieron, las aserciones dejaron de encontrar el texto que buscaban.

El arreglo no es aflojar la aserción sino corregir de qué habla: `CompositionSourceText` lee **todos**
los `CompositionRoot*.cs` y las diecisiete llamadas pasan por ahí. `ServiceConsumptionTests` —la
puerta contra el defecto de la casa, «registrado y nunca alimentado»— recibió el mismo tratamiento, y
ahí importaba más: leyendo un solo archivo el grafo se habría encogido en silencio y la puerta habría
seguido verde mientras dejaba pasar exactamente lo que existe para cazar. / The fix is not to loosen
the assertion but to correct what it talks about.

**Verificado con una mutación**, porque una prueba que pasa no demuestra que mire: cambiar
`PersistenceTrigger.Seek` por `PersistenceTrigger.Interval` en el módulo de reproducción pone en rojo
`ProgressWiringTests.Seeking_flushes_the_position`, y restaurarlo la devuelve a verde. Las aserciones
conservan sus dientes tras la partición. / Verified by mutation, because a passing test does not prove
it looks at anything.

## Puertas / Gates

| Puerta / Gate | Resultado / Result |
|---|---|
| `dotnet format --verify-no-changes --severity warn` | 0 |
| `dotnet build -c Release -warnaserror` | 0 advertencias, 0 errores / 0 warnings, 0 errors |
| UiTests | 378/378 |
| ArchitectureTests | 18/18 |
| IntegrationTests | 381/382 (1 omitida / 1 skipped) |
