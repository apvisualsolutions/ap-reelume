// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// Subtitles are the accessibility feature for anyone who cannot rely on the audio, so their own
/// controls have to be operable, announced, and wide enough to be useful.
/// </summary>
public sealed class SubtitleCustomizationTests
{
    private static readonly string[] DeclaredControls =
    [
        "SubtitleSizeSlider",
        "SubtitleFamilySelector",
        "SubtitleForegroundFirst",
        "SubtitleBackgroundFirst",
        "SubtitleBackgroundOpacitySlider",
        "SubtitleOutlineSlider",
    ];

    [AvaloniaFact]
    public void Every_subtitle_control_is_named_focusable_and_announces_its_value()
    {
        var audit = new AuditLog(nameof(Every_subtitle_control_is_named_focusable_and_announces_its_value));
        var surface = CanonicalJourney.Surfaces.Single(entry => entry.Surface == nameof(SubtitleStyleView));

        foreach (var language in new[] { "es-ES", "en-US" })
        {
            using var host = CanonicalJourney.Show(surface, language);
            foreach (var name in DeclaredControls)
            {
                var control = host.View.GetVisualDescendants()
                    .OfType<Control>()
                    .SingleOrDefault(candidate => candidate.Name == name);
                Assert.NotNull(control);

                var peer = ControlAutomationPeer.CreatePeerForElement(control);
                if (string.IsNullOrWhiteSpace(peer.GetName()))
                {
                    audit.Add(
                        "settings",
                        nameof(SubtitleStyleView),
                        name,
                        DefectSeverity.Critical,
                        "The subtitle control announces no name.",
                        $"Open the subtitle settings in {language} and focus {name}.");
                }

                if (!control.Focusable)
                {
                    audit.Add(
                        "settings",
                        nameof(SubtitleStyleView),
                        name,
                        DefectSeverity.Critical,
                        "The subtitle control cannot take keyboard focus.",
                        $"Open the subtitle settings in {language} and tab to {name}.");
                }

                // A swatch's name IS its value — the accessible name of the first ink is «#FFFFFF» —
                // so there is nothing left for a value pattern to announce. The two colours were
                // text boxes until 2026-08-25 and a text box does carry one; what replaced them is a
                // row of buttons, and a button that announced "#FFFFFF, value #FFFFFF" would be
                // saying it twice. The name is still asserted above, which is the part that matters.
                var nameIsTheValue = name is "SubtitleForegroundFirst" or "SubtitleBackgroundFirst";
                var announcesValue = nameIsTheValue
                    || peer.GetProvider<IRangeValueProvider>() is not null
                    || peer.GetProvider<IValueProvider>() is not null
                    || peer.GetProvider<ISelectionProvider>() is not null;
                if (!announcesValue)
                {
                    audit.Add(
                        "settings",
                        nameof(SubtitleStyleView),
                        name,
                        DefectSeverity.Major,
                        "The control carries a value but exposes no pattern that announces it.",
                        $"Open the subtitle settings in {language}, focus {name} and listen.");
                }
            }
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void The_preview_states_the_style_in_words_instead_of_only_showing_it()
    {
        var surface = CanonicalJourney.Surfaces.Single(entry => entry.Surface == nameof(SubtitleStyleView));
        using var host = CanonicalJourney.Show(surface);

        var preview = host.View.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.Name == "SubtitlePreviewSurface");
        Assert.False(
            string.IsNullOrWhiteSpace(AutomationProperties.GetName(preview)),
            "The subtitle preview has no accessible name, so it is a picture with no caption.");
    }

    [Fact]
    public void The_offered_range_reaches_the_sizes_low_vision_readers_need()
    {
        Assert.True(
            SubtitleStyle.MaximumFontSizePercent >= 200,
            $"Subtitles only scale to {SubtitleStyle.MaximumFontSizePercent} %.");
        Assert.True(
            SubtitleStyle.MinimumFontSizePercent <= 75,
            $"Subtitles never go below {SubtitleStyle.MinimumFontSizePercent} %.");
        Assert.True(
            SubtitleStyle.MaximumOutlineThickness >= 2,
            "The outline cannot be made thick enough to separate text from a bright picture.");
    }
}
