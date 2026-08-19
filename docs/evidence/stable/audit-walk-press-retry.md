# Un botón que se lleva su propia pregunta al pulsarse / A button that takes its own question with it

CI puso en rojo `4b2c326` en la puerta del paseo, y la causa no estaba en lo que ese commit cambiaba.
/ CI failed `4b2c326` at the walk gate, and the cause was not in what that commit changed.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

```
The_other_version_is_switched_to_with_the_mouse_and_its_question_answered [FAIL]
RestartSwitchButton is on screen but cannot be pressed: visible=False, enabled=True.
```

## Lo primero, descartar el commit / First, ruling the commit out

`4b2c326` toca `DesignTokens.axaml` y pruebas de tema; ni una línea del reproductor, del cambio de
versión ni del arnés. La escena pasa **seis de seis** en local, a 5 s por pasada contra los 8 s del
runner. / It touches theme files and theme tests, nothing of the player, the version switch or the
harness, and the scene passes **six of six** locally.

## La causa, leída en el bucle que reintenta / The cause, read in the retry loop

`PressAsync` repite una pulsación que no ha mostrado efecto, y antes de repetirla mira **una sola
cosa**: / `PressAsync` repeats a press that has shown no effect, and before repeating it looks at
**one thing**:

```csharp
if (!control.IsEffectivelyEnabled && waits++ < 16) { continue; }
pressed = Click(host, control);      // <- Click es quien afirma visible && enabled
```

Y la escena que falló hace esto: / And the scene that failed does this:

```csharp
await PressAsync(host, "VersionSwitchRestart", () => …Player.MediaPath, …);
```

**Contestar la pregunta la cierra**, que es lo correcto y lo que la propia escena afirma dos veces
(«la pregunta se quedó en pantalla después de refusarla» es un rojo suyo). Así que en cuanto
`RestartSwitchButton` se pulsa, `_chosen` se asigna, `IsVisible` pasa a `false` y el botón sale del
árbol. Su comando, en cambio, **sigue habilitado**. El efecto que la sonda vigila —la otra versión
abriéndose— tarda más en un runner cargado, así que el bucle da otra vuelta, ve `enabled=True`, no
espera, y pulsa un botón que ya no está. / **Answering the question closes it**, which is right and
which the scene itself asserts. The button leaves the tree the moment it is pressed; its command
stays enabled; the effect takes longer on a loaded runner; the loop sees `enabled=True`, does not
wait, and presses a button that is no longer there.

**Es el arnés, y el producto estaba haciendo exactamente lo que debe.** / **It is the harness, and
the product was doing exactly the right thing.**

## Y la regla que había escrita era demasiado corta / And the rule as written was too short

La regla de la casa decía: **`visible=False` acusa al producto y `enabled=False` al arnés**. Ha
resistido varias veces y esta es su excepción: **un control que se retira al hacer su trabajo**. Los
tres botones de esta pregunta son de esa clase, y también lo es cualquier confirmación que cierre lo
que confirma. La regla queda: `visible=False` acusa al producto **salvo que pulsar ese control sea
justamente lo que lo quita de la pantalla**. / The house rule said `visible=False` accuses the
product. This is its exception: **a control that removes itself by working**.

## La corrección / The fix

La decisión sale del bucle a `WalkPressPolicy`, donde se puede medir sin un runner lento: / The
decision moves out of the loop into `WalkPressPolicy`, where it can be measured without a slow runner:

| Control | Esperas | Qué hace / What it does |
| --- | --- | --- |
| visible y habilitado | — | **pulsa otra vez** |
| deshabilitado, visible | < 16 | **espera** (su trabajo está en vuelo) |
| deshabilitado, visible | agotadas | **pulsa igual**, para que un apagado por otra razón lo diga |
| **no visible** | < 16 | **espera** |
| **no visible** | agotadas | **deja de pulsar**: que hable el tiempo de espera del efecto |

Lo último importa: un control que no está en pantalla no lo puede pulsar nadie, así que insistir sólo
cambia un fallo verdadero —«el efecto no llegó»— por uno que culpa al producto. / A control that is
not on screen cannot be pressed by anyone, so insisting only swaps a true failure for one that blames
the product.

## Probada fallando / Proved failing

```
la regla vieja (sólo mira "deshabilitado")  -> RED  A_control_that_left_the_screen_is_waited_for_and_never_pressed_again
```

Se mide con una prueba propia y no sólo a través del paseo, porque **el caso sólo aparece en un
runner lento**: una regla que se ejercita por suerte no la está comprobando nadie. Las tres pruebas
cubren las cinco combinaciones de la tabla. / Measured with a test of its own rather than only through
the walk, because the case only appears on a slow runner: a rule exercised by luck is a rule nobody
checks.

## Verde / Green

```
AccessibilityTests   dos pasadas, 135 y 135, 0 critical / 0 major / 0 minor
El paseo / the walk  129 declared command controls in 128 identities; 128 pressed, 0 pending
```

## Lo que queda anotado y NO se ha tocado / What is noted and NOT touched

Midiendo esto apareció otra cosa, real y distinta: **la fila que abre la pregunta sigue pulsable
mientras la pregunta está en pantalla.** `SwitchToVersionAsync` retorna en cuanto llama a `Apply`, así
que el `finally` de la fila la vuelve a habilitar con el diálogo abierto. Una segunda pulsación vacía
el cabezal, contesta cero, cae por debajo del suelo de reanudación y **se lleva la pregunta**. No es
lo que rompió CI —lo que rompió CI fue el bucle— y no se corrige aquí para no mezclar dos cosas en un
cambio; queda escrito para su propia medición. / Measuring this turned up something else, real and
separate: the row that opens the question stays pressable while the question is on screen. It is not
what broke CI, and it is not fixed here so as not to mix two things in one change.
