using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>
/// One episode of a series as far as continuity is concerned: where it sits in the order and whether
/// there is something playable behind it right now.
/// </summary>
public sealed record EpisodeSequenceEntry(
    EpisodeId Id,
    TitleId ShowId,
    int SeasonNumber,
    int EpisodeNumber,
    MediaFileId? MediaFileId,
    string? Path,
    bool IsAvailable)
{
    /// <summary>Season zero is the specials season, which is never chained into automatically.</summary>
    public bool IsSpecial => SeasonNumber == 0;

    /// <summary>True when the catalogue has a file for this episode and the file is reachable.</summary>
    public bool IsPlayable => IsAvailable && MediaFileId is not null && !string.IsNullOrWhiteSpace(Path);
}

/// <summary>
/// Which episode follows which. Ordering is season then episode with specials at the end, and the
/// chain only ever offers something that can actually be played.
/// </summary>
public static class NextEpisodePolicy
{
    /// <summary>Standard viewing order: seasons in ascending order, specials last.</summary>
    public static IReadOnlyList<EpisodeSequenceEntry> Order(IEnumerable<EpisodeSequenceEntry> episodes)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        return
        [
            .. episodes
                .OrderBy(entry => entry.IsSpecial)
                .ThenBy(entry => entry.SeasonNumber)
                .ThenBy(entry => entry.EpisodeNumber)
        ];
    }

    /// <summary>
    /// The next playable episode after this one, or null when there is none. A regular episode never
    /// runs on into a special, and a special only continues into another special, because a special is
    /// something a person chooses deliberately.
    /// </summary>
    public static EpisodeSequenceEntry? FindNext(
        IEnumerable<EpisodeSequenceEntry> episodes,
        EpisodeId current)
    {
        var ordered = Order(episodes);
        var index = -1;
        for (var position = 0; position < ordered.Count; position++)
        {
            if (ordered[position].Id == current)
            {
                index = position;
                break;
            }
        }

        if (index < 0)
        {
            return null;
        }

        var isSpecial = ordered[index].IsSpecial;
        for (var position = index + 1; position < ordered.Count; position++)
        {
            var candidate = ordered[position];
            if (candidate.IsSpecial != isSpecial)
            {
                return null;
            }

            if (candidate.IsPlayable)
            {
                return candidate;
            }
        }

        return null;
    }
}

/// <summary>
/// Reads the episodes of one series and re-reads a single episode when its availability has to be
/// confirmed at the last moment.
/// </summary>
public interface IEpisodeSequenceRepository
{
    Task<IReadOnlyList<EpisodeSequenceEntry>> GetSeriesAsync(
        TitleId showId,
        CancellationToken cancellationToken = default);

    Task<EpisodeSequenceEntry?> GetAsync(EpisodeId episodeId, CancellationToken cancellationToken = default);

    /// <summary>The episode a media file backs, or null when the file is not an episode.</summary>
    Task<EpisodeSequenceEntry?> FindByFileAsync(
        MediaFileId fileId,
        CancellationToken cancellationToken = default);
}
