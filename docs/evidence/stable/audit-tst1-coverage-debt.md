# La deuda que nadie vigilaba / The debt nobody was watching

Evidencia de la deuda de **TST-001**: los tres archivos que la calibración dejó por debajo del
umbral, y el guardián que les faltaba. / Evidence for **TST-001**'s debt: the three files the
calibration left below the bar, and the guard they were missing.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## Lo primero fue re-medir, y el resultado no era el esperado / Re-measuring came first

[El documento de TST-001](TST1-coverage-gate.md) daba tres números del 2026-08-09, y se suponía que
estaban caducados y que la situación habría mejorado. Medidos otra vez contra el informe fusionado
de la verificación completa: / They were assumed stale and improved. Measured again:

| Archivo / File | 2026-08-09 | 2026-08-10 |
|---|---|---|
| `ReconcileScannedFiles.cs` | 86,73 % / 76,00 % | 86,73 % / 76,00 % |
| `CompositeFileIdentityProvider.cs` | 66,67 % / 16,67 % | 66,67 % / 16,67 % |
| `PlayerVersionsViewModel.cs` | 60,61 % / 27,27 % | **45,45 % / 14,29 %** |

**Dos idénticos y uno peor.** No estaban caducados: estaban parados. Y el tercero **retrocedió**
—quince puntos de líneas y trece de ramas— porque ARQ-004 le quitó su clase de comando escrita a
mano y las líneas que se fueron eran, en buena parte, las que sí estaban cubiertas. / Two unchanged
and one worse: ARQ-004 removed its hand-written command class, and the lines it took were largely
the covered ones.

Nada avisó. Es la demostración medida del agujero que el plan anticipaba: **la puerta decide qué
vigilar por novedad contra la referencia base**, así que un archivo que ya viajaba y empeora no lo
mira nadie. / Nothing said a word — the measured demonstration of the hole: the gate decides what to
hold by newness, so a file that already shipped and gets worse is held by nobody.

## El rojo / The red

La lista de vigilados, exigiéndoles el umbral del repositorio:

```
Coverage gate: watched files, held at every run whatever their age:

File                                                     LinePct BranchPct Floor Verdict
ReconcileScannedFiles.cs                                   86,73     76,00 96/96 FAIL (below its floor)
CompositeFileIdentityProvider.cs                           66,67     16,67 96/96 FAIL (below its floor)
PlayerVersionsViewModel.cs                                 45,45     14,29 96/96 FAIL (below its floor)

exit 1
```

## El guardián / The guard

`eng/check-coverage.ps1` recibe una lista explícita de vigilados que se miden **en cada ejecución,
sea cual sea su edad**. Cada entrada lleva el suelo que su código cumple hoy, y la regla es la de la
lista de huérfanos de `ServiceConsumptionTests`: / an explicit watchlist, measured on every run
whatever the file's age, each entry carrying the floor its code meets today.

- Por **debajo** del suelo → falla. No puede empeorar. / below its floor → fails.
- Por **encima** del suelo → **también falla**, pidiendo subir el número. Una deuda que se salda en
  silencio puede volver en silencio; anotarla es lo que hace que sólo pueda encoger. / above it →
  also fails, asking for the number to be raised.
- Bajar un suelo no lo puede hacer el guion: es una línea visible en un diff, igual que retirar una
  entrada de la lista de huérfanos. / lowering a floor is a visible line in a diff.

Efecto secundario buscado: la puerta **ya no puede salir sin dientes**. Antes, cuando no había
archivos nuevos —el caso normal en CI, porque `main` avanza en fast-forward con la rama— salía cero
sin leer un solo informe. Ahora siempre mide algo. / A wanted side effect: the gate can no longer
exit toothless.

`CoverageGateTests` fija las dos mitades: que los vigilados estén declarados y **existan** —una
entrada que nombra un archivo inexistente no sostiene nada— y que el trinquete bloquee en los dos
sentidos. / The pinning tests hold both halves.

## La deuda, saldada por donde tocaba empezar / The debt, paid where it had to start

| Archivo / File | Antes / Before | Después / After |
|---|---|---|
| `PlayerVersionsViewModel.cs` | 45,45 % / 14,29 % | **100 % / 100 %** |
| `CompositeFileIdentityProvider.cs` | 66,67 % / 16,67 % | **100 % / 100 %** |
| `ReconcileScannedFiles.cs` | 86,73 % / 76,00 % | 86,73 % / 76,00 %, vigilado / watched |

Lo que faltaba en los dos primeros no era casualidad, y merece decirse porque se repite: **las
pruebas existentes cubrían el cableado y no el contenido**. De `PlayerVersionsViewModel` estaba
comprobado que una fila entrega su versión al caso de uso y que una no disponible no se puede elegir;
la etiqueta que una persona **lee** —resolución, códec, rango— no la tocaba nadie, y ahí vivían todas
sus ramas. De `CompositeFileIdentityProvider` estaban recorridos los escaneos donde las dos mitades
contestan, y no los fallos **para los que la clase existe**: un volumen sin identificadores estables,
un archivo que otro proceso retiene. / What was missing in both was not accidental: the existing
tests covered the wiring and not the content — the label a person reads, and the failures the class
exists for.

Once pruebas nuevas para el primero y doce para el segundo, incluida la que impide que el filtro se
convierta en un tragadero: una excepción que **no** significa «no disponible» tiene que seguir
viajando, o un defecto se convierte en un escaneo silenciosamente incompleto. / Eleven new tests and
twelve, including the one that stops the filter becoming a catch-all.

**Lo que quedaba**: `ReconcileScannedFiles.cs`, 98 líneas y 50 ramas, a 9 puntos de líneas y 20 de
ramas del umbral. Quedó como deuda con nombre y con suelo —que es lo que la hizo visible en cada
ejecución en lugar de una vez al año— y se saldó el mismo día al 100 %/100 %:
[audit-tst1-reconcile-coverage.md](audit-tst1-reconcile-coverage.md). / What was left was named,
floored, and visible on every run; it was paid the same day at 100%/100%.

## Verde / Green

| Suite | Resultado / Result |
|---|---|
| `ApSolutions.LocalMedia.ArchitectureTests` | 20 de 20 / of 20 |
| `ApSolutions.LocalMedia.UiTests` | 405 de 405 / of 405 |
| `ApSolutions.LocalMedia.IntegrationTests` | 419 de 420, 1 omitida por diseño / 1 skipped by design |
| `eng/check-coverage.ps1` | 3 vigilados en su sitio / 3 watched files where they have to be |
