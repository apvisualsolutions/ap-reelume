# Una carpeta de episodios es una serie / A folder of episodes is a series

Evidencia de **LIB-005**: el analizador de nombres leía `S01E01` desde el primer día y **nadie le
preguntaba nunca dónde iba el episodio**. / Evidence for **LIB-005**: the name parser has read
`S01E01` since day one and **nobody ever asked it where the episode belonged**.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-25.

## Español

### El informe

El propietario metió una carpeta con dos series: *Juego de Tronos*, ocho temporadas y más de setenta
y cuatro capítulos, y *La Casa del Dragón*, tres temporadas y más de veinticinco. Lo que vio:

> Ninguna se organiza debidamente. Se muestran todos los capítulos sueltos en la biblioteca.

### La medición previa

Antes de decidir nada, qué había ya. Medido sobre `src/`:

```
Domain          MediaNameParser: S01E01, 1x04, «Temporada 1 Episodio 2», Cap.803       existe
Infrastructure  migración 0004: tablas titles, seasons, episodes                       existe
Infrastructure  migración 0014: tabla episode_media                                    existe
Presentation    ShowDetailsView, EpisodeRowView, SeasonViewModel, el selector          existe
Presentation    LibraryViewModel enruta Kind == Show a la ficha de serie               existe
Application     algo que ESCRIBA una fila en cualquiera de las cuatro tablas           NO EXISTE
```

Cuatro tablas desde la migración 0004, una vista dibujada, una ruta cableada, y **cero filas
escritas por nadie jamás**. El defecto característico de esta casa en su forma más grande: la mitad
de arriba construida y probada, y el conducto que la alimenta sin abrir. LIB-005 figuraba como
`VERIFIED` con la evidencia T11, que mide el analizador — y el analizador funciona. Lo que no había
era quien lo llamara.

### El rojo

Una prueba de integración sobre el árbol real que el informe describe: dos carpetas de serie con sus
carpetas de temporada, más una película en la misma raíz para que la regla no se trague lo que no es
suyo. Noventa y nueve archivos.

```
Antes:  99 archivos → 99 tarjetas en la rejilla
```

### La corrección

Dos piezas y ninguna en la vista:

1. **`LocalSeriesPolicy`** (dominio, pura). Dice qué carpeta nombra la serie. La regla es **la
   carpeta, nunca el archivo**: `D:\Series\Juego de Tronos\Temporada 1\S01E01.mkv` escribe el nombre
   de la serie una sola vez y es en la carpeta; el archivo lleva un número y lo que el codificador
   quisiera añadir. Si la última carpeta dice «Temporada N», la de encima nombra la serie; si no, la
   última. Un episodio suelto en una raíz cae en el título que él mismo lleva. La clave incluye la
   raíz, así que un respaldo no se funde con el original.

2. **`GroupScannedEpisodes`** (aplicación). Corre después de cada escaneo, en el mismo sitio y con la
   misma forma que la agrupación de versiones, y escribe la serie, sus temporadas, sus episodios y el
   archivo detrás de cada uno.

Los identificadores se **derivan** de la clave y de los números: el primer episodio de una serie tiene
que llegar al mismo identificador que el septuagésimo cuarto, en otro escaneo, otro día, sin que
ninguno de los dos haya leído al otro.

Y la unión que la biblioteca consulta gana una segunda cláusula: un archivo ya enlazado a un episodio
no vuelve a salir como tarjeta suelta. Es una cláusula y no un borrado porque `scanned_titles` se
reescribe en cada escaneo — borrar la fila la traería de vuelta a la siguiente pasada.

### El verde, medido

```
Después: 99 archivos → 3 tarjetas
         Juego de Tronos      Serie   72 episodios   8 temporadas
         La Casa del Dragon   Serie   27 episodios   3 temporadas
         El Faro de Piedra    Película
```

Y cada episodio con su archivo detrás: `EpisodeSequenceRepository.GetSeriesAsync` devuelve 72
entradas, todas `IsPlayable`. Un segundo escaneo de la misma carpeta escribe las mismas filas y no
duplica nada.

### Lo que no cambia

Una película sigue siendo una película. Una serie que un proveedor identificó conserva lo que el
proveedor dijo: la fila que escribe la identificación va con la clave del **archivo** y la que se
escribe aquí con la de la **carpeta**, y las dos no pueden coincidir nunca.

### Lo que queda anotado y no corregido

El título de una película sin identificar sigue siendo su nombre de archivo tal cual —«El Faro de
Piedra 2019»—, porque un título escaneado siempre lo fue. Es un defecto distinto de éste y está
**afirmado** en la prueba en vez de corregido, para que cambiarlo sea una decisión de alguien y no una
sorpresa.

## English

### The report

The owner put a folder with two shows on the disk: *Juego de Tronos*, eight seasons and more than
seventy-four episodes, and *La Casa del Dragón*, three seasons and more than twenty-five. What he
saw:

> Neither is organised properly. Every episode shows up loose in the library.

### The measurement first

What was already there, measured over `src/`:

```
Domain          MediaNameParser: S01E01, 1x04, "Temporada 1 Episodio 2", Cap.803       exists
Infrastructure  migration 0004: titles, seasons, episodes tables                       exists
Infrastructure  migration 0014: episode_media table                                    exists
Presentation    ShowDetailsView, EpisodeRowView, SeasonViewModel, the picker           exists
Presentation    LibraryViewModel routes Kind == Show to the series card                exists
Application     anything that WRITES a row into any of the four tables                 DOES NOT
```

Four tables since migration 0004, a view drawn, a route wired, and **zero rows ever written by
anybody**. This repository's characteristic defect in its largest form: the top half built and
tested, and the pipe that feeds it never opened. LIB-005 stood as `VERIFIED` on the T11 evidence,
which measures the parser — and the parser works. What was missing was a caller.

### The red

An integration test over the real tree the report describes: two show folders with their season
folders, plus a film in the same root so the rule does not swallow what is not its own. Ninety-nine
files.

```
Before:  99 files → 99 cards in the grid
```

### The correction

Two pieces, neither of them in the view:

1. **`LocalSeriesPolicy`** (domain, pure). It says which folder names the series. The rule is **the
   folder, never the file**: `D:\Series\Juego de Tronos\Temporada 1\S01E01.mkv` writes the show's
   name exactly once and it is in the folder; the file carries a number and whatever the encoder felt
   like adding. If the last folder says "Temporada N", the one above it names the show; otherwise the
   last one does. An episode loose in a root falls back to the title it carries itself. The key
   includes the root, so a backup does not fold into the original.

2. **`GroupScannedEpisodes`** (application). It runs after every scan, in the same place and with the
   same shape as version grouping, and writes the show, its seasons, its episodes, and the file
   behind each one.

The identifiers are **derived** from the key and the numbers: the first episode of a show has to
arrive at the same identifier as the seventy-fourth, in a different scan, on a different day, without
either of them having read the other.

And the union the library queries gains a second clause: a file already linked to an episode does not
come back as a loose card. A clause and not a delete, because `scanned_titles` is rewritten by every
scan — deleting the row would bring it back on the next pass.

### The green, measured

```
After:  99 files → 3 cards
        Juego de Tronos      Series   72 episodes   8 seasons
        La Casa del Dragon   Series   27 episodes   3 seasons
        El Faro de Piedra    Film
```

And every episode with its file behind it: `EpisodeSequenceRepository.GetSeriesAsync` returns 72
entries, all `IsPlayable`. A second scan of the same folder writes the same rows and duplicates
nothing.

### What does not change

A film is still a film. A show a provider identified keeps what the provider said: the row
identification writes is keyed by the **file** and the one written here by the **folder**, and the two
can never collide.

### What is written down and not corrected

The title of an unidentified film is still its file name verbatim — "El Faro de Piedra 2019" —
because a scanned title always was. That is a different defect from this one, and it is **asserted**
in the test rather than corrected, so changing it is somebody's decision rather than a surprise.
