# T44 — Actualizador independiente seguro / T44 — Safe Independent Updater

- IDs: `REL-003`, `DAT-001`, `PRI-001`, `DOC-001`
- Commit: `feat: verify summarize and confirm independent updates`
- Superficie: `Ajustes → Actualizaciones` / `Settings → Updates`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts must be updated together.

---

## Español

### Qué se cierra

`REL-003` pasa a `VERIFIED`. La aplicación pregunta si hay una versión nueva sólo cuando alguien lo
pide o lo ha permitido, muestra qué cambia en los dos idiomas, descarga a una carpeta aparte,
comprueba lo que llega contra el hash y el tamaño publicados, y entrega el paquete a Windows
únicamente después de una confirmación **para esa versión concreta**.

La Store tiene su propio canal de actualización. Este actualizador no se duplica ahí: dos
actualizadores compitiendo por sustituir la misma instalación no son el doble de seguridad.

### La forma del trabajo

Diez archivos nuevos, en cuatro capas. La política ya estaba escrita —`UpdatePolicy`, en `Domain`,
con sus treinta pruebas— y decide antes de descargar nada; lo que se añade aquí es todo lo que hay
alrededor de esa decisión.

| Capa | Archivo | Qué decide |
|---|---|---|
| `Application` | `UpdateContracts.cs` | Los puertos, el consentimiento y las cuatro formas de fallar |
| `Application` | `CheckForUpdates.cs` | Preguntar, no preguntar, o no haber podido preguntar |
| `Application` | `ConfirmUpdate.cs` | Descargar y verificar; entregar sólo con confirmación |
| `Infrastructure` | `GitHubReleaseUpdateProvider.cs` | Traducir una publicación de GitHub en una descripción |
| `Infrastructure` | `VerifiedUpdateDownloader.cs` | Traer los bytes y demostrar que son los prometidos |
| `Infrastructure` | `StoredUpdateSettings.cs` | Si la aplicación puede mirar por su cuenta |
| `Windows` | `WindowsUpdateLauncher.cs` | Entregar el paquete a Windows, o no poder |
| `Presentation` | `UpdateViewModel.cs`, `UpdateView.axaml` | Leer, confirmar y saber qué pasó |

Tres decisiones de diseño sostienen el resto:

**Descargar e instalar son dos llamadas distintas.** Responden a cosas distintas: descargar responde a
quien pulsó un botón, instalar responde a quien leyó qué cambiaba. Unirlas produciría exactamente el
comportamiento que esta tarea existe para hacer imposible —un paquete que se instala porque terminó
de descargarse.

**El consentimiento nombra una versión.** `UpdateConsent` se construye en un único sitio, el modelo de
vista, con la versión que estaba en pantalla en el momento de pulsar. Un consentimiento para otra
versión se rechaza con `ConsentMismatch`. Nada más en la aplicación puede fabricar uno.

**Se verifica dos veces.** Una cuando los bytes llegan y otra justo antes de entregarlos. Todo lo que
ocurre entre esos dos momentos es el disco de otro, y el sentido de un hash es poder volver a
comprobarlo cuando importa y no una sola vez cuando resultó cómodo.

### Las redirecciones se siguen a mano

Una dirección de un artefacto de GitHub responde con una redirección a su propio almacenamiento, así
que rechazarlas todas no descargaría nada nunca. Seguirlas automáticamente dejaría que la dirección
que realmente se descarga sea una que la política no vio. El descargador las sigue él mismo y exige
HTTPS **en cada salto**, con un máximo de cinco: la promesa de que la descarga viaja cifrada es sobre
la dirección de la que salen los bytes, no sobre la que se anunció.

### Interrumpida no es lo mismo que equivocada

Una descarga que se corta conserva lo que llegó bajo un nombre `.partial` y la siguiente pide con
`Range` sólo lo que falta. Una que llega completa pero con otro hash se borra: reanudar desde bytes
que fallaron la verificación envenenaría todos los intentos posteriores. Y un servidor que ignora el
`Range` y manda el archivo entero reinicia el archivo en lugar de duplicarlo.

Nada aparece con el nombre real del paquete hasta estar comprobado. Que un archivo con ese nombre
exista en la carpeta significa una sola cosa: ha sido demostrado.

### Verificación física: seis defectos encontrados

Un arnés fuera del repositorio ejecuta los componentes reales contra un servidor TLS real —socket,
handshake y bytes por el cable— sobre una instalación real: un binario en ejecución y una base de
datos SQLite migrada. El certificado se genera en memoria y no se toca ningún almacén del equipo.

**Defecto 1 — la casilla de comprobación automática no hacía nada.** Estaba en la pantalla, se
guardaba en disco y ningún camino de la aplicación pedía nunca una comprobación automática:
`UpdateCheckTrigger.Automatic` no se usaba en producción. La aplicación sólo comprobaba al pulsar un
botón, y la casilla prometía otra cosa. Se corrige lanzando la comprobación automática al montar la
ventana, y el modelo de vista pasa a ser único: empezarla sobre una segunda instancia habría
actualizado algo que nadie mira, que es indistinguible de no comprobar. Una prueba lo sostiene.

**Defecto 2 — el repositorio de muestra elegido para comprobar el contrato de GitHub no publicaba
artefactos**, así que la mitad del contrato quedaba sin verificar mientras el arnés decía que todo
estaba presente. Se cambió por uno que sí los publica.

**Defecto 3 — el actualizador consultaba un propietario que no existe.** El código pedía
`ap-solutions/ap-reelume` y el repositorio es `apvisualsolutions/ap-reelume`. Esas dos cadenas son la
dirección entera, y equivocarse no tiene síntoma: GitHub responde 404, la ausencia de publicación es
una respuesta resuelta y no un fallo, y la aplicación diría «ya tienes la versión más reciente» para
siempre, con todas las pruebas en verde porque cada una levanta su propio servidor. `UpdateSourceTests`
compara ahora la constante con la dirección de publicaciones que los dos changelogs publican, y falla
si vuelven a separarse.

**Defecto 4 — un resumen que empezara por un subtítulo habría llegado vacío.** Las notas de una
publicación son una entrada de changelog, y una entrada de changelog se escribe con subtítulos `###`.
El lector de secciones cortaba en cualquier línea que empezara por `##`, así que `### Añadido` se leía
como el comienzo de otra sección: el resumen se truncaba, y una versión que abriera con un subtítulo
—como hace hoy la sección sin publicar— habría producido un resumen vacío y **la versión no se habría
ofrecido a nadie**. Ahora sólo un encabezado del mismo nivel cierra una sección.

Ese cuarto salió de generar las notas de verdad en lugar de escribir un ejemplo a mano. Es la misma
lección que las anteriores: un formato inventado para una prueba concuerda consigo mismo.

**Defecto 5 — el lanzador documentaba un mecanismo que no es el que actúa.** Está escrito alrededor
de un `catch` que enumera cinco excepciones, y el rechazo que de verdad ocurre no pasa por ahí: en un
Windows sin nada registrado para `.msix`, la llamada devuelve nulo sin lanzar nada. El `catch` sigue
siendo correcto, pero quien lo lea creerá que es el camino vivo y optimizará el muerto. Corregido en
los dos sitios, con lo medido escrito al lado.

**Defecto 6 — el aviso de que Windows no abrió el paquete decía «puedes intentarlo otra vez».** En
una máquina sin instalador de aplicaciones, reintentar no funciona nunca. El mensaje ahora dice que
el archivo está descargado y comprobado, y cómo instalarlo a mano.

Los dos últimos salieron de instalar el paquete de verdad en un Windows limpio, que es la cuarta de
las cuatro preguntas: las pruebas construyen la superficie, la aplicación la ensambla, el instalador
la congela, y **Windows la instala**.

### Las notas de la publicación son parte del artefacto

`eng/package-x64.ps1` las escribe en `artifacts/package/release-notes.md` desde los dos changelogs y
los hashes recién calculados, y publicar es pegar ese archivo. `ReleaseNotesTests` no comprueba un
formato: entrega lo generado al proveedor real dentro de la respuesta que GitHub devolvería y le
pregunta a la política real si ofrecería esa versión. Es la diferencia entre unas notas que se
analizan y unas notas que llegan a alguien.

Lo que la verificación midió, dos pasadas idénticas:

| Pregunta | Medido |
|---|---|
| Identificador de runtime del proceso | `win-x64`, que es el que el proveedor usa para elegir el artefacto |
| Versión que declara el ensamblado | `0.1.0.0`, y la política la compara sin problema con un `0.2.0` |
| Actualización correcta | ofrecida, verificada, entregada a Windows una vez |
| Cancelada, manipulada, interrumpida | ninguna entrega a Windows |
| Binario en ejecución, en los cuatro casos | hash idéntico |
| Base de datos, en los cuatro casos | hash idéntico y `integrity_check` correcto |
| Archivos de actualización dentro de la instalación | 0 |
| Comprobación automática desactivada | `NotAsked`, **0 conexiones** |
| Comprobación automática activada | oferta producida, **1 conexión** |
| Entrega real por `ShellExecute` | Windows arrancó el manejador registrado |
| JSON real de una publicación de GitHub | todos los campos que el proveedor lee están presentes |
| Descarga más lenta que el tiempo de espera del cliente | **se completa** |

Esa última se midió antes de escribirla. Con el paquete publicado rondando los cien megabytes, que el
tiempo de espera del cliente fuera también un techo para la descarga habría dejado sin actualizar a
cualquiera con conexión lenta. Pedir sólo las cabeceras lo evita, y ahora una prueba lo fija.

Las conexiones se contaron desde los orígenes de eventos de .NET dentro del proceso, y el cero
significa algo porque la misma medida llega a uno cuando se permite la comprobación.

### La entrega a Windows, medida en las dos máquinas que existen

La aplicación no instala nada: abre el archivo verificado como lo abriría una persona y dice si
Windows lo aceptó. Que esa afirmación sea cierta depende de lo que devuelva `ShellExecute`, y eso no
puede decidirse desde este lado. Se midió a mano, y responde distinto según la máquina.

**Sin nada registrado para `.msix`** —un Windows Sandbox recién creado, que no trae instalador de
aplicaciones— la llamada **devuelve nulo y no lanza ninguna excepción**. El rechazo llega por el
nulo, no por el `catch`. Windows instaló el paquete firmado, la aplicación arrancó y escribió su base
de datos en la ruta documentada (356 352 bytes), la entrega no arrancó nada, la versión siguió siendo
`0.1.0.0` y la base de datos quedó byte a byte igual.

**Con instalador de aplicaciones** —hardware de desarrollo con la versión 1.29.280.0— la llamada
devuelve un proceso y aparece la ventana «Instalador de aplicación». No se instaló nada: el paquete
lleva un certificado que esa máquina no acepta, y lo que se medía era sólo lo que devolvió la
llamada.

Esa segunda mitad existe por una razón concreta: `ShellExecute` puede entregar un archivo a un
proceso que ya está corriendo y devolver nulo aunque algo sí se haya abierto. Si eso pasara, la
aplicación diría «Windows no ha abierto el paquete» mientras el instalador aparece delante de quien
lo está leyendo. Medido: no ocurre.

**Lo que el código decía de sí mismo era inexacto.** El lanzador está escrito alrededor de un
`catch` que enumera cinco excepciones, y el rechazo que de verdad sucede no pasa por ahí. El `catch`
sigue siendo correcto —una máquina puede negarse de esas otras formas— pero un comentario que
describe un mecanismo que no es el que actúa es peor que ninguno: la próxima persona optimizaría el
camino muerto creyendo que es el vivo. Corregido en los dos sitios.

**Y el mensaje al usuario mentía en una de las dos máquinas.** Decía «puedes intentarlo otra vez»;
en una máquina sin instalador de aplicaciones, reintentar no funcionará nunca. Ahora dice que el
archivo está descargado y comprobado, y que puede instalarse a mano desde la página de publicaciones.

`UpdateHandoverTests` archiva la medición y caduca con el manifiesto, por la misma razón que el
informe del ciclo de vida: el manifiesto es lo que Windows lee al instalar.

### Lo que sigue sin cerrarse

**Una actualización completa de extremo a extremo.** Lo que no se ha ejercido es el camino entero en
una sola máquina: la aplicación instalada descarga, entrega, y el instalador de aplicaciones sustituye
esa misma aplicación mientras corre. La máquina que tiene instalador no acepta el certificado
desechable, y la que lo acepta no tiene instalador. Cerrarlo exige un certificado de confianza real,
que es lo mismo que bloquea `REL-001`.

Esto no bloquea `REL-003`: el compromiso es descargar en segundo plano, mostrar los cambios y no
instalar sin confirmar. Las tres cosas están verificadas, y lo que Windows hace después de aceptar el
paquete ya está medido por separado en `MsixLifecycleTests`, incluida una actualización que conserva
la biblioteca.

### Cifras

| | |
|---|---|
| Pruebas | 1452, 0 fallos, 0 omitidas, dos pasadas idénticas |
| Añadidas por T44 | 123 |
| Cobertura de líneas, ocho archivos nuevos | 100 % |
| Cobertura de ramas | 96,2 % la más baja (`VerifiedUpdateDownloader`) |
| Conexiones con la comprobación automática desactivada | 0 |
| Hosts declarados en el registro de red | 4 |
| Defectos encontrados por la verificación física | 6, todos corregidos |

`UpdateView.axaml` queda con 1 de 2 ramas, igual que las veinte vistas que ya existían: es un
artefacto del compilador XAML de Avalonia y no código de este repositorio.

### Privacidad

Dos entradas nuevas en el registro de propósitos de red, ambas con consentimiento requerido:
`api.github.com` para preguntar si hay una versión nueva, y `github.com` para descargar el paquete
que alguien eligió instalar. La prueba que recorre el árbol de fuentes buscando clientes HTTP sin
propósito declarado sigue siendo la que decide, y ahora nombra cuatro.

La pantalla nombra el paquete y nunca la carpeta donde está: una ruta en pantalla es lo que hace que
una captura deje de poder compartirse.

---

## English

### What this settles

`REL-003` becomes `VERIFIED`. The application asks whether a newer version exists only when somebody
requests it or has allowed it, shows what changed in both languages, downloads into a folder of its
own, checks what arrives against the published hash and size, and hands the package to Windows only
after a confirmation **for that exact version**.

The Store has its own update channel. This updater is not duplicated into it: two updaters racing to
replace the same installation is not twice the safety.

### The shape of the work

Ten new files across four layers. The policy was already written — `UpdatePolicy`, in `Domain`,
with its thirty tests — and decides before anything is downloaded; what is added here is everything
around that decision.

| Layer | File | What it decides |
|---|---|---|
| `Application` | `UpdateContracts.cs` | The ports, the consent, and the four ways to fail |
| `Application` | `CheckForUpdates.cs` | Asking, not asking, or not having been able to ask |
| `Application` | `ConfirmUpdate.cs` | Fetch and verify; hand over only with a confirmation |
| `Infrastructure` | `GitHubReleaseUpdateProvider.cs` | Translating a GitHub release into a description |
| `Infrastructure` | `VerifiedUpdateDownloader.cs` | Fetching the bytes and proving they are the promised ones |
| `Infrastructure` | `StoredUpdateSettings.cs` | Whether the application may look on its own |
| `Windows` | `WindowsUpdateLauncher.cs` | Handing the package to Windows, or failing to |
| `Presentation` | `UpdateViewModel.cs`, `UpdateView.axaml` | Reading, confirming, and knowing what happened |

Three design decisions hold up the rest:

**Downloading and installing are two separate calls.** They answer to different things: downloading
answers to whoever pressed a button, installing answers to whoever read what changed. Merging them
would produce exactly the behaviour this task exists to make impossible — a package that installs
because it finished downloading.

**A consent names a version.** `UpdateConsent` is constructed in one place, the view model, from the
version that was on screen when the button was pressed. A consent for another version is refused as
`ConsentMismatch`. Nothing else in the application can manufacture one.

**Verification happens twice.** Once when the bytes arrive and once immediately before they are
handed over. Everything between those two moments is somebody else's disk, and the point of a hash is
that it can be checked again when it matters rather than once when it was convenient.

### Redirects are followed by hand

A GitHub asset address answers with a redirect to its own storage, so refusing them all would
download nothing, ever. Following them automatically would let the address that is actually fetched
be one the policy never saw. The downloader follows them itself and requires HTTPS **on every hop**,
to a maximum of five: the promise that the download travels encrypted is about the address the bytes
come from, not the one that was advertised.

### Interrupted is not the same as wrong

A download that is cut off keeps what arrived under a `.partial` name, and the next attempt asks with
`Range` for only what is missing. One that arrives complete but hashes to something else is deleted:
resuming from bytes that failed verification would poison every later attempt. And a server that
ignores the `Range` and sends the whole file restarts the file rather than doubling it.

Nothing appears under the package's real name until it has been verified. A file with that name in
the folder means one thing only: it has been proved.

### Physical verification: six defects found

A harness outside the repository runs the real components against a real TLS server — socket,
handshake, and bytes on the wire — over a real installation: a running binary and a migrated SQLite
database. The certificate is generated in memory and no store on the machine is touched.

**Defect 1 — the automatic-check box did nothing.** It was on the screen, it was written to disk, and
no path in the application ever asked for an automatic check: `UpdateCheckTrigger.Automatic` was
unused in production. The application only ever checked when a button was pressed, and the box
promised otherwise. Fixed by starting the automatic check when the window is configured, with the
view model becoming a singleton: starting it on a second instance would have updated something nobody
is looking at, which is indistinguishable from not checking. A test holds it up.

**Defect 2 — the sample repository chosen to check GitHub's contract published no assets**, so half
the contract went unverified while the harness reported everything present. It was changed for one
that does publish them.

**Defect 3 — the updater asked for an owner that does not exist.** The code asked for
`ap-solutions/ap-reelume` and the repository is `apvisualsolutions/ap-reelume`. Those two strings are
the entire address, and getting one wrong has no symptom: GitHub answers 404, the absence of a
release is a settled answer rather than a failure, and the application would tell everybody they are
up to date forever — with every test still green, because each one brings its own server.
`UpdateSourceTests` now compares the constant against the release address both changelogs publish,
and fails if they drift apart again.

**Defect 4 — a summary that began with a subheading would have arrived empty.** Release notes are a
changelog entry, and a changelog entry is written with `###` subheadings. The section reader broke on
any line starting with `##`, so `### Añadido` read as the start of another section: the summary was
truncated, and a version that opened with a subheading — as the unreleased section does today — would
have produced an empty summary and **the release would have been offered to nobody**. Only a heading
of the same level ends a section now.

That fourth one came out of generating the notes for real instead of writing an example by hand. It
is the same lesson as the others: a format invented for a test agrees with itself.

**Defect 5 — the launcher documented a mechanism that is not the live one.** It is written around a
catch listing five exceptions, and the refusal that actually happens does not go through it: on a
Windows with nothing registered for `.msix`, the call returns null without throwing. The catch is
still correct, but whoever reads it will believe it is the live path and optimise away the dead one.
Fixed in both places, with what was measured written beside it.

**Defect 6 — the notice that Windows did not open the package said "you can try again".** On a
machine with no App Installer, trying again never works. The message now says the file is downloaded
and verified, and how to install it by hand.

The last two came out of installing the package for real on a clean Windows, which is the fourth of
the four questions: the tests build the surface, the application assembles it, the installer freezes
it, and **Windows installs it**.

### The release notes are part of the artifact

`eng/package-x64.ps1` writes them to `artifacts/package/release-notes.md` from the two changelogs and
the hashes it has just computed, and publishing is a matter of pasting that file. `ReleaseNotesTests`
does not check a format: it hands what was generated to the real provider inside the payload GitHub
would return and asks the real policy whether it would offer that version. That is the difference
between notes that parse and notes that reach somebody.

What the verification measured, across two identical passes:

| Question | Measured |
|---|---|
| Runtime identifier this process reports | `win-x64`, which is what the provider uses to pick the asset |
| Version the assembly declares | `0.1.0.0`, and the policy compares it against a `0.2.0` without trouble |
| A correct update | offered, verified, handed to Windows once |
| Cancelled, tampered, interrupted | nothing handed to Windows |
| The running binary, in all four | identical hash |
| The database, in all four | identical hash and a passing `integrity_check` |
| Update files inside the installation | 0 |
| Automatic check switched off | `NotAsked`, **0 connections** |
| Automatic check switched on | an offer produced, **1 connection** |
| The real `ShellExecute` handover | Windows started the registered handler |
| The real JSON of a GitHub release | every field the provider reads is present |
| A download slower than the client timeout | **it finishes** |

That last one was measured before it was written down. With the published package around a hundred
megabytes, a client timeout that was also a ceiling on the download would have left anybody on a slow
connection unable to update. Asking only for the headers avoids it, and a test now pins it.

Connections were counted from the .NET event sources inside the process, and the zero means something
because the same measurement reaches one when the check is allowed.

### The handover to Windows, measured on both machines that exist

The application installs nothing: it opens the verified file the way a person would and reports
whether Windows took it. Whether that claim is true depends on what `ShellExecute` returns, and that
cannot be decided from this side. It was measured by hand, and it answers differently per machine.

**With nothing registered for `.msix`** — a freshly created Windows Sandbox, which ships without an
App Installer — the call **returns null and throws nothing**. The refusal arrives through the null,
not through the catch. Windows installed the signed package, the application launched and wrote its
database at the documented path (356,352 bytes), the handover started nothing, the version stayed at
`0.1.0.0`, and the database was byte for byte unchanged.

**With an App Installer** — developer hardware carrying version 1.29.280.0 — the call returns a
process and the "Instalador de aplicación" window appears. Nothing was installed: the package carries
a certificate that machine does not trust, and what was being measured was only what the call
returned.

That second half exists for a specific reason: `ShellExecute` may hand a file to a process that is
already running and return null even though something did open. If that happened, the application
would say "Windows did not open the package" while the installer appeared in front of whoever was
reading it. Measured: it does not.

**What the code said about itself was inaccurate.** The launcher is written around a catch listing
five exceptions, and the refusal that actually happens does not go through it. The catch is still
correct — a machine can refuse in those other ways — but a comment describing a mechanism that is not
the live one is worse than none: the next person would optimise away the dead path believing it was
the live one. Fixed in both places.

**And the message to the person was untrue on one of the two machines.** It said "you can try
again"; on a machine with no App Installer, trying again will never work. It now says the file is
downloaded and verified, and can be installed by hand from the releases page.

`UpdateHandoverTests` archives the measurement, and it expires with the manifest for the same reason
the lifecycle report does: the manifest is what Windows reads when it installs.

### What still cannot be settled

**A complete update, end to end.** What has not been exercised is the whole path on one machine: the
installed application downloads, hands over, and the App Installer replaces that same application
while it runs. The machine with an installer does not trust the throwaway certificate, and the one
that trusts it has no installer. Closing that needs a real trusted certificate, which is the same
thing blocking `REL-001`.

This does not block `REL-003`: the commitment is to download in the background, show what changed,
and never install without confirmation. All three are verified, and what Windows does once it accepts
the package is measured separately in `MsixLifecycleTests`, including an upgrade that keeps the
library.

### Numbers

| | |
|---|---|
| Tests | 1452, 0 failures, 0 skipped, two identical passes |
| Added by T44 | 123 |
| Line coverage, eight new files | 100 % |
| Branch coverage | 96.2 % at the lowest (`VerifiedUpdateDownloader`) |
| Connections with the automatic check off | 0 |
| Hosts declared in the network registry | 4 |
| Defects found by physical verification | 6, all fixed |

`UpdateView.axaml` sits at 1 of 2 branches, exactly like the twenty views that already existed: that
is an artifact of Avalonia's XAML compiler and not code from this repository.

### Privacy

Two new entries in the network purpose registry, both requiring consent: `api.github.com` to ask
whether a newer version exists, and `github.com` to download the package somebody chose to install.
The test that walks the source tree looking for HTTP clients with no declared purpose is still what
decides, and it now names four.

The screen names the package and never the folder it is in: a path on screen is how a screenshot
stops being safe to share.
