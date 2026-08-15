# La segunda tanda, y tres controles que no hacían nada / The second batch, and three controls that did nothing

Segunda tanda del paseo autónomo: el transporte del reproductor. Es la tanda que justifica el
trabajo — encontró **tres defectos del producto**, los tres de la misma forma: visible, activo e
incapaz de hacer nada. / Three product defects, all the same shape.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 15 | **22** |
| Pendientes / Pending | 113 | **106** |

## Por qué los tres sobrevivieron / Why all three survived

**El reproductor responde al teclado él mismo.** `PlayerView` maneja sus propias teclas, así que cada
verificación anterior —incluida la del propio paseo, que pausaba con la barra espaciadora— pasaba por
un camino que no toca ninguno de estos tres defectos. Es la misma frase que ya costó una auditoría
entera: **verificar con el teclado no es verificar con el ratón.** / The player answers the keyboard
itself, so every earlier check went down a path none of these three defects touch.

## Defecto 1: el recuadro de estado cubría todo el reproductor / The status badge covered the whole player

`VideoStatusOverlay` no fijaba alineación, así que su borde se estiraba hasta llenar lo que lo
contuviera. En el shell eso es el escenario entero del reproductor. Medido en ejecución: / Measured
while running:

```
statusVisible=True  statusBounds=0,0,1280,1200  statusOpaque=True  stageBounds=0,0,1280,1200
```

Un recuadro **opaco del tamaño exacto del escenario**, encima del vídeo y encima de la barra de
transporte. Cada clic destinado a reproducir, pausar, detener o al volumen aterrizaba ahí. La cadena
de impacto bajo el botón de pausa lo dijo antes que nada: / The hit chain under the pause button said
it first:

```
Border < ContentPresenter < VideoStatusOverlay < ContentPresenter < ContentControl < Panel
```

Corrección: es un distintivo, y un distintivo se dimensiona a su texto. Arriba a la izquierda, con
margen. / It is a badge, and a badge sizes to its own text.

## Defecto 2: los botones del transporte no volvían a preguntar / The transport buttons never asked again

`PlayerViewModel` notificaba `CanPause`, `CanResume` y `CanStop` por `INotifyPropertyChanged` —lo que
mueve `IsVisible`— pero **no llamaba a `RaiseCanExecuteChanged` en ninguna parte**. Un botón no vigila
esas propiedades: le pregunta a su comando una vez y no vuelve a preguntar hasta que el comando se lo
dice. / A button asks its command once and does not ask again until the command says so.

Consecuencia medida: se pausaba con el ratón y **Reanudar se quedaba deshabilitado para siempre**. Una
sesión que un ratón puede parar y no puede volver a arrancar. / A session a mouse can stop and never
restart.

```
Reproducir is on screen but cannot be pressed: visible=True, enabled=False.
```

## Defecto 3: el deslizador de volumen era decorado / The volume slider was scenery

De los **cinco** `Slider` de la aplicación, era el **único** enlazado `OneWay`, y su vista no tenía
manejador ninguno. `SetVolumeAsync` tenía exactamente **dos llamantes, los dos del teclado**. Mover el
pulgar con el ratón cambiaba un número en la pantalla y nada que se pudiera oír; el siguiente estado
del motor lo devolvía a su sitio. / One of five, the only OneWay one, with two callers and both of
them the keyboard.

El enlace **sigue siendo de una dirección a propósito**: `VolumePercent` es lo que contestó la sesión
y no tiene setter. Lo que lleva un nivel elegido a la sesión es el manejador, con la comprobación de
igualdad que impide el bucle. / The binding stays one way on purpose.

## Cuatro trampas del arnés, todas del mismo día / Four harness traps, all the same day

1. **El desmontaje reemplazaba el fallo.** Dentro de un `using`, si el cuerpo lanza y el `Dispose`
   también, **gana el del `Dispose`**: sesenta segundos de espera y un `ObjectDisposedException` como
   único mensaje, sin una palabra sobre qué se estaba esperando. Ahora el fallo del apagado se
   entrega a la suite, que lo levanta **después** de que la escena haya dicho lo suyo.
2. **Un clic fuera de la ventana no decía nada.** Ahora lo dice, con las coordenadas.
3. **El centro de un control de rango suele ser donde ya está.** El volumen va de 0 a 200 y arranca
   en 100: pulsar su centro pedía exactamente el nivel que ya sonaba. Se pulsa a un cuarto.
4. **La caché de muestras ignoraba la duración pedida.** Pedir noventa segundos con una muestra de
   doce ya en disco devolvía la de doce, y el salto adelante —treinta segundos— se salía del archivo,
   dejando Detener deshabilitado por una razón que no tenía nada que ver. La duración es parte del
   nombre.

## Lo que no se tocó / What was not touched

Los otros cinco superpuestos del reproductor **tampoco fijan alineación**, así que se estiran igual
cuando son visibles. Aquí sólo se corrigió el que la medición demostró que estorbaba; los demás se
verán en sus tandas, con su propia medición. Decirlo vale más que corregir a ciegas cinco vistas que
además están en mitad de un rediseño. / Only the one the measurement proved was in the way.
