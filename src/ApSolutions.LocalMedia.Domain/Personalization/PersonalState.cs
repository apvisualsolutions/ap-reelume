// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Domain.Personalization;

/// <summary>
/// The rules personal marks follow. A rating is an integer from one to ten or nothing at all; there is
/// no half star and no zero, because zero and "not rated" would be indistinguishable.
/// </summary>
public static class PersonalStatePolicy
{
    public const int MinimumRating = 1;

    public const int MaximumRating = 10;

    /// <summary>True for a rating inside the accepted range, and for the absence of one.</summary>
    public static bool IsValidRating(int? rating) =>
        rating is null || (rating >= MinimumRating && rating <= MaximumRating);

    /// <summary>True when nothing is marked, which is what lets the row be dropped instead of stored.</summary>
    public static bool IsEmpty(PersonalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.IsEmpty;
    }
}

/// <summary>
/// Three independent facts a person can record about one piece of content: whether it is a favourite,
/// whether it is kept for later, and what they thought of it. Like progress, this belongs to the
/// content rather than to the file, so replacing a version keeps the marks.
/// </summary>
public sealed record PersonalState
{
    public required ContentKey Content { get; init; }

    public bool IsFavorite { get; init; }

    public bool IsWatchLater { get; init; }

    /// <summary>An integer from one to ten, or null when the person has not rated it.</summary>
    public int? Rating { get; init; }

    public bool HasRating => Rating is not null;

    /// <summary>True when none of the three facts is set.</summary>
    public bool IsEmpty => !IsFavorite && !IsWatchLater && Rating is null;

    public static PersonalState Empty(ContentKey content) => new() { Content = content };

    public PersonalState WithFavorite(bool isFavorite) =>
        isFavorite == IsFavorite ? this : this with { IsFavorite = isFavorite };

    public PersonalState WithWatchLater(bool isWatchLater) =>
        isWatchLater == IsWatchLater ? this : this with { IsWatchLater = isWatchLater };

    /// <summary>
    /// Records a rating, or clears it with null. A value outside one to ten is refused rather than
    /// clamped: a rating nobody chose would be worse than no rating at all.
    /// </summary>
    public PersonalState WithRating(int? rating)
    {
        if (!PersonalStatePolicy.IsValidRating(rating))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                rating,
                $"A rating must be between {PersonalStatePolicy.MinimumRating} and {PersonalStatePolicy.MaximumRating}, or absent.");
        }

        return rating == Rating ? this : this with { Rating = rating };
    }

    public PersonalState ToggleFavorite() => WithFavorite(!IsFavorite);

    public PersonalState ToggleWatchLater() => WithWatchLater(!IsWatchLater);
}
