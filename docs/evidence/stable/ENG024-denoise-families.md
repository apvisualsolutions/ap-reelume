<!-- SPDX-FileCopyrightText: 2026 AP Solutions -->
<!-- SPDX-License-Identifier: LicenseRef-APSolutions -->

# ENG-024 — qué familia de reductor quita los cuadros, medido antes de escribir ninguno

**Fecha:** 2026-09-25
**Alcance:** `ENG-024`, el reductor de ruido
**Estado:** medición de laboratorio sobre una escena sintética; **no hay código de producción**

## Lo que se comprobó antes de medir

Tres premisas de la fila, cada una en su fuente y no en la memoria:

- **Los filtros de VLC no sirven por la ruta de esta aplicación, sea cual sea su licencia.**
  `PLY16-low-res-spike.md` midió `hqdn3d` —y `sharpen` y `postproc`— y ninguno procesó un solo
  fotograma: la cadena de filtros de VLC 3 se retira entera con la salida por callbacks. **La fila
  decía que `hqdn3d` «podía sobrevivir» porque no cambia el formato, y esa medición ya lo había
  descartado.** El control de aquella evidencia —pedir I420, el formato que procesan los filtros de
  CPU de VLC— falló igual, así que el formato no era el bloqueo.
- **`hqdn3d` es GPL.** Leído en el fuente de VLC 3.0.x,
  `modules/video_filter/hqdn3d.h`: «This file is part of MPlayer», GPL-2.0 o posterior. Coincide con
  `audit-eng027-plugin-gpl-sources.md`.
- **Los cuadros no los crea el decodificador de este programa.** El filtro antibloques del propio
  códec lo gobierna `avcodec-skiploopfilter`, cuyo valor por defecto en VLC 3.0.x es **0** —leído en
  `modules/codec/avcodec/avcodec.c`—, y `src/` no lo toca: la única opción de `avcodec` que se pasa
  es `avcodec-hw`. Así que están en el fichero, que es lo que el propietario comprobó en VLC.

De ahí sale que el reductor tiene que ser **propio**, escrito desde la descripción de un algoritmo y
nunca desde un código GPL.

## El laboratorio

FFmpeg 2024-06-21 del equipo, **como instrumento y no como parte del programa**: sus filtros son las
implementaciones de referencia de cada familia, y lo que se decide aquí es qué familia escribir.

- **La verdad**: 720×480, cuatro segundos, degradados oscuros (luma de 16 a unos 40) mezclados con un
  fractal de poco contraste que pone bordes y detalle.
- **El fichero**: la verdad con grano temporal (`noise=alls=7:allf=t`) comprimida con poca calidad,
  en **H.264** (`libx264 -crf 30`) y en **MPEG-4 parte 2** (`mpeg4 -q:v 9`, la familia de DivX y
  Xvid). Mirado a ojo con gamma 1,5: los dos dejan cuadros en las zonas planas, y MPEG-4 más.
- **La cadena**: fichero → reductor → gamma 1,5 → luma, fotogramas 20 a 79. La verdad pasa por la
  misma gamma.

## El instrumento, que falló dos veces antes de medir

**Primero, el cero.** `hqdn3d=4:3:0:0` y `hqdn3d=0:0:6:4.5` dieron cifras idénticas entre sí y con
el valor por defecto: en ese filtro un **0 significa «el valor por defecto»**. Con `0.001` en su
lugar, las variantes difieren.

**Segundo, la vara.** Con el error medio —PSNR, SSIM y el RMS de las zonas planas— **un desenfoque
gaussiano, puesto como control, superaba a casi todos los reductores**, y `hqdn3d`, el filtro con
el que el propietario vio arreglada la imagen, salía **peor que no filtrar**. El error medio premia
lo liso aunque esté desplazado de tono, y un cuadro no es error medio: **es un salto en la frontera
de cada bloque de 8×8**. La vara que sirve es la de bloques de Wang, Bovik y Evans (2000): en las
zonas planas de la verdad, el salto medio entre píxeles vecinos **en** la frontera de la rejilla de
8, dividido por el salto medio **dentro** de los bloques. **Control: la verdad da 1,003.**

## Lo que contestó

| Reductor | Bloques H.264 | Bloques MPEG-4 | RMS plano H.264 | RMS plano MPEG-4 | RMS detalle MPEG-4 |
| --- | --- | --- | --- | --- | --- |
| verdad (control) | 1,00 | 1,00 | 0 | 0 | 0 |
| sin filtro | 2,09 | 286 | 1,89 | 1,43 | 5,50 |
| `hqdn3d` por defecto | 1,57 | 4,98 | 2,10 | 1,58 | 5,36 |
| `hqdn3d` sólo espacial | 1,50 | 6,56 | 1,90 | 1,48 | 5,37 |
| `hqdn3d` sólo temporal | 2,02 | 210 | 2,05 | 1,48 | 5,49 |
| bilateral (σs 1,5, σr 0,06) | 2,02 | 34,6 | 1,87 | 1,41 | 5,36 |
| `deblock` | 1,66 | 0,95 | 1,88 | 1,31 | 5,27 |
| `atadenoise` (temporal) | 2,03 | 244 | 1,84 | 1,30 | 5,41 |
| medias no locales (s 3, p 7, r 9) | **1,17** | **0,77** | **1,73** | **1,19** | **5,19** |
| desenfoque gaussiano 1,5 (control) | 1,67 | 1,04 | 1,83 | 1,31 | 5,25 |

En MPEG-4 el denominador es casi cero —los bloques salen planos por dentro—, y por eso la cifra sin
filtro se dispara. Se lee como orden, no como proporción.

## Lo que se saca

- **Lo que quita los cuadros es la parte espacial.** `hqdn3d` sólo temporal casi no los toca (2,02
  y 210), y el temporal adaptativo tampoco. Es una buena noticia para construirlo: un filtro
  espacial trabaja fotograma a fotograma, sin estado entre uno y otro.
- **Las medias no locales ganan en las cinco columnas**, y son la única familia que quita los
  bloques sin pagar en el detalle: el desenfoque también los quita en MPEG-4, pero a costa del
  detalle. Son también la familia más cara.
- **El antibloques clásico funciona donde la rejilla es fija de 8** (MPEG-4) y poco en H.264, cuyos
  bloques son de tamaño variable y cuyo propio filtro ya actuó. **Un filtro alineado con la rejilla
  tiene que correr en la resolución del fichero**: después de ampliar, la rejilla ya no está donde
  el filtro la busca.
- **El bilateral no sirve para esto**: suaviza el grano, pero un salto de bloque es un borde para él
  y lo respeta.

## Límites, y lo que falta antes de construir

- **La escena es sintética.** Lo que falta es medir sobre el fichero en el que el propietario vio los
  cuadros, fuera del árbol y sin que su nombre ni su contenido lleguen a ningún documento.
- **El detalle ocupa el 1 % de la escena**, así que la columna de detalle es la más débil de la
  tabla. Una escena con textura real la reforzaría.
- **El coste no está medido.** Las medias no locales de referencia comparan 81 parches de 7×7 por
  píxel. Hay que medir qué cabe por fotograma en la resolución del fichero, en la CPU —donde el
  fotograma ya se recorre para convertirlo, y donde correría con el reescalado encendido o apagado—
  y en el shader.
- **El mando sigue pendiente**: por la regla 11 vive en el engranaje del reproductor, con su
  «Restaurar valores por defecto».
