# ENG-016 — La previsualización ve los archivos nuevos antes del commit / The preview sees new files before the commit

Evidencia de `ENG-016`, cerrada el 2026-09-20. Un aviso mudo es peor que ninguno, y éste callaba
exactamente en el momento para el que existe. / Evidence for `ENG-016`, closed on 2026-09-20. A
silent warning is worse than none, and this one went quiet at the very moment it exists for.

## El RED archivado / The archived RED

`eng/preview-coverage-floors.ps1` buscaba los archivos nuevos con un rango de commits:

```powershell
git diff --name-only --diff-filter=A "$BaseRef...HEAD" -- 'src/*.cs'
```

Un rango sólo nombra lo que **ya está en un commit**. Para la puerta eso es correcto —corre en CI,
donde el árbol que mide está commiteado por definición—, pero la previsualización existe para que te
avisen **antes** de commitear, así que su única fuente era ciega justo para su caso de uso. /
A range only names what is already IN a commit. That is right for the gate — it runs in CI, where
the tree it measures is committed by definition — but the preview exists to warn you **before**
committing, so its only source was blind to its own use case.

Medido dos veces antes de hoy: el 2026-09-13, con tres ficheros nuevos sin commitear, dijo «nada se
queda corto» y dos de los tres estaban bajo 96/96; el 2026-09-19 esa misma calma costó un rojo, con
`TitleFramePass.cs` nuevo midiendo 100/50 y el run `35471110732` caído. / Measured twice before
today; the second silence cost run `35471110732`.

## La reproducción, con su control / The reproduction, with its control

Una sonda `.cs` nueva y sin commitear en `src/`, y las dos consultas al lado:

| Consulta / Query | Con la sonda / With probe | Sin ella / Without |
| --- | --- | --- |
| Sólo el rango / Range alone | **0** | 0 |
| La unión de las tres / Union of three | **1** | **0** |

El control de los dos lados importa: una búsqueda que siempre encuentra algo no es una búsqueda. La
sonda se retiró en la misma medición. / The control on both sides matters: a search that always
finds something is no search. The probe was removed in the same measurement.

## La corrección / The fix

La detección se ensancha con dos fuentes más y se extrae a `Get-NewSourceFile`:

```powershell
git diff --name-only --diff-filter=A "$BaseRef...HEAD" -- 'src/*.cs'   # lo commiteado
git diff --name-only --diff-filter=A --cached        -- 'src/*.cs'     # lo preparado
git ls-files --others --exclude-standard             -- 'src/*.cs'     # lo que git no sigue
```

**Se ensancha, nunca se estrecha**: un archivo que falte de esta lista se lee como un archivo que
pasó. Y **`eng/check-coverage.ps1` no se toca**, porque allí el rango es la respuesta correcta. /
**Widen, never narrow**: a file missing from this list reads as a file that passed. The gate is left
alone, because there the range is the right answer.

## Por qué hay una costura, y no sólo cuatro líneas / Why there is a seam rather than four lines

Una prueba que leyera el guion buscando la palabra `ls-files` **seguiría verde mientras la consulta
que nombra no corriera en ningún sitio**, que es la puerta ciega que este repositorio no para de
encontrar —y la que ya estuvo verde semanas en `RootWatchWiringTests`—. Por eso `-ListNewFiles`: da
a la prueba una costura para medir **por efecto**.

Y la escena es un repositorio **de mentira** y no éste, porque preguntando al árbol real la
respuesta cambia con lo que cualquiera tenga sin commitear, así que el caso negativo no se podría
creer nunca. / The scene is a lying repository rather than this one: asked against the real tree the
answer changes with whatever anybody has uncommitted, so the negative case could never be trusted.

## El GREEN / The GREEN

`PreviewCoverageFloorsTests`, cinco casos con su contrario al lado:

| Caso / Case | Espera / Expects |
| --- | --- |
| Archivo nuevo sin seguir / Untracked new file | se lista / listed |
| Archivo nuevo preparado / Staged new file | se lista / listed |
| Archivo nuevo ya commiteado contra la base / Committed against base | se lista / listed |
| Árbol limpio / Clean tree | **nada** / nothing |
| Un `.md` nuevo / A new `.md` | **nada** / nothing |

El tercero es el que nadie echaría de menos: si al ensanchar se hubiera perdido la fuente original,
ninguna otra prueba lo habría notado. / The third is the one nobody would miss: had the original
source been lost while widening, no other test would have noticed.

**Vista fallar antes de darla por buena.** Con el guion mutado a su versión vieja —quitadas las dos
fuentes nuevas, y comprobado antes que el mutante difiere del original—:

```
Con error: 2, Superado: 3, Total: 5
  A_new_file_that_is_staged_but_not_committed_is_listed
  A_new_file_that_git_does_not_track_yet_is_listed
```

Rompe **exactamente** las dos de las fuentes nuevas y deja verdes las otras tres, que es lo que dice
que la prueba mide el arreglo y no otra cosa. Guion restaurado y comparado con la copia. / It breaks
**exactly** the two new-source cases and leaves the other three green. Script restored and diffed
against the copy.

## Las puertas / The gates

| Puerta / Gate | Resultado / Result |
| --- | --- |
| `ArchitectureTests` | **60 de 60** (55 antes de estas cinco) |
| `dotnet format --verify-no-changes` | 0 |
