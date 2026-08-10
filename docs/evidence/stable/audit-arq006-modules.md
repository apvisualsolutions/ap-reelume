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

## Lo que se extrajo, y lo que la puerta de cobertura devolvió / What was extracted, and what the coverage gate sent back

`DatabaseStartup` (`Startup/`) se queda con **una sola función**: `FindLatestBackup`, que elige qué
copia se te ofrece cuando la base de datos no abre. **Llegó con las pruebas que su lógica nunca
tuvo**, porque mientras fue un método privado dentro de la composición la única forma de ejercitarla
era hacer fallar una base de datos real. Dos de las cinco cubren casos que nadie había comprobado: una
ruta de migración que nombra un archivo que no está en el disco (se descarta en vez de prometer una
restauración imposible) y la copia de **otra** base de datos en la misma carpeta (no se ofrece). / It
arrived with the tests its logic never had.

**`WindowsFilePickers` se intentó y se devolvió.** Los dos diálogos salieron a su propia clase, y la
puerta de cobertura los midió al **0 %**: no se pueden ejercitar sin una ventana real, y al reescribir
sus firmas dejaron de ser código movido para ser código nuevo. La puerta tenía razón y no había nada
que corregirle, así que los pickers volvieron a `CompositionRoot.cs`, de donde no debieron salir sin
poder probarse. Lo mismo con `CreateRecoveryView` y `HandleRecoveryAction`, que necesitan ventana y
`Process.Start`. La regla que queda escrita: **se extrae lo que se puede sostener con pruebas.** / The
gate measured them at 0 % and they went back; the rule that stands is that a class is extracted when
its tests can follow it.

`WindowLifecycle` **no** se extrae. `ConfigureWindow` está tejido con el arranque que ARQ-001 va a
mover de todas formas; sacarlo ahora significaría moverlo dos veces. Queda dicho aquí para que la
omisión sea una decisión y no un olvido. / `WindowLifecycle` is deliberately left for ARQ-001.

## La puerta de cobertura confundía ruta nueva con código nuevo / The gate confused a new path with new code

Partir el archivo hizo fallar `check-coverage.ps1`: decide qué es «nuevo» con
`git diff --diff-filter=A`, es decir, **por ruta**, y un módulo lleno de líneas que llevan meses
publicadas aparecía como código recién escrito al 46 % de cobertura. Sostenerlo habría significado que
la puerta empuja contra la limpieza que existe para hacer segura.

Ahora «nuevo» se decide por contenido: se construye el corpus de líneas de código del `BaseRef` —sin
comentarios ni `using`, que no llevan cobertura— y un archivo cuyo código ya existía en un 85 % es un
movimiento, **anunciado en la salida** archivo por archivo en vez de exento en silencio. No se
debilita nada: para colar código sin cubrir por esa vía habría que haberlo escrito antes en el árbol
base, donde esta misma puerta lo habría retenido. El 85 % en vez de un 90 % más redondo es porque el
andamiaje inevitable de una partición —la declaración de clase parcial y una firma por módulo— es
texto genuinamente nuevo y pesa más cuanto más pequeño es el módulo. / "New" is now decided by
content, announced rather than silent.

Que la puerta siguió mordiendo después del arreglo lo demuestra ella misma: con la nueva regla dejó
pasar los módulos movidos y **retuvo** `WindowsFilePickers` y `DatabaseStartup`, que era exactamente
donde había código nuevo sin cubrir. / The gate proved it still bites by holding the two files that
genuinely carried new uncovered code.

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
