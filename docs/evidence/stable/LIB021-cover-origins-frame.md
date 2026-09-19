# Un fotograma del propio vídeo cuando no hay otra portada / A Frame of the Video Itself When There Is No Other Cover

- IDs: `LIB-021`, `ENG-003`
- Fecha / Date: 2026-09-19
- Alcance / Scope: `CaptureTitleFrames`, `TitleFramePass`, `TitleFrameSourceRepository`, `LibVlcVideoFrameGrabber`, `IdentifyingScanCoordinator`, `CompositionRoot`, `CatalogItemViewModel`, `LibraryViewModel`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Qué se cerró

El segundo de los tres planes de `LIB-021`
(`docs/superpowers/plans/2026-09-18-lib-021-cover-origins-frame.md`). Una película, una serie o un
archivo sin identificar que no tiene portada elegida ni del proveedor muestra un fotograma de su
propio vídeo, sacado solo, una vez, y guardado en `cache/title-frames`. Las piezas estaban hechas
desde el 2026-09-18 y sin conectar; hoy se conectaron.

### Las tres preguntas que pararon la conexión, contestadas

1. **¿El paseo pasa por el montaje de la ventana y por el aviso de fin de escaneo?** Leído en el
   código: sólo dos escenas montan la ventana entera, pero **todas las que escanean** pasan por el
   coordinador único, que es el que avisa.
2. **¿Sus bibliotecas tienen títulos sin portada?** Sí, todas: el paseo no tiene proveedor, así que
   la pasada corre al lado de cada escena que escanea.
3. **La salida.** El plan proponía que el paseo esperase a la pasada antes de pulsar. Se eligió
   otra que quita el riesgo en su origen: **la cuadrícula ya no rehace las tarjetas cuando llega un
   fotograma, sino que la tarjeta que está en pantalla cambia su imagen.** Rehacerlas era lo que
   podía sacar una tarjeta de debajo de una pulsación, fuese de una persona o del paseo. La pasada
   no se apaga en pruebas: corre en todo el paseo, y el paseo entero pasó (152 de 152).

### El cambio

- `TitleFramePass` lanza la pasada sin esperarla y avisa con un evento tras cada tanda que sacó algo.
  La piden la ventana al abrirse y el coordinador al acabar cada escaneo; una segunda petición
  mientras corre la primera no hace nada.
- La cuadrícula escucha ese evento y llama a `RefreshPosters` en el hilo de la interfaz.
- `CatalogItemViewModel.ShowPoster` cambia la portada de la misma tarjeta y avisa de `PosterFile` y
  `HasPoster`.
- El capturador abre los archivos con la única instancia nativa, y sólo los que pasan la lista de
  extensiones aprobadas (lo prueba `A_file_outside_the_approved_containers_is_refused`).
- La previsualización de suelos leyó `CatalogItemViewModel.cs` subiendo de 100/90 a 100/91. Las
  ramas que faltaban eran una copia privada de la lectura de textos traducidos; se sustituyó por
  `PresentationText.Resource`, el archivo llega a 100/100 (30 de 30 ramas, medido con el JSON de
  coverlet) y sale de la lista de deuda, copiada del artefacto del run `35466324832` sin su fila. El
  trinquete baja de 186 a 185.

### Las pruebas, y cuáles se vieron fallar

| Prueba | Mutante que la tumba |
| --- | --- |
| `A_film_with_no_cover_anywhere_shows_a_frame_of_its_own_video` (paseo) | no lanzar la pasada; no avisar a la cuadrícula; lanzarla sólo al abrir la ventana y no tras el escaneo |
| `A_title_left_without_a_frame_gets_one_when_the_window_opens` (paseo) | no lanzarla al abrir la ventana |
| `A_card_on_screen_draws_a_cover_that_arrives_later` y `Refreshing_the_posters_redraws_…` | no avisar de `HasPoster` |
| `A_batch_of_twenty_five_frames_costs_seconds_not_the_sum_of_every_deadline` | un capturador que espera el plazo en cada archivo: 82,7 s |

La escena de la ventana existe porque la primera no distingue las dos entradas: su escaneo de
arranque también pide la pasada.

### El coste medido

Una tanda (25 archivos, uno tras otro) sobre la muestra `mp4-h264-aac`, dos veces: **7,9 s y 7,7 s**,
unos 310 ms por fotograma, por debajo de los 433-472 ms del spike de `CRS-006`. El techo de la
prueba es la mitad de la suma de los plazos (37,5 s).

### Lo que queda

- El plan 3: el ajuste del orden con su «Restaurar valores por defecto» y la excepción por título.
- `CRS-006`: la tarjeta del curso con su fotograma, que ya puede usar el mismo capturador.

---

## English

### What was closed

The second of the three `LIB-021` plans
(`docs/superpowers/plans/2026-09-18-lib-021-cover-origins-frame.md`). A film, a series or an
unidentified file with neither a picked cover nor a provider poster shows a frame of its own video,
taken on its own, once, and kept in `cache/title-frames`. The pieces had been built since
2026-09-18 and left unwired; they were wired today.

### The three questions that stopped the wiring, answered

1. **Does the walk go through the window setup and through the end-of-scan notice?** Read in the
   code: only two scenes set up the whole window, but **every scene that scans** goes through the
   single coordinator, which is what sends the notice.
2. **Do its libraries have titles with no cover?** Yes, all of them: the walk has no provider, so the
   pass runs beside every scene that scans.
3. **The way out.** The plan proposed that the walk wait for the pass before pressing. A different
   one was chosen, which removes the risk at its source: **the grid no longer rebuilds its cards when
   a frame arrives; the card on screen changes its picture.** Rebuilding them was what could pull a
   card from under a press, a person's or the walk's. The pass is not switched off in tests: it runs
   throughout the walk, and the whole walk passed (152 of 152).

### The change

- `TitleFramePass` starts the pass without awaiting it and raises an event after every batch that
  took something. The window asks for it when it opens and the coordinator when each scan ends; a
  second request while the first runs does nothing.
- The grid listens to that event and calls `RefreshPosters` on the interface thread.
- `CatalogItemViewModel.ShowPoster` changes the cover of the same card and notifies `PosterFile` and
  `HasPoster`.
- The grabber opens files on the single native instance, and only those that pass the approved
  extension list (`A_file_outside_the_approved_containers_is_refused` tests it).
- The floor preview read `CatalogItemViewModel.cs` rising from 100/90 to 100/91. The missing branches
  were a private copy of the translated-text lookup; it was replaced with `PresentationText.Resource`,
  the file reaches 100/100 (30 of 30 branches, measured with coverlet's JSON) and leaves the debt
  list, copied from the artefact of run `35466324832` without its row. The ratchet goes from 186 to
  185.

### The tests, and which ones were seen failing

| Test | Mutant that brings it down |
| --- | --- |
| `A_film_with_no_cover_anywhere_shows_a_frame_of_its_own_video` (walk) | not starting the pass; not telling the grid; starting it only when the window opens and not after the scan |
| `A_title_left_without_a_frame_gets_one_when_the_window_opens` (walk) | not starting it when the window opens |
| `A_card_on_screen_draws_a_cover_that_arrives_later` and `Refreshing_the_posters_redraws_…` | not notifying `HasPoster` |
| `A_batch_of_twenty_five_frames_costs_seconds_not_the_sum_of_every_deadline` | a grabber that waits out the deadline on every file: 82.7 s |

The window scene exists because the first one cannot tell the two entry points apart: its startup
scan asks for the pass too.

### The measured cost

One batch (25 files, one after another) over the `mp4-h264-aac` sample, twice: **7.9 s and 7.7 s**,
about 310 ms a frame, below the 433-472 ms of the `CRS-006` spike. The test's ceiling is half the sum
of the deadlines (37.5 s).

### What remains

- Plan 3: the order setting with its «Restore defaults» and the per-title override.
- `CRS-006`: the course card with its frame, which can now use the same grabber.
