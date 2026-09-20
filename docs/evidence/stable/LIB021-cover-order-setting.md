# LIB-021, plan 3 de 3: el orden de portadas se cambia / the cover order can be changed

**Fecha / Date:** 2026-09-20 · **Fila / Row:** `LIB-021` · **Tarea / Task:** `ENG-003` ·
**Decisión / Decision:** [`ADR-0009`](../../adr/0009-a-cover-has-three-origins-and-an-order.md), punto 4 /
decision 4 · **Plan:** [`2026-09-20-lib-021-cover-order-setting.md`](../../superpowers/plans/2026-09-20-lib-021-cover-order-setting.md)

## Qué se midió / What was measured

Lo que contesta esta evidencia es **qué orden se guardó, qué orden se leyó y qué portada salió
elegida**, no qué código se escribió. / What this evidence answers is **which order was stored, which
was read back and which cover won**, not what code was written.

### 1. El orden se repara antes de usarse / An order is repaired before it is walked

`CoverOrderPolicyTests`, 19 pruebas en `Domain.Tests`. Lo medido:

| Entrada / Input | Sale / Comes out |
| --- | --- |
| `null`, `[]` | `[Personal, Provider, Frame]` |
| `[Frame]` | `[Frame, Personal, Provider]` |
| `[Provider, Provider, Personal]` | `[Provider, Personal, Frame]` |
| `[(CoverOrigin)42, Provider]` | `[Provider, Personal, Frame]` |
| `"Provider,Frame,Personal"` | la misma lista, ida y vuelta / the same list, round trip |
| `"Nonsense"`, `"42"`, `"provider"`, `","` | rechazado, y el por defecto igualmente / refused, and the default anyway |

**Cobertura del archivo, medida con el JSON de coverlet** (`--collect:"XPlat Code Coverage;Format=json"`,
suite `Domain.Tests`): **23/23 líneas y 24/24 ramas**. Ninguna rama quedó sin tomar, así que no hubo
que decidir si excluir ninguna.

### 2. El ajuste general sobrevive a cerrar / The general setting survives a restart

`StoredCoverOrderSettingsTests`, 5 pruebas en `IntegrationTests`, contra el almacén **real** sobre un
fichero temporal y no contra un doble: lo que se mide es el viaje por JSON.

- Un fichero nunca escrito da el orden por defecto.
- `[Frame, Provider, Personal]` guardado y **releído con otro objeto** vuelve igual.
- `[Frame]` guardado se escribe en el fichero como `Frame,Personal,Provider` — comprobado leyendo el
  fichero, no el objeto —, así que la reparación es de ida y no sólo de vuelta.
- Un fichero editado a mano con `"Nonsense"` da el orden por defecto.

### 3. La excepción por título viaja hasta SQLite / A title's override reaches SQLite

Migración **25**, `cover_order TEXT NULL`. Huella SHA-256 del fichero, medida y escrita en el
manifiesto: `310D0A705ABD982CEF2D5EBE0F65CD873B379FE022252CC26C08546CFAF65A7A`.

**La puerta del esquema se vio sonar antes de cuadrarla**: con las afirmaciones movidas a 25 y el
manifiesto aún sin la entrada, `SqliteBootstrapTests` dio `Expected: 25, Actual: 24`.

**Y eran cinco afirmaciones, no tres.** La historia de este repositorio decía que la versión del
esquema tiene tres —el conteo, el máximo y la lista de nombres—; al mover esas tres, una cuarta
prueba siguió roja: `Migration_is_idempotent_and_creates_one_valid_copy_per_new_migration` cuenta
**una copia de seguridad por migración** (`:201`) y vuelve a contar el historial (`:208`). Quien
añada la 26 tiene que mover **cinco** números.

**El mutante**: quitando `cover_order = excluded.cover_order` del `UPSERT`,
`A_titles_own_cover_order_is_stored_and_can_be_changed_and_removed` falló con
`Expected: "Provider,Personal,Frame"` / `Actual: "Frame,Personal,Provider"` — la prueba escribe dos
veces a propósito, porque una que sólo guarda una vez se queda verde con esa línea quitada, que es lo
que le pasó a la columna de al lado el 2026-09-18.

### 4. Lo que la cuadrícula pide / What the grid asks for

`LibraryPosterLookupTests`: con un título cuya columna guarda `"Frame,Personal,Provider"`, la
cuadrícula pide la portada **con ese texto**, y con un título sin excepción pide con `null`. Sin esa
aserción, una excepción guardada se dibujaría con el orden general y nada lo diría — el defecto
característico de este repositorio.

`ResolveTitlePosterTests`: la excepción de un título gana **sin preguntar** por el orden general
(contador de lecturas del doble: `0`), y un texto que no nombra orígenes cae al general.

### 5. Lo que el paseo autónomo pulsó / What the autonomous walk clicked

`AssembledPhysicalWalkTests.The_cover_order_is_moved_and_restored_with_the_mouse`, con ratón real
sobre la aplicación montada. La sonda es **el ajuste guardado**, no la lista en pantalla:

1. «Orden de las portadas» en el índice abre la sección.
2. «Bajar» dos veces mueve el orden guardado.
3. «Subir» lo mueve de vuelta — los dos sentidos se pulsan, porque un botón que nunca movió nada es
   medio control sin nada que lo diga.
4. «Restaurar valores por defecto» devuelve el guardado a `[Personal, Provider, Frame]`.

El trinquete de `eng/walk-pending.txt` **no sube**: los dos botones se pulsan.

### 6. El control que cuatro puertas rechazaron, y por qué tenían razón

La excepción por título se construyó primero como un **desplegable**, y **cuatro puertas distintas
lo rechazaron**. Las cuatro decían lo mismo desde sitios diferentes, y hacerles caso era la
corrección; ensancharlas habría sido el arreglo equivocado (regla 0, la factura del 2026-09-02).

1. `MetadataEditorTests`: un `TextBox` sin nombre accesible. La plantilla de `ComboBox` construye uno
   para su modo editable **aunque `IsEditable` sea `false`** — comprobado en la documentación de
   Avalonia antes de tocar nada.
2. `MetadataEditorLayoutTests`: el mismo `TextBox`, por no pintar lo que anuncia.
3. `eng/check-walk-coverage.ps1`: `MetadataEditorView#MetadataCoverSourceLabel` **pulsado por
   nadie**. Un desplegable guarda sus opciones dentro de un popup, y **nada dentro de un popup lo
   alcanza el paseo autónomo**. El trinquete sólo encoge, así que no había dónde apuntarlo.
4. Y la que lo zanjó: con el control cambiado, `OptionRowShapeTests` exigió decidir **qué es** esta
   lista — fila del panel del reproductor o campo de un formulario— y dejarlo escrito.

**El control es ahora una fila de opciones con radios**, que es la forma que la lista de dispositivos
de audio ya tenía aquí: el paseo la pulsa, no tiene partes anónimas, y **las dos puertas del editor
vuelven a su forma original** — no hizo falta aflojar ninguna. La aserción de los radios vive en
`MetadataEditorTests` y **no** en `MetadataEditorLayoutTests`, porque ésa monta la vista **sin
contexto de datos**: la lista sale vacía y una aserción sobre ella pasaría sin medir nada.

`OptionRowShapeTests` lo lleva escrito como lo que no es una fila de lista, con su razón: el
prototipo escribe esa forma tres veces y las tres son listas de un panel de 320 px; vestir así un
campo de un editor de once campos haría que uno pareciera otra cosa.

**Y con el control ya cambiado, tres puertas más reclamaron**, que suman **siete en total**:

5. `ConstructorGuardTests`: `CoverSourceOption` aceptaba un nombre nulo y habría fallado al dibujar
   en vez de al construirse.
6. `CommandNotificationTests`: el comando que elige una opción declara un `CanExecuteChanged` que
   tira sus suscripciones, así que hay que **nombrarlo con el predicado que hace segura su mudez** —
   aquí, que el parámetro sea una de las cuatro opciones, y las cuatro se construyen una vez y no se
   rehacen mientras el editor está abierto.
7. El paseo otra vez: las cuatro filas comparten el nombre accesible de la lista —una identidad para
   su inventario—, así que la fila se distingue por su texto de ayuda, y **ese texto se resuelve del
   diccionario y no se escribe en la prueba**: esta máquina habla español y el runner inglés.

**Ninguna de las siete se aflojó.** La única que cambió de forma fue la de maquetación del editor, y
cambió para medir **mejor**: la aserción sobre las opciones se mudó a la prueba que monta la vista
**con** contexto de datos, porque la de maquetación la monta sin él y habría pasado sobre una lista
vacía — un verde que no mide nada.

## Lo que este cambio deja escrito / What this change leaves written

- `SettingsSection.Covers` es un destino propio y no una tarjeta dentro de Biblioteca, porque
  `OptionGroupTests` rechaza dos grupos de opciones en un mismo destino y `Library` ya es de
  `Scanning`. El grupo `Covers` entra en esa lista con su razón, y los cuatro suelos anti-ceguera de
  esa puerta suben de 12 a 13 y el ancla de `ShellView.axaml` de 10 a 11.
- `MetadataFieldChanges.CoverOrder` tiene **dos centinelas**: `null` no toca la excepción y la lista
  vacía la quita. Sin el segundo no habría vuelta atrás una vez puesta, que es el hueco que
  `PersonalCover` todavía tiene.
- La columna guarda **el orden entero** y no el origen que gana, para que mover el orden general más
  tarde no cambie lo que un título tenía dicho.

---

# English

This is the evidence for the third and last plan of `LIB-021`: the general cover order is moved in
Settings and a single title can override it from its editor. The Spanish section above carries the
measurements; the tables, file names, figures, SHA-256 hash and test names are the same in both
languages and are not repeated here.

What was measured, in order: the domain policy repairing any stored order (19 tests, 23/23 lines and
24/24 branches by coverlet's JSON); the general setting surviving a restart through the real JSON
store over a temporary file; the per-title override reaching SQLite through migration 25, with the
schema gate seen red at `Expected: 25, Actual: 24` before it was squared and a mutant killed by
removing the upsert's assignment; what the grid asks for, with and without an override; the
autonomous walk clicking both directions and the reset, probing the stored setting rather than the
list on screen; and the control four separate gates refused.

**The per-title override was a drop-down first, and seven gates refused it or what replaced it**: two
editor gates over a `TextBox` that `ComboBox`'s template builds for its editable mode whether or not
it is editable; the walk coverage ratchet, because nothing inside a popup can be clicked by the
autonomous walk and that ratchet only shrinks; the option row shape gate, which made the decision be
written down; the constructor guard; the command notification gate, which wants the predicate that
makes a silent `CanExecuteChanged` safe; and the walk again, because four rows share one accessible
name and are told apart by help text resolved from the dictionary — this machine speaks Spanish and
the runner English.

It is a row of radio options now, the shape the audio device list already had. **None of the seven
was loosened.** The one that changed form changed to measure better: the assertion over the options
moved to the test that shows the editor **with** a data context, because the layout test shows it
without one and would have passed over an empty list.

**Five schema assertions, not three**: this repository's history named the count, the maximum and the
name list; a fourth test counts one backup per migration and the history again. Whoever adds
migration 26 moves five numbers.
