# T41 — Cierre documental y puerta MVP / T41 — Documentation Closure and MVP Gate

- IDs: todos los `MVP` / all `MVP`; cierre especial / special closure `PRD-001`, `PRD-005`, `UX-008`, `DOC-001`
- Commit: `docs: close the bilingual x64 MVP release gate`
- Informe para el Product Owner / Report for the Product Owner: [release-readiness.md](release-readiness.md)

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts must be updated together.

---

## Español

### Qué se cerró

T40 dejó el artefacto. T41 cierra lo que queda para poder mirarlo y decidir: la documentación pública
en los dos idiomas, un manifiesto que mapea cada compromiso MVP a cómo se resolvió, y cuatro suites
que convierten «la matriz dice que sí» en una comprobación que se puede ejecutar.

La puerta no la aprueba la suite. La suite garantiza que **nada queda informalmente pendiente**; la
aprobación es del Product Owner y se hace sobre [release-readiness.md](release-readiness.md).

### La puerta es una prueba, no una lectura

Cuatro suites nuevas en `tests/ApSolutions.LocalMedia.DocumentationTests`:

| Suite | Qué exige |
|---|---|
| `FeatureCoverageTests` | 53 IDs y 46 MVP; manifiesto y matriz cubren los mismos IDs con el mismo estado; nada `VERIFIED` sin evidencia; **todo lo no resuelto declara razón, responsable y condición de desbloqueo**; nada resuelto arrastra un bloqueo; cada compromiso nombra sus tareas y sus suites |
| `EvidenceLinkTests` | cada enlace de la matriz y del manifiesto abre; los dos registros enlazan lo mismo; ningún enlace relativo roto en `docs/`; el informe de disposición nombra cada compromiso sin resolver; cada artefacto se identifica por su SHA-256 |
| `BilingualHeadingTests` | los diez documentos públicos existen en ambos idiomas con la misma estructura de encabezados; cada evidencia lleva los dos idiomas en un solo archivo; ningún documento español ha perdido sus tildes |
| `ScopeBoundaryTests` | ninguna capacidad excluida aparece en los recursos que ve el usuario; la hoja de ruta da cuenta de las cinco exclusiones por identificador; `UX-008` y `PLY-015` conservan su rechazo; ninguna migración crea tabla para algo excluido |

RED archivada en `artifacts/test-results/T41/red/`: **21 fallos, 34 pasadas, 0 omitidas**, por
documentos y manifiesto ausentes. GREEN en `artifacts/test-results/T41/green/`.

**Dos marcadores de exclusión estaban mal puestos y la RED los descubrió.** `Passthrough` a secas
señalaba `VideoStatusHdrPassthrough`, que es la ruta HDR10 de `PLY-003` —una función incluida, no
excluida—; y `Note` señalaba `RestoreFindingNotEnoughSpace` y cada `Notice` de las licencias. La
exclusión real es el passthrough **de audio**, y la nota personal **de la línea de tiempo**: los
marcadores dicen ahora eso.

### Bilingüe, y comprobado como tal

El repositorio tiene dos convenciones y las dos mantienen los idiomas en un archivo: los informes
anteriores emparejan las lenguas dentro de cada encabezado con una barra, y desde T39B el texto
español es una sección y su traducción otra. La suite acepta ambas y rechaza lo que importa: un
informe escrito en un solo idioma.

Documentos nuevos, los diez en pareja: `README`, `docs/roadmap/README`, `docs/user-guide/README`,
`docs/troubleshooting/README`, `docs/CHANGELOG`, más los cuatro que ya existían y
`docs/release/SMARTSCREEN` y `RELEASING` de T40. La comprobación compara la **secuencia de niveles**
de encabezado, que es lo que caza el fallo real: una sección añadida a un idioma y olvidada en el
otro.

### El manifiesto se genera, no se escribe

[`verification-manifest.json`](verification-manifest.json) lo produce
`eng/generate-verification-manifest.ps1` leyendo `docs/FEATURES.md`. El estado y la evidencia no se
escriben dos veces: dos registros que pueden discrepar son un registro y un rumor.

Del script salen sólo tres cosas declaradas a mano: qué suites cubren qué área, qué artefactos
publica la versión —con su hash leído de `SHA256SUMS.txt`— y **los bloqueos**. El generador se niega
a producir un manifiesto donde un compromiso sin resolver no declare su bloqueo, o donde uno resuelto
lo arrastre.

| | |
|---|---:|
| Compromisos MVP | 46 |
| `VERIFIED` | 44 |
| `OUT_OF_SCOPE` | 1 |
| `BLOCKED` con condición | 1 |

### Los seis estados que cambiaron

| ID | Antes | Ahora | Por qué |
|---|---|---|---|
| `PRD-001` | `IN_PROGRESS` | `VERIFIED` | La aplicación funciona sin autenticación y guarda todo localmente; `ScopeBoundaryTests` comprueba además que ninguna capacidad de cuenta, sincronización o nube aparece en la interfaz |
| `PLY-001` | `IN_PROGRESS` | `VERIFIED` | El integrado es predeterminado y el externo no promete progreso exacto; T39B lo hizo alcanzable y T40 lo reprodujo desde el artefacto empaquetado |
| `DOC-001` | `IN_PROGRESS` | `VERIFIED` | Los diez documentos públicos existen en ambos idiomas y cuatro suites fallan por un enlace roto o una sección sin pareja |
| `PRD-002` | `IMPLEMENTED` | `VERIFIED` | El ciclo que ejecuta Windows se corrió entero en una máquina limpia y desechable; encontró dos defectos que hacían perder datos y los dos están cerrados. Ver [T40](T40-x64-packaging.md) y [el informe](windows-lifecycle.json) |
| `PLY-003` | `IN_PROGRESS` | `VERIFIED` | Su criterio —indicador correcto y fallback por software sin caída— está demostrado en hardware, y el plan pide los adaptadores **disponibles**; este equipo no tiene gráficos integrados ni puede tenerlos. [ADR-0005](../../adr/0005-verify-the-video-matrix-on-available-adapters.md) |
| `PLY-004` | `IN_PROGRESS` | `BLOCKED` | Ningún punto final de audio activo declara más de dos canales |

`BLOCKED` no es una degradación: `PLY-004` venía de un estado que tampoco era `VERIFIED`, y el
glosario de la matriz reserva `BLOCKED` exactamente para esto —«existe un bloqueo documentado con
responsable y condición de desbloqueo»—, que es lo que ahora lleva.

### Medido, no heredado

Los tres candidatos a bloqueo se comprobaron en este equipo antes de escribirlos, y **medir cambió el
resultado en dos de los tres**. Sólo uno sobrevivió como bloqueo.

- **Gráficos integrados.** La evidencia de T22 daba por hecho que el procesador incorporaba gráficos
  y declaraba un bloqueo a la espera de habilitarlos. Al medirlo: cero dispositivos gráficos Intel
  enumerados —ni presentes, ni fantasma, ni en el registro de la clase de vídeo— frente a veinte
  dispositivos Intel del chipset, de modo que la enumeración funciona y sencillamente no hay
  adaptador. El propietario confirma que su procesador no lleva gráficos. No hay nada que habilitar,
  así que no es un bloqueo: es un límite de cobertura, y `PLY-003` se cierra sobre la matriz física
  del adaptador disponible según [ADR-0005](../../adr/0005-verify-the-video-matrix-on-available-adapters.md).
- **Salida multicanal.** Leyendo la misma propiedad del registro que lee `WindowsAudioDeviceCatalog`,
  los cuatro puntos finales de render activos declaran **dos** canales. Sigue siendo un bloqueo
  porque hay algo concreto que hacer: cualquier punto final que declare seis u ocho canales sirve, y
  la matriz de salida de audio se repite sobre él.
- **Instalación por Windows.** Se dio por bloqueada por falta de máquina limpia, elevación y firma,
  hasta que **Windows Sandbox** resultó ser las tres cosas y estar en este equipo. El ciclo se
  ejecutó allí y encontró dos defectos que hacían perder datos —la redirección de escrituras al
  contenedor y una desinstalación que borraba la biblioteca—, ambos cerrados en el manifiesto. Doce
  fases en verde. El detalle está en [T40](T40-x64-packaging.md).

La lección se repite en las dos direcciones: **un bloqueo heredado sin medir puede ser falso por
exceso o por defecto.** El de gráficos integrados prometía un trabajo que nadie podía hacer; el de
instalación escondía un defecto que destruía la biblioteca del usuario en una desinstalación
corriente. Sólo el de audio multicanal era lo que decía ser.

### La limitación que se registra en lugar de esconderse

`LIB-008` sigue `VERIFIED` y no se degrada: su criterio está demostrado y la superficie de
comparación es alcanzable. Lo que falta es que algo **cree** los grupos de versiones:
`GroupMediaVersions` existe, tiene adaptador SQLite y pruebas, y no se invoca desde la aplicación.

En el artefacto publicado, la comparación de versiones sólo aparecería si un grupo llegara por otra
vía. Queda escrito en tres sitios —aquí, en el changelog y en la hoja de ruta— para que cablearlo sea
una decisión y no un descubrimiento.

### Alcance negativo

`ScopeBoundaryTests` lee los diccionarios de recursos que el usuario ve y falla si una clave ofrece
algo excluido. Nueve exclusiones cubiertas: cuentas y sesión remota, sincronización y nube, varios
vídeos a la vez, cursos, gestión de vídeo más allá del catálogo, notas en la línea de tiempo, listas
personalizadas, Dolby Vision y passthrough de audio, y macOS/Linux.

Además comprueba que ninguna migración crea tabla para una capacidad excluida: la interfaz se puede
revertir, un esquema publicado no.

### La documentación afirma cosas sobre la aplicación, y se comprobaron en ella

Un documento que dice «la atribución se lee en Ajustes» es una afirmación verificable, y se verificó
sobre el artefacto empaquetado, no sobre el código. Automatización UIA sobre el ejecutable
desempaquetado del MSIX, con carpeta de datos propia y sin token. Seis pasos, **seis en verde**:
arrancar, ir a Ajustes, encontrar la superficie de créditos, leer la atribución de TMDB palabra por
palabra, leer el aviso de GPL-3.0-or-later, y salir con código 0.

El artefacto lleva además, comprobado sobre el ZIP publicado: `LICENSE` (33,9 KB), `NOTICE`,
`licenses/THIRD-PARTY-NOTICES.es.md` y `.en.md`, y `sbom/` con los dos formatos. Lo que `NOTICE`
promete es exactamente lo que hay dentro.

### Verificación

- `dotnet format --verify-no-changes`: sin cambios.
- Debug y Release con `-warnaserror`: 0 avisos, 0 errores.
- Suite completa en Release, dos ejecuciones: **1263 pruebas, 0 fallos, 0 omitidas**.
- `eng/verify.ps1 -Configuration Release -Runtime win-x64`: correcta, dos veces.
- `eng/verify-docs.ps1`: 80 Markdown, 20 localizados, 53 IDs, 46 MVP.
- `eng/run-accessibility.ps1 -Mode Verify -Passes 2`: 0 críticos, 0 mayores, 0 menores.
- `eng/run-recovery.ps1 -Mode Verify -Passes 2`: 9 filas, dos pasadas iguales.
- `eng/run-performance.ps1`: 12 de 12 métricas en presupuesto.
- 0 dependencias vulnerables.

---

## English

### What was closed

T40 left the artifact. T41 closes what remains before anyone can look at it and decide: the public
documentation in both languages, a manifest mapping every MVP commitment to how it was resolved, and
four suites that turn "the matrix says so" into something that can be run.

The suite does not approve the gate. The suite guarantees that **nothing is informally pending**;
approval belongs to the Product Owner and is made against
[release-readiness.md](release-readiness.md).

### The gate is a test, not a reading

Four new suites in `tests/ApSolutions.LocalMedia.DocumentationTests`: `FeatureCoverageTests` pins the
53 identifiers and the 46 MVP ones, requires manifest and matrix to cover the same identifiers with
the same status, refuses anything `VERIFIED` without evidence, requires **every unsettled commitment
to declare reason, owner, and unblock condition**, refuses a settled one that still carries a
blocker, and requires each commitment to name its tasks and suites. `EvidenceLinkTests` follows every
link in both records and in `docs/`, requires them to agree, requires the readiness report to name
every unsettled commitment, and requires each artifact to be identified by a full SHA-256.
`BilingualHeadingTests` pins the ten public documents in both languages with the same heading shape,
requires each evidence report to carry both languages in one file, and refuses a Spanish document
that has lost its diacritics. `ScopeBoundaryTests` reads the resource dictionaries the user sees and
fails when a key offers something excluded.

RED archived under `artifacts/test-results/T41/red/`: **21 failures, 34 passes, 0 skipped**, for the
absent documents and manifest. GREEN under `artifacts/test-results/T41/green/`.

**Two exclusion markers were wrong and RED found them.** A bare `Passthrough` matched
`VideoStatusHdrPassthrough`, which is `PLY-003`'s HDR10 path — an included feature, not an excluded
one — and `Note` matched `RestoreFindingNotEnoughSpace` and every `Notice` in the licence strings.
The real exclusions are **audio** passthrough and **timeline** notes, and the markers now say that.

### Bilingual, and checked as such

The repository has two conventions and both keep the languages in one file: the earlier reports pair
the languages inside each heading with a slash, and from T39B onwards the Spanish text is one section
and its translation another. The suite accepts both and rejects what matters: a report written in one
language.

The check compares the **sequence of heading levels**, which catches the real failure: a section
added to one language and forgotten in the other.

### The manifest is generated, not written

[`verification-manifest.json`](verification-manifest.json) is produced by
`eng/generate-verification-manifest.ps1` from `docs/FEATURES.md`. Status and evidence are not typed
twice: two records that can disagree are one record and one rumour.

Only three things are declared by hand: which suites cover which area, which artifacts the release
publishes — with hashes read from `SHA256SUMS.txt` — and **the blocks**. The generator refuses to
produce a manifest where an unsettled commitment declares no block, or where a settled one still
carries one. Of 46 MVP commitments: 44 verified, 1 out of scope, 1 blocked.

### The six statuses that changed

`PRD-001`, `PRD-002`, `PLY-001`, `PLY-003`, and `DOC-001` move to `VERIFIED`; `PLY-004` moves to
`BLOCKED`. The Spanish table above gives the reason for each. `BLOCKED` is not a downgrade: it was
not `VERIFIED` either, and the matrix's glossary reserves `BLOCKED` for exactly this — a documented
block with an owner and an unblock condition — which is what it now carries.

### Measured, not inherited

All three block candidates were checked on this machine before being written down, and **measuring
changed the answer for two of the three**. Only one survived as a block.

T22's evidence had assumed the processor carried integrated graphics and declared a block pending
their enablement; measuring found zero Intel display devices enumerated — none present, none phantom,
none in the video class registry — against twenty Intel chipset devices, so enumeration works and
there is simply no adapter. With nothing to enable it is not a block but a coverage limit, and
`PLY-003` is closed under
[ADR-0005](../../adr/0005-verify-the-video-matrix-on-available-adapters.md).

The Windows install was taken as blocked for want of a clean machine, elevation, and a signature —
until Windows Sandbox turned out to be all three and to be on this machine. Running the cycle there
found two defects that lose data, both closed in the manifest; [T40](T40-x64-packaging.md) has the
detail. Twelve phases green.

Only the audio one stands. Reading the same registry property `WindowsAudioDeviceCatalog` reads, all
four active render endpoints declare **two** channels; any endpoint declaring six or eight would
unblock the commitment, and the audio output matrix repeats against it.

The lesson repeats in both directions: **an inherited block that nobody measured can be wrong by
excess or by omission.** The integrated-graphics one promised work nobody could do; the install one
was hiding a defect that destroyed the user's library on an ordinary uninstall.

### The limitation that is recorded rather than hidden

`LIB-008` stays `VERIFIED` and is not downgraded: its criterion is demonstrated and the comparison
surface is reachable. What is missing is anything that **creates** the version groups:
`GroupMediaVersions` exists, has a SQLite adapter and tests, and is not invoked from the application.

In the published artifact the version comparison would appear only if a group arrived some other way.
It is written in three places — here, in the changelog, and in the roadmap — so that wiring it is a
decision rather than a discovery.

### Negative scope

Nine exclusions are covered: accounts and remote sessions, sync and cloud, simultaneous multi-video
playback, courses, video management beyond the catalogue, timeline notes, custom lists, Dolby Vision
and audio passthrough, and macOS/Linux. The suite also checks that no migration creates a table for
an excluded capability: an interface can be reverted, a shipped schema cannot.

### The documentation makes claims about the application, and they were checked on it

A document saying "the attribution can be read in Settings" is a verifiable claim, and it was
verified on the packaged artifact rather than on the source. UI Automation over the executable
unpacked from the MSIX, with a data folder of its own and no token. Six steps, **six green**: launch,
go to Settings, find the credits surface, read the TMDB attribution word for word, read the
GPL-3.0-or-later notice, and exit with code 0.

The artifact also carries, checked against the published ZIP: `LICENSE` (33.9 KB), `NOTICE`,
`licenses/THIRD-PARTY-NOTICES.es.md` and `.en.md`, and `sbom/` with both formats. What `NOTICE`
promises is exactly what is inside.

### Verification

`dotnet format --verify-no-changes` clean; Debug and Release with `-warnaserror` at 0 warnings and 0
errors; full Release suite over two runs at **1263 tests, 0 failures, 0 skipped**;
`eng/verify.ps1 -Configuration Release -Runtime win-x64` passed twice; `eng/verify-docs.ps1` 79
Markdown files, 53 identifiers, 46 MVP; accessibility over two passes 0/0/0; recovery over two passes
9 rows, identical; performance 12 of 12 within budget; 0 vulnerable dependencies.
