# El paquete reducido que dejaba cinco pruebas sin correr / The Reduced Package That Left Five Tests Unrun

- IDs: `PLY-002`, `PLY-003`, `PRD-003`, `ENG-001`
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

### La quinta, cerrada el 2026-09-18 (`ENG-001`)

**Seguía omitida trece días después.** El run `35352745213` de `9af3de3b` dio en `MediaTests` 199 de
201 con dos omitidas: esta, con el mismo motivo literal, y un diagnóstico que sólo corre cuando
alguien le pasa un fotograma por variable de entorno, que no es un hueco. Leído del `.trx` del
artefacto `test-results`.

**La causa, medida aquí con el mismo binario del servidor** —el 9.0 que empaqueta `ffmpeg-full`
9.0.0, sacado de su `.nupkg` y usado con `FFMPEG_PATH`—, y leída con `ffprobe` en
`color_space,color_transfer,color_primaries`:

| Qué | ffmpeg 2024-06-21 | ffmpeg 9.0 |
| --- | --- | --- |
| Receta tal cual, `.mkv` | `bt2020nc,smpte2084,bt2020` | **`bt2020nc,unknown,unknown`** |
| Misma receta, flujo HEVC suelto (`-f hevc`) | `bt2020nc,smpte2084,bt2020` | `bt2020nc,smpte2084,bt2020` |
| Receta con `-vf setparams=…` | `bt2020nc,smpte2084,bt2020` | `bt2020nc,smpte2084,bt2020` |

**La segunda fila es la que ubica el defecto**: x265 escribe la curva en el flujo con las dos
versiones; es el contenedor el que en 9.0 sale sin ella. **Lo que sigue es deducción, no medida**:
lo coherente con las tres filas es que esa versión tome los campos de color del contenedor de los
fotogramas, que `testsrc2` deja sin etiquetar, de modo que las opciones `-color_trc` del codificador
ya no llegan al Matroska. No se leyó el código de ffmpeg; el arreglo no depende de ello. `setparams` —el filtro que según su propia
ayuda «fuerza la propiedad de color del fotograma de salida»— los etiqueta en origen.

**El cambio**: la receta de `mkv-hevc-hdr10` lleva
`-vf setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc`, y la prueba **deja de
omitirse** cuando falta la curva. Una omisión razonada es lo que tuvo esta fila meses sin correr
en el servidor; con una receta que funciona en las dos versiones, perderla otra vez tiene que ser rojo.

**La prueba, vista fallar antes de darla por buena**, `HdrAccelerationTests` con la muestra borrada
antes de cada pasada (la caché sólo mira si el fichero existe):

| Pasada | Resultado |
| --- | --- |
| 9.0, receta y prueba viejas | 6 superadas, **1 omitida** — lo mismo que el servidor |
| 9.0, receta nueva | **7 de 7** |
| 9.0, receta vieja y prueba nueva | **1 con error**: `Expected: "smpte2084"` |
| 2024, receta nueva (control) | 7 de 7 |

Con esto `ENG-001` se cierra: las cinco omisiones que dejaba el paquete reducido corren en el
servidor. El número que lo confirma es el del run de este cambio, no esta tabla.

**Y lo que esa prueba NO dice, medido en el mismo run.** El run `35362996825` de `cfec2dec` se puso
rojo por la puerta de cobertura, no por la prueba: al correr, recorrió más de
`LibVlcVideoCapabilities.cs` y su suelo tuvo que subir de 77/61 a 77/66, copiado del artefacto
`coverage-debt`. Buscando qué ramas faltaban salió que la función que la prueba ejercita,
`WithColourTransfer`, **no la llama ningún código del programa**: el reproductor recibe siempre
`SourceHdr` en `None`. La prueba verde demuestra que la clasificación es correcta, no que el
reproductor reconozca un HDR10. Registrado como `ENG-030`.

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

### The fifth, closed on 2026-09-18 (`ENG-001`)

**It was still skipped thirteen days later.** Run `35352745213` of `9af3de3b` gave 199 of 201 in
`MediaTests` with two skipped: this one, with the same literal reason, and a diagnostic that only runs
when somebody hands it a frame through an environment variable, which is not a gap. Read from the
`.trx` in the `test-results` artifact.

**The cause, measured here with the server's own binary** — the 9.0 that `ffmpeg-full` 9.0.0
packages, taken out of its `.nupkg` and used through `FFMPEG_PATH` — and read with `ffprobe` as
`color_space,color_transfer,color_primaries`:

| What | ffmpeg 2024-06-21 | ffmpeg 9.0 |
| --- | --- | --- |
| Recipe as it was, `.mkv` | `bt2020nc,smpte2084,bt2020` | **`bt2020nc,unknown,unknown`** |
| Same recipe, bare HEVC stream (`-f hevc`) | `bt2020nc,smpte2084,bt2020` | `bt2020nc,smpte2084,bt2020` |
| Recipe with `-vf setparams=…` | `bt2020nc,smpte2084,bt2020` | `bt2020nc,smpte2084,bt2020` |

**The second row is the one that places the defect**: x265 writes the curve into the stream on both
releases; it is the container that comes out without it on 9.0. **What follows is inference, not
measurement**: what fits the three rows is that this release takes the container's colour fields from
the frames, which `testsrc2` leaves untagged, so the encoder's `-color_trc` options no longer reach
the Matroska track. ffmpeg's code was not read; the fix does not depend on it. `setparams` — the filter that, by its own help, forces
«color property for the output video frame» — tags them at the source.

**The change**: `mkv-hevc-hdr10`'s recipe carries
`-vf setparams=color_primaries=bt2020:color_trc=smpte2084:colorspace=bt2020nc`, and the test **stops
skipping** when the curve is missing. A reasoned skip is what kept this row unrun on the server for
months; with a recipe that works on both releases, losing it again has to be red.

**The test, seen failing before it was trusted**, `HdrAccelerationTests` with the sample deleted
before every pass (the cache only checks that the file exists):

| Pass | Result |
| --- | --- |
| 9.0, old recipe and old test | 6 passed, **1 skipped** — the same as the server |
| 9.0, new recipe | **7 of 7** |
| 9.0, old recipe and new test | **1 failed**: `Expected: "smpte2084"` |
| 2024, new recipe (control) | 7 of 7 |

With this `ENG-001` closes: the five skips the reduced package left behind run on the server. The
number that confirms it is this change's run, not this table.

**And what that test does NOT say, measured in the same run.** Run `35362996825` of `cfec2dec` went red
on the coverage gate, not on the test: by running, it walked more of `LibVlcVideoCapabilities.cs` and
that file's floor had to rise from 77/61 to 77/66, copied from the `coverage-debt` artifact. Looking
for the missing branches showed that the function the test exercises, `WithColourTransfer`, **is
called by no code in the program**: the player always receives `SourceHdr` as `None`. The green test
proves the classification is right, not that the player recognises an HDR10. Filed as `ENG-030`.
