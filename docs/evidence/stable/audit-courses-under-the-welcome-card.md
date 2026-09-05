# Cursos se dibujaba debajo de la bienvenida / Courses Was Drawn Under the Welcome Card

- IDs: `CRS-003`, `PRD-006`
- Fecha / Date: 2026-09-05
- Alcance / Scope: `Presentation/Shell/ShellViewModel`, `UiTests/Shell`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Qué se veía

La pantalla de Cursos salía **ilegible**: «Cursos» y «Tu biblioteca, en tu PC» escritos uno encima
del otro en la misma línea, y las dos descripciones también superpuestas. No es un detalle de
alineación — el texto no se puede leer.

### Cómo apareció, y por qué ninguna prueba lo decía

Salió de **fotografiar la aplicación real al lado del prototipo**, en la primera pareja que nadie
había mirado nunca: la comparación anterior cubrió ocho pantallas y Cursos no estaba entre ellas.

La tarjeta de bienvenida se retira mirando una lista de superficies, y **Cursos no estaba en ella**.
Cursos llegó después y nadie la amplió.

**Y no había puerta que pudiera verlo**, lo que importa más que el defecto:

- Las puertas de desborde montan **cada vista por separado** — que es justo lo que las hace ver todas
  las ramas a la vez—, así que dos superficies dibujadas una sobre otra es exactamente lo que no
  pueden mirar.
- Sí existía una prueba, `The_review_route_shows_the_inbox_instead_of_the_welcome_card`, escrita para
  **una** ruta y nunca generalizada.

### La corrección, y por qué no es una línea

La línea que faltaba se añadió. Pero **una prueba por ruta es la forma que dejó pasar esto**, así que
la guarda nueva es una **tabla cerrada sobre el enumerado de rutas**: una ruta que no esté en la tabla
falla, y una ruta sobre la que la bienvenida no se retire falla.

Son tres afirmaciones, y cada una es ciega a lo que la otra ve:

| Qué afirma | Qué no vería sola |
| --- | --- |
| Toda ruta está en la tabla | que la lista real la ignore |
| La bienvenida se retira para cada una, leído del código | un signo invertido |
| Cursos enseña su cuadrícula, montando el shell | una ruta que nadie pudo construir |

**Comprobado que ven**: quitando otra vez la línea, dos de las tres fallan y la nombran —
`The welcome card is drawn over these destinations…: Courses (IsCoursesVisible)`.

### La trampa que casi hace perseguir un fantasma

**El arnés de captura apunta al binario de Debug por defecto, y el ciclo compila en Release.** Con el
arreglo puesto, la captura **seguía saliendo rota**: estaba fotografiando código viejo.

Se cerró midiendo en las dos direcciones con `-Exe` apuntando a Release:

| Binario | Qué se lee |
| --- | --- |
| Release **sin** la línea | «Cursos» y «Tu biblioteca, en tu PC» superpuestos |
| Release **con** la línea | «Cursos», solo, con su descripción |

Es la misma trampa que `--no-build` sin haber compilado, un piso más arriba: **una captura de un
binario que no es el que acabas de tocar miente igual que una prueba sobre binarios viejos**.

### El verde

`Domain` 743, `Application` 353, `Architecture` 39, `Ui` 1.240, `Accessibility` 150.

---

## English

### What was on screen

The Courses screen was **unreadable**: «Cursos» and «Tu biblioteca, en tu PC» written over each other
on the same line, and both descriptions overlapping too. Not an alignment detail — the text cannot be
read.

### How it turned up, and why no test said so

It came out of **photographing the real application beside the prototype**, in the first pair nobody
had ever looked at: the previous comparison covered eight screens and Courses was not among them.

The welcome card stands down by consulting a list of surfaces, and **Courses was not in it**. Courses
arrived afterwards and nobody widened it.

**And no gate could have seen it**, which matters more than the defect:

- The overflow gates mount **each view on its own** — which is exactly what makes them see every
  branch at once — so two surfaces drawn over one another is precisely what they cannot look at.
- A test did exist, `The_review_route_shows_the_inbox_instead_of_the_welcome_card`, written for
  **one** route and never generalised.

### The fix, and why it is not one line

The missing line was added. But **one test per route is the shape that let this through**, so the new
guard is a **closed table over the route enum**: a route not in the table fails, and a route the
welcome card does not stand down for fails.

Three assertions, each blind to what the others see:

| What it asserts | What it alone would miss |
| --- | --- |
| Every route is in the table | the real list ignoring it |
| The card stands down for each, read from the source | a flipped sign |
| Courses shows its grid, with the shell mounted | a route nobody could build |

**Shown to see**: removing the line again fails two of the three, and names it —
`The welcome card is drawn over these destinations…: Courses (IsCoursesVisible)`.

### The trap that nearly meant chasing a ghost

**The capture harness points at the Debug binary by default, and the cycle builds Release.** With the
fix in place the capture **still came out broken**: it was photographing old code.

Settled by measuring both ways with `-Exe` pointed at Release:

| Binary | What reads |
| --- | --- |
| Release **without** the line | «Cursos» and «Tu biblioteca, en tu PC» overlapping |
| Release **with** the line | «Cursos», alone, with its description |

Same trap as `--no-build` without having built, one floor up: **a capture of a binary that is not the
one you just touched lies exactly as much as a test over stale binaries**.

### Green

`Domain` 743, `Application` 353, `Architecture` 39, `Ui` 1,240, `Accessibility` 150.
