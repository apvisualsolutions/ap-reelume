# WP-2 — Cableado del ensamblado / Assembly wiring

Evidencia del paquete WP-2 de la auditoría profunda del 2026-08-08. Cada bloque lleva su RED
archivado, la corrección mínima y el GREEN con sus puertas. / Evidence for the WP-2 package from the
deep audit of 2026-08-08. Each block carries its archived RED, the minimal fix, and the GREEN with
its gates.

## La puerta registrado→consumido / The registered→consumed gate

**El defecto de la casa / The house defect.** La auditoría encontró una familia de un solo defecto
con diez caras: componentes construidos, registrados y probados que la aplicación ensamblada nunca
invoca. Cada cara se cazó a mano; faltaba la guardia que impida la siguiente. / The audit found one
defect wearing ten faces: components built, registered, and tested that the assembled application
never invokes. Each face was hunted by hand; what was missing is the guard against the next one.

**La puerta / The gate.** `ServiceConsumptionTests` (ArchitectureTests) exige que cada servicio
registrado en `CompositionRoot` tenga al menos una resolución fuera de su propio registro. El
análisis es textual —el estilo aceptado hasta que WP-6 convierta las aserciones a
`IServiceCollection`— y el consumo es transitivo desde las resoluciones que la aplicación ejecuta:
`GetRequiredService` fuera de la cadena de registros es raíz, uno dentro de una fábrica es una
arista que sólo cuenta si su dueño se consume, y los parámetros de constructor de cada
implementación registrada son las aristas que el contenedor cablea sin texto visible. Un `new` junto
al registro no es una resolución: la doble propiedad de ARQ-008 sigue visible. / requires every
service registered in `CompositionRoot` to be resolved at least once outside its own registration.
The analysis is textual — the accepted style until WP-6 — and consumption is transitive from the
resolutions the application actually performs; a `new` next to a registration is not a resolution.

**RED (archivado / archived).** Con la lista de deudas vacía, la puerta enumeró **32 servicios
registrados que nada resuelve** — la estimación de la auditoría era ~12; la puerta encontró las
caras que el barrido manual no llegó a nombrar / with the debt list empty, the gate enumerated
**32 registered services nothing resolves** — the audit estimated ~12; the gate found the faces the
manual sweep never named:

> ApplyPlaybackPreferences, ArtworkCache, ConfigureWatchedThreshold, FileReconciliationPolicy,
> GetNextEpisode, ICandidateScorer, ICatalogRepository, IFallbackScanScheduler,
> IIdentificationCandidateSource, IMediaKeySource, IMediaNameParser, IMetadataCache,
> IMetadataProvider, IMigrationRunner, IRootWatcher, IdentifyMediaFile, LibVlcAudioOutputAdapter,
> ManualReassignmentViewModel, MarkerEditorViewModel, PlayerViewModel, PlayerWindowCoordinator,
> ReconcileScanResults, RemoveLibraryRoot, RootWatchCoordinator, SetWatchStatus,
> SkipMarkerViewModel, StartPlayback, StopPlayback, SwitchMediaVersion, TmdbOptions,
> TmdbRateLimiter, WatchStatusViewModel

**Caras nuevas que la puerta destapó / New faces the gate uncovered.** Registradas el mismo día en
el plan de remediación con identificador propio / recorded the same day in the remediation plan
under their own identifiers:

- **AUD-A01** — `LibVlcAudioOutputAdapter`: elegir un dispositivo de salida nunca llega al motor. /
  choosing an output device never reaches the engine.
- **PLY-A01** — `ApplyPlaybackPreferences`: las preferencias de reproducción guardadas nunca se
  aplican a una sesión. / stored playback preferences are never applied to a session.
- **CNT-A01** — `SetWatchStatus` y `ConfigureWatchedThreshold`: el conmutador de «visto» se
  construye con manejador nulo y nada configura el umbral. / the watched toggle is built with a null
  handler and nothing configures the threshold.
- **LIB-A01** — `RemoveLibraryRoot`: ninguna superficie permite retirar una carpeta de la
  biblioteca. / no surface can remove a library root.
- **ARQ-A01** — registros muertos duplicados por un `new` manual (`PlayerViewModel`,
  `SkipMarkerViewModel`, `MarkerEditorViewModel`, `WatchStatusViewModel`, `StartPlayback`,
  `StopPlayback`, `IMigrationRunner`): retirar o cablear, nunca dejar en silencio. / dead
  registrations duplicated by a manual `new`: remove or wire, never leave silent.

**La corrección mínima / The minimal fix.** La puerta queda en verde con la deuda **nombrada**, no
perdonada: cada huérfano vive en `PendingWiring` con su identificador de auditoría, y una segunda
aserción (`The_pending_wiring_list_names_only_services_still_unconsumed`) expulsa la entrada en
cuanto su cableado aterriza — la lista sólo puede encoger diciendo la verdad. Una tercera aserción
fija el suelo del analizador (más de 100 registros, más de 20 raíces) para que un refactor que lo
ciegue no ponga la puerta en verde por silencio. / The gate goes green with the debt **named**, not
forgiven: each orphan lives in `PendingWiring` under its audit identifier, and a second assertion
evicts the entry the moment its wiring lands — the list can only shrink truthfully. A third
assertion floors the parser so a refactor that blinds it cannot turn the gate green by silence.

**GREEN.** ArchitectureTests 14/14 (las tres aserciones nuevas incluidas), `dotnet format` limpio,
compilación `-warnaserror` 0/0, `verify-docs` en verde. / ArchitectureTests 14/14 (the three new
assertions included), clean format, `-warnaserror` 0/0, `verify-docs` green.

## LIB-006/007 — La identificación por fin tiene quien la llame / Identification finally has a caller

**El defecto / The defect.** `IdentifyMediaFile` estaba registrado, probado y completo — analiza el
nombre, puntúa candidatos locales, consulta el proveedor sólo si hace falta y persiste el
resultado — y ningún camino de la aplicación lo invocaba. El escaneo catalogaba archivos, la bandeja
de revisión se abría, y entre los dos no había nada: una pantalla que cualquiera podía abrir y a la
que nada podía llegar jamás. / `IdentifyMediaFile` was registered, tested, and complete — and no
application path invoked it. The scan catalogued files, the review inbox opened, and between the two
there was nothing: a screen anybody could open that nothing could ever arrive in.

**RED (archivado / archived).** `IdentificationWiringTests`, dos aserciones contra el ensamblado,
ambas en rojo a la primera ejecución / two assembly assertions, both red on their first run:
`The_scan_hands_what_it_found_to_identification` (ningún `GetRequiredService<IdentifyScannedFiles>`)
y `Identification_runs_on_the_summary_the_scan_produced`.

**La corrección / The fix.**

- `IdentifyScannedFiles` (Application): recorre el resumen del escaneo, localiza cada archivo,
  **deja en paz los que ya tienen candidatos** (una decisión aceptada sobrevive a todos los escaneos
  posteriores), reintenta los que nadie identificó nunca (los `Unchanged` de bibliotecas anteriores
  a este cableado sanan en su siguiente escaneo), y cuenta y salta los que fallan — un nombre
  ilegible no cuesta la identificación de los otros novecientos noventa y nueve. Sin el token del
  proveedor la cadena queda local por construcción. / walks the scan summary, finds each file,
  **leaves alone those that already have candidates**, heals never-identified `Unchanged` files on
  their next scan, and counts-and-skips the ones that fail. Without the provider token the chain
  stays local by construction.
- `CompositionRoot.ScanRootAsync`: el resumen del escaneo se entrega a `IdentifyScannedFiles`; un
  escaneo cancelado se deja en paz. / the scan summary is handed to `IdentifyScannedFiles`; a
  cancelled scan is left alone.

**GREEN.**

- `IdentificationWiringTests` 2/2; `IdentifyScannedFilesTests` 6/6 (decididos intactos, `Unchanged`
  sanados, un fallo no detiene el resto, las carpetas llegan al parser, cancelado y raíz
  desconocida en paz).
- `ScanIdentificationTests` — **el paseo de punta a punta con SQLite real**: dos archivos reales
  escaneados por el `ScanCoordinator` real, identificados por la cadena real (parser, scorer,
  `MetadataCandidateSource`, repositorio SQLite; el proveedor es el único doble), «Dune» se resuelve
  **automático** y la ambigua aterriza en la bandeja real de `GetReviewInbox`; repetir la
  identificación no reemplaza nada. / **the end-to-end walk on real SQLite**: two real files
  scanned by the real coordinator, identified by the real chain (the provider is the only stub),
  "Dune" resolves **automatic**, the ambiguous one lands in the real inbox, and running again
  replaces nothing.
- Suites completas / full suites: Application.Tests 180/180, UiTests 327/327, IntegrationTests
  354/354 (+1 skip declarado), ArchitectureTests 14/14 — la puerta expulsó las ocho entradas de la
  cadena de identificación de `PendingWiring` (queda `ArtworkCache` como ART-A01, cara propia). /
  the gate evicted the identification chain's eight entries from `PendingWiring` (`ArtworkCache`
  stays as ART-A01, its own face).
- `dotnet format` limpio; `-warnaserror` 0/0; `verify-docs` en verde.

**Estado / Status.** `LIB-006` y `LIB-007` vuelven a `VERIFIED` con esta evidencia; bloqueadores
retirados del generador; manifiesto en **38 verificados / 1 fuera de alcance / 7 bloqueados**. /
`LIB-006` and `LIB-007` return to `VERIFIED` with this evidence; blockers removed from the
generator; manifest at **38 verified / 1 out of scope / 7 blocked**.

**Límite declarado / Declared limit.** El paseo físico del artefacto ensamblado (escanear una
carpeta real con TMDB de verdad y ver la bandeja llenarse) queda para la verificación física tras
WP-2, como exige la regla de la casa. / The physical walk of the assembled artifact awaits the
post-WP-2 physical verification, as the house rule requires.

## LIB-002/003 — La vigilancia arranca con la aplicación / Watching starts with the application

**El defecto / The defect.** `RootWatchCoordinator`, `DebouncedFileWatcher` y el planificador de
respaldo: registrados, probados y nunca resueltos. La aplicación sólo escaneaba al pulsar un botón;
la vigilancia continua, el escaneo de arranque por política y la recuperación de eventos perdidos
existían únicamente en las pruebas de componente. / registered, tested, and never resolved. The
application only scanned when a button was pressed; continuous watching, the policy-driven startup
scan, and missed-event recovery existed only in component tests.

**RED (archivado / archived).** `RootWatchWiringTests`, cinco aserciones contra el ensamblado, las
cinco en rojo a la primera ejecución / five assembly assertions, all five red on their first run:
nadie arrancaba `RootWatchBackground`, nadie lo paraba al salir, ningún escaneo manual aseguraba la
vigilancia de su raíz, la identificación sólo viajaba con el escaneo manual, y el planificador de
respaldo no tenía intervalo — `Continuous` significaba «nunca» en silencio.

**La corrección / The fix.**

- `RootWatchBackground` (Application): posee la vida de los vigilantes — `Start()` entrega cada
  raíz conocida al coordinador fuera del hilo de quien llama, `EnsureWatching(rootId)` incorpora
  una raíz recién escaneada sin esperar al siguiente arranque, y `Stop()` cancela todo al salir.
  Una raíz se vigila una sola vez y un vigilante que muere cuesta la vigilancia, nunca la
  aplicación. / owns the watchers' lives — start with the window, join on first scan, stop on the
  way out; a root is watched once, and a dying watcher costs the watching, never the application.
- `RootWatchCoordinator`: el watcher en vivo sólo corre para raíces con `Continuous` — una raíz
  `Manual` no se sigue a espaldas de su dueño; el planificador de respaldo lee la política por sí
  mismo, como siempre hizo. / the live watcher only runs for `Continuous` roots — a `Manual` root
  is not followed behind its owner's back.
- `IdentifyingScanCoordinator` (Application): el `IScanCoordinator` que toda la aplicación resuelve
  entrega cada resumen a la identificación antes de devolverlo — un escaneo del vigilante alimenta
  la bandeja exactamente igual que uno manual, en vez de ser la identificación una cortesía del
  llamante que se acordó. / the `IScanCoordinator` the whole application resolves hands every
  summary to identification before returning it.
- `FallbackScanScheduler.DefaultRecoveryInterval` (15 min) llega al registro: `Continuous` vuelve a
  significar lo que promete para USB y NAS. / reaches the registration: `Continuous` means what it
  promises again for USB and NAS roots.
- `ConfigureWindow` arranca el fondo con la ventana y `exitApplication` lo para junto a la
  detección; `ScanRootAsync` asegura la vigilancia de la raíz recién escaneada. / the window starts
  the background, exit stops it, and a hand-scanned root is watched from then on.

**GREEN.**

- `RootWatchWiringTests` 5/5; `RootWatchBackgroundTests` 3/3 (todas las raíces entregadas, una sola
  vigilancia por raíz, parar cancela y rehúsa); `WatchCoordinatorTests` 6/6 con la prueba nueva
  (una raíz `Manual` no se vigila); `IdentifyingScanCoordinatorTests` 1/1 (el resumen llega a la
  identificación y vuelve al llamante).
- `ContinuousWatchTests` — **el paseo de punta a punta con disco y SQLite reales**: el anfitrión
  arranca, el pase de arranque cataloga lo que ya estaba, y un archivo soltado después aparece en
  el catálogo sin que nadie pida nada (vigilante real con debounce de 150 ms). / **the end-to-end
  walk on a real disk and real SQLite**: the host starts, the startup pass catalogues what was
  there, and a file dropped afterwards appears in the catalogue with nobody asking.
- Suites completas / full suites: Application.Tests 185/185, UiTests 332/332, IntegrationTests
  355/355 (+1 skip declarado), ArchitectureTests 14/14 — la puerta expulsó `RootWatchCoordinator`,
  `IRootWatcher` y `IFallbackScanScheduler`; la mitad de archivos movidos (`ReconcileScanResults`,
  `ManualReassignmentViewModel`, `FileReconciliationPolicy`, `ICatalogRepository`) sigue en la
  lista con su razón. / the gate evicted the watching half; the moved-file half stays listed with
  its reason.
- `dotnet format` limpio; `-warnaserror` 0/0; `verify-docs` en verde.

**Estado / Status.** `LIB-002` y `LIB-003` vuelven a `VERIFIED` con esta evidencia; bloqueadores
retirados del generador; manifiesto en **40 verificados / 1 fuera de alcance / 5 bloqueados**. /
`LIB-002` and `LIB-003` return to `VERIFIED`; manifest at **40 verified / 1 out of scope / 5
blocked**.

**Límites declarados / Declared limits.** El paseo físico tras WP-2 sigue pendiente, y la mitad de
archivos movidos de LIB-002/003 (reconciliación → reasignación manual) sigue sin superficie que la
alcance — está contada en la puerta y en el plan. / The physical walk stays pending, and the
moved-file half still has no surface that reaches it — counted in the gate and the plan.

## PLY-011 — El final de un episodio por fin ofrece el siguiente / The end of an episode finally offers the next

**El defecto / The defect.** Tres eslabones sueltos: el motor no observaba `EndReached` — el estado
se quedaba en `Playing` para siempre y ningún código podía saber que un episodio terminó solo —,
`StartNextEpisodeCountdown` (T28: cancelable, configurable, revalidando el archivo en cero) no
estaba ni registrado, y `NextEpisodeViewModel` se resolvía sin manejador: sus dos botones sólo
escondían el cartel. / Three loose links: the engine never observed `EndReached` — the state stayed
at `Playing` forever —, `StartNextEpisodeCountdown` was not even registered, and
`NextEpisodeViewModel` was resolved with no handler: its two buttons only hid the card.

**RED (archivado / archived).** `NextEpisodeWiringTests`, cuatro aserciones contra el ensamblado,
las cuatro en rojo a la primera ejecución / four assembly assertions, all four red on their first
run: sin `EndReached`, sin estado `Ended`, sin oferta al terminar, y el overlay resuelto a pelo.

**La corrección / The fix.**

- `PlaybackState.Ended` — distinto de `Stopped` a propósito: parar es una decisión de alguien,
  terminar es el momento en que puede ofrecerse el siguiente. El motor traduce `EndReached` a esa
  transición (el evento llega en el hilo de LibVLC, que jamás debe reentrar al player: la
  transición sólo cambia el estado y quien escucha postea a otro hilo). / `Ended`, distinct from
  `Stopped` on purpose; the engine translates `EndReached` into that transition without touching
  the player from LibVLC's thread.
- `CompositionRoot.OfferNextEpisodeAsync`: al terminar un episodio, la oferta corre por el
  dispatcher — etiqueta y segundos al overlay (`Offer`), cada tick por el hilo de interfaz, y la
  cuenta atrás es exactamente el caso de uso de T28. «Reproducir ya» cancela la espera y abre;
  «Cancelar» sólo cancela; sin siguiente episodio o con el archivo desaparecido, el shell vuelve a
  la ficha. El episodio elegido se abre por el shell, así que la sesión nueva recibe rastreador,
  marcas, pistas y reanudación como cualquier otra. / the offer runs on the dispatcher, the
  countdown is T28's use case exactly, both buttons act, and the chosen episode opens through the
  shell so the new session gets its tracker, markers, tracks, and resume like any other.

**GREEN.**

- `NextEpisodeWiringTests` 4/4;
  `LibVlcSmokeTests.Playing_to_the_end_of_the_media_reports_the_ended_state` — **decodificación
  real**: una muestra H.264 abierta cerca del final reproduce hasta terminar y el motor informa
  `Ended` (13 s). / **real decoding**: an H.264 sample opened near its end plays out and the engine
  reports `Ended`.
- Suites completas / full suites: MediaTests 107/107, UiTests 336/336, Application.Tests 185/185,
  ArchitectureTests 14/14 — `GetNextEpisode` fuera de `PendingWiring`.
- `dotnet format` limpio; `-warnaserror` 0/0; `verify-docs` en verde.

**Estado / Status.** `PLY-011` vuelve a `VERIFIED`; manifiesto en **41 verificados / 1 fuera de
alcance / 4 bloqueados**. / `PLY-011` returns to `VERIFIED`; manifest at **41 / 1 / 4**.

**Límites declarados / Declared limits.** Cuando la cuenta atrás llega a cero, el caso de uso abre
por el coordinador y el shell reconstruye la sesión sobre el mismo archivo — una reapertura breve;
el encadenado físico de dos episodios reales pertenece al paseo tras WP-2. / When the countdown
reaches zero the use case opens through the coordinator and the shell rebuilds the session over the
same file — one brief reopen; the physical two-episode chain belongs to the post-WP-2 walk.

## PLY-014 / ARQ-002 — Los atajos y las teclas por fin son una cadena / Shortcuts and media keys finally are one chain

**El defecto / The defect.** Cada eslabón existía y ninguno tocaba otro: `IMediaKeySource` estaba
registrado y su `StartAsync` no se llamaba nunca, `InputCommandRouter` — el que impide que una
tecla actúe dos veces — no se instanciaba, el reproductor no tenía ni un manejo de teclado, y el
editor de Ajustes aceptaba un mapa opcional con `?? new`: podía editar un segundo mapa que ninguna
tecla leería jamás. / Every link existed and none touched another: the source never started, the
router never instantiated, the player had no key handling at all, and the settings editor could
edit a second map no key press would ever read.

**RED (archivado / archived).** `ShortcutWiringTests`, seis aserciones contra el ensamblado, las
seis en rojo a la primera ejecución / six assembly assertions, all six red on their first run.

**La corrección / The fix.**

- **Un enrutador por sesión** en `OpenPlayerAsync`: teclado y teclas multimedia resuelven en la
  misma acción exactamente una vez (la ventana de coalescencia del router, ya probada, es lo que
  impide que una tecla multimedia que la ventana también ve como pulsación conmute dos veces). /
  one router per session: keyboard and media keys resolve into the same action exactly once.
- **El reproductor responde al teclado**: `PlayerView` toma foco al montarse y resuelve cada gesto
  en el `ShortcutMap` compartido vía `PlayerViewModel.GestureHandler`; un gesto sin comando se deja
  pasar. / the player takes focus and resolves each gesture in the shared map; an unbound key is
  left for whoever wanted it.
- **Las teclas multimedia escuchan sólo mientras hay sesión** — la regla del propio servicio:
  `StartAsync` tras el teardown de la sesión anterior, `CommandReceived` marshalizado al dispatcher
  (llega del hilo STA), y el teardown de sesión desuscribe, para el servicio y libera el router. /
  the hardware keys listen only while a session exists, marshalled off the STA pump, and the
  session teardown unsubscribes, stops, and disposes.
- **El ejecutor**: reproducir/pausar/detener van por los mismos comandos que los botones en
  pantalla (con sus guardas), saltos y silencio por `ControlPlayback` (con su persistencia de
  búsqueda), volumen en pasos de 5 por el transporte, y los modos de ventana por el shell que los
  posee. / play, pause, and stop go through the on-screen buttons' commands, skips and mute through
  `ControlPlayback`, volume through the transport, window modes through the shell.
- **El editor exige el mapa registrado**: `ShortcutSettingsViewModel(ShortcutMap map)` sin `?? new`
  (ARQ-002). / the editor demands the registered map.

**GREEN.**

- `ShortcutWiringTests` 6/6; `ShortcutChainTests` 2/2 — **las teclas esenciales operando la sesión
  en Avalonia headless**: espacio, flecha, M y F llegan del `KeyDown` real de la vista al router y
  ejecutan reproducir/pausar, avanzar, silencio y pantalla completa; una tecla sin comando se queda
  sin capturar. / **the essential keys operating the session in headless Avalonia**: Space, Arrow,
  M, and F travel from the view's real `KeyDown` through the router; an unbound key stays free.
- Suites completas / full suites: UiTests 344/344, AccessibilityTests 54/54 (editor con mapa
  exigido), ArchitectureTests 14/14 — `IMediaKeySource` fuera de `PendingWiring`.
- `dotnet format` limpio; `-warnaserror` 0/0; `verify-docs` en verde.

**Estado / Status.** `PLY-014` vuelve a `VERIFIED`; manifiesto en **42 verificados / 1 fuera de
alcance / 3 bloqueados**. / `PLY-014` returns to `VERIFIED`; manifest at **42 / 1 / 3**.

**Límite declarado / Declared limit.** Las teclas multimedia físicas (hardware real, registro
global de Windows) pertenecen al paseo físico tras WP-2; el servicio real ya demostró sus registros
en T24 y AccessibilityTests. / Physical hardware media keys belong to the post-WP-2 walk; the real
service already proved its registrations in T24 and AccessibilityTests.

## LIB-008 — Los duplicados por fin se agrupan solos / Duplicates finally group on their own

**El defecto / The defect.** `GroupMediaVersions` tenía repositorio, política y pruebas (T15), y
nada en la aplicación lo invocaba: los grupos de versiones sólo existían si un test los guardaba.
Además, un grupo se almacena bajo una sola clave de título, y las copias sin identificar llevan
cada una la suya: el grupo creado desde una tarjeta era invisible desde las demás. / had a
repository, a policy, and tests, and nothing invoked it: groups only existed if a test stored them.
And a group lives under one title key while unidentified copies each carry their own, so a group
was invisible from the other copies' cards.

**RED (archivado / archived).** `DuplicateWiringTests`, dos aserciones contra el ensamblado, ambas
en rojo a la primera ejecución / two assembly assertions, both red on their first run: nada entrega
el escaneo a la agrupación y no existe búsqueda por miembro.

**La corrección / The fix.**

- `GroupScannedVersions` (Application): recorre el resumen del escaneo, analiza cada nombre con el
  parser real, y donde la política de T15 dice «mismo contenido» invoca `GroupMediaVersions` — que
  funde con el grupo existente y conserva la versión preferida. Una diferencia material de duración
  se queda esperando a una persona (`ConfirmationRequired`); nada se borra ni se oculta. La clave
  del grupo reutiliza la de un grupo existente o la del identificador más bajo del conjunto, así el
  grupo es estable ante reescaneos. / walks the scan summary, parses each name, and where T15's
  policy says "same content" invokes `GroupMediaVersions`; material duration differences wait for a
  person, nothing is deleted or hidden, and the group key stays stable across rescans.
- El pipeline compartido (`IdentifyingScanCoordinator`) entrega cada resumen a identificación **y**
  a agrupación: un escaneo del vigilante forma grupos igual que uno manual. / the shared pipeline
  hands every summary to identification **and** grouping.
- `IMediaVersionGroupRepository.FindByMemberAsync` + repliegue en las dos superficies
  (`OpenDuplicatesAsync`, ficha de película): el grupo se alcanza desde **cualquier** copia. / a
  member lookup plus fallbacks in both surfaces: the group is reachable from **any** copy.

**GREEN.**

- `DuplicateWiringTests` 2/2;
  `ScanVersionGroupingTests.Two_scanned_copies_form_one_group_on_their_own_and_a_preference_survives_a_rescan`
  — **de punta a punta con SQLite real**: dos copias reales de la misma película escaneadas forman
  un grupo sin intervención, alcanzable desde ambas; una preferencia fijada sobrevive al reescaneo;
  el inventario del disco queda intacto. / **end to end on real SQLite**: two real copies form one
  group on their own, reachable from both; a pinned preference survives the rescan; the disk
  inventory is untouched.
- Suites completas / full suites: Application.Tests 185/185, UiTests 346/346, IntegrationTests
  356/356 (+1 skip declarado), ArchitectureTests 14/14.
- `dotnet format` limpio; `-warnaserror` 0/0; `verify-docs` en verde.

**Estado / Status.** `LIB-008` vuelve a `VERIFIED`; manifiesto en **43 verificados / 1 fuera de
alcance / 2 bloqueados**. / `LIB-008` returns to `VERIFIED`; manifest at **43 / 1 / 2**.

**Límite declarado / Declared limit.** Mover una **sesión en reproducción** a otra versión sigue
sin superficie de origen (el diálogo de confirmación existe y nadie lo alimenta); queda contado en
la puerta como VSW-A01 y en el plan. / Moving a **playing session** to another version still has no
origin surface; counted in the gate as VSW-A01 and in the plan.

## BUG-008 — Las marcas siguen a la sesión en vivo / Markers follow the live session

**El defecto / The defect.** Las marcas de la sesión eran una instantánea tomada al abrir: guardar,
borrar, aceptar o corregir una marca cambiaba los almacenes y nada recomponía lo que el botón de
saltar sigue — una marca hecha durante la reproducción sólo funcionaba tras cerrar y reabrir el
episodio. / The session's markers were a snapshot taken at open: saving, deleting, accepting, or
correcting changed the stores and nothing recomposed what the skip button follows.

**RED (archivado / archived).** `LiveMarkerWiringTests`, dos aserciones contra el ensamblado, ambas
en rojo a la primera ejecución / two assembly assertions, both red on their first run: no existe
recomposición y ninguna mutación la invoca.

**La corrección / The fix.** `RefreshSessionMarkersAsync` en la sesión: relee las marcas manuales de
la serie y las detectadas del archivo, recompone con la regla probada
(`SegmentDetectionPolicy.ComposeForFile`) y recarga la lista del editor. Las cinco mutaciones —
guardar y borrar manual, aceptar, corregir y borrar detectada — recomponen antes de devolver su
resultado. / rereads both stores, recomposes with the tested rule, and reloads the editor's list;
all five mutations recompose before returning.

**GREEN.** `LiveMarkerWiringTests` 2/2; UiTests 348/348; `dotnet format` limpio; `-warnaserror`
0/0. **Límite declarado**: ver el botón aparecer sin reabrir sobre vídeo real pertenece al paseo
físico tras WP-2. / seeing the button appear over real video without reopening belongs to the
post-WP-2 walk.

## ARQ-008 — Un solo dueño para el coordinador de ventanas / One owner for the window coordinator

**El defecto / The defect.** `PlayerWindowCoordinator` estaba registrado en el contenedor y a la
vez construido a mano en `ShellView`: dos instancias, una de las cuales guardaba geometría que
nadie leería jamás, y cualquier cableado futuro elegiría una mitad en silencioso desacuerdo con la
otra. / registered in the container and simultaneously newed in `ShellView`: two instances, one
holding geometry nobody would ever read.

**La decisión / The decision.** El dueño es `ShellView`: el coordinador es estado de ventana por
vista (geometrías de mini y pantalla completa de **esa** ventana), no un servicio de aplicación. El
registro del contenedor era la mitad muerta y se retira. / The owner is `ShellView`: the
coordinator is per-view window state, not an application service; the container registration was
the dead half and is removed.

**RED (archivado / archived).** `WindowCoordinatorOwnershipTests`, una aserción doble contra el
ensamblado, en rojo a la primera ejecución. / one two-sided assembly assertion, red on its first
run.

**GREEN.** `WindowCoordinatorOwnershipTests` 1/1; UiTests 349/349; ArchitectureTests 14/14 —
`PlayerWindowCoordinator` fuera de `PendingWiring` porque ya no hay registro que vigilar;
`dotnet format` limpio; `-warnaserror` 0/0.

## ARQ-A01 — Los registros muertos, retirados / The dead registrations, removed

**RED (archivado / archived).** Con sus seis entradas fuera de `PendingWiring`, la puerta los
nombró / with their six entries out of `PendingWiring`, the gate named them:

> These services are registered in CompositionRoot and nothing ever resolves them: IMigrationRunner,
> MarkerEditorViewModel, PlayerViewModel, SkipMarkerViewModel, StartPlayback, StopPlayback

**La corrección / The fix.** Los seis registros retirados (2026-08-09): cada uno estaba duplicado
por el `new` que el ensamblado usa de verdad — las sesiones arrancan y paran por el coordinador, el
host migra con `MigrationRunner` directamente, y `OpenPlayerAsync` construye sus superficies con
sus manejadores. De paso, la aserción de alcanzabilidad que daba por cableado el arranque de
reproducción porque veía `StartPlayback` registrado afirma ahora `IPlaybackSessionCoordinator`, que
es quien lo hace. `WatchStatusViewModel` queda en la puerta: es la mitad de CNT-A01. / all six
removed; each was shadowed by the `new` the assembly really uses. The reachability assertion that
trusted the dead registration now asserts the coordinator that actually starts sessions.

**GREEN.** ArchitectureTests 16/16; UiTests 354/354; `dotnet format` limpio; `-warnaserror` 0/0.

## PLY-A01 — Las preferencias guardadas se aplican / Stored preferences are applied

**El defecto / The defect.** `ApplyPlaybackPreferences` — resolución archivo→serie→global con
repliegue por idioma, probada de punta a punta — registrado y jamás invocado: cada sesión abría con
las pistas que LibVLC eligiera. / registered and never invoked: every session opened with whatever
tracks LibVLC picked.

**La corrección / The fix.** `OpenPlayerAsync` aplica las preferencias en cuanto el medio abre
(ámbito de archivo por identificador, ámbito de serie cuando el archivo es un episodio) y el
selector de pistas recibe las pistas efectivamente aplicadas en vez de `null`. La entrada salió de
`PendingWiring`. / the session applies the resolved preferences the moment the media opens and the
track selector receives what was actually applied.

**GREEN.** ArchitectureTests 16/16 (la puerta exige el consumo); el paseo físico ensamblado
(`AssembledPhysicalWalkTests` 3/3) recorre el camino con sesiones y decodificación reales;
IntegrationTests de reproducción/updates 78/78; `dotnet format` limpio; `-warnaserror` 0/0.

### Seguimiento 2026-08-09 — la carrera que el cableado trajo consigo / The race the wiring brought

**El defecto / The defect.** Aplicar las preferencias tras `OpenAsync` supone una sesión viva, pero
`ApplyPlaybackPreferences.ApplyAsync` llama a `SelectTrackAsync` incondicionalmente (deshabilitar
subtítulos también es una selección). Con un medio que nunca abrió — el archivo de un byte que
`AssembledJourneyTests` siembra a propósito — el motor lanza `EngineUnavailable` y la excepción
escapaba sin observar. / Applying preferences after `OpenAsync` assumes a live session, but
`ApplyAsync` calls `SelectTrackAsync` unconditionally (disabling subtitles is also a selection).
Against a medium that never opened — the one-byte file `AssembledJourneyTests` seeds on purpose —
the engine throws `EngineUnavailable` and the exception escaped unobserved.

**RED.** Cuatro runs de CI consecutivos desde `f6a99e9` (31307998351, 31307997280, 31308081009,
31308313660; 31308314809 con el mismo job en rojo) fallando
`Playing_from_a_card_opens_the_session_the_application_wired` con
`EngineUnavailable: No media is currently open` en `SelectTrackAsync`; reproducido en local a la
primera ejecución. / Four consecutive CI runs since `f6a99e9` failing the same journey with the
same exception; reproduced locally on the first run.

**La corrección / The fix.** `OpenPlayerAsync` tolera `EngineUnavailable` alrededor del apply: una
sesión que no abrió, o que se cerró debajo, no tiene pistas entre las que elegir, y la superficie
ya lleva el diagnóstico de esa sesión; el selector repliega a «nada aplicado». Cualquier otro fallo
sigue su camino. / `OpenPlayerAsync` tolerates `EngineUnavailable` around the apply: a session that
never opened, or closed underneath, has no tracks to choose between, and the surface already
carries that session's own diagnosis; the selector falls back to "nothing applied". Any other
failure still propagates.

**GREEN.** El test antes rojo pasa; AccessibilityTests 57/57; UiTests 354/354; ArchitectureTests
16/16; `dotnet format` limpio; `-warnaserror` 0/0; verify-docs en verde.

## CNT-A01 — El conmutador de «visto» guarda y el umbral tiene superficie / The watched toggle stores and the threshold has a surface

**El defecto / The defect.** Tres caras del defecto de la casa en la continuidad: el conmutador de
«visto» se construía con manejador nulo (cada marca de una persona iba a ninguna parte y la tarjeta
la olvidaba al recargar), el contenedor llevaba un registro muerto de `WatchStatusViewModel`
sombreado por el `new` que la tarjeta usa de verdad, y `ConfigureWatchedThreshold` — probado,
con clamp y recálculo — no tenía superficie. / Three faces of the house defect in continuity: the
watched toggle was built with a null handler (a person's mark went nowhere and the card forgot it
on reload), the container carried a dead `WatchStatusViewModel` registration shadowed by the `new`
the card really uses, and `ConfigureWatchedThreshold` — tested, clamping, recalculating — had no
surface.

**RED.** Las tres entradas salieron de `PendingWiring` y la puerta los enumeró:
`These services are registered in CompositionRoot and nothing ever resolves them:
ConfigureWatchedThreshold, SetWatchStatus, WatchStatusViewModel`. / The three entries left
`PendingWiring` and the gate named them.

**La corrección / The fix.** `CreateLibraryViewModel` entrega el manejador real: un estado elegido
se guarda como decisión manual por `SetWatchStatus.MarkAsync` (con el archivo de la versión
efectiva como origen), quitar la decisión pasa por `ClearOverrideAsync` bajo el umbral en vigor, y
el control muestra después lo que el repositorio tiene — nunca lo que el clic esperaba. El registro
muerto se retiró en el mismo cambio. El umbral vive en los ajustes de recomendaciones
(`RecommendationSettingsViewModel` recibe el caso de uso por constructor): deslizador 50–100 %,
aplicar persiste, recalcula sólo los estados automáticos y dice cuántos movió. / The card hands the
real handler over; clearing recomputes under the threshold in force; the dead registration left in
the same change. The threshold lives in the recommendation settings: a 50–100 % slider, apply
persists, recalculates only automatic states, and says how many moved.

**GREEN.** El paseo ensamblado nuevo
(`Marking_a_card_watched_survives_reloading_through_the_application_wiring`) marca, recarga y
limpia contra SQLite real por el cableado de la aplicación; la ruta de ajustes ensamblada afirma
`HasWatchedThreshold`; `WatchedThresholdSettingsTests` (3) y `WatchStatusWiringTests` (3) en
UiTests; ArchitectureTests 16/16 con la puerta ya sin las tres entradas; AccessibilityTests 58/58;
UiTests 360/360; `dotnet format` limpio; `-warnaserror` 0/0; verify-docs en verde.

## LIB-A01 — Una carpeta puede irse del catálogo / A folder can leave the catalog

**El defecto / The defect.** `RemoveLibraryRoot` — probado en T5 con la retirada conservadora que
deja el disco intacto — registrado y sin superficie: no había manera de retirar una carpeta de la
biblioteca desde la aplicación. Además ninguna superficie listaba las raíces catalogadas. /
registered with no surface: no way to remove a folder from the application, and no surface even
listed the cataloged roots.

**RED.** La entrada salió de `PendingWiring` y la puerta lo nombró: `These services are registered
in CompositionRoot and nothing ever resolves them: RemoveLibraryRoot`. / The entry left
`PendingWiring` and the gate named it.

**La corrección / The fix.** La gestión de carpetas vive en la superficie de onboarding de la ruta
de biblioteca, que ahora lista las carpetas catalogadas (releídas en cada visita a la ruta).
Retirar es una decisión confirmada: pedir no toca nada; la confirmación dice la verdad — la
carpeta se retira del catálogo, ningún vídeo del disco se toca, y re-añadirla la cataloga de
nuevo — y confirmar ejecuta `RemoveLibraryRoot`, refresca la lista y hace que el shell recargue el
catálogo en pantalla. / Folder management lives in the library route's onboarding surface, which
now lists the cataloged folders (re-read on every visit). Removal is a confirmed decision: asking
touches nothing; the confirmation tells the truth — the folder leaves the catalog, no video on
disk is touched, adding it again catalogs it anew — and confirming runs `RemoveLibraryRoot`,
refreshes the list, and has the shell reload the catalog on screen.

**GREEN.** El paseo ensamblado
(`Removing_a_folder_from_the_library_route_leaves_every_video_on_disk`) lista, pide, confirma y
comprueba el archivo intacto en disco con SQLite real; cinco pruebas de retirada en
`RootOnboardingViewModelTests`; ArchitectureTests 16/16 con la puerta sin la entrada;
AccessibilityTests 59/59; UiTests 365/365; `dotnet format` limpio; `-warnaserror` 0/0; verify-docs
en verde.

## ART-A01 — El arte se retira en vez de prometerse / Artwork is retired rather than promised

**El defecto / The defect.** `ArtworkCache` — descarga acotada a 10 MB, allowlist de hosts,
probado en SEC-005 — registrado y jamás resuelto: nadie descargaba arte y ninguna superficie lo
mostraba (la mitad de arte del hueco que encontró ADR-0003). / registered and never resolved:
nobody fetched art and no surface showed it.

**RED.** La última entrada salió de `PendingWiring` — **la lista queda vacía** — y la puerta lo
nombró: `ArtworkCache`. / The last entry left `PendingWiring` — **the list is now empty** — and
the gate named it.

**La decisión / The decision.** La alternativa honesta que el plan pre-autorizó: cablear la cadena
completa exige llevar el póster por el almacén de candidatos (esquema nuevo o segunda llamada al
proveedor), dos superficies de imagen y pruebas de red — desproporcionado para un MVP cuya
identificación remota viaja desactivada (sin token no hay red, así que el arte estaría dormido en
casi toda instalación). El registro se retiró (regla ARQ-A01) con el hueco documentado aquí y en
el plan; la clase y su propósito de red declarado (`image.tmdb.org`) permanecen para el arte
personal y para el día en que el hueco se cierre. / The pre-authorised honest alternative: wiring
the full chain — poster plumbing through the candidate store, two image surfaces, network tests —
is out of proportion for an MVP whose remote identification ships disabled. The registration left
(ARQ-A01 rule) with the gap documented; the class and its declared network purpose stay.

**De paso, ARQ-006 arrancó / Along the way, ARQ-006 started.** La aserción textual de
alcanzabilidad (`SurfaceReachabilityTests`) que exigía «ArtworkCache» en el texto de
`CompositionRoot` llevaba desde T39B satisfecha por el registro muerto — la tercera aparición del
patrón «texto muerto satisface aserción». Según lo decidido, el registro completo se extrajo como
`AddLocalMedia(this IServiceCollection, IAppDataPaths, ShellHost)` y esa aserción vive ahora en
`CompositionDescriptorTests` (AccessibilityTests) afirmando **descriptores registrados** — proveedor
y fuente de candidatos presentes, `ArtworkCache` ausente — en vez de caracteres del archivo. Los
pasos 2-3 de ARQ-006 (módulos y las demás aserciones textuales) siguen pendientes en el plan. /
The reachability assertion that demanded "ArtworkCache" in the file's text had been satisfied by
the dead registration since T39B — the third dead-text case. As decided, the registrations were
extracted as `AddLocalMedia(...)` and the assertion now lives in `CompositionDescriptorTests`,
asserting registered descriptors instead of characters. ARQ-006 steps 2-3 remain planned.

**GREEN.** ArchitectureTests 16/16 con `PendingWiring` vacío; `CompositionDescriptorTests` 1/1;
UiTests 379/379; AccessibilityTests 60/60; IntegrationTests 372/372+1 omitido (los
`ArtworkCacheTests` de SEC-005 intactos — la clase no cambió); DocumentationTests 58/58;
`dotnet format` limpio; `-warnaserror` 0/0; verify-docs en verde.

## AUD-A01 — Elegir salida llega al motor / Choosing an output reaches the engine

**El defecto / The defect.** `LibVlcAudioOutputAdapter` — que pausa, enruta, reanuda y guarda,
probado en T23 — registrado y jamás resuelto, y además sin camino posible: `IMediaPlayerEngine` no
tenía ninguna API de dispositivo de salida. Elegir un dispositivo en `AudioOutputViewModel` no
cambiaba nada de dónde sonaba el audio. / registered and never resolved, and with no possible
path: the engine had no output-device API at all. Picking a device on screen changed nothing about
where the sound went.

**RED.** La entrada salió de `PendingWiring` y la puerta lo nombró: `LibVlcAudioOutputAdapter`. /
The entry left `PendingWiring` and the gate named it.

**La corrección / The fix.** En el orden decidido — motor primero: `IMediaPlayerEngine` gana
`SetAudioOutputDeviceAsync`, que une los identificadores estables de Windows con los de LibVLC por
su sufijo común de endpoint y entrega sin cambios un identificador no anunciado (VLC lo ignora:
perder un dispositivo jamás mata la sesión); probado con motor y decodificación reales en
MediaTests, incluida la ausencia de sesión nombrada como `EngineUnavailable`. Después el
adaptador: `EngineAudioOutputTarget` implementa `IAudioOutputTarget` sobre el motor (pausar,
reanudar, enrutar). Por último el manejador: `AudioOutputViewModel` gana `SelectionHandler`
(opcional, como el de gestos) y `OpenPlayerAsync` lo cablea a `LibVlcAudioOutputAdapter.SelectAsync`
con ámbito global; la respuesta del adaptador es la autoritativa en pantalla (repliegues y
reducciones reales, no la intención del clic). Al abrir, el dispositivo global guardado se aplica
directamente al motor — sin pasar por `SelectAsync`, para que un repliegue por dispositivo ausente
no se guarde como si fuera una elección. / Engine first: `SetAudioOutputDeviceAsync` joins the
Windows identifiers with LibVLC's by their common endpoint suffix and hands unknown identifiers
over unchanged; tested against a real engine, including the sessionless case named
`EngineUnavailable`. Then the adapter over the engine; last the optional `SelectionHandler` wired
by `OpenPlayerAsync` to `SelectAsync` with global scope, the adapter's answer being what the
screen shows. On open the stored global device is applied directly, bypassing `SelectAsync` so a
fallback is never stored as a choice.

**GREEN.** `AudioOutputDeviceTests` (MediaTests, 2/2) con decodificación real;
`AudioOutputWiringTests` (4) en UiTests: el pick llega al manejador y la pantalla muestra la
respuesta de la máquina, una sesión muerta bajo el pick deja la superficie en pie, sin manejador
la superficie sigue listando, y la composición une superficie, adaptador y motor;
`AudioDeviceLifecycleTests` 3/3 intactos; ArchitectureTests 16/16 con la puerta sin la entrada;
Application.Tests 195/195; UiTests 380/380; AccessibilityTests 59/59; `dotnet format` limpio;
`-warnaserror` 0/0; verify-docs en verde. **El límite honesto sigue**: PLY-004 permanece `BLOCKED`
por hardware — ningún endpoint de esta máquina declara más de dos canales, así que 5.1/7.1 sigue
sin ejercerse sobre hardware real. / The honest limit stays: PLY-004 remains hardware-blocked.

## VSW-A01 — El reproductor ofrece las otras versiones / The player offers the other versions

**El defecto / The defect.** `SwitchMediaVersion` — que escribe la posición vieja antes de nada y
pregunta antes de trasladar lo que la política no traslada sola, probado en T27 — registrado y sin
superficie de origen; el diálogo de confirmación colgaba del shell resuelto sin manejador, así que
sus botones sólo escondían la pregunta. / registered with no origin surface; the confirmation
dialog hung off the shell resolved with no handler, so its buttons only hid the question.

**RED.** La entrada salió de `PendingWiring` y la puerta lo nombró: `SwitchMediaVersion`. / The
entry left `PendingWiring` and the gate named it.

**La corrección / The fix.** La sesión en vivo lista sus otras versiones
(`PlayerVersionsViewModel`, sólo cuando el título tiene grupo — el mismo repliegue por miembro que
las fichas): cada fila dispara el caso de uso sin confirmar; si la política pide confirmación, el
diálogo ya probado aparece con manejador de verdad — «continuar ahí» re-ejecuta confirmado,
«empezar de nuevo» re-ejecuta con el reinicio explícito nuevo del comando (`RestartFromZero`,
probado: abre en cero y graba cero), «cancelar» no toca nada. Tras un cambio abierto, las
superficies del reproductor se reconstruyen para el archivo que ahora suena, reanudando desde el
estado recién grabado; el registro sin manejador del diálogo se retiró. / The live session lists
its other versions; each row fires the use case unconfirmed; when the policy asks, the tested
dialog appears with a real handler — confirm re-runs confirmed, "start again" re-runs with the
command's new explicit restart (opens at zero, records zero), cancel touches nothing. After an
opened switch the player surfaces are rebuilt for the file now playing, resuming from the freshly
recorded state; the dialog's handlerless registration left.

**GREEN.** `SwitchMediaVersionTests` 7/7 con el caso nuevo de reinicio; `VersionSwitchWiringTests`
(5) en UiTests: el origen alcanza el caso de uso, el diálogo se construye con su manejador, una
fila entrega su propia versión, una versión no disponible no se ofrece y la superficie sólo existe
con alternativas; el paseo ensamblado afirma que un título de una sola copia no ofrece el cambio y
el diálogo llega cableado; ArchitectureTests 16/16; Application.Tests 195/195; UiTests 376/376;
AccessibilityTests 59/59; `dotnet format` limpio; `-warnaserror` 0/0; verify-docs en verde.

## LIB-002/003 (mitad de movidos) — Todo escaneo reconcilia / The moved-file half — every scan reconciles

**El defecto / The defect.** La reconciliación existía como piezas y nunca como conducta:
`ReconcileScanResults`, `FileReconciliationPolicy` y `ManualReassignmentViewModel` — probados en
T8 — registrados y jamás invocados por ningún escaneo. Un archivo movido se catalogaba como
extraño (fila nueva, progreso huérfano en la fila vieja), nadie capturaba identidades al escanear,
e `ICatalogRepository` era además un registro muerto. / Reconciliation existed as parts and never
as behaviour: tested in T8, registered, and invoked by no scan. A moved file was catalogued as a
stranger, nobody captured identities at scan time, and `ICatalogRepository` was moreover a dead
registration.

**RED.** Las cuatro entradas salieron de `PendingWiring` y la puerta las nombró:
`FileReconciliationPolicy, ICatalogRepository, ManualReassignmentViewModel, ReconcileScanResults`.
/ The four entries left `PendingWiring` and the gate named them.

**La corrección / The fix.** `ReconcileScannedFiles` monta primero en el pipeline compartido
(reconciliar → identificar → agrupar): cada escaneo captura la identidad de lo nuevo
(`CompositeFileIdentityProvider`: id estable NTFS + huella acotada, cada mitad puede fallar sola) y
reconcilia con reglas conservadoras — coincidencia exacta con el archivo viejo ausente del escaneo
se reasigna sola (la fila extraña que el propio escaneo creó se absorbe dentro de
`ReconcileScanResults`, también en la confirmación manual); una coincidencia cuyo archivo viejo
sigue presente es una copia y la posee el agrupado; lo ambiguo queda en `PendingReassignments` SIN
identidad guardada, de modo que cada escaneo re-deriva la oferta hasta que una persona decide. La
bandeja de revisión lista las ofertas: confirmar un candidato pasa por
`ManualReassignmentViewModel` y conserva entidad y progreso bajo la ruta nueva; «es un archivo
nuevo» guarda su identidad y la oferta no vuelve. El registro muerto de `ICatalogRepository` se
retiró (regla ARQ-A01). / `ReconcileScannedFiles` mounts first in the shared pipeline; every scan
captures identity and reconciles conservatively — exact matches with the old file gone reassign
alone (the stranger row the scan created is absorbed, in the manual confirmation too); a match
whose old file is still present is a copy owned by grouping; the ambiguous is held with no stored
identity so every scan re-derives the offer until a person decides. The inbox lists the offers;
confirming keeps entity and progress under the new path; "it is a new file" stores its identity
and the offer never returns. The dead `ICatalogRepository` registration was removed (ARQ-A01
rule).

**GREEN.** `ScanReconciliationTests` (IntegrationTests, 4/4) recorre disco, escaneo y SQLite
reales: un archivo movido entre escaneos conserva su entidad sin dejar fila extraña, una copia
byte a byte sigue siendo copia, lo ambiguo espera, sobrevive al re-escaneo y se confirma a mano, y
«como nuevo» apaga la oferta para siempre. `ReassignmentInboxTests` (4) y
`ReconciliationWiringTests` (2) en UiTests; ArchitectureTests 16/16 con la puerta sin las cuatro
entradas; Application.Tests 194/194; IntegrationTests 372/372+1 omitido; AccessibilityTests 59/59;
UiTests 371/371; `dotnet format` limpio; `-warnaserror` 0/0; verify-docs en verde. **De paso**: el
run de CI de `a6c7b9a` destapó que la reflexión de `RootLifecycleTests` no rellena los parámetros
opcionales nuevos del VM de onboarding (`MissingMethodException`); corregido aquí pasando los
argumentos explícitos. / Along the way: the `a6c7b9a` CI run uncovered that `RootLifecycleTests`'
reflection does not fill the onboarding view model's new optional parameters; fixed here by
passing them explicitly.
