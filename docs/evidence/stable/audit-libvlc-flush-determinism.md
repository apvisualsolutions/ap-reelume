# El abandono que sólo ocurría en un runner lento / The giving up that only happened on a slow runner

`LibVlcFactory.cs` medía distinto **dentro de la misma máquina de integración**, con el archivo
intacto: una rama de su vaciado se ejecutaba o no según lo ocupada que estuviera la cola compartida
cuando a una prueba le tocaba desmontar. La corrección pide esa condición a propósito y la afirma. /
The same file measured differently **on the same CI machine**, untouched: one branch of its flush ran
or did not depending on how busy the shared queue happened to be when a test tore down. The fix asks
for that condition on purpose and asserts it.

Fecha / Date: 2026-08-18. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

**Cinco ejecuciones de CI del mismo árbol**, leídas del informe fusionado que lee la puerta
(`gh run download <id> -n test-results`). Cuatro dijeron una cosa y una dijo otra: / **Five CI runs
of the same tree**, read from the merged report the gate reads. Four said one thing and one said
another:

```
run 32134783777: lines 90/95 = 94,74 %   branches 18/20 = 90,00 %
run 32155083153: lines 90/95 = 94,74 %   branches 18/20 = 90,00 %
run 32131550943: lines 90/95 = 94,74 %   branches 18/20 = 90,00 %
run 32128429158: lines 90/95 = 94,74 %   branches 18/20 = 90,00 %
run 32161925025: lines 89/95 = 93,68 %   branches 17/20 = 85,00 %   <-- el suelo que hubo que ceder
```

Comparadas **línea a línea**, la diferencia es exactamente una línea y una rama, y las dos son la
misma decisión: / Compared **line by line**, the difference is exactly one line and one branch, and
both are the same decision:

```
run bueno / good run:  164 = 2/2 ramas,  166 ejecutada
run malo  / bad run:   164 = 1/2 ramas,  166 nunca ejecutada
```

La línea 164 es `if (Stopwatch.GetElapsedTime(started) >= ceiling)` y la 166 es su `return false`: el
vaciado **agotando su techo**. / Line 164 is the ceiling check and line 166 its `return false`: the
flush **exhausting its ceiling**.

## La causa / The cause

Nadie pedía esa rama. El motor vacía con un techo de cinco segundos al desmontar, y la cola de
liberación diferida es **una sola para todo el proceso**: durante la suite hay medias de otras
pruebas descansando en ella, cada una con su ventana de un segundo. Si el runner iba lo bastante
cargado, cinco segundos no bastaban y el vaciado se rendía; si iba holgado, terminaba a tiempo y la
rama no se ejecutaba jamás. La cobertura del archivo dependía, literalmente, de lo ocupada que
estuviera la máquina. / Nobody asked for that branch. The engine flushes with a five-second ceiling
on teardown, and the deferred release queue is **one for the whole process**: during the suite other
tests' media rest in it, each with its own one-second window. Busy enough, five seconds were not
enough and the flush gave up; idle enough, it finished in time and the branch never ran. The file's
coverage depended, literally, on how busy the machine was.

No es un defecto del producto y por eso el producto no cambia: el techo existe precisamente para que
un cierre no se quede esperando medias ajenas, y el comentario de `FlushDeferredReleasesAsync` ya
decía que esperar por el medio de otro componente es el precio de tener una sola cola endurecida. Lo
que fallaba era que **la prueba de esa decisión no existía**, y en su lugar la ejercía el azar. / It
is not a product defect and the product does not change: the ceiling exists so that a teardown never
waits on someone else's media, and the method already said as much. What was missing was **a test for
that decision**; chance was standing in for one.

## La corrección / The fix

Una prueba pide un techo **por debajo de la ventana de quiescencia** —un milisegundo contra un
segundo— con un medio recién encolado, así que rendirse es el único desenlace que el reloj permite,
por rápida que sea la máquina. Y lo afirma. No hizo falta tocar el archivo: el techo ya era un
parámetro. / A test asks for a ceiling **below the quiescence window** — one millisecond against one
second — with a media just enqueued, so giving up is the only outcome the clock allows, however fast
the machine. And it asserts it. The file needed no change: the ceiling was already a parameter.

Con ella llegaron las otras tres decisiones del desmontaje que nadie ejercía a propósito: el techo
que no puede esperar nada y se rechaza, el segundo `DisposeAsync` que no hace nada, y el disponer con
un reproductor todavía prestado. / Three more teardown decisions came with it, none of them exercised
on purpose before: the ceiling that cannot wait at all and is refused, the second `DisposeAsync` that
does nothing, and disposing while a player is still borrowed.

## Lo que se midió y no se esperaba / What was measured and not expected

**El guardia del drenaje no se puede provocar sin falsear el tipo nativo.** El drenaje envuelve cada
`Dispose` en un `catch` porque un obrero muerto sería una fuga silenciosa de todos los medios
siguientes, y esas dos líneas están sin cubrir en las cinco mediciones. La vía obvia se midió y no
sirve: / **The drain's guard cannot be provoked without faking the native type.** The drain wraps
every `Dispose` in a `catch` because a dead worker would silently leak every media after it, and
those two lines are uncovered in all five measurements. The obvious route was measured and does not
work:

```
A_media_disposed_twice_reports_it [FAIL]
  A second dispose is a no-op, so the drain's guard cannot be provoked this way.
```

Las dos vías que quedan cuestan más de lo que valen dos líneas: heredar de `Media` exige construir un
`LibVLC` propio —una segunda instancia nativa, que es el modo de fallo que esta clase existe para
evitar— o abrir la instancia de la fábrica. Queda descubierto **de forma estable**, que es
distinto de bailar. / The two remaining routes cost more than two lines are worth: subclassing
`Media` needs a `LibVLC` of its own — a second native instance, the very failure mode this class
exists to prevent — or opening up the factory's. It stays uncovered **stably**, which is a different
thing from swinging.

**Y una propiedad pública que no consumía nadie.** `IsHeadless` se asignaba en el constructor y se
exponía, y su línea estaba sin cubrir en las cinco mediciones por la razón más simple: nadie la leía.
No es una deducción de un `grep` —esa aquí ya salió mal una vez— sino del compilador: retirada,
`ApSolutions.LocalMedia.sln` compila en Release con `-warnaserror` sin un solo error. El modo sin
ventana lo deciden las opciones con las que se construye la instancia, no un `bool` que nadie
pregunta. / **And a public property nobody consumed.** `IsHeadless` was assigned and exposed, and its
line was uncovered in all five measurements for the simplest reason: nothing read it. Not deduced
from a `grep` — that has gone wrong here before — but from the compiler: with it removed, the
solution builds in Release with `-warnaserror` and no errors. Headless is decided by the options the
instance is built with, not by a `bool` nobody asks.

## Verde / Green

Tres ejecuciones seguidas de `ApSolutions.LocalMedia.MediaTests`, mismo binario, leídas igual que
arriba: / Three consecutive runs of the media suite, same binary, read the same way:

```
después / after, run 1: lines 88/91 = 96,70 %   branches 20/20 = 100 %
después / after, run 2: lines 88/91 = 96,70 %   branches 20/20 = 100 %
después / after, run 3: lines 88/91 = 96,70 %   branches 20/20 = 100 %
```

Antes, en esa misma suite y esa misma máquina: `lines 89/95 = 93,68 %  branches 18/20 = 90,00 %`. Las
tres líneas que faltan son `CreateDefault()` —que sólo la llama el arranque de la aplicación, y en el
informe fusionado sí se cubre— y las dos del guardia del drenaje. / Before, on that same suite and
machine: 89/95 and 18/20. The three missing lines are `CreateDefault()` — called only by the
application's startup, and covered in the merged report — and the drain guard's two.

```
Correctas! - Con error: 0, Superado: 123, Omitido: 0, Total: 123  (MediaTests, 2 m 30 s)
Correctas! - Con error: 0, Superado: 456, Omitido: 1, Total: 457  (IntegrationTests, 1 m 9 s)
Correctas! - Con error: 0, Superado:  30, Omitido: 0, Total:  30  (ArchitectureTests, 1 s)
```

El suelo de `eng/coverage-debt.txt` sube con la medición de CI que verifique este commit, copiando el
artefacto `coverage-debt` entero, como manda la regla. / The floor in `eng/coverage-debt.txt` rises
with the CI measurement that verifies this commit, copying the whole `coverage-debt` artifact, as the
rule requires.
