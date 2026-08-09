# T24 — Pantalla completa, mini reproductor y entradas / Fullscreen, mini player, and input

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `d2ffc49`
- Commit de tarea / Task commit: `feat: preserve playback across windows and input methods`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  dos ASUS ProArt PA279CRV a 2560×1440 con escala 150 %
- IDs: `PLY-007=VERIFIED`, `PLY-014=VERIFIED`, `SYS-001=IN_PROGRESS`, `A11Y-001=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`PlaybackModeTests`, `WindowLifecycleTests`, `PlaybackInputTests` y `MediaKeyTests` se escribieron
antes que el coordinador, el mapa de atajos y el servicio de teclas. RED falló porque
`IAudioOutputTarget`, `PlayerWindowCoordinator`, `ShortcutMap` y `WindowsMediaKeyService` no
existían; la salida se conserva en `artifacts/test-results/T24/red/`. / The four plan-named test
files were written before the coordinator, the shortcut map, and the key service existed; RED is
retained above.

GREEN ejecuta 429 pruebas con 0 fallos y 0 omitidas en `Release`, con TRX, registros y Cobertura bajo
`artifacts/test-results/T24/green/`. La cobertura de líneas del código nuevo es 95,65 % (308/322).
`dotnet format` y ambas compilaciones terminan con 0 advertencias. / GREEN runs 429 tests with zero
failures and zero skips; new-code line coverage is 95.65%.

## El defecto de pantalla completa, corregido / The fullscreen defect, fixed

T18 dejó documentado y trasladado aquí un defecto reproducible: con la ventana forzada a
`WindowState.FullScreen` sobre una pantalla al 150 %, el tamaño de cliente llegaba en píxeles físicos
mientras el render seguía aplicando el factor de escala. El aviso centrado se dibujaba en
`(1920, 1079)` de una captura de 2560×1440 en lugar de en `(1280, 720)`, y la barra de transporte,
anclada abajo, caía cerca de `y≈2070`, fuera de una pantalla de 1440. / T18 recorded and handed over a
reproducible defect: in that window state on a 150% display the client size arrived in physical
pixels while rendering still scaled, so the centred notice landed at 1.5× its intended position and
the bottom-anchored bar fell off the screen.

`PlayerWindowCoordinator` **no usa** `WindowState.FullScreen`. Calcula la geometría como los límites
de la pantalla actual **divididos por el factor de escala**, de modo que el layout y el render
trabajan en las mismas unidades, y quita las decoraciones de ventana. Comprobado:

| Comprobación / Check | Resultado / Result |
|---|---|
| Geometría a 100 %, 150 % y 200 % / Geometry at the three scalings | ancho y alto = límites de pantalla ÷ escala / screen bounds divided by the scaling |
| Barra de transporte a 150 % / Transport bar at 150% | su borde inferior en píxeles físicos queda dentro de los 1440 de la pantalla / inside the display |
| Reproducción del defecto / Defect reproduction | dimensionar en píxeles físicos sitúa el borde inferior en 2160, más allá de la pantalla; dimensionar en unidades lógicas lo deja exactamente en 1440 / physical-pixel sizing overshoots, logical sizing lands exactly |
| Escala cero o negativa / Zero or negative scaling | rechazada, en lugar de producir una ventana infinita / refused |

/ The coordinator sizes fullscreen from the screen bounds divided by the scaling and drops the window
decorations; the table above is what the tests assert.

## Una sola sesión entre los tres modos / One session across the three modes

- `ChangePlaybackMode` cambia de modo **sin reabrir ni detener** el motor: en cien transiciones el
  contador de aperturas, paradas y liberaciones del motor permanece en cero y la posición se conserva
  intacta en cada una.
- Exactamente un modo está en vigor en cada momento; pedir el modo actual no cuenta como transición.
- `PlayerWindowCoordinator` mueve **la misma instancia** de la superficie: tras cien cambios de modo
  el contenido de la ventana sigue siendo el mismo objeto y sólo existe una superficie de vídeo.
- El mini reproductor es `Topmost` y no aparece en la barra de tareas; pantalla completa quita las
  decoraciones y volver a incrustado las restaura.
- La geometría sólo se recuerda **si está visible** en alguna pantalla: una posición fuera de la
  pantalla se descarta en lugar de guardarse y dejar la ventana inalcanzable.

/ Mode changes never reopen or stop the engine, exactly one mode is in force, the same surface
instance moves between windows, the mini player is always on top and out of the taskbar, and only
on-screen geometry is remembered.

## Entradas sin duplicar acciones / Input without duplicated actions

`InputCommandRouter` ejecuta cada comando **una vez**, venga del teclado, del ratón o de una tecla
multimedia. El mismo comando desde un segundo origen dentro de la ventana de coalescencia se
descarta y el origen descartado se registra; comandos distintos nunca se fusionan entre sí; pasada la
ventana, el mismo comando vuelve a ejecutarse. Esto es lo que impide que una tecla multimedia que la
ventana enfocada también recibe como pulsación alterne la reproducción dos veces. / The router
executes each command once whatever produced it, drops a duplicate from a second origin inside the
coalescing window, never merges different commands, and lets the same command run again afterwards.

`ShortcutMap` da un gesto predeterminado a **todas** las acciones esenciales, sin dos comandos
compartiendo gesto. Un reenlace que colisiona se **rechaza** y devuelve el comando que ya lo tiene;
un gesto libre se acepta; los valores iniciales se restauran completos. El editor lo presenta con
nombre de automatización, foco de teclado y un mensaje de conflicto en texto. / Every essential action
has a unique default gesture, a colliding rebind is refused and names its holder, a free gesture is
accepted, defaults restore completely, and the editor is named, focusable, and states conflicts in
text.

## Teclas multimedia / Media keys

El enrutamiento se prueba con una fuente falsa desde `IntegrationTests`, que apunta al marco neutro y
no puede referenciar el host de Windows: una tecla produce exactamente una acción, tecla y atajo para
la misma acción no se suman, cada tecla reclamada mapea a una acción de transporte y, al terminar la
sesión, la fuente se libera y ninguna tecla posterior actúa. / Routing is tested with a fake source
from the neutral-framework suite for the reasons the plan states.

`WindowsMediaKeyService` real se asevera desde `AccessibilityTests`, que sí referencia el host:
registra las teclas al iniciar, mantiene sus registros mientras escucha y **los libera por completo
al parar**, dejando el recuento en cero; iniciar y parar dos veces es idempotente; el servicio reclama
únicamente cuatro teclas de transporte, cada una mapeada a una acción distinta. Las teclas se
registran en un hilo propio con su bomba de mensajes y se liberan en el `finally` de ese hilo, de modo
que un cierre inesperado no las deja retenidas. / The real service is asserted from the suite that can
reference the host: it registers on start, releases everything on stop, is idempotent, and claims only
four transport keys, each on its own thread with a message pump whose finally block releases them.

Si otra aplicación ya tiene una tecla, el registro de esa tecla no se obtiene; el servicio no falla
por ello y la evidencia no lo declara como error, porque es una condición legítima del sistema. /
When another application already holds a key, that registration is simply not obtained; that is a
legitimate system condition rather than a failure.

## Límites y privacidad / Boundaries and privacy

T24 no añade cliente de red ni telemetría. Las teclas multimedia se registran sólo mientras hay
sesión y se liberan al terminar. Ninguna geometría, atajo ni modo se guarda fuera del equipo. Ningún
archivo multimedia se modifica. / T24 adds no network client or telemetry; media keys are held only
while a session exists, nothing is stored off the machine, and no media file is modified.

`PLY-007` pasa a `VERIFIED`: una sola sesión conserva posición y superficie entre incrustado,
pantalla completa y mini, y el defecto de escalado que lo bloqueaba está corregido y guardado con una
prueba. `PLY-014` pasa a `VERIFIED`: teclado, ratón y teclas multimedia resuelven a una acción cada
uno, con atajos reconfigurables sin conflicto. `SYS-001` sigue `IN_PROGRESS` porque sólo cubre las
teclas: bandeja e inicio con Windows son T34. `A11Y-001` sigue `IN_PROGRESS` hasta la auditoría
integral de T33. / The two playback identifiers are verified; the system identifier covers only keys
and the accessibility identifier waits for the end-to-end audit.
