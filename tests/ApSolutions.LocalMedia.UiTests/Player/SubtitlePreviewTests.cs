// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Domain.Continuity;
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
/// The subtitle preview shows the subtitle that was chosen.
/// </summary>
/// <remarks>
/// <para>
/// It used to take the shell's card surface and the theme's text colour, so <b>four of the five
/// controls above it changed nothing anybody could see</b>: the two colours, the opacity and the
/// outline all fed a preview that only ever showed the font family. A panel called a preview that
/// previews one setting out of five is the house defect with the friendliest possible name.
/// </para>
/// <para>
/// It also sits on the player's surface now rather than the shell's, which is §4's ask and the
/// difference between judging a colour against the grey of a settings page and against the black a
/// film is actually letterboxed into.
/// </para>
/// </remarks>
public sealed class SubtitlePreviewTests
{
    [AvaloniaFact]
    public void The_preview_paints_the_colours_that_were_chosen()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var viewModel = new SubtitleStyleViewModel(new StubPreferenceRepository());
        var view = new SubtitleStyleView { DataContext = viewModel };
        var window = new Window { Width = 480, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        viewModel.ForegroundHex = "#FF0000";
        viewModel.BackgroundHex = "#0000FF";
        viewModel.BackgroundOpacity = 1;
        Dispatcher.UIThread.RunJobs();

        var text = Assert.Single(
            view.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Name == "SubtitlePreviewText");
        var box = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "SubtitlePreviewBox");

        Assert.Equal(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(text.Foreground).Color);
        Assert.Equal(Colors.Blue, Assert.IsAssignableFrom<ISolidColorBrush>(box.Background).Color);

        // And the surface underneath is the player's, not the shell's: a colour judged against the grey
        // of a settings page is not the colour anybody will see over a film.
        var surface = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "SubtitlePreviewSurface");
        var application = Avalonia.Application.Current!;
        Assert.True(application.TryGetResource("PlayerSurfaceBrush", application.ActualThemeVariant, out var player));
        Assert.Equal(
            Assert.IsAssignableFrom<ISolidColorBrush>(player).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color);

        window.Close();
    }

    /// <summary>
    /// Half a hex value keeps the preview up rather than taking it down.
    /// </summary>
    /// <remarks>
    /// Both colours are typed into text boxes, so every intermediate state of every keystroke reaches
    /// the converter: "#", "#F", "#FF00" and an empty box are all real inputs. A converter that threw
    /// on them would fail the panel while somebody was still typing, so an unreadable value falls back
    /// to what a subtitle is — opaque white.
    /// </remarks>
    [AvaloniaFact]
    public void A_half_typed_colour_falls_back_instead_of_failing()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var converter = new SubtitleColourConverter();

        // Measured rather than assumed: Avalonia accepts the short forms, so "#FF00" is a real colour
        // (ARGB) and not a half-typed one. What is genuinely unreadable is shorter or not hex at all.
        foreach (var typed in new[] { "#", string.Empty, "not a colour", "#12345" })
        {
            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(
                converter.Convert(typed, typeof(IBrush), null, CultureInfo.InvariantCulture));
            Assert.Equal(Colors.White, brush.Color);
        }

        var half = Assert.IsAssignableFrom<ISolidColorBrush>(
            converter.Convert("#00FF00", typeof(IBrush), "0.5", CultureInfo.InvariantCulture));
        Assert.Equal(127, half.Color.A);
        Assert.Equal(255, half.Color.G);

        Assert.Throws<NotSupportedException>(
            () => converter.ConvertBack("#FFFFFF", typeof(string), null, CultureInfo.InvariantCulture));
    }

    /// <summary>Nothing is being stored here; the preview is what these tests are about.</summary>
    private sealed class StubPreferenceRepository : IPlaybackPreferenceRepository
    {
        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PlaybackPreference?>(null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
