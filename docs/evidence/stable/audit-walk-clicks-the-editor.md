# El paseo pulsa el botón / The walk presses the button

Tercer commit de los cuatro de
[la auditoría del 2026-08-14](audit-identification-never-reaches-the-catalogue.md). Es la prueba de
que los dos anteriores llegan a quien usa la aplicación. / Third of the four commits: the proof that
the previous two reach the person using the application.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que faltaba / What was missing

`AssembledPhysicalWalkTests` ya conducía la aplicación real con `Window.KeyPress`. Los **clics** de
`Avalonia.Headless` (12.1.1) **no los usaba nadie** en todo el repositorio —comprobado por búsqueda:
cero apariciones de `MouseDown`, `MouseUp` y `MouseMove`—, y ése es el hueco por el que un par de
botones inertes pasaron una auditoría entera. / Nobody in the repository used the headless mouse
input, and that is the gap the inert buttons went through.

El recorrido nuevo: un título identificado, su ficha abierta, su editor abierto, y «Actualizar desde
el proveedor» **pulsado con el ratón**. / The new walk presses the button with the mouse.

## Lo que el primer clic destapó / What the first click uncovered

**El paseo montaba la ventana de una forma que la aplicación no usa.** `AssembledStartup.FinalContent`
**extrae** el `ShellView` del `ContentControl` que la aplicación construye, y el paseo metía ese hijo
en una `Window` nueva. El árbol quedaba entonces colgando de un contenedor que nunca se mostró, así
que **el shell entero estaba fuera del árbol lógico** — medido, no supuesto: / Measured, not assumed:

```
refresh attached=False enabled=False
save    attached=False enabled=False
back(sin Command / no Command) attached=False enabled=True
```

Y de ahí la consecuencia: un `Button` sólo consulta el `CanExecute` de su comando cuando está en el
árbol lógico, de modo que **todos los botones enlazados por `Command` del shell se declaraban
deshabilitados** y ninguno podía recibir un clic. Los que usan `Click=` no dependen de eso y estaban
bien, que es exactamente por qué la diferencia era invisible: **nadie hacía clic**. La misma vista
montada sola daba `enabled=True/True`. / A button only consults its command once it is on the logical
tree, so every command-bound button in the shell reported itself disabled. The same view mounted on
its own reported enabled.

La corrección es que la ventana muestre **lo que la aplicación puso en ella** —el contenedor— en vez
del shell sacado de dentro. El paseo queda más fiel de lo que era. / The window now shows what the
application put in it.

## Dos cosas más que se midieron por el camino / Two more measurements

- **`InputHitTest` no predice a dónde va un clic en este arnés.** Con el botón visible, habilitado y
  su centro dentro de la ventana, `Window.InputHitTest` devolvía el `ScrollContentPresenter` — y sin
  embargo el clic **sí llegaba al botón**. La comprobación previa que se había escrito para «asegurar»
  el clic era lo único que fallaba, y de haberla creído habría declarado roto algo que funciona. Lo
  que vale es el efecto. / The hit test disagreed with where the input actually went; the effect is
  what counts.
- **Un clic que funciona necesita su control.** Antes de pulsar el botón, el recorrido pulsa **al
  lado** y comprueba que no pasa nada. Sin eso, un clic que acertara por casualidad en cualquier otra
  cosa que refrescara la ficha se leería como éxito. / The walk clicks beside the button first and
  asserts nothing happens.

## El recorrido, y por dónde pasa / The walk, and what it goes through

El proveedor contesta **desde su propia caché**, que es el camino que lleva el artefacto publicado:
sin token, `TmdbMetadataProvider` sirve lo que tenga guardado y **no abre ninguna conexión**. Así que
participan el proveedor real, su analizador de JSON, la política de fusión y el repositorio real; lo
único que hace el arnés es dejar el payload donde lo habría dejado una consulta anterior. / The
provider answers out of its own cache, which is the shipped path exactly.

Al final se comprueba lo que ve la persona **y** lo que queda escrito: la ficha muestra «La llegada»
y la fila de `catalog_metadata` también, con su fecha de refresco. / The entry shows it and the row
holds it.

## El ancla del paseo completo, medida el 2026-08-15 / The anchor for the full walk

El propietario decidió que **toda la aplicación** se pruebe de forma autónoma, y eso obligaba a
contestar antes una pregunta: cómo se localiza lo que hay que pulsar. Medido del árbol: **129
controles de mando** en las 48 vistas, y **sólo 60 llevan `x:Name`**, que es lo que
`Click(host, name)` usaba. Anclar en él habría significado añadir un nombre a 69 controles en
beneficio de una prueba. / Only 60 of 129 command controls carry an `x:Name`.

La alternativa estaba ya puesta: **239 elementos llevan `AutomationProperties.Name`**, hay 80 pruebas
que lo exigen para todo control interactivo, y un rediseño cambia la forma sin quitar el nombre. Así
que el paseo localiza por la **clave de recurso**, resuelta contra el mismo diccionario que usa la
aplicación —no por el texto, que se reescribe, ni por el idioma cargado—. / The walk anchors on the
resource key behind the accessible name.

**Probado sobre un control que no tiene `x:Name`**: el candado del título en el editor. El clic al
lado no lo cambia; el clic lo pone. Es la medición que decide que el paseo puede cubrir la aplicación
entera **sin añadir superficie**. Quedan **dos** clicables sin `x:Name` y sin clave por
`DynamicResource`, anotados para la primera tanda. / Two clickables have neither, noted for the first
batch.

## Lo que queda verde / What is green

| Suite | Resultado |
| --- | --- |
| `AccessibilityTests` | 80 / 80 |
