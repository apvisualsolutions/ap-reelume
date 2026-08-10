# TST-001 — Puerta de cobertura automatizada / Automated coverage gate

- Fecha / Date: 2026-08-09
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `dec5ac3`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, reportgenerator 5.5.11
- IDs: `TST-001`
- Plan: [2026-08-08-audit-remediation.md](../../superpowers/plans/2026-08-08-audit-remediation.md) (WP-7)

## Qué se montó / What was built

`eng/check-coverage.ps1`: cada archivo fuente **nuevo respecto a la referencia base**
(`origin/main` por defecto) debe llegar con **≥ 96 % de líneas y ≥ 96 % de ramas** en los
informes Cobertura de la corrida. Los informes se fusionan con `reportgenerator` (declarado en
`.config/dotnet-tools.json`, que `dotnet tool restore` ya restaura en cada verificación, con los
fuentes generados `*.g.cs` excluidos del fusionado). Un archivo nuevo que no aparece en el
fusionado no tiene líneas instrumentables —interfaces y contratos puros— y pasa diciéndolo; un
archivo con código real que ninguna prueba ejecuta aparece con cero ejecuciones y falla con su
nombre. La comparación de novedad es entre árboles (`git diff --diff-filter=A base HEAD`), sin
merge-base, para que el checkout superficial de CI no la rompa; una referencia base inalcanzable
se intenta traer y, si sigue sin existir, la puerta **falla en voz alta** en lugar de pasar
muda. `eng/verify.ps1` la ejecuta como paso bloqueante tras las suites, y
`CoverageGateTests` (ArchitectureTests) fija las tres piezas: el script con sus umbrales, la
invocación bloqueante en la verificación, y la herramienta declarada en el manifiesto. / Every
source file that is new against the base ref must arrive with ≥96% lines and ≥96% branches in
the run's merged Cobertura reports; newness is a tree comparison so CI's shallow checkout cannot
break it, an unreachable base ref fails loudly rather than passing quietly, the merge excludes
generated sources, and an architecture test pins the script, its thresholds, the blocking
invocation, and the declared tool.

**Dónde muerde / Where it bites.** En este repositorio `main` avanza en fast-forward junto a la
rama de trabajo, así que en CI la diferencia suele estar vacía y la puerta lo declara sin coste
(mira el diff antes de exigir informes). Sus dientes están en la verificación **local**, antes
de cada push — el punto del ciclo donde las puertas corren — y en cualquier pull request ajeno,
donde la diferencia es real. / In this repository main advances by fast-forward with the working
branch, so on CI the diff is usually empty and the gate says so at no cost; its teeth are in the
local verification before every push, and on any third-party pull request, where the diff is
real.

## RED

La puerta sin informes de cobertura se niega con nombre y remedio (archivado en
`artifacts/test-results/TST1/red/TST1-red-no-reports.log`): `No Cobertura report found …; run
the test suites with coverage first`, salida 1. / The gate with no coverage reports refuses with
a name and a remedy, exit 1, archived at the path above.

## Calibración contra la historia real / Calibration against real history

Corrida completa de las suites con cobertura (las de empaquetado fallan sin el artefacto
sellado, como declaran; ninguna de ellas cubre los archivos calibrados) y la puerta apuntada a
`797c8cb` — el inicio de la sesión anterior — para que los archivos nuevos de esa sesión
enseñen números reales
(`artifacts/test-results/TST1/red/TST1-calibration-797c8cb.log`): / A full suite run with
coverage and the gate pointed at the previous session's start, so that session's new files show
real numbers:

| Archivo nuevo / New file | Líneas / Lines | Ramas / Branches | Veredicto / Verdict |
|---|---:|---:|---|
| `PendingReassignments.cs` | 100,00 % | 100,00 % | PASS |
| `ReconcileScannedFiles.cs` | 86,73 % | 76,00 % | FAIL |
| `CompositeFileIdentityProvider.cs` | 66,67 % | 16,67 % | FAIL |
| `PlayerVersionsView.axaml.cs` | 100,00 % | 100,00 % | PASS |
| `PlayerVersionsViewModel.cs` | 60,61 % | 27,27 % | FAIL |

> **Actualización del 2026-08-10.** Estos tres se re-midieron, y uno de ellos había **empeorado**
> sin que ninguna puerta lo notara. Dos quedaron al 100 % y el tercero pasó a estar vigilado con
> suelo. Detalle en [audit-tst1-coverage-debt.md](audit-tst1-coverage-debt.md). / **Update:** they
> were re-measured, one had got worse unnoticed, two are now at 100%, and the third is watched.

Los tres rojos son los anticipados por el plan y son **verdaderos**: los paseos de punta a punta
cubren el camino feliz de la reconciliación, la identidad compuesta y el cambio de versión, pero
no sus ramas de error. Esos archivos ya están en `main`, así que quedan fuera del alcance de la
puerta (que sólo mira archivos nuevos en adelante); se nombran aquí como deuda visible en lugar
de bajar el umbral para taparlos. El umbral se queda en 96/96. / The three reds are the ones the
plan anticipated and they are **true**: the end-to-end walks cover the happy paths but not the
error branches. Those files are already on main and therefore outside the gate's reach, which
only holds files that are new from now on; they are named here as visible debt rather than
lowering the threshold to hide them. The threshold stays at 96/96.

## GREEN

Contra `origin/main` la puerta declara que no hay archivo nuevo que sostener y sale 0
(`artifacts/test-results/TST1/green/TST1-green-origin-main.log`). `CoverageGateTests` 3/3,
`dotnet format` sin cambios, compilación `-warnaserror` limpia, DocumentationTests y
`verify-docs` en verde. / Against origin/main the gate declares there is no new file to hold and
exits 0; the pinning tests pass 3/3 and every standard gate is green.

## Privacidad / Privacy

Ni el script ni la evidencia contienen rutas absolutas, nombres de usuario o de equipo; los
registros con rutas de máquina viven bajo `artifacts/`, que Git ignora. / Neither the script nor
this evidence carries absolute paths, user names, or machine names; the logs with machine paths
live under the ignored artifacts tree.
