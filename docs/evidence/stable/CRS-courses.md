# El hilo que la aplicación guarda por ti / The thread the application keeps for you

Evidencia de **CRS-002**, **CRS-003** y **CRS-005**: un curso es una carpeta de vídeos numerados, la
aplicación guarda por dónde ibas, y marcar una lección escribe donde el progreso ya vivía. /
Evidence for **CRS-002**, **CRS-003** and **CRS-005**: a course is a folder of numbered videos, the
application keeps where you were, and marking a lesson writes where progress already lived.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-30. Decisión / Decision:
[`ADR-0006`](../../adr/0006-courses-are-a-third-kind-of-title.md).

## El progreso no es nuevo, y eso es la mitad del trabajo / Progress is not new, and that is half the work

`CourseProgressKey` guarda la posición de una lección bajo la clave que **PLY-008 ya usa**, con el
curso donde va un título y la lección donde va un episodio. Reanudar, el umbral de visto de PLY-009,
la marca manual que gana sobre él y la cuenta atrás de PLY-011 siguen funcionando **sin saber que
existen los cursos**. / A lesson's position goes under the key PLY-008 already uses, so resume, the
watched threshold, the manual mark and the countdown all keep working without knowing courses exist.

Inventar una tabla `lesson_progress` habría sido una segunda respuesta a «¿por dónde iba?», y dos
respuestas a una pregunta es como empiezan a contradecirse. / Inventing a `lesson_progress` table
would have been a second answer to one question.

**Lo que había que medir era la clave**, porque se compone en dos sitios: por concatenación en SQL
—`'title:' || course_id || '/episode:' || lesson_id`— y desde `ContentKey` en C#. Hay una prueba de
integración que **escribe por uno y lee por el otro** contra una base real. / The key is composed in
two places, so a test writes through one and reads through the other.

## Lo que el paseo pulsa con el ratón / What the walk presses with a mouse

`The_courses_destination_marks_a_folder_and_carries_on_with_a_course` recorre las seis:

| control | efecto comprobado / effect asserted |
| --- | --- |
| `CoursesMarkFolderAction` | el diálogo de añadir medios se abre |
| `{Binding AccessibleName}` de la tarjeta | el curso se abre bajo la cuadrícula |
| `{Binding MarkAccessibleName}` de la fila | **el estado de visto en el almacén**, no el glifo |
| `{Binding ThreadActionText}` | la sesión se abre en la lección que el hilo señala |
| `{Binding AccessibleName}` de la fila | la lección se reproduce |
| `{Binding ActionText}` de la tarjeta | abre el curso y arranca el hilo de una vez |

**La marca se comprueba contra el almacén y no contra el glifo**, porque un glifo sólo demostraría
que la fila se volvió a dibujar. La aserción es que `watch_state` tiene `Watched` y
`IsManualOverride`. / The mark is asserted against the store rather than the glyph, because a glyph
would only prove the row redrew itself.

El trinquete del paseo **no se movió**: 238 controles declarados, 213 pulsados, 20 pendientes, los
mismos veinte de antes. / The walk ratchet did not move.

## El defecto que sólo el ratón encontró / The defect only the mouse found

`IsCoursesVisible` **no estaba** entre las propiedades que el shell anuncia al navegar. El destino
existía, la ruta cambiaba y **la pantalla no se dibujaba**. Ninguna prueba de ViewModel lo habría
visto: el booleano era correcto y nadie escuchaba su cambio. El paseo falló por no encontrar el
botón, que es exactamente para lo que sirve pulsar con un ratón en vez de leer un booleano. / The
destination existed, the route changed and the screen never drew. No ViewModel test would have seen
it: the boolean was right and nothing listened to its change.

## Tres defectos más que encontraron las pruebas / Three more the tests found

1. **La política normaliza las rutas a `/`** y el caso de uso indexaba el diccionario con las barras
   tal y como venían del enumerador: el mapa se construía con unas claves y se leía con otras.
2. **El módulo guardaba el nombre de la carpeta** (`01 - Módulo uno`) donde la ficha pide el título
   (`Módulo uno`), porque el número ya viaja aparte en `module_sort_major`.
3. **SQLite pone los NULL primero en `ORDER BY`**, así que una lección sin número de cabecera habría
   abierto todos los cursos. La consulta ordena por `sort_major IS NULL` antes que por `sort_major`.

Y una cuarta del arnés: en la lista de tablas de `SqliteBootstrapTests`, `lessons` va **antes** que
`library_roots`, porque la colación es binaria y ahí `e` va antes que `i`. Ordenado «de leer», la
prueba se pone roja en el índice 13. / Under SQLite's binary collation `e` precedes `i`.

## Suites en verde / Suites green

`Domain.Tests` 638 · `Application.Tests` 281 · `ArchitectureTests` 30 · `DocumentationTests` 91 ·
`UiTests` 1047 · `IntegrationTests` 508 · `AccessibilityTests` 147 · `dotnet format` · la puerta de
documentación con 65 identificadores.

## Lo que falta para que estas tres pasen a VERIFIED / What is left before these reach VERIFIED

- La matriz de capturas de la aplicación junto al prototipo, en claro y oscuro, como pide el patrón
  de PRD-006. / The captures matrix beside the prototype.
- Una prueba de que el progreso de una lección **sobrevive a mover el archivo**, que es lo que
  CRS-005 promete y hoy sólo está garantizado por construcción. / A test that a lesson's progress
  survives the file being moved.
