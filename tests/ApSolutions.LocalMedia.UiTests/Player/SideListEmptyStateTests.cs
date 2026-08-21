// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The five lists in the player's side column say something when there is nothing in them.
/// </summary>
/// <remarks>
/// <para>
/// Measured on 2026-08-20: not one of them carried an empty string, so a person who opened the
/// track selector on a file with a single audio track found a panel with nothing in it and no way to
/// tell that from something still loading.
/// </para>
/// <para>
/// <b>"Empty" is not the same state in all of them</b>, which is the substance of this. Markers and
/// detections are empty at zero. The track selector <b>never reaches zero</b> — its subtitle list
/// always carries the "off" option the view model adds itself — so its empty is one real option per
/// kind. And the versions list resolves it the other way round from the rest of the tree: it says
/// "only one version" instead of disappearing, because it lives in a column beside three others and
/// one that vanishes moves the rest.
/// </para>
/// </remarks>
public sealed class SideListEmptyStateTests
{
    [AvaloniaFact]
    public void Each_of_the_five_side_lists_says_what_its_own_empty_means()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        Assert.Contains(Resource("MarkersEmptyTitle"), VisibleTexts(new MarkerEditorView()));
        Assert.Contains(Resource("DetectedMarkersEmptyTitle"), VisibleTexts(new DetectedMarkerReviewView()));
        Assert.Contains(Resource("TracksEmptyTitle"), VisibleTexts(new TrackSelectorView()));
        Assert.Contains(Resource("PlayerVersionsEmptyTitle"), VisibleTexts(new PlayerVersionsView()));
        Assert.Contains(Resource("ShortcutsEmptyTitle"), VisibleTexts(new ShortcutSettingsView()));
    }

    /// <summary>
    /// What comes out of the speakers not being what was asked for is a warning, not a line of text.
    /// </summary>
    /// <remarks>
    /// The three of them — no device at all, a layout the hardware refused, a device that went away —
    /// were plain sentences in the same colour as the labels above them, so the one thing on this panel
    /// that needed reading looked like the two that did not. None is a failure: sound still plays, or
    /// is about to. They take the same surface as the unavailable badge, and the glyph is what keeps
    /// the signal off colour alone.
    /// </remarks>
    [AvaloniaFact]
    public void The_three_audio_notices_are_warnings_rather_than_sentences()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var view = new AudioOutputView();
        var window = new Window { Width = 420, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var application = Avalonia.Application.Current!;
        Assert.True(application.TryGetResource("WarningSurfaceBrush", application.ActualThemeVariant, out var warning));
        var expected = Assert.IsAssignableFrom<ISolidColorBrush>(warning).Color;

        foreach (var name in new[] { "NoOutputNotice", "DegradedLayoutNotice", "DeviceFallbackNotice" })
        {
            var notice = Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Name == name);
            Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(notice.Background).Color);
            Assert.True(notice.BorderThickness.Top > 0, $"{name} has no border, so its only signal is colour.");
            Assert.Contains(notice.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "⚠");
        }

        window.Close();
    }

    /// <summary>
    /// The words a view puts on screen with nothing bound to it.
    /// </summary>
    /// <remarks>
    /// No data context, which is what leaves every <c>IsVisible</c> at its default and puts every
    /// branch on screen at once. That makes this an upper bound: a string that is not here is a string
    /// no state can reach, which is exactly what "the four say nothing when empty" meant.
    /// </remarks>
    private static string[] VisibleTexts(Control view)
    {
        var window = new Window { Width = 420, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var texts = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToArray();
        window.Close();
        return texts;
    }

    private static string Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        return Assert.IsType<string>(value);
    }
}
