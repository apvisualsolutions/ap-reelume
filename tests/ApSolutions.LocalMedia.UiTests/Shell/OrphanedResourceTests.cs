// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Xml.Linq;

using ApSolutions.LocalMedia.TestSupport;

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// A translated string no screen ever asks for is this repository's own defect wearing another
/// coat: something finished, paid for twice — once in each language — and called by nobody.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written on 2026-09-05, after an audit found seven of them at once.</b> Six pairs the sweep of
/// the sixty views turned up, plus a third name for the mini player that only an old evidence file
/// still mentioned. None of them broke a test, because until this file there was no test that could
/// break: <c>ShellLocalizationTests</c> compares the two dictionaries against each other and passes
/// happily when both carry the same dead key, and <c>ViewLiteralTests</c> guards the opposite
/// direction — that no view writes a word instead of asking for it.
/// </para>
/// <para>
/// <b>A key counts as consumed if its name appears anywhere outside the two dictionaries.</b> That
/// is deliberately generous: a name in a comment counts, and so does a name in a test. The cost of
/// being generous is a dead key surviving because somebody mentioned it; the cost of being strict is
/// this gate deleting a string the application needs at run time, which is worse and harder to see.
/// </para>
/// <para>
/// <b>And some keys are composed rather than written.</b> <c>MarkerRowLabelConverter</c> asks for
/// <c>"MarkerKind" + kind</c>, so <c>MarkerKindIntro</c> is consumed without its full name appearing
/// anywhere. Every literal that a <c>+</c> follows is therefore read as a prefix, and any key
/// starting with one is consumed. Miss that and this gate would demand the deletion of strings the
/// player draws every session.
/// </para>
/// </remarks>
public sealed class OrphanedResourceTests
{
    /// <summary>
    /// Keys with no consumer that are kept on purpose, each with the reason it is kept.
    /// </summary>
    /// <remarks>
    /// <b>A key belongs here only when something already written says it will be drawn</b> — a row
    /// in the scope record, or a finding in an audit that names it. Everything else goes: on
    /// 2026-09-05 eight strings were deleted rather than parked here, among them a second «Mini
    /// reproductor» and a second «Pantalla completa» left behind by a player header the application
    /// no longer has. The list shrinks by drawing what is in it, never by adding to it because a
    /// string looks useful.
    /// </remarks>
    private static readonly Dictionary<string, string> KeptWithoutConsumer = new(StringComparer.Ordinal)
    {
        // CRS-007 is DESIGN_APPROVED in docs/FEATURES.md: the Courses filter menu is scope that has
        // not been started, and its three words were translated when the row was written.
        ["CoursesMenuAll"] = "CRS-007, DESIGN_APPROVED",
        ["CoursesMenuFinished"] = "CRS-007, DESIGN_APPROVED",
        ["CoursesMenuThreadPending"] = "CRS-007, DESIGN_APPROVED",

        // The library summary the owner decided on 2026-09-05 to bring back as an out-of-reach
        // notice rather than as the card that was deleted on 2026-08-23. The count of unavailable
        // media is the one number that has no other home; the rest of the card's words wait with it
        // so the decision is taken once, with all of them on the table.
        ["HomeCountSeparator"] = "the out-of-reach notice, decided 2026-09-05",
        ["HomeLibraryEntryAccessibleName"] = "the out-of-reach notice, decided 2026-09-05",
        ["HomeLibraryHeading"] = "the out-of-reach notice, decided 2026-09-05",
        ["HomeLibraryMoviesLabel"] = "the out-of-reach notice, decided 2026-09-05",
        ["HomeLibraryShowsLabel"] = "the out-of-reach notice, decided 2026-09-05",
        ["HomeLibraryUnavailableLabel"] = "the out-of-reach notice, decided 2026-09-05",

        // Findings 9 and 10 of the built-and-not-drawn audit, both still open. The first two are the
        // reason a screen reader says «episodio» in the middle of a course.
        ["PlayerCourseFinishedNotice"] = "audit finding 9, open",
        ["PlayerNextLessonLabel"] = "audit finding 9, open",
        ["CourseLastOpenedFormat"] = "audit finding 10, open",

        // Finding 4: the scan row vanishes without saying it finished. It goes with the cancel
        // button, in the notices strip.
        ["ScanProgressCompleted"] = "audit finding 4, open",
    };

    [Fact]
    public void No_translated_string_is_left_without_a_screen_that_asks_for_it()
    {
        var spanish = Keys(Path.Combine(PresentationRoot(), "Resources", "Strings.es.axaml"));
        Assert.True(spanish.Length > 500, $"Only {spanish.Length} keys were read; the dictionary moved.");

        var consumers = ConsumerText(SourceRoot());
        var prefixes = ComposedPrefixes(consumers);
        Assert.NotEmpty(prefixes);

        var orphaned = spanish
            .Where(key => !consumers.Contains(key, StringComparison.Ordinal))
            .Where(key => !prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .Where(key => !KeptWithoutConsumer.ContainsKey(key))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            orphaned.Length == 0,
            "These strings are translated in both languages and no screen asks for them. Draw them, "
                + "or delete them from both dictionaries in the same change: "
                + string.Join(", ", orphaned));
    }

    /// <summary>
    /// The anti-blindness half. A gate that finds nothing because it read nothing is the failure
    /// mode this repository names as its own, so the count of keys it did find a consumer for is
    /// asserted rather than assumed.
    /// </summary>
    [Fact]
    public void The_sweep_actually_reads_the_tree_it_claims_to_read()
    {
        var spanish = Keys(Path.Combine(PresentationRoot(), "Resources", "Strings.es.axaml"));
        var consumers = ConsumerText(SourceRoot());

        var consumed = spanish.Count(key => consumers.Contains(key, StringComparison.Ordinal));

        Assert.True(consumed > 400, $"Only {consumed} keys were matched to a consumer; the sweep read the wrong tree.");
        Assert.Contains("NavigationHome", spanish);
        Assert.Contains("PlaybackSettingsTitle", spanish);
    }

    private static string ConsumerText(string sourceRoot)
    {
        var files = Directory
            .EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileName(path).StartsWith("Strings.", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(files.Length > 200, $"Only {files.Length} source files were swept; the root moved.");
        return string.Join('\n', files.Select(File.ReadAllText));
    }

    /// <summary>
    /// The two ways this tree builds a key it never writes whole: concatenation, as
    /// <c>MarkerRowLabelConverter</c>'s <c>"MarkerKind" + kind</c>, and interpolation, as
    /// <c>RestoreWizardViewModel</c>'s <c>$"RestoreFinding{finding.Kind}"</c>. Both were found by
    /// running this gate and reading what it wrongly called dead: the first pass named forty live
    /// strings, and every correction came from a consumer that existed and did not look like one.
    /// </summary>
    private static string[] ComposedPrefixes(string consumers) =>
        Regex.Matches(consumers, "\"([A-Za-z]{4,})\"\\s*\\+")
            .Concat(Regex.Matches(consumers, "\\$\"([A-Za-z]{4,})\\{"))
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string[] Keys(string path)
    {
        Assert.True(File.Exists(path), $"Resource dictionary is missing: {path}");
        return XDocument.Load(path).Root!
            .Elements()
            .Select(element => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key"))
            .Where(attribute => attribute is not null)
            .Select(attribute => attribute!.Value)
            .ToArray();
    }

    /// <summary>
    /// The whole of <c>src/</c>, and not the presentation project alone. Keys travel: the domain
    /// hands identification codes and restore findings over as strings the converter resolves, and
    /// the Windows host owns the tray menu and the system dialogs. Sweeping one project made this
    /// gate name forty live strings as dead on its first run, which is the shape of mistake it
    /// exists to prevent.
    /// </summary>
    private static string SourceRoot()
    {
        var root = RepositoryLayout.PathFromRoot("src");
        Assert.True(Directory.Exists(root), $"Source root is missing: {root}");
        return root;
    }

    private static string PresentationRoot()
    {
        var root = RepositoryLayout.PathFromRoot("src", "ApSolutions.LocalMedia.Presentation");
        Assert.True(Directory.Exists(root), $"Presentation project is missing: {root}");
        return root;
    }

}
