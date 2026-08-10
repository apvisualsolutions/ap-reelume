# La ventana existe mientras migra / The window exists while it migrates

Evidencia de **ARQ-005, segunda mitad**: el arranque deja de retener el hilo que dibuja. /
Evidence for **ARQ-005, second half**: the startup stops holding the thread that draws.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## La medición que decidió la forma / The measurement that decided the shape

`MigrateAsync` está escrita con `await`s de verdad de principio a fin, y eso invitaba a la
corrección barata: cambiar `GetAwaiter().GetResult()` por `await`. Se midió antes de escribirla, y
menos mal. / It is written with real awaits throughout, which invited the cheap correction. It was
measured first.

```
16 migration(s): the call held the thread for 140 ms of the 140 ms it took in total.
```

**Cede el hilo en cero de sus puntos de espera.** `Microsoft.Data.Sqlite` implementa su superficie
`Async` de forma síncrona porque SQLite no tiene E/S asíncrona a la que ceder, así que cada `await`
reanuda en línea y la migración entera corre sobre quien la llamó — que es el hilo de interfaz. Un
`await` ahí habría dejado la ventana **igual de bloqueada y con aspecto de arreglada**. Por eso el
trabajo va a un hilo propio. / **It yields at none of them.** An await would have left the window
just as blocked while looking fixed, so the work goes to a thread of its own.

`MigrationYieldTests` lo fija en vez de suponerlo: una tarea ya completada al volver de la llamada
no puede haber cedido. Y si algún día el proveedor de SQLite gana E/S asíncrona de verdad, esa
prueba **falla**, que es exactamente el aviso de que el hilo aparte ya no hace falta. / The test
pins it, and turns into the notice that the extra thread is no longer needed if that ever changes.

## Qué cambió / What changed

| Antes / Before | Después / After |
|---|---|
| `FinishShell` migra y **luego** devuelve el shell | Devuelve un `ContentControl` con la vista de arranque, ya / returns a container holding the startup view, now |
| El hilo de interfaz migra / the interface thread migrates | `Task.Run`, y el contenido se sustituye al terminar / and the content is replaced when it ends |
| La ventana aparece cuando el trabajo acaba | La ventana aparece en el primer fotograma / in the first frame |

**Lo que no cambió, a propósito**: la decisión entre el shell y la pantalla de recuperación es la
misma de siempre —las cuatro excepciones que significan «este archivo no se puede usar», más la
comprobación de integridad cuando una migración reescribió el archivo—; sólo cambia **cuándo** se
toma. Lo que no es ninguna de esas cuatro sigue siendo un defecto y no se disfraza de problema de
base de datos: sube a la guarda de `GuardedEvent`, que existe desde ARQ-004. / The decision between
the shell and the recovery screen is the same one; only when it is taken has changed.

**Sin barra de progreso indeterminada.** Nada en ese camino sabe cuánto falta, y una barra que se
mueve sin significar nada es una imagen de progreso en lugar de progreso. La vista lleva el nombre
del producto, una línea de estado y su nombre de automatización, en los dos idiomas. / No
indeterminate progress bar: nothing there knows how much is left.

## El defecto de la casa, cazado en el acto / The house defect, caught in the act

Añadir la vista hizo fallar `No_surface_with_an_accessible_name_is_left_out_of_the_application`:

```
These surfaces announce an accessible name and cannot be reached: StartupView.
```

Es la prueba de **«registrado y nunca alimentado»** aplicada a las superficies, y preguntó lo
correcto: ¿quién abre esto? La respuesta legítima es que la ventana lo sostiene directamente, igual
que al shell y a la recuperación, así que `StartupView` pasa a ser una **raíz** del grafo — de dos a
tres. Que la corrección sea declarar la raíz y no silenciar la prueba es la diferencia entre
responder y tapar. / The orphan test asked the right question; the answer is that the window holds
it directly, so it becomes a third root rather than a silenced failure.

## El rojo / The red

```
AssembledStartupTests.The_window_is_given_something_to_show_before_the_database_is_ready   [FAIL]
AssembledStartupTests.The_shell_takes_the_startup_view_s_place_once_the_database_is_ready  [FAIL]
AssembledStartupTests.The_startup_view_names_itself_for_assistive_technology               [FAIL]
  Assert.IsType() Failure: Value is not the exact type

Con error: 3, Superado: 0, Omitido: 0, Total: 3
```

Los dos sitios que llamaban a `CreateShell()` afirmaban `Assert.IsType<ShellView>` sobre lo devuelto.
Ahora esperan el contenido final con un tope de 30 s, y **el mensaje del tope nombra lo que quedó en
su lugar**: la vista de arranque significa que el trabajo sigue, y la de recuperación que terminó y
se negó. Un tope que sólo dice «no llegó» no diagnostica nada. / The two callers now wait for the
final content with a ceiling, and the ceiling names whatever was left standing.

**Una prueba que hubo que replantear.** La del nombre accesible se escribió sobre el arranque
ensamblado y **perdió la carrera**: para cuando la ventana se muestra y el dispatcher corre, la
preparación ya ha terminado y el shell ha ocupado el sitio. Eso es el mecanismo funcionando, no un
fallo, así que la comprobación pasó a hacerse sobre la vista misma dentro de una ventana — el nombre
llega por recurso dinámico y un control que no cuelga de nada no tiene dónde buscarlo. / One test had
to be re-aimed: the assembled version lost the race, because by then the shell had already taken
over — which is the mechanism working.

## El antes y el después, que es para lo que estaba la línea base / Before and after

Tiempo hasta que hay ventana, según
[la línea base](audit-arq005-startup-baseline.md). Dos ejecuciones antes, una después, misma
máquina. / Time until there is a window: two runs before, one after, same machine.

| Fase / Phase | Antes / Before | Después / After |
|---|---|---|
| `open-with` | 1233 ms, 1214 ms | **779 ms** |
| `repair` | 2658 ms, 2779 ms | **2001 ms** |
| `first-launch` | 2292 ms, 1527 ms | **2316 ms** |

**Y aquí está lo que justifica haber medido tres y no uno.** Los dos ciclos que se repetían con poca
dispersión bajan de forma clara —440 ms y 700 ms—, y el que ya variaba en 765 ms entre dos
ejecuciones del mismo código **no dice nada**: su «después» cae dentro de su propio ruido. Si la
línea base hubiera sido sólo `first-launch`, que era lo que pedía el plan, la conclusión habría sido
«no cambió nada». / The two cycles that repeated tightly drop clearly; the one that already varied
by 765 ms between two runs of the same code says nothing, because its "after" lands inside its own
noise. A baseline of `first-launch` alone would have concluded that nothing changed.

La caída es mayor que los 140 ms que cuesta migrar medidos en local, y la explicación probable es
que ahí se midió con los binarios calientes: en un paquete recién desempaquetado, migrar cuesta más.
No se afirma más que eso — es **una** muestra después. / The drop is larger than the 140 ms the
migration costs locally, most likely because that was measured with warm binaries. That is as far as
one sample allows.

## Lo que esto no arregla / What this does not fix

**El rojo intermitente del 2026-08-10 sigue sin explicación.** Se le había atribuido a esta misma
tarea, y la [línea base](audit-arq005-startup-baseline.md) ya lo había desmentido: aquel fallo agotó
90 000 ms y el arranque entero cuesta poco más de dos segundos. Ahorrar medio segundo no cierra un
plazo de noventa. Si vuelve, la causa hay que buscarla en un arranque que **no ocurre**, no en uno
lento — y ahora hay una serie de números contra la que compararlo. / **The intermittent failure is
still unexplained.** Saving half a second does not close a ninety-second deadline. If it returns,
the cause is a launch that does not happen rather than a slow one — and now there is a series to
compare against.

## Verde / Green

| Suite | Resultado / Result |
|---|---|
| `ApSolutions.LocalMedia.AccessibilityTests` | 79 de 79 / of 79 |
| `ApSolutions.LocalMedia.UiTests` | 394 de 394 / of 394 |
| `ApSolutions.LocalMedia.IntegrationTests` | 407 de 408, 1 omitida por diseño / 1 skipped by design |
| `ApSolutions.LocalMedia.ArchitectureTests` | 18 de 18 / of 18 |
