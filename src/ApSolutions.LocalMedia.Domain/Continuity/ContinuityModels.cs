using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>How far through a piece of content the person has got.</summary>
public enum WatchStatus
{
    NotStarted,
    InProgress,
    Watched,
}

/// <summary>
/// What progress belongs to. Progress follows the content, not the file, so replacing a movie with a
/// better version or moving an episode keeps the same key.
/// </summary>
public readonly record struct ContentKey(TitleId TitleId, EpisodeId? EpisodeId)
{
    private const string TitlePrefix = "title:";
    private const string EpisodeSeparator = "/episode:";

    /// <summary>The stable text used as the storage key; never a path or a file name.</summary>
    public string Value => EpisodeId is { } episode
        ? string.Create(
            CultureInfo.InvariantCulture,
            $"{TitlePrefix}{TitleId.Value:D}{EpisodeSeparator}{episode.Value:D}")
        : string.Create(CultureInfo.InvariantCulture, $"{TitlePrefix}{TitleId.Value:D}");

    public static ContentKey ForTitle(TitleId titleId) => new(titleId, null);

    public static ContentKey ForEpisode(TitleId titleId, EpisodeId episodeId) => new(titleId, episodeId);

    public static ContentKey Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var separator = value.IndexOf(EpisodeSeparator, StringComparison.Ordinal);
        var titlePart = separator < 0 ? value : value[..separator];
        if (!titlePart.StartsWith(TitlePrefix, StringComparison.Ordinal))
        {
            throw new FormatException($"'{value}' is not a content key.");
        }

        var titleId = new TitleId(Guid.Parse(titlePart[TitlePrefix.Length..], CultureInfo.InvariantCulture));
        return separator < 0
            ? new ContentKey(titleId, null)
            : new ContentKey(
                titleId,
                new EpisodeId(Guid.Parse(
                    value[(separator + EpisodeSeparator.Length)..],
                    CultureInfo.InvariantCulture)));
    }

    public override string ToString() => Value;
}

/// <summary>
/// Everything remembered about one piece of content: where it was left, how long it was observed to
/// be, which version produced that reading, and whether a person set the status by hand.
/// </summary>
public sealed record WatchState
{
    public required ContentKey Content { get; init; }

    public required TimeSpan Position { get; init; }

    /// <summary>What the engine reported as the length of the version that was playing; null when unknown.</summary>
    public TimeSpan? ObservedDuration { get; init; }

    /// <summary>The version the position came from, kept so a later transfer can be audited.</summary>
    public required MediaFileId SourceMediaFileId { get; init; }

    public required WatchStatus Status { get; init; }

    /// <summary>True while a person's explicit decision is in force; automatic progress never clears it.</summary>
    public required bool IsManualOverride { get; init; }

    public required DateTimeOffset StartedUtc { get; init; }

    public required DateTimeOffset UpdatedUtc { get; init; }
}
