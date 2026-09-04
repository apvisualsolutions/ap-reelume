# La portada propia que se guardaba y no se dibujaba / The personal cover that was stored and never drawn

Medición de por qué una portada elegida con el botón de `LIB-018` quedaba escrita, bloqueada y
respaldada sin que ninguna pantalla la enseñara jamás, y de lo que costó cerrarlo. / A measurement of
why a cover chosen with `LIB-018`'s button was written, locked and backed up without any surface ever
showing it, and of what closing that cost.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-09-04.

Motivo: el relevo proponía subir `LIB-011` a `VERIFIED` porque el botón de elegir portada ya existía
y su cerradura tenía prueba. La medición dice que el botón funciona entero y que la imagen no llega a
la pantalla, así que la fila no sube y el defecto se arregla. / Reason: the handover proposed raising
`LIB-011` to `VERIFIED` because the cover button now existed and its lock had a test. The measurement
says the button works end to end and the picture never reaches the screen, so the row does not rise
and the defect is fixed instead.

## 1 · Dos muros, no uno / Two walls, not one

El diagnóstico inicial nombraba un solo obstáculo. Hay dos, y arreglar sólo el primero habría dejado
el defecto igual de invisible. / The initial diagnosis named one obstacle. There are two, and fixing
only the first would have left the defect exactly as invisible.

**Primero, la forma.** `FindCachedPoster` preguntaba únicamente «¿es esto una ruta del proveedor?», y
`PosterAddressPolicy` exige barra inicial y sólo `[A-Za-z0-9_-]` con un punto. Medido ejecutando la
política: / **First, the shape.** `FindCachedPoster` asked only «is this a provider path?», and
`PosterAddressPolicy` requires a leading slash and only `[A-Za-z0-9_-]` with one dot. Measured by
running the policy:

```
NULO    <- C:\Users\alguien\AppData\Local\ApReelume\personal-artwork\3f2b\cover.png
NULO    <- C:/Users/alguien/AppData/Local/ApReelume/personal-artwork/3f2b/cover.png
DIRECC. <- /wXsQvli6tWqja51pYxXNG1LFIGV.jpg
              => https://image.tmdb.org/t/p/w780/wXsQvli6tWqja51pYxXNG1LFIGV.jpg
NULO    <- /personal-artwork/3f2b/cover.png
```

**Segundo, el sitio.** Aunque la dirección se hubiera construido, `ArtworkCache.Find` sólo mira dentro
de `cache/artwork/<id>`, y el selector copia a `personal-artwork/<id>`. Son dos carpetas distintas: la
búsqueda habría estado bien formada y en el lugar equivocado. / **Second, the place.** Even had the
address been composed, `ArtworkCache.Find` only looks inside `cache/artwork/<id>`, while the picker
copies into `personal-artwork/<id>`. Two different folders: the lookup would have been well formed
and in the wrong place.

## 2 · Lo que NO hacía falta tocar / What did NOT need touching

Ninguna vista y ningún conversor. `CachedPosterConverter` acepta cualquier cadena no vacía y se la
pasa al decodificador; su propia prueba ya lo demuestra con una ruta absoluta de la carpeta temporal,
que es exactamente la forma que tiene una ruta de `personal-artwork`. El bloqueo entero vivía en una
función de doce líneas del anfitrión. / No view and no converter. `CachedPosterConverter` accepts any
non-empty string and hands it to the decoder; its own test already proves this with an absolute path
in the temp folder, which is exactly the shape a `personal-artwork` path has. The whole block lived in
a twelve-line function of the host.

## 3 · El tercer defecto, que nadie había nombrado / The third defect, which nobody had named

Cerrar el editor **suelta las dos superficies y no recarga nada**, así que la ficha que reaparece
detrás es el mismo modelo que se rellenó al abrir el título. Aunque la portada se hubiera resuelto,
habría hecho falta salir del título y volver a entrar para verla — y el mensaje en pantalla dice
«Portada puesta» en el instante en que todavía no ha pasado. / Closing the editor **drops both
surfaces and reloads nothing**, so the card that reappears behind is the same model that was filled
when the title was opened. Even with the cover resolved, somebody would have had to leave the title
and come back — while the message on screen says «Portada puesta» at the moment it has not yet
happened.

## 4 · El cuarto, que se arregla de paso: restaurar en otro ordenador / The fourth, fixed along the way: restoring on another machine

La copia de seguridad guarda la imagen **por su sitio relativo** y la restauración la recompone bajo
la carpeta de datos de la máquina nueva; la ficha, en cambio, guarda la ruta **absoluta**. Restaurar
en otro ordenador —o con otra cuenta de Windows— dejaría la imagen en el disco y la ficha señalando a
donde ya no está. / The backup stores the image **by its relative place** and the restore recomposes
it under the new machine's data folder; the row, however, stores the **absolute** path. Restoring on
another machine — or under another Windows account — would leave the image on disk and the row
pointing where it no longer is.

**El arreglo lo cierra sin cambiar lo guardado**: del valor almacenado se toma sólo el nombre del
archivo y la carpeta se compone aquí, con la carpeta de datos de esta máquina y el título que se está
dibujando. Probado con dos raíces de datos distintas. / **The fix closes this without changing what is
stored**: only the file name is taken from the stored value and the folder is composed here, from
this machine's data folder and the title being drawn. Tested across two different data roots.

## 5 · La frontera, y por qué es un alfabeto y no un filtro / The boundary, and why it is an alphabet and not a filter

El campo es texto libre: una persona escribe en él, un proveedor puede escribirlo mientras no esté el
candado, y una copia restaurada puede traer uno escrito en la máquina de otro. Componer una ruta con
él convertiría una caja de texto en un lector de archivos cualquiera. / The field is free text: a
person types into it, a provider can write it while the lock is off, and a restored backup can carry
one written on somebody else's machine. Composing a path out of it would turn a text box into a
reader of arbitrary files.

Lo aceptado es el nombre que el propio almacén escribe: **64 hexadecimales en minúscula y uno de los
cuatro contenedores aprobados**. En ese alfabeto no se puede escribir un separador, los dos puntos de
una unidad, los dos puntos de un flujo alternativo, el par de puntos de una escalada, el prefijo de un
recurso de red ni un nombre de dispositivo reservado. No se rechazan uno a uno —eso es una lista que
alguien tiene que mantener completa—: **son inescribibles**. / What is accepted is the name the store
itself writes: **64 lower-case hexadecimal characters and one of the four approved containers**. That
alphabet cannot spell a separator, a drive's colon, an alternate stream's colon, a climb's pair of
dots, a network share's prefix or a reserved device name. They are not refused one by one — that is a
list somebody has to keep complete — they are **unspellable**.

**Lo que no defiende, dicho en voz alta**: una unión de directorio plantada dentro de la propia
carpeta de datos redirigiría una ruta compuesta enteramente con piezas de confianza. Quien pueda
plantarla ya puede reescribir los bytes de la imagen, así que la guarda no compraría nada. Se nombra
para que el próximo lector sepa que se midió y no que se pasó por alto. / **What it does not defend
against, said out loud**: a directory junction planted inside the application's own data folder would
redirect a path composed entirely of trusted pieces. Whoever can plant one can already rewrite the
image's bytes, so the guard would buy nothing. It is named here so the next reader knows it was
measured and not missed.

## 6 · El defecto que el arreglo habría creado / The defect the fix would have created

Hasta ahora toda portada venía del proveedor y era `w780` por construcción, así que el presupuesto de
memoria del conversor —3,5 MB por entrada, ocho entradas— se cumplía por suerte y no por regla. Una
portada elegida del disco propio es lo que diera la cámara: diez megas de JPEG son decenas de millones
de píxeles. / Until now every poster came from the provider and was `w780` by construction, so the
converter's memory budget — 3.5 MB per entry, eight entries — held by luck rather than by rule. A
cover chosen off one's own disk is whatever the camera produced: ten megabytes of JPEG is tens of
millions of pixels.

Medido sobre Avalonia 12.1.1, con imágenes reales escritas por un codificador real: / Measured on
Avalonia 12.1.1, with real images written by a real encoder:

```
origen 300x450   -> sin tope 300x450   | acotado a 780  780x1170
origen 2000x3000 -> sin tope 2000x3000 | acotado a 780  780x1170
```

**El tope agranda una imagen pequeña, y eso se escribe en vez de esconderse.** Una portada de 300×450
pasa a costar 3,65 MB donde el archivo habría costado 0,5; una de 2000×3000 baja de 24 MB a 3,65. La
cesión es un coste fijo por entrada en lugar de uno que nadie puede predecir, que es justo el
presupuesto alrededor del cual esa clase ya estaba escrita. / **The bound enlarges a small image, and
that is written down rather than hidden.** A 300×450 cover comes to cost 3.65 MB where the file would
have cost 0.5; a 2000×3000 one drops from 24 MB to 3.65. The cession is a fixed cost per entry instead
of one nobody can predict, which is exactly the budget that class was already written around.

## 7 · Dos cosas que la documentación de Avalonia no contesta bien / Two things the Avalonia docs get wrong

**La firma.** La documentación presenta `DecodeToWidth` como método de instancia. Medido por
reflexión sobre el ensamblado real: / **The signature.** The documentation presents `DecodeToWidth` as
an instance method. Measured by reflection against the real assembly:

```
Avalonia: 12.1.1.0
static Bitmap DecodeToWidth(Stream stream, Int32 width, BitmapInterpolationMode = HighQuality)
static Bitmap DecodeToHeight(Stream stream, Int32 height, BitmapInterpolationMode = HighQuality)
```

**Y el fallo.** Ante un archivo que no es una imagen, el constructor entero lanza
`ArgumentException` —«Unable to load bitmap from provided data»— mientras que `DecodeToWidth` lanza
una `NullReferenceException` desde dentro de `Avalonia.Skia.ImmutableBitmap`, para el mismo archivo.
Se descubrió porque una prueba verde se puso roja al cambiar de una a otra, y no razonando sobre la
API. / **And the failure.** Faced with a file that is not an image, the whole-file constructor throws
`ArgumentException` — «Unable to load bitmap from provided data» — while `DecodeToWidth` throws a
`NullReferenceException` from inside `Avalonia.Skia.ImmutableBitmap`, for the very same file. It was
found because a green test went red on switching from one to the other, not by reasoning about the
API.

## 8 · Lo que queda abierto, y es decisión del propietario / What is still open, and is the owner's call

**Un campo guarda dos cosas y sólo las separa una casilla.** Mientras la portada propia no se dibujaba
esto estaba tapado. Ahora que se dibuja, la secuencia es concreta: alguien quita el candado por
cualquier motivo, se refresca contra el proveedor, y su portada se sustituye — el archivo se queda
huérfano en el disco, sin nada que lo nombre, viajando dentro de cada copia de seguridad para siempre.
El arreglo honesto es una columna aparte con su migración, y es una tanda entera. / **One field holds
two things and only a checkbox separates them.** While the personal cover was never drawn this stayed
covered. Now that it is drawn, the sequence is concrete: somebody unlocks the field for whatever
reason, a refresh runs against the provider, and their cover is replaced — the file stays orphaned on
disk, with nothing naming it, travelling inside every backup forever. The honest fix is a column of
its own with a migration, and that is a batch in itself.

**Y los cursos siguen sin tener por dónde.** `LIB-018` promete la portada de «esa película, serie o
curso»; la ficha de un curso ofrece un solo botón —reanudar— y su imagen es un fotograma que la
aplicación saca sola del vídeo. El tercio de los cursos no existe. / **And courses still have no way
in.** `LIB-018` promises the cover of «that film, series or course»; a course's card offers a single
button — resume — and its picture is a frame the application takes from the video itself. The third of
that promise does not exist.
