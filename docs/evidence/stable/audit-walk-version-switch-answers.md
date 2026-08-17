# Las otras dos respuestas del cambio de versión / The switch's other two answers

Refusar un cambio de versión y empezar la otra versión de cero, pulsados con el ratón — y el defecto
que impedía las dos: la posición que la persona acaba de aceptar trasladar **se perdía al abrir**. /
Refusing a version switch and starting the other version over, pressed with the mouse — and the
defect that blocked both: the second the person had just agreed to carry across was **lost on open**.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 118 | **120** |
| Pendientes / Pending | 10 | **8** |

```
The walk: 129 declared command controls in 128 identities; 120 pressed, 8 pending.
```

## Lo que estaba medido antes, y era un síntoma / What was measured before, and was a symptom

La sesión anterior dejó esto escrito al lado de los dos controles: «un `Button` en pantalla, sin
nombre y deshabilitado» mientras la fila que levanta la pregunta estaba visible y habilitada. Se
confirmó tal cual, con la comprobación que estaba decidida —si el control seguía colgando de la
ventana—: / The previous session left this beside the two controls, and it reproduced exactly, with
the check that had been decided — whether the control still hung from the window:

```
before: row detached=False vis=True  en=True  name=Reproducir esta versión
after:  row detached=True  vis=True  en=False name=<null>
rows=1 [0, 0, 340, 36 detached=False vis=True en=True]
```

El control **queda desprendido del árbol** —y uno desprendido pierde el nombre que le daba un
`DynamicResource` y contesta `IsEffectivelyEnabled` falso—, mientras una fila nueva, viva, ocupa su
sitio. Pero eso es lo que hace `PressAsync` **cuando la sonda no cambia**: reintenta, y para el
segundo intento la sesión ya se ha reconstruido. **El síntoma, no la causa.** / The control is
detached from the tree while a live replacement takes its place — but that is what `PressAsync` does
when the probe never changes: it presses again, and by then the session has been rebuilt.

## La causa: aritmética, no arnés / The cause: arithmetic, not harness

En la misma ejecución, `asking=False`. **La pregunta no se levantaba.** / In the same run, the
question was never raised.

`ProgressPolicy.MinimumResumePosition` son **treinta segundos**, y la escena cambiaba a una versión
de **veinte**: confirmar dejaba la sesión en 8,9 s de 20, el siguiente cambio vaciaba esa posición
antes de decidir, y la política contestaba `Restart` —«no hay progreso que merezca la pena»— en vez de
preguntar. **No existe ninguna posición entre 30 s y un final de 20 s**, así que las dos respuestas
que faltaban eran inalcanzables por aritmética. / The resume floor is thirty seconds and the scene
switched onto a twenty-second version, so no position could ever satisfy both. The two answers were
unreachable by arithmetic.

Las dos duraciones pasan a **60 s y 180 s**, que es lo que hace que **cualquiera de las dos** pueda
sostener progreso que merezca volver: 40 s de 60 se trasladan a 120 s de 180, y las dos cifras quedan
por encima del suelo y por debajo del final. / Sixty and a hundred and eighty: 40 s of 60 carries
across as 120 s of 180, and every leg stays above the floor and below the end.

## El defecto de producto: la posición aceptada se abría en otro sitio / The transferred second was opened over

Con las duraciones arregladas, la escena midió lo que nadie miraba: / With the lengths fixed, the
scene measured what nobody was watching:

```
playhead: 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 3
path=…\Arrival.2016.Extended.mp4
stored[theatrical]=00:02:01.4550000 dur=00:03:00
stored[extended]=00:00:00 dur=<none>
```

El cambio hizo **todo su trabajo**: preguntó, calculó el segundo, lo escribió (`00:02:01`). Y después
la sesión se reabrió **desde cero** y empezó a escribir ese cero bajo su propia clave de contenido,
encima del progreso que la persona acababa de aceptar. / The switch did all of its work — it asked,
worked out the second, wrote it — and then the session reopened at zero and began writing that zero
over the progress just agreed to.

**Es el defecto característico de la casa, del lado del consumidor.** `PlayDetailsRequest` lleva una
posición de inicio desde el primer día, su documento decía ya que «un cero es un reinicio deliberado y
no una ausencia de progreso», y una prueba vigilaba que la ficha de película la construyera bien.
**Nadie la leía nunca**: `OpenPlayerAsync` tomaba el archivo del encargo y la posición de la política
de reanudación, que lee el almacén bajo la clave del **archivo nuevo** — de la que una versión recién
abierta no tiene ninguna. / The house defect, from the consumer's side: the field was produced in five
places, documented, and guarded by a test — and read in none.

La corrección es que la posición pedida **mande cuando se pide**, y para eso la ausencia necesita un
valor propio: `TimeSpan?`, donde `null` significa «decide tú con la política». Quien ya sabía dónde
abrir —el cambio de versión, la ficha de película, «Continuar» de la biblioteca— manda; quien sólo
quería abrir un archivo —una fila de episodio, el episodio siguiente— pasa `null` y se comporta
exactamente igual que antes. / The requested position wins when it is given, and absence gets a value
of its own: `null` asks the host to decide.

## Lo que la escena prueba ahora / What the scene proves now

| Control | Efecto afirmado / Asserted effect |
|---|---|
| Reproducir esta versión / Switch | la pregunta aparece, tres veces / the question appears, three times |
| Continuar ahí / Confirm | la sesión pasa a la otra versión **y llega al segundo trasladado** / the session moves and reaches the transferred second |
| Cancelar / Cancel | la respuesta queda registrada, la pregunta se retira y **la sesión no cambia** / the answer registers, the question withdraws, the session does not change |
| Empezar de nuevo / Start over | la otra versión se abre **por su principio**, leído del motor y del almacén / the other version opens at its beginning |

El orden —confirmar, refusar, empezar de nuevo— **lo fija la misma aritmética**: empezar de nuevo deja
la sesión en cero, que está por debajo del suelo de reanudación, así que ninguna pregunta sobrevive a
esa respuesta. Refusar conserva su sentido en medio: se pulsa con una sesión sonando y otra versión a
la que cambiar, así que «no cambió nada» es una afirmación sobre algo. / The order is fixed by the
same arithmetic: starting over leaves the session at zero, below the resume floor, so no question
survives it.

## El tercer superpuesto que no se dimensionaba / The third undimensioned overlay

Medido en la misma ejecución, y con el mismo número que los dos anteriores: / Measured in the same
run, with the same number as the previous two:

```
stage=0, 0, 1280, 1400
surfaces=1 [0, 0, 1280, 1400 vis=False]
```

Sin alineación, el diálogo se estira sobre **todo el escenario del reproductor** y dibuja sus tres
respuestas en la esquina superior izquierda. Recibe la corrección que ya recibieron la oferta de
reanudar y la del siguiente episodio: `Border` centrado, con relleno, fondo y borde, y la fila de
respuestas a `WrapPanel`. **Quedan dos** —`SkipMarkerButton` y `LooseFileBanner`—, cada uno en su
escena y con su medición. / Without alignment the dialogue stretches over the whole player stage. It
gets the correction the resume prompt and the next-episode offer already got. Two overlays left.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn                      # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
dotnet test …UiTests                                                   # 448 / 448
dotnet test …AccessibilityTests                                        # 113 / 113
eng/run-accessibility.ps1 -Mode Verify -Passes 2                       # 113 + 113, 0 críticos / 0 critical
eng/check-walk-coverage.ps1                                            # 120 pulsados, 8 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```
