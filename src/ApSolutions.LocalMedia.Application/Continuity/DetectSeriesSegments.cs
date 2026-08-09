using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>One series to read again, named both ways the catalogue knows it.</summary>
public sealed record DetectSeriesSegmentsCommand(TitleId ShowId, SeriesId SeriesId);

/// <summary>Why a detection run ended the way it did.</summary>
public enum DetectSegmentsOutcome
{
    Completed,
    Disabled,
    NothingToRead,
}

/// <summary>What one run achieved. A cancelled run throws instead; it never reports halfway.</summary>
public sealed record DetectSeriesSegmentsResult(
    DetectSegmentsOutcome Outcome,
    int EpisodesRead,
    int MarkersStored);

/// <summary>
/// Whether an embedded playback session is running right now. Detection always yields to playback:
/// the person watching something is the whole point of the application.
/// </summary>
public interface IPlaybackActivity
{
    bool IsPlaybackActive { get; }
}

/// <summary>
/// Runs the local segment detector over one series and stores what the policy keeps. The switch is
/// off until a person turns it on, a run is cancelable at any moment, and cancellation leaves the
/// stored rows exactly as they were.
/// </summary>
public sealed class DetectSeriesSegments
{
    /// <summary>Where the person's choice to enable detection is stored between sessions.</summary>
    public const string EnabledSettingKey = "continuity.segment-detection-enabled";

    private readonly ISettingsStore _settings;
    private readonly IEpisodeSequenceRepository _episodes;
    private readonly IAutomaticSegmentDetector _detector;
    private readonly IDetectedMarkerRepository _repository;

    public DetectSeriesSegments(
        ISettingsStore settings,
        IEpisodeSequenceRepository episodes,
        IAutomaticSegmentDetector detector,
        IDetectedMarkerRepository repository)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _episodes = episodes ?? throw new ArgumentNullException(nameof(episodes));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>Detection is an option a person chooses; nothing runs while this is false.</summary>
    public bool IsEnabled => _settings.Read<bool?>(EnabledSettingKey) ?? false;

    public void SetEnabled(bool enabled) => _settings.Write(EnabledSettingKey, enabled);

    public async Task<DetectSeriesSegmentsResult> ExecuteAsync(
        DetectSeriesSegmentsCommand command,
        IProgress<SegmentDetectionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!IsEnabled)
        {
            return new DetectSeriesSegmentsResult(DetectSegmentsOutcome.Disabled, 0, 0);
        }

        var entries = await _episodes.GetSeriesAsync(command.ShowId, cancellationToken).ConfigureAwait(false);
        var episodes = entries
            .Where(entry => entry.IsPlayable)
            .Select(entry => new SegmentDetectionEpisode(entry.MediaFileId!.Value, entry.Path!, Duration: null))
            .ToList();
        if (episodes.Count == 0)
        {
            return new DetectSeriesSegmentsResult(DetectSegmentsOutcome.NothingToRead, 0, 0);
        }

        var detection = await _detector
            .DetectAsync(command.SeriesId, episodes, progress, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await _repository
            .GetForSeriesAsync(command.SeriesId, cancellationToken)
            .ConfigureAwait(false);
        var merged = SegmentDetectionPolicy.MergeDetections(detection, existing);
        await _repository
            .ReplaceForSeriesAsync(command.SeriesId, merged, cancellationToken)
            .ConfigureAwait(false);

        return new DetectSeriesSegmentsResult(
            DetectSegmentsOutcome.Completed,
            episodes.Count,
            merged.Count(row => !row.UserCorrected));
    }
}
