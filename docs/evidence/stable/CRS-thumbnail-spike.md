# Cursos — Spike de miniatura desde el vídeo / Courses — thumbnail-from-video spike

- Fecha / Date: 2026-09-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `802d264`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, LibVLCSharp 3.10.0,
  LibVLC 3.0.23.1
- IDs: ninguno — no está en la matriz de alcance / none — not in the scope matrix

## La pregunta / The question

El prototipo abre cada tarjeta de curso con una imagen 16:9 rellena de un degradado generado,
porque no puede distribuir carátulas. Un curso se detecta de la carpeta y **nunca se consulta a un
proveedor**, así que en esta aplicación ese panel sería un marcador de posición para siempre, a
menos que la imagen saliera del propio vídeo. La pregunta del propietario fue exactamente ésa:
¿es posible capturar un fotograma? / The prototype opens every course card with a 16:9 panel filled
with a generated gradient, because it cannot ship artwork. A course is detected from a folder and
**never looked up**, so in this application that panel would be a placeholder for ever unless the
picture came from the video itself. The owner's question was exactly that: can a frame be captured?

## Veredicto / Verdict

**Sí, y la hipótesis que se tenía antes de medir era falsa.** Se esperaba que la ruta por callbacks
—la que esta aplicación usa para pintar vídeo— lo impidiera, porque `PLY-016` midió que los filtros
de vídeo de VLC 3 **nunca procesan un fotograma** por esa misma ruta. No se transfiere: la captura
funciona por las tres vías probadas, y la más barata entrega el fotograma **en 137 ms**. / **Yes,
and the hypothesis held before measuring was false.** The callback path — the one this application
draws video through — was expected to block it, because `PLY-016` measured that VLC 3's video
filters **never process a frame** on that same path. It does not carry over: capture works by all
three routes tried, and the cheapest hands over the frame **in 137 ms**.

**Lo que decide no es si se puede, sino el precio**, y está medido abajo: **unos 460 ms por
archivo** con salto de posición, cinco archivos en secuencia **sin que el proceso caiga**. / **What
decides is not whether it can be done but what it costs**, measured below: **about 460 ms per file**
with a seek, five files in sequence **without the process falling over**.

## Método / Method

`ThumbnailSpike` (MediaTests, decodificación real) sobre las muestras sintéticas que el propio
repositorio genera con FFmpeg. Cuatro vías. / `ThumbnailSpike` (MediaTests, real decoding) over the
synthetic samples this repository generates with FFmpeg. Four routes.

| Vía / Route | Qué hace / What it does | Resultado / Result |
| --- | --- | --- |
| A | `TakeSnapshot` sin salida de vídeo asignada / with no video output attached | PNG de 113.677 B en 2.293 ms |
| B | `TakeSnapshot` con la salida por callbacks, la de esta aplicación / on the callback output, this application's | PNG de 97.127 B en 2.283 ms, 54 fotogramas vistos, 720×480 |
| C | Quedarse el primer fotograma que el callback entrega / keep the first frame the callback hands over | 1.382.400 B en crudo a 720×480, **primer fotograma a los 137 ms**, 345.600 píxeles opacos |
| D | Saltar al 10 % y quedarse el fotograma, sobre cinco archivos en secuencia / seek to 10 % and keep the frame, over five files in sequence | **4 de 5**, ~460 ms por archivo, proceso vivo |

Las vías A y B incluyen **1.500 ms de espera fija del arnés** antes de pedir la captura, así que sus
cifras no son el coste de `TakeSnapshot`: son el coste del arnés. La vía C y la D miden el tiempo
hasta el fotograma. / Routes A and B include **1,500 ms of fixed wait in the harness** before asking
for the snapshot, so their figures are not `TakeSnapshot`'s cost — they are the harness's. Routes C
and D measure time to the frame.

### La vía D, archivo por archivo / Route D, file by file

| Archivo / File | Duración / Length | Fotograma / Frame | Tras el salto / After the seek |
| --- | --- | --- | --- |
| `mpeg2-480p-noisy.mkv` | 6.000 ms | 720×480 | 433 ms |
| `silence.mkv` | 8.046 ms | 320×192 | 469 ms |
| `mkv-av1-opus.mkv` | 3.008 ms | 384×256 | 433 ms |
| `mkv-avs2-unsupported.mkv` | 3.000 ms | **ninguno / none** | — (4.519 ms hasta rendirse / to give up) |
| `mkv-h264-no-audio.mkv` | 3.000 ms | 320×258 | 472 ms |

**El que falla no es un fallo**: `mkv-avs2-unsupported.mkv` es la muestra que la matriz de códecs ya
trata como no soportada. Sin decodificador no hay imagen, que es la respuesta correcta — pero cuesta
**4,5 s en rendirse**, y ese techo lo puso el arnés, no LibVLC. Cualquier implementación necesita un
plazo propio o un archivo ilegible bloquea la rejilla. / **The failure is not a failure**:
`mkv-avs2-unsupported.mkv` is the sample the codec matrix already treats as unsupported. No decoder
means no picture, which is the right answer — but it costs **4.5 s to give up**, and that ceiling was
the harness's rather than LibVLC's. Any implementation needs a deadline of its own or one unreadable
file stalls the grid.

## Lo que esta medición NO dice / What this measurement does NOT say

- **Las muestras son sintéticas, cortas y pequeñas**: de 3 a 8 segundos, de 320×192 a 720×480. Una
  lección real de cuarenta minutos a 1080p no está medida, y el salto a un punto de un archivo largo
  es justo donde el coste puede crecer. / **The samples are synthetic, short and small** — 3 to 8
  seconds, 320×192 to 720×480. A real forty-minute 1080p lesson is not measured, and seeking inside a
  long file is exactly where the cost can grow.
- **Cinco archivos no son una biblioteca.** El modo de fallo nativo que este repositorio ya conoce
  —abrir y liberar medios en secuencia— no apareció en cinco; no está descartado en cincuenta. /
  **Five files are not a library.** The native failure mode this repository already knows — opening
  and releasing media in sequence — did not appear over five; it is not ruled out over fifty.
- **No mide el guardado.** La vía C y la D entregan **1,38 MB de píxeles en crudo** por fotograma.
  Codificarlos, guardarlos y saber cuándo caducan es trabajo que no está medido aquí. / **It does not
  measure storage.** Routes C and D hand over **1.38 MB of raw pixels** per frame. Encoding, storing
  and knowing when they go stale is work not measured here.
- **No mide qué se ve.** Un fotograma al 10 % de una lección puede ser una diapositiva en blanco o
  una cara a media palabra. Que sea una miniatura *útil* es una pregunta distinta de que exista. /
  **It does not measure what you see.** A frame at 10 % of a lesson can be a blank slide or a face
  mid-word. Whether it is a *useful* thumbnail is a different question from whether it exists.

## Lo que esto le costaría a la aplicación / What this would cost the application

Nombrado y no medido, porque es una decisión antes que una cifra: hoy **sólo se decodifica el vídeo
que alguien decide ver**. Sacar miniaturas hace que la aplicación abra archivos por su cuenta, lo que
ensancha la superficie del componente que este repositorio ya nombra como su mayor riesgo residual.
Es una decisión del propietario y no un detalle de implementación. / Named rather than measured,
because it is a decision before it is a figure: today **only the video somebody chooses to watch is
decoded**. Thumbnails would have the application open files on its own, which widens the surface of
the component this repository already names as its biggest residual risk. That is the owner's
decision and not an implementation detail.

## Reproducir / Reproduce

El arnés no queda en el árbol: es un spike, y una prueba de quince segundos sin puerta detrás es
coste por vuelta de CI a cambio de nada. Se reconstruye desde esta página. / The harness does not stay
in the tree: it is a spike, and a fifteen-second test with no gate behind it is CI cost per round for
nothing. It is rebuilt from this page.
