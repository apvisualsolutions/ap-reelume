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
3. **`ARQ-014`**, el User-Agent que anuncia `1.0` mientras la versión declarada es `0.1.0`: la marca
   se queda y la versión sale del ensamblado, fijada contra el `<Version>` de
   `Directory.Build.props`.
4. **`ARQ-012`**, una raíz del repositorio y un ancla: hoy hay **dos** —`docs/FEATURES.md` y el
   `.sln`— y una docena de copias del mismo `while`. Decidido: ancla el `.sln`, archivo compartido
   en `tests/Shared/`, y una regla que sólo puede encoger.
5. **`QA-001`**, cultura: **no se escribe un regex**, se encienden `CA1305`/`CA1304`/`CA1310` como
   error. Primero se cuentan los avisos por proyecto, y el criterio al corregir es invariante para
   lo que se guarda o viaja, cultura de la interfaz para lo que lee una persona.
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

## Pendiente tuyo (sólo lo que un agente no puede hacer)

- ~~Añadir el secreto `RELEASE_SIGNING_SECRET_KEY` al repositorio público.~~ **Hecho el 2026-08-10
  (22:46 UTC)**, y era lo único que impedía cortar la primera versión pública: `release.yml` exige
  que `SHA256SUMS.txt.minisig` exista y verifique, y se detenía ahí. Comprobado por nombre y fecha
  con `gh secret list`, que nunca enseña el valor. La copia sigue donde la dejaste (ver
  `SECURITY.md`), y **el respaldo cifrado sigue siendo la única red**: un secreto de Actions no se
  relee.
- El **paseo físico manual de diez minutos**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- La **copia de seguridad cifrada** de la clave de firma, que **hoy no existe**: medido el
  2026-08-10, el archivo local es el ejemplar **único** y ninguna copia lo alcanza. El destino y el
  cifrado están decididos fuera de este repositorio (bóveda de IT); lo que importa aquí es cómo se
  comprueba que la copia sirve, y no es que el archivo descifre: se firma algo trivial con la copia
  restaurada y se verifica contra [`eng/release-signing.pub`](../eng/release-signing.pub). Repetir
  esa comprobación cada trimestre, porque un respaldo corrupto no avisa.
- **La notificación de exportación** a `crypt@bis.doc.gov` y `enc@nsa.gov`: el texto está redactado
  entero en [LEGAL.es.md](legal/LEGAL.es.md) y sale de tu identidad, por eso es tuya.
- **El dictamen jurídico profesional** (`REL-004`). Le quedan dos preguntas concretas de licencia, y
  las dos son de forma y no de entrega: bajo qué apartado del §6 de la LGPL-2.1 queda amparada la
  manera en que LibVLC viaja aquí, y si la oferta escrita de código correspondiente que recoge
  `NOTICE-VideoLAN.txt` basta como el acompañamiento que pide el §3 de la GPL-2.0.
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
