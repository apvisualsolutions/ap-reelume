# «No hay espacio en el disco» se pintaba igual que «Listo», y la copia recién hecha no la enseñaba nadie / "No room on the disk" was painted like "Done", and the copy just made was shown to nobody

Tercer trabajo del tramo 7 de la §4, **y el que sacó una regla de dos sitios y la puso en uno**. /
§4's seventh tranche, and the piece that took a rule out of two places and stated it once.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Un fallo con cara de éxito / A failure wearing success's face

El bloque de estado era `AccentSubtleBrush` dijera lo que dijera. «No hay espacio suficiente en el disco
de destino» y «Listo» **se pintaban idénticos**. La §4 pide que el estado fallido se lea como uno. / The
status block was the same surface whatever it said.

Los dos fallos que el modelo puede alcanzar son `BackupStatusFailed` y `BackupStatusNoSpace`. **Cancelar
NO es uno**, y eso es una decisión con su razón: su propia cadena dice «Se ha cancelado. No se ha creado
nada a medias», y a quien pulsa cancelar no hay que decirle en rojo que lo que paró paró. `HasFailed` se
**deriva** de la clave en vez de asignarse, así que un tercer fallo no puede aparecer sin pasar por ahí.
/ Cancelling is deliberately not a failure; `HasFailed` is derived rather than set.

**Dos bordes y no un fondo enlazado**, porque `HighContrastTests` prohíbe un `Background` que venga de
un `{Binding` — y con razón: un estado distinguido por un color que eligió un enlace es un estado que
ninguna puerta puede vigilar. / Two borders and not one bound background.

## Y la copia que existía y no se veía / And the copy that existed and was not shown

`LastCopyName` y `LastArchiveName` llevan en el modelo desde que se escribió, **con sus resúmenes
diciendo para qué son**: «el nombre de la carpeta de la copia, nunca la ruta que lleva a ella» y «el
nombre del archivo, nunca la carpeta en la que se escribió». Estaban **modelados para una pantalla**, y
ninguna vista pintaba ninguno: quien acababa de hacer una copia sólo podía enterarse abriendo el
explorador de archivos. / Both were shaped for a screen and no view painted either.

Es el defecto de la casa en su forma más irónica: **el que los escribió pensó en la privacidad de lo que
se muestra y nadie lo mostró.** Van en `FontFamilyMono`, quinto consumidor de la familia, porque son
nombres de archivo. / The person who wrote them thought about the privacy of what would be shown, and
nobody showed it.

## La regla que vivía dos veces / The rule that lived twice

Al añadir el glifo `⚠` saltó `BackupViewTests.Every_visible_string_…` — **exactamente como había saltado
`LifecycleSettingsTests` dos piezas antes**, por el mismo glifo y por el mismo motivo. Dos copias de la
misma regla, cada una vigilando **su propio archivo**, cada una escrita como «ningún literal».

Dos cosas iban mal en eso: cubría **dos vistas de cincuenta**, y era **más estricta que el árbol** — el
mismo `⚠` es literal en todas las demás vistas que lo llevan, y `○ ◐ ●`, `→`, `✓` y `!` lo son por
decisión. / It covered two views of fifty and was stricter than the tree.

Se enuncia una vez, sobre todas: **un literal pasa sólo si no contiene ninguna letra**. Medido antes de
escribirla, **cero** literales con letra en los cincuenta `.axaml` de `src/`, así que la regla no llega
a un árbol que la incumple. Las dos copias se van y sus mitades de comprobación de claves se quedan
donde estaban — **al borrar algo, la garantía se mueve, no se pierde**. / Stated once over all of them,
with the guarantee moved rather than dropped.

## El verde / The green

```
UiTests             694/694
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```

Queda de la fila el bloque de ruta de la base: `BackupViewModel` **no conoce ninguna ruta** —ni la de la
base ni la del destino—, así que traerlo es cablear `IAppDataPaths` hasta él, y eso es una pieza aparte
con su propio consumidor. Anotado en `NEXT-SESSION`. / The path block is left: the model knows no path,
so bringing one means wiring, and that is its own piece.
