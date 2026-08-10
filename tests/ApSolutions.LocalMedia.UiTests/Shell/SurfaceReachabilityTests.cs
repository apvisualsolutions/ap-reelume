// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The assembly gate. Every other suite builds the surface it is about to examine; this one asks the
/// application whether that surface can be reached at all. A view nobody can open is not a feature,
/// however well it is tested, so an orphan here is a failing suite rather than a manual discovery.
/// </summary>
/// <remarks>
/// Reachability is read from what the application is made of: a surface reaches another when its XAML
/// instantiates it or when its code-behind names its type. The roots are the controls the composition
/// root can hand to the main window — the startup view it shows while the database is being made
/// ready, the shell, and the recovery screen it shows instead when the database cannot be opened.
/// <para>
/// ARQ-005 added the third one, and this suite is what noticed: a view was built with an accessible
/// name and nothing in the presentation project reached it, which is this repository's characteristic
/// defect. It is a root rather than an orphan because the window holds it directly, the same way it
/// holds the other two.
/// </para>
/// </remarks>
public sealed class SurfaceReachabilityTests
{
    private static readonly string[] Roots = ["StartupView", "ShellView", "DatabaseRecoveryView"];

    /// <summary>
    /// The fourteen surfaces ADR-0003 found built, tested, and unreachable. Each one is named on its
    /// own so a failure says which promise is still not in the application.
    /// </summary>
    public static TheoryData<string, string, string> DeclaredSurfaces() => new()
    {
        { "RootOnboardingView", "LIB-001", "añadir una carpeta a la biblioteca / add a library folder" },
        { "ScanSettingsView", "LIB-002", "ajustes de escaneo / scan settings" },
        { "ReviewInboxView", "LIB-007", "bandeja de revisión / review inbox" },
        { "DuplicateReviewView", "LIB-008", "duplicados como versiones / duplicates as versions" },
        { "MetadataEditorView", "LIB-011", "editor de metadatos y arte / metadata and artwork editor" },
        { "RenamePreviewView", "LIB-012", "renombrado seguro / safe rename" },
        { "PlayerView", "PLY-001", "reproductor integrado / embedded player" },
        { "AudioOutputView", "PLY-004", "salida de audio / audio output" },
        { "TrackSelectorView", "PLY-005", "pistas de audio y subtítulos / audio and subtitle tracks" },
        { "ResumePromptView", "PLY-008", "reanudar donde se dejó / resume where it was left" },
        { "MarkerEditorView", "PLY-012", "marcadores manuales / manual markers" },
        { "ShortcutSettingsView", "PLY-014", "atajos configurables / configurable shortcuts" },
        { "CreditsView", "PRD-005", "créditos y atribución TMDB / credits and TMDB attribution" },
        { "SubtitleStyleView", "A11Y-002", "estilo de subtítulos / subtitle styling" },
    };

    [Theory]
    [MemberData(nameof(DeclaredSurfaces))]
    public void Every_surface_the_product_declares_is_reachable_from_the_application(
        string surface,
        string featureId,
        string commitment)
    {
        var graph = SurfaceGraph.Load();

        Assert.True(
            graph.Contains(surface),
            $"{surface} does not exist in the presentation project, so {featureId} has nothing to reach.");
        Assert.True(
            graph.IsReachable(surface),
            $"{surface} is built and tested but unreachable from the application, "
            + $"so {featureId} ({commitment}) is not delivered.");
    }

    /// <summary>
    /// The structural rule the fourteen came from. A surface that announces itself to assistive
    /// technology is a surface the product intends someone to use, so leaving it out of the
    /// application is a defect on its own terms.
    /// </summary>
    [Fact]
    public void No_surface_with_an_accessible_name_is_left_out_of_the_application()
    {
        var graph = SurfaceGraph.Load();

        var orphans = graph.Surfaces
            .Where(surface => surface.HasAccessibleName && !graph.IsReachable(surface.Name))
            .Select(surface => surface.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            orphans.Length == 0,
            $"These surfaces announce an accessible name and cannot be reached: {string.Join(", ", orphans)}.");
    }

    /// <summary>
    /// TMDB attribution is a licence condition, not decoration. It has to be a real surface with a
    /// name, or nobody can read it and no audit can find it.
    /// </summary>
    [Fact]
    public void The_tmdb_attribution_is_a_named_surface_and_not_a_loose_fragment()
    {
        var graph = SurfaceGraph.Load();
        var credits = Assert.Single(graph.Surfaces, surface => surface.Name == "CreditsView");

        Assert.False(
            string.IsNullOrWhiteSpace(credits.ClassName),
            "CreditsView has no x:Class, so no surface can host it.");
        Assert.True(
            credits.HasAccessibleName,
            "CreditsView has no accessible name, so a screen reader cannot announce the attribution.");
        Assert.True(graph.IsReachable("CreditsView"), "The TMDB attribution cannot be read in the application.");
    }

    // The textual assertion that identification's provider, candidate source, and artwork cache
    // are registered lived here — and its artwork half was satisfied by a dead registration since
    // T39B, the third time a textual assertion was satisfied by dead text. It moved to
    // CompositionDescriptorTests (AccessibilityTests), which asserts against the registered
    // IServiceCollection descriptors instead of the file's characters — the start of ARQ-006.

    // The textual assertions that playback starts from a title card through the coordinator, and
    // that the automatic update check runs on a singleton surface, lived here as matches on the
    // composition root's text. They moved to CompositionDescriptorTests (AccessibilityTests),
    // which asserts the registered descriptors instead (ARQ-006); the card actually opening a
    // session is walked by the assembled physical walk with real decoding.

    /// <summary>
    /// Reaching a surface is not the same as feeding it. The status overlay was reachable and stayed
    /// blank through a real playback, because the composition root built the view model and nothing
    /// ever handed it what the engine had decided — so `PLY-003`'s indicator did not exist in the
    /// application, only in its tests.
    /// </summary>
    [Fact]
    public void The_composition_root_tells_the_video_status_overlay_what_the_engine_decided()
    {
        var composition = CompositionSourceText.Read();

        Assert.Contains("videoStatus.Apply(", composition, StringComparison.Ordinal);
        Assert.Contains("HardwareAccelerationRequested", composition, StringComparison.Ordinal);
    }

    /// <summary>
    /// A preference nothing reads is not a preference. The window startup must ask the update
    /// surface for its automatic check; the singleton half of the old assertion moved to the
    /// descriptors, but the invocation lives in startup code no descriptor can express, so this
    /// half stays textual until the startup path leaves the file (ARQ-001 / ARQ-006 steps 2-3).
    /// </summary>
    [Fact]
    public void The_window_startup_asks_the_update_surface_for_its_automatic_check()
    {
        var composition = CompositionSourceText.Read();

        Assert.Contains("CheckAutomaticallyAsync", composition, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    /// <summary>One declared surface and what it is made of.</summary>
    private sealed record Surface(string Name, string? ClassName, bool HasAccessibleName, string[] References);

    /// <summary>
    /// The surfaces of the presentation project and the edges between them, walked from the roots the
    /// composition root can actually show.
    /// </summary>
    private sealed class SurfaceGraph
    {
        private readonly HashSet<string> _reachable;

        private SurfaceGraph(IReadOnlyList<Surface> surfaces)
        {
            Surfaces = surfaces;
            _reachable = Walk(surfaces);
        }

        public IReadOnlyList<Surface> Surfaces { get; }

        public static SurfaceGraph Load()
        {
            var presentationRoot = Path.Combine(RepositoryRoot(), "src", "ApSolutions.LocalMedia.Presentation");
            var files = Directory
                .EnumerateFiles(presentationRoot, "*.axaml", SearchOption.AllDirectories)
                .Where(IsSurfaceFile)
                .ToArray();
            var names = files.Select(Path.GetFileNameWithoutExtension).Select(name => name!).ToHashSet(StringComparer.Ordinal);
            return new SurfaceGraph([.. files.Select(file => Describe(file, names))]);
        }

        public bool Contains(string surface) => Surfaces.Any(candidate => candidate.Name == surface);

        public bool IsReachable(string surface) => _reachable.Contains(surface);

        private static bool IsSurfaceFile(string path)
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}Resources{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                return false;
            }

            var name = Path.GetFileName(path);
            return name is not ("App.axaml" or "DesignTokens.axaml");
        }

        private static Surface Describe(string path, IReadOnlySet<string> names)
        {
            var name = Path.GetFileNameWithoutExtension(path)!;
            var markup = File.ReadAllText(path);
            var codeBehindPath = path + ".cs";
            var codeBehind = File.Exists(codeBehindPath) ? File.ReadAllText(codeBehindPath) : string.Empty;
            var references = names
                .Where(candidate => candidate != name
                    && (ReferencesInMarkup(markup, candidate) || ReferencesInCode(codeBehind, candidate)))
                .Order(StringComparer.Ordinal)
                .ToArray();
            return new Surface(name, ReadClassName(markup), HasAccessibleName(markup), references);
        }

        // A surface instantiates another when its markup opens that element under any namespace prefix.
        private static bool ReferencesInMarkup(string markup, string candidate) =>
            Regex.IsMatch(
                markup,
                $@"<[A-Za-z0-9_]+:{Regex.Escape(candidate)}[\s/>]",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));

        private static bool ReferencesInCode(string code, string candidate) =>
            code.Length > 0
            && Regex.IsMatch(
                code,
                $@"\b{Regex.Escape(candidate)}\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));

        private static string? ReadClassName(string markup)
        {
            var match = Regex.Match(markup, @"x:Class=""([^""]+)""", RegexOptions.None, TimeSpan.FromSeconds(1));
            return match.Success ? match.Groups[1].Value : null;
        }

        private static bool HasAccessibleName(string markup)
        {
            var document = XDocument.Parse(markup);
            XNamespace automation = "clr-namespace:Avalonia.Automation;assembly=Avalonia.Controls";
            return document.Root is { } root
                && root.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "AutomationProperties.Name"
                    || (attribute.Name.Namespace == automation && attribute.Name.LocalName == "Name"));
        }

        private static HashSet<string> Walk(IReadOnlyList<Surface> surfaces)
        {
            var byName = surfaces.ToDictionary(surface => surface.Name, StringComparer.Ordinal);
            var reached = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Queue<string>();
            foreach (var root in Roots.Where(byName.ContainsKey))
            {
                if (reached.Add(root))
                {
                    pending.Enqueue(root);
                }
            }

            while (pending.Count > 0)
            {
                foreach (var reference in byName[pending.Dequeue()].References)
                {
                    if (byName.ContainsKey(reference) && reached.Add(reference))
                    {
                        pending.Enqueue(reference);
                    }
                }
            }

            return reached;
        }
    }
}
