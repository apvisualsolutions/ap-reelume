using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Identification;

public sealed record DuplicateFileMatch(
    MediaFileId MediaFileId,
    string StableContentKey,
    ParsedMediaName ParsedName);

public sealed record DuplicateGroupingDecision(
    bool CanGroup,
    bool RequiresConfirmation,
    string ReasonCode,
    IReadOnlyList<MediaFileId> VisibleFileIds);

public sealed class DuplicateGroupingPolicy
{
    private readonly StringComparer _stableKeyComparer = StringComparer.Ordinal;

    public DuplicateGroupingDecision Assess(IReadOnlyList<DuplicateFileMatch> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (files.Count < 2)
        {
            throw new ArgumentException("At least two files are required.", nameof(files));
        }

        var first = files[0];
        var exactContent = files.All(file =>
            _stableKeyComparer.Equals(file.StableContentKey, first.StableContentKey)
            && file.ParsedName.Kind == first.ParsedName.Kind
            && file.ParsedName.Season == first.ParsedName.Season
            && file.ParsedName.Episode == first.ParsedName.Episode);
        var canGroup = exactContent
            && first.ParsedName.Kind is ParsedMediaKind.Movie or ParsedMediaKind.Episode;

        return new DuplicateGroupingDecision(
            canGroup,
            RequiresConfirmation: !canGroup,
            canGroup
                ? first.ParsedName.Kind == ParsedMediaKind.Episode
                    ? "Identification.Duplicate.SameEpisode"
                    : "Identification.Duplicate.SameMovie"
                : "Identification.Duplicate.NeedsReview",
            files.Select(file => file.MediaFileId).ToArray());
    }
}
