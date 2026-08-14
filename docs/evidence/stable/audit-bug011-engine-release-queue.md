# Una sola cola de liberación / One deferred-release queue

Evidencia de **BUG-011**: el motor de reproducción guardaba la **tercera** cola de liberación
diferida, con el mismo desecho sin guarda que ya costó `BUG-010`. / Evidence for **BUG-011**: the
playback engine kept the **third** deferred-release queue, with the same unguarded dispose that
already cost `BUG-010`.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-14.

## La medición previa / The measurement first

Copias de la cola en `src/`, antes de tocar nada: / Copies of the queue in `src/`, before touching
anything:

```
src/…/Playback/LibVlcMediaPlayerEngine.cs:46   private readonly Queue<DeferredMedia> _deferredReleases
src/…/Playback/LibVlcFactory.cs:34            private static readonly Queue<DeferredMedia> DeferredReleases
```

Dos, y no son la misma cosa. La del motor desecha el medio **dentro de su propio candado y sin
guarda**: / Two, and they are not the same thing. The engine's disposes the media **inside its own
lock and with no guard**:

```csharp
_ = _deferredReleases.Dequeue();
pending.Media.Dispose();          // sin try/catch — LibVlcMediaPlayerEngine.cs:652-653
```

Una excepción ahí sale del bucle con `_isDrainScheduled` en `true`, de modo que **el trabajador no
vuelve a programarse nunca** y todo medio abierto después se filtra en silencio. La de la fábrica
vive con la guarda puesta desde `BUG-010` y explica por qué. / An exception there leaves the loop with
the worker flag raised, so the drain is never scheduled again and every media opened afterwards leaks
in silence. The factory's has carried that guard since `BUG-010`.

## El rojo / The red

Dos, y ninguno es el mismo defecto visto dos veces. / Two, and neither is the same defect seen twice.

**Regla de origen** — al quitar el motor de la lista que sólo puede encoger: / **Source rule** — with
the engine taken off the shrink-only list:

```
NativeInstanceOwnershipTests.The_deferred_release_queue_has_one_implementation… [FAIL]
  The deferred media release is the factory's; these keep a queue of their own:
  src/ApSolutions.LocalMedia.Infrastructure/Playback/LibVlcMediaPlayerEngine.cs.
```

**Comportamiento** — dónde descansa el medio cuando el motor lo suelta: / **Behaviour** — where the
media rests once the engine lets go of it:

```
MediaPlayerReleaseOwnershipTests.A_detached_media_rests_in_the_factorys_queue [FAIL] (12 s)
  Stopping detached the media without handing it to the factory's deferred release, so it is
  being freed by something else — and whatever that is, its failure is not the one the drain was
  hardened against.
```

El segundo importa porque el primero se puede satisfacer moviendo texto. Este se mide desde fuera:
el contador de la fábrica es la única superficie pública donde un medio en reposo se ve. / The second
matters because the first can be satisfied by moving text. This one is measured from outside: the
factory's count is the only public surface where a resting media is visible.

## La corrección / The fix

`LibVlcFactory` gana `FlushDeferredReleasesAsync(TimeSpan techo)`: espera a que la cola quede vacía,
y **agotar el techo devuelve `false`, no lanza** — un desmontaje que no puede terminar es peor que un
medio que descansa un momento más, y el drenaje lo libera igual. El motor cambia lo que el diseño
decía y nada más: `DeferRelease` propio → `_factory.DeferRelease` (tres llamadas), y en `DisposeAsync`
el drenaje propio → el vaciado de la fábrica, **antes** de `ReleaseMediaPlayer`. Ese orden es lo
único intocable: soltar el reproductor antes que sus medios revienta la destrucción nativa. La
ventana de reposo de 1 s **no se toca**. / The factory gains a flush with a ceiling that returns
`false` instead of throwing; the engine swaps three release calls and one await, and the order —media
before player— is untouched, as is the 1 s quiescence window.

Desaparecen del motor la cola, el candado, la bandera, el drenaje, la constante de reposo y el
registro `DeferredMedia`: **−52 líneas en el motor, +27 en la fábrica**. El techo del vaciado es de
5 s, que es holgado para la ventana de 1 s de los medios propios y de lo que descanse en la cola
compartida, y corto para que un catálogo ocupado no retenga un cierre. / The engine loses its queue,
lock, flag, drain, quiescence constant and record; the flush ceiling is 5 s.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `NativeInstanceOwnershipTests` | 3 de 3 / of 3 — la lista quedó **vacía** / the list is now empty |
| `ApSolutions.LocalMedia.ArchitectureTests` | 23 de 23 / of 23 |
| `MediaPlayerReleaseOwnershipTests` | 1 de 1 / of 1 |
| `ApSolutions.LocalMedia.MediaTests` | 116 de 116 / of 116 |
| `eng/verify.ps1` completo / full | verde / green |

La prueba de resistencia vive donde ya había un proceso hijo que abre y cierra el motor treinta
veces: `HandleGrowthTests` mide ahora también la cola compartida al terminar. Un contador de proceso
sólo es medible ahí, porque en el anfitrión compartido las demás suites escriben en el mismo número.
/ The resistance test lives in the child process that already ran thirty open/close cycles; a
process-wide counter is only measurable there.

| Fase / Phase | Manejadores / Handles (ciclo 1 → 30) | Cola al soltar / Queue after dispose |
|---|---|---|
| `open-only` | 438 → 437 | **0** |
| `software-play` | 450 → 452 | **0** |

Esa columna **no podía ser roja antes del cambio**, y se dice aquí en vez de presentarla como
prueba: antes, los medios del motor descansaban en su cola privada y el contador de la fábrica valía
cero por no verlos. Esa invisibilidad **era** el defecto, igual que en `BUG-010`. Lo que la columna
afirma ahora es que el vaciado vació de verdad, que es lo que el techo podría no cumplir. / That
column could not have been red before the change, and saying so is better than presenting it as
proof: the engine's media rested in a private queue and the factory's count read zero because it
could not see them. That invisibility **was** the defect. What the column states now is that the
flush actually flushed, which is the thing the ceiling could fail to do.
