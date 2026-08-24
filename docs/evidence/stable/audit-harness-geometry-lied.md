# El arnés mentía sobre su propia geometría / The harness lied about its own geometry

Veintiuna capturas salieron a 1600 × 2186 pidiendo 1600 × 1000, y la causa no estaba en la
aplicación ni en Windows: estaba en el guion que las tomaba. / Twenty-one captures came out at
1600 × 2186 while asking for 1600 × 1000, and the cause was in neither the application nor Windows:
it was in the script taking them.

Fecha / Date: 2026-08-24. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El síntoma, y lo que descartó cada medición / The symptom, and what each measurement ruled out

Toda captura de la matriz de paridad llegó con **la anchura exacta y el alto disparado**. Una
anchura correcta y un alto que no lo es **no es un problema de escala de DPI**: una escala mal
aplicada deforma las dos dimensiones a la vez. / Every parity-matrix capture arrived with the exact
width and a blown-up height. A right width beside a wrong height is not a DPI-scale problem: a
misapplied scale deforms both dimensions at once.

Cinco hipótesis, cinco mediciones, cuatro refutadas: / Five hypotheses, five measurements, four refused:

| Hipótesis | Medición | Veredicto |
| --- | --- | --- |
| La ventana se auto-dimensiona al contenido | `App.axaml.cs` no usa `SizeToContent`; nace 1180 × 760 lógicos | **refutada** |
| La aplicación restaura una geometría guardada | `settings.json` de esa raíz no tiene `window.main.placement`: el arnés mata el proceso y `Closing` nunca corre | **refutada** |
| `System.Drawing` fija el contexto de DPI antes de tiempo | Con la carga adelantada, `SetProcessDpiAwarenessContext` devuelve `True` y el tamaño sale correcto | **refutada** |
| El tema o el paso de parámetros | Sin `-Theme` y con la altura explícita, idéntico resultado | **refutada** |
| El guion se pisa a sí mismo | Instrumentado, imprimió `wanted 1600x200766` | **CONFIRMADA** |

## La causa / The cause

**PowerShell no distingue mayúsculas y minúsculas en los nombres de variables.** El guion recibía la
altura en un parámetro `$H` y, cincuenta líneas después, guardaba el manejador de la ventana en
`$h`. Son **la misma variable**. Para cuando se llamaba a `MoveWindow`, la altura era el manejador
—200766—, y Windows recortaba esa petición absurda al máximo que la pantalla admite: 2186. La
anchura salía bien porque a `$w` nunca se le asignó nada. / PowerShell does not distinguish case in
variable names. The height parameter `$H` and the window handle `$h` are the same variable; by the
time `MoveWindow` ran, the height was the handle, and Windows clamped that absurd request to what
the screen allows. The width was always right because nothing was ever assigned to `$w`.

Corregido renombrando los parámetros a `-Width`/`-Height`, lo que elimina la colisión de raíz en vez
de esquivarla. Verificado: `1600 x 1000`, a la primera y sin reintentos. / Fixed by renaming the
parameters, which removes the collision at the root rather than dodging it. Verified: `1600 x 1000`,
first try, no retries.

## Lo que queda escrito, que es lo que vale / What is left written, which is what matters

**Un arnés que no verifica su propia geometría captura evidencia de otra cosa.** El guion pide ahora
el tamaño, **lo comprueba** y lo reintenta hasta tres veces, diciendo en voz alta lo que obtuvo
frente a lo que pidió — que es justo la línea que delató el defecto. / A harness that does not
verify its own geometry captures evidence of something else. The script now asks for the size,
**checks it**, and says out loud what it got against what it asked for — the very line that gave the
defect away.

Las veintiuna capturas de la matriz **siguen siendo válidas para lo que la matriz afirma**: son la
aplicación real, con su biblioteca sembrada, y a mayor altura se ve **más** de cada vista, no menos.
La matriz no declaraba ninguna resolución, así que no había afirmación que corregir; lo que habría
sido un defecto de evidencia es haber escrito «1600 × 1000» junto a un PNG que mide otra cosa. / The
twenty-one captures remain valid for what the matrix claims: a taller window shows **more** of each
view, not less. The matrix declared no resolution, so there was no claim to correct.

Esto importa más allá de una captura: el paso 11 —la página del repositorio— pide cinco PNG **a
1600 × 1000**, y hasta hoy el guion no podía darlos aunque se le pidiera. / This matters beyond one
capture: step 11 asks for five PNGs at 1600 × 1000, and until today the script could not deliver
them however politely it was asked.

## Y una alarma falsa del mismo día, por la misma familia de causa / And a false alarm of the same day

Revisando la captura inglesa, «Add media…» parecía **cortado** contra el borde derecho: la píldora no
cerraba su curva. Era falso, y el culpable volvió a ser la herramienta: el recorte con el que se
amplió la esquina llegaba hasta x = 1520 y el botón termina en 1530, así que **el recorte cortó el
botón y luego se leyó ese corte como un defecto de la vista**. Rehecho hasta el borde real —x = 1600—
la píldora cierra entera y la barra de desplazamiento queda a su derecha sin taparla. / Reviewing the
English capture, "Add media…" looked clipped against the right edge. It was false, and the tool was
again to blame: the crop used to magnify the corner ended at x = 1520 and the button ends at 1530.

Es la segunda alarma falsa de la sesión nacida del arnés y no del producto —la primera fue el panel
de fallo del reproductor, que parecía solaparse con la banda de transporte porque el arnés lo apilaba
con otro control y le daba una fracción del alto—. La regla que las dos dejan es la misma: **cuando
algo parece mal en una captura, la primera sospecha es el instrumento**, y se comprueba cambiándolo,
no razonando sobre la imagen. / The rule both leave is the same: when something looks wrong in a
capture, the first suspect is the instrument.
