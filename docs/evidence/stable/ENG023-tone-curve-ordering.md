<!-- SPDX-FileCopyrightText: 2026 AP Solutions -->
<!-- SPDX-License-Identifier: LicenseRef-APSolutions -->

# ENG-023 — la curva de tono detrás del escalado, medida y no construida

**Fecha:** 2026-09-25
**Alcance:** `ENG-023`, abierta el 2026-09-13 desde `PLY18-tone-banding.md`
**Prueba que vigila la decisión:** `tests/ApSolutions.LocalMedia.Domain.Tests/Playback/ToneCurveOrderingCandidateTests.cs`

## La pregunta

El difuminado de la curva de tono se construyó el 2026-09-13 **antes** del escalado y salió en la
pantalla como bloques de 32×32. La fila concluía que el difuminado va después del escalado, lo que
pide llevar la curva al shader; y como el shader sólo corre con el reescalado activo, había que
decidir qué pasaba cuando no lo está. **Antes de decidir eso se midió si el cambio gana algo.**

## El instrumento

Un degradado de 8 bits, en gris, ampliado cuatro veces. La cadena que se usa lleva la tabla **real**
de la curva (`BuildLookup` y `Quantise`); la candidata recibe el fotograma sin curva, lo amplía en
precisión completa, aplica la curva después y difumina con una matriz ordenada de 8×8 en resolución
de pantalla. Cinco escenas —rampa lenta en sombra, rango completo, sombra media, parche casi plano y
negro profundo— con gamma 0,7, 1,5, 2 y 3.

Dos varas de medir, cada una con su escalado:

- **La banda**: el mayor salto entre dos medias vecinas de 8×8, con el escalado **por repetición**.
  Se promedia porque lo que el ojo ve de un difuminado es su media. Y se usa el escalado más duro
  porque uno lineal ×4 parte cualquier salto de hasta cuatro niveles en saltos de uno y esconde la
  banda. El reproductor usa un cúbico afilado, que queda entre los dos.
- **El tono medio**: el mayor error de una media de 8×8 contra la curva en precisión completa, con
  escalado lineal.

## Lo que contestó

| Gamma | Escena | Banda: actual | Candidata | Rampa sin cuantizar | Tono medio: actual | Candidata |
| --- | --- | --- | --- | --- | --- | --- |
| 1,5 | sombra lenta | 3,0 | 2,9 | 1,0 | 0,85 | 0,69 |
| 1,5 | negro profundo | 3,0 | 3,2 | 1,0 | 0,95 | 0,80 |
| 2 | sombra media | 3,0 | 2,4 | 1,0 | 0,89 | 0,54 |
| 2 | negro profundo | 3,0 | 3,6 | 1,0 | 0,78 | 0,92 |
| 3 | casi plano | 2,0 | 2,7 | 1,0 | 0,48 | 0,64 |
| 0,7 | sombra lenta | 2,0 | 1,5 | 1,0 | 0,96 | 0,33 |

Una muestra de las dieciocho escenas que miden algo; la sonda completa está en la prueba.

**La banda que se ve al subir la gamma no la quita un difuminado.** Es el escalón de 8 bits del
propio fichero, que la curva estira a dos, tres o cuatro niveles. La misma rampa antes de cuantizarse
lee uno. Con la gamma por encima de 1, la candidata recorta la banda en algunas escenas (hasta 0,6
niveles, de 3,0 a 2,4) y la ensancha en otras, pero **nunca la baja de dos niveles** donde la cadena
actual la tiene así de ancha. Un difuminado reparte una fracción de nivel y no puede devolver un
escalón que el fichero ya no tiene. Eso lo hace un filtro que suaviza bandas (deband), no un
difuminado.

**Lo que sí gana la candidata es precisión del tono medio**: hasta dos tercios de nivel en algunas
escenas, sobre todo con gamma por debajo de 1, y pierde en otras. La peor escena de la cadena actual
queda en 1,01 niveles, que son dos redondeos (el de la curva y el de la conversión), y la de la
candidata en 0,96. Una diferencia de menos de un nivel en el tono medio no la enseña una pantalla de
8 bits.

## La decisión

**No se construye.** Costaría un shader en cada fotograma y un segundo sitio donde vive la curva, y
con él desaparece la pregunta de qué hacer con el reescalado apagado. Lo que el propietario vio
alrededor de las letras era ruido de compresión que la gamma saca a la luz (`ENG-021`), y lo que lo
arregla es el reductor de ruido de `ENG-024`. VLC trae además `libgradfun_plugin.dll`, que es
justamente un filtro contra bandas: si algún día molesta la banda de las sombras, ése es el camino, y
la sonda de filtros de `ENG-024` contesta si llegan a procesar un fotograma.

## Cómo se vigila

`ToneCurveOrderingCandidateTests`, con 27 casos:

- **La banda**: se pone roja si la candidata baja de dos niveles la banda de alguna escena con gamma
  por encima de 1. **Control**: la rampa sin cuantizar lee al menos un nivel menos que la cadena
  actual, así que la vara ve una banda cuando la hay.
- **El tono medio**: se pone roja si la peor escena de la candidata supera a la de la cadena actual
  en un cuarto de nivel. Tiene dos controles. **Donde la candidata gana, gana claramente** (0,33
  frente a 0,96), así que un error que la debilite no refuerza la decisión en silencio. Y **la curva
  truncada**, un defecto que fue real, sube el error más de 0,4 niveles.
- **La copia de la curva**: la prueba lleva la curva continua, porque la candidata la necesita entre
  niveles. Está atada a la tabla que pinta el reproductor en ocho combinaciones de brillo, contraste
  y gamma.
- **Ninguna escena vacía**: `Measure` rechaza una escena que todas las cadenas pintan plana.

## Lo que encontró el auditor en el primer borrador

El primer borrador de esta prueba medía la banda con el escalado lineal, y así **no podía fallar**:
posterizar la imagen a niveles pares o a múltiplos de tres le dejaba el escalón en uno. Su control
medía sin interpolar, que no era la condición que vigilaba. Además, la comparación miraba en un solo
sentido: quitar el difuminado o sesgar la candidata 0,4 niveles lo dejaba todo en verde. La copia de
la curva sólo se comprobaba con brillo 0 y contraste 1, y dos escenas eran negro total. El texto
decía «sin signo constante» cuando la candidata ganaba 9 de 14 escenas. Todo está corregido arriba.

## Mutantes

Copia comparada con el original antes de creer el resultado, y restaurada después con `cmp`:

| Mutante | Cuántas caen |
| --- | --- |
| La candidata sin difuminado | 1 de 27 |
| La candidata sesgada +0,4 niveles | 1 de 27 |
| El signo del brillo cambiado en la copia de la curva | 3 de 27 |
| Sin brillo en `BuildLookup`, en `src/` | 3 de 27 |
| Una candidata que sí quitara la banda | 12 de 27 |
| `Quantise` posterizando a niveles pares, en `src/` | 2 de 27 |

## Límites del modelo

Gris y en una dimensión, a propósito: la curva actúa sobre la luma y `PackedYuvConverter` suma la
misma luma a los tres canales, y una rampa es donde vive la banda. Las cifras de banda son del
escalado por repetición, que es el extremo duro; el reproductor las suaviza algo con su cúbico, igual
para las dos cadenas.
