<!-- SPDX-FileCopyrightText: 2026 AP Solutions -->
<!-- SPDX-License-Identifier: LicenseRef-APSolutions -->

# ENG-022 — el reescalador guiado por bordes, medido y no construido

**Fecha:** 2026-09-25
**Alcance:** `ENG-022`, abierta el 2026-09-13 desde `PLY16-upscale-fidelity.md`
**Prueba que vigila la decisión:** `tests/ApSolutions.LocalMedia.UiTests/Player/EdgeDirectedUpscaleCandidateTests.cs`

## La pregunta

Lo que se usa hoy —cúbico `B=0 C=0,85` con una máscara de 0,45 acotada a cinco vecinos— queda un
**35,7 %** más cerca de la verdad que la composición, y lo paga con un escalón de **16** niveles junto a
cada canto. El registro decía que nada que no pudiera salirse del rango local pasaba del **13 %**, así
que nitidez sin escalón pediría interpolar a lo largo del canto. La fila pedía medir las opciones y su
coste **antes** de escribir el shader.

## El instrumento, y su control

Un arnés sobre un lienzo Skia por software, fuera de Avalonia, que dibuja la misma verdad fuera de
rejilla que `VideoUpscaleFidelityTests` —ahora compartida en `UpscaleTruth.cs`, para que los dos
arneses no puedan medir cuadros distintos— y lee las tres medidas del criterio: la distancia a la
verdad, el escalón sobre un canto gris 64/192 y la rampa sobre un canto duro negro/blanco.

**Control positivo, antes de creerse nada**: por este arnés la composición da **21,70**, lo que se usa
da **35,7 %**, escalón **16** y rampa **2**, y la composición rampa **4**. Son exactamente las cifras
archivadas a través de un `VideoFrameView` real. Lo que se usa se monta con los miembros de producción
(`UpscaleShaderSource`, `UpscaleDrawPlan.SharpeningSource`), no con una copia, así que un cambio en la
cadena es un cambio en el control.

**Una trampa del propio banco, cazada por el instrumento**: el primer barrido de «empinado» devolvió
cifras idénticas con tres fuerzas distintas. No era el resultado: la sustitución que añadía el
parámetro no había casado por los finales de línea y el shader nunca lo leía. Un cero de variación se
comprueba antes de leerse.

## Las cifras

Ampliación ×4 de 80×60 a 320×240. «Más cerca» es contra la composición; el desglose es la distancia
media en cada cuarto de la verdad.

| Candidato | Distancia | Más cerca | Escalón | Rampa | Canto / diagonal / anillos / rayas |
| --- | --- | --- | --- | --- | --- |
| Composición (lineal) | 21,70 | — | 0 | 4 | 5,82 / 2,73 / 46,73 / 31,50 |
| Vecino más cercano | 22,77 | −4,9 % | 0 | 0 | 5,59 / 1,46 / 46,96 / 37,06 |
| **Lo que se usa** | **13,94** | **35,7 %** | **16** | **2** | 3,83 / 1,52 / 39,65 / 10,78 |
| Cúbico C=0,5 acotado a los 4 texeles | 17,52 | 19,2 % | 0 | 4 | 5,26 / 2,18 / 41,37 / 21,29 |
| Cúbico C=1,0 acotado a los 4 texeles | 15,44 | 28,8 % | 0 | 4 | 4,73 / 2,15 / 40,17 / 14,72 |
| Cúbico C=1,25 acotado a los 4 texeles | 15,22 | 29,9 % | 0 | 4 | 4,50 / 2,16 / 40,76 / 13,45 |
| Cúbico C=0,85 sin acotar | 15,56 | 28,3 % | 16 | 4 | 4,91 / 2,16 / 39,69 / 15,49 |
| Lanczos-3 sin acotar | 15,14 | 30,2 % | 15 | 4 | 5,08 / 2,27 / 39,45 / 13,74 |
| Lanczos-3 acotado a los 4 texeles | 15,43 | 28,9 % | 0 | 4 | 4,85 / 2,07 / 39,84 / 14,99 |
| Cúbico C=1 + máscara 0,5 + empinado 0,5, acotado | 14,33 | 33,9 % | 0 | 4 | 3,97 / 1,70 / 39,96 / 11,71 |
| Orientado, sin estirar (Lanczos-2 radial) | 15,66 | 27,8 % | 0 | 2 | 4,45 / 1,57 / 39,31 / 17,30 |
| **Orientado, estirado 0,75, umbral 3** | **14,13** | **34,9 %** | **0** | **2** | 4,58 / 1,62 / 40,09 / 10,22 |
| Orientado, el mismo sin acotar (mutante) | — | 42,2 % | 29 | — | — |

**Lo que el barrido dejó claro alrededor del mejor**: estirar a lo largo del canto entre 0,5 y 1 da
entre 33,3 y 34,9 %; estrecharlo a través lo empeora siempre (0,1 → 33,7 %, 0,6 → −3,4 %, porque
emborrona las rayas finas); una máscara contra la copia bilineal mejora el canto y estropea anillos y
rayas (0,25 → 33,0 %); y el empinado dentro del rango, que al cúbico le da tres puntos, al orientado le
quita dos. Es una meseta, no un parámetro por afinar.

## Lo que esto contesta

**No había un techo del 13 % sin escalón.** Lo que se quedaba en el 13 % era acotar a **cinco muestras
ya remuestreadas por el cúbico afilado**: el timbre ya está dentro del rango cuando la acotación lo
mira. Acotando a los **cuatro texeles que el decodificador produjo** el escalón es cero por
construcción, y un cúbico simple llega al 30 %. La afirmación estaba escrita en
`SkiaUpscaleDrawOperation.cs`, en `PLY16-upscale-fidelity.md` y en la propia fila; las tres se
corrigen en el mismo cambio.

**Y el mejor candidato no gana.** Con escalón cero y la misma rampa, se queda **0,8 puntos por debajo**
en fidelidad. Pierde en el canto recto (4,58 contra 3,83), que es donde los lóbulos negativos de lo que
se usa estrechan la transición, y gana en las rayas (10,22 contra 10,78). El escalón de 16 que
eliminaría está por debajo del techo de 22, y el propietario nunca lo señaló: los cuadros que vio eran
la curva de tono (`ENG-021`).

**Y el mutante sin acotar lo confirma por el otro lado**: el mismo núcleo orientado sin acotación da
42,2 % con un escalón de 29, lo mismo que el cúbico sin acotar de `PLY16-upscale-fidelity.md` (41,9 %,
26). Toda la fidelidad por encima de la meseta sigue siendo rebase, con cualquier núcleo.

## El coste

`UpscaleCostPolicy.MeasureDifference`, tres pasadas contra una, a 3840×2160 en el lienzo por software de
esta máquina. Tres lecturas por fila.

| Candidato | 1920×1080 → 4K, ms por fotograma | 960×540 → 4K |
| --- | --- | --- |
| Composición (lineal) | 12 / 13 / 12 | 13 / 13 / 13 |
| Lo que se usa | 820 / 857 / 1000 | 888 / 899 / 896 |
| Cúbico C=1,25 acotado | 7003 / 7688 / 7696 | 4346 / 4129 / 4173 |
| Lanczos-3 acotado | 16154 / 15710 / 5398 | 7805 / 7651 / 7771 |
| Orientado 0,75 / 3 | 6470 / 5914 / 5447 | 5129 / 5137 / 5052 |

**Sirve para ordenar y no para decidir contra el presupuesto.** Ninguna fila cabe en un tercio de
fotograma por software —lo que se usa tampoco—, porque la aplicación no dibuja esto en la CPU. Lo que
sí dice: el orientado cuesta **seis veces** lo que se usa en el mismo lienzo. En la tarjeta la
proporción puede ser otra —lo que se usa lee cinco muestras cúbicas y el candidato dieciséis texeles
con más aritmética—, y eso es una hipótesis, no una medida.

**La cifra de la tarjeta no se puede leer sin ventana, comprobado por reflexión sobre `SkiaSharp
3.119.4`**: `GRContext` ofrece `CreateGl`, `CreateVulkan`, `CreateDirect3D` y `CreateMetal`, y los tres
primeros piden un contexto del sistema que una prueba sin ventana no tiene (Direct3D 12 aquí, no el
Direct3D 11 que ya usa la sonda). Control negativo en la misma pasada: `SKLanczosResampler` no existe.
Como el candidato no gana en calidad, esa cifra no cambia la decisión y no se persigue.

## Lo que no se midió, y por qué

- **DCCI y NEDI**, que la fila nombraba: los dos están definidos sobre una rejilla ×2 —NEDI además
  resuelve un sistema de covarianzas por píxel— y un reproductor amplía por lo que pida la ventana. La
  forma general de «interpolar a lo largo del canto» para cualquier escala es el núcleo orientado por
  el tensor de estructura, que es lo que se midió.
- **La primera pasada de FSR**, por la atribución que ya recoge `UpscaleShaderSource`.

## La decisión

**No se construye.** Lo que se usa sigue siendo lo mejor en fidelidad y lo más barato de los
candidatos serios. La alternativa queda escrita y vigilada: si algún día aparecen contornos alrededor
de las letras, o lo que se usa se ablanda, el candidato está a un cambio de producción, y
`The_edge_directed_candidate_lands_just_short_of_what_ships` se pone roja en cuanto lo supere.

**Vista fallar**: quitando la acotación del candidato, la prueba del escalón da 29 contra 0 y la de la
decisión da 42,2 % contra 35,7 %; con la acotación, 3 de 3 en verde.

**Y `gate-auditor` encontró una puerta débil, ya corregida**: la prueba de la decisión aceptaba
cualquier candidato por encima del 33 %, y dos mutantes rotos pasaban las tres —sin la coherencia en
el estiramiento, 33,9 %; con un gradiente mal indexado, 34,5 %—. Ahora exige la banda 34,8–35,0, tan
estrecha como la del control, y el primero de los dos se ve morir. Otros siete mutantes del shader ya
morían: sin tensor 21,4 %, estirar a través 10,7 %, diagonal reflejada 23,4 %, sin umbral 32,6 %,
acotación sobre los texeles equivocados −24,1 %, sin el medio texel −83,9 %, y sin estirar 27,8 % —la
misma cifra que la fila del Lanczos-2 radial, que confirma que la prueba reproduce la tabla—.
