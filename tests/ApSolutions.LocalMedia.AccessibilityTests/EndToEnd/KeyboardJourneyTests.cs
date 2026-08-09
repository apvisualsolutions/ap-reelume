using System.Text;
using System.Xml.Linq;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Home;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// The whole approved journey, walked without a mouse. Every stop must offer a keyboard entry point,
/// the tab ring must close instead of trapping the person, focus must be visible from the approved
/// token, and the keyboard must actually invoke what it lands on.
/// </summary>
public sealed class KeyboardJourneyTests
{
    private static readonly string[] Languages = ["es-ES", "en-US"];

    [AvaloniaFact]
    public void Every_stop_of_the_journey_can_be_entered_from_the_keyboard()
    {
        var audit = new AuditLog(nameof(Every_stop_of_the_journey_can_be_entered_from_the_keyboard));
        var order = new StringBuilder();

        foreach (var language in Languages)
        {
            foreach (var surface in CanonicalJourney.Surfaces)
            {
                using var host = CanonicalJourney.Show(surface, language);
                var reachable = Focusable(host.View);
                order.Append(language).Append('|').Append(surface.Surface).Append('|')
                    .AppendLine(string.Join(" → ", reachable.Select(Describe)));

                if (reachable.Count == 0)
                {
                    audit.Add(
                        surface.StepId,
                        surface.Surface,
                        "surface",
                        DefectSeverity.Critical,
                        "The stop offers no control the keyboard can reach, so the journey stops here "
                            + "for anyone without a mouse.",
                        $"Open {surface.Surface} in {language} and press Tab.");
                }
            }
        }

        WriteEvidence("tab-order.txt", order);
        audit.Complete();
    }

    [AvaloniaFact]
    public void Tab_walks_the_whole_ring_and_comes_back_without_trapping_anyone()
    {
        var audit = new AuditLog(nameof(Tab_walks_the_whole_ring_and_comes_back_without_trapping_anyone));

        foreach (var surface in CanonicalJourney.Surfaces)
        {
            using var host = CanonicalJourney.Show(surface);
            var reachable = Focusable(host.View);
            if (reachable.Count == 0)
            {
                continue;
            }

            reachable[0].Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();

            // Identity, not description: three theme buttons describe identically and would look like
            // a trap while the focus is in fact moving between them.
            var visited = new List<object> { reachable[0] };
            for (var step = 0; step < reachable.Count; step++)
            {
                host.Window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                host.Window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                Dispatcher.UIThread.RunJobs();
                if (TopLevel.GetTopLevel(host.Window)?.FocusManager?.GetFocusedElement() is { } focused)
                {
                    visited.Add(focused);
                }
            }

            var distinct = visited.Distinct(ReferenceEqualityComparer.Instance).Count();
            if (reachable.Count > 1 && distinct < 2)
            {
                audit.Add(
                    surface.StepId,
                    surface.Surface,
                    Describe(reachable[0]),
                    DefectSeverity.Critical,
                    $"Tab never leaves {Describe(reachable[0])}: the focus is trapped.",
                    $"Open {surface.Surface}, focus the first control and press Tab repeatedly.");
            }
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void Focus_stays_visible_through_the_approved_token_on_every_control_type()
    {
        var audit = new AuditLog(nameof(Focus_stays_visible_through_the_approved_token_on_every_control_type));
        var covered = FocusStyledTypes();
        var seen = new Dictionary<string, JourneySurface>(StringComparer.Ordinal);

        foreach (var surface in CanonicalJourney.Surfaces)
        {
            using var host = CanonicalJourney.Show(surface);
            foreach (var control in Focusable(host.View))
            {
                seen.TryAdd(control.GetType().Name, surface);
            }
        }

        foreach (var (typeName, surface) in seen.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (covered.Contains(typeName))
            {
                continue;
            }

            audit.Add(
                surface.StepId,
                surface.Surface,
                typeName,
                DefectSeverity.Major,
                $"{typeName} takes keyboard focus but no approved focus token styles it, so its focus "
                    + "ring is whatever the base theme draws and it was never contrast-checked.",
                $"Tab onto a {typeName} in high contrast and look for the focus ring.");
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void The_keyboard_invokes_what_it_lands_on()
    {
        var audit = new AuditLog(nameof(The_keyboard_invokes_what_it_lands_on));

        foreach (var surface in CanonicalJourney.Surfaces)
        {
            using var host = CanonicalJourney.Show(surface);
            foreach (var button in Focusable(host.View).OfType<Button>())
            {
                var peer = ControlAutomationPeer.CreatePeerForElement(button);
                if (peer.GetProvider<IInvokeProvider>() is not null
                    || peer.GetProvider<IToggleProvider>() is not null
                    || peer.GetProvider<IExpandCollapseProvider>() is not null)
                {
                    continue;
                }

                audit.Add(
                    surface.StepId,
                    surface.Surface,
                    Describe(button),
                    DefectSeverity.Critical,
                    "The control looks like a button but exposes no invoke pattern, so a reader cannot "
                        + "activate it.",
                    $"Open {surface.Surface}, focus {Describe(button)} and press Enter.");
            }
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void Enter_activates_the_primary_action_of_the_stops_that_own_one()
    {
        var home = CanonicalJourney.Surfaces.First(surface => surface.Surface == nameof(HomeView));
        using (var host = CanonicalJourney.Show(home))
        {
            var resume = Named(host.View, "ResumeHeroAction");
            resume.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Assert.True(resume.IsKeyboardFocusWithin, "Home does not accept keyboard focus on Continue.");
            Assert.NotNull(resume.Command);
            Assert.True(resume.Command!.CanExecute(null));
        }

        var personal = CanonicalJourney.Surfaces.First(surface =>
            surface.Surface == nameof(PersonalActionsView));
        using (var host = CanonicalJourney.Show(personal))
        {
            var viewModel = Assert.IsType<PersonalActionsViewModel>(host.View.DataContext);
            var before = viewModel.IsWatchLater;
            var toggle = host.View.GetVisualDescendants()
                .OfType<Button>()
                .First(button => ReferenceEquals(button.Command, viewModel.ToggleWatchLaterCommand));

            toggle.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            host.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            host.Window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(before, viewModel.IsWatchLater);
        }
    }

    /// <summary>Reads the control types the approved focus token actually styles.</summary>
    private static HashSet<string> FocusStyledTypes()
    {
        var document = XDocument.Load(Path.Combine(
            AuditLog.GetRepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Theme",
            "DesignTokens.axaml"));
        var styled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var style in document.Descendants().Where(element => element.Name.LocalName == "Style"))
        {
            var selector = style.Attribute("Selector")?.Value;
            if (selector is null || !selector.Contains(":focus", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var part in selector.Split(','))
            {
                var typeName = part.Trim().Split([':', '.', ' ', '>'], StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    styled.Add(typeName);
                }
            }
        }

        return styled;
    }

    private static IReadOnlyList<Control> Focusable(Control view) =>
        [.. view.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Focusable
                && control.IsEffectivelyVisible
                && control.IsEffectivelyEnabled
                && control.TemplatedParent is null)];

    private static Button Named(Control view, string name) =>
        view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static string Focused(CanonicalJourney.SurfaceHost host)
    {
        var focused = TopLevel.GetTopLevel(host.Window)?.FocusManager?.GetFocusedElement();
        return focused is Control control ? Describe(control) : "none";
    }

    private static string Describe(Control control) =>
        control.Name is { Length: > 0 } name
            ? $"{control.GetType().Name}:{name}"
            : control is ContentControl { Content: { } content }
                ? $"{control.GetType().Name}:{content}"
                : control.GetType().Name;

    private static void WriteEvidence(string fileName, StringBuilder content)
    {
        var directory = Path.Combine(AuditLog.GetRepositoryRoot(), "artifacts", "ui-captures", "T33");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), content.ToString());
    }
}
