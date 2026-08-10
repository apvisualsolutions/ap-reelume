// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Guards the T18 acceptance criterion that accessible transport controls compose above the decoded
/// picture. The decoded frame is a flat colour, so a pixel that is no longer that colour proves the
/// overlay was drawn on top rather than behind the video surface.
/// </summary>
public sealed class PlayerOverlayTests
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private static readonly (double Scaling, int Percentage)[] SupportedScalings =
        [(1.0, 100), (1.5, 150), (2.0, 200)];

    [AvaloniaFact]
    public void Transport_controls_stay_inside_the_player_and_above_the_video_at_every_scaling()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));
        var captures = Path.Combine(GetRepositoryRoot(), "artifacts", "ui-captures", "T18");
        Directory.CreateDirectory(captures);

        foreach (var (scaling, percentage) in SupportedScalings)
        {
            var frames = new FlatColourFrameSource(640, 360);
            var view = new PlayerView
            {
                DataContext = new PlayerViewModel(new InertPlaybackSessionCoordinator(), frames),
            };
            var window = new Window { Width = WindowWidth, Height = WindowHeight, Content = view };
            window.SetRenderScaling(scaling);
            window.Show();
            Dispatcher.UIThread.RunJobs();
            frames.Publish();
            Dispatcher.UIThread.RunJobs();

            var surface = view.GetVisualDescendants()
                .OfType<VideoFrameView>()
                .Single();
            var transport = view.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Name == "TransportControlsSurface");
            var composition = Assert.IsType<Panel>(transport.GetVisualParent());
            Assert.Same(composition, surface.GetVisualParent());

            Assert.True(
                composition.Children.IndexOf(transport) > composition.Children.IndexOf(surface),
                "The transport controls must be composed after the video surface.");
            Assert.True(transport.Bounds.Width > 0, $"Transport width was {transport.Bounds.Width} at {percentage}%.");
            Assert.True(transport.Bounds.Height > 0, $"Transport height was {transport.Bounds.Height} at {percentage}%.");

            var origin = transport.TranslatePoint(default, view);
            Assert.NotNull(origin);
            var bottom = origin.Value.Y + transport.Bounds.Height;
            var right = origin.Value.X + transport.Bounds.Width;
            Assert.InRange(bottom, 1.0, view.Bounds.Height);
            Assert.InRange(right, 1.0, view.Bounds.Width);

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var overlayPixel = ReadPixel(
                frame,
                (origin.Value.X + (transport.Bounds.Width / 2)) * scaling,
                (origin.Value.Y + (transport.Bounds.Height / 2)) * scaling);
            var videoPixel = ReadPixel(frame, view.Bounds.Width / 2 * scaling, 40 * scaling);

            Assert.Equal(FlatColourFrameSource.Colour, videoPixel);
            Assert.NotEqual(FlatColourFrameSource.Colour, overlayPixel);

            frame.Save(
                Path.Combine(captures, $"player-overlay-scale-{percentage}.png"),
                PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hiding_the_transport_bar_keeps_every_control_focusable()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var viewModel = new PlayerViewModel(new InertPlaybackSessionCoordinator());
        var view = new PlayerView { DataContext = viewModel };
        var window = new Window { Width = WindowWidth, Height = WindowHeight, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var transport = view.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Name == "TransportControlsSurface");
        var buttons = transport.GetVisualDescendants().OfType<Button>().ToArray();
        Assert.NotEmpty(buttons);
        Assert.True(viewModel.AreControlsRevealed);
        Assert.Equal(0.92, viewModel.ControlsOpacity);

        viewModel.HideControls();
        Dispatcher.UIThread.RunJobs();

        Assert.False(viewModel.AreControlsRevealed);
        Assert.Equal(0.0, viewModel.ControlsOpacity);
        Assert.True(transport.IsVisible, "Hiding must not remove the bar from the tree.");
        Assert.All(buttons, button => Assert.True(button.Focusable, "A hidden control lost keyboard focus."));
        Assert.All(
            buttons,
            button => Assert.Contains(button, transport.GetVisualDescendants().OfType<Button>()));

        viewModel.RevealControls();
        Assert.True(viewModel.AreControlsRevealed);
        window.Close();
    }

    private static (byte Blue, byte Green, byte Red) ReadPixel(WriteableBitmap frame, double x, double y)
    {
        using var buffer = frame.Lock();
        var column = Math.Clamp((int)x, 0, buffer.Size.Width - 1);
        var row = Math.Clamp((int)y, 0, buffer.Size.Height - 1);
        var pixel = new byte[4];
        Marshal.Copy(buffer.Address + (row * buffer.RowBytes) + (column * 4), pixel, 0, 4);
        return (pixel[0], pixel[1], pixel[2]);
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

    /// <summary>Publishes one flat-coloured BGRA frame so overlay pixels are unambiguous.</summary>
    private sealed class FlatColourFrameSource(int width, int height) : IVideoFrameSource
    {
        public static (byte Blue, byte Green, byte Red) Colour => (0, 255, 0);

        public event EventHandler<VideoFrameEventArgs>? FrameRendered;

        public void Publish()
        {
            var stride = width * 4;
            var pixels = new byte[stride * height];
            for (var offset = 0; offset < pixels.Length; offset += 4)
            {
                pixels[offset] = Colour.Blue;
                pixels[offset + 1] = Colour.Green;
                pixels[offset + 2] = Colour.Red;
                pixels[offset + 3] = 255;
            }

            FrameRendered?.Invoke(this, new VideoFrameEventArgs(pixels, width, height, stride));
        }
    }

    /// <summary>A coordinator that answers the view model without touching a real engine.</summary>
    private sealed class InertPlaybackSessionCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession => null;

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromResult(new PlaybackSession(Guid.Empty, request.MediaFileId, request.Path));
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
