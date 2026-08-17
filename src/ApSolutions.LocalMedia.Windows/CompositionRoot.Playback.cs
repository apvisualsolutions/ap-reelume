// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Windows.MediaKeys;
using ApSolutions.LocalMedia.Windows.Playback;
using ApSolutions.LocalMedia.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace ApSolutions.LocalMedia.Windows;

public static partial class CompositionRoot
{
    /// <summary>
    /// Everything a session touches: the engine, the surfaces that drive it, what it remembers about
    /// where you were, and the markers and segments it can skip.
    /// </summary>
    /// <remarks>ARQ-006 step 2.</remarks>
    private static IServiceCollection AddPlayback(this IServiceCollection services) =>
        services
            .AddSingleton(_ => LibVlcFactory.CreateDefault())
            .AddSingleton<IDisplayCapabilityProvider, WindowsDisplayCapabilityProvider>()
            .AddSingleton<IAudioDeviceCatalog, WindowsAudioDeviceCatalog>()
            .AddSingleton<LibVlcAudioOutputAdapter>()
            .AddTransient<AudioOutputViewModel>()
            .AddSingleton<LibVlcMediaPlayerEngine>()
            .AddSingleton<IMediaPlayerEngine>(provider => provider.GetRequiredService<LibVlcMediaPlayerEngine>())
            .AddSingleton<IVideoFrameSource>(provider => provider.GetRequiredService<LibVlcMediaPlayerEngine>())
            // The ninth exit the isolation rule covers, and the same choice by the same data root:
            // "Open with an external application" starts a real process, so a run that does not own
            // this profile writes down the file it would have handed over instead of opening the
            // system's player on the machine measuring it.
            .AddSingleton<IExternalPlaybackLauncher>(provider =>
                provider.GetRequiredService<IAppDataPaths>().SystemHandoffDirectory is not null
                    ? new RecordingExternalPlaybackLauncher(
                        provider.GetRequiredService<RecordingSystemHandoff>())
                    : new ShellExternalPlaybackLauncher())
            .AddSingleton<PlaybackSessionCoordinator>()
            .AddSingleton<IPlaybackSessionCoordinator>(provider =>
                provider.GetRequiredService<PlaybackSessionCoordinator>())
            .AddSingleton<IPlaybackPreferenceRepository, PlaybackPreferenceRepository>()
            .AddSingleton<IWatchStateRepository, WatchStateRepository>()
            .AddSingleton(provider => new PlaybackProgressTracker(
                provider.GetRequiredService<IWatchStateRepository>(),
                provider.GetRequiredService<IClock>()))
            .AddTransient<ResumePlayback>()
            .AddTransient<SetWatchStatus>()
            .AddTransient<ConfigureWatchedThreshold>()
            .AddTransient<SwitchMediaVersion>()
            .AddSingleton<IEpisodeSequenceRepository, EpisodeSequenceRepository>()
            .AddTransient<GetNextEpisode>()
            .AddTransient<StartNextEpisodeCountdown>()
            .AddSingleton<IIntroMarkerRepository, IntroMarkerRepository>()
            .AddTransient<SaveManualMarker>()
            .AddTransient<DeleteManualMarker>()
            .AddSingleton<IDetectedMarkerRepository, DetectedMarkerRepository>()
            .AddSingleton<ISegmentFeatureExtractor>(provider =>
                new LocalSegmentFeatureExtractor(
                    provider.GetRequiredService<LibVlcFactory>(),
                    provider.GetRequiredService<IMediaProbe>()))
            .AddSingleton<IPlaybackActivity, CoordinatorPlaybackActivity>()
            .AddSingleton<IAutomaticSegmentDetector>(provider => new AutomaticSegmentDetector(
                provider.GetRequiredService<ISegmentFeatureExtractor>(),
                provider.GetRequiredService<IPlaybackActivity>()))
            .AddTransient<DetectSeriesSegments>()
            .AddTransient<ReviewDetectedSegments>()
            .AddSingleton(provider => new SegmentDetectionScheduler(
                () => provider.GetRequiredService<DetectSeriesSegments>()))
            .AddSingleton(provider => new LooseFileViewModel(folder =>
                provider.GetRequiredService<AddLibraryRoot>()
                    .ExecuteAsync(
                        new AddLibraryRootCommand(folder, RootKind.Local, ScanPolicy.Manual),
                        CancellationToken.None)))
            .AddTransient<SelectTrack>()
            .AddTransient<ApplyPlaybackPreferences>()
            // A seek is a moment the person chose on purpose; it is flushed so a crash right after
            // costs nothing. The observation lands first so the flush writes the chosen position.
            .AddSingleton(provider => new ControlPlayback(
                provider.GetRequiredService<IMediaPlayerEngine>(),
                async (position, duration, token) =>
                {
                    var tracker = provider.GetRequiredService<PlaybackProgressTracker>();
                    tracker.Observe(position, duration);
                    _ = await tracker.FlushAsync(PersistenceTrigger.Seek, token).ConfigureAwait(false);
                }))
            .AddSingleton<ChangePlaybackMode>()
            .AddSingleton<IMediaKeySource, WindowsMediaKeyService>()
            .AddSingleton<ShortcutMap>()
            .AddTransient<ShortcutSettingsViewModel>()
            .AddTransient<TransportControlsViewModel>()
            .AddTransient<SubtitleStyleViewModel>();

    /// <summary>
    /// What the library knows about you rather than about the files: the home surface, the marks you
    /// put on a title, and the recommendations drawn from both. All of it stays on this machine.
    /// </summary>
    /// <remarks>ARQ-006 step 2.</remarks>
    private static IServiceCollection AddPersonalisation(this IServiceCollection services) =>
        services
            .AddSingleton<IHomeReadModel, HomeReadModel>()
            .AddSingleton<IPersonalStateRepository, PersonalStateRepository>()
            .AddTransient<SetPersonalState>()
            .AddTransient<GetPersonalFilters>()
            .AddSingleton<IRecommendationReadModel, RecommendationReadModel>()
            .AddTransient<GetRecommendations>()
            .AddTransient<GetHome>();
}
