# La fila que seguía pulsable debajo de su propia pregunta / The row still pressable under its own question

El guardián de reentrada del cambio de versión cubría el cambio **en marcha** y dejaba fuera el
cambio **que pregunta**: entre que la pregunta aparece y alguien la contesta, la fila que la levantó
volvía a estar pulsable, y una segunda pulsación se llevaba la pregunta y el progreso. /
The version switch's re-entry guard covered a switch **in flight** and left out a switch that
**asks**: between the question appearing and somebody answering it, the row that raised it was
pressable again, and a second press took both the question and the progress away.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Qué faltaba, exactamente / What was missing, exactly

[La tanda anterior](audit-version-switch-reentry.md) puso el guardián que cierra la fila mientras su
propio cambio está en vuelo, y ese guardián termina **cuando el cambio devuelve**. Un cambio que
levanta la pregunta devuelve **en el acto**: el caso de uso contestó `Confirm`, no abrió nada, y el
diálogo se queda esperando a una persona. Así que el `finally` volvía a abrir la fila con la pregunta
encima. / The previous batch closed the row while its own switch was in flight, and that guard ends
**when the switch returns**. A switch that raises the question returns immediately — the use case
answered `Confirm`, opened nothing, and the dialogue is waiting — so the `finally` reopened the row
with the question sitting on top of it.

Los dos guardianes miden cosas distintas y hacen falta los dos: uno cubre **el trabajo**, el otro
cubre **la espera**. / The two guards measure different things and both are needed: one covers the
work, the other covers the wait.

## El rojo / The red

Con la pregunta en pantalla, medido antes de corregir: / With the question on screen, measured before
the fix:

```
PlayerVersionsViewModelTests.A_row_cannot_be_pressed_while_its_own_question_is_on_screen [FAIL]
  Assert.False() Failure
  Expected: False
  Actual:   True
  at PlayerVersionsViewModelTests.cs:line 236
Failed: 1, Passed: 18, Total: 19
```

La línea 236 es `Assert.False(row.SwitchCommand.CanExecute(null))` con `question.IsVisible` en
verdadero. La fila contestaba que sí. / Line 236 is `CanExecute` asked while `question.IsVisible` is
true. The row answered yes.

## Lo que costaba la segunda pulsación / What the second press cost

No es un clic desperdiciado: el segundo cambio empieza el mismo trabajo desde el principio, y ese
trabajo **vacía el cabezal antes de decidir**. Una sesión cuyo demultiplexor todavía no ha aplicado
su posición de inicio contesta cero; cero está por debajo del suelo de reanudación, así que la
política deja de preguntar, abre la otra versión sin consultar y **escribe encima la posición
guardada**. La pregunta desaparece de la pantalla porque el segundo cambio sustituye la decisión que
estaba mostrando. / It is not a wasted click: the second switch restarts the same work, and that work
flushes the playhead before it decides. A demuxer that has not applied its start position answers
zero, zero is below the resume floor, so the policy stops asking, opens the other version unasked and
writes the stored position away. The question leaves the screen because the second switch replaces
the decision it was showing.

Una pulsación de más costaba **el progreso y la oportunidad de opinar sobre él**. / One press too
many cost **the progress and the chance to say anything about it**.

## La corrección, y por qué el parámetro es obligatorio / The fix, and why the argument is mandatory

`PlayerVersionRowViewModel` recibe el `VersionSwitchViewModel` como parámetro **obligatorio**, su
predicado añade `&& !_question.IsVisible`, y se suscribe al `PropertyChanged` del diálogo para
reevaluar cuando `IsVisible` cambia. / The row takes the dialogue as a **mandatory** constructor
argument, its predicate gains `&& !_question.IsVisible`, and it subscribes to the dialogue's
`PropertyChanged` to re-evaluate when `IsVisible` moves.

**Un opcional dejado a nulo era la cuarta forma del defecto de la casa.** Compilaría, cada llamante
que lo olvidase seguiría construyendo filas, y esas filas volverían a ser exactamente lo que este
documento mide: pulsables debajo de la pregunta. Obligatorio, el compilador enumera los sitios. /
An optional left at null is the registered-and-never-fed defect wearing a different hat: it compiles,
and any caller that forgot it would build precisely the rows this document measures. Mandatory, the
compiler enumerates the sites.

**La suscripción no es adorno, y el paseo dice por qué.** Refusar la pregunta **no reconstruye las
superficies** —la escena afirma `Assert.Same(afterConfirm, host.ViewModel.Player!.VersionSwitch)`—,
así que tiene que ser **esa misma fila** la que vuelva a habilitarse. Sin el aviso, el predicado sería
correcto y el botón se quedaría gris en pantalla, y las respuestas «Cancelar» y «Empezar de cero» del
paseo no tendrían dónde pulsarse. / The subscription is not decoration, and the walk says why:
refusing the question does not rebuild the surfaces, so it has to be that very row that becomes
pressable again. Without the announcement the predicate would be right and the button would stay grey.

**No se hizo modal el diálogo**, que era la otra vía: es un cambio estructural de la superficie para
un defecto de un predicado. / The dialogue was **not** made modal — a structural change to the
surface for a one-predicate defect.

## El verde / The green

```
PlayerVersionsViewModelTests + VersionSwitchWiringTests   19/19
ApSolutions.LocalMedia.UiTests                           515/515
ApSolutions.LocalMedia.ArchitectureTests                  30/30
The_other_version_is_switched_to_with_the_mouse_and_its_question_answered   1/1 (19 s)
dotnet build -c Release -warnaserror                     0 advertencias / 0 warnings
dotnet format --verify-no-changes --severity warn        limpio / clean
```

La escena del paseo pasando **es** la comprobación del aviso: pulsa la fila tres veces, y las dos
últimas ocurren después de contestar una pregunta. Sin la reevaluación, el arnés habría encontrado la
fila con `enabled=False`. / The walk scene passing **is** the check on the announcement: it presses
the row three times, and the last two happen after a question was answered.

## Cobertura / Coverage

`PlayerVersionsViewModel.cs` no tiene suelo declarado en `eng/coverage-debt.txt`, así que le toca el
listón entero. Medido en local sólo con `UiTests`, que es un límite inferior: / The file holds no
floor, so it owes the full bar. Measured locally with `UiTests` alone, which is a lower bound:

```
PlayerVersionsViewModel.cs   líneas / lines 100,00 %   ramas / branches 100,00 %
```

Las dos ramas del aviso se recorren solas: `Apply` y `ChooseAsync` emiten seis nombres de propiedad
cada uno y sólo uno es `IsVisible`. / Both branches of the announcement are walked without arranging
anything: `Apply` and `ChooseAsync` each raise six property names and only one of them is `IsVisible`.
