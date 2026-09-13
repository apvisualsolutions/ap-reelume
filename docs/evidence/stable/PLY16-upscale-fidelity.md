<!-- SPDX-FileCopyrightText: 2026 AP Solutions -->
<!-- SPDX-License-Identifier: LicenseRef-APSolutions -->

# PLY-016 — la nitidez medida contra una verdad, y por qué la medida anterior premiaba lo contrario

**Fecha:** 2026-09-13
**Alcance:** `PLY-016`, el eslabón portátil ya dibujando
**Origen:** el propietario, dos veces el mismo día: «se nota poco, algo mejoró pero sigue medio
borroso» y después «¿y la nitidez?»

## El hallazgo, que es del instrumento y no del shader

El eslabón portátil se validó el 2026-09-13 con **una sola medida**: la anchura de la rampa de un
canto duro negro-a-blanco ampliado cuatro veces — cuántos píxeles vuelven ni negros ni blancos.
Menos es mejor, así que **su puntuación perfecta es cero**. Y cero es exactamente lo que da el vecino
más cercano, como afirma literalmente la prueba de al lado
(`Turning_interpolation_off_gives_the_hard_step_that_proves_the_others_are_smoothing`).

**Una medida cuyo mejor valor pertenece al peor filtro no se puede optimizar.** Empujar hacia ella
empuja hacia un umbral duro: puntúa de maravilla y dibuja escaleras. Medido el mismo día: un
escalador de remapeo local, escrito para ganar en esa medida, dio rampa 4 —igual que el bilineal— y
además rompió la guarda del rebase. La conclusión no fue «el escalador es malo»: fue que **la medida
no distingue nítido de crudo**.

## Lo que la documentación contestó antes de razonar

- **AMD, sobre su afilado adaptativo al contraste (CAS)**, en su propia página: «The algorithm adjusts
  the amount of sharpening per pixel to target an even level of sharpness across the image. Areas of
  the input image that are already sharp are sharpened less, while areas that lack detail are
  sharpened more. This allows for higher overall natural visual sharpness with fewer artifacts.» Es
  decir: el objetivo declarado del sector no es maximizar la pendiente de un canto, sino repartir
  nitidez sin artefactos. La medida de rampa maximiza justo lo que CAS evita.
- **La literatura de superresolución** mide `Q = D(salida, verdad)` — una distancia a una referencia,
  no una propiedad de un canto (arXiv:2503.13074, §1). Su objeción conocida es que **las verdades de
  los conjuntos publicados son de mala calidad**, así que las métricas premian parecerse a una mala
  referencia. Esa objeción **no alcanza aquí**: la verdad se dibuja en el propio fichero de prueba y
  es exacta por construcción.
- **La documentación de SkSL de Skia** confirma que el hijo que se evalúa es un `SkShader` con sus
  propias opciones de muestreo, y **todos sus ejemplos usan `kLinear`**: ninguno dice si un
  remuestreo cúbico llega al hijo. Inconcluso no es «no», así que se midió (abajo).
- **La API del remuestreador cúbico acepta la familia entera**, no sólo sus dos nombres. Medido por
  reflexión sobre `SkiaSharp 3.119.4`: `SKCubicResampler` tiene `.ctor(Single, Single)` más los campos
  `Mitchell` y `CatmullRom`. Control negativo: `SKLanczosResampler` no existe.

## El instrumento nuevo

`tests/ApSolutions.LocalMedia.UiTests/Player/VideoUpscaleFidelityTests.cs`. Dibuja una verdad de
320×240, la **reduce por caja** a 80×60 para fabricar el fotograma decodificado, la amplía por la ruta
que se quiera medir y devuelve la **distancia media absoluta por píxel** a la verdad, de 0 a 255.
Castiga en un solo número el borrón, el dentado y el halo, porque los tres alejan de la verdad.

**La trampa que costó el primer borrador, y que es la parte que hay que recordar.** La primera verdad
tenía todos los cantos en múltiplos de cuatro, así que reducir por cuatro **no perdía nada** y el
vecino más cercano la reconstruía byte a byte: puntuó **0,50** contra los **30,54** del bilineal. Un
patrón alineado a la rejilla del origen premia lo mismo que la rampa, con otra cara. La verdad de
ahora está **fuera de rejilla a propósito** —canto en 41,7, diagonal de pendiente 0,62, anillos de
frecuencia creciente y rayas diagonales finas— y se dibuja por supermuestreo de dieciséis puntos por
píxel, porque un fotograma real está suavizado.

Sus tres controles:

| Control | Qué afirma | Por qué |
| --- | --- | --- |
| Instrumento | La composición mide **más de 8** de distancia | Una verdad que el origen ya puede contener mide casi cero para todo, y entonces nada se está midiendo |
| Negativo | El vecino más cercano queda **más lejos** que el borrón | Es exactamente lo que la rampa dice al contrario. Si esto pasa al revés, la medida nueva se ha convertido en la vieja |
| Alineación | Una imagen que no hace falta ampliar vuelve **a menos de 1** de sí misma | Un píxel de desfase cobraría el mismo peaje a todos los candidatos y los ordenaría igual, así que el orden se vería sano con las distancias siendo ruido |

## Las cifras

Todo medido en ese arnés, sobre la misma verdad. La distancia es media absoluta por píxel; «más
cerca» es contra la composición.

| Ruta | Distancia | Más cerca | Qué puerta cae |
| --- | --- | --- | --- |
| Vecino más cercano (`BitmapInterpolationMode.None`) | 22,77 | **−4,9 %**, o sea peor | — |
| La composición, sin la mejora | 21,70 | — | — |
| Lo que se publicó antes: lineal + máscara 0,6 | 15,11 | 30,4 % | ninguna; rampa 2, halo 17 |
| **Lo que queda puesto: cúbico B=0 C=0,85 + máscara 0,45 acotada** | **13,94** | **35,7 %** | **ninguna; rampa 2, halo 16** |
| Cúbico C=0,85 + máscara 0,3 acotada | — | 36,8 % | **la rampa**, que vuelve a 4 |
| Cúbico C=1,0 + máscara 0,45 acotada | — | 36,2 % | **la rampa**, que vuelve a 4 |
| Cúbico C=0,85 + máscara 0,6 acotada | 14,32 | 34,0 % | **el suelo de fidelidad** |
| Cúbico C=0,5 (Catmull-Rom) + máscara 0,45 acotada | — | 28,1 % | **el suelo de fidelidad** |
| Cúbico C=0,85 + máscara 1,0 acotada | — | 30,3 % | **el suelo de fidelidad** |
| Cúbico C=0,75 + máscara 0,45 **sin acotar** | **12,60** | **41,9 %** | **el halo**: 26 niveles contra 22 |

**La distancia va sólo donde el arnés la imprimió**; en las demás filas se leyó el porcentaje, que es
lo que la puerta compara. El halo y la rampa van sólo donde se midieron: las filas sin cifra se
conocen por el color de su puerta, no por un número.

**Las tres medidas a la vez son el criterio, y ninguna se aflojó.** El suelo de fidelidad es 35 %, la
rampa tiene que quedar por debajo de los 4 de la composición, y el halo por debajo de 22 niveles —ese
límite lo puso `gate-auditor` el 2026-09-13, entre los 17 que medía la máscara de 0,6 y los 28 que
«dibujan un contorno alrededor de todo»—. **Cada configuración que puntúa mejor en fidelidad falla
una de las otras dos**, incluida la mejor de todas: 41,9 % con el halo en 26.

**Y el modelo fuera de línea predijo el renderizador real.** Antes de tocar código se modeló la
aritmética en un guion aparte, que reprodujo el perfil archivado `3,86,169,252` **exacto**; predijo
21,69 / 15,10 / 30,4 % para la composición y la ruta de entonces, y el arnés midió **21,70 / 15,11 /
30,4 %**. Para el cúbico sin acotar predijo 38 % y el arnés dio 41,9 %: acertó el orden y se quedó corto
en el tamaño. Un modelo calibrado contra una medición archivada sirve para elegir; **la medición sigue
siendo la del arnés**.

## Por qué el shader acota su resultado, que es la decisión de fondo

Porque el cúbico afilado **timbra por su cuenta**: sus lóbulos negativos ya rebasan antes de que la
máscara añada nada, y sumados dieron 26 niveles contra un límite de 22. La salida cómoda era subir el
límite. La correcta es la que AMD describe para su propio afilado adaptativo al contraste, en sus
palabras: «areas of the input image that are already sharp are sharpened less … higher overall
natural visual sharpness with fewer artifacts».

Así que el shader **recorta el píxel afilado entre el más claro y el más oscuro de los cinco que
leyó**. Con eso el halo no es pequeño: es imposible, porque el resultado no puede salir del rango que
ya existía alrededor. Medido: 16 niveles con el recorte y **29 sin él**, con los mismos coeficientes.

**Y está vigilado, no sólo pretendido.** Borrar las dos líneas del recorte pone roja la puerta del
halo —29 contra 22—, comprobado por mutación el 2026-09-13.

## Por qué 0,45 y no 0,3, que puntúa mejor

Porque **la rampa sigue siendo una puerta**. Con máscara 0,3 la rampa del canto duro vuelve a 4 —la de
la composición— y tres pruebas de `VideoUpscaleQualityTests` se ponen rojas; medido poniendo 0,3 y
ejecutándolas. Lo mismo pasa con un cúbico de C=1,0, que también puntúa mejor en fidelidad.

**Y una trampa del método, que costó una vuelta**: el modelo fuera de línea acertó la fidelidad, el
perfil del canto y el rebase —predijo 17 y 26, y el arnés midió 17 y 26—, pero **falló la rampa en un
punto de la frontera**: dijo 2 para C=0,65 con máscara 0,3 y el arnés dio 4. La rampa es un recuento
discreto pegado a dos umbrales, así que salta con diferencias que el modelo no resuelve. **La rampa se
mide en el arnés; el modelo sirve para elegir a quién medir.**

## Lo que esto contestó de paso, y que la documentación no podía

**El muestreo cúbico sí llega al shader hijo.** Ninguno de los ejemplos de Skia lo usa, así que era
una pregunta abierta con dos respuestas indistinguibles a simple vista: si la opción se ignorara, la
fidelidad se habría quedado en el 30,4 % de antes. Subió a 41,9 %, así que llegó. El control lo
proporciona la propia puerta: su suelo del 35 % está **entre** los dos valores.

## El diagnóstico falso, que es la parte que hay que recordar

Con el cúbico ya puesto, el propietario miró unos créditos y dijo: «mejoró bastante pero sigue con
artefactos o ruido», y al concretar, «el fondo negro de la escena está limpio pero alrededor de las
letras se ven cuadraditos o de distintos tonos».

**El timbre del cúbico era el sospechoso obvio y encajaba**: el escalón de 16 niveles está justo al
lado del canto, el negro plano no lo tiene, y la descripción coincidía. Así que se bajó el coeficiente
a 0,7 —escalón 13—, **y se aflojó el suelo de fidelidad de 35 a 30 para que pasara**. Él volvió a
mirar: «mejoró pero siguen ahí». Segundo escalón preparado.

**Entonces puso el control que aquí nadie había puesto**: «eso me ocurría al subir la gamma a 1.5 […]
la tenía en 1.5 y se veían los cuadros, bajé a 1 y dejó de verse».

No era el reescalado. Era **banda de cuantización de la curva de tono**, medida en el código:
`PictureAdjustment` construye una tabla de 256 niveles en 8 bits y escribe `(byte)(256d * v)`, así que
una gamma pronunciada manda niveles de entrada distintos al mismo de salida y un degradado se
convierte en parches planos. Queda registrado como `ENG-021`, con el difuminado como arreglo conocido
y con la puerta que le falta, porque **hoy ninguna prueba mide bandas** y por eso llegó a producción.

**Lo que se deshizo**: el coeficiente volvió a 0,85, el suelo a 35 y el techo del escalón a 22. Las
tres cosas se habían movido persiguiendo un defecto ajeno.

**La lección, y vale para cualquier informe visual**: lo que alguien ve es un **síntoma, no un
diagnóstico**. Antes de tocar un parámetro se apaga el sospechoso y se mira otra vez; aquí había dos
candidatos tocando el mismo píxel y se ajustó uno sin aislarlo. Una puerta aflojada para acomodar un
diagnóstico equivocado es peor que el defecto, porque el defecto se ve y la puerta ya no.

## El techo de este diseño, medido con tres shaders distintos

El detour dejó algo que vale: **cuánta nitidez se puede sacar sin escalón alguno.**

| Diseño | Más cerca de la verdad | Escalón |
| --- | --- | --- |
| Muestreo lineal + máscara acotada, cualquier fuerza hasta 2,2 | **13,7 %** | 0 |
| Remapeo monótono del rango local (no puede rebasar por construcción) | **13,4 %** | **0** |
| Atado a una segunda copia lineal con margen de 0,04 | 25,7 % | 10 |
| Cúbico C=0,5 + máscara acotada | 28,1 % | 9 |
| Cúbico C=0,7 + máscara acotada | 33,2 % | 13 |
| **Cúbico C=0,85 + máscara acotada — lo que se envía** | **35,7 %** | 16 |

**Toda la nitidez por encima del 13 % sale de los lóbulos negativos del cúbico, y el escalón también.**
No hay ajuste que separe las dos cosas dentro de esta familia. Y el remapeo monótono —que se había
descartado por la mañana con la métrica de rampa— da 13,4 % con escalón cero: **la métrica vieja lo
rechazó por el motivo equivocado, y la nueva confirma que el rechazo era correcto por otro**.

Lo que consigue nitidez sin escalón es interpolar **a lo largo** del canto en vez de a través — la
familia dirigida por bordes, que es lo que hace la primera pasada de FSR y que no se copia por la
atribución. Registrado como `ENG-022` con su coste por medir.

## Lo que queda abierto

- **Lanczos-3 sigue siendo el techo de la familia de núcleos fijos**: en el barrido dio 11,91 contra los 12,60 del cúbico
  afilado, un 6 % más de fidelidad a cambio de 36 muestras por píxel de destino en vez de 16 que hace
  el hardware. No se construye sin medir antes el coste por fotograma, que es lo que
  `UpscaleCostPolicy` existe para contestar.
- **El juicio visual final es del propietario**, que es parte del criterio de aceptación escrito de
  `PLY-016`. Esto mide fidelidad, no gusto.
