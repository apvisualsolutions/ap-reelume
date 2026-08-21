// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The status overlay may only say what the engine reported. These tests drive it with each reported
/// state and check that exactly the matching line is visible.
/// </summary>
public sealed class VideoStatusOverlayTests
{
    /// <summary>
    /// A fact about the decode and a warning about it are not painted the same way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All six lines used to share one surface, so "HDR10 is passing through" — a video playing exactly
    /// as asked — looked identical to "this fell back to software". §4 splits them: the four facts read
    /// as quiet caption text, the two warnings take the warning surface and the glyph.
    /// </para>
    /// <para>
    /// What is asserted is that the warning box is <b>absent</b> while only facts are on screen. A test
    /// that merely found the box when a warning was present would pass a badge that drew it always.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void A_decode_fact_and_a_decode_warning_are_not_the_same_surface()
    {
        // Build already mounts the view in a window; a second one would try to reparent it.
        var view = Build(out var viewModel);

        var warning = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "VideoStatusWarningSurface");

        viewModel.Apply(
            new PlaybackCapabilities(true, true, HdrFormat.Hdr10, true, VideoOutputPath.Hdr10Passthrough),
            fellBackToSoftware: false);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.HasDecodeFacts);
        Assert.False(viewModel.HasDecodeWarnings);
        Assert.False(warning.IsVisible);

        viewModel.Apply(
            new PlaybackCapabilities(false, false, HdrFormat.None, false, VideoOutputPath.Sdr),
            fellBackToSoftware: true);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.HasDecodeWarnings);
        Assert.True(warning.IsVisible);
    }

    [AvaloniaFact]
    public void Nothing_is_shown_until_the_engine_has_reported_something()
    {
        var view = Build(out var viewModel);
        var surface = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "VideoStatusSurface");

        Assert.False(viewModel.HasStatus);
        Assert.False(surface.IsVisible);
    }

    [AvaloniaFact]
    public void Each_reported_path_shows_exactly_its_own_line()
    {
        var view = Build(out var viewModel);
        // Keyed by name, so the warning box's glyph — which has none — is left out rather than
        // throwing on a null key.
        var lines = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => !string.IsNullOrEmpty(block.Name))
            .ToDictionary(block => block.Name!, block => block);

        viewModel.Apply(
            new PlaybackCapabilities(true, true, HdrFormat.Hdr10, true, VideoOutputPath.Hdr10Passthrough),
            fellBackToSoftware: false);
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["HdrPassthroughText"].IsVisible);
        Assert.False(lines["ToneMappedText"].IsVisible);
        Assert.False(lines["StandardRangeText"].IsVisible);
        Assert.True(lines["HardwareText"].IsVisible);
        Assert.False(lines["SoftwareFallbackText"].IsVisible);

        viewModel.Apply(
            new PlaybackCapabilities(true, true, HdrFormat.Hdr10, false, VideoOutputPath.SdrToneMapped),
            fellBackToSoftware: false);
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["ToneMappedText"].IsVisible);
        Assert.False(lines["HdrPassthroughText"].IsVisible);

        viewModel.Apply(
            new PlaybackCapabilities(false, false, HdrFormat.None, false, VideoOutputPath.Sdr),
            fellBackToSoftware: false);
        Dispatcher.UIThread.RunJobs();
        Assert.True(lines["StandardRangeText"].IsVisible);
        Assert.False(lines["HardwareText"].IsVisible);
    }

    [AvaloniaFact]
    public void A_software_fallback_is_announced_in_text()
    {
        var view = Build(out var viewModel);
        var fallback = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(block => block.Name == "SoftwareFallbackText");

        viewModel.Apply(
            new PlaybackCapabilities(true, false, HdrFormat.None, false, VideoOutputPath.Sdr),
            fellBackToSoftware: true);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.FellBackToSoftware);
        Assert.False(viewModel.IsHardwareAccelerated);
        Assert.True(fallback.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(fallback.Text));
    }

    [AvaloniaFact]
    public void Dolby_Vision_is_announced_as_unsupported_rather_than_silently_degraded()
    {
        var view = Build(out var viewModel);
        var unsupported = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(block => block.Name == "UnsupportedFormatText");

        viewModel.Apply(
            new PlaybackCapabilities(true, true, HdrFormat.DolbyVision, true, VideoOutputPath.SdrToneMapped),
            fellBackToSoftware: false);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsUnsupportedFormat);
        Assert.True(unsupported.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(unsupported.Text));
    }

    [AvaloniaFact]
    public void The_overlay_is_named_and_captured_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var captures = Path.Combine(RepositoryLayout.Root, "artifacts", "ui-captures", "T22");
        Directory.CreateDirectory(captures);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            var viewModel = new VideoStatusViewModel();
            var view = new VideoStatusOverlay { DataContext = viewModel };
            var window = new Window { Width = 480, Height = 240, Content = view };
            window.Show();
            viewModel.Apply(
                new PlaybackCapabilities(true, true, HdrFormat.Hdr10, true, VideoOutputPath.Hdr10Passthrough),
                fellBackToSoftware: false);
            Dispatcher.UIThread.RunJobs();

            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(view)));
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(
                Path.Combine(captures, $"video-status-{cultureName}.png"),
                PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    private static VideoStatusOverlay Build(out VideoStatusViewModel viewModel)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        viewModel = new VideoStatusViewModel();
        var view = new VideoStatusOverlay { DataContext = viewModel };
        var window = new Window { Width = 480, Height = 240, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }
}
