# Disposición para publicar / Release readiness

- Versión / Version: `0.1.0` (`0.1.0.0` en el paquete / in the package)
- Runtime: `win-x64`
- Firma / Signing: **ninguna / none**
- Puerta MVP / MVP gate: **aprobada el 2026-08-05 con `PLY-004` bloqueado / approved on 2026-08-05 with `PLY-004` blocked**
- Manifiesto de verificación / Verification manifest: [verification-manifest.json](verification-manifest.json)

Este documento contiene primero el informe en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This document contains the Spanish report first and its English translation second. Both parts must be updated together.

---

## Español

### Para qué es este documento

La puerta MVP la aprueba el Product Owner, no la suite. Esto es lo que hay que leer antes de
decidirlo: qué está verificado, qué no, y por qué exactamente.

**Recomendación de Engineering: aprobar con un bloqueo declarado.** El producto hace lo que prometió,
el artefacto existe, se puede comprobar, y el ciclo de instalación de Windows se ejecutó entero en una
máquina limpia. El único compromiso sin verificar lo está por falta de una salida de audio
multicanal, no por falta de trabajo.

### El recuento

| | |
|---|---:|
| Compromisos MVP | **46** |
| `VERIFIED` | **44** |
| `OUT_OF_SCOPE` por decisión | **1** |
| `BLOCKED` con condición de desbloqueo | **1** |
| Sin estado o sin evidencia | **0** |

Ningún compromiso queda informalmente pendiente. `FeatureCoverageTests` falla si alguno no está
`VERIFIED` ni `OUT_OF_SCOPE` y no declara razón, responsable y condición de desbloqueo; y falla
también si uno resuelto arrastra un bloqueo, que es como un bloqueo se convierte en aprobado sin que
nadie lo decida.

### Lo que el ciclo real de Windows encontró

`PRD-002` estuvo bloqueado hasta que se descubrió que **Windows Sandbox es una máquina limpia y
desechable**, disponible en este equipo: se crea de cero al abrirla y se destruye al cerrarla, y
dentro se es administrador. El ciclo se ejecutó allí sobre una copia firmada por un certificado
desechable con el editor real del manifiesto —el artefacto publicado sigue sin firmar— y encontró
**dos defectos que ninguna sustitución podía ver**:

1. **Los datos no iban donde la documentación promete.** MSIX redirigía las escrituras de la
   aplicación al contenedor del paquete: la biblioteca aparecía en
   `…\Packages\<familia>\LocalCache\Local\APSolutions\LocalMedia` en lugar de
   `…\AppData\Local\APSolutions\LocalMedia`.
2. **Desinstalar borraba la biblioteca.** Al retirar el paquete, Windows eliminaba el contenedor y con
   él el catálogo, el progreso, los favoritos y las copias de seguridad. La fase sustituta daba verde
   justo aquí.

Los dos salían de lo mismo, y la corrección está en el manifiesto porque la redirección ocurre por
debajo de la aplicación: reescribiría cualquier ruta que el código eligiera. Con
`FileSystemWriteVirtualization` y `RegistryWriteVirtualization` desactivadas, el paquete y el ZIP
comparten una sola carpeta de datos y desinstalar retira sólo la aplicación.

Tras la corrección, **doce fases en verde**: las siete sustitutas y las cinco que Windows ejecuta. La
asociación de archivos se vio funcionando por primera vez —los ocho contenedores registran «Abrir
con»—, que es algo que ningún recorrido desempaquetado puede comprobar.

El informe está en [windows-lifecycle.json](windows-lifecycle.json) y **caduca solo**:
`verify-package.ps1` lo acepta mientras su versión y el SHA-256 de `Package.appxmanifest` coincidan
con el paquete actual. Si el manifiesto cambia, las cuatro fases vuelven a declararse bloqueadas.

### El bloqueo que queda

#### `PLY-004` — Audio 5.1 y 7.1

**Qué falta.** La selección de 5.1 y 7.1 no se ha ejercido sobre hardware real.

**Por qué.** Medido de dos formas independientes: el formato de mezcla de los cuatro puntos finales
activos declara **dos** canales, y preguntando al hardware con WASAPI en modo exclusivo los cuatro
rechazan 6 y 8 canales con `AUDCLNT_E_UNSUPPORTED_FORMAT`. No hay salida multicanal en este equipo.

**Qué sí se hizo.** La selección de dispositivo por identificador estable, el cambio en caliente y la
preferencia persistente están verificados en estéreo.

La misma medición **confirmó una decisión de diseño de T23**. En modo compartido los cuatro puntos
finales aceptan 6 y 8 canales, porque el motor de Windows remezcla lo que le den al formato del
punto final: preguntar así habría hecho que la aplicación ofreciera 5.1 en unos auriculares estéreo
y la reproducción no lo entregara. `WindowsAudioDeviceCatalog` lee el formato de mezcla —lo que el
punto final entrega hoy— y por eso informa estéreo. Es lo correcto, y ahora está demostrado en vez de
razonado.

**Qué lo desbloquea.** Cualquier punto final que declare seis u ocho canales, y repetir la matriz de
salida de audio. Vale un receptor A/V o una televisión por HDMI, una salida S/PDIF con codificación
multicanal habilitada en el controlador, o unos auriculares USB que expongan 5.1 o 7.1 de verdad en
lugar de virtualizarlo por software. El equipo tuvo registrado un Logitech PRO X que declaraba ocho
canales, pero ese aparato ya no está disponible.

**Riesgo si se publica igualmente.** Bajo para quien reproduzca en estéreo, que es lo que este equipo
ofrece. Desconocido para quien tenga una salida multicanal.

### Riesgos que esta versión deja abiertos para `STABLE`

Ninguno afecta al MVP. Los tres nacen de decisiones que se tomaron aquí y se cobran más adelante, así
que quedan escritos antes de que alguien se los encuentre.

**`unvirtualizedResources` es una capacidad restringida.** Es la que permite desactivar la
redirección de escrituras, y sin ella el paquete borraría la biblioteca al desinstalarse. La Store
exige justificar las capacidades restringidas y puede rechazarlas: `REL-001` tendrá que defenderla o
resolver el problema de otra manera. La justificación es concreta —la aplicación guarda una
biblioteca que debe sobrevivir a la desinstalación, igual que la versión en archivo— y conviene
llevarla preparada.

**Firmar de verdad cambiará la identidad del paquete.** `Publisher` tiene que coincidir con el sujeto
del certificado, y hoy es `CN=AP Solutions Test Publisher, O=AP Solutions`. Al adquirir un
certificado comercial ese sujeto será otro, la identidad cambiará y con ella el `PackageFamilyName`:
quien hubiera instalado este MSIX **no** recibiría la actualización, tendría que desinstalar e
instalar de nuevo. Como el MSIX sin firma no se instala hoy, el número de afectados es cero, pero la
decisión de cuándo firmar conviene tomarla sabiéndolo.

**El informe del ciclo de Windows caduca con el manifiesto, no con el payload.** Es deliberado: lo
que el informe demuestra —registro, asociaciones, virtualización, desinstalación— depende de lo que
Windows lee, que es el manifiesto. Un cambio de payload que rompiera el arranque de la copia
instalada no invalidaría el informe, y por eso la fase sustituta `first-launch`, que sí se ejecuta en
cada verificación, arranca el paquete desempaquetado. Entre las dos cubren el hueco; ninguna sola lo
haría.

### Limitaciones que no son bloqueos

**Una sola clase de adaptador de vídeo.** `PLY-003` está verificado sobre la matriz física ejecutada
en el adaptador disponible, que es lo que pide el plan: este equipo no tiene gráficos integrados —se
midió: cero dispositivos gráficos Intel enumerados— y no puede adquirirlos habilitando nada. Queda un
límite real: la ruta de decodificación de Intel Quick Sync no se ha ejercido nunca, y un defecto
exclusivo de esa ruta habría sido invisible para todas las pruebas del proyecto. La decisión está en
[ADR-0005](../../adr/0005-verify-the-video-matrix-on-available-adapters.md), y S1 —que ya exige
hardware distinto para ARM64— es el momento natural para ampliar la cobertura.

**La agrupación automática de versiones no está cableada.** `LIB-008` está verificado y no se degrada:
su criterio —que ningún archivo se borre ni se oculte, y que se pueda elegir versión por calidad y
disponibilidad— está demostrado, y la superficie de comparación es alcanzable desde una ficha.

Lo que falta es que algo **cree** los grupos: `GroupMediaVersions` existe, tiene repositorio SQLite y
pruebas, y no se invoca desde la aplicación. En el artefacto publicado, por tanto, la comparación de
versiones sólo aparecería si un grupo llegara por otra vía. Está registrado aquí, en el changelog y
en la hoja de ruta para que la decisión de cablearlo sea deliberada y no un descubrimiento.

### Lo que sí está comprobado

| Comprobación | Resultado |
|---|---|
| Suite completa en Release | 1263 pruebas, 0 fallos, 0 omitidas |
| Formato | `dotnet format --verify-no-changes` sin cambios |
| Compilación Debug y Release | `-warnaserror`, 0 avisos, 0 errores |
| Dependencias vulnerables | 0 |
| Documentación | 80 Markdown, 53 IDs, 46 MVP, sin enlaces rotos |
| Accesibilidad, dos pasadas | 0 críticos, 0 mayores, 0 menores |
| Recuperación, dos pasadas | 9 fallos cubiertos, dos pasadas idénticas |
| Rendimiento | 12 de 12 métricas en presupuesto |
| Ciclo de vida del paquete | 12 fases ejecutadas, 0 bloqueadas |
| Reproducibilidad | 671 archivos idénticos entre dos compilaciones limpias, 0 diferencias |
| Fuga de secretos en el artefacto | 0 coincidencias sobre 23 archivos |
| SBOM | 35 componentes, 0 huecos frente a los locks |

### Los artefactos

| Archivo | SHA-256 |
|---|---|
| `APSolutions.LocalMedia_0.1.0_x64.msix` | ver `SHA256SUMS.txt` en la publicación |
| `ApReelume-0.1.0-win-x64.zip` | ver `SHA256SUMS.txt` en la publicación |

El manifiesto de verificación registra los hashes exactos de la compilación que se publique, y
`EvidenceLinkTests` falla si alguno no es un SHA-256 completo.

### La decisión del Product Owner

Lo que había que decidir era aprobar la puerta MVP con el bloqueo declarado, o no aprobarla hasta
conseguir una salida de audio multicanal.

**Aprobada el 2026-08-05 con el bloqueo declarado.**

El bloqueo se volvió a comprobar antes de decidir, y sobre una fuente distinta a las dos anteriores:
el registro de puntos finales de reproducción del equipo, que conserva también los desconectados y
los que ya no están. Los cuatro activos declaran dos canales; el único que llegó a declarar ocho
—con máscara 7.1 completa— es el aparato que el informe ya daba por no disponible, y el mando de
consola que aparece registrado declara cuatro, que no es 5.1 ni 7.1. Ninguna salida multicanal es
accesible hoy, ni conectando algo que ya esté en la casa.

Lo que la aprobación obliga y lo que no cambia:

- La publicación **no** puede afirmar que está firmada, y las notas enlazan
  [SMARTSCREEN.es.md](../../release/SMARTSCREEN.es.md).
- `PLY-004` **no** cambia de estado por haberse aprobado la puerta. Sigue `BLOCKED` con la misma
  condición de desbloqueo, y sólo la cierra una medición sobre un punto final que entregue 5.1 o 7.1.
- Los tres riesgos abiertos para `STABLE` se heredan con la aprobación, no se cierran con ella:
  viajan a `REL-001` y al incremento S1.

Con esto queda abierta la Parte B, que empieza por la paridad ARM64 (`PRD-003`).

---

## English

### What this document is for

The Product Owner approves the MVP gate, not the suite. This is what to read before deciding: what is
verified, what is not, and exactly why.

**Engineering's recommendation: approve with one declared block.** The product does what it promised,
the artifact exists, it can be checked, and Windows' own install cycle ran in full on a clean machine.
The single unverified commitment is unverified for want of a multichannel audio output rather than
for want of work.

### The count

| | |
|---|---:|
| MVP commitments | **46** |
| `VERIFIED` | **44** |
| `OUT_OF_SCOPE` by decision | **1** |
| `BLOCKED` with an unblock condition | **1** |
| Without status or without evidence | **0** |

No commitment is informally pending. `FeatureCoverageTests` fails when one is neither `VERIFIED` nor
`OUT_OF_SCOPE` and declares no reason, owner, and unblock condition; it fails equally when a settled
one still carries a blocker, which is how a block becomes a pass without anyone deciding it.

### What the real Windows cycle found

`PRD-002` stayed blocked until Windows Sandbox turned out to be exactly what was missing: a clean,
disposable Windows, available on this machine, created from nothing when it opens and destroyed when
it closes, with administrator rights inside. The cycle ran there against a copy signed by a throwaway
certificate carrying the manifest's real publisher — the published artifact stays unsigned — and found
**two defects no substitute could see**:

1. **The data was not going where the documentation promises.** MSIX redirected the application's
   writes into the package container: the library landed in
   `…\Packages\<family>\LocalCache\Local\APSolutions\LocalMedia` instead of
   `…\AppData\Local\APSolutions\LocalMedia`.
2. **Uninstalling deleted the library.** Removing the package deleted the container, and with it the
   catalogue, the progress, the favourites, and the backups. The substitute phase went green on
   exactly this.

Both came from the same thing, and the fix belongs in the manifest because the redirection happens
below the application: it would rewrite any path the code chose. With `FileSystemWriteVirtualization`
and `RegistryWriteVirtualization` disabled, the package and the archive share one data folder and
uninstalling removes the application alone.

After the fix, **twelve phases green**: the seven substitutes and the five Windows performs. The file association
was seen working for the first time — all eight containers register an Open-with entry — which no
unpacked run can check.

The report is in [windows-lifecycle.json](windows-lifecycle.json) and **expires on its own**:
`verify-package.ps1` accepts it while its version and the SHA-256 of `Package.appxmanifest` match the
current package. Change the manifest and the four phases go back to blocked.

### The remaining block

#### `PLY-004` — 5.1 and 7.1 audio

**What is missing.** 5.1 and 7.1 selection was not exercised on real hardware.

**Why.** Measured two independent ways: the mix format of all four active endpoints declares **two**
channels, and asking the hardware through WASAPI in exclusive mode has all four reject 6 and 8
channels with `AUDCLNT_E_UNSUPPORTED_FORMAT`. There is no multichannel output on this machine.

**What was done instead.** Device selection by stable identifier, hot switching, and the persistent
preference are verified in stereo.

The same measurement **confirmed a design decision from T23**. In shared mode all four endpoints
accept 6 and 8 channels, because the Windows engine remixes whatever it is given down to the
endpoint's format: asking that way would have made the application offer 5.1 on a stereo headset and
then not deliver it. `WindowsAudioDeviceCatalog` reads the mix format — what the endpoint delivers
today — and therefore reports stereo. That is the right choice, and it is now demonstrated rather
than argued.

**What unblocks it.** Any endpoint declaring six or eight channels, and a repeat of the audio output
matrix: an A/V receiver or television over HDMI, an S/PDIF output with multichannel encoding enabled
in the driver, or USB headphones exposing real 5.1 or 7.1 rather than virtualising it in software.
The machine had a Logitech PRO X registered that declared eight channels, but that device is no
longer available.

**Risk if published anyway.** Low for anyone playing in stereo, which is what this machine offers.
Unknown for anyone with a multichannel output.

### Risks this release leaves open for `STABLE`

None affects the MVP. All three come from decisions taken here and paid for later, so they are
written down before somebody runs into them.

**`unvirtualizedResources` is a restricted capability.** It is what allows the write redirection to
be turned off, and without it the package would delete the library when uninstalled. The Store
requires restricted capabilities to be justified and can refuse them: `REL-001` will have to defend
it or solve the problem another way. The justification is concrete — the application keeps a library
that must outlive an uninstall, exactly as the archive version does — and is worth having ready.

**Signing for real will change the package identity.** `Publisher` must match the certificate's
subject, and today it is `CN=AP Solutions Test Publisher, O=AP Solutions`. A commercial certificate
will carry a different subject, the identity will change, and with it the `PackageFamilyName`:
anyone who had installed this MSIX would **not** receive the update and would have to uninstall and
reinstall. Since the unsigned MSIX does not install today the affected count is zero, but the timing
of signing is worth deciding with that in view.

**The Windows lifecycle report expires with the manifest, not with the payload.** That is deliberate:
what the report demonstrates — registration, associations, virtualisation, uninstall — depends on
what Windows reads, which is the manifest. A payload change that broke the installed copy's startup
would not expire the report, which is why the substitute `first-launch` phase, which does run on
every verification, starts the unpacked package. Between them the gap is covered; neither would cover
it alone.

### Limitations that are not blocks

**One class of video adapter.** `PLY-003` is verified on the physical matrix run against the
available adapter, which is what the plan asks for: this machine has no integrated graphics — it was
measured, zero Intel display devices enumerated — and cannot acquire any by enabling something. A
real limit remains: Intel Quick Sync's decode path has never been exercised, and a defect unique to
it would have been invisible to every test in this project. The decision is in
[ADR-0005](../../adr/0005-verify-the-video-matrix-on-available-adapters.md), and S1 — which already
requires different hardware for ARM64 — is the natural moment to widen the coverage.

**Automatic version grouping is not wired.** `LIB-008` is verified and is not downgraded: its
criterion — that no file is deleted or hidden, and that a version can be chosen by quality and
availability — is demonstrated, and the comparison surface is reachable from a card.

What is missing is anything that **creates** the groups: `GroupMediaVersions` exists, has a SQLite
repository and tests, and is not invoked from the application. In the published artifact, therefore,
the version comparison would appear only if a group arrived some other way. It is recorded here, in
the changelog, and in the roadmap so that wiring it is a deliberate decision rather than a discovery.

### What is checked

The table in the Spanish section lists every gate and its result: 1263 tests with no failures and
nothing skipped, clean formatting, warning-free Debug and Release builds, no vulnerable dependencies,
80 Markdown files with no broken links, two clean accessibility passes, two identical recovery
passes, 12 of 12 performance metrics within budget, twelve lifecycle phases run with none blocked,
671 files identical across two clean builds, no secret matches in the payload, and an SBOM with no
gaps.

### The artifacts

The MSIX and the ZIP are published with their SHA-256 sums in `SHA256SUMS.txt`. The verification
manifest records the exact hashes of the build that ships, and `EvidenceLinkTests` fails if one is
not a full SHA-256.

### The Product Owner's decision

What had to be decided was whether to approve the MVP gate with the one declared block, or withhold
approval until a multichannel audio output is available.

**Approved on 2026-08-05 with the declared block.**

The block was re-checked before deciding, against a third source independent of the other two: the
machine's register of render endpoints, which also keeps the disconnected ones and the ones that are
gone. All four active endpoints declare two channels; the only one that ever declared eight — with a
full 7.1 mask — is the device the report already recorded as unavailable, and the game controller
that appears there declares four, which is neither 5.1 nor 7.1. No multichannel output is reachable
today, not even by plugging in something already in the house.

What the approval requires, and what it does not change:

- The release **must not** claim to be signed, and the notes link
  [SMARTSCREEN.en.md](../../release/SMARTSCREEN.en.md).
- `PLY-004` does **not** change status because the gate was approved. It stays `BLOCKED` under the
  same unblock condition, and only a measurement against an endpoint that delivers 5.1 or 7.1
  settles it.
- The three risks left open for `STABLE` are inherited by the approval rather than closed by it:
  they travel to `REL-001` and to increment S1.

Part B is open with this, and it starts with ARM64 parity (`PRD-003`).

---

## Adenda — auditoría profunda del 2026-08-08 / Addendum — deep audit of 2026-08-08

La auditoría profunda posterior a este informe encontró que varios compromisos aprobados como
`VERIFIED` lo estaban a nivel de componente, no de aplicación ensamblada: el mismo defecto que este
informe ya nombraba para `LIB-008` («existe y nada lo invoca») tiene más apariciones. Vuelven a
`IMPLEMENTED`, cada uno con bloqueo, responsable y condición de desbloqueo en el manifiesto de
verificación:

- `PRD-002` — el ciclo MSIX se verificó sobre una copia resellada; el artefacto sin firma no puede repetirlo.
- `LIB-002` y `LIB-003` — la vigilancia de carpetas está registrada y nunca se resuelve; no hay escaneo incremental automático.
- `LIB-006` y `LIB-007` — la identificación nunca se invoca; la bandeja de revisión queda siempre vacía.
- `LIB-008` — lo ya descrito arriba, ahora con su estado acorde.
- `PLY-008` — el bucle de guardado cada cinco segundos nunca arranca y la reanudación no pasa la posición al motor.
- `PLY-011` — la cuenta atrás del siguiente episodio nunca se ofrece.
- `PLY-014` — teclas multimedia y atajos sin cablear en el reproductor.

The deep audit that followed this report found that several commitments approved as `VERIFIED` held
at the component level, not in the assembled application: the defect this report already named for
`LIB-008` ("it exists and nothing invokes it") has more appearances. They return to `IMPLEMENTED`,
each with its blocker, owner, and unblock condition in the verification manifest:

- `PRD-002` — the MSIX cycle was verified on a re-signed copy; the unsigned artifact cannot repeat it.
- `LIB-002` and `LIB-003` — folder watching is registered and never resolved; there is no automatic incremental scan.
- `LIB-006` and `LIB-007` — identification is never invoked; the review inbox stays empty forever.
- `LIB-008` — what is described above, now with a status to match.
- `PLY-008` — the five-second save loop never starts and resume never hands the position to the engine.
- `PLY-011` — the next-episode countdown is never offered.
- `PLY-014` — media keys and shortcuts unwired in the player.

## Adenda — el eslabón siguiente, 2026-08-14

`LIB-006` vuelve a `BLOCKED`, y merece leerse junto a la adenda anterior porque es la misma familia
un eslabón más allá. En agosto se anotó que «la identificación nunca se invoca»; eso se corrigió, el
escaneo entrega lo hallado a la identificación y la bandeja se llena. Lo que **nunca existió** es el
eslabón que sigue: nada convierte una identificación en metadata guardada.

`catalog_metadata` sólo lo escriben el editor manual y un `RefreshMetadata` cuya entrada asigna
únicamente una prueba; `ResolveMatch` marca el estado del candidato y publica un evento que no
consume ningún archivo de `src/`; `ReviewState.Automatic` sólo se calcula, para decidir si hace falta
la red. Así que una identificación —automática con ≥90 % de confianza o aceptada a mano— no cambia
nada de lo que la biblioteca muestra, y ésta enseña lo que el analizador de nombres sacó del archivo.

**`LIB-007` se queda `VERIFIED`, y es una decisión, no un descuido.** Su criterio es que los umbrales
sean exactos y que la corrección persista, y las dos cosas están demostradas. Lo que falla es aplicar
la metadata, que es la promesa de `LIB-006`; degradar también los umbrales culparía a la función
equivocada y escondería dónde está el trabajo.

La lección para la próxima verificación está en cómo sobrevivió: **la corrección anterior comprobó
que la identificación se invocara y nadie comprobó que su resultado se aplicara**. Una cadena se
verifica hasta lo que el usuario ve, no hasta el eslabón que se acababa de arreglar. Medición y forma
en [audit-identification-never-reaches-the-catalogue.md](../stable/audit-identification-never-reaches-the-catalogue.md).

### Cierre — 2026-08-15

`LIB-006` vuelve a `VERIFIED`, y la condición que se puso para desbloquearlo se cumplió tal cual: el
recorrido ensamblado **pulsa el botón con el ratón** y la ficha cambia. La cadena entera está en pie
—`ApplyIdentification` escribiendo por sus dos llamantes, la bandeja y el camino automático de ≥90 %;
`RefreshMetadata` resolviendo por la referencia guardada; y el editor sin la entrada que nadie
rellenaba—, y cada eslabón trae su medición:
[quien escribe la identificación](../stable/audit-apply-identification.md),
[el refresco se resuelve solo](../stable/audit-refresh-resolves-itself.md) y
[el paseo pulsa el botón](../stable/audit-walk-clicks-the-editor.md).

**Lo que costó saberlo, y conviene no repetirlo**: el clic destapó que el propio paseo montaba la
ventana de una forma que la aplicación no usa, dejando el shell fuera del árbol lógico y **todos** los
botones enlazados por `Command` declarándose deshabilitados. Nadie lo había visto porque hasta ahora
nada hacía clic. Verificar con el teclado no es verificar con el ratón.

## Addendum — the next link, 2026-08-14

`LIB-006` returns to `BLOCKED`, and it is worth reading beside the addendum above because it is the
same family one link further on. August recorded that "identification is never invoked"; that was
fixed, the scan hands what it found to identification, and the inbox fills. What **never existed** is
the link after it: nothing turns an identification into stored metadata.

`catalog_metadata` is written only by the manual editor and by a `RefreshMetadata` whose input is
assigned in a test and nowhere else; `ResolveMatch` marks the candidate's review state and publishes
an event no source file consumes; `ReviewState.Automatic` is only ever calculated. An identification —
automatic at 90% confidence or accepted by hand — changes nothing the library shows.

**`LIB-007` stays `VERIFIED`, and that is a decision rather than an oversight.** Its criterion is that
the thresholds are exact and a correction persists, and both are demonstrated. What fails is applying
the metadata, which is `LIB-006`'s promise; demoting the thresholds too would blame the wrong feature
and hide where the work is.

The lesson is in how it survived: **the earlier fix checked that identification was invoked, and
nobody checked that its result was applied.** A chain is verified as far as what the user sees, not as
far as the link that was just repaired.


### Closed — 2026-08-15

`LIB-006` returns to `VERIFIED`, and the condition set for unblocking it was met as written: the
assembled walk **clicks the button with the mouse** and the entry changes. The whole chain stands —
`ApplyIdentification` writing through both of its callers, the inbox and the automatic path above
90%; `RefreshMetadata` resolving through the stored reference; and the editor without the input
nobody filled in — and each link carries its measurement:
[what writes the identification](../stable/audit-apply-identification.md),
[the refresh resolves itself](../stable/audit-refresh-resolves-itself.md) and
[the walk presses the button](../stable/audit-walk-clicks-the-editor.md).

**What it cost to find out**: the click uncovered that the walk itself mounted the window in a way
the application does not, leaving the shell off the logical tree and **every** command-bound button
reporting itself disabled. Nobody had seen it because until now nothing clicked. Verifying with the
keyboard is not verifying with the mouse.
