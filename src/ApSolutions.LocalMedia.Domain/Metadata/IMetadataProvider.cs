// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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

/// <param name="TrailerKey">
/// The provider's own identifier for a trailer on YouTube, never an address. What a string that
/// arrived over the network is allowed to become is decided by <see cref="TrailerLinkPolicy"/>, and
/// nothing else composes a link from it.
/// </param>
public sealed record MetadataDetails(
    MetadataReference Reference,
    string Language,
    string Title,
    string? OriginalTitle,
    string? Overview,
    int? ReleaseYear,
    IReadOnlyList<string> Genres,
    string? PosterPath,
    string? BackdropPath,
    string? TrailerKey);

public interface IMetadataProvider
{
    /// <summary>
    /// The name this provider stamps on every reference it produces, and the one a stored
    /// identification is recorded under. It is exposed rather than repeated as a literal because a
    /// reference whose provider does not match is rejected by the provider itself, so two copies of
    /// the same string are two chances to write rows that can never be refreshed.
    /// </summary>
    string Name { get; }

    Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        MetadataSearchQuery query,
        MetadataLanguage language,
        CancellationToken cancellationToken = default);

    Task<MetadataDetails?> GetDetailsAsync(
        MetadataReference reference,
        MetadataLanguage language,
        CancellationToken cancellationToken = default);
}
