# ADR-0013 — El programa lleva licencia propia, y su identificador apunta en vez de nombrar / The Program Carries Its Own Licence, and Its Identifier Points Rather Than Names

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-09-13
- Decisor / Decision owner: Product Owner
- Relacionado / Related: [`PRD-005`](../FEATURES.md), [`PLY-016`](../FEATURES.md),
  [`docs/legal/LEGAL.es.md`](../legal/LEGAL.es.md),
  [`LICENSE`](../../LICENSE),
  [la auditoría](../evidence/stable/audit-legal-public.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

El propietario describió lo que quería el 2026-09-13, sin nombrar ninguna licencia: «que todos puedan
usarla gratis, pero es mía, solo yo puedo modificar el código y solo yo decido si vender o no».

**Lo que el proyecto tenía escrito hacía exactamente lo contrario.** `GPL-3.0-or-later` concede a
cualquiera el derecho a modificar el programa, a redistribuirlo y a venderlo — los tres puntos que él
quiere reservarse, regalados por escrito desde el 2026-08-10.

La conversación llegó ahí por otro camino: buscando cómo encender la superresolución de NVIDIA sin
depender del usuario. Esa vía resultó estar cerrada por el lado del controlador —cuatro formas
medidas, ninguna funciona— y abierta por el lado del kit de vídeo RTX, que **exige que el programa no
sea libre**. Así que la licencia apareció como obstáculo técnico antes de que nadie la mirase como
decisión de negocio, y las dos respuestas resultaron ser la misma.

### Decisión

**1. El programa se publica bajo una licencia propia de AP Solutions**, gratuita para quien la use,
sin derecho a modificar, redistribuir ni vender. El texto vive en `LICENSE`, con el español como
versión auténtica y el inglés como cortesía, y el fuero en Santa Cruz de Tenerife.

**2. El identificador SPDX de las cabeceras es `LicenseRef-APSolutions`, sin versión y sin el nombre
del producto**, y las dos ausencias son deliberadas:

- **Sin versión**, porque nombrar la licencia obligó a reescribir 1.033 archivos el día que cambió.
  Un `LicenseRef-` apunta al documento; la versión y la fecha viven dentro de `LICENSE`, que es adonde
  hay que ir de todos modos.
- **Sin el nombre del producto**, porque `SqliteIsolationTests` prohíbe la cadena «Reelume» en los
  cinco archivos que fijan la identidad interna, y la razón de esa regla es del usuario: la carpeta
  donde vive su biblioteca no puede depender de un nombre comercial, o renombrar el producto se la
  movería. Lo descubrió CI en rojo, con `LicenseRef-AP-Reelume` ya escrito en todo el árbol.

**3. El repositorio sigue público**, y la licencia lo reconoce en vez de pelearlo. Los términos de
GitHub conceden por sí mismos ver y **bifurcar** cualquier repositorio público; AP Solutions no lo
concede ni lo puede retirar. La sección 5 de `LICENSE` lo dice y reserva todo lo demás. Sigue público
porque de ello dependen las máquinas de compilación gratuitas, las ARM64 incluidas.

**4. No se aceptan aportaciones de código.** Una sola línea ajena bastaría para que AP Solutions
dejase de poder decidir sola sobre su producto. Las propuestas de cambio quedan limitadas a
colaboradores; los informes de error, abiertos a todo el mundo.

**5. El artefacto no se puede publicar todavía.** El código fuente sí lleva la licencia nueva —el
árbol solo contiene código de AP Solutions—, pero el paquete transporta el decodificador de VideoLAN
compilado con `--enable-gpl`, y un componente contagioso dentro de un programa propietario es un
incumplimiento. **La publicación queda bloqueada hasta que ese motor se compile sin esa opción.**
*Enmendada el 2026-09-14 y el 2026-09-18, al final: esa opción no basta, hace falta que el motor no
lleve código GPL por ninguna vía.*

### Por qué, y qué se descartó

- **Añadir una excepción a la licencia libre**, que es la vía elegante cuando se quiere enlazar con
  algo propietario: descartada, porque solo se puede conceder sobre código propio y el paquete lleva
  código de VideoLAN que no lo es.
- **Poner el repositorio en privado** para cerrar la bifurcación: descartada, porque cuesta los
  runners gratuitos —incluido el ARM64 que este proyecto necesita— y porque no retira las copias
  existentes: una vez público, volverse privado deja las bifurcaciones públicas en una red aparte.
- **Prohibir la bifurcación en la licencia**: descartada por inútil y por dañina. Una cláusula que
  prohíbe lo que la plataforma ya permite debilita las que sí se sostienen.
- **Renunciar al kit de NVIDIA y quedarse en libre**: era la recomendación mientras se creyó que
  cambiar la licencia obligaba a rehacer el reproductor entero. Medido, el motor de vídeo no pierde
  formatos al compilarse sin la opción contagiosa, así que el precio resultó ser trabajo de
  infraestructura y no funcionalidad.

### Lo irreversible, dicho

Lo publicado bajo `GPL-3.0-or-later` hasta el 2026-09-13 **conserva sus derechos para siempre**.
Relicenciar mira adelante y nunca atrás. Fue posible porque los 646 commits del repositorio tienen un
solo autor: no hay copyright ajeno que relicenciar.

### Lo que queda del propietario

- **El dictamen de un abogado**, con los siete puntos marcados al final del borrador. Ninguno bloquea
  el desarrollo; el más delicado es la sección 5 y su convivencia con las bifurcaciones.
- **Si se cobra algún día por el programa**, hay que releer antes los términos de TMDB: reservan el
  uso comercial a un acuerdo escrito aparte. El cambio de licencia no lo activa —lo que decide es
  cobrar— pero acerca el día.

---

## English

### Context

On 2026-09-13 the owner described what he wanted without naming any licence: "everyone can use it for
free, but it is mine, only I may modify the code, and only I decide whether to sell it".

**What the project had written did exactly the opposite.** `GPL-3.0-or-later` grants anyone the right
to modify the program, redistribute it and sell it — the three things he wants to keep, given away in
writing since 2026-08-10.

The conversation arrived there by another road: looking for a way to switch NVIDIA's super resolution
on without depending on the user. That road turned out to be closed on the driver side — four ways
measured, none works — and open on the RTX Video SDK side, which **requires the program not to be
free software**. So the licence showed up as a technical obstacle before anybody looked at it as a
business decision, and both answers turned out to be the same one.

### Decision

**1. The program is published under AP Solutions' own licence**, free of charge to whoever uses it,
with no right to modify, redistribute or sell. The text lives in `LICENSE`, Spanish authentic and
English by courtesy, with jurisdiction in Santa Cruz de Tenerife.

**2. The SPDX identifier in the headers is `LicenseRef-APSolutions`, with no version and without the
product name**, and both absences are deliberate:

- **No version**, because naming the licence forced 1,033 files to be rewritten the day it changed. A
  `LicenseRef-` points at the document; the version and date live inside `LICENSE`, which is where a
  reader has to go anyway.
- **No product name**, because `SqliteIsolationTests` forbids the string "Reelume" in the five files
  that fix the internal identity, and that rule's reason belongs to the user: the folder where their
  library lives cannot depend on a marketing name, or renaming the product would move it. CI found it
  in red, with `LicenseRef-AP-Reelume` already written across the tree.

**3. The repository stays public**, and the licence acknowledges that rather than fighting it.
GitHub's terms grant, of their own force, the right to view and **fork** any public repository; AP
Solutions neither grants nor can withdraw it. Section 5 of `LICENSE` says so and reserves everything
else. It stays public because the free build machines depend on it, the ARM64 ones included.

**4. Code contributions are not accepted.** One line written by somebody else would be enough for AP
Solutions to stop being able to decide about its own product alone. Pull requests are limited to
collaborators; bug reports stay open to everyone.

**5. The artifact cannot be released yet.** The source does carry the new licence — the tree holds
only AP Solutions' code — but the package ships VideoLAN's decoder built with `--enable-gpl`, and a
copyleft component inside a proprietary program is a breach. **Release is blocked until that engine is
built without that option.** *Amended on 2026-09-14 and 2026-09-18, at the end: that option is not
enough; the engine must carry no GPL code by any route.*

### Why, and what was rejected

- **Adding an exception to the free licence**, the elegant route when linking with something
  proprietary: rejected, because it can only be granted over one's own code and the package carries
  VideoLAN's, which is not.
- **Making the repository private** to close forking: rejected, because it costs the free runners —
  the ARM64 one this project needs included — and because it does not retract existing copies: once
  public, going private leaves the public forks in a separate network.
- **Forbidding forking in the licence**: rejected as useless and harmful. A clause forbidding what the
  platform already permits weakens the ones that do hold.
- **Giving up NVIDIA's SDK and staying free software**: this was the recommendation while it was
  believed that changing the licence meant rebuilding the player. Measured, the video engine loses no
  formats when built without the copyleft option, so the price turned out to be infrastructure work
  rather than functionality.

### The irreversible part, said out loud

Everything published under `GPL-3.0-or-later` up to 2026-09-13 **keeps its rights forever**.
Relicensing looks forward, never back. It was possible because the repository's 646 commits have a
single author: there is no third-party copyright to relicense.

### What stays with the owner

- **A lawyer's opinion**, with the seven points marked at the end of the draft. None blocks
  development; the most delicate is section 5 and how it lives beside forking.
- **If the program is ever charged for**, TMDB's terms must be read again first: they reserve
  commercial use for a separate written agreement. The licence change does not trigger it — charging
  does — but it brings the day closer.

---

## Enmienda del 2026-09-14 — la decisión 5 tiene una condición que no se había visto / Amendment of 2026-09-14 — decision 5 carries a condition nobody had seen

**La decisión 5 no cambia y sigue vigente**: la publicación está bloqueada hasta que el motor se
compile sin `--enable-gpl`. Lo que esta enmienda añade es **una condición que aparece al ejecutarla** y
que el ADR no podía prever, porque sólo se ve compilando. / Decision 5 stands unchanged; this
amendment adds a condition that only appears when carrying it out.

**Medido el 2026-09-14** (`docs/evidence/stable/ENG013-nogpl-build-spike.md`): compilar sin GPL es
viable y no pierde formatos —330 complementos, cero con la cadena contagiosa, decodificación real
comprobada—, **pero `freetype2` se queda fuera**. Su receta en el árbol de VLC exige GPL salvo que se
acepten cláusulas de publicidad, y `freetype2` es quien dibuja el texto de los subtítulos.

**La licencia de FreeType es dual y eso lo resuelve**: la FTL —tipo BSD, con cláusula de publicidad— o
la GPL-2.0, a elección de quien la usa, y su propio `LICENSE.TXT` dice que la primera «is suited to
products which don't use the GNU General Public License». **El propietario eligió la FTL el
2026-09-14.** El precio es reconocer el uso de FreeType en la documentación del producto, que es lo que
hace ahora la sección propia de `docs/release/THIRD-PARTY-NOTICES.{es,en}.md`.

**Y la obligación no nacía con LibVLC: ya existía.** FreeType viaja hoy dentro de los recursos nativos
de Skia, y su reconocimiento **faltaba** — medido al escribir esta enmienda. Así que la elección de la
FTL cubre dos caminos, no uno, y cierra un incumplimiento que estaba abierto sin que nadie lo contara.
/ The obligation predates LibVLC: FreeType already travels inside Skia's native assets and its
acknowledgement was missing. Choosing the FTL covers both paths and closes a breach nobody was counting.

---

## Enmienda del 2026-09-18 — «sin `--enable-gpl`» no es «sin GPL», y cómo se compila / Amendment of 2026-09-18 — "without `--enable-gpl`" is not "without GPL", and how it is built

**La decisión 5 se corrige en su condición**: la publicación queda bloqueada hasta que el motor **no
lleve código GPL por ninguna vía**, no sólo hasta que FFmpeg se compile sin `--enable-gpl`. Medido
(`docs/evidence/stable/audit-eng027-plugin-gpl-sources.md`): el configure de VLC no tiene interruptor
de GPL para sus propios módulos, y el paquete que se distribuye hoy lleva catorce complementos GPL en
x64 por tres vías —FFmpeg compilado como GPL, módulos del propio VLC con fuentes GPL y una biblioteca
GPL enlazada dentro de un módulo LGPL—. La cadena `--enable-gpl` sólo ve la primera. / Decision 5's
condition is corrected: release is blocked until the engine carries no GPL code by any route, not
only until FFmpeg is built without `--enable-gpl`, which is one of three routes.

**Y se decide cómo se compila** (`docs/evidence/stable/ENG013-reproducible-build.md`): como compila
VideoLAN, con su `extras/package/win32/build.sh` y dentro de las imágenes de CI que nombra su
`extras/ci/gitlab-ci.yml` en el tag, en el flujo `libvlc-nogpl.yml`, aparte del CI principal y una vez
por versión de VLC. Los módulos GPL se retiran leyendo sus fuentes, nunca con una lista a mano, y dos
parches mínimos (quitar yadif del desentrelazador, no pedir dvdread) viajan en `eng/libvlc/patches/`.
**Descartado** compilarlo en MSYS2, como hizo el spike: siete de sus nueve trampas eran herramientas de
2026 contra código de 2014-2018, y la misma vía de VideoLAN cruza también a ARM64. / It is built the
way VideoLAN builds it, inside their CI images, in a workflow of its own, once per VLC version; GPL
modules are dropped by reading their sources, never from a hand list. MSYS2 is rejected.

**Lo que queda abierto** es de `ENG-013` (llevar el árbol a la aplicación) y de `ENG-028` (leer la
LGPL-3.0 de tres bibliotecas que el motor ya lleva). / What remains is ENG-013's (bringing the tree
into the application) and ENG-028's (reading the LGPL-3.0 of three libraries the engine already
carries).

### Enmienda del 2026-09-18: la licencia lleva la excepción que la LGPL exige / Amendment of 2026-09-18: the licence carries the exception the LGPL requires

Al leer la LGPL-3.0 (`ENG-028`) salió algo que esta decisión no vio: la LGPL-2.1 §6 y la LGPL-3.0 §4
sólo dejan usar la biblioteca a un programa de licencia propia si sus condiciones permiten modificarlo
para uso propio e ingeniería inversa para depurar esas modificaciones, y `LICENSE` 2.1, 2.4 y la
cláusula 3 lo prohibían. El propietario aprobó una excepción expresa, limitada a eso y sin derecho a
redistribuir; `LicenceTextTests` falla si se quita. El detalle está en `LEGAL`. / Reading the LGPL-3.0
showed what this decision missed: LGPL-2.1 §6 and LGPL-3.0 §4 only let a program under its own
licence use the library if its terms permit modifying it for one's own use and reverse engineering to
debug that, and `LICENSE` forbade both. The owner approved an express exception, limited to that and
without a right to redistribute; `LicenceTextTests` fails if it is removed.

---

## Enmienda del 2026-09-18, segunda — la aplicación lleva el motor propio, y dónde vive / Second amendment of 2026-09-18 — the application ships the engine of its own, and where it lives

**La aplicación deja de referenciar `VideoLAN.LibVLC.Windows`.** El motor llega del árbol que
`libvlc-nogpl.yml` compila y verifica, **promovido sin recompilar** por `libvlc-publish.yml` a una
**prerelease de este mismo repositorio** y fijado por el SHA-512 de cada archivo en
`eng/libvlc/libvlc.lock.json`. `eng/libvlc/LibVlc.targets` lo descarga, rechaza cualquier otro byte y lo
copia con la misma forma que tenía el paquete. El sitio lo eligió el propietario el mismo día: es
permanente, se descarga sin credenciales y el actualizador no puede confundirlo con una versión de la
aplicación, porque sólo pregunta por `releases/latest`, que por documentación de GitHub excluye las
prereleases. / The application stops referencing the NuGet package. The engine comes from the tree
the no-GPL workflow built and verified, promoted without rebuilding to a prerelease of this
repository and pinned by SHA-512; the owner chose the place the same day.

**Descartado**: guardar los binarios en el repositorio con Git LFS (cuota y peso sin ganar nada) y
recompilar el motor en cada run de CI (media hora por arquitectura y un binario distinto cada vez, sin
nada fijo que auditar). / Rejected: Git LFS, and rebuilding the engine on every CI run.

**El motor está modificado**, y por eso los ficheros que tocan los dos parches llevan un aviso con
fecha, como pide el §2(b) de la LGPL-2.1. Su código fuente correspondiente —VLC en el tag, los parches
y guiones en el commit que compiló, y cada tarball de terceros que usó— se publica con el motor y
viaja con cada versión de la aplicación. / The engine is modified, the patched files say so with a
date (LGPL-2.1 §2(b)), and its corresponding source travels with every release.

**Lo que queda abierto** es `ENG-028`. / What remains open is ENG-028.
