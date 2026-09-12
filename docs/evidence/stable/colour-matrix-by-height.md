# El color de todo vídeo HD / The colour of every HD video

- Fecha / Date: 2026-09-12
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, LibVLC 3.0.23.1,
  ffmpeg 2024-06-21 (`C:\ffmpeg\bin`).
- IDs: defecto, sin fila de alcance propia — corrige lo que `PLY-001` y `PLY-003` ya prometen.
- Pruebas re-ejecutables / Re-runnable tests:
  `tests/ApSolutions.LocalMedia.Domain.Tests/Playback/YuvMatrixPolicyTests.cs`,
  `tests/ApSolutions.LocalMedia.MediaTests/Playback/PackedYuvConverterTests.cs`,
  `tests/ApSolutions.LocalMedia.MediaTests/Playback/DecodedColourFidelityTests.cs`

## Veredicto / Verdict

**El defecto era real y está corregido.** Todo vídeo de 720 líneas para arriba se decodificaba con la
matriz de definición estándar, y el error es visible: el rojo puro llegaba a la pantalla como **231 en
vez de 253**, veintidós niveles por debajo. Ahora la matriz se elige por la altura del fotograma. /
**The defect was real and is fixed.**

Medido sobre los bytes que el decodificador entrega de cada muestra, leídos con
`ffmpeg -pix_fmt uyvy422` sobre los mismos ficheros que la suite genera:

| Muestra | Lo que el decodificador entrega | Antes (matriz fija BT.601) | Ahora |
| --- | --- | --- | --- |
| Rojo puro, 1280×720, BT.709 | `Y=62 U=102 V=239` | R=231 G=0 B=1 | **R=253 G=0 B=0** |
| Rojo puro, 720×576, BT.601 | `Y=81 U=90 V=239` | R=253 G=0 B=0 | R=253 G=0 B=0 |

**253 y no 255**: el viaje de ida y vuelta por croma de rango limitado cuesta dos niveles, y ffmpeg
decodificando el mismo fichero llega al mismo sitio. Lo que se corrige son los 22 que faltaban.

La fila de definición estándar es el control negativo: no cambia, y no cambiar es el resultado. El
fallo espejo —decodificar esa misma muestra con la matriz de alta definición— le mete **25 de verde**
a un rojo puro.

## Por qué la altura decide, y no lo que declara el fichero

`LibVlcVideoCapabilities.Describe` lee las pistas de LibVLC 3, y **la estructura de una pista de vídeo
no lleva espacio de color**: geometría, relación de aspecto, cadencia y orientación, nada más. Leerlo
del contenedor pediría un segundo decodificador dentro de la aplicación, y `ffprobe` —que es lo que la
suite de HDR usa— es una herramienta de pruebas y no está en la máquina de quien la usa.

Así que el criterio es el del sector: **720 líneas o más es BT.709, por debajo es BT.601**.
**Y no hay forma de pasarle un espacio**, que es una decisión y no un olvido. Preferir lo que declare
el origen es la respuesta correcta el día que algo del árbol pueda leerlo —la cadena de `PLY-016`
tendrá que decirle al procesador de vídeo qué le entrega—, y `YuvMatrixPolicy` es el único sitio que
cambiará ese día. Mientras nadie suministre ese valor, un parámetro para él es una puerta que nadie
cruza: el defecto característico de esta casa, no un adelanto. /
**LibVLC 3 does not expose the colour space at all, so the height decides, and there is deliberately
no parameter for a space nothing can supply yet.**

## Los coeficientes, y de dónde salen

Escalados por 256, sobre una imagen de rango limitado (luma menos 16, croma menos 128):

| | luma | R de V | G de U | G de V | B de U |
| --- | --- | --- | --- | --- | --- |
| BT.601 | 298 | 409 | 100 | 208 | 516 |
| BT.709 | 298 | **459** | **55** | **136** | **541** |

Barridos uno a uno contra la fórmula exacta en coma flotante sobre **todas** las combinaciones válidas
de su par de canales: ninguno se desvía más de **un nivel**, y cada uno es el de menor error medio
entre sus vecinos enteros — 459 da 0,0220 frente a 0,0931 de 458 y 0,1178 de 460; 541 da 0,0296 frente
a 0,0604 y 0,1094; el par (55, 136) da 0,1095 frente a 0,1273, 0,1274 y 0,1547 de los otros tres.

El luma es el mismo en las dos porque la ganancia de rango limitado no depende de la matriz.

## Lo que costó, y es la lección de la tanda

**La primera versión de esta medición dio por demostrado que LibVLC normaliza el color a BT.601, y era
falso.** El fichero de prueba se generó con `-colorspace bt709`, `ffprobe` contestó `color_space=bt709`,
y los dos ficheros —el de alta y el de definición estándar— llegaron al decodificador con **exactamente
los mismos bytes**, `Y=81 U=90 V=240`. La conclusión encajaba con los números.

**`-colorspace bt709` sólo escribe la etiqueta.** El filtro que genera el color produce YUV con la
matriz por defecto, que es BT.601, y ffmpeg no reconvierte nada: lo etiqueta y lo pasa. Leyendo el YUV
crudo de dentro del fichero —que es la comprobación que `ffprobe` **no** hace, porque lee la etiqueta—
apareció `Y=81 U=90 V=240` dentro de un fichero que decía BT.709.

Forzando la conversión de verdad, `-vf format=rgb24,scale=out_color_matrix=bt709:out_range=tv`, el
fichero pasó a llevar `Y=62 U=102 V=239` dentro, y LibVLC lo entregó tal cual. **LibVLC no normaliza
nada**; entrega el espacio del origen, y la matriz fija era el defecto que parecía.

**Una etiqueta no es una muestra**, y la guarda que lo comprueba está escrita:
`The_two_samples_really_carry_different_matrices_or_none_of_this_measures_anything` compara los bytes
que llegan de los dos ficheros y falla si coinciden. Mutada la receta a la forma que sólo etiqueta,
dice literalmente «the two samples reached the decoder with the same luma (81 and 81)». /
**A label is not a sample; the guard that checks it is in the suite and was measured failing.**

## Lo que la auditoría de puertas encontró, y que la primera versión no medía

`gate-auditor` encontró **dos puertas ciegas y una rama muerta**, cada una con su mutación medida:

- **El coeficiente de azul no lo medía nada.** Doblarlo dejaba `MediaTests` **165/165 en verde**, y el
  motivo es de libro: las diez filas eran o neutras —croma a 128, el término desaparece— o primarios
  saturados, donde el azul se va a 0 o a 255 con cualquier ganancia. **Un primario saturado no mide un
  coeficiente.** La salida son dos filas que no saturan **ningún** canal, buscadas midiendo: con ellas,
  doblar cualquiera de los cuatro coeficientes de croma mueve algún canal más que la tolerancia.
  Vuelto a mutar, el azul deja **4 pruebas en rojo**.
- **Elegir la matriz del búfer del decodificador en vez de la imagen pasaba entero.** Las dos muestras
  eran múltiplos de 16, así que las dos alturas coincidían y el caso no existía. La salida es una
  tercera muestra de **1280×716**, que H.264 codifica en un búfer de 720 y recorta: ahí las dos
  alturas caen a lados distintos del umbral. Vuelto a mutar, deja **1 prueba en rojo**.
- **La guarda de la matriz era una rama que nada podía tomar**, medida en el IL con coverlet: cero
  impactos. Y no era gratis — el suelo de ramas de ese fichero tenía **una sola rama de margen**. Se
  quitó: la matriz se escribe y se borra junto al búfer que la guarda de al lado ya comprueba.

**Y los números publicados estaban corridos uno o dos niveles** respecto a lo que el código produce.
Se volvieron a medir con `ffmpeg -pix_fmt uyvy422` sobre los ficheros reales y se corrigieron aquí, en
el registro de cambios y en los comentarios de las pruebas. **Un número falso viaja más rápido que uno
verdadero.**

## Controles / Controls

- **Negativo**: la muestra de definición estándar tiene que seguir saliendo correcta. Una política
  que devolviera BT.709 siempre la rompe — comprobado mutando `Choose`.
- **Positivo del instrumento**: los dos ficheros tienen que llegar al decodificador con bytes
  distintos, o ninguna de las otras pruebas mide nada.
- **Mutación, con el binario comprobado**: devolver la matriz de BT.601 desde la de BT.709 deja
  2 pruebas en rojo; subir el umbral de alta definición a 1081, 4; volver el motor a la matriz fija,
  2; doblar el coeficiente de azul, 4; y elegir la matriz del búfer del decodificador, 1. En cada una
  se comparó la huella del ensamblado con la anterior antes de creerse el veredicto — la trampa que el
  mismo día dio tres mutaciones con idéntico resultado porque las tres midieron el binario de la
  primera.

## Reproducir / Reproduce

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet test tests/ApSolutions.LocalMedia.Domain.Tests -c Release -m:1 `
  --settings eng/test.runsettings --filter "FullyQualifiedName~YuvMatrixPolicy"
dotnet test tests/ApSolutions.LocalMedia.MediaTests -c Release -m:1 `
  --settings eng/test.runsettings --filter "FullyQualifiedName~DecodedColourFidelity"
```

Las muestras se generan solas bajo `artifacts/test-media/COLOUR/`, y la suite se salta entera si no
hay codificador. / The samples generate themselves; the suite skips without an encoder.

## Procedencia / Provenance

- Coeficientes exactos: Rec. ITU-R BT.709-6 y BT.601-7 (`Kr`/`Kb` de cada una), con las ganancias de
  rango limitado 255/219 y 255/224.
- Patrón oro de la comparación: ffmpeg decodificando las mismas muestras, no la aritmética de este
  repositorio — para que el número venga de fuera y no coincida consigo mismo.
