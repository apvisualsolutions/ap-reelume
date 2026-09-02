# La pantalla completa cambiaba una flecha y nada más, y dos deducciones mías tuvieron que morir antes de encontrarlo / Fullscreen swapped an arrow and nothing else, and two of my own deductions had to die before it was found

«Aun no funciona la pantalla completa […] me refiero a todo el monitor, no que se vea el menú de
Windows», dijo el propietario el 2026-09-02. / Reported the same day.

Fecha / Date: 2026-09-02. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El defecto, y es el de la casa / The defect, and it is this repository's own

`ApplyPlaybackMode` hacía esto y nada más para pantalla completa: / For fullscreen it did this and
nothing else:

```csharp
session.Player.IsCompact = mode == PlaybackMode.Mini;
session.Player.IsFullscreen = mode == PlaybackMode.Fullscreen;

if (mode == PlaybackMode.Mini) { /* construye la ventana y la coloca */ }
```

**A la ventana no se la tocaba.** La bandera decide qué flecha dibuja el botón del transporte, y ahí
acababa todo. `PlayerWindowCoordinator` tiene la geometría de pantalla completa —escrita, comentada,
probada y alcanzable— y **nadie la llamaba nunca con ese modo**: las dos únicas llamadas a `Apply`
en todo `src/` son para el mini reproductor. Registrado y nunca alimentado, esta vez en forma de
**modo**. / Nobody ever called it with that mode: both calls to `Apply` in `src/` are the mini
player's.

**Y ninguna puerta lo vio**, por una razón que vale para cualquier prueba de este árbol: la suite que
cubre esto afirma sobre `shell.PlaybackMode`, y el modelo **sí** cambiaba, siempre. Lo que faltaba
era la mitad que nadie preguntaba —si algo en pantalla lo seguía—. / The view model did change every
time; what nothing asked was whether anything on screen followed.

## Dos deducciones mías que la medición mató / Two of my own deductions the measurement killed

Antes de llegar al defecto real diagnostiqué dos veces mal, y las dos veces por deducir en vez de
medir. Se anotan porque son el tipo de error que este repositorio ya tiene nombre para. / Both are
written down because they are the kind this repository already has a name for.

**1. «Tu pantalla no está escalada.»** Leído con `System.Windows.Forms`, que informa en unidades
lógicas desde un proceso que no es consciente del DPI: dijo 2560×1440. Avalonia, que informa en
píxeles físicos, dice **3840×2160** — la pantalla está a **factor 1,5**. / The tool was reporting
logical units from a DPI-unaware process.

**2. «Una ventana del tamaño de la pantalla no tapa la barra de tareas.»** Falso, y medido sobre la
pantalla real con un arnés que pinta la ventana de magenta y cuenta la franja inferior: / Measured on
the real screen:

```
mode=size   screen=3840x2160  bottomStrip magenta=960/960
mode=state  screen=3840x2160  bottomStrip magenta=960/960
```

Las dos formas la tapan, 960 de 960 muestras. **La causa del síntoma no era ésa**, y de haberme
quedado ahí habría «arreglado» algo que ya funcionaba mientras el defecto seguía puesto. / Both cover
it. Stopping there would have fixed something that already worked.

## Lo que cambia / What changes

**El modo llega a la ventana del shell.** Todo modo que no sea el mini reproductor —que vive en una
ventana propia— aplica la geometría del coordinador a la ventana del shell, y al entrar en pantalla
completa se **recuerda** la que había para devolverla al salir. / The mode reaches the shell's own
window, and what it was is remembered on the way in.

**Y la pantalla completa es además un estado de ventana**, no sólo un tamaño. Aunque la medición de
arriba diga que el tamaño basta para tapar la barra en esta máquina, `WindowState.FullScreen` es lo
que el sistema entiende por pantalla completa y lo que no depende de que un tamaño coincida. La
razón por la que se había evitado se conserva escrita —en una pantalla escalada ese estado se midió
entregando el tamaño de cliente en píxeles físicos mientras el render aplicaba la escala— y por eso
**se hacen las dos cosas**: el tamaño en unidades lógicas y el estado. / Both, and the earlier reason
for avoiding the state is kept rather than deleted.

**El orden importa y está escrito donde se hace**: el estado se suelta *antes* de escribir la
geometría y se pone *después*. Una ventana en un estado tiene su `Width` y su `Height` guardados y no
dibujados, así que al revés el modo incrustado volvería del tamaño de la pantalla. / A window in a
state has its size stored and not drawn.

## La puerta, probada fallando / The gate, proved by failing

Dos pruebas nuevas montan el shell en una ventana de verdad, cambian el modo y miran **la ventana**.
Quitando el cableado, las dos caen: / Removing the wiring, both fall:

```
ShellWindowModeTests.Asking_for_fullscreen_puts_the_shells_own_window_into_that_state [FAIL]
ShellWindowModeTests.Leaving_fullscreen_gives_back_the_window_that_went_in [FAIL]
```

La segunda es la mitad fácil de perder: comprueba que salir devuelve **la ventana que entró** —900 de
ancho, no los 1180 del valor por defecto del coordinador— y que el estado se soltó, porque una
ventana que se queda en él ignora cualquier tamaño que se le escriba después. / That leaving gives
back the window that went in.

## Lo verificado / What was verified

- 1 144 pruebas de interfaz, 30 de arquitectura, formato y compilación en Release con
  `-warnaserror`: sin avisos.
- **Accesibilidad, dos pasadas** —el paseo cambia de modo con un ratón real—: 147 de 147 cada una,
  `0 critical, 0 major, 0 minor`.
- La puerta del paseo: 246 controles, **219 pulsados, 22 pendientes**, sin mover el trinquete.

## Lo que esto NO demuestra / What this does not prove

**Que en una pantalla escalada el estado se comporte.** La razón original para evitarlo se midió en su
día y no se ha vuelto a medir con escala; lo que hay ahora es el tamaño en unidades lógicas —que es
lo que aquella medición pedía— **y** el estado encima. Si el defecto de escala sigue vivo, se
manifestará como una barra inferior fuera de sitio, y el tamaño en unidades lógicas es exactamente lo
que lo evita. / The scaled-display behaviour of the state has not been re-measured.
