# El minirreproductor como ventana flotante / The mini player as a floating window

Evidencia de **PLY-007**: el modo mini pasa de ser una ventana normal con barra de título a una
ventana sin marco que se arrastra por la imagen, se redimensiona por sus ocho bordes conservando la
relación **16:9**, y recuerda dónde se dejó entre sesiones. / Evidence for **PLY-007**: the mini mode
goes from an ordinary titled window to a frameless one dragged by the picture, resized from its eight
edges at a fixed **16:9**, and remembered between sessions.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-28.

## Lo que ya estaba y lo que faltaba / What was already there and what was not

El 2026-08-25 el mini dejó de duplicar la barra de transporte, se volvió `Topmost` y ganó geometría
por modo. Lo que quedaba abierto era la ventana en sí. / On 2026-08-25 the mini stopped duplicating
the transport bar, became `Topmost`, and got geometry per mode. The window itself was still open.

## La medición que decidió el diseño / The measurement that decided the design

El primer intento de arrastre dejaba pasar el gesto que otro control ya hubiera atendido —`if
(e.Handled) return;`—, sobre la premisa de que un botón marca su propia pulsación. Se midió con un
`MouseDown` del arnés headless sobre `MiniPlayerPlayPause`, con un manejador de la ventana registrado
con `handledEventsToo: true`: / The first draft of the drag skipped a gesture another control had
already handled, on the premise that a button marks its own press. Measured with a headless
`MouseDown` on `MiniPlayerPlayPause`, with a window handler registered `handledEventsToo: true`:

```
button bounds=0,0,44,44   origin=136,247
seen=1                    handled=0
```

**La premisa era falsa.** Avalonia marca el *soltar*, que es donde está el clic de un botón, así que
la guarda no guardaba nada y **los cinco controles del cromo habrían arrastrado la ventana en vez de
funcionar**. Lo que decide es dónde cae la pulsación, no qué golpeó: la imagen arrastra y la franja
del cromo no. / **The premise was false.** Avalonia marks the *release*, so the guard protected
nothing and all five chrome controls would have dragged the window. What decides is where the press
lands, not what it hit.

La segunda medición, del mismo arnés, decidió dónde vive el filtro de la relación de aspecto: / The
second measurement, from the same harness, decided where the aspect filter lives:

```
resized: 600,270  reason=Layout
```

**Un backend headless nunca levanta un redimensionado de usuario.** Un filtro `e.Reason ==
User` enterrado en el `override` habría dejado toda la corrección detrás de una rama que ninguna
prueba puede tomar, así que la decisión es un método público que toma la razón. / **A headless
backend never raises a user resize.** A filter buried in the override would have left the whole
correction behind a branch no test can take, so the decision is a public method that takes the reason.

## El defecto de la casa, otra vez, y cerrado / The house defect, again, and closed

`PlayerWindowCoordinator.Remember` y `Recall` existían desde el 2026-08-19. Un barrido sobre `src/`
y `tests/`: / `Remember` and `Recall` had existed since 2026-08-19. A sweep over `src/` and `tests/`:

```
llamadas a .Remember(  en src/   0
llamadas a .Remember(  en tests/ 3
llamadas a .Recall(    en src/   0
```

**Registrado y nunca alimentado**: se guardaba en un diccionario que sólo leían sus propias pruebas.
Ahora `ShellView` lo llama al cerrar la ventana, y el coordinador escribe a través de
`IMiniPlayerPlacementStore` la mitad que sobrevive al proceso. / **Registered and never fed**: it
wrote to a dictionary only its own tests read. `ShellView` now calls it when the window closes, and
the coordinator writes the half that outlives the process through `IMiniPlayerPlacementStore`.

## Las tres guardas que se quitaron por no guardar nada / The three guards removed for guarding nothing

Medido con el informe Cobertura de `UiTests`, sobre esta máquina y la misma suite: / Measured from the
Cobertura report of `UiTests`, on this machine and the same suite:

| Archivo / File | Antes / Before | Con las guardas / With the guards | Sin ellas / Without |
| --- | --- | --- | --- |
| `ShellView.axaml.cs` | 97,50 / 67,85 | 96,29 / 65,62 | **98,03 / 69,23** |
| `MiniPlayerWindow.axaml.cs` | 100 / 100 | 80,00 / 80,95 | **98,73 / 97,61** |

Las tres: el `??=` que reutilizaba una ventana que el shell ya había cerrado y anulado, el
`sender is not MiniPlayerWindow` de un manejador enganchado a una sola ventana, y el segundo
`Screens.Primary?.Bounds ?? …` escrito al lado del primero. / The three: the `??=` reusing a window
the shell had already closed and dropped, the `sender is not MiniPlayerWindow` of a handler attached
to exactly one window, and the second `Screens.Primary?.Bounds ?? …` written beside the first.

**`MiniPlayerWindow.axaml.cs` no está en `eng/coverage-debt.txt`**, así que su listón es 96/96 y lo
cumple: CI no lo nombró. `ShellView.axaml.cs` subió por encima de su suelo, y CI lo midió en **98/73**
—no en el 98,03/69,23 de la tabla—, que es el número con el que se movió el suelo. / **The mini
window is not in the debt list**, so its bar is 96/96 and CI did not name it. `ShellView.axaml.cs`
rose above its floor and CI measured it at **98/73**, which is the number the floor moved with.

**Las tres columnas de arriba son lecturas locales por suite, y sirven para comparar entre sí y para
nada más.** La puerta mide con el informe **fusionado**, y sobre esta misma tanda esa diferencia
convirtió un 100/100 local en un 96/70 de CI para otro archivo. Lo que vale de la tabla es la forma
—las guardas muertas bajan un archivo y quitarlas lo suben—, no los decimales. / **The three columns
above are local per-suite readings, good for comparing with each other and nothing else.** The gate
measures from the merged report, and on this same batch that difference turned a local 100/100 into
CI's 96/70 for another file.

## Verde / Green

```
UiTests             995 superadas, 0 con error
AccessibilityTests  146 superadas, 0 con error
IntegrationTests    482 superadas, 0 con error, 1 omitida
ArchitectureTests    30 superadas, 0 con error
dotnet format --verify-no-changes --severity warn   0
dotnet build -c Release -warnaserror                0 errores
eng/verify-docs.ps1   247 Markdown, 34 localizados, 59 IDs
```

## Lo que sigue sin hacerse / What is still not done

El cromo del mini sigue siendo cinco botones en un `WrapPanel`, y el prototipo dibuja además el
título, el tiempo y una barra de progreso de tres píxeles sobre ellos. No entra en esta tanda: es la
composición del cromo, no la ventana. / The mini's chrome is still five buttons in a `WrapPanel`,
while the prototype also draws the title, the time, and a three-pixel progress bar above them. Not in
this batch: that is the chrome's composition, not the window.
