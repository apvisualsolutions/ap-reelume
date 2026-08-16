# Las dos salidas de la pantalla de recuperación / The two exits of the recovery screen

La regla de aislamiento cubría tres salidas —el registro, el navegador y los selectores de archivo—.
Faltaban las **dos de la pantalla que aparece cuando la base de datos no abre**: mostrar la carpeta
donde estaría la copia, y salir. Ninguna de las dos podía pulsarla nada que no fuera una persona. /
The isolation rule covered three exits. These are the two on the screen that appears when the
database will not open, and neither could be pressed by anything but a person.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Por qué estas dos estaban bloqueadas / Why these two were blocked

No es que la prueba fuera difícil: es que **la prueba se destruía a sí misma**. / The test was not
hard to write; it destroyed itself.

- **«Abrir la carpeta de copias»** hacía `Process.Start` con `UseShellExecute`, o sea abría una
  ventana del Explorador en la máquina que estuviera midiendo. / opened a real Explorer window on
  whichever machine was measuring.
- **«Salir»** llamaba a `desktop.Shutdown()`, o sea terminaba el proceso que estaba midiendo —cuando
  había un `IClassicDesktopStyleApplicationLifetime`, que bajo un arnés headless **no lo hay**, así
  que el botón no hacía nada que sondear. / ended the process doing the measuring — and under a
  headless harness there is no desktop lifetime at all, so the button did nothing to probe.

La segunda es la forma más difícil de este problema: un control que **no hace nada** bajo el arnés no
se distingue de uno que **está roto**. / A control that does nothing under the harness cannot be told
apart from one that is broken.

## La corrección / The correction

Un puerto, `ISystemHandoff`, con dos métodos y **un solo llamante**: la pantalla de recuperación
ofrece exactamente estas dos cosas que hacer. Cuál de las dos implementaciones se construye lo decide
la **raíz de datos resuelta**, una vez, en la composición — igual que el lanzador de enlaces y los
selectores de archivo. / One port with two methods and one caller; which implementation is built is
decided by the resolved data root, once, in the composition.

| Ejecución / Run | Carpeta / Folder | Salir / Leaving |
|---|---|---|
| Dueña del perfil / Owns the profile | Explorador, sin cambio / Explorer, unchanged | Apaga de verdad / Really shuts down |
| Raíz propia / Own root | Se anota / Written down | Se anota / Written down |

Lo anotado es una línea por entrega, en el orden en que se pidieron, con un verbo por delante:
`open-folder <ruta>` y `exit`. El verbo es lo que permite a una sonda distinguir «se ofreció la
carpeta» de «se pidió salir» sin analizar nada. / One line per handover, in order, with a verb in
front, so a probe can tell the two apart without parsing.

## Lo que la medición cambió del diseño / What the measurement changed

El diseño inicial ponía la resolución del ciclo de vida **dentro** de la salida de Windows, para que
la puerta de cobertura pudiera llegar a las dos ramas con un doble. El compilador lo desmintió: /
The first design put the lifetime lookup inside the Windows exit so a double could reach both
branches. The compiler said no:

```
error CS0535: 'LifetimeDouble' no implementa el miembro de interfaz
'IClassicDesktopStyleApplicationLifetime.(This interface or abstract class is -not- implementable by user code !)()'
```

Avalonia declara esa interfaz **no implementable por código de usuario**, con un miembro cuyo nombre
es el propio aviso. Así que no hay doble posible, y apagar uno de verdad dentro de una suite
terminaría la suite. / Avalonia declares the interface not implementable by user code, so no double
exists, and shutting a real one down inside a suite would end the suite.

**La decisión, y no se disfraza:** la búsqueda del ciclo de vida se queda en `CompositionRoot`, que
es donde ya vivían **dos copias literales de la misma expresión**, y lo que llega a
`WindowsSystemHandoff` es la llamada. Con eso la clase queda entera bajo prueba y la expresión que no
se puede probar en ningún sitio está en un solo sitio, en vez de en tres. / The lookup stays in the
composition, where two literal copies of it already lived; what reaches the exit is the call itself.

Nótese lo que **no** se hizo: no se cambió la interfaz para poder probarla, ni se movió ninguna
decisión de seguridad. Cuál es la salida se decide por la raíz, que es la misma regla que ya deciden
el registro de arranque y el navegador. / What was not done: no interface was changed to be testable
and no security decision moved.

## Lo que se comprueba, y en qué mitad / What is checked, and on which half

Las dos mitades de la elección viven en el **mismo archivo** (`IsolatedRunTests`), porque al fusionar
informes Cobertura se conserva el mejor por línea y no la unión: una elección de dos lados repartida
entre dos suites se lee a la mitad para siempre. / Both halves live in one file, because merged
Cobertura keeps the better reading per line rather than the union.

De la mitad aislada se **conduce el efecto**: se pide la carpeta y se pide salir, y se lee lo
anotado. De la mitad dueña del perfil se afirma **cuál se construyó**, antes de pedirle nada — pedirle
algo abriría una ventana en quien ejecute la suite y después terminaría el proceso. / The isolated
half is driven; of the owning half only which exit was built is asserted, because asking it anything
would open a window on whoever ran the suite and then end the process.

## Lo que sigue fuera, y se dice en vez de callarlo / What stays out, said rather than hidden

**Cerrar la ventana y salir por la bandeja siguen apagando directamente.** Son otro camino del
producto —con el guardado de la posición y la parada del trabajo de fondo alrededor— y no son de esta
tanda. Lo que sí se hizo es quitar la duplicación: la expresión de Avalonia vive ahora en un solo
método. Una ejecución aislada que llegue por ese camino **sí** apagaría, y eso está sin medir. / The
window's close and the tray's exit still shut down directly. They are another path with its own
saving around it, and not part of this batch; the duplicate expression is gone, but an isolated run
arriving that way would still shut down, and that is unmeasured.

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet test tests/ApSolutions.LocalMedia.PackagingTests -c Release -m:1 --settings eng/test.runsettings --logger trx --filter "FullyQualifiedName~SystemHandoff"
dotnet test tests/ApSolutions.LocalMedia.AccessibilityTests -c Release -m:1 --settings eng/test.runsettings --logger trx --filter "FullyQualifiedName~IsolatedRunTests"
```
