<!-- SPDX-FileCopyrightText: 2026 AP Solutions -->
<!-- SPDX-License-Identifier: LicenseRef-APSolutions -->

# PLY-018 — los cuadros al subir la gamma: dos diagnósticos míos equivocados y el control que los zanjó

**Fecha:** 2026-09-13
**Alcance:** `PLY-018`, el ajuste de imagen en la reproducción
**Origen:** el propietario, mirando unos créditos con la gamma en 1,5

## El resumen, porque el camino fue largo y el resultado es corto

Con la gamma en 1,5 aparecían cuadros alrededor de las letras. **Se propusieron dos causas y las dos
eran mías y las dos estaban mal.** La tercera la encontró el propietario con un control de treinta
segundos: **los cuadros son ruido de compresión del propio fichero**, y el reductor de ruido de VLC
los quita.

Lo que quedó puesto de esta tanda es pequeño y real: el ajuste **redondea al nivel más cercano** en
vez de siempre hacia abajo. Lo que no quedó puesto está registrado con su porqué medido.

## Primer diagnóstico equivocado: el timbre del reescalador

El reescalador deja un escalón de 16 niveles junto a un canto — medido, y del tamaño de lo que se
describía. Se bajó su coeficiente dos veces persiguiéndolo **y se aflojó un suelo de fidelidad** para
que el ajuste pasara.

**El control lo puso él**: «eso me ocurría al subir la gamma a 1.5 […] la tenía en 1.5 y se veían los
cuadros, bajé a 1 y dejó de verse». Todo lo del reescalador se deshizo — coeficiente, suelo y techo.

## Segundo diagnóstico equivocado: la cuantización de la curva

Con la gamma señalada, el mecanismo obvio estaba en el código y es real: `BuildLookup` construía una
tabla de **256 niveles enteros**, y una curva más plana que uno a uno manda dos niveles de entrada al
mismo de salida — el degradado pierde un escalón. El arreglo de manual es **difuminado**: guardar la
fracción y repartirla entre píxeles vecinos.

Se construyó entero, con su matriz ordenada 8×8, sus pruebas y su control de instrumento. Y el
propietario lo vio en veinte minutos: **«ahora aparecen un montón de cuadraditos en toda la imagen»**.

**Medido después, la culpa no es la amplitud sino el orden.** El afilado **no** lo amplifica —el
recorte del shader lo impide, el patrón sigue midiendo un nivel, comprobado sobre un parche plano
ampliado y afilado—. Lo que pasa es que **el patrón vive en la resolución del fotograma y la pantalla
es cuatro veces eso**: una celda de 8×8 sale como un bloque de **32×32** píxeles de pantalla, y un
nivel de diferencia repartido sobre esa área vuelve a leerse como banda, con otra forma.

**Un difuminado va después del escalado, siempre.** Aquí iba antes. Retirado, y registrado como
`ENG-023` con lo que exige: llevar la curva de tono al shader, que sólo corre cuando el reescalado
está activo — así que hay que decidir qué pasa cuando no lo está.

**Medido el 2026-09-25 y no construido** (`ENG023-tone-curve-ordering.md`): la banda que se ve al
subir la gamma es el escalón de 8 bits del propio fichero estirado por la curva, y un difuminado
detrás del escalado tampoco la quita; lo que gana es menos de un nivel de tono medio.

## El diagnóstico bueno, y quién lo encontró

Pedido el control que sólo él podía hacer —el fichero es suyo—: abrir el mismo vídeo en VLC y subirle
la gamma allí. Su respuesta:

> «En VLC con la reducción de ruido se arregla casi todo, sólo faltaría la nitidez y el reescalado.»

Eso cierra la pregunta en los dos sentidos. **Los cuadros no los crea nuestro código**: están en el
fichero, los dejó la compresión, y subir la gamma estira las sombras y los saca a la luz. Y dice
además cuál es la imagen que él quiere: **nuestro reescalado más un reductor de ruido**.

**Y el complemento viaja en el paquete que ya usamos.** Medido sobre
`videolan.libvlc.windows 3.0.23.1`: `libhqdn3d_plugin.dll` está ahí, junto con `libgradfun_plugin.dll`
—que es justamente el que quita bandas— y `libsharpen_plugin.dll`. Control negativo: un barrido por un
nombre inventado no devuelve nada, así que el barrido funciona.

**Ojo con la evidencia archivada antes de darlo por hecho**: `PLY16-low-res-spike.md` midió que los
filtros de vídeo de VLC 3 **nunca procesan un fotograma** por la ruta de callbacks. Pero eso se midió
con un filtro que **cambia el formato**, y falló en la compensación de formatos; `hqdn3d` no lo
cambia. Es una sonda distinta y está registrada como `ENG-024`.

## Lo que sí quedó puesto: el redondeo

Un defecto aparte que estaba al lado y que la lupa destapó. La tabla **truncaba**: una curva que pedía
100,9 pintaba 100. No hace bandas —no colapsa niveles— pero desvía la imagen entera hacia abajo, y con
un mando cuyo desplazamiento no es un número entero de niveles lo hace en todas partes: **brillo 0,1
son 25,5 niveles**, así que toda la imagen salía medio nivel oscura.

Ahora la tabla es **punto fijo** con ocho bits de fracción y se redondea al nivel más cercano. No
introduce ningún patrón, porque todos los píxeles de un mismo nivel reciben la misma respuesta.

| Qué | Dónde | Afirma |
| --- | --- | --- |
| El redondeo | `PictureAdjustmentRoundingTests` | Cada nivel se pinta a **medio nivel o menos** de lo que la curva pide, en 8 combinaciones de los tres mandos |
| **El control del instrumento** | la misma | Truncando, ese error **sí** pasa de medio nivel — si no, la prueba de arriba aprobaría el escenario |
| El neutro | la misma | Identidad **exacta**: la tabla vale `nivel << 8` y se pinta el nivel |
| Que hay fracción que redondear | la misma | Más de 200 de los 256 niveles llevan fracción; si no, la tabla serían niveles enteros disfrazados |
| Que no hay patrón | `PackedYuvConverterTests` | Un parche plano sale de **un solo tono** con gamma 1,0, 1,5 y 0,6 |

**El detalle que casi rompe el neutro.** `PLY-018` promete la imagen byte a byte idéntica sin tocar
nada. La tabla de FFmpeg llega a la identidad **truncando** `256 × v`, que es un factor de 256/255
tapando el redondeo; redondeada, esa misma tabla manda el nivel 254 al 255. La curva es ahora
`255 × v` en punto fijo, así que el neutro cae en enteros exactos y redondear no lo mueve.

**Coste**: los 18 presupuestos de `PerformanceTests` en verde.

## Las dos lecciones, que valen más que el cambio

**Lo que alguien ve es un síntoma, no un diagnóstico.** Dos cosas tocaban el mismo píxel y se ajustó
una sin apagarla primero. El coste fueron dos cambios de producción, un suelo aflojado y un techo
bajado, todo deshecho. **Antes de tocar un parámetro se apaga el sospechoso.**

**Y un arreglo de manual aplicado en el sitio equivocado es un defecto nuevo.** El difuminado era la
respuesta correcta a la pregunta correcta, construido con sus pruebas y su control — y salió peor que
el defecto porque estaba en la primera etapa de una cadena que amplía cuatro veces. Ninguna de sus
pruebas podía verlo: **todas medían la resolución del fotograma, y el defecto vive en la de la
pantalla.**
