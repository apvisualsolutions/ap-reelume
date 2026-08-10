// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Presentation.Home;
using ApSolutions.LocalMedia.Presentation.Library;
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
                onResume: null,
                provider.GetRequiredService<RecommendationsViewModel>()));
}
