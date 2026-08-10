// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Domain.Personalization;

/// <summary>
/// One row per marked piece of content. Content nobody has marked simply has no row: an absent row is
/// the normal case, not a failure.
/// </summary>
public interface IPersonalStateRepository
{
    Task<PersonalState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonalState>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the state atomically, replacing any earlier row for the same content. The stamp is
    /// storage metadata rather than part of the state, which is why it arrives separately.
    /// </summary>
    Task SaveAsync(
        PersonalState state,
        DateTimeOffset updatedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the row. Removing content that was never marked is not an error.</summary>
    Task DeleteAsync(ContentKey content, CancellationToken cancellationToken = default);
}
