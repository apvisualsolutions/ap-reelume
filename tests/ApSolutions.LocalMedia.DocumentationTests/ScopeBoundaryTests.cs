namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// What this release deliberately does not do.
/// </summary>
/// <remarks>
/// An exclusion is only real while nothing quietly implements it. These checks read the shipped
/// interface — the resource dictionaries the user actually sees — and the public documents, and fail
/// when an excluded capability appears as though it were a feature. The excluded words also have to
/// stay *documented* as excluded: a boundary nobody wrote down is a boundary the next task will cross
/// without noticing.
/// </remarks>
public sealed class ScopeBoundaryTests
{
    /// <summary>
    /// Each exclusion, with the strings that would betray it in the interface. The words are the ones
    /// the product would have to use if it had the capability.
    /// </summary>
    public static TheoryData<string, string[]> Exclusions() => new()
    {
        { "cuentas y sesión remota / accounts and remote sessions", ["IniciarSesion", "SignIn", "CrearCuenta", "CreateAccount", "Contrasena", "Password"] },
        { "sincronización entre equipos / cross-device sync", ["Sincronizar", "Sync", "Nube", "Cloud"] },
        { "reproducción simultánea de varios vídeos / simultaneous multi-video playback", ["SegundaSesion", "SecondSession", "MultiReproductor", "MultiPlayer"] },
        { "cursos y formación / courses and training", ["Curso", "Course", "Leccion", "Lesson"] },
        { "gestión de vídeos ajena a la biblioteca / video management beyond the library", ["Convertir", "Transcode", "Recortar", "Trim", "Exportar vídeo", "Export video"] },
        // "Note" is deliberately not a marker on its own: it matches RestoreFindingNotEnoughSpace and
        // every Notice in the licence strings, which are not this capability.
        { "notas personales en la línea de tiempo / personal timeline notes", ["TimelineNote", "PersonalNote", "NotaPersonal", "NotaLineaTiempo", "BookmarkNote"] },
        { "listas personalizadas / custom lists", ["ListaPersonalizada", "CustomList", "Playlist"] },
        // The excluded passthrough is the audio one. VideoStatusHdrPassthrough is PLY-003's HDR10
        // path, which this release does implement, so the marker names the audio capability instead.
        { "Dolby Vision y passthrough de audio / Dolby Vision and audio passthrough", ["DolbyVision", "DolbyAtmos", "DtsPassthrough", "AudioPassthrough", "BitstreamPassthrough"] },
        { "macOS y Linux", ["macOS", "Linux"] },
    };

    [Theory]
    [MemberData(nameof(Exclusions))]
    public void No_excluded_capability_appears_in_the_shipped_interface(string exclusion, string[] markers)
    {
        var offenders = new List<string>();
        foreach (var dictionary in Directory.EnumerateFiles(
            RepositoryLayout.PathFromRoot("src/ApSolutions.LocalMedia.Presentation/Resources"), "Strings.*.axaml"))
        {
            var text = File.ReadAllText(dictionary);
            foreach (var marker in markers)
            {
                // A resource key names a capability the interface offers; the values are prose and may
                // legitimately mention a word the key must not carry.
                foreach (var key in ResourceKeys(text).Where(key =>
                    key.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                {
                    offenders.Add($"{Path.GetFileName(dictionary)} offers '{key}'");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"The interface offers something excluded from {exclusion}: {string.Join("; ", offenders)}.");
    }

    /// <summary>
    /// The exclusions are written down where someone deciding what to build next would look.
    /// </summary>
    [Fact]
    public void The_roadmap_states_what_this_release_does_not_do()
    {
        foreach (var language in new[] { "es", "en" })
        {
            var path = RepositoryLayout.PathFromRoot($"docs/roadmap/README.{language}.md");
            Assert.True(File.Exists(path), $"docs/roadmap/README.{language}.md is missing.");
            var text = File.ReadAllText(path);

            foreach (var id in new[] { "UX-007", "UX-008", "PLY-013", "PLY-015", "PRD-003" })
            {
                Assert.True(
                    text.Contains(id, StringComparison.Ordinal),
                    $"The {language} roadmap does not account for {id}.");
            }
        }
    }

    /// <summary>
    /// The two commitments the product refuses outright keep their refusal, in the matrix and in the
    /// specification. Anything else is a scope change, and a scope change is a decision, not an edit.
    /// </summary>
    [Fact]
    public void The_refused_commitments_keep_their_refusal()
    {
        foreach (var (id, expected) in new[] { ("UX-008", "OUT_OF_SCOPE"), ("PLY-015", "OUT_OF_SCOPE") })
        {
            var row = Assert.Single(FeatureMatrix.Rows, candidate => candidate.Id == id);
            Assert.Equal(expected, row.Status);
        }
    }

    /// <summary>
    /// No database table, and therefore no schema, exists for something the release excludes. The
    /// interface can be changed back; a table that shipped cannot.
    /// </summary>
    [Fact]
    public void No_migration_creates_a_table_for_an_excluded_capability()
    {
        var migrations = RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Infrastructure/Data/Migrations");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(migrations, "*.sql"))
        {
            var text = File.ReadAllText(file);
            foreach (var table in new[] { "accounts", "sessions", "sync_", "playlists", "custom_lists", "notes", "courses" })
            {
                if (text.Contains($"CREATE TABLE {table}", StringComparison.OrdinalIgnoreCase))
                {
                    offenders.Add($"{Path.GetFileName(file)} creates {table}");
                }
            }
        }

        Assert.True(offenders.Count == 0, $"Excluded capabilities reached the schema: {string.Join("; ", offenders)}.");
    }

    private static IEnumerable<string> ResourceKeys(string dictionary) =>
        System.Text.RegularExpressions.Regex
            .Matches(dictionary, @"x:Key=""(?<key>[^""]+)""", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(2))
            .Select(match => match.Groups["key"].Value);
}
