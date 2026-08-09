using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Presentation.Show;

/// <summary>
/// One season and its episodes in viewing order. Season zero is the specials season and says so in
/// words, because it is a deliberate choice rather than part of the run.
/// </summary>
public sealed class SeasonViewModel
{
    public SeasonViewModel(int seasonNumber, IReadOnlyList<EpisodeRowViewModel> episodes)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentOutOfRangeException.ThrowIfNegative(seasonNumber);
        SeasonNumber = seasonNumber;
        Episodes = episodes;
    }

    public int SeasonNumber { get; }

    public bool IsSpecials => SeasonNumber == 0;

    public bool IsRegular => !IsSpecials;

    public string SeasonNumberText => SeasonNumber.ToString(CultureInfo.CurrentCulture);

    public IReadOnlyList<EpisodeRowViewModel> Episodes { get; }

    public int EpisodeCount => Episodes.Count;

    public string EpisodeCountText => EpisodeCount.ToString(CultureInfo.CurrentCulture);
}

/// <summary>
/// One episode row: where it sits, whether it can be opened right now, and how far through it the
/// person is. An episode with no file stays listed so the season never looks shorter than it is.
/// </summary>
public sealed class EpisodeRowViewModel
{
    private readonly EpisodeSequenceEntry _entry;
    private readonly WatchState? _watchState;

    public EpisodeRowViewModel(EpisodeSequenceEntry entry, WatchState? watchState)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _watchState = watchState;
    }

    public EpisodeId Id => _entry.Id;

    public int SeasonNumber => _entry.SeasonNumber;

    public int EpisodeNumber => _entry.EpisodeNumber;

    public string EpisodeNumberText => EpisodeNumber.ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// Which episode this row is, in the form every catalogue writes it. Ten "Play episode" buttons
    /// announce the same sentence without it, so a reader user cannot tell which one they are on.
    /// </summary>
    public string SeasonEpisodeLabel => string.Create(
        CultureInfo.CurrentCulture,
        $"S{SeasonNumber:D2}E{EpisodeNumber:D2}");

    public MediaFileId? MediaFileId => _entry.MediaFileId;

    public bool IsAvailable => _entry.IsAvailable;

    /// <summary>True only when a reachable file exists behind this episode.</summary>
    public bool IsPlayable => _entry.IsPlayable;

    public WatchStatus WatchStatus => _watchState?.Status ?? Domain.Continuity.WatchStatus.NotStarted;

    public bool IsWatched => WatchStatus == Domain.Continuity.WatchStatus.Watched;

    public bool IsInProgress => WatchStatus == Domain.Continuity.WatchStatus.InProgress;

    public bool IsNotStarted => WatchStatus == Domain.Continuity.WatchStatus.NotStarted;

    /// <summary>True while a decision made by hand is in force, announced in words rather than colour.</summary>
    public bool IsManualOverride => _watchState?.IsManualOverride ?? false;
}
