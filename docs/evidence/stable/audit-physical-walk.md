# Paseo físico del artefacto ensamblado / Physical walk of the assembled artifact

Evidencia del paseo que la regla de la casa exige tras WP-2: empaquetar la aplicación y recorrerla
como se usa, no como se prueba. La parte que un arnés sin pantalla ni red puede recorrer está
recorrida y guardada como pruebas re-ejecutables; la parte que exige manos y ojos del propietario
está escrita abajo como guion de diez minutos. / Evidence for the walk the house rule demands after
WP-2: package the application and walk it the way it is used, not the way it is tested. The part a
harness without a screen or a network can walk is walked and kept as re-runnable tests; the part
that needs the owner's hands and eyes is written below as a ten-minute script.

## El paquete / The package

`eng/package-x64.ps1` sobre `3b9d9afc` selló ambos artefactos con MakeAppx validando el manifiesto
(sin `/nv`), 671 archivos, 0 huecos de SBOM, 0 secretos en el payload / sealed both artifacts with
MakeAppx validating the manifest (no `/nv`), 671 files, 0 SBOM gaps, 0 secrets in the payload:

```text
66311c449502182bd432ec8dd33ca518a25342d5670c98be7ee04e7b1cd3bec0  APSolutions.LocalMedia_0.1.0_x64.msix
4301490c0e5edd63d92022f80bf7b7244404800b2212c8fec5622626ed222545  ApReelume-0.1.0-win-x64.zip
```

## Lo que el arnés recorrió / What the harness walked

`AssembledPhysicalWalkTests` (AccessibilityTests, colección `AssembledShell`) recorre la aplicación
que `CompositionRoot.CreateShell` construye — el mismo ensamblado que sella el paquete — con
archivos reales en disco, SQLite real y el motor LibVLC decodificando vídeo sintético real de
FFmpeg. Nada está sustituido; la prueba sólo fabrica los archivos de medios. / walks the application
`CompositionRoot.CreateShell` builds — the same assembly the package seals — with real files on a
real disk, real SQLite, and the LibVLC engine decoding real FFmpeg-synthesised video. Nothing is
stubbed; the test only manufactures the media files.

1. **Archivo soltado catalogándose solo + dos copias agrupándose** — los vigilantes arrancan por
   `ConfigureWindow` como con una persona; el escaneo de arranque cataloga la copia que ya estaba,
   una segunda copia soltada después se cataloga sin que nadie pulse nada, el grupo de versiones
   existe tras el escaneo del vigilante y la tarjeta lo abre (`HasDuplicates`). / the watchers start
   through `ConfigureWindow` as they do for a person; the startup scan catalogues the copy already
   there, a second copy dropped afterwards is catalogued with nobody pressing anything, the version
   group exists after the watcher's scan and the card opens it.
2. **Teclas operando la sesión + marca apareciendo sin reabrir** — un vídeo real decodificando por
   la sesión que abrió la tarjeta; el espacio pausa y reanuda por la cadena ensamblada (vista → mapa
   compartido → router → coordinador → motor), respetando la ventana de coalescencia de 250 ms del
   router; una marca guardada en mitad de la sesión hace aparecer la oferta de saltar sobre el
   cabezal sin cerrar nada. / a real video decoding through the session the card opened; space
   pauses and resumes through the assembled chain (view → shared map → router → coordinator →
   engine), honouring the router's 250 ms coalescing window; a marker saved mid-session surfaces
   the skip offer on the playhead without closing anything.
3. **Encadenado de dos episodios** — el primer episodio decodifica hasta su propio final, el estado
   `Ended` del motor levanta la oferta con el nombre del siguiente («T1 E2»), y «reproducir ya»
   encadena la sesión sobre el segundo archivo. / the first episode decodes to its own end, the
   engine's `Ended` state raises the offer with the next episode's name (“T1 E2”), and “play now”
   chains the session onto the second file.

## RED (archivado / archived)

La escena de las teclas falló en su primera ejecución / the keys scene failed on its first run:

> `the space bar never paused the playing session` — AssembledPhysicalWalkTests, 2026-08-09

El foco, el mapa y el router estaban bien; el eslabón muerto era la vuelta: **nada alimentaba
`PlayerViewModel.ApplySessionState`**. El método existía, estaba probado en unidad, y en la
aplicación ensamblada nadie lo llamaba — el motor pausaba y la pantalla se quedaba en «reproduciendo»
para siempre. Una cara más del defecto de la casa, encontrada precisamente por el paseo que existe
para eso. / Focus, map, and router were fine; the dead link was the way back: **nothing fed
`PlayerViewModel.ApplySessionState`**. The method existed, was unit-tested, and in the assembled
application nobody called it — the engine paused and the screen stayed “playing” forever. One more
face of the house defect, found precisely by the walk that exists for that.

## Corrección mínima / Minimal fix

`CompositionRoot.OpenPlayerAsync`, en `OnStateChanged`: cada transición del motor (salvo `Failed`,
cuyo camino propio lleva el código de fallo que este evento no tiene) se reenvía al modelo de la
sesión por el dispatcher. La pausa, la reanudación, la parada y el final llegan ahora a la
pantalla. / every engine transition (except `Failed`, whose own path carries the failure code this
event does not have) is forwarded to the session's view model through the dispatcher. Pause,
resume, stop, and the end now reach the screen.

## GREEN + puertas / GREEN + gates

Las tres escenas del paseo en verde; `dotnet format` limpio; compilación con `-warnaserror` sin
avisos; AccessibilityTests 57/57, UiTests 349/349, ArchitectureTests 16/16 (todas con `-m:1
--settings eng/test.runsettings`); verify-docs en verde. / All three walk scenes green;
`dotnet format` clean; `-warnaserror` build without warnings; AccessibilityTests 57/57, UiTests
349/349, ArchitectureTests 16/16 (all with `-m:1 --settings eng/test.runsettings`); verify-docs
green.

## Guion de diez minutos — la parte que exige tus manos / Ten-minute script — the part that needs your hands

Lo que el arnés no puede jurar: una imagen en una pantalla física, sonido en un altavoz real, TMDB
respondiendo por la red y las teclas multimedia de un teclado de verdad. Con el ZIP del paquete
(`artifacts/package/ApReelume-0.1.0-win-x64.zip`) / What the harness cannot swear to: a picture on
a physical screen, sound on a real speaker, TMDB answering over the network, and the media keys of
a real keyboard. With the package ZIP:

1. **(1 min) Instalar como el manual dice.** Descomprime el ZIP en una carpeta cualquiera y ejecuta
   `ApSolutions.LocalMedia.Windows.exe`. SmartScreen avisará (sin firma): «Más información» →
   «Ejecutar de todas formas» — exactamente lo que documenta SMARTSCREEN.es.md. / Unzip anywhere,
   run the exe, accept the SmartScreen warning the docs describe.
2. **(2 min) Identificación llenando la bandeja con TMDB de verdad.** En Ajustes, pon tu token de
   TMDB (variable `AP_LOCALMEDIA_TMDB_TOKEN` o el campo de la aplicación si ya existe). Añade una
   carpeta con 2-3 películas reales tuyas bien nombradas y una mal nombrada. Tras el escaneo: las
   bien nombradas se identifican solas y la mal nombrada aparece en la bandeja de revisión con
   candidatos. / Set your TMDB token, add a folder with well-named films plus one badly named;
   well-named ones identify alone, the badly named one lands in the review inbox with candidates.
3. **(2 min) Vídeo real en pantalla.** Abre una película desde su tarjeta: imagen visible, sonido
   audible, la barra avanza. Pausa con el espacio y mira que la interfaz DIGA pausado (la corrección
   de este paseo). Flechas: ±10 s. `M` silencia. `F` pantalla completa. / Open a film from its
   card: picture visible, sound audible, bar advancing. Pause with space and check the UI SAYS
   paused (this walk's fix). Arrows ±10 s. `M` mutes. `F` full screen.
4. **(1 min) Teclas multimedia físicas.** Con la sesión abierta, usa la tecla ⏯ del teclado: un
   solo pausado/reanudado (el router deduplica). / With the session open press the hardware ⏯ key:
   exactly one pause/resume.
5. **(2 min) Dos episodios de verdad.** Abre el episodio de una serie tuya y salta cerca del final;
   al terminar, la oferta del siguiente con cuenta atrás; deja que expire una vez (encadena solo) y
   la segunda vez pulsa «Cancelar» (vuelve a la ficha). / Open an episode, seek near the end; the
   next-episode offer counts down; let it expire once (chains alone), cancel the second time.
6. **(1 min) Marca sin reabrir.** En mitad de un episodio, marca una intro [0:00–0:30] en el editor:
   el botón «Saltar intro» aparece sin cerrar el vídeo; púlsalo. / Mid-episode, save an intro
   marker; the skip button appears without reopening; press it.
7. **(1 min) Cierre honesto.** Cierra la ventana con la sesión abierta; reabre la aplicación y la
   película: la oferta de reanudar está donde lo dejaste. / Close with the session open; reopen:
   the resume offer is where you left it.

Si cualquiera de estos siete pasos no hace lo que dice, es un RED del paseo físico: anótalo tal
cual y vuelve aquí. / If any of these seven steps does not do what it says, that is a physical-walk
RED: write it down verbatim and come back here.
