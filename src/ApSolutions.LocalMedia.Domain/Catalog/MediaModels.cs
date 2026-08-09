namespace ApSolutions.LocalMedia.Domain.Catalog;

public sealed record TechnicalMetadata(
    TimeSpan? Duration,
    string Container,
    IReadOnlyList<string> VideoCodecs,
    IReadOnlyList<string> AudioCodecs,
    int? Width,
    int? Height);

public sealed record MediaFile(
    MediaFileId Id,
    LibraryRootId LibraryRootId,
    string Path,
    long SizeBytes,
    DateTimeOffset LastWriteUtc,
    TechnicalMetadata TechnicalMetadata,
    bool IsAvailable = true);
