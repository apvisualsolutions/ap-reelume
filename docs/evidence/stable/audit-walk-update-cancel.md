# La descarga que se para a medias / The download stopped halfway

«Cancelar» del actualizador, pulsado con el ratón **mientras la descarga está en vuelo**, que es el
único momento en que ese botón existe. / The updater's Cancel, pressed with the mouse while the fetch
is in flight, which is the only moment that button exists.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 94 | **95** |
| Pendientes / Pending | 34 | **33** |

```
The walk: 129 declared command controls in 128 identities; 95 pressed, 33 pending.
```

## El rojo archivado / The archived red

Con la escena escrita y el transporte todavía sin espera, la pulsación de Descargar volvió con la
descarga **ya terminada**: / With the scene written and the transport still without a wait, the press
of Download came back with the fetch already finished:

```
Assert.Equal() Failure: Strings differ
                    ↓ (pos 12)
Expected: "UpdateStatusDownloading"
Actual:   "UpdateStatusReady"
```

Eso es exactamente la razón por la que este control llevaba dos tandas declarado impulsable:
`IsEnabled="{Binding IsBusy}"` y un comando que sólo puede ejecutarse ocupado, con el paquete en la
carpeta de al lado. / That is exactly why this control had been declared unpressable: it exists only
while something runs, and the package sits in the folder next door.

## Dónde vive la espera, y por qué ahí / Where the wait lives, and why there

El manifiesto gana un campo **opcional**, `serveDelayMilliseconds`, y el transporte del traspaso
sostiene la respuesta ese tiempo antes de servir el paquete. **No se añadió nada al producto.** La
línea es la misma que ya se trazó en esta superficie: lo que una ejecución aislada sustituye es **de
dónde vienen los bytes**, y una fuente tiene derecho a ser lenta. / The manifest gains an optional
field and the handover transport holds the answer for that long before serving the package. Nothing
was added to the product: what an isolated run replaces is where the bytes come from, and a source is
entitled to be slow.

Lo que la cancelación recorre es **del producto, entero**: el token que el modelo de vista creó al
empezar, la interrupción de la espera, la `OperationCanceledException` que el descargador ya sabía
responder y el `UpdateStatusCancelled` que la pantalla lee. / What the cancellation travels is the
product's own, all of it: the token the view model created, the interrupted wait, the exception the
downloader already answered to, and the status the screen reads.

**Y por eso es escena propia** y no un paso más de la anterior: un transporte que contesta despacio
cambiaría los tiempos de descargar e instalar, que ya estaban verdes. / Its own scene rather than a
step in the one before it: a slow transport would change what Download and Install measure.

## La ventana, medida antes de fijarse / The window, measured before it was set

| Medida / Measure | Valor / Value |
|---|---|
| Lo que consumen las dos pulsaciones / Spent by both presses | **950 ms** |
| Presupuesto de reintentos de `PressAsync` / Its retry budget | 8 × ~300 ms = **2400 ms** |
| Ventana declarada / Declared window | **5000 ms** |

Se empezó por 3000 ms, y **la medición lo descartó**: 950 + 2400 = 3350 ms es lo que la ventana tiene
que poder aguantar, porque `PressAsync` repite hasta ocho veces una pulsación que no cambia nada y
cada reintento es un asentamiento de tiempo real. Una ventana que no quepa el presupuesto del arnés
convierte una pulsación lenta en un fallo falso. Y no cuesta nada tenerla holgada: en cuanto la
cancelación llega, el resto de la espera se abandona. / 3000 ms was the starting point and the
measurement rejected it: the window has to hold the harness's own retry budget as well, and a window
with room in it costs nothing, because cancelling abandons the rest of the wait.

La escena **afirma ese número**, no sólo lo escribe: si las pulsaciones se pasan de la ventana, lo
dice con los milisegundos gastados en vez de dejar un «esperaba cancelado, había listo» que no
explica nada. / The scene asserts that number rather than only writing it down.

## Las sondas / The probes

- **Aquí el estado ES el efecto**, y es el único sitio de esta superficie donde eso vale: entre
  pulsar «Cancelar» y que la descarga se pare no hay ningún transitorio con el que una sonda pueda
  darse por satisfecha. Es la carrera que costó tres mediciones en otras pantallas, y aquí no
  existe. / The status is the effect here, and this is the one place on this surface where that is
  safe.
- **Lo que un estado no puede decir es que no llegó nada**, así que después se pregunta a la carpeta
  de preparación: ningún `.msix`. / A status cannot say that nothing arrived, so the staging folder
  is asked afterwards.
- **Y la pulsación de Descargar usa el estado a propósito**: una sonda que cambia en el instante del
  clic es la equivocada para una pulsación que tiene que aterrizar, y la correcta para una cuyo único
  propósito es que ahora hay algo en vuelo. / The Download press uses the status on purpose: a probe
  that turns the instant a button is pressed is the wrong one for a press that has to land and the
  right one for a press whose point is that something is now in flight.

## Las dos ramas del campo opcional, en la misma suite / Both branches, in one suite

El campo se lee con `TryGetProperty`, así que tiene dos ramas: declarado y ausente. **Las dos se
cubren en `PackagingTests`**, en una sola prueba que mide las dos respuestas —una sostenida 500 ms y
otra inmediata sobre el mismo paquete—, porque al fusionar informes Cobertura se conserva **el mejor
de los dos** para cada línea y no la unión: una rama partida entre dos suites se lee como media rama
para siempre. / The optional field is read with a fallback, so it has two branches, and both are
covered inside one suite: a merged Cobertura report keeps the better of two runs for a line rather
than the union of them.

**Una trampa del arnés, medida de paso:** reescribir el paquete mientras la respuesta anterior aún lo
tiene abierto da `IOException`. La segunda mitad de esa prueba reescribe **sólo el manifiesto**, que
es lo único que cambia. / A harness trap measured on the way: rewriting the package while the previous
answer still holds it open throws; only the manifest is rewritten.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn          # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
eng/run-accessibility.ps1 -Mode Verify -Passes 2           # 107 + 107, 0 críticos / 0 critical
dotnet test tests/ApSolutions.LocalMedia.PackagingTests    # 192
eng/verify-docs.ps1                                        # 161 documentos / documents
eng/check-walk-coverage.ps1                                # 95 pulsados, 33 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
dotnet test tests/ApSolutions.LocalMedia.PackagingTests -c Release -m:1 --settings eng/test.runsettings --logger trx --filter "FullyQualifiedName~HandoffUpdate"
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```
