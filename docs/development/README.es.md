# Desarrollo de AP Reelume en Windows

Esta guía describe el entorno reproducible del MVP x64. Los identificadores
técnicos permanecen bajo `ApSolutions.LocalMedia`; el nombre público es
**AP Reelume by AP Solutions**.

## Requisitos

- Windows 11 x64 y PowerShell 7.
- SDK .NET `10.0.302`, fijado en `global.json`.
- Visual Studio 2026 Build Tools y Windows 11 SDK 22621 o posterior serán
  obligatorios para el MSIX; I0 solo requiere el SDK .NET.
- Git con soporte para rutas largas recomendado.

Comprueba el SDK con `dotnet --info`. Si no está instalado, usa el instalador
oficial de .NET 10 LTS y confirma que `dotnet --version` devuelve `10.0.302`.

## Restauración y compilación

Desde la raíz del repositorio:

```powershell
dotnet tool restore
dotnet restore ApSolutions.LocalMedia.sln --locked-mode
dotnet build ApSolutions.LocalMedia.sln -c Debug --no-restore -warnaserror
```

Las versiones NuGet se administran solo en `Directory.Packages.props`. Cada
proyecto conserva su `packages.lock.json`; no se admiten rangos flotantes.

## Pruebas y verificación

```powershell
dotnet test ApSolutions.LocalMedia.sln -c Debug --no-build -m:1 --settings eng/test.runsettings
pwsh ./eng/verify-docs.ps1
pwsh ./eng/verify.ps1 -Configuration Release -Runtime win-x64
pwsh ./eng/run-accessibility.ps1 -Mode Verify -Passes 2
pwsh ./eng/check-walk-coverage.ps1
```

`check-walk-coverage.ps1` compara los controles de mando que declaran las vistas
contra los que el paseo autónomo **pulsó de verdad con el ratón**, y lo que aún no
se pulsa vive en [`eng/walk-pending.txt`](../../eng/walk-pending.txt) con su
motivo. Esa lista **sólo puede encoger**.

`verify.ps1` construye **los dos paquetes** —x64 y ARM64— y recorre el ciclo de
vida del primero antes de ejecutar las pruebas, porque las suites de empaquetado
leen los artefactos sellados y los informes que produce `verify-package.ps1`: sin
ellos serían inaplicables en silencio. El ARM64 se construye aunque se verifique
x64, y por el mismo motivo: un artefacto que sólo se produce cuando alguien se
acuerda de pedirlo es una suite que deja de aplicarse. La comparación de reproducibilidad crea dos copias limpias del árbol
con `git stash create`, así que **añada al índice lo que vaya a publicar antes de
ejecutarla**; un archivo sin rastrear no existe para una copia limpia y el script
se detiene si encuentra alguno. `-SkipPackaging` existe para el flujo de
publicación, que ya lo hizo, y no para acortar una verificación local.

`generate-verification-manifest.ps1` produce el manifiesto de evidencia desde
`docs/FEATURES.md` y se niega a escribirlo si un compromiso sin resolver no
declara su bloqueo. `generate-package-assets.ps1` redibuja las imágenes del
paquete desde los tokens del tema; se ejecuta a mano y se confirma el resultado,
porque una publicación no debe poder cambiar sus propios iconos.

La auditoría de accesibilidad tiene dos modos y la diferencia importa. `Audit`
inventaría todos los hallazgos de una pasada y siempre termina en cero, que es
como se recoge la lista completa en un ciclo rojo. `Verify` es la puerta:
cualquier defecto crítico o mayor la hace fallar, y debe pasar **dos veces
seguidas**. Ninguno de los dos puede suprimir un chequeo ni rebajar una
severidad. `-RealApp` añade el árbol UIA del ejecutable real capturado con
FlaUI, que es lo que un lector de pantalla lee de verdad.

Cuatro trampas de esa auditoría, todas descubiertas midiendo:

- `DesiredSize` **incluye el margen** y `Bounds` no. Compararlos en crudo marca
  como recortada cualquier etiqueta con margen; hay que restar el margen antes.
- Un atributo adjunto con prefijo llega a `XDocument` con el nombre local
  compuesto —`AutomationProperties.LiveSetting`, no `LiveSetting`—, así que
  buscarlo por igualdad simple oculta lo que sí existe.
- Sólo es superficie de la aplicación lo que ella declara. Los controles con
  `TemplatedParent` distinto de nulo son partes que genera el tema, no son
  alcanzables por teclado y auditarlos produce ruido, no defectos.
- Windows **no concede el primer plano** a un proceso lanzado en segundo plano,
  así que un recorrido de tabulación sintético sobre la aplicación real acaba
  llegando a otra ventana del escritorio. Lo que sí funciona es pedir el foco por
  UIA a cada control, que es lo que hace un lector; el orden de tabulación se
  verifica en la automatización sin cabeza.

Avalonia 12.1 no ofrece variable de entorno para forzar la escala, así que el
200 % real no se puede simular sin cambiar la escala del sistema: la matriz de
escalado se cubre con `SetRenderScaling` en la automatización, que es el mismo
mecanismo que usa el DPI real.

Cuando un archivo de producción aparece al 0 % con pruebas verdes, sospecha
primero del escollo del perfilador, pero **comprueba también si simplemente no
tiene pruebas**: un adaptador nuevo sin suite propia da exactamente la misma
lectura.

`-m:1` es obligatorio sobre la solución: sin él MSBuild programa una
invocación por proyecto y los hosts de prueba arrancan a la vez, lo que
desestabiliza la biblioteca nativa de vídeo y mata algún host en su tiempo de
espera de conexión.

Los resultados reproducibles se escriben en `artifacts/test-results/`, una
ruta ignorada por Git. Compilador y analizadores tratan advertencias como
errores.

Las pruebas que **miden recursos del proceso** —handles, memoria— o que matan
un proceso a propósito lanzan un hijo con el ejecutable del propio proyecto de
prueba, porque el host compartido añade más ruido que señal. **Todo** hijo que
una prueba lance debe vaciar las variables del perfilador
(`CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER*` y sus equivalentes `COR_*`), sin
excepción y aunque el hijo no mida nada: si las hereda, sobrescribe los datos de
cobertura del padre y el código realmente ejercitado se informa como no
cubierto. Ese fallo es silencioso —las pruebas pasan y la cobertura miente—, así
que sospecha de él cada vez que un archivo con pruebas verdes aparezca al 0 %.
Además, hay que drenar sus dos tuberías a la vez; leer una hasta el final
mientras la otra se llena bloquea al hijo.

Cinco escollos más, todos descubiertos ejecutando la aplicación de verdad:

- Un botón enlazado a un `ICommand` **pregunta una vez y espera a que le digan**.
  Un comando cuyo `CanExecuteChanged` no se dispara nunca deja su botón como
  estaba al construirse: deshabilitado para siempre si empezó así. Las pruebas
  sin cabeza no lo ven porque llaman a `CanExecute` directamente, así que la
  prueba tiene que **contar las notificaciones del evento**.
- La automatización UIA sobre la aplicación real necesita la ventana
  **maximizada**: con el tamaño por defecto, lo que queda por debajo del área
  visible no aparece en el árbol y un clic físico no lo alcanza. Invocar por
  patrón `Invoke` en vez de hacer clic evita además depender de la posición.
- `HttpListener` **resuelve un nombre al enlazar**, así que un servidor señuelo
  hecho con él contamina la propia medición de resoluciones. Un `TcpListener`
  que responde una línea de HTTP a mano no lo hace.
- Dos suites que lanzan **hosts de prueba hijos** no pueden correr a la vez:
  cada hijo es un host completo y el segundo agota tiempos que sobran para el
  primero. Se resuelve con una colección `DisableParallelization`, no relajando
  el tiempo límite.
- Un `File.Move` sobre un destino que es un directorio lanza
  `UnauthorizedAccessException`, no `IOException`. La aserción debe nombrar lo
  que de verdad ocurre.

La regresión visual se fija con una **baseline estructural versionada** en
`tests/ApSolutions.LocalMedia.UiTests/Baselines/<tarea>/*.json`, no con
imágenes: `artifacts/` está ignorado, un PNG binario no se revisa en un diff y
el render sin cabeza varía entre equipos. La baseline registra viewport lógico,
primer foco, orden de foco, visibilidad de cada superficie y los bordes que
importan; el PNG se sigue capturando en `artifacts/ui-captures/` como prueba
visual. Cuando una tarea cambia la superficie a propósito, se regenera la
baseline, se revisa el diff y se vuelve a aprobar **en el mismo commit** que la
cambia: una baseline que no sigue a la interfaz deja de proteger nada.

## Probar en ARM64

La aplicación se compila, se empaqueta **y se ejecuta** en Windows 11 ARM64 en cada vuelta de CI,
sobre un runner que GitHub presta gratis a los repositorios públicos. Cómo se lanza, cómo se lee su
resultado —que **no** es el color del run— y las cinco trampas que ya costaron una vuelta cada una
están en [arm64-ci.es.md](arm64-ci.es.md). Léelo antes de tocar nada que dependa del sistema
operativo.

## Ejecutar el shell x64

El host no crea cuentas ni usa servicios remotos. Compílalo y ejecútalo así:

```powershell
dotnet build src/ApSolutions.LocalMedia.Windows -c Release --no-restore
./src/ApSolutions.LocalMedia.Windows/bin/Release/net10.0-windows10.0.22621.0/ApSolutions.LocalMedia.Windows.exe
```

La interfaz arranca en español y la ruta inicial es Inicio. Los cinco destinos
se activan con teclado. Los recursos alternativos ingleses viven en
`Resources/Strings.en.axaml`; cualquier texto visible nuevo debe añadirse al
diccionario español y al inglés con la misma clave.

## Tema y preferencias locales

La preferencia `System`, `Light` o `Dark` se escribe atómicamente en
`%LOCALAPPDATA%\APSolutions\LocalMedia\settings.json`. Los colores, espaciado,
foco y movimiento se definen en `Theme/DesignTokens.axaml`; no añadas colores
de producto incrustados en vistas. El reproductor debe solicitar siempre la
variante oscura y toda animación nueva debe consultar `IReducedMotionService`.
Mica sólo se implementa en el host Windows y conserva un fondo sólido si no
está disponible.

## Base local y migraciones

La base se crea en
`%LOCALAPPDATA%\APSolutions\LocalMedia\library.db` —o donde diga
`AP_LOCALMEDIA_DATA_ROOT`, si está definida— con WAL, claves foráneas,
timeout ocupado de 5 s e integridad al iniciar. Las migraciones viven en
`Infrastructure/Data/Migrations/`, se enumeran en `Manifest.json` y su SHA-256
debe coincidir con el recurso SQL incrustado. Cada versión pendiente crea antes
una copia SQLite válida y se aplica en una sola transacción. No edites una
migración publicada ni añadas tablas antes de la tarea vertical que las posee.
Si falla integridad o migración, la aplicación muestra las rutas conservadas y
nunca ofrece sustituir la copia previa.

Al añadir una migración: usa el **siguiente número libre**, aunque el plan cite
otro que ya esté ocupado, y anótalo en la evidencia de la tarea. Un hueco
temporal en la numeración es válido: `MigrationRunner` sólo exige versiones
únicas y positivas, aplica en orden ascendente las que faltan y las registra por
número, de modo que una base ya migrada acepta después una versión intermedia.
El `sha256` del
manifiesto se calcula sobre el texto UTF-8 del archivo tal como lo lee
`MigrationRunner`; `.gitattributes` fuerza saltos `LF`, así que el hash es
estable entre equipos sólo si el archivo se guarda sin BOM y con `LF`.
`SqliteBootstrapTests` fija el número de migraciones, la lista de nombres, la
lista de tablas y el número de copias previas: actualízalo en el mismo commit.

**Aislar una ejecución.** `AP_LOCALMEDIA_DATA_ROOT` nombra la carpeta donde la
aplicación guarda todo: base, ajustes, copias, arte y diagnósticos. Se lee una
sola vez al arrancar y un valor en blanco equivale a no ponerla. Existe para que
una comprobación de ciclo de vida —instalar, arrancar, actualizar, desinstalar—
pueda ejecutarse sin tocar la carpeta de perfil de quien la ejecuta. `LOCALAPPDATA`
**no** sirve para esto: .NET resuelve la carpeta con `SHGetFolderPath` y no lee esa
variable, así que redirigirla no redirige nada y la aplicación escribe igualmente
en la carpeta real.

## Arquitectura

La regla de dependencias es `Presentation → Application → Domain ←
Infrastructure`. El host Windows compone los cuatro proyectos. Domain no usa
paquetes; Application no referencia Infrastructure, Avalonia ni Windows; y
Presentation no referencia Infrastructure.

## Identidad, privacidad y secretos

- Namespace raíz: `ApSolutions.LocalMedia`.
- Identidad persistente de paquete: `APSolutions.LocalMedia`.
- Esquema URI: `apsolutions-localmedia`.
- No añadas tokens, rutas privadas, bases locales ni vídeos del usuario.
- La CLI .NET se ejecuta con telemetría desactivada en verificación local/CI.

## Flujo de contribución

Cada comportamiento sigue RED→GREEN→refactor, conserva sus TRX y actualiza
evidencia bilingüe. Ejecuta la verificación transversal antes de crear el
commit de la tarea y no mezcles trabajo del incremento siguiente.
