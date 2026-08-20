# La escala de radios, y la hipótesis bonita que la medición tiró / The corner scale, and the neat hypothesis the measurement threw out

La tercera y última escala del árbol. Los 37 `CornerRadius` de `src/` piden ya el token, de los que 30
eran literales repartidos por 26 archivos. Con ella, **ninguna de las tres escalas —tipografía,
espaciado y radios— admite ya un número escrito a mano**, y las tres tienen puerta. / The third and
last scale in the tree. All 37 `CornerRadius` sites in `src/` now ask the token.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que había / What was there

| Valor / Value | Sitios / Sites | ¿En la escala 4/8? |
|---|---|---|
| 8 | 18 | sí / yes |
| 4 | 5 | sí / yes |
| 6 | 4 | no |
| 10 | 2 | no |
| 12 | 1 | no |

## La hipótesis, y por qué merecía tenerse / The hypothesis, and why it was worth having

**Los tres radios mayores —10, 10 y 12— son las tres superficies de tarjeta.** `LibraryEntryView`,
`ResumeHeroView` y la tarjeta de bienvenida de `ShellView`, las tres con `CardSurfaceBrush`. Y el
argumento de diseño es real: una tarjeta grande con el mismo radio que un botón se ve mal.

Aplicando el criterio que acababa de decidir la escala de espaciado —«si un valor fuera de la escala
concentra la desviación, es un escalón que falta»—, tocaba declarar un `CornerRadiusLarge`. / By the
criterion that had just decided the spacing scale, a `CornerRadiusLarge` was due.

## La medición la refutó / The measurement refused it

**De las siete superficies pintadas con `CardSurfaceBrush`, cuatro ya llevaban 8:**

```
LibraryEntryView        10
ResumeHeroView          10
ShellView               12
LooseFileBanner          8
SubtitleStyleView        8
DiagnosticsPreviewView   8
UpdateView               CornerRadiusMedium (8)
```

**Cuatro a ocho contra tres a diez o doce no es un escalón que el árbol pida: es un reparto que nadie
decidió.** Un token nuevo habría convertido una inconsistencia en una regla, con el aval de una
métrica que decía lo que se quería oír. / **Four at eight against three at ten or twelve is not a step
being asked for, it is a split nobody decided.**

Y los cuatro sitios de valor 6 tampoco comparten nada: dos llevan `AccentSubtleBrush` y dos no llevan
fondo. / The four sites at 6 share nothing either.

**Así que los siete van a `CornerRadiusMedium` y la escala se queda en dos valores.** Un solo sitio se
mueve más de 2 px —la tarjeta de bienvenida, de 12 a 8— y se mueve **hacia** las otras seis. / So all
seven become `CornerRadiusMedium`. One site moves more than 2px, and it moves *towards* the other six.

**Los nombres se quedan semánticos** donde los de espaciado pasaron a numéricos, y la razón es la
misma que obligó a renombrar aquéllos: **dos valores sin hueco entre ellos no pueden desarrollar un
escalón que falte.** El renombrado de la escala de espacio existió porque el 12 no cabía en
`Small`/`Medium`; aquí no hay dónde. / **The names stay semantic**, because two values with no gap
between them cannot develop a missing step.

## La forma general / The general shape

**Un criterio recién probado es exactamente cuando más fácil es aplicarlo mal.** «Si un valor
concentra la desviación, falta un escalón» acababa de acertar con el 12 del espaciado, y aquí encajaba
igual de bien de lejos. Lo que separa los dos casos es un dato que sólo aparece **mirando el otro
lado**: en el espaciado, ningún sitio de los 46 usaba otro valor para lo mismo; aquí, la mayoría de
las tarjetas ya usaba el valor de la escala. **La pregunta no es «¿tiene sentido el escalón?» sino
«¿lo contradice algo que ya está en el árbol?»**. / **A criterion that has just been proved is exactly
when it is easiest to misapply.** The question is not "does the step make sense?" but "does anything
already in the tree contradict it?"

## La puerta, probada fallando / The gate, proved by failing

```
CornerRadius="12" literal de vuelta en ShellView
  -> a view writes its own corner radius instead of asking the scale:
       ShellView.axaml: CornerRadius="12"
```

Con su suelo anticeguera en 37 referencias, porque un lector que dejara de leer pasaría midiendo
nada. / With its anti-blindness floor at 37 references.

## El verde / The green

```
UiTests             612/612
AccessibilityTests  135/135
IntegrationTests    456/457 (1 omitida por diseño / 1 skipped by design)
DocumentationTests   87/87
```

La línea base estructural de Inicio **no se movió**: un radio no cambia dónde termina nada. / Home's
structural baseline did not move: a radius changes where nothing ends.
