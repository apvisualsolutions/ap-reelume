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
        var expected = Assert.IsType<CornerRadius>(
            Avalonia.Application.Current!.TryFindResource("CornerRadiusMedium", out var token)
                ? token
                : null);
        Assert.True(expected.TopLeft > 0, "CornerRadiusMedium resolved to nothing, so this proves nothing.");

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
                Player = new PlayerViewModel(new InertCoordinator()),
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
