# PLY-016 — El eslabón portátil: un shader propio que estrecha el borde a la mitad / The portable link: our own shader halves the edge ramp

- Fecha / Date: 2026-09-13
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  SkiaSharp 3.119.4.
- IDs: `PLY-016=IN_PROGRESS`
- Pruebas re-ejecutables / Re-runnable tests:
  `tests/ApSolutions.LocalMedia.Domain.Tests/Playback/UpscaleChainPolicyTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Player/SkiaShaderHarnessTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Player/UpscaleDrawPlanTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Player/UpscaleShaderSourceTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Player/SkiaUpscaleDrawOperationTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Player/VideoUpscaleQualityTests.cs`

## Veredicto / Verdict

**Un vídeo por debajo de la caja en la que se dibuja se ve más nítido sin que nadie toque nada, y está
medido en tinta: la rampa de un borde duro ampliado cuatro veces baja de 4 píxeles a 2.** El perfil
pasa de `31,95,159,223` a `3,86,169,252` — el lado oscuro se va a 3 y el claro a 252. Apagado, el
fotograma sale byte a byte como salía. /
**A video below the box it is drawn in looks sharper with nobody touching anything, measured in ink:
the ramp across a hard edge enlarged four times falls from 4 pixels to 2.** The profile goes from
`31,95,159,223` to `3,86,169,252`. Switched off, the frame comes out byte for byte as it did.

## Lo primero, y no era código: ¿puede el arnés ver un shader?

`PLY-016` acaba dibujando, y el arnés de rasterización de este repositorio usa render por software.
Antes de construir nada había que saber si una puerta escrita sobre él vería algo — porque un
silencio se lee como «no cambió nada», que es el peor verde que este árbol conoce.

**La documentación no lo contesta.** Los cinco ejemplos de `SKRuntimeEffect` de SkiaSharp,
consultados por Context7, son todos de GPU — OpenGL, Metal, WebGL, una cadena de intercambio — y
ninguno dice si un shader SkSL corre sobre un lienzo por software. Inconcluyente no es «no».

**Y la API no era la que parecía**, que es la lección de la ruta GPU otra vez: el método es
`SKRuntimeEffect.CreateShader(sksl, out errors)` y **no existe ningún `SKRuntimeEffect.Create`** en
3.119.4. Medido por reflexión sobre el ensamblado antes de escribir una línea, junto con
`ISkiaSharpApiLeaseFeature.Lease()`, `ICustomDrawOperation`,
`OptionalFeatureProviderExtensions.TryGetFeature<T>()`, `SKMatrix.CreateScaleTranslation` y
`SKCanvas.DrawImage(SKImage, SKRect, SKSamplingOptions, SKPaint)`.

### La sonda, con sus cuatro controles

`SkiaShaderHarnessTests` monta una marca verde de 100×40 y cuenta **4.000 píxeles exactos**, no «más
de mil»: una marca que llegue a un cuarto de su tamaño es una conclusión distinta.

| Pregunta | Respuesta |
| --- | --- |
| Un visual ordinario deja tinta que el barrido cuenta (instrumento) | **4.000** |
| Una escena vacía no deja tinta (negativo de escena) | **0** |
| Una operación que corre y no pinta deja el papel blanco (negativo de operación) | **0**, con la constancia de que sí arrendó el lienzo |
| El shader de la sonda compila | sí, sin una queja |
| **Una operación de dibujado propia llega al fotograma capturado** | **4.000** |
| **Un shader SkSL llega al fotograma capturado** | **4.000** |
| ¿Skia dibuja aquí en GPU o en software? | **software**: `GrContext` es nulo |

**Ese último es el que hace que la lectura valga para CI.** El runner hospedado sólo tiene el
«Microsoft Basic Render Driver», que no expone `ID3D11VideoDevice` y bloquea la mitad D3D11 de
`PLY-016`; no bloquea a Skia, que aquí ya dibujó el shader **sin** contexto de GPU.

**Dos mutantes mataron la sonda antes de creerle**: el shader pintando el color del papel mató sólo
la prueba del shader —así que el barrido mide el shader y no otra cosa—, y el SkSL roto mató además
la de compilación, que nombró el error exacto. Ésa es la que distingue «shader roto» de «arnés
ciego», y sin ella las dos serían un mismo silencio.

## La fuerza del realce está medida, no elegida a ojo

A **0,3** la rampa se queda en **4** —perfil `18,91,164,237`, que es el ancho de la propia composición
y por tanto ninguna mejora que alguien vea— y a **0,6** baja a **2**, perfil `3,86,169,252`. El número
es la diferencia entre pasar la puerta y no pasarla.

**Y lo que esa medición NO puede ver es un halo**, que conviene decirlo en vez de insinuar lo
contrario: la escena es negro contra blanco, así que un exceso por cualquiera de los dos extremos se
recorta invisiblemente. Si una fuerza mayor dibujaría un contorno alrededor de todo es el **juicio
visual del propietario** sobre material real, que el criterio de aceptación pide por su nombre.

## La cadena de descarte, como aritmética

`UpscaleChainPolicy` en `Domain`, sin E/S y sin tarjeta. De mejor a peor: superresolución del
fabricante → realce del procesador de vídeo → escalador portátil → remuestreo cúbico → la
composición. **Las dieciséis combinaciones de capacidad están escritas a mano**, porque calcular lo
esperado con la misma regla que usa el código haría que una regla equivocada estuviese de acuerdo
consigo misma.

**No hay un miembro que signifique «nada».** El fondo de la cadena es lo que la aplicación ya hacía,
así que «apagado» es un camino de dibujado real y no una ausencia — que es lo que mantiene «apagado»
distinguible de «no se midió nada».

**`UpscaleCostPolicy` tiene por fin un llamante**, y con él una trampa nombrada:
`UpscaleCostPolicy.Measure` contesta `default` cuando la medición no pudo ocurrir, y
`default(UpscaleCost).FitsBudget` es `false`. Un llamante que preguntara «¿cabe?» de un coste sin
medir apagaría todos los eslabones en una máquina que nadie había cronometrado. Por eso
`WithoutWhatItCannotAfford` toma `UpscaleCost?` y `null` no apaga nada.

## Dónde se inserta, y por qué no en el motor

En `VideoFrameView.Render`, que es donde el reescalado **ya ocurre** cada fotograma. Tres razones
medidas:

- **El motor no conoce el tamaño de la caja**, y `UpscalePolicy.Decide` lo necesita. Llevarlo allí
  pediría un canal de vuelta de `Presentation` a `Infrastructure`.
- **No añade ni una copia.** El array que el escalador lee es el que `OnFrameRendered` ya asignaba y
  tiraba cada fotograma; retenerlo no copia nada, y ser un array nuevo por fotograma es lo que lo
  hace seguro de leer en el hilo de render mientras se descodifica el siguiente.
- **Sustituye trabajo en vez de añadirlo**: la composición ya reescala aquí, y esto dibuja en su
  lugar.

## El shader es propio, y la razón es de licencia

FSR 1 de AMD es lo obvio de portar y su licencia lo permite, **pero exige atribución en los avisos de
terceros — y `ThirdPartyNoticeTests` lee el fichero de bloqueo de paquetes**, así que un shader
pegado en el fuente traería una obligación que ninguna puerta de este árbol puede ver. Con la licencia
propia de tres días y la publicación ya bloqueada por un complemento GPL, asumir una obligación
invisible es el intercambio equivocado.

Lo que hay en su lugar es una **máscara de desenfoque**, que es aritmética y no el código de nadie:
se resta del centro la media de sus cuatro vecinos y se devuelve la diferencia amplificada. El paso
se mide en **un píxel de origen** y no de destino: a cuatro aumentos, un paso de destino muestrea
cuatro veces dentro del mismo píxel de origen y resta un desenfoque idéntico al centro — un shader que
cuesta un fotograma y no cambia nada.

## Nunca deja la pantalla negra

Un fotograma es lo que alguien está mirando, así que un fallo callado aquí es una pantalla negra. Las
tres formas de fallar acaban en el mismo `DrawBitmap` que la composición habría hecho: sin lienzo
Skia, con un shader que no compila, y con un tamaño de origen degenerado. **Las tres están probadas
midiendo tinta**, no un valor de retorno: «cayó al eslabón siguiente» sólo vale algo si salió una
imagen.

## La auditoría de puertas, que encontró once huecos en lo que acababa de escribirse

`gate-auditor`, sobre una copia aislada del árbol y midiendo cada mutante contra la suite **entera**.
Nueve mutantes sobrevivían. Los tres peores, arreglados:

1. **La suite era ciega al color.** `SKColorType.Bgra8888` → `Rgba8888` sobrevivía las 1.437 pruebas:
   todo vídeo ampliado saldría con el rojo y el azul cambiados y nada lo diría. La causa es fina —
   todas las imágenes de prueba eran grises, y la única con color de toda la suite era **verde puro,
   el único color idéntico en los dos órdenes de bytes**. Es la invariancia que la sonda usa a
   propósito, vuelta contra la suite. Ahora hay una prueba que dibuja **rojo** y lee el canal según el
   formato que la captura declara, y el mutante muere en tres pruebas.
2. **La matriz del muestreador podía perder el desplazamiento de las barras.** Sustituirla por un
   `CreateScale` sobrevivía, y un 4:3 en una ventana 16:9 se habría deslizado arriba e izquierda
   **dentro de su propia caja**. Invisible porque las dos escenas eran ortogonales: la que tenía borde
   dibujaba en el origen sin barras, y las que tenían barras eran de color plano, donde mover el punto
   de muestreo no cambia nada. Ahora hay una escena con borde **y** barras.
3. **La pregunta que la sonda existe para contestar no podía fallar.** La pintura de respaldo era
   verde y el shader devolvía verde, así que borrar la asignación del shader dejaba la prueba en
   verde: Skia simplemente usaba el color de la pintura. **La sonda no distinguía «el shader corrió»
   de «el shader se ignoró», y sobre esa lectura se decidió toda la arquitectura.** Ahora la pintura
   es **roja** y el shader devuelve verde — y vuelto a medir así, **el shader sí corre**: 4.000 verdes.

Y los otros:

- **El `[ExcludeFromCodeCoverage]` sí tapaba decisiones**, incumpliendo la regla 10, y su
  justificación escrita era falsa por sus dos mitades. Cuatro decisiones vivían dentro: el orden de
  bytes, el núcleo de remuestreo, la matriz y la rama de ruta. Las tres primeras son ahora miembros
  con nombre de `UpscaleDrawPlan`, con sus pruebas; lo que queda tras el atributo sigue a `Route`.
- **El núcleo de remuestreo no tenía puerta**: `CatmullRom` → `Mitchell` sobrevivía mientras el
  comentario de encima argumentaba justo lo contrario.
- **Dos suelos antiblindaje ausentes.** El peor: «se sirve encendido» pasaba con la ruta ampliada
  dibujando **nada en absoluto** —medido, nueve pruebas rojas y ésa verde—, así que la única prueba
  que sostiene la promesa del propietario habría dado por encendida una pantalla negra.
- **`SourceStep` no distinguía X de Y**: todas sus filas eran isótropas. Hoy es equivalente en
  producción porque `VideoFitPolicy.Fit` conserva la forma, pero esa equivalencia es de otra pieza.
- **La mitad del coste estaba registrada y nadie la alimentaba**, que es el defecto de esta casa en mi
  propio trabajo: `WithoutWhatItCannotAfford` sólo la llamaba su fichero de pruebas. **Borrada**, con
  sus seis pruebas, en vez de conservar una sexta pieza muerta. Cablearla de verdad pide un reloj en la
  ruta de dibujado, y eso va con la medición de coste por fotograma.
- **Compilar el shader una sola vez no lo mide nada**: borrar la guarda lo compila 173.000 veces en
  una película de dos horas y la suite entera sigue verde. Queda con tarea propia, porque lo que
  necesita es una costura y no un comentario más alto.

**La lección que se lleva la tanda: doce mutantes muertos por mí y nueve supervivientes encontrados
por el auditor.** Escribir la prueba y comprobar que puede fallar no es lo mismo, y el auditor mide
contra la suite entera mientras yo medía contra un filtro.

## Dos puntos ciegos que aparecieron al mutar, y no antes

1. **«Apagada, nada cambia» comparaba dos cosas que un mutante movía juntas.** Ignorar el interruptor
   dejaba «apagado» y «la composición» midiendo lo mismo —los dos mejorados—, así que una prueba
   puramente relativa daba por bueno un interruptor roto. Ahora exige además que encendido y apagado
   **difieran**.
2. **La vista repetía dos guardas que la política ya tenía**, y un mutante que invirtió la local no
   mató nada: `Choose` contestaba lo mismo. Una guarda que ninguna prueba puede distinguir de su
   propia ausencia no es una guarda. Las dos reglas viven ahora en un solo sitio.

Y un suelo antiblindaje que nació mal: exigía más de dos valores distintos en la captura, y **un
borde duro dibujado a su propio tamaño tiene exactamente dos**. Lo que prueba que la captura es la
escena es que estén **los dos lados** del borde, leídos en el canal verde porque cada byte de alfa es
255 y satisfaría «blanco» por sí solo.

## Cobertura

Los tres ficheros nuevos a **100/100**, medido fusionando los JSON de coverlet de `Domain.Tests` y
`UiTests` con la aritmética de la puerta. Llegar ahí pidió la costura de la **regla 10**:
`UpscaleDrawPlan` es puro y se afirma entero, y `[ExcludeFromCodeCoverage]` cubre sólo
`DrawThroughCanvas`, donde no queda más que la creación de los objetos de Skia y las llamadas de
dibujado. Lo que esa parte pone en pantalla se afirma en tinta, que es una afirmación más fuerte que
una cuenta de líneas.

**`VideoFrameView.cs` sube de 100/89 a 100/94** y la puerta pedirá su suelo nuevo. Está previsto:
`eng/preview-coverage-floors.ps1 -Suites Domain.Tests,UiTests,AccessibilityTests` lo nombró antes de
empujar, y el suelo se copia del artefacto `coverage-debt` del run que lo mida — nunca de una
ejecución local.

## Lo que NO se puede afirmar todavía

- **La mitad del fabricante no puede llegar a un fotograma en esta arquitectura.** Lo medido es que
  `Compositor.TryGetCompositionGpuInterop()` **contesta nulo**, así que no hay forma de entregar la
  textura a la composición. La vía que queda es leerla de vuelta a memoria del sistema, que a 4K son
  **33 MB por fotograma** — y **eso es el tamaño, no un tiempo**: los costes por fotograma de la sonda
  restan la lectura de vuelta a propósito (`UpscaleCostPolicy.MeasureDifference` existe para eso), así
  que nadie la ha cronometrado. Que no quepa en el tercio de fotograma de
  `UpscaleCostPolicy.MaximumFrameShare` es una **inferencia del tamaño**, y si alguna vez hace falta
  decidirlo se mide. Lo desbloquea un decodificador que no sea copyleft y después el kit de vídeo RTX,
  que corre dentro del proceso y no necesita ni textura compartida ni lectura de vuelta.
- **AMD sigue sin verificar** por una decisión de gasto del propietario.
- **El indicador que el criterio exige no existe todavía.** Su sitio es `VideoStatusOverlay`, que ya
  está montado, y la restricción está medida: **`Render` no puede notificar un cambio** —asignar una
  propiedad de layout durante el pase de dibujado invalida la medida desde dentro del commit del
  compositor y lanza—, así que tiene que avisar de forma diferida y sólo cuando el eslabón cambie.
- **El juicio visual final lo firma el propietario**, que es parte del criterio y ninguna prueba lo
  sustituye.

---

## English

### Verdict

**A video below the box it is drawn in looks sharper with nobody touching anything, and it is measured
in ink: the ramp across a hard edge enlarged four times falls from 4 pixels to 2**, the profile going
from `31,95,159,223` to `3,86,169,252`. Switched off, the frame comes out byte for byte as it did.

### The first thing, and it was not code: can the harness see a shader?

`PLY-016` ends in drawing, and this repository's rasterisation harness renders in software. Before
building anything, it had to be known whether a gate written on it would see anything at all — a
silence reads as «nothing changed», which is the worst green this tree knows.

**The documentation does not answer it.** SkiaSharp's five `SKRuntimeEffect` samples are all GPU
surfaces and none says whether SkSL runs on a software canvas. Inconclusive is not «no».

**And the API was not what it looked like**: the method is
`SKRuntimeEffect.CreateShader(sksl, out errors)` and there is **no `SKRuntimeEffect.Create`** in
3.119.4 — measured on the assembly before a line was written.

The probe answers both halves with four controls: a custom draw operation reaches the captured frame
(**4,000 pixels exactly**), an SkSL shader reaches it too (**4,000**), and Skia is drawing **in
software** here (`GrContext` is null) — which is what makes the reading hold for a CI runner with no
adapter. Two mutants killed the probe before it was believed: a shader painting the paper's own
colour, and broken SkSL.

### The chain, as arithmetic

`UpscaleChainPolicy` in `Domain`, no I/O and no card. Best to worst: the vendor's super resolution,
the video processor's enhancement, our portable upscaler, cubic resampling, the composition. All
sixteen capability rows are written out by hand, because computing the expected link from the same
rule the implementation uses would let a wrong rule agree with itself.

There is **no member meaning «nothing»**: the bottom of the chain is what the application already
did, so «off» is a real drawing path rather than an absence.

`UpscaleCostPolicy` finally has a caller, and with it a named trap: `Measure` answers `default` for a
measurement that could not happen, and `default(UpscaleCost).FitsBudget` is `false`. A caller simply
asking «does it fit?» would switch every link off on a machine nobody had timed.

### Where it goes, and why not in the engine

In `VideoFrameView.Render`, where the rescaling already happens every frame. The engine does not know
the size of the box and `UpscalePolicy.Decide` needs it; the array the upscaler reads is the one the
view was already allocating and discarding, so it adds no copying; and it replaces work rather than
adding it.

### The shader is ours, for a licence reason

AMD's FSR 1 is the obvious port and its licence permits it, but it requires attribution in the
third-party notices — and `ThirdPartyNoticeTests` reads the package lock file, so a shader pasted into
source would carry an obligation no gate here can see. What is here instead is an unsharp mask, which
is arithmetic rather than anybody's code, stepping by **one source pixel** because a destination-pixel
step samples inside the same source pixel four times at a four-times enlargement.

### It never leaves the screen black

All three ways it can fail end at the same `DrawBitmap` the composition would have done, and all
three are asserted in ink rather than by a return value.

### Two blind spots that only showed up under mutation

«Off changes nothing» compared two things a mutant moved together, so it now also requires on and off
to **differ**; and the view repeated two guards the policy already had, which a mutant proved
indistinguishable from their own absence.

### And eleven more the gate auditor found in what had just been written

Nine mutants were surviving the whole suite. The three worst: **the suite was colour-blind** — naming
the other byte order swaps red and blue in every enlarged video, and every test picture was grey except
one that was pure green, the single colour identical in both orders; **the sampler's matrix could lose
the letterbox offset**, sliding a 4:3 episode up and left inside its own box, invisible because the
scene with an edge had no bars and the scenes with bars were flat colour; and **the probe's central
question could not fail**, because its fallback paint and its shader were both green, so «the shader
ran» and «the shader was ignored» read the same — and the architecture was chosen on that reading. Re-
measured with the paint in red, the shader does run.

Also fixed: the coverage exclusion did hide four decisions and its written justification was false on
both halves; the resampling kernel had no gate while its comment argued for it; two anti-blindness
floors were missing, the worse one letting «it ships switched on» pass with the enhanced route drawing
nothing at all; and `SourceStep` could not tell its axes apart. **And the cost half was registered and
fed by nobody** — this repository's defining defect, in my own work — so it was deleted with its six
tests rather than kept as a sixth instance.

### Coverage

The three new files at **100/100**, merged with the gate's arithmetic. Reaching it needed rule 10's
seam: `UpscaleDrawPlan` is pure and fully asserted, and `[ExcludeFromCodeCoverage]` covers only
`DrawThroughCanvas`. **`VideoFrameView.cs` rises from 100/89 to 100/94** and the gate will ask for its
new floor, which is copied from a CI `coverage-debt` artefact and never from a local run.

### What cannot be claimed yet

The vendor half cannot reach a frame in this architecture. What is measured is that the compositor
answers it has no GPU interop; the remaining route is reading the texture back, which is 33 MB a frame
at 4K — a **size and not a timing**, because the probe's per-frame costs subtract the read-back on
purpose. That it will not fit one third of a frame is an inference from the size, and it is measured
the day it has to decide anything. AMD is unverified by a spending decision. The indicator the criterion asks for does not
exist yet, and its constraint is measured: `Render` cannot raise a change notification. The final
visual judgement is the owner's.
