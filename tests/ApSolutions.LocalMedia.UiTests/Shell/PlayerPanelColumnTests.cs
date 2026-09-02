// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The pills that head a session and the column they open.
/// </summary>
/// <remarks>
/// The prototype heads its player with four pills — Audio, Subtítulos, Vídeo, Marcadores — and gives
/// the side column to whichever one is pressed; pressing the open one gives the width back to the
/// picture. This application had a tab strip that was always showing something, so 320 px of every
/// session belonged to a panel nobody had asked for. What is measured here is the whole of that
/// change: which pills a session draws, what each panel puts on screen, and that the column takes no
/// width until somebody opens it.
/// </remarks>
public sealed class PlayerPanelColumnTests
{
    private static readonly MediaFileId MediaFile = new(new Guid("55555555-5555-5555-5555-555555555555"));

    [AvaloniaFact]
    public async Task A_session_starts_with_its_column_closed_and_a_pill_for_every_subject_it_has()
    {
        var (window, view, viewModel) = await ShowSessionAsync();

        Assert.False(viewModel.IsPlayerPanelOpen);
        Assert.Equal(PlayerPanel.None, viewModel.PlayerPanel);
        Assert.False(Column(view).IsVisible);
        Assert.Equal(0, Column(view).Bounds.Width);

        // Every subject this session has, and nothing else: it carries tracks, an output, a video
        // status and markers, and no second version — so four pills and not five.
        Assert.Equal(
            ["Audio", "Subtítulos", "Vídeo", "Marcadores"],
            Pills(view));
        Assert.False(viewModel.HasPlayerVersions);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_pill_opens_the_column_under_its_own_name_and_the_same_pill_closes_it_again()
    {
        var (window, view, viewModel) = await ShowSessionAsync();

        viewModel.TogglePlayerPanelCommand.Execute(PlayerPanel.Markers);
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.IsPlayerPanelOpen);
        Assert.True(viewModel.IsMarkerPanelOpen);
        Assert.Equal(320, Column(view).Bounds.Width);
        var narrowed = Stage(view).Bounds.Width;

        // The column's own header, which the tab strip stood in for: the name of what is open.
        Assert.Contains(
            "Marcadores",
            Column(view).GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .Select(block => block.Text ?? string.Empty),
            StringComparer.Ordinal);

        // And the glyph on the pill, which is the second signal: in either high contrast dictionary
        // the accent fill and the resting fill are the same colour, so the fill alone says nothing.
        Assert.Equal("●", viewModel.MarkerPanelStateCue);
        Assert.Equal("○", viewModel.AudioPanelStateCue);

        viewModel.TogglePlayerPanelCommand.Execute(PlayerPanel.Markers);
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        // The width goes back to the picture, which is what closing the column is for. Measured on
        // the stage and not on the column: an invisible control is never arranged, so its own Bounds
        // keep whatever they held while it was on screen — which would make the closed column look
        // 320 px wide to anything that asked it directly.
        Assert.False(viewModel.IsPlayerPanelOpen);
        Assert.False(Column(view).IsVisible);
        Assert.Equal(narrowed + 320, Stage(view).Bounds.Width);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_audio_panel_holds_the_audio_tracks_and_the_output_and_the_subtitle_panel_the_rest()
    {
        var (window, view, viewModel) = await ShowSessionAsync();

        viewModel.TogglePlayerPanelCommand.Execute(PlayerPanel.Audio);
        Dispatcher.UIThread.RunJobs();

        // One view mounted twice rather than two views: both halves come out of the same reader, and
        // what differs is which of them a person is looking at.
        var selector = Assert.Single(Visible<TrackSelectorView>(view));
        Assert.True(selector.ShowsAudio);
        Assert.False(selector.ShowsSubtitles);
        Assert.NotEmpty(Visible<AudioOutputView>(view));
        Assert.NotNull(Find(view, "TrackSelectorAudioLabel"));
        Assert.Null(Find(view, "TrackSelectorSubtitleLabel"));

        viewModel.TogglePlayerPanelCommand.Execute(PlayerPanel.Subtitles);
        Dispatcher.UIThread.RunJobs();

        selector = Assert.Single(Visible<TrackSelectorView>(view));
        Assert.False(selector.ShowsAudio);
        Assert.True(selector.ShowsSubtitles);
        Assert.Empty(Visible<AudioOutputView>(view));
        Assert.NotNull(Find(view, "TrackSelectorSubtitleLabel"));
        Assert.Null(Find(view, "TrackSelectorAudioLabel"));
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_video_panel_says_what_the_decoder_and_the_display_agreed_on()
    {
        var (window, view, viewModel) = await ShowSessionAsync();
        viewModel.Player!.VideoStatus!.Apply(
            new PlaybackCapabilities(
                HardwareAccelerationRequested: true,
                HardwareAccelerationActive: true,
                SourceHdr: HdrFormat.Hdr10,
                DisplaySupportsHdr: true,
                OutputPath: VideoOutputPath.Hdr10Passthrough),
            fellBackToSoftware: false);

        viewModel.TogglePlayerPanelCommand.Execute(PlayerPanel.Video);
        Dispatcher.UIThread.RunJobs();

        var written = Column(view).GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text ?? string.Empty)
            .ToArray();
        Assert.Contains("Por hardware", written, StringComparer.Ordinal);
        Assert.Contains("HDR10", written, StringComparer.Ordinal);
        Assert.DoesNotContain("Por software", written, StringComparer.Ordinal);
        Assert.DoesNotContain("SDR", written, StringComparer.Ordinal);

        // The sentence under the row, which is what the prototype puts there and what the badge over
        // the picture already carries: the state is the headline and this is why.
        Assert.Contains("HDR10 directo a una pantalla compatible.", written, StringComparer.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_session_badge_counts_the_sessions_and_the_column_closes_when_one_is_replaced()
    {
        var (window, view, viewModel) = await ShowSessionAsync();
        Assert.Equal("Sesión 1 · motor único activo", viewModel.PlayerSessionBadge);

        viewModel.TogglePlayerPanelCommand.Execute(PlayerPanel.Audio);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.IsAudioPanelOpen);

        // A second file, opened over the first: the panel that was standing belonged to the session
        // that just left, and a badge that kept counting the previous one would be the only thing on
        // this header still describing it.
        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Sesión 2 · motor único activo", viewModel.PlayerSessionBadge);
        Assert.Equal(PlayerPanel.None, viewModel.PlayerPanel);
        Assert.False(Column(view).IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_foot_says_where_the_sound_is_going()
    {
        var (window, view, viewModel) = await ShowSessionAsync();
        var player = Assert.Single(view.GetVisualDescendants().OfType<PlayerView>());

        // The endpoint belongs to the output model and the foot belongs to the player, so the shell
        // is what carries one to the other. What it says is the device and what it can carry, which
        // is the pill the prototype draws at the right of its transport.
        Assert.Equal("Altavoces del sistema · 2.0", player.OutputSummary);

        // Summary and not Display, since 2026-09-02: the panel's row draws the name and the
        // capability apart, in two weights, and the foot is the one surface that still wants them
        // joined. Asserting against the model as well as against the literal is what would catch the
        // foot being wired to the half that says only the name.
        Assert.Equal(
            viewModel.Player!.AudioOutput!.SelectedDevice!.Summary,
            player.OutputSummary);

        var surface = Assert.Single(
            player.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "OutputSummarySurface");
        Assert.True(surface.IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Playing_takes_everything_but_the_picture_away_and_a_key_or_the_mouse_brings_it_back()
    {
        var (window, view, viewModel) = await ShowSessionAsync();
        var player = viewModel.Player!.Player;

        // Everything is there while the file opens: what is on screen at that moment is a header
        // saying which one.
        Assert.True(viewModel.IsChromeRevealed);
        Assert.True(TitleBar(view).IsVisible);
        Assert.True(Rail(view).IsVisible);
        var withChrome = Stage(view).Bounds;

        player.ApplySessionState(PlaybackState.Playing, failure: null);
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        // The prototype does not do this — its player keeps its header standing the whole time. It
        // is a requirement of this application's own, and what it means is measured on the picture:
        // the stage ends up being the window. The transport goes too, and it goes by opacity rather
        // than by visibility, so the controls stay in the focus and automation trees — it floats
        // over the picture and takes none of its height either way.
        Assert.False(viewModel.IsChromeRevealed);
        Assert.False(TitleBar(view).IsVisible);
        Assert.False(Rail(view).IsVisible);
        Assert.False(Header(view).IsVisible);
        Assert.False(player.AreControlsRevealed);
        Assert.Equal(0d, player.ControlsOpacity);
        Assert.Equal(withChrome.Width + 64, Stage(view).Bounds.Width);
        Assert.Equal(window.Bounds.Width, Stage(view).Bounds.Width);
        Assert.Equal(window.Bounds.Height, Stage(view).Bounds.Height);
        Assert.True(Stage(view).Bounds.Height > withChrome.Height);

        // A key anywhere brings it back — tunnelling, so a gesture the player handles counts too.
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.IsChromeRevealed);
        Assert.True(player.AreControlsRevealed);

        // And so does the mouse, from the state the key just left.
        viewModel.HideChrome();
        Assert.False(viewModel.IsChromeRevealed);
        window.MouseMove(new Point(400, 300), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.IsChromeRevealed);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Pausing_brings_the_chrome_back_and_the_open_panel_goes_with_the_picture()
    {
        var (window, view, viewModel) = await ShowSessionAsync();
        var player = viewModel.Player!.Player;
        viewModel.TogglePlayerPanelCommand.Execute(PlayerPanel.Audio);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Column(view).IsVisible);

        player.ApplySessionState(PlaybackState.Playing, failure: null);
        Dispatcher.UIThread.RunJobs();

        // The column is a panel somebody opened and it is chrome, so it answers to both: it is not
        // closed — the pill it was opened with is still pressed — it is simply not drawn.
        Assert.True(viewModel.IsAudioPanelOpen);
        Assert.False(Column(view).IsVisible);

        // Paused is somebody who has stopped watching for a moment and wants the controls they
        // paused with, so everything comes back, the panel included.
        player.ApplySessionState(PlaybackState.Paused, failure: null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(viewModel.IsChromeRevealed);
        Assert.True(Column(view).IsVisible);
        window.Close();
    }

    private static Panel Stage(ShellView view) =>
        view.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == "PlayerStage");

    private static Border TitleBar(ShellView view) =>
        view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "TitleBarSurface");

    private static Border Rail(ShellView view) =>
        view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "NavigationRailSurface");

    private static Border Header(ShellView view) =>
        view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PlayerHeaderSurface");

    private static Border Column(ShellView view) =>
        view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PlayerPanelColumn");

    private static string[] Pills(ShellView view) =>
        view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("player-pill"))
            .Where(button => button.IsEffectivelyVisible)
            .Select(button => AutomationProperties.GetName(button) ?? string.Empty)
            .ToArray();

    private static T[] Visible<T>(ShellView view)
        where T : Control =>
        view.GetVisualDescendants().OfType<T>().Where(control => control.IsEffectivelyVisible).ToArray();

    private static Control? Find(ShellView view, string key)
    {
        var expected = Avalonia.Application.Current!.TryFindResource(key, out var resolved) && resolved is string text
            ? text
            : key;
        return view.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.IsEffectivelyVisible)
            .FirstOrDefault(control => AutomationProperties.GetName(control) == expected);
    }

    private static async Task<(Window Window, ShellView View, ShellViewModel ViewModel)> ShowSessionAsync()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var viewModel = CreateViewModel();
        var view = new ShellView { DataContext = viewModel };
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        await viewModel.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        return (window, view, viewModel);
    }

    /// <summary>
    /// Each of the four panels, alone, is enough to give the column its width — and none of them is
    /// enough to take it when it is not there.
    /// </summary>
    /// <remarks>
    /// The column is 320 px of the window, so which panel decides it is not a detail: a session with
    /// one audio track, no markers and no other version used to leave an empty rectangle taking a
    /// fifth of the picture. Each arm is asked for on its own here because a chain of four alternatives
    /// tested only at its ends is a chain where the middle two are never the one that answered.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("audio")]
    [InlineData("video")]
    [InlineData("markers")]
    [InlineData("versions")]
    [InlineData("none")]
    public void One_panel_is_enough_for_the_column_and_none_is_not(string only)
    {
        var shell = new ShellViewModel(
            new NavigationService(),
            new ShellSurfaces
            {
                OpenPlayer = (_, _) => Task.FromResult<PlayerSurfaces?>(OnlyPanel(only)),
                ClosePlayer = _ => Task.CompletedTask,
            });

        shell.OpenPlayerAsync(new PlayDetailsRequest(new MediaFileId(Guid.NewGuid()), null))
            .GetAwaiter()
            .GetResult();

        Assert.Equal(only != "none", shell.HasPlayerPanels);
    }

    private static PlayerSurfaces OnlyPanel(string only)
    {
        var player = new PlayerViewModel(new InertCoordinator());
        if (only == "versions")
        {
            var version = new MediaVersion(
                new MediaFileId(Guid.NewGuid()),
                @"rootlternate.mkv",
                true,
                TimeSpan.FromMinutes(90),
                1280,
                720,
                IsHdr: false,
                "H264",
                1_000_000);
            var versions = new PlayerVersionsViewModel(
                [new PlayerVersionRowViewModel(version, new VersionSwitchViewModel(), _ => Task.CompletedTask)]);
            return new PlayerSurfaces { Player = player, Versions = versions };
        }

        if (only == "audio")
        {
            var output = new AudioOutputViewModel(new StubCatalog());
            output.LoadAsync().GetAwaiter().GetResult();
            return new PlayerSurfaces { Player = player, AudioOutput = output };
        }

        return only switch
        {
            "video" => new PlayerSurfaces { Player = player, VideoStatus = new VideoStatusViewModel() },
            "markers" => new PlayerSurfaces { Player = player, Markers = new MarkerEditorViewModel() },
            _ => new PlayerSurfaces { Player = player },
        };
    }

    private static ShellViewModel CreateViewModel() => new(
        new NavigationService(),
        new ShellSurfaces
        {
            OpenPlayer = (_, _) => Task.FromResult<PlayerSurfaces?>(BuildSurfaces()),
            ClosePlayer = _ => Task.CompletedTask,
            ChangePlaybackMode = (mode, _) => Task.FromResult(mode),
        });

    private static PlayerSurfaces BuildSurfaces()
    {
        var tracks = new TrackSelectorViewModel(
            new SelectTrack(new StubEngine(), new StubPreferences()),
            PlaybackPreference.FileKey(Guid.Empty),
            PlaybackPreference.SeriesKey(Guid.Empty));
        tracks.Load(
            [
                new MediaTrack("1", MediaTrackKind.Audio, "spa", "Español", 6, "eac3"),
                new MediaTrack("2", MediaTrackKind.Subtitle, "spa", "Español", null, "subrip"),
            ],
            null,
            activeSubtitle: null);

        var output = new AudioOutputViewModel(new StubCatalog());
        output.LoadAsync().GetAwaiter().GetResult();

        return new PlayerSurfaces
        {
            Player = new PlayerViewModel(new InertCoordinator()),
            Tracks = tracks,
            AudioOutput = output,
            VideoStatus = new VideoStatusViewModel(),
            Markers = new MarkerEditorViewModel(),
        };
    }

    private sealed class StubCatalog : IAudioDeviceCatalog
    {
        public Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AudioOutputDevice>>(
            [
                new AudioOutputDevice(
                    "system",
                    "Altavoces del sistema",
                    [AudioChannelLayout.Stereo],
                    IsDefault: true,
                    IsAvailable: true),
            ]);
    }

    private sealed class StubPreferences : IPlaybackPreferenceRepository
    {
        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) => Task.FromResult<PlaybackPreference?>(null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
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

    private sealed class StubEngine : IMediaPlayerEngine
    {
        public PlaybackState State => PlaybackState.Idle;

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PlaybackFailureEventArgs>? Failure
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(PlaybackState.Idle, TimeSpan.Zero, null, []));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle));

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
