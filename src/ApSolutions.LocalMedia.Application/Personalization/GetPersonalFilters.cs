// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;

namespace ApSolutions.LocalMedia.Application.Personalization;

/// <summary>
/// Reads personal marks and turns the chosen ones into catalogue filters. There is no list here and
/// no collection: a filter is a question about the marks, not a stored grouping.
/// </summary>
public sealed class GetPersonalFilters
{
    private readonly IPersonalStateRepository _repository;

    public GetPersonalFilters(IPersonalStateRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <summary>The marks on one piece of content; unmarked content reads as an empty state.</summary>
    public async Task<PersonalState> GetAsync(
        ContentKey content,
        CancellationToken cancellationToken = default) =>
        await _repository.GetAsync(content, cancellationToken).ConfigureAwait(false)
        ?? PersonalState.Empty(content);

    /// <summary>Everything that carries at least one mark.</summary>
    public Task<IReadOnlyList<PersonalState>> GetMarkedAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAllAsync(cancellationToken);

    /// <summary>Combines the three switches into the catalogue's filter flags.</summary>
    public static CatalogFilter ToFilter(bool favorites, bool watchLater, bool rated)
    {
        var filter = CatalogFilter.None;
        if (favorites)
        {
            filter |= CatalogFilter.Favorite;
        }

        if (watchLater)
        {
            filter |= CatalogFilter.WatchLater;
        }

        if (rated)
        {
            filter |= CatalogFilter.Rated;
        }

        return filter;
    }
}
