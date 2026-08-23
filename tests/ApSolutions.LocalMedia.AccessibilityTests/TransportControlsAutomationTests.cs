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

    /// <summary>
    /// The scrubber: absent until the engine says how long the file is, then named, focusable, and
    /// scaled to the duration — with both clocks beside it saying where the session is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asserted on the view model as well as on the control, and that is the lesson this repository
    /// paid for on 2026-08-22: five properties read only by a <c>DataTemplate</c> are five properties
    /// no test covers, and the file they lived in went from 83/100 to 38/50 on CI without a red.
    /// </para>
    /// <para>
    /// The absent case goes first because it is the one a screenshot never shows. Until the engine
    /// answers, <c>Duration</c> is null, and a bar whose maximum is unknown would put its thumb
    /// wherever it liked.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task The_scrubber_arrives_with_the_duration_and_says_where_the_session_is()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var viewModel = new TransportControlsViewModel(new ControlPlayback(new StubEngine()));

        // Nothing known yet: no bar, no length, and the position reads as a clock rather than blank.
        Assert.False(viewModel.HasDuration);
        Assert.Equal(string.Empty, viewModel.DurationLabel);
        Assert.Equal("0:00", viewModel.PositionLabel);
        Assert.Equal(1.0, viewModel.DurationSeconds);

        var view = new TransportControlsView { DataContext = viewModel };
        var window = new Window { Width = 640, Height = 320, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        // Present in the tree and not drawn, which is what IsVisible on the row means: Avalonia
        // realises the children of a hidden panel, so "is it there" is the wrong question and
        // "would anybody see it" is the right one.
        var hidden = Assert.Single(
            view.GetVisualDescendants().OfType<Slider>(),
            slider => slider.Name == "PositionSlider");
        Assert.False(hidden.IsEffectivelyVisible, "The scrubber is drawn before the duration is known.");

        // The engine answers with a ten-minute file, and the row arrives with it.
        await viewModel.SeekAsync(TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.HasDuration);
        Assert.Equal("2:00", viewModel.PositionLabel);
        Assert.Equal("10:00", viewModel.DurationLabel);
        Assert.Equal(120.0, viewModel.PositionSeconds);
        Assert.Equal(600.0, viewModel.DurationSeconds);

        var scrubber = Assert.Single(
            view.GetVisualDescendants().OfType<Slider>(),
            slider => slider.Name == "PositionSlider");
        Assert.True(scrubber.IsEffectivelyVisible, "The scrubber never appeared with the duration.");
        Assert.False(
            string.IsNullOrWhiteSpace(AutomationProperties.GetName(scrubber)),
            "The scrubber has no accessible name.");
        Assert.True(scrubber.Focusable, "The scrubber cannot take focus.");
        Assert.Equal(0, scrubber.Minimum);
        Assert.Equal(600.0, scrubber.Maximum);
        Assert.Equal(120.0, scrubber.Value);

        // And the two clocks either side of it, which are the whole point of the row.
        var clocks = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.Classes.Contains("transport-clock"))
            .Select(text => text.Text ?? string.Empty)
            .ToArray();
        Assert.Equal(["2:00", "10:00"], clocks);

        // The guard, and it is the half of the fix that the notification order does not cover: a
        // value that arrives while the bar's maximum is not the file's length is a clamp and not a
        // choice. Reproduced by giving the bar a maximum that is not the duration and then moving the
        // thumb — which is exactly the state that took a two-minute position to 0:01 before the fix.
        scrubber.Maximum = 300;
        scrubber.Value = 150;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(TimeSpan.FromMinutes(2), viewModel.Position);
        window.Close();
    }

    /// <summary>The hour appears only when there is one, which is what a two-hour film needs.</summary>
    [Fact]
    public void A_position_past_the_hour_is_written_with_it()
    {
        Assert.Equal("0:00", PlaybackClock.Format(TimeSpan.Zero));
        Assert.Equal("9:59", PlaybackClock.Format(TimeSpan.FromSeconds(599)));
        Assert.Equal("59:59", PlaybackClock.Format(TimeSpan.FromSeconds(3599)));
        Assert.Equal("1:00:00", PlaybackClock.Format(TimeSpan.FromHours(1)));
        Assert.Equal("2:00:00", PlaybackClock.Format(TimeSpan.FromHours(2)));
    }

    /// <summary>The volume is a number beside the thumb, which it never was.</summary>
    [Fact]
    public async Task The_volume_is_written_as_a_number_and_not_only_as_a_thumb()
    {
        var viewModel = new TransportControlsViewModel(new ControlPlayback(new StubEngine()));

        await viewModel.SetVolumeAsync(80, TestContext.Current.CancellationToken);
        Assert.Equal("80 %", viewModel.VolumeLabel);

        // Above a hundred is the case the readout is worth most: the limiter comes on there, and the
        // thumb alone cannot say whether it is at 110 or at 200.
        await viewModel.SetVolumeAsync(180, TestContext.Current.CancellationToken);
        Assert.Equal("180 %", viewModel.VolumeLabel);
        Assert.True(viewModel.IsBoosted);
    }

    /// <summary>
    /// The speed menu's command: every item hands over its literal text, parsed invariant, so the
    /// engine hears the same step on every machine's locale — and «Volver a 1×» exists only while
    /// there is something to come back from.
    /// </summary>
    [AvaloniaFact]
    public async Task The_menus_press_reaches_the_engine_and_the_reset_row_knows_when_to_exist()
    {
        var engine = new StubEngine();
        var viewModel = new TransportControlsViewModel(new ControlPlayback(engine));
        var announced = new List<string>();
        viewModel.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        Assert.False(viewModel.IsAwayFromNormalSpeed);

        Assert.True(viewModel.SetSpeedCommand.CanExecute("2"));
        viewModel.SetSpeedCommand.Execute("2");
        await WaitUntilAsync(() => engine.LastSpeed == 2.0, "the menu's 2× never reached the engine");
        Assert.True(viewModel.IsAwayFromNormalSpeed);
        Assert.Contains(nameof(viewModel.IsAwayFromNormalSpeed), announced);

        // The literal is invariant on purpose: "1.25" must be a step, not a locale accident.
        viewModel.SetSpeedCommand.Execute("1.25");
        await WaitUntilAsync(() => engine.LastSpeed == 1.25, "the menu's 1,25× never reached the engine");

        viewModel.SetSpeedCommand.Execute("1");
        await WaitUntilAsync(() => engine.LastSpeed == 1.0, "coming back to 1× never reached the engine");
        Assert.False(viewModel.IsAwayFromNormalSpeed);

        // A parameter that is not a number never executes: the guard is the difference between a
        // wrong menu item and a crash in a command whose exceptions nobody awaits.
        Assert.False(viewModel.SetSpeedCommand.CanExecute("fast"));
        Assert.False(viewModel.SetSpeedCommand.CanExecute(null));
    }

    /// <summary>
    /// The constructor's null half, asked on purpose: the throw is a branch like any other, and a
    /// model built over nothing must say so at the door rather than on the first press.
    /// </summary>
    [AvaloniaFact]
    public void A_transport_over_nothing_refuses_to_be_built()
    {
        Assert.Throws<ArgumentNullException>(() => new TransportControlsViewModel(null!));
    }

    /// <summary>
    /// A duration that is known and zero long: the slider's arithmetic gets one, not zero, for the
    /// same reason the unknown duration does - a maximum equal to the minimum divides by their
    /// difference. The engine can say Zero for a stream it opened and cannot measure.
    /// </summary>
    [AvaloniaFact]
    public async Task A_zero_length_duration_keeps_the_bars_arithmetic_at_one()
    {
        var engine = new StubEngine { Duration = TimeSpan.Zero };
        var viewModel = new TransportControlsViewModel(new ControlPlayback(engine));

        await viewModel.SetVolumeAsync(100, TestContext.Current.CancellationToken);

        Assert.False(viewModel.HasDuration);
        Assert.Equal(1.0, viewModel.DurationSeconds);
    }

    /// <summary>
    /// The menu writes its steps as literals - a MenuFlyout with a conditional last row does not
    /// bind an ItemsSource well - so this is the seam that keeps them being the policy's steps.
    /// The keyboard walks <see cref="PlaybackControlPolicy.SpeedSteps"/>; a menu that drifted from
    /// it would offer the mouse speeds the keyboard cannot reach.
    /// </summary>
    [AvaloniaFact]
    public void The_menus_literal_steps_are_the_policys_steps()
    {
        var markup = File.ReadAllText(TestSupport.RepositoryLayout.PathFromRoot(
            "src", "ApSolutions.LocalMedia.Presentation", "Player", "TransportControlsView.axaml"));
        var offered = System.Text.RegularExpressions.Regex
            .Matches(markup, "CommandParameter=\"([0-9.]+)\"")
            .Select(match => double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(step => step)
            .ToArray();

        Assert.Equal(PlaybackControlPolicy.SpeedSteps.OrderBy(step => step), offered);
    }


    private static async Task WaitUntilAsync(Func<bool> condition, string complaint)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), complaint);
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

        public TimeSpan? Duration { get; set; } = TimeSpan.FromMinutes(10);

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(State, Position, Duration, []));

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
