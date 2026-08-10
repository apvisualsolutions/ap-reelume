# Avisos de terceros

AP Reelume by AP Solutions se publica bajo `GPL-3.0-or-later`. Este documento recoge los componentes
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
| VideoLAN.LibVLC.Windows | 3.0.23.1 | LGPL-2.1-or-later para el núcleo; véanse los complementos |
| Microsoft.Data.Sqlite | 10.0.10 | MIT |
| Microsoft.Data.Sqlite.Core | 10.0.10 | MIT |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.11 | Apache-2.0 |
| SQLitePCLRaw.core | 2.1.11 | Apache-2.0 |
| SQLitePCLRaw.provider.e_sqlite3 | 2.1.11 | Apache-2.0 |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 | Apache-2.0 sobre SQLite, que es de dominio público |
| Microsoft.Extensions.DependencyInjection | 10.0.10 | MIT |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.10 | MIT |

`GPL-3.0-or-later` es compatible con la incorporación de componentes `LGPL-2.1-or-later`, `MIT`,
`Apache-2.0` y `BSD-3-Clause`. Las licencias MIT y BSD-3-Clause exigen que su aviso de copyright
viaje con el binario, y para eso están este archivo y la carpeta `licenses/` dentro del artefacto.

### El motor de ejecución de .NET

El artefacto es autocontenido: lleva su propia copia del motor de ejecución de .NET 10 y de su
biblioteca base (`coreclr.dll`, `System.*.dll`, `mscorlib.dll` y sus acompañantes), más la proyección
del SDK de Windows (`Microsoft.Windows.SDK.NET.dll`, `WinRT.Runtime.dll`). Todo ello lo publica
Microsoft bajo `MIT`. Nadie tiene que instalar un motor de ejecución para usar AP Reelume, y esa
comodidad es lo que mete varios cientos de archivos con licencia de Microsoft dentro del paquete.

### LibVLC, su núcleo y sus complementos

El paquete `VideoLAN.LibVLC.Windows` declara `LGPL-2.1-or-later`, que cubre `libvlc.dll` y
`libvlccore.dll`. Además transporta unos trescientos complementos en `plugins/`, y **esos llevan sus
propias licencias**, algunas `GPL-2.0-or-later` en lugar de LGPL — el codificador x264 que hay detrás
de `libx26410b_plugin.dll` es el ejemplo más claro. La biblioteca se distribuye sin modificar y
conserva el aviso de licencia de VideoLAN dentro del paquete, que el artefacto debe incluir íntegro.

Para un programa publicado bajo `GPL-3.0-or-later`, un complemento `GPL-2.0-or-later` es compatible:
el «o posterior» es lo que hace que ambos se encuentren en GPL-3.0. Uno licenciado `GPL-2.0-only` no
lo sería, y confirmar que ninguno lo está, en la compilación exacta de VideoLAN que este paquete
fija, es asunto del dictamen jurídico profesional registrado como
[REL-004 en la matriz de alcance](../FEATURES.md), no de este documento. La palanca práctica, si ese
dictamen la pide, es recortar el conjunto de complementos a los que la reproducción carga de verdad.

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
jurídico profesional de REL-004 las responda: si todos los complementos de VideoLAN que viajan en la
compilación fijada son compatibles con `GPL-3.0-or-later`, y si el aviso propio de algún componente
debe reproducirse íntegro en vez de referenciarse. Ninguna de las dos frena el desarrollo; ambas se
nombran aquí para que nadie confunda este documento con el dictamen.
