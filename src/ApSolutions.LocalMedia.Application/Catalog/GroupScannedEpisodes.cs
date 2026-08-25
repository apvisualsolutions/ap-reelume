// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Catalog;

/// <summary>How many series a scan assembled, and how many episodes went into them.</summary>
public sealed record GroupScannedEpisodesResult(int SeriesCount, int EpisodeCount);

/// <summary>
/// Turns a folder of episodes into a series in the catalogue, with no provider and no network.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue has had <c>titles</c>, <c>seasons</c>, <c>episodes</c> and <c>episode_media</c>
/// since migration 0004, the series card has been drawn and routed to since it was written, and
/// <b>nothing had ever written a row into any of the four</b>. Every scanned file became one loose
/// card, so the owner's two shows — eight seasons and seventy-four episodes of one, three and
/// twenty-five of the other — arrived as a hundred and two cards in the grid. This is the caller
/// those four tables were waiting for, and it is the same shape <c>GroupScannedVersions</c> already
/// has for duplicates: run after every scan, decide from the names, write through the ports.
/// </para>
/// <para>
/// It only ever adds. A show a provider identified keeps whatever the provider said, because the
/// title row it writes is keyed by the <b>media file</b> and the one written here is keyed by the
/// series folder — the two can never collide. A file already claimed by an identified title is
/// skipped outright, so an identification made by a person is never overwritten by a folder name.
/// </para>
/// <para>
/// It is also idempotent by construction: every identifier it writes is derived from the series key
/// and the numbers, so the same folder scanned twice writes the same rows twice, and a season that
/// grows adds rows instead of renumbering the ones already there.
/// </para>
/// </remarks>
public sealed class GroupScannedEpisodes
{
    private readonly ILibraryRootRepository _roots;
    private readonly IMediaFileRepository _mediaFiles;
    private readonly ICatalogRepository _catalog;
    private readonly IMediaNameParser _parser;

    public GroupScannedEpisodes(
        ILibraryRootRepository roots,
        IMediaFileRepository mediaFiles,
        ICatalogRepository catalog,
        IMediaNameParser parser)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _mediaFiles = mediaFiles ?? throw new ArgumentNullException(nameof(mediaFiles));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public async Task<GroupScannedEpisodesResult> ExecuteAsync(
        ScanSummary summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.IsCancelled
            || await _roots.GetAsync(summary.RootId, cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return new GroupScannedEpisodesResult(0, 0);
        }

        // Everything the scan saw, placed. A series is assembled from the whole set rather than file
        // by file, because what a show is available and when it was added are questions about all of
        // its episodes: a show whose only reachable copy is one episode is still reachable, and one
        // added yesterday is not a show added last year.
        var episodes = new List<(LocalSeriesPlacement Placement, MediaFile File)>();
        foreach (var item in summary.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Outcome is not (ScanItemOutcome.Added or ScanItemOutcome.Updated or ScanItemOutcome.Unchanged)
                || await _mediaFiles.FindByPathAsync(summary.RootId, item.Path, cancellationToken)
                    .ConfigureAwait(false) is not { } file)
            {
                continue;
            }

            var context = FileNameContext.ForFile(item.Path, root.Path);
            if (LocalSeriesPolicy.Place(summary.RootId.Value, context, _parser.Parse(context))
                is not { } placement)
            {
                continue;
            }

            episodes.Add((placement, file));
        }

        var series = episodes
            .GroupBy(entry => entry.Placement.SeriesKey, StringComparer.Ordinal)
            .ToArray();
        var written = 0;
        foreach (var show in series)
        {
            cancellationToken.ThrowIfCancellationRequested();
            written += await WriteSeriesAsync(show.Key, [.. show], cancellationToken).ConfigureAwait(false);
        }

        return new GroupScannedEpisodesResult(series.Length, written);
    }

    private async Task<int> WriteSeriesAsync(
        string seriesKey,
        IReadOnlyList<(LocalSeriesPlacement Placement, MediaFile File)> episodes,
        CancellationToken cancellationToken)
    {
        var showId = new TitleId(LocalSeriesPolicy.ShowIdFor(seriesKey));

        // The title the folder gives it, and the earliest thing in it: a show is as old as its first
        // episode, which is what «Añadido recientemente» has to sort by for a season that arrives one
        // episode at a time.
        var title = episodes[0].Placement.SeriesTitle;
        var added = episodes.Min(entry => entry.File.LastWriteUtc);
        var isAvailable = episodes.Any(entry => entry.File.IsAvailable);

        await _catalog.UpsertTitleAsync(
            new CatalogTitle(
                showId,
                CatalogTitleKind.Show,
                title,
                title,
                Year: null,
                AlternateTitles: [],
                Cast: [],
                Genres: [],
                added,
                LastPlayedUtc: null,
                HasProgress: false,
                IsPersonal: false,
                isAvailable),
            cancellationToken).ConfigureAwait(false);

        // The seasons before the episodes, because an episode's foreign key points at one. The season
        // has no name of its own here — the folder said «Temporada 3» and the number is the whole of
        // it — so the row carries the number and the card writes the word in whichever language is in
        // force.
        foreach (var seasonNumber in episodes
            .Select(entry => entry.Placement.SeasonNumber)
            .Distinct()
            .Order())
        {
            await _catalog.UpsertSeasonAsync(
                new CatalogSeason(
                    showId,
                    seasonNumber,
                    seasonNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                cancellationToken).ConfigureAwait(false);
        }

        var written = 0;
        foreach (var group in episodes.GroupBy(entry => (
            entry.Placement.SeasonNumber,
            entry.Placement.EpisodeNumber)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // One row per number, and the copy behind it is the reachable one when there is a choice:
            // two files claiming T01E01 are a duplicate, which is the version grouping's question and
            // not this one's. Picking the reachable copy is what stops a disconnected backup from
            // being the episode the card offers to play.
            var entry = group.FirstOrDefault(candidate => candidate.File.IsAvailable, group.First());
            var placement = entry.Placement;
            var episodeId = new EpisodeId(LocalSeriesPolicy.EpisodeIdFor(
                seriesKey,
                placement.SeasonNumber,
                placement.EpisodeNumber));

            await _catalog.UpsertEpisodeAsync(
                new CatalogEpisode(
                    episodeId,
                    showId,
                    placement.SeasonNumber,
                    placement.EpisodeNumber,
                    AbsoluteNumber: null,
                    placement.EpisodeTitle,
                    SortOrder: (placement.SeasonNumber * 1000) + placement.EpisodeNumber,
                    entry.File.IsAvailable),
                cancellationToken).ConfigureAwait(false);
            await _catalog.LinkEpisodeMediaAsync(episodeId, entry.File.Id, cancellationToken)
                .ConfigureAwait(false);
            written++;
        }

        return written;
    }
}
