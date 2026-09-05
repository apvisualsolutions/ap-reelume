// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
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
                () => provider.GetRequiredService<GroupScannedVersions>(),
                () => provider.GetRequiredService<GroupScannedEpisodes>(),
                () => provider.GetRequiredService<NameScannedTitles>()))
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
            .AddTransient(provider =>
            {
                var onboarding = new RootOnboardingViewModel(
                    provider.GetRequiredService<AddLibraryRoot>(),
                    provider.GetRequiredService<RemoveLibraryRoot>(),
                    provider.GetRequiredService<ILibraryRootRepository>());

                // The dialog's two host answers, decided here the way the archive pickers are:
                // the kind is read from the path, and Browse goes to the Windows picker for the
                // person whose profile this is, or stays inside the handover folder for a run
                // that owns a data root of its own.
                onboarding.KindDetector = DetectRootKind;
                onboarding.FolderPicker = HandoffOrDialog(
                    provider,
                    picker => picker.ChooseMediaFolderAsync,
                    ChooseMediaFolderDialogAsync);
                return onboarding;
            })
            .AddSingleton<ScanProgressViewModel>()
            .AddSingleton<RootNoticeViewModel>()
            .AddTransient<ManualReassignmentViewModel>()
            .AddSingleton<Application.Catalog.IDuplicateOverviewReader, ApSolutions.LocalMedia.Infrastructure.Data.Repositories.DuplicateOverviewReader>()
            .AddTransient(provider => new Application.Catalog.GetDuplicateOverview(
                provider.GetRequiredService<Application.Catalog.IDuplicateOverviewReader>()))
            .AddTransient(CreateLibraryViewModel)
            .AddTransient(provider => new RecommendationsViewModel(
                provider.GetRequiredService<GetRecommendations>(),
                provider.GetRequiredService<IRecommendationSettings>(),

                // The rail's titles, which nothing was feeding: the parameter defaulted to a lookup
                // that answers the empty string, so every suggestion was drawn as initials of
                // nothing under a blank caption. Registered and never fed, in its quietest form —
                // the rail rendered, the cards had the right shape, and there was no error anywhere.
                //
                // The catalogue is read once and remembered for the rail's lifetime, which is the
                // shape a synchronous lookup can have at all: the formula ranks ids and the words
                // live in the catalogue, and asking it per card would be twenty queries for one row
                // of pictures.
                CatalogTitleLookup(provider),
                onOpenDetails: titleId => OpenTitleCardAsync(provider, titleId)))
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
                onResume: async request =>
                {
                    if (provider.GetRequiredService<ShellHost>().Shell is not { } shell)
                    {
                        return;
                    }

                    var state = await provider.GetRequiredService<IWatchStateRepository>()
                        .GetAsync(request.Content, CancellationToken.None)
                        .ConfigureAwait(true);
                    if (state is null)
                    {
                        return;
                    }

                    // Zero when the glyph beside Continue was the one pressed, and the stored
                    // point otherwise. Both are a position the caller named, which is what stops the
                    // player asking the same question over again once it is open.
                    await shell.OpenPlayerAsync(
                        new PlayDetailsRequest(
                            state.SourceMediaFileId,
                            request.FromStart
                                ? TimeSpan.Zero
                                : ProgressPolicy.ClampPosition(state.Position, state.ObservedDuration),
                            request.Title,
                            request.Subtitle),
                        CancellationToken.None).ConfigureAwait(true);
                },
                provider.GetRequiredService<RecommendationsViewModel>(),

                // Detalles, the hero's second action, which the prototype has had all along. It
                // lands where the library's own grid lands: the shell navigates to the library and
                // the library opens that title's card, so there is one way into a card and not two.
                // The catalogue is asked for the row rather than the row being carried from Home:
                // what a card needs is the whole CatalogItem, and the read model behind the hero
                // answers with a title id.
                onOpenDetails: titleId => OpenTitleCardAsync(provider, titleId)));

    /// <summary>
    /// Opens one title's card from wherever on Home it was pressed.
    /// </summary>
    /// <remarks>
    /// It lands where the library's own grid lands: the shell navigates to the library and the
    /// library opens that title's card, so there is one way into a card and not two. The catalogue is
    /// asked for the row rather than the row being carried from Home — what a card needs is the whole
    /// <c>CatalogItem</c>, and the read models behind the rails answer with a title id.
    /// <para>
    /// A method and no longer a lambda, because three surfaces reach it now: the hero's Detalles, the
    /// wide card's, and the cover of a card on either of the two poster rails.
    /// </para>
    /// </remarks>
    private static async Task OpenTitleCardAsync(IServiceProvider provider, TitleId titleId)
    {
        if (provider.GetRequiredService<ShellHost>().Shell is not { Library: { } library } shell)
        {
            return;
        }

        var page = await provider.GetRequiredService<ICatalogQueryService>()
            .QueryAsync(new CatalogQuery(PageSize: 100), CancellationToken.None)
            .ConfigureAwait(true);
        if (page.Items.FirstOrDefault(item => item.Id == titleId) is not { } row)
        {
            return;
        }

        shell.NavigateCommand.Execute(AppRoute.Library);
        await library.OpenDetailsAsync(
            new CatalogItemViewModel(row),
            CancellationToken.None).ConfigureAwait(true);
    }

    /// <summary>
    /// The words behind the ids a formula ranks.
    /// </summary>
    /// <remarks>
    /// One query for the whole rail, and it answers with a map rather than with a string per card:
    /// the recommendation read model deals in identifiers and the catalogue is where the words are,
    /// so this is the join, made once per load. A title the catalogue does not hold is left out of
    /// the map and its card falls back to the empty caption it already draws.
    /// </remarks>
    private static Func<IReadOnlyList<TitleId>, CancellationToken, Task<IReadOnlyDictionary<TitleId, string>>>
        CatalogTitleLookup(IServiceProvider provider) =>
        async (ids, cancellationToken) =>
        {
            if (ids.Count == 0)
            {
                return new Dictionary<TitleId, string>();
            }

            var wanted = ids.ToHashSet();
            var page = await provider.GetRequiredService<ICatalogQueryService>()
                .QueryAsync(new CatalogQuery(PageSize: 200), cancellationToken)
                .ConfigureAwait(false);
            return page.Items
                .Where(item => wanted.Contains(item.Id))
                .GroupBy(item => item.Id)
                .ToDictionary(group => group.Key, group => group.First().Title);
        };
}
