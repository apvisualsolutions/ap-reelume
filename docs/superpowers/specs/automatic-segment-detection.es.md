# Detección automática de segmentos — subespecificación congelada

- Estado: `FROZEN` — aprobada por el propietario el 2026-08-07
- Idioma: español; la traducción inglesa está en
  [automatic-segment-detection.en.md](automatic-segment-detection.en.md)
- IDs: `PLY-013`; relacionados `PLY-012`, `PRI-001`
- Tarea: T43 del [plan de implementación](../plans/2026-08-01-ap-reelume-windows-mvp-implementation.md)
- Documentos: [`docs/FEATURES.md`](../../FEATURES.md),
  [T29 — marcas manuales](../../evidence/mvp/T29-manual-markers.md)

Esta subespecificación se congela **antes** de escribir código de detección. Los umbrales y el
corpus de abajo son los aprobados; cambiarlos exige una nueva aprobación del propietario y queda
registrado como cambio de esta página en los dos idiomas.

## 1. Qué detecta y qué no

El detector encuentra **segmentos recurrentes** dentro de una misma serie: tramos de audio que se
repiten entre episodios. Con eso clasifica tres tipos del modelo compartido de T29 (`MarkerKind`):

- **Intro**: el tramo recurrente presente en la mayoría de los episodios en la ventana inicial.
- **Resumen (recap)**: un tramo recurrente distinto del de la intro que, en los episodios donde
  aparece, precede a la intro dentro de la ventana inicial.
- **Créditos**: el tramo recurrente en la ventana final.

Lo que no se repite no se detecta. Un resumen real cuyo contenido cambia por completo en cada
episodio sólo es detectable en su porción recurrente (careta o sintonía); la verdad terreno del
corpus define el resumen exactamente así. Esta limitación es deliberada y queda declarada.

La versión 1 del detector usa **sólo audio**. El vídeo del corpus existe para que los episodios
sean archivos reales reproducibles, no como señal.

## 2. Contrato del detector

- **Local y sin red.** Ninguna extracción ni comparación abre una conexión. Se verifica con el
  mismo patrón de canario de `PRI-001`.
- **Cancelable.** La cancelación responde y no deja marcas a medio escribir.
- **Baja prioridad y pausable.** El trabajo corre con prioridad baja y se pausa mientras hay una
  reproducción activa; la pausa se mide, no se declara.
- **Acotado.** Sólo se extraen la ventana inicial y la final de cada episodio; nunca el episodio
  completo. Las huellas extraídas se guardan en caché para no re-extraer.
- **Produce, nunca impone.** Las marcas manuales de T29 son por serie; una detección es por
  episodio, porque un cold open variable mueve la intro de sitio en cada episodio y una marca fija
  por serie no puede representarlo. Cada detección se persiste por episodio con tipo, rango,
  confianza y versión del detector, y el reproductor la consume con la misma forma de
  `IntroMarker` (`Origin = Detected`). Si la serie tiene una marca manual de un tipo, las
  detecciones de ese tipo no se usan; una re-detección reemplaza las detecciones no corregidas y
  **jamás** toca una marca `Manual` ni una detección con `UserCorrected = true`.
- **Revisable.** La interfaz permite revisar, aceptar, corregir o eliminar cada detección.
  Corregir una detección la convierte en `UserCorrected = true`.
- **Apagable.** La detección es una opción; desactivada, no se extrae ni se compara nada.

## 3. Corpus de evaluación

Aprobado: corpus **íntegramente sintético y generado**, nunca la biblioteca personal ni material
de terceros. En el repositorio viven la estructura y la verdad terreno
(`tests/ApSolutions.LocalMedia.MediaTests/Fixtures/segment-corpus-manifest.json`); el vídeo se
materializa bajo `artifacts/test-media/segments/` (ignorado por git) con ffmpeg y codificadores
nativos (`mpeg4` + `aac`), reproducible desde cero en cualquier máquina.

Cada episodio se compone en este orden: `[resumen] [cold open] [intro] cuerpo [créditos]`, donde
cada pieza es opcional según la serie. El audio de cada pieza es una secuencia determinista de
acordes (un acorde de dos senos cada 2,5 s) derivada de una semilla: las piezas recurrentes usan
la semilla de la serie y las piezas únicas una semilla por episodio, de modo que lo recurrente se
repite de verdad y lo único no se parece a nada más. El acorde en lugar del tono simple hace el
espacio por paso lo bastante grande para que dos semillas distintas no compartan por azar ninguna
racha de cuatro acordes, y una prueba del corpus lo comprueba.

### Series

| Serie | División | Ep. | Patrón que aporta |
|---|---|---:|---|
| S01 | desarrollo | 10 | Intro 25 s exacta desde 0 s; créditos 30 s. El caso base. |
| S02 | desarrollo | 10 | Cold open variable (5–45 s) antes de la intro 25 s; créditos 30 s. |
| S03 | desarrollo | 9 | Resumen 15 s en 6 de 9 episodios, luego intro 20 s; créditos 25 s. |
| S04 | desarrollo | 8 | Un especial sin ningún segmento; el resto cold open variable + intro 24 s + créditos 30 s. |
| S05 | desarrollo | 12 | Intro corta 12 s; resumen 15 s en 6 de 12; créditos largos 45 s. |
| S06 | desarrollo | 8 | **Sin ningún segmento recurrente.** Control de falsos positivos. |
| S07 | retenida | 10 | Cold open variable + intro 22 s + créditos 30 s. |
| S08 | retenida | 9 | Resumen 15 s en 5 de 9; intro 18 s; créditos 28 s; un especial sin segmentos. |
| S09 | retenida | 11 | Intro 30 s desde 0 s; créditos 35 s. |
| S10 | retenida | 8 | **Sin ningún segmento recurrente.** Control de falsos positivos retenido. |

95 episodios; cuerpos de 120–200 s por episodio, de duración variable. La verdad terreno de cada
episodio son los rangos de resumen, intro y créditos que resultan de su estructura; el cold open y
el cuerpo no son segmentos.

### Protocolo retenido

- El detector sólo se ajusta con las series de **desarrollo** (S01–S06).
- Las series **retenidas** (S07–S10) se ejecutan únicamente en la fase de verificación (T43.4) y
  son las que deciden los umbrales. Ajustar el detector después de mirar el retenido invalida la
  medición y obliga a regenerar un retenido nuevo antes de volver a medir.
- Las métricas se publican **agregadas y por serie**; un promedio nunca tapa una serie.

## 4. Definición de acierto y métricas

- Una detección **acierta** cuando coincide el tipo y sus dos fronteras caen a ≤ 2,0 s de las de
  un rango de la verdad terreno del episodio (`|inicio−inicio| ≤ 2 s` y `|fin−fin| ≤ 2 s`).
- **Precisión** por tipo: detecciones acertadas / detecciones emitidas de ese tipo.
- **Exhaustividad** por tipo: rangos de verdad terreno acertados / rangos existentes de ese tipo.
- **Detección espuria**: un episodio sin ningún segmento en la verdad terreno que recibe al menos
  una detección. La tasa es sobre el conjunto de episodios sin segmento.

## 5. Umbrales aprobados

Medidos sobre las series retenidas, con la tolerancia de ±2,0 s por frontera:

| Métrica | Umbral |
|---|---|
| Intro: precisión | ≥ 0,90 |
| Intro: exhaustividad | ≥ 0,80 |
| Créditos: precisión | ≥ 0,90 |
| Créditos: exhaustividad | ≥ 0,80 |
| Resumen: precisión | ≥ 0,85 |
| Resumen: exhaustividad | ≥ 0,70 |
| Episodios sin segmento: tasa espuria agregada | ≤ 5 % |
| Cada serie individual: precisión | ≥ 0,80 |
| Cada serie individual: tasa espuria | ≤ 10 % |

Con 9 episodios retenidos sin segmento, el 5 % agregado tolera en la práctica **cero** episodios
con detección espuria; se deja constancia de que esa severidad es intencionada.

`PLY-013` pasa a `VERIFIED` sólo si **todos** los umbrales se cumplen, los agregados y los de cada
serie. Si alguno falla, el estado queda en el valor honesto que corresponda con su condición.

## 6. Sensibilidad del banco (T43.2)

Antes de implementar nada, el corpus se ejecuta contra un **detector nulo** (no emite nada) y un
baseline trivial de posición fija (por ejemplo «intro = primeros 30 s, créditos = últimos 30 s»).
Ambos deben **incumplir** los umbrales; si alguno los cumpliera, el banco no mide nada y se
corrige antes de continuar. Las métricas de ambos se archivan con la evidencia RED.

## 7. Limitaciones declaradas

- El corpus sintético mide el **mecanismo** de comparación intra-serie (alineación, fronteras,
  clasificación, falsos positivos), no la variedad estética de series reales. Los umbrales sobre
  series reales quedan fuera del alcance de T43 y se revisarían con material real redistribuible.
- El resumen se define como porción recurrente; un resumen sin porción recurrente no es detectable
  y no cuenta en la verdad terreno.
- La versión 1 no usa señal de vídeo ni metadatos de capítulos del contenedor.
