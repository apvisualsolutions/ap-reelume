# ADR-0005 — Verificar la matriz de vídeo sobre los adaptadores disponibles / Verify the Video Matrix on the Available Adapters

- Estado / Status: `ACCEPTED`, con enmienda del 2026-09-12 / `ACCEPTED`, amended on 2026-09-12
- Fecha / Date: 2026-08-04
- Decisor / Decision owner: Product Owner, a propuesta de Engineering / on Engineering's proposal
- Relacionado / Related: [`FEATURES.md`](../FEATURES.md), [T22](../evidence/mvp/T22-hdr-acceleration.md),
  [matriz por hardware](../evidence/mvp/hardware-video-matrix.md), [T41](../evidence/mvp/T41-release-gate.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Enmienda del 2026-09-12 — el equipo ya tiene dos clases de adaptador

**La decisión sigue en pie; su premisa de hecho no.** Este ADR se apoya en que el equipo de
referencia tenía **un solo adaptador** y en que «su procesador no incorpora gráficos». El 2026-09-12,
midiendo para `PLY-016`, se enumeraron **tres adaptadores**: la NVIDIA GeForce RTX 5070, una **Intel
UHD Graphics 770** que sí existe y mueve 3840×2160, y el «Microsoft Basic Render Driver». La gráfica
integrada estaba desactivada en la BIOS cuando se escribió este documento y el propietario la activó
después.

**Lo que eso cambia del límite escrito, con precisión y sin ensanchar:**

- El adaptador de Intel **ya no falta**: su procesador de vídeo se ha ejercido con una batería de
  ocho formatos y una prueba de píxel, y su superresolución cambia el 54,6 % de la imagen. La
  evidencia es [PLY16-d3d11-probe.md](../evidence/stable/PLY16-d3d11-probe.md).
- **Lo que sigue sin ejercerse es la DECODIFICACIÓN de Quick Sync**, que es otra cosa: lo medido es
  el procesador de vídeo, no el decodificador. Y hay un motivo que este ADR no podía conocer: el
  motor **apaga la aceleración por hardware de forma incondicional** porque con D3D11VA se pierde el
  subtítulo compuesto. Así que la ruta de decodificación de cualquier fabricante está sin ejercer, no
  sólo la de Intel.
- **La matriz física de `PLY-003` no se ha vuelto a ejecutar** sobre la segunda clase de adaptador.
  Este ADR dice que la publicación estable es su momento natural, y ese momento ahora es posible sin
  comprar nada.
- **Y nada de este árbol monta el anfitrión real de Windows**, añadido esa misma noche. Buscar
  `UsePlatformDetect` en `tests/` y en `eng/` no devuelve nada: el paseo «físico» de
  `AccessibilityTests` también es headless, y lo dice en su propia cabecera. Así que qué motor
  gráfico elige la aplicación que se distribuye —`Program.cs` pide detección de plataforma, y Windows
  ofrece software, ANGLE, WGL y Vulkan— **no lo establece ninguna puerta**, y la importación de
  textura D3D11 que `PLY-016` necesita vive en la ruta de ANGLE. La decisión de contestarlo con un
  ejecutable de diagnóstico pequeño, en vez de con otra línea del guion manual, es de Engineering,
  tomada el 2026-09-12 y sin ejecutar; abre una ventana de verdad, así que pide la máquina del
  propietario cuando no esté trabajando.

No se toca la decisión: `PLY-003` sigue `VERIFIED` por lo que se demostró en su día. Lo que se
corrige es un hecho del contexto que, sin esta nota, seguiría diciendo que aquí no hay gráficos
integrados — y, ahora, que algo del árbol ejercita el anfitrión real.

### Contexto

`PLY-003` —aceleración por hardware, HDR10 y conversión de tono SDR— quedó sin verificar desde T22
por un motivo: la matriz de vídeo no se había ejecutado sobre gráficos integrados. La evidencia de
T22 lo escribió como bloqueo y T41 lo heredó como tal.

Al revisarlo se midió el equipo en lugar de asumirlo:

| Comprobación | Resultado |
|---|---|
| Adaptadores de clase Display | 1: NVIDIA GeForce RTX 5070 |
| Dispositivos gráficos Intel enumerados | 0, ni presentes ni fantasma |
| Adaptadores en el registro de clase de vídeo | 1, y nunca hubo otro |
| Dispositivos Intel del chipset en el bus PCI | 20 |

La enumeración de dispositivos Intel funciona; simplemente no hay ninguno gráfico. El propietario del
equipo confirma que su procesador no incorpora gráficos.

Y el plan pide, en T22.4, «ejecutar en GPU integrada y discreta **disponibles**». Con una sola
disponible, la matriz se ejecutó sobre todas las disponibles.

### El problema con dejarlo bloqueado

Un bloqueo es una promesa de trabajo futuro: dice qué falta y qué lo desbloquearía. Aquí no falta
trabajo, falta un adaptador que este equipo no tiene y no puede adquirir habilitando nada. Mantenerlo
como bloqueo tendría dos efectos, los dos malos:

- registraría como pendiente algo que nadie va a hacer en este hardware, que es como un bloqueo deja
  de significar nada;
- y dejaría `PLY-003` sin verificar cuando **su criterio de aceptación está cumplido y demostrado en
  hardware**: «indicador correcto y fallback por software sin caída».

### Decisión

**`PLY-003` pasa a `VERIFIED` sobre la matriz física ejecutada en el adaptador disponible, y la
cobertura de una sola clase de GPU se registra como límite conocido.**

Lo que respalda la verificación, todo sobre la RTX 5070 y todo físico:

| Escenario | Resultado observado |
|---|---|
| HDR10 con pantalla HDR activa | `Hdr10Passthrough`, fotogramas decodificados |
| HDR10 con pantalla SDR | `SdrToneMapped`, fotogramas decodificados |
| SDR (BT.709) | `Sdr`, fotogramas decodificados |
| Aceleración forzada a caer | `HardwareAccelerationActive=false`, la reproducción continúa |
| Indicador en la aplicación empaquetada | «Decodificación acelerada por hardware», con `D3D11VA` en el registro del motor |

La decisión de ruta no depende del adaptador: `VideoOutputPolicy` decide a partir de los hechos que
recibe —transferencia declarada de la fuente, capacidad de la pantalla, aceleración solicitada— y esa
política está probada de forma exhaustiva sin hardware.

### Consecuencias

- `PLY-003` cuenta como cumplido para la puerta MVP. El recuento pasa a 43 `VERIFIED`, 1
  `OUT_OF_SCOPE` y 2 `BLOCKED`.
- **Queda un límite de cobertura real y escrito:** la ruta de decodificación de Intel Quick Sync no
  se ha ejercido nunca. Un defecto exclusivo de esa ruta no lo habría visto ninguna prueba de este
  proyecto.
- La publicación estable, que certifica ARM64 (`PRD-003`) sobre hardware distinto, es el momento
  natural para ejecutar la matriz en una segunda clase de adaptador. Si eso ocurre y aparece un
  defecto, este ADR se reemplaza.
- No cambia nada del código ni de las pruebas: es una decisión sobre qué significa «matriz física
  aprobada» en un equipo con un solo adaptador.

### Alternativas descartadas

- **Mantener el bloqueo indefinidamente.** Registra como pendiente un trabajo que nadie puede hacer
  aquí, y deja sin verificar un criterio que sí está demostrado.
- **Conseguir hardware con gráficos integrados para el MVP.** Es una compra para cerrar un compromiso
  cuyo criterio ya está cumplido; el momento razonable para ampliar la cobertura de adaptadores es
  S1, que ya exige hardware distinto.

---

## English

### Amendment of 2026-09-12 — the machine now has two classes of adapter

**The decision stands; its factual premise does not.** This ADR rests on the reference machine having
**a single adapter** and on its processor having no integrated graphics. On 2026-09-12, measuring for
`PLY-016`, **three adapters** were enumerated: the NVIDIA GeForce RTX 5070, an **Intel UHD Graphics
770** that does exist and drives 3840×2160, and the "Microsoft Basic Render Driver". The integrated
graphics were disabled in the BIOS when this document was written and the owner enabled them
afterwards.

**What that changes about the recorded limit, precisely and without widening it:**

- The Intel adapter **is no longer missing**: its video processor has been exercised with a battery
  of eight formats and a pixel test, and its super resolution changes 54.6 % of the picture. The
  evidence is [PLY16-d3d11-probe.md](../evidence/stable/PLY16-d3d11-probe.md).
- **What remains unexercised is Quick Sync's DECODE path**, which is a different thing: what was
  measured is the video processor, not the decoder. And there is a reason this ADR could not have
  known: the engine **switches hardware decoding off unconditionally** because D3D11VA loses the
  composed subtitle. So no vendor's decode path is exercised, not only Intel's.
- **`PLY-003`'s physical matrix has not been re-run** on the second class of adapter. This ADR names
  the stable release as its natural moment, and that moment is now possible without buying anything.
- **And nothing in this tree runs the real Windows host at all**, added the same evening. Searching
  `tests/` and `eng/` for `UsePlatformDetect` returns nothing: the «physical» walk of
  `AccessibilityTests` is headless too, and says so in its own header. So which graphics backend the
  shipped application picks — `Program.cs` asks for platform detection, and Windows offers software,
  ANGLE, WGL and Vulkan — is **not established by any gate**, and the D3D11 texture import
  `PLY-016` needs lives on the ANGLE route. The decision to answer it with a small diagnostic
  executable rather than another line of the manual walkthrough is Engineering's, taken 2026-09-12
  and not yet carried out; it opens a real window, so it needs the owner's machine when he is not
  working.

The decision is untouched: `PLY-003` stays `VERIFIED` on what was demonstrated at the time. What is
corrected is a fact in the context that would otherwise keep saying there are no integrated graphics
here — and, now, that something in the tree exercises the real host.

### Context

`PLY-003` — hardware acceleration, HDR10, and SDR tone mapping — has been unverified since T22 for
one reason: the video matrix had not been run on integrated graphics. T22's evidence wrote that up as
a block and T41 inherited it as one.

On review, the machine was measured rather than assumed: one display-class adapter (the RTX 5070),
zero Intel display devices enumerated — none present, none phantom, none in the video class registry
— against twenty Intel chipset devices on the PCI bus. Intel enumeration works; there is simply no
graphics device. The machine's owner confirms the processor has no integrated graphics.

And the plan asks, in T22.4, to "run on **available** integrated/discrete GPUs". With one available,
the matrix ran on all of them.

### The problem with leaving it blocked

A block is a promise of future work: it says what is missing and what would clear it. Here no work is
missing; an adapter is, and this machine cannot acquire one by enabling anything. Keeping the block
would have two effects, both bad: it would record as pending something nobody will do on this
hardware, which is how a block stops meaning anything; and it would leave `PLY-003` unverified when
**its acceptance criterion is met and demonstrated on hardware** — a correct indicator and a software
fallback without a crash.

### Decision

**`PLY-003` moves to `VERIFIED` on the physical matrix run against the available adapter, and
single-GPU-class coverage is recorded as a known limit.**

What backs the verification is physical and all on the RTX 5070: HDR10 passthrough on an active HDR
display, SDR tone mapping on an SDR display, plain SDR, a forced fallback where
`HardwareAccelerationActive=false` and playback continues, and the indicator reading
"hardware-accelerated decoding" in the packaged application with `D3D11VA` in the engine's own log.

The path decision does not depend on the adapter: `VideoOutputPolicy` decides from the facts it is
given — the source's declared transfer characteristics, the display's capability, whether
acceleration was requested — and that policy is exhaustively tested without hardware.

### Consequences

- `PLY-003` counts as met for the MVP gate. The count becomes 43 `VERIFIED`, 1 `OUT_OF_SCOPE`, and 2
  `BLOCKED`.
- **A real coverage limit remains, and is written down:** Intel Quick Sync's decode path has never
  been exercised. A defect unique to that path would have been invisible to every test in this
  project.
- The stable release, which certifies ARM64 (`PRD-003`) on different hardware, is the natural moment
  to run the matrix on a second class of adapter. If that happens and a defect appears, this ADR is
  superseded.
- Nothing in the code or the tests changes: this is a decision about what "approved physical matrix"
  means on a machine with one adapter.

### Alternatives rejected

- **Keeping the block indefinitely.** It records as pending work nobody can do here, and leaves
  unverified a criterion that is demonstrated.
- **Acquiring hardware with integrated graphics for the MVP.** That is a purchase to close a
  commitment whose criterion is already met; the reasonable moment to widen adapter coverage is S1,
  which already requires different hardware.
