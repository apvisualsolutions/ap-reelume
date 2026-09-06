# ADR-0011 — Una pregunta destructiva flota y bloquea / A Destructive Question Floats and Blocks

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-09-06
- Decisor / Decision owner: Product Owner
- Relacionado / Related: [`LIB-001`](../FEATURES.md), [`LIB-010`](../FEATURES.md),
  [`PRD-006`](../FEATURES.md), [ADR-0010](0010-a-state-takes-space-and-an-event-floats.md),
  [la vuelta cuatro de paridad](../evidence/stable/audit-prototype-fidelity-round-four.md),
  [lo que se midió aquí](../evidence/stable/audit-root-removal-deletes-the-catalogue.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

Al retirar una carpeta de la biblioteca había **tres versiones incompatibles** de lo que ocurría, y
el código no cumplía ninguna:

| Quién | Qué decía |
| --- | --- |
| El prototipo, en tres sitios | «El catálogo conserva sus elementos como no disponibles» |
| El aviso de la aplicación | «Sus títulos salen del catálogo, junto con sus marcas y su progreso» |
| El código | `_ = preserveCatalog;` — la bandera se descartaba, y sólo se borraba la fila de la carpeta |

**Y lo que desaparecía de verdad era nada.** Medido el 2026-09-06 sobre la biblioteca sembrada de las
capturas: 1 carpeta, 9 títulos, 28 archivos, 14 entradas de progreso que suman **706,4 minutos**, 0
favoritos, 0 marcadores. Tras retirar, los 9 títulos y los 706 minutos seguían en el catálogo
marcados como **disponibles**, aunque su carpeta ya no se vigilase.

**La puerta que lo tapaba comprobaba una firma.** La única prueba que mencionaba la bandera afirmaba
por reflexión que el parámetro tenía `true` por defecto. Nunca contó una fila.

### Decisión

**Retirar una carpeta borra su catálogo, y la pregunta que lo autoriza flota sobre la aplicación,
enumera lo que se va a perder y advierte de que no se deshace.**

1. **Sale del catálogo lo que se queda sin archivo**, y sólo eso. Una serie con episodios en otra
   carpeta sobrevive con lo que le quede; una copia de una película en otra carpeta es otra ficha,
   con su propio progreso, y no se toca.
2. **Nada se deja a la cascada de la base de datos.** Los disparadores que mantienen los dos índices
   de búsqueda no se ejecutan para las filas que una cascada borra, así que un título retirado
   seguiría apareciendo al buscarlo. Cada tabla lleva su sentencia, en una transacción.
3. **Las portadas se borran después de confirmar la transacción**, y nunca dentro: un fichero
   borrado no se revierte.
4. **La pregunta se dibuja sobre un velo, acotada en los dos ejes y centrada, y el velo no la
   cierra.** Una pregunta destructiva se responde; no se descarta por un clic que cayó al lado.
5. **Ninguno de sus dos botones va acentuado.** Acentuar el afirmativo de un consentimiento es un
   patrón oscuro, y aquí el afirmativo destruye.

### Por qué así, y no de otra manera

**Es la tercera forma que `ADR-0010` no cubre.** Aquel decidió que un aviso que describe un **estado**
ocupa sitio y uno que narra un **suceso** flota. Una pregunta no es ninguna de las dos: no dura lo que
dura una condición ni narra algo ya ocurrido — espera respuesta, y hasta que llega el resto de la
aplicación no es una oferta. Su punto 5 además la respalda: «un empujón dentro de la ventana de causa
y efecto de una acción propia es aceptable». Retirar es una acción propia.

**No es un `Flyout` ni una ventana modal, y eso está medido.** El paseo autónomo no recorre ninguna
de las dos, y cualquiera habría subido su trinquete con la razón «el arnés no llega». Se hace sobre
el patrón que este árbol ya tiene —el diálogo de añadir carpeta y el de cambio de versión—, y el
paseo lo recorrió: 150 de 150, con el trinquete quieto en 23.

**Y la pregunta estaba dibujada dos veces**, en la primera ejecución y en Ajustes, con las mismas
claves en dos superficies. Al pasar a una sola, el trinquete de esquinas escritas a mano bajó de 79 a
78.

### Lo que NO cambia, y por qué

**Un disco USB desconectado sigue prometiendo conservación**, y no es una incoherencia. Desconectar
un disco no es un acto de quien usa el programa: la aplicación describe algo que le pasó a la máquina
y que se deshace enchufando el cable, y por eso la fila se queda marcada como no disponible. Retirar
una carpeta sí es un acto suyo, y una biblioteca que la conserva no ha obedecido. Los dos textos
dicen cosas distintas porque describen situaciones distintas.

### Consecuencias

- **`PreserveCatalog` desaparece.** Estaba modelada, con valor por defecto, hilvanada por un comando,
  una interfaz de dominio, un adaptador y dieciséis dobles de prueba, y no la leía nadie. Retirar es
  una sola cosa ahora, y es la que este ADR nombra.
- **El aviso enseña tres cifras** —títulos, marcas y minutos—, leídas con las mismas expresiones que
  el borrado usa después. Si el lector y el borrado divergieran, el aviso mentiría; lo impide una
  prueba que mide el catálogo antes y después, y no una consulta contra otra.
- **Los marcadores que un detector propuso no se cuentan** entre las marcas, aunque se borren: se
  vuelven a producir analizando el mismo archivo, y sumarlos inflaría la cifra con algo que nadie
  eligió.
- **Los cursos y sus lecciones siguen cayendo por cascada**, que aquí es lo correcto: su ruta es
  relativa a la carpeta, así que sin ella no hay contra qué resolverla.
- **`docs/evidence/mvp/T5-roots.md` decía que el borrado sólo afectaba a la fila de la carpeta.** Era
  el único documento del árbol con esa frase, y se corrigió en el mismo cambio.

---

## English

### Context

Removing a folder from the library had **three incompatible versions** of what happened, and the code
honoured none of them:

| Who | What it said |
| --- | --- |
| The prototype, in three places | «The catalogue keeps its items as unavailable» |
| The application's notice | «Its titles leave the catalogue, along with their marks and progress» |
| The code | `_ = preserveCatalog;` — the flag was discarded, and only the folder's row was deleted |

**And what actually disappeared was nothing.** Measured on 2026-09-06 over the seeded library the
captures use: 1 folder, 9 titles, 28 files, 14 progress rows totalling **706.4 minutes**, 0
favourites, 0 markers. After removing, the 9 titles and the 706 minutes were still in the catalogue
marked **available**, although their folder was no longer watched.

**The gate that covered it checked a signature.** The one test that mentioned the flag asserted by
reflection that the parameter defaulted to true. It never counted a row.

### Decision

**Removing a folder deletes its catalogue, and the question that authorises it floats over the
application, states what will be lost and warns that it cannot be undone.**

1. **What leaves is what runs out of files**, and only that. A show with episodes in another folder
   survives with what it has left; a copy of a film in another folder is a different card, with
   progress of its own, and is not touched.
2. **Nothing is left to the database's cascade.** The triggers that keep the two search indexes do
   not fire for rows a cascade removed, so a removed title would still answer a search. Every table
   gets its own statement, inside one transaction.
3. **Covers are deleted after the transaction commits**, never inside it: a deleted file does not roll
   back.
4. **The question is drawn over a scrim, bounded in both dimensions and centred, and the scrim does
   not dismiss it.** A destructive question is answered; it is not waved away by a click that landed
   beside it.
5. **Neither of its two buttons is accented.** Accenting the affirmative of a consent is a dark
   pattern, and here the affirmative destroys.

### Why this shape and not another

**It is the third shape `ADR-0010` does not cover.** That one decided a notice describing a **state**
takes space and one narrating an **event** floats. A question is neither: it does not last as long as
a condition and it does not narrate something already over — it waits for an answer, and until one
comes the rest of the application is not an offer. Its point 5 also backs this: «a push inside the
cause-and-effect window of one's own action is acceptable». Removing is one's own action.

**It is not a `Flyout` and not a modal window, and that is measured.** The autonomous walk reaches
neither, and either would have raised its ratchet with the reason «the harness cannot get there». It
is built on the pattern this tree already has — the add-folder dialog and the version-switch one —
and the walk reached it: 150 of 150, with the ratchet still at 23.

**And the question was drawn twice**, in the first run and in Settings, with the same keys on two
surfaces. Folding it into one brought the hand-written-corner ratchet down from 79 to 78.

### What does NOT change, and why

**A disconnected USB drive still promises the catalogue is kept**, and that is not an inconsistency.
Unplugging a drive is not an act of the person using the program: the application is describing
something that happened to the machine and undoes itself when the cable goes back, which is why the
row stays marked unavailable. Removing a folder is their act, and a library that keeps it has not
obeyed. The two texts say different things because they describe different situations.

### Consequences

- **`PreserveCatalog` is gone.** It was modelled, defaulted, threaded through a command, a domain
  interface, an adapter and sixteen test doubles, and read by nobody. Removing is one thing now, and
  it is the thing this ADR names.
- **The notice states three figures** — titles, marks and minutes — read with the very expressions the
  removal then uses. If the reader and the deleter diverged the notice would lie; what prevents it is
  a test measuring the catalogue before and after, not one query against another.
- **Markers a detector merely proposed are not counted** among the marks, although they are deleted:
  they come back by analysing the same file, and counting them would inflate the figure with
  something nobody chose.
- **Courses and their lessons still fall by cascade**, which is right here: their path is relative to
  the folder, so without it there is nothing to resolve it against.
- **`docs/evidence/mvp/T5-roots.md` said removal affected only the folder's row.** It was the only
  document in the tree with that sentence, and it was corrected in the same change.
