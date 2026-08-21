# Las filas de acciones dejan de poder salirse, y una vista nueva paga su entrada en el trinquete / Rows of actions stop being able to run off, and a new view pays its way into the ratchet

Segundo trabajo del tramo 3 de la §4, y la primera vez que **añadir una superficie** choca con el
trinquete de cobertura. Las dos mitades están aquí porque llegaron juntas. / §4's third tranche
continues, and adding a surface meets the coverage ratchet for the first time.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Las filas que se envuelven / The rows that wrap

La §4 pide `WrapPanel` por su nombre en la fila de filtros de la biblioteca y en las de acciones de
las dos fichas. La forma que sustituyen —botones con palabras traducidas en fila fija— es la que ha
dibujado un control **fuera de la ventana siete veces** en este repositorio. / The shape they replace
has drawn a control off the side seven times here.

```
Every_decided_row_of_actions_is_a_wrap_panel
  ["LibraryFilterSurface is not declared in Library/LibraryView.axaml any more.",
   "MovieActionsSurface is not declared in Movie/MovieDetailsView.axaml any more.",
   "ShowActionsSurface is not declared in Show/ShowDetailsView.axaml any more."]
```

- **`LibraryView`**: la fila era un `Grid` de cuatro columnas fijas, así que el botón de aplicar salía
  por el lado en vez de bajar. La caja de búsqueda gana un ancho porque **un `WrapPanel` da a sus hijos
  lo que piden**, y un `TextBox` pide casi nada. / A Grid of fixed columns pushes the last one out.
- **`MovieDetailsView`**: cuatro botones y un texto en fila. **`ShowDetailsView`**: las acciones
  personales y el enlace del tráiler.
- **La puerta es una tabla cerrada de las superficies decididas**, y absorbe la que el tramo 1 dejó
  suelta en `ShellNavigationBarTests`: dos mecanismos para la misma regla envejecen distinto. Lee el
  **marcado** y no una pantalla montada a propósito: `ViewOverflowTests` ya mide anchura, pero la mide
  en un idioma y a una escala, y **una fila que hoy cabe en español no es una fila que se envuelva**. /
  What is asserted is the panel that *can* wrap, which survives a longer translation.

## La puerta, probada fallando / The gate, proved by failing

Mutando `MovieActionsSurface` a `StackPanel` y revirtiendo. Y la primera mutación enseñó algo que no
se sabía: **el compilador de Avalonia ya rechaza media degradación**, porque `ItemSpacing` y
`LineSpacing` no existen en un `StackPanel`:

```
AVLN2000: Unable to resolve suitable regular or attached property ItemSpacing on type StackPanel
```

Así que la puerta **no es redundante y tampoco es la única red**: cubre exactamente el caso que el
compilador no ve, que es alguien escribiendo un `StackPanel` con `Orientation` y `Spacing` desde cero.
Con esa mutación —la que sí compila— la puerta dice lo que tiene que decir:

```
MovieActionsSurface is a StackPanel, not a WrapPanel.
```

/ The compiler refuses half the degradation on its own; the gate covers the half it cannot see.

## El peaje del trinquete, que es la parte que no se veía venir / The ratchet toll

El run `32367074466` dio **todas las suites en verde** y falló en la puerta de cobertura, por dos
razones a la vez:

```
Coverage gate: 217 file(s) still short of 96/96, ratchet 217, 3 improved.
  GetHome.cs now reaches 97/91; raise its floor…
  HomeViewModel.cs now reaches 99/81; raise its floor…
  RecommendationsViewModel.cs now reaches 93/85; raise its floor…
Coverage gate: wrote 218 file(s) to eng/coverage-debt.txt.
```

Las tres mejoras son reales y del cambio: el carril nuevo hace que `RecentlyAddedItemViewModel` se
ejecute por primera vez, y `IsEmpty` llegó con su prueba. **Lo que no se veía venir es el 218**:

**Todos los `.axaml` de vista de este árbol miden 100/50.** Es el código que el generador de Avalonia
escribe para el marcado, y su rama que nadie ejerce; `HomeView`, `InProgressRailView`,
`LibraryEntryView`, `RecommendationsRailView`, `ResumeHeroView`, `MiniPlayerChromeView` — todos, sin
excepción. Así que **una vista nueva añade siempre una línea a la deuda**, y la lista **sólo puede
encoger**. / A new view always adds one line to the debt, and the list may only shrink.

**La salida no es subir el trinquete: es pagar la entrada.** El trinquete ha ido 219 → 218 → 217 y
nunca ha subido; subirlo porque hemos añadido superficie convertiría la regla en una que se relaja
sola. Así que se saca un archivo **mejorándolo de verdad**, que es la única forma de salir de esa
lista. / The way in is to pay, not to raise the bar.

## Cuál, y por qué ése / Which file, and why

Elegido leyendo el informe **línea a línea**, no adivinando: `CandidateScorer.cs` estaba a **95,45 %
de ramas (21/22)** con las dos que faltaban en las líneas 81 y 86, que son
`parsed.Episode.HasValue && facts.EpisodeMatch.HasValue` y su gemela de temporada. La mitad que nunca
se ejercía es **el nombre trae el dato y el proveedor no lo contesta**, que es la respuesta ordinaria
de una fuente que sólo conoce la serie. / Read from the report, not guessed.

La prueba cubre las tres condiciones opcionales —temporada, episodio y año— y afirma lo que la
política garantiza: la señal **se descarta** y su peso se renormaliza sobre el título, así que un
proveedor callado puntúa igual que uno al que no se preguntó. Medido después: **44/44 ramas, 100 %**.
El archivo sale de la lista, que vuelve a **217**. / The silent provider scores the same as one never
asked.

## Cómo queda `eng/coverage-debt.txt` / How the debt file ends up

Copiado **entero** del artefacto `coverage-debt` del run que lo midió, con **una** línea retirada: la
de `CandidateScorer.cs`, porque un archivo que mejora **sale de la lista en el mismo cambio** y ésa es
la única edición que la puerta admite. Los cuatro números que cambian son los que midió CI, no esta
máquina. / Copied whole from the run's artefact, with the improved file's line removed.

```
- GetHome.cs                    96 91   →  + 97 91
- HomeViewModel.cs              98 81   →  + 99 81
- RecommendationsViewModel.cs   93 83   →  + 93 85
- CandidateScorer.cs           100 95   →  (fuera: 100/100 / out of the list)
+ RecentlyAddedRailView.axaml  100 50
```

## El verde / The green

```
UiTests             626/626
Domain.Tests        466/466
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 134 declaraciones en 133 identidades; 133 pulsadas, 0 pendientes
CandidateScorer.cs  44/44 ramas, 100 % de líneas / 44/44 branches
```
