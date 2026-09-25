<!-- SPDX-FileCopyrightText: 2026 AP Solutions -->
<!-- SPDX-License-Identifier: LicenseRef-APSolutions -->

# ENG-054 — el kit de vídeo RTX, obtenido y medido sobre el fichero real

**Fecha:** 2026-09-26
**Alcance:** `ENG-054`, la base de `PLY-016` en tarjetas NVIDIA, y `ENG-024` en las mismas tarjetas
**Estado:** medido en laboratorio, fuera del árbol; **no hay código de producción**

## Cómo se obtuvo

El propietario lo descargó con su cuenta de desarrollador de NVIDIA: `rtx_video_sdk_v1.0.2.zip`,
36 840 463 bytes, SHA-256 `AD959E55BD511AC3DB5C459FC8E1DA21E11451FC001D74326C36693455C6F429`. La página
pública del kit ya no existe —redirige a «AI for Media»—, pero la descarga con cuenta sigue viva.

**Nada del kit entra en este repositorio.** Es público, y la licencia prohíbe distribuir el kit
suelto. **Su guía de programación va marcada como confidencial de NVIDIA y entregada bajo acuerdo
de confidencialidad**, así que aquí no se cita; lo que sigue es lo medido y lo que dice la licencia.

## La licencia, leída en el zip (`NVIDIA RTX SDKs LICENSE`, v. 23-02-2024)

Nombra el RTX Video SDK expresamente. Lo que importa para este programa:

- **Permite distribuir** el kit «incorporado en código objeto» dentro de una aplicación con
  funciones propias más allá del kit (§1c y §2a). Este programa las tiene de sobra.
- **Exige** repartirlo con condiciones al menos tan protectoras como las suyas (§2c), y prohíbe
  desensamblarlo (§4a) y usarlo de forma que quede sujeto a una licencia de código abierto (§4e).
- **Suplemento**: la aplicación tiene que funcionar con tarjetas NVIDIA y el kit de NGX sólo se
  usa en sistemas con ellas (§1); **hay que avisar a NVIDIA antes del lanzamiento comercial**, con
  empresa, aplicación, plataforma y fecha (§4); y **la marca de NVIDIA va en la pantalla de inicio
  y en «Acerca de»** (§7.1b).

**Queda una pregunta para lo legal**, no para el código: cómo conviven el §2c y el §4a con la
excepción que `LICENSE` concede por la LGPL-2.1 §6 —modificar el programa para uso propio y
depurarlo con ingeniería inversa—. La lectura técnica es que esa excepción cubre el programa y no
una biblioteca de terceros que viaja con sus propias condiciones, pero no es una opinión legal.

## El laboratorio

Un programa de consola en C++, fuera del árbol, sobre la capa sencilla que trae el kit en sus
ejemplos: crea un dispositivo D3D11 en la tarjeta NVIDIA, sube cada fotograma RGBA, llama a la
superresolución con un nivel de calidad y lee la salida. **No abre ninguna ventana.** Se usaron las
DLL de publicación: las de desarrollo pueden llevar marca de agua, que falsearía la medición.

**Y no hace falta encender nada, medido con su control.** El interruptor de NVIDIA App estaba
**apagado**: `_User_Global_VAL_SuperResolution` valía 0 en la clave de clase del adaptador, que es lo
que NVIDIA App escribe al tocarlo (5 encendido, 0 apagado, medido en `PLY16-d3d11-probe.md`). En la
misma sesión y con el mismo fotograma real ampliado a 1080p, la vía del controlador **no movió ni un
byte** en la NVIDIA, mientras la de Intel, como control positivo, movía 7 554 888 de 8 294 400. **Con
ese interruptor apagado, el kit sí procesó los 192 fotogramas.** Es exactamente lo que la vía del
controlador no permitía.

## Lo medido sobre el fichero real

Los mismos cuatro fragmentos oscuros del Xvid de `ENG024-denoise-families.md`, 192 fotogramas, con
la vara sin referencia de allí: bloques en las zonas planas (1,00 es sin bloques) y detalle que
queda en las zonas con textura. En esta tabla la gamma se aplica sobre RGBA, así que el original lee
1,74 y no 1,54; dentro de la tabla el camino es el mismo.

| | Bloques | Detalle que queda | Coste en la RTX 5070 |
| --- | --- | --- | --- |
| original (control) | 1,74 | 100 % | — |
| **kit de NVIDIA, calidad 1, a tamaño original** | **1,01** | 109 % | **0,49 ms** |
| kit de NVIDIA, calidad 4, a tamaño original | 1,06 | 135 % | 0,99 ms |
| DCT solapada σ 3 (FFmpeg, referencia) | 0,95 | 79 % | ~130 ms en CPU |

**El detalle por encima del 100 % se miró**, que es la regla desde que una acuarela engañó a esta
vara: aquí es el afilado que el kit declara hacer, no un posterizado. A tamaño original, la calidad
1 alisa los bloques sin emborronar la tela; la calidad 4 afila más y marca la textura. **Ampliado a
1080p con calidad 4**, frente a la ampliación bicúbica, los pliegues de la ropa y el enrejado salen
nítidos y sin bloques.

**Lo que esto cambia**: en tarjetas NVIDIA el kit hace a la vez el reescalado de `PLY-016` y la
limpieza de bloques de `ENG-024`, en menos de un milisegundo y sin que nadie toque nada. El reductor
propio sigue haciendo falta para las demás tarjetas.

## Lo que falta

- **Integrarlo en el programa.** La capa del kit es C++ con una biblioteca estática, así que desde
  C# hace falta una DLL puente pequeña, compilada en CI. Y la DLL de NVIDIA no puede vivir en este
  repositorio ni en sus releases públicas, así que CI necesita obtenerla de un sitio privado.
- **Devolver la imagen a la ventana**: el mismo obstáculo que ya tenía la vía del controlador.
- **Lo del propietario**: la marca de NVIDIA en inicio y «Acerca de», el aviso a NVIDIA antes de
  publicar, y la pregunta legal de arriba.
