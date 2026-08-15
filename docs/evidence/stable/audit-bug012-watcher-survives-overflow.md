# El vigilante sobrevive al desbordamiento / The watcher survives the overflow

`BUG-012`. Lo destapó un rojo intermitente de CI —`InternalBufferOverflowException` en
`FileWatcherRecoveryTests`, ejecución
[31852708016](https://github.com/apvisualsolutions/ap-reelume/actions/runs/31852708016)— y detrás
había un defecto que no es intermitente. / Raised by an intermittent CI red; behind it sat a defect
that is not intermittent.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La cadena estaba leída, no ejecutada / The chain was read, not run

El plan decía: el desbordamiento cierra el canal, el coordinador lo degrada a un escaneo de
recuperación, `RootWatchBackground` suelta el root, y `Watch` vuelve al arrancar o **tras un escaneo
manual**. La primera medición desmintió la última mitad. / The written chain had the last half wrong.

Rojo archivado, con el código de antes:

```
A_watcher_that_died_is_watched_again_after_a_manual_scan  [FAIL, 10 s]
   The watcher was never started.
```

Un root `Continuous` **nunca sale** de `_watching`: `RootWatchCoordinator.StartAsync` es un
`Task.WhenAll` del vigilante y del planificador de reserva, y el planificador de un root continuo
**no termina nunca** —su bucle de recuperación es infinito—, así que el `finally` que libera el root
no llega. `EnsureWatching` tras un escaneo manual encuentra el root todavía apuntado y no hace nada.
/ A continuous root never leaves the watching set, because the fallback schedule it shares a task
with never ends; a manual scan cannot bring the live watcher back.

**Lo que de verdad pasaba: una carpeta en `Continuous` dejaba de vigilarse en vivo hasta el siguiente
arranque de la aplicación.** No se pierden archivos —el escaneo de recuperación se hace—, pero la
vigilancia inmediata desaparece justo tras la primera tanda grande, que es cuando más se la necesita.
/ Nothing is lost, but live watching was gone until the next launch.

## El desbordamiento no se reproduce en esta máquina / The overflow does not reproduce here

Antes de tocar el tamaño del buffer se midió cuánto cuesta desbordarlo, con una tormenta de **64 000
operaciones** (32 000 archivos creados y borrados, ocho hilos, 6 s): / Measured before changing it:

| Buffer | Tormenta / Storm | Lotes / Batches | Cambios / Changes | Desbordamientos / Overflows |
| --- | --- | --- | --- | --- |
| 8 KiB (el de antes / the old one) | 64 000 operaciones | 1 | 32 000 | **0** |
| 64 KiB (el nuevo / the new one) | 64 000 operaciones | 1 | 32 000 | **0** |

Ninguno de los dos desbordó aquí, y el runner hospedado desbordó con **mil** añadidos a un archivo.
Es una carrera contra la velocidad de la máquina, así que **no hay prueba de integración determinista
del desbordamiento real** y no se finge una: la decisión que el desbordamiento dispara se prueba
donde no hace falta la tormenta. / It is a race against machine speed, so the decision is tested
where no storm is needed, and no deterministic integration test of a real overflow is pretended.

## La corrección / The fix

1. **Una política, en el dominio.** `WatchErrorPolicy.MeansEventsWereLost` es una pregunta cerrada
   con un solo sí: un desbordamiento significa «he perdido eventos»; cualquier otro fallo —el root
   que deja de contestar, el handle que se cierra— es el final de ese vigilante. Se abandona un
   vigilante sólo por razones que no sean ésta, porque equivocarse cuesta una carpeta que deja de
   seguirse en silencio. / A closed question with one yes.
2. **El buffer, en su techo.** `DebouncedFileWatcher.InternalBufferBytes` = 64 KiB, el máximo que la
   plataforma admite, frente a los 8 KiB por defecto. No evita el desbordamiento: lo hace raro, y por
   eso no es la corrección sino su acompañante. / Rare, not impossible — which is why it is not the
   fix but its companion.
3. **El lote dice que perdió eventos.** `FileChangeBatch.EventsLost` viaja hasta el coordinador, que
   lo convierte en **un** escaneo de recuperación —que cubre también los cambios que venían en el
   mismo lote— y **sigue leyendo el flujo**. El canal ya no se cierra. / The batch carries the news
   and the watching goes on.
4. **Y un vigilante que muere de verdad vuelve.** El coordinador reintenta la vigilancia en la
   siguiente pasada de reserva, que es el latido que esta parte ya tenía: sin reloj nuevo, sin
   constante nueva y sin bucle caliente contra un root roto. Cuando no viene ninguna pasada más, el
   canal de reintentos se cierra y no se espera a un latido que paró. / The heartbeat this slice
   already had.

## Lo que se afirma, y dónde / What is asserted, and where

| Prueba / Test | Qué fija / What it pins |
| --- | --- |
| `WatchErrorPolicyTests` (6) | El desbordamiento es «eventos perdidos»; `IOException`, `UnauthorizedAccessException`, `NotSupportedException` y `ObjectDisposedException` no lo son |
| `A_batch_that_lost_events_is_one_recovery_scan_and_the_watching_goes_on` | Un lote marcado da **un** `Recovery`, no un `Watcher` de más — y **llega un segundo lote**, que es la mitad que el defecto se comía |
| `A_watcher_that_died_is_started_again_by_the_next_fallback_pass` | Dos arranques del vigilante y dos escaneos de recuperación |
| `A_continuous_root_is_watched_once_for_the_life_of_the_application` | Lo medido arriba, escrito como prueba: `EnsureWatching` **no** revive al vigilante, y por eso revivirlo no es tarea de `RootWatchBackground` |
| `Create_change_rename_delete_storm_is_coalesced_by_final_path` | La que se ponía roja en CI: ahora, tras la tormenta, un archivo creado después **sigue llegando** |

El comentario de `RootWatchBackground` decía que un escaneo posterior podía volver a arrancar un
vigilante terminado. Era falso, y ahora dice lo que pasa. / The comment said a later scan could start
an ended watcher again. It could not, and now it says what happens.

## Lo que queda verde / What is green

| Suite | Resultado |
| --- | --- |
| `Domain.Tests` | 409 / 409 |
| `Application.Tests` | 217 / 217 |
| `IntegrationTests` | 435 / 435 (+1 omitida declarada) |
| `ArchitectureTests` | 26 / 26 |
