# PLY-016 — Dónde se puede medir el píxel de una ruta GPU / Where a GPU route's pixel can be measured

- Fecha / Date: 2026-09-12
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  SkiaSharp 3.119.4.
- IDs: `PLY-016=IN_PROGRESS`
- Pruebas re-ejecutables / Re-runnable tests:
  `tests/ApSolutions.LocalMedia.UiTests/Player/GpuCompositionHarnessTests.cs`

## Veredicto / Verdict

**La puerta no nace ciega, pero se parte en dos, y la documentación de Avalonia está mal en tres
sitios.** El arnés de rasterización de este repositorio **sí** ve la capa de composición —donde
termina la cadena de `PLY-016`—, y **no** tiene la ruta de importación GPU, que contesta que no
existe en vez de colgarse. /
**The gate is not born blind, but it splits in two, and Avalonia's documentation is wrong in three
places.**

## La pregunta 1: ¿ve el arnés lo que la cadena va a dibujar?

La documentación de Avalonia avisa de que `RenderTargetBitmap` renderiza por software y de que los
controles con interop «pueden no renderizarse correctamente» capturados así — y `CaptureRenderedFrame`
es esa captura. Si la respuesta fuera «no», un silencio se leería como «no cambió nada», que es el
peor verde que conoce esta casa.

| Escena | Qué debe pasar | Medido |
| --- | --- | --- |
| Un `Border` verde de 100×40 dibujado por `Render` | debe sonar | **4 000** píxeles verdes |
| Escena vacía sobre papel blanco | debe callar | **0** píxeles verdes |
| Un `CompositionSolidColorVisual` de 100×40 como visual hijo | la pregunta | **4 000** píxeles verdes |

**Exactamente 100 × 40 = 4 000, ni uno más.** Un visual de composición **no** lo dibuja `Render`: se
entrega al compositor y se compone en el hilo de render, que es la misma puerta por la que va a
entrar la textura importada. Esa puerta está abierta para el arnés.

**Comprobado por mutación**, que es lo que separa un verde de una medición: invertida la aserción a
«cero», la prueba falla nombrando los 4 000, y la huella del binario cambia entre las dos ejecuciones
—medida con `Get-FileHash` antes y después—, así que lo que corrió fue el código nuevo.

La marca es verde a propósito: el verde ocupa el byte 1 tanto en `Rgba8888` como en `Bgra8888`, así
que el barrido no puede confundirse por el orden de canales que ya costó una lectura equivocada en
este árbol. El formato se afirma igualmente, para que la esquiva quede escrita y no supuesta.

## La pregunta 2: ¿existe la ruta GPU en el arnés?

**No, y contesta que no.** `Compositor.TryGetCompositionGpuInterop()` devuelve nulo bajo
`AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }`. La prueba exige además que el
compositor **conteste** —con un tope de diez segundos y una aserción de que la tarea se resolvió—,
porque un cuelgue y un «no hay» se leen igual desde fuera.

## Lo que eso decide: dónde va cada puerta

- **Lo que decide** —qué textura, de qué tamaño, con qué orientación y en qué formato llega a la
  superficie— se mide en el arnés, con un visual de composición dibujado por software. Es la regla 10
  de `CLAUDE.md` otra vez: lo que habla con la máquina se separa de lo que decide.
- **Lo que habla con la tarjeta** —la importación D3D11 y el keyed mutex— se mide donde vive el
  píxel, leyendo de vuelta la textura con una textura de staging. Es lo que
  `WindowsVideoUpscaleProbe` ya hace, y de ahí salieron los 54,6 % de Intel.

## La documentación oficial está mal en tres sitios, medido en el ensamblado

`Avalonia.Base 12.1.1.0`, 2 121 tipos, recorrido por reflexión buscando `ImportGpu|ImportImage|ImportSemaphore`:
**13 coincidencias y ninguna se llama `ImportGpuImage`.**

| La documentación dice | El ensamblado tiene |
| --- | --- |
| `compositor.ImportGpuImage(props, handle)` | **no existe**; es `ICompositionGpuInterop.ImportImage(IPlatformHandle, PlatformGraphicsExternalImageProperties)` |
| `compositor.ImportGpuSemaphore(handle)` | **no existe**; es `ICompositionGpuInterop.ImportSemaphore(IPlatformHandle)` |
| `surface.UpdateWithKeyedMutexAsync(image)` | toma **tres** argumentos: `(image, acquireIndex, releaseIndex)` |

La ruta real, entonces:

1. `await compositor.TryGetCompositionGpuInterop()` → `ICompositionGpuInterop?`
2. `interop.ImportImage(handle, properties)` → `ICompositionImportedGpuImage`
3. `surface.UpdateWithKeyedMutexAsync(image, acquireIndex, releaseIndex)`

`PlatformGraphicsExternalImageProperties` lleva `Width`, `Height`, `Format`, `MemorySize`,
`MemoryOffset` y `TopLeftOrigin`; el formato sólo admite `R8G8B8A8UNorm` y `B8G8R8A8UNorm`.
`KnownPlatformGraphicsExternalImageHandleTypes` declara `D3D11TextureNtHandle` y
`D3D11TextureGlobalSharedHandle`, y `CompositionGpuImportedImageSynchronizationCapabilities` declara
`KeyedMutex`.

**Y el anfitrión real tiene con qué**, medido sobre los 25 ensamblados de Avalonia 12.1.1:
`Avalonia.Skia.GlSkiaExternalObjectsFeature` implementa `IExternalObjectsRenderInterfaceContextFeature`,
y `Avalonia.Win32` trae `AngleExternalMemoryD3D11Texture2D`,
`AngleExternalMemoryD3D11ExportedTexture2D` y los proxies de `IDXGIKeyedMutex`. **La cadena es
construible sobre esta versión.**

**Lo que esto NO contesta, y no se supone**: qué backend elige la aplicación real. `Program.cs` usa
`UsePlatformDetect()` sin opciones, y `Win32RenderingMode` ofrece cuatro —`Software`, `AngleEgl`,
`Wgl`, `Vulkan`—; el orden por defecto no está en la documentación XML del paquete y el tipo de
opciones no se deja instanciar fuera de la aplicación, porque su inicializador de módulo pide código
nativo. La importación D3D11 existe **en la ruta ANGLE**, así que si el anfitrión resolviera a Vulkan
o a software la cadena entraría por otra puerta o por ninguna. **Eso se contesta ejecutando la
aplicación de verdad, y hoy nada de este árbol lo hace**: se comprobó buscando `UsePlatformDetect` en
`tests/` y en `eng/`, y no aparece en ninguno — el paseo «físico» de `AccessibilityTests` también es
headless, y su propia cabecera lo dice («as far as a headless harness can carry it»). Lo que headless
no puede probar vive como guion manual de diez minutos en `audit-walkthrough.md`.

**Así que la sonda del backend real es trabajo pendiente y hay que decidir dónde vive**: o una línea
más en ese guion manual, o un ejecutable de diagnóstico que abra una ventana, pida el interop al
compositor y escriba qué contestó. Lo segundo es lo que convierte la respuesta en una medición
repetible; lo primero no cuesta nada y depende de que alguien lo mire.

## La lección, que vale más que el dato

**La documentación de un fabricante es el primer paso, no el último.** Aquí acertó al nombrar la
capacidad y falló al nombrar los métodos, así que consultarla evita inventarse la API y **no** evita
equivocarse de firma. Lo que cierra la pregunta es el ensamblado, y cuesta una llamada.
