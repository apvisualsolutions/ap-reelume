// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The resume offer is only real when the assembly feeds it: the decision has to exist before the
/// media opens, the media has to open at the chosen point, and the prompt's buttons have to reach
/// the playback they claim to control. A prompt with no handler looks identical to a working one
/// and leaves the video at zero — the defect the deep audit filed as BUG-002.
/// </summary>
public sealed class ResumeWiringTests
{
    [Fact]
    public void The_resume_prompt_is_built_with_a_handler_that_reaches_playback()
    {
        var source = CompositionRootSource();

        // "new ResumePromptViewModel(resume)" builds the surface and connects it to nothing: the
        // second constructor argument is what makes either button do something.
        Assert.Contains("new ResumePromptViewModel(resume,", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_resume_decision_exists_before_the_media_opens()
    {
        var source = CompositionRootSource();

        var decision = source.IndexOf(".DecideAsync(", StringComparison.Ordinal);
        var open = source.IndexOf("player.OpenAsync(", StringComparison.Ordinal);

        Assert.True(decision >= 0, "The composition root never asks ResumePlayback for a decision.");
        Assert.True(open >= 0, "The composition root never opens the player.");
        Assert.True(
            decision < open,
            "The resume decision is computed after the media has already opened, so the stored "
            + "position can never reach the engine.");
    }

    [Fact]
    public void The_media_opens_at_the_position_the_decision_chose()
    {
        var source = CompositionRootSource();

        // The engine accepts a start position and nothing was passing one: opening with the plain
        // two-argument call always starts at zero, whatever the decision said.
        Assert.Matches(
            new Regex(@"player\.OpenAsync\(mediaFileId,\s*file\.Path,\s*start", RegexOptions.None, TimeSpan.FromSeconds(2)),
            source);
    }

    [Fact]
    public async Task The_view_model_hands_the_start_position_to_the_session_it_starts()
    {
        var coordinator = new RequestRecordingCoordinator();
        var viewModel = new PlayerViewModel(coordinator, new StillFrameSource(), new RefusingLauncher());
        var stored = TimeSpan.FromMinutes(40);

        await viewModel.OpenAsync(
            new MediaFileId(Guid.NewGuid()),
            @"D:\Media\Example\episode.mkv",
            stored,
            TestContext.Current.CancellationToken);

        var request = Assert.Single(coordinator.Requests);
        Assert.Equal(stored, request.StartPosition);
    }

    private static string CompositionRootSource()
    {
        return CompositionSourceText.Read();
    }

    private sealed class RequestRecordingCoordinator : IPlaybackSessionCoordinator
    {
        private readonly List<PlaybackRequest> _requests = [];

        public PlaybackSession? ActiveSession { get; private set; }

        public IReadOnlyList<PlaybackRequest> Requests => _requests;

        public Task<PlaybackSession> StartAsync(PlaybackRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            _requests.Add(request);
            ActiveSession = new PlaybackSession(Guid.NewGuid(), request.MediaFileId, request.Path);
            return Task.FromResult(ActiveSession);
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StillFrameSource : IVideoFrameSource
    {
        public event EventHandler<VideoFrameEventArgs>? FrameRendered
        {
            add { }
            remove { }
        }
    }

    private sealed class RefusingLauncher : IExternalPlaybackLauncher
    {
        public Task<bool> TryLaunchAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
