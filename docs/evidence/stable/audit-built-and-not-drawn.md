# Auditoría — lo construido que ninguna pantalla enseña / Audit — what is built and no screen shows

- IDs: `LIB-004`, `PLY-011`, `PLY-014`, `LIB-002`, `LIB-018`, `UX-002`, `CRS-003`, `CRS-004`
- Fecha / Date: 2026-09-04
- Alcance / Scope: las 60 vistas contra `design/AP Reelume.dc.html`, más dos barridos automáticos

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Por qué se hizo esta auditoría

El 2026-09-04 se encontró que la cuadrícula de la biblioteca **no dibujaba ninguna portada**: la
aplicación las descargaba, dejaba elegir la propia, las guardaba y las respaldaba, y la pantalla a
través de la cual se mira la biblioteca entera pintaba un degradado con iniciales. El propietario
pidió entonces barrer las sesenta vistas buscando más casos de esa clase.

**No se buscaron píxeles.** El radio de una esquina, la escala tipográfica y los iconos ya tienen sus
auditorías. Lo que se buscó es lo funcional: **qué existe en el programa y ninguna pantalla enseña**,
y **qué dibuja el prototipo que la aplicación no tiene**.

Salieron dieciocho. Dos se comprobaron a mano antes de escribir esto y los dos resultaron ciertos.

### El defecto de la casa, otra vez, y ahora con su forma exacta

Los dieciocho son la misma cosa vista desde ángulos distintos: **una capa termina su trabajo y la
siguiente no lo recoge**. El caso más caro no es que falte algo por construir — es que **está
construido, probado, traducido a dos idiomas, y nadie lo llama**.

### Lo que rompe algo que ya se prometió

Estas cuatro no son alcance nuevo: son filas que la matriz da por buenas y que la aplicación no
cumple.

#### 1. La biblioteca se corta en el título 50 — `LIB-004`

`LibraryViewModel` pagina con cursor: `HasMore`, `LoadMoreCommand`, `LoadMoreAsync`, y `PageSize: 50`.
**Ninguna vista enlaza nada de eso.** `LibraryView.axaml` no nombra `LoadMoreCommand` ni `HasMore`, y
su código detrás sólo atiende el tamaño y el clic. `ReviewInboxView.axaml` **sí** lo enlaza para su
bandeja, así que la pieza existe en esta casa y aquí falta.

`LIB-004` está `VERIFIED` y promete «Biblioteca de 10.000 archivos… navegación cumple los
presupuestos». Con diez mil archivos, cincuenta son alcanzables.

**Y su evidencia explica cómo pasó**: `T7-catalog-search.md` mide *la consulta por cursor*, que
funciona. La cadena se verificó hasta la capa de datos y no hasta la pantalla — que es exactamente la
lección que este repositorio ya escribió el 2026-08-15: **una cadena se verifica hasta lo que la
persona ve**.

De paso, `ItemCount` cuenta la página cargada, así que la cabecera dirá «50 elementos» para siempre.

#### 2. La cuenta atrás del siguiente episodio no se puede configurar — `PLY-011`

`PLY-011` está `VERIFIED` y su criterio dice, literalmente, «Es **cancelable, configurable** y vuelve
a la ficha si el siguiente archivo no existe». `ContinuityCountdown` guarda su duración en una
preferencia, y **cero la apaga entera**; se lee al reproducir, y quien la escribe son sólo las
pruebas. El comentario de esa clase afirma que «the settings surface already reads and writes» esa
clave. **Esa superficie no existe.**

El prototipo la pone en una sección **«Reproducción»** de Ajustes que la aplicación no tiene, y que
llevaría también «Salto atrás/adelante» y «Preferir reproductor externo».

#### 3. La línea de atajos del reproductor miente en dos de seis — `PLY-014`

`PlayerShortcutHint` dice, en los dos idiomas, «… F pantalla completa · **N** mini · **Esc cierra**».
El mini es **Ctrl+P**, y `Escape` sale del modo superpuesto: no cierra nada. Y es una cadena fija, así
que quien reasigne un atajo en Ajustes seguirá leyendo el anterior.

#### 4. El escaneo se puede cancelar por dentro y no por fuera — `LIB-002`

`ScanProgressViewModel` tiene `CanCancel` y un `Cancel()` completo con su origen de cancelación. La
fila de progreso muestra el punto, el texto y el recuento — **y ningún botón**. Tampoco enseña en qué
carpeta va, que sí calcula. Y `ScanProgressCompleted` («El escaneo ha terminado.») no la lee nadie: la
fila desaparece sin más. El prototipo describe el escaneo como «incremental y **cancelable**».

### Construido, traducido, y sin dibujar

#### 5. Inicio calcula el resumen de la biblioteca y no lo enseña

`HomeViewModel` expone cuántas películas, cuántas series, **cuántos medios no disponibles**, y la
frase que los junta. Las seis cadenas están en los dos idiomas. `HomeView.axaml` tiene cuatro filas y
ninguna las consume. El número de no disponibles es justo el dato por el que alguien conecta un disco.

#### 6. Los carriles de Inicio siguen sin portada real — `LIB-018`

`IPosterCard.PosterFile` existe desde hoy y sólo lo rellena la biblioteca. Los tres carriles de
Inicio montan la misma tarjeta sin darle nada, así que la primera pantalla del programa sigue siendo
un muro de iniciales.

#### 7. La ficha de película no dice por dónde vas

`MovieDetailsViewModel` expone la posición de reanudación y su texto. `MovieDetailsView.axaml` no
tiene ninguna barra de progreso; la de serie y la de curso sí. El prototipo la dibuja en la cabecera.

#### 8. Volver de una ficha te deja al principio de la rejilla

`LibraryViewModel` declara `ScrollAnchorId` y lo escribe al abrir una ficha. **No lo lee nada**: sólo
dos pruebas. No hay ningún `ScrollIntoView` en el árbol. Con la paginación rota, el efecto se acumula.

#### 9. Al terminar un curso, el reproductor se cierra en silencio — `CRS-004`

`PlayerCourseFinishedNotice` («Curso terminado») no la lee nadie; el código que cierra la sesión
incluso escribe esa intención en un comentario. Y el aviso de encadenado reutiliza «Siguiente
episodio» para las lecciones teniendo «Siguiente lección» traducida y sin usar: un lector de pantalla
dice «episodio» en mitad de un curso.

#### 10. Los cursos no dicen cuándo los abriste por última vez — `CRS-003`

`CourseLastOpenedFormat` («Última vez hace {0}») no la lee nadie. El prototipo la pinta en la tarjeta
y en la cabecera de la ficha. Para un curso que se retoma a semanas vista, es el dato que ordena.

#### 11. Seis pares de cadenas huérfanas

Traducidas en los dos idiomas y sin consumidor, comprobadas contra las que se componen por
interpolación: «versiones» de un grupo de duplicados, «episodios» de una temporada, «temporada»,
«Episodio», «Inicio» y el sufijo de porcentaje.

#### 12. Dos órdenes del shell duplicadas, y tres nombres para lo mismo

`ToggleMiniPlayerCommand` y `ToggleFullscreenCommand` del shell no aparecen en ningún `.axaml`: los
botones reales usan los del reproductor. Y esa función se llama **«Ventana flotante»** en la barra de
transporte, **«Mini reproductor»** en Ajustes › Atajos, y hay un tercer par de cadenas traducidas sin
usar. El prototipo pone además esos dos botones en la cabecera del reproductor, donde aquí no están —
en la aplicación sólo viven en la barra que se desvanece.

### Lo que el prototipo dibuja y la aplicación no tiene

Esto sí es alcance, y por tanto necesita una decisión antes que código.

#### 13. Elegir entre las portadas que el proveedor mandó

`ArtworkPickerViewModel` tiene `SelectedRemoteUri` y un `CanApply` que **nadie invoca** — su propio
comentario ya lo dice. El editor sólo ofrece una caja de ruta y «Elegir una imagen…». El prototipo
dibuja una galería de cuatro opciones **y** el botón de archivo. Es el mismo defecto que la portada de
la rejilla, un piso más arriba.

#### 14. Deshacer en la bandeja de revisión, y «mantener pendiente»

La tarjeta ofrece aceptar, rechazar y buscar a mano. El prototipo ofrece cuatro para lo pendiente
—añade «mantener pendiente»— y **«deshacer» para lo ya resuelto**. Hoy, aceptar una coincidencia
equivocada es irreversible desde la pantalla que la propuso.

#### 15. Editar la ficha de un episodio suelto

El prototipo da a cada fila de episodio «Editar ficha» y «Reproducir»; la aplicación sólo el segundo.
Si el proveedor pone mal el título de un episodio, no hay ruta al editor desde la ficha de serie.

#### 16. Recorrer la cuadrícula con las flechas

El prototipo declara la rejilla como una cuadrícula navegable y lo **anuncia en Ajustes › Atajos**
como «Moverse por la cuadrícula ↑↓←→». La vista de biblioteca no tiene ni atajos ni manejo de teclas.

#### 17. El menú de filtros de Cursos — `CRS-007`

Sus tres cadenas están traducidas y sin consumidor. Ya tiene fila propia, `DESIGN_APPROVED`, así que
no es un hallazgo nuevo: es la confirmación de que sigue sin empezar.

#### 18. Los estados vacíos no llevan la acción que los saca

El vacío de la biblioteca tiene título y descripción y **ningún botón**, y su propio comentario dice
que el botón «llega con el diálogo de añadir raíz» — diálogo que ya existe y que la cabecera invoca.
Lo mismo en la tarjeta de bienvenida del shell. El prototipo pone ahí «Añadir carpeta» y «Escanear
ahora».

### Lo que se comprobó y NO es un hallazgo

Para que nadie repita la pasada: los códigos de identificación, los motivos de recomendación, los
hallazgos de restauración, los rechazos del actualizador y los tipos de marcador **sí** se dibujan —
se componen por interpolación y se traducen con el conversor de claves. Las estrellas de valoración
usan el código detrás en vez de una orden. Las vistas de Actualización y Privacidad están completas
contra su modelo.

### Estado medido el 2026-09-06, porque había tres cuentas y ninguna cuadraba

El relevo de la madrugada del 2026-09-05 decía «quedan quince», la hoja de ruta de esa misma noche
dijo «quedan doce», y un barrido del código daba trece. Las dos primeras se suceden en el tiempo y no
discrepan; lo que había que medir era el doce contra el trece. Medido hallazgo a hallazgo:

| # | Estado | Lo que lo dice, medido en el árbol |
| --- | --- | --- |
| 1 | **cerrado** | `LibraryView.axaml` enlaza `LoadMoreCommand` y `HasMore` |
| 2 | **cerrado** | `Settings/PlaybackSettingsView.axaml` existe |
| 3 | abierto | `Strings.es.axaml:669` sigue diciendo «N mini · Esc cierra» |
| 4 | **cerrado** | `LibraryView.axaml` enlaza `CancelScan` |
| 5 | abierto | `HomeView.axaml` no nombra ninguna de las seis claves del resumen |
| 6 | abierto | `PosterFile` no aparece en ninguna vista ni modelo de `Home/` |
| 7 | abierto | `Movie/MovieDetailsView.axaml` no tiene barra de progreso ni posición |
| 8 | abierto | `ScrollAnchorId` sólo vive en su ViewModel y dos pruebas; no hay `ScrollIntoView` en `src/` |
| 9 | abierto | `PlayerCourseFinishedNotice` y `PlayerNextLessonLabel`, sólo en los diccionarios |
| 10 | abierto | `CourseLastOpenedFormat`, sólo en los diccionarios |
| 11 | **cerrado** | con la puerta `OrphanedResourceTests` detrás |
| 12 | **cerrado** | las dos órdenes aparecen hoy en `MiniPlayerChromeView` y `TransportControlsView` |
| 13 | abierto | `SelectedRemoteUri` sólo vive en `ArtworkPickerViewModel`; gobernado por `ADR-0009` |
| 14 | abierto | con su medición escrita: tres cerrojos en el almacén |
| 15 | **cerrado, y su premisa era falsa** | ver abajo |
| 16 | abierto | `LibraryView` no tiene `KeyBinding` ni `KeyDown` |
| 17 | abierto | `CRS-007`, `DESIGN_APPROVED`, sin empezar |
| 18 | abierto | el bloque `LibraryEmptySurface` no contiene ningún `Button` |

**Seis cerrados y doce abiertos.** El doce coincide con el de la hoja de ruta **por un camino
distinto**, y eso importa más que el número: la hoja de ruta cuenta entre sus seis «las portadas en
la rejilla», que fue el detonante de esta auditoría y no uno de sus dieciocho, y no había visto que
el 15 estaba cerrado. Dos cuentas iguales no son una cuenta confirmada.

**El hallazgo 15 se cierra porque lo que afirma no es cierto.** Decía que «no hay ruta al editor
desde la ficha de serie», y la hay: `Show/ShowDetailsView.axaml:203` monta `TitleActionsView`, que
ofrece `EditMetadataCommand`, `PreviewRenameCommand` y `ReviewDuplicatesCommand`. Lo que queda en pie
—que el editor abre por **título** y no por episodio— no es un hallazgo, sino la diferencia ya
escrita con su razón en
[la comparación vista a vista](audit-prototype-fidelity-round-three.md): `CatalogMetadata` está
indexado por `TitleId`, así que un botón por fila diría una cosa y haría otra.

**Y ésa es la lección de esta reconciliación**, más útil que la cifra: este hallazgo se escribió
**tres días después** de esa razón y sin citarla, de modo que pedía exactamente el botón que la
comparación anterior había decidido no poner. Una auditoría que no cruza contra lo ya decidido
reabre lo cerrado, y cada vuelta lo vuelve a levantar.

---

## English

### Why this audit happened

On 2026-09-04 the library grid was found to **draw no cover at all**: the application downloaded
them, let somebody pick their own, stored them and backed them up, and the screen the whole library
is looked at through painted a gradient with initials. The owner then asked for a sweep of all sixty
views looking for more of the same.

**Pixels were not the subject.** Corner radii, the type scale and the icons already have their own
audits. What was looked for is functional: **what exists in the program that no screen shows**, and
**what the prototype draws that the application does not have**.

Eighteen came out. Two were checked by hand before this was written and both held.

### The house defect again, and now with its exact shape

The eighteen are one thing seen from different angles: **a layer finishes its work and the next one
does not pick it up**. The expensive case is not something left to build — it is something **built,
tested, translated into both languages, and called by nobody**.

### What breaks something already promised

These four are not new scope: they are rows the matrix calls good that the application does not meet.

#### 1. The library stops at title 50 — `LIB-004`

`LibraryViewModel` pages by cursor: `HasMore`, `LoadMoreCommand`, `LoadMoreAsync`, `PageSize: 50`.
**No view binds any of it.** `LibraryView.axaml` names neither `LoadMoreCommand` nor `HasMore`, and
its code-behind only handles size and clicks. `ReviewInboxView.axaml` **does** bind it for its inbox,
so the piece exists in this house and is missing here.

`LIB-004` is `VERIFIED` and promises «a 10,000-file library… navigation meets the budgets». With ten
thousand files, fifty are reachable.

**And its evidence explains how**: `T7-catalog-search.md` measures *the cursor query*, which works.
The chain was verified as far as the data layer and not as far as the screen — which is exactly the
lesson this repository already wrote on 2026-08-15: **a chain is verified as far as what the person
sees**.

Incidentally, `ItemCount` counts the loaded page, so the header will say «50 items» forever.

#### 2. The next-episode countdown cannot be configured — `PLY-011`

`PLY-011` is `VERIFIED` and its criterion says, literally, «**Cancelable, configurable**, and returns
to details when the next file is missing». `ContinuityCountdown` stores its duration in a preference,
and **zero switches the whole chain off**; it is read at playback, and the only thing that writes it
is the tests. That class's own comment claims «the settings surface already reads and writes» that
key. **That surface does not exist.**

The prototype puts it in a **«Playback»** section of Settings the application does not have, which
would also carry «Skip back/forward» and «Prefer external player».

#### 3. The player's shortcut line lies about two of six — `PLY-014`

`PlayerShortcutHint` says, in both languages, «… F fullscreen · **N** mini · **Esc closes**». Mini is
**Ctrl+P**, and `Escape` leaves overlay mode: it closes nothing. And it is a fixed string, so anybody
who reassigns a shortcut in Settings goes on reading the old one.

#### 4. Scanning can be cancelled from inside and not from outside — `LIB-002`

`ScanProgressViewModel` has `CanCancel` and a complete `Cancel()` with its cancellation source. The
progress row shows the dot, the text and the count — **and no button**. Nor does it show which folder
it is in, which it does compute. And `ScanProgressCompleted` («Scanning finished.») is read by
nobody: the row simply vanishes. The prototype describes scanning as «incremental and **cancelable**».

### Built, translated, and never drawn

#### 5. Home computes the library summary and does not show it

`HomeViewModel` exposes how many films, how many shows, **how many unavailable media**, and the
sentence joining them. All six strings exist in both languages. `HomeView.axaml` has four rows and
consumes none of them. The unavailable count is the very number somebody plugs a disk in over.

#### 6. Home's rails still have no real cover — `LIB-018`

`IPosterCard.PosterFile` exists as of today and only the library fills it. Home's three rails mount
the same card without giving it one, so the first screen of the program is still a wall of initials.

#### 7. The film card does not say how far in you are

`MovieDetailsViewModel` exposes the resume position and its text. `MovieDetailsView.axaml` has no
progress bar; the show and course cards do. The prototype draws one in the header.

#### 8. Coming back from a card leaves you at the top of the grid

`LibraryViewModel` declares `ScrollAnchorId` and writes it when a card opens. **Nothing reads it**:
only two tests. There is no `ScrollIntoView` anywhere in the tree. With paging broken, it compounds.

#### 9. Finishing a course closes the player in silence — `CRS-004`

`PlayerCourseFinishedNotice` («Course finished») is read by nobody; the code that closes the session
even writes that intention in a comment. And the chaining prompt reuses «Next episode» for lessons
while «Next lesson» sits translated and unused: a screen reader says «episode» in the middle of a
course.

#### 10. Courses do not say when you last opened them — `CRS-003`

`CourseLastOpenedFormat` («Last opened {0} ago») is read by nobody. The prototype paints it on the
card and in the card header. For a course picked up weeks later, it is the ordering fact.

#### 11. Six pairs of orphaned strings

Translated in both languages with no consumer, checked against the ones composed by interpolation:
a duplicate group's «versions», a season's «episodes», «season», «Episode», «Home», and the percent
suffix.

#### 12. Two duplicate shell commands, and three names for one thing

The shell's `ToggleMiniPlayerCommand` and `ToggleFullscreenCommand` appear in no `.axaml`: the real
buttons use the player's. And that function is called **«Floating window»** on the transport bar,
**«Mini player»** in Settings › Shortcuts, and a third translated pair sits unused. The prototype also
puts those two buttons in the player header, where the application has none — here they live only on
the bar that fades away.

### What the prototype draws and the application does not have

This is scope, and so it needs a decision before code.

#### 13. Choosing among the covers the provider sent

`ArtworkPickerViewModel` has `SelectedRemoteUri` and a `CanApply` **nobody invokes** — its own comment
says so. The editor offers only a path box and «Choose an image…». The prototype draws a gallery of
four options **and** the file button. It is the same defect as the grid's cover, one floor up.

#### 14. Undo in the review inbox, and «keep pending»

The card offers accept, reject and search manually. The prototype offers four for what is pending —
adding «keep pending» — and **«undo» for what is already settled**. Today, accepting a wrong match is
irreversible from the screen that proposed it.

#### 15. Editing a single episode's card

The prototype gives every episode row «Edit card» and «Play»; the application only the second. If the
provider gets an episode title wrong, there is no route to the editor from the show card.

#### 16. Walking the grid with the arrow keys

The prototype declares the grid navigable and **announces it in Settings › Shortcuts** as «Move
around the grid ↑↓←→». The library view has neither key bindings nor key handling.

#### 17. The Courses filter menu — `CRS-007`

Its three strings are translated with no consumer. It already has a row of its own, `DESIGN_APPROVED`,
so this is not a new finding: it is confirmation that it has not been started.

#### 18. The empty states carry no action out of them

The library's empty state has a title and a description and **no button**, and its own comment says
the button «arrives with the add-root dialog» — a dialog that already exists and that the header
invokes. The same in the shell's welcome card. The prototype puts «Add folder» and «Scan now» there.

### What was checked and is NOT a finding

So nobody repeats the pass: the identification codes, the recommendation reasons, the restore
findings, the updater's refusals and the marker kinds **are** drawn — they are composed by
interpolation and translated through the key converter. The rating stars use code-behind rather than
a command. The Update and Privacy views are complete against their models.

### Status measured on 2026-09-06, because there were three counts and none agreed

The handover from the early hours of 2026-09-05 said «fifteen left», the roadmap from that same night
said «twelve left», and a sweep of the code gave thirteen. The first two follow one another in time
and do not disagree; what had to be measured was the twelve against the thirteen. Measured finding by
finding:

| # | Status | What says so, measured in the tree |
| --- | --- | --- |
| 1 | **closed** | `LibraryView.axaml` binds `LoadMoreCommand` and `HasMore` |
| 2 | **closed** | `Settings/PlaybackSettingsView.axaml` exists |
| 3 | open | `Strings.es.axaml:669` still says «N mini · Esc cierra» |
| 4 | **closed** | `LibraryView.axaml` binds `CancelScan` |
| 5 | open | `HomeView.axaml` names none of the summary's six keys |
| 6 | open | `PosterFile` appears in no view or model under `Home/` |
| 7 | open | `Movie/MovieDetailsView.axaml` has no progress bar and no position |
| 8 | open | `ScrollAnchorId` lives only in its ViewModel and two tests; there is no `ScrollIntoView` in `src/` |
| 9 | open | `PlayerCourseFinishedNotice` and `PlayerNextLessonLabel`, only in the dictionaries |
| 10 | open | `CourseLastOpenedFormat`, only in the dictionaries |
| 11 | **closed** | with the `OrphanedResourceTests` gate behind it |
| 12 | **closed** | both commands appear today in `MiniPlayerChromeView` and `TransportControlsView` |
| 13 | open | `SelectedRemoteUri` lives only in `ArtworkPickerViewModel`; governed by `ADR-0009` |
| 14 | open | with its measurement written down: three locks in the store |
| 15 | **closed, and its premise was false** | see below |
| 16 | open | `LibraryView` has neither `KeyBinding` nor `KeyDown` |
| 17 | open | `CRS-007`, `DESIGN_APPROVED`, not started |
| 18 | open | the `LibraryEmptySurface` block contains no `Button` |

**Six closed and twelve open.** The twelve matches the roadmap's **by a different route**, and that
matters more than the number: the roadmap counts «the posters in the grid» among its six, which was
what triggered this audit and is not one of its eighteen, and it had not seen that 15 was closed. Two
counts that agree are not a confirmed count.

**Finding 15 closes because what it claims is not true.** It said there is «no route to the editor
from the show card», and there is: `Show/ShowDetailsView.axaml:203` mounts `TitleActionsView`, which
offers `EditMetadataCommand`, `PreviewRenameCommand` and `ReviewDuplicatesCommand`. What remains
standing — that the editor opens per **title** and not per episode — is not a finding but the
difference already written with its reason in
[the view-by-view comparison](audit-prototype-fidelity-round-three.md): `CatalogMetadata` is indexed
by `TitleId`, so a per-row button would say one thing and do another.

**And that is this reconciliation's lesson**, more useful than the figure: this finding was written
**three days after** that reason and without citing it, so it asked for exactly the button the
previous comparison had decided not to add. An audit that does not cross-check against what is
already decided reopens what was closed, and every round raises it again.
