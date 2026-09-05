# La columna de la raíz que nadie escribía / The Root Column Nobody Wrote

- IDs: `LIB-010`, `LIB-002`
- Fecha / Date: 2026-09-05
- Alcance / Scope: `Domain/Discovery`, `Infrastructure/Data/Repositories/LibraryRootRepository`,
  `Application/Discovery/ScanCoordinator`, `Presentation/Onboarding`, `Presentation/Settings`,
  `Presentation/Shell/ShellViewModel`

Este documento contiene primero la evidencia en español y después su traducción inglesa. Ambas
partes deben actualizarse juntas.

This document contains the Spanish evidence first and its English translation second. Both parts
must be updated together.

---

## Español

### El rojo, medido antes de tocar nada

`LIB-010` está `VERIFIED` y promete, con esas palabras, que una unidad desconectada «muestra no
disponible». La lista de carpetas de Ajustes decía **«Disponible» siempre**.

La prueba que ya monta y retira una unidad de verdad —`subst`, y comprobando que la carpeta ha dejado
de existir antes de escanear— sólo miraba los **archivos**. Se le añadió la lectura de la carpeta:

```
RemovedDriveTests.A_drive_that_is_taken_away_keeps_the_catalogue_and_comes_back_without_duplicates [FAIL]
Assert.Equal() Failure: Values differ
Expected: Unavailable
Actual:   Available
```

### Dónde se cortaba la cadena, y por qué la evidencia anterior no lo vio

La columna existe en el esquema desde la migración 0002, **con su restricción de tres valores ya
puesta**, y sólo la escribía el `INSERT` que crea la carpeta — siempre como disponible. **No existía
ninguna operación que la actualizara después**: el contrato del repositorio de carpetas tenía cuatro
métodos y ninguno escribía disponibilidad.

Lo que sí se actualizaba al escanear era la disponibilidad de **cada archivo**, que es otra tabla y
otra insignia. La evidencia que dio `LIB-010` por buena —`T8`— midió exactamente eso: `IsAvailable`
de los archivos. **La cadena se verificó hasta los datos y no hasta la pantalla**, que es la misma
lección que este repositorio ya escribió el 2026-08-15 y otra vez el 2026-09-04.

### El tercer estado estaba a una línea de existir

El dominio declara `Available`, `Unavailable` y `AccessDenied` desde siempre, y el escaneo **ya
distinguía** los dos fallos: mira si el código de error de la ruta de la raíz es `AccessDenied` o
`IoError`. Acto seguido los aplastaba a un `bool`, y ahí moría el tercero.

**No es un matiz cosmético.** Un disco que no está se arregla enchufándolo; una carpeta que Windows
rechaza está exactamente donde estaba, y decir «no disponible» manda a alguien a buscar un cable que
ya está puesto.

### El segundo corte, detrás del primero

Arreglar la escritura no bastaba: **la lista sólo se releía al entrar en la Biblioteca**, no al entrar
en Ajustes, que es donde se dibuja. Un disco desenchufado mientras alguien está en Ajustes habría
seguido diciendo «Disponible» hasta pasar por la Biblioteca y volver.

Se comprobó que la guarda ve de verdad, revirtiendo el arreglo y volviendo a correrla:

```
ShellAssemblyTests.Walking_into_settings_reads_the_folder_list_again [FAIL]
walking into Settings never asked the repository for the folder list, so the list on screen is
whatever the last route left there.
```

### Lo que el prototipo contestó, y cambió la forma del arreglo

Se iba a declarar una clase de estilo propia para los dos distintivos de la lista. La consulta al
prototipo lo desaconsejó con tres datos:

| Lo que el prototipo hace | Medido en |
| --- | --- |
| Tiene **tres** estados de raíz: «Conectada», «Desconectada», «Acceso denegado» | `rootState(r)` |
| Dibuja los tres con el mismo elemento, radio **999** — una píldora | `tag(tone)` |
| Usa el mismo tono rojo para los dos fallos; lo que cambia es la palabra | `tone: 'err'` |

Ese elemento **ya está emparejado** en el árbol con `Border.state-chip`, así que el distintivo nuevo
nació sobre la clase existente y el verde de al lado se mudó a ella. El trinquete de esquinas escritas
en las vistas **bajó de 80 a 79**: la puerta convirtió un distintivo nuevo en la ocasión de emparejar
el viejo, que es para lo que sirve un trinquete que sólo baja.

**El rojo es del diseño y no una invención**: el prototipo pinta los dos fallos en `err`. Lo que esta
aplicación mantiene distinto es la insignia compartida de «no disponible», que es ámbar por una
decisión escrita en su propio archivo — algo que no está a mano no es un error, es algo que no está.

### El verde

| Suite | Resultado |
| --- | --- |
| `Domain.Tests` | 743 de 743 |
| `Application.Tests` | 353 de 353 |
| `ArchitectureTests` | 39 de 39 |
| `UiTests` | 1.233 de 1.233 |
| `IntegrationTests` | 563 de 563, 3 omitidas |
| `AccessibilityTests` | 149 de 149 |

`preview-coverage-floors.ps1` sobre esas cinco suites: **465 archivos medidos, ningún suelo se mueve
y ningún archivo nuevo se queda corto**.

### Lo que esto cuesta a quien mantiene

Añadir una operación al contrato de carpetas obligó a tocar **trece dobles** repartidos por cuatro
suites. Es el precio de un puerto que se ensancha, y se paga entero en el mismo cambio.

---

## English

### The red, measured before anything was touched

`LIB-010` is `VERIFIED` and promises, in those words, that a disconnected drive «shows unavailable».
The settings folder list said **«Available» always**.

The test that already mounts and takes away a real drive — `subst`, asserting the folder is gone
before scanning — only looked at the **files**. The root's own row was added to it:

```
RemovedDriveTests.A_drive_that_is_taken_away_keeps_the_catalogue_and_comes_back_without_duplicates [FAIL]
Assert.Equal() Failure: Values differ
Expected: Unavailable
Actual:   Available
```

### Where the chain was cut, and why the earlier evidence never saw it

The column has been in the schema since migration 0002, **with its three-value CHECK already there**,
and only the `INSERT` that creates a root ever wrote it — always as available. **No operation updated
it afterwards**: the root repository's contract had four methods and none wrote availability.

What a scan did update was the availability of **each file**, which is another table and another
badge. The evidence that passed `LIB-010` — `T8` — measured exactly that: the files' `IsAvailable`.
**The chain was verified to the data and not to the screen**, which is the same lesson this
repository already wrote on 2026-08-15 and again on 2026-09-04.

### The third state was one line from existing

The domain has declared `Available`, `Unavailable` and `AccessDenied` all along, and the scan **already
told the two failures apart**: it looks at whether the root path's error code is `AccessDenied` or
`IoError`. It then collapsed them into a `bool`, and the third died there.

**This is not cosmetic.** A drive that is not there is fixed by plugging it in; a folder Windows
refuses is exactly where it was, and saying «unavailable» sends somebody to look for a cable that is
already plugged in.

### The second cut, behind the first

Fixing the write was not enough: **the list was only re-read on entering the Library**, not on
entering Settings, which is where it is drawn. A drive unplugged while somebody sat in Settings would
have kept saying «Available» until they walked through the Library and came back.

The guard was shown to see, by reverting the fix and running it again:

```
ShellAssemblyTests.Walking_into_settings_reads_the_folder_list_again [FAIL]
walking into Settings never asked the repository for the folder list, so the list on screen is
whatever the last route left there.
```

### What the prototype answered, and how it changed the fix

A style class of its own was about to be declared for the list's two chips. Asking the prototype
argued against it with three facts:

| What the prototype does | Measured in |
| --- | --- |
| It has **three** root states: «Connected», «Disconnected», «Access denied» | `rootState(r)` |
| It draws all three with the same element, radius **999** — a pill | `tag(tone)` |
| It uses the same red tone for both failures; the word is what differs | `tone: 'err'` |

That element is **already paired** in the tree with `Border.state-chip`, so the new chip was born on
the existing class and the green one beside it moved onto it. The ratchet of corners written in views
**fell from 80 to 79**: the gate turned a new chip into the occasion to pair the old one, which is
what a ratchet that only falls is for.

**The red is the design's rather than an invention**: the prototype paints both failures in `err`.
What this application keeps different is the shared «unavailable» badge, amber by a decision written
in its own file — something out of reach is not an error, it is something that is not here.

### Green

| Suite | Result |
| --- | --- |
| `Domain.Tests` | 743 of 743 |
| `Application.Tests` | 353 of 353 |
| `ArchitectureTests` | 39 of 39 |
| `UiTests` | 1,233 of 1,233 |
| `IntegrationTests` | 563 of 563, 3 skipped |
| `AccessibilityTests` | 149 of 149 |

`preview-coverage-floors.ps1` over those five suites: **465 files measured, no floor moves and no new
file falls short**.

### What this costs whoever maintains it

Adding one operation to the root contract forced **thirteen test doubles** across four suites to be
touched. That is the price of widening a port, and it is paid in full in the same change.
