# El desbordamiento que ocurría a veces / The overflow that used to happen sometimes

`DebouncedFileWatcher.cs` medía distinto en cada ejecución del mismo binario, y no era deriva del
recolector: eran tres condiciones que las pruebas provocaban sin poder garantizar. La corrección las
hace ciertas y las afirma. / The same binary measured this file differently on each run, and it was
not collector drift: three conditions were provoked without being guaranteed. The fix makes each of
them certain and asserts it.

Fecha / Date: 2026-08-18. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

Dos ejecuciones consecutivas de `ApSolutions.LocalMedia.IntegrationTests`, **mismo binario, sin tocar
una línea entre ellas**: / Two consecutive runs of the integration suite, **same binary, nothing
touched in between**:

```
antes / before, run 1: lines 85/96 = 88,54 %   branches 31/42 = 73,81 %
antes / before, run 2: lines 90/96 = 93,75 %   branches 30/42 = 71,43 %
```

Y después de la corrección, tres ejecuciones seguidas del mismo modo: / And after the fix, three
consecutive runs measured the same way:

```
después / after, run 1: lines 102/102 = 100 %   branches 46/48 = 95,83 %
después / after, run 2: lines 102/102 = 100 %   branches 46/48 = 95,83 %
después / after, run 3: lines 102/102 = 100 %   branches 46/48 = 95,83 %
```

Las dos ramas que faltan no son alcanzables y por eso el número se queda quieto en ellas: el `while`
que lee del canal nunca sale por `false` —el canal sólo se completa con un error, y entonces lanza— y
un `Renamed` sin ruta anterior no existe viniendo de `FileSystemWatcher`. / The two missing branches
are unreachable, which is why the figure sits still: the channel loop never exits by `false` — the
channel only completes with an error, and that throws — and a `Renamed` with no previous path cannot
arrive from `FileSystemWatcher`.

## La causa registrada era una de tres / The recorded cause was one of three

La cola decía que el baile venía del manejador `watcher.Error`, que sólo corre si Windows desborda el
búfer. Cierto, y medido: en la primera ejecución de arriba las líneas 112-115 no se ejecutaron nunca.
Pero al mirar las dos ejecuciones completas aparecieron otras dos, y ninguna estaba escrita en ningún
sitio: / The queue said the swing came from the `watcher.Error` handler, which only runs when Windows
overflows the buffer. True, and measured. But comparing the two full runs turned up two more that
were written down nowhere:

1. **El desbordamiento.** La prueba de `BUG-012` provoca una tormenta y no afirma que desbordara;
   cuando no desborda, aprueba igual. / The `BUG-012` storm never asserted the overflow, so it passed
   just the same when there was none.
2. **La otra mitad del mismo manejador.** El error que **no** es un desbordamiento —una raíz que deja
   de contestar— sí se ejecutaba en la segunda ejecución, y por casualidad: un directorio de prueba
   desapareciendo debajo de un vigilante todavía vivo. Nadie lo pedía; ocurría o no. / The half of the
   handler that ends the watching ran in the second run **by accident**: a test directory vanishing
   under a still-live watcher. Nobody asked for it; it happened or it did not.
3. **La coalescencia.** Qué parejas de cambios se juntaban dependía de lo que el sistema entregara
   durante la tormenta: 13 de 16 ramas en una ejecución y 11 de 16 en la otra. / Which pairs of
   changes met depended on what the system happened to deliver during the storm: 13 of 16 branches on
   one run and 11 of 16 on the other.

Y quedaba una cuarta, que no llegó a moverse en estas dos mediciones pero es la misma clase de cosa:
el debounce que expira **en el mismo instante** en que llega el cambio que lo cancela. Contra un
reloj real es una carrera de microsegundos. / And a fourth, which did not move across these two runs
but is the same kind of thing: the debounce elapsing in the very instant the change that cancels it
arrives — against a real clock, a race of microseconds.

## La corrección / The fix

**El búfer pasa a parámetro opcional del constructor**, con el valor de producto por defecto y el
mismo patrón que ya tenía `debounce`. Nada se volvió `internal`, `WatchSignal` sigue privado y la
aplicación sigue pidiendo los 64 KiB de siempre. La prueba pide el mínimo que la plataforma respeta
—4 KiB; por debajo lo eleva sola— y ahí **sí** desborda. / The buffer becomes an optional constructor
parameter defaulting to the product value, the pattern `debounce` already had. Nothing became
`internal`, `WatchSignal` is still private, and the application still asks for its 64 KiB. The test
asks for the smallest the platform honours — 4 KiB — and there it does overflow.

Cada una de las otras tres tiene ahora una prueba que la provoca a propósito: la raíz que desaparece,
la secuencia de cambios sobre una misma ruta, y un reloj cuya espera termina **con éxito** en el
momento en que se la cancela, que es la única forma de reproducir esa carrera sin depender del
azar. / Each of the other three now has a test that provokes it on purpose: the root that vanishes,
the sequence of changes over one path, and a clock whose wait ends **successfully** the moment it is
cancelled, which is the only way to reproduce that race without leaving it to chance.

## Lo que se midió y no se esperaba / What was measured and not expected

**Una tormenta secuencial no desborda 4 KiB.** El primer intento creó 2 000 archivos con nombres de
cien caracteres —unos veinte registros llenan ese búfer— uno detrás de otro, y no desbordó ni una
vez. El cuello de botella no es el vigilante vaciando el búfer: es crear el archivo. Cada creación
cuesta unas tres décimas de milisegundo, y eso es exactamente el respiro que el hilo del vigilante
necesita para ir por delante. Con las mismas creaciones **en paralelo** desborda en el primer
segundo. / **A sequential storm does not overflow 4 KiB.** The first attempt created 2 000 files with
hundred-character names — some twenty records fill that buffer — one after another, and never
overflowed. The bottleneck is not the watcher draining: it is creating the file. Each creation costs
about three tenths of a millisecond, which is exactly the breathing room the watcher's thread needs
to stay ahead. The same creations **in parallel** overflow within the first second.

Ese primer intento fue el rojo, y salió por la aserción nueva y no por un tiempo agotado: / That
first attempt was the red, and it came out through the new assertion rather than through a timeout:

```
Overflowing_the_system_buffer_is_reported_as_lost_events_and_the_watching_goes_on [FAIL]
  The storm never overflowed the buffer, so nothing was proven.
```

Que es justamente lo que la aserción existe para decir: la prueba anterior, en esa misma situación,
aprobaba. / Which is precisely what the assertion exists to say: the earlier test, in that same
situation, passed.

## Verde / Green

```
Correctas! - Con error: 0, Superado: 456, Omitido: 1, Total: 457  (IntegrationTests, 1 m 8 s)
```

Cinco ejecuciones seguidas de la prueba del desbordamiento, aisladas: cinco verdes, un segundo cada
una. / Five consecutive isolated runs of the overflow test: five greens, one second each.
