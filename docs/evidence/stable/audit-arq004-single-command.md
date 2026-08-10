# Veinticuatro maneras de perder un fallo, y una de quedárselo / Twenty-four ways to lose a failure, and one to keep it

Evidencia de **ARQ-004, segunda mitad**: las clases de comando escritas a mano salen y entra una que
captura. / Evidence for **ARQ-004, second half**: the hand-written command classes go and one that
catches arrives.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## El defecto, y por qué no era una elección / The defect, and why it was not a choice

Un `async void` que lanza no devuelve el fallo a quien lo llamó: lo relanza sobre el contexto de
sincronización vigente, que en el hilo de interfaz es la aplicación misma. Así que nunca hubo una
elección entre manejar el fallo y dejarlo pasar. La había entre manejarlo y terminar el proceso. /
An `async void` that throws does not hand the failure back to its caller: it rethrows it on whatever
synchronization context was current. So the choice was never between handling a failure and leaving
it — it was between handling it and ending the process.

| Medición / Measurement | Antes / Before | Después / After |
|---|---:|---:|
| `async void` en `src/` | 27 | **2** |
| De ésos, los que capturan / of those, the ones that catch | 0 | **2** |
| Clases `ICommand` asíncronas escritas a mano / hand-written async `ICommand` classes | 24 | **0** |
| Líneas que eso costaba / lines that cost | — | **−582 / +227** |

Las **13** clases `ICommand` que quedan son síncronas (`Action execute`) y nunca estuvieron en el
alcance: no hay `async void` en ellas que perder. / The **13** remaining `ICommand` classes are
synchronous and were never in scope.

Los dos `async void` que sobreviven son deliberados y ambos capturan: `AsyncRelayCommand.Execute`,
porque un `ICommand` no devuelve tarea que nadie pueda esperar, y `GuardedEvent`, porque un manejador
de evento tampoco. En algún sitio la espera tiene que parar; ahora para dentro de un `catch`. / The
two that survive are deliberate and both catch: somewhere the awaiting has to stop, and it now stops
inside a catch.

## Lo que la migración rompió, y que es lo mejor que pasó / What the migration broke, and it is the best thing that happened

Reemplazar veinticuatro clases por una supone que las veinticuatro hacían lo mismo. Dos no. / Replacing
twenty-four classes with one assumes the twenty-four did the same thing. Two did not.

**`PersonalActionsViewModel`** comprobaba `CanExecute(parameter)` **dentro** de `Execute`, y de ahí
colgaba una validación real: una valoración fuera de uno a diez no llegaba nunca al catálogo. El
comando nuevo ejecutaba sin preguntar, y
`A_rating_outside_one_to_ten_never_reaches_the_host` se puso roja. **Es la corrección, no la
excepción**: nada en `ICommand` promete que quien llama haya consultado `CanExecute` antes — un atajo
de teclado, una vista que no pregunta, o código llamando directamente llegan igual. Ahora
`AsyncRelayCommand.Execute` pregunta primero, y una prueba lo fija. / **`PersonalActionsViewModel`**
checked `CanExecute` inside `Execute`, and a real validation hung off it. The new command ran without
asking and the test went red. Asking first is now the rule, and pinned.

**`TransportControlsViewModel`** no era un comando genérico: llevaba su propia guarda de re-entrada y
encadenaba el resultado del trabajo en el estado. Un salto en vuelo rechaza el siguiente, porque dos
búsquedas simultáneas se miden desde una posición que la primera aún no había alcanzado. Se
reconstruyó **en el ViewModel**, no en el comando: es la regla de esa barra, no la de todos los
botones. / **`TransportControlsViewModel`** was not a generic command — it carried a re-entrancy
guard. It was rebuilt in the view model, because it is that bar's rule and not every button's.

Y una tercera, que se preserva sin defenderla: **`DetectedMarkerReviewViewModel`** levantaba
`CanExecuteChanged` cada vez que ejecutaba, con un `CanExecute` que siempre devuelve verdadero — un
anuncio que nadie puede aprovechar. Una prueba lo fija, así que viaja tal cual y queda señalado. Un
traslado que cambia en silencio lo que hace una superficie no es un traslado. / A third is preserved
without being defended: an announcement nothing can act on. A move that quietly changes what a
surface does is not a move.

## Lo que la puerta de argumentos enseñó / What the argument guard taught

`GuardedEvent.Run` empezó siendo un solo `async void` con su `ArgumentNullException.ThrowIfNull`
dentro. La prueba que le pasa `null` falló, y por la razón correcta: **dentro de un `async void`, una
guarda no es una guarda**. Lanza dentro de la máquina de estados, que la postea al contexto como
cualquier otro fallo, de modo que quien se equivocó nunca se entera. El método está partido en dos
para que la comprobación llegue a quien cometió el error. / Inside an `async void`, a guard clause is
not a guard: it throws into the state machine, which posts it to the context like any other failure.
The method is split in two so the check reaches whoever made the mistake.

## Dónde aterriza un fallo ahora / Where a failure lands now

| Superficie / Surface | Destino / Destination |
|---|---|
| Con estado de fallo (2) / owning failure state (2) | Su propio `FailureKey` / its own `FailureKey` |
| Sin él (22) / without it (22) | `AsyncRelayCommand.LastFailure`, y la red de la primera mitad |
| Manejador de evento (3) / event handler (3) | `GuardedEvent`, y la misma red |
| Lo que llegue arriba igualmente / whatever still reaches the top | `ProcessFailureHandlers` |

Lo que ya no ocurre en ninguno de los cuatro casos es que el fallo termine el proceso. / What no
longer happens in any of the four is the failure ending the process.

## Verde / Green

| Suite | Resultado / Result |
|---|---|
| `CommandFailureTests` (invertida / inverted) | 8 de 8 / of 8 |
| `GuardedEventTests` (nueva / new) | 4 de 4 / of 4 |
| `ApSolutions.LocalMedia.UiTests` | 394 de 394 / of 394 |
| `ApSolutions.LocalMedia.AccessibilityTests` | 73 de 73 / of 73 |
| `ApSolutions.LocalMedia.ArchitectureTests` | 18 de 18 / of 18 |

La prueba que medía el defecto ahora afirma lo contrario, y es la misma prueba: antes, una excepción
sobre el contexto de sincronización; ahora, ninguna, y el fallo donde el comando lo dejó. / The test
that measured the defect now asserts the opposite, and it is the same test.
