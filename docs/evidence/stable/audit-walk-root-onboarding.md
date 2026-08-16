# La carpeta que hace una biblioteca, añadida y retirada con el ratón / The folder a library is made of, added and taken back out

Ocho controles, la **primera pantalla que ve cualquiera** —hasta hoy sin pulsar por nadie— y el mismo
defecto de diseño por **cuarta vez en el día**. Además, una premisa de la cola que resultó falsa. /
Eight controls, the first surface anybody ever sees, and the same layout defect for the fourth time in
one day.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 76 | **84** |
| Pendientes / Pending | 52 | **44** |

## La premisa que era falsa / The premise that was wrong

Esta tanda estaba programada **detrás** de la regla de aislamiento, con esta razón escrita: «estrena la
regla en los **selectores de carpeta**». No hay ninguno. La carpeta se **escribe en una caja de
texto**, y los selectores de verdad —`OpenFilePickerAsync` y `SaveFilePickerAsync`— viven en las
pantallas de copia y restauración, que son la tanda 6. / This batch was scheduled behind the isolation
rule because it supposedly opens a folder picker. It does not: the folder is typed into a box, and the
real pickers live on the backup and restore surfaces.

Consecuencia práctica: la tanda 9 **no tenía condición previa ninguna** y podría haberse hecho antes.
La condición sigue siendo real para la **tanda 6**, y allí sigue esperando. / Batch 9 had no
precondition at all; batch 6 still does.

## El defecto: «Retirar» se salía de la pantalla / Remove fell off the screen

```
Retirar at 2146, 376 sized 116, 36 is surrounded by other command controls […]
```

**x = 2146 en una ventana de 1600.** La fila de cada carpeta era un `StackPanel` horizontal con la
ruta y el botón; un `StackPanel` horizontal ofrece a sus hijos **ancho infinito**, así que la ruta
—que es tan larga como sean las carpetas de quien usa la aplicación— empujaba «Retirar» fuera. Es la
**cuarta** vez hoy: la fila de candidatos de una reasignación, los dos controles del reproductor, y
ahora esta. / A horizontal StackPanel offers its children infinite width, so a real folder path pushed
Remove out of the window.

**Corrección**: `Grid` con `*,Auto` y la ruta plegándose, como en las otras dos.

**Y la regla, ya con cuatro medidas detrás**: *un `StackPanel` horizontal con contenido de anchura
desconocida —una ruta, un título, cualquier cosa que venga de la biblioteca de una persona— es un
control que se sale.* Lo que va al lado de un dato de anchura libre se coloca en una rejilla. / A
horizontal StackPanel holding content of unknown width is a control that falls off the screen.

## Un fallo del arnés que merece regla propia / A harness fault worth its own rule

```
Clicking beside RootRemoveConfirmAction changed the very thing the press is meant to change
```

La sonda devolvía la **lista** de carpetas del catálogo, y `PressAsync` compara con
`EqualityComparer<T>`: cada lectura era un array nuevo, así que «cambió» siempre y el clic de control
parecía retirar una carpeta. Lo desconcertante es que el añadir sí pasó — porque con cero carpetas las
dos lecturas devuelven **la misma instancia vacía compartida**, que sí es igual a sí misma. / An array
probe answers "changed" on every read; the empty case passed because an empty array is a shared
instance.

**Regla: una sonda se compara por valor.** Una colección se convierte en un texto o en un número antes
de devolverla. / A probe is compared by value.

## Lo que la escena mide / What the scene measures

- **Las tres clases de raíz**, cada una pulsada desde otra distinta: la que está en vigor al abrir va
  la última, porque pulsar la elección que ya está puesta no tiene efecto que observar.
- **Añadir**: la sonda es el catálogo, no la pantalla. Una pantalla que acepta la carpeta y no guarda
  nada se ve exactamente igual.
- **El consentimiento del primer escaneo**, que es una pulsación aparte precisamente porque **nada se
  escanea hasta que alguien lo dice**.
- **Retirar, con las dos respuestas a su confirmación**: cancelada primero —y la carpeta sigue ahí— y
  confirmada después. Una confirmación a la que sólo se le ha dicho que sí es una confirmación que
  nadie ha demostrado que se pueda rechazar, y rechazarla es justo para lo que está.
- **Y la carpeta sigue en el disco** al final: retirarla de la biblioteca no es borrar los vídeos de
  nadie, y esa distinción es la promesa entera de esta pantalla.

## Las puertas / The gates

`dotnet format --verify-no-changes --severity warn`, compilación con `-warnaserror` (0 avisos), las
**diez suites** —2 083 pruebas, ninguna roja—, `eng/check-coverage.ps1` (seis vigilados, los seis en
100/100) y `eng/check-walk-coverage.ps1`: **129 controles declarados en 128 identidades; 84 pulsados,
44 pendientes**. / All green.
