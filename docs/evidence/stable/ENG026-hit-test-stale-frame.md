<!-- SPDX-FileCopyrightText: 2026 AP Solutions -->
<!-- SPDX-License-Identifier: LicenseRef-APSolutions -->

# ENG-026 — el paseo elegía dónde pulsar sobre un fotograma viejo

**Fecha:** 2026-09-26
**Alcance:** `ENG-026`, el rojo del paseo que no venía del código
**Prueba que fija el mecanismo:** `tests/ApSolutions.LocalMedia.AccessibilityTests/HeadlessHitTestFrameTests.cs`

## El síntoma

Cinco rojos en CI sobre commits que no tocaban el código. Los tres últimos, el 2026-09-25 y el
2026-09-26, en la misma escena, `The_players_transport_is_operated_with_the_mouse`, y con el mismo
mensaje letra por letra: el clic de al lado de Pausa se eligió en `94, 1091` «sobre Inicio» y ahora
cae «sobre el reproductor», y pausó la película. En este equipo pasaba siempre.

**Los tres son posteriores a `ENG-018`**, que el 2026-09-25 hizo de la imagen un mando: un clic en ella
pausa. Los dos primeros rojos de la fila eran de otras escenas, y esta evidencia no los explica.

## Lo que se descartó, y cómo

- **Que los mandos se ocultan solos a los tres segundos** (`ENG-018`): en un runner lento, llegar a
  «reproduciendo» puede tardar más. Se metieron cuatro segundos de espera antes de pulsar y la escena
  **pasó**. Descartado por experimento, con el fichero restaurado y comparado después.
- **Que la colocación estuviera sin hacer**: `Reveal` ya invalidaba y colocaba antes de elegir el
  punto, y con todo eso el punto se eligió sobre Inicio.

## La causa, medida

El arnés decide si un punto es seguro —que no esté sobre una imagen— preguntando a
`InputHitTest`. **Esa consulta contesta desde el último fotograma dibujado, no desde el árbol.**
Los métodos que simulan el ratón dibujan un fotograma antes de actuar, como dice la documentación de
Avalonia sobre la plataforma sin ventana; la consulta, no. Así que el punto se eligió sobre un
fotograma anterior a la aparición del reproductor, y el movimiento del propio clic dibujó uno nuevo
en el que ya estaba: el clic cayó en la película y la pausó. En un equipo rápido el temporizador de
dibujo había llegado antes; en el runner, a veces no.

Medido con una sonda de diez repeticiones por método, un control que aparece encima de otro:

| Qué se hace después de colocar | La consulta ve el control nuevo |
| --- | --- |
| nada más | no se midió en la sonda; en una prueba aislada, vieja siempre; dentro de la suite completa, a veces al día |
| `ForceRenderTimerTick()` | **0 de 10** en esta suite; 1 de 10 con Skia |
| `ForceRenderTimerTick(5)` | 0 de 10 en esta suite; 1 de 10 con Skia |
| **`CaptureRenderedFrame()`** | **10 de 10**, en 0 a 3 ms |
| mover el ratón a otro punto | 10 de 10 |
| esperar 300 ms reales | 10 de 10 |

**El primer arreglo escrito fue `ForceRenderTimerTick`, y habría pasado aquí y seguido fallando en
CI.** La primera sonda, en la suite de interfaz, dio el resultado bueno una vez de diez por suerte, y
la prueba permanente lo desmintió en la ejecución siguiente. La suite de accesibilidad arranca
Avalonia sin el dibujo de Skia, al contrario que la de interfaz, así que la sonda se repitió en ella.

## El arreglo

`Reveal`, por donde pasan todas las pulsaciones del paseo y la elección del punto de al lado, captura
el fotograma después de colocar, y otra vez después de desplazar. Así el fotograma sobre el que se
elige es el mismo sobre el que cae el clic.

`HeadlessHitTestFrameTests` fija lo que el paseo necesita: que tras capturar, la consulta ve lo que
hay en pantalla. Mutante visto morir: sin la captura cae con «esperaba Over, obtuvo Under».

**Una primera versión afirmaba también la mitad vieja** —que tras colocar la consulta sigue viendo el
fotograma anterior— **y cayó dentro de la suite completa**: ahí el temporizador de dibujo real avanza a
veces solo durante la prueba y la consulta sale al día. Una prueba que depende de que el temporizador
no avance es la clase de rojo intermitente que esta fila persigue, así que esa mitad queda aquí, con
sus números, y no como aserción.

**Y una trampa del propio mutante, que costó una suite entera.** Restaurar el fichero copiando la
copia de seguridad **conserva su fecha antigua**, anterior al binario compilado con el mutante, y la
compilación incremental lo dio por actualizado: la suite completa midió el mutante y dio 157 de 158,
con la roja siendo precisamente la prueba mutada. Se vio comparando fechas (fuente a la 1:24:07,
binario a la 1:24:27); con la fecha del fichero actualizada, la prueba pasó tres de tres. **Tras
restaurar un mutante se toca la fecha del fichero o se recompila entero**, o se mide el binario
viejo.

## Lo que falta para cerrarla

En este equipo el rojo nunca se reprodujo, así que lo que lo confirma es CI. La fila se cierra cuando
los runs de la rama pasen esta escena de forma sostenida, no con el primero.
