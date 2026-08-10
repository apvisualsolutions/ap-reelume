// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
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
        var lines = view.GetVisualDescendants().OfType<TextBlock>().ToDictionary(block => block.Name!, block => block);

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
        var captures = Path.Combine(GetRepositoryRoot(), "artifacts", "ui-captures", "T22");
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

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
