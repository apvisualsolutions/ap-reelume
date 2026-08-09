# ADR-0005 — Verificar la matriz de vídeo sobre los adaptadores disponibles / Verify the Video Matrix on the Available Adapters

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-08-04
- Decisor / Decision owner: Product Owner, a propuesta de Engineering / on Engineering's proposal
- Relacionado / Related: [`FEATURES.md`](../FEATURES.md), [T22](../evidence/mvp/T22-hdr-acceleration.md),
  [matriz por hardware](../evidence/mvp/hardware-video-matrix.md), [T41](../evidence/mvp/T41-release-gate.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

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
