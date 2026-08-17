# La copia que se para a medias / The copy stopped halfway

«Cancelar» de las copias de seguridad, pulsado con el ratón **mientras la copia está corriendo**, que
es el único momento en que ese botón existe. / The backup surface's Cancel, pressed with the mouse
while the copy is running, which is the only moment that button exists.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 95 | **96** |
| Pendientes / Pending | 33 | **32** |

```
The walk: 129 declared command controls in 128 identities; 96 pressed, 32 pending.
```

## Esta tanda es una medición, no una corrección / This batch is a measurement, not a correction

No hay defecto de producto: el control funcionaba y **nadie podía pulsarlo** porque con una biblioteca
de prueba la copia entera acaba en **51 ms**. Lo que había que averiguar era si existe una biblioteca
honesta que tarde lo suficiente. / There is no product defect here: the control worked and nobody
could press it, because with a test-sized library the whole copy is over in 51 ms. What had to be
found out was whether an honest library exists that takes long enough.

## Las dos palancas, medidas en el orden decidido / Both levers, measured in the decided order

Cronometrando `CreateBackup` en esta máquina: / Timing `CreateBackup` on this machine:

| Siembra / Seeding | BD / DB | Copia / Copy |
|---|---|---|
| 1.000 filas / rows | 1.240 KiB | **51 ms** |
| 10.000 filas / rows | 9.052 KiB | **147 ms** |
| 50.000 filas / rows | 43.980 KiB | **1.159 ms** |
| 50.000 filas + 200 × 1 MiB | 43.880 KiB | **1.650 ms** |
| 2.000 × 100 KiB | 1.240 KiB | **1.694 ms** |
| 4.000 × 100 KiB | 1.236 KiB | **3.059 ms** |
| 6.000 × 100 KiB | 1.248 KiB | **4.377 ms** |
| **6.000 × 50 KiB** | 1.244 KiB | **3.944 ms** |
| 8.000 × 100 KiB | 1.236 KiB | **5.892 ms** |

**El catálogo llega al segundo sólo con 50.000 filas**, y esa palanca es cara: sube la base a 44 MB y
mete en la escena un catálogo que no es de lo que la escena trata. **El coste por archivo pesa más que
el coste por megabyte**, así que la palanca es **cuántas imágenes hay**, no cuánto pesan: 6.000 de
50 KiB compran 3.944 ms con 293 MB de siembra, donde 6.000 de 100 KiB compran 4.377 ms con el doble de
disco. / The catalogue only reaches a second at 50,000 rows, and that lever is the expensive one. The
cost per file outweighs the cost per megabyte, so the lever is the **count**, not the size.

**Y la siembra elegida es honesta**: 50 KiB es lo que pesa un póster de 300×450, y 6.000 archivos son
3.000 títulos identificados con póster y fondo. Eso es una biblioteca de alguien, no un número
inventado para que tarde. / The seeding chosen is an honest one: 50 KiB is what a 300×450 poster
weighs, and 6,000 files is 3,000 identified titles with a poster and a backdrop each.

## Lo que no se hizo, y estaba decidido de antemano / What was not done, and was decided beforehand

**No se metió un gancho en la composición para que la copia tarde.** En el actualizador la fuente
lenta es del arnés y **no toca al producto**; aquí no existe esa figura, así que un gancho sería
cambiar el producto para poder probarlo. La copia tarda porque hay algo que copiar. / No hook was put
in the composition to make the copy slow: in the updater the slow source belongs to the harness and
leaves the product untouched, and here no such thing exists — a hook would be changing the product in
order to test it. The copy takes time because there is something to copy.

## La ventana / The window

| Medida / Measure | Valor / Value |
|---|---|
| La copia / The copy | **3.944 ms** |
| Lo que consumen las dos pulsaciones / Spent by both presses | **1.211 ms** |
| Presupuesto de reintentos de `PressAsync` / Its retry budget | 8 × ~300 ms = **2.400 ms** |
| 1.211 + 2.400 | **3.611 ms**, dentro / inside |

El consumo es **cota superior**: el cronómetro arranca antes de la pulsación que inicia la copia, así
que cuenta también el clic al lado de «Crear una copia», que ocurre **antes** de que haya ventana. La
escena **afirma ese número** en vez de sólo escribirlo, y si algún día no cabe lo dice con los
milisegundos gastados y con qué hacer: sembrar más. / The consumption is an upper bound — the
stopwatch starts before the press that starts the copy — and the scene asserts the number rather than
only writing it down.

**En CI la ventana crece y el consumo no.** El consumo son esperas de tiempo real fijas; la copia
depende del disco, que en un runner es más lento. Por eso la medida local es el caso peor. / On CI the
window grows and the consumption does not: the consumption is fixed real-time waits, while the copy
depends on a disk that is slower there. The local measurement is the worst case.

## Las sondas / The probes

- **El estado es el efecto**, igual que en el «Cancelar» del actualizador: entre pulsar y que la copia
  se pare no hay transitorio del que una sonda pueda fiarse. / The status is the effect, as with the
  updater's Cancel.
- **Lo que un estado no puede decir es que no quedó nada**, así que después se cuenta la carpeta de
  copias: **cero**. Una ejecución que publicara una copia y luego dijera «cancelada» se vería idéntica
  desde la pantalla. / A status cannot say that nothing was left behind, so the backups folder is
  counted afterwards: zero.
- **La pulsación de «Crear una copia» usa el estado a propósito**, que es la sonda equivocada para una
  pulsación que tiene que aterrizar y la correcta para una cuyo único propósito es que ahora hay algo
  corriendo. / The Create press uses the status on purpose.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn          # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
eng/run-accessibility.ps1 -Mode Verify -Passes 2           # 108 + 108, 0 críticos / 0 critical
eng/check-walk-coverage.ps1                                # 96 pulsados, 32 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```

La escalera de mediciones se tomó con un arnés desechable que cronometraba `CreateBackup` sobre raíces
de datos recién creadas, y que **no se conserva**: lo que queda de él son los números de arriba y la
siembra que la escena declara. / The ladder was taken with a throwaway harness timing `CreateBackup`
over fresh data roots; it is not kept, and what remains of it is the numbers above and the seeding the
scene declares.
