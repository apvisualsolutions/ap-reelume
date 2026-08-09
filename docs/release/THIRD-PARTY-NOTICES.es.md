# Avisos de terceros

AP Reelume by AP Solutions se publica bajo `GPL-3.0-or-later`. Este documento recoge los
componentes de terceros que la solución consume hoy y la licencia declarada en el paquete
restaurado. Este archivo se actualiza en cada incremento que añade o retira una dependencia, y
**viaja dentro del artefacto**, en `licenses/`, junto a su versión inglesa.

El inventario de materiales de la compilación exacta que usted ejecuta está en `sbom/`, dentro del
mismo artefacto, en formatos CycloneDX 1.5 y SPDX 2.3. Se genera desde los ficheros de bloqueo, de
modo que describe lo que la compilación resolvió y no lo que los proyectos piden.

## Componentes distribuidos con la aplicación

| Componente | Versión | Licencia declarada |
|---|---|---|
| Avalonia | 12.1.1 | MIT |
| Avalonia.Desktop | 12.1.1 | MIT |
| Avalonia.Themes.Fluent | 12.1.1 | MIT |
| LibVLCSharp | 3.10.0 | LGPL-2.1-or-later |
| VideoLAN.LibVLC.Windows | 3.0.23.1 | LGPL-2.1-or-later |
| Microsoft.Data.Sqlite | 10.0.10 | MIT |
| SQLitePCLRaw.lib.e_sqlite3 | 3.53.3 | Apache-2.0 sobre SQLite, que es de dominio público |
| Microsoft.Extensions.DependencyInjection | 10.0.10 | MIT |

`GPL-3.0-or-later` es compatible con la incorporación de componentes `LGPL-2.1-or-later`, `MIT` y
`Apache-2.0`. La biblioteca nativa LibVLC se distribuye sin modificar y conserva su propio aviso de
licencia dentro del paquete; el artefacto del MVP debe incluirlo íntegro.

## Componentes usados solo durante el desarrollo y las pruebas

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
declaradas para que una adopción futura no introduzca un rango flotante, y no forman parte de
ningún artefacto:

- `LibVLCSharp.Avalonia` 3.10.0 — no se adopta porque apunta a Avalonia 11.x y expondría el objeto
  reproductor del motor a la vista.
- `NetArchTest.Rules` 1.3.2 — no se restaura en la solución actual; las reglas de arquitectura se
  comprueban leyendo los archivos de proyecto.

## Herramientas externas no redistribuidas

La matriz de contenedores y códecs se genera con **FFmpeg**, que debe estar instalado en la máquina
de desarrollo y se localiza por `FFMPEG_PATH` o por `PATH`. FFmpeg no se incluye en el repositorio
ni en ningún artefacto publicado, y su licencia depende de la compilación que cada persona instale.
Las muestras que produce proceden de sus generadores sintéticos `testsrc2` y `sine`, de modo que el
contenido resultante no incorpora obra de terceros.

## Contenido multimedia

Ningún archivo de vídeo, audio o subtítulos está versionado. La biblioteca personal nunca se lee ni
se copia durante las pruebas.
