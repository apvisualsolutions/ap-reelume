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

/// <remarks>
/// There is no language on an answer, and its absence is a measurement. The field that used to sit
/// here held the language that was *asked for*, not the one the answer came back in — TMDB serves a
/// title in whatever it has when the requested language has no translation — so a reader would have
/// been told the opposite of what the name promised. Nothing in src/ ever read it. It goes rather
/// than stays as a claim the provider cannot make.
/// </remarks>
public sealed record MetadataSearchResult(
    MetadataReference Reference,
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

    /// <summary>
    /// The reference a stored key stands for, or nothing when the key is not one of this provider's.
    /// <para>
    /// A reference carries a content kind that the catalogue does not store, because which kind a key
    /// belongs to is written inside the key's own format — and that format is the provider's, not the
    /// database's. Reading it here is what keeps a refresh from having to know that a TMDB film key
    /// starts with <c>movie:</c>.
    /// </para>
    /// </summary>
    MetadataReference? TryCreateReference(string key);

    Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
        MetadataSearchQuery query,
        MetadataLanguage language,
        CancellationToken cancellationToken = default);

    Task<MetadataDetails?> GetDetailsAsync(
        MetadataReference reference,
        MetadataLanguage language,
        CancellationToken cancellationToken = default);
}
