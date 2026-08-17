# La cuarta salida: el paquete que se entrega a Windows / The fourth exit: the package handed to Windows

Instalar una actualización es entregarle un paquete a Windows con `Process.Start`. Bajo un arnés eso
arranca un instalador **de verdad** en la máquina que está midiendo, así que era la salida que
faltaba. / Installing an update means handing a package to Windows; under a harness that starts a
real installer on the machine doing the measuring.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que no hizo falta / What was not needed

**Ni una clase nueva, ni una interfaz nueva.** `WindowsUpdateLauncher` ya recibía la entrega como un
`Func<string, bool>`, y su propio comentario decía por qué: «este repositorio las guarda en la raíz de
composición — el mismo sitio que ya abre la carpeta de copias». Ese comentario **dejó de ser cierto**
con la tanda 7b, que sacó la carpeta de copias a `ISystemHandoff`; lo que faltaba era llevarse el
paquete al mismo sitio. / No new class and no new interface: the launcher already took the handover
as a delegate, and its own comment pointed at where the backup folder used to be — which 7b had just
moved.

Así que `ISystemHandoff` gana `TryOpenPackage`, la composición se lo pasa al lanzador, y **la elección
ya estaba hecha**: quien decide cuál de las dos entregas se construye es la raíz de datos resuelta, una
vez, como con las otras cuatro. / So the port gains one method, the composition hands it to the
launcher, and the choice was already made by the resolved data root.

`OpenWithWindows` quedó sin llamantes, y eso **lo dijo el compilador**, no un `grep`: se retiró y la
solución compiló. / `OpenWithWindows` was left with no callers, and the compiler said so — it was
removed and the solution built.

## La asimetría que sí importa / The asymmetry that matters

Abrir una carpeta y abrir un paquete son la misma llamada al shell y **no el mismo resultado**:

| | Proceso nulo / Null process |
|---|---|
| Carpeta / Folder | **Éxito.** La carpeta cae en una ventana del Explorador ya abierta / Success: it lands in an Explorer window already open |
| Paquete / Package | **Refusal.** Nada tomó el paquete / Refusal: nothing took it |

Y no es teoría: en un Windows limpio sin nada registrado para `.msix`, la llamada **devuelve nulo y no
lanza**. Tratar eso como éxito diría que empieza una instalación mientras no empieza nada. Está
archivado en [updater-handover.json](updater-handover.json), y ahora vive donde se decide en vez de en
un comentario. / Measured on a clean Windows: the call returns null and throws nothing, so treating it
as success would report an installation starting while nothing had.

## Lo que anota una ejecución aislada / What an isolated run writes down

Un verbo propio, `open-package`, en el mismo registro que los otros dos. Un verbo por entrega es lo
que permite a una sonda distinguir «se enseñó una carpeta» de «se entregó el paquete» sin analizar
nada. / A verb of its own in the same record, which is what lets a probe tell the handovers apart
without parsing.

Y se comprueba **a través del lanzador que la aplicación construye de verdad**, no llamando al método
directamente: lo que tiene que quedar aislado es que arranque un instalador, no que alguien se acuerde
de llamar al sitio correcto. / Checked through the launcher the application really builds, because
what has to be isolated is the installer starting.

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet test tests/ApSolutions.LocalMedia.PackagingTests -c Release -m:1 --settings eng/test.runsettings --logger trx --filter "FullyQualifiedName~SystemHandoff"
dotnet test tests/ApSolutions.LocalMedia.AccessibilityTests -c Release -m:1 --settings eng/test.runsettings --logger trx --filter "FullyQualifiedName~IsolatedRunTests"
```
