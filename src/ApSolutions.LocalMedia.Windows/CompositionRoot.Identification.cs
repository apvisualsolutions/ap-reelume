// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Settings;
using ApSolutions.LocalMedia.Presentation.Metadata;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.Windows.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace ApSolutions.LocalMedia.Windows;

public static partial class CompositionRoot
{
    /// <summary>
    /// Working out what a file is: the name parser, the scorer, the provider, and the inbox where
    /// anything doubtful waits for a person instead of being guessed at.
    /// </summary>
    /// <remarks>
    /// ARQ-006 step 2. Without a provider the review inbox has nothing to review, which is exactly
    /// the gap ADR-0003 found.
    /// </remarks>
    private static IServiceCollection AddIdentification(this IServiceCollection services) =>
        services
            .AddSingleton<IMetadataCache, SqliteMetadataCache>()
            .AddSingleton(_ => new TmdbOptions(
                Environment.GetEnvironmentVariable(TmdbOptions.EnvironmentVariableName)))
            .AddSingleton<TmdbRateLimiter>()
            .AddSingleton<IMetadataProvider>(provider => new TmdbMetadataProvider(
                CreateProviderClient(),
                provider.GetRequiredService<IMetadataCache>(),
                provider.GetRequiredService<TmdbOptions>(),
                provider.GetRequiredService<TmdbRateLimiter>(),
                TimeProvider.System))
            // ArtworkCache left the container with ART-A01 (2026-08-09): nothing fetched art and
            // no surface showed it, and wiring the whole chain — poster plumbing through the
            // candidate store, two image surfaces, network tests — is out of proportion for an
            // MVP whose remote identification ships disabled. The gap is documented in the plan
            // and in FEATURES; the class stays for personal artwork and for the day the gap
            // closes. Removed rather than left silent, the ARQ-A01 rule.
            .AddSingleton<IIdentificationCandidateSource>(provider => new MetadataCandidateSource(
                provider.GetRequiredService<IMetadataProvider>(),
                provider.GetRequiredService<IMetadataCache>(),
                CurrentMetadataLanguage(),
                // No token, no connection. Putting the token in place is the deliberate, revocable act
                // that consents to talking to the provider at all; the shipped artifact carries none.
                () => provider.GetRequiredService<TmdbOptions>().AccessToken is not null))
            .AddSingleton<IMediaNameParser, MediaNameParser>()
            .AddSingleton<ICandidateScorer, CandidateScorer>()
            .AddSingleton<IMatchCandidateRepository, MatchCandidateRepository>()
            .AddTransient<IdentifyMediaFile>()
            .AddTransient<IdentifyScannedFiles>()

            // What makes an identification visible. Both callers below resolve it, which is the
            // point: registered with nobody resolving it is the shape of the defect it repairs.
            .AddTransient(provider => new ApplyIdentification(
                provider.GetRequiredService<ICatalogMetadataRepository>(),
                provider.GetRequiredService<IMetadataProvider>(),
                provider.GetRequiredService<MetadataMergePolicy>(),
                CurrentMetadataLanguage(),
                TimeProvider.System))
            .AddTransient<GetReviewInbox>()
            .AddTransient<ResolveMatch>()
            .AddTransient<RejectMatch>()

            // What the inbox's Search button reaches. Until it existed the button raised an event
            // nothing listened to, so pressing it answered nothing at all — and the inbox is where a
            // person goes precisely because the automatic reading was not good enough.
            .AddTransient<SearchForMatch>()
            .AddTransient<ReviewInboxViewModel>();

    /// <summary>
    /// The three surfaces a title card leads to: correcting its metadata, renaming the files behind
    /// it, and deciding which copy of a duplicate is the one that plays.
    /// </summary>
    /// <remarks>ARQ-006 step 2.</remarks>
    private static IServiceCollection AddCatalogEditing(this IServiceCollection services) =>
        services
            .AddSingleton<ICatalogMetadataRepository, CatalogMetadataRepository>()
            .AddSingleton<MetadataMergePolicy>()

            // The trailer this application does not play (LIB-015). The provider's trailer is a
            // YouTube key, and the browser is the use YouTube's terms allow — so what is registered
            // is something that hands an address to the shell, not something that connects. The
            // declared network purposes are unchanged for that exact reason.
            //
            // Which exit is built is decided by the data root, once, here: the person whose profile
            // this is gets their browser, and a run that keeps its data somewhere of its own writes
            // the address down under that root instead of opening anything on the machine it is
            // running on. Both refuse the same addresses, because both ask the same policy.
            .AddSingleton<IExternalLinkLauncher>(provider =>
                provider.GetRequiredService<IAppDataPaths>().SystemHandoffDirectory is { } handoff
                    ? new RecordingExternalLinkLauncher(handoff)
                    : new ShellExternalLinkLauncher())
            .AddTransient<UpdateMetadata>()

            // The refresh resolves the provider entry from the row itself, so it needs the provider
            // — which is what the editor's two buttons were missing all along.
            .AddTransient(provider => new RefreshMetadata(
                provider.GetRequiredService<ICatalogMetadataRepository>(),
                provider.GetRequiredService<IMetadataProvider>(),
                provider.GetRequiredService<MetadataMergePolicy>(),
                CurrentMetadataLanguage(),
                TimeProvider.System))

            // LIB-016. Off by default, and off means the repository is not even read. The pass runs
            // once per launch, after the window is painted, and yields to a scan or to playback.
            .AddSingleton<IAutoRefreshSettings, StoredAutoRefreshSettings>()
            .AddTransient(provider => new RefreshStaleMetadata(
                provider.GetRequiredService<ICatalogMetadataRepository>(),
                provider.GetRequiredService<RefreshMetadata>(),
                provider.GetRequiredService<IAutoRefreshSettings>(),
                provider.GetRequiredService<IPlaybackActivity>(),
                provider.GetRequiredService<IScanActivity>(),
                TimeProvider.System))
            .AddTransient<ArtworkPickerViewModel>()
            .AddSingleton<RenamePolicy>()
            .AddSingleton<PreviewRename>()
            .AddSingleton<ISafeFileRenamer>(provider => new SafeFileRenamer(
                provider.GetRequiredService<SqliteConnectionFactory>()))
            .AddTransient<ExecuteRename>()
            .AddTransient<UndoRename>()
            .AddSingleton<IMediaVersionGroupRepository, MediaVersionGroupRepository>()
            .AddSingleton<MediaVersionSelectionPolicy>()
            .AddSingleton<DuplicateGroupingPolicy>()
            .AddTransient<GroupMediaVersions>()
            .AddTransient<GroupScannedVersions>()
            .AddTransient<GroupScannedEpisodes>()
            .AddTransient<SetPreferredVersion>();
}
