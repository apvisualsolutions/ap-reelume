# Dónde retomar

Estado del proyecto al cerrar la **quinta** sesión del **2026-08-10**, la que saldó la última deuda
de cobertura e instrumentó el camino del fallo del rojo intermitente. La versión inglesa está en [NEXT-SESSION.en.md](NEXT-SESSION.en.md). El registro
canónico del alcance sigue siendo [FEATURES.md](FEATURES.md); el trabajo pendiente de la auditoría
vive en [2026-08-08-audit-remediation.md](superpowers/plans/2026-08-08-audit-remediation.md). Esto es
sólo el punto de retomada.

## Verificación de arranque

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version                                        # 10.0.302
git status --short --branch                             # limpio, sobre origin/codex/ap-reelume-mvp-x64
git merge-base --is-ancestor main HEAD; $LASTEXITCODE   # 0
```

## Dónde vive el código ahora

`apvisualsolutions/ap-reelume` es **público** desde el 2026-08-10, con un corte fresco de un solo
commit raíz. El historial completo de desarrollo quedó en `apvisualsolutions/ap-reelume-archive`
(privado), de modo que **los SHA que citan los documentos de evidencia resuelven en el archivo, no
aquí**. Los remotos locales son `origin` (público) y `archive` (el histórico), y las ramas viejas se
conservan como `archived/main` y `archived/codex/…` apuntando al archivo.

La CI corre en runners hospedados, gratuitos en repositorios públicos. El runner self-hosted sigue
instalado en `.runner/` (ignorado por git) pero **apagado**, y el workflow ya no tiene forma de
llamarlo.

## Lo terminado el 2026-08-10 (segunda sesión, la legal)

Cuatro commits, cada uno con su ciclo completo y su evidencia bilingüe.

- **El artefacto entrega las licencias que nombra.** Era el único incumplimiento legal abierto.
  `licenses/`, dentro de los dos artefactos, lleva los cinco textos canónicos —LGPL-2.1, GPL-2.0,
  Apache-2.0, MIT, BSD-3-Clause— y los avisos de copyright de ANGLE, SkiaSharp, HarfBuzzSharp,
  BouncyCastle, SQLite, SQLitePCLRaw y VideoLAN: quince archivos, 209 KiB. El aviso nativo de Skia y
  HarfBuzz destapó **veintitantas bibliotecas** que `libSkiaSharp.dll` transporta —freetype, ICU,
  libpng, libwebp, zlib— y que no aparecían en ningún documento del proyecto. Nada está transcrito:
  `LicenceTextTests` compara cada copia byte a byte contra el paquete NuGet que la compilación
  consumió, y lee cada copyright del `.nuspec` restaurado. Detalle en
  [audit-legal-licence-texts.md](evidence/stable/audit-legal-licence-texts.md).
- **El logotipo de TMDB está en Créditos**, que cierra el último punto abierto de sus términos. El
  archivo es el que TMDB publica y se puede demostrar: la huella SHA-256 que ellos incrustan en la
  dirección del recurso coincide con la del archivo versionado. Sólo publican SVG y Avalonia no dibuja
  SVG, así que la vista lleva la geometría del archivo —una prueba las compara carácter a carácter—
  en vez de traer un renderizador y media docena de paquetes con sus licencias. La especificación
  decía 24 px frente a 48 px del nombre del producto; ese 48 no existía en ninguna vista, así que se
  midió y quedó en 16 frente a 24. Detalle en
  [audit-legal-tmdb-logo.md](evidence/stable/audit-legal-tmdb-logo.md).
- **ARQ-001 / WIN-005 / resto de BUG-004.** El proveedor de servicios tiene dueño y se libera al
  salir; `PendingActivationPath` y el estado de la sesión de reproducción salieron de los estáticos.
  `DisableParallelization` se retiró de `AssembledShellSuites` y las 70 pruebas de accesibilidad pasan
  sin él, que es la prueba de que la propiedad es real. `WindowLifecycle` se extrajo, la puerta de
  cobertura lo midió al 70,89 % de líneas y 28,57 % de ramas, y **volvió** igual que
  `WindowsFilePickers`. Detalle en
  [audit-arq001-application-host.md](evidence/stable/audit-arq001-application-host.md).
- **La integración continua dejaba de hablar durante una hora.** Seis de las diez ejecuciones de esta
  sesión murieron al techo de sesenta minutos, con el registro mudo desde «compilación correcta»
  hasta la cancelación cincuenta y seis minutos después. El paso siguiente,
  `eng/generate-test-media.ps1`, arrancaba FFmpeg sin ningún tope. Ahora cada llamada tiene techo,
  cada muestra se anuncia antes de producirse, y una prueba con un codificador que nunca vuelve
  comprueba que el guion muere en segundos nombrando la receta. Producir la matriz entera cuesta
  1,6 s medidos, así que el techo no es un presupuesto de rendimiento. La primera ejecución con el
  techo puesto falló en cuatro minutos y nombró al culpable: `mkv-dual-audio-english-first`. Las once
  recetas que usaban `-shortest` —un bloqueo documentado de FFmpeg— fijan ahora la duración de salida
  de forma explícita. **El cuelgue nunca se reprodujo en local**: es una carrera, y lo que se retiró
  es la clase de riesgo, no una reproducción. Si vuelve a aparecer, ahora dirá en qué receta.
- **Los dos endurecimientos que la auditoría archivó como «no explotables».** Uno lo era mucho menos
  de lo anotado: el lanzador externo entregaba al shell de Windows un `.ps1`, un `.txt` y un archivo
  sin extensión. El otro no existía en la forma descrita, y sólo se supo forjando el archivo que lo
  habría explotado. Detalle en
  [audit-hardening-launcher-and-restore.md](evidence/stable/audit-hardening-launcher-and-restore.md).

## Lo que sigue

La cola de dos —la última deuda de cobertura y la instrumentación del rojo intermitente— **se
ejecutó entera el 2026-08-10** (quinta sesión), y `BUG-010` cayó detrás. **La cola siguiente está
decidida entera en
[el plan](superpowers/plans/2026-08-08-audit-remediation.md): forma, sitio, primera medición y
criterio de aceptación de cada una.** Se ejecuta en este orden y no se re-delibera; lo que sí se
hace en cada una es **medir antes de corregir**, porque tres premisas escritas se han caído esta
semana al medirlas.

1. ~~**`BUG-011`**~~ **Hecho el 2026-08-14**: una sola cola de liberación para el proceso. La
   fábrica aprendió a vaciar a petición con techo que no lanza; el motor soltó su cola, su candado,
   su bandera, su drenaje y su constante de reposo; la lista que sólo puede encoger de
   `NativeInstanceOwnershipTests` quedó **vacía**. El orden «medios antes que reproductor» y la
   ventana de 1 s siguen intactos.
   [audit-bug011-engine-release-queue.md](evidence/stable/audit-bug011-engine-release-queue.md).
2. ~~**`ARQ-013`**~~ **Hecho el 2026-08-14**: la lectura de referencias salió a `SurfaceReferences`
   y ahora quita los comentarios antes de casar. Medido antes de tocar: **nada** se escondía detrás
   de un comentario, así que la puerta era ciega pero no estaba tapando ningún huérfano.
   [audit-arq013-reachability-comments.md](evidence/stable/audit-arq013-reachability-comments.md).
3. ~~**`ARQ-014`**~~ **Hecho el 2026-08-14**: la versión sale del ensamblado y la prueba afirma sobre
   la cabecera que sale de verdad, con la versión esperada leída de `Directory.Build.props`.
   [audit-arq014-updater-identity.md](evidence/stable/audit-arq014-updater-identity.md).
4. ~~**`ARQ-012`**~~ **Hecho el 2026-08-14**, y adelantado a `ARQ-014` porque ese necesita leer
   `Directory.Build.props` desde la raíz y sin ancla compartida habría escrito una copia más. La
   estimación del plan se quedó **cinco veces corta**: 59 archivos buscaban la raíz y 56 nombraban
   el ancla.
   [audit-arq012-repository-anchor.md](evidence/stable/audit-arq012-repository-anchor.md).
5. ~~**`QA-001`**~~ **Hecho el 2026-08-14**, y la medición dio la vuelta a la tarea: **cero avisos**
   en toda la solución, así que no era deuda sino puerta — y hacía falta igual, porque las tres
   reglas vienen apagadas. El cero se comprobó con un canario antes de creérselo.
   [audit-qa001-culture-gate.md](evidence/stable/audit-qa001-culture-gate.md).
6. **Documentación al final**: `DOC-101`, `DOC-201`, `T44.1`-`T44.6` y el manual de usuario, que se
   escribe desde la aplicación construida y no desde el código.

**Decisión sobre la primera publicación**: la tubería ya no está bloqueada —el secreto de firma está
puesto—, pero **no se corta `v0.1.0` todavía**. Faltan dos cosas que no son de código y no las
decide un agente: el dictamen `REL-004` y el paseo físico. Publicar antes sería cambiar una
verificación pendiente por una fecha.

## Un rojo intermitente que hay que vigilar, no tapar

El 2026-08-10, la fase `first-launch` de `verify-package.ps1` **falló una vez** en la rama y **pasó
con el mismo commit** en `main`. Mismo código, mismo flujo de trabajo, resultado distinto: es
intermitente, y por eso no es un defecto que se pueda encontrar leyendo.

Lo que se sabe, medido del registro:

- La fase duró **137 s**, que es exactamente el plazo de ventana (90 s) más el de cierre (45 s). Los
  dos se agotaron y el proceso acabó matado, de ahí el `exit code -1`.
- En **esa misma ejecución**, `repair`, `downgrade-refused`, `open-with` y las cuatro fases
  `windows-*` arrancaron la aplicación y la vieron pintar. Sólo falló el primer arranque.
- ~~El primer arranque es el único que migra de verdad, y migrar bloquea el hilo de interfaz.~~
  **Medido el 2026-08-10 y desmentido**: cada ciclo recibe su propia carpeta, así que los cinco
  migran una base nueva, y un arranque entero con sus dieciséis migraciones cuesta **2 292 ms**
  frente a los **90 000 ms** de plazo que aquel fallo agotó. Lo que falló allí no fue un arranque
  lento sino uno que no llegó a ocurrir, y la causa candidata sigue abierta. Detalle en
  [audit-arq005-startup-baseline.md](evidence/stable/audit-arq005-startup-baseline.md).
- **Frecuencia observada: una de cuatro.** No se repitió en las tres ejecuciones siguientes que
  llevaban ese mismo código, ni en las ocho posteriores hasta `fa968de`. Es la cifra contra la que
  comparar si vuelve a verse.
- **Y media pregunta ya estaba contestada en el registro archivado, sin que nadie lo hubiera
  leído.** La línea entera de aquella fase decía `16 migration(s) applied to a new database`: el
  proceso vivió lo suficiente para aplicar las dieciséis, así que «murió antes de migrar» estaba
  descartado desde el principio y las dos hipótesis nunca fueron dos. Lo que no dice ningún registro
  es la otra mitad —si al llegar el plazo quedaba algo vivo que pintar—, porque `exit code -1` es el
  matarile del propio arnés.

**Lo que ya no falta**: desde el 2026-08-10 la verificación **deja diagnóstico** cuando la ventana no
llega. Antes de matar el proceso anota si seguía vivo, cuánto procesador gastó y en cuántos hilos —lo
que separa girar de esperar—, el estado de `library.db` y de `schema_history`, y qué hay en la
carpeta de datos, todo en la misma línea que CI imprime. Detalle en
[audit-first-launch-instrumentation.md](evidence/stable/audit-first-launch-instrumentation.md).

**Lo que no se hace**: subir el plazo de 90 s. Eso convierte la única señal que hay en silencio, que
es el error que ya costó seis ejecuciones con los `cancelled` del generador de medios. Si vuelve a
aparecer, deja de ser trabajo pendiente y pasa a ser la corrección urgente — y ahora dirá algo al
hacerlo.

## Lo terminado el 2026-08-10 (tercera sesión)

Cuatro commits, cada uno con su ciclo, su evidencia bilingüe y sus puertas.

- **ARQ-010 — el contenedor se revisa al construirse.** `ValidateOnBuild` encendido, con una prueba
  que le pasa una colección rota **por la ruta del producto**; afirmar sobre una copia de las opciones
  sólo demuestra la copia. **No destapó ningún registro roto**, que era lo que el plan esperaba, y el
  límite quedó medido: valida 109 de 156 registros, porque los 45 construidos con una factoría son
  opacos por construcción. Cuesta +0,22 ms.
  [audit-arq010-container-validation.md](evidence/stable/audit-arq010-container-validation.md).
- **ARQ-004 — un fallo tiene dónde caer, y ya no puede cerrar la aplicación.** La medición invirtió
  el orden de sus dos mitades: `AppDomain.UnhandledException` **no** impide que el proceso termine,
  sólo deja constancia, así que un comando tiene que capturar siempre — y algo que captura siempre
  necesita un destino siempre. Ese destino no existía (2 de 24 superficies tienen estado de fallo), y
  buscándolo apareció que **el informe de diagnóstico se construía de una sola fuente**, la auditoría
  de renombrados: en una sesión sin renombrados, una aplicación que fallaba parecía sana. Luego, de
  **27 `async void` a 2**, y los dos capturan. −582/+227 líneas.
  [audit-arq004-failure-net.md](evidence/stable/audit-arq004-failure-net.md) y
  [audit-arq004-single-command.md](evidence/stable/audit-arq004-single-command.md).
- **ARQ-005, primera mitad — el candado que nadie podía abrir.** La espera de las teclas multimedia
  salió del `lock` y recibió techo. Bloqueaba el hilo de interfaz en **cada apertura de vídeo**, y sin
  contestación el hilo atrapado sujetaba el candado que la cancelación necesitaba.
  [audit-arq005-media-keys.md](evidence/stable/audit-arq005-media-keys.md).

## Lo terminado el 2026-08-10 (cuarta sesión)

Tres commits, cada uno con su ciclo, su evidencia bilingüe y su verificación completa.

- **La verificación dice cuánto se espera a la ventana, no sólo que llegó.** Y en su primera
  medición desmintió dos cosas que estaban escritas aquí: los **cinco** ciclos migran una base nueva
  —contadas las migraciones en cada carpeta—, no sólo el primero, y el primer arranque tampoco es el
  más lento de los tres. Se miden tres fases y no una **a propósito**, y esa decisión se pagó sola:
  al comparar el antes y el después, `open-with` se repitió con 6 ms de dispersión mientras
  `first-launch` variaba 1245 ms entre ejecuciones del mismo código.
  [audit-arq005-startup-baseline.md](evidence/stable/audit-arq005-startup-baseline.md).
- **ARQ-005, segunda mitad: la ventana existe mientras migra.** La medición previa decidió la forma
  y evitó la corrección falsa: `MigrateAsync` **no cede el hilo en ninguno de sus `await`s** —140 ms
  de 140—, así que un `await` habría dejado la ventana igual de bloqueada con aspecto de arreglada.
  El trabajo va a un hilo propio y la ventana sale en el primer fotograma. De regalo, la prueba de
  superficies huérfanas cazó `StartupView` en el acto y pasó a ser la tercera raíz del grafo.
  [audit-arq005-async-startup.md](evidence/stable/audit-arq005-async-startup.md).
- **TST-001: la puerta de cobertura ya vigila código que no es nuevo.** Re-medir fue lo primero y
  el resultado no era el esperado: dos archivos exactamente igual que el día anterior y el tercero
  **quince puntos peor**, porque ARQ-004 se llevó por delante sus líneas cubiertas sin que nada
  avisara. Hay una lista de vigilados con trinquete en los dos sentidos, y dos de las tres deudas
  quedaron al 100 %. [audit-tst1-coverage-debt.md](evidence/stable/audit-tst1-coverage-debt.md).

## Lo terminado el 2026-08-10 (quinta sesión)

Tres commits de código, cada uno con su ciclo, su evidencia bilingüe y su verificación completa.

- **`BUG-010`: la instancia nativa tiene un dueño.** El sondeo de medios levantaba su propia LibVLC
  con las mismas tres opciones que la de reproducción, así que un proceso que catalogaba y
  reproducía mantenía dos motores nativos — y el contador que afirma «una por juego de opciones» no
  podía ver el segundo. Con la cola pasó lo mismo y era peor: desechaba el medio **sin guarda** y
  dejaba su bandera en alto, de modo que un único fallo al liberar habría matado al trabajador para
  siempre. La regla es de código fuente a propósito, porque en ejecución la segunda instancia es
  invisible; y encontró la clase donde el plan nombraba un caso, con una **tercera** cola en el motor
  de reproducción anotada como `BUG-011`.
  [audit-bug010-native-instance.md](evidence/stable/audit-bug010-native-instance.md).

- **TST-001 queda saldado: la última deuda pasa de 86,73 %/76,00 % al 100 % de líneas y de ramas**,
  con nueve unitarias que apuntan a lo que `ReconcileScannedFiles` **decide** —un escaneo cancelado,
  un resultado que el escaneo no pudo catalogar, una ruta sin fila, una identidad ilegible que cuenta
  como fallo sin costarle el escaneo al resto, contenido `Updated` que refresca la identidad, un
  catálogo que lanza, una cancelación que no es un fallo—. Medir la lista antes de escribir la
  recortó en un tercio: cinco de los puntos anotados leyendo el código ya estaban cubiertos. Y
  apareció uno que la lectura no da: la propiedad `AttemptedCount` no la leía **ninguna** prueba,
  porque comparar registros enteros va por campos y no por propiedades. El suelo de la puerta sube a
  100/100. [audit-tst1-reconcile-coverage.md](evidence/stable/audit-tst1-reconcile-coverage.md).
- **El rojo intermitente ya deja diagnóstico.** Y lo primero fue leer el registro archivado de la
  única ejecución que falló, que contestaba media pregunta: `16 migration(s) applied to a new
  database`. Lo que se instrumenta es la mitad que sigue sin recogerse, más el procesador y los
  hilos, que separan girar de esperar. Nada del diagnóstico puede lanzar —sustituiría al fallo que
  viene a explicar—, así que cada lectura va guardada y lo que salga mal se cuenta dentro de la
  frase. `LaunchDiagnosisTests` saca las funciones del guion publicado **parseándolo** y las ejerce
  contra procesos de estado conocido, incluida una `library.db` que no es una base de datos.
  [audit-first-launch-instrumentation.md](evidence/stable/audit-first-launch-instrumentation.md).

## Por dónde se sigue (decidido, no se re-delibera)

1. ~~**`LIB-015`**~~ **Hecho el 2026-08-14**, en el orden que fijaba el plan. Tres cosas cambiaron al
   medirlas: el lanzador endurecido que se iba a reutilizar **no existía** —los tres `Process.Start`
   del árbol abren un `.msix`, una carpeta y un archivo de medios, y ninguno una dirección—; la clave
   de la caché **no incluye la dirección**, así que `append_to_response` habría servido el payload
   anterior como respuesta nueva, y **subir `ProviderVersion` habría sido peor** —esas filas dejarían
   de leerse y el techo de 180 días sólo se aplica al leer esa misma clave, así que nada podría
   borrarlas nunca—, de modo que la migración vacía lo de TMDB; y la puerta de red cazó
   `www.youtube.com` en `src/`, resuelto con una **segunda lista cerrada** (`HandedOff`) en vez de
   declarar una conexión que no existe.
   [audit-lib015-provider-trailer.md](evidence/stable/audit-lib015-provider-trailer.md).
2. ~~**El eslabón que falta**~~ **Hecho el 2026-08-15**, en cuatro commits y con `LIB-006` de vuelta
   en `VERIFIED` (43 verificados, 2 bloqueados). La primera medición que el plan exigía dio más de lo
   esperado: el puente `media_file_id` → `title_id` **es la identidad del GUID** y no costó ninguna
   migración —`titles` no lo escribe nadie en `src/`, así que todo título del catálogo es un archivo
   escaneado—, y lo que sí faltaba era el **nombre del proveedor**, que no viaja con el candidato y
   sin el cual `GetDetailsAsync` lanza.
   [audit-apply-identification.md](evidence/stable/audit-apply-identification.md).
   Detrás salieron **dos defectos que nadie buscaba**: guardar por primera vez en una ficha sin
   editar devolvía `NotFound` en silencio, y **ningún llamante subía la revisión al guardar**, de modo
   que el control optimista comparaba contra un número que nunca se movía y dos ventanas podían ganar
   las dos. Los dobles en memoria sí la subían, que es por qué ninguna prueba unitaria podía verlo.
   [audit-refresh-resolves-itself.md](evidence/stable/audit-refresh-resolves-itself.md).
   Y el clic destapó el tercero: **el paseo ensamblado montaba la ventana de una forma que la
   aplicación no usa**, dejando el shell fuera del árbol lógico y **todos** los botones enlazados por
   `Command` declarándose deshabilitados. Sólo se veía haciendo clic, y nadie hacía clic.
   [audit-walk-clicks-the-editor.md](evidence/stable/audit-walk-clicks-the-editor.md).

   ~~**El eslabón que falta, tal como se describió el 2026-08-14.**~~ La primera medición de `LIB-016` destapó
   que **nada convierte una identificación en metadata guardada**: `catalog_metadata` sólo lo
   escriben el editor y un `RefreshMetadata` que nadie alimenta —la única asignación de su entrada en
   todo el repositorio está **en una prueba**—, `ResolveMatch` publica un evento que no escucha nadie,
   y `ReviewState.Automatic` sólo se calcula. La sinopsis de `LIB-013` y la clave de `LIB-015` sólo
   llegan a la base a mano.
   **Decidido entero, con el orden de los commits**, en
   [audit-identification-never-reaches-the-catalogue.md](evidence/stable/audit-identification-never-reaches-the-catalogue.md):
   un caso de uso `ApplyIdentification` con sus dos llamantes, `RefreshMetadata` resolviendo por la
   referencia guardada, el editor sin la propiedad que nadie rellena, y el paseo ensamblado llegando
   al editor con clics. **La migración `0018` ya dejó la base preparada.**
   **La primera medición, antes de escribir una línea**: cómo se llega del `media_file_id` de los
   candidatos al `title_id` de `catalog_metadata`. Ese puente no está medido.
   **Ya se corrigió el estado de la matriz**: `LIB-006` pasó a `BLOCKED` el 2026-08-14 con su bloqueo
   en el manifiesto —42 verificados, 3 bloqueados—, y `LIB-007` **se queda `VERIFIED`** a propósito,
   porque su criterio es de umbrales y de corrección persistente, y ambos se cumplen. `LIB-006` vuelve
   a `VERIFIED` sólo cuando el recorrido con clics esté verde.
3. ~~**`BUG-012` — el vigilante que muere al desbordar su buffer.**~~ **Hecho el 2026-08-15**, y la
   primera medición desmintió la última mitad de lo que estaba escrito aquí: un root `Continuous`
   **nunca sale** de `_watching`, porque `StartAsync` es un `Task.WhenAll` con el planificador de
   reserva y ése **no termina nunca**, así que ni un escaneo manual podía revivir al vigilante —sólo
   volver a arrancar la aplicación—. Un desbordamiento es ahora «he perdido eventos»
   (`WatchErrorPolicy` en el dominio), viaja como `FileChangeBatch.EventsLost`, se convierte en **un**
   escaneo de recuperación y **la vigilancia sigue**; el buffer se pide al techo de 64 KiB; y un
   vigilante que muere de verdad se reintenta en la siguiente pasada de reserva, que es el latido que
   esta parte ya tenía. **El desbordamiento real no se reproduce en esta máquina** —64 000
   operaciones, cero desbordamientos, con 8 KiB y con 64 KiB—, así que la decisión se prueba en el
   dominio y no se finge una prueba de integración determinista.
   [audit-bug012-watcher-survives-overflow.md](evidence/stable/audit-bug012-watcher-survives-overflow.md).
4. **`LIB-016`** — el refresco automático, apagado por defecto, rancio a los 90 días y 20 fichas por
   pasada. **El texto del propósito de red declarado cambia con el código**, no después. Ya no está
   bloqueado: `catalog_metadata` guarda `provider`, `provider_key` y `refreshed_utc`, que son los dos
   datos por título que le faltaban. **Decidido el 2026-08-15: un `refreshed_utc` nulo cuenta como
   rancio** —una ficha sin fecha nunca se refrescó, así que es la más rancia que hay—, con los nulos
   **primero** en el orden y el tope de 20 por pasada conteniendo la primera pasada de una biblioteca
   entera.
5. **`DES-001` — la instalación también se ve, y hoy no está diseñada.** Los cinco activos de
   `src/ApSolutions.LocalMedia.Windows.Package/Assets/` son marcadores de posición del 3 de agosto
   —de 576 B a 7 KiB— y son **lo primero que alguien ve del producto**, antes que ninguna vista. Y
   hay un defecto medido: el manifiesto declara `es-ES` y `en-US` pero su `Description` es **una sola
   cadena con una barra dentro** («Biblioteca y reproductor de vídeo local / Local video library and
   player»), que Windows enseña tal cual en los dos idiomas. La localización de verdad va con
   `ms-resource:` y un recurso por idioma, como **ya se hace en winget**, que sí tiene sus dos
   `locale.es-ES.yaml` y `locale.en-US.yaml`. Ojo: tocar el paquete obliga a reempaquetar o las
   pruebas de empaquetado fallan.
6. **El rediseño visual**, que el propietario está preparando con Claude Design. Lo que este
   repositorio le debe es el inventario de **todo lo que se ve**, y ya está escrito y medido en
   [docs/design/SURFACES.es.md](design/SURFACES.es.md) y [SURFACES.en.md](design/SURFACES.en.md): 48
   vistas en 15 áreas, las 468 cadenas por idioma, los 23 mensajes distintos de la vista de
   actualización —15 estados y 8 motivos de rechazo, que no son errores del usuario y piden otro
   tratamiento— y los cinco activos de instalación. **`LIB-016` añade superficie visible nueva** (el
   interruptor de refresco automático y su texto), así que el inventario no se cierra hasta que esa
   entrada esté hecha.
7. **Documentación**: `DOC-101`, `DOC-201`, `T44.1`-`T44.6` y el manual de usuario, que se escribe
   desde la aplicación construida y no desde el código — por eso va el último, y sus capturas
   dependen del rediseño.

## Lo terminado el 2026-08-15 (octava sesión)

Cuatro commits, y la cadena que tenía `LIB-006` en `BLOCKED` quedó cerrada: el manifiesto lee **43
verificados, 2 bloqueados**. `ApplyIdentification` escribe lo que el proveedor sabe por sus dos
llamantes —la bandeja y el camino automático de ≥90 %, que no existía—, `RefreshMetadata` resuelve
por la referencia guardada, el editor perdió la propiedad que nadie rellenaba, y el paseo ensamblado
**pulsa el botón con el ratón**. Detalle en
[audit-apply-identification.md](evidence/stable/audit-apply-identification.md),
[audit-refresh-resolves-itself.md](evidence/stable/audit-refresh-resolves-itself.md) y
[audit-walk-clicks-the-editor.md](evidence/stable/audit-walk-clicks-the-editor.md).

**Dos defectos que no estaban en ninguna lista y salieron midiendo**: guardar por primera vez en una
ficha sin editar devolvía `NotFound` en silencio —el editor traducía eso a nada, ni conflicto ni
cambio—, y **ningún llamante subía la revisión al guardar**, de modo que `WHERE revision = $expected`
comparaba contra un número que nunca se movía y dos ventanas podían ganar las dos.

## Lo terminado el 2026-08-14 (sexta sesión)

- **`BUG-011`: una sola cola de liberación en todo el proceso.** El motor de reproducción guardaba
  la tercera, y desechaba el medio nativo **dentro de su candado y sin guarda**: una excepción allí
  salía del bucle con la bandera del trabajador en alto, así que un único fallo lo mataba para
  siempre y todo lo abierto después se filtraba en silencio. El rojo fue **doble a propósito** —la
  regla de origen, que se satisface moviendo texto, y una prueba de comportamiento que mide desde
  fuera dónde descansa el medio cuando el motor lo suelta—. `LibVlcFactory` ganó un vaciado con
  techo que **no lanza** al agotarse, el motor soltó cola, candado, bandera, drenaje y constante de
  reposo (−52/+27), y la lista que sólo puede encoger quedó **vacía**. La prueba de resistencia no
  se escribió de cero: `HandleGrowthTests` ya abría y cerraba el motor treinta veces en un proceso
  hijo, que es el único sitio donde un contador de proceso se puede leer sin que las demás suites
  escriban en él. Y esa columna **no podía ser roja antes**, lo que se dice en la evidencia en vez
  de presentarla como prueba.
  [audit-bug011-engine-release-queue.md](evidence/stable/audit-bug011-engine-release-queue.md).

- **La cola de la auditoría quedó cerrada entera**: `ARQ-013`, `ARQ-012` (−836/+196 en 88 archivos),
  `ARQ-014` y `QA-001` —esta última con **cero** violaciones que corregir: era puerta, no deuda—.
  Más dos rojos ajenos que aparecieron por el camino: el canario de red pedía un puerto de un rango
  que el sistema reserva ([audit-canary-port.md](evidence/stable/audit-canary-port.md)) y una prueba
  del corpus borraba un archivo que otra suite podía estar leyendo
  ([audit-corpus-shared-file-race.md](evidence/stable/audit-corpus-shared-file-race.md)).
- **Y un rojo que habría aparecido en la primera publicación**: la herramienta que firma **no
  compilaba** —faltaba el encabezado de licencia y su proyecto estaba **fuera de la solución**, así
  que ninguna puerta lo construía—, y `release.yml` la ejecuta con `dotnet run` en el paso que
  verifica la firma. Corregido; el proyecto entra en la solución y una regla nueva impide que otro se
  quede fuera. **No lo encontró una puerta de este repositorio**: lo encontró la sesión de IT del
  propietario ejecutando la prueba de restauración de la clave.
  [audit-release-signing-tool-build.md](evidence/stable/audit-release-signing-tool-build.md).

## El bloque nuevo: sinopsis y tráiler (2026-08-14)

Pedido por el propietario y **decidido entero** en
[2026-08-14-synopsis-and-trailer.md](superpowers/plans/2026-08-14-synopsis-and-trailer.md). Va antes
de la documentación a propósito: el manual se escribe desde la aplicación construida, y escribirlo
antes de este bloque sería escribirlo dos veces.

- **`LIB-013` hecho**: la sinopsis se lee en las dos fichas. Ya estaba guardada de punta a punta; lo
  único que faltaba era el camino de lectura.
- **`LIB-014` hecho**: el tráiler **local** se reproduce desde la ficha, con la convención de Plex,
  Jellyfin y Kodi. No hizo falta camino de reproducción nuevo: `OpenLooseFile` ya valida extensión,
  existencia y no escribe fila de catálogo.
- **`LIB-015` pendiente**: la clave de YouTube al navegador. **Cuesta una migración** —campo nuevo en
  `MetadataDetails` y columna—, más `append_to_response=videos` en la petición que ya se hace. No hay
  host nuevo.
- **`LIB-016` pendiente**: el refresco automático, apagado por defecto. **Toca un contrato de
  privacidad**: hoy el propósito declarado de TMDB dice «the metadata a person explicitly asked to
  identify or refresh», y con un refresco automático deja de ser verdad, así que ese texto cambia con
  el código. El techo de 180 días de la caché no se sube nunca.

**La decisión sobre el tráiler remoto no se re-delibera**: dentro de la aplicación sólo si es un
archivo local. La vía de LibVLC contra YouTube incumple sus términos, y su incrustación oficial
pediría un WebView con hosts sin declarar, publicidad y cookies.

## Dos cosas aprendidas el 2026-08-14 que salieron caras

- **Un reemplazo mecánico validado sólo con «compila» es un cambio sin medir.** El guion que migró
  las 59 copias del buscador de raíz se llevó el método **equivocado en catorce archivos**, porque
  buscaba el primero con la forma correcta en vez del que contiene el paseo. Ninguno llegó a un
  commit, y no por suerte: dos los cazó el compilador, **trece los cazó la regla nueva** —la propia
  puerta que ese trabajo añadía midió su propio destrozo— y tres más las suites, porque devolvían
  `…/src/…Presentation` y no la raíz. Lo que cerró el hueco no fue esperar a que algo fallara sino
  buscar en el diff **toda** devolución de un subdirectorio.
- **Un error que habla del arnés esconde el del sistema.** El canario de red falló con un
  `ObjectDisposedException` en su propio constructor, que no dice nada. La primera corrección no hizo
  pasar la prueba: la hizo **decir la verdad** —«ningún puerto libre en el rango»—, y esa frase se
  midió en un comando: Windows reserva 50996-51095 y el rango fijo de la prueba estaba entero dentro.
  No era intermitente; las exclusiones se asignan al arrancar el anfitrión.

## Pendiente tuyo (sólo lo que un agente no puede hacer)

- ~~Añadir el secreto `RELEASE_SIGNING_SECRET_KEY` al repositorio público.~~ **Hecho el 2026-08-10
  (22:46 UTC)**, y era lo único que impedía cortar la primera versión pública: `release.yml` exige
  que `SHA256SUMS.txt.minisig` exista y verifique, y se detenía ahí. Comprobado por nombre y fecha
  con `gh secret list`, que nunca enseña el valor. La copia sigue donde la dejaste (ver
  `SECURITY.md`), y **el respaldo cifrado sigue siendo la única red**: un secreto de Actions no se
  relee.
- El **paseo físico manual de diez minutos**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- La **copia de seguridad cifrada** de la clave de firma. El destino y el cifrado están decididos
  fuera de este repositorio (bóveda de IT); lo que importa aquí es cómo se comprueba que la copia
  sirve, y **no es que el archivo descifre**: se firma algo trivial con la copia restaurada y se
  verifica contra [`eng/release-signing.pub`](../eng/release-signing.pub). Repetir cada trimestre,
  porque un respaldo corrupto no avisa.
  - **Hecho el 2026-08-14**: la sesión de IT del propietario ejecutó la prueba que vale —restaurar,
    firmar y verificar— y el respaldo **sirve**. Comprobado por ejecución, que es la única forma.
  - **Y de paso, dos avisos sobre cómo se comprueba esto.** Desde aquí se midió el respaldo por
    **tamaño** contra un umbral, y esa medición dio «no cumple» cuando la realidad era que sí: un
    proxy puede estar exactamente en su umbral y no significar nada. Después se dedujo, de un archivo
    de 58 bytes, que no podía ser la clave porque no cabe en el **formato de fichero de minisign** —
    y este proyecto no usa ese formato de fichero, sólo su verificación. **Las dos veces el error fue
    el mismo**: sustituir la ejecución por una deducción sobre metadatos. Del tamaño de un archivo
    cifrado no se deduce su contenido, y de un formato que no se ha comprobado que se use, tampoco.
  - Repetir la prueba **cada trimestre**, porque un respaldo corrupto no avisa.
- **La notificación de exportación** a `crypt@bis.doc.gov` y `enc@nsa.gov`: el texto está redactado
  entero en [LEGAL.es.md](legal/LEGAL.es.md) y sale de tu identidad, por eso es tuya.
- **El dictamen jurídico profesional** (`REL-004`), **reducido el 2026-08-14**. Sus dos preguntas de
  licencia están cerradas por ingeniería, no por dictamen: en vez de encargar quién interpretaba el
  §6 de la LGPL-2.1, la versión pasó a la opción que ambas licencias enuncian **sin condiciones**
  —§6(d) y el último párrafo del §3 de la GPL-2.0: ofrecer el fuente desde el mismo sitio del que se
  descarga el binario—, y `release.yml` adjunta ahora `vlc-3.0.23.tar.xz` verificado y el archivo de
  LibVLCSharp. **Lo que queda del encargo es marca, dominio y la notificación de exportación**, que es
  donde el criterio ajeno aporta algo.
  [audit-corresponding-source.md](evidence/stable/audit-corresponding-source.md).
- Las decisiones económicas de siempre: certificado Authenticode, Store, hardware ARM64.

## Cosas aprendidas que conviene no volver a aprender

- **`eng/verify.ps1` no es lo que ejecuta CI**: CI corre además
  `eng/run-accessibility.ps1 -Mode Verify -Passes 2` y `eng/run-recovery.ps1 -Mode Verify -Passes 2`.
  Conviene correrlas. Pero **cuidado con la conclusión fácil**: el rojo que llegó a `main` el
  2026-08-10 apareció en **tres sitios distintos** en tres ejecuciones —pasada 1, pasada 2, y la
  suite dentro de `verify.ps1`—, así que no faltaba una puerta: **la carrera no se reproduce en
  esta máquina**. Más pasadas son más tiradas, no determinismo. Contra una carrera, lo que vale es
  quitarla, no buscarla.
- **Observar un estado transitorio obliga a esperar a que termine antes de salir.** La prueba que
  comprueba la vista de arranque afirma sobre algo que dura lo que dura el trabajo de fondo, y se
  marchaba en mitad: el `Task.Run` seguía con la base abierta cuando el desmontaje borraba la
  carpeta. Aquí terminaba a tiempo y pasaba; en un runner más lento, no. La aserción no necesita la
  espera, pero el desmontaje sí.
- **Una línea base de una sola medida no es una línea base.** La fase que había que vigilar resultó
  ser la más ruidosa de las tres —1245 ms de variación con el mismo código—, y la señal la dieron
  los dos controles que se añadieron «de más». Medir el sujeto sin medir nada con qué compararlo
  produce un número que no puede distinguir el cambio del día que hacía.
- **Un número heredado se re-mide aunque se dé por hecho que ha mejorado.** De los tres de TST-001,
  dos estaban exactamente donde los dejaron y el tercero había **retrocedido**. Lo esperado era lo
  contrario, y suponerlo habría dejado la deuda peor de como se creía.
- **Una premisa escrita en un documento propio también hay que medirla.** «El primer arranque es el
  único que migra» estaba en dos sitios y era falsa: contar `schema_history` en cada carpeta cuesta
  un minuto y descartó la causa candidata del único rojo abierto.
- **Una prueba que falla porque la corrección funcionó no se arregla en el código.** La del nombre
  accesible perdió una carrera contra el propio arranque asíncrono: para cuando miraba, el shell ya
  había ocupado el sitio. Ahí lo que se reapunta es la prueba, y decir por qué evita que la próxima
  lectura la tome por un defecto.

- **Verificar con el teclado no es verificar con el ratón.** El paseo ensamblado conducía la
  aplicación con `Window.KeyPress` y **nadie usaba los clics** de `Avalonia.Headless`. El primer clic
  destapó que el propio paseo montaba la ventana de una forma que la aplicación no usa —
  `AssembledStartup.FinalContent` **saca** el `ShellView` de su contenedor y el paseo lo remontaba en
  otra ventana—, dejando el shell **fuera del árbol lógico**. Un `Button` sólo consulta el
  `CanExecute` de su comando estando en el árbol lógico, así que **todos** los botones enlazados por
  `Command` se declaraban deshabilitados. Los que usan `Click=` no, y por eso era invisible.
- **`Window.InputHitTest` no predice a dónde va un clic** en Avalonia headless: nombraba el
  `ScrollContentPresenter` mientras el clic llegaba al botón. La guarda escrita «para asegurar» el
  clic era lo único que fallaba, y creerla habría declarado roto algo que funciona. Se afirma sobre
  el **efecto**, con un clic **al lado** como control.
- **Antes de instrumentar un fallo, léelo entero donde quedó archivado.** Las dos hipótesis del rojo
  intermitente se daban por indistinguibles en dos documentos, y la línea de aquella ejecución
  —`16 migration(s) applied to a new database`— descartaba una de las dos desde el primer día. Costó
  un `gh run view --log` filtrado. Un registro que nadie ha leído no es una pregunta abierta.
- **Una lista de huecos escrita leyendo el código es una hipótesis.** La de las ramas que faltaban en
  `ReconcileScannedFiles` se recortó **un tercio** al medirla, y el hueco que no estaba en ella
  —una propiedad que ninguna prueba leía— era el que ninguna lectura podía dar, porque la igualdad
  de un registro va por campos.
- **La puerta de cobertura lee de `HEAD`, no del disco.** Con los archivos nuevos sólo preparados en
  el índice declara «ningún archivo nuevo» y sale verde. Hay que confirmar el commit y volver a
  ejecutar `eng/check-coverage.ps1` **antes** del push, o CI será quien encuentre el rojo.
- **Un hallazgo archivado como «no explotable» es un hallazgo sin medir.** De los dos que la auditoría
  dejó anotados, uno era una entrega directa al shell de Windows y el otro no existía. No se supo
  hasta escribir la prueba que lo habría explotado.
- **Una prueba que sale verde antes de la corrección no es una buena noticia**, es la hipótesis
  avisando de que estaba mal. Ahí es donde hay que parar y volver a medir.
- **`eng/verify-package.ps1` compara dos checkouts limpios**, así que se niega a correr con archivos
  sin preparar en el índice. Un `git add -A` antes de `eng/verify.ps1` ahorra media hora.
- **Se extrae una clase cuando sus pruebas pueden seguirla**, y la puerta de cobertura es quien lo
  decide, no la intuición. `WindowLifecycle` compilaba y los recorridos ensamblados estaban en verde;
  aun así volvió, como `WindowsFilePickers` antes.
- **Las pruebas que leen la composición como texto se rompen cada vez que algo se mueve.** Van tres.
  Al sacar código de `CompositionRoot`, actualizar `CompositionSourceText` y `CompositionGraph` es
  parte del traslado, no un arreglo posterior.
- **`AppDomain.UnhandledException` no impide que el proceso termine**, sólo deja constancia. Eso
  invirtió el orden de las dos mitades de ARQ-004: si un comando no puede permitirse dejar escapar
  nada, tiene que capturar siempre — y algo que captura siempre necesita un destino siempre, así que
  el destino va antes.
- **Antes de escribir «asíncrono», comprobar que algo cede el hilo.** `Microsoft.Data.Sqlite`
  implementa buena parte de su superficie `Async` de forma síncrona. Un `await` sobre algo que no cede
  deja el hilo igual de bloqueado **con aspecto de arreglado**, que es peor que no tocarlo.
- **Migrar N clases a una supone que las N hacían lo mismo, y no lo hacían.** Dos de veinticuatro
  guardaban comportamiento propio, y lo cazó la suite, no la lectura: una comprobaba `CanExecute`
  dentro de `Execute` y de ahí colgaba una validación real. Nunca se migra sin correr la suite entera.
- **Dentro de un `async void`, una guarda de argumento no es una guarda**: lanza dentro de la máquina
  de estados y se postea al contexto, de modo que quien se equivocó nunca se entera. Hay que partir el
  método en uno síncrono que valida y otro que espera.
- **Hay rojos que no son rojos, son cuelgues.** Un candado retenido por un hilo que no vuelve no
  produce un aserto roto, produce una suite que no termina. Ahí el «rojo archivado» no existe, y se
  dice en la evidencia en vez de fingir uno.
