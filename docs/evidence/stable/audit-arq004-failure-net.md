# Un fallo tiene dónde caer / A failure has somewhere to land

Evidencia de **ARQ-004, primera mitad**: la red de último recurso y el registro de fallos de sesión,
que es lo que el comando único necesitaba antes de poder existir. / Evidence for **ARQ-004, first
half**: the handler of last resort and the session failure log — what the single command needed
before it could exist.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## Lo que se midió antes de diseñar / What was measured before designing

El plan pedía medir los sitios antes de inventar nada. Esto es lo que había. / The plan asked for the
sites to be measured before anything was invented. This is what was there.

| Medición / Measurement | Número / Number |
|---|---:|
| `async void Execute` en superficies de comando / on command surfaces | 24 |
| De ésos, los que capturan algo / of those, the ones catching anything | **0** |
| Clases de comando declaradas a mano / hand-written command classes | 38 |
| Formas distintas de constructor entre ellas / distinct constructor shapes among them | 6 |
| Superficies con estado de fallo (`FailureKey`) / surfaces owning failure state | **2 de 24 / of 24** |

Esa última fila es la que cambió el diseño. El plan fijaba que «un fallo aterriza en el estado de
error de su propia superficie», y veintidós de las veinticuatro superficies no tienen ninguno. Sin
resolver eso, capturar la excepción sólo sería una forma más silenciosa de perderla. / That last row
changed the design: twenty-two of the twenty-four surfaces own no error state, so catching without
somewhere to put it would only be a quieter way of losing it.

## Y una medición que no se esperaba / And one measurement nobody expected

El informe de diagnóstico se construía desde **una sola fuente**: la auditoría de renombrados que la
base de datos guarda. Cualquier otro fallo de la aplicación —un comando, una tarea sin dueño, lo que
llegara a lo alto del proceso— no aparecía en ninguna parte. En una sesión donde nadie renombra nada,
el informe llegaba con la lista de errores vacía, de modo que **una aplicación que fallaba parecía una
aplicación sana**. / The diagnostics report was built from **one source**: the rename audit. Every
other failure appeared nowhere, so in a session where nobody renamed anything the report arrived with
an empty error list — an application that was failing looked like a healthy one.

| Fuentes del informe / Report sources | Antes / Before | Después / After |
|---|---:|---:|
| Auditoría de renombrados / rename audit | 1 | 1 |
| Fallos de esta sesión / this session's failures | 0 | 1 |

## Qué se construyó / What was built

**`ISessionFailureLog`** (`Application/Privacy`) y **`InMemorySessionFailureLog`**
(`Infrastructure/Privacy`). En memoria, una instancia por aplicación, acotada a **32 códigos
distintos**, y contando repeticiones en vez de acumularlas. / In memory, one per application, capped
at **32 distinct codes**, counting repeats rather than stacking them.

**Por qué en memoria y no en un archivo**, que era la alternativa real: un registro de fallos en disco
es exactamente la clase de archivo que crece en silencio en la máquina de alguien y sobrevive al
motivo por el que se escribió, y este proyecto no tiene una historia para borrarlo. El informe que lo
lee ya pide consentimiento antes de construirse. / **Why memory and not a file**: a failure log on
disk is the kind of file that grows quietly on somebody's machine and outlives the reason it was
written, and this project has no story for deleting one.

**`ProcessFailureHandlers`** (`Windows/Shell`), instalado en `Program.Main` y retirado al salir.
Engancha `AppDomain.UnhandledException` y `TaskScheduler.UnobservedTaskException`. No es estático:
ARQ-001 gastó la paciencia de este repositorio en exactamente un campo estático que nada podía
liberar. / Installed in `Program.Main` and taken back off on the way out. Not static — ARQ-001 spent
this repository's patience on exactly one static field nothing could release.

## Lo que nunca viaja / What never travels

Los manejadores **no formatean ni una línea propia**. Registran un código que la clase posee y la
excepción tal cual; el saneado es el que ya existía y estaba probado. Un manejador que construyera su
propio texto sería el único sitio al que la lista blanca no llega. / The handlers **format no line of
their own**: they record a code the class owns and the exception itself, and the sanitizing is the one
that already existed.

| Qué / What | Cómo sale / How it comes out |
|---|---|
| Código / Code | `DiagnosticsAllowlist.SanitizeMessage` |
| Excepción / Exception | `DiagnosticsAllowlist.Sanitize` — cadena de tipos, mensaje descartado entero |
| Repeticiones / Occurrences | `DiagnosticsAllowlist.Bucket` |

La prueba que lo fija registra un fallo cuyo mensaje lleva una ruta con el título de una película,
construye el informe entero y lo serializa: ni el título ni la extensión aparecen en el resultado. /
The pinning test records a failure whose message carries a path with a film's title, builds the whole
report and serializes it: neither the title nor the extension appears.

## El defecto, todavía en pie y ahora fijado como tal / The defect, still standing and now pinned as such

`CommandFailureTests` mide lo que la aplicación hace **hoy**: la excepción sale entera del comando y
aterriza en el contexto de sincronización, que en la aplicación real es el hilo de interfaz. Está en
verde porque el defecto es real. La segunda mitad de ARQ-004 la invierte. / `CommandFailureTests`
measures what the application does **today** and is green because the defect is real. ARQ-004's second
half inverts it.

**Por qué la red va primero**, y es una corrección al orden que parecía obvio: `AppDomain.
UnhandledException` **no impide** que el proceso termine, sólo deja constancia. Así que un comando no
puede permitirse dejar escapar nada, y por tanto tiene que capturar siempre — y si captura siempre,
necesita un destino siempre. Ese destino es esto. / **Why the net comes first**:
`AppDomain.UnhandledException` does **not** stop the process from ending, it only records. So a
command cannot afford to let anything escape, which means it must always catch — and something that
always catches always needs somewhere to put it.

## Verde / Green

| Suite | Resultado / Result |
|---|---|
| `SessionFailureLogTests` (nueva / new) | 5 de 5 / of 5 |
| `ProcessFailureHandlersTests` (nueva / new) | 4 de 4 / of 4 |
| `ApSolutions.LocalMedia.IntegrationTests` | 402 de 403, 1 omitida / of 403, 1 skipped |
| `ApSolutions.LocalMedia.ArchitectureTests` | 18 de 18 / of 18 |
| `ApSolutions.LocalMedia.AccessibilityTests` | 73 de 73 / of 73 |
