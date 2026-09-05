# El prototipo entrega sus vistas en claro, y otras cuatro cosas que el arnés callaba / The Prototype Hands Over Its Views in Light, and Four Other Things the Harness Was Not Saying

- IDs: `PRD-006`
- Fecha / Date: 2026-09-05
- Alcance / Scope: el arnés de captura (fuera del árbol), `design/vistas/`,
  `Presentation/Theme/DesignTokens`, `Presentation/Shell/ShellView`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Por qué existe este documento

`PRD-006` sube cuando la comparación cubra las sesenta pantallas, y la anterior cubrió ocho. Al
empezar a cubrir el resto, el aparejo contestó cinco veces con algo que no era la respuesta pedida —
y en tres de esas cinco **dijo que todo iba bien**. Se archivan aquí porque cada una habría
convertido la comparación en una lista de defectos inventados.

### 1. Las vistas del prototipo no se fotografían por `file://`, y no lo dicen

`design/vistas/` tiene un archivo por vista, y **eso es lo que hace posible comparar vista a vista**
en vez de sólo las ocho pantallas que tienen ruta. Cada archivo son unos 490 bytes: un `<dc-import>`
que `support.js` resuelve leyendo el prototipo vecino.

Bajo `file://` esa lectura está bloqueada, y Chrome sin cabeza **escribe igualmente un PNG**:

| Vía | Bytes | Qué se ve |
| --- | --- | --- |
| `file:///…/vistas/LibraryView.dc.html` | **6.756** | en blanco |
| lo mismo con `--allow-file-access-from-files` | **6.756** | en blanco |
| `http://127.0.0.1:8791/vistas/LibraryView.dc.html` | **511.804** | la Biblioteca dibujada |

Los 6.756 son **el mismo número exacto para cualquier vista**, que es la única señal que hay: no hay
error, ni código de salida, ni aviso. Un lote de cincuenta y siete habría producido cincuenta y
siete archivos y ninguna pantalla.

Servido por un servidor estático local sobre `design/`, **las 57 dibujaron y ninguna quedó en
blanco**. El guion levanta su propio servidor, comprueba que contesta antes de disparar, y lo apaga
al terminar.

### 2. Cincuenta y siete archivos de vista son cuarenta y dos pantallas

Comparando las 57 capturas por huella, **diez grupos comparten pantalla**: las seis de Inicio
—`HomeView` y sus cuatro carriles más `ShellView`—, las tres de Biblioteca, y siete parejas más
(reproductor y transporte, mini reproductor y su cromo, bandeja y su tarjeta, carpetas y escaneo,
versiones y su diálogo, ciclo de vida y privacidad, marcadores detectados y su editor, duplicados y
su revisión).

**Una matriz que prometiera 57 referencias distintas prometería lo que no hay.** Son 42.

### 3. El prototipo entrega sus vistas en tema CLARO

Medido el píxel del fondo de página en las 57:

| Tema | Vistas |
| --- | --- |
| Claro (L > 70 %) | **41** |
| Oscuro (L < 30 %) | **14** |
| Intermedio | 2 |

Las catorce oscuras son las del reproductor, y eso es correcto en los dos lados: el prototipo y la
aplicación **ignoran a propósito el tema elegido ahí**, porque una sala a oscuras no quiere una
interfaz blanca. Las dos intermedias son diálogos sobre un fondo atenuado.

Fondo medido: prototipo **#FBFCFE** (L = 99 %), aplicación en oscuro **#08090C** (L = 4 %).

### 4. La alarma falsa que eso produjo, y cuánto habría costado

Comparando la aplicación en **oscuro** contra el prototipo en **claro**, lo primero que salta es que
el botón «Continuar» de una tarjeta de carril es **azul** en el prototipo y **blanco** en la
aplicación. Medido:

| Dónde | Fondo del botón |
| --- | --- |
| Prototipo, tarjeta de carril | **#1769AA** |
| Aplicación, tarjeta de carril | **#F3F6FA** |
| `PrimaryActionBrush`, diccionario claro | #1769AA |
| `PrimaryActionBrush`, diccionario oscuro | #F3F6FA |

Los dos valores son **los dos correctos**, cada uno en su tema. No había defecto: había dos temas
distintos puestos uno al lado del otro.

**Y la trampa tenía un segundo piso.** El botón del héroe de Inicio sí coincide —píldora clara en
los dos—, así que la diferencia parecía selectiva y por tanto real. Lo que la cerró fue medir el
**fondo de la página**, no el botón.

**La lección: el tema de una captura se mide, no se mira.** Este árbol ya sabía que una captura de
1500 × 1000 se lee oscura y la misma a 750 × 500 se lee como es; lo nuevo es el caso contrario, una
captura clara leída como oscura porque su mitad superior es una fotografía.

### 5. El arnés sólo sabía pulsar botones, y su excepción dejaba la aplicación abierta

Las ocho primeras escenas salieron; **las catorce siguientes dieron «SIN CAPTURA»**, y todas
empezaban entrando en Ajustes. La causa, reproducida con una sola:

> `Exception calling "GetCurrentPattern" with "1" argument(s): "Modelo no admitido."`

El índice de Ajustes son elementos de **selección**, no botones, y el arnés pedía `InvokePattern` a
todo. **Es el mismo defecto que `?press=` tenía con las casillas del panel «Demostración»**, que se
corrigió el mismo día por la otra punta: un ayudante que sólo sabe pulsar botones no encuentra lo
que no lo es.

**Y el daño real no fue el fallo, sino lo que dejó detrás.** La excepción abortaba el guion antes de
su línea de cierre, así que la aplicación **se quedaba abierta**: se acumularon **siete**, y a partir
de la tercera las siguientes ni arrancaban, porque competían por la misma raíz de datos. Un fallo se
convirtió en catorce.

Corregido en las dos direcciones: se intentan `Invoke`, `Select` y `Expand` por ese orden y se dice
cuál no admite ninguno; y la aplicación se cierra aunque el guion aborte. Medido después sobre el
caso exacto que fallaba: Ajustes → Apariencia capturó 1500 × 1000, con la sección seleccionada, y no
quedó ninguna instancia viva.

### 6. Y un guion mío cantó verde sobre cero capturas

Invocado con `pwsh -File guion.ps1 -Only A,B`, PowerShell **no parte la lista**: entrega un array de
un elemento, la cadena `"A,B"`. El filtro descartó las veinte escenas, el bucle no corrió una sola
vez, y el resumen dijo **«todas capturadas y ninguna sospechosa»** sobre un directorio vacío.

Es el defecto de esta casa dentro de la herramienta que venía a medirla. Corregido con la guarda que
faltaba —un filtro que no selecciona nada es un error, no un éxito— y con un contador de escenas
intentadas que se niega a imprimir un resumen si vale cero. Comprobado en las dos direcciones: un
nombre inventado falla nombrando las escenas válidas, y dos nombres reales capturan dos archivos.

### Las dos primeras parejas, ya con el aparejo fiable

Comparadas **en el mismo tema, el claro**, que es el que el prototipo entrega. Ninguna de estas
diferencias se corrige aquí: por decisión del propietario del 2026-09-05, lo que el prototipo dibuja
y la aplicación no tiene **se registra y no se construye** dentro de la comparación.

**Inicio**

| Qué | Prototipo | Aplicación | Veredicto |
| --- | --- | --- | --- |
| Primario del héroe | píldora clara sobre la fotografía | acento (#1769AA), el del tema | **registrada** |
| Secundario del héroe | contorno transparente | relleno claro sólido | **registrada** |
| Reiniciar en el héroe | no existe | botón circular | cesión ya escrita, extendida |
| Encabezado del carril | «Continuar viendo» | «En curso» | **registrada** |

Las dos primeras son la misma decisión vista dos veces: **el prototipo trata el héroe como una
superficie sobre imagen** y le da botones invertidos, mientras la aplicación le aplica el tema. En
oscuro las dos pintan el primario claro, así que la diferencia sólo existe en claro. El botón de
reiniciar no es un hallazgo: es la cesión ya escrita para la ficha de película —«reanudar y empezar
de nuevo son dos cosas distintas»— aplicada al héroe, y se anota aquí para que la próxima
comparación no vuelva a levantarla.

**Biblioteca**, medida contando los bordes de las tarjetas en una fila de píxeles lisa:

| Medida | Prototipo | Aplicación |
| --- | --- | --- |
| Ancho de tarjeta | **154 px** | **146 px** |
| Hueco entre tarjetas | 20 px | 20 px |
| Donde empieza el contenido | x = 98 | x = 130 |
| Tarjetas por fila | 8 | 7 |

**El hueco coincide y el ancho no**, y las dos cosas juntas explican la columna que falta: ocho
píxeles menos de tarjeta se recuperarían, pero treinta y dos píxeles más de margen izquierdo no. La
columna perdida es del margen, no del tamaño de la ficha.

Más tres diferencias de texto en la misma pantalla —el marcador de posición del buscador, la
etiqueta del desplegable de filtro, y el orden con el que la rejilla nace—, que se registran para
mirarlas con el resto de las cadenas y no una a una.

### Lo que se corrigió de paso en el árbol

- **El comentario del riel decía «cinco nombres» y son seis** desde que llegó Cursos. Ya no cuenta:
  nombra la regla que la prueba de rutas hace cumplir, que es lo que no caduca.
- **La razón del distintivo rojo estaba tres estilos por encima de él**, con el bloque entero de los
  avisos en medio, de modo que quien leyera ese bloque habría encontrado encima una justificación
  que no era la suya. Ahora el estilo va pegado a su razón.

---

## English

### Why this document exists

`PRD-006` goes up when the comparison covers the sixty screens, and the previous one covered eight.
On starting to cover the rest, the rig answered five times with something that was not the answer
asked for — and in three of those five it **said everything was fine**. They are archived here
because each one would have turned the comparison into a list of invented defects.

### 1. The prototype's views do not photograph over `file://`, and they do not say so

`design/vistas/` has one file per view, and **that is what makes a view-by-view comparison possible**
rather than only the eight screens that have a route. Each file is about 490 bytes: a `<dc-import>`
that `support.js` resolves by reading the neighbouring prototype.

Under `file://` that read is blocked, and headless Chrome **writes a PNG anyway**:

| Route | Bytes | What is seen |
| --- | --- | --- |
| `file:///…/vistas/LibraryView.dc.html` | **6,756** | blank |
| the same with `--allow-file-access-from-files` | **6,756** | blank |
| `http://127.0.0.1:8791/vistas/LibraryView.dc.html` | **511,804** | the Library, drawn |

The 6,756 is **the same exact number for any view**, which is the only signal there is: no error, no
exit code, no warning. A batch of fifty-seven would have produced fifty-seven files and no screens.

Served by a local static server over `design/`, **all 57 drew and none came out blank**. The script
starts its own server, checks that it answers before firing anything, and shuts it down at the end.

### 2. Fifty-seven view files are forty-two screens

Comparing the 57 captures by hash, **ten groups share a screen**: Home's six — `HomeView` and its
four rails plus `ShellView` — the Library's three, and seven more pairs (player and transport, mini
player and its chrome, inbox and its card, roots and scanning, versions and its dialog, lifecycle and
privacy, detected markers and its editor, duplicates and its review).

**A matrix promising 57 distinct references would promise what is not there.** There are 42.

### 3. The prototype hands over its views in the LIGHT theme

Measuring the page-background pixel across the 57:

| Theme | Views |
| --- | --- |
| Light (L > 70 %) | **41** |
| Dark (L < 30 %) | **14** |
| In between | 2 |

The fourteen dark ones are the player's, and that is correct on both sides: the prototype and the
application **deliberately ignore the chosen theme there**, because a darkened room does not want a
white interface. The two in between are dialogs over a dimmed backdrop.

Measured background: prototype **#FBFCFE** (L = 99 %), application in dark **#08090C** (L = 4 %).

### 4. The false alarm that produced, and what it would have cost

Comparing the application in **dark** against the prototype in **light**, the first thing that jumps
out is that a rail card's «Continuar» button is **blue** in the prototype and **white** in the
application. Measured:

| Where | Button fill |
| --- | --- |
| Prototype, rail card | **#1769AA** |
| Application, rail card | **#F3F6FA** |
| `PrimaryActionBrush`, light dictionary | #1769AA |
| `PrimaryActionBrush`, dark dictionary | #F3F6FA |

Both values are **both correct**, each in its own theme. There was no defect: there were two
different themes placed side by side.

**And the trap had a second floor.** Home's hero button does match — a light pill on both sides — so
the difference looked selective and therefore real. What closed it was measuring the **page
background**, not the button.

**The lesson: a capture's theme is measured, not looked at.** This tree already knew that a
1500 × 1000 capture reads dark and the same one at 750 × 500 reads as it is; what is new is the
opposite case, a light capture read as dark because its upper half is a photograph.

### 5. The rig only knew how to press buttons, and its exception left the application open

The first eight scenes came out; **the next fourteen gave "SIN CAPTURA"**, and all of them started by
entering Settings. The cause, reproduced with a single one:

> `Exception calling "GetCurrentPattern" with "1" argument(s): "Modelo no admitido."`

Settings' index is made of **selection** elements, not buttons, and the rig asked everything for
`InvokePattern`. **It is the same defect `?press=` had with the «Demostración» panel's radios**,
fixed the same day from the other end: a helper that only knows how to press buttons does not find
what is not one.

**And the real damage was not the failure but what it left behind.** The exception aborted the script
before its closing line, so the application **stayed open**: **seven** piled up, and from the third
on the next ones would not even start, because they competed for the same data root. One failure
became fourteen.

Fixed in both directions: `Invoke`, `Select` and `Expand` are tried in that order and the one that
admits none is named; and the application closes even when the script aborts. Measured afterwards
against the exact failing case: Settings → Appearance captured at 1500 × 1000, with the section
selected, and no instance was left alive.

### 6. And a script of mine reported green over zero captures

Invoked as `pwsh -File script.ps1 -Only A,B`, PowerShell **does not split the list**: it hands over a
one-element array, the string `"A,B"`. The filter discarded all twenty scenes, the loop never ran
once, and the summary said **"all captured and none suspicious"** over an empty directory.

It is this house's own defect inside the tool that came to measure it. Fixed with the guard that was
missing — a filter that selects nothing is an error, not a success — and with a counter of attempted
scenes that refuses to print a summary when it is zero. Checked in both directions: an invented name
fails while naming the valid scenes, and two real names capture two files.

### The first two pairs, now with a rig that can be trusted

Compared **in the same theme, the light one**, which is what the prototype hands over. None of these
differences is corrected here: by the owner's decision of 2026-09-05, what the prototype draws and
the application does not have **is recorded and not built** inside the comparison.

**Home**

| What | Prototype | Application | Verdict |
| --- | --- | --- | --- |
| Hero's primary | light pill over the photograph | accent (#1769AA), the theme's | **recorded** |
| Hero's secondary | transparent outline | solid light fill | **recorded** |
| Restart in the hero | does not exist | circular button | concession already written, extended |
| Rail heading | «Continuar viendo» | «En curso» | **recorded** |

The first two are one decision seen twice: **the prototype treats the hero as a surface over an
image** and gives it inverted buttons, while the application applies the theme to it. In dark both
paint the primary light, so the difference exists only in light. The restart button is not a
finding: it is the concession already written for the film card — «resuming and starting again are
different things» — applied to the hero, and it is noted here so the next comparison does not raise
it again.

**Library**, measured by counting card edges along a smooth row of pixels:

| Measure | Prototype | Application |
| --- | --- | --- |
| Card width | **154 px** | **146 px** |
| Gap between cards | 20 px | 20 px |
| Where the content starts | x = 98 | x = 130 |
| Cards per row | 8 | 7 |

**The gap matches and the width does not**, and the two together explain the missing column: eight
pixels of card would be recovered, thirty-two pixels of left margin would not. The lost column
belongs to the margin, not to the size of the card.

Plus three text differences on the same screen — the search placeholder, the filter dropdown's
label, and the order the grid is born with — recorded to be looked at with the rest of the strings
rather than one at a time.

### Fixed along the way in the tree

- **The rail's comment said "five names" and there are six** since Cursos arrived. It no longer
  counts: it names the rule the route test actually enforces, which is what does not rot.
- **The red badge's reason sat three styles above it**, with the whole notices block in between, so
  whoever read that block would have found a justification on top of it that was not its own. The
  style now sits against its reason.
