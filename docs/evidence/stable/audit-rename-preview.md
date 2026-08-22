# El truncado se comía la única parte que distingue una ruta de otra / The truncation was eating the one part that tells two paths apart

Cuarto trabajo del tramo 6 de la §4, **y el que por fin declara la familia monoespaciada** — porque
ahora hay tres vistas que la gastan y no una. / §4's sixth tranche, and the piece that finally declares
the fixed-width family, because three views spend it now instead of one.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El defecto, que la fila no nombra pero la §4 razona en otro sitio / The defect the row does not name

Las dos rutas de un renombrado se truncaban con `TextTrimming="CharacterEllipsis"`, que **se come el
final**. Y el final de un renombrado es **el nombre del archivo**, que es exactamente la parte que
cambia: / Character trimming eats the end, and the end of a rename is the file name — the only part
that differs:

```
R:\media\Arrival.mkv          →  R:\media\Arrival (2016).mkv
R:\media\muy\larga\ruta\Arr…  →  R:\media\muy\larga\ruta\Arr…      ← lo que se veía
```

Una pantalla que existe **para comparar dos rutas antes de tocar los archivos de alguien** estaba
escondiendo la diferencia. Medido el 2026-08-22, esta Avalonia ofrece seis modos de truncado —`None`,
`CharacterEllipsis`, `WordEllipsis`, `PrefixCharacterEllipsis`, `LeadingCharacterEllipsis` y
`PathSegmentEllipsis`— y el último está hecho a medida: **quita segmentos del medio y conserva los dos
extremos**. / This Avalonia offers six trimming modes and the last is purpose-built for paths.

**La §4 razona esto mismo en otra fila**, la del asistente de restauración: «la ruta truncada por la
izquierda (**las rutas se distinguen por el final**)». El razonamiento vale aquí y la fila no lo dice. /
§4 reasons exactly this in another row and does not say it in this one.

## La familia monoespaciada, y por qué ahora sí / The fixed-width family, and why now

`FontFamilyMono` se declara por fin. **Lo que se rechazó antes era `FontSizeMono`**, un *tamaño* con un
solo lector, y sigue rechazado: las tres superficies de ancho fijo leen a tamaño de cuerpo y ninguna
pide uno propio. La **familia** es otro token, y ahora la gastan tres vistas: el volcado de
diagnósticos que alguien lee antes de compartirlo, las dos rutas de un renombrado, y las cifras que
distinguen un duplicado de otro. / What was refused before was `FontSizeMono`, a size with one reader;
a family with three is a different thing.

Un token con un solo lector es el defecto que este repositorio nombra, y por eso vivía como literal en
`DiagnosticsPreviewView` hasta hoy. **La regla no es «¿tiene sentido el token?» sino «¿lo gasta el
árbol?»** — el mismo criterio con el que entró `Space12`. / A token with one reader is the defect this
repository names; the criterion is whether the tree spends it.

Se declaran **tres familias en una**: `Consolas` viene con Windows, `Cascadia Mono` es la del terminal,
y el nombre genérico es a lo que cae un anfitrión sin ninguna de las dos.

## Y la flecha, que era un carácter y nada más / And the arrow, which was a character and nothing else

El `→` entre las dos rutas **no tenía nombre accesible**. Se queda como símbolo —es lo que hace este
árbol con `○ ◐ ●`, `⚠` y `✓`— y gana las palabras detrás, que es la mitad que recibe un lector de
pantalla. Sin ellas la fila se lee como dos rutas y un carácter. / The arrow stays a symbol and gains
the words behind it.

## Lo que vino de paso / What came along

El título pasa a **nivel 2 y `FontSizeSubtitle`** —no tenía nivel ninguno y es una sección del panel de
la biblioteca, cuyo nivel 1 es de `LibraryView`— y la fila de acciones a `WrapPanel`.

## ⚠ Lo que queda anotado y NO entra aquí / What is written down and does not go in here

**`RenameConflict.Detail` llega a la pantalla en crudo, y lo que lleva es una de dos cosas:** una frase
en inglés escrita dentro de `SafeFileRenamer` —«Rename paths are not normalized and confined.»— o **una
ruta pelada**. Y `RenameConflictKind` —`UnsafeState`, `SourceMissing`, `DestinationExists`,
`DuplicateDestination`—, que es **el significado**, no lo pinta nadie: el detalle es la prueba, el tipo
es la respuesta. Es la misma familia que los códigos de identificación y cuesta lo mismo: cuatro
cadenas por idioma y una clave por tipo. Va en su propia pieza. / Written down rather than squeezed in.

## El verde / The green

```
UiTests             684/684
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```
