# La última deuda de cobertura / The last coverage debt

Evidencia del tercer archivo de **TST-001**, `ReconcileScannedFiles.cs`: la lista de trabajo medida,
las nueve pruebas que la saldan y el suelo que sube detrás. / Evidence for **TST-001**'s third file:
the measured work list, the nine tests that pay it, and the floor that rises behind them.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## El rojo, medido antes de escribir nada / The red, measured before writing anything

Las tres suites que ejecutan este archivo —`Application.Tests`, `IntegrationTests`, `UiTests`— con
cobertura, fusionadas por línea con la misma aritmética de la puerta. El resultado reprodujo
**exactamente** el suelo vigilado, que es lo que demuestra que el subconjunto es el correcto: / The
three suites that execute this file, with coverage, merged per line with the gate's own arithmetic.
The result reproduced the watched floor **exactly**, which is what shows the subset is the right one:

```
lines 85/98 = 86,73 %      branches 38/50 = 76,00 %
```

Trece líneas sin cubrir y doce ramas de menos, en siete decisiones: / Thirteen uncovered lines and
twelve missing branches, in seven decisions:

| Dónde / Where | Qué decide / What it decides | Ramas / Branches |
|---|---|---:|
| 44-48 | las cinco guardas del constructor / the five constructor guards | 5 de 10 |
| 56 | un escaneo cancelado no reconcilia nada / a cancelled scan reconciles nothing | 1 de 2 |
| 75 | un resultado que el escaneo no pudo catalogar / a result the scan could not catalogue | 3 de 4 |
| 84 | una ruta sin fila / a path with no row | 1 de 2 |
| 102 | una identidad que no se puede leer / an identity that cannot be read | 2 de 4 |
| 108 | contenido `Updated`, que refresca la identidad / `Updated` content refreshing the identity | 1 de 2 |
| 193 | un identificador estable sin huella detrás / a stable id with no fingerprint behind it | 1 de 2 |

## Medir recortó la lista escrita a mano / Measuring cut the hand-written list

El plan traía la lista de ramas **leída del código**, y medirla la redujo en un tercio: cinco de sus
puntos ya estaban cubiertos por los recorridos de escaneo. Se anotan porque la próxima lista escrita
por lectura tendrá el mismo sesgo. / The plan carried the branch list **read off the code**, and
measuring it cut it by a third: five of its entries were already covered by the assembled scans.

| Punto del plan / Plan entry | Medido / Measured |
|---|---|
| identidad ya almacenada en una fila no `Updated` | 4/4 |
| un candidato visto en el mismo escaneo (una copia) | 6/6 |
| decisión no exacta o más de un candidato | 4/4 |
| `KeepAsNewAsync` con y sin fila | 2/2 |
| identificador estable que apunta a uno mismo | 4/4 |

Y una línea sin cubrir que ninguna lectura habría encontrado: la del contador `AttemptedCount`.
Ninguna prueba lo leía —las comparaciones de registro entero van por campos, no por propiedades—,
así que el único de los cuatro contadores que nadie miraba nunca era precisamente el que cuenta el
trabajo intentado. / And one uncovered line no reading would have found: the `AttemptedCount`
getter. No test read it — whole-record comparisons go through fields, not properties — so the one
counter nobody ever looked at was the one that counts the work attempted.

## Lo que se escribió / What was written

Nueve pruebas unitarias con dobles en memoria de `IMediaFileRepository` e `IFileIdentityProvider`,
en `tests/ApSolutions.LocalMedia.Application.Tests/Discovery/ReconcileScannedFilesTests.cs`.
`ReconcileScanResults`, `FileReconciliationPolicy` y `PendingReassignments` se construyen de verdad:
la política que decide es la del producto, no una imitación. / Nine unit tests with in-memory
doubles; the three concrete collaborators are built for real, so the policy that decides is the
product's.

Apuntan a **decisiones**, no al camino feliz —que ya recorren los escaneos ensamblados de
`ScanReconciliationTests`, y por eso las líneas estaban al 87 % y las ramas al 76 %—: qué se niega a
tocar (un escaneo cancelado, un resultado omitido o fallido, una ruta sin fila), qué cuenta (una
identidad ilegible cuenta como fallo y el escaneo sigue; una excepción del catálogo cuesta un archivo
y no el escaneo), qué guarda (contenido `Updated` refresca la identidad que describía bytes que ya no
están; un identificador estable que nada más lleva convierte la fila en su propia entidad) y qué no
es un fallo (una cancelación se relanza en vez de contarse). / They aim at the decisions: what it
refuses to touch, what it counts, what it stores, and what is not a failure.

## Verde / Green

```
lines 98/98 = 100 %        branches 50/50 = 100 %
ReconcileScannedFilesTests: 9 de 9 / of 9, 38 ms
```

El suelo de `eng/check-coverage.ps1` sube a **100,00 / 100,00**, que es el trinquete: la puerta falla
si el número medido no se anota, igual que falla si el archivo retrocede. Los tres archivos que
TST-001 nombró el 2026-08-09 quedan al 100 % y vigilados. / The floor in `eng/check-coverage.ps1`
rises to **100.00 / 100.00** — the ratchet: the gate fails if the measured number is not recorded,
just as it fails if the file slips back. All three files TST-001 named are now at 100% and watched.

| Archivo / File | 2026-08-09 | Ahora / Now |
|---|---|---|
| `PlayerVersionsViewModel.cs` | 60,61 % / 27,27 % | 100 % / 100 % |
| `CompositeFileIdentityProvider.cs` | 66,67 % / 16,67 % | 100 % / 100 % |
| `ReconcileScannedFiles.cs` | 86,73 % / 76,00 % | **100 % / 100 %** |

Detalle de las dos primeras en [audit-tst1-coverage-debt.md](audit-tst1-coverage-debt.md); la
calibración original, en [TST1-coverage-gate.md](TST1-coverage-gate.md). / The first two are detailed
in the first document; the original calibration in the second.
