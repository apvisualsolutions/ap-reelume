using System.ComponentModel;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The view model must expose the session as plain state and commands. It never holds an engine
/// object, so every transition is asserted through the single-session coordinator.
/// </summary>
public sealed class PlayerViewModelTests
{
    private const string SamplePath = @"D:\Media\sample.mp4";

    [Fact]
    public async Task Opening_a_file_walks_from_opening_to_playing_and_publishes_every_flag()
    {
        var coordinator = new RecordingCoordinator();
        var viewModel = new PlayerViewModel(coordinator);
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        await viewModel.OpenAsync(new MediaFileId(Guid.NewGuid()), SamplePath, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SamplePath, viewModel.MediaPath);
        Assert.True(viewModel.IsPlaying);
        Assert.False(viewModel.IsIdle);
        Assert.False(viewModel.IsOpening);
        Assert.False(viewModel.HasFailed);
        Assert.True(viewModel.CanPause);
        Assert.False(viewModel.CanResume);
        Assert.True(viewModel.CanStop);
        Assert.Contains(nameof(PlayerViewModel.MediaPath), changed, StringComparer.Ordinal);
        Assert.Contains(nameof(PlayerViewModel.IsPlaying), changed, StringComparer.Ordinal);
        Assert.Equal(SamplePath, Assert.Single(coordinator.StartedPaths));
    }

    [Theory]
    [InlineData(PlaybackFailureCode.FileNotFound)]
    [InlineData(PlaybackFailureCode.OpenFailed)]
    [InlineData(PlaybackFailureCode.EngineUnavailable)]
    public async Task A_failed_open_surfaces_exactly_one_actionable_reason(PlaybackFailureCode code)
    {
        var coordinator = new RecordingCoordinator
        {
            FailureOnStart = new PlaybackFailure(code, "engine detail"),
        };
        var viewModel = new PlayerViewModel(coordinator);

        await viewModel.OpenAsync(new MediaFileId(Guid.NewGuid()), SamplePath, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasFailed);
        Assert.False(viewModel.CanStop);
        Assert.Equal(code == PlaybackFailureCode.FileNotFound, viewModel.FileWasNotFound);
        Assert.Equal(code == PlaybackFailureCode.OpenFailed, viewModel.OpenFailed);
        Assert.Equal(code == PlaybackFailureCode.EngineUnavailable, viewModel.EngineWasUnavailable);
    }

    [Fact]
    public async Task Transport_commands_only_run_for_the_state_that_allows_them()
    {
        var coordinator = new RecordingCoordinator();
        var viewModel = new PlayerViewModel(coordinator);

        Assert.False(viewModel.PauseCommand.CanExecute(null));
        Assert.False(viewModel.ResumeCommand.CanExecute(null));
        Assert.False(viewModel.StopCommand.CanExecute(null));

        await viewModel.OpenAsync(new MediaFileId(Guid.NewGuid()), SamplePath, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(viewModel.PauseCommand.CanExecute(null));
        viewModel.PauseCommand.Execute(null);
        await RecordingCoordinator.WaitForAsync(() => coordinator.PauseCount == 1);

        viewModel.ApplySessionState(PlaybackState.Paused, failure: null);
        Assert.True(viewModel.IsPaused);
        Assert.True(viewModel.ResumeCommand.CanExecute(null));
        viewModel.ResumeCommand.Execute(null);
        await RecordingCoordinator.WaitForAsync(() => coordinator.ResumeCount == 1);

        viewModel.ApplySessionState(PlaybackState.Playing, failure: null);
        Assert.True(viewModel.StopCommand.CanExecute(null));
        viewModel.StopCommand.Execute(null);
        await RecordingCoordinator.WaitForAsync(() => coordinator.StopCount == 1);

        viewModel.ApplySessionState(PlaybackState.Stopped, failure: null);
        Assert.True(viewModel.IsStopped);
        Assert.False(viewModel.StopCommand.CanExecute(null));
    }

    [Fact]
    public void A_reported_session_failure_replaces_the_previous_reason()
    {
        var viewModel = new PlayerViewModel(new RecordingCoordinator());

        viewModel.ApplySessionState(
            PlaybackState.Failed,
            new PlaybackFailure(PlaybackFailureCode.OpenFailed, "codec"));
        Assert.True(viewModel.OpenFailed);

        viewModel.ApplySessionState(
            PlaybackState.Failed,
            new PlaybackFailure(PlaybackFailureCode.FileNotFound, "gone"));
        Assert.True(viewModel.FileWasNotFound);
        Assert.False(viewModel.OpenFailed);

        viewModel.ApplySessionState(PlaybackState.Idle, failure: null);
        Assert.True(viewModel.IsIdle);
        Assert.False(viewModel.FileWasNotFound);
    }

    [Fact]
    public async Task An_unsupported_codec_offers_another_version_and_external_playback_but_no_retry()
    {
        var launcher = new RecordingLauncher();
        var coordinator = new RecordingCoordinator
        {
            FailureOnStart = new PlaybackFailure(PlaybackFailureCode.UnsupportedCodec, "no decoder"),
        };
        var viewModel = new PlayerViewModel(coordinator, frameSource: null, externalLauncher: launcher);

        await viewModel.OpenAsync(new MediaFileId(Guid.NewGuid()), SamplePath, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(viewModel.CodecIsUnsupported);
        Assert.True(viewModel.CanChooseAnotherVersion);
        Assert.True(viewModel.CanOpenExternally);
        Assert.False(viewModel.CanRetry);

        viewModel.OpenExternallyCommand.Execute(null);
        await RecordingCoordinator.WaitForAsync(() => launcher.Requests.Count == 1);
        Assert.Equal(SamplePath, launcher.Requests[0]);
        Assert.False(viewModel.ExternalLaunchFailed);
    }

    [Fact]
    public async Task Corrupted_media_reports_its_own_reason_and_a_refused_external_launch_is_visible()
    {
        var launcher = new RecordingLauncher { Accepts = false };
        var coordinator = new RecordingCoordinator
        {
            FailureOnStart = new PlaybackFailure(PlaybackFailureCode.CorruptedMedia, "truncated"),
        };
        var viewModel = new PlayerViewModel(coordinator, frameSource: null, externalLauncher: launcher);

        await viewModel.OpenAsync(new MediaFileId(Guid.NewGuid()), SamplePath, cancellationToken: TestContext.Current.CancellationToken);
        viewModel.OpenExternallyCommand.Execute(null);
        await RecordingCoordinator.WaitForAsync(() => launcher.Requests.Count == 1);

        Assert.True(viewModel.MediaWasCorrupted);
        Assert.False(viewModel.CodecIsUnsupported);
        Assert.True(viewModel.ExternalLaunchFailed);
    }

    [Fact]
    public async Task A_missing_file_can_be_retried_and_succeeds_once_the_media_returns()
    {
        var coordinator = new RecordingCoordinator
        {
            FailureOnStart = new PlaybackFailure(PlaybackFailureCode.FileNotFound, "gone"),
        };
        var viewModel = new PlayerViewModel(coordinator);

        await viewModel.OpenAsync(new MediaFileId(Guid.NewGuid()), SamplePath, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(viewModel.CanRetry);
        Assert.False(viewModel.CanOpenExternally);

        coordinator.Recover();
        viewModel.RetryCommand.Execute(null);
        await RecordingCoordinator.WaitForAsync(() => viewModel.IsPlaying);
        Assert.Equal(SamplePath, Assert.Single(coordinator.StartedPaths));
    }

    [Fact]
    public async Task A_file_without_audio_plays_and_announces_the_absence()
    {
        var viewModel = new PlayerViewModel(new RecordingCoordinator());
        await viewModel.OpenAsync(new MediaFileId(Guid.NewGuid()), SamplePath, cancellationToken: TestContext.Current.CancellationToken);

        viewModel.ApplyTracks([new MediaTrack("v", MediaTrackKind.Video, Codec: "H264")]);
        Assert.True(viewModel.HasNoAudioTrack);

        viewModel.ApplyTracks(
        [
            new MediaTrack("v", MediaTrackKind.Video, Codec: "H264"),
            new MediaTrack("a", MediaTrackKind.Audio, Codec: "AAC"),
        ]);
        Assert.False(viewModel.HasNoAudioTrack);
        Assert.Throws<ArgumentNullException>(() => viewModel.ApplyTracks(null!));
    }

    [Fact]
    public void External_playback_is_never_offered_without_a_launcher()
    {
        var viewModel = new PlayerViewModel(new RecordingCoordinator());

        viewModel.ApplySessionState(
            PlaybackState.Failed,
            new PlaybackFailure(PlaybackFailureCode.UnsupportedCodec, "no decoder"));

        Assert.True(viewModel.CanChooseAnotherVersion);
        Assert.False(viewModel.CanOpenExternally);
        Assert.False(viewModel.OpenExternallyCommand.CanExecute(null));
    }

    [Fact]
    public void The_view_model_refuses_to_exist_without_a_session_coordinator() =>
        Assert.Throws<ArgumentNullException>(() => new PlayerViewModel(null!));

    [Fact]
    public async Task The_resume_prompt_offers_the_stored_point_and_reports_the_choice()
    {
        var chosen = new List<(ResumeChoice Choice, TimeSpan Position)>();
        var prompt = new ResumePromptViewModel(
            new ResumeDecision(ResumeChoice.Resume, TimeSpan.FromSeconds(4_215)),
            (choice, position) =>
            {
                chosen.Add((choice, position));
                return Task.CompletedTask;
            });
        var changed = new List<string>();
        prompt.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        Assert.True(prompt.IsVisible);
        Assert.True(prompt.ResumeCommand.CanExecute(null));
        Assert.Equal("01:10:15", prompt.ResumePositionText);
        Assert.Null(prompt.Chosen);

        prompt.ResumeCommand.Execute(null);
        prompt.ResumeCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(ResumeChoice.Resume, prompt.Chosen);
        Assert.False(prompt.IsVisible);
        Assert.Equal((ResumeChoice.Resume, TimeSpan.FromSeconds(4_215)), Assert.Single(chosen));
        Assert.Contains(nameof(ResumePromptViewModel.IsVisible), changed);
    }

    [Fact]
    public async Task Restarting_from_the_prompt_reports_zero_and_trivial_progress_never_asks()
    {
        var chosen = new List<(ResumeChoice Choice, TimeSpan Position)>();
        var prompt = new ResumePromptViewModel(
            new ResumeDecision(ResumeChoice.Resume, TimeSpan.FromMinutes(9)),
            (choice, position) =>
            {
                chosen.Add((choice, position));
                return Task.CompletedTask;
            });

        prompt.RestartCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(ResumeChoice.Restart, prompt.Chosen);
        Assert.Equal((ResumeChoice.Restart, TimeSpan.Zero), Assert.Single(chosen));

        var silent = new ResumePromptViewModel(new ResumeDecision(ResumeChoice.Restart, TimeSpan.Zero));
        Assert.False(silent.IsVisible);
        silent.ResumeCommand.Execute(null);
        Assert.Equal(ResumeChoice.Resume, silent.Chosen);
        Assert.Throws<ArgumentNullException>(() => new ResumePromptViewModel(null!));
    }

    [Fact]
    public void The_view_model_exposes_frames_and_never_an_engine_object()
    {
        var frames = new EmptyFrameSource();

        var viewModel = new PlayerViewModel(new RecordingCoordinator(), frames);

        Assert.Same(frames, viewModel.FrameSource);
        Assert.All(
            typeof(PlayerViewModel).GetProperties(),
            property => Assert.DoesNotContain(
                "MediaPlayer",
                property.PropertyType.Name,
                StringComparison.OrdinalIgnoreCase));
    }

    private sealed class EmptyFrameSource : IVideoFrameSource
    {
        public event EventHandler<VideoFrameEventArgs>? FrameRendered;

        public void Ignore() => FrameRendered?.Invoke(this, new VideoFrameEventArgs(default, 0, 0, 0));
    }

    private sealed class RecordingLauncher : IExternalPlaybackLauncher
    {
        public bool Accepts { get; init; } = true;

        public List<string> Requests { get; } = [];

        public Task<bool> TryLaunchAsync(string path, CancellationToken cancellationToken = default)
        {
            Requests.Add(path);
            return Task.FromResult(Accepts);
        }
    }

    private sealed class RecordingCoordinator : IPlaybackSessionCoordinator
    {
        private readonly List<string> _startedPaths = [];

        public PlaybackSession? ActiveSession { get; private set; }

        public PlaybackFailure? FailureOnStart { get; set; }

        public void Recover() => FailureOnStart = null;

        public IReadOnlyList<string> StartedPaths => _startedPaths;

        public int PauseCount { get; private set; }

        public int ResumeCount { get; private set; }

        public int StopCount { get; private set; }

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (FailureOnStart is { } failure)
            {
                throw new PlaybackFailureException(failure);
            }

            _startedPaths.Add(request.Path);
            ActiveSession = new PlaybackSession(Guid.NewGuid(), request.MediaFileId, request.Path);
            return Task.FromResult(ActiveSession);
        }

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            PauseCount++;
            return Task.CompletedTask;
        }

        public Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            ResumeCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            ActiveSession = null;
            return Task.CompletedTask;
        }

        public static async Task WaitForAsync(Func<bool> condition)
        {
            for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            Assert.True(condition(), "The coordinator never observed the expected command.");
        }
    }
}
