# Ejecutar el ciclo de vida de Windows / Running the Windows lifecycle

Las cuatro fases que ejecuta Windows —instalar, actualizar, reparar y desinstalar el paquete— no se
pueden lanzar desde `verify-package.ps1`: necesitan una máquina limpia, un administrador y una firma.
Se ejecutan a mano en **Windows Sandbox**, que es una copia de Windows creada de cero al abrirla y
destruida al cerrarla, y su resultado se archiva en
[`docs/evidence/mvp/windows-lifecycle.json`](../docs/evidence/mvp/windows-lifecycle.json).

The four phases Windows performs cannot be launched from `verify-package.ps1`: they need a clean
machine, an administrator, and a signature. They are run by hand in **Windows Sandbox** — a Windows
created from nothing when it opens and destroyed when it closes — and the result is archived at the
path above.

## Por qué / Why

Ejecutar esto encontró **dos defectos que ninguna sustitución podía ver**: MSIX redirigía las
escrituras de la aplicación al contenedor del paquete, y desinstalarlo borraba la biblioteca del
usuario. Los dos están cerrados en el manifiesto; sin este ciclo, seguirían en la versión publicada.

Running this found **two defects no substitute could see**: MSIX redirected the application's writes
into the package container, and uninstalling deleted the user's library. Both are closed in the
manifest; without this cycle they would still be in the published release.

## Requisitos / Requirements

- Windows 11 Pro o Enterprise con Windows Sandbox habilitado (Características de Windows).
- El SDK de Windows, para `makeappx.exe` y `signtool.exe`.
- Un paquete recién construido: `pwsh ./eng/package-x64.ps1`.

## Los pasos / The steps

1. **Firmar una copia de prueba.** Windows rechaza un paquete sin firma cuyo editor no está en el
   espacio de nombres sin firmar (`0x80073D2C`), así que el ciclo corre sobre una copia firmada por un
   certificado desechable con el editor real del manifiesto. **El artefacto que se publica sigue sin
   firmar.** Genere el certificado, firme, y **retírelo del almacén del equipo** en cuanto termine:
   la confianza se concede sólo dentro del sandbox.
2. **Preparar la carpeta compartida** con el paquete de esta versión, uno de la siguiente (misma
   identidad, versión mayor), el certificado exportado y el guion del ciclo.
3. **Lanzar el sandbox** con un `.wsb` que mapee esa carpeta y ejecute el guion al iniciar sesión. El
   guion tiene que ser **ASCII puro**: el sandbox trae Windows PowerShell 5.1, que lee un archivo sin
   BOM como ANSI y convierte cualquier otro byte en un error de sintaxis. El comando de inicio debe
   esperar a que la carpeta esté montada antes de leer nada.
4. **Recoger el informe**, añadirle la versión y el SHA-256 de `Package.appxmanifest`, y archivarlo.

## La entrega del actualizador / The updater's handover

`REL-003` añade una segunda pregunta que este mismo entorno responde: el actualizador entrega el
paquete verificado con `ShellExecute` y dice si Windows lo aceptó. Que esa afirmación sea cierta
depende de lo que devuelva la llamada, y **responde distinto según la máquina**, así que hay que
medirla en las dos: un Windows sin nada registrado para `.msix` —el sandbox recién creado— y uno con
instalador de aplicaciones. Lo medido se archiva en
[`docs/evidence/stable/updater-handover.json`](../docs/evidence/stable/updater-handover.json) y
`UpdateHandoverTests` lo exige.

Dos cosas que aprendió el guion por las malas, y que conviene no repetir: nada dentro del sandbox
debe esperar por red sin un límite de tiempo —una descarga sin `timeout` lo dejó colgado sin escribir
informe—, y **cada fase va envuelta y el informe se escribe en un `finally`**. Una verificación que
no deja rastro cuando algo va mal esconde justo lo que se ejecutó para encontrar.

`REL-003` adds a second question this same environment answers: the updater hands the verified
package over with `ShellExecute` and reports whether Windows took it. Whether that claim is true
depends on what the call returns, and it **answers differently per machine**, so both are measured:
one with nothing registered for `.msix` and one with an App Installer. The result is archived at the
path above and `UpdateHandoverTests` requires it. Nothing inside the sandbox may wait on the network
without a timeout, and every phase is wrapped with the report written in a `finally`.

## Cuándo caduca / When it expires

`verify-package.ps1` acepta el informe archivado **sólo** mientras su `version` y su `manifestSha256`
coincidan con el paquete actual. El manifiesto es lo que gobierna la instalación, las asociaciones de
archivo y la virtualización de escrituras: si cambia, el informe deja de valer, las cuatro fases
vuelven a declararse bloqueadas y `MsixLifecycleTests` lo dice. Un cambio del payload no lo caduca,
porque el payload no es lo que Windows lee al instalar.

`verify-package.ps1` accepts the archived report **only** while its `version` and `manifestSha256`
match the current package. Change the manifest and the report stops counting, the four phases go back
to blocked, and the suite says so.
