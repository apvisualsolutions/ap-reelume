using System.Globalization;
using System.Text;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Show;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// What a screen reader is given. Every actionable control must announce a name that identifies it
/// uniquely on its surface, every control that carries a state must announce that state, and work
/// that takes time must announce itself instead of happening silently.
/// </summary>
public sealed class NarratorMetadataTests
{
    private static readonly string[] Languages = ["es-ES", "en-US"];
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [AvaloniaFact]
    public void Every_actionable_control_of_the_journey_announces_a_name()
    {
        var audit = new AuditLog(nameof(Every_actionable_control_of_the_journey_announces_a_name));
        var tree = new StringBuilder();

        foreach (var language in Languages)
        {
            foreach (var surface in CanonicalJourney.Surfaces)
            {
                using var host = CanonicalJourney.Show(surface, language);
                foreach (var control in Actionable(host.View))
                {
                    var peer = ControlAutomationPeer.CreatePeerForElement(control);
                    var name = peer.GetName();
                    tree.Append(language).Append('|').Append(surface.Surface).Append('|')
                        .Append(Describe(control)).Append('|').Append(name).Append('|')
                        .Append(peer.GetAutomationControlType()).AppendLine();
                    if (!string.IsNullOrWhiteSpace(name) && !LooksLikeATypeName(name))
                    {
                        continue;
                    }

                    audit.Add(
                        surface.StepId,
                        surface.Surface,
                        Describe(control),
                        control is ListBox ? DefectSeverity.Major : DefectSeverity.Critical,
                        string.IsNullOrWhiteSpace(name)
                            ? "The control announces no name at all."
                            : $"The control announces \"{name}\", which is a class name, not a name.",
                        $"Open {surface.Surface} in {language} and move the reader to {Describe(control)}.");
                }
            }
        }

        WriteTree("uia-names.txt", tree);
        audit.Complete();
    }

    [AvaloniaFact]
    public void Two_actions_on_one_surface_never_announce_the_same_thing()
    {
        var audit = new AuditLog(nameof(Two_actions_on_one_surface_never_announce_the_same_thing));

        foreach (var language in Languages)
        {
            foreach (var surface in CanonicalJourney.Surfaces)
            {
                using var host = CanonicalJourney.Show(surface, language);
                var announcements = Actionable(host.View)
                    .Select(control =>
                    {
                        var peer = ControlAutomationPeer.CreatePeerForElement(control);
                        return new
                        {
                            Control = control,
                            Sentence = string.Join(
                                " · ",
                                new[] { peer.GetName(), peer.GetHelpText(), peer.GetItemStatus() }
                                    .Where(part => !string.IsNullOrWhiteSpace(part))),
                        };
                    })
                    .GroupBy(entry => entry.Sentence, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1);

                foreach (var group in announcements)
                {
                    audit.Add(
                        surface.StepId,
                        surface.Surface,
                        Describe(group.First().Control),
                        DefectSeverity.Critical,
                        $"{group.Count()} actions announce the identical sentence \"{group.Key}\", so a "
                            + "reader user cannot tell them apart.",
                        $"Open {surface.Surface} in {language} and tab through the repeated controls.");
                }
            }
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void Every_control_that_carries_a_state_announces_that_state()
    {
        var audit = new AuditLog(nameof(Every_control_that_carries_a_state_announces_that_state));

        foreach (var language in Languages)
        {
            CheckStateful(
                audit,
                language,
                "favourite",
                nameof(PersonalActionsView),
                CanonicalJourney.Surfaces.Single(surface => surface.Surface == nameof(PersonalActionsView)));
            CheckStateful(
                audit,
                language,
                "settings",
                nameof(RecommendationSettingsView),
                CanonicalJourney.Surfaces.Single(surface =>
                    surface.Surface == nameof(RecommendationSettingsView)));
            CheckSelectedDestination(audit, language);
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void Work_that_takes_time_announces_itself_instead_of_running_silently()
    {
        var audit = new AuditLog(nameof(Work_that_takes_time_announces_itself_instead_of_running_silently));

        foreach (var language in Languages)
        {
            var surface = CanonicalJourney.Surfaces.Single(entry => entry.Surface == nameof(LibraryView));
            using var host = CanonicalJourney.Show(surface, language);
            var live = host.View.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => AutomationProperties.GetLiveSetting(control) != AutomationLiveSetting.Off)
                .ToArray();

            if (live.Length == 0)
            {
                audit.Add(
                    surface.StepId,
                    surface.Surface,
                    "scan progress",
                    DefectSeverity.Major,
                    "Scanning is the long-running work of the journey and no surface announces it: the "
                        + "library declares no live region at all.",
                    $"Open {surface.Surface} in {language} and start a scan; nothing is announced.");
            }
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void Every_surface_opens_with_a_heading_a_reader_can_jump_to()
    {
        var audit = new AuditLog(nameof(Every_surface_opens_with_a_heading_a_reader_can_jump_to));

        foreach (var surface in CanonicalJourney.Surfaces.Where(entry => entry.IsPage))
        {
            using var host = CanonicalJourney.Show(surface);
            var headings = host.View.GetVisualDescendants()
                .OfType<Control>()
                .Count(control => AutomationProperties.GetHeadingLevel(control) > 0);
            if (headings == 0)
            {
                audit.Add(
                    surface.StepId,
                    surface.Surface,
                    "heading",
                    DefectSeverity.Minor,
                    "The surface declares no heading level, so a reader cannot jump between sections.",
                    $"Open {surface.Surface} and ask the reader for the heading list.");
            }
        }

        audit.Complete();
    }

    // Runs on the UI thread: resolving the status text reads the application's theme variant, which
    // is exactly where a binding converter runs in production.
    [AvaloniaFact]
    public void The_pieces_a_reader_hears_are_derived_and_never_written_back()
    {
        var converter = new RouteStateConverter();
        Assert.Equal("●", converter.Convert(AppRoute.Library, typeof(string), AppRoute.Library, Invariant));
        Assert.Equal("○", converter.Convert(AppRoute.Home, typeof(string), AppRoute.Library, Invariant));
        Assert.Equal("○", converter.Convert(null, typeof(string), AppRoute.Library, Invariant));
        Assert.Equal("○", converter.Convert(AppRoute.Home, typeof(string), "not a route", Invariant));

        var status = new RouteStateConverter { Kind = RouteStateKind.Status };
        Assert.Equal(string.Empty, status.Convert(AppRoute.Home, typeof(string), AppRoute.Library, Invariant));
        Assert.False(string.IsNullOrWhiteSpace(
            status.Convert(AppRoute.Home, typeof(string), AppRoute.Home, Invariant)?.ToString()));
        Assert.Throws<NotSupportedException>(() =>
            converter.ConvertBack("●", typeof(AppRoute), null, Invariant));

        var available = new CatalogItemViewModel(CatalogEntry(isAvailable: true));
        var missing = new CatalogItemViewModel(CatalogEntry(isAvailable: false));
        Assert.Equal("MediaAvailable", available.AvailabilityKey);
        Assert.Equal("MediaUnavailable", missing.AvailabilityKey);

        var episode = new EpisodeRowViewModel(
            new EpisodeSequenceEntry(
                new EpisodeId(Guid.Empty),
                new TitleId(Guid.Empty),
                SeasonNumber: 3,
                EpisodeNumber: 7,
                MediaFileId: null,
                Path: null,
                IsAvailable: true),
            watchState: null);
        Assert.Equal("S03E07", episode.SeasonEpisodeLabel);
    }

    private static CatalogItem CatalogEntry(bool isAvailable) => new(
        new TitleId(Guid.Empty),
        CatalogTitleKind.Movie,
        "Arrival",
        2016,
        isAvailable,
        HasProgress: false,
        IsPersonal: false,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    private static void CheckStateful(
        AuditLog audit,
        string language,
        string step,
        string surfaceName,
        JourneySurface surface)
    {
        using var host = CanonicalJourney.Show(surface, language);
        foreach (var control in Actionable(host.View))
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(control);
            if (peer.GetProvider<IToggleProvider>() is not null
                || peer.GetProvider<ISelectionItemProvider>() is not null
                || peer.GetProvider<IRangeValueProvider>() is not null
                || !string.IsNullOrWhiteSpace(peer.GetItemStatus()))
            {
                continue;
            }

            if (!IsStateBearing(control))
            {
                continue;
            }

            audit.Add(
                step,
                surfaceName,
                Describe(control),
                DefectSeverity.Major,
                "The control switches a state but announces no state of its own: the answer only exists "
                    + "in a neighbouring label the reader has to go and find.",
                $"Open {surfaceName} in {language}, focus {Describe(control)} and listen.");
        }
    }

    private static void CheckSelectedDestination(AuditLog audit, string language)
    {
        var surface = CanonicalJourney.Surfaces.First(entry => entry.Surface == nameof(ShellView));
        using var host = CanonicalJourney.Show(surface, language);
        var destinations = host.View.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("navigation-destination"))
            .ToArray();

        Assert.NotEmpty(destinations);
        var announceSelection = destinations.Count(button =>
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(button);
            return peer.GetProvider<ISelectionItemProvider>() is not null
                || !string.IsNullOrWhiteSpace(peer.GetItemStatus());
        });

        if (announceSelection == 0)
        {
            audit.Add(
                "first-run",
                nameof(ShellView),
                "navigation-destination",
                DefectSeverity.Major,
                "No navigation destination announces whether it is the current one, so a reader user "
                    + "cannot tell where they are.",
                $"Open the shell in {language} and tab across the five destinations.");
        }
    }

    /// <summary>
    /// True when the control switches a state rather than running a one-way command. The two are
    /// indistinguishable in the visual tree, so the check compares the bound command with the toggles
    /// the view model actually exposes.
    /// </summary>
    private static bool IsStateBearing(Control control) => control switch
    {
        Button { DataContext: PersonalActionsViewModel personal } button =>
            ReferenceEquals(button.Command, personal.ToggleFavoriteCommand)
                || ReferenceEquals(button.Command, personal.ToggleWatchLaterCommand),
        Button { DataContext: RecommendationsViewModel recommendations } button =>
            ReferenceEquals(button.Command, recommendations.ToggleCommand),
        _ => false,
    };

    /// <summary>
    /// The surface the application owns: everything it declares itself. Parts a control template
    /// generates are excluded because they are neither ours to name nor reachable by keyboard; the
    /// composite control that owns them is audited instead.
    /// </summary>
    private static IReadOnlyList<Control> Actionable(Control view) =>
        [.. view.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control is Button or TextBox or ComboBox or CheckBox or Slider or ListBox)
            .Where(control => control.IsEffectivelyVisible && control.TemplatedParent is null)];

    /// <summary>A peer that falls back to the content's type name is announcing nothing useful.</summary>
    private static bool LooksLikeATypeName(string name) =>
        name.StartsWith("Avalonia.", StringComparison.Ordinal)
        || name.StartsWith("ApSolutions.", StringComparison.Ordinal);

    private static string Describe(Control control) =>
        control.Name is { Length: > 0 } name
            ? $"{control.GetType().Name}:{name}"
            : $"{control.GetType().Name}:{ContentText(control)}";

    private static string ContentText(Control control) => control switch
    {
        ContentControl { Content: { } content } => content.ToString() ?? "?",
        TextBox box => box.Text ?? "?",
        _ => "?",
    };

    private static void WriteTree(string fileName, StringBuilder tree)
    {
        var directory = Path.Combine(
            AuditLog.GetRepositoryRoot(),
            "artifacts",
            "ui-captures",
            "T33");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), tree.ToString());
    }
}
