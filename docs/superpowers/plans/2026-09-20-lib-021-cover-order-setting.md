# LIB-021, plan 3 de 3: el orden de portadas se cambia, en general y en un título

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** que quien usa la aplicación pueda cambiar de qué sitio sale la portada —la elegida a mano, la del proveedor o un fotograma del vídeo— para toda la biblioteca desde Ajustes, y saltarse ese orden en un título concreto desde su editor.

**Architecture:** `CoverOrderPolicy` deja de ser una lista fija y gana tres funciones puras: `Normalize` (una lista cualquiera se convierte en una válida, o vuelve a la de por defecto), `Format` y `TryParse` (texto ↔ lista, que es como viaja a SQLite). El orden general vive en el almacén de ajustes por el puerto `ICoverOrderSettings`, con el molde de `StoredLifecycleSettings`. La excepción por título es una columna nueva, `cover_order` (migración 25), que guarda la lista entera normalizada y `NULL` cuando no hay excepción. `ResolveTitlePoster.Find` gana un cuarto parámetro con el orden a recorrer y normaliza lo que le den; quien decide cuál es el orden efectivo es `CompositionRoot`, que pasa la excepción del título si la hay y el general si no. En Ajustes nace una sección propia, `Covers`, porque `Library` ya aloja un grupo de opciones y la puerta prohíbe dos en el mismo destino.

**Tech Stack:** C# .NET 10, Avalonia 12, SQLite (esquema `STRICT`, migraciones con SHA-256), xUnit v3.

**Spec:** [`docs/adr/0009-a-cover-has-three-origins-and-an-order.md`](../../adr/0009-a-cover-has-three-origins-and-an-order.md), decisión 4 («la elección se puede cambiar en dos sitios») y el párrafo «los dos mecanismos no son redundancia». Planes anteriores: [`2026-09-18-lib-021-cover-origins-storage.md`](2026-09-18-lib-021-cover-origins-storage.md) y [`2026-09-18-lib-021-cover-origins-frame.md`](2026-09-18-lib-021-cover-origins-frame.md). Fila de alcance: `LIB-021`. Tarea que cierra: `ENG-003`.

## Decisiones tomadas aquí, con su porqué

- **La excepción por título es un desplegable de cuatro opciones, no un segundo reordenador.** «El orden general» más los tres orígenes; elegir uno lo pone delante y el resto conserva el orden general. Es lo que el ADR pide —«saltárselo en un título concreto»— con un solo control, y evita que la misma persona tenga que aprender dos formas de decir lo mismo. Un reordenador por título habría duplicado seis controles en una pantalla que ya tiene once campos.
- **El ajuste general reordena con un `ListBox` y dos botones, no arrastrando.** Arrastrar no lo puede pulsar el paseo autónomo —el hit test headless no sigue un gesto— y no lo alcanza un teclado. Dos botones que actúan sobre el elemento seleccionado es además lo que Windows dibuja en sus propias listas de prioridad. `ListBox` con `ListBoxItem` es el patrón que el índice lateral de Ajustes ya usa y que el paseo ya sabe pulsar (`AssembledPhysicalWalkTests`, la escena de Recomendaciones).
- **La columna guarda la lista entera, no el origen elegido.** Un solo origen obligaría a recomponer el resto en cada lectura, y esa recomposición cambiaría de resultado el día que alguien mueva el orden general — la excepción de un título dejaría de significar lo que significaba cuando se puso. Guardar la lista la congela, que es lo que una excepción quiere decir.
- **`NULL` es «sin excepción» y una lista vacía en `MetadataFieldChanges` es «quítala».** `MetadataFieldChanges` usa `null` para «déjalo como está», así que sin el segundo centinela no habría forma de volver al orden general una vez puesta una excepción — el defecto que `PersonalCover` sí tiene y que aquí se evita desde el principio.
- **Sección propia en Ajustes, no una tarjeta dentro de Biblioteca.** `OptionGroupTests` falla si un destino aloja dos grupos, y `SettingsSection.Library` ya es de `Scanning`. Es la misma razón por la que `Language` se separó de `Appearance` el 2026-09-13.

## Global Constraints

- **Licencia por archivo**: todo fuente nuevo abre con `SPDX-FileCopyrightText: 2026 AP Solutions` y `SPDX-License-Identifier: LicenseRef-APSolutions`. Hay un hook que rechaza la escritura sin ella.
- **Código en inglés, texto de pantalla en español con acentos** y su pareja inglesa. Cadena visible nueva ⇒ las dos, en la misma línea de `Strings.es.axaml` y `Strings.en.axaml` (están alineados línea a línea).
- **Ninguna clave de traducción huérfana**: `OrphanedResourceTests` falla ante una clave que ninguna pantalla pide.
- **Ningún `.axaml` escribe un número que tenga token**: `FontSize*`, los cinco `Space*` y los dos `CornerRadius*` se referencian con `{DynamicResource}` (`ScalarTokenTests`).
- **Archivo nuevo: 96 % de líneas y 96 % de ramas, sin excepción.** Y la trampa medida el 2026-09-19: **la puerta SUMA las ramas de cada suite**, no toma «cubierta en cualquier sitio», así que las dos mitades de una rama tienen que cubrirse **dentro de la misma suite**.
- **`eng/preview-coverage-floors.ps1` calla sobre un archivo nuevo que aún no está en un commit** (`ENG-016`). Un archivo nuevo se mide a mano con `--collect:"XPlat Code Coverage;Format=json"` **antes** de empujar.
- **Ejecución de pruebas**: siempre `-m:1 --settings eng/test.runsettings`. El SDK no está en el `PATH`: `$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"`.
- **Todo servicio registrado tiene quien lo resuelva** (`ServiceConsumptionTests`): un registro sin consumidor es el defecto característico de este repositorio.
- **Regla 0**: antes de usar un tipo o una propiedad de Avalonia, `mcp__avalonia-docs__lookup_avalonia_api`. Si contesta «no results», eso también es un dato.

---

### Task 1: el dominio sabe normalizar y escribir un orden

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Domain/Metadata/CoverOrderPolicy.cs`
- Test: `tests/ApSolutions.LocalMedia.Domain.Tests/Metadata/CoverOrderPolicyTests.cs`

**Interfaces:**
- Consumes: `CoverOrigin` y `CoverOrderPolicy.Default` del plan 2.
- Produces:
  - `static IReadOnlyList<CoverOrigin> CoverOrderPolicy.Normalize(IReadOnlyList<CoverOrigin>? order)`
  - `static string CoverOrderPolicy.Format(IReadOnlyList<CoverOrigin> order)`
  - `static bool CoverOrderPolicy.TryParse(string? text, out IReadOnlyList<CoverOrigin> order)`
  - `static IReadOnlyList<CoverOrigin> CoverOrderPolicy.WithFirst(CoverOrigin first, IReadOnlyList<CoverOrigin>? rest)`

- [ ] **Step 1: escribir las pruebas que fallan**

```csharp
[Fact]
public void A_null_or_empty_order_falls_back_to_the_default()
{
    Assert.Equal(CoverOrderPolicy.Default, CoverOrderPolicy.Normalize(null));
    Assert.Equal(CoverOrderPolicy.Default, CoverOrderPolicy.Normalize([]));
}

[Fact]
public void An_order_missing_an_origin_gets_it_back_at_the_end()
{
    Assert.Equal(
        [CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider],
        CoverOrderPolicy.Normalize([CoverOrigin.Frame]));
}

[Fact]
public void A_repeated_origin_keeps_only_its_first_place()
{
    Assert.Equal(
        [CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame],
        CoverOrderPolicy.Normalize([CoverOrigin.Provider, CoverOrigin.Provider, CoverOrigin.Personal]));
}

[Fact]
public void A_complete_order_comes_back_untouched()
{
    IReadOnlyList<CoverOrigin> given = [CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal];
    Assert.Equal(given, CoverOrderPolicy.Normalize(given));
}

[Fact]
public void An_order_survives_the_round_trip_through_text()
{
    var text = CoverOrderPolicy.Format([CoverOrigin.Provider, CoverOrigin.Frame, CoverOrigin.Personal]);
    Assert.Equal("Provider,Frame,Personal", text);
    Assert.True(CoverOrderPolicy.TryParse(text, out var parsed));
    Assert.Equal([CoverOrigin.Provider, CoverOrigin.Frame, CoverOrigin.Personal], parsed);
}

[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
[InlineData("Provider,Nonsense")]
[InlineData("42")]
public void Text_that_does_not_name_origins_is_refused_and_yields_the_default(string? text)
{
    Assert.False(CoverOrderPolicy.TryParse(text, out var parsed));
    Assert.Equal(CoverOrderPolicy.Default, parsed);
}

[Fact]
public void Parsing_normalizes_what_it_reads()
{
    Assert.True(CoverOrderPolicy.TryParse("Frame", out var parsed));
    Assert.Equal([CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider], parsed);
}

[Fact]
public void Putting_one_origin_first_keeps_the_rest_in_the_order_given()
{
    Assert.Equal(
        [CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal],
        CoverOrderPolicy.WithFirst(CoverOrigin.Frame, [CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame]));
}

[Fact]
public void Putting_one_origin_first_with_no_rest_falls_back_to_the_default_order()
{
    Assert.Equal(
        [CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame],
        CoverOrderPolicy.WithFirst(CoverOrigin.Provider, null));
}
```

- [ ] **Step 2: verlas fallar**

```powershell
dotnet test tests/ApSolutions.LocalMedia.Domain.Tests -c Release -m:1 --settings eng/test.runsettings --filter "FullyQualifiedName~CoverOrderPolicyTests"
```

Esperado: no compila — `Normalize`, `Format`, `TryParse` y `WithFirst` no existen.

- [ ] **Step 3: escribir las cuatro funciones**

En `CoverOrderPolicy`, debajo de `Default`. El `<remarks>` de la clase pierde la frase que decía que el ajuste general «sustituirá esta lista»: ya no es futuro.

```csharp
    /// <summary>
    /// Turns any list into one that can be walked: every origin exactly once, in the order given,
    /// and the ones left out added at the end in the default order.
    /// </summary>
    /// <remarks>
    /// A stored order is repaired on the way out rather than handed to the application, which is
    /// what a hand-edited settings file or a row written by an older build would otherwise do: an
    /// order missing an origin would make a cover that exists unreachable, with nothing saying so.
    /// </remarks>
    public static IReadOnlyList<CoverOrigin> Normalize(IReadOnlyList<CoverOrigin>? order)
    {
        if (order is null || order.Count == 0) { return Default; }

        var seen = new List<CoverOrigin>(Default.Count);
        foreach (var origin in order)
        {
            if (Enum.IsDefined(origin) && !seen.Contains(origin)) { seen.Add(origin); }
        }

        foreach (var origin in Default)
        {
            if (!seen.Contains(origin)) { seen.Add(origin); }
        }

        return seen;
    }

    /// <summary>Writes an order as the text a column or a settings file holds.</summary>
    public static string Format(IReadOnlyList<CoverOrigin> order) =>
        string.Join(',', Normalize(order));

    /// <summary>
    /// Reads an order back. Returns <see langword="false"/> for anything that does not name
    /// origins, and <paramref name="order"/> is the default either way, so a caller that ignores
    /// the verdict still gets a list it can walk.
    /// </summary>
    public static bool TryParse(string? text, out IReadOnlyList<CoverOrigin> order)
    {
        order = Default;
        if (string.IsNullOrWhiteSpace(text)) { return false; }

        var parsed = new List<CoverOrigin>(Default.Count);
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse<CoverOrigin>(part, ignoreCase: false, out var origin) || !Enum.IsDefined(origin))
            {
                return false;
            }

            parsed.Add(origin);
        }

        if (parsed.Count == 0) { return false; }

        order = Normalize(parsed);
        return true;
    }

    /// <summary>
    /// The order a single title gets when somebody picks one origin for it: that origin wins and
    /// the rest keep the order they had, which is what makes the choice survive a later change to
    /// the general order.
    /// </summary>
    public static IReadOnlyList<CoverOrigin> WithFirst(CoverOrigin first, IReadOnlyList<CoverOrigin>? rest)
    {
        var order = new List<CoverOrigin>(Default.Count) { first };
        order.AddRange(Normalize(rest));
        return Normalize(order);
    }
```

- [ ] **Step 4: verlas pasar**

```powershell
dotnet test tests/ApSolutions.LocalMedia.Domain.Tests -c Release -m:1 --settings eng/test.runsettings --filter "FullyQualifiedName~CoverOrderPolicyTests"
```

Esperado: todas verdes. `Domain.Tests` entera en verde también, porque `ResolveTitlePosterTests` la usa.

- [ ] **Step 5: medir las ramas de este archivo antes de seguir**

```powershell
dotnet test tests/ApSolutions.LocalMedia.Domain.Tests -c Release -m:1 --settings eng/test.runsettings --collect:"XPlat Code Coverage;Format=json" --filter "FullyQualifiedName~CoverOrderPolicyTests"
```

Leer el JSON y comprobar que `CoverOrderPolicy.cs` no baja de 96/96. Si alguna rama queda sin tomar, el JSON la nombra con línea y offset: se cubre, no se rebaja.

- [ ] **Step 6: Commit**

```bash
git add src/ApSolutions.LocalMedia.Domain/Metadata/CoverOrderPolicy.cs tests/ApSolutions.LocalMedia.Domain.Tests/Metadata/CoverOrderPolicyTests.cs
git commit -m "feat: LIB-021, el orden de portadas se normaliza, se escribe y se vuelve a leer"
```

---

### Task 2: el orden general vive en los ajustes

**Files:**
- Create: `src/ApSolutions.LocalMedia.Application/Metadata/ICoverOrderSettings.cs`
- Create: `src/ApSolutions.LocalMedia.Infrastructure/Settings/StoredCoverOrderSettings.cs`
- Test: `tests/ApSolutions.LocalMedia.IntegrationTests/Settings/StoredCoverOrderSettingsTests.cs`
- Modify: `src/ApSolutions.LocalMedia.Windows/CompositionRoot.Backup.cs` (donde se registra `ISettingsStore` y sus vecinos `Stored*Settings`)

**Interfaces:**
- Consumes: `CoverOrderPolicy.Normalize`, `CoverOrigin`, `ISettingsStore.Read<T>/Write<T>`.
- Produces: `ICoverOrderSettings` con `IReadOnlyList<CoverOrigin> Current { get; }` y `void Save(IReadOnlyList<CoverOrigin> order)`; `StoredCoverOrderSettings(ISettingsStore store)`; clave `"covers.order"`.

- [ ] **Step 1: escribir las pruebas que fallan**

```csharp
public sealed class StoredCoverOrderSettingsTests
{
    [Fact]
    public void A_store_that_has_never_been_asked_gives_the_default_order()
    {
        var settings = new StoredCoverOrderSettings(new FakeStore());

        Assert.Equal(CoverOrderPolicy.Default, settings.Current);
    }

    [Fact]
    public void What_was_saved_comes_back()
    {
        var store = new FakeStore();
        var settings = new StoredCoverOrderSettings(store);

        settings.Save([CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal]);

        Assert.Equal(
            [CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal],
            new StoredCoverOrderSettings(store).Current);
    }

    [Fact]
    public void An_order_saved_short_is_repaired_on_the_way_in_and_on_the_way_out()
    {
        var store = new FakeStore();
        new StoredCoverOrderSettings(store).Save([CoverOrigin.Frame]);

        Assert.Equal(
            [CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider],
            new StoredCoverOrderSettings(store).Current);
    }

    [Fact]
    public void A_hand_edited_file_that_names_nothing_valid_gives_the_default_order()
    {
        var store = new FakeStore();
        store.Write("covers.order", "Nonsense");

        Assert.Equal(CoverOrderPolicy.Default, new StoredCoverOrderSettings(store).Current);
    }

    [Fact]
    public void Saving_nothing_is_refused_rather_than_stored()
    {
        var settings = new StoredCoverOrderSettings(new FakeStore());

        _ = Assert.Throws<ArgumentNullException>(() => settings.Save(null!));
    }
}
```

`FakeStore` es un `ISettingsStore` de diccionario; si ya existe uno en esa suite (`grep -rln ": ISettingsStore" tests`), se reutiliza en vez de escribir otro.

- [ ] **Step 2: verlas fallar.** Esperado: no compila, `StoredCoverOrderSettings` no existe.

- [ ] **Step 3: escribir el puerto y el adaptador**

`ICoverOrderSettings.cs`:

```csharp
/// <summary>
/// The order the covers of the whole library are looked for in, as the one using it left it.
/// </summary>
/// <remarks>
/// It is a port and not a call to the settings store because the order is a decision of
/// <see cref="CoverOrderPolicy"/>, and a store that hands back whatever it holds would let a
/// hand-edited file decide it instead.
/// </remarks>
public interface ICoverOrderSettings
{
    /// <summary>The stored order, always complete and walkable.</summary>
    IReadOnlyList<CoverOrigin> Current { get; }

    /// <summary>Stores an order, repaired first.</summary>
    void Save(IReadOnlyList<CoverOrigin> order);
}
```

`StoredCoverOrderSettings.cs`, con el molde exacto de `StoredLifecycleSettings`. El almacén guarda la lista como texto por `CoverOrderPolicy.Format`, no como una lista de enums: `JsonSettingsStore` la escribiría con `JsonStringEnumConverter`, pero un texto que se lee con `TryParse` deja el repair en una sola función y no en dos.

```csharp
public sealed class StoredCoverOrderSettings : ICoverOrderSettings
{
    private const string OrderKey = "covers.order";

    private readonly ISettingsStore _store;

    public StoredCoverOrderSettings(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public IReadOnlyList<CoverOrigin> Current =>
        CoverOrderPolicy.TryParse(_store.Read<string?>(OrderKey), out var order)
            ? order
            : CoverOrderPolicy.Default;

    public void Save(IReadOnlyList<CoverOrigin> order)
    {
        ArgumentNullException.ThrowIfNull(order);
        _store.Write(OrderKey, CoverOrderPolicy.Format(order));
    }
}
```

- [ ] **Step 4: registrarlo**, junto a los demás `Stored*Settings` de `CompositionRoot.Backup.cs`:

```csharp
        services.AddSingleton<ICoverOrderSettings, StoredCoverOrderSettings>();
```

- [ ] **Step 5: verlas pasar** y compilar la solución con `-warnaserror`.

```powershell
dotnet test tests/ApSolutions.LocalMedia.IntegrationTests -c Release -m:1 --settings eng/test.runsettings --filter "FullyQualifiedName~StoredCoverOrderSettingsTests"
```

`ServiceConsumptionTests` se pondrá roja hasta la Task 6, que es quien lo resuelve. **Es lo previsto**: se anota y se cierra ahí; si al terminar la Task 6 sigue roja, es que nadie lo consume de verdad.

- [ ] **Step 6: Commit**

```bash
git add src/ApSolutions.LocalMedia.Application/Metadata/ICoverOrderSettings.cs src/ApSolutions.LocalMedia.Infrastructure/Settings/StoredCoverOrderSettings.cs src/ApSolutions.LocalMedia.Windows/CompositionRoot.Backup.cs tests/ApSolutions.LocalMedia.IntegrationTests/Settings/StoredCoverOrderSettingsTests.cs
git commit -m "feat: LIB-021, el orden general de portadas se guarda y se repara al leerlo"
```

---

### Task 3: la resolución recorre el orden que le den

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Application/Metadata/ResolveTitlePoster.cs:65-82`
- Test: `tests/ApSolutions.LocalMedia.Application.Tests/Metadata/ResolveTitlePosterTests.cs`

**Interfaces:**
- Produces: `string? ResolveTitlePoster.Find(TitleId titleId, string? posterPath, string? personalCover = null, IReadOnlyList<CoverOrigin>? order = null)`.

- [ ] **Step 1: escribir las pruebas que fallan**

```csharp
[Fact]
public void The_provider_wins_when_the_order_puts_it_first()
{
    // personal y provider existen los dos en disco
    var found = _resolve.Find(_titleId, ProviderPath, PersonalFile, [CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame]);

    Assert.Equal(ProviderOnDisk, found);
}

[Fact]
public void The_frame_wins_when_the_order_puts_it_first_and_it_exists()
{
    var found = _resolve.Find(_titleId, ProviderPath, PersonalFile, [CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider]);

    Assert.Equal(FrameOnDisk, found);
}

[Fact]
public void An_order_that_puts_a_missing_origin_first_falls_through_to_the_next()
{
    // sin fotograma en disco
    var found = _resolve.Find(_titleId, ProviderPath, PersonalFile, [CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal]);

    Assert.Equal(ProviderOnDisk, found);
}

[Fact]
public void No_order_given_is_the_default_order()
{
    Assert.Equal(
        _resolve.Find(_titleId, ProviderPath, PersonalFile, CoverOrderPolicy.Default),
        _resolve.Find(_titleId, ProviderPath, PersonalFile));
}

[Fact]
public void A_broken_order_is_repaired_rather_than_obeyed()
{
    // una lista con un solo origen que no existe en disco: el resto se recorre igual
    var found = _resolve.Find(_titleId, ProviderPath, PersonalFile, [CoverOrigin.Frame]);

    Assert.Equal(PersonalOnDisk, found);
}
```

- [ ] **Step 2: verlas fallar.** Esperado: no compila, `Find` tiene tres parámetros.

- [ ] **Step 3: el cuarto parámetro**

```csharp
    public string? Find(
        TitleId titleId,
        string? posterPath,
        string? personalCover = null,
        IReadOnlyList<CoverOrigin>? order = null)
    {
        foreach (var origin in CoverOrderPolicy.Normalize(order))
        {
```

El resto del cuerpo no cambia: el `switch` de los tres brazos se queda como está. **El parámetro es opcional a propósito** — hay dieciséis llamadas en pruebas que no tienen nada que decir sobre el orden, y obligarlas a pasar `CoverOrderPolicy.Default` sería ruido que además tapa el caso de por defecto.

- [ ] **Step 4: verlas pasar**

```powershell
dotnet test tests/ApSolutions.LocalMedia.Application.Tests -c Release -m:1 --settings eng/test.runsettings --filter "FullyQualifiedName~ResolveTitlePosterTests"
```

- [ ] **Step 5: Commit**

```bash
git add src/ApSolutions.LocalMedia.Application/Metadata/ResolveTitlePoster.cs tests/ApSolutions.LocalMedia.Application.Tests/Metadata/ResolveTitlePosterTests.cs
git commit -m "feat: LIB-021, la resolución de portada recorre el orden que se le pide"
```

---

### Task 4: la excepción por título en el registro

**Files:**
- Create: `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0025_cover_order.sql`
- Modify: `src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/Manifest.json`
- Modify: `src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/CatalogMetadataRepository.cs` (los cinco sitios: `Columns:23-25`, `VALUES:103-104`, `DO UPDATE SET:106-119`, un `AddWithValue` en `128-149`, y el ordinal 15 en `Read:160-195`)
- Modify: `src/ApSolutions.LocalMedia.Domain/Metadata/MetadataMergePolicy.cs` (`EditableMetadata`, junto a `PersonalCover:41`)
- Modify: `src/ApSolutions.LocalMedia.Application/Metadata/UpdateMetadata.cs` (`MetadataFieldChanges:9-23` y la fusión en `:137`)
- Test: `tests/ApSolutions.LocalMedia.IntegrationTests/Data/SqliteBootstrapTests.cs:46-50`, `tests/ApSolutions.LocalMedia.IntegrationTests/Data/CatalogMetadataRepositoryTests.cs`, `tests/ApSolutions.LocalMedia.Application.Tests/Metadata/UpdateMetadataTests.cs`

**Interfaces:**
- Produces: `EditableMetadata.CoverOrder { get; init; }` (`string?`, texto de `CoverOrderPolicy.Format`, `null` = sin excepción); `MetadataFieldChanges.CoverOrder { get; init; }` (`IReadOnlyList<CoverOrigin>?`: `null` = no tocar, lista vacía = quitar la excepción, lista con contenido = fijarla); columna `cover_order TEXT NULL`.

- [ ] **Step 1: la migración**

`0025_cover_order.sql`, una línea, en LF sin BOM (lo garantiza `.gitattributes`, pero se comprueba):

```sql
ALTER TABLE catalog_metadata ADD COLUMN cover_order TEXT NULL;
```

- [ ] **Step 2: el manifiesto, con el hash medido**

```bash
sha256sum src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations/0025_cover_order.sql | cut -d' ' -f1 | tr 'a-f' 'A-F'
```

El valor que salga va en la entrada nueva, con los cuatro campos en el mismo orden que las demás:

```json
    {
      "version": 25,
      "name": "cover_order",
      "resource": "ApSolutions.LocalMedia.Infrastructure.Data.Migrations.0025_cover_order.sql",
      "sha256": "<el que imprimió el comando>"
    }
```

**El hash no se copia de ningún sitio ni se escribe a mano**: `MigrationRunner` compara SHA-256 del texto UTF-8 del recurso incrustado y rechaza la base entera si no cuadra.

- [ ] **Step 3: mover las tres afirmaciones del esquema**

En `SqliteBootstrapTests.cs`: `24L` → `25L` en las líneas 46 y 47, y `…,picture_adjustment,personal_cover` → `…,picture_adjustment,personal_cover,cover_order` en 48-50. **Las tres, o la puerta pasa sin medir la nueva.**

- [ ] **Step 4: verlas fallar**

```powershell
dotnet test tests/ApSolutions.LocalMedia.IntegrationTests -c Release -m:1 --settings eng/test.runsettings --filter "FullyQualifiedName~SqliteBootstrapTests"
```

Esperado: rojo con `Expected 25, Actual 24` **si el manifiesto aún no tiene la entrada**; verde una vez puesta. Correr este paso antes y después es lo que demuestra que la afirmación mide algo.

- [ ] **Step 5: el campo en el dominio y en el caso de uso**

En `EditableMetadata`, junto a `PersonalCover`:

```csharp
    /// <summary>
    /// The order of origins this one title overrides the general one with (LIB-021), written by
    /// <see cref="CoverOrderPolicy.Format"/>, or <see langword="null"/> to follow the general one.
    /// </summary>
    /// <remarks>
    /// It holds the whole order and not the winning origin, so that moving the general order later
    /// cannot change what this title was told to do.
    /// </remarks>
    public string? CoverOrder { get; init; }
```

`MetadataMergePolicy.Merge` **no lo asigna**, igual que `PersonalCover`: un refresco del proveedor no tiene campo por donde alcanzarlo.

En `MetadataFieldChanges`:

```csharp
    /// <summary>
    /// The order for this one title: <see langword="null"/> leaves it as it is, an empty list
    /// removes the override and goes back to the general order, and a list sets it.
    /// </summary>
    /// <remarks>
    /// The empty list is the second sentinel and it is not decoration: with only
    /// <see langword="null"/> there would be no way to undo an override once set, which is the
    /// hole <see cref="PersonalCover"/> still has.
    /// </remarks>
    public IReadOnlyList<CoverOrigin>? CoverOrder { get; init; }
```

Y la fusión, junto a la de `PersonalCover` en `UpdateMetadata.cs:137`:

```csharp
            CoverOrder = changes.CoverOrder switch
            {
                null => current.Metadata.CoverOrder,
                { Count: 0 } => null,
                var order => CoverOrderPolicy.Format(order),
            },
```

- [ ] **Step 6: los cinco sitios del repositorio.** `Columns` gana `, cover_order` al final; `VALUES` gana `$coverOrder`; `DO UPDATE SET` gana `cover_order = excluded.cover_order`; un `AddWithValue("$coverOrder", (object?)metadata.CoverOrder ?? DBNull.Value)` antes de `$expected`; y en `Read`, el ordinal **15**, `reader.IsDBNull(15) ? null : reader.GetString(15)`.

- [ ] **Step 7: pruebas del ida y vuelta**

En `CatalogMetadataRepositoryTests`: guardar con `CoverOrder = "Frame,Personal,Provider"` y leerlo; guardar con `null` y leerlo `null`; y **una fila escrita por el esquema viejo** (INSERT a mano sin la columna) que se lee como `null` sin excepción. En `UpdateMetadataTests`: los tres brazos del `switch` —no tocar, quitar, fijar—, que son tres ramas y las tres se cubren **en la misma suite**.

- [ ] **Step 8: verdes** `IntegrationTests` (filtros `SqliteBootstrapTests` y `CatalogMetadataRepositoryTests`) y `Application.Tests`.

- [ ] **Step 9: Commit**

```bash
git add src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations src/ApSolutions.LocalMedia.Infrastructure/Data/Repositories/CatalogMetadataRepository.cs src/ApSolutions.LocalMedia.Domain/Metadata/MetadataMergePolicy.cs src/ApSolutions.LocalMedia.Application/Metadata/UpdateMetadata.cs tests
git commit -m "feat: LIB-021, un título puede llevar su propio orden de portadas, con migración 25"
```

---

### Task 5: el orden efectivo llega a la cuadrícula y a las fichas

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Application/Catalog/CatalogQueries.cs:71` (`CatalogItem`) y la consulta que lo rellena
- Modify: `src/ApSolutions.LocalMedia.Presentation/Library/LibraryViewModel.cs:25,55,59,485,535`
- Modify: `src/ApSolutions.LocalMedia.Windows/CompositionRoot.cs:606-614,661,696,724-729`
- Test: `tests/ApSolutions.LocalMedia.Application.Tests/Catalog/CatalogQueriesTests.cs`, `tests/ApSolutions.LocalMedia.UiTests/Library/LibraryViewModelTests.cs`

**Interfaces:**
- Produces: `CatalogItem.CoverOrder` (`string?`); `LibraryViewModel` recibe `Func<TitleId, string?, string?, string?, string?> findPoster` (el cuarto argumento es el texto del orden del título, `null` = el general).

- [ ] **Step 1: la prueba que falla**

```csharp
[Fact]
public void A_title_with_its_own_order_asks_for_the_poster_with_it()
{
    string? asked = null;
    var view = NewLibrary(findPoster: (_, _, _, order) => { asked = order; return null; });

    await view.LoadAsync(WithItems(new CatalogItem(…) { CoverOrder = "Frame,Personal,Provider" }));

    Assert.Equal("Frame,Personal,Provider", asked);
}

[Fact]
public void A_title_without_its_own_order_asks_with_nothing_and_lets_the_general_one_decide()
{
    string? asked = "sentinel";
    var view = NewLibrary(findPoster: (_, _, _, order) => { asked = order; return null; });

    await view.LoadAsync(WithItems(new CatalogItem(…)));

    Assert.Null(asked);
}
```

- [ ] **Step 2: verlas fallar.** Esperado: no compila — el delegado tiene tres argumentos.

- [ ] **Step 3: ensanchar la cadena.** `CatalogItem` gana `string? CoverOrder = null` al final de sus parámetros posicionales; la consulta SQL que lo construye lee la columna nueva; el delegado de `LibraryViewModel` pasa a cuatro argumentos, con su valor por defecto `(_, _, _, _) => null`.

- [ ] **Step 4: quien decide el orden efectivo**, en `CompositionRoot.cs`. `FindCachedPoster` gana el parámetro y resuelve:

```csharp
    private static string? FindCachedPoster(
        IServiceProvider provider,
        TitleId titleId,
        string? posterPath,
        string? personalCover,
        string? coverOrder)
    {
        var order = CoverOrderPolicy.TryParse(coverOrder, out var forThisTitle)
            ? forThisTitle
            : provider.GetRequiredService<ICoverOrderSettings>().Current;
        return provider.GetRequiredService<ResolveTitlePoster>().Find(titleId, posterPath, personalCover, order);
    }
```

**Ésta es la única resolución del puerto del ajuste general**, y es la que pone verde `ServiceConsumptionTests` tras la Task 2. El delegado que se pasa a `LibraryViewModel` en `:614` se adapta a la firma nueva; el comentario de `:606-613` explica por qué es el método y no un lambda, y sigue valiendo.

- [ ] **Step 5: verdes** `Application.Tests`, `UiTests --filter LibraryViewModelTests`, y la solución con `-warnaserror`. **`ServiceConsumptionTests` tiene que estar verde aquí**; si no lo está, el puerto está registrado y no lo resuelve nadie, que es el defecto característico de este repositorio.

- [ ] **Step 6: Commit**

```bash
git add src/ApSolutions.LocalMedia.Application/Catalog/CatalogQueries.cs src/ApSolutions.LocalMedia.Presentation/Library/LibraryViewModel.cs src/ApSolutions.LocalMedia.Windows/CompositionRoot.cs tests
git commit -m "feat: LIB-021, el orden efectivo de cada título llega a la cuadrícula y a las fichas"
```

---

### Task 6: el mando del orden general

**Files:**
- Create: `src/ApSolutions.LocalMedia.Presentation/Settings/CoverOrderSettingsViewModel.cs`
- Test: `tests/ApSolutions.LocalMedia.UiTests/Settings/CoverOrderSettingsViewModelTests.cs`

**Interfaces:**
- Consumes: `ICoverOrderSettings`, `CoverOrderPolicy`, `PresentationText.Resource(string key, string fallback)`.
- Produces: `CoverOrderSettingsViewModel(ICoverOrderSettings settings)`; `IReadOnlyList<CoverOriginRow> Origins`; `int SelectedIndex { get; set; }`; `ICommand MoveUpCommand`; `ICommand MoveDownCommand`; `ICommand RestoreDefaultsCommand`; `bool CanMoveUp`; `bool CanMoveDown`; `sealed record CoverOriginRow(CoverOrigin Origin, string Name)`.

- [ ] **Step 1: escribir las pruebas que fallan**

```csharp
[Fact]
public void The_rows_open_in_the_stored_order()
{
    var vm = new CoverOrderSettingsViewModel(new FakeCoverOrderSettings([CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider]));

    Assert.Equal([CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider], vm.Origins.Select(row => row.Origin));
}

[Fact]
public void Moving_the_selected_row_up_stores_the_new_order()
{
    var settings = new FakeCoverOrderSettings(CoverOrderPolicy.Default);
    var vm = new CoverOrderSettingsViewModel(settings) { SelectedIndex = 1 };

    vm.MoveUpCommand.Execute(null);

    Assert.Equal([CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame], settings.Saved);
    Assert.Equal(0, vm.SelectedIndex);
}

[Fact]
public void Moving_down_stores_it_too_and_the_selection_follows_the_row()
{
    var settings = new FakeCoverOrderSettings(CoverOrderPolicy.Default);
    var vm = new CoverOrderSettingsViewModel(settings) { SelectedIndex = 0 };

    vm.MoveDownCommand.Execute(null);

    Assert.Equal([CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame], settings.Saved);
    Assert.Equal(1, vm.SelectedIndex);
}

[Fact]
public void The_first_row_cannot_go_up_and_the_last_cannot_go_down()
{
    var vm = new CoverOrderSettingsViewModel(new FakeCoverOrderSettings(CoverOrderPolicy.Default));

    vm.SelectedIndex = 0;
    Assert.False(vm.CanMoveUp);
    Assert.True(vm.CanMoveDown);

    vm.SelectedIndex = vm.Origins.Count - 1;
    Assert.True(vm.CanMoveUp);
    Assert.False(vm.CanMoveDown);
}

[Fact]
public void With_nothing_selected_neither_button_can_be_pressed()
{
    var vm = new CoverOrderSettingsViewModel(new FakeCoverOrderSettings(CoverOrderPolicy.Default));

    vm.SelectedIndex = -1;

    Assert.False(vm.CanMoveUp);
    Assert.False(vm.CanMoveDown);
}

[Fact]
public void Pressing_a_move_that_cannot_happen_changes_nothing()
{
    var settings = new FakeCoverOrderSettings(CoverOrderPolicy.Default);
    var vm = new CoverOrderSettingsViewModel(settings) { SelectedIndex = 0 };

    vm.MoveUpCommand.Execute(null);

    Assert.Null(settings.Saved);
    Assert.Equal([.. CoverOrderPolicy.Default], vm.Origins.Select(row => row.Origin));
}

[Fact]
public void Restoring_defaults_puts_the_order_back_and_stores_it()
{
    var settings = new FakeCoverOrderSettings([CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal]);
    var vm = new CoverOrderSettingsViewModel(settings);

    vm.RestoreDefaultsCommand.Execute(null);

    Assert.Equal(CoverOrderPolicy.Default, settings.Saved);
    Assert.Equal([.. CoverOrderPolicy.Default], vm.Origins.Select(row => row.Origin));
}

[Fact]
public void Every_origin_has_a_name_to_show()
{
    var vm = new CoverOrderSettingsViewModel(new FakeCoverOrderSettings(CoverOrderPolicy.Default));

    Assert.All(vm.Origins, row => Assert.False(string.IsNullOrWhiteSpace(row.Name)));
    Assert.Equal(Enum.GetValues<CoverOrigin>().Length, vm.Origins.Count);
}

[Fact]
public void A_view_model_with_no_settings_is_refused()
{
    _ = Assert.Throws<ArgumentNullException>(() => new CoverOrderSettingsViewModel(null!));
}
```

- [ ] **Step 2: verlas fallar.** Esperado: no compila.

- [ ] **Step 3: escribir el ViewModel**, con el molde de `ScanSettingsViewModel` (`INotifyPropertyChanged`, `SetField` con `CallerMemberName`, `RelayCommand` privado anidado). El nombre de cada origen sale de `PresentationText.Resource($"CoverOrigin{origin}", fallbackInglés)` — la composición de clave hay que **declararla** en la lista de excepciones de `OrphanedResourceTests` si esa puerta no la ve, que es exactamente el caso que su historia documenta (`"MarkerKind" + kind` sí, interpolación no).

  **Los dos comandos guardan al instante**, sin botón de aplicar: es el comportamiento de los demás grupos de Ajustes y lo que el paseo espera al pulsar.

- [ ] **Step 4: verlas pasar y medir las ramas**

```powershell
dotnet test tests/ApSolutions.LocalMedia.UiTests -c Release -m:1 --settings eng/test.runsettings --filter "FullyQualifiedName~CoverOrderSettingsViewModelTests" --collect:"XPlat Code Coverage;Format=json"
```

96/96 o se cubre lo que falte, **en esta misma suite**.

- [ ] **Step 5: Commit**

```bash
git add src/ApSolutions.LocalMedia.Presentation/Settings/CoverOrderSettingsViewModel.cs tests/ApSolutions.LocalMedia.UiTests/Settings/CoverOrderSettingsViewModelTests.cs
git commit -m "feat: LIB-021, el mando del orden general de portadas, con su restaurar"
```

---

### Task 7: la sección de Ajustes, sus textos y su vista

**Files:**
- Create: `src/ApSolutions.LocalMedia.Presentation/Settings/CoverOrderSettingsView.axaml` y `.axaml.cs`
- Modify: `src/ApSolutions.LocalMedia.Presentation/Shell/SettingsSection.cs` (miembro `Covers`, detrás de `Library`)
- Modify: `src/ApSolutions.LocalMedia.Presentation/Shell/ShellView.axaml` (el `ListBoxItem` del índice y el `ContentControl` que la monta, dentro de `SettingsSections`)
- Modify: `src/ApSolutions.LocalMedia.Presentation/Shell/ShellViewModel.cs` (`HasCoverOrderSettings`, `IsCoversSection` **y su `OnPropertyChanged` en el setter de `CurrentSettingsSection`, línea ~682**)
- Modify: `src/ApSolutions.LocalMedia.Presentation/Shell/ShellSurfaces.cs` (la propiedad `init`)
- Modify: `src/ApSolutions.LocalMedia.Windows/CompositionRoot.cs:735+` (`CreateShellSurfaces`) y el registro del ViewModel
- Modify: `src/ApSolutions.LocalMedia.Presentation/Resources/Strings.es.axaml` y `Strings.en.axaml`
- Test: `tests/ApSolutions.LocalMedia.UiTests/Theme/LeadingActionTests.cs:148-160`

- [ ] **Step 1: las cadenas, en los dos idiomas y en la misma línea de los dos ficheros**

| Clave | Español | English |
| --- | --- | --- |
| `CoverOrderSettingsTitle` | Orden de las portadas | Cover order |
| `CoverOrderSettingsAccessibleName` | Ajustes del orden de las portadas | Cover order settings |
| `CoverOrderSettingsDescription` | De qué sitio sale la portada de cada película o serie. Se busca de arriba abajo: la primera que exista es la que se ve. | Where each film's or show's cover comes from. They are looked for top to bottom: the first one that exists is the one shown. |
| `CoverOriginPersonal` | La que elegiste tú | The one you picked |
| `CoverOriginProvider` | La del proveedor | The provider's |
| `CoverOriginFrame` | Un fotograma del vídeo | A frame of the video |
| `CoverOrderMoveUpAction` | Subir | Move up |
| `CoverOrderMoveDownAction` | Bajar | Move down |

- [ ] **Step 2: la vista**, con el molde exacto de `ScanSettingsView.axaml`: `Border Padding="32" Background="{DynamicResource ShellSurfaceBrush}"`, `StackPanel MaxWidth="620" Spacing="{DynamicResource Space12}"`, la fila `Grid ColumnDefinitions="*,Auto"` con el título a `FontSizeSubtitle` + `HeadingLevel="2"` y el botón `x:Name="CoverOrderSettingsResetButton"` con `Content` y `AutomationProperties.Name` en `{DynamicResource RestoreDefaultsAction}`, la descripción, y debajo el `ListBox` de tres con los dos botones de mover.

  **Ningún literal numérico**: los espaciados con `Space*`, el tamaño con `FontSize*`. **Ningún botón con la clase `primary-action`**: un panel de ajustes no lidera con nada.

  Antes de escribirlo: `mcp__avalonia-docs__lookup_avalonia_api` de `ListBox` y de `SelectingItemsControl` —`SelectedIndex`, `ItemTemplate`—, y `get_avalonia_expert_rules` si es la primera vista de la sesión.

- [ ] **Step 3: registrar la vista en `LeadingActionTests`**, junto a sus vecinas:

```csharp
        ["CoverOrderSettingsView"] = null,
```

- [ ] **Step 4: los cinco sitios de la sección.** El enum, el `ListBoxItem` del índice con `Tag="{x:Static shell:SettingsSection.Covers}"` y `IsVisible="{Binding HasCoverOrderSettings}"`, el `ContentControl` dentro de `SettingsSections`, las dos propiedades del `ShellViewModel` **con su `OnPropertyChanged(nameof(IsCoversSection))` en el setter de `CurrentSettingsSection`**, y `CoverOrderSettings = provider.GetRequiredService<CoverOrderSettingsViewModel>()` en `CreateShellSurfaces` más su `AddTransient` en el registro.

  **El `OnPropertyChanged` es el que se olvida**: sin él la sección se elige en el índice y la pantalla no cambia, y ninguna prueba de ViewModel lo ve.

- [ ] **Step 5: verdes** `UiTests` entera (es la que mide AXAML, tokens, desbordamiento y acción principal) y `DocumentationTests` (bilingüismo).

```powershell
dotnet test tests/ApSolutions.LocalMedia.UiTests -c Release -m:1 --settings eng/test.runsettings
dotnet test tests/ApSolutions.LocalMedia.DocumentationTests -c Release -m:1 --settings eng/test.runsettings
```

- [ ] **Step 6: subir el trinquete de cobertura en el mismo cambio.** Una vista nueva mide 100/50 —la única rama que el compilador de Avalonia genera— y **eso no es deuda**: `eng/check-coverage.ps1:451` pasa de `185` a `186`, con su línea de motivo junto a las anteriores, y la fila correspondiente se añade copiando el artefacto `coverage-debt` del run de CI, **nunca a mano**. Es el único caso en que este número sube.

- [ ] **Step 7: Commit**

```bash
git add src/ApSolutions.LocalMedia.Presentation src/ApSolutions.LocalMedia.Windows/CompositionRoot.cs eng/check-coverage.ps1 tests
git commit -m "feat: LIB-021, el orden de las portadas se cambia desde Ajustes"
```

---

### Task 8: la excepción por título, en el editor de la ficha

**Files:**
- Modify: `src/ApSolutions.LocalMedia.Presentation/Metadata/MetadataEditorViewModel.cs` (junto a `PersonalCover:104` y en `SaveAsync:132-155`)
- Modify: `src/ApSolutions.LocalMedia.Presentation/Metadata/MetadataEditorView.axaml` (una fila nueva debajo del selector de portada, `Grid.Row="6"`)
- Modify: `Strings.es.axaml` y `Strings.en.axaml`
- Test: `tests/ApSolutions.LocalMedia.UiTests/Metadata/MetadataEditorViewModelTests.cs`

**Interfaces:**
- Produces: `MetadataEditorViewModel.CoverSourceIndex { get; set; }` (`0` = el orden general, `1..3` = los tres orígenes en el orden de `CoverOrderPolicy.Default`); `IReadOnlyList<string> CoverSourceChoices`.

- [ ] **Step 1: las cadenas**

| Clave | Español | English |
| --- | --- | --- |
| `MetadataCoverSourceLabel` | Qué portada usar en este título | Which cover to use for this title |
| `MetadataCoverSourceDefault` | El orden general | The general order |

Las tres opciones restantes reutilizan `CoverOriginPersonal`, `CoverOriginProvider` y `CoverOriginFrame` de la Task 7 — **una idea, una clave**, que es la regla que `UX-010` dejó escrita.

- [ ] **Step 2: escribir las pruebas que fallan**

```csharp
[Fact]
public void A_title_with_no_override_opens_on_the_general_order()
{
    var vm = NewEditor(coverOrder: null);

    Assert.Equal(0, vm.CoverSourceIndex);
}

[Fact]
public void A_title_with_an_override_opens_on_the_origin_that_wins()
{
    var vm = NewEditor(coverOrder: "Frame,Personal,Provider");

    Assert.Equal(1 + (int)CoverOrigin.Frame, vm.CoverSourceIndex);
}

[Fact]
public void Saving_the_general_order_removes_the_override()
{
    var vm = NewEditor(coverOrder: "Frame,Personal,Provider");
    vm.CoverSourceIndex = 0;

    await vm.SaveCommand.ExecuteAsync();

    Assert.Empty(_captured!.Changes.CoverOrder!);
}

[Fact]
public void Saving_an_origin_puts_it_first_and_keeps_the_rest()
{
    var vm = NewEditor(coverOrder: null);
    vm.CoverSourceIndex = 1 + (int)CoverOrigin.Provider;

    await vm.SaveCommand.ExecuteAsync();

    Assert.Equal(CoverOrigin.Provider, _captured!.Changes.CoverOrder![0]);
    Assert.Equal(Enum.GetValues<CoverOrigin>().Length, _captured.Changes.CoverOrder!.Count);
}

[Fact]
public void An_unreadable_stored_order_opens_on_the_general_order_rather_than_on_nothing()
{
    var vm = NewEditor(coverOrder: "Nonsense");

    Assert.Equal(0, vm.CoverSourceIndex);
}

[Fact]
public void The_choices_are_the_general_order_plus_every_origin()
{
    var vm = NewEditor(coverOrder: null);

    Assert.Equal(1 + Enum.GetValues<CoverOrigin>().Length, vm.CoverSourceChoices.Count);
    Assert.All(vm.CoverSourceChoices, choice => Assert.False(string.IsNullOrWhiteSpace(choice)));
}
```

- [ ] **Step 3: verlas fallar.** Esperado: no compila.

- [ ] **Step 4: implementar.** Al cargar, `CoverOrderPolicy.TryParse(metadata.CoverOrder, out var order)` decide: si falla, índice `0`; si no, `1 + (int)order[0]`. Al guardar, el índice `0` manda lista vacía —quitar la excepción— y cualquier otro manda `CoverOrderPolicy.WithFirst(origen, ordenActual)`.

  En el AXAML, un `ComboBox` con `ItemsSource="{Binding CoverSourceChoices}"` y `SelectedIndex="{Binding CoverSourceIndex}"`, con su `TextBlock` de etiqueta y su `AutomationProperties.Name` apuntando a la misma clave, dentro del `StackPanel` del selector de portada. `ComboBox` ya se usa en cuatro vistas de este árbol.

- [ ] **Step 5: verdes** `UiTests` entera y la solución con `-warnaserror`.

- [ ] **Step 6: Commit**

```bash
git add src/ApSolutions.LocalMedia.Presentation/Metadata src/ApSolutions.LocalMedia.Presentation/Resources tests
git commit -m "feat: LIB-021, un título puede saltarse el orden general desde su editor"
```

---

### Task 9: las puertas, la escena del paseo y el registro

**Files:**
- Modify: `tests/ApSolutions.LocalMedia.UiTests/Theme/OptionGroupTests.cs` (`Groups:88`, `RestoreButtons:234`, y los cuatro suelos: `:284`, `:322`, `:346`, `:418`, más el ancla `minimum: 10` de `:519`)
- Modify: `tests/ApSolutions.LocalMedia.AccessibilityTests/EndToEnd/AssembledPhysicalWalkTests.cs`
- Modify: `docs/FEATURES.md:63` (la fila `LIB-021`), `docs/TAREAS.md` (cerrar `ENG-003`), `CHANGELOG.es.md` y `CHANGELOG.en.md`
- Create: `docs/evidence/stable/LIB021-cover-order-setting.md`

- [ ] **Step 1: el grupo de opciones nuevo**, en `Groups`:

```csharp
        ["Covers"] = new(
            OptionPlace.Settings,
            "SettingsSection.Covers",
            "CoverOrderSettingsView",
            "CoverOrderSettingsResetButton",
            "Where a cover comes from is decided looking at the library, not at a video, so it is a settings page and not a player gear (rule 11). It has a destination of its own because SettingsSection.Library already hosts Scanning and this gate refuses two groups in one place."),
```

Y en `RestoreButtons`: `["CoverOrderSettingsView#CoverOrderSettingsResetButton"] = GroupResetKey,`.

- [ ] **Step 2: subir los cuatro suelos anti-ceguera**, de `12` a `13` en `:284`, `:322` y `:346`, y el ancla de `ShellView.axaml` de `minimum: 10` a `11` en `:519`. `:418` se mueve solo, porque cuenta `RestoreButtons.Count`.

  **Si no se suben, la puerta sigue pasando y deja de contar lo nuevo**: un suelo que no sube es una puerta que se vuelve ciega, que es el fallo que esta casa ha medido más veces.

- [ ] **Step 3: verlas fallar antes de tocar nada más.** Quitar temporalmente el botón de restaurar de la vista y comprobar que `OptionGroupTests` se pone roja nombrándolo; devolverlo. Es el control positivo: sin él, no se sabe si la fila nueva mide algo.

- [ ] **Step 4: la escena del paseo**, con el molde de la de Recomendaciones (`AssembledPhysicalWalkTests.cs:1115-1167`): navegar a Ajustes, pulsar `CoverOrderSettingsTitle` en el índice, comprobar que `CurrentSettingsSection` es `Covers`, seleccionar la segunda fila, pulsar `CoverOrderMoveUpAction`, comprobar que **el ajuste guardado cambió** —no el ViewModel: el almacén, que es lo que sobrevive a cerrar— y después pulsar `RestoreDefaultsAction` y comprobar que vuelve a `CoverOrderPolicy.Default`.

  **Mover algo antes de restaurar es obligatorio**: sin eso, restaurar no se distingue de no hacer nada, que es exactamente lo que la escena de escaneo documenta en `:899-900`.

- [ ] **Step 5: el trinquete del paseo no sube.** `eng/check-walk-coverage.ps1:100` sigue en `23`; sólo puede encoger. Si algún control de la vista nueva no se puede pulsar, **no se añade a `eng/walk-pending.txt`**: se rediseña el control, porque el trinquete no admite crecer sin una razón medida escrita en su cabecera.

- [ ] **Step 6: la matriz y el registro.** `LIB-021` pasa de `IN_PROGRESS` a `IMPLEMENTED` con el enlace a la evidencia nueva en sus dos idiomas; `ENG-003` se mueve a «Hechas» con su fecha de cierre; el changelog lo cuenta en los dos idiomas. La evidencia dice **qué se midió**, no qué se escribió: el orden que se guardó, el que se leyó, y la portada que la cuadrícula dibujó antes y después.

- [ ] **Step 7: las puertas enteras**

```powershell
dotnet format --verify-no-changes --severity warn
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
dotnet test tests/ApSolutions.LocalMedia.Domain.Tests -c Release -m:1 --settings eng/test.runsettings
dotnet test tests/ApSolutions.LocalMedia.Application.Tests -c Release -m:1 --settings eng/test.runsettings
dotnet test tests/ApSolutions.LocalMedia.UiTests -c Release -m:1 --settings eng/test.runsettings
dotnet test tests/ApSolutions.LocalMedia.IntegrationTests -c Release -m:1 --settings eng/test.runsettings
dotnet test tests/ApSolutions.LocalMedia.AccessibilityTests -c Release -m:1 --settings eng/test.runsettings
dotnet test tests/ApSolutions.LocalMedia.ArchitectureTests -c Release -m:1 --settings eng/test.runsettings
dotnet test tests/ApSolutions.LocalMedia.DocumentationTests -c Release -m:1 --settings eng/test.runsettings
pwsh -NoProfile -File eng/verify-docs.ps1
pwsh -NoProfile -File eng/preview-coverage-floors.ps1 -Suites Domain.Tests,Application.Tests,UiTests,IntegrationTests
```

**`preview-coverage-floors.ps1` calla sobre archivos nuevos sin commitear** (`ENG-016`): los tres archivos nuevos de este plan ya estarán commiteados a estas alturas, que es lo que hace su silencio creíble aquí y no antes.

- [ ] **Step 8: Commit y push a la rama**

```bash
git add -A
git commit -m "feat: LIB-021, el orden de portadas se cambia en general y por título (cierra ENG-003)"
git push
```

Y **armar el vigía con el SHA entero**, que es lo que el hook recuerda tras cada push:

```powershell
pwsh -NoProfile -File eng/watch-ci.ps1 -Sha <sha completo>
```

---

## Self-review

**Cobertura de la decisión.** ADR-0009 decisión 4, «la elección se puede cambiar en dos sitios»: el ajuste general es la Task 7 y la excepción por título la Task 8. El párrafo «los dos mecanismos no son redundancia» se respeta: ninguno de los dos se ha recortado. La consecuencia «hay migración» se cumple en la Task 4 — **el ADR dice que es la 23 y ya no lo es**: la 23 fue `picture_adjustment` y la 24 `personal_cover`, así que ésta es la **25**, medido en `Manifest.json`. La galería que el ADR menciona («el editor de fichas pasa a ser la galería del prototipo») **no entra aquí**: es una fila de paridad con el prototipo, vive en `PRD-006`, y mezclarla con esto convertiría el plan en dos.

**Tipos y nombres, de punta a punta.** `CoverOrderPolicy.Normalize/Format/TryParse/WithFirst` (Task 1) los consumen las Tasks 2, 3, 4, 5, 6 y 8 con esas mismas firmas. `ICoverOrderSettings` nace en la 2, se registra en la 2 y **se resuelve en la 5**, que es donde `ServiceConsumptionTests` se pone verde. `CatalogItem.CoverOrder` es `string?` en la 4 y en la 5, y sólo se convierte en lista dentro de `FindCachedPoster`. `MetadataFieldChanges.CoverOrder` es `IReadOnlyList<CoverOrigin>?` en la 4 y así lo usa la 8.

**Huecos.** Ninguna tarea dice «añadir validación» ni «manejar errores»: los tres centinelas —`null`, lista vacía, texto ilegible— tienen su prueba nombrada. Los suelos que hay que mover están con su número y su línea. Lo único que el plan **no** puede fijar de antemano es el SHA-256 de la migración, y por eso lleva el comando que lo calcula en vez de un valor inventado.
