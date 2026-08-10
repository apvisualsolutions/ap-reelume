# Dónde retomar

Estado del proyecto al cerrar la **cuarta** sesión del **2026-08-10**, la que ejecutó la cola de la
línea base, el arranque asíncrono y la deuda de cobertura. La versión inglesa está en [NEXT-SESSION.en.md](NEXT-SESSION.en.md). El registro
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

La cola de tres —línea base, ARQ-005 segunda mitad, deuda de TST-001— **se ejecutó entera el
2026-08-10** (cuarta sesión). Lo que queda, en orden de valor:

1. **`ReconcileScannedFiles.cs`, la última deuda de cobertura.** 98 líneas y 50 ramas, a 86,73 % y
   76,00 %, ya vigilado con ese suelo. **La forma está decidida en el plan**: unitarias con dobles
   en memoria en `Application.Tests/Discovery/ReconcileScannedFilesTests.cs`, apuntando a las
   **decisiones** y no al camino feliz —que ya cubren los recorridos de escaneo, y por eso las
   líneas van al 87 % y las ramas al 76 %—, con la lista de ramas que faltan escrita una a una. Al
   terminar hay que **subir el suelo**, o la puerta falla: eso es el trinquete.
2. **El rojo intermitente, que sigue sin causa.** Decidido: **no** salir a buscarlo —es una carrera
   y no se reproduce aquí— sino **instrumentar su camino**, para que la próxima vez deje diagnóstico
   en vez de un `exit code -1` mudo. Detalle y forma en el plan.
3. **Lo que quede del plan de remediación** en
   [2026-08-08-audit-remediation.md](superpowers/plans/2026-08-08-audit-remediation.md).

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

**Lo que no se hace**: subir el plazo de 90 s. Eso convierte la única señal que hay en silencio, que
es el error que ya costó seis ejecuciones con los `cancelled` del generador de medios. Si vuelve a
aparecer, deja de ser trabajo pendiente y pasa a ser la corrección urgente.

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

## Pendiente tuyo (sólo lo que un agente no puede hacer)

- **Añadir el secreto `RELEASE_SIGNING_SECRET_KEY` al repositorio público.** No se pudo copiar —los
  secretos no se leen—, y **sin él la tubería de publicación falla a propósito**: `release.yml`
  comprueba que `SHA256SUMS.txt.minisig` existe y verifica, y se detiene si no. Es lo único que separa
  al proyecto de poder cortar su primera versión pública. La copia está donde la dejaste (ver
  `SECURITY.md`).
- El **paseo físico manual de diez minutos**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- La **copia de seguridad cifrada** de la clave de firma.
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
