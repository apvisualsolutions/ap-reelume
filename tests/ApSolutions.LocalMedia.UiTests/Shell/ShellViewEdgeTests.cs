// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

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
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// Three states the shell's view can be in that no scene reaches on its way somewhere else.
/// </summary>
/// <remarks>
/// Written on 2026-09-25 because the coverage gate asked for them: the view gained the pointer's
/// handling with ENG-018, its branches moved, and the file had to reach the bar rather than carry a
/// floor copied from a run that had not happened yet. The guards nothing could take were removed; these
/// are the ones something can.
/// </remarks>
public sealed class ShellViewEdgeTests
{
    private static readonly MediaFileId MediaFile = new(new Guid("88888888-8888-8888-8888-888888888888"));

    [AvaloniaFact]
    public void A_view_whose_context_is_taken_away_stops_listening_to_it()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());
        var view = new ShellView { DataContext = shell };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.DataContext = null;
        shell.HideChrome();
        Dispatcher.UIThread.RunJobs();

        // The stage keeps the pointer it had: the model that hid its chrome is not this view's any more.
        Assert.Null(view.FindControl<Panel>("PlayerStage")!.Cursor);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_view_that_is_in_no_window_takes_a_change_of_mode_without_a_window_to_resize()
    {
        var (shell, _) = await OpenAsync((mode, _) => Task.FromResult(mode));
        var view = new ShellView { DataContext = shell };

        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PlaybackMode.Fullscreen, shell.PlaybackMode);
        Assert.True(shell.Player!.Player.IsFullscreen);
        _ = view;
    }

    [AvaloniaFact]
    public async Task A_player_closed_while_its_mode_changes_leaves_the_view_nothing_to_tell()
    {
        ShellViewModel? shell = null;
        (shell, _) = await OpenAsync(async (mode, _) =>
        {
            await shell!.ClosePlayerAsync(TestContext.Current.CancellationToken);
            return mode;
        });
        var view = new ShellView { DataContext = shell };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(shell.Player);
        window.Close();
    }

    private static async Task<(ShellViewModel Shell, PlayerViewModel Player)> OpenAsync(
        Func<PlaybackMode, CancellationToken, Task<PlaybackMode>> change)
    {
        var player = new PlayerViewModel(new InertCoordinator());
        var shell = new ShellViewModel(
            new NavigationService(),
            new ShellSurfaces
            {
                OpenPlayer = (_, _) => Task.FromResult<PlayerSurfaces?>(new PlayerSurfaces { Player = player }),
                ClosePlayer = _ => Task.CompletedTask,
                ChangePlaybackMode = change,
            });
        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        return (shell, player);
    }

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
