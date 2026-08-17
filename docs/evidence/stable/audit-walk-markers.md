# Las marcas de un episodio / The ranges of an episode

Los siete mandos que hacen una marca, la saltan y deciden lo que un detector propuso, pulsados con el
ratón sobre una sesión que está decodificando vídeo de verdad. / The seven controls that make a range,
skip it, and decide what a detector proposed, pressed with the mouse over a session decoding real
video.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 105 | **112** |
| Pendientes / Pending | 23 | **16** |

```
The walk: 129 declared command controls in 128 identities; 112 pressed, 16 pending.
```

## El defecto: un botón fuera de la ventana, por quinta vez / A button outside the window, a fifth time

```
DeleteMarkerButton sits at 1611, 799 and the window is 1600x2000, so the press would land outside it.
Scrollers between it and the window: offset 0, viewport 1968, extent 1968
```

**Un `StackPanel` horizontal ofrece a sus hijos anchura infinita** y los dibuja donde caigan. Estas dos
superficies viven en la columna de 320 px del reproductor, así que «Guardar» entraba y «Borrar»
quedaba **once píxeles fuera de la pantalla**, sin nada que desplazar — inalcanzable con el ratón para
cualquiera. / A horizontal StackPanel offers its children infinite width; these surfaces share the
player's 320 px column, so Save fitted and Delete sat eleven pixels off the screen, with nothing to
scroll.

**Cuatro paneles corregidos** —los dos del editor y los dos de la revisión— a `WrapPanel`, que es la
misma corrección que ya recibieron la fila de la bandeja de revisión y los tres botones de modo del
reproductor. Es la **quinta** aparición de esta forma, y la regla sigue siendo la vecindad y no la
orientación. / Four panels corrected to WrapPanel, the same fix the review row and the player's mode
buttons already got.

## La trampa del arnés, medida aquí / The harness trap, measured here

```
clicking Skip never moved the playhead out of the range it was inside. 8 presses, the last at
143, 1000, where a click reaches PART_ContentPresenter inside SkipMarkerButtonControl inside …
```

El clic **llegaba al botón** —la cadena de impacto lo nombra— y el salto **ocurría**. Lo que no lo veía
era la sonda: `transport.Position` se alimenta de los eventos de posición del motor, y **una sesión en
pausa no emite ninguno**, así que la copia de la superficie se quedaba donde estaba. / The click did
reach the button and the seek did happen; the probe could not see it, because the transport's position
is fed by the engine's position events and a paused session raises none.

**Regla nueva: una sonda alimentada por eventos no ve un efecto mientras la fuente de eventos está
detenida.** Se le pregunta al motor (`GetSnapshotAsync`), en segundos enteros. / A probe fed by events
cannot see an effect while the source of those events is stopped: ask the engine.

**Y hace falta pausar**, porque la sonda del salto es el cabezal y una sesión reproduciendo lo mueve
sola: el clic al lado «cambiaría» justo lo que la pulsación tiene que cambiar. Así que la marca se
guarda **reproduciendo** —que es lo único que hace aparecer la oferta, porque se recompone en los
eventos de posición— y se pausa **después**. / The marker is saved while playing, because that is the
only thing that makes the offer appear, and the session is paused afterwards.

## Lo que la escena prueba, leído de la base de datos / What the scene proves, read from the database

| Control | Efecto afirmado / Asserted effect |
|---|---|
| Tipo de marca / Marker kind | el desplegable se abre / the drop-down opens |
| Guardar / Save | `Intro:0-40` en las marcas de la serie / in the series' ranges |
| Saltar / Skip | el cabezal del **motor** pasa de dentro del rango a ≥ 39 s / the engine's playhead leaves the range |
| Borrar / Delete | la serie vuelve a no tener marcas / the series holds none again |
| Aceptar / Accept | la fila propuesta queda `UserCorrected` y sobrevive a la siguiente detección / survives re-detection |
| Corregir / Correct | la fila lleva el rango tecleado (68 s) / carries the typed range |
| Borrar detección / Delete detection | la fila desaparece del episodio / the row is gone |

**Las detecciones se siembran antes de abrir el reproductor**, porque la superficie de revisión las
carga mientras se construye la sesión: sembrarlas después llenaría una base de datos que ya nadie iba
a leer. / The detections are seeded before the player opens, because the review surface loads them
while the session is being built.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn          # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
eng/run-accessibility.ps1 -Mode Verify -Passes 2           # 111 + 111, 0 críticos / 0 critical
dotnet test tests/ApSolutions.LocalMedia.UiTests            # 448
dotnet test tests/ApSolutions.LocalMedia.MediaTests         # 116
eng/check-walk-coverage.ps1                                # 112 pulsados, 16 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```
