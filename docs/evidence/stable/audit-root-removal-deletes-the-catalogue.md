# Retirar una carpeta borra su catálogo / Removing a Folder Deletes Its Catalogue

- Fecha / Date: 2026-09-06
- Relacionado / Related: [`LIB-001`](../../FEATURES.md), [`LIB-010`](../../FEATURES.md),
  [`PRD-006`](../../FEATURES.md), [ADR-0011](../../adr/0011-a-destructive-question-floats-and-blocks.md),
  [la vuelta cuatro de paridad](audit-prototype-fidelity-round-four.md)

Este documento contiene primero la medición en español y después su traducción inglesa.

This document contains the measurement in Spanish first and its English translation second.

---

## Español

### Lo que se midió antes de decidir

El propietario pidió la cifra antes de elegir. Se leyó la biblioteca sembrada que usan las capturas,
en modo sólo lectura, y se contrastó contra el código que la genera. Los dos caminos cuadran.

| Qué | Cuánto |
| --- | --- |
| Carpetas de biblioteca | **1** — retirar «una carpeta» se lo lleva todo o nada |
| Títulos | **9** (8 películas + 1 serie de 3 temporadas y 16 episodios) |
| Archivos de medios | **28** |
| Progreso de reproducción | **14 filas = 706,4 minutos** (11 h 46 min); 11 terminados, 3 en curso |
| Favoritos, ver después, valoraciones | **0** |
| Marcadores de intro y créditos | **0** |
| Cursos y lecciones | **0** |

**Y lo que desaparecía era nada.** El borrado ejecutaba una sola sentencia —la fila de la carpeta— y
descartaba en su primera línea la bandera que habría decidido el resto. Los 9 títulos y los 706
minutos se quedaban en el catálogo marcados como **disponibles**, aunque su carpeta ya no se
vigilase.

Las dos mediciones difieren en 22 segundos —706,37 contra 706— y el motivo está identificado: una
sesión de captura dejó correr el vídeo real.

### Las tres versiones incompatibles

| Quién | Qué decía |
| --- | --- |
| El prototipo, en tres sitios | «El catálogo conserva sus elementos como no disponibles» |
| El aviso de la aplicación | «Sus títulos salen del catálogo, junto con sus marcas y su progreso» |
| El código | Sólo `DELETE FROM library_roots` |

### La puerta que lo tapaba

La única prueba que mencionaba la bandera afirmaba **por reflexión que el parámetro tenía `true` por
defecto**. Verificaba una firma y no un efecto, y ninguna prueba del árbol contaba una fila después
de retirar. Se retiró y en su lugar quedaron pruebas que cuentan filas.

**Y no dejó de compilar al quitar la bandera, que es lo que se había predicho.** El arnés la
construía por reflexión, así que habría fallado en ejecución. Una puerta escrita con reflexión no
avisa al compilador.

### El rojo archivado

Las tres primeras pruebas se escribieron contra la API de entonces, para que el rojo fuera del
comportamiento y no de la compilación:

| Prueba | Qué dijo el rojo |
| --- | --- |
| `A_title_whose_only_files_were_in_the_removed_root_leaves_with_its_marks_and_progress` | `media_files` esperaba 0 y había **1** |
| `A_show_with_episodes_in_two_roots_survives_removing_one` | `media_files` esperaba 1 y había **2** |
| `A_removed_root_stops_matching_a_search` | `catalog_fts` esperaba 0 y había **1** |

**La segunda se esperaba verde y salió roja, y eso mejoró la pareja.** Se había anotado que pasaría
«por accidente», porque hoy no se borra nada; medida, falla también, porque afirma además que el
archivo y el episodio de la carpeta retirada sí se van. Ninguna de las dos mitades es una puerta
ciega.

### La prueba del recuento, vista fallar por mutación

`The_notice_counts_exactly_what_the_removal_then_deletes` se escribió con el código ya puesto, así
que se comprobó que puede fallar: sustituido el recuento por el error obvio —contar las filas del
conjunto en bruto, que incluye los episodios— la prueba dijo **5 títulos en vez de 3**. El control
positivo existe.

Compara el resumen contra el catálogo **antes y después**, no una consulta contra otra. Y el lector
construye sus conjuntos con la misma sentencia que ejecuta el borrado, así que no pueden divergir por
edición.

### Lo que las puertas dijeron al mover la superficie

- **`SurfaceCornerTests`: 79 → 78.** La confirmación estaba dibujada dos veces —primera ejecución y
  Ajustes— y pasó a ser una sola pregunta flotante. Dos sitios fuera, uno dentro. El trinquete sólo
  puede bajar, y bajó.
- **`LeadingActionTests`** exigió la decisión del botón líder para la vista nueva: **ninguno**.
- **El paseo autónomo: 150 de 150**, y su trinquete quieto en **23**. La pregunta flotante se alcanza
  y se pulsa; ni un `Flyout` ni una ventana modal lo habrían permitido.
- **`UiTests`: 1.254 de 1.254.** Un primer intento dio veinticinco fallos en pruebas del shell que no
  tocan la retirada: era `Padding="{DynamicResource Space24}"`, y ese token es un número, no un
  grosor de cuatro lados. El shell entero no se montaba.
- **Una aserción medía `IsVisible` y tenía que medir `IsEffectivelyVisible`**: el bloque de texto
  seguía marcado visible mientras quien estaba oculto era el panel que lo contiene.

---

## English

### What was measured before deciding

The owner asked for the figure before choosing. The seeded library the captures use was read in
read-only mode and cross-checked against the code that generates it. The two paths agree.

| What | How much |
| --- | --- |
| Library folders | **1** — removing "a folder" takes everything or nothing |
| Titles | **9** (8 films + 1 show with 3 seasons and 16 episodes) |
| Media files | **28** |
| Playback progress | **14 rows = 706.4 minutes** (11 h 46 min); 11 finished, 3 in progress |
| Favourites, watch later, ratings | **0** |
| Intro and credit markers | **0** |
| Courses and lessons | **0** |

**And what disappeared was nothing.** The removal ran one statement — the folder's row — and
discarded on its first line the flag that would have decided the rest. The 9 titles and the 706
minutes stayed in the catalogue marked **available**, although their folder was no longer watched.

The two measurements differ by 22 seconds — 706.37 against 706 — and the reason is identified: a
capture session left the real video running.

### The three incompatible versions

| Who | What it said |
| --- | --- |
| The prototype, in three places | «The catalogue keeps its items as unavailable» |
| The application's notice | «Its titles leave the catalogue, along with their marks and progress» |
| The code | Only `DELETE FROM library_roots` |

### The gate that covered it

The one test that mentioned the flag asserted **by reflection that the parameter defaulted to true**.
It checked a signature and not an effect, and no test in the tree counted a row after a removal. It
is gone, and tests that count rows took its place.

**And removing the flag did not stop it compiling, which is what had been predicted.** The harness
built it by reflection, so it would have failed at run time. A gate written with reflection does not
warn the compiler.

### The archived red

The first three tests were written against the API as it then was, so the red would be about
behaviour and not about compilation:

| Test | What the red said |
| --- | --- |
| `A_title_whose_only_files_were_in_the_removed_root_leaves_with_its_marks_and_progress` | `media_files` expected 0 and had **1** |
| `A_show_with_episodes_in_two_roots_survives_removing_one` | `media_files` expected 1 and had **2** |
| `A_removed_root_stops_matching_a_search` | `catalog_fts` expected 0 and had **1** |

**The second was expected green and came out red, and that improved the pair.** It had been noted it
would pass "by accident", since nothing is deleted today; measured, it fails too, because it also
asserts that the leaving folder's own file and episode do go. Neither half is a blind gate.

### The count test, seen to fail by mutation

`The_notice_counts_exactly_what_the_removal_then_deletes` was written with the code already in place,
so it was checked that it can fail: replacing the count with the obvious mistake — counting the raw
set, which includes the episodes — the test said **5 titles instead of 3**. The positive control
exists.

It compares the summary against the catalogue **before and after**, not one query against another.
And the reader builds its sets with the very statement the removal runs, so they cannot diverge by
editing.

### What the gates said when the surface moved

- **`SurfaceCornerTests`: 79 → 78.** The confirmation was drawn twice — first run and Settings — and
  became one floating question. Two sites out, one in. The ratchet only falls, and it fell.
- **`LeadingActionTests`** demanded the leading-button decision for the new view: **none**.
- **The autonomous walk: 150 of 150**, its ratchet still at **23**. The floating question is reached
  and pressed; neither a `Flyout` nor a modal window would have allowed that.
- **`UiTests`: 1,254 of 1,254.** A first attempt gave twenty-five failures in shell tests that do not
  touch removal: it was `Padding="{DynamicResource Space24}"`, and that token is a number, not a
  four-sided thickness. The whole shell failed to mount.
- **One assertion measured `IsVisible` where it had to measure `IsEffectivelyVisible`**: the text
  block was still flagged visible while what was hidden was the panel holding it.
