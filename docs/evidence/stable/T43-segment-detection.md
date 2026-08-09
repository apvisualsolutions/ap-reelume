# T43 — Detección automática de segmentos / T43 — Automatic Segment Detection

- IDs: `PLY-013`, `PLY-012`, `PRI-001`
- Commit: `feat: detect and review recurring playback segments locally`
- Superficies: `Ajustes → Detección de segmentos` y `Reproductor → Segmentos detectados` /
  `Settings → Segment detection` and `Player → Detected segments`
- Subespecificación congelada / Frozen subspec:
  [automatic-segment-detection.es.md](../../superpowers/specs/automatic-segment-detection.es.md)

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts must be updated together.

---

## Español

### Qué se cierra

`PLY-013` pasa a `VERIFIED`. La aplicación compara localmente los episodios de una serie entre sí,
encuentra las introducciones, resúmenes y créditos recurrentes, los guarda por episodio con
confianza y versión de detector, y los ofrece al botón de saltar. Una marca manual del mismo tipo
los suprime siempre, y aceptar o corregir una detección la protege de todas las ejecuciones
posteriores. Nada sale de la máquina.

Los umbrales los aprobó el propietario el 2026-08-07, antes de escribir código, y quedaron
congelados en la subespecificación junto con el corpus. El detector se ajustó únicamente contra
las series de desarrollo; el corpus retenido **pasó en su primera medición** y después de verlo no
se ajustó nada.

### Los resultados contra el corpus retenido

Cuatro series (S07–S10) que jamás se usaron para ajustar, 38 episodios, con tolerancia de ±2 s por
frontera:

| Métrica | Umbral | Medido |
|---|---|---|
| Intro: precisión / exhaustividad | ≥ 0,90 / ≥ 0,80 | **1,000 / 1,000** (29/29) |
| Resumen: precisión / exhaustividad | ≥ 0,85 / ≥ 0,70 | **1,000 / 1,000** (5/5) |
| Créditos: precisión / exhaustividad | ≥ 0,90 / ≥ 0,80 | **1,000 / 0,966** (28/29) |
| Episodios sin segmento con detección espuria | ≤ 5 % | **0 %** (0 de 9) |
| Peor serie individual: precisión / espurios | ≥ 0,80 / ≤ 10 % | **1,000 / 0 %** |

El único fallo es un créditos de S08 que no se emitió; ninguna detección emitida es falsa. En la
división de desarrollo (57 episodios): intro 48/48, resumen 12/12, créditos 47/48, 0 espurios.
La sensibilidad del banco está demostrada en la dirección contraria: el detector nulo incumple
tres umbrales y el de posición fija («intro = primeros 30 s») incumple catorce, con el 100 % de
los episodios sin segmento marcados en falso.

### La forma del trabajo

| Capa | Archivo | Qué decide |
|---|---|---|
| `Domain` | `DetectedMarker.cs`, `IDetectedMarkerRepository.cs` | Una detección es por episodio, porque un cold open variable mueve la intro de sitio |
| `Domain` | `IAutomaticSegmentDetector.cs` | El contrato: local, cancelable, con progreso, sin tocar almacenamiento |
| `Domain` | `SegmentDetectionPolicy.cs` | Qué se guarda, qué sobrevive a una re-detección y qué ve el reproductor |
| `Application` | `DetectSeriesSegments.cs` | El interruptor, los episodios reproducibles y la escritura atómica |
| `Application` | `ReviewDetectedSegments.cs` | Aceptar, corregir con validación y eliminar |
| `Infrastructure` | `0016_detected_markers.sql`, `DetectedMarkerRepository.cs` | Una fila por archivo y tipo, reemplazo atómico por serie |
| `Infrastructure` | `LocalSegmentFeatureExtractor.cs` | Ventanas acotadas → WAV temporal → huellas espectrales por segundo |
| `Infrastructure` | `AutomaticSegmentDetector.cs` | Alineación intra-serie, perfil de soporte, clústeres y clasificación |
| `Windows` | `SegmentDetectionBackground.cs` | Ceder el paso a la reproducción y una serie por sesión |
| `Presentation` | Dos vistas con sus modelos | El interruptor y la revisión, alcanzables desde el shell |

Tres decisiones sostienen el resto:

**La detección es por episodio y lo manual por serie.** El modelo de T29 guarda una marca por
serie porque una persona dibuja un rango; un detector mide cada episodio y un cold open variable
hace imposible que un solo rango sirva para todos. Las dos verdades conviven: la política compone
qué ve el reproductor y una marca manual de un tipo apaga las detecciones de ese tipo.

**El detector encuentra recurrencia, no «intros».** Busca tramos de audio que se repiten entre
episodios y los nombra por posición y coincidencia: el clúster que más episodios comparten en la
ventana inicial es la intro, el que le precede es el resumen, el de la ventana final son los
créditos. Un episodio sin nada recurrente no produce nada, y eso es exactamente lo que miden las
dos series de control.

**La evaluación no se negocia.** El corpus quedó congelado antes que el detector, el retenido se
midió una vez, y la prueba que compara contra los umbrales corre en cada ejecución de la suite,
serie a serie, para que un promedio no pueda tapar a nadie.

### Los tres defectos que encontró la medición del mecanismo

**Los tonos simples colisionaban.** Con 48 frecuencias, dos semillas distintas compartían una
racha de cuatro tonos por pura estadística de cumpleaños (~5 colisiones esperadas; la prueba
encontró la primera). El corpus pasó a acordes de dos senos y la prueba de unicidad vigila que
ninguna racha de cuatro acordes se repita entre semillas.

**El logaritmo inventaba parecido.** `log10(1+1000·p)` elevaba el suelo de ruido del códec al
orden de los picos reales: dos episodios sin relación daban coseno 0,956 de media. Con amplitud
(`sqrt`) y centrado de media, la misma pareja da −0,011 de media y 4 de 900 pares por encima del
umbral, aislados. La corrección se midió antes de escribirse.

**Sondear la duración estrellaba el proceso.** Analizar un medio y liberarlo inmediatamente es el
modo de fallo nativo que `LibVlcMediaProbe` ya esquivaba con su liberación diferida de un segundo
— y el extractor lo reprodujo: violación de acceso, consistente, con el testhost muriendo antes de
poder informar. La corrección no fue reinventar la sonda sino inyectar la existente.

### La corrección evidente que era peor que el problema

El motor no puede muxear vídeo en un WAV y lo dice en su registro una vez por episodio. La
corrección obvia —quitar la rama de vídeo con `no-sout-video`— se midió: la búsqueda de
`start-time` pasa a ser tan gruesa que **la mitad de las ventanas finales aterrizan segundos
lejos** y las fronteras dejan de ser verdad (10 de 20 rangos fuera de tolerancia en la
verificación física). La rama de vídeo se queda, con su ruido; unas líneas de registro pierden
contra unos límites falsos.

### Lo que el ensamblado escondía

El botón de saltar y el editor de marcas de `PLY-012` se construían en la raíz de composición y
**nunca recibían ni las marcas ni la posición**: la misma clase de defecto que el indicador de
vídeo encontró en T40 — alcanzar una superficie y alimentarla son preguntas distintas. Al cablear
la detección quedó corregido: las marcas compuestas (manuales más detectadas) siguen ahora al
playhead, el editor carga las marcas de la serie, y la serie de un episodio se resuelve de verdad
(`FindByFileAsync`) en lugar de usar el identificador del archivo como serie.

También cayeron: la guardia de T29 «no existe ningún tipo de detección» (reemplazada por el
invariante que sigue siendo cierto: la vía manual no puede fabricar origen detectado), cinco
pruebas ancladas al esquema 15 (ahora 16, con `detected_markers` en el censo de tablas), una
prueba de integración que construía mal sus episodios y un GUID no hexadecimal en un banco.

### Verificación física: quince comprobaciones, cero fallos

Un arnés fuera del repositorio ejecuta los componentes reales sobre una instalación real: base
SQLite migrada por el ejecutor real, catálogo sembrado con los diez episodios reales de S07,
ajustes en su archivo real. Con el interruptor apagado no se lee nada; encendido, los 20 rangos de
la serie quedan dentro de ±2 s y ninguna fila cae fuera de la verdad; la marca manual suprime la
intro detectada y deja los créditos; corregir una detección la marca como corregida y una
re-detección real la devuelve intacta, con el mismo identificador y rango. El informe está en
`artifacts/test-results/T43/physical/`.

### Rendimiento y privacidad

- **Nada se extrae mientras algo se reproduce**: medido con marcas de tiempo, la primera lectura
  ocurre después de que la reproducción termina.
- Analizar una serie de doce episodios cuesta menos de diez segundos y un latido de reproductor de
  20 ms no pierde nunca más de 250 ms mientras tanto.
- Una detección completa de nueve episodios no abre **ninguna** petición HTTP ni resuelve **ningún**
  nombre en el lado gestionado, medido con los mismos orígenes de eventos de `PRI-001`; el motor
  nativo sigue gobernado por `--no-metadata-network-access`. La lista de dueños de `HttpClient`
  del árbol no cambia.
- El corpus es sintético y generado; `RepositoryPrivacyTests` sigue en verde en cada ejecución.

### Cobertura de los archivos nuevos

Los trece archivos nuevos superan el 96 % en líneas **y** en ramas; el peor queda en 98,3 %.

| Archivo | Líneas | Ramas |
|---|---:|---:|
| `SegmentDetectionPolicy.cs` | 100 % | 100 % |
| `DetectedMarker.cs`, `IAutomaticSegmentDetector.cs`, `IDetectedMarkerRepository.cs` | 100 % | 100 % |
| `DetectSeriesSegments.cs`, `ReviewDetectedSegments.cs` | 100 % | 100 % |
| `DetectedMarkerRepository.cs` | 100 % | 100 % |
| `LocalSegmentFeatureExtractor.cs` | 98,3 % | 98,5 % |
| `AutomaticSegmentDetector.cs` | 99,3 % | 98,3 % |
| Los dos modelos de vista y sus vistas | 100 % | 100 % |
| `SegmentDetectionBackground.cs` | 100 % | 100 % |

Lo que queda sin cubrir son dos guardas defensivas contra estados que ninguna prueba puede
provocar: un `Play()` que el motor rechaza con un medio ya asignado, y un conjunto de clústeres
donde ninguno alcanza dos episodios pese a existir intervalos con soporte.

### Limitaciones declaradas

- El corpus sintético mide el mecanismo, no la variedad estética de series reales; la
  subespecificación lo deja escrito.
- Un resumen se detecta por su porción recurrente; uno que cambia por completo cada episodio no
  existe para este detector.
- La caché de huellas vive en memoria por sesión; re-detectar en la misma sesión no re-decodifica,
  entre sesiones sí.
- La detección de una serie se programa al abrir un episodio suyo y espera a que la reproducción
  termine; cambiar el interruptor no relee lo ya visto hasta la siguiente apertura.

---

## English

### What closes

`PLY-013` becomes `VERIFIED`. The application compares the episodes of a series with each other
locally, finds the recurring intros, recaps, and credits, stores them per episode with confidence
and detector version, and hands them to the skip button. A manual marker of the same kind always
suppresses them, and accepting or correcting a detection protects it from every later run.
Nothing leaves the machine.

The owner approved the thresholds on 2026-08-07, before any code, and they were frozen in the
subspec together with the corpus. The detector was tuned against the development series only; the
held-out corpus **passed on its first measurement**, and nothing was tuned after seeing it.

### Results against the held-out corpus

Four series (S07–S10) never used for tuning, 38 episodes, ±2 s per boundary:

| Metric | Threshold | Measured |
|---|---|---|
| Intro: precision / recall | ≥ 0.90 / ≥ 0.80 | **1.000 / 1.000** (29/29) |
| Recap: precision / recall | ≥ 0.85 / ≥ 0.70 | **1.000 / 1.000** (5/5) |
| Credits: precision / recall | ≥ 0.90 / ≥ 0.80 | **1.000 / 0.966** (28/29) |
| Segment-free episodes with a spurious detection | ≤ 5 % | **0 %** (0 of 9) |
| Worst individual series: precision / spurious | ≥ 0.80 / ≤ 10 % | **1.000 / 0 %** |

The only miss is one S08 credits that was not emitted; no emitted detection is false. On the
development split (57 episodes): intro 48/48, recap 12/12, credits 47/48, 0 spurious. The
benchmark's sensitivity is proven in the opposite direction: the null detector fails three
thresholds and the fixed-position one ("intro = first 30 s") fails fourteen, marking 100 % of the
segment-free episodes falsely.

### The shape of the work

| Layer | File | What it decides |
|---|---|---|
| `Domain` | `DetectedMarker.cs`, `IDetectedMarkerRepository.cs` | A detection is per episode, because a variable cold open moves the intro around |
| `Domain` | `IAutomaticSegmentDetector.cs` | The contract: local, cancelable, with progress, never touching storage |
| `Domain` | `SegmentDetectionPolicy.cs` | What is stored, what survives a re-detection, and what the player sees |
| `Application` | `DetectSeriesSegments.cs` | The switch, the playable episodes, and the atomic write |
| `Application` | `ReviewDetectedSegments.cs` | Accept, correct with validation, and delete |
| `Infrastructure` | `0016_detected_markers.sql`, `DetectedMarkerRepository.cs` | One row per file and kind, atomic per-series replacement |
| `Infrastructure` | `LocalSegmentFeatureExtractor.cs` | Bounded windows → temporary WAV → per-second spectral fingerprints |
| `Infrastructure` | `AutomaticSegmentDetector.cs` | Within-series alignment, support profile, clusters, classification |
| `Windows` | `SegmentDetectionBackground.cs` | Yielding to playback and one series per session |
| `Presentation` | Two views with their models | The switch and the review, reachable from the shell |

Three decisions carry the rest:

**Detection is per episode, manual markers per series.** The T29 model stores one range per series
because a person draws one; a detector measures each episode, and a variable cold open makes one
range for all impossible. Both truths coexist: the policy composes what the player sees, and a
manual marker of a kind switches off detections of that kind.

**The detector finds recurrence, not "intros".** It looks for stretches of audio that repeat
across episodes and names them by position and co-occurrence: the cluster most episodes share in
the opening window is the intro, the one preceding it is the recap, the closing window's is the
credits. An episode with nothing recurring produces nothing — which is exactly what the two
control series measure.

**Evaluation is not negotiable.** The corpus was frozen before the detector, the held-out split
was measured once, and the test that judges the thresholds runs on every suite run, series by
series, so no average can hide anyone.

### The three defects measurement found in the mechanism

**Single tones collided.** With 48 frequencies, two different seeds shared a four-tone run by
birthday statistics alone (~5 expected collisions; the test found the first). The corpus moved to
two-sine chords and the uniqueness test guards that no four-chord run repeats across seeds.

**The logarithm invented similarity.** `log10(1+1000·p)` lifted the codec's noise floor into the
order of the real peaks: two unrelated episodes averaged 0.956 cosine. With amplitude (`sqrt`) and
mean-centring the same pair averages −0.011, with 4 of 900 pairs above threshold, isolated. The
fix was measured before it was written.

**Probing the duration crashed the process.** Parsing a media and releasing it immediately is the
native failure mode `LibVlcMediaProbe` already works around with its one-second deferred release —
and the extractor reproduced it: a consistent access violation with the test host dying before it
could report. The fix was not a new probe but injecting the existing one.

### The obvious fix that was worse than the problem

The engine cannot mux video into a WAV and says so in its log once per episode. The obvious fix —
dropping the video branch with `no-sout-video` — was measured: `start-time` seeking becomes so
coarse that **half the closing windows land seconds away** and the boundaries stop being true
(10 of 20 ranges out of tolerance in the physical verification). The video branch stays, noise and
all; log lines lose to false boundaries.

### What the assembly was hiding

`PLY-012`'s skip button and marker editor were built in the composition root and **never received
the markers or the position**: the same class of defect the video status indicator had in T40 —
reaching a surface and feeding it are different questions. Wiring detection fixed it: the composed
markers (manual plus detected) now follow the playhead, the editor loads the series' markers, and
an episode's series is resolved for real (`FindByFileAsync`) instead of using the file identifier
as the series.

Also fell: the T29 guard "no detection type exists" (replaced by the invariant that stays true:
the manual path cannot fabricate a detected origin), five tests pinned to schema 15 (now 16, with
`detected_markers` in the table census), an integration test that built its episodes wrong, and a
non-hexadecimal GUID in a benchmark.

### Physical verification: fifteen checks, zero failures

A harness outside the repository runs the real components over a real installation: a SQLite
database migrated by the real runner, a catalogue seeded with the ten real S07 episodes, settings
in their real file. With the switch off nothing is read; on, all 20 ranges of the series land
within ±2 s and no row falls outside the truth; the manual marker suppresses the detected intro
and leaves the credits; correcting a detection marks it corrected and a real re-detection returns
it intact, same identifier and range. The report lives in `artifacts/test-results/T43/physical/`.

### Performance and privacy

- **Nothing is extracted while something plays**: measured with timestamps, the first read happens
  after playback ends.
- Analysing a twelve-episode series costs under ten seconds, and a 20 ms player beat never loses
  more than 250 ms meanwhile.
- A full nine-episode detection opens **no** HTTP request and resolves **no** name on the managed
  side, measured with the same event sources as `PRI-001`; the native engine stays governed by
  `--no-metadata-network-access`. The tree's `HttpClient` owner list is unchanged.
- The corpus is synthetic and generated; `RepositoryPrivacyTests` stays green on every run.

### Coverage of the new files

All thirteen new files exceed 96 % on lines **and** branches; the worst sits at 98.3 %.

| File | Lines | Branches |
|---|---:|---:|
| `SegmentDetectionPolicy.cs` | 100 % | 100 % |
| `DetectedMarker.cs`, `IAutomaticSegmentDetector.cs`, `IDetectedMarkerRepository.cs` | 100 % | 100 % |
| `DetectSeriesSegments.cs`, `ReviewDetectedSegments.cs` | 100 % | 100 % |
| `DetectedMarkerRepository.cs` | 100 % | 100 % |
| `LocalSegmentFeatureExtractor.cs` | 98.3 % | 98.5 % |
| `AutomaticSegmentDetector.cs` | 99.3 % | 98.3 % |
| The two view models and their views | 100 % | 100 % |
| `SegmentDetectionBackground.cs` | 100 % | 100 % |

What remains uncovered are two defensive guards against states no test can provoke: a `Play()`
the engine refuses with a media already assigned, and a cluster set where none reaches two
episodes despite supported intervals existing.

### Declared limitations

- The synthetic corpus measures the mechanism, not the aesthetic variety of real shows; the
  subspec says so.
- A recap is detected by its recurring portion; one that changes completely every episode does not
  exist for this detector.
- The fingerprint cache lives in memory per session; re-detecting within a session does not
  re-decode, across sessions it does.
- A series' detection is scheduled when one of its episodes opens and waits for playback to end;
  flipping the switch does not re-read what was already watched until the next open.
