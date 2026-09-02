# ADR-0008 — Lo que habla con la máquina se separa de lo que decide / Separate What Talks to the Machine from What Decides

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-09-02
- Decisor / Decision owner: Product Owner
- Relacionado / Related: [`CONTRIBUTING.md`](../../CONTRIBUTING.md), [`CLAUDE.md`](../../CLAUDE.md),
  `eng/check-coverage.ps1`, `eng/coverage-debt.txt`,
  [la costura del adaptador de audio](../evidence/stable/audit-channel-layout-is-a-windows-setting.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

La puerta de archivos nuevos exige **96 % de líneas y 96 % de ramas** y no admite excepción. La lista
de deuda, en cambio, **sí** sabe decir «este archivo depende de hardware que el runner no tiene»:
sostiene siete archivos en esos términos, con el suelo que CI mide y el trinquete que sólo encoge.

El 2026-09-02 esa asimetría se encontró de frente. `WindowsAudioEndpointConfigurator` —el adaptador
que escribe la disposición de canales de un endpoint de audio— medía **23/20** en un runner sin
tarjeta de sonido, y la puerta lo rechazó. La reacción inmediata fue proponer **ensanchar la
puerta**: que un archivo nuevo ya presente en la lista de deuda quedara sujeto a su suelo en vez de
al listón.

El propietario lo paró antes de decidir: «antes de tomar ninguna decisión necesito que te asegures,
documéntate». La documentación de coverlet contestó dos cosas en una consulta:

1. `[ExcludeFromCodeCoverage]` existe **para métodos difíciles o imposibles de probar directamente**,
   y coverlet lo honra sin configurar nada. **No hacía falta tocar ninguna guarda.**
2. Y aplicarlo a la clase entera habría sido el arreglo equivocado, porque dentro había dos cosas
   distintas.

### Decisión

**Un archivo que habla con el sistema operativo se escribe en dos mitades, y sólo la de abajo se
excluye de la cobertura.**

- **Abajo**: la creación de los objetos del sistema, sus `catch`, y el reenvío directo a ellos. Sólo
  puede fallar si el sistema falla, y no se puede ejecutar donde el hardware no está. Lleva
  `[ExcludeFromCodeCoverage]` **con la razón escrita al lado**.
- **Arriba**: lo que decide. Va detrás de una interfaz pública, se prueba entera y **no se excluye
  nunca**.

La interfaz es pública por la misma razón que `IAudioOutputTarget` ya lo era: es la superficie
mínima que el adaptador toca del mundo exterior, y hacerla sustituible es lo que permite ejecutar lo
de arriba en cualquier máquina.

### Lo que se midió al tomarla

| | Antes | Después |
|---|---:|---:|
| `WindowsAudioEndpointConfigurator` en un runner sin audio | **23/20** | **100/100** |
| Pruebas que recorren su aritmética | 0 | **17** |
| Trinquete de deuda | 190 | **189** (baja) |

Las diecisiete cubren lo que una persona **oye**: el recuento de canales y la máscara de altavoces de
cada disposición, el alineado de bloque y los bytes por segundo derivados, la profundidad y la
frecuencia que deliberadamente no se tocan, un endpoint que ya lleva lo pedido, un controlador que
rechaza, una escritura que falla y un sondeo que sólo acepta 16 bits.

**Y qué línea excluir no se adivinó**: `--collect:"XPlat Code Coverage;Format=json"` nombró una a una
las que nada alcanzaba, y nombró **sólo** la creación de objetos y sus `catch`. Ahí estaba la
costura, dibujada por la medición en vez de por el criterio.

### Consecuencias que esto acepta

1. **La puerta de archivos nuevos no se toca.** Su rigidez es la que obligó a encontrar la costura;
   ensancharla habría dejado el adaptador sin probar y la regla sin aprender.
2. **`[ExcludeFromCodeCoverage]` entra en el vocabulario del repositorio**, y sólo con este uso. Un
   atributo sobre algo que decide es la forma de tapar código sin probar, que es exactamente lo que
   las guardas existen para impedir.
3. **La lista de deuda sigue siendo para archivos que ya estaban.** Un archivo nuevo no entra en ella:
   o llega al listón, o se parte hasta que llegue.

### Lo que NO decide esto

**Los siete archivos que ya están en la lista de deuda por hardware no se tocan de oficio.** Partirlos
es trabajo con su propio riesgo, y la lista existe precisamente para que no haya que hacerlo todo a
la vez. Esta decisión gobierna lo que **entra** a partir de ahora.

---

## English

### Context

The new-file gate demands **96 % of lines and 96 % of branches** with no exception. The debt list, by
contrast, **does** know how to say "this file depends on hardware the runner lacks": it holds seven
files on those terms, each with the floor CI measured and a ratchet that only shrinks.

On 2026-09-02 that asymmetry was met head-on. `WindowsAudioEndpointConfigurator` — the adapter that
writes an audio endpoint's channel layout — measured **23/20** on a runner with no sound card, and
the gate refused it. The immediate reaction was to propose **widening the gate**: letting a new file
already on the debt list be held to its floor rather than to the bar.

The owner stopped it before the decision: "antes de tomar ninguna decisión necesito que te asegures,
documéntate". Coverlet's documentation answered two things in one query:

1. `[ExcludeFromCodeCoverage]` exists **for methods that are difficult or impossible to test
   directly**, and coverlet honours it with no configuration. **No gate needed touching.**
2. And applying it to the whole class would have been the wrong fix, because there were two different
   things inside it.

### Decision

**A file that talks to the operating system is written in two halves, and only the lower one is
excluded from coverage.**

- **Below**: creation of operating-system objects, their `catch` blocks, and direct forwarding onto
  them. It can only fail if the system fails, and cannot run where the hardware is absent. It carries
  `[ExcludeFromCodeCoverage]` **with the reason written beside it**.
- **Above**: what decides. It sits behind a public interface, is tested in full, and is **never**
  excluded.

The interface is public for the reason `IAudioOutputTarget` already was: it is the minimum surface
the adapter touches of the outside world, and making it substitutable is what lets the half above run
on any machine.

### What was measured in taking it

| | Before | After |
|---|---:|---:|
| `WindowsAudioEndpointConfigurator` on a runner with no audio | **23/20** | **100/100** |
| Tests walking its arithmetic | 0 | **17** |
| Debt ratchet | 190 | **189** (down) |

The seventeen cover what a person **hears**: each layout's channel count and speaker mask, the block
align and bytes per second derived from them, the depth and rate deliberately left alone, an endpoint
already carrying what was asked, a driver refusing, a write failing, and a probe taking 16 bits only.

**And which lines to exclude was not guessed**: `--collect:"XPlat Code Coverage;Format=json"` named
the unreached ones one by one, and named **only** object creation and its catches. That was the seam,
drawn by the measurement rather than by judgement.

### Consequences this accepts

1. **The new-file gate is not touched.** Its rigidity is what forced the seam to be found; widening
   it would have left the adapter untested and the rule unlearned.
2. **`[ExcludeFromCodeCoverage]` enters this repository's vocabulary**, and only with this use. The
   attribute over something that decides is how untested code gets hidden, which is precisely what
   the gates exist to prevent.
3. **The debt list remains for files that were already there.** A new file does not join it: either it
   reaches the bar, or it is split until it does.

### What this does not decide

**The seven files already on the debt list for hardware are not split as a matter of course.**
Splitting them is work with its own risk, and the list exists precisely so it need not all happen at
once. This decision governs what **arrives** from here on.
