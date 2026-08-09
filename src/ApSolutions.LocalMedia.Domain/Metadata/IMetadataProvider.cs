namespace ApSolutions.LocalMedia.Domain.Metadata;

public enum MetadataContentKind
{
    Movie,
    Show,
}

public sealed record MetadataLanguage(string Primary, string? Fallback)
{
    public IReadOnlyList<string> OrderedValues()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Primary);
        if (string.IsNullOrWhiteSpace(Fallback)
            || string.Equals(Primary, Fallback, StringComparison.OrdinalIgnoreCase))
        {
            return [Primary];
        }

        return [Primary, Fallback];
    }
}

public sealed record MetadataSearchQuery(
    string Title,
    int? Year,
    MetadataContentKind Kind);

public sealed record MetadataReference(
    string Provider,
    string Key,
    MetadataContentKind Kind);

public sealed record MetadataSearchResult(
    MetadataReference Reference,
    string Language,
    string Title,
    string? OriginalTitle,
    int? ReleaseYear);

public sealed record MetadataDetails(
    MetadataReference Reference,
    string Language,
    string Title,
    string? OriginalTitle,
    string? Overview,
    int? ReleaseYear,
    IReadOnlyList<string> Genres,
    string? PosterPath,
    string? BackdropPath);

public interface IMetadataProvider
{
    Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        MetadataSearchQuery query,
        MetadataLanguage language,
        CancellationToken cancellationToken = default);

    Task<MetadataDetails?> GetDetailsAsync(
        MetadataReference reference,
        MetadataLanguage language,
        CancellationToken cancellationToken = default);
}
