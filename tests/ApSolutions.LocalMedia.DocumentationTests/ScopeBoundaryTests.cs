// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// What this release deliberately does not do.
/// </summary>
/// <remarks>
/// An exclusion is only real while nothing quietly implements it. These checks read the shipped
/// interface — the resource dictionaries the user actually sees — and the database schema, and fail
/// when an excluded capability appears as though it were a feature.
/// </remarks>
public sealed partial class ScopeBoundaryTests
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
        // Narrowed rather than dropped when courses arrived. This row used to ban the words
        // "Curso", "Course", "Leccion" and "Lesson" outright, which also banned cataloguing a folder
        // of numbered videos that is already on the disk — the thing this application exists to do.
        // What the exclusion actually protects is the platform: the markers are the words the
        // product would have to use if it enrolled anybody, certified anything, or kept a study
        // record. "Badge" is deliberately not among them: ten existing keys carry it, starting with
        // UnavailableBadge.
        { "formación como plataforma / training as a platform", ["Matricula", "Enrolment", "Enrollment", "Certificado", "Certificate", "Diploma", "Cuestionario", "Quiz", "Racha", "Streak", "ProgresoFormacion", "TrainingProgress", "PorcentajeFormacion", "CompletionPercent", "EstadisticasEstudio", "StudyStatistics"] },
        { "gestión de vídeos ajena a la biblioteca / video management beyond the library", ["Convertir", "Transcode", "Recortar", "Trim", "Exportar vídeo", "Export video"] },
        // "Note" is deliberately not a marker on its own: it matches RestoreFindingNotEnoughSpace and
        // every Notice in the licence strings, which are not this capability.
        { "notas personales en la línea de tiempo / personal timeline notes", ["TimelineNote", "PersonalNote", "NotaPersonal", "NotaLineaTiempo", "BookmarkNote"] },
        { "listas personalizadas / custom lists", ["ListaPersonalizada", "CustomList", "Playlist"] },
        // The excluded passthrough is the audio one. VideoStatusHdrPassthrough is the HDR10 path,
        // which this release does implement, so the marker names the audio capability instead.
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
            // "courses" left this list when a course became a third kind of title; "lessons" never
            // joined it. What replaces them is the schema a course platform would need and this one
            // must never grow: an enrolment, a certificate, a quiz, a streak. A table that shipped
            // cannot be changed back.
            foreach (var table in new[] { "accounts", "sessions", "sync_", "playlists", "custom_lists", "notes", "enrolments", "enrollments", "certificates", "quizzes", "streaks" })
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
