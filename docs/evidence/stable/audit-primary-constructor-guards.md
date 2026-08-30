# Los ocho constructores primarios, y la lista que se vacía / The eight primary constructors, and the list that empties

El barrido de guardas de null dejó ocho constructores fuera de la regla y **escritos en una lista
cerrada con su motivo**. Los ocho declaraban sus dependencias como constructor primario, que no tiene
dónde poner la guarda sin declarar un campo. Aquí se promueven a constructor explícito, la lista se
queda vacía y desaparece: la regla vuelve a ser estructural, sin nada que mantener a mano. / The null
guard sweep left eight constructors outside the rule and **written down in a closed list with the
reason**. All eight declared their dependencies as a primary constructor, which has nowhere to put a
guard without a field. Here they are promoted to explicit constructors, the list empties and goes,
and the rule is structural again.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-30.

## Qué se rompía, y cuándo / What broke, and when

Un constructor primario que no valida **acepta el null y falla más tarde**, en el primer uso del
campo capturado: una `NullReferenceException` desde dentro de un método, sin nombre de parámetro y
sin relación visible con la composición que la causó. La guarda no es defensa contra un usuario, es
el sitio donde un error de cableado se dice a sí mismo con su propio nombre. / A primary constructor
that does not validate **takes the null and fails later**, at first use of the captured parameter: a
`NullReferenceException` from inside a method, with no parameter name and no visible connection to
the composition that caused it.

## Los ocho / The eight

| tipo / type | parámetros / parameters |
| --- | --- |
| `ExecuteRename` | 1 |
| `PreviewRename` | 1 |
| `UndoRename` | 1 |
| `UpdateMetadata` | 1 |
| `IntegrityChecker` | 1 |
| `RefreshMetadata` | 5 |
| `ApplyIdentification` | 6 |
| `RefreshStaleMetadata` | 6 |

**Veintidós parámetros**, y el barrido los cuenta: `Application` pasa de **127 a 148** guardas
ejercidas y `Infrastructure` de **64 a 65**. `Presentation` se queda en 112, porque no tenía ninguno.
Total **325**. La suma cuadra exactamente con los 22, que es la comprobación de que no se ha añadido
una guarda que nadie toma ni se ha perdido una que ya estaba. / **Twenty-two parameters**, and the
sweep counts them: `Application` goes from **127 to 148** guards exercised and `Infrastructure` from
**64 to 65**, for **325** in total. The sum matches the 22 exactly.

## La lista hizo su trabajo antes de morir / The list did its job before it died

Al convertir los ocho y **antes** de tocar las pruebas, la segunda prueba de cada suite —la que
existe para que la lista sólo pueda encoger— falló nombrándolos uno a uno:

```
The_list_names_only_constructors_that_still_accept_a_null [FAIL]
  These constructors now refuse a null; take them out of the list so the debt stays true:
  ExecuteRename, PreviewRename, UndoRename, ApplyIdentification, RefreshMetadata,
  RefreshStaleMetadata, UpdateMetadata
```

Eso es lo que separa una lista de deuda de una lista de exenciones: **una exención calla cuando deja
de hacer falta y una deuda protesta**. La de `IntegrationTests` hizo lo mismo con `IntegrityChecker`.
/ That is what separates a debt list from an exemption list: **an exemption goes quiet when it stops
being needed, and a debt complains**.

## Lo que había que vigilar al convertirlos / What had to be watched

**`ServiceConsumptionTests` lee los constructores como TEXTO**, no por reflexión: su análisis del
grafo de composición mira los parámetros de constructor de cada implementación registrada para
decidir qué servicio alimenta a cuál. Cambiar la forma del constructor cambia justo lo que ese
analizador lee, y el riesgo no era que fallara —eso se ve— sino que **dejara de ver** y aprobara por
silencio. Tiene su propia prueba contra eso, `The_analysis_still_sees_the_composition_it_guards`, y
las 30 de `ArchitectureTests` quedaron verdes. / **`ServiceConsumptionTests` reads constructors as
TEXT**, not by reflection, so changing a constructor's shape changes exactly what that analyser
reads. The risk was not that it would fail — that shows — but that it would **stop seeing** and pass
by silence. Its own floor test covers that, and all 30 stayed green.

El renombrado de los usos se hizo saltando las líneas de comentario, y por un motivo medido:
`RefreshMetadata` lleva un comentario que dice «another provider's name is not this provider's to
read», donde `provider` casa con la palabra completa. Un barrido ciego habría escrito `_provider's`
dentro de una frase en inglés. / The rename skipped comment lines for a measured reason: one comment
contains the word `provider`, and a blind sweep would have written `_provider's` inside an English
sentence.

## Lo que esto cuesta en cobertura / What this costs in coverage

Otras **dos vueltas de CI**, y por el motivo de siempre: veintidós guardas nuevas son veintidós
ramas nuevas, todas cubiertas por el barrido, así que los archivos suben y la puerta pide mover sus
suelos. Tres de los ocho estaban en la lista de deuda —`RefreshMetadata` 97/91, `UpdateMetadata`
81/87, `IntegrityChecker` 94/83— y los otros cinco ya estaban en el listón, donde una rama cubierta
de más no los baja. / Another **two CI runs**: twenty-two new guards are twenty-two new branches, all
covered, so the files rise and the gate asks for their floors to move.

## Cómo se verificó / How it was verified

`dotnet build -c Release -warnaserror` sin una advertencia y `dotnet format --verify-no-changes
--severity warn` limpio. Las nueve suites que leen estos archivos, verdes. El barrido, sin listas y
con los suelos en 140, 105 y 60 sobre los 148, 112 y 65 medidos. / `dotnet build -warnaserror`
without a warning, `dotnet format` clean, the nine suites that read these files green, and the sweep
with no lists.
