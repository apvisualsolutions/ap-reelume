# El paquete que llega y se confirma / The package that arrives and is confirmed

Descargar e instalar, pulsados con el ratón, sin que salga un byte a la red ni arranque un instalador
en la máquina que mide. / Download and install, pressed with the mouse, without a byte reaching the
network or an installer starting on the machine doing the measuring.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 92 | **94** |
| Pendientes / Pending | 36 | **34** |

```
The walk: 129 declared command controls in 128 identities; 94 pressed, 34 pending.
```

## Lo que se sustituye es el transporte, y sólo el transporte / Only the transport is replaced

`VerifiedUpdateDownloader` **hace el trabajo en los dos lados**. Lo que cambia para una ejecución
aislada es de dónde vienen los bytes y qué anfitriones admite su lista — nada más. Por eso siguen
siendo reales: / The verified downloader does the work on both sides; what changes is where the bytes
come from:

- **el hash**, comprobado contra el que declaró el manifiesto;
- **el tamaño**, que es lo que distingue una descarga cortada de una completa;
- **el `.partial`**, porque un archivo con el nombre del paquete en la carpeta de preparación
  significa una cosa y sólo una: que ha sido probado.

Un descargador escrito para el arnés habría demostrado que **el descargador del arnés** funciona. /
A downloader written for the harness would have proved the harness's downloader works.

**Y se comprueba que la sustitución no aflojó nada**: con un paquete distinto del que el manifiesto
promete, la descarga lo rechaza y **no deja nada** en la carpeta de preparación. Es la aserción que
dice que lo único cambiado es el transporte. / With a package that is not what the manifest promised,
the download refuses it and leaves nothing staged — the assertion that says only the transport
changed.

## Lo que el transporte no hace / What the transport does not do

- **No implementa `Range`.** El descargador ya trata una respuesta que no es `PartialContent` como
  «empezar de cero», y eso es camino del producto, no un atajo: un servidor tiene derecho a ignorar
  una petición de rango. La reanudación se mide donde vive, contra su servidor de bucle. / It does not
  implement `Range`, because the downloader already treats a non-partial answer as "start from zero".
- **No compone rutas a partir de la petición.** Responde a **la dirección que el manifiesto declara**
  y con **el archivo que nombra**, resuelto dentro de la carpeta de traspaso. Un transporte que
  sirviera lo que cualquier dirección pidiera sería una forma de leer esta máquina a través de un
  manifiesto. / It answers only the declared address with the named file: a transport that served
  whatever an address asked for would be a way to read this machine through a manifest.

## La escena / The scene

El manifiesto se escribe **después** de que el shell esté arriba, y el hash y el tamaño se **calculan
del archivo sembrado** en vez de declararse a mano: un manifiesto que prometiera otra cosa estaría
midiendo la verificación en lugar del botón. / The manifest is written after the shell is up, and its
hash and size are computed from the seeded file — a manifest promising something else would measure
the verification instead of the button.

La sonda de instalar no es la pantalla: es **la última línea de lo anotado**, que dice qué paquete se
habría entregado. Una pantalla puede decir «entregado a Windows» sin que nada haya salido. / The
install probe is the last line of the handover record, not the screen: a screen can say "handed to
Windows" with nothing having left.

## Las dos carreras que la puerta cazó / The two races the gate caught

Ninguna la vio `dotnet test`. Las dos salieron en `eng/run-accessibility.ps1 -Passes 2`, que es como
lo corre CI. / Neither showed up under `dotnet test`; both came out of the gate as CI runs it.

**1. Una sonda de estado se satisface con que la pulsación haya empezado algo.** Tercera aparición de
esta forma —antes en el informe de privacidad y en las copias—:

```
Expected: "UpdateStatusReady"
Actual:   "UpdateStatusDownloading"
```

`StatusKey` pasa a «descargando» en el instante del clic, así que una sonda que lo mire da la
pulsación por buena **antes de que haya llegado nada**. La sonda es ahora **cuántos paquetes hay en la
carpeta de preparación**, que sólo cambia cuando el hash y el tamaño coinciden, y el resultado se
afirma tras esperar al reposo (`IsBusy`). / A status turns to "downloading" the instant the button is
pressed; the probe now counts staged packages, and the outcome is asserted after waiting for idle.

**2. Una sonda que lee un archivo que la aplicación está escribiendo mide la carrera, no el efecto.**
Sólo en la **segunda** pasada:

```
System.IO.IOException : The process cannot access the file '…\handoff\system-handoff.txt'
because it is being used by another process.
```

El registro se escribe desde el hilo en el que aterriza el efecto, y abrirlo de la forma corriente
pierde la carrera. Ahora se abre **compartiendo la escritura**, y un instante en que no se puede leer
contesta «nada todavía», que es lo honesto y lo que `PressAsync` sabe reintentar. Se aplicó a las tres
escenas que leen un registro, no sólo a la que falló. / The record is written from whichever thread
the effect lands on, so the probe now shares the write and answers "nothing yet" when it cannot read —
applied to all three scenes that read a record, not only the one that failed.

## Lo que queda, y va aparte con su razón / What is left, apart and with its reason

**«Cancelar» es lo único que queda de la 7a**, y es la **7c**: lleva `IsEnabled="{Binding IsBusy}"` y
`CanExecute => IsBusy`, así que sólo existe mientras algo corre — y con el paquete al lado, en el
disco, la descarga entera acaba en **milisegundos**. La ventaja sobre la de copias sigue en pie: la
fuente es del arnés y **puede servir despacio a propósito**, así que la siembra tiene que declarar
una espera y medirse antes de escribir la escena. Se dice aquí en vez de dejarlo en silencio. / Cancel
is what is left, and it becomes 7c: it only exists while something is running, and with the package
on disk beside it the download finishes in milliseconds. The harness's source can serve slowly on
purpose, so the seeding has to declare a wait and be measured before the scene is written.

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet test tests/ApSolutions.LocalMedia.PackagingTests -c Release -m:1 --settings eng/test.runsettings --logger trx --filter "FullyQualifiedName~HandoffUpdateDownload"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/check-walk-coverage.ps1
```
