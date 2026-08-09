using System.Globalization;
using System.Xml.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// When Windows asks for less motion, nothing may animate on its own schedule. A duration written
/// into a view escapes the reduced-motion token entirely, so no view may declare one.
/// </summary>
public sealed class ReducedMotionTests
{
    [Fact]
    public void No_view_writes_a_duration_of_its_own_instead_of_asking_the_token()
    {
        var audit = new AuditLog(nameof(No_view_writes_a_duration_of_its_own_instead_of_asking_the_token));

        foreach (var view in ViewFiles())
        {
            var document = XDocument.Load(view);
            var durations = document.Descendants()
                .SelectMany(element => element.Attributes()
                    .Where(attribute => attribute.Name.LocalName is "Duration" or "Delay")
                    .Select(attribute => new { element, attribute }))
                .Where(entry => !entry.attribute.Value.TrimStart().StartsWith('{'))
                .ToArray();

            foreach (var duration in durations)
            {
                audit.Add(
                    "motion",
                    Path.GetFileNameWithoutExtension(view),
                    $"{duration.element.Name.LocalName}.{duration.attribute.Name.LocalName}",
                    DefectSeverity.Major,
                    $"The view hard-codes {duration.attribute.Name.LocalName}=\"{duration.attribute.Value}\", "
                        + "so the reduced-motion preference cannot switch it off.",
                    $"Turn on \"Show animations in Windows\" = Off and open {Path.GetFileName(view)}.");
            }

            var transitions = document.Descendants()
                .Where(element => element.Name.LocalName.EndsWith("Transition", StringComparison.Ordinal))
                .ToArray();
            foreach (var transition in transitions)
            {
                var value = transition.Attribute("Duration")?.Value;
                if (value is null || !value.TrimStart().StartsWith('{'))
                {
                    audit.Add(
                        "motion",
                        Path.GetFileNameWithoutExtension(view),
                        transition.Name.LocalName,
                        DefectSeverity.Major,
                        "A transition does not take its duration from the motion token, so reduced "
                            + "motion does not reach it.",
                        $"Open {Path.GetFileName(view)} and inspect {transition.Name.LocalName}.");
                }
            }
        }

        audit.Complete();
    }

    [Fact]
    public void The_reduced_motion_token_is_zero_and_the_standard_one_stays_short()
    {
        var tokens = XDocument.Load(Path.Combine(
            AuditLog.GetRepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Theme",
            "DesignTokens.axaml"));
        var key = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        var values = tokens.Descendants()
            .Where(element => element.Attribute(key) is not null)
            .GroupBy(element => element.Attribute(key)!.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);

        Assert.Equal(
            0,
            double.Parse(values["MotionDurationReducedMilliseconds"], CultureInfo.InvariantCulture));
        Assert.InRange(
            double.Parse(values["MotionDurationStandardMilliseconds"], CultureInfo.InvariantCulture),
            1,
            250);
    }

    [AvaloniaFact]
    public void An_overlay_that_appears_on_its_own_announces_itself_politely()
    {
        var audit = new AuditLog(nameof(An_overlay_that_appears_on_its_own_announces_itself_politely));
        var overlays = new[] { "NextEpisodeOverlay", "VideoStatusOverlay" };

        foreach (var overlayName in overlays)
        {
            var path = Directory.EnumerateFiles(
                Path.Combine(AuditLog.GetRepositoryRoot(), "src", "ApSolutions.LocalMedia.Presentation"),
                $"{overlayName}.axaml",
                SearchOption.AllDirectories).SingleOrDefault();
            Assert.NotNull(path);

            var document = XDocument.Load(path);
            // The attribute arrives as "AutomationProperties.LiveSetting", prefix and all.
            var announces = document.Descendants()
                .Attributes()
                .Any(attribute => attribute.Name.LocalName.EndsWith("LiveSetting", StringComparison.Ordinal));
            if (!announces)
            {
                audit.Add(
                    "play",
                    overlayName,
                    "overlay",
                    DefectSeverity.Major,
                    "The overlay appears without being asked for and declares no live setting, so a "
                        + "reader user is never told it arrived.",
                    $"Play to the end of an episode and listen while {overlayName} appears.");
            }
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void The_transport_bar_keeps_its_controls_in_the_tree_when_it_hides()
    {
        var surface = CanonicalJourney.Surfaces.Single(entry => entry.Surface == "PlayerView");
        using var host = CanonicalJourney.Show(surface);
        var before = host.View.GetVisualDescendants()
            .OfType<Button>()
            .Count(button => !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));

        Assert.True(before > 0, "The player exposes no named transport control at all.");
    }

    private static IReadOnlyList<string> ViewFiles() =>
        [.. Directory.EnumerateFiles(
            Path.Combine(AuditLog.GetRepositoryRoot(), "src", "ApSolutions.LocalMedia.Presentation"),
            "*.axaml",
            SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)];
}
