// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Metadata;

public enum MetadataField
{
    Title,
    OriginalTitle,
    Overview,
    ReleaseYear,
    Genres,
    PosterPath,
    BackdropPath,
}

/// <param name="TrailerKey">
/// Provider data, and deliberately absent from <see cref="MetadataField"/>: that enumeration is the
/// list of what a person can edit and lock, and no surface edits a video identifier. A refresh
/// therefore replaces it whenever the provider offers one, the same way it would replace any field
/// nobody locked. The day an editor for it exists, it becomes lockable — and joins the merge above.
/// </param>
public sealed record EditableMetadata(
    string Title,
    string? OriginalTitle,
    string? Overview,
    int? ReleaseYear,
    IReadOnlyList<string> Genres,
    string? PosterPath,
    string? BackdropPath,
    string? TrailerKey,
    IReadOnlySet<MetadataField> LockedFields);

public sealed class MetadataMergePolicy
{
    private readonly EqualityComparer<MetadataField> _fieldComparer = EqualityComparer<MetadataField>.Default;

    public EditableMetadata Merge(EditableMetadata current, MetadataDetails remote)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(remote);

        return current with
        {
            Title = SelectText(MetadataField.Title, current.Title, remote.Title, current.LockedFields)!,
            OriginalTitle = SelectText(
                MetadataField.OriginalTitle,
                current.OriginalTitle,
                remote.OriginalTitle,
                current.LockedFields),
            Overview = SelectText(
                MetadataField.Overview,
                current.Overview,
                remote.Overview,
                current.LockedFields),
            ReleaseYear = IsLocked(current.LockedFields, MetadataField.ReleaseYear)
                ? current.ReleaseYear
                : remote.ReleaseYear ?? current.ReleaseYear,
            Genres = IsLocked(current.LockedFields, MetadataField.Genres) || remote.Genres.Count == 0
                ? current.Genres
                : [.. remote.Genres],
            PosterPath = SelectText(
                MetadataField.PosterPath,
                current.PosterPath,
                remote.PosterPath,
                current.LockedFields),
            BackdropPath = SelectText(
                MetadataField.BackdropPath,
                current.BackdropPath,
                remote.BackdropPath,
                current.LockedFields),
            TrailerKey = string.IsNullOrWhiteSpace(remote.TrailerKey)
                ? current.TrailerKey
                : remote.TrailerKey,
        };
    }

    private string? SelectText(
        MetadataField field,
        string? current,
        string? remote,
        IReadOnlySet<MetadataField> lockedFields) =>
        IsLocked(lockedFields, field) || string.IsNullOrWhiteSpace(remote) ? current : remote;

    private bool IsLocked(IReadOnlySet<MetadataField> lockedFields, MetadataField field) =>
        lockedFields.Any(locked => _fieldComparer.Equals(locked, field));
}
