# La carpeta que se señala / The folder that gets pointed at

Evidencia de **CRS-001**: la opción «Curso (carpeta de lecciones)» del diálogo de añadir, que es lo
único que permite declarar un curso. /
Evidence for **CRS-001**: the add dialog's «Course (folder of lessons)» option, which is the only
thing that lets a course be declared at all.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-31. Decisión / Decision:
[`ADR-0006`](../../adr/0006-courses-are-a-third-kind-of-title.md) y su
[enmienda 1 / and its amendment 1](../../adr/0006-courses-are-a-third-kind-of-title.md#enmienda-1--2026-08-31-la-profundidad-se-señala-no-se-teclea).

## El nivel se deriva del gesto / The level is derived from the gesture

`CourseRootDeclarationPolicy.Derive` contesta dos preguntas de una vez porque son una: **qué raíz** y
**qué profundidad**. Ocho pruebas puras en `Domain.Tests` fijan las dos lecturas y los tres rechazos.
/ It answers two questions at once because they are one question; eight pure tests fix both readings
and all three refusals.

| se señala / pointed at | raíces catalogadas / catalogued roots | raíz / root | nivel / level |
| --- | --- | --- | --- |
| `D:\Cursos\3D\Composición` | `D:\Cursos` | `D:\Cursos` | **2** |
| `D:\Cursos\Composición` | `D:\Cursos` | `D:\Cursos` | **1** |
| `D:\Cursos\3D\Composición` | ninguna / none | `D:\Cursos\3D` | **1** |
| `D:\Cursos` | `D:\Cursos` | — | rechazado / refused |
| `D:\Composición` | ninguna / none | — | rechazado / refused |
| vacío / empty | cualquiera / any | — | rechazado / refused |

Las dos formas de raíz que el ADR midió salen **1** y **2** del gesto solo, que es exactamente el
número que antes se tecleaba. Los tres rechazos no son ramas defensivas: son cosas que una persona
puede señalar de verdad. Una raíz no es un curso dentro de sí misma —profundidad 0 no es una
profundidad, y tomarla por una convertiría en curso a cada hija suya sin que nadie lo haya dicho—, y
una carpeta colgada de la unidad haría raíz del volumen entero, que es justo lo que la ayuda del
diálogo promete no hacer. / The two root shapes the ADR measured come out as 1 and 2 from the gesture
alone, which is the number that used to be typed. None of the three refusals is a defensive branch.

**Las barras se comparan canonizadas.** Una ruta pegada con `/` tiene que encontrar la raíz que el
catálogo escribió con `\`; compararlas como llegan simplemente no casa nunca. Y una raíz que sólo
comparte el prefijo —`D:\Cursos2` frente a `D:\Cursos`— **no** la contiene, porque casar por prefijo
metería la lección de una biblioteca en el curso de otra. / Separators are compared canonically, and
a root that merely shares a prefix does not hold the folder.

## Las vecinas se preguntan, no se reclaman / The neighbours are asked about, not claimed

Señalar una carpeta declara la profundidad de **toda** la raíz, y a esa profundidad suele haber
carpetas sobre las que nadie ha dicho nada. `MarkCoursesInRoot` gana un filtro: marca lo que se le
nombra y **devuelve el resto** en vez de quedárselo. / Pointing at one folder declares a depth for the
whole root, so the use case marks what it is named and hands the rest back.

| pasada / pass | marca / marks | devuelve / returns |
| --- | --- | --- |
| se señala `Composición` de tres / one of three | `Composición` | `Modelado`, `Render` |
| «sí, son todas» / "yes, all of them" | las tres / all three | nada / nothing |
| se nombra una que la detección no halló / a folder detection never found | nada / nothing | lo detectado / what was detected |

El filtro se compara contra **lo que la detección encontró** y no se toma como dado: una ruta que
nadie detectó no es un curso que esta pasada pueda marcar, la nombre quien la nombre. / The filter is
compared against what detection found rather than trusted as given.

**El sí es una segunda pasada que relee la raíz**, en lugar de fiarse de lo que contó la primera.
Cuesta un recorrido más de una carpeta delante de la cual hay alguien de pie, y compra una respuesta
que es verdad **cuando se actúa sobre ella** y no cuando se calculó. / Yes is a second pass that
re-reads the root: one extra walk, and an answer that is true when it is acted on.

La frase la escribió el propietario el 2026-08-31 y pregunta por el **hecho**, no por la acción:
«Hemos encontrado {0} carpetas más. ¿Son todas cursos?». Los dos botones son verbos —**«Marcar
todas»** y **«Sólo esta»**— y no un sí y un no, que es la forma que ya tienen las otras dos preguntas
de esta aplicación. Los propuso Engineering como «Marcarlas todas» y **el propietario los revisó ese
mismo día**: se queda la forma sin enclítico, que es la del resto del árbol —«Continuar», «Marcar
como curso», «Quitar la marca»—. / The sentence is the owner's and asks about the fact; both answers
are verbs, and the owner settled their wording the same day on the shorter form the rest of the tree
uses.

## Lo que una puerta encontró y la lectura no / What a gate found and reading did not

`ViewHeightTests` midió el diálogo en **640 px** —su propio `MaxHeight`— contra una ventana cuyo
mínimo son **600**. El fondo del panel era contenido que nadie podía alcanzar, y llevaba ahí desde
antes: lo que cambió es que la mitad de curso hizo que el contenido **pidiera** esa altura. /
`ViewHeightTests` measured the dialog at 640 px against a 600 px minimum window; the course half is
what made the content actually ask for it.

Baja a **560** y su contenido se desplaza **dentro del panel**, no el panel dentro del shell: un hijo
centrado de un `ScrollViewer` deja de estar centrado, y el overlay está centrado a propósito. / It
drops to 560 and scrolls inside itself, because a centred child of a scroller stops being centred.

## Dos cosas que se midieron en vez de suponerse / Two things measured rather than assumed

**El árbol en monoespaciada llega entero.** El esquema se escribe con `&#10;` porque AXAML es XML, y
que eso sobreviva hasta la cadena que pinta un `TextBlock` es justo lo que se ve bien en el marcado y
llega como una sola línea. El MCP de Avalonia **no tiene página de `TextBlock`** —la consulta
contestó «no results», que también es un dato—, así que la respuesta salió de ejecutarlo: **seis
líneas** en los dos idiomas, con sus caracteres de rama en su sitio. / The MCP has no `TextBlock`
page, so this was answered by running it: six lines in both languages.

**La pregunta lleva su número sin `StringFormat`.** El formato de `StringFormat` tiene que ser un
literal, y esta frase tiene que seguir el idioma elegido. La clave viaja como parámetro del converter
y la cuenta como su valor, y se comprueba la frase entera en los dos idiomas. / `StringFormat`'s
format has to be a literal and this sentence has to follow the chosen language, so the key travels as
the converter's parameter.

## Registrado y alimentado, esta vez también / Registered and fed, this time as well

`ICourseRootDeclarationStore` y `MarkCoursesInRoot` estaban **fuera del contenedor a propósito**:
nadie los resolvía, y un servicio que nadie resuelve es el defecto característico de este
repositorio. `ServiceConsumptionTests` lo dijo en voz alta el día que se registraron sin consumidor.
Vuelven ahora con quien los resuelve, `DeclareCourseFolder`. / They were deliberately out of the
container because nothing resolved them; they come back with the consumer they were missing.

**Y la composición tenía una trampa que habría reproducido el mismo defecto en su forma más
callada.** `RootOnboardingViewModel` es transitorio, así que pedirlo una segunda vez para la mitad de
curso habría dado **otra instancia**: pulsar «Añadir carpeta» habría funcionado perfectamente, sobre
una caja de ruta que nadie está mirando. Se resuelve una vez y se entrega a las dos mitades. /
The onboarding model is transient, so a second resolution would have given the course half a
different instance: its Add would work perfectly, on a path box nobody is looking at.

## Lo que el paseo pulsa con el ratón / What the walk presses with a mouse

`The_courses_destination_marks_a_folder_and_carries_on_with_a_course` gana el gesto entero, con una
segunda carpeta de curso al lado para que la pregunta tenga sobre qué preguntar: /
The scene gains the whole gesture, with a second course folder beside the first so the question has
something to ask about:

| control | efecto comprobado / effect asserted |
| --- | --- |
| `AddAsCourseOption` | el diálogo pasa a su mitad de curso y cambia de título |
| la acción del diálogo / the dialog's action | **el curso que devuelve la pasada**, no el número de cursos |
| `AddCourseNeighboursConfirmAction` | la vecina queda marcada: **1 → 2 cursos en el almacén** |
| `AddCourseNeighboursDeclineAction` | la pregunta se va y no se marca nada más |
| `AddAsRootOption` | el diálogo vuelve a su mitad de raíz |

**El trinquete no se mueve**: 242 controles declarados en 237 identidades, **217 pulsados y 20
pendientes**, los mismos veinte de siempre y por la misma razón medida el 2026-08-25. / The ratchet
does not move: 217 pressed and 20 pending, the same twenty and for the same measured reason.

La acción del diálogo se registra por **la expresión de su enlace** y no por una clave, porque lo que
dice sigue a la mitad elegida — el inventario ya conoce así los dos controles que se nombran con sus
propios datos. / The dialog's action is recorded under its binding expression rather than a key.

## Lo que encontró el paseo y ninguna prueba unitaria podía ver / What the walk found and no unit test could see

«Marcar todas» se construye cuando **todavía no hay vecinas**. Contesta que no puede ejecutarse, y
sin nadie que le avise se queda **deshabilitado toda la vida del diálogo**: en pantalla, con el
aspecto exactamente correcto, y sin poder pulsarse. El paseo lo dijo en una línea —
`visible=True, enabled=False`— porque es lo único que aprieta el botón como lo aprieta una persona. /
The neighbours' button is created before there are any neighbours, answers that it cannot execute,
and without being told stays disabled for the dialog's whole life; the walk said so in one line.

Un botón atado a un comando pregunta `CanExecute` **una vez** y luego espera a que le avisen. Leer
`CanExecute` del modelo da la respuesta buena **se haya avisado o no**, así que una prueba escrita
así habría pasado con el defecto puesto. Lo que se afirma es **el evento**: que algo le dijo al botón
que había cambiado. / A button asks `CanExecute` once and then waits to be told; reading it off the
model passes either way, so what is asserted is the event.

**Y el probe de la escena tenía el mismo problema en su otra forma.** El primer intento medía la
pulsación contando los cursos del almacén — y esa carpeta **ya era un curso** en esa escena, así que
marcarla otra vez es un upsert y la cuenta es idéntica antes y después. Un probe que no puede moverse
es una pulsación que no prueba nada. / The first probe counted courses, and that folder was already a
course: a probe that cannot move is a press that proves nothing.

## Una acción y no dos / One action and not two

El diálogo tiene **un** botón acentuado. Dos botones con `primary-action` son una pantalla que no ha
decidido para qué es, y `LeadingActionTests` lo rechaza. Lo que el botón dice y lo que hace siguen a
la píldora elegida, así que se identifica por `x:Name` y no por una clave de recurso, y la tabla
cerrada de esa puerta lo registra como `AddOrMarkAction`. / The dialog has one accented button; what
it says and what it does both follow the chosen pill.

El prototipo ofrece cuatro opciones en un desplegable —automático, película, serie y curso—. Aquí son
**dos píldoras**, porque nada detrás distingue una raíz de películas de una de series: tres opciones
que hacen lo mismo serían tres controles registrados y nunca alimentados. / The prototype offers four
options; two pills are what this tree can actually deliver.
