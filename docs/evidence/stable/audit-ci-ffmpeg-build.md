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
