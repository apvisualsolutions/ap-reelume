# La otra versión de lo que se está viendo / The other version of what is playing

Cambiar de versión desde el reproductor, pulsado con el ratón, y la pregunta que eso levanta cuando
hay progreso que no se puede trasladar sin decidir. / Switching version from the player, pressed with
the mouse, and the question that raises when there is progress that cannot be carried across without
a judgement.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 116 | **118** |
| Pendientes / Pending | 12 | **10** |

```
The walk: 129 declared command controls in 128 identities; 118 pressed, 10 pending.
```

## El defecto: la fila de una versión ponía su botón fuera de la ventana / Off the window again

```
Reproducir esta versión sits at 1674, 1364 and the window is 1600x1400, so the press would land
outside it. Scrollers between it and the window: offset 60, viewport 1368, extent 1428
```

**Sexta vez que se mide esta forma en este repositorio**, y esta vez con la vecindad exacta que ya
estaba escrita: un `StackPanel` horizontal ofrece anchura infinita, así que **una etiqueta de anchura
libre al lado de un botón empuja al botón tan a la derecha como largo sea el texto**. La etiqueta de
calidad es «320×240 · H264» hoy y sería más larga con una ruta o un idioma; el botón acababa a 74 px
fuera de la ventana, **sin nada que desplazar en horizontal**. La fila de una versión a la que nadie
podía cambiar. / A horizontal StackPanel offers infinite width, so a free-width label beside a button
pushes the button as far right as the text is long: measured at x=1674 in a 1600 px window.

Pasa a `Grid` con `*,Auto`, que es la misma corrección que recibió la fila de la bandeja de revisión:
la etiqueta toma la columna elástica y se pliega, el botón conserva su ancho y su sitio. / It becomes
a Grid with `*,Auto`, the same correction the review inbox's row got.

## Lo que la escena prueba / What the scene proves

Dos versiones del mismo título con **duraciones deliberadamente distintas** —90 s y 20 s—, porque un
cambio **sólo pregunta** cuando el progreso no se puede trasladar sin decidir: con dos archivos de la
misma longitud la aplicación traslada la posición y no dice nada, que es lo correcto y dejaría los
botones de la pregunta fuera del alcance de cualquiera. / Two versions of deliberately different
lengths, because a switch only asks when the progress cannot be carried across without a judgement.

| Control | Efecto afirmado / Asserted effect |
|---|---|
| Reproducir esta versión / Switch | la pregunta aparece en pantalla / the question appears |
| Continuar ahí / Confirm | la sesión pasa a la otra versión, leído de la ruta del medio / the session moves to the other version |

## Dónde se para, y por qué se para ahí / Where it stops, and why

Las otras dos respuestas de la pregunta —empezar la otra versión de cero y refusarla— **necesitan que
la pregunta se levante otra vez**, y levantarla por segunda vez se topó con un control **en pantalla,
sin nombre y deshabilitado**, mientras la fila que levanta la pregunta estaba a la vez **visible y
habilitada**: / The dialogue's other two answers each need the question raised again, and raising it a
second time ran into a control that is on screen, unnamed and disabled, while the row that raises the
question was itself visible and enabled:

```
versions=1  paths=…\Arrival.2016.Extended.mp4
"Reproducir esta versión" vis=True en=True
Button is on screen but cannot be pressed: visible=True, enabled=False.
```

**Eso es una medición, no un diagnóstico.** Los dos controles se quedan en la lista de pendientes con
esa línea escrita al lado, en vez de pulsarse sobre una suposición. Y se sabe además que confirmar un
cambio **reconstruye la sesión entera**: las superficies que la escena tenía en la mano pertenecen a
una sesión que ya no existe, así que se releen — está en la escena, medido con `Assert.NotSame`. /
That is a measurement and not a diagnosis, so the two controls stay pending with it written beside
them. And confirming a switch is known to rebuild the whole session, so the surfaces are read again.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn          # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
eng/run-accessibility.ps1 -Mode Verify -Passes 2           # 113 + 113, 0 críticos / 0 critical
eng/check-walk-coverage.ps1                                # 118 pulsados, 10 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```
