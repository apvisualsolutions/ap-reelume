# La oferta de antes y la de después / The offer before, and the offer after

Los cuatro mandos que contestan a «¿sigo donde lo dejaste?» y a «¿pongo el siguiente?», pulsados con
el ratón sobre sesiones reales. / The four controls that answer "shall I carry on where you left off?"
and "shall I put the next one on?", pressed with the mouse over real sessions.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 112 | **116** |
| Pendientes / Pending | 16 | **12** |

```
The walk: 129 declared command controls in 128 identities; 116 pressed, 12 pending.
```

## Cuatro controles, cuatro sesiones / Four controls, four sessions

Las dos superficies **se contestan una vez** y se retiran, así que no se pueden pulsar los dos botones
de una en la misma sesión: una oferta ya contestada no es una oferta, y pulsar su otro botón sería
pulsar algo que nadie puede alcanzar. La escena abre el reproductor **cuatro veces**. / Both surfaces
answer once and withdraw, so the scene opens the player four times.

**Y «Reanudar» se pulsa desde otro sitio de la línea de tiempo a propósito**: la sesión ya abre en el
punto guardado —eso es lo que hizo la decisión—, así que pulsarlo donde el cabezal ya está sería pedir
la posición que ya se tiene, que es la regla que costó una medición en el deslizador de volumen. Se
mueve el cabezal primero y Reanudar lo trae de vuelta. / Resume is pressed from elsewhere on the
timeline on purpose: the session already opens at the stored point.

## Lo que casi se declara defecto y no lo era / What was nearly called a defect and was not

El primer rojo decía que «Empezar de nuevo» no movía nada. La medición lo desmintió: / The first red
said Start over moved nothing. The measurement said otherwise:

```
playhead over two seconds: 0, 40, 40, 40, 40, 41, 41, 41
```

**El motor contesta 0 hasta que el demultiplexor aplica la posición de inicio.** La pulsación
funcionaba —`chosen=Restart`, medido— y el cabezal ya estaba en 0 cuando se leyó, así que la sonda no
tenía nada que ver cambiar. Se espera a que la sesión llegue al punto guardado **antes** de pulsar. /
The engine answers 0 until the demuxer has applied the start position: the press worked and the probe
had nothing to see.

**De regalo, una aserción que no existía**: que una sesión con progreso guardado **abre donde se
dejó**. Lo único que había era una prueba de cableado que comprueba que la petición lleva la posición,
contra un coordinador que no abre nada. / A byproduct: nothing in this repository checked end to end
that a session with stored progress opens where it was left — the wiring test asserts the request
carries the position, against a coordinator that never opens anything.

## El defecto: dos superpuestos que no se dimensionan / Two overlays that do not size themselves

```
ResumePromptView vis=True eff=True bounds=0, 0, 1280, 1400
```

Sobre un escenario de 1280×1400. Sin las dos alineaciones, el panel se estira a lo que lo contenga —y
en el shell eso es **el escenario entero**—, con sus dos botones en la esquina superior izquierda. Es
la misma forma que la insignia de estado corregida el 2026-08-15, con una diferencia medida: **estos
no llevan fondo**, así que no se tragaban ningún clic; lo que costaban era una oferta dibujada como un
panel a pantalla completa en vez de como la tarjeta que es. / Without the two alignments the panel
stretches to whatever contains it, which in the shell is the whole stage. Unlike the status badge,
these carry no background, so they swallowed nothing.

Los dos pasan a `Border` con alineación, fondo y borde, y sus filas de botones a `WrapPanel`. Quedan
**tres** superpuestos sin dimensionar, cada uno para su escena. / Both become a sized Border, and
their button rows become WrapPanels. Three overlays are left, each for its own scene.

## Lo que la escena prueba / What the scene proves

| Control | Efecto afirmado / Asserted effect |
|---|---|
| Empezar de nuevo / Start over | el cabezal del motor pasa de 40 s a menos de 5 / the engine's playhead leaves the stored point |
| Reanudar / Resume | desde 5 s, el cabezal vuelve a ≥ 39 / from 5 s, the playhead returns |
| Cancelar el siguiente / Cancel next | la oferta se retira **y la sesión sigue siendo el episodio que terminó** / the offer withdraws and the session is still the episode that ended |
| Poner el siguiente / Play now | la ruta del medio pasa a ser el episodio dos / the media path becomes episode two |

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn          # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
eng/run-accessibility.ps1 -Mode Verify -Passes 2           # 112 + 112, 0 críticos / 0 critical
eng/check-walk-coverage.ps1                                # 116 pulsados, 12 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```
