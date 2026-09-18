# Avisos de terceros

AP Reelume by AP Solutions se publica bajo una licencia propia, `LicenseRef-APSolutions`, cuyo texto
está en `LICENSE`. Este documento recoge los componentes
de terceros que el artefacto publicado transporta y la licencia que cada uno declara. Este archivo se
actualiza en cada incremento que añade o retira una dependencia, y **viaja dentro del artefacto**, en
`licenses/`, junto a su versión inglesa.

El inventario de materiales de la compilación exacta que usted ejecuta está en `sbom/`, dentro del
mismo artefacto, en formatos CycloneDX 1.5 y SPDX 2.3. Se genera desde los ficheros de bloqueo, de
modo que describe lo que la compilación resolvió y no lo que los proyectos piden. Las tablas de abajo
se contrastan con ese inventario en `ThirdPartyNoticeTests`, así que una dependencia no puede entrar
en el artefacto sin aparecer aquí.

## Componentes distribuidos con la aplicación

### Bibliotecas gestionadas y sus recursos nativos

Todos los componentes de esta tabla viajan dentro de los artefactos `win-x64` y `win-arm64`. Las
dependencias transitivas se listan por su nombre porque a una obligación de licencia no le importa si
el paquete se pidió directamente o llegó arrastrado.

| Componente | Versión | Licencia declarada |
|---|---|---|
| Avalonia | 12.1.1 | MIT |
| Avalonia.Desktop | 12.1.1 | MIT |
| Avalonia.Themes.Fluent | 12.1.1 | MIT |
| Avalonia.BuildServices | 11.3.2 | MIT |
| Avalonia.FreeDesktop | 12.1.1 | MIT |
| Avalonia.FreeDesktop.AtSpi | 12.1.1 | MIT |
| Avalonia.HarfBuzz | 12.1.1 | MIT |
| Avalonia.Native | 12.1.1 | MIT |
| Avalonia.Remote.Protocol | 12.1.1 | MIT |
| Avalonia.Skia | 12.1.1 | MIT |
| Avalonia.Win32 | 12.1.1 | MIT |
| Avalonia.X11 | 12.1.1 | MIT |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | BSD-3-Clause, de The ANGLE Project Authors |
| SkiaSharp | 3.119.4 | MIT |
| SkiaSharp.NativeAssets.Win32 | 3.119.4 | MIT, sobre Skia, que es BSD-3-Clause de Google |
| HarfBuzzSharp | 8.3.1.3 | MIT |
| HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.3 | MIT, sobre HarfBuzz, que es MIT |
| MicroCom.Runtime | 0.11.6 | MIT |
| Tmds.DBus.Protocol | 0.94.1 | MIT |
| BouncyCastle.Cryptography | 2.7.0 | MIT |
| LibVLCSharp | 3.10.0 | LGPL-2.1-or-later |
| LibVLC, compilado por AP Solutions sin GPL | 3.0.23-nogpl.1 | LGPL-2.1-or-later |
| GNU MP (GMP), dentro de cuatro complementos de LibVLC | 6.3.0 | LGPL-3.0-or-later, elegida de su doble licencia con GPL-2.0-or-later |
| GNU Nettle, dentro de cuatro complementos de LibVLC | 3.7.3 | LGPL-3.0-or-later, elegida de su doble licencia con GPL-2.0-or-later |
| LIVE555 Streaming Media, dentro de un complemento de LibVLC | 2016.11.28 | LGPL-3.0-or-later |
| Microsoft.Data.Sqlite | 10.0.10 | MIT |
| Microsoft.Data.Sqlite.Core | 10.0.10 | MIT |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.11 | Apache-2.0 |
| SQLitePCLRaw.core | 2.1.11 | Apache-2.0 |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.11 | Apache-2.0 |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 | Apache-2.0 sobre SQLite, que es de dominio público |
| Microsoft.Extensions.DependencyInjection | 10.0.10 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.10 | MIT |

Una licencia propietaria es compatible con la incorporación de componentes `LGPL-2.1-or-later`, `MIT`,
`Apache-2.0` y `BSD-3-Clause`. Las licencias MIT y BSD-3-Clause exigen que su aviso de copyright
viaje con el binario, y para eso están este archivo y la carpeta `licenses/` dentro del artefacto.

Esa carpeta lleva además **el texto íntegro de cada licencia** y los avisos de copyright que cada
paquete publica. Qué contiene y de dónde salió cada texto está en
[licenses/README.es.md](licenses/README.es.md), que viaja con ellos.

### FreeType, y por qué lleva su propio reconocimiento

> Portions of this software are copyright © The FreeType Project (www.freetype.org).
> All rights reserved.

**Esta frase es una obligación, no una cortesía**, y esta sección existe para cumplirla. FreeType se
distribuye bajo **dos licencias mutuamente excluyentes** y hay que elegir una: la **FreeType License**
(FTL), parecida a BSD, o la **GPL-2.0**. AP Solutions elige la **FTL**, decidido por el propietario el
2026-09-14, porque la otra vía es incompatible con una licencia propia — y el propio `LICENSE.TXT` de
FreeType dice que la FTL «is suited to products which don't use the GNU General Public License».

El precio de esa vía es su cláusula de publicidad, que pide literalmente «acknowledge somewhere in
your documentation that you have used the FreeType code». Eso es lo que hace el recuadro de arriba.

**Y viaja por dos caminos, uno de ellos desde antes de esta decisión**: hoy llega dentro de los
recursos nativos de Skia —su texto completo está en
[`licenses/NOTICE-Skia-HarfBuzz-natives.txt`](licenses/NOTICE-Skia-HarfBuzz-natives.txt)—, y desde el
2026-09-18 también dentro de LibVLC, que se compila sin GPL (`ENG-013`) y la usa para dibujar el texto
de los subtítulos. El reconocimiento se escribe una vez y cubre los dos.

### El motor de ejecución de .NET

El artefacto es autocontenido: lleva su propia copia del motor de ejecución de .NET 10 y de su
biblioteca base (`coreclr.dll`, `System.*.dll`, `mscorlib.dll` y sus acompañantes), más la proyección
del SDK de Windows (`Microsoft.Windows.SDK.NET.dll`, `WinRT.Runtime.dll`). Todo ello lo publica
Microsoft bajo `MIT`. Nadie tiene que instalar un motor de ejecución para usar AP Reelume, y esa
comodidad es lo que mete varios cientos de archivos con licencia de Microsoft dentro del paquete.

### LibVLC, su núcleo y sus complementos

**Desde el 2026-09-18 el motor no es el paquete de VideoLAN, sino una compilación propia sin GPL**
(`ENG-013`). Se construye desde la misma versión de VLC, la 3.0.23, con el guion y las imágenes de
compilación de VideoLAN, las bibliotecas de terceros configuradas con `--disable-gpl` y FreeType bajo su
FTL. Después se retira todo complemento cuyo código fuente sea GPL, y una puerta exige que no quede
ninguno. La publica este repositorio como `libvlc-3.0.23-nogpl.1`, fijada por el hash de cada archivo.
**Está modificada**, y las dos modificaciones son quitar piezas GPL: el algoritmo de desentrelazado
yadif del complemento `libdeinterlace` y la biblioteca libdvdread del guion de compilación. Los
ficheros tocados lo dicen en su cabecera, con fecha, como pide el §2(b) de la LGPL-2.1.

Todo lo que viaja —`libvlc.dll`, `libvlccore.dll` y los complementos de `plugins/`— es
`LGPL-2.1-or-later`. El texto viaja en `licenses/LGPL-2.1.txt`, y en `licenses/NOTICE-VideoLAN.txt` el
detalle de la compilación y dónde está su código fuente. Qué complementos faltan respecto al paquete de
VideoLAN, y por qué, lo registra el `manifest.json` que se publica con el motor: los que eran GPL y
los que ni ese paquete ni esta compilación usan.

**Tres bibliotecas de terceros van enlazadas dentro de cinco complementos bajo `LGPL-3.0-or-later`**,
y el paquete de VideoLAN también las llevaba: GNU MP y GNU Nettle dentro de `libgnutls`,
`libaccess_srt`, `libaccess_output_srt` y `libdcp`, y LIVE555 dentro de `liblive555`, en las dos
arquitecturas. GMP y Nettle se ofrecen con doble licencia, LGPL-3.0 o GPL-2.0, y se usa la LGPL. Sus
avisos de copyright y el detalle están en `licenses/NOTICE-VideoLAN.txt`, y los textos en
`licenses/LGPL-3.0.txt` y `licenses/GPL-3.0.txt`, porque la LGPL-3.0 está escrita sobre la GPL-3.0 y
pide las dos. Cómo encaja esa licencia con un programa propietario se leyó el 2026-09-18 (`ENG-028`) y
está en `LEGAL`.

**Y «todo es LGPL-2.1» es la licencia de VLC, no el inventario de lo que llevan dentro sus
complementos.** Además de esas tres, van enlazadas otras bibliotecas de terceros con sus propias
licencias —SRT, por ejemplo, es MPL-2.0—, y ese inventario completo está sin hacer (`ENG-029`).

**Lo que había antes, y por qué hubo que cambiarlo.** El paquete `VideoLAN.LibVLC.Windows` 3.0.23.1
llevaba catorce complementos GPL en x64 y once en ARM64. `libavcodec_plugin.dll` y
`libswscale_plugin.dll` lo eran porque su FFmpeg se compiló con `--enable-gpl`. Once más lo eran porque
su código fuente es GPL, entre ellos `liblua`, `libdeinterlace` y `libhqdn3d`, y `libts_plugin.dll`
porque enlaza aribb24. La lista sale de `eng/libvlc/scan-plugin-licenses.ps1` (evidencia
`audit-eng027-plugin-gpl-sources.md`).

**Esto estuvo cerrado desde el 2026-08-10 y se reabrió el 2026-09-13, y el motivo no fue un error.**
El razonamiento de entonces era: para un programa publicado bajo `GPL-3.0-or-later`, un complemento
`GPL-2.0-or-later` es compatible, porque el «o posterior» hace que ambos se encuentren en GPL-3.0.
Era correcto y lo sigue siendo. **Lo que cambió es el programa**: desde el 2026-09-13 lleva licencia
propia, así que no hay ninguna versión común a la que llegar, y un complemento contagioso dentro de
un programa propietario es un incumplimiento, no un encaje.

**Y la palanca de recortar complementos ya no es opcional: es la única salida.** De los catorce, los
que importan **no son el codificador x264** —un reproductor no codifica y ése se puede quitar—, sino
**`libavcodec_plugin.dll` y `libswscale_plugin.dll`, los decodificadores**: su línea de compilación
empieza por `--enable-gpl`, leída dentro de los dos binarios. Los módulos GPL de VLC son funciones que
la aplicación no ofrece, un algoritmo de desentrelazado que no es el de por defecto y una conversión
de color que `libswscale` también hace; `libts` sin aribb24 sigue leyendo `.ts`.

**Mientras esos complementos viajaran aquí, el artefacto no se podía distribuir, y eso es lo que cerró
la compilación propia.** No pierde formatos: la biblioteca que descodifica es permisiva por defecto, y
lo contagioso eran piezas opcionales que se activan al compilar —un filtro de posprocesado heredado y
algunas optimizaciones—, **sin ningún decodificador entre ellas**. Medido en Windows x64 y ARM64
reales: los catorce códecs prometidos decodifican con las mismas cifras que el paquete de VideoLAN,
subtítulos incluidos (evidencia `ENG013-reproducible-build.md`).

**El texto de las licencias ya viaja.** La LGPL-2.1 (§6), la GPL-2.0 (§1) y la Apache-2.0 (§4a)
exigen entregar una copia de la licencia con la distribución binaria, y MIT y BSD-3-Clause exigen
reproducir su aviso de copyright. Nombrar el componente y su licencia, que es lo que hace este
documento, no sustituye a acompañarla, así que la carpeta `licenses/` del paquete lleva los textos
íntegros junto a estos avisos. Los que un paquete publica se copian literalmente de él y una prueba
los compara byte a byte contra el paquete que la compilación consumió; los canónicos se tomaron de
una fuente que ya los distribuía y se contrastaron con una segunda copia independiente. El detalle
está en [licenses/README.es.md](licenses/README.es.md).

### Código portado, que ninguna puerta automática ve

**Esto se escribe a mano y por eso está separado.** Las demás filas de este documento salen de
`packages.lock.json` y `ThirdPartyNoticeTests` las exige; un fichero fuente **portado** no es una
dependencia empaquetada, así que no aparece en ningún fichero de bloqueo y ninguna prueba lo echaría
de menos. La obligación de la licencia es la misma.

| Origen | Fichero de origen | Licencia declarada | Qué se trajo |
| --- | --- | --- | --- |
| VideoLAN / VLC | `modules/video_output/win32/d3d11_scaler.cpp` | `LGPL-2.1-or-later` | Los identificadores de las extensiones de superresolución de NVIDIA e Intel, sus cargas útiles y la secuencia de llamadas del procesador de vídeo de Direct3D 11, en `src/ApSolutions.LocalMedia.Windows/Playback/`. |
| The Chromium Authors | `ui/gl/swap_chain_presenter.cc` | `BSD-3-Clause` | Por qué puerta va cada llamada de Intel —las dos primeras son extensiones de salida y sólo la tercera de flujo— y que el controlador de NVIDIA acepta la petición y la ignora mientras la función esté apagada. |

Una licencia propietaria admite incorporar las dos. Los textos íntegros de `LGPL-2.1` y `BSD-3-Clause` ya
viajan en `licenses/` por otras dependencias, así que no hace falta añadir ninguno.

## Componentes usados solo durante el desarrollo y las pruebas

Estos no entran nunca en un artefacto. Lo construyen, lo prueban o lo miden.

| Componente | Versión | Licencia declarada |
|---|---|---|
| Avalonia.Headless.XUnit | 12.1.1 | MIT |
| BenchmarkDotNet | 0.15.8 | MIT |
| coverlet.collector | 10.0.1 | MIT |
| FlaUI.Core | 5.0.0 | MIT |
| FlaUI.UIA3 | 5.0.0 | MIT |
| FsCheck | 3.3.4 | BSD-3-Clause |
| Microsoft.NET.Test.Sdk | 18.8.1 | MIT |
| NSubstitute | 6.0.0 | BSD-3-Clause |
| xunit.v3 | 3.2.2 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 |

## Versiones declaradas y no consumidas

`Directory.Packages.props` fija dos versiones que hoy ningún proyecto referencia. Se conservan
declaradas para que una adopción futura no introduzca un rango flotante, y no forman parte de ningún
artefacto:

- `LibVLCSharp.Avalonia` 3.10.0 — no se adopta porque apunta a Avalonia 11.x y expondría el objeto
  reproductor del motor a la vista.
- `NetArchTest.Rules` 1.3.2 — no se restaura en la solución actual; las reglas de arquitectura se
  comprueban leyendo los archivos de proyecto.

## Herramientas externas no redistribuidas

La matriz de contenedores y códecs se genera con **FFmpeg**, que debe estar instalado en la máquina
de desarrollo y se localiza por `FFMPEG_PATH` o por `PATH`. FFmpeg no se incluye en el repositorio ni
en ningún artefacto publicado, y su licencia depende de la compilación que cada persona instale. Las
muestras que produce proceden de sus generadores sintéticos `testsrc2` y `sine`, de modo que el
contenido resultante no incorpora obra de terceros.

## Contenido multimedia

Ningún archivo de vídeo, audio o subtítulos está versionado. La biblioteca personal nunca se lee ni
se copia durante las pruebas.

## Lo que este documento no resuelve

Este archivo dice qué declara cada componente y cómo encajan esas declaraciones entre sí. Lo escriben
quienes ensamblaron el programa, no un abogado, y dos preguntas siguen abiertas hasta que el dictamen
jurídico profesional de REL-004 las responda. **La primera se cerró por ingeniería el 2026-09-18**:
desde el 2026-09-13 era un hallazgo —los complementos GPL del paquete de VideoLAN no eran compatibles
con la licencia propia y frenaban la publicación—, y desde el 2026-09-18 el motor se compila sin ellos.
La `LGPL-3.0` de tres bibliotecas se leyó el mismo día (`ENG-028`): tanto esa licencia como la
LGPL-2.1 piden que el programa se pueda modificar para uso propio y depurar con ingeniería inversa, y
`LICENSE` lo permite desde entonces con una excepción expresa. La segunda sigue siendo
pregunta: bajo qué apartado
del §6 de la LGPL-2.1 queda amparada la forma en que LibVLC viaja aquí, ahora que la vía del §6(a)
—publicar nuestro fuente bajo licencia libre— ya no está disponible. Ambas se nombran aquí para que
nadie confunda este documento con el dictamen.
