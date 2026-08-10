# El contenedor se revisa a sí mismo al construirse / The container checks itself as it is built

Evidencia de **ARQ-010**: `ValidateOnBuild = true` en la construcción del contenedor, con una prueba
que lo fija por comportamiento. / Evidence for **ARQ-010**: `ValidateOnBuild = true` where the
container is built, pinned by a behavioural test.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## Qué pregunta responde / What question it answers

No si un registro roto existe, sino **cuándo se oye**. Sin la validación, una dependencia que nadie
registró espera a la primera resolución que la toque, y en una aplicación de escritorio eso significa
una pantalla, delante de alguien, en un rincón que ninguna prueba abrió. Validar al construir mueve
toda esa clase de defecto al arranque, que es un coste que cada prueba de este repositorio ya paga y
que, por tanto, puede informar. / Not whether a broken registration exists, but **when it gets
heard**: at build, or at whichever resolution first happens to touch it — which on the desktop means
a person's screen, in a corner no test opened.

## Qué cambió / What changed

| Antes / Before | Después / After |
|---|---|
| `.BuildServiceProvider(validateScopes: true)`, en línea dentro de `Create` | `ApplicationHost.BuildProvider(services)` |
| `ValidateScopes` únicamente / only | `ValidateScopes` **y / and** `ValidateOnBuild` |

La construcción salió a un método propio por una razón concreta: una prueba puede pasarle una
colección deliberadamente rota **por la misma ruta que usa el producto**. Afirmar sobre una copia de
las opciones sólo demostraría la copia. / The build moved into its own method for one concrete
reason: a test can hand it a deliberately broken collection through the very path the product uses.
Asserting on a copy of the options would only prove the copy.

## El rojo, archivado / The red, archived

Primero se extrajo `BuildProvider` **con el comportamiento de entonces**, para que el rojo fuese de
conducta y no de compilación. / `BuildProvider` was extracted with the then-current behaviour first,
so the red would be behavioural rather than a compile error.

```
ApplicationHostTests.A_registration_whose_dependency_nobody_registered_fails_at_build_not_at_resolution [FAIL]
  Assert.Throws() Failure: No exception was thrown
  Expected: typeof(System.AggregateException)

Con error: 1, Superado: 8, Total: 9
```

Después, una línea: `ValidateOnBuild = true`. / Then one line.

## El límite, medido y dicho en voz alta / The limit, measured and said out loud

`ValidateOnBuild` sólo puede revisar los descriptores cuyo tipo de implementación conoce. Una lambda
es opaca: el contenedor no puede saber qué resolverá dentro hasta que la ejecute. Así que decir «ahora
todo registro roto sale al arranque» sería falso, y éste es el reparto real de la composición del
producto. / `ValidateOnBuild` can only check descriptors whose implementation type it knows. A factory
lambda is opaque, so the claim "every broken registration now surfaces at startup" would be false.

| Forma del registro / Registration shape | Cuántos / How many | ¿Validado? / Validated? |
|---|---:|---|
| Por tipo / By type | 109 | Sí / yes |
| Por factoría / By factory | 45 | No |
| Por instancia / By instance | 2 | No hace falta / not needed |
| **Total** | **156** | **69,9 %** |

Los 45 por factoría siguen sin red y **no** están en el alcance de ARQ-010. Quedan escritos aquí para
que nadie deduzca del verde una cobertura que no existe. / The 45 factory registrations remain
uncovered and are **not** in ARQ-010's scope; they are written down so nobody reads a completeness
into the green that is not there.

## El coste / The cost

Cada `ApplicationHost.Create` paga la validación. Mejor de cinco, misma colección, construida de las
dos formas: / Every `ApplicationHost.Create` pays for it. Best of five, same collection, built both
ways:

| Construcción / Build | Milisegundos / Milliseconds |
|---|---:|
| `ValidateScopes` solo / only | 0,03 |
| `ValidateScopes` + `ValidateOnBuild` | 0,25 |

**+0,22 ms** por contenedor. No es un presupuesto que haya que vigilar. / **+0.22 ms** per container.
Not a budget anybody needs to watch.

## Lo que el plan esperaba y no ocurrió / What the plan expected and did not happen

El plan anotaba que ARQ-010 «destapará registros rotos, y ésa es la señal barata que justifica hacerla
primero». **No destapó ninguno.** Las tres suites que construyen o leen la composición pasaron a la
primera. / The plan expected ARQ-010 to expose broken registrations. **It exposed none.**

| Suite | Resultado / Result |
|---|---|
| `ApSolutions.LocalMedia.AccessibilityTests` | 73 de 73 / of 73 |
| `ApSolutions.LocalMedia.ArchitectureTests` | 18 de 18 / of 18 |
| `ApSolutions.LocalMedia.UiTests` | 382 de 382 / of 382 |

Eso es un resultado, no una decepción: dice que los 109 registros por tipo estaban sanos antes de
tocarlos. Lo que ARQ-010 compra de verdad es una red **permanente** para lo que viene — ARQ-004 y
ARQ-005 mueven arranque y comandos, y un registro roto por esos movimientos ahora aparece en el
arranque de cada prueba en vez de en la resolución que tuviera la mala suerte de tocarlo. / That is a
result, not a disappointment: the 109 type-based registrations were healthy before anyone touched
them. What ARQ-010 actually buys is a **permanent** net for what comes next.

## Las tres pruebas que lo fijan / The three tests that pin it

En `ApplicationHostTests`, y las tres van por `BuildProvider`, nunca por una copia de las opciones. /
In `ApplicationHostTests`, all three through `BuildProvider` rather than a copy of the options.

1. Una dependencia que nadie registró hace fallar la **construcción**, y el mensaje nombra el tipo que
   falta. / A dependency nobody registered fails the **build**, naming the missing type.
2. `ValidateScopes` sigue encendido: un servicio con ámbito resuelto desde la raíz se rechaza. Se fija
   aquí porque ahora viaja en el mismo objeto de opciones que el nuevo, y una extracción puede
   perderlo en silencio. / `ValidateScopes` is still on, pinned here because it now travels in the
   same options object and an extraction can drop it silently.
3. La aplicación que el producto envía sobrevive a esa revisión, cada vez que cualquier prueba la
   construye. / The application the product ships survives that check, every time any test builds it.
