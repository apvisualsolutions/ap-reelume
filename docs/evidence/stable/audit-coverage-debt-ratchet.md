# La cobertura deja de poder empeorar / Coverage can no longer get worse

**219 archivos** de `src/` están por debajo de 96/96, y desde hoy ninguno puede bajar de donde está.
El número sólo puede encoger. / 219 files sit below the bar, and from today none of them can slip.
The number can only shrink.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número, y lo que costó llegar a él / The number, and what it took

| Medida / Measure | Valor |
|---|---|
| Archivos medidos / Files measured | 369 |
| **Por debajo de 96/96** | **219** |
| Vigilados a su suelo alto / Watched at their high floor | 14 |

```
Coverage gate: 219 file(s) still short of 96/96, ratchet 219.
Coverage gate: 0 new file(s) against origin/main and 14 watched file(s) are where they have to be.
```

**Tres mediciones distintas antes de la buena**, y las tres diferencias enseñan algo:

- **344** leyendo los informes crudos sin filtrar. Contaba código **generado** (`.g.cs`), AXAML y
  puntos de entrada, que no son código que se pruebe.
- **267 → 255** al resolver cada ruta al archivo real de `src/`: los informes traen **dos formatos de
  ruta** —con y sin prefijo de proyecto— y el mismo archivo se estaba contando dos veces, a trozos.
- **219**, el bueno, leyendo el informe **fusionado por `reportgenerator`** que es el que la puerta
  lee, con `-filefilters:-*.g.cs` aplicado.

**La lección, y por eso el guion emite su propia lista:** generar la deuda con una aritmética y
verificarla con otra siempre diverge. El primer intento discrepó en tres archivos porque una ruta
puede aparecer dos veces en el informe fusionado y la comprobación **toma la primera**, mientras que
el generador las fusionaba. Ahora `eng/check-coverage.ps1 -WriteDebt` escribe la lista con la misma
función que después la lee. / A list produced by one arithmetic and verified by another always
drifts; the script now emits its own.

## Cómo funciona / How it works

Tres reglas, y ninguna nueva — son las que este repositorio ya usa:

| Un archivo… | …tiene que |
|---|---|
| que **no** está en `eng/coverage-debt.txt` | cumplir **96/96** |
| que **sí** está | cumplir **su suelo de hoy** |
| que **mejora** | salir de la lista o subir su suelo, **en el mismo cambio** |

Y el trinquete: `$debtRatchet = 219`. Una lista más larga es un fallo. **Se sale de la lista mejorando,
nunca editándola** — un suelo por debajo de lo medido falla igual que uno por encima, así que el
archivo siempre dice lo que es verdad hoy.

Los suelos van **truncados al entero** a propósito: una décima de ruido de medición no debe ser un
rojo, y lo que se quiere impedir es la degradación, no el temblor.

## Por qué esta forma y no «ponerlo todo al 96 %» / Why a ratchet

Poner 219 archivos al 96/96 son meses, y ponerlo delante del rediseño bloquearía todo lo demás. Esta
es **la misma forma que ya funcionó**: `eng/walk-pending.txt` empezó con 126 controles sin pulsar y
llegó a **0** en dieciséis tandas, sin que ninguna tanda fuera un muro. / This is the shape that
already worked: the walk's pending list went from 126 to 0 in sixteen batches.

Y cierra un agujero medido, no imaginado: la puerta decidía «nuevo» contra la rama base, así que **un
archivo antiguo que empeora no lo vigilaba nadie**. `ARQ-004` adelgazó `PlayerVersionsViewModel` y se
lo llevó de 60,61/27,27 a 45,45/14,29 **sin que ninguna puerta dijera una palabra**. Con la lista,
eso es un rojo.

## La puerta muerde, y se comprobó / The gate bites, and that was checked

Bajado a mano el suelo de un archivo de `100 92` a `10 10`:

```
Coverage gate: 219 file(s) still short of 96/96, ratchet 219, 1 improved.
src/ApSolutions.LocalMedia.Application/Backup/BackupContracts.cs now reaches 100/92;
raise its floor in eng/coverage-debt.txt so the debt cannot come back.
```

Dice el número real y qué hacer con él. Restaurado después. / It names the real number and what to do
about it.

## Las puertas / The gates

```
pwsh -File eng/check-coverage.ps1            # 219 pendientes, trinquete 219, 14 vigilados en su sitio
pwsh -File eng/check-coverage.ps1 -WriteDebt # reescribe la lista con la medición del propio guion
```
