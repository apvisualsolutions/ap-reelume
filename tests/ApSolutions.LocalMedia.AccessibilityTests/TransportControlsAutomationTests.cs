// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests;

/// <summary>
/// Every transport action must be reachable without a mouse and must announce a name, a state, and
/// its shortcut. Boosted volume must also announce itself in text, not by colour alone.
/// </summary>
public sealed class TransportControlsAutomationTests
{
    private static readonly string[] DeclaredControls =
        ["SkipBackwardButton", "SkipForwardButton", "MuteButton", "VolumeSlider"];

    [AvaloniaFact]
    public void Every_transport_control_has_a_name_a_shortcut_and_keyboard_focus_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            var view = new TransportControlsView
            {
                DataContext = new TransportControlsViewModel(new ControlPlayback(new StubEngine())),
            };
            var window = new Window { Width = 640, Height = 320, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var controls = view.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => DeclaredControls.Contains(control.Name, StringComparer.Ordinal))
                .ToArray();

            Assert.Equal(DeclaredControls.Length, controls.Length);
            Assert.All(controls, control => Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                $"{control.Name} has no accessible name."));
            Assert.All(controls, control => Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetAcceleratorKey(control)),
                $"{control.Name} announces no shortcut."));
            Assert.All(controls, control => Assert.True(control.Focusable, $"{control.Name} cannot take focus."));

            var treePath = Path.Combine(
                RepositoryLayout.Root,
                "artifacts",
                "ui-captures",
                "T21",
                $"transport-uia-{cultureName}.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(treePath)!);
            File.WriteAllLines(treePath, controls.Select(control =>
                $"{control.GetType().Name}|{control.Name}|{AutomationProperties.GetName(control)}|" +
                $"key={AutomationProperties.GetAcceleratorKey(control)}|focusable={control.Focusable}"));
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Boosted_volume_announces_itself_in_text_and_never_without_its_limiter()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var viewModel = new TransportControlsViewModel(new ControlPlayback(new StubEngine()));
        var view = new TransportControlsView { DataContext = viewModel };
        var window = new Window { Width = 640, Height = 320, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var warning = view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "BoostWarningSurface");
        var warningText = view.GetVisualDescendants().OfType<TextBlock>().Single(b => b.Name == "BoostWarningText");
        Assert.False(warning.IsVisible);

        await viewModel.SetVolumeAsync(150, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsBoosted);
        Assert.True(viewModel.LimiterEngaged);
        Assert.True(warning.IsVisible);
        Assert.False(string.IsNullOrWhiteSpace(warningText.Text));
        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(warning)));

        await viewModel.SetVolumeAsync(100, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.False(viewModel.IsBoosted);
        Assert.False(warning.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void Keyboard_alone_reaches_every_transport_action()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var view = new TransportControlsView
        {
            DataContext = new TransportControlsViewModel(new ControlPlayback(new StubEngine())),
        };
        var window = new Window { Width = 640, Height = 320, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var focusable = view.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => DeclaredControls.Contains(control.Name, StringComparer.Ordinal))
            .ToArray();

        foreach (var control in focusable)
        {
            Assert.True(control.Focus(NavigationMethod.Tab), $"{control.Name} refused keyboard focus.");
            Dispatcher.UIThread.RunJobs();
        }

        window.Close();
    }

    [AvaloniaFact]
    public async Task A_hundred_rapid_commands_leave_a_deterministic_state()
    {
        var engine = new StubEngine();
        var viewModel = new TransportControlsViewModel(new ControlPlayback(engine));

        for (var iteration = 0; iteration < 100; iteration++)
        {
            await viewModel.SetSpeedAsync(iteration % 2 == 0 ? 4.0 : 0.25, TestContext.Current.CancellationToken);
            await viewModel.SetVolumeAsync(iteration % 2 == 0 ? 200 : 40, TestContext.Current.CancellationToken);
        }

        Assert.Equal(0.25, viewModel.SpeedMultiplier);
        Assert.Equal(40, viewModel.VolumePercent);
        Assert.False(viewModel.IsBoosted);
        Assert.False(viewModel.LimiterEngaged);
        Assert.Equal(0.25, engine.LastSpeed);
        Assert.False(engine.LastVolume!.IsBoosted);
    }

    private sealed class StubEngine : IMediaPlayerEngine
    {
#pragma warning disable CS0067 // The contract declares these events; this double never raises them.
        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

        public event EventHandler<PlaybackFailureEventArgs>? Failure;
#pragma warning restore CS0067

        public PlaybackState State => PlaybackState.Playing;

        public TimeSpan Position { get; private set; } = TimeSpan.FromMinutes(1);

        public double LastSpeed { get; private set; } = 1.0;

        public VolumeDecision? LastVolume { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
        {
            Position = position;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(State, Position, TimeSpan.FromMinutes(10), []));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle, IsExternal: true));

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default)
        {
            LastSpeed = multiplier;
            return Task.CompletedTask;
        }

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default)
        {
            Assert.False(decision.IsBoosted && !decision.LimiterEngaged);
            LastVolume = decision;
            return Task.CompletedTask;
        }

        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
