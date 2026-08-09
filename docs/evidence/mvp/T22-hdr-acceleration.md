# T22 — Aceleración, HDR10 y conversión SDR / Acceleration, HDR10, and SDR tone mapping

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `7637f76`
- Commit de tarea / Task commit: `feat: report HDR and acceleration with safe fallback`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, LibVLC 3.0.23.1,
  NVIDIA GeForce RTX 5070, dos ASUS ProArt PA279CRV con HDR activo
- IDs: `PLY-003=IN_PROGRESS`, `PLY-015=OUT_OF_SCOPE`

## RED y GREEN / RED and GREEN

`VideoOutputPolicyTests`, `HdrAccelerationTests` y `VideoStatusOverlayTests` se escribieron antes que
la política. RED falló porque `DisplayCapabilities`, `VideoSourceCapabilities` y `VideoOutputPolicy`
no existían; la salida se conserva en `artifacts/test-results/T22/red/`. / The three plan-named test
files were written before the policy existed; RED failed for missing types and is retained above.

GREEN ejecuta 363 pruebas con 0 fallos y 0 omitidas en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T22/green/`. La cobertura de líneas del código nuevo es 87,50 %
(196/224) y `VideoOutputPolicy` alcanza 100 % de ramas. `dotnet format` y ambas compilaciones
terminan con 0 advertencias. / GREEN runs 363 tests with zero failures and zero skips; new-code line
coverage is 87.50% and the output policy reaches 100% branch coverage.

## Lo que se informa y de dónde sale / What is reported and where it comes from

`VideoOutputPolicy` decide desde dos hechos: lo que declara la fuente y lo que puede la pantalla
**en este momento**. La aceleración es una petición que puede fallar; fallar cambia el decodificador,
nunca la ruta de salida, y nunca detiene la reproducción. / The policy decides from what the source
declares and what the display can do right now; acceleration is a request that may fail, and failing
changes the decoder rather than the output path.

- HDR10 con pantalla HDR activa → paso directo `Hdr10Passthrough`.
- HDR10 con pantalla sin HDR, o con HDR apagado en Windows → `SdrToneMapped`.
- Fuente SDR → `Sdr` en cualquier pantalla.
- Aceleración pedida y no disponible → software, con `FellBackToSoftware` visible.

El fallback ocurre **una sola vez** por motor: `HardwareAccelerationFallback.TryFallBack()` devuelve
`true` la primera vez y `false` después, de modo que un decodificador defectuoso no puede hacer que
la sesión oscile entre hardware y software. Verificado con medio real: tras el fallback la
reproducción continúa y siguen decodificándose fotogramas. / The fallback happens once per engine,
verified with real media: playback continues and frames keep decoding afterwards.

## Detección de HDR en la fuente / Source HDR detection

La clasificación mira la **característica de transferencia declarada**, no el contenedor ni el
nombre. Con ffprobe sobre las muestras generadas: `mkv-hevc-hdr10` declara `smpte2084` y se clasifica
`Hdr10`; `mkv-hevc-sdr` declara `bt709` y se mantiene `None`. Un origen SDR nunca se promociona. /
Classification reads the declared transfer characteristics; the HDR sample declares the perceptual
quantiser and the SDR one does not, and an SDR source is never promoted.

## Estado de la pantalla verificado en el sistema / Display state verified against the system

`WindowsDisplayCapabilityProvider` consulta la configuración de pantalla de Windows y devolvió
`supportsHdr10=True`, `hdrEnabled=True`, `paths=2 queried=2 refused=0`, coincidiendo con lo que el
sistema informa para ambos ASUS ProArt PA279CRV (`BT2020RGB`, `BT2020YCC`, `Eotf2084Supported`). Dos
consultas consecutivas coinciden. Cuando la consulta no funciona, el proveedor informa **ausencia de
HDR** y registra el motivo, en lugar de suponer que la hay. / The provider queries the live Windows
display configuration, agreed with what the system reports for both monitors, and is consistent
across calls; when the query fails it reports no HDR and records why, rather than assuming.

Durante la implementación, la primera versión de la interoperabilidad declaraba
`DISPLAYCONFIG_RATIONAL` como un campo de 64 bits. El tiempo de ejecución lo alineaba y desplazaba
todos los campos posteriores, y la consulta devolvía `ERROR_INVALID_PARAMETER` (87) para cada
pantalla. Declararlo como dos campos de 32 bits es lo que hace que la lectura sea correcta; el
diagnóstico quedó en el propio proveedor para que un fallo futuro sea distinguible de "no hay HDR". /
An early version of the interop declared a native rational as one 64-bit field, which made the
runtime realign every field after it and the query fail with invalid-parameter for every display;
declaring it as two 32-bit fields is what makes the read correct, and the diagnostic stayed in the
provider so a future failure is distinguishable from "no HDR".

## Indicador basado en el estado real / Indicator driven by the real state

`VideoStatusOverlay` sólo muestra lo que el motor reportó: paso directo HDR10, conversión a SDR,
rango estándar, decodificación acelerada, caída a software y formato no admitido. Cada estado
enciende exactamente su línea, comprobado sobre el árbol visual real, y el conjunto está en paridad
ES/EN con capturas en `artifacts/ui-captures/T22/video-status-es-ES.png` y `-en-US.png`. / The
overlay shows only reported state, one line per state, verified on the real visual tree, in both
languages with the captures listed above.

## Dolby Vision y passthrough / Dolby Vision and passthrough

`PLY-015` mantiene su estado `OUT_OF_SCOPE` y **no recibe código de reproducción**. La revisión de
alcance de esta tarea concluye:

- Dolby Vision se **reconoce** para poder rechazarlo explícitamente: la política devuelve
  `UnsupportedCapability` y la interfaz lo dice con texto, en lugar de degradarlo en silencio.
- No existe ninguna ruta de salida Dolby Vision: `VideoOutputPath` sólo declara `Sdr`,
  `Hdr10Passthrough` y `SdrToneMapped`, y una prueba comprueba que ningún nombre del enumerado
  menciona Dolby.
- El passthrough de audio Dolby/DTS tampoco se implementa; se revisa en T23 desde el lado del audio.
- Cualquier cambio exige nueva evaluación técnica, legal y de demanda, que es la condición que la
  matriz ya registra para este identificador.

/ The Dolby identifier stays out of scope and receives no playback code: the format is recognised
only so it can be refused explicitly and announced in text, there is no Dolby output path, and any
change requires the new technical, legal, and demand review the matrix already records.

## Matriz de hardware y bloqueos / Hardware matrix and blocks

Los resultados por hardware, con la separación entre lo automatizado, lo físico y las limitaciones,
están en [matriz de vídeo por hardware / per-hardware video matrix](hardware-video-matrix.md). Los
bloqueos abiertos son:

1. **No hay GPU integrada activa** en el equipo de referencia, así que la comparación integrada
   contra discreta **no se ha ejecutado**. `PLY-003` permanece `IN_PROGRESS` por este motivo.
2. La aceleración activa se informa desde la petición y el estado del fallback, porque LibVLC 3 no
   expone el decodificador elegido.
3. La señal HDR no se ha medido con instrumento; la cadena se verifica por estado declarado.

/ The open blocks are: no active integrated GPU, so the integrated-versus-discrete comparison was not
run and the identifier stays in progress; active acceleration is reported from the request and
fallback state; and the HDR signal was not photometrically measured.

## Límites y privacidad / Boundaries and privacy

T22 no añade cliente de red ni telemetría. La consulta de pantalla es una llamada local de solo
lectura. La evidencia nombra modelos de GPU y monitor, que son reproducibles, y no registra
identificadores de instancia, números de serie ni rutas locales. / T22 adds no network client or
telemetry; the display query is a local read-only call, and the evidence names models rather than
instance identifiers, serial numbers, or local paths.
