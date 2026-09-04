# Probar en ARM64 con los runners de GitHub

Cómo se ejecuta AP Reelume en una máquina Windows 11 ARM64, qué contesta y qué trampas tiene. La
versión inglesa está en [arm64-ci.en.md](arm64-ci.en.md).

Esta guía existe porque la parte ARM64 va a volver a tocarse cada vez que se implemente algo que
dependa del sistema operativo, y lo que costó averiguar la primera vez no debería costar dos veces.

## Qué hay montado

El trabajo `arm64-matrix` de [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) corre sobre
`runs-on: windows-11-arm` y ejecuta [`eng/package-arm64.ps1`](../../eng/package-arm64.ps1), que
publica el paquete ARM64 y, **sólo cuando el anfitrión es ARM64**, ejecuta la matriz de seis fases.

- **No cuesta nada.** Los runners `windows-11-arm` son gratis e ilimitados en repositorios públicos,
  y éste lo es.
- **Corre en paralelo con el trabajo x64** y lleva su propio reloj, así que no consume el margen que
  mide [`eng/measure-ci-time.ps1`](../../eng/measure-ci-time.ps1).
- **No bloquea todavía**, a propósito. La imagen la mantiene Arm Limited y no es la de x64; un rojo
  por una herramienta que esa imagen no trae se lee como código roto, que es lo único que un rojo no
  debe significar nunca.

## Cómo se lee el resultado, que NO es el color del run

**Un run verde no significa que las fases hayan pasado**, porque el trabajo no bloquea y porque el
guion no falla por una fase sin pasar. Lo que contesta es el artefacto:

```powershell
gh run download <id> -n arm64-matrix-native -D <carpeta>
```

Trae cuatro cosas:

| Archivo | Qué contesta |
|---|---|
| `arm64-probe.txt` | Qué traía la máquina: arquitectura, SDK, Chocolatey, `ffmpeg`, `makeappx` |
| `package-arm64/arm64-matrix.json` | Las seis fases, con `outcome`, `detail` y `reason` |
| `package-arm64/lifecycle.json` | Las fases del ciclo de instalación sobre el paquete ARM64 |
| `package-arm64/matrix/**/*.trx` | **Qué** prueba se omitió, no sólo cuántas |

Los `.trx` están ahí porque la primera vez hubo que deducir las omisiones del log de un trabajo ya
terminado, y eso es arqueología en vez de evidencia.

## Lo que trae la imagen NO se lee: se mide

**Su documentación pública miente sobre sí misma.** El manifiesto de
`actions/partner-runner-images` anunciaba `.NET 10.0.101` y `Chocolatey 2.6.0`; la máquina traía
`10.0.302` —el exacto que fija `global.json`— y `2.7.4`. Por eso el primer paso real del trabajo es
una sonda que se ejecuta pase lo que pase después, y por eso el SDK se instala igualmente: **una
guarda que depende de que un tercero no cambie su imagen no es una guarda**.

Lo que sí conviene saber de antemano, sabiendo que puede cambiar sin avisar:

- **Trae** Chocolatey, Visual Studio 2022, varios Windows SDK con `makeappx.exe` en `x64` **y** en
  `arm64`, PowerShell 7 y `git`. Que `makeappx` esté en `x64` es lo que permite que
  [`eng/find-sdk-tool.ps1`](../../eng/find-sdk-tool.ps1) siga buscando sólo ahí.
- **No trae `ffmpeg`.** Se instala con el mismo paquete y la misma versión fijada que en x64, y ahí
  corre emulado. Eso no contamina la medición: `ffmpeg` **fabrica** las muestras, que son archivos;
  quien las decodifica es LibVLC ARM64 nativo, que es lo que `PRD-003` compromete.
- **Una máquina hospedada sí muestra ventanas GUI.** La fase `native-execution` lo necesita y pasa.

## Las seis fases

La lista vive en **dos sitios que se cruzan**, y una prueba falla si dejan de coincidir: el array
`$matrixPhases` de `eng/package-arm64.ps1` y `RequiredPhases` de
`tests/ApSolutions.LocalMedia.MediaTests/Playback/Arm64PlaybackTests.cs`. **Si añades una fase,
tocas los dos.**

| Fase | Qué pregunta | Qué necesita |
|---|---|---|
| `native-execution` | El host ARM64 arranca y reporta ARM64 | Nada más que la máquina |
| `codec-matrix` | La matriz T19 decodifica de forma nativa | Muestras de `ffmpeg` |
| `hdr-acceleration` | HDR10, conversión de tono y ruta de decodificación | Muestras de `ffmpeg` |
| `audio-output` | Selección de dispositivo y preferencia persistente | Nada: corre el motor en mudo |
| `package-lifecycle` | El ciclo de instalación sobre el paquete ARM64 | `lifecycle.json`, que el guion produce invocando `eng/verify-package.ps1` |
| `cross-architecture-data` | Una biblioteca creada en x64 se abre en ARM64 | Una carpeta de datos escrita en x64, pasada con `-X64DataRoot` |

## Cinco trampas, todas pagadas ya

1. **El trabajo NO corre `verify.ps1` ni la suite entera, y no es un descuido.**
   `Arm64PlaybackTests` vive en `MediaTests` y, **en un anfitrión ARM64, rechaza toda fase que no
   esté `Passed`**. Correrla mientras queden fases sin pasar garantiza un rojo. Cuando las seis
   pasen, esa suite pasa a ser la puerta natural de este trabajo.

2. **Un código de salida cero no significa que se midiera nada.** `CodecMatrixTests` y
   `HdrAccelerationTests` se omiten solas cuando falta `ffmpeg`, y `dotnet test` devuelve 0 igual.
   `Invoke-MediaSuite` cuenta lo ejecutado leyéndolo del `.trx`, **no del resumen de consola**, que
   está traducido: aquí se programa en `es-ES` y CI corre en `en-US`.

3. **El listón es «algo se ejecutó y pasó», no «cero omisiones».** El paquete `ffmpeg` de Chocolatey
   no trae `libsvtav1` ni `libxavs2` y multiplexa la muestra HDR10 sin sus metadatos de transferencia
   de color, así que tres pruebas se omiten — **y el runner x64 omite las mismas**. Exigir cero
   omisiones ataría el desbloqueo de `PRD-003` a que un tercero empaquete un codificador.

4. **`| Write-Output` dentro de una función de PowerShell destruye su valor de retorno.** Quien la
   llama recibe un array cuyo último elemento es el objeto, y preguntarle al array por las
   propiedades del objeto **contesta que no**, en silencio. Hay una prueba que lo prohíbe, y mira las
   sentencias y no los comentarios — porque se cazó a sí misma la primera vez.

5. **El informe del ciclo de instalación se llama `lifecycle.json` y lo escribe
   `eng/verify-package.ps1` en la raíz que se le pasa.** La fase lo buscaba con otro nombre y en otra
   carpeta, así que decía «falta la máquina» con la máquina delante. Un bloqueo significa que la
   máquina no puede contestar, **nunca** que el guion preguntó mal.

## Cuánto cuesta

**No se escribe aquí una duración fija**, porque es un dato que siempre acabará desfasado. El primer
run nativo costó nueve minutos de punta a punta, seis de ellos empaquetando, y el techo del trabajo
está puesto en treinta con esa medición al lado. Para saber cuánto cuesta hoy:

```powershell
pwsh -NoProfile -File eng/measure-ci-time.ps1 -Detailed
```

Si el trabajo sano se acerca al techo, lo que ha crecido es el trabajo, no el techo el que se quedó
corto.

## Lo que no se hace

**Emular ARM64 no sustituye a esto.** Lo que `PRD-003` compromete es que código ARM64 **nativo**
decodifique y reproduzca; una capa de traducción mide la capa de traducción. Está escrito en
[`docs/evidence/stable/T42-arm64.md`](../evidence/stable/T42-arm64.md), junto con lo que contestó
cada fase la primera vez.
