# Textos de licencia que viajan en el artefacto

Esta carpeta es la que el empaquetado copia dentro del paquete como `licenses/`. No es documentación
sobre las licencias: **es la entrega de las licencias**. La versión inglesa está en
[README.en.md](README.en.md).

Los [avisos de terceros](../THIRD-PARTY-NOTICES.es.md) dicen qué componente lleva qué licencia. Eso
no basta. La LGPL-2.1 (§6), la GPL-2.0 (§1) y la Apache-2.0 (§4a) exigen **acompañar** la copia de la
licencia con la distribución binaria, y la MIT y la BSD-3-Clause exigen reproducir el aviso de
copyright. Nombrar el componente en una tabla no es ninguna de las dos cosas.

## Qué hay aquí

| Archivo | Qué es | A qué obliga |
|---|---|---|
| `Apache-2.0.txt` | Texto canónico | SQLitePCLRaw (§4a) |
| `BSD-3-Clause.txt` | Texto canónico | ANGLE y Skia |
| `GPL-2.0.txt` | Texto canónico | Complementos de VLC (§1) |
| `LGPL-2.1.txt` | Texto canónico | LibVLC, libvlccore y LibVLCSharp (§6) |
| `MIT.txt` | Texto canónico y los avisos de quienes no publican el suyo | Avalonia, MicroCom, Tmds.DBus.Protocol, Microsoft y el motor de .NET |
| `NOTICE-ANGLE.txt` | Copia literal del paquete | Avalonia.Angle.Windows.Natives |
| `NOTICE-BouncyCastle.txt` | Copia literal del paquete | BouncyCastle.Cryptography |
| `NOTICE-HarfBuzzSharp.txt` | Copia literal del paquete | HarfBuzzSharp |
| `NOTICE-SkiaSharp.txt` | Copia literal del paquete | SkiaSharp |
| `NOTICE-Skia-HarfBuzz-natives.txt` | Copia literal del paquete | Todo lo que Skia y HarfBuzz llevan dentro: ANGLE, freetype, ICU, libpng, libwebp, zlib y veinte más |
| `NOTICE-SQLite.txt` | Copia literal del paquete | SQLite (dominio público) |
| `NOTICE-SQLitePCLRaw.txt` | Aviso compuesto | SQLitePCLRaw |
| `NOTICE-VideoLAN.txt` | Aviso compuesto | LibVLC y sus complementos |

No hay `GPL-3.0.txt`: la licencia del propio programa viaja como `LICENSE` en la raíz del paquete,
que es donde se busca.

## De dónde salió cada texto

Un texto de licencia escrito de memoria no es una copia de la licencia. Cada uno se tomó de una
fuente que ya lo distribuía y se contrastó con una segunda copia independiente antes de aceptarlo:

- **LGPL-2.1** y **Apache-2.0**: del directorio SPDX de una instalación de Blender, contrastados
  respectivamente con la copia que Git para Windows distribuye con `xz` (idéntica byte a byte) y con
  la que trae `dotnet-reportgenerator` (idéntica salvo el apéndice ya rellenado con su titular).
- **GPL-2.0**: del propio árbol de VLC. El paquete de VideoLAN la lleva como la cadena que
  `vlc_about.h` compila dentro de `libvlc`; se extrajo de ahí y se contrastó con la copia que
  distribuye HandBrake, y coinciden salvo una línea en blanco final. Es la licencia que VLC muestra
  de sí mismo, que es exactamente la que obliga a sus complementos.
- **BSD-3-Clause**: del mismo directorio SPDX. Su reproducción con titular concreto es
  `NOTICE-ANGLE.txt`, que es el archivo que el propio paquete de ANGLE publica.
- **MIT**: el texto canónico, con los avisos de copyright que cada paquete declara en sus metadatos.

Las copias literales se toman del paquete NuGet restaurado, no se transcriben.
`LicenceTextTests` las compara byte a byte contra el paquete que la compilación consumió, de modo que
una subida de versión que cambie el aviso pone la prueba en rojo en vez de dejar el artefacto
distribuyendo el aviso de la versión anterior.

## Qué prueba lo fija

- `LicenceTextTests` — cada licencia que los avisos declaran tiene su texto aquí, cada texto está
  entero, cada copia literal coincide con su paquete y ningún identificador nuevo pasa sin archivar.
- `ArtifactContentsTests` y `Arm64PackageTests` — todo lo de esta carpeta llega a `licenses/` dentro
  de los dos artefactos, con el mismo contenido.

## Lo que sigue abierto

El paquete lleva las licencias; el dictamen jurídico de `REL-004` sigue pendiente y es de quien
publica, no de quien programa. Dos puntos concretos le tocan: bajo qué apartado del §6 de la LGPL-2.1
queda amparada la forma en que LibVLC viaja aquí —biblioteca dinámica sin modificar y sustituible— y
si la oferta escrita de código correspondiente que `NOTICE-VideoLAN.txt` recoge basta como la
acompaña el §3 de la GPL-2.0 para los complementos.
