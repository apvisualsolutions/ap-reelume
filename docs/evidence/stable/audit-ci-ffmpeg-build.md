# El paquete reducido que dejaba cinco pruebas sin correr / The Reduced Package That Left Five Tests Unrun

- IDs: `PLY-002`, `PLY-003`, `PRD-003`
- Fecha / Date: 2026-09-05
- Alcance / Scope: `.github/workflows/ci.yml`, `.github/workflows/release.yml`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Qué se midió, y por qué importa

`docs/evidence/mvp/T19-codec-matrix.md` afirma, desde el 2026-08-02, que la matriz de códecs corre
con **cero omitidas** y que «todos los codificadores necesarios estaban disponibles, así que ninguna
fila se saltó». Es cierto — **en la máquina donde se midió**. En el servidor no lo fue nunca.

El run de `eff2c7f`, el último verde antes de este cambio, dice esto de `MediaTests`:

| Suite | Total | Pasadas | Omitidas |
| --- | --- | --- | --- |
| `MediaTests` | 155 | 150 | **5** |

Y las cinco, con su motivo literal leído del informe del run:

| Prueba | Motivo que el propio arnés escribió |
| --- | --- |
| `CodecMatrixTests.Every_playable_row_starts_audio_and…` | «The local encoder cannot produce 'mkv-av1-opus': missing libsvtav1.» |
| `CodecMatrixTests.The_matrix_records_its_provenance_f…` | «The local encoder cannot produce 'mkv-av1-opus': missing libsvtav1.» |
| `CorruptMediaTests.Every_unplayable_row_reports_its_e…` | «The local encoder cannot produce 'mkv-avs2-unsupported': missing libxavs2.» |
| `CorruptMediaTests.A_failed_row_never_deletes_or_rewr…` | «The local encoder cannot produce 'mkv-avs2-unsupported': missing libxavs2.» |
| `HdrAccelerationTests.An_HDR10_source_is_recognised_f…` | «The generated HDR sample carries no colour-transfer metadata on this encoder build.» |

**La tercera no es un códec ausente**, y confundirla con las otras dos habría hecho perseguir el
defecto equivocado: el build reducido sí produce la muestra HDR10, pero la multiplexa **sin sus
metadatos de transferencia de color**, así que no queda nada que reconocer.

### La causa, y por qué el arnés no la delató

El paso instalaba `choco install ffmpeg --version 9.0.0`. Ese paquete de la comunidad empaqueta el
build **essentials** de gyan.dev, que no trae `libsvtav1` ni `libxavs2`. El manifiesto de muestras
los pide por nombre —`tests/ApSolutions.LocalMedia.MediaTests/Fixtures/media-manifest.json`, filas
`requiredEncoders`—, y `MediaToolchain.HasEncoder` **los consulta de verdad** ejecutando
`ffmpeg -encoders`: nunca adivina. Al faltar, `Assert.SkipWhen` omite la fila con su razón escrita.

**Ése es exactamente el comportamiento correcto del arnés, y es también por lo que nadie lo vio.**
Una omisión razonada no pone el run en rojo, así que cinco comprobaciones llevaban meses sin
ejecutarse en el servidor y la puerta seguía verde. Se destapó midiendo por qué dos fases de la
matriz ARM64 quedaban marcadas, no por una alarma.


### La contraprueba, medida aquí

Esta máquina lleva instalado el full build de gyan.dev — el mismo que empaqueta `ffmpeg-full` —, y
declara `libsvtav1` y `libxavs2` en `ffmpeg -encoders`. Con él, la misma suite y el mismo commit:

```
Con error: 0, Superado: 155, Omitido: 0, Total: 155, Duración: 1 m 33 s
```

**155 de 155 contra 150 de 155.** Las cinco que el servidor omitía pasan cuando el codificador las
puede producir, así que lo que faltaba era el paquete y no el código. Es la contraprueba que separa
«el arnés se salta cinco filas» de «hay cinco filas rotas».

### El cambio, y por qué no puede romper nada

Los tres pasos —`ci.yml` en el trabajo x64 y en el ARM64, y `release.yml`— pasan a
`choco install ffmpeg-full --version 9.0.0`.

**La versión no se mueve.** `ffmpeg-full` publica también la 9.0.0, comprobado con `choco search
ffmpeg-full --all-versions`, así que este cambio aísla el build y no toca nada más. Si algo cambiara
de comportamiento, no habría una segunda variable a la que culpar.

**Y el paquete completo es un superconjunto estricto del reducido.** No es una deducción: su propia
descripción lo dice — «This package is for the stable static **full** ffmpeg version […] provides
everything included in the 'essentials' build with additional libraries». Nada que hoy pase puede
empezar a fallar por falta de un códec.

**El nombre del paso no se toca**, y eso no es cosmética: `eng/watch-ci.ps1` reconoce
`Install ffmpeg` por su literal para tratarlo como andamiaje y no anunciarlo en cada run.
Renombrarlo habría convertido el vigía en algo que suena siempre, que es lo que enseña a ignorarlo.


### La corrección: fueron cuatro de cinco, y la quinta no era del paquete

El run de `c30e7d6` —el primero con `ffmpeg-full`— dio **154 de 155 con una omitida**, no 155 de 155.
Cuatro de las cinco se destaparon; la de HDR sigue saltándose, y con el mismo motivo literal.

**La causa era otra desde el principio, y la medición lo dice sin ambigüedad.** La receta de
`mkv-hevc-hdr10` pide `libx265`, que **el paquete reducido también trae**: esa fila nunca se saltó
por un codificador ausente. Lo que falla es que el multiplexor no escribe la curva de transferencia
que la receta pide, y eso **no depende del build sino de la versión**:

| ffmpeg | Origen | `color_transfer` que escribe |
| --- | --- | --- |
| 2024-06-21, full build | la máquina del propietario | **`smpte2084`** |
| 9.0.0, full build | el runner, este run | **ausente** |

Generado aquí con la misma receta recortada a un segundo y leído con `ffprobe`. Así que fijar la
versión en 9.0.0 —que era lo correcto para aislar el cambio del paquete— es también lo que mantiene
esa fila omitida.

**Lo que esto deja escrito, para que nadie lo persiga como si fuera del paquete**: la omisión que
queda es un cambio de comportamiento entre versiones de ffmpeg, no una pieza que falte. Las salidas
son tres y ninguna se toma aquí: subir la versión fijada y volver a medir, cambiar la receta para que
el metadato sobreviva a 9.0.0, o aceptar la omisión con este número al lado.

**Y la lección de método es la de siempre en esta casa**: la evidencia de arriba se escribió
prediciendo cinco antes de que el servidor contestara. El servidor contestó cuatro. Lo que vale es el
número que volvió, no el que se esperaba.
---

## English

### What was measured, and why it matters

`docs/evidence/mvp/T19-codec-matrix.md` has claimed since 2026-08-02 that the codec matrix runs with
**zero skips** and that «every required encoder was available, so no row skipped». That is true — **on
the machine where it was measured**. On the server it never was.

The run of `eff2c7f`, the last green before this change, says this about `MediaTests`:

| Suite | Total | Passed | Skipped |
| --- | --- | --- | --- |
| `MediaTests` | 155 | 150 | **5** |

And the five, with the reason read literally from the run's own report:

| Test | The reason the harness itself wrote |
| --- | --- |
| `CodecMatrixTests.Every_playable_row_starts_audio_and…` | «The local encoder cannot produce 'mkv-av1-opus': missing libsvtav1.» |
| `CodecMatrixTests.The_matrix_records_its_provenance_f…` | «The local encoder cannot produce 'mkv-av1-opus': missing libsvtav1.» |
| `CorruptMediaTests.Every_unplayable_row_reports_its_e…` | «The local encoder cannot produce 'mkv-avs2-unsupported': missing libxavs2.» |
| `CorruptMediaTests.A_failed_row_never_deletes_or_rewr…` | «The local encoder cannot produce 'mkv-avs2-unsupported': missing libxavs2.» |
| `HdrAccelerationTests.An_HDR10_source_is_recognised_f…` | «The generated HDR sample carries no colour-transfer metadata on this encoder build.» |

**The third is not a missing codec**, and confusing it with the other two would have sent anybody
after the wrong defect: the reduced build does produce the HDR10 sample, but muxes it **without its
colour-transfer metadata**, so there is nothing left to recognise.

### The cause, and why the harness did not give it away

The step installed `choco install ffmpeg --version 9.0.0`. That community package carries gyan.dev's
**essentials** build, which ships neither `libsvtav1` nor `libxavs2`. The sample manifest asks for
them by name — `tests/ApSolutions.LocalMedia.MediaTests/Fixtures/media-manifest.json`,
`requiredEncoders` rows — and `MediaToolchain.HasEncoder` **actually asks**, by running
`ffmpeg -encoders`: it never guesses. When they are absent, `Assert.SkipWhen` skips the row with its
reason written down.

**That is exactly the right harness behaviour, and it is also why nobody saw this.** A reasoned skip
does not turn a run red, so five checks went unrun on the server for months while the gate stayed
green. It surfaced by measuring why two phases of the ARM64 matrix were marked, not from an alarm.


### The counter-proof, measured here

This machine carries gyan.dev's full build — the same one `ffmpeg-full` packages — and declares
`libsvtav1` and `libxavs2` under `ffmpeg -encoders`. With it, the same suite and the same commit:

```
Failed: 0, Passed: 155, Skipped: 0, Total: 155, Duration: 1 m 33 s
```

**155 of 155 against 150 of 155.** The five the server skipped pass once the encoder can produce
them, so what was missing was the package and not the code. It is the counter-proof that separates
«the harness skips five rows» from «five rows are broken».

### The change, and why it cannot break anything

All three steps — `ci.yml`'s x64 job and its ARM64 job, and `release.yml` — move to
`choco install ffmpeg-full --version 9.0.0`.

**The version does not move.** `ffmpeg-full` publishes 9.0.0 as well, confirmed with `choco search
ffmpeg-full --all-versions`, so this change isolates the build and touches nothing else. Were any
behaviour to change, there would be no second variable to blame.

**And the full package is a strict superset of the reduced one.** That is not a deduction: its own
description says so — «This package is for the stable static **full** ffmpeg version […] provides
everything included in the 'essentials' build with additional libraries». Nothing green today can
start failing for want of a codec.

**The step's name is left alone**, and that is not cosmetic: `eng/watch-ci.ps1` matches
`Install ffmpeg` by its literal to treat it as scaffolding rather than announce it on every run.
Renaming it would have turned the watcher into something that always sounds, which is what teaches
people to ignore it.

### The correction: it was four of five, and the fifth was never the package

The run of `c30e7d6` — the first with `ffmpeg-full` — gave **154 of 155 with one skipped**, not 155
of 155. Four of the five came back; the HDR one still skips, with the same literal reason.

**The cause was different all along, and the measurement says so without ambiguity.**
`mkv-hevc-hdr10`'s recipe asks for `libx265`, which **the reduced package carries too**: that row
never skipped for a missing encoder. What fails is that the muxer does not write the transfer curve
the recipe asks for, and that **depends on the version rather than the build**:

| ffmpeg | Where | `color_transfer` it writes |
| --- | --- | --- |
| 2024-06-21, full build | the owner's machine | **`smpte2084`** |
| 9.0.0, full build | the runner, this run | **absent** |

Generated here with the same recipe cut to one second and read with `ffprobe`. So pinning the version
at 9.0.0 — which was the right thing to do to isolate the package change — is also what keeps that
row skipped.

**What this writes down, so nobody chases it as a package problem**: the remaining skip is a
behaviour change between ffmpeg versions, not a missing piece. There are three ways out and none is
taken here: raise the pinned version and measure again, change the recipe so the metadata survives
9.0.0, or accept the skip with this number beside it.

**And the method lesson is this house's usual one**: the evidence above was written predicting five
before the server answered. The server answered four. What counts is the number that came back, not
the one that was expected.
