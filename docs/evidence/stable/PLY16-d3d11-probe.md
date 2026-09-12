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
