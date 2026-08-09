# Muestras multimedia de prueba

## Qué son y por qué no están en el repositorio

La matriz de contenedores y códecs se genera durante la ejecución de las pruebas a partir de los
generadores sintéticos de FFmpeg: el patrón de vídeo `testsrc2` y el tono `sine`. No interviene
ninguna grabación de terceros, ningún archivo de la biblioteca personal y ningún material
redistribuible con licencia propia.

El repositorio guarda **la receta**, nunca el archivo. Las muestras se materializan bajo
`artifacts/test-media/`, una ruta ignorada por Git, y `eng/verify.ps1` falla si alguna acaba en el
árbol de trabajo.

## Manifiesto

`tests/ApSolutions.LocalMedia.MediaTests/Fixtures/media-manifest.json` es la fuente de verdad. Cada
fila declara identificador, ruta relativa, contenedor, códec de vídeo y de audio, resolución,
duración, número de pistas, si es HDR, los codificadores que necesita, la receta exacta y el
resultado esperado: `Playable` o `ActionableUnsupported` con su código de fallo de dominio.

## Generar la matriz

```powershell
pwsh ./eng/generate-test-media.ps1 -Output artifacts/test-media
```

El script localiza el codificador por la variable de entorno `FFMPEG_PATH` y, si no está definida,
por `PATH`. Reutiliza las muestras que ya existen; `-Force` las regenera. Al terminar imprime el
tamaño y el SHA-256 de cada muestra, que es la procedencia verificable de esa ejecución.

## Codificadores necesarios

| Muestra | Codificadores |
|---|---|
| MP4 / H.264 / AAC | `libx264`, `aac` |
| Matroska / HEVC / E-AC-3 | `libx265`, `eac3` |
| AVI / MPEG-4 Parte 2 / MP3 | `mpeg4`, `libmp3lame` |
| QuickTime / H.264 / PCM S16LE | `libx264`, `pcm_s16le` |
| WebM / VP9 / Opus | `libvpx-vp9`, `libopus` |
| Matroska / AV1 / Opus | `libsvtav1`, `libopus` |
| Matroska / H.264 sin audio | `libx264` |
| Matroska / AVS2 no soportado | `libxavs2` |
| MP4 truncado | derivado de la primera fila |

Si el FFmpeg local no puede producir un codificador, la fila correspondiente **se omite con el
motivo exacto**. Nunca se sustituye por un equivalente ni se declara superada.

## Filas de error

Tres filas comprueban que un archivo que no se puede reproducir produce un diagnóstico accionable y
deja el archivo y la entidad del catálogo intactos:

- **AVS2 en Matroska**: códec real y estandarizado para el que la compilación fijada de LibVLC no
  tiene decodificador. El contenedor se analiza y la pista se anuncia, pero el motor no informa de
  ninguna descripción de códec y no decodifica ningún fotograma, así que se diagnostica
  `UnsupportedCodec`.
- **MP4 truncado**: los primeros 35 % de bytes de la primera fila, de modo que el índice final
  nunca llega. Se diagnostica `CorruptedMedia`.
- **Archivo ausente**: se diagnostica `FileNotFound` y se ofrece reintentar.

Ninguna acción de recuperación elimina ni reescribe el archivo.
