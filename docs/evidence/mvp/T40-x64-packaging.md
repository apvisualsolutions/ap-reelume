# T40 — MSIX x64 y artefacto GitHub reproducible / T40 — x64 MSIX and Reproducible GitHub Artifact

- IDs: `PRD-002`, `PRD-005`, `SYS-002`, `REL-002`
- Decidida en / Decided in: [ADR-0004](../../adr/0004-seal-the-package-with-makeappx.md)
- Commit: `build: package and verify the Windows x64 MVP`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts must be updated together.

---

## Español

### Qué se cerró

T39B dejó la aplicación ensamblada. T40 la congela en un artefacto: un MSIX x64 y un ZIP
independiente, con hashes, SBOM, licencias dentro y documentación de SmartScreen que no afirma una
firma que no existe.

No hay `.wapproj`. Construir uno exige `Microsoft.DesktopBridge.targets`, que viaja con una carga de
trabajo de Visual Studio y no está en este equipo ni en el runner de CI; un proyecto que el
repositorio no puede construir sería un fichero que ninguna prueba examina. El layout se monta con
un script y se sella con `MakeAppx.exe`. [ADR-0004](../../adr/0004-seal-the-package-with-makeappx.md)
registra la decisión.

### El artefacto

| | |
|---|---|
| Versión / paquete | `0.1.0` → `0.1.0.0` |
| MSIX | `APSolutions.LocalMedia_0.1.0_x64.msix`, 99,6 MB |
| ZIP | `ApReelume-0.1.0-win-x64.zip`, 100,7 MB |
| Payload | 671 archivos, 233,9 MB |
| SBOM | 35 componentes, CycloneDX 1.5 y SPDX 2.3, 0 huecos frente a los locks |
| Escaneo de secretos | 23 archivos examinados, 0 coincidencias |
| Validación | semántica de MakeAppx **activada**: el paquete pasa la comprobación que decide si Windows podría instalarlo |
| Firma | ninguna, declarada `"signed": false` |

SHA-256 publicados en `SHA256SUMS.txt`:

```
765e267f5238c43aa179f8959e30cc9c52ea61f8c6c5327a5ed7ee5b9a891a11  APSolutions.LocalMedia_0.1.0_x64.msix
3bba3e2af056ebaa63be7d55dae4ab89cffbd077072e60f541c33b2a5e297a05  ApReelume-0.1.0-win-x64.zip
```

### Un artefacto x64 que llevaba tres arquitecturas

Un publish autocontenido `win-x64` de esta aplicación trae LibVLC para **tres** arquitecturas:
`win-x64`, `win-x86` y `win-arm64`. Nadie lo había mirado porque ninguna prueba empaqueta.

| | Archivos | Tamaño |
|---|---:|---:|
| Publish tal cual | 1498 | 512,3 MB |
| Payload publicado | 671 | 233,9 MB |

Las dos cargas retiradas no son sólo peso muerto: son código que el artefacto distribuye y el
cargador nunca abrirá, y que hace que un revisor lea el paquete como construido para otra cosa.
`ArtifactContentsTests` lee la cabecera COFF de cada binario del payload —distinguiendo los
ensamblados administrados, que son neutrales por definición— y falla si alguno no es AMD64.

### Cuatro defectos, y quién los encontró

**1. Un downgrade que nadie rechazaba.** `MigrationRunner` sólo buscaba migraciones sin aplicar. Una
compilación antigua frente a una base que una nueva ya migró no encontraba ninguna pendiente,
concluía que el esquema estaba al día y **empezaba a escribir** en tablas cuya forma desconoce. El
instalador MSIX rechaza un downgrade por su cuenta, pero la publicación también sale como ZIP sin
instalador: cualquiera puede descomprimir una versión vieja sobre la carpeta que una nueva venía
usando. Ahí sólo el binario puede negarse. Ahora comprueba la versión aplicada más alta antes de
escribir nada, y si supera lo que conoce aborta con `InvalidDataException` —que la aplicación ya
traduce en la pantalla de recuperación— sin tocar el archivo ni tomar copia.
Cerrado con [`SchemaDowngradeTests`](../../../tests/ApSolutions.LocalMedia.IntegrationTests/Data/SchemaDowngradeTests.cs).

**2. El indicador de vídeo no decía nada.** Lo encontró el recorrido físico, no una prueba.
`CompositionRoot` construía `VideoStatusViewModel` y **nadie llamaba nunca a `Apply`**: el overlay
era alcanzable —T39B se aseguró de eso— y se quedaba en blanco durante una reproducción real
mientras LibVLC decodificaba en la GPU. Es el mismo patrón que T39B encontró cinco veces, en su
variante siguiente: **alcanzar una superficie y alimentarla son preguntas distintas**. El criterio de
aceptación de `PLY-003` es «indicador correcto y fallback por software sin caída», y el indicador no
existía en la aplicación, sólo en sus pruebas. Ahora la raíz aplica lo que el motor decidió en cuanto
el medio está abierto, y deduce el fallback de `HardwareAccelerationRequested` sin acelerización
activa.

**3. `PathMap` global dejaba la cobertura vacía.** Al normalizar las rutas de origen en todo el
repositorio para la reproducibilidad, coverlet dejó de encontrar los archivos fuente a los que apunta
su instrumentación y devolvió un informe con un solo ensamblado. El mapeo se aplica ahora únicamente
en el publish del paquete, donde cada compilación mapea **su propio** checkout al mismo marcador: dos
carpetas distintas producen los mismos bytes y la cobertura vuelve a medirse.

**4. Dos pruebas que competían con el reloj.** Con el empaquetado por delante, la suite destapó dos
pruebas de integración que fallaban de forma intermitente —una de ellas la mitad de las veces— por
depender de la planificación y no de la promesa que verifican:

- `Create_change_rename_delete_storm_is_coalesced_by_final_path` leía **sólo el primer lote** del
  vigilante. Mil escrituras tardan lo que tardan, así que el renombrado y el borrado podían llegar
  después de cerrarse la ventana de 150 ms. El vigilante promete consolidar por ruta final, nunca que
  toda la tormenta quepa en un lote: ahora se leen lotes hasta encontrar el borrado, comprobando en
  cada uno la consolidación.
- `A_snapshot_taken_while_progress_is_written_opens_and_passes_integrity` tomaba la copia en cuanto
  encolaba el escritor concurrente. Bajo carga, el bucle no había corrido ni una vez al cancelarse.
  Ahora la copia espera a que el escritor haya escrito, lo que además **garantiza** la concurrencia
  que la prueba afirma.

Ninguna de las dos se relajó: las dos verifican ahora más de lo que verificaban.

### El ciclo de vida: siete ejecutadas, cuatro declaradas

No hay máquina virtual limpia de Windows en este equipo, ni elevación de administrador, ni
certificado de firma. El ciclo `install→launch→upgrade→repair→uninstall` que el plan exige necesita
las tres cosas. La sustitución es el **paquete desempaquetado**, ejercitado con una carpeta de datos
propia por ciclo mediante `AP_LOCALMEDIA_DATA_ROOT`; redirigir `LOCALAPPDATA` no serviría, porque
.NET resuelve esa carpeta con `SHGetFolderPath` y no lee esa variable.

| Fase | Tipo | Resultado | Qué se observó |
|---|---|---|---|
| `unpack` | sustituta | `Passed` | 670 archivos recuperados del paquete, 0 alterados, 0 ausentes |
| `first-launch` | sustituta | `Passed` | ventana en pantalla, salida 0, 15 migraciones sobre una base nueva |
| `open-with` | sustituta | `Passed` | activación sobre `.mp4`; catálogo en 0 archivos y 0 raíces |
| `upgrade` | sustituta | `Passed` | `0.1.0.0` reemplazado por `0.2.0.0` sobre la misma carpeta; catálogo y preferencias intactos |
| `downgrade-refused` | sustituta | `Passed` | esquema 16 frente a una compilación que conoce 15; pantalla de recuperación y ni un byte escrito |
| `repair` | sustituta | `Passed` | 3 archivos borrados y restaurados desde el paquete; la aplicación volvió a arrancar |
| `uninstall` | sustituta | `Passed` | la instalación desaparece; la carpeta de datos conserva 2 archivos catalogados |
| `windows-install` | nativa | `Passed` | Windows registra `APSolutions.LocalMedia 0.1.0.0` |
| `windows-upgrade` | nativa | `Passed` | 0.1.0.0 → 0.2.0.0 con la base intacta en 356.352 bytes |
| `windows-repair` | nativa | `Passed` | re-registro desde el paquete que guarda Windows |
| `windows-uninstall` | nativa | `Passed` | paquete retirado, **la biblioteca sobrevive** |

**El bloqueo no se puede leer como un aprobado.** `MsixLifecycleTests` exige que las cuatro fases
nativas estén `Blocked` **con razón** mientras `environment` diga que no hay VM limpia, elevación ni
firma, y exige que estén `Passed` en cuanto las tres sean ciertas. Convertir un SKIP en PASS es un
fallo de la suite, no una decisión editorial. Aquí las tres pasaron a ser ciertas y las fases se
exigieron en verde, que es la misma regla mirando en la otra dirección.

### El bloqueo escondía dos defectos que hacen perder datos

Las cuatro fases nativas estuvieron bloqueadas hasta que **Windows Sandbox** resultó ser lo que
faltaba: una copia de Windows creada de cero al abrirla y destruida al cerrarla, disponible en este
equipo, con permisos de administrador dentro. El modo de desarrollador se activa allí porque nada de
lo que se hace allí sobrevive a la ventana.

El primer intento no llegó ni a instalar: **`0x80073D2C`**, «el editor no está en el espacio de
nombres sin firmar». Windows no instala un paquete sin firma cuyo `Publisher` no lleve el marcador
correspondiente, y añadírselo tampoco bastó. El ciclo corre entonces sobre una copia firmada por un
certificado desechable con el **editor real** del manifiesto, confiado dentro del sandbox y en ningún
otro sitio; el certificado se retira del equipo en cuanto termina de firmar y **el artefacto que se
publica sigue sin firmar**.

Con eso, la instalación funcionó y enseñó dos cosas:

1. **Los datos no iban donde la documentación promete.** MSIX redirige las escrituras bajo `AppData`
   al contenedor del paquete: la biblioteca aparecía en
   `…\Packages\<familia>\LocalCache\Local\APSolutions\LocalMedia`, no en
   `…\AppData\Local\APSolutions\LocalMedia`, que es lo que dicen el README, el manual, la solución de
   problemas y SMARTSCREEN.
2. **Desinstalar borraba la biblioteca.** `datos antes: sí; datos después: no`. Al retirar el paquete,
   Windows elimina el contenedor entero y con él el catálogo, el progreso, los favoritos y las copias
   de seguridad. El plan exige literalmente «desinstalación sin borrar datos personales salvo elección
   explícita», y **la fase sustituta daba verde justo aquí**.

La corrección va en el manifiesto, no en el código, porque la redirección ocurre por debajo de la
aplicación y reescribiría cualquier ruta que el código eligiera:
`desktop6:FileSystemWriteVirtualization` y `desktop6:RegistryWriteVirtualization` a `disabled`, con la
capacidad `unvirtualizedResources` que Windows exige para ello —y que la validación semántica de
MakeAppx reclamó por su nombre en cuanto faltó—. `FileAssociationPackageTests` fija ahora las dos
propiedades y la lista exacta de capacidades.

Tras la corrección, la instalación registra los **ocho** contenedores en «Abrir con», la actualización
conserva la base byte a byte, el downgrade se rechaza, la reparación re-registra y la desinstalación
deja la biblioteca donde estaba.

### Lo que la revisión adversarial encontró en este trabajo

Repasar las decisiones buscando refutarlas destapó dos huecos propios:

- **`"semanticValidation": true` era una constante, no una medición.** El script lo escribía siempre,
  así que devolver `/nv` habría dejado el informe diciendo que la validación se hizo cuando ya no se
  hacía. Ahora una prueba lee `eng/package-x64.ps1` y falla si el `pack` lleva `/nv` o
  `/noValidation`: la afirmación y el comando que la sostiene se comprueban juntos.
- **La asociación de archivos no se cobraba.** El informe del sandbox la traía en verde, pero el
  script sólo adoptaba las cuatro fases `windows-*`, así que la única comprobación de `SYS-002` a
  nivel de sistema se quedaba en un JSON que nada leía. Es ahora una fase nativa más, exigida por
  `MsixLifecycleTests`: doce fases.

El informe se archiva en [`windows-lifecycle.json`](windows-lifecycle.json) y **caduca solo**:
`verify-package.ps1` lo adopta mientras su versión y el SHA-256 de `Package.appxmanifest` coincidan
con el paquete. El manifiesto es lo que gobierna instalación, asociaciones y virtualización, así que
si cambia, las cuatro fases vuelven a declararse bloqueadas.
[`eng/README-sandbox.md`](../../../eng/README-sandbox.md) explica cómo repetirlo.

### Reproducibilidad

Dos compilaciones del mismo estado, desde dos checkouts limpios en dos carpetas distintas:

| | |
|---|---|
| Archivos idénticos | **671** |
| Archivos con distinto hash | **0** |
| Archivos en una sola compilación | **0** |
| Excluidos de la comparación | **0** |

El contenedor MSIX no se compara y la evidencia lo dice: un paquete OPC registra el instante en que
se selló, así que dos payloads idénticos nunca producen dos archivos idénticos. Lo que se compara es
todo lo que hay dentro, archivo por archivo por SHA-256. `ReproducibleBuildTests` exige además que
los dos checkouts hayan estado en rutas distintas —si no, la comparación no demuestra nada— y que la
lista de exclusiones esté vacía.

Los checkouts se crean con `git stash create`, de modo que la comparación es sobre el estado que se
va a confirmar y no sobre el último commit. Si hay archivos sin rastrear, el script se detiene: un
archivo que no está en el índice no existe para un checkout limpio.

### Privacidad y consentimiento de red en el artefacto

- El manifiesto declara **una sola** capacidad, `runFullTrust`, que es la que necesita cualquier
  aplicación de escritorio. Ninguna de red, ninguna de ubicación, ninguna de biblioteca del sistema.
  `FileAssociationPackageTests` falla si aparece otra.
- El artefacto **no lleva token**. La identificación remota sólo funciona si alguien pone uno a mano
  en `AP_LOCALMEDIA_TMDB_TOKEN`; sin ese acto deliberado no se abre ninguna conexión.
- La asociación de archivos es una oferta, no una apropiación: no declara verbos ni se reclama
  gestor predeterminado. Esa decisión es de quien instala.
- El escaneo del payload busca claves privadas, tokens de GitHub, cabeceras `Bearer` y rutas locales
  sobre los 23 archivos que este repositorio produce o que son texto. Cero coincidencias.

### La lista de contenedores existe tres veces y no puede divergir

`.mp4`, `.mkv`, `.avi`, `.mov`, `.webm`, `.m4v`, `.ts`, `.m2ts` están en el dominio (lo que el
escáner cataloga), en `FileAssociations.xml` (junto al código que la honra) y en el manifiesto (lo
que Windows lee). `FileAssociationPackageTests` compara las tres, y además compara el manifiesto
extraído **del paquete sellado**, que es el único que Windows leerá de verdad.

### El recorrido, sobre el artefacto empaquetado

Automatización UIA sobre el ejecutable **desempaquetado del MSIX**, con carpeta de datos propia y sin
token. Dieciséis pasos, **dieciséis en verde**:

| Paso | Resultado |
|---|---|
| Arrancar la aplicación empaquetada | ✅ |
| Ir a Biblioteca | ✅ |
| Escribir la ruta y añadir la carpeta | ✅ sin rechazo |
| Permitir el primer escaneo | ✅ |
| Aplicar y listar | ✅ 1 ficha |
| Abrir la ficha de título | ✅ |
| Reproducir desde la ficha | ✅ |
| El reproductor está en pantalla | ✅ |
| La sesión declara que reproduce | ✅ |
| **El estado del vídeo informa de una ruta por hardware** | ✅ «Rango dinámico estándar. Decodificación acelerada por hardware.» |
| No se cayó a la pantalla de recuperación | ✅ |
| Cerrar el reproductor y salir | ✅ salida 0 |

LibVLC registró `Using D3D11VA … for hardware decoding` durante el recorrido: **la poda de las dos
arquitecturas ajenas no rompió la reproducción**, que era la pregunta que ninguna prueba sin cabeza
podía responder.

Lo escrito en la base tras cerrar: 1 raíz de biblioteca, 1 archivo, 1 estado de visionado.

### Estados

- `PRD-005` → `VERIFIED`: licencia, avisos de terceros en ambos idiomas y SBOM viajan **dentro** del
  artefacto, y la atribución de TMDB se lee en la aplicación desde T39B.
- `SYS-002` → `VERIFIED`: la asociación empaquetada está declarada, comparada con el dominio y
  ejercitada sobre el artefacto sin crear una sola fila.
- `REL-002` → `VERIFIED`: hashes publicados, compilación reproducible comprobada y SmartScreen
  documentado sin afirmar ninguna firma.
- `PRD-002` sigue **`IMPLEMENTED`**, no `VERIFIED`. Su criterio es «MSIX x64 instala, inicia,
  actualiza y desinstala correctamente», y las cuatro fases que Windows ejecuta están bloqueadas por
  falta de VM limpia, elevación y certificado. El paquete existe, su ciclo sustituto pasa entero y el
  informe lo dice; declararlo verificado sería exactamente convertir el bloqueo en un aprobado.
  **Condición de desbloqueo:** una máquina virtual limpia de Windows 11 x64 y un certificado en el
  que el instalador confíe.

`PLY-003` sigue `IN_PROGRESS`: su indicador ya funciona en la aplicación real, pero el bloqueo de
hardware —no hay GPU integrada activa para probar el fallback por software— no ha cambiado.

### Cobertura

T40 no añade ningún archivo de producción; modifica dos.

| Archivo | Cobertura |
|---|---|
| `MigrationRunner.cs` | 96,7 % |
| `CompositionRoot.cs` | 56,7 % |

`MigrationRunner.cs` supera el umbral con el rechazo de downgrade dentro. `CompositionRoot.cs` queda
donde T39B lo dejó (56,4 %): lo que no se ejercita es el arranque de la ventana real, que sólo ocurre
con la aplicación en marcha y se comprueba en el recorrido físico.

### Verificación

- RED archivada en `artifacts/test-results/T40/red/`: **45 fallos, 8 pasadas, 0 omitidas**, todos por
  ausencia del manifiesto, los scripts y los informes. Más `red-downgrade/` (2 fallos por aceptar el
  downgrade, 1 control en verde) y `red-videostatus/` (1 fallo por no alimentar el indicador).
- GREEN en `artifacts/test-results/T40/green/`.
- `dotnet format --verify-no-changes`: sin cambios.
- Debug y Release con `-warnaserror`: 0 avisos, 0 errores.
- Suite completa en Release: **1207 pruebas, 0 fallos, 0 omitidas**.
- `eng/verify.ps1 -Configuration Release -Runtime win-x64`: correcta. Ahora construye el paquete y
  recorre su ciclo antes de las pruebas, porque las suites de empaquetado leen el artefacto y sus
  informes: sin eso serían inaplicables en silencio.
- `eng/verify-docs.ps1`, accesibilidad dos pasadas, recuperación dos pasadas y rendimiento: en las
  cifras de cierre del incremento.

---

## English

### What was closed

T39B left the application assembled. T40 freezes it into an artifact: an x64 MSIX and an independent
ZIP, with hashes, an SBOM, licences inside, and SmartScreen documentation that claims no signature it
does not have.

There is no `.wapproj`. Building one needs `Microsoft.DesktopBridge.targets`, which ships with a
Visual Studio workload and is on neither this hardware nor the CI runner; a project the repository
cannot build would be a file no test examines. The layout is assembled by a script and sealed with
`MakeAppx.exe`. [ADR-0004](../../adr/0004-seal-the-package-with-makeappx.md) records the decision.

### The artifact

| | |
|---|---|
| Version / package | `0.1.0` → `0.1.0.0` |
| MSIX | `APSolutions.LocalMedia_0.1.0_x64.msix`, 99.6 MB |
| ZIP | `ApReelume-0.1.0-win-x64.zip`, 100.7 MB |
| Payload | 671 files, 233.9 MB |
| SBOM | 35 components, CycloneDX 1.5 and SPDX 2.3, 0 gaps against the lock files |
| Secret scan | 23 files examined, 0 matches |
| Validation | MakeAppx semantic validation **on**: the package passes the check that decides whether Windows could install it |
| Signature | none, declared `"signed": false` |

Published SHA-256 sums are in `SHA256SUMS.txt`, reproduced in the Spanish section above.

### An x64 artifact carrying three architectures

A self-contained `win-x64` publish of this application brings LibVLC for **three** architectures:
`win-x64`, `win-x86`, and `win-arm64`. Nobody had looked, because no test packages anything.

| | Files | Size |
|---|---:|---:|
| Publish as it comes | 1498 | 512.3 MB |
| Published payload | 671 | 233.9 MB |

The two removed payloads are not merely wasted bytes: they are code the artifact ships and the loader
will never open, and their presence reads as a package built for something else.
`ArtifactContentsTests` reads the COFF header of every binary in the payload — distinguishing managed
assemblies, which are architecture-neutral by definition — and fails when one is not AMD64.

### Four defects, and what found them

**1. A downgrade nobody refused.** `MigrationRunner` looked only for unapplied migrations. An older
build meeting a database a newer one had migrated found none missing, concluded the schema was
current, and **started writing** into tables whose shape it does not know. The MSIX installer refuses
a downgrade on its own, but the release also ships as a ZIP with no installer: anyone can extract an
older build over the folder a newer one had been using, and there only the binary can refuse. It now
checks the highest applied version before writing anything and aborts with an `InvalidDataException`
— which the application already turns into its recovery screen — without touching the file or taking
a backup. Closed by
[`SchemaDowngradeTests`](../../../tests/ApSolutions.LocalMedia.IntegrationTests/Data/SchemaDowngradeTests.cs).

**2. The video status said nothing.** The physical walk found this, not a test. `CompositionRoot`
built a `VideoStatusViewModel` and **nothing ever called `Apply`**: the overlay was reachable — T39B
saw to that — and stayed blank through a real playback while LibVLC decoded on the GPU. It is the
same pattern T39B found five times, in its next variant: **reaching a surface and feeding it are
different questions**. `PLY-003`'s acceptance criterion is a correct indicator with a software
fallback, and the indicator did not exist in the application, only in its tests. The composition root
now applies what the engine decided as soon as the media is open, deriving the fallback from
`HardwareAccelerationRequested` without active acceleration.

**3. A repository-wide `PathMap` emptied the coverage report.** Normalising source paths everywhere
for reproducibility left coverlet unable to find the sources its instrumentation points at, and the
report came back with a single assembly. The map now applies only to the package publish, where each
build maps **its own** checkout to the same placeholder: two directories produce the same bytes and
coverage is measurable again.

**4. Two tests racing the clock.** With packaging ahead of it, the suite exposed two integration
tests failing intermittently — one of them half the time — because they depended on scheduling rather
than on the promise they verify:

- `Create_change_rename_delete_storm_is_coalesced_by_final_path` read **only the first batch**. A
  thousand writes take as long as they take, so the rename and the delete could land after the 150 ms
  window closed. The watcher promises coalescing by final path, never that the whole storm fits in
  one batch: batches are now read until the deletion appears, checking coalescing in each.
- `A_snapshot_taken_while_progress_is_written_opens_and_passes_integrity` took the snapshot as soon
  as it queued the concurrent writer. Under load the loop had not run once by the time it was
  cancelled. The snapshot now waits for the writer to have written, which also **guarantees** the
  concurrency the test asserts.

Neither was relaxed: both now verify more than they did.

### The lifecycle: seven run, four declared

There is no clean Windows virtual machine on this hardware, no administrator elevation, and no
signing certificate, and the `install→launch→upgrade→repair→uninstall` cycle the plan requires needs
all three. The substitution is the **unpacked package**, exercised against a data folder of its own
per cycle through `AP_LOCALMEDIA_DATA_ROOT`; redirecting `LOCALAPPDATA` would not work, because .NET
resolves that folder through `SHGetFolderPath` and never reads the variable.

Seven substitute phases passed — unpack, first launch, open-with, upgrade, downgrade refused, repair,
uninstall — with the observations tabulated in the Spanish section, and Windows' own four passed too.

**A block cannot be read as a pass.** `MsixLifecycleTests` requires all four native phases to be
`Blocked` **with a reason** while `environment` reports no clean virtual machine, no elevation, and
no signature, and requires them to be `Passed` the moment all three are true. Turning a skip into a
pass is a failing suite, not an editorial decision — and here all three became true, so the suite
demanded the phases in green, which is the same rule seen from the other side.

### The block was hiding two defects that lose data

The four native phases stayed blocked until **Windows Sandbox** turned out to be what was missing: a
Windows created from nothing when it opens and destroyed when it closes, available on this machine,
with administrator rights inside. Developer mode is enabled there because nothing done there outlives
the window.

The first attempt did not even install: **`0x80073D2C`**, the publisher is not in the unsigned
namespace. Windows will not install an unsigned package whose `Publisher` lacks the corresponding
marker, and adding the marker did not help either. The cycle therefore runs against a copy signed by
a throwaway certificate carrying the manifest's **real** publisher, trusted inside the sandbox and
nowhere else; the certificate is removed from the machine as soon as it has signed, and **the
published artifact stays unsigned**.

With that, the install worked and taught two things:

1. **The data was not going where the documentation promises.** MSIX redirects writes under `AppData`
   into the package container: the library landed in
   `…\Packages\<family>\LocalCache\Local\APSolutions\LocalMedia`, not in
   `…\AppData\Local\APSolutions\LocalMedia`, which is what the README, the guide, the troubleshooting
   page, and SMARTSCREEN all say.
2. **Uninstalling deleted the library.** Data before: yes; data after: no. Removing the package
   deletes the whole container, and with it the catalogue, the progress, the favourites, and the
   backups. The plan requires an uninstall that does not delete personal data without an explicit
   choice, and **the substitute phase went green on exactly this**.

The fix belongs in the manifest rather than the code, because the redirection happens below the
application and would rewrite any path the code chose: `desktop6:FileSystemWriteVirtualization` and
`desktop6:RegistryWriteVirtualization` set to `disabled`, with the `unvirtualizedResources` capability
Windows requires for it — which MakeAppx's semantic validation named the moment it was missing.
`FileAssociationPackageTests` now pins both properties and the exact capability list.

After the fix the install registers all **eight** containers under Open-with, the upgrade keeps the
database byte for byte, the downgrade is refused, the repair re-registers, and the uninstall leaves
the library where it was.

### What the adversarial review found in this work

Going back over the decisions looking to refute them turned up two holes of my own:

- **`"semanticValidation": true` was a constant, not a measurement.** The script always wrote it, so
  putting `/nv` back would have left the report claiming the validation ran when it no longer did. A
  test now reads `eng/package-x64.ps1` and fails if the `pack` carries `/nv` or `/noValidation`: the
  claim and the command behind it are checked together.
- **The file association was not being collected.** The sandbox report carried it green, but the
  script adopted only the four `windows-*` phases, so the one system-level check of `SYS-002` sat in
  a JSON nothing read. It is now a native phase of its own, required by `MsixLifecycleTests`: twelve
  phases.

The report is archived in [`windows-lifecycle.json`](windows-lifecycle.json) and **expires on its
own**: `verify-package.ps1` adopts it while its version and the SHA-256 of `Package.appxmanifest`
match the package. The manifest governs installation, associations, and virtualisation, so if it
changes the four phases go back to blocked.
[`eng/README-sandbox.md`](../../../eng/README-sandbox.md) explains how to repeat it.

### Reproducibility

Two builds of the same state, from two clean checkouts in two different directories: **671 identical
files, 0 differing, 0 unmatched, 0 excluded**.

The MSIX container is not compared and the evidence says so: an OPC package records the moment it was
sealed, so two identical payloads never produce two identical files. What is compared is everything
inside, file by file by SHA-256. `ReproducibleBuildTests` additionally requires that the two
checkouts were in different directories — otherwise the comparison proves nothing — and that the
exclusion list is empty.

The checkouts are made with `git stash create`, so the comparison is about the state being committed
rather than the last commit. Untracked files stop the script: a file outside the index does not exist
to a clean checkout.

### Privacy and network consent in the artifact

- The manifest declares **one** capability, `runFullTrust`, which any desktop application needs. None
  for network, location, or system libraries. `FileAssociationPackageTests` fails if another appears.
- The artifact carries **no token**. Remote identification works only when somebody places one by
  hand in `AP_LOCALMEDIA_TMDB_TOKEN`; without that deliberate act no connection is opened.
- The file association is an offer rather than a seizure: no verbs are declared and no default
  handler is claimed. That decision belongs to whoever installs it.
- The payload scan looks for private keys, GitHub tokens, `Bearer` headers, and local paths across
  the 23 files this repository produces or that are text. Zero matches.

### The container list exists three times and cannot diverge

`.mp4`, `.mkv`, `.avi`, `.mov`, `.webm`, `.m4v`, `.ts`, `.m2ts` live in the domain (what the scanner
catalogues), in `FileAssociations.xml` (beside the code that honours it), and in the manifest (what
Windows reads). `FileAssociationPackageTests` compares all three, and also compares the manifest
extracted **from the sealed package**, which is the only one Windows will actually read.

### The walk, on the packaged artifact

UI Automation over the executable **unpacked from the MSIX**, with a data folder of its own and no
token. Sixteen steps, **sixteen green**: launch, Library, type the path and add the folder with no
refusal, consent to the first scan, apply and list one card, open the title card, play from it, the
player on screen, the session reporting it is playing, **the video status reporting a hardware path**
("Standard dynamic range. Hardware-accelerated decoding."), no fall into the recovery screen, and a
clean exit.

LibVLC logged `Using D3D11VA … for hardware decoding` during the walk: **pruning the two foreign
architectures did not break playback**, which was the question no headless test could answer.

Storage after closing: 1 library root, 1 media file, 1 watch state.

### Statuses

- `PRD-005` → `VERIFIED`: licence, bilingual third-party notices, and SBOM travel **inside** the
  artifact, and the TMDB attribution has been readable in the application since T39B.
- `SYS-002` → `VERIFIED`: the packaged association is declared, compared against the domain, and
  exercised on the artifact without creating a single row.
- `REL-002` → `VERIFIED`: hashes published, reproducibility demonstrated, and SmartScreen documented
  without claiming any signature.
- `PRD-002` stays **`IMPLEMENTED`**, not `VERIFIED`. Its criterion is that the x64 MSIX installs,
  starts, updates, and uninstalls correctly, and the four phases Windows performs are blocked for
  want of a clean virtual machine, elevation, and a certificate. The package exists, its substitute
  cycle passes in full, and the report says so; calling it verified would be exactly turning the
  block into a pass. **Unblock condition:** a clean Windows 11 x64 virtual machine and a certificate
  the installer trusts.

`PLY-003` remains `IN_PROGRESS`: its indicator now works in the real application, but the hardware
block — no active integrated GPU to exercise the software fallback — is unchanged.

### Coverage

T40 adds no production file and changes two: `MigrationRunner.cs` at 96.7% with the downgrade refusal
inside it, and `CompositionRoot.cs` at 56.7%, where T39B left it. What is not exercised is real
window startup, which only happens with the application running and is checked by the physical walk.

### Verification

RED archived under `artifacts/test-results/T40/red/`: **45 failures, 8 passes, 0 skipped**, all for
the absent manifest, scripts, and reports, plus `red-downgrade/` (2 failures accepting the downgrade,
1 control green) and `red-videostatus/` (1 failure for the unfed indicator). GREEN under
`artifacts/test-results/T40/green/`. `dotnet format --verify-no-changes` clean; Debug and Release with
`-warnaserror` at 0 warnings and 0 errors; full Release suite **1207 tests, 0 failures, 0 skipped**;
`eng/verify.ps1 -Configuration Release -Runtime win-x64` passed — it now builds the package and walks
its lifecycle before the tests, because the packaging suites read the artifact and its reports and
would otherwise be silently unenforceable.

---

> **Nota (2026-08-08).** Este documento es el acta de una verificación anterior a T44. Las frases
> «sin ese acto deliberado no se abre ninguna conexión» eran ciertas al escribirse; el actualizador
> de T44 introdujo después dos destinos más (`api.github.com`, `github.com`), bajo control del
> usuario y desactivados de fábrica. La tabla vigente vive en
> [PRIVACY.es.md](../../privacy/PRIVACY.es.md).
>
> **Note (2026-08-08).** This document records a verification that predates T44. The sentences
> "without that deliberate act no connection is opened" were true when written; the T44 updater later
> introduced two more destinations (`api.github.com`, `github.com`), under the user's control and off
> by default. The current table lives in [PRIVACY.en.md](../../privacy/PRIVACY.en.md).
