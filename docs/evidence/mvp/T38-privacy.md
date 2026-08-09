# T38 — Privacidad offline y diagnósticos inspeccionables / Offline Privacy and Inspectable Diagnostics

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `b4aa28e`
- Commit de tarea / Task commit: `feat: enforce offline privacy and inspectable diagnostics`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  FlaUI UIA3 5.0.0, NVIDIA GeForce RTX 5070
- IDs: `PRI-001=VERIFIED`, `PRI-002=VERIFIED`; `UX-006` suma la comprobación de que las
  recomendaciones tampoco entran en el informe / gains its diagnostics check
- Decisión que gobierna la captura / Governing decision:
  [ADR-0002](../../adr/0002-publication-history-and-privacy-capture.md)

## RED y GREEN / RED and GREEN

`DiagnosticsAllowlistTests`, `DiagnosticsPayloadTests`, `NetworkPrivacyTests` y `PrivacyConsentTests`
se escribieron antes que la lista permitida, el constructor del informe, el registro de propósitos y
la pantalla. RED falló en compilación porque no existían `DiagnosticsAllowlist`, `DiagnosticsReport`,
`DiagnosticsSerialization`, `AllowlistedDiagnosticsBuilder`, `NetworkPurposeRegistry` ni
`PrivacySettingsViewModel`. La salida está en `artifacts/test-results/T38/red/build.log`. / The four
suites were written first and RED failed on every missing type.

El ViewModel que crea esta tarea tiene prueba desde el ciclo RED. / The one view model this task
creates is covered from RED.

GREEN ejecuta **1047 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T38/green/`. `dotnet format --verify-no-changes` no informa cambios, Debug
y Release terminan con 0 advertencias bajo `-warnaserror`, `eng/verify.ps1` pasa entera y
`eng/run-accessibility.ps1 -Mode Verify -Passes 2` da **0 críticos, 0 mayores, 0 menores** con la
pantalla de privacidad ya dentro del recorrido canónico. La suite pasó de **970** a **1047**. / GREEN
runs 1047 tests with no failures and no skips; the suite grew by 77.

## La lista permitida es cerrada, no un filtro / The allowlist is closed, not a filter

Un filtro tiene que imaginar de antemano todo lo malo y se le escapa lo que nadie pensó. Una lista
cerrada no puede: un campo que no está nombrado sencillamente no viaja. El informe carga ocho cosas y
sólo ocho: versión de aplicación, de Windows y del entorno; idioma; capacidades agregadas; código de
error; tipo de excepción; y recuentos por tramos.

| Regla / Rule | Comportamiento / Behaviour |
|---|---|
| Mensaje de excepción | se descarta **entero**; sólo viaja la cadena de tipos, con un máximo de cuatro |
| Mensaje con ruta, UNC, URI, credencial o identidad | se reduce a `[redacted]`, no se recorta |
| Recuento | viaja como `0`, `1`, `2-5`, `6-20`, `21-100` o `100+` |
| Fecha del consentimiento | sólo el día, sin hora |
| Historial y términos de búsqueda | se reciben y **no se leen jamás** |

Los recuentos viajan por tramos porque el número exacto de elementos de una biblioteca es, en sí
mismo, un dato sobre esa biblioteca. / Counts are buckets because an exact count is itself a fact
about the library.

## El serializador no puede escribir lo que no se le declaró / The serializer cannot write what was never declared

El informe se escribe con un contexto generado en compilación y con **ningún** resolutor de reserva.
Pedirle que serialice una entidad del dominio no reflexiona sobre ella: falla con
`NotSupportedException` nombrando el contexto. Esa es la diferencia entre una promesa y una garantía,
y hay una prueba que le entrega `LibraryRoot` para demostrarlo. / Asking it to serialize a domain
entity fails rather than reflecting over it.

## Canarios / Canaries

Se siembran diez canarios en las categorías que la especificación prohíbe y se busca cada uno **en el
archivo escrito**, no en el objeto que lo produjo:

| Categoría / Category | Apariciones / Hits |
|---|---:|
| Ruta completa / Full path | 0 |
| Nombre de archivo / File name | 0 |
| Título / Title | 0 |
| Token de proveedor / Provider token | 0 |
| Identificador de contenido / Content identifier | 0 |
| Historial / History | 0 |
| **Credencial NAS señuelo / Decoy NAS credential** | 0 |
| Nombre de usuario / User name | 0 |
| Nombre de equipo / Machine name | 0 |
| Término de búsqueda / Search term | 0 |
| **La palabra «canary» en cualquier forma / The word “canary” at all** | **0** |

Los canarios no se reproducen en esta evidencia con su valor completo. / The canary values are not
reproduced here in full.

## Consentimiento / Consent

| Comprobación / Check | Resultado / Result |
|---|---|
| Estado inicial / Initial state | desactivado / off |
| Sin consentimiento, ¿hay informe? / Report without consent | **null**, y la carpeta de diagnósticos ni se crea |
| Con consentimiento, ¿preview = archivo? / Preview equals file | idénticos carácter a carácter / identical |
| Desactivar / Turning it off | borra el consentimiento y la vista previa |
| Consentimiento sin fecha en `settings.json` | se trata como **no dado** |
| Dos exportaciones / Two exports | un solo archivo, sustituido |
| Exportación que falla / A failing export | lo dice; no se parece a un éxito |

## Registro de propósito por cada cliente HTTP / A declared purpose per HTTP client

| Componente / Component | Destino / Host | Para qué / For what |
|---|---|---|
| `TmdbMetadataProvider` | `api.themoviedb.org` | Los metadatos de un título que alguien pidió identificar |
| `ArtworkCache` | `image.tmdb.org` | La imagen de un título ya identificado |

Una prueba recorre `src/` buscando declaraciones reales de `HttpClient` —campo o parámetro, no la
palabra dentro de un comentario— y exige que cada componente tenga propósito declarado. Hoy son
exactamente esos dos y la prueba lo fija por nombre, así que uno nuevo sin propósito rompe la
compilación de la suite. Otra prueba busca cualquier URL en el código fuente y exige que su host esté
declarado o sea de documentación. / One test finds real HTTP clients and demands a declared purpose;
another refuses any undeclared host named in the source.

Ninguna prueba encontró almacén propio de credenciales: `CredentialCache`, `NetworkCredential`,
`CredWrite` y `ProtectedData` no aparecen en el árbol. Las credenciales de un NAS son de Windows. /
No application-owned credential store exists in the tree.

## Verificación sobre la aplicación real / Verification on the real application

El ejecutable `Release` se lanza y se recorre con automatización UIA. La ventana se **maximiza** antes
de mirar: con el tamaño por defecto, la parte inferior de Ajustes queda fuera del área visible y ni el
árbol de accesibilidad la publica ni un clic físico la alcanza. Eso es una propiedad del guion de
verificación, no del producto, y queda anotada para que la próxima persona no la descubra otra vez.

| Comprobación / Check | Resultado / Result |
|---|---|
| Los cinco destinos alcanzados / All five destinations reached | sí / yes |
| Extremos remotos durante el recorrido / Remote endpoints during the walk | **0** |
| Diagnósticos activados desde la pantalla / Diagnostics switched on from the screen | sí, con fecha registrada |
| Longitud de la vista previa / Preview length | 517 caracteres |
| Archivo escrito / File written | sí, 517 bytes |
| **La vista previa coincide con el archivo carácter a carácter** | **sí / yes** |
| Claves del payload / Payload keys | las nueve permitidas y ninguna más |
| Canarios en el archivo real / Canaries in the real file | **0** en seis categorías |
| Diagnósticos desactivados al terminar / Switched off afterwards | sí / yes |

## El recorrido de treinta minutos / The thirty-minute journey

La captura es la que fija [ADR-0002](../../adr/0002-publication-history-and-privacy-capture.md): sin
proxy, sin certificado, sin elevación. La aplicación real se recorre entera —los cinco destinos, dos
veces—, se activan los diagnósticos, se ve la vista previa, se guarda el informe, y después la
aplicación se queda **treinta minutos en reposo** mientras se muestrean sus conexiones TCP por
proceso cada diez segundos.

| Medida / Measure | Resultado / Result |
|---|---:|
| Duración del reposo / Idle duration | 30 min |
| Muestras de conexiones por proceso / Per-process connection samples | **177** |
| Muestras con algún extremo remoto / Samples with any remote endpoint | **0** |
| Extremos remotos distintos / Distinct remote endpoints | **0** |
| Tamaño del informe escrito / Report size | 517 bytes |
| Claves del informe / Report keys | las nueve permitidas / the nine allowed |
| Apariciones prohibidas en el informe / Forbidden hits in the report | **0** |
| Archivos de registro en la carpeta de datos / Log files in the data folder | **0** |

La aplicación siguió en pie los treinta minutos. / The application was still running at the end.

**Alcance de la afirmación.** Esto observa lo que hace **este proceso .NET**, no lo que hace el
equipo. No es una captura de red completa y no se presenta como tal: es el alcance que ADR-0002
eligió a cambio de no instalar un certificado raíz en el almacén de nadie. / This observes the .NET
process, not the machine, exactly as the ADR decided.

## Un defecto que sólo la aplicación real enseña / A defect only the real application shows

La verificación encontró que **ninguno de los tres ViewModel de I6 avisaba a su superficie cuando la
respuesta de un comando cambiaba**. Un botón enlazado a un comando pregunta una vez y espera a que le
digan; sin ese aviso, «Guardar el informe» permanecía deshabilitado por mucho que se activara el
interruptor, «Cancelar» no llegaba a habilitarse nunca durante una copia, y «Restaurar ahora» no podía
pulsarse jamás. Las pruebas sin cabeza no lo veían porque llaman a `CanExecute` directamente.

Está corregido en los tres —copias, restauración y privacidad— y **fijado por tres pruebas** que
cuentan las notificaciones del evento, no sólo el valor devuelto. / The command surfaces never raised
`CanExecuteChanged`; three tests now pin the notification itself.

La misma verificación mostró dos cosas más, ya corregidas: reunir los datos de la máquina no puede
tumbar la pantalla si un proveedor no responde, y una exportación que falla tiene que decirlo en lugar
de parecerse a un éxito.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Domain/Privacy/DiagnosticsAllowlist.cs` | 64/64 — 100 % |
| `Application/Privacy/CreateDiagnostics.cs` | 31/31 — 100 % |
| `Infrastructure/Privacy/AllowlistedDiagnosticsBuilder.cs` | 28/28 — 100 % |
| `Infrastructure/Privacy/NetworkPurposeRegistry.cs` | 21/21 — 100 % |
| `Infrastructure/Settings/StoredPrivacySettings.cs` | 15/15 — 100 % |
| `Presentation/Settings/PrivacySettingsViewModel.cs` | 83/85 — 97,65 % |
| `Application/Privacy/DiagnosticsContracts.cs` | 37/38 — 97,37 % |
| **Total del código nuevo / New code total** | **279/282 — 98,94 %** |

## Privacidad y límites / Privacy and boundaries

- **Sin transporte**: no hay envío, ni automático ni manual. Se escribe un archivo local y ahí acaba
  la responsabilidad del programa.
- **Sin rutas en la interfaz**: la pantalla nombra el archivo escrito, nunca su carpeta.
- **Sin secretos**: el token de TMDB vive en una variable de entorno; no entra en la base, ni en las
  copias, ni en los diagnósticos.
- **Sin operaciones destructivas**: nada de esta tarea borra ni mueve ningún medio.
- **Artefactos y medios ignorados**: `git status` no incluye `artifacts/` ni ningún medio.
- **Sin datos personales versionados**: esta evidencia nombra categorías de canario, nunca su valor.
