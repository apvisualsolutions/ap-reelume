# La capa Domain, y las tres ramas que nadie puede tomar / The Domain layer, and the three branches nobody can take

Los nueve archivos de `Domain` que estaban por debajo de 96/96 se llevan al listón, salvo dos que
tienen **techo medido**. Y el techo no se supone: se leyó del IL, del dominio de entradas de un
patrón y de una excepción provocada. / The nine `Domain` files below 96/96 are brought to the bar,
except two with a **measured ceiling**. And the ceiling is not assumed: it was read off the IL, off
the input domain of a pattern, and off a provoked exception.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-30.

## De dónde salen los números / Where the numbers come from

Los suelos los mide CI, así que los de partida **no se calcularon aquí**: se descargó el artefacto
`test-results` del run 33272085794 —el verde de `c5239b1`—, se fusionaron sus **20 informes Cobertura**
con el mismo `reportgenerator` que usa la puerta, y se leyeron con la aritmética de
`eng/check-coverage.ps1`. Los nueve suelos salieron **idénticos** a los de `eng/coverage-debt.txt`,
que es lo que dice que el instrumento mide lo mismo que la puerta. / CI measures the floors, so the
starting ones were **not computed here**: the `test-results` artefact of run 33272085794 — the green
one for `c5239b1` — was downloaded, its **20 Cobertura reports** merged with the same
`reportgenerator` the gate uses, and read with `eng/check-coverage.ps1`'s own arithmetic. All nine
floors came out **identical** to those in `eng/coverage-debt.txt`, which is what says the instrument
measures what the gate measures.

Medir sólo `Domain.Tests` habría mentido: `MatchModels` da 5 de 8 ramas ahí y **7 de 8** en la
fusión, porque otras suites cubren lo que a ésta le falta. La lista de trabajo salió de la fusión. /
Measuring `Domain.Tests` alone would have lied: `MatchModels` reads 5 of 8 branches there and **7 of
8** merged, because other suites cover what this one does not. The work list came from the merge.

## Lo que faltaba, y lo que queda / What was missing, and what is left

```
                            suelo/floor    despues/after   sale? / leaves?
RenameOperation.cs             95/ 50       100/100        si / yes
RootRemapPolicy.cs            100/ 90       100/100        si / yes
MetadataMergePolicy.cs        100/ 92       100/100        si / yes
RecommendationPolicy.cs       100/ 92       100/100        si / yes
RecommendationModels.cs        97/ 94       100/100        si / yes
IMetadataProvider.cs           93/100       100/100        si / yes
MediaNameParser.cs            100/ 91       100/ 98        si / yes   (69 de 70 / of 70)
MatchModels.cs                100/ 80       100/ 90        no        (9 de 10 / of 10)
SegmentDetectionPolicy.cs     100/ 92       100/ 92        no        (13 de 14 / of 14)
```

Eran **15 ramas y 4 líneas**. Se cierran 12 ramas y las 4 líneas; las 3 restantes no las puede tomar
ninguna entrada. / It was **15 branches and 4 lines**. Twelve branches and all four lines close; the
remaining three cannot be taken by any input.

## Las tres inalcanzables / The three unreachable ones

Se identificaron por el JSON de coverlet, que sí nombra la rama —línea, offset y camino— donde
Cobertura sólo dice «3 de 4». / They were identified from coverlet's JSON, which does name the branch
— line, offset and path — where Cobertura only says "3 of 4".

**`SegmentDetectionPolicy.MergeDetections`, línea 32, offset 139.** El IL en ese offset es
`dup; brtrue.s`: el caché del delegado del `GroupBy`. Pero ese campo de caché vive en la **clase de
cierre** que el método construye para capturar `durations`, y se instancia **una por llamada**, así
que nace nulo y el salto no se toma nunca. Diez llamadas, camino 0 diez veces, camino 1 ninguna. /
The IL at that offset is `dup; brtrue.s`: the `GroupBy` lambda's delegate cache. But that cache field
lives on the **closure class** the method builds to capture `durations`, and a fresh one is allocated
**per call**, so it is born null and the jump is never taken. Ten calls, path 0 ten times, path 1
never.

**`MatchModels.ForFile`, línea 31, offset 69.** Es el brazo vacío de `relative is "." or ""`. Lo
único que produciría una ruta relativa vacía es un nombre de archivo sin carpeta, cuyo directorio es
`""` — y `Path.GetRelativePath` **lanza** con una ruta vacía antes de llegar al patrón. Medido: /
It is the empty arm of `relative is "." or ""`. The only thing that would produce an empty relative
path is a bare file name, whose directory is `""` — and `Path.GetRelativePath` **throws** on an empty
path before the pattern is reached. Measured:

```
path='movie.mkv'          dir=''         relative=LANZA / THROWS
path='D:\'                dir=<null>     relative=<no evaluado / not evaluated>
path='D:\Media\a.mkv'     dir='D:\Media' relative='.'
```

**`MediaNameParser.IsValidSeason`, línea 166, offset 2.** Es `value >= 0` siendo falso, o sea una
temporada negativa. Los dos llamadores pasan `ParseNumber` sobre un grupo que los tres patrones
escriben como `\d{1,3}`, así que el valor está siempre en [0, 999]. / It is `value >= 0` being false,
that is, a negative season. Both callers pass `ParseNumber` over a group every pattern writes as
`\d{1,3}`, so the value is always in [0, 999].

Las tres quedan escritas **en la prueba que se buscará la próxima vez**, no sólo aquí. / All three are
written **into the test someone will look at next time**, not only here.

## Dos hallazgos que no son cobertura / Two findings that are not coverage

**Una raíz de unidad no se remapea, y nadie lo dice.** `RootRemapPolicy.Normalize` conserva a
propósito la barra de `"D:\"`, porque `"D:"` en Windows es el directorio actual de esa unidad y no su
raíz. Pero `IsUnder` pregunta si la ruta empieza por `root + '\'`, que para esa raíz es `"D:\\"`, y
ninguna ruta real empieza así. El efecto: `Resolve` devuelve la decisión como `Remapped` —la interfaz
dice que la biblioteca se movió— y `Rewrite` deja cada ruta apuntando al disco viejo. Sin error. Hay
un segundo defecto encadenado en `RootRemapPolicy.cs:107`: con `OldPath` de 3 caracteres el resto
llega sin barra, así que `"F:\library" + "film.mkv"` daría `"F:\libraryfilm.mkv"`. **Se dejó fuera de
esta tanda a propósito**, con su propio rojo pendiente: la prueba
`A_drive_root_keeps_the_separator_that_makes_it_a_root` llega hasta la decisión y su comentario dice
por qué no sigue. Afirmar ahí la respuesta de hoy sería escribir el defecto como contrato. / A drive
root is never remapped and nothing says so. `Normalize` deliberately keeps the separator of `"D:\"`,
because `"D:"` on Windows is that drive's current directory, not its root. But `IsUnder` asks whether
the path starts with `root + '\'`, which for that root is `"D:\\"`, and no real path does. `Resolve`
returns the decision as `Remapped` while `Rewrite` leaves every path on the old disk. Silently. A
second defect is chained at `RootRemapPolicy.cs:107`. **Left out of this batch on purpose**, with its
red pending.

**Dos campos que se rellenan y nadie lee** — el defecto de la casa. `MetadataSearchResult.Language` y
`MetadataDetails.Language` los escribe `TmdbMetadataProvider` en cada respuesta
(`ParseSearchResults(payload, kind, requestedLanguage)` y `ParseDetails(payload, reference,
requestedLanguage)`), y ninguna lectura existe en `src/`. La asimetría importa: `SqliteMetadataCache`
indexa por el idioma **pedido**, no por el que contestó, así que una consulta que cayó al idioma de
reserva se guarda bajo un idioma que nadie recibió. `WatchedTitle.Id` es el tercero: `Summarize` sólo
lee géneros, reparto, nota y año. No se han borrado —son contrato de un puerto y tocarlos alcanza a
Infrastructure—, pero quedan nombrados. / Two fields written and never read — the house defect.
`TmdbMetadataProvider` fills `Language` on every answer and nothing in `src/` reads it back, while
the cache keys on the **requested** language: a lookup that fell through to its fallback is stored
under a language nobody got. `WatchedTitle.Id` is the third. Not deleted — they are a port's
contract — but named.

## Cómo se verificó / How it was verified

583 pruebas en `Domain.Tests`, 24 más que las 559 de partida, todas verdes; `ArchitectureTests` 30 y
`DocumentationTests` 91, verdes; `dotnet format --verify-no-changes --severity warn` y
`dotnet build -c Release -warnaserror` sin una advertencia. Los suelos nuevos los escribe CI: este
árbol no toca `eng/coverage-debt.txt`. / 583 tests in `Domain.Tests`, 24 more than the starting 559,
all green; `ArchitectureTests` 30 and `DocumentationTests` 91, green; `dotnet format` and
`dotnet build -warnaserror` without a warning. CI writes the new floors: this tree does not touch
`eng/coverage-debt.txt`.
