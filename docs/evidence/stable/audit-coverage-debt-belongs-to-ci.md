# El suelo lo pone quien mide / The floor belongs to whoever measures

El trinquete de cobertura nació el 2026-08-18 y **falló en CI desde su primer commit**. La causa no
era el código: los suelos se midieron en una máquina y se verifican en otra. / The coverage ratchet
failed on CI from the commit that introduced it, because its floors were measured on one machine and
verified on another.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

Siete archivos, medidos aquí y medidos en el runner, en el mismo commit: / Seven files, measured here
and measured on the runner, at the same commit:

| Archivo / File | Aquí / Here | CI |
|---|---|---|
| `Windows/Playback/WindowsAudioDeviceCatalog.cs` | 79/61 | **32/11** |
| `Infrastructure/Playback/LibVlcAudioOutputAdapter.cs` | 97/83 | 86/79 |
| `Presentation/Player/AudioOutputViewModel.cs` | 98/79 | 94/73 |
| `Infrastructure/Playback/LibVlcVideoCapabilities.cs` | 77/66 | 77/61 |
| `Infrastructure/Playback/LibVlcMediaPlayerEngine.cs` | 91/82 | 91/80 |
| `Infrastructure/FileSystem/DebouncedFileWatcher.cs` | 88/73 | 93/71 |
| `Windows/CompositionRoot.cs` | 90/64 | 89/64 |

Audio, LibVLC y temporizadores. **Un runner hospedado no tiene tarjeta de sonido**, así que la mitad
de `WindowsAudioDeviceCatalog` no se ejecuta allí jamás: no es que empeore, es que no hay nada que
enumerar. / A hosted runner has no audio device, so half of that file never runs there.

## Que no es azar, medido / Measured: it is not chance

La duda evidente es si los números simplemente bailan. Se midió, y **no bailan**: dos ejecuciones
locales completas del árbol entero, con una hora de diferencia, mueven **2 suelos de 219**, y uno de
los dos es un cambio real de código. / Two full local runs an hour apart move 2 floors out of 219,
and one of the two is a real code change.

```
-src/…/Library/LibraryViewModel.cs        89  80      ← el cambio de ARQ-004
+src/…/Library/LibraryViewModel.cs        94  81
-src/…/FileSystem/DebouncedFileWatcher.cs 88  73
+src/…/FileSystem/DebouncedFileWatcher.cs 93  71
```

Así que los otros seis miden **estable en local y distinto en CI**: la diferencia es del **entorno**,
no del reloj. / The other six measure stably here and differently on CI: the difference is the
environment, not the clock.

## El que sí baila, y también está medido / The one that does vary

`DebouncedFileWatcher` es la excepción y tiene causa propia. Dos ejecuciones consecutivas de
`IntegrationTests`, **mismo binario y las mismas 451 pruebas**: / Two consecutive runs of the same
binary and the same 451 tests:

```
tirada 3: DebouncedFileWatcher  lines=93,44 %  branches=69,44 %
tirada 4: DebouncedFileWatcher  lines=86,88 %  branches=66,66 %
```

Un watcher con antirrebote: según lleguen los tiempos, unas ramas corren y otras no. El 73 que la
lista traía era **una tirada afortunada**. / Whether a branch runs depends on how the timers land;
the 73 the list carried was a lucky run.

## Por qué ningún número servía a los dos sitios / Why no single number worked

El trinquete falla **en las dos direcciones**: por debajo del suelo («coverage does not go
backwards») y por encima («raise its floor»). Esa simetría es deliberada y es lo que impide que la
lista mienta. Pero con dos entornos que miden distinto, un suelo de CI hace fallar cada ejecución
local pidiendo subirlo, y un suelo local hace fallar cada ejecución de CI pidiendo bajarlo. **No hay
número que satisfaga a los dos**, así que el problema no era el número: era de dónde salía. / The
ratchet fails both ways by design. With two environments that measure differently, no single number
satisfies both — so the problem was never the number, it was where it came from.

## La corrección / The fix

**El suelo lo pone quien verifica.** Es el mismo principio con el que el ciclo se movió a CI el
2026-08-18, aplicado a la medición en vez de a la ejecución: / The floor belongs to whoever verifies,
the same principle that moved the cycle itself to CI, applied to the measurement:

1. `eng/coverage-debt.txt` se copia del artefacto **`coverage-debt`** de un run de CI, nunca de una
   ejecución local. El flujo lo emite **en cada build, pase o falle** —`-WriteDebt` corre con
   `if: always()` después de las puertas—, así que mover un suelo es copiar una medición y no
   adivinarla.
2. Fuera de CI el trinquete **informa y no bloquea**: dice qué se ha movido y recuerda de dónde sale
   el suelo. Bloquear aquí sólo podía pedir subir suelos que CI no puede alcanzar.
3. **La lista `$watched` y la puerta de archivos nuevos siguen mordiendo en los dos sitios**, porque
   ninguna de las dos varía con el hardware: los catorce vigilados están al 100/100 aquí y allí.

Lo que **no** cambia: el trinquete sigue siendo simétrico, sigue en 219, y un archivo sigue saliendo
de la lista sólo llegando a 96/96. / What does not change: the ratchet stays symmetric, stays at 219,
and a file still leaves the list only by reaching 96/96.

## Lo que queda abierto / What stays open

`DebouncedFileWatcher` sigue midiendo distinto en cada tirada del **mismo** entorno, y eso el anclaje
a CI no lo arregla: lo hará fallar de vez en cuando. Su cobertura tiene que dejar de depender del
reloj —forzando las ramas del antirrebote en vez de esperarlas— y hasta entonces es un rojo conocido
con nombre. / Anchoring to CI does not fix a file that varies within one environment; its coverage
has to stop depending on the clock.
