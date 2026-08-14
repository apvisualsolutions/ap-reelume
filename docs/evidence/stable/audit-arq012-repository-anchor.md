# Una raíz y un ancla / One root and one anchor

Evidencia de **ARQ-012**: el paseo hacia arriba en busca de la raíz del repositorio estaba pegado en
decenas de archivos de prueba, y ni siquiera era el mismo paseo. / Evidence for **ARQ-012**: the walk
up to the repository root was pasted into dozens of test files, and it was not even the same walk.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-14.

## La medición previa, que corrigió el plan / The measurement, which corrected the plan

El plan decía «una docena de copias del mismo `while`». La regla, escrita antes de tocar nada, contó
lo que había: / The plan said "a dozen copies of the same `while`". The rule, written before touching
anything, counted what was there:

```
archivos que buscan la raíz por su cuenta      59
archivos que nombran el ancla                  56
```

**Cinco veces la estimación.** Y había una segunda cosa que el plan no decía: dos de esas copias
—`RepositoryLayout` en `ArchitectureTests` y en `DocumentationTests`— anclaban en `docs/FEATURES.md`
mientras el resto anclaba en el `.sln`. Este repositorio tenía **dos definiciones de su propia
raíz**. / Five times the estimate, and two of the copies anchored on a different file, so the
repository held two definitions of its own root.

## La corrección / The fix

Un archivo compartido, `tests/Shared/RepositoryLayout.cs` (espacio de nombres
`ApSolutions.LocalMedia.TestSupport`), enlazado en cada proyecto de pruebas desde
`tests/Directory.Build.targets`. Ni proyecto nuevo ni paquete: las pruebas son el único consumidor.
El ancla es el **`.sln`**, porque es la definición de «este checkout»; `docs/FEATURES.md` es
contenido, y un documento que se mueva rompería la localización de la raíz. / One shared file linked
from every test project; the anchor is the solution file, because a document can move.

**−836/+196 líneas** en 88 archivos. La regla queda con la lista que sólo puede encoger que esta casa
ya usa, y con **dos mitades**: nadie más pasea hacia arriba, y nadie más nombra el ancla. La segunda
es la que caza un buscador de raíz escrito de otra forma, porque encontrar la raíz obliga a nombrar
un archivo marcador. / The rule has two halves: nobody else walks up, and nobody else names the
anchor.

## Lo que costó, y por qué se cuenta / What it cost, and why it is written down

La migración se hizo con un guion, y el guion **se llevó el método equivocado en catorce archivos**:
buscaba el primer método con la forma `private static string X()` en vez del método **que contiene el
paseo**. En trece de ellos se llevó `CompositionRootSource()` y dejó las aserciones comparando contra
una ruta; en uno se llevó `Load()`. / The migration script removed the wrong method in fourteen
files, because it matched the first method of the right shape instead of the one containing the walk.

Ninguno de los catorce llegó a un commit, y no por suerte: / None of the fourteen reached a commit,
and not by luck:

- **La compilación cazó dos** (`Load` y los miembros públicos que otros archivos usaban).
- **La regla nueva cazó trece**, porque el paseo seguía allí — la propia puerta que este trabajo
  añade fue la que midió su propio destrozo.
- **Las suites cazaron tres más** de otra clase: métodos que no devolvían la raíz sino
  `…/src/ApSolutions.LocalMedia.Presentation`. Ahí el reemplazo compilaba y era falso.
- Y para no depender de que una prueba mire, se buscó en el diff **toda** devolución de un
  subdirectorio: exactamente esas tres.

La lección, que no es sobre este guion: **un reemplazo mecánico verificado sólo por «compila» es un
cambio sin medir**. Lo que salvó esto fue tener tres redes distintas —compilador, regla nueva, suites
completas— y una búsqueda dirigida en el diff en vez de esperar sentado a que algo fallara. / A
mechanical replacement verified only by "it compiles" is an unmeasured change.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `RepositoryAnchorTests` | 2 de 2 / of 2 |
| `ApSolutions.LocalMedia.ArchitectureTests` | 25 de 25 / of 25 |
| `ApSolutions.LocalMedia.UiTests` | 410 de 410 / of 410 |
| `ApSolutions.LocalMedia.DocumentationTests` | 84 de 84 / of 84 |
| `ApSolutions.LocalMedia.IntegrationTests` | 419 de 420, 1 omitida / 1 skipped |
| `eng/verify.ps1` completo / full | verde / green |

Su **primera** ejecución no lo fue, y por algo ajeno a este cambio: el canario de red de
`GetRecommendationsTests` pedía un puerto de un rango que el sistema tiene reservado. Se corrigió en
su propio commit y con su propia evidencia —[audit-canary-port.md](audit-canary-port.md)—, porque un
rojo que aparece durante un trabajo no es parte de ese trabajo. / Its first run was not green, for a
reason unrelated to this change; it was fixed in its own commit with its own evidence.
