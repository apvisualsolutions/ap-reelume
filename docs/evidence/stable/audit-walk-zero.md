# Cero pendientes / Zero pending

Los tres últimos controles del inventario, pulsados con el ratón. **128 de 128.** / The last three
controls in the inventory, pressed with a mouse. **128 of 128.**

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 125 | **128** |
| Pendientes / Pending | 3 | **0** |

```
The walk: 129 declared command controls in 128 identities; 128 pressed, 0 pending.
```

**El trinquete de `eng/check-walk-coverage.ps1` es 0 y no vuelve a subir.** `eng/walk-pending.txt`
queda vacío, con la regla escrita dentro: un control añadido a partir de aquí llega con su escena en
el mismo cambio. / The ratchet is 0 and does not go up again.

Desde el primer recuento: **126 → 0**, en dieciséis tandas.

## El defecto: el tráiler local sólo existía para películas duplicadas / Only duplicated films had one

La ficha busca el tráiler que está junto a la película en el disco. Lo hacía así:

```csharp
private static string? FindTrailer(MediaVersionGroup? versions)
{
    if (versions?.Versions.FirstOrDefault(v => v.IsAvailable)?.Path is not { } path) return null;
```

Y **`GroupMediaVersions` rechaza menos de dos versiones** —«At least two versions are required»—, así
que **una película sin duplicado no tiene grupo**, y sin grupo no hay ruta, y sin ruta no hay tráiler.
El descubrimiento por nombre estaba bien y probado; lo que le llegaba era `null` en el caso normal. /
The grouping use case refuses fewer than two versions, so a film with no duplicate has no group — and
the path the trailer policy needed came from that group.

Medido con la película y su hermano `-trailer` en el disco: `HasTrailer` falso. La corrección es que
`FindTrailer` **toma la ruta de la película**, del grupo cuando lo hay y de su propia fila cuando no,
que es el caso ordinario.

**Esto se descubrió porque la cola decía lo contrario.** La sesión anterior había «corregido» la nota
que exigía un grupo de versiones, razonando que `TrailerDiscoveryPolicy` busca por nombre — cierto, y
sin embargo equivocado, porque olvidaba de dónde salía la **ruta**. La nota vieja tenía razón por un
motivo que nadie había escrito. / The queue had been "corrected" the other way, and the old note was
right for a reason nobody had written down.

## Lo que las dos escenas prueban / What the two scenes prove

| Control | Efecto afirmado / Asserted effect |
|---|---|
| Continuar / Resume | la sesión abre **en el segundo guardado**, leído del motor / the session opens at the stored second |
| Reproducir desde el principio / Play from the start | con progreso guardado, el cabezal queda **en 0** / with progress stored, the playhead is at 0 |
| Ver el tráiler / Trailer | reproduce **el archivo `-trailer`** como sesión suelta, y **sin fila nueva en el catálogo** / it plays the `-trailer` file as a loose session, with no catalogue row |
| Reproducir episodio / Episode row | abre **el episodio que la fila nombra**, desempatado por su etiqueta `S01E02` / it opens the episode its row names |

El segundo cierra el hallazgo que dejó el cambio de versión: hasta que la posición pedida empezó a
mandar, el anfitrión la recalculaba del almacén y «desde el principio» habría reanudado. Ahora está
medido. / The second closes the finding the version switch left behind.

## Una predicción que se midió y NO se cumplió / A prediction measured, and refuted

Estaba escrito que la fila de acciones de la ficha —`StackPanel` horizontal con una etiqueta de
anchura libre entre botones— sacaría un control fuera de la ventana, séptima vez. **Se midió antes de
pulsar, con los cinco controles visibles a la vez, y no se sale.** La condición de las seis anteriores
no era la forma sola: era la forma **en un contenedor estrecho** —la columna de 320 px del reproductor,
o una fila con un dato largo—, y la ficha ocupa la columna ancha. La comprobación se queda en la
escena de todos modos: cuesta cuatro líneas y el rediseño va a mover esa fila. / The prediction was
measured with all five controls on screen and refuted: the six earlier cases needed the shape **in a
narrow container**, and the card is not one. The check stays in the scene anyway.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn                      # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
dotnet test …AccessibilityTests                                        # 117 / 117
eng/check-walk-coverage.ps1                                            # 128 pulsados, 0 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/check-walk-coverage.ps1
```
