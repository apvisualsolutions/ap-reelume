# El anfitrión que suelta lo que toma / The host that lets go of what it takes

Evidencia de **ARQ-001 / WIN-005 / lo que quedaba de BUG-004**: el proveedor de servicios pasa a
tener dueño y a liberarse al salir, y con él LibVLC, SQLite, la bandeja, las teclas del teclado
multimedia y los clientes HTTP. / Evidence for **ARQ-001 / WIN-005 / the rest of BUG-004**: the
service provider gets an owner and is released on exit.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## El defecto, en dos consecuencias / The defect, in two consequences

`CompositionRoot` guardaba el proveedor en `private static IServiceProvider? _services`, y nada lo
liberaba nunca. / `CompositionRoot` kept the provider in a static field that nothing ever released.

1. **El proceso le devolvía a Windows lo que había tomado.** Eso no es un cierre, es una apuesta: un
   registro de tecla global, un icono de bandeja o un reproductor nativo sobreviven a la ventana que
   los creó hasta que el sistema decide recogerlos. / The process handed back what it had taken by
   ending. That is a bet, not a teardown.
2. **Dos aplicaciones no podían existir a la vez en un proceso.** La segunda pisaba los servicios de
   la primera. Esa era la razón real de `DisableParallelization` en `AssembledShellSuites`, y por eso
   retirarlo es la prueba. / Two applications could not exist at once in one process. That was the
   real reason for `DisableParallelization`, which is why removing it is the proof.

## Qué cambió / What changed

| Antes / Before | Después / After |
|---|---|
| `private static IServiceProvider? _services` | `ApplicationHost : IAsyncDisposable`, uno por aplicación |
| `private static PlaybackSessionHooks?` | Estado de sesión del anfitrión / the host's session state |
| `private static NextEpisodeOffer?` | Ídem / likewise |
| `public static string? PendingActivationPath` | Propiedad del anfitrión / a property of the host |
| `CompositionRoot.ConfigureWindow(window)` | `ConfigureWindow(host, services, window)`, sin estado |
| `CompositionRoot.cs`: 1 569 líneas | 1 529 líneas, más `Shell/ApplicationHost.cs` (172) |

## `WindowLifecycle`: se extrajo, se midió y volvió / extracted, measured, returned

ARQ-006 dejó esta extracción pendiente a propósito para no moverla dos veces, y esta tarea la hizo:
`Shell/WindowLifecycle.cs`, 142 líneas, compilando y con los dos recorridos ensamblados en verde.
Entonces la puerta de cobertura la midió. / ARQ-006 left this extraction for this task, and this task
did it. Then the coverage gate measured it.

| Archivo / File | Líneas / Lines | Ramas / Branches | Veredicto / Verdict |
|---|---:|---:|---|
| `Shell/ApplicationHost.cs` | 100,00 % | 100,00 % | PASS |
| `Shell/WindowLifecycle.cs` | 70,89 % | 28,57 % | FAIL |

**Volvió a `CompositionRoot`.** `Attach` resuelve diez servicios del contenedor, de modo que alcanzar
el camino de la bandeja, las dos ramas del cierre y el `catch` de la activación suelta obliga a
entregarle un proveedor fabricado a medida lleno de dobles, y varios de esos servicios son clases
selladas con dependencias propias. Es el mismo veredicto que recibió `WindowsFilePickers` y se le
aplica la misma regla escrita: **una clase se extrae cuando sus pruebas pueden seguirla**. Ésta
todavía no puede, así que se queda donde los recorridos ya la alcanzan, y lo que eso cuesta queda
escrito en vez de escondido. / **It went back.** `Attach` resolves ten services from the container, so
reaching the tray path, both closing branches and the loose-activation catch means handing it a
purpose-built provider full of fakes, several of them sealed classes with dependencies of their own.
Same verdict as `WindowsFilePickers`, same written rule: a class comes out when its tests can follow
it.

Lo que sí quedó, y es lo que la tarea pedía, es que `ConfigureWindow` ya no lea ningún estático:
recibe el anfitrión y el proveedor como argumentos. / What did stay is what the task asked for:
`ConfigureWindow` reads no static; it takes the host and the provider as arguments.

## Qué alcanza la liberación, y qué no / What the release reaches, and what it does not

Disponer del anfitrión dispone del contenedor, que es lo que ejecuta el `Dispose` de cada singleton
que él construyó: el motor de reproducción y su fábrica, el adaptador de salida de audio, el icono de
bandeja, los registros de teclas y los clientes HTTP del actualizador y del proveedor de metadatos. /
Disposing the host disposes the container, which runs every singleton's own release.

**Lo que no alcanza, dicho en voz alta:** la instancia nativa de LibVLC. `LibVlcFactory` mantiene
exactamente una por juego de opciones durante toda la vida del proceso, a propósito, porque crear y
destruir LibVLC repetidamente es un modo de fallo nativo que este repositorio ya conoce. Muere con el
proceso, y decirlo es mejor que insinuar una completitud que no existe. / **What it deliberately does
not reach**: the native LibVLC instance, kept one per option set for the life of the process on
purpose.

**Desviación del plan, con motivo:** el plan decía liberar en `ShutdownRequested`. Se libera en el
`finally` de `Main`, después de que `StartWithClassicDesktopLifetime` devuelve. Es estrictamente más
tarde y más seguro: `ShutdownRequested` se levanta mientras la ventana principal todavía puede estar
vaciando la posición de reproducción, y disponer los servicios ahí sería tirar el suelo mientras
alguien lo pisa. El `finally` cubre además toda salida, incluida la de la bandeja. / **A deviation
from the plan, with a reason**: release happens in `Main`'s `finally`, which is strictly later and
safer than `ShutdownRequested`, and covers every exit path.

## Verde / Green

| Suite | Resultado / Result |
|---|---|
| `ApplicationHostTests` (nueva / new) | 6 de 6 / of 6 |
| `ApSolutions.LocalMedia.AccessibilityTests` | 70 de 70 / of 70, **sin `DisableParallelization`** |
| `ApSolutions.LocalMedia.ArchitectureTests` | 18 de 18 / of 18 |
| `ApSolutions.LocalMedia.UiTests` | 382 de 382 / of 382 |

Las pruebas nuevas dicen exactamente lo que el estático impedía: dos aplicaciones en un proceso
conservan sus servicios, cada una publica el suyo a las superficies que construye, el archivo que una
activación pidió abrir pertenece a la aplicación a la que se lo pidieron, y disponer dos veces no es
un error. / The new tests state exactly what the static field prevented.

## La trampa de la casa, por tercera vez / The house trap, a third time

Las pruebas de cableado y la puerta de consumo leen la composición **como texto**. Al sacar el
arranque de la ventana, cuatro se pusieron rojas sin que cambiara un solo cable —el mismo linaje que
ARQ-006 paso 2 y que la puerta de cobertura—. Ahora `CompositionSourceText` y `CompositionGraph`
leen los `CompositionRoot*.cs` **más** `Shell/ApplicationHost.cs`, y ambos fallan en voz alta si
encuentran menos archivos de los que esperan. / The wiring tests and the consumption gate read the
composition as text; moving the startup out turned four of them red without a wire changing. Both
readers now include the host's file and fail loudly on finding fewer.

Y la puerta de consumo tenía un defecto propio que este cambio destapó: leía el tipo de una instancia
capturada con `new (\w+)`, de modo que un tipo anidado escrito con su calificador —
`new CompositionRoot.ShellHost()`— se registraba como el servicio «CompositionRoot». Ahora lee el
nombre calificado y lo reduce a su nombre propio en los dos lados del grafo, registro y resolución. /
The consumption gate had a defect of its own that this change exposed: a qualified nested type read
as its outer name. It now reads the qualified name and reduces it to the type's own on both sides.
