# PLY-018 — Lo que le pasa de verdad a un vídeo que se ve mal / What is actually wrong with a video that looks bad

- Fecha / Date: 2026-09-12
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, ffmpeg 8.x.
- IDs: `PLY-018=IN_PROGRESS`
- Pruebas re-ejecutables / Re-runnable tests:
  `tests/ApSolutions.LocalMedia.Domain.Tests/Playback/PictureAdjustmentTests.cs`,
  `tests/ApSolutions.LocalMedia.MediaTests/Playback/PackedYuvConverterTests.cs`,
  `tests/ApSolutions.LocalMedia.MediaTests/Playback/PictureAdjustmentOnRealFramesTests.cs`

## Veredicto / Verdict

**La imagen no estaba borrosa: estaba aplastada en negro, y se estaba construyendo la pieza
equivocada.** `PLY-016` promete nitidez al ampliar, y el archivo que motivó la queja no tiene ese
defecto como el dominante. /
**The picture was not soft, it was crushed into black, and the wrong feature was being built.**

## Lo que se midió, y por qué cambió el plan

El propietario preguntó por qué el vídeo seguía viéndose mal y dio la ruta de un episodio suyo. Nada
de ese archivo entra en este repositorio —ni ruta ni título, que es la regla 5—, así que aquí van
sólo las cifras.

| Qué | Medido |
| --- | --- |
| Resolución | **720 × 404**, con píxeles no cuadrados (SAR 404:405, que distorsiona un 0,25 %: invisible) |
| Códec | **MPEG-4 Simple Profile**, de 2003 |
| Tasa de vídeo | **1,5 Mbit/s** |
| Audio | MP3 a 128 kbit/s |
| Los diez episodios | del mismo tamaño, así que es la copia entera y no un archivo suelto |

Y el dato que cambió la decisión, el brillo medio de cinco escenas del mismo archivo:

| Momento | Luma mínimo | **Luma medio** | Luma máximo |
| --- | --- | --- | --- |
| 00:03 | 13 | **116,7** | 227 |
| 00:12 | 11 | **43,3** | 232 |
| 00:33 | 10 | **69,1** | 209 |
| 00:47 | 13 | **28,5** | 161 |
| 01:02 | 13 | **34,2** | **97** |

Sobre 235, que es el blanco de rango limitado. **En la última escena no hay un solo píxel en toda la
pantalla más claro que 97.** Eso no es falta de detalle: es que la mitad de la imagen está por
debajo de donde un panel la distingue del negro.

**El rango es limitado y correcto** —el luma vive entre 10 y 232, nunca llega a 0 ni a 255—, así que
la aplicación no estaba oscureciendo nada: el archivo es así.

## La conclusión que costó el cambio de rumbo

**Ampliar con más nitidez no hace nada por una imagen que está a oscuras.** `PLY-016` estaba a medio
construir y su primer eslabón ya está cerrado y verificado; lo que faltaba era una fila que
prometiera que una persona puede hacer algo con una película oscura, y no existía. Ahora es
`PLY-018`.

**Y la aplicación no tenía ningún control de imagen**: buscando brillo, contraste, gamma y
saturación por todo `src/`, lo único que aparece es el tema de la interfaz y el estilo de los
subtítulos. Ni un ajuste para el vídeo.

## La aritmética, y por qué es la de ffmpeg

La comparación que el propietario miró y aprobó la produjo `ffmpeg -vf eq=gamma=1.6`. Cualquier otra
fórmula aquí sería una imagen distinta de la que se firmó, así que se reproduce la suya
—`libavfilter/vf_eq.c`, `LGPL-2.1-or-later`, compatible con la `GPL-3.0` de este árbol— con su
`gamma_weight` fijo en 1, que es su propio valor por defecto.

**Con los tres controles en su valor neutro la tabla es la identidad exacta**, y eso no es un
redondeo afortunado: es lo que permite que el valor por defecto no cueste ni cambie nada. La
comprobación está sobre los píxeles —la conversión de producción con y sin tabla tiene que dar el
mismo búfer— y no sobre la política.

## La guarda que parecía defensiva y cazaba un defecto visible

`v <= 0` se escribió por costumbre. Al quitarla para medir si alguien la vigilaba, con brillo −0,5 y
contraste 2 la tabla devolvió **255 en el nivel 127 y 1 en el 128**: toda la zona oscura de la
imagen saldría **blanca**. Un `double` negativo convertido a `byte` no da cero, da lo que quede tras
la vuelta; y con una gamma distinta de 1 da `NaN`, que **sí** convierte a cero y esconde el mismo
agujero en este runtime y no necesariamente en otro.

Las tres filas que ahora lo miden llevan la monotonía de la curva como aserción, que es lo que un
desbordamiento rompe.

**Y una de esas filas falló por la aserción y no por el código**: pedía que el nivel 255 siguiera por
encima de 200 con brillo −0,9, cuando a ese brillo la imagen entera es casi negra por definición y
el 255 aterriza en 25. El control correcto es que la tabla siga distinguiendo negro de blanco.

## Una trampa de la previsualización de cobertura, medida de paso

`eng/preview-coverage-floors.ps1` **no ve un archivo nuevo que no esté commiteado**: busca con
`git diff --diff-filter=A`, y un archivo sin seguimiento no aparece ahí. Su «no new file falls
short» era cierto y no significaba nada sobre el archivo recién escrito.

**Y al intentar comprobarlo a mano se llegó a un 78 % que tampoco era cierto**, por sumar las ramas
de dos informes sin fusionar. La puerta **fusiona primero con `reportgenerator`** y lee un solo
informe, donde cada línea se queda con la mejor lectura. Reproducido con la fusión de verdad:
`PictureAdjustment.cs` mide **100 % de líneas y 100 % de ramas (16 de 16)**, y los dos controles
—`UpscalePolicy` y `YuvMatrixPolicy`, que llevan tiempo pasando la puerta— miden lo mismo. Sin esos
dos controles, el 78 % se habría creído.

## Lo que queda

- El control en pantalla y la preferencia que lo recuerda.
- El juicio final sobre el valor por defecto, que es del propietario y se firma por el ojo.
