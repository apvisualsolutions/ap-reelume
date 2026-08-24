# Las cinco capturas del README, y los tres defectos que destaparon / The README's five captures, and the three defects they uncovered

El paso 11 pedía cinco imágenes de la aplicación. Tomarlas encontró un defecto del producto que
ninguna puerta miraba, otro que llevaba un día en la matriz de paridad sin que nadie lo viera, y un
tercero en la biblioteca sembrada que hacía mentir a la propia captura. / Step 11 asked for five
images of the application. Taking them found a product defect no gate was watching, another that had
been sitting in the parity matrix for a day unseen, and a third in the seeded library that made the
capture itself lie.

Fecha / Date: 2026-08-24. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Qué se publica y cómo se toma / What is published and how it is taken

Cinco PNG en `docs/assets/`, a 1600 × 1000, tema oscuro, en inglés: `home`, `library`, `show`,
`player`, `review`. Las toma `shoot.ps1` contra el binario de Release, con
`AP_LOCALMEDIA_DATA_ROOT` apuntando a una raíz sembrada **fuera del árbol** y con títulos
inventados. / Five PNGs under `docs/assets/`, 1600 × 1000, dark theme, in English. `shoot.ps1` takes
them against the Release binary over a seeded data root outside the tree, with invented titles.

Dos mediciones cambiaron cómo se toman: / Two measurements changed how they are taken:

| Pregunta | Medición | Consecuencia |
| --- | --- | --- |
| ¿Por qué el reproductor sale transparente? | `PrintWindow` no compone la capa de GPU | `-Screen`: `CopyFromScreen` sobre los límites reales |
| ¿Por qué el escritorio asoma en las esquinas? | La ventana es redondeada y el rectángulo la excede | `DWMWA_WINDOW_CORNER_PREFERENCE` = `DONOTROUND` y `DWMWA_EXTENDED_FRAME_BOUNDS` |
| ¿Por qué la vista se desborda a 1600 × 1000? | La pantalla pinta al 150 %: la app recibía 1067 × 667 lógicos | `-Downscale 1.5`: se pide 2400 × 1500 y se guarda a 1600 × 1000 |
| ¿`AVALONIA_SCREEN_SCALE_FACTORS` fuerza el 1:1? | Captura idéntica con y sin la variable | **refutada**, y por eso el sobremuestreo |

La tercera fila es la que más cambia la imagen: a 1600 × 1000 **físicos** la aplicación sólo tenía
667 px lógicos de alto, y con eso la banda de transporte del reproductor caía fuera de la ventana y
la ficha de una serie nacía desplazada. Pidiendo el doble y guardando la mitad, la aplicación recibe
los 1600 × 1000 **lógicos** que el diseño dibuja, y el texto llega sobremuestreado. / The third row
is the one that changes the image most: at 1600 × 1000 physical pixels the application had only 667
logical pixels of height, which put the player's transport outside the window and started the series
card scrolled. Asking for twice and saving half gives the application the 1600 × 1000 the design
draws, and supersamples the text on the way down.

**Sin insignia de CI, decidido habiendo medido.** El flujo no corre en `main` a propósito (`ci.yml`
lo dice: `main` recibe el mismo SHA por avance rápido). `gh run list --branch main` devuelve
ejecuciones cuya última es del 2026-08-23, con `main` ya varios commits por delante: una insignia
apuntando ahí se queda verde y **deja de significar nada**, que es la definición de puerta ciega de
esta casa. En su lugar, la página dice en palabras qué ha pasado todo commit que llega a `main`, con
enlace al flujo. / **No CI badge, decided having measured.** The workflow deliberately does not run
on `main`, whose newest run is from 2026-08-23 while `main` is several commits ahead: a badge
pointing there stays green and stops meaning anything. The page says in words what every commit
reaching `main` has been through, and links to the workflow.

## Defecto 1: el reproductor no tenía ni barra ni reloj / Defect 1: the player had no scrubber and no clock

**Síntoma medido**: con una película real abierta y reproduciéndose, la banda de transporte mostraba
saltos, silencio, volumen, velocidad, pausa, parada y «Playing» — y **ninguna barra y ningún
reloj**. Pulsando el salto adelante aparecían de golpe, con `1:00:29` y `1:36:00`. / **Measured
symptom**: with a real film open and playing, the transport showed the skips, mute, volume, speed,
pause, stop and "Playing" — and no scrubber and no clocks. Pressing the forward skip made all of it
appear at once.

**Causa**: la fila del scrubber vive tras `IsVisible="{Binding HasDuration}"`, y `HasDuration` sale
de `_state.Duration`, que sólo cambia en `Apply(...)` — llamado por las órdenes del propio
transporte y por nadie más. El cabezal sí llegaba a `CompositionRoot.OnPositionChanged`, que se lo
entrega al rastreador de progreso y a la oferta de salto; el transporte no estaba en esa lista. /
**Cause**: the scrubber's row hangs off `HasDuration`, which comes from a state only the bar's own
commands ever changed. The playhead did reach `OnPositionChanged`, which hands it to the progress
tracker and to the skip offer; the transport was not on that list.

Las dos superficies vecinas de ese mismo manejador llevan escrito el comentario de haber estado
«alcanzables y sin alimentar». Ésta es la tercera del manejador y la **decimoquinta forma del
defecto de la casa**. / The two neighbouring surfaces in that same handler carry a comment about
having been "reachable and never fed". This is the third in the handler, and the fifteenth shape of
the house defect.

**Corrección**: `TransportControlsViewModel.Observe(position, duration)` toma el cabezal sin tocar lo
que es de la persona —velocidad, volumen, los dos saltos— y **sólo acepta la duración cuando el
motor da una**, porque los primeros pulsos de una sesión llegan antes que la longitud y un cero
pondría en pantalla una barra cuyo máximo es mentira. `OnPositionChanged` la llama por el
despachador. / **Fix**: `Observe(position, duration)` takes the playhead without touching what
belongs to the person, and takes the duration only when the engine gives one.

**Puertas**: `ProgressWiringTests` gana tres — que la composición llama a `transport.Observe(`, que
observar da escala y relojes, y que observar sin longitud deja la barra fuera de pantalla. /
**Gates**: three new tests in `ProgressWiringTests`.

**Y una consecuencia en el paseo físico**: los dos paseos que pulsan saltos los medían con la sesión
reproduciendo. Ese paseo prueba cada control dos veces —un clic **al lado** no debe cambiar nada, y
el clic encima sí—, y con el cabezal vivo el clic de al lado mueve la posición tanto como el salto.
Los saltos pasan a medirse **en pausa**, y el reloj se lee en segundos enteros para que un evento
rezagado de la pausa no cuente como movimiento. / **And a consequence in the physical walk**: the two
walks that press skips measured them on a playing session, where the beside-click moves the position
as surely as the press. They now measure on a paused session, reading the clock in whole seconds.

## Defecto 2: el selector de temporada escribía un nombre de clase / Defect 2: the season picker printed a class name

La ficha de cualquier serie mostraba `ApSolutions.LocalMedia.Presentation.Show.SeasonViewModel`
donde debía leerse «Temporada 1». La píldora `filter-pill` enlaza `SelectionBoxItem` en su
`ContentControl` y **no enlazaba `ItemTemplate`**, así que el presentador recibía un modelo de vista
sin forma de dibujarlo y caía en `ToString()`. La plantilla del tema base enlaza los dos. / Every
series card read the view model's type name where it should read "Season 1": the pill's template
bound the selection box item and not the item template, so the presenter fell back to `ToString()`.

Estaba **en la matriz de paridad del día anterior** —`app-show-dark-v3.png` lo enseña— y nadie lo
vio. Los dos desplegables de la biblioteca nunca lo enseñaron porque sus filas son `ComboBoxItem`
con texto dentro. Puerta: `ComboBoxStateTests` mide una píldora con plantilla y exige que dibuje lo
que la plantilla dibuja. / It was in the previous day's parity matrix and nobody saw it. Gate:
`ComboBoxStateTests` now measures a templated pill.

## Defecto 3: la biblioteca sembrada listaba cada archivo dos veces / Defect 3: the seeded library listed every file twice

La primera captura de Biblioteca decía **21 elementos** y enseñaba «Cartas desde Antares» junto a
«Cartas.desde.Antares.2017»: el mismo archivo, dos tarjetas. / The first library capture said 21
items and showed the same file twice, once identified and once by its file name.

La causa **no es del producto sino de la siembra**, y el producto lo dice en dos sitios:
`ApplyIdentification` escribe «every title the catalogue projects is a scanned file, and its id is
the media file's own», y la proyección de `CatalogRepository` oculta un archivo escaneado sólo
`WHERE NOT EXISTS` un título **cuyo id es el del archivo**. El sembrador daba a cada título un GUID
nuevo, así que la condición nunca se cumplía. / The cause is the seed's, not the product's: the
application's own bridge says a title's id is its media file's, and the catalogue's projection hides
a scanned file only when a title with that id exists. The seeder minted a fresh guid per title.

Corregido en el sembrador (fuera del árbol): el id del título es el del archivo. Efecto medido: 12
elementos, ninguno repetido — y **una segunda cosa que ya no pasa**, que la reproducción escribiera
su progreso bajo una clave que Inicio no lee. Antes de la corrección, `watch_state` tenía dos filas
para «El Faro de Piedra» y el aviso de reanudación ofrecía 00:00:34 mientras Inicio decía 54 %.
Ahora hay una sola fila y el aviso ofrece 00:52:14. / Fixed in the seeder: 12 items, none repeated —
and progress no longer written under a key Home does not read. The resume prompt offered 00:00:34
against Home's 54 %; it now offers 00:52:14.

La misma tanda sembró tres archivos que **nadie identifica** y sus candidatos, porque la bandeja de
revisión salía vacía —«Nothing left to review»— y una de las cinco capturas del README no puede ser
un estado vacío. / The same pass seeded three files nobody identifies and their candidates, because
the review inbox came out empty and one of the README's five captures cannot be an empty state.

## Lo que el vídeo del reproductor es, y por qué / What the player's video is, and why

Los archivos que siembra el sembrador son de **2 bytes**: no hay nada que reproducir. La cuarta
captura necesita una película de verdad, y una película de verdad en un README es un problema de
derechos. Se genera con `ffmpeg` una imagen fija —un faro sobre el mar, en la paleta del producto— y
se codifica a la duración que el catálogo declara para «El Faro de Piedra», 1 h 36 m, de modo que la
posición sembrada del 54 % sea válida y el reloj de la captura diga `52:12 / 1:36:00`. Pesa 15 MB y
vive fuera del árbol. / The seeder's files are two bytes long, so the fourth capture needs a real
film — and a real film in a README is a rights problem. A still is generated with `ffmpeg` and
encoded to the duration the catalogue declares, so the seeded 54 % is a valid position and the
capture's clock reads `52:12 / 1:36:00`.

## El rojo de CI que trajo esta tanda, y por qué no era del cambio / The CI red this batch brought, and why it was not the change's

`MediaPlayerReleaseOwnershipTests.A_detached_media_rests_in_the_factorys_queue` falló en CI y pasó
aquí. La suite no referencia ni `Presentation` ni `Windows` —lo único que este cambio toca del
producto—, así que el rojo no podía venir de él; lo que sí depende del ejecutor es lo que la prueba
mide. / The test failed on CI and passed here. The suite references neither of the two projects this
change touches, so the red could not come from it; what does depend on the runner is what the test
measures.

**La causa es el instrumento**: afirmaba sobre `PendingDeferredReleaseCount`, que es un **nivel** —lo
que la cola tiene ahora mismo— y el drenaje lo vacía un segundo después de que cada media llegue. Si
`StopAsync` tarda más que esa ventana de reposo, el nivel ya ha vuelto a su sitio cuando se lee.
Medido: la suite tarda **1 m 33 s aquí y 6 m 28 s en el ejecutor**, cuatro veces más lento. / The
assertion read a level the drain lowers a second later; on a runner four times slower, the stop
outlasts the quiescence window and the level is back before it is read.

**Corregido cambiando lo que se mide, no la tolerancia**: `LibVlcFactory` expone
`DeferredReleaseTotal`, un total monótono que se incrementa dentro de `DeferRelease`, y la prueba
afirma sobre él. Lo que el contrato dice es que la media **pasó por la cola**, y eso no se lo puede
llevar nadie. / Fixed by changing what is measured, not the tolerance: a monotone total, incremented
inside the queue's own door, says what the contract says.

## Verificación / Verification

| Puerta | Resultado |
| --- | --- |
| `dotnet format --verify-no-changes --severity warn` | sin cambios / clean |
| `UiTests` | 783 / 783 |
| `AccessibilityTests` | 146 / 146 |
| `IntegrationTests` | 461 / 462, 1 omitida por diseño |
| `ArchitectureTests` | 30 / 30 |
| `DocumentationTests` | 87 / 87 |
| `eng/verify-docs.ps1` | 241 documentos, 32 localizados, 59 identificadores |
