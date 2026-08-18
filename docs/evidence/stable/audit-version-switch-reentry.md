# La pregunta que se iba de la pantalla sola / The question that left the screen by itself

Un cambio de versión pedido dos veces se salta la pregunta y deja la posición guardada en cero. Lo
cazó CI el 2026-08-18 en la escena del paseo, y el defecto no era del paseo. / Asking for a version
switch twice skips the question and leaves the stored position at zero. CI caught it on 2026-08-18 in
a walk scene, and the defect was not the walk's.

Fecha / Date: 2026-08-18. Rama / Branch: `codex/ap-reelume-mvp-x64`. Run: `32155083153`, sobre
`ba1502e`.

## El rojo / The red

```
AssembledPhysicalWalkTests.The_other_version_is_switched_to_with_the_mouse_and_its_question_answered [FAIL]
  ConfirmSwitchButton is on screen but cannot be pressed: visible=False, enabled=True.
  Failed: 1, Passed: 116, Total: 117 — 5 m 38 s
```

`visible=False` con el arnés quejándose de que el control **está en pantalla** no es una
contradicción: es la prueba de que el botón dejó de estarlo **entre resolverlo y pulsarlo**. El
resolvedor sólo devuelve controles efectivamente visibles, así que lo era; el clic ocurre unos
cientos de milisegundos después. / The harness resolver only returns effectively visible controls, so
the button was visible when it was found and gone when it was pressed, a few hundred milliseconds
later.

**Local no lo ve, y eso también está medido**: dos pasadas completas de la suite de accesibilidad en
esta máquina dan 117 de 117, a **2 minutos** por pasada frente a los **5 m 38 s** del runner. Es una
carrera que necesita la lentitud del runner para abrirse. / Two full local passes give 117 of 117, at
two minutes a pass against the runner's 5 m 38 s. It is a race that needs the runner's slowness.

## El mecanismo, entero, y medido eslabón por eslabón / The mechanism, whole, measured link by link

1. **El arnés vuelve a pulsar lo que parece no haber hecho nada**, a los 300 ms, y eso es
   deliberado: «una persona a quien se le va el clic no espera sesenta segundos, mira dónde está la
   cosa y la vuelve a pulsar». / The harness presses again after 300 ms of apparent silence, on
   purpose.
2. **La fila de la otra versión seguía pulsable mientras su propio cambio estaba en marcha.** Su
   `CanExecute` era `IsAvailable` y nada más. Medido con la prueba nueva **antes** de corregir:
   / The row stayed pressable while its own switch was in flight. Measured before the fix:

   ```
   PlayerVersionsViewModelTests.A_switch_already_under_way_cannot_be_asked_for_again [FAIL]
     Assert.False() Failure
   ```

3. **Todo cambio vacía la posición del reproductor antes de decidir**, para decidir sobre el segundo
   en el que se está y no sobre el último guardado. / Every switch flushes the playhead before it
   decides, so the decision is about the second the session is on.
4. **Y una sesión recién abierta responde cero** hasta que el demultiplexor aplica su posición de
   inicio — medido antes, en la escena de reanudación del paseo: **0, 40, 40, 40, 41** en cinco
   pasadas. Cero está por debajo del suelo de reanudación, así que la política deja de preguntar.
   / And a session that has just opened answers zero until the demuxer applies its start position.

El cuarto eslabón era el único deducido, así que se midió: / The fourth link was the only deduced
one, so it was measured:

```
SwitchMediaVersionTests.A_second_unconfirmed_switch_over_a_playhead_at_zero_opens_without_asking
  Decision.Kind = Restart, Opened = true, request.StartPosition = 00:00:00,
  stored.Position = 00:00:00, stored.SourceMediaFileId = the other version
```

Con lo que el defecto queda dicho sin rodeos: **un doble clic en «cambiar de versión» abre la otra
versión sin preguntar y deja en cero por dónde ibas.** No hace falta un runner lento para sufrirlo;
hace falta para que una prueba lo vea. / So the defect states itself: **a double click on "switch
version" opens the other version without asking and leaves your place at zero.** No slow runner is
needed to suffer it; one is needed for a test to see it.

## La corrección / The fix

La fila deja de ser pulsable mientras su propio cambio está en marcha, y lo anuncia, que es lo que
apaga el botón en pantalla. Es el patrón que la barra de transporte ya tenía por la misma razón —el
salto se apaga mientras el anterior busca— y el arnés ya sabe qué hacer con un control apagado:
esperarlo, en lugar de volver a pulsarlo. / The row is unpressable while its own switch is in flight,
and says so, which is what greys the button out. It is the pattern the transport bar already had for
the same reason, and the harness already knows what to do with a greyed control: wait for it.

El caso de uso no se toca. Decidir «empezar de nuevo» cuando no hay progreso que llevarse es
correcto; lo que estaba mal era llegar ahí por una segunda petición que nadie quiso hacer. / The use
case is left alone. Deciding to start again when there is no progress to carry is right; what was
wrong was arriving there through a second request nobody meant to make.

## Verde / Green

```
Correctas! - Con error: 0, Superado: 451, Total: 451  (UiTests)
Correctas! - Con error: 0, Superado: 230, Total: 230  (Application.Tests)
Accessibility Verify over 2 pass(es): 0 critical, 0 major, 0 minor  (117/117 y 117/117)
```

La escena que falló pasa aquí en las dos pasadas —pasaba también antes del arreglo, porque esta
máquina no abre esa ventana—, así que **quien dice la última palabra es CI**, y esta evidencia se
cierra con su run. / The scene that failed passes here on both passes — it did before the fix too,
because this machine does not open that window — so **CI has the last word**, and this document is
closed by its run.
