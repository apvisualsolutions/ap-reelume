# PLY-016 — Sonda del procesador de vídeo D3D11 / D3D11 video processor probe

- Fecha / Date: 2026-09-12
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  SkiaSharp 3.119.4, LibVLC 3.0.23.1. GPU: NVIDIA GeForce RTX 5070 (controlador 32.0.16.1656) e
  Intel UHD Graphics 770 (controlador 32.0.101.7088), las dos moviendo 3840×2160.
- IDs: `PLY-016=IN_PROGRESS`
- Prueba re-ejecutable / Re-runnable test:
  `tests/ApSolutions.LocalMedia.IntegrationTests/Playback/WindowsVideoUpscaleProbeTests.cs`
- Informe / Report: `artifacts/test-results/PLY-016/d3d11-upscale-probe.csv` y `.txt`

## Veredicto / Verdict

**Hay mejora automática en las dos tarjetas sin que nadie toque nada.** La superresolución de Intel
funciona y está probada por píxeles; la de NVIDIA acepta la petición y no mueve un píxel —NO
CONCLUYENTE—, **pero el realce de bordes estándar de Direct3D sí funciona en ella**. /
**There is automatic improvement on both cards with nobody switching anything on.**

| Adaptador / Adapter | Entrada | Extensión | Bytes distintos | Control | Distintos | Filtros estándar | Realce de bordes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| NVIDIA GeForce RTX 5070 | `YUY2` | `S_OK` | **0** de 33 177 600 | 0 | 6 | reducción de ruido, realce de bordes | **8 084 664** (24,4 %) |
| Intel UHD Graphics 770 | `YUY2` | `S_OK` | **18 109 378** (54,6 %) | 0 | 44 | reducción de ruido, realce de bordes | **8 086 614** (24,4 %) |
| Microsoft Basic Render Driver | — | no intentado | no se ejecutó | — | — | no se ejecutó | no se ejecutó |

1280×720 → 3840×2160, decidido por `UpscalePolicy` y no escrito a mano.

## La pregunta del propietario: ¿sin que el usuario cambie nada?

**Sí, y por tres vías que no dependen de ningún interruptor.** / **Yes, by three routes that depend
on no switch.**

1. **Los filtros estándar del procesador de vídeo.** `NOISE_REDUCTION` y `EDGE_ENHANCEMENT` son de
   Direct3D, no de un fabricante: **las dos tarjetas los declaran y el realce de bordes cambia
   8,08 millones de bytes en ambas**, NVIDIA incluida. Ni aplicación del fabricante, ni ajuste del
   usuario, ni licencia. Es el suelo de `PLY-016`, no un extra.
2. **La superresolución de Intel**, que ya funciona sin que nadie la encienda.
3. **El reescalador portátil (FSR 1, MIT) en SkSL**, que corre en cualquier tarjeta.

### La licencia del SDK, releída en su fuente el 2026-09-12

El descarte se confirma, y el motivo concreto importa más que el veredicto. El acuerdo de licencia de
software de NVIDIA —el que rige sus SDK— dice que el cliente «no puede copiar, vender, revender,
alquilar, sublicenciar, transferir, ceder, distribuir, modificar ni crear obras derivadas», y concede
una licencia expresamente **no sublicenciable**. `GPL-3.0` exige poder hacer justo eso: modificar,
redistribuir con fuente y sublicenciar aguas abajo. Las dos obligaciones no se pueden cumplir a la
vez, así que **el SDK no puede viajar dentro de esta aplicación**.

**Pero lo que prohíbe es distribuir el SDK, y eso no es lo que esta cadena hace.** La extensión del
procesador de vídeo que la sonda usa vive en el **controlador que la persona ya tiene instalado**, y
se invoca por una interfaz de Direct3D. No se empaqueta nada de NVIDIA ni se enlaza contra nada suyo.
La licencia cierra la puerta de empaquetar el SDK y deja abierta la que este proyecto usa. /
**The licence forbids shipping the SDK; it does not touch calling a driver interface the user already
has.**

**Y lo que NO se puede hacer, comprobado el 2026-09-12:** una aplicación **no puede encender la Súper
resolución RTX por su cuenta**. No hay ajuste para ella en `NvApiDriverSettings.h` —la cabecera
pública de ajustes de NVIDIA, que sí cubre DLSS, antialiasing, G-SYNC y Optimus—, los usuarios de
`nvidiaProfileInspector` tampoco la encuentran en la base de perfiles del controlador, y en esta
máquina no aparece por su nombre ni en el registro (45 claves de NVIDIA, control positivo) ni en los
ficheros de configuración de NVIDIA App. El SDK que sí la expondría está descartado por licencia.

**El corolario de diseño: la superresolución del fabricante es un extra oportunista, nunca la
promesa.** Lo que se promete corre siempre; lo del fabricante se enciende solo si al compararlo
cambia píxeles, que es exactamente lo que esta sonda mide.

## Lo que contesta el riesgo n.º 1 / What answers risk #1

**`YUY2` entra en el procesador de vídeo de las dos tarjetas reales**, como entrada y como salida
(`flags=3`). Es la respuesta que `PLY-016` estaba esperando: LibVLC ya entrega `UYVY`, y `YUY2` son
esos mismos bytes con cada pareja intercambiada, así que el fotograma llega al reescalador **sin
conversión de color en CPU** — la que hoy corre escalar, byte a byte, dentro del hilo del
decodificador. / **`YUY2` is accepted by both real cards**, as input and as output, which means the
`UYVY` LibVLC already hands out reaches the scaler with a byte-pair swap and no colour conversion.

Las dos aceptan además `NV12`, `P010`, `B8G8R8A8`, `R8G8B8A8` y `R10G10B10A2`. NVIDIA acepta `AYUV`
sólo de entrada y `R16G16B16A16_FLOAT` sólo de salida; Intel no admite `R16G16B16A16_FLOAT` en
ningún sentido. La matriz completa está en el CSV, una fila por adaptador y formato.

## Los dos controles / The two controls

**Sin ellos un cero no dice nada, y esta sonda nació sin el positivo.** / **Without them a zero says
nothing, and this probe was born without the positive one.**

- **Negativo**: dos pasadas con la extensión apagada tienen que salir idénticas. Las dos tarjetas dan
  `control=0`, así que la lectura no inventa diferencias.
- **Positivo**: el `VideoProcessorBlt` tiene que haber dibujado algo. Se cuenta cuántos valores de
  byte distintos trae la imagen leída. **La primera versión de esta sonda no lo tenía**, y un `blt`
  que no dibujara nada habría dejado tres imágenes negras idénticas: cero diferencias y cero control,
  exactamente igual que una superresolución que corre y no cambia nada.
- **Y hay un tercer control, del sistema entero, que llegó solo**: Intel mueve el 54,6 % de la
  imagen con esta misma sonda. Eso prueba que el instrumento **sí detecta** una superresolución que
  funciona, y convierte el cero de NVIDIA en un cero con significado en vez de en un cero sospechoso.
- **Y un cuarto, que vale también para NVIDIA**: encender el realce de bordes **entre dos dibujos**
  cambia 8 084 664 bytes en la propia RTX 5070. Así que su cadena responde a un cambio de
  configuración entre pasadas; el cero de la extensión es de la extensión y no del arnés.

## Lo que la auditoría de puertas encontró, y que invalidaba la medición

`gate-auditor` encontró **siete huecos**, cada uno con su mutación medida. Los que importan:

- **Se podía borrar toda la extensión del fabricante y las pruebas seguían en verde**: `differing`
  no se afirmaba en ninguna parte, así que los 18 109 378 de Intel se volvían cero sin un rojo.
- **`Count == 0 ||` aprobaba «la batería nunca se preguntó»**: con el bucle borrado, el informe
  llamaba a una RTX 5070 «una tarjeta que no admite ningún formato».
- **`ProbeAll` podía devolver vacío** y las dos pruebas se omitían, verdes.
- **La tolerancia de forma de 0,01 era doce veces el error que decía admitir**: encajar el ancho en
  una rejilla de 16 px achataba un PAL de 1800×1440 a 1792×1440 y pasaba.
- **El redondeo documentado no lo ejecutaba ninguna prueba**: truncar en vez de redondear daba verde.
- **La escala del patrón no la medía nadie**: bandas de 32 px en vez de 3 partían en dos la
  diferencia de Intel y todo seguía verde.
- **El informe escribía `0x00000000` tanto para `S_OK` como para «no se intentó»**, así que una
  tarjeta sin procesador de vídeo se leía igual que una que aceptó la extensión.

Los siete están cerrados y **las cinco mutaciones que el auditor usó vuelven a ponerlo rojo**,
comprobado una a una.

## Lo que costó no documentarse / What not reading the documentation cost

**Esta sonda estuvo a punto de registrar dos hallazgos falsos, y los dos eran míos.** / **This probe
was about to record two false findings, and both were mine.**

1. **Intel devolvía `E_INVALIDARG`**. Su estructura no lleva el parámetro, lleva un **puntero** al
   parámetro: 16 bytes en x64, no 8. Corregido, pasó a `E_FAIL`.
2. **Intel devolvía `E_FAIL` en la llamada de versión**, y se iba a escribir que la UHD 770 no tiene
   la interfaz VPE. Es falso: **las dos primeras llamadas de Intel van por
   `VideoProcessorSetOutputExtension` y sólo la tercera por `VideoProcessorSetStreamExtension`** —
   `ToggleIntelVpSuperResolution` de Chromium, en `ui/gl/swap_chain_presenter.cc`. Mandar la primera
   por la puerta equivocada se lee exactamente igual que una tarjeta que no puede hacerlo. Corregido,
   las tres dan `S_OK` y la imagen cambia.

**La lección, que es la regla 0 de este repositorio:** un `E_FAIL` de una API no documentada no es un
hallazgo sobre la máquina hasta haber leído una implementación que funcione.

## El cero de NVIDIA, medido contra cuatro combinaciones en vez de una

**Actualizado el 2026-09-12 por la tarde.** La primera medición varió el interruptor y nada más, así
que su cero decía «con este formato y esta imagen». Las dos cosas que no varió son las dos que un
modelo del fabricante miraría, y ninguna de las dos era la explicación:

| Adaptador | Entrada | Imagen | Bytes distintos de 33 177 600 | Valores distintos |
| --- | --- | --- | --- | --- |
| NVIDIA GeForce RTX 5070 | `YUY2` | bandas | **0** | 6 |
| NVIDIA GeForce RTX 5070 | `YUY2` | detalle | **0** | 256 |
| NVIDIA GeForce RTX 5070 | `NV12` | bandas | **0** | 6 |
| NVIDIA GeForce RTX 5070 | `NV12` | detalle | **0** | 256 |
| Intel UHD Graphics 770 | `YUY2` | bandas | 18 109 378 | 44 |
| Intel UHD Graphics 770 | `YUY2` | detalle | **22 455 544** | 256 |
| Intel UHD Graphics 770 | `NV12` | bandas | 18 109 378 | 44 |
| Intel UHD Graphics 770 | `NV12` | detalle | **22 446 365** | 256 |

**Por qué se midieron esas dos cosas.** El formato, porque tanto VLC como Chromium entregan `NV12`
salido de un decodificador de hardware y esta tubería prefiere el empaquetado; el contenido, porque
la superresolución del fabricante es una red entrenada sobre vídeo comprimido y unas bandas duras con
color plano no le dan nada que reconstruir — el propio comentario del patrón lo daba por supuesto sin
medirlo. La imagen de detalle lleva degradado, textura fina y croma que se mueve.

**Y el cero vale más ahora, porque Intel mueve MÁS con detalle**: 22,4 millones frente a 18,1. El
instrumento **sí** responde al contenido, así que el cero de NVIDIA con la misma imagen no es que la
imagen no le diera trabajo.

**Lo que la documentación decía por adelantado, y se midió igual**: Chromium no comprueba el formato
—admite `NV12`, `YUY2` y `P010`— ni la resolución ni el factor de escala. Sólo el fabricante y **si el
equipo va con batería**. Así que la hipótesis del formato era la débil de las dos, y «con ninguno de
estos» es una frase distinta de «con el único que probamos».

**Lo que queda vivo, y no es de código**: el interruptor de la aplicación de NVIDIA, y la hipótesis de
que la superresolución sólo actúe cuando la imagen se **presenta** en una cadena de intercambio y no
en un dibujo fuera de pantalla que se lee a memoria. Lo segundo sólo se puede comprobar dentro de la
cadena que `PLY-016` tiene que construir.

**Y dos descartes más, medidos el mismo día**: el controlador de esta máquina **pasa** la comprobación
de versión que VLC exige antes de llamar —32.0.16.1656 da 161 656 contra un mínimo de 153 000—, y el
registro de VLC 3.0.23 reproduciendo un vídeo de 480p en esta misma tarjeta dice **«Using Super
Resolution scaler with B8G8R8A8 output»** y **«turning VSR ON»**, sin un solo error. Es decir: la
llamada se acepta igual que la nuestra. Lo que VLC no ha dicho todavía es si mueve un píxel.

## VLC tampoco mueve un píxel con la VSR, y eso CONFIRMA el interruptor

**Medido el 2026-09-12 por la noche con VLC 3.0.23 en la RTX 5070 de esta máquina**, reproduciendo un
fotograma congelado de un vídeo real de 480p a pantalla completa y comparando la pantalla por bytes.

**La clave fue encontrar el cuarto modo.** `--d3d11-upscale-mode` acepta `linear`, `point`,
`processor` y `super`, leídos del binario del plugin. `processor` usa el procesador de vídeo **sin**
la superresolución del fabricante y `super` **con** ella — lo dicen los propios registros de VLC,
«Using Video Processor scaler» frente a «Using Super Resolution scaler» más «turning VSR ON». Ese par
es lo único que aísla la VSR.

| Comparación | Bytes distintos de 33 177 600 | Qué mide |
| --- | --- | --- |
| escritorio → cualquier pasada | 70,6 % | control positivo: VLC dibujó |
| `processor` contra `processor` | **0** | control negativo: dos pasadas idénticas |
| **`processor` contra `super`** | **0** | **la VSR, aislada: no mueve nada** |
| `linear` contra `super` | 50,0 % | el procesador de vídeo entero, no la VSR |

**El cero de VLC vale más que el nuestro**, y por eso cierra la pregunta: es la implementación de un
tercero que funciona, sobre **vídeo real**, con entrada `NV12` salida de un decodificador de hardware,
y **presentándose en una cadena de intercambio** en vez de dibujando fuera de pantalla. Con eso cae
también la hipótesis que quedaba viva — que la superresolución sólo actuara al presentar.

**La primera versión de esta medición dio 50 % y estuvo a punto de escribirse como «VLC sí mejora».**
Comparaba `linear` contra `super`, y `linear` **no crea escalador**: va por un sampler del shader. Ese
50 % es el procesador de vídeo frente a no usarlo, que es una pregunta distinta y ya contestada. Lo
destapó el registro de VLC, no el número.

**Y ese 50 % es la buena noticia para `PLY-016`, leída bien**: el procesador de vídeo **sí** mejora la
imagen en la RTX 5070 — la mitad de los bytes de la pantalla, con un desvío medio de 15 niveles y
máximo de 61 —, y eso es lo que la cadena va a usar. Coincide con lo que la sonda ya medía por otra
vía: el realce de bordes estándar mueve 8 084 664 bytes en esa misma tarjeta. /
**VLC's VSR moves nothing either, with a perfect negative control; the video processor itself moves
half the screen.**

## Por qué el cero de NVIDIA es NO CONCLUYENTE y no «no funciona»

La llamada es **idéntica** a la de Chromium (`ToggleNvidiaVpSuperResolution`): mismo GUID, misma
estructura de tres enteros —versión 1, método 2, interruptor—, mismo
`VideoProcessorSetStreamExtension`. Chromium no comprueba nada antes de llamarla: ni versión de
controlador, ni resolución, ni formato; sólo el fabricante y si el equipo va con batería. Y su propia
nota dice que **el controlador de NVIDIA acepta la llamada y la ignora mientras la función esté
apagada en la aplicación de NVIDIA, que es como viene de fábrica**.

Se declararon además los dos espacios de color que VLC declara —difusión BT.709 a la entrada, RGB
completo a la salida—, y se comprobó que llegan: el número de valores distintos de la imagen de
NVIDIA cambió de 7 a 6 al declararlos. Así que el cero no viene de una llamada a medio hacer.

**Queda una comprobación que no es de código: encender la Súper resolución RTX en NVIDIA App.** Desde
la sesión no se puede leer ese ajuste — la base de perfiles del controlador no nombra la función — y
el propietario eligió medir antes de confirmarlo.

## Lo que cambia para la cadena de PLY-016

- La entrada es **`YUY2`**, y la conversión a BGRA en CPU sale de la ruta de reescalado.
- La salida se pide en **`B8G8R8A8_UNORM`**, que es lo que la composición de Avalonia importa
  (`PlatformGraphicsExternalImageFormat`), así que no hay un segundo cambio de formato al final.
- **Intel es la mitad verificada de `PLY-016`.** NVIDIA está escrita y pendiente de un interruptor;
  AMD sigue escrita-y-no-verificable por falta de máquina, que es decisión de gasto del propietario.

## El nivel del realce salió del archivo excluido — 2026-09-12

**El «máximo» que produjo los dos 24,4 % de arriba se decidía dentro de
`WindowsVideoUpscaleProbe`**, que está `[ExcludeFromCodeCoverage]` de arriba abajo. Era
`range.Maximum` escrito en la llamada, y **ninguna prueba podía llegar a él**: la regla 10 dice que
lo que decide no se excluye nunca, y esto era exactamente eso. Ahora vive en
`D3d11UpscaleFormats.EdgeEnhancementLevel`, con siete casos que lo miden en cualquier máquina.

**Lo que la política dice, y por qué**: el máximo, porque es el único nivel con una medición detrás.
Cualquier otro número sería una suposición con aspecto de política. Si ese nivel es además el que
una persona quiere ver es otra pregunta —el realce compra nitidez con halos— y ésa la firma el
propietario por el ojo, sobre material real.

Y tres formas de no pedir nada, que no son el mismo «no»: un procesador que no ofrece el filtro, un
rango sin margen por encima del defecto del propio controlador —donde encender no puede mover un
píxel—, y un rango que se contradice, que no se obedece.

**La comprobación de que no cambió nada es la única que vale aquí**: la sonda volvió a correr contra
las dos tarjetas de esta máquina y el realce midió **8 084 664** en la NVIDIA y **8 086 614** en la
Intel — **los mismos números, byte por byte, que la tabla de arriba**.

### Y al escribir el nivel en el informe salió algo que nadie sabía

El informe no decía **qué** se había pedido, sólo cuánto se movió. Ahora lo dice, y la primera
ejecución contestó esto:

| Tarjeta | Nivel pedido | Multiplicador | Bytes movidos |
| --- | --- | --- | --- |
| NVIDIA GeForce RTX 5070 | **100** | 1 | 8 084 664 |
| Intel UHD Graphics 770 | **64** | 1 | 8 086 614 |

**A las dos tarjetas NO se les pidió lo mismo** —el máximo de cada una es un número distinto— y aun
así las dos movieron el mismo 24,4 %, con 1 950 bytes de diferencia entre ellas sobre 33 millones.
Eso no es una coincidencia bonita: es un aviso de que **ese 24,4 % probablemente describe el patrón
de prueba —cuántos píxeles tienen un borde al lado— y no la fuerza del realce**. Dos fuerzas que se
diferencian en un 56 % no pueden dar el mismo número si el número midiera la fuerza.

No cambia ninguna conclusión de este documento —el filtro corre, está encendido y mueve píxeles—,
pero sí cambia qué se puede decir del 24,4 %: es «cuántos píxeles toca», no «cuánto los toca». Lo
segundo sigue sin medir, y es lo que una persona ve.

### Y la política nació con una puerta ciega propia, medida en el acto

La comprobación de coherencia del rango **sobrevivió a ser borrada entera con las siete filas en
verde**. Las dos filas escritas para medirla —un máximo por debajo del mínimo, y un defecto por
encima del máximo— eran rechazadas por la comparación siguiente, no por ella.

**Y el arreglo de eso quedó a medias, que lo encontró `gate-auditor` con el dominio enumerado
entero.** De las tres condiciones escritas, **dos no pueden distinguirse de su propia ausencia**:

| Condición | Por qué no mide nada | Cómo se supo |
| --- | --- | --- |
| «defecto por encima del máximo» | siempre que se cumple, la comparación final ya contesta que no | borrada, todo verde |
| «máximo por debajo o igual al mínimo» | para contestar un nivel hacen falta defecto ≥ mínimo y máximo > defecto, y las dos juntas **ya dicen** que el máximo está por encima del mínimo | borrada, todo verde, y **cero entradas** en todo el dominio la separan de su ausencia |

Las dos se quitaron. Queda una sola, «defecto por debajo del mínimo», y su comentario dice por qué
las otras no están: una guarda que no puede fallar no es una protección, es una frase que se lee
como una protección.

**Y faltaba el caso más corriente que existe**: un filtro que viene apagado de fábrica declara su
defecto **en** su mínimo —`min 0, max 100, default 0`—, y ninguna fila lo tenía. Aflojar esa
comparación de «por debajo del mínimo» a «por debajo o igual» dejaba las ocho filas en verde y
**convertía a la tarjeta corriente en una a la que no se le pide nada**: el realce desaparecería sin
un solo rojo. Con la fila puesta, esa mutación cae.

## Reproducir / Reproduce

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet test tests/ApSolutions.LocalMedia.IntegrationTests -c Release -m:1 `
  --settings eng/test.runsettings --filter "FullyQualifiedName~WindowsVideoUpscaleProbe"
```

En un runner hospedado sólo aparece el «Microsoft Basic Render Driver», que **no expone
`ID3D11VideoDevice`**, así que la prueba de píxel se salta allí y lo dice. La aritmética que decide
—formatos, preferencia, patrón, comparación— no depende de ninguna tarjeta y se mide siempre, en
`UpscaleFormatArithmeticTests`.

## Procedencia / Provenance

- Números de ranura de las tablas de funciones: cabeceras del SDK de Windows 10.0.26100.0
  (`um\d3d11.h`, `shared\dxgi.h`, `shared\dxgiformat.h`), leídos el 2026-09-12. **No de la
  documentación publicada**, cuyo orden de métodos difiere: `CreateVideoProcessor` está en la ranura
  4 y `CreateVideoProcessorEnumerator` —que se llama antes— en la 10.
- GUID, cargas útiles y secuencia: `modules/video_output/win32/d3d11_scaler.cpp` de VLC
  (`LGPL-2.1-or-later`) y `ui/gl/swap_chain_presenter.cc` de Chromium (`BSD-3-Clause`).
- Identificadores de fabricante: leídos del bus PCI de esta máquina (`VEN_10DE`, `VEN_8086`). El de
  AMD (`0x1002`) viene del registro PCI y de la fuente de VLC, y **no está medido aquí**.

## La respuesta que faltaba, medida el 2026-09-13: NVIDIA FUNCIONA

**El «NO CONCLUYENTE» de NVIDIA era el interruptor, y nada de este código.** Con la Super Resolución
de Vídeo RTX encendida en NVIDIA App, la misma sonda, la misma máquina y el mismo código:

| Adaptador | Antes | Después | Valores distintos | Coste base | Coste con la mejora |
| --- | --- | --- | --- | --- | --- |
| NVIDIA GeForce RTX 5070 | **0** bytes | **11 585 463** (34,9 %) | 6 → **256** | 0,064 ms | **1,623 ms** (3,9 % del fotograma) |
| Intel UHD Graphics 770 | 18 109 378 | 18 109 378 (54,6 %) | 44 | 3,882 ms | 2,038 ms (4,9 %) |

Control negativo en cero en las dos. Y el salto de **6 a 256 valores distintos** es el control
positivo más fuerte de toda esta medición: la imagen dejó de ser tres negros y un patrón para tener
la riqueza de una reconstrucción real.

### Que el código era correcto está verificado contra dos implementaciones, no deducido

`mpv` envía para NVIDIA el GUID `d43ce1b3-1f4b-48ac-baee-c3c25375e6f7` por
`VideoProcessorSetStreamExtension` con `{version=1, method=2, enable=1}` — **idéntico, campo por
campo, a lo que esta sonda enviaba desde el primer día**. Y el comentario de Chromium dice por qué el
resultado era cero: «the feature is controlled by the NVIDIA Control Panel app and turned off by
default, so calling VideoProcessorSetStreamExtension will be a no-op then».

### Y NO se puede encender desde una aplicación, esta vez medido por diferencia

La pregunta se había contestado buscando **por nombre**; esta vez se contestó comparando el sistema
antes y después de encenderlo a mano: **272 valores de registro de NVIDIA y 1 221 ficheros con su
huella SHA-256**.

- **El registro no cambió: cero diferencias de 272.**
- **`nvdrsdb0.bin` y `nvdrsdb1.bin` no cambiaron**, así que el ajuste **no vive en la base de perfiles
  del controlador** — que era la única puerta que `NvAPI_DRS_SetSetting` podría abrir.
- Lo único que cambió con contenido propio fue `Drs
vAppTimestamps` (mismo tamaño, otra huella) y
  `storage.json`, que resultó ser **la posición de la ventana de NVIDIA App** y nada más.

**Corolario de diseño, y cambia el de arriba:** la superresolución del fabricante sigue siendo un
extra oportunista, pero ahora se sabe **qué la enciende** y que **cuesta 1,6 ms en una RTX 5070**. Lo
que esta aplicación puede hacer es **detectar si está activa** — comparando píxeles, que es lo que
esta sonda ya hace — y decírselo a quien la usa, en vez de encenderla por su cuenta, que no se puede.

**Y un aviso sobre lo que circula por internet**: los identificadores
`NV_VPP_RTX_VIDEO_SUPER_RESOLUTION_ENB` y sus dos hermanos, que aparecen en el issue 396 de
`nvidiaProfileInspector`, **se los inventó un modelo de lenguaje** — lo dice quien los publicó, que
además no los encuentra ni añadiéndolos a mano. No se usaron aquí.

---

## Enmienda del 2026-09-13 — «el registro no cambió» era cierto y engañoso, y el interruptor sigue sin poder encenderse

El propietario insistió en que tenía que existir una forma de encender la superresolución desde la
aplicación, porque **una función que depende de que el usuario vaya a otro programa es una función
que la mayoría de la gente no va a tener**. Se agotaron las cuatro vías. Ninguna funciona, y la
primera además corrige lo escrito arriba.

### 1. El registro SÍ cambia, y la medición anterior no era falsa: era estrecha

Arriba dice «el registro no cambió: cero diferencias de 272». Ese 272 es el número de valores bajo
`HK*\SOFTWARE\NVIDIA Corporation`, y **el ajuste no vive ahí**. Vive en la clave de clase del
adaptador de pantalla:

```
HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000
```

Barriendo ese árbol —1 164 valores— y comparando encendido contra apagado, **cambia exactamente
uno**: `_User_Global_VAL_SuperResolution`, **5 encendido, 0 apagado**. Sus vecinos
(`_Auto` = 2, `_RTXVideoFlags` = 0x100, los `_XEN_` = 0x80000001, un blob `_DAT_`) no se mueven.

**La lección no es el dato, es el método**: un barrido vacío no prueba una ausencia, prueba que se
miró donde no era. La consulta anterior buscó por el nombre del fabricante; la clave la escribe el
fabricante pero **cuelga del dispositivo**.

### 2. Escribirlo no enciende nada, con permisos de administrador y todo

- `_User_Global_VAL_SuperResolution` puesto a 5 desde fuera de NVIDIA App, con elevación
  (sin elevación Windows contesta «Requested registry access is not allowed»).
- Sonda: **0 bytes distintos de 33 177 600** en la RTX 5070.
- **Control positivo en la misma pasada**: la Intel UHD 770 movió **18 109 378 bytes**. El
  instrumento estaba vivo.

### 3. Reiniciar el servicio del controlador tampoco

`NVDisplay.ContainerLocalSystem` reiniciado con el valor ya en 5, y la sonda repetida: **otra vez 0
bytes**, con la Intel moviendo los mismos 18,1 millones.

### 4. La base de perfiles del controlador no participa — ahora con control positivo

La medición de arriba decía que `nvdrsdb0.bin` no cambia, pero **sin una prueba de que el interruptor
se hubiera movido de verdad**. Repetida el 2026-09-13 con esa prueba al lado: el fichero es
**byte a byte idéntico** (1 924 512 bytes, misma huella, `cmp -l` da 0 diferencias) mientras el valor
del registro pasaba de 0 a 5. El clic ocurrió y el fichero no se enteró.

### 5. Y la propia NVIDIA App tampoco lee ese valor

Con el registro en 5 escrito por nosotros, **su interfaz seguía mostrando la función desactivada**
—observado por el propietario—. Ese valor es el **recuerdo** de la decisión, no la decisión: nadie lo
lee, ni el controlador ni la aplicación que lo escribe.

**Conclusión**: NVIDIA App se lo comunica al controlador **en caliente**, por un canal que no deja
rastro en ningún fichero ni clave que una aplicación pueda escribir. El interruptor global no se
enciende desde fuera. Punto final para esa vía.

### Lo que sí existe, y no lo habíamos mirado: el kit de vídeo RTX

Lo encontró el propietario. NVIDIA publica un **RTX Video SDK** cuya superresolución vive en
`nvngx_vsr.dll` y **se ejecuta dentro del proceso que la llama**: sin interruptor global, sin NVIDIA
App, sin permisos de administrador y sin que el usuario toque nada. Es exactamente la autonomía que
se buscaba, y su contrato **permite repartir el binario dentro de una aplicación** —la guía de NGX lo
dice, y pide la marca de NVIDIA en la pantalla «Acerca de»—.

**Lo que lo bloquea no es técnico.** La cláusula 2.5 del contrato de NVIDIA prohíbe usar el kit «de
manera que quede sujeto a una licencia de código abierto», y nombra las que exigen entregar el código
fuente, permitir obras derivadas o redistribuir sin coste. Con la licencia propia del 2026-09-13 esa
cláusula deja de chocar con la licencia de este programa; **lo que sigue bloqueando es el
decodificador de VideoLAN**, compilado con `--enable-gpl` (ver los avisos de terceros). Mientras ese
complemento viaje en el paquete, el kit de NVIDIA no puede entrar.

**Y la superresolución sigue siendo un extra, no la base**: el kit solo corre en tarjetas RTX. El
escalador propio hace falta igual para AMD, Intel y las NVIDIA anteriores, y es el que da imagen
mejorada al 100 % de quien use el programa sin que nadie mueva un dedo.
