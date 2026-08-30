# El barrido que le da un null a cada constructor / The sweep that hands every constructor a null

Noventa de los doscientos cinco archivos por debajo de 96/96 lo estaban por **una sola forma
repetida**: un `?? throw new ArgumentNullException` en el constructor que ninguna prueba había
ejercido nunca. Perseguirlos de uno en uno habrían sido noventa métodos de prueba diciendo la misma
frase; este barrido la dice una vez y la apunta a un ensamblado entero. / Ninety of the two hundred
and five files short of 96/96 were short of it for **one repeated shape**: a
`?? throw new ArgumentNullException` in the constructor that no test had ever exercised. Chasing them
one file at a time would have been ninety test methods saying the same sentence; this sweep says it
once and points it at a whole assembly.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-30.

## Por qué exactamente 100/50 / Why exactly 100 over 50

Un `throw` que nadie toma no deja una línea sin cubrir: la línea se ejecuta cada vez que se construye
el tipo. Deja **media pareja de ramas**. Por eso **seis** archivos de `Application` medían
`100` de líneas y `50` de ramas clavados, y por eso ninguna lectura de la cobertura de líneas los
señalaba. `StopPlayback.cs` es el caso mínimo del repositorio: dos líneas ejecutables, una rama, y la
cobertura dice `50% (1/2)` en la línea 12. / An untaken `throw` leaves no uncovered line — the line
runs every time the type is built. It leaves **half a branch pair**. That is why eight `Application`
files measured exactly `100` lines and `50` branches, and why no reading of line coverage pointed at
them.

**Seis y no ocho, y la diferencia se cuenta**: el análisis de la décima sesión dijo ocho y la lista
tiene seis —`SetPreferredVersion`, `GetNextEpisode`, `RemoveLibraryRoot`, `IdentifyingScanCoordinator`,
`StartPlayback`, `StopPlayback`—. Y `100/50` no significa lo mismo en todas partes: **69 archivos del
árbol medían ese par**, y los otros 63 son casi todos `.axaml`, que es el número que mide un archivo
de vista por su propia forma. Un par de cifras idéntico no es una causa idéntica. / **Six, not
eight**: the tenth session's analysis said eight and the list holds six. And `100/50` does not mean
the same thing everywhere — **69 files measured that pair**, and 63 of them are `.axaml` views,
where it is what a view file measures by its own shape.

Medido con la suite de `Application` antes y después, sobre el mismo binario:

```
StopPlayback.cs  linea 12  antes:  hits 1  50% (1/2)
StopPlayback.cs  linea 12  despues: hits 4  100% (2/2)
```

## Lo que el barrido hace / What the sweep does

Para cada parámetro que pueda contener un null, `ConstructorGuardSweep` construye el tipo con un
sustituto en **todas** las demás posiciones y un null en ésa, y exige un `ArgumentNullException` que
**nombre ese parámetro**. Cualquier otra cosa —una construcción con éxito, u otra excepción— se
informa, porque una guarda que lanza lo que no es no es una guarda. / For each parameter that could
hold a null, the sweep builds the type with a stand-in in **every** other position and a null in that
one, and requires an `ArgumentNullException` **naming that parameter**. Anything else — a successful
build, or a different exception — is reported, because a guard that throws the wrong thing is not a
guard.

Los sustitutos son `RuntimeHelpers.GetUninitializedObject` para toda clase concreta, y NSubstitute
para interfaces, clases abstractas y delegados. El objeto sin inicializar importa: **no ejecuta una
línea del constructor de ese tipo**, así que medir una guarda no despierta a otro tipo de paso. / The
stand-ins are `RuntimeHelpers.GetUninitializedObject` for every concrete class and NSubstitute for
interfaces, abstract classes and delegates. The uninitialised object matters: it **runs not one line
of that type's constructor**, so measuring one guard does not wake another type up as a side effect.

## Tres exclusiones, todas estructurales / Three exclusions, all structural

Ninguna es una lista, porque una lista es algo que mantener:

| exclusión / exclusion | cómo se reconoce / how it is recognised | por qué / why |
| --- | --- | --- |
| records | el método `<Clone>$` que toda declaración de record recibe | son los portadores de datos de este repositorio y legítimamente no validan |
| excepciones / exceptions | `typeof(Exception).IsAssignableFrom` | su mensaje y su excepción interna son anulables por convención de .NET |
| lo que escribió el compilador / compiler-written | `CompilerGeneratedAttribute` o `GeneratedCodeAttribute` | el `JsonSerializerContext` del generador no es código de este repositorio |

El primer sondeo por reflexión sobre `Application.dll` midió **319** parámetros de referencia, de los
que 129 lanzaban. Los otros 190 eran `InstalledRelease`, `StagedUpdate` y compañía: records de datos.
Sin la exclusión, el barrido habría exigido a doscientos records una validación que ninguno debe
tener. / The first reflection probe over `Application.dll` measured **319** reference parameters, of
which 129 threw. The other 190 were data records. Without the exclusion, the sweep would have
demanded validation of two hundred records that must not have it.

## Lo que encontró / What it found

| ensamblado / assembly | parámetros guardados / guarded | constructores que aceptan un null / accepting a null | sustitutos imposibles / unbuildable |
| --- | --- | --- | --- |
| `Application` | 127 | 7 | 0 |
| `Presentation` | 112 | 0 | 1 → 0 |
| `Infrastructure` | 64 | 1 | 0 |

**303 guardas ejercidas**, ninguna de las cuales lo había sido. `Presentation` pasó a la primera y
por eso **no lleva lista**: inventarle una sería inventar un sitio donde la deuda futura pueda
esconderse. / **303 guards exercised**, none of which had been. `Presentation` passed first time and
therefore **carries no list**: giving it one would be inventing a place for future debt to hide.

### El único sustituto imposible, y lo que enseñó / The one unbuildable stand-in, and what it taught

`RecommendationItemViewModel.ctor(title)` no lanzaba el `ArgumentNullException` de `title` sino uno
que nombraba `source`: el inicializador de campo `[.. recommendation.ReasonCodes.Select(...)]` corría
con un `Recommendation` sin inicializar, cuya lista es null, y LINQ se quejaba primero. El defecto no
era del barrido: `title` estaba declarado `string` **no anulable** y el cuerpo lo trataba con
`title ?? string.Empty`. La firma mentía y ahora dice lo que el cuerpo hace, con lo que el barrido
deja de pedirle una guarda que nunca debió pedirle. / The defect was not the sweep's: `title` was
declared as a **non-nullable** `string` while the body treated it as `title ?? string.Empty`. The
signature lied; it now says what the body does.

### Los ocho que siguen aceptando un null / The eight that still accept a null

Los ocho declaran sus dependencias como **constructor primario**, que no tiene dónde poner la guarda
sin declarar un campo, así que se tragan el null y fallan más tarde en el primer uso: una
`NullReferenceException` desde dentro de un método en lugar de una `ArgumentNullException` desde la
composición que la causó. Van en lista cerrada con su motivo, como la `PendingWiring` de
`ServiceConsumptionTests`, y una segunda prueba por suite fuerza la salida de una entrada en cuanto su
guarda llega. / All eight declare their dependencies as a **primary constructor**, which has nowhere
to put a guard without a field.

`ExecuteRename`, `PreviewRename`, `UndoRename`, `ApplyIdentification`, `RefreshMetadata`,
`RefreshStaleMetadata`, `UpdateMetadata` y `IntegrityChecker`.

## El suelo que impide que se quede ciego / The floor that keeps it from going blind

Cada suite lleva un mínimo de parámetros alcanzados —120, 105 y 60 sobre los 127, 112 y 64
medidos—. La reflexión **se queda muda en vez de roja** cuando deja de casar: un renombrado, un
filtro que ya no acierta o un ensamblado que se parte dejarían el barrido pasando sobre nada. Es el
fallo que más veces se ha medido en este árbol, y por eso el suelo va escrito con el número que se
midió y la fecha en que se midió. / Each suite carries a minimum of parameters reached. Reflection
**goes quiet rather than red** when it stops matching, which is the failure this tree has measured
more often than any other.

## Un rojo del arnés, medido y no atribuido / A harness red, measured and not attributed

La primera pasada completa de `UiTests` con el barrido dentro dio un `Test Case Cleanup Failure` en
`CandidateCardTests.Each_decision_reaches_the_hand_that_was_given_it` —«the calling thread cannot
access this object»— con la traza entera dentro de Avalonia:
`HeadlessUnitTestSession.EnsureIsolatedApplication` → `Compositor..ctor` → `Dispatcher.VerifyAccess`,
sin una sola línea de este repositorio, y sobre una prueba que duró 1 ms y ni llegó a correr. Es la
forma que la casa ya tenía escrita desde el 2026-08-28. / The first full `UiTests` pass with the
sweep in it produced a `Test Case Cleanup Failure` whose entire stack is inside Avalonia, on a test
that lasted 1 ms and never ran.

**No se atribuye al barrido, y eso también se midió**: catorce pasadas de la suite entera, **seis con
el barrido y seis sin él** además de la que falló y una suelta, y las trece restantes en verde. Una
tasa de uno entre catorce no permite decir de quién es, y seis verdes seguidas con el barrido dentro
no permiten decir que sea suyo. / **It is not attributed to the sweep, and that was measured too**:
fourteen passes of the whole suite, **six with the sweep and six without**, and the other thirteen
green. One in fourteen names nobody.

Lo que sí se hizo es bajar la superficie con una razón propia: el barrido corría **una vez por método
de prueba** —tres en dos suites, dos en la tercera— midiendo lo mismo cada vez. Ahora corre una y el
resultado se comparte, porque es reflexión pura sobre un ensamblado y no deja nada vivo detrás.
Menos trabajo concurrente al lado de una sesión headless vale por sí mismo, se llame o no ésa la
causa. / What was done is lower the surface for a reason of its own: the sweep ran **once per test
method**, measuring the same answer each time. It now runs once and the result is shared.

## Lo que esto NO hace / What this does NOT do

No toca `Windows`, aunque doce de sus archivos estén en la deuda: son adaptadores nativos que un
runner hospedado no puede ejercitar, y un suelo medido aquí sería un suelo para una máquina que no
verifica nada. / It does not touch `Windows`, though twelve of its files are in the debt: they are
native adapters a hosted runner cannot exercise.

## Las dos vueltas, y lo que midió la primera / The two runs, and what the first one measured

Subir cobertura cuesta **dos vueltas de CI**: la puerta se pone roja en cuanto un archivo mejora
—pidiendo sacarlo de la lista o subir su suelo—, y la segunda copia el artefacto `coverage-debt` de la
primera y mueve `$debtRatchet`. **El rojo de la primera es el resultado esperado, no un fallo.** /
Raising coverage costs **two CI runs**, and the first one's red is the expected result rather than a
failure.

El run `33309085668`, sobre `899c360`, salió rojo por la puerta de cobertura **y por nada más**: las
suites pasaron enteras, `AccessibilityTests` incluida con sus 146. Lo que dijo:

```
Coverage gate: 205 file(s) still short of 96/96, ratchet 205, 186 measured under the bar, 66 improved.
```

**Sesenta y seis archivos mejoran y diecinueve alcanzan 96/96 y salen**, así que el trinquete baja de
205 a **186**: el mayor movimiento que esta lista ha tenido de una vez. **Ninguno entra**, que es la
otra mitad de la lectura — la puerta exige que la lista sea completa además de exacta, así que un
archivo degradado habría salido nombrado y no hay ninguno. / **Sixty-six files improve and nineteen
reach 96/96 and leave**, so the ratchet comes down from 205 to **186**. **None enter**, which is the
other half of the reading: the gate requires the list to be complete as well as accurate, so a
degraded file would have been named and none was.

**Cuatro de los seis** archivos de `Application` clavados en 100/50 están entre los que salen:
`GetNextEpisode`, `IdentifyingScanCoordinator`, `StartPlayback` y `StopPlayback`.
`SetPreferredVersion` sube a 100/75 y `RemoveLibraryRoot` se queda exactamente donde estaba, porque
sus archivos tienen más ramas que la guarda: el barrido cubre la suya y deja las demás para quien las
mire. / **Four of the six** `Application` files stuck at 100/50 are among those that leave. The other
two have branches beyond the guard.

**El artefacto se copió con sus 186 filas verbatim y convertido a LF**: viene con CRLF y
`.gitattributes` fija `eol=lf`, que es la misma trampa que este árbol ya tenía escrita para
PowerShell. / **The artefact was copied verbatim and converted to LF**: it arrives with CRLF and
`.gitattributes` pins `eol=lf`.
