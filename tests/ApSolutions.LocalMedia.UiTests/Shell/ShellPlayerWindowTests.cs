// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The shell owns the window the mini player lives in.
/// <para>
/// A mode change has to move the surface that is already playing rather than build a second one:
/// building a new player for the mini window is how two sessions end up running at the same time,
/// and it is the reason the coordinator hands the same control back and forth.
/// </para>
/// </summary>
public sealed class ShellPlayerWindowTests
{
    private static readonly MediaFileId MediaFile = new(new Guid("44444444-4444-4444-4444-444444444444"));

    [AvaloniaFact]
    public async Task The_mini_mode_moves_the_playing_surface_out_of_the_shell_and_back()
    {
        var (window, view, viewModel) = Show();
        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        var stage = Stage(view);
        var host = Host(view);
        Assert.Same(host, stage.Parent);

        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        // The stage is inside the mini window rather than being all of it: the window keeps a tree of
        // its own, which is where its five controls live. What is asserted is the window it ended up
        // under, because the parent it hangs from is now the panel that holds it.
        var mini = Assert.IsType<MiniPlayerWindow>(
            stage.GetVisualAncestors().OfType<Window>().FirstOrDefault());
        Assert.True(mini.Topmost);
        Assert.Contains(stage, mini.GetVisualDescendants());
        Assert.NotEmpty(mini.GetVisualDescendants().OfType<MiniPlayerChromeView>());

        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(host, stage.Parent);
        Assert.Equal(PlaybackMode.Embedded, viewModel.PlaybackMode);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Closing_the_session_takes_the_mini_window_with_it()
    {
        var (window, view, viewModel) = Show();
        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        var stage = Stage(view);
        await viewModel.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<MiniPlayerWindow>(stage.GetVisualAncestors().OfType<Window>().FirstOrDefault());

        await viewModel.ClosePlayerAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(Host(view), stage.Parent);
        Assert.False(viewModel.IsPlayerVisible);
        window.Close();
    }

    /// <summary>
    /// A shell whose data context is replaced stops listening to the old one. Without that, two view
    /// models would both drive the same window and the second mode change would fight the first.
    /// </summary>
    [AvaloniaFact]
    public async Task A_replaced_view_model_no_longer_drives_the_window()
    {
        var (window, view, first) = Show();
        var second = CreateViewModel();
        view.DataContext = second;
        Dispatcher.UIThread.RunJobs();

        await first.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        await first.TogglePlaybackModeAsync(PlaybackMode.Mini, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(Host(view), Stage(view).Parent);
        window.Close();
    }

    [AvaloniaFact]
    public void A_shell_without_a_view_model_at_all_still_shows()
    {
        var view = new ShellView();
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(Stage(view));
        window.Close();
    }

    // The stage is looked up by name rather than walked to: once the mini window owns it, it is no
    // longer a visual descendant of the shell, which is the whole thing under test.
    private static Panel Stage(ShellView view) =>
        view.FindControl<Panel>("PlayerStage") ?? throw new InvalidOperationException("No player stage.");

    private static ContentControl Host(ShellView view) =>
        view.FindControl<ContentControl>("PlayerHost") ?? throw new InvalidOperationException("No player host.");

    private static (Window Window, ShellView View, ShellViewModel ViewModel) Show()
    {
        var viewModel = CreateViewModel();
        var view = new ShellView { DataContext = viewModel };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
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
