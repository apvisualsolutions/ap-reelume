// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

public sealed class PlaybackContractTests
{
    private static readonly string[] ForbiddenAssemblyTokens =
        ["LibVLCSharp", "Avalonia", "System.Windows", "WindowsBase", "Microsoft.Win32"];

    /// <summary>The complete set of transitions the approved lifecycle allows; everything else is denied.</summary>
    private static readonly HashSet<(PlaybackState From, PlaybackState To)> ApprovedTransitions =
    [
        (PlaybackState.Idle, PlaybackState.Opening),
        (PlaybackState.Stopped, PlaybackState.Opening),
        (PlaybackState.Failed, PlaybackState.Opening),
        (PlaybackState.Opening, PlaybackState.Playing),
        (PlaybackState.Opening, PlaybackState.Failed),
        (PlaybackState.Opening, PlaybackState.Stopped),
        (PlaybackState.Playing, PlaybackState.Paused),
        (PlaybackState.Paused, PlaybackState.Playing),
        (PlaybackState.Playing, PlaybackState.Stopped),
        (PlaybackState.Paused, PlaybackState.Stopped),
        (PlaybackState.Playing, PlaybackState.Failed),
        (PlaybackState.Paused, PlaybackState.Failed),
    ];

    [Fact]
    public void Session_lifecycle_allows_only_the_approved_transitions()
    {
        foreach (var from in Enum.GetValues<PlaybackState>())
        {
            foreach (var to in Enum.GetValues<PlaybackState>())
            {
                var expected = ApprovedTransitions.Contains((from, to));

                Assert.Equal(expected, PlaybackStatePolicy.CanTransition(from, to));
            }
        }

        Assert.False(PlaybackStatePolicy.CanTransition(PlaybackState.Idle, PlaybackState.Playing));
        Assert.False(PlaybackStatePolicy.CanTransition(PlaybackState.Stopped, PlaybackState.Playing));
        Assert.False(PlaybackStatePolicy.CanTransition(PlaybackState.Playing, PlaybackState.Playing));
    }

    [Fact]
    public void Only_opening_playing_and_paused_hold_an_active_session()
    {
        Assert.True(PlaybackStatePolicy.IsActive(PlaybackState.Opening));
        Assert.True(PlaybackStatePolicy.IsActive(PlaybackState.Playing));
        Assert.True(PlaybackStatePolicy.IsActive(PlaybackState.Paused));

        Assert.False(PlaybackStatePolicy.IsActive(PlaybackState.Idle));
        Assert.False(PlaybackStatePolicy.IsActive(PlaybackState.Stopped));
        Assert.False(PlaybackStatePolicy.IsActive(PlaybackState.Failed));
    }

    [Fact]
    public void Requests_reject_missing_paths_and_negative_start_positions()
    {
        var mediaFileId = new MediaFileId(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => new PlaybackRequest(mediaFileId, "   "));
        Assert.Throws<ArgumentNullException>(() => new PlaybackRequest(mediaFileId, null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PlaybackRequest(mediaFileId, @"C:\Media\sample.mp4", TimeSpan.FromSeconds(-1)));

        var request = new PlaybackRequest(mediaFileId, @"C:\Media\sample.mp4", TimeSpan.FromSeconds(30));

        Assert.Equal(mediaFileId, request.MediaFileId);
        Assert.Equal(TimeSpan.FromSeconds(30), request.StartPosition);
        Assert.True(request.UseHardwareAcceleration);
    }

    [Fact]
    public void Snapshots_clamp_the_position_into_the_observed_duration()
    {
        var duration = TimeSpan.FromMinutes(10);
        var track = new MediaTrack("video-1", MediaTrackKind.Video, Codec: "h264");

        var overrun = PlaybackSnapshot.Create(PlaybackState.Playing, TimeSpan.FromMinutes(12), duration, [track]);
        var underrun = PlaybackSnapshot.Create(PlaybackState.Playing, TimeSpan.FromSeconds(-5), duration, [track]);
        var unknownDuration = PlaybackSnapshot.Create(PlaybackState.Opening, TimeSpan.FromMinutes(12), null, []);

        Assert.Equal(duration, overrun.Position);
        Assert.Equal(TimeSpan.Zero, underrun.Position);
        Assert.Equal(TimeSpan.FromMinutes(12), unknownDuration.Position);
        Assert.Empty(unknownDuration.Tracks);
        Assert.Null(overrun.Failure);
    }

    [Fact]
    public void Every_failure_carries_an_actionable_domain_code_however_it_is_raised()
    {
        var inner = new InvalidOperationException("native");
        var missing = new PlaybackFailure(PlaybackFailureCode.FileNotFound, @"D:\Media\gone.mkv");

        var fallback = new PlaybackFailureException();
        var described = new PlaybackFailureException("The engine refused the media.");
        var wrapped = new PlaybackFailureException("The engine refused the media.", inner);
        var explicitFailure = new PlaybackFailureException(missing);
        var explicitWrapped = new PlaybackFailureException(missing, inner);

        Assert.Equal(PlaybackFailureCode.OpenFailed, fallback.Failure.Code);
        Assert.Equal(PlaybackFailureCode.OpenFailed, described.Failure.Code);
        Assert.Equal("The engine refused the media.", described.Failure.Detail);
        Assert.Same(inner, wrapped.InnerException);
        Assert.Equal(PlaybackFailureCode.OpenFailed, wrapped.Failure.Code);
        Assert.Equal(missing, explicitFailure.Failure);
        Assert.Contains("FileNotFound", explicitFailure.Message, StringComparison.Ordinal);
        Assert.Same(inner, explicitWrapped.InnerException);
        Assert.Equal(missing, explicitWrapped.Failure);
    }

    [Fact]
    public void Typed_events_and_capabilities_describe_what_the_engine_actually_did()
    {
        var state = new PlaybackStateChangedEventArgs(PlaybackState.Opening, PlaybackState.Playing);
        var position = new PlaybackPositionChangedEventArgs(TimeSpan.FromSeconds(12), TimeSpan.FromMinutes(3));
        var failure = new PlaybackFailureEventArgs(
            new PlaybackFailure(PlaybackFailureCode.EngineUnavailable, "no engine"));
        var capabilities = new PlaybackCapabilities(
            HardwareAccelerationRequested: true,
            HardwareAccelerationActive: false);
        var track = new MediaTrack(
            "audio-2",
            MediaTrackKind.Audio,
            Language: "spa",
            Description: "Español 5.1",
            Channels: 6,
            Codec: "eac3",
            IsExternal: false);

        Assert.Equal(PlaybackState.Opening, state.PreviousState);
        Assert.Equal(PlaybackState.Playing, state.CurrentState);
        Assert.Equal(TimeSpan.FromSeconds(12), position.Position);
        Assert.Equal(TimeSpan.FromMinutes(3), position.Duration);
        Assert.Equal(PlaybackFailureCode.EngineUnavailable, failure.Failure.Code);
        Assert.True(capabilities.HardwareAccelerationRequested);
        Assert.False(capabilities.HardwareAccelerationActive);
        Assert.Equal("spa", track.Language);
        Assert.Equal(6, track.Channels);
        Assert.False(track.IsExternal);
    }

    [Fact]
    public void A_failed_snapshot_keeps_the_reason_the_shell_must_present()
    {
        var reason = new PlaybackFailure(PlaybackFailureCode.OpenFailed, "unsupported codec");

        var snapshot = PlaybackSnapshot.Create(PlaybackState.Failed, TimeSpan.Zero, null, [], reason);

        Assert.Equal(PlaybackState.Failed, snapshot.State);
        Assert.Equal(reason, snapshot.Failure);
        Assert.Null(snapshot.Duration);
        Assert.Throws<ArgumentNullException>(
            () => PlaybackSnapshot.Create(PlaybackState.Idle, TimeSpan.Zero, null, null!));
    }

    [Fact]
    public void Diagnosis_maps_every_observation_to_one_actionable_domain_code()
    {
        var video = new MediaTrack("v", MediaTrackKind.Video, Codec: "H264 - MPEG-4 AVC (part 10)");
        var audio = new MediaTrack("a", MediaTrackKind.Audio, Codec: "MPEG AAC Audio", Channels: 2);
        var undecodable = new MediaTrack("v", MediaTrackKind.Video, Codec: null);
        var subtitle = new MediaTrack("s", MediaTrackKind.Subtitle, Codec: "Text subtitles");

        Assert.Null(PlaybackDiagnosticsPolicy.Diagnose(new MediaOpenObservation(true, [video, audio]), "d"));
        Assert.Null(PlaybackDiagnosticsPolicy.Diagnose(new MediaOpenObservation(true, [video]), "d"));
        Assert.Equal(
            PlaybackFailureCode.CorruptedMedia,
            PlaybackDiagnosticsPolicy.Diagnose(new MediaOpenObservation(false, [video]), "d")!.Code);
        Assert.Equal(
            PlaybackFailureCode.CorruptedMedia,
            PlaybackDiagnosticsPolicy.Diagnose(new MediaOpenObservation(true, []), "d")!.Code);
        Assert.Equal(
            PlaybackFailureCode.NoPlayableTrack,
            PlaybackDiagnosticsPolicy.Diagnose(new MediaOpenObservation(true, [subtitle]), "d")!.Code);
        Assert.Equal(
            PlaybackFailureCode.UnsupportedCodec,
            PlaybackDiagnosticsPolicy.Diagnose(new MediaOpenObservation(true, [undecodable]), "d")!.Code);
        Assert.Null(PlaybackDiagnosticsPolicy.Diagnose(new MediaOpenObservation(true, [undecodable, audio]), "d"));
        Assert.Throws<ArgumentNullException>(() => PlaybackDiagnosticsPolicy.Diagnose(null!, "d"));
    }

    [Fact]
    public void Missing_audio_is_a_notice_for_playable_media_not_a_failure()
    {
        var video = new MediaTrack("v", MediaTrackKind.Video, Codec: "H264");
        var audio = new MediaTrack("a", MediaTrackKind.Audio, Codec: "AAC");

        Assert.True(PlaybackDiagnosticsPolicy.IsMissingAudio([video]));
        Assert.False(PlaybackDiagnosticsPolicy.IsMissingAudio([video, audio]));
        Assert.False(PlaybackDiagnosticsPolicy.IsMissingAudio([audio]));
        Assert.False(PlaybackDiagnosticsPolicy.IsMissingAudio([]));
        Assert.Throws<ArgumentNullException>(() => PlaybackDiagnosticsPolicy.IsMissingAudio(null!));
    }

    [Fact]
    public void Recovery_never_offers_a_destructive_action_for_any_failure()
    {
        foreach (var code in Enum.GetValues<PlaybackFailureCode>())
        {
            var actions = PlaybackDiagnosticsPolicy.RecoveryActionsFor(code);

            Assert.NotEmpty(actions);
            Assert.Equal(actions.Distinct().Count(), actions.Count);
            Assert.All(actions, action => Assert.Contains(action, Enum.GetValues<PlaybackRecoveryAction>()));
        }

        Assert.Equal(
            [PlaybackRecoveryAction.Retry, PlaybackRecoveryAction.ChooseAnotherVersion],
            PlaybackDiagnosticsPolicy.RecoveryActionsFor(PlaybackFailureCode.FileNotFound));
        Assert.Equal(
            [PlaybackRecoveryAction.Retry],
            PlaybackDiagnosticsPolicy.RecoveryActionsFor(PlaybackFailureCode.EngineUnavailable));
        Assert.Equal(
            [PlaybackRecoveryAction.ChooseAnotherVersion, PlaybackRecoveryAction.OpenExternally],
            PlaybackDiagnosticsPolicy.RecoveryActionsFor(PlaybackFailureCode.UnsupportedCodec));
        Assert.Equal(
            [PlaybackRecoveryAction.ChooseAnotherVersion, PlaybackRecoveryAction.OpenExternally],
            PlaybackDiagnosticsPolicy.RecoveryActionsFor(PlaybackFailureCode.NoPlayableTrack));
        Assert.Equal(
            new PlaybackFailure(PlaybackFailureCode.CorruptedMedia, "d").RecoveryActions,
            PlaybackDiagnosticsPolicy.RecoveryActionsFor(PlaybackFailureCode.CorruptedMedia));
    }

    [Fact]
    public void Playback_contracts_stay_free_of_engine_and_user_interface_frameworks()
    {
        var assembly = typeof(IMediaPlayerEngine).Assembly;

        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            Assert.DoesNotContain(
                ForbiddenAssemblyTokens,
                token => reference.Name!.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        var contractTypes = assembly.GetTypes()
            .Where(type => type.Namespace == "ApSolutions.LocalMedia.Domain.Playback")
            .ToArray();

        Assert.NotEmpty(contractTypes);
        Assert.All(contractTypes, type => Assert.True(
            type.Assembly == assembly,
            $"{type.FullName} must live in the framework-free domain assembly."));
    }
}
