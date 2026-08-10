# Plan de remediación de la auditoría profunda / Deep-audit remediation plan — 2026-08-08

Registro ejecutable de lo que la auditoría profunda del 2026-08-08 encontró y aún no está corregido,
ordenado en paquetes de trabajo aprobables por separado. Lo ya corregido consta al final con su
evidencia. / The executable record of what the 2026-08-08 deep audit found that is not yet fixed,
ordered into separately approvable work packages. What is already fixed is recorded at the end with
its evidence.

**Método por tarea / Method per task:** RED archivado → corrección mínima → GREEN + puertas →
evidencia (en `docs/evidence/stable/`) → changelogs ES/EN → un commit → push con `main` en
fast-forward y CI vigilada. Los asserts de cableado (patrón `*WiringTests` en UiTests) son la
herramienta contra la clase de defecto «registrado y nunca alimentado», que apareció diez veces.

## WP-2 — Cableado del ensamblado (siguiente recomendado / recommended next)

El mayor salto funcional restante. Empezar por la puerta y cablear por orden de valor:

- [x] **Puerta registrado→consumido**: prueba de arquitectura que exija, para cada servicio
      registrado en `CompositionRoot`, al menos una resolución fuera del propio registro.
      **Hecho 2026-08-08 (tarde)**: `ServiceConsumptionTests` (ArchitectureTests) — el RED enumeró
      **32** huérfanos, no ~12; la deuda vive en `PendingWiring` con identificador por entrada y una
      aserción de caducidad que expulsa cada entrada al aterrizar su cableado. Evidencia en
      [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md). Las caras nuevas están
      abajo como AUD-A01, PLY-A01, CNT-A01, LIB-A01 y ARQ-A01.
- [x] **Identificación (LIB-006/007)**: `IdentifyMediaFile` está registrado y nada lo invoca — la
      bandeja de revisión queda siempre vacía. **Hecho 2026-08-08 (tarde)**: `IdentifyScannedFiles`
      recorre el resumen del escaneo (decididos en paz, `Unchanged` nunca identificados sanan, un
      fallo no detiene el resto) y `ScanRootAsync` se lo entrega; paseo de punta a punta con SQLite
      real en `ScanIdentificationTests`. Filas a `VERIFIED`, manifiesto en 38/1/7. Evidencia en
      [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
- [x] **Vigilancia de raíces (LIB-002/003)**: `RootWatchCoordinator`, `DebouncedFileWatcher` y el
      planificador de respaldo registrados y nunca resueltos — no hay escaneo incremental.
      **Hecho 2026-08-08 (tarde)**: `RootWatchBackground` vive y muere con la aplicación (arranque
      con la ventana, incorporación al primer escaneo, parada a la salida), el watcher sólo corre
      para raíces `Continuous`, el planificador gana `DefaultRecoveryInterval` (15 min), y
      `IdentifyingScanCoordinator` hace que todo escaneo identifique. Paseo de punta a punta con
      disco real en `ContinuousWatchTests`. Filas a `VERIFIED`, manifiesto en 40/1/5. **La mitad
      de archivos movidos aterrizó el 2026-08-09 (tarde)** — ver la cara LIB-002/003 abajo.
      Evidencia en [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
- [x] **Siguiente episodio (PLY-011)**: `StartNextEpisodeCountdown` nunca se resuelve y
      `NextEpisodeViewModel.Offer` nunca se llama. **Hecho 2026-08-08 (tarde)**: el motor traduce
      `EndReached` a `PlaybackState.Ended` (probado con decodificación real), el final de un
      episodio dispara `OfferNextEpisodeAsync` (cuenta atrás de T28, botones cableados, vuelta a la
      ficha sin siguiente o sin archivo) y el episodio elegido se abre por el shell. Fila a
      `VERIFIED`, manifiesto en 41/1/4. Evidencia en
      [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
- [x] **Atajos y teclas multimedia (PLY-014 / ARQ-002)**: `IMediaKeySource` sin `StartAsync`,
      `InputCommandRouter` sin instanciar, cero `KeyBinding` en el reproductor; inyectar el
      `ShortcutMap` singleton en `ShortcutSettingsViewModel` (hoy `?? new` puede crear dos mapas).
      Marshalizar `CommandReceived` con el dispatcher (llega del hilo STA). **Hecho 2026-08-08
      (tarde)**: un router por sesión, `PlayerView` con foco y gestos por el mapa compartido,
      teclas multimedia arrancadas por sesión y marshalizadas, editor con mapa exigido (sin
      `?? new`), y las teclas esenciales operando la sesión en headless (`ShortcutChainTests`).
      Fila a `VERIFIED`, manifiesto en 42/1/3. Evidencia en
      [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
- [x] **Duplicados (LIB-008)**: `GroupMediaVersions` no se invoca desde la aplicación; los grupos
      nunca se crean. **Hecho 2026-08-08 (tarde)**: `GroupScannedVersions` en el pipeline
      compartido (todo escaneo agrupa), `FindByMemberAsync` + repliegue en las dos superficies (el
      grupo se alcanza desde cualquier copia), y el paseo con dos copias reales en
      `ScanVersionGroupingTests` (grupo solo, preferencia que sobrevive, inventario intacto). Fila
      a `VERIFIED`, manifiesto en 43/1/2. **Pendiente de este bloque (VSW-A01)**: mover una sesión
      en reproducción a otra versión sigue sin superficie de origen — `SwitchMediaVersion` y el
      diálogo de confirmación quedan contados en la puerta. Evidencia en
      [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
- [x] **Marcas en vivo (BUG-008)**: las marcas de sesión son una instantánea al abrir; recomponer
      tras guardar/borrar/aceptar/corregir para que el botón de saltar aparezca sin reabrir.
      **Hecho 2026-08-08 (tarde)**: `RefreshSessionMarkersAsync` relee, recompone con la regla
      probada y recarga el editor; las cinco mutaciones recomponen antes de devolver. Evidencia en
      [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
- [x] **`PlayerWindowCoordinator` (ARQ-008)**: registrado en DI y a la vez `new` en `ShellView`;
      decidir el dueño y retirar el otro. **Hecho 2026-08-08 (tarde)**: el dueño es `ShellView`
      (estado de ventana por vista); el registro del contenedor era la mitad muerta y se retiró,
      con `WindowCoordinatorOwnershipTests` como guardia. Evidencia en
      [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
- [ ] **Caras nuevas que la puerta destapó (2026-08-08 tarde)** — mismas reglas, identificador
      propio. **Diseño decidido el 2026-08-09 (experto) para cada cara restante**, de modo que la
      siguiente sesión ejecute sin re-deliberar:
  - **LIB-002/003 (reconciliación)**: `ReconcileScanResults` monta en el pipeline compartido del
    escaneo (como la identificación y el agrupado en `IdentifyingScanCoordinator`): todo escaneo
    reconcilia archivos movidos; lo que no se reconcilia solo aparece en la bandeja de revisión
    como elemento de reasignación que abre `ManualReassignmentViewModel`. Nada nuevo de navegación:
    la bandeja ya existe y es donde una persona revisa lo dudoso. **Hecho 2026-08-09 (tarde)**:
    `ReconcileScannedFiles` primero en el pipeline (capturando identidades con
    `CompositeFileIdentityProvider`), reasignación exacta automática con absorción de la fila
    extraña, copia presente → agrupado, lo ambiguo re-derivado en cada escaneo hasta que una
    persona decide en la bandeja; `ICatalogRepository` retirado como registro muerto (regla
    ARQ-A01). Cuatro entradas fuera de `PendingWiring`; e2e con disco y SQLite reales en
    `ScanReconciliationTests`. Evidencia en
    [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
  - **CNT-A01**: `SetWatchStatus` se inyecta como manejador del conmutador de «visto» donde
    `CreateLibraryViewModel` hoy pasa nulo, y el registro muerto de `WatchStatusViewModel` se
    retira en el mismo cambio (su entrada sale con él). `ConfigureWatchedThreshold` gana su
    superficie en los ajustes de recomendaciones (es una regla de continuidad, y esa pantalla ya
    habla de qué cuenta como visto).
  - **LIB-A01**: retirar una raíz vive en la gestión de carpetas de la biblioteca (la superficie de
    onboarding que ya lista las raíces), con confirmación que diga la verdad: se retira del
    catálogo, ningún vídeo del disco se toca, y re-añadir la carpeta lo re-cataloga.
  - **VSW-A01**: el origen del cambio de versión en vivo es el reproductor — la acción aparece
    sólo cuando el título en reproducción tiene grupo de versiones, y dispara `SwitchMediaVersion`
    con el diálogo de confirmación ya construido y probado (T31). Sin superficie nueva fuera de la
    sesión. **Hecho 2026-08-09 (tarde)**: `PlayerVersionsViewModel` en el panel de la sesión, el
    diálogo construido con manejador real (confirmar re-ejecuta confirmado; «empezar de nuevo» usa
    el reinicio explícito nuevo `RestartFromZero`; cancelar no toca nada), superficies
    reconstruidas tras el cambio y el registro sin manejador retirado. Entrada fuera de
    `PendingWiring`. Evidencia en
    [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
  - **AUD-A01**: primero el motor — `IMediaPlayerEngine` gana `SetAudioOutputDeviceAsync`
    (LibVLCSharp `SetOutputDevice`), con prueba de audio real en MediaTests; después un adaptador
    `IAudioOutputTarget` sobre el motor; por último `AudioOutputViewModel` gana un manejador de
    selección (opcional, como `GestureHandler`) que `OpenPlayerAsync` cablea a
    `LibVlcAudioOutputAdapter.SelectAsync` con ámbito global. Ese orden, porque el VM sin motor
    sería otra superficie alcanzable y nunca alimentada.
  - **ART-A01**: la descarga de arte monta en la identificación (candidato automático o aceptado →
    `ArtworkCache` dentro de su propósito de red ya declarado y su techo de 10 MB), y las
    superficies que lo muestran son la tarjeta de la biblioteca y la ficha. Si al aterrizar se ve
    desproporcionado para el MVP, la alternativa honesta es retirar el registro como ARQ-A01 y
    documentar el hueco — nunca dejarlo en silencio.
  - [x] **AUD-A01** — `LibVlcAudioOutputAdapter` registrado y nunca resuelto: elegir un dispositivo
        de salida en `AudioOutputViewModel` nunca llega al motor. **Hecho 2026-08-09 (tarde)** en
        el orden decidido: motor (`SetAudioOutputDeviceAsync`, probado con decodificación real y
        el caso sin sesión), adaptador (`EngineAudioOutputTarget`), manejador
        (`SelectionHandler` opcional cableado a `SelectAsync` con ámbito global; el dispositivo
        guardado se aplica al abrir sin pasar por `SelectAsync` para no guardar repliegues).
        Entrada fuera de `PendingWiring`; PLY-004 sigue `BLOCKED` por hardware (5.1/7.1 sin
        endpoint real). Evidencia en
        [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
  - [x] **ART-A01** — `ArtworkCache` registrado y nunca resuelto: nadie descarga arte y ninguna
        superficie lo muestra (la mitad de arte del hueco que encontró ADR-0003). **Resuelto
        2026-08-09 (tarde) por la alternativa honesta pre-autorizada**: el registro se retiró
        (regla ARQ-A01) — cablear la cadena completa (póster por el almacén de candidatos, dos
        superficies de imagen, pruebas de red) es desproporcionado para un MVP cuya identificación
        remota viaja desactivada. **El hueco queda documentado**: el MVP no descarga ni muestra
        arte remoto; la clase, sus pruebas SEC-005 y su propósito de red declarado permanecen para
        cuando se decida cerrarlo. Con esta cara `PendingWiring` queda **vacío**. De paso arrancó
        ARQ-006 (ver WP-6). Evidencia en
        [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
  - [x] **PLY-A01** — `ApplyPlaybackPreferences` sin invocar. **Hecho 2026-08-09**: `OpenPlayerAsync`
        aplica las preferencias resueltas (archivo sobre serie sobre global, con repliegue por
        idioma) en cuanto el medio abre, y el selector de pistas recibe las pistas efectivamente
        aplicadas. La entrada salió de `PendingWiring` y el paseo físico ensamblado recorre el
        camino con sesiones reales.
  - [x] **CNT-A01** — `SetWatchStatus` inalcanzable (el conmutador de «visto» se construye con
        manejador nulo en `CreateLibraryViewModel`) y `ConfigureWatchedThreshold` sin superficie.
        **Hecho 2026-08-09 (tarde)**: la tarjeta entrega el manejador real (marcar guarda como
        decisión manual; quitar recalcula bajo el umbral en vigor), el registro muerto de
        `WatchStatusViewModel` se retiró en el mismo cambio, y el umbral se configura en los
        ajustes de recomendaciones (50–100 %, con recuento de estados recalculados). Tres entradas
        fuera de `PendingWiring`; paseo ensamblado marca→recarga→limpia contra SQLite real.
        Evidencia en [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
  - [x] **LIB-A01** — `RemoveLibraryRoot` sin superficie: no hay manera de retirar una carpeta de
        la biblioteca desde la aplicación. **Hecho 2026-08-09 (tarde)**: la superficie de
        onboarding lista las carpetas catalogadas (releídas en cada visita a la ruta) y retirar es
        una decisión confirmada que dice la verdad — se retira del catálogo, ningún vídeo del disco
        se toca, re-añadir re-cataloga. El shell recarga el catálogo tras la retirada. Entrada
        fuera de `PendingWiring`; paseo ensamblado con el archivo comprobado intacto en disco.
        Evidencia en [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md).
  - [x] **ARQ-A01** — registros muertos duplicados por un `new` manual. **Hecho 2026-08-09** (menos
        `WatchStatusViewModel`, que pertenece a CNT-A01): retirados `PlayerViewModel`,
        `SkipMarkerViewModel`, `MarkerEditorViewModel`, `StartPlayback`, `StopPlayback` e
        `IMigrationRunner` — seis entradas fuera de `PendingWiring`, con el RED de la puerta
        nombrándolos archivado. De paso, la aserción de alcanzabilidad que se apoyaba en el
        registro muerto de `StartPlayback` afirma ahora el coordinador real.
- [ ] Al aterrizar cada bloque: devolver su fila de FEATURES.md a `VERIFIED` con evidencia enlazada
      y retirar su bloqueador del generador + regenerar el manifiesto.

## WP-4 — Endurecimiento del actualizador

- [x] **SEC-004**: allowlist de hosts por salto de redirección. **Hecho 2026-08-08 (tarde)**:
      `NetworkPurpose.AdditionalHosts` + regla de comodín estrecha, cada salto del descargador
      dentro de la allowlist (por defecto, la del registro), `UndeclaredHost` con nombre en
      pantalla, `ArtworkCache` limitado a su propósito, y el candado registro↔declaración lee
      también los hosts adicionales (PRIVACY ES/EN actualizados). Evidencia en
      [audit-wp4-updater.md](../../evidence/stable/audit-wp4-updater.md).
- [x] **SEC-005**: límites de tamaño de respuesta. **Hecho 2026-08-08 (tarde)**: 1 MB de metadatos
      en flujo, corte del paquete en cuanto `received > SizeInBytes` (parcial envenenado borrado),
      10 MB de arte con el anterior sobreviviendo. Evidencia en
      [audit-wp4-updater.md](../../evidence/stable/audit-wp4-updater.md).
- [x] **SEC-003**: el hash esperado venía del mismo JSON sin firmar. **Hecho 2026-08-09**: firma
      detached minisign de `SHA256SUMS.txt` en cada publicación (clave pública embebida en
      `UpdateSigningKey` y en `eng/release-signing.pub`; privada en el secreto de Actions
      `RELEASE_SIGNING_SECRET_KEY` + copia custodiada del propietario), verificación en el proveedor
      sobre los bytes canónicos con el hash saliendo sólo del bloque verificado, rechazo
      `UnsignedChecksums` con texto ES/EN, paso de firma en `package-x64`/`release.yml` y bloqueador
      en `prepare-release`. Modelo de confianza en PRIVACY/SMARTSCREEN/RELEASING. Evidencia en
      [audit-wp4-updater.md](../../evidence/stable/audit-wp4-updater.md). WP-4 completo.

## WP-5 — Decisión de firma / Signing decision

**Resuelta en dos mitades el 2026-08-09.** La mitad que cuesta dinero sigue siendo del propietario
y sigue pospuesta: comprar certificado (~200-400 €/año) o re-firma de la Store — bloquea REL-003,
PRD-002 de punta a punta y la utilidad del canal MSIX, con el límite documentado con honestidad en
PRIVACY y SMARTSCREEN. La mitad técnica queda **decidida como experto** (autorizado el 2026-08-09):

- **(c) es la vía vigente**: el canal ZIP es la vía real de actualización mientras no haya
  Authenticode, y el actualizador lo ofrece.
- **SEC-003 se corrige con firma detached minisign** — la opción intermedia sin coste que el propio
  plan nombraba: cada publicación firma `SHA256SUMS` con una clave minisign cuyo **público** viaja
  embebido en el binario; el actualizador verifica la firma antes de creer el hash, con lo que el
  hash esperado deja de venir del mismo JSON sin firmar. La clave privada vive fuera del
  repositorio (secreto de GitHub Actions + copia del propietario); rotarla es publicar una versión
  nueva. Esto no toca la decisión de Authenticode: son capas distintas (una autentica el paquete
  ante el actualizador; la otra, ante SmartScreen y Windows).
- [x] Implementar: **hecho 2026-08-09** — ver SEC-003 en WP-4. La clave se generó, la privada quedó
      como secreto de Actions y copia local custodiada del propietario (fuera del repositorio, con
      instrucciones), y el modelo de confianza está en PRIVACY/SMARTSCREEN/RELEASING ES/EN.

## WP-6 — Arquitectura del host

En este orden (el paso 1 desbloquea el resto):

- [x] **ARQ-006 paso 1**: convertir las ~6 aserciones sobre el TEXTO de `CompositionRoot.cs`
      (`SqliteIsolationTests:83-87`, `SurfaceReachabilityTests:115-178`, `UpdateSourceTests:60`) en
      aserciones sobre `IServiceCollection` (extraer `AddLocalMedia(this IServiceCollection, …)`).
      **Iniciado 2026-08-09 (tarde)** al retirar ART-A01; **completado 2026-08-09 (noche)**: el
      constructor explícito del `MigrationRunner`, el coordinador único de sesiones y el singleton
      de la superficie de actualizaciones se afirman ahora por descriptores en
      `CompositionDescriptorTests`, y la dirección del actualizador se afirma sobre el **objeto**
      que la aplicación compone (`GitHubReleaseUpdateProvider` expone su dirección) contra los dos
      changelogs — ya no sobre el texto del archivo. Dos mitades de invocación que ningún
      descriptor puede expresar quedan declaradas: el arranque del check automático sigue textual
      (hasta que el arranque salga del archivo, pasos 2-3/ARQ-001) y `videoStatus.Apply` sigue
      textual porque ese VM se construye a mano en `OpenPlayerAsync`. / Completed: the explicit
      migration-runner constructor, the single session coordinator, and the update surface's
      singleton are asserted on descriptors; the updater's address is asserted on the composed
      object against both changelogs. Two invocation halves no descriptor can express stay
      declared as textual until the startup path leaves the file.
- [x] **ARQ-006 pasos 2-3**: partir el registro en módulos (`AddData`, `AddPlayback`, …) y extraer
      `WindowsFilePickers`, `DatabaseStartup`, `WindowLifecycle` (el archivo ronda las 1.200 líneas).
      **Hecho 2026-08-10**: el registro es ahora ocho módulos por área (`AddData`, `AddPlayback`,
      `AddPersonalisation`, `AddLibrary`, `AddSettingsAndBackup`, `AddUpdates`,
      `AddAppearanceAndLifecycle`, `AddIdentification` + `AddCatalogEditing`), repartidos en seis
      parciales; `CompositionRoot.cs` baja de 1.857 líneas. `DatabaseStartup` salió con
      `FindLatestBackup` y **cinco pruebas** — no era alcanzable mientras fue privada.
      `WindowsFilePickers` se intentó y se devolvió: la puerta de cobertura lo midió al 0 % (no hay
      forma de ejercitar un diálogo de Windows sin ventana) y los pickers volvieron a
      `CompositionRoot.cs`, igual que `CreateRecoveryView` y `HandleRecoveryAction`. Regla que queda:
      se extrae lo que se puede sostener con pruebas. `WindowLifecycle` no se extrae:
      `ConfigureWindow` está tejido con el arranque que ARQ-001 va a mover de todos modos, y sacarlo
      ahora obligaría a moverlo dos veces. \ Done: the registration is eight area modules across six
      partials; two classes extracted, one of them arriving with the tests its logic never had.
      `WindowLifecycle` is deliberately left for ARQ-001, which moves the startup path anyway.
- [x] **Deuda descubierta al partirlo**: las pruebas de cableado abrían `CompositionRoot.cs` por su
      nombre, de modo que «la composición» significaba «un archivo» y ocho de ellas se pusieron rojas
      sin que cambiara un solo cable. Ahora leen todos los `CompositionRoot*.cs` desde una fuente
      única (`CompositionSourceText`), y `ServiceConsumptionTests` —la puerta contra el defecto de la
      casa— hace lo mismo, porque leer un archivo habría encogido el grafo en silencio y dejado pasar
      justo lo que existe para cazar. Verificado con una mutación: alterar el disparador de la
      persistencia rompe su prueba y restaurarlo la devuelve a verde. \ The wiring tests opened one
      file by name; they now read every partial from one source, and so does the consumption gate.
- [x] **La puerta de cobertura confundía ruta nueva con código nuevo.** `check-coverage.ps1` decidía
      con `git diff --diff-filter=A`, así que un módulo lleno de líneas publicadas hacía meses
      aparecía como código recién escrito al 46 %; sostenerlo habría significado que la puerta empuja
      contra la limpieza que existe para hacer segura. Ahora compara el **código** (sin comentarios ni
      `using`) contra el corpus del `BaseRef` y anuncia archivo por archivo lo que exime por movido.
      No se debilita: para colar código sin cubrir habría que haberlo escrito antes en el árbol base,
      donde la misma puerta lo habría retenido — y lo demostró reteniendo `WindowsFilePickers`. \ The
      gate decided by path rather than by content; it now compares code and announces what it exempts.
- [x] **ARQ-001 / WIN-005 / resto de BUG-004**: `ApplicationHost : IAsyncDisposable` que posea el
      `ServiceProvider`; liberar en `ShutdownRequested` (LibVLC, SQLite, bandeja, hotkeys, HttpClients);
      retirar `DisableParallelization` de `AssembledShellSuites` como prueba. **Hecho 2026-08-10**: el
      proveedor tiene dueño, y `PendingActivationPath` y el estado de la sesión de reproducción
      salieron de los estáticos. `DisableParallelization` **retirado** y las 70 pruebas de
      accesibilidad en verde, que es la prueba. `WindowLifecycle` —lo que ARQ-006 dejó a propósito—
      se extrajo, la puerta de cobertura lo midió al 70,89 % de líneas y 28,57 % de ramas, y **volvió**
      igual que `WindowsFilePickers`: resuelve diez servicios del contenedor y sus ramas no se
      alcanzan sin fabricar un proveedor entero de dobles. `ConfigureWindow` sí perdió todo estático.
      Dos desviaciones más, ambas razonadas en la evidencia: se libera en el `finally` de `Main`
      en vez de en `ShutdownRequested` (estrictamente más tarde y más seguro, y cubre la salida por
      bandeja), y la instancia **nativa** de LibVLC sigue viviendo lo que vive el proceso a propósito.
      Destapó de paso un defecto de la puerta de consumo: leía `new (\w+)`, así que un tipo anidado
      calificado se registraba con el nombre de su contenedor. Evidencia en
      [audit-arq001-application-host.md](../../evidence/stable/audit-arq001-application-host.md). \
      Done: the provider has an owner, the window lifecycle came out with the move, and the
      parallelisation switch came off.
**Orden decidido el 2026-08-10 (experto), para que la siguiente sesión no re-delibere**: ARQ-010
primero, ARQ-004 después, ARQ-005 al final. ARQ-010 es una línea y **es una medición**: enciende la
validación del contenedor al construirlo, de modo que cualquier registro roto sale en el arranque de
cada prueba en vez de en la resolución que lo tocara. Barato, y da señal antes de dos refactores
grandes. ARQ-004 va antes que ARQ-005 porque el arranque asíncrono va a producir precisamente el tipo
de fallo que ARQ-004 existe para no perder: si se invierte el orden, ARQ-005 aterriza sobre un suelo
que todavía se traga excepciones. \ Order decided 2026-08-10: ARQ-010, then ARQ-004, then ARQ-005.

- [x] **ARQ-010**: `ValidateOnBuild = true`. Una línea en `ApplicationHost.Create`. Cuenta como hecho
      cuando una prueba fije que la validación está encendida — si no, se apaga sin que nadie lo note.
      Hecho el 2026-08-10. La construcción salió a `ApplicationHost.BuildProvider` para que la prueba
      pase una colección rota **por la ruta del producto**; afirmar sobre una copia de las opciones
      sólo demuestra la copia. **No destapó ningún registro roto** (73/73, 18/18, 382/382 a la
      primera), y el límite quedó medido: valida 109 de 156 registros, porque los 45 por factoría son
      opacos por construcción. Cuesta +0,22 ms por contenedor. Evidencia en
      [audit-arq010-container-validation.md](../../evidence/stable/audit-arq010-container-validation.md). \
      Done: the build moved into its own method so a test can break it through the product's path;
      it exposed nothing, and covers 109 of 156 registrations.
- [x] **ARQ-004** (completo el 2026-08-10, las dos mitades): un único `AsyncRelayCommand` con manejo de errores (hay ~24 `async void` sin red,
      cobertura desigual entre ViewModels) + `UnhandledException`/`UnobservedTaskException` globales
      en `Program.cs`. **Límites decididos**: vive en `Presentation`, porque es una preocupación de
      los ViewModels y no del anfitrión; un fallo aterriza en el estado de error de su propia
      superficie y **nunca** mata el proceso; y los manejadores globales de `Program.cs` respetan la
      lista blanca de diagnóstico — registran un código, jamás un mensaje libre, que es por donde se
      escapa una ruta o un nombre. El diseño fino se decide **midiendo** los ~24 sitios, no aquí:
      inventarlo sin leerlos sería exactamente lo que este repositorio castiga.
  - [x] **Primera mitad, la red** (2026-08-10). La medición cambió el orden de las dos mitades:
        `AppDomain.UnhandledException` **no** impide que el proceso termine, sólo deja constancia, así
        que un comando no puede permitirse dejar escapar nada — tiene que capturar siempre, y algo que
        captura siempre necesita un destino siempre. Ese destino no existía: **2 de las 24 superficies**
        tienen estado de fallo, y el informe de diagnóstico se construía de **una sola fuente**, la
        auditoría de renombrados, así que en una sesión sin renombrados una aplicación que fallaba
        parecía sana. Ahora hay `ISessionFailureLog` (en memoria, una por aplicación, tope de 32
        códigos) y `ProcessFailureHandlers`, y el informe lee de las dos fuentes. Nada se escribe en
        disco y ningún manejador formatea texto propio. Evidencia en
        [audit-arq004-failure-net.md](../../evidence/stable/audit-arq004-failure-net.md). \
        First half: the net, because a command that must always catch always needs somewhere to put it.
  - [x] **Segunda mitad, el comando único** (2026-08-10). De **27 `async void` a 2**, y los dos
        capturan: `AsyncRelayCommand.Execute` y `GuardedEvent`, porque ni un `ICommand` ni un manejador
        de evento devuelven tarea que nadie pueda esperar — en algún sitio la espera para, y ahora para
        dentro de un `catch`. −582/+227 líneas. Las 13 clases `ICommand` que quedan son síncronas y
        nunca estuvieron en el alcance. La migración rompió dos pruebas y **eso fue lo mejor**: una
        superficie comprobaba `CanExecute` dentro de `Execute` y de ahí colgaba la validación de la
        valoración (ahora es la regla del comando, y está fijada), y la barra de transporte llevaba una
        guarda de re-entrada propia (reconstruida en su ViewModel, porque es su regla). Evidencia en
        [audit-arq004-single-command.md](../../evidence/stable/audit-arq004-single-command.md). \
        Second half: 27 async void down to 2, and the two that remain both catch.
- [x] **ARQ-005** (completo el 2026-08-10): arranque sin `GetAwaiter().GetResult()` en el hilo de UI (migración+integridad);
      sacar el bloqueo del `lock` en `WindowsMediaKeyService`. **Límite decidido**: la ventana no
      puede quedarse en blanco mientras migra. Lo que hoy bloquea el hilo devuelve una vista y sólo
      después decide si esa vista es el shell o la de recuperación, así que la versión asíncrona tiene
      que mostrar algo desde el primer fotograma y cambiarlo al terminar — y `AssembledJourneyTests`
      tiene que seguir viendo el shell al final, que es la prueba de que el cambio no rompió el
      arranque.
  - [x] **Primera mitad, las teclas multimedia** (2026-08-10). La espera salió del `lock` y recibió
        techo (5 s al arrancar, 2 s al parar). El defecto tenía dos caras: el hilo de interfaz se
        paraba en **cada apertura de vídeo** hasta que un hilo con código nativo contestaba, y si no
        contestaba nunca, **el mismo hilo atrapado sujetaba el candado** que `StopAsync` necesitaba.
        `IsListening` pasa a ser cierto en cuanto la bomba existe, no cuando contesta, para cerrar la
        ventana en la que un `Stop` no encontraba nada que parar. **El rojo aquí no fue un rojo, fue
        un cuelgue**, y por eso la bomba es sustituible: un techo que nadie ha visto expirar es un
        techo que nadie sabe si funciona. Evidencia en
        [audit-arq005-media-keys.md](../../evidence/stable/audit-arq005-media-keys.md). \
        First half: the wait left the lock and got a ceiling.
  - [x] **Segunda mitad, el arranque asíncrono** (2026-08-10). **La medición previa era el punto**:
        `MigrateAsync` **no cede el hilo en ninguno de sus `await`s** —retuvo al llamante 140 ms de
        los 140 ms que tardó—, así que el `await` habría dejado la ventana igual de bloqueada con
        aspecto de arreglada. El trabajo fue a `Task.Run` y `MigrationYieldTests` fija la medición,
        de modo que fallará el día que el proveedor de SQLite gane E/S asíncrona de verdad. La forma
        se implementó como estaba decidida, vista de arranque en `Presentation/Shell/` incluida.
        Efecto medido contra la línea base: `open-with` de 1233/1214 ms a 779/773/776 —dispersión de
        6 ms, la señal limpia—, `repair` baja con ruido y `first-launch` varía 1245 ms entre
        ejecuciones del mismo código y por sí solo no habría dicho nada. **Lo que no arregló**: el
        rojo intermitente, que se le atribuía y quedó descartado con números. Evidencia en
        [audit-arq005-async-startup.md](../../evidence/stable/audit-arq005-async-startup.md) y
        [audit-arq005-startup-baseline.md](../../evidence/stable/audit-arq005-startup-baseline.md). \
        Second half: the migration yields at none of its awaits, so the work went to its own thread.
  - [x] **Lo que era, antes de medirlo**: `FinishShell` bloquea
        para migrar y, sólo si una migración reescribió el archivo, para comprobar integridad; los
        otros cuatro `GetAwaiter().GetResult()` de `CompositionRoot` son lecturas del informe de
        diagnóstico bajo demanda, y el de `Program.cs` es el `finally` de `Main` y es legítimo. **Sólo
        dos sitios llaman a `CreateShell()`**, los dos en recorridos ensamblados y los dos afirmando
        `Assert.IsType<ShellView>`, así que el coste de mostrar una vista de arranque y cambiarla al
        terminar está acotado. **Empieza por medir si `MigrateAsync` cede el hilo**: está escrita con
        `await`s de verdad, pero `Microsoft.Data.Sqlite` implementa buena parte de su superficie
        `Async` de forma síncrona, y cambiar `GetAwaiter().GetResult()` por `await` sobre algo que no
        cede deja la ventana igual de bloqueada con aspecto de arreglada. Si no cede, va a `Task.Run`;
        `SqliteConnectionFactory` abre una conexión por llamada y soporta el traslado. \ Second half:
        measure whether the migration actually yields before assuming await is enough.

        **Diseño decidido el 2026-08-10 (experto), para que no se re-delibere:**
        - **La forma**: `FinishShell` devuelve un `ContentControl` cuyo contenido inicial es la vista
          de arranque. `App` ya coloca ese control como `Content` de la ventana, así que **la ventana
          aparece en el primer fotograma sin tocar `App` ni `ConfigureWindow`**. El trabajo va detrás
          y sustituye el contenido por `ShellView` o por la de recuperación; **la decisión de cuál es
          la misma de hoy**, sólo cambia cuándo se toma.
        - **El fallo del trabajo va por `GuardedEvent`**, que ya existe: nunca mata el proceso y
          aterriza en `ISessionFailureLog`. No hace falta inventar nada para eso.
        - **La vista de arranque** vive en `Presentation/Shell/`, con el nombre del producto y una
          línea de estado. **Sin barra de progreso indeterminada**: no se sabe cuánto falta, y una
          barra que se mueve sin significar nada es una mentira visual. Cadenas en los dos idiomas y
          `AutomationProperties.Name`, como todo lo demás.
        - **Las dos pruebas ensambladas** pasan de afirmar el tipo devuelto a esperar el contenido
          final bombeando el despachador **con tope**, y su mensaje de fallo tiene que **nombrar lo
          que quedó en su lugar** — arranque o recuperación. Un tope que sólo dice «no llegó» no
          diagnostica nada.
        - **Antes de todo eso, una medición barata que da la línea base**: que la fase `first-launch`
          informe **el tiempo hasta la ventana**, no sólo un booleano. Convierte el intermitente en
          una serie comparable, y el mismo número antes y después de esta tarea es la prueba de que la
          arregló. \ Decided design: a ContentControl swapped when the work ends; measure the
          time-to-window first so the fix has a baseline.

## WP-7 — CI/CD y puertas

**Orden decidido el 2026-08-09 (experto)**: SEC-007 primero (una línea, riesgo cero), después
CI-004/SEC-006 (anclar por SHA + dependabot para `github-actions` — el mismo commit), después
CI-001/CI-002 (los dos scripts como pasos de `ci.yml`), y TST-001 al final (la puerta de cobertura
es la más cara de calibrar y la única que puede dar falsos rojos al principio).

- [x] **TST-001**: puerta de cobertura automatizada (reportgenerator + comparación por archivo nuevo
      vs `main` contra 96 % líneas/ramas; poblar `.config/dotnet-tools.json`). **Hecho 2026-08-09
      (noche)**: `eng/check-coverage.ps1` como paso bloqueante de `verify.ps1` (todo archivo nuevo
      contra `origin/main` debe llegar con 96/96; la novedad se compara entre árboles para que el
      checkout superficial de CI no la rompa; una base inalcanzable falla en voz alta),
      `reportgenerator` en `.config/dotnet-tools.json`, y `CoverageGateTests` fijando script,
      umbrales, invocación y herramienta. Calibrada contra `797c8cb`: cinco archivos nuevos de esa
      sesión, tres rojos verdaderos (ramas de error sin cubrir) nombrados como deuda visible en la
      evidencia — el umbral no se bajó. Evidencia en
      [TST1-coverage-gate.md](../../evidence/stable/TST1-coverage-gate.md).
- [x] **El guardián que le faltaba a TST-001, y dos de las tres deudas** (2026-08-10).
      `eng/check-coverage.ps1` lleva una lista explícita de vigilados que se miden **siempre**, cada
      uno con el suelo que su código cumple hoy, y con trinquete en los dos sentidos: por debajo
      falla, y por encima **también**, pidiendo subir el número. **Re-medir fue lo primero y desmintió
      la premisa**: los números no estaban caducados —dos eran idénticos un día después— y el tercero
      había **retrocedido** de 60,61/27,27 a 45,45/14,29, porque ARQ-004 se llevó por delante sus
      líneas cubiertas sin que ninguna puerta lo notara. Ésa es la demostración medida del hueco.
      `PlayerVersionsViewModel` y `CompositeFileIdentityProvider` quedaron al **100 %/100 %**; las dos
      fallaban por lo mismo, que las pruebas cubrían el cableado y no el contenido. Evidencia en
      [audit-tst1-coverage-debt.md](../../evidence/stable/audit-tst1-coverage-debt.md). \ The gate now
      holds a watchlist with a ratchet; two of the three debts are paid.
  - [x] **`ReconcileScannedFiles.cs`, la deuda que queda**: 86,73 % de líneas y 76,00 % de ramas,
        sobre 98 líneas y 50 ramas. Ya está vigilado con ese suelo, así que no puede empeorar en
        silencio. **Decidido el 2026-08-10 (experto), para que no se re-delibere:**
        - **Dónde**: `tests/ApSolutions.LocalMedia.Application.Tests/Discovery/ReconcileScannedFilesTests.cs`,
          **unitarias con dobles en memoria** de `IMediaFileRepository` e `IFileIdentityProvider`.
          `ReconcileScanResults`, `FileReconciliationPolicy` y `PendingReassignments` son clases
          concretas: se construyen de verdad, no se sustituyen.
        - **Por qué ahí y no en un recorrido**: el camino feliz ya lo cubre
          `ScanReconciliationTests` (IntegrationTests) y por eso las líneas están al 87 % y las
          ramas al 76 %. Lo que falta son **decisiones**, y una decisión se prueba más barata y más
          clara desde fuera que montando un escaneo que la provoque.
        - **Las ramas que faltan**, que son la lista de trabajo: escaneo cancelado; un resultado con
          `Outcome` fuera de Added/Updated/Unchanged; fila inexistente; identidad ya almacenada en
          una fila no `Updated`; identidad ilegible (cuenta como fallida y sigue); `Updated` que
          refresca la identidad porque el fingerprint viejo describe bytes que ya no están; un
          candidato **visto en el mismo escaneo**, que es una copia y no un movimiento; decisión no
          exacta o más de un candidato, que se retiene para la bandeja; la excepción que cuenta y
          continúa frente a la cancelación que se relanza; `KeepAsNewAsync` con y sin fila; y las
          cinco guardas del constructor. En `FindCandidatesAsync`: con identificador estable que
          apunta a uno mismo, sin fingerprint, y el filtrado de uno mismo entre los impresos.
        - **Al terminar, subir el suelo** en `eng/check-coverage.ps1` al número medido — la puerta
          falla si no se hace, que es el punto del trinquete. \ Decided: unit tests with in-memory
          doubles, aimed at the decisions rather than the happy path, then raise the floor.
        - **Hecho el 2026-08-10: 100 % de líneas y 100 % de ramas**, nueve pruebas, suelo subido a
          100/100. **Medir la lista antes de escribir la recortó en un tercio**: cinco de sus puntos
          —identidad ya almacenada, candidato visto en el mismo escaneo, decisión no exacta,
          `KeepAsNewAsync` con y sin fila, identificador estable que apunta a uno mismo— ya estaban
          cubiertos por los escaneos ensamblados. Y apareció uno que la lectura no da: la propiedad
          `AttemptedCount` no la leía ninguna prueba, porque comparar registros enteros va por
          campos. Evidencia en
          [audit-tst1-reconcile-coverage.md](../../evidence/stable/audit-tst1-reconcile-coverage.md).
          \ Done: 100%/100% with nine tests; measuring the list first cut it by a third.
- [x] **El rojo intermitente de `first-launch`, que sigue sin causa.** **Decidido el 2026-08-10
      (experto):** no se sube el plazo de 90 s, y tampoco se sale a buscarlo — es una carrera y no se
      reproduce aquí. Lo que se hace es **instrumentar el camino del fallo**, para que la próxima vez
      que ocurra deje diagnóstico en vez de un `exit code -1` mudo: cuando `Invoke-Application` agota
      el plazo de ventana, **antes de matar el proceso**, anotar en la fase si el proceso seguía
      vivo y volcar el estado de la carpeta de datos — si `library.db` existe y cuántas filas tiene
      `schema_history`. Eso separa las dos hipótesis que hoy no se pueden distinguir: **murió antes
      de migrar** o **migró y nunca pintó**.
      **Y un dato que cambia el análisis**: desde ARQ-005 la ventana aparece **antes** de migrar, así
      que si el plazo de ventana vuelve a agotarse, la migración ya no puede ser la causa y el fallo
      está por debajo — Avalonia, el arranque del runtime o el propio proceso. \ Decided: instrument
      the failure path instead of hunting the race; the window now precedes the migration, so a
      repeat rules the migration out by construction.
      **Hecho el 2026-08-10, y el registro archivado contestó media pregunta antes de escribir
      nada**: la línea de aquella ejecución decía `16 migration(s) applied to a new database`, así
      que «murió antes de migrar» estaba descartada desde el principio y las dos hipótesis nunca
      fueron dos. Lo que sigue sin recogerse es la otra mitad —si quedaba algo vivo que pintar—, y
      eso es lo que se instrumenta, junto con el procesador y los hilos, que separan girar de
      esperar. `LaunchDiagnosisTests` saca las funciones del guion publicado parseándolo y las
      ejerce contra procesos de estado conocido, incluida una base ilegible, porque la propiedad que
      hay que sostener es que el diagnóstico **no lance**. Evidencia en
      [audit-first-launch-instrumentation.md](../../evidence/stable/audit-first-launch-instrumentation.md).
      \ Done: the archived log already ruled out half the question; the other half is now recorded.
- [x] **Lo que se decidió el 2026-08-10 (experto), antes de ejecutarlo:**
      - **Los números del documento están caducados.** Una medición aproximada del 2026-08-10 (el
        máximo por informe, no la unión) los sitúa bastante mejor en líneas y todavía flojos en ramas,
        y `PlayerVersionsViewModel` **adelgazó** al perder su clase de comando en ARQ-004. El primer
        paso es **re-medir con `eng/check-coverage.ps1`**, no partir de los números viejos.
      - **Empezar por `PlayerVersionsViewModel`**, porque ARQ-004 acaba de tocarlo y es el momento
        natural: la regla de la casa es que la deuda se salda cuando se toca la zona.
      - **El hueco estructural, que es lo que de verdad importa**: la puerta mide **sólo archivos
        nuevos por contenido** contra `origin/main`, así que estos tres, por antiguos, **no los mira
        nadie**. Saldar la deuda sin cerrar eso no impide que vuelva mañana. Al saldarla,
        `check-coverage.ps1` recibe una **lista explícita de archivos vigilados** que se mide siempre,
        además de los nuevos, con la misma regla que la lista de huérfanos de `ServiceConsumptionTests`
        —que este repositorio ya aceptó—: **sólo puede encoger**. \ Decided: re-measure first, start
        with the file ARQ-004 already touched, and give the gate a watch-list, because it only ever
        looks at new files and these three are old.
- [x] **CI-001/CI-002**: `run-accessibility.ps1 -Mode Verify -Passes 2` y
      `run-recovery.ps1 -Mode Verify -Passes 2` como pasos de `ci.yml` (hoy solo manuales).
      **Hecho 2026-08-09 (tarde)**: ambos como pasos tras la verificación, con su evidencia subida
      como artefacto `audit-gates`; verificados en local en modo Verify con 2 pases antes de
      montarlos (0 hallazgos; matriz de recuperación 9/9 con salida explícita).
- [x] **CI-003**: rendimiento en CI como no-bloqueante primero (runners ruidosos; medir varianza).
      **Hecho**: `ci.yml` invoca `eng/verify.ps1 … -NonBlockingPerformance`, que corre los
      presupuestos, archiva su veredicto en `performance-nonblocking.json` y no deja que bloqueen.
      Siguen bloqueando donde significan algo, que es el arnés físico. Verificado el 2026-08-10
      leyendo el workflow, no supuesto. \ Done: CI passes the switch; the budgets still block on the
      physical harness.
- [x] **CI-004/SEC-006**: anclar las 9 acciones por SHA con comentario de versión + dependabot para
      `github-actions`. **Hecho 2026-08-09 (tarde)** en un commit: las nueve invocaciones
      (checkout v4.4.0, setup-dotnet v4.3.1, upload-artifact v4.6.2) ancladas por SHA de commit
      con la versión como comentario, y `.github/dependabot.yml` vigilando `github-actions`
      semanalmente para que los anclajes se muevan sólo por revisión.
- [x] **SEC-007**: `NuGetAuditLevel` de `high` a `moderate`. **Hecho 2026-08-09 (tarde)**: la
      solución entera restaura y compila limpia en el nivel nuevo, sin una sola supresión
      (`09c2daa`).
- [x] **CI-005 (nuevo, 2026-08-08 tarde)**: dos pruebas intermitentes sólo en runners, nunca en
      local — `SqliteBootstrapTests.Committed_WAL_transaction_survives_forced_child_process_termination`
      («disk I/O error» tras el kill del proceso hijo, 2 apariciones) y
      `UiThreadBudgetTests.Unchanged_scan_handoff_and_dispatch_stay_under_fifty_milliseconds`
      (77 ms frente a 50 en runner ruidoso, 1 aparición; pertenece a CI-003) y
      `SearchBudgetTests.Search_stays_in_budget_while_an_unchanged_scan_runs` (p95 246 ms,
      1 aparición; también CI-003). **Hecho 2026-08-09** según lo decidido: (1) CI-003 ejecutado —
      `verify.ps1 -NonBlockingPerformance` (lo que `ci.yml` pasa ahora) corre la suite entera sin
      PerformanceTests como bloqueante, corre PerformanceTests aparte, y archiva su veredicto en
      `performance-nonblocking.json` junto a los TRX; los presupuestos siguen bloqueando en el arnés
      físico (`run-performance.ps1` y todo `verify.ps1` sin el interruptor); (2) la reapertura
      posterior al kill de la prueba WAL reintenta acotado (3 intentos / 1 s) sólo alrededor del
      open — un fallo de aserción no se reintenta y un disco que sigue fallando al tercer intento
      falla la prueba con su propia excepción. El RED son las apariciones archivadas arriba.
      **Aparición 2026-08-09**:
      `SegmentCorpusTests.An_episode_materialises_from_nothing_with_the_expected_duration`
      (`IOException`: el corpus recién generado seguía abierto por otro proceso; run 31307701534,
      rama, relanzado con `gh run rerun --failed` y en verde). Primera vez de esta prueba en la
      familia. **Aparición 2026-08-09 (tarde)**:
      `FileWatcherRecoveryTests.Create_change_rename_delete_storm_is_coalesced_by_final_path`
      (`InternalBufferOverflowException`: «too many changes at once» — el búfer del
      `FileSystemWatcher` se desbordó en un runner compartido cargado; run 31319008700, main,
      relanzado con `gh run rerun --failed`). Primera vez de esta prueba en la familia; si
      reaparece, la corrección es agrandar `InternalBufferSize` del watcher del arnés o tratar el
      desbordamiento como el evento perdido que el planificador de respaldo ya recupera.

## WP-8 — Windows y experiencia de errores

- [x] **WIN-002**: `app.manifest` propio con `longPathAware` y `dpiAwareness=PerMonitorV2`.
      **Hecho 2026-08-09**, con `WindowsHostManifestTests` como guardia. Evidencia en
      [audit-wp8-windows.md](../../evidence/stable/audit-wp8-windows.md).
- [x] **WIN-004**: el renombrado con archivo bloqueado guardaba `IOException` y la UI callaba.
      **Hecho 2026-08-09**: clasificación por acción (`FileInUse`/`AccessDenied`), `FailureKey` en
      la superficie del renombrado con mensaje accionable ES/EN, y paseo con un handle real.
      Evidencia en [audit-wp8-windows.md](../../evidence/stable/audit-wp8-windows.md).
- [x] **WIN-003**: persistir posición/tamaño/estado de la ventana. **Hecho 2026-08-09**:
      `MainWindowPlacement` (aplica al arrancar, sigue la última geometría normal, escribe una vez
      al cerrar; descarta posiciones que ninguna pantalla muestra reutilizando `IsVisibleOn`).
      Evidencia en [audit-wp8-windows.md](../../evidence/stable/audit-wp8-windows.md).
- [x] **REL-A03**: entrada `HKCU\...\Run` huérfana tras desinstalar. **Decidido 2026-08-09
      (experto)**: documentar ahora — el manual explica que si activaste «iniciar con Windows» y
      desinstalas, la entrada `Run` queda huérfana e inocua y cómo quitarla; además, el arranque
      con Windows se re-escribe al iniciar la aplicación, así que reinstalar la repara sola.
      `StartupTask` MSIX queda ligado al futuro canal Store (misma suerte que REL-001); no se
      compra complejidad de instalador para un canal que hoy no existe. **Sección del manual ES/EN
      escrita el 2026-08-09.**
- [x] **REL-A08**: documentar que desinstalar el MSIX no borra `%LOCALAPPDATA%`. **Hecho
      2026-08-09** en el manual de usuario, ES/EN.
- [x] **BUG-009**: validar detecciones contra la duración y recortar en `Emit`. **Hecho
      2026-08-09**: `Emit` recorta al episodio medido y `MergeDetections` aplica la regla de las
      marcas manuales cuando conoce duraciones. Evidencia en
      [audit-wp8-windows.md](../../evidence/stable/audit-wp8-windows.md).
- [x] **BUG-011**: idioma coherente + preferencia. **Hecho 2026-08-09**: `StoredLanguageService`
      como única fuente (preferencia guardada o español, el declarado de siempre) que mueve juntos
      los recursos y la cultura del hilo que leen el actualizador y TMDB; selector en Apariencia.
      Evidencia en [audit-wp8-windows.md](../../evidence/stable/audit-wp8-windows.md).
- [x] **BUG-012**: re-verificar checksums de migraciones aplicadas; deduplicar el doble
      `integrity_check` del arranque. **Hecho 2026-08-09**: la historia se compara checksum a
      checksum y rehúsa antes de escribir; el segundo `integrity_check` sólo corre si una migración
      reescribió el archivo. Evidencia en
      [audit-wp8-windows.md](../../evidence/stable/audit-wp8-windows.md).
- [x] **ARQ-009**: diagnóstico con valores reales. **Hecho 2026-08-09**: la aceleración es la
      respuesta del motor para el último medio abierto, el recuento sale del resumen real de la
      biblioteca y los errores son los del registro de renombrados agrupados por código — sin rutas
      ni nombres, con las cubetas de siempre. **WP-8 completo.** Evidencia en
      [audit-wp8-windows.md](../../evidence/stable/audit-wp8-windows.md).

## WP-9 — Ecosistema y repo público

**Orden decidido el 2026-08-09 (experto)**: todo lo no destructivo puede ejecutarse ya y en este
orden — SECURITY.md primero (ahora existe una clave de firma que documentar: qué cubre, cómo
reportar, cómo se rota), CLAUDE.md raíz, CONTRIBUTING.md, plantillas + CODEOWNERS + dependabot
(NuGet y github-actions juntos si CI-004 aterrizó antes), y después DOC-101/DOC-201/T44.x. El
borrado de logs de `.superpowers/` sigue siendo del propietario y no bloquea nada de lo anterior.

- [x] CLAUDE.md raíz (reglas de arranque/ejecución que hoy viven fuera del repo). **Hecho 2026-08-10**
      con WP-9; comprobado en el árbol el 2026-08-10 (tarde), no dado por hecho.
- [x] CONTRIBUTING.md y SECURITY.md; plantillas de issue/PR; CODEOWNERS; dependabot.
      **Completado 2026-08-10** con WP-9: `CONTRIBUTING.md`, `.github/CODEOWNERS`,
      `.github/ISSUE_TEMPLATE`, `.github/pull_request_template.md` y `.github/dependabot.yml`
      cubriendo NuGet y `github-actions`. Las casillas se cerraron el 2026-08-10 (tarde) tras
      comprobar cada archivo: el plan llevaba una sesión diciendo que faltaba lo que ya existía, que
      es el mismo defecto que este repositorio caza en el código.
      **SECURITY.md hecho 2026-08-09 (tarde)**, bilingüe en la raíz: canal de reporte privado
      (aviso de seguridad de GitHub, nunca un issue público), versiones con soporte, y la clave de
      firma documentada — qué cubre (minisign sobre `SHA256SUMS.txt`, verificada por el
      actualizador contra la pública embebida), qué no cubre (Authenticode/SmartScreen), dónde
      vive la privada y cómo se rota publicando. **Dependabot para `github-actions` aterrizó con
      CI-004** (sus tres primeros PRs de bump quedaron abiertos para revisión del propietario);
      falta el ecosistema NuGet. Queda: CLAUDE.md raíz, CONTRIBUTING.md, plantillas, CODEOWNERS.
- [ ] **Borrar los logs de `.superpowers/brainstorm/*/state/`** (rutas de la máquina anterior;
      gitignorados pero higiene). **Acción destructiva: la ejecuta o la aprueba el propietario.**
- [ ] DOC-101 (evidencia de UX-007), DOC-201 (justificar la promoción de SYS-001), casillas
      T44.1-T44.6 del plan MVP.
- [ ] Manual de usuario: documentar actualizador y detección de segmentos.
- [ ] ARQ-012 (RepositoryLayout/TestAppBuilder duplicados, dos anclas), ARQ-013 (regex de la puerta
      de alcanzabilidad acepta comentarios), ARQ-014 (User-Agent con marca y versión desincronizada).
- [ ] **BUG-010**: unificar `LibVlcMediaProbe` sobre `LibVlcFactory.DeferRelease` (su cola propia
      puede morir para siempre y mantiene una segunda instancia nativa fuera del contador).
- [ ] QA-001: barrido de `Parse/ToString` sin cultura en `src/`.

## Pendiente transversal / Cross-cutting pending

- [ ] **Paseo físico** del artefacto ensamblado tras WP-2. **Mitad del arnés hecha 2026-08-09**:
      paquete sellado y `AssembledPhysicalWalkTests` recorriendo la aplicación de
      `CompositionRoot` con disco, SQLite y decodificación reales — vigilancia catalogando un
      archivo soltado y agrupando dos copias, teclas pausando/reanudando un vídeo en reproducción,
      marca a mitad de sesión ofreciendo el salto sin reabrir, y dos episodios encadenándose. El
      RED de la escena de teclas destapó que nada alimentaba
      `PlayerViewModel.ApplySessionState` (motor pausado, pantalla en «reproduciendo» para
      siempre); corregido reenviando cada transición del motor al modelo por el dispatcher.
      Evidencia y guion de diez minutos para la mitad manual (vídeo real en pantalla, TMDB real,
      teclas multimedia físicas) en
      [audit-physical-walk.md](../../evidence/stable/audit-physical-walk.md). **Pendiente: la
      mitad manual, del propietario.**
- [x] **Paquete del Surface Pro 7 — resuelto 2026-08-09 (experto)**: el Surface Pro 7 es x64, así
      que el ZIP portable `win-x64` que cada verificación ya construye **es** su paquete; no existe
      ningún artefacto nuevo que producir. Instalar allí es descomprimir y ejecutar, como documenta
      el manual.
- [ ] Decisiones que siguen siendo del propietario (dinero, identidad o hardware): cuenta de Store
      (REL-001), certificado Authenticode o Store (WP-5 mitad económica), retirar la copia antigua
      (sus vídeos personales), máquina ARM64 física (PRD-003), comprobación jurídica (REL-004), y
      el borrado de los logs de `.superpowers/` antes de publicar (destructivo: lo ejecuta o lo
      aprueba en el momento).

## Hecho y verificado — segunda ola / Done and verified — second wave (2026-08-08 tarde–2026-08-09)

Con `main` en fast-forward y CI vigilada para cada commit (dos flakes de runner relanzados y en
verde; ver CI-005):

- **WP-2 completo**: puerta registrado→consumido (`0fcd010`), identificación (`fb16f1a`),
  vigilancia (`125f84f`), siguiente episodio (`127ca71`), atajos y teclas (`d0e4c5e`), duplicados
  (`83f7967`), marcas en vivo (`86934cb`), un dueño para el coordinador de ventanas (`c159809`).
  Evidencia en [audit-wp2-assembly.md](../../evidence/stable/audit-wp2-assembly.md); manifiesto en
  **43 verificados / 1 fuera de alcance / 2 bloqueados** (PRD-003, REL-001).
- **WP-4** SEC-004 + SEC-005 (`1f25a44`); SEC-003 sigue con la firma. Evidencia en
  [audit-wp4-updater.md](../../evidence/stable/audit-wp4-updater.md).
- **WP-8 primera ola**: WIN-002, BUG-009, BUG-012, REL-A08 (`615aa6d`). Evidencia en
  [audit-wp8-windows.md](../../evidence/stable/audit-wp8-windows.md).

## Hecho y verificado / Done and verified (2026-08-08)

Con CI en verde en `main` y `codex/ap-reelume-mvp-x64` para cada commit:

- **WP-3** privacidad documental + candado registro↔documento (`07e11f8`).
- **Re-sincerado**: diez filas a `IMPLEMENTED` con bloqueadores en el manifiesto (`a967bf4`).
- **CI reparada**: búsqueda de makeappx escalar-vs-array en tres scripts (`12ae97a`, `b8eef7e`),
  ffmpeg reinstalado en los workflows — la matriz de códecs, el corpus y la asociación de archivos
  se miden en CI por primera vez (`b7b3cec`) —, dos pruebas de progreso de copia inestables por
  construcción corregidas y cinco supuestos de máquina convertidos en skips declarados (`f9cfea3`).
- **WP-1 completo** (evidencia en [audit-wp1-playback.md](../../evidence/stable/audit-wp1-playback.md)):
  reanudación real (`9f31cd7`), orden de liberación nativa del extractor + `DeferRelease` (`c7bc2ad`),
  bucle de guardado de 5 s + flush en pausa/seek + desuscripción por sesión (`fb2a172`), JSON
  ilegible → `Unreachable` + cero `Post(async` (`91aa494`), detección cancelable y parada al salir
  (`71af80d`). **`PLY-008` de vuelta a `VERIFIED`**; manifiesto en 36 verificados / 1 fuera de
  alcance / 9 bloqueados.
