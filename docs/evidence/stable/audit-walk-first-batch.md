# La primera tanda del paseo, y la puerta que la cuenta / The walk's first batch, and the gate that counts it

Primera tanda del **paseo autónomo de toda la aplicación**, decidido por el propietario el
2026-08-15 y colocado **por delante** de `DES-001` y del rediseño porque es su red. / First batch of
the whole-application autonomous walk, deliberately ahead of the installation work and the redesign
because it is their safety net.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo medido antes de escribir nada / Measured before writing anything

| Medida / Measure | Valor / Value |
|---|---|
| Controles de mando declarados / Declared command controls | **129** |
| Identidades (vista + ancla) / Identities (view + anchor) | **128** |
| Pulsados con ratón antes / Pressed with a mouse before | **2** |
| Pulsados con ratón después / Pressed with a mouse after | **15** |
| Pendientes / Pending | **113** |
| Defectos del arnés que salieron midiendo / Harness defects found by measuring | **3** |

El 129 se confirmó, y confirmarlo destapó cómo se puede contar mal: la primera medición dio **142**
porque `<ComboBoxItem>` casa con `<ComboBox` si el patrón no exige un límite de palabra. 142 − 13
elementos de lista = 129. El guion lleva ese límite y el comentario que dice por qué. / The first
count read 142 because `<ComboBoxItem>` matches `<ComboBox` without a word boundary.

**Las identidades son 128 y no 129 a propósito**: `LibraryView#LibraryBackAction` está declarado
**dos veces** —el mismo botón Atrás en las dos ramas excluyentes de la biblioteca, la de película y
la de serie—. Es el único colapso de todo el árbol. / One collapse in the whole tree, and it is the
same button duplicated across two mutually exclusive branches.

## El rojo: el ancla entregada no alcanzaba el botón Atrás / The red: the shipped anchor could not reach the Back button

El ancla por clave de recurso se entregó en `6c08051` probada contra **un** control sin `x:Name`. La
primera tanda la ejerció contra la biblioteca y falló en el acto, medido quitando el filtro de
visibilidad: / Measured by removing the visibility filter:

```
LibraryBackAction matched 2 controls on screen; a click needs exactly one.
```

Las dos ramas de detalle viven en el árbol visual **a la vez** aunque sólo una se vea, así que casar
por la clave encontraba dos controles donde un clic sólo puede llegar a uno. La corrección es que
**sólo lo que está en pantalla es candidato** (`IsEffectivelyVisible`), que además es la verdad: un
clic no alcanza otra cosa. Y para los diez botones de valoración —que comparten nombre accesible **por
diseño** y se distinguen por la nota que llevan— el desempate es el `HelpText`, que es lo que un
lector de pantalla lee después del nombre. / Both detail branches live in the visual tree at once, so
matching on the key alone found two controls where a click can only reach one.

Nota: la escena de la ficha pasaba **sin** el filtro, porque allí el desempate por `HelpText` bastaba.
El defecto sólo aparecía en la biblioteca. / The card scene passed without the filter; only the
library exposed the defect.

## Un segundo defecto que destapó la puerta en su primera ejecución / A second defect the gate found on its first run

El paseo pulsaba el botón de refresco del editor anclando en su `x:Name`
(`RefreshProviderMetadata`), y las vistas lo declaran con la clave `MetadataRefreshAction`. **El
mismo control tenía dos nombres**, así que quedaba pulsado bajo uno y contado como pendiente bajo el
otro. Lo cazó la comprobación de la puerta que rechaza un control pulsado que ninguna vista declara —
no una lectura. El paseo ancla ahora en la clave, que es lo que el inventario cuenta. / The same
control answered to two names, so it was pressed under one and reported missing under the other.

## Un tercer defecto: el clic «al lado» pulsaba al vecino / A third defect: the control click pressed the neighbour

El clic de control se ponía **a la altura de un control por encima**, y en la ficha los controles se
reparten en filas: la franja de encima de un botón es **la fila anterior**, es decir otro botón.
Medido el 2026-08-15, el clic de control de «Quitar la nota» **aterrizaba en el interruptor de
favorito y lo apagaba**, y el paseo no decía nada porque su aserción sólo preguntaba por la
**nota**. / The control click for *Clear rating* landed on the favourite toggle and turned it back
off, and the walk said nothing because its assertion only asked about the rating.

El defecto no lo encontró una lectura: lo encontró una aserción añadida a propósito para buscarlo, y
falló a la primera. / Found by an assertion added to look for it, and it failed on the first run:

```
A control click reached the favourite toggle.
```

**Un clic de control que pulsa otra cosa no es un control: es una segunda pulsación sin registrar.**
La corrección elige el punto **por geometría** —se miden todos los controles de mando en pantalla y
gana el primer desplazamiento candidato que cae dentro de ninguno—, y no con `InputHitTest`, que ya
se midió que no predice a dónde va un clic. Si no hay tal punto, el paseo lo dice en vez de pulsar a
ciegas. / The point is chosen by geometry, not by hit-testing, and the walk says so rather than
clicking blind when there is no such point.

## Cómo se cuenta lo pulsado / How the pressed half is counted

**En ejecución, no leyendo el fuente.** `WalkLedger` anota una identidad `Vista#ancla` sólo cuando
`PressAsync` ha hecho las **tres** cosas: el clic **al lado** que no cambia nada, el clic real, y el
**efecto** leído por una sonda. Contar llamadas `Press(...)` leyendo el archivo contaría una llamada
escrita, no un control alcanzado —y sería la cuarta versión del error que ya dejó obsoletas a
`CompositionSourceText` y `CompositionGraph`—. / A control enters the ledger only after all three,
recorded while running.

La mitad de la vista se toma **del control que se pulsó**, no de la llamada: el `UserControl` más
cercano por encima. Por eso `DetailsTrailerLinkAction` cuenta como los dos controles que es —uno en la
ficha de película y otro en la de serie, con modelos distintos— en vez de como una clave. / The view
half comes from the control that was actually clicked.

## Lo que la primera tanda cubre / What the first batch covers

Trece controles nuevos, cada uno con clic real, aserción sobre el efecto y clic al lado: / Thirteen
new controls, each with all three:

- **Biblioteca**: filtro, orden, aplicar, la entrada de la lista y Atrás. La entrada es uno de los
  **dos** controles de toda la aplicación cuyo nombre accesible es su propio dato, así que su ancla es
  el título que el propio paseo sembró. / The list entry is anchored on the data the walk seeded.
- **Ficha**: visto, no empezado, quitar la marca manual, favorito, ver más tarde, la nota **7**,
  quitar la nota, y **Reproducir**, que abre la sesión real con LibVLC decodificando el archivo.

Cada aserción lee la superficie **después de que el repositorio conteste**, no lo que el clic
esperaba: un interruptor que sólo se volteara a sí mismo pasaría una prueba que afirmara sobre el
clic. / Every assertion reads the surface after the repository answered.

## Lo que resulta inalcanzable, y por qué / What is unreachable, and why

Dos de los 129, ambos nombrados en `eng/walk-pending.txt` con su motivo: / Two of the 129, both named
with their reason:

- `MovieDetailsView#DetailsTrailerLinkAction` y `ShowDetailsView#DetailsTrailerLinkAction`. Pulsarlos
  entrega la dirección al shell de Windows y **abre un navegador de verdad en la máquina que corre la
  puerta**. Es lo que `LIB-015` decidió que hicieran, así que no es un defecto: es el límite de un
  paseo autónomo. / Pressing them opens a real browser on the machine running the gate.

Y tres más de esta misma área que sí son alcanzables pero piden sembrado que esta tanda no hace:
`MovieResumeAction` (progreso guardado), `MovieTrailerAction` (un archivo de tráiler junto a la
película, descubierto a través de un grupo de versiones) y `EpisodePlayAction` (la ficha de serie).
/ Three more are reachable but need seeding this batch does not do.

## La puerta / The gate

`eng/check-walk-coverage.ps1` corre el paseo, lee su informe y lo compara con el inventario de las
vistas. `eng/walk-pending.txt` guarda lo que falta, **con el motivo en la misma línea**, y **sólo
puede encoger**: la puerta rechaza una entrada que el paseo ya pulsa, rechaza un control que no está
ni pulsado ni en la lista, y rechaza una lista más larga que su trinquete (hoy **113**). / The
pending list may only shrink, and the ratchet is 113.

Comprobado que muerde en las dos direcciones, por ejecución: / Verified to bite in both directions:

```
These command controls are pressed by nobody and are not on the pending list. Press them, or name
them there with the reason: ShellView#NavigationLibrary

These are on the pending list and are not pending: either the walk now presses them, or no view
declares them any more. Take them out of eng/walk-pending.txt: ShellView#Fake
```

CI la ejecuta detrás de las puertas de accesibilidad y recuperación. / CI runs it after the
accessibility and recovery gates.

## Verificación / Verification

```
dotnet format --verify-no-changes --severity warn      sin cambios / no changes
dotnet build -c Release -warnaserror                   0 avisos, 0 errores
AccessibilityTests                                     82/82
DocumentationTests                                     87/87
eng/verify-docs.ps1                                    139 md, 32 localizados, 58 IDs, 46 MVP
eng/check-walk-coverage.ps1                            129 declarados, 128 identidades, 15/113
```
