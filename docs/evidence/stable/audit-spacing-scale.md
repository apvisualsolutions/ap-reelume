# La escala de espaciado, y las dos propiedades que un límite de palabra estuvo a punto de perder / The spacing scale, and the two properties a word boundary nearly lost

Los 186 sitios de espaciado de `src/` pasan a pedir la escala. Es la última fase que se decide
**contando** en vez de eligiendo, y la que cierra la lista de escalares sin gastar: `NotSpentYet`
queda **vacía**. / All 186 spacing sites in `src/` now ask the scale. It is the last phase decided by
**counting** rather than by choosing, and it empties the unspent-scalars list.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El recuento, que es la decisión / The count, which is the decision

| Valor / Value | Sitios / Sites | ¿En la escala 4/8/16/24? |
|---|---|---|
| 8 | 92 | sí / yes |
| **12** | **46** | **no** |
| 4 | 21 | sí / yes |
| 6 | 12 | no |
| 16 | 6 | sí / yes |
| 24 | 4 | sí / yes |
| 2 | 3 | no |
| 10 | 2 | no |

**El 12 gana un escalón propio.** El hueco entre 8 y 16 es de 2× y el uso real se acumula justo
dentro: el 12 es **una cuarta parte de todo el espaciado de la aplicación**. Mapearlo a 16 mueve 46
sitios **+33 %**; a 8, **−33 %**. Cualquiera de los dos sería un cambio visual grande decidido **por
redondeo y no por diseño**, que es lo que un sistema de tokens existe para evitar. / **12 earns a step
of its own.** The gap from 8 to 16 is 2× and real use piles up inside it.

**Los diecisiete restantes se mueven 2 px y ninguno más:** 6 → `Space8` (12 sitios), 2 → `Space4`
(3), 10 → `Space12` (2). Los otros 169 conservan su número exacto. / **The remaining seventeen move
by 2px and no more.**

**Los nombres son numéricos** —`Space4`, `Space8`, `Space12`, `Space16`, `Space24`— porque un nombre
semántico obliga a inventar uno cada vez que falta un paso, y acababa de faltar: la alternativa era
`SpaceSmallMedium`, que no describe nada. El renombrado costó cero porque **ninguna vista consumía
todavía los cinco viejos**; hacerlo después habría costado 186 sustituciones. / **Numeric names**,
because a semantic scale forces a new name every time a step turns out to be missing.

**No hay `Space32`.** Nadie escribe 32 en ninguna de las cinco propiedades, y un escalar que nadie lee
es el defecto característico de esta casa con nombre ordenado — `ScalarTokenTests` lo rechazaría, y se
comprobó rechazándolo. Es la misma decisión que `FontSizeMono`: llega con el primer sitio que lo pida.
/ **There is no `Space32`**, for the same reason `FontSizeMono` is not declared.

## El error de medición, que es lo que hay que llevarse de aquí / The measurement error, which is what to take away

**El primer recuento dio 163 y acusó al documento de haber contado mal. El documento tenía razón.**
La cifra escrita el 2026-08-19 era 183, y 183 + los 3 sitios que `PlayerView` añadió esa misma mañana
son exactamente los **186** reales. / **The first count said 163 and accused the note of counting
wrong. The note was right.**

La causa: el patrón llevaba `\b` delante de `(Spacing|ItemSpacing|LineSpacing)`, así que **no veía
`RowSpacing` ni `ColumnSpacing`** —las de `Grid`—, que son 23 sitios y el mismo `double` diciendo lo
mismo. Los 23 que faltaban eran justo esos. / The pattern was anchored with a word boundary, so it
never saw `RowSpacing` or `ColumnSpacing`.

**Lo peligroso no fue el número, fue la conclusión.** Con 163 delante se construyó una explicación
plausible y falsa —«el 183 contaba los `.axaml` copiados a `bin/`»— que además **cuadraba**: contar con
`rglob` sobre `src/` daba 186. Cuadraba porque los dos scripts diferían en **el patrón** y no en los
archivos, y ninguna de las dos mediciones era la que yo creía estar comparando. **Dos mediciones que
discrepan no se reconcilian con una hipótesis: se reconcilian mirando en qué se diferencian los dos
comandos.** / **The dangerous part was not the number, it was the conclusion**: a plausible and false
explanation that even added up. Two measurements that disagree are reconciled by diffing the two
commands, not by inventing a story that fits.

Y el barrido lo dijo solo: sustituyó 163, informó `sin mapear: []`, y **quedaban 23 literales**. Una
comprobación posterior de que no queda ninguno es lo que convirtió el error en un dato. / The sweep
said so itself: it replaced 163, reported nothing unmapped, and 23 literals were still there.

## La puerta, probada fallando en tres direcciones / The gate, proved by failing in three directions

```
1. RowSpacing="12" literal en HomeView
   -> a view writes its own spacing instead of asking the scale:
        HomeView.axaml: RowSpacing="12"
2. Space12 de vuelta en NotSpentYet
   -> Space12 - the unspent list was empty and is not any more.
3. Space32 declarado y sin gastar
   -> Space32 - declared in the theme and read by no .axaml under src/.
```

La puerta lee **las cinco** propiedades, no las tres del nombre obvio: una que vigilara cuatro de
cinco daría por terminada una fase que no lo está, que es exactamente lo que estuvo a punto de pasar.
Y afirma que el marcado **no escribe el número**, no que el valor pintado coincida: un token de 8 y un
literal de 8 dan el mismo píxel, así que el falso verde es el caso normal. / The gate reads **all
five** properties, and asserts that the markup does not write the number.

**La lista vacía se afirma en voz alta.** Un bucle sobre una lista vacía pasa sin medir nada, y una
comprobación que se ha quedado ciega es indistinguible de una satisfecha. / **Empty is asserted, not
merely allowed.**

## Lo que se movió en la pantalla / What moved on screen

La línea base estructural de Inicio cambió **un solo campo** en sus 36 combinaciones:
`LibraryEntryBottom`, **+1 px lógico** (+2 al 200 %, que es el mismo píxel redondeado). Es el efecto
de los `Spacing="2"` y `"6"` de esa vista subiendo a 4 y a 8, y está dentro de los 2 px que la decisión
anunciaba. Se aprueba con el número medido, que es para lo que la línea base existe: que un cambio
deliberado se apruebe y no se absorba. / Home's structural baseline moved **one field** across its 36
combinations, by one logical pixel. It is approved with the measured number.

## El verde / The green

```
UiTests             611/611
AccessibilityTests  135/135
IntegrationTests    456/457 (1 omitida por diseño / 1 skipped by design)
DocumentationTests   87/87
```
