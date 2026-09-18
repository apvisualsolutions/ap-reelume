# LIB-021, plan 1 de 3: la portada elegida a mano vive aparte de la del proveedor

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** que la portada elegida a mano se guarde en su propio campo y gane por orden, de modo que refrescar desde el proveedor —incluso «restaurar campos del proveedor»— no pueda borrarla ni dejar su archivo huérfano.

**Architecture:** `EditableMetadata` gana `PersonalCover` (sólo el nombre de archivo, 64 hex + extensión aprobada). Una política de dominio, `CoverOrderPolicy`, fija el orden de orígenes (hoy `Personal` > `Provider`); `ResolveTitlePoster` la recorre. SQLite gana la columna `personal_cover` (migración 24) y el repositorio mueve al leer el valor heredado que `poster_path` guardaba con forma de portada propia. `MetadataMergePolicy` no toca `PersonalCover`, y eso es el arreglo.

**Tech Stack:** C# .NET 10, Avalonia 12, SQLite `STRICT` con migraciones SQL embebidas y verificadas por SHA-256, xUnit v3.

**Spec:** `docs/adr/0009-a-cover-has-three-origins-and-an-order.md`, fila `LIB-021` de `docs/FEATURES.md`.

**Los otros dos planes**, que se escriben cuando éste esté verde en CI: **2 —** el tercer origen, el fotograma, conectando `ICourseFrameGrabber`/`LibVlcCourseFrameGrabber` (hoy sin registrar) para película y serie con su plazo propio; **3 —** el ajuste general del orden con su «Restaurar valores por defecto» (regla 11, `OptionGroupTests.Groups`) y la galería por título del prototipo en `MetadataEditorView`.

## Global Constraints

- Cabecera SPDX `LicenseRef-APSolutions` en todo `.cs` nuevo (`IDE0073`).
- Código e identificadores en inglés; comentarios, commits y documentos en español; documentos públicos en los dos idiomas.
- Pruebas: `-c Release -m:1 --settings eng/test.runsettings`; la suite afectada es **quien lee** el archivo.
- La migración nueva es la **24** (la 23 es `picture_adjustment`); el manifiesto lleva su SHA-256 del texto UTF-8.
- `LIB-011` verificó la caja de texto de la ruta de portada: se conserva.
- Nada se registra sin quien lo consuma (el defecto de la casa).
- Cobertura: archivos nuevos 96/96; `eng/preview-coverage-floors.ps1` antes de empujar.

---

### Task 1: el campo y el orden, en el dominio

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Domain/Metadata/MetadataMergePolicy.cs` (record `EditableMetadata`, línea 23)
- Create: `src/ApSolutions.LocalMedia.Domain/Metadata/CoverOrderPolicy.cs`
- Test: `tests/ApSolutions.LocalMedia.Domain.Tests/Metadata/MetadataMergePolicyTests.cs`
- Test: `tests/ApSolutions.LocalMedia.Domain.Tests/Metadata/CoverOrderPolicyTests.cs`

**Interfaces:**
- Produces: `EditableMetadata.PersonalCover { get; init; }` (`string?`, por defecto `null`); `enum CoverOrigin { Personal, Provider }`; `CoverOrderPolicy.Default : IReadOnlyList<CoverOrigin>`.

- [ ] **Step 1: prueba que falla — un refresco no toca la portada propia, ni siquiera sin candados**

```csharp
[Fact]
public void A_refresh_never_touches_the_hand_picked_cover_even_with_nothing_locked()
{
    var current = Sample() with { PersonalCover = new string('a', 64) + ".png", LockedFields = new HashSet<MetadataField>() };
    var remote = RemoteWith(posterPath: "/fromProvider.jpg");

    var merged = new MetadataMergePolicy().Merge(current, remote);

    Assert.Equal(new string('a', 64) + ".png", merged.PersonalCover);
    Assert.Equal("/fromProvider.jpg", merged.PosterPath);
}
```

(`Sample()` y `RemoteWith(...)`: usar los ayudantes que ya tenga el archivo; si no existen, construir `EditableMetadata` y `MetadataDetails` a mano como hacen las pruebas vecinas.)

- [ ] **Step 2:** correr `--filter "FullyQualifiedName~A_refresh_never_touches_the_hand_picked_cover"` en `Domain.Tests`. Esperado: no compila (`PersonalCover` no existe).
- [ ] **Step 3: el campo.** En `EditableMetadata`, cuerpo del record:

```csharp
    /// <summary>
    /// The file name of the cover somebody picked from their own disk, apart from the provider's
    /// <see cref="PosterPath"/> (ADR-0009). <see cref="MetadataMergePolicy"/> never assigns it, which
    /// is the whole fix: a refresh — even one that restores every provider field — cannot reach it.
    /// </summary>
    public string? PersonalCover { get; init; }
```

- [ ] **Step 4:** la prueba pasa (el `with` del merge copia el campo). Correr la suite entera de `Domain.Tests`.
- [ ] **Step 5: prueba que falla — el orden por defecto.**

```csharp
public sealed class CoverOrderPolicyTests
{
    [Fact]
    public void The_hand_picked_cover_wins_and_the_provider_follows() =>
        Assert.Equal([CoverOrigin.Personal, CoverOrigin.Provider], CoverOrderPolicy.Default);

    [Fact]
    public void Every_origin_appears_once() =>
        Assert.Equal(Enum.GetValues<CoverOrigin>().Length, CoverOrderPolicy.Default.Distinct().Count());
}
```

- [ ] **Step 6: implementación mínima**

```csharp
// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Metadata;

/// <summary>Where a cover came from (ADR-0009). The frame taken from the video joins in plan 2.</summary>
public enum CoverOrigin
{
    Personal,
    Provider,
}

/// <summary>
/// The order in which a title's covers are asked for. The first origin that has a file on disk
/// draws; the general setting and the per-title override of plan 3 replace this list, never the
/// loop that walks it.
/// </summary>
public static class CoverOrderPolicy
{
    public static IReadOnlyList<CoverOrigin> Default { get; } = [CoverOrigin.Personal, CoverOrigin.Provider];
}
```

- [ ] **Step 7:** `Domain.Tests` verde. Commit: `feat: LIB-021, la portada propia tiene su campo y el orden su política`.

### Task 2: `ResolveTitlePoster` recorre el orden

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Application/Metadata/ResolveTitlePoster.cs`
- Test: `tests/ApSolutions.LocalMedia.Application.Tests/Metadata/ResolveTitlePosterTests.cs`

**Interfaces:**
- Consumes: `CoverOrderPolicy.Default`, `CoverOrigin`.
- Produces: `string? Find(TitleId titleId, string? posterPath, string? personalCover = null)`.

- [ ] **Step 1: pruebas que fallan** (usar el `StubStore` del archivo):

```csharp
[Fact]
public void The_hand_picked_cover_wins_over_the_provider_when_both_are_on_disk()
{
    var store = new StubStore { RemoteAnswer = "cache/artwork/abc/poster.jpg", PersonalAnswer = "personal-artwork/abc/cover.png" };

    var found = new ResolveTitlePoster(store).Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg", new string('a', 64) + ".png");

    Assert.Equal("personal-artwork/abc/cover.png", found);
}

[Fact]
public void The_provider_draws_when_the_hand_picked_file_is_gone()
{
    var store = new StubStore { RemoteAnswer = "cache/artwork/abc/poster.jpg", PersonalAnswer = null };

    var found = new ResolveTitlePoster(store).Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg", new string('a', 64) + ".png");

    Assert.Equal("cache/artwork/abc/poster.jpg", found);
}

[Fact]
public void A_personal_field_that_is_not_a_cover_name_is_never_read_as_a_path()
{
    var store = new StubStore { PersonalAnswer = "anything" };

    Assert.Null(new ResolveTitlePoster(store).Find(Title, posterPath: null, @"C:\Windows\win.ini"));
    Assert.Equal(0, store.PersonalCalls);
}
```

- [ ] **Step 2:** correrlas; esperado: no compila (falta el tercer parámetro).
- [ ] **Step 3: implementación**

```csharp
public string? Find(TitleId titleId, string? posterPath, string? personalCover = null)
{
    foreach (var origin in CoverOrderPolicy.Default)
    {
        var file = origin switch
        {
            CoverOrigin.Personal => FindPersonal(titleId, personalCover) ?? FindPersonal(titleId, posterPath),
            CoverOrigin.Provider => PosterAddressPolicy.TryBuildPosterAddress(posterPath) is { } address
                ? _artwork.Find(titleId, new Uri(address, UriKind.Absolute))
                : null,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null),
        };
        if (file is not null)
        {
            return file;
        }
    }

    return null;
}

private string? FindPersonal(TitleId titleId, string? value) =>
    PersonalCoverPathPolicy.TryGetCoverFileName(value) is { } cover ? _artwork.FindPersonal(titleId, cover) : null;
```

El `FindPersonal(titleId, posterPath)` sostiene las filas heredadas que el repositorio aún no ha migrado al leer; reescribir el comentario `<remarks>` de la clase: el orden ya no es «el proveedor primero» sino el de `CoverOrderPolicy`, y decirlo.

- [ ] **Step 4:** `Application.Tests` entera verde; las once pruebas previas siguen pasando.
- [ ] **Step 5:** Commit: `feat: LIB-021, la portada se elige recorriendo el orden y la propia gana`.

### Task 3: la columna, la migración 24 y el traslado de lo heredado

**Files:**
- Create: `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0024_personal_cover.sql`
- Modify: `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`
- Modify: `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/CatalogMetadataRepository.cs`
- Test: `tests/ApSolutions.LocalMedia.IntegrationTests/Catalog/CatalogMetadataRepositoryTests.cs`
- Test: `tests/ApSolutions.LocalMedia.IntegrationTests/Data/MigratedSchemaTemplateTests.cs` (el nombre `..._twenty_four` pasa a `..._twenty_five`), y cualquier prueba que cuente migraciones (`grep -rn "23\b" tests/ApSolutions.LocalMedia.IntegrationTests/Data`).

**Interfaces:**
- Consumes: `EditableMetadata.PersonalCover`, `PersonalCoverPathPolicy.TryGetCoverFileName`.
- Produces: columna `catalog_metadata.personal_cover TEXT NULL`.

- [ ] **Step 1: pruebas que fallan**

```csharp
[Fact]
public async Task A_hand_picked_cover_survives_a_save_of_the_provider_poster()
{
    // guardar con PersonalCover = hex64.png y PosterPath = "/a.jpg"; releer; guardar otra vez
    // con PosterPath = "/b.jpg" y el PersonalCover leído; releer.
    // Esperado: PersonalCover == hex64.png y PosterPath == "/b.jpg".
}

[Fact]
public async Task A_cover_stored_the_old_way_is_read_as_hand_picked()
{
    // insertar por SQL una fila con poster_path = "C:\\x\\personal-artwork\\<id>\\" + hex64 + ".png"
    // y personal_cover NULL; GetAsync.
    // Esperado: PersonalCover == hex64 + ".png" y PosterPath == null.
}
```

Escribir el cuerpo con los ayudantes de base de datos que ya usa el archivo (misma fábrica y mismo `TitleId` de siembra).

- [ ] **Step 2:** correrlas; esperado: rojo (columna inexistente / campo sin leer).
- [ ] **Step 3: la migración**

```sql
-- The cover somebody picked from their own disk, apart from the provider's (LIB-021, ADR-0009).
--
-- One column held both until now, and that is the defect this closes: choosing a cover overwrote the
-- provider's, and restoring the provider's fields overwrote the choice and orphaned its file in
-- personal-artwork. Apart, a refresh has no way to reach it.
--
-- NULL for every existing row. A row that stored a personal cover the old way keeps it in
-- poster_path, and the repository moves it on read with PersonalCoverPathPolicy — the one place
-- that knows what a personal cover name looks like, instead of a second copy of that rule in SQL.
ALTER TABLE catalog_metadata ADD COLUMN personal_cover TEXT NULL;
```

Añadir la entrada 24 al manifiesto con `(Get-FileHash -Algorithm SHA256 <archivo>).Hash`, y comprobar antes que el `.sql` está en LF y UTF-8 sin BOM, como los demás, porque el hash es del texto.

- [ ] **Step 4: el repositorio.** `Columns` gana `personal_cover` al final; el `INSERT` gana `$personal` y `personal_cover = excluded.personal_cover`; el parámetro es `(object?)metadata.PersonalCover ?? DBNull.Value`. En `Read`, índice 14:

```csharp
var stored = reader.IsDBNull(6) ? null : reader.GetString(6);
var personal = reader.IsDBNull(14) ? null : reader.GetString(14);
// Stored the old way: the choice lived in poster_path. Moved here so the next save writes it apart.
if (personal is null && PersonalCoverPathPolicy.TryGetCoverFileName(stored) is { } legacy)
{
    personal = legacy;
    stored = null;
}
```

y construir `EditableMetadata(... stored ...) { PersonalCover = personal }`.

- [ ] **Step 5:** `IntegrationTests` filtrada a `CatalogMetadataRepositoryTests|Migration|Schema` verde; renombrar `..._twenty_four` a `..._twenty_five` con su cuerpo si cuenta archivos.
- [ ] **Step 6:** Commit: `feat: LIB-021, la portada propia tiene su columna y lo guardado a la antigua se traslada al leer`.

### Task 4: elegir una portada ya no pisa la del proveedor

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Application/Metadata/UpdateMetadata.cs` (`MetadataFieldChanges` y `ExecuteAsync`)
- Modify: `src/ApSolutions.LocalMedia.Presentation/Metadata/MetadataEditorViewModel.cs` (`OnPickerChanged` 200-211, `SaveAsync` 124-144, `ApplyCatalog` 213-236)
- Test: `tests/ApSolutions.LocalMedia.Application.Tests/Metadata/UpdateMetadataTests.cs` (o donde vivan las pruebas de `UpdateMetadata`: `grep -rln "new UpdateMetadata(" tests`)
- Test: `tests/ApSolutions.LocalMedia.UiTests/Metadata/MetadataEditorTests.cs`, `ChooseCoverTests.cs`
- Test: la prueba de `RefreshMetadata` con `RestoreProviderFields` (`grep -rln "RestoreProviderFields" tests`)

**Interfaces:**
- Produces: `MetadataFieldChanges.PersonalCover` (`string?`, init, por defecto `null` = sin cambio); `MetadataEditorViewModel.PersonalCover`.

- [ ] **Step 1: pruebas que fallan**
  - `UpdateMetadata`: un comando con `PersonalCover = hex64.png` lo guarda y deja `PosterPath` como estaba.
  - `RefreshMetadata` con `RestoreProviderFields = true` sobre una fila con `PersonalCover` y `PosterPath` propios: tras el refresco `PersonalCover` sigue y `PosterPath` es el del proveedor. **Ésta es la prueba del defecto que cierra la ADR-0009.**
  - `MetadataEditorTests`: elegir una portada pone `PersonalCover` al nombre del archivo y **no** cambia `PosterPath` ni `LockPosterPath`.
- [ ] **Step 2:** rojo.
- [ ] **Step 3:** `MetadataFieldChanges` gana `public string? PersonalCover { get; init; }`; `ExecuteAsync` añade `PersonalCover = changes.PersonalCover ?? current.Metadata.PersonalCover` al `with`. En el editor, `OnPickerChanged` hace `PersonalCover = PersonalCoverPathPolicy.TryGetCoverFileName(ArtworkPicker.SelectedPersonalPath)` y nada más; `SaveAsync` pasa `new MetadataFieldChanges(...) { PersonalCover = PersonalCover }`; `ApplyCatalog` hace `PersonalCover = catalog.Metadata.PersonalCover`. Reescribir las pruebas existentes que afirmaban `PosterPath == ruta elegida` y `LockPosterPath == true`: afirmaban el comportamiento defectuoso, y se dice en su comentario.
- [ ] **Step 4:** `Application.Tests` y `UiTests` verdes.
- [ ] **Step 5:** Commit: `fix: LIB-021, elegir portada ya no pisa la del proveedor y restaurar ya no la borra`.

### Task 5: la cuadrícula y las fichas dibujan por el orden

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Application/Catalog/CatalogQueries.cs:54` (`CatalogItem` gana `string? PersonalCover = null` al final)
- Modify: `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/CatalogRepository.cs:266-300` (subselect `m.personal_cover`, `NULL` en la rama de escaneados, columna en el `SELECT` final y en la lectura)
- Modify: `src/ApSolutions.LocalMedia.Presentation/Library/LibraryViewModel.cs:25,54-58,520` (`Func<TitleId, string?, string?, string?> findPoster`, llamada con `item.PersonalCover`)
- Modify: `src/ApSolutions.LocalMedia.Windows/CompositionRoot.cs:610,652,687,715-716` (pasar `stored?.Metadata.PersonalCover`)
- Test: `tests/ApSolutions.LocalMedia.IntegrationTests` — la prueba del catálogo que ya lee `PosterPath` (`grep -rln "PosterPath" tests/ApSolutions.LocalMedia.IntegrationTests/Catalog`) gana la lectura de `PersonalCover`.
- Test: `tests/ApSolutions.LocalMedia.UiTests/Library/PosterArtTests.cs` — una tarjeta con las dos portadas dibuja la propia.

- [ ] **Step 1:** pruebas que fallan (las dos de arriba).
- [ ] **Step 2:** rojo.
- [ ] **Step 3:** los cambios de la lista; el comentario del subselect dice que trae las dos columnas y que cuál gana lo decide `ResolveTitlePoster`, no la consulta.
- [ ] **Step 4:** verdes `IntegrationTests` (catálogo), `UiTests` y `AccessibilityTests` (monta la aplicación entera: **quien lee** `CompositionRoot`).
- [ ] **Step 5:** Commit: `feat: LIB-021, la cuadrícula y las fichas dibujan la portada por su orden`.

### Task 6: evidencia, matriz y cierre del plan

- [ ] Evidencia bilingüe `docs/evidence/stable/LIB021-cover-origins-storage.md`: el defecto reproducido (prueba de `RestoreProviderFields` roja antes, verde después), la migración y el traslado heredado con su control.
- [ ] `docs/FEATURES.md`: `LIB-021` pasa al estado de la leyenda que corresponde a «construido en parte» y enlaza la evidencia; los dos planes que faltan se nombran.
- [ ] `docs/CHANGELOG.{es,en}.md`, sección «Corregido»: elegir una portada ya no pisa la del proveedor y restaurar los campos del proveedor ya no la borra.
- [ ] `eng/preview-coverage-floors.ps1 -Suites Domain.Tests,Application.Tests,IntegrationTests,UiTests`; ningún archivo nuevo bajo 96/96.
- [ ] Puertas (`dotnet format`, build `-warnaserror`, suites afectadas, `verify-docs.ps1`), commit, push, `eng/watch-ci.ps1`, y fast-forward de `main` con la conclusión leída.
