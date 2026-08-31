# ADR-0006 — Un curso es un tercer tipo de título / A Course Is a Third Kind of Title

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-08-30
- Decisor / Decision owner: Product Owner, a propuesta de Engineering / on Engineering's proposal
- Relacionado / Related: [`FEATURES.md`](../FEATURES.md), [hoja de ruta / roadmap](../roadmap/README.es.md),
  `LIB-005`, `PLY-009`, `PLY-011`

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

La biblioteca de quien usa esta aplicación contiene cursos y videotutoriales: carpetas de vídeos
numerados que se ven en orden. Son archivos locales como cualquier otro, y catalogarlos y
reproducirlos es exactamente lo que este proyecto existe para hacer.

**Este ADR no se escribió contra ejemplos inventados.** Se midió una colección real de **595 vídeos
en 15 cursos**, y los números de abajo salen de ahí. Se miró además una segunda raíz que hoy tiene
siete carpetas y **ningún archivo**: de ésa no sale ningún número, sólo su **forma de carpetas**, y
se dice porque es justo la que tumba la regla de profundidad fija. Ningún nombre de esa colección
aparece aquí: la regla 5 de este repositorio prohíbe escribir la biblioteca de nadie en el árbol,
así que lo que sigue son patrones.

#### Lo que el parser hace hoy con esa colección

`MediaNameParser` conoce cuatro formas —`S01E02`, `1x02`, `temporada N episodio N` y
`capítulo NNN`— y ninguna es la de un curso.

| resultado | cuántos |
|---|---|
| `Unknown` | **594 de 595** |
| `Movie`, y es un falso positivo | 1 |
| `Episode` | 0 |

El falso positivo importa más que su tamaño. Una lección cuyo título contenía un año de cuatro
cifras se leyó como una película de ese año, **y el año se borró del título**: la lección se queda
con una frase rota, a la que le falta justamente la palabra que la fechaba. Que no haya ni un falso
positivo de episodio es la otra cara y es una buena noticia: ninguna numeración de curso se parece
tanto a `SxxExx` como para colisionar.

#### Cómo se numeran las lecciones de verdad

| patrón | proporción |
|---|---|
| `NN - título` y `NN-título` | 62,5 % |
| `NN. título` | 17,8 % |
| `NN_título` | 0,5 % |
| sin número de cabecera | 19,2 % |

Ese último 19,2 % **no es material sin numerar**, y suponerlo habría sido el error: lleva la
numeración en otro sitio. Aparecen esquemas codificados del tipo `XX_NNN_SS_LL` —idioma, curso,
sección, lección, con relleno de ceros— y numeración **entre paréntesis** en mitad del nombre. El
relleno de ceros tiene una consecuencia práctica útil: en esos casos el orden alfabético **coincide**
con el numérico, así que dejarlos al final en orden alfabético estable los ordena bien.

#### Un curso no es una carpeta de vídeos

De los **1955 archivos** de la colección medida, sólo **595 son vídeo**. El resto —más del 69 %— son
imágenes de secuencia, escenas de software 3D y de composición, PDF y ZIP: el material de trabajo del
curso. La carpeta de un curso es mayoritariamente proyecto, y la aplicación tiene que catalogar el
vídeo e **ignorar el resto sin tropezar con él**. `MediaFileExtensions` ya filtra por extensión, así
que esto no pide código nuevo, pero sí decide la forma de la detección de más abajo.

#### La profundidad no es fija

Las dos raíces miradas tienen formas distintas:

- una es `raíz / categoría / curso / [sección] / lección`, con cinco categorías por encima de los
  cursos;
- la otra es `raíz / curso / sección / lección`, sin categoría ninguna.

**Cualquier regla de profundidad fija habría acertado en una y fallado en la otra.** Fue el primer
diseño de este ADR —«una carpeta de primer nivel es un curso»— y mirar el disco lo tumbó. La segunda
raíz no aporta vídeos y aun así aporta esto, que es la razón de haberla mirado.

#### Lo que ya está construido

Casi todo lo demás que un curso necesita: la velocidad de reproducción con sus límites y su
preferencia persistente, reanudar donde se dejó, la lección siguiente con cuenta atrás, los estados
no iniciado/en curso/visto con umbral configurable, los subtítulos y las pistas de audio. El modelo
serie → temporada → episodio es además isomorfo a curso → sección → lección.

### El no-objetivo que esto toca, y hasta dónde

La hoja de ruta dice, en los dos idiomas:

> **No es una plataforma de cursos.** No hay lecciones, ni progreso de formación, ni certificados.

Esa frase **se acota, no se borra**, y lo que sigue fuera es la parte que la motivaba: no hay
matrículas, ni certificados, ni cuestionarios, ni rachas, ni estadísticas de estudio, ni porcentaje
de formación completada, ni nada que hable con una plataforma. Sigue sin haber servidor, cuentas,
streaming, telemetría ni sincronización.

Lo que entra es lo que la aplicación ya hace con una serie: reconocer lo que hay en el disco,
ordenarlo, reproducirlo en orden y recordar por dónde iba.

### Decisión

**1. `CatalogTitleKind.Course` es un tercer tipo de título**, junto a `Movie` y `Show`.

**2. Una raíz de biblioteca se declara de cursos, y la detección no adivina el tipo.** Es la decisión
que más consecuencias evita. Un curso y una serie mal nombrada se parecen demasiado —una carpeta con
`01.mkv`, `02.mkv`— y cualquier heurística que los separe se equivocará en los dos sentidos. Como el
tipo decide si algo sale a la red, **una clasificación equivocada no es cosmética**. La aplicación ya
tiene raíces de biblioteca y quien organiza cursos los tiene aparte, así que la señal existe y es del
usuario, no del programa.

**3. Cada raíz de cursos declara a qué profundidad están sus cursos, y el programa no lo adivina.**
**Enmendada el 2026-08-31 en cómo se declara** —se señala una carpeta en vez de teclear un número—;
ver [Enmienda 1](#enmienda-1--2026-08-31-la-profundidad-se-señala-no-se-teclea). Lo que decide sigue
en pie y la medición de abajo también.

Se intentó adivinarlo y **se midió que no funciona**. La primera regla candidata —hoja con vídeo, y
el curso es el ancestro a distancia 0 o 1, con las secciones reconocidas por llevar número de
cabecera— se simuló sobre la colección real y devolvió **31 cursos donde hay 12**. Sus cuatro modos
de fallo son todos reales y ninguno es rebuscado:

- secciones llamadas `Lección N`, que no llevan el número en la forma que el patrón esperaba;
- secciones con el número **al final** y no en cabecera, del tipo `nombre-vol-N`;
- carpetas técnicas que intercala el reproductor de una editorial, sin numeración de ninguna clase;
- una carpeta de vídeo cuatro niveles por debajo de la raíz, dentro de una de esas técnicas.

Cada modo de fallo se arregla con un parche al patrón, y ése es exactamente el problema: la regla
sólo sería correcta hasta el próximo curso que alguien descargue. Con la profundidad declarada, en
cambio, la detección es **exacta por construcción**: sobre la colección real, profundidad 2 devuelve
los **12 cursos con vídeo, con sus secciones bien** —8, 6, 14, 6, 2, 2 y 13 secciones, y cinco
planos—, y no hay heurística que mantener.

Debajo del curso, **una subcarpeta con vídeo es una sección** y lo que cuelgue por debajo se aplana
contra ella. Una carpeta de recursos sin un solo vídeo —proyecto, escenas, PDF— **no es sección**, y
eso sale gratis en lugar de pedir una lista de nombres que mantener.

**4. El orden es numérico y no alfabético.** Una política pura extrae el número de cabecera de
`NN -`, `NN-`, `NN.` y `NN_`, y conserva la numeración jerárquica `N.N` como par ordenado en lugar de
destruirla —hoy `1.3 Título` acaba llamándose `1 3 Título`—. Lo que no lleva número de cabecera va al
final en orden alfabético estable, que es lo que ordena bien los esquemas codificados con relleno de
ceros.

**5. Una raíz de cursos no se identifica nunca contra un proveedor remoto.** No es una preferencia,
es una regla con puerta: los cursos no están en un catálogo de cine y consultarlos sería enviar los
nombres de las carpetas de alguien a un tercero a cambio de nada. Su título sale del nombre de la
carpeta y su carátula, si la hay, del disco. Esta regla es además la que apaga el falso positivo del
año medido arriba, porque el título de una lección deja de pasar por el limpiador de nombres de cine.

**6. El progreso es el que ya existe**, y no se añade ninguno nuevo. Reanudar, la lección siguiente y
el estado de visto por umbral ya funcionan y funcionan igual aquí.

### Consecuencias

- **Migración de esquema `0022`**, sobre las 21 actuales. `SqliteBootstrapTests` fija el conteo y los
  nombres a propósito, así que la migración mueve tres afirmaciones suyas.
- **El enum crece**, y todo lo que hace exhaustividad sobre él tiene que decidir qué hace con el
  tercer valor. Un `switch` que hoy cubre dos casos y tiene un `default` es justo donde un tipo nuevo
  se pierde en silencio.
- **Una vista nueva**, que son 49 en lugar de 48: entra en la tabla cerrada de `LeadingActionTests`
  con su acción principal decidida, en `ViewOverflowTests` a 900 px y con su escena en el paseo
  autónomo en el mismo commit.
- **Todo archivo nuevo llega a 96/96**, porque la puerta de cobertura lo exige a lo que es nuevo
  contra `main` y no admite entrar en la lista de deuda.
- El trabajo se hace por tramos, y **cada tramo cuesta su vuelta de CI**.

### Alternativas consideradas y rechazadas

**Mapear los cursos a series sin tipo nuevo.** Es mucho más barato: el parser aprende la forma, la
agrupación existente hace el resto, y no hay migración, ni vista, ni enum que crezca. Se rechaza
porque un curso pasaría a llamarse serie en toda la aplicación —en la biblioteca, en los filtros y en
la ficha— y porque la regla de no salir a la red quedaría colgando de una heurística de nombres en
lugar de de un tipo. El Product Owner decidió el tipo propio el 2026-08-30.

**Detectar cursos por heurística sobre los nombres.** Rechazada por lo dicho en la decisión 2: el
tipo decide si algo sale a la red, y una heurística se equivoca en los dos sentidos.

**Fijar la profundidad de la carpeta de curso.** Rechazada por medición, no por gusto: las dos raíces
reales tienen profundidades distintas y ninguna constante sirve para las dos.

**Añadir progreso de formación** —porcentaje de curso, rachas, certificados—. Rechazada: es
exactamente lo que el no-objetivo protege, y nada de eso hace falta para ver un curso que ya está en
el disco.

### Enmienda 1 — 2026-08-31: la profundidad se señala, no se teclea

**Qué cambia:** la decisión 3 se mantiene entera en lo que decide —el programa no adivina el nivel—
y cambia **cómo lo recibe**. Ya no se declara escribiendo un número: **la persona señala una carpeta
de curso y el nivel se deriva de ese gesto**. Después, la aplicación dice cuántas carpetas hermanas
ha encontrado a ese mismo nivel y ofrece marcarlas también, y esa respuesta también es suya.

La decisión 2 **no se toca**: la señal sigue siendo del usuario y no del programa. Esto sólo cambia
la forma de la señal.

**Por qué**, y es del propietario: se le preguntó cómo debía declararse la profundidad y contestó que
lo sano era que **la carpeta que se mete sea la del curso**, una por curso. Tenía razón por una razón
que la decisión 3 ya había medido sin nombrarla así: **no hay forma de distinguir una carpeta
categoría de una carpeta curso**, porque una categoría contiene sólo carpetas y un curso con módulos
también. Señalar una carpeta resuelve eso sin regla ninguna.

**Qué se conserva de lo medido:** todo. La detección sigue siendo **exacta por construcción** —el
nivel derivado es el mismo número que se tecleaba—, y sobre la colección real sigue devolviendo los
12 cursos con sus secciones. Lo que desaparece es la pregunta con un número dentro, que obligaba a
contar carpetas mentalmente.

**Y una alternativa que se descartó por medición, no por gusto:** dejar que el diálogo del sistema
seleccione varias carpetas de golpe. `OpenFolderPickerAsync` devuelve una lista y admite
`AllowMultiple`, así que **la API lo permite**; lo que **no está medido** es que el diálogo nativo de
Windows deje marcar varias carpetas a la vez. Ofrecer las hermanas después de señalar una da el
mismo resultado sin depender de eso, y además deja decir que no.

**Lo que queda pendiente y es del propietario:** la cadena que hace la pregunta de las hermanas, con
su `{0}`, en los dos idiomas. Las cinco cadenas del diálogo que ya existen encajan sin cambios,
porque siempre dijeron «marcar **una carpeta** como curso».

---

## English

### Context

The library this application serves holds courses and video tutorials: folders of numbered videos
watched in order. They are local files like any other, and cataloguing and playing them is exactly
what this project exists to do.

**This ADR was not written against invented examples.** A real collection of **595 videos across 15
courses** was measured and the numbers below come from it. A second root was also looked at, which
today holds seven folders and **no files**: no number comes from it, only its **folder shape**, and
that is said because it is exactly what knocks down the fixed-depth rule. No name from that
collection appears here: rule 5 of this repository forbids writing anybody's library into the tree.

**What the parser does with it today.** `MediaNameParser` knows four shapes and none is a course's:
**594 of 595** came back `Unknown`, one came back `Movie` — a false positive — and none came back
`Episode`. The false positive matters more than its size: a lesson whose title contained a
four-digit year was read as a film of that year **and the year was stripped from the title**, leaving
a broken sentence missing the very word that dated it. That no episode false positive exists is the
other side, and it is good news.

**How lessons are really numbered**: 62.5 % as `NN - title` or `NN-title`, 17.8 % as `NN. title`,
0.5 % as `NN_title`, and 19.2 % with no leading number. That last group **is not unnumbered
material**, and assuming so would have been the mistake: it carries the numbering elsewhere — encoded
schemes of the `XX_NNN_SS_LL` kind, with zero padding, and numbering in parentheses mid-name. The
zero padding has a useful consequence: for those, alphabetical order **agrees** with numeric order.

**A course is not a folder of videos.** Of the **1955 files** measured, only **595 are video**. The
rest — over 69 % — are image sequences, 3D and compositing scenes, PDFs and ZIPs: the course's working
material. A course folder is mostly project, and the application has to catalogue the video and
ignore the rest without tripping over it.

**Depth is not fixed.** One root is `root / category / course / [section] / lesson`; the other is
`root / course / section / lesson`, with no category at all. **Any fixed-depth rule would have been
right about one and wrong about the other.** That was this ADR's first design, and looking at the
disk knocked it down. The second root contributes no videos and contributes this, which is why it
was worth looking at.

**Almost everything else a course needs is already built**: playback speed with its limits and its
persistent preference, resume, the next lesson with its countdown, the watched threshold, subtitles
and audio tracks. Show → season → episode is isomorphic to course → section → lesson.

### The non-goal this touches, and how far

The roadmap says, in both languages: **"Not a course platform. No lessons, no training progress, no
certificates."** That sentence is **narrowed, not deleted**. What stays out is the part that
motivated it: no enrolments, no certificates, no quizzes, no streaks, no study statistics, no
percentage of training completed, and nothing that talks to a platform.

### Decision

1. **`CatalogTitleKind.Course` is a third kind of title**, beside `Movie` and `Show`.
2. **A library root is declared to hold courses, and detection does not guess the kind.** Because the
   kind decides whether something leaves for the network, a wrong classification is not cosmetic.
3. **Each course root declares the depth its courses sit at, and the program does not guess it.**
   **Amended on 2026-08-31 in how it is declared** — a folder is pointed at instead of a number
   typed; see [Amendment 1](#amendment-1--2026-08-31-the-depth-is-pointed-at-not-typed). What it
   decides still stands, and so does the measurement below.
   Guessing was tried and **measured not to work**: the first candidate rule — video leaves, with the
   course as the ancestor at distance 0 or 1 and sections recognised by a leading number — was
   simulated over the real collection and returned **31 courses where there are 12**. Its four
   failure modes are all real: sections named `Lección N`, sections numbered at the **end** rather
   than the head (`name-vol-N`), technical folders a publisher's player interleaves with no numbering
   at all, and a video folder four levels below the root inside one of those. Each is fixable with a
   patch to the pattern, and that is the problem: the rule would be correct until the next course
   somebody downloads. With declared depth the detection is **exact by construction** — depth 2
   returns the **12 courses that hold video, with their sections right**. Below the course, a
   subfolder holding video is a section and anything beneath flattens against it; a resource folder
   with no video is not a section, and that comes free.
4. **Ordering is numeric, not alphabetical.** A pure policy reads the leading number of `NN -`,
   `NN-`, `NN.` and `NN_`, and keeps hierarchical `N.N` as an ordered pair instead of destroying it —
   today `1.3 Title` ends up named `1 3 Title`. Anything without a leading number sorts last,
   alphabetically and stably, which is what orders the zero-padded encoded schemes correctly.
5. **A course root is never identified against a remote provider.** A gated rule, not a preference.
   It is also what switches off the year false positive measured above.
6. **Progress is the progress that already exists**, and no new kind is added.

### Consequences

Schema migration `0022` on top of the current 21, which moves three assertions in
`SqliteBootstrapTests`; a wider enum, where anything exhaustive over it must decide what the third
value means; one new view, making 49 rather than 48, with its row in `LeadingActionTests`, its check
in `ViewOverflowTests` and its walk scene in the same commit; and 96/96 on every new file. The work
goes in tranches, and each tranche costs its own CI run.

### Alternatives considered and rejected

**Mapping courses onto shows with no new kind** — cheaper, rejected because a course would be called
a show throughout the application and the no-network rule would hang off a name heuristic rather than
a kind. The Product Owner chose the dedicated kind on 2026-08-30.

**Detecting courses by heuristic** — rejected for the reason in decision 2.

**Fixing the depth of the course folder** — rejected by measurement, not by taste: the two real roots
have different depths and no constant serves both.

**Adding training progress** — rejected: it is exactly what the non-goal protects.

### Amendment 1 — 2026-08-31: the depth is pointed at, not typed

**What changes:** decision 3 stands entire in what it decides — the program does not guess the level
— and changes **how it receives it**. It is no longer declared by typing a number: **a person points
at one course folder and the level is derived from that gesture**. The application then says how many
sibling folders it found at that same level and offers to mark those too, and that answer is theirs
as well.

Decision 2 **is untouched**: the signal is still the user's rather than the program's. Only the shape
of the signal changes.

**Why**, and it is the owner's: asked how the depth should be declared, he answered that the sane
thing was for **the folder you hand over to be the course's own**, one per course. He was right for a
reason decision 3 had already measured without naming it that way: **there is no way to tell a
category folder from a course folder**, because a category holds only folders and a course with
modules does too. Pointing at a folder settles it with no rule at all.

**What survives of the measurement:** all of it. Detection is still **exact by construction** — the
derived level is the same number that used to be typed — and over the real collection it still
returns the 12 courses with their sections. What goes away is the question with a number in it, which
made somebody count folders in their head.

**And one alternative rejected by measurement rather than taste:** letting the system dialog select
several folders at once. `OpenFolderPickerAsync` returns a list and honours `AllowMultiple`, so **the
API allows it**; what is **not measured** is that the native Windows dialog lets several folders be
marked at a time. Offering the siblings after one is pointed at reaches the same place without
depending on that, and it also allows saying no.

**Left open and owned by the Product Owner:** the string that asks the sibling question, with its
`{0}`, in both languages. The five dialog strings that already exist fit unchanged, because they
always said "mark **a folder** as a course".
