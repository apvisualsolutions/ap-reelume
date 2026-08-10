# El candado que nadie podía abrir / The lock nobody could open

Evidencia de **ARQ-005, primera mitad**: la espera sale del `lock` de `WindowsMediaKeyService` y
recibe un techo. / Evidence for **ARQ-005, first half**: the wait leaves `WindowsMediaKeyService`'s
lock and gets a ceiling.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## El defecto, en dos consecuencias / The defect, in two consequences

`StartAsync` esperaba la señal de la bomba con `ready.Task.GetAwaiter().GetResult()` **dentro** de
`lock (_sync)`, y se llama desde el hilo de interfaz **cada vez que se abre un vídeo**. /
`StartAsync` blocked on the pump's signal inside the lock, and it is called from the interface thread
every time a video opens.

1. **La ventana dejaba de responder** hasta que un hilo ejecutando código nativo de registro
   contestaba. No es una espera larga en una máquina sana, pero es una espera del hilo que dibuja, en
   una ruta que se recorre en cada reproducción. / The window stopped answering until a thread running
   native registration code answered back.
2. **Una bomba que no contestara se llevaba todo por delante.** Sin techo, el hilo de interfaz
   esperaba para siempre — y **sosteniendo el candado**, de modo que el `StopAsync` que podría haberlo
   rescatado tampoco podía entrar. El hilo que nunca iba a volver era el que sujetaba la puerta. / A
   pump that never signalled left the interface thread waiting forever **while holding the lock**, so
   the stop that could have rescued it could not get in either.

## Qué cambió / What changed

| Antes / Before | Después / After |
|---|---|
| `GetAwaiter().GetResult()` dentro del `lock` | `await ... .WaitAsync(techo)` fuera de él / outside it |
| Sin techo / no ceiling | 5 s al arrancar, 2 s al parar / 5 s starting, 2 s stopping |
| `Join(2 s)` dentro del `lock` al parar | La bomba se avisa dentro y se espera fuera / told inside, waited for outside |
| `IsListening` tras contestar la bomba | En cuanto la bomba existe / from the moment the pump exists |

Ese último renglón no es cosmético. Marcar `IsListening` sólo al recibir respuesta abría una ventana
en la que un `StopAsync` no encontraba nada que parar y dejaba la bomba corriendo. / Marking
`IsListening` only on the answer left a window where a stop found nothing to stop and left the pump
running.

**Pasar el techo no es un error.** Las teclas del teclado multimedia son un extra, y una sesión que
empieza sin ellas es mejor que una sesión que no empieza; `RegisteredKeyCount` dice la verdad sobre lo
que realmente se reclamó. / **Passing the ceiling is not an error**: a session that starts without the
keys beats a session that does not start.

## El rojo, que no fue un rojo / The red, which was not a red

Aquí no hay salida de prueba que archivar, y decirlo importa más que disimularlo: con el código
anterior estas pruebas **no fallaban, colgaban**. Un candado retenido por un hilo que no vuelve no
produce un aserto roto, produce una suite que no termina. Por eso la bomba se puede sustituir: un
techo que nadie ha visto expirar es un techo que nadie sabe si funciona, que es exactamente la lección
que dejó el cuelgue del generador de medios. / There is no test output to archive: with the previous
code these tests did not fail, they hung. That is why the pump is substitutable — a ceiling nobody has
watched expire is a ceiling nobody knows works.

| Prueba / Test | Qué fija / What it pins |
|---|---|
| `A_pump_that_never_answers_does_not_take_the_caller_with_it` | Arrancar vuelve pese a que nadie conteste / starting returns anyway |
| `Stopping_gets_through_while_somebody_is_waiting_for_the_pump` | Parar entra mientras otro espera / stopping gets in while another waits |
| `A_ceiling_that_is_not_a_ceiling_is_refused` | Un techo de cero se rechaza al construir / a zero ceiling is refused |

La segunda es la que mide el candado y no el techo: con la espera dentro del `lock`, ese `StopAsync`
no habría vuelto nunca. / The second one measures the lock rather than the ceiling.

## Verde / Green

| Suite | Resultado / Result |
|---|---|
| `PlaybackInputTests` | 12 de 12 / of 12 |
| `ApSolutions.LocalMedia.AccessibilityTests` | 76 de 76 / of 76 |
| `ApSolutions.LocalMedia.ArchitectureTests` | 18 de 18 / of 18 |

## Lo que queda de ARQ-005, ya medido / What is left of ARQ-005, already measured

El arranque asíncrono, que es la otra mitad y la más grande: `FinishShell` bloquea el hilo de
interfaz con `GetAwaiter().GetResult()` para migrar la base de datos y, si esa migración reescribió el
archivo, para comprobar su integridad. / The asynchronous startup, which is the larger half.

| Sitio / Site | Qué bloquea / What it blocks |
|---|---|
| `CompositionRoot.cs:289` | La migración / the migration |
| `CompositionRoot.cs` (integridad / integrity) | Sólo si una migración reescribió el archivo / only if a migration rewrote the file |
| `CompositionRoot.cs` ×4 (diagnóstico / diagnostics) | Lecturas para el informe, bajo demanda / reads for the report, on demand |
| `Program.cs` (`DisposeAsync`) | Legítimo: el `finally` de `Main`, no el arranque / legitimate |

**Y el dato que hace la tarea abordable**: sólo **dos** sitios llaman a `CreateShell()`, los dos en
recorridos ensamblados, y los dos afirman `Assert.IsType<ShellView>`. El límite escrito sigue en pie —
la ventana no puede quedarse en blanco mientras migra, así que hay que mostrar algo desde el primer
fotograma y cambiarlo al terminar—, y ahora se sabe lo que cuesta. / **The number that makes it
tractable**: only **two** sites call `CreateShell()`, both in assembled walks.
