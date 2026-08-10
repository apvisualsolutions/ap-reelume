# Cuánto se espera a la ventana / How long the window keeps you waiting

Línea base de **ARQ-005**: la verificación del paquete deja de informar *si* apareció una ventana y
pasa a informar *cuándo*. / Baseline for **ARQ-005**: the package verification stops reporting
*whether* a window appeared and starts reporting *when*.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## Por qué un sí no sirve / Why a yes is not enough

`Invoke-Application` esperaba a que el proceso publicara una ventana y anotaba `windowShown: True`.
Ese sí cubre por igual un arranque instantáneo y uno que llegó un segundo antes de que expirara el
plazo de 90 s, así que una degradación no se ve hasta que ya es un fallo. / That yes covers both an
instant launch and one that arrived a second before the 90 s deadline, so a degradation is invisible
until it is already a failure.

Se mide desde **antes** de arrancar el proceso, porque el tiempo que el runtime tarda en levantarse
es parte de lo que alguien espera. La ventana se sondea, no se observa, así que la cifra lleva la
resolución del sondeo: **100 ms**. / The clock starts before the process does, because the runtime
coming up is part of the wait. The window is polled, so the figure carries its resolution: 100 ms.

## La línea base / The baseline

Una ejecución de `eng/verify-package.ps1 -Mode Verify` sobre `c4fd20a`, misma máquina, mismos
minutos. / One run, one machine, the same few minutes.

| Fase / Phase | Ventana / Window | Migraciones / Migrations |
|---|---|---|
| `first-launch` | **2292 ms** | 16 |
| `open-with` | **1233 ms** | 16 |
| `repair` | **2658 ms** | 16 |

Las tres se informan juntas a propósito: **un número solo no dice qué midió**. Con tres, la
diferencia entre ellas separa un arranque que empeora de una máquina que iba lenta ese día. / The
three are reported together on purpose: one number alone cannot say what it measured.

## Dos cosas que la tabla desmiente / Two things the table refutes

**Los cinco ciclos migran una base nueva, no sólo el primero.** Cada ciclo recibe su propia carpeta
de datos —que es justo lo que `Each_cycle_ran_against_a_data_folder_of_its_own` exige—, así que cada
uno estrena una base vacía y le aplica las dieciséis migraciones. Contadas en `schema_history` de
cada `cycle-*/data/library.db` al terminar la ejecución: 16, 16, 16, 16 y 17 —la de `downgrade`
lleva una más porque la prueba se la añade a propósito—. La frase «el primer arranque es el único
que migra de verdad», escrita en `NEXT-SESSION` y en la memoria del proyecto, **no se sostiene**. /
**All five cycles migrate a new database, not just the first.** Every cycle gets a data folder of
its own, so every one applies the sixteen migrations. Counted, not assumed.

**Y el primer arranque no es el más lento.** `repair` tarda más, y arranca justo después de que
MakeAppx reescriba 686 archivos. Lo que se está midiendo ahí no es la migración: es un arranque en
frío pagando disco. / **And the first launch is not the slowest one.** `repair` takes longer.

Eso deja al intermitente del 2026-08-10 sin la explicación que se le había puesto. Aquel fallo
agotó **90 000 ms** de plazo; el arranque completo, migración incluida, cuesta **2 292**. Ninguna
parte de un arranque sano se acerca a ese plazo, así que lo que falló allí no fue un arranque lento
sino uno que no llegó a ocurrir. La segunda mitad de ARQ-005 sigue siendo correcta —el hilo que
dibuja no debe estar migrando— pero **no hay que esperar de ella que cierre ese intermitente**. /
That leaves the intermittent failure without the explanation it had been given: it burned 90 000 ms
of deadline, and a whole healthy launch costs 2 292. ARQ-005's second half is still right, but it
should not be expected to close that one.

**Lo que no cambia**: el plazo de 90 s se queda donde está. Subirlo convertiría la única señal que
hay en silencio. / **What does not change**: the 90 s deadline stays.

## El rojo / The red

```
Every_launch_reports_how_long_its_window_took_to_appear(id: "first-launch") [FAIL]
  Phase first-launch says whether a window appeared but not when, so no run of it can be
  compared with the next one.
Every_launch_reports_how_long_its_window_took_to_appear(id: "open-with")    [FAIL]
Every_launch_reports_how_long_its_window_took_to_appear(id: "repair")       [FAIL]

Con error: 3, Superado: 0, Omitido: 0, Total: 3
```

La prueba exige el número y lo acota contra el plazo que la propia fase informa, de modo que un
arranque al filo no puede leerse como uno holgado. / The test demands the number and bounds it
against the deadline the phase reports, so a launch at the edge cannot read as a comfortable one.

## Verde / Green

| Suite | Resultado / Result |
|---|---|
| `ApSolutions.LocalMedia.PackagingTests` | 116 de 116 / of 116 |
