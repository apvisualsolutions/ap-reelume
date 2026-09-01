# El asentado del paseo estaba en el sitio donde dolió, no donde está la regla, y una sonda midió el caso que no era / The walk's layout settling sat where it hurt rather than where the rule is, and a probe measured the wrong case

La limpieza que el relevo del 2026-09-01 dejó decidida y sin ejecutar. Su premisa —que las dos
formas de asentar el layout son equivalentes— resultó **falsa en el único caso que importa**, y
comprobarlo movió el arreglo de la carrera a donde tenía que estar. / The cleanup the previous
handover left decided and unexecuted. Its premise — that the two ways of settling the layout are
equivalent — turned out **false in the only case that matters**.

Fecha / Date: 2026-09-02. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La sonda que midió el caso fácil / The probe that measured the easy case

La primera medición hizo lo obvio: una fila de tres botones, se quita el del medio, y se mira dónde
queda el tercero. / A row of three buttons, the middle one removed, and where the third lands:

```
nothing                    third.X 80 -> 80
UpdateLayout               third.X 80 -> 40
InvalidateMeasure+RunJobs  third.X 80 -> 40
```

Contestó «equivalentes», y **es cierto para ese caso y engañoso para el del paseo**: quitar un hijo
de un panel marca el árbol sucio, y sobre un árbol sucio `UpdateLayout()` ejecuta el pase igual que
`InvalidateMeasure()` seguido de `RunJobs()`. La pregunta que decide es otra: **qué pasa cuando el
árbol se declara limpio.** / It is true for that case and misleading for the walk's: removing a child
marks the tree dirty, and on a dirty tree the two do the same thing. The deciding question is what
happens when the tree calls itself clean.

## La sonda que midió el caso del paseo / The probe that measured the walk's case

Instrumentando `BesidePoint` en el paseo completo —250 clics «al lado», sobre las 37 escenas—, con
`IsMeasureValid`/`IsArrangeValid` de la ventana antes del asentado y el número de controles cuyo
rectángulo cambia después: / Instrumenting `BesidePoint` over a full walk — 250 beside-clicks across
37 scenes:

| | ventana válida antes | controles movidos por el asentado |
|---|---|---|
| Con `Reveal` usando `UpdateLayout()` | **250 de 250** | **5** |
| Con `Reveal` forzando el pase | **250 de 250** | **0** |

**Los 250 decían que el layout estaba al día, y en cinco no lo estaba.** Un árbol que se declara
limpio no es un árbol cuya geometría de descendientes esté vigente, y la diferencia es invisible para
cualquiera que se limite a preguntar. Por eso `UpdateLayout()` —que corre el pase **sólo si está
sucio**— no bastaba, y `InvalidateMeasure()` seguido de `RunJobs()` sí. / All 250 said the layout was
current, and in five it was not. A tree that calls itself clean is not a tree whose descendants'
geometry is up to date.

Los cinco, por si sirven de pista: «Marcar todas», «Retirar», `RestoreProviderMetadata`,
«Reproducir esta versión» y `UpdateInstallButton`.

## Lo que cambia / What changes

**El forzado se muda a `Reveal`**, que es por donde pasan **las dos** rutas que leen geometría:
`BesidePoint`, que elige el punto de al lado, y `Click`, que es lo que usa cada `PressAsync` para
apuntar al centro de un control. / The forcing moves into `Reveal`, which both geometry-reading paths
go through.

**Y eso destapa lo que llevaba fuera de la protección:** el press ordinario. Desde el 2026-09-01
`BesidePoint` asentaba y `Click` no, así que **el clic al centro de un control leía exactamente la
geometría rancia de la que el clic de al lado estaba protegido**. Nadie lo había mirado porque la
carrera se manifestó por el lado del beside-click. / The ordinary press was outside the protection:
a click at a control's centre read exactly the staleness the beside-click was shielded from.

Con `Reveal` forzando, la copia de `BesidePoint` mueve **0 de 250** y se retira; y los **33**
`host.Window.InvalidateMeasure()` repartidos por las escenas —treinta y dos con la forma
`RunJobs / InvalidateMeasure / RunJobs`, más el del sitio donde se encontró la carrera— se van con
ella. Queda **una** forma, en **un** sitio. / One form, in one place.

Los treinta y tres también usaban `host.Window`, y `Reveal` usa `RootOf(host, control)`: con el mini
reproductor en pantalla no son la misma ventana, así que asentar el shell para leer la otra dejaba la
staleness puesta. / They settled `host.Window` while the geometry read belongs to the control's own
window.

## Lo verificado / What was verified

- **Accesibilidad, dos pasadas** —como las corre CI—: 147 de 147 cada una, `0 critical, 0 major,
  0 minor`.
- **La puerta del paseo**: 244 controles declarados en 239 identidades, **219 pulsados, 20
  pendientes**. El trinquete no se mueve.
- Formato, y la solución entera en Release con `-warnaserror`: sin avisos.

## Lo que esto NO demuestra / What this does not prove

**La carrera no se reproduce en esta máquina** y no se ha reproducido nunca: la escena sola y la suite
entera han salido verdes aquí en las tres fechas. Lo que estas mediciones establecen es el
**mecanismo** —que un árbol válido puede tener geometría rancia, y en cuántos casos de 250— no que el
rojo de CI haya desaparecido. Eso sólo lo dice la segunda pasada de CI, y sólo con el tiempo. / What
these measurements establish is the mechanism, not that CI's red is gone.

**Y la lección que deja la primera sonda es la más transferible**: una medición que contesta rápido y
limpio puede estar contestando **una pregunta parecida**. La de la fila de tres botones era correcta,
reproducible y sin relación con el caso que decidía. / A measurement that answers quickly and cleanly
may be answering a similar question rather than the one that decides.
