# Los textos de licencia dentro del artefacto / The licence texts inside the artifact

Evidencia del cierre del único incumplimiento legal que la revisión del 2026-08-10 dejó abierto: el
artefacto nombraba las licencias de terceros y no entregaba ninguna. / Evidence for closing the one
legal breach the 2026-08-10 review left open: the artifact named the third-party licences and
delivered none of them.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## Qué obligaba y qué faltaba / What was required and what was missing

Nombrar el componente en una tabla no cumple ninguna de estas cláusulas. / Naming the component in a
table meets none of these clauses.

| Cláusula / Clause | Qué exige / What it requires | Componentes / Components |
|---|---|---|
| LGPL-2.1 §6 | Acompañar una copia de la licencia / Accompany a copy of the licence | LibVLCSharp, libvlc, libvlccore |
| GPL-2.0 §1 | Acompañar una copia de la licencia / Accompany a copy of the licence | Complementos de VLC / VLC plugins |
| Apache-2.0 §4a | Entregar una copia de la licencia / Give a copy of the licence | SQLitePCLRaw (4 paquetes / packages) |
| MIT | Reproducir el aviso de copyright / Reproduce the copyright notice | Avalonia (12), SkiaSharp, HarfBuzzSharp, MicroCom, Tmds.DBus.Protocol, BouncyCastle, Microsoft (4 + el motor / + the runtime) |
| BSD-3-Clause | Reproducir el aviso y las condiciones / Reproduce the notice and the conditions | ANGLE, Skia |

El paquete NuGet de VideoLAN **no trae ningún `COPYING`**, comprobado archivo por archivo sobre el
paquete restaurado, así que nadie aportaba el texto. / VideoLAN's NuGet package carries **no
`COPYING` at all**, verified file by file over the restored package, so nobody was supplying it.

## Rojo archivado / Red archived

Las pruebas se escribieron antes que los archivos, contra el paquete construido en la sesión
anterior. / The tests were written before the files, against the package built in the previous
session.

| Suite | Resultado / Result |
|---|---|
| `LicenceTextTests` | 16 con error, 1 superada de 17 / 16 failed, 1 passed of 17 |
| `ArtifactContentsTests`, `Arm64PackageTests` | 2 con error de 2 / 2 failed of 2 |

El mensaje que archivan es el que describe el hueco: *«The versioned licence texts are not at
docs/release/licenses, so nothing states what the artifact owes.»* / The message they archive is the
one that describes the gap.

## Verde / Green

| Suite | Resultado / Result |
|---|---|
| `ApSolutions.LocalMedia.DocumentationTests` | 84 de 84 / 84 of 84 |
| `ArtifactContentsTests` | 16 de 16 / 16 of 16 |

## Qué se añadió y cuánto pesa / What was added and what it weighs

Quince archivos, 213 685 bytes (209 KiB), en `docs/release/licenses/`, que los dos guiones de
empaquetado copian enteros a `licenses/` dentro del artefacto. / Fifteen files, 213,685 bytes
(209 KiB), in `docs/release/licenses/`, which both packaging scripts copy whole into `licenses/`
inside the artifact.

| Artefacto / Artifact | Archivos en el payload / Files in the payload | Tamaño / Size | En `licenses/` |
|---|---|---|---|
| `win-x64` | 687 | 239,4 MB | 17 |
| `win-arm64` | 674 | 227,7 MB | 17 |

Los diecisiete son los quince versionados más los dos avisos de terceros que ya viajaban. La
comparación de paridad entre las dos compilaciones da 0 diferencias de aplicación, de modo que ningún
texto llega a una arquitectura y no a la otra. / The seventeen are the fifteen versioned files plus
the two third-party notices that already travelled. The parity comparison between the two builds
reports 0 application differences, so no text reaches one architecture and not the other.

## Procedencia de cada texto, contrastada / Provenance of each text, contrasted

Un texto de licencia escrito de memoria no es una copia de la licencia. Cada canónico se tomó de una
fuente que ya lo distribuía y se comparó con una segunda copia independiente antes de aceptarlo. /
A licence text written from memory is not a copy of the licence. Each canonical text was taken from a
source that already distributed it and compared with a second, independent copy before being
accepted.

| Archivo / File | sha256 (16) | Origen / Source | Segunda copia / Second copy | Resultado / Result |
|---|---|---|---|---|
| `LGPL-2.1.txt` | `dc626520dcd53a22` | Directorio SPDX de Blender / Blender's SPDX directory | `xz` de Git para Windows / Git for Windows' `xz` | Idénticas / Identical |
| `Apache-2.0.txt` | `c71d239df91726fc` | Directorio SPDX / SPDX directory | `dotnet-reportgenerator` | Idénticas salvo el apéndice ya rellenado / Identical but for a filled-in appendix |
| `GPL-2.0.txt` | `8177f97513213526` | `vlc_about.h` del paquete de VideoLAN / from VideoLAN's package | `COPYING` de HandBrake | Idénticas salvo una línea en blanco final / Identical but for a trailing blank line |
| `BSD-3-Clause.txt` | `7c0bceba5d7abaa6` | Directorio SPDX / SPDX directory | `NOTICE-ANGLE.txt`, que es una instancia con titular / an instance with a holder | Coincide el articulado / The conditions agree |
| `MIT.txt` | `3097903dadcbda42` | Texto canónico más los avisos de los metadatos / Canonical text plus the notices from package metadata | `NOTICE-SkiaSharp.txt`, `NOTICE-BouncyCastle.txt` | Mismo articulado, distinto ajuste de línea / Same wording, different wrapping |

El de la GPL-2.0 se extrajo del literal C que `vlc_about.h` compila dentro de `libvlc`: es la
licencia que VLC muestra de sí mismo, que es exactamente la que obliga a sus complementos, y lleva la
cláusula «or (at your option) any later version» que ya se había comprobado. / The GPL-2.0 text was
extracted from the C literal `vlc_about.h` compiles into `libvlc`: the licence VLC displays for
itself, which is exactly the one binding its plugins, carrying the "or (at your option) any later
version" clause already checked.

## Copias literales, comparadas contra el paquete restaurado / Verbatim copies, compared against the restored package

Estas no se transcriben: se copian del paquete NuGet que la compilación consumió, y
`LicenceTextTests` las compara byte a byte contra él en cada ejecución. Una subida de versión que
cambie un aviso pone la prueba en rojo en vez de dejar el artefacto distribuyendo el aviso de la
versión anterior. / These are not transcribed: they are copied from the NuGet package the build
consumed, and `LicenceTextTests` compares them byte for byte against it on every run.

| Archivo / File | sha256 (16) | Paquete / Package |
|---|---|---|
| `NOTICE-ANGLE.txt` | `bf4da21bd20bcfb5` | `Avalonia.Angle.Windows.Natives` 2.1.27548.20260419 |
| `NOTICE-BouncyCastle.txt` | `0e01f1549c9022f4` | `BouncyCastle.Cryptography` 2.7.0 |
| `NOTICE-HarfBuzzSharp.txt` | `bc2eb4f37d574f9b` | `HarfBuzzSharp` 8.3.1.3 |
| `NOTICE-SkiaSharp.txt` | `bc2eb4f37d574f9b` | `SkiaSharp` 3.119.4 |
| `NOTICE-Skia-HarfBuzz-natives.txt` | `98acf9d4d6083959` | `SkiaSharp.NativeAssets.Win32` 3.119.4 |
| `NOTICE-SQLite.txt` | `6ea0f46456e63170` | `SQLitePCLRaw.lib.e_sqlite3` 3.53.3 |

El aviso de los binarios nativos son 137 KB y 24 secciones: ANGLE, HarfBuzz, skia, etc1, gif, libpng,
DNG SDK, expat, freetype, ICU, imgui, jsoncpp, libjpeg-turbo, libwebp, libmicrohttpd, piex, sdl,
sfntly, SPIR-V Headers, SPIR-V Tools y zlib entre ellas. Nada de eso aparecía en ningún documento del
proyecto: `libSkiaSharp.dll` los lleva dentro y el artefacto los distribuía sin su aviso. / The
notice for the native binaries is 137 KB and 24 sections. None of it appeared in any project
document: `libSkiaSharp.dll` carries them inside and the artifact was distributing them without their
notice.

Los avisos compuestos —`MIT.txt`, `NOTICE-SQLitePCLRaw.txt`, `NOTICE-VideoLAN.txt`— existen porque
esos paquetes no publican ninguno. Cada uno reproduce el copyright que su propio paquete declara en
los metadatos, y la prueba lo lee del `.nuspec` restaurado en vez de darlo por bueno. /
The assembled notices exist because those packages publish none. Each reproduces the copyright its
own package declares in its metadata, and the test reads it from the restored `.nuspec` rather than
taking it on trust.

## Qué queda abierto / What stays open

De entrega, nada. De forma, dos preguntas para el dictamen de `REL-004`: bajo qué apartado del §6 de
la LGPL-2.1 queda amparada la manera en que LibVLC viaja aquí —biblioteca dinámica sin modificar y
sustituible— y si la oferta escrita de código correspondiente que recoge `NOTICE-VideoLAN.txt`,
válida tres años, basta como el acompañamiento que el §3 de la GPL-2.0 pide por los complementos. /
On delivery, nothing. On form, two questions for the `REL-004` opinion: which subsection of
LGPL-2.1 §6 covers the way LibVLC travels here — an unmodified, replaceable dynamic library — and
whether the written offer of corresponding source recorded in `NOTICE-VideoLAN.txt`, valid for three
years, is enough as the accompaniment GPL-2.0 §3 asks for on behalf of the plugins.
