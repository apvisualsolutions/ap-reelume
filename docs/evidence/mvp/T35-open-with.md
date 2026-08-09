# T35 — «Abrir con…» sin importar / “Open with…” without importing

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `2162921`
- Commit de tarea / Task commit: `feat: open loose media without catalog import`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  LibVLC 3.0.23.1, SQLite en WAL, NVIDIA GeForce RTX 5070
- IDs: `SYS-002=IMPLEMENTED`, a la espera del paquete de T40 / awaiting the T40 package;
  `PLY-001=IN_PROGRESS` y `PRI-001=IN_PROGRESS`, que suman evidencia / both gain evidence

## RED y GREEN / RED and GREEN

`OpenLooseFileTests`, `FileActivationTests` y `LooseFileTests` se escribieron antes que el caso de
uso, el analizador de argumentos y la superficie. RED falló en compilación porque no existían
`OpenLooseFile`, `LooseFileSession`, `FileActivationHandler`, `LooseFileViewModel` ni
`LooseFileBanner`. La salida está en `artifacts/test-results/T35/red/build.log`. / The three suites
were written first and RED failed on every missing type.

El ViewModel que crea esta tarea tiene prueba desde el ciclo RED. / The one view model this task
creates is covered from RED.

GREEN ejecuta **828 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T35/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias. La suite pasó de **807** a **828**. / GREEN runs 828
tests with no failures and no skips; the suite grew by 21.

## La sesión es efímera por construcción / The session is ephemeral by construction

`OpenLooseFile` valida la extensión y la existencia del archivo, resuelve la ruta y arranca la
reproducción a través del mismo `PlaybackSessionCoordinator` que usa el catálogo. El identificador de
archivo se genera para esa sesión y **no se entrega a nada que escriba**:

- No toca `ICatalogRepository`, `IMediaFileRepository`, `IWatchStateRepository` ni
  `IPersonalStateRepository`.
- No llama a `PlaybackProgressTracker.BeginAsync`, que es lo único que autoriza al rastreador a
  escribir. Sin esa llamada, `FlushAsync` devuelve `false` sin tocar el disco.
- Dos activaciones del mismo archivo producen **identificadores distintos**, así que ni siquiera
  existe una clave estable que pudiera convertirse en fila.

Los fallos hablan el idioma que el reproductor ya conoce, sin inventar mensajes: un archivo ausente
es `FileNotFound` y un contenedor no reconocido es `UnsupportedCodec`, con las mismas acciones de
recuperación no destructivas de T19. / Failures reuse the T19 diagnoses instead of inventing new ones.

## Cero filas nuevas, contadas en todas las tablas / Zero new rows, counted across every table

La prueba de integración no comprueba las tablas que parecían probables: censa **todas** las tablas
que el esquema declara y compara el recuento entero antes y después.

| Escenario / Scenario | Resultado / Result |
|---|---|
| Una activación / One activation | 31 tablas censadas, ninguna cambia |
| Segunda activación y una fallida / A second and a failed one | ninguna cambia |
| Ruta con espacios, Unicode y más de 240 caracteres | se abre, y ninguna cambia |

## Verificación física / Physical verification

Cinco activaciones reales del ejecutable `Release`, con muestras generadas por ffmpeg y la base de
datos real del equipo censada antes y después:

| Comprobación / Check | Resultado / Result |
|---|---|
| Tablas censadas en la base real / Tables counted in the real database | **32** |
| Nombre con espacios / Name with spaces | la aplicación abre con la ventana `AP Reelume` |
| Nombre con Unicode / Unicode name | abre |
| Ruta de **228** caracteres / A 228-character path | abre |
| Segunda activación con una ya abierta / A second activation | abre |
| Ruta inexistente / A missing path | la ventana sigue en pie; no se cierra ni se cuelga |
| **Tablas que cambiaron / Tables that changed** | **0** |

Y por el camino de «Abrir con…», que no es lo mismo que arrancar el proceso a mano: se registra la
aplicación en `HKCU\Software\Classes\Applications\...\shell\open\command` con la forma
`"<ejecutable>" "%1"`, se deja que esa cadena se resuelva y se ejecute, y después se retira el
registro.

| Comprobación / Check | Resultado / Result |
|---|---|
| Forma del comando registrado / Registered command shape | `"<ejecutable>" "%1"` |
| `%1` se sustituye por la muestra / `%1` resolves to the sample | sí / yes |
| El comando resuelto arranca la aplicación / The resolved command starts the app | sí, ventana `AP Reelume` |
| Registro retirado al terminar / Registration removed afterwards | sí / yes |
| Tablas que cambiaron / Tables that changed | **0** |

## Los argumentos se leen sin shell / Arguments are read without a shell

`FileActivationHandler.Parse` toma el **primer argumento posicional y nada más**. No ejecuta un
intérprete, no expande comodines y rechaza cualquier cosa que empiece por `-` o `/`, así que una
lista de argumentos preparada no puede convertir una activación en un comando. Una lista vacía, un
argumento en blanco o un modificador devuelven `null` y la aplicación arranca normalmente. / The
parser takes one positional path, never runs a shell, and refuses switches.

## El banner y la única salida hacia la biblioteca / The banner and the one way into the library

El banner dice en palabras que lo que suena no está en la biblioteca y que su progreso no se
guardará. Muestra **el nombre del archivo, nunca la ruta completa**: un aviso no es sitio para las
carpetas de nadie, y una prueba lo fija leyendo el XAML. La acción secundaria añade **la carpeta que
contiene el archivo**, no el archivo, y pide confirmación antes: el texto de la confirmación explica
exactamente eso. Rechazarla no añade nada. / The banner shows the file name and never the path, and
the only way into the library is the containing folder, behind a confirmation.

## Una sola lista de contenedores / One list of containers

El escáner y la activación tienen que estar de acuerdo: un archivo que la biblioteca catalogaría
tiene que ser un archivo que «Abrir con…» reproduce. Las ocho extensiones aprobadas vivían dentro del
enumerador de archivos; ahora están en `Domain/Discovery/MediaFileExtensions.cs` y las usan los dos.
`Windows/Packaging/FileAssociations.xml` declara exactamente esas ocho para el paquete de T40. / The
approved containers now live in one place that both the scanner and the activation read.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Application/Playback/OpenLooseFile.cs` | 27/27 — 100 % |
| `Windows/Shell/FileActivationHandler.cs` | 8/8 — 100 % |
| `Presentation/Player/LooseFileViewModel.cs` | 47/48 — 97,92 % |
| `Domain/Discovery/MediaFileExtensions.cs` | 13/14 — 92,86 % |
| **Total del código nuevo / New code total** | **95/97 — 97,94 %** |

## Privacidad y límites / Privacy and boundaries

- **Sin red ni telemetría**: ningún archivo de esta tarea abre un socket ni resuelve un nombre.
- **Sin rutas en la interfaz**: el banner muestra el nombre del archivo; la ruta sólo existe en
  memoria mientras dura la sesión.
- **Sin escrituras**: comprobado por censo completo de la base, tanto en integración como sobre la
  base real del equipo.
- **Sin operaciones destructivas**: ningún `File.Delete` ni `File.Move`; el archivo suelto se lee y
  nada más.
- **Artefactos y medios ignorados**: las muestras se generan fuera del repositorio y `git status` no
  incluye `artifacts/` ni ningún archivo multimedia.
- **Sin datos personales versionados**: ningún archivo tocado contiene nombre de usuario, nombre de
  equipo ni ruta absoluta local; la evidencia describe la forma del comando del registro, no su
  contenido.

## Salvedades declaradas / Declared caveats

1. **Sin MSIX no hay asociación real de extensiones.** Windows no ofrece la aplicación en el menú
   «Abrir con…» hasta que el paquete de T40 declare `FileAssociations.xml`. Lo verificado aquí es la
   ruta que el shell ejecuta —el comando registrado con `%1`— y la activación por argumentos, que es
   exactamente lo que el paquete acabará invocando. Por eso `SYS-002` queda `IMPLEMENTED`.
2. **La lista de contenedores se unificó**, moviéndola del enumerador al dominio. Es un cambio de
   una sola línea de comportamiento —ninguna extensión entra ni sale— y evita que las dos listas
   diverjan.

`SYS-002` pasa a `IMPLEMENTED`: un archivo suelto se reproduce, se anuncia como lo que es, no crea
ninguna entidad persistente y sólo entra en la biblioteca si alguien añade su carpeta a propósito. /
The loose-activation identifier is implemented, with packaged association pending.
