# ADR-0009 — Una portada tiene tres orígenes y un orden / A Cover Has Three Origins and an Order

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-09-05
- Decisor / Decision owner: Product Owner
- Relacionado / Related: [`LIB-011`](../FEATURES.md), [`LIB-018`](../FEATURES.md),
  [`CRS-006`](../FEATURES.md), [`PRD-006`](../FEATURES.md),
  [la portada que se guardaba y nadie veía](../evidence/stable/audit-personal-cover-never-drawn.md),
  [lo construido que ninguna pantalla enseña](../evidence/stable/audit-built-and-not-drawn.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben
actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be
updated together.

---

## Español

### Contexto

Una portada puede venir de tres sitios y hoy el catálogo sólo sabe de uno. `catalog_metadata` guarda
**un campo**, `poster_path`, que a veces contiene una dirección del proveedor y a veces el nombre de
un archivo que alguien eligió de su disco; lo único que los separa es una casilla de bloqueo. Las
imágenes sí viven separadas —`cache/artwork`, `personal-artwork` y `cache/course-thumbnails`—, así
que la mezcla no está en el disco: está en el registro.

**Eso ya tiene una consecuencia medida.** Si alguien quita el candado y se refresca contra el
proveedor, su portada se sustituye y el archivo se queda huérfano, sin nada que lo nombre, viajando
dentro de cada copia de seguridad para siempre. Está escrito en la evidencia de `LIB-018` desde el
2026-09-04, con el arreglo nombrado: una columna aparte con su migración.

**Y el tercer origen no existe para películas ni series.** El fotograma sacado del vídeo se
implementó sólo para cursos, por una vía completamente separada que no toca `poster_path`. El
capturador, sin embargo, no es de cursos: recibe un vídeo, un momento y un destino.

**La prioridad actual no la fija nadie.** La decide una línea del arranque —el proveedor primero, la
propia después— que hasta el 2026-09-04 no tenía una sola prueba. Y el prototipo no dibuja un
candado: dibuja **una galería de cuatro opciones** con «Elegir archivo…» debajo.

### Decisión

**Una portada tiene tres orígenes con un orden, y ese orden se puede cambiar.**

1. **Tres orígenes separados en el registro**, cada uno con su columna: la elegida a mano, la del
   proveedor y el fotograma sacado del vídeo. La columna aparte es lo que impide que refrescar
   destruya una elección, que es el defecto que esta decisión cierra.
2. **Un orden por defecto**: la elegida a mano gana; si no hay, la del proveedor; si tampoco, el
   fotograma.
3. **El fotograma se saca solo cuando no hay ninguna otra**, sin preguntar, y se guarda. Sin esto,
   una biblioteca de carpetas sin identificar es una rejilla de degradados.
4. **La elección se puede cambiar en dos sitios**: un ajuste general que fija el orden, y la
   posibilidad de saltárselo en un título concreto desde su ficha, con la galería que el prototipo
   dibuja.
5. **Vale para película, serie y curso.** El capturador ya sirve para cualquier vídeo; lo que es de
   cursos es sólo el envoltorio.

### Por qué así, y no de otra manera

**No es una invención de esta casa.** Jellyfin —la aplicación libre de referencia del sector—
resuelve esto con exactamente esta forma: distingue proveedores de imagen **local**, **remoto** y
**dinámico** —«genera o extrae imágenes bajo demanda, por ejemplo de fotogramas de vídeo»— y los
ordena con una preferencia configurable más un bloqueo por elemento. Consultado en su documentación
antes de decidir, que es la regla 0 de este repositorio.

**Los dos mecanismos no son redundancia.** El ajuste general evita tener que decidir título a título
en una biblioteca de diez mil; la excepción por título evita tener que aceptar una portada concreta
que no gusta. Quitar cualquiera de los dos deja un caso real sin respuesta.

### Consecuencias

- **Hay migración**, y es la número 23. El esquema es `STRICT` y el ejecutor comprueba el SHA-256 de
  cada migración aplicada, así que no es un cambio gratuito.
- **La decisión de qué imagen se dibuja baja a una política con pruebas.** Ya empezó: el 2026-09-04
  salió del arranque a `ResolveTitlePoster`, con once pruebas donde antes no había ninguna.
- **El editor de fichas cambia de forma**: hoy es una caja de texto con la ruta, un botón y una
  casilla; pasa a ser la galería del prototipo. La caja se conserva, porque `LIB-011` la verificó y
  hay bibliotecas que dependen de ella.
- **La aplicación abrirá vídeos por su cuenta** para sacar el fotograma. Es un ensanche del
  componente que este repositorio nombra como su mayor riesgo residual, y se acepta con su límite:
  sólo archivos que ya están en la biblioteca declarada, una vez por título, y el resultado se
  guarda. El spike de `CRS-006` midió que un archivo ilegible tarda 4,5 s en rendirse, así que la
  llamada necesita su propio plazo.
- **`CRS-006` deja de ser una fila de cursos** y pasa a ser el tercer origen de esta decisión.

---

## English

### Context

A cover can come from three places and the catalogue only knows about one. `catalog_metadata` stores
**a single field**, `poster_path`, sometimes holding a provider address and sometimes the name of a
file somebody picked off their disk; the only thing separating them is a lock checkbox. The images
themselves do live apart — `cache/artwork`, `personal-artwork` and `cache/course-thumbnails` — so the
mixing is not on the disk: it is in the record.

**That already has a measured consequence.** If somebody unlocks the field and a provider refresh
runs, their cover is replaced and the file is left orphaned, with nothing naming it, travelling
inside every backup forever. It has been written in `LIB-018`'s evidence since 2026-09-04, with the
fix named: a column of its own with a migration.

**And the third origin does not exist for films or shows.** The frame taken from the video was built
for courses only, by a wholly separate path that never touches `poster_path`. The grabber, however,
is not course-specific: it takes a video, a moment and a destination.

**Nobody pins the current priority down.** It is decided by one line of the start-up — provider
first, personal second — which until 2026-09-04 had not a single test. And the prototype draws no
lock: it draws **a gallery of four options** with «Choose file…» beneath.

### Decision

**A cover has three origins with an order, and that order can be changed.**

1. **Three origins kept apart in the record**, each with its own column: the hand-picked one, the
   provider's, and the frame taken from the video. The separate column is what stops a refresh from
   destroying a choice, which is the defect this decision closes.
2. **A default order**: the hand-picked one wins; failing that, the provider's; failing that, the
   frame.
3. **The frame is taken automatically when there is no other**, without asking, and stored. Without
   this, a library of unidentified folders is a grid of gradients.
4. **The choice can be changed in two places**: a general setting fixing the order, and the option to
   override it on one title from its card, with the gallery the prototype draws.
5. **It applies to films, shows and courses.** The grabber already works for any video; what is
   course-specific is only the wrapper.

### Why this shape and not another

**It is not a house invention.** Jellyfin — the sector's reference free application — solves this in
exactly this shape: it distinguishes **local**, **remote** and **dynamic** image providers —
«generates or extracts images on demand, e.g. from video frames» — and orders them with a
configurable preference plus a per-item lock. Consulted in its documentation before deciding, which
is this repository's rule 0.

**The two mechanisms are not redundancy.** The general setting saves deciding title by title across
ten thousand; the per-title override saves having to accept one particular cover somebody dislikes.
Removing either leaves a real case unanswered.

### Consequences

- **There is a migration**, and it is number 23. The schema is `STRICT` and the runner checks each
  applied migration's SHA-256, so it is not a free change.
- **The decision about which picture is drawn moves down into a tested policy.** It has already
  started: on 2026-09-04 it left the start-up for `ResolveTitlePoster`, with eleven tests where there
  had been none.
- **The metadata editor changes shape**: today a path box, a button and a checkbox; it becomes the
  prototype's gallery. The box stays, because `LIB-011` verified it and there are libraries that rely
  on it.
- **The application will open videos on its own** to take the frame. That widens the component this
  repository names as its largest residual risk, and it is accepted with its limit: only files
  already in the declared library, once per title, and the result is stored. `CRS-006`'s spike
  measured that an unreadable file takes 4,5 s to give up, so the call needs a deadline of its own.
- **`CRS-006` stops being a courses row** and becomes the third origin of this decision.
