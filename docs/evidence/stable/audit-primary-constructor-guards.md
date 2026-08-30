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

## Lo que costó de verdad: tres archivos que CAYERON / What it actually cost: three files that FELL

La primera vuelta de CI no dijo lo que se esperaba. Tres archivos subieron —`RefreshMetadata`,
`UpdateMetadata`, `IntegrityChecker`— y otros tres **cayeron por debajo del listón y quedaron en
ninguna lista**, que es el fallo que la puerta nombra desde el 2026-08-28:

| archivo | antes | después de promover |
|---|---|---|
| `PreviewRename.cs` | 100/100 | **100/50** |
| `UndoRename.cs` | 100/100 | **100/75** |
| `ExecuteRename.cs` | 100/100 | **100/83** |

**La primera explicación era falsa y merece quedar escrita**: «no tienen pruebas». Sí las tienen —
`RenameTransactionTests` construye los tres con dependencias reales—. Así que en lugar de seguir
adivinando se **reprodujo la fusión de CI aquí**: se descargó el artefacto `test-results` del run y
se fusionaron sus 20 informes con el mismo `reportgenerator` que usa la puerta. La línea del
constructor lee `1/2` en **todos** los informes y `1/2` fusionada. / The first explanation was false:
"they have no tests". They do. So the merge was reproduced here instead of guessed at, and the
constructor line reads `1/2` in every report and `1/2` merged.

**Son dos causas encadenadas, y ninguna es del código:**

1. **Un archivo sin ninguna rama mide 100 % por definición.** La puerta calcula
   `if BranchesTotal > 0 ... else 100`, así que estos tres, que no tenían rama en el constructor,
   medían 100 de ramas sin que nadie hubiera cubierto nada. Su primer par de ramas es también su
   primera ocasión de quedar a medias: con dos ramas totales, una sin cubrir es el 50 %. **Es el
   espejo exacto de «borrar código cubierto baja un archivo»**, que la puerta ya tenía anotado.
2. **Los dos lados del par se toman en suites distintas.** El barrido pasa el null en
   `Application.Tests` y `RenameTransactionTests` pasa la dependencia real en `IntegrationTests`. El
   Cobertura fusionado **se queda con el mejor informe de una línea, no con la unión de ellos**, así
   que el par lee medio cubierto para siempre. `ReviewInboxViewModel` chocó con esta misma pared el
   2026-08-28 y su comentario de suelo lo dice.

/ Two chained causes, neither in the code: a file with **no** branches measures 100 % by definition,
so its first branch pair is also its first chance to be half covered; and the two sides of that pair
are taken in **different suites**, where merged Cobertura keeps the better report for a line rather
than their union.

**La corrección no es otra aserción, es la misma en un solo sitio.** `RenameUseCaseTests` hace que
`Application.Tests` a la vez rechace el null —por el barrido— y construya los tres con algo real.
Medido sobre esa suite sola, la línea del constructor pasa de `1/2` a **`2/2`** en los tres, y las
mitades que quedaban en las líneas 22 y 27 ya estaban enteras en la fusión. La segunda vuelta lo
confirmó: **186 en la lista y 186 medidos bajo el listón, que cuadran**, y ningún archivo fuera de
lista. / The fix is the same assertion in one place, and the second run confirmed it: 186 listed and
186 measured under the bar, with nothing off-list.

**Y el riesgo restante quedó acotado sin gastar otra vuelta**, porque la causa lo predice: de los
ocho promovidos sólo caen los que **no tienen prueba en la misma suite que el barrido**.
`ApplyIdentification` y `RefreshStaleMetadata` la tienen y siguieron en el listón; los tres
`*Rename` no la tenían y cayeron. La teoría cuadra con los seis casos.

## El resultado en la lista / The result on the list

La lista **no se mueve** —186 antes y 186 después— y **tres suelos suben**, porque los tres archivos
ganan ramas y ninguno llega todavía al listón:

| archivo | suelo |
|---|---|
| `RefreshMetadata.cs` | 97/91 → **97/95** |
| `UpdateMetadata.cs` | 81/87 → **81/88** |
| `IntegrityChecker.cs` | 94/83 → **94/87** |

El trinquete se queda en **186**: un suelo que sube no saca a nadie de la lista. / The list does not
move and three floors rise; the ratchet stays at 186.

## Cómo se verificó / How it was verified

`dotnet build -c Release -warnaserror` sin una advertencia y `dotnet format --verify-no-changes
--severity warn` limpio. Las nueve suites que leen estos archivos, verdes. El barrido, sin listas y
con los suelos en 140, 105 y 60 sobre los 148, 112 y 65 medidos. / `dotnet build -warnaserror`
without a warning, `dotnet format` clean, the nine suites that read these files green, and the sweep
with no lists.
