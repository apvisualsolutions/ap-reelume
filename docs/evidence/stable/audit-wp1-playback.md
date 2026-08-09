# WP-1 — Reproducción y ciclo de vida / Playback and lifecycle

Evidencia de las correcciones del paquete WP-1 de la auditoría profunda del 2026-08-08. Cada defecto
lleva su RED archivado, la corrección mínima y el GREEN con sus puertas. / Evidence for the WP-1
fixes from the deep audit of 2026-08-08. Each defect carries its archived RED, the minimal fix, and
the GREEN with its gates.

## BUG-002 — «Continuar donde lo dejaste» dejaba el vídeo en cero / "Continue where you left off" left the video at zero

**El defecto / The defect.** Tres eslabones sueltos en la misma cadena: la decisión de reanudación se
calculaba *después* de abrir el medio, nadie pasaba la posición inicial a `PlaybackRequest` (el motor
la acepta desde siempre), y el diálogo se construía sin manejador — pulsar «Continuar» sólo ocultaba
el cartel. / Three loose links in one chain: the resume decision was computed *after* the media
opened, nobody passed the start position into `PlaybackRequest` (the engine has always accepted
one), and the prompt was built with no handler — pressing "Continue" only hid the card.

**RED (archivado / archived).** `ResumeWiringTests`, tres aserciones contra el ensamblado real,
las tres en rojo a la primera ejecución / three assertions against the real assembly, all three red
on their first run:

- `The_resume_prompt_is_built_with_a_handler_that_reaches_playback` — el prompt se construía con
  un solo argumento / the prompt was built with a single argument.
- `The_resume_decision_exists_before_the_media_opens` — `DecideAsync` aparecía después de
  `player.OpenAsync` / `DecideAsync` appeared after `player.OpenAsync`.
- `The_media_opens_at_the_position_the_decision_chose` — la apertura no pasaba ninguna posición /
  the open passed no position.

**La corrección / The fix.**

- `CompositionRoot.OpenPlayerAsync`: la decisión se calcula antes de abrir; el medio se abre en
  `resume.Position` cuando la decisión es `Resume`; el prompt recibe un manejador que busca a la
  posición elegida (`ControlPlayback.SeekAsync`) — «Empezar de nuevo» busca a cero. / the decision
  is computed before the open; the media opens at `resume.Position` when the decision is `Resume`;
  the prompt receives a handler that seeks to the chosen position — "Restart" seeks to zero.
- `PlayerViewModel.OpenAsync` gana `startPosition` y lo entrega a `PlaybackRequest`. / gains
  `startPosition` and hands it to `PlaybackRequest`.

**GREEN.**

- `ResumeWiringTests` 4/4: las tres aserciones de ensamblado más
  `The_view_model_hands_the_start_position_to_the_session_it_starts` (40 min entran, 40 min llegan
  al coordinador / 40 minutes in, 40 minutes reach the coordinator).
- `LibVlcSmokeTests.Opening_at_a_stored_position_starts_playback_there_and_not_at_zero` — nuevo,
  con decodificación real / new, with real decoding: una muestra de 3 s abierta con
  `startPosition = 1,5 s` reproduce desde ≥1,0 s, no desde cero (la búsqueda de `start-time` es
  gruesa a propósito; la aserción es «reanudó, no reinició»). / a 3 s sample opened with
  `startPosition = 1.5 s` plays from ≥1.0 s, not from zero (the `start-time` seek is deliberately
  coarse; the assertion is "it resumed, it did not restart").
- Suites completas / full suites: UiTests 317/317, LibVlcSmokeTests 6/6, `dotnet format` limpio,
  compilación `-warnaserror` 0/0.

**Límite declarado / Declared limit.** El paseo físico por el artefacto ensamblado (abrir un
episodio con progreso real y pulsar «Continuar») queda pendiente de la siguiente verificación
física; los tres niveles de prueba anteriores cubren la cadena completa por partes. / The physical
walk of the assembled artifact (opening an episode with real progress and pressing "Continue")
awaits the next physical verification; the three levels above cover the full chain piecewise.

## BUG-001 — El extractor liberaba LibVLC en el orden que estrella el proceso / The extractor released LibVLC in the order that crashes the process

**El defecto / The defect.** `LocalSegmentFeatureExtractor.DecodeWindowAsync` hacía las tres cosas
que este repositorio tiene escritas como modo de fallo nativo: paraba un player aún reproduciendo,
liberaba el player antes que su media, y disponía el media sin ventana de quiescencia — sobre la
misma instancia nativa que usa la reproducción. / did the three things this repository has written
down as the native failure mode: stopped a still-playing player, released the player before its
media, and disposed the media with no quiescence window — on the same native instance playback uses.

**Sobre el RED / About the RED.** El crash es nativo y probabilístico: el simulacro de veinte
ventanas con el orden malo **pasó en esta máquina** (2 s), así que no hay un rojo determinista que
archivar. La medición que sustenta el defecto es la contradicción con las tres reglas escritas en el
propio código (`LibVlcMediaPlayerEngine.cs` — orden media→player y pausa antes de parar —,
`LibVlcMediaProbe.cs` — quiescencia —) y el historial: reproducir ese crash es como se aprendió cada
regla. / The crash is native and probabilistic: the twenty-window drill with the wrong order
**passed on this machine** (2 s), so there is no deterministic red to archive. The measurement
behind the defect is the contradiction with the three rules written in the code itself, and the
history: reproducing that crash is how each rule was learnt.

**La corrección / The fix.**

- `LibVlcFactory.DeferRelease(media)`: cola de liberación diferida compartida, con ventana de
  quiescencia de 1 s y un trabajador de drenaje que **sobrevive a un `Dispose` que lanza** — el modo
  de muerte permanente que la auditoría señaló en la copia del probe (BUG-010) no se replica aquí. /
  a shared deferred-release queue with a 1 s quiescence window and a drain worker that **survives a
  throwing `Dispose`** — the permanent-death mode the audit flagged in the probe's copy (BUG-010) is
  not replicated here.
- `DecodeWindowAsync`: pausa si reproduce → `Stop()` → `player.Media = null` → `DeferRelease(media)`
  → `ReleaseMediaPlayer(player)`. El mismo orden que el teardown del engine. / the same order the
  engine's teardown keeps.

**GREEN.**

- `SegmentExtractionEnduranceTests` — nuevo simulacro permanente: diez copias del episodio (la caché
  del extractor archiva por ruta+tamaño+fecha), veinte ciclos completos crear-decodificar-liberar,
  cero players vivos, cero instancias nuevas, y el drenaje observado vaciándose (la cola es global
  al proceso y otras suites encolan en paralelo, así que la prueba exige «decrece», no «vacía»). /
  a new permanent drill: ten copies of the episode, twenty full cycles, zero live players, zero new
  instances, and the drain observed shrinking.
- MediaTests completa 106/106, formato limpio, compilación `-warnaserror` 0/0.

**Pendiente relacionado / Related pending.** El probe conserva su propia cola (con el defecto de
BUG-010) y su propia instancia nativa; unificarlo sobre `DeferRelease` es una tarea propia (P2). /
The probe keeps its own queue (with BUG-010's defect) and its own native instance; unifying it over
`DeferRelease` is its own task (P2).

## BUG-003 y BUG-007 — El bucle de guardado nunca arrancaba; el manejador de posición se acumulaba / The save loop never started; the position handler accumulated

**El defecto / The defect.** `PlaybackProgressTracker.RunAsync` — el bucle que hace verdad la
promesa de los cinco segundos — sólo se invocaba desde los tests: en la aplicación únicamente
escribían el cierre ordenado y el cambio de versión, así que un corte de luz perdía la sesión
entera. Los disparadores `Tick`, `Pause` y `Seek` estaban definidos y sin usar. Y cada apertura
suscribía `PositionChanged` al motor singleton sin desuscripción: veinte episodios en una sesión son
veinte manejadores vivos alimentando marcas de sesiones muertas. / `RunAsync` — the loop that makes
the five-second promise true — was only invoked from tests: in the application only the orderly
close and the version switch wrote, so a power cut lost the whole session. The `Tick`, `Pause`, and
`Seek` triggers were defined and unused. And every open subscribed `PositionChanged` on the
singleton engine with no unsubscribe: twenty episodes in one sitting are twenty live handlers
feeding dead sessions' markers.

**RED (archivado / archived).** `ProgressWiringTests`, cuatro aserciones contra el ensamblado, las
cuatro en rojo a la primera ejecución / four assertions against the assembly, all four red on their
first run: el bucle sin arrancar, `Pause` sin usar, `Seek` sin usar, y ningún `PositionChanged -=`.

**La corrección / The fix.**

- La sesión arranca el bucle (`RunProgressLoopAsync`, con sus excepciones observadas) y registra sus
  ganchos (`PlaybackSessionHooks`): cancelación del bucle y desuscripción de `PositionChanged` y
  `StateChanged`, ejecutados al cerrar el reproductor y antes de abrir el siguiente. / The session
  starts the loop (exceptions observed) and registers its hooks: loop cancellation and handler
  detachment, run at player close and before the next open.
- Pausar dispara `FlushAsync(Pause)` desde `StateChanged` — cubre todas las fuentes de pausa. /
  Pausing flushes from `StateChanged` — every pause source covered.
- `ControlPlayback` acepta un callback de persistencia tras cada búsqueda (transporte, saltos, botón
  de saltar intro, reinicio del prompt) que observa el destino recortado y dispara
  `FlushAsync(Seek)`. / accepts a persistence callback after every seek that observes the clamped
  target and flushes.

**GREEN.** `ProgressWiringTests` 4/4;
`ControlPlaybackTests.Every_seek_hands_its_clamped_target_to_the_persistence_callback` (el destino
recortado llega al callback en seek, seek fuera de rango y salto / the clamped target reaches the
callback on seek, out-of-range seek, and skip); UiTests 321/321; Application.Tests 173/173 y 9/9 en
ControlPlayback; formato limpio; `-warnaserror` 0/0.

**Estado / Status.** Con la reanudación (BUG-002) y el bucle cableado, el bloqueo de `PLY-008` queda
resuelto y la fila vuelve a `VERIFIED` con esta evidencia enlazada. / With resume (BUG-002) and the
loop wired, `PLY-008`'s blocker is resolved and the row returns to `VERIFIED` with this evidence
linked.

## BUG-005 — Un JSON ilegible del actualizador escapaba hasta el hilo de interfaz / Unreadable updater JSON escaped onto the interface thread

**El defecto / The defect.** Ningún `catch` de la cadena cubría `JsonException`: un portal cautivo
que responde `200` con HTML a `api.github.com` lanzaba desde `JsonDocument.Parse`, y la comprobación
automática del arranque corría dentro de un `Dispatcher.Post(async …)` — un `async void` disfrazado
que relanza en el hilo de interfaz y tumba la aplicación al abrirse. La ruta manual dejaba la
pantalla congelada en «Comprobando…». Había tres `Post(async …)` así: arranque, salida por bandeja y
activación de archivo suelto. / No `catch` in the chain covered `JsonException`: a captive portal
answering `200` with HTML threw from `JsonDocument.Parse`, and the startup automatic check ran
inside a `Dispatcher.Post(async …)` — an async void in disguise that rethrows on the interface
thread and takes the application down as it opens. The manual path froze the screen at "checking…".
There were three such `Post(async …)`: startup, tray exit, and loose-file activation.

**RED (archivado / archived).** Tres pruebas, tres rojos a la primera /
three tests, three first-run reds:
`UpdateWorkflowTests.A_source_answering_html_is_unreachable_rather_than_a_crash` (servidor real
respondiendo HTML → `JsonException` sin traducir),
`UpdateSurfaceTests.An_unexpected_failure_reads_as_unreachable_instead_of_checking_forever`
(la excepción salía del ViewModel), y
`DispatcherWiringTests.No_dispatcher_post_carries_a_bare_async_lambda` (tres `Post(async` en el
ensamblado).

**La corrección / The fix.** El proveedor traduce el cuerpo ilegible a
`UpdateSourceUnavailableException` («una fuente que nunca se alcanzó de verdad»); los tres métodos
del ViewModel cierran con una red final que aterriza en un estado (`Unreachable`/`LaunchRefused`)
en vez de propagar; y los tres `Post(async …)` pasan por `PostSafely`, que observa la excepción en
lugar de entregársela al dispatcher. / The provider translates the unreadable body into
`UpdateSourceUnavailableException`; the ViewModel's three methods end in a final net that lands on a
state instead of propagating; and the three `Post(async …)` go through `PostSafely`, which observes
the exception instead of handing it to the dispatcher.

**GREEN.** Los tres tests nuevos en verde; UiTests 323/323; UpdateWorkflowTests 57/57 (servidor TLS
real); formato limpio; `-warnaserror` 0/0.

## BUG-004 — La detección en segundo plano era incancelable y sobrevivía a la salida / Background detection was uncancellable and survived exit

**El defecto / The defect.** `ExecuteAsync` acepta un token de cancelación desde el día que se
escribió y el planificador lo llamaba sin ninguno: una detección en vuelo no podía pararse. Y la
salida de la aplicación no avisaba a nadie: cerrar la ventana podía dejar un proceso decodificando
por LibVLC y escribiendo en SQLite sin nadie mirándolo. / `ExecuteAsync` has taken a cancellation
token since the day it was written and the scheduler called it bare: a detection in flight could not
be stopped. And the application's exit told nobody: closing the window could leave a process
decoding through LibVLC and writing to SQLite with nobody watching.

**RED (archivado / archived).** `DetectionLifecycleWiringTests`, dos aserciones contra el
ensamblado, ambas en rojo a la primera ejecución: la llamada sin token y la salida sin parada. /
two assembly assertions, both red on their first run: the bare call and the stop-less exit.

**La corrección / The fix.** El planificador posee su token de apagado y lo pasa a cada ejecución;
`Stop()` deja de aceptar trabajo y cancela lo que esté en vuelo (la cancelación recorre la cadena ya
probada hasta el extractor, cuyo teardown ordenado corre en su `finally`); y `exitApplication` llama
a `Stop()` y termina los ganchos de la sesión antes de apagar. / The scheduler owns its shutdown
token and hands it to every run; `Stop()` refuses new work and cancels what is in flight (the
cancellation walks the already-tested chain down to the extractor, whose orderly teardown runs in
its `finally`); and `exitApplication` calls `Stop()` and ends the session hooks before shutting
down.

**GREEN.** `DetectionLifecycleWiringTests` 2/2; UiTests 325/325; formato limpio; `-warnaserror` 0/0.

**Límites declarados / Declared limits.** La cesión a la reproducción sigue siendo por episodio
(una ventana en decodificación no se aborta al arrancar una sesión — P3 de la auditoría), y liberar
el contenedor de servicios al salir pertenece a WP-6 (`ARQ-001`). / Yielding to playback remains
per-episode (a window mid-decode is not aborted when a session starts — the audit's P3), and
disposing the service container on exit belongs to WP-6 (`ARQ-001`).
