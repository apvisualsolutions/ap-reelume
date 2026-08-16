// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Library;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace ApSolutions.LocalMedia.Windows;

public static partial class CompositionRoot
{
    /// <summary>
    /// Finding the media and keeping the catalog honest about it: the scan, the watchers that start
    /// one, the reconciliation that survives a file moving, and the surfaces that show all of it.
    /// </summary>
    /// <remarks>ARQ-006 step 2.</remarks>
    private static IServiceCollection AddLibrary(this IServiceCollection services) =>
        services
            .AddSingleton<ScanCoordinator>()

            // Whether a scan is running, answered by the one that runs them. Background work that
            // must not compete with a scan reads this, so it has to be the same instance the
            // coordinator counts in — not a second one that would always say no.
            .AddSingleton<IScanActivity>(provider => provider.GetRequiredService<ScanCoordinator>())
            // Identification rides inside the shared coordinator: a watcher-triggered scan feeds
            // the review inbox exactly like a manual one, instead of identification being a
            // courtesy of whichever caller remembered it.
            .AddSingleton<IScanCoordinator>(provider => new IdentifyingScanCoordinator(
                provider.GetRequiredService<ScanCoordinator>(),
                () => provider.GetRequiredService<ReconcileScannedFiles>(),
                () => provider.GetRequiredService<IdentifyScannedFiles>(),
                () => provider.GetRequiredService<GroupScannedVersions>()))
            .AddSingleton<RootWatchCoordinator>()
            .AddSingleton<RootWatchBackground>()
            .AddSingleton<FileReconciliationPolicy>()
            .AddSingleton<ReconcileScanResults>()
            // The identity a scan captures is what lets a moved file keep being its entity: the
            // stable NTFS id when the volume has one, the bounded fingerprint always.
            .AddSingleton<IFileIdentityProvider>(_ => new CompositeFileIdentityProvider(
                new NtfsFileIdentityProvider(),
                new LightweightFingerprintProvider()))
            .AddSingleton<PendingReassignments>()
            .AddTransient<ReconcileScannedFiles>()
            .AddTransient<AddLibraryRoot>()
            .AddTransient<RemoveLibraryRoot>()
            .AddTransient<RootOnboardingViewModel>()
            .AddSingleton<ScanProgressViewModel>()
            .AddTransient<ManualReassignmentViewModel>()
            .AddTransient(CreateLibraryViewModel)
            .AddTransient(provider => new RecommendationsViewModel(
                provider.GetRequiredService<GetRecommendations>(),
                provider.GetRequiredService<IRecommendationSettings>()))
            .AddTransient(provider => new HomeViewModel(
                provider.GetRequiredService<GetHome>(),
                provider.GetRequiredService<INavigationService>(),

                // Continue is the primary action of the whole application, and it was built with no
                // handler at all: Home offered it, the button enabled itself because there was
                // progress to return to, and pressing it did nothing. The characteristic defect of
                // this repository, on the first surface anybody sees.
                //
                // What it opens is the version the position was read from — watch_state keeps it for
                // exactly this — at the position the progress policy allows. The shell is read at
                // press time rather than captured: this view model is built while the shell is still
                // being assembled.
                onResume: async content =>
                {
                    if (provider.GetRequiredService<ShellHost>().Shell is not { } shell)
                    {
                        return;
                    }

                    var state = await provider.GetRequiredService<IWatchStateRepository>()
                        .GetAsync(content, CancellationToken.None)
                        .ConfigureAwait(true);
                    if (state is null)
                    {
                        return;
                    }

                    await shell.OpenPlayerAsync(
                        new PlayDetailsRequest(
                            state.SourceMediaFileId,
                            ProgressPolicy.ClampPosition(state.Position, state.ObservedDuration)),
                        CancellationToken.None).ConfigureAwait(true);
                },
                provider.GetRequiredService<RecommendationsViewModel>()));
}
