# Dos ramas que ningún test puede tomar, y la lista que sube por primera vez / Two branches no test can take, and the list that rises for the first time

La tanda de Cursos trajo siete archivos por debajo del listón y en ninguna lista, y la puerta los
nombró. Dos de los siete **no son deuda perseguible: tienen techo medido**, y este documento es esa
medición. Con ella, el trinquete sube de 186 a 193 — la primera vez que esta lista crece. / The
Courses batch brought seven files under the bar and on no list. Two of them **are not chaseable debt:
they have a measured ceiling**, and this document is that measurement.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-30.

## Por qué la lista puede subir sin romper su regla / Why the list may rise without breaking its rule

La regla dice que la lista sólo encoge, y se escribió contra **la degradación**: un archivo que
estaba en el listón y empeora. `ARQ-004` adelgazó un archivo de 60,61/27,27 a 45,45/14,29 y ningún
guardián dijo nada, y de ahí salió el trinquete.

**Un archivo que NACE por debajo del listón no es eso.** No hay nada que recuperar, porque nunca
estuvo arriba. La propia puerta lo dice en su mensaje de error —«*Bring it back to the bar, or add it
to `eng/coverage-debt.txt` with the reason and raise the ratchet in the same change*»— y hasta hoy
nadie lo había ejercido: las 48 vistas del árbol entraron el día que la lista se creó, en agosto, y
desde entonces no se había añadido ninguna. / A file **born** below the bar is not degradation: there
is nothing to bring back, because it was never up. The gate's own error message allows it, and until
today nobody had used that path.

Lo que la regla sigue impidiendo, intacto: un archivo de la lista que empeore falla igual.

## Techo 1: la única rama de un `.axaml` la escribe el compilador / The one branch in an `.axaml` is the compiler's

Los tres `.axaml` de Cursos miden **100/50**, y ese par no es un defecto suyo: es lo que mide **toda**
vista de este repositorio.

Medido sobre las 48 vistas, leyendo el Cobertura de `UiTests`: **todas tienen exactamente una línea
con ramas, siempre la del elemento raíz, y siempre a `1/2`**. Ninguna otra línea de un `.axaml` tiene
rama ninguna.

```
UpdateView.axaml       L5 hits=156  50% (1/2)
EpisodeRowView.axaml   L5 hits=28   50% (1/2)
ShowDetailsView.axaml  L5 hits=198  50% (1/2)
App.axaml              L5 hits=0     0% (0/2)
```

Es la rama que el compilador de Avalonia genera al convertir el `.axaml` en código; no la escribió
nadie de este proyecto y **ninguna prueba la ha tomado jamás en 48 vistas**. No se pudo leer el
fuente generado —Avalonia no deja `.g.cs` en `obj/`— así que **no se afirma cuál es la condición**,
sólo lo que la medición sostiene: existe una, es única, y está fuera del alcance de una prueba.

Eso explica de golpe un número que llevaba meses en el árbol: **63 de los 69 archivos con el par
exacto `100/50` son vistas**. / It is the branch Avalonia's compiler emits for an `.axaml`. Nobody
here wrote it and no test has ever taken it across 48 views. Which explains a number that had been
in the tree for months: 63 of the 69 files measuring exactly `100/50` are views.

## Techo 2: el caché de delegado de una lambda que captura / The delegate cache of a capturing lambda

`CourseThreadPolicy.cs` se queda en **100/93** por una sola rama, y el JSON de coverlet la nombra:
línea 147, offset 26, en `Recap`. La línea es

```csharp
: lessons.TakeWhile(lesson => lesson.Id != thread.Lesson).Count();
```

La lambda **captura `thread`**, así que su clausura se reconstruye en cada llamada y el campo donde
el compilador cachea el delegado nace nulo siempre: el `dup; brtrue.s` que lo comprueba no salta
nunca. Es la **cuarta** rama inalcanzable de esta misma forma que `Domain` encuentra, tras las tres
del 2026-08-30.

**Y se midió haciendo, no razonando**: se reescribió `Recap` con un bucle sin lambda y la rama
**desapareció del informe**. El cambio se revirtió, porque el bucle aporta ocho ramas propias y deja
el archivo en 94 — peor negocio que el techo. / Measured by doing: rewriting `Recap` without the
lambda made the branch vanish from the report. The change was reverted, because the loop brings eight
branches of its own and leaves the file at 94 — a worse deal than the ceiling.

## Los otros cuatro sí son deuda, y quedan nombrados / The other four are debt, and are named

`CourseLessonReader` 100/88, `CoursesViewModel` 96/58 y `CourseDetailsViewModel` 93/58. Los dos
ViewModels no tienen pruebas unitarias propias: los cubre el paseo autónomo a través del shell
ensamblado, que mide comportamiento y no ramas.

## Lo que NO se hace aquí, y por qué / What is NOT done here, and why

Si la puerta ignorase la cobertura de **ramas** de un `.axaml` —no el archivo, cuyas líneas sí miden
algo real— **saldrían 63 archivos de la lista** y el trinquete caería de 193 a unos 123. Es la
corrección de fondo y está medida, pero cambiar la puerta central para que deje de mirar algo es
exactamente la forma que este repositorio llama «aflojar una puerta»: merece su tanda, su evidencia y
su vuelta de CI, no ir de rondón en una que existe para desbloquear `main`. / The deep fix is
measured and named, and deliberately not taken here.

## Cómo se verificó / How it was verified

Las 193 filas son el artefacto `coverage-debt` del run `33332028974`, copiadas verbatim y convertidas
a LF. Las dos afirmaciones del guion que decían que la lista sólo encoge se corrigen en el mismo
cambio, porque una nota que sobrevive a lo que describía es como este árbol acumula frases falsas
sobre sí mismo. / The 193 rows are the run's artefact verbatim; the two claims in the script saying
the list only shrinks are corrected in the same change.
