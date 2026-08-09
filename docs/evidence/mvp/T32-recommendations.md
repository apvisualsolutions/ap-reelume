# T32 — Recomendaciones privadas y explicables / Private, explainable recommendations

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `420d7a4`
- Commit de tarea / Task commit: `feat: recommend titles locally with explanations`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1, SQLite en WAL
- IDs: `UX-006=VERIFIED`, `PRI-001=IN_PROGRESS` (cierra con la auditoría completa / closes with the full audit)

## RED y GREEN / RED and GREEN

`RecommendationPolicyTests`, `GetRecommendationsTests`, `RecommendationBudgetTests` y
`RecommendationsRailTests` se escribieron antes que el modelo, la política, el caso de uso, la
proyección, el ajuste persistente y las dos superficies. RED falló porque no existían
`RecommendationCandidate`, `WatchedTitle`, `RecommendationTaste`, `Recommendation`,
`RecommendationReason`, `RecommendationPolicy`, `IRecommendationReadModel`, `GetRecommendations`,
`RecommendationOptions`, `IRecommendationSettings`, `RecommendationsViewModel`,
`RecommendationSettingsViewModel`, `RecommendationReadModel` ni `StoredRecommendationSettings`. La
salida está en `artifacts/test-results/T32/red/build.log`. / The four test files were written first and
RED failed on every missing type.

Los dos ViewModels de esta tarea tienen prueba desde el ciclo RED. / Both view models were covered
from RED.

GREEN ejecuta **743 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros, Cobertura y
mediciones de rendimiento bajo `artifacts/test-results/T32/green/`.
`dotnet format --verify-no-changes` no informa cambios y ambas compilaciones terminan con 0
advertencias. La suite pasó de **701** a **743**. / GREEN runs 743 tests with zero failures and zero
skips; the suite grew by 42.

## Los pesos aprobados, sin reinterpretar / The approved weights, unreinterpreted

| Señal / Signal | Peso / Weight | Cómo se calcula / How it is computed |
|---|---:|---|
| Géneros / Genres | `0.40` | media de la afinidad de los géneros del candidato, en `[-1, 1]` |
| Reparto / Cast | `0.25` | media de la afinidad de las personas del candidato, en `[-1, 1]` |
| Valoración / Rating affinity | `0.20` | `(valoración − 5,5) / 4,5`, de modo que `10 → +1`, `1 → −1` y `5,5 → 0` |
| Proximidad de año / Year proximity | `0.10` | `1 − |año − año preferido| / 20`, y `0` a partir de veinte años |
| Novedad / Freshness | `0.05` | `1` si no está visto, `0` si lo está |

Los cinco pesos suman exactamente `1.0`, comprobado por prueba. `ScoringModelVersion = 1`, para que un
cambio futuro sea visible y no silencioso. La explicación de cada sugerencia son sus **señales no
nulas ordenadas de mayor a menor peso**, entregadas como códigos que la interfaz traduce. / The five
weights sum to one, the model version is recorded, and the explanation is the non-zero signals,
heaviest first, as codes the UI translates.

## Determinismo / Determinism

| Condición / Condition | Resultado / Result |
|---|---|
| Catálogo nuevo, sin historial | ordena por novedad y lo dice: la única razón es «todavía no lo has visto» |
| Mismo catálogo, dos ejecuciones | idéntico elemento a elemento, incluidas las puntuaciones |
| Mismo catálogo, orden de entrada invertido | idéntico: el desempate es el identificador, en orden ordinal |
| Señal negativa / Negative signal | un género que la persona valoró mal **resta**, no se ignora |
| Sobre SQLite real / On real SQLite | dos ejecuciones seguidas sobre 20 títulos dan el mismo orden y las mismas razones |

## Desactivado significa no calculado / Off means not computed

Con el interruptor apagado, `GetRecommendations` devuelve una lista vacía y **no llama al modelo de
lectura ni una vez**: lo comprueban un contador de lecturas en la prueba unitaria y otro en la de
rendimiento. Medido, una llamada desactivada cuesta **0,16 ms p95** frente a un presupuesto de 1 ms; no
es que el resultado se oculte, es que el trabajo no ocurre. El ajuste se guarda en
`recommendations.enabled` y un proceso nuevo lo relee: apagado sigue apagado tras reiniciar. / Switched
off, the use case reads nothing at all, costs 0.16 ms, and the stored choice survives a restart.

## Presupuesto de rendimiento / Performance budget

| Medida / Measurement | p95 | Presupuesto / Budget |
|---|---:|---:|
| Ordenar 10.000 candidatos / Ranking 10,000 candidates | **36,45 ms** | 200 ms |
| Caso de uso completo sobre 10.000 / Whole use case over 10,000 | **18,84 ms** | 200 ms |
| Llamada desactivada / Disabled call | **0,16 ms** | 1 ms |

Diez repeticiones tras calentamiento para la política, cinco para el caso de uso. Los JSON están en
`artifacts/test-results/T32/green/perf/`. / Ten warmed repetitions for the policy and five for the use
case, with the raw measurements kept as artifacts.

## Cero tráfico / Zero traffic

- **Servidor señuelo**: un `HttpListener` en `127.0.0.1` escucha mientras se calculan 200
  recomendaciones. Su contador de solicitudes termina en **0**.
- **Sin pila HTTP**: ni `ApSolutions.LocalMedia.Application` ni `ApSolutions.LocalMedia.Domain`
  referencian ningún ensamblado cuyo nombre contenga `Http`, ni `System.Net.Sockets`, ni
  `System.Net.Requests`. Comprobado por reflexión sobre los ensamblados compilados.
- **Ningún tipo proveedor**: no existe en `Domain.Personalization` ningún tipo cuyo nombre contenga
  `Http`, `Remote`, `Provider` o `Telemetry`.
- **Nada se serializa**: la biblioteca y el historial se leen como columnas y se convierten en
  afinidades numéricas; no hay JSON, ni carga útil, ni exportación intermedia.

/ A canary listener stays at zero, neither assembly references an HTTP stack, no provider type exists,
and nothing is serialised.

## El resumen de gustos no es un perfil / The taste summary is not a profile

`RecommendationTaste` es un diccionario de afinidades derivado en el momento a partir de lo que esta
máquina ya sabe. No se almacena, no tiene identificador, no se sincroniza y no sobrevive a la consulta
que lo produjo. Marcar algo como visto o valorarlo lo cambia en la siguiente lectura, y borrar esas
marcas lo deshace. `UX-007`, las listas personalizadas, sigue `DEFERRED` y esta tarea no lo toca. /
The taste summary is computed per query, stored nowhere, and carries no identifier.

## Superficies / Surfaces

El rail de Inicio muestra el título, la etiqueta «Por qué» y las razones ya traducidas; con las
recomendaciones apagadas queda vacío y lo dice en palabras. La pantalla de Ajustes ofrece la casilla,
repite el estado en texto y explica que nada sale del equipo. Todos los controles tienen nombre de
automatización y aceptan foco; ningún atributo de texto de las dos vistas es un literal. Las cinco
razones tienen clave en `Strings.es.axaml` **y** en `Strings.en.axaml`, comprobado por prueba. / The
rail explains itself in translated words, the settings screen states the choice in text, and every
reason code has a key in both dictionaries.

## Baseline estructural re-aprobada / Structural baseline re-approved

Inicio ahora incluye el rail, así que la baseline de T30 se regeneró y se volvió a aprobar con un
campo nuevo, `RecommendationsRailVisible`, en las 36 combinaciones. El acceso a Biblioteca sigue dentro
del primer viewport en las 36 y el primer foco sigue siendo Continuar. Una baseline que no siguiera a
la superficie dejaría de proteger nada. / The Home baseline was regenerated and re-approved with the
rail recorded; the library shortcut and the first focus are unchanged.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines | Ramas / Branches |
|---|---:|---:|
| `Domain/Personalization/RecommendationModels.cs` | 39/40 — 97,50 % | 17/18 — 94,44 % |
| `Domain/Personalization/RecommendationPolicy.cs` | 67/67 — 100 % | 26/28 — 92,86 % |
| `Application/Personalization/GetRecommendations.cs` | 20/20 — 100 % | 8/8 — 100 % |
| `Infrastructure/Data/Repositories/RecommendationReadModel.cs` | 43/43 — 100 % | 18/20 — 90,00 % |
| `Presentation/Home/RecommendationsViewModel.cs` | 45/48 — 93,75 % | 9/12 — 75,00 % |
| `Presentation/Settings/RecommendationSettingsViewModel.cs` | 11/11 — 100 % | 6/6 — 100 % |
| `Infrastructure/Settings/StoredRecommendationSettings.cs` | 4/4 — 100 % | 2/2 — 100 % |
| `Presentation/Home/ResourceKeyConverter.cs` | 8/8 — 100 % | 9/12 — 75,00 % |
| **Total del código nuevo / New code total** | **237/241 — 98,34 %** | **95/106 — 89,62 %** |

La política de dominio supera el mínimo de ramas con **92,86 %**. Las ramas no cubiertas del resto son
comprobaciones de nulidad generadas por el compilador. / The domain policy clears the branch minimum;
the rest are compiler-generated null checks.

## Privacidad y límites / Privacy and boundaries

- **Sin telemetría**: ningún archivo de esta tarea emite un evento, escribe un registro remoto ni abre
  un socket.
- **Sin rutas**: el rail muestra títulos; la proyección no lee `normalized_path` en ninguna consulta.
- **Sin operaciones destructivas**: ningún `File.Delete`, `File.Move` ni escritura sobre archivos
  multimedia.
- **Artefactos ignorados**: `git status` no incluye `artifacts/` ni ningún archivo multimedia.
- **Sin datos personales versionados**: ningún archivo tocado contiene nombre de usuario, nombre de
  equipo ni ruta absoluta local; la evidencia no cita ningún título real de la biblioteca.

`UX-006` pasa a `VERIFIED`: las sugerencias se explican, se pueden desactivar —y desactivadas ni se
calculan—, son deterministas y no producen una sola solicitud de red. `PRI-001` sigue `IN_PROGRESS`
hasta la auditoría completa de tráfico del cierre. / The recommendation identifier verifies; the
privacy identifier waits for the full traffic audit.
