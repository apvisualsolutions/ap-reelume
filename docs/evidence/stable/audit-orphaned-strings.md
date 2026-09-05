# Ocho cadenas que nadie leía, y la puerta que las habría cazado / Eight Strings Nobody Read, and the Gate That Would Have Caught Them

- IDs: `PRD-006`, `PLY-014`
- Fecha / Date: 2026-09-05
- Alcance / Scope: `Resources/Strings.es.axaml`, `Resources/Strings.en.axaml`, `UiTests/Shell/OrphanedResourceTests`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### Por qué había ocho, y por qué ninguna prueba se enteró

Una cadena traducida que ninguna pantalla pide es el defecto de esta casa con otro traje: algo
terminado, pagado **dos veces** —una por idioma— y que nadie llama.

Ninguna rompía nada, porque **no había ninguna prueba que pudiera romperse**. La que compara los dos
diccionarios los compara entre sí, así que una clave muerta en ambos la deja igual de contenta. Y la
que vigila el marcado guarda la dirección contraria: que ninguna vista escriba una palabra en vez de
pedirla. El hueco estaba en medio.

### La puerta, y las dos veces que se equivocó antes de acertar

Se escribió la puerta **antes** de borrar nada, y esa decisión es la que salvó la tanda.

| Pasada | Qué barría | Cadenas que declaró muertas |
| --- | --- | --- |
| Primera | sólo el proyecto de presentación | **58** |
| Segunda | todo `src/` | **42** |
| Tercera | reconociendo también la interpolación | **21** |

**En la primera pasada, cuarenta de esas cincuenta y ocho estaban vivas.** Los códigos de
identificación, los motivos de recomendación, los hallazgos de restauración y los rechazos del
actualizador se resuelven desde capas que la presentación no contiene: el dominio los entrega como
texto y el anfitrión de Windows tiene el menú de la bandeja y los diálogos del sistema. Barrer un
solo proyecto los daba por muertos.

**Y en la segunda seguían vivas trece**, porque una clave se puede componer de dos maneras y la
puerta sólo conocía una: `"MarkerKind" + kind` la reconocía, y `$"RestoreFinding{finding.Kind}"` no.

**Si se hubiera borrado sin la puerta, o con la puerta de la primera pasada, se habrían perdido
cuarenta cadenas que el programa dibuja cada sesión.** Esa es la razón de que la puerta se escriba
antes que la limpieza y no después.

### Las ocho borradas

| Clave | Texto | Por qué sobraba |
| --- | --- | --- |
| `DuplicateGroupCountLabel` | «versiones» | ninguna vista de duplicados la pide |
| `SeasonEpisodeCountLabel` | «episodios» | la cuenta de una temporada nunca se compone |
| `ShowSeasonSuffixOne` | «temporada» | la ficha de serie usa el plural, y cadena vacía si hay una |
| `EpisodePrefix` | «Episodio» | sin consumidor |
| `HomeTitle` | «Inicio» | la pantalla usa su nombre accesible y el raíl el suyo |
| `HomePercentSuffix` | «%» | el porcentaje se devuelve sin el símbolo |
| `PlayerMiniModeAction` | «Mini reproductor» | **tercer nombre** de lo mismo |
| `PlayerFullscreenAction` | «Pantalla completa» | duplicado de la barra de transporte |

Las dos últimas son el hallazgo 12 de la auditoría con su medida: esa función se llamaba «Ventana
flotante» en la barra, «Mini reproductor» en los atajos, y había **un tercer par traducido** que
sobrevivía a una cabecera del reproductor que la aplicación ya no tiene.

### Las trece que se quedan, cada una con su razón

La puerta lleva una lista de excepciones, y **una clave sólo entra en ella cuando algo ya escrito
dice que se va a dibujar**: una fila de la matriz, o un hallazgo de una auditoría que la nombre.

- **Tres del menú de filtros de Cursos** — `CRS-007` está `DESIGN_APPROVED`: es alcance sin empezar.
- **Seis del resumen de biblioteca de Inicio** — el aviso de medios fuera de alcance que el
  propietario decidió el 2026-09-05, en vez de la tarjeta que se borró el 2026-08-23.
- **Dos del final de un curso y una de la última vez que se abrió** — hallazgos 9 y 10, abiertos.
- **Una del escaneo terminado** — hallazgo 4, que va con el botón de cancelar.

**La lista encoge dibujando lo que hay en ella, nunca creciendo porque una cadena parezca útil.**

### Lo medido

`UiTests` completa: **1.230 de 1.230**, cero omitidas. La puerta lleva su propia mitad
anti-ceguera —afirma que barrió más de doscientos archivos y que encontró consumidor para más de
cuatrocientas claves—, porque una puerta que no encuentra nada porque no leyó nada es el modo de
fallo que este repositorio nombra como suyo.

---

## English

### Why there were eight, and why no test noticed

A translated string no screen asks for is this house's defect wearing another coat: something
finished, paid for **twice** — once per language — and called by nobody.

None broke anything, because **there was no test that could break**. The one comparing the two
dictionaries compares them against each other, so a dead key in both leaves it just as happy. And the
one guarding the markup guards the opposite direction: that no view writes a word instead of asking
for it. The gap sat between them.

### The gate, and the two times it was wrong before it was right

The gate was written **before** anything was deleted, and that decision is what saved the batch.

| Pass | What it swept | Strings it called dead |
| --- | --- | --- |
| First | the presentation project only | **58** |
| Second | the whole of `src/` | **42** |
| Third | recognising interpolation too | **21** |

**On the first pass, forty of those fifty-eight were alive.** Identification codes, recommendation
reasons, restore findings and updater refusals resolve from layers the presentation project does not
contain: the domain hands them over as text, and the Windows host owns the tray menu and the system
dialogs. Sweeping one project called them dead.

**And on the second, thirteen were still alive**, because a key can be composed two ways and the gate
knew one: it recognised `"MarkerKind" + kind` and not `$"RestoreFinding{finding.Kind}"`.

**Had the deletion happened without the gate, or with the first pass's gate, forty strings the
program draws every session would have gone.** That is why the gate is written before the cleanup and
not after it.

### The eight deleted

| Key | Text | Why it was surplus |
| --- | --- | --- |
| `DuplicateGroupCountLabel` | «versions» | no duplicates view asks for it |
| `SeasonEpisodeCountLabel` | «episodes» | a season's count is never composed |
| `ShowSeasonSuffixOne` | «season» | the show card uses the plural, and empty for one |
| `EpisodePrefix` | «Episode» | no consumer |
| `HomeTitle` | «Home» | the screen uses its accessible name and the rail its own |
| `HomePercentSuffix` | «%» | the percentage is returned without the symbol |
| `PlayerMiniModeAction` | «Mini player» | **third name** for one thing |
| `PlayerFullscreenAction` | «Fullscreen» | duplicate of the transport bar's |

The last two are the audit's finding 12 with its measurement: that function was «Floating window» on
the bar, «Mini player» in the shortcuts, and there was **a third translated pair** surviving a player
header the application no longer has.

### The thirteen that stay, each with its reason

The gate carries an exception list, and **a key enters it only when something already written says it
will be drawn**: a row in the scope record, or an audit finding naming it.

- **Three from the Courses filter menu** — `CRS-007` is `DESIGN_APPROVED`: scope not started.
- **Six from Home's library summary** — the out-of-reach notice the owner decided on 2026-09-05,
  instead of the card deleted on 2026-08-23.
- **Two from finishing a course and one from when it was last opened** — findings 9 and 10, open.
- **One from the finished scan** — finding 4, which goes with the cancel button.

**The list shrinks by drawing what is in it, never by growing because a string looks useful.**

### What was measured

Full `UiTests`: **1,230 of 1,230**, zero skipped. The gate carries its own anti-blindness half — it
asserts that it swept more than two hundred files and found a consumer for more than four hundred
keys — because a gate that finds nothing because it read nothing is the failure mode this repository
names as its own.
