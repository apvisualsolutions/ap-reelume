# El archivo movido, decidido con el ratón / The moved file, decided with the mouse

Dos controles pulsados, **dos defectos del producto** y uno del arnés. El más grave: el único botón
que confirma una reasignación **estaba fuera de la pantalla** en cuanto la ruta era la de una
biblioteca de verdad. / Two controls pressed, two product defects and one harness defect. The worst:
the only button that confirms a reassignment sat off-screen as soon as the path was a real library
path.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 59 | **61** |
| Pendientes / Pending | 69 | **67** |

## El estado que se siembra es el único que la aplicación produce / The state seeded is the only one the application produces

`FileReconciliationPolicy` contesta `Exact` para una identidad estable y para una huella única, así
que **la única oferta que llega a una persona es una colisión de huella**: dos filas del catálogo
llevan la huella del archivo descubierto y alguien dice cuál de ellas es. Sembrar un solo candidato
habría sido más fácil y habría montado una pantalla que la aplicación no alcanza. / The only offer a
person is ever asked to decide is a fingerprint collision, so that is what the scene seeds.

Y de ahí salieron los dos defectos: **dos candidatos son dos botones**.

## El primer defecto: dos botones idénticos, dos consecuencias distintas / Two identical buttons

`ReassignmentConfirmAction matched 2 controls on screen; a click needs exactly one.`

Cada candidato repite el botón «Es el mismo, reasignar», y el nombre accesible era el mismo en los
dos. Elegir mal no es un detalle: decide **qué entidad conserva su progreso y sus decisiones** bajo la
ruta nueva. Ni un lector de pantalla ni el paseo podían distinguirlos. / Each candidate repeats the
Confirm button and both carried the same accessible name, though each decides a different entity.

**Corrección**: el botón lleva la ruta del candidato como texto de ayuda, que es lo que ya hacía el
resto de la aplicación con sus mandos repetidos —`EpisodeRowView` nombra su «Reproducir» por el
episodio, las filas de duplicados por su ruta—. / The button now carries the candidate's path as help
text, the way every other repeated command in the application already distinguishes itself.

## El segundo, y el que impide usar la función: el botón cae fuera de la ventana / The button falls off the window

```
Es el mismo, reasignar at 2234, 341 sized 326, 36 is surrounded by other command controls […]
Tried: 2234, 305 is outside the window; 1908, 341 is outside the window; 2560, 341 is outside […]
```

**x = 2234 en una ventana de 1600.** La fila del candidato era un `StackPanel` horizontal, y un
`StackPanel` horizontal ofrece a sus hijos **ancho infinito**: el `TextWrapping="Wrap"` de la ruta no
envolvía nunca, la ruta ocupaba lo que quería y empujaba el botón fuera del lado. Sin nada que
desplazar en horizontal, la persona **no puede confirmar la reasignación en absoluto**. Y no es un
caso raro: cualquier ruta de una biblioteca real —`C:\Users\…\Vídeos\Películas\…`— es así de larga.
/ A horizontal StackPanel offers infinite width, so the path never wrapped and pushed the only
confirming control off the side, with nothing to scroll sideways.

**Corrección**: la fila pasa a `Grid` con `ColumnDefinitions="*,Auto"` —el patrón que ya usan la
revisión de duplicados y la propia caja de búsqueda de esta pantalla—, así que la ruta envuelve dentro
del espacio que hay y el botón se queda visible a la derecha. / The row is a grid now: the path wraps
inside the space there is and the button stays on screen.

## El tercero, del arnés: un control puede no sobrevivir a su propia pulsación / A control may not survive its own press

`The control pressed for ReassignmentConfirmAction sits under no view.`

El registro leía la vista del control **después** de que el efecto llegara, y confirmar retira la
oferta: el botón vive dentro de la fila que desaparece, así que para cuando se le preguntaba ya no
colgaba de ninguna vista. Lo que un control es, es lo que era **cuando se pulsó**, y ahí es donde se
lee ahora. / The ledger read the control's view after the effect arrived, and confirming removes the
offer the button lives in. Identity is now taken before the press.

De paso, el arnés dice **dónde** miró cuando no encuentra sitio para el clic de control: sin esos
puntos, «está rodeado» obliga a volver a medir un diseño que el arnés ya había medido. Es lo que
convirtió el segundo defecto de una conjetura en un número. / The harness now reports the points it
tried, which is what turned the second defect from a guess into a number.

## Lo que la escena mide / What the scene measures

- **Confirmar**: se pulsa el **segundo** candidato a propósito. La sonda pregunta **cuál** fila acabó
  en la ruta descubierta, y además comprueba que el candidato no pulsado sigue intacto en la suya: una
  pulsación que hubiera caído en el otro botón deja una reasignación igual de terminada y equivocada.
- **Mantener como nuevo**: la sonda es la **identidad almacenada** de la fila descubierta, que es lo
  que impide que un escaneo posterior vuelva a ofrecer el archivo. Después se comprueba que las tres
  filas siguen siendo suyas: decidir «es un archivo nuevo» decide una fila y no mueve ninguna.
- Las dos ofertas se sirven **de una en una**: dos a la vez pondrían dos botones «Es un archivo nuevo»
  en pantalla, que es el primer defecto otra vez.

## Las puertas / The gates

`dotnet format --verify-no-changes --severity warn`, compilación con `-warnaserror` (0 avisos),
interfaz (439), accesibilidad (95) y `eng/check-walk-coverage.ps1`: **129 controles declarados en 128
identidades; 61 pulsados, 67 pendientes**. / All green.
