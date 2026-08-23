// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Application.Catalog;

/// <summary>
/// One row of the duplicates destination: a title that resolves to more than one file, with enough
/// on it to be recognised and opened. The review itself stays where it always was — this is the
/// list that leads to it.
/// </summary>
public sealed record DuplicateOverviewEntry(
    TitleId TitleId,
    string Title,
    int VersionCount);

/// <summary>
/// Reads every group holding two or more versions, newest grammar of the rail's fifth destination.
/// A port rather than a repository method on the domain interface, because the answer joins the
/// catalogue for a display title — which is a read-model concern, the same shape
/// <see cref="ICatalogQueryService"/> already has.
/// </summary>
public interface IDuplicateOverviewReader
{
    Task<IReadOnlyList<DuplicateOverviewEntry>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>The use case the duplicates destination loads through.</summary>
public sealed class GetDuplicateOverview
{
    private readonly IDuplicateOverviewReader _reader;

    public GetDuplicateOverview(IDuplicateOverviewReader reader) =>
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));

    public Task<IReadOnlyList<DuplicateOverviewEntry>> ExecuteAsync(
        CancellationToken cancellationToken = default) => _reader.ListAsync(cancellationToken);
}
