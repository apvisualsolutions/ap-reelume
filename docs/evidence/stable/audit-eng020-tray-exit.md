# ENG-020 — Cerrar la aplicación ya no termina en una excepción / Closing the application no longer ends on an exception

- Fecha / Date: 2026-09-25
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `82ef8742`
- Entorno / Environment: Windows 11 x64, .NET según `global.json`, Avalonia 12.1.1, LibVLC 3.0.23
- IDs: `ENG-020` (cerrada aquí), `ENG-005` (lo que queda sin medir)

## Veredicto / Verdict

**El icono de la bandeja se suelta en el hilo de la interfaz antes de que la liberación pueda ceder,
y soltarlo desde otro hilo ya no lanza.** Las dos cosas con su prueba vista fallar y su mutante visto
morir. / **The tray icon is let go on the interface thread before the teardown can yield, and letting
go of it from another thread no longer throws.** Both with their test seen red and their mutant seen
killed.

## La causa / The cause

`Program.Main` libera la aplicación desde el hilo de la interfaz cuando el bucle ya ha terminado. La
liberación del reproductor espera a que los medios que soltó reposen (`FlushDeferredReleasesAsync`,
un `Task.Delay` con `ConfigureAwait(false)`), y desde esa espera el resto del contenedor se libera en
un hilo del grupo. El icono pertenece al hilo de la interfaz, así que `WindowsTrayService.Dispose()`
lanzaba `The calling thread cannot access this object because a different thread owns it`. **Sólo
hay espera si algo se ha reproducido**, y por eso el fallo apareció al cerrar tras ver un vídeo y
ninguna prueba lo vio: las liberaciones de las pruebas no ceden. / `Program.Main` releases the
application from the interface thread once the loop has ended. The player's teardown waits for the
media it released to rest, and from that wait the rest of the container is released on a pool
thread. The icon belongs to the interface thread, so its `Dispose()` threw. **There is only a wait if
something played**, which is why it failed on exit after a video and no test saw it.

## Rojo / Red

`TrayLifecycleTests.The_real_tray_adapter_can_be_let_go_from_a_thread_that_does_not_own_it`, antes
del cambio, con el mensaje exacto que se vio al cerrar la aplicación el 2026-09-13:

```text
Actual:   System.InvalidOperationException: The calling thread cannot access this object because a different thread owns it.
   at Avalonia.Threading.Dispatcher.VerifyAccess()
```

`ApplicationHostTests.The_window_s_tray_icon_is_let_go_before_the_release_can_yield`, con el adaptador
ya corregido y el anfitrión sin tocar: la liberación **cedió** —la prueba lo exige antes de afirmar
nada, o no probaría nada— y el icono seguía vivo en ese momento:

```text
Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.ObjectDisposedException)
```

La espera se provoca dejando un medio en la cola de liberaciones diferidas de LibVLC, que es la misma
espera que deja una reproducción, sin reproducir nada. / The wait is provoked by leaving a media in
LibVLC's deferred-release queue — the same wait a playback leaves, without playing anything.

## Corrección / Fix

- `ApplicationHost.DisposeAsync` suelta el icono de la ventana **lo primero**, antes de cualquier
  `await`, en el hilo que libera. / Lets go of the window's icon first, before any `await`.
- `WindowsTrayService.Dispose` fuera de su hilo **encarga** la retirada al hilo de la interfaz en vez
  de tocar el objeto. / Off its thread, hands the removal to the interface thread.

La segunda sola no bastaba: en el cierre real el bucle ya no corre, así que lo encargado no lo
ejecutaría nadie y el icono quedaría en el área de notificación hasta pasar el ratón por encima. /
The second alone was not enough: on a real exit nobody runs what is handed to the loop.

## Mutantes / Mutants

| Mutante / Mutant | Prueba que lo mata / Killed by |
| --- | --- |
| Sin la liberación temprana en `ApplicationHost` / No early release in the host | `The_window_s_tray_icon_is_let_go_before_the_release_can_yield` (el rojo de arriba) |
| `Dispose` fuera de hilo no encarga nada (`Post(Release)` → `_ = 0`) / Off-thread `Dispose` hands nothing over | `The_real_tray_adapter_can_be_let_go_from_a_thread_that_does_not_own_it`: `Assert.False() Failure` |
| El `Dispose` original / The original `Dispose` | la misma, con la excepción de arriba |

## Lo que una prueba puede afirmar del cierre, y lo que no / What a test can say about closing

Puede afirmar el orden **dentro** de `ApplicationHost`: que el icono se va antes de la primera espera,
con una espera real delante. No puede afirmar que `Program.Main` libere desde el hilo de la interfaz,
ni el código con el que sale el proceso: eso es el anfitrión real de Windows, que nada del árbol monta
(`ENG-005`, del propietario). **El código 82 no sale de este árbol** —`ProcessFailureHandlers` no fija
códigos de salida— y no se ha vuelto a medir: lo comprueba el cierre a mano tras ver un vídeo. / It
can assert the order inside the host, with a real wait in front. It cannot assert what `Program.Main`
does or the process exit code: that is the real Windows host (`ENG-005`). **Exit code 82 does not come
from this tree** and was not measured again: a manual close after watching a video checks it.

Las teclas multimedia, el otro recurso del anfitrión atado a un hilo, no comparten el defecto: tienen
su propio hilo y se sueltan por mensaje (`PostThreadMessage`). / The media keys do not share the
defect: they own their thread and are released by message.

## Puertas / Gates

`dotnet format --verify-no-changes` limpio; compilación Release con `-warnaserror` sin avisos;
`TrayLifecycleTests` 13/13, `ApplicationHostTests` 10/10, `ArchitectureTests` 61/61,
`DocumentationTests` 118/118, las de la bandeja de `PerformanceTests` 3/3 y `verify-docs.ps1` en
verde. Las suites enteras de integración y accesibilidad las corre CI. / Format clean, Release build
with warnings as errors clean, and the suites above green; the full integration and accessibility
suites run in CI.
