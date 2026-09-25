// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// Escape steps back one layer at a time, the way the prototype's own handler does (ENG-018).
/// </summary>
/// <remarks>
/// The prototype's order is written in its source (<c>design/AP Reelume.dc.html</c>, the key handler):
/// an open panel first, then fullscreen or the mini player back to embedded, then the player itself.
/// Until 2026-09-25 this application did only the middle step, so Escape did nothing at all with a
/// panel open in the window or with the film simply playing embedded.
/// </remarks>
public sealed class ShellEscapeTests
{
    private static readonly MediaFileId MediaFile = new(new Guid("77777777-7777-7777-7777-777777777777"));

    [Fact]
    public async Task Escape_closes_the_gear_first_then_the_panel_then_fullscreen_then_the_player()
    {
        var (shell, player, closed) = await OpenSessionAsync();
        var token = TestContext.Current.CancellationToken;
        shell.TogglePlayerPanelCommand.Execute(PlayerPanel.Audio);
        player.Settings!.ToggleCommand.Execute(null);
        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, token);

        await shell.EscapeAsync(token);
        Assert.False(player.Settings.IsOpen);
        Assert.True(shell.IsPlayerPanelOpen);

        await shell.EscapeAsync(token);
        Assert.False(shell.IsPlayerPanelOpen);
        Assert.Equal(PlaybackMode.Fullscreen, shell.PlaybackMode);

        await shell.EscapeAsync(token);
        Assert.Equal(PlaybackMode.Embedded, shell.PlaybackMode);
        Assert.NotNull(shell.Player);

        await shell.EscapeAsync(token);
        Assert.Null(shell.Player);
        Assert.Equal(1, closed.Count);
    }

    [Fact]
    public async Task Escape_brings_the_mini_player_back_rather_than_closing_it()
    {
        var (shell, _, closed) = await OpenSessionAsync();
        var token = TestContext.Current.CancellationToken;
        await shell.TogglePlaybackModeAsync(PlaybackMode.Mini, token);

        await shell.EscapeAsync(token);

        Assert.Equal(PlaybackMode.Embedded, shell.PlaybackMode);
        Assert.NotNull(shell.Player);
        Assert.Equal(0, closed.Count);
    }

    [Fact]
    public async Task Escape_with_no_player_does_nothing()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());

        await shell.EscapeAsync(TestContext.Current.CancellationToken);

        Assert.Null(shell.Player);
    }

    private static async Task<(ShellViewModel Shell, PlayerViewModel Player, Counter Closed)> OpenSessionAsync()
    {
        var player = new PlayerViewModel(new InertCoordinator(), settings: new PlayerSettingsMenuViewModel());
        var closed = new Counter();
        var shell = new ShellViewModel(
            new NavigationService(),
            new ShellSurfaces
            {
                OpenPlayer = (_, _) => Task.FromResult<PlayerSurfaces?>(new PlayerSurfaces { Player = player }),
                ClosePlayer = _ =>
                {
                    closed.Count++;
                    return Task.CompletedTask;
                },
                ChangePlaybackMode = (mode, _) => Task.FromResult(mode),
            });
        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        return (shell, player, closed);
    }

    private sealed class Counter
    {
        public int Count { get; set; }
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
