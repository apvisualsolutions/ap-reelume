// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The mini player's own five controls, and the thing that used to throw them away.
/// </summary>
/// <remarks>
/// <para>
/// The window declared a surface in its markup and never showed it: <c>PlayerWindowCoordinator</c>
/// assigns <c>window.Content</c>, which replaces the whole tree the AXAML built. So anything the
/// window declared for itself — a panel, a button, a chrome bar — was gone the moment a session
/// moved in. It went unnoticed because the only thing declared was an empty black panel, and the
/// stage that replaced it is also black.
/// </para>
/// <para>
/// That is why these assertions are made <b>after</b> a mode change rather than on a freshly
/// constructed window: a window that only holds its chrome before the session arrives holds no
/// chrome at all in the one mode it exists for.
/// </para>
/// </remarks>
public sealed class MiniPlayerChromeTests
{
    private static readonly MediaFileId MediaFile = new(new Guid("55555555-5555-5555-5555-555555555555"));

    /// <summary>The five, by the resource key that is both their label and their accessible name.</summary>
    private static readonly string[] Chrome =
    [
        "MiniPlayerPlayPause",
        "MiniPlayerSkipBack",
        "MiniPlayerSkipForward",
        "MiniPlayerRestore",
        "MiniPlayerClose",
    ];

    [AvaloniaFact]
    public async Task The_mini_window_carries_its_five_controls_once_a_session_moves_in()
    {
        var (window, view, viewModel) = await ShowPlayingAsync();

        var mini = MiniWindow(view);
        var present = mini.GetVisualDescendants()
            .OfType<Button>()
            .Select(button => button.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var missing = Chrome.Where(name => !present.Contains(name)).ToArray();
        Assert.True(
            missing.Length == 0,
            $"The mini player is missing {string.Join(", ", missing)}. It holds "
                + $"{(present.Count == 0 ? "no named buttons at all" : string.Join(", ", present.Order(StringComparer.Ordinal)))}.");
        window.Close();
        _ = viewModel;
    }

    /// <summary>
    /// The chrome and the picture share the window instead of replacing each other. Without this the
    /// five controls could be present and the video gone, which is the same defect facing the other
    /// way.
    /// </summary>
    [AvaloniaFact]
    public async Task The_picture_and_the_chrome_are_both_inside_the_mini_window()
    {
        var (window, view, _) = await ShowPlayingAsync();

        var stage = view.FindControl<Panel>("PlayerStage")
            ?? throw new InvalidOperationException("No player stage.");
        var mini = MiniWindow(view);

        Assert.Same(mini, stage.GetVisualAncestors().OfType<Window>().FirstOrDefault());
        Assert.Contains(
            mini.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "MiniPlayerPlayPause");
        window.Close();
    }

    /// <summary>
    /// Every one of the five names itself, because a control the walk cannot aim at is a control
    /// nobody presses.
    /// </summary>
    [AvaloniaFact]
    public async Task Each_of_the_five_carries_an_accessible_name()
    {
        var (window, view, _) = await ShowPlayingAsync();
        var mini = MiniWindow(view);

        foreach (var name in Chrome)
        {
            var button = mini.GetVisualDescendants()
                .OfType<Button>()
                .SingleOrDefault(candidate => candidate.Name == name);

            Assert.True(button is not null, $"{name} is not in the mini player's chrome.");
            Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(button!)),
                $"{name} has no accessible name, so a screen reader announces a button with no work.");
        }

        window.Close();
    }

    /// <summary>
    /// The <c>player-chrome</c> class reaches the element that paints, not only the control.
    /// </summary>
    /// <remarks>
    /// A setter on a <c>Button</c> is not the same as a setter on what draws it — measured in phase
    /// 2a, where a <c>Background</c> on the button lost to the base theme outright. So the corner
    /// radius is read off the presenter, and the token is resolved rather than written down here: a
    /// test carrying its own copy of 8 would agree with itself while the theme said something else.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_chrome_class_reaches_the_element_that_paints()
    {
        var (window, view, _) = await ShowPlayingAsync();
        var mini = MiniWindow(view);
        // The pill and not the medium radius since 2026-08-25: on a 44 by 44 target the medium one
        // is a square with its corners taken off, and «nunca cuadrados» is the rule now.
        var expected = Assert.IsType<CornerRadius>(
            Avalonia.Application.Current!.TryFindResource("CornerRadiusPill", out var token)
                ? token
                : null);
        Assert.True(expected.TopLeft > 0, "CornerRadiusPill resolved to nothing, so this proves nothing.");

        foreach (var name in Chrome)
        {
            var button = mini.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == name);

            Assert.Contains("player-chrome", button.Classes);
            Assert.True(button.MinWidth >= 36 && button.MinHeight >= 36, $"{name} is smaller than the target area.");

            var presenter = button.GetVisualDescendants()
                .OfType<ContentPresenter>()
                .FirstOrDefault();
            Assert.True(presenter is not null, $"{name} has no presenter, so nothing painted it.");
            Assert.Equal(expected, presenter!.CornerRadius);
        }

        window.Close();
    }

    /// <summary>
    /// The row separates its own five, because the class no longer does it for them.
    /// </summary>
    /// <remarks>
    /// The separation used to be a <c>Margin</c> on <c>player-chrome</c>, which worked while the mini
    /// player was the only thing wearing it. The large transport adopting the class is what took it
    /// out: it places its three in a panel that already spaces them, and four a side on top of that
    /// pushes them twenty apart. So the spacing moved to whoever does the placing, and this asserts
    /// that the mini player picked up what the class put down — without it, removing that setter
    /// would leave these five touching and nothing would say so.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_chrome_row_spaces_its_own_five()
    {
        var (window, view, _) = await ShowPlayingAsync();
        var mini = MiniWindow(view);

        var row = mini.GetVisualDescendants()
            .OfType<WrapPanel>()
            .SingleOrDefault(panel => panel.Name == "MiniPlayerChromeSurface");

        Assert.True(row is not null, "The mini player's chrome row is not in the window.");
        Assert.True(
            row!.ItemSpacing > 0 && row.LineSpacing > 0,
            $"The chrome row spaces its buttons by {row.ItemSpacing}x{row.LineSpacing}, so five "
                + "controls that used to be separated by the class are now touching.");

        foreach (var name in Chrome)
        {
            var button = mini.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == name);

            Assert.Equal(default, button.Margin);
        }

        window.Close();
    }

    /// <summary>
    /// The prototype's composition, in the order it draws it: the bar of progress above the row, and
    /// the row carrying the readout beside the five.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written as geometry rather than as a list of names on purpose. "The bar is declared" is what
    /// a grep answers, and the defect it would miss is the one this repository keeps finding: a
    /// control that exists, is bound, and is drawn where nobody looks. What is asserted is that the
    /// bar's foot is at or above the row's head - which is the only reading of "over the five
    /// buttons" a person watching the window would agree with.
    /// </para>
    /// <para>
    /// Three pixels is the prototype's number and it is asserted, because a <c>ProgressBar</c> in
    /// this theme has a minimum height of four: the three setters that get past it are exactly the
    /// kind of thing a later tidy-up removes, and the bar would silently grow by a third.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task The_bar_of_progress_is_three_pixels_and_sits_over_the_five()
    {
        var (window, view, viewModel) = await ShowPlayingAsync();
        var mini = MiniWindow(view);

        // A length first, because the fill is absent without one and an absent control measures
        // nothing: asserting three pixels against a bar that is not on screen reads zero and passes
        // for the wrong reason the moment the number stops being three.
        viewModel.Player!.Player.Transport!.Observe(
            TimeSpan.FromMinutes(52),
            TimeSpan.FromMinutes(96));
        Dispatcher.UIThread.RunJobs();

        var bar = mini.GetVisualDescendants()
            .OfType<ProgressBar>()
            .Single(control => control.Name == "MiniPlayerProgress");
        var row = mini.GetVisualDescendants()
            .OfType<WrapPanel>()
            .Single(panel => panel.Name == "MiniPlayerChromeSurface");

        Assert.Equal(3, bar.Bounds.Height);

        var barFoot = bar.TranslatePoint(new Point(0, bar.Bounds.Height), mini);
        var rowHead = row.TranslatePoint(new Point(0, 0), mini);
        Assert.NotNull(barFoot);
        Assert.NotNull(rowHead);
        Assert.True(
            barFoot!.Value.Y <= rowHead!.Value.Y,
            $"the bar of progress ends at y={barFoot.Value.Y:0.0} and the five start at "
                + $"y={rowHead.Value.Y:0.0}, so it is not over them.");

        window.Close();
    }

    /// <summary>
    /// The band says what is playing and where it is, which the window did not say at all.
    /// </summary>
    /// <remarks>
    /// The text is read off the screen and compared against the model rather than against a string
    /// written here: a test carrying its own copy of the composed line agrees with itself while the
    /// binding points at nothing, which is the state this view was in for the title until today.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_band_carries_the_title_and_the_clock_of_what_is_playing()
    {
        var (window, view, viewModel) = await ShowPlayingAsync();
        var mini = MiniWindow(view);

        viewModel.Player!.Player.Transport!.Observe(
            TimeSpan.FromMinutes(52),
            TimeSpan.FromMinutes(96));
        Dispatcher.UIThread.RunJobs();

        var title = Text(mini, "MiniPlayerTitleText");
        var clock = Text(mini, "MiniPlayerClockText");

        Assert.Equal(viewModel.PlayerTitle, title.Text);
        Assert.False(
            string.IsNullOrWhiteSpace(title.Text),
            "the mini player's title is empty, so the binding reaches nothing.");
        Assert.Equal(viewModel.Player.Player.Transport.Readout, clock.Text);
        Assert.Contains("52:00", clock.Text ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("1:36:00", clock.Text ?? string.Empty, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>
    /// The bar follows the playhead, and stays away until there is a length for it to be a fraction
    /// of.
    /// </summary>
    /// <remarks>
    /// The absence is half the assertion and the more expensive half to get wrong:
    /// <c>DurationSeconds</c> answers 1 until the engine says otherwise, so a position of fifty-two
    /// minutes against that maximum is clamped and paints a <b>full</b> bar over a film that has
    /// barely started.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_bar_is_away_until_there_is_a_length_and_then_follows_the_playhead()
    {
        var (window, view, viewModel) = await ShowPlayingAsync();
        var mini = MiniWindow(view);
        var transport = viewModel.Player!.Player.Transport!;

        var bar = mini.GetVisualDescendants()
            .OfType<ProgressBar>()
            .Single(control => control.Name == "MiniPlayerProgress");

        transport.Observe(TimeSpan.FromMinutes(52), duration: null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(bar.IsVisible, "the bar is on screen before the engine has said how long the file is.");

        transport.Observe(TimeSpan.FromMinutes(52), TimeSpan.FromMinutes(96));
        Dispatcher.UIThread.RunJobs();

        Assert.True(bar.IsVisible);
        Assert.Equal(5760, bar.Maximum);
        Assert.Equal(3120, bar.Value);

        window.Close();
    }

    /// <summary>
    /// The band keeps its height when the length arrives, because a window sized to 16:9 plus the
    /// chrome would stop being either.
    /// </summary>
    /// <remarks>
    /// This is why the track is a panel of its own that is always there and only the fill comes and
    /// goes. Hiding the whole three pixels would move the picture under a window nobody touched: the
    /// resize handler puts 16:9 back on the video and adds the chrome's height on top, and it only
    /// runs on a drag.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_band_is_exactly_as_tall_before_and_after_the_length_arrives()
    {
        var (window, view, viewModel) = await ShowPlayingAsync();
        var mini = MiniWindow(view);
        var chrome = mini.GetVisualDescendants().OfType<MiniPlayerChromeView>().Single();
        var transport = viewModel.Player!.Player.Transport!;

        transport.Observe(TimeSpan.FromSeconds(1), duration: null);
        Dispatcher.UIThread.RunJobs();
        var before = chrome.Bounds.Height;

        transport.Observe(TimeSpan.FromMinutes(52), TimeSpan.FromMinutes(96));
        Dispatcher.UIThread.RunJobs();

        Assert.True(before > 0, "the chrome measured nothing, so this compares two zeroes.");
        Assert.Equal(before, chrome.Bounds.Height);

        window.Close();
    }

    private static TextBlock Text(Window mini, string name) =>
        mini.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == name);

    private static Window MiniWindow(ShellView view)
    {
        var stage = view.FindControl<Panel>("PlayerStage")
            ?? throw new InvalidOperationException("No player stage.");
        return stage.GetVisualAncestors().OfType<Window>().FirstOrDefault()
            ?? throw new InvalidOperationException("The stage is under no window at all.");
    }

    private static async Task<(Window Window, ShellView View, ShellViewModel ViewModel)> ShowPlayingAsync()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));
        var viewModel = CreateViewModel();
        var view = new ShellView { DataContext = viewModel };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        return (window, view, viewModel);
    }

    private static ShellViewModel CreateViewModel() => new(
        new NavigationService(),
        new ShellSurfaces
        {
            OpenPlayer = (_, _) => Task.FromResult<PlayerSurfaces?>(new PlayerSurfaces
            {
                // A title and a transport, because the band draws both and a session without them
                // would let the two new bindings point at nothing and still pass.
                Title = "El Faro de Piedra",
                Player = new PlayerViewModel(new InertCoordinator())
                {
                    Transport = new TransportControlsViewModel(
                        new ControlPlayback(new SpeedMenuTests.RecordingEngine())),
                },
            }),
            ClosePlayer = _ => Task.CompletedTask,
            ChangePlaybackMode = (mode, _) => Task.FromResult(mode),
        });

    private sealed class InertCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession => null;

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlaybackSession(Guid.Empty, request.MediaFileId, request.Path));

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
